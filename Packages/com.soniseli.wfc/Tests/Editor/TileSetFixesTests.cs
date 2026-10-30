using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using WfcStudio.Core;
using WfcStudio.Editor;

namespace WfcStudio.Tests
{
    public class TileSetFixesTests
    {
        readonly List<Object> _created = new List<Object>();
        Tile _grass;
        Tile _water;
        TileSet _tileSet;

        [SetUp]
        public void SetUp()
        {
            _grass = Track(ScriptableObject.CreateInstance<Tile>());
            _water = Track(ScriptableObject.CreateInstance<Tile>());
            _tileSet = Track(ScriptableObject.CreateInstance<TileSet>());
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearUndo(_tileSet);
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        static TileEntry Entry(string name, TileBase tile, float weight, string socket)
        {
            return new TileEntry(name, tile, weight, socket, socket, socket, socket);
        }

        TileSetFix FixFor(IssueCode code)
        {
            var issue = _tileSet.Compile().Issues.First(i => i.Code == code);
            return TileSetFixes.For(_tileSet, issue);
        }

        [Test]
        public void InvalidWeight_IsSetToOne_AndUndoRestoresIt()
        {
            _tileSet.SetContents(new[] { new SocketDefinition("grass") }, new[] { Entry("Grass", _grass, 0f, "grass") });

            FixFor(IssueCode.InvalidWeight).Apply();
            Assert.That(_tileSet.Tiles[0].Weight, Is.EqualTo(1f));
            Assert.That(_tileSet.Compile().HasErrors, Is.False);

            Undo.PerformUndo();
            Assert.That(_tileSet.Tiles[0].Weight, Is.EqualTo(0f));
        }

        [Test]
        public void DuplicateTile_RemovesTheRepeatedEntry()
        {
            _tileSet.SetContents(new[] { new SocketDefinition("grass") },
                new[] { Entry("Grass", _grass, 1f, "grass"), Entry("Grass again", _grass, 2f, "grass") });

            FixFor(IssueCode.DuplicateTile).Apply();

            Assert.That(_tileSet.Tiles.Select(t => t.DisplayName), Is.EqualTo(new[] { "Grass" }));
        }

        [Test]
        public void UnknownSocket_AddsEveryMissingLabel()
        {
            _tileSet.SetContents(new[] { new SocketDefinition("grass", true, "shore") },
                new[] { Entry("Grass", _grass, 1f, "grass"), Entry("Water", _water, 1f, "water") });

            FixFor(IssueCode.UnknownSocket).Apply();

            Assert.That(_tileSet.Sockets.Select(s => s.Name), Is.EquivalentTo(new[] { "grass", "water", "shore" }));
            Assert.That(_tileSet.Compile().HasErrors, Is.False);
        }

        [Test]
        public void UnusedSocket_IsRemoved_WithConnectionsToIt()
        {
            _tileSet.SetContents(new[] { new SocketDefinition("grass", true, "sand"), new SocketDefinition("sand") },
                new[] { Entry("Grass", _grass, 1f, "grass") });

            FixFor(IssueCode.UnusedSocket).Apply();

            Assert.That(_tileSet.Sockets.Select(s => s.Name), Is.EqualTo(new[] { "grass" }));
            Assert.That(_tileSet.Sockets[0].ConnectsTo, Is.Empty);
            Assert.That(_tileSet.Compile().Issues, Is.Empty);
        }

        [Test]
        public void BrokenException_IsRemoved()
        {
            var outsider = Track(ScriptableObject.CreateInstance<Tile>());
            _tileSet.SetContents(new[] { new SocketDefinition("grass") }, new[] { Entry("Grass", _grass, 1f, "grass") },
                new[] { new AdjacencyException(ExceptionKind.Forbid, _grass, ExceptionDirection.Any, outsider) });

            FixFor(IssueCode.UnknownExceptionTile).Apply();

            Assert.That(_tileSet.Exceptions, Is.Empty);
        }

        [Test]
        public void RedundantExceptions_AreRemoved_ButEffectiveOnesKept()
        {
            var sockets = new[] { new SocketDefinition("grass"), new SocketDefinition("water") };
            var tiles = new[] { Entry("Grass", _grass, 1f, "grass"), Entry("Water", _water, 1f, "water") };
            var redundant = new AdjacencyException(ExceptionKind.Forbid, _grass, ExceptionDirection.Right, _water);
            var effective = new AdjacencyException(ExceptionKind.Forbid, _grass, ExceptionDirection.Up, _grass);
            _tileSet.SetContents(sockets, tiles, new[] { redundant, effective });
            var before = _tileSet.Compile().Rules;

            FixFor(IssueCode.RedundantException).Apply();

            Assert.That(_tileSet.Exceptions, Is.EqualTo(new[] { effective }));
            var after = _tileSet.Compile().Rules;
            Assert.That(after.Allows(0, Direction.Up, 0), Is.EqualTo(before.Allows(0, Direction.Up, 0)));
        }

        [Test]
        public void DesignProblems_GetNoAutomaticFix()
        {
            _tileSet.SetContents(new[] { new SocketDefinition("grass"), new SocketDefinition("cliff", false) },
                new[] { Entry("Grass", _grass, 1f, "grass"), new TileEntry("Cliff", _water, 1f, "cliff", "grass", "grass", "grass") });

            var noNeighbour = _tileSet.Compile().Issues.First(i => i.Code == IssueCode.NoNeighbour);

            Assert.That(TileSetFixes.For(_tileSet, noNeighbour), Is.Null);
        }
    }
}
