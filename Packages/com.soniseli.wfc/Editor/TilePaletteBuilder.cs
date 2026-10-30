using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace WfcStudio.Editor
{
    /// <summary>Builds a Tile Palette holding every tile of a tile set, so designers can paint pins with Unity's own tools.</summary>
    internal static class TilePaletteBuilder
    {
        // Tiles per palette row. Tile sets list related tiles together, so rows keep biomes roughly grouped.
        const int Columns = 8;

        /// <summary>
        /// Creates a rectangular palette at <paramref name="path"/> (a project-relative .prefab path), replacing any asset
        /// there, with the tile set's tiles laid out left to right and top to bottom.
        /// </summary>
        public static GameObject Create(TileSet tileSet, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);

            string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            var palette = GridPaletteUtility.CreateNewPalette(folder, name, GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Automatic, new Vector3(1f, 1f, 0f), GridLayout.CellSwizzle.XYZ);
            path = AssetDatabase.GetAssetPath(palette);

            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var tilemap = contents.GetComponentInChildren<Tilemap>();
                int placed = 0;
                foreach (var entry in tileSet.Tiles)
                {
                    if (entry.Tile == null)
                        continue;
                    tilemap.SetTile(new Vector3Int(placed % Columns, -(placed / Columns), 0), entry.Tile);
                    placed++;
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }
}
