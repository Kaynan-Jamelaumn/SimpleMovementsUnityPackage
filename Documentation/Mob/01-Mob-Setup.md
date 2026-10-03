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
a **`<Type> Basic Attack`** ability asset, so both can be seen and edited.

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

## 5. Checklist

- [ ] No errors in the mob inspector's Checks.
- [ ] Type set; Preys contain `Player` if it should attack the player.
- [ ] A Mob Profile is assigned.
- [ ] Collider layer included in *Combat Settings ▸ Character Layers*.
- [ ] The mob stands on a baked NavMesh (blue overlay in the Scene view with the AI Navigation overlay).
- [ ] Animator Controller assigned (parameters optional).
