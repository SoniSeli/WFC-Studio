using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using WfcStudio.Core;

namespace WfcStudio.Dev
{
    /// <summary>
    /// Rebuilds the Puny World sample tile sets from the tables below, so every socket label is reviewable in code
    /// and the assets can be regenerated at any time. Existing assets are updated in place, keeping their GUIDs.
    /// </summary>
    public static class PunyWorldSampleBuilder
    {
        const string Root = "Assets/Samples/PunyWorld";
        const string SheetPath = Root + "/punyworld-overworld-tileset.png";
        const string ExtrudedSheetPath = Root + "/PunyWorldTiles.png";
        const string TilesFolder = Root + "/Tiles";
        const int CellSize = 16;
        const int Extrusion = 2;
        const int ExtrudedColumns = 8;
        const string Grass = "grass";

        // Outer edge of every feature. It faces grass but never another border, so features always have grass between them.
        const string Border = "border";

        // Top of a mountain plateau. Kept separate from grass so every cell knows whether it is inside a plateau: with plain
        // grass inside, nothing told the ring's two sides where to meet, and most mountain maps needed hundreds of backtracks.
        const string Plateau = "plateau";
        const string Summit = "summit";
        const string TerraceBorder = "terrace-border";

        // Biomes that may grow to any size but never smaller than 3x3 (2x2 blobs look like stamps). Forests are left out:
        // a 2x2 or 2-wide grove still reads as trees.
        static readonly string[] GrowingBiomes = { "Lake", "Mountain", "Terrace" };

        // A tile cut from the sheet. Rows are counted from the top of the sheet. Sockets go up, right, down, left.
        readonly struct TileDef
        {
            public readonly string Name;
            public readonly int Column;
            public readonly int Row;
            public readonly float Weight;
            public readonly string Up, Right, Down, Left;

            public TileDef(string name, int column, int row, float weight, string up, string right, string down, string left)
            {
                Name = name;
                Column = column;
                Row = row;
                Weight = weight;
                Up = up;
                Right = right;
                Down = down;
                Left = left;
            }
        }

        // Plain grass dominates, so features read as landmarks in open country rather than a patchwork.
        static IEnumerable<TileDef> Meadow() => Meadow(10f, 5f, 0.5f);

        // The Overworld has busier ground, picked by the user from the WFC Studio window: more tufts and flowers.
        static IEnumerable<TileDef> OverworldMeadow() => Meadow(5f, 8f, 1f);

        static IEnumerable<TileDef> Meadow(float grass, float tufts, float flowers)
        {
            yield return Plain("Grass", 0, 0, grass);
            yield return Plain("Grass Tufts", 1, 0, tufts);
            yield return Plain("White Flowers", 7, 40, flowers);
            yield return Plain("Red Flowers", 8, 40, flowers);
            yield return Plain("Bush", 2, 43, 0.3f);
            yield return Plain("Tree", 3, 40, 0.8f);
        }

        static TileDef Plain(string name, int column, int row, float weight)
        {
            return new TileDef(name, column, row, weight, Grass, Grass, Grass, Grass);
        }

        // A ring of eight border pieces around an interior. The outside is the border socket; each side of the ring carries
        // its own socket, so the ring can only continue itself and always closes into a rectangle.
        // Weights are the tuning knobs: every ring piece can start a feature next to grass, so the total ring weight sets how
        // often features appear; a side piece versus the corner that would end it sets how long the sides grow; and the
        // centre weight fills the inside. With interior = Grass there is no centre tile: meadow tiles (and smaller
        // features) fill the inside instead.
        static IEnumerable<TileDef> Blob(string biome, string socket, int column, int row, float cornerWeight, float sideWeight, float centreWeight, string interior, string outer = Border)
        {
            string top = socket + "-top", bottom = socket + "-bottom", left = socket + "-left", right = socket + "-right";
            int c = column, r = row;

            yield return new TileDef(biome + " Top Left", c, r, cornerWeight, outer, top, left, outer);
            yield return new TileDef(biome + " Top", c + 1, r, sideWeight, outer, top, interior, top);
            yield return new TileDef(biome + " Top Right", c + 2, r, cornerWeight, outer, outer, right, top);
            yield return new TileDef(biome + " Left", c, r + 1, sideWeight, left, interior, left, outer);
            if (interior != Grass)
                yield return new TileDef(biome + " Centre", c + 1, r + 1, centreWeight, interior, interior, interior, interior);
            yield return new TileDef(biome + " Right", c + 2, r + 1, sideWeight, right, outer, right, interior);
            yield return new TileDef(biome + " Bottom Left", c, r + 2, cornerWeight, left, bottom, outer, outer);
            yield return new TileDef(biome + " Bottom", c + 1, r + 2, sideWeight, interior, bottom, outer, bottom);
            yield return new TileDef(biome + " Bottom Right", c + 2, r + 2, cornerWeight, right, outer, outer, bottom);
        }

        // Concave corners, where a ring's top edge steps up or its bottom edge steps down. They let the ring bend instead of
        // being a strict rectangle, so its sides no longer have to end on the same row or column to close. The sockets come
        // from the sheet's edge pixels: each piece continues the rim, side or cliff profile of the outer ring it joins.
        static IEnumerable<TileDef> InnerCorners(string biome, string socket, int column, int row, float weight, string interior)
        {
            string top = socket + "-top", bottom = socket + "-bottom", left = socket + "-left", right = socket + "-right";

            yield return new TileDef(biome + " Inner Top Left", column, row, weight, left, interior, interior, top);
            yield return new TileDef(biome + " Inner Top Right", column + 1, row, weight, right, top, interior, interior);
            yield return new TileDef(biome + " Inner Bottom Left", column, row + 1, weight, interior, interior, left, bottom);
            yield return new TileDef(biome + " Inner Bottom Right", column + 1, row + 1, weight, interior, bottom, right, interior);
        }

        // A structure that only ever appears as exactly 3x3: every inner edge has a socket of its own, used by just the two
        // tiles that meet there.
        static IEnumerable<TileDef> Fixed3x3(string biome, string socket, int column, int row, float weight)
        {
            string[] rowNames = { "Top", "", "Bottom" };
            string[] columnNames = { "Left", "", "Right" };
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    string name = $"{biome} {rowNames[y]} {columnNames[x]}".Replace("  ", " ").Trim();
                    if (x == 1 && y == 1)
                        name = biome + " Centre";

                    string up = y == 0 ? Border : $"{socket}-v{x}{y - 1}";
                    string down = y == 2 ? Border : $"{socket}-v{x}{y}";
                    string left = x == 0 ? Border : $"{socket}-h{y}{x - 1}";
                    string right = x == 2 ? Border : $"{socket}-h{y}{x}";
                    yield return new TileDef(name, column + x, row + y, weight, up, right, down, left);
                }
            }
        }

        static IEnumerable<TileDef> Lake() => Blob("Lake", "lake", 7, 10, 0.06f, 0.15f, 0.5f, "lake");
        // Corners outweigh sides so most forests close early as small groves; a big forest still shows up now and then.
        static IEnumerable<TileDef> Forest() => Blob("Forest", "forest", 11, 40, 0.14f, 0.12f, 0.4f, "forest");
        // Inner corners are rare: frequent bends turn plateaus into maze-like walls.
        static IEnumerable<TileDef> Mountain() =>
            Blob("Mountain", "mountain", 0, 4, 0.07f, 0.16f, 0.5f, Plateau)
                .Concat(InnerCorners("Mountain", "mountain", 3, 4, 0.01f, Plateau))
                .Concat(PlateauDecor())
                .Concat(Terrace());

        // A second, higher plateau that only sits on top of a mountain. Its own border and summit sockets keep every level
        // distinct. Measured on 200 seeds: letting ordinary features (another mountain, a lake) sit on a plateau brought back
        // the "inside or outside?" ambiguity one level up, and most mountain maps backtracked again; with a dedicated level
        // there were no backtracks at all.
        static IEnumerable<TileDef> Terrace()
        {
            foreach (var def in Blob("Terrace", "terrace", 0, 4, 0.05f, 0.14f, 0.4f, Summit, TerraceBorder))
                yield return def;
            yield return new TileDef("Summit Tufts", 1, 0, 0.2f, Summit, Summit, Summit, Summit);
            yield return new TileDef("Summit Tree", 3, 40, 0.1f, Summit, Summit, Summit, Summit);
        }

        // Meadow decoration on top of plateaus: the same sprites, as separate tiles with the plateau socket.
        static IEnumerable<TileDef> PlateauDecor()
        {
            yield return new TileDef("Plateau Tufts", 1, 0, 0.3f, Plateau, Plateau, Plateau, Plateau);
            yield return new TileDef("Plateau Flowers", 7, 40, 0.1f, Plateau, Plateau, Plateau, Plateau);
            yield return new TileDef("Plateau Tree", 3, 40, 0.15f, Plateau, Plateau, Plateau, Plateau);
            yield return new TileDef("Plateau Bush", 2, 43, 0.08f, Plateau, Plateau, Plateau, Plateau);
        }
        static IEnumerable<TileDef> Castle() => Fixed3x3("Castle", "castle", 2, 45, 0.03f);

        // Forbids the shortcuts that would make a growing biome 2 wide or 2 tall: corner beside corner, and an edge piece
        // directly beside its opposite edge piece.
        static IEnumerable<AdjacencyException> MinimumThreeByThree(string biome, Dictionary<string, Tile> tiles)
        {
            (string, ExceptionDirection, string)[] shortcuts =
            {
                ("Top Left", ExceptionDirection.Right, "Top Right"),
                ("Left", ExceptionDirection.Right, "Right"),
                ("Bottom Left", ExceptionDirection.Right, "Bottom Right"),
                ("Top Left", ExceptionDirection.Down, "Bottom Left"),
                ("Top", ExceptionDirection.Down, "Bottom"),
                ("Top Right", ExceptionDirection.Down, "Bottom Right"),
            };
            foreach (var (tile, direction, neighbour) in shortcuts)
                yield return new AdjacencyException(ExceptionKind.Forbid, tiles[$"{biome} {tile}"], direction, tiles[$"{biome} {neighbour}"]);
        }

        static readonly (string Name, Func<IEnumerable<TileDef>> Tiles)[] Sets =
        {
            ("Meadow", Meadow),
            ("Lake", () => Meadow().Concat(Lake())),
            ("Forest", () => Meadow().Concat(Forest())),
            ("Mountain", () => Meadow().Concat(Mountain())),
            ("Castle", () => Meadow().Concat(Castle())),
            ("Overworld", () => OverworldMeadow().Concat(Lake()).Concat(Forest()).Concat(Mountain()).Concat(Castle())),
        };

        [MenuItem("Tools/WFC Studio/Dev/Rebuild Puny World Samples")]
        public static void Build()
        {
            Directory.CreateDirectory(TilesFolder);
            var allTiles = Sets.SelectMany(set => set.Tiles()).GroupBy(def => def.Name).Select(group => group.First()).ToList();
            var tiles = BuildTiles(allTiles);

            foreach (var (setName, defsOf) in Sets)
            {
                var defs = defsOf().ToList();
                var entries = defs.Select(def => new TileEntry(def.Name, tiles[def.Name], def.Weight, def.Up, def.Right, def.Down, def.Left));
                var socketNames = defs.SelectMany(def => new[] { def.Up, def.Right, def.Down, def.Left }).Distinct().ToList();
                var sockets = socketNames.Select(name =>
                    name == Border ? new SocketDefinition(Border, false, Grass) :
                    name == TerraceBorder ? new SocketDefinition(TerraceBorder, false, Plateau) :
                    new SocketDefinition(name));

                var exceptions = new List<AdjacencyException>();
                foreach (string biome in GrowingBiomes)
                    if (defs.Any(def => def.Name == biome + " Top Left"))
                        exceptions.AddRange(MinimumThreeByThree(biome, tiles));

                if (setName == "Overworld")
                {
                    // Forests cover the clustered trees here, so keep single trees apart.
                    exceptions.Add(new AdjacencyException(ExceptionKind.Forbid, tiles["Tree"], ExceptionDirection.Any, tiles["Tree"]));
                }

                var set = GetOrCreate<TileSet>($"{Root}/{setName}.asset");
                set.SetContents(sockets, entries, exceptions);
                EditorUtility.SetDirty(set);

                var result = set.Compile();
                foreach (var issue in result.Issues)
                    Debug.Log($"[{setName}] {issue}");
                if (result.HasErrors)
                    throw new InvalidOperationException($"Tile set '{setName}' has errors; see the log.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Built {Sets.Length} Puny World tile sets from {allTiles.Count} tiles.");
        }

        // Copies every used tile into its own cell of a new sheet, repeating its border pixels outward. On the original
        // sheet many tiles border fully transparent cells, and at non-integer zoom a point-sampled tile can pick up a texel
        // just outside its rect, which shows as thin seams. With extruded edges, that texel is the tile's own edge colour.
        static Dictionary<string, Tile> BuildTiles(List<TileDef> defs)
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(SheetPath));

            int cell = CellSize + 2 * Extrusion;
            int rows = (defs.Count + ExtrudedColumns - 1) / ExtrudedColumns;
            var sheet = new Texture2D(ExtrudedColumns * cell, rows * cell, TextureFormat.RGBA32, false);
            sheet.SetPixels32(new Color32[sheet.width * sheet.height]);

            var rects = new Dictionary<string, Rect>();
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                int sourceX = def.Column * CellSize;
                int sourceY = source.height - (def.Row + 1) * CellSize;
                int targetX = i % ExtrudedColumns * cell;
                int targetY = (rows - 1 - i / ExtrudedColumns) * cell;

                for (int y = 0; y < cell; y++)
                {
                    for (int x = 0; x < cell; x++)
                    {
                        int fromX = sourceX + Mathf.Clamp(x - Extrusion, 0, CellSize - 1);
                        int fromY = sourceY + Mathf.Clamp(y - Extrusion, 0, CellSize - 1);
                        sheet.SetPixel(targetX + x, targetY + y, source.GetPixel(fromX, fromY));
                    }
                }
                rects[def.Name] = new Rect(targetX + Extrusion, targetY + Extrusion, CellSize, CellSize);
            }

            File.WriteAllBytes(ExtrudedSheetPath, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(sheet);
            ImportExtrudedSheet();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ExtrudedSheetPath);
            var tiles = new Dictionary<string, Tile>();
            foreach (var def in defs)
                tiles[def.Name] = GetOrCreateTile(def.Name, texture, rects[def.Name]);

            // Remove tiles that are no longer in the tables, so the folder always matches the code.
            foreach (string guid in AssetDatabase.FindAssets("t:Tile", new[] { TilesFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!tiles.ContainsKey(Path.GetFileNameWithoutExtension(path)))
                    AssetDatabase.DeleteAsset(path);
            }
            return tiles;
        }

        static void ImportExtrudedSheet()
        {
            AssetDatabase.ImportAsset(ExtrudedSheetPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ExtrudedSheetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = CellSize;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // Each Tile asset holds its own sprite as a sub-asset, cut from the extruded sheet.
        static Tile GetOrCreateTile(string name, Texture2D texture, Rect rect)
        {
            string path = $"{TilesFolder}/{name}.asset";
            var tile = GetOrCreate<Tile>(path);
            foreach (var old in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                UnityEngine.Object.DestroyImmediate(old, true);

            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), CellSize, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            AssetDatabase.AddObjectToAsset(sprite, tile);

            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        /// <summary>Generates a map from each sample tile set and writes it as a PNG, for reviewing the socket labels.</summary>
        [MenuItem("Tools/WFC Studio/Dev/Render Sample Previews")]
        public static void RenderPreviews()
        {
            RenderPreviews(Path.Combine("Library", "WfcPreviews"), 40, 24, 3);
        }

        public static void RenderPreviews(string folder, int width, int height, int seeds)
        {
            Directory.CreateDirectory(folder);
            var sheet = new Texture2D(2, 2);
            sheet.LoadImage(File.ReadAllBytes(ExtrudedSheetPath));

            foreach (var (setName, _) in Sets)
            {
                var set = AssetDatabase.LoadAssetAtPath<TileSet>($"{Root}/{setName}.asset");
                var rules = set.Compile().Rules;
                for (uint seed = 1; seed <= seeds; seed++)
                {
                    var solver = new WfcSolver(rules, width, height, seed);
                    var status = solver.Run();
                    var image = new Texture2D(width * CellSize, height * CellSize, TextureFormat.RGBA32, false);
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int tile = solver.GetTile(solver.CellIndex(x, y));
                            var sprite = tile >= 0 ? (set.Tiles[tile].Tile as Tile)?.sprite : null;
                            Color[] pixels = sprite != null
                                ? sheet.GetPixels((int)sprite.rect.x, (int)sprite.rect.y, CellSize, CellSize)
                                : Enumerable.Repeat(Color.magenta, CellSize * CellSize).ToArray();
                            image.SetPixels(x * CellSize, y * CellSize, CellSize, CellSize, pixels);
                        }
                    }

                    string file = Path.Combine(folder, $"{setName}-{seed}.png");
                    File.WriteAllBytes(file, image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                    Debug.Log($"[{setName}] seed {seed}: {status}, {solver.Steps} steps, {solver.Backtracks} backtracks, {solver.Restarts} restarts -> {file}");
                }
            }

            UnityEngine.Object.DestroyImmediate(sheet);
        }

        // Entry point for batch mode: -executeMethod WfcStudio.Dev.PunyWorldSampleBuilder.BuildFromCommandLine [-wfcPreviewDir <folder>]
        public static void BuildFromCommandLine()
        {
            Build();

            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-wfcPreviewDir");
            if (index >= 0 && index + 1 < args.Length)
                RenderPreviews(args[index + 1], 40, 24, 3);
        }

        static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
