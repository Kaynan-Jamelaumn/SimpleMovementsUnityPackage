# Player — Guide

How to build the player prefab and what each of its scripts does: movement and input, stamina and the other status
bars, classes, races, traits and experience, combat stats, body parts, the camera, animation and ability keys. The inventory, items, armor and weapons
the player carries are in the [Inventory guide](../Inventory/README.md).

| Chapter | Covers |
|---|---|
| [01 — Player Setup](01-Player-Setup.md) | Combat Settings, Absorption Settings and the other project assets (what and why), the hierarchy, every component (added by you or automatically), the Player Setup Validator, body parts, the Armor Sets window, input, the character creation screen, the pause and settings menu (key bindings), checklist |
| [02 — Movement & Input](02-Movement-and-Input.md) | Movement state machine (idle, walk, sprint, crouch, jump, dash, roll), input actions, jump buffering, stamina costs, stun and death |
| [03 — Status, Classes, Traits & Experience](03-Status-Classes-Traits-XP.md) | The 11 status managers, `PlayerStatusController`, player classes, traits, experience and level-ups |
| [04 — Camera, Animation & Abilities](04-Camera-Animation-Abilities.md) | Cinemachine cameras, the Animator parameters, interaction, ability keys |
| [05 — Troubleshooting](05-Troubleshooting.md) | Symptoms → causes → fixes |
| [06 — Races, Classes, Advanced Stats, Buffs & Threat](06-Races-Classes-Stats-and-Threat.md) | Races / classes / backgrounds on one archetype model, character identity and height, every combat stat with formulas and caps, the damage pipeline, timed buffs and debuffs, threat and multiplayer aggro |
| [07 — Swimming, Water & Streamed Ground](07-Swimming-Water-and-Streamed-Ground.md) | Subnautica-style swimming (swim where you look, stay at your depth, float at the surface) and its settings, swimmable water, the oxygen fix, and how long dashes wait for terrain chunks to load |
| [08 — Character Size, Animator, Audio, Interaction](08-Character-Size-Animator-Audio.md) | Character Identity vs Character Body (height, Scale Limits, CharacterController), completing the Animator controller, movement sounds and realm music, Player Interactor / Net Interactor |
| [09 — Targeting, Party, Guild, Inspect](09-Targeting-Party-Guild-Inspect.md) | Clicking another player (highlight ring, target frame, menu), parties (invite, accept, leader, no damage between members, buffs still work, duels override), guilds and alliances, fame, inspecting equipment and stats, the NetSocial relay |
| [10 — Character Creation, Appearance & Menus](10-Character-Creation-Appearance-and-Menus.md) | Start screens (solo, host, join, settings), character select and creation (race, class, traits, appearance with a 3D preview), saved characters, the server check, Character Appearance (body type, hair, beard, eyes, nose, colours; placeholder shapes until your art is set up), the inventory's character panel, the restyled pause and settings menu |
| [11 — Player HUD](11-Player-HUD.md) | The HUD built in code: player frame (health, mana, stamina, level, experience), needs gauges, abilities bar (icons, keys, casting glow, cooldown countdowns, hover info), active trait, the restyled hotbar and quickslots, the palette and your own slot prefabs |

## The player at a glance

```mermaid
flowchart TB
    IN["Input<br/>(generated PlayerInput class + Player Input component)"] --> MSM["MovementStateMachine<br/>idle · walk · run · crouch · jump · dash · roll"]
    IN --> ASM["AbilitiesStateMachine<br/>ability keys"]
    IN --> INV["InventoryManager<br/>inventory, hotbar, use item"]
    IN --> PL["Player<br/>interaction (pick up, open)"]
    MSM --> MC["PlayerMovementController<br/>gravity, rotation, ground check"]
    MC --> CC["CharacterController"]
    AV["AvailabilityStateMachine<br/>unaffected · stunned · dead"] -. blocks .-> MSM
    AV -. blocks .-> ASM
    PSC["PlayerStatusController<br/>11 status managers, class, traits, XP"] --> MSM
    INV --> WC["WeaponController<br/>attacks with the weapon in hand"]
    MSM --> ANIM["PlayerAnimationModel / Controller<br/>Animator parameters"]
    WC --> ANIM
    CE["CombatEntity<br/>(added automatically)"] --- PSC
    INV --> EQ["EquipmentManager · ArmorSetManager<br/>EquipmentVisuals · BlockController · QuickSlotBar<br/>(added automatically)"]
    EQ --> CS["CombatStats<br/>Strength, Defense, crit, penetration…"]
    ID["CharacterIdentity<br/>race, class stats, height, level"] --> CS
    CS --- CE
    BP["BodyPartController<br/>head · torso · arms · legs"] --- CE
```

## Ten-minute setup

1. Create the Combat Settings and Trait Database: *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Combat Settings (Resources)*,
   *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Trait Database (Resources)*; put the player's layer in *Combat Settings ▸ Character Layers*
   ([01 §1](01-Player-Setup.md#1-project-wide-assets-once-per-project)).
2. Make a player GameObject with a `CharacterController` and the movement scripts; press each component's
   **Auto-assign References** ([01 §3](01-Player-Setup.md)).
3. Add `PlayerStatusController` and press **Complete Setup Wizard**: it adds every status manager, the experience and
   trait managers and the dash / roll models, and wires them.
4. Add the model with its Animator as a child; add `PlayerAnimationModel` and `PlayerAnimationController`.
5. Add an `InventoryManager` and a `WeaponController` (Hand Game Object = the right hand bone). Select the player: the
   inventory UI, quickslots, status bars, interaction prompt and Armor Sets window are built automatically.
6. Optional: `CharacterIdentity` + `CharacterBody` (race, class stats, height), `BodyPartController` with
   **Add Hitboxes (Humanoid)** ([01 §5](01-Player-Setup.md#5-body-parts-on-the-player)).
7. Add a **Player Input** component for the camera and interaction events ([01 §7](01-Player-Setup.md#7-input)).
8. Add a **Player Setup Validator** and fix anything red. In the scene: *Tools ▸ SimpleMovements ▸ Scene ▸ Build Pause & Settings Menu*,
   and where characters are created *Tools ▸ SimpleMovements ▸ Scene ▸ Build Character Creation Screen*
   ([01 §8](01-Player-Setup.md#8-the-scene-character-creation-and-the-pause-menu)).
9. Press Play.
