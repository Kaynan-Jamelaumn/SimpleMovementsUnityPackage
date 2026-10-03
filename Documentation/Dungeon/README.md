# Dungeon Generation — Technical Reference

A complete onboarding and reference guide to the procedural dungeons in `Scripts/Procedural/Dungeon/` (namespace
`ProceduralDungeon`), the shared helpers in `Scripts/Procedural/Common/`, and the world portal in
`Scripts/Essentials/Portal.cs`. It is written for a developer who has never seen the system.

Every chapter goes **Concept → Implementation → Data flow → Configuration → Example → Performance → Debugging**. The
**macro** flowchart (the whole trip from a world portal to a playable dungeon and back) is in
[01 Architecture](01-Architecture.md). The **micro** flowcharts (one per important algorithm) are in the chapter for
that stage.

## Chapters

| # | Chapter | Covers |
|---|---|---|
| 01 | [Architecture](01-Architecture.md) | layers, macro flowchart, data model, stage contract, determinism, master data flow, dependency graph, script map |
| 02 | [Request, Profile and Seeds](02-Request-Profile-Seed.md) | `DungeonRequest`, size classes, world-position seeds, attempt seeds, random streams, `CompiledProfile`, report |
| 03 | [Macro Plan and Anchors](03-Macro-Plan.md) | floor count, grid, footprints, openness/complexity, difficulty, style choice, floor heights per style, floor modifiers, stairs, drops, climbs, entrance and exit rooms |
| 04 | [Layout Styles](04-Layout-Styles.md) | anchor areas, Rooms (scatter), BSP, Caverns (cellular automaton and chambers), Hybrid zones, Grid Maze, Citadel, Catacombs, room shapes (Tower, Undercity, Hive, Islands, Den and Astral: chapter 16) |
| 05 | [Connectivity and Corridors](05-Connectivity-and-Corridors.md) | Gabriel graph, spanning tree, loops, dead ends, connection kinds, A* corridor router, doors, secret passages |
| 06 | [Roles and Templates](06-Roles-and-Templates.md) | dungeon graph, main path, progress, difficulty, role rules, relaxation, room templates |
| 07 | [Carve, Validate and Analyse](07-Carve-Validate-Analysis.md) | template fitting, routing order, floor heights, ceilings (presets, size bonus, vaults, auto floor spacing), validation and repair, distance fields, chokepoints |
| 08 | [Population](08-Population.md) | portals and player spawn, bosses, elites, encounter budgets, packs, ambush waves, loot tiers, special-room loot, props and placement rules |
| 09 | [Meshing and Build](09-Meshing-and-Build.md) | blocky and marching-squares meshing, stairs and drops, water, tile kit, colliders, NavMesh, barriers, mobs, materials, pooling |
| 10 | [Runtime, Session and Portals](10-Runtime-Session-Portals.md) | world portal → dungeon → world, world pause, atmosphere, floor streaming, respawns, hazards, secret doors, gameplay API |
| 11 | [Editor Tools and Testing](11-Editor-Tools-and-Testing.md) | Dungeon Preview, setup menus, portal validation, inspectors, tests, verification results |
| 12 | [Configuration Reference](12-Configuration-Reference.md) | every setting with ranges, effects, performance and status |
| 13 | [Debugging Guide](13-Debugging.md) | symptoms → causes → fixes |
| 14 | [Glossary, Status and Future](14-Glossary-and-Future.md) | glossary, implementation status, potential improvements |
| 15 | [Dungeon Types, Special Rooms, Mechanics](15-Types-Special-Rooms-Mechanics.md) | the 17 dungeon types, the 22 special rooms, locks, keys, room events, ambushes, pit fights, puzzles, shortcuts, nests, altars, levers, gas, sleepers, roamers, shifting walls, tripwires, swinging logs, the map, floor modifiers |
| 16 | [Towers, Cities, Hives, Chasms, Dens, the Void](16-Towers-Cities-Hives-Chasms.md) | the six newer floor styles, floor heights per style, the spiral staircase, outdoor streets and roofed buildings, chasms, bridges, moving platforms, teleporters, falls, gravity flips, climbing vines |

## Find the answer to…

| Question | Read |
|---|---|
| What happens when the player walks into a portal? | 01 §2, 10 §2 |
| Where does a dungeon's seed come from? Why does the same portal give the same dungeon? | 02 §3 |
| How are floors, stairs and drops planned? | 03 |
| How are rooms / caves / mazes laid out? | 04 |
| How are rooms joined? How does a corridor find its way? | 05 |
| How are the boss room, treasure rooms and secrets chosen? | 06 |
| How are floor and ceiling heights calculated? | 07 §3 |
| How do I make ceilings higher? | 07 §3, 12 §4b — profile inspector › *Ceiling Heights* (Tall / Cathedral) or *Height Scale* |
| How do I make a different kind of dungeon (crypt, fortress, flooded caves…)? | 15 §2 — profile inspector › *Dungeon Type › Apply*, or *Create Dungeon Type* menus |
| What special rooms exist and what do they contain? | 15 §3, 08 §6 |
| How do locked doors, keys, gates, puzzles and shortcuts work? | 15 §4–5 |
| What are flooded, molten, overgrown, dark and frozen floors? | 15 §6 |
| How do towers, the undercity, hives, islands, the den and the astral void work? | 16 |
| What happens when a player falls into a chasm? How do platforms, teleporters and climbing work? | 16 §6–7 |
| How do nests, cursed altars, levers, gas, sleeping barracks, roamers, shifting walls and tripwires work? | 15 §3–5 |
| How does the generator guarantee every room is reachable? | 07 §4 |
| How are mobs, loot and props placed? How are portals placed? | 08 |
| How does the grid become meshes, colliders and a NavMesh? | 09 |
| What runs on worker threads and what on the main thread? | 01 §6, 09 §7 |
| How do I read dungeon data from gameplay code? | 10 §7 |
| What does every setting do? | 12 |
| Something is wrong — where do I look? | 13 |

The terrain side of the portal (where world portals appear, their difficulty and closing rules) is documented in
[Terrain 15 — World Portals and Mobs](../Terrain/15-Portals-and-Mobs.md).

Diagrams are Mermaid code blocks: they render on GitHub, in VS Code (Markdown Preview Mermaid Support), in many
Markdown viewers, and in the documentation page that accompanies this package.
