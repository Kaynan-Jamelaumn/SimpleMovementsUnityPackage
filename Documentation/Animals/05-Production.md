# 05 — Production

Living animals give things: hens and ducks lay eggs, cows and goats give milk, sheep grow wool. How much they give and how
good it is follow how they live through each production cycle: what they are fed, how they are kept, how healthy, clean
and comfortable they are, and what they were born with. Every egg, jug of milk and bundle of wool remembers its quality
and, where the rules run, which animal it came from, which cycle and how that animal lived meanwhile.

* [What each species gives](#what-each-species-gives)
* [How fast: the production cycle](#how-fast-the-production-cycle)
* [How good: quality and tiers](#how-good-quality-and-tiers)
* [Storage: what waits to be collected](#storage-what-waits-to-be-collected)
* [Collecting](#collecting)
* [The products as items](#the-products-as-items)
* [Traceability: the product ledger](#traceability-the-product-ledger)
* [The window, the body and the history](#the-window-the-body-and-the-history)
* [Authoring products](#authoring-products)
* [The rules (Animal Rules ▸ Production)](#the-rules-animal-rules--production)
* [Multiplayer: what the server checks](#multiplayer-what-the-server-checks)
* [Scripting](#scripting)
* [Saving](#saving)
* [Tests](#tests)

## What each species gives

| Species | Product (item) | Who | A lot | Cycle (well kept) | Waits at most | Collected | Keeps |
|---|---|---|---|---|---|---|---|
| Chicken | Egg (`animal:product/egg`) | hens | 1 egg | 26 h | 6 (the nest) | by hand (*Collect*) | fresh 48 h, then −0.15 quality a day |
| Duck | Duck egg (`animal:product/duck_egg`) | ducks | 1 egg | 30 h | 6 | by hand (*Collect*) | fresh 48 h, then −0.15 a day |
| Cow | Milk (`animal:product/milk`) | cows | 2 jugs (more or fewer by how she lived) | 8 h | 6 (the udder) | *Milk*, with a milking pail | keeps while it waits |
| Goat | Goat milk (`animal:product/goat_milk`) | nannies | 1 jug (by how she lived) | 10 h | 4 | *Milk*, with a milking pail | keeps |
| Sheep | Wool (`animal:product/wool`) | ewes and rams | 3 bundles (by how it lived) | 336 h (two weeks) | 3 (one fleece) | *Shear*, with shears | keeps |

Pigs, horses and rabbits give nothing while they live (their remains are butchered, chapter 04).

An animal gives only in a stage of its life with *Production Enabled*: the built-in species give as adults and seniors,
never as young. Milk does not wait for a calf or a kid yet: lactation tied to birth comes with breeding (Phase 4).

**The tools.** Two of animal husbandry's own tools are added to the built-in tools and supplies:

| Tool | Item | Tag | Quality | Price | Uses | Weight |
|---|---|---|---|---|---|---|
| Sheep shears | `animal:tool/shears` | `shears` | 0.7 | 120 | 20 | 0.4 |
| Milking pail | `animal:tool/milking_pail` | `milking` | 0.7 | 400 | 12 | 1.2 |

A milking or a shearing wears the tool one use per animal, like the care tools (a pen-wide collection wears each tool
it used: the pail once per cow, the shears once per sheep).

## How fast: the production cycle

Production is worked out in every step of the animal's life (`AnimalSim`, hour by hour), so a week nobody watched makes
what a watched week would make. Each product has a **cycle**: the hours a well kept, typical animal takes to make one lot.
A step that ends one cycle and begins the next shares its hours between them (the lot is made at the point of the step
where its cycle ended), so long steps make what short ones do. Each step moves the cycle on by

> step hours × pace × rate ÷ cycle hours

* **Pace** is the pace the animal lives at: 1, or the rules' *Away Needs Rate* (0.35) while its owner is logged out. An
  animal whose owner is away lives, and makes, slower.
* **Rate** is a product of how it lives, so **no one thing done well is enough**:

  | Factor | From | 1 when |
  |---|---|---|
  | Welfare band | the band's *Production* (Critical ×0.5, Poor ×0.8, Average ×1, Good ×1.05, Excellent ×1.15) | Average |
  | Illness | the worst stage's ×*Production* of its illnesses (multiplied) | well |
  | Genes | its production-rate gene: 1 ± *Gene Spread* (0.25) | a typical animal |
  | Age | *Senior Rate* (0.6) for a senior | not old |
  | Nutrition | 0.5 at *Min Nutrition* (30), rising smoothly to 1 at 75 | well nourished |
  | Comfort | 0.7 at 20, rising smoothly to 1 at *Comfort Full* (60) | comfortable |

  Perfectly fed but miserable, a cow gives at 0.7; comfortable but barely fed, at 0.5; both, at 0.35.

**It stops** (and the window says why) when:

| Status | When | Shown as |
|---|---|---|
| Starving | its satiety or hydration is 0 | *starving: it gives nothing* |
| Ill | health below *Min Health* (35) | *too weak to give any* |
| Underfed | nutrition below *Min Nutrition* (30) | *too poorly fed to give any* |
| Full | what waits fills its storage: the cycle pauses | *full: collect it* (*ready* for a fleece) |
| Boarded | it is at the boarding stable | *gives nothing at the boarding stable* |
| Resting | not at this stage of life or of this sex | *gives no egg now* |
| Off | production is turned off in the rules | *gives nothing: production is off* |

A stopped cycle keeps its progress: a hen that was hungry a day lays later, not never.

**How much.** An egg is an egg: a lot holds its *Per Cycle* units. Where the *Yield Scales* (milk, wool), the lot holds
*Per Cycle* × the cycle's mean rate (between ×0.3 and ×1.3), rounded, at least 1: a well kept cow gives two jugs a
milking, a poorly kept one one. A lot never holds more than its store has room for: a fleece is three bundles however
fine the sheep, and a cow with five jugs waiting gives one more.

## How good: quality and tiers

A lot's quality is judged when it is made, from the **means of its whole cycle** (every hour of it, the bad ones too),
never from the moment it is collected. A cow fed her favourite food just before milking does not give premium milk; a
hen that went hungry for most of the day her egg formed lays a poorer egg, however well she is fed when she lays it.

> Q = (0.30 N + 0.25 W + 0.20 G + 0.15 H + 0.10 E) ÷ (the sum of the weights)

| Letter | What | Over the cycle |
|---|---|---|
| N | nutrition | its mean nutritional condition (0-100) |
| W | welfare | its mean welfare |
| G | genetics | its production-quality gene (0-1) |
| H | health | its mean health, less half the worst illness's severity |
| E | environment | the mean of its comfort and of how clean its pen's floor is |

Then:

* **its owner's care**: + *Owner Care Bonus* (0.04) × the share of the cycle in which its own owner had cared for it within
  the last day (a farm hand's care does not count);
* **age**: + *Senior Quality* (−0.05) for a senior;
* **hygiene**: × (1 − w + w × (0.55 + 0.45 × its mean hygiene ÷ 100)), where w is the product's *Hygiene Weight*
  (0.25 for eggs and milk, 0.5 for wool: a dirty sheep's fleece is matted and cheap);
* within the rules' *Quality Min* (0.05) and *Quality Max* (1).

The weights, the bonus, the senior's change and the limits are settings (*Animal Rules ▸ Production*). Weights that do
not add up to 1 are normalised.

**Tiers.** A quality is told in six tiers:

| Tier | From |
|---|---|
| Poor | — |
| Common | 0.35 |
| Good | 0.52 |
| Excellent | 0.68 |
| Premium | 0.82 |
| Special | 0.92, **and** an animal with a rare trait |

An animal without a rare trait gives at most Premium. A lot keeps the tier it had when it was collected.

**Freshness.** Eggs keep fresh 48 hours in the nest; after that they lose *Stale Loss Per Day* (0.15) of their quality a
day (down to *Quality Min*). Milk and wool keep while they wait. Collected, a copy keeps its quality.

## Storage: what waits to be collected

What is made **waits at the animal** (a nest, an udder, a fleece) up to its *Capacity*. When the store is full the cycle
**pauses** (*Full*): nothing more is made, and none of the hours it waits count in the next lot, so an udder milked a day
late gives a full milking, and a fleece shorn a fortnight late is not followed by another an hour after. Its owner is
told once, when the lot that fills it is made (*Henrietta: 6 eggs to collect.*). Collected, the next cycle starts.

* What waits stays with the animal wherever it goes: moved to another pen, or to the boarding stable when a lease ends.
  A boarded animal makes nothing, but its owner can collect what it made before from **any** of their pens' windows, as
  they can see to its health from there.
* Nothing is made twice: a lot is made once in the simulation, and taken once by a collection (§[Collecting](#collecting)).

## Collecting

In the pen's window, on the animal's **Care** page, **What it gives** shows each product: the button (*Collect*, *Milk*,
*Shear*), what waits and its tier (*Milk: 2 jugs of milk (Good)*), how the animal is making it and how far the next lot
is (a gauge), and the tool it takes and the one the player would use (*With your milking pail*). **From all here**
collects that product from every animal of the pen the player may collect from. In the pen column, **Collect all**
(shown while something waits) collects everything waiting that the player has the tools for, and says what waits
(*Waiting: 5 eggs, 6 jugs of milk*).

A collection is a request like any other (`AnimalActionKind.Collect`), planned, taken and committed by the rules:

* **Who**: the animal's owner, or one who may care for others' animals in that pen (a farm hand) while the rules' *Others
  Collect* is on (the default): the copies go into the farm hand's bag, and its owner is told (*Ann collected 2 eggs from
  Henrietta.*). The lot stays its owner's animal's in the ledger. With *Others Collect* off only its owner collects, and
  others are not shown what waits.
* **What**: the request's text names a product (`egg`), or nothing for all of them; its animal names one animal, or
  nothing for the whole pen.
* **Tools**: *Milk* needs a tool tagged `milking`, *Shear* one tagged `shears`. Without one it is refused (*You need a
  milking pail to milk Daisy.*); collecting the whole pen takes what it can and leaves the rest. The best tool carried
  for each product is used, and each tool used wears one use per animal.
* **Handling**: milking stresses a skittish cow by 2 × (1 − its temperament), shearing a sheep by 4 ×; twice that with a
  worn-out tool (*Your worn-out tool is rough on it.*).
* **Refusals** say why: *Henrietta has no egg ready yet (about 9 hours more).*, *Henrietta: too poorly fed to give any.*,
  *Not your animal.*, *There is nothing to collect.*
* **The answer**: *You collect 2 eggs from Henrietta (Good).*, *You milk Daisy: 2 jugs of milk (Excellent).*,
  *You shear Dolly: 3 bundles of wool (Good).*, *You collect 7 from 4 animals (Good on the whole).* It is heard at the
  animal (the *Collect*, *Milk* and *Shear* sounds of the Animal Database; milking and shearing use *Collect* when empty).
* **Into the bag**: the copies go into the player's inventory, each with its code; what does not fit is dropped at their
  feet (*Your bag is full: the rest is on the ground.*).

## The products as items

Each product is an item of its own, made at runtime from its id like feed and crates (`animal:product/<product id>`:
every machine makes the same item from the same id, so saves, merchants, trades and the network resolve it), or an asset
bound to that id in the Animal Database (*Assets ▸ Create ▸ SimpleMovements ▸ Animals ▸ Animal Product (Food Item)* or
*(Material Item)*).

* **Eggs and milk are food** (`AnimalProductFoodSO`, a consumable): an egg sates by its *Nutrition* (10), milk is a drink
  that also sates a little. One copy is one bite, and a copy sates by its quality: a perfect one fully, a poor one half.
  They brew in **Alchemy** as their spec's ingredient (*Egg Yolk*; milk as *Goat Milk*).
* **Wool is a material** (`AnimalProductMaterialSO`, cloth). It is named *Wool*, so the tailor's bench of **Crafting**
  takes it as raw wool fibre.
* **Each copy keeps its quality and its lot** in its per-copy code (the inventory's durability list):
  `2 + quality to the hundredth (0-100) + 128 × (its animal has a rare trait) + 256 × ledger serial`, under 2³⁰. So its
  tier, Special included, is the same on every machine and after the ledger has forgotten its lot. A copy without a code
  (bought, looted) is of middling quality (0.6) and of no known origin.
* **Merchants pay by quality**: a copy is worth its price × (0.1 + 0.9 × quality^1.4), the curve of produce and
  harvested pieces. Its price is the spec's *Value* (eggs 3, duck eggs 4, milk 6, goat milk 7, wool 8).
* **Its tooltip** tells its quality and tier (*Quality: Excellent (74%)*), where it came from (*From Henrietta, a
  Leghorn chicken (lot 12)* and *Its feeding good, its welfare excellent, kept clean*, where the ledger is known) and a
  stack's mean tier.
* **Merchants** with *Animal Goods* sell the shears and the milking pail with the care tools, and, with *Products* ticked
  (off by default), eggs, milk and wool bought in, of middling quality and no origin.

Other systems read a copy through `AnimalProducts`: `QualityOf(stack, fromTop)`, `TierOf`, `OriginOf` (null on a network
client, see below) and `ValueFactor`.

## Traceability: the product ledger

When a lot is collected it gets a **serial** from the world's product ledger (`AnimalBook.Ledger`), never given twice,
and an entry that tells where it came from (its quality kept to the hundredth, as its copies keep it):

| Field | What |
|---|---|
| serial, product | the lot |
| animal, animalName, species, breed, owner | the animal, as it was |
| cycle | its lot number (the animal's count of lots of that product) |
| cycleStart, madeAt, collectedAt, by | when its cycle began, when it was made, when and by whom it was collected |
| units, quality, tier | how much and how good |
| nutrition, welfare, health, environment, hygiene | the means of its cycle |

Each copy's code names its serial, so `AnimalProducts.OriginOf(stack)` (and `book.Ledger.Find(serial, product)`) finds
it. The ledger is saved with the animals. It lets go of entries collected more than *Keep Ledger Days* (120) ago and of
the oldest past *Max Ledger* (20 000): a copy of a forgotten lot keeps its quality, only where it came from is forgotten.
Serials run from 1 to 4 194 302 and then start again (by then the first are long forgotten). A collection saves the
world soon after (as a trade does), so what was taken never waits at the animal again after a crash.

Each animal also keeps a **production history** (`AnimalRecord.productLog`, the last *Keep Product Log* (24) entries):
what it made, and what was collected of it, with the quality and the cycle.

## The window, the body and the history

* **The Care page** shows *What it gives* above its care (see [Collecting](#collecting)), to its owner and its carers
  only. Of a boarded animal of the player's, what it made before it went.
* **The pen column** shows what waits of the player's own animals (or all, for a carer) and *Collect all*.
* **The History page** has a *Production* part: the last four entries (*2 hours ago: 1 egg collected (good).*).
* **The editor**: in Play Mode the Animal Pen inspector shows each product of each animal as the rules see it
  (*2/6 waiting (Good 61%) · cycle 45% of 26 h · lot 7 · Producing*), and **Ripen** completes every cycle under way now
  (judged by how each animal lived through it so far) so it can be collected at once.
* **The body**: a sheep grows bulkier as its fleece grows (up to ×1.14 wide and tall) and is slim once shorn (×0.9). It
  is seen by everyone (`AnimalView.fleece`); what waits and its quality are not.

## Authoring products

A species' **Products** list (*Animal Species* asset ▸ *Products*) gives what it gives. Each entry:

| Field | What |
|---|---|
| **Id** | unique among the game's products (its item is `animal:product/<id>`); no spaces, slashes or colons |
| **Name**, **Plural**, **Unit** | *Egg*, *Eggs*; a unit when one is not the name (*jug* of milk, *bundle* of wool) |
| **Description** | its item's description |
| **Female Only** | only females give it (eggs, milk) |
| **Cycle Hours** | game hours a well kept, typical animal takes to make one lot (at least 0.5) |
| **Per Cycle** | units in a lot |
| **Yield Scales** | a lot holds more or fewer by how well it lived (milk, wool); off: always Per Cycle |
| **Capacity** | units that can wait (at least Per Cycle, or it never makes one) |
| **Tools** | the tool tags collecting it needs, any one (`milking`, `shears`); empty: by hand |
| **Verb** | the button: *Collect*, *Milk*, *Shear* |
| **Fresh Hours**, **Stale Loss Per Day** | how long it keeps while it waits (0: it keeps), and what it loses after |
| **Hygiene Weight** | how much the animal's cleanliness counts in its quality |
| **Value** | the price of a perfect unit |
| **Tags** | words other systems may read (`egg`, `dairy`, `fiber`; `fleece` makes the body show it) |
| **Handling Stress** | stress collecting it gives a skittish animal |
| **Edible**, **Nutrition**, **Drink** | eaten or drunk (a food item) or a material; how much it sates |
| **Alchemy Ingredient** | the Alchemy ingredient preset it brews as (*Egg Yolk*) |

And each **stage** of its life has *Production Enabled*.

**Old species assets.** A species asset made before Phase 3 (*Data Version* below 3) takes its products from the
built-in species of its id until it is updated (**Tools ▸ SimpleMovements ▸ Animals ▸ Update Species Assets To Phase 5**),
and so does its senior stage's *Production Enabled*. One with no built-in twin gives nothing until it is given products.

**Validation** (*Validate Animals*) checks: a product's id, its cycle (under half an hour is an error), a capacity below
Per Cycle, two products of one id on a species, products with no productive stage, a collection tool that no tool is,
a product with no item, a product id two species give differently (its item is made from the first), an item binding of
a product no species gives, and the production rules (every weight 0, Quality Min above Quality Max, tiers out of order).

## The rules (Animal Rules ▸ Production)

| Setting | Default | What |
|---|---|---|
| Enabled | on | production at all |
| Nutrition / Welfare / Genetics / Health / Environment Weight | 0.30 / 0.25 / 0.20 / 0.15 / 0.10 | the quality formula |
| Quality Min / Max | 0.05 / 1 | its limits |
| Owner Care Bonus | 0.04 | for a cycle its owner cared for it through |
| Senior Quality / Senior Rate | −0.05 / ×0.6 | an old animal's |
| Common / Good / Excellent / Premium / Special From | 0.35 / 0.52 / 0.68 / 0.82 / 0.92 | the tiers (Special: with a rare trait) |
| Min Health / Min Nutrition | 35 / 30 | below them it gives nothing |
| Gene Spread | 0.25 | how much the production-rate gene moves the rate |
| Comfort Full | 60 | comfort from which it gives at the full rate |
| Others Collect | on | one who may care for others' animals in a pen collects what they made (into their own bag; the owner is told); off: only the owner |
| Keep Product Log | 24 | entries of each animal's production history |
| Keep Ledger Days / Max Ledger | 120 / 20 000 | how long, and how many, lots the ledger remembers |

## Multiplayer: what the server checks

Everything is decided by the server (or a game alone); a client only asks and is shown:

* **Production** runs only in the server's simulation. A client never makes a product, sets a quality or picks a lot.
* **Collecting** is a request through the relay, checked like any other: the player from the connection, the rate, a
  well-formed request, the pen within reach of where the player really stands, the player's rights (owner, or carer
  with *Others Collect* on), the tools they really carry (and their wear), and only what really waits. The items, with their codes, are given by the
  server into the player's own inventory. A retried request (the same token) is answered and never done twice.
* **What a client sees**: the window's picture carries what waits in words and units, the tier word, a coarse gauge of
  the next lot and the status in words, to the owner and carers only; everyone sees a fleece's growth on the body. Never
  a quality number, a serial or the ledger.
* **The ledger stays on the server.** A copy's code (in the inventory a client is sent) carries its quality and serial,
  and whether its animal has a rare trait, so a client's tooltip shows the quality and tier (Special too), but where it
  came from only where the rules run (the host, a game alone). `AnimalProducts.OriginOf` returns null on a client.

## Scripting

```csharp
// What a product is, and an animal's making of it
AnimalProductSpec egg = AnimalProduction.Spec(AnimalDatabase.Instance.Species("chicken"), "egg");
AnimalProductState st = AnimalProduction.State(record, "egg", false); // st.Waiting, st.progress, st.status, st.waiting (lots)
bool makes = AnimalProduction.Makes(record, species, egg);
float rate = AnimalProduction.Rate(record, species, rules.production, band, in stageEffects);
float q = AnimalProduction.Quality(lotMeans, record, egg, rules.production, ownerCareShare);
AnimalProductTier tier = AnimalProduction.Tier(q, AnimalProduction.Rare(record), rules.production);

// Collecting (through the director, like every request; the rules give the copies)
pen.Perform(new AnimalRequest { kind = AnimalActionKind.Collect, animal = id, text = "egg" }, player); // animal "": the whole pen; text "": everything

// A copy in an inventory
AnimalItemCode.TryProduct(code, out float q, out long serial, out bool rare); // the code alone
float quality = AnimalProducts.QualityOf(stack);           // 0-1 (0.6 without a code)
AnimalProductTier t = AnimalProducts.TierOf(stack);
AnimalProductRecord origin = AnimalProducts.OriginOf(stack); // null: no code, forgotten, or a network client
ItemSO eggItem = AnimalItems.Product("egg");

// Tests and tools: complete every cycle under way now
AnimalProduction.Ripen(record, species, rules, now);
```

The notices (`AnimalDirector.Notice`) add `ProductsReady` (storage full) and `Collected` (someone else collected).

## Saving

Phase 3 brought the save to **version 3** (Phase 4 to 4: chapter 06). Each animal record adds `products` (each product's cycle under way, its sums, and the lots
waiting) and `productLog`; the world's save adds `ledger` (its last serial and its entries). A save of version 1 or 2
loads with nothing made yet and an empty ledger, and its animals start their first cycles; a version 3 save is refused
by an older build (as any newer save is).

## Tests

`AnimalProductionTests` (36, Edit Mode):

* the built-in species give what they should (who, by hand or with which tool, the items, the wording, the stages), and
  every built-in species and the production rules are sound; content that can never be made is caught;
* the quality formula is the rules' weights over the cycle's means (normalised), with the owner's care, a senior's
  change and the limits; hygiene scales it by the product's weight; six tiers, Special only with a rare trait;
* **quality is judged over the whole cycle, never the last meal**: two hens fed alike when they lay, one hungry for most
  of the cycle, lay eggs of different quality, the hungry one later;
* **no one thing done well gives the most**: each factor of the rate on its own, and all of them together;
* a well kept hen lays about an egg a day, her nest fills at six and she stops, her owner is told, and she lays again
  once collected; a rooster, a chick, a hungry, an ill, a starving and a boarded hen give nothing, and keep their
  progress; production off in the rules; an animal whose owner is away makes slower; milk's yield follows how the cow
  lived, an egg is an egg; eggs go stale after two days;
* collected copies carry their lot's quality and serial, and the ledger knows the lot's animal, owner, collector, cycle
  and means; the per-copy code's bounds; milking and shearing need their tools, wear them and stress a skittish animal;
  only an owner or a carer collects, and the owner is told; collecting the whole pen takes what can be taken, of the
  player's own, and only the product named; a boarded animal's products wait for its owner from any pen;
* a catch-up makes exactly what a watched stretch makes (cow, sheep, goat); the ledger is saved and a loaded world never
  gives a serial again; a version 2 save loads with nothing made; the ledger lets old lots go;
* the keeper sees what an animal gives and others only its fleece, and the view carries no quality number or serial;
  the history's *Production* part; *Ripen*;
* and the cases of the review: a gifted sheep in an excellent life still grows its fleece (a lot holds no more than its
  store); a full udder pauses the cow, so milking her late still gives a full milking; a fleece shorn late is not
  followed by another; cycles shorter than a step make sound lots, the same in long steps as in short; freshness is lost
  only for the hours past it; a full nest is told of once; collecting all with a pail and shears takes both and wears
  each, and saves soon; a rare animal's Special copies are Special wherever they are read; production off says so;
  with *Others Collect* off only the owner collects; a worn-out tool is rough on the animal. Each of these fails with
  its bug put back (eleven bugs, each caught).

**Manual Play Mode checks**:

1. A coop and a pasture on a property; **Give Me A Keeper's Starter Kit** and **Give Me A Care Kit** (it holds the shears
   and the milking pail).
2. Let out hens, a cow and a sheep. In the pen inspector, **Ripen**; open the window, *Care* page: *What it gives*. Collect
   the eggs; milk the cow (try first without the pail); shear the sheep and watch it slim down.
3. Hover a copy: its quality, tier and origin. Sell eggs of two tiers to a merchant and compare the price. Eat an egg.
4. **+1 week** without collecting: the nest is full, the owner is told; *Collect all*.
5. Quit and play again: what waits, the histories and the origins of the copies are as they were.
6. Online, host plus a client: the client collects (with its own tools), sees the quality and tier of its copies but no
   origin; the host sees the origin.
