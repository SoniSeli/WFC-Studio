# WFC Studio (package)

Designer-friendly procedural map generation for Unity 6 using the Wave Function Collapse algorithm.
Requires Unity 6000.0 or later. See the [repository README](https://github.com/SoniSeli/WFC-Studio#readme) for features,
install steps and a quick start, and [the browser demo](https://soniseli.itch.io/map-generator-wfc) to try it.

| Assembly | Purpose |
|---|---|
| `WfcStudio.Core` | Solver, rule tables, sockets and validation in plain C#. No `UnityEngine` references. |
| `WfcStudio.Runtime` | Unity integration: tile set assets, the `WfcGenerator` component, Tilemap output, entropy heatmap. |
| `WfcStudio.Editor` | The **Window → WFC Studio** editor window and edit-mode generation with Undo. |
| `WfcStudio.Tests` | EditMode tests. |

## Samples

Import these from the Package Manager's **Samples** tab:

- **Puny World Tile Sets**: six tile sets and a ready-to-run scene. Needs nothing else.
- **Live Demo**: the browser demo's scene. Needs the Puny World Tile Sets sample, the Input System and URP.

The sample art is [16x16 Puny World](https://merchant-shade.itch.io/16x16-puny-world) by merchant-shade, CC0.
