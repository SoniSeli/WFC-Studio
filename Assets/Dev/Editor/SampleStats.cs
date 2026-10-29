using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using WfcStudio.Core;
using Debug = UnityEngine.Debug;

namespace WfcStudio.Dev
{
    /// <summary>
    /// Solves every sample tile set over many seeds and reports how often the solver backtracks, restarts or fails,
    /// and how long it takes. Used to compare rule changes with numbers instead of impressions.
    /// </summary>
    public static class SampleStats
    {
        const string SamplesFolder = "Assets/Samples/PunyWorld";

        [MenuItem("Tools/WFC Studio/Dev/Measure Sample Tile Sets")]
        public static void Measure()
        {
            Debug.Log(Run(48, 28, 200));
        }

        // Batch mode: -executeMethod WfcStudio.Dev.SampleStats.MeasureFromCommandLine [-wfcStatsOut <file>]
        public static void MeasureFromCommandLine()
        {
            string report = Run(48, 28, 200);
            Debug.Log(report);

            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-wfcStatsOut");
            if (index >= 0 && index + 1 < args.Length)
                File.WriteAllText(args[index + 1], report);
        }

        public static string Run(int width, int height, int seeds)
        {
            var report = new StringBuilder();
            report.AppendLine($"{width}x{height}, {seeds} seeds per set, default options");
            report.AppendLine("set        | tiles | backtracking seeds | backtracks mean / p95 / max | restarts | failed | ms mean / p95");

            foreach (string guid in AssetDatabase.FindAssets("t:TileSet", new[] { SamplesFolder }).OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
            {
                var set = AssetDatabase.LoadAssetAtPath<TileSet>(AssetDatabase.GUIDToAssetPath(guid));
                var rules = set.Compile().Rules;
                var backtracks = new List<int>();
                var milliseconds = new List<double>();
                int restarts = 0, failed = 0;

                for (uint seed = 0; seed < seeds; seed++)
                {
                    var timer = Stopwatch.StartNew();
                    var solver = new WfcSolver(rules, width, height, seed);
                    var status = solver.Run();
                    timer.Stop();

                    backtracks.Add(solver.Backtracks);
                    milliseconds.Add(timer.Elapsed.TotalMilliseconds);
                    restarts += solver.Restarts;
                    if (status != SolverStatus.Completed)
                        failed++;
                }

                backtracks.Sort();
                milliseconds.Sort();
                report.AppendLine(
                    $"{set.name,-10} | {rules.TileCount,5} | {backtracks.Count(b => b > 0),18} | " +
                    $"{backtracks.Average(),8:0.0} / {Percentile(backtracks, 0.95),5} / {backtracks.Last(),5} | " +
                    $"{restarts,8} | {failed,6} | {milliseconds.Average(),6:0.0} / {Percentile(milliseconds, 0.95),6:0.0}");
            }

            return report.ToString();
        }

        static T Percentile<T>(List<T> sorted, double p)
        {
            return sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(p * sorted.Count) - 1)];
        }
    }
}
