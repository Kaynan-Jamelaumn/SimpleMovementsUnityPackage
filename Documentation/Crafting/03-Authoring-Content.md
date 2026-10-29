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
Clay, Cupel, Other, Fibre, Varnish, Compound, Solder, Detergent), item, strength (thread strength, padding protection,
how much oil or wax gives, how well a flux works, how tough a varnish is, how strongly a detergent scours), colour (dyes)
and price; a **Fibre** lists **Spins Into** (the thread supplies a coarse, an even and a fine hank become) and how it
comes: **staple** (mm; under 40 it cannot be combed), **grease**, **dirt**, **short fibre**, whether it **felts** and the
grams of one item, and **weaves into** - the cloth (a textile hide id) its yarn becomes on a loom; a **Compound** its **polish hardness** (the hardest stone it polishes well). A varnish whose id
contains *resin* builds a resin film; a Wax supply is rubbed in.

## Gems and cuts

A **gem** is a species or variety: hardness (Mohs), toughness, cleavage (None, Poor, Good, Perfect) and its planes,
refractive index, specific gravity, dispersion, pleochroism, heat sensitivity, whether it is opaque (a cabochon only),
its preferred cut, its typical rough carats and clarity, its value per carat, an element affinity and strength (what it
gives jewellery), what the rough looks like (its item is named for that) and a description (shown once it is known).
Players tell gems apart by these very numbers - keep them distinct, or two stones become indistinguishable. Hardness and
toughness also set how forgiving the timing bars are when the stone is split, sawn and faceted (`CraftLapidary.Difficulty`),
and whether a stone without a cleavage can be nicked and snapped (toughness under 0.65) or must be sawn.

A **cut** has its outline's length ratio, whether it is a cabochon, its yield (share of the outline's rough left) and its
**tiers** in the order they are cut: each a name, a kind (Pavilion, Girdle, Crown, Table, Base, Dome), the facets in it
and an angle offset from the stone's ideal (stars and breaks sit shallower). A design's sheet part (kind Hide) says in **Leather ▸ Fabric**
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
* **joints**: kind (Fit, Peen, Rivet, Stitch, Wrap, Wedge, Socket, Buckle, Solder, Bezel Set, Prong Set), the two roles,
  a count (stitches, rivets, turns, prongs) and an importance;
* **jewellery** (kind Jewelry, an armour slot - Ring, Amulet, Helmet for a circlet, Bracers for a bracelet): metal parts
  with a function **Band** (and a **Jewel Part**: Band, Bail, Chain) or **Setting** (a bezel or prong head, by its joint
  with the stone), a part of kind **Gem** with function **Stone** and a **Gem spec** (carats from-to, faceted and/or
  cabochon), a Solder supply; their metal parts need no shape profile;
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
abrasion, smelting, process time, how quickly players learn, and the **timing bars**: the half-width of each zone -
Perfect, Excellent, Good, Poor -, the score at each zone's edge, the score below which a hit does damage, the
indicator's speed, how much harder material narrows the zones and how much practice widens them). Change them to make
metal heat faster, tanning slower, learning quicker or the precision work more forgiving; the tests in `CustomEditor/Crafting` check the built-in values behave as described.

**Mastery** (`settings.mastery`, chapter 04 *Practice*) sets how players get better at each craft:

| Setting | Default | What it does |
|---|---|---|
| Experience Scale | 45 | the experience for 63 % mastery (`1 − e^(−experience / scale)`): double it and every rank takes twice the work |
| Per Item | 1.2 | experience for a finished item, × (0.5 + its quality) × how much work the design is (its parts and joints) |
| Work Per Minute | 0.35 | the most small work (blows, strokes, a bobbin spun) teaches a minute in one craft |
| Per Insight | 0.5 | experience for each insight, in its craft |
| Apprentice, Journeyman, Expert, Master | 0.15, 0.35, 0.55, 0.78 | the mastery each rank starts at |
| Fatigue Relief | 0.4 | how much less a master's arm tires |
| Finishing Touch | 0.12 | the share of what an item still lacks a master's finishing adds to its quality |
| Routine Low, Routine High | 0.55, 0.9 | the quality of work done by routine at the rank that allows it, and at full mastery |
