# 05 — The Default Content

**Spoilers.** This is everything the built-in Crafting Database holds (`CraftDefaults`), and what it does when worked.
Players are meant to find it out at the anvil.

## Elements

| Element | Id | Density (g/cm³) | Melts (°C) | Before it is known |
|---|---|---|---|---|
| Iron | `iron` | 7.87 | 1538 | a grey metal that rusts |
| Carbon | `carbon` | 2.2 | 3550 | it cannot be seen in the metal |
| Manganese | `manganese` | 7.2 | 1246 |  |
| Nickel | `nickel` | 8.9 | 1455 | a pale, slightly golden grey metal |
| Silica | `silica` | 2.65 | 1710 |  |
| Phosphorus | `phosphorus` | 1.8 | 44 |  |
| Sulfur | `sulfur` | 2.07 | 115 |  |
| Copper | `copper` | 8.96 | 1085 | a soft, rosy metal |
| Tin | `tin` | 7.3 | 232 | a soft, bright metal that crackles when bent |
| Zinc | `zinc` | 7.14 | 420 | a bluish-grey metal |
| Lead | `lead` | 11.34 | 327 | a dull, very heavy, very soft metal |
| Silver | `silver` | 10.49 | 962 | a bright white metal |
| Gold | `gold` | 19.3 | 1064 | a heavy yellow metal that never tarnishes |
| Mythril | `mythril` | 3.2 | 1820 | a pale blue, feather-light metal with a pearly sheen |
| Starmetal | `starmetal` | 8.5 | 1600 | a dark metal with a faint inner shimmer, like a night sky |
| Orichalcum | `orichalcum` | 8.2 | 1050 | a fiery red-gold metal, warm to the touch |

## Material classes

What a metal is called follows from its composition alone: the classes are tried highest priority first, and of equal
priority the one listed first (no two built-in classes of one base and one priority overlap; *Validate* warns of any
that do). *Worth ×* is the class's price multiplier (`priceMultiplier`): what its making adds to its matrix - the base
and what is alloyed in no dearer than it; a dear metal alloyed in (the mythril of a mythril steel, the orichalcum of an
orichalcum bronze) counts at its own worth (see *What things are worth*).

| Class | Base | Ranges | Worth × | |
|---|---|---|---|---|
| Cast iron | Iron | Carbon 2.1-100% | 0.6 | Too much carbon: it casts well and cannot be forged. |
| Starmetal steel | Iron | Starmetal 3-100%, Carbon 0.25-2.1% | 2.5 | Steel with fallen-star metal in it. |
| Copper matte | Copper | Sulfur 2-100% | 0.3 | Copper still full of sulfur: roast it and smelt it again. |
| Meteoric iron | Iron | Nickel 4-20%, Starmetal 1-100%, Carbon 0-0.25% | 2 | Fallen-star iron: nickel and starmetal. |
| Mythril steel | Iron | Mythril 5-50% | 2.5 |  |
| Phosphoric iron | Iron | Phosphorus 0.2-100%, Carbon 0-0.3% | 1 | Iron from bog ore: hard, and brittle in the cold. |
| Orichalcum bronze | Copper | Orichalcum 5-50% | 2 |  |
| Nickel steel | Iron | Nickel 1-10%, Carbon 0.25-2.1% | 2.5 |  |
| Nickel silver | Copper | Nickel 10-30%, Zinc 10-35% | 1.5 |  |
| Wrought iron | Iron | Carbon 0-0.08% | 1 | Iron with hardly any carbon: tough, soft, it does not harden. |
| Bell bronze | Copper | Tin 16-35% | 1.4 | Too much tin: very hard, very brittle. |
| Leaded bronze | Copper | Tin 4-16%, Lead 3-30% | 1.2 | Casts well; softer. |
| Bronze | Copper | Tin 4-16%, Zinc 0-10% | 1.6 | Copper with about a tenth of tin: hard, tough, it casts well and hardens when hammered cold. |
| Brass | Copper | Zinc 10-45% | 1.5 | Copper and zinc (a tenth or more of zinc makes a brass, tin or no tin). |
| Electrum | Gold | Silver 15-50% | 0.7 | Gold and silver together, as rivers give it. |
| Rose gold | Gold | Copper 15-35%, Silver 0-15% | 0.8 | Gold reddened with copper (with as much silver again it is electrum). |
| Mild steel | Iron | Carbon 0.08-0.3% | 1.3 | Low-carbon steel: tough; it hardly hardens. |
| Medium-carbon steel | Iron | Carbon 0.3-0.6% | 1.8 | Hardens well, stays tough: axes, hammers, springy blades. |
| High-carbon steel | Iron | Carbon 0.6-1.2% | 2.4 | Hardens very hard: keen edges, if it is tempered. |
| Ultra-high-carbon steel | Iron | Carbon 1.2-2.1% | 2.4 | The hardest steel - and the most brittle. |
| Pewter | Tin | Copper 0.5-10% | 1.2 |  |
| Silver-bearing lead | Lead | Silver 0.05-30% | 1 | Lead carrying silver: cupel it. |
| Sterling silver | Silver | Copper 5-10% | 1 |  |
| Mythril silver | Mythril | Silver 3-20% | 1.3 |  |
| Coin silver | Silver | Copper 10-25% | 0.9 |  |
| Fine silver | Silver | Copper 0-5% | 1.1 |  |
| Fine gold | Gold | Silver 0-2%, Copper 0-2% | 1.1 |  |
| Copper | Copper | Tin 0-4%, Zinc 0-10% | 1 |  |
| Tin | Tin |  | 1 |  |
| Lead | Lead |  | 1 |  |
| Mythril | Mythril |  | 1 |  |
| Zinc | Zinc |  | 1 |  |
| Nickel | Nickel |  | 1 |  |
| Orichalcum | Orichalcum |  | 1 |  |
| Gold | Gold |  | 1 |  |

There is no class for the starmetal itself: it is only ever a few per cent of a fallen star's iron, and nothing
separates it.

## Stock metals

The bars a merchant sells (and a test station gives), 20 cm × 3 cm × 6 mm unless made otherwise, priced by the one
value rule (their metal, their class's making on its matrix: a bar of tool steel is worth 2.4 times wrought iron's,
gram for gram, as it is in a sword). Each sits well inside its class: a twentieth more or less of what is alloyed in
names it the same. The jeweller's **wire** and **bezel strip** of the jewellery metals are the same metals
drawn or rolled (see *Gems, cuts and jewellery*); ready-made **fasteners** are iron, sold as ten rivets or ten studs
(1.21) or two buckles (2.31): their metal and the making of each.

| Stock | Composition | Class | Base hardness, soft (HV) | A bar weighs (g) | Price |
|---|---|---|---|---|---|
| Wrought iron | Iron 99.94%, Carbon 0.04%, Phosphorus 0.02% | Wrought iron | 98 | 283 | 1.13 |
| Mild steel | Iron 99.75%, Carbon 0.18%, Manganese 0.07% | Mild steel | 90 | 282 | 1.46 |
| Spring steel | Iron 99.35%, Carbon 0.55%, Manganese 0.1% | Medium-carbon steel | 90 | 279 | 2.00 |
| Tool steel | Iron 98.95%, Carbon 0.95%, Manganese 0.1% | High-carbon steel | 90 | 276 | 2.63 |
| Nickel steel | Iron 96.55%, Carbon 0.75%, Nickel 2.5%, Manganese 0.2% | Nickel steel | 113 | 279 | 2.84 |
| Copper | Copper 99.8%, Iron 0.2% | Copper | 48 | 322 | 3.22 |
| Bronze | Copper 89%, Tin 11% | Bronze | 123 | 315 | 5.52 |
| Brass | Copper 70%, Zinc 30% | Brass | 82 | 300 | 4.49 |
| Tin | Tin 100% | Tin | 6 | 263 | 7.88 |
| Sterling silver | Silver 92.5%, Copper 7.5% | Sterling silver | 81 | 373 | 172.7 |
| Gold | Gold 99%, Silver 1% | Fine gold | 27 | 689 | 3005 |
| Mythril | Mythril 98.5%, Silver 1.5% | Mythril | 316 | 116 | 230 |
| Starmetal steel | Iron 93.25%, Carbon 0.75%, Starmetal 5%, Nickel 1% | Starmetal steel | 103 | 279 | 23.6 |
| Orichalcum bronze | Copper 72%, Tin 8%, Orichalcum 20% | Orichalcum bronze | 175 | 311 | 79.9 |

## Ores

| Ore | Id | Gives | Rock | Reduces from (°C) | Ore in a lump | Price | |
|---|---|---|---|---|---|---|---|
| Red iron ore | `hematite` | Iron, Silica, Phosphorus, Sulfur, Manganese | Quartz | 1050 | 35-70% | 0.6 | Hematite. The common iron ore: rich, with quartz around it. |
| Black iron ore | `magnetite` | Iron, Silica, Phosphorus, Sulfur | Quartz | 1100 | 45-80% | 0.85 | Magnetite. Rich and clean, slow to reduce. |
| Bog iron | `bog_iron` | Iron, Silica, Phosphorus, Sulfur, Manganese | Clay | 950 | 40-75% | 0.5 | Limonite from the bogs: easy to smelt, but full of phosphorus. |
| Green copper ore | `malachite` | Copper, Silica, Iron | Limestone | 900 | 25-60% | 0.9 | Malachite. Smelts easily into clean copper; it often sits in limestone. |
| Brassy copper ore | `chalcopyrite` | Copper, Iron, Sulfur, Silica | Quartz | 1100 | 30-60% | 0.9 | Chalcopyrite: copper and iron bound with sulfur. Roast it, or it gives matte. |
| Tin stone | `cassiterite` | Tin, Iron, Silica | Quartz | 1000 | 15-45% | 2.3 | Cassiterite. Heavy: it stays in the pan when the rock washes away. |
| Lead ore | `galena` | Lead, Sulfur, Silver | Quartz | 800 | 30-70% | 0.18 | Galena: lead and sulfur, and a little silver. Roast it, smelt it, cupel the lead (cast it and put it back in the furnace on bone-ash cupels): the silver is lifted out as a bead. Its silver is 0.01-0.3 % of the mineral: about one deposit in nine smelts to plain lead, too poor to cupel. |
| Native silver | `native_silver` | Silver, Copper, Gold | Quartz | 960 | 15-35% | 50 | Silver as it grows in the rock: melt it out. |
| Gold-bearing quartz | `gold_quartz` | Gold, Silver, Copper | Quartz | 1065 | 2-5% (1.5 kg lumps) | 65 | Gold in quartz: crush it, wash it, melt the gold out. Natural gold carries silver. |
| Calamine | `calamine` | Zinc, Iron, Silica | Limestone | 950 | 30-65% | 1 | Zinc ore. Its zinc boils away in a furnace (only its little iron stays, in the bloom): heat it in a covered crucible with molten copper and the copper drinks it (brass). |
| Green nickel ore | `garnierite` | Nickel, Iron, Silica | Clay | 1150 | 30-60% | 0.9 | Garnierite: nickel in a green clay-rock (apple-green, waxy, crumbly). Its nickel does not melt in the furnace but gathers in a bloom, as iron does; melted with copper and zinc it makes nickel silver. |
| Fallen star | `meteorite` | Iron, Nickel, Starmetal, Phosphorus, Carbon | Quartz | 1000 | 75-95% (2.5 kg lumps) | 52 | Iron from the sky, with nickel and starmetal. Heat it and it welds into a bloom. |
| Mythril vein | `mythril_ore` | Mythril, Silver, Silica | Quartz | 1350 | 30-50% | 200 | It will not give up its metal without a very hot furnace and an arcane flux. |
| Orichalcum ore | `orichalcum_ore` | Orichalcum, Copper, Sulfur, Silica | Quartz | 1000 | 25-55% | 110 | A sulfide like copper's: roast it first. |

*Ore in a lump* is the share of a lump (1 kg unless said) that is ore rather than rock; how rich that ore is in its
metal differs from seam to seam (`yields`). The price follows one rule: a lump costs about what the metal of the poorest
lump of the poorest seam is worth (by its class, as a bar is), and a usual lump's metal is worth 2-3 times that (galena
4.5: its worth is the silver a cupel takes out of it). Its dirt - loose earth and caked clay, 3-12 % of a lump (1-4 %
of a fallen star's, 8-20 % of bog iron's and garnierite's clay) - is what the brushes and the pan take off.

Every ore smelts in the furnace. The crucible takes only an ore whose metal melts at a crucible's heat — green copper
ore, tin stone, native silver, gold-bearing quartz — and calamine, whose zinc goes into molten copper; never iron ore,
the fallen star, the nickel ore or the mythril vein, nor a sulfide (brassy copper ore, galena, orichalcum ore: roast and
smelt them in the furnace). In the crucible the ore's rock floats on the melt as slag: skim it off.

## Fuels, quenchants and vat agents

| Fuel | Id | Hottest (°C) |
|---|---|---|
| Charcoal | `charcoal` | 1350 |
| Coal | `coal` | 1450 |
| Coke | `coke` | 1550 |
| Firewood | `firewood` | 950 (any wood in the bag burns as it: planks, logs, offcuts, wooden blanks) |

The heart of a forge, deep in front of the tuyere, runs up to 6 % hotter than the fire while the bellows blow: a blown
charcoal forge reaches some 1420 °C there - every stock metal's welding heat (wrought iron's is the highest, about
1310 °C) - while the edge of the coals stays far cooler.

| Quenchant | Id | Severity | Vapour blanket | Where cracks start (`CrackHarshness`) | A tankful takes |
|---|---|---|---|---|---|
| Water | `water` | 1 | 0.55 | 1 - harsh | nothing (the well) |
| Brine | `brine` | 1.6 | 0.15 | 1.6 - harsh | Rock Salt |
| Quenching oil | `oil` | 0.38 | 0.25 | 0.17 | Quenching Oil |
| Frostwater | `frostwater` | 1.25 | 0.05 | 0.5 - harsh | Frostwater |
| Dragon's blood | `dragon_blood` | 0.75 | 0.2 | 0.45 - harsh | Dragon's Blood |

Frostwater lends ice and dragon's blood fire to the steel they harden. A harsh quenchant (`CraftThermal.HarshQuench`,
0.4) cracks a blade of hardening steel quenched bare - a thin edge almost always, a full one often - and the quench
says so before it goes in; oil hardens it whole (less hard). A station's tank holds its *Quench Medium*; it can be
emptied and filled with any of its *Quenchants* (Quench ▸ *Fill the tank*), the item above used up - *Create Stock
Items* makes them (stock kind Quenchant). The editor's presets with a tank keep water, brine and quenching oil
(`CraftThermal.PresetQuenchants`), and a test station has every one, free.

| Agent | Id | Kind | How fast it works | Looks like |
|---|---|---|---|---|
| Water | `water` | Water | soaks a hide through in about 1 h | clear water |
| Lime water | `lime` | Lime | loosens the hair in about a day (`actionHours` 24, at 20 °C; faster warm, stronger) | milky, caustic lime water |
| Oak bark liquor | `bark` | Vegetable | tans a 4 mm hide through in 72 h | a brown, bitter-smelling liquor |
| Alum and salt | `alum` | Mineral | 18 h | a clear, salty, puckering liquor |
| Oil emulsion | `brain_oil` | Oil | 30 h | a creamy, greasy emulsion |
| Alchemical tanning essence | `tanning_essence` | Alchemical | 8 h | a violet liquor that smells of smoke |
| Vinegar pickle | `vinegar` | Acid | tans nothing: it eats the scale off metal, then etches the bright metal (a pattern-welded blade shows its layers), at the pace `actionHours` 6 sets | sharp-smelling vinegar |

A tanning liquor's hours (`hoursThrough`) are at its best warmth and full strength; lime's and the pickle's work is set
by `actionHours`.

Each liquor has its own colour in the vats: water blue, lime milky white, bark brown, alum pale, the oil emulsion creamy, the
alchemical essence violet, vinegar pale amber.

## Hides and woods

| Hide | Id | Cells | Weighs (a raw hide, a bolt) | Price | |
|---|---|---|---|---|---|
| Cow hide | `cow` | 18x13 | 9.0 kg | 8 | Big and thick along the back: armour, soles, belts. |
| Deer hide | `deer` | 10x8 | 1.4 kg | 6 | Thin and supple: gloves, wraps, garments. |
| Boar hide | `boar` | 12x9 | 4.0 kg | 5 | Tough and bristly, scarred. |
| Wolf pelt | `wolf` | 11x8 | 1.9 kg | 9 | A thick fur. |
| Bear hide | `bear` | 16x12 | 8.7 kg | 15 | Very thick, very fatty. |
| Rabbit pelt | `rabbit` | 4x3 | 0.11 kg | 1 | Small and soft: linings. |
| Drake hide | `drake` | 14x10 | 7.4 kg | 60 | Scaled and fire-proof by nature. |
| Snakeskin | `snake` | 18x5 | 0.83 kg | 18 | Thin and scaled: belts, wraps, sheaths. (Skinned giant serpents.) |
| Troll hide | `troll` | 16x12 | 13.0 kg | 60 | Thick as a door: heavy leather armour that shrugs off blows. (Skinned trolls.) |
| Linen cloth (textile) | `linen_cloth` | 14x20 | 0.49 kg | 12 | Smooth, cool linen: shirts, linings, the layers of a gambeson. |
| Wool cloth (textile) | `wool_cloth` | 14x20 | 1.12 kg | 13 | Warm, springy wool: cloaks, hoods, padding. |
| Canvas (textile) | `canvas` | 12x18 | 0.65 kg | 8 | Heavy hemp canvas: the outer shell of a gambeson, bags, sails. |
| Silk cloth (textile) | `silk_cloth` | 12x18 | 0.16 kg | 45 | Light, smooth and strong: robes, fine shirts, the lining of a lord's coat. |
| Cotton cloth (textile) | `cotton_cloth` | 14x20 | 0.42 kg | 9 | Soft and cool: shirts, tunics, quilting. |
| Felt (textile) | `felt` | 12x14 | 1.26 kg | 9 | Thick, fulled wool that does not fray: hats, padding, boot liners. |
| Velvet (textile) | `velvet` | 12x16 | 0.67 kg | 60 | A rich, napped cloth: surcoats and capes for those who can pay. |

A hide weighs what its cells weigh: each cell's area (cm²) × its thickness × the leather's density - 0.75 + 0.45 ×
its moisture g/cm³ (a wet raw hide about 1.1, dry leather about 0.8), cloth 0.23 + 0.25 × its moisture (about 0.25:
linen some 175 g a square metre, wool 400) - and up to 40 % more for the oil in it; a hide that shrinks keeps its mass.
The stock hide and the bolt of cloth weigh what they weigh on the bench. Each animal has its own skin and hair colour.

| Wood | Id | Density | Cleave | Stock blank (mm) | Price | A log (cm) | Log price | |
|---|---|---|---|---|---|---|---|---|
| Ash | `ash` | 0.68 | 0.85 | 2200 × 40 × 40 | 2.5 | 220 | 45 | Springy and tough, and it rives straight: the wood for hafts and shafts. |
| Hickory | `hickory` | 0.8 | 0.6 | 1000 × 40 × 40 | 1.5 | 100 | 27 | The toughest haft wood. |
| Oak | `oak` | 0.75 | 0.9 | 1000 × 40 × 40 | 1.2 | 100 | 21.6 | Hard and heavy; it cleaves true, and splits under shock. |
| Maple | `maple` | 0.7 | 0.5 | 1000 × 40 × 40 | 1.2 | 100 | 21.6 | Fine and hard: grips. |
| Linden plank | `linden` | 0.5 | 0.6 | 1100 × 650 × 16 | 5 | 110 | 13.85 | Light boards for shields: long and wide enough for a kite's tapered board (rip it). |
| Ironwood | `ironwood` | 1.1 | 0.3 | 1000 × 40 × 40 | 8 | 100 | 144 | So dense it sinks; a legend among haft-makers. |
| Yew | `yew` | 0.67 | 0.5 | 1900 × 40 × 40 | 5 | 190 | 90 | Heartwood that resists, sapwood that pulls: the bow wood. |
| Elm | `elm` | 0.6 | 0.15 | 1000 × 40 × 40 | 1 | 100 | 18 | Interlocked grain that will not split: shield boards, mallets, clubs. |
| Walnut | `walnut` | 0.64 | 0.55 | 1000 × 40 × 40 | 2.5 | 100 | 45 | Dark and fine: grips and stocks that look rich. |
| Birch | `birch` | 0.65 | 0.5 | 1000 × 40 × 40 | 0.8 | 100 | 14.4 | Cheap and even: handles and staves. |
| Pine plank | `pine` | 0.48 | 0.75 | 800 × 300 × 20 | 1.5 | 90 | 8.1 | Light and soft: cheap boards; it dents. |

A stock blank is the species' blank (`blankLength` × `blankWidth` × `blankThickness`): an ash stave long enough for a
spear's shaft, a yew one for a longbow, a linden plank for a kite shield's board - saw it to the length a part wants. A
stock log is as long as the species' blank and never shorter than 90 cm, 24 cm across, green and in its bark (moisture
0.6), and is priced by the one rule for wood: its volume of blanks, at half while it is unseasoned. Wood weighs its
water: a species' density is seasoned wood's (12 %), green wood is heavier by the water in it - a log about half again a
seasoned one (the 2.2 m ash log 123 kg) - and the stock log item weighs what the same log weighs on the bench. (An old
blank asset still at the bar's 200 × 30 × 6 mm, or a log at 90 × 24 cm, is given the species' size, and that size's
price and weight, by *Create Stock Items*.)

**Cleave** is how true the wood splits along its grain (`CraftWoodDefinition.cleave`, 0-1): a froe or a wedge in oak or
ash follows the grain; in elm's interlocked grain it strays, wanders across the rays and the maul has to drive it harder,
and the carving knife splits it more readily.

## Supplies

| Supply | Id | Kind | |
|---|---|---|---|
| Limestone | `limestone` | Flux (furnace) | Furnace flux: its lime takes the silica's place in the slag, so the iron stays. |
| Borax | `borax` | Flux (weld, melt cover) | Flux for welding and for covering a melt. |
| Sand | `sand` | Flux (weld) | A cheap welding flux. |
| Arcane flux | `arcane_flux` | Flux (furnace) | Without it, mythril ore gives nothing. |
| Bone-ash cupel | `bone_ash` | Cupel | Soaks up lead as it oxidises; silver stays on it. |
| Linen thread | `linen_thread` | Thread | Ordinary sewing thread. |
| Waxed thread | `waxed_thread` | Thread | Strong and it sheds water. |
| Sinew | `sinew` | Thread | Shrinks tight as it dries. |
| Silk thread | `silk_thread` | Thread | The strongest thread. |
| Wool batting | `wool_padding` | Padding | Padding under armour: it softens blows. |
| Linen padding | `linen_padding` | Padding | Quilted linen padding. |
| Beeswax | `beeswax` | Wax | Sheds water; stiffens leather a little. On wood it is rubbed in (no drying). |
| Neatsfoot oil | `neatsfoot_oil` | Oil | Makes leather supple. |
| Linseed oil | `linseed_oil` | Oil | Protects metal and wood (it dries slowly in the grain: 10 hours a coat); softens leather a little. |
| Black dye | `dye_black` | Dye | Iron and vinegar. |
| Walnut dye | `dye_brown` | Dye | A warm brown. |
| Madder dye | `dye_red` | Dye | A deep red. |
| Woad dye | `dye_blue` | Dye | A dark blue. |
| *(oil, wax and dye)* | | | One item (a flask, a cake, a pot) covers about 12 cells of leather taken up fully (`Cells Per Item`): what a stroke puts into the leather comes out of the one opened. |
| Refractory clay | `refractory_clay` | Clay | Coat the spine before the quench: it stays soft and tough, the edge hardens. |
| Raw wool | `wool_fibre` | Fibre | Spins into coarse wool yarn (0.6), wool yarn (0.85), fine worsted (1.05). |
| Flax tow | `flax_fibre` | Fibre | Spins into tow yarn (0.7), linen thread (1), fine linen thread (1.25). |
| Coarse wool yarn, Wool yarn, Fine worsted | `coarse_wool_yarn`, `wool_yarn`, `fine_wool_yarn` | Thread | Hand-spun wool. |
| Tow yarn, Fine linen thread | `coarse_linen_yarn`, `fine_linen_thread` | Thread | Hand-spun linen. |
| Hemp fibre | `hemp_fibre` | Fibre | Spins into rough hemp cord (0.9), hemp cord (1.2), hemp twine (1.35): bowstrings, lacing. |
| Cotton boll | `cotton_fibre` | Fibre | Spins into slubby cotton yarn (0.5), cotton thread (0.8), fine cotton thread (0.95). |
| Silk floss | `silk_floss` | Fibre | Spins into raw silk yarn (1.2), silk yarn (1.4), silk thread (1.5). |
| Rough hemp cord, Hemp cord, Hemp twine | `coarse_hemp_cord`, `hemp_cord`, `hemp_twine` | Thread | Hand-spun hemp. |
| Slubby cotton yarn, Cotton thread, Fine cotton thread | `coarse_cotton_yarn`, `cotton_thread`, `fine_cotton_thread` | Thread | Hand-spun cotton. |
| Raw silk yarn, Silk yarn | `raw_silk_yarn`, `silk_yarn` | Thread | Hand-spun silk. |
| Horsehair | `horsehair` | Thread | Plaited: bowstrings and stiff stitching (1.1). |
| Felt padding | `felt_padding` | Padding | The best padding under a helm (1.15). |
| Spirit varnish | `spirit_varnish` | Varnish | Dries fast to a bright, thin film (1 hour a coat). |
| Oil varnish | `oil_varnish` | Varnish | Slow to dry (8 hours a coat), tough and weatherproof (strength 1.25). |
| Pine resin sealer | `pine_resin` | Varnish | Resin cooked in oil: a hard, amber film that stands wear and wet (5 hours a coat; strength 1.1). |
| Tung oil | `tung_oil` | Oil | Soaks in and hardens: it keeps water out of the grain without a film (6 hours a coat). |
| Soft soap | `soap` | Detergent | Lifts grease from fleece in warm water. |
| Washing soda | `washing_soda` | Detergent | Strong: it scours fast, hot water and all (1.3). |
| Fuller's earth | `fullers_earth` | Detergent | A clay that soaks up grease; gentle on wool (0.8). |
| *(compounds and solders)* | | | The lapidary's polishing compounds (tripoli, cerium oxide, alumina, diamond paste) and the jeweller's silver and gold solder: see *Gems, cuts and jewellery*. |

**Fibre, thread and cloth** are worth more at each step, never many times more. Raw wool and flax tow cost 0.5 for
100 g, hemp 0.4, cotton 0.3 for 80 g, silk floss 1.2 for 50 g. A skein of thread (20 m, a thread item) is priced by the
fibre in it - about 5 g of a middling yarn - × 1.8 for the coarse tier, 2.2 the even, 2.6 the fine: wool and linen 0.045,
0.055, 0.065; hemp 0.035, 0.045, 0.05; cotton 0.035, 0.04, 0.05; silk 0.2, 0.25, 0.3 (waxed thread 0.08, sinew and
horsehair 0.06). A bolt is worth more than the yarn it is woven from at its finest (12 m a cell). *Validate* warns
where a skein costs more than four times the fibre in it, or a bolt less than its yarn.

**What one item weighs** (`CraftSupplyDefinition.ItemGrams`; none of the built-in supplies sets its own): a fibre its
grams (100 g; cotton 80, silk floss 50), a batting of padding 300 g, a skein of thread its 20 m of middling yarn - about
5 g (`CraftFibre.SkeinGrams`), a pallion of solder half a gram, anything else 300 g. The bag item weighs that, and so does
the item it goes into: a padded gambeson weighs its four panels and three battings (about 1.3 kg), a padded jack, an
arming cap or quilted trousers their padding, a ring its solder - and each is worth the price of what went into it.

The wood finishes each dry at their own pace (`dryingHours`, process hours a coat: spirit varnish 1, the pine resin
sealer 5 and tung oil 6, oil varnish 8, linseed oil 10) and each has its own colour in the woodshop's pot. One item of a
finish lays about two full-length coats at the finishing bench. An oil or a wax opened for a hide and for wood is the
same pot: the player's open pot of each supply at a station serves the tannery and the finishing bench alike.

In the furnace go only its fluxes — limestone, the arcane flux — and the bone-ash cupel (the Flux menu there offers
nothing else); borax covers a melt in the crucible; sand is for welding only.

## Abrasives

| Tool | Id | Kind | Bites up to (HV) |
|---|---|---|---|
| Wire brush | `wire_brush` | Brush | 2000 |
| Scraper | `scraper` | Brush | 2000 |
| Coarse wheel | `coarse_wheel` | Wheel | 2000 |
| Fine wheel | `fine_wheel` | Wheel | 2000 |
| Bastard file | `bastard_file` | File | 600 |
| Smooth file | `smooth_file` | File | 620 |
| Needle file | `needle_file` | File | 620 |
| Rasp | `rasp` | File | 150 |
| Carving knife | `carving_knife` | File | 60 |
| Coarse emery | `coarse_emery` | Polish | 2000 |
| Fine emery | `fine_emery` | Polish | 2000 |
| Polishing rouge | `rouge` | Polish | 2000 |
| Coarse whetstone | `coarse_stone` | Whetstone | 2000 |
| Medium whetstone | `medium_stone` | Whetstone | 2000 |
| Fine whetstone | `fine_stone` | Whetstone | 2000 |
| Leather strop | `strop` | Whetstone | 2000 |

A polish clears scratches up to six times its own finish at its pace (coarse emery 3 µm, fine emery 1, rouge 0.2 - after
the fine wheel's 6 and the coarse wheel's 25: the fine emery is done for the rouge at 1.2 µm); a whetstone wears a flat edge at 0.06 mm² a stroke × its finish over 8 µm
(the coarse stone fastest, the strop not at all) and leaves the apex it is made for (coarse 18 µm, medium 7, fine 2.5,
the strop 1), coarser on a soft metal or under an unsteady hand.

## Moulds and patterns

| Mould | Id | Size (mm) | Takes (g of mild steel / bronze) |
|---|---|---|---|
| Ingot | `ingot` | 120x40x20 | 752 / 839 |
| Bar | `bar` | 250x30x12 | 705 / 787 |
| Plate | `plate` | 250x200x3 | 1175 / 1311 |
| Blade blank | `blade_blank` | 600x14x7, a blade's outline: 14 mm wide at the tang end, 45 mm by a seventh of its length, 35 mm near the far end, 10 mm at the point | 1157 / 1291 |
| Dagger blank | `dagger_blank` | 430x12x6, its tang stepped down to 12 mm wide over the first 46% (the dagger's) | 304 / 340 |
| Axe head | `axe_head` | 170x40x30, its 28 mm eye near the butt | 938 / 1047 |
| Mace head | `mace_head` | 90x40x40, a 28 mm eye through its middle | 1025 / 1144 |
| Spear head | `spear_head` | 280x32x32, the spear's socket (a 26 mm bore) at its foot | 615 / 686 |
| Guard | `guard` | 200x22x12, a 13 mm slot | 401 / 448 |
| Pommel | `pommel` | 50x40x18, an 11 mm hole | 269 / 300 |
| Shield boss | `boss` | 160x160x4 | 802 / 895 |
| Pick head | `pick_head` | 450x10x10, swelling to 35x30 round its 30 mm eye | 1775 / 1981 |
| Rivets | `rivets` | 14x5x5 (12 of 5 g) | 60 |
| Studs | `studs` | 10x8x4 (24 of 4 g) | 96 |
| Buckles | `buckles` | 50x6x6 (2 of 30 g) | 60 |

Besides these, every castable metal part of a design (65 of them) has a **pattern mould** of its own, named for the
design and the part (*Hatchet: Hatchet head*, `cast:hatchet/head`): the part's own profile at the middle of its length
range, with its eye, so what is cast there is meant for that part. Each such part's profile lies inside its mass range in
steel, bronze and brass (the heavy ones at 85-90 % of the range's top in steel): the hatchet's head takes 552 g of mild
steel or 616 g of bronze (two stock bronze bars), the arming sword's blade 1291 g of steel.

| Pattern | Id | Cells |
|---|---|---|
| Jerkin body | `jerkin_body` | 44 |
| Strap | `strap` | 4 |
| Belt strap | `long_strap` | 10 |
| Cap crown | `cap` | 7 |
| Bracer | `bracer` | 6 |
| Glove | `glove` | 6 |
| Boot upper | `boot_upper` | 11 |
| Sole | `sole` | 3 |
| Helm liner | `liner` | 9 |
| Grip wrap | `grip_wrap` | 3 |
| Gambeson panel (cloth) | `gambeson_panel` | 38 |
| Cloak half (cloth) | `cloak_half` | 43 |
| Hood side (cloth) | `hood` | 17 |
| Trouser leg (cloth) | `trouser_leg` | 31 |
| Cuirass panel | `cuirass_panel` | 38 |
| Leg panel | `chaps_leg` | 20 |
| Shoulder cop | `pauldron` | 13 |
| Tall boot upper | `tall_boot` | 20 |
| Sandal strap | `sandal_strap` | 5 |
| Shield facing | `facing` | 21 |
| Tunic body (cloth) | `tunic_body` | 34 |
| Sleeve (cloth) | `sleeve` | 13 |
| Shirt body (cloth) | `shirt_body` | 23 |
| Coif side (cloth) | `coif` | 12 |
| Mitten (cloth) | `mitten` | 5 |
| Tabard panel (cloth) | `tabard_panel` | 32 |
| Sash (cloth) | `sash` | 12 |
| Foot wrap (cloth) | `foot_wrap` | 12 |
| Robe panel (cloth) | `robe_panel` | 58 |
| Hat brim (cloth) | `hat_brim` | 12 |
| Cape (cloth) | `cape` | 33 |

## Designs

| Design | Id | Kind | Parts | Joints |
|---|---|---|---|---|
| Dagger | `dagger` | Weapon | Blade, Guard, Grip, Pommel, Grip wrap (optional) | Guard on the tang (Fit), Grip on the tang (Fit), Pommel peened on (Peen), Grip wrap (Wrap) |
| Arming sword | `arming_sword` | Weapon | Blade, Guard, Grip, Pommel, Grip wrap (optional) | Guard on the tang (Fit), Grip on the tang (Fit), Pommel peened on (Peen), Grip wrap (Wrap) |
| Greatsword | `greatsword` | Weapon | Blade, Guard, Grip, Pommel, Grip wrap (optional) | Guard on the tang (Fit), Grip on the tang (Fit), Pommel peened on (Peen), Grip wrap (Wrap) |
| Battle axe | `battle_axe` | Weapon | Axe head, Haft | Head on the haft (Wedge) |
| Spear | `spear` | Weapon | Spear head, Shaft, Socket pin (optional) | Head on the haft (Socket) |
| Mace | `mace` | Weapon | Mace head, Haft | Head on the haft (Wedge) |
| Pickaxe | `pickaxe` | Tool | Pick head, Haft | Head on the haft (Wedge) |
| Hatchet | `hatchet` | Tool | Hatchet head, Haft | Head on the haft (Wedge) |
| Leather cap | `leather_cap` | Armor | Crown, Thread | Crown seam (Stitch) |
| Leather jerkin | `leather_jerkin` | Armor | Body, Shoulder strap ×2, Buckles, Thread | Straps sewn on (Stitch), Buckles (Buckle) |
| Studded leather armour | `studded_armor` | Armor | Body, Studs, Padding, Thread | Studs set (Rivet), Padding sewn in (Stitch) |
| Plated bracers | `plated_bracers` | Armor | Bracer leather ×2, Plate ×2, Rivets | Plates riveted on (Rivet) |
| Plated gloves | `gauntlets` | Armor | Glove ×2, Hand plate ×2, Rivets, Thread | Seams (Stitch), Plates riveted on (Rivet) |
| Leather boots | `leather_boots` | Armor | Upper ×2, Sole ×2, Thread | Soles sewn on (Stitch) |
| Plate helm | `plate_helm` | Armor | Helm shell, Liner, Padding, Rivets | Liner riveted in (Rivet) |
| Breastplate | `breastplate` | Armor | Breastplate, Strap ×2, Buckles, Rivets, Padding | Straps riveted on (Rivet), Buckles (Buckle) |
| Belt | `belt` | Armor | Strap, Buckle, Rivet | Buckle riveted on (Buckle) |
| Round shield | `round_shield` | Shield | Board, Boss, Grip strap, Rivets | Boss riveted on (Rivet), Grip riveted on (Rivet) |
| Padded gambeson | `padded_gambeson` | Armor | Outer shell ×2 (cloth), Lining ×2 (cloth), Batting, Thread | Quilting through the batting (Stitch), Side and shoulder seams (Stitch) |
| Wool cloak | `wool_cloak` | Armor (Cloak) | Cloak half ×2 (cloth), Clasp, Thread | Back seam (Stitch), Clasp sewn on (Buckle) |
| Hood | `cloth_hood` | Armor (Helmet) | Hood side ×2 (cloth), Thread | Crown seam (Stitch) |
| Quilted trousers | `quilted_trousers` | Armor (Leggings) | Trouser leg ×2 (cloth), Batting, Thread | Inseams and quilting (Stitch) |
| Shortsword | `shortsword` | Weapon (Sword) | Blade, Guard, Grip, Pommel, Grip wrap (optional) | Guard on the tang (Fit), Grip on the tang (Fit), Pommel peened on (Peen), Grip wrap (Wrap) |
| Longsword | `longsword` | Weapon (Sword) | Blade, Guard, Grip, Pommel, Grip wrap (optional) | Guard on the tang (Fit), Grip on the tang (Fit), Pommel peened on (Peen), Grip wrap (Wrap) |
| Sabre | `sabre` | Weapon (Sword) | Blade, Guard, Grip, Pommel, Grip wrap (optional) | Guard on the tang (Fit), Grip on the tang (Fit), Pommel peened on (Peen), Grip wrap (Wrap) |
| Falchion | `falchion` | Weapon (Sword) | Blade, Guard, Grip, Pommel, Grip wrap (optional) | Guard on the tang (Fit), Grip on the tang (Fit), Pommel peened on (Peen), Grip wrap (Wrap) |
| Rapier | `rapier` | Weapon (Sword) | Blade, Guard, Grip, Pommel, Grip wrap (optional) | Guard on the tang (Fit), Grip on the tang (Fit), Pommel peened on (Peen), Grip wrap (Wrap) |
| Seax | `seax` | Weapon (Dagger) | Blade, Guard, Grip, Pommel, Grip wrap (optional) | Guard on the tang (Fit), Grip on the tang (Fit), Pommel peened on (Peen), Grip wrap (Wrap) |
| Stiletto | `stiletto` | Weapon (Dagger) | Blade, Guard, Grip, Pommel, Grip wrap (optional) | Guard on the tang (Fit), Grip on the tang (Fit), Pommel peened on (Peen), Grip wrap (Wrap) |
| War hammer | `war_hammer` | Weapon (Hammer) | Hammer head, Haft | Head on the haft (Wedge) |
| Maul | `maul` | Weapon (Hammer) | Maul head, Haft | Head on the haft (Wedge) |
| Flanged mace | `flanged_mace` | Weapon (Mace) | Flanged head, Haft | Head on the haft (Wedge) |
| Morning star | `morning_star` | Weapon (Mace) | Ball head, Haft, Spikes | Head on the haft (Wedge), Spikes set (Rivet) |
| Dane axe | `dane_axe` | Weapon (Axe) | Broad axe head, Long haft | Head on the haft (Wedge) |
| Throwing axe | `throwing_axe` | Weapon (Thrown) | Axe head, Short haft | Head on the haft (Wedge) |
| Halberd | `halberd` | Weapon (Spear) | Halberd head, Pole, Langets' rivets (optional) | Head on the haft (Socket) |
| Glaive | `glaive` | Weapon (Spear) | Glaive blade, Pole, Socket rivets (optional) | Head on the haft (Socket) |
| Boar spear | `boar_spear` | Weapon (Spear) | Broad spear head, Shaft, Socket pin (optional) | Head on the haft (Socket) |
| Javelin | `javelin` | Weapon (Thrown) | Javelin head, Light shaft | Head on the haft (Socket) |
| Throwing knives | `throwing_knives` | Weapon (Thrown) | Throwing knife ×3, Grip wrap ×3 (optional) | Grip wraps (Wrap) |
| Hunting bow | `hunting_bow` | Weapon (Bow) | Bow stave, Bowstring, Grip wrap (optional) | Strung (Wrap), Grip wrap (Wrap) |
| Longbow | `longbow` | Weapon (Bow) | Long stave, Bowstring, Grip wrap (optional) | Strung (Wrap), Grip wrap (Wrap) |
| Quarterstaff | `quarterstaff` | Weapon (Staff) | Staff, Grip wrap (optional) | Grip wrap (Wrap) |
| Studded club | `club` | Weapon (Mace) | Club, Iron studs (optional) | Studs driven in (Rivet) |
| Cestus | `cestus` | Weapon (Fist) | Knuckle plate ×2, Hand wrap ×2, Rivets | Plates riveted on (Rivet) |
| Sickle | `sickle` | Tool (Tool, Scythe) | Sickle blade, Handle | Head on the haft (Wedge) |
| Scythe | `scythe` | Tool (Tool, Scythe) | Scythe blade, Snath (steamed into a 25° sweep: the woodshop's snath form) | Head on the haft (Wedge) |
| Felling axe | `felling_axe` | Tool (Tool, Axe) | Felling head, Long haft | Head on the haft (Wedge) |
| Buckler | `buckler` | Shield (Shield) | Buckler plate, Grip, Rivets | Grip riveted on (Rivet) |
| Kite shield | `kite_shield` | Shield (Shield) | Boards, Leather facing, Enarmes ×2, Rivets | Facing riveted on (Rivet), Enarmes riveted on (Rivet) |
| Heater shield | `heater_shield` | Shield (Shield) | Boards, Leather facing, Enarmes ×2, Rivets | Facing riveted on (Rivet), Enarmes riveted on (Rivet) |
| Kettle hat | `kettle_hat` | Armor (Helmet) | Brimmed shell, Liner, Padding, Rivets | Liner riveted in (Rivet) |
| Nasal helm | `nasal_helm` | Armor (Helmet) | Conical shell, Liner, Padding, Rivets | Liner riveted in (Rivet) |
| Great helm | `great_helm` | Armor (Helmet) | Helm barrel, Liner, Padding, Rivets | Liner riveted in (Rivet) |
| Pauldrons | `pauldrons` | Armor (Shoulders) | Shoulder plate ×2, Strap ×2, Buckles, Rivets | Straps riveted on (Rivet), Buckles (Buckle) |
| Greaves | `greaves` | Armor (Leggings) | Greave ×2, Strap ×4, Buckles, Rivets | Straps riveted on (Rivet), Buckles (Buckle) |
| Sabatons | `sabatons` | Armor (Boots) | Foot lame ×2, Upper ×2, Sole ×2, Rivets, Thread | Soles sewn on (Stitch), Lames riveted on (Rivet) |
| Vambraces | `vambraces` | Armor (Bracers) | Vambrace ×2, Strap ×4, Buckles, Rivets | Straps riveted on (Rivet), Buckles (Buckle) |
| Brigandine | `brigandine` | Armor (Chestplate) | Canvas cover (cloth), Small plate ×8, Rivets, Thread | Plates riveted in (Rivet), Seams (Stitch) |
| Scale armour | `scale_armour` | Armor (Chestplate) | Backing, Scale ×10, Thread | Scales laced on (Stitch) |
| Cuir bouilli cuirass | `cuir_bouilli` | Armor (Chestplate) | Cuirass panel ×2, Strap ×2, Buckles, Rivets | Straps riveted on (Rivet), Buckles (Buckle) |
| Leather pauldrons | `leather_pauldrons` | Armor (Shoulders) | Shoulder cop ×2, Strap ×2, Buckles, Thread | Straps sewn on (Stitch), Buckles (Buckle) |
| Leather gloves | `leather_gloves` | Armor (Gloves) | Glove ×2, Thread | Seams (Stitch) |
| Leather bracers | `leather_bracers` | Armor (Bracers) | Bracer ×2, Buckles, Rivets | Buckles riveted on (Buckle) |
| Leather leggings | `leather_leggings` | Armor (Leggings) | Leg panel ×2, Buckle, Thread | Leg seams (Stitch), Buckle (Buckle) |
| Riding boots | `riding_boots` | Armor (Boots) | Tall upper ×2, Sole ×2, Thread | Soles sewn on (Stitch) |
| Sandals | `sandals` | Armor (Boots) | Sole ×2, Sandal strap ×4, Thread | Straps sewn on (Stitch) |
| Sword belt | `sword_belt` | Armor (Belt) | Belt strap, Hanger ×2, Buckles, Rivets | Hangers riveted on (Rivet), Buckles (Buckle) |
| Linen shirt | `linen_shirt` | Armor (Chestplate) | Shirt body ×2 (cloth), Sleeve ×2 (cloth), Thread | Sleeves set in (Stitch), Side seams (Stitch) |
| Wool tunic | `wool_tunic` | Armor (Chestplate) | Tunic body ×2 (cloth), Sleeve ×2 (cloth), Thread | Sleeves set in (Stitch), Side seams (Stitch) |
| Padded jack | `padded_jack` | Armor (Chestplate) | Outer shell ×2 (cloth), Sleeve ×2 (cloth), Batting, Thread | Quilting (Stitch), Sleeves set in (Stitch) |
| Robe | `robe` | Armor (Chestplate) | Robe panel ×2 (cloth), Sleeve ×2 (cloth), Thread | Side seams (Stitch), Sleeves set in (Stitch) |
| Wool trousers | `wool_trousers` | Armor (Leggings) | Trouser leg ×2 (cloth), Thread | Inseams (Stitch) |
| Linen coif | `linen_coif` | Armor (Helmet) | Coif side ×2 (cloth), Thread | Crown seam (Stitch) |
| Arming cap | `arming_cap` | Armor (Helmet) | Coif side ×2 (cloth), Padding, Thread | Quilting (Stitch) |
| Wool hat | `wool_hat` | Armor (Helmet) | Cap crown (cloth), Hat brim (cloth), Thread | Brim sewn on (Stitch) |
| Wool mittens | `wool_mittens` | Armor (Gloves) | Mitten ×2 (cloth), Thread | Seams (Stitch) |
| Tabard | `tabard` | Armor (Cloak) | Tabard panel ×2 (cloth), Thread | Shoulder seams (Stitch) |
| Cape | `cape` | Armor (Cloak) | Cape (cloth), Clasp, Thread | Hem (Stitch), Clasp sewn on (Buckle) |
| Sash | `sash` | Armor (Belt) | Sash (cloth), Thread | Hem (Stitch) |
| Foot wraps | `foot_wraps` | Armor (Boots) | Foot wrap ×2 (cloth), Thread | Hem (Stitch) |

**Main parts.** What the item is judged by first: its edge or striking part (else its protection), the length and
mass its design allows, what a casting of the part's own profile (its pattern mould, or the anvil's target) weighs in
mild steel and in bronze, and the reference mass a weapon or tool is balanced against (inside its main part's range;
where a design has several of the part, each piece is weighed on its own). A socket is counted solid round its bore,
with a 3 mm wall; a pommel is a disc; a head's eye leaves at least 1 mm of metal each side.

| Design | Main part | Length (mm) | Mass (g) | Its own profile weighs (g of steel / bronze) | Reference mass (g) |
|---|---|---|---|---|---|
| Dagger | Blade | 390-490 | 200-410 | 318 / 355 | 315 |
| Arming sword | Blade | 940-1070 | 980-1500 | 1291 / 1440 | 1230 |
| Greatsword | Blade | 1430-1650 | 1990-3100 | 2640 / 2946 | 2400 |
| Battle axe | Axe head | 160-210 | 900-1500 | 922 / 1029 | 1300 |
| Spear | Spear head | 220-320 | 300-700 | 593 / 662 | 590 |
| Mace | Mace head | 80-120 | 700-1200 | 931 / 1038 | 950 |
| Pickaxe | Pick head | 400-500 | 1000-2000 | 1740 / 1942 | 1700 |
| Hatchet | Hatchet head | 130-170 | 450-1000 | 552 / 616 | 550 |
| Plated bracers | Plate ×2 | 170-220 | 120-280 | 179 / 199 | - |
| Plate helm | Helm shell | 220-280 | 750-1100 | 940 / 1049 | - |
| Breastplate | Breastplate | 380-450 | 2200-3200 | 2722 / 3038 | - |
| Round shield | Board | 550-700 | - | - | - |
| Shortsword | Blade | 640-770 | 520-920 | 743 / 829 | 870 |
| Longsword | Blade | 1120-1290 | 1240-1900 | 1612 / 1799 | 1600 |
| Sabre | Blade | 900-1020 | 830-1230 | 1085 / 1211 | 1030 |
| Falchion | Blade | 760-900 | 980-1580 | 1349 / 1505 | 1180 |
| Rapier | Blade | 1060-1220 | 570-930 | 667 / 745 | 660 |
| Seax | Blade | 390-520 | 300-550 | 476 / 531 | 470 |
| Stiletto | Blade | 290-400 | 120-260 | 163 / 181 | 160 |
| War hammer | Hammer head | 120-170 | 600-1000 | 655 / 731 | 650 |
| Maul | Maul head | 180-240 | 2200-3800 | 3336 / 3722 | 3500 |
| Flanged mace | Flanged head | 100-140 | 900-1500 | 1304 / 1456 | 1300 |
| Morning star | Ball head | 80-110 | 800-1300 | 1091 / 1218 | 1100 |
| Dane axe | Broad axe head | 200-260 | 900-1500 | 1178 / 1314 | 1170 |
| Throwing axe | Axe head | 110-150 | 350-650 | 367 / 410 | 600 |
| Halberd | Halberd head | 350-450 | 1600-2600 | 1869 / 2086 | 1850 |
| Glaive | Glaive blade | 450-550 | 1000-1600 | 1363 / 1521 | 1350 |
| Boar spear | Broad spear head | 300-400 | 550-1150 | 982 / 1095 | 970 |
| Javelin | Javelin head | 120-180 | 100-220 | 181 / 202 | 180 |
| Throwing knives | Throwing knife ×3 | 200-260 | 90-200 | 120 / 134 | 150 |
| Hunting bow | Bow stave | 1300-1600 | - | - | 700 |
| Longbow | Long stave | 1700-2000 | - | - | 900 |
| Quarterstaff | Staff | 1700-2000 | - | - | 1600 |
| Studded club | Club | 600-800 | - | - | 1200 |
| Cestus | Knuckle plate ×2 | 80-100 | 40-110 | 63 / 71 | 65 |
| Sickle | Sickle blade | 300-400 | 200-380 | 236 / 263 | 235 |
| Scythe | Scythe blade | 600-750 | 500-950 | 836 / 932 | 830 |
| Felling axe | Felling head | 170-220 | 1100-1700 | 1182 / 1319 | 1400 |
| Buckler | Buckler plate | 280-340 | 650-1400 | 1166 / 1301 | - |
| Kite shield | Boards | 900-1100 | - | - | - |
| Heater shield | Boards | 650-800 | - | - | - |
| Kettle hat | Brimmed shell | 300-360 | 900-1800 | 1489 / 1662 | - |
| Nasal helm | Conical shell | 230-290 | 650-1300 | 798 / 891 | - |
| Great helm | Helm barrel | 300-360 | 1800-3000 | 2388 / 2665 | - |
| Pauldrons | Shoulder plate ×2 | 180-240 | 330-700 | 474 / 529 | - |
| Greaves | Greave ×2 | 320-400 | 450-900 | 530 / 592 | - |
| Sabatons | Foot lame ×2 | 150-200 | 110-260 | 164 / 184 | - |
| Vambraces | Vambrace ×2 | 220-280 | 220-480 | 313 / 350 | - |
| Brigandine | Small plate ×8 | 60-90 | 25-85 | 41 / 46 | - |
| Scale armour | Scale ×10 | 40-60 | 6-30 | 14 / 16 | - |

**Hilted blades.** Each blade's tang (`tangTo` of the blade, measured from the piece: the narrow run at its end) is long
enough, at the design's shortest blade, for its longest grip and pommel and the 3 mm peened over them - with a segment
to spare; the blade proper is as long as before, and the masses, reference masses and ideal balances follow the longer
tang. The guard's slot and the pommel's hole (`eyeDiameter`) are made for the tang: half a millimetre over its section.

| Design | Blade (mm) | Tang | Grip + pommel (mm) | Slot and hole (mm) |
|---|---|---|---|---|
| Dagger | 390-490 | 0.46 | 85-110 + 25-45 | 11 |
| Arming sword | 940-1070 | 0.24 | 95-120 + 40-60 | 12.6 |
| Greatsword | 1430-1650 | 0.30 | 220-280 + 50-70 | 14.2 |
| Shortsword | 640-770 | 0.31 | 90-110 + 35-55 | 11.8 |
| Longsword | 1120-1290 | 0.32 | 180-240 + 45-65 | 13.5 |
| Sabre | 900-1020 | 0.23 | 95-115 + 25-40 | 12 |
| Falchion | 760-900 | 0.27 | 95-115 + 35-50 | 12.8 |
| Rapier | 1060-1220 | 0.21 | 95-120 + 40-55 | 11.2 |
| Seax | 390-520 | 0.49 | 100-130 + 20-35 | 12.8 |
| Stiletto | 290-400 | 0.55 | 85-105 + 20-35 | 9.5 |

**Buckles and threads.** A buckle in a design with no thread is riveted on, and its rivets are in the set with the
straps' (breastplate 10 = 8 for the straps and 2 for the buckles, pauldrons 8, greaves 12, vambraces 12, cuir bouilli
8, sword belt 7, belt 1, leather bracers 2); one in a design with thread is sewn on (4 stitches each). A thread suits
leather or cloth up to a thickness (`CraftTextiles.ThreadNeed`): linen thread 2 mm, horsehair 2.5 mm, sinew 3 mm, fine
linen thread 4 mm, waxed thread 5 mm, hemp twine 6 mm, silk thread 9 mm - the thickest sole.

Where each is made (the station presets; a station lists only these in its Assembly area - a part's metal wants an
anvil or a crucible, its leather a leather bench, its cloth a tailor's table or a leather bench, its wood files or the
woodshop's tools, jewellery a jeweller's bench; optional parts, supplies and fasteners are not asked for):

* **Smithy** (33): the blades, axes, hammers, maces, polearms, thrown weapons, tools - and the bows, quarterstaff and
  club (its files shape wood).
* **Woodshop** (4): the hunting bow, longbow, quarterstaff and studded club.
* **Leatherworks** (30): the leather armour and clothes with no metal plates, and every cloth one (with no tailor's
  table, its leather bench cuts the cloth too).
* **Tailor's** (17): the cloth armour and clothes.
* **Jeweller's** (14): the ten pieces of jewellery (wire and strip come drawn or rolled from the stores: no anvil) - and,
  with its files, the bows, quarterstaff and club.
* **Armourer** (smithy + leather bench + tailor's table: 81, all but the jewellery) or a complete workshop (all 91):
  everything with metal on leather or cloth - plate helms, breastplate, pauldrons, greaves, sabatons, vambraces, plated
  gloves and bracers, brigandine, scale armour, cestus and the four shields - is made only there.
* The **Smeltery**, **Tannery**, **Spinnery** and **Lapidary** assemble nothing (they have no bench).

## Gems, cuts and jewellery

The lapidary's and the jeweller's content (`CraftDefaultsJewelry.cs`). The gems have their real properties, so the
tests tell them apart:

| Gem | Id | Species | Hardness | Toughness | Cleavage | RI | SG | Pleochroism | Heat-sensitive | Cut | Value / ct | Element |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Rock crystal | `rock_crystal` | quartz | 7 | 0.55 | none | 1.544 | 2.65 | 0 | | Round brilliant | 3 | - |
| Amethyst | `amethyst` | quartz | 7 | 0.55 | none | 1.544 | 2.65 | 0.25 | yes | Oval brilliant | 10 | Poison |
| Citrine | `citrine` | quartz | 7 | 0.55 | none | 1.544 | 2.65 | 0.1 | | Cushion | 8 | Fire |
| Almandine garnet | `garnet` | garnet | 7.5 | 0.6 | none | 1.79 | 4.05 | 0 | | Round brilliant | 18 | Fire |
| Peridot | `peridot` | olivine | 6.75 | 0.45 | poor | 1.67 | 3.34 | 0.15 | yes | Oval brilliant | 25 | Earth |
| Blue topaz | `topaz` | topaz | 8 | 0.4 | perfect | 1.62 | 3.53 | 0.15 | | Emerald cut | 20 | Wind |
| Aquamarine | `aquamarine` | beryl | 7.75 | 0.55 | poor | 1.58 | 2.7 | 0.35 | | Emerald cut | 60 | Water |
| Emerald | `emerald` | beryl | 7.75 | 0.35 | poor | 1.58 | 2.72 | 0.4 | yes | Emerald cut | 180 | Earth |
| Ruby | `ruby` | corundum | 9 | 0.8 | none | 1.77 | 4.0 | 0.5 | | Oval brilliant | 300 | Fire |
| Sapphire | `sapphire` | corundum | 9 | 0.8 | none | 1.77 | 4.0 | 0.55 | | Cushion | 250 | Ice |
| Diamond | `diamond` | diamond | 10 | 0.7 | perfect | 2.417 | 3.52 | 0 | | Round brilliant | 900 | Holy |
| Tourmaline | `tourmaline` | tourmaline | 7.25 | 0.5 | none | 1.63 | 3.06 | 0.7 | | Emerald cut | 40 | Lightning |
| Opal (opaque) | `opal` | opal | 5.75 | 0.3 | none | 1.45 | 2.1 | 0 | yes | Oval cabochon | 70 | Water |
| Moonstone (opaque) | `moonstone` | feldspar | 6.25 | 0.4 | perfect | 1.52 | 2.57 | 0 | | Round cabochon | 15 | Dark |
| Turquoise (opaque) | `turquoise` | turquoise | 5.5 | 0.5 | none | 1.62 | 2.7 | 0 | yes | Oval cabochon | 12 | Water |
| Lapis lazuli (opaque) | `lapis_lazuli` | lazurite | 5.5 | 0.45 | none | 1.5 | 2.8 | 0 | | Round cabochon | 10 | Holy |

What that means at the bench:

* **Splitting**: topaz, diamond and moonstone cleave along their plane; every other stone but corundum is nicked and
  snapped (a poor cleavage - peridot, aquamarine, emerald - is not worth following); ruby and sapphire are too tough
  to snap and are sawn.
* **Hardness points** (5, 6, 6.5, 7, 7.5, 8, 9, 9.5): a point scratches only a stone softer than itself, so the
  readings bracket each stone's hardness - quartz between 7 and 7.5, garnet between 7.5 and 8, corundum between 9 and
  9.5, diamond above 9.5.
* **Heat**: amethyst, peridot, emerald, opal and turquoise crack when sawn, ground or polished hot.
* **Opaque** stones are cut en cabochon only.

| Cut | Id | Length : width | Yield | Tiers (facets) |
|---|---|---|---|---|
| Round brilliant | `round_brilliant` | 1 | 0.4 | Pavilion mains (8), lower girdle facets (16), girdle (16), crown mains (8), stars (8), upper girdle facets (16), table (1) |
| Oval brilliant | `oval` | 1.35 | 0.45 | Pavilion mains (8), lower girdle facets (16), girdle (16), crown mains (8), stars (8), table (1) |
| Cushion | `cushion` | 1 | 0.48 | Pavilion (8), girdle (8), crown (8), table (1) |
| Emerald (step) cut | `emerald_cut` | 1.4 | 0.55 | Pavilion steps (12), pavilion keel (8), girdle (8), crown steps (8), table (1) |
| Round cabochon | `cabochon_round` | 1 | 0.65 | Base, dome |
| Oval cabochon | `cabochon_oval` | 1.3 | 0.65 | Base, dome |

| Supply | Id | Kind | |
|---|---|---|---|
| Tripoli | `tripoli` | Compound (polishes up to hardness 6) | Opal, turquoise, lapis and the softer stones. |
| Cerium oxide | `cerium_oxide` | Compound (7.5) | The quartz polish: amethyst, citrine, beryl, peridot. |
| Alumina | `alumina` | Compound (9) | Topaz, corundum. |
| Diamond paste | `diamond_paste` | Compound (10) | It polishes anything - diamond itself. |
| Silver solder | `silver_solder` | Solder | Flows a little below the silver itself (745 °C, `flowTemperature`). |
| Gold solder | `gold_solder` | Solder | Matches the colour of the gold (flows at 760 °C). |

The jeweller works **wire** (600 × 2.5 × 1.8 mm) and **bezel strip** (150 × 4 × 0.5 mm) of copper, brass, bronze,
sterling silver, gold, mythril or orichalcum bronze. The designs (each with solder; the stone's carats, and whether it
may be faceted or a cabochon):

| Design | Id | Worn as | Parts | Stone | Joints |
|---|---|---|---|---|---|
| Bezel ring | `bezel_ring` | Ring (sized to a finger) | Band, Bezel | 0.2-6 ct, either | Bezel soldered on, Stone set (bezel) |
| Solitaire ring | `solitaire_ring` | Ring (sized to a finger) | Band, Prong head | 0.2-5 ct, faceted | Head soldered on, Stone set (4 claws) |
| Signet ring | `signet_ring` | Ring (sized to a finger) | Band, Bezel | 0.5-8 ct, cabochon | Bezel soldered on, Stone set (bezel) |
| Pendant | `pendant` | Amulet | Bail, Bezel, Cord | 0.5-20 ct, either | Bail soldered on, Stone set (bezel) |
| Gem necklace | `gem_necklace` | Amulet | Chain, Prong head | 0.5-15 ct, faceted | Head soldered to the chain, Stone set (6 claws) |
| Amulet | `amulet` | Amulet | Chain, Bezel | 2-30 ct, cabochon | Bezel soldered to the chain, Stone set (bezel) |
| Circlet | `circlet` | Helmet | Band, Bezel | 0.5-20 ct, either | Bezel soldered on, Stone set (bezel) |
| Gem bracelet | `gem_bracelet` | Bracers | Cuff, Prong head | 0.5-12 ct, faceted | Head soldered on, Stone set (4 claws) |
| Brooch | `brooch` | Trinket | Pin, Bezel | 0.5-15 ct, either | Bezel soldered to the pin, Stone set (bezel) |
| Gem charm | `charm` | Trinket | Bail, Claw cage, Cord | 1-25 ct, cabochon | Bail soldered on, Stone set (4 claws) |

A prong head is formed with as many prongs as its design's claws; only the rings take the ring sizer's measure.

## What things are worth

One rule (`CraftValue`) prices the stock a merchant sells, a worked piece taken from a station and the materials of a
finished item alike, so working a thing never conjures value out of nothing:

* **metal**: its grams × its elements' value of a gram, and its class's making on its matrix: the base and what is
  alloyed in no dearer than it × the class's multiplier (*Worth ×* above), a dearer metal alloyed in at its own worth -
  a bar, a blade and the sword alike; welding mythril onto iron or melting orichalcum into copper is worth its parts,
  never twice them; slag in a bloom counts for nothing;
* **wood**: its share by volume of the species' blank (a blank is worth the wood's price), half that while it is
  unseasoned (wetter than 20 %): sawing never adds value, and a log is priced by the same rule;
* **a hide**: its price × its share of the whole hide it came from × (1 + its mean tannage) - tanned through, it is worth
  twice the raw hide (never more than the whole hide); **cloth**: its price × its cells against the bolt's (it has
  nothing to tan) - a length woven longer than a bolt is worth more than a bolt, and cut in two its halves are worth
  the whole;
* **an ore lump**: its price × its ore against a usual lump's (a lump's mass × the middle of its ore share);
* **fasteners**: their metal, and 0.1 for the making of each rivet or stud, 1 for each buckle;
* **fibre** by weight and how well it is prepared, **yarn** by its length (a thread item's price for every 20 m: a skein
  is priced by the fibre in it, see *Supplies*), **a cut stone** by its grade.

A finished item is worth its design's price × (0.3 + 1.4 × its quality), and the worth of its parts on top.

## Insights

The journal's 40 insights (`CraftJournal.Insights`), each learnt once from what went wrong or what was seen - every one
has something in the work that teaches it (the events the workshop raises: a crack by its cause, a flaw the first time
it shows, a note the work tells; and a finished item):

| Id | What the player understands | Taught by |
|---|---|---|
| `burning` | Steel heated to a sparkling white burns: its grain falls apart and nothing brings it back. Forge below a yellow heat. | steel throwing sparks in the fire (it burns); burnt metal crumbling under the hammer |
| `quench_crack` | A hard quench cracks high-carbon steel: the faster the medium, the higher the carbon, the thinner the edge and the more abrupt the shape, the likelier. Oil is gentler than water, water than brine: fill the tank with oil to harden a blade; in water, leave its edge thick and clay its spine. | a crack as the steel hardens, in the tank or out of it (an interrupted quench, an air-hardening steel) |
| `untempered` | Steel straight from the quench is glass-hard and brittle. Heat it gently again (temper it) and it gives up a little hardness for a lot of toughness. | a hardening quench |
| `temper_colours` | On bright steel, heat paints colours: pale straw, straw, bronze, purple, blue. They tell the tempering heat. | the first colour running over bright (ground) steel as it warms |
| `magnet` | Steel stops sticking to a magnet at a cherry-red heat: that is the heat to harden it from. | steel in the fire passing the heat a magnet stops sticking at - at a station with a magnet |
| `cold_forging` | Forged too cold, steel tears and cracks. Iron and steel move at a red to yellow heat; bronze and copper move cold, but harden as they are worked until heat softens them again. | a crack from forging too cold, or from metal worked too hard cold |
| `cast_iron` | Cast iron shatters under the hammer: it has too much carbon. It can be cast, not forged. | cast iron cracking under the hammer |
| `slag` | A bloom is spongy and full of slag: hammer it at welding heat until it closes up and the slag runs out, or it will crumble and hide flaws. | a bloom struck too cold falling apart |
| `scale_pits` | Scale left on the metal is hammered into it as pits. Brush it off, or keep the fire reducing. | scale hammered into the surface, the first time it pits a piece |
| `grind_heat` | Pressed too hard on the wheel, a thin edge overheats and loses its temper: it turns blue and goes soft. Cool it often, press lightly. | an edge whose temper runs on the wheel |
| `file_skate` | A file skates on hardened steel: file and shape before hardening. | a file skating on hardened steel |
| `stone_angle` | On the whetstone, hold the blade at its bevel's own angle or a little steeper: held lower, the stone only rubs the bevel's shoulder and never reaches the edge. | the stone only rubbing the bevel's shoulder |
| `wrong_flux` | Without flux, a forge weld does not take; without lime, a bloomery's slag eats the iron. | a fold or a stack that does not take, never fluxed (its seam cools unset) |
| `sulfur` | Iron with sulfur in it (from a coal fire, or an unroasted ore) tears at forging heat. Charcoal is cleaner; roast sulfide ores first. | iron with sulfur tearing at forging heat |
| `phosphorus` | Iron from bog ore is hard but brittle in the cold: it is full of phosphorus. | iron with phosphorus cracking cold |
| `cupel` | On a bone-ash cupel in a strong hot draft, lead soaks away and leaves the silver. | the bead flashing bright on the cupel |
| `cementation` | Zinc boils away before copper melts; covered in the crucible with molten copper, its ore gives its zinc to the copper: brass. | zinc boiling away as fumes (calamine in the furnace, zinc off a melt) |
| `case_hardened` | A tanning liquor too strong on a raw hide tans its outside fast and closes it: the core stays raw. Start weak, then strengthen. | a raw hide's outside closing in a liquor too strong |
| `rot` | A raw hide left wet rots: the hair slips, the grain spoils. Lime it, tan it, or dry it. | a raw hide beginning to rot |
| `alum` | Alum-tawed leather is soft and pale, but water washes the tawing out. | alum-tawed leather soaking in water |
| `shrink` | Wet leather shrinks and hardens in hot water: a little makes hard armour (cuir bouilli), too much ruins it. | a hide shrinking in a vat too hot |
| `stiff_leather` | Leather left to dry on its own dries stiff: work it (stake it) while it is still damp. | leather drying stiff, unstaked |
| `blotch` | Oiled leather will not take dye evenly: dye first, then oil. | dye brushed over oiled or waxed leather |
| `warp` | A blade quenched unevenly - held still, moved side to side, or heated on one face - warps. Turn it in the fire and plunge it point first, moving it up and down. | an uneven quench bowing a piece (held still, side to side, one face hotter) |
| `normalise` | Steel held too long too hot grows a coarse, weak grain. Heating it just past the critical heat and letting it cool in the air refines it again. | a finished item whose steel is coarse-grained (its inspection says so) |
| `loose_joint` | A well-made part badly fitted is a weak item: a loose guard rattles, a loose head flies off, a badly peened rivet lets go. | a finished item with a badly fitted part (practice in the item's own craft) |
| `drawknife` | Debark at the bark's own depth: the inner bark lifts off green wood at the cambium. Dig deeper and the drawknife gouges the wood you wanted. | the drawknife gouging under the bark |
| `grits` | Sand through the grits in order: each paper only takes out the scratches of the one before it. Jump from coarse to fine and the coarse scratches stay under the finish. | a coarser grit's scratches left under a finer one |
| `felting` | Wool felts when it is hot, wet and moved - or shocked from hot water into cold. Scour it in hot water, but leave it still and rinse it at the same heat. | wool matting into felt |
| `carding` | Card lightly: pressed hard, the cards roll the fibres into little knots (neps) that make a lumpy yarn. | carding hard enough to roll neps |
| `cleavage` | A stone with cleavage parts cleanly along its plane with one firm blow - off the plane, or struck too hard, it shatters. Find the plane first. | a stone shattering instead of parting |
| `table_cleavage` | Never lay a table along a cleavage plane: it flakes when it is polished. Tilt it a few degrees off. | a flake lifting off the table |
| `heat_gem` | Opal, emerald, peridot and their like crack with heat: grind them wet and polish them light and slow. | a stone crazing with heat |
| `polish_compound` | A compound polishes only stones no harder than it: tripoli for the soft ones, cerium for quartz, alumina for corundum, diamond for diamond. | a compound too soft for the stone |
| `pavilion_angle` | The pavilion's angle decides the light: too shallow and it falls through (a window), too deep and the middle goes dark. The higher the stone's refractive index, the shallower it can be. | a pavilion too shallow or too deep |
| `clean_rough` | Clean the rough before testing it: matrix stuck to it blurs the refractometer, reads light on the balance and hides inclusions from the loupe. The brush is slow but safe; the pick marks bare stone. | a crust spoiling a reading |
| `preform_grind` | Grind the preform to its outline on the wheel before faceting: ground freehand on the lap, the outline comes out lopsided and the cut suffers. Ease off before the wheel cuts into the planned stone. | an outline ground freehand on the lap |
| `overpolish` | Polish each part of a stone until it is bright, then stop: polished on, the facet edges round and it loses weight. And no polish mends a badly cut facet. | facet edges rounding as it is polished on |
| `weaving_beat` | Beat each row in evenly with the reed: a row beaten off its measure lies askew, and a bad beat skips a pick - a flaw across the cloth. Tension the warp evenly first, or the cloth puckers. | a bad beat skipping a pick |
| `seat_fit` | A seat cut wider than the stone never holds it, however hard the setting is pushed: cut the seat to the girdle. | a stone moving in its seat |

## Measured examples

From the calibration runs (the Edit Mode tests check the same behaviour):

| Procedure | Result |
|---|---|
| Tool steel (0.95 % C) bar 200×30×5 mm, heated to ~810 °C, water quench, moved up and down | ≈ 85 % martensite, ≈ 790 HV, toughness ≈ 9 J; tempered 2 min at 200 °C: ≈ 700 HV, ≈ 18 J |
| The same in oil | half hardened, ≈ 575 HV, ≈ 29 J |
| Spring steel (0.55 % C) in oil | stays soft, ≈ 280 HV |
| Starmetal steel in oil | hardens fully, ≈ 790 HV |
| Tool steel in agitated brine | hardens (≈ 810 HV) and cracks in about ten places |
| Tool steel normalised in air / annealed in the ashes | ≈ 230 HV / ≈ 210 HV |
| Forge: cold bar to forging heat (1000 °C) with a pumped charcoal fire | about a minute |
| A 200×30×6 mm bar folded by hand at welding heat, its seam set with seven flat blows a heat where it is still open, fluxed with borax again each heat | spring steel set in 2 heats, mild steel in 3 (charcoal or coal), wrought iron in 4 (coal) or 5 (charcoal); without fresh flux it stalls, scaled |
| A bevelled spring steel blade (250×35×4 mm), 3 mm of clay on the spine, quenched up and down | water: edge ≈ 750 HV, spine ≈ 630 HV (bare: ≈ 760 / 740); oil: edge ≈ 440 HV, spine ≈ 330 HV |
| 60 cross-peen blows on a mild steel bar at forging heat | 200 → 270 mm long, mass unchanged |
| Four lumps of hematite, raw, in the bloomery with limestone, kept under 1200 °C | a medium-carbon steel bloom; about 80 % of the iron recovered, porous and slaggy |
| The same lumps crushed, sorted and washed first | nearly all the iron recovered, less slag and phosphorus |
| Deer hide fleshed, then 60 h in oak bark liquor | surface ≈ 0.9 tanned, core ≈ 0.8; dried unstaked: stiff (≈ 0.75); staked while drying: noticeably less stiff |
| Copper melted, then tin added under borax and stirred | bronze; little lost to dross |
| Brass covered with borax, held 40 °C over its liquidus for two minutes | it keeps over nine tenths of its zinc |
| Calamine in the furnace at 1150 °C for ten minutes | its zinc goes up the shaft as fumes; nothing to pour |
| A bar of lead carrying 1 % silver, back in the furnace on six bone-ash cupels in a strong blast at 1300 °C for 25 minutes | the lead soaks into the cupels; the silver, lifted out with the tongs as a bead, is all there within 5 % |
| A 30 g bobbin of raw wool, a middling draft (0.5) / fully prepared wool | ≈ 116 m / ≈ 132 m of yarn (a fine draft: ≈ 200 m raw but lumpy, ≈ 229 m prepared; a thick one ≈ 31 m) |
| Four bobbins of prepared wool (≈ 132 m each) wound together, woven 6 cells wide | ≈ 529 m: 7 rows (72 m a row) - enough for a tunic body (6 × 6); one bobbin alone weaves a single row |
| An even draft (0.85) at the right twist: raw fleece drafted thick (0.2) / middling (0.5) / a combed batch at 0.5 | wool yarn (the even tier, the same from the bag as from a raw batch) / coarse wool yarn (raw fleece drafted fine is lumpy) / fine wool yarn |

