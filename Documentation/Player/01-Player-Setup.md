# Player 01 — Player Setup

Everything needed to build a working player: the project-wide settings assets, the hierarchy, every component (which
ones you add, which ones are added for you), body parts, the armor sets window, input, the scene (character creation
screen, pause and settings menu), and a final checklist.

> **Fastest check:** add **Player Setup Validator** to the player (*Tools ▸ SimpleMovements ▸ Player ▸ Add Player Setup Validator To
> Selection*). Its inspector lists every script below as present or missing with an **Add** button (and **Add All
> Required / Recommended**), checks the hands, animator, layer and body-part hitboxes, and the project and scene setup
> (Combat Settings, Trait Database, EventSystem, pause menu, character creation) with a button to fix each.

---

## 1. Project-wide assets (once per project)

These are **ScriptableObject assets read by every character**. Each one is loaded by name from a `Resources` folder,
so it must be at `Assets/Resources/<Name>.asset` (the menu commands create it there and select it). Without the asset
the game still runs with built-in defaults, but you cannot change those defaults, so create the ones you need.

| Asset | Create with | Needed when |
|---|---|---|
| **Combat Settings** | *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Combat Settings (Resources)* | Always (recommended): layers, PvP, body parts, threat |
| **Trait Database** | *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Trait Database (Resources)* | The player has traits (character creation, trait UI) |
| **Absorption Settings** | *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Absorption Settings (Resources)* | Players learn abilities by killing mobs |
| **Ability Database** | *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Ability Database (Resources)* | You save absorbed abilities (it maps saved ids back to abilities) |

### Combat Settings: what it is and why

One asset with the rules every fight in the game shares, for players and mobs alike. Select it and set:

| Section | What to set | Why it matters |
|---|---|---|
| **Layers** | *Character Layers* = the layers the player and mobs are on; *Obstacle Layers* = terrain, walls; *Ground Layers* | Hit shapes, projectiles and AI perception only search these layers. If the player's layer is missing, nothing can hit it. |
| **Rules** | *Players Can Hurt Each Other* (PvP), *Party / Ally Friendly Fire*, *Kill Players At Zero Health* | Who can damage whom; whether the player dies through the Availability State Machine at 0 health. |
| **Body Parts** | *Body Parts For Every Character*, *Default Body Part Profile*, *Ability Strike Height / Spread* | Turns on head / torso / arm / leg damage for every character without adding components one by one (§5). |
| **Threat** | Threat per damage / healing / control, first-contact and detection threat, decay, switch margins | How mobs choose which player to attack in multiplayer ([06 §6](06-Races-Classes-Stats-and-Threat.md#6-threat-and-aggro-mobs-multiplayer)). |
| **AI Budgets** | Max melee / ranged attackers per target, near / far / sleep distances | How many mobs attack one player at once; distant mobs think less (performance). |
| **Telegraphs** | Show enemy telegraphs, colours, line width | The ground warnings of enemy abilities. |
| **Defaults** | Default projectile, absorb pickup, wall segment prefabs | Used when an ability or pickup has no prefab of its own. |
| **Debug** | *Draw Debug* | Draws hit shapes and AI decisions in the Scene view while playing. |

### Absorption Settings: what it is and why

Only for games where **the player learns abilities from the mobs it kills** (a mob's Fireball can drop as an orb that
gives the player a copy). Each mob decides *whether* it can give abilities (`MobAbilityController ▸ Absorption`: chance
per kill and which abilities); this asset decides the *global rules*:

| Field | Meaning |
|---|---|
| Enabled | Turns the whole mechanic on or off. |
| Global Chance Multiplier | Multiplies every absorb chance (difficulty tuning). |
| Max Abilities Per Kill, Only Player Kills | Limits. |
| Delivery | *Pickup*: a glowing orb drops where the mob died (arm delay, lifetime, pickup radius, magnet radius). *Instant*: the killer gets it at once. |
| Duplicate Policy | The player already has that ability: keep it, replace if stronger, always replace, or add another slot. |
| Full Slots Policy | Every ability slot is used: refuse, replace the oldest absorbed, or replace the last slot. |
| Default Variants | The forms an absorbed copy can take and their weights: Weaker, Same, Stronger, Altered (e.g. a triple shot becomes a focused single shot). Abilities can add their own variants. |

Absorbed abilities go into the player's ability slots (`PlayerAbilityController`). To save them use
`GetAbsorbedAbilities()` / `RestoreAbsorbedAbilities()`; restoring needs the **Ability Database**. If your game has no
absorption, skip this asset (or create it and untick *Enabled*).

### Input Actions

An Input Actions asset with a **Player** map, *Generate C# Class* on and the class named `PlayerInput`
([02 §2](02-Movement-and-Input.md)). Required packages: Input System, TextMeshPro, Cinemachine.

---

## 2. Hierarchy

```
Player                               ← CharacterController + the components of §3
├── Model                            ← mesh + Animator (Humanoid recommended)
│   ├── … Head, Spine, arm and leg bones
│   │      └── Hitbox_Head, Hitbox_Torso…   ← body-part hitboxes (§5), triggers on the player's layer
│   └── … RightHand / LeftHand bones  ← WeaponController ▸ Hand / Off Hand Game Object, InventoryManager ▸ Hand Parent
├── Cameras                          ← Cinemachine cameras listed in PlayerCameraModel
├── Ability Keys                     ← one child per ability key (ability key tools)
└── Inventory Canvas                 ← built automatically: hotbar, quickslots, inventory, equipment,
    └── ArmorSetUI                      status bars, interaction prompt, message line, Armor Sets window (§6)
```

The canvas lives **inside** the player, so the prefab carries its own UI and every player in multiplayer has its own.
No `EventSystem` is put in the prefab: the scene needs one (one is created at runtime when the scene has none).

---

## 3. Components

### 3a. Components you add

| # | Component | What it does | Setup |
|---|---|---|---|
| 1 | `CharacterController` | Moves the player and collides | Height and radius fit the model |
| 2 | `PlayerMovementModel` | Gravity, jump force, stamina costs, references | **Auto-assign References** |
| 3 | `PlayerMovementController` | Gravity, rotation toward the camera, ground check | **Auto-assign References** |
| 4 | `PlayerStatusController` | Status managers, class, experience, traits | **Complete Setup Wizard** — adds and wires #5–#7 |
| 5 | Status managers | `HealthManager`, `StaminaManager`, `ManaManager`, `HungerManager`, `ThirstManager`, `SleepManager`, `SanityManager`, `BodyHeatManager`, `OxygenManager`, `WeightManager`, `SpeedManager` | Added by the wizard |
| 6 | `ExperienceManager`, `TraitManager` | Levels, stat points, traits | Added by the wizard |
| 7 | `PlayerDashModel`, `PlayerRollModel` | Dash and roll | Added by the wizard |
| 8 | `AvailabilityStateMachine` | Unaffected / Stunned / Dead; blocks movement, abilities and attacks | Nothing to assign |
| 9 | `MovementStateMachine` | Idle, walk, sprint, crouch, jump, dash, roll ([02](02-Movement-and-Input.md)) | **Auto-assign References** |
| 10 | `PlayerAnimationModel`, `PlayerAnimationController` | Animator parameters, attack animations | Animator = the model's ([04](04-Camera-Animation-Abilities.md)) |
| 11 | `PlayerCameraModel`, `PlayerCameraController`, `PlayerCameraView` | Cameras, first / third person, look, zoom | List the Cinemachine cameras in the model |
| 12 | `Player` | Interaction: pick up items, open storage, use interactables | Assigned by the UI builder |
| 13 | `InventoryManager` | Inventory, hotbar, equipment, use / attack with the item in hand, grid mode | Selecting the player builds the UI; set *Hand Parent* |
| 14 | `WeaponController` | Weapon attacks: melee, ranged, dual wield, combos, charge, reload | **Hand Game Object** = right hand bone; **Off Hand Game Object** = left hand bone (empty = found on a Humanoid) |
| 15 | `AbilitiesStateMachine`, `PlayerAbilityController` | Ability keys and slots (absorbed abilities land here) | Optional ([04 §4](04-Camera-Animation-Abilities.md)) |
| 16 | `CharacterIdentity` | Race, class archetype, height, level, attribute points; applies the class's combat stats | Recommended ([06](06-Races-Classes-Stats-and-Threat.md)); character creation adds it when a race is picked |
| 17 | `CharacterBody` | Height: scales the model and resizes the CharacterController | Optional; *Model* = the model child, *Model Height* = its height in metres |
| 18 | `BodyPartController` | Head / torso / arm / leg damage on the player | Optional (§5) |
| 19 | `AudioSource` | Item, weapon and interaction sounds | Optional |

### 3b. Components added for you at runtime

Add them yourself only to change their settings in the inspector.

| Component | Added by | Does |
|---|---|---|
| `CombatEntity` | The combat system | The player as a combatant: health link, team, faction, party, body size, crowd control, threat, damage hooks |
| `CombatStats` | Equipment / stats | Strength, Defense, crit, penetration, resistances, cooldown reduction… summed from race, class, traits, items and buffs |
| `EquipmentManager` | Inventory | Applies the effects of worn items |
| `ArmorSetManager` | Equipment Manager | Armor set bonuses by pieces worn |
| `EquipmentVisuals` | Inventory (*Equipment Visuals* on) | Weapons in hand or sheathed, shields and armour on the body |
| `BlockController` | Inventory (*Shield Blocking* on) | Blocking, parry and guard break with shields and weapon guards |
| `QuickSlotBar` | Inventory (*Quick Slots* on) | The consumable bar next to the hotbar |
| `InventoryFeedback`, `ItemHoverPanel`, `HeldItemModel` | Inventory | Message line, item details panel, the model in hand |
| `TimedStatModifiers` | First buff or debuff received | Timed buffs / debuffs, removed exactly when they end |
| `BodyPartController` | Combat Settings ▸ *Body Parts For Every Character* | Body parts on every character |
| `ForcedMovement` | First knockback / pull | Displacements |

### 3c. Setup buttons

| Inspector | Button | Does |
|---|---|---|
| Player Status Controller | **Complete Setup Wizard** | Creates missing managers and models, assigns references, validates, applies the class values |
| Movement State Machine / Controller / Model | **Auto-assign References** | Fills empty references from the object, children and parents |
| Inventory Manager | **Build / Repair UI Now**, **UI Builder…** | Builds the inventory UI, status bars, prompt, quickslots, Armor Sets window |
| Armor Set UI Manager | **Build / Repair UI** | Builds or repairs only the Armor Sets window |
| Body Part Controller | **Create Editable Profile**, **Add Hitboxes (Humanoid)** | Body parts (§5) |
| Availability State Machine (Play Mode) | Stun, Silence, Clear, Kill, Revive | Test states |
| Combat Stats / Combat Entity (Play Mode) | — | Show stat totals and sources, threat, active buffs / debuffs |
| Player Setup Validator | **Add**, **Add All Required / Recommended**, **Run Status Setup Wizard**, **Auto-assign References**, fix buttons | Checks and completes the whole player (see the note at the top) |
| *Tools ▸ SimpleMovements* | *Validate ▸ Validate All Players*, *Player ▸ Auto-Setup All Players In Scene* | Every player in the open scenes |

---

## 4. Movement and status values

| Setting | Where | Suggested |
|---|---|---|
| Gravity | Movement Model | `-9.81` (negative) |
| Gravity Multiplier | Movement Model | `2` |
| Jump Force | Movement Model | `6` (about 0.9 m with multiplier 2) |
| Speed | Speed Manager | `5` m/s walking; run and crouch multipliers on the same component |
| Dash Speed / Duration / Cooldown | Dash Model | `35`, `1`, `4` |
| Roll Speed / Duration / Cooldown | Roll Model | `2`, `0.25`, `4` |
| Should Consume Stamina | Movement, Dash, Roll Models | On for survival games |

---

## 5. Body parts on the player

With body parts, a hit on the head deals more damage than a hit on the legs, armour protects the parts it covers,
shields cover chosen parts, and parts can have effects (a leg hit slows). Hits are located by **hitboxes** (colliders
on the bones) or, without hitboxes, by the **height** where they land.

1. Add a **`BodyPartController`** to the player (or turn on *Combat Settings ▸ Body Parts For Every Character*).
2. **Profile:** empty uses a built-in humanoid (Head ×1.6, Torso ×1, Arms ×0.8, Legs ×0.85). To change parts,
   multipliers, natural armour or effects press **Create Editable Profile** (or *Create ▸ SimpleMovements ▸ Combat ▸
   Body Part Profile*) and edit it.
3. **Hitboxes (recommended):** with a Humanoid Animator press **Add Hitboxes (Humanoid)**: a sphere on the head and
   capsules on the torso, arms and legs, as triggers named `Hitbox_…` on the player's layer. Adjust each radius on its
   object. Other rigs: add a collider + `BodyPartHitbox` (Part = the part's name) to each bone yourself.
4. **Check it:** select the player to see the coloured parts and hitboxes in the Scene view (*Always Show Parts* to
   see them unselected). In Play Mode each hit is marked with its part and damage in the Scene and Game views, and the
   inspector's **Hit** buttons test each part.

The hitboxes must be on a layer in *Combat Settings ▸ Character Layers* and stay **triggers**, so they never push the
CharacterController. When one attack overlaps two parts (head and torso), it is counted **once**, on one part, chosen
by the profile's *When Several Parts* rule. Details: [Inventory 10 §2](../Inventory/10-Hands-Shields-Ranged-and-Body-Parts.md#2-body-part-damage).

---

## 6. Armor Sets window (`ArmorSetUIManager`)

A window that shows the **armor sets the player is wearing pieces of**: the list of worn sets, the selected set's icon,
name, description and pieces worn (e.g. 2/4), its piece slots, which bonuses are active and which are unlocked by the
next pieces. It only displays information: the bonuses themselves are applied by `ArmorSetManager` whether or not the
window exists. Skip it if your game has no armor sets.

* **Build it:** the inventory's UI build creates it (*UI Builder ▸ Build Armor Set UI*, on by default) as `ArmorSetUI`
  in the player's canvas, wires it to the Inventory Manager and adds a **Sets** button to the inventory panel. On an
  existing `ArmorSetUIManager`, **Build / Repair UI** creates only what is missing.
* **Open it:** the Sets button, or an input action named `ArmorSetUI` / `ArmorSets` / `Sets`. With *Show UI On Set
  Completion* it opens by itself when a set is completed and closes after *Auto Hide Delay* seconds (0 = stays open).
* **Settings:** sounds for set completion and bonus activation, console notifications. In Play Mode the inspector can
  open and close it.

---

## 7. Input

**The generated `PlayerInput` class** is read directly by movement, ability keys, traits, inventory, weapons,
blocking, quickslots and the Armor Sets window — they find their actions **by name** and fall back to default keys:

| Feature | Action names looked for | Fallback |
|---|---|---|
| Inventory | `Inventory`, `OpenInventory`, `ToggleInventory` | Tab, I |
| Use / attack with the item in hand | `UseItem`, `Use`, `Attack`, `PrimaryAttack`, `Fire` | Left mouse |
| Off-hand attack | `OffHandAttack`, `LeftAttack`, `SecondaryAttack` | Right mouse (when not blocking) |
| Block | `Block`, `Guard`, `Defend`, `Shield` | Right mouse |
| Reload | `Reload` | R |
| Rotate item (grid inventory) | `RotateItem`, `Rotate` | R (only while the inventory is open) |
| Quickslots | `QuickSlot1`…`QuickSlotN` | Z, X, C, V, B, N, F1, F2 |
| Draw / sheathe | `Sheathe`, `ToggleWeapon`, `DrawWeapon`, `Holster` | none |
| Armor Sets window | `ArmorSetUI`, `ArmorSets`, `Sets` | the Sets button |

Key prompts show only the device in use (keyboard and mouse, or gamepad).

**A Player Input component** (Unity's, *Behavior = Invoke Unity Events*) is needed only for these events:

| Action | Method |
|---|---|
| Look, Zoom | `PlayerCameraView.OnLook`, `OnZoom` |
| Change Camera / First Person / Third Person | `PlayerCameraView.OnChangeCamera`, `OnSwitchToFirstPerson`, `OnSwitchToThirdPerson` |
| Interact | `Player.OnInteract` — or add a **Player Interactor** instead, which reads an `Interact` action itself (created with E when missing), adds clicks, proximity and prompts, and opens NPCs ([NPC 01](../NPC/01-Interaction.md)) |
| Light / Heavy / Special / Alternate Attack | `WeaponController.OnLightAttack`… — or list the actions in `WeaponController ▸ Extra Attack Inputs` and wire nothing |
| Pause | `PauseMenuManager.OnPause` |

---

## 8. The scene: character creation and the pause menu

### Character creation screen

*Tools ▸ SimpleMovements ▸ Scene UI ▸ Build Character Creation Screen* builds a complete screen in the open scene, wired to
`CharacterCreationUI`:

| Part | What the player does |
|---|---|
| Name | Types the character's name (set on the player's `PlayerNameComponent`). |
| Race | Picks a race (Character Archetype assets of kind Race); its description and bonuses are shown. |
| Height | Slider limited to the race's height range. |
| Class | Picks a class (Player Class assets); its summary, trait points and unique traits appear. |
| Traits | Available traits (with costs for this race and class) and chosen traits; details with Add / Remove; points left. |
| Create Character | Spawns the **Player Prefab** at **PlayerSpawnPoint** with the class, race, height, traits and name applied, hides the screen and locks the cursor for the game. |

The builder finds the player prefab (the selected prefab, else the first prefab with a `PlayerStatusController`), every
Player Class, every Race archetype offered at creation and the creation traits (Trait Database), creates
`PlayerSpawnPoint` (move it where the player should appear) and a camera when the scene has none (turned off when the
player is created). On the `CharacterCreationUI`: **Rebuild Screen** (keeps the prefab, spawn point and lists) and
**Find Classes, Races & Traits**. With no races in the project the race step stays empty and is not required.

### Pause and settings menu

*Tools ▸ SimpleMovements ▸ Scene UI ▸ Build Pause & Settings Menu* (or **Build Menu UI** on a `PauseMenuManager`) builds the in-game menu:

* **Escape** (or the gamepad **Start** button) opens it **only while a player is in the game** — not on the creation
  screen. It pauses time, frees the cursor and turns the player's input off, so clicks on the menu never attack.
  Escape first closes an open inventory; inside the settings it goes back one page; while paused it resumes.
* **Pause window:** Resume, Settings, Main Menu (loads *Main Menu Scene Name*; hidden when that scene is not in Build
  Settings), Quit.
* **Character** (pause window button, or the settings tab): the player's name, race, class, level and height,
  resources, combat stats, traits, abilities (active and passive) and current buffs / debuffs. Hover a row: a trait,
  race or class lists every change it makes, a stat lists every source adding to it — **green** helps, **red** hurts.
* **Settings:** *Audio* (master, music, effects), *Graphics* (quality, resolution, fullscreen, VSync, brightness, FPS
  limit, FPS counter, render distance, motion blur, anti-aliasing, shadows), *Controls* (look sensitivity, invert Y),
  *Key Bindings*.
* **Key Bindings** (`KeyRebindingMenu`): every action of the Player map for keyboard and mouse or gamepad (movement
  keys one by one). Click a key and press the new one (Escape cancels); conflicts are reported; **Reset** per key and
  **Reset All Keys**. Bindings are saved (PlayerPrefs) and applied to every copy of the player's input at once.
* Settings are saved in PlayerPrefs and loaded at start. Look sensitivity and inversion are read by the player camera
  (`LookSettings`).

---

## 9. Checklist

- [ ] *Combat Settings* exists in `Resources`, and the player's layer is in *Character Layers*.
- [ ] `PlayerStatusController` validation shows no errors; `MovementStateMachine` shows no warnings.
- [ ] `WeaponController ▸ Hand Game Object` and `InventoryManager ▸ Hand Parent` are the same hand bone; *Off Hand Game Object* is the other hand.
- [ ] The `InventoryManager` inspector shows no problems (UI built).
- [ ] The scene has an `EventSystem` and a camera with a `CinemachineBrain`.
- [ ] Optional: `CharacterIdentity` (+ `CharacterBody`), `BodyPartController` with hitboxes, Armor Sets window,
      Absorption Settings + Ability Database.
- [ ] The game scene has the pause menu; the scene where characters are made has the character creation screen.
- [ ] The Player Setup Validator shows nothing red.
- [ ] Play: walk, sprint, jump, open the inventory, pick up an item, attack, block, use a quickslot.
