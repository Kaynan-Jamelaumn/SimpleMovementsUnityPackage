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

**Sanding** smooths the surface toward a floor set by the grit and the direction (across the grain leaves scratches).
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
times how near the twist was to right (`SpinQuality`); a fibre lists what it spins into (`spinsInto`: coarse, even,
fine). **Thread** added to an item carries its strength (`Attach`'s supply strength, kept as `<role>@strength`): the
seams are as strong as the thread (×0.8-1.15 of ordinary thread).

## Assembly

A **design** lists its parts (role, kind, count, the shape or leather or wood it should have, how important it is) and
its joints (fit, peen, rivet, stitch, wrap, wedge, socket, buckle). A part goes in only if it is the right kind, cut to
the right pattern, cool enough near leather and wood, and it physically fits: a guard's slot must clear the tang (too
tight: file it), a haft must go into the eye (a little tight is a good drive; too thick: rasp it). Joints are made by
hand: holes punched (their spacing and alignment), stitches pulled (tension and its evenness; too tight cuts the
leather), rivets peened (too light: loose; too hard: crushed leather, a cracked hard rivet), a tang peened over the
pommel (it must be long enough), a wrap wound, a wedge driven (too far splits the haft). An item finishes when every
required part is in place and every joint made.

**Where it is made** (`CraftAssembly.MadeAt`): a design is started only at a station with an assembly or leather bench
that has the tools for each required part - metal: an anvil or a crucible; leather: a leather bench; cloth: a tailor's
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
  padding, studs.

They give the quality (and its tier: Crude, Poor, Serviceable, Good, Fine, Superior, Masterwork) and the item's numbers:
damage, critical chance and multiplier, knockback, attack speed (from mass and balance), tool damage, element (from
materials with an affinity, or what it was quenched in), durability, defence, magic defence, resistances, weight, price.
A missing part makes an item incomplete (it cannot be finished; asked anyway, it rates very low).

## Reproducibility and saving

The same operations at the same steps, with the same rules, tools, ambient and seed, give the same workshop exactly:
`CraftWorkshop.Replay` rebuilds one from its history; `Capture`/`Restore` save and continue a workshop exactly (the state
is JsonUtility-friendly). Stations save their state (without the history) and continue after a restart. Pieces brought
in from elsewhere get new ids when theirs are taken, keeping an assembly's parts attached.
