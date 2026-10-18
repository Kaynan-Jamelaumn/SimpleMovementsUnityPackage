# Validation, auto-assign and tests

Editor tools that find broken setups before you press Play, and fill empty references for you. Everything is in
**Tools ▸ SimpleMovements ▸ Validate** and in **Window ▸ SimpleMovements ▸ Project Validator**. Nothing here runs in a
build.

| Tool | What it does |
|---|---|
| [Project Validator window](#project-validator-window) | Lists every problem of the selection, the open scenes or all prefabs, with **Select** and **Fix** buttons |
| [Auto-assign references](#auto-assign-references) | Fills empty object fields of components from their own hierarchy |
| [`[AutoAssign]` and `[RequiredReference]`](#attributes-for-your-own-scripts) | Tell the tools where a field's object is, or that a field must not stay empty |
| [Missing scripts](#missing-scripts) | Finds and removes components whose script was deleted or renamed |
| [Network checks](#network-checks) | Network prefab mistakes (Fish-Net) |
| [Game setup checks](#game-setup-checks) | The Input Actions asset (every action the scripts read, keys bound twice) and the open scenes (weather, day/night, relays) |
| [The Tools ▸ SimpleMovements menu](#the-tools--simplemovements-menu) | How the menu is organised |
| [Tests](#tests) | Edit Mode tests for the validators, the network core and every network module |

## Project Validator window

**Window ▸ SimpleMovements ▸ Project Validator** (or **Tools ▸ SimpleMovements ▸ Project Validator**).

1. Choose the **scope**: *Selection* (scene objects and prefab assets), *Open Scenes*, *All Prefabs* (everything under
   `Assets/`), or *Everything*.
2. Choose the **checks**:
   - **Missing Scripts**: components whose script no longer exists.
   - **Broken References**: fields that point to a deleted object ("Missing" in the inspector).
   - **Required References**: empty fields marked `[RequiredReference]`.
   - **Assignable References** (off by default): empty fields the auto-assigner can fill without doubt. Each one is
     listed as a note with an **Assign** button.
   - **Network**: network prefabs and components. This needs Fish-Net; without it the check only says it was skipped.
   - **Project Assets**: settings and databases the package looks for in `Resources/SimpleMovements` (Combat Settings,
     Absorption Settings, Ability, Trait, Item and Item Category databases, Default Currency, Hub World Settings). It
     also flags build-settings scenes that no longer exist, and generated assets still where older versions put them
     (**Organise** moves them: see [Project Organisation](../Project-Organisation.md)).
   - **Mobs**: every mob's setup (components, references, physics, animation, profile, patrol, attacks, drops), the
     same checks as the [Mob Setup Validator](../Mob/01-Mob-Setup.md#5-mob-setup-validator).
3. Press **Run**. Errors come first. Use the toggles to show or hide errors, warnings and notes, and the search field
   to filter.
4. **Select** pings the object. For an object inside a prefab, it selects the prefab asset. **Fix** applies that
   issue's fix. **Fix All Shown** applies every fix that is visible.

Fixes on scene objects can be undone with Ctrl+Z. Fixes on prefab assets open the prefab, change it and save it, so
use version control to revert those.

At the bottom, **Auto-assign and other validators** has the auto-assign buttons. It also runs the package's other
validators: players, abilities, traits, NPCs in the scene, dungeon portals, hub world and network setup.

The same checks are also in the menu, with results in the Console:
**Validate ▸ Validate Selection / Validate Open Scenes / Validate All Prefabs**. Click a Console line to select its
object.

## Auto-assign references

**Tools ▸ SimpleMovements ▸ Auto-Assign**:

- **In Selection**, **In Open Scenes** and **In All Prefabs** fill the fields.
- Each one has a **Preview** entry that only reports what it would do.
- Every component's context menu (the ⋮ in the inspector) has **Auto-Assign Missing References** and
  **Preview Auto-Assign**.

For each component of your project's scripts (Unity's, Fish-Net's and TextMesh Pro's own components are skipped):

1. **The component's own `AutoAssignReferences()` method runs first**, when it has one. Most controllers of the
   package have one: player movement, abilities, state machines, mob controllers.
2. Then **every empty object field** is looked at:
   - **Never replaced:** a field that already has a value.
   - **Never filled:** assets (prefabs, items, sounds, materials) and fields named `...Prefab` or `...Template`.
   - **Order of search:** the same object, its children, its parents, the rest of its hierarchy and, for scene
     objects, the rest of its scene (references across scenes cannot be saved).
   - **Name match first:** an object named like the field wins, even if it is farther away. A `Collider hitbox` takes
     the child "Hitbox", not the body collider next to the script.
   - **Otherwise the nearest candidate:** the only component of the field's type on the nearest level is used, so an
     `Animator animator` takes the model's Animator.
   - **Ties are left empty:** if several candidates fit equally well, the field is reported as ambiguous.
   - **Transform and GameObject fields are filled by name only:** `Transform weaponSocket` takes "WeaponSocket",
     `Transform rightHand` takes "Hand_R".
   - **Fields whose name says they point elsewhere** (`target...`, `other...`, `ignore...`, `override...`,
     `fallback...`, `optional...`, and similar) are filled only by a clear name match, never just because one
     candidate exists.
   - **Scene-wide matches** need a single candidate or a near-exact name.

The Console report lists each assigned field and the reason (*only candidate on a child*, *named like the field
(100%)*...). It also lists the ambiguous fields and the required ones nothing could fill. One Ctrl+Z undoes a whole
run on scene objects.

Names are compared without case, spaces, `_`, `m_` and `(Clone)`, and word by word. Short forms are understood:
`cam` = camera, `anim` = animator, `rb` = rigidbody, `L`/`R` = left/right, `mgr` = manager, `ctrl` = controller.
"Ref", "Reference", "Component" and the type name are ignored at the end of a field name (`hitColliderRef` → "hit").

## Attributes for your own scripts

Both attributes are in `Essentials/ReferenceAttributes.cs`, in the global namespace. They do nothing at runtime.

```csharp
public class Turret : MonoBehaviour
{
    public Animator animator;                         // filled automatically: the only Animator nearby

    [AutoAssign(ChildName = "Barrel_Tip")]            // this exact child
    public Transform muzzle;

    [AutoAssign(AutoAssignFrom.Parents)]              // only look up the hierarchy
    public Rigidbody vehicleBody;

    [AutoAssign(AutoAssignFrom.Self | AutoAssignFrom.Scene)]
    public Light alarmLight;

    [AutoAssign(Never = true)]                        // set by code: leave it alone
    public Transform currentTarget;

    [RequiredReference("projectiles are spawned from it")]
    public Transform spawnPoint;                      // the validator reports it while empty
}
```

- `[AutoAssign(from)]` chooses where to look. `AutoAssignFrom` values: `Self`, `Children`, `Parents`, `Hierarchy`,
  `Scene`, and `Nearby` (the default: everything except the scene). With an explicit `from`, the scene is only
  searched if you include `Scene`.
- `ChildName = "..."` takes the object with that name (case and spaces ignored) instead of guessing.
- `Never = true` tells the auto-assigner to leave the field alone.
- `[RequiredReference]` makes the field an **error** in the Project Validator while it is empty, with an **Assign**
  fix when one object clearly fits. The optional reason is shown with the error. The package uses it on the
  **Net Player Spawner**'s player prefab.

## Missing scripts

**Tools ▸ SimpleMovements ▸ Validate ▸ Remove Missing Scripts ▸ In Selection / In Open Scenes / In All Prefabs**
lists the objects, asks first, then removes the dead components. Inside prefabs, the prefab is opened, cleaned and
saved. A missing script that comes from a prefab must be removed from that prefab: the issue names it.

## Network checks

With Fish-Net installed, the **Network** check (also **Validate ▸ Validate Network Prefabs** and
**Network ▸ Validate Network Prefabs**) reports these problems:

| Problem | Severity | Fix |
|---|---|---|
| A network component with no NetworkObject on its object or a parent (it never runs) | Error | Add a NetworkObject |
| A player whose NetworkObject has prediction off, or a Rigidbody prediction type | Error / Warning | Re-apply *Make Networked ▸ Player* |
| A player with no Graphical Object (the model is not smoothed between ticks), or with the root as its Graphical Object | Warning / Error | Re-apply (finds the visual child) |
| A player with no CharacterController on its root | Warning | - |
| A mob with no NetworkTransform, or a mob or world item with a client-authoritative NetworkTransform (the server cannot move it) | Error | Re-apply / make it server-authoritative |
| A mob prefab without a NetworkObject (clients never see it) | Warning | *Make Networked ▸ Mob* |
| A network prefab missing a module component (NetCombatStatus, NetInventory, ...) | Warning | Re-apply |
| A NetworkObject with *Is Networked* off | Note | - |
| Everything that **Validate ▸ Network Setup** lists (Bootstrap scene first in the build, City, World and dungeon instance scenes in the build, player prefab, world item carrier) | Warning | Create the world item prefab |

The "re-apply" fixes run **Tools ▸ SimpleMovements ▸ Network ▸ Make Selected Prefab Networked** on the prefab again.
It only adds what is missing and fixes the prediction and NetworkTransform settings.

## Tests

Open **Window ▸ General ▸ Test Runner ▸ Edit Mode** and press **Run All**. The test folders are:

| Folder | Covers |
|---|---|
| `CustomEditor/Validation/Tests` | Name matching (`ReferenceMatcherTests`). The auto-assigner on a small hierarchy: nearest and named candidates, ties, Transforms by name, attributes, dry run, existing values kept, the components' own methods (`ReferenceAutoAssignerTests`). The validator's required, assignable and broken references. The network checks (`NetworkPrefabValidatorTests`, with Fish-Net). |
| `CustomEditor/Networking/Tests` | The network core: command line, pose history, authority per mode (`AuthorityAndDatabaseTests`), realm separation, stable item ids. Every network module: movement, abilities, status, weapons, mobs, inventory, economy, hub world, dungeons, terrain. |
| `CustomEditor/HubWorld`, `CustomEditor/DungeonEditor/Tests`, `CustomEditor/InventoryManager`, `CustomEditor/NPC`, `CustomEditor/Player` | The hub world (cycles, zones, duels, travel), dungeon generation, inventory and equipment, merchants, progression. |

The tests only create temporary objects and remove them afterwards. They do not open or change your scenes or
assets.

## Game setup checks

Part of the Project Validator (the **Game Setup** check), and also in the menu:
* **Validate ▸ Input Actions** (also **Input ▸ Check Input Actions Asset**);
* **Validate ▸ Scene Setup (open scenes)**.

| Check | Severity | Fix |
|---|---|---|
| An action the scripts read is missing from the Input Actions asset's Player map: Movement, Jump, Use, Interact, Inventory... (error), or SelectTarget, CycleTarget, InspectTarget, TargetMenu, Trade, AcceptRequest, DeclineRequest, InteractClick, Map... (warning) | Error / Warning | **Add**: added on its usual key when that key is free, else without a key. All at once: **Auto-Assign ▸ Add Missing Input Actions** |
| One key bound to several actions of the same map | Warning (Info when it is on purpose: the left click shared by Use, SelectTarget and InteractClick) | Rebind one in the Input Actions editor |
| The Weather System is not in a scene with the terrain, or is on a player | Warning / Error | - |
| No Player Weather Effects (the weather is only visual) | Info | **Add Player Weather Effects** |
| Player Weather Effects on a player | Error | **Remove from the player** |
| A weather condition both in Conditions (asset) and in Rules (applies twice) | Warning | **Remove them from Rules** |
| Shelter Layers includes the players' layer | Warning | **Exclude the player layer** |
| Several Day Night Cycles open, or one on a player; none at all | Warning / Error / Info | **Remove from the player** |
| Several Player Targeting or Party System objects | Warning | - |
| The Network World misses a relay (NetHubWorld, NetDuels, NetTrading, NetWorldChunks, NetSocial) | Warning | **Add the relays** |

**Auto-Assign ▸ Fix Scene Setup (open scenes)** applies every fix of the scene checks at once.

## The Tools ▸ SimpleMovements menu

Sections are grouped with separators. The layout (paths and order) is defined in one file, `CustomEditor/SMMenu.cs`.

| Block | Sections | What is there |
|---|---|---|
| Checking and fixing | **Project Validator...**, **Check Everything...**, **Validate**, **Auto-Assign** | The validator window; everything at once; one area at a time (Selection, Open Scenes, All Prefabs, Players, Abilities, Traits, NPCs, Dungeon Portals, Hub World, Network Setup and Prefabs, Input Actions, Scene Setup, Remove Missing Scripts); fixers (references, players' setup, player interactors, missing input actions, scene setup) |
| Project | **Project Setup**, **Input** | Settings and databases in Resources (combat, abilities, traits, absorption, item categories, currency, hub world), building the item and ability databases; checking / completing the Input Actions asset and the saved key bindings |
| Characters and items | **Player**, **Inventory**, **Mobs**, **NPC**, **Social** | Setup validator, animator controller, movement audio, player social; inventory UI and tools; mob creation; NPCs; targeting and parties |
| World | **Hub World**, **Weather**, **Dungeon**, **Scene** | City scene and gates; day/night, weather effects, condition assets; dungeons and portals; menus, character creation, realm music |
| Multiplayer | **Network** | Bootstrap scene, networked prefabs, relays, builds, local runs, Play In Editor As |
| Other | **Legacy**, **Help** | Old-asset converters; open the documentation, what is in the menu |

Moved items: *Validate All …* are now **Validate ▸ Players / Abilities / Traits**; *Auto-Assign References* is its own **Auto-Assign** section; *NPC ▸ Validate NPCs*, *Dungeon ▸ Validate Portal Setup*, *Hub World ▸ Validate Setup* and *Network ▸ Validate Network Setup / Prefabs* are under **Validate**; *Network ▸ Build Item / Ability Database* and *Hub World ▸ Create Settings Asset* are under **Project Setup**; *Player ▸ Auto-Setup All Players* and *NPC ▸ Add Player Interactor To Players* are under **Auto-Assign**; *Scene UI* and *Audio* are merged into **Scene**; *Social ▸ Add Player Social* is under **Player**.
