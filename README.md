# WFC Studio

**Designer-friendly procedural map generation for Unity 6, built on the Wave Function Collapse algorithm.**

[![Tests](https://github.com/SoniSeli/WFC-Studio/actions/workflows/tests.yml/badge.svg)](https://github.com/SoniSeli/WFC-Studio/actions/workflows/tests.yml)
![Unity 6.3 LTS](https://img.shields.io/badge/Unity-6.3%20LTS-black?logo=unity)
![License: MIT](https://img.shields.io/badge/license-MIT-blue)

> **Status:** early development, Milestone 1 (core solver). See the [roadmap](#roadmap).

<!-- M7: hero GIF of a map generating, with the entropy heatmap on -->

WFC Studio is a from-scratch rewrite of the Unity tool I built for my Computer Science bachelor's thesis,
*Map Generator using Wave Function Collapse Algorithm* ([itch.io page and thesis PDF](https://soniseli.itch.io/map-generator-wfc)).
The thesis tackled the content bottleneck in game development: designers need more levels than they can build by hand.
This version keeps that goal and rebuilds the tool to production standards: a deterministic, tested solver,
validated tile rules, and editor tooling a designer can use without writing code.

## Planned features

- **Deterministic solver in plain C#**: same seed, same map, on every platform. No `UnityEngine` dependency, fully unit tested.
- **Weighted entropy and backtracking**: control how often each tile appears, and recover from contradictions instead of restarting.
- **Validated tile sets**: tile sets are ScriptableObject assets, and a rule checker finds and fixes one-sided or missing rules.
- **Live visualization**: watch the map collapse step by step, with an entropy heatmap overlay.
- **WFC Studio editor window**: paint the tiles you want fixed, then let WFC fill in the rest. Edit-mode generation with full Undo.
- **Measured performance**: published benchmarks for 20×20 to 100×100 maps.

## Repository layout

```
WFC-Studio/
├─ Packages/com.soniseli.wfc/   The tool, as a UPM package
│  ├─ Runtime/Core/             WfcStudio.Core: solver, plain C#
│  ├─ Runtime/Unity/            WfcStudio.Runtime: assets, Tilemap output, visualization
│  └─ Tests/Editor/             WfcStudio.Tests: EditMode tests
├─ Assets/                      Demo project (demo scene arrives in M4)
└─ .github/workflows/           CI: EditMode tests on every push and PR
```

## Installing (from v0.1)

In Unity: **Window → Package Manager → + → Install package from git URL**, then enter:

```
https://github.com/SoniSeli/WFC-Studio.git?path=/Packages/com.soniseli.wfc
```

## Development

- Unity **6000.3.25f1** (6.3 LTS). Open the repository root with Unity Hub.
- Run tests from **Window → General → Test Runner → EditMode**, or from a terminal with the Unity CLI:
  `unity test . --mode EditMode`

## Roadmap

- [x] **M0 Foundation**: project, package skeleton, CI
- [x] **M1 Core solver**: domains, propagation, weighted entropy, seeded RNG, pinned tiles
- [ ] **M2 Robustness**: backtracking, stats, failure handling
- [ ] **M3 Authoring data**: tile set assets, rule checker, biomes
- [ ] **M4 Runtime visualization**: Tilemap output, live stepping, entropy heatmap, demo scene
- [ ] **M5 Editor tool**: WFC Studio window, pinned tiles, Undo
- [ ] **M6 Performance**: profiling, benchmarks, optional Burst solver
- [ ] **M7 Release**: WebGL demo, docs, v1.0.0

## Credits

- Wave Function Collapse was created by Maxim Gumin: [mxgmn/WaveFunctionCollapse](https://github.com/mxgmn/WaveFunctionCollapse).
- Tile art: to be decided.

## License

[MIT](LICENSE) © 2026 Soni Seli
