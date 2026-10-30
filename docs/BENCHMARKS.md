# Benchmarks

How fast the WFC Studio solver fills a map, measured with Unity's
[Performance Testing package](https://docs.unity3d.com/Packages/com.unity.test-framework.performance@latest).

## Reference machine

| | |
|---|---|
| CPU | Intel Core i5-14400F |
| Memory | 32 GB |
| OS | Windows 11 Pro |
| Unity | 6000.3.25f1, in the Editor (Mono) |

## Method

- **Tile sets:** the Puny World samples in `Assets/Samples/PunyWorld`. *Lake* has 15 tiles, and *Overworld* has all 61 (meadow, lakes, forests, mountains with terraces, castles).
- **Sizes:** square maps of 20×20, 50×50 and 100×100 cells.
- **Solve** times one whole `WfcSolver.Run()`: every step, propagation and any backtracking. Building the solver (pruning, pins) runs outside the timed region and is reported as **Setup**.
- **Samples:** each row is the median of 12 maps (6 at 100×100), each with a new seed, after 2 warm-up maps.
- **GC allocations** are counted with Unity's `GC.Alloc` profiler marker while solving.

## Results

Solve time, median:

| Tile set | Size | Before | After | Speed-up |
|---|---|---|---|---|
| Lake | 20×20 | 1.3 ms | 0.5 ms | 2.7× |
| Lake | 50×50 | 29.6 ms | 3.3 ms | 9.0× |
| Lake | 100×100 | 399.5 ms | 13.5 ms | 30× |
| Overworld | 20×20 | 3.9 ms | 1.9 ms | 2.1× |
| Overworld | 50×50 | 45.8 ms | 13.6 ms | 3.4× |
| Overworld | 100×100 | 466.3 ms | 57.7 ms | 8.1× |

Setup at 100×100: Lake 2.6 → 1.8 ms, Overworld 8.1 → 5.5 ms.

GC allocations while solving: 3 to 5 per map before, **0** after, at every size.

*Before* is the solver as of commit `19b342e`, and *after* is the solver in version 1.0.0. Both ran on the same machine, in the same session, on the sample tile sets of version 1.0.0.

## What changed

- **Finding the next cell:** the solver used to scan every cell, computing a logarithm for each, to find the one with the lowest entropy. The scan cost grew with the map, so the time per step did too: on Overworld, a step took about five times as long at 100×100 as at 20×20. Undecided cells now sit in an indexed min-heap. Only cells whose possible tiles changed are re-keyed before each pick.
- **Propagation:** with up to 64 tiles, a cell's possible tiles fit in one 64-bit word. That case now builds the set of allowed neighbours in a local variable, instead of writing to an array for every tile.
- **No allocation while solving:** the backtracking trail is sized once, for the most it can ever hold (every entry removes at least one tile, and a cell can lose at most all but one), instead of doubling as it fills. Restarts reseed the random generator in place instead of creating a new one.

None of this changes the maps. Equal entropies still go to the lowest cell index, exactly as the scan did. EditMode tests compare the final maps of 12 fixed cases, covering backtracking, restarts, wrapped edges and tile sets of more than 64 tiles, against hashes recorded from the solver before these changes.

## Running the benchmarks

Open **Window → General → Test Runner → EditMode** and run `WfcStudio.Benchmarks`, or from a terminal:

```
unity test . --mode EditMode
```

Results are written to `PerformanceTestResults.json` in the project's persistent data folder (on Windows, `%USERPROFILE%\AppData\LocalLow\Soni Seli\WFC Studio`).
The benchmarks are in the `Performance` category, and CI skips that category: timings on shared CI machines are too noisy to compare.
