using System;
using System.Collections.Generic;
using WfcStudio.Core;

namespace WfcStudio.Editor
{
    /// <summary>Questions the editor asks about a tile set, kept apart from the UI so they can be tested.</summary>
    internal static class TileSetQueries
    {
        /// <summary>Tiles that may sit next to <paramref name="tile"/> in <paramref name="direction"/>, in tile set order.</summary>
        public static List<int> Neighbours(RuleTable rules, int tile, Direction direction)
        {
            var neighbours = new List<int>();
            for (int other = 0; other < rules.TileCount; other++)
                if (rules.Allows(tile, direction, other))
                    neighbours.Add(other);
            return neighbours;
        }

        /// <summary>
        /// The tile's share of the set's total weight, from 0 to 1. Only a rough guide to how often it appears, since
        /// the rules decide where a tile may go at all.
        /// </summary>
        public static float WeightShare(IReadOnlyList<TileEntry> tiles, int tile)
        {
            float total = 0f;
            foreach (var entry in tiles)
                total += Math.Max(0f, entry.Weight);
            return total > 0f ? Math.Max(0f, tiles[tile].Weight) / total : 0f;
        }

        /// <summary>Indices of the tiles whose name contains <paramref name="search"/>, ignoring case. Empty search matches all.</summary>
        public static List<int> Filter(IReadOnlyList<TileEntry> tiles, string search)
        {
            var matches = new List<int>();
            for (int i = 0; i < tiles.Count; i++)
                if (string.IsNullOrWhiteSpace(search) || tiles[i].DisplayName.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                    matches.Add(i);
            return matches;
        }
    }
}
