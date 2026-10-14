# 10 - Dungeons: stacked run scenes, layouts without seeds, progressive reveal, server-only contents

In a network session the server generates and runs every dungeon. Clients never get the dungeon seed. They get the
layout of each floor when their party reaches it and build a copy that only shows things. Mobs, loot, traps and room
events are decided on the server, and their results reach the clients through the network.

Single player is unchanged. Without Fish-Net, or with Fish-Net installed but no session running, dungeons are generated,
built and played exactly as before. The new hooks are null or no-ops, and no component is added to built objects.

## How it works

### One stacked scene per run

When a group enters a dungeon portal on a server (`GameNet.NetScenes.ThroughNetwork`), `DungeonSession.Enter` asks
`DungeonScenes.CreateAsync` for a scene. It loads the template scene `DungeonSceneSettings.networkSceneName`
("DungeonInstance") through Fish-Net with **scene stacking**, so every run gets a copy of its own. The scene uses its
own physics when `ownPhysics` is on. The slot is reserved before loading starts, so two runs never share one.

- While the scene loads, the run stays `Entering`, with the group frozen at the portal. Generation starts when the scene
  exists (`OnLeaseReady`). If loading fails, the run ends as `Failed` and the group gets its controls back. If the run
  ended while loading (the cycle jumped, a player left), the scene is unloaded as soon as it arrives.
- Network sessions always use a run scene, even when the hub world turned `dungeonsInOwnScenes` off. A dungeon built
  into a shared realm scene could not be shown to its own party only.
- `DungeonScenes.Release` unloads a networked scene through `NetScenes.Unload`. The clients still in it unload it too.
- In a session the classic mode never pauses the world (`DungeonWorldPause`). The world is shared.

On a client, Fish-Net loads the instance when the client's player is moved in (`NetScenes.MoveToScene`, already used by
`DungeonSession.OnReady`). `DungeonScenes` **adopts** such a scene when it loads: it is recognised by its
`DungeonInstanceSceneMarker`. It gets a `Lease` with `Remote = true`, so `DungeonScenes.Find` works on clients too
(`CombatRules.Separation`, `HubWorldDirector`, `RealmObserverCondition`). The run header then gives the lease its slot
(`PlaceRemote`), so physics queries inside it are routed by `ScenePhysics` like on the server. A scene with physics of
its own is stepped by the `DungeonScenes` physics driver on the client as well, for debris, platforms and rigidbodies.

The server keeps its active scene (the City) and only moves objects into the run's scene. A client follows the scene it
was moved to.

### The relay: `NetDungeonRun`

`GameNet.NetDungeonRun` sits on a NetworkObject in the template scene. It is a scene object, so every stacked copy
has its own relay, and only clients whose player is in that run observe it. That is why no observer setup is needed:
the realm observer condition already requires the same scene. On the server it:

1. finds its run (`DungeonSession.RunAt`) and notices when the run's dungeon changes. That happens for a new dungeon,
   and also for *Next Dungeon*, which counts up the **generation**;
2. sends the **run header** (`DungeonNetHeader`): the profile's asset name, the label, the origin, the grid and cell
   size, the floor count, the exit portal action and the slot. The header never contains the seed;
3. sends **floors** as the party reaches them (see *Progressive reveal*), then the states of the elements on them;
4. forwards element states and messages to the run's clients;
5. validates clients' requests to use elements;
6. gives arriving players their controls back once their client has built the floor they stand on.

A client that starts observing the relay later receives the header, every floor sent so far and every element state of
those floors, in one go (`OnSpawnServer`). This covers players who join late and players who are moved in later.

### Layouts without the seed

`DungeonLayoutCodec` (always compiled, unit-tested) encodes one floor into a deflated `byte[]`. The packet holds the
floor spec, the full tile grid (types, flags, area and connection ids, exact floor, ceiling, roof and sky heights), the
areas (role, template, bounds, cells...), the connections (doors, platform tracks), the vertical links that touch the
floor, and the placements clients build, each with its index in the server's layout. Mobs, bosses, room-event
controllers and item pickups are never sent (`DungeonBuilder.BuiltOnClients`). The client's `DungeonLayout` holds only
the floors it was sent. Its `Seed`, `AttemptSeed` and request seed are 0.

The only randomness the presentation needs is the cave wall jitter of the floor meshes. In a session the server meshes
each floor with a **build seed** (`DungeonLayout.FloorBuildSeeds`) and sends it with the floor, so server and clients
build identical geometry and colliders. Prediction needs this. Single player keeps its old mesh seeds.

Why this is safe: a build seed is `HMAC-SHA256(key, attempt seed, floor)`, where the key is 32 random bytes the server
process creates and never sends (`DungeonBuildSeed`). A plain hash would not be enough. The dungeon seed has only 2^32
values, so a client could hash every candidate and match it. The secret key stops that, and the generator never reads
build seeds anyway.

For the same reason the **dungeon seed itself** is keyed on a server. A world portal's request seed comes from the
world seed and the portal's position, and clients know the world seed. `DungeonSession` therefore replaces the request
seed with `DungeonBuildSeed.SecretDungeonSeed(seed)`. The same portal still leads to the same dungeon on that server,
but no client can compute the seed. Offline the request seed is used as before.

### Progressive reveal

The arrival floor (where the players spawn, usually floor 0) is sent with the header. The server checks four times a
second (`revealInterval`) where the run's players stand, using its own positions:

- a player on floor *f* means floor *f* is sent;
- a player within `revealDistance` (18 m) of a way down from *f* (stairs, a drop, a climb or a spiral) means floor
  *f + 1* is sent. The stairs' geometry belongs to the floor below, so a player must have it before stepping in;
- a player on a floor whose chasm falls lead to the floor below (chasm rule other than *Death*) also means *f + 1* is
  sent.

Floors are sent in order and never ahead of these rules. Floors deeper than one below the deepest player are never sent.

### The client's copy

`NetDungeonRun` builds a **replica** (`DungeonInstance.IsReplica`) with `DungeonBuilder.BeginReplica`. It is placed at
the header's origin in the run's scene, and uses the profile found by name (`DungeonProfile.FindLoaded`). Each floor is
merged into the replica layout in order. It is meshed on a dungeon worker thread (`DungeonMeshing.BuildFloor`) and then
built a few milliseconds per frame (`BuildReplicaFloor`). The build includes geometry, water, void, stairs, the tile
kit, prefab rooms, doors and secret doors, floor mechanics visuals and the placements clients see. It does **not** bake
a NavMesh, spawn mobs or create item pickups. After a floor is built:

- every component in `DungeonNet.ServerOnlyOnReplicas` is switched off: room events, puzzles, shifting floors, gravity
  flips, chasm falls, pressure plates, tripwires, teleporters, dart traps, respawns, nests, sleepers and roamers. These
  only make decisions, and the server replicates what they change;
- states received for that floor are applied;
- the client tells the server (`ServerFloorBuilt`).

`DungeonFloorStreamer` follows the local player on the replica. `DungeonFloorAtmosphere` applies the theme and floor
atmosphere when this machine's player is inside (a networked owner copy counts), and releases it when the player
leaves or the scene goes. It never runs on a dedicated server. The host keeps the City active, so
`DungeonAtmosphere.Release` restores the settings of the scene it changed whenever that scene is not a dungeon scene.

### Arrival

`DungeonSession.OnReady` keeps a player frozen when `DungeonSession.ArrivalHold` says so. `NetDungeonRun` holds the
players of remote clients (the host's own player is not held). It calls `DungeonSession.ReleaseArrival` once that
client reports the floor under the player built, or after `ArrivalTimeout` (30 s, with a warning). The movement lock
(`IControlLock`) freezes input and gravity, so a client never falls through a floor it has not built yet. Teleports
made by the dungeon call `MovementStateMachine.NotifyTeleported` in sessions, so the predicted model snaps.

### Server-only contents

- **Mobs**: `DungeonBuilder.SpawnMob`, `DungeonInstance.SpawnMob` (nests), `DungeonRespawnDirector` and the legacy
  `DungeonMobSpawner` run only where `NetSpawn.ShouldSpawnHere`. Each mob is handed to `GameNet.MobNetSpawn.Spawn` with
  the run's scene. Dormant mobs (ambush waves) stay hidden on the server and are network-spawned when revealed
  (`DungeonSpawned.Reveal`), because Fish-Net does not replicate `SetActive`. Stand-in mobs (no prefab) are skipped in
  sessions, since nobody but the server would see them.
- **Item pickups** placed by the layout become networked world items at the root of the run's scene
  (`WorldItems.InstantiatePickable`, then `Publish`). Dormant ones are published when revealed.
- **Chest drops** use `WorldItems` too: offline this is a plain `Instantiate`; on a server it is a networked item in
  the chest's scene.
- Clearing a dungeon (*Next Dungeon*, the end of a run) despawns its mobs and pickups (`DungeonInstance.DespawnDetached`).
- `DungeonMessages.Show(context, text)` sends a message to the run's clients. The server shows it locally only when
  its own player is in that run.

### Portals and exits

`DungeonPortal`, `Portal` (world portals) and `DungeonSession.Enter/UsePortal/Advance` act only where
`NetAuthority.IsServer`, on the server's copies of the players. A client's predicted copy never starts or leaves a run.
A world portal's lifetime runs on the server and ends with `NetSpawn.Destroy`. *Scene To Load* portals (single player
only) are ignored in sessions, with a warning. Party members travel together as before.

## Element ids and the generic state / interaction API

Every interactive object the builder creates in a session gets a `DungeonElement` with an id that is the same on the
server and every client (`DungeonNetIds`):

| What | Id |
|---|---|
| A placement's object (chest, door, gate, key, lever, shrine, herb, rest point, altar, breakable, portal, teleporter, trap...) | its index in the server's layout placements (`ForPlacement`) |
| A secret door | `ForSecretDoor(floor, cell)` |
| A climb | `ForLink(link id)` |
| A floor's own mechanics | `ForFloor(floor)` (reserved, see below) |

Ids come from the layout, not from build order. They do not depend on objects only the server creates, or on which
floors a client has. `DungeonNetIds.Of(component)` and `ElementOf(component)` work on any child, and
`DungeonNetIds.Find(dungeon, id)` (or `DungeonInstance.Element(id)`) looks an element up.

**Server to clients, "element state changed":** `DungeonNet.Publish(component, state, payload, channel)`. There are
two channels per element. `State` holds the object's own state. `Presence` is `Hidden`, `Revealed` (the payload holds
the position it appeared at) or `Removed`. The server keeps the last value of each channel for players who arrive
later. A client calls `IDungeonNetState.ApplyNetState(state, payload)` on every component of the element. A state for a
floor that is not built yet is kept until that floor exists.

**Clients to server, "interact with element id":** a client-only player's Interact key on a replica's `Interactable`
ends in `DungeonInteractable.Interact()`. It is reached through `PlayerInteractor`'s local fallback or through
`Player.NetInteractHandler`, which `NetDungeonRun` chains in front of the previous handler. That call sends
`ServerInteract(generation, id)`. The server checks:

- the generation is current;
- at most one request per 0.2 s per client;
- the caller's player is alive, is a participant of this run and is in its scene;
- the element exists, is active and lies on a floor that was sent;
- the element has an `IDungeonNetInteract`;
- the player is within its `NetInteractRange` plus 0.75 m, horizontally, and within 3 m vertically, using the server's
  positions.

Only then does it call `NetInteract(player)`, which runs the object's normal server code.

### Implemented end to end

| Object | Server | Clients |
|---|---|---|
| Dungeon portals | trigger acts on the server only | shown; trigger ignored |
| Gates, vault doors, shortcut doors, shifting walls (`DungeonBarrier`) | decide; `SetClosed` publishes 1/0 | follow the state (an old state is applied at once); no unlocking or opening of their own |
| Secret doors | searching and levers open them; publish 1 | sink when told |
| Chests | open (hold-to-use or request); loot dropped as networked world items | lid opens on the published state |
| All `DungeonInteractable`s (shrines, herbs, rest points, altars, map tables, levers, nests, breakables) | used by hold-to-use (server positions) or validated requests; publish the number of uses | show it: used-up look, lever handle, map known, nest shrinking or destroyed, barrel shattered |
| Rest points | revive fallen participants with health in sessions (`CombatEntity.Revive(fraction)`, replicated by the status module); single player keeps `Revive()` | the revived players' health arrives through the status module |
| Keys | picked up by proximity; publish `Removed` | hidden; no pickup |
| Dormant objects | `DungeonSpawned.Reveal` publishes `Revealed` (with position); mobs and pickups are network-spawned | revealed at that position |
| Poison gas | timer and damage; publishes 0/1/2 (quiet, flooding, shut) | shows the gas; no timer or damage |
| Messages | sent to the run's clients | shown to the local player |

### Networking another object type (the pattern)

1. On the server, after the object changes, call `DungeonNet.Publish(this, state, payload)`. Publish whatever a client
   needs to show the change, and nothing else. Use the `Presence` channel to show or hide.
2. Implement `IDungeonNetState.ApplyNetState` to show that state on the client. Do not raise gameplay events, apply
   damage, give items or move players there.
3. Guard the object's own decisions (timers, proximity checks, triggers) with `if (DungeonNet.IsReplica(this)) return;`.
   If the component only decides and shows nothing, add its type to `DungeonNet.ServerOnlyOnReplicas` instead.
4. If players use it, implement `IDungeonNetInteract` (a `DungeonInteractable` already does).
5. A floor's own mechanics (the `DungeonChasm`, `DungeonGravityShift` and `DungeonShiftingFloor` on the floor root) need
   a child object of their own carrying `DungeonNetIds.ForFloor(floor)`. Children of the floor root must not inherit an
   element.

## What the prefab and scenes need

- **Dungeon instance scene** (Tools ▸ SimpleMovements ▸ Network ▸ Create Dungeon Instance Scene, in the build): its
  "Dungeon Instance" object holds `DungeonInstanceSceneMarker`, a `NetworkObject` (a scene object, with no observer
  override, so the realm observer condition shows it to the players in that copy of the scene only) and
  `GameNet.NetDungeonRun`. A scene made before the relay existed needs the last two added by hand (or the scene
  deleted and created again). A server logs an error when a run scene has no `NetDungeonRun`.
- **No global relay**: the dungeon needs nothing on the "Network World" object; every run has its own relay.
- **Hub world settings**: `dungeonScenes.networkSceneName` = "DungeonInstance".
- **Dungeon profiles** must have unique asset names. They must be loaded on clients: referenced by a loaded scene or
  asset (portal spawners, hub world settings), or placed in a Resources folder.
- **Mob prefabs** networked (mobs module), and **item prefabs** or the world item carrier (inventory module), for
  their contents to be visible.

## Testing locally

1. Build the item database and network the prefabs (Network menu), and create the dungeon instance scene.
2. Start a host and a client on 127.0.0.1. Walk both players into a world portal together.
3. Both arrive frozen for a moment and get their controls once their floor is built. The client shows the same rooms,
   doors and decoration as the host, its mobs are network-spawned, and its Console shows no seed (see the run header in
   the debugger).
4. Open a chest from the client with the Interact key: the lid opens on both machines and the loot appears on the
   ground for both. Walk to the stairs: the floor below is built on the client before you reach the first step.
5. Have the host pick up a vault key: the key vanishes on the client and the vault door sinks on both.
6. Join a third client later and walk it into the same run's scene (for example through a party portal). It gets every
   floor and state so far.
7. Edit Mode tests: `GameNet.Tests.DungeonNetTests`. They cover the codec round trip, server and client meshes being
   identical, seeds, ids and offline behaviour.

## Limitations

- Client-side Fish-Net looks up scene objects by scene id. If one client has two copies of the instance scene loaded at
  once, which only happens when it is moved straight from one run into another, the second copy's `NetDungeonRun` can
  fail to spawn. Runs always end through a realm (City or World), so this does not happen in normal play.
- Visual timers that still run on both sides are not synchronised: blade traps, hazards, moving platforms, flickering
  lights, spectators. Their damage is the server's (`CombatEntity.ApplyDamage` is ignored on replicas), so only the
  phase may differ. Gravity flips, chasm falls, teleporters, climbing and moving platforms move players outside the
  predicted movement (see 02-Movement-Prediction), so the owner may briefly rubber-band.
- Pressure-plate puzzles, nests hatching and room events run on the server. Their visible results (gates, reveals,
  messages) are replicated, but a plate does not show as pressed on clients.
- Chest drops and drops from other prefabs that are neither item pickups nor networked prefabs exist on the server only.
- `RealmObserverCondition.distanceInDungeons` must stay off unless the relay is exempted from it: the relay's object
  stands at the template's position, not near the players.
- Generic `Storage` loot containers are not networked by element (use `DungeonChest` with item drops).
