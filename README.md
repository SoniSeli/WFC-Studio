# WFC Studio

**Designer-friendly procedural map generation for Unity 6, built on the Wave Function Collapse algorithm.**

[![Tests](https://github.com/SoniSeli/WFC-Studio/actions/workflows/tests.yml/badge.svg)](https://github.com/SoniSeli/WFC-Studio/actions/workflows/tests.yml)
![Unity 6.3 LTS](https://img.shields.io/badge/Unity-6.3%20LTS-black?logo=unity)
![License: MIT](https://img.shields.io/badge/license-MIT-blue)

> **Status:** early development. The solver, tile set assets, live demo, WFC Studio editor window and benchmarks are done; a public release with a WebGL demo is next.

<!-- M7: hero GIF of a map generating, with the entropy heatmap on -->

WFC Studio is a from-scratch rewrite of the Unity tool I built for my Computer Science bachelor's thesis,
*Map Generator using Wave Function Collapse Algorithm* ([itch.io page and thesis PDF](https://soniseli.itch.io/map-generator-wfc)).
The thesis tackled the content bottleneck in game development: designers need more levels than they can build by hand.
This version keeps that goal and rebuilds the tool to production standards: a deterministic, tested solver,
validated tile rules, and editor tooling a designer can use without writing code.

## Planned features

- **Deterministic solver in plain C#**: same seed, same map, on every platform. No `UnityEngine` dependency, fully unit tested.
- **Weighted entropy and backtracking**: control how often each tile appears, and recover from contradictions instead of restarting.
- **Edge sockets instead of neighbour lists**: label each tile edge, and connected sockets may touch. Rules can never be one-sided, and a new tile needs only its own four labels. Socket connections express layout rules. For example, a `border` that faces grass but not another border keeps features apart. Exceptions cover the special cases, such as a minimum feature size.
- **Validated tile sets**: tile sets are ScriptableObject assets. The checker explains problems in plain language, such as an unknown socket, a tile that can only appear at the map edge, or conflicting exceptions.
- **Live visualization**: watch the map collapse step by step, with an entropy heatmap overlay.
- **WFC Studio editor window**: browse a tile set's tiles, weights and allowed neighbours, fix validation issues in one click, paint the tiles you want fixed with the Tile Palette, then let WFC fill in the rest. Edit-mode generation with full Undo.
- **Measured performance**: a 100×100 map with all 61 sample tiles solves in about 56 ms in the Editor, with no garbage-collector allocations while solving. See [docs/BENCHMARKS.md](docs/BENCHMARKS.md).

## Repository layout

```
WFC-Studio/
├─ Packages/com.soniseli.wfc/   The tool, as a UPM package
│  ├─ Runtime/Core/             WfcStudio.Core: solver, plain C#
│  ├─ Runtime/Unity/            WfcStudio.Runtime: assets, Tilemap output, visualization
│  ├─ Editor/                   WfcStudio.Editor: WFC Studio window, edit-mode generation
│  └─ Tests/Editor/             WfcStudio.Tests: EditMode tests
├─ Assets/Demo/                 Demo scene: UI Toolkit panel, live generation, entropy heatmap
├─ Assets/Samples/PunyWorld/    Sample tile sets: Meadow, Lake, Forest, Mountain, Castle, Overworld
├─ Assets/Dev/Editor/           Tools that rebuild the samples and demo scene from code, measure them, and make WebGL builds
├─ Assets/Tests/Editor/         Tests that every sample validates and generates
├─ Assets/Tests/Performance/    Solver benchmarks (Performance Testing package), see docs/BENCHMARKS.md
└─ .github/workflows/           CI: EditMode tests on every push to main
```

## Installing (from v0.1)

In Unity: **Window → Package Manager → + → Install package from git URL**, then enter:

```
https://github.com/SoniSeli/WFC-Studio.git?path=/Packages/com.soniseli.wfc
```

## Development

- Unity **6000.3.25f1** (6.3 LTS). Open the repository root with Unity Hub, then open `Assets/Demo/Demo.unity` and press Play.
- Demo controls: pick a tile set, size and seed, then **Generate** to watch it run or **Instant** to solve in one go. Shortcuts: **Space** pauses, **N** steps, **G** generates, **R** picks a new seed.
- Editor tool: open **Window → WFC Studio**. Pick or create a generator, choose a tile set, then generate in Edit mode. Ctrl+Z undoes any map. To pin tiles, use **Add Pins Tilemap**, **Create Tile Palette** and **Paint Pins**.
- Run tests from **Window → General → Test Runner → EditMode**, or from a terminal with the Unity CLI:
  `unity test . --mode EditMode`

## Credits

- Wave Function Collapse was created by Maxim Gumin: [mxgmn/WaveFunctionCollapse](https://github.com/mxgmn/WaveFunctionCollapse).
- Sample tile art: [16x16 Puny World](https://merchant-shade.itch.io/16x16-puny-world) by merchant-shade, a rework of [16x16 Mini World Sprites](https://merchant-shade.itch.io/16x16-mini-world-sprites) by Kurt Gabriel Sotelo and octoshrimpy. Both are CC0; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## License

[MIT](LICENSE) © 2026 Soni Seli
