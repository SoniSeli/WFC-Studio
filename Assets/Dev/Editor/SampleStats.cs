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

        // Biomes whose patch sizes are reported, since tile weights decide how big features grow. Mountains are left out:
        // their insides are plateau and terrace tiles, so a name match would undercount them.
        static readonly string[] PatchBiomes = { "Forest", "Lake" };

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

            report.AppendLine();
            report.AppendLine("Patch sizes (cells joined through tiles named after the biome; patches cut by the map edge included)");
            report.AppendLine("set        | biome    | patches/map | coverage | area median / p90 | area <= 6 | area <= 12 | area >= 25");
            foreach (string guid in AssetDatabase.FindAssets("t:TileSet", new[] { SamplesFolder }).OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
            {
                var set = AssetDatabase.LoadAssetAtPath<TileSet>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (string biome in PatchBiomes)
                {
                    bool[] inBiome = set.Tiles.Select(t => t.DisplayName.StartsWith(biome + " ")).ToArray();
                    if (inBiome.Any(b => b))
                        report.AppendLine(MeasurePatches(set, inBiome, biome, width, height, seeds));
                }
            }

            return report.ToString();
        }

        static string MeasurePatches(TileSet set, bool[] inBiome, string biome, int width, int height, int seeds)
        {
            var rules = set.Compile().Rules;
            var areas = new List<int>();
            long covered = 0;
            var stack = new Stack<int>();

            for (uint seed = 0; seed < seeds; seed++)
            {
                var solver = new WfcSolver(rules, width, height, seed);
                solver.Run();
                var seen = new bool[width * height];
                for (int start = 0; start < seen.Length; start++)
                {
                    if (seen[start] || !IsBiome(solver, inBiome, start))
                        continue;

                    int area = 0;
                    seen[start] = true;
                    stack.Push(start);
                    while (stack.Count > 0)
                    {
                        int cell = stack.Pop();
                        area++;
                        int x = cell % width, y = cell / width;
                        int[] neighbours = { x > 0 ? cell - 1 : -1, x < width - 1 ? cell + 1 : -1, y > 0 ? cell - width : -1, y < height - 1 ? cell + width : -1 };
                        foreach (int next in neighbours)
                        {
                            if (next >= 0 && !seen[next] && IsBiome(solver, inBiome, next))
                            {
                                seen[next] = true;
                                stack.Push(next);
                            }
                        }
                    }
                    areas.Add(area);
                    covered += area;
                }
            }

            if (areas.Count == 0)
                return $"{set.name,-10} | {biome,-8} | none";
            areas.Sort();
            return $"{set.name,-10} | {biome,-8} | {(double)areas.Count / seeds,11:0.0} | {100.0 * covered / ((long)width * height * seeds),7:0.0}% | " +
                $"{Percentile(areas, 0.5),11} / {Percentile(areas, 0.9),3} | {100.0 * areas.Count(a => a <= 6) / areas.Count,8:0}% | " +
                $"{100.0 * areas.Count(a => a <= 12) / areas.Count,9:0}% | {100.0 * areas.Count(a => a >= 25) / areas.Count,9:0}%";
        }

        static bool IsBiome(WfcSolver solver, bool[] inBiome, int cell)
        {
            int tile = solver.GetTile(cell);
            return tile >= 0 && inBiome[tile];
        }

        static T Percentile<T>(List<T> sorted, double p)
        {
            return sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(p * sorted.Count) - 1)];
        }
    }
}
