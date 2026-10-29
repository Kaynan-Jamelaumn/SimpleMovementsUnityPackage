# 02 — The Fight and the Body

While a harvestable creature lives, every hit it takes is remembered **where it landed** and **by what dealt it**. When
it dies, its carcass is exactly what the fight left. The rules are in `HarvestCondition.ApplyHit`; the numbers are the
Harvest Database's (*Combat damage* settings).

## Regions

A creature's body is cut into **regions** — a bear's head, neck, shoulder, back, ribs, belly, haunch, four legs and
tail; a troll's head, neck, chest, belly, pelvis, arms and legs; a fish's head, back, belly, tail and fins; a spider's
head, thorax, abdomen and two sets of legs. Each region says what it is made of:

| Tissue | Meaning |
|---|---|
| **Hide** (0-1) | How much of it is covered by hide (a beak or a shell: none). It also has a place on the hide laid flat (*Pelt*), so a hit there marks those cells of the pelt. |
| **Meat** | Relative mass of meat: where the cuts come from. |
| **Bone** (0-1) | How much of it is bone: blunt blows break it. |
| **Tendon** (0-1) | Tendons and ligaments: limbs and joints have many; slashes cut them. |
| **Organs** (0-1) | The body cavity: deep hits reach the organs inside. |
| **Armor** (0-1) | Natural armour — scales, plates, a thick skull — takes part of every hit before the tissues under it. |

The materials (a pelt, the backstrap, the heart, five claws per paw) say which regions they come from, so the damage to
a region is the damage to what comes from it.

## Where a hit lands

1. **The hit's body part.** A creature with a *Body Part Controller* (its own, or the one made from its anatomy — see
   below) reports which part was hit: a hitbox it struck, or the part at the hit's height. The region whose *Body
   Parts* list names that part (or one of its aliases) takes the hit.
2. **Otherwise the hit's point** on the body: its height (feet to top), its side (left or right of the centre line),
   and — for bodies longer than they are tall (four legs, fish, serpents) — how far along the body it is, from the tail
   end to the nose. Each region has a height band, a length band and a side.
3. **Without a point** (damage over time, some area effects) the hit goes to the anatomy's *Default Region* (when it
   names none, the region with the most meat that is not a weak point and takes no extra damage - a multiplier of 1 or
   less: a four-legged creature's haunch).

The point also tells **where on the hide** the hit landed: an arrow in the left flank holes the pelt in the left
flank, not just "somewhere in the ribs".

### Body parts made from the anatomy

When a harvestable mob has no body parts of its own (and the database's *Add Body Parts* is on), it gets a profile made
from its anatomy: a part per region with the region's bands, side, **damage multiplier**, **natural armour** (as
defense: one point of armour = 60 defense) and **reaction** (*Stagger*: a short stun, for heads; *Slow*: for legs and
wings). That is what makes weak points work: a bear's neck takes ×1.6 damage, its legs ×0.8. A body-part profile
supports length bands for long bodies (*Length Range* on a part: 0 the tail end – 1 the nose), so a hit on a wolf's
snout and one on its haunch, at the same height, are told apart.

A mob with its own Body Part Controller keeps it: list its part names in the anatomy's regions (*Body Parts*) so its
hits mark the right region.

## Weak points

A region marked **Weak Point** (with a damage multiplier above 1) is a weak point: hits there deal more damage to the
creature (the combat's body-part system) — and the inspection rings it. Other regions can deal more or less too without
being weak points (a soft belly ×1.15, legs ×0.8). The built-in creatures:

| Creature | Weak points |
|---|---|
| Bear, wolf, deer, boar, rabbit, sheep | Head ×1.5, neck ×1.6, ribs (heart and lungs) ×1.3 (the belly takes ×1.15, but is not ringed) |
| Drake | Head ×1.5, neck ×1.6, ribs ×1.3 and its **soft belly ×1.4** — the head, neck, shoulders, back, haunch and tail are scaled (armour 0.5), the belly hardly (0.1) |
| Troll | Head ×1.6, neck ×1.8 (its hide is 1.4× tougher, and its wounds close by themselves) |
| Eagle | Head ×1.6, neck ×1.4; wings ×0.7 |
| Salmon | Head ×1.5 |
| Giant spider | Head ×1.4, abdomen ×1.3 (its softest shell); legs ×0.6 |
| Giant serpent | Head ×1.8, neck ×1.4 |

Aiming well kills faster **and** spoils less: a clean shot to the head leaves the pelt and the meat whole, while forty
cuts to the flanks shred the hide.

## What a hit does, by weapon and element

Every hit has a **form** (how it wounds) and an **element**. A weapon's attack has the form of its category unless it
says otherwise — swords and axes slash; spears, daggers' stabs, arrows and bolts pierce; maces, hammers, staves, fists
and shields are blunt; a monster's teeth bite and its claws claw; a giant's stomp or a falling rock crushes. Abilities
set their own (a fireball: no form, fire). A flaming sword slashes **and** burns: the form's effect and the element's
add up.

How much each harms each tissue of the region it lands on (per unit of severity; the database's *Forms* and *Elements*
lists, editable):

| Form | Hide | Meat | Bone | Tendon | Organs | Claws, teeth, horns | On the hide |
|---|---|---|---|---|---|---|---|
| Slash (swords, axes) | **1** | 0.55 | 0.15 | **0.8** | 0.3 | 0.25 | a line of cut cells |
| Pierce (spears, arrows) | 0.35 | 0.35 | 0.15 | 0.25 | **1** | 0.15 | a hole (small) |
| Blunt (maces, hammers) | 0.12 | 0.5 | **1** | 0.15 | 0.45 | **0.8** | a bruise (barely marks it) |
| Bite | 0.85 | 0.6 | 0.35 | 0.5 | 0.35 | 0.35 | a torn patch |
| Claw | **1** | 0.45 | 0.1 | 0.45 | 0.2 | 0.2 | parallel scratches |
| Crush (stomps, rocks) | 0.3 | 0.85 | **1** | 0.35 | 0.8 | **1** | a crushed patch |
| None (magic, scripts) | 0.35 | 0.4 | 0.2 | 0.2 | 0.3 | 0.2 | a bruise |

| Element | Adds | On the hide |
|---|---|---|
| Fire | ruins the hide (1.1: hair singed, leather burned through), chars the meat under it (0.5) | a burned patch |
| Ice | little harm, but makes the hide and horns brittle (frozen) | a frozen patch |
| Lightning | scorches its path, stops hearts (organs 0.7) | a branching scorch |
| Poison | no mark outside — the meat and the liver are not fit to eat (contaminates the region, and spreads through the blood as damage over time) | none |
| Holy | sears lightly (tainted) | a small patch |
| Dark | withers the inside (organs 0.4, taints) | none |
| Wind | cuts | a line |
| Earth | crushes: bones 0.6, trophies 0.5 | a bruise |
| Water | little | none |

Inside a region:

* **An organ** the hit reaches is damaged (the bigger ones are likelier to be hit): an arrow through the ribs may
  pierce the heart or the lungs. A torn **gut, bladder or gall bladder** soils everything around it: the region's meat
  and organs are *gut-shot* and spoil faster.
* **A bone** is damaged; a blunt blow that does real harm **fractures** it.
* **Claws, teeth, horns, scales** of the region crack (blunt and crushing blows above all).
* **The hide** is marked in its cells where the hit landed, by the mark of its form and element; a hole that went
  through leaves an exit hole too. A part with no hide cells of its own that grows feathers or scales (a griffon's
  wing) has a small hide of its own, 2 × 2 cells, marked the same way: it fares as an eagle's wing does.
* **Damage over time**: burning spreads over the hide (those small hides too, as often as any four of its cells);
  poison runs through the blood and taints every region a little.
* **Area damage** (explosions, slams) hits its region hard — its meat is destroyed 1.6× faster — and a share of it
  (*Area Spread*, 0.4) reaches every other region: an explosive kill leaves little worth taking.

### How hard

A hit's **severity** is the damage it dealt against the creature's maximum health × *Severity Per Health* (2.5): a hit
taking a tenth of its health has severity 0.25; one that removes all of it, 2.5 (capped at *Max Severity*, 2). Severity
1 ruins the tissue a hit harms most in that region. Critical hits mark 1.25× more; damage over time marks half; the
killing blow counts in full with its overkill (*Count Overkill*) — a fireball that kills a deer burns its hide as hard as
it hit. The creature's **toughness** (a troll's 1.4) divides it, **natural armour** softens it (meat and tendons by up
to 60%, organs 40%, bones 30%), and **elemental resistances** remove the element's part (a drake resists fire 0.9: a
fireball barely singes it).

**Regeneration**: a creature with *Regeneration* (trolls: 0.5) closes its wounds while it lives — by that fraction a
minute — but never burns or acid.

## Fight damage and harvest damage are kept apart

Every piece remembers its marks, split in two:

* **From the fight**: *cut, pierced, bruised, fractured, torn, crushed, burned, frozen, gut-shot, poisoned, tainted,
  blasted*.
* **From the harvest**: *nicked, ripped, contaminated, splintered, cracked, rough* — and from time: *spoiling, spoiled*.

The inspection shows only the fight's (it is what the body is); a piece's tooltip tells both ("Pierced in the fight;
nicked while skinning."), and a hide for the tannery keeps every hole and burn where it was.

## The wounds

The carcass window lists every wound, newest first: what (a cut, a piercing wound, a blow, a burning cut, poison…),
where, and with what (the weapon's or ability's name), critical ones marked. With **Anatomy 5** (*Read the Body*) the
player can also tell what each did inside: "pierced the heart", "broke the skull", "tore the gall bladder open",
"blasted the whole body". A creature remembers up to 32 wounds (*Max Wounds*: the oldest small ones are forgotten).

## Hunting well

| To get… | Hunt it with… | Avoid… |
|---|---|---|
| A whole pelt | Blunt blows to the head, a single arrow to the neck or ribs | Slashing it to death, fire, claws and bites (your hounds) |
| Organs (heart, liver, glands) | Blows to the head, cuts to the legs | Arrows and spears to the ribs, lightning |
| Bones, skulls, teeth, horns | Arrows, blades | Maces, hammers, crushing — they fracture and crack |
| Tendons | Arrows to the body | Slashing the legs and the back |
| Meat | Precise kills | Explosions, poison (the meat is not fit to eat), gut shots |
