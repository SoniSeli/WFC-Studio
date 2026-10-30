# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the package uses [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-10-30

First release.

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
- `WfcGenerator` component: runs the solver on a `TileSet` and draws into a `Tilemap`, redrawing only changed cells in one batch per frame. Advances a set number of steps per second from `Update`, capped by a per-frame time budget so large maps never stall a frame. Has `Generate`, `GenerateInstantly`, `Pause`, `Resume`, `StepOnce`, `Complete` and `Clear`, an optional placeholder tile for undecided cells, solver time, and `Started` and `Redrawn` events.
- `EntropyHeatmap` component: overlays each undecided cell's entropy, one texel per cell, with a configurable gradient.
- `TilePopAnimation` component: tiles pop into place through Tilemap cell transforms, without extra GameObjects.
- `WfcStudio.Editor` assembly and the **Window → WFC Studio** window (UI Toolkit): pick or create a generator, choose its Tilemap and tile set (with a tile, socket and issue summary), and set size, seed, edge wrapping and speed. Generate live, instantly or step by step, then pause, finish or clear. Fields are bound to the generator's serialized properties, so they support Undo like the Inspector.
- WFC Studio window, **Tiles** section: a searchable tile list with sprite thumbnails, each tile's share of the total weight and an editable weight (bound to the tile set asset, with Undo). Selecting a tile shows its sockets and, for each side, every tile allowed there after socket rules and exceptions; click a neighbour to jump to it, double-click a tile to select its asset.
- WFC Studio window, **Validation** section: every issue the tile set compiles with, errors first, colour-coded by severity. Issues with one clear repair get a fix button: set an invalid weight to 1, remove a missing or duplicate tile entry, add undefined sockets, remove unused or unnamed sockets, and remove broken or redundant exceptions. A redundant exception is removed only if the compiled rules stay the same without it. Each fix is one Undo step. Issues about a tile get **Show tile**, which opens it in the Tiles section.
- Pinned tiles from a Tilemap: `WfcGenerator.Pins` names a Tilemap that designers paint with the Tile Palette. Before each map, every tile on it inside the map area is pinned at the matching cell. Tiles that aren't in the tile set are ignored with a warning, and pins that conflict fail the map with a warning.
- WFC Studio window, **Pins** section: add a half-transparent Pins Tilemap above the map, open the Tile Palette aimed at it, clear the pins with Undo, and see how many cells are pinned. **Create Tile Palette** builds a palette asset holding every tile of the tile set.
- Dependency on `com.unity.2d.tilemap` (the Tile Palette and its editor API).
- Determinism tests: final-map hashes for fixed rules, sizes and seeds (with backtracking, restarts, wrapping and more than 64 tiles), recorded before the solver speed-ups below. Allocation tests check that solving allocates nothing, counted with Unity's `GC.Alloc` profiler marker.
- Edit-mode generation: outside Play mode, an `EditorApplication.update` callback steps the solver at the generator's rate and frame budget. Each generate, step-started map and clear is one Undo step, including live generation that runs over many frames.
- Samples, importable from the Package Manager: **Puny World Tile Sets** (six tile sets made from CC0 pixel art, and a scene with a generator and a Pins Tilemap, with no other package dependencies) and **Live Demo** (the browser demo's scene and UI, which needs the first sample, the Input System and URP).

### Changed

- The minimum Unity version is 6000.0 (Unity 6 LTS), down from 6000.3. CI installs the package on its own into an empty 6000.0 project and runs its tests there.

- Solver speed: the next cell to collapse comes from an indexed min-heap of undecided cells, and only cells whose domains changed are re-keyed, so a step no longer scans the grid. Propagation for tile sets of up to 64 tiles builds each neighbour mask in a single word. On the Overworld sample, a 100×100 map solves in 58 ms instead of 466 ms in the Editor; see `docs/BENCHMARKS.md`. Maps are unchanged: the same rules and seed give the same map as before.
- The solver allocates nothing while solving, including during backtracking and restarts. The backtracking trail is sized once for its largest possible length, and restarts reseed the random generator in place (`Pcg32.Reset`).
- `WfcGenerator` marks its solver and Tilemap work with the `WfcGenerator.Solve` and `WfcGenerator.Redraw` Profiler markers.
- Socket matching is now a set of connections instead of a single mate. `SocketTileSet.SetConnected(a, b, connected)` replaces `Pair`, and `SocketDefinition` has `ConnectsToSelf` and a `ConnectsTo` list instead of `Mate`. One socket can now face several others, for example a feature `border` that faces `grass` but not another border, which keeps features from touching. `IssueCode.ConflictingSocketPairs` is removed.

- `SolverStatus.Failed` now means no solution was found: the rules and pins can't be satisfied, or the backtrack and restart budget ran out. Before M2 it meant the first contradiction.
