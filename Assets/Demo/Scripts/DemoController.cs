using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using WfcStudio.Core;

namespace WfcStudio.Demo
{
    /// <summary>Connects the demo's UI Toolkit panel and keyboard shortcuts to a <see cref="WfcGenerator"/>, and frames the camera on the map.</summary>
    public sealed class DemoController : MonoBehaviour
    {
        // Left margin plus panel width, in pixels, kept clear when framing the map.
        const float PanelInset = 324f;
        const float MaxStepsPerSecond = 5000f;

        [SerializeField] UIDocument _document;
        [SerializeField] WfcGenerator _generator;
        [SerializeField] EntropyHeatmap _heatmap;
        [SerializeField] Camera _camera;
        [SerializeField] TileSet[] _tileSets = Array.Empty<TileSet>();

        DropdownField _tileSetField;
        SliderInt _widthField;
        SliderInt _heightField;
        IntegerField _seedField;
        Toggle _wrapField;
        Slider _speedField;
        Label _speedLabel;
        Toggle _heatmapField;
        Button _pauseButton;
        Label _stats;
        Vector2Int _framedScreen;

        void OnEnable()
        {
            var root = _document.rootVisualElement;
            _tileSetField = root.Q<DropdownField>("tile-set");
            _widthField = root.Q<SliderInt>("width");
            _heightField = root.Q<SliderInt>("height");
            _seedField = root.Q<IntegerField>("seed");
            _wrapField = root.Q<Toggle>("wrap");
            _speedField = root.Q<Slider>("speed");
            _speedLabel = root.Q<Label>("speed-value");
            _heatmapField = root.Q<Toggle>("heatmap");
            _pauseButton = root.Q<Button>("pause");
            _stats = root.Q<Label>("stats");

            _tileSetField.choices = new System.Collections.Generic.List<string>(Array.ConvertAll(_tileSets, set => set.name));
            _tileSetField.index = Mathf.Max(0, Array.IndexOf(_tileSets, _generator.TileSet));
            _widthField.value = _generator.Size.x;
            _heightField.value = _generator.Size.y;
            _seedField.value = _generator.Seed;
            _wrapField.value = _generator.WrapEdges;
            _speedField.value = SliderFromSpeed(_generator.StepsPerSecond);
            _heatmapField.value = _heatmap.enabled;
            UpdateSpeedLabel();

            _speedField.RegisterValueChangedCallback(_ =>
            {
                _generator.StepsPerSecond = SpeedFromSlider(_speedField.value);
                UpdateSpeedLabel();
            });
            _heatmapField.RegisterValueChangedCallback(evt => _heatmap.enabled = evt.newValue);
            root.Q<Button>("random-seed").clicked += RandomSeed;
            root.Q<Button>("generate").clicked += Generate;
            root.Q<Button>("instant").clicked += GenerateInstantly;
            _pauseButton.clicked += TogglePause;
            root.Q<Button>("step").clicked += Step;
        }

        void Update()
        {
            HandleShortcuts();

            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen != _framedScreen)
                FrameCamera();

            UpdateStats();
        }

        void Generate()
        {
            ApplySettings();
            _generator.Generate();
            FrameCamera();
        }

        void GenerateInstantly()
        {
            ApplySettings();
            _generator.GenerateInstantly();
            FrameCamera();
        }

        void TogglePause()
        {
            if (_generator.IsPaused)
                _generator.Resume();
            else
                _generator.Pause();
        }

        void Step()
        {
            if (!_generator.IsGenerating)
            {
                Generate();
                _generator.Pause();
            }
            _generator.StepOnce();
        }

        void RandomSeed()
        {
            _seedField.value = UnityEngine.Random.Range(1, 1_000_000);
        }

        void ApplySettings()
        {
            if (_tileSetField.index >= 0 && _tileSetField.index < _tileSets.Length)
                _generator.TileSet = _tileSets[_tileSetField.index];
            _generator.Size = new Vector2Int(_widthField.value, _heightField.value);
            _generator.Seed = _seedField.value;
            _generator.WrapEdges = _wrapField.value;
        }

        void HandleShortcuts()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || IsTyping())
                return;

            if (keyboard.spaceKey.wasPressedThisFrame)
                TogglePause();
            if (keyboard.nKey.wasPressedThisFrame)
                Step();
            if (keyboard.gKey.wasPressedThisFrame)
                Generate();
            if (keyboard.rKey.wasPressedThisFrame)
            {
                RandomSeed();
                Generate();
            }
        }

        bool IsTyping()
        {
            var focused = _document.rootVisualElement.focusController?.focusedElement as VisualElement;
            return focused != null && (focused is TextField || focused.GetFirstAncestorOfType<IntegerField>() != null || focused.GetFirstAncestorOfType<SliderInt>() != null);
        }

        void UpdateStats()
        {
            _pauseButton.text = _generator.IsPaused ? "Resume" : "Pause";

            var solver = _generator.Solver;
            if (solver == null)
            {
                _stats.text = "Press Generate to start.";
                return;
            }

            int decided = 0;
            for (int cell = 0; cell < solver.CellCount; cell++)
                if (solver.IsCollapsed(cell))
                    decided++;

            string status = _generator.Status switch
            {
                SolverStatus.Completed => "Completed",
                SolverStatus.Failed => "Failed: no solution within the budget",
                _ => _generator.IsPaused ? "Paused" : "Generating",
            };

            _stats.text =
                $"{status}\n" +
                $"{decided} / {solver.CellCount} cells decided\n" +
                $"{solver.Steps} steps · {solver.Backtracks} backtracks · {solver.Restarts} restarts\n" +
                $"Solver time {_generator.SolveTime.TotalMilliseconds:0.0} ms";
        }

        // Fits the whole map in the part of the screen the panel doesn't cover.
        void FrameCamera()
        {
            _framedScreen = new Vector2Int(Screen.width, Screen.height);
            var size = _generator.Size;
            float availableWidth = Mathf.Max(1f, Screen.width - PanelInset);
            float halfHeight = Mathf.Max(size.y / 2f, size.x / 2f * Screen.height / availableWidth) * 1.06f;
            _camera.orthographicSize = halfHeight;

            float unitsPerPixel = 2f * halfHeight / Mathf.Max(1, Screen.height);
            var mapCentre = _generator.Tilemap.CellToWorld(_generator.Origin) + new Vector3(size.x / 2f, size.y / 2f, 0f);
            _camera.transform.position = new Vector3(mapCentre.x - PanelInset / 2f * unitsPerPixel, mapCentre.y, _camera.transform.position.z);
        }

        // The slider is logarithmic: 0 is 1 step per second, 1 is MaxStepsPerSecond.
        static float SpeedFromSlider(float value)
        {
            return Mathf.Round(Mathf.Pow(MaxStepsPerSecond, value));
        }

        static float SliderFromSpeed(float stepsPerSecond)
        {
            return Mathf.Log(Mathf.Max(1f, stepsPerSecond), MaxStepsPerSecond);
        }

        void UpdateSpeedLabel()
        {
            _speedLabel.text = $"{_generator.StepsPerSecond:0} steps per second";
        }
    }
}
