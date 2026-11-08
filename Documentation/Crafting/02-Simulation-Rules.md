# 02 — Simulation Rules

Everything below is `Crafting/Core`: plain C# with no MonoBehaviour, run in fixed steps (`stepSeconds`, 0.1 s) whatever
the frame rate. Every number named here is a field of **Simulation Settings** in the Crafting Database.

## The workshop and its pieces

A `CraftWorkshop` holds the pieces and the shared equipment: the forge fire, the furnace (its fire, charge, slag, bloom
and liquid pool), the crucible (its melt, dross, cover, gas, how well mixed), the quench tank (medium, temperature,
volume), the tempering oven, the ash box, the vats, the steam box and the drying stack (and its cover). Each operation
(put a piece down, move it, strike it, grind it, fill a vat…) is recorded with the step it happened at; between
operations the workshop simply runs.

A `CraftPiece` is one of:

| Kind | Its state |
|---|---|
| Metal | its composition (grams per element) and `segments` (24 along its length): length, width, thickness, the two bevels (depth and width each), fuller, bend, dish, hole, slag, porosity, scale, pits, roughness, polish, temper film, coating, edge apex, micro-bevels, burr, chips, clay, flux, how much of a forge weld's seam is still open; and two metallurgical **zones** per segment — the edge and the body — each with its temperature, austenite, martensite, pearlite, fineness, transformation progress, temper progress, grain, cold work, stress, decarburisation, burning |
| Ore | its ore and deposit, fragments (mass, size, mineral share, dirt), what is bound in the mineral, roasting, moisture |
| Hide | a grid of cells (`cellSize` 10 cm): present, thickness, flesh, hair, loosened hair, grain, damage, flaws, moisture, tannage at the surface and in the core, stiffness, oil, wax, dye, smoothness, rot, stretch, shrink, hardness; the frame's tension, the agent it was tanned with, its shrink temperature. It weighs its cells: area × thickness × the leather's density (`CraftPiece.HideDensity`: 0.75 + 0.45 × moisture g/cm³, cloth 0.23 + 0.25 × moisture), up to 40 % more with oil in it; shrinking loses no mass |
| Wood | species and segments (it is shaped like metal: rasped, carved, slotted, sawn), moisture, bark, runout, checks, splits, charring, shade in the stack, its finish; a log in its bark. It weighs its volume × the species' density (seasoned wood's, at 12 %) × (1 + its moisture) / 1.12: a green log in its bark is about half again as heavy as the same log seasoned |
| Fibre | a batch: grams, dirt, grease, tangle, alignment, short fibre, neps, felting, water, soap (*Fibre and yarn*) |
| Yarn | a bobbin: fibre, grams, length, twist, evenness, strength, plies, the count of a skein |
| Gem | a rough or cut stone: carats, crust, inclusions, its axis and cleavage, outline, tiers, polish (*Gems*) |
| Fasteners | kind (rivets, studs, buckles), count, grams each, quality |
| Assembly | its design, which piece is in which place, every joint's state, supplies added |

Each piece also has defects (kind, where, how bad, whether it shows, why; the first time a flaw of a kind the work makes
shows on a piece - scale hammered in, rot, a closed tan, shrinking - a `Defect` event tells it in what can be seen, and
the journal learns its lesson) and a **record** of what was done to it. A
harm done again at the same place makes that defect worse (`CraftPiece.AddDefect`: the worse of the two plus 0.3 of the
lesser); a state the piece goes on being in - rot, an outside closed by too strong a liquor, shrinking, a stretch
burning in the fire, scale hammered in blow after blow, a stretch ground too thin - is told again while it lasts and held
as bad as it is now (`HoldDefect`), never stacked on itself.

## Heat

Each zone's temperature moves toward its surroundings exponentially: in the air by convection and radiation, in the
fire by `fireTransfer` scaled by how deep it sits and how high the flames are, in the oven, in the ashes (very slowly:
annealing), in the quench by the medium's severity. Heat also conducts along the bar (`conduction`) and between edge and
body (`zoneConduction`): a thin edge heats and cools faster than a thick spine.

**The fire** burns fuel (`fuelBurnRate`); its level rises and falls with a lag; its draft (`CraftThermal.Draft`) is a
base draft plus the air from the bellows, which decays (`bellowsDecaySeconds`), all through the vent: shut, the fire
gets no draft at all, the bellows' neither (the 3D bellows' meter shows that draft, `DraftShare`). Each pump adds its puff of air
(`CraftBellowsPump`): a steady rhythm builds the strongest draft and is light work - the arm that strikes rests while the
bar heats, so a welding heat pumped at one or two a second leaves it fresh for the setting blows; pumps closer together
than a quarter of a second (`Frantic`) give little air and tire the arm like any hurried movement. The window and the
3D bellows send at most five pumps a second (the guard's allowance): a quicker pump's air goes with the next. Each fuel has a maximum temperature
(charcoal 1350 °C, coal 1450 °C with sulfur, coke 1550 °C, firewood 950 °C), a reducing character and a smoke. A fuel
with `burnsAnyWood` (firewood) is fed from any wood in the bag when its own item is missing: wooden blanks, and plain
materials named as wood or after a wood (Oak Plank, Ash Log, Timber, Branch) - wood named as firewood (kindling,
offcuts, branches, sticks, twigs) first, then the cheapest by its price (`CraftFuel.BurnOrder`), so a dear blank never
goes on the fire while a cheap one is left. A piece lies in the fire at a **depth**:
at the edge of the coals it drives toward `fireEdge` of the fire's heat, in the heart toward all of it - and in a forge,
deep in front of the tuyere, up to 6 % more (`CraftThermal.HeartBlast`, a constant) while the bellows blow (a blown
charcoal fire brings every stock metal to its welding heat). The depth also sets how fast it heats and how much air
reaches it.

**One face hotter**: while a piece heats in the forge the face down in the coals runs ahead of the face up, the more the
further the piece lags behind the fire (`faceBias`, face 1's lead over face 0's); the lead evens out through the steel
(about 20 s) once it soaks or leaves the fire. Turning it over only changes which face heats next. A face that ran hotter
warps it in the quench, toward the face that ran cooler.

**Flux** lies on the stretches it was sprinkled on (a stretch it missed has none): from a red heat it melts, takes up
the scale under it (`CraftThermal.FluxScaleRate`, 0.02 mm a second for a full coat above 750 °C), keeps new scale off, and slowly
burns away (`CraftThermal.FluxBurnRate`: a full coat lasts some two and a half minutes at heat, however finely the bar is cut).

**Scale** grows in an oxidising fire, parabolically, doubling every `scaleDoublingDegrees` and eating metal
(`scaleMetalLoss`); a reducing fire scales less. Above ~1000 °C steel loses carbon at the surface (decarburisation) in an
oxidising fire and gains a little in a carburising one; a coal fire puts sulfur in.

## Phases, hardening and tempering

What a metal *is* comes from its composition only (`CraftMetallurgy.Analyze`): the base element, the alloy effects of
the others (each a curve over its fraction: tin in copper hardens it up to a peak and embrittles beyond), the class
whose ranges it matches (the highest priority first; of equal priority, the one listed first). Steel is iron with carbon
above `hardenableCarbon` and below `castIronCarbon`; its critical temperature runs from 727 °C (eutectoid) to 912 °C
(pure iron).

* **Austenitising** — above the critical temperature a zone turns to austenite over `austenitizeSeconds`, refining its
  grain once complete; held long and hot, the grain grows again (`grainGrowthRate`).
* **Cooling** — austenite turns to pearlite along a TTT curve (fast near its nose at ~550 °C, `pearliteNoseSeconds`,
  slower with more carbon, nickel, manganese, starmetal — hardenability — and coarser grain), and below the martensite
  start temperature (lower with more carbon) to martensite (Koistinen–Marburger). What is left is retained austenite.
  So: water quenches a high-carbon bar hard (≈ 790 HV), oil only half hardens it, mild steel barely hardens at all,
  an alloy steel hardens even in oil, and steel that was never above its critical temperature does not harden.
* **Stress and cracks** — martensite forming fast builds stress (`quenchStress`): beyond `crackStress` a segment cracks,
  each with its own flaw so cracks start at weak spots and relieve their neighbours. Brine cracks more than water,
  water than oil; thin, high-carbon, agitated pieces crack more. How hard a quenchant pulls heat where the cracks start
  (under 300 °C) is its `CraftThermal.CrackHarshness` (severity × low-temperature factor: water 1, brine 1.6, frostwater
  0.5, oil under 0.2); from `HarshQuench` (0.4) up it cracks a blade of hardening steel quenched bare - a thin edge
  almost always, a full one often (clay on the spine spares a full-thickness blade) - while oil hardens it whole. The
  quench says so before a blade of hardening steel still to harden goes in (`CraftThermal.HarshFor`,
  `CraftLook.HarshQuench`: fill the tank with the oil the station keeps, or clay the spine and leave the edge thick), and
  the editor's presets keep oil beside water and brine (`CraftThermal.PresetQuenchants`).
* **Warp** — an uneven quench bends the piece (`quenchWarp` degrees for the whole piece): moved side to side (it cools one
  face first), held still in a vapour jacket, plunged flat - toward the face up - and one face left hotter in the fire,
  toward the face that ran cooler. Upright, point first, moved up and down along it (or round) it stays true; a
  single-edged blade curves toward its spine (a positive `bend`). A piece has the edges its purpose gives it
  (`CraftPiece.IntendedEdges`: a sword's blade two, an axe's head one), however the purpose came - said at the anvil or
  the carving bench, cast in the part's mould, fitted to an item - and a weld or a fold keeps them; one meant for
  nothing gets an edge with its first bevel on the wheel. Only the part under the surface quenches: going in upright, the far end first, as deep as
  it was let down. The bow the unevenness gives it in one immersion is kept on the piece (`CraftThermal.QuenchWarpTag`;
  a single edge's own curve is no warp): once it passes what the eye sees (`WarpShows`, 3°) the quench says it bows.
* **Tempering** — reheated, martensite tempers by the Hollomon–Jaffe parameter (temperature and time;
  `temperTimeScale` compresses hours into minutes): hardness falls, toughness rises, stress relaxes. 200 °C keeps most of
  the hardness, 450 °C much less. Bright steel shows the tempering heat as oxide colours (straw, bronze, purple, blue;
  the first, pale straw, from `CraftThermal.TemperColourFrom`, 195 °C, is said as it runs).
  The oven is held to its dial (`CraftOvenState.Dial`): off, or 100-650 °C - a tempering oven, never a furnace, whatever
  is asked of it (the workshop and the guard alike).
* **Annealing and normalising** — cooled in the ashes it ends soft and coarse pearlite; in the air, finer and harder.
  A steel left coarse-grained (`CraftEvaluator.CoarseGrain`: over 2 on average, as a long forging heat leaves it when it
  is quenched straight from it) is weak; the item's inspection names it.
* **The magnet** — magnetic steel in the fire passing its Curie heat (770 °C) raises `MagnetLost` (no words: a magnet
  shows it, `CraftLook.Magnet`, and the journal learns it only where the station has one).
* **Burning and melting** — above its burn temperature (lower with more carbon) in the fire steel burns: its grain falls
  apart and it crumbles under the hammer; above its solidus a segment drips away. A casting or a bar cooling in the
  air does not burn.
* **Clay** painted along the spine slows the body's quench (by 1 + 3 × its mm); the edge is left bare and is slowed only
  through the steel: a hard edge on a tough spine, in oil too. A coat painted again replaces the old one stretch by
  stretch (wiped bare, none is left); brushing, grinding or pickling takes it off. Only clay that goes on is taken from
  the bag (`CraftWorkshop.ClayGoesOn`: some stretch coated thicker than it was): wiping it off is free, and the brush
  wipes with no clay in the bag.
* **The ash box** is warmed by the hot metal buried in it (as far as `ashTemperature`): a cold bar warms nothing.
* **The quench tank** can be emptied and filled with another of the station's quenchants (its item used up): fresh, at
  the room's heat.

Hardness and toughness of each zone are computed from its phases, temper, grain, cold work and decarburisation;
non-steel metals harden by cold work only (bronze much more than copper) and soften again above their
recrystallisation temperature.

## Forging

A blow (`CraftForging.Strike`: position, tool, face, force, tilt) reduces the thickness (or width) under the hammer by
`strikeReduction` × force × how plastic the metal is at that temperature × the tool, and puts the volume back into
length or width: the flat face spreads, the cross peen draws it long, the straight peen widens it, the fuller grooves
it, the horn bends it, the hardy cuts it, the drift opens an eye, the flatter smooths it, the dishing stake cups a
plate, a blow tilted on purpose makes a bevel (a face held within `CraftForging.FlatTilt`, 0.15 of a full tilt, of level strikes
flat: the hands' own tremble does not bevel - nor does a hand that drifts a little as it holds the hammer up: the tilt
counts only past a few pixels of where it pressed, `CraftForging.TiltFrom`, the window's 8 of its 400 and 2 % of the
screen's height at the 3D anvil; past it the bevel grows with the tilt, and on a single-edged piece it
curves the blade by its share of the length - toward the spine, as the stretched edge grows longer than it: a positive
`bend`, the way a sabre curves and the quench curves a single edge). The horn bends the stretch it bears on by its share of the contact, the
way the hammer's tilt says - about 17° for a full hot blow however finely the bar is cut. The hardy (from below) and
the hot chisel (from above) cut the same way, blow by blow, a quarter as fast cold and hard; a half-made cut stays where
it is when the bar is reworked, and the last stretch can be cut off too. The cut keeps which way it was driven
(`MetalSegment.cutFromAbove`) and the record names the tool (*Cut with the hot chisel*, *Cut on the hardy*). The drift's first blow goes in as deep as it is
driven (a tap only marks it). Volume is conserved (only scale is lost). The anvil chills the work (`anvilChill`); the
blow itself warms it a little. A double-edged blade's bevels meet in the middle at the most: ground on, its edges go
back (it gets narrower, never weightless).

* Hot work above the recrystallisation temperature refines the grain; cold work hardens (and eventually cracks) it.
* Forged far below its working heat, steel tears; hardened steel struck cold hardly moves and cracks; cast iron
  shatters; red-short iron (sulfur) tears at forging heat, cold-short iron (phosphorus) when cold; burnt steel crumbles.
  The crack's event carries its cause (`Cracked`, id `CraftForging.CrackId` + burnt, castiron, bloom, sulfur - or
  hotshort, by another element - phosphorus or coldshort, cold, overworked; a steel cracking as it hardens, in the tank
  or out of it, `hardening`), named after the element that makes the metal short (`CraftMetallurgy.MainEffect`), so
  the journal learns the lesson of the real cause.
  A soft metal (copper, brass, silver, gold: `CraftForging.ColdPlasticity` half or more) still moves cold - over half as
  much as at forging heat - and hardens with the work until it is annealed. How a stretch gives at its heat is
  `CraftForging.Gives`; the anvil's words (*it barely moves*, *steel tears*, *it is stiffening: anneal it*) follow it
  (`CraftForging.BlowFeel`, the window's and the 3D anvil's alike).
* Wood is only straightened on the anvil (*Straighten*: every other tool is refused before the hammer rises), as far as
  it gives (`CraftForging.WoodGive`): steamed through and still hot, like hot iron; cold, it mostly springs back.
* Thick scale is hammered into the surface as pits (said the first time it pits the piece: a `Defect` event,
  `CraftWorkshop.Flaw`). Upsetting a thin flat bar on its edge folds it (a cold shut).
* A bloom struck at welding heat closes up and squeezes out its slag; struck cold, it falls apart. The pores that close
  take their room with them (`CraftForging.Compact`: the stretch sinks under the hammer by it), so the metal in it stays
  what it was and only the slag driven out and the scale the fire burns leave: a bloom hammered sound ends smaller and
  lighter than it came from the furnace (a porous casting struck hot the same). Once no stretch of it is more porous than
  2 % (`SoundPores`) and no more than 2 % of it is slag (`SoundSlag`) it is **consolidated**
  (`CraftForging.Consolidated`): a bar of bloom iron, no longer a bloom (`CraftPiece.bloom` cleared, the furnace's slag
  no longer a flaw, said once: *consolidated*) - its look and its name follow (`CraftLook.Bloom`; a piece is named by
  its purpose first, a blade forged from a bloom is the blade).
* **Folding** and **forge welding** need welding heat (`weldingMargin` below the burn temperature) and flux. By hand the
  fold (or the second bar stacked on, about as long: 20 % at most between them) is laid at forging heat at least (its
  mean heat) and its **seam left open**; then each blow of the flat face (the flatter too, the cross peen a little; on
  its edge none) closes the seam where it lands by its force × how weldable that stretch is at that moment:
  `CraftForging.SegmentWeldQuality` = welding heat (from 150 °C below it) × (½ + ½ flux) × clean faces (no scale left at 0.25 mm). The
  bar can go back into the fire between heats (flux it again: the fold squeezes the flux thin). A stretch whose seam
  cools 300 °C below forging heat still open is a **delamination** for good (1.5 × how open it was) - and a seam never
  fluxed (no flux since the fold or stack before it: `CraftForging.FluxedSinceSeam`) teaches that a weld wants flux. A
  test station's quick fold (and a script) still welds at once, judged as the bar is then. Folding mixes and loses a
  little carbon.
* **Cutting** with a hot chisel splits a piece in two (mass conserved). **Fasteners** are cut and headed from a small rod
  (no more than 800 g and 14 mm, not a flat bar), or a buckle bent over the horn - the whole rod; never from a blade or a
  piece meant for another part, and not without metal enough for one.
* **The forging window** (`HotWorkMin`-`HotWorkMax`) ends 40 °C short of where the metal burns at the most, alloys too.

The sound of a blow tells what happened: a clear ring (cold, sound), a dull thud (hot), a flat dead sound (a crack).

## Surface work and sharpening

Abrasion (`CraftSurface`) takes metal off where it is applied, at a rate set by the abrasive's cut, the pressure, the
time and the metal's hardness. **The bevels are planes** (`GrindBevel`): a bevel grows at the angle held (its angle is
the area-weighted angle of all the grinding) until it meets the other face's; ground on, its plane moves in and the apex
slides down the other bevel's plane toward the face being ground - the other bevel shortens and the edge goes back, the
section losing exactly what was ground; ground in turn, the apex stays in the middle; once the other face has no bevel
left (a chisel grind), the edge goes straight back. A double-edged blade's bevels are ground on both edges at once and
reach the middle of the width at most. No bevel is ground steeper than 35° a side (`MaxBevelAngle`), the steepest the
stone is held. The faces come down without skewing the edge (each bevel keeps its angle and loses the depth its face
lost; a bevel ground away lets its face reach the apex); the edge line takes the edge back to a flat, and its honing,
micro-bevel and burr with it; the spine, the tang and the eye are shaped. A wheel or a file that cannot bite anywhere it
is held (a file on hardened steel) is refused and noted (*file_skate*), and so is a file on an eye that is not there. A
rasp or a knife works wood (it takes the bark off where it cuts - `exp(-depth / 1.5)` of it stays where a stroke takes
*depth* mm - and the sanding papers start again after it: the coarsest first); a wheel would burn it; polishing and
oiling wood belong to the finishing bench. The window and the 3D wheel send what the tool did every fifth of a second:
the time it was held to the blade, shared out over the stretch it went along (`CraftStroke.Dwell`: held still, one
stretch has all of it), so sweeping along the blade spreads the work and staying takes more off one place.

**The strip** (the Grind area's, `CraftSurface.Judge`) judges each stretch against its design as the evaluator does: an
edge is done once its bevels have met (`EdgeMet`, 0.3 mm left) in the middle at the angle the design's section carries
there and no wider or thicker than the design (4 %); until they meet it is still to grind - however much narrower or
thinner than the design the hammer left the stretch, for it wants its edge all the same (`Judgement.under` tells the
undersize apart: the strip's orange line). With nothing left to grind, a stretch a little short of the design (or met at
another angle) is off, more than 12 % short too far: grinding cannot put metal back, and the words say so without
claiming it was ground.

**Heat.** A wheel heats where it works (`grindHeat`): the bevels and the edge line the thin edge most, the spine and the
tang the body, the faces both, each by its thickness. On a hardened edge the heat draws the temper: `TemperRunsAt` is the
heat at which a few seconds draw an edge past where it was tempered (straight from the quench, past a blade's temper,
`IdealTemper`) - about 290 °C for an untempered edge, more for one already tempered hotter; going past it on the wheel is
noted (*temper_drawn*: the *grind_heat* insight); unhardened steel, iron and bronze have no temper to lose. The wheel
cannot hold a piece above 400 °C, a file above 350 °C, the stone above 150 °C. Bare metal fresh from an abrasive is never
duller than the wheel's grey (`GroundPolish`, 0.3) and takes the colours of heat from there; the film follows the
hottest zone.

**The surface.** Every abrasive scratches what it works toward its own finish, and the polish is one rule for all of
them, once the scale is off, read on the eye's own log scale (`PolishOf`: 0.3 at the least - the coarse wheel's grey -
0.4 off the fine wheel, 0.5 and 0.7 off the emeries, 1 off the rouge's 0.2 µm): a coarse wheel greys a mirror, each grit
finer leaves it brighter, and a brighter surface keeps rust off better. Its words are one ladder for the Grind area and
what a piece looks like (`FinishOf`: rough from 40 µm, coarse scratches from 15, fine scratches from 4, a smooth grey
finish from 1.5, bright from 0.5, a mirror below). A grit clears scratches up to six times its finish at its pace
(`GritReach`); deeper ones (a grit skipped) come out at a fifteenth of it, and that is noted (*grits*); the Grind area
calls a grit done (`GritDone`) once its scratches are within 1.5 times its finish and within the next finer grit's
reach, so grit by grit nothing reads as skipped. Brushes and scrapers take scale off (but mend nothing; descaling is
theirs - the wheel grinds scale off with its heat), and worked on past it scratch the bare metal. An oil or wax coat
protects until abrasion takes it with the surface or heat burns it off (from 250 °C, `CoatingBurns`): coat last.

**Sharpening** (`Sharpen`: face, stone, angle, pressure, strokes, wobble, edge leading) hones only an edge: a stretch with
less than 0.6 of its thickness left at the edge (`EdgeGround`, the evaluator's own test; a square bar is refused). While a
flat thicker than 0.15 mm still shows (`FlatGone`), the stone first wears it away, slowly - it cuts the face's bevel at the
angle held by `StoneBite` (0.06 mm² a stroke for a coarse stone at full pressure on soft metal, finer stones less, the
strop nothing), so a flat of f mm costs strokes as f² - and nothing is honed to an apex meanwhile. What the stone says
next keeps to the grind strip's threshold (`NextHone`): a flat thicker than where the strip calls the bevels met
(`EdgeMet`, 0.3 mm) is back to the wheel (`WearFlat`); the hair of flat the wheel leaves where the strip is green the
strokes wear away first, a minute or so on a medium stone, and then the faces hone (`WearLastFlat`). The stone reaches the
apex held at the bevel's angle or steeper (`Contact`: all of it from a degree under, nothing 4° under; lower, it only rubs
the bevel's shoulder - noted, *stone_angle*); the micro-bevel follows the angle held, never finer than the bevel under it.
Each face is honed toward the apex; once a face meets it (hone 1: its bar full - strokes past it hone no further),
edge-leading strokes roll a burr over; trailing strokes take the burr off the more the lighter they are (`BurrOff`: none
from 0.6 pressure, fastest from 0.2 down), the strop at any pressure. The strop (`IsStrop`) is drawn spine first, edge
trailing: edge leading, the edge slices into its leather - it hones nothing, takes no burr off and dulls a little (noted:
*strop_lead*); nothing chips on it. With both faces honed the apex closes toward the floor the stone reaches (`SharpenFloor`: the stone's own
apex, coarser for a wobbling hand and for a soft metal - and a stone coarser than the edge only coarsens it). Heavy
edge-leading strokes chip a hard, thin edge. The edge's quality is its sharpness, its angle against the design's - as
the design's section carries it along the edge (`CraftSurface.EdgeHalfAngle`: steeper toward a point, or on a blade too
narrow for the design's angle; the Grind area's strip and the stone ask for the same) - (an obtuse edge cuts poorly; a
very acute one is fragile), its symmetry, chips and burr - the sharpest edge is not always the best. The strokes are what the hand did (`CraftStroke`): a pass is a run along the stone of at least a fifth of it
(`DefaultPassLength`; a stone may ask for more) that turns back - the hand coming back 1 % of the stone from the furthest
point of the run - so a tiny quick wiggle is no stroke; no more than three passes a second of stroking count
(`PassesPerSecond`, and one to start with), and each pass keeps its own angle, wobble, pressure and whether the edge led
(`PassRuns`): each is its own request. The strokes on one stone are one line in the piece's record. Stroking tires the
arm by the time it works (a movement every 0.1 s), whatever the frame rate.

**Cooling dips.** A piece going into the quench tank with austenite the quench still turns to martensite is quenched
(*Quenched in water*: it hardens, and lifted out, the journal learns how the quench went) - more than 2 % of a zone
(`HardeningShare`), counted as the transformation turns it, down to the tank's heat and past what the zone's last
quench already reached: fresh austenite from a hardening heat, or a quench cut short going back in. One without is only
dipped to cool it (*Dipped in water to cool*: nothing is learnt about quenching) - a hardened blade the wheel heated
too, however hot: the few hundredths of austenite its quench left stay as they are. A quenchant lends its element only
to the martensite that formed in it, in that immersion (`CraftThermal.Hardening`, the piece tags `quench.hardening` and
`quench.martensite`). The Grind area's dip (and the 3D wheel's) is for cooling what the wheel heated: it refuses a
piece that can still harden (`CraftSurface.DipQuenches`) - that is quenched at the tank, or left to cool in the air.

## Ores and smelting

An ore lump comes from a **deposit**: its richness, rock and impurities follow from the deposit's number and the lump's
(hashed, so the same deposit always gives the same ore, and no two lumps are quite alike). **Crushing** breaks fragments
smaller (liberating ore from rock — and powdering it when struck too hard): a blow of force *a* on an ore of hardness *h*
(*k* = *a* / (0.4 + *h*/10)) splits a stone in two from *k* = 0.25, in three above 0.7, in four above 1.2, and below that
only chips it; a lump breaks into 48 stones at the most, after which a blow only chips. Throwing out barren fragments removes
rock (the last stone too: then nothing is left of the lump); **washing** carries dirt and light rock away by density
(cassiterite and gold stay in the pan) — a jerky hand loses ore with the mud, and the flow sets how sharply heavy is
parted from light (`CraftOres.Separation`: a strong flow is quick but carries ore off with the rock, a gentle one is slow
and keeps it); **sieving** separates the fines (smaller than the mesh × (0.55 + 0.45 × thoroughness)); **brushing**
(`CraftOres.Brush`, one stone at a time, dry) takes the dirt off what the brush went over — a soft brush loose dirt (a third of caked clay), a stiff one dirt and clay, a wire brush the dirt and the weathered rind of rock at the cost of a little mineral (more of a soft one, hardness under 4). The rind is the lump's outside (`OreFragment.RindShare`, the share of its rock: a lump comes out of the seam with `Rind(size)` of it, the larger share of a small lump) — the stones a blow breaks it into share what is left of it with their rock, and the faces the blow opened are fresh rock with no rind of their own, so crushing and brushing over and over never takes more rock than the rind the lump came with. Each brush goes over each part of a stone once: the stone keeps how much of its surface each brush has gone over (`OreFragment.Brushed`), so a stone gone over in many strokes ends as one sweep over the same surface leaves it, and a brush takes only its share however long it scrubs — the soft brush never more than a third of caked clay, the wire brush the rind once, not the rock under it; a brush that has been all over a stone is refused (*"it takes no more off it"*), and the fresh faces of a break start unbrushed (what the brushes left on the old faces can be gone over again). Nothing is lost to the water. What is bound in the mineral (phosphorus in bog iron, sulfur in sulfides)
does not wash out. Ore is worked only on the bench: not while it is part of the furnace's charge (it is not fished out
again) or in the crucible, nor while it is too hot to handle — ore lifted out of the crucible cools in the air (a minute
or so), and cannot be carried off above 120 °C. A request that names a stone by its place carries the lump's layout
(`CraftOres.Layout`: its crushings and stones); if the stones moved meanwhile (a blow, a stone thrown out, a sieving) it is
refused rather than striking the wrong stone.

In the **furnace** (`CraftSmelting`): sulfide ores roast first (roasted 0.7 or more smelts clean; less gives a matte);
above their reduction temperature, with charcoal (a reducing, carbon-rich fire), the mineral gives up its metal. Iron
that does not melt gathers as a spongy **bloom** (so does the green nickel ore's nickel, unless the furnace runs past
its 1455 °C), picking up carbon (more, the hotter it runs) and trapping slag; metals that melt (copper, lead, silver,
tin) run to a **liquid pool**; if the bloom gets hotter than its own solidus it melts into cast iron. A metal that boils
below the heat its ore gives it up at (zinc from calamine) goes up the shaft as **fumes** (`Furnace.fumes`) — calamine
leaves only its little iron in the bloom; zinc is won by cementation in the crucible — and what boils at the furnace's
heat leaves the pool too. The rock becomes slag — and silica takes iron with it into the slag (`ironPerSilica`,
`gangueReactivity`) unless lime (limestone flux) takes its place. Phosphorus goes with the iron less when there is lime.
Slag builds up and chokes the draft (`slagCapacity`; past 60 % of it): tap it (it must be 1120 °C or hotter to run).
Fine ore packs the charge and chokes it too (`CraftSmelting.Choke`: a charge all fines as much as slag at 120 %): sieve
the fines off and charge lumps.
Tapping leaves the matte, which lies under the slag. The fire's flame: below 0.35 of `CraftFireState.Oxidizing` it is
reducing (metal comes), from 0.35 sulfides roast, above 0.45 lead goes into a cupel and a matte's sulfur burns (in
minutes: it falls off in proportion, like a piece's). Lead on a bone-ash cupel in a strong hot draft oxidises away and
leaves its silver. Mythril ore needs the arcane flux. Only furnace fluxes go into the furnace (a supply whose Flux Use
includes Furnace, a cupel, or a flux an ore needs): borax and sand are refused there, and a flux waiting for its ore is
not counted as metal (`PoolMass` is the metal alone).

Metal goes back into the furnace too: a matte, or lead carrying silver, cast and brought back (fasteners are melted in the
crucible instead). It roasts in an oxidising fire and melts down into the pool — whole, its sulfur into the matte, its
slag into the slag — and comes back out with the tongs if it has not melted. The pool is run into a mould (`TakeMelt`)
only while the furnace is above its liquidus (a frozen pool is refused), and from 20 g; less — the silver left on the
cupel — is lifted out with the tongs as a **bead** (`LiftBead`: all the metal there, with no more of the matte's sulfur
than a sulfide of it holds). A bloom is pulled from 50 g of iron; less waits for more.

## Crucible and casting

The crucible sits in the hotter of the forge and the furnace (`crucibleLag`). Pieces melt at their own solidus and join
the melt, which mixes over time and with stirring: a piece melted into an empty crucible, or one of the melt's own metal,
leaves it one even melt; another metal runs through it in streaks - the lesser of the two through the greater
(`CraftSmelting.MixedAfter`: twelve parts of tin into eighty-eight of copper leave it 0.88 mixed, half and half 0.5) -
until time and the stirring rod even it. A melt poured unmixed casts **streaky** (`CraftPiece.inhomogeneity`, 1 - how
mixed): it is weaker where the streaks meet - soundness and toughness down by up to 30 % (`StreakWeakness`) - and its
look, the pour's verdict and the item's notes say so past 0.3; a fold halves the streaks (drawn out thin and laid
together, as a bloom's carbon evens). The liquid oxidises into dross (less under a borax cover), boils off
what boils (zinc: pure, or as the melt's main metal, freely; dissolved in another metal at its share × 0.2 of that rate —
a covered brass held just above its liquidus loses about a percent of its zinc a minute, three times that bare), and takes up gas when far too
hot (net from about 145 °C over the liquidus without a cover); held just above its liquidus it lets the gas go, faster
under a cover, and frozen it lets it go in a minute or so. While solid metal still lies in it (a piece put in that has
not melted yet: the copper, long after the tin put in with it has run) the charge chills the melt and it takes no gas up
(`CraftSmelting.ChargeUnmelted`, `CrucibleGasChange`; its look says so), so copper and tin melted together cast as sound
as bronze from bars. A melt holding more than `PoreGas` (0.06) casts with pores
however it is poured, and its look says so (fine bubbles rising; above 0.3 it spits). One whole sprinkle of borax covers
the whole melt (a sprinkle over part of it, that part). Zinc ore heated with molten copper under a cover gives
its zinc to the copper (cementation: brass). Only an ore whose metal melts at a crucible's heat (below 1250 °C: copper,
tin, lead, silver and gold ores, native ones too) or boils off into a copper melt (calamine) goes in — never iron ore, nor
mythril ore, nor a sulfide (it roasts and smelts in the furnace, and ore does not come back out of it); its rock and impurities float on the melt as **slag** (`Crucible.slag`), which a
skim takes without metal; the oxide dross takes a little metal with it (less with care, never more than 6 % of the melt).
Stirring and skimming need a liquid melt, a cover a melt to cover; the crust of slag and dross left in a crucible poured
empty is scraped out whole with the skimmer (there is no metal under it to lose). A melt that cannot be poured is let freeze and
**knocked out** as a cake (a Pour with `flag`: all its metal, its dross and slag in it). What the alloy is follows from
the proportions in the melt.

**Pouring** (rate, steadiness, interruptions) makes the casting: poured below the liquidus it will not run (the pour is
checked before the hands start, `CanPour`); too slowly (under 0.35) or too cold (a fill of `Fill` under 0.85: less than
about 30 °C over the liquidus for bronze) it freezes short (misrun); too hot (`porositySuperheat`), gassy or too fast
(over 0.8) it traps gas (porosity — its cause names the larger: the gas the melt held, the heat, the churning stream); a
stream that stops short of a full mould and starts again leaves a cold shut (one unbroken stream stopped early leaves it
only short); dross and slag poured in leave inclusions. A casting is coarse-grained and rough from
the mould. The metal fills the cavity from the mouth, whole stretches while it lasts and part of the next (1 mm at the
least), and stops sooner where it freezes: a casting never holds more metal than the pour brought (`filled`, thousandths
of the mould) nor than the melt has — a pour stopped early or a melt too small is short (misrun, *the pour stopped before
the mould was full*, *too little metal to fill the mould*), and nothing sets from a trickle under a millimetre (the metal
goes back into the melt). A fastener mould's cavities fill one after another off the runner, as a shaped mould fills
along its length: a fluid melt fills every cavity the metal brought, a sluggish one only as far as it runs before it freezes
(`Reach(fill)`), and the rest of the metal goes back into the melt. Metal that missed the mould's
mouth or ran over it is lost from the crucible: as measured (`lost`, thousandths of a mould), or for older pours per gram
cast (`spilled`, `overflow`). The pouring cup takes the last of the stream past full (0.05 of a mould); poured on, it runs
over the top and leaves flash (above 0.02 of run-over: a rough surface to file off). The furnace's metal pours by the same
rules (`TakeMelt`, with its breaks): its pool holds no gas of its own (its pores come from the heat and the stream) and is one
metal; what runs in with it is the furnace's slag (tap it before running the metal). What came of a pour is told in the
workshop's own terms by `CraftSmelting.PourVerdict` (the window's and the 3D pour's words alike).

How far out the stream reaches from the lip (`CraftPourControl.Reach`) follows the tilt as the hand holds it - a stream
building up falls at once where it will run, one eased back draws in as it slows - and once the hand lets go the crucible
tips back with its lip still over the cup: a stream held in the cup and tipped back as soon as the mould is full loses
nothing on the floor.

Besides the stock moulds (`Molds`), every design's metal part with a shape profile has a **pattern mould**
(`CraftRules.PatternMold("cast:design/role")`, all of them in `AllMolds()`): the part's profile at the middle of its
length range, with its eye. What is cast in it is meant for that part (`intentDesign`/`intentRole`), so its target
shows at the anvil and the wheel. It comes out blunt and rough: grind the edge, file the flash. `CraftSmelting.MoldMass`
says how much metal a mould takes.

## Leather

A hide's cells are worked by strokes (`CraftLeather`; each tool works a band round its line: the knives 0.7 of a cell,
the slicker and the dye brush 0.8, the staking blade 0.9, the oil rag 1 - `ScrapeReach`... `OilReach`): **fleshing** takes
flesh and fat off; where a cell is clean (flesh under 0.12) or thin (under 2 mm) the knife scores it when pressed harder
than `SafeFleshingPressure` = 0.55 × thickness / 3 + 0.175 - 0.3 × (angle - 0.7): a steep knife bites sooner (and takes
more flesh), a flat one is gentler and slower. **Liming** loosens the hair over hours (warmth speeds it, `LimeWarmth`, but
no more than twice, about 30 °C; too long and the grain goes spongy), and the grain side is then scraped (pressed harder
than 0.5 the knife scuffs the grain, and at any pressure where hair the lime has loosened less than 0.3 holds -
`HairHolds`; bare grain, a drake's or one scraped clean, only when pressed hard). Leather tanned with its **hair on** is
worth less: `HairLeft` is 0 once the mean hair is under 0.1 (`UnhairedBelow`, the path's and the guide's line) up to 1
with all the animal grew, and the part's workmanship is × (1 - 0.5 × it) (*the hair was left on*, *tufts of hair were
left on it*); hair keeps the slicker off the grain (the smoothing × (1 - hair)) and much of the dye (× (1 - 0.7 × hair)).
In a **tanning vat** the agent tans the
surface first and the core over time (by its strength, the vat's temperature, stirring, the hide's thickness); too strong
too early closes the surface of a cell whose core is tanned under 0.4 (`CaseHardensBelow`) — the core stays raw (case
hardening); past that a strong liquor only tans it faster. Alum tawing washes out in water (the journal is told while
it does: the *alum* insight); oil tanning makes it
supple; the alchemical essence tans in hours. Hot water (from 55 °C, `HardensFrom`) hardens tanned leather (a little:
cuir bouilli; too much ruins it); in **any** liquor, past the hide's shrink temperature (60 °C raw, the agent's once
tanned) it shrinks fast and a raw hide cooks. An agent poured into a vat of plain water makes its liquor there. A stir
mixes the liquor as well as the paddle's circles were steady (0.5 + 0.5 × steadiness; never leaving it less mixed than it
was) and settles in about three hours of vat time. A vat holding one liquor takes no other agent (`CanFillVat`; water
thins it, an agent poured into plain water makes its liquor). Vats deplete as they work. Soaked again, dried leather is wet again (it dries, and is recorded drying, once
more). Process time runs faster than real time (`processHoursPerSecond`: a tanning hour is a few seconds of workshop
time).

**Drying** (the rack, the frame, the bench) is faster warm, ventilated and in dry air — the workshop's humidity, read as
wood and fibre read it (0.5 where the station has no weather; `dryingHours` is the time in still room air at that
humidity). Leather stiffens as it dries through the damp stage (below 55% wet) unless it is **staked** then: staking
works best between 55% and 15% wet (`StakeWet`, `StakeDry`), little wetter (it stiffens again) or drier - and staked dry,
bark-tanned grain cracks (`CraftLeather.CracksStakedDry`). Oil softens leather that dried stiff and does it no harm (it
keeps dye out: dye first), so the guide and the tannery's path send a bark-tanned hide that dried stiff to be oiled
supple at the beam; leather of any other tannage is staked dry (it softens slowly). A hide is dry once every cell is at 16% or less (`DryAt`); dried at 0.6 stiff or more on
average and not hardened on purpose (`CraftLeather.DriedStiff`, `DriesStiffFrom`) its `Dried` event says *It has dried
stiff.* (id `stiff`: the *stiff_leather* insight). Dye brushed where oil or wax lies past 0.25 (`BlotchesFrom`) beads and
takes in blotches, and the stroke says so when it lay over more oiled than bare leather. Unstretched - on the rack, on the
bench or laced slack - it **puckers** as it shrinks (a mean drying shrink kept as the `puckered` tag: the leather's
workmanship × (1 - 1.5 × min(0.2, puckered)), *dried puckered*). In the **frame** each side is pulled as a share of what
its edge can take (`EdgeLimit`: the weakest present edge cell, fibre × (0.4 + 0.15 × thickness) × (1 - 0.8 × damage) ×
(1 - 0.5 × rot); a rabbit's rim about 0.25, a deer's 0.45, a cow's 0.8, a troll's past 1): pulled to at least half of it
on every side (`FlatFrom`) it dries flat - only as flat as its slackest side holds it (`Flatness`: one side slack and it
puckers however hard the others pull); past 0.85 (`OverdrawnFrom`) that edge thins as it dries; past the whole of it the
edge tears - by as much as the pull goes past both what a cell can take and what the side already held, so a side sent
again unchanged never tears again. Opposite sides pulled unevenly (`Unevenness` = |N - S| + |E - W| in those shares) by
more than 0.3 dry **out of square** (the `distortion` tag: workmanship × (1 - 0.5 × distortion)). A hide laced in afresh
starts slack; the frame holds one hide, the rack four side by side (`RackHides`). A raw hide (tanned no more than 0.3)
left wet rots - in the air or in plain water (`Rots`), at its warmth (`RotWarmth`: twice as fast every 10 °C, the
liquor's own in a vat, up to 40 °C; hotter it cooks); lime, a tanning liquor or a pickle at least 0.05 strong keeps it
(`Preserved`). A cell rotting past 0.4 spoils (`SpoilsFrom`: its hair slips, its grain and skin go, and the hide carries
*Rot* as rotten as it is).

**Oil and wax** make it supple and water-resistant; **dye** goes on evenly only on unoiled, unwaxed leather (dye first)
and gives what is cut from it its colour (an item is its main part's colour, `CraftLeather.Colour`: the skin browning to
the tan's colour, a cloth's own, the dye as far as it took, the hair left on over it; a dye less even than 0.7 is noted,
*dyed patchily*);
oil, wax and dye are used by what the leather takes in (`Uptake`, in cell-loads, by the very rules that apply them): the
station takes them from the pot (flask, cake) the player has open, opening a new item whenever it runs dry - one covers
`cellsPerItem` cells (12). A player has one open pot of each supply at a station, whichever bench draws from it
(`CraftOpenPots`): the beeswax opened to wax a hide waxes a bow stave at the finishing bench too. **Slicking** smooths
and seals the grain (water resistance + 0.15 × the slicked grain; workmanship × (0.96 + 0.04 × it)) and burnishes a cut
part's edges by the strokes that run along them. **Skiving** thins it - one straight pass takes off a cell at most 1.1
× its depth (`SkivePass`), going back over a place in the same stroke up to twice - and holes a cell it takes under 0.6
mm (`SkivedThrough`). Knife scores
and skived-through holes are recorded on the cells where they were made, like the frame's tears; the scars a hide is
born with are kept apart (`HideCell.scar`). **Cutting** a pattern takes its cells out of the hide - and with them the
flaws, scores, holes and tears there (the part's own cells); of the hide's whole-hide flaws only what it all went
through (rot, a closed tan, shrinking), and how it dried (puckered, out of square) - and a careless cut leaves ragged
edges. The knife follows the pattern's true outline, the cells' own edges (`CraftPattern.Outline`, drawn by
`CraftArt.PatternOutline`, the same line the tailor's shears follow): the line is cut only where the moving blade runs
past it (points along every side at most the blade's touch apart - 10 px, never past 0.45 of a cell - so a dab cuts only
where it lands), the cut's cleanness is how close the blade kept to the line, and from nine-tenths round the part can be
cut out, the rest torn (each hundredth left costs 2.5 % of the cleanness); a cut is never cleaner than the hands'
practice (1 - 4 × the hands' floor: a novice 0.90, a master 0.96), and one along the pattern chalked at the very place
comes out 0.05 cleaner (`ChalkedCut`). A part's edges are cut clean past 0.85 (`CleanCut`), a little ragged down to 0.6
(`RaggedCut`), ragged under it - the table's words and the look alike. A part already cut is not cut again
(`CraftWorkshop.AlreadyCut`). An item names a leather or cloth part outside its spec (*Sole: too thin (1.8 mm, wants
5-9)*, *too stiff*, *dried puckered*).

**Finishing** - staking, slicking, skiving, oiling and dyeing - is done at the tannery's fleshing beam or at a leather
bench (`CraftWorkshop.FinishingTools`): a tannery finishes its own hides; a leatherworks shows the same tools under
**Finish leather**.

## Carving wood

Wood (a grip, a haft, scales) is shaped with a knife on the bench (`CraftSurface.Carve`, needs the Files tool): a cut
takes off its depth (scaled by the steadiness of the stroke: 70 % for a shaky hand, all of it for a steady one) across
the stretch drawn — off the **width**, the **thickness**, the corners (**round**: an oval section), or a **bore** for the
tang (at most 70 % of the smaller side). Wood is never cut below 3 mm. Forcing it (a cut deeper than
`CraftWoodwork.CarveSplitDepth`, 1.4 mm, in one stroke as the workshop takes it — the window's strokes at its careful
pace) or a wavering stroke (steadiness under `CarveSplitSteady`, 35 %) splits it (a crack along the grain, which counts
against the part), the more readily the more the wood **cleaves** (its species' `cleave`). A stroke's steadiness is how
far it kept to its own line (`CraftWoodwork.StrokeWander`: the farthest of its samples from the straight line that fits
them, whichever way it runs, against how far a stroke may stray - `StrokeSteady`, times what the hands' tremble leaves),
in the window and at the 3D bench alike - a straight stroke down a sloping taper is steady. The knife takes the bark off
where it cuts (each stretch's bark × e^(−depth/1.5 mm)), and a fresh cut starts the sanding grits again. A blank is sawn
to length with the woodshop's saw and its rule (`SawCut`, below). The design's part says what shape it should end up —
the window draws that shape (its profile of widths and thicknesses, its length range), and the 3D bench shows it. The
slot of a part fitted on a tang must clear the tang (`CraftWoodwork.TangSlot`: the blade's real tang where it is in the
workshop, else the design's); the bit is turned by circles of the hand, counted round their own middle wherever they are
drawn (`CraftHandCircles`: a lap is a turn, a path along a line none), each turn as steady as the circles were even
(uneven circles split it, as a wavering stroke does); each turn of the bit bores a stretch deeper than the slot already goes in the piece
(`CraftWoodwork.BoreSpan`, read from the piece, so the window and the bench go on from each other's slot; turns sent and
not yet answered reach past it), up to three quarters of its length. A haft's last stretch goes into the head's eye
(`CraftWoodwork.HeadEye`).

## The woodshop

Wood is fibres (`CraftWoodwork`). **Runout** (`WoodState.runout`, 0 straight-grained - 1 cross-grained) is how far
they run out of the piece: sawn stock has some (0.2); **riving** splits a piece in two along its length and the halves
keep the grain the split followed - a clean split straightens it, one that strays cuts across the fibres, and a split
that runs out leaves one half short and tapered. Runout costs toughness (×(1 - 0.55·runout)) and makes bends tear.
Each species has a **cleave** (`CraftWoodDefinition.cleave`, 0-1): oak 0.9 and ash 0.85 rive true, elm 0.15 and
ironwood 0.3 have interlocked grain - a split in them strays more (`RiveStray`), wanders off the rays
(`SplitAlongOf`), drives harder under the maul and carving splits them more readily.

**Splitting and riving halve the larger of the two cross dimensions** (a log's diameter, then the other way), so the
chain round log → halves → quarters → billets ends in square stock a haft or a grip is carved from. Each split or rive
halves the bark of both halves (the new faces are clean) and starts the grits again.

**Splitting a log** (`SplitLog`, at the chopping block; only stock at least `MinSplitWidth`, 60 mm, across and
`MinWedgeThickness`, 40 mm, thick - `LesserSize`: a steel wedge is not driven into the end of a thin board, which is
rived or ripped): the wedge's line `a` off the heart (0 through the pith) and `b` along the rays: through the heart the
halves are even and their grain follows the split; off it one half is bigger (`SplitShareOf`: 50 % + 38 % × off, up to
88/12), across the rays the split twists out (runout, a defect); `c` how solid the maul's blows landed.
`SplitsWithWedges` gives the reason a piece is not split (too small or too thin: rive it, or rip it). The window draws
the end as it is and judges the line it is given: it must run across the side the wedges part (`WedgeLineAcross`: a
half's line from its flat face out; a near-square end either way), and the rays run out from the heart, so a line that
misses the heart of a log, a half or a quarter crosses them however it is angled.

**Riving** (`Rive`, the froe at the block): refused for a round log 60 mm or more across (split it with the wedges
first) and for anything too narrow to give two halves of `MinRiveWidth` (8 mm) - `RivesWithFroe`. `a` how far the lever
strayed off the grain, `b` how steady it was (jerky levering under 0.5 checks the faces), `c` the grain line the froe
was set on (0-1 across: 0.5 halves it; off the middle the halves are that share, and the split runs out more -
`RiveStray` adds (|c − 0.5|·2)²·0.3). A rived log is a log no more (`WoodState.log` false on both halves).

**Sawing** (`SawCut` across, at the sawhorse, or with the carving bench's saw): it cuts **exactly at the mark** — the
workshop makes a boundary there (`CraftPiece.InsertBoundary`, splitting the stretch it falls in), and a mark within 2 mm
of an end cuts nothing (`CanSawAt`). `b` how far the saw wandered off the line, `c` how square it ran: a wandering,
ragged cut leaves rough, torn ends, and each part has one fresh end - half its ends sealed at most (`endsSealed` halves)
- and keeps at least a fresh blank's stretches (half the settings' `segments`: a haft sawn from the 2.2 m stave is worked
as finely as one from a metre blank). The window's saw (`CraftSawStroke`) counts a stroke by its travel (40 px a full
stroke, under 8 px none) and rhythm: a cut across a section `w` × `t` mm takes 6 + √(w·t)/5 full strokes
(`StrokesFor(width, thickness)`: a square section as many as a rip through its side - 6 + side/5, the 40 mm ash stave 14 -,
a 650 × 16 mm plank 26, a 240 mm log 54), a rip 6 + t/5 for every 300 mm, and a short, snatched stroke trembles the
blade more. A saw lifted out of its kerf and set back on the mark goes on from where the kerf ran (`Resume`), so lifting it never puts a wandering cut back on the line. **Ripping a taper** (`RipCut`): the line from `a` to `b` (0-1 along the piece), its width `c`
mm at `a` and `d` mm at `b`; both edges of every stretch between are cut down to the line (with the saw's wander, `i0`
thousandths, as a wave of up to ±3 mm and a rough edge); it must narrow the piece somewhere (`RipNarrows`). It takes a
kite shield's tapered board from a long plank. At a real station wood is sawn stroke by stroke: a cut in one go
(`Split`, a test station's quick *Cut here*) is refused for wood there (*"Wood is sawn through stroke by stroke: mark it
and saw it (Woodshop → Saw, or Carve)."*), so a sawn end always gets its roughness from the saw's wander and squareness;
at a test station the quick cut parts it exactly at the mark (*Sawn to length*). Metal is cut with the hot chisel's
blows.

**Steaming** (the Steam location): the piece heats through in the box first (the thermal model: thick wood takes
longer); once its mean heat passes `SoakHeat` (85 °C) the steam soaks in - steamed through after about an hour of
process time for every 25 mm of thickness. Steam brings the wood's moisture up to `SteamWater` (32 %) and never dries
it (no heat-drying in the steam box either); left in far too long it goes soft and mushy. Out of the steam it is
plastic while it is hot (`Plasticity` = steamed × warmth) and it **sets again within a minute or so**: `steamed` falls by
the seconds out of the steam over `CoolSeconds` (45 s for 25 mm thick, thinner sooner, no less than 18 s). **Bending**
over a form (`BendWood`: `c` the form's radius in mm; 0, in older records, the radius of the stretch bent): the strain on the
outside fibres is the thickness over the radius - the same for any angle - against what they take now
(`BendStrain`, 1 = the limit): dry wood about 1:80, steamed through 1:25, with a strap holding the outside 1:10 - less
for rushed bends, cross-grained or checked wood: the pace is the pull's fastest (a yank strains the fibres whatever the
hand does after it), and the window's STRAIN gauge shows that, with the pace now as a thin tick. Past 1 the outside tears
(a crack, `split` grows). It springs back
part of the way (it keeps 45-95% of the bend: more when it was soft). The window's forms are 60, 120, 250 and 600 mm
(a snath's sweep); the guard keeps a form between 40 and 800 mm.

**Seasoning** (the Stack location, or the rack): moisture falls toward the **equilibrium moisture** of the station's
humidity (`CraftWoodwork.Equilibrium`: about 8% in dry air, 12% in ordinary air, 17% in damp), faster in dry air,
slower with bark on, at a rate set by the stickers' evenness, the shade and the warmth - and by its thickness: past
`SeasonThickness` (40 mm) in proportion slower (`Seasoning`: a whole 240 mm log six times slower than the billets rived
from it). When it dries faster than the wood can shrink evenly - `0.03 × (1.3 − ½ density) × (½ + ½ ends sealed)` of its
water a process hour, times that pace - the surface and the ends **check** (`checks`): a stack laid well (every gauge
in its green band) dries under that and hardly checks (under the 0.08 the look calls checked), unsealed or in the sun
it checks (about 0.2), and all of it wrong, through. Stickered less evenly than `EvenStickers` (0.7) it warps: the
stickers are judged as a stack is laid (`StickerEvenness`) - four or more, even gaps between them, and one under each
end (within `StickerOverhang`, 6 % of the length; further in the end droops off it, none held by 16 %). Ends sealed
`WellSealed` (0.8) and a cover `WellShaded` (0.7) far over the stack are what the window calls well done; a seal stays
with the piece (`endsSealed` keeps the best), on and off the stack. **The cover is the stack's** (`CraftRequestKind.StackCover`, a setting kept in
the workshop variable `stackCover`): it shades every board in the stack as it is pulled now; a stack never covered
leaves each board as it was stacked (`WoodState.shade`). Damp air even wets dry wood again. Wood still green (moisture
over `DryEnough`, 20%) in an item shrinks loose (a note, less integrity); the Look panel calls it damp, the guide sends
it to the stack. Wood dried by heat elsewhere (the forge's warmth) loses the same water whatever its stretches, and loose
in the air it takes the water back toward the air's equilibrium as it cools (`AirRegain`).

**Debarking** (`DebarkWood`, at the shaving horse): a green **log** (`CraftDefaults.Log`, the Log stock kind: as long as
the species' blank and at least 90 cm, 24 cm across, at 60 % moisture) comes in its bark (`MetalSegment.bark` on every
stretch); a stock **blank** is the species' own (`blankLength` × `blankWidth` × `blankThickness`: an ash stave 2.2 m
long, a linden plank 110 × 65 cm), so a part is sawn to its length from it. Each pull of the drawknife takes bark off a
stretch by its **depth** (under `PeelDepth`, 0.3, only the outer bark; from 0.3 at the cambium all of it), how much of
the log's round it reached (`c`: one pull on a face is `DebarkPerStroke`, an eighth - two pulls on each of the four
faces go all round) and how readily the bark lets go (green wood ×1.25, so one round peels it; dry ×0.65, so its tight
bark takes two; sharper tools more); past the cambium (`GougeDepth`: 0.62 green, 0.52 dry) it **gouges**: width and
thickness lost, a rough surface, `WoodState.gouged`. The knife's strokes (above), the rasp's and splitting or riving
also take bark. Bark left on wet wood slows its drying and lets rot in.

**Sanding** smooths the surface toward a floor set by the grit and the direction (`SandFloor`: across the grain leaves
scratches); the **grits must go in order**, judged on the surface each stroke: a paper finer than the coarsest
(`CoarsestGrit`, 0.4) cuts at its full rate only where the surface is no rougher than the paper one grade coarser
(`GritStep`, 0.3) leaves, plus `GritReach` (3); deeper scratches it hardly touches (×`SkippedRate`, 0.1) - `SkipsGrit`.
A careful knife leaves about 17-25, so the medium paper may follow it. The window sends a pass as each counted pass of
the stroke is made (`CraftStroke`), 1.5 passes' worth each (the guard's own allowance for sanding: 6 a second), shows
stretch by stretch where the paper in hand bites (a strip under the board) and says where it does not (*"At the right
end the medium paper hardly bites: take the coarse paper there first."*). Sanded with a paper of `FineGrit` (0.8) or
finer it is sanded smooth; with a coat on it as the look sees one it is sealed (`Sealed`).
**Finishes** go on with the brush or the cloth (`Varnish`), each by its own rule, on dry wood (on damp wood each takes
about a third): a **varnish** builds a film (`finish`, `finishEven`), evenly on a smooth surface (blotchy on a rough
one), runs when laid thick, wrinkles over a wet coat; a **resin** sealer (a varnish whose id says resin) builds the
hardest film (`resin`); an **oil** soaks into the grain (`oil`, best into a smooth surface) without a film; a **wax** is
rubbed in (`wax`): thin and even it sheds water, laid thick or unevenly it smears, and it wears off. A varnish or an oil
dries at its supply's pace (`CraftSupplyDefinition.dryingHours`: spirit varnish 1 h, pine resin 5, tung oil 6, oil
varnish 8, linseed 10; else `DefaultDryingHours`, 4). The finish keeps water out (`FinishProtection`: a film best, then
oil, then wax) and stands wear (`FinishWear`: resin best, wax least): items with finished wooden parts last longer. The
station charges the brush from a pot: one item of a finish lays about two full-length coats (`CraftWoodwork.BrushUse`);
a new item is opened only when what is left will not cover the stroke. A player has one open pot of each supply at a
station (`CraftOpenPots`), shared with the tannery: the linseed oil or beeswax opened for a hide is the same pot.

## The tailor's

**Cloth** is a hide-like sheet of cells from a hide definition marked **textile** (`CraftTextiles`): it comes woven
and finished (no fleshing, liming or tanning; the tannery's tools and vats refuse it) as a straight-edged bolt, and is
cut with the patterns at the tailor's table (or a leather bench). Its grain runs along the bolt: a part cut across it
(a pattern turned a quarter) is weaker (×0.85, *cut across the grain*); a ragged cut frays (strength ×0.75-1 by the cut's
cleanness). A design's sheet part says what it is cut from (`CraftLeatherSpec.fabric`: Leather, Cloth, Either).

**Spinning** (at the wheel, `SpinFibre`): a bobbin is spun from a batch of fibre lying on the station (see *Fibre and
yarn*); spun from the bag (`Spin`), one fibre item is laid on the station as a batch, as it comes (raw: greasy, dirty,
tangled - `CraftTextiles.RawPrep`), and the bobbin spun from it in the same request, the rest of the batch staying on
the bench - so the bag and the bench give the same yarn for the same hands. The yarn's quality is the draft's evenness
times how near the twist was to right (`SpinQuality`), the preparation already in the evenness (counted once); a skein
of it reels into the coarse, even or fine thread of its fibre (`spinsInto`) by that quality (`CraftTextiles.ThreadTier`:
below 0.45, below 0.72, above - the same tiers as `CraftTextiles.Spun`). A twist within 0.07 of right
(`CraftTextiles.TwistTrue`) keeps nine-tenths of the quality and more - the wheel's green band; further off it reels a
coarser thread (the amber band, out to 0.3 and 0.7), and beyond it pulls apart or kinks. **Thread** added to an item carries its
strength (`Attach`'s supply strength, kept as `<role>@strength`): the seams are as strong as the thread (×0.8-1.15 of
ordinary thread).

**Measuring and fit**: `MeasureFit` (the tape, the ring sizer) records how accurately the wearer was measured and the
size read: into the item it was taken for when that item takes that kind of measurement (a ring the finger's - the
sizer's, under 60 mm -, a garment the chest's: a design with a part cut from cloth, `CraftTextiles.MadeToMeasure`, which
is what its fit counts for), otherwise it waits, each kind on its own, for the next item started that takes it - a
shield or leather armour started meanwhile does not use up a chest's measure (`AssemblyState.measured`, `fit`,
`fitSize`; `CraftWorkshop.WaitingMeasurement`). Of jewellery only a ring is sized to a finger: the ring sizer's measure
is as accurate as the few rings tried and the hands make it (its steps of 0.75 mm are the sizer's own, not the
measurer's - the band is formed to the ring that fits). A measure is never more
accurate than the tailor's practice allows (`CraftRequestGuard.TrueCap`: a novice 0.90, a master 0.96 - the tape's
words and ACCURACY gauge show the same). Clothes and rings made without measuring fit like any other (×0.95, *a loose, common
fit*); measured, ×(0.9 + 0.12·accuracy) (`CraftTextiles.FitOf`) - a measure less accurate than about 0.42 (`FitWorthIt`)
fits worse than none, one above 0.8 is *made to measure*; a ring's band formed off the measured size fits worse still.

**Layout**: patterns are chalked on a length of cloth before cutting (`LayoutPattern`; the leather bench lays out
hides) - never on a part already cut to a pattern (`AlreadyCut`): they may not overlap; the share of the sheet they use is its utilization (`CraftTextiles.Utilization`); a cut
on a chalked placement - the same pattern, cell and turn - is cleaner (+0.05) and any other chalk a cut goes through is
lost (`CraftTextiles.Overlaps`, which can leave out the chalk the pattern is laid on).

**Cutting cloth** follows the pattern's true outline - the cells' own edges, where the chalk is drawn
(`CraftPattern.Outline`, `CraftArt.PatternOutline`): a side counts as cut when the shears pass within a third of a cell of
its middle; the cut's cleanness (`Cut`'s accuracy) is how close the snips kept to the line (×0.85), how even they were
(×0.15), and how much of the outline was cut (a part cut free from nine-tenths round tears the rest: each hundredth left
uncut costs 2.5% of the cleanness), and it is never cleaner than the tailor's practice allows - the same cap as the
leather knife's (`CraftRequestGuard.CutCap`: a novice 0.90, a master 0.96).

**Sewing**: the stitches' spacing and spread are as true as the station's tools (÷ tool quality); a practised hand
lays a run of stitches with each click (see *Practice*). The **needle's pull** pierces the holes its stitches need as it
draws the thread (`JointWork` with the needle and its flag: the holes already punched are used first, never more than
the seam takes, spaced and lined up as the hand did - one request for the pull, which the guard charges a whole stitch's
time), so stitches never wait on an answer to a pierce. A stitch is best pulled to `CraftAssembly.StitchTensionIdeal`
(0.6 - the seam's quality falls off either side) and each stitch pulled past `StitchTensionCuts` (0.9) cuts into the
material (0.05 ÷ the seam's stitches of damage): a run of stitches says how many of its pulls went past it (its `i2`),
and only those cut, whatever the run's mean pull - a run without that count (a single pull, an older history) is judged
by its pull. The **thread must suit the material** (`CraftTextiles.ThreadSuits`): heavy leather needs a strong thread
(0.6 + 0.2 per mm of thickness), fine cloth puckers under a far heavier one; a seam whose thread suits it less than 0.8
(`CraftEvaluator.ThreadSuitsWell`) is named in the item's notes (*Crown seam: sewn with too weak a thread for the cloth
(0.6; it wants 0.92)*). A seam that runs through a padding (quilting through
the batting: a joint whose part is a supply other than its thread) is sewn only once the padding is laid in, and a
finished item takes no more joint work (*It is already finished.*).

## Fibre and yarn

A **batch of fibre** (`FibreState`, laid on the fibre bench from a Fibre supply: its staple, grease, dirt and short
fibre come from the supply) carries dirt, grease, tangle, alignment, short fibre, neps, felting, water and soap.

* **Clean** (`CleanFibre`): picks a share of the dirt out - 0.85 of it for every burr seen and picked, more with fine
  tools, never more than 95% (`PickedOut`); a click that found no burr picks nothing, and fibre pulled with it is lost.
* **Scour** (`ScourFibre`): grease melts out from about 35 °C and fully from about 62 °C (silk's gum from 70 to 95 °C:
  `CraftFibre.ScourHeat`) with a detergent, over the minutes it soaks (two-thirds of what the heat can lift in the first
  15 minutes, 40 for silk: `SoakMinutes`); wool **felts** when stirred hot, or shocked from hot to cold - a rinse more
  than 15 °C from the bath's heat starts it, more than about 25 °C leaves the Felted defect (`ShockFrom`, `ShockFelts`);
  a scour with a detergent leaves 0.4 of soap in it and each rinse takes 0.25 out (`SoapAfter`: two rinses clear it);
  it comes out wet and dries in the air (slower in damp air). A soak is no longer than the real time the fleece can
  have had in the bath since the hands came to the bench - a minute a second (`SoakMinutesPerSecond`), 240 at most
  (`MaxSoakMinutes`): the guard's soak allowance, with a few minutes' room for the network's lag.
* **Open** (`OpenFibre`): teases the tangle out; torn hard, fibres break (more short fibre); felted fibre hardly opens,
  and wet fibre (over 0.4) opens 0.6 as far (`OpenGive` - what the bench draws).
* **Card** (`CardFibre`): aligns the fibres toward two-thirds as far as the card's teeth reach into them
  (`CardGrip`: nothing below a touch of 0.05, fully from 0.25 - `CardLight`), opens the last tangles; strokes harder
  than 0.4 (`CardNeps`) knot **neps**, harder than 0.5 break fibres, and clogged cards (wet, tangled, felted fibre) knot
  them too. Not while it is wet.
* **Comb** (`CombFibre`): long staple only (40 mm and up: not cotton): straightens toward 0.95 and leaves the short
  fibre, neps and dirt in the comb as noil (weight lost). A combed batch spins **worsted**.
* **Spin** (`SpinFibre`, at the wheel): grams of the batch (a bobbin: 30 g, `BobbinGrams`) into a bobbin of **yarn**
  (`YarnState`): its length is the grams drawn out by the draft (`MetresSpun`: grams × (1.2 + 6.5 × fine) × (0.85 + 0.15
  × prep) - a fine draft gives over six times the metres of a thick one), its evenness the draft's times what the
  preparation keeps of it (`DraftEvenness`: (0.55 + 0.45 × prep), and a fine draft of poorly prepared fibre loses up to
  a further 30%), its strength the fibre's, the twist's and the evenness's (+12% worsted, less with short fibre and
  neps).
* **Ply** (`PlyYarn`): two or three bobbins of the same fibre twisted the other way: the shortest sets the length and
  the plied yarn is wound on its bobbin (on a tie the first one's) - every other strand keeps what is left on its
  bobbin, no longer the skein it was counted as (counted again before it is reeled); evenness rises, the strengths
  add; it is **balanced** when the ply twist is near 0.8 of the singles' twist
  (it hangs straight within `PlyBand` of it, a balance above 0.75). Strands that disagree in twist do not cancel out:
  the singles' twist is as far from right as they are on average (`MixedTwist`).
* **Join** (`JoinYarn`, at a loom or a wheel): bobbins of the same fibre and the same plies wound together onto one,
  end to end - the lengths and grams add, evenness, preparation, strength and balance are the stretches', by length,
  and the twist as far from right as they were (`MixedTwist`); the joined length is not counted. A warp longer than one
  bobbin holds is wound this way.
* **Measure** (`MeasureYarn`): a skein wound on the niddy-noddy and counted (1.5 m a wrap): the count may be off.
* **Reel** (the station's ReelYarn): a counted skein becomes thread items, one per 20 m of the count (never more than is
  really there), coarse, even or fine by the yarn's quality (`CraftFibre.Quality`: plied yarn +0.08; `ThreadTier`).
* **Weave** (`WeaveCloth`, at a **Loom**): a bobbin of yarn into a new length of cloth (a textile hide piece: the fibre's
  `weavesInto` - wool cloth, linen, canvas, cotton, silk) `width` cells wide (2-16), one row for every beat sent, never
  more rows than the yarn allows (`CraftFibre.RowsFor`: `MetresPerCell` = 12 m of yarn per cell, at most 30 rows a
  length - wind more bobbins on for a longer one); what is left stays on the bobbin; a practised weaver's beat packs 2
  or 3 rows, each as its beat - see *Practice*). The cloth is as **sound as its yarn**: the yarn's strength against the
  best its fibre spins - a combed worsted, even and rightly twisted, strand by strand (`WeaveSoundness`) - sets its
  cells' soundness (their grain: 0.4 + 0.6 × it, which the evaluator reads as the cloth's strength); a yarn below 0.45
  (`WeakYarn`) is named in the cloth's record (*of a weak, kinked yarn*), an over-twisted one weaves a stiffer, wiry
  cloth, and a cloth whose weave is loose (below 0.65 on average, `CraftLook.LooseWeave`) says so in its look. Each
  row's beat (its timing-bar score, never truer than the weaver's practice: `CraftRequestGuard.TrueCap`) sets how sound
  it is (`RowSoundness`: 0.8 + 0.2 × the beat), how smooth, and how often its patches are flawed ((1 - beat) × 0.2 more
  likely); a beat below the damage threshold skips a pick - a flaw across the row; an uneven warp (the tension)
  puckers cells, a lumpy yarn shows slubs, a beat far from balanced (open or packed) is weaker and stiffer. A fine yarn
  weaves a thinner cloth. The cloth is laid out, cut and sewn like any other.

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
  worked on bare stone it scratches - more on a soft stone - and, held carelessly on a brittle one, chips it). A sector
  loses `CraftLapidary.CrustRemoval` of its crust: the work × 0.35 (brush) or 0.9 (pick) × the tool's quality ×
  (0.6 + 0.4 × how controlled the strokes were) - the window's preview is the same number. How clean it was left, less
  the marks, is its `cleaned`. Crust left on blurs the refractometer and the dichroscope (×1 + 3·dirt), makes the
  balance read light (−0.6·dirt), hides inclusions beneath it from the loupe (a thick crust is opaque) and throws the
  saw (more kerf lost). A cut stone has no crust: the first tier grinds through its skin, crust and all.

* **Tests** (`TestGem`): the loupe (and the cleavage cracks, for a stone that has them); scratch with a hardness point
  (a point harder than the stone - strictly: one of the stone's own hardness does not - scratches it, a surface scratch
  the polish takes off; the readings bracket the stone's own hardness); the refractometer (the refractive index, ±0.035
  × (1.15 - care) ÷ tool quality, doubled on the rough: a spot reading); the balance, weighed in air and in water
  (specific gravity, less exact under a carat); the dichroscope (pleochroism and the axis' tilt). Each needs its
  instrument at the station; the refusal names it (`CraftStation.InstrumentName`).
* **Identification** (`IdentifyGem`, at least two tests): named once - the name sticks to the stone, right or wrong, and
  a second name is refused (*"You named it garnet: the name sticks."*). The note it raises carries only its id
  (`identified` or `misidentified`) and no words, so nothing at the bench tells whether the name was right; named
  rightly, the player knows that gem from then on (the journal).
* **Evaluation** (`EvaluateGem`): every look of the loupe (where, at what focus depth) finds what it shows sharp
  (`CraftLapidary.Seen`): the inclusions within `LoupeReach` of it - (0.1 + 0.08 × the hand's steadiness) × the tool's
  quality (at most 1.3), in the rough's radius - plus 0.6 × the inclusion's size, less under crust (a thick crust is
  opaque), and within 0.12 + its size of the depth in focus (`FocusDepth`). The window draws sharp exactly what a look
  finds, where the hand's tremble puts it, never steadier than the hands. The stone's worth is then estimated from what
  was found only - a careless search over-values it. The window's SEARCHED meter counts what the loupe truly went over
  (`CraftLapidary.MarkSearched`, `SearchedShare`): cells of the plan 0.1 across whose middle lay within a look's reach
  (less under a thick crust, which hides what is beneath it), at the steps of depth (0.05 deep) wholly within 0.12 of
  the focus - each stroke counted at the steadiness it was sent with - over the stone as it now is (`InStone`: the cut
  stone's planned outline once faceted, its ground outline once ground): rings that leave gaps, or a focus that skips a
  depth, leave it short of 100%, and a search it calls complete has found what is there.
* **Orientation** (`OrientGem`): the cut, where the finished stone sits in the rough and the table's tilt to the
  crystal. The planned outline is an ellipse in the window (its half-length the window's radius), as long as the cut
  (`TargetRadius`); inclusions outside it are cut away (`Kept`); a pleochroic stone's colour is best with the table
  across its axis (×1 - 0.6·pleochroism·sin²); a table within 8° of a Good or Perfect cleavage flakes when polished.
  The yield the plan expects is `CraftLapidary.PlannedCarats` (the ellipse's share of the rough, or once ground the
  ground outline's share of the planned one, ^1.5 - what lies inside the outline, as the wheel takes it - × what the
  cut keeps of that, `CutKeeps`: its yield × 2.2, at most all of it: a round brilliant 0.88, a cushion, a step cut or
  a cabochon all of it) - the very number the first tier delivers, and the same whether the preform was ground to its
  outline first or not: the Orient place's expected yield is what the guided path keeps (pressed hard to the end, the
  wheel spills a little into an oval's flanks), and leaving the wheel out never gives a heavier stone - only a lopsided
  one, worth less. A stone ground on the wheel to its planned outline cannot be planned again (*"It is ground to
  its planned outline: the plan is fixed."* - a new plan would grind its carats off twice).
* **Splitting and sawing** (`CleaveGem`, `SawGem`): a line across the plan, cut where it was drawn whichever way it was
  drawn (`CraftLapidary.NormalLine`, `LineThrough`: its direction kept in a half turn, the offset's sign turning with
  it - a line drawn right to left, or downward, is never cut at its mirror image through the centre). A line with no
  stone on one of its sides (`LineCuts`: what the faces leave of the rough, on the grid its area is weighed by; once
  ground, the ground outline) misses the stone: it is refused (*"That misses the stone."*) and costs nothing - no kerf,
  no chips, no heat. **Cleaving** (only a Good or Perfect
  cleavage can be followed: a stone with a Poor one is nicked and snapped, or sawn): the blow is judged on the split's
  timing bar (`CraftLapidary.SplitBar`: the ideal force 0.42 + 0.25 × toughness, zones narrowed by
  `CraftLapidary.Difficulty` - harder, more brittle stones are less forgiving); within 4° (Perfect) or 2.5° (Good) of
  the cleavage's trace (`CleaveTolerance`) and above the damage threshold it parts along its plane, losing 1% + 6% ×
  (1 - score); a blow off its measure (graded below Good) steps the face (a chip); off the plane or below the damage
  threshold it shatters (a chip, a feather into the stone, 12% lost). Under 0.3 the blow does not part it. **Nick and
  snap** (`snap`, a stone with no cleavage worth following and toughness under 0.65 - quartz, garnet, tourmaline,
  peridot, emerald, opal): never clean - 6% + 15% × (1 - score) lost and chips; below the damage threshold a crack runs
  in. Tough stones without a cleavage worth following (corundum) must be sawn. The refusals never name the stone (it
  may not be known yet). **Sawing**: its steadiness is the strokes' average score; the kerf and a wandering cut cost
  carats; every **miss** (a stroke below the damage threshold) jumps the blade: 2% more lost and a chip; a dry cut heats
  it (heat-sensitive stones crack); a feed pressure over 0.65 (`SawChipPressure`) chips a brittle stone (toughness
  under 0.55). The far side, when big enough (0.15 ct), is a stone of its own - rough (its stage too: never preformed as
  a stone of its own, it is planned and sawn or ground like any rough): no outline, no plan, no polish. A stone
  already ground keeps its ground outline, cut back where the new face crosses it, and its carats follow that outline. A
  preform has at most 55% of its rough outside the planned stone.
* **Grinding** (`GrindGem`, the gem bench's wheel or the faceting machine, once oriented and with no more than 80% of
  the rough outside the planned outline - `GrindableExcess`; more: saw it off first): the preform's outline (32 radii
  from the planned stone's centre) is ground toward the planned outline by touches (angle, amount) at a rate set by the
  stone's hardness, the tool and the steadiness; each touch takes the neighbouring radii too. Carats follow the area.
  Ground into the planned outline it is `overground` (smaller), and its `shapeError` grows with that and with how
  unevenly the outline came. **Heat** comes touch by touch (`CraftLapidary.GrindHeat`), the same however the touches are
  sent: a touch's dry pressure (amount × (1 - coolant)) above 0.2 heats the stone 0.04 a unit (a quarter of that for a
  stone that is not heat-sensitive), and a heat-sensitive stone cracks once its heat reaches 0.25. Under 15% left
  outside the outline it is a preform ready to facet; faceting a ground preform with more left is refused. Never
  ground, the first tier grinds it freehand: `shapeError` 0.25 (lopsided).
* **Faceting** (`FacetGem`, the faceting machine): tier by tier in the cut's order, each at its angle - the pavilion's
  ideal is 40.75° + (2.42 - n) × 2.6 and the crown's 34.5° + (2.42 - n) × 8.5 for refractive index n (diamond 40.75/34.5,
  quartz about 43/42), plus the tier's offset; the index stops' error; how the facets met (short: they do not meet, a
  window of old surface; past: overcut, weight lost); how unevenly the tier's facets came (`asymmetry`) and how many
  were botched (`misses`: 1% weight and a chip each - recut the tier). A tier is cut with as many facets as the cut
  gives it (`CraftWorkshop.TierFacets`: the caller's count is not trusted, and the guard times the hits by the tier's
  own). Recutting a tier (3% of the weight) grinds its botched facets away with it: the chips they left
  (`CraftFacetRecord.botchChips`) and their flaw - kept apart from the stone's other chips, at `CraftLapidary.BotchAt(tier)`
  - go; the stone's other chips stay. In the window each facet is a timing-bar hit (`CraftLapidary.TierFromHits`): its
  offset sets the angle's and the meet's error, its score the index error, and the spread of the offsets (0.6 a unit)
  and of the scores the asymmetry - facets cut alternately steep and shallow are uneven, however well they average. A
  worn machine adds to the angle's error. Heat-sensitive stones ground dry crack. The first tier takes the preform
  down to the planned stone (`PlannedCarats`) and its crust off; after the last the girdle is measured.
  `CanFacet(id, tier)` asks the same checks as `FacetGem` and changes nothing (the window asks it before a tier's first
  hit, so no hit is spent on a tier the station would refuse).
* **Polishing** (`PolishGem`): every rule is per pass and follows the time on the stone, so the result is the same
  however the passes are sent. The window sends a pass for each half second on the lap (`CraftLapidary.PassSeconds`)
  with the seconds it spent on each of 9 regions: a region's share is its seconds against the passes' own (no more is
  counted than the passes took), so a pass that only grazed the stone polishes as little as the graze, and time off the
  stone nothing (no regions given: every region evenly, the whole passes). A compound polishes well only stones up to
  its hardness (`CompoundFit`: at most 0.5 softer than the stone ×1, up to 1.5 softer ×0.45, softer still ×0.12; tripoli
  6, cerium oxide 7.5, alumina 9, diamond paste 10) - a compound too soft (under 0.5, `SoftCompound`) is said pass by
  pass; the rate rises with the pressure up to 0.45 (`PolishFullRate`); each region's shine is 1 - e^(-1.6 × its
  polish), the stone's polish their mean, a region below 0.6 is a dull patch; past 2.5 the edges round (meets, a little
  weight). Heat (pressure × speed over 0.45) crazes sensitive stones and rounds the facets' meets, as far as the lap was
  on the stone. Rounding only ever makes a meet worse (`WorseMeet`: further from 0 whichever way it was off, a true one
  going short) - neither heat nor polishing on past bright mends a bad cut. A table laid on a cleavage flakes under a
  pressure over 0.3 (`FlakePressure`), as far as the lap pressed on it (a pass that never touched the table does not
  flake it): each pass takes 0.34 × (pressure - 0.3) ÷ 0.7 of polish off the table region and chips it - a light lap
  polishes it safely. Polish takes scratches off but never changes a facet's angle, index, asymmetry or misses. The lap
  is charged with a compound for the stone (`GemState.compound`): the first pass with it, and every change of compound,
  takes one from the bag (`CraftLapidary.ChargesLap`) and is refused without one; passes with the compound the lap is
  charged with need nothing more.
* **Grade** (`CraftLapidary.Grade`): clarity (the inclusions kept), colour (tone × orientation, less for heat),
  integrity (feathers, heat, chips, cleavage), cut (each tier's angle, index, meet, × (1 - 0.5 asymmetry) ×
  (1 - 0.15 per botched facet); the pavilion counts double; × (1 - 0.6 × min(1, 2 × shapeError)) for the outline),
  polish, **brilliance** (a pavilion more than 3° shallow leaks light, more than 3° deep goes dark), carats → quality and
  value (value per carat × carats × (0.15 + 1.7 q²), more for a big stone). Before the first tier its yield is
  `PlannedCarats` - what the first tier will leave of it.

## Jewellery

The jeweller's work is done on metal **wire and strip** (`CraftDefaults.JewelleryForms`: 600 mm × 2.5 × 1.8 wire, 150 mm ×
4 × 0.5 bezel strip) at the jeweller's bench (`CraftJewelry`):

* **Forming** (`FormShank`): a band, a bezel, a prong head (3-8 prongs), a bail or a chain, to an inner size (2-200 mm
  for a band, a bezel or a head): the metal must be thin enough (bezel 1.2 mm, chain 2.5, others 4) and long enough
  (π × (size + thickness) for a band or bezel, 9 mm a prong, 14 mm a bail, three times its size and at least 300 mm a chain). Only the length the
  part needs is used: the wire or strip is cut there first (`CutToLength`) and the rest goes back on the bench as it was,
  unformed (the record says how many mm were used and put back). How round it came off the mandrel and the gap left at
  the seam are kept; a hard, brittle metal cracks. `CanFormShank` asks the same checks and changes nothing (the window
  asks it at the first tap).
* **The seat** (`SeatSetting`): cut in a bezel or prong head - loose, or already fitted to the piece - to a stone's
  girdle: 0.15 mm too small and the stone does not go in; up to 0.08 mm wide it is snug; wider, it never holds fully
  (`SeatFit`); how level and clean it was cut. The record words it by its fit (`SeatWords`: *snug to the stone*, *a
  little wide: the stone will not sit tight*, *too wide: the stone will rattle - start a new setting* - exactly where no
  pushing could ever set the stone in it, `SeatHolds` -, *still too small for the stone*). The setting's **collar** (the size it was formed to) must suit the stone (`CollarProblem`): more than
  0.6 mm smaller than the girdle the seat would cut through its wall, more than 1.5 mm wider it could never hold it -
  the seat and the fitting are refused; up to 0.3 mm wider it closes snugly (`CollarFit`), wider it holds less and
  less.
* **Soldering** (a Solder joint, `CraftJointWork.Solder`, with solder added to the piece): flux, then heat to the
  solder's flow - `CraftJewelry.SolderFlow` = smooth(0.85, 1, heat) × `FluxReach` (0.35 + 0.65 × flux) ×
  e^(-gap ÷ 0.35); the joint takes from 0.5, so a well-fluxed, closed seam takes a little below full heat, a thinly
  fluxed one only nearer full heat, and too little flux or a gaping seam never (the window's heat gauge shows where this
  joint takes; the parts glow at the solder's own flow point, `CraftSupplyDefinition.flowTemperature` - silver solder
  745 °C, gold solder 760 °C, 760 °C when none is given). Past 1.12 it
  overheats (porosity, firescale), and a stone already set feels it (heat-sensitive or brittle stones crack).
* **Setting** (BezelSet, ProngSet joints, `CraftJointWork.Push`): pushes, their pressure and how evenly round
  (opposite sides in turn) - security grows up to what the setting allows (`MostSecure` = (0.35 + 0.65 × seat fit) ×
  collar fit); uneven pushing and an unlevel seat tilt the stone; a push harder than 0.8 (`ChipPress`) on a brittle
  stone (toughness under 0.55) chips it. Set when secure (`CraftJewelry.SetSecure`, 0.7) and, for prongs, every prong
  pushed: one line for the joint done (the Set place's green and its *"it holds"*), the inspection (under it: *the stone
  moves in its setting*) and the finished item (under it: *the stone is loose*). A claw setting is formed with as many
  prongs as the design holds its stone in (`ClawsWanted`); another head is refused at fitting.
* **Finishing** (`FinishJewel`): buffed and cleaned, then inspected - a loose or tilted stone, pitted solder, a chip
  are found here. Buffing takes the stock's scratches off the metal parts (their roughness falls by up to 90%, their
  polish rises to the buff). Only the time on the wheel counts: the station grants at most
  `CraftJewelry.BuffPerSecond` (0.12 of a full buff a second, a little over for the hand) - asked for more, it buffs
  what the time allows (*"Too fast: the wheel has not buffed it that bright yet."*), and the window then waits to finish
  the piece until a later inspection is granted the whole buff (the piece keeps the brighter of the two). A jewellery
  design finishes only after it.
* **Workmanship**: a jewellery design's metal parts are judged by the jeweller's own work
  (`CraftJewelry.PartWorkmanship`), not against a smith's target shape: how round off the mandrel, how closed the seam
  and - a setting - how its collar suits the stone and how level and clean its seat was cut.
* **What it is** (`CraftJewelry.Evaluate`): named for its metal and stone ("Gold solitaire ring with a ruby"), its magic
  defence from the stone's quality and size (and the metal's magic), a resistance of the stone's element, as much of it
  as the stone is secure; durability from the metal and the setting; the price adds the stone's value.

## Assembly

A **design** lists its parts (role, kind, count, the shape or leather or wood it should have, how important it is) and
its joints (fit, peen, rivet, stitch, wrap, wedge, socket, buckle). A part goes in only if it is the right kind, cut to
the right pattern, cool enough near leather and wood, and it physically fits (`CraftAssembly.WhyNotOn`):

* **The tang is measured from the blade** (`CraftAssembly.MeasureTang`): the run of segments from its end narrower than
  halfway between the design's tang and the blade just past its shoulder (a design whose profile shows no shoulder: the
  segments before its `tangTo`). A blade with no such run has no tang and takes no guard, grip or pommel; one that does
  not widen past it has no shoulder for a guard. A guard's slot must clear the tang where it meets the shoulder (the
  stretch that starts to widen is what the guard rests against), a grip's hole the tang along it, a pommel's hole the
  tang's end - each refusal gives both sizes. The tang must come through every grip and pommel fitted with **3 mm**
  (`PeenAllowance`) more to peen over: a shorter one refuses the grip or pommel with its length and what the hilt takes.
  Once the pommel is peened the hilt is closed: nothing else goes on or comes off but the pommel - the blade held in it
  too (`HeldOn`).
* A haft must go into the head's eye: a little tight is a good drive; over 3 mm too thick, rasp it. Up to 1 mm of play
  (`PlayFree`) the head still sits tight; a wedge driven home takes up 2 mm more (`WedgeTakesUp`, as far as it is
  driven: `PlayLeft`) - looser than that it rattles however well it is wedged. A socket has no wedge: its play stays.
  The bench says which as the part goes on (`HaftFitWords`: "the wedge will take up the play", "more play than a wedge
  takes up: fit a thicker haft", "nothing takes that play up: fit a thicker shaft").
* **Supplies** (thread, padding, solder, a bowstring) come from the bag: the one picked, never more than the item still
  takes (`SupplyRoom`); their strength is kept with them, and what they weigh and are worth (`Attach`'s supply id: the
  item's grams, `CraftSupplyDefinition.ItemGrams`, and its price, kept as `<role>@grams` and `<role>@worth`;
  `CraftAssembly.SupplyIn` - put in unnamed, they count as the first supply of their kind); what is added stays in.
  The item weighs them and is worth them (the batting quilted into a gambeson, 300 g a batting; the thread in its seams,
  a skein's 5 g; the solder in a ring's joint, half a gram - and the gold solder's price), the balance weighs them as it
  is made (`CraftLook.AssembledMass`), and its parts name them beside its pieces (*Batting, 900 g*). **Fasteners**: any set of the right
  kind goes on, and another set goes in with the one there (the counts add up; metal, size and make mix by count). A
  rivet set stays part of the item: the set still counts it (`FastenerState.used` of its `count` are set, `Left` still
  to set), so the item weighs it, is worth it and lists it among its parts. When the item is finished, the rivets or
  studs left over in its set come off it, a set of their own on the bench (the Finish operation carries its id): the
  item keeps exactly those set into it.
* A part taken off undoes every joint it was in - holes, stitches, rivets, peen, wedge, solder, its blows: nothing of
  the old joint counts for the next part. The rivets in those joints are drilled out and lost (they leave their set); a
  set of rivets taken off comes off with those still to set, every rivet set from it drilled out (the holes stay);
  leather or cloth taken off is marked where it was worked. The bench asks first (*Take off* twice) when it undoes
  work.

Joints are made by hand (`CraftAssembly.Work`), one beat or one blow at a time:

* **Holes** punched on the beat (spacing and alignment from the timing; a hole well off the beat is misaligned): the
  pricking iron punches 4 a beat through leather, a punch one a beat for a rivet, the needle pierces cloth as it sews.
  A stitch or a rivet takes a hole each; a buckle a hole for its rivet (or 4 for its stitches when the design has no
  rivets); a stud driven into wood needs none.
* **Stitches**, never more than the holes punched: the tension pulled - best 0.6 (`StitchTensionIdeal`), over 0.9 it
  cuts the leather (`StitchTensionCuts`) - and its evenness. A buckle in a design with no thread is riveted on; one
  sewn on takes 4 stitches (`StitchesPerBuckle`).
* **The hammer**: each blow is rated against the joint's ideal (`IdealCharge`, as a share of a full blow: a rivet
  through leather or cloth 0.6, through wood 0.7, metal on metal 0.8; a pommel peened 0.8, a guard's slot closed 0.65,
  a wedge 0.7): within 0.85-1.15 of it a good blow, under 0.6 too light, over 1.25 too hard - the band is the rule
  wherever the meter draws it. A wedge, a socketed head and a guard's slot are driven as far as the blow, up to the
  top of the good band and no further (`DriveEffect`): a blow under 0.6 bounces off and does less than its share, and
  a harder one only harms. Every blow that drives, peens or closes a joint is kept (`JointState.blows`, `blowQuality`:
  the mean of their `BlowQuality`) and the joint is rated 0.75-1 by it (`StruckWell`: a wedge knocked in crooked, a
  peen hammered ragged, a guard battered on; a rivet's own blow is in its quality). A **rivet** is set as well
  as its blow (`BlowQuality`, × the set's make; ×0.4 forced through without a hole - the workshop asks for the hole
  now): too light it stays loose, too hard it crushes the leather or splits the wood round it, and a hard rivet (over
  300 HV, cold) struck hard cracks. The **pommel** spreads 0.2 a blow on the ideal (light blows barely move it, a
  blow past the good band spreads no more than its top: `PeenEffect`): good from 1.0 to 1.4, past it the peen's head
  cracks on a hard tang (over 350 HV); a too-hard blow bends the tang. A **guard's slot** is closed onto the tang -
  never into it, at most 0.8 mm in all (`PeenCloseBudget`); a too-hard blow dents the guard, and a hard guard (over
  400 HV) struck hard while cold cracks; a wooden grip is not peened (it fits as it was bored). A **wedge** is home
  from 0.6 to 1.3: past it the haft splits, and a too-hard blow splits it at once (0.1 × (r − 1.25)/0.25 a blow; the
  haft's wood carries the split - no second defect on the item). A **socketed head** (a spear's, a halberd's) is driven
  onto its shaft over the same span, and past it - or by a too-hard blow - splits the shaft in the socket. Its pins
  (the design's pin part, when one is fitted: a spear's socket pin, a halberd's two langet rivets - `PinsWanted`) go
  through socket and shaft once the head is home: their holes punched, then set; the head is driven no further once
  they are through. A pin adds to the hold, never replaces the drive: a socket is done once its head is driven on, and
  a pin fitted is set (or taken off) before the item is finished (`PinLeft`).
* **Wraps** wound turn by turn (best tension 0.65).

A joint is **done** (`JointDone`) when the item can be finished with it, and **complete** (`JointComplete`) when the
work has made it as well as work makes it - every stitch, rivet and turn, the pommel peened into its band, the wedge
home, a socketed head home with its fitted pins set; the bench moves on to the next joint only then. An item
finishes - once: a finished item takes no more work - when every required part is in place (a set of buckles holding as
many as the design takes) and every joint between the parts that are there is done (`CraftAssembly.Missing`: one rule
for the bench, the guide and an unfinished item's tooltip). A joint to an optional part left off - a grip wrap, a club's studs - is not made, holds nothing up, cannot be
worked and does not count when the item is rated (a supply such as thread or solder always counts as there).

**Trying it.** A finished item comes off the bench untried: its evaluation is kept with it from the start and shows
once it is tried - at the bench (*Try it*; the jeweller's *Inspect & finish* tries a jewel as it finishes it), or later
from the bag (*Try that*, the `InspectItem` request) at a bench where such things are made. Its maker is whoever
finished it (`AssemblyState.maker`, which its record names): their journal notes it as made, with its tier, only then -
wherever they are in the world; whoever else tries it learns only what it is, and a maker away when it is tried on the
bench hears of it when they take it. It is named as its maker knows it (`CraftEvaluator.KnownName`): its metal only if
they know the metal, a jewel's stone by the name they gave it, by sight, or by what it looks like; its parts likewise.

**Giving it up.** An item nothing has gone into yet (`CraftAssembly.IsEmpty`: nothing fitted, added, worked or buffed -
a design started by mistake) is put aside at any station (`CraftWorkshop.PutAside`); the measurement it took when it was
started waits again for the next item started (unless a newer one of that kind already waits). Anything else is thrown
away (`ScrapPiece`) only at a test station.

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
* **Workmanship** — how close each part is to its intended shape (profile, length, mass, eye, edge), its finish;
  a jewellery design's metal parts by the jeweller's own work instead (`CraftJewelry.PartWorkmanship`).
* **Integrity** — what the defects leave of it (cracks, inclusions, porosity, burns, delamination, scores…): each
  part's, and the item's as their importance-weighted *geometric* mean - one broken part drags the whole down, sound
  fittings do not make good a blade cracked through. A part less sound than `CraftEvaluator.Unsound` (0.5) is noted as
  what will break (*Blade: cracked - it will break in use*; full of pores, of slag; wood or leather weakened), and an
  item that unsound is rated by it: its quality × `SoundnessFactor` (all of it from 0.5 up, a quarter at nothing) - a
  dagger whose blade cracked in the quench rates Poor, however fine its guard and grip; its durability falls with it.
* **Edge** — sharpness, angle, symmetry, chips, burr (weapons and tools); a blunt head (a mace's, a war hammer's) is
  rated by its striking face's hardness instead, and its inspection calls it so.
* **Heat treatment** — hardened where it should be, tempered, not brittle (or not hardened at all).
* **Assembly** — the quality of every joint.
* **Protection** — for armour: thickness, hardness and toughness of the plates, the leather's tannage and stiffness,
  padding, studs - each weighed against what a well-made part of its spec gives (`Reference`: a plate as its profile;
  leather of the middle of its range, tanned through, middling stiff and strong; a part cut from cloth a sound plain
  weave as supple and as strong as wool cloth, `ClothStiffness` and `ClothStrength` - no cloth comes near leather for
  its thickness, so a flawless wool tunic guards as its design says and canvas better). Only what guards counts
  (`Guards`): the plates and panels, the leather a plate sits on (at 0.4), and in an item of nothing but straps (a belt,
  a sash, a sword belt) its straps - a jerkin's shoulder straps, a shield's enarmes, a breastplate's straps hold it on
  and guard nothing (their leather still bears its resistances). The Defense is the design's as far as its parts
  guard so: more where they guard better (hardened in hot water, thicker, a stronger cloth), never more than half again
  its design's (`GuardsAtMost`), × (0.7 + 0.3 × the assembly); the durability is the design's × soundness × assembly ×
  material - and, of leather, how well it sheds water (0.7 + 0.3 × its water resistance; cloth was never tanned and is
  not judged so); for jewellery, the stone's quality.
* **Fit** — clothes and rings (of jewellery, only a ring): measured or not, and how well (see *The tailor's*).
* **Seams** — stitches as strong as their thread, and only if the thread suits the material: a material t mm thick
  wants a thread of strength 0.6 + 0.2 t up to 3 mm, then 1.2 + 0.05 (t − 3) (`CraftTextiles.ThreadNeed`: linen to 2 mm,
  waxed thread to 5 mm, hemp twine to 6 mm, silk to the thickest sole). A buckle counts as riveted on, or as sewn with
  its 4 stitches; padding counts by the strength of the padding added; solder by its own (× 0.85-1.03); a wrap of
  thread by its thread; a bowstring by its strength against the bow's draw (a weak one stretches and snaps). Holes
  out of line cost a riveted joint as they cost a seam, once (× (1 − 0.5 × the error): each rivet's quality holds its
  hole's alignment). A joint is rated by all it takes; the bench colours a joint in progress by the work done on it so
  far (`JointQuality(..., soFar: true)`: a quarter of a seam sewn well reads as well sewn, holes with nothing in them
  yet as true as they were punched).
* **Weapons** — the main part's mass makes it hit harder only up to the design's heaviest (`massMax`) or half again
  its reference mass: an overweight head swings slower and hits no harder. A wooden weapon (a bow, a staff, a club)
  has no edge and no heat treatment: its **wood** is rated instead - for a bow a springy, tough wood (its draw is the
  stave's section and spring), for a club or staff a tough, hard one; damp wood less. A main part of several pieces
  (three throwing knives) is rated as their mean. Studs set in armour protect as well as they were set.

They give the quality - raised by the finisher's **finishing touches** (`AssemblyState.mastery`, kept with the item when
it was finished: the quality gains `finishingTouch` × mastery of what it still lacks; *Practice*) - and its tier (Crude,
Poor, Serviceable, Good, Fine, Superior, Masterwork) and the item's numbers: damage, critical chance and multiplier,
knockback, attack speed (from mass and balance), tool damage, element (from materials with an affinity, or what it was
quenched in), durability, defence, magic defence, resistances, weight, price (the design's price × (0.3 + 1.4 × the
quality), and the worth of its parts on top by the one value rule for metal, wood, hide and cloth, stone - `CraftValue`,
chapter 05 *What things are worth*). A missing part makes an item incomplete (it cannot be finished; asked anyway, it
rates very low).

Its name is the design's with what its main part is made of: a metal ("Steel arming sword"), a wood ("Yew longbow"),
the animal's leather in place of the design's own "leather" ("Cow leather cap", "Hardened cow leather cap"), a cloth in
place of the design's own cloth ("Wool shirt"); its colour is its main part's (a metal's own; leather or cloth as it is
coloured, dyed as far as the dye took - `CraftLeather.Colour`), and so is its icon. Trying it tells its qualities in words and every note on it, once each
- its joints, its defects in words, its edge (a blunt head's striking face), its fit, its wood
(`CraftEvaluator.InspectionLines`).

## Practice

Each player's practice in each craft (`CraftMastery`, kept in their journal: chapter 04) is used where the rules run -
the station, or the server in a network session - so a client cannot claim more than it has:

* **Which craft** (`CraftMastery.OfRequest`): from the request and the piece it is on - grinding, filing, polishing,
  sawing to length (`Split`), coating or brushing wood is woodwork, steel smithing (a wooden piece cut so is *Sawn to
  length*); pouring, from the crucible or the furnace's pool (`TakeMelt`), is smithing; cutting cloth is tailoring,
  leather leatherwork; the tape is tailoring, the ring
  sizer jewellery; work on an item is the item's craft (`OfDesign`: jewellery for a jewellery design, otherwise the kind
  of part that matters most - metal smithing, wood woodwork, a stone jewellery, cloth tailoring, leather leatherwork).
  The bellows, the fuel, waiting, putting and taking are nobody's craft.
* **The hands** of that request are as steady as that practice (the guard's floor and the window's pace too).
* **More with each action** (`CraftMastery.ByPractice`): a JointWork request names its tool in `i1` - 1 the needle (it
  punches and stitches a run of `StitchesPerAction`: 1, 2 from Journeyman, 3 for a master; its pull that pierces as it
  draws keeps its `flag`, which nothing else does), 2 the pricking iron and awl (the iron punches `HolesPerBeat` = 4
  holes a beat, the awl a run of stitches) - and the count is multiplied there, with the run's pulls past the cut (`i2`)
  (the workshop's operation records the stitches, so replays are exact); a WeaveCloth request's beats are each repeated
  `RowsPerBeat` times (at most 30 rows).
* **Routines** (`CraftRequestKind.Routine`, `i0` = the `CraftRoutine`): refused below the rank they need; otherwise
  `CraftMastery.DoRoutine` does the ordinary operation at the quality practice gives (`RoutineQuality` ± 3 % from one
  time to the next): the skein counted a little short (never long), the wearer measured near their size, the fibre
  picked or teased, the crust brushed sector by sector as long as each needs, the stone searched on a grid as fine as
  the practice at every depth.
* **Experience**: each request done teaches its craft (`WorkExperience(request, piece)`, judged on the piece as it is
  before the request runs: 0.01 a blow or stroke, 0.04 most work, 0.12 a cut, a weld, a facet tier, a bend, a seat, a
  stone named; 0.25 a bobbin, a pour (from either), a bloom, a length of cloth; nothing done by routine, nothing for
  reading, measuring or planning - testing or searching a stone, planning its cut, the tape, laying a pattern out - and
  nothing for a step done again, a skein counted again or a stone named again), no faster than `workPerMinute`; a
  finished item `ItemExperience` (`DesignWork` × the repeat factor: chapter 04); a wrong name for a stone a quarter. A new rank is told in a line of its own (*"You are an apprentice tailor now. You can wind and count a skein
  by routine."*).

## Reproducibility and saving

The same operations at the same steps, with the same rules, tools, ambient and seed, give the same workshop exactly:
`CraftWorkshop.Replay` rebuilds one from its history; `Capture`/`Restore` save and continue a workshop exactly (the state
is JsonUtility-friendly). Stations save their state (without the history) and continue after a restart. Pieces brought
in from elsewhere get new ids when theirs are taken, keeping an assembly's parts attached.
