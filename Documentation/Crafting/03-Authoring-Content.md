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
in (`With(element, min, max)`), a priority (the highest matching class wins; of equal priority, the one listed first), a
price multiplier (what its making adds to its matrix - the base and what is alloyed in no dearer than it; a dearer metal
alloyed in counts at its own worth, so an alloy of a precious metal is worth its parts: a premium steel is worth its
premium as a bar, as a blade and in the sword), a description and an observation (a noun phrase first - "a soft, bright
metal that crackles when bent": an unknown bar of it is a "Bar of soft, bright metal"). "Bronze" is copper with 4–16 % tin and at most 10 %
zinc — so a melt of copper and tin *becomes* bronze, whatever it was called. Add a class to name a new alloy; add an
effect to make it behave differently. Keep classes of one base and one priority from overlapping: *Validate* warns
where they do.

## Ores

An **ore** (`content.ores`) has its observation, colour, lustre, streak, density, hardness, the range of mineral in a
lump (`mineralMin`–`mineralMax`), its **yields** (grams of each element per gram of mineral, with a range across
deposits), its gangue (Quartz, Clay or Limestone — how it washes and slags), whether it is native or a sulfide (roast
it), its roasting and reduction temperatures and rate, the lump mass, the dirt and caked clay in a lump
(`dirtMin`–`dirtMax`), whether it is magnetic, a flux it needs (mythril: the arcane flux) and a price - a usual lump's
(one of its lump mass, its mineral at the middle of its range): a lump is worth its price × its mineral against that.
Deposits vary the yields within their ranges. Price an ore at about what the metal of its poorest lump of the poorest
deposit is worth: *Validate* warns of an ore dearer than that, or worth under a fifth of a usual lump's metal.

## Fuels, quenchants and vat agents

| Kind | Fields |
|---|---|
| Fuel | item used up (empty = free with a woodpile), burn units per item (`energy`), maximum temperature, reducing and carburising character, sulfur, smoke, burn rate; **Burns Any Wood**: any wood in the bag feeds it too (wooden blanks, materials named as or after a wood) |
| Quenchant | item, severity, vapour blanket, low-temperature factor, boiling and flash points, heat capacity, element affinity it lends to steel it hardens, observation |
| Tanning agent | kind (Water, Lime, Vegetable, Mineral, Oil, Alchemical, Acid), item, dose strength, hours to tan through, case-hardening threshold, fixation (washes out?), stiffness and water resistance it gives, shrink temperature, best and maximum temperature, colour, durability, fire resistance, action hours (lime, acid) |

## Hides, woods and supplies

A **hide** has its size in cells, thickness along the back and at the belly, fibre strength, suppleness, hair and fat,
flaws, colour, hair colour, an affinity (drake hide: fire) and a price - the whole hide's (a piece of it is worth its
share of the whole, twice that tanned through); tick **Textile** for cloth (a straight-edged bolt, woven and finished:
cut and sewn at the tailor's, never tanned; a piece is worth its share of the bolt). A **wood** has density, hardness,
toughness, flexibility, cleave, colour, a blank size and a price - one blank's: any piece of the wood is worth its share
of a blank by volume, half that while it is unseasoned (wetter than 20 %). The blank is what one stock item of the wood
puts on the bench, so make it as long and wide as the longest part a design wants of it (an ash stave 2.2 m long for a
spear's shaft, a linden plank 110 × 65 cm for a kite shield's board); a stock log is as long, at least 90 cm. *Validate*
warns of a wooden part no blank or log of a tough-enough wood can give. A **supply** is used up as it is worked: kind
(Flux, Thread, Padding, Wax, Oil, Dye, Clay, Cupel, Other, Fibre, Varnish, Compound, Solder, Detergent), item, strength
(thread strength, padding protection, how much oil or wax gives, how well a flux works, how tough a varnish is, how
strongly a detergent scours), colour (dyes), price and **item grams** - what one item of it weighs in the bag and in
what it goes into (0: by its kind - a fibre its grams per item, padding 300 g a batting, thread a 20 m skein's 5 g,
solder half a gram, anything else 300 g): a gambeson weighs the battings quilted into it, and is worth them; a **Flux** says where it works (**Flux Use**: in the furnace's
charge, as a cover over a melt, on a weld - any of them unless set, so a flux from an older database works everywhere it
did); a **Fibre** lists **Spins Into** (the thread supplies a coarse, an even and a fine hank become) and how it comes:
**staple** (mm; under 40 it cannot be combed), **grease**, **dirt**, **short fibre**, whether it **felts** and the grams
of one item, and **weaves into** - the cloth (a textile hide id) its yarn becomes on a loom; a **Compound** its **polish
hardness** (the hardest stone it polishes well); a **Solder** its **flow temperature** (°C: the glow the joint comes to as
it takes; 0 = a hard solder's 760). A varnish whose id contains *resin* builds a resin film; a Wax supply
is rubbed in. A wood's **cleave** (0-1) is how true it splits along its grain (oak 0.9, elm 0.15: a froe or a wedge
strays in it, the knife splits it sooner); a Varnish's or an Oil's **drying hours** are the process hours a coat of it
takes to dry on wood (0: four).

## Gems and cuts

A **gem** is a species or variety: hardness (Mohs), toughness, cleavage (None, Poor, Good, Perfect) and its planes,
refractive index, specific gravity, dispersion, pleochroism, heat sensitivity, whether it is opaque (a cabochon only),
its preferred cut, its typical rough carats and clarity, its value per carat, an element affinity and strength (what it
gives jewellery), what the rough looks like (its item is named for that) and a description (shown once it is known).
Players tell gems apart by these very numbers - keep them distinct, or two stones become indistinguishable. Hardness and
toughness also set how forgiving the timing bars are when the stone is split, sawn and faceted (`CraftLapidary.Difficulty`),
and whether a stone without a cleavage can be nicked and snapped (toughness under 0.65) or must be sawn.

A **cut** has its outline's length ratio, whether it is a cabochon, its yield (share of the outline's rough left: × 2.2
what it keeps of what lies inside its outline, never more than all of it - from 0.46 up a cut keeps it all) and its
**tiers** in the order they are cut: each a name, a kind (Pavilion, Girdle, Crown, Table, Base, Dome), the facets in it
and an angle offset from the stone's ideal (stars and breaks sit shallower). A design's sheet part (kind Hide) says in **Leather ▸ Fabric**
whether it is cut from leather, cloth or either.

## Abrasives, moulds and patterns

An **abrasive** has its kind (Brush, Wheel, File, Polish, Whetstone), how fast it cuts and takes scale, the finish it
leaves, the heat it puts in, the hardest metal it bites, whether it works wood or leather and the apex a whetstone
leaves (it is a station's tool, not an item: none is used up). A **mould** has a size or a profile (width and thickness
along its length), an eye (where, how wide) or makes fasteners (kind, count, grams each); besides the stock moulds every
castable metal part of a design (a profile of two points or more) gets a pattern mould of its own (`cast:design/role`),
in the part's shape. A **pattern** is rows of `X` (the cells it covers), top row first; a stitch joint's count is its
holes.

## Designs and templates

A **design** is an item that can be made:

* `kind` (Weapon, Armor, Tool, Shield, Jewelry), weapon category, tool type, damage form, scaling, armour slot;
* **components**: each with a role, kind (Metal, Hide, Wood, Fasteners, Gem or a supply), function (Edge, Striking, Guard,
  Grip, Counterweight, Protection, Backing, Strap, Fastener, Padding, Wrap, Thread, Stone, Setting, Band), count,
  optional, importance, and what
  it should be — a **shape target** for metal and wood (length range, profile points of width and thickness, edge kind
  and where it runs, edge angle - the edge's best inclusive angle, 70° at the most (35° a side, the steepest bevel);
  where the profile is too narrow for it (a bevel reaches the middle of a double edge at the most), the edge is judged
  at the angle the section carries there, on the strip, the stone and by the evaluator alike - tang, curvature, dish,
  eye, mass range, whether it is hardened), a **leather spec**
  (pattern, thickness, tannage, stiffness), a **wood spec**, a fastener kind and count, a supply kind and count;
* **joints**: kind (Fit, Peen, Rivet, Stitch, Wrap, Wedge, Socket, Buckle, Solder, Bezel Set, Prong Set), the two roles,
  a count (stitches, rivets, turns, prongs) and an importance. A hilted blade's tang is measured from the piece (the
  narrow run at its end, against the blade's profile: give the tang its own width up to `tangTo`, then the shoulder):
  at the shortest blade it must be as long as the longest grip and pommel and 3 mm to peen - the rules check warns
  otherwise - and the guard's and pommel's `eyeDiameter` should be the tang's section and half a millimetre. A buckle in
  a design with no thread is riveted on: count its rivets in the rivet set. A Socket's pins are the design's rivet part
  (its fastener count: a spear's one pin, a halberd's two langet rivets), usually optional; they go through once the
  head is driven home;
* **jewellery** (kind Jewelry, an armour slot - Ring, Amulet, Helmet for a circlet, Bracers for a bracelet): metal parts
  with a function **Band** (and a **Jewel Part**: Band, Bail, Chain) or **Setting** (a bezel or prong head, by its joint
  with the stone), a part of kind **Gem** with function **Stone** and a **Gem spec** (carats from-to, faceted and/or
  cabochon), a Solder supply; their metal parts need no shape profile;
* **menu group**: where a station's Assembly menu lists it, as "Category/Group" ("Weapons/Swords", "Armour/Plate",
  or your own: "Heirlooms/Knives"); empty = worked out from its kind and parts (`CraftDesignMenu`);
* the numbers a well-made one reaches: damage, critical chance and multiplier, knockback, reference mass (inside the
  main part's mass range), ideal balance, tool damage, defence, magic defence, durability, price (a finished item is
  worth it × (0.3 + 1.4 × its quality), and its parts' worth on top), inventory grid size.

*Validate Crafting Database* (chapter 07) checks each design against itself: a part's own profile (its pattern mould,
the anvil's target) outside its mass range in steel or in bronze, an eye leaving less than 1 mm of metal each side, a
tang too short for the grip and pommel, a wooden part no blank or log of a tough-enough wood can give, a reference mass
outside the main part's range, a leather seam no thread is strong enough for, an edge angle steeper than bevels are
ground, an edge its section is too narrow for along most of it (give it the angle the section carries) or so thick
somewhere that its bevels meet steeper than a whetstone is held (35° a side: it could not be honed there), and ranges
that run backwards.

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
| Per Item | 1.2 | experience for a finished item, × (0.5 + its quality) × how much work the design is (1.4 × √(its price / Work Price), 0.4-3) × the repeat factor |
| Work Price | 45 | the price of an ordinary piece of work: one worth four times as much teaches twice as much |
| Repeats Halve | 15 | each item of a design already made teaches less: × 1 / (1 + made / this) |
| Work Per Minute | 0.35 | the most small work (blows, strokes, a bobbin spun) teaches a minute in one craft |
| Per Insight | 0.5 | experience for each insight, in its craft |
| Apprentice, Journeyman, Expert, Master | 0.15, 0.35, 0.55, 0.78 | the mastery each rank starts at |
| Fatigue Relief | 0.4 | how much less a master's arm tires |
| Finishing Touch | 0.12 | the share of what an item still lacks a master's finishing adds to its quality |
| Routine Low, Routine High | 0.55, 0.9 | the quality of work done by routine at the rank that allows it, and at full mastery |
