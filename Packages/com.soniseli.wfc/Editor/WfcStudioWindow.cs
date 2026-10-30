using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using WfcStudio.Core;

namespace WfcStudio.Editor
{
    /// <summary>
    /// The WFC Studio window: pick a generator and tile set, then generate, step and clear maps in Edit or Play mode.
    /// Settings are bound to the generator's serialized fields, so every change goes through Undo like the Inspector.
    /// </summary>
    public sealed class WfcStudioWindow : EditorWindow
    {
        const string UxmlPath = "Packages/com.soniseli.wfc/Editor/UI/WfcStudioWindow.uxml";

        // Serialized so the window keeps its target across domain reloads.
        [SerializeField] WfcGenerator _generator;

        EditModeGeneration _session;
        TilesPanel _tiles;
        ValidationPanel _validation;
        PinsPanel _pins;
        VisualElement _pinsSection;
        TileSet _tileSet;
        ObjectField _generatorField;
        VisualElement _noGenerator;
        VisualElement _generatorSettings;
        VisualElement _generateSection;
        Label _tileSetSummary;
        Label _status;
        IntegerField _seed;
        Button _pause;
        Button _step;
        Button _finish;

        [MenuItem("Window/WFC Studio")]
        public static void Open()
        {
            GetWindow<WfcStudioWindow>("WFC Studio");
        }

        void CreateGUI()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            tree.CloneTree(rootVisualElement);
            var root = rootVisualElement;

            _generatorField = root.Q<ObjectField>("generator");
            _generatorField.objectType = typeof(WfcGenerator);
            _generatorField.RegisterValueChangedCallback(evt => SetGenerator(evt.newValue as WfcGenerator));

            _noGenerator = root.Q("noGenerator");
            _generatorSettings = root.Q("generatorSettings");
            _generateSection = root.Q("generateSection");
            _tileSetSummary = root.Q<Label>("tileSetSummary");
            _status = root.Q<Label>("status");
            _seed = root.Q<IntegerField>("seed");
            _pause = root.Q<Button>("pause");
            _step = root.Q<Button>("step");
            _finish = root.Q<Button>("finish");

            root.Q<ObjectField>("tilemap").objectType = typeof(Tilemap);
            var tileSetField = root.Q<ObjectField>("tileSet");
            tileSetField.objectType = typeof(TileSet);
            // The event carries the new value; the generator's field may not be written yet when it arrives.
            tileSetField.RegisterValueChangedCallback(evt => ShowTileSet(evt.newValue as TileSet));

            var tilesSection = root.Q<Foldout>("tilesSection");
            _tiles = new TilesPanel(tilesSection);
            _validation = new ValidationPanel(root.Q<Foldout>("validationSection"));
            _tiles.Edited += () =>
            {
                RefreshTileSetSummary();
                _validation.Refresh();
            };
            _validation.ShowTileRequested += tile =>
            {
                tilesSection.value = true;
                _tiles.ShowTile(tile);
                var scroll = root.Q<ScrollView>("root");
                scroll.schedule.Execute(() => scroll.ScrollTo(root.Q("adjacency")));
            };

            root.Q<Button>("createGenerator").clicked += CreateGenerator;
            root.Q<Button>("randomSeed").clicked += () => _seed.value = Random.Range(1, 100000);
            root.Q<Button>("generate").clicked += () => _session?.Generate();
            root.Q<Button>("instant").clicked += () => _session?.GenerateInstantly();
            _pause.clicked += TogglePause;
            _step.clicked += () => _session?.StepOnce();
            _finish.clicked += () => _session?.Complete();
            root.Q<Button>("clear").clicked += () => _session?.Clear();

            _pinsSection = root.Q("pinsSection");
            _pins = new PinsPanel(_pinsSection);

            // Play mode generation runs from the generator's own Update, and pins are painted in the Scene view,
            // so poll rather than wait for our own events.
            root.schedule.Execute(() =>
            {
                RefreshStatus();
                _pins.Refresh();
            }).Every(100);

            SetGenerator(_generator != null ? _generator : FindGenerator());
        }

        void OnDisable()
        {
            _session?.Dispose();
            _session = null;
        }

        void OnSelectionChange()
        {
            var selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<WfcGenerator>() : null;
            if (selected != null && selected != _generator)
                SetGenerator(selected);
        }

        void OnHierarchyChange()
        {
            // The target was deleted: fall back to another generator in the scene, if any.
            if (_generatorField != null && _generator == null && _session != null)
                SetGenerator(FindGenerator());
        }

        void SetGenerator(WfcGenerator generator)
        {
            _session?.Dispose();
            _session = generator != null ? new EditModeGeneration(generator) : null;
            if (_session != null)
                _session.Changed += RefreshStatus;
            _generator = generator;
            _generatorField.SetValueWithoutNotify(generator);

            bool hasGenerator = generator != null;
            _noGenerator.EnableInClassList("hidden", hasGenerator);

            // Bind only the generator's own fields: the Tiles section is bound to the tile set asset instead.
            var serialized = hasGenerator ? new SerializedObject(generator) : null;
            foreach (var section in new[] { _generatorSettings, _pinsSection, _generateSection })
            {
                section.EnableInClassList("hidden", !hasGenerator);
                if (hasGenerator)
                    section.Bind(serialized);
                else
                    section.Unbind();
            }

            _pins.SetGenerator(generator);
            ShowTileSet(hasGenerator ? generator.TileSet : null);
            RefreshStatus();
        }

        static WfcGenerator FindGenerator()
        {
            var selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<WfcGenerator>() : null;
            return selected != null ? selected : FindFirstObjectByType<WfcGenerator>();
        }

        // A Grid with a Tilemap child and a generator that draws into it, ready for a tile set.
        void CreateGenerator()
        {
            var grid = new GameObject("WFC Map", typeof(Grid));
            var map = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
            map.transform.SetParent(grid.transform, false);

            var generator = grid.AddComponent<WfcGenerator>();
            generator.Tilemap = map.GetComponent<Tilemap>();
            generator.GenerateOnStart = false;

            Undo.RegisterCreatedObjectUndo(grid, "Create WFC Generator");
            Selection.activeGameObject = grid;
            SetGenerator(generator);
        }

        void TogglePause()
        {
            if (_session == null)
                return;

            if (_generator.IsPaused)
                _session.Resume();
            else
                _session.Pause();
        }

        void ShowTileSet(TileSet tileSet)
        {
            _tileSet = tileSet;
            _tiles.SetTileSet(tileSet);
            _validation.SetTileSet(tileSet);
            RefreshTileSetSummary();
        }

        void RefreshTileSetSummary()
        {
            var tileSet = _tileSet;
            if (tileSet == null)
            {
                _tileSetSummary.text = "Pick a tile set to generate with.";
                return;
            }

            var issues = tileSet.Compile().Issues;
            int errors = issues.Count(issue => issue.Severity == IssueSeverity.Error);
            int warnings = issues.Count(issue => issue.Severity == IssueSeverity.Warning);
            string health = errors > 0 ? $"{Plural(errors, "error")}, can't generate"
                : warnings > 0 ? Plural(warnings, "warning")
                : "no issues";
            _tileSetSummary.text = $"{Plural(tileSet.Tiles.Count, "tile")} · {Plural(tileSet.Sockets.Count, "socket")} · {health}";
        }

        void RefreshStatus()
        {
            if (_status == null)
                return;

            var solver = _generator != null ? _generator.Solver : null;
            bool generating = _generator != null && _generator.IsGenerating;
            _pause.text = generating && _generator.IsPaused ? "Resume" : "Pause";
            _pause.SetEnabled(generating);
            _finish.SetEnabled(generating);
            _step.SetEnabled(_generator != null && (!generating || _generator.IsPaused));

            if (solver == null)
            {
                _status.text = "No map in progress.";
                return;
            }

            int decided = 0;
            for (int cell = 0; cell < solver.CellCount; cell++)
                if (solver.IsCollapsed(cell))
                    decided++;

            string state = generating && _generator.IsPaused ? "Paused" : solver.Status.ToString();
            _status.text = $"{state} · {decided} / {solver.CellCount} cells decided\n" +
                $"{Plural(solver.Steps, "step")} · {Plural(solver.Backtracks, "backtrack")} · {Plural(solver.Restarts, "restart")} · " +
                $"{_generator.SolveTime.TotalMilliseconds:0.0} ms";
        }

        internal static string Plural(int count, string noun)
        {
            return $"{count} {noun}{(count == 1 ? "" : "s")}";
        }
    }
}
