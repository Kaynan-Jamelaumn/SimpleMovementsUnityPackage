# 03 — The Work

Taking a material is **work done by hand**, in stages. Each stage is one of seven small games played with the mouse on
the work surface — the same seven, combined differently for every material and shaped by its body: the opening cuts
of a bear's pelt follow a bear's belly and legs; a femur is sawn at the knee; the heart sits where the heart is, with
the gut beside it. The plan of the work (`HarvestPlanner`) comes from the material, its condition, the player's skills
and techniques, and the tool they will use; the games (`HarvestStageModel`s) measure what the hands did.

Every stage shows: a title (*Stage 2 of 3 · Separate the hide*), one line of instructions, the tool and how fit it is
(*Skinning knife (good)*), what the gauge on the right means, and feedback as it happens. Colours are the same
everywhere: **green** is right, **amber** is a warning, **red** is harm. The hands are not perfectly steady — they
wander a little, more as the arm tires (the bar at the top right; let go to rest it) and less with practice — so the
pointer must keep correcting.

## The seven games

| Game | What to do | The gauge | What goes wrong |
|---|---|---|---|
| **Trace** — opening cuts, seams between muscles, opening the body | Start at the green dot and follow the line to its end. | The knife's speed, with a band for the safe speed. | Off the line the cut goes ragged and wastes. Too fast it wanders. Where the line crosses a **joint** (amber) or runs over the **gut** (red) the safe speed is much lower: rushing a joint hacks the bone, rushing over the gut opens it and soils everything inside. |
| **Peel** — separating a hide from the flesh | Press on the edge of the hide still on and pull it outward. | The pull, with a green band. | Slack does nothing; past the band the hide **rips**. Faces, paws and tails have a narrow band; holes from the fight tear easily; a dull knife needs more pull and nicks more. Pulling in the middle of the band leaves the least flesh on the hide. |
| **Steady** — cutting small attachments (an organ's ligaments, a tendon's ends) | Press on a yellow mark and hold the knife on it until it is cut; then the next. | How far the tip is off the mark. | The hand wanders the longer it holds: wandering into the piece damages it; into a gut, bladder or gall bladder (red when near) **spills** it. Fragile glands have narrow marks and are ruined by one slip. |
| **Pull** — drawing something out (a tendon, an organ, a membrane) | Grip the end (the ring) and pull along the arrow. | The pull (keep it in the green) and the strain (red: about to snap). | Now and then it **catches** — the band jumps up and the pull must follow — then gives; a pull left high after it gives strains it, and strain past the limit **snaps** or tears it. |
| **Saw** — bones at the joint, horns and antlers at the base | Put the saw on the line and stroke back and forth along it, in time with the beat (the amber dot). | How far through. | A snatched stroke makes the saw jump and **splinter** the bone; a crooked one binds and splinters. |
| **Pry** — claws, teeth, talons, scales, shell plates, one after another | Grip the highlighted piece and rock it side to side: each swing past the inner marks loosens it. | How loose it is, and the strain. | Swinging past the outer marks, or snatching, strains it; strain past the limit **cracks** it (a piece already cracked in the fight gives way sooner). |
| **Scrape** — cleaning a bone, plucking feathers, scaling a fish, trimming fat | Drag the scraper over it with brisk, even strokes. | The scraper's speed (keep it in the green) and how clean it is. | Dawdling on what is already bare **gouges** it; frantic strokes skim (and break feathers). **Accept** ends it as it is: what is left on stays on. |

Skill widens every margin (tolerances, bands, strain limits) and steadies the hands; a good tool widens them too and
works faster; a dull or wrong one narrows them. A difficult material, a fragile one, or one badly damaged in the fight
narrows them further. The margins are never so wide that the work does itself.

## The work of each kind of material

| Method (skill) | Materials | Stages |
|---|---|---|
| **Skinning** (Skinning) | Hides, pelts, skins | **Trace** the opening cuts — the dashed lines over the body laid belly up, slow where they are amber (paws, face) or red (over the gut) — then **Peel** the hide off, cell by cell. The hide comes off with every cell as the fight and the knife left it. |
| | Feathers, loose scales | **Scrape** — plucking or scaling over the regions they come from. |
| | Wing membranes (countable) | **Trace** around it, then **Pull** it off. |
| **Butchering** (Butchering) | Cuts of meat | **Trace** the seam around each region the cut comes from (a shank is four legs): joints along the seam are slow going. |
| | Fat | **Scrape** — trimming it off. |
| **Tendons** (Tendon Extraction) | Sinew, tendons | For each: **Steady** — free both ends — then **Pull** it out whole. |
| **Bones** (Bone Harvesting) | Skulls, long bones, ribs, vertebrae, a pelvis, a wishbone | For each: **Saw** it free at the joint, then **Scrape** it clean. |
| **Organs** (Organ Harvesting) | Hearts, livers, lungs, kidneys, the gall bladder, glands, sacs, stones | **Trace** — open the body, slowly over the gut (once per carcass) — then for each organ: **Steady** — cut it free without nicking it or the gut — then **Pull** — lift it out. |
| **Trophies** (Trophy Extraction) | Claws, teeth, fangs, talons, scales, shell plates | **Pry** them loose, one after another. |
| | Horns, antlers, tusks, beaks, paws | For each: **Saw** it off at the base. |

The pieces of countable materials (each claw, each organ, each bone) are worked and graded one by one. A material's
**Tools** decide which tools serve each stage (see [04](04-Skills-Tools-and-Quality.md#tools)); a stage that needs a
blade (trace, peel, steady, saw) cannot be done without one: "You need a tool for this: a skinning knife or any
blade."

## The order of work

A carcass is taken apart in an order, as a real one is:

* **The hide first.** Meat, fat, tendons and bones of a creature with a hide wait for it to come off (*the hide must come
  off first*). **Hack the hide off** strips it in one go when the player only wants what is under it: quick, and the
  hide is ruined (a pelt's, that is: the feathers or scales growing beside it stay on).
* **Opening the body** is the first stage of the first organ taken; after that the cavity is open for everyone.
* **Taking the guts** (stomach, intestines) **guts** the carcass: its meat and hide spoil slower from then on (×0.6).
  A gut torn — by the fight or a slip of the knife — **soils** the cavity: the meat around it spoils faster (×1.5) and
  the pieces there come out *contaminated*.
* **Meat before bones.** Sawing a bone out of a region still full of meat tears up the meat around it: the details warn
  about it ("Cutting it out now tears up the haunch around it: take the meat first").
* **Skill gates.** Some materials need a level to try at all (a drake's hide: Skinning 15; its fire gland: Organ
  Harvesting 20).
* **Rare finds** — a bezoar in a bear's stomach, a heartstone in a drake, a musk pod in a deer — show as *Something
  unusual* until the player's Anatomy is good enough to tell what they are (or they have taken one from that species
  before), and they are not drawn among the organs until then.

## Partial harvesting, coming back, sharing

* Take what you want and leave the rest. **Countable materials** can be taken part by part: the toggles under a material
  choose the regions its pieces come from (only the forepaws' claws, only the left femur).
* Walk away and come back: the carcass stays (spoiling) for its *Carcass Minutes* — longer while someone works on it.
  Nothing taken is lost; a stage left half done is.
* Several players can work one carcass at the same time, each on a different material. A material being worked by
  someone else shows *someone is working on it*.
* **Stop** abandons the work in hand: nothing is taken, nothing is lost, the material is free again. Walking away or
  closing the window does the same.

## Fair play

The same carcass, player and tools always give the same plan, and the same inputs at the same moments always give the
same result. In a network session the server makes the plan, sends it to the player's machine, and holds the result to
it: the same stages for the same pieces, every measure between 0 and 1, the tool as the server knows it (not as the
client says), the hide's cells the size of the hide. Work done faster than careful hands could do it counts as
**rushed**: it comes out rough, as rushing does in the games themselves. See
[07 — Multiplayer](07-Scripting-Editor-Multiplayer.md#multiplayer).
