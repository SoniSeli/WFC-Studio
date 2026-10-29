using System;

namespace WfcStudio.Core
{
    /// <summary>
    /// Immutable adjacency rules and weights for a tile set, in the form the solver consumes.
    /// Tiles are integer ids from 0 to <see cref="TileCount"/> - 1.
    /// Rules are symmetric by construction: if A allows B to its right, B allows A to its left.
    /// </summary>
    public sealed class RuleTable
    {
        // For each (tile, direction), a bitset of the tiles allowed next to it in that direction.
        readonly ulong[] _compatible;
        readonly double[] _weights;
        readonly double[] _weightLogWeights;

        RuleTable(int tileCount, ulong[] compatible, double[] weights)
        {
            TileCount = tileCount;
            WordsPerSet = Bitset.WordsFor(tileCount);
            _compatible = compatible;
            _weights = weights;
            _weightLogWeights = new double[tileCount];
            for (int tile = 0; tile < tileCount; tile++)
                _weightLogWeights[tile] = weights[tile] * Math.Log(weights[tile]);
        }

        public int TileCount { get; }

        /// <summary>Number of ulong words in one tile bitset.</summary>
        internal int WordsPerSet { get; }

        internal ulong[] Compatible => _compatible;

        public double GetWeight(int tile)
        {
            return _weights[tile];
        }

        internal double GetWeightLogWeight(int tile)
        {
            return _weightLogWeights[tile];
        }

        /// <summary>True if <paramref name="neighbour"/> may sit in <paramref name="direction"/> from <paramref name="tile"/>.</summary>
        public bool Allows(int tile, Direction direction, int neighbour)
        {
            return Bitset.Contains(_compatible, CompatibleOffset(tile, direction), neighbour);
        }

        internal int CompatibleOffset(int tile, Direction direction)
        {
            return (tile * DirectionExtensions.Count + (int)direction) * WordsPerSet;
        }

        public sealed class Builder
        {
            readonly int _tileCount;
            readonly int _wordsPerSet;
            readonly ulong[] _compatible;
            readonly double[] _weights;

            public Builder(int tileCount)
            {
                if (tileCount < 1)
                    throw new ArgumentOutOfRangeException(nameof(tileCount), "A tile set needs at least one tile.");

                _tileCount = tileCount;
                _wordsPerSet = Bitset.WordsFor(tileCount);
                _compatible = new ulong[tileCount * DirectionExtensions.Count * _wordsPerSet];
                _weights = new double[tileCount];
                for (int tile = 0; tile < tileCount; tile++)
                    _weights[tile] = 1.0;
            }

            public Builder SetWeight(int tile, double weight)
            {
                CheckTile(tile, nameof(tile));
                if (!(weight > 0) || double.IsInfinity(weight))
                    throw new ArgumentOutOfRangeException(nameof(weight), weight, "Weights must be positive and finite.");

                _weights[tile] = weight;
                return this;
            }

            /// <summary>Allows <paramref name="neighbour"/> in <paramref name="direction"/> from <paramref name="tile"/>, and the reverse.</summary>
            public Builder Allow(int tile, Direction direction, int neighbour)
            {
                CheckTile(tile, nameof(tile));
                CheckTile(neighbour, nameof(neighbour));

                Bitset.Set(_compatible, Offset(tile, direction), neighbour);
                Bitset.Set(_compatible, Offset(neighbour, direction.Opposite()), tile);
                return this;
            }

            /// <summary>Removes the rule allowing <paramref name="neighbour"/> in <paramref name="direction"/> from <paramref name="tile"/>, and the reverse.</summary>
            public Builder Disallow(int tile, Direction direction, int neighbour)
            {
                CheckTile(tile, nameof(tile));
                CheckTile(neighbour, nameof(neighbour));

                Bitset.Clear(_compatible, Offset(tile, direction), neighbour);
                Bitset.Clear(_compatible, Offset(neighbour, direction.Opposite()), tile);
                return this;
            }

            public RuleTable Build()
            {
                return new RuleTable(_tileCount, (ulong[])_compatible.Clone(), (double[])_weights.Clone());
            }

            int Offset(int tile, Direction direction)
            {
                return (tile * DirectionExtensions.Count + (int)direction) * _wordsPerSet;
            }

            void CheckTile(int tile, string parameterName)
            {
                if (tile < 0 || tile >= _tileCount)
                    throw new ArgumentOutOfRangeException(parameterName, tile, $"Tile ids must be between 0 and {_tileCount - 1}.");
            }
        }
    }
}
