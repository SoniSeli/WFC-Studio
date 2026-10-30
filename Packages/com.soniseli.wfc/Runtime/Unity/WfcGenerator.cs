using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Tilemaps;
using WfcStudio.Core;
using Debug = UnityEngine.Debug;

namespace WfcStudio
{
    /// <summary>
    /// Runs a <see cref="WfcSolver"/> on a <see cref="TileSet"/> and draws the result into a <see cref="Tilemap"/>.
    /// In Play mode it advances a number of steps per second from <c>Update</c>, capped by a per-frame time budget,
    /// so large maps never stall a frame. <see cref="GenerateInstantly"/> solves in one call.
    /// </summary>
    /// <remarks>Solver cell (x, y) is drawn at Tilemap cell <c>origin + (x, y)</c>.</remarks>
    [AddComponentMenu("WFC Studio/WFC Generator")]
    public sealed class WfcGenerator : MonoBehaviour
    {
        [SerializeField] TileSet _tileSet;
        [SerializeField] Tilemap _tilemap;
        [SerializeField] Vector2Int _size = new Vector2Int(32, 20);

        [Tooltip("Optional Tilemap of pinned tiles, painted with the Tile Palette. Each tile on it inside the map area is " +
            "fixed before a map is generated, and the rest of the map is built around it.")]
        [SerializeField] Tilemap _pins;

        [Tooltip("Tilemap cell where solver cell (0, 0) is drawn.")]
        [SerializeField] Vector3Int _origin;

        [SerializeField] int _seed = 1;
        [SerializeField] bool _wrapEdges;

        [Tooltip("Solver steps per second while generating. 0 runs as fast as the frame budget allows.")]
        [Min(0)] [SerializeField] float _stepsPerSecond = 200f;

        [Tooltip("Most time the solver may use per frame, in milliseconds.")]
        [Min(0.1f)] [SerializeField] float _frameBudgetMilliseconds = 4f;

        [Tooltip("Drawn in cells that are not decided yet. Leave empty to show nothing.")]
        [SerializeField] TileBase _undecidedTile;

        [SerializeField] bool _generateOnStart = true;

        WfcSolver _solver;
        RuleTable _rules;
        bool _paused;
        double _stepDebt;
        readonly Stopwatch _solveTime = new Stopwatch();

        // Named samples in the Unity Profiler, so a frame's solver time can be told apart from drawing the Tilemap.
        static readonly ProfilerMarker SolveMarker = new ProfilerMarker("WfcGenerator.Solve");
        static readonly ProfilerMarker RedrawMarker = new ProfilerMarker("WfcGenerator.Redraw");

        // Cells changed since the last redraw, collected across all steps of a frame.
        readonly List<int> _changed = new List<int>();
        bool[] _isChanged = Array.Empty<bool>();

        /// <summary>Raised after a new map is started, before any step.</summary>
        public event Action<WfcGenerator> Started;

        /// <summary>Raised after the Tilemap is updated. <see cref="ChangedCells"/> lists the cells that were redrawn.</summary>
        public event Action<WfcGenerator> Redrawn;

        public TileSet TileSet
        {
            get => _tileSet;
            set => _tileSet = value;
        }

        public Tilemap Tilemap
        {
            get => _tilemap;
            set => _tilemap = value;
        }

        /// <summary>Tilemap whose tiles are pinned before each map, or null for none. Read at the same cells the map is drawn to.</summary>
        public Tilemap Pins
        {
            get => _pins;
            set => _pins = value;
        }

        public Vector2Int Size
        {
            get => _size;
            set => _size = new Vector2Int(Mathf.Max(1, value.x), Mathf.Max(1, value.y));
        }

        public Vector3Int Origin
        {
            get => _origin;
            set => _origin = value;
        }

        public int Seed
        {
            get => _seed;
            set => _seed = value;
        }

        public bool WrapEdges
        {
            get => _wrapEdges;
            set => _wrapEdges = value;
        }

        public float StepsPerSecond
        {
            get => _stepsPerSecond;
            set => _stepsPerSecond = Mathf.Max(0f, value);
        }

        public float FrameBudgetMilliseconds
        {
            get => _frameBudgetMilliseconds;
            set => _frameBudgetMilliseconds = Mathf.Max(0.1f, value);
        }

        public TileBase UndecidedTile
        {
            get => _undecidedTile;
            set => _undecidedTile = value;
        }

        public bool GenerateOnStart
        {
            get => _generateOnStart;
            set => _generateOnStart = value;
        }

        /// <summary>The solver for the current map, or null before the first generation.</summary>
        public WfcSolver Solver => _solver;

        /// <summary>Status of the current map, or null before the first generation.</summary>
        public SolverStatus? Status => _solver?.Status;

        public bool IsGenerating => _solver != null && _solver.Status == SolverStatus.Running;

        public bool IsPaused => _paused;

        /// <summary>Time spent inside the solver for the current map, excluding drawing and idle frames.</summary>
        public TimeSpan SolveTime => _solveTime.Elapsed;

        /// <summary>Cells redrawn by the last update, as solver cell indices.</summary>
        public IReadOnlyList<int> ChangedCells => _changed;

        void Start()
        {
            if (_generateOnStart)
                Generate();
        }

        void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Starts a new map with the current settings. Steps run from <c>Update</c> until it completes.</summary>
        public bool Generate()
        {
            if (!BeginMap())
                return false;

            _paused = false;
            return true;
        }

        /// <summary>Starts a new map and solves it completely in this call.</summary>
        public SolverStatus GenerateInstantly()
        {
            if (!BeginMap())
                return SolverStatus.Failed;

            BeginSolve();
            while (_solver.Status == SolverStatus.Running)
            {
                _solver.Step();
                CollectChanges();
            }
            EndSolve();

            Redraw();
            return _solver.Status;
        }

        public void Pause()
        {
            _paused = true;
        }

        public void Resume()
        {
            _paused = false;
        }

        /// <summary>Runs a single step and redraws, whether or not generation is paused.</summary>
        public void StepOnce()
        {
            if (!IsGenerating)
                return;

            BeginSolve();
            _solver.Step();
            EndSolve();
            CollectChanges();
            Redraw();
        }

        /// <summary>Finishes the current map in this call.</summary>
        public void Complete()
        {
            if (!IsGenerating)
                return;

            BeginSolve();
            while (_solver.Status == SolverStatus.Running)
            {
                _solver.Step();
                CollectChanges();
            }
            EndSolve();
            Redraw();
        }

        public void Clear()
        {
            _solver = null;
            _changed.Clear();
            if (_tilemap != null)
                _tilemap.ClearAllTiles();
        }

        // Forgets the current map but leaves the Tilemap alone, for when Undo has already restored the tiles.
        internal void DiscardMap()
        {
            _solver = null;
            _changed.Clear();
        }

        // Advances the solver by the steps owed for deltaTime, within the frame budget. Called from Update in Play mode
        // and by the editor's update loop in Edit mode; visible to tests.
        internal void Tick(float deltaTime)
        {
            if (!IsGenerating || _paused)
                return;

            int steps;
            if (_stepsPerSecond <= 0f)
            {
                steps = int.MaxValue;
            }
            else
            {
                _stepDebt += _stepsPerSecond * deltaTime;
                steps = (int)_stepDebt;
                _stepDebt -= steps;
            }

            if (steps == 0)
                return;

            long budgetTicks = (long)(_frameBudgetMilliseconds * Stopwatch.Frequency / 1000.0);
            long frameStart = Stopwatch.GetTimestamp();
            BeginSolve();
            for (int i = 0; i < steps && _solver.Status == SolverStatus.Running; i++)
            {
                _solver.Step();
                CollectChanges();

                // Out of time: drop the backlog rather than trying to catch up and stalling the next frames.
                if (Stopwatch.GetTimestamp() - frameStart > budgetTicks)
                {
                    _stepDebt = 0;
                    break;
                }
            }
            EndSolve();
            Redraw();
        }

        bool BeginMap()
        {
            if (_tileSet == null || _tilemap == null)
            {
                Debug.LogError("WfcGenerator needs a Tile Set and a Tilemap.", this);
                return false;
            }

            var compilation = _tileSet.Compile();
            if (compilation.HasErrors)
            {
                foreach (var issue in compilation.Issues)
                    if (issue.Severity == IssueSeverity.Error)
                        Debug.LogError($"{_tileSet.name}: {issue.Message}", _tileSet);
                return false;
            }

            _rules = compilation.Rules;
            var options = new SolverOptions { WrapEdges = _wrapEdges };
            _solver = new WfcSolver(_rules, _size.x, _size.y, unchecked((uint)_seed), options);
            ApplyPins();
            _stepDebt = 0;
            _solveTime.Reset();

            int cellCount = _solver.CellCount;
            if (_isChanged.Length != cellCount)
                _isChanged = new bool[cellCount];
            Array.Clear(_isChanged, 0, cellCount);
            _changed.Clear();

            // Draw every cell once: pruning at construction may already have decided some of them.
            _tilemap.ClearAllTiles();
            for (int cell = 0; cell < cellCount; cell++)
                MarkChanged(cell);

            Started?.Invoke(this);
            Redraw();
            return true;
        }

        /// <summary>
        /// The pins inside the map area: every cell of <see cref="Pins"/> at <c>origin + (x, y)</c> holding a tile from the
        /// tile set, as solver cell and tile id. Cells holding other tiles are counted in <paramref name="unknown"/>.
        /// </summary>
        internal List<(int X, int Y, int Tile)> ReadPins(out int unknown)
        {
            var pins = new List<(int X, int Y, int Tile)>();
            unknown = 0;
            if (_pins == null || _tileSet == null)
                return pins;

            var ids = new Dictionary<TileBase, int>();
            for (int i = 0; i < _tileSet.Tiles.Count; i++)
                if (_tileSet.Tiles[i].Tile != null && !ids.ContainsKey(_tileSet.Tiles[i].Tile))
                    ids.Add(_tileSet.Tiles[i].Tile, i);

            for (int y = 0; y < _size.y; y++)
            {
                for (int x = 0; x < _size.x; x++)
                {
                    var tile = _pins.GetTile(new Vector3Int(_origin.x + x, _origin.y + y, _origin.z));
                    if (tile == null)
                        continue;

                    if (ids.TryGetValue(tile, out int id))
                        pins.Add((x, y, id));
                    else
                        unknown++;
                }
            }
            return pins;
        }

        void ApplyPins()
        {
            var pins = ReadPins(out int unknown);
            if (unknown > 0)
                Debug.LogWarning($"{unknown} pinned cell(s) hold tiles that aren't in '{_tileSet.name}', so they were ignored.", _pins);

            foreach (var (x, y, tile) in pins)
                _solver.Pin(x, y, tile);

            if (pins.Count > 0 && _solver.Status == SolverStatus.Failed)
                Debug.LogWarning("The pinned tiles conflict with the tile set's rules or with each other, so no map can include them all.", _pins);
        }

        void BeginSolve()
        {
            SolveMarker.Begin();
            _solveTime.Start();
        }

        void EndSolve()
        {
            _solveTime.Stop();
            SolveMarker.End();
        }

        void CollectChanges()
        {
            foreach (int cell in _solver.ChangedCells)
                MarkChanged(cell);
        }

        void MarkChanged(int cell)
        {
            if (_isChanged[cell])
                return;

            _isChanged[cell] = true;
            _changed.Add(cell);
        }

        void Redraw()
        {
            if (_changed.Count == 0)
                return;

            using (RedrawMarker.Auto())
            {
                var positions = new Vector3Int[_changed.Count];
                var tiles = new TileBase[_changed.Count];
                for (int i = 0; i < _changed.Count; i++)
                {
                    int cell = _changed[i];
                    positions[i] = CellToTilemap(cell);
                    int tile = _solver.GetTile(cell);
                    tiles[i] = tile >= 0 ? _tileSet.Tiles[tile].Tile : _undecidedTile;
                }
                _tilemap.SetTiles(positions, tiles);
            }

            Redrawn?.Invoke(this);

            foreach (int cell in _changed)
                _isChanged[cell] = false;
            _changed.Clear();
        }

        /// <summary>The Tilemap cell that shows solver cell <paramref name="cell"/>.</summary>
        public Vector3Int CellToTilemap(int cell)
        {
            int width = _solver != null ? _solver.Width : _size.x;
            return new Vector3Int(_origin.x + cell % width, _origin.y + cell / width, _origin.z);
        }
    }
}
