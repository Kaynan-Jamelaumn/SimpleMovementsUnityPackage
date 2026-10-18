# 04 - Status and death: health, resources, crowd control, buffs, XP, respawn

A character's combat state is decided by the server. That covers health and the other resources, death, crowd control,
buffs and debuffs, speed, weight, experience and traits. Clients only show it. Single player is unchanged: without
Fish-Net, or with Fish-Net installed but no session running, nothing is a replica and every status rule runs locally as
before.

## How it works

### The server simulates, the clients show

The server (a dedicated server, or the host) runs all of the existing status code unchanged: regeneration, hunger and
thirst decay, sleep, sanity, body heat and oxygen routines, timed effects, `CombatEntity` damage, heals, crowd control,
threat and death. `GameNet.NetCombatStatus` reads the results every network tick and writes them into SyncVars.
Unchanged values are not sent again: health, mana and the survival values are rounded to 0.1, and positive values never
round down to 0.

On a pure client, every copy of a networked character becomes a **replica** (`NetCombatStatus.OnStartClient`):

| Part | Replica switch | Effect on the client |
|---|---|---|
| `StatusManager` (health, mana, hunger, thirst, sleep, sanity, body heat, oxygen, weight) | `SetReplica(true)` | No regeneration, decay or coroutine. Consume, restore, max value, factor and effect calls are ignored. Only `ApplyReplicated(current, max)` (max first, then current) and `SetCurrentValue` write. |
| `StaminaManager` | `SetReplicatedLimits(true)`, **not** a replica | The movement module predicts stamina and its reconcile corrects it (see 02). Only the limits (max, regeneration amount, gain and loss factors) come from the server, through `ApplyReplicatedLimits`. |
| `SpeedManager` | `SetReplica(true)` | Shows the server's speed, which already includes slows, roots, traits, equipment and level-ups. Local speed writes and effects are ignored, so a slow is never applied twice. |
| `CombatEntity` | `SetReplica(true)` | `ApplyDamage`, `ApplyHeal`, `HandleDeath`, `Revive`, `ApplyControl`, `ClearControl`, threat, invulnerability and knockbacks are ignored. `LateUpdate` keeps only the velocity estimate (no health tracking, speed debuff or AI noise). |
| `MobStatusController` | via the entity | No death check, `Kill` or `ApplyEffect`. It never spawns loot or destroys the mob. |
| `PlayerStatusController` | via the entity | `ApplyEffect` is ignored. The class's starting traits and points are not granted locally. Sleep requests go to the server. |
| `ExperienceManager` | rules check | `AddExperience` does nothing where the rules do not run. `UpgradeStat` sends a request to the server. |
| `TimedStatModifiers` | via the entity | `Apply` does nothing. The list holds display-only entries from `SetReplicatedView`. |
| `TraitManager` | `IsMirror = true` | Follows the server's trait list. Starting traits are not granted and temporary traits do not expire locally. The movement traits (double jump, wall climb, glide) exist where movement is predicted or replayed. |

The host never becomes a replica. Its change handlers ignore `asServer` and host duplicates, so nothing is applied
twice. Offline, `IsReplica` is always false.

### What is replicated

| Data | Who sees it | Type and rate |
|---|---|---|
| Health (current, max) | observers | `SyncVar<Resource>`, 0.05 s |
| Dead flag | observers | `SyncVar<bool>`, reliable. A client calls `CombatEntity.SetReplicatedDeath`. |
| Crowd control (stun, silence, root, slow, taunt, invulnerability) | observers | `SyncVar<ControlState>` holds **end ticks**, so nothing is resent while a timer runs down. Clients convert the ticks to local `Time.time`, and stun and silence are mirrored into `AvailabilityStateMachine`. |
| Level | observers | `SyncVar<int>`. `ExperienceManager.ApplyReplicated` raises `OnLevelUp` once when the level rises. |
| Speed values (current, base, run and crouch multipliers) | observers | exact floats. Spectators run the movement step too. |
| Weight (current, max) | observers | exact floats, because weight changes movement speed |
| Stamina limits | observers | max, increment and factors (the value itself travels in the movement reconcile) |
| Buffs and debuffs | observers | `SyncList<BuffView>` (name, debuff, element, duration, end tick), rebuilt on `TimedStatModifiers.Changed` |
| Trait names | observers | `SyncList<string>` of the active traits, rebuilt on `TraitManager.OnTraitsChanged` |
| Mana, hunger, thirst, sleepiness, sanity, body heat, oxygen | owner only | `SyncVar<Resource>`, 0.1 to 0.25 s |
| Sleeping / collapsed, underwater / altitude / ventilation, oxygen tank, environment temperature | owner only | `SyncVar<SurvivalState>` |
| XP, XP to next level, skill / attribute / stat points, trait points | owner only | `SyncVar<Progress>` |
| Respawn countdown | owner only | `SyncVar<uint>` end tick (`NetCombatStatus.RespawnSecondsLeft`) |

Late joiners receive the current values of every SyncVar and SyncList when the character spawns for them. The
component applies everything once in `OnStartClient` and again on the first frame after, which covers parts whose
`Start` runs later.

Threat tables, regeneration timers, coroutine handles and effect lists stay on the server and are never replicated.

### Hits, heals and death on clients (presentation only)

`CombatEntity` has presentation events that fire the same way offline, on the host and on clients:

- `DamageFeedback(DamageInfo)`: where the rules run, it fires right after `Damaged`, and also for hits fully stopped by a
  block or parry (amount 0). On a client it fires when the server reports the hit.
- `HealFeedback(float)`: healing actually received.
- `ReplicatedDamage`, `ReplicatedHeal`, `ReplicatedLifeChanged(bool dead)`, `ReplicatedControlChanged`: client copies
  only.

The server sends hits through `[ObserversRpc(ExcludeServer = true)] ObserversHit(HitEvent)`, which carries the amount,
element, damage type, delivery, flags (crit, periodic, blocked, parried, reflected), source `NetworkObject`, hit point
and direction. Heals go through `ObserversHeal(float)`. Clients call `CombatEntity.PresentReplicatedDamage` /
`PresentReplicatedHeal`. The gameplay events `Damaged`, `CombatEvents.Damaged`, `Died`, `CombatEvents.Killed` and
`Revived` are **never** raised on a client. So loot drops, ability absorption, kill rewards, on-kill heals and ability
pickups happen once, on the server.

Damage numbers, hit flashes and similar effects should subscribe to `DamageFeedback`, which works everywhere. Death
animations and health bars should use `ReplicatedLifeChanged` together with `IsDead`.

### Death and respawn

Death runs exactly as before on the server: `CombatEntity.HandleDeath`, then `Availability.Kill`, `Died` and
`CombatEvents.Killed`. A dead mob is despawned through `GameNet.NetSpawn.Destroy` after its destroy delay. On clients,
the dead flag puts the replica's availability state into Death, so the existing `CanMove` / `CanCastSpells` / `CanAct`
gates block input.

Networked players respawn on the server (`NetCombatStatus`, players only):

1. After `respawnDelay` seconds (default 5) the owner sees the countdown through `RespawnSecondsLeft`.
2. Inside a running dungeon run, the fallen wait for a rest point. When every participant is dead and `endDungeonOnWipe`
   is on, the run ends (`DungeonSession.Exit`), the group leaves, and the respawn continues.
3. The player is sent to the City spawn point. With the hub world this uses
   `HubWorldDirector.TravelToCity(state, respawnPenalties, toSpawn: true)`. Without it, the player goes to the City
   `RealmScene`'s spawn pose, or failing that to where they first spawned.
4. `CombatEntity.Revive(respawnHealthFraction)` revives them with that fraction of their max health (default 0.5).
   `spawnProtection` seconds of invulnerability follow.

A player revived some other way (a rest point, a script) cancels the pending respawn. Duels never kill, so a duel
never triggers a respawn. Offline nothing changes: there is still no automatic respawn.

### Requests from the owner (validated on the server)

- `ExperienceManager.UpgradeStat(stat)` on the owning client calls `ServerUpgradeStat`. The server checks that the
  player is alive, that the name is one of `AvailableStats` (64 characters at most), and the rate limit
  (`requestInterval`). Then `ExperienceManager.UpgradeStat` checks and spends the points as usual.
- `PlayerStatusController.StartSleeping` / `StopSleeping` on the owning client calls `ServerSetSleeping`. The server
  refuses to put a dead or stunned player to sleep, and applies the rate limit.

Clients never send amounts, max values, damage, durations or positions.

## What the prefabs need

- **Player prefab root** (next to `NetworkObject` and `NetPlayer`): `GameNet.NetCombatStatus`. The respawn settings live
  on it.
- **Networked mob prefab roots** (next to `NetworkObject` and `NetMob`): `GameNet.NetCombatStatus`. The respawn settings
  are ignored for mobs.
- Tools ▸ SimpleMovements ▸ Network adds it automatically (`NetworkSetupMenu.PrefabComponents`).
- Traits must be findable by name on clients: in the player's `TraitManager` database, its starting traits, the
  class's starting traits, or `Resources/SimpleMovements/Databases/TraitDatabase`.

## Testing locally

1. Start a host and join a second client on 127.0.0.1 (see 01).
2. Hit the client's player with a mob or an ability. Health drops on both screens, damage feedback plays on the client,
   and the hit player's HUD shows the server's health.
3. Stun or slow the client's player. It cannot move for exactly the stun's length, and the slow is not applied twice.
4. Let the client's player die outside a dungeon. Its copy enters the Death state on both machines, and after
   `respawnDelay` it reappears at the City spawn point with half health and 2 s of invulnerability.
5. Kill a mob as the client. Loot drops once (on the server) and the mob is despawned for everyone.
6. Wait for hunger and thirst to decay on the client. The owner's bars follow the server, and other clients never
   receive those values.
7. Join late. A second client that joins after the fight sees the right health, buffs and dead or alive state at once.
8. Edit Mode tests: `GameNet.Tests.StatusNetTests` (rounding, tick conversion, flags, trait list diff, request
   validation, and the replica switches of the managers, speed, combat entity, experience and timed buffs).

## Limitations

- Class, race, archetypes, height and attribute upgrades (`CharacterIdentity`) are not replicated. A client's copy uses
  the prefab's class, and the derived `CombatStats` totals on the client's character sheet can differ from the
  server's. Gameplay always uses the server's values.
- Suspended traits are sent as absent, so clients drop them until the server resumes them.
- Spectators do not receive other players' mana, survival values or XP (owner only), and they never show other
  players' stamina.
- Hit events carry no ability or weapon id. Effects that depend on the weapon should use the attacker's replicated
  equipment.
- The dead flag and health arrive a few ticks after the hit. A client may briefly see 0 health before the death state.
- Survival drains that use `Time.deltaTime` inside 1 s coroutines depend on the server's frame rate (cap it with
  `HeadlessServer`).
