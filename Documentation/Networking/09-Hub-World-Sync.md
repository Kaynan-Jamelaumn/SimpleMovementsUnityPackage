# 09 - Hub world sync: cycles, realms, travel, zones, duels, messages

The hub world (Stage 1, see [Hub World 01 - Architecture](../HubWorld/01-Architecture.md)) already kept its rules
apart from its presentation: the `HubWorldDirector` decides, the HUD and the sky only read. With Fish-Net, the server's
director decides everything and the clients show it. Single player is unchanged: without Fish-Net, or with Fish-Net
installed but no session running, the director runs every rule locally as before.

**Scripts:** `Networking/Runtime/HubWorld/NetHubWorld.cs`, `NetDuels.cs`, `NetPlayerRealm.cs` (all `#if FISHNET`);
always compiled: `HubWorld/Runtime/HubWorldDirector.cs`, `PlayerRealmState.cs`, `RealmTravel.cs`, `RealmGate.cs`,
`RealmScene.cs`, `HubWorldMessages.cs`, `DuelSystem.cs`, `DuelChallengeTarget.cs`, `CityAbilityRelay.cs`,
`HubWorld/Core/DuelBook.cs`, `DuelSnapshot.cs`.

## Who decides what

| Decided on the server only | How the clients learn it |
|---|---|
| The cycle clock, warnings, the jump, the next modifier | `NetHubWorld.State` (SyncVar) + `RpcWarning` / `RpcJump` |
| The world seed of each cycle (the shard seed stays on the server) | `NetHubWorld.State.worldSeed` |
| Gate decisions, travel, world loading and unloading | the player's realm (`NetPlayerRealm.View`) + Fish-Net scene loads |
| Zones and PvP flags, mob strength, dungeon difficulty | `NetPlayerRealm.View` (zone index, name, PvP, difficulty...) |
| Loot loss and Dimensional Disorientation at the jump | inventory / status replication + messages + `TargetLost` |
| Duels (challenge, accept, countdown, fight, end, never-lethal save) | `NetDuels.Duels` (SyncList) + messages |
| Visual-only abilities in the city | the abilities module's cast messages |

Dungeon seeds are never sent. The shard seed is not sent either: it would reveal every future world and modifier, and
the loot-loss rolls (their random generator is seeded from it).

## The director on each machine

`HubWorldDirector.IsMirror` (= `NetAuthority.IsClientOnly`, read when acting) splits the director's behaviour:

* **Server (host or dedicated).** Everything runs as in single player, with these changes:
  * The World scene is loaded with `NetScenes.LoadAdditive` and unloaded with `NetScenes.Unload` (through Fish-Net;
    clients load it when one of their players is moved in). Offline the old `SceneManager` calls run unchanged.
  * Players move between scenes with `NetScenes.MoveToScene` (through `RealmTravel.Arrive`); they are never parked in
    DontDestroyOnLoad; they are frozen during travel through `DungeonSession.SetControls` (the movement's
    `IControlLock`).
  * The server's active scene never changes (it stays the City). Arrivals call `RealmTravel.Place`, which also tells
    the movement (`MovementStateMachine.NotifyTeleported`) so the teleport reaches the owner in the reconcile.
  * **Arrival hold.** A remote client's player stays frozen after the server placed them until their client has loaded
    the destination and has ground under the player: the owner's `NetPlayerRealm` confirms the arrival token
    (`HubWorldDirector.ConfirmArrival`). After `ClientArrivalTimeout` (30 s) the controls come back anyway. The host's
    own player is not held. While held, gates ignore the player and the client's HUD keeps the travel veil up.
  * `LocalPlayer` is null on a dedicated server: nothing is hidden there, no HUD is created in headless mode, and the
    terrain streams around every player out in the world (see "Terrain viewers").
  * On a host the City is hidden for the host's own view by switching off its renderers, lights, sounds and terrain
    drawing only. Colliders, gates, NPCs and scripts keep running for the other players.
  * Arrivals in the World wait for the World's own ground (`RealmTravel.TryFindGroundIn`): the City stays loaded and
    solid on the server and overlaps the World's copy of it, so its floor must not be taken for the terrain.
* **Client.** The director is a mirror. It raises no cycle event of its own, loads no scene, moves nobody and decides
  no zone. Its public state is written by the relays:
  * `ApplyServerCycle(cycle, secondsLeft, duration)`: the local clock is restored to the server's countdown. It moves
    only when it drifted more than 0.25 s, so the HUD counts down smoothly.
  * `ApplyServerWorld(worldSeed, currentModifier, nextModifier)` installs the world rules at once
    (`TerrainGenerator.SeedOverride`, the city's flattened and kept-clear `WorldAreas`). A World scene loaded later
    generates the server's terrain. Spawn hooks are installed too; they are harmless because spawners do not run on
    clients.
  * `ApplyServerFlags(jumping, gateOpen)`, `ShowServerWarning`, `ShowServerJump` (sounds, `CycleWarning`,
    `CycleJumped`, `ModifierAnnounced`) and `ShowLostBetweenDimensions`.
  * `ApplyReplicatedRealm(...)` writes each player's `PlayerRealmState`.
  * When the client finishes loading a World scene (Unity's `sceneLoaded`), the director adopts it: `WorldScene`,
    `WorldState = Ready`, the cycle's weather, and the local player as the terrain viewer. It forgets the scene when it
    unloads.
  * Requests made on a client go to the server. `RequestLeaveCity` and `RequestReturnToCity` use
    `ClientTravelRequest`; duel requests use `DuelSystem.ClientRequest`.
  * Once the client's player has arrived in the World, `NetRealmScenes` unloads the City on that client; the City is
    loaded again when the server moves the player back (a second director in it removes itself). While both are
    loaded, `RealmScene` hides the City: roots without networked objects are switched off, roots holding networked
    objects only lose their own visuals, and networked objects are left to Fish-Net (which shows each client only what
    it observes).

`HubWorldDirector.LocalPlayer` is the network-aware "this machine's player". `PlayerLocator.Local` falls back to the
first player, which on a server or an early client would be someone else.

## NetHubWorld (global relay)

On the "Network World" object of the Bootstrap scene. It finds `HubWorldDirector.Instance` whenever the City loads.

* `State` (one SyncVar, sent the tick it changes) holds the cycle index, the **network time of the next jump**
  (`TimeManager.TicksToTime(GetPreciseTick(TickType.Tick))` + seconds left), the cycle length, the world seed, the
  current and next modifier indexes (`HubWorldSettings.modifiers`), the jumping and gate-open flags, and the server's
  World state. The jump time is resent only when the cycle changes or it drifts more than 0.25 s. Each client counts
  down locally.
* `RpcWarning`, `RpcJump` (carrying the new state, so no client hears the jump before it knows the new seed),
  `RpcMessage`, `TargetMessage` and `TargetLost` are all `ExcludeServer`, or skipped for the host's own connection.
  The host already saw them locally.
* It installs `HubWorldMessages.Remote` on the server. `HubWorldMessages.Send(player, ...)` reaches that player's
  client (TargetRpc to the owner). `Broadcast` reaches every client. Texts are cut at 512 characters. Offline `Remote`
  is null and messages stay local.
* It applies the command line's shard seed (`NetShardSeed.Value`, from `-shard`) with
  `HubWorldDirector.UseShardSeed` on the server.

## NetPlayerRealm (player prefab)

`View` (SyncVar) holds the realm, the destination while in transit, the cycle the player went out in, the zone index,
name, PvP flag, dungeon difficulty, mob strength, reward multiplier and colour, and the arrival token. The server copies
it from the director's `PlayerRealmState` whenever it changes. Clients write it back into their `PlayerRealmState`, and
the distance from the city is measured locally. This feeds:

* the HUD zone label ("Safe Zone", "Dangerous Zone - PvP Active", dungeon difficulty);
* `HubCombatPolicy` PvP verdicts used by targeting and UI on clients, and duel checks;
* `RealmScene` hiding of the City while the local player is out.

ServerRpcs (owner only):

* `ServerConfirmArrival(token)`. Stale or forged tokens are ignored.
* `ServerUseGate(kind)`. It is rate-limited. The player must be alive, neither travelling nor awaiting an arrival, and
  inside, or within 4 m of, a gate of that kind in their own scene (`RealmGate.IsNear`). Then the director decides as
  it does for the trigger (city: gate open, player in the city; return gate: player in the world).

**Gates are decided on the server.** The server simulates every player's movement, so a gate's trigger fires for the
server's copy. `RealmGate` ignores a client's predicted copy (`NetAuthority.RunsRules`) and players awaiting an
arrival.

## NetDuels (global relay)

* `Duels` (SyncList of `DuelView`) holds the open duels. Players are named by `PlayerIds` (the network object id + 1,
  the same everywhere). It also carries the state, the deadline in network time (converted once per state, so jitter
  never resends a duel), the winner, the reason and the arena centre. Clients convert deadlines to their own
  `HubWorldDirector.Now` and call `DuelSystem.ApplyReplicated`. The client's `DuelBook` becomes a copy
  (`DuelBook.ApplySnapshot`). `CombatRules.PlayerVersusPlayer` answers Opponents for two fighters, the HUD prompt and
  countdown work, and the arena ring is created around the fighters on every client that sees them.
* `ServerDuelRequest(request, targetId)` (`RequireOwnership = false`) takes the asking player from the connection, never
  from the message. It is rate-limited (0.25 s per client). The request then goes through the same `DuelSystem` rules
  as single player: range and realm measured on the server, both alive, nobody already in a duel. A failure reason is
  sent back as a message.
* The "Challenge to a duel" interaction (`DuelChallengeTarget`) can reach the server twice: through the economy
  module's interactor relay and through the client's duel system. A repeated identical challenge is ignored silently.
* `DuelSystem.NameResolver` shows the `NetPlayer.DisplayName` in duel texts.
* On a client nothing ends, heals, saves or resets cooldowns. The server's duel system does that.

## City visual-only abilities

The abilities module runs casts on the server. Visual-only casts raise `CombatRules.VisualCast` there, and its own
cast messages show the cast to observers. `CityAbilityRelay` therefore only runs where the rules run, skips separated
players, and never plays anything. Its `Relayed` event is a server-side notification; subscribers must not replay
visuals while networked.

## Terrain viewers

A dedicated server must stream terrain (and its colliders) around every player out in the world.
`HubWorldDirector.TerrainViewers` (`Func<EndlessTerrain, IReadOnlyList<Transform>, bool>`) receives, every frame, the
list of transforms to stream around: this machine's player first, then on a server every player in the World or
heading there. When the hook is null or returns false, the terrain's single `viewer` gets the first entry, as before.
The terrain module's multi-viewer support plugs in here: when `EndlessTerrain` can stream around several transforms,
install `HubWorldDirector.TerrainViewers = (terrain, viewers) => { terrain.SetViewers(viewers); return true; }` (or the
equivalent call) while a session runs. Until then a dedicated server streams around one player only (see Limitations).

## Prefab and scene setup

| Object | Components |
|---|---|
| "Network World" (Bootstrap scene, NetworkObject observed by everyone) | `NetHubWorld`, `NetDuels` |
| Player prefab root (next to `NetworkObject`, `NetPlayer`) | `NetPlayerRealm` |
| City scene | `HubWorldDirector` as before (no NetworkObject). It is DontDestroyOnLoad on every machine. |

The City and World scenes must be in the Build Settings (Fish-Net loads them by name).

## Testing locally

1. Tools ▸ SimpleMovements ▸ Hub World ▸ Build City Scene / Build World Scene, then the networking setup menu.
2. Start a host and join with one or two clients on 127.0.0.1 (see 01).
3. Walk a client through the City Gate. The veil stays up until the World is loaded and the ground is there. The zone
   label and the PvP flag follow the server. The other players' HUD shows the same countdown.
4. On the server, use the director's context menu *Skip To 70 Seconds Before The Jump* and then *Jump Now*. Every
   client hears the warnings and the jump. Players out in the world are sent to the city with the loot-loss banner, and
   the "Next jump" countdown restarts for everyone.
5. Challenge another player in the City (Interact, then Y / N on the other client). The arena ring appears for both
   players and for onlookers. The fight is allowed only between the two, and the loser is saved at low health.
6. Start a dedicated server with `-shard 1234` twice. Both runs give the same worlds.

Edit Mode tests: `CustomEditor/Networking/Tests/HubWorldNetTests.cs` (the duel book copy, snapshots, the mirrored
clock, message forwarding, and the wire types).

## Limitations

* One World per process (the terrain's static caches). A dedicated server keeps one World for all players; it is
  unloaded when nobody is out, and at every jump.
* Until `EndlessTerrain` supports several viewers, a server generates terrain (and its colliders) around the first
  player out in the world only (the host's own player first). Another player far from them walks over ungenerated
  ground on the server, and an arrival there waits `arrivalGroundTimeout` and is placed at the terrain data's height.
* City and World overlap in space in one process. On the server both are loaded in the default physics scene. A
  World player near the walls touches both copies of the same walls, and gates ignore players of the other realm.
* On a host the City is hidden visually only. Objects created in the City after hiding (effects, drops) can show.
* The host's active scene stays the City. World lighting follows Fish-Net's preferred active scene for clients only.
* Cooldowns reset at a duel's start (`resetCooldownsAtStart`) are reset on the server. Clients see them through the
  abilities module's replication.
* Hub world messages are transient. A client that joins late misses earlier banners, but it is told the current
  dimension when it first syncs.
