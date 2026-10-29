using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using WfcStudio.Core;

namespace WfcStudio.Tests
{
    public class TileSetTests
    {
        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        T Create<T>(string name) where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            obj.name = name;
            _created.Add(obj);
            return obj;
        }

        static bool Has(TileSetCompilation compilation, IssueCode code)
        {
            return compilation.Issues.Any(issue => issue.Code == code);
        }

        static SocketDefinition[] Sockets(params string[] names)
        {
            return names.Select(name => new SocketDefinition(name)).ToArray();
        }

        static TileEntry Entry(TileBase tile, string all, float weight = 1f)
        {
            return new TileEntry(null, tile, weight, all, all, all, all);
        }

        [Test]
        public void ValidSet_CompilesAndGenerates()
        {
            var grass = Create<Tile>("Grass");
            var flowers = Create<Tile>("Flowers");
            var set = Create<TileSet>("Meadow");
            set.SetContents(Sockets("grass"), new[] { Entry(grass, "grass", 4f), Entry(flowers, "grass") });

            var result = set.Compile();

            Assert.That(result.HasErrors, Is.False, string.Join("\n", result.Issues));
            Assert.That(result.Rules.TileCount, Is.EqualTo(2));
            Assert.That(result.Rules.GetWeight(0), Is.EqualTo(4.0));
            Assert.That(new WfcSolver(result.Rules, 8, 8, 1).Run(), Is.EqualTo(SolverStatus.Completed));
        }

        [Test]
        public void TileIds_FollowEntryOrder()
        {
            var grass = Create<Tile>("Grass");
            var water = Create<Tile>("Water");
            var set = Create<TileSet>("Set");
            set.SetContents(Sockets("grass", "water"), new[] { Entry(grass, "grass"), Entry(water, "water") });
            var rules = set.Compile().Rules;

            Assert.That(set.Tiles[0].Tile, Is.SameAs(grass));
            Assert.That(rules.Allows(0, Direction.Right, 0), Is.True);
            Assert.That(rules.Allows(0, Direction.Right, 1), Is.False);
        }

        [Test]
        public void UnknownSocketLabel_IsAnError_NamingTheEdge()
        {
            var grass = Create<Tile>("Grass");
            var set = Create<TileSet>("Set");
            set.SetContents(Sockets("grass"), new[] { new TileEntry(null, grass, 1f, "grass", "gras", "grass", "grass") });

            var result = set.Compile();

            var issue = result.Issues.Single(i => i.Code == IssueCode.UnknownSocket);
            Assert.That(result.Rules, Is.Null);
            Assert.That(issue.Message, Does.Contain("'gras'").And.Contain("right edge"));
        }

        [Test]
        public void EmptySocketLabel_IsReportedAsMissing()
        {
            var grass = Create<Tile>("Grass");
            var set = Create<TileSet>("Set");
            set.SetContents(Sockets("grass"), new[] { new TileEntry(null, grass, 1f, "grass", "grass", "", "grass") });

            var result = set.Compile();

            Assert.That(Has(result, IssueCode.MissingSocket), Is.True);
        }

        [Test]
        public void StructuralProblems_AreErrors()
        {
            var grass = Create<Tile>("Grass");
            var set = Create<TileSet>("Set");
            set.SetContents(
                new[] { new SocketDefinition("grass"), new SocketDefinition("grass"), new SocketDefinition(""), new SocketDefinition("cliff", true, "nowhere") },
                new[] { Entry(grass, "grass"), Entry(grass, "grass"), Entry(null, "grass") });

            var result = set.Compile();

            Assert.That(result.Rules, Is.Null);
            Assert.That(Has(result, IssueCode.DuplicateSocket), Is.True);
            Assert.That(Has(result, IssueCode.UnnamedSocket), Is.True);
            Assert.That(Has(result, IssueCode.UnknownSocket), Is.True);
            Assert.That(Has(result, IssueCode.DuplicateTile), Is.True);
            Assert.That(Has(result, IssueCode.MissingTile), Is.True);
        }

        [Test]
        public void EmptySet_IsAnError()
        {
            var set = Create<TileSet>("Empty");

            Assert.That(Has(set.Compile(), IssueCode.NoTiles), Is.True);
        }

        [Test]
        public void SocketConnections_WorkWhicheverSideListsThem()
        {
            var top = Create<Tile>("Cliff Top");
            var bottom = Create<Tile>("Cliff Bottom");
            var set = Create<TileSet>("Cliff");
            set.SetContents(
                new[] { new SocketDefinition("top", false, "base"), new SocketDefinition("base", false) },
                new[] { Entry(top, "top"), Entry(bottom, "base") });

            var rules = set.Compile().Rules;
            Assert.That(rules.Allows(0, Direction.Down, 1), Is.True);
            Assert.That(rules.Allows(1, Direction.Up, 0), Is.True);
            Assert.That(rules.Allows(0, Direction.Down, 0), Is.False);
            Assert.That(rules.Allows(1, Direction.Down, 1), Is.False);
        }

        [Test]
        public void BorderThatFacesOnlyGrass_KeepsFeaturesApart()
        {
            var grass = Create<Tile>("Grass");
            var pond = Create<Tile>("Pond");
            var rock = Create<Tile>("Rock");
            var set = Create<TileSet>("Set");
            set.SetContents(
                new[] { new SocketDefinition("grass"), new SocketDefinition("border", false, "grass") },
                new[] { Entry(grass, "grass"), Entry(pond, "border"), Entry(rock, "border") });

            var result = set.Compile();

            Assert.That(result.HasErrors, Is.False, string.Join("\n", result.Issues));
            Assert.That(result.Rules.Allows(1, Direction.Right, 0), Is.True);
            Assert.That(result.Rules.Allows(1, Direction.Right, 2), Is.False);
            Assert.That(result.Rules.Allows(2, Direction.Up, 2), Is.False);
        }

        [Test]
        public void AnyDirectionException_AppliesToAllFourSides()
        {
            var grass = Create<Tile>("Grass");
            var tree = Create<Tile>("Tree");
            var set = Create<TileSet>("Set");
            set.SetContents(Sockets("grass"), new[] { Entry(grass, "grass"), Entry(tree, "grass") },
                new[] { new AdjacencyException(ExceptionKind.Forbid, tree, ExceptionDirection.Any, tree) });

            var rules = set.Compile().Rules;

            foreach (Direction direction in new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left })
                Assert.That(rules.Allows(1, direction, 1), Is.False, direction.ToString());
            Assert.That(rules.Allows(1, Direction.Right, 0), Is.True);
        }

        [Test]
        public void ExceptionWithTileOutsideSet_IsAnError()
        {
            var grass = Create<Tile>("Grass");
            var stranger = Create<Tile>("Stranger");
            var set = Create<TileSet>("Set");
            set.SetContents(Sockets("grass"), new[] { Entry(grass, "grass") },
                new[]
                {
                    new AdjacencyException(ExceptionKind.Forbid, grass, ExceptionDirection.Up, stranger),
                    new AdjacencyException(ExceptionKind.Forbid, grass, ExceptionDirection.Up, null),
                });

            var result = set.Compile();

            Assert.That(result.Issues.Count(i => i.Code == IssueCode.UnknownExceptionTile), Is.EqualTo(2));
        }

        [Test]
        public void TileNames_DefaultToTheTileAsset()
        {
            var grass = Create<Tile>("Grass Tile");

            Assert.That(Entry(grass, "grass").DisplayName, Is.EqualTo("Grass Tile"));
            Assert.That(new TileEntry("Custom", grass, 1f, "a", "a", "a", "a").DisplayName, Is.EqualTo("Custom"));
        }
    }
}
