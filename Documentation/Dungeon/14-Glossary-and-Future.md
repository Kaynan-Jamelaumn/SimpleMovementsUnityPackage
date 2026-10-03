# Dungeon 14 — Glossary, Implementation Status and Potential Future Improvements

## 1. Glossary

| Term | Meaning (in this project) |
|---|---|
| **Cell** | one square of a floor's grid (1.5 m); solid, floor, door or link |
| **2.5D grid** | a flat grid where each open cell stores its own floor and ceiling height |
| **Floor** | one level of the dungeon (`FloorLayout`), its style's spacing (plus a chasm above) below the previous one |
| **Effective floor spacing** | *Floor Spacing*, raised by *Auto Floor Spacing* until the tallest ceiling fits |
| **Headroom** | floor-to-ceiling clearance of a cell (at least *Min Headroom*) |
| **Vaulted ceiling** | a ceiling that rises towards the middle of a room like an arch |
| **Height Scale** | one multiplier for every ceiling in a profile |
| **Dungeon type** | a ready-made combination of styles, shapes, heights, roles, mechanics and theme (`DungeonType`) |
| **Special room** | a role with its own furniture, loot and often a mechanic (Guardian, Vault, Trap Gauntlet, Puzzle, Ambush, Library, Armory, Prison, Crypt, Laboratory, Garden, Throne, Nest, Cursed Altars, Kitchen, Gallery, Barracks, Pit Fight, Greenhouse, Wine Cellar, Map Room, Gas Chamber) |
| **Chasm** | solid cells flagged `Chasm` (islands, astral void): a pit with a bottom (negative floor height) and the Void surface; walkable chasm cells are **bridges** |
| **Jump** | a connection crossed without a carved path: a portal (teleport pads) or a moving platform; `FloorLayout.Flood` crosses them |
| **Spiral** | a `VerticalLink` of a tower dungeon: one flight of the continuous spiral staircase through the stair core |
| **Climb** | a shaft between two floors with vines round a giant root, climbable both ways |
| **Outdoor / Roofed** | undercity cells under the cavern's sky / inside buildings with a roof and the sky above it |
| **Area hint** | a layout's suggestion of a role for an area (the den's cavern → Boss), tried first |
| **Shifting wall** | a wall on a loop passage that the floor raises and sinks every few minutes, always keeping the floor connected |
| **Roamer** | an elite mini-boss that patrols between rooms |
| **Champion** | a pit fight's last, toughest opponent |
| **Floor modifier** | a whole-floor twist: Flooded, Molten, Overgrown, Darkness, Frozen |
| **Room event** | a room that locks its gates until its fight (or ambush waves) ends, or a pressure-plate puzzle |
| **Dormant placement** | something built hidden and revealed by its room's event (ambush waves, rewards) |
| **Elite** | a guardian/throne mob: the toughest allowed encounter, scaled up, tier +1 |
| **Barrier** | a gate, vault door or shortcut door that slides into the floor (`DungeonBarrier`) |
| **Key ring** | the keys a party has found in a dungeon, shared by all players |
| **Shortcut** | a loop door that only opens from its far side |
| **Citadel** | a floor style: a central keep, rings of rooms and corner towers |
| **Catacombs** | a floor style: a lattice of small chambers joined by galleries with burial niches |
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
| **Role** | an area's purpose: Boss, Treasure, Rest, Arena, Shrine, Secret, Custom, the special rooms (+ anchor roles) |
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
| Planning | floors, footprints, styles (13), last-floor style, difficulty, floor modifiers, spacing per style and chasm, stairs, extra stairs, drops, climbs, tower spiral planning, entrance/exit | — | — | per-floor size classes |
| Layout | Rooms, BSP, Caverns, Hybrid, Grid Maze, Citadel, Catacombs, Tower, Undercity, Hive, Islands, Den, Astral, 12 room shapes, pluggable strategies, preset links, area hints | — | — | — |
| Connectivity | Gabriel + Kruskal, loops, dead-end budget, 9 connection kinds (incl. bridges, portals, moving platforms), A* router, secret passages | — | — | — |
| Roles | main path, progress, difficulty, 6 classic + 22 special-room rules, hidden rooms, area hints, per-role ceiling height and vaults, custom roles/tags, templates, relaxation | — | — | — |
| Heights | cave relief, slope limit, domed cave ceilings, built ceilings, Height Scale, size bonus, vaulted ceilings, presets, auto floor spacing | — | — | ramps inside built rooms |
| Validation | reachability with repairs, doors, landings, sizes, retries | — | — | — |
| Population | portals, spawn, bosses, elites, budgets, packs, ambush waves, pit fights and champions, sleepers, roamers, corridor wanderers, loot tiers, special-room loot, 11 prop placements, modifier and area-tag props, defaults, locks with reachability checks, keys, puzzles, shortcuts, crossings, nests, altar rewards, levers, gas, shifting walls, tripwires | — | — | — |
| Meshing/Build | blocky + marching squares, stairs, drops, climbs, spirals, water surface, chasms and the void, roofs, tile kit, prefab rooms, doors, secret doors, barriers, job-thread colliders, NavMesh, pooling, primitives | Mark Static (editor builds only) | — | LOD for dungeon meshes, occlusion culling data |
| Runtime | session, world pause, atmosphere, floor modifier atmosphere, floor streaming, respawns, hazards (constant / cycling), blade and dart traps, swinging logs, tripwires, secret doors, room events, pit fights, gates, keys and vault doors, puzzles, shortcuts, shrines, rest points (checkpoint), chests, herbs, nests, gambling altars, breakables, levers, gas, map and map overlay, sleepers, roamers, shifting walls, chasm falls, moving platforms, teleporters, climbing, gravity flips, run stats, messages, next-dungeon chains | — | — | saving dungeon state, multiplayer synchronisation, mobs using teleporters / platforms / climbs |
| Tools | Preview (7 views incl. Ceilings), batch test, setup menus incl. Create Dungeon Type, height presets, Apply Dungeon Type, Add Special Rooms, Add Missing Built-in Props, portal validation, inspectors, tests | tests need the Test Framework | — | — |

