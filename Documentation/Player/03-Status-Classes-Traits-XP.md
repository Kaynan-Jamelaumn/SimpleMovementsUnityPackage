# Player 03 — Status, Classes, Traits & Experience

**Scripts:** `Essentials/Status/*` (status managers, `BaseStatusController`), `Player/RPGSystem/PlayerStatusController.cs`,
`PlayerStatusSetupHelper.cs`, `Classes/Class.cs` (`PlayerClass`), `Trait/*` (`Trait`, `TraitManager`, `TraitDatabase`,
behaviours, presets), `ExperienceManager.cs`, `PlayerStartItemController.cs`.

---

## 1. Status managers

Each status is a component deriving from `StatusManager`, on the player next to `PlayerStatusController`. The same
managers are used by mobs (mobs need at least `HealthManager` and `SpeedManager`).

| Manager | What it represents | Typical behaviour |
|---|---|---|
| `HealthManager` | Hit points | Regenerates slowly; damage / heal factors; 0 = unconscious (with `Unconsciousness`) or death |
| `StaminaManager` | Energy for sprinting, jumping, dashing, rolling, attacks | Spent by actions, regenerates when idle |
| `ManaManager` | Ability resource | Spent by abilities, regenerates |
| `HungerManager`, `ThirstManager` | Survival needs | Drain one tick every *Consuming Tick Rate* s (*Should Consume*); food and drink restore them; empty = damage |
| `SleepManager` | Rest (100 = rested) | Drains while awake, tiredness stages, sleep in beds / on the ground restores it ([12](12-Survival-Death-and-Respawn.md)) |
| `SanityManager` | Mental state | Auras, hits, items, abilities lower / restore it; stages of consequences ([12](12-Survival-Death-and-Respawn.md)) |
| `BodyHeatManager` | Body temperature | Follows the weather; ideal range per race / class; heat / cold resistance; cold and heat effects ([12](12-Survival-Death-and-Respawn.md)) |
| `OxygenManager` | Breath | Changed by status effects (drowning, thin air) |
| `WeightManager` | Carried weight vs capacity | The inventory sets it; overweight slows the player |
| `SpeedManager` | Movement speed | Base speed, running / crouching multipliers, slows and hastes |

Common fields:

| Field | Meaning |
|---|---|
| Current Value / Max Value | The value and its maximum |
| Tick Rate | **Regeneration** tick: seconds between two gains (health, stamina, mana) |
| Consuming Tick Rate | **Consumption** tick: seconds between two losses of a status that drains by itself (hunger, thirst) |
| Increment Value / Decrement Value | **How much** is gained / lost per tick |
| Increment Factor / Decrement Factor | **Percent** change of every gain / loss: +50 = 50% more, -50 = half, 0 = unchanged (it is not a multiplier: 1 means +1%) |
| Should Consume | The value goes down over time by itself (hunger, thirst) |
| UI Image | The image the status fills (the Player HUD assigns its own) |

Example: hunger with *Consuming Tick Rate* 6, *Decrement Value* 1 and *Decrement Factor* 0 loses 1 point every 6 s =
10 per minute (a bar of 100 lasts 10 minutes). *Decrement Factor* 50 makes it 1.5 per tick (15 per minute). Cold
(body heat) and sleeping change the drain through drain multipliers on top (`StatusManager.SetRateMultiplier`).

The **status bars** are drawn by the [Player HUD](11-Player-HUD.md) (status frame and needs gauges). The inventory UI
builder no longer generates the old "StatusBars" panel: it adds the Player HUD instead (an old panel in your prefab is
hidden by the HUD while it runs, and can be deleted).

---

## 2. `PlayerStatusController`

The hub that owns every manager, the movement / dash / roll models, the experience and trait managers, the armor set
manager and the **player class**. Other systems reach the player's stats through it.

| Setting | Meaning |
|---|---|
| Current Player Class | The `PlayerClass` asset; its base stats are applied at start (*Auto Apply Class Stats*) |
| Auto Apply Starting Traits | Gives the class's starting traits at start |

Inspector tools: **Complete Setup Wizard**, **Auto-Assign All Components**, **Create Missing Components**, the
*Setup … Only* buttons, **Validate Setup**, **Validate Status Values**, **Apply Reasonable Defaults**,
**Log Component Hierarchy**. *Tools ▸ SimpleMovements ▸ Validate ▸ Players* and *Tools ▸ SimpleMovements ▸ Auto-Assign ▸ Auto-Setup All Players In Scene* run them
on every player in the open scenes.

---

## 3. Player classes

*Assets ▸ Create ▸ SimpleMovements ▸ Character ▸ Player Class* (or *Assets ▸ Create ▸ SimpleMovements ▸ Legacy ▸ Player Class (Old Template)*).

| Section | Fields |
|---|---|
| Basic Info | Name, description, lore, icon, colour |
| Base Stats | Health, stamina, mana, speed, hunger, thirst, weight capacity, sleep, sanity, body heat, oxygen |
| Combat Stats | Strength, agility, intelligence, endurance, defense, magic resistance |
| Special Stats | Critical chance, critical damage, attack speed, casting speed |
| Leveling | Stat gains per level and stat multipliers |
| Traits | Available, exclusive and **starting** traits, trait points, preferred / difficult trait types (cheaper / more expensive) |
| Archetype | Optional `CharacterArchetype`: growth per level, attribute scaling, passives, abilities, trait affinities |

The **Combat** and **Special** stats reach the combat system through a `CharacterIdentity` on the player (Critical
Damage counts above 150, Attack / Casting Speed above 1), and level-up points spent on Strength, Agility, Intelligence
or Endurance are added to Combat Stats. Races, class archetypes, the full stat reference, buffs / debuffs and threat
are in [06 — Races, Classes, Advanced Stats, Buffs & Threat](06-Races-Classes-Stats-and-Threat.md).

`PlayerStartItemController` gives each class its starting items (a list of item prefabs per class name).

---

## 4. Traits

A trait is an asset (*Assets ▸ Create ▸ SimpleMovements ▸ Character ▸ Trait*, or **Trait From Preset…**):

| Part | What it does |
|---|---|
| **Cost** | Trait points: positive = a benefit that costs points, negative = a drawback that gives points back |
| **Passive Modifiers** | Always-on stat changes: +20 % max health, −15 % damage taken, +10 % move speed… |
| **Behaviours** | Things the trait lets the character *do* or reacts with: double jump, wall climb, glide, an ability on a key, second wind, life steal… (picked from a dropdown) |
| Dependencies | Incompatible traits, required traits, exclusive groups (Tall / Short) |
| Legacy Effects | Old string-based effects (still applied; weapons read some of them) |

**Active traits.** A trait whose behaviour is *Active Ability On A Key* is an **active trait**: it gives an ability on
its own key. A character can **pick only one** (*Trait Manager ▸ Max Active Traits* and *Character Creation UI ▸ Max
Active Traits*, default 1); the creation screen marks them *[Active]* and refuses a second one. Traits that only use
existing keys (Double Jump, Wall Climb, Glide) are not active traits, and traits given by the race, class or equipment
do not count. The key: the behaviour's *Input* if assigned, else the first input action named *ActiveTrait*,
*TraitAbility* or *UseTrait* (rebindable in the settings), else the *Fallback Key* (J).

The `TraitManager` (on the player) applies and removes traits **exactly** (every change is recorded and undone),
counts points, enforces dependencies, supports temporary traits, and tracks who granted a trait (the player, armor,
a set, the class): taking the armor off releases only its grant. Armor sets can strengthen, extend or replace traits
([Inventory 04](../Inventory/04-Armor-and-Sets.md)).

```csharp
TraitManager traits = player.GetComponentInChildren<TraitManager>();
traits.AddTrait(myTrait);                         // pays its cost
traits.AddTrait(myTrait, ignoreRequirements: true, isTemporary: true, duration: 30f);
traits.RemoveTrait(myTrait);                      // refunds exactly what was paid
```

The **Trait Database** (`Resources/SimpleMovements/Databases/TraitDatabase`) lists every trait of the game for the character creation screen
and lookups. *Tools ▸ SimpleMovements ▸ Validate ▸ Traits* checks them all.

### Weapon traits vs player traits

A trait in a weapon's **Weapon Traits** list belongs to the weapon (it changes only that weapon's attacks); a trait
granted **While Wielded** goes to the player. See [Inventory 05](../Inventory/05-Weapons-and-Attacks.md).

---

## 5. Experience and levels

`ExperienceManager` holds the current XP, the XP to the next level, the level and the points earned (skill,
attribute and stat-upgrade points). Each level applies the class's stat gains; *Special Level Rewards* give extra
rewards at chosen levels.

```csharp
xpManager.AddExperience(50);
xpManager.OnLevelUp += level => Debug.Log($"Level {level}!");
```

Events: `OnLevelUp`, `OnSkillPointGained`, `OnAttributePointGained`, `OnStatUpgradePointsGained`, `OnStatUpgraded`.
