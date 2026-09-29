# Dungeon 04 — Layout Styles

**Scripts:** `Stages/Layout/LayoutStage.cs` (`LayoutStage`, `AnchorAreas`, `LayoutUtil`), `RoomScatterLayout.cs`,
`BspLayout.cs`, `CaveLayout.cs` (`CaveLayout`, `CaveField`), `HybridLayout.cs`, `GridMazeLayout.cs` (+
`TemplateStamper`), `RoomShapes.cs`.

---

## 1. Concept

Stage 2 fills each floor with **areas**: rooms, halls, cave chambers and the fixed anchor rooms. The areas are
stamped into the grid but **not yet connected**. That is stage 3's job.

Each floor has one **style**, and each style is an `ILayoutStrategy`:

| Style | Strategy | Looks like | Area kinds / zone style |
|---|---|---|---|
| **Rooms** | `RoomScatterLayout` | scattered rooms of many shapes and sizes, spread apart | Room / Hall, Built |
| **BSP** | `BspLayout` | a tidy building: space split recursively, one room per cell of the split | Room / Hall, Built |
| **Caverns** | `CaveLayout` (`CaveField`) | organic caves from a cellular automaton, split into chambers | Cavern, Cavern (Organic cells) |
| **Hybrid** | `HybridLayout` | a floor divided into zones: caves in cavern zones, rooms in built and ruins zones | mixed |
| **Grid Maze** | `GridMazeLayout` | rooms on a regular grid of blocks joined as a maze (the original dungeon's idea); supports room templates and legacy prefabs | Room, Built |

`LayoutStage.Strategies` is a public dictionary: replace an entry to plug in your own strategy.

## 2. Macro flowchart (per floor, all floors in parallel)

```mermaid
flowchart TD
    A["LayoutStage: ForEachFloor<br/>stream 'Layout' + floor"] --> B["AnchorAreas.Create:<br/>anchor rooms → fixed areas with roles<br/>shafts → Link cells (Reserved, Pit for drops)<br/>stair wells get a Reserved rock ring<br/>landing cells flagged"]
    B --> C{"floor style"}
    C -- Rooms --> R["RoomScatterLayout"]
    C -- BSP --> S["BspLayout"]
    C -- Caverns --> V["CaveLayout"]
    C -- Hybrid --> H["HybridLayout"]
    C -- GridMaze --> M["GridMazeLayout"]
    R --> Z["RebuildAreaCells, RemoveEmptyAreas,<br/>RecomputeBounds"]
    S --> Z
    V --> Z
    H --> Z
    M --> Z
    Z --> Y["ArrivalArea / DepartureArea<br/>from the anchor cells"]
    Y -- "arrival lost its area" --> F["ctx.Fail"]
    Y --> L["after all floors: tie each VerticalLink<br/>to its two landing areas (lost → Fail)"]
```

**Rules every strategy follows:**

- never carve `Reserved` cells: outside the footprint, around stair wells, or shafts;
- keep at least one rock cell between separate areas, so rooms never merge by accident (`LayoutUtil.CanPlace` checks
  a Chebyshev spacing);
- every room plan is 4-connected. Diagonal-only contacts never count as connected, in generation or in the mesh.

## 3. Rooms (scatter)

```mermaid
flowchart TD
    A["target = allowed cells × Coverage.Lerp(openness)<br/>(0.20-0.38 of the floor)"] --> B["size-class weights:<br/>weight × (1 + bias·(2·open − 1))<br/>× (1 − bias·0.5·(2·complex − 1))"]
    B --> C{"placed < target and<br/>attempts left and<br/>failures in a row < 160?"}
    C -- no --> END["done"]
    C -- yes --> D["pick size class → side<br/>(after 60 failures: side shrinks)"]
    D --> E["aspect = 1 + triangular × Aspect Variation<br/>w = side·aspect, h = side / aspect"]
    E --> F["spacing = 1 (Tight Packing Chance)<br/>or Spacing 2-4"]
    F --> G["Spread Candidates (4) random positions,<br/>sorted by distance to the nearest room centre"]
    G --> H["shape = PickShape (rect, L, T, cross,<br/>circle, composite, pillared hall, ruined)"]
    H --> I{"CanPlace at the farthest candidate?<br/>(allowed cells, spacing)"}
    I -- "no: next candidate" --> I
    I -- "none fits" --> J["failures + 1"] --> C
    I -- yes --> K["Stamp area:<br/>Hall if hall class or ≥ 110 cells, else Room"]
    K --> C
```

**Openness and complexity:** "Large" and "Hall" have a positive *openness bias*, so open floors get more big rooms.
"Closet" and "Small" have a negative bias, so closed floors get more small rooms. Complexity nudges the other way.

### Room shapes (`RoomShapes.Generate`)

| Shape | Default weight | Minimum side | Notes |
|---|---|---|---|
| Rectangle | 3 | 3 | — |
| L-shape | 1 | 5 | random rotation |
| T-shape | 0.6 | 5 | random rotation |
| Cross | 0.5 | 5 | — |
| Circle / ellipse | 0.6 | 5 | — |
| Composite | 1 | 5 | overlapping rectangles |
| Pillared hall | 0.5 | 8 | adds `Pillar` cells (solid pillars inside the room) |
| Ruined | 0 | 4 | eroded outline (Hybrid ruins zones use it with *Ruins Erosion*) |

A room too small for its shape falls back to a rectangle. After shaping, only the **largest 4-connected group** of
floor cells is kept.

## 4. BSP (binary space partition)

```mermaid
flowchart TD
    A["minLeaf = Min Leaf Size × lerp(1.25, 0.8, complexity)<br/>(≥ 6)"] --> B["Split(rect, depth)"]
    B --> C{"depth > 2 and<br/>rand < Early Stop Chance × (1.3 − complexity)?"}
    C -- yes --> LEAF["leaf"]
    C -- no --> D{"can split<br/>(both halves ≥ minLeaf, depth ≤ 12)?"}
    D -- no --> LEAF
    D -- yes --> E["axis: the longer one if ratio > 1.25, else random<br/>position: Split Ratio 0.38-0.62"]
    E --> B
    LEAF --> F{"rand < Empty Leaf Chance (0.08)?"}
    F -- yes --> X["no room"]
    F -- no --> G["room inside the leaf with Room Margin 1-3,<br/>size ≥ lerp(0.45, 0.65, openness) of the leaf"]
    G --> H["shaped with Shaped Room Chance (0.3),<br/>else rectangle"]
    H --> I{"CanPlace?"}
    I -- "no (3 tries, shrinking)" --> X
    I -- yes --> J["stamp Room / Hall"]
```

## 5. Caverns (cellular automaton + chambers)

```mermaid
flowchart TD
    A["fill = Solid Fill.Lerp(1 − openness)<br/>(0.46-0.555)"] --> B["seed every allowed cell:<br/>open if rand ≥ fill + (noise − 0.5)·2·Fill Noise Strength<br/>(value noise, scale 0.06)"]
    B --> C["Smoothing Iterations (5) × cellular automaton:<br/>8-neighbour rock count ≥ Rock Threshold (5) → rock<br/>≤ Open Threshold (3) → open"]
    C --> D["ResolveSaddles: open diagonal-only contacts"]
    D --> E["Widen ×2: remove 1-cell passages<br/>(if Min Passage Width ≥ 2)"]
    E --> F["drop pockets < Min Region Cells (40)<br/>unless they touch an existing area"]
    F --> G["keep a rock ring around non-cave areas"]
    G --> H["MakeChambers"]
    H --> H1["distance field (exact EDT) of open cells"]
    H1 --> H2["peaks ≥ Chamber Min Radius (2.2),<br/>sorted by distance to rock"]
    H2 --> H3["centres ≥ Chamber Spacing × lerp(1.3, 0.8, complexity) apart<br/>(spatial hash), every pocket gets ≥ 1"]
    H3 --> H4["geodesic partition: BFS from all centres<br/>at once → one chamber per centre"]
    H4 --> H5["merge tiny chambers into the neighbour<br/>with the longest shared border"]
    H5 --> I["chambers already touching →<br/>preset Opening connections"]
```

**The cellular-automaton rule** turns random static into cave shapes. A cell surrounded by rock becomes rock, and a
cell surrounded by open space opens up. After five passes only smooth blobs and winding passages remain. Every cave
cell gets the `Organic` flag, which later selects noise-varied floor heights, domed ceilings and marching-squares walls.

## 6. Hybrid (zones)

```mermaid
flowchart TD
    A["zone points: jittered grid, step Zone Size (22 cells)"] --> B["zone style per point, weighted:<br/>Built (1), Cavern (1 × (0.7 + 0.6·openness)), Ruins (0.5)"]
    B --> C["ensure ≥ 1 cavern zone and ≥ 1 built/ruins zone"]
    C --> D["zone of a cell = nearest zone point<br/>at noise-warped coordinates (Border Noise 6)"]
    D --> E["cavern zones → CaveField on those cells"]
    D --> F["built / ruins zones → room scatter<br/>(coverage × 1.1, ruins rooms = Ruined shape<br/>with Ruins Erosion 0.35)"]
    E --> G["areas keep their zone style<br/>(Built, Cavern, Ruins)"]
    F --> G
```

Where a cave meets a built room, stage 3 creates a **Breach**: a rough passage with rubble. Props with placement
*Transition* go there.

## 7. Grid Maze

```mermaid
flowchart TD
    A["block = max(Block Size 7, largest maze template)<br/>pitch = block + Gap (2)"] --> B["cols × rows that fit the footprint, centred"]
    B --> C["valid block = a plain room fits there<br/>(not over anchors or reserved cells)"]
    C --> D["randomised depth-first search<br/>from shuffled start blocks<br/>(every valid component, no cap)"]
    D --> E["prune = valid × Prune Fraction.Lerp(1 − complexity)<br/>leaves (degree 1) removed at random"]
    E --> F["extra loop edges between adjacent blocks<br/>with Loop Chance.Lerp(openness)"]
    F --> G["one area per block:<br/>maze template (TemplateStamper) or plain room"]
    G --> H["maze edges → PresetConnections<br/>(forced in stage 3)"]
```

Maze templates can be **legacy RoomBehaviour prefabs** (`RoomTemplate` with *Legacy Room Behaviour* ticked, 7 × 7
cells, a door in the middle of each side). The old dungeon's rooms therefore still work inside the new generator.

## 8. Configuration summary

| Group | Key settings (defaults) |
|---|---|
| Rooms | Coverage 0.2–0.38 · Sizes (Closet 3–4 w0.45, Small 4–6 w1, Medium 6–9 w1.2, Large 9–13 w0.55, Hall 13–18 w0.2) · Shapes (above) · Aspect Variation 0.35 · Spacing 2–4 · Tight Packing Chance 0.15 · Placement Attempts 700 · Spread Candidates 4 · Room/Hall/Corridor ceiling 4 / 6.5 / 3.2 m |
| BSP | Min Leaf Size 11 · Split Ratio 0.38–0.62 · Room Margin 1–3 · Empty Leaf Chance 0.08 · Early Stop Chance 0.12 · Shaped Room Chance 0.3 |
| Caves | Solid Fill 0.46–0.555 · Fill Noise Strength 0.2 · Smoothing Iterations 5 · Rock/Open Threshold 5/3 · Min Region Cells 40 · Min Passage Width 2 · Chamber Min Radius 2.2 · Chamber Spacing 8 |
| Hybrid | Zone Size 22 · Border Noise 6 · Built/Cavern/Ruins weights 1/1/0.5 · Ruins Erosion 0.35 |
| Maze | Block Size 7 · Gap 2 · Prune Fraction 0–0.35 · Templates |

Full ranges and effects: [12 Configuration Reference](12-Configuration-Reference.md).

## 9. Example

A Medium floor (≈ 64 × 64 cells, openness 0.6, style Rooms):

- target coverage = 0.2 + 0.18 × 0.6 ≈ 0.31 → about 0.31 × 4 000 allowed cells ≈ 1 240 room cells;
- with Medium rooms averaging ~56 cells, that is roughly 15–25 rooms, plus 3–6 anchor rooms (entrance or arrival
  landing, departure landing, drop rooms).

## 10. Performance

All plain C# on the worker, floors in parallel. The cellular automaton costs O(cells × iterations). The distance
field is linear (Felzenszwalb–Huttenlocher). Room scatter costs O(attempts × room area).

## 11. Debugging

Use the Dungeon Preview window ([11](11-Editor-Tools-and-Testing.md)) with **Styles** / **Zones** view.

| Symptom | Cause | Fix |
|---|---|---|
| Few, huge caves | low Solid Fill, few Smoothing Iterations | raise Solid Fill or Rock Threshold |
| Caves full of specks | high Fill Noise Strength, low Min Region Cells | lower noise / raise Min Region Cells |
| Rooms clumped | Spread Candidates 1, Tight Packing Chance high | raise Spread Candidates, lower Tight Packing |
| Floors half empty | Coverage low, large rooms that don't fit | raise Coverage, lower Large/Hall weights |
| Maze with few rooms | high Prune Fraction, low complexity | lower Prune Fraction |
| "Floor N: the arrival cell lost its area" (retry) | a custom strategy overwrote anchor cells | never write over `Fixed` areas or `Reserved` cells |
