# 02 - Player movement: prediction and reconciliation

The player's locomotion (walk, sprint, crouch, jump, dash, roll, plus the Double Jump / Wall Climb / Glide traits) is
server-authoritative and predicted on the owning client with Fish-Net's prediction v2. Single player is unchanged: the
same code runs every frame from the keyboard, with no Fish-Net involved.

## How it works

### One deterministic step

`MovementStateMachine.Simulate(in MovementInput input, float dt)` is the only place where movement happens. It depends
only on the input, the step length and the player's authoritative state (position, speeds, stamina, stun). Its order is
fixed, because a replay has to reproduce it:

1. sanitize the input (non-finite values dropped, move clamped to length 1, yaw wrapped); neutral input while the
   controls are locked (`Frozen`) or the AvailabilityStateMachine says the player cannot move (stunned, dead);
2. advance the movement clock (`SimTime`), buffer a jump press (0.2 s), set `Movement2D` / `Direction`;
3. step the state machine (`StateManager.StepMachine`: next state, then Update or Exit+Enter);
4. step the movement traits (`TraitManager.TickMovementTraits`);
5. gravity (`PlayerMovementController.ApplyGravity(dt)`), then the body's rotation
   (`ApplyRotation(cameraYaw, rotateWithCamera)`);
6. stamina regeneration (`StaminaManager.TickRegen(dt)`).

What used to depend on frames was moved onto that step: the dash and roll movement coroutines are timers
(`MovementContext.ActionElapsed`), the sprint/crouch stamina drain is a timer (`StaminaDrainTimer`, one cost every
0.8 s), stamina regeneration is a timer (`StaminaManager.TickRegen`), cooldowns and the jump buffer use `SimTime`
instead of `Time.time`, the running jump reads the previous state instead of the Animator, and the camera yaw /
rotate-with-camera mode come with the input instead of being read from the camera. The double gravity integration
while jumping (the controller's gravity plus the Jumping state's own) is kept on purpose: the jump height and air time
were tuned with it. Dash and roll are ready as soon as the player spawns.

Offline, `MovementStateMachine.Update` calls `Simulate(ReadDeviceInput(), Time.deltaTime)` every frame. While the
machine runs, `PlayerMovementController.DrivenBySimulation`, `StaminaManager.DrivenBySimulation` and
`TraitManager.MovementTraitsDriven` are set, so their own FixedUpdate/LateUpdate/Update leave gravity, rotation,
regeneration and the movement traits to the step (no double application).

### Snapshots

`Capture()` returns a `MovementSnapshot` with everything the step changes: position, root and body yaw, vertical
velocity, state / previous state / time in state, the clock, current speed, last rotation, jump buffer, dash and roll
cooldowns (as time left), dash/roll progress, stamina, the drain and regeneration timers, `SuspendGravity`, `Frozen`,
the teleport count and the movement traits' state (air jumps used, climbing, climb time). `Restore()` puts all of it
back without running any Enter/Exit or raising `StateChanged` (`StateManager.RestoreState`), switching the
CharacterController off around the position change so its collision position follows. Timers are stored as time left,
so a snapshot means the same on machines whose clocks differ.

### Fish-Net: `GameNet.NetPlayerMovement`

A `TickNetworkBehaviour` on the player prefab root (next to the NetworkObject). On `OnStartNetwork` it sets
`MovementStateMachine.ExternallyDriven`, so the machine only moves on the network tick.

- **Owner, every frame**: one-tick presses (Jump, Dash, Roll) are latched so a press between two ticks is never lost
  and never sent twice.
- **Every tick (`OnTick`)**: `[Replicate] PerformReplicate(MoveData)`. The owner builds `MoveData` (move vector, the
  seven buttons packed in a byte - see `MovementButtons` - and the camera yaw) and simulates it immediately
  (prediction). The server runs the same step with the data the client sent. Spectators run the forwarded data; past
  the newest data they reuse the last input for one tick (without its presses) and then assume the player let go,
  as in Fish-Net's CharacterController demo.
- **After the tick (`OnPostTick`)**: `CreateReconcile()` captures a `MoveReconcile` (the snapshot in network form). The
  server's copy is authoritative; the owner restores it when it arrives and Fish-Net replays the inputs sent since.
  Replays run with `MovementStateMachine.IsReplaying` set: state-change events are suppressed and nothing
  presentation-related runs twice.
- Only the copy controlled from this machine reads devices: `SetDeviceInputEnabled(false)` on every other copy (other
  players, every player on a dedicated server). `NetPlayer` already switches off the camera rig, Unity's PlayerInput
  component, `PlayerMovementView`, the emote wheel and the HUD on those copies.

### What is authoritative, what is replicated

| Data | Who decides | How it reaches clients |
|---|---|---|
| Position, facing, vertical velocity, movement state, timers, dash/roll cooldowns | server simulation | reconcile (owner and, with state forwarding, spectators) |
| Stamina | server (movement, weapons, abilities spend it there) | reconcile; the owner predicts it in between |
| Move input, buttons, camera yaw | owner (intention only) | replicate data, sanitized by the server |
| Speed, weight, stun / death | status module (server) | its SyncVars; the step reads them through the usual managers |
| Controls locked (`Frozen`) | server (`DungeonSession.SetControls` through `GameNet.IControlLock`) | reconcile |
| Teleports | server (`RealmTravel.Place`, `DungeonSession.Teleport`, portals, respawn) | reconcile: position + teleport count |

Validation: the client sends no positions, speeds, costs or cooldowns. The server clamps the move to length 1, drops
non-finite values and unknown button bits; dash, roll and jump requests go through the same cooldown, stamina,
grounded and can-move checks as in single player, so spamming them does nothing. Fish-Net bounds how many inputs per
tick the server queues.

### Teleports

When the server moves a player further than `teleportDetectDistance` (3 m, on the MovementStateMachine) between two
steps, or code calls `MovementStateMachine.NotifyTeleported()` / `NetPlayerMovement.NotifyTeleported()`, the teleport
count in the snapshot changes. The owner's reconcile carries the new position (its prediction is not undone: the
replay starts from the server's position), and on every machine `NetPlayerMovement` snaps the NetworkObject's
smoothed graphical object (`PredictionSmoother.Teleport()`) instead of letting it slide across the map. The
NetworkObject's own teleport threshold is a second safety net for any other large jump.

### Remote players

No NetworkTransform and no NetworkAnimator for locomotion. With state forwarding every client runs the same step for
other players, so `PlayerAnimationController` / `MovementAnimationHandler` animate them from `Movement2D` and the
current state exactly like the local player.

## Prefab setup

On the player prefab root (Tools ▸ SimpleMovements ▸ Network ▸ Make Selected Prefab Networked ▸ Player sets these up):

- `NetworkObject`: **Enable Prediction** on, **Prediction Type** Other, **Enable State Forwarding** on, **Graphical
  Object** = the child that holds the model, Animator and renderers (never the CharacterController root), **Enable
  Teleport** on with a threshold around 2-5 m (raise it if dashes are faster than threshold x tick rate - the snap on
  server teleports does not depend on it), owner interpolation 1, spectator interpolation 2.
- `NetPlayer`, `NetPlayerMovement` on the root. No NetworkTransform on the predicted root.
- The **CharacterController must be on the NetworkObject root** (the predicted object), and
  `PlayerMovementModel ▸ Player Transform` (the body the simulation turns; the dash/roll direction) must NOT be the
  Graphical Object or inside it - use the root. `NetPlayerMovement` logs a warning when either is wrong.
- Animator root motion off. TimeManager physics mode can stay Unity (CharacterController moves need no physics step);
  a tick rate of 50-60 is recommended.

## Testing locally

1. Single player: Play the start scene and choose Single player (or run without Fish-Net). Walk, sprint until
   stamina runs out, crouch, jump (also a running jump), dash, roll, and with the traits double jump / wall climb /
   glide. Everything must feel as before.
2. Host + client on this PC: Tools ▸ SimpleMovements ▸ Network ▸ Build ▸ Client, start one instance as Host and
   enter Play Mode with Play In Editor As ▸ Client (joins 127.0.0.1) - or use Run ▸ Server And Two Clients. Move both
   players; each sees the other walk, run, jump and animate.
3. Latency: add Fish-Net's `LatencySimulator` to the NetworkManager (e.g. 100 ms, 2% loss). The owner must still
   respond instantly; jumping, dashing and sprinting into stamina exhaustion must not rubber-band.
4. Teleports: travel between City and World, enter a dungeon and use its portals: the player snaps to the destination
   on every screen instead of sliding.
5. Pause on a client or the host: the game keeps running for the others (no `Time.timeScale = 0` in a session).
6. Edit Mode tests: `GameNet.Tests.MovementTests` (input packing and sanitizing, snapshot timers and flags, jump
   buffer, manual stepping/restoring of a state machine, stamina regeneration rhythm).

## Limitations

- Movers outside the step still run where the rules run (the server): `ForcedMovement` knockbacks and ability dashes,
  `DungeonClimbable`, `DungeonGravityShift`, `DungeonMovingPlatform`, `DungeonBladeTrap`, and
  `AbilityCaster.FaceDirection`. The owner does not predict them, so it can rubber-band briefly while they act (the
  reconcile corrects it).
- Speed changes (slows, roots, casting) only take effect at the next state change, exactly as in single player.
- Stamina spent by weapons and abilities on the server reaches the owner with the next reconcile; near zero stamina the
  owner can briefly mispredict a sprint or dash.
- `PlayerMovementModel.CurrentPlayerState` is still not written by default (combat sneaking/evasion stay off as they
  always were); tick **Report State To Combat** on the MovementStateMachine to turn it on.
- Cooldowns, the jump buffer and state times use a float movement clock; a session lasting many days loses timer
  precision.

## Smooth camera on the owning machine

The predicted body moves on network ticks (30 per second by default). On the machine that controls the player,
`NetPlayerMovement` (*Smooth Camera Targets*, on by default) points every Cinemachine camera that follows the body or
one of its children (not under the Graphical Object) at a stand-in interpolated between the last two ticks every
frame, so walking does not look jerky. Teleports snap. The cameras get their own targets back when the session ends.

Cameras placed as children of the player (a camera rig on the body, no Follow target) are moved under a smoothed
stand-in of their parent for the session, at the same local pose (only the position is interpolated; rotations stay
live so mouse look is not delayed).

## The player cannot walk forward / shakes

The `MovementGuard` (added next to the CharacterController by the movement) makes the controller ignore the player's
own solid colliders - weapons and shields in hand, body-part hit boxes, equipment. In a network session the model is
smoothed and trails the body slightly, so a solid collider in its hands used to block walking forward every step. On
the machine that controls the player it also writes a Console warning when a move key is held but the Movement action
reads nothing (a binding or rebinding problem), or when the player is asked to move but stays in place - with the
reason (frozen, cannot move, what it runs into, a non-kinematic Rigidbody or a NetworkTransform on the player).
*Diagnose Blocked Movement* on the Movement State Machine turns the warnings off. `NetPlayerMovement` also warns when
the NetworkObject has no Graphical Object (the model is then not smoothed at all) or the player has a NetworkTransform.

## Model smoothing: NetworkTickSmoother

Fish-Net 4.6+ smooths the model with a **NetworkTickSmoother** on the model's object (Target Transform = the player
root, which moves every tick); the NetworkObject's own *Graphical Object* field is obsolete and jitters for the host.
*Make Selected Prefab Networked ▸ Player* adds and configures the NetworkTickSmoother (owner smoothing over 1 tick,
other players over 2, teleports over 5 m snap) and clears the old field, so running it again migrates an older prefab.
Never use both: they fight. The Project Validator and a `[NetPlayerMovement]` warning report either problem.

### Model directly under the turned body (Smooth Body)

When the model's meshes sit directly under the body the movement turns (PlayerMovementModel ▸ Player Transform, e.g.
PlayerShellObject - the package's usual layout), no child holds only the visuals, and moving meshes or bones would
break the Animator's bindings. `NetPlayerMovement` (*Smooth Body*, on) then shows that body interpolated between the
last two ticks every frame and puts its real pose back before every tick, so the simulation, the reconcile and the
server never see the displayed pose. No NetworkTickSmoother is used in that case (*Make Selected Prefab Networked ▸
Player* removes it). The root never turns: cameras placed under it measure their rotation from it.
