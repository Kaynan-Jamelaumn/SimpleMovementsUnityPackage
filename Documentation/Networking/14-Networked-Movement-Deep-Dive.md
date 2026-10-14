# 14 - Networked player movement: the complete deep dive

You know how the movement works in single player: `MovementStateMachine` reads the keyboard and moves the
`CharacterController` every frame through its states (Idle, Walking, Running, Jumping, Dashing...). This page explains
**everything that changes when the same movement runs in a network session**:

* the scripts involved and where each one runs;
* how the client and the server each handle movement;
* how the server knows that Player A's inputs come from Player A's keyboard;
* what a "bridge" is;
* how players travel between the City, the World and dungeons;
* how inventory and abilities fit in.

The class and method names are the real ones. Open the files next to this page and follow along.

Contents:

1. Before and after: the one big change
2. The scripts and what each one is for
3. The managers (Fish-Net's and the package's)
4. What a "bridge" is, and which components are bridges
5. Who is who: connections, ownership, and why your keys only move your player
6. Where each piece of code runs (server, owner, other clients)
7. One tick, step by step, with the code
8. The data sent over the network
9. Corrections: reconcile and replay
10. Other players on your screen (spectators)
11. Smoothing: why the screen looks smooth when the game moves 30 times a second
12. Special cases: presses between ticks, frozen controls, teleports, stun, streamed ground
13. Travel: City, World, dungeons
14. Inventory and abilities, and how they touch movement
15. A full timeline: from a key press to every screen
16. Debugging checklist

---

## 1. Before and after: the one big change

### Single player (before)

```
Every frame:
MovementStateMachine.Update()
   └─ Simulate(ReadDeviceInput(), Time.deltaTime)
         read keyboard → state machine → CharacterController.Move → gravity → rotation → stamina
```

### Network session (after)

```
Every network TICK (30 per second by default), on every machine that has a copy of the player:
NetPlayerMovement.TimeManager_OnTick()
   └─ PerformReplicate(MoveData)            ← Fish-Net decides where MoveData comes from
         └─ MovementStateMachine.Simulate(input, TickDelta)    ← the SAME movement code as single player
```

The movement code itself (`Simulate`, the states, `MovementContext`) did not change. Two things did:

1. **Who calls `Simulate`.** Before, `MovementStateMachine.Update()` called it every frame. Now `NetPlayerMovement`
   calls it on every network tick, and `Update()` does nothing. The flag that switches this is
   `MovementStateMachine.ExternallyDriven = true`, set in `NetPlayerMovement.OnStartNetwork()`.
2. **Where the input comes from.** Before, always the local keyboard. Now the input is a small packet (`MoveData`):
   * on the player's own machine, it comes from the keyboard;
   * on the server, it is the packet that machine sent;
   * on other players' machines, it is the packet the server forwarded.

To make this possible, `Simulate` was made **deterministic**: the same starting state plus the same input gives the
same result on every machine. Everything frame-based was moved onto the step:
* dash and roll timers;
* the stamina drain timer;
* stamina regeneration (`TickRegen`);
* cooldowns and the jump buffer (they use `SimTime` instead of `Time.time`);
* the camera yaw (it now comes inside the input, because the server has no camera).

## 2. The scripts and what each one is for

### Gameplay side (the movement itself, no Fish-Net code)

| Script | Role in the network version |
|---|---|
| `MovementStateMachine` | The movement. Key members for networking: `Simulate(in MovementInput, float dt)` (one step), `ReadDeviceInput()` (keyboard → input), `Capture()` / `Restore(snapshot)` (save and load the full state), `ExternallyDriven` (stop stepping in Update), `SetDeviceInputEnabled(bool)` (stop reading this machine's keyboard), `IsReplaying` (a re-run step: no sounds or events), `Frozen` / `SetControlsLocked` (travel lock), `TeleportCount` / `NotifyTeleported()`. |
| `MovementInput` | What a player asks for in one step: `Move` (stick/WASD), `Sprint`, `Crouch`, `JumpPressed`, `JumpHeld`, `DashPressed`, `RollPressed`, `RotateWithCamera`, `CameraYaw`. Helpers: `PackButtons()` (7 buttons → 1 byte), `FromPacked(...)`, `Sanitized()` (cleans bad values), `Neutralized()` (no input). |
| `MovementSnapshot` | Everything one step can change: position, yaws, vertical speed, state, time in state, the movement clock, speed, timers (jump buffer, dash/roll cooldowns, dash progress, stamina drain and regen), stamina, flags (gravity suspended, frozen, climbing...), trait data and the teleport counter. |
| `MovementContext` | Shared data of the states for the current step: `Input`, `Dt`, `SimTime`, `IsReplaying`, `Frozen`, timers, water info. |
| `PlayerMovementController` | Gravity and rotation, called from `Simulate` (`DrivenBySimulation = true` stops its own FixedUpdate/LateUpdate). |
| `StaminaManager` | Stamina regen is ticked by `Simulate` (`DrivenBySimulation`), so a replay regenerates exactly the same. |
| `MovementGuard` | Diagnostics on the controlling machine ("W is held but the Movement action reads 0"), ignores the player's own colliders. |

### Network side (`Networking/Runtime`, compiled with Fish-Net)

| Script | Role |
|---|---|
| `NetPlayerMovement` | **The networked movement controller.** A Fish-Net `TickNetworkBehaviour` on the player root. Reads input on the owner, calls `Simulate` every tick on every copy, sends corrections from the server, replays on the owner, smooths the visuals. Holds the `MoveData` and `MoveReconcile` structs. |
| `NetPlayer` | **The player's identity.** Decides whether this copy is "my player" (`IsLocalPlayer`). Turns camera, `PlayerInput`, UI and audio listener ON for my player and OFF for everyone else's copies. Carries the name (`DisplayName`). |
| `CameraTargetSmoother` | Created by `NetPlayerMovement` on the owner: gives Cinemachine targets that move smoothly every frame. |
| `BodyVisualDriver` | Tiny helper `NetPlayerMovement` adds: shows the smoothed body every LateUpdate (`ShowBodySmoothed`). |
| `NetSession` | Installs `NetAuthority`'s resolvers, so gameplay code can ask "is this object networked? owned here?" |
| `NetPlayerSpawner` | Creates each player object on the server and gives it to the right connection (ownership). |
| `NetRealmScenes` | Moves a player's object between scenes over the network (City, World, dungeon). |
| `NetPlayerRealm` | Sends the player's realm state, including the "arrival token" used to unfreeze after travel. |
| `NetCombatStatus` | Sends speed, weight, stun and death, which the movement reads, so the owner predicts with the server's values. |

### Fish-Net pieces used by the movement

| Fish-Net piece | What it does for us |
|---|---|
| `TickNetworkBehaviour` | Base class of `NetPlayerMovement`. Gives `TimeManager_OnPreTick`, `TimeManager_OnTick`, `TimeManager_OnPostTick` (enabled with `SetTickCallbacks(PreTick \| Tick \| PostTick)` in `Awake`). |
| `[Replicate]` method | `PerformReplicate(MoveData md, ReplicateState state, Channel channel)`. Fish-Net calls it with the right input on each machine, sends the owner's input to the server, forwards inputs to other clients, and calls it again for replays. |
| `[Reconcile]` method | `PerformReconcile(MoveReconcile rd, Channel channel)`. On the server it sends the state; on clients it is called with the server's state to correct. |
| `IReplicateData` / `IReconcileData` | Interfaces our two structs implement (they carry a tick number: `GetTick`/`SetTick`). |
| `TimeManager` | The tick clock: `TickDelta` (seconds per tick), `LocalTick`, `GetTickPercentAsDouble()` (how far between two ticks we are, for smoothing). |
| `PredictionManager` | Stores past inputs and runs the replays after a reconcile. |
| `NetworkObject` | The player's network identity: `ObjectId`, `Owner`, `IsOwner`, `IsServerStarted`, `IsClientStarted`. |
| `NetworkTickSmoother` | Optional: smooths a visual child between ticks (used when the model is on a child). |

## 3. The managers

"Manager" means two different things here.

### Fish-Net's managers (all on the NetworkManager object in the Bootstrap scene)

| Manager | What it manages | Movement-related use |
|---|---|---|
| `NetworkManager` | Owns all the others | `NetPlayerSpawner` uses it to instantiate and spawn the player |
| `ServerManager` | The server: connections, spawning (`Spawn(nob, owner, scene)`) | Spawns each player owned by its connection |
| `ClientManager` | This machine's connection to a server | Knows which connection is "me" (`IsOwner` compares to it) |
| `TimeManager` | The tick (fixed 30 Hz by default, `-tickrate`) | Calls our tick callbacks; all movement runs on it |
| `PredictionManager` | Prediction: input history, reconcile, replay | Drives `[Replicate]` / `[Reconcile]` |
| `SceneManager` | Network scene loading (per connection, stacked scenes) | Used by `NetRealmScenes` for travel |
| `ObserverManager` | Who sees which object (`RealmObserverCondition`) | Your client only receives players in your scene (and in the World within 140 m) |
| `TransportManager` + `Tugboat` | The actual UDP sockets, port 7770 | Carries the packets |

### The package's controllers (on the same object)

| Controller | Movement-related job |
|---|---|
| `NetSession` | Sets `NetAuthority.Mode`; installs `NetworkedObjectResolver` (is it spawned?) and `LocalOwnerResolver` (is `nob.IsOwner` true here?). |
| `NetBootstrap` | Starts Host / Server / Client. Sets `LocalPlayerName` (sent by `NetPlayer`). |
| `NetRealmScenes` | Moves players between scenes, decides when a client may unload the scene it left. |
| `NetPlayerSpawner` | Spawns the player for a connection when it enters the City. |
| `NetLagCompensation` | Records every player's position each tick (for combat hit checks, not for movement). |

## 4. What a "bridge" is, and which components are bridges

A **bridge** is a small connection point between the game code and the network code. It lets the game code work
without knowing whether there is a network.

The idea in one sentence: **the gameplay script exposes a "hook", and the network script plugs into that hook.**
With no network, nothing is plugged in and the gameplay script behaves as in single player.

The package uses three kinds of bridge.

### Kind 1: static question helpers (`Networking/Core`, always compiled)

Gameplay code asks questions; the network layer installs the answers.

```csharp
// Networking/Core/NetAuthority.cs (always compiled)
public static bool IsServer => mode != NetMode.Client;           // does this process decide?
public static Func<GameObject, bool> NetworkedObjectResolver;     // installed by NetSession
public static Func<GameObject, bool> LocalOwnerResolver;          // installed by NetSession
public static bool IsLocallyControlled(GameObject go) { ... LocalOwnerResolver(go) ... }

// Networking/Runtime/NetSession.cs (Fish-Net layer) installs them:
NetAuthority.LocalOwnerResolver = go => { NetworkObject nob = NetObjects.Of(go); return nob != null && nob.IsOwner; };
```

Offline, `IsLocallyControlled` returns true (you are the only player). In a session, it asks Fish-Net whether this
machine owns the object. `NetSpawn`, `NetScenes` and `LagCompensation` work the same way.

### Kind 2: interface bridges (inventory, weapons, abilities)

The gameplay class has a property of an interface type. The network script assigns itself to it, on the owner only:

| Gameplay | Bridge property | Interface | Plugged in by |
|---|---|---|---|
| `InventoryManager` | `NetBridge` | `IInventoryNetBridge` (`Send`, `SendWorld`) | `NetInventory.SetBridge(true)` |
| `WeaponController`, `BlockController` | `NetBridge` | `IWeaponNetBridge` | `NetWeaponCombat.SetBridges(true)` |
| `AbilityCaster` | `NetBridge` | `IAbilityNetBridge` (`RequestCast`, `RequestCancel`) | `NetAbilityCaster.ApplyRole()` (`caster.NetBridge = local ? this : null`) |

When the property is set, the gameplay class **sends a request instead of acting**:

```csharp
// AbilityCaster (gameplay), simplified
if (NetBridge != null)
    return NetBridge.RequestCast(this, index, request, out reason);  // client: ask the server
// otherwise cast right here (single player, or the server itself)
```

### Kind 3: the movement's bridge: a driver flag plus a step API

Movement does **not** use a request bridge: asking the server and waiting for the answer before moving would feel
laggy. Its bridge is:

* **`MovementStateMachine.ExternallyDriven`**: "someone else steps me". Set to true by `NetPlayerMovement`.
* **The step API:** `Simulate`, `Capture`, `Restore`, `ReadDeviceInput`, `SetDeviceInputEnabled`, `IsReplaying`.
  `NetPlayerMovement` uses only these.
* **`GameNet.IControlLock`**: the network-safe way to freeze a player. `MovementStateMachine` implements it as
  `SetControlsLocked(bool)` → `Frozen`. `DungeonSession.SetControls` uses it for networked players instead of
  disabling components, which would break prediction.

So for movement, **`NetPlayerMovement` is the bridge component**: it sits between Fish-Net (ticks, replicate,
reconcile) and `MovementStateMachine` (the movement), and translates one into the other.

## 5. Who is who: connections, ownership, and why your keys only move your player

This is the question "how does the server know Player A's inputs are Player A's?". The answer is a chain of five
links.

### Link 1: every client is a connection with a number

When a game connects, Fish-Net's server creates a `NetworkConnection` for it with a unique `ClientId` (0, 1, 2...).
Every packet that arrives is tagged with the connection it came from; the transport knows which socket it arrived on.
A client cannot pretend to be another connection.

### Link 2: each player object is owned by exactly one connection

```csharp
// NetPlayerSpawner.OnClientEnteredCity(NetworkConnection conn)   (server)
NetworkObject nob = manager.GetPooledInstantiated(playerPrefab, pose.position, pose.rotation, true);
manager.ServerManager.Spawn(nob, conn, scene);      // ← "conn" becomes the OWNER of this player object
```

Player A's connection owns player object #12; Player B's owns #13. The server has one full copy of each player
object, and each copy has its own `MovementStateMachine`, its own `NetPlayerMovement`, its own stamina and so on.
The players share nothing.

### Link 3: only the owner's machine reads its keyboard into a player

On every machine there is a copy of every player it can see. `IsOwner` (Fish-Net) is true only on the copy owned by
this machine. Three things use it.

`NetPlayerMovement.RefreshDeviceInput()` runs on start and whenever ownership changes:
```csharp
bool local = IsOwner && NetAuthority.HasLocalPlayer;
machine.SetDeviceInputEnabled(local);     // other copies never read this machine's keyboard
```

`NetPlayerMovement.BuildMoveData()` builds input only for the owner:
```csharp
if (machine == null || !IsOwner) return default;
MovementInput input = machine.ReadDeviceInput();
```

`NetPlayer.ApplyPresentation()` switches the owner's camera rig, Unity `PlayerInput`, UI and audio listener ON only on
`IsLocalPlayer`, OFF on everyone else's copy (`SwitchOffOwnerParts`). It also blocks the shared input for remote
copies (`SharedPlayerInput.SetBlocked`).

Also, each `MovementStateMachine` creates **its own** instance of the generated `PlayerInput` class in `Awake`, so the
action maps of two copies are separate objects. With input disabled on non-owned copies, only one of them reads.

### Link 4: Fish-Net only accepts a player's inputs from its owner

`PerformReplicate` is a `[Replicate]` method. On a client, Fish-Net sends the data only from the **owner's** copy, and
the server only accepts replicate data for object #12 **from the connection that owns #12**. If Player B's game tried
to send inputs for Player A's object, the server would ignore them. On the server, the `MoveData` arriving for #12
therefore always comes from Player A's keyboard.

### Link 5: the server never trusts the numbers

Even Player A's own inputs are cleaned before use:

```csharp
// NetPlayerMovement.PerformReplicate (server side)
MovementInput input = md.ToInput();
if (IsServerStarted)
    input = input.Sanitized();   // NaN → 0, move longer than 1 → length 1, yaw wrapped, unknown bits dropped
```

`Simulate` sanitizes again, and everything else comes from the **server's** own copy: speed (from SpeedManager and
weight), stamina, cooldowns, stun (`AvailabilityStateMachine.CanMove()`) and position. A client can ask to move; it
cannot choose how fast, cannot skip a cooldown, cannot move while stunned, and cannot teleport.

### Who is "the local player" in code

| Question | Code |
|---|---|
| Is this copy mine? (Fish-Net) | `IsOwner` inside a NetworkBehaviour |
| Is this copy mine? (any script) | `GameNet.NetAuthority.IsLocallyControlled(gameObject)` |
| Is this my player and do I have one? | `NetPlayer.IsLocalPlayer` (`IsOwner && NetAuthority.HasLocalPlayer`) |
| Where is my player? | `NetPlayer.Local`, `PlayerLocator.Local` (set in `ApplyPresentation`) |
| Which connection owns this player? (server) | `nob.Owner`, `NetObjects.OwnerOf(entity)` |
| Which player belongs to this connection? (server) | `NetObjects.PlayerOf(conn)` |
| A stable id for a player | `PlayerIds` (network: `ObjectId + 1`) |

## 6. Where each piece of code runs

Four roles exist for each player object:

* **Server copy.** On the dedicated server, or the host. The truth.
* **Owner copy.** On the machine of the player who controls it. Predicts.
* **Spectator copy.** The same player seen on another player's machine.
* **Host's own player.** Server copy and owner copy at the same time.

| Code | Server copy | Owner copy | Spectator copy | Host's own player |
|---|---|---|---|---|
| `NetPlayerMovement.Update()` (latches Jump/Dash/Roll presses) | no | **yes** | no | yes |
| `BuildMoveData()` (reads keyboard) | returns empty | **yes** | returns empty | yes |
| `PerformReplicate` → `Simulate` | **yes**, with the owner's input | **yes**, prediction | **yes**, with forwarded input | yes (once: it is the server) |
| Replays (`IsReplaying = true`) | never | **yes**, after a correction | can happen | no |
| `CreateReconcile()` → `PerformReconcile` (sends the state) | **yes** (every `serverReconcileInterval` ticks) | builds its own local record each tick | records | yes |
| `PerformReconcile` receiving (→ `Restore`) | no | **yes** | **yes** | no |
| `NetPlayer` camera, input, UI | off (dedicated) / host: on for host's player | **on** | off | on |
| `CameraTargetSmoother`, body smoothing | no (headless) | **yes** | body smoothing yes | yes |
| `MovementGuard` warnings | no | **yes** | no | yes |

**Where are the "server-side scripts"?** There is no separate server project. The server runs the **same scenes and
the same prefabs**; every script checks its role at runtime:

* `IsServerStarted` / `IsServerInitialized` in NetworkBehaviours;
* `NetAuthority.IsServer` / `RunsRules(go)` in gameplay code.

A dedicated server is the same game started with `-server` (or a server build): `NetAuthority.HasLocalPlayer` is
false, so no camera or UI is used, and `HeadlessServer` caps the frame rate.

## 7. One tick, step by step, with the code

The `TimeManager` fires these callbacks every tick, in order, on every machine:

```
OnPreTick  → NetPlayerMovement.TimeManager_OnPreTick()  → RestoreBody()     (visual body back to its real pose)
(clients: Fish-Net applies a reconcile here if a server state arrived)
OnTick     → NetPlayerMovement.TimeManager_OnTick()     → PerformReplicate(BuildMoveData())
OnPostTick → NetPlayerMovement.TimeManager_OnPostTick() → SnapAfterTeleport(); RecordBody(); camera RecordTick(); CreateReconcile()
```

### 7.1 On the owner's machine (Player A's PC)

**Every frame, between ticks:** `NetPlayerMovement.Update()` remembers one-frame presses so none is lost:

```csharp
MovementInput frame = machine.ReadDeviceInput();
latchedEdges |= (MovementButtons)frame.PackButtons() & MovementButtons.Edges;   // Jump/Dash/Roll pressed
```

**At the tick:** `TimeManager_OnTick()` → `PerformReplicate(BuildMoveData())`.

`BuildMoveData()`:
1. `machine.ReadDeviceInput()`: reads WASD/stick into `Move`, the held buttons, and the **camera's yaw**
   (`cameraModel.CameraTransform.eulerAngles.y`) and rotate-with-camera mode.
2. Adds the latched presses, clears the latch, and remembers the frame (`edgesTakenFrame`), so a second tick in the
   same frame does not press Jump twice.
3. Returns `new MoveData(input)`: Move, 1 byte of buttons, CameraYaw.

`PerformReplicate(md, state)`. Fish-Net does three things around this call:
* gives the data a tick number;
* stores it in the owner's input history (for replays);
* queues it to be sent to the server (unreliable channel; inputs are sent redundantly, so a lost packet does not lose
  an input).

Then our body runs:
```csharp
MovementInput input = md.ToInput();
machine.IsReplaying = state.ContainsReplayed();      // false now: this is the first run
machine.Simulate(input, (float)TimeManager.TickDelta);   // the character moves on Player A's screen right away
```

**After the tick:** `TimeManager_OnPostTick()` records the body pose for smoothing and calls `CreateReconcile()`. On a
client this builds a local record of the state for this tick (Fish-Net compares and keeps history); it is not sent.

### 7.2 On the server

The input packet from Player A's connection arrives and Fish-Net queues it for object #12. At the server's tick, Fish-Net
calls `PerformReplicate(md)` on **#12's `NetPlayerMovement`** with that input:

```csharp
MovementInput input = md.ToInput();
if (IsServerStarted)
    input = input.Sanitized();
machine.Simulate(input, (float)TimeManager.TickDelta);   // the REAL movement of Player A
```

`Simulate` then runs in this order:
1. Sanitize the input again. If `Frozen` or `!availabilityStateMachine.CanMove()` (stunned, dead), use
   `Neutralized()` instead.
2. Detect an external teleport (server only).
3. Advance `SimTime`, buffer a jump press, set `Movement2D` and `Direction`.
4. Sample water and start swimming if needed.
5. `StepMachine()`: the current state's `GetNextState` / `UpdateState`, which calls `CharacterController.Move`.
6. Movement traits.
7. Gravity (not while swimming) and rotation to `CameraYaw`.
8. Stamina regeneration.
9. Keep the player on streamed ground.

If the input for this tick has not arrived yet (lag), Fish-Net runs the tick with no new data, or with data that
arrives later in a burst. That is why inputs carry tick numbers.

**After the tick**, `CreateReconcile()` on the server:
```csharp
PerformReconcile(new MoveReconcile(machine.Capture()));   // every serverReconcileInterval ticks (default 1)
```
Fish-Net sends that state to Player A (the owner) and to the clients that observe #12.

### 7.3 On Player B's machine (spectator of Player A)

Fish-Net forwards Player A's inputs and the server's states. Player B's copy of #12 runs
`PerformReplicate(forwarded md)` → `Simulate`. Player A walks, jumps and animates on B's screen with the same code and
the same input. When a state arrives, `PerformReconcile` → `Restore` keeps B's copy exactly on the server's track
(section 10).

## 8. The data sent over the network

### `MoveData` (owner → server → spectators), every tick

| Field | Type | Meaning |
|---|---|---|
| `Move` | Vector2 | WASD/stick: x = right, y = forward |
| `Buttons` | byte | Sprint, Crouch, JumpPressed, JumpHeld, DashPressed, RollPressed, RotateWithCamera (one bit each, `MovementButtons`) |
| `CameraYaw` | float | The owner camera's yaw. The server has no camera, so "forward" must come from the client |
| (tick) | uint | Set by Fish-Net |

That is about 13 bytes per tick. It contains **no position, no speed, no stamina**: only intentions.

### `MoveReconcile` (server → owner and spectators)

`MovementStateMachine.Capture()` → `MovementSnapshot` → `MoveReconcile`. Fields:

* `Position`, `RootYaw`, `BodyYaw`, `VerticalVelocity`;
* `State`, `PreviousState`, `StateTime`, `SimTime`;
* `CurrentSpeed`, `LastRotation`;
* timers: `JumpBufferLeft`, `DashCooldownLeft`, `RollCooldownLeft`, `ActionElapsed`;
* stamina: `Stamina`, `StaminaDrainTimer`, `StaminaRegenTimer`;
* `Flags` (consuming, regenerating, gravity suspended, frozen, climbing);
* traits: `AirJumpsUsed`, `ClimbTime`;
* `Teleports`.

Timers are sent as **time left**, never as absolute times (`MovementSnapshot.Remaining` / `Since`), so they mean the
same on every machine even though each machine's clock is different.

## 9. Corrections: reconcile and replay

Prediction is a guess. Usually it is exactly right, because the same code runs with the same input. It can be wrong
when something on the server was different, for example:

* another player pushed you;
* you were stunned by an attack you had not seen yet;
* your stamina was lower on the server;
* the server teleported you.

What happens then:

```
tick 100  owner predicts, sends input #100
tick 101  owner predicts, sends input #101
tick 102  owner predicts, sends input #102
          ← state for tick 100 arrives from the server (it was computed when input #100 arrived)

Fish-Net on the owner:
  1. PerformReconcile(serverState@100)   → machine.Restore(snapshot)    position/state/timers = server's at tick 100
  2. replay: PerformReplicate(input #101, state = Replayed) → Simulate   (IsReplaying = true)
             PerformReplicate(input #102, state = Replayed) → Simulate
  3. the player is now at "server truth + my inputs since then"
```

* If the prediction was right, the replay ends exactly where the player already was, and nothing is visible.
* If it was wrong, the player is moved to the corrected place. The smoothing (section 11) makes small corrections
  invisible.

**`Restore(in MovementSnapshot s)`** puts everything back **without** running any state's `EnterState` / `ExitState`
and without events:
* it switches the CharacterController off while moving it;
* it sets the yaws, speeds and timers;
* `RestoreState(state, previous, enteredTime)` sets the state directly;
* it sets the dash/roll flags on the animation model.

**`IsReplaying`.** During a replay, `MovementContext.IsReplaying` is true. States and the machine skip:
* state-change events (`SuppressStateEvents`);
* sounds and effects;
* the `MovementGuard` diagnostics.

These already happened the first time. The **result** of the step (position, stamina) is computed normally.

## 10. Other players on your screen (spectators)

A spectator copy is never controlled locally:
* `SetDeviceInputEnabled(false)` is set;
* `NetPlayer` turned off its camera and input.

It runs `PerformReplicate` with the inputs Fish-Net forwards, and `PerformReconcile` with the server's states.

The inputs arrive a little late, so Fish-Net runs the spectator **ahead** of the newest input ("future" ticks). The
code guesses:

```csharp
// NetPlayerMovement.PerformReplicate, spectator branch
if (state.ContainsTicked())        lastTickedData = md;          // a real input arrived: remember it
else if (state.IsFuture())
{
    if (md.GetTick() - lastTickedData.GetTick() > spectatorFutureTicks)
        md = new MoveData { CameraYaw = lastTickedData.CameraYaw };   // too long without news: assume they let go
    else
    {
        md = lastTickedData;                                           // keep doing the same thing
        md.Buttons = MovementInput.WithoutEdges(md.Buttons);           // but never repeat a one-tick press (no double jump)
    }
}
```

The animation of remote players needs no extra network data. Their movement state machine runs on your machine, so
the animation handler sees "Running", "Jumping" or "Swimming" exactly as it does for your own player.

## 11. Smoothing: why the screen looks smooth when the game moves 30 times a second

The simulation moves the player **once per tick** (30 Hz). Your screen draws 60, 144 or more frames per second. If
the model were drawn at the simulated position, it would visibly step. So the **visual** is drawn between the last two
tick positions, while the **real** position stays exact.

The package supports three setups. `NetPlayerMovement.SmoothedModel()` picks the one that applies:

| Setup | When | How |
|---|---|---|
| **NetworkTickSmoother** (Fish-Net) | The model is a child object of the root | The smoother component on the model child interpolates it every frame towards the root's tick positions. |
| **Smooth Body** (package) | The model's meshes sit directly under the body the movement turns (`PlayerMovementModel ▸ Player Transform`, e.g. PlayerShellObject) | `SetupBodySmoothing()`. **Before each tick** `RestoreBody()` puts the body back to its real pose (the simulation must see the truth). **After each tick** `RecordBody()` stores the new pose. **Every frame** `BodyVisualDriver.LateUpdate` → `ShowBodySmoothed()` places the body at `Lerp(prev, curr, TimeManager.GetTickPercentAsDouble())`. |
| **Camera targets** | On the owner (`Smooth Camera Targets`) | `CameraTargetSmoother` gives Cinemachine stand-in targets interpolated every frame, so the camera does not step either. |

Teleports snap instead of sliding across the map. The server increments `TeleportCount` (`NotifyTeleported`, or
detected when the player jumps more than *Teleport Detect Distance*), and the count travels in the reconcile. In
`OnPostTick`, `SnapAfterTeleport()` sees the new count and resets the smoothing (`UniversalSmoother.Teleport()`, or
the body's previous pose).

**Root motion** is switched off for the session (`DisableRootMotion`), because animation moving the root would fight
the tick movement.

## 12. Special cases

| Situation | How it is handled |
|---|---|
| **Jump pressed between two ticks** | `Update()` latches it into `latchedEdges`; the next `BuildMoveData()` includes it. |
| **Two ticks in one frame** (low frame rate) | `edgesTakenFrame` stops the second tick from pressing Jump again. |
| **Frozen controls** (travel, dungeon loading, arrival hold) | `DungeonSession.SetControls(player, false)` → `IControlLock.SetControlsLocked(true)` → `MovementStateMachine.Frozen = true`. `Simulate` keeps ticking with neutral input and no gravity. `Frozen` is in the snapshot, so client and server agree. Components are never disabled (that would break prediction). |
| **Stunned or dead** | The server's `NetCombatStatus` sets the control state; `AvailabilityStateMachine.CanMove()` returns false on both server and owner (`ApplyControl` copies it), so `Simulate` neutralises the input. |
| **Speed changes** (buffs, weight, sprint multiplier) | `NetCombatStatus` replicates `speed` and `weight`, and the owner applies them (`ApplySpeed`, `ApplyWeight`), so the prediction uses the server's numbers. |
| **Server teleport** (travel, respawn, portal) | `DungeonSession.Teleport` / `RealmTravel.Place` move the root (CharacterController off) and call `NotifyTeleported()`. The new position and teleport count reach the owner in the reconcile, which snaps there. |
| **Long dash into terrain that is not loaded** | `KeepOnStreamedGround` (server and owner only) puts the player back at the edge of ready ground. See Player 07. |
| **Swimming** | Part of `Simulate`; water is sampled from data every machine has, so it predicts like walking. |
| **Packet loss** | Inputs are sent redundantly; the owner keeps building its own reconcile records every tick as a fallback. |
| **Bandwidth** | `serverReconcileInterval`: send the state every N ticks instead of every tick. |

## 13. Travel: City, World, dungeons

Travel is decided only by the server. The client follows. All travel uses the same building blocks:

| Building block | Code | Network effect |
|---|---|---|
| Freeze | `RealmTravel.Hold` / `SetControls(false)` → `DungeonSession.SetControls` → `IControlLock` | `Frozen` travels in the reconcile |
| Change scene | `RealmTravel.MoveToScene` → `DungeonSession.MoveToScene` → `GameNet.NetScenes.MoveToScene` → **`NetRealmScenes.MoveToScene`** | Server moves the object at once; the owner's client loads the destination and follows; the observers are rebuilt |
| Place | `RealmTravel.Place` → `DungeonSession.Teleport` + `NotifyTeleported()` | The reconcile snaps every copy to the new position |
| Unfreeze | `SetControls(true)`, possibly after waiting for the client (arrival hold) | |

### 13.1 Joining: into the City

`NetRealmScenes.OnClientLoadedStartScenes` makes the new client load the City. `OnPresenceChanged` →
`ClientEnteredCity(conn)` → `NetPlayerSpawner.OnClientEnteredCity` spawns the player at a City spawn pose, owned by
that connection.

### 13.2 City → World

```
SERVER                                                              PLAYER A'S CLIENT
RealmGate.OnTriggerEnter → TryUse(collider)
  only if NetAuthority.RunsRules(player)  (the server's copy; a client's predicted copy never triggers travel)
  → HubWorldDirector.TravelToWorld(state, null, false)
     → coroutine ToWorld:
        SetRealm(Transit → World)        ── NetPlayerRealm ──────────►  HUD shows "travelling"
        RealmTravel.Hold(player)         → Frozen = true ─ reconcile ─►  can't move
        EnsureWorld()                    loads the World scene on the server (if not loaded)
        RealmTravel.Place(arrival + 2 m up)  (terrain streams around the waiting player)
        wait until FindWorldGround() finds the World's ground under the arrival point
        RealmTravel.Arrive(player, WorldScene, pose, controls: !waitForClient)
          MoveToScene → NetRealmScenes.MoveToScene:
             SceneManager.MoveGameObjectToScene(root, World)
             SceneLoadData(World){ MovedNetworkObjects = [player] }
             scenes.LoadConnectionScenes(conn, sld)   ─────────────►  client loads the World scene, the player follows
             RebuildObservers(player)                               (now A sees World objects, not City objects)
          Place(pose) → Teleport + NotifyTeleported ─ reconcile ─►  snaps to the arrival point
        SetRealm(World)
        HoldForClient(state): ArrivalToken = N  ─ NetPlayerRealm ─►  NetPlayerRealm.ConfirmArrivalWhenReady():
                                                                     waits until its own World ground exists
                                                         ◄──────── ServerConfirmArrival(N)  [ServerRpc]
        ConfirmArrival(player, N) → ReleaseArrival → SetControls(true) → Frozen = false
                                                                     player can walk
        (if the client never confirms: ExpireArrivalHolds() releases it after ClientArrivalTimeout)
NetRealmScenes.OnPresenceChanged (client arrived) → LeaveScenes(conn, [City])  → the client unloads the City
```

The arrival hold exists because the server can place the player as soon as **its** ground exists, but the client
loads the World later. Without the hold, the player could fall through ground that is not there yet on their own
machine.

### 13.3 World → City

The Return Gate calls `TravelToCity` → `ToCity`. It is the same pattern: Hold, wait for the City `RealmScene`, Arrive
(MoveToScene City, Place), SetRealm(City), and an arrival hold for remote clients. The penalties of a dimensional
jump are applied here if needed.

### 13.4 World → Dungeon

```
SERVER
Portal.OnTriggerEnter (returns at once if !NetAuthority.IsServer)
  → shared/linked dungeon running? DungeonSession.Join(...)   (see Hub World 06)
  → else DungeonSession.Enter(group, request, manager, profile, origin, returnPose, options)
      remembers each traveller's return pose and scene; SetControls(false) (frozen)
      DungeonScenes.CreateAsync → a stacked copy of the DungeonInstance scene, with its own physics
      OnLeaseReady → CreateManagerInScene → Generate(run)    (the dungeon seed is replaced by a secret one)
      OnReady:
        MoveToScene(each traveller, run scene)   ─────────►  the client loads its stacked dungeon scene
        Teleport to the arrival point + NotifyTeleported ─►  snap
        ArrivalHold(run, player) → NetDungeonRun.HoldArrival: keep frozen until that client built the floor
NetDungeonRun (in the run scene) sends the header + floor layouts ─►  the client builds a presentation copy
                                                       ◄───────  ServerFloorBuilt(generation, floor)
UpdateHolds → DungeonSession.ReleaseArrival → SetControls(true)  ─►  the player can move in the dungeon
```

### 13.5 Dungeon → back out

An exit portal (or completing the dungeon) → `DungeonSession.UsePortal` → `Exit` → `End(run)`. For each traveller,
`ExitHandler.TakePlayer` (the `HubWorldDirector`) runs:
* if the World cycle is still the same, `TravelToWorld(state, returnPose, fromDungeon: true)`: back in front of the
  portal;
* if the world has jumped since, `TravelToCity` (with penalties if the rules say so).

The dungeon scene is then released and unloaded.

### 13.6 What the movement does during all this

Nothing special. It keeps ticking:
* while frozen, it simulates neutral input;
* each server teleport arrives in a reconcile and snaps;
* when `Frozen` turns false, the next tick moves again.

The client's movement never decides to change scene. It only follows the server.

## 14. Inventory and abilities, and how they touch movement

These systems are **not predicted**. The client asks, the server decides, the client shows the answer. Movement is
the only predicted system, because a delay there would be felt with every step.

### Inventory (`NetInventory`, bridge `IInventoryNetBridge`)

1. You drag a sword to the hotbar. `InventoryManager` sees `NetBridge != null`, so it calls
   `NetBridge.Send(request)` instead of moving the item.
2. `NetInventory` numbers the request and calls `CmdInventoryRequest(request)` [ServerRpc]. Fish-Net delivers it to
   the server copy of **your** player (ServerRpcs on an owned object require ownership by default).
3. The server runs the same `InventoryManager` operation on its copy, then `SendSnapshot` →
   `TargetInventorySnapshot(conn, snapshot)` [TargetRpc]. Only your client receives it, and shows it.
4. Other players see only what you wear and hold, through the `appearance` SyncVar (`OnAppearanceChanged`).

**Link to movement:** carried weight changes speed. The server's weight and speed go to your client through
`NetCombatStatus` (`ApplyWeight`, `ApplySpeed`), so your prediction uses the new speed.

### Abilities (`NetAbilityCaster`, bridge `IAbilityNetBridge`)

1. You press an ability key. Only the owner's `AbilitiesStateMachine` reads keys (`SetInputMachines(local)`).
2. `AbilityCaster.TryCast` sees `NetBridge != null` → `RequestCast` → `CmdCast(slot, aim, viewTick)` [ServerRpc].
3. The server validates in this order: rate limit, slot and ability id, aim, then `caster.CanCast` (cooldown,
   resources, range, line of sight with lag compensation). Then it casts for real, or answers
   `RpcCastRejected(conn, reason)`. A cast that is only a moment too early because of timing differences is buffered
   and retried briefly.
4. Every client replays the cast visually (`RpcCastStart`, `RpcCastRelease`...). Damage and effects happen only on the
   server, and arrive through `NetCombatStatus`.

**Link to movement:**
* An ability that dashes or teleports the player moves the server's copy. `NotifyTeleported` or the snapshot brings
  the owner along.
* A stun from an ability reaches the owner through `NetCombatStatus` (`ApplyControl`), and the movement stops in the
  prediction as well.

### Weapons (`NetWeaponCombat`), briefly

Attacks are intentions (`CmdWeaponInput`, `CmdWeaponAim` with the tick you saw). The server runs the real weapon code
inside `LagCompensation.For(attacker)`: everyone is rewound to where you saw them, the hit check runs, and they are put
back. Other clients see the result through ObserversRpcs.

## 15. A full timeline: from a key press to every screen

Player A presses **W** on a client. Player B watches. The server is dedicated. The tick is 33 ms.

```
t=0 ms     A's PC   Update(): W is held (no latch needed, it is a held key)
t=5 ms     A's PC   Tick 500: BuildMoveData() → Move=(0,1), CameraYaw=90
                    PerformReplicate(#500) → Simulate → A moves forward 0.17 m ON A'S SCREEN NOW
                    Fish-Net sends input #500 (with #498, #499 again, in case of loss)
t=5-38 ms  A's PC   Every frame: ShowBodySmoothed / tick smoother / camera smoother draw A between ticks
t=25 ms    Server   receives inputs for object #12 from connection 0 (A's connection, the owner) → accepted
t=38 ms    Server   Tick: PerformReplicate(#500) on #12 → Sanitized → Simulate → A's REAL position
                    PostTick: CreateReconcile → PerformReconcile(Capture()) → state@500 sent to A and observers
                    Forwards input #500 to B (B observes #12: same scene, within range)
t=60 ms    B's PC   Spectator #12: PerformReplicate(#500 forwarded) → Simulate → A walks on B's screen
                    PerformReconcile(state@500) → Restore → exactly on the server's track
t=60 ms    A's PC   PerformReconcile(state@500) → Restore → replay #501..#502 (IsReplaying)
                    → same position as predicted: nothing visible
```

If A had been stunned on the server at tick 499, the state at 500 would show A standing still. A's replay would then
put A back, and the smoothing would blend the correction over the next frames.

## 16. Debugging checklist

| Symptom | Look at |
|---|---|
| My player doesn't move in a session | Console warnings from `NetPlayerMovement.CheckPrefabSetup` and `MovementGuard`. `ExternallyDriven` must be true and `IsOwner` true on your copy. `Frozen` must be false (arrival hold waiting for `ConfirmArrival`?). |
| W moves the wrong player, or every player | `NetPlayer` missing, or `RefreshDeviceInput` not run: only the owner's `SetDeviceInputEnabled(true)` may read keys. |
| Rubber-banding (pulled back often) | Something differs between client and server: a NetworkTransform on the player (remove it), root motion, physics objects only one side has, a speed changed on the server but not replicated. |
| Jitter or shaking | Smoothing setup (section 11), CinemachineBrain Update Method = Late Update, no NetworkTransform, root motion off. |
| Stuck after travel | Arrival hold: the client must confirm (`NetPlayerRealm.ServerConfirmArrival`, or `NetDungeonRun.ServerFloorBuilt` in dungeons); otherwise released after the timeout, with a warning. |
| Other players stutter | Raise `spectatorFutureTicks` a little, check packet loss with F9 (ping), and keep `serverReconcileInterval` at 1. |

Related pages: 02 (movement prediction reference), 12 (script reference), 13 (code tour), Hub World 01 (realms),
Networking 10 (dungeons), Player 02 (movement and input), Player 07 (swimming and streamed ground).
