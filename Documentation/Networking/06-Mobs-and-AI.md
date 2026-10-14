# 06 - Mobs and AI: server-run AI, network-spawned mobs, replicated movement and animation

Mobs are run by the server. Their senses, decisions, threat, abilities, NavMesh movement, loot and death are all
decided there. Clients get a copy that only shows the server's mob. Single player is unchanged: without Fish-Net, or
with Fish-Net installed but no session running, every mob thinks for itself exactly as before.

## How it works

### Where the AI runs

`MobMovementStateMachine` runs its AI only where the mob's rules run (`GameNet.NetAuthority.RunsRules`): in single
player, on a host, and on a dedicated server. On a pure client the copy of a networked mob is a **replica**
(`MobMovementStateMachine.IsReplica`). A replica keeps all of its components, but:

- perception is unsubscribed, the brain, ability selector, combat coordinator and threat logic never tick, and the
  motor gets no orders;
- its `NavMeshAgent` is disabled, because the agent would pull the copy back onto its own path. The server's position
  and rotation arrive through the prefab's `NetworkTransform`;
- `MobStatusController` and `CombatEntity` are replicas too (status module). They ignore local damage, effects and
  death checks.

A mob with a network identity counts as a replica on a client even before its spawn message is processed
(`GameNet.MobNetSpawn.HasNetworkIdentity`), so its AI never runs for a single frame there. A mob **without** a network
identity on a pure client (a scene mob or a prefab that was never networked) would fight, drop loot and wander on
that machine only. It is switched off instead, and a warning is logged once.

When the session ends and a copy outlives it (a scene mob), the copy becomes a normal mob again: the agent is turned
back on (warped to where the copy stands) and perception is subscribed again.

### Animation: replicated state, not a NetworkAnimator

`MobAnimationDriver` already works out every animator parameter from what the mob is doing: speed, normalised speed,
moving, in combat, stunned, the MoveX/MoveY blend, and legacy Idle/Moving/Chasing/Patrol cross-fades. On a replica it
runs from replicated data instead of the local AI:

- **Speed and direction** are measured from how the copy actually moves on screen (`MobMotionEstimator`). The feet
  therefore match the interpolated movement the player sees. Jumps (teleports, warps) are ignored and jitter is
  smoothed.
- **AI state, in-combat, fleeing and stunned**, plus the mob's walk and run speeds (to scale the blend), come from
  `MobReplicaState`. The server captures it each tick (`CaptureReplicaState`). Speeds are rounded to 0.05 m/s, so
  small changes are not resent.
- **One-shots**: alerts and dodges are repeated with an observers RPC. Hit flinches come from the status module's
  `CombatEntity.ReplicatedDamage` (non-periodic hits only, with the same 0.4 s cooldown as the server). The death
  comes from `CombatEntity.ReplicatedLifeChanged`.

This was chosen over a `NetworkAnimator` for three reasons. The driver uses damped parameters and legacy
`CrossFade` calls, which a NetworkAnimator does not replicate unless every call goes through it. Each machine
computes the same parameters, so no animator traffic is sent. And the animation stays smooth whatever the network
update rate is. **Do not add a NetworkAnimator to mob prefabs**: it would fight the local driver.

### Death on clients

The server kills the mob as before: `MobStatusController.Die`, `CombatEntity.HandleDeath`, loot, absorption,
`Mob.Died`. It then despawns the mob through `GameNet.NetSpawn.Destroy` after the profile's destroy delay. On the
client, `Mob.ApplyReplicatedDeath(true)` (raised by `ReplicatedLifeChanged`, or by the `Dead` flag of the replicated
state when a prefab has no status replication) moves the copy to its Dead state. That plays the death animation and
turns the body's colliders off. It raises nothing: no `Died`, no drops, no absorption. A revival from the server
(`false`) stands the copy up again, and the Dead state turns back on only the colliders it turned off.

### Spawning: only on the server, in the spawner's scene

Every mob spawn site creates mobs only where `GameNet.NetSpawn.ShouldSpawnHere` (single player, host, server). It then
calls `GameNet.MobNetSpawn.Spawn(mob, scene)` with the scene of its spawner:

- On a server it moves the mob into that scene. The server's active scene is always the City, while interest
  management (`RealmObserverCondition`), physics and the realm rules are per scene. It then network-spawns the mob.
  Fish-Net spawns only root objects into a scene, so a mob that will be network-spawned is unparented first, keeping
  its world position.
- Offline, or for a prefab without a `NetworkObject`, it changes nothing: the spawner's parenting is kept, and only a
  root mob is moved into the spawner's scene.

Call it after the spawner has finished setting the mob up (zone strength, dungeon tags, names, scale), so the first
state clients receive is complete. Mobs are removed with `GameNet.MobNetSpawn.Despawn` / `GameNet.NetSpawn.Destroy`:
network-despawned on a server, destroyed offline, and never destroyed by a client.

`MobSpawner` (the World's chunk spawner) works like this:

- It does nothing on clients; mobs arrive from the server. On a server it serves all connected players.
- Only players in the chunk's realm count, both for activation and for the minimum and maximum spawn distances.
- The camera-view rule becomes "outside every player's sight": within Hidden Spawn Distance and 60 degrees of where
  each player faces. A dedicated server has no camera, and a host's camera shows only its own player.
- In a session one player entering a dungeon does not pause the shared World. `WorldSpawnRegistry.Paused` is a
  single-player rule.
- Networked mobs are root objects, not children of the chunk's "Mobs" container. Despawning them when the chunk
  deactivates or unloads therefore goes through the spawner's own list, as before.

Dungeon mobs (`DungeonBuilder`, `DungeonInstance.SpawnMob`, `DungeonNest`, `DungeonRespawnDirector`,
`DungeonMobSpawner`) are owned by the dungeon module and use the same helper with the run's scene. Summons are spawned
by `SummonAction` (abilities module) into the summoner's scene.

### Realms: who a mob can perceive and target

City and World overlap in space, and each dungeon run has its own scene. `CombatRules.Separation` decides when two
characters are in separate realms. The AI uses it at every entry point:

- `MobBrain.Classify` returns Neutral for anyone separated from the mob. Perception therefore never notices them,
  remembered targets in another realm are dropped at the next decision, and `IsHostileTo` (ability target filters)
  says no.
- Taunts, forced targets (`ForceTarget`, summons, scripts), help calls, noises with a source, melee swings and cast
  warnings from another realm are ignored.
- The AI update rate (`MobAIScheduler`) uses the nearest player of the mob's own realm. A World mob under a crowded
  City sleeps.
- `MobRealms` is the scene-level form used by the chunk spawners. The hub world can install
  `MobRealms.SceneSeparation`. Without it, two different scenes are separate realms whenever a separation rule exists,
  except the DontDestroyOnLoad scene, where travelling players wait.

Without any realm rule (a plain single-scene game) nothing is filtered.

### Many players, no camera

The AI never depended on "the" local player. Perception scans `CombatEntity.All`, targets come from memory, and the
scheduler uses the nearest of `CombatEntity.Players`, which on a server are all connected players. On a dedicated
server nothing in the mob code needs a camera, a renderer or the local player. A server with nobody connected lets
its mobs sleep, while offline scenes without any player still think at full rate, as before.

## What is authoritative and what is replicated

| What | Decided by | Reaches clients through |
|---|---|---|
| Senses, decisions, targets, threat, fleeing, dodging, ability choice | server | not sent (only the results below) |
| Position, rotation, scale | server (NavMeshAgent, AI) | `NetworkTransform` (scale for elites and champions) |
| AI state, in combat, fleeing, stunned, dead, walk and run speed | server | `NetMob` `SyncVar<MobReplicaState>` (sent when it changes) |
| Current target | server | `NetMob` `SyncVar<NetworkObject>` → `MobMovementStateMachine.VisibleTarget`, `NetMob.Target` |
| Name, zone strength, elite flag | server (spawner, `ZoneMobStrength`, `DungeonSpawned`) | `NetMob` `SyncVar<NetMob.Identity>` (checked every second) → `Mob.DisplayName`, `Mob.Strength`, `Mob.IsElite` |
| Alert, dodge | server | `NetMob` observers RPC |
| Hits, health, crowd control, death | server | status module (`NetCombatStatus`) |
| Ability casts | server | abilities module (`NetAbilityCaster`) |
| Spawning, despawning, loot | server | Fish-Net spawn/despawn, inventory module (drops) |

Clients send nothing to mobs: `NetMob` has no ServerRpc. Players affect mobs only through their own validated combat
requests (abilities, weapons), and those are lag-compensated against the mob. `NetMob` adds `GameNet.LagCompensated`
to every mob on the server. The dungeon seed is never involved: mobs arrive fully built.

## What the prefab needs

On the root of every mob prefab (Tools ▸ SimpleMovements ▸ Network adds them):

| Component | Settings |
|---|---|
| `NetworkObject` | Root of the prefab, in the spawnable prefabs. No owner (the server owns mobs). |
| `GameNet.NetMob` | Identity Check Interval 1 s (default). |
| `FishNet.Component.Transforming.NetworkTransform` | Server authoritative (mobs have no owner). Synchronize position, rotation and **scale**. Interpolation 2 to 3 ticks, no extrapolation. Teleport threshold about 5 m, because NavMeshAgent warps and dungeon placement snap. |
| `GameNet.NetCombatStatus` | Status module (health, crowd control, death). |
| `GameNet.NetAbilityCaster` | Abilities module (casts). Slot Contents: Auto. |

No `NetworkAnimator`. The `Mob`, `MobStatusController`, `MobMovementStateMachine`, `NavMeshAgent` and
`MobAbilityController` stay as they are. `RotateToGroundNormal` on the root does nothing on clients, because the
server's rotation already includes it. On a child model it keeps aligning the model locally.

## Testing locally

1. Build the item database and network the prefabs (Tools ▸ SimpleMovements ▸ Network), then start a host and a client
   on 127.0.0.1 (see 01).
2. Walk the client's player into the World. Mobs appear near **both** players, and each player's surroundings get their
   own chunk activation. On the client the mobs walk, run and turn smoothly, and their animations match their speed.
3. Aggro a mob with the client. It chases and attacks the client's player, the alert and attack animations play on
   both screens, and hits make it flinch on both.
4. Stand the host in the City and the client in the World at overlapping coordinates. No World mob notices or chases
   the City player. With both in the World, the mob picks the nearer or more threatening player.
5. Kill a mob as the client. It plays its death on both screens, loot drops once on the server, and the body
   disappears for everyone after the destroy delay.
6. Start a dedicated server (`-batchmode -nographics -server`) and join with two clients. Mobs keep thinking around
   each player, with no camera on the server. Disconnect both clients: the mobs fall asleep.
7. Edit Mode tests: `CustomEditor/Networking/Tests/MobNetTests.cs` (replicated state, speed estimate, realm rules, AI
   rate tiers, the spawn helper offline).

## Limitations

- **NavMesh across realms.** Unity has one NavMesh world per process. On a server that keeps the City, the World and
  dungeon runs loaded at overlapping coordinates, their NavMesh surfaces overlap too, and an agent can be placed on
  or path across another realm's surface. Give realms different agent types or areas, or vertical offsets, when their
  walkable areas overlap.
- Noises without a source (a scripted alarm at a position) cannot be attributed to a realm and are heard by every
  mob in range.
- The "outside every player's sight" spawn rule uses the players' facing, not their cameras, which a server cannot
  see.
- A copy's speed is measured from its interpolated movement. Right after a teleport, the movement animation restarts
  from standing.
- `Mob.Strength` and `Mob.IsElite` are information for UI. The rules that apply them (zone damage and health,
  dungeon elites) run on the server only.
- Disabling a spawned mob on the server (for example to hide a dormant dungeon mob) is not replicated: Fish-Net does
  not sync `SetActive`. Spawn such mobs when they are revealed instead.
