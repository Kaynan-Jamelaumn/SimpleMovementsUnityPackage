# Terrain 20 — Glossary, Implementation Status and Potential Future Improvements

## 1. Technical glossary

| Term | Meaning (in this project) |
|---|---|
| **Procedural generation** | creating content by algorithm from rules and a seed instead of by hand |
| **Seed** | the number every random decision derives from (`TerrainGenerator > Voronoi Seed`) |
| **Deterministic generation** | the same seed and settings always produce the same result, whatever the order or thread |
| **Hash** | a function turning numbers (seed, cell, stream) into a well-mixed pseudo-random number without state |
| **Noise** | a smooth pseudo-random function of position (nearby points similar, distant points unrelated) |
| **Perlin noise** | gradient noise on a grid: random gradient directions at corners, blended smoothly |
| **Simplex noise** | a cheaper, less axis-aligned relative of Perlin noise on a triangular grid (**not used** in this project) |
| **Value noise** | random *values* at grid corners, interpolated (used by placement density and the shader) |
| **Octave** | one layer of noise in a fractal sum |
| **FBM (fractal Brownian motion)** | the sum of several octaves with rising frequency and falling amplitude |
| **Frequency / wavelength** | how often features repeat / their typical size (1 / frequency) |
| **Amplitude** | how tall a noise layer is |
| **Lacunarity** | frequency multiplier between octaves (≈ 2) |
| **Persistence** | amplitude multiplier between octaves (≈ 0.5) |
| **Ridged noise** | `1 − |noise|` — sharp crests where noise crosses zero |
| **Domain warping** | sampling a function at a position displaced by another noise field → bent, organic shapes |
| **Mask** | a 0–1 field multiplying another (limits where a feature appears) |
| **Smoothstep / falloff** | S-shaped 0→1 (or 1→0) curve with zero slope at both ends |
| **Heightmap** | a grid of heights (here `float[ChunkSize+1, ChunkSize+1]` per chunk) |
| **Voronoi diagram / cell** | division of the plane into regions nearest to each of a set of points |
| **Distance field / distance transform** | a grid storing the distance to the nearest feature (mountain territory edge, water) |
| **Biome** | an environment type (texture, climate niche, landform, water, weather, objects) |
| **Landform** | the shape of the ground a biome sits on (Hills, Mountains, Dunes…) |
| **Massif** | a mountain mass built from the distance into a mountain territory |
| **Climate** | static temperature and moisture fields (0–1) |
| **Rain shadow** | dry area downwind of a mountain range |
| **Hydrology** | water systems: oceans, lakes, rivers, waterfalls |
| **Endorheic lake** | a terminal lake with no outlet (a river trapped in a basin) |
| **Priority flood** | a basin-filling algorithm that always expands the lowest reachable cell — finds spill points |
| **Erosion** | simulated weathering: thermal (slope collapse) and hydraulic (water droplets) |
| **Talus angle** | the steepest slope loose material stays on |
| **Chunk** | a square piece of the world generated, shown and unloaded as a unit (240 × 240 u by default) |
| **Streaming** | creating/destroying chunks around the viewer as it moves |
| **LOD (level of detail)** | coarser versions of a mesh for distant chunks |
| **Skirt** | vertical strip under a chunk edge hiding cracks between different LODs |
| **Mesh** | vertices + triangles describing a surface for the GPU |
| **Vertex** | a mesh point (position, normal, UVs, colour) |
| **Triangle** | three vertex indices; the drawable unit |
| **Normal** | direction a surface faces (lighting, slope) |
| **Tangent** | surface direction used by normal maps (not used by the terrain shader) |
| **UV** | texture coordinates on a vertex |
| **Mesh collider** | a triangle-mesh physics collider |
| **Cooking / pre-baking** | building PhysX's collision acceleration data for a mesh |
| **Shader** | a program running on the GPU that computes positions (vertex) and colours (fragment) |
| **Vertex shader** | the shader stage that transforms each vertex to the screen |
| **Fragment (pixel) shader** | the stage computing each pixel's colour |
| **Texture** | an image sampled by shaders |
| **Texture array** | a stack of same-sized textures addressed by index (`Texture2DArray`) |
| **Splat map** | a texture whose channels are texture *weights* |
| **Material** | a shader plus its property values and textures |
| **Tri-planar mapping** | texturing from three world-axis projections blended by the normal |
| **Mipmap** | pre-shrunk copies of a texture for distant/angled sampling |
| **PBR** | physically based rendering (albedo, smoothness, metallic…) |
| **CPU / GPU** | general processor / graphics processor |
| **Main thread** | Unity's thread, the only one allowed to use most Unity APIs |
| **Thread / worker thread** | a parallel execution path; here the terrain's worker pool |
| **Job** | a unit of work run by Unity's job system on its worker threads |
| **Burst** | Unity's compiler for high-performance jobs (**not used** by the terrain) |
| **Coroutine** | a Unity method that pauses across frames (**not used** by the terrain pipeline) |
| **Async** | work that completes later without blocking (NavMesh build, `async/await` not used) |
| **NavMesh** | the walkable-surface data AI agents move on |
| **Pooling** | keeping destroyed objects inactive for reuse |
| **Work token** | a cancellation flag for a chunk's pending work |

## 2. Implementation status summary

| Area | Implemented | Partially implemented | Placeholder | Not currently implemented |
|---|---|---|---|---|
| Streaming | chunks, cache, LOD, skirts, cancellation, priorities | — | Scale Factor (fixed 1) | — |
| Noise | Perlin fBm, gradient noise, ridged, warp, value noise | — | — | simplex noise |
| Height | Classic, 12 landforms, massifs, belts, transitions, coasts, volcanoes | — | — | runtime terrain editing |
| Biomes | Voronoi layout, climate placement, clustering, blending, ocean/volcanic roles | height band (objects only) | `BiomeInstance.currentNumberOfObjects` | altitude/slope-selected biomes |
| Climate | noise fields, rain shadow, cooling, coastal moisture | — | — | seasons, dynamic climate |
| Water | oceans, coasts, sea stacks, islands, lakes, ponds, rivers, junctions, waterfalls, wetness, swim triggers | swim trigger is a per-chunk box | — | dynamic water levels, flowing simulation |
| Erosion | thermal, hydraulic, seamless tiles | — | — | GPU erosion |
| Texturing | biome splat (≤ 4/pixel, ≤ 16 biomes), tri-planar, wetness, snow | Texture Variations (UV0 + extra textures), height-based texturing (no material) | Default Texture, compute splat shader | normal/roughness maps, rock-on-slope textures |
| Weather | full model, effects, ground wetness/snow | — | — | weather affecting water/erosion |
| Objects | rule-based placement, landmarks, pooling, far switching | — | old counters, cluster frequency/amplitude | roads/villages, GPU instanced grass |
| Tools | World Preview, stats, validation, presets, debug views | — | — | — |

## 3. Potential Future Improvements

These do **not** exist today; they are suggestions that would fit the architecture.

1. **Stable Classic-noise phase:** replace `biome.name.GetHashCode()` in `HeightGenerator.ClassicPhaseOffset` with
   `LandformGenerator.StableNameHash` (FNV-1a) — guarantees identical Classic terrain on every runtime. (Changes existing
   Classic terrain once.)
2. **Slope/height-based texture layers:** optional per-biome "cliff" and "high altitude" textures blended in the shader
   by slope and height (the shader already computes slope for tri-planar).
3. **Normal and roughness maps** per biome in parallel texture arrays.
4. **Use texture variations in the package shader** (pick a variation layer per region with a hash).
5. **More than 16 textured biomes:** loop over more splat maps in the shader (or a biome-index texture approach).
6. **Height-based texturing in the streaming pipeline** (or remove the option).
7. **Burst/Jobs port** of height, erosion and mesh generation (large speed-up; needs native containers).
8. **GPU erosion** via compute shaders.
9. **GPU-instanced detail layer** for dense grass/flowers instead of GameObjects.
10. **Seasons:** a time-varying temperature offset feeding weather and snow line.
11. **Weather → water:** rivers/lakes rising after rain (visual level offset).
12. **Exact swim volumes:** per-water-body mesh or per-cell queries instead of a per-chunk box.
13. **Collider LOD:** coarser colliders for far chunks where no physics happens.
14. **Road and settlement generation** that registers `PlacementFeatures` automatically.
15. **Preview parity:** an option to include erosion and water carving in the World Preview for small areas.
