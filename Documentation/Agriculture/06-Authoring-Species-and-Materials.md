# 06 — Authoring Species and Materials

[← Agriculture](README.md)

## The Agriculture Database

`Resources/SimpleMovements/Settings/AgricultureDatabase` (**Agriculture ▸ Create Agriculture Database**). Its inspector
shows what it holds, its problems, and buttons to make **Species Assets**, **Soil Material Assets** and **Item Assets**.

* **Settings** — the rules: time (*Growth Speed*, *Tick Hours*, *Max Step Hours*, *Plants Per Frame*, *Environment
  Step Hours*, *Day Length Minutes*), sleeping gardens (*Sleep When Alone*, *Wake Radius*, *Sleep Radius*, *Sleep
  Delay Seconds*, *Unload When Asleep*, *Maintenance Lag Hours*, *Catch Up Millis Per Frame*:
  [chapter 07](07-Scripting-Editor-Multiplayer.md#sleeping-gardens)), catching up (*Fast Forward Bare Gardens*, *Quiet
  Steps*, *Quiet Hours*, *Cache Cell Values*: [chapter 07](07-Scripting-Editor-Multiplayer.md#how-it-stays-light)), the calendar (*Days Per Season* 12, *Lunar Cycle Days* 8, *Start Season*, *Start
  Hour*), the city's *Climate* (per season, [chapter 03](03-Soil-Water-and-Weather.md)), *Use World Weather* and
  *Seasons On World Weather*, the soil (*Evaporation*, *Leaching*, *Mineralization* multipliers), plants (*Water Use*,
  *Nutrient Use*, *Stress Damage*, *Dead Plant Days*), *Pest Pressure*, *Disease Pressure*, *Spread*, *Pollinators*,
  *Mutation Chance*, *Rare Trait Chance*, the skills, *Work Range*, *Max Hold Seconds*, saving (*Save Fields*,
  *Autosave Minutes*) and the *Water Quality* of each kind of water.
* **Species** — Plant Species assets; one with the id of a built-in species replaces it. *Include Built In Species*
  keeps the built-in ones for the ids no asset gives.
* **Hybrids** — crosses that sometimes give seeds of another species (*Include Built In Hybrids*).
* **Amendments** — soil material assets (fertilizers, amendments, treatments); same rule by id.
* **Items** — item assets bound to agricultural ids (`agri:seed/<species>`, `agri:produce/<species>`,
  `agri:amend/<material>`, `agri:tool/<tool>`, `agri:instr/<instrument>`, `agri:water/<container>`). Ids without one get
  an item made at runtime from the id — the same item on every machine, so saves, merchants and the network resolve it.

## Plant Species assets

**Agriculture ▸ Create Species Assets** makes an editable asset of every built-in species (in
`Resources/SimpleMovements/Agriculture/Species`, listed in the database); copy one to make a new plant. The numbers are
the plant's **true nature** — no window ever shows them to the player.

| Group | Fields |
|---|---|
| Identity | *Id* (no spaces, `/` or `:`), *Name*, *Category* (Common, Fruit-bearing, Medicinal, Alchemical, Magical, Ornamental, Rare), *Handbook* (what the Botanical Handbook says: may be vague, partial or empty) |
| Life | *Life Cycle* (single harvest, annual, perennial), days per stage (*Germination … Senescence*), perennials' *Years To Fruit*, *Years To Full Size*, *Lifespan Years* |
| Soil | *Soil Affinity* per type (0–1), pH (*min, low, high, max*: survives / thrives / survives), *Nutrient Demand* and per-nutrient *Needs*, *Salinity Tolerance*, *Compaction Tolerance*, *Min Tilth*, *Magic Need*, *Nitrogen Fixer* |
| Water | *Water Low/High* (the water in reach it likes), *Drought Tolerance*, *Waterlog Tolerance*, *Water Use*, *Water Quality Sensitivity* |
| Temperature | *min, low, high, max* °C, *Germination Min/Max* (soil), *Frost Sensitivity*, *Shock Sensitivity* |
| Air and light | *Humidity Low/High*, *Light Low/High* (1 full sun, 0.5 partial shade, 0.2 shade), *Light Excess* (scorching), *Photoperiod* (neutral, long-day, short-day) and *Critical Day Length* |
| Seasons | *Sowing Seasons*, *Flowering Seasons*, *Dormancy Seasons* (perennials), *Chill Hours* |
| Moon and time | *Lunar Strength*, *Moon Germination* and *Moon Potency* per phase, *Planting Time* (any, day, night) and its strength |
| Planting | *Depth Cm* and *Depth Tolerance Cm*, *Surface Sown*, *Spacing*, *Row Spacing*, *Root Radius*, *Needs Support* |
| Neighbours | *Companions* (target: a species id, `category:Medicinal` or `*`; effect: growth, repel pests, attract pollinators, inhibit; strength; radius), *Cross Group* |
| Produce | *Part* (fruit, leaf, root, flower, grain), *Name*, *Uses*, *Count Min/Max* per flush, *Flush Interval Days*, *Max Flushes*, *Stage Days* of a piece (9: bud … overripe, rotten), *Pollination*, *Whole Plant*, *Regrows*, *Seeds Per Piece*, *Value*, *Weight*, *Nutrition*, *Potency*, colours young/ripe/overripe/rotten, *Piece Size*, *Alchemy Ingredient* (the alchemy ingredient whose substances a piece gives) |
| Pests and diseases | *Pest Susceptibility* (8) and *Disease Susceptibility* (6), 0–2 |
| Pruning | *Pruning* need and *Prune Seasons* |
| Genetics | *Genetic Variation* (how much its seeds differ) |
| Look | *Shape* (herb, bush, vine, tree, grain, root crop, rosette...), *Height*, *Width*, leaf, stem, flower colours, *Glow* |
| Seeds | *Seed Value*, *Seed Stack* |

The inspector checks the species as you type (ranges in the wrong order, missing values, a spacing smaller than the
roots' reach, dormancy on a non-perennial, hand-only pollination...) and offers **Add** when the database does not list
it yet.

## The trial plot

Every Plant Species inspector has a **Trial plot**: it sows a few seeds in worked soil and grows them hour by hour in
the city's climate — **the game's own simulation** — then tells you, in numbers, what happened. For tuning only.

* **Soil** (by default the one it likes best that also feeds it), **Its best pH** (or the type's own), **Sown in**
  (season and day: by default the first day of its sowing seasons warm enough for it), **Days**, **Plants**, **Water
  when dry**; *Feed* and *Protect* (in code: `AgriTrial.Options`) keep nutrients topped up and pests away.
* **Grow** reports: how many came up and lived, the first day each stage was reached, the first ripe produce, how many
  pieces ripened, the end quality, health and size, the worst pests and disease, the water given, the average growth
  and **what held it back most** ("nutrients (potassium) 71 % of its growing hours, met at 65 %; temperature 27 %…").

Use it to check a new species grows in the seasons you meant, ripens in a sensible number of days, and needs what you
meant it to need. The Edit Mode tests run it for every built-in species.

## The built-in species

| Species | Category | Life cycle | Character |
|---|---|---|---|
| Wheat | Common | Annual | Grain, cut when dry; wind-pollinated |
| Carrot | Common | Single harvest | A root; likes light sandy soil |
| Cabbage | Common | Single harvest | A head of leaves; a heavy feeder; hardy |
| Climbing Bean | Common | Annual | Fixes nitrogen; needs support; pods in flushes |
| Potato | Common | Single harvest | A tuber; frost-tender; likes slightly acid soil |
| Tomato | Fruit-bearing | Annual | Warmth-loving; many fruit ripening one by one; needs support; hungry |
| Strawberry | Fruit-bearing | Perennial | Surface-sown; berries in flushes; rests in winter |
| Apple Tree | Fruit-bearing | Perennial | Years to fruit; needs winter cold; pruning |
| Grapevine | Fruit-bearing | Perennial | Stony or volcanic ground, sun; years to fruit; winter cold; support; pruning |
| Chamomile | Medicinal | Annual | Flowers picked as they open |
| Comfrey | Medicinal | Perennial | Leaves cut again and again; a heavy feeder |
| Lavender | Medicinal | Perennial | Poor, alkaline, dry soil; full sun; repels pests; pruning |
| Marigold | Ornamental | Annual | Repels pests around it; frost-tender |
| Mandrake | Alchemical | Single harvest | Shade; sown at night, under a new moon; potency by the moon |
| Nightshade | Alchemical | Perennial | Shade; alkaline soil (acid soil harms it); sown at night |
| Moonpetal | Magical | Annual | Magical soil, shade; strongly moon-bound (sow under a full moon, at night) |
| Emberroot | Magical | Single harvest | Heat, volcanic ash, phosphorus |
| Frostbloom | Rare | Perennial | Flowers in winter; acid soil; rests in summer |
| Twilight Bloom | Rare | Perennial | Shade, short days, magic; a hybrid of moonpetal and frostbloom |

## Soil materials

**Agriculture ▸ Create Soil Material Assets** makes an item asset of every built-in material (in
`Items/Agriculture/Soil Materials`, listed in the database). A material's **data**: id, name, kind (texture, organic,
pH, fertilizer, mulch, treatment, beneficial), portion size, description, price, colour, its **effects** on soil
properties per portion per m², its pH effect, whether it needs working in, is a liquid, mulch, what it does to pests and
diseases and how much it harms the plant. A new asset with a new id is a new material.

## Hybrids

A **Hybrid Rule**: *Parent A* × *Parent B* → *Child* with a *Chance*. When a flower of one parent is pollinated by the
other (insects, wind or a brush, within the same *Cross Group*), its seeds are sometimes seeds of the child species.
The database warns when the parents share no Cross Group (they would never meet).

## Items

**Agriculture ▸ Create Items** makes assets for every id the database does not bind yet — seeds and produce of every
species, the tools, instruments and water containers — exactly like the runtime items (names, prices, per-copy codes),
with their **icons saved as PNG sprites** (`Items/Agriculture/Icons`) and a **model** to lie on the ground
(`Items/Agriculture/Models`), and binds them. Give them your own icons, models and prices; use them in recipes,
merchants, loot and quests.

Per-copy codes (in each copy's durability list): a **seed**'s genes, a piece of **produce**'s quality, potency, ripeness
and rare lustre, a **water container**'s litres and kind of water. Produce shows its quality grade (and potency for
medicinal and magical produce) in its tooltip and is worth what its quality is; food sates by its quality; produce with
an *Alchemy Ingredient* works in the alchemy vessels.
