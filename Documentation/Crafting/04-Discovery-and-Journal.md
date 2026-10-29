# 04 — Discovery and the Journal

Nothing is revealed up front. The window describes what a smith would see, hear and feel, through the player's
**journal** (`CraftKnowledge` on the player, saved as `persistentDataPath/Crafting/Journal/<character>.json`).

## What the player sees

`CraftLook` turns a piece's real state into words that describe and never advise:

* **Metals** are named once known — otherwise they are what they look like ("a grey metal that throws a bushy shower
  of stars", "a golden-brown metal"). Iron and copper are always known.
* **Heat** is a colour (black heat, faint blood red, cherry red, orange, yellow, white throwing sparks), earlier in the
  dark — with its degrees only if the player has learnt what that colour means, or with a pyrometer.
* **The surface**: scale, polish, scratches, hammer marks, temper colours, an etched pattern or hardening line, a coat.
* **The shape**: bent, uneven, dished, an eye; how close it is to the part it is meant to be.
* **The edge**: light glinting along a dull edge, keen enough to catch a fingernail, a wire burr, nicks, uneven bevels.
* **In the hand**: heavy or light for its size, humming (magic), warm (fire), a faint frost.
* **Flaws** that show (cracks, cold shuts, slag, porosity, burnt metal, scale pits, an overground edge, a pale raw core
  on a cut edge of case-hardened leather, rot, tears…) — hairline ones only under a loupe.
* **Ores**: colour, lustre, density, fragments that are rich or barren, dirt, roasting; a magnet's pull; the streak.
* **Hides**: flesh left, hair, how far tanned, sodden or dry, stiff or supple, water beading, patchy dye, shrinking.
* **The workshop**: how the fire burns and how much fuel is left, slag labouring the draft, the melt sluggish or bright
  or glaring, dross, a flux cover, streaks of unmixed metal, the tank warm or steaming, the vats' strength and heat.
* **Sound**: a clear ring, a dull thud, a flat dead blow, something tearing; a ping from the tank.

## Instruments

| Instrument | What it adds |
|---|---|
| Thermometer (pyrometer) | the temperature in degrees (and teaches what the colours mean) |
| Calipers | length, width, thickness in mm; how far a part is from its pattern |
| Scale | the mass in grams |
| Loupe | hairline cracks, inclusions and other faint flaws |
| Magnet | whether steel still pulls — it stops at its hardening heat |
| Test file | skates, barely bites, bites with effort, bites easily — roughly how hard |
| Assay kit | what a metal or ore is made of; names it at once |
| Streak plate | an ore's streak colour |
| Refractometer | a gem's refractive index (a spot reading on the rough, less exact) |
| Hardness points | scratch tests: which points scratch a stone and which do not |
| Dichroscope | whether a gem's colour changes with direction, and where its axis lies |
| Moisture meter | wood's moisture in percent (without it: heavy and cold, damp, dry and light) |

A test station has all of them and adds the numbers behind everything.

## How the player learns

| Learnt | How |
|---|---|
| A metal's name | working it (striking, grinding, filing) on `workingsToRecognise` visits, or one assay |
| An element | an assay that shows it |
| An ore's name | smelting it once, or handling (cleaning) it often, or an assay; each deposit smelted is noted with what it gave |
| A heat colour's meaning | reading a pyrometer while the colour shows, `readingsToLearnHeat` times |
| A gemstone | naming it rightly after testing it (the journal's `gems`): from then on its rough is known by sight, its description and readings are shown, and the cutting chart for its refractive index |
| Quenching | every quench of a metal in a medium is remembered: hardened, stayed soft, cracked |
| Insights | mistakes and surprises, each explained once: burning, a quench crack, untempered brittleness, temper colours, the magnet, cold forging, cast iron, slag in a bloom, scale pits, grinding heat, a skating file, missing flux, sulfur, phosphorus, cupellation, cementation, case-hardened leather, rot, alum washing out, shrinking, stiff leather, blotchy dye, warping, coarse grain, a loose joint, a gouging drawknife, skipped grits, felted wool, carding neps, a shattered cleave, a table flaking on its cleavage, a heat-crazed stone, a compound too soft, a pavilion's angle, a loose stone in a wide seat |
| What was made | every finished item with its tier; the best of each design is kept with the steps that made it (its pieces' records) |

## Practice: mastery, craft by craft

The journal also keeps how practised the player is in each craft — **Smithing**, **Leatherwork**, **Woodwork**,
**Tailoring**, **Jewellery** and **Alchemy** (`CraftMastery`). A master smith is still a novice at the loom.

**How it is earned.** Every finished item teaches its craft: `perItem` × (0.5 + its quality) × how much work the design
is (more parts and joints, more experience). Work done teaches too, a little at a time — a blow or a stroke very little,
a bobbin spun, a casting poured or a length of cloth woven more — but small work counts no faster than `workPerMinute`
a minute in one craft, so a thousand idle blows teach no more than a few minutes of real work. Each insight counts for
its craft (`perInsight`). Work done *by routine* (below) teaches nothing. A finished item always counts.

Mastery is `1 − e^(−experience / experienceScale)`: quick at first, then every step is harder won. With the default
settings:

| Rank | Mastery | Experience | Roughly (rings of 60 % quality alone; the work on them comes on top) |
|---|---|---|---|
| Novice | 0 | 0 | — |
| Apprentice | 15 % | 7 | four or five rings, or half an hour of steady work |
| Journeyman | 35 % | 19 | a dozen rings |
| Expert | 55 % | 36 | twenty rings |
| Master | 78 % | 68 | forty rings |

`experienceScale` sets the pace: double it and every rank takes twice the work. The ranks' thresholds are settings
too (*Crafting Database → Simulation → Mastery*).

Which craft a piece of work belongs to is decided where the rules run, from the work itself: blows, quenches, the
crucible and grinding steel are smithing; the tannery and cutting leather are leatherwork; the woodshop, carving and
grinding wood are woodwork; the fibre bench, spinning, weaving, cutting cloth and the tape are tailoring; the lapidary,
the ring sizer and the jeweller's bench are jewellery; work on an item (fitting, joints, finishing) is the item's craft
— jewellery for jewellery, otherwise the craft of the parts that matter most in it. Pumping the bellows or waiting is
nobody's craft. The brewing station's practice is alchemy.

**What practice gives, in its own craft.**

* **Steadier hands** — less tremble, fewer slips, the window's pace nearer the real tool's (60 % → 90 %), the timing
  bars' zones wider (`skillWidening`).
* **An arm that tires less** — up to `fatigueRelief` (40 %) less for a master.
* **More done with each action** — the needle lays 2 stitches a click from Journeyman, 3 for a master, and a needleful
  of thread lasts 40 stitches for a novice, up to 80; the loom beats in 2 rows a beat from Journeyman, 3 for a master;
  the awl's stitches likewise (the pricking iron always punches its 4 holes a beat); a tier of facets is cut by hand
  6 at a time for a novice, 5 / 4 / 3 for a journeyman / expert / master, the rest following the same setting.
* **Routines** — the tedious steps done in one action at the quality practice gives (`routineLow` at the rank that
  allows it, rising to `routineHigh` at full mastery: never a careful master's best by hand):

  | Routine | Craft | Rank |
  |---|---|---|
  | Wind and count a skein | Tailoring | Apprentice |
  | Measure the wearer | Tailoring | Apprentice |
  | Size the finger | Jewellery | Apprentice |
  | Pick the burrs out | Tailoring | Journeyman |
  | Tease the locks open | Tailoring | Journeyman |
  | Clean the crust off a rough stone | Jewellery | Journeyman |
  | Search a stone under the loupe | Jewellery | Expert |

  Threading the needle is quicker too: an apprentice tailor threads it at once (*Thread it (practised)*).
* **A master's finishing touches** — the finisher's mastery is recorded with the item, and its quality gains
  `finishingTouch` × mastery of what it still lacks (12 % at full mastery: a 0.50 item finishes at 0.56); an expert's or
  a master's work says so.
* **Alchemy** — a steadier pestle, spoon and jug; a cleaner decant (the purity gains the finishing touch, the potion's
  quality with it; its tier is what the brew was); and *intuition*: from Journeyman the verdict names what a brew made
  before is becoming, from Expert roughly how good it will be.

A new rank is announced with what it allows (*"You are a journeyman tailor now. 2 stitches a click; …"*). The
journal's **Mastery** page shows each craft's title, how far it is to the next rank, the items finished and everything
practice gives now; the window's subtitle shows the practice of the craft being worked.

A journal from before mastery turns its work into practice once, when it is loaded: each design made counts as made
(at a little under its best quality), each insight for its craft, each gemstone named for jewellery.

## Final inspection

A finished item is taken off the bench **untried** at a normal station: its tooltip says how good it is will show in
use or at a bench. **Try it (inspect)** — it is flexed, struck, its edge tried, its straps pulled — and its quality,
tier, material, workmanship, soundness, edge, heat treatment, assembly and protection are known, here and on the item.
A test station's items come out already tried.

## Failure is a result

A cracked blade, a burnt bar, a short casting, a spongy bloom, a case-hardened hide, a split haft are what they are:
they can still be finished, reheated, ground out, cut down, recast (a casting or scrap remelts in the crucible), or
thrown away — and the journal remembers what caused them.
