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
| `SoilField` | The component: `layout`, `State`, `Perform`/`PerformHere`, `Rebuild`, `Local`/`World`/`Contains`, `Changed`, `Answered`, `Save` (false when nothing was written), `SavePath`, `Unreadable`, `ExportVisual`/`ImportVisual`, `Net`; sleeping: `Asleep`, `CatchingUp`, `Loaded`, `LivedTo`, `FallAsleep`, `WakeUp`, `PickRipe`; the static `AccessCheck` hook (who may work it: Housing installs it for rented gardens) |
| `FieldCatchUp` | Brings a `FieldState` to a time in lockstep (weather and plants together), resumable over frames |
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
| `FarmClock`, `AgriDate`, `AgriEnvironment` | Time, the calendar and moon, the climate; `FarmClock.SaveFolder` (where the open world keeps its farming) and `FarmClock.World` |
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
  plant that grew does not resend the soil), both every *Refresh Interval* (30 s) for clocks. **No hidden number ever
  leaves the server** — a client cannot read a soil's chemistry or a seed's genes it has not measured.
* **Only what changed.** A watcher gets the whole picture once; after that only what changed since the picture it
  holds — plants added, gone or changed, the stretches of soil that changed, the clock — at most every *Send Interval*,
  and every *Refresh Interval* even when nothing changed (a few dozen bytes: the clock and a checksum). A garden that
  catches up on hours of weather sends what grew, not the whole garden again: for a garden of 200 plants, 2.7 KB instead
  of 11.6 KB after 3 hours, 4.3 KB instead of 10.5 KB after 3 days. A client keeps the picture of the last 16 gardens it
  walked away from (packed, about 12 KB each) and the server the last picture of a garden nobody watches, so coming back
  to a garden that slept costs only what grew meanwhile. The whole picture goes again only to a client that holds none,
  holds one the server no longer keeps (it keeps the last few), or when the changes would be no smaller. Every change
  names the picture it applies to and the checksum of the result: a client that missed one, or would not end with
  exactly the server's picture, keeps drawing what it has and asks again — for the rest from the picture it holds, or
  for the whole picture. Messages are still cut into parts of at most 48 KB. Server and clients must run the same build.
* **Only to those near it.** A client **watches** a garden while its player is within the garden's *Draw Distance*
  (plus 10 m) and **forgets** it — picture, mesh, memory — beyond that plus 30 m; the server sends pictures only to the
  clients watching (it drops a watcher far beyond, whatever it says), and none while the garden sleeps (nobody is near
  a sleeping garden). A city of hundreds of gardens costs each client only the few around it.
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

## Sleeping gardens

Where the rules run (offline, the server), a garden **nobody is near sleeps**: about once a second the Farm Director
looks where the players are. With no player within the database's *Sleep Radius* (150 m) for *Sleep Delay* (20 s), the
garden is brought to the present, **saved**, no longer simulated or drawn and — with *Unload When Asleep* — **freed from
memory**. A player within *Wake Radius* (120 m: above the fields' draw distance, so a garden is awake before anyone
sees it) **wakes** it: it is loaded and **brought up to date with the weather that fell meanwhile, hour by hour** —
the weather of each step first (rain, drying, warmth, the soil's temperature, the day's light), then every plant to
the step's end, then the dead that have lain long enough are gone — in steps of *Max Step Hours* (3), the seasons
following from the date, the plants under the covers the garden was saved with (its greenhouses are looked at again
first when they are there). The city's climate is deterministic, so the garden wakes **as if someone had watched it
all along**, within an Edit Mode test's tolerances: the soil within 5 %, the plants' growth and stage, and their produce
(flowers open by how far the flowering has come, not one a step, so fruit ripens and rots on the same days). A garden
with no living plant is caught up 16 steps at a time and its plants, in quiet stretches, do not look at their soil
every step (*How it stays light*, below) — the same result either way. A garden
in a scene with a Weather System lives the slept hours, all but the last two, with the city's climate: the live
weather only knows now. A few hours are caught up at once; longer gaps over frames (*Catch Up Millis Per Frame*,
2 ms), the garden nearest a player first, during which the garden answers "The garden is waking up." Wakes and sleeps
themselves are spread too: each look does as many as fit in that budget (at least one each way), the nearest first,
and the rest a moment later. A player counts near unless their scene is apart from the garden's (`MobRealms`: a
player kept in DontDestroyOnLoad counts everywhere). A request is checked for reach before anything is loaded: a
player far away never wakes a garden.

A garden is freed from memory **only once its save is written**; a save that fails keeps it in memory and is tried
again. Saves are written whole (aside, then swapped in, the previous copy kept as `.bak`), and a save that cannot be
read is never written over: the last whole copy is used (the unreadable file set aside), and without one the garden
refuses work until it is mended. Gardens and the farm clock are kept with the world that is open
(`Worlds/<world>/Agriculture`, see `FarmClock.SaveFolder`), saved into it as it closes and read again from the next
one when the session changes.

**Abandoned gardens live on**: a sleeping garden that lags more than *Maintenance Lag Hours* (72 game hours) behind is
brought up to date **in the background**, one at a time (a garden that fell asleep in the middle of a catch-up first)
— so a garden nobody visits for weeks still grows, dries, seeds and dies on time, and waking it is instant. A hidden
realm (offline: the city while the player is away) sleeps the same way, and stays asleep when it is shown again until
someone comes near. *Sleep When Alone* off keeps every garden awake (the old behaviour). The Soil Field inspector shows
in Play mode whether a garden is awake, asleep (in memory or saved), waking or catching up, with **Sleep now** (it wakes
again within a second while a player is within *Wake Radius*) and **Wake now**.

The same lockstep catch-up is used for any jump of the clock (sleeping, a skip), and for an awake garden read from a
save that lags the clock — another world opens (hosting after playing alone, back offline), the field is made again,
a garden with *Sleep When Alone* off comes into play: plants and weather move together, never a plant through days of
weather that already fell. Until it has caught up, the garden is not simulated and actions wait ("The garden is waking
up.").

## How it stays light

* **Soil cells are lazy.** A cell is worked out only when something touches it (a root, a tool, an instrument, the
  drawing near the camera). The field keeps running totals of rain, drying and warmth; a cell remembers the totals when
  it was last brought up to date and catches up in one go — closed-form decays, in a few even steps for long gaps (so a
  corner untouched for weeks costs a few steps, once, and reads within about 1 % of one updated every hour).
* **Plants are data.** No GameObject, no MonoBehaviour per plant. The Farm Director updates each plant of an awake
  garden about once a game hour (*Tick Hours*), round robin over every field, at most *Plants Per Frame* (64) a frame;
  time away is caught up in lockstep steps of at most *Max Step Hours* (3), spread over frames. A plant step costs
  some microseconds (tens for a tree).
* **The steps stay short where it matters.** A plant's step sees the weather of its last hour, so a longer step would
  see one hour's frost, heat or sunshine for all its hours: tried on a summer bed of tomatoes, twelve-hour steps
  grew it as if nothing had harmed it (health 0.98 instead of 0.59), and six-hour steps already drifted (0.66).
  Frosts, ripening and harvest windows are why the steps are short, and they stay short. What is made lighter instead (each a switch in the
  database's settings, under *Catching up*, all on by default):
  * **Bare gardens fast-forward** (*Fast Forward Bare Gardens*). A garden with no living plant — empty, or only the
    dead waiting to be gone — is caught up 16 steps at a time. Its soil is lazy (above), so the steps were only ever
    there for the plants; the weather is still sampled every *Environment Step Hours* and the soil comes out the same,
    digit for digit. Sampling the weather is then all the work there is (a millisecond or two for a year of an empty garden),
    so it saves steps more than time. Resting plants are not bare: they still drink and feed a little, gather the
    cold they need to flower, bear frost and heat (less), keep their fruit rotting and their history, all by the
    hour's weather — they go through quiet steps instead.
  * **Quiet steps** (*Quiet Steps*, *Quiet Hours* 6). The dearest part of a step is looking around: every root cell
    brought up to date and read, the neighbours within five metres, how well the soil meets each nutrient need. In a
    quiet stretch of a catch-up a plant goes on with what it last saw, for up to *Quiet Hours*, while everything the
    weather decides (warmth, frost, light, development, ripening, the chances of pests and diseases) is still worked
    out every step. It looks again as soon as anything could matter: rain since, work on the soil, a neighbour that
    came up, died, rested, woke, caught or lost pests or a disease, helpers released, its own stage or rest changed,
    its roots outgrown, the water in the cell it stands in moved by a fiftieth, pests or diseases next door, or its
    water near the line where wilting starts or slugs come out. Compared with looking every step — a winter's rest, a
    summer crop, a frost spell over ripe fruit, perennials over a year — the stages, the produce (pieces made, ripe,
    overripe, rotten, and when), the plants alive, their rest and the cold they gathered all came out the same; health,
    size, quality and pest loads within about a thousandth, the soil's averages within about a ten-thousandth. It
    saves a tenth or so of the time where gardens rest or are perennial and next to nothing for a summer crop: most
    stretches are not quiet (it rains, the soil dries, pests come and go).
  * **Cells keep what they give** (*Cache Cell Values*). How much of each nutrient a cell's pH lets through, and its
    water figures (wilting point, field capacity, what it holds), are kept with the very pH and water retention they
    were worked out from and worked out again when that value changes — whoever changed it — so plants sharing cells
    do not repeat them. The garden grows exactly the same; it saves a few percent (more under trees, whose roots reach
    a hundred cells).
* **Only what someone is near is simulated and drawn** (sleeping gardens, above); a dedicated server never builds a
  garden's mesh.
* **Drawing is batched.** Plants are instances of a few shared low-poly meshes coloured from a 216-colour palette of
  instancing materials, drawn with GPU instancing in batches of up to 1023, rebuilt only when a plant looks different;
  fruit, flowers and pests only within *Detail Distance*; nothing beyond *Draw Distance* or while the field's realm is
  hidden. The soil is one mesh with a texture of one pixel per cell, a few hundred cells refreshed a frame and only
  while the camera is near.
* **Neighbours are found in 1 m buckets**; root zones are cached until a plant grows.
* **Saving** is binary and compressed, only for changed fields, every few minutes; network pictures are compressed,
  throttled and sent as changes.

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
* **Items and saving**: produce and water codes round-trip; agricultural ids read back; a soil grid saves and loads whole,
  with what covers it (and refuses a save of another shape).
* **Sleeping gardens**: gardens asleep for weeks and then caught up match the same gardens watched hour by hour (soil
  within 5 %, plants within their seeds' chance, several gardens compared together); their produce too — as many
  pieces made, ripe, overripe and rotten (within a fifth); a catch-up spread over frames ends exactly as one done at
  once; a catch-up across a whole year follows the seasons (a perennial counts the year).
* **Catching up lightly**: a bare garden (empty, or only the dead) caught up in long steps has the same rain, drying,
  warmth and soil, with a tenth of the steps, and its dead go alike; cells that keep what they give grow a garden
  exactly as before; quiet steps end as looking every step — through a winter's rest, a summer crop, a frost spell over
  ripe fruit and a year of perennials: the same stages, rest, years, produce and ripeness, health, size, quality and
  frost marks within 0.005, the soil within 0.1 %.
* **The component**: a client's picture carries the plants but not the soil's chemistry nor the genes; a picture may
  carry only the plants (or only the soil); a picture of another field is refused; a client copy is dropped when the
  session ends; a field accepts work only within reach — and a plant named by id must be within reach too.
