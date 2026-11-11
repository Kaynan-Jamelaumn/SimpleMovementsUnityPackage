# Rune Magic 03 — Rune acquisition, absorption and energy

Monsters carry runes in their bodies or souls. A defeated monster may drop a rune, and its killer gains magical energy. A Mage absorbs runes into their soul, as long as the soul has room, and chooses which rune receives each kill's energy (design spec 3, 5, 6.2).

Rank, Quality, Energy and Concept knowledge stay four separate values:
- the maximum energy comes from the rank only (*Quality Affects Max Energy* is off);
- full energy never advances a rank;
- a high rank never reveals a concept.

All numbers are data, in the *Rune Magic Database*:
- **Data ▸ Drop Profiles**;
- **Data ▸ Balance ▸ Acquisition**.

## Where runes come from

### Drop profiles: which monsters carry which runes

A profile lists:
- **runes**, each with a weight and optional rank limits;
- a **drop chance** per kill and **Max Drops**;
- a **base rank**, a **quality range** and the **starting energy** of its runes (a fraction of their maximum);
- the **magical energy** the killer gains.

**How a monster gets its profile** (the highest **priority** wins):
1. Its **Rune Source** names one.
2. Otherwise a profile matches by:
   - **words** of the monster's species (mob *type*), name or faction (whole words, any case; `FireElemental` counts as "fire elemental");
   - **tags** (special properties given on the Rune Source).
3. Otherwise the **default profile** applies (`generic`). With no default profile, the monster carries nothing.

**Built-in profiles.** The first six are the spec's illustrative table:

| Profile | Matches (words) | Runes | Energy |
|---|---|---|---|
| Fire Elemental | fire, flame, ember, inferno, salamander, magma, lava, blaze | Fire, Heat, Energy | 150 |
| Wind Beast | wind, storm, gale, harpy, griffin, roc, tempest, cyclone | Wind, Gale, Vibration | 140 |
| Stone Golem | golem, stone, rock, earth, gargoyle, boulder, granite | Earth, Matter, Armor | 160 |
| Shadow Creature | shadow, shade, wraith, specter, phantom, ghost, dark, umbral | Shadow, Mind, Absorption | 150 |
| Crystal Guardian | crystal, gem, prism, quartz, crystalline | Crystal, Space, Reflection | 170 |
| Ancient Dragon | dragon, drake, wyrm, wyvern | Fire, Gravity; rarely Time, Soul. Rank 2+, up to 2 drops, quality 40-90 | 1,500 |
| Beast, Undead, Aquatic, Venomous, Sylvan, Construct, Storm Spirit, Humanoid | (see the database) | Proposals | 80-150 |
| Any monster (default) | — | Wind, Fire, Water, Earth, Energy | 100 |

### What changes the drop (*Balance ▸ Acquisition*)

| Factor (design spec 3.1) | Effect |
|---|---|
| **Species** | Picks the profile |
| **Monster rank** (Rune Source) | Its runes are at least that rank |
| **Magical alignment** (Rune Source ▸ Alignments) | Runes of that alignment weigh ×3, and can drop even when the profile does not list them |
| **Rarity**: Normal, Elite, Rare, Boss | Chance ×2 / ×3 / ×5; rank +0 / +1 / +1; quality +5 / +10 / +15; energy ×2 / ×3 / ×6 |
| **Rune rarity** | Rare monsters and bosses use the *Rare Monsters* rarity weights, so unusual, powerful runes come more often |
| **Dungeon difficulty** (tier) | +10 % chance, a 15 % chance of +1 rank, +4 quality and +25 % energy, per tier |
| **Boss** | Always drops at least one rune; up to one extra drop |
| **Special properties** (Rune Source ▸ Tags) | Match profiles' tags |
| **Zone** | The hub world zone's reward multiplier scales the energy (richer far from the city) |

**Ranks** stay within:
- the rune's own ranks;
- the entry's limits;
- *Max Drop Rank* (5).

**Qualities** are whole numbers from 0 to 100.

### The Rune Source (mob hook, directive 4)

Add *Rune Source* to a mob prefab to give it rune data. It only adds data: the mob, its AI, loot, experience and essences are unchanged.

| Field | Default |
|---|---|
| **No Runes** | — |
| **Profile** | — |
| **Alignments** | — |
| **Tags** | — |
| **Monster Rank** | — |
| **Rarity** | *Auto* = from the dungeon placement |
| **Tier** | −1 = from the dungeon |
| Chance / energy multipliers | — |
| **Guaranteed** | Runes it always drops |

Without a Rune Source, the species, name, faction and dungeon placement decide.

### The kill (server-side)

Kills are handled where the rules run: offline, or on the server. The handler listens to `CombatEvents.Killed`, like item drops and essence absorption, and changes neither.

**What counts:**
- Only kills by a player or their summon count.
- Summons and players carry no runes.

**What the kill gives:**
- **Energy** goes to the **killer only**, if a rune user (decision D5). It waits in the soul's pending pool.
- **Rune drops** roll for kills by rune users (*Drop Killers ▸ Rune Users*), so other classes never see runes they cannot use.
  - Set *Drop Killers ▸ Any Player* for a rune economy where anyone finds runes and sells or trades them to Mages.
- Dropped runes are world items that **anyone can pick up** (decision D4).

### Other sources

**Dungeons, chests, merchants, quests:** create a **Rune Item** asset (*Create ▸ SimpleMovements ▸ Rune Magic ▸ Rune Item*: rune, rank, quality, energy). It goes anywhere an item does: loot tables, chests, merchants' stock, quest rewards.

**From code:**
- `RuneItems.Spawn(drop, position, source)` drops a rune;
- `soul.AbsorbDrop(drop, origin, out reason)` absorbs one directly (a reward).

## Rune items

An unabsorbed rune is an inventory item:
- **Id:** `rune:<rune>:<rank>:<quality>:<energy>`, e.g. `rune:fire:1:42:50`.
- **Same everywhere:** every machine makes the same item from the id, so saves, merchants, trades and the network all resolve it.
- **Stacking:** identical runes stack (*Item Stack*, 5).
- **Price:** merchants pay *Item Price Per Rank* × rank × rarity (×1 common … ×16 legendary).
- **Tooltip:** rank, rarity, quality, energy / maximum, slot cost, alignment, and the number of (hidden) concepts.

Any class can carry, sell or trade a rune; only Mages can absorb it.

## Absorbing (design spec 3.2, 4)

**How:** use the rune item, or press **Absorb** in the Rune Soul window.

**What the server checks:**
- The player is a rune user.
- The rune's **slot cost** fits in the **free soul slots**. With no capacity the rune cannot be absorbed: free a slot (combine, or release a rune) or increase the capacity.
- The item is in the inventory. It is consumed only when the rune is absorbed.

**Alignment conflict.** A rune whose alignment conflicts with the runes held strains the soul:
- the strain is the sum of the negative alignment rules between them (fire against water = 0.4);
- it is applied as *Conflict Damage* (25) per unit, as Soul Damage of kind `incompatible_absorption`.

**The absorbed rune keeps:**
- rank, quality and energy (up to its maximum);
- its **origin** (item, reward, …);
- an empty **synthesis history**, filled by later phases.

## Releasing (design spec 4.2, decision D12)

**Where:** in the Rune Soul window, *Release*, then confirm.

**Rules:**
- It is not allowed in combat, by default (*Release In Combat*).
- The rune and its energy are gone.
- *Allow Release* turns releasing off.

## Rune Energy (design spec 5)

- **Gaining it:** a kill's energy goes to the **pending pool** (*Max Pending Energy*: 0 = no limit).
- **Choosing the rune:** the player gives pending energy to the rune they choose, any amount or all of it (Rune Soul window). **Only that rune receives it.**
- **A full rune** takes nothing: choose another rune. What does not fit stays pending.
- **No automatic advancement.** Nothing ever advances a rank or turns overflow into a higher-rank rune (5.2, 5.4).
- **Maximum by rank:** 1,000 / 5,000 / 25,000 / 125,000 / 625,000 (*Balance ▸ Energy By Rank*).
- **Soul Collapse** loses the pending energy (*Soul Collapse ▸ Lose Pending Energy*).

## Concept knowledge (decision D13)

Concept knowledge belongs to the player, per rune definition:
- releasing a Wind rune and absorbing another one keeps the Wind concepts already discovered;
- unknown concepts stay hidden ("???");
- discovery itself (dungeon riddles and minigames) is [chapter 04](04-Concept-Discovery.md). `RuneSoul.LearnConcept(conceptId)` records one directly (tools, rewards).

## The Rune Soul window

**Opening it:**
- with the `RuneSoul` input action of your Input Actions asset (no key is fixed in code; see [soul resources ▸ Input](02-Soul-Resources.md#input));
- or with *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Toggle Rune Soul Window (Play Mode)*.

Only rune users can open it. Escape closes it, and it frees the cursor while open.

**What it shows:**
- **Header:** Soul Integrity and condition, slots used of the capacity, magical energy waiting.
- **Runes in your soul:** name and rank, quality and its tier, energy / maximum, slots. Click one to select it.
- **The selected rune:**
  - alignment and its domain, magical properties;
  - known concepts with their descriptions, then "??? ×N" for the unknown ones;
  - synthesis history, flaws, origin;
  - an energy bar with **Give** (choose the amount) and **Give all**;
  - **Release**, with confirmation.
- **Runes in your bag**, each with **Absorb**, disabled when there is no room (the reason is shown).
- The latest message about the soul.

## Network (FishNet)

**What the server sends** (NetRuneMagic, to the **owner only**):
- the soul view (pending energy included), whenever it changes, at most 4 times a second;
- the rune list (runes and concept knowledge), when it changes.

**What the owner asks:**
- absorb (item id);
- release (rune id);
- give energy (rune id, amount).

The server checks everything again, and the requests are rate-limited.

## Debug

**Debug panel**, where the rules run:
- runes and pending energy;
- *Energy +500*;
- *Absorb a rune*;
- *Drop a rune* (in front of the player);
- *Energy → first rune*;
- *Learn a concept*;
- *Release first*.

**Inspector:** the Rune Soul inspector lists the runes, adds energy and opens the window.

**Logging:** `RuneKillListener.LogRolls = true` logs every kill's roll.

## Tests

*Window ▸ General ▸ Test Runner ▸ Edit Mode*:
- **`RuneMagic.Tests.RuneAcquisitionTests`** (pure) covers:
  - the spec's monster table;
  - profile matching: words, camel case, plurals, faction, tags, priority, Rune Source names, the default profile;
  - chance by tier, rarity and boss;
  - deterministic rolls within the rank, quality and energy rules;
  - bosses always dropping;
  - rank and quality growing with difficulty;
  - alignment and rarity weights;
  - energy rewards (the spec's 150), tier, boss and zone;
  - energy to the chosen rune only;
  - full and overflow rules, with no rank-up;
  - the pending cap;
  - independent attributes;
  - capacity-checked absorption and slot costs;
  - alignment strain;
  - release rules;
  - concept knowledge per definition;
  - saving runes and pending energy;
  - rune item ids;
  - the rune list view.
- **`RuneMagic.Tests.RuneItemRuntimeTests`** (category `RequiresEngine`) covers:
  - rune items made from ids;
  - their tooltip and validation;
  - the Rune Source;
  - a soul without a rune user.
