using System;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

namespace WfcStudio.Editor
{
    /// <summary>
    /// The window's Pins section. Designers paint tiles from the tile set onto a separate Pins Tilemap with Unity's Tile
    /// Palette; the generator fixes those cells before each map. This panel adds that Tilemap, builds a palette from the
    /// tile set, opens the Tile Palette aimed at the pins, and clears them.
    /// </summary>
    internal sealed class PinsPanel
    {
        readonly Label _summary;
        readonly Button _add;
        readonly Button _paint;
        readonly Button _clear;
        readonly Button _createPalette;
        WfcGenerator _generator;

        public PinsPanel(VisualElement section)
        {
            section.Q<ObjectField>("pins").objectType = typeof(Tilemap);
            _summary = section.Q<Label>("pinsSummary");
            _add = section.Q<Button>("addPins");
            _paint = section.Q<Button>("paintPins");
            _clear = section.Q<Button>("clearPins");
            _createPalette = section.Q<Button>("createPalette");

            _add.clicked += AddPinsTilemap;
            _paint.clicked += Paint;
            _clear.clicked += Clear;
            _createPalette.clicked += CreatePalette;
        }

        public void SetGenerator(WfcGenerator generator)
        {
            _generator = generator;
            Refresh();
        }

        /// <summary>Updates the pin count and buttons. Polled, since pins are painted in the Scene view.</summary>
        public void Refresh()
        {
            if (_generator == null)
                return;

            bool hasPins = _generator.Pins != null;
            _add.EnableInClassList("hidden", hasPins);
            _paint.EnableInClassList("hidden", !hasPins);
            _clear.EnableInClassList("hidden", !hasPins);
            _createPalette.SetEnabled(_generator.TileSet != null);

            if (!hasPins)
            {
                _summary.text = "Add a Pins Tilemap, then paint tiles on it with the Tile Palette to fix them in every map.";
                return;
            }

            var pins = _generator.ReadPins(out int unknown);
            string text = pins.Count == 0 && unknown == 0
                ? "No pins yet. Paint tiles from the tile set inside the map area."
                : WfcStudioWindow.Plural(pins.Count, "pinned cell") + " inside the map area";
            if (unknown > 0)
                text += $" · {unknown} with tiles that aren't in the tile set, ignored";
            _summary.text = text;
        }

        // A half-transparent Tilemap above the map, so pins stay visible over generated tiles.
        void AddPinsTilemap()
        {
            var output = _generator.Tilemap;
            var grid = output != null ? output.layoutGrid : _generator.GetComponentInParent<Grid>();

            var pinsObject = new GameObject("Pins", typeof(Tilemap), typeof(TilemapRenderer));
            pinsObject.transform.SetParent(grid != null ? grid.transform : _generator.transform, false);
            var pins = pinsObject.GetComponent<Tilemap>();
            pins.color = new Color(1f, 1f, 1f, 0.5f);
            if (output != null && output.TryGetComponent(out TilemapRenderer outputRenderer))
            {
                var renderer = pinsObject.GetComponent<TilemapRenderer>();
                renderer.sortingLayerID = outputRenderer.sortingLayerID;
                renderer.sortingOrder = outputRenderer.sortingOrder + 1;
            }
            Undo.RegisterCreatedObjectUndo(pinsObject, "Add Pins Tilemap");

            var serialized = new SerializedObject(_generator);
            serialized.FindProperty("_pins").objectReferenceValue = pins;
            serialized.ApplyModifiedProperties();
            Refresh();
        }

        void Paint()
        {
            var pins = _generator.Pins.gameObject;
            Selection.activeGameObject = pins;
            EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
            GridPaintingState.scenePaintTarget = pins;
        }

        void Clear()
        {
            Undo.RegisterCompleteObjectUndo(_generator.Pins, "Clear Pins");
            _generator.Pins.ClearAllTiles();
            Refresh();
        }

        void CreatePalette()
        {
            var tileSet = _generator.TileSet;
            string tileSetPath = AssetDatabase.GetAssetPath(tileSet);
            string folder = tileSetPath.StartsWith("Assets/") ? System.IO.Path.GetDirectoryName(tileSetPath).Replace('\\', '/') : "Assets";
            string path = EditorUtility.SaveFilePanelInProject("Create Tile Palette", tileSet.name + " Palette", "prefab",
                "Save a Tile Palette that holds every tile of this tile set.", folder);
            if (string.IsNullOrEmpty(path))
                return;

            var palette = TilePaletteBuilder.Create(tileSet, path);
            EditorGUIUtility.PingObject(palette);
            try
            {
                GridPaintingState.palette = palette;
            }
            catch (ArgumentException)
            {
                // The Tile Palette window lists a new palette once it refreshes; the designer can pick it there.
            }
        }
    }
}
