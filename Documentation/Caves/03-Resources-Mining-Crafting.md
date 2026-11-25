# Caves 03 — Resources, Mining and Crafting

## 1. Where the resources come from

Every deposit gives a material of the **Crafting Database**. Lumps are the database's stock items, made at runtime by
`CraftItems.Stock(rules, kind, id)`, the same items the crafting stations already use. Nothing is duplicated: a cave's
hematite *is* the crafting system's hematite, with the same ore chemistry, lump mass, price and icon.

| Kind | Lumps are | Examples |
|---|---|---|
| `Ore` | ore lumps that keep their seam (`OreCode(seam, lump)`), so the ore bench and bloomery see where they came from | hematite, magnetite, bog iron, malachite, chalcopyrite, cassiterite, galena, native silver, gold-bearing quartz, calamine, garnierite, meteorite, mythril ore, orichalcum ore |
| `Fuel` | fuel | coal |
| `Supply` | supplies | limestone (flux), refractory clay, sand, borax, fuller's earth, tripoli |
| `Quenchant` | quenchant stock | brine (rock salt) |
| `Gem` | rough stones, each a stone of its own | rock crystal, citrine, amethyst, turquoise, lapis lazuli, moonstone, garnet, topaz, peridot, tourmaline, opal, aquamarine, emerald, ruby, sapphire, diamond |

## 2. The Resource Table

A **Resource Table** (*Create ▸ SimpleMovements ▸ Caves ▸ Resource Table*) lists the kinds of deposit a cave can hold,
all editable without code: spawn rates, rarity and depth requirements in one list. Empty or missing = the built-in
resources.

| Field | Meaning |
|---|---|
| Name | label: the prompt when a player aims at the deposit ("Red Iron Ore Seam (worked out)"), statistics |
| Kind, Craft Id | what a lump is (§1) |
| Item Override | optional: a specific item asset instead of the runtime stock item |
| Gem, Gem Chance | a gemstone a lump brings out now and then (a by-product) |
| Depth, Peak Depth | the depth range it appears in and where it is most common. At the range's edges it is a quarter as common (`CaveDepth.Suitability`) |
| Weight | how common at its peak against the other entries |
| Rarity | its tier (0 Common … 4 Legendary): hoards favour rarer ones, statistics, the deposit's tier |
| Special Chambers Only | only in rich veins, grottos and hoards (the fallen star) |
| Rich Vein / Grotto Affinity | how much more often in those chambers |
| Mount | in walls, on the floor, or both |
| Vein Size, Vein Spread | deposits per vein and how far apart |
| Size | the rock's size (bigger = more lumps) |
| Lumps | lumps before it is worked out (scaled by size) |
| Work Per Lump | pick Tool Damage needed per lump (a crafted pickaxe deals about 8–15 a swing) |
| Min Tool Damage | the weakest pick that can break anything off (0 = any) |
| Regrow Minutes | play minutes until a worked-out deposit grows back (0 = never) |
| Gather By Hand, Loose Stones | a few stones can be gathered with Interact before a pick is needed |
| Prefab | optional model (it gets an `OreDeposit` if it has none; give it a collider) |
| Database Colors, Rock / Mineral Color, Glow | the generated look (§5) |

The inspectors of the table and of the stage show **what each depth holds**: the most likely deposits of an ordinary
chamber per band, as percentages, with a warning for a band that would hold nothing. *Add Missing Built-in Resources* and
*Reset To Built-in* refill a table.

## 3. The built-in deposits

`CaveDefaults.Resources()`: every material of the Crafting Database that comes out of the ground (38 entries; `*` =
special chambers only). Depth bands: Shallow < 1, Medium 1–2.5, Deep 2.5–4, Abyssal ≥ 4.

| Deposit | Gives (kind: id) | Depth (peak) | Rarity | Weight | Where | Vein | Lumps | Work/lump | Min pick | By hand | By-product |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Coal Seam | Fuel: `coal` | 0-3.5 (0.8) | Common | 1.4 | Wall | 2-4 | 8-14 | 18 | - | 3 | - |
| Limestone | Supply: `limestone` | 0-2.5 (0.4) | Common | 1 | Any | 1-3 | 6-10 | 20 | - | 3 | - |
| Clay Bank | Supply: `refractory_clay` | 0-1.8 (0.3) | Common | 0.55 | Floor | 1-2 | 5-8 | 12 | - | 4 | - |
| Sand Bank | Supply: `sand` | 0-1.5 (0.2) | Common | 0.4 | Floor | 1-2 | 6-10 | 10 | - | 5 | - |
| Bog Iron | Ore: `bog_iron` | 0-1.6 (0.3) | Common | 0.7 | Any | 1-3 | 5-9 | 26 | - | 2 | - |
| Red Iron Ore | Ore: `hematite` | 0-3.2 (1) | Common | 1.25 | Wall | 2-4 | 6-10 | 38 | - | 2 | garnet 4% |
| Green Copper Ore | Ore: `malachite` | 0-2.8 (0.7) | Common | 1 | Wall | 1-3 | 6-10 | 30 | - | 2 | turquoise 8% |
| Fuller's Earth | Supply: `fullers_earth` | 0.2-2 (0.8) | Uncommon | 0.25 | Floor | 1-2 | 4-7 | 12 | - | 3 | - |
| Tripoli | Supply: `tripoli` | 0.3-2.5 (1) | Uncommon | 0.2 | Wall | 1-2 | 4-7 | 16 | - | 2 | - |
| Rock Crystal Cluster | Gem: `rock_crystal` | 0.3-4.5 (1.5) | Uncommon | 0.25 | Wall | 1-2 | 2-4 | 40 | - | - | - |
| Citrine Cluster | Gem: `citrine` | 1-4.5 (2.2) | Uncommon | 0.1 | Wall | 1-2 | 2-3 | 40 | - | - | - |
| Tin Stone | Ore: `cassiterite` | 0.6-3.5 (1.6) | Uncommon | 0.75 | Wall | 1-3 | 5-8 | 41 | - | 2 | - |
| Brassy Copper Ore | Ore: `chalcopyrite` | 1-3.8 (2) | Uncommon | 0.7 | Wall | 2-3 | 6-9 | 32 | - | 2 | - |
| Lead Ore | Ore: `galena` | 1-4.2 (2.2) | Uncommon | 0.55 | Wall | 1-3 | 5-9 | 26 | - | 2 | - |
| Calamine | Ore: `calamine` | 1-3.8 (2) | Uncommon | 0.45 | Any | 1-2 | 5-8 | 34 | - | 2 | - |
| Rock Salt | Quenchant: `brine` | 1-3.5 (1.8) | Uncommon | 0.4 | Any | 1-3 | 4-8 | 18 | - | 2 | - |
| Borax Crust | Supply: `borax` | 1.5-4 (2.5) | Uncommon | 0.25 | Any | 1-2 | 4-7 | 16 | - | 2 | - |
| Black Iron Ore | Ore: `magnetite` | 1.5-5 (2.8) | Uncommon | 0.65 | Wall | 1-3 | 6-9 | 40 | - | 2 | - |
| Amethyst Geode | Gem: `amethyst` | 1-5 (2.5) | Rare | 0.15 | Wall | 1-2 | 2-3 | 42 | - | - | - |
| Turquoise Vein | Gem: `turquoise` | 1.2-4.5 (2.4) | Rare | 0.08 | Wall | 1-2 | 2-3 | 30 | - | - | - |
| Lapis Lazuli | Gem: `lapis_lazuli` | 1.5-5 (2.8) | Rare | 0.07 | Wall | 1-2 | 2-3 | 32 | - | - | - |
| Moonstone Seam | Gem: `moonstone` | 1.8-5.5 (3) | Rare | 0.06 | Wall | 1-2 | 1-3 | 36 | - | - | - |
| Garnet Cluster | Gem: `garnet` | 1.5-5.5 (3) | Rare | 0.12 | Wall | 1-2 | 2-3 | 46 | - | - | - |
| Green Nickel Ore | Ore: `garnierite` | 2-5.5 (3.2) | Rare | 0.35 | Any | 1-2 | 5-8 | 24 | - | 2 | - |
| Native Silver | Ore: `native_silver` | 2-6.5 (3.5) | Rare | 0.26 | Wall | 1-2 | 3-6 | 36 | - | 2 | moonstone 10% |
| Gold-Bearing Quartz | Ore: `gold_quartz` | 2.5-7 (4) | Rare | 0.2 | Wall | 1-2 | 3-6 | 44 | - | 2 | citrine 12% |
| Blue Topaz Cluster | Gem: `topaz` | 2.5-6.5 (4) | Rare | 0.1 | Wall | 1-2 | 2-3 | 50 | - | - | - |
| Peridot Nodule | Gem: `peridot` | 2.5-6 (3.8) | Rare | 0.08 | Wall | 1-2 | 1-3 | 44 | - | - | - |
| Tourmaline Cluster | Gem: `tourmaline` | 2.5-6.5 (4) | Rare | 0.08 | Wall | 1-2 | 1-3 | 46 | - | - | - |
| Opal Seam | Gem: `opal` | 2.5-6.5 (4.2) | Rare | 0.06 | Wall | 1-2 | 1-2 | 30 | - | - | - |
| Orichalcum Ore | Ore: `orichalcum_ore` | 3.5-9 (5.5) | Epic | 0.12 | Wall | 1-2 | 3-5 | 50 | 8 | 2 | ruby 10% |
| Mythril Vein | Ore: `mythril_ore` | 4-10 (6.5) | Legendary | 0.08 | Wall | 1-1 | 2-4 | 56 | 10 | 2 | aquamarine 12% |
| Fallen Star * | Ore: `meteorite` | 2.5-10 (5) | Epic | 0.05 | Floor | 1-1 | 2-3 | 60 | 10 | 2 | peridot 25% |
| Aquamarine Cluster | Gem: `aquamarine` | 3.5-8 (5) | Epic | 0.06 | Wall | 1-2 | 1-3 | 50 | 8 | - | - |
| Emerald Seam | Gem: `emerald` | 3.5-8 (5.2) | Epic | 0.05 | Wall | 1-2 | 1-2 | 50 | 8 | - | - |
| Ruby Cluster | Gem: `ruby` | 4-9 (6) | Epic | 0.04 | Wall | 1-2 | 1-2 | 56 | 8 | - | - |
| Sapphire Cluster | Gem: `sapphire` | 4-9 (6) | Epic | 0.04 | Wall | 1-2 | 1-2 | 56 | 8 | - | - |
| Diamond Pipe | Gem: `diamond` | 5-12 (7.5) | Legendary | 0.03 | Wall | 1-2 | 1-2 | 64 | 10 | - | - |

Measured over 160 generated caves (502 levels; 07 §2), the commonest deposits per band are:

| Band | Commonest |
|---|---|
| Shallow | coal, red iron, limestone, green copper, bog iron, clay, sand |
| Medium | coal, red iron, brassy copper, green copper, tin, limestone, lead |
| Deep | black iron, green nickel, coal, lead, native silver, brassy copper, tin |
| Abyssal | orichalcum, mythril, gold-bearing quartz, native silver, aquamarine, sapphire, the fallen star |

The mean deposit tier rises level by level: 1.05 on the first level, 1.42, 1.91, 2.34, 2.58, then 3.00 on the sixth.

## 4. Mining

Every deposit is an `OreDeposit`, the crafting system's ore seam, set up from its entry (`OreDeposit.Configure`):

* **A pickaxe** (a weapon whose Tool Type is *Pick Axe*): every hit adds the pick's **Tool Damage** to the work on the
  current lump. Each *Work Per Lump* breaks one lump off into the hitting player's inventory (dropped beside the
  deposit when the inventory is full). Hits reach deposits through the attack's normal object hits
  (`IWeaponHittable`), so no extra input is needed.
* **Too hard:** a pick below *Min Tool Damage* only rings off the rock; the player is told it needs a better pick.
* **By hand:** *Interact* gathers one of the *Loose Stones* before a pick is needed.
* **Gems:** a seam with a *Gem* gives a rough stone of it with some lumps (*Gem Chance*; the same lump always does).
* **Worked out:** the mineral disappears, the bare rock stays, and it grows back after *Regrow Minutes* of play (×
  *Between Visits ▸ Regrow Scale*). The cave remembers it across visits (05 §4).

Events for quests and statistics: `OreDeposit.LumpTaken` / `AnyLumpTaken`, `CaveInstance.Mined` (cave, deposit, player,
item), `CaveInstance.MinedOf(name)`, `CaveInstance.LumpsMined`.

## 5. The look of a deposit

Without a prefab a deposit is generated (`CaveDepositArt`): a lump of rock with the mineral showing on the side facing
the cave. Nuggets for ores, coal and stone, softer lumps for supplies, crystals for gemstone veins. There are three shapes
per kind, shared by every deposit (one mesh each, never one per deposit). Colours come from the Crafting Database
(the ore's, gem's or supply's colour) with *Database Colors*, or from the entry. Rare minerals *Glow*. Wall deposits are
half sunk in the drawn wall, facing out; floor deposits are mounds. Deposit objects are **pooled**: a cave entered again
reuses them.

## 6. Adding a material

1. Add it to the Crafting Database (its ore, gem or supply definition), as for any crafting material.
2. Add an entry to the cave's Resource Table: Kind and Craft Id of the new material, its depth range and peak, weight,
   rarity, mount, vein and mining values.
3. *Validate Cave Setup* (or the table's inspector) checks that the id gives an item and that every depth band still has
   resources.

An entry can also give any item asset (**Item Override**), for example a quest crystal that isn't a crafting material.
