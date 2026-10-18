# Mobs 01 — Mob Setup

**Scripts:** `Mob/Mob.cs`, `Mob/Controller/MobActionsController.cs`, `MobStatusController.cs`,
`MobAbilityController.cs`, `Mob/Controller/MovementStateMachine/*`, `Mob/AI/*`,
`CustomEditor/MobEditor/MobQuickSetup.cs` (`MobSetupUtility`), `Essentials/Ability/Core/CombatEntity.cs`.

---

## 1. Before you start

| Needed | Why |
|---|---|
| **Combat Settings** in `Resources` (*Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Combat Settings (Resources)*) | Character layers (abilities only hit colliders on them), AI budgets (how many mobs attack at once), telegraphs |
| A baked **NavMesh** (AI Navigation package: a `NavMeshSurface`, or the terrain / dungeon builds it) | Mobs walk on it |
| The mob's model with an **Animator** | Optional, but mobs without animation look frozen |

---

## 2. Quick setup

*Tools ▸ SimpleMovements ▸ Mobs ▸ Create New Mob* creates a new mob; right-click an existing object ▸
*SimpleMovements ▸ Quick Setup Mob* turns it into one; the mob inspector's **Auto-Configure All Components** does the same.
They add **only what is missing** and keep existing settings:

| Added | Why |
|---|---|
| `CapsuleCollider` | So weapons, projectiles and abilities can hit it |
| `Rigidbody` (kinematic) | Hit detection; a non-kinematic body would fight the NavMeshAgent |
| `NavMeshAgent` | Movement |
| `Model / VisualModel` children with an `Animator` | Where your model goes |
| `MobStatusController` + `HealthManager` + `SpeedManager` | Health (damage, death) and speed |
| `MobActionsController` | **The mob** (type, preys, team, faction, home, patrol, basic attack) |
| `MobMovementStateMachine` | **The AI** (perception, brain, movement, combat) |
| `MobAbilityController` | Abilities |
| `CombatEntity` | Team, faction, party, body size, crowd control immunities |

*Create New Mob* also creates a **`<Type> Profile`** asset and, when the mob would use the invisible automatic bite,
a **`<Type> Basic Attack`** ability asset, so both can be seen and edited. They go in
`Assets/Resources/SimpleMovements/Mobs/<Type>/` (one folder per mob type; the mob's abilities and body parts too).

The setup also **sizes things to your model**: a model already under the mob gets the Animator on its root (no
placeholder capsule is added), the capsule collider is fitted to the model's meshes and the NavMeshAgent takes the
capsule's radius and height. Then it applies every safe fix of the Mob Setup Validator (below) and logs how many
problems are left.

*Tools ▸ SimpleMovements ▸ Mobs ▸ Add Missing Components* repairs the selected mob; *Tools ▸ SimpleMovements ▸ Mobs ▸ Help* summarises the steps.

---

## 3. Step by step

1. **Model.** Put the mesh under `Model / VisualModel`; assign its **Animator Controller** (see
   [04 §1](04-Animation-Death-Spawning.md) for the parameters it can use).
2. **Collider and agent.** Fit the capsule to the body; set the NavMeshAgent's radius about the same as the capsule's
   (the inspector warns when they differ a lot). The collider's layer must be in
   *Combat Settings ▸ Character Layers*.
3. **Identity** (on the mob component):
   * **Type** — `Wolf`, `Sheep`, `Bandit`… Other mobs hunt or fear it by this name; mobs of the same type are allies.
   * **Preys** — types it hunts: `Player` for players, other types (`Sheep`).
   * **Team Override** — puts different types on one side (a pack of wolves and their alpha).
   * **Faction** — optional `CombatFaction` asset: allies and enemies between sides ([02 §5](02-AI-Profiles-and-Behaviour.md)).
4. **Profile.** Assign a **Mob Profile**, or *Create From Preset ▾* / *Create From These Settings* in the inspector.
   Shared by every mob of the type; **Make Unique Copy** for a one-off.
5. **Health and speed.** `HealthManager` max / current value, `SpeedManager` speed (the profile's walk / run
   multipliers apply on top).
6. **Abilities.** Add Ability Definitions to `MobAbilityController` (*Add Ability From Preset ▾*), or leave it empty
   to use the basic attack built from *Bite Damage / Attack Distance / Bite Cooldown* ([03](03-Combat-and-Abilities.md)).
7. **Home and patrol** (optional). A **Patrol Route** (a transform whose children are the points, outside the mob)
   or **Patrol Points** (*Relative To Home* for prefabs placed by spawners).
8. **Save as a prefab** and bake the NavMesh. Press Play.

---

## 4. The mob inspector

| Section | Shows / does |
|---|---|
| Quick Setup | **Auto-Configure All Components** |
| Profile | The profile's one-line summary, *Create From Preset ▾*, *Make Unique Copy*, *Select*; in Play mode *Save Runtime Profile As Asset* |
| Components | Which required components exist, with **Add** buttons |
| Checks | Problems with **Fix** buttons: no NavMeshAgent, no AI, no health, no collider, empty Type, misspelt preys, non-kinematic Rigidbody, agent / collider size mismatch, layer not in Character Layers, not on a NavMesh, Aggressive with no preys, patrol route inside the mob… |
| Abilities | What the mob can use, and its absorption table |
| Live (Play) | Current state, target, awareness, health |

---

## 5. Mob Setup Validator

**Tools ▸ SimpleMovements ▸ Mobs ▸ Mob Setup Validator** (also *Validate ▸ Mobs*, and the **Mobs** checks of the Project
Validator). It checks every mob of the **selection**, the **open scenes** or **every mob prefab of the project**, lists the
problems per mob (errors first), and fixes them one by one (**Fix**), per mob (**Fix Safe**, **Configure**) or all at
once (**Fix All Safe (all)**, **Auto-Configure (all)**). Prefabs are fixed in their prefab file, so every instance follows.
The mob inspector's **Setup checks** use the same checks, with **Fix All Safe** and **Validate All Mobs...** buttons.

| Area | What is checked | Fix |
|---|---|---|
| Components | Mob, NavMeshAgent, AI (MobMovementStateMachine), Status Controller, Health / Speed Managers, Ability Controller, CombatEntity, collider, Rigidbody; more than one Mob in a hierarchy; missing scripts | Add (safe), Add Fitted Capsule, Remove missing scripts |
| References | The AI's Mob / agent / status / Animator, the Status Controller's managers, the Ability Controller's mob and Animator | Wire (safe) |
| Identity | Type empty or not in the Mob Types list, hunting its own type, empty or misspelt preys | Use Name, Add To List, Remove |
| Physics | Rigidbody not kinematic / with gravity, trigger-only collider, agent vs capsule radius and height, root scale and non-uniform scale, capsule much smaller or bigger than the model, collider layer not in Character Layers, deleted agent type, agent speed 0, not on a NavMesh | Make Kinematic, Match Agent, Fit To Model, Use layer…, Use Humanoid |
| Model & animation | No mesh, still the placeholder capsule, no Animator, Apply Root Motion on, no controller, no avatar, the controller missing the profile's movement parameters / Moving states, death and hit triggers, parameters of the wrong type | Add Animator, Turn Off root motion |
| Profile | No profile, the profile's own checks, Aggressive without preys | Create Profile, + Player |
| Patrol | Route inside the mob, empty route, scene route on a prefab, absolute points on a prefab | Make Relative |
| Attacks | The ability controller's checks, no way to hurt anything, the hidden runtime bite | Create Basic Attack asset |
| Status | Max health 0, current above max, base speed 0 | Set values |
| Drops | Item Drop On Death + Item Spawner (double drops), drops nothing, items without prefabs in its table | Add Item Drop On Death |
| Network | A mob prefab without a NetworkObject (when Fish-Net is installed) | — |

**Safe** fixes only add, wire or correct something plainly wrong; anything that changes a choice of yours (layers, the
collider size, preys, removing components) is a separate button.

## 6. Checklist

- [ ] No errors in the mob inspector's Checks.
- [ ] Type set; Preys contain `Player` if it should attack the player.
- [ ] A Mob Profile is assigned.
- [ ] Collider layer included in *Combat Settings ▸ Character Layers*.
- [ ] The mob stands on a baked NavMesh (blue overlay in the Scene view with the AI Navigation overlay).
- [ ] Animator Controller assigned (parameters optional).
