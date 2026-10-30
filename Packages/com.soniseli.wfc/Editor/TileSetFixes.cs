using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Tilemaps;
using WfcStudio.Core;

namespace WfcStudio.Editor
{
    /// <summary>A one-click repair for a validation issue. <see cref="Apply"/> records Undo.</summary>
    internal sealed class TileSetFix
    {
        public TileSetFix(string label, string undoName, Action apply)
        {
            Label = label;
            UndoName = undoName;
            Apply = apply;
        }

        public string Label { get; }

        public string UndoName { get; }

        public Action Apply { get; }
    }

    /// <summary>
    /// Repairs for the validation issues that have one obvious fix. Issues that need a design decision (a tile with no
    /// neighbour, conflicting exceptions) get none. Fixes find their targets in the tile set itself, so the same fix
    /// works whichever of several matching issues it is offered on.
    /// </summary>
    internal static class TileSetFixes
    {
        static readonly Direction[] AllDirections = { Direction.Up, Direction.Right, Direction.Down, Direction.Left };

        public static TileSetFix For(TileSet tileSet, ValidationIssue issue)
        {
            switch (issue.Code)
            {
                case IssueCode.InvalidWeight when IsEntry(tileSet, issue.Tile):
                    int weightTile = issue.Tile;
                    return Fix("Set weight to 1", "Fix Tile Weight", tileSet, () => SetWeight(tileSet, weightTile, 1f));
                case IssueCode.MissingTile when IsEntry(tileSet, issue.Tile):
                case IssueCode.DuplicateTile when IsEntry(tileSet, issue.Tile):
                    int entry = issue.Tile;
                    return Fix("Remove entry", "Remove Tile Entry", tileSet, () => RemoveEntry(tileSet, entry));
                case IssueCode.UnknownSocket:
                    return Fix("Add missing sockets", "Add Missing Sockets", tileSet, () => AddMissingSockets(tileSet));
                case IssueCode.UnusedSocket:
                    return Fix("Remove unused sockets", "Remove Unused Sockets", tileSet, () => RemoveUnusedSockets(tileSet));
                case IssueCode.UnnamedSocket:
                    return Fix("Remove unnamed sockets", "Remove Unnamed Sockets", tileSet, () => RemoveUnnamedSockets(tileSet));
                case IssueCode.UnknownExceptionTile:
                    return Fix("Remove broken exceptions", "Remove Broken Exceptions", tileSet, () => RemoveBrokenExceptions(tileSet));
                case IssueCode.RedundantException:
                    return Fix("Remove redundant exceptions", "Remove Redundant Exceptions", tileSet, () => RemoveRedundantExceptions(tileSet));
                default:
                    return null;
            }
        }

        public static void SetWeight(TileSet tileSet, int tile, float weight)
        {
            // Through SerializedObject, so only the weight changes; the caller records Undo.
            var serialized = new SerializedObject(tileSet);
            serialized.FindProperty("_tiles").GetArrayElementAtIndex(tile).FindPropertyRelative("_weight").floatValue = weight;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void RemoveEntry(TileSet tileSet, int tile)
        {
            var tiles = tileSet.Tiles.ToList();
            tiles.RemoveAt(tile);
            tileSet.SetContents(tileSet.Sockets, tiles, tileSet.Exceptions);
        }

        /// <summary>Defines every socket label that tile edges or socket connections use but the set doesn't define.</summary>
        public static void AddMissingSockets(TileSet tileSet)
        {
            var sockets = tileSet.Sockets.ToList();
            var defined = new HashSet<string>(sockets.Select(s => s.Name).Where(name => !string.IsNullOrEmpty(name)));
            var used = tileSet.Tiles.SelectMany(t => AllDirections.Select(t.GetSocket))
                .Concat(sockets.SelectMany(s => s.ConnectsTo))
                .ToList();
            foreach (string label in used)
                if (!string.IsNullOrEmpty(label) && defined.Add(label))
                    sockets.Add(new SocketDefinition(label));
            tileSet.SetContents(sockets, tileSet.Tiles, tileSet.Exceptions);
        }

        /// <summary>Removes sockets no tile edge uses, and drops connections that point at them.</summary>
        public static void RemoveUnusedSockets(TileSet tileSet)
        {
            var used = new HashSet<string>(tileSet.Tiles.SelectMany(t => AllDirections.Select(t.GetSocket)).Where(label => !string.IsNullOrEmpty(label)));
            var sockets = tileSet.Sockets
                .Where(s => string.IsNullOrEmpty(s.Name) || used.Contains(s.Name))
                .Select(s => new SocketDefinition(s.Name, s.ConnectsToSelf, s.ConnectsTo.Where(used.Contains).ToArray()));
            tileSet.SetContents(sockets.ToList(), tileSet.Tiles, tileSet.Exceptions);
        }

        public static void RemoveUnnamedSockets(TileSet tileSet)
        {
            tileSet.SetContents(tileSet.Sockets.Where(s => !string.IsNullOrEmpty(s.Name)).ToList(), tileSet.Tiles, tileSet.Exceptions);
        }

        /// <summary>Removes exceptions with a missing tile, or a tile that isn't in the set.</summary>
        public static void RemoveBrokenExceptions(TileSet tileSet)
        {
            var inSet = new HashSet<TileBase>(tileSet.Tiles.Select(t => t.Tile).Where(t => t != null));
            var kept = tileSet.Exceptions.Where(e => e.Tile != null && e.Neighbour != null && inSet.Contains(e.Tile) && inSet.Contains(e.Neighbour));
            tileSet.SetContents(tileSet.Sockets, tileSet.Tiles, kept.ToList());
        }

        /// <summary>
        /// Removes exceptions whose removal leaves the compiled rules unchanged. Checked one at a time against the set as it
        /// stands, so an exception that only part of the time repeats what sockets already say is kept.
        /// </summary>
        public static void RemoveRedundantExceptions(TileSet tileSet)
        {
            var reference = tileSet.Compile().Rules;
            if (reference == null)
                return;

            var exceptions = tileSet.Exceptions.ToList();
            for (int i = exceptions.Count - 1; i >= 0; i--)
            {
                var without = new List<AdjacencyException>(exceptions);
                without.RemoveAt(i);
                tileSet.SetContents(tileSet.Sockets, tileSet.Tiles, without);
                var rules = tileSet.Compile().Rules;
                if (rules != null && SameRules(reference, rules))
                    exceptions = without;
            }
            tileSet.SetContents(tileSet.Sockets, tileSet.Tiles, exceptions);
        }

        static TileSetFix Fix(string label, string undoName, TileSet tileSet, Action change)
        {
            return new TileSetFix(label, undoName, () =>
            {
                Undo.IncrementCurrentGroup();
                Undo.RecordObject(tileSet, undoName);
                change();
                Undo.FlushUndoRecordObjects();
                EditorUtility.SetDirty(tileSet);
            });
        }

        static bool IsEntry(TileSet tileSet, int tile)
        {
            return tile >= 0 && tile < tileSet.Tiles.Count;
        }

        static bool SameRules(RuleTable a, RuleTable b)
        {
            if (a.TileCount != b.TileCount)
                return false;

            for (int tile = 0; tile < a.TileCount; tile++)
                foreach (var direction in AllDirections)
                    for (int neighbour = 0; neighbour < a.TileCount; neighbour++)
                        if (a.Allows(tile, direction, neighbour) != b.Allows(tile, direction, neighbour))
                            return false;
            return true;
        }
    }
}
