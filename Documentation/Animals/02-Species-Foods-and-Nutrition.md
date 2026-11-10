# 02 — Species, Foods and Nutrition

[← Animals](README.md)

Everything about an animal is data. The rules never name a species, so a new species, breed, food, illness, medicine or
care tool is a new asset or entry (or a new entry of the built-in content).

## The Animal Database

Create it with **Tools ▸ SimpleMovements ▸ Animals ▸ Create Animal Database**. It lives at
`Resources/SimpleMovements/Settings/AnimalDatabase`. It holds:

* **Rules**: every number below and in [chapter 04](04-Care-Welfare-and-Health.md#the-default-numbers).
* **Species**: *Animal Species* assets. One with the id of a built-in species replaces it.
  **Create Species Assets** makes editable copies of the eight built-in species in `Animals/Species`.
* **Foods**: *Animal Food* assets, made the same way with **Create Food Assets**.
* **Illnesses**, **Medicines** and **Care tools and supplies**: entries edited in the database itself. An entry with the
  id of a built-in one replaces it (in its place); *Include Built In ...* (on) adds the built-in ones for the ids no
  entry gives. An illness an animal still has but the content no longer holds (its entry removed, or *Include Built In
  Conditions* turned off) stays on its record and does nothing: it does not keep it from healing, and the vet finds
  *Nothing wrong with it.* It acts again if its entry comes back.
* **Sounds**: an optional clip for each care family, mucking out, medicine, the vet, a slaughter and a butchering,
  collecting, milking and shearing (milking and shearing use the collect sound when empty), and the animals' own calls
  (content, protest, sick). An empty slot is silent.
* **Items**: item assets bound to animal item ids (`animal:feed/hay`, `animal:livestock/cow/jersey`,
  `animal:tool/soft_brush`, `animal:product/egg`). Without a binding, each item is made at runtime from its id, the same
  on every machine.

**Species assets made before Phase 2** (their *Data Version* is below 2) have no care, **those made before Phase 3**
(below 3) no products, **those made before Phase 4** (below 4) no breeding, behaviour or perks of their own, and **those
made before Phase 5** (below 5) no egg-tray products or breed yields. Until they are updated, the database fills in what
they lack from the built-in species of their id (care, susceptibility and anatomy below 2; products, and the senior
stage's *Production Enabled*, below 3; breeding, behaviour and perks below 4; which products roll into egg trays and the
breeds' Product Yield and Specialty below 5), and warns. **Animals ▸ Update Species Assets To Phase 5** writes those into
the assets (undoable) and sets their Data Version to 5. An asset whose content you made yourself: set its Data Version to
5 instead. An asset of a species with no built-in twin must be given them by hand. A species asset made now (*Assets ▸
Create*, or *Reset* in its inspector) starts at Data Version 5: its content is its own.

## The rules (Animal Rules)

| Group | Setting | Default | What it does |
|---|---|---|---|
| Time | Growth Speed | 1 | Multiplies growth (hunger and thirst are not changed) |
| | Tick Hours | 1 | Game hours between two updates of a pen |
| | Step Hours | 1 | Longest step of the simulation (a long gap is lived in steps) |
| | Steps Per Frame / Millis Per Frame | 400 / 2 | The budget of the simulation each frame |
| Owners away | Away Needs Rate | 0.35 | Hunger and thirst grow at this share while the owner is logged out (so do coats getting dirty, hooves wearing and illness building) |
| | Away Health Floor | 25 | Hardship never takes health below this while the owner is away (old age still does, so an animal that dies then dies of old age). Illness counts as hardship |
| Nutrition | Nutrition Window Hours | 48 | Over how long nutrition is judged |
| | Excess Penalty | 0.35 | How much too much of a nutrient hurts |
| | Malnourished Below / Well Nourished Above | 35 / 75 | Where harm begins, and where healing and growth are at full pace |
| | Poor Growth | 0.15 | Growth pace of a badly nourished animal |
| | Favorite Absorption / Poor Absorption | 1.1 / 0.55 | How well favourite and poor foods are absorbed |
| Eating | Harmful Below / Toxic Below | 35 / 12 | Satiety below which an animal eats harmful or toxic food from a feeder |
| | Full At | 96 | It eats until this (a sick animal less) |
| | Harmful Upset / Toxic Dose | 18 / 6 | Upset and toxin of a portion that is harmful or toxic to the species |
| | Harmful / Toxic From Hand Below | 75 / 45 | The same thresholds for food from a hand |
| | Upset Recovery | 0.06 | Share of an upset that fades per game hour |
| Mood | Favorite Happiness, Mood Hours, Hand Feed Affinity | 4, 12, 0.8 | Hand Feed Affinity is the bond a meal from its owner's hand gives, within the bond's daily cap (*Affinity Per Day*, chapter 04) |
| Feeding by hand | Work Range, Hand Feed Cooldown Hours, Max Hand Portions | 2.6 m, 0.5, 6 | Max Hand Portions is for an average animal; it scales with size |
| Limits | Max Animals Per Player, Max Name Length | 60, 24 | |
| History | Keep Events, Keep Days, Keep Feedings, Remember Requests Hours | 32, 14, 16, 24 | |
| Boarding | Board When Unreachable, Boarding Diet, Boarding Happiness | on, 0.8, 35 | |
| Bodies | Show Radius / Hide Radius | 70 / 90 m | |
| Saving | Save Animals, Autosave Seconds | on, 30 | |
| Care, Habitat, Comfort, Welfare, Health, Veterinarian, Remains | (Phase 2) | | [Chapter 04](04-Care-Welfare-and-Health.md#the-rules-animal-rules) |
| Production | (Phase 3) | | [Chapter 05](05-Production.md#the-rules-animal-rules--production) |
| Breeding, Social | (Phase 4) | | [Chapter 06](06-Breeding-Genetics-and-Behaviour.md#the-rules) |
| Trade, Upgrades | (Phase 5) | | [Chapter 07](07-Trading-Upgrades-and-Automation.md#the-rules) |

## A species

| Field | What it is |
|---|---|
| Id, Name, Plural, Young Name, Description | The id names it in saves and items (no spaces, `/`, `:` or `\|`). *Young Name* names its crate ("Calf") |
| Sexed, Female Share | Whether its animals are female or male (Phase 4 breeding needs both) |
| **Stages** | Its stages in order (Newborn, Baby, Juvenile, Adolescent, Adult, Senior: use those it has), each with: *Duration Hours* (the growth a well kept animal needs to leave it; 0 on the last stage it grows into), *Production Enabled* (it gives its products in this stage, chapter 05) and *Breeding Enabled* (it may be bred in this stage, chapter 06), *Appetite* and *Size* (against an adult), *Need Scale* per nutrient (a chick needs more protein), and its own *Diet* (a calf drinks milk) |
| Senior Age Hours, Lifespan Hours | When age makes it Senior, and past when old age wears its health away (0 = never) |
| **Needs** | See below |
| **Diet** | See below |
| **Habitat** | The *kinds* it can live in, and the m² of pen an adult needs |
| Look | Its shape (quadruped, bird, small), length, height, coat, accent, the **gait** it moves with (the mobs' gaits: Walk, Hop...) and its walking speed. These are used for a placeholder body when the asset has no *Body Prefab* (a model; not a Mob) |
| Breeds | Each with its growth, appetite, size, hardiness, temperament, production quality, rarity, coats and the price of a young one (the vet's fee takes 5 % of it); Phase 5: its *Product Yield* and *Specialty*, what it is bred for ([chapter 07](07-Trading-Upgrades-and-Automation.md#breed-specialities)) |
| Harvest Anatomy | The Harvesting anatomy its remains are butchered as. The built-in species have `chicken`, `duck`, `rabbit`, `sheep`, `goat`, `pig`, `cow` and `horse`. Empty: its remains can only be cleared |
| **Care** | How it keeps clean and comfortable, and the care actions it takes (see [Care](#care)) |
| **Susceptibility** | How prone it is to each illness (see [Susceptibility](#susceptibility)) |
| **Products** | What it gives while it lives: eggs, milk, wool... each with its cycle, lot, capacity, tools, freshness, value and item ([chapter 05](05-Production.md#authoring-products)) |
| **Breeding**, **Behaviour**, **Perks** | How it breeds, how it behaves, what a bond with its owner unlocks ([chapter 06](06-Breeding-Genetics-and-Behaviour.md#authoring)) |
| Data Version | 5 for a species made for Phase 5 (see above) |

### Needs

* **Portion Scale**: how much an adult eats compared to an average animal (a cow 4, a hen 0.2). Portions of food,
  nutrient needs, toxin doses and medicine doses all count by it, so a portion of feed fills a hen far more than a cow,
  and a cow needs more bottles of medicine than a hen.
* **Hunger Per Hour / Thirst Per Hour**: satiety and hydration (0–100) an adult loses per game hour. *Heat Above* and
  *Heat Thirst* make it thirstier in the heat.
* **Eat Below / Drink Below**: when it goes to the feeder or the trough. *Litres Per Point* is how much it drinks.
* **Need Per Hour**: the nutrients an adult needs per game hour (energy, protein, fibre, minerals, vitamins), and their
  **Importance** in its nutritional condition.
* **Harm and recovery**: *Starvation*, *Dehydration* and *Malnutrition Damage*; *Recovery Per Hour*; *Toxin
  Tolerance*, *Toxin Clearance* and *Toxin Damage*. Damage is divided by the breed's hardiness and the animal's
  resistance. Hardiness also slows the build-up of every illness.

### Care

| Field | Default (a new species) | What it is |
|---|---|---|
| Hygiene Loss Per Hour | 0.4 | Hygiene an adult loses per game hour while it is cleaner than its resting level (faster on a dirty floor) |
| Self Groom Per Hour | 0.5 | Hygiene it regains on its own while below its resting level (keep it at or above the loss) |
| Self Groom Cap | 55 | Its resting level: the hygiene it keeps itself at in a clean pen without care |
| Messiness | 1 | How much it soils its pen (a pig 1.3, a rabbit 0.7) |
| Mud Sensitivity | 1 | How much mud on an open pen in the rain dirties it |
| Dust Bath | 0 | Fresh, dry bedding lets it clean itself: up to +25 on its resting level (a hen 1) |
| Bathes | 0 | A pond lets it clean itself: +25 × this (a duck 1) |
| Hoof Wear Per Hour | 0 | Hoof condition lost per game hour (0: no hooves; it never gets hoof rot) |
| Comfort Min / Max | 0 / 25 °C | Its comfortable air temperature |
| Rain Dislike | 0.4 | How much it minds rain without shelter |
| Bedding Need | 0.5 | How much it misses bedding in a bedded pen |
| Crowded From | 0.8 | Share of its pen's room in use from which it feels crowded |
| Companions Wanted | 0 | Others of its kind it wants in its pen (0: content alone) |
| **Actions** | | The care it takes (below) |

**A care action**:

| Field | What it is |
|---|---|
| Id | Unique in its species: `brush`, `wash`, `pet`, `clean_hooves`, or a species' own (a request names it). Empty: from its kind |
| Kind | Its family: Brush, Wash, Pet, Clean Hooves or Custom. It gives the default label, verb, message, icon and sound (a Custom action needs an id) |
| Label, Past Verb, Message | Its button, "Daisy was **brushed** 2 hours ago", and what is said when it has no reaction (`{name}` is the animal) |
| Tools | Tool tags, any one of them (`brush`, `soft_brush`, `curry_comb`, `hoof_pick`...). Empty: bare hands, no tool needed or worn |
| Water Litres | For an average adult (× its size). 0: none |
| Supply, Supply Boost | A supply tag (`soap`) the player may choose to use, and how much better it then cleans (×1.5) |
| Cooldown Hours | Before it can be done again |
| Min Stage | The youngest stage it can be done to |
| Hygiene, Comfort, Happiness, Affinity, Stress, Hooves | What it does (affinity only with its owner; hooves only for hooved species) |
| Parasite Relief | Share of the build-up of worms and lice it removes |
| Handling Stress | Extra stress for a skittish or unfamiliar animal (× (1 − temperament) × (1 − affinity/100)) |
| Overuse Hours, Overuse After | The hours over which repeats count, and how many before it does harm |
| Overuse Stress, Overuse Comfort, Overuse Hygiene | The harm of each repeat past that |
| Chill Below, Chill Comfort | °C below which it chills (a wash in the cold), and the comfort lost per °C (at most 10) |
| Heat Above, Heat Comfort | °C above which it is a pleasure (a pig's wallow), and the comfort gained per °C |
| Inappropriate | It is allowed but bad for this species (a bathed rabbit panics): the player is told |
| Reaction | What is seen when it is done (*{name} leans into the comb and sighs.*) |

The built-in species' care (what each takes, its numbers, its reactions) is in
[chapter 04](04-Care-Welfare-and-Health.md#each-species-care).

### Susceptibility

A list of illness ids and factors: ×1.4 builds up 40 % faster, 0 never takes hold. Unlisted illnesses are ×1. An illness
marked *Hooved Only* never takes an animal without hooves.

| Species | Prone to |
|---|---|
| Chicken, duck | infection ×1.2 |
| Rabbit | digestive ×1.4, chronic stress ×1.3 |
| Sheep | worms and lice ×1.4 |
| Goat | worms and lice ×0.8 (less) |
| Pig | infection ×1.1, worms and lice ×0.9 |
| Cow | hoof rot ×1.1 |
| Horse | digestive ×1.2 (colic), injury ×1.1 |

### Diets

A diet has seven lists: **favorite, preferred, normal (acceptable), neutral, poor, harmful, toxic**. Each list holds
food ids (`hay`) or tags (`#grain`), and *Otherwise* says what every other food is (*Inedible* by default).

* A food named by id takes that category.
* Otherwise it takes the worst category one of its tags falls in. For example, the cow's diet puts `#grain` in *normal*
  but `low_quality_feed` in *poor*: the id wins.

| Category | From a feeder | Absorbed | Besides |
|---|---|---|---|
| Favorite | first | ×1.1 | happiness |
| Preferred | | ×1 | |
| Normal | | ×0.92 | |
| Neutral | | ×0.7 | |
| Poor | | ×0.55 | |
| Harmful | only when hungry and nothing better is there, and only until that hunger is eased | ×0.44 | upset stomach (and, when it lasts, a digestive upset) |
| Toxic | only when starving, only a mouthful | ×0.33 | toxin dose (plus the food's own; past its tolerance, food poisoning) |
| Inedible | never | — | refused |

## A food

| Field | What it is |
|---|---|
| Id, Name, Description, Tags | Diets name foods by id or tag (`forage`, `grain`, `legume`, `vegetable`, `fruit`, `meat`, `poisonous`, `spoiled`...) |
| Items | The item ids that are this food: `animal:feed/hay`, the farm's `agri:produce/carrot`, or a prefix ending in `*` (`harvest:*`: any harvested **food** piece) |
| Portions Per Item | How many portions one copy of the item holds |
| Satiety, Hydration | What one portion gives an average adult |
| Nutrients | Energy, protein, fibre, minerals and vitamins per portion |
| Quality Matters | The copy's quality (from its code: feed, produce, harvested meat) scales its nutrients (×0.5 to ×1) |
| Toxin, Irritation | A dose and a digestive upset per portion |
| Fresh Hours | How long it stays fresh in a feeder. After that its quality halves every as many hours |

Built-in foods:

* feed made for animals: fresh grass, hay, alfalfa, clover, grain mix, chicken feed, oatmeal, cheap feed, spoiled feed,
  poisonous herbs, kitchen scraps, dried mealworms, milk replacer;
* the farm's produce: wheat, carrot, cabbage, potato, beans, apple, strawberries, comfrey, nightshade, mandrake;
* meat (any harvested food piece).

## An illness, a medicine, a tool

These live in the database's lists (the built-in ones: [chapter 04](04-Care-Welfare-and-Health.md#illnesses)).

**An illness** (*Illnesses*):

| Field | What it is |
|---|---|
| Id, Name, Description, Kind | Kind: Malnutrition, Dehydration, Infection, Digestive, Parasites, Stress, Injury, Poisoning (what treats it, what it does to the remains) |
| Causes | Each cause with a weight: the build-up a game hour is the sum of weight × cause |
| Onset At, Exposure Hours | The build-up at which it takes hold (±15 % by the animal's genes), and the hours over which it builds and fades |
| Start Severity | Where it starts (0-100) |
| Worsen Per Hour, Course Without Cause | Severity a game hour while its causes go on (twice as strong as it takes to start it), and once they are gone (negative: it mends by itself) |
| Needs Treatment Above | Above this severity it never mends by itself (101: always can) |
| Needs Vet Above | Above this, while there is a veterinarian, a keeper's medicine only holds it (101: never) |
| Recovery Hours, Immunity Hours | Its recovering stage, then the time it cannot take hold again |
| Contagion | Build-up a game hour it gives each pen-mate at full severity (0: not catching) |
| Hooved Only | Only animals with hooves get it |
| Taints Carcass | At death its taint runs through the whole carcass (a fever, worms): an Infection or Parasites illness in a stage that shows makes the meat and organs unfit to eat. Off for a complaint of one part of the body (hoof rot: the meat stays fit to eat). On by default |
| Diagnose From | The stage (an index into its stages) from which a careful look names it |
| Death Cause | What its death is recorded as |
| **Stages** | In order of severity, the first from 0. Each: *Name*, *From*, *Visible* (a hidden stage is found only by the vet), *Symptoms* (a visible stage must have one: the first is what the owner is told), *Health Per Hour*, *Comfort*, *Stress*, *Happiness* (added), *Hygiene Per Hour*, and ×*Appetite*, ×*Growth*, ×*Absorption*, ×*Production* (of what it gives, chapter 05), *Mobility*. Every stage lists every effect: nothing is taken from the stage before |
| Recovering | How it is while it mends |

**A medicine** (*Medicines*):

| Field | What it is |
|---|---|
| Id, Name, Description | |
| Items | The item keys that are it: `potion:antidote` (any bottle of that Alchemy outcome), `ingredient:charcoal`, `craftstock:Fuel:charcoal`; a key ending in `*` is a prefix |
| Quality Matters | The copy's quality scales it ×0.4 (worthless) to ×2 (perfect) |
| Route | Drench (by mouth), Topical (on the skin or hooves: weaker on a filthy coat, never a whole pen at once) or Feed (in the feed) |
| Doses Per Item | Doses for an average animal one item holds |
| Effects | Per illness id (`*`: every illness): severity off at once, per game hour while it works, and for how many hours. Doses working together do not add up: each game hour gets the strongest working then, each dose to its own end ([chapter 04](04-Care-Welfare-and-Health.md#medicine)). An effect with nothing per game hour works only at once: it leaves no treatment working |
| Toxin Cleared, Upset Eased, Health Now, Comfort, Stress, Satiety, Hydration | What else it does (× its strength). A dose too soon does only those that harm: a Health Now, Comfort, Satiety, Hydration or Upset Eased below 0, a Stress above 0 |
| Min Interval Hours, Overdose Share | The same medicine again sooner than this after its last dose that did its good is too soon. A dose too soon does not count: the next waits only for the last dose that did its good, and doses of other medicines change nothing. With an Overdose Share of 0 a dose too soon is not given: nothing is used up, and the keeper is told when it may be, the wait rounded up so that a keeper who comes back then may give it (*Too soon for another dose: the cow can have the vinegar wash again in 3 hours.*). With an Overdose Share above 0 it is given, as an overdose: it does no more good at once (no severity off at once, nothing besides that helps), only its harm, the stress of being dosed and toxins of this share × its strength × the animal's tolerance (the same harm to a hen as to a cow); its hourly work goes on as any dose's. *Give to all here* leaves out the animals that had it too lately, either way. The interval also spaces the bond its owner's doses give (+1, at most once per interval) |
| Handling Stress | Stress of being dosed |

**A tool or supply** (*Care tools and supplies*):

| Field | What it is |
|---|---|
| Id, Name, Description | |
| Tags | What it is used as: `brush`, `soft_brush`, `curry_comb`, `hoof_pick`, `fork`, `shovel`, `soap`, `bedding` |
| Items | The item keys that are it (`animal:tool/soft_brush`, `agri:tool/shovel`, `craftstock:Supply:soap`, `agri:amend/straw_mulch`) |
| Consumed | A supply, used up one a use, instead of a tool, worn |
| Quality | How well it does the work |
| Uses, Value, Weight | For a tool of animal husbandry's own (an `animal:tool/<id>` item): its durability, price and weight |

## How it works

* **Satiety** is how full an animal is. A portion fills it by `food.satiety / appetite`, where appetite is the stage ×
  the breed × its size × the species' portion scale.
* **Nutrition** is judged over the window. Each hour, what it takes in and what it needs are both added to sums that
  fade with the window, and each nutrient scores by intake ÷ need:
  * short of the need scores less and less (half the need scores under half);
  * too much loses a little.

  The condition (0–100) is the scores weighted by importance. That is why cheap feed keeps a cow full but leaves it
  badly nourished, and one favourite meal cannot undo a week. An illness that harms digestion (worms, an upset gut)
  makes everything it eats count for less.
* **Growth**: the animal gains *development hours* at a pace set by its nutrition (from *Poor Growth* to full at *Well
  Nourished Above*), halved below 40 health, and stopped while it starves or has no water. That pace is multiplied by
  the breed's growth and its genes, its welfare band and its illnesses. Its stage follows its development; its age
  follows the clock.
* **Health** falls with starvation, thirst, malnutrition and toxins above its tolerance, with its illnesses, and with
  old age past its lifespan. It recovers when the animal is fed, watered, nourished, free of poison and upsets, and not
  badly ill, faster in a good welfare band.
* **Genes** are made at birth (or when a crate is let out) from the breed and a seed, so the same seed gives the same
  animal. There are nine traits (size, growth, feed efficiency, resistance, fertility, temperament, production quality
  and rate, longevity) plus a coat, a pattern and sometimes a rare trait. They are never shown as numbers. Temperament
  makes handling and crowding harder or easier, and production quality moves how well its remains are judged a little;
  Phase 4 passes them to offspring.
