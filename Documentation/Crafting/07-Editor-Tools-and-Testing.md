# 07 — Editor Tools and Testing

## Menus

| Menu | What it does |
|---|---|
| *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Crafting Database (Resources)* | the project's database (`Settings/CraftingDatabase`), filled with the built-in content |
| *Tools ▸ SimpleMovements ▸ Crafting ▸ Create Smithy / Smeltery / Tannery / Leatherworks / Complete Workshop* | a station with those tools in the open scene: primitive work places in a row, a collider, the station, its visuals, its surroundings (and, with Fish-Net, a NetworkObject and a `NetCraftStation`) |
| *… ▸ Create Test Station* | a station with every tool and instrument, free materials and the numbers shown |
| *… ▸ Create Ore Deposit* | a seam of hematite (change its ore in the inspector) |
| *… ▸ Create Stock Items* | item assets (`Items/Crafting/Stock`, icons in `Items/Crafting/Icons`) for the stock metals, every ore, hide, wood, fuel, supply and agent, and fasteners |
| *… ▸ Create Design Templates* | a weapon (built-in moveset of its kind) or armour asset per design in `Items/Crafting/Templates`, assigned in the database |
| *… ▸ Validate Crafting Database* | errors and warnings in the Console |
| *… ▸ Help* | this guide |
| *Assets ▸ Create ▸ SimpleMovements ▸ Crafting* | Crafting Database, Stock Material |

## Inspectors

* **Crafting Database** — counts, errors and warnings, buttons to create the templates and stock items or reset to the
  built-in content.
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

`CustomEditor/Networking/Tests/StationNetTests.cs` covers the network parts (no Fish-Net needed):

| Area | Tests |
|---|---|
| Payloads | small and large text survive packing, splitting and reassembly (and compress); garbage and oversized payloads are refused; a new transfer replaces an unfinished one |
| Following the server | a client's copy of a forge session is the server's, number for number, and raises its own events; a late joiner starts from a snapshot; a lost packet or a drifted copy asks for a snapshot; a replaced workshop is sent whole; an alchemy copy follows the server's brew exactly |
| The server's bounds | work takes real time; the hammer must be raised to strike hard; blows that never tremble are noticed; hands are never steadier than practice allows; malformed requests are refused; a network request carries every field |
