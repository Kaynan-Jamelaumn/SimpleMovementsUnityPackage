# Dungeon 14 — Glossary, Implementation Status and Potential Future Improvements

## 1. Glossary

| Term | Meaning (in this project) |
|---|---|
| **Cell** | one square of a floor's grid (1.5 m); solid, floor, door or link |
| **2.5D grid** | a flat grid where each open cell stores its own floor and ceiling height |
| **Floor** | one level of the dungeon (`FloorLayout`), `Floor Spacing` below the previous one |
| **Footprint** | the rectangle of the shared grid a floor may use |
| **Area** | a room, hall, cave chamber or landing (`Area`) |
| **Anchor** | a room fixed before layout: entrance, exit, stair landings, drop rooms |
| **Vertical link** | stairs (two-way) or a drop (one-way pit) between two floors |
| **Landing** | the cell where a stair or drop arrives |
| **Connection** | a link between two areas on one floor: door, corridor, tunnel, opening, breach, secret |
| **Style** | how a floor is laid out: Rooms, BSP, Caverns, Hybrid, Grid Maze |
| **Zone style** | Built, Cavern or Ruins — per area (drives meshes, props, connections) |
| **Openness / complexity** | per-floor 0–1 rolls: big rooms and loops / dead ends and branching |
| **BSP** | binary space partition: recursive splitting of a rectangle |
| **Cellular automaton** | grid rule applied repeatedly (here: rock if ≥ 5 rock neighbours) to grow cave shapes |
| **Gabriel graph** | graph linking two points when no third point is inside the circle on their segment |
| **Spanning tree** | the fewest links that connect every area (Kruskal's algorithm + union-find) |
| **Loop** | an extra connection creating a second route |
| **Dead end / leaf** | an area with only one connection |
| **Hub** | an area with three or more connections |
| **A\*** | best-first path search with a distance estimate; routes corridors |
| **Turn penalty** | extra A* cost for changing direction (straight corridors) |
| **Main path** | cheapest walk from the entrance to the exit across all floors |
| **Progress** | 0–1 position along the main path (branches inherit it) |
| **Role** | an area's purpose: Boss, Treasure, Rest, Arena, Shrine, Secret, Custom (+ anchor roles) |
| **Room template** | an authored room: a text plan or a prefab with door sockets |
| **Socket** | a template cell where corridors may attach |
| **Distance transform (EDT)** | per-cell distance to the nearest wall |
| **Chokepoint** | a one-cell passage or a door |
| **Safe zone** | cells within *Safe Radius* walking distance of the arrival (no mobs) |
| **Encounter budget** | mob cost units per area (density × size × difficulty × role) |
| **Pack** | mobs placed together with a shared group id |
| **Tier** | loot/mob level handed to gameplay |
| **Placement** | a data record of something to spawn (kind, cell, height, yaw, tier…) |
| **Marching squares** | contouring an open/solid grid with diagonal cuts (cave walls) |
| **Tile kit** | modular floor/wall/ceiling prefabs used instead of generated meshes |
| **NavMeshLink** | a navigation connection between two NavMesh points (stairs, drops) |
| **Attempt / retry** | one run of all stages; failures retry with a derived seed |
| **Repair** | a corridor carved to an unreachable area during validation |
| **Compiled profile** | a thread-safe snapshot of the profile used by the worker |
| **World pause** | disabling world streaming, weather and spawners while inside |

## 2. Implementation status summary

| Area | Implemented | Partially implemented | Placeholder | Not currently implemented |
|---|---|---|---|---|
| Planning | floors, footprints, styles, difficulty, stairs, extra stairs, drops, entrance/exit | — | — | per-floor size classes |
| Layout | Rooms, BSP, Caverns, Hybrid, Grid Maze, 8 room shapes, pluggable strategies | — | — | multi-level areas (balconies, bridges) |
| Connectivity | Gabriel + Kruskal, loops, dead-end budget, 6 connection kinds, A* router, secret passages | — | — | locked doors / keys (possible as a custom stage) |
| Roles | main path, progress, difficulty, 6 default rules, custom roles/tags, templates, relaxation | — | — | — |
| Heights | cave relief, slope limit, domed cave ceilings, built ceilings | — | — | ramps inside built rooms |
| Validation | reachability with repairs, doors, landings, sizes, retries | — | — | — |
| Population | portals, spawn, bosses, budgets, packs, corridor wanderers, loot tiers, 9 prop placements, defaults | — | — | patrol routes, encounter scripting |
| Meshing/Build | blocky + marching squares, stairs, drops, tile kit, prefab rooms, doors, secret doors, job-thread colliders, NavMesh, pooling, primitives | Mark Static (editor builds only) | — | LOD for dungeon meshes, occlusion culling data |
| Runtime | session, world pause, atmosphere, floor streaming, respawns, hazards, secret doors, next-dungeon chains | hazards report damage (your health system applies it) | — | saving dungeon state, multiplayer |
| Tools | Preview (6 views), batch test, setup menus, portal validation, inspectors, tests | tests need the Test Framework | — | — |

