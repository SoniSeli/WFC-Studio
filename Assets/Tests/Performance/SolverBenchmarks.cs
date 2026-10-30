using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEditor;
using WfcStudio.Core;

namespace WfcStudio.Benchmarks
{
    // Timings for docs/BENCHMARKS.md and the D17 targets: whole maps of a small and the full sample tile set.
    // Solver setup (constructor and pruning) runs in SetUp, outside the timed region, and is measured on its own.
    [Category("Performance")]
    public class SolverBenchmarks
    {
        const string SamplesFolder = "Assets/Samples/PunyWorld";

        static RuleTable Rules(string setName)
        {
            return AssetDatabase.LoadAssetAtPath<TileSet>($"{SamplesFolder}/{setName}.asset").Compile().Rules;
        }

        [Test, Performance]
        public void Solve([Values("Lake", "Overworld")] string setName, [Values(20, 50, 100)] int size)
        {
            var rules = Rules(setName);
            WfcSolver solver = null;
            uint seed = 0;

            Measure.Method(() => solver.Run())
                .SetUp(() => solver = new WfcSolver(rules, size, size, seed++))
                .WarmupCount(2)
                .MeasurementCount(size >= 100 ? 6 : 12)
                .SampleGroup(new SampleGroup("Solve", SampleUnit.Millisecond))
                .GC()
                .Run();
        }

        [Test, Performance]
        public void Setup([Values("Lake", "Overworld")] string setName, [Values(100)] int size)
        {
            var rules = Rules(setName);
            uint seed = 0;

            Measure.Method(() => new WfcSolver(rules, size, size, seed++))
                .WarmupCount(2)
                .MeasurementCount(12)
                .SampleGroup(new SampleGroup("Setup", SampleUnit.Millisecond))
                .Run();
        }
    }
}
