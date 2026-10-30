using System;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using WfcStudio.Core;

namespace WfcStudio.Tests
{
    // NFR2: once a solver is built, stepping it allocates no managed memory, so long generations never trigger the GC.
    // Counted with Unity's GC.Alloc profiler marker: GC.GetAllocatedBytesForCurrentThread always reads 0 under Unity's Mono.
    public class SolverAllocationTests
    {
        static long AllocationsWhile(Action action)
        {
            using var recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 1,
                ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            recorder.Start();
            action();
            recorder.Stop();
            return recorder.Count > 0 ? recorder.GetSample(0).Count : 0;
        }

        // Proves the counter works on this runtime, so a zero below means no allocation rather than no measurement.
        [Test]
        public void AllocationCounter_SeesAllocations()
        {
            byte[] buffer = null;
            long allocations = AllocationsWhile(() => buffer = new byte[4096]);

            Assert.That(allocations, Is.GreaterThanOrEqualTo(1));
            Assert.That(buffer, Is.Not.Null);
        }

        [Test]
        public void Run_AllocatesNothing_OnAMapWithoutContradictions()
        {
            var solver = new WfcSolver(TestRules.Terrain(), 40, 40, 7);

            long allocations = AllocationsWhile(() => solver.Run());

            Assert.That(solver.Status, Is.EqualTo(SolverStatus.Completed));
            Assert.That(allocations, Is.Zero);
        }

        [Test]
        public void Run_AllocatesNothing_WhileBacktracking()
        {
            var rules = TestRules.Colouring(3);
            uint seed = Enumerable.Range(1, 100).Select(s => (uint)s)
                .First(s => { var probe = new WfcSolver(rules, 16, 16, s); probe.Run(); return probe.Backtracks > 0; });
            var solver = new WfcSolver(rules, 16, 16, seed);

            long allocations = AllocationsWhile(() => solver.Run());

            Assert.That(solver.Backtracks, Is.GreaterThan(0));
            Assert.That(allocations, Is.Zero);
        }

        [Test]
        public void Run_AllocatesNothing_WhileRestarting()
        {
            var rules = TestRules.Colouring(3);
            var options = new SolverOptions { MaxBacktracks = 0, MaxRestarts = 200 };
            uint seed = Enumerable.Range(1, 100).Select(s => (uint)s)
                .First(s => { var probe = new WfcSolver(rules, 16, 16, s, options); probe.Run(); return probe.Restarts > 0; });
            var solver = new WfcSolver(rules, 16, 16, seed, options);

            long allocations = AllocationsWhile(() => solver.Run());

            Assert.That(solver.Restarts, Is.GreaterThan(0));
            Assert.That(allocations, Is.Zero);
        }
    }
}
