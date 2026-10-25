# Alchemy 01 — Overview and Setup

**Scripts:** `Alchemy/Runtime/AlchemyStation.cs`, `Alchemy/UI/AlchemyStationUI.cs`, `Alchemy/UI/AlchemyCirclePad.cs`, `AlchemyTrace.cs`,
`Alchemy/Runtime/AlchemyStationVisuals.cs`, `AlchemyVesselParts.cs`, `AlchemyHandsOn.cs`, `Alchemy/Core/AlchemyLook.cs`,
`Alchemy/Runtime/AlchemyKnowledge.cs`, `Alchemy/Items/AlchemyIngredientSO.cs`, `Alchemy/Items/BrewedPotionSO.cs`,
`CustomEditor/Alchemy/AlchemyEditors.cs`.

---

## 1. Setup (two minutes)

1. *Tools ▸ SimpleMovements ▸ Alchemy ▸ Create Test Bench (free ingredients)*. It builds an open cauldron on three legs
   over a fire pit in the open scene (at the Scene view's centre) with an **Alchemy Station**, **Alchemy Surroundings**
   (weather and time of day) and **Alchemy Station Visuals** set to the cauldron's inside: in Play Mode the liquid,
   its layers, froth, sediment, floating chunks, bubbles, steam, smoke, the fuel on the fire, the wooden spoon, the
   bellows and the filter stand are built from the preparation's state (§5). On a test bench every built-in ingredient
   is a free supply and the window shows the numbers (quality, amounts, reactions), so you can brew and tune straight away.
2. Press Play, walk to it and press **Interact** (*[E] Brew*). The station window opens.
3. For a real game: *Create Alchemy Station* (no free supplies), then *Tools ▸ SimpleMovements ▸ Alchemy ▸ Create
   Ingredient Items* and put the ingredients in loot tables, merchants' stock or starting items. Players then brew with
   what they carry.
4. Optional, recommended:
   * *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Alchemy Database (Resources)* — a copy of the built-in rules you
     can edit (without it the built-in alchemy is used).
   * *Tools ▸ SimpleMovements ▸ Alchemy ▸ Create Potion Items* — an item asset for every quality band of every result.
     Without them, bottles are items made at runtime: fine in single player, but **network sessions and saves need
     assets** (then rebuild the Item Database).
   * *Create Equipment Presets* — the Iron Cauldron, Copper Alembic, Glass Retort, Frost Basin, Fractionating Still and
     Evaporating Dish as assets; put one in the station's *Equipment*.
   * Put **Firewood**, **Filter Paper** and **Charcoal** items where players find them when your stations have no woodpile
     or use paper and charcoal filters (*Create Ingredient Items* makes them too).

The station inspector lists problems (no collider, no water and no supplies, a manual fire without a woodpile, an unknown
filter...) and, in Play Mode, shows the preparation live: every substance, the reactions running and their rates, the
fire, the stirring, the pH and the layers, what it would be if bottled, with Stir, Pause, *Copy State JSON* and *Open In
Lab*.

---

## 2. How a player brews

```mermaid
flowchart LR
    W["Pour water<br/>(or another liquid)"] --> G["Grind ingredients<br/>(the mortar)"]
    G --> I["Add them<br/>(they give up their substances over time)"]
    I --> F["Tend the fire<br/>(fuel, bellows, air vent · baths)"]
    F --> T["Time passes:<br/>reactions transform<br/>the substances"]
    T -- "add more, stir (stirring minigame),<br/>feed or choke the fire" --> T
    T --> P["Process: filter, decant,<br/>skim, let it cool and crystallise"]
    P --> T
    T --> B["Bottle (all or part)"]
    T --> D["Collect the distillate<br/>(alembic, still)"]
    T --> E["Empty: harvest the solids,<br/>pour the liquid away"]
    B --> J["Journal: discovery,<br/>steps, identified compound"]
    D --> J
    E --> J
```

* **Liquid first.** Ingredients give up their substances into the liquid in the vessel; without liquid they barely do.
  Water comes from the station (*Water Source*) or from items (Spring Water, Grain Spirit, Lamp Oil, Turpentine,
  Vinegar...). The right solvent matters: oils and alkaloids want spirit or oil, salts want water — and oil and water
  form two layers.
* **Ingredients** come from the inventory (+1 / +5). Each is made of several substances that come out at their own pace
  (faster when hot, stirred, at the right pH), and its own temperature mixes in (Frostmint is ice cold, Ice Chips colder).
* **Grinding** (a station with a mortar): *Grind…* takes the ingredient to the **mortar** on the table (§6). It lies in
  the bowl as pieces: **click to pound** (it breaks the big pieces — best on hard roots and crystals, but blows alone never
  make a powder) and **hold the button and grind round the bowl** (it wears down what the pestle passes over — best on
  soft petals and leaves, down to dust). The pieces slide back to the bottom: work the whole bowl, or the missed ones stay
  lumps. What you see is what you get: **Whole → Partially ground → Finely ground → Pulverised**, even or with lumps
  among the dust (an uneven grind goes in partly coarser, partly finer); *Enter* tips it into the vessel. Finer gives
  everything up faster — the bitter, locked-in parts too — and pulverising lets volatile scents escape.
* **The fire** — at most stations a **real fire** (the fire minigame): put fuel on it (*+ Firewood*, *+ Charcoal*, *+ Pine
  Resin*, *+ Lamp Oil*: the logs, coals, resin and the dish of oil appear on the fire bed and burn down), **work the
  bellows** with your own hands (§6) to blow it up, and open or choke the **air vent** to hold it low. The flames
  rise and fall over seconds and burn the fuel down, so holding a temperature means tending it: the trace shows the last
  minute of the temperature and whether it is *held steady*, *wavering* or *swinging*. A regulated station has a burner
  instead (a heat slider: *Heat 35% → 97 °C*). Poor control has consequences: a roaring fire scorches the bottom of a
  vessel nobody stirs, spirit fumes flare over an open fire, overshooting destroys what is heat-sensitive.
* **Baths**: stand the vessel in a **water bath** (gentle, at most ~96 °C, nothing scorches) or an **ice bath** (fast
  cooling toward 0 °C: preserves what heat destroys, crash-cools into powders rather than crystals).
* **Stirring** — *Take the spoon…* and stir in the vessel itself (§6): hold the button and move the mouse in circles;
  how fast, how wide and how steadily you turn is the stirring, a reversal sloshes it unevenly, and a thick brew drags the
  spoon (stronger arms stir harder). The pad in the window still works too (1.5 turns a second is full speed), and the
  window says whether you stir *steadily*, *a little unevenly* or *erratically*. Stirring speeds extraction
  and brings reactants together, mixes oil and water into an emulsion, and stirs up the sediment; too hard beats air in
  (healing essences brown) and slops the brew over the rim. *Stir once* gives a single stir. Leave it still to let
  crystals grow and particles settle.
* **Time** is a real variable: the preparation keeps transforming continuously, even when the window is closed (*Run
  While Away*). A healing essence that forms after a minute may turn into something else after three, and into ash and
  sludge after ten.
* **Adding during the process**: what an ingredient does depends on what is in the vessel when it goes in — and on the
  pH (vinegar draws alkaloids out, curdles milk, destroys healing essence).
* **Process**: *Filter…* (cloth strainer, filter paper, charcoal bed — what each catches differs) is the **filtering
  minigame** (§6): pour the brew from a jug through the funnel, steadily — what runs over its edge goes through
  unfiltered. *Decant* (pour off the top layer: an oil from water), *Skim* (take what floats), *Clean* (scrub the residue
  film out of an empty vessel). What the filter or skimmer catches is collected — crystals, salts, charcoal.
* **Pause** stops time for the preparation (its state is kept exactly); **Fire off** chokes the fire (or turns the burner
  off) and lets it cool by the rules.
* **Bottle** takes the liquid out as bottles of what it is *now* — all of it, or one bottle: what is left keeps going.
  Only then does a workshop reveal what it was and how good ("Bottled 3 × Healing Draught (Potent) - quality 74 %
  (strength 80 %, purity 92 %)"), and every bottle keeps that strength and purity (§7). An optional *Bottle Item* (an
  empty vial) is used per bottle. Items that do not fit drop at the station.
* **Empty vessel** ends the preparation: solids at the bottom (crystals, ash...) are harvested, the liquid is poured
  away (the window asks first when it is worth something). A film of residue stays in the vessel unless it is cleaned.

---

## 3. The window

A **test bench** (*Test Bench* or *Free Default Ingredients* on) shows everything below. Anywhere else — a real
workshop — the window shows only what can be seen, smelt and measured with the equipment, and nothing it computes:

* **What you see** (instead of *In the vessel*): "The iron cauldron is about half full. A slightly hazy, deep green
  liquid. It has separated into two layers: an amber one floats on a pale blue one. It appears there is something afloat.
  Something seems to have settled at the bottom. Pieces of Sunpetal lie at the bottom and look mostly spent. Faint streaks
  of colour show it is not evenly mixed. A thin froth sits on top. It smells sharp and sour." — and when the colour shifts:
  "Its colour is slowly turning brown."
* **What it is doing**: the heat's signs (it shimmers, small bubbles rise, it is boiling hard, clouds of steam), too
  hot (a burnt smell, the colour darkens, acrid smoke), what the reactions look like ("the liquid blushes a deep rose"),
  slopping over, the fire burning low or smouldering when choked.
* **The brew** (instead of *If bottled now*): its colour and look only — "What it has become, and how good it is, shows
  once it is bottled." No outcome name, no quality bar, no strength or purity.
* The fill is in words, the fire's fuel too ("plenty of fuel", "the fuel is burning down"), there is no pH, no target
  temperature and no unit counts. The **temperature** is read off the thermometer (*Thermometer*, on by default); without
  one it is only "warm", "steaming hot", "about to boil". The notes never say what to do ("filter it"): only what is seen
  ("it looks cloudy: something seems to drift in it").

| Area | Shows (test bench) |
|---|---|
| Left — **Ingredients** | *Pour water*, the station's free supplies, then the alchemy ingredients in the inventory, with their form and what the player knows they contain ("Whole · Solar Sap, ?"); *Grind…*, *Add*, *Add 5* |
| Middle — status | Temperature with its trend (▲ ▼), time, fill, boiling, pressure (sealed vessels), pH, layers, stirring, pause, the surroundings' temperature and night |
| Middle — **On the fire** | Manual fire: the fuel buttons (how many the player carries), the flames and the fuel left, *Work the bellows…* (§6), the *Air vent* slider, the temperature trace (*held steady ±0.8 °C*, *cooling fast*), *Stand in*: no bath / *Water bath* / *Ice bath*. A burner: the *Heat* slider instead |
| Middle — **Stirring** | The stir pad (drag circles with the spoon), the speed and how steady, *Take the spoon…* (stir in 3D, §6), *Stir once*, *Pause / Resume*, *Fire off* |
| Middle — **In the vessel** | Every substance: its name if identified, else what it looks like ("a deep rose liquid that smells of summer"), units and share, saturated or supersaturated, and what is undissolved (floating, clouding or settled; crystals); the layers; ingredients still giving up their substances (form, "% given up"); vapour held; the distillate flask |
| Middle — **What is happening** | The reactions running (breakdown in the heat or the acid included): by name and rule once understood, else what is seen ("the rose darkens to amber..."); substances coming out of solution or crystallising |
| Right — **If bottled now** | The result *if the player has made it before* (else "Unknown result"), its quality with strength and purity, why ("Weak: diluted", "It looks cloudy", "It appears to sit in two layers", "It looks gritty"), how many bottles it fills |
| Right — buttons | *Bottle all*, *Bottle one*, *Collect distillate* (distilling vessels), *Empty vessel*; **Process**: *Filter…* (a choice of the filters at hand, with what they use up: the filtering minigame), *Decant*, *Skim*, *Clean* |
| Right — **Observations** | The log of the preparation in the player's words |
| **Journal** | Discoveries (with the steps of the best one), Reactions, Substances, Ingredients, Experiments ([04](04-Discovery-and-Journal.md)) |

The window is an open panel of the inventory (free cursor, clicks never attack); Escape closes the journal or
the confirmation, then the window. It closes when the player walks away (and nobody stirs a closed window: the stirring
stops).

---

## 4. Station settings

| Setting | |
|---|---|
| *Station Name* | Prompt and window title (empty = the vessel's name) |
| *Test Bench* | The window shows the result if bottled now with its quality, strength and purity, the amounts and the reactions by name (on with *Free Default Ingredients* too). Off: only what is seen (§3), and the result is known once bottled |
| *Thermometer* | The window shows the temperature in degrees and the temperature trace (off: only how hot it looks) |
| *Database* | The rules (empty = the project's Alchemy Database, else the built-in alchemy) |
| *Equipment* | The vessel ([02 §12](02-Simulation-Rules.md#12-vessels)); empty = an iron cauldron |
| *Interaction Point*, *Range* | Where the prompt is and how close the player must be |
| *Fire Control* | **Manual** (a real fire: fuel, bellows, air vent — the fire minigame) or **Regulated** (a burner: a heat slider) ([02 §6](02-Simulation-Rules.md#6-heat-the-fire-baths-boiling-and-evaporation)) |
| *Woodpile* | Free firewood at the station (off: players need Firewood items or other fuels) |
| *Has Mortar* | The *Grind…* button and the mortar on the table (§6) ([02 §3](02-Simulation-Rules.md#3-extraction-ingredients-forms-and-grinding)) |
| *Has Water Bath*, *Has Ice Bath* | The baths the vessel can stand in |
| *Filters* | The filters at hand (ids: `strainer`, `paper`, `charcoal`; empty = all of the database's) — a filter with a medium uses up that item |
| *Vessel Gets Dirty* | A residue film stays in the vessel when it is emptied ([02 §10](02-Simulation-Rules.md#10-compatibility-and-contamination)); players *Clean* it |
| *Water Source*, *Water Per Pour*, *Water Temperature* | A *Pour water* button (units per press, °C) |
| *Free Ingredients*, *Free Default Ingredients* | Supplies that cost nothing (a teaching lab, a test bench) |
| *Bottle Item* | An item used up per bottle (an empty vial); empty = free bottles |
| *Drop Point* | Where items that do not fit in the inventory drop |
| *Run While Away* | The preparation keeps transforming while nobody tends it (off: time stops when the window closes) |
| *Seed*, *New Seed Each Preparation* | The defined random variable ([02 §14](02-Simulation-Rules.md#14-reproducibility-and-randomness)) |
| *Conditions Interval*, *Max Steps Per Frame* | How often the surroundings are read; catch-up after a hitch |
| *Persist Preparation*, *Station Id* | Save the preparation in progress (and the vessel's residue film) and continue it after a restart ([02 §15](02-Simulation-Rules.md#15-interrupting-resuming-and-saving)) |

---

## 5. The vessel in 3D (Alchemy Station Visuals)

The preparation is drawn from its actual state (`AlchemyLook`), every frame, in the vessel:

| | |
|---|---|
| **Liquid** | Rises with what is in the vessel (and what still dissolves), takes the mixture's colour, see-through when clear, opaque when cloudy, murky or thick |
| **Layers** | Two layers lie one over the other, each its own colour; stirred, they swirl into one (the emulsion) and part again when left |
| **Froth** | Air beaten in by hard stirring, a hard boil with plant matter, an emulsifier stirred |
| **What floats** | Undissolved matter lighter than the liquid (charcoal, wax, ash...) bobs on the surface as chunks of its colour, drifts round with the swirl and is pushed aside by the spoon |
| **What sinks** | Settled matter and solids make a layer at the bottom; stirring lifts it into drifting specks (the cloud) |
| **Ingredients** | Whole herbs, roots and crystals: fresh plant matter bobs on top, then darkens, shrinks and sinks as it gives up its substances; minerals lie at the bottom. Poured liquids and pastes only streak the brew while they mix in |
| **Streaks** | Arcs of another colour curling round the surface: ingredients still giving up their substances into a still liquid, half-blended layers, erratic stirring |
| **Heat** | A shimmer and wisps of vapour, then small bubbles rising from the bottom, then a rolling boil that makes the surface tremble; steam with the boil |
| **Too hot** | Plant matter scorching past ~140 °C, a near-dry hot bottom, or what breaks down from heat: smoke and a burnt smell |
| **Reactions** | Bubbles, smoke, sparks, a flash, a hiss, a glow in the liquid's colour — as each reaction's *Visual* says |
| **The fire bed** | The fuel on the fire: logs, charcoal lumps, resin, a dish of lamp oil — the oldest burning away first, charring, embers glowing with the flames; flames and their light follow the fire; it smokes when choked |
| **Props** | The wooden spoon (resting against the rim, in the hand while stirring), the bellows beside a manual fire, the filter stand on the table (funnel, filter, flask, jug), the mortar and pestle (stations with a mortar) |

With **Auto Build** (on) all of it is made from primitives under the vessel; set *Vessel Space* (the transform the inner
size is measured in), *Inner Radius*, *Bottom Radius Scale*, *Belly Radius Scale* / *Belly At* (a round-bellied
cauldron) and *Empty Height* / *Full Height* (the inner bottom and the brim) to your vessel. *Liquid Material* gives the
liquid your own material; *Fire Bed*, *Bellows Point* and *Filter Point* place the fire and the props. Assign your own
*Liquid* renderer, particle systems and lights to use your art instead (*Mortar Point* places the mortar); Auto Build off keeps only those (the props are
still built when a hands-on tool is taken up). The open cauldron of *Create Alchemy Station* is saved as
`Items/Alchemy/Models/Cauldron Mesh.asset`.

> Stations made with *Create Alchemy Station* before this version use a closed cylinder as the cauldron, which hides
> the liquid: create the station again (or give it an open vessel and set the Visuals' inner size).

---

## 6. Hands on: the spoon, the bellows, the filter (Alchemy Hands On)

*Take the spoon…*, *Work the bellows…*, *Filter…* and *Grind…* put the window aside: the view moves to the vessel and the mouse
becomes the hand. **Right-click** (or **Q**) puts the tool down and brings the window back; **Escape** goes back to the
game. The preparation goes on exactly as it is, and the tool can be taken up again whenever you like. A small panel says
what you feel ("The spoon drags through it", "It runs over the edge of the filter!") and how tired your arms are.

| Tool | How | What it does |
|---|---|---|
| **Spoon** | Hold the left button to dip it, move the mouse in circles (let go to lift it out) | How fast, how wide (stirring near the middle does little) and how steadily it turns is the stirring. A reversal sloshes it (uneven stirring), a jerky hand too. A thick brew (syrup, paste, cold) drags the spoon behind the hand. Floating chunks are pushed aside, the swirl follows the spoon's direction |
| **Bellows** | Hold the left button: move the mouse down to squeeze, up to let them fill — or tap **Space** | Each squeeze is one pump: a full, quick squeeze blows hardest, a half one puffs. The flames leap with each pump |
| **Mortar** | Click (or **Space**) to pound; hold the left button and move to grind; **1–5** how many (before you start); **Enter** tips it into the vessel | A blow breaks the big pieces under the pestle (hard roots and crystals shatter, petals only bruise; never finer than *Finely ground*). Grinding wears down the pieces the head passes over and shoves them aside — soft matter fastest, down to dust. They slide back to the middle: sweep the whole bowl. The level reached is the fineness of the whole; the evenness is how alike the pieces are (lumps left among dust make an uneven grind). A test bench also shows the evenness in % |
| **Filter** | Hold the left button and raise the mouse to tilt the jug | The brew pours into the funnel and runs through the filter into the flask (paper slower than cloth, charcoal slowest, thick brews slower still). Too fast and the funnel overflows: what spills over the edge goes through unfiltered — the filter catches less and some liquid is lost (the journal says "a little hastily"). Right-click stops (it goes back in the vessel: nothing is filtered) |

The player's stats shape the hands (`CombatStats`; a character without them has average hands):

| Stat | |
|---|---|
| **Strength** | How hard the spoon stirs and pushes through a thick brew; how hard a squeeze of the bellows blows; how hard the pestle grinds and pounds |
| **Endurance** | How fast the arms tire: tired arms stir and pump weaker until they rest |
| **Agility** | How steady the hand is: the jug trembles less while pouring |

0 points make 0.7 × average hands, *Stat Reference* points (25) make 1.1 ×, approaching 1.5 ×. The stats, the views
(*Stir View*, *Bellows View*, *Filter View*, *Mortar View*: fixed camera poses, else placed by the vessel) and the feel (*Turns For Full
Stir*, *Funnel Seconds*) are on the **Alchemy Hands On** component (added to the station when first used; add it yourself
to set them). Everything reaches the preparation as the window's requests do (*SetStirring*, *Bellows*, *Filter* with
its care), so the rules, the history, replays and saves are the simulation's.

---

## 7. Bottles and their strength

Every bottle keeps the strength and purity it was bottled at (in its copy's durability list: it survives stacking,
dropping, storage, trading and saves). Drinking it applies the effects **at that strength**, smoothly — not only by
quality band: each band's *Effect Multiplier* holds at its *Min Quality* and the strength runs on toward the next band's,
so a 68 % and an 84 % Potent Healing Draught heal ×1.56 and ×1.88; a brew stronger than full strength (strength over
100 %) gets up to a quarter more. The tooltip shows the bottle's own quality and effects ("This bottle · quality 84 %
(strength 88 %, purity 96 %)"). Bottles made before this version keep their asset's effects.

---

## 8. Your own vessel (no code)

**Equipment assets are the vessel's physics, not its model.** An *Alchemy Equipment* asset (the presets in
`Resources/SimpleMovements/Alchemy/Equipment`: Iron Cauldron, Copper Alembic, Glass Retort, Frost Basin, Fractionating
Still, Evaporating Dish) says how the vessel behaves — capacity, how fast it heats and cools, what it does with vapour
(open, distils into a flask, sealed), its burst pressure, whether it is transparent, its tags — and nothing about how it
looks, so any model can be any vessel. *Create Alchemy Station* builds one model (the open iron cauldron); for the
others, use your own model and pick the preset (or make an Equipment asset of your own).

To make a station from your own cauldron (or alembic, retort, basin) model:

1. **The model** must be open at the top (a hollow inside): a closed mesh hides the brew. Any mesh, any material.
2. **The station object**: an empty GameObject on the floor (*My Cauldron*), your model as its child. Give it a
   **collider** (a Box Collider around the model is enough): the player's interactor finds the station by it.
3. *Add Component ▸ SimpleMovements ▸ Alchemy ▸* **Alchemy Station** on the root. Set *Equipment* (a preset, or *Assets ▸
   Create ▸ SimpleMovements ▸ Alchemy ▸ Equipment* for your own capacity and behaviour), *Fire Control* (a real fire or a
   burner), *Woodpile*, *Has Mortar*, the baths, *Filters*, *Water Source*, *Test Bench* while you tune it, *Thermometer*.
   **Alchemy Surroundings** is added by itself (weather and time of day).
4. *Add Component ▸* **Alchemy Station Visuals** on the root, and fit it to the inside of your model — select the object:
   the Scene view draws the inside as it is set (blue rings at the inner bottom, the belly and the brim, the fire bed in
   orange):
   * *Vessel Space*: the root (heights and radii are then in its local units; leave the root unscaled);
   * *Empty Height*: the inner bottom; *Full Height*: where the brim's fill line is;
   * *Inner Radius*: the inner radius at the brim; *Bottom Radius Scale*: the inner bottom's as a share of it; for a
     round belly, *Belly Radius Scale* (more than 1 when wider than the brim) and *Belly At* (0 bottom – 1 brim);
   * *Fire Bed*: an empty child where the fire burns (under the vessel); *Bellows Point*, *Filter Point*, *Mortar Point*:
     empty children where the bellows, the filter stand and the mortar stand (optional: they are placed beside the vessel);
   * optionally *Liquid Material* (your own see-through liquid), and your own particle systems, lights and sounds.
5. Optionally **Alchemy Hands On** (it is added when first used): fixed views for the tools, and which stats shape the
   hands.
6. Press Play, brew at it (a test bench has every ingredient free), and adjust the rings until the liquid sits in the
   bowl. Drag the root into the Project window to make it a **prefab** and place it wherever you like.
