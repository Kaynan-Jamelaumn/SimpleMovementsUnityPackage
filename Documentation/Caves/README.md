# Caves — Exploration and Mining

Natural cave entrances in the open world lead into procedurally generated caves to explore and mine. Caves have narrow
winding tunnels, huge chambers, dead ends and branches. Standing rock formations, stalactites and rubble fill them.
Rich ore veins, glowing crystal grottos and guarded hoards are hidden among them. Creatures grow tougher the deeper you
go. Deposits of every mineable material of the Crafting Database are mined with a pickaxe into the inventory and used by
crafting. A cave keeps what you did in it, so mined seams stay mined and grow back slowly, slain creatures stay dead for
a while, and emptied chests stay empty when you come back.

The cave system is built **on** the existing systems and does not replace them:

| It uses | For |
|---|---|
| The dungeon generator (`Procedural/Dungeon/`) | Every cave *is* a dungeon: its natural cavern layouts, tunnels, floors and drops, connectivity guarantees, validation, population (creatures, loot, props), meshing, NavMesh, streaming, the session and its portals, networking |
| `Portal`, `DungeonSession`, `DungeonPortal` | Going in and out: groups and parties, shared runs, player limits, the re-entry cooldown, dungeon scenes |
| `OreDeposit` (Crafting) | Mining: pickaxe hits (`ToolType.PickAxe`, Tool Damage), loose stones by hand, lumps that keep their seam, gem by-products, regrowth |
| The Crafting Database | What a deposit gives: the stock items of its ores, coal, stones, clays, salt and gemstones (`CraftItems.Stock`) |
| The inventory, combat, AI and mob prefabs | Unchanged: the world's own mobs (imported into the cave's encounter table) fight in caves, made tougher with depth through their `CombatStats` |
| `EndlessTerrain` chunk spawners, `FlatSpots`, `LoadedTerrain` | Where cave mouths appear and how they sit on the ground |
| `WorldStore` | Saving each cave's state with the world |

## Quick start

1. **Tools ▸ SimpleMovements ▸ Caves ▸ Create Cave Setup…** and pick a folder. This creates a cave Dungeon Profile, its
   Cave Mining Stage, a Resource Table filled from the Crafting Database, a theme, loot, props, an encounter table with
   the open scene's world mobs (if there is an `EndlessTerrain`) and a Cave Dungeon Manager prefab. It then offers to
   put cave entrances in the world.
2. Answer **Yes**: the profile and the manager go into **EndlessTerrain ▸ Cave Entrances**. Press Play, find a cave mouth
   (an outcrop with a dark opening, facing downhill) and walk in.
3. Mine with a pickaxe (or gather the loose stones by hand), fight, go deeper, walk back out through the passage of
   daylight. Then come back: the cave remembers.

Or place a mouth by hand: **Tools ▸ SimpleMovements ▸ Caves ▸ Add Cave Entrance Here** puts one where the Scene view
looks, opening towards the camera.

**Tools ▸ SimpleMovements ▸ Caves ▸ Validate Cave Setup** checks the profiles, the resource ids, the depth coverage, the
entrances in the open scenes and the terrain's settings.

## Chapters

| # | Chapter | Covers |
|---|---|---|
| 01 | [Overview and Setup](01-Overview-and-Setup.md) | what is created, the full loop step by step, hand-placed entrances, existing projects, menus |
| 02 | [Generation, Chambers and Formations](02-Generation-Chambers-Formations.md) | the cave profile, the Cave Mining Stage in the pipeline, depth, rich veins, crystal grottos, guarded hoards, deposits, formations, the never-blocks guarantee |
| 03 | [Resources, Mining and Crafting](03-Resources-Mining-Crafting.md) | the resource table, depth bands, rarity, the 38 built-in deposits, mining with a pickaxe, inventory and crafting, adding materials |
| 04 | [Creatures and Depth](04-Creatures-and-Depth.md) | encounters, Deep-Dweller toughness, guarded veins, deeper caves |
| 05 | [Entrances, Transitions and Persistence](05-Entrances-Transitions-Persistence.md) | cave mouths in the world, the passages inside, the fade, revisits and what a cave remembers |
| 06 | [Performance, Networking and Extending](06-Performance-Networking-Extending.md) | threads, pooling, batching, kept generations, multiplayer, the build-extension API |
| 07 | [Testing and Troubleshooting](07-Testing-and-Troubleshooting.md) | Edit Mode tests, the validator, verification results, symptoms and fixes |

## Find the answer to…

| Question | Read |
|---|---|
| How do I get caves into my world? | Quick start above, 01 §2 |
| Where do cave entrances appear, and how many? | 05 §1 |
| What decides which ores a place holds? | 02 §3, 03 §2 |
| How do I make an ore rarer, deeper or more common? | 03 §2 — the Resource Table |
| How do I add a new mineable material? | 03 §6 |
| Why are some seams too hard for my pickaxe? | 03 §4 (*Min Pick*) |
| How do creatures get tougher deeper down? | 04 §2 |
| Can rocks or ore ever block a tunnel? | 02 §6 — no, and why |
| What is kept when I leave and come back? | 05 §4 |
| Does it work in multiplayer? | 06 §3 |
| How do I build something of my own from a custom stage? | 06 §4 |
| Something doesn't work | 07 §3 |

## Scripts

| Folder | Contents |
|---|---|
| `Procedural/Caves/Config/` | `CaveMiningStage` (the stage asset), `CaveSettings` (depth, deposits, chambers, formations, creatures, persistence, passages), `CaveResourceTable`, `CaveDefaults` (built-in resources, the cave profile and theme, cave names) |
| `Procedural/Caves/Generation/` | Plain data, worker threads: `CaveRules`, `CaveMiningPlanner`, `CaveFormationMesher`, `CaveRockMesh` and `CaveWalls` (seeded rock shapes, the mesher's wall rule) |
| `Procedural/Caves/Runtime/` | `CaveBuildSession` (builds a cave's part of the dungeon), `CaveInstance`, `CaveDeposit`, `CaveDepositArt`, `CaveDeepDweller`, `CaveStore`, `CaveEntrance`, `CavePassage`, `CaveTransition`, `CaveMouthBuilder`, `CaveMaterials` |
| `Procedural/Caves/World/` | `CaveEntranceSettings`, `CaveSitePlanner`, `CaveEntranceSpawner` (one per terrain chunk) |
| `Procedural/Dungeon/Build/DungeonBuildExtensions.cs` | The generic build-extension API custom stages use (`IDungeonBuildExtension`, `IDungeonBuildSession`, `DungeonBuildContext`) |
| `Procedural/Dungeon/Runtime/DungeonGenerationCache.cs` | Kept generations: a dungeon entered again is not generated again |
| `CustomEditor/Caves/` | Setup menu, inspectors, Edit Mode tests |
