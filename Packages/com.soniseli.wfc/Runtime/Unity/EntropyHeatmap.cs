using UnityEngine;

namespace WfcStudio
{
    /// <summary>
    /// Draws each undecided cell's entropy over the map while a <see cref="WfcGenerator"/> runs: one texel per cell,
    /// coloured by how uncertain the cell still is. Decided cells are transparent.
    /// </summary>
    [AddComponentMenu("WFC Studio/Entropy Heatmap")]
    public sealed class EntropyHeatmap : MonoBehaviour
    {
        [SerializeField] WfcGenerator _generator;

        [Tooltip("Left end: nearly decided. Right end: every tile still possible.")]
        [SerializeField] Gradient _gradient = DefaultGradient();

        [SerializeField] int _sortingOrder = 10;

        WfcGenerator _bound;
        Texture2D _texture;
        Color32[] _pixels;
        SpriteRenderer _renderer;
        float _maxEntropy;

        public WfcGenerator Generator
        {
            get => _generator;
            set => _generator = value;
        }

        /// <summary>The heatmap texture: one texel per solver cell, or null before the first map.</summary>
        public Texture2D Texture => _texture;

        void OnEnable()
        {
            Bind(_generator);
            if (_renderer != null)
                _renderer.enabled = true;

            // Changes made while disabled were not drawn, so repaint everything.
            if (_bound != null && _bound.Solver != null && _texture != null)
                RepaintAll(_bound);
        }

        void OnDisable()
        {
            Bind(null);
            if (_renderer != null)
                _renderer.enabled = false;
        }

        void OnDestroy()
        {
            if (_texture != null)
                Destroy(_texture);
        }

        // Subscribes to a generator's events. Called from OnEnable and OnDisable; internal so tests can bind directly.
        internal void Bind(WfcGenerator generator)
        {
            if (_bound != null)
            {
                _bound.Started -= OnStarted;
                _bound.Redrawn -= OnRedrawn;
            }

            _bound = generator;
            if (_bound != null)
            {
                _bound.Started += OnStarted;
                _bound.Redrawn += OnRedrawn;
            }
        }

        void OnStarted(WfcGenerator generator)
        {
            var solver = generator.Solver;
            EnsureTexture(solver.Width, solver.Height);

            _maxEntropy = 0f;
            for (int cell = 0; cell < solver.CellCount; cell++)
                _maxEntropy = Mathf.Max(_maxEntropy, solver.GetEntropy(cell));

            PlaceOver(generator);
        }

        void OnRedrawn(WfcGenerator generator)
        {
            var solver = generator.Solver;
            foreach (int cell in generator.ChangedCells)
                _pixels[cell] = ColourFor(solver, cell);

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        void RepaintAll(WfcGenerator generator)
        {
            var solver = generator.Solver;
            if (_texture.width != solver.Width || _texture.height != solver.Height)
                return;

            for (int cell = 0; cell < solver.CellCount; cell++)
                _pixels[cell] = ColourFor(solver, cell);
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        Color32 ColourFor(Core.WfcSolver solver, int cell)
        {
            if (solver.IsCollapsed(cell))
                return new Color32(0, 0, 0, 0);
            if (solver.GetPossibleCount(cell) == 0)
                return new Color32(255, 0, 255, 255);

            float t = _maxEntropy > 0f ? solver.GetEntropy(cell) / _maxEntropy : 1f;
            return _gradient.Evaluate(t);
        }

        void EnsureTexture(int width, int height)
        {
            if (_texture != null && _texture.width == width && _texture.height == height)
                return;

            if (_texture != null)
                DestroyImmediate(_texture);

            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Entropy Heatmap",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _pixels = new Color32[width * height];

            if (_renderer == null)
            {
                var child = new GameObject("Entropy Heatmap");
                child.transform.SetParent(transform, false);
                _renderer = child.AddComponent<SpriteRenderer>();
                _renderer.sortingOrder = _sortingOrder;
            }
            _renderer.sprite = Sprite.Create(_texture, new Rect(0, 0, width, height), Vector2.zero, 1f);
            _renderer.enabled = enabled;
        }

        // Lines the texture up with the Tilemap so each texel covers exactly one cell.
        void PlaceOver(WfcGenerator generator)
        {
            var tilemap = generator.Tilemap;
            var cellSize = tilemap.layoutGrid != null ? tilemap.layoutGrid.cellSize : Vector3.one;
            var t = _renderer.transform;
            t.position = tilemap.CellToWorld(generator.Origin);
            t.rotation = tilemap.transform.rotation;
            t.localScale = new Vector3(cellSize.x, cellSize.y, 1f);
        }

        static Gradient DefaultGradient()
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
    }
}
