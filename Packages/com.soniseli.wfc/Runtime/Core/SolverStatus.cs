namespace WfcStudio.Core
{
    public enum SolverStatus
    {
        /// <summary>Some cells are still undecided.</summary>
        Running,

        /// <summary>Every cell holds exactly one tile, and all rules are satisfied.</summary>
        Completed,

        /// <summary>
        /// No solution was found: the rules and pins cannot be satisfied, or the backtrack and restart budget ran out.
        /// </summary>
        Failed,
    }

    /// <summary>Outcome of one <see cref="WfcSolver.Step"/>.</summary>
    public readonly struct StepResult
    {
        public StepResult(SolverStatus status, int cell, int tile, bool backtracked = false)
        {
            Status = status;
            Cell = cell;
            Tile = tile;
            Backtracked = backtracked;
        }

        /// <summary>Solver status after the step.</summary>
        public SolverStatus Status { get; }

        /// <summary>The cell collapsed by this step, or -1 if none was.</summary>
        public int Cell { get; }

        /// <summary>The tile chosen for <see cref="Cell"/>, or -1 if no cell was collapsed.</summary>
        public int Tile { get; }

        /// <summary>True if the collapse led to a contradiction and was undone by backtracking or a restart.</summary>
        public bool Backtracked { get; }
    }
}
