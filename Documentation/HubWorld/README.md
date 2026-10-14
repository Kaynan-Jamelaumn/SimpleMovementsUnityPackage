# Hub World — City, Dimensional Cycles, Zones, Dungeon Scenes and Duels

The game-world layer on top of the terrain, dungeons, combat and inventory: one **permanent hub city**, an **open world
regenerated with a new seed every cycle**, **dungeons in scenes of their own**, **safe and dangerous zones** around the
city, **visual-only abilities** in the city and **duels**. Scripts are in `Scripts/HubWorld/` (namespace `HubWorld`),
with small hooks in the combat core, the terrain and the dungeon runtime.

This is **stage 1** of the multiplayer plan: the complete loop runs locally, without networking, but every decision is
already made by one authority object (the `HubWorldDirector`) from plain rules, so the same code becomes the server in
the Fish-Net stages. With Fish-Net (stages 2 and 3), the server's director decides and every client mirrors it; see
[Networking 09 - Hub world sync](../Networking/09-Hub-World-Sync.md).

> **Lore.** The city jumps between dimensions to protect itself. With every jump the world outside changes. Anyone who
> fails to return in time is lost between dimensions.

## Chapters

| # | Chapter | Covers |
|---|---|---|
| 01 | [Architecture](01-Architecture.md) | realms and scenes, the director, travel between scenes, one scene per dungeon, own physics per dungeon, slots, authority split, multiplayer readiness |
| 02 | [Dimensional Cycle](02-Dimensional-Cycle.md) | the clock, warnings and sky effects, the jump, loot loss, Dimensional Disorientation, players in dungeons, seeds, modifiers |
| 03 | [Zones, PvP and Difficulty](03-Zones-PvP-Difficulty.md) | rings around the city, the PvP rule, dungeon difficulty and mob strength by distance, the city's levelled and kept-clear area, the HUD |
| 04 | [City Abilities and Duels](04-City-Abilities-and-Duels.md) | visual-only casts and their relay, the duel flow, arena, never-lethal duels, combat rule providers |
| 05 | [Setup and Configuration](05-Setup-and-Configuration.md) | step by step, every setting, editor menus, tests, troubleshooting |
| 06 | [Portal Rules, Zones, Player Limits and Linked Portals](06-Portal-Rules-Zones-Players-Links.md) | portal and mob types by zone, after-use rules, max players, shared and linked dungeons, how multiplayer terrain streams |

## Quick start

1. *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Hub World Settings (Resources)*.
2. Select your player prefab, then *Hub World ▸ Build City Scene…* — a City scene with walls, the City Gate, the spawn and
   the director (first in Build Settings).
3. Open the scene that has your `TerrainGenerator` / `EndlessTerrain` / `WeatherSystem` and run *Hub World ▸ Make Open
   Scene The World Scene* — it adds the exterior copy of the city, the Return Gate and the arrival point.
4. *Hub World ▸ Validate Setup*, open the City scene and press Play. Walk through the gate. To see a jump quickly, set
   *Cycle Minutes* to 3, or right-click the director ▸ *Skip To 70 Seconds Before The Jump*.

## Find the answer to…

| Question | Read |
|---|---|
| Which scene is loaded when? Why is the world unloaded when I go back to the city? | 01 §2–3 |
| How does a player move from one scene to another? | 01 §4 |
| Why are dungeons no longer under the world? What is a dungeon slot? | 01 §5 |
| Why do raycasts go through `ScenePhysics`? | 01 §6 |
| What exactly happens at the end of a cycle? | 02 §3 |
| What does a player lost between dimensions lose? | 02 §4 |
| What happens to players in a dungeon when the city jumps? | 02 §5 |
| How is the world seed chosen? Can two servers produce the same world? | 02 §6 |
| How do Frozen Dimension / Eclipse / Invaded World work? | 02 §7 |
| Where is PvP on? Why can't I hit someone standing in the safe zone? | 03 §2 |
| How hard is a dungeon? How strong are mobs? | 03 §3 |
| Why is there no tree inside the city walls in the world? | 03 §4 |
| Why do my abilities do nothing in the city? | 04 §1 |
| How do I duel someone? | 04 §2 |
| How do I add my own combat rule (a guild war, an arena)? | 04 §4 |
| What does every setting do? | 05 §2 |
| Something is wrong — where do I look? | 05 §5 |

## Changes to existing systems

| System | Change | Without the hub world |
|---|---|---|
| Combat (`CombatRelations`, `CombatEntity`, `AbilityCaster`, abilities, weapons) | Pluggable rules: `CombatRules` (PvP verdicts, a harm gate, visual-only casts) | Nothing changes until a provider is registered |
| Physics queries | Gameplay raycasts and overlaps go through `ScenePhysics`, which routes them to the physics scene owning the position | Same results (direct `Physics` calls when no region is registered) |
| Dungeons (`DungeonSession`) | Several runs at once (`DungeonRun`, one per group), optional scene per dungeon (`DungeonScenes`), exit handler | Classic mode by default: one run, built under the world, world paused — as before |
| Terrain | `TerrainGenerator.SeedOverride`, `WorldAreas` (levelled / kept-clear circles), `WorldSpawnHooks` (mob weight & strength, portal difficulty, dungeon type) | No effect until set |
| Abilities (`AbilityPresentation`) | Optional *Visual Only Effect* | Empty |
