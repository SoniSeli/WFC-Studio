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
