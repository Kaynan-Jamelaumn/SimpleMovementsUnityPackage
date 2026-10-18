# 12 — Survival, Death & Respawn

**Scripts:** `Essentials/Status/*` (`StatusManager`, `NeedStatusManager`, `HungerManager`, `ThirstManager`,
`SleepManager`, `SanityManager`, `BodyHeatManager`, `StatusDamage`), `Essentials/Status/Conditions/*`
(`StatusConsequence`, `SleepSpot`, `SleepController`, `SanityAura`, `SanityOnHit`, `SanityScreenEffect`,
`TemperatureProfile`, the Sanity / Revive / Body Heat ability effects), `Essentials/Combat/Downed/*`
(`Unconsciousness`, `UnconsciousScreen`, `DeathPenalty`), `Essentials/Ability/Core/AbsorptionMilestones.cs`.

Every consequence below (tiredness, low sanity, cold, heat...) is a **Status Consequence**, edited the same way
everywhere: a status icon name, combat stats and character stats while it lasts, health / stamina / sanity per
second, faster or slower hunger and thirst, and a chance per minute to faint (a short stun). Nothing is hard-coded:
change, remove or add stages in the inspectors (right-click a component ▸ *Reset ... To Defaults* brings the defaults
back).

---

## 1. The numbers: tick rates, values and factors

| Field | What it is |
|---|---|
| **Tick Rate** | Seconds between two **regeneration** ticks (health, stamina, mana gaining back). |
| **Consuming Tick Rate** | Seconds between two **consumption** ticks of a status that drains by itself (hunger, thirst). |
| **Increment Value** | **How much** one regeneration tick gives. |
| **Decrement Value** | **How much** one consumption tick removes. |
| **Increment Factor** | A **percent** change of every gain (regeneration, food, potions, heals): +50 = 50% more, −50 = half, 0 = unchanged. |
| **Decrement Factor** | A **percent** change of every loss (consumption ticks, damage, costs): +50 = 50% more, −50 = half, 0 = unchanged. |

*Decrement Value vs Decrement Factor*: the value is the **amount** per tick; the factor **scales** that amount (and
every other loss of the status) by a percentage. Traits and equipment usually change the factor ("+20% hunger
drain"), you set the value. The factors are percents, not multipliers: a factor of 1 is +1%.

*Tick Rate vs Consuming Tick Rate*: two separate clocks — one for gaining, one for losing. Hunger and thirst only use
the consuming one (they never regenerate by themselves).

**Example (hunger):** Consuming Tick Rate 6, Decrement Value 1, Decrement Factor 0 → 1 point every 6 s = 10 per
minute; a bar of 100 lasts 10 minutes. Decrement Factor 50 → 1.5 per tick = 15 per minute. On top, other systems set
**drain multipliers** (cold ×1.3, sleeping in a bed ×0.5...), multiplied together (`StatusManager.RateMultiplier`).

### What was wrong with hunger (fixed)

The old hunger started a new consumption coroutine every frame and used the *regenerating* flag as an on/off switch,
so the flag flickered and hunger drained about once every two frames (30+ points per second) instead of once per
tick. Now one timer drains exactly *Decrement Value* every *Consuming Tick Rate* seconds; *Is Regenerating* stays off
(needs never regenerate by themselves) and *Is Consuming* is on while it drains. Thirst had the same problem and the
same fix. The starvation / dehydration damage has its own interval (*Starving Hp Penalty Tick Rate*, before it waited
"Starving Hp Penalty" seconds). Needs do not drain, and their penalty pauses, while the character is dead or
unconscious.

Also fixed in every status: timed factor effects (cold hunger, sleep penalties, heal / damage factor buffs) used to
**overwrite** the factor and were never undone when refreshed or removed, which left stamina or hunger with a wrong
factor forever ("stats regenerating unexpectedly"). They now add their amount and take exactly that away when they
end, are replaced or removed.

---

## 2. Rest and sleeping (`SleepManager`)

**100 = fully rested, 0 = exhausted** (it used to be "sleepiness", where 100 was the worst and the game started at
100 — so the player collapsed into a forced nap at once and the value kept going down).

| Setting | Default | Meaning |
|---|---|---|
| Awake Loss Per Minute | 1.5 | Rest lost per minute while awake (Decrement Factor changes it) |
| Sleep Recovery Per Minute | 25 | Rest recovered per minute asleep, × the sleep quality, Increment Factor and the **Rest Recovery** stat |
| Stages | Tired < 50%, Very Tired < 25%, Exhausted < 10% | Debuffs as rest drops (slower, weaker stamina regen, slower casting, sanity loss, fainting) |
| Collapse At Percent | 0 | Empty: the character collapses asleep where it stands (Collapsed quality: slow recovery, Groggy on waking) |
| Cannot Sleep Above Percent | 95 | "You are not tired" |
| No Sleep After Damage Seconds | 8 | "You are in danger" |
| Wake When Rested / Wake On Damage | on / on | |

**Where you sleep decides the quality** (each one editable on the Sleep Manager):

| Kind | Recovery | While asleep | On waking |
|---|---|---|---|
| Bed | ×1 | +12 health/min, +4 sanity/min, hunger and thirst ×0.5 | **Well Rested** (+10% stamina regen, +3% move speed, 5 min) after a full rest |
| Bedroll | ×0.75 | +1 sanity/min | Slept Rough (−5% stamina regen) |
| Bench | ×0.6 | | Sore (−8% stamina regen) |
| Ground | ×0.45 | −1 sanity/min | **Stiff Back** (−10% stamina regen, −5% move speed, 3 min) |
| Collapsed | ×0.3 | −3 sanity/min | **Groggy** (−15% move, −20% stamina regen, slower casting, 4 min) |

How to sleep:

* **Beds, benches, bedrolls:** add a **Sleep Spot** to the object (*Add Component ▸ SimpleMovements ▸ Sleep Spot*),
  set its *Kind* (or *Custom* with its own quality), optionally a *Sleep Point* where the sleeper lies. The player
  uses it with **Interact** ("[E] Sleep  Bed") when it is free (*Capacity*); it is occupied until they wake up.
* **Anywhere:** the **Sleep** input of the **Sleep Controller** (default Z / D-pad down; or an action named Sleep or
  Rest) sleeps on the ground; press it again to wake up. While asleep the screen dims and shows the rest.
* **Potions and food:** a consumable with a *Sleep* effect restores rest (positive amounts), *Sleep Regeneration*
  restores it over time. Coffee-like items: a small positive amount.

While asleep the character cannot move or act (it is not a stun for crowd control); damage wakes it.

---

## 3. Sanity (`SanityManager`)

**What lowers it** (every loss reduced by the **Sanity Resistance** stat — 100% = immune — and Decrement Factor):

| Source | How to set it up |
|---|---|
| Mob presence, haunted places, darkness | **Sanity Aura** on the mob / an object: *Per Second* (negative), *Radius*, falloff, line of sight; stops when the mob dies |
| Mob attacks | **Sanity On Hit** on the mob: sanity per hit + a percent of the damage |
| Abilities / skills (yours or a mob's) | Ability effect **Support ▸ Sanity** with a negative amount (a scream, a curse) |
| Items used | Consumables with a negative *Sanity* effect |
| Armour / items equipped | Equipment with a negative *Sanity Regeneration* (a cursed amulet drains it every second) |
| Exhaustion, freezing, sleeping on the ground | Their stages / qualities (sanity per second / minute) |
| Your scripts | `SanityManager.ChangeSanity(-amount, source)`, `SetSource(key, perSecond)` / `ClearSource(key)` |

**What restores it:** consumables and potions (*Sanity*, *Sanity Regeneration*), the **Support ▸ Sanity** ability
effect with a positive amount (a prayer, a song — on skills, traits, racial abilities, items), positive auras (a
campfire or shrine with a positive *Per Second*), sleeping in a bed, equipment with a positive *Sanity Regeneration*,
and a slow passive recovery (*Passive Recovery Per Second*) up to *Passive Recovery Up To Percent* (70%) while nothing
drains it.

**Consequences** (stages, all editable):

| Stage | Below | Effects (default) |
|---|---|---|
| Uneasy | 70% | −2% critical chance; slight screen darkening |
| Paranoid | 40% | −5% crit, −15% stamina regen, +10% cast time |
| Hysteric | 20% | −10% move speed, +20% cast time, +10% damage taken, −10% weapon damage, hunger ×1.2, panic attacks (30%/min, 1.5 s stun) |
| Broken Mind | 5% | −20% move, +30% cast time, +25% damage taken, −20% weapon damage, −0.5 health/s, panic attacks (50%/min, 2 s) |

The **Sanity Screen Effect** shows it to the local player (darkening, pulsing edges; optional whispers) — purely
visual. `onStageChanged` lets you add your own (hallucinations, music).

---

## 4. Body temperature (`BodyHeatManager`)

The body moves toward the temperature the player feels (the weather sets it through Player Weather Effects, or your
scripts with `SetAirTemperature(°C)` / `SetEnvironmentalTemperature(points)`). The scale is 0–100 points, about 1.6
points per °C, 50 = 18 °C. Without weather (City, dungeons) the body rests at its own ideal.

* **Ideal temperature and comfortable range per race / class:** *Character Archetype ▸ Survival ▸ Temperature*
  (*Custom Temperature* on): *Ideal Celsius*, *Tolerance Celsius* (comfortable = ideal ± tolerance), *Adapt Speed*,
  and optionally *Custom Effects* (the race's own cold / heat effects). Races are used first, then classes. Without
  one, the Body Heat Manager's defaults (18 °C ± 8 °C).
* **Resistances and immunity:** the **Heat Resistance** and **Cold Resistance** combat stats — set them in the
  archetype's *Base Combat Stats*, a trait's combat stats, equipment, or a buff (the **Support ▸ Body Heat** ability
  effect can grant temporary protection). 50% = the body follows the cold / heat half as far, **100% = immune**: the
  body stays at its ideal on that side and no effect of that side applies. Negative = a weakness.
* **Effects outside the range** (editable, by °C beyond the comfortable edge):

| Effect | Side | Starts | Default |
|---|---|---|---|
| Chilly | cold | 0 °C beyond | −5% move, −10% stamina regen, hunger ×1.3 |
| Freezing | cold | 8 °C beyond | −15% move, −25% stamina regen, −10% attack speed, hunger ×1.6, −0.5 health/s, shivering |
| Hypothermia | cold | 16 °C beyond | −30% move, −40% stamina regen, −20% attack speed, hunger ×2, −2 health/s |
| Warm | hot | 0 °C beyond | +10% stamina cost, thirst ×1.4 |
| Overheated | hot | 8 °C beyond | −20% stamina regen, +20% stamina cost, thirst ×1.8, −0.5 health/s |
| Heat Exhaustion | hot | 16 °C beyond | −20% move, −35% stamina regen, thirst ×2.5, −2 health/s, fainting |

**Weather without double penalties:** *Player Weather Effects ▸ Temperature Handling* (default *Skip Temperature
Rules*): for players with a Body Heat Manager the weather's pure temperature conditions (Chilled, Heatstroke) are left
to the body temperature, and Ice / Fire conditions (Frostbite) are scaled by Cold / Heat Resistance (immune = none).
Weather status rates on Body Heat are resisted too. See [Terrain 21](../Terrain/21-Weather-Players-Day-Night.md).

---

## 5. Unconsciousness (`Unconsciousness`)

| Rule | Default | Meaning |
|---|---|---|
| Death Threshold | 15 | Damage below 0 health survived. At 10 health a 20-damage hit (−10) knocks out; a 30-damage hit (−20) kills instantly |
| Death Threshold % Of Max Health | 0 | Adds a percent of max health (25 = a 200-health character survives 50 more) |
| Unconscious Seconds | 30 | Time to be revived before dying (*Bleed Out* off = waits forever) |
| Damage While Down Multiplier | 1 | Hits on the unconscious count toward the threshold (0 = cannot be finished off) |
| Revive Health Fraction | 0.25 | Health after a revive |
| Revive By Interacting / Hold Seconds | on / 3 s | Another player holds Interact on the unconscious one |
| Healing Revives | off | Heals first pay back the damage below 0, then wake the character |
| Allow Give Up / Hold Seconds | on / 1.5 s | Give Up button on the screen, or hold the Give Up input (X / B) |

The rules are global — **Combat Settings ▸ Downed Rules** — or per character (*Use Global Rules* off). Traits, races,
classes, equipment and buffs add to them with the **Death Threshold** (points) and **Unconscious Time** (seconds)
combat stats. While unconscious the character cannot move or act, its health does not regenerate, and the local
player sees the countdown, how far below 0 they are and where they die. Revive with the **Support ▸ Revive** ability
effect (optionally resurrecting the dead too), by interacting, or `Unconsciousness.Revive(fraction, reviver)`. The
Animator gets `IsUnconscious` (without that parameter the `IsDead` pose is shown, without the death trigger).

---

## 6. Death penalties and respawn (`DeathPenalty`)

Choose the **Mode**:

| Mode | What dying costs |
|---|---|
| None | Nothing |
| **Respawn Lose Items** | Items in the bag / hotbar (and optionally equipped) are lost, all or by *Chance Per Stack*; dropped where you died or destroyed; *Never Lose* list |
| **Character Reset** | The character's save is deleted (permadeath) and the game goes back to the start / creation scene (*Character Creation Scene*, else build index 0). Network sessions: hook *On Character Reset* |
| **Lose Experience** | *Experience Loss Percent* of the level's requirement (+ flat); *Can Lose Levels*: each lost level takes back its skill / attribute / stat points and undoes the stat points spent at it; never below *Minimum Level* |
| **Milestone Regression** | Back to the last **absorption milestone** (levels 10, 20, 35, 40, 45: level 57 → 45) and the **latest absorbed ability is lost** (*Abilities Lost*). *On Milestone Drop To Previous*: a character exactly on 45 drops to 40 |

**Absorption milestones** (one more absorbed ability per milestone level reached; the player cannot absorb more
until the next one): **Absorption Settings ▸ Level Milestones** (project-wide, off by default), a race or class
(*Character Archetype ▸ Survival ▸ Absorption Milestones*, classes first), or the player's Death Penalty (*Own
Milestones*). The level an ability was absorbed at is saved with it.

**Respawn:** offline, after *Respawn Delay* at *Respawn Point* (else where the player started), with *Spawn
Protection*. In network sessions the server's NetCombatStatus respawns the player. On every revive the **Status
Rules** refill the needs (at least: hunger 60%, thirst 60%, stamina and mana 100%, rest 60%, sanity 50%, oxygen
full, body temperature back to ideal) — before, hunger and thirst stayed at 0 after a death. A player without a
Death Penalty component gets these defaults too.

---

## 7. Setup checklist

1. **Player Setup Validator ▸ Add All Recommended** adds Hunger / Thirst / Sleep / Sanity / Body Heat / Oxygen,
   **Unconsciousness**, **Death Penalty** and **Sleep Controller** (optional: **Sanity Screen Effect**). Its *Survival,
   Death & Respawn* section shows the rules in use.
2. Hunger / thirst: set *Should Consume*, *Consuming Tick Rate* and *Decrement Value* (see §1).
3. Put **Sleep Spots** on beds and benches.
4. Give mobs **Sanity Aura** / **Sanity On Hit** where they should frighten.
5. Races / classes: *Survival ▸ Temperature* and Heat / Cold Resistance in *Base Combat Stats*.
6. Choose the **Death Penalty** mode; for Milestone Regression turn the milestones on (Absorption Settings, the
   archetype or the component).
7. Optional Animator parameters: `IsSleeping`, `IsUnconscious`.
