using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;
using WfcStudio.Editor;

namespace WfcStudio.Tests
{
    // Palettes are prefab assets that reference tile assets, so this test works on disk in a temporary folder.
    public class TilePaletteBuilderTests
    {
        const string Folder = "Assets/WfcStudioPaletteTest";
        TileSet _tileSet;
        Tile[] _tiles;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "WfcStudioPaletteTest");
            _tiles = new Tile[10];
            for (int i = 0; i < _tiles.Length; i++)
            {
                _tiles[i] = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(_tiles[i], $"{Folder}/Tile {i}.asset");
            }

            _tileSet = ScriptableObject.CreateInstance<TileSet>();
            _tileSet.SetContents(new[] { new SocketDefinition("a") },
                _tiles.Select((tile, i) => new TileEntry($"Tile {i}", tile, 1f, "a", "a", "a", "a")));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tileSet);
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void Create_PlacesEveryTileInRowsOfEight()
        {
            var palette = TilePaletteBuilder.Create(_tileSet, $"{Folder}/Test Palette.prefab");

            Assert.That(palette, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(palette)).OfType<GridPalette>(), Is.Not.Empty);

            var tilemap = palette.GetComponentInChildren<Tilemap>();
            Assert.That(tilemap.GetTile(new Vector3Int(0, 0, 0)), Is.SameAs(_tiles[0]));
            Assert.That(tilemap.GetTile(new Vector3Int(7, 0, 0)), Is.SameAs(_tiles[7]));
            Assert.That(tilemap.GetTile(new Vector3Int(0, -1, 0)), Is.SameAs(_tiles[8]));
            Assert.That(tilemap.GetTile(new Vector3Int(1, -1, 0)), Is.SameAs(_tiles[9]));
        }

        [Test]
        public void Create_ReplacesAnExistingPalette()
        {
            string path = $"{Folder}/Test Palette.prefab";
            TilePaletteBuilder.Create(_tileSet, path);

            var second = TilePaletteBuilder.Create(_tileSet, path);

            Assert.That(AssetDatabase.GetAssetPath(second), Is.EqualTo(path));
        }
    }
}
