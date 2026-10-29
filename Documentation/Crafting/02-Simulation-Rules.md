# 02 — Simulation Rules

Everything below is `Crafting/Core`: plain C# with no MonoBehaviour, run in fixed steps (`stepSeconds`, 0.1 s) whatever
the frame rate. Every number named here is a field of **Simulation Settings** in the Crafting Database.

## The workshop and its pieces

A `CraftWorkshop` holds the pieces and the shared equipment: the forge fire, the furnace (its fire, charge, slag, bloom
and liquid pool), the crucible (its melt, dross, cover, gas, how well mixed), the quench tank (medium, temperature,
volume), the tempering oven, the ash box and the vats. Each operation (put a piece down, move it, strike it, grind it,
fill a vat…) is recorded with the step it happened at; between operations the workshop simply runs.

A `CraftPiece` is one of:

| Kind | Its state |
|---|---|
| Metal | its composition (grams per element) and `segments` (24 along its length): length, width, thickness, the two bevels (depth and width each), fuller, bend, dish, hole, slag, porosity, scale, pits, roughness, polish, temper film, coating, edge apex, micro-bevels, burr, chips, clay; and two metallurgical **zones** per segment — the edge and the body — each with its temperature, austenite, martensite, pearlite, fineness, transformation progress, temper progress, grain, cold work, stress, decarburisation, burning |
| Ore | its ore and deposit, fragments (mass, size, mineral share, dirt), what is bound in the mineral, roasting, moisture |
| Hide | a grid of cells (`cellSize` 10 cm): present, thickness, flesh, hair, loosened hair, grain, damage, flaws, moisture, tannage at the surface and in the core, stiffness, oil, wax, dye, smoothness, rot, stretch, shrink, hardness; the frame's tension, the agent it was tanned with, its shrink temperature |
| Wood | species and segments (it is shaped like metal: rasped, carved, slotted), splits, charring |
| Fasteners | kind (rivets, studs, buckles), count, grams each, quality |
| Assembly | its design, which piece is in which place, every joint's state, supplies added |

Each piece also has defects (kind, where, how bad, whether it shows, why) and a **record** of what was done to it.

## Heat

Each zone's temperature moves toward its surroundings exponentially: in the air by convection and radiation, in the
fire by `fireTransfer` scaled by how deep it sits and how high the flames are, in the oven, in the ashes (very slowly:
annealing), in the quench by the medium's severity. Heat also conducts along the bar (`conduction`) and between edge and
body (`zoneConduction`): a thin edge heats and cools faster than a thick spine.

**The fire** burns fuel (`fuelBurnRate`); its level rises and falls with a lag; its draft is a base draft plus the air
from the bellows, which decays (`bellowsDecaySeconds`); the vent chokes it. Each fuel has a maximum temperature
(charcoal 1350 °C, coal 1450 °C with sulfur, coke 1550 °C, firewood 950 °C), a reducing character and an ash. A fuel with
`burnsAnyWood` (firewood) is fed from any wood in the bag when its own item is missing: wooden blanks, and plain materials
named as wood or after a wood (Oak Plank, Ash Log, Timber, Branch). The middle
of the fire is hotter than its edge (`fireMiddle`, `fireEdge`). Leaving a piece unturned heats one face more.

**Scale** grows in an oxidising fire, parabolically, doubling every `scaleDoublingDegrees` and eating metal
(`scaleMetalLoss`); a reducing fire scales less. Above ~1000 °C steel loses carbon at the surface (decarburisation) in an
oxidising fire and gains a little in a carburising one; a coal fire puts sulfur in.

## Phases, hardening and tempering

What a metal *is* comes from its composition only (`CraftMetallurgy.Analyze`): the base element, the alloy effects of
the others (each a curve over its fraction: tin in copper hardens it up to a peak and embrittles beyond), the class whose
ranges it matches (the highest priority first). Steel is iron with carbon above `hardenableCarbon` and below
`castIronCarbon`; its critical temperature runs from 727 °C (eutectoid) to 912 °C (pure iron).

* **Austenitising** — above the critical temperature a zone turns to austenite over `austenitizeSeconds`, refining its
  grain once complete; held long and hot, the grain grows again (`grainGrowthRate`).
* **Cooling** — austenite turns to pearlite along a TTT curve (fast near its nose at ~550 °C, `pearliteNoseSeconds`,
  slower with more carbon, nickel, manganese, starmetal — hardenability — and coarser grain), and below the martensite
  start temperature (lower with more carbon) to martensite (Koistinen–Marburger). What is left is retained austenite.
  So: water quenches a high-carbon bar hard (≈ 790 HV), oil only half hardens it, mild steel barely hardens at all,
  an alloy steel hardens even in oil, and steel that was never above its critical temperature does not harden.
* **Stress and cracks** — martensite forming fast builds stress (`quenchStress`): beyond `crackStress` a segment cracks,
  each with its own flaw so cracks start at weak spots and relieve their neighbours. Brine cracks more than water,
  water than oil; thin, high-carbon, agitated pieces crack more.
* **Warp** — an uneven quench (held still in a vapour jacket, one face heated more, plunged flat) bends the piece
  (`quenchWarp` degrees for the whole piece); a single-edged blade curves.
* **Tempering** — reheated, martensite tempers by the Hollomon–Jaffe parameter (temperature and time;
  `temperTimeScale` compresses hours into minutes): hardness falls, toughness rises, stress relaxes. 200 °C keeps most of
  the hardness, 450 °C much less. Bright steel shows the tempering heat as oxide colours (straw, bronze, purple, blue).
* **Annealing and normalising** — cooled in the ashes it ends soft and coarse pearlite; in the air, finer and harder.
* **Burning and melting** — above its burn temperature (lower with more carbon) in the fire steel burns: its grain falls
  apart and it crumbles under the hammer; above its solidus a segment drips away. A casting or a bar cooling in the
  air does not burn.
* **Clay** along the spine slows its quench: a hard edge on a tough spine.

Hardness and toughness of each zone are computed from its phases, temper, grain, cold work and decarburisation;
non-steel metals harden by cold work only (bronze much more than copper) and soften again above their
recrystallisation temperature.

## Forging

A blow (`CraftForging.Strike`: position, tool, face, force, tilt) reduces the thickness (or width) under the hammer by
`strikeReduction` × force × how plastic the metal is at that temperature × the tool, and puts the volume back into
length or width: the flat face spreads, the cross peen draws it long, the straight peen widens it, the fuller grooves
it, the horn bends it, the hardy cuts it, the drift opens an eye, the flatter smooths it, the dishing stake cups a
plate, a tilted blow makes a bevel. Volume is conserved (only scale is lost). The anvil chills the work
(`anvilChill`); the blow itself warms it a little.

* Hot work above the recrystallisation temperature refines the grain; cold work hardens (and eventually cracks) it.
* Forged far below its working heat, steel tears; hardened steel struck cold hardly moves and cracks; cast iron
  shatters; red-short iron (sulfur) tears at forging heat, cold-short iron (phosphorus) when cold; burnt steel crumbles.
* Thick scale is hammered into the surface as pits. Upsetting a thin flat bar on its edge folds it (a cold shut).
* A bloom struck at welding heat closes up and squeezes out its slag; struck cold, it falls apart.
* **Folding** and **forge welding** need welding heat (`weldingMargin` below the burn temperature) and flux; without
  them the layers do not take (delamination). Folding mixes and loses a little carbon.
* **Cutting** with a hot chisel splits a piece in two (mass conserved). **Fasteners** are cut and headed from a thin rod,
  or a buckle bent over the horn.

The sound of a blow tells what happened: a clear ring (cold, sound), a dull thud (hot), a flat dead sound (a crack).

## Surface work and sharpening

Abrasion (`CraftSurface`) takes metal off where it is applied, at a rate set by the abrasive's cut, the pressure, the
time and the metal's hardness: bevels grow at the angle held (their angle is the area-weighted angle of all the
grinding), faces thin, the edge line, spine, tang and eye are shaped. A file skates on steel harder than it is; a rasp
or a knife works wood. A wheel heats the thin edge (`grindHeat`) — pressed hard and long it draws the temper. Brushes
and scrapers take scale off (but mend nothing); emery and rouge polish through grits; coatings protect.

Sharpening (`Sharpen`: face, stone, angle, pressure, strokes, wobble, edge leading) hones each face toward a
micro-bevel: once both faces meet, the apex closes toward the stone's floor (coarse stones leave a coarse apex, the
strop the finest), raising a burr on the far side that light trailing strokes and the strop take off. A wobbly hand
rounds the edge; heavy edge-leading strokes chip a hard, thin edge. The edge's quality is its sharpness, its angle
against the design's (an obtuse edge cuts poorly; a very acute one is fragile), its symmetry, chips and burr — the
sharpest edge is not always the best.

## Ores and smelting

An ore lump comes from a **deposit**: its richness, rock and impurities follow from the deposit's number and the lump's
(hashed, so the same deposit always gives the same ore, and no two lumps are quite alike). **Crushing** breaks fragments
smaller (liberating ore from rock — and powdering it when struck too hard); throwing out barren fragments removes rock;
**washing** carries dirt and light rock away by density (cassiterite and gold stay in the pan) — a jerky hand loses ore
with the mud; **sieving** separates the fines; **brushing** (`CraftOres.Brush`, one stone at a time, dry) takes the dirt off what the brush went over — a soft brush loose dirt (a third of caked clay), a stiff one dirt and clay, a wire brush the dirt and the weathered rind of rock (the rind is the larger share of a small stone's rock, `Rind(size)`) at the cost of a little mineral (more of a soft one, hardness under 4). Nothing is lost to the water. What is bound in the mineral (phosphorus in bog iron, sulfur in sulfides)
does not wash out.

In the **furnace** (`CraftSmelting`): sulfide ores roast first; above their reduction temperature, with charcoal (a
reducing, carbon-rich fire), the mineral gives up its metal. Iron that does not melt gathers as a spongy **bloom**,
picking up carbon (more, the hotter it runs) and trapping slag; metals that melt (copper, lead, silver, tin) run to a
**liquid pool**; if the bloom gets hotter than its own solidus it melts into cast iron. The rock becomes slag — and
silica takes iron with it into the slag (`ironPerSilica`, `gangueReactivity`) unless lime (limestone flux) takes its
place. Phosphorus goes with the iron less when there is lime. Slag builds up and chokes the draft (`slagCapacity`): tap
it (it must be hot enough to run). Lead on a bone-ash cupel in a strong hot draft oxidises away and leaves its silver.
Unroasted copper sulfide gives matte. Mythril ore needs the arcane flux.

## Crucible and casting

The crucible sits in the hotter of the forge and the furnace (`crucibleLag`). Pieces melt at their own solidus and join
the melt, which mixes over time and with stirring. The liquid oxidises into dross (less under a borax cover), boils
off what boils (zinc), and takes up gas when far too hot. Zinc ore heated with molten copper under a cover gives its
zinc to the copper (cementation: brass). What the alloy is follows from the proportions in the melt.

**Pouring** (rate, steadiness, interruptions) makes the casting: poured below the liquidus it will not run; too slowly
or too cold it freezes short (misrun); too hot (`porositySuperheat`) or too fast it traps gas (porosity); a broken
stream leaves a cold shut; dross poured in leaves inclusions. A casting is coarse-grained and rough from the mould.
How much of the mould the pour brought (`filled`) sets its length: a pour stopped early is short (misrun, *the pour
stopped before the mould was full*). Metal that missed the mould's mouth (`spilled`, per gram cast) is lost from the
crucible; metal poured on after it was full (`overflow`) runs over the top — also lost, and it leaves flash (a rough
surface to file off).

Besides the stock moulds (`Molds`), every design's metal part with a shape profile has a **pattern mould**
(`CraftRules.PatternMold("cast:design/role")`, all of them in `AllMolds()`): the part's profile at the middle of its
length range, with its eye. What is cast in it is meant for that part (`intentDesign`/`intentRole`), so its target
shows at the anvil and the wheel. It comes out blunt and rough: grind the edge, file the flash.

## Leather

A hide's cells are worked by strokes (`CraftLeather`): **fleshing** takes flesh and fat off (too hard on clean or thin
skin scores it); **liming** loosens the hair over hours (too long spoils the grain), and the grain side is then scraped;
in a **tanning vat** the agent tans the surface first and the core over time (by its strength, the vat's temperature,
stirring, the hide's thickness); too strong too early closes the surface — the core stays raw (case hardening). Alum
tawing washes out in water; oil tanning makes it supple; the alchemical essence tans in hours. Hot water shrinks and
hardens tanned leather (a little: cuir bouilli; too much ruins it); above its shrink temperature it shrinks fast. Vats
deplete as they work. Process time runs faster than real time (`processHoursPerSecond`: a tanning hour is a few
seconds of workshop time).

**Drying** on the rack or in the frame (faster warm, dry, ventilated — the workshop's humidity) stiffens leather unless
it is **staked** while damp; unstretched leather shrinks; the frame's tension stretches it (unevenly: distorted). A raw
hide left wet rots. **Oil and wax** make it supple and water-resistant; **dye** goes on evenly only on unoiled leather;
**slicking** smooths the grain; **skiving** thins it. **Cutting** a pattern takes its cells out of the hide (a careless
cut leaves ragged edges; flaws under the pattern go into the part).

**Finishing** - staking, slicking, skiving, oiling and dyeing - is done at the tannery's fleshing beam or at a leather
bench (`CraftWorkshop.FinishingTools`): a tannery finishes its own hides; a leatherworks shows the same tools under
**Finish leather**.
## Carving wood

Wood (a grip, a haft, scales) is shaped with a knife on the bench (`CraftSurface.Carve`, needs the Files tool): a cut
takes off its depth (scaled by the steadiness of the stroke: 70 % for a shaky hand, all of it for a steady one) across
the stretch drawn — off the **width**, the **thickness**, the corners (**round**: an oval section), or a **bore** for the
tang (at most 70 % of the smaller side). Wood is never cut below 3 mm. Forcing it (a cut deeper than 1.4 mm in one
stroke) or a wavering stroke (steadiness under 35 %) splits it (a crack along the grain, which counts against the
part). A long piece is sawn in two with the saw (`SplitPiece` on wood). The design's part says what shape it should
end up — the window and the 3D bench both show it.

## The woodshop

Wood is fibres (`CraftWoodwork`). **Runout** (`WoodState.runout`, 0 straight-grained - 1 cross-grained) is how far
they run out of the piece: sawn stock has some (0.2); **riving** splits a piece in two along its length and the halves
keep the grain the split followed - a clean split straightens it, one that strays cuts across the fibres, and a split
that runs out leaves one half short and tapered. Runout costs toughness (×(1 - 0.55·runout)) and makes bends tear.
**Sawing** across a piece cuts it in two where the saw went; a wandering, ragged cut leaves rough, torn ends.

**Steaming** (the Steam location): the piece heats to 100 °C and takes up water; it is steamed through after about an
hour of process time for every 25 mm of thickness. Out of the steam it is plastic while it is hot (`Plasticity` =
steamed × warmth) and sets again as it cools. **Bending** over a form: the bend's tightness is its thickness over its
radius (the form's); dry wood takes about 1:80, steamed through about 1:25, with a strap holding the outside 1:10 -
less for rushed bends, cross-grained or checked wood (`BendTear`). Past it the outside tears (a crack, `split` grows).
It springs back part of the way (it keeps 45-95% of the bend: more when it was soft).

**Seasoning** (the Stack location, or the rack): moisture falls toward 12% at a rate set by the stickers' evenness,
the shade and the warmth; when it dries faster than the wood can shrink evenly the surface and the ends **check**
(`checks`; sealed ends check far less), and unevenly stickered it warps. Checks cost toughness and integrity; wood
still green (moisture over 20%) in an item shrinks loose (a note, less integrity).

**Debarking** (`DebarkWood`, at the shaving horse): a green **log** (`CraftDefaults.Log`, the Log stock kind) comes in its
bark (`MetalSegment.bark` on every stretch). Each pull of the drawknife takes bark off a stretch by its **depth** (under
0.3 only the outer bark, from 0.3 at the cambium all of it), how much of the log's round it reached and how readily the
bark lets go (green wood ×1.25, dry ×0.65, sharper tools more); past the cambium (0.62 green, 0.52 dry) it **gouges**:
width and thickness lost, a rough surface, `WoodState.gouged`. Bark left on wet wood slows its drying and lets rot in.

**Splitting a log** (`SplitLog`, at the chopping block; only stock at least 60 mm wide): the wedge's line `a` off the
heart (0 through the pith) and `b` along the rays: through the heart the halves are even and their grain follows the
split; off it one half is much bigger (up to 88/12), across the rays the split twists out (runout, a defect); `c` how
solid the maul's blows landed.

**Seasoning** follows the air: the stack dries the wood toward the **equilibrium moisture** of the station's humidity
(`CraftWoodwork.Equilibrium`: about 8% in dry air, 17% in damp), faster in dry air, slower with bark on; damp air even
wets dry wood again.

**Sanding** smooths the surface toward a floor set by the grit and the direction (across the grain leaves scratches);
the **grits must go in order**: a paper only takes out the scratches of the one before it, so jumping more than one
grade (coarse 0.25, medium 0.55, fine 0.85) leaves the coarse floor under the fine (`lastGrit`).
**Wax** is rubbed in (`wax`): it sheds water but wears off; a **resin** sealer (a varnish whose id says resin) builds the
hardest film (`resin`); **oil** soaks in. `FinishWear` is how well it stands wear: resin best, wax least.
**Varnish or oil** builds a finish (`finish`, `finishEven`): it takes on dry wood (on damp wood it clouds and lifts),
evenly on a smooth surface (blotchy on a rough one), runs when laid thick, wrinkles over a wet coat. The finish keeps
water out (`FinishProtection`): items with finished wooden parts last longer.

## The tailor's

**Cloth** is a hide-like sheet of cells from a hide definition marked **textile** (`CraftTextiles`): it comes woven
and finished (no fleshing, liming or tanning; the tannery's tools and vats refuse it) as a straight-edged bolt, and is
cut with the patterns at the tailor's table (or a leather bench). Its grain runs along the bolt: a part cut across it
(a pattern turned a quarter) is weaker (×0.85, *cut across the grain*); a ragged cut frays (strength ×0.75-1 by the cut's
cleanness). A design's sheet part says what it is cut from (`CraftLeatherSpec.fabric`: Leather, Cloth, Either).

**Spinning** (at the station: fibre from the inventory - one per bobbin, checked before the drafting starts - thread
back into it; threading the needle needs the thread in the inventory too): the quality is the draft's evenness
times how near the twist was to right (`SpinQuality`), times how well the fibre was prepared (0.55 + 0.45 × its
preparation, `CraftFibre.Prep`): spun straight from the bag the fibre is raw (`CraftTextiles.RawPrep` - greasy, dirty,
tangled), so it never spins the fine thread; for that, prepare a batch at the fibre bench and spin it. A fibre lists
what it spins into (`spinsInto`: coarse, even, fine). **Thread** added to an item carries its strength (`Attach`'s supply strength, kept as `<role>@strength`): the
seams are as strong as the thread (×0.8-1.15 of ordinary thread).

**Measuring and fit**: `MeasureFit` (the tape, the ring sizer) records how accurately the wearer was measured and the
size read; the next item started takes it (`AssemblyState.measured`, `fit`, `fitSize`). Clothes and rings made without
measuring fit like any other (×0.95, *a loose, common fit*); measured, ×(0.9 + 0.12·accuracy) - a careless measure fits
worse than none; a ring's band formed off the measured size fits worse still.

**Layout**: patterns are chalked on cloth or a hide before cutting (`LayoutPattern`): they may not overlap; the share of
the sheet they use is its utilization (`CraftTextiles.Utilization`); a cut on a chalked placement is cleaner (+0.05) and
any chalk a cut goes through is lost.

**Sewing**: the stitches' spacing and spread are as true as the station's tools (÷ tool quality); a practised hand
lays a run of stitches with each click (see *Practice*); and the **thread must suit the material** (`CraftTextiles.ThreadSuits`): heavy leather needs a strong thread (0.6 + 0.2 per mm of
thickness), fine cloth puckers under a far heavier one.

## Fibre and yarn

A **batch of fibre** (`FibreState`, laid on the fibre bench from a Fibre supply: its staple, grease, dirt and short
fibre come from the supply) carries dirt, grease, tangle, alignment, short fibre, neps, felting, water and soap.

* **Clean** (`CleanFibre`): picks a share of the dirt out; fibre pulled with it is lost.
* **Scour** (`ScourFibre`): grease melts out above about 35-62 °C (silk's gum near boiling) with a detergent, over the
  minutes it soaks; wool **felts** when stirred hot or shocked from hot to cold (`felted`, the Felted defect); too few
  rinses leave soap in it; it comes out wet and dries in the air (slower in damp air).
* **Open** (`OpenFibre`): teases the tangle out; torn hard, fibres break (more short fibre); felted fibre hardly opens.
* **Card** (`CardFibre`): aligns the fibres toward two-thirds, opens the last tangles; heavy strokes and clogged cards
  (wet, tangled, felted fibre) roll **neps** and break fibres. Not while it is wet.
* **Comb** (`CombFibre`): long staple only (40 mm and up: not cotton): straightens toward 0.95 and leaves the short
  fibre, neps and dirt in the comb as noil (weight lost). A combed batch spins **worsted**.
* **Spin** (`SpinFibre`, at the wheel): grams of the batch into a bobbin of **yarn** (`YarnState`): its evenness is the
  draft's times the preparation (`CraftFibre.Prep`), its length the fineness of the draft, its strength the fibre's, the
  twist's and the evenness's (+12% worsted, less with short fibre and neps).
* **Ply** (`PlyYarn`): two or three bobbins twisted the other way: the shortest sets the length; evenness rises, the
  strengths add; it is **balanced** when the ply twist is near 0.8 of the singles' twist.
* **Measure** (`MeasureYarn`): a skein wound on the niddy-noddy and counted (1.5 m a wrap): the count may be off.
* **Reel** (the station's ReelYarn): a counted skein becomes thread items, one per 20 m of the count (never more than is
  really there), coarse, even or fine by the yarn's quality (`CraftFibre.Quality`: plied yarn +0.08).
* **Weave** (`WeaveCloth`, at a **Loom**): a bobbin of yarn into a new length of cloth (a textile hide piece: the fibre's
  `weavesInto` - wool cloth, linen, canvas, cotton, silk) `width` cells wide, one row for every beat sent (each row takes
  `CraftFibre.MetresPerCell` = 12 m of yarn per cell; never more rows than the yarn allows, at most 30 a length; what is
  left stays on the bobbin; a practised weaver's beat packs 2 or 3 rows, each as its beat - see *Practice*). Each row's beat (its timing-bar score) sets how smooth it is; a beat below the damage
  threshold skips a pick - a flaw across the row; an uneven warp (the tension) puckers cells, a lumpy yarn shows slubs,
  a beat far from balanced (open or packed) is weaker and stiffer. A fine yarn weaves a thinner cloth. The cloth is laid
  out, cut and sewn like any other.

## The timing bar

`CraftTimingBar` (with `CraftTimingSettings` on the database: **Simulation → Timing bars**) is the precision mechanic
sawing, splitting, faceting and weaving share. An indicator runs along the bar (speed × (0.75 + 0.25 × difficulty)
lengths a second) and turns back at either end; the bar has targets (an ideal point, a size, the best score it gives).
Each target has nested zones - half-widths `perfectZone` 0.02, `excellentZone` 0.055, `goodZone` 0.12, `poorZone` 0.24
of the bar, × the target's size × difficulty^-`narrowing` × (1 + `skillWidening` × practice). A hit's score falls
continuously with its distance from the ideal point: 100% on it, then linearly through `perfectScore` 0.95,
`excellentScore` 0.8, `goodScore` 0.55 and `poorScore` 0.2 at the zones' edges, to 0 a further Poor-zone width beyond
(the grade is the zone it fell in; beyond the Poor zone it is a Miss). Below `damageBelow` (0.25) the hit harms the work.
The hand's tremble moves the hit; the targets' places come from the piece's seed, so the same moments give the same hits.

## Gems

A **gemstone** (`GemState`, `CraftLapidary`) is rough, a preform, faceted or polished. Rough, it has its carats, tone, the
tilt of its optic axis and of its cleavage plane and the trace of that plane, and **inclusions** (crystals, feathers -
cracks -, clouds, needles) placed in its plan and depth, as many and as large as its species' clarity makes likely;
and **crust** - matrix and dirt round its skin, in 16 sectors, thicker on one side.

* **Cleaning** (`CleanGem`): work on each sector of the crust with a brush (slow, never harms) or a steel pick (quick;
  worked on bare stone it scratches - more on a soft stone - and, held carelessly on a brittle one, chips it). How
  clean it was left, less the marks, is its `cleaned`. Crust left on blurs the refractometer and the dichroscope
  (×1 + 3·dirt), makes the balance read light (−0.6·dirt), hides inclusions beneath it from the loupe (a thick crust is
  opaque) and throws the saw (more kerf lost).

* **Tests** (`TestGem`): the loupe (and the cleavage cracks, for a stone that has them); scratch with a hardness point
  (harder than the stone: it is scratched - a surface scratch the polish takes off - and the reading narrows); the
  refractometer (the refractive index, ±0.035 × (1.15 - care) ÷ tool quality, doubled on the rough: a spot reading); the
  hydrostatic balance (specific gravity, less exact under a carat); the dichroscope (pleochroism and the axis' tilt).
  Each needs its instrument at the station.
* **Identification** (`IdentifyGem`, at least two tests): the name given sticks to the stone, right or wrong; named
  rightly, the player knows that gem from then on (the journal).
* **Evaluation** (`EvaluateGem`): every look of the loupe (where, at what focus depth) finds the inclusions near it at
  that depth; the stone's worth is then estimated from what was found only - a careless search over-values it.
* **Orientation** (`OrientGem`): the cut, where the finished stone sits in the rough (a circle in the plan) and the
  table's tilt to the crystal. Inclusions outside the circle are cut away; a pleochroic stone's colour is best with the
  table across its axis (×1 - 0.6·pleochroism·sin²); a table within 8° of a Good or Perfect cleavage flakes when polished.
* **Splitting and sawing** (`CleaveGem`, `SawGem`): a line across the plan. **Cleaving** (a stone with a cleavage):
  the blow is judged on the split's timing bar (`CraftLapidary.SplitBar`: the ideal force 0.42 + 0.25 × toughness,
  zones narrowed by `CraftLapidary.Difficulty` - harder, more brittle stones are less forgiving); within 4° (Perfect),
  2.5° (Good) or 1° (Poor) of the cleavage's trace and above the damage threshold it parts along its plane, losing
  1% + 6% × (1 - score); below Good the face steps (a chip); off the plane or below the damage threshold it shatters (a
  chip, a feather into the stone, 12% lost). Under 0.3 the blow does not part it. **Nick and snap** (`snap`, a stone
  with no cleavage worth following and toughness under 0.65 - quartz, garnet, tourmaline, peridot, emerald, opal):
  never clean - 6% + 15% × (1 - score) lost and chips; below the damage threshold a crack runs in. Tough stones
  without a cleavage (corundum) must be sawn. **Sawing**: its steadiness is the strokes' average score; the kerf and a
  wandering cut cost carats; every **miss** (a stroke below the damage threshold) jumps the blade: 2% more lost and a
  chip; a dry cut heats it (heat-sensitive stones crack); pressing hard on a brittle stone chips it. The far side, when
  big enough (0.15 ct), is a stone of its own. A preform has at most 55% of its rough outside the planned stone.
* **Grinding** (`GrindGem`, the gem bench's wheel or the faceting machine, once oriented): the preform's outline (32
  radii from the planned stone's centre) is ground toward the planned outline - an ellipse as long as the cut, inside
  the window - by touches (angle, amount) at a rate set by the stone's hardness, the tool and the steadiness; each
  touch takes the neighbouring radii too. Carats follow the area. Ground into the planned outline it is `overground`
  (smaller), and its `shapeError` grows with that and with how unevenly the outline came; dry grinding heats a sensitive
  stone. Under 15% left outside the outline it is a preform ready to facet; faceting a ground preform with more left is
  refused. Never ground, the first tier grinds it freehand: `shapeError` 0.25.
* **Faceting** (`FacetGem`, the faceting machine): tier by tier in the cut's order, each at its angle - the pavilion's
  ideal is 40.75° + (2.42 - n) × 2.6 and the crown's 34.5° + (2.42 - n) × 8.5 for refractive index n (diamond 40.75/34.5,
  quartz about 43/42), plus the tier's offset; the index stops' error; how the facets met (short: they do not meet, a
  window of old surface; past: overcut, weight lost); how unevenly the tier's facets came (`asymmetry`) and how many
  were botched (`misses`: 1% weight and a chip each - recut the tier). In the window each facet is a timing-bar hit:
  its offset sets the angle's and the meet's error, its score the index error, the spread of the scores the asymmetry.
  A worn machine adds to the angle's error. Heat-sensitive stones ground dry crack. The first tier takes the preform
  down to the planned stone; after the last the girdle is measured.
* **Polishing** (`PolishGem`): a compound polishes well only stones up to its hardness (tripoli 6, cerium oxide 7.5,
  alumina 9, diamond paste 10); the polish is followed over 9 regions (where the lap worked; evenly if not said): each
  region's shine is 1 - e^(-1.6 × its polish), the stone's polish their mean, a region below 0.6 is a dull patch; past
  2.5 the edges round (meets, a little weight). Heat (pressure × speed) crazes sensitive stones and rounds the facets'
  meets. Polish takes scratches off but never changes a facet's angle, index, asymmetry or misses.
* **Grade** (`CraftLapidary.Grade`): clarity (the inclusions kept), colour (tone × orientation, less for heat),
  integrity (feathers, heat, chips, cleavage), cut (each tier's angle, index, meet, × (1 - 0.5 asymmetry) ×
  (1 - 0.15 per botched facet); the pavilion counts double; × (1 - 0.6 × min(1, 2 × shapeError)) for the outline),
  polish, **brilliance** (a pavilion more than 3° shallow leaks light, more than 3° deep goes dark), carats → quality and
  value (value per carat × carats × (0.15 + 1.7 q²), more for a big stone).

## Jewellery

The jeweller's work is done on metal **wire and strip** (`CraftDefaults.JewelleryForms`: 600 mm × 2.5 × 1.8 wire, 150 mm ×
4 × 0.5 bezel strip) at the jeweller's bench (`CraftJewelry`):

* **Forming** (`FormShank`): a band, a bezel, a prong head (3-8 prongs), a bail or a chain, to an inner size: the metal
  must be thin enough (bezel 1.2 mm, chain 2.5, others 4) and long enough (π × (size + thickness) for a band or bezel);
  how round it came off the mandrel and the gap left at the seam are kept; a hard, brittle metal cracks.
* **The seat** (`SeatSetting`): cut in a bezel or prong head to a stone's girdle: 0.15 mm too small and the stone does
  not go in; up to 0.08 mm wide it is snug; wider, it never holds fully (`SeatFit`); how level and clean it was cut.
* **Soldering** (a Solder joint, `CraftJointWork.Solder`, with solder added to the piece): flux, then heat to the
  solder's flow (0.85-1 of it); a gapped seam does not fill; past 1.12 it overheats (porosity, firescale), and a stone
  already set feels it (heat-sensitive or brittle stones crack).
* **Setting** (BezelSet, ProngSet joints, `CraftJointWork.Push`): pushes, their pressure and how evenly round
  (opposite sides in turn) - security grows up to what the seat allows; uneven pushing and an unlevel seat tilt the
  stone; too hard on a brittle stone chips it. Set when secure (0.6) and, for prongs, every prong pushed.
* **Finishing** (`FinishJewel`): buffed and cleaned, then inspected - a loose or tilted stone, pitted solder, a chip
  are found here. A jewellery design finishes only after it.
* **What it is** (`CraftJewelry.Evaluate`): named for its metal and stone ("Gold solitaire ring with a ruby"), its magic
  defence from the stone's quality and size (and the metal's magic), a resistance of the stone's element, as much of it
  as the stone is secure; durability from the metal and the setting; the price adds the stone's value.

## Assembly

A **design** lists its parts (role, kind, count, the shape or leather or wood it should have, how important it is) and
its joints (fit, peen, rivet, stitch, wrap, wedge, socket, buckle). A part goes in only if it is the right kind, cut to
the right pattern, cool enough near leather and wood, and it physically fits: a guard's slot must clear the tang (too
tight: file it), a haft must go into the eye (a little tight is a good drive; too thick: rasp it). Joints are made by
hand: holes punched (their spacing and alignment), stitches pulled (tension and its evenness; too tight cuts the
leather), rivets peened (too light: loose; too hard: crushed leather, a cracked hard rivet), a tang peened over the
pommel (it must be long enough), a wrap wound, a wedge driven (too far splits the haft). An item finishes when every
required part is in place and every joint made.

Jewellery adds solder joints and bezel and prong settings (see *Jewellery*): a metal part must be formed into the
right part (a band, a bail, a chain; a setting follows its joint with the stone - a bezel or a prong head), and a stone
must be cut and polished, within the design's carats, faceted or a cabochon as the setting takes.

**Where it is made** (`CraftAssembly.MadeAt`): a design is started only at a station with an assembly or leather bench
that has the tools for each required part - jewellery: a jeweller's bench (its metal is wire and strip); metal: an anvil or a crucible; leather: a leather bench; cloth: a tailor's
table or a leather bench; wood: files, a chopping block or a sawhorse. A station can narrow it further to a list of
designs (`CraftStation.designs`). The Assembly area lists only what can be made there, and the station refuses the rest.

## Evaluation

`CraftEvaluator.Item` reads the parts and the joints — nothing else:

* **Material** — the class and properties of the main parts against what their role needs.
* **Workmanship** — how close each part is to its intended shape (profile, length, mass, eye, edge), its finish.
* **Integrity** — what the defects leave of it (cracks, inclusions, porosity, burns, delamination, scores…).
* **Edge** — sharpness, angle, symmetry, chips, burr (weapons and tools).
* **Heat treatment** — hardened where it should be, tempered, not brittle (or not hardened at all).
* **Assembly** — the quality of every joint.
* **Protection** — for armour: thickness, hardness and toughness of the plates, the leather's tannage and stiffness,
  padding, studs; for jewellery, the stone's quality.
* **Fit** — clothes and rings: measured or not, and how well (see *The tailor's*).
* **Seams** — stitches as strong as their thread, and only if the thread suits the material.

They give the quality - raised by the finisher's **finishing touches** (`AssemblyState.mastery`, kept with the item
when it was finished: the quality gains `finishingTouch` × mastery of what it still lacks; *Practice*) - and its tier
(Crude, Poor, Serviceable, Good, Fine, Superior, Masterwork) and the item's numbers:
damage, critical chance and multiplier, knockback, attack speed (from mass and balance), tool damage, element (from
materials with an affinity, or what it was quenched in), durability, defence, magic defence, resistances, weight, price.
A missing part makes an item incomplete (it cannot be finished; asked anyway, it rates very low).

## Practice

Each player's practice in each craft (`CraftMastery`, kept in their journal: chapter 04) is used where the rules run -
the station, or the server in a network session - so a client cannot claim more than it has:

* **Which craft** (`CraftMastery.OfRequest`): from the request and the piece it is on - grinding, filing or polishing
  wood is woodwork, steel smithing; cutting cloth is tailoring, leather leatherwork; the tape is tailoring, the ring
  sizer jewellery; work on an item is the item's craft (`OfDesign`: jewellery for a jewellery design, otherwise the kind
  of part that matters most - metal smithing, wood woodwork, a stone jewellery, cloth tailoring, leather leatherwork).
  The bellows, the fuel, waiting, putting and taking are nobody's craft.
* **The hands** of that request are as steady as that practice (the guard's floor and the window's pace too).
* **More with each action** (`CraftMastery.ByPractice`): a JointWork request names its tool in `i1` - 1 the needle (it
  punches and stitches a run of `StitchesPerAction`: 1, 2 from Journeyman, 3 for a master), 2 the pricking iron and awl
  (the iron punches `HolesPerBeat` = 4 holes a beat, the awl a run of stitches) - and the count is multiplied there
  (the workshop's operation records the stitches, so replays are exact); a WeaveCloth request's beats are each repeated
  `RowsPerBeat` times (at most 30 rows).
* **Routines** (`CraftRequestKind.Routine`, `i0` = the `CraftRoutine`): refused below the rank they need; otherwise
  `CraftMastery.DoRoutine` does the ordinary operation at the quality practice gives (`RoutineQuality` ± 3 % from one
  time to the next): the skein counted a little short (never long), the wearer measured near their size, the fibre
  picked or teased, the crust brushed sector by sector as long as each needs, the stone searched on a grid as fine as
  the practice at every depth.
* **Experience**: each request done teaches its craft (`WorkExperience`: 0.01 a blow or stroke, 0.04 most work, 0.12 a
  cut, a weld, a facet tier, a bend, a seat; 0.25 a bobbin, a pour, a bloom, a length of cloth; nothing done by
  routine), no faster than `workPerMinute`; a finished item `ItemExperience`; a wrong name for a stone a quarter.

## Reproducibility and saving

The same operations at the same steps, with the same rules, tools, ambient and seed, give the same workshop exactly:
`CraftWorkshop.Replay` rebuilds one from its history; `Capture`/`Restore` save and continue a workshop exactly (the state
is JsonUtility-friendly). Stations save their state (without the history) and continue after a restart. Pieces brought
in from elsewhere get new ids when theirs are taken, keeping an assembly's parts attached.
