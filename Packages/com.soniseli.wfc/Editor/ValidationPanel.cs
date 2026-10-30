using System;
using System.Linq;
using UnityEngine.UIElements;
using WfcStudio.Core;

namespace WfcStudio.Editor
{
    /// <summary>
    /// The window's Validation section: every issue the tile set compiles with, most severe first. Issues with an obvious
    /// repair get a fix button; issues about a tile get a button that shows it in the Tiles section.
    /// </summary>
    internal sealed class ValidationPanel
    {
        readonly Foldout _section;
        readonly VisualElement _issues;
        TileSet _tileSet;

        public ValidationPanel(Foldout section)
        {
            _section = section;
            _issues = section.Q("issues");
        }

        /// <summary>Raised when the designer asks to see the tile an issue is about.</summary>
        public event Action<int> ShowTileRequested;

        public void SetTileSet(TileSet tileSet)
        {
            _tileSet = tileSet;
            Refresh();
        }

        public void Refresh()
        {
            _issues.Clear();
            _section.EnableInClassList("hidden", _tileSet == null);
            if (_tileSet == null)
                return;

            var issues = _tileSet.Compile().Issues.OrderByDescending(issue => issue.Severity).ToList();
            int errors = issues.Count(issue => issue.Severity == IssueSeverity.Error);
            int warnings = issues.Count(issue => issue.Severity == IssueSeverity.Warning);
            int notes = issues.Count - errors - warnings;
            _section.text = issues.Count == 0 ? "Validation · no issues"
                : "Validation · " + string.Join(", ", new[]
                {
                    errors > 0 ? WfcStudioWindow.Plural(errors, "error") : null,
                    warnings > 0 ? WfcStudioWindow.Plural(warnings, "warning") : null,
                    notes > 0 ? WfcStudioWindow.Plural(notes, "note") : null,
                }.Where(part => part != null));

            if (issues.Count == 0)
            {
                var clean = new Label("The tile set compiles with no errors, warnings or notes.");
                clean.AddToClassList("hint");
                _issues.Add(clean);
                return;
            }

            foreach (var issue in issues)
                _issues.Add(MakeRow(issue));
        }

        VisualElement MakeRow(ValidationIssue issue)
        {
            var row = new VisualElement();
            row.AddToClassList("issue");
            row.AddToClassList("issue--" + issue.Severity.ToString().ToLowerInvariant());

            var message = new Label(issue.Message);
            message.AddToClassList("issue-message");
            row.Add(message);

            var actions = new VisualElement();
            actions.AddToClassList("issue-actions");

            var fix = TileSetFixes.For(_tileSet, issue);
            if (fix != null)
            {
                var button = new Button(() =>
                {
                    fix.Apply();
                    Refresh();
                }) { text = fix.Label, tooltip = "Undo reverts this." };
                actions.Add(button);
            }

            int tile = issue.Tile;
            if (tile >= 0 && tile < _tileSet.Tiles.Count)
                actions.Add(new Button(() => ShowTileRequested?.Invoke(tile)) { text = "Show tile" });

            if (actions.childCount > 0)
                row.Add(actions);
            return row;
        }
    }
}
