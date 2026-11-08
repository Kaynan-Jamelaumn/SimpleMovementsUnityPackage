# 03 — Soil, Water and Weather

[← Agriculture](README.md)

## The soil types

Each cell has a **type** (from the layout: the base soil or a section) and its own properties. The type gives the
soil's character — its preset (**Use its soil** in the inspector), how it looks, how warm it gets, how hard it is to
work — and every plant species likes each type more or less (its *Soil Affinity*).

| Type | Preset (pH · N P K ppm · organic matter) | Character |
|---|---|---|
| **Sandy** | 6.2 · 12 12 70 · 1.2 % | Drains fast, holds little water or food, warms fast; nutrients wash out; easy to work |
| **Clay** | 6.8 · 28 18 260 · 3.5 % | Holds water and food, drains slowly, packs and waterlogs; heavy to work |
| **Rocky** | 7.0 · 10 8 120 · 1 % | Stones in the way of roots and tools; drains; poor |
| **Organic-rich** | 6.5 · 45 35 220 · 7 % | Dark, crumbly, fertile; holds water well; feeds itself as it rots |
| **Peat** | 4.4 · 35 10 60 · 65 % | Acid, wet, holds water like a sponge; barely rots until limed and drained |
| **Volcanic** | 6.0 · 30 14 300 · 9 % | Rich in minerals (potassium, magnesium, iron); holds phosphorus tight; warm |
| **Swamp** | 5.2 · 18 9 90 · 25 % | Waterlogged, salty, full of soil diseases |
| **Magical** | 6.6 · 40 30 200 · 8 % | Fertile and charged with magical essence (0.8) |

## pH and nutrients

The **pH** decides how much of each nutrient the roots can take (*availability*): acid soil locks phosphorus,
calcium, magnesium and molybdenum and frees iron, manganese and zinc (too much of them can harm); alkaline soil locks
iron, manganese, zinc, copper, boron and phosphorus. Every species has the pH it survives and the band it thrives in;
outside the band it grows slower and suffers.

The **twelve nutrients** — nitrogen, phosphorus, potassium (primary), calcium, magnesium, sulfur (secondary), iron,
zinc, copper, manganese, boron, molybdenum (micronutrients) — are concentrations in ppm. A plant needs each in
proportion to its appetite (*Nutrient Demand*, and per nutrient) and its stage (leaves want nitrogen, flowers and fruit
potassium and phosphorus); what is scarce limits its growth (the scarcest of all decides most of it), and a lack shows
in its leaves after a while. Roots take less of what is scarce: a hungry soil leaves a plant hungry, not stripped bare.
Too much of anything burns.

Every nutrient a plant takes comes out of **its root zone** — the cells its roots reach, which grows with the plant —
so neighbours compete for the same soil.

## The soil's own life

The soil is not a set of numbers waiting for the player: it changes by itself.

* **Water**: rain soaks in (packed wet soil sheds some); water above what the soil holds drains away, faster in
  sandy and loose soil; the surface dries with the sun, warmth and wind (slower under mulch).
* **Leaching**: what drains takes nitrogen, sulfur, boron, potassium, salts (and a little calcium and magnesium) with
  it — over-watering a sandy bed washes its food away. Waterlogged soil also loses nitrogen to the air and breeds
  root diseases.
* **The organic cycle**: organic matter rots while the soil is warm and moist (hardly at all in acid peat) and feeds
  slow reserves of nitrogen, phosphorus and potassium that release as the soil warms; it also gives sulfur and boron.
  A fallow organic-rich soil keeps itself fertile; a sandy one does not. Compost, manure and mulch keep the cycle going.
* **Weathering**: clay, stones and volcanic ash slowly give back potassium, calcium, magnesium, iron and a little
  phosphorus.
* **pH**: limestone, wood ash and sulfur change it over days (faster in moist soil), not at once; rain sours soil very
  slowly; watering drifts it towards the water's own pH.
* **Settling**: worked soil settles back over days, rain packs bare soil; mulch rots into organic matter; open holes
  slump in; materials left on top work in slowly (rain, worms).
* **Magic** drifts back to the soil type's own level.

## Soil materials

Materials are items (made at runtime, or assets: [chapter 06](06-Authoring-Species-and-Materials.md)). Put on the soil
with a method (*spread wide*, *around it*, *one spot*) and portions; unskilled hands spread unevenly. Solid materials
that need working in (compost, manure, lime, sand...) lie on top until tilled in, or work in slowly with water and
time; liquids soak in at once; mulch stays on top; treatments go on plants (*Treat*).

| Kind | Built in |
|---|---|
| Texture | Sand (drains, loosens), Clay (holds water and food), Gravel (drains) |
| Organic | Peat (organic matter, sours), Organic Compost (organic matter and a little of everything, slowly), Magical Compost (organic matter and magical essence) |
| pH | Wood Ash (potassium, calcium; sweetens), Limestone (calcium; sweetens), Sulfur (sours) |
| Fertilizers | Animal Manure (strong; salts and germs), Bone Meal (phosphorus, calcium), Mineral Fertilizer (N P K, salts), Alchemical Fertilizer (everything, a little magic), Liquid Fertilizer (fast), Slow-Release Fertilizer (feeds for weeks), Fish Fertilizer (with micronutrients), Herbal Fertilizer (potassium) |
| Mulch | Straw Mulch (keeps the water in, rots into organic matter) |
| Treatments | Alchemical Pesticide (kills every insect), Herbal Pest Spray (mild), Fungicide (copper and sulfur), Soil Cleanser (kills soil life, good and bad), Slug Bait |
| Beneficial | Ladybirds (eat aphids and other soft pests for a while) |

Too much fertilizer burns: salts build up (especially in soil that does not drain) and excess nutrients harm roots.

## Water

Every container (watering can, bucket, alchemist's flask) carries **litres of one kind of water**, per copy. Fill it at a
**Water Source**; pouring puts that water — and what it carries — into the soil.

| Water | Minerals | Salt (dS/m) | Dirt | Magic | pH | |
|---|---|---|---|---|---|---|
| Rain | 0 | 0 | 0 | 0 | 5.6 | Soft and clean, slightly acid |
| River | 0.3 | 0.25 | 0.25 | 0 | 7.2 | Some silt and germs |
| Well | 0.65 | 0.5 | 0.05 | 0 | 7.7 | Hard: adds calcium and magnesium, raises the pH, salts build up |
| Lake | 0.25 | 0.35 | 0.2 | 0 | 7.4 | |
| Swamp | 0.2 | 1.2 | 0.8 | 0.02 | 5.3 | Salty, dirty: feeds soil diseases |
| Spring | 0.5 | 0.1 | 0 | 0.03 | 7.0 | Clean and mineral |
| Alchemical | 0.05 | 0 | 0 | 0.1 | 6.5 | Carries a little nitrogen |
| Magical | 0.1 | 0 | 0 | 0.7 | 6.8 | Charged with magical essence |

(The Agriculture Database's *Water Quality* changes any of these.) A **rain barrel** has a capacity and fills with the
rain on its *catchment* of roof; a well never runs dry unless given a capacity. **Irrigated** sections keep their soil
at a moisture with their kind of water ([chapter 02](02-Defining-Soil-Areas.md)). Water that does not soak in runs off;
too much waterlogs the roots.

## Climate, seasons, the moon, day and night

The city has no Weather System, so it has **its own climate** (the Agriculture Database's *Climate*): for each season a
mean temperature, the day's swing between afternoon and night, the chance and intensity of rain, humidity, clouds, wind,
storms, heat waves and cold snaps. It is deterministic: every machine reads the same weather from the same time.

| Season | Mean | Swing | Rain chance | |
|---|---|---|---|---|
| Spring | 15 °C | 9 °C | 0.35 | Cool nights, frost possible early |
| Summer | 24 °C | 10 °C | 0.22 | Heat waves, storms |
| Autumn | 15 °C | 8 °C | 0.40 | Wet, windy, the first frosts |
| Winter | 3 °C | 6 °C | 0.30 | Frost, little light |

A field in a scene with a **Weather System** (and *Use World Weather*) uses its weather, with the seasons' differences
added. The calendar: **four seasons of 12 days** (*Days Per Season*), a **lunar cycle of 8 days** (new, waxing, full,
waning), days from 9 hours of light in mid-winter to 15 in mid-summer.

* **Temperature**: growth has a band per species; frost, heat and sudden changes harm; seeds germinate only in warm
  enough soil (the soil follows the air slowly).
* **Seasons**: species sow, flower and rest in their seasons; perennials gather cold hours over winter (some need them
  to flower) and go dormant.
* **The moon** changes how well some seeds come up and how potent some produce is (strongly for magical and alchemical
  plants); **the hour of sowing** matters to some (sown at night).
* **Light**: intensity now (clouds, the sun's height, shade from taller plants and structures) and **the day's
  exposure** (the light gathered since dawn). Each species has its band; too little makes it leggy and slow, too much
  scorches shade-lovers. Long-day and short-day plants flower by the day's length.
* **Humidity**: dry air stresses moisture-lovers; damp air and wet leaves breed fungal diseases.

## Growing structures

A **Growing Structure** is a box over (part of) a field that changes the weather of the cells inside it.

| Kind | Rain through | Drying | Light | Warmth | Frost kept off |
|---|---|---|---|---|---|
| Greenhouse | 0 | 0.6 × | 0.85 | +6 °C | 0.85 |
| Shade Shelter | 0.6 | 0.75 × | 0.45 | −1.5 °C | 0.2 |
| Cold Frame | 0.15 | 0.7 × | 0.8 | +3.5 °C | 0.65 |
| Magical Chamber | 0 | 0.5 × | 0.3 | +2 °C | 1 |

*Custom* takes your own numbers. Under glass nothing waters the soil but you. Fields work out their covers again when a
structure appears, moves or goes.
