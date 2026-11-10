# 03 — Scripting, Multiplayer, Saving and Tests

[← Animals](README.md)

## Scripting

Where the rules run (offline, or the server):

```csharp
AnimalBook book = AnimalDirector.Book;                  // null on a network client, or while the office is closed
AnimalRecord cow = book.Find(id);
IReadOnlyList<AnimalRecord> herd = book.InPen(pen.PenId);
List<AnimalRecord> mine = book.OwnedBy(AnimalDirector.KeyOf(player));
AnimalPenView view = AnimalDirector.ViewFor(pen, player); // what that player sees there
AnimalDirector.SaveNow();
```

**Requests** go through the pen. Offline, and on the host, a request is done at once and answered. On a client it goes
to the server and the answer arrives through `pen.Answered` (matched by `seq`):

```csharp
var r = new AnimalRequest
{
    kind = AnimalActionKind.Feed, animal = cow.id, item = AnimalIds.Feed("hay"),
    seq = ++mySeq, token = Guid.NewGuid().ToString("N"),
};
AnimalResult result = pen.Perform(r, player);           // null on a client: wait for pen.Answered
```

* A request with the same **token** is done once: a retry is answered from memory, and the memory is kept with the save.
* `AnimalPen.AccessCheck` is a hook asked before every piece of work: return a refusal, or null. Chain onto the
  previous delegate and call it first, as Housing's hooks do.
* `AnimalDirector.AnyChanged` reports a pen's id when something in it changed.

### The Phase 2 requests

The new kinds are appended to `AnimalActionKind` (they are sent and remembered as numbers). The request's fields are
reused:

| Kind | `animal` | `text` | `item` | `count` | `expected` | Who may | What it needs |
|---|---|---|---|---|---|---|---|
| `Care` | the animal, or `""`: every animal in the pen that can take it | the care action's id (`brush`, `wash`, `pet`, `clean_hooves`...) | a supply (soap), optional | | | its owner, or a carer (*CareForOthers*) | the species takes the action; old enough; not done lately; the tool, when it needs one; at least 30 % of its water |
| `CleanPen` | | `""` to muck out; `AnimalCommands.TroughText` (`"trough"`) to scrub the trough | straw, optional | most straw to use | | *Tend* (the trough on public land: a keeper of an animal there) | a fork or a shovel (not for the trough); something to do |
| `Examine` | the animal (or one's own at the boarding stable) | | | | | owner or carer | not looked over in the last 0.25 game hours |
| `Treat` | the animal (or one's own boarded), or `""`: the whole pen | | the medicine | | | owner or carer | items for all its doses; not too soon, for a medicine with no *Overdose Share*; a whole pen: not a wash or a salve, and each animal that had it too lately left out (refused when every one did) |
| `CallVet` | the animal (or one's own boarded) | | | | the fee the player was shown | owner or carer | a veterinarian; not seen in the last 8 game hours; the same fee; the money |
| `Slaughter` | a living animal here | | | | | its owner | *Allow Slaughter* |
| `Butcher` | a dead animal here (or one's own boarded remains, at a pen where one keeps animals) | | | | | its owner | its species has an anatomy |
| `History` | the animal (its owner: wherever it is) | | | | | owner or carer | (an answer: nothing changes) |

`Inspect` now also answers for the owner's animal at the boarding stable, from any pen.

**Phase 3** adds one kind ([chapter 05](05-Production.md#collecting)):

| Kind | `animal` | `text` | `item` | `count` | `expected` | Who may | What it needs |
|---|---|---|---|---|---|---|---|
| `Collect` | the animal (or one's own boarded), or `""`: every animal of the pen one may collect from | a product's id (`egg`, `milk`, `wool`), or `""`: all | | | | owner or carer | something waiting; the tool its product needs (`milking`, `shears`), carried |

**Phase 4** adds one more ([chapter 06](06-Breeding-Genetics-and-Behaviour.md#breeding)):

| Kind | `animal` | `text` | `item` | `count` | `expected` | Who may | What it needs |
|---|---|---|---|---|---|---|---|
| `Breed` | one of the pair, here | the partner's id, here | | | | the owner of both, where they may keep animals | one kind, a female and a male, not close kin; each ready (stage, health, nutrition, welfare, not expecting, not in a cooldown); room in the pen and the owner's keeping for the largest litter; the species' cost item, if any (taken) |

**Phase 5** adds three ([chapter 07](07-Trading-Upgrades-and-Automation.md#multiplayer-what-the-server-checks)), and new
texts for three others. `Release` also takes `code`: the crate chosen, by its copy's code (`AnimalCommands.PlainCrate`,
1: any plain crate; 0: the one in hand, else any - but a crated animal is let out only when its own code is chosen).

| Kind | `animal` | `text` | `item` | `count` | `expected` | Who may | What it needs |
|---|---|---|---|---|---|---|---|
| `Crate` | one's own, here | | | | | its owner | fit to travel (not expecting, not nursing, health above the rules' Crate Min Health); a livestock item of its breed in the Item Database, and room for it in the bag |
| `Upgrade` | | `level`, `hopper`, `waterer`, `trays`, or `remove:<machine>` | | | the price shown | one who keeps animals here; taking out: who fitted it, or one who may care for others' animals here | a level above, or a machine it lacks and has something to work on (trays taken out only empty); not in disrepair; the money (taken first) |
| `PayUpkeep` | | | | game days to pay ahead (0: what it owes) | the price shown | one who tends it | something to pay; the money |
| `StockFeeder` | | `hopper`: into the feed hopper | | | | | a hopper fitted, room in it |
| `Collect` | | `trays`: only the egg trays | | | | owner of the hen, or carer | something in the trays theirs to take |

The plan's `give` lists the items with their per-copy codes (`AnimalItemChange.codes`): the director gives them into the
player's inventory (what does not fit is dropped at their feet). The tools used are worn by `wearKey` / `wearUses` and,
for a second tool (a pail and shears in one go), `moreWear`.

```csharp
string NewToken() => Guid.NewGuid().ToString("N");

// Brush every animal in the pen that can take it, with the player's best brush.
pen.Perform(new AnimalRequest { kind = AnimalActionKind.Care, animal = "", text = "brush", seq = ++mySeq, token = NewToken() }, player);

// Muck out, laying straw; then scrub out a fouled trough.
pen.Perform(new AnimalRequest { kind = AnimalActionKind.CleanPen, item = "agri:amend/straw_mulch", seq = ++mySeq, token = NewToken() }, player);
pen.Perform(new AnimalRequest { kind = AnimalActionKind.CleanPen, text = AnimalCommands.TroughText, seq = ++mySeq, token = NewToken() }, player);

// Collect every egg of the pen; milk one cow (with the player's milking pail).
pen.Perform(new AnimalRequest { kind = AnimalActionKind.Collect, animal = "", text = "egg", seq = ++mySeq, token = NewToken() }, player);
pen.Perform(new AnimalRequest { kind = AnimalActionKind.Collect, animal = cow.id, text = "milk", seq = ++mySeq, token = NewToken() }, player);

// Dose the whole pen with a vermifuge; send for the vet (when the view says the vet can come) for the fee it showed
// (0: a free visit).
pen.Perform(new AnimalRequest { kind = AnimalActionKind.Treat, animal = "", item = "potion:vet_vermifuge", seq = ++mySeq, token = NewToken() }, player);
if (cowView.vetCanCome)
    pen.Perform(new AnimalRequest { kind = AnimalActionKind.CallVet, animal = cow.id, expected = cowView.vetFee, seq = ++mySeq, token = NewToken() }, player);
```

### The commands

**The commands** (`Animals/Core/AnimalCommands.cs`) work in two steps, so a command is all or nothing:

1. `AnimalCommands.Plan(book, request, actor, now, ctx)` checks everything against the present and changes nothing:
   rights, the animal, the food and whether it will take it, the count carried, room in the feeder or pen, the
   species' habitat, the player's limit; and for Phase 2 the care action, the tool, the water, the medicine's doses and
   whether a dose is too soon, the vet's fee.
2. The caller takes what the plan says:
   * the money first (`plan.fee`);
   * then the items (`plan.takeItem` × `plan.takeCount`), reading each copy's quality;
   * then the water (`plan.takeWater` litres).
3. `AnimalCommands.Commit(book, plan, taken, now, ctx)` records what was really taken. Fewer is fine. Each kind says
   what it cannot do without:
   * a wash on one animal needs 30 % of its water;
   * the vet needs the whole fee (`taken.paid`);
   * soap and straw are optional (unless laying straw was the whole job);
   * a whole pen's dose leaves out, again, each animal that had it too lately, and fails (*Too soon for another dose.*)
     when that is every one;
   * the other kinds need some of what they take.

**The actor** (`AnimalActor`, filled by `AnimalToolkit.ActorFor`) gains:

* `tool`: the best tool the player carries with any of the tags asked (`AnimalToolUse`: its key, id, quality and what is
  left of it);
* `vetFee`: the veterinarian's fee for an animal, null when there is no vet.

Its `name` is the player's name as other players read it in histories and messages: `AnimalToolkit.ShownName(name)` is
`AnimalCommands.CleanName(name, AnimalToolkit.MaxNameLength)` (32), or `""` when nothing visible is left.
`CleanName(text, max)` cleans an animal's name too (at the rules' *Max Name Length*, 24):

* it drops `<`, `>`, backslashes, control characters, the line and paragraph separators (U+2028, U+2029: a text box
  starts a new line at them), invisible format characters (Unicode's *Format* category: zero-width spaces, direction
  marks, embeddings, overrides and isolates, word joiners, a byte-order mark, a soft hyphen...) and any lone half of a
  character made of two (a surrogate), wherever it is, before it counts, so none of them takes room. It keeps the format
  characters emoji and scripts are made of: the zero-width joiner (U+200D) and non-joiner (U+200C), and the tag
  characters (U+E0020-U+E007F) of a flag such as England's, but only right after its black flag (U+1F3F4) or another of
  its tags. Nor does it keep at the start a space, any format character, a mark (a lone accent or variation selector)
  or a letter that shows as nothing (the Hangul fillers U+3164, U+115F, U+1160, U+FFA0 and blank Braille U+2800);
* a name longer than `max` (UTF-16 characters, so an emoji counts as two) is cut before the character the cap falls in,
  never inside one: not between the two halves of an emoji, a letter and its accents or marks, the parts of an emoji
  joined by zero-width joiners, a variation selector or a skin tone and what it marks, a flag written with tags
  (England's) and its tags, the two letters of a flag, the letters of one Hangul syllable (conjoining letters, or a
  syllable and the letter added to it), a Thai or Lao letter and the SARA AM after it (U+0E33, U+0EB3), a halfwidth
  katakana and its voiced or semi-voiced sound mark (U+FF9E, U+FF9F), or a sign
  written before its letter and that letter (Unicode's *Prepend*: Malayalam's dot reph and some signs of other Brahmic
  scripts; the rest, such as the Arabic number signs, are format characters, dropped);
* then the spaces, blank letters and joiners (zero-width joiner or non-joiner) at its end are dropped, but a joiner
  after a virama (a nonspacing mark), which is part of the letter (a Malayalam chillu, a Devanagari half form), and it
  is null when nothing visible is left: only spaces, marks, format characters or blank letters. Naming an animal so, or
  letting one out of its crate with such a name, is refused (*That is not a name.*); `ShownName` gives `""` and the
  player is then not named.

**The plan** (`AnimalPlan`) gains:

* `fee`;
* `wearKey` and `wearUses`: the tool to wear after a successful commit (a pen's care sets them to the animals really
  done: read them after the commit);
* `toolUsed`;
* `give`: items to give after the commit (mucking out's manure: filled by the commit);
* `remains`: a Butcher's remains report;
* `plannedLitres`, `plannedCount`.

`AnimalDirector.Perform` does it all:

1. guards, the done-request memory, the actor, the plan;
2. the vet's fee (`AnimalToolkit.TakeFee`, in the vet's currency): nothing else is taken when it cannot be paid;
3. the items, each copy kept so it can be given back as it was, and the water;
4. a snapshot of the animal and the pen (of every animal in the pen for a request on the whole pen: CleanPen, or Care
   and Treat with no animal);
5. the commit. When it fails or throws, the records are restored and the items, the water and the money given back;
6. then the world's part:
   * a Butcher lays the carcass out (`AnimalCarcass.LayOut`). Only once it lies there is the record removed; if it
     cannot be laid out, everything is given back and nothing is remembered;
   * the tool is worn;
   * what came of it (the manure) is given, by its Item Database id;
   * a payment saves the purse at once;
7. the request is remembered, the owners are told, the pen's watchers are sent its new picture.

**The rules of Phase 2** are plain C# in `Animals/Core`. The commands and the simulation only call them:

| Class | What it holds |
|---|---|
| `AnimalCare` | Care actions (`CanDo`, `Apply`, `ToolFactor`, `WaterFor`, the bond's daily cap), and each step's hygiene, hooves, comfort and the stress its surroundings give |
| `AnimalHabitat` | A step's herd facts (dirt, bedding, water, worms, shelter, crowding, company, contagion), the pen after a step, pouring and scrubbing water, mucking out |
| `AnimalHealth` | The causes, each illness's build-up, onset, course, recovery and relapse; its stages' effects; a look (`Examine`), a dose (`Treat`: a dose too soon does no more good at once, only its harm), the wait before a medicine does its good again (`WaitBefore`), doses together (`Dose`: each game hour gets the strongest dose working then, each to its own end), the vet (`VetVisit`); the health history's words |
| `AnimalWelfare` | Welfare and its bands, the owner's warning, the life record |
| `AnimalRemains` | A dead animal's remains report (rearing, lean, poison, taint, contamination, coat); `Taints(illness)`: whether an illness can taint its carcass (an Infection or Parasites illness with *Taints Carcass*; the report counts it only in a stage that shows) |

**The simulation** (`AnimalSim`) is plain C#. To run it in a tool or a test:

```csharp
var cat = AnimalCatalog.BuiltIn();                     // with the built-in illnesses, medicines and tools
var ctx = new AnimalSimContext { catalog = cat, climate = new CityClimateSource(AgriSettings.Defaults()), vetAvailable = true };
var book = new AnimalBook(cat, "test");
AnimalHerd h = book.EnsurePen(new AnimalPenSpec { id = "p", kind = AnimalHabitatKind.Pasture, area = 48f }, "Scene", "", now);
AnimalSim.AdvanceHerd(h, now + 24 * 7, ctx);            // a week, hour by hour, in lockstep
```

* `ctx.vetAvailable` says whether there is a veterinarian in the world (the director sets it every frame). Without one,
  no illness is gated: a keeper's medicine can cure everything.
* A catalog made with the three-argument `AnimalCatalog` constructor has no illnesses, medicines or tools. Pass them to
  the six-argument one, or add them with `AddCondition`, `AddTreatment` and `AddTool`.
* Each step, the herd's facts are taken from the start of the step, so the order its animals are stepped in does not
  matter. Contagion is summed in a fixed order: the result is the same on every machine.
* `AnimalHealth.Begin(animal, condition, cause, now, rules)` makes an illness take hold (the editor's *Infect…* uses
  it).
* An illness's treatment (`AnimalCondition`) is kept as steps, each a dose's power a game hour to its own end, each
  working on after the one before it ends: the strongest dose working, `treatPower` until `treatedUntil`; the next,
  `thenPower` until `thenUntil` (−1: none); any more in `laterSteps`, a list of `AnimalDoseStep` (`power`, `until`).
  `PowerAt(t)` reads them in that order and gives the power working at `t` (what `AnimalHealth.Step` takes off the
  illness's course by the hour, read at the step's start), and `Treated(t)` is `PowerAt(t) > 0`.
  `AnimalHealth.Dose(condition, now, power, hours)` adds a dose, as `Treat` and `VetVisit` do: the steps still working
  and the new dose, each to its own end, are put strongest first (of two alike, the longer), and each counts only from
  where the stronger ones end; one with no power, or ended, is dropped. So the record holds each game hour's strongest
  dose exactly, however many doses there are, up to `AnimalHealth.MaxDoseSteps` (8) steps; past that, the weakest,
  which work last, are let go. A dose with no power per hour adds no step (nothing is then working on the illness).
* `AnimalHealth.WaitBefore(animal, medicine, now)` is how many game hours before the medicine does its good again: its
  *Min Interval Hours* after the animal's last dose of it that did its good (`AnimalRecord.LastDose`); 0 when it may
  have it now, when that dose is recorded after `now` (a world's clock set back), and in the last microhour (3.6 ms of
  game time) of the wait, so a keeper who comes back exactly when told is never refused for the clock's rounding.
  Within it, `AnimalHealth.Treat`
  does only what harms (and the overdose, for a medicine with an *Overdose Share*), still doses each illness it acts on,
  and does not record the dose: only a dose that did its good is recorded, so a dose too soon puts nothing off. Called
  from a script, `Treat` gives a dose too soon of any medicine (*Too soon for another dose: it does no more good yet.*
  without an *Overdose Share*); the commands refuse one of a medicine with no *Overdose Share* before anything is taken,
  and leave out of a whole pen's dose every animal that had it too lately.
* `AnimalObservation.Wait(hours)` tells a wait in words, rounded up so that a keeper told *in 2 hours* may come back
  then: *less than an hour* under 1, *1 hour* at 1, then whole hours rounded up (1.01 is *2 hours*, 5.99 *6 hours*) up
  to *47 hours*, then whole days rounded up (49 hours is *3 days*). The commands' too-soon refusals tell their wait with
  it. `AnimalObservation.Age(hours)` rounds down, for what is past (*The veterinarian saw the cow 1 hour ago.* at 1.9
  hours).
* `AnimalRecord.Dosed(dose, keep)` records a dose in place of the last dose of the same medicine (the ids are matched
  without case), so doses of other medicines never make it forget one; past `keep` medicines (16 when `keep` is less)
  those dosed longest ago are let go. `AnimalHealth.Treat` passes the rules' *Keep Doses* (64) or the number of the
  catalog's medicines, whichever is more, so an animal keeps the last dose of every medicine the game has, however low
  *Keep Doses* is set (a script calling `Treat` with no catalog gets *Keep Doses*, 16 at least).
  `AnimalRecord.LastDose(id)` reads it.
* `AnimalSim.NameOf(animal, species)` names an animal at the start of a sentence (*Daisy*, or *A cow* when it has no
  name), `AnimalSim.NameIn(animal, species)` in the middle of one (*the cow*).

**Items**:
* `AnimalItems.Feed("hay")`, `AnimalItems.Livestock("cow", "jersey")`, `AnimalItems.Tool("soft_brush")` and
  `AnimalItems.All(feed, livestock, tools)` (for merchants and chests).
* `AnimalToolkit.KeyOf(item)` tells what an item is to animals:
  * an animal, farm or harvest key;
  * a brewed potion's `potion:<outcome>`;
  * an alchemy ingredient's `ingredient:<id>`;
  * crafting stock's `craftstock:<kind>:<id>`;
  * else its Item Database id.

  `AnimalToolkit.FoodOf(item)` gives its food, and `AnimalDatabase.Instance.TreatmentOfItem(key)` and `ToolOfItem(key)`
  its medicine and tool.
* `AnimalToolkit.QualityOfCopy(stack, fromTop)` gives one copy's quality (a potion's from its code, through
  `AnimalAlchemy.QualityOfCopy`).
* `AnimalToolkit.ToolOf(inv, catalog, tags)` and `AnimalToolkit.Wear(inv, key, uses)` (where the rules run).
* `AnimalToolkit.GiveStarterKit(player)` and `AnimalToolkit.GiveCareKit(player)`.
* `AnimalAlchemy`:
  * `EnsureInstalled()` adds the three veterinary outcomes to Alchemy. It runs before the first scene on every machine.
  * `KeyOf`, `QualityOfCopy` and `OutcomeExists`.
  * `PotionItem(outcome, tier)` and `MakePotions(player, outcome, count, quality)`: coded bottles, where the rules run.
* `AnimalCarcass.LayOut(report, pen, player, out why)`: where the rules run, lays a remains report out as a Harvesting
  carcass by the pen.
* `AnimalVeterinarian.Current` is the city's veterinarian (null: none), and `FeeFor(animal, species)` its fee.
* `AnimalMerchantStock.Goods(...)` lists the keeper's goods.
* `AnimalAudio.Play(what, position)` plays a sound of the database's *Sounds*.

## Multiplayer

Put **NetAnimals** on the "Network World" object: **Network ▸ Add Missing Relays To Network World (open scenes)**, then
save the Bootstrap scene. One relay serves every pen; pens need no NetworkObject. For butchering, also make the animal
carcass prefab: **Network ▸ Create Animal Carcass Prefab** (*Resources/SimpleMovements/Networking/AnimalCarcass*: a
NetworkObject, a Harvestable Creature, a NetCarcass and an Animal Carcass Look; *Create Bootstrap Scene* makes it too).

* **The server keeps everything.** Clients receive a picture of each pen near their player (`AnimalPenView`):
  * the animals as they look (coarse levels 0–4, words and signs; no genes, toxins, nutrient sums, severities,
    build-ups or owner keys);
  * for the animals the player owns or may care for: the illnesses known by name, the care cards (whether each can be
    done now), the vet's fee and whether the vet can be sent for (`vetCanCome`; a free vet's fee is 0);
  * the feeder, trough, grass, floor, bedding, water, shelter and weather as they look;
  * what that player may do there (and whether there is something to muck out or a trough to scrub);
  * the farm clock, which the client sets its own clock by.

  A picture is sent when the pen changes (at most every 0.5 s), and every 30 s besides.
* **Watching**: a client watches the pens within *Show Radius* of its player and stops beyond *Hide Radius*. The server
  checks the player's real position (and scene) and drops watchers that went away.
* **Requests**: the player comes from the connection, never from the request. The server applies, in order:
  * a token bucket (8 at once, then one every 0.1 s; a flood is refused at most once a second);
  * bounded texts and ids (a care id travels in the 120-character text; the expected fee is clamped to
    0–1 000 000 000);
  * a known action;
  * for `CallVet`, at most one every 2 real seconds per client (*Wait a moment before sending for the veterinarian
    again.*);
  * the pen within reach of where the player really stands.

  It then does the request with the player's own items, money, tools and water, and answers with its number. Only the
  answer's message and lines leave the server (a history is lines too).
* **What the server checks in Phase 2** (in the plan, against its own state):
  * the care action is the species' own, and the animal can take it now;
  * the tool's quality and wear are read from the player's server-side bag, and worn there;
  * each medicine copy's quality is read from its code on the server;
  * whether a dose is too soon comes from the server's record of the animal: one of a medicine with no *Overdose Share*
    is refused before anything is taken, and a whole pen's dose leaves out the animals that had it too lately and takes
    items only for the rest;
  * the vet's fee is the server's, and must be the one the player saw;
  * on public land, only a keeper of an animal in the pen may scrub out its trough, or pour in water dirtier than the
    trough's once that would leave it murky (however little at a time);
  * slaughter and butchering are the owner's (the owner's key never leaves the server);
  * the carcass is laid out and spawned by the server. Who may work it is decided on the server
    (`HarvestableCreature.AccessCheck`): a client may still show the prompt, and is refused;
  * a player's name goes into others' histories and messages only as `AnimalToolkit.ShownName` leaves it.
* **Interacting**: when a client interacts with a pen, the server opens its window for that client with the pen's
  picture (or the client opens it at once when it already holds one). The vet's window and a merchant's goods are the
  NPC's: the goods are added the same way on every machine, so a client maps the server's shop lines onto them.

## Saving

Where the rules run, a world's animals are kept in `Worlds/<world>/Animals/animals.json`. It is gzip JSON, the world
`WorldStore` has open (offline, online, or `-world <id>`).

* **Written whole**: aside, flushed, swapped in, with the previous copy kept as `.bak`.
* **When**:
  * every *Autosave Seconds* (30) while something changed;
  * **at once** after a trade or anything that cannot be done again (a crate let out, an animal moved or boarded,
    remains cleared, a slaughter, a carcass laid out, a vet's visit paid for), at most every 2 s;
  * when the world closes (which is how quitting saves, before the world's lock is let go) and on pause.

  It is written down on the main thread, then packed and written by a worker.
* **Read from** `animals.json`, or `animals.json.tmp` when it is newer (a write that was not swapped in), else
  `animals.json.bak`.
  * A main file that cannot be read is set aside as `animals.json.unreadable-<time>`, never deleted.
  * If it cannot be set aside, or no copy can be read, the animals office stays **closed** until the file is mended:
    requests are refused and nothing is written over the save.
* A save of a newer version of the game is never half read, rolled back from or set aside: the office stays closed.
* The farm clock is saved by Agriculture. Animals use it, so their age, growth and hunger follow the world's own game
  time, and nothing changes while the server is down.

**Version 2** (Phase 2). Each animal also keeps:

* its hooves, welfare and digestion now;
* what bothers it most;
* its illnesses (severity, stage, cause, named or not, the treatment working: each step, the strongest dose to its end,
  then each weaker one to its own);
* the build-up of the next ones, and immunity;
* its care marks (when each action was last done, and how often lately);
* its care history, health history and its last dose of each medicine;
* when it was last looked over, seen by the vet, cared for by its owner and its owner warned;
* the illness that killed it;
* its life record.

Each day record keeps its coat, comfort, welfare (and its lowest), illness, care counts and the bond gained. Each pen
keeps its floor dirt, bedding, water dirt, worms, when and by whom it was last mucked out, bedded and disturbed, and the
manure owed.

* **A Phase 1 (version 1) save loads as it is.** What it lacks takes its default:
  * hooves 100 and welfare 60, and empty histories;
  * a life record that starts from the animal's state at its first step;
  * the day it was saved in takes its coat, comfort and welfare means from the animal as it loads (so the rest of that
    day is not averaged with zeros); its earlier days, complete, say nothing of its coat;
  * a bedded pen's bedding laid fresh (60), another's 0.

  It is written back as the current version (3). A version 2 record is read back exactly as it was written.
* **A version 2 save from before an illness kept every treatment step** has no `thenPower` or `thenUntil` (they read 0
  and −1: no second step) or no `laterSteps` (no further steps), so the treatment it saved works on as it was. Its doses
  (then the last 8 given, a medicine perhaps more than once) are read as they are: the last of each medicine counts, and
  the next of it that does its good takes the place of them all. The save version stays 2.
* **Species assets made before Phase 2** are filled in from the built-in species until they are updated
  ([chapter 02](02-Species-Foods-and-Nutrition.md#the-animal-database)).

**Version 3** (Phase 3). Each animal also keeps its production: for each product, the cycle under way (its progress,
start, count, status and the sums of how it lived through it) and the lots waiting to be collected, and its production
history. The world's save keeps the **product ledger**: its last serial and an entry for each lot collected (its animal,
owner, collector, cycle, times, quality, tier and means), pruned by age and size
([chapter 05](05-Production.md#traceability-the-product-ledger)).

* **A version 1 or 2 save loads as it is**, with nothing made yet and an empty ledger; its animals begin their first
  cycles. It is written back as the current version.
* **Species assets made before Phase 3** (Data Version 2) take their products, and their senior stage's *Production
  Enabled*, from the built-in species of their id until they are updated (**Update Species Assets To Phase 5**).

**Version 5** (Phase 5). The save also keeps the animals in crates (each with its serial, when and by whom it was crated,
and its grade) and the last crate serial, and each pen its level, its machines, its hopper's feed, what waits in its
egg trays and the upkeep it owes (below 0: paid ahead) ([chapter 07](07-Trading-Upgrades-and-Automation.md#saving)). A
save of version 1-4 loads with every pen as built, no machines, nothing owed and nothing crated. Species assets made
before Phase 5 take their egg-tray products and breed yields from the built-in species of their id until updated.

**Version 4** (Phase 4). Each animal also keeps its family (parents and their names, grandparents, generation), its
pregnancy (the sire and his genes at the mating, the kinship, when conceived and due, the litter, its seed and the young's ids), when it
may breed again, its tries and young, its bonds with other animals, when its owner was last near it, and the affinity
time together gave it today ([chapter 06](06-Breeding-Genetics-and-Behaviour.md#saving)). A save of version 1-3
loads with no family, no pregnancy and no bonds. Species assets made before Phase 4 take their breeding, behaviour and
perks from the built-in species of their id until updated.
* **Carcasses are not saved**, as in Harvesting: the animal's record is removed when its carcass is laid out (so
  nothing is butchered twice), and a restart loses a carcass not yet harvested.

## Editor tools

| Where | What |
|---|---|
| Property inspector | **+ Pasture**, **+ Coop**, **+ Sty**: a pen on a free spot of the land, like **+ Garden**; pens drawn in the Scene view; the checks of pens against the land, the house, the zones, the gardens and each other |
| Animal Pen inspector | **Edit Outline** (Scene view), *Snap*, **New Id**, the checks (a foreign property's land is an error; shelter and shade out of range warn). In Play mode: the feeder, the water and its fouling, the grass, the floor (dirt, bedding, worms, when last mucked out), the veterinarian, and every animal's numbers: its needs; coat, comfort and stress with what causes them; welfare and band, hooves and bond; each illness (severity, stage, cause, hidden or named, treatment, seen by the vet, needs the vet, recovering) and the build-up of the next ones against their onset. Buttons: **+6 h**, **+1 day**, **+1 week**, **Save now**, **Starter kit**, **Care kit**, and the debug tools **Dirty pen**, **Heal all** (`AnimalPenEditor.HealNow(animal, catalog, rules, now)` for each animal here, testable in Edit Mode: a living animal loses its illnesses and their build-up, its toxins and upset, and is at full health; each illness its keepers knew of, shown or named, is logged as over, as when one clears by itself, and no other is), and per animal **Infect…** (an illness at a chosen stage) and **Kill (illness)** |
| Tools ▸ SimpleMovements ▸ Animals | Create Pasture / Coop / Sty On Selected Property, Create Standalone Pen, Create Veterinarian In City, Add Animal Goods To Selected Merchant, Create Animal Database, Create Species Assets, Create Food Assets, Update Species Assets To Phase 2, Create Animal Carcass Prefab (Network), Give Me A Keeper's Starter Kit (Play Mode), Give Me A Care Kit (Play Mode), Validate Animals, Help |
| Tools ▸ SimpleMovements ▸ Network | Create Animal Carcass Prefab; Add Missing Relays To Network World |
| Assets ▸ Create ▸ SimpleMovements ▸ Animals | Animal Database, Animal Species, Animal Food, Animal Feed (Item), Livestock (Item), Care Tool (Item) |
| Add Component ▸ SimpleMovements ▸ Animals | Animal Pen, Veterinarian, Animal Goods (Merchant), Animal Carcass Look |
| Project Validator | The database (rules, species, foods, diets naming unknown foods, item bindings; illnesses, medicines, tools, species' care and susceptibility, anatomies, welfare; species assets made before Phase 2, with an **Update** fix), every pen (outline, kind, stations, scale, prefabs, duplicated ids with a **New Id** fix, lying on another property's land, shelter values), the veterinarian and the merchant of care tools where animals are kept (with fixes), Alchemy stations there whose own *Database* lacks the veterinary medicines, and with Fish-Net the carcass prefab (with a fix). The veterinary outcomes are added to Alchemy first, so the medicines that name them check out |

## Tests

Run them from **Window ▸ General ▸ Test Runner ▸ Edit Mode**. They are plain C# (no scene).

**`CustomEditor/Animals/AnimalCoreTests.cs`** (28, Phase 1). It covers:

* **Diets**: by id and tag, items as foods (produce, harvested meat by prefix).
* **Nutrition**:
  * a full animal can be badly nourished;
  * one good meal does not undo a week.
* **Growth**: through the stages, slowed by poor feeding, while age stays the same.
* **Hardship**:
  * hunger and thirst kill;
  * the owner-away floor holds;
  * toxic food poisons (with signs) and harmful food upsets;
  * harmful and toxic food is eaten only as far as hunger drives it, from a feeder and from a hand;
  * a full animal refuses, and a cow refuses meat.
* **Pens and room**:
  * a pasture's grass feeds a cow;
  * a stocked pasture with its usual trough keeps a cow a week;
  * room by adult space;
  * habitat kinds;
  * the player's limit.
* **Time and boarding**:
  * a week nobody watched ends exactly as a week watched hour by hour, and a catch-up spread over frames ends as one
    done at once (both now also compare every Phase 2 field: coat, comfort, stress, welfare, hooves, bond, the life
    record, the illnesses and their build-up, the pen's dirt, bedding, water dirt and worms);
  * boarded animals live on;
  * the remains of an animal that died boarded can be cleared, by its owner only.
* **Commands**:
  * a command that got nothing does nothing, and only what was taken counts;
  * the feeder's capacity;
  * rights: visitors look, friends tend, only owners name and move;
  * a request done once is never done twice, across a save, and is forgotten later.
* **Saving and identity**:
  * the world saves and loads whole;
  * ids are never reused;
  * a newer save is refused;
  * genes are deterministic;
  * item codes round trip;
  * a crate without a code becomes an animal of its own.
* **Content**:
  * a new species needs no code;
  * the built-in content is sound;
  * what is shown is coarse and hides the numbers.

**`CustomEditor/Animals/AnimalCareTests.cs`** (86, Phase 2 and the fixes of its four reviews). It covers:

* **Care**:
  * brushing cleans and soothes, needs a brush, waits its turn and wears the brush; petting needs no tool; a worn brush
    does a poor job;
  * too much brushing gives less and does harm;
  * washing a rabbit, or washing in the cold, chills it;
  * a wash needs its water and soap is optional;
  * a bond is not bought by petting all day, nor by hand feeding all day;
  * care for a whole pen is done to every animal that can take it (water shared, a drench for all);
  * an animal with no name is *the cow* in the middle of a sentence (a look, the vet, care, a rename, its remains);
  * a name cut at *Max Name Length* never ends in half of a character made of two (an emoji the cut would split is
    left out, one that fits is kept), in a rename too;
  * a name is cut only where a whole character ends, wherever the cap falls in one: an emoji, a family joined by
    zero-width joiners, two flags, a thumbs-up with a skin tone, a heart with a variation selector, an accented letter
    and a flag written with tags are each kept whole or left out whole; lone halves anywhere and spaces before the
    name are dropped; a player's shown name is cut the same way at its own 32;
  * a name keeps nothing invisible: zero-width spaces and direction marks before it are dropped and take no room, so are
    a right-to-left override inside it and a joiner at its end, and a byte-order mark, a joiner or word joiners alone
    leave no name; the joiners and tags emoji and scripts are made of stay (a family, England's flag, a Persian word
    with its non-joiner); at the cap, a Hangul syllable written in its three letters, a syllable and the letter added to
    it, Thai or Lao SARA AM, a halfwidth katakana and its sound mark and a Malayalam dot reph are each kept whole with
    what they belong to or left out whole; line and paragraph separators are dropped; Hangul fillers and blank Braille
    are no name and neither start nor end one; no lone accent or variation mark starts a name; tag characters stay only
    in a tag flag; a joiner after a virama (a Malayalam chillu, a Devanagari half form) stays at the end; a rename to
    nothing visible is refused (*That is not a name.*).
* **Living conditions**:
  * a coat settles where its pen allows, and care lifts it above that;
  * shelter keeps animals comfortable in cold rain, and a keeper can see what bothers them;
  * an animal kept alone misses its kind;
  * a crowded pen is less comfortable, more stressed and of lower welfare;
  * a boarded animal is kept well (five species for 40 days), and its owner sees to its health from any pen.
* **Welfare**:
  * it follows the weighted formula and its bands (the stress penalty and the health cap too);
  * a Critical animal falls ill faster than an Excellent one;
  * daily care makes welfare Good, and an owner's affection Excellent;
  * the owner is told when an animal is in a bad way, at most once a day; an owner who comes back is told at once; a
    long spell is told every day but kept once in the animal's event log;
  * an animal in a bad way from low health shows that it needs help.
* **Illness**:
  * a filthy, crowded coop makes its hens ill (from a dirty pen), and one mucked out every two days does not, for 30
    days;
  * many short steps, as frequent requests make them, give the same illnesses at the same hours (within 2) as hourly
    steps;
  * a well-kept animal of every species stays well for a month; a lone cow on a pasture stays well and at ease;
  * three cows on a full pasture never mucked out fall ill between day 5 and day 20 (§12 S2);
  * an infection spreads to pen-mates until the sick one is taken away (an animal never catches its own; remains spread
    it for 48 hours);
  * worms need the vermifuge and leave immunity behind;
  * a mild upset passes by itself, a bad one must be treated; poisonous food shows as food poisoning within hours;
  * a mending illness comes back if its cause returns, else it clears and leaves immunity;
  * an illness the game no longer has does not keep an animal from healing.
* **Medicine and the vet**:
  * the right medicine cures, the wrong one does not, a big animal needs more, and too much poisons;
  * a weak dose does not stretch a strong one, nor a cheap wash the veterinarian's work;
  * nor does a weak dose given again and again (the vet then a wash every 4 hours, a draught then a wash every 6): the
    treatment takes off what each hour's strongest dose would, and the stronger dose keeps its own end; hour by hour,
    the stronger dose works to its end, then the weaker one to its own;
  * three powers at once (the vet, a Standard draught at 14 hours, a wash every 4 hours from 16 to 44) take off each
    hour's strongest dose, each to its own end (186), and once the vet's work ends the draught works first, to its own
    end; after each of 15 000 random doses of three or four powers the record holds each hour's strongest dose exactly;
    a record keeps at most 8 steps, lets the weakest go and saves every step; a medicine that only acts at once leaves
    no treatment working, so nothing mends by it;
  * washes asked for one after another (14 within a game minute) are refused after the first, each with the wait, and
    take nothing; at its interval a wash does its full good again;
  * the wait is told rounded up, so a keeper who comes back when told may give it: a moment after a wash, *in 6 hours*;
    with 1.01 hours left, *in 2 hours*, and two hours later the wash is given (and *less than an hour* for half an hour,
    *1 hour* for one, *24 hours* for 23.5, *3 days* for 49);
  * a medicine given again within its interval does no more good yet (nothing off an illness at once, no health, food,
    water, comfort or calm, no toxins cleared, no upset eased) but still does its harm (a sting, the stress of being
    dosed, an overdose's toxins), and its interval after the last dose that did its good it does its full good again;
  * a dose too soon puts nothing off: the interval counts from the last dose that did its good (washes at 0, 5.9 and 6
    hours; washed every 5 hours, every other wash does its good), an overdose's too (a calming tonic at 0, 4 and 8
    hours); doses of eight other medicines never make one forget its last dose, and a record keeps one dose per
    medicine; each of the 17 medicines given once keeps its last dose and waits its interval, however few doses the
    rules keep (*Keep Doses* 64, 16, 8 or 1); a dose recorded ahead of the clock makes no wait;
  * *Give to all here* leaves out the animals that had it too lately and takes items only for the rest, says so, and is
    refused with the soonest wait when every one had it; one animal is still given a medicine with an overdose too soon
    (the overdose); a whole pen's dose of a medicine with an overdose takes items only for those it is given to (a
    calming tonic for six sheep, one dosed an hour before: items for the five);
  * its owner's medicine adds to the bond at most once per interval (a calming tonic at 0, 4 and 8 hours adds 1, then
    nothing, then 1 again);
  * the vinegar wash turns an outbreak in a small coop once it is mucked out;
  * a medicine for an illness still hidden *does not seem to help*, though it works;
  * lavender grown on the farm is the lavender remedy;
  * past *Needs Vet Above*, keeper medicine only holds an illness and the veterinarian cures it (the fee planned, a
    changed fee refused, no vet refused);
  * with no veterinarian in the world, keeper medicine cures everything;
  * a free veterinarian can be sent for, but not by a visitor.
* **Diagnosis and history**:
  * a look names an illness only from its diagnose-from stage; a look that finds an illness showing puts it in its
    history; a look still tells a named illness treated below the stages that show;
  * an illness that comes back in a stage that hides is hidden until it shows, and then its owner is told; one that
    mends again unseen still ends with its owner told it is over (*Daisy has got over its infection.*, and *Over its
    infection.* in its history);
  * the history tells an illness by its symptoms until it is named;
  * what the veterinarian or a dose did reads above the visit or the dose;
  * an hour ago reads *1 hour*;
  * the pen inspector's *Heal all* (`AnimalPenEditor.HealNow`) takes every illness, its toxins and its lost health away,
    and tells over each illness its keepers knew of (a named one, though hidden again) and no other (worms nobody knew
    of).
* **Remains**:
  * an animal that dies of an infection is recorded so, and its remains show it (lower rearing, a taint; a well-raised
    animal's rearing is higher);
  * a death no illness caused ends its health history (*Died (of thirst)*), and a slaughter is recorded once;
  * an old, ill animal whose owner is away dies of old age;
  * only an illness of the whole body that shows taints the carcass (not hoof rot, not hidden worms);
  * slaughter and butchering are the owner's, and butchering changes nothing until the carcass is laid out.
* **The pen**:
  * mucking out needs a fork, cleans, lays straw, gives manure and disturbs the herd;
  * mucking out a fresh barn with no straw says that straw is what is missing, and fresh straw waits its turn;
  * pouring water mixes, and scrubbing the trough changes foul water;
  * a stranger on public land may not make a trough murky, but may fill a dry one; nor a little at a time (300 pours of
    0.4 L, or pours a nearly full trough keeps small), nor by pours too small to measure in a big trough (400 pours of
    0.05 L into 2000 L), while cleaner water and a keeper's own pours are let be; a stranger may pour water no dirtier
    than a murky trough's (lake water into a trough as murky as lake water: it stays as it was), but not water a little
    dirtier than the trough's.
* **Saving and determinism**:
  * a Phase 1 save (a literal version 1 file) loads with the new fields at their defaults;
  * the day a Phase 1 save was made in keeps sensible means, and its earlier days say nothing of the coat;
  * a version 2 save round-trips every new field;
  * illness and dirt caught up end exactly as watched hour by hour.
* **What is shown and the content**:
  * what is shown of health stays coarse (no severities, build-ups or hidden keys);
  * the built-in health and care content is sound.

**`CustomEditor/Animals/AnimalAlchemyTests.cs`** (8): the veterinary pack installs once and adds only its three
outcomes; it never changes an Alchemy Database asset (a wrapper takes its place in play); the outcomes count as existing
even on an asset left as it is; they use only what the rules can make; liquids made by hand are each veterinary medicine
and nothing else is; the documented recipes make them; every existing documented recipe keeps its outcome and tier with
the pack; a bottle is its outcome to the animals and each copy keeps its quality.

**`CustomEditor/Animals/AnimalProductionTests.cs`** (36, Phase 3): what each built-in species gives and content that can
never be made; the quality formula, hygiene, the tiers and Special; quality over the whole cycle, never the last meal;
the rate's factors alone and together; a hen's laying, her full nest and laying again; what stops production; an
absent owner; milk's yield; freshness; collected copies' quality, serial and origin; the code's bounds; tools, wear and
handling stress; who may collect; collecting the whole pen; a boarded animal's products; a catch-up against a watched
stretch; the ledger saved, never reusing a serial, and pruned; a version 2 save; the views and the history; *Ripen*;
and eleven for the defects of the review (a full store pauses, a lot fits its store, short cycles, freshness, the
notice, two tools, the prompt save, Special on every machine, production off, *Others Collect*, a worn-out tool).
[Chapter 05](05-Production.md#tests) says more, with the Play Mode checks.

**`CustomEditor/Animals/AnimalBreedingTests.cs`** (23, Phase 4): the species' breeding, behaviour and perks; a pair's calf
and what it knows; who may be bred (every refusal, the cost); close kin and kinship; the chance to conceive; inheritance,
mutations, rare traits, coats and inbreeding; a whole clutch; nursing; losing the young; births in a catch-up and after
a load; companions, missing one, rivals; time together, perks, calm handling, a bond that never stands in for welfare;
what each viewer is shown; an older save; and the review's cases (young on the way in every limit, ids fixed at the
mating, time together and the day's history, bonds in a crowded pen and rivals, the boarding stable, a look's discretion,
the cost taken whole). [Chapter 06](06-Breeding-Genetics-and-Behaviour.md#tests) says more, with the
Play Mode checks.

**`CustomEditor/Animals/AnimalTradeTests.cs`** (25, Phase 5): crate codes; crating and letting out to a new owner, once,
whole; a crate of the wrong kind; what keeps an animal from travelling; crates through a save and their wait in real
days; worth by grade and stage; pen levels (room, price, rights, short payment), their prices and upkeep by size, and
their room, comfort, cleanliness and production; upkeep, disrepair, paying and paying ahead; the hopper, the waterer and
the egg trays (who may take what, capacity, aging); a week of machines caught up as watched; breed yields; an older
save; the pen's picture; wool quality in Crafting; and the review's cases (choosing a crated animal, who may take out a
machine, upgrades off, undoing a failed crating, yields that do not count twice, orphan tray lots). [Chapter 07](07-Trading-Upgrades-and-Automation.md#tests) says more,
with the Play Mode checks.

**`CustomEditor/Harvesting/HarvestRearingTests.cs`** (9, in Harvesting's suite): rearing scales the quality of every
piece, and a body that names no rearing is judged as before; the farm anatomies are sound and every material can be
planned; a neglected cow's tannery hide is worse than a well-raised one's; a farm animal's remains show in its carcass;
the carcass lies outside the fence away from the feeder and trough; a farm carcass is its owner's only; the hours dead
spoil it.
