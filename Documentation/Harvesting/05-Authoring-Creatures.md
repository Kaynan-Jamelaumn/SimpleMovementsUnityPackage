# 05 — Authoring Creatures

A creature is **data**: a body plan, regions and materials. Every mechanic — how hits mark it, the inspection, the
minigames, quality, spoiling, items — works from that alone.

## Creature Anatomy assets

* *Assets ▸ Create ▸ SimpleMovements ▸ Harvesting ▸ Creature Anatomy* (it starts as a wolf), or *Harvesting ▸ Create
  Creature Anatomy Assets* for an editable copy of every built-in creature (in `Resources/SimpleMovements/Harvesting/
  Anatomies`, registered in the database).
* **Start From** (top of the inspector) replaces everything with a built-in creature, to start a new one from the closest
  existing body.
* Register it in the Harvest Database's **Anatomies** list. An asset with the id of a built-in creature replaces it.
* Mobs use it when one of its **Mob Profiles** is theirs, or a word of its **Mob Types** is their type or a whole word of
  their name ("Black Bear" is a bear; "Bearded Vulture" is not) — or when a Harvestable Creature names it.

The inspector draws the body as the carcass window does, coloured by tissue (*Overall, Hide, Meat, Bone, Tendons,
Organs*), with its weak points ringed, lists the weak points and the materials by method, and shows every problem
(a material from a region that does not exist, a countable material without pieces, a hide without a tannery hide…).

### The creature

| Field | Meaning |
|---|---|
| **Id** | Stable id (`bear`, `giant_spider`): item ids and saves use it (`harvest:bear/claws`). |
| **Display Name**, **Description** | Shown to players. |
| **Plan** | How the body is built: *Quadruped* (four legs, a horizontal body — add wings as regions `wing_l`/`wing_r`), *Biped* (two legs, upright), *Avian* (wings, two legs), *Fish* (fins and a tail), *Serpent* (one long body), *Arthropod* (a shell and many legs). It decides how the body and its organs are drawn, how the hide is opened, and how hits are located along it. |
| **Mob Types** | Words of mob types or names it is used for (Bear, Grizzly). |
| **Mass**, **Reference Height** | A typical adult. Yields scale with a mob's size against this height. |
| **Pelt Size** | Cells of its hide laid flat, head up (across × along). For a hide that becomes a tannery hide, the crafting hide's *Height* × *Width* (the tannery lays hides with the spine across). |
| **Hide / Hair / Meat Color** | The drawings' colours. |
| **Toughness** | Its tissues resist damage (a troll: 1.4): hits mark them less. |
| **Regeneration** | While alive, wounds close by this fraction a minute (trolls 0.5); burns never do. |
| **Resistances** | Elements its tissues shrug off (a drake: fire 0.9). |
| **Default Region** | Where hits without a location go (empty: the region with the most meat that is not a weak point and takes no extra damage, a multiplier of 1 or less). |
| **Regions**, **Materials** | Below. |

### Regions

| Field | Meaning |
|---|---|
| **Id**, **Display Name** | `head`, `foreleg_l`… (materials name regions by id). |
| **Body Parts** | The combat body parts that are this region (names or aliases: *Head*, *Snout*, *LeftForeleg*…). Hits on them mark it. |
| **Slot** | The shape it is drawn with (a slot of the plan; empty = the id). Quadruped: `head, neck, shoulder, back, ribs, belly, haunch, foreleg_l/r, hindleg_l/r, tail, wing_l/r`; Biped: `head, neck, chest, belly, pelvis, arm_l/r, leg_l/r, tail`; Avian: `head, neck, breast, belly, wing_l/r, leg_l/r, tail`; Fish: `head, back, belly, tail, fins`; Serpent: `head, neck, body, tail`; Arthropod: `head, thorax, abdomen, legs_l/r, tail`. |
| **Hide, Meat, Bone, Tendon, Organs, Armor** | What it is made of (see [02](02-The-Fight-and-the-Body.md#regions)). |
| **Pelt** | Where it lies on the hide laid flat (x across 0-1, y from the head end 0-1). |
| **Difficult Skin** | Face, paws, tail, joints: a narrow band when peeling here. |
| **Height**, **Length**, **Side**, **Side Offset** | Where hits land on it when there is no hitbox: height band (0 feet – 1 top), length band (0 tail end – 1 nose), and the side for limbs. |
| **Damage Multiplier**, **Weak Point**, **Bonus Defense** | Combat: hits here deal × this; a weak point is shown as one; a thick skull or a soft belly. |
| **Reaction** | *Stagger* (head hits stun briefly) or *Slow* (leg and wing hits slow it). |
| **Redirect When Severed** | Where hits go when this part is gone. |
| **Hitbox Bone**, **Hitbox Radius** | Optional exact hit locations: when the mob plays, a trigger sphere (a Body Part Hitbox for the region's part) goes on that bone of its model - radius in metres, 0 = from its height. Only on a mob that uses its anatomy's body parts and has no hitboxes of its own; the hitbox takes the mob's layer (one of the Combat Settings' Character Layers). |

### Materials

| Field | Meaning |
|---|---|
| **Id**, **Display Name**, **Description** | `pelt`, `backstrap`, `heart`, `claws`… |
| **Kind** | Hide, Feathers, Scales, Meat, Fat, Tendon, Bone, Organ, Gland, Claw, Tooth, Horn, Chitin, Essence, Other: the item it becomes, how damage reaches it, its icon. |
| **Method** | Skinning, Butchering, Tendons, Bones, Organs, Trophies: the work (see [03](03-The-Work.md#the-work-of-each-kind-of-material)), the tools and the skill. |
| **Shape** | How it is drawn and worked (`heart, liver, gall, gland, longbone, skull, ribs, spine, claw, fang, horn, antler, tusk, beak, scale, plate, tendon, sinew, cut, fat, feathers`…). Horns, antlers, tusks, beaks and paws are sawn off; claws, teeth and scales are pried loose. |
| **Sources** | The regions it comes from: for a countable material, how many pieces each holds (5 claws per forepaw); for a bulk one, each region's share. |
| **Countable** | Each piece taken and graded on its own (claws, organs, bones, tendons). Off: a bulk yield (meat, fat, a hide). **Yield**, **Unit**: the bulk pieces from a typical, unhurt, perfectly worked creature (min – max) and what one is called. |
| **Difficulty** | 0 easy – 1 very hard: narrower margins, faster mistakes. |
| **Min Skill** | The method's skill level needed to try it. |
| **Reveal Skill**, **Chance** | Rare finds: the Anatomy level needed to notice it, and the chance a creature has it. |
| **Tools** | The tools it is worked with (empty: the method's usual ones). |
| **Requires** | What must be taken or done first: material ids (or that they can no longer be had: spoiled, destroyed), `hide` (the hide off), `cavity` (the body opened: an organ taken - organs open it themselves), `gutted` (the stomach and guts out). |
| **Spoil Minutes** | Real minutes until it spoils at 20 °C (0 = never). |
| **Value**, **Weight**, **Tier**, **Xp** | One perfect piece. |
| **Craft Hide** | A hide that becomes this Crafting Database hide for the tannery (`bear`, `wolf`, `deer`, `boar`, `rabbit`, `drake`, `troll`, `snake`, `cow`). |
| **Hazard**, **Fragile** | Torn, it soils what is around it (gut, bladder, gall); one slip and it is lost (glands, sacs). |
| **Edible**, **Food Value** | It can be eaten (meat, fat and organs only); hunger one perfect piece restores. |
| **Color** | Its icon and drawing. |
| **Alchemy** | What one piece gives an alchemy vessel: Alchemy Database substances, their amount and extractability. |

## The bear, explained

```csharp
HarvestAnatomyData a = New("bear", "Bear", HarvestBodyPlan.Quadruped, 250f, 1.4f, 12, 16,
    "A heavy, thick-furred predator: a great pelt, fat, claws and the prized gall.", "Bear", "Grizzly");
a.regions = QuadrupedRegions();            // head, neck, shoulder, back, ribs, belly, haunch, four legs, tail
a.Region("head").bonusDefense = 3f;        // a thick skull
a.materials.Add(Material("pelt", "Bear pelt", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back")
    .Yield(1f, 1f, "pelt").Hard(0.55f).Spoils(120f).Worth(45f, 9f, 16f, 2).Tannery("bear"));
QuadrupedMeat(a, "Bear", 30f, 2.2f);       // haunch, backstrap, shoulder, ribs, neck meat, shanks
a.materials.Add(Material("fat", "Bear fat", HarvestMaterialKind.Fat, HarvestMethod.Butchering, "fat")
    .From("belly", 1, 1f).From("back", 1, 0.5f).Yield(3f, 5f, "slab").Needs(HarvestMethods.Hide).Spoils(80f).Alchemy("oil", 3f, 0.6f));
QuadrupedTendons(a, "Bear", 5f);           // leg sinew ×4, back sinew
QuadrupedBones(a, "Bear", 4f);             // skull, femurs, humeri, rib bones, vertebrae, pelvis
Organs(a, "Bear", 2f, gallValue: 40f);     // heart, lungs, liver, kidneys, gall bladder (prized), stomach, intestines
Claws(a, "claws", "Bear claw", 5, 5, 5f, 0.5f);
a.materials.Add(Material("fangs", "Bear fang", HarvestMaterialKind.Tooth, HarvestMethod.Trophies, "fang").From("head", 4).Count());
a.materials.Add(Material("bezoar", "Bezoar stone", HarvestMaterialKind.Other, HarvestMethod.Organs, "gland").From("belly").Count()
    .Rare(0.12f, 15).Alchemy("antitoxin", 2f, 0.5f));
```

* **The body.** Twelve regions in the four-legged layout. The head (×1.5), the neck (×1.6) and the ribs over the heart
  and lungs (×1.3) are weak points; the belly is soft (×1.15, −2 defense); legs (×0.8) slow it, the head staggers it.
  Paws, face and tail are hard to skin. The pelt is 12 × 16 cells, the size of the crafting system's bear hide.
* **The pelt** (from the whole back of the hide) is the prize: difficult (0.55), it keeps two hours at 20 °C, worth 45 —
  and becomes a *bear hide* for the tannery, holes and all.
* **Under the hide**: six cuts of meat, fat, two kinds of sinew and six kinds of bone all wait for the hide.
* **In the cavity**: seven organs, behind the ribs (heart, lungs) and in the belly (liver, gall bladder, kidneys, stomach,
  guts). The gall bladder is fragile and prized (40); torn, it soils the liver beside it. The stomach and guts soil the
  cavity when torn; taking them out guts the carcass.
* **Trophies**: five claws on each forepaw and hindpaw, four fangs.
* **A rare find**: one bear in eight carries a bezoar (Anatomy 15 to notice it), worth 60, an antitoxin for alchemists.

## The wolf, compared

The wolf shares the bear's layout (the same regions and weak points), so it differs only where wolves differ: a lighter
body (45 kg, 0.85 m), a smaller pelt (8 × 11 cells — the tannery's wolf pelt) worth 20, less meat, no fat worth taking,
cheaper bones and sinew, five claws on the forepaws and four on the hind, four fangs, and a rare **scent gland** in the
haunch (one wolf in three; Anatomy 10; fragile; spoils in 30 minutes; *esters* for alchemists). Its Mob Types cover its
kin: wolf, warg, fox, hound, dog, coyote, jackal.

## A creature of your own

A **griffon** — an eagle's head and wings on a lion's body:

1. *Create ▸ SimpleMovements ▸ Harvesting ▸ Creature Anatomy*, **Start From** `drake` (four legs and wings), *Apply*.
2. Id `griffon`, name *Griffon*, Mob Types *Griffon, Hippogriff*; Mass 300, Reference Height 1.6; Pelt Size 12 × 16 with
   Craft Hide `bear` (or add a griffon hide to the Crafting Database and name it here).
3. Regions: keep the drake's, but take the armour off (it has fur and feathers, not scales: Armor 0), make the head a
   stronger weak point (×1.7).
4. Materials: rename the hide *Griffon pelt*; delete *scales*, *fire gland* and *heartstone*; change *membrane* into
   *Flight feathers* (Kind Feathers, Method Skinning, from `wing_l` and `wing_r`, Countable off, Yield 20-40 — plucked); add a
   *Beak* (Kind Horn, Method Trophies, Shape `beak`, from `head`: sawn off) and *Talons* (Kind Claw, Shape `talon`, from
   the forelegs, 4 each: pried); add a rare *Griffon eye* (Organ, Shape `gland`, from `head`, Chance 0.3, Reveal Skill
   15, Alchemy `zephyr_essence`).
5. Add it to the Harvest Database's Anatomies. Mobs named *Griffon* are now harvestable; check the inspector for
   problems, and try one with a practice carcass (set the practice carcass's Anatomy Id to `griffon`).

Plucking the feathers leaves the pelt on (only the pelt frees the meat), and hacking the pelt off leaves the
feathers be. The drake's wings have no hide of their own, so they have no place on the pelt: feathers (or scales) on
such parts grow on a small hide of their own, 2 × 2 cells, that the fight marks (cuts, holes, burns, burning over
time) and regeneration heals as it does the pelt - a griffon's wing fares as an eagle's does under the same blows - and
the window and the work judge them by it.

The same in code, with the short-hands of `HarvestBuild` (and the built-in layouts of `HarvestDefaults`):

```csharp
using static HarvestBuild;

HarvestAnatomyData g = new HarvestAnatomyData { id = "griffon", displayName = "Griffon", plan = HarvestBodyPlan.Quadruped,
    mass = 300f, referenceHeight = 1.6f, peltSize = new Vector2Int(12, 16), mobTypes = { "Griffon", "Hippogriff" } };
g.regions = HarvestDefaults.QuadrupedRegions(wings: true);
g.Region("head").Weak(1.7f);
g.materials.Add(Material("pelt", "Griffon pelt", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back")
    .Yield(1f, 1f, "pelt").Hard(0.6f).Spoils(150f).Worth(80f, 8f, 18f, 2).Tannery("bear"));
g.materials.Add(Material("feathers", "Flight feathers", HarvestMaterialKind.Feathers, HarvestMethod.Skinning, "feathers")
    .From("wing_l").From("wing_r").Yield(20f, 40f, "feather").Hard(0.4f).Worth(3f, 0.02f, 2f));
g.materials.Add(Material("beak", "Griffon beak", HarvestMaterialKind.Horn, HarvestMethod.Trophies, "beak").From("head").Count().Hard(0.5f).Worth(40f, 0.6f, 12f));
g.materials.Add(Material("talons", "Griffon talon", HarvestMaterialKind.Claw, HarvestMethod.Trophies, "talon")
    .From("foreleg_l", 4).From("foreleg_r", 4).Count().Hard(0.5f).Worth(12f, 0.05f, 6f));
g.materials.Add(Material("eye", "Griffon eye", HarvestMaterialKind.Organ, HarvestMethod.Organs, "gland").From("head").Count()
    .Rare(0.3f, 15).Hard(0.7f).Spoils(20f).Worth(60f, 0.05f, 15f).Fragile().Alchemy("zephyr_essence", 2f));

var asset = ScriptableObject.CreateInstance<CreatureAnatomy>();
asset.data = g;
HarvestDatabase.Instance.anatomies.Add(asset);
HarvestDatabase.Instance.MarkChanged();
```

Run *Harvesting ▸ Validate Harvest Database* (or the Project Validator) after editing: it checks every creature, the
item bindings, that the tannery has every hide the creatures give and that alchemy knows every substance they release.

## The Harvest Database

`Resources/SimpleMovements/Settings/HarvestDatabase.asset` (*Harvesting ▸ Create Harvest Database*). Without it the
built-in rules and creatures are used.

| Section | Settings |
|---|---|
| **Creatures** | *Anatomies* (Creature Anatomy assets), *Include Built In* (the twelve built-in creatures for the ids no asset gives). |
| **Items** | *Items*: an item asset given for a material (by creature and material id) instead of the item made at runtime (*Harvesting ▸ Create Items* fills it). *Tools*: the harvesting tool assets (*Create Tools*). |
| **Combat damage** | *Severity Per Health* (2.5), *Critical Multiplier* (1.25), *Area Meat Multiplier* (1.6), *Area Spread* (0.4), *Periodic Scale* (0.5), *Count Overkill*, *Max Severity* (2), *Max Wounds* (32), and the *Forms* and *Elements* tables (what each does to each tissue, how it marks the hide, what marks it leaves). |
| **Carcass and spoiling** | *Carcass Minutes* (30), *Keep After Work Minutes* (5), *Remove When Empty*, *Default Temperature* (18 °C), *Doubling Degrees* (10), *Freezing Point* (0 °C), *Gutted Spoil Factor* (0.6), *Soiled Spoil Factor* (1.5), *Spoiling Below* (0.45). |
| **Quality and yield** | *Freshness Weight* (0.75), *Tool Weight* (0.12), *Skill Quality Bonus* (0.06), *Butchering Yield Bonus* (0.25), *Skill Yield Bonus* (0.1), *Wrong Tool Below* (0.3), *Discard Below* (0.02), *Destroyed Below* (0.08). |
| **Skills** | *Max Level* (50), *Xp First Level* (20), *Xp Growth* (1.08), *Anatomy Share* (0.3), *Failure Share* (0.35), *Character Xp Share* (0.25), *Harvests To Know Species* (6). |
| **Minigames** | *Forgiveness* (× every margin), *Work Speed*, *Tremble*, *Skill Margin* (how much wider the margins are at the top of a skill). |
| **World** | *Work Range* (2.4 m from the carcass plus its size), *Skip Generic Mob Drops*, *Auto Attach*, *Add Body Parts*. |
