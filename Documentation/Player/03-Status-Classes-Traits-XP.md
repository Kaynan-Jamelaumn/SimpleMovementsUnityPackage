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

### Disabilities (drawbacks that give points)

A trait with a **negative cost** is a drawback: picking it **gives** that many trait points to spend on other traits.
**69 disabilities** come ready-made - they take an ability, a sense, a limb or an appetite away, for good or now and
then:

* *Tools ▸ SimpleMovements ▸ Player ▸ Create Disability Traits* creates them all in `Characters/Traits/Disabilities`
  (or the selected folder), adds them to the Trait Database and makes the degrees of one thing mutually exclusive.
  Run it again after an update: traits that exist are kept, new ones are added.
* One at a time: *Trait From Preset ▸ Disability*, or the Trait Database's preset menu.
* A class that lists its own *Selectable Traits* needs them added there to offer them at character creation.

**How many**: a new character may take at most **5 drawbacks** giving at most **12 points** in all (*Character
Catalog ▸ Max Drawbacks / Max Drawback Points*; the old *Character Creation UI* has the same two fields; 0 = no
limit). Traits given later by quests or items are not counted.

**Legs and movement**

| Trait | Points | What it does |
|---|---|---|
| **Limp** | +1 | -12% move speed, -15% sprint speed. |
| **Bad Leg** | +3 | -20% move speed, -20% jump, cannot sprint. |
| **Lame** | +5 | -35% move speed; cannot sprint, jump, dash or roll. |
| **Missing Foot** | +4 | The left foot is missing: hobbles (injury system) or cannot sprint, dash or roll. |
| **One-Legged** | +6 | The left leg ends at the knee: hops (injury system) or cannot sprint, jump, dash or roll. |
| **Stiff Joints** | +2 | Cannot dash or roll. |
| **Bad Knees** | +2 | Cannot jump; +30% fall damage, -1.5 m safe fall height. |
| **Trick Knee** | +2 | While sprinting the knee gives way now and then: a stumble, then -35% move speed for a while. |
| **Two Left Feet** | +2 | -25% jump; cannot dash; trips now and then while sprinting. |
| **Vertigo** | +2 | In the air the world spins now and then: dizzy and slower for a while. |
| **Poor Balance** | +2 | Always a little dizzy; +20% fall damage; cannot roll. |
| **Overweight** | +1 | -6% move speed, -10% sprint speed, +20% stamina cost. |
| **Obese** | +3 | -14% move speed, -20% sprint speed, +45% stamina cost, +25% fall damage; cannot roll. |
| **Underweight** | +2 | -12% max health, -25% carry weight, -8% weapon damage, feels the cold. |
| **Bad Back** | +2 | -30% carry weight, +15% fall damage; cannot roll. |

**Arms, hands and what the character will not do**

| Trait | Points | What it does |
|---|---|---|
| **Missing Hand** | +4 | The off hand is missing: no two-handed weapons, nothing in the off hand. |
| **One-Armed** | +5 | The off-hand arm is missing: no two-handed weapons, no shield, nothing in the off hand. |
| **Weak Grip** | +2 | -12% attack speed, -20% charge, draw and reload speed. |
| **Tremor** | +2 | The hands shake, some hours hardly, some badly: up to -15 critical chance, -12% attack speed, +15% cast time. |
| **Clumsy** | +1 | Fumbles now and then while moving: a short stumble that interrupts what you were casting. |
| **Slow Reflexes** | +2 | -12% attack speed, +20% stun and slow duration; cannot dash. |
| **Open Guard** | +1 | Cannot block. |
| **Pacifist** | +5 | Cannot attack with weapons. |
| **Magic-Dead** | +6 | Cannot cast abilities. |
| **Tongue-Tied** | +2 | While casting, the words tangle now and then: the cast is lost, +15% cast time for a moment. |

**Sight**

| Trait | Points | What it does |
|---|---|---|
| **Short-Sighted** | +1 | The picture is a little hazy; -3 critical chance. |
| **Poor Eyesight** | +2 | Hazy, narrowed sight; -8 critical chance, -10% ability range. |
| **Nearly Blind** | +5 | A thick haze and tunnel vision; -20 critical chance, -25% ability range. |
| **One-Eyed (Left)** | +3 | The left eye is blind: the left half of the screen is dark; -8 critical chance. |
| **One-Eyed (Right)** | +3 | The right eye is blind: the right half of the screen is dark; -8 critical chance. |
| **Tunnel Vision** | +2 | The edges of the screen are closed in. |
| **Night Blind** | +2 | In the dark the sight dims and narrows, the darker the more. |
| **Light-Sensitive** | +1 | In daylight the picture is washed out, the brighter the more. |
| **Migraines** | +2 | Now and then a migraine: hazy tunnel vision, +25% cast time, -10 critical chance, sanity loss; mild to severe. |

**Hearing**

| Trait | Points | What it does |
|---|---|---|
| **Hard Of Hearing** | +1 | Hears about a third of the sound. |
| **Deaf** | +3 | Hears nothing: no footsteps, no warning. |
| **Tinnitus** | +1 | Now and then the ears ring: little is heard, and it wears on the mind. |

**Hunger, thirst and sleep**

| Trait | Points | What it does |
|---|---|---|
| **Hearty Appetite** | +1 | Hunger drains 40% faster. |
| **Insatiable** | +3 | Hunger drains twice as fast. |
| **High Thirst** | +1 | Thirst drains 40% faster. |
| **Always Parched** | +3 | Thirst drains twice as fast. |
| **Sleepyhead** | +1 | Gets tired 50% faster. |
| **Insomniac** | +2 | Sleep restores rest half as fast. |
| **Narcoleptic** | +4 | Now and then falls asleep where it stands for a few seconds: eyes shut, helpless. |

**Body**

| Trait | Points | What it does |
|---|---|---|
| **Asthmatic** | +3 | -15% max stamina; sprinting brings on asthma attacks: stamina drains, -20% move speed. |
| **Anemic** | +2 | +30% bleeding, -12% max stamina, feels the cold. |
| **Slow Healer** | +2 | -40% injury healing, -25% healing received, -40% health regeneration. |
| **Weak Stomach** | +1 | Twice as likely to get food poisoning and dysentery (injury system). |
| **Prone To Illness** | +2 | +75% chance to catch illnesses, and they get worse more often (injury system). |
| **Weak** | +2 | -18% weapon damage, -25% carry weight. |
| **Cold-Natured** | +1 | The cold affects the body 40% more. |
| **Heat-Intolerant** | +1 | The heat affects the body 40% more. |
| **Aching Joints** | +2 | In the cold the joints seize: -15% move speed, -10% attack speed. |
| **Motion Sick** | +1 | Sprinting turns the stomach now and then: dizzy, blurred, stamina comes back slowly. |
| **Sun Sickness** | +3 | In daylight the skin burns: health drains slowly and the sight hazes, until back in the dark. |
| **Chronic Pain** | +3 | A pain that never leaves, some hours faint, some crippling: up to -18% move speed, -25% stamina regeneration, -8% attack speed, sanity loss. |

**Mind and fears**

| Trait | Points | What it does |
|---|---|---|
| **Fragile Mind** | +2 | Loses sanity 50% faster. |
| **Panic Attacks** | +3 | In a fight panic strikes now and then: stamina drains, -20% weapon damage, +25% cast time, tunnel vision, sanity loss. |
| **Faint-Hearted** | +4 | Badly hurt (below 35% health), may faint for a few seconds. |
| **Coward** | +3 | In a fight: -15% weapon and ability damage, sanity drains. |
| **Afraid Of The Dark** | +2 | At night: sanity drains, -8% weapon damage, the edges of the sight close in. |
| **Afraid Of Water** | +2 | While swimming: panic - stamina drains, sanity drains, -15% move speed, tunnel vision. |
| **Squeamish** | +2 | While bleeding, retches now and then: a short stun, -15% weapon damage, sanity loss (injury system). |
| **Absent-Minded** | +2 | +20% ability cooldowns. |

**Bad luck and small annoyances**

| Trait | Points | What it does |
|---|---|---|
| **Jinxed** | +2 | In a fight, every 20 seconds a coin flip you nearly always lose lowers a random stat for a while. |
| **Conspicuous** | +1 | +50% threat: enemies pick you first. |
| **Daydreamer** | +1 | Standing still, drifts off now and then: out for a couple of seconds. |
| **Allergies** | +1 | In daylight a sneezing fit now and then: a short stun that interrupts casting, -10 critical chance for a moment. |
| **Hiccups** | +1 | Now and then a bout of hiccups: interrupts what you were casting, +20% cast time while it lasts. |

**Only one of each group**: Limp / Bad Leg / Lame / Missing Foot / One-Legged · Overweight / Obese / Underweight ·
Missing Hand / One-Armed · Short-Sighted / Poor Eyesight / Nearly Blind · the two blind eyes · Hard Of Hearing / Deaf ·
Hearty Appetite / Insatiable · High Thirst / Always Parched · Sleepyhead / Insomniac.

**The parts they are made of** (trait behaviours under *Disability* in the Behaviours dropdown; mix them into your
own traits, on top of ordinary modifiers and combat stats):

| Behaviour | What it does |
|---|---|
| **Cannot Do** | Blocks actions for good: sprint, jump, dash, roll, crouch, weapon attacks, two-handed weapons, main or off hand, block, cast abilities, use items - with the reason told to the player. Works with the injury system on or off |
| **Impaired Sight** | What the player sees: an eye's half of the screen dark (*Eye* Left / Right; Center = both), *Haze*, *Tunnel*, *Sway*; *Always*, *In The Dark* (the darker the night the more) or *In Daylight* |
| **Impaired Hearing** | The game's volume for that player: 0.35 = hard of hearing, 0 = deaf |
| **Needs Drain Faster** | Hunger, thirst and tiredness multipliers (1.4 = 40% faster) |
| **Missing Limb** | A body part that is not there (*Part*: `LeftHand`, `LeftForearm`, `LeftUpperArm`, `LeftFoot`, `LeftShin`, `LeftThigh`, or the right ones; *On The Off Hand Side* takes it on the side that does not hold the weapon, in a right-handed game or a left-handed one). **With the injury system** the limb is really missing: hidden on the model, its hand unusable, hopping or crawling, shown in the Health tab - no wound, nothing to reattach, and it stays missing after a full heal or a respawn. **Without it**, the actions under *Without Injury System* are blocked instead |
| **Episodes (comes and goes)** | The variable ones, below |

**Disabilities that come and go.** An *Episodes* behaviour starts an episode now and then, **at random** - they
cannot be timed - while what brings them on holds (*Brought On By*: anytime, in combat, sprinting, moving, standing
still, in the air, badly hurt, out of breath, casting, swimming, at night, in daylight, tired, hungry or thirsty,
bleeding, cold):

* **Timing**: *Average Minutes Between*, a *Minimum Gap* of peace after one, a *Duration* between two values and a
  **Severity** between two values - both **picked again for every episode**, so one migraine is mild and short, the
  next crippling. With *Average Minutes Between* 0 it never leaves and only changes strength: Chronic Pain and Tremor
  are re-rolled every few minutes between nothing and their worst (good hours and bad ones).
* **While it lasts**, scaled by the severity: a status (*"Mild Migraine"*, *"Migraine"*, *"Severe Migraine"*) with
  stats and health / stamina / sanity per second, what it does to the sight and to the hearing.
* **When it starts**: a stun (a stumble), or *Out For All Of It* (a faint, a sleep attack: the eyes close),
  *Interrupts Casting*, and a message; another when it ends.
* *Ends When It Stops*: the episode ends with what brought it on (Sun Sickness ends in the shade).

Ready-made: Trick Knee, Two Left Feet, Vertigo, Tremor, Clumsy, Tongue-Tied, Migraines, Tinnitus, Narcoleptic,
Asthmatic, Aching Joints, Motion Sick, Sun Sickness, Chronic Pain, Panic Attacks, Faint-Hearted, Coward, Afraid Of
The Dark, Afraid Of Water, Squeamish, Daydreamer, Allergies, Hiccups. (*Jinxed* uses the Born Gambler behaviour
with luck turned down to 10%.)

**Daylight and night** come from the Day Night Cycle (or the Weather System's clock, or a Day Night Manager);
without any, the traits that need them do nothing. *In daylight* also needs **open sky**: under a roof, in a cave or
a dungeon the sun does not reach the character, whatever the hour.

**Multiplayer**: the server decides the episodes; clients learn of one - and how bad it is - from the name of its
status, and draw the sight and hearing from it. What a trait blocks is known on every machine, so movement
prediction agrees with the server. Sight and hearing are only ever changed for the player who controls the
character (`DisabilityEffects`, added to the player by the traits that need it; put it on the player yourself to
scale the screen effects or to leave the volume alone).

**Scripts**: `entity.SetActionBlock(key, BodyAction.Sprint, "Cursed")` / `ClearActionBlock(key)` block actions from
anything (a curse, a quest); `entity.AllowsAction(action)` and `entity.WhyNotAllowed(action)` answer for injuries and
blocks together. `injuries.RemovePartForGood(part)` / `GiveBackPart(part)` take a limb away without a wound.
`DisabilityEffects.For(player).SetSight(key, eye, visionEffect, strength)` / `SetHearing(key, volume)`.

### Your own scripts on a trait (Attach Script)

A trait can do more than change stats. The built-in behaviours (Double Jump, Wall Climb, Glide, Life Steal...) are
code that runs while the trait is owned. For your own mechanics, pick the **Custom ▸ Attach Script** behaviour: it puts
one of your scripts on the character while the trait is owned and takes it away when the trait is removed.

| Mode | What it does |
|---|---|
| **Add Script** | Adds the script (drag the `.cs` file onto the field, or pick it from the list) as a component, then removes it. If the character already has it, it is turned on and off instead (*Use Existing If Present*) |
| **Spawn Prefab** | Spawns a prefab under the character, so its scripts keep the values you set on the prefab, then destroys it |
| **Enable Existing** | Turns on a script the character already carries (left disabled on the prefab), and puts it back as it was afterwards |

*Attach To* puts it on a child (a bone such as `Hand_R`) instead of the root. If two sources ask for the same script,
they share one copy, which goes away when the last one ends.

The same behaviour works on items: *Equipment Effects ▸ Traits ▸ Passive Behaviour ▸ Custom ▸ Attach Script* attaches
the script while the item is worn. You can also give an armor piece a trait (*Inherent Traits*).

Your script can implement `ITraitAttachedScript` to be told when it is attached (with the `TraitContext`: status,
movement, abilities, input...), when its strength changes (armor set enhancements) and when it is about to be removed:

```csharp
public class RocketBoots : MonoBehaviour, ITraitAttachedScript
{
    private TraitContext ctx;
    private float strength = 1f;

    public void OnTraitAttached(TraitContext context, float s) { ctx = context; strength = s; }
    public void OnTraitStrengthChanged(float s) => strength = s;
    public void OnTraitDetached() { /* undo anything you changed */ }

    private void Update()
    {
        if (ctx != null && ctx.JumpPressed && !ctx.IsGrounded)
            ctx.Movement.VerticalVelocity = 12f * strength;
    }
}
```

In multiplayer the script is attached wherever the trait runs. If it only makes sense for the owner, check ownership
inside the script.

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
