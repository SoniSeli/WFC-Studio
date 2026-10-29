using System;

namespace WfcStudio.Core
{
    /// <summary>
    /// Wave Function Collapse solver for the tiled model on a W×H grid.
    /// Each <see cref="Step"/> collapses the undecided cell with the lowest weighted Shannon entropy,
    /// then propagates the rules outward. The same rules, size and seed always produce the same map.
    /// </summary>
    /// <remarks>
    /// Cells are indexed <c>x + y * Width</c>, with y pointing up. Edges do not wrap.
    /// A contradiction sets <see cref="Status"/> to <see cref="SolverStatus.Failed"/>; recovery is the caller's job.
    /// </remarks>
    public sealed class WfcSolver
    {
        // Small enough never to outweigh a real entropy difference; only breaks ties.
        const double NoiseScale = 1e-6;

        readonly RuleTable _rules;
        readonly Pcg32 _random;
        readonly int _words;

        // Per cell: a bitset of the tiles it can still be, at words [cell * _words, (cell + 1) * _words).
        readonly ulong[] _domains;
        readonly int[] _possibleCount;
        readonly double[] _sumWeights;
        readonly double[] _sumWeightLogWeights;
        readonly double[] _noise;
        int _undecidedCount;

        // Scratch bitset of one tile set, reused by propagation and collapse.
        readonly ulong[] _mask;

        // Cells waiting to propagate. _queued keeps each cell in the stack at most once.
        readonly int[] _stack;
        readonly bool[] _queued;
        int _stackSize;

        // Cells whose domain changed during the last operation.
        readonly int[] _changed;
        readonly bool[] _isChanged;
        int _changedCount;

        bool _stepped;

        public WfcSolver(RuleTable rules, int width, int height, uint seed)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));
            if (width < 1)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be at least 1.");
            if (height < 1)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be at least 1.");

            _rules = rules;
            _random = new Pcg32(seed);
            _words = rules.WordsPerSet;
            Width = width;
            Height = height;

            int cellCount = width * height;
            _domains = new ulong[cellCount * _words];
            _possibleCount = new int[cellCount];
            _sumWeights = new double[cellCount];
            _sumWeightLogWeights = new double[cellCount];
            _noise = new double[cellCount];
            _mask = new ulong[_words];
            _stack = new int[cellCount];
            _queued = new bool[cellCount];
            _changed = new int[cellCount];
            _isChanged = new bool[cellCount];

            double sumWeights = 0;
            double sumWeightLogWeights = 0;
            for (int tile = 0; tile < rules.TileCount; tile++)
            {
                Bitset.Set(_mask, 0, tile);
                sumWeights += rules.GetWeight(tile);
                sumWeightLogWeights += rules.GetWeightLogWeight(tile);
            }

            for (int cell = 0; cell < cellCount; cell++)
            {
                Array.Copy(_mask, 0, _domains, cell * _words, _words);
                _possibleCount[cell] = rules.TileCount;
                _sumWeights[cell] = sumWeights;
                _sumWeightLogWeights[cell] = sumWeightLogWeights;
                _noise[cell] = _random.NextDouble() * NoiseScale;
            }

            _undecidedCount = rules.TileCount > 1 ? cellCount : 0;
            Status = SolverStatus.Running;

            // Remove tiles that can never fit next to their neighbours before the first step.
            for (int cell = 0; cell < cellCount; cell++)
                Push(cell);
            Propagate();
            UpdateCompletion();
        }

        public int Width { get; }

        public int Height { get; }

        public int CellCount => Width * Height;

        public SolverStatus Status { get; private set; }

        /// <summary>Number of cells collapsed by <see cref="Step"/> so far.</summary>
        public int Steps { get; private set; }

        /// <summary>Cells whose possible tiles changed during the last construction, <see cref="Pin"/> or <see cref="Step"/>.</summary>
        public ReadOnlySpan<int> ChangedCells => new ReadOnlySpan<int>(_changed, 0, _changedCount);

        public int CellIndex(int x, int y)
        {
            return x + y * Width;
        }

        /// <summary>
        /// Fixes cell (<paramref name="x"/>, <paramref name="y"/>) to <paramref name="tile"/> and propagates.
        /// Pins must be set before the first <see cref="Step"/>. A pin that conflicts with the rules or another pin fails the solver.
        /// </summary>
        public void Pin(int x, int y, int tile)
        {
            if (_stepped)
                throw new InvalidOperationException("Pins must be set before the first Step.");
            if (x < 0 || x >= Width)
                throw new ArgumentOutOfRangeException(nameof(x), x, $"x must be between 0 and {Width - 1}.");
            if (y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException(nameof(y), y, $"y must be between 0 and {Height - 1}.");
            if (tile < 0 || tile >= _rules.TileCount)
                throw new ArgumentOutOfRangeException(nameof(tile), tile, $"Tile ids must be between 0 and {_rules.TileCount - 1}.");

            ClearChanges();
            if (Status == SolverStatus.Failed)
                return;

            int cell = CellIndex(x, y);
            if (!IsPossible(cell, tile))
            {
                Status = SolverStatus.Failed;
                return;
            }

            Collapse(cell, tile);
            UpdateCompletion();
        }

        public StepResult Step()
        {
            _stepped = true;
            ClearChanges();
            if (Status != SolverStatus.Running)
                return new StepResult(Status, -1, -1);

            int cell = FindLowestEntropyCell();
            int tile = ChooseTile(cell);
            Steps++;
            Collapse(cell, tile);
            UpdateCompletion();
            return new StepResult(Status, cell, tile);
        }

        /// <summary>Steps until the solver completes, fails, or has taken <paramref name="maxSteps"/> steps.</summary>
        public SolverStatus Run(int maxSteps = int.MaxValue)
        {
            for (int i = 0; i < maxSteps && Status == SolverStatus.Running; i++)
                Step();
            return Status;
        }

        public bool IsCollapsed(int cell)
        {
            return _possibleCount[cell] == 1;
        }

        /// <summary>The cell's tile once it is collapsed, otherwise -1.</summary>
        public int GetTile(int cell)
        {
            if (_possibleCount[cell] != 1)
                return -1;

            int offset = cell * _words;
            for (int word = 0; word < _words; word++)
            {
                ulong bits = _domains[offset + word];
                if (bits != 0)
                    return (word << 6) + Bitset.TrailingZeroCount(bits);
            }
            return -1;
        }

        public int GetPossibleCount(int cell)
        {
            return _possibleCount[cell];
        }

        public bool IsPossible(int cell, int tile)
        {
            return Bitset.Contains(_domains, cell * _words, tile);
        }

        /// <summary>Weighted Shannon entropy of the cell's remaining tiles, without tie-break noise. 0 once collapsed.</summary>
        public float GetEntropy(int cell)
        {
            return _possibleCount[cell] > 1 ? (float)Entropy(cell) : 0f;
        }

        // H = log(ΣW) - ΣWlogW / ΣW, kept incrementally per cell.
        double Entropy(int cell)
        {
            double sumWeights = _sumWeights[cell];
            return Math.Log(sumWeights) - _sumWeightLogWeights[cell] / sumWeights;
        }

        int FindLowestEntropyCell()
        {
            int best = -1;
            double bestEntropy = double.MaxValue;
            for (int cell = 0; cell < _possibleCount.Length; cell++)
            {
                if (_possibleCount[cell] <= 1)
                    continue;

                double entropy = Entropy(cell) + _noise[cell];
                if (entropy < bestEntropy)
                {
                    bestEntropy = entropy;
                    best = cell;
                }
            }
            return best;
        }

        // Picks one of the cell's remaining tiles at random, in proportion to its weight.
        int ChooseTile(int cell)
        {
            int offset = cell * _words;
            double total = 0;
            for (int word = 0; word < _words; word++)
            {
                for (ulong bits = _domains[offset + word]; bits != 0; bits &= bits - 1)
                    total += _rules.GetWeight((word << 6) + Bitset.TrailingZeroCount(bits));
            }

            double target = _random.NextDouble() * total;
            int chosen = -1;
            for (int word = 0; word < _words; word++)
            {
                for (ulong bits = _domains[offset + word]; bits != 0; bits &= bits - 1)
                {
                    chosen = (word << 6) + Bitset.TrailingZeroCount(bits);
                    target -= _rules.GetWeight(chosen);
                    if (target < 0)
                        return chosen;
                }
            }

            // Rounding left a sliver of the total unclaimed: fall back to the last tile.
            return chosen;
        }

        void Collapse(int cell, int tile)
        {
            Array.Clear(_mask, 0, _words);
            Bitset.Set(_mask, 0, tile);
            Intersect(cell, _mask);
            Push(cell);
            Propagate();
        }

        void Propagate()
        {
            while (_stackSize > 0 && Status == SolverStatus.Running)
            {
                int cell = _stack[--_stackSize];
                _queued[cell] = false;
                int x = cell % Width;
                int y = cell / Width;

                for (int d = 0; d < DirectionExtensions.Count && Status == SolverStatus.Running; d++)
                {
                    var direction = (Direction)d;
                    int neighbourX = x + direction.OffsetX();
                    int neighbourY = y + direction.OffsetY();
                    if (neighbourX < 0 || neighbourX >= Width || neighbourY < 0 || neighbourY >= Height)
                        continue;

                    int neighbour = CellIndex(neighbourX, neighbourY);
                    CollectAllowedNeighbours(cell, direction);
                    if (Intersect(neighbour, _mask))
                        Push(neighbour);
                }
            }

            // After a contradiction, drop the remaining work so the solver is left in a clean state.
            while (_stackSize > 0)
                _queued[_stack[--_stackSize]] = false;
        }

        // Fills _mask with every tile that some remaining tile of the cell allows in the given direction.
        void CollectAllowedNeighbours(int cell, Direction direction)
        {
            Array.Clear(_mask, 0, _words);
            ulong[] compatible = _rules.Compatible;
            int offset = cell * _words;
            for (int word = 0; word < _words; word++)
            {
                for (ulong bits = _domains[offset + word]; bits != 0; bits &= bits - 1)
                {
                    int tile = (word << 6) + Bitset.TrailingZeroCount(bits);
                    int allowed = _rules.CompatibleOffset(tile, direction);
                    for (int k = 0; k < _words; k++)
                        _mask[k] |= compatible[allowed + k];
                }
            }
        }

        // Removes every tile not in mask from the cell. Returns true if anything was removed.
        bool Intersect(int cell, ulong[] mask)
        {
            int offset = cell * _words;
            int countBefore = _possibleCount[cell];

            for (int word = 0; word < _words; word++)
            {
                ulong current = _domains[offset + word];
                ulong removed = current & ~mask[word];
                if (removed == 0)
                    continue;

                _domains[offset + word] = current & mask[word];
                for (; removed != 0; removed &= removed - 1)
                {
                    int tile = (word << 6) + Bitset.TrailingZeroCount(removed);
                    _possibleCount[cell]--;
                    _sumWeights[cell] -= _rules.GetWeight(tile);
                    _sumWeightLogWeights[cell] -= _rules.GetWeightLogWeight(tile);
                }
            }

            int countAfter = _possibleCount[cell];
            if (countAfter == countBefore)
                return false;

            if (countBefore > 1 && countAfter <= 1)
                _undecidedCount--;
            if (countAfter == 0)
                Status = SolverStatus.Failed;

            MarkChanged(cell);
            return true;
        }

        void UpdateCompletion()
        {
            if (Status == SolverStatus.Running && _undecidedCount == 0)
                Status = SolverStatus.Completed;
        }

        void Push(int cell)
        {
            if (_queued[cell])
                return;

            _queued[cell] = true;
            _stack[_stackSize++] = cell;
        }

        void MarkChanged(int cell)
        {
            if (_isChanged[cell])
                return;

            _isChanged[cell] = true;
            _changed[_changedCount++] = cell;
        }

        void ClearChanges()
        {
            for (int i = 0; i < _changedCount; i++)
                _isChanged[_changed[i]] = false;
            _changedCount = 0;
        }
    }
}
