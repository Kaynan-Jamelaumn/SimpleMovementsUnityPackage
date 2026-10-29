# 07 — Editor Tools and Testing

## Menus

| Menu | What it does |
|---|---|
| *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Crafting Database (Resources)* | the project's database (`Settings/CraftingDatabase`), filled with the built-in content |
| *Tools ▸ SimpleMovements ▸ Crafting ▸ Create Smithy / Smeltery / Tannery / Leatherworks / Woodshop / Tailor's / Armourer / Spinnery / Lapidary / Jeweller's / Complete Workshop* | a station with those tools in the open scene: primitive work places in a row, a collider, the station, its visuals, its surroundings (and, with Fish-Net, a NetworkObject and a `NetCraftStation`) |
| *… ▸ Create Test Station* | a station with every tool and instrument, free materials and the numbers shown |
| *… ▸ Create Ore Deposit* | a seam of hematite (change its ore in the inspector) |
| *… ▸ Create Stock Items* | item assets (`Items/Crafting/Stock`, icons in `Items/Crafting/Icons`) for the stock metals (and the jeweller's wire and bezel strip), every ore, hide, wood (blanks and green logs), rough gem, fuel, supply and agent, and fasteners |
| *… ▸ Create Design Templates* | a weapon (built-in moveset of its kind) or armour asset per design in `Items/Crafting/Templates`, assigned in the database |
| *… ▸ Validate Crafting Database* | errors and warnings in the Console |
| *… ▸ Help* | this guide |
| *Assets ▸ Create ▸ SimpleMovements ▸ Crafting* | Crafting Database, Stock Material |

## Inspectors

* **Crafting Database** — counts, errors and warnings, buttons to create the templates and stock items, **Add Missing
  Built-in** (adds the built-in metals, ores, hides, woods, supplies, patterns and designs the database lacks, by id: an
  older database catches up with new built-in designs without losing your changes; then run Create Design Templates and
  Create Stock Items for the new ones) or reset to the built-in content.
* **Craft Station** — configuration problems, *Make Networked* (Fish-Net); in play mode the live workshop: time, the fires, the furnace, the
  crucible, every piece (class, temperature, hardness, mass; tannage and moisture; fragments), Pause/Resume, Copy State
  JSON (bug reports, tests), Clear.

## Edit Mode tests

`CustomEditor/Crafting/CraftingTests.cs` (Window ▸ General ▸ Test Runner ▸ Edit Mode), on the built-in rules:

| Area | Tests |
|---|---|
| Configuration | the built-in rules compile; content extends without code (a new element and class name a new alloy) |
| Reproducibility and saving | the same operations give the same workshop; replay from the history; capture and restore continue exactly; the state survives JSON; deposits are repeatable and vary |
| Conservation | forging conserves metal; splitting conserves mass; crushing conserves mass and washing cannot remove bound impurities; the crucible conserves metal and alloys by proportion; cutting moves cells out of the hide |
| Boundaries | refused operations change nothing; cold forging hardens bronze and cracks hardened steel; overheating burns steel; descaling does not mend cracks |
| Heat treatment | water hardens high-carbon steel, not mild steel; no hardening from below the critical heat; tempering trades hardness for toughness; annealing softens more than air cooling; quenchants differ in severity; a fantastic quenchant lends its element; an uneven quench warps |
| Smelting | cleaned ore yields more iron |
| Surface | grinding makes an edge and sharpening keens it; shaky hands give a worse edge; heavy grinding draws the temper |
| Leather | tanning reaches the core with time; scraping too hard scores; staking keeps drying leather supple; a hot vat shrinks tanned leather |
| Assembly | parts and fit are checked; an unfinished assembly moves between stations with its parts; hot parts are refused near wood; an overdriven wedge splits the haft; a belt from tanned leather and a buckle |
| Evaluation | better work makes a better item; incomplete items rate low |
| Observation and knowledge | no numbers without instruments; the journal learns metals by working and assay, and from what happens |
| Hand-skill models | the hammer is repeatable and rewards a full swing; the stroke measures what the hands did; the pour counts interruptions; the rhythm rewards timing |

`CustomEditor/Crafting/CraftProcessChainTests.cs` covers the production chains:

| Area | Tests |
|---|---|
| Woodworking | a careful debark keeps the wood, a hasty one gouges it; a split through the heart gives even halves; seasoning follows the air's humidity; sanding through the grits and a resin seal last longest |
| Fibre | hot, churned wool felts; prepared fibre spins a better yarn and plying strengthens it; a skein reels only its counted length; short staple is carded, not combed |
| Tailoring | layout refuses overlaps and a cut on the chalk is cleaner; measured garments fit; thread must suit the material |
| Lapidary | the tests read the stone's properties and a wrong name sticks; the loupe finds what it sweeps over; facets at the right angles give a brilliant stone; a compound must be as hard as the stone, a table must not lie on the cleavage; cleaving along the plane parts cleanly |
| Jewellery | a ring from rough to finished carries its stone's element; a wide seat leaves the stone loose; jewellery is made only at a jeweller's bench |
| Bounds and knowledge | the guard holds faceting and carding to real hands; the journal learns gems and the new insights; the whole chain replays exactly |

`CustomEditor/Crafting/CraftMasteryTests.cs` covers practice:

| Area | Tests |
|---|---|
| Earning it | mastery grows with experience, each step harder won; small work comes only so fast, a finished item always counts; a new rank is announced with what it allows; an older journal turns its work into practice once, and it is saved |
| What it gives | more stitches, rows and fewer facets by hand with each rank; routines at their rank and never a master's best; a master's arm tires less and is steadier; the station multiplies a needle's or an awl's run and a reed's rows itself; a master's finishing touches raise what an item lacks, and replay |
| Which craft | each design and each request belongs to its craft (cloth is tailoring, leather leatherwork, the sizer jewellery; the bellows nobody's) |
| Routines | done as well as practice does them: fibre picked and teased, a skein counted closer by a master and never long, the wearer and the finger measured, a stone brushed clean without a mark and searched to the last inclusion; the guard lets them through one at a time and only known ones; they replay |
| Fibre | raw fibre spun from the bag never spins fine |

`CustomEditor/Networking/Tests/StationNetTests.cs` covers the network parts (no Fish-Net needed):

| Area | Tests |
|---|---|
| Payloads | small and large text survive packing, splitting and reassembly (and compress); garbage and oversized payloads are refused; a new transfer replaces an unfinished one |
| Following the server | a client's copy of a forge session is the server's, number for number, and raises its own events; a late joiner starts from a snapshot; a lost packet or a drifted copy asks for a snapshot; a replaced workshop is sent whole; an alchemy copy follows the server's brew exactly |
| The server's bounds | work takes real time; the hammer must be raised to strike hard; blows that never tremble are noticed; hands are never steadier than practice allows; malformed requests are refused; a network request carries every field |
