# Rune Magic 02 — Soul resources: Soul Rune Capacity, Soul Integrity and recovery

A rune user (Mage) has two soul resources besides the runes themselves:

| Resource | What it limits |
|---|---|
| **Soul Rune Capacity** | How many runes the soul can hold. Each rune occupies its slot cost. |
| **Soul Integrity** | The soul's stability. Synthesis and casting strain it. A weak soul synthesizes and casts worse, then not at all. |

Both are separate from Physical Health, Mana, Stamina and Rune Energy (design spec 20). No rule reads or changes another resource. Every valid mix can happen: full health with a critically damaged soul, powerful runes with no free slot, and so on.

All numbers are data: *Rune Magic Database ▸ Data ▸ Balance*.

## Setup

1. Select the player prefab (or the player in the scene), then *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune Magic to Selected Player*. This adds:
   - **Rune Soul**, the soul and its rules;
   - **Soul Integrity Manager**, the status the HUD shows as the **Soul** gauge;
   - **Meditation Controller**, the Meditate input: the `Meditate` action of your Input Actions asset (see *Input* below);
   - **NetRuneMagic**, in a FishNet project with a NetworkObject on the player. *Network ▸ Make Networked* adds it as well.
2. All four are safe on every player. On a player who is not a rune user they stay inert: no gauge, no input, no saving. Abilities and essences work exactly as before.
3. **Without step 1**, a rune user still gets a Rune Soul when the game starts, where the rules run, with a Console warning (*Settings ▸ Add Missing Soul Components*). The soul is then kept and its rules run, but the HUD gauge, the meditation input and network replication need the components on the prefab.

## Soul Rune Capacity (design spec 4)

**Capacity** = the capacity of the character's progression stage + lasting bonuses + temporary bonuses.

| Stage (*Balance ▸ Capacity Stages*) | From level | Slots |
|---|---|---|
| Beginning | 1 | 7 |
| Intermediate | 20 | 14 |
| Advanced | 40 | 21 |
| Endgame | 60 | 28 |

The level comes from the player's Experience Manager. The stage levels are placeholders: the spec names only the stages.

**Bonuses:**

| Kind | Examples | API | Kept |
|---|---|---|---|
| Lasting, per source | Magical breakthroughs, rewards | `RuneSoul.SetCapacityBonus("breakthrough:x", 2)` | Saved |
| Temporary, per key | Equipment, passive abilities | `RuneSoul.SetCapacityModifier(key, slots)` | While it lasts |

A **Soul Ritual Site** can grant a breakthrough (see below).

**What uses slots:** each rune held uses its *Slot Cost*. Runes are absorbed in the next phase. This phase provides the rules:
- `RuneSoul.CanFit(slots)` tells whether runes of that cost fit;
- `FreeSlots` gives the slots left;
- when a bonus ends, the soul can be **overfull**: nothing more fits until slots are freed, and nothing already held is lost.

## Soul Integrity (design spec 14)

**Maximum:** *Balance ▸ Max Soul Integrity* (100), plus lasting bonuses (`SetMaxIntegrityBonus`) and temporary ones (`SetMaxIntegrityModifier`). A new rune user starts full.

### Conditions

| Soul Integrity | Condition | Casting debuff | Built-in effects |
|---|---|---|---|
| 100-80 % | Stable | — | None |
| 79-60 % | Strained | Mild Strain | Synthesis stability ×0.95, cast time ×1.1 |
| 59-40 % | Damaged | Moderate Strain | +10 % synthesis failure chance; spells weaker (×0.85) and costlier (×1.15) |
| 39-20 % | Critical | Severe Strain | +25 % failure chance, at most 3 runes per synthesis, effects of complexity above 0.7 unavailable, 15 % spell instability, soul damage ×1.25 |
| 19-1 % | Near Collapse | Critical Strain | Synthesis disabled, only rank-1 spells, heavy casting penalties, soul damage ×1.5 |
| 0 % | Soul Collapse | Soul Collapse | No synthesis, no casting |

Each condition is a row of *Balance ▸ Soul Conditions*. Its modifiers are what synthesis (Phase 5) and casting (Phase 7) read:
- `RuneSoul.Restrictions` gives them;
- `AllowsSynthesis(runes, complexity, out reason)` and `AllowsCasting(rank, out reason)` check them.

### Soul damage

Other systems call `RuneSoul.Damage(amount, kind)`. The caller scales the amount with complexity and risk. The **kind** is a row of *Balance ▸ Soul Damage Kinds*: an id, a multiplier, and a **Deep Share**.

| Kind (id) | Source (design spec 14.1) | Deep share |
|---|---|---|
| `synthesis` | Any rune synthesis | 0 |
| `synthesis_failure` | A failed combination | 10 % |
| `incompatible_absorption` | Absorbing a rune the soul rejects | 15 % |
| `overuse` | Using runes beyond safe limits | 10 % |
| `spell_cast` | Casting a powerful effect (per spell, never automatic) | 0 |
| `spell_failure` | A cast that failed | 5 % |
| `spelling_mistake` | A wrong key in the Spelling System (per spell) | 0 |
| `concept_overreach` | Concepts beyond the caster's understanding | 25 % |
| `primordial` | Using a Primordial rune | 60 % |
| `experiment` | Unstable or dangerous experiments | 30 % |

How damage is computed:
- The current condition's *Soul Damage Multiplier* scales damage (a weak soul is hurt more), unless the kind turns that off.
- The Soul Integrity Manager's *Decrement Factor* also applies.
- An unknown or empty kind is ordinary damage.

**Deep damage.** The deep share of a damage *scars* the soul:
- It lowers the ceiling ordinary recovery can reach: rest and meditation stop at *maximum − deep damage*.
- Only **specialized healing** removes it: soul items that heal deep damage, healing rituals, and sanctuaries that heal it.
- This is the spec's "powerful soul damage may require specialized healing": ordinary synthesis damage heals with meditation, while Primordial damage needs rare materials or rituals.
- Deep damage never exceeds *Soul Recovery ▸ Max Deep Damage Fraction* of the maximum (60 %).

### Soul Collapse (decision D8)

At 0 the soul collapses (*Balance ▸ Soul Collapse*):
- the character is incapacitated for *Incapacitate Seconds* (8);
- meditation and rituals end;
- casting and synthesis stay **locked until Soul Integrity is back to *Lock Until Percent*** (20 %), whatever the condition says;
- the collapse adds *Deep Damage Percent* (5 %) of deep damage;
- *Lose Pending Energy* is applied by the energy system (next phase), through the `RuneSoul.Collapsed` event.

## Recovery (design spec 15)

Soul Integrity is a long-term resource:
- it does **not** recover by itself (*Passive Per Minute* is 0, but you can set a trickle);
- it does not return to full after a fight;
- respawning does not restore it.

Recovery needs time, resources or specific actions.

| Method | How | Built-in rate (*Balance ▸ Soul Recovery*) |
|---|---|---|
| **Rest** | Sleeping (Sleep input, beds, benches; the existing sleep system) | 3/min × the sleep quality's Recovery Multiplier. Gradual. |
| **Meditation** | The `Meditate` input action, in place | 10/min once settled (3 s). The efficient method. |
| **Soul Healing Items** | *Create ▸ SimpleMovements ▸ Rune Magic ▸ Soul Restorative* | Per item: amount, % of max, over time, deep damage healed |
| **Healing Rituals** | A **Soul Ritual Site** at a magical location | Per site: materials, duration, deep damage healed, restore, breakthrough |
| **Soul Sanctuaries** | A **Soul Sanctuary** zone | Rest and meditation ×2; +1/min just for being there out of combat; optional deep healing. The hub world's city can count too (*City Multiplier*). |
| **Specialized Abilities** | A **Soul Attunement** component on the player (or under it), or `RuneSoul.SetRecoveryModifier(key, multiplier, perMinute)` | Per source |
| **Rare Materials** | Ordinary items, used as ritual materials and as ingredients of soul restoratives (any crafting or alchemy recipe) | — |

**Combining rates:**

> ((passive + rest + meditation) × best place multiplier + sanctuary trickle + flat bonuses) × modifiers × (1 + Increment Factor %)

Equipment and effects that change the Soul Integrity Manager's Increment Factor therefore speed up all soul recovery.

### Meditation

**Refused:**
- in combat (hit or hitting within *Combat Window Seconds*);
- while asleep, downed or incapacitated;
- when there is nothing ordinary to recover. With only deep damage left, the player is told it needs specialized healing.

**Broken by:**
- moving more than *Channel Move Tolerance* (0.4 m);
- taking damage;
- crowd control;
- attacking;
- falling asleep or dying.

Press Meditate again to stop. While meditating, the screen shows the soul, its condition and the recovery per minute.

*Meditation Locks Movement* turns it into a sleep-like state, where only the Meditate input ends it.

### Soul Ritual Site

Put it on an altar with a collider, then set:
- the materials;
- *Ritual Seconds*;
- *Deep Heal* (or *Heal All Deep Damage*), *Restore Amount* / *Restore Percent*;
- a cooldown per player and the total uses;
- optionally *Capacity Bonus Slots* with a *Breakthrough Id*: lasting capacity, granted once per player per id.

**Using it:**
1. Rune users see "[E] Perform Ritual". Others don't see it.
2. The player must stay within range and unhurt for the duration.
3. Materials are consumed only when the ritual completes.
4. The Meditate input breaks it off.

### Soul Restorative items

A consumable with soul fields:
- *Restore Amount*;
- *Restore Percent*;
- *Over Seconds*;
- *Deep Heal Amount*.

Its ordinary Consumable Effects still apply to anyone, but only rune users' souls are affected. Its tooltip lists the soul effects.

## Input

Rune Magic reads its inputs only through the Input System, from actions of the player's Input Actions asset (the
generated PlayerInput). **No key is fixed in code.**

| Action (default name) | Does |
|---|---|
| `Meditate` | Starts meditating; again stops meditation or a ritual |
| `RuneSoul` | Opens and closes the Rune Soul window |
| `Synthesis` | Opens and closes the Rune Synthesis window (added in Phase 6; see [06](06-Synthesis-UI.md)) |
| `Spellbook` | Opens and closes the Spellbook (added in Phase 7; see [07](07-Spells-and-Slots.md)). Spells are cast with the ordinary `Ability1…` actions, never with this one. |
| `Grimoire` | Opens and closes the Grimoire window (see [10](10-Grimoire.md)): the book where runes of the soul are imprinted and prints absorbed. |
| `RuneMagicDebug` | Opens and closes the debug panel (editor and development builds) |

- The names are data: *Rune Magic Database ▸ Settings ▸ Input actions*. A component can also be given an Input Action reference (Meditation Controller ▸ Meditate Action).
- *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune Magic Input Actions* adds the missing actions to the Player map of your Input Actions asset **without a key**. Bind them in the Input Actions editor (double-click the asset); players rebind them in Key Bindings like every other action.
- The Project Validator (Game Setup) reports Rune Magic actions that are missing (with an Add fix) or have no key.
- An action that is missing simply does nothing (one Console warning says which).

## Saving (decision D6)

Where the rules run (offline, the server), each rune user's soul is kept:
- in the world's store (`WorldStore`, section `runeMagic`), keyed by the player's lasting key (`PlayerKeys`), like money and housing;
- written atomically, with a backup;
- separately for the offline and online worlds.

**When the saved soul comes back:**
- as soon as the player's key is known (in a network session, once the server accepted the character);
- until then the soul runs with a provisional state, which the saved one replaces;
- a player new to the world starts full.

**What is not saved:**
- clients save nothing;
- players who are not rune users get no record;
- meditation, rituals in progress, timed item effects and temporary modifiers are not saved.

The saved data is `RuneSoulState` (`RuneMagic/Core/State`), plain JSON. Later phases add the runes, concept knowledge and spells to it.

## Network (FishNet)

- The server runs every soul rule.
- **NetRuneMagic** sends the soul's **view** to the player's owner only, at most 4 times a second while it changes. Other players never see it (decision D18).
- The owner's client shows the view: the Soul gauge, the meditation screen and the debug panel.
- The owner sends its requests (meditate / stop) with a sequence number. A token bucket limits them (4/s, burst 6), and the server checks everything again.
- Rituals start through the normal interaction system, which runs on the server.

## Debug and inspection

**Debug panel.** In development builds and the editor, the `RuneMagicDebug` input action (or *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Toggle Debug Panel (Play Mode)*) shows the local player's soul:
- integrity, deep damage and condition;
- what synthesis and casting are allowed;
- capacity and its next stage;
- recovery and its sources;
- what the soul is focused on, and whether it is saved.

Where the rules run, it also has buttons:
- damage by kind;
- collapse;
- restore, heal deep, full;
- meditate;
- capacity ±1;
- recovery ×10;
- copy the saved state as JSON.

*Settings ▸ Debug Panel* turns it off.

**Inspector.** In play mode, the Rune Soul inspector shows the same and has the same tools.

## For your own code

```csharp
RuneSoul soul = RuneSoul.For(player);           // any component on the player
if (soul != null && soul.IsRuneUser)
{
    SoulRestrictions r = soul.Restrictions;     // what the condition allows
    if (r.AllowsSynthesis(runeCount, effect.complexity, out string why)) { /* ... */ }
    soul.Damage(12f, "synthesis_failure");      // where the rules run
    soul.SetRecoveryModifier(this, 1.25f);      // a passive skill
    soul.Collapsed += s => { /* consequences */ };
}
```

## Tests

*Window ▸ General ▸ Test Runner ▸ Edit Mode*:
- **`RuneMagic.Tests.RuneSoulRulesTests`** (pure; also run outside Unity) covers:
  - the spec's condition thresholds and their restrictions;
  - damage multipliers and deep damage, with its invariants under 2,000 random operations;
  - Soul Collapse and its lock;
  - maximum bonuses;
  - every recovery source and how sources combine;
  - capacity stages and bonuses;
  - the independence of the soul from other resources;
  - the saved state round trip;
  - older database upgrade;
  - the request guard.
- **`RuneMagic.Tests.RuneSoulRuntimeTests`** (category `RequiresEngine`) covers:
  - inert souls on non rune users;
  - the restorative item;
  - sanctuaries;
  - ritual sites.
