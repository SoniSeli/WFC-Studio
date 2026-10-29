namespace WfcStudio.Core
{
    public enum SolverStatus
    {
        /// <summary>Some cells are still undecided.</summary>
        Running,

        /// <summary>Every cell holds exactly one tile, and all rules are satisfied.</summary>
        Completed,

        /// <summary>A cell ran out of possible tiles (a contradiction).</summary>
        Failed,
    }

    /// <summary>Outcome of one <see cref="WfcSolver.Step"/>.</summary>
    public readonly struct StepResult
    {
        public StepResult(SolverStatus status, int cell, int tile)
        {
            Status = status;
            Cell = cell;
            Tile = tile;
        }

        /// <summary>Solver status after the step.</summary>
        public SolverStatus Status { get; }

        /// <summary>The cell collapsed by this step, or -1 if none was.</summary>
        public int Cell { get; }

        /// <summary>The tile chosen for <see cref="Cell"/>, or -1 if no cell was collapsed.</summary>
        public int Tile { get; }
    }
}
