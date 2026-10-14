# 11 - World terrain: deterministic chunks, multi-viewer streaming, world portals, weather

The open world is not sent over the network. Every machine generates its own copy from the **world seed** (which the
hub world relay sends - never a dungeon seed): the same heights, water, biomes, colliders and placed objects (trees,
rocks, props) with the same ids. The server decides everything in that world that has gameplay value: it spawns the
world portals, removes placed objects for good, and runs the weather clock. Single player is unchanged: without
Fish-Net, or with Fish-Net installed but no session running, the terrain streams around its single viewer and the
spawners place everything locally, exactly as before.

## How it works

### Determinism: same seed, same world

Generation is a pure function of the world seed, the `TerrainGenerator` settings and the hub world's `WorldAreas`
(the flattened, kept-clear city area). Both the seed and the areas are installed by the hub world on the server and
on each client before the World scene's `TerrainGenerator` wakes up.

What was checked:

- **Randomness.** Heights, erosion, water (lakes, ponds, rivers, oceans, sea stacks), volcanoes, biome layout, UV
  variation and object placement use seeded `System.Random` instances or the stateless `PlacementRandom` hash. Nothing
  in generation reads `UnityEngine.Random`, `Time`, the camera, the frame count or the platform. `UnityEngine.Random`
  is used only outside generation: weather effects (lightning, tornado spawn points), `Portal` despawn times
  (server only), `GetRandomActiveChunk`. `ObjectSpawner` and `SpawnerBase` are legacy and unused.
- **Order of generation.** Chunks are generated on worker threads in no fixed order, and the server streams around
  several players while each client streams only around its own. The one setting that made the result depend on the
  order is the biome layout with *Order-Independent Layout* switched off: neighbouring cells already generated
  influenced the biome of new ones. A `TerrainGenerator` that wakes up during a network session therefore always uses
  the order-independent layout (`TerrainGenerator.OrderIndependentBiomeLayout`), and logs once when it overrides the
  inspector setting. The decision is made once, in `Awake`, so a world never mixes two layout rules.
- **Caches.** The Voronoi point cache, the water feature caches and the landmark picks are keyed by seed and/or
  cleared in `TerrainGenerator.Awake`. Cached chunk data (*Chunk Data Cache Size*) gives back exactly what was
  generated before.
- **Ids.** A placed object is identified by `PlacedObjectId` = chunk coordinate + placement index. Because placement
  is deterministic, the same id names the same tree on every machine.

### Streaming around several viewers

`EndlessTerrain` streams chunks around a **set** of viewers:

```csharp
terrain.SetViewers(IReadOnlyList<Transform> viewers); // first = primary viewer (also written to terrain.viewer)
terrain.ClearExtraViewers();                         // back to the single viewer field
IReadOnlyList<Transform> v = terrain.Viewers;        // what was streamed around in the last update
```

- A chunk is **created** when it comes within reach of any viewer. The primary viewer's chunks come first, and each
  viewer's nearest chunks first. It is **shown** (active: collider, objects and spawners running) and given its
  **NavMesh** by its distance to the *nearest* viewer, and it is **unloaded** only when it is far from *every* viewer.
  Worker-thread job priorities and the object instantiation order also use the nearest viewer.
- The mesh **level of detail** follows the *primary* viewer only, which is the one this machine looks through. On a host,
  chunks streamed only for remote players keep coarse render meshes. Their collider and NavMesh always use the
  full-detail mesh, so gameplay is unaffected.
- The `viewer` field keeps working: it is the primary viewer. Assigning it directly drops the extra viewers and returns
  to single-viewer behaviour. With one viewer every distance, range and unload decision is computed exactly as before.
- `SetViewers` can be called every frame with a reused list. It allocates nothing, and nulls and duplicates are skipped.

Who sets the viewers: the hub world's `HubWorldDirector.TerrainViewers` hook is installed by `GameNet.WorldTerrainNet`
(always compiled). During a network session it calls `SetViewers` with the list the hub world builds every frame:

| Machine | Viewers |
|---|---|
| Dedicated server | every player out in the World (or travelling there) |
| Host | the host's own player first, then every other player in the World |
| Client | its own player only (it renders only its own surroundings) |
| Single player | the hook declines; the hub world sets the single `viewer` as before |

### Headless dedicated server

When `GameNet.NetAuthority.IsHeadless` (a dedicated server in batch mode or a server build), the chunks skip work that
only serves rendering:

- no terrain material, splat-map textures or wetness map;
- no coarser LOD meshes: the full-detail mesh stays, because it is also the collider's and the NavMesh's source;
- no water mesh or water renderer. The water object and its swim trigger (`WaterVolume`) are kept;
- `WeatherSystem` runs its state and clock but makes no particles, fog, lighting, sounds, lightning or tornadoes (this
  applies to every dedicated server, headless or not, and to a host while its weather viewer is another player - for
  example its own player is in the City).

Kept on purpose: the terrain `MeshRenderer` component (NavMesh surfaces collect render meshes by default), the placed
objects as they are (their colliders matter), and Far Objects switching (it keeps colliders near players). Splat data
is still computed on the worker threads because object placement reads it.

### World portals: server-spawned network objects

`PortalSpawner` (one per chunk) places portals only where `GameNet.NetSpawn.ShouldSpawnHere`: single player, host or
dedicated server.

- Sites, spots and difficulty are decided on the server exactly as in single player (`PortalSitePlanner`, `FlatSpots`
  on the chunk's exact data and NavMesh, `WorldSpawnHooks.ApplyPortalDifficulty`, closed sites in
  `WorldSpawnRegistry`).
- Once set up, the portal is handed to `GameNet.WorldTerrainNet.SpawnWorldObject`. If the prefab has a `NetworkObject`,
  the portal leaves the chunk's hierarchy (Fish-Net spawns root objects only), is moved into the World scene (a
  server's active scene is the City) and is network-spawned. The realm observer condition shows it to the players in
  the World near it.
- It is despawned for everyone through `GameNet.NetSpawn.Destroy` when its chunk unloads on the server (no player near),
  when its site is closed, or when it despawns by itself (`Portal`'s despawn time, decided on the server).
- Clients place nothing. A client that still has portals it placed before joining removes them. Players enter a
  portal on the server: `Portal.OnTriggerEnter` acts on the server's copy of the player (dungeon module).
- Player proximity (*Min/Max Distance From Player*) counts, during a session, only players in the chunk's scene,
  because the City overlaps the World in space (`WorldSpawnRegistry.DistanceToNearestPlayer(..., scene)`).
- `WorldSpawnRegistry.Paused` (a single player in a dungeon) no longer pauses world spawners during a network session.
  The World is shared, and one player's dungeon must not stop it for the others. A disabled terrain still pauses them.
- A portal prefab **without** a `NetworkObject` still works on the server, but clients cannot see it. The server logs
  a warning once per prefab.

Mobs are spawned by `MobSpawner` (mobs module, page 06) with the same rules.

### Removed placed objects (harvest state)

There are no harvestable resources in chunk generation yet: placed objects are scenery. What exists is the id scheme
and the replication of "this object is gone", so harvesting or destroying scenery can be built on the server:

- On the server (or offline), `EndlessTerrain.RemovePlacedObject(GameObject)` or `RemovePlacedObjectById(PlacedObjectId)`
  destroys the object, remembers it (it is skipped when its chunk is generated again) and raises
  `EndlessTerrain.PlacedObjectRemoved`.
- `NetWorldChunks` adds the id to its `Removed` SyncList, tagged with the world seed (`RemovedSeed`). Every client then
  makes its own terrain's removed set equal to the server's (`EndlessTerrain.SyncRemovedObjects`). Loaded objects are
  destroyed at once, and chunks generated later never create them. Clients that join later receive the whole list.
- On a client, `RemovePlacedObject` and `RemovePlacedObjectById` refuse and return false: only the server decides.
- `SetRemovedObjects` (restoring a save) on the server replaces the whole list for everyone.
- The list follows the server's World terrain. A new terrain (next cycle, or the World unloaded and loaded again)
  starts from that terrain's own removed set, and clients apply a list only to a terrain with the same world seed.

### Weather

The weather is a function of place, world seed and *weather time*. Every machine's `WeatherSystem` would agree if
their clocks agreed. They do not, because each loads the World at a different moment. `NetWorldChunks` therefore
sends the server's `WeatherSyncState` (`WeatherSystem.CaptureSyncState`): the weather clock with the network time it
was read at, the forced weather (kind, strength, seconds left) and the time of day.

- It is sent whenever the forced weather, the time of day, the world or the weather system changes, and the clock is
  re-sent every *Weather Resend Seconds* (10 by default).
- Each client applies it (`WeatherSystem.ApplySyncState`). The clock is run forward by the time since it was read, at
  most 10 minutes. Forced weather is set, changed or cleared to match. Between messages the client runs the clock
  itself at the same rate.
- A client applies the state only once its world is the server's (same world seed). During a cycle jump it waits.
- The hub world's per-cycle forced weather (the dimensional modifier) is forced on the server. Clients follow it
  through this state even if their own director forced it too.

Effects stay local presentation around each machine's viewer. Lightning bolts and tornado positions differ between
machines, and so do thunder and particles. A tornado pushes only rigidbodies that this machine decides
(`NetAuthority.RunsRules`). On a client it never pushes a networked object, because the server moves those.

## What is authoritative and what is replicated

| Thing | Decided by | How clients get it |
|---|---|---|
| Terrain, water, biomes, colliders, NavMesh | generated on every machine | world seed (hub world relay) + deterministic generation |
| Placed objects (trees, rocks, props) | generated on every machine | same; ids are chunk + placement index |
| Removal of a placed object | server | `NetWorldChunks.Removed` (+ `RemovedSeed`) |
| World portals (where, when, difficulty, closing) | server | network-spawned portal objects |
| World mobs | server (mobs module) | network-spawned mobs |
| Weather clock, forced weather, time of day | server | `NetWorldChunks.Weather` |
| Weather effects (particles, lightning, tornado) | each machine, locally | not replicated (presentation) |
| Which chunks exist | each machine, from its own viewers | not replicated |

`NetWorldChunks` has no ServerRpc. Clients send it nothing, so there is nothing to validate.

## What the prefab and scenes need

- **Bootstrap scene, "Network World" object:** add `GameNet.NetWorldChunks` next to the other global relays. Its
  options are *Sync Weather* (on) and *Weather Resend Seconds* (10).
- **World portal prefabs** (the EndlessTerrain's *Portals > Prefabs*): a `NetworkObject` on the root and the prefab
  in Fish-Net's spawnable prefabs. No `NetworkTransform` is needed, because portals do not move.
- **World scene:** unchanged (`TerrainGenerator`, `EndlessTerrain`, optional `WeatherSystem`). *Bake NavMesh* should
  stay on: the server's mobs and portals need NavMesh around every player.
- Placed-object prefabs must **not** have a `NetworkObject`. They are generated locally on every machine.

## Testing locally

1. Tools > SimpleMovements > Network > Create Bootstrap Scene (make sure "Network World" has `NetWorldChunks`), and
   network the world portal prefabs.
2. Run > Server And Two Clients (or Play In Editor As > Host plus a built client joining 127.0.0.1).
3. Walk both players out of the City in different directions:
   - each client generates the same terrain and trees at the same places (compare screenshots at the same spot);
   - on the server (Scene view or the SpawnerManager overlay), chunks exist around **both** players, and mobs and portals
     appear near each;
   - walk away from a portal with every player: it disappears for everyone when the server unloads its chunk, and is
     placed again, identically, when someone comes back.
4. Enter a world portal from a client: the server starts the dungeon run for that player.
5. Weather: force a weather on the server (WeatherSystem inspector buttons on the host) and watch it change on the
   clients; check that a late-joining client shows the same weather as the others.
6. Removed objects: on the host, call `EndlessTerrain.RemovePlacedObject` on a tree (for example from a debug script).
   It disappears on every client, stays gone when the chunk unloads and comes back, and is gone for a client that
   joins afterwards.
7. Headless: Build > Local Dedicated Server (headless), connect two clients, and check that the server's memory stays
   low (no terrain textures) while mobs still walk on NavMesh around both players.
8. Single player (no session): the world behaves exactly as before.

Edit Mode tests: `CustomEditor/Networking/Tests/TerrainNetTests.cs` (multi-viewer distances, the viewer API, the
offline viewer hook, removed-object ids and sync, weather state capture/apply).

## Limitations

- **Floating-point determinism** holds for the same build on the same machine or CPU architecture, which is what local
  testing uses. A server and a client built with different scripting backends or for different CPU architectures
  could round differently. A borderline object placement could then differ, and its placement index would then name
  different objects on the two machines. Movement and combat are unaffected, because the server decides them.
- A World scene that was already open **before** the session started (testing the World scene directly, then starting
  a host) keeps the layout rule it woke up with. With *Order-Independent Layout* off it may then differ from the
  clients' worlds. Start sessions from the Bootstrap scene.
- The removed-object list is kept per World terrain on the server. It is not saved across server restarts unless the
  game saves `EndlessTerrain.RemovedObjects` and gives it back with `SetRemovedObjects` on the server.
- Only one terrain (the first enabled World terrain) is replicated. Additional `EndlessTerrain`s stay local.
- With no viewer at all (a dedicated server with nobody in the World) chunks stay as they are until a viewer comes
  back, as in single player. The hub world unloads an empty World after its delay.
- NavMesh is global in Unity: a City NavMesh baked at the same coordinates as the World's city area overlaps the World's
  chunk NavMesh on a server. The hub world keeps the World's city area free of mobs and portals.
- Weather effects (lightning, tornadoes) are not synchronized, only the weather itself.
