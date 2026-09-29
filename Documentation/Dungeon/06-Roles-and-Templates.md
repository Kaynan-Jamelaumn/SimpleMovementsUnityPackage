# Dungeon 06 — Roles, Main Path and Room Templates

**Scripts:** `Stages/Roles/RolesStage.cs`, `Config/DungeonSettings.cs` (`RoleRule`), `Config/DungeonProfile.cs`
(`DefaultRoles`), `Config/RoomTemplate.cs`, `Stages/Layout/GridMazeLayout.cs` (`TemplateStamper` / `TemplateStamp`),
`Stages/Carve/CarveStage.cs` (`FitTemplates`).

---

## 1. Concept

After stage 3 the dungeon is a graph of anonymous areas. Stage 4 gives it **meaning**:

- **Main path:** the cheapest walk from the entrance (floor 0) to the exit (last floor), through stairs and drops.
- **Progress** (0 → 1): how far along that walk an area is. Areas on side branches inherit the progress of the
  main-path area they hang from.
- **Difficulty:** grows along the path.
- **Roles:** Boss, Treasure, Rest, Arena, Shrine, Secret or Custom, placed by designer rules such as "the boss at the
  end of the main path on the last floor" or "treasure in dead ends after 15 % progress".
- **Templates:** hand-made rooms (text plans or prefabs) fitted into the areas chosen for a role.

Later stages read these values. Population scales mobs and loot by difficulty and progress, and props target roles
and tags. Gameplay code can query them through `DungeonInstance`.

## 2. Macro flowchart

```mermaid
flowchart TD
    A["RolesStage (whole dungeon, not per floor)"] --> B["BuildGraph: one node per area on every floor"]
    B --> B1["edges: connection (both ways) cost 1 + 0.05 × estimated length<br/>stairs (both ways) cost 1<br/>drop (down only) cost 4"]
    B1 --> C["Entrance = floor 0 arrival area<br/>Exit = last floor departure area"]
    C --> D["Depth = BFS hops from the entrance<br/>(drops only downwards)"]
    D --> E["Main path = Dijkstra entrance → exit"]
    E -- "no path" --> F["ctx.Fail → retry"]
    E --> G["main-path areas: OnMainPath,<br/>Progress = k / (n − 1)<br/>connections and links on the path flagged"]
    G --> H["branches: BFS from all main-path areas,<br/>BranchDepth, Progress = origin's progress"]
    H --> I["IsLeaf = degree ≤ 1, IsHub = degree ≥ 3<br/>Difficulty = floor difficulty × (0.75 + 0.5 × progress)"]
    I --> J["AssignRoles"]
```

Drops are allowed on the main path but discouraged (cost 4 vs 1). The main path is usually the stairs, but a drop is
used when it saves a long walk.

## 3. Assigning roles (micro)

```mermaid
flowchart TD
    A["rules sorted: Required first"] --> B["for each rule, for each floor f"]
    B --> C{"floor allowed?<br/>Last Floor Only, Min/Max Floor,<br/>Max Total not reached"}
    C -- no --> B
    C -- yes --> D["count = Per Floor roll<br/>Required: at least 1<br/>optional: 0 unless rand < Chance<br/>capped by Max Total"]
    D --> E["Choose(floor, rule, relax 0)"]
    E --> G{"found?"}
    G -- "no and Required" --> R["relax 1: ignore progress<br/>relax 2: + ignore placement<br/>relax 3: + ignore style<br/>relax 4: + ignore min cells"]
    R --> G2{"found?"}
    G2 -- no --> FAIL["ctx.Fail → retry the dungeon"]
    G2 -- yes --> GV
    G -- "no, optional" --> B
    G -- yes --> GV["Give(area): Role, Tag<br/>Secret Entrance → its built corridors/doors become Secret<br/>role templates → pick one that fits (weighted)"]
    GV --> D
```

### Choosing the area (micro)

```mermaid
flowchart TD
    A["candidate = free area<br/>(no role, not an anchor room)"] --> B{"Placement = End Of Main Path?"}
    B -- yes --> C["the free main-path area with the<br/>highest progress (style and size permitting)"]
    B -- no --> D{"passes filters?<br/>progress in range · placement (Leaf / On / Off main path / Hub)<br/>· style · Min Cells"}
    D -- no --> X["skip"]
    D -- yes --> E["sizeScore from the area/average size ratio r:<br/>Large r² · Small 1/r² · Medium 1/(1 + |ln r|) · Any 1"]
    E --> F["score = weight × sizeScore × (0.6 + 0.8 × rand)<br/>× 1.5 if the rule has templates"]
    F --> G["keep the highest score"]
```

## 4. Default role rules

| Rule | Required | Per floor | Max total | Floors | Progress | Placement | Size | Min cells | Styles | Chance | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **Boss** | yes | 1 | 1 | last only | — | End Of Main Path | Large | 30 | any | — | the dungeon's climax |
| **Treasure** | — | 0–2 | — | any | 0.15–1 | Leaf (dead end) | — | — | any | 0.85 | rewards exploration |
| **Rest** | — | 0–1 | — | any | 0.3–0.85 | On Main Path | Small | — | any | 0.5 | no mobs (role multiplier 0) |
| **Arena** | — | 0–1 | — | any | 0.2–0.9 | On Main Path | Large | 50 | any | 0.45 | mob budget × 2.2 |
| **Shrine** | — | 0–1 | — | any | — | Off Main Path | — | — | Built, Ruins | 0.4 | altar prop |
| **Secret** | — | 0–1 | — | any | — | Leaf | Small | — | Built, Ruins | 0.3 | *Secret Entrance*: reached through a hidden door |

Anchor areas already carry roles from stage 2: Entrance, Exit, StairsUp, StairsDown, DropSource, DropLanding.

### Role rule fields

| Field | Meaning |
|---|---|
| Name, Role, Tag | what to assign; `Custom` roles use the name (or Tag) so props and code can target them |
| Required | must be placed (relaxes filters, otherwise fails the attempt) |
| Per Floor, Max Total | how many per floor / in the whole dungeon (−1 = no limit) |
| Min Floor, Max Floor, Last Floor Only | where it may appear |
| Progress | range along the main path (0 = entrance, 1 = exit) |
| Placement | Any, Leaf, On Main Path, Off Main Path, Hub, End Of Main Path |
| Size, Min Cells | Any/Small/Medium/Large preference and hard minimum |
| Styles | Built / Cavern / Ruins |
| Chance, Weight | chance per floor for optional rules; relative score |
| Templates | room templates to fit into the chosen area |
| Secret Entrance | built corridors into it become secret passages (only off the main path) |

## 5. Room templates

A `RoomTemplate` asset is an authored room, in one of two modes:

| Mode | How it's defined | Geometry |
|---|---|---|
| **Shape Mask** | text plan: `.` floor, `P` pillar, `D` door socket, `#`/space outside; north row first | generated like any room |
| **Prefab** | your prefab covering *Footprint* cells, with door *Sockets* (side + cell; optional child objects shown when a socket is used/unused), *Pivot* (Center / South-West / North-West corner), *Offset* | the prefab brings its own floor, walls and colliders |

Other fields: *Role* and *Tag* given to the area, *Weight*, *Styles*, *Ceiling Height* (0 = default), *Allow
Population* (mobs/loot/props inside), *Legacy Room Behaviour* (old 7 × 7 RoomBehaviour rooms with a door in the middle
of each side, opened with `RoomBehaviour.UpdateRoom`).

```mermaid
flowchart TD
    A["Templates come from:<br/>role rules (Templates) and Maze settings"] --> B["CompiledProfile: TryGetPlan → TemplateInfo<br/>(width, height, floor, pillars, sockets)<br/>invalid / prefab missing → warning, skipped"]
    B --> C{"used by"}
    C -- "Grid Maze" --> D["stage 2: TemplateStamper stamps the template<br/>as a whole block (area.TemplateApplied)"]
    C -- "a role" --> E["stage 4: Give() picks a template that fits<br/>the area's bounds and style (weighted)"]
    E --> F["stage 5: FitTemplates: try the centred position,<br/>else scan the bounds for a spot where every<br/>template floor cell is inside the area"]
    F -- "doesn't fit" --> G["warning, TemplateIndex = −1<br/>(the area stays procedural)"]
    F -- fits --> H["area cells → rock, TemplateStamp.Apply<br/>(template floor, pillars, sockets)"]
    D --> I["corridors enter only through sockets,<br/>straight on (router step cost)"]
    H --> I
    I --> J["build: Shape Mask → generated meshes<br/>Prefab → prefab placed at the footprint,<br/>socket children toggled, Prefab cells left empty by the mesher"]
```

## 6. Example

A 3-floor dungeon whose main path passes 14 areas: floor 0 has 5, floor 1 has 5 and floor 2 has 4.

- Area *k* on the path has progress k / 13. The 7th (k = 6) has 0.46.
- A dead-end room hanging off the 7th inherits progress 0.46. On floor 1 (difficulty 1.2) its difficulty is
  1.2 × (0.75 + 0.5 × 0.46) = 1.18.
- Boss: the last free main-path area on floor 2 with ≥ 30 cells, before the exit room.
- Treasure: up to 2 per floor, 85 % chance, in leaves with progress ≥ 0.15. Larger leaves score no better
  (size preference Any).

## 7. Performance

A graph of at most a few hundred nodes. Dijkstra and BFS are negligible.

## 8. Debugging

| Symptom | Cause | Fix |
|---|---|---|
| Report: "Required role 'Boss' has no suitable area…" (then retries) | no free area ≥ Min Cells even after relaxing; only anchor rooms on the last floor | lower Min Cells; larger size class; more room coverage |
| Report: "Required role … was not placed (check its floor limits)" | Min/Max Floor exclude every floor | fix the floor limits |
| No treasure rooms | few dead ends (open floors add loops) or Chance low | raise Dead End Share / Chance |
| Template never appears | template larger than typical areas; wrong styles; invalid plan | check the report warnings; use bigger size classes for the role |
| Secret room reachable without a secret door | it was on the main path (secret entrance is skipped there) or connected by a tunnel/breach | expected; Secret rule Styles default to Built/Ruins |
