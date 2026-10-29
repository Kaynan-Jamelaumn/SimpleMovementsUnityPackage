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

What a metal is called follows from its composition alone (highest priority first):

| Class | Base | Ranges | |
|---|---|---|---|
| Cast iron | Iron | Carbon 2.1-100% | Too much carbon: it casts well and cannot be forged. |
| Starmetal steel | Iron | Starmetal 3-100%, Carbon 0.25-2.1% | Steel with fallen-star metal in it. |
| Copper matte | Copper | Sulfur 2-100% | Copper still full of sulfur: roast it and smelt it again. |
| Meteoric iron | Iron | Nickel 4-20%, Starmetal 1-100%, Carbon 0-0.25% | Fallen-star iron: nickel and starmetal. |
| Mythril steel | Iron | Mythril 5-50% |  |
| Phosphoric iron | Iron | Phosphorus 0.2-100%, Carbon 0-0.3% | Iron from bog ore: hard, and brittle in the cold. |
| Orichalcum bronze | Copper | Orichalcum 5-50% |  |
| Nickel steel | Iron | Nickel 1-10%, Carbon 0.25-2.1% |  |
| Nickel silver | Copper | Nickel 10-30%, Zinc 10-35% |  |
| Wrought iron | Iron | Carbon 0-0.08% | Iron with hardly any carbon: tough, soft, it does not harden. |
| Bell bronze | Copper | Tin 16-35% | Too much tin: very hard, very brittle. |
| Leaded bronze | Copper | Tin 4-16%, Lead 3-30% | Casts well; softer. |
| Rose gold | Gold | Copper 15-35% |  |
| Electrum | Gold | Silver 15-50% | Gold and silver together, as rivers give it. |
| Bronze | Copper | Tin 4-16% | Copper with about a tenth of tin: hard, tough, it casts well and hardens when hammered cold. |
| Brass | Copper | Zinc 10-45% | Copper and zinc. |
| High-carbon steel | Iron | Carbon 0.6-1.2% | Hardens very hard: keen edges, if it is tempered. |
| Mythril silver | Mythril | Silver 3-20% |  |
| Pewter | Tin | Copper 0.5-10% |  |
| Mild steel | Iron | Carbon 0.08-0.3% | Low-carbon steel: tough; it hardly hardens. |
| Silver-bearing lead | Lead | Silver 0.05-30% | Lead carrying silver: cupel it. |
| Medium-carbon steel | Iron | Carbon 0.3-0.6% | Hardens well, stays tough: axes, hammers, springy blades. |
| Sterling silver | Silver | Copper 5-10% |  |
| Ultra-high-carbon steel | Iron | Carbon 1.2-2.1% | The hardest steel - and the most brittle. |
| Coin silver | Silver | Copper 10-25% |  |
| Fine gold | Gold | Silver 0-2%, Copper 0-2% |  |
| Fine silver | Silver | Copper 0-5% |  |
| Tin | Tin |  |  |
| Copper | Copper | Tin 0-4%, Zinc 0-10% |  |
| Starmetal | Starmetal |  |  |
| Mythril | Mythril |  |  |
| Zinc | Zinc |  |  |
| Nickel | Nickel |  |  |
| Lead | Lead |  |  |
| Orichalcum | Orichalcum |  |  |
| Gold | Gold |  |  |

## Stock metals

The bars a merchant sells (and a test station gives), 20 cm × 3 cm × 6 mm unless made otherwise:

| Stock | Composition | Class | Base hardness, soft (HV) |
|---|---|---|---|
| Wrought iron | Iron 99.94%, Carbon 0.04%, Phosphorus 0.02% | Wrought iron | 98 |
| Mild steel | Iron 99.75%, Carbon 0.18%, Manganese 0.07% | Mild steel | 90 |
| Spring steel | Iron 99.35%, Carbon 0.55%, Manganese 0.1% | Medium-carbon steel | 90 |
| Tool steel | Iron 98.95%, Carbon 0.95%, Manganese 0.1% | High-carbon steel | 90 |
| Nickel steel | Iron 96.55%, Carbon 0.75%, Nickel 2.5%, Manganese 0.2% | Nickel steel | 113 |
| Copper | Copper 99.8%, Iron 0.2% | Copper | 48 |
| Bronze | Copper 89%, Tin 11% | Bronze | 123 |
| Brass | Copper 70%, Zinc 30% | Brass | 82 |
| Tin | Tin 100% | Tin | 6 |
| Sterling silver | Silver 92.5%, Copper 7.5% | Sterling silver | 81 |
| Gold | Gold 99%, Silver 1% | Fine gold | 27 |
| Mythril | Mythril 97%, Silver 3% | Mythril silver | 311 |
| Starmetal steel | Iron 93.25%, Carbon 0.75%, Starmetal 5%, Nickel 1% | Starmetal steel | 103 |
| Orichalcum bronze | Copper 72%, Tin 8%, Orichalcum 20% | Orichalcum bronze | 175 |

## Ores

| Ore | Id | Gives | Rock | Reduces from (°C) | |
|---|---|---|---|---|---|
| Red iron ore | `hematite` | Iron, Silica, Phosphorus, Sulfur, Manganese | Quartz | 1050 | Hematite. The common iron ore: rich, with quartz around it. |
| Black iron ore | `magnetite` | Iron, Silica, Phosphorus, Sulfur | Quartz | 1100 | Magnetite. Rich and clean, slow to reduce. |
| Bog iron | `bog_iron` | Iron, Silica, Phosphorus, Sulfur, Manganese | Clay | 950 | Limonite from the bogs: easy to smelt, but full of phosphorus. |
| Green copper ore | `malachite` | Copper, Silica, Iron | Limestone | 900 | Malachite. Smelts easily into clean copper; it often sits in limestone. |
| Brassy copper ore | `chalcopyrite` | Copper, Iron, Sulfur, Silica | Quartz | 1100 | Chalcopyrite: copper and iron bound with sulfur. Roast it, or it gives matte. |
| Tin stone | `cassiterite` | Tin, Iron, Silica | Quartz | 1000 | Cassiterite. Heavy: it stays in the pan when the rock washes away. |
| Lead ore | `galena` | Lead, Sulfur, Silver | Quartz | 800 | Galena: lead and sulfur, and a little silver. Roast it, smelt it, cupel the lead. |
| Native silver | `native_silver` | Silver, Copper, Gold | Quartz | 960 | Silver as it grows in the rock: melt it out. |
| Gold-bearing quartz | `gold_quartz` | Gold, Silver, Copper | Quartz | 1065 | Gold in quartz: crush it, wash it, melt the gold out. Natural gold carries silver. |
| Calamine | `calamine` | Zinc, Iron, Silica | Limestone | 950 | Zinc ore. Its zinc boils away in a furnace: heat it in a covered crucible with molten copper and the copper drinks it (brass). |
| Fallen star | `meteorite` | Iron, Nickel, Starmetal, Phosphorus, Carbon | Quartz | 1000 | Iron from the sky, with nickel and starmetal. Heat it and it welds into a bloom. |
| Mythril vein | `mythril_ore` | Mythril, Silver, Silica | Quartz | 1350 | It will not give up its metal without a very hot furnace and an arcane flux. |
| Orichalcum ore | `orichalcum_ore` | Orichalcum, Copper, Sulfur, Silica | Quartz | 1000 | A sulfide like copper's: roast it first. |

## Fuels, quenchants and vat agents

| Fuel | Id | Hottest (°C) |
|---|---|---|
| Charcoal | `charcoal` | 1350 |
| Coal | `coal` | 1450 |
| Coke | `coke` | 1550 |
| Firewood | `firewood` | 950 (any wood in the bag burns as it: planks, logs, offcuts, wooden blanks) |

| Quenchant | Id | Severity | Vapour blanket |
|---|---|---|---|
| Water | `water` | 1 | 0.55 |
| Brine | `brine` | 1.6 | 0.15 |
| Quenching oil | `oil` | 0.38 | 0.25 |
| Frostwater | `frostwater` | 1.25 | 0.05 |
| Dragon's blood | `dragon_blood` | 0.75 | 0.2 |

Frostwater lends ice and dragon's blood fire to the steel they harden.

| Agent | Id | Kind | Hours to tan through | Looks like |
|---|---|---|---|---|
| Water | `water` | Water | 1 | clear water |
| Lime water | `lime` | Lime | 36 | milky, caustic lime water |
| Oak bark liquor | `bark` | Vegetable | 72 | a brown, bitter-smelling liquor |
| Alum and salt | `alum` | Mineral | 18 | a clear, salty, puckering liquor |
| Oil emulsion | `brain_oil` | Oil | 30 | a creamy, greasy emulsion |
| Alchemical tanning essence | `tanning_essence` | Alchemical | 8 | a violet liquor that smells of smoke |
| Vinegar pickle | `vinegar` | Acid | 36 | sharp-smelling vinegar |

## Hides and woods

| Hide | Id | Cells | |
|---|---|---|---|
| Cow hide | `cow` | 18x13 | Big and thick along the back: armour, soles, belts. |
| Deer hide | `deer` | 10x8 | Thin and supple: gloves, wraps, garments. |
| Boar hide | `boar` | 12x9 | Tough and bristly, scarred. |
| Wolf pelt | `wolf` | 11x8 | A thick fur. |
| Bear hide | `bear` | 16x12 | Very thick, very fatty. |
| Rabbit pelt | `rabbit` | 4x3 | Small and soft: linings. |
| Drake hide | `drake` | 14x10 | Scaled and fire-proof by nature. |
| Snakeskin | `snake` | 18x5 | Thin and scaled: belts, wraps, sheaths. (Skinned giant serpents.) |
| Troll hide | `troll` | 16x12 | Thick as a door: heavy leather armour that shrugs off blows. (Skinned trolls.) |
| Linen cloth (textile) | `linen_cloth` | 14x20 | Smooth, cool linen: shirts, linings, the layers of a gambeson. |
| Wool cloth (textile) | `wool_cloth` | 14x20 | Warm, springy wool: cloaks, hoods, padding. |
| Canvas (textile) | `canvas` | 12x18 | Heavy hemp canvas: the outer shell of a gambeson, bags, sails. |
| Silk cloth (textile) | `silk_cloth` | 12x18 | Light, smooth and strong: robes, fine shirts, linings. |
| Cotton cloth (textile) | `cotton_cloth` | 14x20 | Soft and cool: shirts, tunics, quilting. |
| Felt (textile) | `felt` | 12x14 | Thick, fulled wool that does not fray: hats, padding, boot liners. |
| Velvet (textile) | `velvet` | 12x16 | A rich, napped cloth: surcoats and capes. |

| Wood | Id | Density | |
|---|---|---|---|
| Ash | `ash` | 0.68 | Springy and tough: the wood for hafts and shafts. |
| Hickory | `hickory` | 0.8 | The toughest haft wood. |
| Oak | `oak` | 0.75 | Hard and heavy; it splits under shock. |
| Maple | `maple` | 0.7 | Fine and hard: grips. |
| Linden plank | `linden` | 0.5 | Light boards for shields. |
| Ironwood | `ironwood` | 1.1 | So dense it sinks; a legend among haft-makers. |
| Yew | `yew` | 0.67 | Heartwood that resists, sapwood that pulls: the bow wood. |
| Elm | `elm` | 0.6 | Interlocked grain that will not split: shield boards, mallets, clubs. |
| Walnut | `walnut` | 0.64 | Dark and fine: grips and stocks. |
| Birch | `birch` | 0.65 | Cheap and even: handles and staves. |
| Pine plank | `pine` | 0.48 | Light and soft: cheap boards; it dents. |

## Supplies

| Supply | Id | Kind | |
|---|---|---|---|
| Limestone | `limestone` | Flux | Furnace flux: its lime takes the silica's place in the slag, so the iron stays. |
| Borax | `borax` | Flux | Flux for welding and for covering a melt. |
| Sand | `sand` | Flux | A cheap welding flux. |
| Arcane flux | `arcane_flux` | Flux | Without it, mythril ore gives nothing. |
| Bone-ash cupel | `bone_ash` | Cupel | Soaks up lead as it oxidises; silver stays on it. |
| Linen thread | `linen_thread` | Thread | Ordinary sewing thread. |
| Waxed thread | `waxed_thread` | Thread | Strong and it sheds water. |
| Sinew | `sinew` | Thread | Shrinks tight as it dries. |
| Silk thread | `silk_thread` | Thread | The strongest thread. |
| Wool batting | `wool_padding` | Padding | Padding under armour: it softens blows. |
| Linen padding | `linen_padding` | Padding | Quilted linen padding. |
| Beeswax | `beeswax` | Wax | Sheds water; stiffens leather a little. |
| Neatsfoot oil | `neatsfoot_oil` | Oil | Makes leather supple. |
| Linseed oil | `linseed_oil` | Oil | Protects metal and wood; softens leather a little. |
| Black dye | `dye_black` | Dye | Iron and vinegar. |
| Walnut dye | `dye_brown` | Dye | A warm brown. |
| Madder dye | `dye_red` | Dye | A deep red. |
| Woad dye | `dye_blue` | Dye | A dark blue. |
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
| Spirit varnish | `spirit_varnish` | Varnish | Dries fast to a bright, thin film. |
| Oil varnish | `oil_varnish` | Varnish | Slow to dry, tough and weatherproof (strength 1.25). |

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

## Moulds and patterns

| Mould | Id | Size (mm) |
|---|---|---|
| Ingot | `ingot` | 120x40x20 |
| Bar | `bar` | 250x30x12 |
| Plate | `plate` | 250x200x3 |
| Blade blank | `blade_blank` | 600x14x7 |
| Dagger blank | `dagger_blank` | 300x12x6 |
| Axe head | `axe_head` | 170x40x30 |
| Mace head | `mace_head` | 90x60x60 |
| Spear head | `spear_head` | 300x26x26 |
| Guard | `guard` | 200x22x12 |
| Pommel | `pommel` | 55x40x35 |
| Shield boss | `boss` | 160x160x4 |
| Pick head | `pick_head` | 450x10x10 |
| Rivets | `rivets` | 14x5x5 |
| Studs | `studs` | 10x8x4 |
| Buckles | `buckles` | 50x6x6 |

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
| Dagger | `dagger` | Weapon | Blade, Guard, Grip, Pommel, Grip wrap | Guard on the tang (Fit), Grip on the tang (Fit), Pommel peened on (Peen), Grip wrap (Wrap) |
| Arming sword | `arming_sword` | Weapon | Blade, Guard, Grip, Pommel, Grip wrap | Guard on the tang (Fit), Grip on the tang (Fit), Pommel peened on (Peen), Grip wrap (Wrap) |
| Greatsword | `greatsword` | Weapon | Blade, Guard, Grip, Pommel, Grip wrap | Guard on the tang (Fit), Grip on the tang (Fit), Pommel peened on (Peen), Grip wrap (Wrap) |
| Battle axe | `battle_axe` | Weapon | Axe head, Haft | Head on the haft (Wedge) |
| Spear | `spear` | Weapon | Spear head, Shaft, Socket pin | Head on the haft (Socket) |
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
| Belt | `belt` | Armor | Strap, Buckle | Buckle (Buckle) |
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
| Scythe | `scythe` | Tool (Tool, Scythe) | Scythe blade, Snath | Head on the haft (Wedge) |
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
| Leather bracers | `leather_bracers` | Armor (Bracers) | Bracer ×2, Buckles | Buckles (Buckle) |
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

Where each is made (the station presets; a station lists only these in its Assembly area):

* **Smithy**: the blades, axes, hammers, maces, polearms, thrown weapons, tools - and the bows, quarterstaff and club
  (its files shape wood).
* **Woodshop**: the hunting bow, longbow, quarterstaff and studded club.
* **Leatherworks**: all leather and cloth armour and clothes (a leather bench cuts cloth too).
* **Tailor's**: the cloth armour and clothes.
* **Armourer** (smithy + leather bench + tailor's table) or a complete workshop: everything with metal on leather or
  cloth - plate helms, breastplate, pauldrons, greaves, sabatons, vambraces, plated gloves and bracers, brigandine,
  scale armour, cestus, shields with leather.

## Insights

| Id | What the player understands |
|---|---|
| `burning` | Steel heated to a sparkling white burns: its grain falls apart and nothing brings it back. Forge below a yellow heat. |
| `quench_crack` | A hard quench cracks high-carbon steel: the faster the medium, the higher the carbon, the thinner the edge and the more abrupt the shape, the likelier. Oil is gentler than water, water than brine. |
| `untempered` | Steel straight from the quench is glass-hard and brittle. Heat it gently again (temper it) and it gives up a little hardness for a lot of toughness. |
| `temper_colours` | On bright steel, heat paints colours: pale straw, straw, bronze, purple, blue. They tell the tempering heat. |
| `magnet` | Steel stops sticking to a magnet at a cherry-red heat: that is the heat to harden it from. |
| `cold_forging` | Forged too cold, steel tears and cracks. Iron and steel move at a red to yellow heat; bronze and copper move cold, but harden as they are worked until heat softens them again. |
| `cast_iron` | Cast iron shatters under the hammer: it has too much carbon. It can be cast, not forged. |
| `slag` | A bloom is spongy and full of slag: hammer it at welding heat until it closes up and the slag runs out, or it will crumble and hide flaws. |
| `scale_pits` | Scale left on the metal is hammered into it as pits. Brush it off, or keep the fire reducing. |
| `grind_heat` | Pressed too hard on the wheel, a thin edge overheats and loses its temper: it turns blue and goes soft. Cool it often, press lightly. |
| `file_skate` | A file skates on hardened steel: file and shape before hardening. |
| `wrong_flux` | Without flux, a forge weld does not take; without lime, a bloomery's slag eats the iron. |
| `sulfur` | Iron with sulfur in it (from a coal fire, or an unroasted ore) tears at forging heat. Charcoal is cleaner; roast sulfide ores first. |
| `phosphorus` | Iron from bog ore is hard but brittle in the cold: it is full of phosphorus. |
| `cupel` | On a bone-ash cupel in a strong hot draft, lead soaks away and leaves the silver. |
| `cementation` | Zinc boils away before copper melts; covered in the crucible with molten copper, its ore gives its zinc to the copper: brass. |
| `case_hardened` | A tanning liquor too strong on a raw hide tans its outside fast and closes it: the core stays raw. Start weak, then strengthen. |
| `rot` | A raw hide left wet rots: the hair slips, the grain spoils. Lime it, tan it, or dry it. |
| `alum` | Alum-tawed leather is soft and pale, but water washes the tawing out. |
| `shrink` | Wet leather shrinks and hardens in hot water: a little makes hard armour (cuir bouilli), too much ruins it. |
| `stiff_leather` | Leather left to dry on its own dries stiff: work it (stake it) while it is still damp. |
| `blotch` | Oiled leather will not take dye evenly: dye first, then oil. |
| `warp` | A blade quenched unevenly - held still, moved side to side, or heated on one face - warps. Turn it in the fire and plunge it point first, moving it up and down. |
| `normalise` | Steel held too long too hot grows a coarse, weak grain. Heating it just past the critical heat and letting it cool in the air refines it again. |
| `loose_joint` | A well-made part badly fitted is a weak item: a loose guard rattles, a loose head flies off, a badly peened rivet lets go. |

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
| 60 cross-peen blows on a mild steel bar at forging heat | 200 → 270 mm long, mass unchanged |
| Four lumps of hematite, raw, in the bloomery with limestone, kept under 1200 °C | a medium-carbon steel bloom; about 80 % of the iron recovered, porous and slaggy |
| The same lumps crushed, sorted and washed first | nearly all the iron recovered, less slag and phosphorus |
| Deer hide fleshed, then 60 h in oak bark liquor | surface ≈ 0.9 tanned, core ≈ 0.8; dried unstaked: stiff (≈ 0.75); staked while drying: noticeably less stiff |
| Copper melted, then tin added under borax and stirred | bronze; little lost to dross |

