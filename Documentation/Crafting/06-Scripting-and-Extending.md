# 06 — Scripting and Extending

## The workshop (`Crafting/Core`)

```csharp
CraftRules rules = CraftDatabase.Instance.Rules;
var ws = new CraftWorkshop(rules, CraftTools.Smithy, ambient: 18, seed: 1);

string bar = ws.AddPiece(CraftDefaults.Bar(rules, 200, 30, 6, ("iron", 0.9895), ("carbon", 0.0095), ("manganese", 0.001)));
ws.AddFuel("charcoal", 6, out _);
ws.Bellows(1);
ws.MovePiece(bar, CraftLocation.Fire, out _, depth: 0.7);
ws.RunUntil(90);                                     // seconds of workshop time
ws.MovePiece(bar, CraftLocation.Air, out _);
ws.Strike(bar, 0.5, CraftStrikeTool.PeenAcross, CraftStrikeFace.Flat, force: 0.9, tilt: 0, out string why);
ws.MovePiece(bar, CraftLocation.Quench, out _);
ws.Agitate(bar, CraftQuenchMotion.UpDown, out _);
ws.Advance(Time.deltaTime);                          // fixed steps, however long the frame was

CraftPieceReport r = CraftEvaluator.Piece(rules, ws.Piece(bar));   // hardness, toughness, edge, defects…
List<string> seen = CraftLook.Describe(rules, ws.Piece(bar), journal, CraftInstruments.None, dark: false, reveal: false);
```

Every operation returns `false` (or `null` for those that make a piece) with a `reason` when it cannot be done — and then
changes nothing. The operations:

| Area | Methods |
|---|---|
| Pieces | `AddPiece(piece, components)`, `RemovePiece`, `MovePiece(id, location, depth, from, to, vat, entryAngle)`, `TurnPiece`, `SetIntent(id, design, role)` (a metal piece takes the part's edges: `CraftPiece.IntendedEdges(rules, otherwise)` - the purpose's double 2, single 1, none 0 - as a pattern mould's casting, a fitting, a weld, a fold and the wheel's first bevel do too) |
| Fires | `AddFuel(fuel, count, furnace)`, `Bellows(strength, furnace)`, `SetDamper`, `SetOven` (held to the dial: `CraftOvenState.Dial` - 0 off, else `DialMin`..`DialMax`, 100-650 °C), `Agitate`, `FillTank(quenchant)` (the tank emptied and filled again); `CraftThermal.CrackHarshness(quenchant)`, `HarshQuench`, `HarshFor(rules, piece, tank)` (a blade of hardening steel still to harden over a quenchant that cracks it bare - the quench's warning, `CraftLook.HarshQuench(rules, piece, tank, offered)`), `PresetQuenchants(rules, tools, test)` (what the editor's presets keep) |
| Ore | `CrushOre(id, fragment, force, layout)`, `DiscardFragment(id, fragment, layout)`, `BrushOre(id, fragment, brush, coverage, layout)` (soft, stiff or wire: each brush goes over each part of a stone once, `OreFragment.Brushed`), `WashOre(id, flow, seconds, technique)`, `SieveOre(id, mesh, thoroughness)` - only on the bench (not in the furnace or the crucible, not hot); `layout` is the stamp of the bench as the hands saw it, so an aim at a stone that has since moved is refused. `CraftOres.Tray(stones, width, height, perCm, least, most, lay)` lays the stones where they lie (each at its `x`/`y`, as big as it is, pushed apart; `lay` keeps where they lay last, so a stone thrown out or broken leaves the others be), `MostPieces(lump)` is the most a blow can split a stone into now, `BrushedAll(brush)` what a brush that has been all over a stone says, and `OreFragment.RindShare` the share of a stone's rock that is the lump's weathered rind (shared by its broken stones) |
| Furnace and crucible | `AddFlux(supply, count, where, piece, coverage)` (where: 0 the furnace's charge - only a flux that `FurnaceTakes`, or a cupel -, 1 a cover over the melt, 2 and up a weld flux on a piece), `TapSlag(thoroughness)`, `ExtractBloom`, `LiftBead` (a bead too small to pour, the silver left on a cupel), `StirMelt`, `SkimDross`, `CanPour(fromFurnace)` (why not, before a pour starts), `Pour(mould, rate, steadiness, interruptions, spilled, overflow, filled, lost)` and `TakeMelt(...)` from the furnace (what the hands measured: the metal lost, the run-over, how full), `KnockOut` (a melt frozen in the crucible comes out as a cake); `CraftSmelting.CrucibleTakes(rules, piece)` (which ores a crucible melts), `MoldMass(rules, mould, density)` (what a mould takes), `Fumes(element, t, ore)`, `MixedAfter(mixed, before, added, difference)` and `Difference(a, b)` (how a melt is mixed once a piece melts into it), `Choke(workshop)` and `FinesInCharge(workshop)` (the furnace's draft, choked by slag and fine ore), `PourVerdict(rules, cast, pour, need, mould, fromFurnace)` (what came of a pour, in words: the window's and the 3D pour's), the thresholds `PoreGas`, `Streaky`, `StreakWeakness`, `GoodPour`, `EmptyCrucible` |
| Forging | `Strike(id, position, tool, face, force, tilt)` (tool `Chisel`: the hot chisel cuts like the hardy, from above - `MetalSegment.cutFromAbove`; wood takes only `Straighten`, as far as `CraftForging.WoodGive` says it gives; how metal gives at its heat: `CraftForging.Gives`, `ColdPlasticity`, `BarelyMoves`, and a blow's words `BlowFeel(rules, piece, segment)`), `FluxPiece(supply, count, piece, coverage, perSegment)` (a welding flux, stretch by stretch), `Fold(id, set, open)`, `Weld(id, other, set, open)` (`open`: the seam is left to the blows - `CraftForging.SeamOpen`, `WeldFactors`), `CanFold`, `CanWeld`, `SplitPiece(id, position)`, `FormFastener(id, kind)`, `CanFormFastener` |
| Surface | `ApplyClay`, `ClayGoesOn` (would that coat put clay on - only then is a clay item used), `Descale`, `Grind(id, from, to, side, wheel, angle, pressure, seconds)`, `File(…)`, `Polish`, `Coat`, `Sharpen(id, from, to, side, stone, angle, pressure, strokes, wobble, edgeLeading)` |
| Leather | `FillVat(vat, agent, doses)`, `CanFillVat(vat, agent)` (would it take that agent, and why not - asking refuses nothing), `DrainVat`, `SetVatHeat`, `StirVat` (never less mixed than it was), `Scrape(id, path, grainSide, pressure, angle)`, `Stretch(id, n, e, s, w)`, `Stake`, `Condition`, `Dye`, `Slick`, `Skive`, `CutPattern(id, pattern, x, y, rotation, accuracy)`; the rack takes `CraftLeather.RackHides` (4) hides |
| Defects | `CraftPiece.AddDefect(kind, at, severity, surface, cause)` (a harm done again at a place: worse), `HoldDefect(...)` (a state the piece goes on being in - rot, a closed tan, shrinking, burning, hammered-in scale, ground too thin: as bad as it is now, never stacked on itself) |
| Assembly | `StartAssembly(design)`, `Attach(assembly, role, index, piece)`, `Detach`, `JointWork(assembly, joint, work, count, a, b)`, `Finish`, `PutAside(id)` (an item nothing has gone into yet - `CraftAssembly.IsEmpty`: nothing fitted, added, worked or buffed - given up; the measurement it took waits again, a `MeasureFit` op with `flag` set) |
| Time | `Advance(seconds)`, `RunSteps(n)`, `RunUntil(seconds)`, `Suspend`, `Resume`, `SetAmbient`, `SetVariable` |
| Saving | `Capture()` → `CraftWorkshopState` (`ToJson`/`FromJson`), `Restore(rules, state)`, `Replay(rules, tools, ambient, seed, id, history, untilStep)`, `ForgetHistory(keepLast)` |

`CraftLeather` holds the rules the tannery and the cutting table show: `HairLeft(def, meanHair)` / `HairLeft(rules, hide)`
(how much of its hair a hide still has, by `UnhairedBelow` - the path's Unhair step, the guide, the look and the
evaluator's `CraftPieceReport.HairLeft`), `HairHolds(cell)` (where the hair knife scuffs at any pressure), `Preserved(ws,
piece)` / `Rots(cell, preserved)` / `RotWarmth(celsius)` / `Rotting(ws, piece, out spoiling)` (what rots, how fast, and
whether a cell spoils - past `SpoilsFrom`; `SmellsFrom` the warning before), `Flatness(pulls)` (how flat the frame dries
it: its slackest side), `GrainColour(rules, hide, cell, fleshShows)` / `Colour(rules, hide)` (the leather's or cloth's
own colour, dyed: the drawing, the item report's `Color` and its icon), `ChalkAt(hide, pattern, x, y, rotation)` (a
pattern chalked at that very place: a cut there is `ChalkedCut` cleaner), `WorkMarks(piece, least)` / `Worked(cell)` (what
marked each cell - `ByKnife`, `ByFrame`, `ByOther` - and the work's share of its damage), and the constants
`CaseHardensBelow`, `PreservesFrom`, `SkivePass`, `SkivedThrough`, `CleanCut`/`RaggedCut`; `CraftEvaluator.HairNoted` and
`PatchyDye` are where the item notes the hair and a patchy dye.

`CraftSurface` also holds the rules the Grind and Sharpen areas show, so the window shows what the workshop does:
`GrindBevel(segment, left, area, angle, edges)` (the bevels as planes), `Judge(segment, width, thickness, angle, edged,
edges)` (a stretch against the design, as the evaluator judges it: `Progress` ToGrind / Done / Off / TooFar - an edge
not yet met is ToGrind however short of the design the stretch is, `Judgement.under` telling the undersize apart - and
`want`, the angle a side it judged by), `EdgeHalfAngle(width, thickness, angle, edges)` (the angle a side the design's section
carries there: its own, steeper where the section is too narrow for it - what `CraftArt.TargetAt` gives the areas and
the evaluator's `IdealEdgeAngle` averages), `PolishOf(rough)` (on a log scale), `FinishOf(rough[, polish])` /
`FinishWords` (the surface's words: one ladder for the areas and the look), `GritDone(grit, next)` (where a grit has done
what it can), `KeenStep(sharpness)` / `KeenWords` (blunt, sharp, keen, razor-keen: the stone, the look, the notes, the
guide),
`TemperRunsAt(rules, zone)` (the heat a hardened edge's temper runs at), `HasEdge`, `Contact(held, bevel)`,
`BurrOff(pressure)`, `SharpenFloor(rules, piece, segment, stone, wobble)`, `NextHone(...)` (what to do next on the stone:
`WearFlat` past `EdgeMet` - back to the wheel -, `WearLastFlat` the hair of flat the strip calls done),
`WorkedHardness`, and their constants (`MaxBevelAngle`, `StoneAngleMin`/`Max`, `WheelTooHot`, `FileTooHot`, `StoneTooHot`,
`GroundPolish`, `GritReach`, `CoatingBurns`, `BurrShows`, `BurrOffBelow`/`Full`, `FlatGone`, `EdgeGround`, `EdgeMet`,
`StoneBite`, `IdealTemper`, `DrawnSoft`). `CraftThermal.Hardening(rules, piece, tank)` says whether going into a tank
at that heat is a quench (austenite it still turns to martensite: more than `HardeningShare` of a zone) or a dip to cool
it; `CraftSurface.DipQuenches(workshop, piece)` is the wheel's dip's own check (refused to a piece that can still harden,
with `DipWouldQuench(quenchAt, ashes)`'s words).

`EventRaised` (and `RecentEvents`) report what happens — `Struck` (with the sound: ring, thud, dead, crack), `Cracked`
(its id the cause: `CraftForging.CrackId` + burnt, castiron, bloom, sulfur, hotshort, phosphorus, coldshort, cold,
overworked or hardening), `Defect` (a flaw of a kind first showing on a piece - its id the `CraftDefectKind`),
`MagnetLost` (steel in the fire past its Curie heat; no text), `Burning`, `Melted`, `Quenched`, `QuenchFlare`,
`Tempered`, `Smelted`, `SlagTapped`, `Poured`, `Ground` (with the sparks), `Sharpened`, `Scraped`, `Tanned`, `Dried`
(id `stiff` when it dried stiff), `Torn`, `Joined`, `Finished`, `Refused`, `Note` (its id what it is: `fold`, `seam`,
`unfluxed`, `warp`, `temper_colours`, `blick`, `CraftSmelting.FumesId` + the element boiling off, `alum`, `blotch`,
`skate`...)… Their text is what can be seen or heard, never a hidden number (a few - `MagnetLost`, `unfluxed`, `alum`, a
coarse grain - have none: only the journal hears them). `CraftJournal.InsightOf(event, piece, kit)` is what one teaches
(the kit: the station's instruments - the magnet's lesson needs a magnet); `CraftJournal.Learn(journal, id, settings,
craft)` learns an insight as practice in `craft` (else the insight's own, `CraftMastery.OfInsight`); `Worded(record,
journal, rules)` is a record line in the player's words (heats by their glow, a vat by its strength and warmth: the
journal's procedures).

`CraftEvaluator.Item(rules, ws, assembly)` is what a finished item is (`CraftItemReport`): its soundness the parts'
importance-weighted geometric mean, a part under `Unsound` noted as what will break and the quality × `SoundnessFactor(integrity)`. `CraftJournal` holds the
learning rules over a `CraftJournalData`. `CraftValue` (in `CraftPiece.cs`) is the one value rule for stock, worked
pieces and finished items' materials: `Metal(profile, grams)` (grams × the elements' value of a gram, and the class's
making on its matrix: + (multiplier − 1) × `CraftAlloyProfile.MatrixPricePerGram`, the base and what is alloyed in no
dearer than it - so a dear metal alloyed into a cheap one is worth its parts), `Wood(wood, volume, moisture)` (its share of a blank, half while unseasoned), `OreMetal(rules, ore, at)`
(the metal a lump gives, 0 the poorest lump of the poorest deposit .. 1 the richest), `WholeHideCells(rules, hide)` and
`Of(rules, piece)` (a piece on its own); `CraftPiece.HideDensity(cloth, moisture)` weighs a hide or cloth. The stock
sizes: `CraftDefaults.Log(rules, wood, length, diameter)` (0: the stock log, `LogLength(wood)` - as long as the
species' blank, at least 900 mm - and `LogDiameter`, 240 mm), `BlankSize(wood)`, `CraftStockItemSO.BarSize` and
`UnsetWoodSize(size)` (an old blank asset at the bar's size takes the species' blank). `CraftContent.AddMissing` also
brings the alloy effects a database lacks (`AddMissingEffects`, `SameEffect`).

The metal journeys' helpers: `CraftForging.Consolidated(rules, piece)` (a bloom hammered sound - every stretch's pores
under `SoundPores`, its slag under `SoundSlag` of it: `CraftPiece.bloom` is cleared on the blow that gets it there) and
`CraftLook.Bloom(rules, piece)` (a bloom still: what the look and `ShapeWord` call one - a piece's purpose names it
first); `CraftSmelting.ChargeUnmelted(workshop)` and `CrucibleGasChange(workshop, superheat)` (solid metal still in the
crucible chills the melt: no gas taken up until it has run; `CraftLook.Crucible(rules, crucible, journal, thermometer,
workshop)` says so).

The review's helpers: `CraftMetallurgy.MainEffect(rules, profile, property)` (the element adding most to a property:
what makes a metal hot- or cold-short); `CraftEvaluator.CoarseGrain`, `MeanGrain(piece)` and `CraftItemReport.CoarseGrained` (an item with a steel part left
coarse: the journal's *normalise*);
`CraftLook.BulkDensity(ore, lump)` (a lump's heft), `AssembledMass(rules, workshop, item)` (an item weighed with its
parts; `Describe(..., workshop)` passes it to the balance), `VatStrength(strength)` (strong, medium, weak, spent);
`CraftLeather.DriedStiff(hide)` (`DriesStiffFrom`) and `BlotchesFrom`; `CraftThermal.QuenchWarpTag`, `WarpShows`,
`TemperColourFrom`; `CraftForging.FluxedSinceSeam(piece)`; `CraftRules.SpinningAddsAtMost` (the check on thread and
cloth prices); `CraftHands.SkillSteadies`; `CraftMastery.WorkExperience(request, piece)` (judged on the piece before the
request), `DesignWork(design)`, `RepeatFactor(timesMade)` and `ItemExperience(design, quality, settings, timesMade)`
(with `CraftMasterySettings.workPrice` and `repeatsHalve`), `Benefits(craft, mastery, settings, timing)`.

### Fibre, gems and jewellery

```csharp
// The fibre bench and the wheel.
string fleece = ws.AddPiece(CraftFibre.NewFibre(ws.Rules, "wool_fibre"));
ws.CleanFibre(fleece, picked: 0.9, lost: 0.05, out _);
ws.ScourFibre(fleece, celsius: 62, minutes: 30, agitation: 0.1, shock: 3, detergent: "soap", rinses: 3, out _);
ws.CardFibre(fleece, strokes: 12, pressure: 0.3, evenness: 0.9, out _);   // or CombFibre for long staple
string bobbin = ws.SpinFibre(fleece, evenness: 0.85, twist: 0.5, fineness: 0.6, grams: 30, out _);
ws.PlyYarn(bobbin, otherBobbin, null, plyTwist: 0.4, tension: 0.9, out _);
ws.MeasureYarn(bobbin, countedMetres: 120, out _);                       // then the station's ReelYarn gives thread
ws.JoinYarn(bobbin, anotherBobbin, out _);                                // same fibre and plies: one bobbin for a long warp
string cloth = ws.WeaveCloth(bobbin, width: 6, tension: 0.9, density: 0.5, beats /* a score per row */, out _); // at a Loom

// The lapidary.
string gem = ws.AddPiece(CraftLapidary.NewRough(ws.Rules, "ruby", seed: 7));
ws.CleanGem(gem, pick: false, work /* 16 sector amounts */, control: 0.9, out _);
ws.TestGem(gem, CraftGemTest.Refractometer, care: 0.9, point: 0, out _);
ws.IdentifyGem(gem, "ruby", out _);
ws.EvaluateGem(gem, looks /* x, y, depth triples */, steadiness: 0.9, out _);
ws.OrientGem(gem, "oval", tableAngle: 0, x: 0, y: 0, radius: 0.6, out _);
ws.SawGem(gem, angle: 0, offset: 0.66, steadiness: 0.9, coolant: 1, pressure: 0.3, out _, misses: 0);
ws.CleaveGem(topaz, angle, offset, force: 0.52, out _);                    // or snap: true for a stone without cleavage
ws.GrindGem(gem, touches /* (angle °, amount) pairs */, coolant: 1, steadiness: 0.9, out _);
ws.FacetGem(gem, tier: 0, facets: 8, angle: 41.4, indexError: 0.05, meet: 0, coolant: 1, out _, asymmetry: 0.1, misses: 0); // the tier's own facets are cut
ws.PolishGem(gem, "alumina", passes: 12, pressure: 0.4, speed: 0.5, out _, regions /* the seconds on each of 9 regions, or null */);
CraftGemGrade grade = CraftLapidary.Grade(ws.Rules, ws.Piece(gem));

// The jeweller.
ws.FormShank(wire, CraftJewelPart.Band, size: 18, roundness: 0.95, seamGap: 0.02, prongs: 0, out _);
ws.SeatSetting(bezel, gem, diameter: girdle + 0.02, level: 0.95, clean: 0.9, out _);
ws.MeasureFit(null, accuracy: 0.95, size: 18, out _);                    // the next item started takes it
string ring = ws.StartAssembly("bezel_ring", out _);
ws.JointWork(ring, "seat", CraftJointWork.Solder, 1, a: 1.0, b: 0.9, out _);   // a: the peak heat (1 = the solder just flows), b: how well fluxed
ws.JointWork(ring, "set", CraftJointWork.Push, 20, a: 0.6, b: 0.9, out _);      // a: the pressure, b: how evenly round (set from CraftJewelry.SetSecure)
ws.FinishJewel(ring, buff: 0.9, clean: 0.9, out _);
ws.Finish(ring, out _);
```

The lapidary's own rules, for a window or hands of your own: `CraftLapidary.LineThrough(ax, ay, bx, by, out angle, out
offset)` is a line drawn across the plan as the saw and the chisel take it (`NormalLine` keeps a line drawn either way as
the one line), and `LineCuts(gem, angle, offset)` whether it runs through what is left of the stone (`SawGem` and
`CleaveGem` refuse one that misses, at no cost); `TierFromHits(hits, ...)` is a tier of facets from its hits on the bar,
and `CraftWorkshop.TierFacets(id, tier)` the facets the tier is cut with, whatever a request claims;
`MarkSearched(gem, searched, x, y, z, reach)`, `SearchedShare(gem, searched, more)` and `InStone(gem, x, y)` count the
loupe's search as the SEARCHED meter does (`SearchCells`, `SearchDepths`, `FocusDepth`); `PassSeconds` is a pass of the
lap (`PolishGem`'s regions are its seconds on each), `ChargesLap(gem, compound)` whether a pass charges the lap afresh
(`GemState.compound`), `SoftCompound` the fit the lap slides at, `WorseMeet` how heat and polishing on round a facet's
meet, and `BotchAt(tier)` where a tier's botched facets are kept among the flaws (`CraftFacetRecord.botchChips`: what a
recut grinds away). The jeweller's: `CraftJewelry.SetSecure` (a stone set: the joint done, the inspection and the item
alike) and `SeatHolds` (a seat too wide ever to set it); a solder's flow point is `CraftSupplyDefinition.flowTemperature`
(`FlowTemperature`: 760 °C when none is given).

The woodshop adds `DebarkWood(id, from, to, depth, steadiness, round)` and `SplitLog(id, offHeart, alongRays, solid,
blows)`; the tailor's table `LayoutPattern(id, pattern, x, y, rotation, clear)`. The station's tool quality is the
workshop variable `toolQuality` (`CraftWorkshop.ToolQuality`); its humidity `humidity`. New request kinds (append-only,
after Wait): Carve, BrushOre, Rive, BendWood, SawCut, StackWood, SandWood, Varnish, Spin, DebarkWood, SplitLog,
CleanFibre, ScourFibre, OpenFibre, CardFibre, CombFibre, SpinFibre, PlyYarn, MeasureYarn, ReelYarn, LayoutPattern,
MeasureFit, TestGem, IdentifyGem, EvaluateGem, OrientGem, CleaveGem, SawGem, FacetGem, PolishGem, FormShank,
SeatSetting, FinishJewel, CleanGem, GrindGem, WeaveCloth, then Routine, HandsOn, FillTank, JoinYarn (`piece` the bobbin
wound onto, `other` the bobbin wound on), StackCover (a setting: `a` how far the drying stack is covered, 0 open sun - 1
all shaded), RipCut (`a` and `b` where the taper's line starts and ends along the board, `c` and `d` its widths there in
mm, `i0` the saw's wander in thousandths) and InspectItem (`itemId` the runtime id of a finished item in the bag, tried
at a bench where such things are made) (SawGem's `i1` carries the misses, FacetGem's `i1` the botched facets and `i2`
the asymmetry × 1000, CleaveGem's `i0` 1 for nick-and-snap, PolishGem's `points` the seconds on each of the 9 regions, WeaveCloth's
`points` the rows' beats and `i1` how many); a PutItem with no item and a fibre id lays a batch of that fibre on the
fibre bench. A project item that is a fibre by its name ("Raw Wool", "Wool", "Fleece", "Flax", "Cotton bale") is laid
down as a batch at a fibre bench or a wheel, and the wheel spins a batch lying on the station even when none is picked.
An item whose copies differ in quality implements `ICraftFibreQuality` (`float FibreQuality(int code)`: 0-1, below 0
unknown): each copy laid down makes a batch shaped by it (`CraftFibre.Grade`: quality 0.6 is the supply's typical batch,
1 has half its dirt and short fibre, 0 three quarters more). Animal husbandry's wool does, from each fleece's quality.

**The timing bar** (`CraftTimingBar`, settings `CraftTimingSettings` at `Rules.Settings.timing`): build one, add
`CraftTimingTarget`s (`ideal`, `size`, `best`) or call `NewTargets(seed, round, safe)`, `Tick(dt)` it and `Hit(hands)`
when the player strikes: a `CraftTimingHit` with `score` (0-1), `grade`, signed `offset`, `position` and `damage`.
`Score(distance, target)` and `Zone(grade, target)` give the curve itself; `CraftMode.DrawTimingBar` draws one.

UI: `CraftFibreMode`, `CraftLapidaryMode`, `CraftJewellerMode`, and new places in `CraftWoodshopMode` and
`CraftTailorMode`.

### The woodshop and the tailor's

`CraftWorkshop.Rive(id, stray, steadiness)` (returns the other half), `SawCut(id, position, wander, square)`,
`BendWood(id, from, to, degrees, speed, strap)`, `StackWood(id, stickers, sealedEnds, shade)`,
`SandWood(id, from, to, grit, strokes, alongGrain)`, `Varnish(id, supply, from, to, evenness, thickness)`; moving a
wooden piece to `CraftLocation.Steam` steams it, to `CraftLocation.Stack` seasons it. `CraftWoodwork.Plasticity`,
`BendStrain` (the same strain the window's gauge shows, at the pace the bend is judged by), `FinishProtection`,
`Sealed(wood)` (a coat as the look sees one), `Seasoning(piece)` (its pace in the stack for its thickness) and
`LesserSize(piece)` read a piece; `SplitsWithWedges` / `RivesWithFroe` say whether the wedges or the froe take it (and
why not), `WedgeLineAcross(piece, dx, dy, out reason)` whether a wedge's line drawn so runs across the side the wedges
part, `StickerEvenness(stickers)` how well stickers laid at those places (0-1 along the board) hold it flat (the window's
STICKERS), `StrokeWander(points)` how far a knife stroke strayed from its own line (with `out last`: where its newest
point lies off it, signed - the 3D knife rides there), and `StrokeSteady(points, tolerance, unsteadiness)` the
steadiness a stroke is sent with (the window's and the 3D bench's one rule: its *Straight* meter; `CleanStroke`, 0.75,
is where it shaves clean). `CraftSawStroke.StrokesFor(width, thickness)` is the strokes a cut across that section takes (one
argument: a rip through a thickness); the saw goes on in its kerf after `Stop` and `Resume`. `CraftTextiles.IsCloth`,
`OnGrain`, `SpinQuality`, `TwistTrue` (how far off right a twist stays well twisted: the wheel's green band),
`ThreadTier(quality)` (coarse, even, fine) and `Spun(rules, fibre, evenness, twist, prep)` (the thread a hank becomes);
`FitOf(measured, accuracy)`, `FitWorthIt`, `FitCareful` (the fit of a measure, as the evaluator rates it) and
`MadeToMeasure(design)` (a garment: what takes a chest's measure, `CraftJewelry.Takes`, and what its fit counts for); `Overlaps(rules, hide, pattern, x, y, rotation, except)` (the chalk a cut would rub out, leaving out its own),
`ClothPattern(rules, pattern)`, `PatternsWanted(ws)` (the cloth parts the unfinished items still want) and
`ClothSeam(ws, assembly, joint)` (a seam of cloth parts: the needle's). Spinning from the bag is a station request
(`CraftRequestKind.Spin`: one fibre item laid as a batch and a bobbin spun from it, `c` the draft); the wheel's batch is
`SpinFibre`. `CraftFibre` gives the rules the window shows: `MetresSpun(grams, fine, prep)`, `DraftEvenness(prep,
fine)`, `BobbinGrams`, `CardGrip(press)` and the carding bands (`CardTouch`, `CardLight`, `CardNeps`, `CardBreaks`,
`CardHard`), `ScourHeat(fibre, out from, out full)`, `SoakMinutes`, `SoapAfter(soap, detergent, rinses)`, the shock
(`ShockFrom`, `ShockFelts`, `ShockFull`), `SoakMinutesPerSecond` and `MaxSoakMinutes` (the soak the station grants),
`PickedOut(share, toolQuality)` (the dirt a pick takes), `OpenGive(fibre)` (how far felted or wet fibre opens),
`PlyBalance(ply, singles)`, `PlyBand`, `SinglesTwist(strands)`, `MixedTwist(parts)`, `RowsFor(metres, width)` with
`MinWidth`, `MaxWidth`, `MaxRows`, `WeaveSoundness(rules, yarn)` (how sound a cloth that yarn weaves is), `WeakYarn`,
`RowSoundness(beat)` and `WorstedStrength`; `CraftWorkshop.CanJoinYarn(onto, from, out reason)` asks without raising
anything. `CraftAssembly.StitchTensionIdeal` and `StitchTensionCuts` are a stitch's best pull and where it cuts;
`CraftWorkshop.JointWork(..., pierce, spacing, align, pastCut)` sews with the needle (its stitches pierce the holes they
need, spaced and lined up as given) and counts the stitches pulled past the cut (-1: by the mean pull) - the request is
a JointWork Stitch with `i1` = 1, `flag` (pierce), `c`/`d` (spacing, alignment) and `i2` (pulls past the cut);
`CraftAssembly.WhyNot(..., piercing)` counts holes the needle may still make. `CraftEvaluator.ThreadSuitsWell` is where a
seam's thread is called too weak or too heavy in the item's notes. `CraftRequestGuard.TrueCap(floor)` is the truest a beat
of the reed, a warp's tension or the tape can be (the windows show it), and `SoakSlack` the soak's room for lag.
`Attach(..., supplyCount, supplyStrength, supply)` records how strong a supply was and, by its id (`supply`; unnamed, the
first of its kind), what it weighs and is worth in the item (`CraftWorkshop.GramsSuffix`, `WorthSuffix`);
`CraftAssembly.SupplyIn(rules, assembly, spec, out grams, out worth)` reads them for a supply part and
`SuppliesMass(rules, item)` for all of an item's, and `CraftSupplyDefinition.ItemGrams` is what one item of a supply
weighs (its `itemGrams`, or by its kind: `CraftFibre.SkeinGrams` a skein of thread). The item's report weighs them and
is worth them; its `PartPieces` list them with no piece (named by their role), and `KnownParts` names them.

The armour's rules: `CraftEvaluator.GuardsAtMost` (the most times its design's Defense an armour gives),
`ClothStiffness` and `ClothStrength` (the plain cloth a cloth part is weighed against). The lapidary's
`CraftLapidary.CutKeeps(cut)` is the share of what lies inside its outline a cut keeps (`PlannedCarats` × it, ground or
not). The leather's `CraftLeather.CracksStakedDry(rules, hide)` says whether staking it dry cracks its grain
(bark-tanned): the guide then sends a hide that dried stiff to be oiled.

The assembly's hammer and joints: `CraftAssembly.DriveEffect(r)` is how much of a joint's driving work a blow of `r` ×
its ideal does (a wedge, a socketed head, a guard's slot: in proportion up to `GoodHigh`, less than its share under
`TooLight`), `PeenEffect(r)` the pommel's spread (no more past `GoodHigh`), `StruckWell(joint)` how true its blows were
(0.75-1, from `JointState.blows` and `blowQuality`, the mean `BlowQuality` of the blows that drove, peened or closed it).
`PlayLeft(spec, joint)` is the play left between a head and its haft (a loose eye's gap less what its wedge takes up,
`WedgeTakesUp` × how far it is driven; a socket's as it went on), tight up to `PlayFree`; `HaftFitWords(spec, gap)` says
so as the part goes on. A socket's pins: `PinsWanted(design, spec, pieceIn)` (or `(ws, assembly, design, spec)`: the
design's pin part's count when it is fitted, else 0), `RivetsWanted(spec, design)` (a socket's pin part's own count),
`PinLeft(...)` (driven on, a fitted pin not yet set - `Missing` lists it), and `JointComplete(spec, joint, pins)`.
`FastenerState.used` counts the rivets of a set already set into its item (they stay in its `count`, its mass and its
worth) and `Left` those still to set; at the finish those left over come off as a loose set (the Finish operation's
`other` is its id, a second set's that id with `.1`). `CraftAssembly.SeamThickness(ws, assembly, spec, out cloth)` says whether the
thickest part sewn is cloth. `CraftEvaluator.JointQuality(..., soFar)` rates the work done on a joint so far (what is
still to do left out; holes alone by how true they are) - the bench colours a joint in progress by it.

## The hand-skill models (`CraftMinigames`)

The station window turns the pointer into operations through these — use them for your own input (a gamepad, VR hands):

| Model | Input → output |
|---|---|
| `CraftHands` | a seeded tremble that grows with fatigue and shrinks with `skill` (the window sets it to the player's practice in the area's craft); `fatigueRelief` (a practised arm tires less); `Strength` (what a tired arm has left), `Slips(salt, rushed)` (a seeded slip once it is past half tired), `Level(fatigue)` / `LevelFrom(level)` (its steps: tiring from 0.35, heavy 0.6, spent 0.85 - the bar's colours and the words), `FeelingAt(level, blows)` (in words, for blows or the hand's other work; `Feeling` for blows), `Move(effort)` (a movement of the working hand), `Exert(fatigue)` (light work that is no movement of it: it tires a little and the arm still rests), `Rest(seconds)` (slow while working, faster when still) |
| `CraftHammer` | `Raise`, `Tick`, `Release(aim, tilt, hands)` → a `CraftBlow` (position, force, tilt, miss, rushed, slipped; `Words`); `Expected(hands)` is the blow as it would land if let go now (a tired arm lighter, `Rushed` - held so long that it `Wavers`, or struck too soon - weaker): what a blow bar shows, full at `FullBlow` (the hammer's weight's). The tilt comes from the hand's drag: `CraftForging.TiltFrom(drag, level, full)` - level within `level` of where it pressed (a hand drifts a little), a full tilt `full` beyond (the window: 8 and 60 px; the 3D anvil: 2 % and 12 % of the screen's height); past `FlatTilt` the flat face bevels |
| `CraftStroke` | samples (time, x, y, angle, pressure) → span, mean angle, wobble, pressure, passes, edge leading, a hide path. It tires the hands by the samples' own time (a movement every `TireEvery` s, the clock kept across `Clear`); a pass is a run of at least its pass length (`new CraftStroke(passLength)`, 0.2 by default) that turns back by `Turn`, at most `PassesPerSecond` a second; `PassRuns` (the passes that turned, each a `CraftPass`: from, to, times, mean angle, wobble, pressure, edge leading) and `Current` (the run under way, if it is one) let an area send each pass as it comes - `TakePasses(ref sent, ending, into)` hands over the passes not yet sent (and, at the end, the run under way if it is a pass): the window's stone and the 3D one alike; `Span(piece)` gives the segments covered by their real lengths, and `Dwell(piece, window, out from, out to)` a tool's time on each of them (the window shared out over the stretch: the Grind area and the 3D wheel) |
| `CraftHandTrail` | a 3D hand's recent path by time (a point kept only once the hand moved `MinStep`, the last `Keep` seconds counting): how far and how fast it moved, and the quench motion (up and down, side to side, round, held still) by the window's own rule, the same at any frame rate |
| `CraftHandCircles` | the circles a hand draws turning a bit: `Begin(x, y)` where it pressed, `Add(x, y)` (a point kept every `MinStep`), `Turns` (whole turns, `Spend` takes them off as they are sent), `Steadiness` (how evenly round, 0-1), `Wound` (how far round it has wound, for a bit drawn turning), `Turned`; counted round the middle of the circle that fits the last lap, so a circle counts wherever it is drawn and from wherever the hand starts it, a path along a line turns nothing - the window's bore and the 3D bench's alike |
| `CraftBrushCover` | the 3D ore stone's surface as the brushes work it, patch by patch: each brush goes over each patch once (`OreFragment.Brushed` keeps how far on the stone), and a going-over is counted as the workshop counts it, so what is sent (`GoingOver`) is what the stone shows |
| `CraftPourControl` | `Tick(dt, tilt, hands, fillSeconds, onTarget)` → rate, steadiness, interruptions, how full; `Supply` (the metal the crucible holds, in mould-fulls: the stream stops when it is gone, no interruption), `Spilled` (missed the mouth), `InCup` (the pouring cup takes up to `Reserve` past full: a prompt tip-back is clean), `Overflow` (run over after that: flash past `Flash`), `Lost`, `SpillShare`, `OverflowShare`; `Settled(tippingBack)` (the pour is over: tipped back, full and upright, or empty), `FillSeconds(grams)` (a big mould fills slower), `Request(mould, fromFurnace, out request)` (the Pour or TakeMelt the station gets); an interruption is counted when a stream stopped short of full starts again; `Aim` / `AimOf(tilt)` (the rate a tilt asks for) and `Reach` (how far out the stream reaches: as far as the tilt throws it, then as it slows - hosts keep the landing point once the hand lets go) - the window's crucible and the 3D one alike |
| `CraftRhythm` | `Answer(pull, hands)` on a beat → mean timing error, spread, pull |
| `CraftBellowsPump` | `Pump(hands)` → the strength of the puff (a steady rhythm builds the draft); `Rhythm` how the last pump kept it (1 steady, 0.8 slow, 0.75 hurried, 0.35 frantic) and `SinceLast`; a steady pump is light work (`CraftHands.Exert(SteadyEffort)`: the hammer arm still rests), one closer than `Frantic` (0.25 s) to the last a hurried movement (`Move`). What the fire gets is `CraftThermal.Draft(settings, fire)` - its own draft and the bellows' air, through its vent - and `DraftShare` that as a share of the most there is (the 3D bellows' meter) |

`CraftOreRock` builds one stone of a lump as a seeded vertex-coloured mesh (ore veins, rind, dirt as much as the
fragment holds) at its own size (`Radius` within `MinRadius`..`MaxRadius`; `Magnified` says how much larger a pebble is
drawn, and the 3D brush reaches that much further over it) and scrubs it along a ray (`Brush(from, dir, reach, effort,
brush)`, through a `CraftBrushCover`), saying how much of it each brush went over — the coverage
`CraftWorkshop.BrushOre` takes. `CraftOres.BrushTo(fragment, ore, brush, to)` is the brush's rule on one stone (what
`BrushOre` does): applied to the stone as it was put on the bench, brush by brush as far as the hand has gone over it,
it gives the dirt share the station will have (the 3D *Clean* meter).

`CraftHandsOn` (on the station, added on demand) is the 3D way of working the anvil, the bellows, the wheel, the
whetstone, the quench tank, the crucible, the carving bench and the ore bench with these models:
`CraftHandsOn.For(station).Begin(mode, player, hands, setup, ended, out reason)` takes the work place up - once
`CraftHandsOn.CanTakeUp(station, mode, setup, out reason)` agrees, which the window asks before it steps aside - (its
own camera on the player's side of the station's point for it, `CraftStationVisuals.HandsOnPoint(place)` - without one,
just clear of the station's colliders (its children's too) - turned to the other side when a wall stands close on the
player's and the view drawn nearer when something stands in its way (a sphere cast, never through the work place or
anybody's body); the station's own model of the place hidden meanwhile by `CraftStationVisuals.HideModel(place,
hidden)` - or, when `HasModel(place)` is false, `HideOverlapping(area, hidden)`: what of the station's own stands right
where the work place is built (`UnmodelledPlaces` lists points set without a model for the inspector) - and shown again
by `ShowAgain(hidden)`; the piece built by
`CraftModels.PieceMesh` with one glowing material per segment and, when it has an eye or cracks that show, one more
sub-mesh for them (`CraftModels.MarkedMesh`, `CraftModels.Marks`), rebuilt when `CraftModels.ShapeKey` changes),
`End(backToWindow)` puts it down. The window's areas declare theirs with `CraftMode.HandsOnMode` and fill a
`CraftHandsOnSetup` (tool, face, abrasive, side, angle, mould, the stretch of the piece, the carving cut...);
`CraftHandsOn.Setup` is kept current with what the hands choose there, and the window hands it back to the area
(`CraftMode.TakeBackHandsOn`) when the tool is put down (a carving cut the knife does not make by hand, the window's
saw, starts it shaving and comes back unchanged unless another cut is chosen there). A `Move` into the fire carries the
stretch as segments `i0` to `i1` with `flag` set (without the flag an `i1` of 0 is the whole piece). It sends only
requests - numbered and answered as the window's are, its `Say(text, refused)` putting a line over the bottom of the
screen (and into the window's *What happened*: `CraftStation.Log(text, refused)` raises `CraftStation.Logged`, which
the window keeps) - so a mode of your own can call it too; `CraftHandsOn.WorkedAt(mode, kind)` says which requests a work place
makes (the station gives the full pace only to those, `WorkingByHand(player, kind)`). Its keys are the `Station` input
map's (`StationControls`: `Pressed`, `Held`, `Released`, `Key`/`K` for the key bound, with the `StationPrevChoice` /
`StationNextChoice` steps and the *Less* / *More* vent keys); `CraftWoodwork.BoreSpan(piece, end, out from, out to,
ahead)` is the stretch the next turn of the bit bores, read from the piece, so the window and the hands go on from each
other's slot (`ahead`: turns sent and not yet answered, which the next reaches past).

## The station (`Crafting/Runtime`)

Everything a player does at a station is a `CraftRequest` sent with `station.Request(player, request, out reason)` (or
`RequestAndTell`, which tells the player why not - the same words again only after `CraftStation.RepeatInterval`), so
a network layer can carry it: `PutItem`, `TakePiece`, `ScrapPiece`, `Move`, `Turn`, `SetIntent`,
`AddFuel`, `Bellows`, `Damper`, `Oven`, `Crush`, `DiscardFragment`, `Wash`, `Sieve`, `Flux`, `TapSlag`, `ExtractBloom`,
`StirMelt`, `Skim`, `Pour`, `TakeMelt`, `Strike`, `Fold`, `Weld`, `Split`, `FormFastener`, `Agitate`, `Clay`, `Descale`,
`Grind`, `File`, `Polish`, `Coat`, `Sharpen`, `FillVat`, `DrainVat`, `VatHeat`, `StirVat`, `Scrape`, `Stretch`, `Stake`,
`Condition`, `Dye`, `Slick`, `Skive`, `Cut`, `StartAssembly`, `Attach`, `Detach`, `JointWork`, `Finish`, `Inspect`, `Assay`,
`Suspend`, `Resume`, `Wait` (test stations), `Routine` (work done by routine: below), `FillTank` (one of the station's `Quenchants`, its item used up). A `Fold` or `Weld` at a real station is always laid open (`flag`), its seam set by the `Strike`s that follow; only a test station's quick buttons weld at once. The station takes the materials from the inventory (or free supplies),
gives back items, tells the player's journal what happened, and raises `WorkshopEvent` and `Changed`.

Other members: `Workshop`, `Rules`, `Tools`, `Has`, `Instruments`, `HasInstrument`, `InstrumentName(instrument)` (the
Scale is a "balance"), `IsTestStation`, `Reveals`, `Mastery(player, craft)`, `SkillFor(player, request)` (the practice a
request is worked with), `WorkingByHand(player)` / `WorkingByHand(player, kind)` (at a 3D work place, and at the one
that works this request), `ForgetPlayer(player)` (drops the player's guard, 3D place and open pots: the oil, wax, dye
or finish a player has open here is kept one pot a player and a supply - `CraftOpenPots<TPlayer>` in Core: `Left`,
`ToOpen`, `Use`, `Forget`, `Prune` - and shared by the tannery and the finishing bench),
`Accepts(item)`, `FreeSupplies()`, `SupplyAvailable`, `IsInspected`, `AssayLines`, `SaveWorkshop`, `LoadState`,
`ClearWorkshop`, `Validate`. `ICraftStationNet` (`RunsStationRules`, `SendRequest`, `SendTending`, `Tell`, `Open`) is the
network side; `GameNet.NetCraftStation` is that side for Fish-Net ([08 — Multiplayer](08-Multiplayer.md)). On a client
the station is a copy following the server's (`IsMirror`, `SetMirror`, `SetMirrorMeta`, `ClearMirror`), and
`LocalMessage` carries the server's answers to the window.

## Practice (`CraftMastery`)

A player's practice in each craft (`CraftDiscipline`: Smithing, Leatherwork, Woodwork, Tailoring, Jewellery, Alchemy) is
kept in their journal (`CraftJournalData.mastery`, `AlchemyJournalData.mastery`: a `CraftMasteryNote` per craft with its
experience and items finished). The rules, all pure and static:

| Member | What it does |
|---|---|
| `Level(xp)`, `ExperienceFor(level)` | mastery (0-1) from experience, and back |
| `RankOf(m)`, `RankStart(rank)`, `RankProgress(m)`, `Title(craft, m)` | Novice … Master; "Journeyman tailor" |
| `Of(journal or notes, craft)` | a player's mastery of a craft |
| `Practise(notes, craft, xp, work, now, finishedOne)` | adds experience (work no faster than `workPerMinute`); returns the line announcing a new rank, or null |
| `ItemExperience(design, quality)`, `WorkExperience(request)` | what a finished item and a request teach |
| `OfDesign(design)`, `OfRequest(rules, workshop, request)`, `OfInsight(id)` | the craft a design, a request, an insight belongs to (null: nobody's) |
| `FatigueFactor(m)`, `FinishingTouch(quality, m)`, `StitchesPerAction(m)`, `RowsPerBeat(m)`, `HolesPerBeat`, `NeedlefulStitches(m)`, `FacetHits(facets, m)` | what practice gives |
| `ByPractice(ref request, m)` | multiplies a JointWork's stitches (its `i1`: 1 the needle, 2 the pricking iron and awl; reset to 0) and its pulls past the cut (`i2`), keeps the `flag` (pierce as it draws) only on the needle's stitch, and repeats a WeaveCloth's beats as rows |
| `RoutineRank`, `RoutineDiscipline`, `CanRoutine(routine, m)`, `RoutineQuality(routine, m)`, `RoutineName` | the routines (`CraftRoutine`: WindSkein, MeasureWearer, SizeFinger, PickFibre, TeaseFibre, CleanGem, SearchGem) |
| `DoRoutine(workshop, routine, m, piece, wearer, settings, out reason)` | does one: the ordinary workshop operation at the quality practice gives |
| `Benefits(craft, m)`, `Unlocked(craft, rank)` | the journal's lines |
| `Seed(journal, rules)` | an older journal's work turned into practice, once |

The station uses them where the rules run: `Request` works out the craft and the practice, applies `ByPractice`, does
the request, and on success calls `CraftKnowledge.Practised(craft, xp, work: true)`; a `Routine` request (`i0` = the
`CraftRoutine`, `piece` = what it is done to) is refused below its rank and otherwise goes to `DoRoutine`; `Finish`
records the finisher's mastery with the item (`CraftWorkshop.Finish(id, out reason, mastery, maker)` →
`AssemblyState.mastery`, which `CraftEvaluator` turns into the finishing touches, and `AssemblyState.maker`, the
finisher's name: the item's record names them and only their journal notes it as made) and awards `ItemExperience`.
`CraftKnowledge.Mastery(craft)` reads it; its `Learnt` event carries the rank-up line (the window shows it). Alchemy:
`AlchemyKnowledge.Mastery`, `Practised(xp, work, finishedOne)` and `AlchemyStation.Mastery(player)`.

In the window, `CraftMode.Discipline` names an area's craft (the hands' steadiness and the subtitle follow it);
`MasteryOf(craft)`, `Mastery`, `Practised(craft, rank)`, `RoutineButton(row, routine, label, piece)` (sends the routine,
shows the rank it needs until then) and `PractisedButton(row, craft, rank, label, action)` help an area of your own;
`GoTo(place)` opens one of its places. The guide is in Core and testable: `CraftGuide.Next(ws, tools, piece, known,
inspected)` → the next step (`Step`: `text`, `area` - the work area's key, its type name (`CraftGuide.Forge`,
`Lapidary`, `Tailor`… in `CraftGuide.AreaKeys`) -, `place` - an area's own place by its enum name - and `piece`, a
fitted part Go should pick), read from the state alone; with no piece it says what the furnace holds once its charge is
spent. A wooden part is held to `CraftWoodwork.LengthWanted(part, out min, out max)` - its shape's length when the design
gives it a profile (the shape the evaluator scores and the carving bench dashes), else the wooden part's own range -
and `CraftWoodwork.LengthOff(part, length)` is the evaluator's own note (`CraftEvaluator.LengthScore` below
`CraftEvaluator.LengthNoted`): a blank meant for the part is sent to the saw (`CraftGuide.Carve`, *Saw*; the Woodshop's
Saw at a sawhorse alone), one fitted at that length is taken off again before *Finish it*. Before that, wood is broken
down along the grain while it pays (a log split, green split stock split on into billets before it seasons, stock meant
for a part split or rived while each half still holds the part); after it, a seasoned part still wider or thicker than its
shape is carved to it (`CraftEvaluator.ProfileError(piece, shape, overOnly: true, out widthMore)` - the evaluator's own
measure of a profile, `ProfileFit` below `ProfileNoted` - counting only what a knife can still take off: *Width* or
*Thickness*, the more off), a grip bored for its tang (`TangSlot`, *Bore*), and the part sanded and sealed (optional:
the Woodshop's *Finish*, until `lastGrit` reaches `CraftWoodwork.FineGrit` and `Sealed`). The window asks through `CraftGuide.Next(station, piece, journal)` (`Crafting/UI/CraftGuideStation.cs`) for its
guide line and its **Go** button, which shows only where the area is `Available` and `HasPlace(place)`;
`CraftGuide.KeysMatch(areas)` warns once a session if an area was renamed without its key. An area of your own that the
guide should send pieces to needs a key there and `HasPlace` + `GoTo`, and `PlaceTitle(place)` when its buttons name a
place otherwise than its key (the guide line shows the button's words). `ArmWords(level)` words the arm in the area's
terms (`CraftHands.FeelingAt(level, blows)`: an area whose hands strike says *blows* while they do).

## Networking (pure parts)

| Type | What it does |
|---|---|
| `CraftWorkshop.OperationRecorded`, `ApplyRemote(op)`, `RunTo(step)`, `Checksum()`, `Capture(withHistory)` | a copy of a workshop follows one that runs elsewhere, step for step |
| `CraftWorkshopFeed` / `CraftWorkshopFollower` (`Networking/Core/Stations`) | the server numbers the operations and hands out packets and snapshots; a client restores, applies, and asks again when out of step |
| `NetPayload`, `NetPayloadAssembler` | compressed text cut into parts for the network, bounded when unpacked |
| `CraftNetRequest` | a request as plain values (the item by id) |
| `CraftRequestGuard` | the bounds on what every player's requests claim, kept by the station per player (real time, all-or-nothing bundles, the hammer's charge, the hands' practice, each kind's list; a reason for every refusal and part grant): `Check(ref request, now, out reason, ws)` (with the workshop it can tell an empty item, which may be given up anywhere); `TestStationOnly(in request, ws)` (Suspend, Resume, a scrap - but of an empty item - and nothing else); `Refund()` gives back what a request took when the station then refused it; at a test station only the pace of requests (`Paced`) for a player on another machine. The shape (`WellFormed`) is checked at every station; `FloorOf(skill)` is the hands' floor a window shows, `CutCap(floor)` the cleanest cut those hands make |
| `CraftSettingQueue` | the settings a hand turns (`CraftRequest.IsSetting`): only the latest value of each, sent at most ten times a second - by the window where the rules run and by a client's station |
| `CraftRequest.seq`, `CraftAnswer`, `CraftAnswerQueue`, `CraftAnswerWaits(numberFrom)` | a client's request numbered; the server's answer with the operations it made, raised (`CraftStation.Answered`) once the copy shows them; the window's callbacks, given up on after 10 s (the 3D work places number theirs from a range of their own, `numberFrom`) |
| `IRuntimeItemSource` (`ItemDatabase.AddSource`) | runtime items the server can `Export` and clients `Import`; `ItemDatabase.RuntimeItemMissing` / `RuntimeItemDefined` / `RuntimeItemChanged` |
| `CraftModels` | the model of a runtime piece, stock or crafted item, built from what it is (a blade's bevels on one edge or both, its bends toward the spine for a positive `bend`, the fuller's groove, a dish, a cut half made, an eye and the cracks that show); `Forget(key)` frees its model and the meshes built for it |
| `CraftStationVisuals` | the station's fire, glow, sparks, steam and sounds at its points; where each 3D work place stands (`HandsOnPoint`) and the station's own model of it (`WorkPlaceModel`, hidden by `HideModel` while the hands work there and shown by `ShowAgain`; never its particles or effects; without one set - `HasModel` - `HideOverlapping` hides what stands right there, and `UnmodelledPlaces` names the points set without a model); `GrindSparks(at, count)` throws the wheel's sparks from a point without moving the grinder's own |

## Items

| Type | What it is |
|---|---|
| `CraftStockItemSO` (asset) | a raw material: Metal (composition and size), Ore, Hide, Wood, Fasteners, Fuel, Supply, Agent; ore lumps and hides keep a value per copy (deposit and lump, animal) in the stack's durability list |
| `CraftPieceItemSO` (runtime) | one worked piece carried off a station, with its whole state (an unfinished assembly carries its parts) |
| `ForgedWeaponSO`, `ForgedArmorSO` (runtime) | a finished item: a copy of its design's template with the numbers of what was made, and a `CraftedItemRecord` (maker, quality, tier, parts, procedure, inspection, the assembly itself) |

Runtime items are written to `persistentDataPath/Crafting/Items` (offline, or on the server) and found again by id:
`ItemDatabase.Id(item)` gives `craftpiece:…` / `crafted:…` / `craftstock:…` (`IRuntimeItem`), and `ItemDatabase.Item(id)`
asks the registered sources (`CraftItems` is an `IRuntimeItemSource`; `ItemDatabase.AddResolver` registers a plain
resolver). `CraftItems` also makes runtime stock (`Stock`, `AllStock`), piece items (`PieceItem`) and finished items
(`Finished`, `Build`), and `Export` / `Import` their definitions for the network. Every runtime item gets a model from
`CraftModels` (its world prefab, and the hand model of a crafted weapon whose template has none); a piece put back on a
station (`CraftItems.Forget`) frees its model, the meshes built for it and the icon drawn for it.

## Extending

* **New metals, ores, hides, designs** — data in the Crafting Database or an extension database (chapter 03).
* **New conditions** — any `IAlchemyConditionSource` on the station (the same interface alchemy uses) sets the ambient
  temperature and named variables; drying reads `humidity`.
* **New work areas** — subclass `CraftMode` (draw from the workshop, turn the pointer into requests) and add it to the
  window's list (`CraftStationUI.CreateModes`; the areas are made anew for each visit). Keep the window's conventions:
  right-click (`CraftPointer.Cancel`) puts down or aborts and never commits, the wheel (one notch a `Scroll`) turns or
  adjusts; finish or drop per-piece work in `SelectionChanging` (the selected piece can change under an open area);
  `Busy` names hand work on the selected piece that taking it would lose, `Kept` what the area keeps when the window
  closes (it is there again when the window opens at the same station: the window says so instead of that it was
  stopped); word results inside the `answered` callback of `Send` (on a client it comes a round trip later, the copy
  already showing the result; `AnswerEvents` are the events that request raised) - and never send work that depends on
  an earlier request only from that request's answer when the window may close or the piece be taken meanwhile: the
  station runs one player's requests in the order they were sent, so send them together (the needle's pull is one
  request for that reason); `CraftStationUI.Area<T>()` reads another area's choice (the wheel the fibre bench's bath); use `SettingSlider` for a setting the hand turns,
  `Confirm` for what cannot be undone, `LiveDraw` while something on the surface moves fast, and `Meter` (with target
  bands) for gauges. Draw in the shared look: one lettering for every work surface - `CraftArt.Label(c, x, y, text,
  color, scale)`, `LabelWidth`, `LabelFit` (cut to a width with a full stop) and `LabelWrap` (lines by pixels),
  `LabelHeight` (10 px a line at scale 1), `LabelInk` for a meter's name and a reading, `LabelDim` for a place's name or
  a caption; its glyphs are the capitals, the digits and `. , : ; ' " ! ? - + = _ / \ ( ) [ ] < > % ° × # & * …` (small
  letters are drawn as capitals, accented ones as their plain letter, anything else as a space). The glyphs and the
  measuring live in Core, `CraftLettering` (`Of(ch)`, `HasGlyph(ch)`, `Width`, `Fit`, `Wrap`; `CraftArt`'s label calls
  forward to it), so a test holds every name of your content to them - and one palette,
  `CraftMode`'s: `GoodBand` (green: the safe or ideal band), `FairBand` (amber: barely, not yet), `HarmBand` (red:
  harm), `ColdBand` (blue: too cold or slow to take), `IdleBand` (grey: does nothing yet), with `Good`, `Fair`, `Harm`
  for words and ticks, `Mark` (gold: what the hands picked), `CraftArt.TargetColor` (the design's target, dashed) and
  `CraftArt.Heat` (metal's glow). Never an area's own greens and reds (an area must not declare its own
  `Good`/`Fair`/`Harm`), and name every meter on the surface beside it; end the hint with what right-click does there.
* **Your own presentation** — read `station.Workshop` and listen to `WorkshopEvent`; `CraftArt` draws pieces into a
  `CraftPixels` buffer if you want the same pictures: `DrawTop`, `DrawSide` and `DrawSection` (`edges`: a double-edged
  blade's bevels on both sides) with `Heat(celsius, dark)` - the glow with the words' thresholds; `DrawHide` with row 0
  (the north edge, a pattern's top row) at the top and `HideCellAt` / `HideCells` / `HidePoint` / `HideCellRect` to turn
  the pointer into cells and back; `DrawPattern`, `DrawChalk`, and `PatternOutline` - a placed pattern's true cutting
  line along its cells' edges (`CraftPattern.Outline`), with `NearestOnOutline` to score a knife or shears against it.
