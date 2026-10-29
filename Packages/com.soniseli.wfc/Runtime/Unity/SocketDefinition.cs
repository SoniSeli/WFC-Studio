using System;
using UnityEngine;

namespace WfcStudio
{
    /// <summary>An edge label in a <see cref="TileSet"/>. Edges with matching sockets may touch.</summary>
    [Serializable]
    public sealed class SocketDefinition
    {
        [SerializeField] string _name;

        [Tooltip("Optional. When set, this socket matches only the named socket, and that socket only this one, instead of each matching itself. Use it for edges that fit one way, like the top and bottom of a cliff.")]
        [SerializeField] string _mate;

        public SocketDefinition()
        {
        }

        public SocketDefinition(string name, string mate = null)
        {
            _name = name;
            _mate = mate;
        }

        public string Name => _name;

        public string Mate => _mate;
    }
}
