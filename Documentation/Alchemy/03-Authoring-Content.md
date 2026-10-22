# Alchemy 03 — Authoring Content

**Scripts:** `Alchemy/Core/AlchemyDatabase.cs`, `AlchemyTypes.cs`, `AlchemyReaction.cs`, `AlchemyOutcome.cs`,
`AlchemyIngredient.cs`, `AlchemyEquipment.cs`, `AlchemyProcessing.cs`, `Alchemy/Items/AlchemyIngredientSO.cs`, `BrewedPotionSO.cs`,
`Alchemy/Runtime/AlchemyConditionSources.cs`.

Everything is data. New ingredients, substances, reactions, potions, vessels, filters, fuels and conditions need no code — and a new
ingredient joins every reaction its substances fit, so the possibilities grow without writing recipes.

---

## 1. The Alchemy Database

*Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Alchemy Database (Resources)* puts a copy of the built-in rules at
`Resources/SimpleMovements/Settings/AlchemyDatabase`. Stations use it (or the one in their *Database* field). Its
inspector counts the content, lists errors and warnings, and has *Open Alchemy Lab*, *Create Potion Items* and *Reset To
Built-in*.

| Section | |
|---|---|
| *Settings* | The physics ([02](02-Simulation-Rules.md)): step, boiling, extraction and stirring, solubility (*Precipitation Rate*, *Redissolve Rate*), evaporation, continuous stirring (turbulence, aeration, splashing, emulsions), the manual fire, baths, layers and particles, acidity, contamination, observation, bottling |
| *Properties* | The traits substances can have |
| *Substances* | Every chemical species |
| *Reactions* | The transformation rules |
| *Outcomes* | What a mixture can be |
| *Forms* | How ingredients come: whole, partially ground, finely ground, pulverised, extract — and yours (§7) |
| *Filters*, *Fuels* | The ways of filtering the liquid and what a manual fire burns (§8); empty = the built-in ones |
| *Item Ingredients* | Existing items (foods, monster parts...) usable as ingredients without changing their type |
| *Extensions* | More databases merged in (content packs): same ids replace, new ids add, a disabled reaction removes |

Ids are stable strings (lower case, no spaces): saves, histories and journals refer to them — rename the display names
freely, not the ids.

---

## 2. Properties

`id`, *Display Name*, *Description*, *Color*. Properties make rules general: a reaction can consume *anything with
organic ≥ 0.45* (every herb, present and future), an outcome can require *toxicity ≥ 0.04* whatever made it toxic. The
mixture's value of a property is the concentration-weighted sum over the liquid.

## 3. Substances

| Field | |
|---|---|
| *Id*, *Display Name*, *Description* | |
| *Observation* | What the player sees before identifying it ("a golden, sweet-smelling sap") |
| *Role* | Solvent (dilutes), Raw (unreacted ingredient matter), Intermediate, **Active** (makes potions), Reagent, Byproduct, Residue |
| *Phase* | Liquid (bottled, boils) or Solid (settles, harvested when emptying) |
| *Boiling Point*, *Evaporation Multiplier* | |
| *Color*, *Color Strength* | The liquid's colour is the blend (water is faint) |
| *Properties* | Its traits |
| *Harvest Item*, *Units Per Item* | What it is collected as (solids and undissolved matter when emptying; distilled liquids) |
| *Always Known* | Never shown as unknown (water) |
| *Solubility* | Per solvent, the units of it one unit of that solvent holds (Moon Dew: water 1, spirit 0.4, oil 0.03). Empty = no limit: it always stays in solution |
| *Other Solvent Solubility*, *Solubility Doubling Degrees* | For solvents not listed (0.05); how much more dissolves when hot (×2 per 40 °C; 0 = the same at any temperature) |
| *Stable Max*, *Stable Min* | The stable temperature range (2000 / −273 = always stable) |
| *Degrade Rate*, *Degrade Doubling Degrees* | How fast it breaks down outside the range: share per second 10 °C past the limit, ×2 per 10 °C further |
| *Degrades Into*, *Degrade Observation* | What it becomes (empty = destroyed) and what the player sees ("the hum fades as the violet pales") |
| *Density*, *Layer* | Relative to water (1): what floats, sinks or stays suspended; for solvents, the layer they form (*Aqueous* water, *Oily* oils, *Auto* mixes with both) — a fixed *Layer* places any substance |
| *Extraction pH*, *pH Sensitivity* | It comes out of ingredients faster the closer the liquid is to this pH (0 sensitivity = pH does not matter) |
| *Stable pH Min / Max*, *pH Degrade Rate*, *pH Degrade Observation* | The pH it survives (0 / 14 = always); outside it, it breaks down into its *Degrades Into* |
| *Crystal*, *Nucleation*, *Crystal Stillness*, *Max Cooling Rate*, *Crystal Growth* | The Solid substance it crystallises as when it comes out of solution still and slowly cooled; how supersaturated it must be to start without a seed; how still and how slowly cooled; how fast crystals grow |
| *Interactions* | What it does not get along with: **Interferes** (slows every reaction that consumes it), **Clashes** (destroys each other into products), **Curdles** (comes out of solution beside the other) — each with a selector, a strength and an observation |

Solubility, stability and compatibility need no reactions of your own: the rules turn a stable range into a breakdown
reaction (`breakdown_hot.<id>`, `breakdown_cold.<id>`, `breakdown_acid.<id>`, `breakdown_alkali.<id>`) and a *Clashes*
interaction into a reaction (`clash.<id>.<n>`) that the journal learns like any other, add *Interferes* as modifiers to
the reactions concerned, and apply each substance's solubility to whatever solvents the liquid holds
([02 §4](02-Simulation-Rules.md#4-solubility-saturation-and-crystals)). To change a breakdown's details beyond the fields
above, write a reaction yourself and leave the range open.

Properties the rules read: **acid** and **alkali** (the pH), **contaminant** (slows reactions, spoils purity),
**clarifying** (makes particles settle), **emulsifier** (keeps an emulsion), **oxidisable** (browns when air is beaten
in), **organic** (rots, scorches), **toxicity** (tainted outcomes, the charcoal filter).

## 4. Reactions

| Field | |
|---|---|
| *Reactants* | Selector (**Substance** / **Property** ≥ min / **Role**, with *Except*), *Ratio* (units per unit of reaction), *Order* (how strongly the rate follows its concentration; 0 = only needs it present) |
| *Products* | Substance and ratio |
| *Rate*, *Reference Temperature*, *Doubling Degrees* | Speed and how it changes with temperature (negative = faster when colder) |
| *Min / Max Temperature* | It only happens inside this window |
| *Modifiers* | Catalysts (× > 1), inhibitors and stabilisers (× < 1), blockers (× 0), reaching full effect at *Full Effect At* |
| *Conditions* | All must be met (pick types from the dropdown) |
| *Conditional Rates* | Speed changes while a condition holds (× 2 in copper, × 1.5 at night) |
| *Rate Variance* | The defined random variable (0 = fully predictable) |
| *Heat Per Unit* | Exothermic (+) / endothermic (−) |
| *Eruption Rate*, *Spill Fraction* | Violent reactions |
| *Observation*, *Visual* | What the player sees before understanding it; a presentation hint (bubbles, smoke, sparks, flash, glow, hiss) |

Design patterns:

* **A → B → degraded**: a chain of first-order reactions with rising minimum temperatures and slower rates
  (sap → essence at 55 °C+, essence → stronger essence at 75 °C+ slowly, → ash above 85 °C slower still). Players learn
  *when* to stop and *how hot*.
* **Redirection**: a reactant that combines with whichever intermediate is present, in different temperature windows.
* **Same result whatever the progress**: a reactant with a **Role** or **Property** selector ("any active compound").
* **Interrupt / stabilise**: a modifier with a multiplier of 0 or 0.2 on the chain.
* **Unexpected reaction**: a fast exothermic reaction with an eruption, between substances that rarely meet.
* **Environment**: conditions on `night`, `agitation`, vessel tags, pressure, your variables.
* **Acid or alkali gated**: a condition on `ph` (willow essence only forms in warm acid), an *Extraction pH* that draws an
  alkaloid out in vinegar, and a *Stable pH* range that makes the same vinegar destroy something else.
* **Two layers**: put two reactants in different layers (an oil-soluble one and a watery one): nothing happens until the
  player stirs them into an emulsion — or adds an emulsifier, or picks a solvent that mixes with both.
* **Process by hand**: conditions on `aeration` (stirred too hard), `heat` (a roaring fire), `bath`, `swing` (a steady
  temperature), `liquid` (mixed dry), `turbulence` (stirred evenly) — the minigames' precision becomes chemistry.
* **Purify by crystals**: a salt with a *Crystal*, a strong *Solubility Doubling* and some dirt (*contaminant*) in its
  crude ingredient: dissolve hot, filter, cool slowly and still — or seed it.
* **Incompatible**: a *Clashes* interaction for things that destroy each other, *Curdles* for one that comes out of
  solution beside another, *Interferes* for one that only gets in the way.
* **One herb, several uses**: give an ingredient several substances with different extractabilities and reactions — its
  main compound, an aroma that only spirit or oil holds, a toxin locked in the fibres. Whole, it gives the main compound;
  ground or steeped long, the toxin too; in spirit, the aroma.
* **The right solvent**: give a compound a low solubility in water and a high one in spirit (a resin) or oil (an aroma):
  the player must find which liquid draws it out — and that diluting the result with water brings it out of solution.
* **Too hot**: give the product of a reaction a *Stable Max* a little above the reaction's window: more fire makes it
  faster at first, then destroys it.

## 5. Outcomes

| Field | |
|---|---|
| *Id*, *Display Name*, *Description*, *Kind* | Potion, Material, Unstable, Incomplete, Degraded, Inert, Failed |
| *Priority* | Tried from the highest down; put variants (Tainted Healing) above their base potion, the catch-all at 0 |
| *Requirements* | Conditions on the **liquid**: *Dominant Substance* (the main active compound), *Substance Amount*, *Property Range*, *Role Share*, *Any Of*... |
| *Potency*, *Full Potency At* | What strength is measured by, and the share of the liquid that is full strength |
| *Tiers* | Label, *Min Quality*, *Item*, *Effect Multiplier* (lowest first) |
| *Units Per Bottle*, *Produces Items* | 0 = the database default; off = nothing is bottled (plain water) |
| *Effects*, *Base Price* | What drinking it does (for generated items) |

**Potion items**: *Create Potion Items* makes a `BrewedPotionSO` per tier (effects × the tier's multiplier, price, a vial
icon and model in the liquid's colour) and assigns it. You can also put any item in a tier (a hand-made potion, a
`MiscItemSO` for a material). A brewed potion poured into a vessel releases what it is made of (its potency substance and
water), so brews can be ingredients of further brews.

## 6. Ingredients

* **Alchemy Ingredient** items (*Create ▸ SimpleMovements ▸ Alchemy ▸ Ingredient*, or *Tools ▸ … ▸ Alchemy ▸ Create
  Ingredient Items* for the built-in ones): a material with an **Alchemy** profile:

  | Field | |
  |---|---|
  | *Releases* | What it is made of: substance, units per item and **extractability** (0–1: how easily it comes out). A Sunpetal: Solar Sap 3.6 u (0.9), Sunpetal Oil 0.5 u (0.6), Bitter Latex 0.6 u (0.06), Water 0.5 u (1) |
  | *Dissolve Seconds* | Seconds for an easily extracted substance to come out completely, whole, at 20 °C, still; 0 = at once (liquids, powders) |
  | *Form*, *Can Be Ground*, *Hardness* | The form it comes in (a form id: whole, coarse, ground, powder, extract), whether a mortar can grind it, and how hard it is to grind (0–1: more turns of the pestle) |
  | *Temperature*, *Observation* | Its own temperature (Frostmint −5 °C); what the player sees when it goes in |
* **Any other item** through the database's *Item Ingredients* (a Cooked Meat that releases fungal pulp...).
* **Brewed potions** are ingredients too (above).
* Byproducts that share a preset's name (Moon Crystal, Fire Ash), and substances a preset is made of (brine: Rock
  Salt), get the ingredient item as *Harvest Item*: crystals grown in one preparation dissolve into mana essence in the
  next; salt that crystallised out of a boiled-down brine is Rock Salt again.
* **Extracts** are ingredients like the others, with the *Extract* form and only what was drawn out (Sunpetal Extract:
  4.5 u of sap, no oil, no latex) — the cleaner, pricier way to the same compound.

## 7. Forms

The database's *Forms* (empty = the built-in **whole → coarse (partially ground) → ground (finely ground) → powder
(pulverised)**, and **extract**):

| Field | |
|---|---|
| *Id*, *Display Name* | Ingredient profiles name the id ("ground") |
| *Release Speed* | × how fast it gives up its substances (coarse 1.8, ground 3, powder 4, extract 8) |
| *Selectivity* | How much extractability matters: rate ∝ extractability ^ selectivity. High (whole: 2) = hard parts barely come out; 0 (extract) = everything at the same pace |
| *Ground Into* | The next grind level a mortar turns it into (whole → coarse → ground → powder); empty = cannot be ground further. The mortar offers every level along the chain |
| *Volatile Loss* | Share of its volatile substances (boiling below 150 °C, the more the lower) lost when it is ground to this form (powder 0.4) |
| *Observation* | What the player sees as it goes in ("ground to a coarse paste") |

Add a "dried" form (slower, more selective) or another grinding step — every ingredient that names the form follows it,
with no change to the code.

## 8. Filters and fuels

**Filters** (the database's *Filters*):

| Field | |
|---|---|
| *Id*, *Display Name*, *Description* | Stations list the ids they have at hand |
| *Strains Ingredients* | Takes out the ingredients still in the vessel (spent herbs) |
| *Catches Undissolved*, *Catches Solids* | Shares of the particles and of the solids at the bottom it catches (collected: crystals, salts) |
| *Adsorbs* | Selectors and shares of the dissolved matter it holds back (a charcoal bed: toxins, residues, contaminants) |
| *Liquid Loss* | Share of the liquid that stays soaked in it |
| *Medium* | An item used up each time (Filter Paper, Charcoal); empty = reusable |
| *Observation* | What the player sees |

**Fuels** (the database's *Fuels*): *Id*, *Display Name*, *Energy* (fuel units per item: firewood 1 ≈ 50 s at full fire),
*Flare* (a burst of flame when it catches, 0–1), *Item* (the item used up; empty = free with a woodpile), *Observation*.

## 9. Vessels

*Create ▸ SimpleMovements ▸ Alchemy ▸ Equipment* (or *Create Equipment Presets*); settings in [02 §12](02-Simulation-Rules.md#12-vessels).
A new vessel type is a new asset: give it tags and variables and write reactions or outcomes that care. *Surface* makes
an evaporating dish, *Fractionation* a still with a column.

## 10. Crafting conditions from the scene

* **Alchemy Surroundings** (added automatically when a station has no condition source): *Indoors* (fixed *Indoor
  Temperature*) or outdoors (the Weather System's temperature + *Outdoor Offset*, `humidity`, `rain`), `night` from the
  world clock.
* **Alchemy Fixed Conditions**: a list of named variables and an optional ambient override, for a place with its own
  alchemy (a ley-line shrine, a cold cellar, a volcano forge).
* Several sources on a station are read in hierarchy order; later ones override.

## 11. Content packs

Make a second Alchemy Database with only your additions and put it in the main database's *Extensions* (keep pack assets
outside `Resources/SimpleMovements`, or name the main one `AlchemyDatabase`, so the game loads the right one). Entries
with the same id replace the base ones (forms, filters and fuels too); a reaction with *Enabled* off removes the base reaction of that id. The Edit Mode
test *Extensions_AddAndReplaceRules* shows a pack adding a substance, a reaction that the existing Moonbloom joins, and a
potion — with no change to the base content or the code.
