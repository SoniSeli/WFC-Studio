using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace WfcStudio
{
    /// <summary>
    /// Makes tiles "pop" into place as a <see cref="WfcGenerator"/> decides them, by animating each Tilemap cell's
    /// transform. No extra GameObjects are created.
    /// </summary>
    [AddComponentMenu("WFC Studio/Tile Pop Animation")]
    public sealed class TilePopAnimation : MonoBehaviour
    {
        struct Pop
        {
            public Vector3Int Cell;
            public float StartTime;
        }

        [SerializeField] WfcGenerator _generator;

        [Min(0.01f)] [SerializeField] float _duration = 0.25f;

        [Tooltip("Scale over the animation. Horizontal axis is normalised time.")]
        [SerializeField] AnimationCurve _scale = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.7f, 1.15f), new Keyframe(1f, 1f));

        WfcGenerator _bound;
        readonly List<Pop> _pops = new List<Pop>();

        public WfcGenerator Generator
        {
            get => _generator;
            set => _generator = value;
        }

        void OnEnable()
        {
            Bind(_generator);
        }

        void OnDisable()
        {
            Bind(null);
            FinishAll();
        }

        void Bind(WfcGenerator generator)
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
            _pops.Clear();
        }

        void OnRedrawn(WfcGenerator generator)
        {
            var tilemap = generator.Tilemap;
            var solver = generator.Solver;
            float now = Time.time;
            foreach (int cell in generator.ChangedCells)
            {
                if (!solver.IsCollapsed(cell))
                    continue;

                var position = generator.CellToTilemap(cell);
                tilemap.SetTileFlags(position, TileFlags.None);
                tilemap.SetTransformMatrix(position, Matrix4x4.Scale(Vector3.zero));
                _pops.Add(new Pop { Cell = position, StartTime = now });
            }
        }

        void LateUpdate()
        {
            if (_bound == null || _pops.Count == 0)
                return;

            var tilemap = _bound.Tilemap;
            float now = Time.time;
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var pop = _pops[i];
                float t = (now - pop.StartTime) / _duration;
                if (t >= 1f)
                {
                    tilemap.SetTransformMatrix(pop.Cell, Matrix4x4.identity);
                    _pops[i] = _pops[_pops.Count - 1];
                    _pops.RemoveAt(_pops.Count - 1);
                    continue;
                }

                float scale = _scale.Evaluate(t);
                tilemap.SetTransformMatrix(pop.Cell, Matrix4x4.Scale(new Vector3(scale, scale, 1f)));
            }
        }

        void FinishAll()
        {
            if (_generator != null && _generator.Tilemap != null)
                foreach (var pop in _pops)
                    _generator.Tilemap.SetTransformMatrix(pop.Cell, Matrix4x4.identity);
            _pops.Clear();
        }
    }
}
