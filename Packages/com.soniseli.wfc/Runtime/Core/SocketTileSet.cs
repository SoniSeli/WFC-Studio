using System;
using System.Collections.Generic;

namespace WfcStudio.Core
{
    /// <summary>
    /// A tile set described by edge sockets: every tile edge carries a socket, and two facing edges may touch
    /// when their sockets match. A socket matches itself unless it is paired with another socket, in which case
    /// it matches only that one (for edges that fit one way, like the top and bottom of a cliff).
    /// Exceptions then allow or forbid individual pairs. <see cref="Compile"/> turns it into a <see cref="RuleTable"/>
    /// and reports anything a designer should look at.
    /// </summary>
    public sealed class SocketTileSet
    {
        public const int NoSocket = -1;

        struct AdjacencyException
        {
            public int Tile;
            public Direction Direction;
            public int Neighbour;
            public bool Allow;
        }

        readonly string[] _tileNames;
        readonly string[] _socketNames;
        readonly int[] _sockets;
        readonly int[] _mates;
        readonly double[] _weights;
        readonly List<AdjacencyException> _exceptions = new List<AdjacencyException>();

        public SocketTileSet(IReadOnlyList<string> tileNames, IReadOnlyList<string> socketNames)
        {
            if (tileNames == null)
                throw new ArgumentNullException(nameof(tileNames));
            if (socketNames == null)
                throw new ArgumentNullException(nameof(socketNames));

            _tileNames = new string[tileNames.Count];
            for (int i = 0; i < _tileNames.Length; i++)
                _tileNames[i] = tileNames[i];

            _socketNames = new string[socketNames.Count];
            _mates = new int[socketNames.Count];
            for (int i = 0; i < _socketNames.Length; i++)
            {
                _socketNames[i] = socketNames[i];
                _mates[i] = i;
            }

            _sockets = new int[_tileNames.Length * DirectionExtensions.Count];
            for (int i = 0; i < _sockets.Length; i++)
                _sockets[i] = NoSocket;

            _weights = new double[_tileNames.Length];
            for (int i = 0; i < _weights.Length; i++)
                _weights[i] = 1.0;
        }

        public int TileCount => _tileNames.Length;

        public int SocketCount => _socketNames.Length;

        public string GetTileName(int tile)
        {
            return _tileNames[tile];
        }

        public string GetSocketName(int socket)
        {
            return _socketNames[socket];
        }

        public int GetSocket(int tile, Direction direction)
        {
            return _sockets[tile * DirectionExtensions.Count + (int)direction];
        }

        /// <summary>Sets the socket on one edge. <see cref="NoSocket"/> clears it, which <see cref="Compile"/> reports as an error.</summary>
        public void SetSocket(int tile, Direction direction, int socket)
        {
            CheckTile(tile);
            if (socket != NoSocket)
                CheckSocket(socket, nameof(socket));

            _sockets[tile * DirectionExtensions.Count + (int)direction] = socket;
        }

        public void SetSockets(int tile, int up, int right, int down, int left)
        {
            SetSocket(tile, Direction.Up, up);
            SetSocket(tile, Direction.Right, right);
            SetSocket(tile, Direction.Down, down);
            SetSocket(tile, Direction.Left, left);
        }

        /// <summary>Sets a tile's weight. Invalid weights are reported by <see cref="Compile"/> rather than thrown.</summary>
        public void SetWeight(int tile, double weight)
        {
            CheckTile(tile);
            _weights[tile] = weight;
        }

        /// <summary>Makes <paramref name="socket"/> and <paramref name="mate"/> match each other instead of themselves. Pairing a socket with itself undoes a pairing.</summary>
        public void Pair(int socket, int mate)
        {
            CheckSocket(socket, nameof(socket));
            CheckSocket(mate, nameof(mate));

            Unpair(socket);
            Unpair(mate);
            _mates[socket] = mate;
            _mates[mate] = socket;
        }

        public bool Matches(int socket, int other)
        {
            return _mates[socket] == other;
        }

        /// <summary>
        /// Allows or forbids <paramref name="neighbour"/> in <paramref name="direction"/> from <paramref name="tile"/>,
        /// whatever their sockets say. Applies in both directions, like every rule.
        /// </summary>
        public void AddException(int tile, Direction direction, int neighbour, bool allow)
        {
            CheckTile(tile);
            CheckTile(neighbour);
            _exceptions.Add(new AdjacencyException { Tile = tile, Direction = direction, Neighbour = neighbour, Allow = allow });
        }

        public TileSetCompilation Compile()
        {
            var issues = new List<ValidationIssue>();
            if (TileCount == 0)
            {
                issues.Add(new ValidationIssue(IssueSeverity.Error, IssueCode.NoTiles, "The tile set has no tiles."));
                return new TileSetCompilation(null, issues);
            }

            CheckDefinitions(issues);
            CheckExceptions(issues, out bool conflicting);
            if (conflicting || HasErrors(issues))
                return new TileSetCompilation(null, issues);

            var rules = BuildRules();
            AnalyseRules(rules, issues);
            return new TileSetCompilation(rules, issues);
        }

        void CheckDefinitions(List<ValidationIssue> issues)
        {
            var used = new bool[SocketCount];
            for (int tile = 0; tile < TileCount; tile++)
            {
                double weight = _weights[tile];
                if (!(weight > 0) || double.IsInfinity(weight))
                    issues.Add(new ValidationIssue(IssueSeverity.Error, IssueCode.InvalidWeight,
                        $"'{_tileNames[tile]}' has weight {weight}. Weights must be greater than zero.", tile));

                for (int d = 0; d < DirectionExtensions.Count; d++)
                {
                    int socket = GetSocket(tile, (Direction)d);
                    if (socket == NoSocket)
                        issues.Add(new ValidationIssue(IssueSeverity.Error, IssueCode.MissingSocket,
                            $"'{_tileNames[tile]}' has no socket on its {EdgeName((Direction)d)} edge.", tile));
                    else
                        used[socket] = true;
                }
            }

            for (int socket = 0; socket < SocketCount; socket++)
                if (!used[socket])
                    issues.Add(new ValidationIssue(IssueSeverity.Info, IssueCode.UnusedSocket,
                        $"Socket '{_socketNames[socket]}' is not used on any edge."));
        }

        void CheckExceptions(List<ValidationIssue> issues, out bool conflicting)
        {
            conflicting = false;
            var seen = new Dictionary<long, bool>();
            foreach (var exception in _exceptions)
            {
                Normalise(exception, out int tile, out Direction direction, out int neighbour);
                string pair = DescribePair(tile, direction, neighbour);
                long key = ((long)tile * DirectionExtensions.Count + (int)direction) * TileCount + neighbour;

                if (seen.TryGetValue(key, out bool allowedBefore))
                {
                    if (allowedBefore != exception.Allow)
                    {
                        issues.Add(new ValidationIssue(IssueSeverity.Error, IssueCode.ConflictingExceptions,
                            $"Exceptions both allow and forbid {pair}.", tile));
                        conflicting = true;
                    }
                    continue;
                }
                seen.Add(key, exception.Allow);

                bool socketsMatch = SocketsMatch(tile, direction, neighbour);
                if (socketsMatch == exception.Allow)
                    issues.Add(new ValidationIssue(IssueSeverity.Info, IssueCode.RedundantException,
                        $"The exception that {(exception.Allow ? "allows" : "forbids")} {pair} has no effect: their sockets already {(socketsMatch ? "match" : "don't match")}.", tile));
            }
        }

        RuleTable BuildRules()
        {
            var builder = new RuleTable.Builder(TileCount);
            for (int tile = 0; tile < TileCount; tile++)
            {
                builder.SetWeight(tile, _weights[tile]);
                for (int d = 0; d < DirectionExtensions.Count; d++)
                    for (int neighbour = 0; neighbour < TileCount; neighbour++)
                        if (SocketsMatch(tile, (Direction)d, neighbour))
                            builder.Allow(tile, (Direction)d, neighbour);
            }

            foreach (var exception in _exceptions)
            {
                if (exception.Allow)
                    builder.Allow(exception.Tile, exception.Direction, exception.Neighbour);
                else
                    builder.Disallow(exception.Tile, exception.Direction, exception.Neighbour);
            }

            return builder.Build();
        }

        void AnalyseRules(RuleTable rules, List<ValidationIssue> issues)
        {
            // A tile with nothing allowed on one side can only sit on that edge of the map.
            var edgeOnly = new bool[TileCount];
            for (int tile = 0; tile < TileCount; tile++)
            {
                for (int d = 0; d < DirectionExtensions.Count; d++)
                {
                    var direction = (Direction)d;
                    if (HasNeighbour(rules, tile, direction, null))
                        continue;

                    edgeOnly[tile] = true;
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, IssueCode.NoNeighbour,
                        $"Nothing may sit {Preposition(direction)} '{_tileNames[tile]}', so it can only appear on the {EdgeName(direction)} edge of the map, and never on a wrapped map.", tile));
                }
            }

            // Repeatedly remove tiles that lack a neighbour among the remaining tiles. What is left can fill the
            // inside of an arbitrarily large map; anything removed can only appear near an edge.
            var inside = new bool[TileCount];
            for (int tile = 0; tile < TileCount; tile++)
                inside[tile] = true;

            bool removed = true;
            while (removed)
            {
                removed = false;
                for (int tile = 0; tile < TileCount; tile++)
                {
                    if (!inside[tile])
                        continue;

                    for (int d = 0; d < DirectionExtensions.Count; d++)
                    {
                        if (HasNeighbour(rules, tile, (Direction)d, inside))
                            continue;

                        inside[tile] = false;
                        removed = true;
                        break;
                    }
                }
            }

            for (int tile = 0; tile < TileCount; tile++)
                if (!inside[tile] && !edgeOnly[tile])
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, IssueCode.CannotAppearInside,
                        $"'{_tileNames[tile]}' can't appear away from the map edges: on some side, every tile allowed next to it can itself only appear near an edge.", tile));
        }

        bool HasNeighbour(RuleTable rules, int tile, Direction direction, bool[] among)
        {
            for (int neighbour = 0; neighbour < TileCount; neighbour++)
                if ((among == null || among[neighbour]) && rules.Allows(tile, direction, neighbour))
                    return true;
            return false;
        }

        bool SocketsMatch(int tile, Direction direction, int neighbour)
        {
            int socket = GetSocket(tile, direction);
            int facing = GetSocket(neighbour, direction.Opposite());
            return socket != NoSocket && facing != NoSocket && Matches(socket, facing);
        }

        // Writes each pair one way (Up or Right) so "A right of B" and "B left of A" are recognised as the same rule.
        static void Normalise(AdjacencyException exception, out int tile, out Direction direction, out int neighbour)
        {
            if (exception.Direction == Direction.Down || exception.Direction == Direction.Left)
            {
                tile = exception.Neighbour;
                direction = exception.Direction.Opposite();
                neighbour = exception.Tile;
            }
            else
            {
                tile = exception.Tile;
                direction = exception.Direction;
                neighbour = exception.Neighbour;
            }
        }

        string DescribePair(int tile, Direction direction, int neighbour)
        {
            return $"'{_tileNames[neighbour]}' {Preposition(direction)} '{_tileNames[tile]}'";
        }

        void Unpair(int socket)
        {
            int mate = _mates[socket];
            _mates[mate] = mate;
            _mates[socket] = socket;
        }

        static bool HasErrors(List<ValidationIssue> issues)
        {
            foreach (var issue in issues)
                if (issue.Severity == IssueSeverity.Error)
                    return true;
            return false;
        }

        static string EdgeName(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return "top";
                case Direction.Right: return "right";
                case Direction.Down: return "bottom";
                default: return "left";
            }
        }

        static string Preposition(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return "above";
                case Direction.Right: return "to the right of";
                case Direction.Down: return "below";
                default: return "to the left of";
            }
        }

        void CheckTile(int tile)
        {
            if (tile < 0 || tile >= TileCount)
                throw new ArgumentOutOfRangeException(nameof(tile), tile, $"Tile ids must be between 0 and {TileCount - 1}.");
        }

        void CheckSocket(int socket, string parameterName)
        {
            if (socket < 0 || socket >= SocketCount)
                throw new ArgumentOutOfRangeException(parameterName, socket, $"Socket ids must be between 0 and {SocketCount - 1}.");
        }
    }
}
