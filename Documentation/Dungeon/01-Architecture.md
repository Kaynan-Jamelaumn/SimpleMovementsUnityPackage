# Dungeon 01 — Architecture Overview

## 1. Concept

A dungeon is a **stack of floors**. Each floor is a 2.5D grid of square cells (1.5 m by default). Every cell is solid
rock or open ground, and every open cell stores its own floor height and ceiling height. Floors are joined by
**stairs** (two-way) and **drops** (one-way holes). Inside a floor, **areas** (rooms, halls, cave chambers, landings)
are joined by **connections** (doors, corridors, tunnels, openings, breaches, secret passages).

Generation is split into two halves that never mix:

| Half | Where it runs | What it does | Touches Unity objects? |
|---|---|---|---|
| **Generation** (stages + mesh data) | a background worker thread (`DungeonWorkers.Pool`) | decides everything as plain data: floors, rooms, corridors, heights, roles, mobs, loot, props, mesh vertices | **never** |
| **Build** | the main thread, a few ms per frame | turns that data into GameObjects: meshes, colliders, NavMesh, portals, props, mobs | yes |

So the expensive thinking never stalls the game. The same request and profile always give the same dungeon, whatever
thread or order the work runs in.

## 2. Macro flowchart — from a world portal to a playable dungeon and back

```mermaid
flowchart TD
    P["Player walks into a world Portal<br/>(Essentials/Portal.cs)"] --> Q{"Leads to a dungeon?<br/>(Should Instantiate Dungeon)"}
    Q -- no --> SC["Load Scene To Load<br/>(scene mode)"]
    Q -- yes --> G{"DungeonSession.CanEnter?<br/>not already inside or entering,<br/>3 s re-entry cooldown over"}
    G -- no --> X0["ignore"]
    G -- yes --> R["Portal.BuildRequest():<br/>seed = hash(world seed, portal position)<br/>size, difficulty"]
    R --> E["DungeonSession.Enter:<br/>remember return pose, freeze controls,<br/>find or create DungeonManager at y = -10000"]
    E --> WP["DungeonWorldPause.Pause:<br/>EndlessTerrain, WeatherSystem, SpawnerManager off,<br/>world spawners paused"]
    WP --> CP["DungeonManager: CompiledProfile.Compile<br/>(main thread, copies all settings)"]
    CP --> W["Worker thread job"]
    subgraph Worker["Worker thread (no Unity objects)"]
        W --> PL["DungeonPipeline.Generate<br/>8 stages, up to 6 attempts"]
        PL --> MS["DungeonMeshing.BuildAll<br/>mesh data per floor"]
    end
    MS --> B["Main thread: DungeonBuilder coroutine<br/>(6 ms per frame)"]
    subgraph Build["Main thread, time-sliced"]
        B --> BF["per floor: meshes, colliders (cooked on job threads),<br/>stairs, tile kit, prefab rooms, doors, portals, props, loot"]
        BF --> NM["NavMesh bake per floor (async)"]
        NM --> MB["mobs placed on the NavMesh"]
    end
    MB --> RD{"first 2 floors done?"}
    RD -- yes --> OK["Ready: teleport player to spawn,<br/>apply theme atmosphere, controls on"]
    OK --> PLAY["Play: floor streamer keeps nearby floors active,<br/>deeper floors keep building"]
    PLAY --> EX{"Which portal does the player use?"}
    EX -- "Entrance portal" --> OUT["Exit(false): clear dungeon, restore atmosphere,<br/>resume world, teleport back"]
    EX -- "Exit portal = CompleteDungeon" --> OUT2["Exit(true): same, dungeon counts as completed"]
    EX -- "Exit portal = NextDungeon" --> NX["Enter(request.Next()):<br/>new seed, depth + 1, harder"]
    NX --> CP
    OUT2 --> CL["world portal site may close<br/>(CloseAfterCompleting)"]
    PL -- "every attempt failed" --> F["Failed: undo everything,<br/>player back in front of the portal"]
```

Two guarantees shape this flow:

- **Controls are never left disabled.** Success (`OnReady`), failure (`OnFailed`) and exit all end with `SetControls(true)`.
- **The world does not keep generating above the dungeon.** `EndlessTerrain` follows the player's x/z, so without the
  pause it would stream chunks around the dungeon's position.

## 3. The generation pipeline (inside the worker)

```mermaid
flowchart LR
    A["1 MacroPlan<br/>floors, grid, footprints,<br/>styles, stairs, drops,<br/>entrance/exit rooms"] --> B["2 Layout<br/>areas per floor:<br/>rooms, BSP, caves,<br/>hybrid, maze"]
    B --> C["3 Connectivity<br/>which areas join:<br/>tree + loops,<br/>connection kinds"]
    C --> D["4 Roles<br/>main path, progress,<br/>difficulty, boss,<br/>treasure, secrets"]
    D --> E["5 Carve<br/>templates, corridors<br/>(A*), doors,<br/>heights, ceilings"]
    E --> F["6 Validate<br/>reachability,<br/>repairs, sanity"]
    F --> G["7 Analysis<br/>walking distance,<br/>wall distance,<br/>chokepoints"]
    G --> H["8 Population<br/>portals, spawn,<br/>mobs, loot, props"]
```

| Stage | Class | Per floor in parallel? | Can fail the attempt? |
|---|---|---|---|
| 1 MacroPlan | `MacroPlanStage` + `AnchorPlanner` | no (plans all floors together) | yes — no room for stairs or the entrance/exit room |
| 2 Layout | `LayoutStage` + one `ILayoutStrategy` per style | yes | yes — an anchor room was lost |
| 3 Connectivity | `ConnectivityStage` | yes | no |
| 4 Roles | `RolesStage` | no (needs the whole dungeon graph) | yes — a required role (Boss) found no room |
| 5 Carve | `CarveStage`, `CorridorRouter`, `HeightPass` | yes | no (unroutable connections are warnings) |
| 6 Validate | `ValidateStage` | yes | yes — an area stays unreachable, a landing is blocked… |
| 7 Analysis | `AnalysisStage` | yes | no |
| 8 Population | `PopulationStage` (`FloorPopulator`) | yes | yes — no player spawn |

A failing stage calls `ctx.Fail(reason)`, which throws `DungeonGenerationException`. The pipeline catches it and
**restarts the whole dungeon** with a derived seed (`Hash(seed, 0xA77E, attempt)`), up to *Max Attempts* (6). Each
attempt's failure reason is kept in the report.

**Custom stages:** a profile may list `DungeonStageAsset`s (ScriptableObjects implementing `IDungeonStage`). Each has
a `slot` (`AfterMacroPlan` … `AfterPopulation`), and the pipeline inserts it right after that built-in stage. Custom
stages follow the same contract: plain data only, no Unity objects.

## 4. Data model

```mermaid
classDiagram
    class DungeonLayout {
        Seed, AttemptSeed, Attempt
        CellSize, FloorSpacing, Width, Height
        Floors : List~FloorLayout~
        Links : List~VerticalLink~
        MainPath : List~AreaRef~
        PlayerSpawn, EntrancePortal, ExitPortal : DungeonPose
        Placements : List~Placement~
        Report : GenerationReport
    }
    class FloorLayout {
        Index, Spec : FloorSpec
        Grid : TileGrid
        Areas : List~Area~
        Connections : List~Connection~
        Anchors : List~Anchor~
        ArrivalArea, DepartureArea
        DistanceFromArrival : int[]
        WallDistance : float[]
    }
    class TileGrid {
        Width, Height
        Type : CellType[]
        Flags : CellFlags[]
        Area : int[]
        Connection : int[]
        FloorHeight : float[]
        CeilingHeight : float[]
    }
    class Area {
        Id, Kind, Role, Tag, Style
        Cells, Bounds, Center
        TemplateIndex, AnchorIndex, Fixed
        Depth, Progress, OnMainPath
        IsLeaf, IsHub, Openness, Difficulty
    }
    class Connection {
        A, B, Kind, Width
        InTree, IsLoop, IsRepair, OnMainPath
        Cells, DoorA, DoorB, Failed
    }
    class VerticalLink {
        Kind : Stairs or Drop
        UpperFloor, LowerFloor
        Footprint, landings, OneWay
    }
    class Placement {
        Kind, Table, Entry
        Floor, Area, Cell, Height, Yaw
        Group, Scale, Tier
    }
    DungeonLayout "1" --> "*" FloorLayout
    DungeonLayout "1" --> "*" VerticalLink
    DungeonLayout "1" --> "*" Placement
    FloorLayout "1" --> "1" TileGrid
    FloorLayout "1" --> "*" Area
    FloorLayout "1" --> "*" Connection
```

**Cell types** (`CellType`): `Solid` (rock), `Floor`, `Door`, `Link` (stair well or drop shaft).

**Cell flags** (`CellFlags`, several per cell): `Organic` (cave), `Reserved` (keep solid), `NoCeiling` (under a
drop), `Corridor`, `Prefab`, `Secret`, `Chokepoint`, `MainPath`, `Landing`, `Pillar`, `Rubble`, `Occupied`, `Pit`.

**Grid coordinates:** index = `x + y · Width`. +x is east, +y is north (world +z). Cell centre = `(x + 0.5, y + 0.5) ×
CellSize`. Floor *f* sits at `BaseY = −f × FloorSpacing` under the dungeon root (floor 0 is the top floor). All floors
share the same grid size, so stairs line up across floors.

## 5. Determinism

| Source of randomness | How it is made deterministic |
|---|---|
| Dungeon seed | `request.seed`; a world portal derives it from the world seed and the portal's position (see [02](02-Request-Profile-Seed.md)) |
| Retry seeds | `Hash(seed, 0xA77E, attempt)` |
| Each stage / floor / area | its own stream: `DungeonRandom.Create(seed, Salt(purpose), a, b)` (SplitMix64). Changing one stage — say, loot — never moves the rooms |
| Parallel floors | each floor only writes its own data and uses its own stream, so thread timing cannot change the result |
| Searches (A*, Dijkstra, floods) | `BinaryHeap` returns equal priorities in push order |
| Mesh noise (rough cave walls) | position hashes `PlacementRandom.Value(seed, …, x, y)`, identical across mesh chunks and threads |

**Not deterministic by design:** a request with `seed = 0` picks a time-based seed. The respawn director uses
`System.Random`, because respawns are runtime gameplay.

## 6. Threads

```mermaid
flowchart TB
    subgraph Main["Main thread"]
        M1["Portal trigger, DungeonSession"]
        M2["CompiledProfile.Compile"]
        M3["DungeonBuilder coroutine<br/>(Frame Budget Ms per frame)"]
        M4["NavMeshSurface.UpdateNavMesh (starts async bake)"]
        M5["Runtime: streamer, respawns, portals"]
    end
    subgraph Workers["Dungeon Worker threads (1-4)"]
        W1["DungeonPipeline.Generate"]
        W2["ForEachFloor: floors in parallel"]
        W3["DungeonMeshing.BuildAll (floors in parallel)"]
    end
    subgraph Jobs["Unity job threads"]
        J1["MeshColliderBaker: collider cooking"]
        J2["NavMesh bake (Unity internal)"]
    end
    subgraph GPU["GPU"]
        G1["draws meshes with theme or flat materials"]
    end
    M2 --> W1 --> W2 --> W3 --> M3
    M3 --> J1 --> M3
    M3 --> M4 --> J2 --> M3
    M3 --> G1
```

`DungeonWorkers.Pool` is a `TerrainWorkerPool` with `clamp(cores − 1, 1, 4)` threads named "Dungeon Worker". It is
separate from the terrain's pool and is shut down when the application quits. Outside Play mode (editor preview,
tests) generation runs synchronously.

## 7. Master data flow and feedback loops

```mermaid
flowchart TD
    REQ["DungeonRequest<br/>seed, size, difficulty, depth"] --> MP["MacroPlan"]
    PROF["DungeonProfile + Theme + Tables<br/>→ CompiledProfile"] --> MP
    MP -- "FloorSpec: footprint, style,<br/>openness, complexity, difficulty<br/>Anchors + VerticalLinks" --> LY["Layout"]
    LY -- "Areas + cells" --> CN["Connectivity"]
    CN -- "Connections (kind, width, tree/loop)" --> RL["Roles"]
    RL -- "roles, progress, difficulty,<br/>templates, secret kinds" --> CV["Carve"]
    CV -- "carved grid, doors,<br/>floor/ceiling heights" --> VL["Validate"]
    VL -- "repaired, reachable grid" --> AN["Analysis"]
    AN -- "walking distance, wall distance,<br/>main-path cells, chokepoints, openness" --> PO["Population"]
    PO -- "Placements, poses" --> MSH["Meshing (worker)"]
    MSH -- "FloorMeshData" --> BLD["Builder (main)"]
    BLD --> INST["DungeonInstance"]

    MP -. "ctx.Fail → new attempt seed" .-> MP
    LY -. "Fail" .-> MP
    RL -. "required role unplaced → Fail" .-> MP
    VL -. "unreachable after repairs → Fail" .-> MP
    PO -. "no spawn → Fail" .-> MP
    VL -. "repair: route a new corridor,<br/>then redo doors, cells, heights" .-> VL
    RL -. "relax filters 1..4<br/>for required roles" .-> RL
    INST -. "NextDungeon portal:<br/>request.Next() (depth + 1)" .-> REQ
```

| Feedback loop | Trigger | Effect |
|---|---|---|
| **Attempt retry** | any `ctx.Fail` | whole dungeon regenerated with `Hash(seed, 0xA77E, attempt)`; after *Max Attempts* the manager raises `Failed` |
| **Role relaxation** | a required role (Boss) finds no matching area | filters dropped one by one: progress → placement → style → size |
| **Repair** | an area is unreachable after carving | `CorridorRouter.RouteToReached` carves a corridor from it to the reachable part (≤ 16 per floor), then openings, doors, cells and heights are recomputed |
| **Room shrinking** | room scatter fails 60 times | tries smaller rooms, gives up after 160 failures |
| **Build ordering** | NavMesh needs colliders; mobs need the NavMesh | colliders are assigned before the bake; mobs are spawned only after it finishes |
| **Deeper dungeons** | *Next Dungeon* exit portal | `request.Next()` → new seed, `depth + 1`; each depth adds +0.25 to the difficulty multiplier (`1 + 0.2·floor + 0.25·depth`) |
| **World portal closing** | player entered / completed | `WorldSpawnRegistry.ClosePortalSite` (terrain side) |
| **Respawn** | fewer living mobs than 75 % of the floor's original count | the respawn director reuses the generator's own mob placements |

## 8. Dependency graph

```mermaid
flowchart LR
    subgraph Config
        DP["DungeonProfile"]
        DT["DungeonTheme"]
        RT["RoomTemplate"]
        ET["Encounter / Loot / Prop tables"]
        DD["DungeonDefaults"]
        CPp["CompiledProfile"]
    end
    subgraph Common["Procedural/Common"]
        BH["BinaryHeap"]
        DF["DistanceField"]
        GB["GraphBuilders (Gabriel)"]
        GP["GridPathfinder (A*)"]
        SH["SpatialHash2D"]
        UF["UnionFind"]
    end
    subgraph Terrain["Procedural/World (shared)"]
        TWP["TerrainWorkerPool, WorkToken"]
        PR["PlacementRandom (hashes)"]
        MCB["MeshColliderBaker"]
        GS["GenerationStats"]
        WSR["WorldSpawnRegistry, LoadedTerrain"]
    end
    DP --> CPp
    DT --> CPp
    RT --> CPp
    ET --> CPp
    DD --> CPp
    CPp --> Stages["Pipeline stages"]
    GB --> Stages
    UF --> Stages
    GP --> Stages
    DF --> Stages
    SH --> Stages
    BH --> GP
    PR --> Stages
    TWP --> Stages
    GS --> Stages
    Stages --> Mesh["Presentation (mesher)"]
    Mesh --> Builder["Build (DungeonBuilder)"]
    MCB --> Builder
    Builder --> Runtime["Runtime (Session, Instance, Portals)"]
    WSR --> Runtime
    Portal["Essentials/Portal.cs"] --> Runtime
```

The dungeon **reuses** terrain infrastructure (`TerrainWorkerPool`, `PlacementRandom`, `MeshColliderBaker`,
`GenerationStats`) but never reads terrain *data*. The only world inputs are the world seed (via the portal) and the
portal's position. The dungeon is built far below the world (default origin y = −10 000), so the two never overlap.

## 9. Script map

| Folder | Scripts | Role |
|---|---|---|
| `Core/` | `DungeonEnums`, `DungeonElements` (Area, Connection, VerticalLink, Anchor, FloorSpec), `DungeonLayout` (FloorLayout, Placement, GenerationReport), `TileGrid`, `DungeonRandom`, `DungeonRequest` | the data model |
| `Config/` | `DungeonProfile`, `DungeonSettings` (all setting groups), `DungeonTheme`, `RoomTemplate`, `DungeonEncounterTable`, `DungeonLootTable`, `DungeonPropTable`, `DungeonDefaults`, `SpawnableMobImport`, `CompiledProfile` | authoring assets and their thread-safe compiled copy |
| `Pipeline/` | `DungeonPipeline` (IDungeonStage, DungeonContext, StageSlot), `DungeonStageAsset` | stage runner, retries, custom stages |
| `Stages/Planning/` | `MacroPlanStage` (+ AnchorPlanner) | stage 1 |
| `Stages/Layout/` | `LayoutStage` (+ AnchorAreas, LayoutUtil), `RoomScatterLayout`, `BspLayout`, `CaveLayout` (CaveField), `HybridLayout`, `GridMazeLayout` (+ TemplateStamper), `RoomShapes` | stage 2 |
| `Stages/Connect/` | `ConnectivityStage` | stage 3 |
| `Stages/Roles/` | `RolesStage` | stage 4 |
| `Stages/Carve/` | `CarveStage` (+ HeightPass), `CorridorRouter` | stage 5 |
| `Stages/Validate/` | `ValidateStage` | stage 6 |
| `Stages/Analysis/` | `AnalysisStage` | stage 7 |
| `Stages/Population/` | `PopulationStage` (FloorPopulator) | stage 8 |
| `Presentation/` | `DungeonMeshing`, `DungeonMesher`, `LinkMesher`, `MeshBuffers`, `TileKitPlanner` | mesh data (worker thread) |
| `Build/` | `DungeonBuilder`, `DungeonMaterials`, `DungeonObjectPool`, `DungeonPrimitives` | scene objects (main thread) |
| `Runtime/` | `DungeonManager` (+ DungeonWorkers), `DungeonSession` (+ DungeonWorldPause, DungeonAtmosphere), `DungeonInstance`, `DungeonPortal`, `DungeonFloorStreamer`, `DungeonRespawnDirector`, `DungeonHazard`, `DungeonSecretDoor`, `DungeonFlicker`, `DungeonSpawned` | play-time behaviour |
| `Editor/` | `DungeonPreviewWindow`, `DungeonPreviewTexture`, `DungeonInspectors`, `DungeonSetupMenu`, `PortalSetupMenu`, `PortalEditor`, `RangeDrawers`, `Tests/DungeonGenerationTests` | tools |
| `Procedural/Common/` | `BinaryHeap`, `DistanceField`, `GraphBuilders`, `GridPathfinder`, `SpatialHash2D`, `UnionFind` | shared algorithms |
| `Essentials/Portal.cs` | world portal | entry point from the open world |
| `RoomBehaviour.cs` | old room prefab script | kept for legacy room prefabs (used through a legacy `RoomTemplate`) |

## 10. Status legend

Used throughout, as in the Terrain docs: **Implemented** (works as described) · **Partially implemented** (works with
limits stated) · **Placeholder** (exists but does nothing meaningful yet) · **Not currently implemented** (a
suggestion, not in the code).
