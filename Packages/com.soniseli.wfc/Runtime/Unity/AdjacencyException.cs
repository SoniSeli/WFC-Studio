using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace WfcStudio
{
    public enum ExceptionKind
    {
        /// <summary>Forbid the pair even though their sockets match.</summary>
        Forbid,

        /// <summary>Allow the pair even though their sockets don't match.</summary>
        Allow,
    }

    public enum ExceptionDirection
    {
        /// <summary>All four directions.</summary>
        Any,
        Up,
        Right,
        Down,
        Left,
    }

    /// <summary>Overrides the sockets for one pair of tiles, for example "no Tree next to Tree".</summary>
    [Serializable]
    public sealed class AdjacencyException
    {
        [SerializeField] ExceptionKind _kind;
        [SerializeField] TileBase _tile;

        [Tooltip("Where the neighbour sits, seen from the tile.")]
        [SerializeField] ExceptionDirection _direction;

        [SerializeField] TileBase _neighbour;

        public AdjacencyException()
        {
        }

        public AdjacencyException(ExceptionKind kind, TileBase tile, ExceptionDirection direction, TileBase neighbour)
        {
            _kind = kind;
            _tile = tile;
            _direction = direction;
            _neighbour = neighbour;
        }

        public ExceptionKind Kind => _kind;

        public TileBase Tile => _tile;

        public ExceptionDirection Direction => _direction;

        public TileBase Neighbour => _neighbour;
    }
}
