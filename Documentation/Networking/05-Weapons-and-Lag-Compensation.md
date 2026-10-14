# 05 - Weapons and lag compensation

Weapon combat is server-authoritative. A player's machine only sends **intentions** (an attack input pressed or
released, a reload, a quick throw, "I want my block up", where it aims); the server feeds them to its own copy of the
player, where the unchanged `WeaponController` / `AttackExecutor` / `BlockController` code decides everything: which
attack, combos and charge, stamina, ammo and magazines, durability, crits, hits, blocks, parries and guard breaks.
Every client then **shows** what the server did. Hit queries the server runs for a remote player are **lag
compensated**: the others are moved back to where that player saw them.

Code: `Networking/Runtime/Combat/NetWeaponCombat.cs` (component), `NetWeaponMessages.cs` (message structs), the
always-compiled hooks in `Inventory/Items/Weapon/WeaponController/WeaponNetSupport.cs` (bridge and presentation
interfaces, the attack catalog, validation helpers), the presentation half of `AttackExecutor`, `BlockController` and
`WeaponProjectile`. Tests: `CustomEditor/Networking/Tests/WeaponNetTests.cs`.

## How an attack travels

```
owner client                         server (its copy of the player)                 every client watching (owner too)
------------                         -------------------------------                 ---------------------------------
Use Item / attack input
  BeginAttackInput -> ForwardPress
  IWeaponNetBridge ----------------> CmdWeaponInput (validate, ReportView, aim)
                                       ServerBeginAttackInput (item cooldown)
                                       BeginAttackInput -> AttackExecutor
                                       charge ---------------------------------->  RpcWeaponCharge -> PresentCharge
crosshair while a ranged input is held -> CmdWeaponAim (unreliable, ~20 Hz)
input released ------------------->  ReleaseAttackInput -> attack starts ------->  RpcWeaponAttack -> PresentAttack
                                       active frames: hit queries (rewound)        (animation, trail, combo effects,
                                       damage, crits, knockback, on-hit effects     cosmetic behaviours, same timeline)
                                       shot: projectiles fly here --------------->  RpcWeaponShot -> cosmetic projectiles
                                       hit feedback ----------------------------->  RpcWeaponHit -> particles, sounds
                                       ground impact ---------------------------->  RpcWeaponImpact -> ground effect
                                       interrupted (stun, parry, unequip) ------->  RpcWeaponStopped
                                       reload start / end ----------------------->  RpcWeaponReload
   (owner) <------------------------ RpcWeaponFailed (empty, ammo, stamina...)     AttackFailed event, empty click
   (owner) <------------------------ rounds loaded (owner-only SyncVars)           ammo counters
```

- **Offline / single player**: nothing is installed (`NetBridge`, `PresentationSink` stay null), `RunsRules` is true and
  every input runs locally exactly as before. The host's own player is the same (the host is the server).
- **Owner client**: `WeaponController.RunsRules` is false. Its inputs (device polling, PlayerInput events, the
  inventory's Use Item and quick throws) still arrive in `BeginAttackInput` / `ReleaseAttackInput` / `Reload` /
  `QuickThrow` / `CancelCharge`, which forward them through `NetBridge` after the checks that belong to this machine
  (UI focus, an ability preview owning the click, nothing in hand). Each message carries the camera's aim ray and
  `TimeManager.GetPreciseTick(TickType.LastPacketTick)`. The swing is not predicted: it starts when the server's
  `RpcWeaponAttack` arrives (a few milliseconds on localhost).
- **Server copy of a remote player**: never reads this machine's devices or camera (`IsLocallyControlled` is false).
  Ranged attacks aim along the owner's streamed ray (`SetRemoteAim`, used by `TryGetAimRay` / `AimPoint`).
- **Other copies** (remote players on a client): no input, no rules. `AttackExecutor.Tick` plays the presentation
  timeline only.

## What is authoritative, what is replicated

| Thing | Where it is decided | How clients learn it |
|---|---|---|
| Attack choice, combos, chains, charge, speed | Server copy (`AttackExecutor`) | `RpcWeaponCharge` / `RpcWeaponAttack` (weapon id + attack number + charge, speed, seed) |
| Damage, crits, knockback, on-hit effects, kills | Server (`DealWeaponDamage`, `CombatEntity.ApplyDamage`) | Status layer (health, hit events) - see 04 |
| Stamina, mana, health shot costs | Server (`ConsumeStamina`, `PayShotCosts`) | Status layer |
| Ammo, magazines, reloads, thrown items | Server (`FireRanged`, `FinishReload`, `SpendAmmo`) | Rounds loaded: owner-only SyncVars; reserve: inventory replication; reload: `RpcWeaponReload` |
| Durability, broken weapons | Server (`ConsumeDurability`) | Inventory replication |
| Item cooldown of the primary attack / throws | Server (`ServerBeginAttackInput`, `ServerTapAttack`, `ServerQuickThrow`) | The owner also starts its local cooldown (UI) |
| Block up / down, guard break, parry window | Server (`BlockController`) | `blockState` SyncVar -> `ApplyReplicatedState` |
| Blocked / parried hits | Server (`InterceptDamage`) | Hit flags of the status layer -> `BlockController` feedback via `DamageFeedback` |
| Shield bash | Server (`TryBash`) | `RpcShieldBash` |
| Behaviours that change the game (cast ability, lunge, self effects, area damage, invulnerability) | Server only | Their own replication (abilities mirror, movement, status) |
| Cosmetic behaviours (Spawn Effect, the VFX/sound of Area Burst) | Every machine | Played on the presentation timeline with the server's seed |

**Attack numbering.** Messages name an attack with `WeaponAttackCatalog`: the five actions (each followed by its
variations), then every combo sequence's special action, then every combo tree branch's action. It depends only on the
weapon asset, which every machine has. Weapons and ammo are named by their Item Database id (Tools ▸ SimpleMovements ▸
Project Setup ▸ Build Item Database); an empty id means "the weapon in that hand".

**Chance-based behaviours.** `AttackBehaviour.ShouldRun` now rolls `WeaponNetMath.Roll(seed, behaviour, moment, hit)`
with a random seed per attack (offline: as random as before). The server sends the seed, so a client plays exactly the
cosmetic behaviours the server's roll chose.

## Lag compensation

`GameNet.LagCompensation.For(attacker)` wraps **only the physics query** of every hit test the server runs for a player
(see 01 for how the rewind works). Hits found are collected inside the scope and applied after it, in the present:

| Query | Where |
|---|---|
| Hit Shape overlap | `AttackExecutor.DetectHits` (HitShape) |
| Weapon Cast (`attackCast.DetectObjects`) | `AttackExecutor.DetectHits` (WeaponCast) |
| Weapon Blade sweep (sub-stepped volumes vs. character capsules) | `AttackExecutor.DetectBlade` / `SweepBlade` |
| Ground impact area | `AttackExecutor.DoImpact` (edge strength measured in the rewound pose) |
| Crosshair aim ray of ranged weapons | `WeaponController.AimPoint` |
| Projectile sweep, point-blank overlap, explosion | `WeaponProjectile.Update`, `CheckOverlapAtStart`, `Explode` |
| Area Burst behaviour | `AreaBurstBehaviour.Execute` |
| Shield bash cone | `BlockController.TryBash` |

The client's view tick is reported with `NetLagCompensation.Instance.ReportView` in every attack ServerRpc
(`CmdWeaponInput`, `CmdWeaponAim`, `CmdQuickThrow`). Mobs, the host's own player and players without a report are
never rewound. Ground detection (impact contact, ground below) is not rewound: the ground does not move.

Realm separation: weapon hits, impacts, projectiles (real and cosmetic), area bursts, bashes and the aim ray skip
characters in another realm scene (`CombatRules.AreSeparated`); the Cast Ability behaviour's auto-target uses
`CombatQuery.BestInCone`, which already does.

## Validation (server)

- Ownership: every ServerRpc requires it (`RequireOwnership` default).
- Known values only (input kind, attack input), finite aim, direction normalised, the aim origin pulled back to
  within 15 m of the player (`WeaponNetMath.SanitizeAim`).
- Rate limits: presses / taps / reloads 15/s (burst 8), releases 30/s (burst 16, separate so a release is never
  dropped behind spam), aim 60/s, quick throws 6/s, raising the block 20/s (lowering always passes), failure replies
  8/s.
- Presses, taps, reloads and throws are ignored for a dead player; the weapon code's own checks do the rest (stunned,
  casting, broken, stamina, ammo, reloading, fire interval, item cooldown).
- Quick throws name a weapon id; the server throws only a stack its own inventory holds (`IThrowableSource`, else the
  `InventoryManager`'s stacks, else a hand) and starts the stack's cooldown.
- The client never sends damage, positions of others, ammo counts or cooldowns.
- When the owner leaves or the object changes hands, held inputs, buffered presses, the remote aim and the block intent
  are dropped and a charge in progress is cancelled (it does not go off).

## Presentation on clients

`AttackExecutor` has a presentation mode (`Running.presentation`): `PresentCharge`, `PresentAttack`, `PresentStop`,
`PresentShot`, `PresentHit`, `PresentImpact` and `TickPresentation`. A presented attack plays the same animation clip
stretched to the same timeline (frames / speed), the trail, the charge effects (draw sound, charging VFX, full-charge
VFX), combo branch / finisher effects and the cosmetic behaviours at their moments. It raises `AttackStarted`,
`AttackEnded` and `RangedFired` (equipment visuals draw the weapons, hide a thrown weapon). It restricts only the
`CastMoveMultiplier` the owner's movement prediction reads; speed comes from the server. It never deals damage, spends,
moves the character or queries for hits.

Shots: clients launch `WeaponProjectile`s with `LaunchData.cosmetic = true` along the server's velocities (spread
included). They stop on what the real projectile could hit, show the impact effect, stick, and never call back, explode
for damage or become pickups. If the session ends mid-presentation, the next tick drops it.

Blocks: the owner's `BlockController` sends its wanted state on change; the server runs the block; every client shows
the replicated state (animator bool, `BlockChanged` for the equipment visuals, guard break sound and trigger) and plays
block / parry feedback when the status layer reports a blocked or parried hit.

## Prefab setup

- Player prefab root (next to the NetworkObject): `GameNet.NetWeaponCombat` (fields can stay empty: it finds the
  `WeaponController` and `BlockController` in its children). The Network Setup menu adds it.
- Optional: mobs with a `BlockController` (shield item or innate defense) may also have one, so clients see their
  guard; it works without a Weapon Controller.
- Every `WeaponSO` and `AmmoSO` must be in the Item Database (rebuild it after adding weapons).
- Mobs and summons need `LagCompensated` on the server (added by the network layer) to be rewound.

## Testing locally

1. Build the Item Database, run Tools ▸ SimpleMovements ▸ Network setup, open the Bootstrap scene.
2. Start a host in the editor and a client in a build (or a second editor), both on 127.0.0.1.
3. Swing a sword at a mob from the client: the swing, trail and hit particles show on both; health changes come from
   the server. Charge a bow: the draw shows on the host, the arrow flies on both, ammo drops on the client's counter.
4. Hold the block on the client and attack it from the host: the guard pose shows on the host, the block sound plays
   on both, stamina drops on the server. Parry by raising it just before the hit.
5. Lag compensation: with Fish-Net's latency simulator (Transport ▸ Latency Simulator, e.g. 150 ms) hit a moving
   target from the client where you see it; the hit lands. `NetLagCompensation.DelaySeconds(conn)` shows the delay used.
6. Single player: open a gameplay scene without the Bootstrap: everything behaves as before.

## Limitations

- The owner's swing starts when the server reports it (no cosmetic prediction); fine on localhost, visible on a slow
  link.
- A presentation timeline is not fast-forwarded by the network delay; on clients a swing ends a few milliseconds
  later than on the server.
- The magazine reload "one round at a time" loop and the crosshair bloom are approximated on clients (bloom grows on
  each presented shot and settles locally).
- Projectiles are rewound for their shooter on every flight step; many arrows in flight at once cost more server CPU.
- A recovered arrow or thrown weapon becomes a networked pickup only when the inventory layer installs
  `WeaponProjectile.RecoveredItemSpawner`; otherwise it is a pickup on the server only (the host can collect it).
- A client's cosmetic projectile can stop on a slightly different spot than the server's (different poses); hits
  and damage always follow the server.
- Late joiners do not see an attack already in progress (they see the next one); the block state is a SyncVar and is
  correct on join.
