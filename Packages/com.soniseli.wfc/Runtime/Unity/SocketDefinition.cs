using System;
using System.Collections.Generic;
using UnityEngine;

namespace WfcStudio
{
    /// <summary>
    /// An edge label in a <see cref="TileSet"/>. Two facing edges may touch when their sockets connect.
    /// A socket connects to itself unless that is turned off, plus to any sockets listed in <see cref="ConnectsTo"/>.
    /// Connections always work both ways, so listing a connection on either socket is enough.
    /// </summary>
    [Serializable]
    public sealed class SocketDefinition
    {
        [SerializeField] string _name;

        [Tooltip("Whether edges with this socket may face each other. Turn off for borders that must not touch, such as the outside of two lakes.")]
        [SerializeField] bool _connectsToSelf = true;

        [Tooltip("Other sockets this one may face, for example a feature border that may face grass.")]
        [SerializeField] List<string> _connectsTo = new List<string>();

        public SocketDefinition()
        {
        }

        public SocketDefinition(string name, bool connectsToSelf = true, params string[] connectsTo)
        {
            _name = name;
            _connectsToSelf = connectsToSelf;
            _connectsTo = new List<string>(connectsTo ?? Array.Empty<string>());
        }

        public string Name => _name;

        public bool ConnectsToSelf => _connectsToSelf;

        public IReadOnlyList<string> ConnectsTo => _connectsTo;
    }
}
