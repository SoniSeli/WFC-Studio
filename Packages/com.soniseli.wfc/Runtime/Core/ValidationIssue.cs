namespace WfcStudio.Core
{
    public enum IssueSeverity
    {
        /// <summary>Worth knowing; nothing is wrong.</summary>
        Info,

        /// <summary>The set compiles, but probably not as intended.</summary>
        Warning,

        /// <summary>The set cannot be compiled until this is fixed.</summary>
        Error,
    }

    public enum IssueCode
    {
        // Structure: reported by the tile set asset before rules are analysed.
        NoTiles,
        MissingTile,
        DuplicateTile,
        UnnamedSocket,
        DuplicateSocket,
        UnknownSocket,
        ConflictingSocketPairs,
        UnknownExceptionTile,

        // Definition: reported when compiling sockets into rules.
        InvalidWeight,
        MissingSocket,
        ConflictingExceptions,
        RedundantException,
        UnusedSocket,

        // Analysis of the compiled rules.
        NoNeighbour,
        CannotAppearInside,
    }

    /// <summary>One finding from validating a tile set, with a message written for designers.</summary>
    public readonly struct ValidationIssue
    {
        public ValidationIssue(IssueSeverity severity, IssueCode code, string message, int tile = -1)
        {
            Severity = severity;
            Code = code;
            Message = message;
            Tile = tile;
        }

        public IssueSeverity Severity { get; }

        public IssueCode Code { get; }

        public string Message { get; }

        /// <summary>The tile the issue is about, or -1 if it is about the whole set.</summary>
        public int Tile { get; }

        public override string ToString()
        {
            return $"{Severity} {Code}: {Message}";
        }
    }
}
