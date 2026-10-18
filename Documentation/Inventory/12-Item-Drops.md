# 12 — Item Drops: Drop Tables and the Item Drop Manager

What chests, urns, barrels, crates, mobs, elites and bosses drop: which items, how many, how likely, and from which
tier. Two assets do it:

| Asset | What it is |
|---|---|
| **Item Drop Table** (`ItemDropTable`) | One loot list: items that always drop, a weighted random pool rolled a number of times, and other tables used as groups. Every entry has its chance, quantity and tier range. |
| **Item Drop Manager** (`ItemDropManager`) | The one place that holds every table of the game (the "inventory" of what can drop and from where, like Albion Online's loot tables), the default tables for chests, breakables, mobs, elites and bosses, global multipliers and items that never drop. |

## 1. Setup (once)

1. **Tools ▸ SimpleMovements ▸ Inventory ▸ Item Drops ▸ Create Item Drop Manager.** It creates
   `Assets/Resources/SimpleMovements/Items/Drops/ItemDropManager.asset` (keep it there under that name) with two
   empty tables in `Assets/Resources/SimpleMovements/Items/Drops/Tables`: **Chest Drops** and **Breakable Drops**, already set as the
   manager's Chest and Breakable tables.
2. Select **Chest Drops**, select item assets in the Project window and press **Add Selected Items To Pool** (or
   *Add Selected As Guaranteed*). Set each entry's weight, chance, quantity and tiers.
3. Do the same for **Breakable Drops** (it starts with 1 roll and a 50 % chance of nothing: urns are often empty).
4. Press Play: every dungeon chest and breakable (urns, barrels, crates) without its own table now drops from these.

Without a manager asset nothing breaks: defaults are used, but there are no default tables, so only sources with a table
of their own drop items.

## 2. An Item Drop Table

Create more with **Create ▸ SimpleMovements ▸ Items ▸ Item Drop Table** (or the manager's *Create New Drop Table*).

| Field | Meaning |
|---|---|
| Display Name, Notes | Name in the manager and the logs (empty = the asset's name) |
| **Guaranteed** | Entries dropped every time (each still has its own *Chance*: 0.5 = half the time) |
| **Rolls** | How many times the pool is rolled (min – max) |
| Extra Rolls Per Tier | More rolls at higher tiers (0.5 = one more every two tiers; the fraction is a chance) |
| **Nothing Chance** | Chance that a roll gives nothing (an empty urn) |
| Unique Picks | The same entry cannot come out twice in one drop |
| **Pool** | The random items, picked by *Weight* |
| Groups | Other tables rolled as one entry of the pool ("common materials", "rare gems"), weighted with the items |

Each entry (`ItemDropEntry`):

| Field | Meaning |
|---|---|
| Item | The item asset (it needs a prefab to appear in the world) |
| Weight | Relative chance among the pool's entries |
| Chance | Chance it really drops once picked |
| **Quantity** | How many (min – max); above the item's stack size it is split into several pickups |
| Quantity Per Tier | Extra quantity per tier above its Min Tier |
| Min Tier / Max Tier | The tiers of the source at which it can drop (-1 = no limit) |

**Tier** is the source's level: the dungeon placement's tier for chests, breakables and mobs (deeper floors, treasure
and boss rooms give higher tiers), plus the source's *Tier Bonus*; elites and bosses add the manager's bonuses. Outside
dungeons it is 0 unless set.

The inspector shows problems (items without a prefab, a group containing its own table, an empty table), registers the
table in the manager, and **Simulate** rolls it N times at a tier: how often each item drops and how many on average.

## 3. The Item Drop Manager

| Section | Fields |
|---|---|
| Every drop table | **Tables**: the registry. *Find All Drop Tables In The Project* fills it. |
| Default tables | **Chest Table**, **Breakable Table**, **Mob Table**, **Elite Table** (empty = the Mob table with the elite bonuses), **Boss Table** (empty = Elite, else Mob); *Drop From Every Mob*; *Only Player Kills* |
| Global multipliers | Drop Chance Multiplier, Quantity Multiplier, Elite / Boss Tier Bonus, Elite Chance Multiplier |
| World | Spread Radius, Drop Height (items fall to the ground around the source) |
| Never dropped | **Blocked Items**: no table drops them (quest items, unfinished items) |
| Debug | Log Drops |

The inspector lists **every item that can drop and from which tables** (blocked ones marked).

## 4. Who uses which table

| Source | Table used (first one set) |
|---|---|
| Dungeon **loot entry** (DungeonLoot asset ▸ entry ▸ *Drop Table*) | the entry's table → the chest / breakable's own → the manager's Chest / Breakable table |
| Dungeon **prop entry** (DungeonProps ▸ entry ▸ *Drop Table*), e.g. Barrel, Crate, Urn | the entry's table → the manager's Breakable / Chest table |
| A **DungeonChest** placed by hand | its *Drop Table* → the manager's Chest table; plus its *Drops* prefabs |
| A **DungeonBreakable** placed by hand | its *Drop Table* → the manager's Breakable table (*Use Default Table*) |
| A mob with **Item Drop On Death** | its *Table* → the manager's Mob / Elite / Boss table; plus *Extra Prefabs* |
| A mob with an **Item Spawner** that has a *Drop Table* | that table (rolled with *Table Tier*) and its prefabs |
| Any other mob | the manager's Mob / Elite / Boss table, when *Drop From Every Mob* is on |

Summons never drop. With *Only Player Kills* mobs drop only when a player (or a player's summon) killed them.

Dungeon loot entries also have **Roles**: an urn only in crypts, a weapon crate only in armories (empty = any room).
With a prefab of your own, *Add Mechanics* gives it the placeholder's behaviour (a Chest placeholder makes it a chest,
a Barrel a breakable) when it has none, so it opens / breaks and drops.

## 5. Multiplayer

Drops are rolled where the game rules run (offline, or on the server) and become networked world items; clients never
roll. Nothing to set up.

## 6. Scripts

| Script | Role |
|---|---|
| `ItemDropTable` (+ `ItemDropEntry`, `ItemDropGroup`, `DropAmount`, `ItemStack`) | A table and its rolling |
| `ItemDropManager` | Registry, defaults, multipliers; `Roll`, `Drop`, `Spawn`, `Give` (into an inventory), the `Dropped` event; drops for mobs without a component |
| `ItemDropOnDeath` | A mob's own table |
| `ItemSpawner` | Now also takes a *Drop Table* |
| `DungeonChest`, `DungeonBreakable` | Drop from their table at their tier |

From code: `ItemDropManager.Drop(table, tier, position, source)` drops in the world;
`ItemDropManager.Give(ItemDropManager.Roll(table, tier), inventory)` puts a roll straight into an inventory (quest
rewards) and returns what did not fit.

## 7. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| A chest / urn opens or breaks but drops nothing | No table: set the entry's *Drop Table* or the manager's Chest / Breakable table; check *Nothing Chance* and the entries' tiers with *Simulate* |
| "has no prefab: it cannot be dropped" | The item asset needs a world prefab |
| Mobs drop nothing | *Drop From Every Mob* off, no Mob table, *Only Player Kills* and a non-player killer, or the mob has its own Item Drop On Death / Item Spawner |
| An item never drops | It is in *Blocked Items*, or its Min Tier is above the source's tier |
| A custom urn prefab cannot be broken | *Add Mechanics* off on its entry, or its placeholder is not Barrel / Crate / Urn-like: pick the placeholder whose behaviour it should get |
