# 01 — Overview and Setup

[← Agriculture](README.md)

## Setup in two minutes

1. *(Optional)* **Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Agriculture Database (Resources)** — the project's
   rules and content at `Resources/SimpleMovements/Settings/AgricultureDatabase`. Without one the built-in rules, the
   nineteen built-in species and the twenty-four soil materials are used.
2. Open the **City** scene (the hub world's permanent scene: fields elsewhere are rebuilt with their scene every cycle).
3. **Tools ▸ SimpleMovements ▸ Agriculture ▸ Create Soil Field** — an 8 × 6 m plot of organic-rich soil at the Scene
   view's pivot, on the ground. Or select a garden bed, a plot mesh or a patch of ground and use **Create Soil Field Over
   Selected Object**: the field takes its footprint and its turn.
4. Shape it and say what its soil is in the **Soil Field** inspector ([chapter 02](02-Defining-Soil-Areas.md)).
5. *(Optional)* **Agriculture ▸ Create Water Source ▸ Well** (or a rain barrel, a river bank, a spring, an alchemical
   font...) next to it, and **Agriculture ▸ Create Growing Structure ▸ Greenhouse** with the field selected to cover it.
6. Press Play, walk up to the soil and **Interact** (the prompt says *Tend*). In Play mode the Soil Field inspector has
   **Starter kit** (seeds, tools, instruments, a watering can and some materials for the local player) and buttons to
   skip six hours, a day or a week.

For multiplayer select the field and use **Agriculture ▸ Make Selected Field Networked** (a NetworkObject and a
NetSoilField; [chapter 07](07-Scripting-Editor-Multiplayer.md)).

## The field window

Interacting opens the field window (an exclusive panel of the player's inventory; walking away closes it). Nothing in
it says what a plant needs or what is wrong with it — only what is seen and what instruments read.

* **Left — the spot**: what the player sees there (the soil, the plant, its fruit), the last instrument reading with
  **Write it down** (into the notebook), and the plant's **label**.
* **Middle — the work surface**: **Bed** (the soil around the player from above: click a spot or a plant), **Plant**
  (the chosen plant up close: click a fruit, a flower, a leaf to pick that one), **Soil** (a cut through the soil at the
  spot: the hole as deep as it was dug — with a ruler only when a depth gauge is in the bag).
* **Right — the work**: everything the player can do with what they carry.

| Work | What it takes | What it does |
|---|---|---|
| **Look** | — | Describes the soil and the plant at the spot (sharper with Botany and a magnifying lens) |
| **Till** | a hoe (a shovel, a trowel) | Loosens the soil (tilth) and eases its packing, works in what lies on top (mulch, materials), fills old holes; heavy clay and stony ground are harder work |
| **Clear stones** | hands | Picks out stones |
| **Dig (hold)** | a trowel, dibber, shovel or bare hands | A hole as deep as the hold was long and the tool digs |
| **Sow here** | seeds (pick them in the list) | Drops one seed into the hole (or onto the surface) |
| **Cover** | hands | Pushes the soil back and firms it (some seeds want light: leave those uncovered) |
| **Pour (hold)** | a watering can, bucket or flask with water | Pours as long as held; how much soaks in depends on the soil, the rest runs off; the water's kind goes into the soil |
| **Put on the soil** | a material (pick it), *portions*, a *method*: spread wide, around the plant, one spot | Spreads a fertilizer, amendment or mulch; it works in with tilling, water and time |
| **Treat the plant** | a treatment (pesticide, spray, fungicide...) | Treats the plant at the spot |
| **Measure** | an instrument (pick it) | Reads the soil, the air, the light, the plant ([chapter 05](05-Knowledge-Instruments-and-Skills.md)) |
| **Prune** | pruning shears (a sickle) | Dead wood, diseased parts, thinning, shaping, cutting back, pinching out |
| **Take pollen / Pollinate** | a pollination brush | Carries pollen from one flower to another (crosses: [chapter 04](04-Plants.md)) |
| **Pick it / Pick everything** | hands (shears, a sickle) | Picks the chosen piece, or every ripe one |
| **Harvest the plant** | hands, a sickle | Takes a whole-plant crop (roots, cabbages, grain) |
| **Save seeds** | hands, tweezers help | Seeds from ripe produce, each carrying its plant's genes |
| **Stake** | garden stakes | Supports a climber or a heavy cropper |
| **Pick off pests / Pull up** | tweezers help | Removes the pests you can see / uproots the plant |
| **Mark** | — | Writes a label on the plant |

**Planting by hand** is the design's sequence: work the soil (Till), dig a hole of the right depth (Dig), drop the seed
(Sow), cover it (Cover), water it (Pour) — each step judged by what the player did: the depth, the spacing to the next
plant, the row, the hour (some seeds come up better sown at night), the moon.

The **Notebook** and **Skills** buttons open the player's notes (what they wrote down, with the date and the field) and
their seven agricultural skills.

## The components

| Component | Where | What it does |
|---|---|---|
| **Soil Field** | on the ground (one per piece of soil) | The soil, its layout, its plants, saving, interaction, drawing |
| **Water Source** | a well, a barrel, a bank | Interact (*Fill*) to fill the container in hand or the emptiest carried; a rain barrel fills with the rain on its roof |
| **Growing Structure** | over (part of) a field | A box that changes rain, drying, light, warmth and frost for the cells inside |
| **NetSoilField** | next to a NetworkObject on the field | Multiplayer (Fish-Net) |
| Farm Director | made by itself | Updates every field and plant within a frame budget |
| Agri Knowledge | added to the player by itself | The player's skills, notes and the pollen on their brush |

The **farm clock** follows the Day Night Cycle's hour (only forwards: sleeping skips time and the plants catch up);
without one it runs by itself (*Day Length Minutes*). The calendar has four seasons of twelve days and a lunar cycle of
eight days by default ([chapter 03](03-Soil-Water-and-Weather.md)).

## Saving

Where the rules run (offline, or the server), under `persistentDataPath/Agriculture/`:

* `Fields/<scene>_<field id>.bin` — each field's soil (every cell), plants and weather history, compressed; saved every
  *Autosave Minutes* when changed and when the game quits. A field whose layout changed in the editor starts its soil
  afresh from the new layout (its plants are kept).
* `Skills/<player>.json`, `Notebook/<player>.json` — each player's skills and notes.
* `clock.json` — the farm clock.

**Forget its save** in the Soil Field inspector (Play mode) or the context menu **Agriculture ▸ Forget The Saved Field**
starts a field afresh.
