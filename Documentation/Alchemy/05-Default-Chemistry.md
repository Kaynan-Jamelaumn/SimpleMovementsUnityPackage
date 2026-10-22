# Alchemy 05 — The Default Chemistry

**Spoilers.** This is the built-in content (`Alchemy/Core/AlchemyDefaults.cs`), what *Create Alchemy Database* copies into
your project: **48 ingredients, 66 substances, 33 authored reactions (58 once the rules add the breakdowns and clashes),
26 outcomes, 3 filters, 4 fuels and 6 vessels**. Players are meant to discover it; designers can read it here, change it,
or replace it.

All numbers below were measured with the simulation (iron cauldron unless said, 20 °C indoors, seed 1) — run them
yourself in the Alchemy Lab ([07](07-Editor-Tools-and-Testing.md)). "Fire 24 %" is a burner setting; a manual fire is
measured in [§6](#manual-fire).

---

## 1. Ingredients

Each ingredient is made of several substances; the number in brackets is its **extractability** (how easily it comes
out: [02 §3](02-Simulation-Rules.md#3-extraction-ingredients-forms-and-grinding)). "Whole" ingredients can be ground at a
station with a mortar; *Hardness* (default 0.4) makes grinding take longer. Temperatures are the ingredient's own when it
goes in (default 20 °C).

| Ingredient | Contains (per item) | Form | Gives up its substances in | Notes |
|---|---|---|---|---|
| Spring Water | 10 u Water | liquid | at once | A flask of clean water: the base of most brews. |
| Grain Spirit | 6 u Grain Spirit | liquid | at once | Strong alcohol. Draws out resins and oils, keeps things from rotting - and burns. |
| Lamp Oil | 6 u Lamp Oil | liquid | at once | A heavy oil that lets a brew get far hotter than water does. |
| Sunpetal | 3.6 u Solar Sap (0.9), 0.5 u Sunpetal Oil (0.6), 0.6 u Bitter Latex (0.06), 0.5 u Water | whole | 20 s | Golden petals full of sweet sap, with a fragrant oil and a bitter latex in the stems. 22 °C. |
| Sunpetal Extract | 4.5 u Solar Sap, 1 u Water | extract | 4 s | A vial of sunpetal sap, already drawn out and cleaned of oil and latex. |
| Frostmint | 4 u Frost Oil (0.85), 0.3 u Fungal Pulp (0.3) | whole | 15 s | Leaves so cold they frost your fingers. -5 °C. |
| Frostmint Extract | 5 u Frost Oil | extract | 3 s | Frost oil pressed from frostmint: pure and ice cold. -10 °C. |
| Embercap | 3 u Ember Dust (0.8), 1 u Fungal Pulp (0.5) | whole | 25 s | A mushroom that glows like a coal. 45 °C. |
| Bitterroot | 3 u Bitter Resin (0.6), 1 u Fungal Pulp (0.4) | whole | 30 s | A gnarled, poisonous root. |
| Moonbloom | 4 u Moon Dew (0.95) | whole | 10 s | A flower that only opens at night. 15 °C. |
| Rock Salt | 3 u Brine | whole | 8 s | Coarse salt crystals. Water holds about a third of its own amount; spirit almost none. |
| Quicksilver Drop | 0.6 u Quicksilver | liquid | at once | A bead of liquid metal in a tiny vial. |
| Lead Dust | 1 u Lead Dust | ground | 4 s | A pouch of heavy grey dust. |
| Void Salt | 2 u Void Salt | ground | 5 s | Black salt that seems to drink the light. |
| Glowcap Mushroom | 3 u Fungal Pulp (0.7) | whole | 20 s | A common pale mushroom. |
| Moon Crystal | 3 u Mana Essence (0.9) | whole | 40 s | A pale crystal grown from moon dew. Dissolves into pure mana essence. 10 °C. |
| Fire Ash | 0.5 u Ember Dust, 1 u Fire Ash | ground | 10 s | Grey ash of burnt-out ardent essence. Still faintly warm. 30 °C. |
| Vinegar | 8 u Water, 1 u Acetic Acid | liquid | at once | Soured wine: an acid. It draws alkaloids out of berries and roots, curdles milk - and destroys healing essence. |
| Muddy Water | 9 u Water, 0.6 u Silt | liquid | at once | River water full of silt. It clouds every brew made with it: filter it, or settle it with alum. 15 °C. |
| Seawater | 9 u Water, 1.5 u Brine, 0.1 u Silt | liquid | at once | Salt water. Boil it down in a wide dish and the salt is left behind. 15 °C. |
| Holy Water | 9 u Water, 1.5 u Sanctity | liquid | at once | Water from a shrine. It destroys venom, sludge and unstable magic. 15 °C. |
| Turpentine | 6 u Turpentine | liquid | at once | Distilled pine resin: an oily solvent that floats on water and dissolves resins and waxes. |
| Goat Milk | 8 u Water, 1 u Milk Curd, 0.4 u Sugars | liquid | at once | Fresh milk. Its curd binds venoms - and curdles in acid. 12 °C. |
| Egg Yolk | 0.4 u Lecithin, 1.5 u Egg Albumen, 1 u Water | whole (cannot be ground) | 3 s | A yolk with a little white: it holds oil and water together. The white binds tannins, and sets above 62 °C. 15 °C. |
| Honey | 3 u Sugars, 1 u Water, 0.3 u Beeswax | whole (cannot be ground) | 15 s | Thick and sweet: it keeps brews from rotting, and yeast turns it into mead. |
| Ice Chips | 5 u Water | liquid | at once | Chipped ice: it chills a brew in an instant (melting takes a lot of heat). -60 °C. |
| Sourfruit | 1.2 u Citric Acid (0.8), 1.5 u Water, 0.4 u Sugars (0.9) | whole | 15 s | A small, fiercely sour fruit: an acid with a little sugar. 18 °C. Hardness 0.2. |
| Potash | 2.5 u Potash Lye | ground | 5 s | Lye leached from wood ash: a strong alkali. It cooks oil into soap and neutralises acids. |
| Chalk | 3 u Chalk | ground | 3 s | Powdered chalk: it hardly dissolves, but slowly neutralises acid. |
| Alum Crystals | 2 u Alum Crystals | whole (cannot be ground) | 2 s | Clear, pure crystals of alum, prized by dyers and healers. They dissolve slowly, and well only when hot. |
| Alum Powder | 2 u Alum | ground | 10 s | Powdered alum: a clarifier that dissolves well only when hot. |
| Crude Alum | 1.6 u Alum (0.9), 0.5 u Silt | whole | 40 s | Alum dug from the ground, mixed with silt. Dissolve it hot, filter it, cool it slowly and still to grow pure crystals. Hardness 0.7. |
| Saltpetre Crystals | 2 u Saltpetre Crystals | whole (cannot be ground) | 2 s | Pure saltpetre needles: they feed fire, and make blasting powder. Far more dissolves hot than cold. |
| Saltpetre Powder | 2 u Niter | ground | 8 s | Powdered saltpetre: it feeds fire and dissolves quickly. |
| Cave Saltpetre | 1.6 u Niter (0.9), 0.4 u Silt, 0.3 u Brine | whole | 30 s | Saltpetre scraped from cave walls, full of dirt and salt. Purify it: dissolve hot, filter, cool slowly - or seed it. 18 °C. Hardness 0.5. |
| Iron Filings | 1 u Iron Filings | ground | 2 s | Fine iron: it turns tannin into ink, and hurries the browning of brews stirred too hard. |
| Charcoal | 2 u Charcoal | whole | 3 s | Porous charcoal: it soaks up poisons, burns long and steady, and makes a filter bed. Hardness 0.3. |
| Lavender | 0.8 u Lavender Oil (0.55), 0.3 u Tannin (0.3), 0.4 u Plant Fibre (0.3), 0.3 u Water | whole | 20 s | Fragrant flowers. Their oil calms - but water barely holds it: use spirit or oil, or distil it. Hardness 0.2. |
| Lavender Oil | 3 u Lavender Oil | extract | 2 s | Lavender's oil, distilled pure. |
| Fire Pepper | 1.2 u Pepperfire Oil (0.5), 0.8 u Plant Fibre (0.3), 0.4 u Water | whole | 20 s | A small red pepper that burns. Its oil wants oil or spirit, not water. Hardness 0.3. |
| Oak Gall | 2 u Tannin (0.6), 0.5 u Plant Fibre (0.3) | whole | 40 s | A hard nut-like growth on oak leaves, full of tannin. With iron it makes ink. Hardness 0.6. |
| Willow Bark | 1 u Salicin (0.4), 1 u Tannin (0.6), 1 u Plant Fibre (0.3) | whole | 40 s | Bitter bark: in warm acid it gives an essence that eases pain. Its tannin spoils the purity: bind it with egg white. Hardness 0.7. |
| Snakeweed Root | 1 u Serpentine (0.2), 0.5 u Tannin (0.5), 1 u Plant Fibre (0.3) | whole | 90 s | A bitter root. In acid it gives up an alkaloid that gentle heat turns into antitoxin. Hardness 0.6. |
| Dreamshade Berries | 0.6 u Somnine (0.2), 1.2 u Sugars (0.9), 1 u Water | whole | 40 s | Dusky berries that bring deep sleep - their alkaloid comes out in acid; heat and alkali destroy it. 18 °C. Hardness 0.15. |
| Pine Resin | 1.2 u Turpentine (0.7), 1.8 u Rosin (0.6) | whole | 30 s | Sticky resin: distil it for turpentine. It also flares on a fire. Hardness 0.5. |
| Beeswax | 3 u Beeswax | whole | 30 s | Wax from the comb: it melts into hot oil and sets as it cools. Hardness 0.3. |
| Brewer's Yeast | 0.5 u Live Yeast | ground | 5 s | Live yeast: with sugar and warmth it brews spirit. Heat above 42 °C kills it. 18 °C. |
| Blasting Powder | 2 u Blasting Powder | ground | 1 s | Saltpetre, ember dust and charcoal. It goes off above 190 °C. |

Two more presets are **supplies**, not ingredients: *Filter Paper* (used up by the Filter Paper filter) and *Firewood*
(burned by a manual fire). *Charcoal*, *Pine Resin* and *Lamp Oil* are ingredients **and** fuels; *Charcoal* also makes a
filter bed.

**Forms** ([02 §3](02-Simulation-Rules.md#3-extraction-ingredients-forms-and-grinding)):

| Form | Release speed | Selectivity | Volatile oils lost when ground | Grinds into |
|---|---|---|---|---|
| Whole (`whole`) | × 1 | 2 | 0 % | coarse |
| Partially ground (`coarse`) | × 1.8 | 1.6 | 0 % | ground |
| Finely ground (`ground`) | × 3 | 0.5 | 0 % | powder |
| Pulverised (`powder`) | × 4 | 0.15 | 40 % |  |
| Extract (`extract`) | × 8 | 0 | 0 % |  |

"Gives up its substances in" is for an easily extracted substance, whole, at 20 °C, still: heat (× 2 per 30 °C), stirring
and grinding speed it up; a hard-to-extract substance takes far longer (a whole Sunpetal's latex: about 90 minutes at 20 °C).

## 2. Substances

Properties are on a 0–1 scale (acid and alkali can go higher). "Reactivity" is the `volatility` property: how violently
it reacts — how easily something evaporates is its boiling point. Liquids marked "—" do not boil (salts, sugars, fibres).

| Substance | Role | Phase · boils | Density | Properties | Notes |
|---|---|---|---|---|---|
| Water | Solvent | liquid · 100 °C | 1 |  | watery layer; known from the start |
| Grain Spirit | Solvent | liquid · 78 °C | 0.79 | reactivity 0.15 | known from the start |
| Lamp Oil | Solvent | liquid · 260 °C | 0.9 | organic 0.2 | oily layer; known from the start |
| Turpentine | Solvent | liquid · 155 °C | 0.86 | organic 0.3, fire 0.2, reactivity 0.1 | oily layer |
| Solar Sap | Raw | liquid · 160 °C | 1.2 | vitality 0.4, fire 0.2, organic 0.6, oxidisable 0.3 |  |
| Sunpetal Oil | Byproduct | liquid · 95 °C | 0.9 | fire 0.3, reactivity 0.4, organic 0.3 | evaporates ×2 |
| Bitter Latex | Raw | liquid · 200 °C | 1 | toxicity 1, organic 0.3 |  |
| Frost Oil | Raw | liquid · 140 °C | 0.92 | frost 0.6, mind 0.2, organic 0.4 |  |
| Ember Dust | Raw | solid | 2.2 | fire 0.8, reactivity 0.4, mineral 0.2 |  |
| Bitter Resin | Raw | liquid · 190 °C | 1.1 | toxicity 0.6, organic 0.5 |  |
| Moon Dew | Raw | liquid · 90 °C | 1.05 | arcane 0.6, mind 0.3 |  |
| Brine | Reagent | liquid · — | 1.2 | mineral 0.6, stability 0.6 |  |
| Quicksilver | Reagent | liquid · 357 °C | 13.5 | mineral 0.7, arcane 0.2, toxicity 0.3, reactivity 0.2 |  |
| Lead Dust | Reagent | solid | 11 | mineral 0.9, toxicity 0.2 |  |
| Void Salt | Reagent | liquid · — | 2.5 | mineral 0.5, stability 0.8 |  |
| Fungal Pulp | Raw | liquid · 120 °C | 1.05 | organic 0.8, toxicity 0.05 |  |
| Vital Essence | Active | liquid · 180 °C | 1.05 | vitality 1, organic 0.1, oxidisable 0.35 | watery layer |
| Ardent Essence | Active | liquid · 200 °C | 1 | fire 0.8, vitality 0.3 |  |
| Serene Essence | Active | liquid · 150 °C | 1 | mind 0.9, frost 0.3, arcane 0.2 |  |
| Tempered Essence | Active | liquid · 210 °C | 1 | fire 0.4, frost 0.4, stability 0.8 |  |
| Rime Essence | Active | liquid · 140 °C | 1 | frost 0.9, stability 0.3 |  |
| Mana Essence | Active | liquid · 170 °C | 1.02 | arcane 1 |  |
| Ember Spirit | Active | liquid · 60 °C | 1 | fire 0.6, reactivity 0.7 |  |
| Null Essence | Active | liquid · 220 °C | 1 | stability 1 |  |
| Venom | Active | liquid · 230 °C | 1.1 | toxicity 1 |  |
| Unstable Aether | Intermediate | liquid · 170 °C | 1 | arcane 0.6, reactivity 0.9 |  |
| Moon Crystal | Byproduct | solid | 2.3 | arcane 0.5, mineral 0.5 |  |
| Fire Ash | Residue | solid | 0.6 | fire 0.2, mineral 0.3 |  |
| Bitter Sludge | Residue | liquid · 190 °C | 1.15 | toxicity 0.3, organic 0.3, contaminant 0.3 |  |
| Char | Residue | solid | 0.5 | mineral 0.2 |  |
| Acetic Acid | Reagent | liquid · 118 °C | 1.05 | acid 1, organic 0.1 |  |
| Citric Acid | Reagent | liquid · — | 1.5 | acid 1.3 |  |
| Potash Lye | Reagent | liquid · — | 1.4 | alkali 1.4, toxicity 0.15, mineral 0.4 |  |
| Chalk | Reagent | liquid · — | 2.7 | alkali 0.8, mineral 0.8 |  |
| Alum | Reagent | liquid · — | 1.75 | astringent 0.8, clarifying 0.6, acid 0.2, mineral 0.6 | crystallises as Alum Crystals |
| Alum Crystals | Byproduct | solid | 1.75 | mineral 0.7, astringent 0.4 |  |
| Niter | Reagent | liquid · — | 2.1 | fire 0.3, mineral 0.6 | crystallises as Saltpetre Crystals |
| Saltpetre Crystals | Byproduct | solid | 2.1 | fire 0.3, mineral 0.7 |  |
| Silt | Residue | liquid · — | 1.08 | contaminant 1, mineral 0.6 |  |
| Tannin | Raw | liquid · — | 1.3 | astringent 1, organic 0.4, oxidisable 0.5 |  |
| Iron Filings | Reagent | solid | 7.8 | mineral 0.9 |  |
| Iron Gall Ink | Active | liquid · 300 °C | 1.1 | astringent 0.3, mineral 0.2 |  |
| Lavender Oil | Active | liquid · 92 °C | 0.88 | mind 0.6, organic 0.3, oxidisable 0.35 | evaporates ×2.5 |
| Rosin | Byproduct | liquid · — | 1.08 | organic 0.5 |  |
| Sugars | Raw | liquid · — | 1.45 | organic 0.4, vitality 0.1 |  |
| Live Yeast | Reagent | liquid · — | 1.1 | organic 0.4 |  |
| Esters | Byproduct | liquid · 77 °C | 0.9 | organic 0.1 | evaporates ×1.5 |
| Beeswax | Byproduct | liquid · — | 0.96 | organic 0.3, stability 0.3 |  |
| Pepperfire Oil | Raw | liquid · 300 °C | 0.94 | fire 0.9, organic 0.3, toxicity 0.02 |  |
| Salamander Essence | Active | liquid · 220 °C | 1.02 | fire 0.6, vitality 0.5 |  |
| Lecithin | Reagent | liquid · — | 1 | emulsifier 1, organic 0.3 |  |
| Egg Albumen | Raw | liquid · — | 1.04 | organic 0.4, vitality 0.1, clarifying 0.3 |  |
| Egg Curds | Residue | solid | 1.05 | contaminant 0.5 |  |
| Lees | Residue | solid | 1.3 |  |  |
| Milk Curd | Raw | liquid · — | 1.3 | organic 0.5, vitality 0.2 |  |
| Bound Toxins | Residue | solid | 1.2 |  |  |
| Serpentine | Raw | liquid · — | 1.15 | toxicity 0.05, mind 0.2, organic 0.2 |  |
| Antitoxin | Active | liquid · 210 °C | 1.05 | vitality 0.4, stability 0.3 |  |
| Somnine | Active | liquid · 200 °C | 1.05 | mind 0.6, toxicity 0.03 |  |
| Salicin | Raw | liquid · — | 1.2 | organic 0.3, vitality 0.1 |  |
| Willow Essence | Active | liquid · 220 °C | 1.05 | vitality 0.3, mind 0.3 |  |
| Plant Fibre | Raw | liquid · — | 1.1 | organic 0.6 |  |
| Sanctity | Reagent | liquid · — | 1 | stability 0.4, arcane 0.2 |  |
| Charcoal | Reagent | solid | 0.45 | mineral 0.3 |  |
| Blasting Powder | Byproduct | solid | 1.7 | fire 1, reactivity 0.9 |  |
| Soft Soap | Byproduct | liquid · — | 1.02 | emulsifier 0.8, alkali 0.2 |  |

### Solubility

Units one unit of the solvent holds (in brackets: *Other Solvent Solubility*, for a solvent it does not list); substances
not listed here have no limit. ×2 per: how much hotter the liquid must be to hold twice as much.

| Substance | Water | Grain Spirit | Lamp Oil | Turpentine | ×2 per | Crystals |
|---|---|---|---|---|---|---|
| Sunpetal Oil | 0.02 | 0.6 | 1 | 1 | 40 °C |  |
| Bitter Resin | 0.3 | 1 | 0.5 | 1 | 40 °C |  |
| Moon Dew | 1 | 0.4 | 0.03 | 0.02 | 40 °C |  |
| Brine | 0.36 | 0.01 | 0.001 | 0.001 | 400 °C |  |
| Mana Essence | 1 | 0.5 | 0.02 | 0.02 | 40 °C |  |
| Venom | 0.01 | 1 | 0.5 | 0.5 | 40 °C |  |
| Citric Acid | 0.6 | 0.3 | 0.005 | 0.005 | 40 °C |  |
| Potash Lye | 1.1 | 0.05 | 0.001 | 0.001 | 40 °C |  |
| Chalk | 0.004 | 0.001 | 0.001 | 0.001 | — |  |
| Alum | 0.06 | 0.002 | 0.001 | 0.001 | 16 °C | Alum Crystals (nucleation ×1.4, still ≤ 0.15, cooling ≤ 0.8 °C/s) |
| Niter | 0.3 | 0.01 | 0.001 | 0.001 | 22 °C | Saltpetre Crystals (nucleation ×1.6, still ≤ 0.15, cooling ≤ 0.8 °C/s) |
| Silt | 0.001 | ~0 | 0 | ~0 | — |  |
| Tannin | 0.5 | 0.8 | 0.01 | 0.01 | 40 °C |  |
| Lavender Oil | 0.01 | 1 | 1 | 1 | 40 °C |  |
| Rosin | 0.001 | 0.6 | 0.5 | 1.5 | 20 °C |  |
| Sugars | 2 | 0.15 | 0.005 | 0.005 | 40 °C |  |
| Beeswax | ~0 | 0.01 | 0.15 | 0.4 | 10 °C |  |
| Pepperfire Oil | 0.01 | 0.5 | 1 | 0.8 | 40 °C |  |
| Milk Curd | 0.2 | 0.01 | 0.01 | 0.005 | 40 °C |  |
| Plant Fibre | 0.001 | 0.001 | 0.001 | 0.001 | — |  |

### Stability

Outside these ranges a substance breaks down ([02 §5](02-Simulation-Rules.md#thermal-and-ph-stability)): the heat rate is
the share lost per second 10 °C past the limit, the pH rate at 20 °C (×2 per 20 °C hotter).

| Substance | Stable | Breakdown | Becomes | Seen |
|---|---|---|---|---|
| Solar Sap | up to 135 °C | 0.03/s (×2 / 10 °C) | Bitter Sludge | the golden sap caramelises and turns bitter |
| Bitter Latex | up to 85 °C | 0.15/s (×2 / 10 °C) | — (destroyed) | the milky trace curdles and vanishes |
| Vital Essence | up to 115 °C, pH ≥ 3, pH ≤ 11.5 | 0.03/s (×2 / 10 °C) · pH 0.03/s | Bitter Sludge | the rose turns a muddy brown / the rose curdles to a dull, sour pink |
| Ardent Essence | up to 170 °C | 0.03/s (×2 / 10 °C) | Fire Ash | the amber blackens |
| Serene Essence | up to 75 °C | 0.02/s (×2 / 10 °C) | Water, Bitter Sludge | the teal fades and clouds |
| Rime Essence | up to 25 °C | 0.01/s (×2 / 10 °C) | Water | the frost flowers melt away |
| Mana Essence | up to 85 °C | 0.02/s (×2 / 10 °C) | Water | the hum fades as the violet pales |
| Venom | up to 95 °C | 0.03/s (×2 / 10 °C) | Bitter Sludge | the green film breaks up |
| Lavender Oil | up to 160 °C | 0.03/s (×2 / 10 °C) | Bitter Sludge | the floral scent turns acrid |
| Sugars | up to 160 °C | 0.04/s (×2 / 10 °C) | Char | the sweetness darkens into bitter caramel |
| Live Yeast | up to 42 °C | 0.2/s (×2 / 10 °C) | — (destroyed) | the froth collapses |
| Salamander Essence | up to 120 °C | 0.03/s (×2 / 10 °C) | Bitter Sludge | the scarlet glow dims to brown |
| Egg Albumen | up to 62 °C | 0.1/s (×2 / 10 °C) | Egg Curds | white curds set in the liquid |
| Antitoxin | up to 85 °C, pH ≥ 2.5 | 0.03/s (×2 / 10 °C) · pH 0.02/s | Bitter Sludge | the pale glow fades / the green turns a dull grey |
| Somnine | up to 75 °C, pH ≤ 9 | 0.04/s (×2 / 10 °C) · pH 0.05/s | Bitter Sludge | the violet fades to grey / the violet turns a murky green |
| Willow Essence | up to 110 °C | 0.03/s (×2 / 10 °C) | Bitter Sludge | the amber clouds |

### pH and extraction

These come out of ingredients faster the closer the liquid is to their pH (×2 per 2 ÷ sensitivity pH units closer than
neutral is): vinegar (pH ~4.4 with two waters) roughly doubles the alkaloids.

| Substance | Comes out best at | Sensitivity |
|---|---|---|
| Tannin | pH 9 | 0.3 |
| Serpentine | pH 3.5 | 0.8 |
| Somnine | pH 4 | 0.7 |
| Salicin | pH 5 | 0.3 |

### Compatibility

| Substance | Kind | With | Strength | Makes | Seen |
|---|---|---|---|---|---|
| Egg Albumen | Clashes | Tannin | 0.6 | Lees | the cloudiness gathers into soft flocks |
| Milk Curd | Curdles | anything with Acid ≥ 0.5 | 4 | — |  |
| Milk Curd | Clashes | Venom | 0.4 | Bound Toxins | the milk curdles green around the venom |
| Sanctity | Clashes | Venom | 0.5 | Water | the green film hisses away |
| Sanctity | Clashes | Bitter Sludge | 0.3 | Water | the murk clears |
| Sanctity | Clashes | Unstable Aether | 0.8 | Null Essence | the crackling dies with a sigh |

## 3. Reactions

The authored reactions; "faster with / slowed by / stopped by" are modifiers (not used up), "needs" are conditions, "×"
are conditional rates. The rules add one reaction per stability limit above ("Vital Essence breaks down", "Acid destroys
Vital Essence"...) and one per *Clashes* interaction, for 58 in all.

| Reaction | Consumes → makes | Window and speed |
|---|---|---|
| Vital Extraction | Solar Sap → Vital Essence + Water | 55–130 °C · faster when hotter · faster with Quicksilver · stopped by Lead Dust |
| Kindling | Vital Essence → Ardent Essence | 75–400 °C · faster when hotter · faster with Ember Dust · slowed by Brine · stopped by Lead Dust · gives off heat |
| Burn-out | Ardent Essence → Fire Ash + Bitter Sludge | above 85 °C · faster when hotter · slowed by Brine |
| Serene Binding | Vital Essence + Frost Oil → Serene Essence | 15–80 °C · cools the mixture |
| Tempering | Ardent Essence + Frost Oil → Tempered Essence | 60–110 °C |
| Flash Quench | Ardent Essence + Frost Oil → Ember Spirit | above 110 °C · cools the mixture |
| Frost Settling | Frost Oil → Rime Essence | below 8 °C |
| Moon Infusion | Moon Dew → Mana Essence | 30–75 °C · faster when hotter · stopped by Lead Dust · ×2 in a copper vessel · ×1.5 at night |
| Moon Crystallisation | Moon Dew + Brine → Moon Crystal | below 12 °C · needs: left perfectly still · ×3 at night |
| Overcharge | Mana Essence → Unstable Aether | above 70 °C · faster when hotter · needs: Quicksilver at least 0.4% of the liquid |
| Aether Eruption | Unstable Aether → Fire Ash + Water | above 95 °C · faster when hotter · gives off heat · violent |
| Nullification | any Active compound except Null Essence + Void Salt → Null Essence | cools the mixture |
| Venom Drawing | Bitter Resin + Grain Spirit → Venom + Grain Spirit | 35–78 °C · faster when hotter |
| Tainting | Vital Essence + Bitter Resin → Bitter Sludge | above 20 °C · faster when hotter |
| Ember Flash | Ember Dust + Grain Spirit → Fire Ash | above 30 °C · gives off heat · violent |
| Ember Infusion | Ember Dust → Ardent Essence + Fire Ash | above 60 °C · faster when hotter · faster with Niter · gives off heat |
| Rot | anything with Organic ≥ 0.45 except Lamp Oil, Grain Spirit → Bitter Sludge | 25–50 °C · slowed by Grain Spirit, Brine, Sugars |
| Scorching | anything with Organic ≥ 0.1 except Lamp Oil → Char | above 170 °C · faster when hotter · gives off heat |
| Scorched Bottom | anything with Organic ≥ 0.3 except Lamp Oil, Grain Spirit, Turpentine → Char + Bitter Sludge | above 60 °C · needs: over a roaring fire (heat ≥ 0.85), left perfectly still, not in a bath |
| Spirit Flare | Grain Spirit → Water | above 65 °C · needs: over a roaring fire (heat ≥ 0.8), in an open vessel, Grain Spirit at least 6% of the liquid · gives off heat · violent |
| Oxidation | anything with Oxidisable ≥ 0.3 → Bitter Sludge | above 0 °C · faster when hotter · faster with Iron Filings · needs: stirred hard enough to beat in air (aeration ≥ 0.3) · ×1.5 in an iron vessel |
| Neutralisation | anything with Acid ≥ 0.5 + anything with Alkali ≥ 0.5 → Brine + Water | gives off heat |
| Fermentation | Sugars → Grain Spirit + Water + Esters | 12–40 °C · faster when hotter · faster with Live Yeast · slowed by Grain Spirit · needs: Live Yeast at least 0.2% of the liquid |
| Yeast Poisoning | Live Yeast → nothing | needs: Grain Spirit at least 12% of the liquid |
| Inking | Tannin → Iron Gall Ink | 5–100 °C · faster when hotter · needs: ≥ 0.3 u of Iron Filings · ×3 stirred hard enough to beat in air (aeration ≥ 0.15) |
| Salamander Binding | Pepperfire Oil + Vital Essence → Salamander Essence | 40–95 °C · gives off heat |
| Saponification | Potash Lye + Lamp Oil → Soft Soap + Water | 50–130 °C · faster when hotter · gives off heat |
| Antitoxin Tempering | Serpentine → Antitoxin | 45–85 °C · faster when hotter · faster with Venom |
| Willow Hydrolysis | Salicin → Willow Essence | 50–100 °C · faster when hotter · needs: in acid (pH ≤ 5) · ×2 in acid (pH ≤ 3.5) |
| Adsorption | anything with Toxicity ≥ 0.4 except Potash Lye, Quicksilver → Bound Toxins | -10–200 °C · faster when hotter · faster with Charcoal · needs: ≥ 0.5 u of Charcoal · ×2 stirred ≥ 0.3 |
| Charcoal Clearing | Bitter Sludge → Bound Toxins | -10–200 °C · faster when hotter · faster with Charcoal · needs: ≥ 0.5 u of Charcoal |
| Powder Mixing | Saltpetre Crystals + Ember Dust + Charcoal → Blasting Powder | 10–150 °C · needs: (almost) no liquid in the vessel (≤ 0.5 u), stirred ≥ 0.2 |
| Detonation | Blasting Powder → Fire Ash | above 190 °C · gives off heat · violent |

## 4. Outcomes (highest priority first)

Effects are the Standard tier's; Weak ×0.5, Potent ×1.5, Superior ×2. Tiers: Weak from 0 %, Standard 35 %, Potent 65 %,
Superior 90 % quality. Strength from: the substance whose concentration is the potency, and the concentration that is full
strength.

| Priority | Outcome | Kind | Requires | Strength from | Effects |
|---|---|---|---|---|---|
| 80 | Unstable Concoction | Unstable | (Reactivity ≥ 0.35 or Unstable Aether at least 5% of the liquid) | — | −15 health, +10% speed 10 s |
| 70 | Tainted Healing Draught | Potion | Vital Essence is the main active compound, Vital Essence at least 3% of the liquid, Toxicity ≥ 0.04 | Vital Essence (full at 30 %) | +25 health, −10 sanity |
| 68 | Murky Healing Draught | Potion | Vital Essence is the main active compound, Vital Essence at least 3% of the liquid, Contaminant ≥ 0.01 | Vital Essence (full at 35 %) | +25 health, −15 stamina |
| 66 | Healing Salve | Potion | Vital Essence is the main active compound, Vital Essence at least 3% of the liquid, Lamp Oil at least 30% of the liquid, Beeswax at least 2% of the liquid | Vital Essence (full at 30 %) | +20 health, +3 health regen 40 s |
| 60 | Healing Draught | Potion | Vital Essence is the main active compound, Vital Essence at least 3% of the liquid | Vital Essence (full at 35 %) | +35 health, +2 health regen 12 s |
| 60 | Draught of Vigour | Potion | Ardent Essence is the main active compound, Ardent Essence at least 3% of the liquid | Ardent Essence (full at 30 %) | +60 stamina, +3 stamina regen 20 s, +8 body heat |
| 60 | Calming Tonic | Potion | Serene Essence is the main active compound, Serene Essence at least 3% of the liquid | Serene Essence (full at 30 %) | +25 sanity, +2 mana regen 15 s |
| 60 | Elixir of Fortitude | Potion | Tempered Essence is the main active compound, Tempered Essence at least 3% of the liquid | Tempered Essence (full at 30 %) | −25% damage taken 60 s |
| 60 | Rime Draught | Potion | Rime Essence is the main active compound, Rime Essence at least 3% of the liquid | Rime Essence (full at 30 %) | −20 body heat, −10 body heat 20 s |
| 60 | Mana Potion | Potion | Mana Essence is the main active compound, Mana Essence at least 3% of the liquid | Mana Essence (full at 30 %) | +50 mana |
| 60 | Quickstep Elixir | Potion | Ember Spirit is the main active compound, Ember Spirit at least 3% of the liquid | Ember Spirit (full at 25 %) | +20% speed 30 s |
| 60 | Stillwater Draught | Potion | Null Essence is the main active compound, Null Essence at least 3% of the liquid | Null Essence (full at 30 %) | +40 breath, −30% breath loss 45 s |
| 60 | Salamander Draught | Potion | Salamander Essence is the main active compound, Salamander Essence at least 3% of the liquid | Salamander Essence (full at 25 %) | +15 body heat, +1.5 health regen 30 s, +2 stamina regen 20 s |
| 60 | Antidote | Potion | Antitoxin is the main active compound, Antitoxin at least 3% of the liquid | Antitoxin (full at 8 %) | +15 health, −20% damage taken 60 s, +1 health regen 20 s |
| 60 | Dreamless Sleep Draught | Potion | Somnine is the main active compound, Somnine at least 2% of the liquid | Somnine (full at 8 %) | +35 rest, +15 sanity, −15% speed 30 s |
| 60 | Willowbark Draught | Potion | Willow Essence is the main active compound, Willow Essence at least 3% of the liquid | Willow Essence (full at 8 %) | +10 health, −15% damage taken 90 s, +1 sanity regen 30 s |
| 60 | Lavender Tincture | Potion | Lavender Oil is the main active compound, Lavender Oil at least 1.5% of the liquid | Lavender Oil (full at 15 %) | +20 sanity, +10 rest, +1 sanity regen 30 s |
| 60 | Iron Gall Ink | Material | Iron Gall Ink is the main active compound, Iron Gall Ink at least 3% of the liquid | Iron Gall Ink (full at 30 %) | −3 sanity |
| 60 | Venom Extract | Material | Venom is the main active compound, Venom at least 3% of the liquid | Venom (full at 30 %) | −30 health, −20 health 10 s |
| 53 | Honey Brandy | Potion | Esters at least 0.4% of the liquid, Grain Spirit at least 30% of the liquid, Active matter ≤ 10% of the dissolved matter | Grain Spirit (full at 60 %) | +25 stamina, +10 body heat, +5 sanity, −10% speed 30 s |
| 52 | Honey Mead | Potion | Esters at least 0.1% of the liquid, Grain Spirit 3–30% of the liquid, Active matter ≤ 10% of the dissolved matter | Grain Spirit (full at 12 %) | +15 drink, +15 stamina, +8 sanity, −5% speed 30 s |
| 50 | Soft Soap | Material | Soft Soap at least 20% of the liquid | Soft Soap (full at 50 %) | −5 health |
| 40 | Spoiled Mixture | Degraded | Residue matter ≥ 40% of the dissolved matter | — | −5 health, −5 sanity |
| 30 | Cloudy Infusion | Incomplete | Raw matter ≥ 50% of the dissolved matter | — | +10 drink |
| 20 | Plain Liquid | Inert | (Solvent matter ≥ 97% of the liquid or (Active matter ≤ 2% of the dissolved matter and Raw matter ≤ 5% of the dissolved matter and Residue matter ≤ 5% of the dissolved matter and Intermediate matter ≤ 5% of the dissolved matter)) | — | — |
| 0 | Murky Mixture | Failed | — | — | +5 drink, −2 sanity |

## 5. Filters, fuels and vessels

| Filter | Strains ingredients | Particles caught | Solids caught | Also holds back | Liquid lost | Uses up |
|---|---|---|---|---|---|---|
| Cloth Strainer | yes | 60 % | 80 % | — | 2 % | — (reusable) |
| Filter Paper | yes | 100 % | 100 % | — | 5 % | a Filter Paper |
| Charcoal Bed | yes | 100 % | 100 % | 75 % of toxins (toxicity ≥ 0.4), 80 % of residues, 90 % of contaminants, 50 % of astringents (tannin), 10 % of every active compound | 8 % | a Charcoal |

| Fuel | Fuel units (≈ 50 s each at full fire) | Flare | Item |
|---|---|---|---|
| Firewood | 1 | — | free (a woodpile at the station) |
| Charcoal | 2.2 | — | Charcoal |
| Pine Resin | 0.5 | 0.8 | Pine Resin |
| Lamp Oil | 1.2 | 0.4 | Lamp Oil |

| Vessel | Holds | Mass | Hottest | Vapour | Notes |
|---|---|---|---|---|---|
| Iron Cauldron | 60 u | 25 | 240 °C | vented | `iron`, `open`: oxidation ×1.5; spirit can flare |
| Copper Alembic | 30 u | 15 | 220 °C | condensed into a flask | `copper`: moon infusion ×2 |
| Glass Retort | 20 u | 6 | 300 °C | sealed — bursts above 2 pressure | transparent: reveals what is inside |
| Frost Basin | 40 u | 20 | 70 °C | vented | cools to 2 °C; `open` |
| Fractionating Still | 30 u | 18 | 220 °C | condensed, fractionation 2.5 | `copper`; surface 0.8 |
| Evaporating Dish | 25 u | 5 | 220 °C | vented | surface 6: evaporates six times faster; `open` |

---

## 6. Measured procedures

**One preparation, three results over time** — 2 Spring Water + 3 Sunpetal, fire 35 % (→ 95 °C):

| Time | 0:20 | 0:45 | 1:00 | 1:30 | 2:00 | 3:00 | 6:00 | 10:00 | 15:00 |
|---|---|---|---|---|---|---|---|---|---|
| Result | Healing (Weak) 4 % | Healing (Weak) 31 % | **Healing 38 %** | Healing (Weak) 30 % | Vigour (Weak) 33 % | **Vigour 45 %** | Vigour (Weak) 32 % | Vigour (Weak) 14 % | **Spoiled** |

At 95 °C the latex that leaches out of the whole petals is destroyed as it comes (stable up to 85 °C), and the petal oil
evaporates and boils off with the steam.

**The same ingredients, another temperature** — fire 24 % (→ 72 °C, below the kindling's 75 °C): Healing (Weak) 29 %
at 1:00, 62 % at 2:00, **Potent 68 %** at 3:00, 69 % at 5:00 — the healing essence keeps. At 15:00 it is 67 %: the whole
petals have slowly leached 0.95 u of latex, which nothing destroys at 72 °C — steeping too long costs purity.

**Quantity** — at fire 24 %, 3:00–5:00: 1 Spring Water → **Superior 93 %**, 5 bottles; 2 → Potent 68 %, 7 bottles;
4 → Standard 40–41 %, 11 bottles.

### Grinding levels and evenness

2 Spring Water + 3 Sunpetal at fire 24 %; latex in the brew at 0:30, result at 3:00:

| Grind | Latex at 0:30 | Result at 3:00 |
|---|---|---|
| Whole | 0.02 u | **Healing Draught (Potent)** 68 % |
| Partially ground, even | 0.09 u | Healing Draught 63 % |
| Partially ground, uneven (0.3) — dust in it | 0.44 u | Healing Draught 63 % |
| Finely ground | all 1.8 u | **Tainted Healing Draught (Potent)** 69 % |
| Finely ground, uneven (0.3) — lumps in it | 1.44 u | Tainted Healing Draught (Potent) 70 % |
| Pulverised | all 1.8 u — and 0.33 u of the 1.5 u petal oil lost in the grinding (it warms) | Tainted Healing Draught (Potent) 68 % |

At 72 °C the slow part is the extraction reaction (sap → essence), not the release, so finer petals are not ready sooner
— they only bring the latex out. **Partially ground is the useful middle**: a little faster, still clean.
Ground and boiled: fire 50 % for the first 0:30 (→ 96 °C, the latex is destroyed: 1.8 → 0.15 u), then 24 % →
**Healing Draught** 41 % — clean, but the boil cooked 2.8 u of the healing essence into ardent essence.

Grinding pays where the release is the slow part: **Venom** (2 Grain Spirit + 2 Bitterroot, fire 18 % → 58 °C) is 53 %
at 0:40 and Potent 66 % at 1:00 ground, Weak 35 % at 0:40 and 65 % at 1:00 whole — but the ground roots give up all their
fungal pulp at once (2 u at 0:20, against 0.3 u whole). In the Frost Basin, ground Frostmint is no faster (the frost
settling is the slow part) and brings out 0.9 u of pulp against 0.25 u: Rime Draught 59 % at 1:00 instead of 62 %.
**Snakeweed** in water at fire 20 % (→ 64 °C): whole roots give an Antidote 48 % at 10:00 (the alkaloid is hard to
extract), ground ones **Potent 78 %**.

**Sunpetal Extract** ×3 instead: no latex at all → **Healing Draught (Potent)** 73 % at 2:00, 82 % at 3:00.

### Manual fire

2 Spring Water + 3 Sunpetal over **one log** (no burner): the flames reach 0.32 at 0:30 (67 °C, a swing of 20 °C while it
heats), 79 °C at 1:00 → Healing Draught 36 %; the log burns down — 67 °C at 2:00, 50 °C at 3:00, 37 °C at 4:00 —
and the brew keeps: Healing Draught 48 %. Two more logs: 96 °C within a minute; ten pumps of the bellows take the fire to
0.6. Left alone, the fire goes out and the vessel cools to 20 °C.

* **Two logs, air vent at 0.4**: the fire holds at about 0.17 — a steady 57 °C for three minutes, 52 °C at five →
  **Healing Draught 65 %**, clean: the vent is how a fire is held low.
* **One charcoal**: burns twice as long and hotter (96 °C at 1:00, 100 °C at 2:00) → the kindling turns it into a
  **Draught of Vigour** 53 % by 4:00, which keeps as the fire dies.
* **Pine resin** flares up and burns out fast: 54 °C at 1:00.

### Stirring

2 Spring Water + 3 Sunpetal at fire 24 %, stirred continuously from the start:

| Speed | Sap given up in 10 s | Agitation | Air beaten in | Result at 3:00 |
|---|---|---|---|---|
| none | 5.4 u | 0 | 0 | **Healing Draught (Potent)** 68 % |
| 0.5 (steady) | 10.7 u — twice as fast | 0.55 | 0.1 | Healing Draught 65 % |
| 1 (as hard as possible) | 10.3 u | 1 | 1 | **Tainted Healing Draught (Weak)** 23 % — the healing essence oxidised into sludge (2.75 u), and over 40 % of the brew slopped over the rim |

### Poor fire control

* **Scorching**: the same brew on full fire (heat 1), 1:30: left still, 1 u of char forms on the bottom → Draught of
  Vigour (Potent) 76 %, 59 % purity; stirred at 0.4, no char (77 %, 61 %); in a **water bath** the contents stay at 89 °C
  and nothing scorches — still a **Healing Draught** 37 %, with four times the water left.
* **Spirit flare**: 3 Grain Spirit + 1 Spring Water at fire 90 % in the open cauldron: the spirit fumes catch three times
  and erupt; at 2:00 nothing is left.
* **Overshooting**: 2 Spring Water + 3 Moonbloom at fire 15 % (→ 53 °C) is a **Mana Potion (Superior) 100 %** at 3:20 and
  still at 8:20. Turn the fire to 40 % at 3:20: at 100 °C the mana essence breaks down (above 85 °C) — 51 % at 4:20,
  **Plain Liquid** by 5:20 (10.7 → 0.8 u of mana essence). A water bath does not save it (it caps at 96 °C).

### Solubility

* **Too much salt**: 2 Spring Water (20 u) hold 7.2 u of brine — 2.4 Rock Salt. With 3 Rock Salt, 1.8 u stays
  undissolved; with 4, 4.8 u. Brought to 100 °C the water holds only 7.55 u: salt barely dissolves more when hot.
* **Salting out**: 2 Spring Water + 2 Rock Salt (all 6 u dissolved), then 3 Grain Spirit: the blend holds only 2.5 u, and
  3.5 u comes out of solution within seconds. Emptying the vessel gives back 1 Rock Salt.
* **Boiling down**: 1 Spring Water + 1 Rock Salt at fire 60 %: at 2:00, with 1 u of water left, 2.55 u has come out; dry
  at 4:00 — all 3 u of salt at the bottom, collected as a Rock Salt.
* **The wrong solvent**: 3 Lamp Oil + 3 Moonbloom, fire 15 %: oil holds almost no moon dew (10.3 u of it undissolved
  at 1:00), so the mana infusion crawls → Mana Potion (Weak) 7–11 %. The little mana essence that forms does not stay
  dissolved in oil either: after 10:00, 10 u of it lie at the bottom, collected as 2 Moon Crystals. Bitterroot in water
  instead of spirit: no venom at all (Cloudy Infusion — venom drawing needs spirit).
* **Too much water**: the Venom drawn above (Potent 71 %, 4.8 u in 12 u of spirit) + 3 Spring Water: venom barely
  dissolves in water — 2.25 u comes out of solution at once → Venom Extract (Weak) 13–15 %, 9 bottles.
* **An oil water will not hold**: in a 72 °C water brew, 0.35 u of the 1.5 u of sunpetal oil floats undissolved; in
  2 Lamp Oil it all dissolves (Healing Draught (Potent) 88 %: less solvent, a stronger draught).

### Crystals

* **Alum**: 2 Spring Water + 4 Alum Powder at fire 40 % — at 100 °C the water holds 36.6 u, so all 8 u dissolve; at
  20 °C it holds 1.1 u. Fire off at 2:00 and:
  * **left still** — the cooling is slow and nothing stirs: 6.9 u of **Alum Crystals** grow → emptying gives 3 Alum
    Crystals;
  * **stirred** at 0.5 — the same 6.9 u come out as a **powder** → 3 Alum Powder;
  * **in an ice bath** — crash-cooled to 0 °C: 7.2 u of powder and only 0.4 u of crystals.
* **Purifying crude alum**: 2 Spring Water + 2 Crude Alum (finely ground), fire 40 % for 2:00 → 3.2 u of alum dissolved,
  1 u of silt floating in it. **Filter it hot through paper** (the silt stays in the paper), let it cool still: 2 u of
  clean Alum Crystals grow (1.1 u stays dissolved) — emptying gives 1 Alum Crystals.
* **Supersaturation and seeding**: 1 Spring Water + 2 Saltpetre Powder, warmed then cooled still to 20 °C: the water now
  holds 3 u, but all 4 u stay dissolved — 1.35 times supersaturated, below the 1.6 it needs to start by itself — and it
  stays that way. **Drop in one Saltpetre Crystal** and 3 u of needles grow on it.
* **Moon crystals** (Frost Basin, cooling 80 %, 2 water + 2 Moonbloom + 2 Rock Salt, left still): 6.2 u in 2:00 by day,
  7.8 u at night (faster at the start: 6.7 against 4.0 u at 1:00); stirring every few seconds stops them forming. Empty the
  basin to harvest them.

### Evaporation and concentration

* **A wide dish**: 1 Seawater at fire 30 % never boils (78 °C in the dish, 86 °C in the cauldron) but evaporates: in
  10 minutes the dish loses 1.6 u of water, the cauldron 0.4 u.
* **Boiling seawater dry**: 2 Seawater in the Evaporating Dish at fire 60 %: the water boils at 100 °C, the salt starts
  coming out at 4:00, dry at about 5:30 — all 3 u of salt at the bottom → **1 Rock Salt**.
* **Boiling**: 20 u of water in the iron cauldron boils from 0:24 and is dry at about 7:10 at fire 50 %; at full fire it
  boils from 0:09 and is dry at 1:37 — then the vessel heats freely (what is left scorches above 170 °C).

### Fermentation and distillation

* **Mead**: 2 Spring Water + 2 Honey + 1 Brewer's Yeast, kept warm (fire 5 % → 31 °C): Cloudy Infusion at 1:00,
  **Honey Mead** 59 % at 5:00, Potent 79 % at 10:00, 86 % at 30:00 (2.9 u of spirit, 0.23 u of esters). Too warm
  (fire 15 % → 53 °C): the yeast dies above 42 °C — still a Cloudy Infusion of honey at 15:00.
* **Distilling it** (1 Spring Water + 1 Honey + 1 Yeast, fermented 25 minutes → Honey Mead (Potent) 84 %, then fire 45 %):

  | Flask after | 0:30 | 1:00 | 1:30 | 2:00 | 4:00 | 6:00 |
  |---|---|---|---|---|---|---|
  | **Copper Alembic** | 0.8 u, 74 % spirit | 1.7 u, 84 % | 2 u | 2.4 u | 4 u | 5.6 u — mostly water |
  | **Fractionating Still** | 0.6 u, 84 % spirit | 1.55 u, **91 %** | 1.8 u | 2.2 u | 3.8 u | 5.4 u |
  | Result | **Honey Brandy (Superior)** | Honey Brandy (Superior) | Superior | Superior | Honey Brandy | Honey Mead again |

  The spirit (78 °C) and esters (77 °C) come over first; once they are gone the flask fills with water. **Collect the
  flask in time** — the fraction is the product. The still's column sends the water back: almost none reaches the flask
  while there is spirit left to boil.
* **Lavender**: 2 Spring Water + 3 Lavender at fire 24 %: water barely holds the oil — 1.9 of 2.4 u float undissolved →
  Lavender Tincture (Weak) 9 %. In 2 Grain Spirit (fire 10 %): **Lavender Tincture (Potent)** 82 %. Steam-distilled (Copper
  Alembic, 1 Spring Water + 2 Lavender, fire 50 %): at 2:00 the flask holds 2.9 u with all 1.6 u of the oil → **Lavender
  Tincture (Superior) 100 %**.

### Acidity

* **Dreamshade** (2 Spring Water + 3 Berries, fire 14 % → 51 °C, 5:00): plain (pH 7) → Dreamless Sleep Draught (Weak) 25 %,
  1.05 u of somnine; **+1 Vinegar** (pH 4.7) → 1.8 u, 34 %; **+1 Potash** (pH 13.3) → the alkaloid does not survive (0.01 u):
  Cloudy Infusion.
* **Willow Bark** needs warm acid: 2 Spring Water + 1 Vinegar + 2 ground Willow Bark, fire 25 % (→ 74 °C, pH 4.4) →
  **Willowbark Draught** 40 % at 10:00, purity 31 % (its tannin); without vinegar, no willow essence at all (Cloudy
  Infusion). **Fining**: let it cool, add an Egg Yolk — its white binds 64 % of the tannin into lees → 46 %, purity 43 %.
* **Milk** + Vinegar (pH 2.7): 87 % of its curd comes out of solution at once.
* **Snakeweed** with vinegar: the whole roots give their alkaloid twice as fast (0.22 u at 5:00 against 0.11 u in water,
  and 1.1 u of antitoxin against 0.5 u), though the acid lowers the purity (Antidote 49 %).

### Layers and emulsions

* **Salamander Draught**: a healing brew (2 Spring Water + 3 Sunpetal, fire 24 %, 3:00), then 2 Lamp Oil + 4 ground Fire
  Peppers. The pepperfire oil stays in the oil layer, the healing essence in the water:
  * left still — they barely meet: 2.4 u of salamander essence in 5 minutes → still a Healing Draught (Weak) 29 %;
  * **stirred** at 0.5 (emulsified) → 8.2 u → **Salamander Draught** 41 %;
  * **an egg yolk, stirred for only 20 seconds** — the lecithin keeps the emulsion (0.55 when measured) → **45 %**;
  * Grain Spirit instead of oil — one layer, no stirring needed → 48 %.
* **Decanting**: 2 Spring Water + 2 Lamp Oil + 3 ground Lavender, fire 24 %: the oil floats with 2.3 u of lavender oil in it
  (it went where it dissolves). **Decant**: a **Lavender Tincture (Superior) 99 %**, 3 bottles; the water stays behind.
* **Soap**: 1 Spring Water + 1 Potash + 2 Lamp Oil, fire 35 % (96 °C): left still, the lye and the oil sit in different
  layers — 0.15 u of soap in 5 minutes (Plain Liquid, pH 13.6). **Stirred** at 0.5: 6.5 u → **Soft Soap** 54 %, and the
  first soap holds the rest together (emulsion 1).
* **A salve**: 2 Lamp Oil + 3 Sunpetal + 1 Beeswax, fire 24 %, 5:00 → **Healing Salve (Potent)** 79 %; cooled, 1.2 u of the
  wax sets out of the oil (80 %).

### Compatibility and contamination

* **Milk binds venom**: the Venom (Potent 71 %) + 2 Goat Milk → 2.9 u of the venom bound into curds → Venom Extract (Weak)
  20 %. **Holy Water** destroys it: Weak 32 % in 30 seconds.
* **Charcoal**: 2 Spring Water + 3 ground Sunpetal + 1 Charcoal, fire 24 %, 5:00: the charcoal binds all 1.8 u of the
  latex → **Healing Draught (Potent)** 72 %, purity 89 % (without it: Tainted, 73 % purity). The charcoal floats: **Skim** it
  off (1 Charcoal back).
* **Muddy water**: 2 Muddy Water + 3 Sunpetal, fire 24 %, 3:00 → **Murky Healing Draught (Potent)** 68 %. Filter it through
  paper → **Healing Draught (Potent)** 71 %, purity 83 %. With an Alum Powder from the start the silt settles (Healing
  Draught 61 % — clear, but the alum is an impurity of its own).
* **A dirty vessel**: emptying a spoiled brew leaves a film of 0.17 u of fire ash and 0.25 u of sludge, which the next
  preparation at that station starts with — unless the vessel is cleaned.

### Adding during the process — redirection by state

All at fire 35 %, 2 Spring Water + 3 Sunpetal:

* At 1:30 (vital essence dominant), fire to 10 %, add 3 Frostmint → **Calming Tonic** 43 % (type C).
* At 3:00 (ardent essence dominant), add 2 Frostmint → **Elixir of Fortitude (Potent)** 70 % at 5:00 (type D).
  At 5:00 (less ardent essence left): 2 Frostmint → 50 %, 3 Frostmint → 42 % — the extra frost oil is an impurity:
  proportions matter.
* Void Salt ×2 at 1:30 → **Stillwater Draught (Potent)** 69 %; at 5:00 → Stillwater Draught 36 % — the same result
  whatever the mixture was becoming (weaker, because less active compound was left).
* Lead Dust at 1:00 → still **Healing Draught** (44 %) at 15:00 (the kindling is blocked); Lead Dust from the start →
  Cloudy Infusion (nothing is ever extracted).
* Bitterroot at 2:00 into a healing brew at 72 °C → **Tainted Healing Draught** 40 % at 4:00.

**Catalysts and stabilisers:** a Quicksilver Drop at the start (fire 24 %) → Healing 58 % at 1:00 instead of 29 %. A Rock
Salt (fire 35 %) slows the whole chain: still Healing (40 %) at 2:00, Vigour from 5:00 and still Vigour (42 %) at 15:00,
when the unsalted brew is already spoiled.

### Vessels

* Moonbloom: 2 water + 3 Moonbloom, fire 15 % → Mana Potion 58 % at 1:00, Superior 95 % at 2:00 (iron cauldron). In the
  **Copper Alembic** (1 water + 2 Moonbloom) → Superior 92 % already at 1:00 (copper doubles the infusion).
* **Frost Basin**, cooling 80 % (→ 6 °C): 2 water + 3 Frostmint → Rime Draught 62 % at 1:00, Superior 96 % at 3:00.
* **Copper Alembic**: 1 Lamp Oil + 3 Sunpetal, fire 40 %; at 3:00 fire 80 %; at 3:40 (≈ 116 °C) add 2 Frostmint → the
  ember spirit boils into the flask: at 5:00 **Collect distillate** → **Quickstep Elixir (Potent)** 66 % × 2, and the vessel
  holds an Elixir of Fortitude (Potent) 79 %. Waiting longer is worse: at 4:00 (161 °C) 59 %, at 4:20 (175 °C) Weak 9 % —
  the sap has caramelised and the vital essence broken down. In an open iron cauldron the same steps lose the spirit to
  the air and leave a Spoiled Mixture.
* **Glass Retort** at full fire with 1 water + 1 Sunpetal: the vapour builds pressure and the retort **bursts** at 1:19
  (water alone fills it with 1.5 pressure of steam and holds).

**Unexpected reactions:** Embercap into warm Grain Spirit catches fire (*whoomp!*) and erupts; Quicksilver into hot mana
essence makes Unstable Aether, which above 95 °C erupts out of the vessel; **Blasting Powder** (mixed dry in the
Evaporating Dish: 1 Saltpetre Crystals + 2 Fire Ash + 1 Charcoal, stirred, makes 2.4 u in a minute → 1 Blasting Powder)
goes off twice in hot oil.
