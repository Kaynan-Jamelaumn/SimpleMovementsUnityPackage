# Terrain 18 — Configuration Reference

Every setting that exists in the code, grouped as in the inspectors. **Status**: **Impl.** = implemented and used;
**Partial** = only some paths use it (explained); **Placeholder** = kept for compatibility, nothing reads it.
Settings that do not exist are listed at the end under *Not currently implemented*.

Performance impact: — none · low · med · **high**.

---

## 1. World and chunk settings

### TerrainGenerator › Terrain Configuration / Other / LOD

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Terrain Size | enum 61/121/181/241 | chunk resolution (span = size − 1) | 241 | bigger chunks, fewer objects | more chunks | per chunk ∝ size² | Impl. |
| Scale Factor | float (hidden) | world units per cell | 1 | — | — | — | Placeholder (fixed 1) |
| Level Of Detail | int 0–6 | base mesh step (0 = every cell … 6 = every 12th) | 2 (code default 6) | coarser mesh, collider, placement grid | finer | **high** | Impl. |
| Distance LOD | bool | coarser meshes far away | on | — | — | saves GPU | Impl. |
| LOD Full Detail Distance | float | full base LOD within | 300 | detail farther | coarser sooner | med | Impl. |
| LOD Distance Step | float | one level coarser per | 300 | slower falloff | faster | med | Impl. |
| LOD Max Level | int 0–6 | coarsest level | 4 | coarser far chunks | — | — | Impl. |
| LOD Skirt Depth | float | extra skirt depth | 2 | hides cracks | — | — | Impl. |

### EndlessTerrain

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Viewer | Transform | what chunks follow (required) | player | — | — | — | Impl. |
| Max View Dst | float | visible radius | 250–600 | see farther | — | **high** (∝ d²) | Impl. |
| Should Have Max Chunk Per Side / Max Chunks Per Side | bool/int | hard cap on the grid | on / 5 | allows larger views | caps memory | high | Impl. |
| Unload Distance | float | destroy beyond (0 = view + chunk) | 0 | fewer regenerations | less memory | med | Impl. |
| Chunk Data Cache Size | int 0–256 | unloaded chunks kept | 16–64 | faster returns | less memory | memory | Impl. |
| Bake NavMesh | bool | chunk NavMeshes (mobs, portals) | on | — | no mobs | high (async) | Impl. |
| NavMesh Distance | float | NavMesh + spawners within | 250 | mobs/portals farther | cheaper | high | Impl. |
| Should Use HDRP Shaders | bool | HDRP project shaders | pipeline | — | — | — | Impl. |
| Portal Settings / Mob Settings | classes | see §11 | — | — | — | — | Impl. |
| Enable Debugging | bool | console logs | off | — | — | low | Impl. |

## 2. Noise settings

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Octaves | int | Classic fBm octaves; also sizes slope-safe borders | 4–6 | finer detail, wider borders | smoother | low–med | Impl. |
| Lacunarity | float | frequency ratio between octaves | 2 | detail sizes spread | muddier | — | Impl. |
| Min Height / Max Height | float | tracked extremes for height-based texturing | auto | — | — | lock per cell | Partial (only with Voronoi texturing off) |

## 3. Height / landform / mountain settings

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Terrain Shape Mode | enum | Classic Only / Per Biome / Landforms Only | Per Biome | — | — | Classic cheapest | Impl. |
| Relief Transition Width | 0.05–1 | landform relief fade at borders (× site spacing) | 0.35 | longer foothills | tall to the edge | — | Impl. |
| Mountain Belt Strength | 0–1 | mountains along long belts | 0.5 | long ranges | scattered | — | Impl. |
| Mountain Belt Scale Multiplier | float | belt spacing = Voronoi Scale × this | 6 | fewer, longer ranges | more | — | Impl. |
| Mountain Foothill Reach | float | foothills past the border | 40 | longer foothills | none | low | Impl. |
| Biome: amplitude / frequency / persistence / baseElevation / landform | per biome | see [Biomes](08-Biomes.md) | — | — | — | — | Impl. |

### Volcanoes

| Variable | Type | Purpose | Typical | Increase | Decrease | Perf | Status |
|---|---|---|---|---|---|---|---|
| Enable Volcanoes | bool | volcanoes and calderas | on | — | — | low | Impl. |
| Volcano Spacing | float | grid cell (≥ 500) | 5000 | rarer | commoner | — | Impl. |
| Volcano Chance | 0–1 | per cell | 0.35 | commoner | rarer | — | Impl. |
| Volcano Min / Max Radius | float | edifice radius (apron ≈ 1.9×) | 450 / 1000 | bigger | smaller | — | Impl. |
| Volcano Min / Max Height | float | summit/rim height | 110 / 230 | taller | lower | — | Impl. |
| Caldera Chance | 0–1 | caldera vs cone | 0.4 | more calderas | more cones | — | Impl. |

## 4. Voronoi / biome placement settings

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Num Voronoi Points | int | sites per Voronoi cell | 8 | smaller regions | bigger | low | Impl. |
| Voronoi Seed | int | **world seed** | any | — | — | — | Impl. |
| Voronoi Scale | float | Voronoi cell size; scales climate, warp, belts, continents | 350 | bigger regions | smaller | — | Impl. |
| Use Weighted Biome | bool | use biome.weight | on | — | — | — | Impl. |
| Biome Cluster Strength | 0–1 | neighbour bias | 0.4–0.6 | contiguous territories | scattered | — | Impl. |
| Biome Cluster Radius Multiplier | float | × Voronoi Scale | 1.5 | territories across more cells | local | low | Impl. |
| Biome Repeat Penalty | 0–1 | discourage disconnected repeats | 0.6 | fewer islands | more | — | Impl. |
| Use Natural Climate Placement | bool | climate-fit placement | on | — | random by weight | — | Impl. |
| Climate Scale Multiplier | float | climate scale = Voronoi Scale × this | 5–10 | larger zones | patchwork | — | Impl. |
| Voronoi Warp Strength | float | border wobble (world units) | 50 | wobblier | straighter | low | Impl. |
| Voronoi Warp Scale Multiplier | float | warp wavelength (× Voronoi Scale) | 1.5 (≥ 1) | longer wobble | jagged | — | Impl. |
| Height Blend Range (`biomeBlendRange`) | 0–1 | blend band (× Voronoi Scale) | 0.25 | wider transitions | sharper | — | Impl. |
| Use Biome Blended Texturing | bool | blend splat weights | on | — | hard edges | low | Impl. |
| Splat Textures Per Pixel | 2–4 | biomes mixed per pixel | 4 | smooth junctions | seams | GPU | Impl. |
| Boundary Max Walkable Slope | 0–89° | widen borders to stay walkable | 28 | steeper borders allowed | wider borders | — | Impl. |
| Order Independent Biome Layout | bool | layout independent of load order | on | — | — | low | Impl. |

## 5. Climate settings

| Variable | Type | Purpose | Typical | Increase | Decrease | Perf | Status |
|---|---|---|---|---|---|---|---|
| Terrain Aware Climate | bool | belts/continents shape climate | on | — | — | low | Impl. |
| Prevailing Wind Angle | 0–360° | wind blows toward (0 = +X) | any | rotates rain shadows | — | — | Impl. |
| Rain Shadow Strength | 0–1 | lee drying / windward wetting | 0.6 | stronger | weaker | — | Impl. |
| Altitude Cooling | 0–1 | colder belts/interiors | 0.5 | colder | milder | — | Impl. |
| Coastal Moisture | 0–1 | wet coasts, dry interiors | 0.4 | stronger | uniform | — | Impl. |
| Biome ideal T/M, tolerances | per biome | niche | — | — | — | — | Impl. |
| Seasons | — | — | — | — | — | — | **Not implemented** |

## 6. Water settings

| Variable | Type | Purpose | Typical | Increase | Decrease | Perf | Status |
|---|---|---|---|---|---|---|---|
| Enable Water | bool | master toggle | on | — | zero cost off | — | Impl. |
| Water Level | float | sea level (oceans only) | 0 | higher sea | — | — | Impl. |
| Water / Ocean / Lake / Pond / River / Waterfall Material | Material | per-type materials (fallbacks) | package shader | — | — | — | Impl. |
| Enable Swim Detection | bool | trigger box + `WaterVolume` | on | — | — | low | Impl. |
| Enable Oceans | bool | oceans | on | — | — | low | Impl. |
| Continent Scale Multiplier | float | continent size (× Voronoi Scale) | 18 | bigger continents | smaller | — | Impl. |
| Ocean Threshold | −0.8–0.8 | where the continent field becomes sea | −0.2 | more ocean | rarer | — | Impl. |
| Beach Width / Height | float | beach band | 30 / 2.5 | wider/higher | — | — | Impl. |
| Coast Blend Width | float | coast → inland blend | 140 | softer | abrupt | — | Impl. |
| Continental Shelf Width | float | shore → deep sea | 260 | gentler | steeper | — | Impl. |
| Ocean Depth | float | open-sea depth | 35 | deeper | shallower | — | Impl. |
| Inland Rise / Distance | float | land rising from the coast | 40 / 3000 | seaward drainage | flatter | — | Impl. |
| Island Frequency / Scale Mult. / Peak | 0–1 / float / float | islands | 0.3 / 1.4 / 14 | more/bigger/taller | fewer | — | Impl. |
| Spawn Land Radius | float | land around 0,0 | 700 | — | 0 = off | — | Impl. |
| Coast Cliff Frequency / Height / Terraces | 0–1 / float / 0–1 | cliff coasts | 0.35 / 26 / 0.5 | more/taller/tiered | beaches | — | Impl. |
| Sea Stack Chance / Spacing / Max Height | 0–1 / float / float | sea stacks | 0.3 / 220 / 30 | more | fewer | low | Impl. |
| Enable Lakes, Lake Spacing, Lake Chance | bool/float/0–1 | lakes | on, 900, 0.35 | sparser / more | — | low–med | Impl. |
| Lake Min/Max Radius, Max Depth, Max Site Slope, Outlet Chance | floats | lake shape/site | 45/130, 10, 0.3, 0.5 | bigger/deeper/more sites/more outlets | — | — | Impl. |
| Enable Ponds, Pond Spacing/Chance/Min/Max Radius/Depth/Max Site Slope | | ponds | on, 220, 0.25, 8/22, 2, 0.45 | | | low | Impl. |
| Shore Rim Width / Shore Freeboard | float | guaranteed shore band / height | 12 / 0.6 | safer shores | — | — | Impl. |
| Enable Rivers, River Spacing, River Chance | | rivers | on, 1000, 0.35 | fewer / more | | **high** (tracing) | Impl. |
| River Min Spring Elevation / Min Length / Max Length | float | spring rules, length | 10 / 350 / 2600 | fewer short rivers / longer rivers, larger search | | high | Impl. |
| River Source / Mouth Width, Width Variation | float | width taper | 5 / 26, 0.35 | wider | narrower | — | Impl. |
| River Meander / Meander Wavelength | 0–1 / float | meandering | 0.55 / 180 | twistier / longer bends | straighter | — | Impl. |
| River Depth / Valley Slope / Max Valley Width / Bank Freeboard | floats | channel/valley | 3 / 28° / 150 / 0.8 | deeper/steeper/wider/higher | | — | Impl. |
| Enable Waterfalls / River Junctions / Meander Cutoffs | bool | features | on | — | rapids / parallel / leaks | low | Impl. |
| Waterfall Min Drop / Tier Height / Min Slope | float | fall detection | 4 / 12 / 17° | fewer, taller | more | — | Impl. |
| Wetness Distance / Height | float | wet ground band | 14 / 4 | wider | narrower | — | Impl. |
| Snow Line Height / Snowmelt Springs | float / 0–3 | springs above snow line; snow caps (shader) | 90 / 1 | more springs | — | — | Impl. |

## 7. Erosion settings

| Variable | Type | Purpose | Typical | Increase | Decrease | Perf | Status |
|---|---|---|---|---|---|---|---|
| Enable Erosion | bool | thermal + hydraulic | on | — | — | **high** | Impl. |
| Erosion Padding | int | context cells | 40 (> lifetime) | safer seams | seams | area² | Impl. |
| Seamless Erosion | bool | shared tiles | on | — | — | med | Impl. |
| Thermal Iterations / Talus Angle / Thermal Erosion Rate | int / 1–89° / 0–1 | slope relaxation | 5 / 33 / 0.5 | smoother / steeper allowed / faster | | med | Impl. |
| Hydraulic Droplet Density | float | droplets per cell | 0.12 | more channels | 0 = off | **high** | Impl. |
| Droplet Lifetime | int | steps | 30 | longer channels | shorter | high | Impl. |
| Droplet Inertia | 0–1 | straightness | 0.05 | straighter | follows slope | — | Impl. |
| Sediment Capacity Factor / Min Sediment Capacity | float | carrying capacity | 4 / 0.01 | deeper carving | gentler | — | Impl. |
| Erode Speed / Deposit Speed | 0–1 | rates | 0.3 / 0.3 | faster change | slower | — | Impl. |
| Evaporate Speed | 0–1 | droplet death rate | 0.02 | shorter droplets | longer | — | Impl. |
| Erosion Gravity | float | acceleration | 4 | faster droplets | — | — | Impl. |
| Erosion Radius | 1–6 | brush radius | 3 | smoother channels | grooves | med | Impl. |
| Visualize Erosion Debug (+ min/max delta, stride, gizmo size, height offset, max gizmos) | debug | Scene gizmos | off | — | — | editor | Impl. |
| Biome erosionResistance / rainfallErosionMultiplier | per biome | | 0.5 / 1 | | | — | Impl. |

## 8. Weather settings (WeatherSystem)

| Variable | Purpose | Typical | Increase | Decrease | Perf | Status |
|---|---|---|---|---|---|---|
| Terrain Generator / Viewer / Sun | links (auto) | — | — | — | — | Impl. |
| System Size | weather system size | 2500 | wider, longer | patchier | — | Impl. |
| Drift Speed | drift with wind | 4 | faster changes | longer spells | — | Impl. |
| Weather Time Scale | time multiplier | 1 | faster | slower | — | Impl. |
| Precipitation / Windiness | global multipliers 0–2 | 1 | wetter/windier | drier/calmer | — | Impl. |
| Biome Influence | biome vs world climate | 0.75 | biome decides | world decides | — | Impl. |
| Climate Sample Radius | averaging radius | 60 | smoother borders | sharper | low | Impl. |
| Transition Seconds | blending time | 12 | slower | abrupt | — | Impl. |
| Allow Tornadoes, Start Time, Time Of Day | | on, 0, −1 | | | — | Impl. |
| Effect Radius / Max Particles | particles | 35 / 8000 | denser | cheaper | **high** (GPU/CPU) | Impl. |
| Fog Effects / Max Fog Density | fog | on / 0.03 | thicker | — | low | Impl. |
| Lighting Effects / Lightning Bolts / Max Lightning Per Minute | | on / on / 8 | | | low | Impl. |
| Wind Zone | drive a WindZone | on | — | — | low | Impl. |
| Ground Effects / Ground Effect Radius / Snow Caps | shader wetness & snow | on / 400 / on | | | — | Impl. |
| Tornado Force | push rigidbodies | 40 | stronger | 0 = off | low | Impl. |
| Rain/Snow/Dust/Lightning Material, audio loops, thunder clips, volume | optional assets | — | | | — | Impl. |
| Biome `weather` multipliers (12, 0–3) + presets | per biome | 1 | more often | less | — | Impl. |

## 9. Texture and shader settings

| Variable | Purpose | Typical | Increase | Decrease | Perf | Status |
|---|---|---|---|---|---|---|
| Texture Based On Voronoi Points | biome textures via splat maps | on | — | **off: no material** | — | Impl. (off = Partial/non-functional) |
| Default Texture, Splat Map Shader (compute) | legacy | — | — | — | — | Placeholder (hidden, unused) |
| Enable Texture Variations | master toggle | on | — | — | — | Impl. |
| Enable UV Rotation / Texture Scale Variation (+ range) | per-chunk UV0 rotation/scale | on / on, 0.3 | — | — | — | Partial (UV0 only; ignored by the package shader) |
| Enable UV Noise / Strength / Scale | UV offset noise | on / 0.3 / 0.1 | stronger wobble | tiling visible | GPU low | Impl. |
| Enable Shader Enhancements | per-layer rotation/scale + blend sharpness | off (recommended on) | — | — | — | Impl. |
| Shader UV Rotation Strength / Scale Variation / Texture Blend Sharpness | | 0.5 / 1.2 / 4 (rec. 1.5) | | | — | Impl. |
| Biome textureVariations[] | extra textures | — | — | — | — | Partial (in the array, not sampled by the package shader) |
| Terrain Shader | Package Tri-Planar / Project Shader / Custom Material | Package | — | — | — | Impl. |
| Custom Terrain Material | for Custom Material mode | — | — | — | — | Impl. |
| Terrain Texture Size | world units per repeat (0 = auto) | 0 | larger | smaller | — | Impl. |
| Triplanar Strength / Slope Start / Slope End / Sharpness | tri-planar | 1 / 25 / 45 / 6 | | | GPU | Impl. |
| Terrain Smoothness / Wetness Darkening / Wetness Smoothness | gloss & wetness | 0.08 / 0.35 / 0.55 | | | — | Impl. |
| Biome Texture Quality / Resolution | texture array format/size | Automatic / 0 | | | memory | Impl. |

## 10. Object placement settings

| Variable | Purpose | Typical | Increase | Decrease | Perf | Status |
|---|---|---|---|---|---|---|
| Should Spawn Objects | master | on | — | — | — | Impl. |
| Cluster Base Frequency / Cluster Amplitude | old global clustering | — | — | — | — | Placeholder |
| Object Cliff Angle | "cliff" for rules | 45 | fewer cliffs | more | — | Impl. |
| Object Spawn Budget Ms / Max Objects Per Frame | creation budget | 2 / 300 | faster appearance | smoother | main thread | Impl. |
| Full Object Distance / Far Object Parts | far switching | 150 / colliders, scripts, animators, audio | | | **high** | Impl. |
| Pool Objects / Max Pooled Objects | pooling | on / 4000 | fewer hitches | memory | — | Impl. |
| Worker Threads / Main Thread Budget Ms / Prepare Meshes On Workers | threading | 0 / 4 / on | | | — | Impl. |
| BiomeObject rules | per object — see [Object Placement](14-Object-Placement.md) §3 | — | — | — | — | Impl. |
| BiomeObject.currentNumberOfThisObject, densityMap | old counters | — | — | — | — | Placeholder |
| BiomeInstance.currentNumberOfObjects | old counter | — | — | — | — | Placeholder |

## 11. Portal and mob settings (EndlessTerrain)

See [Portals and Mobs](15-Portals-and-Mobs.md) §5 for every value. All fields listed there are Impl.; settings removed in
the rewrite (weather/time-based portal spawning, ecosystem balance, territorial behaviour, seasonal modifiers…) no
longer exist.

## 12. Preview settings (World Preview)

| Setting | Purpose | Typical | Increase | Decrease | Perf |
|---|---|---|---|---|---|
| Center | map centre | 0,0 | — | — | — |
| Area Size | area shown | 4000 | more world, less detail | more detail | ∝ rivers traced |
| Resolution | pixels per side | 256 | detail | speed | ∝ res² |
| Show | view mode | Combined | — | — | recolour only |
| Shading / Light Angle / Contours / Interval | display | 1 / 315 / off / 25 | — | — | — |
| Chunk Grid / Biome Borders / Origin / Scene View / Landmarks overlays | display | — | — | — | landmarks: plan build |
| Auto Regenerate / Image Size | workflow | off / 512 | — | — | — |

## 13. Not currently implemented

Asked-about variables that do **not** exist in the code: seasonal variation, humidity separate from moisture, slope- or
height-based texture *selection* (rock on cliffs, snow textures as layers), normal/roughness/metallic maps per biome,
dynamic climate change, lava/eruptions, water levels reacting to rain, GPU (Burst/compute) generation, terrain
editing/deformation at runtime, road/village generation. See [Glossary & Future](20-Glossary-and-Future.md).
