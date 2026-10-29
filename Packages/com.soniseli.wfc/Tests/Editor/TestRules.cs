using NUnit.Framework;
using WfcStudio.Core;

namespace WfcStudio.Tests
{
    // Small rule sets and assertions shared by the solver tests.
    static class TestRules
    {
        public const int Grass = 0;
        public const int Sand = 1;
        public const int Water = 2;

        /// <summary>
        /// Grass, sand and water. Each sits next to itself; sand sits next to both others; grass never touches water.
        /// Every combination of neighbours leaves sand possible, so this set can never contradict.
        /// </summary>
        public static RuleTable Terrain()
        {
            var builder = new RuleTable.Builder(3);
            AllowEverywhere(builder, Grass, Grass);
            AllowEverywhere(builder, Sand, Sand);
            AllowEverywhere(builder, Water, Water);
            AllowEverywhere(builder, Grass, Sand);
            AllowEverywhere(builder, Sand, Water);
            return builder.Build();
        }

        /// <summary>
        /// Grid colouring: neighbours must differ. Always solvable without wrapping (a checkerboard),
        /// but with three or more colours a cell can end up with every colour around it, so greedy collapse contradicts.
        /// </summary>
        public static RuleTable Colouring(int colours)
        {
            var builder = new RuleTable.Builder(colours);
            for (int a = 0; a < colours; a++)
                for (int b = a + 1; b < colours; b++)
                    AllowEverywhere(builder, a, b);
            return builder.Build();
        }

        /// <summary>Every tile may sit next to every tile, so only weights shape the map.</summary>
        public static RuleTable.Builder Unconstrained(int tileCount)
        {
            var builder = new RuleTable.Builder(tileCount);
            for (int a = 0; a < tileCount; a++)
                for (int b = a; b < tileCount; b++)
                    AllowEverywhere(builder, a, b);
            return builder;
        }

        public static void AllowEverywhere(RuleTable.Builder builder, int a, int b)
        {
            for (int d = 0; d < DirectionExtensions.Count; d++)
                builder.Allow(a, (Direction)d, b);
        }

        public static int[] Tiles(WfcSolver solver)
        {
            var tiles = new int[solver.CellCount];
            for (int cell = 0; cell < tiles.Length; cell++)
                tiles[cell] = solver.GetTile(cell);
            return tiles;
        }

        /// <summary>
        /// Asserts every cell is collapsed and every pair of neighbours is allowed by the rules,
        /// including pairs across the edges when <paramref name="wrapEdges"/> is true.
        /// </summary>
        public static void AssertValidMap(WfcSolver solver, RuleTable rules, bool wrapEdges = false)
        {
            for (int y = 0; y < solver.Height; y++)
            {
                for (int x = 0; x < solver.Width; x++)
                {
                    int tile = solver.GetTile(solver.CellIndex(x, y));
                    Assert.That(tile, Is.Not.EqualTo(-1), $"Cell ({x}, {y}) is not collapsed.");

                    if (x + 1 < solver.Width || wrapEdges)
                    {
                        int right = solver.GetTile(solver.CellIndex((x + 1) % solver.Width, y));
                        Assert.That(rules.Allows(tile, Direction.Right, right), $"Tile {right} may not sit right of tile {tile} at ({x}, {y}).");
                    }
                    if (y + 1 < solver.Height || wrapEdges)
                    {
                        int up = solver.GetTile(solver.CellIndex(x, (y + 1) % solver.Height));
                        Assert.That(rules.Allows(tile, Direction.Up, up), $"Tile {up} may not sit above tile {tile} at ({x}, {y}).");
                    }
                }
            }
        }
    }
}
