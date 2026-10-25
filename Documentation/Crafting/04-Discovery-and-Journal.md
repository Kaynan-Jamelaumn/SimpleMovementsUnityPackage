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

A test station has all of them and adds the numbers behind everything.

## How the player learns

| Learnt | How |
|---|---|
| A metal's name | working it (striking, grinding, filing) on `workingsToRecognise` visits, or one assay |
| An element | an assay that shows it |
| An ore's name | smelting it once, or handling (cleaning) it often, or an assay; each deposit smelted is noted with what it gave |
| A heat colour's meaning | reading a pyrometer while the colour shows, `readingsToLearnHeat` times |
| Quenching | every quench of a metal in a medium is remembered: hardened, stayed soft, cracked |
| Insights | mistakes and surprises, each explained once: burning, a quench crack, untempered brittleness, temper colours, the magnet, cold forging, cast iron, slag in a bloom, scale pits, grinding heat, a skating file, missing flux, sulfur, phosphorus, cupellation, cementation, case-hardened leather, rot, alum washing out, shrinking, stiff leather, blotchy dye, warping, coarse grain, a loose joint |
| What was made | every finished item with its tier; the best of each design is kept with the steps that made it (its pieces' records) |

Practice also steadies the hands: the more items made and insights had, the less the hand-skill models tremble.

## Final inspection

A finished item is taken off the bench **untried** at a normal station: its tooltip says how good it is will show in
use or at a bench. **Try it (inspect)** — it is flexed, struck, its edge tried, its straps pulled — and its quality,
tier, material, workmanship, soundness, edge, heat treatment, assembly and protection are known, here and on the item.
A test station's items come out already tried.

## Failure is a result

A cracked blade, a burnt bar, a short casting, a spongy bloom, a case-hardened hide, a split haft are what they are:
they can still be finished, reheated, ground out, cut down, recast (a casting or scrap remelts in the crucible), or
thrown away — and the journal remembers what caused them.
