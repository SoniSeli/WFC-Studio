using System;

namespace WfcStudio.Core
{
    /// <summary>
    /// Wave Function Collapse solver for the tiled model on a W×H grid.
    /// Each <see cref="Step"/> collapses the undecided cell with the lowest weighted Shannon entropy,
    /// then propagates the rules outward. The same rules, size, seed and options always produce the same map.
    /// </summary>
    /// <remarks>
    /// Cells are indexed <c>x + y * Width</c>, with y pointing up.
    /// On a contradiction the solver backtracks: it undoes the newest collapse, bans that tile in that cell and propagates again.
    /// When the backtrack budget runs out it restarts from the state after construction and pins.
    /// </remarks>
    public sealed class WfcSolver
    {
        // Small enough never to outweigh a real entropy difference; only breaks ties.
        const double NoiseScale = 1e-6;

        // One removal of tiles from one word of a cell's domain. The bits are enough to undo it.
        struct TrailEntry
        {
            public int Cell;
            public int Word;
            public ulong Bits;
        }

        // One collapse made by Step, and where the trail stood before it.
        struct Frame
        {
            public int TrailStart;
            public int Cell;
            public int Tile;
        }

        readonly RuleTable _rules;
        readonly uint _seed;
        readonly bool _wrapEdges;
        readonly int _maxBacktracks;
        readonly int _maxRestarts;
        readonly int _words;
        Pcg32 _random;

        // Per cell: a bitset of the tiles it can still be, at words [cell * _words, (cell + 1) * _words).
        readonly ulong[] _domains;
        readonly int[] _possibleCount;
        readonly double[] _sumWeights;
        readonly double[] _sumWeightLogWeights;
        readonly double[] _noise;
        int _undecidedCount;

        // Bitset of every tile, and a scratch bitset reused by propagation, collapse and bans.
        readonly ulong[] _allTiles;
        readonly ulong[] _mask;

        // Cells waiting to propagate. _queued keeps each cell in the stack at most once.
        readonly int[] _stack;
        readonly bool[] _queued;
        int _stackSize;

        // Removals since the first Step, newest last, and the collapses they belong to.
        // Removals before the first Step (pruning and pins) are the base state and are never undone.
        TrailEntry[] _trail;
        int _trailLength;
        readonly Frame[] _frames;
        int _frameCount;
        int _attemptBacktracks;

        // Cells whose domain changed during the last operation.
        readonly int[] _changed;
        readonly bool[] _isChanged;
        int _changedCount;

        bool _stepped;
        bool _contradiction;

        public WfcSolver(RuleTable rules, int width, int height, uint seed, SolverOptions options = null)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));
            if (width < 1)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be at least 1.");
            if (height < 1)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be at least 1.");

            options ??= new SolverOptions();
            if (options.MaxBacktracks < 0)
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxBacktracks, "MaxBacktracks cannot be negative.");
            if (options.MaxRestarts < 0)
                throw new ArgumentOutOfRangeException(nameof(options), options.MaxRestarts, "MaxRestarts cannot be negative.");

            _rules = rules;
            _seed = seed;
            _wrapEdges = options.WrapEdges;
            _maxBacktracks = options.MaxBacktracks;
            _maxRestarts = options.MaxRestarts;
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
            _allTiles = new ulong[_words];
            _mask = new ulong[_words];
            _stack = new int[cellCount];
            _queued = new bool[cellCount];
            _trail = new TrailEntry[Math.Max(64, cellCount * _words)];
            _frames = new Frame[cellCount];
            _changed = new int[cellCount];
            _isChanged = new bool[cellCount];

            double sumWeights = 0;
            double sumWeightLogWeights = 0;
            for (int tile = 0; tile < rules.TileCount; tile++)
            {
                Bitset.Set(_allTiles, 0, tile);
                sumWeights += rules.GetWeight(tile);
                sumWeightLogWeights += rules.GetWeightLogWeight(tile);
            }

            for (int cell = 0; cell < cellCount; cell++)
            {
                Array.Copy(_allTiles, 0, _domains, cell * _words, _words);
                _possibleCount[cell] = rules.TileCount;
                _sumWeights[cell] = sumWeights;
                _sumWeightLogWeights[cell] = sumWeightLogWeights;
            }
            DrawNoise();

            _undecidedCount = rules.TileCount > 1 ? cellCount : 0;
            Status = SolverStatus.Running;

            // Remove tiles that can never fit next to their neighbours before the first step.
            for (int cell = 0; cell < cellCount; cell++)
                Push(cell);
            Propagate();
            FailIfContradiction();
            UpdateCompletion();
        }

        public int Width { get; }

        public int Height { get; }

        public int CellCount => Width * Height;

        public SolverStatus Status { get; private set; }

        /// <summary>Collapses made by <see cref="Step"/>, including ones later undone.</summary>
        public int Steps { get; private set; }

        /// <summary>Collapses undone by backtracking, across all attempts.</summary>
        public int Backtracks { get; private set; }

        /// <summary>Times the solver started over because the backtrack budget ran out.</summary>
        public int Restarts { get; private set; }

        /// <summary>Cells whose possible tiles changed during the last construction, <see cref="Pin"/> or <see cref="Step"/>, including cells restored by backtracking.</summary>
        public ReadOnlySpan<int> ChangedCells => new ReadOnlySpan<int>(_changed, 0, _changedCount);

        public int CellIndex(int x, int y)
        {
            return x + y * Width;
        }

        /// <summary>
        /// Fixes cell (<paramref name="x"/>, <paramref name="y"/>) to <paramref name="tile"/> and propagates.
        /// Pins must be set before the first <see cref="Step"/> and are never undone.
        /// A pin that conflicts with the rules or another pin fails the solver.
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
            FailIfContradiction();
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
            _frames[_frameCount++] = new Frame { TrailStart = _trailLength, Cell = cell, Tile = tile };
            Collapse(cell, tile);

            bool backtracked = _contradiction;
            if (_contradiction)
                Recover();

            UpdateCompletion();
            return new StepResult(Status, cell, tile, backtracked);
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

        void DrawNoise()
        {
            for (int cell = 0; cell < _noise.Length; cell++)
                _noise[cell] = _random.NextDouble() * NoiseScale;
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

        // Undoes collapses until the state is consistent again, restarting or failing if the budget runs out.
        void Recover()
        {
            while (_contradiction)
            {
                _contradiction = false;

                // Every alternative from the base state has been ruled out: no solution exists.
                if (_frameCount == 0)
                {
                    Status = SolverStatus.Failed;
                    return;
                }

                if (_attemptBacktracks >= _maxBacktracks)
                {
                    Restart();
                    return;
                }

                Frame frame = _frames[--_frameCount];
                Undo(frame.TrailStart);
                Backtracks++;
                _attemptBacktracks++;

                // The ban is recorded in the parent frame, so it is undone too if the solver backs up further.
                Ban(frame.Cell, frame.Tile);
            }
        }

        void Ban(int cell, int tile)
        {
            Array.Copy(_allTiles, _mask, _words);
            _mask[tile >> 6] &= ~(1UL << (tile & 63));
            Intersect(cell, _mask);
            if (_contradiction)
                return;

            Push(cell);
            Propagate();
        }

        void Restart()
        {
            Restarts++;
            if (Restarts > _maxRestarts)
            {
                Status = SolverStatus.Failed;
                return;
            }

            Undo(0);
            _frameCount = 0;
            _attemptBacktracks = 0;

            // A new stream per restart, so each attempt explores a different map.
            _random = new Pcg32(_seed, (ulong)Restarts);
            DrawNoise();
        }

        void Propagate()
        {
            while (_stackSize > 0 && !_contradiction)
            {
                int cell = _stack[--_stackSize];
                _queued[cell] = false;
                int x = cell % Width;
                int y = cell / Width;

                for (int d = 0; d < DirectionExtensions.Count && !_contradiction; d++)
                {
                    var direction = (Direction)d;
                    int neighbourX = x + direction.OffsetX();
                    int neighbourY = y + direction.OffsetY();
                    if (_wrapEdges)
                    {
                        neighbourX = (neighbourX + Width) % Width;
                        neighbourY = (neighbourY + Height) % Height;
                    }
                    else if (neighbourX < 0 || neighbourX >= Width || neighbourY < 0 || neighbourY >= Height)
                    {
                        continue;
                    }

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

        // Removes every tile not in mask from the cell, recording the removal once stepping has begun.
        // Returns true if anything was removed. An emptied cell flags a contradiction.
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
                if (_stepped)
                    Record(cell, word, removed);

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
                _contradiction = true;

            MarkChanged(cell);
            return true;
        }

        void Record(int cell, int word, ulong bits)
        {
            if (_trailLength == _trail.Length)
                Array.Resize(ref _trail, _trail.Length * 2);

            _trail[_trailLength++] = new TrailEntry { Cell = cell, Word = word, Bits = bits };
        }

        // Restores every removal recorded after the trail stood at trailLength, newest first.
        void Undo(int trailLength)
        {
            while (_trailLength > trailLength)
            {
                TrailEntry entry = _trail[--_trailLength];
                int cell = entry.Cell;
                int countBefore = _possibleCount[cell];

                _domains[cell * _words + entry.Word] |= entry.Bits;
                for (ulong bits = entry.Bits; bits != 0; bits &= bits - 1)
                {
                    int tile = (entry.Word << 6) + Bitset.TrailingZeroCount(bits);
                    _possibleCount[cell]++;
                    _sumWeights[cell] += _rules.GetWeight(tile);
                    _sumWeightLogWeights[cell] += _rules.GetWeightLogWeight(tile);
                }

                if (countBefore <= 1 && _possibleCount[cell] > 1)
                    _undecidedCount++;
                MarkChanged(cell);
            }
        }

        void FailIfContradiction()
        {
            if (!_contradiction)
                return;

            _contradiction = false;
            Status = SolverStatus.Failed;
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
