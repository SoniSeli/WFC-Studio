using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using WfcStudio.Core;

namespace WfcStudio
{
    /// <summary>One tile in a <see cref="TileSet"/>: what to draw, how often to pick it, and the socket on each edge.</summary>
    [Serializable]
    public sealed class TileEntry
    {
        [Tooltip("Shown in validation messages. Defaults to the tile asset's name.")]
        [SerializeField] string _name;

        [SerializeField] TileBase _tile;

        [Tooltip("Relative chance of picking this tile. Must be greater than zero.")]
        [SerializeField] float _weight = 1f;

        [Header("Sockets")]
        [SerializeField] string _up;
        [SerializeField] string _right;
        [SerializeField] string _down;
        [SerializeField] string _left;

        public TileEntry()
        {
        }

        public TileEntry(string name, TileBase tile, float weight, string up, string right, string down, string left)
        {
            _name = name;
            _tile = tile;
            _weight = weight;
            _up = up;
            _right = right;
            _down = down;
            _left = left;
        }

        public TileBase Tile => _tile;

        public float Weight => _weight;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(_name))
                    return _name;
                return _tile != null ? _tile.name : "(unnamed tile)";
            }
        }

        public string GetSocket(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return _up;
                case Direction.Right: return _right;
                case Direction.Down: return _down;
                default: return _left;
            }
        }
    }
}
