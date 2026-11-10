# 07 — Trading, Habitat Upgrades and Automation

[← Animals](README.md)

Animal husbandry is now a trade. A keeper can **crate** an animal they raised and sell it to any merchant, or trade it to
another player, who lets out the very same animal: its name, history, family and genes come with it. A pen can be
**improved** to make room for more animals, keep them more comfortable and cleaner, and help them produce. It can be
fitted with three machines: a **feed hopper**, an **automatic waterer** and **egg trays**. These take over the
day-to-day rounds while the keeper is away. Improvements and machines cost **upkeep** by the day: a pen that owes too
much falls into disrepair until its keepers pay. Breeds have **specialities** (a Leghorn lays more, a Holstein milks
more), and the quality of wool now carries into Crafting.

* [Crating and selling animals](#crating-and-selling-animals)
* [Letting a crated animal out: changing hands](#letting-a-crated-animal-out-changing-hands)
* [What an animal is worth](#what-an-animal-is-worth)
* [Improving a pen](#improving-a-pen)
* [The machines](#the-machines)
* [Upkeep and disrepair](#upkeep-and-disrepair)
* [Breed specialities](#breed-specialities)
* [Cooking, Crafting and Alchemy](#cooking-crafting-and-alchemy)
* [The window](#the-window)
* [The rules](#the-rules)
* [Multiplayer: what the server checks](#multiplayer-what-the-server-checks)
* [Scripting](#scripting)
* [Saving](#saving)
* [Tests](#tests)

## Crating and selling animals

On the animal's **Overview** page its owner sees what it is worth in trade (*"In trade: fine stock, worth about x2.6 a
young one"*) and a **Crate it** button (two clicks: the first asks). Crating takes the animal out of the pen and gives its
owner a crate: a livestock item of its breed (*"Leghorn Chick (crate)"*) whose per-copy code names the animal.

| An animal is not crated when | Why |
|---|---|
| It is not its owner who asks | Only an owner sells |
| It is expecting | She does not travel now |
| It is nursing young in its pen | She is not parted from them |
| Its health is below the rules' **Crate Min Health** (30) | It is too weak to travel |
| The rules' **Allow Crating** is off | Animals are not crated in this world |
| The owner's bag has no room for the crate | A crate is never dropped on the ground, where anyone could take the animal |

While crated, an animal is **out of the world**. It is not in a pen, is not simulated, and does not count against
anyone's animals. It waits as it was crated: no hunger, no growth, no production, its pregnancy and bonds as they were.
Its history records the crating. Its pen-mates miss it as they would any animal moved away.

A crate can be:

* **sold to a merchant**: any merchant that buys livestock pays its worth, copy by copy (see below). A merchant resells
  what it buys without the per-copy code, so a crate bought back from a merchant holds a fresh young animal of that
  breed, not the one sold;
* **traded to a player** in the trade window, which keeps per-copy codes: the buyer lets out the very animal;
* **let out again** by its crater, into any pen they may keep animals in (a way to move an animal between scenes).

A crate waits **14 real days** (the rules' **Crate Keep Days**, counted in real time, so an owner away for a week does
not lose it). After that the animal is gone: the carrier sold it on, and its crater is told if they are in the world. A crate whose animal is gone is refused when it is
let out (*"The crate is empty"*), but it still sells to a merchant for its worth. No animal waits outside the world for
ever, so crating cannot be used to keep more animals than the pens allow.

## Letting a crated animal out: changing hands

**Let it out** works as for any crate: the player must be allowed to keep animals in the pen, there must be room, and
the habitat must suit the species. The player's limit on animals counts the young they have on the way. The carried
livestock list shows each crated animal on its own line (*"Leghorn chicken, adult female (fine) - crated"*), so the
player chooses which one to let out. The request names the copy by its code, and the server takes that copy from the
player's own bag or none. Plain crates of a kind share one line, and its **Let it out** takes a plain crate only: a
crated animal is let out only when its own crate is chosen, never as "any crate of the kind".

The server then validates the transfer:

* the crate's code must name an animal still in the world's crated store, and agree with it in everything the code
  holds: the item's species and breed, and the animal's sex, stage and grade (a code in another kind of crate, or a stale
  or forged copy, is refused);
* the animal leaves the crated store as it enters the pen, so **a crate is let out once**. The same code again, from a
  copy that should not exist, finds the crate empty.

Let out by someone other than its crater, the animal **changes hands**:

* it becomes the new keeper's, with their name;
* its origin becomes "traded" and the day it was acquired is today;
* it keeps the rules' **Affinity Kept** share (25%) of its bond: what it felt for the last keeper is mostly not theirs;
* its history records *"Changed hands: from Ann to Bob"*;
* it keeps its name (unless a new one is given as it is let out), its whole history, its parents and grandparents, its
  genes and its product history.

Let out by its own crater, nothing changes hands and the bond is kept whole. In both cases it lives again from the hour
it is let out.

## What an animal is worth

An animal's **grade** (0-15) is judged as it is crated, from the rules' weights:

| Part | Weight |
|---|---|
| Health | 0.25 |
| Nutrition | 0.15 |
| Welfare | 0.15 |
| Genetic merit (the mean of its production quality and rate, efficiency, resistance, fertility and longevity genes, less half its inbreeding) | 0.30 |
| A rare trait | 0.15 |

The grade is described in words: poor (0-3), plain (4-6), sound (7-9), fine (10-12), exceptional (13-15).

A crated animal is worth its breed's price for a young one (the crate's price) × its stage's worth × its grade's worth:

| Stage | Newborn | Baby | Juvenile | Adolescent | Adult | Senior |
|---|---|---|---|---|---|---|
| Worth | 0.7 | 1.0 | 1.2 | 1.6 | 2.4 | 1.0 |

Grade 0 is worth × (1 − **Grade Spread**) and grade 15 × (1 + Grade Spread), with Grade Spread 0.5. The grade and the stage travel in the crate's
code, so a merchant (and a client showing the merchant's prices) prices the copy the same on every machine, without
the animal's record. A plain crate from a merchant is worth its price. Merchants pay their own share of what an item is
worth, as for every item.

Raising a well kept, well bred animal to adulthood is therefore worth several times what its young cost. That is the
profession's trade: buy or breed young, raise them well, sell the surplus, and keep the best for breeding.

## Improving a pen

On the new **Pen** page of the window, anyone who may keep animals in the pen can improve it for the price shown. Each
level's numbers are its whole effect:

| Level | Name | Cost (24 m²) | Room | Comfort | Floor dirt | Products | Upkeep a day (24 m²) |
|---|---|---|---|---|---|---|---|
| 1 | Basic | (as built) | | | | | 0 |
| 2 | Improved | 400 | +25% | +6 | −15% | +5% | 2 |
| 3 | Fine | 1200 | +50% | +12 | −30% | +10% | 5 |

* **Room**: the pen's usable area is its area × (1 + room), so it takes more animals (a set capacity grows the same way),
  and they are less crowded. Crowding is judged against the usable area.
* **Comfort**: an improved pen eases what bothers its animals (draughts, damp, a hard floor). Its comfort is added to
  where their comfort settles, never past 100, so it counts when something is wrong: cold, crowding, a dirty floor.
* **Floor dirt**: the animals soil its floor less (drains, raised floors).
* **Products**: its animals make their products faster.

A level's cost and upkeep are given for a pen of the rules' **Reference Area** (24 m²). Another size pays × its area / 24,
from half to four times as much. The number of levels and every number in them are data: the rules' **Upgrades ▸
Levels**.

## The machines

A pen can be fitted with three machines, each for its price, and each taken out again for nothing (two clicks: the first
asks) by **who fitted it, or one who may care for others' animals in the pen** (its tenant and those they let in). A
passer-by on public land, who may keep animals there, does not take out what someone else paid for. A hopper needs a
pen with a feeder, a waterer one with a trough.

| Machine | Price | Upkeep a day | What it does |
|---|---|---|---|
| **Feed hopper** | 300 | 1 | A sealed store of 240 portions over the feeder. Each hour, before the animals eat, it drops feed into the feeder while the feeder holds less than half of what it can, oldest first. Feed ages half as fast in it as in the open feeder |
| **Automatic waterer** | 250 | 1 | A float valve on the pen's water supply: 8 L of clean well water an hour into the trough, up to what it holds. Fresh water running in mixes the dirt down and washes it out as it overflows, so the trough stays clean |
| **Egg trays** | 150 | 0.5 | Eggs roll out of the nest into the trays as they are laid, up to 36. The nest never fills, so hens keep laying while nobody collects |

* **Filling the hopper**: carried food has a **Hopper** button next to **Stock** (a StockFeeder request with the text
  "hopper"). What does not fit is not taken.
* **Taking out the hopper**: its feed is tipped into the feeder as far as it fits. The rest is lost, as the result says.
  Egg trays are only taken out once they are empty.
* **What waits in the egg trays** is each lot as it was laid: whose hen laid it, her name, breed and owner, its cycle,
  when it was made and how she lived through it. It ages there as in the nest (past its Fresh Hours it loses quality),
  goes into the product ledger with its whole origin when collected, and becomes copies with their quality and their
  lot. A lot of a product its species no longer gives (the content changed) can never be collected, and is dropped. The trays hold every keeper's eggs. Each lot is taken only by someone who may collect from its hen: her owner,
  or (the rules' Others Collect on) one who may care for others' animals in the pen.
* **Collecting** the whole pen (**Collect All**) takes what waits in the trays too. A Collect request with the text
  "trays" takes only the trays. A hen's owner is told when someone else takes her eggs from the trays.

Machines are what partial automation means here: they do the daily feeding, watering and egg gathering. They do not do
the care that needs a keeper: grooming, mucking out, medicine, breeding.

## Upkeep and disrepair

A pen's upkeep (its level's and its machines') falls due by the game day, hour by hour, as it lives. It is owed even
while nobody is there, and at most **Max Owed Days** (30) of it is ever owed.

* **Disrepair**: a pen that owes more than **Grace Days** (3) of upkeep is in disrepair:
  * its machines stand still;
  * its level's comfort, cleanliness and production count for nothing;
  * its room stays, so nobody is turned out;
  * it cannot be improved or fitted until it is paid.
* **Paying**: anyone who may tend the pen pays what it owes (**Pay upkeep**), or that and a week ahead (**Pay a week
  ahead**: a PayUpkeep request with the count 7), for the sum shown. What is paid ahead is spent first, so a keeper who
  pays ahead before going away comes back to machines that kept working. Money is paid in whole coins, and what is
  paid over is paid ahead.
* Money goes to the city: it is a sink, the answer to endless accumulation (the spec's Balancing Issue 2), together with
  pen room, the per-player limit and the crate's wait.

## Breed specialities

A breed's **Product Yield** speeds the making of its **Specialty** product (or of every product, when it names none), on
top of everything else that sets the pace:

| Breed | Speciality | Yield |
|---|---|---|
| Leghorn | eggs | 1.2 |
| Rhode Island Red | eggs | 1.05 |
| Khaki Campbell | duck eggs | 1.25 |
| Holstein | milk | 1.2 |
| Jersey | milk | 1.05 (and the finest quality) |
| Saanen | goat milk | 1.2 |
| Nubian | goat milk | 1.05 |
| Merino | wool | 1.25 |
| Jacob | wool | 1.05 |
| Boer, Angus, Suffolk | bred for meat | 0.85, 0.8, 0.85 |

A yield speeds the cycles only: how much a lot holds (milk, wool) still follows how the animal lives, so nothing counts
twice. With the pen's level, a Leghorn in a Fine coop lays about a third faster than a plain hen in a basic one.

## Cooking, Crafting and Alchemy

* **Crafting**: wool's quality shapes the batch of raw fibre it makes at a tailor's bench. Crafting's new
  `ICraftFibreQuality` asks the item for each copy's quality: a typical copy (0.6) makes the supply's typical batch;
  better wool has up to half the dirt and short fibre; poorer wool has up to three quarters more. A copy without a code
  (bought in) is typical.
* **Alchemy**: eggs and milk brew as their ingredients (Egg Yolk, Goat Milk), as since Phase 3.
* **Cooking**: there is no Cooking system in the project. Eggs and milk are food items whose copies keep their quality
  (a fine one sates more) and their origin. `AnimalProducts.QualityOf`, `TierOf` and `OriginOf` are the API a Cooking
  system reads.
* **The economy**: merchants pay for products by quality (Phase 3) and for crated animals by worth (above). Upgrades,
  machines and upkeep are paid in the animal trade's currency: the veterinarian's, or the default.

## The window

The middle column has a sixth page, **Pen**:

* its level and room (usable and as built), the next level, its price and what it does;
* each machine, fitted or for sale, and what it holds;
* its upkeep a day, what it owes or how far ahead it is paid, and disrepair;
* the buttons **Improve: Fine (1200)**, **Pay upkeep (6)**, **Pay a week ahead (35)**, and one per machine (**Feed
  hopper (300)**, **Take out the feed hopper**).

Prices are the server's, shown as they are: the request carries the price seen, and a different price now is refused.

The **Overview** page has **Crate it** for an owner's animal, enabled while it is fit to travel (the reason shows when it
is not), and its worth in trade. The carried livestock list shows each crated animal on its own line, and carried food
has a **Hopper** button when the pen has a hopper. The pen itself shows its machines: a hopper over the feeder, a pipe
and tank by the trough, trays on a stand between them. They are placeholder shapes on the simple fence look, like the
feeder and trough.

Sounds: crating (**Crate**), building and fitting (**Build**) and a contented call on letting an animal out. Add the
clips in the Animal Audio component. An empty clip is silent.

## The rules

**Animal Rules ▸ Trading, upgrades and automation**:

| Rules | Fields |
|---|---|
| **Trade** | Allow Crating, Crate Min Health (30), Crate Keep Days (14 real days; 0 = for ever), Affinity Kept (0.25); the stage worths; Grade Spread (0.5); the grade's weights |
| **Upgrades** | Enabled (off: no level counts and nothing can be built or fitted, but a fitted machine can still be taken out); Levels (name, cost, space, comfort, dirt, production, upkeep per day); Reference Area (24 m²); each machine's cost and upkeep; Hopper Capacity (240), Hopper Keeps Feeder (0.5), Hopper Age Pace (0.5); Waterer Litres Per Hour (8); Tray Capacity (36); Grace Days (3); Max Owed Days (30) |

Breeds: **Product Yield** and **Specialty** (species ▸ breeds). Products: **Trays** (it rolls into egg trays; the built-in
eggs and duck eggs). Validation warns of a level that does less than the one below it.

## Multiplayer: what the server checks

Everything is a validated command (`AnimalActionKind.Crate`, `Upgrade`, `PayUpkeep`, and Release, StockFeeder and Collect
with their new texts), planned and committed on the server or in single player:

* **Crate**:
  * only the animal's owner may crate it, and only while it is fit to travel and their bag has room for the crate;
  * the crate is given only after the animal has left the world;
  * the director refuses to crate an animal of a breed the Item Database has no livestock item for, so an animal never
    leaves the world without a crate to hold it.
* **Release of a crate**:
  * the copy is taken from the player's own bag by its code (a crated animal only when its own code is chosen);
  * the record named must still be crated, and agree with the code in kind, sex, stage and grade;
  * the transfer is once only;
  * room, habitat and the keeping limit are checked as for any release.
* **Upgrade**:
  * only one who keeps animals in the pen may build in it; a machine is taken out by who fitted it or the pen's keepers;
  * the price must be the one shown, and the money is taken before the commit (given back if the commit fails);
  * a pen in disrepair is not improved;
  * the egg trays are taken out only when empty.
* **PayUpkeep**: one who tends the pen; the sum shown.
* **Collect from the trays**: each lot only by who may collect from its hen.
* **A commit that fails half way** (an exception) is undone on the book's side too: an animal crated by it lives in its
  pen again, one let out goes back into its crate. A crate the server could not give puts its animal back in the pen.
* **Prices on a client** are the server's picture (`AnimalPenView`). The client never prices anything itself, except a
  crated copy's worth, which its code decides the same everywhere.

## Scripting

```csharp
// Worth and grade
int grade = AnimalTrade.Grade(record, rules);                 // 0-15, as it would be crated now
float worth = AnimalTrade.ValueFactor(stage, grade, rules);   // x the breed's young price
string why = AnimalTrade.WhyNotCrate(record, species, herd, catalog, rules); // null: it can be crated
int code = AnimalItemCode.Crated(sex, stage, grade, serial);  // a crated animal's copy code
AnimalItemCode.TryCrated(code, out sex, out stage, out grade, out serial);
AnimalCrated c = book.Crated(serial);                         // the animal a crate holds (null: gone)

// Pen level and room
int level = AnimalUpgrades.Level(pen, rules);
float area = AnimalUpgrades.Area(pen, spec, rules);           // usable area at its level
long price = AnimalUpgrades.NextLevelPrice(pen, spec, rules); // -1: at the top

// Machines and upkeep
bool working = AnimalUpgrades.Working(pen, spec, rules, AnimalDevice.Hopper); // fitted and not in disrepair
float day = AnimalUpgrades.UpkeepPerDay(pen, spec, rules);
long due = AnimalUpgrades.PayPrice(pen, spec, rules, daysAhead);
bool broken = AnimalUpgrades.InDisrepair(pen, spec, rules);

// Breed yield
float yield = AnimalProduction.BreedYield(breed, productSpec);
```

The requests:

| Request | Fields |
|---|---|
| Crate | `{ kind = Crate, pen, animal }` |
| Release of a chosen crate | `{ kind = Release, pen, item, code }` (code `AnimalCommands.PlainCrate`, 1: any plain crate) |
| Improve | `{ kind = Upgrade, pen, text = "level", expected = price }` |
| Fit a machine | `{ kind = Upgrade, pen, text = "hopper" \| "waterer" \| "trays", expected = price }` |
| Take a machine out | `{ kind = Upgrade, pen, text = "remove:hopper" }` |
| Pay upkeep | `{ kind = PayUpkeep, pen, count = daysAhead, expected = price }` |
| Fill the hopper | `{ kind = StockFeeder, pen, item, count, text = "hopper" }` |
| Collect from the trays | `{ kind = Collect, pen, text = "trays" }` |

Merchants price crated copies through `LivestockItemSO`'s `IItemInstanceValue`. Crafting reads wool through
`ICraftFibreQuality`, which any item can answer.

## Saving

The world's save is **version 5**:

* **Crated animals**: the crated store, each animal with its serial, when it was crated (game and real hours), by whom
  and its grade, plus the last crate serial given;
* **Pens**: each pen's level, its machines, its hopper's feed, what waits in its trays and the upkeep it owes (below 0:
  paid ahead).

A version 4 save loads with every pen as built, no machines, nothing owed and nothing crated. Species assets are
**Data Version 5**. An older asset takes its egg-tray products and its breeds' yields from the built-in species of its id
(a breed naming its own specialty keeps it) until **Animals ▸ Update Species Assets To Phase 5** writes them in.

## Tests

`AnimalTradeTests` (Edit Mode, 25):

* **Crates**:
  * crate codes: their limits, and that they are never a fresh crate's seed;
  * crating takes an animal out of the world, and it is let out whole, once, by its new owner (owner, name, genes,
    history, the share of its bond, lived from then);
  * a crate of the wrong kind is refused, and a crater keeps the bond;
  * what keeps an animal from travelling;
  * crates through a save, and their wait in real days;
  * worth by grade and stage.
* **Pen levels**:
  * levels: room, the price shown, rights, short payment;
  * prices and upkeep by size;
  * room, comfort, cleanliness and production by level.
* **Upkeep**: it falls due, disrepair (machines stop, comfort lost, room kept, no building), paying, paying ahead, its
  ceiling.
* **Machines**:
  * the hopper: filling, dispensing, sealed aging, taking it out;
  * the waterer: filling, washing out, its kind of water;
  * egg trays: a hen goes on laying, origin through the ledger, capacity and splitting, who may take what, removal,
    aging;
  * a week of every machine at work caught up equals the week watched hour by hour.
* **Breeds**: yield, and only for their specialty.
* **Saves and the picture**: a version 4 save; the pen's picture (level, prices, rights, trays per viewer, crate fields
  per owner, disrepair).
* **Crafting**: wool quality's effect on fibre.
* **The independent review's cases** (each shown to fail with its defect put back):
  * a crated animal let out only by its own crate, and only as its code says;
  * machines taken out only by who fitted them or the pen's keepers, and none fitted where it has nothing to work on;
  * upgrades switched off;
  * a failed crating or letting out undone on the book's side;
  * a yield that speeds cycles without also filling each lot;
  * tray lots of a product no longer given.

**Manual Play Mode checks** (not verifiable in Edit Mode):

1. Crate an adult hen. Sell it to a merchant: its price is its worth, not a chick's.
2. Trade a crate to a second player in the trade window. They let it out: the same name and history, now theirs.
3. Improve a pen and fit the three machines.
   * The machines appear on the pen.
   * Fill the hopper.
   * Watch the feeder stay stocked, the trough stay full, and eggs gather in the trays.
4. Debug **Owe a week** in the pen inspector: the Pen page shows disrepair and the machines stop. **Pay upkeep** puts
   them back to work.
5. Put a fine fleece and a poor one into a tailor's bench: the fine one is cleaner.

The pen inspector shows each pen's level, machines and upkeep in Play Mode, with the debug buttons **Owe a week** and
**Fit all**.
