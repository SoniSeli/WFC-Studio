# Live Demo

The interactive demo from the WFC Studio web build: pick a tile set, size and seed, then watch the map generate, pause
and step through it, or solve it instantly.

## Requirements

- The **Puny World Tile Sets** sample. Import it first: the demo uses its tile sets.
- The **Input System** package, for the keyboard shortcuts.
- **URP** with the 2D Renderer, for the scene's 2D light. In another render pipeline the light shows as a missing
  component; delete it and the demo still runs.

## Try it

Open **Demo.unity** and press Play.

- **Generate** watches the map fill in; **Instant** solves it in one go.
- **Space** pauses, **N** steps, **G** generates and **R** picks a new seed.
- **Entropy heatmap** tints each undecided cell by how open it still is.
