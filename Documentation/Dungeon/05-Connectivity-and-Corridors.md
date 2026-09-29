# Dungeon 05 — Connectivity and Corridors

**Scripts:** `Stages/Connect/ConnectivityStage.cs` (stage 3: *which* areas connect),
`Stages/Carve/CorridorRouter.cs` (stage 5: *how* each connection is carved), `Common/GraphBuilders.cs`,
`Common/UnionFind.cs`, `Common/GridPathfinder.cs`, `Common/BinaryHeap.cs`.

---

## 1. Concept

Connecting rooms happens in two steps, several stages apart:

1. **Decide the graph** (stage 3): which pairs of areas are joined, of which kind and width. Nothing is carved yet.
   Stage 4 (roles) needs this graph to find the main path and the dead ends.
2. **Carve** (stage 5): each connection is routed through the rock with A* and becomes corridor or tunnel cells,
   with doors at the ends.

A good dungeon graph is **connected** (every room reachable), mostly **tree-like** (so there are dead ends worth
exploring), and has some **loops** (so there is more than one way around). The stage builds exactly that.

## 2. Macro flowchart — deciding the graph (per floor)

```mermaid
flowchart TD
    A["ConnectivityStage, stream 'Connectivity' + floor"] --> B["1 Forced: maze edges (PresetConnections) → Corridor<br/>1b Forced: areas already touching → Opening<br/>(width = number of touching cell pairs)"]
    B --> C["2 Candidates: Gabriel graph of area centres<br/>cost = max(1, distance − r_A − r_B),<br/>r = √(cells / π)"]
    C --> D["sort candidates by cost"]
    D --> E["3 Spanning tree (Kruskal + UnionFind):<br/>pass 0 only cost ≤ Max Connection Length (60),<br/>pass 1 any cost"]
    E --> F{"still several components?"}
    F -- yes --> G["ConnectComponents:<br/>join nearest centres (safety net)"]
    F -- no --> H["4 Loops"]
    G --> H
    H --> I["5 Dead-end budget"]
    I --> J["6 Kind and width per connection"]
```

### Gabriel graph (micro)

Two area centres A and B are linked when **no third centre lies inside the circle whose diameter is AB**. The Gabriel
graph always contains the minimum spanning tree, so it is connected. It never has long skinny edges that pass right
by another room, so every candidate is a "sensible neighbour". `GraphBuilders.Gabriel` checks the O(n²) pairs against
a spatial hash.

### Loops (micro)

```mermaid
flowchart TD
    A["unused candidates, shuffled"] --> B{"cost ≤ Max Connection Length?"}
    B -- no --> SKIP["skip"]
    B -- yes --> C["hops = BFS distance A → B<br/>over current connections (limit 12)"]
    C --> D{"hops ≥ 3?"}
    D -- no --> SKIP
    D -- yes --> E["value = clamp01((hops − 1) / 4)<br/>(3 hops → 0.5, ≥ 5 hops → 1)"]
    E --> F{"rand < Loop Chance.Lerp(openness) × value?"}
    F -- no --> SKIP
    F -- yes --> G["add Corridor, IsLoop"]
```

A loop is only worth adding if it **shortcuts a long walk**. Joining two rooms already one or two doors apart adds
nothing, so those are never picked.

### Dead-end budget (micro)

```mermaid
flowchart TD
    A["budget = round(n × Dead End Share.Lerp(complexity)<br/>× lerp(1.2, 0.7, openness))"] --> B["leaves = areas with degree ≤ 1<br/>(not Entrance / Exit), shuffled"]
    B --> C{"leaves − budget > 0?"}
    C -- no --> END["done"]
    C -- yes --> D["for a leaf: cheapest unused candidate touching it<br/>with cost ≤ 1.2 × Max Connection Length"]
    D --> E["add it as a loop → the leaf is no longer a dead end"]
    E --> C
```

Complex floors keep more dead ends, and open floors fewer.

### Kinds and widths (micro)

```mermaid
flowchart TD
    A["connection A-B (not Opening)"] --> B{"both caverns?"}
    B -- yes --> T["Tunnel<br/>width = Tunnel Width (2-3)"]
    B -- no --> C{"one cavern?"}
    C -- yes --> R["Breach<br/>width = max(2, Tunnel Width − 1)"]
    C -- no --> D["Corridor<br/>wide with Wide Corridor Chance × openness × 2<br/>→ max Corridor Width, else min"]
    D --> E{"a non-forced loop and<br/>rand < Secret Chance (0.12)?"}
    E -- yes --> S["Secret, width 1"]
    E -- no --> K["Corridor"]
```

| Kind | Meaning | Carved as |
|---|---|---|
| **Door** | rooms one wall apart | a corridor of exactly one cell becomes a door straight through the wall |
| **Corridor** | constructed passage | straight-ish, doors at both built ends |
| **Tunnel** | natural passage between caverns | winding, variable width, no doors |
| **Opening** | areas already touching | nothing to carve |
| **Breach** | rough passage between a cave and built structure | winding, rubble where it meets built walls |
| **Secret** | a loop corridor behind a secret door | width 1, the ordinary end is a hidden door |

Stage 4 can also turn corridors into **Secret** when they lead into a role area with *Secret Entrance* (the Secret
room). See [06](06-Roles-and-Templates.md).

## 3. Carving — the corridor router

### Macro flowchart (per floor, inside the Carve stage)

```mermaid
flowchart TD
    A["connections except Openings"] --> B["order: tree connections first,<br/>then by estimated cost, then id"]
    B --> C["CorridorRouter.Route(c)"]
    C --> D["sources = entry cells of A<br/>targets = entry cells of B<br/>(template rooms: socket cells only)"]
    D --> E["GridPathfinder A*<br/>turn penalty: 2.5 built / 0.15 natural<br/>heuristic = minStep × Manhattan distance to B's bounds<br/>max 400 000 expanded nodes"]
    E -- "no path" --> W["c.Failed, warning in the report"]
    E -- path --> F["Carve: open Solid cells outside A and B<br/>(Floor + Corridor flag, Organic if natural)"]
    F --> G{"no outside cells?"}
    G -- yes --> O["kind → Opening"]
    G -- no --> H["Breach: Rubble next to built areas"]
    H --> I["constructed (Corridor/Door/Secret):<br/>1 outside cell → Door kind<br/>doors on the first and last cell<br/>next to built, non-prefab rooms"]
    I --> J["Secret: the end opening into the ordinary area<br/>becomes a hidden door (Secret flag)"]
    J --> K["Width ≥ 2: widen sideways<br/>(natural: noise-varied width)"]
```

### Step cost (micro)

The A* cost of stepping into a cell decides the corridor's character:

```mermaid
flowchart TD
    A["StepCost(from → to)"] --> B{"to is in an area?"}
    B -- "area A (own start)" --> Z0["0"]
    B -- "a target cell of B" --> T1{"socket needs a<br/>specific direction?"}
    T1 -- "wrong direction" --> X["blocked"]
    T1 -- ok --> Z1["1"]
    B -- "another area" --> O1{"tunnel through<br/>another cavern?"}
    O1 -- yes --> Z08["0.8 (it joins it too)"]
    O1 -- no --> X
    B -- "not in an area" --> C{"cell type"}
    C -- "Link (shaft)" --> X
    C -- "Floor / Door (existing corridor)" --> RU["reuse: 0.35 built / 0.6 natural"]
    C -- Solid --> D{"carvable (not Reserved)?"}
    D -- no --> X
    D -- yes --> E["check 4 neighbours"]
    E --> E1{"neighbour is a shaft?"}
    E1 -- yes --> X
    E1 -- no --> E2{"neighbour is an unrelated room?"}
    E2 -- "yes, built" --> X
    E2 -- "yes, cavern and this is a tunnel" --> E3["+0.75 (brushing)"]
    E2 -- no --> F["cost = 1 + extras"]
    E3 --> F
    F --> G{"runs along A or B's wall<br/>(not just leaving it)?"}
    G -- yes --> H["+3 (wall hug penalty)"]
    G -- no --> I["unchanged"]
    H --> J{"natural?"}
    I --> J
    J -- yes --> K["× (0.3 + noise² × 2.4 × Tunnel Winding)"]
    J -- no --> L["final cost"]
    K --> L
```

What each rule achieves:

| Rule | Effect |
|---|---|
| Turn penalty 2.5 (built) | straight corridors with few bends; tunnels (0.15) wander freely |
| Reuse cost 0.35 | corridors merge into existing ones, giving junctions and fewer parallel passages |
| Unrelated rooms blocked | a corridor opens **only** into the two areas it joins — no accidental doors |
| Shafts blocked (and their neighbours) | stair wells and drop shafts stay sealed except at their landings |
| Wall hug +3 | corridors don't scrape along a room's wall before entering it |
| Template sockets | template rooms are entered only through their door sockets, straight on |
| Noise-weighted cost | tunnels meander around "hard rock" patches (noise at scale 0.14) |
| 400 000-node cap | a hopeless search stops instead of freezing the worker |

### Incidental openings

A tunnel may brush past a chamber it doesn't join. That is a real way through, so after routing,
`RecordIncidentalOpenings` adds an **Opening** connection for each such contact. The area graph (dead ends, hubs,
paths) then matches the carved floor.

## 4. Configuration

| Setting | Default | Effect | Performance |
|---|---|---|---|
| Loop Chance | 0.08–0.4 (lerped by openness) | more loops = more alternative routes | more corridors to route |
| Dead End Share | 0.15–0.4 (lerped by complexity) | share of areas allowed to be dead ends | — |
| Corridor Width | 1–2 | min = normal width, max = "wide" | wider = more cells |
| Wide Corridor Chance | 0.4 | × openness × 2 | — |
| Turn Penalty | 2.5 | higher = straighter corridors | higher can expand more A* nodes |
| Reuse Cost | 0.35 (0.05–1) | lower = more merging into existing corridors | — |
| Max Connection Length | 60 | longest candidate used first for the tree and loops | — |
| Secret Chance | 0.12 | loops turned into secret passages | — |
| Caves › Tunnel Width / Tunnel Winding | 2–3 / 0.9 | tunnel width and wander | winding expands more nodes |

## 5. Example

Three rooms in a row, A–B–C, and a fourth room D above B. Gabriel candidates: A-B, B-C, B-D, A-D, C-D.

1. Tree (cheapest first): A-B, B-C, B-D. Everything is connected.
2. Loops: A-D has hops = 2 (A-B-D), fewer than 3, so it is skipped. The same goes for C-D.
3. Dead ends: A, C and D are leaves. With n = 4, complexity 0.5 and openness 0.5, budget = round(4 × 0.275 × 0.95) = 1,
   so 2 leaves get an extra link: A-D and C-D are added as loops.
4. Kinds: all built, so Corridor. Each loop has a 12 % chance to become Secret.

## 6. Performance

Graph building is tiny (tens of areas per floor). Routing costs one A* per connection over the floor grid. The
pathfinder reuses its buffers through a generation stamp, so there are no per-search allocations of grid-sized arrays.
Floors route in parallel.

## 7. Debugging

| Symptom | Cause | Fix |
|---|---|---|
| Report: "could not route …" | the target is walled in by reserved cells or other rooms; the socket side is blocked | usually harmless (validation repairs reachability); check templates' socket sides |
| Long parallel corridors | Reuse Cost high | lower it |
| Corridors zig-zag | Turn Penalty low | raise it |
| No loops at all | Loop Chance low; small floors (few candidates with ≥ 3 hops) | raise Loop Chance / openness |
| Too many dead ends / too few | Dead End Share | adjust it |
| Secret doors in odd places | the hidden end is the one facing the ordinary area | expected; lower Secret Chance to reduce them |
