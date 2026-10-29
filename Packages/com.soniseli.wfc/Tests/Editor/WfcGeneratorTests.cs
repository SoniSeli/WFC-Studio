using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using WfcStudio.Core;

namespace WfcStudio.Tests
{
    public class WfcGeneratorTests
    {
        readonly List<Object> _created = new List<Object>();
        Tile _grass;
        Tile _flowers;
        Tilemap _tilemap;
        WfcGenerator _generator;

        [SetUp]
        public void SetUp()
        {
            _grass = Track(ScriptableObject.CreateInstance<Tile>());
            _flowers = Track(ScriptableObject.CreateInstance<Tile>());
            var tileSet = Track(ScriptableObject.CreateInstance<TileSet>());
            tileSet.SetContents(
                new[] { new SocketDefinition("grass") },
                new[]
                {
                    new TileEntry("Grass", _grass, 3f, "grass", "grass", "grass", "grass"),
                    new TileEntry("Flowers", _flowers, 1f, "grass", "grass", "grass", "grass"),
                });

            var grid = Track(new GameObject("Grid", typeof(Grid)));
            var map = new GameObject("Map", typeof(Tilemap));
            map.transform.SetParent(grid.transform);
            _tilemap = map.GetComponent<Tilemap>();

            _generator = grid.AddComponent<WfcGenerator>();
            _generator.TileSet = tileSet;
            _generator.Tilemap = _tilemap;
            _generator.Size = new Vector2Int(6, 4);
            _generator.Seed = 3;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        TileBase TileAt(int x, int y)
        {
            return _tilemap.GetTile(_generator.Origin + new Vector3Int(x, y, 0));
        }

        void AssertTilemapMatchesSolver()
        {
            var solver = _generator.Solver;
            for (int y = 0; y < solver.Height; y++)
            {
                for (int x = 0; x < solver.Width; x++)
                {
                    int tile = solver.GetTile(solver.CellIndex(x, y));
                    var expected = tile >= 0 ? _generator.TileSet.Tiles[tile].Tile : _generator.UndecidedTile;
                    Assert.That(TileAt(x, y), Is.SameAs(expected), $"Cell ({x}, {y})");
                }
            }
        }

        [Test]
        public void GenerateInstantly_DrawsEveryCell()
        {
            Assert.That(_generator.GenerateInstantly(), Is.EqualTo(SolverStatus.Completed));

            AssertTilemapMatchesSolver();
            Assert.That(TileAt(0, 0), Is.Not.Null);
        }

        [Test]
        public void Origin_OffsetsWhereTheMapIsDrawn()
        {
            _generator.Origin = new Vector3Int(10, -3, 0);
            _generator.GenerateInstantly();

            Assert.That(_tilemap.GetTile(new Vector3Int(10, -3, 0)), Is.Not.Null);
            Assert.That(_tilemap.GetTile(new Vector3Int(0, 0, 0)), Is.Null);
            AssertTilemapMatchesSolver();
        }

        // Timings are powers of two so the step counts are exact in floating point.
        [Test]
        public void Tick_RunsStepsPerSecond_CarryingFractionalSteps()
        {
            _generator.StepsPerSecond = 64f;
            _generator.Generate();

            _generator.Tick(1f / 16f);
            Assert.That(_generator.Solver.Steps, Is.EqualTo(4));

            _generator.Tick(1f / 128f);
            Assert.That(_generator.Solver.Steps, Is.EqualTo(4), "half a step is owed, not taken");

            _generator.Tick(1f / 128f);
            Assert.That(_generator.Solver.Steps, Is.EqualTo(5));
            AssertTilemapMatchesSolver();
        }

        [Test]
        public void ZeroStepsPerSecond_RunsAsFastAsTheBudgetAllows()
        {
            _generator.StepsPerSecond = 0f;
            _generator.FrameBudgetMilliseconds = 1000f;
            _generator.Generate();

            _generator.Tick(0.016f);

            Assert.That(_generator.Status, Is.EqualTo(SolverStatus.Completed));
            AssertTilemapMatchesSolver();
        }

        [Test]
        public void Pause_StopsTicks_ButStepOnceStillAdvances()
        {
            _generator.StepsPerSecond = 64f;
            _generator.Generate();
            _generator.Pause();

            _generator.Tick(1f);
            Assert.That(_generator.Solver.Steps, Is.EqualTo(0));

            _generator.StepOnce();
            Assert.That(_generator.Solver.Steps, Is.EqualTo(1));

            _generator.Resume();
            _generator.Tick(1f / 32f);
            Assert.That(_generator.Solver.Steps, Is.EqualTo(3));
        }

        [Test]
        public void UndecidedCells_ShowThePlaceholderTile()
        {
            var placeholder = Track(ScriptableObject.CreateInstance<Tile>());
            _generator.UndecidedTile = placeholder;
            _generator.Generate();

            Assert.That(TileAt(0, 0), Is.SameAs(placeholder));

            _generator.StepOnce();
            AssertTilemapMatchesSolver();
        }

        [Test]
        public void Redrawn_ReportsTheChangedCells()
        {
            _generator.Generate();
            var reported = new List<int>();
            _generator.Redrawn += generator => reported.AddRange(generator.ChangedCells);

            _generator.StepOnce();

            int collapsed = Enumerable.Range(0, _generator.Solver.CellCount).Single(cell => _generator.Solver.IsCollapsed(cell));
            Assert.That(reported, Does.Contain(collapsed));
            Assert.That(_generator.ChangedCells, Is.Empty, "the list is cleared once listeners have run");
        }

        [Test]
        public void Complete_FinishesTheCurrentMap()
        {
            _generator.Generate();
            _generator.Complete();

            Assert.That(_generator.Status, Is.EqualTo(SolverStatus.Completed));
            Assert.That(_generator.IsGenerating, Is.False);
            AssertTilemapMatchesSolver();
        }

        [Test]
        public void SameSeed_DrawsTheSameMap()
        {
            _generator.Size = new Vector2Int(12, 8);
            _generator.GenerateInstantly();
            var first = Enumerable.Range(0, 96).Select(cell => _generator.Solver.GetTile(cell)).ToArray();

            _generator.Clear();
            _generator.GenerateInstantly();
            var second = Enumerable.Range(0, 96).Select(cell => _generator.Solver.GetTile(cell)).ToArray();

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void TileSetWithErrors_LogsAndDoesNotStart()
        {
            var broken = Track(ScriptableObject.CreateInstance<TileSet>());
            _generator.TileSet = broken;

            LogAssert.Expect(LogType.Error, new Regex("no tiles"));
            Assert.That(_generator.Generate(), Is.False);
            Assert.That(_generator.Solver, Is.Null);
            Assert.That(_generator.Status, Is.Null);
        }

        [Test]
        public void Heatmap_ColoursUndecidedCells_AndClearsDecidedOnes()
        {
            var heatmap = _generator.gameObject.AddComponent<EntropyHeatmap>();
            heatmap.Bind(_generator);

            _generator.Generate();
            Assert.That(heatmap.Texture.width, Is.EqualTo(6));
            Assert.That(heatmap.Texture.height, Is.EqualTo(4));
            Assert.That(heatmap.Texture.GetPixels32().All(pixel => pixel.a > 0), Is.True, "nothing is decided yet");

            _generator.Complete();
            Assert.That(heatmap.Texture.GetPixels32().All(pixel => pixel.a == 0), Is.True, "everything is decided");
        }
    }
}
