# Simple Movements — Unity Gameplay Package

A collection of gameplay systems for a third/first-person action RPG in Unity: a player controller with stamina,
survival stats, classes and traits; mobs with a full AI (senses, temperament, combat styles, abilities); an inventory
with items, armor sets and data-driven weapons; a shared ability and combat system with teams, factions and parties;
and procedural generation of an endless open world and multi-floor dungeons.

Everything lives in `Assets/Scripts/` and is configured with components and ScriptableObject assets — new items,
weapons, mobs and abilities need no code.

## Systems

| System | What you get | Documentation |
|---|---|---|
| **Player** | Movement state machine (walk, sprint, crouch, jump with buffering, dash, roll), stun/death, camera, 11 status managers (health, stamina, mana, hunger, thirst, sleep, sanity, body heat, oxygen, weight, speed), classes, traits, experience, ability keys | [Player guide](Documentation/Player/README.md) |
| **Mobs** | Mob AI: perception (sight, hearing, memory), temperament, territories, patrols, packs that call for help, combat styles (melee, ranged, kiting, brute), dodging, fleeing, abilities, animation, spawning | [Mob guide](Documentation/Mobs/README.md) |
| **Inventory & Items** | Inventory, hotbar, equipment slots, storage, drag and drop, stack splitting, hover info panel, auto-built UI, consumables, armor and armor sets with tiered bonuses, weapons with attacks, chains, charge, combos, blade and ground-impact hit detection | [Inventory guide](Documentation/Inventory/README.md) · [Setup guide](Documentation/Inventory/08-Setup-Guide.md) |
| **Combat & Abilities** | Shared damage, defense and resistances, status effects, abilities for the player and mobs, teams, factions, parties and friendly fire | [Teams, factions & targeting](Documentation/Inventory/09-Teams-Factions-and-Targeting.md) |
| **Procedural Terrain** | Endless streamed world: noise, landforms, mountains, volcanoes, biomes, climate, rivers and lakes, erosion, weather, object placement, world portals, mob spawning | [Terrain guide](Documentation/Terrain/README.md) |
| **Procedural Dungeons** | Multi-floor dungeons entered through world portals: layouts, corridors, room roles, population (mobs, loot, props), meshing, NavMesh, runtime | [Dungeon guide](Documentation/Dungeon/README.md) |

Start at the **[documentation index](Documentation/README.md)**.

## Requirements

* Unity 6 (6000.x)
* Packages: **Input System**, **TextMeshPro** (UI), **AI Navigation** (NavMesh for mobs and dungeons),
  **Cinemachine** (player camera). The test files compile only when the **Unity Test Framework** is installed.
* A generated C# class named `PlayerInput` from your Input Actions asset, with a **Player** action map
  (see [Player 02 — Movement & Input](Documentation/Player/02-Movement-and-Input.md)).

## Installation

1. Copy (or clone) this repository into your project as `Assets/Scripts/`.
2. Install the packages listed above (*Window ▸ Package Manager*).
3. Create the shared settings assets: *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Combat Settings (Resources)* and
   *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Trait Database (Resources)*.
4. Follow the setup guides:
   * [Set up the player](Documentation/Player/01-Player-Setup.md)
   * [Set up a mob](Documentation/Mobs/01-Mob-Setup.md)
   * [Create items, armor and weapons](Documentation/Inventory/08-Setup-Guide.md)
   * [Generate a world](Documentation/Terrain/README.md) / [Add dungeons](Documentation/Dungeon/README.md)

## Folder map

| Folder | Contents |
|---|---|
| `Player/` | Player component, movement / ability / availability state machines, camera, animation, status controller, classes, traits, experience |
| `Mob/` | Mob component, AI (brain, perception, motor, ability selector, combat coordinator, animation driver), mob state machine |
| `Inventory/` | Inventory manager and its handlers, slots, items (consumables, armor, weapons), equipment, armor sets, weapon runtime |
| `Essentials/` | Shared systems: status managers, ability and combat core, factions, AI hazards, UI, portals |
| `Procedural/` | Terrain world generation and dungeons |
| `AudioSystem/` | Audio helpers |
| `CustomEditor/` | Inspectors, setup wizards, preview windows and validation tools (editor only) |
| `Documentation/` | All guides (Markdown, with Mermaid diagrams that render on GitHub) |
