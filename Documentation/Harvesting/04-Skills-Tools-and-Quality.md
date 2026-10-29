# 04 — Skills, Tools and Quality

## Skills

Seven skills, each trained by doing its work (0 to 50 by default). They are kept per player by a **Harvest Knowledge**
component (added the first time the player harvests), saved to `persistentDataPath/Harvesting/Skills/<character>.json`
— in a network session on the server, which sends each player their own.

| Skill | Trained by | What it does |
|---|---|---|
| **Skinning** | hides, pelts, skins, feathers, scales, membranes | Wider pull band, fewer nicks, finer hides |
| **Butchering** | cuts of meat, fat | More meat and better meat, less waste (up to +25% yield), joints shown along the seams |
| **Anatomy** | a share (30%) of every harvest, and more for a species new to the player | Reads the wounds, reveals rare finds, finer work on everything |
| **Bone Harvesting** | bones and skulls | A wider beat for the saw, less gouging |
| **Tendon Extraction** | tendons and sinew | Feels the catch coming, more strain before a snap |
| **Organ Harvesting** | organs, glands, sacs, stones | A steadier knife, glands that survive a slip |
| **Trophy Extraction** | claws, teeth, horns, antlers, scales, plates | Pieces come loose faster with less strain |

Every level widens the margins of its work, steadies the hands, adds a little quality (up to +6% at the top) and a
little yield (+10%; Butchering +25%). **Techniques** come at set levels:

| Skill | Level | Technique | |
|---|---|---|---|
| Skinning | 10 | Steady pull | The hide separates over a wider band of pull. |
| | 25 | Fine work | Faces, paws and tails are no harder to skin than the back. |
| | 40 | Flawless pelts | Half as many nicks; hides come off a little finer. |
| Butchering | 10 | Seam sense | You see the joints along a seam, and can cut through them faster. |
| | 25 | Prime cuts | More meat from every cut, and better meat. |
| | 40 | No waste | Even a hurried cut leaves little behind. |
| Anatomy | 5 | Read the body | You can tell what every wound did inside: which organ, which bone. |
| | 15 | Keen eye | Rare things hidden in a body are easier to notice (a third less Anatomy needed). |
| | 30 | Hunter's knowledge | You know where creatures are weak, and everything you take is a little finer. |
| Bone Harvesting | 10 | Even strokes | The saw keeps its rhythm over a wider beat. |
| | 30 | Clean bones | Scraping hardly ever gouges a bone. |
| Tendon Extraction | 10 | Gentle draw | You feel a tendon catch before it does. |
| | 30 | Whole sinew | Tendons stand far more strain before they snap. |
| Organ Harvesting | 10 | Steady hands | The knife barely trembles in delicate work. |
| | 30 | Gland care | Fragile glands and sacs survive a slip. |
| Trophy Extraction | 10 | Feel the root | Claws and teeth work loose faster. |
| | 30 | Light touch | Prying strains them far less. |

Skills **never do the work**: a master's margins are wider and their hands steadier, but they still have to follow the
line, keep the pull in the band and saw in rhythm.

**Experience.** Each piece gives its material's *XP*, more for difficult materials, less for botched work — but never
less than 35% (*Failure Share*: mistakes teach too). Anatomy gets 30% of it (*Anatomy Share*), and 10 more the first
time a species is harvested. A quarter of it (*Character Xp Share*) goes to the character's own experience. Levels need
20 xp for the first and 8% more for each after (*Xp First Level*, *Xp Growth*).

**Species.** Each player keeps a note per species: harvests, best quality, the materials they have taken. The window
shows what a material is worth once the player has taken it from that species, and what every one is worth after three
harvests of the species: half of *Harvests To Know Species* (6), when it is fully known.

**Rare finds** (a bezoar, a musk pod, a heartstone, an egg sac) are only in some creatures (their *Chance*, rolled once
per creature; elites have them twice as often, old ones ×1.2, ancient ones ×1.5, and the creature's *Rare Finds*
multiplies it too). A player sees one only with enough Anatomy (its *Reveal
Skill*; *Keen Eye* lowers it by a third) or after taking one from that species before; until then it is *Something
unusual*.

## Tools

The harvest uses, for each stage, the **fittest tool the player carries** — in the bag, the hotbar or in hand — and
says which, and how fit it is: *made for it*, *good*, *makeshift*, *the wrong tool*, *useless*.

| Tool (Create Tools) | Made for | Also | Uses |
|---|---|---|---|
| Skinning knife | skinning | butchering, fine work (0.45), scraping | 80 |
| Butcher's knife | butchering | skinning (0.4), chopping | 90 |
| Cleaver | chopping (joints, small bones) | butchering (0.6), sawing (0.35) | 120 |
| Hunting knife | — (all-round) | skinning 0.75, butchering 0.7, fine work 0.55 | 70 |
| Lancet | fine work (organs, glands, tendons) | skinning (0.3) | 50 |
| Bone saw | sawing (bones, horns, antlers) | butchering, chopping | 120 |
| Fleshing scraper | scraping (bones clean, fat, scales) | — | 150 |
| Pliers | gripping and prying (teeth, claws, scales; drawing tendons) | fine work | 200 |

* **Fitness** = what the tool is made for × its **edge** (for cutting work: its durability left — a dull knife is
  makeshift) × how well it is made. A better tool widens the margins and works faster; the tool itself also counts a
  little in the result (*Tool Weight*, 12%).
* **Wrong tool**: below 0.3 fitness (*Wrong Tool Below*) the piece comes out *roughly cut*.
* **No blade**: work that needs one (opening cuts, peeling, cutting free, sawing) cannot start without a blade. Bare
  hands can pull and pry a little (fitness 0.2), and draw organs out.
* **Weapons as tools**: a dagger skins (0.6) and butchers (0.5), a throwing knife less, a sword butchers (0.45), an axe
  chops (0.9) — all makeshift. Crafted weapons count their quality.
* **Wear**: each harvest wears every tool it used by one use of its durability (the sharpest copy of it). Repair them
  as any item.

Tool assets are ordinary items (`HarvestToolSO`: *Assets ▸ Create ▸ SimpleMovements ▸ Harvesting ▸ Harvesting Tool*):
set their *Fit* (0-1 for each kind of work: skinning, butchering, fine, saw, scraper, pliers, chopping), *Quality*,
*Speed* and durability.

## Quality

Every piece gets a **quality** (0-100%) and a **grade**: *Ruined* (under 15%), *Poor*, *Fair*, *Good* (55%+), *Fine*
(75%+), *Pristine* (90%+). It follows from:

| Factor | How it counts |
|---|---|
| **The fight** | The piece's condition after the fight (a cracked claw, a pierced heart, a hide full of holes). Under 8% (*Destroyed Below*) it is destroyed and cannot be taken. |
| **The hands** | Each stage's precision and the harm done (tears, nicks, splinters, cracks, spills); for meat, how much was left behind. |
| **Freshness** | Quality × (1 − 0.75 × staleness) (*Freshness Weight*). |
| **The tools** | Their fitness (a little directly, mostly through the work's margins); the wrong tool makes it *rough*. |
| **The skill** | Up to +6% at the top (*Skill Quality Bonus*), +2% with Hunter's Knowledge, +5% on hides with Flawless Pelts. |
| **The creature** | Age (young: tender meat, small trophies; old and ancient: tougher meat, finer horns and claws), elites +4%. |
| **Soiling, poison, taint, burns** | Meat and organs from a soiled cavity (a torn gut) or a poisoned, tainted or charred region are worse, and marked so; poisoned ones are *Poor* at best. |

A hide's quality is the condition of all its cells (holes, burns, tears, nicks) and how cleanly it was opened. The flesh
left on it does not lower its grade: it goes with it to the tannery, where it has to be fleshed off on the beam.

**Yield** (how many pieces of a bulk material — meat, fat, feathers): the material's yield × the creature's size (steeply:
size^2.4; a young one ×0.7, an old one ×1.05, an ancient one ×1.15) × the condition of its meat × how much was taken ×
the skill's bonus. Pieces under 2% quality (*Discard Below*) are thrown away.

**Each copy keeps its own quality and marks** (in its durability value, like a potion's strength): a stack of claws
remembers which are pristine and which cracked. Merchants pay for each by its quality (from 10% of the price for a
ruined piece to the full price for a perfect one), and the tooltip tells its story: "*Fine (82%). Pierced in the fight;
nicked while skinning.*"

## Spoiling

A material with *Spoil Minutes* spoils: that many real minutes at 20 °C, twice as fast for every 10 °C warmer, half as
fast for every 10 °C colder, not at all at 0 °C or below (*Doubling Degrees*, *Freezing Point*). The temperature is the
Weather System's where the carcass lies. Bones, claws, horns and teeth never spoil. Gutting slows the meat (×0.6), a
torn gut hastens it (×1.5). Below 45% freshness a piece is *starting to turn* and comes out marked *spoiling*; at 5% it
comes out *spoiled*; once it is gone completely the material can no longer be taken. The window says how long each material keeps (*turns in 12 min*, *frozen*).

Spoiled or poisoned food is still food — eating it is not a good idea: food is less filling the worse its quality, and
a spoiled, poisoned, tainted, soiled or gut-shot piece sates hardly at all.
