# Project Organisation — generated assets and the script layout

Two things live here: **where every asset the tools generate goes** (one place, organised by kind), and **how the
scripts are organised**, with the moves recommended to make the layout clearer and the game buildable.

---

## 1. Generated assets: `Assets/Resources/SimpleMovements/`

Every tool of the package that creates an asset without asking you where (settings, databases, profiles, abilities,
prefabs, materials, meshes, backups) now writes it under one folder, with one subfolder per kind. The paths are defined
once, in `Essentials/Project/SMAssets.cs` (runtime) and `CustomEditor/Project/SMAssetsEditor.cs` (editor).

```
Assets/Resources/SimpleMovements/
├─ Settings/          CombatSettings, AbsorptionSettings, HubWorldSettings, ElementalSystem, InjurySettings
├─ Databases/         AbilityDatabase, TraitDatabase, ItemDatabase, ItemCategoryDatabase, CharacterCatalog
├─ Economy/           DefaultCurrency
├─ Characters/
│  ├─ Races/          example races (Human, Dwarf, Elf, Barbarian), race presets
│  ├─ Classes/        class archetypes, class presets, "New Player Class"
│  └─ Traits/         traits made from presets (Trait Manager, Trait Database, Assets ▸ Create)
├─ Abilities/         ability definitions from presets, converted legacy abilities, saved runtime copies
├─ Mobs/
│  └─ <Type>/         one folder per mob type: <Type> Profile, <Type> Basic Attack, its abilities and body parts
├─ Combat/
│  └─ Body Parts/     body part profiles of non-mob characters
├─ Items/
│  ├─ Weapons/        weapon templates; Combo Trees/
│  ├─ Armor/          armor sets (Armor Set Wizard)
│  ├─ Consumables/    consumable presets
│  ├─ Ammo/           ammo presets, ammo made from a weapon
│  ├─ Medical/        medical item presets (bandage, splint, suture kit...) and their icons / model
│  ├─ Limbs/          severed limb items (one per body part that can be lost), icons, models
│  └─ Drops/          ItemDropManager; Tables/ (Chest Drops, Breakable Drops, your tables)
├─ UI/
│  ├─ HUD/            HUD prefabs made from the Player HUD inspector
│  └─ Inventory/      slot / item prefabs and sprites of the Inventory UI Builder
├─ World/
│  ├─ Weather/        Weather Condition assets
│  ├─ Hub World/      hub city materials and wall meshes
│  └─ Biome Backups/  biome backup files (JSON)
├─ Dungeons/          default folder of Dungeon ▸ Create Default Setup / Create Dungeon Type, and portals
├─ Networking/        NetWorldItem prefab, RealmObserverCondition
└─ Generated/         anything else older versions put in Assets/Generated
```

| You create… | With | Goes to |
|---|---|---|
| Combat / Absorption Settings, Ability Database | Tools ▸ SimpleMovements ▸ Project Setup ▸ Create … (Resources) | `Settings/`, `Databases/` |
| Trait Database, Character Catalog, Item Category Database, Default Currency, Hub World Settings | Project Setup ▸ Create … | `Databases/`, `Economy/`, `Settings/` |
| Item Database | Project Setup ▸ Build Item Database | `Databases/` |
| Item Drop Manager, drop tables | Inventory ▸ Item Drops ▸ Create Item Drop Manager / Create New Drop Table | `Items/Drops/`, `Items/Drops/Tables/` |
| Injury Settings | Project Setup ▸ Create Injury Settings (Resources) | `Settings/` |
| Medical items, severed limb items | Player ▸ Injuries ▸ Create Medical Items / Create Severed Limb Items | `Items/Medical/`, `Items/Limbs/` |
| A mob's profile, basic attack, abilities, body parts | Mobs ▸ Create New Mob, Quick Setup Mob, the mob inspectors | `Mobs/<Type>/` |
| Player abilities | Ability presets on the player, casters, legacy conversion | `Abilities/` |
| Example races, class archetypes | Project Setup ▸ Create Example Races, Player Class ▸ Create Class Archetype | `Characters/Races/`, `Characters/Classes/` |
| Weather conditions | Weather ▸ Create Default Weather Condition Assets | `World/Weather/` |
| Hub city materials and meshes | Hub World ▸ Build City Scene | `World/Hub World/` |
| HUD prefabs | Player HUD inspector ▸ … Prefab buttons | `UI/HUD/` |
| Inventory slot / item prefabs | Inventory UI Builder (Prefab Folder) | `UI/Inventory/` |
| World item carrier, observer condition | Network ▸ Create World Item Prefab, Create Bootstrap Scene | `Networking/` |
| Dungeon setups, world portals | Dungeon ▸ Create Default Setup… / Create Dungeon Type… (the folder dialog starts in `Dungeons/`) | the folder you pick |

**Assets ▸ Create menus** (weapon templates, item presets, ability and trait presets, archetype presets, mob profile
presets) keep Unity's convention: the asset goes into the folder **selected in the Project window**; with nothing
selected (or only `Assets`) it goes into its organised folder above.

### How the game finds them

Project-wide assets are loaded with `SMAssets.Load<T>(folder, name)`: the organised path first
(`Resources/SimpleMovements/Settings/CombatSettings`), then the place older versions used (`Resources/CombatSettings`,
at the root of any Resources folder), then any asset of that type under `Resources/SimpleMovements`. So **nothing
breaks before you organise an older project**.

### Organising an older project

**Tools ▸ SimpleMovements ▸ Project Setup ▸ Organise Generated Assets…** lists every asset older versions generated and
moves them into the layout above after you confirm:

| From | To |
|---|---|
| `CombatSettings`, `AbsorptionSettings`, `HubWorldSettings`, `ElementalSystem` at the root of a Resources folder (`Assets/Resources`, `Assets/SimpleMovements/Resources`…) | `Settings/` |
| `AbilityDatabase`, `TraitDatabase`, `ItemDatabase`, `ItemCategoryDatabase`, `CharacterCatalog` (same) | `Databases/` |
| `DefaultCurrency` | `Economy/` |
| `ItemDropManager` | `Items/Drops/` |
| `NetWorldItem.prefab` | `Networking/` |
| `Assets/Generated/Mobs/…` | `Mobs/…` |
| `Assets/Generated/…` (ability definitions → `Abilities/`) | `Generated/…` |
| `Assets/HubWorld Materials` | `World/Hub World/` |
| `Assets/SimpleMovements/Weather Conditions` | `World/Weather/` |
| `Assets/SimpleMovements/Item Drops` | `Items/Drops/Tables/` |
| `Assets/BiomeBackups` | `World/Biome Backups/` |
| `Assets/UI/HUD` | `UI/HUD/` |
| `Assets/Inventory/Generated` | `UI/Inventory/` |
| `Assets/Characters/Races` | `Characters/Races/` |
| `Assets/Networking/RealmObserverCondition.asset` | `Networking/` |

* Moves keep the assets' GUIDs, so **every reference in scenes and prefabs stays**. Folders left empty are removed.
* Nothing is overwritten: when the organised folder already has an asset with the same name, the old one stays.
* Copies of settings outside every Resources folder (which the game never loaded) are left where they are.
* Mob profiles and basic attacks that older versions saved **next to a mob prefab** are not moved (they are your
  folders); new ones go to `Mobs/<Type>/`.
* The **Project Validator** (Project Assets checks) says when there is something to organise and has an *Organise*
  button. *Project Setup ▸ Show Generated Assets Folder* selects the folder.

### Resources and builds

Everything inside a `Resources` folder is included in builds, whether or not a scene uses it. That is what lets the
game load settings and databases by name, and it keeps every generated asset in one predictable place. The cost is
build size for generated assets no scene uses (old HUD prefabs, unused profiles, biome backups): delete the ones you no
longer need.

---

## 2. The script layout

890 scripts, organised by system. Most of the structure is good: `Networking/` (Core = plain logic and tests,
Runtime = Fish-Net components), `Procedural/Dungeon/` (Config, Core, Stages, Presentation, Build, Runtime), `Procedural/World/`,
`HubWorld/` (Config, Core, Runtime), `NPC/`, `Mob/AI`, `Inventory/Items`, `Essentials/UI/*`. The recommendations
below fix what breaks a build, then what is hard to find.

### 2.1 Must do: make the editor scripts editor-only

`CustomEditor` is **not** a Unity "Editor" folder and the project has no assembly definitions, so every script in it
is compiled into the **game** assembly. Scripts wrapped in `#if UNITY_EDITOR` are fine, but **60 of them are not**
(all of `CustomEditor/DungeonEditor`, `CustomEditor/TerrainGenerator`, every `*Tests.cs`, `PlayerAnimatorSetup`,
`PlayerAudioSetup`, `PlayerClassEditor`, `SocialSetupMenu`, `SpawnableMobEditor`, `CharacterBodyEditor`, `SMHelpMenu`…).
They use `UnityEditor` (or NUnit), which does not exist in a player build: **File ▸ Build fails** while the editor
works.

**Fix: rename the folder `CustomEditor` to `Editor`** (in Unity's Project window, so the .meta files move with it).
Unity compiles every folder named `Editor` into the editor assembly only. Checked before recommending it:

* No game script uses a type defined in `CustomEditor`, so nothing else needs to change.
* `MobQuickSetup` (a MonoBehaviour that only holds the mob menu items) cannot be added to objects any more. Nothing
  needs it on an object; if an old prefab has it, Unity shows a missing script there: remove it.
* `SerializableDictionary` (in `CustomEditor`) is not used anywhere: delete it, or move it to `Essentials/Utilities` if
  you want to keep it for your own scripts.

Already fixed in this update: three game scripts had an unused `using UnityEditor` that would also break a build
(`EquipmentVisuals`, `SlotTypeHelper`, `PlacementPlan`).

### 2.2 One place for legacy code

Old systems are spread over five folders. Put them under one `Legacy/` folder (or delete what is only comments):

| Script(s) | State | Do |
|---|---|---|
| `Procedural/Dungeon/Old/BackUp.cs`, `BackUp2.cs`, `NewDungeonGenerator.cs`, `OldOldDungeonGenerator.cs` | entirely commented out | delete |
| `Inventory/Items/BackUpWeaponController.cs`, `Procedural/LEGACY/FuncationBackUp.cs` | entirely commented out | delete |
| `Procedural/LEGACY/NewTerrain.cs`, `Procedural/LEGACY/OldOldTerrain/`, `Procedural/World/LEGACY_VoronoiGeneratior.cs` | not used by any script | `Legacy/Terrain/` (or delete) |
| `Procedural/LEGACY/SingleTerrain/` | used only among themselves | `Legacy/Terrain/SingleTerrain/` |
| `Procedural/Dungeon/WallColission.cs` | not used | delete |
| `Procedural/Dungeon/DungeonGenerator.cs`, `DungeonMobSpawner.cs`, `RoomBehaviour.cs` | the old 7×7 room dungeon; `RoomBehaviour` is still used by Legacy Room templates | `Procedural/Dungeon/Legacy/` |
| `Inventory/InventoryManager/SlotManager/OLDSlotValidationManager.cs` | not used | delete |
| `Essentials/Ability/` root (`AbilityEffectSO`, `AbilityHolder`, `AbilitySO`, `AbilitySpawner`, `BaseAbilityController`, `BaseAbilityHolder`) and `Essentials/Ability/Player Ability/` | the legacy ability system (still read by the converter and old mobs) | `Essentials/Ability/Legacy/` and `Essentials/Ability/Legacy/Player/` (no space in the folder name) |
| `Essentials/CharacterCreation/` root (`CharacterCreationUI`, `ClassSelectionManager`, `RaceSelectionManager`, `TraitSelectionManager`, `PlayerCreationManager`, `UIDisplayManager`, `CharacterCreationUIHelpers`, `CharacterCreationValidator`) | the old creation screen; the current one is `CharacterCreation/Runtime` | `Essentials/CharacterCreation/Legacy/` |
| `Essentials/UI/UIPlayerManager.cs`, `LoadingDisplayManager.cs` | not used by any script (check your scenes) | `Legacy/UI/` |
| `ComponentValidator.cs` (project root) | not used by any script | delete, or `Essentials/Validation/` |

### 2.3 Files at the wrong level

| Move | To | Why |
|---|---|---|
| `Essentials/IAssignmentsValidator.cs`, `Essentials/ReferenceAttributes.cs` | `Essentials/Validation/` | validation attributes and interfaces, next to each other |
| `Essentials/Casts.cs`, `Essentials/ScenePhysics.cs` | `Essentials/Physics/` | detection casts and scene-aware physics queries |
| `Essentials/GenericMethods.cs` | `Essentials/Utilities/` | helpers |
| `Essentials/PlayerNameComponent.cs` | `Player/` | it is the player's name |
| `Essentials/Portal.cs` | `Procedural/Dungeon/Runtime/` | the world portal that starts dungeon sessions (with `DungeonSession`, `DungeonManager`) |
| `Essentials/UI/DayNightManager.cs` | `Procedural/World/Weather/` | day / night lives with `DayNightCycle` and `WorldTime` (which uses it) |
| `Essentials/Project/SMAssets.cs` | (new) | where generated assets go, see section 1 |
| Combat core in `Essentials/Ability/Core/`: `CombatEntity`, `CombatEvents`, `CombatFaction`, `CombatQuery`, `CombatRules`, `CombatSettings`, `CombatTargeting`, `DamageTypes`, `ForcedMovement`, `HazardRegistry` | `Essentials/Combat/Core/` | 37 files mix the combat core with abilities; every system uses the combat core |
| `Mob/Essentials/RotateToGroundNormal.cs` | `Mob/Utilities/` | "Essentials" inside Mob is easily confused with the top-level `Essentials` |
| `Player/Controller/AbilitiesStateMachine/`, `AbilityStateMachine/`, `AvailabilityStateMachine/`, `MovementStateMachine/` | `Player/Controller/StateMachines/Abilities/`, `…/Ability/`, `…/Availability/`, `…/Movement/` | four state machines side by side, two with nearly the same name |
| `Inventory/` root: `CollectableItem`, `ItemPickable`, `ItemSpawner`, `RegenerateCollectableItem`, `WorldItems`, `RandomSpawningItems` | `Inventory/World/` | items in the world |
| `Inventory/` root: `Storage`, `StorageItem` | `Inventory/Storage/` | chests and storage |
| `Inventory/` root: `InventoryItem`, `InventorySlot`, `ItemInfo` | `Inventory/InventoryManager/UI/` | the inventory's UI elements |
| `Inventory/InventoryManager/`: `DragHandler`, `InventoryDropZone`, `InventoryPointerRelay`, `ItemHoverPanel`, `SplitStackPopup`, `StatusBarsUI`, `InventoryUIFactory`, `InventorySlotStyle`, `UILayoutManager`, `UIStateManager`, `InventoryFeedback` | `Inventory/InventoryManager/UI/` | 25 files mix logic (stacks, use, pickup) with UI |
| `Inventory/Items/Armor/`: `EffectRegistry`, `EquippableEffectType`, `SpecialMechanic*`, `ISpecialMechanicHandler`, `TraitEnhancement` | `Inventory/Equipment/Mechanics/` | equipment mechanics used by every equippable, not only armor |
| `Inventory/Loot/` | (new) | drop tables and the Item Drop Manager |

### 2.4 The editor folder (after renaming it `Editor`)

Mirror the game folders so an inspector is found where its component is:

| Now | Recommended |
|---|---|
| `CustomEditor/` root: `CharacterCatalogEditor`, `CharacterCreationUIEditor` | `Editor/Character/` |
| `CustomEditor/` root: `TraitEditor`, `TraitDatabaseEditor`, `TraitManagerEditor`, `TraitModifierDrawer` | `Editor/Player/Traits/` |
| `CustomEditor/` root: `SMMenu`, `SMHelpMenu` | `Editor/Menus/` |
| `CustomEditor/SpawnableMobEditor.cs` | `Editor/World/` (it edits the world spawner's mob entries) |
| `CustomEditor/InventoryManager/` | `Editor/Inventory/` (`CombatFactionEditor` → `Editor/Combat/`) |
| `CustomEditor/MobEditor/`, `DungeonEditor/`, `TerrainGenerator/` | `Editor/Mob/`, `Editor/Dungeon/`, `Editor/World/` |
| Tests spread over 9 folders (`DungeonEditor/Tests`, `Networking/Tests`, `Validation/Tests`, `HubWorld/*Tests`, `InventoryManager/*Tests`, `NPC/MerchantSystemTests`, `Player/ProgressionStatTests`, `Social/PartyBookTests`, `TerrainGenerator/WeatherRuleTests`) | `Editor/Tests/<System>/` |
| `CustomEditor/Project/SMAssetsEditor.cs` | `Editor/Project/` (new: generated-asset folders and Organise) |

### 2.5 The recommended tree

```
Assets/Scripts/            (the package)
├─ Essentials/
│  ├─ Ability/  Core/ · Legacy/ (+ Player/)
│  ├─ Character/ · CharacterCreation/ (Runtime/ · Legacy/)
│  ├─ Combat/  Core/ · Blocking/ · BodyParts/ · Buffs/ · Downed/ · Projectiles/
│  ├─ Currency/ · Input/ · Interactables/ · Physics/ · Project/ · Social/ · StateMachine/
│  ├─ Status/  Conditions/ · StatusEffects/
│  ├─ UI/  CharacterSheet/ · HUD/ · PauseMenu/ · Theme/
│  ├─ Utilities/ · Validation/
├─ Player/  Animation/ · Audio/ · Controller/ (Movement/ · StateMachines/) · Model/ · RPGSystem/ · View/
├─ Mob/  AI/ · Controller/ · Utilities/
├─ Inventory/  Equipment/ (Mechanics/ · Visuals/) · Grid/ · InventoryManager/ (SlotManager/ · UI/) · Items/ · Loot/ · QuickSlots/ · Storage/ · World/
├─ NPC/ · HubWorld/ · Networking/ · AudioSystem/
├─ Procedural/  Common/ · Dungeon/ (… · Legacy/) · World/
├─ Legacy/  Terrain/ · UI/
├─ Editor/  Ability/ · Character/ · Combat/ · Dungeon/ · HubWorld/ · Inventory/ · Menus/ · Mob/ · NPC/ · Networking/ · Player/ · Project/ · Social/ · UI/ · Validation/ · World/ · Tests/
└─ Documentation/
```

### 2.6 Moving scripts safely

* **Move in Unity's Project window** (drag folders and files there), never in the file explorer: Unity moves each
  `.meta` file with its script, so the GUIDs stay and no component in a scene or prefab becomes a missing script.
* If you move outside Unity, move the `.meta` file together with each script.
* Class names and namespaces do not depend on folders: nothing needs to be renamed in code.
* Do one row of the tables at a time and let Unity recompile before the next.
* Commit (or back up) first: a move is easy to undo with version control.
* Deleting a script that a scene or prefab still uses leaves a missing script there: the Project Validator finds them
  (Missing Scripts) and removes them.
