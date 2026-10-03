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
| Floor Spacing | float ≥ 5 | least vertical distance between floors; with *Heights › Auto Floor Spacing* floors move further apart when the tallest ceiling needs it (the inspector shows the effective value) | 12 | more room above ceilings, longer stairs | with auto off, ceilings that don't fit are lowered (warning) | low | Impl. |
| Max Stair Slope | float 20–45° | steepest stair | 33 | shorter stair wells | longer wells (harder to fit) | low | Impl. |
| Floor Count | Vector2Int | floor range when the size class has (0, 0) | 2–4 | deeper dungeons | shallower | **high** (per floor) | Impl. |
| Size Classes | array | Small/Medium/Large/Huge: floor cells, floor count | 46²/64²/86²/112² | bigger floors | smaller | **high** (∝ cells) | Impl. |
| Footprint Variation | float 0–0.4 | per-floor size/position variation | 0.15 | floors less aligned | identical stacked floors | — | Impl. |
| Openness | FloatRange 0–1 | per-floor roll: room size, coverage, loops, caves | 0.3–0.75 | bigger rooms, more loops, open caves | tight floors | low | Impl. |
| Complexity | FloatRange 0–1 | per-floor roll: dead ends, BSP depth, maze pruning | 0.3–0.75 | more dead ends and branching | simpler | low | Impl. |
| Styles (Rooms, BSP, Caverns, Hybrid, Grid Maze, Citadel, Catacombs, Tower, Undercity, Hive, Islands, Den, Astral) | floats ≥ 0 | style weights (only Tower > 0 = a tower dungeon with one spiral) | 1 / 0.6 / 0.8 / 0.9 / 0.15 / 0.35 / 0.3 / 0.15 / 0.25 / 0.25 / 0.2 / 0.12 / 0.1 | style more common | rarer (0 = never) | caves ≈ med | Impl. |
| Override Last Floor Style / Last Floor Style | bool / enum | force the last floor's style | off / Den | — | — | — | Impl. |
| Natural Weight Per Floor | float | extra Caverns/Hybrid weight per floor (≤ 1.2) | 0.25 | deeper = more caves | uniform mix | — | Impl. |
| Repeat Style Penalty | float 0–1 | weight × this for the previous floor's style | 0.45 | repeats allowed | more variety | — | Impl. |
| Difficulty Per Floor | float | +difficulty per floor | 0.2 | harder deep floors | flat difficulty | more mobs | Impl. |

## 2. Rooms

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Coverage | FloatRange | share of the floor covered by rooms (by openness) | 0.2–0.38 (0.15–0.45) | denser floors | more rock | med | Impl. |
| Sizes (name, size, weight, hall, openness bias) | array | room size classes | Closet 3–4 … Hall 13–18 | — | — | — | Impl. (name is a label only) |
| Shapes (rect, L, T, cross, circle, composite, pillared hall, ruined, octagon, ring, apse, diamond) | weights | shape mix | 3/1/0.6/0.5/0.6/1/0.5/0/0.7/0.25/0.4/0.25 | shape more common | rarer | — | Impl. |
| Aspect Variation | float 0–0.8 | room elongation | 0.35 | longer rooms | squarer | — | Impl. |
| Spacing | IntRange | rock between rooms | 2–4 | room for corridors | denser | — | Impl. |
| Tight Packing Chance | float 0–1 | rooms one wall apart (door, no corridor) | 0.15 | building-like | more corridors | — | Impl. |
| Placement Attempts | int ≥ 10 | tries per floor | 700 (300–1500) | fuller big floors | faster | med | Impl. |
| Spread Candidates | int 1–8 | positions tried per room (farthest wins) | 4 | even spread | random clumps | low | Impl. |
| Room / Hall / Corridor Ceiling | float m | base built ceiling heights (× *Heights › Height Scale*) | 5 / 7.5 / 3.6 | taller spaces | lower (Min Headroom enforced) | — | Impl. |

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
| Ceiling Base / Per Wall Distance | float m | cave ceiling height and doming (× Height Scale) | 4 / 0.6 | higher domes | lower | — | Impl. |
| Ceiling Limits | FloatRange m | min/max cave ceiling | 3.4–10 | taller caverns | lower | — | Impl. |
| Ceiling Noise | float m | ceiling unevenness | 0.8 | rougher | smoother | — | Impl. |
| Wall Roughness | float 0–0.45 | cave wall contour jitter | 0.3 | rougher rock | smoother | — | Impl. |
| Wall Bulge | float 0–1 m | cave wall bulging | 0.3 | more organic | flatter | — | Impl. |

## 4b. Heights (`Ceilings`)

The inspector's **Ceiling Heights** buttons set Rooms, Caves and these values together:

| Preset | Room / Hall / Corridor | Cave base / per wall / limits | Size bonus (per cell / max) | Vault chance / height | Door |
|---|---|---|---|---|---|
| Classic | 4 / 6.5 / 3.2 | 3.2 / 0.55 / 2.8–7.5 | 0 / 0 | 0 / 0 | 2.6 |
| Standard (defaults) | 5 / 7.5 / 3.6 | 4 / 0.6 / 3.4–10 | 0.2 / 2.5 | 0.5 / 2.5 | 3 |
| Tall | 6.5 / 10 / 4.5 | 5 / 0.7 / 4–13 | 0.3 / 3.5 | 0.65 / 3.5 | 3.4 |
| Cathedral | 8 / 14 / 5.5 | 6 / 0.9 / 5–17 | 0.4 / 5 | 0.8 / 5 | 4 |

| Variable | Type | Purpose | Typical | Increase does | Decrease does | Perf | Status |
|---|---|---|---|---|---|---|---|
| Height Scale | float 0.5–3 | multiplies every ceiling (rooms, halls, corridors, caves, templates, special rooms) | 1 | whole dungeon taller | lower | — | Impl. |
| Extra Per Room Cell / Size Scaling From / Max Size Bonus | float m / int / float m | bigger rooms get taller: + per cell of the shorter side beyond the threshold, capped | 0.2 / 6 / 2.5 | big rooms tower | uniform heights | — | Impl. |
| Vault Chance / Vault Min Cells / Vault Height | float 0–1 / int / float m | halls and large rooms get an arched ceiling rising to the room's middle | 0.5 / 70 / 2.5 | more and higher vaults | flat ceilings | — | Impl. |
| Min Headroom | float ≥ 2 m | lowest clearance anywhere | 2.6 | roomier | tighter | — | Impl. |
| Door Height | float ≥ 2 m | door opening height (the frame header fills the rest) | 3 | taller doorways | lower | — | Impl. |
| Auto Floor Spacing | bool | floors move apart to fit the tallest ceiling: spacing = max(Floor Spacing, tallest ceiling + cave floor relief + rock) rounded up to 0.5 m | on | — | off: ceilings that don't fit are lowered | longer stairs | Impl. |
| Rock Between Floors | float ≥ 0.3 m | solid rock between a ceiling and the floor above | 0.8 | thicker slabs | thinner | — | Impl. |

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

## 5b. Citadel

| Variable | Type | Purpose | Typical | Status |
|---|---|---|---|---|
| Keep Size | FloatRange | keep side as a share of the floor's shorter side (by Openness) | 0.2–0.3 | Impl. |
| Keep Octagon / Round / Cross / Pillared / Cloister | floats ≥ 0 | keep shape weights | 1 / 0.7 / 0.5 / 0.8 / 0.5 | Impl. |
| Room Size | IntRange | ring room side (smaller on complex floors) | 4–7 | Impl. |
| Ring Gap | IntRange | cells between keep and rings | 3–5 | Impl. |
| Room Spacing | float ≥ 1 | cells between rooms on a ring | 3 | Impl. |
| Max Rings | int 1–4 | rings of rooms | 3 | Impl. |
| Gates | IntRange | ways from the inner ring into the keep | 2–4 | Impl. |
| Ring Link Chance | float 0–1 | outer-ring neighbours joined (inner ring always closed) | 0.7 | Impl. |
| Corner Towers / Tower Size | bool / IntRange | round towers in the corners | on / 6–8 | Impl. |

## 5c. Catacombs

| Variable | Type | Purpose | Typical | Status |
|---|---|---|---|---|
| Pitch | IntRange | cells between chambers (tighter on complex floors) | 10–14 | Impl. |
| Chamber Size / Hall Chance / Hall Size | IntRange / float / IntRange | ossuary chambers and the occasional crypt hall | 3–5 / 0.15 / 6–8 | Impl. |
| Gallery Width | IntRange | gallery width by Openness | 2–3 | Impl. |
| Loop Chance | FloatRange | extra galleries making loops by Openness (0 = perfect maze) | 0.1–0.35 | Impl. |
| Niche Chance / Every / Size / Depth | FloatRange / IntRange ×3 | burial niches along galleries | 0.25–0.6 / 3–5 / 2–3 / 2–3 | Impl. |

## 5d. Tower, Undercity, Hive, Islands, Den, Astral

See [16](16-Towers-Cities-Hives-Chasms.md) for what each does.

| Group › Variable | Type | Purpose | Typical | Status |
|---|---|---|---|---|
| Tower › Diameter | IntRange | floor diameter (cells); a tower dungeon uses one footprint for every floor | 28–36 | Impl. |
| Tower › Core Size | int 4–8 | stair core side (made odd) | 5 | Impl. |
| Tower › Ring Width / Chambers / Great Hall Chance | IntRange / IntRange / float | ring hall, wedge chambers (by Complexity), one columned hall instead | 2–3 / 4–8 / 0.2 | Impl. |
| Tower › Spiral Slope / Newel Radius | float 25–50° / float m | steepest step slope beside the column; the column's radius | 40 / 1.1 | Impl. |
| Tower › Balcony Chance | float 0–1 | a balcony room off the outer wall per floor | 0.3 | Impl. |
| Undercity › Avenue Width / Street Width / Block Size | IntRange ×3 | street grid | 3–4 / 2–3 / 10–16 | Impl. |
| Undercity › Plaza Chance / Plaza Size | float / IntRange | crossings that open into plazas | 0.35 / — | Impl. |
| Undercity › Rooms Per Building / Ruin Chance | IntRange / float | building interiors; collapsed lots open to the street | 1–3 / 0.12 | Impl. |
| Undercity › Sky Height / Interior Ceiling / Roof Above | FloatRange / float / FloatRange m | cavern ceiling over the city; building ceilings; roof thickness above them | 13–17 / 4.5 / 0.8–4 | Impl. |
| Hive › Cell Radius, Wall, Missing Chance, Join Chance, Brood Chance, Wobble, Ceiling | mixed | honeycomb cells, gaps, tunnels, brood chambers, domes | — / — / — / — / — / — / 3.6–6.5 m | Impl. |
| Islands › Island Radius, Gap, Platform Share, Bridge Width, Rim Width, Ceiling, Chasm Depth | mixed | islands, joins (platform share; the rest bridges), the rim ledge, the chasm | — / — / 0.3 / — / 2 / 10–14 m / 6 m | Impl. |
| Den › Cave Share, Side Tunnels, Side Cave Size, Cave Height, Hoard Piles | mixed | the great cavern and its side caves | — / — / 5–9 / 16 m / 6–12 | Impl. |
| Astral › Platform Radius, Gap, Portal Share, Platform Share, Ceiling, Void Depth | mixed | floating platforms and their joins (portals, moving platforms, the rest bridges) | 3–5 / 4–8 / 0.45 / 0.25 / 12–16 m / 8 m | Impl. |
| Astral › Flip Interval / Flip Duration / Float Height | FloatRange s / float s / float m | gravity flips (0 = never) | 35–70 / 6 / 3 | Impl. |

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
| Climb Chance / Climb Size | float / int 1–2 | drops that are climbable shafts (vines, both ways) | 0.12 / 1 | more climbs | only drops | low | Impl. |
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
| Ceiling Height | float m | this room's ceiling (× Height Scale); 0 = normal room/hall | Boss 10, Arena 8, Shrine 7, special rooms 5.5–10 | Impl. |
| Vaulted | bool | arched ceiling (by Vault Height) | Boss, Shrine, Crypt, Throne… | Impl. |
| Hidden Room | bool | a dead end beside the room becomes a secret room behind a lever-operated secret door; rooms with such a neighbour are preferred | Wine Cellar | Impl. |

The default list has 6 classic rules plus 22 special rooms (Guardian, Vault, Trap Gauntlet, Puzzle, Ambush, Library,
Armory, Prison, Crypt, Laboratory, Garden, Throne, Nest, Cursed Altars, Kitchen, Gallery, Barracks, Pit Fight,
Greenhouse, Wine Cellar, Map Room, Gas Chamber); see [15 §3](15-Types-Special-Rooms-Mechanics.md). A layout may
suggest a role for an area (`Area.Hint`, e.g. the den's cavern for the boss); suggestions are tried first. The profile
inspector's **Add Special Rooms** appends the missing ones to an existing list.

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
| Vault Loot / Challenge Loot | IntRange | items in vaults / after gauntlets, puzzles, ambushes and guardians | 3–5 / 1–2 | more loot | less | low | Impl. |
| Fill Missing Role Props | bool | special rooms and floor modifiers the prop table lacks get the built-in props | on | — | only the table's props | low | Impl. |
| Max Placements Per Floor | int ≥ 10 | safety cap | 500 | — | fewer objects | caps build cost | Impl. |

### Table entries

See [08 §7](08-Population.md). Every entry field is used by population or the builder (Impl.). *Encounter › Prefab*
needs a NavMeshAgent of the profile's agent type. *Prop › Modifiers* limits a prop to floors with those modifiers (empty =
any floor). The prop table inspector's **Add Missing Built-in Props** adds the built-in props the table lacks.

## 8b. Mechanics (`RoomEventSettings`)

| Variable | Type | Purpose | Typical | Status |
|---|---|---|---|---|
| Lock Boss Room / Lock Arenas / Lock Guardian Rooms | bool | doors close until the room's mobs are dead | on | Impl. |
| Ambush Waves | IntRange | waves in ambush rooms (each after the previous dies) | 2–3 | Impl. |
| Crypt Ambush Chance | float 0–1 | crypt mobs stay hidden until players enter, then the room locks | 0.5 | Impl. |
| Elite Scale / Elite Tier Bonus | float 1–3 / int 0–5 | guardian and throne elites: size and tier | 1.3 / 1 | Impl. |
| Vault Keys | bool | vaults get a locked door and a reachable key | on | Impl. |
| Puzzle Plates | IntRange | pressure plates in puzzle rooms | 3–4 | Impl. |
| Shortcut Chance | float 0–1 | per floor: a loop door that opens only from its far side | 0.4 | Impl. |
| Max Gates Per Room | int 1–16 | rooms with more openings are never locked | 8 | Impl. |
| Pit Fight Waves / Champion Scale | IntRange / float 1–3 | pit fight waves; the champion's size | 3–4 / 1.45 | Impl. |
| Sleeper Share | float 0–1 | barracks soldiers asleep | 0.8 | Impl. |
| Nests Per Room | IntRange | nests in a nest room | 1–2 | Impl. |
| Roamer Chance / Roamer Scale | float 0–1 / float 1–3 | per floor: an elite that patrols from room to room | 0.35 / 1.3 | Impl. |
| Shifting Chance / Shifting Share / Shift Interval | float / float / FloatRange s | per floor: loop passages that get shifting walls, and how often they reshuffle | 0.25 / 0.6 / 90–180 | Impl. |
| Tripwire Chance | float 0–1 | per corridor of 8+ cells: a tripwire and arrow launchers | 0.15 | Impl. |
| Chasm Fall / Fall Damage | enum / float 0–1 | Floor Below Else Death, Floor Below, Death; damage of a fall to the floor below | Floor Below Else Death / 0.2 | Impl. |

Locks are only placed where every required area (exit, stairs, keys) stays reachable without them.

## 8c. Floor Modifiers

| Variable | Type | Purpose | Typical | Status |
|---|---|---|---|---|
| Chance / First Floor | float 0–1 / int | chance a floor gets a modifier, from this floor down | 0.3 / 1 | Impl. |
| Flooded / Molten / Overgrown / Darkness / Frozen | floats ≥ 0 | modifier weights | 1 / 0.6 / 0.8 / 0.7 / 0.5 | Impl. |
| Water Level | float 0.05–1.2 m | flooded water height above the floor | 0.35 | Impl. |
| Dark Light Share / Dark Encounter Bonus | float 0–1 / float | dark floors: lights kept / extra mobs | 0.2 / 0.25 | Impl. |
| Atmosphere | bool | ambient light and fog change on modified floors (needs the theme's Apply Atmosphere) | on | Impl. |

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
| Liquid, Liquid Colour | Material, Color | flooded-floor water surface | empty → translucent teal | Impl. |
| Gate, Locked Door, Shortcut Door, Key, Pressure Plate | prefabs | replace the mechanic primitives (the components are added when missing) | empty | Impl. |
| Teleporter, Moving Platform, Lever, Nest | prefabs | replace the newer mechanic primitives (components added when missing) | empty | Impl. |
| Void Material, Void Colour | Material, Color | the bottom of chasms and the astral void | empty → near-black blue | Impl. |

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
| DungeonPortal › Action, Player Tag (fallback), Arm Delay | set by builder, Player, 1.5 s | players found by Combat Entity; only participants can use it | Impl. |
| DungeonFloorStreamer › Streaming, Floors Around, Target | from profile | empty Target = every floor with a participant stays active | Impl. |
| DungeonRespawnDirector › Manager, Interval, Max Share Alive, Min Distance, Avoid Camera View | —, 20–45 s, 0.75, 25 m, on | Min Distance from **every** player | Impl. |
| DungeonHazard › Affects, Target Tag (fallback), Damage, Apply Damage, Interval, Spike Rise, On Hit | Players, —, 10, on, 1 s, 0.25 | damage through the combat system | Impl. |
| DungeonSecretDoor › Search Distance, Hold Time, Open Speed | 1.8 m, 1.5 s, 1.2 m/s | any player can find it | Impl. |
| DungeonFlicker › Amount, Speed | 0.25, 7 | | Impl. |
| Portal › Random Profiles | empty | one is picked per seed (Profile when empty) | Impl. |
| DungeonHazard › Timing, Active / Inactive Seconds, Phase Offset, Damage Type, Element | Constant, 1.2 / 1.8 s, 0, Physical, None | Cycle = on/off traps (fire jets, steam) | Impl. |
| DungeonBladeTrap › Affects, Damage, Amplitude, Period, Phase Offset, Blade Size | Players, 18, 55°, 2.6 s, 0, 1.6×0.35×0.15 | swinging blade from the ceiling | Impl. |
| DungeonDartTrap › Range, Lane Width, Reload, Speed, Damage, Element, Muzzle Height | 9 m, 1.4, 1.6 s, 16, 9, Poison, 1.2 | fires when a player crosses its lane | Impl. |
| DungeonBarrier › Travel, Speed | auto (collider height), 3 m/s | base of gates and doors that slide into the floor | Impl. |
| DungeonGate › Start Closed | off | room-event gate | Impl. |
| DungeonLockedDoor › Reach, Key Name | 2.2 m, Vault Key | opens for a player when the party holds its key | Impl. |
| DungeonShortcutDoor › Reach | 2 m | opens from its far side only | Impl. |
| DungeonKey › Key Name, Pickup Distance, Animate | Vault Key, 1.3 m, on | shared by the party | Impl. |
| DungeonRoomEvent › Mode, Floor, Area, Waves, Leave Grace, Messages | set by builder, 4 s, on | LockUntilCleared / Ambush / Puzzle | Impl. |
| DungeonPressurePlate › Radius, Sink | 0.75 m, 0.05 m | | Impl. |
| DungeonPuzzle › Show Time, Wrong Damage | 0.7 s, 8 | Simon-style plate order | Impl. |
| DungeonInteractable › Hold To Use, Use Distance, Uses, Cooldown | 1.2 s, 1.8 m, 1, 2 s | base of shrines, rest points, chests, herbs | Impl. |
| DungeonRestPoint › Radius, Heal, Revive Fallen | 8 m, 1, on | also sets the session checkpoint | Impl. |
| DungeonShrine › Radius, Duration, Blessings | 8 m, 150 s, Might/Swiftness/Warding… | timed stat blessing for the party | Impl. |
| DungeonChest › Drops, Lid Angle | empty, 70° | | Impl. |
| DungeonHerb › Heal, Message | 0.35, "The herbs soothe your wounds." | share of max health (rare herbs 0.6, stew 0.25) | Impl. |
| DungeonSecretDoor › Searchable | on | off: only `Open()` (a lever) opens it | Impl. |
| DungeonDartTrap › Triggered Only, Volley | off, 1 | arrow launchers: fire only when their tripwire calls `Trigger()` | Impl. |
| DungeonBladeTrap › Axis, Knockback | Sideways, 0 | Forward Back + 6 for swinging logs | Impl. |
| DungeonRoomEvent › Mode Pit Fight | — | waves at the gates, the champion last, crowd messages | Impl. |
| DungeonChasm › Fall, Fall Damage, Fall Depth | from Mechanics, 4 m | a fall to the floor below or death | Impl. |
| DungeonTeleporter › Style, Delay, Radius, Cooldown | Pad, 0.6 s, 0.9 m, 2.5 s | pad or painting; partner by link | Impl. |
| DungeonMovingPlatform › Speed, Wait, Half Size, Deck Height, Waypoints | 2.2 m/s, 1.8 s, set by builder | carries players | Impl. |
| DungeonClimbable › Speed, Mantle | 2.6 m/s, 3.5 m/s | climb shafts | Impl. |
| DungeonGravityShift › Interval, Duration, Float Height, Warning | from Astral, 3 s | astral gravity flips | Impl. |
| DungeonNest › Health, Stomp, Spawn Interval, Max Alive, Max Spawns, Activation Radius | 120, 0.25, 7 s, 3, 0, 16 m | spawner | Impl. |
| DungeonGamblingAltar › Price, Blood Cost, Curse Duration, Curses | Blood, 0.3, 240 s, Frailty/Sluggishness/Clumsiness/Dimwit | health or a curse for loot | Impl. |
| DungeonBreakable › Hits, Debris Time | 1, 6 s | breakable barrels and crates; barrel stash | Impl. |
| DungeonLever › Action, Handle | set by builder | open a secret door / shut a gas valve | Impl. |
| DungeonGasCloud › On Time, Off Time, Damage, Tick, Color | 7 s, 6 s, 5, 0.8 s, green | gas chamber | Impl. |
| DungeonMapOverlay › Size, Opacity, Map Action Names, key controls | 0.7, 0.85, "Map", M / gamepad Select | the revealed floor map | Impl. |
| DungeonSleeper › Wake Radius, Sneak Radius, Alarm Radius | 5 m, 1.6 m, 6 m | sleeping barracks mobs | Impl. |
| DungeonRoamer › Stops, Speed, Linger | 5, 2.2 m/s, 3 s | roaming mini-boss route | Impl. |
| DungeonTripwire › Half Length, Rearm, Sneak Over | set by builder, 5 s, on | fires its group's launchers | Impl. |
| DungeonShiftingFloor › Interval, Close Chance | from Mechanics, 0.6 | reshuffles shifting walls, keeping the floor connected | Impl. |
| DungeonSpectators › Jump | 0.25 m | the cheering crowd | Impl. |
| DungeonFloorAtmosphere | set by builder | applies a floor modifier's light and fog | Impl. |

## 12. Constants in code (not settings)

| Constant | Value | Where |
|---|---|---|
| Walkable slope limit between cells | 28° | `HeightPass` |
| Minimum entrance / exit room | 9 cells | `ValidateStage` |
| A* node cap | 400 000 | `CorridorRouter` |
| Wall hug penalty | 3 | `CorridorRouter` |
| Stair step rise | 0.2 m | `LinkMesher` |
| World-position seed quantisation | 8 m | `DungeonRequest.FromWorldPosition` |
| Worker threads | clamp(cores − 1, 1, 4) | `DungeonWorkers` |
| Retry seed salt | 0xA77E | `DungeonPipeline` |

## 13. Not currently implemented

- Per-floor themes (one theme per dungeon).
- Saving a dungeon's state (killed mobs, opened chests) between visits — each visit rebuilds the dungeon fresh from
  its seed.
- Multiplayer synchronisation.
- Mobs crossing teleporters, moving platforms or climbs (the NavMesh doesn't link them; mobs stay on their side).
- Real upside-down gravity (astral flips make players float, they don't walk on the ceiling).
- Lava as a surface (molten floors use lava pool props and hazards; only water is a meshed surface).
