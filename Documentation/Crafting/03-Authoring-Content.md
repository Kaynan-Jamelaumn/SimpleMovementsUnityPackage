# 03 — Authoring Content

Everything is data in the **Crafting Database** (`Resources/SimpleMovements/Settings/CraftingDatabase`): open it in the
Inspector (every field has a tooltip). **Extensions** are further databases merged in by id (a content pack adds
elements, ores or designs without touching the base); **Templates** link designs to template items. No list anywhere
names what a combination "makes": metals are what their composition is, items are what their parts and joints are.

## Elements and alloy effects

An **element** (`content.elements`) has its physical properties — density, melting and boiling points, hardness (HV),
toughness (J), ductility (%), corrosion resistance, castability — how it is worked (oxidation onset and rate,
recrystallisation, hot-working range, burn temperature, grain growth, work hardening), whether it is magnetic and its
Curie point, how readily it goes into slag, its magic, an elemental **affinity** with a strength, its supernatural
quality, its price per gram, what it looks like before it is known (`observation`) and whether it is always known.

An **alloy effect** (`content.effects`) says what an element does to a base element's property as its fraction grows:
it rises to `peakValue` at `peakAt`, then changes by `beyondSlope` (tin in copper hardens it up to about a quarter and
embrittles beyond; carbon in iron raises the quench hardness; nickel raises hardenability…), and how it tints the colour.
Properties: Hardness, Toughness, Ductility, Corrosion, MeltingPoint, Hardenability, Magic, Castability, HotShortness,
ColdShortness, Tint, Supernatural, WorkHardening, QuenchHardness.

## Material classes

A **class** (`content.classes`) names a metal by its composition: a base element and ranges the other elements must be
in (`With(element, min, max)`), a priority (the highest matching class wins), a price multiplier, a description and an
observation. "Bronze" is copper with 4–16 % tin — so a melt of copper and tin *becomes* bronze, whatever it was called.
Add a class to name a new alloy; add an effect to make it behave differently.

## Ores

An **ore** (`content.ores`) has its observation, colour, lustre, streak, density, hardness, the range of mineral in a
lump (`mineralMin`–`mineralMax`), its **yields** (grams of each element per gram of mineral, with a range across
deposits), its gangue (Quartz, Clay or Limestone — how it washes and slags), whether it is native or a sulfide (roast it),
its roasting and reduction temperatures and rate, the lump mass, whether it is magnetic, a flux it needs (mythril: the
arcane flux) and a price. Deposits vary the yields within their ranges.

## Fuels, quenchants and vat agents

| Kind | Fields |
|---|---|
| Fuel | item used up (empty = free with a woodpile), burn units per item, maximum temperature, reducing and carburising character, sulfur, ash, burn rate; **Burns Any Wood**: any wood in the bag feeds it too (wooden blanks, materials named as or after a wood) |
| Quenchant | item, severity, vapour blanket, low-temperature factor, boiling and flash points, heat capacity, element affinity it lends to steel it hardens, observation |
| Tanning agent | kind (Water, Lime, Vegetable, Mineral, Oil, Alchemical, Acid), item, dose strength, hours to tan through, case-hardening threshold, fixation (washes out?), stiffness and water resistance it gives, shrink temperature, best and maximum temperature, colour, durability, fire resistance, action hours (lime, acid) |

## Hides, woods and supplies

A **hide** has its size in cells, thickness along the back and at the belly, fibre strength, hair and fat, flaws, colour,
hair colour, an affinity (drake hide: fire) and a price; tick **Textile** for cloth (a straight-edged bolt, woven and
finished: cut and sewn at the tailor's, never tanned). A **wood** has density, toughness, stiffness, splitting,
colour, a blank size and a price. A **supply** is used up as it is worked: kind (Flux, Thread, Padding, Wax, Oil, Dye,
Clay, Cupel, Other, Fibre, Varnish), item, strength (thread strength, padding protection, how much oil or wax gives,
how well a flux works, how tough a varnish is), colour (dyes) and price; a **Fibre** lists **Spins Into** (the thread
supplies a coarse, an even and a fine hank become). A design's sheet part (kind Hide) says in **Leather ▸ Fabric**
whether it is cut from leather, cloth or either.

## Abrasives, moulds and patterns

An **abrasive** has its kind (Brush, Wheel, File, Polish, Whetstone), how fast it cuts and takes scale, the finish it
leaves, the heat it puts in, the hardest metal it bites, whether it works wood or leather, the apex a whetstone leaves
and an item. A **mould** has a size or a profile (width and thickness along its length), an eye (where, how wide) or
makes fasteners (kind, count, grams each). A **pattern** is rows of `X` (the cells it covers) with seam holes.

## Designs and templates

A **design** is an item that can be made:

* `kind` (Weapon, Armor, Tool, Shield), weapon category, tool type, damage form, scaling, armour slot;
* **components**: each with a role, kind (Metal, Hide, Wood, Fasteners or a supply), function (Edge, Striking, Guard,
  Grip, Counterweight, Protection, Backing, Strap, Fastener, Padding, Wrap, Thread), count, optional, importance, and what
  it should be — a **shape target** for metal and wood (length range, profile points of width and thickness, edge kind
  and where it runs, edge angle, tang, curvature, dish, eye, mass range, whether it is hardened), a **leather spec**
  (pattern, thickness, tannage, stiffness), a **wood spec**, a fastener kind and count, a supply kind and count;
* **joints**: kind (Fit, Peen, Rivet, Stitch, Wrap, Wedge, Socket, Buckle), the two roles, a count (stitches, rivets,
  turns) and an importance;
* **menu group**: where a station's Assembly menu lists it, as "Category/Group" ("Weapons/Swords", "Armour/Plate",
  or your own: "Heirlooms/Knives"); empty = worked out from its kind and parts (`CraftDesignMenu`);
* the numbers a well-made one reaches: damage, critical chance and multiplier, knockback, reference mass, ideal balance,
  tool damage, defence, magic defence, durability, price, inventory grid size.

The **template** of a design is a weapon or armour asset whose attacks, animations, hit volumes, models and sounds every
crafted one copies (**Create Design Templates** makes one per design from the built-in weapon movesets; replace their art
freely). Without a template a crafted weapon has numbers but no attacks. The movesets follow the weapon category: swords,
greatswords, daggers, spears and polearms, axes (and scythes), hammers and maces, bows, staffs, thrown weapons (the
throwing-knife moveset) and fist weapons (knuckles).

Where a design can be made follows from its parts and the station's tools (chapter 02, *Where it is made*); a station's
**Designs** list narrows it further (a village smith who only makes tools and knives, a royal armourer with a fixed
catalogue).

## Simulation settings

`settings` holds every constant of the rules (heat transfer, the fire, scale, carbon, the phase model, forging,
abrasion, smelting, process time, how quickly players learn). Change them to make metal heat faster, tanning slower or
learning quicker; the tests in `CustomEditor/Crafting` check the built-in values behave as described.
