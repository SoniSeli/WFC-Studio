using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using WfcStudio.Core;

namespace WfcStudio
{
    /// <summary>
    /// A designer-authored tile set: the socket labels it uses, its tiles with a socket on each edge, and exceptions.
    /// <see cref="Compile"/> validates it and produces the <see cref="RuleTable"/> the solver runs on.
    /// Tile ids in the compiled rules are indices into <see cref="Tiles"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "WFC Studio/Tile Set", fileName = "New Tile Set", order = 0)]
    public sealed class TileSet : ScriptableObject
    {
        static readonly Direction[] AllDirections = { Direction.Up, Direction.Right, Direction.Down, Direction.Left };

        [SerializeField] List<SocketDefinition> _sockets = new List<SocketDefinition>();
        [SerializeField] List<TileEntry> _tiles = new List<TileEntry>();
        [SerializeField] List<AdjacencyException> _exceptions = new List<AdjacencyException>();

        public IReadOnlyList<SocketDefinition> Sockets => _sockets;

        public IReadOnlyList<TileEntry> Tiles => _tiles;

        public IReadOnlyList<AdjacencyException> Exceptions => _exceptions;

        /// <summary>Replaces the whole contents, for tools and tests. Designers edit the asset in the Inspector.</summary>
        public void SetContents(IEnumerable<SocketDefinition> sockets, IEnumerable<TileEntry> tiles, IEnumerable<AdjacencyException> exceptions = null)
        {
            _sockets = new List<SocketDefinition>(sockets);
            _tiles = new List<TileEntry>(tiles);
            _exceptions = exceptions != null ? new List<AdjacencyException>(exceptions) : new List<AdjacencyException>();
        }

        /// <summary>
        /// Validates the set and compiles it. Problems with its structure (unknown socket names, missing or duplicate tiles)
        /// are reported first; the rules themselves are analysed once the structure is sound.
        /// </summary>
        public TileSetCompilation Compile()
        {
            var issues = new List<ValidationIssue>();
            var socketIds = CheckSockets(issues);
            var tileIds = CheckTiles(issues);
            CheckEdgeLabels(socketIds, issues);
            CheckExceptions(tileIds, issues);
            if (HasErrors(issues))
                return new TileSetCompilation(null, issues);

            var tileNames = new string[_tiles.Count];
            for (int i = 0; i < tileNames.Length; i++)
                tileNames[i] = _tiles[i].DisplayName;
            var socketNames = new string[_sockets.Count];
            for (int i = 0; i < socketNames.Length; i++)
                socketNames[i] = _sockets[i].Name;

            var set = new SocketTileSet(tileNames, socketNames);
            for (int i = 0; i < _sockets.Count; i++)
            {
                set.SetConnected(i, i, _sockets[i].ConnectsToSelf);
                foreach (string other in _sockets[i].ConnectsTo)
                    if (!string.IsNullOrEmpty(other))
                        set.SetConnected(i, socketIds[other], true);
            }

            for (int tile = 0; tile < _tiles.Count; tile++)
            {
                set.SetWeight(tile, _tiles[tile].Weight);
                foreach (var direction in AllDirections)
                {
                    string label = _tiles[tile].GetSocket(direction);
                    set.SetSocket(tile, direction, string.IsNullOrEmpty(label) ? SocketTileSet.NoSocket : socketIds[label]);
                }
            }

            foreach (var exception in _exceptions)
            {
                int tile = tileIds[exception.Tile];
                int neighbour = tileIds[exception.Neighbour];
                bool allow = exception.Kind == ExceptionKind.Allow;
                if (exception.Direction == ExceptionDirection.Any)
                {
                    foreach (var direction in AllDirections)
                        set.AddException(tile, direction, neighbour, allow);
                }
                else
                {
                    set.AddException(tile, ToDirection(exception.Direction), neighbour, allow);
                }
            }

            var compiled = set.Compile();
            issues.AddRange(compiled.Issues);
            return new TileSetCompilation(compiled.Rules, issues);
        }

        Dictionary<string, int> CheckSockets(List<ValidationIssue> issues)
        {
            var ids = new Dictionary<string, int>();
            for (int i = 0; i < _sockets.Count; i++)
            {
                string name = _sockets[i].Name;
                if (string.IsNullOrEmpty(name))
                    issues.Add(new ValidationIssue(IssueSeverity.Error, IssueCode.UnnamedSocket, $"Socket {i + 1} has no name."));
                else if (ids.ContainsKey(name))
                    issues.Add(new ValidationIssue(IssueSeverity.Error, IssueCode.DuplicateSocket, $"Socket '{name}' is defined more than once."));
                else
                    ids.Add(name, i);
            }

            foreach (var socket in _sockets)
            {
                if (string.IsNullOrEmpty(socket.Name))
                    continue;

                foreach (string other in socket.ConnectsTo)
                    if (!string.IsNullOrEmpty(other) && !ids.ContainsKey(other))
                        issues.Add(new ValidationIssue(IssueSeverity.Error, IssueCode.UnknownSocket,
                            $"Socket '{socket.Name}' connects to '{other}', which is not defined in this tile set."));
            }

            return ids;
        }

        Dictionary<TileBase, int> CheckTiles(List<ValidationIssue> issues)
        {
            var ids = new Dictionary<TileBase, int>();
            if (_tiles.Count == 0)
                issues.Add(new ValidationIssue(IssueSeverity.Error, IssueCode.NoTiles, "The tile set has no tiles."));

            for (int i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i].Tile;
                if (tile == null)
                    issues.Add(new ValidationIssue(IssueSeverity.Error, IssueCode.MissingTile, $"'{_tiles[i].DisplayName}' (entry {i + 1}) has no tile asset.", i));
                else if (ids.ContainsKey(tile))
                    issues.Add(new ValidationIssue(IssueSeverity.Error, IssueCode.DuplicateTile, $"Tile asset '{tile.name}' appears more than once.", i));
                else
                    ids.Add(tile, i);
            }
            return ids;
        }

        void CheckEdgeLabels(Dictionary<string, int> socketIds, List<ValidationIssue> issues)
        {
            for (int i = 0; i < _tiles.Count; i++)
            {
                foreach (var direction in AllDirections)
                {
                    string label = _tiles[i].GetSocket(direction);
                    if (!string.IsNullOrEmpty(label) && !socketIds.ContainsKey(label))
                        issues.Add(new ValidationIssue(IssueSeverity.Error, IssueCode.UnknownSocket,
                            $"'{_tiles[i].DisplayName}' uses socket '{label}' on its {direction.ToString().ToLowerInvariant()} edge, but this tile set doesn't define it.", i));
                }
            }
        }

        void CheckExceptions(Dictionary<TileBase, int> tileIds, List<ValidationIssue> issues)
        {
            for (int i = 0; i < _exceptions.Count; i++)
            {
                var exception = _exceptions[i];
                if (exception.Tile == null || exception.Neighbour == null)
                    issues.Add(new ValidationIssue(IssueSeverity.Error, IssueCode.UnknownExceptionTile, $"Exception {i + 1} is missing a tile."));
                else if (!tileIds.ContainsKey(exception.Tile) || !tileIds.ContainsKey(exception.Neighbour))
                    issues.Add(new ValidationIssue(IssueSeverity.Error, IssueCode.UnknownExceptionTile,
                        $"Exception {i + 1} refers to a tile that isn't in this tile set."));
            }
        }

        static bool HasErrors(List<ValidationIssue> issues)
        {
            foreach (var issue in issues)
                if (issue.Severity == IssueSeverity.Error)
                    return true;
            return false;
        }

        static Direction ToDirection(ExceptionDirection direction)
        {
            switch (direction)
            {
                case ExceptionDirection.Up: return Direction.Up;
                case ExceptionDirection.Right: return Direction.Right;
                case ExceptionDirection.Down: return Direction.Down;
                default: return Direction.Left;
            }
        }
    }
}
