# Caves 06 — Performance, Networking and Extending

## 1. Where the work happens

| Work | Thread | Cost (measured headless, 07 §2) |
|---|---|---|
| The whole pipeline incl. the Cave Mining Stage (all levels; the stage plans the levels in parallel) | dungeon worker (`DungeonWorkers.Pool`) | about 82 ms per cave on average (Small to Huge) |
| Rock formation mesh data, per level | dungeon worker, one job per level | about 63 ms per cave in all |
| Dungeon meshes | dungeon worker (as before) | |
| Mesh colliders (dungeon and formation chunks) | Unity's job threads (`Physics.BakeMesh`), before the level's NavMesh | |
| Building objects, deposits, passages | main thread, within the build's frame budget (`ShouldYield`) | |
| NavMesh | `NavMeshSurface.UpdateNavMesh` in the background, per level | |

On the main thread the build only turns finished data into objects:

* **No object per rock.** Formations are batched per mesh chunk (*Build ▸ Mesh Chunk Cells*): one renderer for the rock,
  one for the crystals and one mesh collider (simple prisms, not the visible shapes) per chunk. Over the 502 measured
  levels a level averaged some 56 standing formations, 205 stalactites and 22,000 formation vertices, in a handful of
  objects.
* **Shared meshes.** Generated deposits use three shapes per kind of deposit, made once per session. Materials are one
  per colour.
* **Pooling.** Generated deposit objects return to their pool (`CaveDepositArt`, kept across scenes) when a cave is
  cleared and are reused by the next cave (they leave the dungeon without its placement tag and network identity, so its
  teardown leaves them alone). Deposits made from a prefab are made per build, because objects that keep state are never
  reused by the dungeon's object pool.
* **Frame budget.** Every placement and every chunk checks the build's budget and yields.
* **NavMesh.** Formation and deposit colliders are part of each level's bake, so creatures path round them with no
  runtime obstacles.

## 2. No regeneration on revisit

`DungeonGenerationCache` keeps the last *Build ▸ Keep Generated* generations of each profile (4 for caves), on the
server or offline. Each one holds the finished layout and its mesh data. Entering a cave again (the same request: seed,
size, difficulty, floor count, style, depth, network mode, and the same profile revision) skips the pipeline and the
meshing completely and only builds. Editing a profile, its stages or its resource table makes a new revision, so old
generations are never reused for a changed profile. Generations are read-only once kept; the build never changes a
layout.

The world side is cheap too: cave sites are a hash per region; a mouth is built once per chunk load (two meshes).

## 3. Networking (Fish-Net)

Caves follow the dungeon system's networking. Nothing else is required.

| Part | Server (or offline) | Client |
|---|---|---|
| Cave mouths in the world | built from the world seed | built the same way (scenery, no network object) |
| Walking into a mouth | the server's copy of the player enters the server's trigger → `Portal.TryEnter` | the local player's trigger darkens the screen only |
| The cave | generated (secret-keyed seed), built, run | receives each level (grid, flags, area tags, placements, build seed) and builds a **presentation-only** copy: the same formations, deposits and passages |
| Deposits | the real seams: pickaxe hits (server-side weapon hits), lumps into the server's inventory, the cave's memory; a loose stone gathered by hand only once the client held Interact on that seam for its gather time (`IDungeonNetHold`, timed by the server) | `OreDeposit.ShowOnly`: shows the server's state (`DungeonNet.Publish` → `ApplyNetState`); gathering by hand is relayed to the server by the deposit's element id (`DungeonNet.TryRelayInteract` and `TryRelayHold`, through `NetInteractor`) |
| Creatures | spawned, made tougher (Deep-Dweller), network objects | the server's mobs |
| Chests, containers | opened / broken state kept and published | shown as the server says |
| The fade | — | the player is handed to the server, which takes it by locking its controls (they come back to the client with its movement). While they are locked the screen stays dark, however long the cave takes. It lights up once its copy of the cave is built and the player stands in it, once it is moved back to the world, once the server lets go of it where it stood (the visit failed), or (never taken: refused) after `CaveTransition.RefusedAfterSeconds` |

Every deposit's element id is its placement's id in the server's layout, the same on every machine (and so is
`CaveDeposit.PlacementId`). A client's copy also knows how deep the run is and its difficulty (they travel in the
run's header, never the seed), so its cave shows the same name ("- Depth N") and depths as the server's. A player who
arrives later gets every deposit's last state with the level. Two groups in two separate visits of one cave at the
same time each mine their own copy. The cave's record keeps one lump count per seam for both, so no lump is ever given
twice. A shared visit (*Sharing: Shared Run*) is found by a key of the cave's own, even after the mouth's chunk was
unloaded and loaded again.

## 4. Extending: the build-extension API

The cave system builds its part through a generic hook that any custom stage can use (`Procedural/Dungeon/Build/
DungeonBuildExtensions.cs`). A `DungeonStageAsset` that also implements `IDungeonBuildExtension` is asked for an
`IDungeonBuildSession` whenever a dungeon of its profile is built, and also for every network client's copy:

```csharp
public class MyStage : DungeonStageAsset, IDungeonBuildExtension
{
    public override void Prepare() { /* main thread: copy settings for the worker */ }
    public override void Run(DungeonContext ctx) { /* worker thread: add Placements of your own kind, flag cells */ }
    public IDungeonBuildSession BeginBuild(DungeonBuildContext ctx) => new MySession(ctx);
}

class MySession : IDungeonBuildSession
{
    public bool ShouldBuild(Placement p, int index) => true;          // false leaves a placement out
    public bool TryBuildPlacement(Placement p, int index, Transform parent, Vector3 local, Quaternion rot,
        out GameObject built, out GameObject prefab, out string name) { /* your placement kinds */ }
    public void OnBuilt(GameObject go, Placement p, int index) { }    // every placement, tagged, ids assigned
    public IEnumerator BuildFloor(int floor) { yield break; }         // per level, before its NavMesh bake
    public void OnFullyBuilt() { }
    public void Teardown() { }                                        // give pooled objects back
}
```

A profile uses one Cave Mining Stage: a second one does nothing (it warns, and the setup checks say so), since a
cave's deposits index one resource table.

`DungeonBuildContext` gives the instance, layout, compiled profile, the build's materials (per surface and flat
colours), the object pool, whether it is a client's copy, the frame budget (`ShouldYield`), floor roots, `CreateMesh`
(owned by the dungeon), `AddCollider` (cooked before the bake) and `TrackPooled`. Sessions are called in order: placements
as each level is built (`ShouldBuild`, `TryBuildPlacement`, `OnBuilt`), then `BuildFloor` before the level's colliders
are assigned and its NavMesh is baked, then creatures, `OnFullyBuilt` once (not for a client's copy), and `Teardown` when
the dungeon is cleared. Exceptions in a session are logged and never stop the build.

The cave's own pieces are reusable as well: `CaveRockMesh` (seeded boulders, stalagmites, columns, crystals, collider
prisms), `CaveWalls.WallPoint` (where a cave wall is drawn), `CaveMouthBuilder` (rock passages), `CaveTransition` (a
fade with a caption), `CavePassage` (a trigger with a fade that sends players through a portal).
