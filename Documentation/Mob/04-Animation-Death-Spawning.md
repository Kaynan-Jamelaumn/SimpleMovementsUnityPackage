# Mobs 04 — Animation, Death & Spawning

**Scripts:** `Mob/AI/MobAnimationDriver.cs`, `Mob/AI/MobProfile.cs` (Animation, Death), `Mob/Controller/MobStatusController.cs`,
`Mob/Controller/MovementStateMachine/States/MobDeadState.cs`, `Procedural/World/Spawnable/MobSpawner.cs`,
`MobSettings.cs`, `SpawnableMob.cs`, `Procedural/Dungeon/DungeonMobSpawner.cs`, `Config/DungeonEncounterTable.cs`.

---

## 1. Animation

`MobAnimationDriver` drives the Animator from what the mob is doing. Parameter **names are set in the Mob Profile**
(*Animation* section) and **missing parameters are ignored**, so a simple controller works too.

| Profile field | Default name | Type | Set to |
|---|---|---|---|
| Speed Parameter | `Speed` | float | Movement speed (m/s) |
| Normalized Speed Parameter | `SpeedPercent` | float | Speed ÷ run speed (0–1) |
| Moving Bool | `IsMoving` | bool | Moving |
| In Combat Bool | `InCombat` | bool | Fighting |
| Move X / Move Y Parameter | `MoveX`, `MoveY` | float | Strafing blend trees: sideways and forward velocity (−1…1) |
| Hit Trigger | `Hit` | trigger | Hurt |
| Alert Trigger | `Alert` | trigger | Notices an enemy |
| Dodge Trigger | `Dodge` | trigger | Dodging |
| Stunned Bool | `Stunned` | bool | Stunned |
| Death Trigger | `Die` | trigger | Death |

**Old controllers** with states named `Idle`, `Moving`, `Chasing`, `Patrol` (and `Death` / `Die`) keep working: *Cross Fade Legacy States* (on by default in the profile) cross-fades to them.

Ability animations (wind-up, release) are set on each Ability Definition (*Presentation*), and play on the mob's
Animator.

## 2. Death

1. Health reaches 0 → the **Dead** state: the `Die` trigger plays, the AI stops, `Mob.Died` is raised (with the
   killer), spawners count the kill.
2. With *Disable Colliders On Death* the corpse stops blocking.
3. After *Destroy Delay* seconds (profile, or `MobStatusController ▸ Destroy Delay` when 0 or more) the mob is removed.

Killing a mob can give XP and abilities (absorption, [03 §6](03-Combat-and-Abilities.md)).

## 3. Spawning

### On the terrain (open world)

`EndlessTerrain ▸ Mob Settings` lists **Spawnable Mob** entries; every terrain chunk spawns mobs around the player
and removes them when the player leaves (kills are remembered until the respawn delay passes).

| Entry field | Meaning |
|---|---|
| **Mob Prefab** | Required. A prefab with a `NavMeshAgent` and the mob component |
| Max Instances | Most of this type alive in one chunk |
| Allowed / Preferred / Avoided Biomes | Where it lives |
| Limit Height, Min / Max Preferred Height, Max Spawn Slope | Terrain limits |
| Spawn Weight, Rarity Level | How common it is |
| Pack settings | Pack animals spawn in groups |

The chunk's NavMesh must be built before mobs appear. Details: [Terrain 15 — World Portals and Mobs](../Terrain/15-Portals-and-Mobs.md).

### In dungeons

A **Dungeon Encounter Table** lists encounter entries: **Prefab**, *Boss* (placed once in the boss room), *Weight*,
*Cost* (budget: tougher mobs cost more), *Pack Size*. Terrain mob entries can be imported
(`SpawnableMobImport`). Details: [Dungeon 08 — Population](../Dungeon/08-Population.md).

### By hand or from code

Place the prefab on a NavMesh, or `Instantiate` it — its home is where it spawns (patrol points *Relative To Home*
follow it).

### Summons

Abilities can summon mobs. A summon follows its summoner, fights its enemies, belongs to its team, faction and party,
and is never absorbable.
