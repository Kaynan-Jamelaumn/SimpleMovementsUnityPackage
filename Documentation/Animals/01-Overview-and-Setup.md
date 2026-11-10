# 01 — Overview and Setup

[← Animals](README.md)

## Setup in a few minutes

1. *(Optional)* Run **Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Animal Database (Resources)**. It makes the
   project's rules and content at `Resources/SimpleMovements/Settings/AnimalDatabase`. Without it, the built-in rules
   are used, with eight species (chicken, duck, rabbit, sheep, goat, pig, cow, horse; three breeds each), 24 foods, nine
   illnesses, 17 medicines and seven care tools and supplies.
2. Open the **City** scene and select a **Property**, a house to rent ([Housing](../Housing/README.md)).
3. In its inspector, under *What belongs to it*, press **+ Pasture**, **+ Coop** or **+ Sty**.
   * The pen is made as a child of the property, on a free spot of its land. It is kept clear of the house, the
     gardens, the zones, the other pens and the doors, exactly as **+ Garden** places a garden.
   * Sizes are tried from largest to smallest: a pasture tries 8 × 6 m, then 6 × 6, 6 × 4, 4 × 4 and 3 × 3; a coop tries
     4 × 3 m, then 3 × 3, 3 × 2 and 2 × 2.
   * The **Tools ▸ SimpleMovements ▸ Animals** menu does the same for the selected property.
4. Shape it with its **Animal Pen** inspector:
   * **Edit Outline**: drag the points, Shift+click an edge to add a point, Ctrl/Cmd+click a point to remove it; the
     square moves the pen. The two small cubes are the feeder and the trough.
   * The **Property** inspector draws the pens in straw yellow next to the gardens (brown).
   * The property's checks warn when a pen reaches outside the land, lies under the house, or overlaps a zone, a
     garden or another pen.
5. Still in the City scene, run **Animals ▸ Create Veterinarian In City**. It makes one NPC who is both the city's
   **veterinarian** and a **merchant of the keeper's goods**: care tools, straw and soap, feed, young animals and
   medicine (see below). Replace its capsule with your model.
6. Press Play. **Animals ▸ Give Me A Keeper's Starter Kit** (or **Starter kit** in the pen inspector) gives the local
   player a crate of each species and five of every feed. **Animals ▸ Give Me A Care Kit** (or **Care kit**) gives the
   care tools, straw, soap and some medicine.
7. Rent the house (or test on a pen on public land), walk to the pen and **Interact**: the prompt says *Tend*.

For multiplayer:

* run **Network ▸ Add Missing Relays To Network World** once, so the Network World gets a **NetAnimals**
  ([chapter 03](03-Scripting-Multiplayer-Saving-Tests.md));
* run **Network ▸ Create Animal Carcass Prefab** once (also under **Animals ▸ Create Animal Carcass Prefab (Network)**,
  and done by **Create Bootstrap Scene**). Without it, no animal can be butchered in a network session. Offline nothing
  is needed.

A project that made **species assets** before Phase 2: run **Animals ▸ Update Species Assets To Phase 2** (below).

## Pens

| Setting | Default | What it does |
|---|---|---|
| Kind | Pasture | Pasture, Coop, Barn, Stable, Sty, Hutch, Pond, Hive, Magical. Each species lists the kinds it can live in |
| Outline | 6 × 4 m | The pen's ground, in metres, in its own space. Its **area** decides how many animals it takes: each species needs its own space per adult (a cow 16 m², a horse 20, a sheep 6, a hen 1.5), and the young are counted as the adults they will be |
| Feeder Capacity | 120 portions | What its feeder holds |
| Water Capacity | 80 L (+ Pasture: 4 L per m²) | What its trough holds. A new pen's trough starts full. A bigger trough fouls more slowly |
| Trough Surface | 0.6 m² (+ Pasture: area ÷ 40, 0.4–2 m²) | Open water: rain falls in (1 mm on 1 m² is a litre), the sun and wind dry it, and algae grow in it in the warmth (0: a covered tank) |
| Refill Litres Per Hour | 0 | A spring, a pipe or a stream bringing water into the trough each game hour (0: only rain and buckets). Clean water coming in also dilutes fouled water |
| Feeder / Trough Point | — | Where they stand (hungry and thirsty animals walk there). A carcass is laid out at the fence corner farthest from both |
| Grass Growth | −1 (from its kind) | A pasture grows 1.5 portions of fresh grass per m² per game day in good weather, up to *Grass Capacity* (3 per m²); an 8 × 6 m pasture feeds about two cows on grass alone. It grows nothing below about 3 °C, best from 10 to 26 °C, and slows in great heat and dry weather. Other kinds grow none (and have none) |
| Capacity Override | 0 | A fixed number of animals instead of counting by area |
| **Shelter** | −1 (from its kind) | Roof and walls, 0–1: a pasture or a pond 0 (open to the weather), a sty 0.7, a hutch 0.8, a coop 0.9, a barn or a stable 1. Keeps rain and wind off its animals |
| **Warmth** | −1 (from its kind) | °C its shelter adds in the cold: a barn or a stable 5, a coop 4, a sty or a hutch 3, open ground 0 |
| **Shade** | −1 (as its roof) | Takes up to 5 °C off in the heat |
| **Bedding** | From Kind | Whether straw is laid on its floor: coops, barns, stables, sties and hutches are bedded; pastures and ponds are not (or None, or Bedded) |
| **Self Clean Hours** | −1 (from its kind) | Game hours in which its ground clears itself of dung: a pasture or a pond in 72 (faster in rain); a floor never (0) |
| Show Fence | on | Simple posts and rails along the outline, a feeder and a trough, and dung and straw on the floor as it is. They have no colliders |
| Follow Ground, Ground Layers | on, Everything | The fence, the animals, the dung and straw and a laid-out carcass sit on the ground below (a ray from 4 m above). A roof or a loft more than 1 m above the pen's plane is passed over; only where nothing lies within that metre (a pen set into a slope) is the nearest surface taken. Leave the layer of a lower roof out of *Ground Layers*. Off: everything stands at the pen's height |
| Interactable, Priority, Extra Reach | on, 3, 0.8 m | As a garden's |

How shelter, bedding, dirt and water change how the animals live is in [chapter 04](04-Care-Welfare-and-Health.md).

* A pen belongs to the property it is under (or that lists it in **Pens**). Housing prices and lists pens as
  **Pasture** land, which is the same rate as plain land by default, so adding a pen does not change a house's rent.
* **Who may do what**:
  * The tenant, and those they let work the garden (the **Garden** right), may **tend** a property's pens (feed,
    water, stock the feeder, muck out) and **keep** animals in them (let new ones in, bring their own). They may also
    **care for others' animals** there: feed them by hand, groom them, look them over, treat them, send for the vet,
    clear their remains.
  * An animal's **owner** alone may name it, move it, slaughter it and butcher it.
  * Anyone may **look** over the fence.
  * A pen on no property is public: anyone may stock its feeder and trough, muck it out and keep their own animals
    there. Only an animal's owner may feed it by hand, care for it, treat it or clear its remains, and only a keeper of
    an animal there may scrub out its trough, or pour in water dirtier than the trough's once that would leave it murky
    (a stranger may not, however little at a time; cleaner water, and a dry trough, are anyone's to fill).
* **When the lease ends**, an owner who can no longer keep animals in the pen has them taken to the **boarding
  stable**. There they are fed a plain diet (decent, never fine), groomed, and miss their home. The owner brings them
  back to any pen they keep with **Bring here**. From any pen's window they can look one over, treat it, send for the
  vet and read its history. The same happens when the tenant stops letting a friend in: the friend's animals are
  boarded.

## The veterinarian and the keeper's goods

**Animals ▸ Create Veterinarian In City** makes one NPC with two options:

* **Veterinarian** (*Animal Veterinarian* component). Talking to the vet only tells you things: what the vet treats, the
  fee for each kind of animal, which illnesses grow past a keeper's medicine, the remedies that need no brewing, and a
  folk hint for each veterinary medicine. The visit itself is sent for from the pen (the Health tab).
  * Its settings: *Currency* (empty: the default), *Base Fee* 15, *Fee Per Size* 8, *Fee Of Value* 0.05, and the
    *Hints*.
  * The first enabled veterinarian in the loaded scenes is the city's.
  * **With no veterinarian in the world nobody can be sent for, and a keeper's medicine cures every illness.**
* **Keeper's goods**, a *Merchant* with **Animal Goods** (*Animal Goods (Merchant)*). As the scene wakes, it adds to the
  shop:
  * the care tools (soft brush, curry comb, hoof pick, pitchfork) and the tools collecting takes (sheep shears, a
    milking pail);
  * the supplies (straw, soap);
  * every feed;
  * a crate of each species' first breed;
  * medicine: a Standard bottle of every potion a medicine names, the veterinary ones too, and the plain remedies
    (charcoal, wormwood, vinegar, lavender);
  * with *Products* ticked (off by default), eggs, milk and wool bought in (of middling quality, no known origin).

  Each switch but *Products* is on by default; *Extra* lists more item ids. The goods are unlimited, or 20 of each with *Unlimited*
  off.

**Animals ▸ Add Animal Goods To Selected Merchant** gives any merchant the same goods. Both NPCs belong in the City
scene (the hub world's other scenes are made again every cycle).

## The pen's window

**Left: the animals.**

* Each animal here is shown with its name, kind and stage, and how it seems (*content*, *hungry*, *thirsty*, *unwell*,
  *very weak*, *dead*).
* Below them, *Yours elsewhere*: your animals that live in another pen (with **Bring here** where you may keep animals)
  and those at the boarding stable. Choose one at the boarding stable to see to its health and history from here.

**Middle: the animal, in five tabs.**

* **Overview**:
  * What is seen of it: its breed, sex, stage and age, whose it is.
  * Gauges, in coarse levels, never numbers: **Fed**, **Watered**, **Health**, **Spirits**, **Condition** (how well fed
    it looks over the last days), **Clean**, **Comfort**, **Calm**, **Hooves** (when it has them) and **Welfare** with
    its band (*Excellent*, *Good*, *Average*, *Poor*, *Critical*).
  * The **signs** a keeper notices: *its ribs show*, *it keeps going back to the trough*, *it coughs and its nose runs*,
    *it huddles against the cold*, *its coat is caked with dirt*, *it needs help*...
  * **Look closely** adds its coat, comfort and hooves in words, how it knows you, the illnesses you know it has, and
    when it was last fed and cared for.
  * **Name it** (the owner only).
  * **Slaughter** (its owner, a living animal: click twice within 4 seconds). For a dead animal: **Butcher** (its
    owner, when its species can be butchered) and **Clear the remains**.
* **Care**: first **What it gives**, for an animal that gives something (eggs, milk, wool:
  [chapter 05](05-Production.md#collecting)). Each product shows **Collect**, **Milk** or **Shear** with what waits and
  its tier (*Milk: 2 jugs of milk (Good)*), **From all here**, how the animal is making it (*making it*, *full: collect
  it*, *too poorly fed to give any*...) with a gauge of the next lot, and the tool it takes. Then one card per care
  action of its species (Brush, Wash, Pet, Clean hooves...).
  * Each card says whether it can be done now or why not, the tool it needs and whether you carry one, and the water it
    takes.
  * A **With soap** switch shows when you carry soap.
  * **To all here** does it to every animal in the pen that can take it.
  * **Look it over**: a careful look that names an illness once it shows enough.
* **Health**:
  * the illnesses known by name, with their stage (*being treated*, *recovering*, *needs the vet*);
  * the signs, and what the last look found;
  * each medicine you carry, with **Give** and, for a drench or a remedy in the feed, **Give to all here**. A medicine
    with no overdose asked for again within its interval is not given, and says when it may be (*Too soon for another
    dose: Daisy can have the vinegar wash again in 3 hours.*); **Give to all here** leaves out the animals that had it
    too lately;
  * **Send for the vet (fee)** when there is a veterinarian (**(free)** when the vet asks nothing).
* **History**: **Show its history**: its health history, its care history, what it made and what was collected of it,
  and how its last days went.
* **Family** ([chapter 06](06-Breeding-Genetics-and-Behaviour.md#the-window)): its bond with you and the perks it has
  brought, whether it is expecting, its parents, generation and young, its companions, what you know of its breeding in
  words, and **Breed it with**: your animals here it could be bred with, each with *Breed with …* or why not. A horse
  that comes when called has **Call it**.
* **Pen** ([chapter 07](07-Trading-Upgrades-and-Automation.md#the-window)): the pen's level and room, the next level and
  its price, its machines (a feed hopper, an automatic waterer, egg trays) and its upkeep, with **Improve**, a button per
  machine, **Pay upkeep** and **Pay a week ahead**. An owner's animal has **Crate it** on its Overview page (to sell it to
  a merchant or trade it to a player), and its worth in trade.

**Right: the pen.**

* How the feeder, the trough and the grass look; how clean the floor is, the bedding, the water (*clear*, *murky*,
  *foul*); its shelter and the weather now.
* **Fill the trough**, from the water container in your hand, else the fullest one you carry. Poured water mixes with
  what is there.
* **Scrub the trough** (when its water is fouled): tips the water out and scrubs it clean. Fill it again after.
* **Muck out**, with a pitchfork or a shovel; with the straw switch beside it on (it shows while you carry straw: *with
  straw mulch (3)*), fresh straw goes down in a bedded pen. Mucking out gives manure.
* **Collect all** (while something of yours waits: *Waiting: 5 eggs, 6 jugs of milk*): collects everything waiting that
  you have the tools for. The copies go into your bag, each with its quality and its origin.
* Each food you carry, with:
  * **Feed it**: feeds the chosen animal from your hand, as much as it will take. What it leaves goes into the feeder.
  * **Feeder**: stocks the feeder with ×1, ×5 or ×20 items.
* Each young animal you carry, with **Let it out**. It becomes yours, named with the name typed in the middle column if
  there is one.

Every action is asked of the rules (of the server, in a session) and answered, one at a time. A sound plays for care,
mucking out, medicine, the vet, a slaughter, a butchering and collecting (collect, milk, shear) when the Animal Database's *Sounds* give one (empty slots
are silent). The animals' bodies show how they are: a dirty coat is darker, a sick animal lies down more and coughs, a
lame one limps, a stressed one paces, a thin one is narrower, a sheep grows bulkier as its fleece grows and is slim once
shorn, a content one idles at ease. They groom themselves, keep with their companions, a young keeps to its mother, a
skittish one shies from strangers, and a bonded one greets you and follows you along the fence (chapter 06). They react to care (leaning
into a brush, shaking off water, lifting a hoof).

## Acquiring animals

Young animals come in **crates**, livestock items `animal:livestock/<species>/<breed>` (*Jersey Calf (crate)*). Any
merchant can sell them: a merchant with **Animal Goods** sells a crate of each species' first breed. Add any other to a
merchant's stock like any item; `AnimalItems.All()` lists them all.

Letting a crate out into a pen checks four things:

* that the species can live in that kind of pen;
* that there is room for one more adult;
* that you keep fewer animals than the rules allow (*Max Animals Per Player*, 60);
* that you may keep animals there.

The crate is used up and the animal is yours. A crate bought from a merchant carries no animal yet: the world gives it
its sex and genes (from its new id), and it starts at its species' *Baby* stage. It comes out fairly clean, at ease and
with sound hooves. Crates the game makes later (Phase 5: trading an animal) carry their animal's code.

## Feeding

Animals eat by themselves from the **feeder** whenever they are hungry. Each takes the food it likes best of what is
there, the best kept first; a hungry animal goes first.

* **Harmful food** (spoiled feed, raw potato for most) is eaten from a feeder only when nothing better is left and the
  animal is quite hungry. Even then it eats only until that hunger is eased.
* **Toxic food** (poisonous herbs, nightshade) is eaten only by a starving animal, and only a mouthful.
* **From the hand**, an animal trusts more. It takes harmful food below about three quarters full, and toxic food below
  about half, again only as far as that hunger. It refuses what is not food for it (meat for a cow), and anything when
  it is full. A big animal takes more from a hand than a small one.
* After a meal from the hand it waits a little before it takes another. A meal from its owner's hand counts as its
  owner's care.
* A sick animal eats less, and some illnesses (worms, an upset gut) make its food feed it less.

Fresh food wilts in a feeder (grass in a day and a half; hay keeps). The quality of each copy counts: a fine carrot from
a well kept garden feeds better than a poor one.

## What happens to a neglected animal

* **Empty stomach.** Health falls steadily; with no water left in it, it falls faster.
* **Poor nutrition.** A full animal eating cheap feed for days slowly loses health and barely grows.
* **Poison.** It trembles and drools; its health falls while the poison clears.
* **A dirty pen, foul water, a filthy coat, neglected hooves, crowding, the cold.** Its comfort and welfare fall, and it
  can fall ill: an infection, worms and lice, hoof rot, an injury, chronic stress. Each illness has its causes, its
  stages and its symptoms, and most need treating ([chapter 04](04-Care-Welfare-and-Health.md)).
* **Death.** An animal dies when its health reaches nothing. Its owner is told, and its record stays (*Dead - starved*).
  Its owner can then butcher it through Harvesting, or clear the remains.

**While its owner is logged out**, its hunger and thirst grow at about a third of the pace, and none of this takes its
health below a quarter. An evening offline does not kill a herd, but a neglected herd still suffers. Old age is not
held back.

The owner is told when an animal falls ill (a symptom, not its name), gets worse, gets better, and when it is *in a bad
way* (its welfare Critical or its health very low, at most once a game day; an owner who was away is told when they
come back, a day after the last warning at the earliest).

## Butchering

An animal's owner may **slaughter** it, and **butcher** a dead one. Its remains are laid out as a Harvesting carcass
just outside the pen's fence, and only its owner may work it. How it was raised (its lifelong welfare, nutrition, health
and coat), how it died and what ailed it decide how much it gives and how good it is: a well-raised animal's pieces are
better, a neglected, starved or sick one's poorer. Carcasses are not saved: harvest one before a restart. See [chapter
04](04-Care-Welfare-and-Health.md#slaughter-and-butchering) and [Harvesting](../Harvesting/README.md).

## Menus and validation

**Tools ▸ SimpleMovements ▸ Animals**:

| Menu | What it does |
|---|---|
| Create Pasture / Coop / Sty On Selected Property | A pen on a free spot of the selected property's land |
| Create Standalone Pen | A public pen (on no property) at the Scene view's pivot |
| Create Veterinarian In City | The city's veterinarian, who also sells the keeper's goods (asks first when there is one already) |
| Add Animal Goods To Selected Merchant | The keeper's goods for the selected merchant |
| Create Animal Database | The project's rules and content (also under *Project Setup*) |
| Create Species Assets / Create Food Assets | Editable copies of the built-in species and foods |
| Update Species Assets To Phase 5 | Writes what species assets made before Phase 5 lack from the built-in species of their id (undoable): the care, susceptibility and anatomy of one made before Phase 2, the products (and the senior stage's *Production Enabled*) of one made before Phase 3, the breeding, behaviour and perks of one made before Phase 4, and the egg-tray products and breed yields of one made before Phase 5. An asset with no built-in species of its id must be given them by hand |
| Create Animal Carcass Prefab (Network) | The network prefab a carcass is laid out with in a session (the same as *Network ▸ Create Animal Carcass Prefab*) |
| Give Me A Keeper's Starter Kit (Play Mode) | A crate of each species and five of every feed |
| Give Me A Care Kit (Play Mode) | One of each care tool (the shears and the milking pail too), straw, soap, and two bottles each of seven medicines and two of each plain remedy |
| Validate Animals | Every check below, in the Console |
| Help | This documentation |

**Validation** (*Validate Animals*, or the Project Validator) checks, besides Phase 1's pens and database:

* **The content**:
  * each illness (stages from 0 and rising, a symptom for every visible stage);
  * each medicine (it names known illnesses, and Alchemy outcomes that exist);
  * each tool, and each species' care (cooldowns, unique ids, the tools and supplies it names exist);
  * susceptibilities that name known illnesses;
  * each species' anatomy in the Harvest Database;
  * that some tool can muck out and some supply is bedding;
  * the welfare bands in order;
  * each species' products (ids, cycles, capacity, a productive stage, the tools collecting them needs, an item for
    each, one id given alike by every species) and the production rules ([chapter 05](05-Production.md#authoring-products)).
* **Species assets made before Phase 5**: a warning, with an **Update** fix. Until updated, they take what they lack
  (care, susceptibility and anatomy before Phase 2; products before Phase 3; breeding, behaviour and perks before Phase 4;
  egg-tray products and breed yields before Phase 5) from the built-in species of their id.
* **Pen levels**: a level that does less than the one below it ([chapter 07](07-Trading-Upgrades-and-Automation.md#the-rules)).
* **Breeding**: each species' breeding (gestation, litters, a cost item that exists), a stage that breeds, its perks, and
  the breeding and social rules ([chapter 06](06-Breeding-Genetics-and-Behaviour.md#authoring)).
* **Where animals are kept**:
  * a veterinarian in the open scenes (a warning with a **Create Veterinarian** fix: without one, nobody can be sent
    for, and a keeper's medicine cures everything);
  * a merchant selling care tools (a warning with an **Add Animal Goods** fix: without one, brushing, hoof care and
    mucking out cannot be done);
  * medicine and supplies on sale;
  * the vet and the merchant in the City scene. While the City scene is closed these are only notes;
  * each Alchemy station in the open scenes whose own *Database* field holds a database without the veterinary
    medicines (a warning: they cannot be brewed there; leave the field empty, or add `AnimalAlchemy.VetOutcomes` to
    that database).
* **With Fish-Net**, the animal carcass prefab and its parts (a warning with a fix).

The **Animal Pen** inspector, in Play mode, shows every animal's numbers as the rules see them (coat, comfort, stress
and what causes it, welfare, hooves, illnesses with their stage, cause and treatment, and the build-up of the next
ones), the pen's floor, bedding, water dirt and worms, and whether there is a veterinarian. It has debug buttons where
the rules run: **Dirty pen**, **Heal all**, and for each animal **Infect…** (an illness at the stage chosen) and **Kill
(illness)**.
