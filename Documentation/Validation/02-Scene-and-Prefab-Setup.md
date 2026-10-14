# Scene and Prefab Setup: what every scene and prefab needs

**Scripts:** `CustomEditor/Setup/` — `SetupFramework.cs` (requirements, context, runner), `SceneProfiles.cs` (Bootstrap,
Hub City, World, Dungeon), `PrefabProfiles.cs` (Player, Mob, Item), `SetupReportView.cs` (the list with Fix buttons),
`SceneAndPrefabSetupWindow.cs` (the window and its menus). The Player Setup Validator's inspector
(`CustomEditor/Player/PlayerSetupValidatorEditor.cs`) shows the same Player check.

---

## 1. Opening it

**Tools ▸ SimpleMovements ▸ Scene and Prefab Setup** (also *Window ▸ SimpleMovements ▸ Scene and Prefab Setup*):

| Entry | Opens |
|---|---|
| Open Setup Window (overview)... | Every page and its state at once |
| Bootstrap Scene / Hub City Scene / World Scene / Dungeon Scene | That scene's page |
| Player Prefab / Mob Prefab / Item Prefab | That prefab's page |
| Check Selection Or Active Scene | The page that fits the selected prefab (player, mob or item) or the active scene (recognised from what is in it) |

Each page:

1. **What it is**: what the scene / prefab is for and how it fits with the rest, with a link to its guide.
2. **The target**: the scene or prefab checked. It is found by itself (from the Hub World Settings' scene names, the
   networked player prefab, the first mob / item prefab...). Pick another one, use the active scene / selected prefab,
   or press **Create** (Bootstrap, City and Dungeon scenes) when the project has none. A scene is checked while it is
   open: **Open** or **Open Additively** (to check the City with the Bootstrap scene open, for example).
3. **The summary**: what is missing by level, a progress bar, and **Fix All Required** / **Fix Required + Recommended**.
4. **Every requirement**, grouped. Each line shows:
   * ✔ set up, ✖ missing, **!** set up wrongly, **i** a note, ○ an optional item not used, – does not apply here;
   * its level: **REQUIRED** (it does not work without it), *recommended* (a normal game has it), *optional*;
   * what was found (on which object, which value, what is wrong);
   * **Why:** what it is for and what goes wrong without it;
   * **By hand:** how to do it yourself;
   * **Select** (the object it is about) and **Fix** (does it for you).

Toggles hide what is already set up, and the explanations.

**Where fixes go.** Scene fixes are made in that scene (with Undo); save the scene afterwards. Prefab fixes are saved
to the prefab asset at once (use version control to revert them); when the prefab is open in Prefab Mode, they change
the open prefab instead (save it afterwards). Some fixes change project assets (Build Settings, Hub World Settings,
Combat Settings, the Input Actions asset, the databases).

Every page ends with **No missing scripts or broken references**: the Project Validator's object checks on the whole
scene or prefab.

## 2. The scenes

### Bootstrap Scene (multiplayer)

The first scene of a multiplayer build: Fish-Net's NetworkManager with the package's session components, and the
*Network World* with the global relays. Without Fish-Net the page only says the scene is not needed.

| Group | Requirement | Level | Fix |
|---|---|---|---|
| Network Manager | A `NetworkManager` object | Required | Create NetworkManager (all managers and session components) |
| | Fish-Net managers: Tugboat, Transport, Time, Server, Client, Scene, Prediction, Observer | Required | Add Missing Managers |
| | Server Manager ▸ Start On Headless off (the Net Bootstrap starts the server) | Recommended | Turn It Off |
| | Observer Manager uses the `RealmObserverCondition` | Required | Assign Realm Condition |
| | `NetSession`, `NetBootstrap`, `NetRealmScenes`, `NetPlayerSpawner` | Required | Add To NetworkManager |
| | `NetLagCompensation`, `HeadlessServer` | Recommended | Add To NetworkManager |
| | Net Player Spawner ▸ Player Prefab is the networked player | Required | Assign Networked Player Prefab |
| Network World | A root object *Network World* with a `NetworkObject` | Required | Create Network World |
| | Its observer ignores the manager's conditions (seen by every client) | Required | Set Ignore Manager |
| | Relays `NetHubWorld`, `NetWorldChunks` | Required | Add Relay |
| | Relays `NetDuels`, `NetTrading`, `NetSocial`, `NetPortals` | Recommended | Add Relay |
| | Every scene NetworkObject has a scene id | Required | Assign Ids (saves the scene) |
| Build | First scene in Build Settings | Required | Make It The First Scene |
| | City, World and Dungeon Instance scenes in the build | Required | Add The Ones Found |
| Project | `Resources/NetWorldItem` (carrier of dropped items) | Recommended | Create It |
| | Item Database, Ability Database | Recommended | Build |
| Contents | No player in this scene | Required | – |
| Shared systems | Day Night Cycle here or in the City | Optional | Add Day Night Cycle |

### Hub City Scene

The permanent city: players start and respawn here and leave through the City Gate.

| Group | Requirement | Level | Fix |
|---|---|---|---|
| Realm | A `RealmScene` with Realm = City | Required | Mark As City (with arrival and spawn points) |
| | Arrival point, Spawn point | Recommended | Create … Point |
| | A City Gate (`RealmGate`, Kind = City Gate) | Required | Add City Gate |
| Hub World | A `HubWorldDirector` (here, or in the open Bootstrap scene) | Required | Add Hub World Director |
| | `Resources/HubWorldSettings` | Recommended | Create |
| | Hub World Settings ▸ City Scene Name = this scene | Required | Use This Scene |
| | In Build Settings (first, or right after the Bootstrap scene) | Required | Add To Build |
| Players & UI | A player at the spawn (single player) / none (multiplayer) | note | – |
| | Pause & settings menu | Recommended | Build Menu |
| | EventSystem | Recommended | Create EventSystem |
| | Player Targeting & Party System (only to change their settings) | Optional | Add Both |
| Atmosphere | Day Night Cycle (here or in Bootstrap) | Recommended | Add Day Night Cycle |
| | Directional light | Recommended | Add Sun |
| | Realm music | Optional | Create Realm Music |
| Contents | No mobs; no terrain or weather (they belong in the World) | Recommended | – |

### World Scene

The open world, regenerated every cycle.

| Group | Requirement | Level | Fix |
|---|---|---|---|
| Realm | A `RealmScene` with Realm = World | Required | Mark As World |
| | Arrival point (outside the Return Gate) | Recommended | Create Arrival Point |
| | *City Exterior* (the city seen from outside) | Recommended | Build City Exterior |
| | A Return Gate | Recommended | Add Return Gate |
| Terrain | `TerrainGenerator` | Required | Add Terrain Generator (defaults) — then configure it |
| | `EndlessTerrain` | Required | Add Endless Terrain |
| | Endless Terrain ▸ Viewer empty (the director sets it) | Required | Clear Viewer |
| | Water level below the city's ground | Recommended | – (Hub World Settings ▸ Zones ▸ City Ground Height) |
| Weather | `WeatherSystem` | Recommended | Add Weather System |
| | Weather System ▸ Terrain Generator | Recommended | Assign |
| | `PlayerWeatherEffects` on the Weather System object | Recommended | Add Player Weather Effects |
| Hub World | Hub World Settings ▸ World Scene Name = this scene; in Build Settings | Required | Use This Scene / Add To Build |
| Contents | No Hub World Director, no player | Required | Remove It From This Scene (director) |
| | No camera, no Day Night Cycle | Optional / Recommended | – |

### Dungeon Scene (multiplayer)

The empty template each online dungeon run loads.

| Group | Requirement | Level | Fix |
|---|---|---|---|
| Dungeon instance | *Dungeon Instance* with `DungeonInstanceSceneMarker` | Required | Add Marker |
| | `NetworkObject` + `NetDungeonRun` on it | Required | Add Relay |
| | Scene ids | Required | Assign Ids |
| | Nothing else in the scene | Recommended | – |
| Hub World | Hub World Settings ▸ Dungeons In Own Scenes | Recommended | Turn On |
| | Dungeon Scenes ▸ Network Scene Name = this scene | Required | Use This Scene |
| | In Build Settings | Required | Add To Build |

## 3. The prefabs

### Player Prefab

Every script of the Player Setup Validator's list (grouped: *Movement*, *Status & progression*, *Animation, camera &
input*, *Character*, *Items, combat & abilities*, *Added automatically at runtime*) with **Add** — or **Run Status
Setup Wizard** for the status managers — plus:

| Group | Requirement | Level | Fix |
|---|---|---|---|
| Status & progression | Every status manager linked to the Player Status Controller | Required | Run Status Setup Wizard |
| Object settings | Tag *Player* | Recommended | Set Tag |
| | Layer in Combat Settings ▸ Character Layers | Required | Add Layer To Combat Settings |
| | Character Controller on the root; height ≥ 2 × radius; sensible skin width | Required | Fix Skin Width / Height |
| | No physics (non-kinematic) Rigidbody next to it | Required | Make It Kinematic |
| | No duplicated core scripts | Required | – |
| References | Animator with an Animator Controller | Recommended | – (Player ▸ Complete Player Animator Controller) |
| | Weapon Controller ▸ Hand, Inventory ▸ Hand Parent | Recommended | Use Right Hand Bone / Use The Weapon Hand |
| | Body part hitboxes (with a Body Part Controller) | Optional | – |
| | A camera with a Cinemachine Brain | Recommended | – |
| Input | Player Input: actions asset, Invoke Unity Events, map *Player* | Recommended | Fix Player Input |
| | The Input Actions asset has every action the scripts read | Required | Add Missing Actions |
| Interaction & social | Player Interactor | Recommended | Add Player Interactor |
| | Player Social | Optional | Add Player Social |
| Multiplayer | NetworkObject (prediction) and the player's network components | Recommended | Make Networked |
| Project | Combat Settings, Trait Database, Item Database | Recommended | Create / Build |

### Mob Prefab

| Group | Requirement | Level | Fix |
|---|---|---|---|
| Core | `Mob` (Mob Actions Controller) | Required | Run Quick Setup (adds and wires everything) |
| Navigation & physics | NavMesh Agent, Collider | Required | Run Quick Setup |
| | Kinematic Rigidbody, no gravity | Required | Make It Kinematic |
| | Layer in Combat Settings ▸ Character Layers | Required | Add Layer To Combat Settings |
| Status | Mob Status Controller, Health, Speed — linked | Required | Run Quick Setup |
| AI | Mob Movement State Machine — linked to the agent, status and animator | Required | Run Quick Setup |
| | Mob Ability Controller, Combat Entity | Recommended | Run Quick Setup |
| Behaviour | Mob Profile asset | Recommended | Create Profile From The Mob's Fields |
| | Type and preys | note | – |
| | Animator with a controller | Recommended | – |
| Multiplayer | NetworkObject, NetMob, NetCombatStatus, NetAbilityCaster, NetworkTransform | Recommended | Make Networked |

### Item Prefab

The model of an item (Item ▸ Prefab): held in the hand and lying in the world.

| Group | Requirement | Level | Fix |
|---|---|---|---|
| Model | A renderer | Required | – |
| | A collider (else a box is added when dropped) | Recommended | Add Fitted Box Collider |
| | No collider on the Ignore Raycast layer | Required | Move To Default |
| Item | An Item asset uses this prefab | Recommended | – |
| | Item Pickable (needed to place it in a scene) | Recommended | Add Item Pickable |
| | Item Pickable ▸ Item assigned | Recommended | Assign The Item Using It |
| | Those items are in the Item Database | Recommended | Build Item Database |
| | Those items have icons | Optional | – |
| Multiplayer | `Resources/NetWorldItem` carrier exists | Recommended | Create It |
| | Networked prefab of its own (only for special loot) | Optional | Make Networked |

## 4. Adding your own checks

A page is a `SetupProfile` with a list of `SetupRequirement`s. Each requirement is a group, a title, a level, an
explanation (`Explain(why, how)`), a check returning a `SetupResult` (`Ok`, `Missing`, `Wrong`, `Note`, `Skip`) and,
optionally, a fix (`Fixes(label, ctx => …)`). The `SetupContext` gives the scene or prefab: `All<T>()`, `First<T>()`,
`RootNamed(...)`, `CreateObject(...)` (in the right scene), `Add<T>(go)` (with Undo where it applies), `Record`,
`Dirty`. Add a requirement to a profile in `SceneProfiles.cs` / `PrefabProfiles.cs`, and it appears in the window (and,
for the player, in the validator's inspector).
