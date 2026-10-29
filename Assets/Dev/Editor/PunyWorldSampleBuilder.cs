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
        const string TilesFolder = Root + "/Tiles";
        const int CellSize = 16;
        const string Grass = "grass";

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

        static IEnumerable<TileDef> Meadow()
        {
            yield return Plain("Grass", 0, 0, 8f);
            yield return Plain("Grass Tufts", 1, 0, 4f);
            yield return Plain("White Flowers", 7, 40, 0.4f);
            yield return Plain("Red Flowers", 8, 40, 0.4f);
            yield return Plain("Bush", 2, 43, 0.3f);
            yield return Plain("Stone", 6, 47, 0.3f);
            yield return Plain("Tree", 3, 40, 0.6f);
        }

        static TileDef Plain(string name, int column, int row, float weight)
        {
            return new TileDef(name, column, row, weight, Grass, Grass, Grass, Grass);
        }

        // A 3x3 "blob" whose outer edges are grass, so it can sit anywhere in a meadow and grow to any size of 2x2 or more.
        // Inner edges carry the biome socket; the border pieces carry one socket per side, so borders only continue each other.
        static IEnumerable<TileDef> Blob(string biome, string socket, int column, int row, float weight)
        {
            string top = socket + "-top", bottom = socket + "-bottom", left = socket + "-left", right = socket + "-right";
            int c = column, r = row;

            yield return new TileDef(biome + " Top Left", c, r, weight, Grass, top, left, Grass);
            yield return new TileDef(biome + " Top", c + 1, r, weight, Grass, top, socket, top);
            yield return new TileDef(biome + " Top Right", c + 2, r, weight, Grass, Grass, right, top);
            yield return new TileDef(biome + " Left", c, r + 1, weight, left, socket, left, Grass);
            yield return new TileDef(biome + " Centre", c + 1, r + 1, weight * 2f, socket, socket, socket, socket);
            yield return new TileDef(biome + " Right", c + 2, r + 1, weight, right, Grass, right, socket);
            yield return new TileDef(biome + " Bottom Left", c, r + 2, weight, left, bottom, Grass, Grass);
            yield return new TileDef(biome + " Bottom", c + 1, r + 2, weight, socket, bottom, Grass, bottom);
            yield return new TileDef(biome + " Bottom Right", c + 2, r + 2, weight, right, Grass, Grass, bottom);
        }

        static IEnumerable<TileDef> Lake() => Blob("Lake", "lake", 7, 10, 1f);
        static IEnumerable<TileDef> Mountain() => Blob("Mountain", "mountain", 0, 4, 1f);
        static IEnumerable<TileDef> Forest() => Blob("Forest", "forest", 11, 40, 1f);
        static IEnumerable<TileDef> Castle() => Blob("Castle", "castle", 2, 45, 0.5f);

        static readonly (string Name, Func<IEnumerable<TileDef>> Tiles)[] Sets =
        {
            ("Meadow", Meadow),
            ("Lake", () => Meadow().Concat(Lake())),
            ("Forest", () => Meadow().Concat(Forest())),
            ("Mountain", () => Meadow().Concat(Mountain())),
            ("Castle", () => Meadow().Concat(Castle())),
            ("Overworld", () => Meadow().Concat(Lake()).Concat(Forest()).Concat(Mountain()).Concat(Castle())),
        };

        [MenuItem("Tools/WFC Studio/Dev/Rebuild Puny World Samples")]
        public static void Build()
        {
            var sprites = LoadSprites();
            Directory.CreateDirectory(TilesFolder);

            foreach (var (setName, tiles) in Sets)
            {
                var defs = tiles().ToList();
                var entries = defs.Select(def => new TileEntry(def.Name, GetOrCreateTile(def, sprites), def.Weight, def.Up, def.Right, def.Down, def.Left));
                var sockets = defs.SelectMany(def => new[] { def.Up, def.Right, def.Down, def.Left })
                    .Distinct()
                    .Select(name => new SocketDefinition(name));

                var exceptions = new List<AdjacencyException>();
                if (setName == "Overworld")
                {
                    // Forests cover the clustered trees here, so keep single trees apart.
                    var tree = LoadTile("Tree");
                    exceptions.Add(new AdjacencyException(ExceptionKind.Forbid, tree, ExceptionDirection.Any, tree));
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
            Debug.Log($"Built {Sets.Length} Puny World tile sets.");
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
            sheet.LoadImage(File.ReadAllBytes(SheetPath));

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

        static Dictionary<(int, int), Sprite> LoadSprites()
        {
            var sprites = new Dictionary<(int, int), Sprite>();
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>())
            {
                var rect = sprite.rect;
                if ((int)rect.width != CellSize || (int)rect.height != CellSize)
                    continue;

                int column = (int)rect.x / CellSize;
                int row = (sprite.texture.height - (int)rect.y - CellSize) / CellSize;
                sprites[(column, row)] = sprite;
            }

            if (sprites.Count == 0)
                throw new InvalidOperationException($"No {CellSize}x{CellSize} sprites found in {SheetPath}.");
            return sprites;
        }

        static Tile GetOrCreateTile(TileDef def, Dictionary<(int, int), Sprite> sprites)
        {
            if (!sprites.TryGetValue((def.Column, def.Row), out var sprite))
                throw new InvalidOperationException($"No sprite at column {def.Column}, row {def.Row} for '{def.Name}'.");

            var tile = GetOrCreate<Tile>($"{TilesFolder}/{def.Name}.asset");
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        static Tile LoadTile(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Tile>($"{TilesFolder}/{name}.asset");
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
