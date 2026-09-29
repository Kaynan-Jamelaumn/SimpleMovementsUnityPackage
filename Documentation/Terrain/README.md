# Terrain Generation — Technical Reference

A complete onboarding and reference guide to the procedural world in `Scripts/Procedural/World/`. Written for a
developer who has never seen the system: every chapter goes **Concept → Implementation → Data flow → Configuration →
Example → Performance → Debugging**, with flowcharts (Mermaid) that mirror the actual code.

## Chapters

| # | Chapter | Covers |
|---|---|---|
| 01 | [Architecture](01-Architecture.md) | the big picture, end-to-end flow, run order, master data flow, dependency graph, script map |
| 02 | [World, Chunks, Streaming and LOD](02-World-Chunks-Streaming.md) | coordinates, determinism, chunk lifecycle, caching, seams, LOD, skirts |
| 03 | [Noise](03-Noise.md) | noise concepts, every noise function used, how a coordinate becomes a height |
| 04 | [Height Generator and Landforms](04-Height-and-Landforms.md) | height pipeline, combination operations, 12 landforms, mountain massifs, belts, transitions |
| 05 | [Volcanoes and Calderas](05-Volcanoes.md) | rarity, placement, cone/caldera profiles, blending, volcanic biome |
| 06 | [Voronoi Biome Layout](06-Voronoi.md) | sites, biome assignment, order independence, blend weights, slope-safe borders |
| 07 | [Climate](07-Climate.md) | temperature, moisture, rain shadow, cooling, coasts; climate → biome |
| 08 | [Biomes](08-Biomes.md) | biome fields, selection flowchart, blending, preset catalogue |
| 09 | [Water](09-Water.md) | oceans, coasts, lakes, ponds, rivers, junctions, waterfalls, water map, water mesh, leak prevention |
| 10 | [Erosion](10-Erosion.md) | thermal and hydraulic erosion step by step, seamless tiles |
| 11 | [Mesh and Collider](11-Mesh-and-Collider.md) | heightmap → mesh, normals, UVs, resolution, collider cooking, the pre-baked collision warning |
| 12 | [Texturing, Tri-Planar and Shaders](12-Texturing-and-Shaders.md) | splat maps, texture arrays, tri-planar, the terrain shader, wetness, snow, variation |
| 13 | [Weather](13-Weather.md) | weather model, effects, interactions |
| 14 | [Object Placement](14-Object-Placement.md) | rules, pipeline, determinism across chunks, landmarks, pooling, far objects |
| 15 | [World Portals and Mobs](15-Portals-and-Mobs.md) | portal site planning and resolution, mob populations, registry |
| 16 | [Performance and Threading](16-Performance-and-Threading.md) | threads, jobs, budgets, costs, memory |
| 17 | [World Preview and Editor Tools](17-Editor-Tools.md) | inspector, preview, stats window, tools |
| 18 | [Configuration Reference](18-Configuration-Reference.md) | every setting with ranges, effects, performance and status |
| 19 | [Debugging Guide](19-Debugging.md) | symptoms → causes → fixes |
| 20 | [Glossary, Status and Future](20-Glossary-and-Future.md) | glossary, implementation status, potential improvements |

## Find the answer to…

| Question | Read |
|---|---|
| How does the world generate? | 01 §4, 02 §4 |
| How does a height value get calculated? | 03 §3, 04 §3 |
| How does a heightmap become a mesh? | 11 §2 |
| How are mountains and other landforms generated? | 04 §5–7 |
| How are biomes selected? | 06 §4–5, 08 §3 |
| How does climate work? | 07 |
| How are rivers, lakes, oceans and waterfalls generated? | 09 §3–6 |
| How does erosion modify terrain? | 10 |
| How are textures selected and blended? | 12 Part A |
| How does tri-planar mapping work? | 12 Part B |
| How do the terrain shaders work? | 12 Part C |
| How are objects placed? | 14 |
| How are chunks generated and streamed? | 02 |
| How does LOD work? | 02 §6, 11 §5 |
| What runs on the CPU, worker threads and GPU? | 16 §1 |
| What does every configuration variable control? | 18 |
| How can the terrain be tuned without breaking other systems? | 01 §5–7 (dependencies), 17 §2.6, 18 |
| How can common problems be diagnosed? | 19 |

Diagrams are Mermaid code blocks: they render on GitHub, in VS Code (Markdown Preview Mermaid Support), in many
Markdown viewers, and in the documentation page that accompanies this package.
