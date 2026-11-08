# 07 — Scripting, Editor Tools, Multiplayer, Performance and Tests

[← Agriculture](README.md)

## Scripting

The rules are plain C# (`Agriculture/Core`) and run anywhere — a field component, a test, a tool.

```csharp
// A field's state and work (where the rules run: offline or the server).
SoilField field = ...;
FieldState state = field.State;                       // the soil grid and the plants
FarmResult r = field.Perform(new FarmRequest { kind = FarmActionKind.Dig, spot = field.Local(hit.point), amount = 1.5f }, player);
field.Answered += (who, result) => Debug.Log(result.message);   // also the server's answers on a client
field.Changed += () => { /* the soil or a plant looks different */ };

// The soil itself.
SoilGrid g = state.Grid;
if (g.TryIndex(field.Local(worldPoint), out int cell))
{
    g.Advance(cell, FarmClock.Now, AgricultureDatabase.Instance.Settings);   // bring it up to date first
    float ph = g.Get(cell, SoilChannel.Ph);
}

// The same rules without a scene: a worker with tools, instruments, water and items.
var worker = new FarmWorker { key = 1, name = "Tester" };
worker.tools.Add(new FarmToolProfile { id = "hoe", kind = FarmToolKind.Hoe, quality = 0.6f, speed = 1f, edge = 1f });
FarmResult till = FarmActions.Perform(state, new FarmRequest { kind = FarmActionKind.Till, spot = Vector2.zero }, worker, FarmClock.Now, field.Climate);

// A trial plot of a species (what the inspector's Trial plot runs).
AgriTrial.Report report = AgriTrial.Grow(AgricultureDatabase.Instance.Species("tomato"), AgricultureDatabase.Instance);
```

| Type | What it is |
|---|---|
| `SoilField` | The component: `layout`, `State`, `Perform`/`PerformHere`, `Rebuild`, `Local`/`World`/`Contains`, `Changed`, `Answered`, `Save`, `ExportVisual`/`ImportVisual`, `Net` |
| `SoilLayout`, `SoilSection`, `SoilProfile`, `SoilOverride` | What the designer says about a field ([chapter 02](02-Defining-Soil-Areas.md)); `Bake` makes a grid from it |
| `SoilGrid` | The cells: `Get`/`Set`/`Add`, `Advance` (lazy catch-up), `AddWater`, `Apply` (materials), `Till`, `Fertility`, `Write`/`Read` |
| `FieldState` | A grid, its plants (with 1 m spatial buckets), the weather now, `TickEnvironment` |
| `PlantSim.Advance` | One plant brought up to now |
| `FarmActions.Perform` | Every action of [chapter 01](01-Overview-and-Setup.md) |
| `PlantObservation`, `SoilInstruments` | What is seen and what instruments read |
| `PlantGenes` | Genes: `ToCode`/`FromCode`, `Cross` |
| `AgriItemCode` | Per-copy codes of produce and water |
| `AgriItems` | Items by id (`Seeds`, `Produce`, `Amendment`, `Tool`, `Instrument`, `Container`, `Resolve`, `All` for merchants and chests) |
| `AgriToolkit` | Inventory helpers: `GiveStarterKit`, `Give`, `Consume`, `Fill` |
| `AgriKnowledge` | A player's skills, notes and pollen: `For(player)`, `Level`, `Gain`, `Note`, `Export` |
| `FarmClock`, `AgriDate`, `AgriEnvironment` | Time, the calendar and moon, the climate |
| `AgricultureDatabase` | `Instance`, `Override` (tests), `Species`, `Amendment`, `BoundItem`, `Validate` |

## Editor tools

**Tools ▸ SimpleMovements ▸ Agriculture**:

| Menu | |
|---|---|
| Create Soil Field | An 8 × 6 m plot at the Scene view's pivot, on the ground, in the City scene when it is open |
| Create Soil Field Over Selected Object | Fitted over the selected object's footprint (its meshes, else renderers or colliders), turned with it |
| Create Water Source ▸ Well, Rain Barrel, River Bank, Lake Shore, Spring, Swamp Pool, Alchemical Font, Magical Pool | With a simple model (replace it with yours); next to the selected field |
| Create Growing Structure ▸ Greenhouse, Shade Shelter, Cold Frame, Magical Chamber | Over the selected field, with a stand-in frame |
| Create Agriculture Database | (also under Project Setup) |
| Create Species Assets, Create Soil Material Assets, Create Items | Editable assets of the built-in content, listed or bound in the database ([chapter 06](06-Authoring-Species-and-Materials.md)) |
| Make Selected Field Networked | A NetworkObject and a NetSoilField |
| Validate Agriculture | The database and every open field, in the Console |
| Help | This guide |

The **Soil Field inspector** and its Scene view tools are [chapter 02](02-Defining-Soil-Areas.md); the **Plant Species
inspector** checks the species and has the trial plot; the **Agriculture Database inspector** checks the database and
makes assets. The **Project Validator** (Window ▸ SimpleMovements ▸ Project Validator) runs the same checks with its
others: the database, every field (layout, unique ids, scale, prefabs, a missing NetSoilField), with fixes.

## Multiplayer

With Fish-Net in the project, a field with a **NetworkObject** and a **NetSoilField** (*Make Selected Field Networked*)
is server-authoritative:

* **The server keeps the field** — every hidden number of the soil, the plants, their genes, the weather history — and
  saves it. Clients get a **picture**: the soil as it looks (type, wetness, working, stones, mulch, packing, salt crust,
  holes, what lies on top) and every plant as it looks (stage, size, health, stress, symptoms, pests, diseases, canopy,
  label, each piece of produce), compressed — each part after it changes (at most every *Send Interval*, 0.5 s: a
  plant that grew does not resend the soil), both every *Refresh Interval* (30 s) for clocks, both to a client that
  joins, nothing while no remote client is connected. **No hidden number ever leaves the server** — a client cannot
  read a soil's chemistry or a seed's genes it has not measured.
* **Working the field** is an interaction relayed by the player's NetInteractor: the server opens the field window on
  the player's screen and sends their skills. Every action is a request; the server checks it (a known action, a spot
  on the field, short texts, a field players may work, the player alive and in the scene, the spot **and any plant the
  request names** within reach of the player's server position, no more than a few requests at once and about twelve a
  second), does it with the player's own items, tools and skills, and **always answers** — with only what the player
  sees (the message and the lines; items arrive through the inventory, experience through the skills). Answers carry
  the number of their request, so a late one is never taken for another; a window sends one request at a time and
  gives up on an answer that never comes after five seconds. A flood of requests is answered at most once a second.
* Clients run no rules: their copy is a mirror drawn from the pictures. The farm clock follows the server's. When the
  session ends the client drops the mirror; the field is its own again (and loads its own save) once the rules run on
  that machine — a field made or shown on a client is never saved over its offline field — and the farm clock returns
  to the machine's own.

## How it stays light

* **Soil cells are lazy.** A cell is worked out only when something touches it (a root, a tool, an instrument, the
  drawing near the camera). The field keeps running totals of rain, drying and warmth; a cell remembers the totals when
  it was last brought up to date and catches up in one go — closed-form decays, in a few even steps for long gaps (so a
  corner untouched for weeks costs a few steps, once, and reads within about 1 % of one updated every hour).
* **Plants are data.** No GameObject, no MonoBehaviour per plant. The Farm Director updates each plant about once a
  game hour (*Tick Hours*), round robin over every field, at most *Plants Per Frame* (64) a frame; time away is caught
  up in steps of at most *Max Step Hours* (3), spread over frames. A plant step costs a few microseconds.
* **Drawing is batched.** Plants are instances of a few shared low-poly meshes coloured from a 216-colour palette of
  instancing materials, drawn with GPU instancing in batches of up to 1023, rebuilt only when a plant looks different;
  fruit, flowers and pests only within *Detail Distance*; nothing beyond *Draw Distance* or while the field's realm is
  hidden. The soil is one mesh with a texture of one pixel per cell, a few hundred cells refreshed a frame and only
  while the camera is near.
* **Neighbours are found in 1 m buckets**; root zones are cached until a plant grows.
* **Saving** is binary and compressed, only for changed fields, every few minutes; network pictures are compressed and
  throttled.

## Edit Mode tests

`CustomEditor/Agriculture/AgricultureTests.cs` (Window ▸ General ▸ Test Runner ▸ Edit Mode):

* **Layouts**: the base soil fills the field; a section changes only its part and only the properties it ticks; soft
  edges blend; later sections win; polygons follow their points; painted cells take their section; painting the base
  soil cuts a hole in a section; removing a section keeps the others' paint; trying another cell size keeps the paint
  (baking never rewrites it); moving the whole layout by whole cells moves its paint; the layout hash follows every
  change; validation finds a broken outline.
* **The soil's life**: a cell caught up at once matches one updated every hour; water washes nitrogen out of sand
  faster than clay; limestone raises the pH over days; fallow soil keeps its fertility within reason.
* **Time and weather**: the calendar's seasons, years and moon; the city's climate is deterministic and seasonal.
* **Genes**: seed codes carry every gene; crosses stay within their kind and pass a fine parent's traits on.
* **Growing**: every built-in species gives when tended; perennials outlive a year with only water; the same trial grows
  the same way; rich soil grows better than poor.
* **What the player is told**: a crude probe gives words and a precise one numbers (the same reading for the same spot
  and hour); looking at plants tells symptoms, **never causes, remedies or numbers**.
* **Items and saving**: produce and water codes round-trip; agricultural ids read back; a soil grid saves and loads whole
  (and refuses a save of another shape).
* **The component**: a client's picture carries the plants but not the soil's chemistry nor the genes; a picture may
  carry only the plants (or only the soil); a picture of another field is refused; a client copy is dropped when the
  session ends; a field accepts work only within reach — and a plant named by id must be within reach too.
