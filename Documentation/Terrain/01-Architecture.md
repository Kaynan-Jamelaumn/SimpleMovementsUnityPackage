# Terrain 01 — System Architecture

> **Who this is for:** a developer who has never opened this terrain system. Read this chapter first; every later
> chapter zooms into one box of the diagrams below.
>
> **Source root:** `Scripts/Procedural/World/` (runtime) and `Scripts/CustomEditor/TerrainGenerator/` (editor).

---

## 1. What the system does, in one paragraph

The terrain system builds an **endless, streamed 3D landscape** around the player at runtime. Nothing is stored on
disk: every hill, river, tree and texture is **calculated from a single number, the world seed**
(`TerrainGenerator > Voronoi Seed`), plus the settings in the Inspector. The world is cut into square **chunks**
(240 × 240 world units by default). Chunks near the player are generated on background threads, turned into Unity
meshes, textured, given colliders, water, trees and rocks, a navigation mesh, and finally portals and mobs. Chunks that
fall far behind are destroyed; when the player comes back they are regenerated **identically**, because every step is
deterministic.

## 2. The two central components

| Component | Lives on | Responsibility |
|---|---|---|
| `TerrainGenerator` (4 partial files) | one scene GameObject | **Holds every rule of the world** (≈150 settings), owns the worker-thread pool, generates chunk data on request, applies finished work on the main thread within a time budget, creates placed objects a few per frame. |
| `EndlessTerrain` (+ nested `TerrainChunk`) | one scene GameObject | **Decides which chunks exist** around the viewer, creates/unloads them, and drives each chunk's lifecycle: mesh, collider, water, objects, LOD, NavMesh, spawners. |

Everything else is a static "generator" class that turns a **world position** into some piece of data. They are
pure functions of *(position, seed, settings)*, which is what makes the world seamless and repeatable.

---

## 3. High-level architecture

```mermaid
flowchart TB
    subgraph CFG["Configuration (Inspector / assets)"]
        TG["TerrainGenerator<br/>world rules + seed"]
        BIO["Biome assets<br/>(texture, noise, climate, landform,<br/>erosion, water, weather)"]
        BO["BiomeObject rules<br/>(per biome: trees, rocks...)"]
        ET["EndlessTerrain<br/>viewer, distances, portal/mob settings"]
    end

    subgraph SUP["Supporting systems (pure functions + caches)"]
        NOISE["Noise<br/>Perlin fBm, gradient noise,<br/>ridged, domain warp"]
        VOR["Voronoi biome layout<br/>VoronoiBiomeGenerator"]
        CLI["Climate<br/>ClimateGenerator + TerrainClimate"]
        SEED["Seed & hashing<br/>PlacementRandom, WaterGenerator.Hash"]
        CACHE["Global feature caches<br/>lakes, rivers, volcanoes,<br/>massifs, erosion tiles"]
    end

    subgraph GEN["Chunk generation (worker threads)"]
        HS["TerrainHeightSampler<br/>land + coast + volcano"]
        LF["Landforms<br/>LandformGenerator, MountainMassifs"]
        WAT["Water features<br/>ocean, lakes, ponds, rivers"]
        ERO["Erosion<br/>thermal + hydraulic, ErosionTiles"]
        HM["HeightGenerator<br/>final height map + water map"]
        MG["MeshGenerator<br/>terrain + water mesh data"]
        SPL["SplatMapGenerator<br/>biome texture weights"]
        OPE["ObjectPlacementEngine<br/>where objects go"]
    end

    subgraph MAIN["Main thread (time-budgeted)"]
        CH["TerrainChunk<br/>upload mesh, material, water"]
        COL["MeshColliderBaker<br/>(job thread cook)"]
        INST["PlacementInstantiator<br/>create/pool objects"]
        NAV["NavMeshSurface<br/>async build"]
        SPW["PortalSpawner / MobSpawner"]
        LT["LoadedTerrain + FlatSpots<br/>queries"]
    end

    subgraph GPU["GPU"]
        TSH["SimpleMovements/Terrain<br/>tri-planar splat shader"]
        WSH["SimpleMovements/Water<br/>waves, flow, foam"]
    end

    subgraph RT["Runtime layers"]
        WEA["WeatherSystem<br/>rain, snow, storms"]
        FAR["FarObjectSwitcher"]
    end

    subgraph ED["Editor tools"]
        PREV["World Preview"]
        STATS["Generation Stats window"]
    end

    TG --> HS
    BIO --> VOR
    BO --> OPE
    ET --> CH
    NOISE --> LF
    NOISE --> VOR
    NOISE --> CLI
    CLI --> VOR
    VOR --> HS
    LF --> HS
    SEED --> VOR
    SEED --> WAT
    SEED --> OPE
    CACHE --- WAT
    CACHE --- ERO
    HS --> WAT
    HS --> HM
    WAT --> HM
    ERO --> HM
    HM --> MG
    HM --> SPL
    HM --> OPE
    MG --> CH
    SPL --> CH
    CH --> COL
    CH --> LT
    OPE --> INST
    INST --> NAV
    NAV --> SPW
    LT --> SPW
    CH --> TSH
    CH --> WSH
    WEA -->|"shader globals:<br/>wetness, snow"| TSH
    INST --> FAR
    HS -.-> PREV
    WAT -.-> PREV
```

**How to read it:** configuration feeds pure "supporting" functions; worker threads combine them into a chunk's data;
the main thread turns that data into Unity objects; the GPU draws them. The dotted lines show that the **World
Preview** reuses the exact same samplers (so what you see in the editor is what the game builds, before erosion).

---

## 4. End-to-end flow: from pressing Play to a rendered chunk

```mermaid
flowchart TD
    A["Play pressed"] --> B["TerrainGenerator.Awake<br/>clear static caches<br/>(Voronoi, water, massifs, erosion tiles, placement)"]
    B --> C["EndlessTerrain.Start<br/>find TerrainGenerator, chunk span = ChunkSize - 1"]
    C --> D["EndlessTerrain.Update (every frame)<br/>viewerPosition = viewer x,z"]
    D --> E{"Chunk coord within<br/>create distance and<br/>not loaded?"}
    E -- yes --> F["new TerrainChunk(coord)<br/>GameObject + MeshRenderer, MeshFilter,<br/>MeshCollider, NavMeshSurface, spawners"]
    F --> G{"Data in the<br/>chunk data cache?"}
    G -- yes --> K
    G -- no --> H["TerrainGenerator.RequestChunkData<br/>queued on worker pool,<br/>priority = distance to viewer"]
    H --> I["Worker: HeightGenerator.GenerateHeightMap<br/>biomes, landforms, coast, volcanoes,<br/>water carving, erosion, water map,<br/>placement fields"]
    I --> J["Worker: BuildTerrainData<br/>terrain mesh + normals, biome map,<br/>splat pixels, water mesh"]
    J --> K["Main-thread queue<br/>(Main Thread Budget ms per frame)"]
    K --> L["TerrainChunk.OnTerrainDataReceived<br/>material + splat/wetness textures,<br/>mesh upload, water object,<br/>schedule collider bake,<br/>LoadedTerrain.Register"]
    L --> M["Worker: ObjectPlacementEngine.Place<br/>(or cached placements)"]
    M --> N["PlacementInstantiator<br/>create/pool objects<br/>(Spawn Budget ms per frame)"]
    N --> O["FarObjectSwitcher.Track"]
    L --> P["Every frame: UpdateTerrainChunk<br/>visibility, distance LOD,<br/>collider assign when cooked"]
    O --> Q{"Within NavMesh Distance<br/>and visible?"}
    Q -- yes --> R["NavMeshSurface.UpdateNavMesh (async)"]
    R --> S["StartSpawners:<br/>PortalSpawner.Begin, MobSpawner.Begin"]
    Q -- "Bake NavMesh off" --> S
    E -- "beyond unload distance" --> U["UnloadChunk: spawners End,<br/>cancel work, free meshes/textures/NavMesh,<br/>pool objects, cache data"]
```

The same chunk therefore passes through **four execution contexts**: a worker thread (heights, meshes, textures'
pixels), the main thread (Unity objects), a Unity job thread (collider cooking) and Unity's async NavMesh builder.

---

## 5. What runs before what (and why)

The order inside one chunk's height calculation is **not** "height → climate → biome", which is what many tutorials
use. In this project it is:

```mermaid
flowchart LR
    S["Seed + settings"] --> C["Climate fields<br/>(temperature, moisture)<br/>+ terrain climate:<br/>mountain belts, continents"]
    C --> V["Voronoi points get<br/>their biome labels"]
    V --> W["Biome blend weights<br/>at this position"]
    W --> L["Landform relief +<br/>base elevation"]
    L --> CO["Coast / ocean shaping"]
    CO --> VO["Volcanoes"]
    VO --> WF["Water features traced<br/>on this base terrain"]
    WF --> CA["Pre-erosion carving<br/>(lake bowls, river valleys)"]
    CA --> E["Erosion"]
    E --> G["Post-erosion water<br/>guarantees"]
    G --> F["Final heights + water map"]
```

**Why climate comes first:** biomes are *chosen* from the climate, and heights are *computed* from the biomes. If
climate depended on the final heights, and heights on biomes, the system would be circular. `TerrainClimate` breaks the
circle by reading only two fields that do not depend on which biome is where: the **mountain-belt field** (where
mountain biomes are *encouraged* to go) and the **continent field** (where oceans are). See
[Climate](07-Climate.md).

**Why water features are traced before erosion but enforced after it:** lakes and rivers are decided on the
deterministic *base* terrain (identical for every chunk), carved into it, then weathered by erosion so they look
natural, and finally their rims, banks and channels are **re-enforced** so erosion can never open a leak.

### Systems that run independently

| System | Independent of | Notes |
|---|---|---|
| `WeatherSystem` | chunk generation | Samples climate and biomes at the viewer at runtime; only talks back to the terrain through shader globals. Paused while inside a dungeon. |
| World Preview | Play mode | Uses `TerrainHeightSampler`, `LakeGenerator`, `RiverGenerator` directly in the editor. |
| `PortalSitePlanner` | chunk data | Portal *sites* are known for any area before its chunks exist; only the exact spot needs the chunk. |
| Landmark spots | chunk data | `ObjectPlacementEngine.FindLandmarkSpots` works from the coarse world shape. |
| Dungeons | the terrain | Generated far below the world; the terrain is paused while you are inside (see the Dungeon docs). |

---

## 6. Master data flow (all systems, with real feedback loops)

```mermaid
flowchart TD
    SEED["Seed (VoronoiSeed)"] --> WC["World coordinates (x, z)"]
    WC --> NOISE["Noise fields"]
    NOISE --> CLIM["Climate<br/>temperature, moisture"]
    NOISE --> BELT["Mountain-belt field"]
    NOISE --> CONT["Continent field (LandSide)"]
    BELT --> TC["TerrainClimate<br/>rain shadow, cooling"]
    CONT --> TC
    TC --> CLIM
    CLIM --> BIOME["Biome layout (Voronoi)"]
    BELT --> BIOME
    BIOME --> HEIGHT["Height generator<br/>landforms, massifs"]
    CONT --> COAST["Coast & ocean floor"]
    HEIGHT --> COAST
    COAST --> VOLC["Volcanoes"]
    VOLC --> BASE["Base terrain"]
    BASE --> HYDRO["Hydrology<br/>lakes, ponds, rivers, waterfalls"]
    HYDRO --> CARVE["Carving"]
    BASE --> CARVE
    CLIM --> RAIN["Rainfall & resistance maps"]
    BIOME --> RAIN
    CARVE --> ERO["Erosion"]
    RAIN --> ERO
    ERO --> GUAR["Water guarantees"]
    HYDRO --> GUAR
    GUAR --> FINAL["Final terrain data<br/>heights, water map, wetness"]
    FINAL --> MESH["Mesh generation<br/>(+ skirts, LOD)"]
    BIOME --> SPLAT["Splat weights"]
    FINAL --> WMESH["Water mesh"]
    MESH --> COLL["Mesh collider"]
    FINAL --> PLACE["Object placement"]
    BIOME --> PLACE
    CLIM --> PLACE
    SPLAT --> SHADER["Terrain shader"]
    FINAL -->|"wetness map"| SHADER
    WMESH --> WSHADER["Water shader"]
    PLACE --> OBJ["Objects (pooled)"]
    OBJ --> NAVM["NavMesh"]
    COLL --> NAVM
    NAVM --> SPAWN["Portals & mobs"]
    MESH --> LOD["LOD / streaming"]
    LOD --> RENDER["Rendering"]
    SHADER --> RENDER
    WSHADER --> RENDER
    OBJ --> RENDER
    CLIM -.->|"runtime"| WEATHER["Weather"]
    BIOME -.->|"runtime"| WEATHER
    WEATHER -.->|"feedback: wet ground, snow<br/>(shader globals only)"| SHADER
```

**Feedback relationships that really exist** (and the ones that do not):

| Relationship | Exists? | Where |
|---|---|---|
| Terrain shape → climate | **Yes, indirectly** — via the mountain-belt and continent fields, never via actual heights | `TerrainClimate.Shifts` |
| Climate → biome placement | Yes | `VoronoiBiomeGenerator.ComputeBaseWeights` |
| Climate → erosion strength | Yes (rainfall map) | `HeightGenerator.BuildBaseHeights` |
| Biomes → erosion resistance | Yes | `Biome.erosionResistance` |
| Water features → terrain | Yes (carving + guarantees) | `ChunkWaterContext` |
| Erosion → water placement | **No** — lakes/rivers are traced on pre-erosion base terrain | by design, for determinism |
| Weather → terrain shape | **No** | weather is visual/gameplay only |
| Weather → shader (wetness, snow) | Yes | `WeatherSystem` sets `_SMWeatherWetness`, `_SMWeatherSnow` |
| Water → object placement | Yes (water rules, wetness) | `PlacementEnvironment` |

---

## 7. Dependency graph (only real dependencies)

```mermaid
flowchart LR
    Noise --> Climate
    Noise --> Landforms
    Noise --> Voronoi
    Climate --> Voronoi
    Voronoi --> Biome["Biome blend"]
    Biome --> Height
    Landforms --> Height
    Height --> Coast
    Coast --> Volcanoes
    Volcanoes --> BaseTerrain["Base terrain"]
    BaseTerrain --> Lakes
    BaseTerrain --> Rivers
    Lakes --> Rivers
    Rivers --> Lakes
    Lakes --> Carving
    Rivers --> Carving
    Carving --> Erosion
    Climate --> Erosion
    Biome --> Erosion
    Erosion --> FinalHeights["Final heights"]
    FinalHeights --> WaterMap["Water map"]
    FinalHeights --> Mesh
    Mesh --> Collider
    Mesh --> LODMesh["LOD meshes"]
    Biome --> Splat["Splat maps"]
    Splat --> Material
    WaterMap --> Material
    WaterMap --> WaterMesh["Water mesh"]
    FinalHeights --> Placement["Object placement"]
    Biome --> Placement
    WaterMap --> Placement
    Climate --> Placement
    Placement --> Objects
    Objects --> NavMesh
    Collider --> NavMesh
    NavMesh --> Spawners["Portal/Mob spawners"]
    Climate --> Weather
    Biome --> Weather
    Weather --> Material
```

`Lakes ⇄ Rivers` is a genuine two-way dependency: a river ends when it reaches a lake, a lake's outlet starts a river,
and a lake's final water level is lowered if another river cuts through its rim (`RiverGenerator.ComputeLakeWaterLevel`).
It is resolved lazily and cached, so it cannot loop.

---

## 8. Script map

| Folder | Key classes | Chapter |
|---|---|---|
| `World/` | `TerrainGenerator` (+`.Generation`, `.Properties`, `.Threading`), `EndlessTerrain` (+`.TerrainChunk`), `DataStructure`, `TerrainMonitor` | [02](02-World-Chunks-Streaming.md), [16](16-Performance-and-Threading.md) |
| `World/Threading/` | `TerrainWorkerPool`, `WorkToken` | [16](16-Performance-and-Threading.md) |
| `World/Terrain/` | `HeightGenerator`, `TerrainHeightSampler`, `ErosionGenerator`, `ErosionTiles` | [04](04-Height-and-Landforms.md), [10](10-Erosion.md) |
| `World/LandForms/` | `LandformGenerator` (5 partials), `MountainMassifs`, `LandformSettings`, `Volcanoes/*` | [04](04-Height-and-Landforms.md), [05](05-Volcanoes.md) |
| `World/Biome/` | `Biome`, `BiomeInstance`, `BiomeObject`, `Layout/VoronoiBiomeGenerator` (5 partials), `ClimateGenerator`, `TerrainClimate` | [06](06-Voronoi.md), [07](07-Climate.md), [08](08-Biomes.md) |
| `World/Water/` | `WaterGenerator`, `WaterSettings`, `ChunkWaterContext`, `WaterMapData`, `Oceans/*`, `Lakes/*`, `Rivers/*`, `WaterVolume` | [09](09-Water.md) |
| `World/Mesh/` | `MeshGenerator` (+`.Water`), `MeshData`, `WaterMeshData`, `MeshColliderBaker` | [11](11-Mesh-and-Collider.md) |
| `World/Texturing/` | `SplatMapGenerator`, `SplatBlendData`, `TextureGenerator`, `Resources/SimpleMovementsTerrain.shader/.hlsl` | [12](12-Texturing-and-Shaders.md) |
| `World/Weather/` | `WeatherSystem` (+`.Effects`, `.Storms`), `WeatherModel`, `WeatherTypes` | [13](13-Weather.md) |
| `World/Placement/` | `ObjectPlacementEngine` (+`.Site`, `.Landmarks`), `PlacementPlan`, `PlacementRules`, `PlacementInstantiator`, `FarObjectSwitcher`, … | [14](14-Object-Placement.md) |
| `World/Spawnable/` | `PortalSpawner`, `PortalSitePlanner`, `MobSpawner`, `ChunkSpawnerBase`, `SpawnGround`, `WorldSpawnRegistry` | [15](15-Portals-and-Mobs.md) |
| `World/Queries/` | `LoadedTerrain`, `FlatSpots` | [14](14-Object-Placement.md), [15](15-Portals-and-Mobs.md) |
| `World/Diagnostics/` | `GenerationStats` | [16](16-Performance-and-Threading.md), [17](17-Editor-Tools.md) |
| `CustomEditor/TerrainGenerator/` | `TerrainGeneratorEditor` (≈20 partials, World Preview), `GenerationStatsWindow`, `WeatherSystemEditor` | [17](17-Editor-Tools.md) |

**Legacy / unused code** (kept by the project, not part of the pipeline): `Procedural/LEGACY/**`,
`LEGACY_VoronoiGeneratior.cs` (commented out), `Spawnable/ObjectSpawner.cs` (old per-cell placer, not called),
`QuadTree.cs` (not referenced), the hidden `TerrainGenerator.splatMapShader` compute-shader field.

---

## 9. The implementation-status legend used in these docs

| Label | Meaning |
|---|---|
| **Implemented** | Used by the running pipeline. |
| **Partially implemented** | Exists, but only some paths use it, or a setting has no effect with the package shader. |
| **Placeholder** | A field or class kept for compatibility; nothing reads it. |
| **Not currently implemented** | Asked about in design discussions but does not exist; see *Potential Future Improvements* in [20](20-Glossary-and-Future.md). |

Next: [02 — World, Chunks, Streaming and LOD](02-World-Chunks-Streaming.md)
