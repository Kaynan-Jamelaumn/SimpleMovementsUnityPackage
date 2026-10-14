# 03 - Abilities

Casting is server-authoritative. A player's machine only sends **intentions** (cast this slot, here is my aim,
cancel); the server runs the unchanged `AbilityCaster` pipeline and every client replays what the server did as a
**cosmetic copy** of the cast. Mobs and summons cast on the server only (their AI runs there) and are replayed the
same way.

Code: `Networking/Runtime/Combat/NetAbilityCaster.cs` (component), `NetAbilityMessages.cs` (message structs), the
always-compiled hooks in `Essentials/Ability` (`AbilityNetSupport.cs`, the mirror API of `AbilityCaster`,
`AbilityCastInstance.Cosmetic`). Tests: `CustomEditor/Networking/Tests/AbilityNetTests.cs`.

## How a cast travels

```
owner client                          server                                   other clients (and the owner)
------------                          ------                                   -----------------------------
key press -> TryCastFromInput
  -> SubmitCast -> TryCast
     local CanCast (instant feedback)
     IAbilityNetBridge.RequestCast --> CmdCast (validate, lag view)
                                        TryCast -> StartCast ...  --------->  RpcCastStart  -> BeginMirrorCast
     (refused) <-- RpcCastRejected      (cast time)                            (animation, VFX, telegraphs)
crosshair (Track Until Release) -> CmdAim (10-20 Hz)  RpcCastAim --------->  SetMirrorAim (telegraph follows)
                                        Release -> actions (real hits) ---->  RpcCastRelease -> ReleaseMirrorCast
                                                                               (actions run cosmetically)
cancel key -> CmdCancelCasting          InterruptSlot  ------------------->  RpcCastInterrupted -> InterruptMirrorCast
                                        slot timers ---- RpcSlotState ---->  owner: AbilitySlot.ApplyNetState
                                        slot contents -- SyncList -------->  ApplyReplicatedSlot
```

- **Offline / single player**: nothing is installed, `IsReplica` is false and `TryCast` starts casts locally exactly
  as before. The same holds for the host's own player (the host is the server).
- **Owner client**: `AbilityCaster.IsReplica` is true and `NetBridge` is the `NetAbilityCaster`. `TryCast` still runs
  `CanCast` for instant "not ready / no mana / out of range" feedback, then sends `CmdCast`. A slot with a request in
  flight answers `Busy` for 0.35 s, so held keys do not spam. The cast itself starts when `RpcCastStart` arrives (no
  prediction - on localhost the delay is a few milliseconds).
- **Other copies** (remote players seen from a client, every player on a dedicated server, the host's copies of
  remote players): `PlayerAbilityController.LocalInputEnabled = false` and the `AbilitiesStateMachine` is disabled,
  so no copy ever reads this machine's keyboard, mouse or camera. On the server, `RemoteAim` makes a remote player's
  Track Until Release casts follow the aim its client streams instead of the host's `Camera.main`.

## What the server decides (authoritative)

Everything that changes gameplay: whether a cast may start (cooldowns, charges, global cooldown, resources, stun /
silence, range, line of sight, target filters), costs and refunds, cast and release timing, every action's hits and
effects (damage, heals, control, displacement, DoTs, buffs), summons, barriers' push-out, movement abilities, AI
hazards and noise, absorption rolls and pickups, cooldown resets.

### Validation of `CmdCast` / `CmdAim` / `CmdCancelCasting`

- Fish-Net ownership (`[ServerRpc]` requires the caller to own the object).
- Rate limits per caster: casts 12/s (burst 6), aim 40/s (burst 20), cancels 8/s (burst 4).
- The slot must exist and hold the ability the client named (`abilityId`), otherwise `NoAbility` (the client's view
  of its slots was stale).
- Aim: NaN / infinite / absurd vectors are refused (`AbilityNetMath.SanitizeRequest`), the direction is normalised
  and the point clamped to 160 m from the caster; targets must be spawned, alive and not the caster; targets in
  another realm are dropped (`CombatRules.AreSeparated`).
- Then the caster's own `CanCast`. Range and line of sight are judged with **lag compensation** (where the client saw
  its target). A refusal for timing (cooldown or previous cast ending a moment later on the server than on the
  client) is retried for 0.15 s before `RpcCastRejected` reports it; the owner shows it through the usual
  `CastFailed` event.
- Nothing the client sends is used as an amount: stats, cooldowns, damage and costs are the server's.

### Lag compensation

Every hit query of an ability whose caster is a player is wrapped in `using (LagCompensation.For(casterEntity))`
(only the query, never the effect): `CanCast` range and line of sight, `AbilityCastInstance.HitArea` (areas, cones,
zones, beams, ground surges, barrier contact, landing hits), `ApplyHit` line of sight, projectile overlap and sweep
tests, `DirectEffectAction` range. `CmdCast` and `CmdAim` report the client's view tick to
`NetLagCompensation.ReportView`. Mob casts are not rewound (the provider ignores non-players).

## What is replicated

| Data | Direction | How |
|---|---|---|
| Cast start (slot, ability id, resolved `AbilityStats`, target, aim, cast time, server tick, seed, visual-only) | server to observers | `RpcCastStart` (`ExcludeServer`: a host never mirrors what it runs) |
| Release with final aim | server to observers | `RpcCastRelease` |
| Interrupts | server to observers | `RpcCastInterrupted` |
| Aim of Track Until Release casts while winding up | server to observers except the owner | `RpcCastAim` (unreliable, 15 Hz) |
| Caster-less casts (`AbilityCaster.ExecuteInstant`: items, weapon attacks) whose source has a `NetAbilityCaster` | server to observers | `RpcInstantCast` -> `AbilityCaster.ExecuteMirrorInstant` |
| Slot timers (phase, seconds left, charges, recharge, lock) | server to owner | `RpcSlotState` on every phase change and when a timer jumps (reset / reduced cooldowns, charges) |
| Slot contents (ability id, modifiers JSON, label, source, enabled, absorbed grant) | server to clients | `SyncList<NetAbilitySlotContent>` (players; mobs build theirs from the prefab) |
| Summoner of a summon | server to clients | `SyncVar<NetworkObject>` (team / party relations on clients) |

Times always travel as **seconds left** or ticks, never `Time.time` (it differs per process).

### Cosmetic copies (`AbilityCastInstance.Cosmetic`)

The client replays the cast with the server's numbers: animation triggers, cast / release VFX and sounds,
telegraphs, slot phases (the owner's UI and input gating), and the actions themselves. Every action's visuals run,
but `ApplyHit` stops after the filter / line-of-sight checks and only plays hit feedback, so projectiles stop on the
first valid character, beams end on walls and areas flash where the server's do. Copies never pay costs, use
charges, start cooldowns, raise `CombatEvents` (AI, blocking, noise), add AI hazards, push characters out of
barriers, move characters (`MovementAction` only plays its start / landing effects), summon (`SummonAction` only
plays spawn effects at the server's positions) or raise `CombatRules.VisualCast`. They do not release themselves:
they wait for the server (with a 1 s safety timeout after the planned release).

Randomness that must match - projectile Random / Rain patterns and summon placement - uses `CastRandom`, a
deterministic sequence seeded by the cast's `Seed` (sent with the cast); the global `UnityEngine.Random` is untouched.
Barrier segments are built on every machine (they are colliders that movement and projectiles collide with).

## Spawns

- **Summons** (`SummonAction`): server only; instantiated, moved into the summoner's scene, set up (summoner, target,
  lifetime), then `NetSpawn.Spawn`. Removal goes through `NetSpawn.Destroy` (network despawn). Clients never create
  summons. Summon prefabs need a `NetworkObject` on the root, a `NetAbilityCaster` and the mobs module's components,
  and must be registered as spawnable prefabs.
- **Ability pickups** (`AbilityPickup`, from `AbilityAbsorption` and `AbilitySpawner`): rolled, spawned and collected
  on the server only, in the scene of the character that dropped them. With a pickup prefab that has a
  `NetworkObject` (Absorption Settings ▸ Pickup Prefab, Combat Settings ▸ Default Absorb Pickup Prefab, or the
  spawner's visual) every client sees it floating; give it **no** NetworkTransform (clients animate it locally). The
  generated default orb cannot be networked: it then exists on the server only (collectable, but invisible on the
  clients of a dedicated server).
- Casts with `AbilityCaster.ExecuteInstant` run only on the server (it returns null on clients).

## Prefab setup

| Prefab | Needs |
|---|---|
| Player | `NetworkObject` + `NetPlayer` (integrator) + `NetAbilityCaster` on the root. The `PlayerAbilityController` may sit on a child (found automatically). |
| Mob, summon | `NetworkObject` + `NetAbilityCaster` on the root (Slot Contents = Auto: not replicated). |
| Pickup prefab | `NetworkObject` (no NetworkTransform) + optionally `AbilityPickup`; registered as spawnable. |

`NetAbilityCaster` settings: **Caster** (empty = found in children), **Slot Contents** (Auto = players only),
**Aim Send Rate**, **Slot Poll Rate**.

**Ability ids.** Messages carry ability ids, never definitions. Every castable ability asset must be in the Ability
Database (`Resources/AbilityDatabase`): Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Ability Database (Resources)
creates it or adds every `AbilityDefinition` asset to the existing one. Definitions created in code are found by id
too: converted legacy abilities (`legacy:<name>:mob|player`), mob basic attacks (`basic:<key>`) and presets
(`AbilityPresets.AsRuntime(def, id)`, or `unsaved:<name>` for an unnamed one) - as long as the client has created the
same definition (the caster's initialization does this for mobs). A system that creates definitions on demand can
install `AbilityDatabase.FallbackResolver` so a client can build one it has not created yet. Unknown ids are logged
once per id.

**Dedicated server.** `AbilityPool.PresentationEnabled` is false when `NetAuthority.IsHeadless`: no VFX, sounds,
telegraphs, cast effects, zone effects or projectile visuals are created (barrier segments still are - they are
gameplay colliders).

## Hooks for other modules (always compiled)

- `AbilityCaster.IsReplica`, `NetBridge`, `SetReplica(bool)`; mirror API `BeginMirrorCast`, `ReleaseMirrorCast`,
  `InterruptMirrorCast`, `SetMirrorAim`, `ApplyReplicatedSlot`, `NotifyAbilityReceived`, public `RaiseCastFailed`,
  `IndexOfId`.
- `AbilityCaster.ExecuteInstant` (server; null on clients) raises `AbilityCaster.InstantCastExecuted`;
  `AbilityCaster.ExecuteMirrorInstant(...)` shows one on a client. Weapons and equipment simply keep calling
  `ExecuteInstant` on the server: the source's `NetAbilityCaster` relays it automatically.
- `PlayerAbilityController.LocalInputEnabled`, `RemoteAim`, `SubmitCast(slot, request)`.
- `ForcedMovement.Started` (mover, offset, seconds, arc, linear) and `ForcedMovement.Teleported` (character,
  destination): raised where dashes, leaps, knockbacks, pulls and blinks run (the server), so the movement module can
  replay them in the owner's prediction.
- `AbilitySlot.GetNetState` / `ApplyNetState`, `LockedUntil`, `RechargeEnd`.
- `AbilityDatabase.Resolve(id)`, `RegisterRuntime(def)`, `FallbackResolver`.
- `AbilityPool.PresentationEnabled`; `CastRandom`; `AbilityNetMath` (validation, timers, seeds, rate limiter).

## Visual-only casts in the City

`CombatRules.IsVisualOnly`: the server still validates the request; the cast costs nothing, runs no action and raises
`CombatRules.VisualCast` on the server (the hub world's `CityAbilityRelay` listens). `RpcCastStart` / `RpcCastRelease`
carry `visualOnly`, so observers (limited by the realm observer condition) play the cast and its visual-only effect.

## Testing locally

1. Build the Ability Database (see above) and set up the prefabs (Tools ▸ SimpleMovements ▸ Network ▸ setup tools add
   the components by name).
2. Start a host from the start menu, then a second instance (a build, or ParrelSync) joining `127.0.0.1`.
3. On the client, cast every kind of ability: projectiles (spread / random / rain patterns look identical on both
   screens), areas, beams (Track Until Release: the host sees the telegraph follow the client's crosshair), summons
   (one creature per summon, despawned on both screens), barriers, dashes.
4. Check the client's cooldown UI and charges; reset cooldowns on the host (e.g. a duel start) and see the client's UI
   follow.
5. Kill a mob that drops an ability: the pickup appears on both screens and only one player gets it.
6. Run a dedicated server (`-batchmode -nographics -server`) and two clients: casts, hits and pickups work; the
   server log shows no VFX objects being created.
7. Single player: start without a session and with Fish-Net removed - casting behaves as before.

## Limitations

- No client-side prediction of casts: the owner's cast starts one round trip after the press (milliseconds on
  localhost).
- An observer that starts seeing a caster in the middle of a cast misses that cast's start (it sees the next one).
- Caster-less casts whose source has no `NetAbilityCaster` (scene traps, barrels without a networked source) are not
  shown on clients.
- Pickups without a networked prefab are invisible on dedicated-server clients.
- Ability presets created only on the server under a non-unique name (`unsaved:<name>`) cannot be resolved by clients
  unless they create the same preset or a `FallbackResolver` builds it; use saved assets in the Ability Database for
  anything players cast.
- Cosmetic hits are judged against what the client sees, so a client can show a hit flash where the server missed
  (or the reverse); damage numbers and health always come from the server.
- `BaseAbilityController` (legacy, unused) is not networked.
