# Player 04 — Camera, Animation, Interaction & Abilities

**Scripts:** `Player/Model/PlayerCameraModel.cs`, `Player/Controller/PlayerCameraController.cs`,
`Player/View/PlayerCameraView.cs`, `Player/Animation/*`, `Player/Player.cs`,
`Player/Controller/AbilitiesStateMachine/*`, `Player/Controller/AbilityStateMachine/*`,
`Essentials/Ability/Player Ability/PlayerAbilityController.cs`, `Essentials/Ability/Core/AbilityDefinition.cs`.

---

## 1. Camera

The camera uses **Cinemachine**. Put your Cinemachine cameras under the player (or in the scene) and list them in
`PlayerCameraModel`:

| Field | Meaning |
|---|---|
| Camera List | One entry per camera: the camera object, *Is First Person*, a name, transition settings (smoothing, duration, curve) |
| Player Should Rotate By Camera Angle | The player turns to face where the camera looks (shooter style) |
| Camera Transform | The transform movement directions are taken from (camera-relative WASD) |
| Default FOV / Zoomed FOV / FOV Transition Speed | Zoom |
| Max Look Up / Max Look Down | Pitch limits |
| Default Noise Settings | Cinemachine noise used for camera shake |

`PlayerCameraController` switches cameras, zooms and shakes them (each camera gets a
`CinemachineBasicMultiChannelPerlin` for shake). `PlayerCameraView` receives the input events: **Look**, **Zoom**,
**Change Camera**, **First Person**, **Third Person** (wire them on the Player Input component, [01 §5](01-Player-Setup.md)).
The scene camera needs a `CinemachineBrain`.

---

## 2. Animation

`PlayerAnimationModel` sets Animator parameters; `PlayerAnimationController` plays attack and action animations
(`AttackAnimationManager`, `MovementAnimationHandler`, `ActionAnimationHandler`). **Parameters that do not exist in
your Animator Controller are ignored**, so add only what you use:

| Group | Parameters |
|---|---|
| Movement (bool) | `IsWalking`, `IsRunning`, `IsCrouching`, `IsDashing`, `IsRolling`, `IsJumping`, `IsFalling` |
| Movement (float) | `MovementX`, `MovementZ`, `MovementSpeed`, `AirTime` |
| Actions (trigger) | `RollTrigger`, `DashTrigger`, `JumpTrigger`, `LandTrigger`, `HitTrigger`, `DeathTrigger` |
| Attacks | `IsAttacking` (bool), `AttackCombo` (int), `AttackTrigger`, `LightAttackTrigger`, `HeavyAttackTrigger`, `ChargedAttackTrigger`, `SpecialAttackTrigger`, `AlternateAttackTrigger` (triggers) |
| State (bool) | `IsInputLocked`, `IsBlocking`, `IsStunned` |

`IsAttacking`, `MovementX` and `MovementZ` are checked at start (a warning lists the missing ones).

**Weapon attacks:** an attack with an *Animation Clip* plays that clip stretched over the attack's duration; without a
clip it fires the trigger of its input (`AttackTrigger` for Normal, `LightAttackTrigger`, `HeavyAttackTrigger`,
`SpecialAttackTrigger`, `AlternateAttackTrigger`). The **Attack Preview** window plays attack clips on the player in
the editor ([Inventory 06](../Inventory/06-Editor-Tools.md)).

---

## 3. Interaction

`Player` casts a ray from the camera through the mouse / crosshair (up to the interaction distance) and shows the
**interaction prompt** when it points at:

| Target | Component | Interact does |
|---|---|---|
| A world item | `ItemPickable` | Picks it up (holding Interact for items with an *Interaction Time*; the circle fills) |
| A chest | `Storage` | Opens the storage panel |
| Anything else | `Interactable` | Calls its interaction |

The prompt (circle + text) is built by the inventory UI builder and assigned to `Player` ▸ Interaction UI, Filling
Circle and Interaction Text.

---

## 4. Ability keys

An **ability key** = an input + the ability it casts. It is stored in two places, which the inspectors edit as one:

* a line in `AbilitiesStateMachine` ▸ *Keys* (the input action), and
* an `AbilityStateMachine` component (the ability), on a child under **Ability Keys**.

`PlayerAbilityController` casts them: it aims (crosshair or mouse, *Aim Layers*, *Max Aim Distance*), shows
click-to-confirm previews, and lists every key in its inspector.

### Adding a key

1. Select the player's `PlayerAbilityController` (or `AbilitiesStateMachine`).
2. Add a key, pick its **input action** (▾ lists the project's actions) and its **Ability**.
3. Create the ability with **Preset ▾** (saved next to the player prefab) or
   *Assets ▸ Create ▸ Scriptable Objects ▸ Ability ▸ Ability From Preset…*, then edit it.
4. In Play mode, **Cast Now** tests a key.

Repair tools: **Repair Ability Keys**, **Add Keys For Unused**, **Remove Broken**, **Auto-assign References**.
Old `AbilityEffectSO` abilities can be converted (*Tools ▸ Abilities ▸ Convert Selected Legacy Abilities ▸ For Player*).

### Ability Definition, in short

| Section | Fields |
|---|---|
| Identity | Name, icon, tags (mobs use them: Gap Closer, Escape, Defensive…) |
| Timing | Cast time (wind-up with telegraph), launch, active, cooldown, charges, global cooldown |
| Casting rules | Moving while casting, interruption by damage / stun, refunds |
| Targeting | Unit, point or direction; range; **Target Filter** (Self, Allies, Enemies, Neutral, Party) |
| Actions | What it does: hit areas, projectiles, beams, dashes, summons, barriers… each with **Hit Settings** (filter, effects such as damage, heal, stun, knockback, and optional **Target Rules**: friendly fire, kinds, factions) |

Mobs use the same ability assets ([Mobs 03](../Mobs/03-Combat-and-Abilities.md)). Who an ability can hit
(teams, factions, parties, friendly fire) is explained in
[Inventory 09](../Inventory/09-Teams-Factions-and-Targeting.md).
