using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using WfcStudio.Core;

namespace WfcStudio.Dev
{
    /// <summary>
    /// Renders a map generating, frame by frame, for the README's animated GIF. Decided cells show their tile; undecided
    /// cells show the demo's entropy heatmap over its background colour. ffmpeg turns the PNG frames into the GIF.
    /// </summary>
    public static class HeroGifRenderer
    {
        const string SheetPath = "Assets/Samples/PunyWorld/PunyWorldTiles.png";
        const int CellSize = 16;

        static readonly Color Background = new Color(0.106f, 0.122f, 0.141f);

        // Batch mode: -executeMethod WfcStudio.Dev.HeroGifRenderer.RenderFromCommandLine -wfcFramesDir <folder> [-wfcSeed <n>]
        public static void RenderFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string folder = Argument(args, "-wfcFramesDir") ?? Path.Combine("Library", "HeroGif");
            uint seed = uint.Parse(Argument(args, "-wfcSeed") ?? "1");
            Render(folder, "Overworld", 48, 28, seed, 90, 60);
        }

        /// <summary>Writes about <paramref name="frames"/> frames of the map filling in, then the finished map <paramref name="holdFrames"/> more times.</summary>
        public static void Render(string folder, string setName, int width, int height, uint seed, int frames, int holdFrames)
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);

            var set = AssetDatabase.LoadAssetAtPath<TileSet>($"Assets/Samples/PunyWorld/{setName}.asset");
            var rules = set.Compile().Rules;
            var sheet = new Texture2D(2, 2);
            sheet.LoadImage(File.ReadAllBytes(SheetPath));
            var heatmap = HeatmapGradient();

            var probe = new WfcSolver(rules, width, height, seed);
            probe.Run();
            int stride = Math.Max(1, probe.Steps / frames);

            var solver = new WfcSolver(rules, width, height, seed);
            float maxEntropy = 0f;
            for (int cell = 0; cell < solver.CellCount; cell++)
                maxEntropy = Mathf.Max(maxEntropy, solver.GetEntropy(cell));

            var image = new Texture2D(width * CellSize, height * CellSize, TextureFormat.RGBA32, false);
            int written = 0;
            void Write()
            {
                Draw(image, solver, set, sheet, heatmap, maxEntropy);
                File.WriteAllBytes(Path.Combine(folder, $"frame_{written++:D4}.png"), image.EncodeToPNG());
            }

            Write();
            while (solver.Status == SolverStatus.Running)
            {
                solver.Step();
                if (solver.Steps % stride == 0)
                    Write();
            }
            for (int i = 0; i < holdFrames; i++)
                Write();

            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(sheet);
            Debug.Log($"Wrote {written} frames of {setName} seed {seed} ({solver.Steps} steps, {solver.Status}) to {folder}.");
        }

        static void Draw(Texture2D image, WfcSolver solver, TileSet set, Texture2D sheet, Gradient heatmap, float maxEntropy)
        {
            var undecided = new Color[CellSize * CellSize];
            for (int y = 0; y < solver.Height; y++)
            {
                for (int x = 0; x < solver.Width; x++)
                {
                    int cell = solver.CellIndex(x, y);
                    int tile = solver.GetTile(cell);
                    var sprite = tile >= 0 ? (set.Tiles[tile].Tile as Tile)?.sprite : null;
                    if (sprite != null)
                    {
                        image.SetPixels(x * CellSize, y * CellSize, CellSize, CellSize,
                            sheet.GetPixels((int)sprite.rect.x, (int)sprite.rect.y, CellSize, CellSize));
                        continue;
                    }

                    // The same blend as the demo: the heatmap at its own alpha over the camera background.
                    var tint = heatmap.Evaluate(maxEntropy > 0f ? solver.GetEntropy(cell) / maxEntropy : 1f);
                    var colour = Color.Lerp(Background, new Color(tint.r, tint.g, tint.b), tint.a);
                    for (int i = 0; i < undecided.Length; i++)
                        undecided[i] = colour;
                    image.SetPixels(x * CellSize, y * CellSize, CellSize, CellSize, undecided);
                }
            }
        }

        // EntropyHeatmap's default colours: warm for nearly decided cells, deep teal for wide-open ones.
        static Gradient HeatmapGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.82f, 0.4f), 0f),
                    new GradientColorKey(new Color(0.94f, 0.28f, 0.44f), 0.5f),
                    new GradientColorKey(new Color(0.03f, 0.23f, 0.3f), 1f),
                },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.8f, 1f) });
            return gradient;
        }

        static string Argument(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
