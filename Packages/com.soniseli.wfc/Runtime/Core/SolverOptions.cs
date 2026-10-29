namespace WfcStudio.Core
{
    /// <summary>Settings for a <see cref="WfcSolver"/>. The solver copies them at construction.</summary>
    public sealed class SolverOptions
    {
        /// <summary>When true, the right edge neighbours the left edge and the top neighbours the bottom, so the map tiles seamlessly.</summary>
        public bool WrapEdges { get; set; }

        /// <summary>Backtracks allowed in one attempt before the solver restarts. 0 restarts on the first contradiction.</summary>
        public int MaxBacktracks { get; set; } = 1000;

        /// <summary>Restarts allowed before the solver gives up with <see cref="SolverStatus.Failed"/>.</summary>
        public int MaxRestarts { get; set; } = 10;
    }
}
