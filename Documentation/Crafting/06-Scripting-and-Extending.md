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

## The hand-skill models (`CraftMinigames`)

The station window turns the pointer into operations through these — use them for your own input (a gamepad, VR hands):

| Model | Input → output |
|---|---|
| `CraftHands` | a seeded tremble that grows with fatigue and shrinks with skill |
| `CraftHammer` | `Raise`, `Tick`, `Release(aim, tilt, hands)` → a `CraftBlow` (position, force, tilt, miss, rushed) |
| `CraftStroke` | samples (time, x, y, angle, pressure) → span, mean angle, wobble, pressure, passes, edge leading, a hide path |
| `CraftPourControl` | `Tick(dt, tilt, hands)` → rate, steadiness, interruptions, how full |
| `CraftRhythm` | `Answer(pull, hands)` on a beat → mean timing error, spread, pull |
| `CraftBellowsPump` | `Pump(hands)` → the strength of the puff (a steady rhythm builds the draft) |

## The station (`Crafting/Runtime`)

Everything a player does at a station is a `CraftRequest` sent with `station.Request(player, request, out reason)` (or
`RequestAndTell`), so a network layer can carry it: `PutItem`, `TakePiece`, `ScrapPiece`, `Move`, `Turn`, `SetIntent`,
`AddFuel`, `Bellows`, `Damper`, `Oven`, `Crush`, `DiscardFragment`, `Wash`, `Sieve`, `Flux`, `TapSlag`, `ExtractBloom`,
`StirMelt`, `Skim`, `Pour`, `TakeMelt`, `Strike`, `Fold`, `Weld`, `Split`, `FormFastener`, `Agitate`, `Clay`, `Descale`,
`Grind`, `File`, `Polish`, `Coat`, `Sharpen`, `FillVat`, `DrainVat`, `VatHeat`, `StirVat`, `Scrape`, `Stretch`, `Stake`,
`Condition`, `Dye`, `Slick`, `Skive`, `Cut`, `StartAssembly`, `Attach`, `Detach`, `JointWork`, `Finish`, `Inspect`, `Assay`,
`Suspend`, `Resume`, `Wait` (test stations). The station takes the materials from the inventory (or free supplies),
gives back items, tells the player's journal what happened, and raises `WorkshopEvent` and `Changed`.

Other members: `Workshop`, `Rules`, `Tools`, `Has`, `Instruments`, `HasInstrument`, `IsTestStation`, `Reveals`,
`Accepts(item)`, `FreeSupplies()`, `SupplyAvailable`, `IsInspected`, `AssayLines`, `SaveWorkshop`, `LoadState`,
`ClearWorkshop`, `Validate`. `ICraftStationNet` (`RunsStationRules`, `SendRequest`, `SendTending`, `Tell`, `Open`) is the
network side; `GameNet.NetCraftStation` is that side for Fish-Net ([08 — Multiplayer](08-Multiplayer.md)). On a client
the station is a copy following the server's (`IsMirror`, `SetMirror`, `SetMirrorMeta`, `ClearMirror`), and
`LocalMessage` carries the server's answers to the window.

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
