# Caves 04 — Creatures and Depth

## 1. Which creatures, and where

A cave's creatures are the dungeon generator's **encounters**, placed by its Population stage. They come from the
profile's **Encounter Table**, with the same budgets, packs, elites, ambush waves, sleepers and roamers as any dungeon,
and they are the project's ordinary mob prefabs with their own AI and combat. The cave setup imports the world's mobs
(EndlessTerrain ▸ Mobs) and spreads them by rarity:

| World mob's Rarity Level | In caves from |
|---|---|
| 1–3 | the first level |
| 4–6 | the second level (*Min Floor* 1) |
| 7+ | the third level (*Min Floor* 2) |

Rarer mobs are also rarer and cost more budget (`SpawnableMobImport`). Edit the table freely: *Min Floor* / *Max Floor*,
*Roles* (only in guardian chambers, nests…), *Styles*, *Progress* (how far along the level), *Boss*.

Population places creatures by **room size** (budgets scale with the area), **location** (role rooms, the main path,
progress along it) and **depth** (difficulty grows per level: *Difficulty Per Floor* 0.25, and with the entrance's
difficulty). It never places them inside walls, on deposits or on formations: their cells are *Occupied*, and creatures
are dropped onto the NavMesh. The headless checks found no creature, chest or prop on a deposit or formation cell among
131,967 placements (07 §2).

## 2. Deep-Dweller: tougher with depth

Where the rules run (offline, the server), every creature below the Shallow band gets **Deep-Dweller**
(`CaveDeepDweller`), per band below Shallow (Medium 1, Deep 2, Abyssal 3):

| Setting (Cave Mining Stage ▸ Creatures) | Default per band | Through |
|---|---|---|
| Endurance Per Band | +4 Endurance | the creature's `CombatStats` (Endurance raises max health; the creature starts full) |
| Defense Per Band | +5 Defense | `CombatStats` (less physical damage taken, diminishing returns) |
| Damage Percent Per Band | +8 % damage dealt | a damage-dealt modifier on its `CombatEntity` (every kind of damage) |

These are the same stats equipment and buffs use, so the creature's inspection and the damage numbers show them.
*Depth Toughness* off = none. The depth used is the creature's chamber's (`CaveInstance.DepthOf(floor, area)`).

## 3. Guarded veins and hoards

Below the first level, some rich veins become **guardian chambers** (02 §4): Population puts an elite and its guards in
them, and the ways in close while they fight (the dungeon's room event). The ore is the reward. Boss, mini-boss and
treasure rooms are **hoards**: more deposits, counted deeper, rarer resources favoured.

## 4. Deeper caves

The lowest level's passage, the **way deeper** (with the cave setup's manager: *Exit Portal Action = Next Dungeon*),
generates the next cave of a chain (`DungeonRequest.Next()`: depth + 1, a new seed derived from this one). Each deeper
cave counts *Per Deeper Cave* (2) deeper, so a chain reaches the Abyssal band quickly. Depth stops counting at **Max
Depth** (8), the bottom of the world: below it every cave is as rich as it gets. The way out of any cave of a chain is
its entrance passage, which leads back to the world mouth. A deeper cave is named after its system: "Whispering Grotto
— Depth 2".

## 5. Reading depth from gameplay code

```csharp
CaveInstance cave = CaveInstance.Current;            // the cave this machine's player is in (or null)
float depth = cave.DepthAt(player.transform.position);
CaveDepthBand band = cave.BandAt(player.transform.position);   // Shallow, Medium, Deep, Abyssal
CaveInstance.Mined += (c, deposit, who, item) => { /* quests, statistics */ };
```

`CaveDeposit` (next to each deposit's `OreDeposit`) knows its resource entry, depth and tier.
