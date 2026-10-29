# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the package uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Package skeleton: `WfcStudio.Core` (engine-free solver assembly), `WfcStudio.Runtime` (Unity integration) and the `WfcStudio.Tests` EditMode test assembly.
- Architecture tests that keep `WfcStudio.Core` free of `UnityEngine`/`UnityEditor` and `WfcStudio.Runtime` free of `UnityEditor`.
- `WfcSolver`: tiled-model WFC on a W×H grid. Picks the cell with the lowest weighted Shannon entropy (seeded noise breaks ties), chooses tiles by weight, and propagates rules with a work stack. Supports pinned tiles, step-by-step or full runs, and reports changed cells and per-cell entropy.
- `RuleTable` and its `Builder`: adjacency rules that are symmetric by construction, plus tile weights. Any tile count, stored as bitsets.
- `Pcg32`: seeded random number generator that gives the same sequence on every platform.
- EditMode tests for the solver, rule table and random number generator.
- Backtracking: contradictions undo the newest collapse exactly (from a trail of removals), ban that tile in that cell and propagate again. Pins are never undone. When `SolverOptions.MaxBacktracks` runs out, the solver restarts on a new random stream, up to `MaxRestarts` times.
- `SolverOptions.WrapEdges`: opposite edges become neighbours, so maps tile seamlessly.
- `Backtracks` and `Restarts` stats, and `StepResult.Backtracked`. Cells restored by backtracking are reported in `ChangedCells`.
- `SocketTileSet` (Core): describes tiles by edge sockets. Matching sockets may touch, paired sockets match only each other, and exceptions allow or forbid single pairs. `Compile()` returns a `RuleTable` plus `ValidationIssue`s, which flag missing sockets, invalid weights, conflicting or redundant exceptions, unused sockets, tiles that can only appear at a map edge, and tiles that can't appear away from the edges.
- `TileSet` asset (Runtime, **Create → WFC Studio → Tile Set**): socket labels, tile entries (tile, weight, four sockets) and exceptions, authored in one asset. Structural problems such as unknown socket names or missing and duplicate tiles are reported before the rules are analysed.
- `RuleTable.Builder.Disallow`.

### Changed

- `SolverStatus.Failed` now means no solution was found: the rules and pins can't be satisfied, or the backtrack and restart budget ran out. Before M2 it meant the first contradiction.
