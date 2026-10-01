# Player 01 — Player Setup

**Scripts:** `Player/Player.cs`, `Player/Controller/MovementStateMachine/*`, `Player/Controller/Movement/*`,
`Player/Model/*`, `Player/RPGSystem/PlayerStatusController.cs`, `Player/Controller/AvailabilityStateMachine/*`,
`Player/Animation/*`, `Player/Controller/PlayerCameraController.cs`, `Player/View/*`.

---

## 1. Before you start

| Needed | Why | How |
|---|---|---|
| **Combat Settings** asset in a `Resources` folder | Layers (characters, obstacles, ground), PvP and friendly fire rules, AI budgets | *Tools ▸ Abilities ▸ Create Combat Settings (Resources)* |
| **Trait Database** in `Resources` | Traits the player can learn | *Tools ▸ Traits ▸ Create Trait Database (Resources)* |
| **Input Actions** asset with a **Player** map, *Generate C# Class* on, class name `PlayerInput` | Movement, abilities, inventory and traits read it | See [02 §2](02-Movement-and-Input.md) |
| Packages | Input System, TextMeshPro, Cinemachine | *Window ▸ Package Manager* |

---

## 2. Recommended hierarchy

```
Player                              ← CharacterController + the components in §3 (tag "Player")
├── Model                           ← the character mesh + Animator
│   └── … Hand bone (e.g. RightHand) ← WeaponController ▸ Hand and InventoryManager ▸ Hand Parent point here
├── Cameras                         ← Cinemachine cameras listed in PlayerCameraModel (third person, first person…)
├── Ability Keys                    ← created by the ability key tools (one child per key)
└── Inventory Canvas                ← built automatically (hotbar, inventory, equipment, status bars, prompt)
```

The canvas is created **inside** the player so the prefab carries its own UI. An `EventSystem` is never added to the
prefab (the scene needs one; the inventory adds one at runtime when the scene has none).

---

## 3. Components, in the order to add them

| # | Component | What it does | Setup |
|---|---|---|---|
| 1 | `CharacterController` | Moves the player and collides | Height / radius to fit the model |
| 2 | `PlayerMovementModel` | Movement settings: gravity, jump force, stamina costs; references to the controller and body | **Auto-assign References** (Controller = here, Player Transform = here, Shell = here) |
| 3 | `PlayerMovementController` | Gravity (FixedUpdate), rotation toward the camera (LateUpdate), ground check | **Auto-assign References** |
| 4 | `PlayerStatusController` | Holds every status manager, the class, experience, traits, armor sets | **Complete Setup Wizard** (adds and wires everything below) |
| 5 | Status managers | `HealthManager`, `StaminaManager`, `ManaManager`, `HungerManager`, `ThirstManager`, `SleepManager`, `SanityManager`, `BodyHeatManager`, `OxygenManager`, `WeightManager`, `SpeedManager` | Added by the wizard |
| 6 | `ExperienceManager`, `TraitManager` | Levels and traits | Added by the wizard |
| 7 | `PlayerDashModel`, `PlayerRollModel` | Dash / roll speed, duration, cooldown, stamina | Added by the wizard |
| 8 | `AvailabilityStateMachine` | Unaffected / Stunned / Dead; blocks movement, abilities and attacks | Add; nothing to assign |
| 9 | `MovementStateMachine` | The movement states ([02](02-Movement-and-Input.md)) | **Auto-assign References**; it also adds a `PlayerAnimationModel` when an Animator exists |
| 10 | `PlayerAnimationModel`, `PlayerAnimationController` | Animator parameters and attack animations | On the player (or the model); Animator = the model's |
| 11 | `PlayerCameraModel`, `PlayerCameraController`, `PlayerCameraView` | Camera list, FOV, first / third person, look input, shake | List your Cinemachine cameras in the model |
| 12 | `Player` | Interaction: what is under the cursor, pick up items, open storage | Cam, Inventory Manager, Interaction UI, Filling Circle (assigned by the UI builder) |
| 13 | `InventoryManager` | Inventory, hotbar, equipment, use item | Selecting the player builds the UI ([Inventory 06 §3a](../Inventory/06-Editor-Tools.md)) |
| 14 | `WeaponController` | Attacks with the weapon in hand | **Hand** = the hand bone (where held items appear) |
| 15 | `AbilitiesStateMachine`, `PlayerAbilityController` | Ability keys ([04 §4](04-Camera-Animation-Abilities.md)) | Optional |
| 16 | `AudioSource` | Item and interaction sounds | Optional |

`CombatEntity`, `EquipmentManager`, `CombatStats` and `ArmorSetManager` are added automatically at runtime when they
are missing; add them yourself only to change their settings (team, faction, party, body size, immunities).

### The setup buttons

| Inspector | Button | Does |
|---|---|---|
| Player Status Controller | **Complete Setup Wizard** | Creates missing managers and models, assigns every reference, validates, applies the class's values |
| | Auto-Assign All Components / Create Missing Components | The two halves of the wizard |
| | Validate Setup / Validate Status Values / Apply Reasonable Defaults | Checks and default values |
| Movement State Machine, Movement Controller, Movement Model | **Auto-assign References** | Fills empty references from the object, its children and parents |
| Inventory Manager | **Build / Repair UI Now**, **UI Builder…** | Builds the inventory UI, status bars and prompt |
| Availability State Machine (Play) | Stun 2s, Silence 3s, Clear, Kill, Revive | Test states |
| *Tools ▸ Player Status* | Validate All Controllers / Auto-Setup All Controllers | For every player in the open scenes |

---

## 4. Movement and status values

Starting values (details in [02](02-Movement-and-Input.md) and [03](03-Status-Classes-Traits-XP.md)):

| Setting | Where | Suggested |
|---|---|---|
| Gravity | Movement Model | `-9.81` (must be negative) |
| Gravity Multiplier | Movement Model | `2` |
| Jump Force | Movement Model | `6` → about 0.9 m high with multiplier 2 |
| Speed | Speed Manager | `5` m/s walking; running / crouching multipliers on the same component |
| Dash Speed / Duration / Cooldown | Dash Model | defaults `35`, `1`, `4` (shorter durations give a snappier dash) |
| Roll Speed / Duration / Cooldown | Roll Model | defaults `2`, `0.25`, `4` |
| Should Consume Stamina | Movement, Dash, Roll Models | On for survival games |

---

## 5. Input

Two kinds of input are used:

* **The generated `PlayerInput` class** (from the Input Actions asset) is read directly by the movement states,
  the ability keys, the traits and the inventory. One copy is shared per player (`SharedPlayerInput`). Nothing to
  wire.
* **A Player Input component** (Unity's, *Behavior = Invoke Unity Events*) calls these methods:

| Action | Method |
|---|---|
| Look | `PlayerCameraView.OnLook` |
| Zoom | `PlayerCameraView.OnZoom` |
| Change Camera / First Person / Third Person | `PlayerCameraView.OnChangeCamera`, `OnSwitchToFirstPerson`, `OnSwitchToThirdPerson` |
| Interact | `Player.OnInteract` (pick up items, open storage, use interactables; hold for items with an interaction time) |
| Light / Heavy / Special / Alternate Attack | `WeaponController.OnLightAttack`, `OnHeavyAttack`, `OnSpecialAttack`, `OnAlternateAttack` — or, simpler, assign the actions to `WeaponController ▸ Extra Attack Inputs` and wire nothing |
| Pause | `PauseMenuManager.OnPause` |

The inventory key, the use / attack key and the armor sets window are **not** wired here: the `InventoryManager`
finds them in the input actions by name (and falls back to Tab / I and the left mouse button) — see
[Inventory 02 §3](../Inventory/02-Items-and-Inventory.md).

---

## 6. Checklist

- [ ] The player is on a layer included in *Combat Settings ▸ Character Layers* (its collider must be found by hit queries).
      The **Player** tag is optional: players are recognised by their `PlayerStatusController` / Combat Entity.
- [ ] `PlayerStatusController` validation shows no errors.
- [ ] `MovementStateMachine` has every reference (no warning in its inspector).
- [ ] `WeaponController ▸ Hand` and `InventoryManager ▸ Hand Parent` are the same hand bone.
- [ ] The `InventoryManager` inspector shows no problems.
- [ ] The scene has an `EventSystem` and a camera with a `CinemachineBrain` (when using Cinemachine cameras).
- [ ] Play: walk, sprint, jump, open the inventory (Tab), pick up an item (Interact), attack (left click).
