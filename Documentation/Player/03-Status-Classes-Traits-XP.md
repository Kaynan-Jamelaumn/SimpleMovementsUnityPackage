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
| `HealthManager` | Hit points | Regenerates slowly; damage / heal factors; 0 = death |
| `StaminaManager` | Energy for sprinting, jumping, dashing, rolling, attacks | Spent by actions, regenerates when idle |
| `ManaManager` | Ability resource | Spent by abilities, regenerates |
| `HungerManager`, `ThirstManager`, `SleepManager` | Survival needs | Decrease over time (*Should Consume*); food and drink restore them |
| `SanityManager` | Mental state | Changed by effects and events |
| `BodyHeatManager` | Temperature | Changed by status effects (cold, heat) |
| `OxygenManager` | Breath | Changed by status effects (drowning, thin air) |
| `WeightManager` | Carried weight vs capacity | The inventory sets it; overweight slows the player |
| `SpeedManager` | Movement speed | Base speed, running / crouching multipliers, slows and hastes |

Common fields:

| Field | Meaning |
|---|---|
| Current Value / Max Value | The value and its maximum |
| Tick Rate / Consuming Tick Rate | Seconds between regeneration / consumption ticks |
| Increment Value / Decrement Value | Amount per tick when regenerating / consuming |
| Increment Factor / Decrement Factor | Multipliers on those amounts (traits, equipment, effects change them) |
| Should Consume | The value goes down over time (hunger, thirst, sleep) |
| UI Image | The bar's fill image (assigned by the inventory UI builder: status bars are built automatically) |

The **status bars** (top left, with "75 / 100" values) are created by the inventory UI builder, one per status type
the player has, and wired to each manager's *UI Image* ([Inventory 06 §3a](../Inventory/06-Editor-Tools.md)).

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
**Log Component Hierarchy**. *Tools ▸ Player Status ▸ Validate All Controllers / Auto-Setup All Controllers* run them
on every player in the open scenes.

---

## 3. Player classes

*Assets ▸ Create ▸ Scriptable Objects ▸ Player Class* (or *Assets ▸ Create ▸ Player Status ▸ Player Class Template*).

| Section | Fields |
|---|---|
| Basic Info | Name, description, lore, icon, colour |
| Base Stats | Health, stamina, mana, speed, hunger, thirst, weight capacity, sleep, sanity, body heat, oxygen |
| Combat Stats | Strength, agility, intelligence, endurance, defense, magic resistance |
| Special Stats | Critical chance, critical damage, attack speed, casting speed |
| Leveling | Stat gains per level and stat multipliers |
| Traits | Available, exclusive and **starting** traits, trait points, preferred / difficult trait types (cheaper / more expensive) |

`PlayerStartItemController` gives each class its starting items (a list of item prefabs per class name).

---

## 4. Traits

A trait is an asset (*Assets ▸ Create ▸ Scriptable Objects ▸ Trait*, or **Trait From Preset…**):

| Part | What it does |
|---|---|
| **Cost** | Trait points: positive = a benefit that costs points, negative = a drawback that gives points back |
| **Passive Modifiers** | Always-on stat changes: +20 % max health, −15 % damage taken, +10 % move speed… |
| **Behaviours** | Things the trait lets the character *do* or reacts with: double jump, wall climb, glide, an ability on a key, second wind, life steal… (picked from a dropdown) |
| Dependencies | Incompatible traits, required traits, exclusive groups (Tall / Short) |
| Legacy Effects | Old string-based effects (still applied; weapons read some of them) |

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

The **Trait Database** (`Resources/TraitDatabase`) lists every trait of the game for the character creation screen
and lookups. *Tools ▸ Traits ▸ Validate All Traits* checks them all.

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
