using System;
using System.Linq;
using NUnit.Framework;
using WfcStudio.Core;
using static WfcStudio.Tests.TestRules;

namespace WfcStudio.Tests
{
    public class WfcSolverTests
    {
        [Test]
        public void CompletesWithEveryRuleSatisfied_OverManySeeds()
        {
            var rules = Terrain();
            for (uint seed = 0; seed < 50; seed++)
            {
                var solver = new WfcSolver(rules, 12, 9, seed);

                Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Completed), $"Seed {seed}");
                AssertValidMap(solver, rules);
            }
        }

        [TestCase(1, 1)]
        [TestCase(1, 8)]
        [TestCase(8, 1)]
        [TestCase(7, 3)]
        public void HandlesNonSquareGrids(int width, int height)
        {
            var rules = Terrain();
            var solver = new WfcSolver(rules, width, height, 3);

            Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Completed));
            AssertValidMap(solver, rules);
        }

        [Test]
        public void SameSeed_GivesSameMap()
        {
            var rules = Terrain();
            var first = new WfcSolver(rules, 16, 16, 99);
            var second = new WfcSolver(rules, 16, 16, 99);
            first.Run();
            second.Run();

            Assert.That(Tiles(second), Is.EqualTo(Tiles(first)));
        }

        [Test]
        public void DifferentSeeds_GiveDifferentMaps()
        {
            var rules = Terrain();
            var reference = new WfcSolver(rules, 16, 16, 1);
            reference.Run();

            bool anyDifferent = false;
            for (uint seed = 2; seed < 6 && !anyDifferent; seed++)
            {
                var other = new WfcSolver(rules, 16, 16, seed);
                other.Run();
                anyDifferent = !Tiles(other).SequenceEqual(Tiles(reference));
            }

            Assert.That(anyDifferent, Is.True);
        }

        [Test]
        public void Weights_ShapeTileFrequency()
        {
            var rules = Unconstrained(2).SetWeight(0, 9.0).SetWeight(1, 1.0).Build();
            var solver = new WfcSolver(rules, 30, 30, 5);
            solver.Run();

            int common = 0;
            foreach (int tile in Tiles(solver))
                if (tile == 0)
                    common++;

            double share = common / (double)solver.CellCount;
            Assert.That(share, Is.InRange(0.85, 0.95));
        }

        [Test]
        public void InitialEntropy_MatchesWeightedShannonFormula()
        {
            var even = new WfcSolver(Unconstrained(2).Build(), 2, 2, 0);
            Assert.That(even.GetEntropy(0), Is.EqualTo(Math.Log(2)).Within(1e-5));

            // Weights 3 and 1: log(4) - (3 log 3) / 4.
            var uneven = new WfcSolver(Unconstrained(2).SetWeight(0, 3.0).Build(), 2, 2, 0);
            Assert.That(uneven.GetEntropy(0), Is.EqualTo(Math.Log(4) - 3 * Math.Log(3) / 4).Within(1e-5));
        }

        [Test]
        public void CollapsedCells_HaveZeroEntropy()
        {
            var solver = new WfcSolver(Terrain(), 4, 4, 0);
            var result = solver.Step();

            Assert.That(solver.IsCollapsed(result.Cell), Is.True);
            Assert.That(solver.GetEntropy(result.Cell), Is.EqualTo(0f));
        }

        [Test]
        public void Step_ReportsCollapsedCellAndChanges()
        {
            var solver = new WfcSolver(Terrain(), 5, 5, 11);
            var result = solver.Step();

            Assert.That(result.Status, Is.EqualTo(SolverStatus.Running));
            Assert.That(solver.GetTile(result.Cell), Is.EqualTo(result.Tile));
            Assert.That(solver.ChangedCells.ToArray(), Does.Contain(result.Cell));
            Assert.That(solver.Steps, Is.EqualTo(1));
        }

        [Test]
        public void Step_AfterCompletion_ReportsCompletedWithoutCollapsing()
        {
            var solver = new WfcSolver(Terrain(), 3, 3, 0);
            solver.Run();
            int steps = solver.Steps;

            var result = solver.Step();

            Assert.That(result.Status, Is.EqualTo(SolverStatus.Completed));
            Assert.That(result.Cell, Is.EqualTo(-1));
            Assert.That(solver.Steps, Is.EqualTo(steps));
        }

        [Test]
        public void Run_StopsAtMaxSteps()
        {
            var solver = new WfcSolver(Unconstrained(3).Build(), 10, 10, 0);

            Assert.That(solver.Run(5), Is.EqualTo(SolverStatus.Running));
            Assert.That(solver.Steps, Is.EqualTo(5));
        }

        [Test]
        public void SingleTileSet_IsCompleteImmediately()
        {
            var rules = Unconstrained(1).Build();
            var solver = new WfcSolver(rules, 4, 4, 0);

            Assert.That(solver.Status, Is.EqualTo(SolverStatus.Completed));
            AssertValidMap(solver, rules);
        }

        [Test]
        public void TilesThatFitNowhere_ArePrunedBeforeTheFirstStep()
        {
            // Tile 0 has no allowed neighbours, so on a grid every cell must be tile 1.
            var builder = new RuleTable.Builder(2);
            AllowEverywhere(builder, 1, 1);
            var solver = new WfcSolver(builder.Build(), 3, 3, 0);

            Assert.That(solver.Status, Is.EqualTo(SolverStatus.Completed));
            Assert.That(solver.Steps, Is.EqualTo(0));
            Assert.That(Tiles(solver), Is.All.EqualTo(1));
        }

        [Test]
        public void UnsatisfiableRules_FailAtConstruction()
        {
            // A single tile that may not sit next to itself cannot fill a 2x1 grid.
            var solver = new WfcSolver(new RuleTable.Builder(1).Build(), 2, 1, 0);

            Assert.That(solver.Status, Is.EqualTo(SolverStatus.Failed));
            Assert.That(solver.Step().Status, Is.EqualTo(SolverStatus.Failed));
        }

        [Test]
        public void PinnedTiles_AreKept_AndConstrainTheirNeighbours()
        {
            var rules = Terrain();
            for (uint seed = 0; seed < 20; seed++)
            {
                var solver = new WfcSolver(rules, 7, 7, seed);
                solver.Pin(3, 3, Water);
                solver.Pin(0, 0, Grass);

                Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Completed), $"Seed {seed}");
                Assert.That(solver.GetTile(solver.CellIndex(3, 3)), Is.EqualTo(Water));
                Assert.That(solver.GetTile(solver.CellIndex(0, 0)), Is.EqualTo(Grass));
                AssertValidMap(solver, rules);
            }
        }

        [Test]
        public void ConflictingPins_FailTheSolver()
        {
            var solver = new WfcSolver(Terrain(), 4, 4, 0);
            solver.Pin(1, 1, Grass);
            solver.Pin(2, 1, Water);

            Assert.That(solver.Status, Is.EqualTo(SolverStatus.Failed));
        }

        [Test]
        public void Pin_AfterStep_Throws()
        {
            var solver = new WfcSolver(Terrain(), 4, 4, 0);
            solver.Step();

            Assert.Throws<InvalidOperationException>(() => solver.Pin(0, 0, Grass));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            var rules = Terrain();
            Assert.Throws<ArgumentNullException>(() => new WfcSolver(null, 4, 4, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WfcSolver(rules, 0, 4, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WfcSolver(rules, 4, 0, 0));

            var solver = new WfcSolver(rules, 4, 4, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => solver.Pin(4, 0, Grass));
            Assert.Throws<ArgumentOutOfRangeException>(() => solver.Pin(0, -1, Grass));
            Assert.Throws<ArgumentOutOfRangeException>(() => solver.Pin(0, 0, 3));
        }
    }
}
