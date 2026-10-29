using System;
using System.Linq;
using NUnit.Framework;
using WfcStudio.Core;
using static WfcStudio.Tests.TestRules;

namespace WfcStudio.Tests
{
    public class BacktrackingTests
    {
        const int Seeds = 40;

        [Test]
        public void DefaultOptions_SolveEverySeed()
        {
            var rules = Colouring(3);
            int totalBacktracks = 0;
            for (uint seed = 0; seed < Seeds; seed++)
            {
                var solver = new WfcSolver(rules, 12, 12, seed);

                Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Completed), $"Seed {seed}");
                AssertValidMap(solver, rules);
                totalBacktracks += solver.Backtracks;
            }

            // The colouring set must actually exercise backtracking, or this test proves nothing.
            Assert.That(totalBacktracks, Is.GreaterThan(0));
        }

        [Test]
        public void BacktrackingAlone_IsComplete()
        {
            // With no restarts, backtracking must still find a solution for every seed if one exists.
            // Some seeds thrash (thousands of backtracks); that is what the restart fallback is for.
            var rules = Colouring(3);
            var options = new SolverOptions { MaxBacktracks = int.MaxValue, MaxRestarts = 0 };
            for (uint seed = 0; seed < Seeds; seed++)
            {
                var solver = new WfcSolver(rules, 12, 12, seed, options);

                Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Completed), $"Seed {seed}");
                AssertValidMap(solver, rules);
                Assert.That(solver.Restarts, Is.EqualTo(0));
            }
        }

        [Test]
        public void WithoutBacktracking_SomeSeedsFail()
        {
            var rules = Colouring(3);
            var options = new SolverOptions { MaxBacktracks = 0, MaxRestarts = 0 };

            bool anyFailed = Enumerable.Range(0, Seeds)
                .Any(seed => new WfcSolver(rules, 12, 12, (uint)seed, options).Run() == SolverStatus.Failed);

            Assert.That(anyFailed, Is.True);
        }

        [Test]
        public void Restarts_SolveMapsWhenBacktrackingIsOff()
        {
            var rules = Colouring(3);
            int totalRestarts = 0;
            for (uint seed = 0; seed < Seeds; seed++)
            {
                var solver = new WfcSolver(rules, 8, 8, seed, new SolverOptions { MaxBacktracks = 0, MaxRestarts = 200 });

                Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Completed), $"Seed {seed}");
                AssertValidMap(solver, rules);
                Assert.That(solver.Backtracks, Is.EqualTo(0));
                totalRestarts += solver.Restarts;
            }

            Assert.That(totalRestarts, Is.GreaterThan(0));
        }

        [Test]
        public void SameSeed_GivesSameMapAndSameBacktracks()
        {
            var rules = Colouring(3);
            var first = new WfcSolver(rules, 16, 16, 7);
            var second = new WfcSolver(rules, 16, 16, 7);
            first.Run();
            second.Run();

            Assert.That(Tiles(second), Is.EqualTo(Tiles(first)));
            Assert.That(second.Backtracks, Is.EqualTo(first.Backtracks));
            Assert.That(second.Restarts, Is.EqualTo(first.Restarts));
        }

        [Test]
        public void BacktrackedSteps_ReportTheRestoredCell()
        {
            var rules = Colouring(3);
            bool sawBacktrack = false;
            for (uint seed = 0; seed < Seeds && !sawBacktrack; seed++)
            {
                var solver = new WfcSolver(rules, 12, 12, seed);
                while (solver.Status == SolverStatus.Running)
                {
                    int backtracksBefore = solver.Backtracks;
                    var result = solver.Step();
                    if (!result.Backtracked)
                        continue;

                    sawBacktrack = true;
                    Assert.That(solver.ChangedCells.ToArray(), Does.Contain(result.Cell));

                    // After a single backtrack the ban is still in place. Backing up further would undo it.
                    if (solver.Backtracks == backtracksBefore + 1 && solver.Restarts == 0)
                        Assert.That(solver.IsPossible(result.Cell, result.Tile), Is.False, "The failed tile should be banned in that cell.");
                    break;
                }
            }

            Assert.That(sawBacktrack, Is.True);
        }

        [Test]
        public void Pins_SurviveBacktracking()
        {
            var rules = Colouring(3);
            for (uint seed = 0; seed < Seeds; seed++)
            {
                var solver = new WfcSolver(rules, 10, 10, seed);
                solver.Pin(0, 0, 0);
                solver.Pin(5, 5, 1);
                solver.Pin(9, 2, 2);

                Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Completed), $"Seed {seed}");
                Assert.That(solver.GetTile(solver.CellIndex(0, 0)), Is.EqualTo(0));
                Assert.That(solver.GetTile(solver.CellIndex(5, 5)), Is.EqualTo(1));
                Assert.That(solver.GetTile(solver.CellIndex(9, 2)), Is.EqualTo(2));
                AssertValidMap(solver, rules);
            }
        }

        [Test]
        public void ExhaustedSearch_FailsWithoutRestarting()
        {
            // Two colours on a wrapped 3x2 grid: each row is a ring of 3, which cannot alternate,
            // but every cell looks fine on its own, so only search can prove it.
            var solver = new WfcSolver(Colouring(2), 3, 2, 0, new SolverOptions { WrapEdges = true });

            Assert.That(solver.Status, Is.EqualTo(SolverStatus.Running));
            Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Failed));
            Assert.That(solver.Restarts, Is.EqualTo(0));
            Assert.That(solver.Backtracks, Is.GreaterThan(0));
        }

        [Test]
        public void ExhaustedBudget_Fails()
        {
            var rules = Colouring(3);
            var options = new SolverOptions { MaxBacktracks = 0, MaxRestarts = 2 };

            var failed = Enumerable.Range(0, Seeds)
                .Select(seed => new WfcSolver(rules, 12, 12, (uint)seed, options))
                .FirstOrDefault(solver => solver.Run() == SolverStatus.Failed);

            Assert.That(failed, Is.Not.Null);
            Assert.That(failed.Restarts, Is.EqualTo(3), "The third restart exceeds a budget of two.");
        }

        [Test]
        public void NegativeBudgets_Throw()
        {
            var rules = Colouring(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => new WfcSolver(rules, 4, 4, 0, new SolverOptions { MaxBacktracks = -1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WfcSolver(rules, 4, 4, 0, new SolverOptions { MaxRestarts = -1 }));
        }
    }
}
