using NUnit.Framework;
using WfcStudio.Core;
using static WfcStudio.Tests.TestRules;

namespace WfcStudio.Tests
{
    public class WrapEdgesTests
    {
        static readonly SolverOptions Wrap = new SolverOptions { WrapEdges = true };

        [Test]
        public void WrappedMaps_SatisfyRulesAcrossEdges()
        {
            var rules = Terrain();
            for (uint seed = 0; seed < 30; seed++)
            {
                var solver = new WfcSolver(rules, 9, 7, seed, Wrap);

                Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Completed), $"Seed {seed}");
                AssertValidMap(solver, rules, wrapEdges: true);
            }
        }

        [Test]
        public void EvenRings_Alternate()
        {
            // With wrapping, a 4x2 grid is rings of 4 across and rings of 2 up, so a checkerboard fits.
            var rules = Colouring(2);
            var solver = new WfcSolver(rules, 4, 2, 0, Wrap);

            Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Completed));
            AssertValidMap(solver, rules, wrapEdges: true);
            Assert.That(solver.GetTile(solver.CellIndex(0, 0)), Is.Not.EqualTo(solver.GetTile(solver.CellIndex(3, 0))),
                "The first and last cells of a row are neighbours.");
        }

        [Test]
        public void WithoutWrapping_EdgesAreIndependent()
        {
            // Two colours on an odd-width grid only fail if the ends of each row are treated as neighbours.
            var solver = new WfcSolver(Colouring(2), 3, 2, 0);

            Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Completed));
        }

        [Test]
        public void SingleCellWrap_NeighboursItself()
        {
            // A 1x1 wrapped grid is its own neighbour, so no colour fits. The two colours "support" each other
            // before anything is chosen, so it takes search, not pruning, to prove it.
            var solver = new WfcSolver(Colouring(2), 1, 1, 0, Wrap);

            Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Failed));
            Assert.That(solver.Restarts, Is.EqualTo(0));
        }
    }
}
