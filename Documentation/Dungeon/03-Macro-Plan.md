# Dungeon 03 — Macro Plan and Anchors

**Scripts:** `Stages/Planning/MacroPlanStage.cs` (`MacroPlanStage`, `AnchorPlanner`), `Core/DungeonElements.cs`
(`FloorSpec`, `Anchor`, `VerticalLink`).

---

## 1. Concept

Before any room exists, stage 1 fixes everything that **more than one floor** must agree on:

- how many floors there are, and each floor's size, position and character (style, openness, complexity, difficulty);
- the **anchors**: the entrance room, the exit room, the stair wells with a landing room at each end, and the drop
  shafts.

Because the anchors are fixed first, each floor can then be laid out **on its own, in parallel**, and still line up
with the floors above and below. A stair well occupies the same cells on both floors it joins.

## 2. Macro flowchart

```mermaid
flowchart TD
    A["MacroPlanStage.Run<br/>stream 'MacroPlan'"] --> B["size class ← request.size"]
    B --> C["floorCount = request.floorCount<br/>or random in size range<br/>or profile Floor Count (clamped 1..64)"]
    C --> D["minSide = StairLength + 2·landing max<br/>+ 2·Edge Margin + 2"]
    D --> E["shared grid W×H = base·(1 + variation) + 2·pad (pad 2)"]
    E --> F["for each floor f"]
    F --> G["footprint: size base·(1 ± variation),<br/>random position inside the grid"]
    G --> H["Openness = lerp(range, rand)<br/>Complexity = lerp(range, rand)"]
    H --> I["Difficulty = request.difficulty ×<br/>(1 + Difficulty Per Floor·f + 0.25·depth)"]
    I --> J["Style = request style<br/>or PickStyle(f, previous)"]
    J --> K["BaseY = −f · Floor Spacing<br/>cells outside the footprint → Reserved"]
    K --> F
    F --> L["AnchorPlanner.Plan()"]
```

### Style choice (micro)

```mermaid
flowchart LR
    A["weight(style) = profile Styles weight"] --> B{"Caverns or Hybrid<br/>and weight > 0?"}
    B -- yes --> C["+ min(Natural Weight Per Floor × f, 1.2)<br/>(deeper = more natural)"]
    B -- no --> D["unchanged"]
    C --> E{"same as previous floor?"}
    D --> E
    E -- yes --> F["× Repeat Style Penalty (0.45)"]
    E -- no --> G["unchanged"]
    F --> H["WeightedIndex over the 5 styles"]
    G --> H
```

Default weights: Rooms 1, BSP 0.6, Caverns 0.8, Hybrid 0.9, Grid Maze 0.15. On floor 3 the Caverns weight becomes
0.8 + 0.75 = 1.55, so deeper floors lean towards caves.

## 3. Anchor planning

```mermaid
flowchart TD
    A["AnchorPlanner.Plan"] --> B["Entrance room on floor 0<br/>(size 6-8, pushed against a random footprint edge)"]
    B -- none fits --> FAIL["ctx.Fail → retry"]
    B --> C["arrival = entrance centre"]
    C --> D["for each floor pair (f, f+1)"]
    D --> E["main stairs: PlaceStairs(f, arrival, main)"]
    E -- none fits --> FAIL
    E --> F["floor f departure = upper landing<br/>floor f+1 arrival = lower landing"]
    F --> G["extra stairs: up to Max Extra Stairs,<br/>each with Extra Stair Chance"]
    G --> H["drop: with Drop Chance → PlaceDrop(f)"]
    H --> I["arrival = main stairs lower landing"]
    I --> D
    D --> J["Exit room on the last floor (size 7-9),<br/>far from the arrival (Departure Distance)"]
    J -- none fits --> FAIL
    J --> K["last floor departure = exit centre"]
```

### Stairs (micro)

```mermaid
flowchart TD
    A["PlaceStairs(upper, arrival, main)"] --> B["shared = intersection of both footprints,<br/>shrunk by Edge Margin (4)"]
    B --> C["up to 220 attempts / 40 options"]
    C --> D["random descent direction N/E/S/W<br/>well = Stair Width × StairLength (2 × 11)"]
    D --> E["landing rooms: depth = Landing Size (4-6),<br/>width ≥ well width + 2, offset sideways"]
    E --> F{"well fits on both floors<br/>and each landing room fits<br/>(2 cells inside footprint,<br/>Anchor Spacing from others)?"}
    F -- no --> C
    F -- yes --> G["option, score = distance(upper landing, arrival)<br/>(extra stairs: random)"]
    G --> C
    C --> H["main: PickFar(scores, Departure Distance 0.6-1)<br/>extra: random option"]
    H --> I["VerticalLink Stairs + anchors:<br/>StairsDownLanding on upper, StairsUpLanding on lower"]
```

**PickFar** picks the option whose score is closest to a random share (60–100 %) of the best score. The way down is
usually far from the way in, but not always the single farthest spot, which would be predictable.

**Why the well is 11 cells long:** the stairs climb one *Floor Spacing* (10 m) at most *Max Stair Slope* (33°):
10 / tan 33° = 15.4 m = 11 cells of 1.5 m. A character controller and a NavMesh agent can both climb that.

### Drops (micro)

```mermaid
flowchart TD
    A["PlaceDrop(upper)"] --> B["pit = Drop Size² (2×2)<br/>ring = 2-3 cells around it"]
    B --> C["room = pit + ring, same rect on both floors"]
    C --> D{"fits on both floors?"}
    D -- "no (60 attempts)" --> X["no drop"]
    D -- yes --> E["VerticalLink Drop (one way)<br/>DropSource anchor on upper (landing beside the pit)<br/>DropLanding anchor on lower (landing under the pit,<br/>NoCeiling over the pit)"]
```

Drops are shortcuts **down** only. In the roles graph they cost 4 and are one-way (see [06](06-Roles-and-Templates.md)).

## 4. What the stage produces

| Output | Used by |
|---|---|
| `layout.Width/Height` (shared grid) | every floor's `TileGrid` |
| `FloorSpec`: footprint, style, openness, complexity, difficulty, BaseY, IsFirst/IsLast, hybrid weights | layout, connectivity, roles, population, build |
| `Anchor`s per floor: Entrance, Exit, StairsDownLanding, StairsUpLanding, DropSource, DropLanding (room rect, landing cell, facing, shaft) | Layout (turned into fixed areas) |
| `VerticalLink`s: stairs and drops, `OnMainPath` for the main stairs | roles graph, meshing (`LinkMesher`), NavMeshLinks |
| `ArrivalCell` / `DepartureCell` per floor | layout (arrival/departure areas), analysis (walking distance) |
| `Reserved` cells outside each footprint | every layout (never carved) |

## 5. Configuration

| Setting | Default | Effect |
|---|---|---|
| Floor Count | 2–4 | used by size classes with (0, 0) floor count (Medium) |
| Size Classes | see [02](02-Request-Profile-Seed.md) | floor cells and floor counts |
| Footprint Variation | 0.15 | floors differ in size and position, so they don't stack as identical squares |
| Openness / Complexity | 0.3–0.75 each | rolled per floor; drive almost every later stage |
| Styles, Natural Weight Per Floor, Repeat Style Penalty | 1/0.6/0.8/0.9/0.15, 0.25, 0.45 | style mix |
| Difficulty Per Floor | 0.2 | +20 % per floor |
| Links › Stair Width | 2 | well width (cells) |
| Links › Landing Size | 4–6 | landing room depth/width |
| Links › Departure Distance | 0.6–1 | how far the way down/exit is from the way in (share of the farthest option) |
| Links › Extra Stair Chance / Max Extra Stairs | 0.35 / 1 | alternative routes between floors |
| Links › Drop Chance / Drop Size | 0.3 / 2 | one-way shortcuts |
| Links › Edge Margin | 4 | shafts keep this far from the footprint edge |
| Links › Anchor Spacing | 3 | cells between anchor rooms |
| Links › Entrance Size / Exit Size | 6–8 / 7–9 | entrance and exit room sides |
| Cell Size, Floor Spacing, Max Stair Slope | 1.5, 10, 33° | set the stair length |

## 6. Example (Medium, defaults)

- `StairLength` = 11, landing max = 6, Edge Margin = 4 → `minSide` = 11 + 12 + 8 + 2 = **33 cells**.
- Base 64 × 64 → grid = ceil(64 × 1.15) + 4 = **78 × 78** (shared by all floors).
- Each floor: 64 × (1 ± 0.15) → 54–74 cells a side, placed randomly in the grid.
- 3 floors → 2 main stairs, 0–2 extra stairs, 0–2 drops, 1 entrance room (floor 0), 1 exit room (floor 2).

## 7. Performance

Tiny: a few hundred rectangle tests per floor pair. The stage is sequential (anchors of floor *f+1* depend on floor *f*).

## 8. Debugging

| Symptom | Cause | Fix |
|---|---|---|
| Report: "No room for stairs between floors …" (then a retry) | footprints overlap too little after the Edge Margin; very small size class; large Landing Size | lower Footprint Variation or Edge Margin; bigger size class |
| Report: "No room for the entrance / exit" | footprint too small for Entrance/Exit Size plus margins | smaller Entrance/Exit Size or larger floors |
| Always the same style | *Override Style* on the request, or other style weights 0 | check the request and Styles |
| Stairs land right next to the entrance | Departure Distance min too low | raise it (e.g. 0.8–1) |
