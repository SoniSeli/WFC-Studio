using System.Linq;
using NUnit.Framework;
using WfcStudio.Core;
using static WfcStudio.Tests.TestRules;

namespace WfcStudio.Tests
{
    public class SocketTileSetTests
    {
        const int GrassSocket = 0;
        const int Shore = 1;

        // A grass tile and a two-tile pond strip: Grass | PondLeft | PondRight | Grass.
        static SocketTileSet Pond()
        {
            var set = new SocketTileSet(new[] { "Grass", "Pond Left", "Pond Right" }, new[] { "grass", "shore" });
            set.SetSockets(0, GrassSocket, GrassSocket, GrassSocket, GrassSocket);
            set.SetSockets(1, GrassSocket, Shore, GrassSocket, GrassSocket);
            set.SetSockets(2, GrassSocket, GrassSocket, GrassSocket, Shore);
            return set;
        }

        static bool Has(TileSetCompilation compilation, IssueCode code)
        {
            return compilation.Issues.Any(issue => issue.Code == code);
        }

        [Test]
        public void MatchingSockets_AllowNeighbours()
        {
            var rules = Pond().Compile().Rules;

            Assert.That(rules.Allows(1, Direction.Right, 2), Is.True, "shore meets shore");
            Assert.That(rules.Allows(0, Direction.Right, 1), Is.True, "grass meets grass");
            Assert.That(rules.Allows(1, Direction.Right, 0), Is.False, "shore never meets grass");
            Assert.That(rules.Allows(1, Direction.Right, 1), Is.False, "a left half can't follow a left half");
            Assert.That(rules.Allows(1, Direction.Up, 2), Is.True, "top and bottom edges are grass");
        }

        [Test]
        public void OneWaySockets_ConnectToEachOtherButNotThemselves()
        {
            var set = new SocketTileSet(new[] { "Cliff Top", "Cliff Bottom" }, new[] { "cliff-top", "cliff-base" });
            set.SetSockets(0, 0, 0, 0, 0);
            set.SetSockets(1, 1, 1, 1, 1);
            set.SetConnected(0, 0, false);
            set.SetConnected(1, 1, false);
            set.SetConnected(0, 1, true);
            var rules = set.Compile().Rules;

            Assert.That(rules.Allows(0, Direction.Down, 1), Is.True);
            Assert.That(rules.Allows(0, Direction.Down, 0), Is.False);
            Assert.That(rules.Allows(1, Direction.Down, 1), Is.False);
        }

        [Test]
        public void Connections_AreSymmetric_AndSocketsStartSelfConnected()
        {
            var set = new SocketTileSet(new[] { "A" }, new[] { "a", "b", "c" });
            set.SetConnected(0, 1, true);

            Assert.That(set.Matches(0, 0), Is.True);
            Assert.That(set.Matches(0, 1), Is.True);
            Assert.That(set.Matches(1, 0), Is.True);
            Assert.That(set.Matches(0, 2), Is.False);

            set.SetConnected(1, 0, false);
            Assert.That(set.Matches(0, 1), Is.False);
        }

        [Test]
        public void BorderSocket_KeepsFeaturesApart()
        {
            // Two one-tile "ponds" whose edges are a border that faces grass but never another border.
            var set = new SocketTileSet(new[] { "Grass", "Pond", "Puddle" }, new[] { "grass", "border" });
            set.SetSockets(0, 0, 0, 0, 0);
            set.SetSockets(1, 1, 1, 1, 1);
            set.SetSockets(2, 1, 1, 1, 1);
            set.SetConnected(1, 1, false);
            set.SetConnected(1, 0, true);
            var result = set.Compile();
            var rules = result.Rules;

            Assert.That(rules.Allows(0, Direction.Right, 1), Is.True);
            Assert.That(rules.Allows(1, Direction.Right, 2), Is.False);
            Assert.That(rules.Allows(1, Direction.Up, 1), Is.False);
            Assert.That(result.Issues.Where(i => i.Severity != IssueSeverity.Info), Is.Empty);

            for (uint seed = 0; seed < 10; seed++)
            {
                var solver = new WfcSolver(rules, 12, 8, seed);
                Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Completed));
                AssertValidMap(solver, rules);
            }
        }

        [Test]
        public void Exceptions_OverrideSockets_InBothDirections()
        {
            var set = Pond();
            set.AddException(0, Direction.Right, 0, allow: false);
            set.AddException(1, Direction.Right, 2, allow: false);
            set.AddException(1, Direction.Right, 1, allow: true);
            var rules = set.Compile().Rules;

            Assert.That(rules.Allows(0, Direction.Right, 0), Is.False);
            Assert.That(rules.Allows(0, Direction.Left, 0), Is.False);
            Assert.That(rules.Allows(1, Direction.Right, 2), Is.False);
            Assert.That(rules.Allows(2, Direction.Left, 1), Is.False);
            Assert.That(rules.Allows(1, Direction.Right, 1), Is.True);
            Assert.That(rules.Allows(1, Direction.Left, 1), Is.True);
            Assert.That(rules.Allows(0, Direction.Up, 0), Is.True, "other directions are unaffected");
        }

        [Test]
        public void ConflictingExceptions_AreAnError_EvenWhenWrittenFromOppositeSides()
        {
            var set = Pond();
            set.AddException(0, Direction.Right, 1, allow: false);
            set.AddException(1, Direction.Left, 0, allow: true);
            var result = set.Compile();

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Rules, Is.Null);
            Assert.That(Has(result, IssueCode.ConflictingExceptions), Is.True);
        }

        [Test]
        public void ExceptionsWithNoEffect_AreReported()
        {
            var set = Pond();
            set.AddException(1, Direction.Right, 2, allow: true);
            var result = set.Compile();

            Assert.That(result.HasErrors, Is.False);
            Assert.That(Has(result, IssueCode.RedundantException), Is.True);
        }

        [Test]
        public void MissingSockets_AndInvalidWeights_AreErrors()
        {
            var set = new SocketTileSet(new[] { "A", "B" }, new[] { "a" });
            set.SetSockets(0, 0, 0, 0, 0);
            set.SetSocket(1, Direction.Up, 0);
            set.SetWeight(0, 0);
            var result = set.Compile();

            Assert.That(result.Rules, Is.Null);
            Assert.That(result.Issues.Count(issue => issue.Code == IssueCode.MissingSocket), Is.EqualTo(3));
            Assert.That(Has(result, IssueCode.InvalidWeight), Is.True);
        }

        [Test]
        public void EmptySet_IsAnError()
        {
            var result = new SocketTileSet(new string[0], new[] { "a" }).Compile();

            Assert.That(result.HasErrors, Is.True);
            Assert.That(Has(result, IssueCode.NoTiles), Is.True);
        }

        [Test]
        public void UnusedSockets_AreReportedAsInfo()
        {
            var set = new SocketTileSet(new[] { "A" }, new[] { "a", "spare" });
            set.SetSockets(0, 0, 0, 0, 0);
            var result = set.Compile();

            var issue = result.Issues.Single(i => i.Code == IssueCode.UnusedSocket);
            Assert.That(issue.Severity, Is.EqualTo(IssueSeverity.Info));
            Assert.That(issue.Message, Does.Contain("spare"));
        }

        [Test]
        public void TilesWithNoNeighbourOnOneSide_AreEdgeOnly()
        {
            // "Lone" has a socket on its right that nothing matches.
            var set = new SocketTileSet(new[] { "Grass", "Lone" }, new[] { "grass", "nothing" });
            set.SetSockets(0, 0, 0, 0, 0);
            set.SetSockets(1, 0, 1, 0, 0);
            var result = set.Compile();

            var issue = result.Issues.Single(i => i.Code == IssueCode.NoNeighbour);
            Assert.That(issue.Tile, Is.EqualTo(1));
            Assert.That(issue.Severity, Is.EqualTo(IssueSeverity.Warning));
            Assert.That(issue.Message, Does.Contain("right edge"));
        }

        [Test]
        public void TilesThatOnlyFitNextToEdgeOnlyTiles_CannotAppearInside()
        {
            // "Lone" can only sit on the right edge. "Leaner" must have "Lone" to its right, so it can't be inside either.
            var set = new SocketTileSet(new[] { "Grass", "Lone", "Leaner" }, new[] { "grass", "nothing", "lean" });
            set.SetSockets(0, 0, 0, 0, 0);
            set.SetSockets(1, 0, 1, 0, 2);
            set.SetSockets(2, 0, 2, 0, 0);
            var result = set.Compile();

            Assert.That(result.Issues.Where(i => i.Code == IssueCode.CannotAppearInside).Select(i => i.Tile), Is.EquivalentTo(new[] { 2 }));
        }

        [Test]
        public void CleanSet_HasNoWarningsOrErrors()
        {
            var result = Pond().Compile();

            Assert.That(result.Issues.Where(i => i.Severity != IssueSeverity.Info), Is.Empty);
        }

        [Test]
        public void CompiledSocketSet_GeneratesValidMaps()
        {
            var rules = Pond().Compile().Rules;
            for (uint seed = 0; seed < 20; seed++)
            {
                var solver = new WfcSolver(rules, 10, 6, seed);

                Assert.That(solver.Run(), Is.EqualTo(SolverStatus.Completed), $"Seed {seed}");
                AssertValidMap(solver, rules);
            }
        }
    }
}
