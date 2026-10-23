# Grass simulation

Run `dotnet run --project tests/GrassGrid/GrassGrid.csproj` from the repository root.

The battle0 scene starts a 100 by 100 grass grid over world XY [-5000, 5000], Z=0.
Cells are 100 units wide. Initial population is 150 distinct random cells, amount 1.
The grass world scale is 10 times the original v0.3 dimensions; the existing
white frame matches the field at [-5000, 5000].
Every 10 seconds of game time, a snapshot-based step births amount 1 in empty
cells with at least one occupied Moore neighbor and increases existing grass by 1.
Grass amount is shown by green brightness; empty cells are transparent.

Provisional choices from the unresolved v0.3 memo: no boundary wrapping,
unique initial positions, maximum amount 10. Grass Field exposes these rule
settings in the Inspector (initial positions always remain unique).
Disable randomizeSeed to reproduce a run with the seed field.
Birth minimum, growth enable, increase, maximum, boundary wrapping and interval
can be changed in the Inspector; these are not yet connected to cards.
Cell resizing, arbitrary rule expressions, rule merging, carriers, animal reproduction
and death rules are outside this initial implementation.

Play Mode checks: open battle0, pan to inspect the grass across [-5000, 5000];
wait 10 seconds for one-cell propagation and brightness growth; verify the
white border, player and target remain visible. Pausing game time pauses grass.
The automated tests exercise the actual simulation source, not Unity rendering
or the MonoBehaviour clock.

## Consumption API

Keep a reference to the scene's `GrassField` and call on the Unity main thread:

```csharp
int eaten = grassField.ConsumeGrass((Vector2)transform.position, 3);
```

Coordinates are continuous world XY, not cell indices or multiples of 100.
For example, (123.4, -56.7) belongs to cell (51, 49), covering
X [100, 200) and Y [-100, 0). Both field axes include -5000 and exclude 5000.
Outside coordinates (even with boundary wrapping enabled), NaN/infinity,
non-positive requests, empty cells and calls before field initialization return 0.
The result is the actual amount removed, capped by available grass.
State changes immediately; display refreshes in LateUpdate, batching calls.
The next simulation tick uses the consumed state; an emptied cell can be born
again if its neighbors satisfy the birth rule.

The Herbivore prefab includes HerbivoreFeeding: after one second of active game
time, and every second thereafter, it requests 1 grass at its current world XY.
Empty cells consume nothing. Feeding pauses with game time and resets its timer
when disabled. The component resolves the scene GrassField once at startup.
Play Mode: place an animal on an occupied cell, verify consumption after one
second, move it across a cell boundary, and verify the next tick uses the new cell.

## Herbivore movement

HerbivoreMovement is attached to the prefab. It keeps a normalized Vector2
heading (initially right), including while stopped. The initial decision happens
at Start; subsequent decisions run immediately after each one-second feeding tick.
GrassField updates before feeding (execution order 10), then movement runs (20).

If the current cell contains grass, stop and skip target selection. Otherwise
visit the square of cells within +/-5 indices of the current cell, clipped to
the field. Score occupied cells using their centers:
`(amount / 10.0) * ((dot + 1) * 0.4 + 0.2)`.
Dot uses the normalized heading and normalized direction to the center.
Tie breaks prefer larger dot, then shorter distance; exact remaining ties use
stable row order. Each cell is visited once without allocations or sorting.
With no target, continue forward.

Movement is world XY at 100 U/s, turning at most 90 degrees/s each frame;
Z is preserved. Integration uses steps of at most 0.02 seconds. Entering an
occupied cell stops movement immediately rather than waiting for the next tick.
After the last grass is consumed, the same feeding tick selects a new target.
An emptied target cell counts as reached upon entry; the next decision selects
another target. No speed or heading reset is applied while stopped.

Near edges, inward steering overrides food targets. An additional position clamp
keeps the animal center inside the field even if initially spawned at the edge
facing outwards. This emergency clamp can reduce actual travel below 100 U/s
while turning at the boundary; it never snaps the heading past the turn limit.

Tests include square corners, exclusion beyond five cells, front/side/back scores,
normalization, occupied-current-cell stop decisions, empty searches, tie breaks,
continuous coordinates and clipped boundary searches. In Play Mode additionally
verify feeding then departure, arrival and stopping, smooth turns, boundary
avoidance, retained Z, and pausing. These visual/runtime checks are not covered
by the command-line simulation tests.
