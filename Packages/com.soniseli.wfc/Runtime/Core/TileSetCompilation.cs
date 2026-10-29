using System.Collections.Generic;

namespace WfcStudio.Core
{
    /// <summary>Result of compiling a tile set: the rules, if there were no errors, and every issue found.</summary>
    public sealed class TileSetCompilation
    {
        public TileSetCompilation(RuleTable rules, IReadOnlyList<ValidationIssue> issues)
        {
            Rules = rules;
            Issues = issues;

            foreach (var issue in issues)
                if (issue.Severity == IssueSeverity.Error)
                    HasErrors = true;
        }

        /// <summary>The compiled rules, or null when <see cref="HasErrors"/> is true.</summary>
        public RuleTable Rules { get; }

        public IReadOnlyList<ValidationIssue> Issues { get; }

        public bool HasErrors { get; }
    }
}
