# 06 — The Built-in Creatures

Twelve creatures come with the package (`HarvestDefaults`), covering every body plan. *Create Creature Anatomy Assets*
makes editable copies of them. Values are for a typical adult; *spoils* is minutes at 20 °C.

| Creature | Plan | Mob types | Weak points | Notable |
|---|---|---|---|---|
| **Bear** | Four legs | Bear, Grizzly | Head ×1.5, neck ×1.6, ribs ×1.3 | A great pelt (tannery: bear hide), fat, a prized gall bladder, a rare bezoar |
| **Wolf** | Four legs | Wolf, Warg, Fox, Hound, Dog, Coyote, Jackal | Head, neck, ribs | A thick pelt (wolf pelt), fangs, a rare scent gland |
| **Deer** | Four legs | Deer, Stag, Doe, Elk, Moose, Reindeer | Head, neck, ribs | Supple hide, venison, a stag's antlers, a rare musk pod |
| **Boar** | Four legs | Boar, Pig, Hog, Swine, Sow | Head, neck, ribs | Tough hide (×1.15 toughness), pork, lard, tusks |
| **Rabbit** | Four legs | Rabbit, Hare, Bunny | Head, neck, ribs | A small pelt, a saddle and legs of meat, a lucky foot |
| **Sheep** | Four legs | Sheep, Ram, Lamb, Ewe, Goat | Head, neck, ribs | Sheepskin (tannery: deer hide), mutton, a ram's horns |
| **Drake** | Four legs and wings | Drake, Wyvern, Dragon, Wyrmling | Head, neck, ribs, **soft belly ×1.4** | Scaled (armour 0.5), resists fire 0.9; drake hide (Skinning 15), wing membranes, a fire gland (Organ Harvesting 20), a rare heartstone |
| **Troll** | Two legs | Troll, Ogre | Head ×1.6, neck ×1.8 | Toughness 1.4, regenerates 0.5/min unless burned; troll hide (Skinning 10), a regenerating heart |
| **Eagle** | Wings | Eagle, Hawk, Falcon, Bird, Vulture, Owl, Raven, Griffon | Head ×1.6, neck ×1.4 | Plucked flight feathers, talons, a beak, a wishbone, eagle eyes |
| **Salmon** | Fish | Fish, Salmon, Trout, Carp, Pike, Bass, Cod | Head ×1.5 | Scales scraped off, a fillet, roe (half of them), fish bones |
| **Giant spider** | Shell and legs | Spider, Arachnid, Broodmother, Crawler | Head ×1.4, abdomen ×1.3 | A shell (armour 0.15-0.5) of chitin plates, venom and silk glands, a rare egg sac |
| **Giant serpent** | Serpent | Snake, Serpent, Viper, Python, Cobra, Asp, Basilisk | Head ×1.8, neck ×1.4 | Snakeskin (tannery: snakeskin), a venom gland, fangs, vertebrae |

## Four-legged game: bear, wolf, deer, boar, rabbit, sheep

All share the four-legged body: **head** (stagger; thick skull on a bear), **neck**, **shoulder**, **back**, **ribs**
(the vitals: heart and lungs), **belly** (liver, gall, kidneys, stomach, guts; soft), **haunch**, four **legs** (slow
it; fiddly to skin) and a **tail**.

| Material | Method | Notes |
|---|---|---|
| Pelt / hide | Skinning | One whole hide; becomes the tannery's hide of the same animal (sheep: deer hide). Spoils 120 (rabbit 100). |
| Haunch, backstrap, shoulder, ribs, neck meat, shanks | Butchering | Bulk portions, from their regions (shanks: all four legs). Wait for the hide. Spoil 45. Food. |
| Fat (bear), lard (boar) | Butchering | Trimmed from the belly and back. Spoils 80. Food; alchemy: oil. |
| Leg sinew ×4, back sinew | Tendons | The back sinew makes the finest bowstrings (worth twice as much). Spoil 60. |
| Skull, femurs, humeri, rib bones, vertebrae, pelvis | Bones | Never spoil. Saw them free after the meat around them is taken. |
| Heart, lungs, liver, kidneys ×2, gall bladder, stomach, intestines | Organs | Spoil 10-20. Heart and liver: food and *sanguine sap*; the gall bladder: fragile, *bitter resin*, soils the liver when torn; stomach and guts soil the cavity when torn, and taking them guts the carcass. |
| Claws (bear 5+5, wolf 5+4), fangs ×4 | Trophies | Pried loose. |
| Antlers (deer, 60% — stags), hooves (deer, boar), tusks (boar), horns (sheep, 50% — rams), foot (rabbit) | Trophies | Antlers and horns are sawn off. |
| Bezoar (bear, 12%), scent gland (wolf, 35%), musk pod (deer, 15%) | Organs | Rare finds: Anatomy 15 / 10 / 15 to notice. |

The rabbit gives only its pelt, a saddle, legs and a foot; the sheep has no claws or fat.

## Drake

Four legs and two wings (`wing_l`, `wing_r`: slow it, ×0.7). Scaled (armour 0.5) on the head, neck, shoulders, back,
haunch and tail; the ribs, legs and wings are bare and the belly's scales are soft (0.1) — aim for the **soft belly
(×1.4)**; fire barely touches it (resists 0.9) and its hide is tough (1.3).

| Material | Method | Notes |
|---|---|---|
| Drake hide | Skinning | Skinning 15 to try. The tannery's drake hide: fire-proof leather. Worth 150. Spoils 240. |
| Drake scales | Trophies | Pried from the scaled regions. |
| Wing membrane ×2 | Skinning | Cut around, then drawn off (Skinning 10). |
| Drake meat | Butchering | One bulk cut. |
| Leg sinew, back sinew, skull, femurs | Tendons, Bones | |
| Heart | Organs | *Draconic ichor*. |
| Fire gland | Organs | Organ Harvesting 20; *ember spirit*. |
| Heartstone | Organs | Rare (25%), Anatomy 20: *wyrmblood essence*, worth 200. |
| Horns, claws, fangs | Trophies | Horns sawn off. |

## Troll

Upright: **head** (×1.6), **neck** (×1.8), **chest**, **belly**, **pelvis**, **arms** and **legs** (×0.8-0.85). Tough
(1.4) and regenerating (its wounds close by half every minute while it lives — but never burns: fight trolls with fire, if
you can spare the hide).

| Material | Method | Notes |
|---|---|---|
| Troll hide | Skinning | Skinning 10. The tannery's troll hide: heavy leather armour. Spoils 200. |
| Troll flesh, troll fat | Butchering | Not food. Flesh: *vital essence*; fat: *oil*. |
| Troll sinew, skull, femur | Tendons, Bones | |
| Regenerating heart | Organs | Organ Harvesting 10; *vital essence*, worth 120. |
| Troll liver | Organs | |
| Tusks, claws | Trophies | |

## Eagle

Wings and two legs: **head** (×1.6), **neck** (×1.4), **breast**, **belly**, **wings** (×0.7), **tail** and **legs**.

| Material | Method | Notes |
|---|---|---|
| Flight feathers | Skinning | Plucked (a scrape over the wings and tail): frantic strokes break them. Never spoil. |
| Bird breast | Butchering | Food. |
| Talons, beak | Trophies | The beak is sawn off. |
| Wishbone | Bones | |
| Eagle eyes ×2 | Organs | Organ Harvesting 5; *zephyr essence*. |
| Bird heart | Organs | |

## Salmon

A fish: **head** (×1.5), **back**, **belly**, **tail**, **fins**.

| Material | Method | Notes |
|---|---|---|
| Fish scales | Skinning | Scraped off. |
| Fish fillet | Butchering | No hide to take off first. Spoils 30. Food. |
| Roe | Organs | Half of them carry it; food; *albumen*. |
| Fish bones | Bones | |

## Giant spider

A shell and eight legs: **head** (×1.4, armour 0.4), **thorax** (armour 0.5), **abdomen** (×1.3, armour 0.15 — its
weak spot), **left and right legs** (×0.6, armour 0.3). No hide.

| Material | Method | Notes |
|---|---|---|
| Chitin plates | Trophies | Pried off the shell. |
| Spider fangs | Trophies | |
| Venom gland | Organs | Organ Harvesting 10; fragile; *venom*. |
| Silk gland | Organs | |
| Spider heart | Organs | |
| Spider leg meat | Butchering | Food (if you dare). |
| Egg sac | Organs | Rare (30%), Anatomy 10, worth 60. |

## Giant serpent

One long body: **head** (×1.8), **neck** (×1.4), **body**, **tail**.

| Material | Method | Notes |
|---|---|---|
| Snakeskin | Skinning | The tannery's snakeskin (a long, narrow hide). Spoils 150. |
| Snake meat | Butchering | Food. |
| Venom gland | Organs | Organ Harvesting 5; *venom*. |
| Serpent fangs | Trophies | |
| Serpent vertebrae | Bones | |
