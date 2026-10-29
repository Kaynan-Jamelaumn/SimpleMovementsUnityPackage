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
| Pieces | `AddPiece(piece, components)`, `RemovePiece`, `MovePiece(id, location, depth, from, to, vat, entryAngle)`, `TurnPiece`, `SetIntent(id, design, role)` |
| Fires | `AddFuel(fuel, count, furnace)`, `Bellows(strength, furnace)`, `SetDamper`, `SetOven`, `Agitate` |
| Ore | `CrushOre(id, fragment, force)`, `DiscardFragment`, `WashOre(id, flow, seconds, technique)`, `SieveOre(id, mesh)` |
| Furnace and crucible | `AddFlux(supply, count, where, piece)`, `TapSlag`, `ExtractBloom`, `StirMelt`, `SkimDross`, `Pour(mould, rate, steadiness, interruptions)`, `TakeMelt(mould, rate, steadiness)` |
| Forging | `Strike(id, position, tool, face, force, tilt)`, `Fold`, `Weld(id, other)`, `SplitPiece(id, position)`, `FormFastener(id, kind)` |
| Surface | `ApplyClay`, `Descale`, `Grind(id, from, to, side, wheel, angle, pressure, seconds)`, `File(…)`, `Polish`, `Coat`, `Sharpen(id, from, to, side, stone, angle, pressure, strokes, wobble, edgeLeading)` |
| Leather | `FillVat(vat, agent, doses)`, `DrainVat`, `SetVatHeat`, `StirVat`, `Scrape(id, path, grainSide, pressure, angle)`, `Stretch(id, n, e, s, w)`, `Stake`, `Condition`, `Dye`, `Slick`, `Skive`, `CutPattern(id, pattern, x, y, rotation, accuracy)` |
| Assembly | `StartAssembly(design)`, `Attach(assembly, role, index, piece)`, `Detach`, `JointWork(assembly, joint, work, count, a, b)`, `Finish` |
| Time | `Advance(seconds)`, `RunSteps(n)`, `RunUntil(seconds)`, `Suspend`, `Resume`, `SetAmbient`, `SetVariable` |
| Saving | `Capture()` → `CraftWorkshopState` (`ToJson`/`FromJson`), `Restore(rules, state)`, `Replay(rules, tools, ambient, seed, id, history, untilStep)`, `ForgetHistory(keepLast)` |

`EventRaised` (and `RecentEvents`) report what happens — `Struck` (with the sound: ring, thud, dead, crack), `Cracked`,
`Burning`, `Melted`, `Quenched`, `QuenchFlare`, `Tempered`, `Smelted`, `SlagTapped`, `Poured`, `Ground` (with the sparks),
`Sharpened`, `Scraped`, `Tanned`, `Dried`, `Torn`, `Joined`, `Finished`, `Refused`, `Note`… Their text is what can be
seen or heard, never a hidden number.

`CraftEvaluator.Item(rules, ws, assembly)` is what a finished item is (`CraftItemReport`). `CraftJournal` holds the
learning rules over a `CraftJournalData`.

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
ws.FacetGem(gem, tier: 0, facets: 8, angle: 41.4, indexError: 0.05, meet: 0, coolant: 1, out _, asymmetry: 0.1, misses: 0);
ws.PolishGem(gem, "alumina", passes: 12, pressure: 0.4, speed: 0.5, out _, regions /* 9 weights, or null */);
CraftGemGrade grade = CraftLapidary.Grade(ws.Rules, ws.Piece(gem));

// The jeweller.
ws.FormShank(wire, CraftJewelPart.Band, size: 18, roundness: 0.95, seamGap: 0.02, prongs: 0, out _);
ws.SeatSetting(bezel, gem, diameter: girdle + 0.02, level: 0.95, clean: 0.9, out _);
ws.MeasureFit(null, accuracy: 0.95, size: 18, out _);                    // the next item started takes it
string ring = ws.StartAssembly("bezel_ring", out _);
ws.JointWork(ring, "seat", CraftJointWork.Solder, 1, heat: 1.0, flux: 0.9, out _);
ws.JointWork(ring, "set", CraftJointWork.Push, 12, pressure: 0.6, evenness: 0.9, out _);
ws.FinishJewel(ring, buff: 0.9, clean: 0.9, out _);
ws.Finish(ring, out _);
```

The woodshop adds `DebarkWood(id, from, to, depth, steadiness, round)` and `SplitLog(id, offHeart, alongRays, solid,
blows)`; the tailor's table `LayoutPattern(id, pattern, x, y, rotation, clear)`. The station's tool quality is the
workshop variable `toolQuality` (`CraftWorkshop.ToolQuality`); its humidity `humidity`. New request kinds (append-only):
DebarkWood, SplitLog, CleanFibre, ScourFibre, OpenFibre, CardFibre, CombFibre, SpinFibre, PlyYarn, MeasureYarn,
ReelYarn, LayoutPattern, MeasureFit, TestGem, IdentifyGem, EvaluateGem, OrientGem, CleaveGem, SawGem, FacetGem,
PolishGem, FormShank, SeatSetting, FinishJewel, CleanGem, GrindGem, WeaveCloth (SawGem's `i1` carries the misses,
FacetGem's `i1` the botched facets and `i2` the asymmetry × 1000, CleaveGem's `i0` 1 for nick-and-snap, PolishGem's
`points` the 9 region weights, WeaveCloth's `points` the rows' beats and `i1` how many); a PutItem with no item and a fibre id lays a batch of that fibre on the
fibre bench. A project item that is a fibre by its name ("Raw Wool", "Wool", "Fleece", "Flax", "Cotton bale") is laid
down as a batch at a fibre bench or a wheel, and the wheel spins a batch lying on the station even when none is picked.

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
`BendTear` (the same strain the window's gauge shows) and `FinishProtection` read a piece. `CraftTextiles.IsCloth`,
`OnGrain`, `SpinQuality` and `Spun(rules, fibre, evenness, twist)` (the thread a hank becomes); spinning itself is a
station request (`CraftRequestKind.Spin`: fibre from the inventory, thread into it). `Attach(..., supplyCount,
supplyStrength)` records how strong a supply was.

## The hand-skill models (`CraftMinigames`)

The station window turns the pointer into operations through these — use them for your own input (a gamepad, VR hands):

| Model | Input → output |
|---|---|
| `CraftHands` | a seeded tremble that grows with fatigue and shrinks with `skill` (the window sets it to the player's practice in the area's craft); `fatigueRelief` (a practised arm tires less); `Strength` (what a tired arm has left), `Slips(salt, rushed)` (a seeded slip once it is past half tired), `Feeling` (in words), `Rest(seconds)` (slow while working, faster when still) |
| `CraftHammer` | `Raise`, `Tick`, `Release(aim, tilt, hands)` → a `CraftBlow` (position, force, tilt, miss, rushed, slipped; `Words`) |
| `CraftStroke` | samples (time, x, y, angle, pressure) → span, mean angle, wobble, pressure, passes, edge leading, a hide path |
| `CraftPourControl` | `Tick(dt, tilt, hands, fillSeconds, onTarget)` → rate, steadiness, interruptions, how full, `Spilled` (missed the mouth), `Overflow` (poured on after it was full), `SpillShare` |
| `CraftRhythm` | `Answer(pull, hands)` on a beat → mean timing error, spread, pull |
| `CraftBellowsPump` | `Pump(hands)` → the strength of the puff (a steady rhythm builds the draft) |

`CraftOreRock` builds one stone of a lump as a seeded vertex-coloured mesh (ore veins, rind, dirt as much as the fragment holds) and scrubs it along a ray (`Brush`), saying how much of it each brush went over — the coverage `CraftWorkshop.BrushOre` takes.

`CraftHandsOn` (on the station, added on demand) is the 3D way of working the anvil, the bellows, the wheel, the
whetstone, the quench tank, the crucible, the carving bench and the ore bench with these models: `CraftHandsOn.For(station).Begin(mode, player, hands,
setup, ended, out reason)` takes the work place up (its own camera at `CraftStationVisuals.PointFor(tool)`, the piece
built by `CraftModels.PieceMesh` with one glowing material per segment), `End(backToWindow)` puts it down. The window's
areas declare theirs with `CraftMode.HandsOnMode` and fill a `CraftHandsOnSetup` (tool, face, abrasive, side, angle,
mould...). It sends only requests, so a mode of your own can call it too.

## The station (`Crafting/Runtime`)

Everything a player does at a station is a `CraftRequest` sent with `station.Request(player, request, out reason)` (or
`RequestAndTell`), so a network layer can carry it: `PutItem`, `TakePiece`, `ScrapPiece`, `Move`, `Turn`, `SetIntent`,
`AddFuel`, `Bellows`, `Damper`, `Oven`, `Crush`, `DiscardFragment`, `Wash`, `Sieve`, `Flux`, `TapSlag`, `ExtractBloom`,
`StirMelt`, `Skim`, `Pour`, `TakeMelt`, `Strike`, `Fold`, `Weld`, `Split`, `FormFastener`, `Agitate`, `Clay`, `Descale`,
`Grind`, `File`, `Polish`, `Coat`, `Sharpen`, `FillVat`, `DrainVat`, `VatHeat`, `StirVat`, `Scrape`, `Stretch`, `Stake`,
`Condition`, `Dye`, `Slick`, `Skive`, `Cut`, `StartAssembly`, `Attach`, `Detach`, `JointWork`, `Finish`, `Inspect`, `Assay`,
`Suspend`, `Resume`, `Wait` (test stations), `Routine` (work done by routine: below). The station takes the materials from the inventory (or free supplies),
gives back items, tells the player's journal what happened, and raises `WorkshopEvent` and `Changed`.

Other members: `Workshop`, `Rules`, `Tools`, `Has`, `Instruments`, `HasInstrument`, `IsTestStation`, `Reveals`,
`Mastery(player, craft)`, `SkillFor(player, request)` (the practice a request is worked with),
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
| `ByPractice(ref request, m)` | multiplies a JointWork's stitches (its `i1`: 1 the needle, 2 the pricking iron and awl; reset to 0) and repeats a WeaveCloth's beats as rows |
| `RoutineRank`, `RoutineDiscipline`, `CanRoutine(routine, m)`, `RoutineQuality(routine, m)`, `RoutineName` | the routines (`CraftRoutine`: WindSkein, MeasureWearer, SizeFinger, PickFibre, TeaseFibre, CleanGem, SearchGem) |
| `DoRoutine(workshop, routine, m, piece, wearer, settings, out reason)` | does one: the ordinary workshop operation at the quality practice gives |
| `Benefits(craft, m)`, `Unlocked(craft, rank)` | the journal's lines |
| `Seed(journal, rules)` | an older journal's work turned into practice, once |

The station uses them where the rules run: `Request` works out the craft and the practice, applies `ByPractice`, does
the request, and on success calls `CraftKnowledge.Practised(craft, xp, work: true)`; a `Routine` request (`i0` = the
`CraftRoutine`, `piece` = what it is done to) is refused below its rank and otherwise goes to `DoRoutine`; `Finish`
records the finisher's mastery with the item (`CraftWorkshop.Finish(id, out reason, mastery)` →
`AssemblyState.mastery`, which `CraftEvaluator` turns into the finishing touches) and awards `ItemExperience`.
`CraftKnowledge.Mastery(craft)` reads it; its `Learnt` event carries the rank-up line (the window shows it). Alchemy:
`AlchemyKnowledge.Mastery`, `Practised(xp, work, finishedOne)` and `AlchemyStation.Mastery(player)`.

In the window, `CraftMode.Discipline` names an area's craft (the hands' steadiness and the subtitle follow it);
`MasteryOf(craft)`, `Mastery`, `Practised(craft, rank)`, `RoutineButton(row, routine, label, piece)` (sends the routine,
shows the rank it needs until then) and `PractisedButton(row, craft, rank, label, action)` help an area of your own;
`GoTo(place)` opens one of its places. `CraftGuide.Next(station, piece, journal)` → the next step for a piece (`text`, the
work area's title, the place) — the window's guide line and its **Go** button.

## Networking (pure parts)

| Type | What it does |
|---|---|
| `CraftWorkshop.OperationRecorded`, `ApplyRemote(op)`, `RunTo(step)`, `Checksum()`, `Capture(withHistory)` | a copy of a workshop follows one that runs elsewhere, step for step |
| `CraftWorkshopFeed` / `CraftWorkshopFollower` (`Networking/Core/Stations`) | the server numbers the operations and hands out packets and snapshots; a client restores, applies, and asks again when out of step |
| `NetPayload`, `NetPayloadAssembler` | compressed text cut into parts for the network, bounded when unpacked |
| `CraftNetRequest` | a request as plain values (the item by id) |
| `CraftRequestGuard` | the server's bounds on what a remote player's requests claim (real time, the hammer's charge, the hands' practice, shape) |
| `IRuntimeItemSource` (`ItemDatabase.AddSource`) | runtime items the server can `Export` and clients `Import`; `ItemDatabase.RuntimeItemMissing` / `RuntimeItemDefined` / `RuntimeItemChanged` |
| `CraftModels` | the model of a runtime piece, stock or crafted item, built from what it is |

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
`CraftModels` (its world prefab, and the hand model of a crafted weapon whose template has none).

## Extending

* **New metals, ores, hides, designs** — data in the Crafting Database or an extension database (chapter 03).
* **New conditions** — any `IAlchemyConditionSource` on the station (the same interface alchemy uses) sets the ambient
  temperature and named variables; drying reads `humidity`.
* **New work areas** — subclass `CraftMode` (draw from the workshop, turn the pointer into requests) and add it to the
  window's list.
* **Your own presentation** — read `station.Workshop` and listen to `WorkshopEvent`; `CraftArt` draws pieces into a
  `CraftPixels` buffer if you want the same pictures.
