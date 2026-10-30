using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using WfcStudio.Core;

namespace WfcStudio.Tests
{
    public class GeneratorPinsTests
    {
        readonly List<Object> _created = new List<Object>();
        Tile _grass;
        Tile _water;
        Tilemap _tilemap;
        Tilemap _pins;
        WfcGenerator _generator;

        // Grass and water may each touch themselves but not each other, so a pin decides its whole region.
        [SetUp]
        public void SetUp()
        {
            _grass = Track(ScriptableObject.CreateInstance<Tile>());
            _water = Track(ScriptableObject.CreateInstance<Tile>());
            var tileSet = Track(ScriptableObject.CreateInstance<TileSet>());
            tileSet.SetContents(
                new[] { new SocketDefinition("grass"), new SocketDefinition("water") },
                new[]
                {
                    new TileEntry("Grass", _grass, 1f, "grass", "grass", "grass", "grass"),
                    new TileEntry("Water", _water, 1f, "water", "water", "water", "water"),
                });

            var grid = Track(new GameObject("Grid", typeof(Grid)));
            _tilemap = new GameObject("Map", typeof(Tilemap)).GetComponent<Tilemap>();
            _tilemap.transform.SetParent(grid.transform);
            _pins = new GameObject("Pins", typeof(Tilemap)).GetComponent<Tilemap>();
            _pins.transform.SetParent(grid.transform);

            _generator = grid.AddComponent<WfcGenerator>();
            _generator.TileSet = tileSet;
            _generator.Tilemap = _tilemap;
            _generator.Pins = _pins;
            _generator.Size = new Vector2Int(6, 4);
            _generator.Origin = new Vector3Int(10, 5, 0);
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

        [Test]
        public void PinnedTile_DecidesTheMap_AtTheOutputCells()
        {
            _pins.SetTile(new Vector3Int(12, 6, 0), _water);

            foreach (uint seed in new uint[] { 1, 2, 3, 4 })
            {
                _generator.Seed = (int)seed;
                Assert.That(_generator.GenerateInstantly(), Is.EqualTo(SolverStatus.Completed));
                Assert.That(_tilemap.GetTile(new Vector3Int(12, 6, 0)), Is.SameAs(_water));
                Assert.That(_tilemap.GetTile(new Vector3Int(15, 8, 0)), Is.SameAs(_water), "water fills the connected map");
            }
        }

        [Test]
        public void ReadPins_SkipsCellsOutsideTheMap_AndCountsForeignTiles()
        {
            var foreign = Track(ScriptableObject.CreateInstance<Tile>());
            _pins.SetTile(new Vector3Int(10, 5, 0), _grass);
            _pins.SetTile(new Vector3Int(0, 0, 0), _water);
            _pins.SetTile(new Vector3Int(11, 5, 0), foreign);

            var pins = _generator.ReadPins(out int unknown);

            Assert.That(pins, Is.EqualTo(new[] { (0, 0, 0) }));
            Assert.That(unknown, Is.EqualTo(1));
        }

        [Test]
        public void ForeignTiles_AreIgnored_WithAWarning()
        {
            _pins.SetTile(new Vector3Int(11, 5, 0), Track(ScriptableObject.CreateInstance<Tile>()));

            LogAssert.Expect(LogType.Warning, new Regex("1 pinned cell"));
            Assert.That(_generator.GenerateInstantly(), Is.EqualTo(SolverStatus.Completed));
        }

        [Test]
        public void ConflictingPins_FailTheMap_WithAWarning()
        {
            _pins.SetTile(new Vector3Int(10, 5, 0), _grass);
            _pins.SetTile(new Vector3Int(11, 5, 0), _water);

            LogAssert.Expect(LogType.Warning, new Regex("conflict"));
            Assert.That(_generator.GenerateInstantly(), Is.EqualTo(SolverStatus.Failed));
        }
    }
}
