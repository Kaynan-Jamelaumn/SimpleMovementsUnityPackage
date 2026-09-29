# Dungeon 12 — Configuration Reference

Every setting in the dungeon system, grouped as in the inspectors. All the settings listed here are read by the code
(**Impl.**). Where a setting only applies in some situations, the Status column says so (**Partial**). Things that do
not exist are listed at the end under *Not currently implemented*.

Performance impact: — none · low · med · **high** (generation on the worker, or building on the main thread).

---

## 1. DungeonProfile › General

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Cell Size | float ≥ 0.5 | metres per grid cell | 1.5 (match the tile kit's Module Size) | bigger rooms/corridors in metres, fewer cells | finer detail, more cells | med | Impl. |
| Floor Spacing | float ≥ 5 | vertical distance between floors | 10 | taller ceilings possible, longer stairs | ceilings clamped (warning) | low | Impl. |
| Max Stair Slope | float 20–45° | steepest stair | 33 | shorter stair wells | longer wells (harder to fit) | low | Impl. |
| Floor Count | Vector2Int | floor range when the size class has (0, 0) | 2–4 | deeper dungeons | shallower | **high** (per floor) | Impl. |
| Size Classes | array | Small/Medium/Large/Huge: floor cells, floor count | 46²/64²/86²/112² | bigger floors | smaller | **high** (∝ cells) | Impl. |
| Footprint Variation | float 0–0.4 | per-floor size/position variation | 0.15 | floors less aligned | identical stacked floors | — | Impl. |
| Openness | FloatRange 0–1 | per-floor roll: room size, coverage, loops, caves | 0.3–0.75 | bigger rooms, more loops, open caves | tight floors | low | Impl. |
| Complexity | FloatRange 0–1 | per-floor roll: dead ends, BSP depth, maze pruning | 0.3–0.75 | more dead ends and branching | simpler | low | Impl. |
| Styles (Rooms, BSP, Caverns, Hybrid, Grid Maze) | floats ≥ 0 | style weights | 1 / 0.6 / 0.8 / 0.9 / 0.15 | style more common | rarer (0 = never) | caves ≈ med | Impl. |
| Natural Weight Per Floor | float | extra Caverns/Hybrid weight per floor (≤ 1.2) | 0.25 | deeper = more caves | uniform mix | — | Impl. |
| Repeat Style Penalty | float 0–1 | weight × this for the previous floor's style | 0.45 | repeats allowed | more variety | — | Impl. |
| Difficulty Per Floor | float | +difficulty per floor | 0.2 | harder deep floors | flat difficulty | more mobs | Impl. |

## 2. Rooms

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Coverage | FloatRange | share of the floor covered by rooms (by openness) | 0.2–0.38 (0.15–0.45) | denser floors | more rock | med | Impl. |
| Sizes (name, size, weight, hall, openness bias) | array | room size classes | Closet 3–4 … Hall 13–18 | — | — | — | Impl. (name is a label only) |
| Shapes (rect, L, T, cross, circle, composite, pillared hall, ruined) | weights | shape mix | 3/1/0.6/0.5/0.6/1/0.5/0 | shape more common | rarer | — | Impl. |
| Aspect Variation | float 0–0.8 | room elongation | 0.35 | longer rooms | squarer | — | Impl. |
| Spacing | IntRange | rock between rooms | 2–4 | room for corridors | denser | — | Impl. |
| Tight Packing Chance | float 0–1 | rooms one wall apart (door, no corridor) | 0.15 | building-like | more corridors | — | Impl. |
| Placement Attempts | int ≥ 10 | tries per floor | 700 (300–1500) | fuller big floors | faster | med | Impl. |
| Spread Candidates | int 1–8 | positions tried per room (farthest wins) | 4 | even spread | random clumps | low | Impl. |
| Room / Hall / Corridor Ceiling | float m | built ceiling heights | 4 / 6.5 / 3.2 | taller spaces | lower (min 2.4 enforced) | — | Impl. |

## 3. BSP

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Min Leaf Size | int ≥ 6 | smallest partition | 11 | fewer, bigger rooms | more, smaller | low | Impl. |
| Split Ratio | FloatRange | split position | 0.38–0.62 | — | 0.5–0.5 = always halves | — | Impl. |
| Room Margin | IntRange | gap to the partition edge | 1–3 | smaller rooms, room for corridors | rooms fill partitions | — | Impl. |
| Empty Leaf Chance | float 0–0.6 | partitions left as rock | 0.08 | sparser | fuller | — | Impl. |
| Early Stop Chance | float 0–0.6 | bigger rooms (lowered by complexity) | 0.12 | more large rooms | more uniform | — | Impl. |
| Shaped Room Chance | float 0–1 | non-rectangular rooms | 0.3 | varied shapes | all rectangles | — | Impl. |

## 4. Caves

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Solid Fill | FloatRange | initial rock share (by 1 − openness) | 0.46–0.555 | tighter caves, isolated pockets | huge open caverns | — | Impl. |
| Fill Noise Scale / Strength | float / 0–0.4 | rock share variation across the floor | 0.06 / 0.2 | patchier caves | uniform | — | Impl. |
| Smoothing Iterations | int 0–10 | cellular-automaton passes | 5 (4–6) | rounder | noisier | med | Impl. |
| Rock Threshold / Open Threshold | int 4–8 / 0–4 | automaton rules (of 8 neighbours) | 5 / 3 | — | — | — | Impl. |
| Min Region Cells | int ≥ 1 | pockets smaller are filled | 40 | fewer tiny caves | more specks | — | Impl. |
| Min Passage Width | int 1–3 | widen narrow passages | 2 | roomier | 1-cell squeezes | low | Impl. |
| Chamber Min Radius | float | chamber centres this far from rock | 2.2 | fewer, bigger chambers | more, smaller | — | Impl. |
| Chamber Spacing | float | distance between chamber centres | 8 | fewer chambers | more | — | Impl. |
| Tunnel Width | IntRange | tunnel width | 2–3 | wider tunnels | narrower | low | Impl. |
| Tunnel Winding | float 0–2 | tunnel wander | 0.9 | twistier | straighter | med (A*) | Impl. |
| Floor Height Amplitude / Scale | float m / float | cave floor relief | 1.1 / 0.07 | bumpier | flatter | — | Impl. |
| Height Smoothing | int 0–12 | floor smoothing passes | 5 | gentler | rougher (still slope-limited) | low | Impl. |
| Ceiling Base / Per Wall Distance | float m | cave ceiling height and doming | 3.2 / 0.55 | higher domes | lower | — | Impl. |
| Ceiling Limits | FloatRange m | min/max cave ceiling | 2.8–7.5 | — | — | — | Impl. (max < Floor Spacing) |
| Ceiling Noise | float m | ceiling unevenness | 0.7 | rougher | smoother | — | Impl. |
| Wall Roughness | float 0–0.45 | cave wall contour jitter | 0.3 | rougher rock | smoother | — | Impl. |
| Wall Bulge | float 0–1 m | cave wall bulging | 0.3 | more organic | flatter | — | Impl. |

## 5. Hybrid and Maze

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Zone Size | float ≥ 8 | hybrid zone size (cells) | 22 | fewer, bigger zones | patchwork | — | Impl. |
| Border Noise | float | zone border irregularity | 6 | wavier | straight borders | — | Impl. |
| Built / Cavern / Ruins Weight | floats | zone mix | 1 / 1 / 0.5 | — | — | — | Impl. |
| Ruins Erosion | float 0–0.8 | how broken ruined rooms are | 0.35 | more collapsed | intact | — | Impl. |
| Block Size | int ≥ 3 | maze room size (match legacy prefabs: 7) | 7 | bigger blocks | smaller | — | Impl. |
| Gap | int ≥ 1 | corridor length between blocks | 2 | longer corridors | adjacent blocks | — | Impl. |
| Prune Fraction | FloatRange | dead-end blocks removed (by 1 − complexity) | 0–0.35 | fewer rooms | full grid | — | Impl. |
| Templates | RoomTemplate list | block templates (e.g. legacy RoomBehaviour rooms) | — | — | plain rooms | low | Impl. |

## 6. Connections and Links

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Loop Chance | FloatRange | loop probability (by openness) | 0.08–0.4 | more alternative routes | tree-like | low | Impl. |
| Dead End Share | FloatRange | allowed dead-end share (by complexity) | 0.15–0.4 | more dead ends | fewer | — | Impl. |
| Corridor Width | IntRange | normal / wide corridor width | 1–2 | wider | narrower | low | Impl. |
| Wide Corridor Chance | float 0–1 | × openness × 2 | 0.4 | more wide corridors | fewer | — | Impl. |
| Turn Penalty | float | A* cost per turn (built) | 2.5 | straighter | winding | low | Impl. |
| Reuse Cost | float 0.05–1 | cost of walking an existing corridor | 0.35 | parallel corridors | more junctions | — | Impl. |
| Max Connection Length | float cells | longer candidates only as a last resort | 60 | long corridors allowed | local links | — | Impl. |
| Secret Chance | float 0–1 | loop corridors behind secret doors | 0.12 | more secrets | none | — | Impl. |
| Stair Width | int 1–4 | stair well width | 2 | wider stairs (harder to fit) | narrower | — | Impl. |
| Landing Size | IntRange | landing room size | 4–6 | bigger landings | smaller | — | Impl. |
| Departure Distance | FloatRange | how far the way down is from the way in | 0.6–1 | longer crossings | shortcuts | — | Impl. |
| Extra Stair Chance / Max Extra Stairs | float / int 0–3 | alternative stairs | 0.35 / 1 | more routes between floors | one stair | low | Impl. |
| Drop Chance / Drop Size | float / int 1–3 | one-way pits | 0.3 / 2 | more drops | none | low | Impl. |
| Edge Margin | int ≥ 2 | shafts away from the footprint edge | 4 | safer, harder to fit | tighter | — | Impl. |
| Anchor Spacing | int ≥ 1 | cells between anchor rooms | 3 | spread anchors | closer | — | Impl. |
| Entrance Size / Exit Size | IntRange | anchor room sides | 6–8 / 7–9 | bigger rooms | smaller (min 9 cells validated) | — | Impl. |

## 7. Roles (list of Role Rules)

| Variable | Type | Purpose | Typical | Status |
|---|---|---|---|---|
| Name, Role, Tag | string, enum, string | what is assigned | see [06 §4](06-Roles-and-Templates.md) | Impl. |
| Required | bool | must be placed (relaxes filters, else retry) | Boss only | Impl. |
| Per Floor / Max Total | IntRange / int | how many | 0–1 / −1 | Impl. |
| Min Floor / Max Floor / Last Floor Only | int / int / bool | where | 0 / −1 / off | Impl. |
| Progress | FloatRange | along the main path | 0–1 | Impl. |
| Placement | enum | Any, Leaf, On/Off Main Path, Hub, End Of Main Path | per rule | Impl. |
| Size / Min Cells | enum / int | size preference and minimum | Any / 0 | Impl. |
| Styles | mask | Built / Cavern / Ruins | all | Impl. |
| Chance / Weight | float / float | optional chance / score | 1 / 1 | Impl. |
| Templates | RoomTemplate list | fitted into the area | — | Impl. |
| Secret Entrance | bool | built connections become secret (off main path only) | Secret rule | Impl. |

## 8. Population

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Encounters / Loot / Props | tables | what to place | — | — | empty → defaults | — | Impl. |
| Default Loot / Default Props | bool | built-ins when a table is missing | on | — | nothing placed | — | Impl. |
| Placeholder Mobs | bool | capsules for mobs without prefabs | on (testing) | — | no mobs without prefabs | low | Impl. |
| Encounter Density | float | mob budget per 100 cells at difficulty 1 | 2.2 (1–4) | more mobs | fewer | med (build) | Impl. |
| Safe Radius | float cells | no mobs within this walking distance of the arrival (half on deeper floors) | 16 | safer spawns | mobs closer | — | Impl. |
| Pack Spacing | float cells | minimum distance between packs | 5 | spread packs | clustered | — | Impl. |
| Corridor Encounter Chance | float 0–1 | wanderers in long corridors | 0.12 | more | none | — | Impl. |
| Treasure Room Loot / Boss Loot | IntRange | items in treasure / boss rooms | 2–4 / 1–2 | more loot | less | low | Impl. |
| Dead End Loot Chance / Room Loot Chance | float 0–1 | loot elsewhere | 0.55 / 0.15 | more loot | less | low | Impl. |
| Max Placements Per Floor | int ≥ 10 | safety cap | 500 | — | fewer objects | caps build cost | Impl. |

### Table entries

See [08 §7](08-Population.md). Every entry field is used by population or the builder (Impl.). *Encounter › Prefab*
needs a NavMeshAgent of the profile's agent type.

## 9. Theme (`DungeonTheme`)

| Variable | Type | Purpose | Default | Status |
|---|---|---|---|---|
| Built Floor/Wall/Ceiling, Cave Floor/Wall/Ceiling, Stairs, Trim materials | Material | surfaces of generated meshes | empty → flat colours | Impl. |
| Texture Scale | float m | metres per texture repeat (world-aligned UVs) | 3 | Impl. |
| … Colors | Color | flat colours when a material is empty | stone greys/browns | Impl. |
| Floor Tile, Wall Segment (required for the kit), Ceiling Tile, Door Frame, Pillar | prefabs | tile kit | empty | Impl. |
| Module Size, Wall Prefab Height, Scale Walls To Ceiling | float, float, bool | kit scaling | 2, 4, on | Impl. |
| Entrance Portal, Exit Portal, Door, Secret Door | prefabs | replace primitives | empty | Impl. |
| Torch Colour / Intensity / Range, Crystal Colour, Portal / Exit Portal Colour | Color / float | primitive lights and glows | warm, 9 m | Impl. |
| Apply Atmosphere, Ambient Light, Fog, Fog Colour, Fog Density | bool, Color, bool, Color, float | inside lighting | on, dark | Impl. |

## 10. Build and Validation

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Frame Budget Ms | float ≥ 0.5 | main-thread build time per frame | 6 (4–8) | faster build, bigger hitches | smoother, slower | **high** | Impl. |
| Stream Floors / Floors Around | bool / int 0–4 (min 1 used) | keep nearby floors active | on / 1 | more floors active | — | med | Impl. |
| Bake NavMesh | bool | NavMesh per floor + links | on | — | no mob movement | med (async) | Impl. |
| NavMesh Agent Type Id | int | agent type to bake for | 0 | — | — | — | Impl. |
| Geometry Layer | int | layer of generated geometry | 0 | — | — | — | Impl. |
| Mesh Chunk Cells | int 8–64 | mesh chunk size | 24 | fewer objects | better culling | med | Impl. |
| Build Ceilings | bool | ceilings | on | — | open-top (top-down cameras) | low | Impl. |
| Cast Shadows | bool | geometry shadows | on | — | cheaper | med (GPU) | Impl. |
| Use Tile Kit | bool | use the theme's kit | on | — | generated meshes | med | Impl. |
| Door Frames | bool | trim at doors | on | — | — | low | Impl. |
| Mark Static | bool | mark geometry static | on | — | — | — | Partial (only for editor builds outside Play mode) |
| Max Attempts | int 1–20 | whole-dungeon retries | 6 | more resilient | fails sooner | worst case × attempts | Impl. |
| Repair Unreachable / Max Repairs Per Floor | bool / int 0–64 | carve passages to unreachable areas | on / 16 | fewer retries | more retries | low | Impl. |
| Log Report | bool | print the report | off | — | — | — | Impl. |
| Custom Stages | DungeonStageAsset list | extra stages in slots | empty | — | — | depends | Impl. |

## 11. Components

| Component › Variable | Default | Purpose | Status |
|---|---|---|---|
| DungeonManager › Profile | — | the dungeon type (null = defaults, with a warning) | Impl. |
| DungeonManager › Request | Medium, difficulty 1 | used by `Generate()` / Generate On Start | Impl. |
| DungeonManager › Generate On Start | off | build when the scene starts (scene-mode portals) | Impl. |
| DungeonManager › Use Worker Thread | on | generation on `DungeonWorkers` (Play mode) | Impl. |
| DungeonManager › Exit Portal Action | Complete Dungeon | Return To World / Complete Dungeon / Next Dungeon | Impl. |
| DungeonManager › Playable Floors | 2 (1–4) | floors finished before Ready | Impl. |
| DungeonManager › Pause While Inside / Hide While Inside | empty | extra behaviours / objects to pause | Impl. |
| Portal › (see [10 §2](10-Runtime-Session-Portals.md)) | | | Impl. |
| DungeonSession.SpawnLift / ReentryCooldown (static) | 1 m / 3 s | arrival height, re-entry block | Impl. |
| DungeonPortal › Action, Player Tag, Arm Delay | set by builder, Player, 1.5 s | | Impl. |
| DungeonFloorStreamer › Streaming, Floors Around, Target, Player Tag | from profile | | Impl. |
| DungeonRespawnDirector › Manager, Interval, Max Share Alive, Min Distance, Avoid Camera View, Player Tag | —, 20–45 s, 0.75, 25 m, on, Player | | Impl. |
| DungeonHazard › Target Tag, Damage, Interval, Spike Rise, On Hit | Player, 10, 1 s, 0.25 | | Impl. (damage applied by your code) |
| DungeonSecretDoor › Player Tag, Search Distance, Hold Time, Open Speed | Player, 1.8 m, 1.5 s, 1.2 m/s | | Impl. |
| DungeonFlicker › Amount, Speed | 0.25, 7 | | Impl. |

## 12. Constants in code (not settings)

| Constant | Value | Where |
|---|---|---|
| Walkable slope limit between cells | 28° | `HeightPass` |
| Minimum headroom | 2.4 m | `HeightPass` |
| Minimum entrance / exit room | 9 cells | `ValidateStage` |
| A* node cap | 400 000 | `CorridorRouter` |
| Wall hug penalty | 3 | `CorridorRouter` |
| Stair step rise | 0.2 m | `LinkMesher` |
| World-position seed quantisation | 8 m | `DungeonRequest.FromWorldPosition` |
| Worker threads | clamp(cores − 1, 1, 4) | `DungeonWorkers` |
| Retry seed salt | 0xA77E | `DungeonPipeline` |

## 13. Not currently implemented

- Per-floor themes (one theme per dungeon).
- Keys/locked doors, levers or puzzles (secret doors open by proximity or `Open()` only).
- Saving a dungeon's state (killed mobs, opened chests) between visits — each visit rebuilds the dungeon fresh from
  its seed.
- Multiplayer synchronisation.
- Vertical features inside a floor (balconies, bridges over pits); floors are 2.5D.
- Water or lava surfaces in dungeons.
