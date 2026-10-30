using System;
using System.IO;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace WfcStudio.Dev
{
    /// <summary>
    /// Copies the generated sample assets into the package's Samples~ folder, which the Package Manager offers for import.
    /// Files keep their .meta files, so GUIDs and every reference between them survive the import. Run it after rebuilding
    /// the samples or the demo; Samples~ is never edited by hand.
    /// </summary>
    public static class PackageSampleExporter
    {
        const string SamplesRoot = "Packages/com.soniseli.wfc/Samples~";

        // What ships, by source path. The Puny World source sheet stays behind: only the builder reads it.
        static readonly (string Sample, string Source, string[] Entries)[] Samples =
        {
            ("PunyWorld", "Assets/Samples/PunyWorld", new[]
            {
                "Castle.asset", "Forest.asset", "Lake.asset", "Meadow.asset", "Mountain.asset", "Overworld.asset",
                "Tiles", "PunyWorldTiles.png", "Puny World.unity", "README.md",
            }),
            ("LiveDemo", "Assets/Demo", new[] { "Demo.unity", "Scripts", "UI", "README.md" }),
        };

        [MenuItem("Tools/WFC Studio/Dev/Export Package Samples")]
        public static void Export()
        {
            DemoSceneBuilder.BuildSampleScene();
            AssetDatabase.SaveAssets();

            foreach (var (sample, source, entries) in Samples)
            {
                string target = Path.Combine(SamplesRoot, sample);
                if (Directory.Exists(target))
                    Directory.Delete(target, true);
                Directory.CreateDirectory(target);

                foreach (string entry in entries)
                {
                    string from = Path.Combine(source, entry);
                    string to = Path.Combine(target, entry);
                    if (Directory.Exists(from))
                        CopyDirectory(from, to);
                    else if (File.Exists(from))
                        File.Copy(from, to);
                    else
                        throw new FileNotFoundException($"Sample file '{from}' is missing. Rebuild the samples and the demo first.");

                    File.Copy(from + ".meta", to + ".meta");
                }
                Debug.Log($"Exported sample '{sample}' from {source} to {target}.");
            }
        }

        // Batch mode: -executeMethod WfcStudio.Dev.PackageSampleExporter.ExportFromCommandLine
        public static void ExportFromCommandLine()
        {
            Export();
        }

        static void CopyDirectory(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
            foreach (string directory in Directory.GetDirectories(from))
                CopyDirectory(directory, Path.Combine(to, Path.GetFileName(directory)));
        }
    }
}
