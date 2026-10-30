using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using WfcStudio.Demo;

namespace WfcStudio.Dev
{
    /// <summary>Rebuilds the demo scene from code and makes WebGL builds of it, so both are reproducible from batch mode.</summary>
    public static class DemoSceneBuilder
    {
        const string ScenePath = "Assets/Demo/Demo.unity";
        const string UiFolder = "Assets/Demo/UI";
        const string PanelSettingsPath = UiFolder + "/DemoPanelSettings.asset";
        const string SamplesFolder = "Assets/Samples/PunyWorld";
        const string SampleScenePath = SamplesFolder + "/Puny World.unity";

        // Overworld first: it is the default and shows every biome.
        static readonly string[] TileSetOrder = { "Overworld", "Lake", "Forest", "Mountain", "Castle", "Meadow" };

        [MenuItem("Tools/WFC Studio/Dev/Rebuild Demo Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 15f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.106f, 0.122f, 0.141f);

            // MSAA and HDR soften pixel-art edges and add nothing to a flat tile map.
            camera.allowMSAA = false;
            camera.allowHDR = false;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var light = new GameObject("Global Light 2D", typeof(Light2D)).GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;

            var grid = new GameObject("Grid", typeof(Grid));
            var map = new GameObject("Map", typeof(Tilemap), typeof(TilemapRenderer));
            map.transform.SetParent(grid.transform, false);
            var tilemap = map.GetComponent<Tilemap>();

            var tileSets = TileSetOrder.Select(name => AssetDatabase.LoadAssetAtPath<TileSet>($"{SamplesFolder}/{name}.asset")).ToArray();
            if (tileSets.Any(set => set == null))
                throw new InvalidOperationException("Sample tile sets are missing. Run Rebuild Puny World Samples first.");

            var generatorObject = new GameObject("WFC Generator");
            var generator = generatorObject.AddComponent<WfcGenerator>();
            generator.TileSet = tileSets[0];
            generator.Tilemap = tilemap;
            generator.Size = new Vector2Int(48, 28);
            generator.Seed = 1;
            generator.StepsPerSecond = 300f;
            generator.GenerateOnStart = true;
            generatorObject.AddComponent<EntropyHeatmap>().Generator = generator;
            generatorObject.AddComponent<TilePopAnimation>().Generator = generator;

            var ui = new GameObject("UI", typeof(UIDocument));
            var document = ui.GetComponent<UIDocument>();
            document.panelSettings = GetOrCreatePanelSettings();
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{UiFolder}/Demo.uxml");

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var controller = new GameObject("Demo").AddComponent<DemoController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_document").objectReferenceValue = document;
            serialized.FindProperty("_generator").objectReferenceValue = generator;
            serialized.FindProperty("_heatmap").objectReferenceValue = generatorObject.GetComponent<EntropyHeatmap>();
            serialized.FindProperty("_camera").objectReferenceValue = camera;
            var sets = serialized.FindProperty("_tileSets");
            sets.arraySize = tileSets.Length;
            for (int i = 0; i < tileSets.Length; i++)
                sets.GetArrayElementAtIndex(i).objectReferenceValue = tileSets[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"Built demo scene at {ScenePath}.");
        }

        /// <summary>
        /// The Puny World sample's scene: a generator, its map and an empty Pins Tilemap, and nothing that needs URP or
        /// the Input System, so it works in any project. Shipped in the package's samples.
        /// </summary>
        [MenuItem("Tools/WFC Studio/Dev/Rebuild Sample Scene")]
        public static void BuildSampleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 15f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.106f, 0.122f, 0.141f);
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var grid = new GameObject("Grid", typeof(Grid));
            var map = new GameObject("Map", typeof(Tilemap), typeof(TilemapRenderer));
            map.transform.SetParent(grid.transform, false);
            var pins = new GameObject("Pins", typeof(Tilemap), typeof(TilemapRenderer));
            pins.transform.SetParent(grid.transform, false);
            pins.GetComponent<Tilemap>().color = new Color(1f, 1f, 1f, 0.5f);
            pins.GetComponent<TilemapRenderer>().sortingOrder = 1;

            var overworld = AssetDatabase.LoadAssetAtPath<TileSet>($"{SamplesFolder}/Overworld.asset");
            if (overworld == null)
                throw new InvalidOperationException("Sample tile sets are missing. Run Rebuild Puny World Samples first.");

            var generatorObject = new GameObject("WFC Generator");
            var generator = generatorObject.AddComponent<WfcGenerator>();
            generator.TileSet = overworld;
            generator.Tilemap = map.GetComponent<Tilemap>();
            generator.Pins = pins.GetComponent<Tilemap>();
            generator.Size = new Vector2Int(48, 28);
            generator.Origin = new Vector3Int(-24, -14, 0);
            generator.Seed = 1;
            generator.StepsPerSecond = 300f;
            generator.GenerateOnStart = true;
            generatorObject.AddComponent<EntropyHeatmap>().Generator = generator;
            generatorObject.AddComponent<TilePopAnimation>().Generator = generator;

            EditorSceneManager.SaveScene(scene, SampleScenePath);
            Debug.Log($"Built sample scene at {SampleScenePath}.");
        }

        static PanelSettings GetOrCreatePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>($"{UiFolder}/DemoTheme.tss");
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // Batch mode: -executeMethod WfcStudio.Dev.DemoSceneBuilder.BuildWebGLFromCommandLine -wfcBuildDir <folder>
        public static void BuildWebGLFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-wfcBuildDir");
            string folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine("Builds", "WebGL");
            BuildWebGL(folder);
        }

        public static void BuildWebGL(string folder)
        {
            // Lets the build run from any static file server, including itch.io, without Content-Encoding headers.
            PlayerSettings.WebGL.decompressionFallback = true;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = folder,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"WebGL build {summary.result}: {summary.totalSize / (1024f * 1024f):0.0} MB in {summary.totalTime.TotalSeconds:0}s -> {folder}");
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"WebGL build failed with {summary.totalErrors} errors.");
        }
    }
}
