using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using WfcStudio.Core;

namespace WfcStudio.Samples.Tests
{
    // Every sample tile set must validate cleanly and generate valid maps, with and without wrapping edges.
    public class SampleTileSetTests
    {
        const string SamplesFolder = "Assets/Samples";

        static IEnumerable<string> SampleTileSets()
        {
            return AssetDatabase.FindAssets("t:TileSet", new[] { SamplesFolder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path);
        }

        static TileSet Load(string path)
        {
            return AssetDatabase.LoadAssetAtPath<TileSet>(path);
        }

        [Test]
        public void AllSixPunyWorldSetsExist()
        {
            Assert.That(SampleTileSets().Count(), Is.EqualTo(6));
        }

        [TestCaseSource(nameof(SampleTileSets))]
        public void CompilesWithoutWarnings(string path)
        {
            var result = Load(path).Compile();

            var problems = result.Issues.Where(issue => issue.Severity != IssueSeverity.Info).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [TestCaseSource(nameof(SampleTileSets))]
        public void GeneratesValidMaps(string path)
        {
            var rules = Load(path).Compile().Rules;
            foreach (bool wrap in new[] { false, true })
            {
                for (uint seed = 0; seed < 5; seed++)
                {
                    var solver = new WfcSolver(rules, 30, 20, seed, new SolverOptions { WrapEdges = wrap });

                    Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Completed), $"Seed {seed}, wrap {wrap}");
                    AssertNeighboursAllowed(solver, rules, wrap);
                }
            }
        }

        static void AssertNeighboursAllowed(WfcSolver solver, RuleTable rules, bool wrap)
        {
            for (int y = 0; y < solver.Height; y++)
            {
                for (int x = 0; x < solver.Width; x++)
                {
                    int tile = solver.GetTile(solver.CellIndex(x, y));
                    if (x + 1 < solver.Width || wrap)
                        Assert.That(rules.Allows(tile, Direction.Right, solver.GetTile(solver.CellIndex((x + 1) % solver.Width, y))), $"({x}, {y}) right");
                    if (y + 1 < solver.Height || wrap)
                        Assert.That(rules.Allows(tile, Direction.Up, solver.GetTile(solver.CellIndex(x, (y + 1) % solver.Height))), $"({x}, {y}) up");
                }
            }
        }
    }
}
