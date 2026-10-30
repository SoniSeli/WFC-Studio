using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using WfcStudio.Core;
using WfcStudio.Editor;

namespace WfcStudio.Tests
{
    public class EditModeGenerationTests
    {
        readonly List<Object> _created = new List<Object>();
        Tilemap _tilemap;
        WfcGenerator _generator;
        EditModeGeneration _session;

        [SetUp]
        public void SetUp()
        {
            var grass = Track(ScriptableObject.CreateInstance<Tile>());
            var flowers = Track(ScriptableObject.CreateInstance<Tile>());
            var tileSet = Track(ScriptableObject.CreateInstance<TileSet>());
            tileSet.SetContents(
                new[] { new SocketDefinition("grass") },
                new[]
                {
                    new TileEntry("Grass", grass, 3f, "grass", "grass", "grass", "grass"),
                    new TileEntry("Flowers", flowers, 1f, "grass", "grass", "grass", "grass"),
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
            _generator.StepsPerSecond = 64f;

            _session = new EditModeGeneration(_generator);
        }

        [TearDown]
        public void TearDown()
        {
            _session.Dispose();
            Undo.ClearUndo(_tilemap);
            foreach (var obj in _created)
                Object.DestroyImmediate(obj);
            _created.Clear();
        }

        T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        int TileCount()
        {
            int count = 0;
            foreach (var position in _tilemap.cellBounds.allPositionsWithin)
                if (_tilemap.HasTile(position))
                    count++;
            return count;
        }

        [Test]
        public void GenerateInstantly_IsOneUndoStep()
        {
            Assert.That(_session.GenerateInstantly(), Is.EqualTo(SolverStatus.Completed));
            Assert.That(TileCount(), Is.EqualTo(24));

            Undo.PerformUndo();
            Assert.That(TileCount(), Is.EqualTo(0));
            Assert.That(_generator.Solver, Is.Null, "the solver no longer matches the restored map");

            Undo.PerformRedo();
            Assert.That(TileCount(), Is.EqualTo(24));
        }

        // Timings are powers of two so the step counts are exact in floating point.
        [Test]
        public void Generate_AdvancesOnEachEditorTick()
        {
            _session.Generate();
            Assert.That(_generator.IsGenerating, Is.True);

            _session.Tick(1f / 16f);
            Assert.That(_generator.Solver.Steps, Is.EqualTo(4));

            _session.Tick(1f / 16f);
            Assert.That(_generator.Solver.Steps, Is.EqualTo(8));
        }

        [Test]
        public void LongEditorFrames_AreCapped()
        {
            _session.Generate();

            _session.Tick(10.0);

            Assert.That(_generator.Solver.Steps, Is.LessThan(24), "a ten-second hitch doesn't finish the map at once");
        }

        [Test]
        public void UndoDuringLiveGeneration_RestoresTheMapAndStops()
        {
            _session.Generate();
            _session.Tick(1f / 8f);
            Assert.That(TileCount(), Is.GreaterThan(0));

            Undo.PerformUndo();

            Assert.That(TileCount(), Is.EqualTo(0));
            Assert.That(_generator.Solver, Is.Null);

            _session.Tick(1f / 8f);
            Assert.That(TileCount(), Is.EqualTo(0), "nothing keeps drawing into the restored map");
        }

        [Test]
        public void StepOnce_StartsAPausedMap()
        {
            _session.StepOnce();

            Assert.That(_generator.IsGenerating, Is.True);
            Assert.That(_generator.IsPaused, Is.True);
            Assert.That(_generator.Solver.Steps, Is.EqualTo(1));

            _session.StepOnce();
            Assert.That(_generator.Solver.Steps, Is.EqualTo(2));
        }

        [Test]
        public void Resume_ContinuesAPausedMap()
        {
            _session.StepOnce();
            _session.Resume();

            _session.Tick(1f / 16f);

            Assert.That(_generator.Solver.Steps, Is.EqualTo(5));
        }

        [Test]
        public void Clear_IsUndoable()
        {
            _session.GenerateInstantly();
            _session.Clear();
            Assert.That(TileCount(), Is.EqualTo(0));

            Undo.PerformUndo();

            Assert.That(TileCount(), Is.EqualTo(24));
        }
    }
}
