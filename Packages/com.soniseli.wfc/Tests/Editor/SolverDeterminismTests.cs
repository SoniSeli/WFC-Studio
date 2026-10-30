using NUnit.Framework;
using WfcStudio.Core;

namespace WfcStudio.Tests
{
    // NFR1: the same rules, size and seed always give the same map. The hashes below were recorded from the solver as it
    // was before the M6 entropy queue, so they also prove that optimisations never change which cell or tile is picked.
    // A deliberate change to the algorithm means re-recording them, and saying so in the changelog.
    public class SolverDeterminismTests
    {
        // FNV-1a over the final tiles, the status and the step, backtrack and restart counts.
        static ulong Hash(WfcSolver solver)
        {
            ulong hash = 14695981039346656037UL;
            void Add(long value)
            {
                hash ^= unchecked((ulong)value);
                hash *= 1099511628211UL;
            }

            for (int cell = 0; cell < solver.CellCount; cell++)
                Add(solver.GetTile(cell));
            Add((long)solver.Status);
            Add(solver.Steps);
            Add(solver.Backtracks);
            Add(solver.Restarts);
            return hash;
        }

        // A dense random rule set, so maps backtrack often and the tie-breaking noise matters.
        static RuleTable RandomRules(int tileCount, uint seed)
        {
            var random = new Pcg32(seed);
            var builder = new RuleTable.Builder(tileCount);
            for (int tile = 0; tile < tileCount; tile++)
            {
                builder.SetWeight(tile, 0.5 + random.NextDouble() * 4.0);
                for (int d = 0; d < DirectionExtensions.Count; d++)
                    for (int other = 0; other < tileCount; other++)
                        if (random.NextDouble() < 0.35)
                            builder.Allow(tile, (Direction)d, other);
            }
            return builder.Build();
        }

        static RuleTable Rules(string name)
        {
            switch (name)
            {
                case "terrain": return TestRules.Terrain();
                case "colouring": return TestRules.Colouring(3);
                case "weighted": return TestRules.Unconstrained(5).SetWeight(0, 8).SetWeight(1, 3).Build();
                case "wide": return RandomRules(70, 9);
                default: return RandomRules(12, 42);
            }
        }

        [TestCase("terrain", 32, 1u, false, 1000, ExpectedResult = 2958508447212347588UL)]
        [TestCase("terrain", 32, 2u, false, 1000, ExpectedResult = 5951474952997980316UL)]
        [TestCase("terrain", 24, 3u, true, 1000, ExpectedResult = 3220031866089250858UL)]
        [TestCase("weighted", 20, 4u, false, 1000, ExpectedResult = 1961456454943864496UL)]
        [TestCase("colouring", 16, 1u, false, 1000, ExpectedResult = 10191873394766092055UL)]
        [TestCase("colouring", 16, 2u, false, 1000, ExpectedResult = 6819022397698536129UL)]
        [TestCase("colouring", 16, 3u, true, 1000, ExpectedResult = 4325125711163087098UL)]
        [TestCase("colouring", 12, 5u, false, 2, ExpectedResult = 3765760521479881556UL)]
        [TestCase("random", 16, 7u, false, 1000, ExpectedResult = 4764846938947646297UL)]
        [TestCase("random", 16, 8u, false, 5, ExpectedResult = 17398153757595581167UL)]
        [TestCase("wide", 16, 1u, false, 1000, ExpectedResult = 13654144803748044131UL)]
        [TestCase("wide", 12, 2u, true, 1000, ExpectedResult = 14298496373727608803UL)]
        public ulong SameSeed_GivesTheRecordedMap(string rules, int size, uint seed, bool wrapEdges, int maxBacktracks)
        {
            var options = new SolverOptions { WrapEdges = wrapEdges, MaxBacktracks = maxBacktracks };
            var solver = new WfcSolver(Rules(rules), size, size, seed, options);
            solver.Run();
            return Hash(solver);
        }
    }
}
