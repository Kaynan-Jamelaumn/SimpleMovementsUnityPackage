# Rune Magic 07 — Spells, shared ability slots and casting costs

A spell a Mage has synthesized is cast **with the same ability keys as every other ability**. There is no second set of slots, no second hotbar and no second casting system (directive 8): a spell is put on one of the player's existing ability keys, shows on the existing ability bar, is cast with that key's Input System action (`Ability1`, `Ability2`…), and runs through the ordinary ability pipeline: cast time, cooldown, aim, telegraph, effects, interruption, network copies. What Rune Magic adds is only what that pipeline knows nothing about: **which spell is on which key**, **Rune Energy drawn from the spell's runes**, **Soul Strain**, **misfires**, and the **debuffs of the soul's condition**.

```
Spellbook window ──request──▶ server: RuneSoul.AssignSpell ──▶ the key's AbilitySlot  (ability + modifiers + label)
                                                                    │ replicated like any ability's slot
                                                                    ▼
ability key pressed ─▶ PlayerAbilityController ─▶ AbilitySlot.Castable (RuneSpellCastable) ─▶ RuneSoul
        (cast time, cooldown, aim, effects: unchanged)        HasResources · OnCastStarted · OnCastReleased · OnCastInterrupted
```

## The Spellbook

Open it from the **Spellbook** button of the Rune Soul window, or with the Input Action named in *Rune Magic Database ▸ Settings ▸ Spellbook Action* (default `Spellbook`). **No key is fixed in code**: *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune Magic Input Actions* adds the action to your Input Actions asset without a key; bind it there (players rebind it in Key Bindings). Only rune users can open it. It is pointer UI.

- **Left:** every spell, with its rank, its form, the key it is on and *flawed* when it came out of a flawed synthesis.
- **Right:** the selected spell (purpose → effect, synthesis quality, power), its costs, and one row per **ability key** of the player (the key's name comes from the Input System, e.g. `[Q]`). Click a key to put the spell there.
- **Take off key** returns it to the book. **Dismiss spell** (two clicks) frees the runes that anchor it.

Assigning follows the rules of abilities (`SpellSlotRules`):

| You do | Result |
|---|---|
| Put a spell on an **empty key** | It is there. |
| Put a spell that is **already on a key** on a key that holds **another spell** | The two **swap**. |
| Put a spell **from the book** on a key that holds another spell | The new spell takes the key; the other goes back to the book (never lost). |
| Put a spell on a key that holds a **non-spell ability** | Refused (the reason says which ability). With *Spell Balance ▸ Replace Other Abilities* on, it replaces it (the ability is not given back). |
| Put a spell on something that is not an ability key (a trait's extra slot) | Refused. |
| Put a spell **on a second key** | It moves: a spell is only ever on one key. |

A spell that is **dismissed**, or whose runes are destroyed by a catastrophic synthesis, leaves its key by itself. The keys are saved with the soul (soul save version 5) and applied when the player loads; if a key does not exist yet when the save returns, it is retried until it does.

## From a spell to something cast

Every synthesized spell has a **form**: the executable shape of its desired effect (a projectile, a lobbed charge, a wall, a dash…). A form is data (`SpellFormDef`) naming an **ability preset** of the ability system (or a template asset a designer assigns on the database component) and the desired effects it serves. The spell's own numbers are applied on top as the slot's `AbilityModifierSet`, so the shared executable is never changed.

| Form | Ability preset | Serves (examples) |
|---|---|---|
| `bolt` Rune Bolt | Firebolt (Projectile) | pierce, penetrate, infect, drain, erode, corrode · fallback for Offensive effects |
| `blast` Rune Blast | Bomb (Lobbed) | explosion, detonate, overload, chain reaction, rupture, shatter |
| `cleave` Rune Cleave | Melee Cleave (Cone) | slash, rend, sever |
| `shockwave` Rune Shockwave | Shockwave (Ring) | crush, pulverize, repel, push |
| `beam` Rune Beam | Fire Breath (Beam) | incinerate, disintegrate |
| `vortex` Rune Vortex | Vortex (Pull to Centre) | implode, attract |
| `cage` Rune Cage | Cage | immobilize, confine, restrict, suspend, freeze, paralyze |
| `chain` Rune Chain | Hook (Pull) | bind, pull |
| `command` Rune Command | War Cry (Silence Nova) | silence, suppress, weaken, distract, disorient, slow · fallback for Control |
| `wall` Rune Wall | Stone Wall | barrier, seal, construct, forge, shape |
| `ward` Rune Ward | Guardian Barrier (Self) | shield, deflect, absorb, reinforce, reflect, nullify, fortify… · fallback for Defensive |
| `rush` Rune Rush | Charge | dash, propel, accelerate, launch (energy ×0.8) |
| `blink` Rune Blink | Blink | teleport, blink, warp, phase, reposition · fallback for Movement (energy ×0.8) |
| `drift` Rune Drift | Disengage (Retreat) | levitate, glide, decelerate, orbit (energy ×0.8) |
| `mending` Rune Mending | Heal Self | restore, cleanse, purify |
| `ritual` Rune Ritual | *(none: an empty self cast)* | fallback for Creation/Transformation and Utility/Exploration effects (energy ×0.6, strain ×0.5) |

**All 101 desired effects map to a form** (a test checks it, and a database whose effect has no form is invalid). Effects with no combat equivalent (reveal, grow, communicate…) use the `ritual` form: **it costs energy and strains the soul but does nothing in the game world** until you give it something to do. To do that, assign an `AbilityDefinition` asset to the form in *Rune Magic Database ▸ Spell Form Templates* (form id `ritual`, or any other form id to replace its preset). A template's mana, stamina and health costs are ignored: a spell costs Rune Energy.

The ability of a spell has the id `rune:<form>:<spell id>`. **Any machine rebuilds it from the id alone** (`AbilityDatabase.FallbackResolver`, chained after any resolver already installed), so the slot content the server replicates (ability id, modifiers, name) is all a client needs: no network format changed.

What the compiled spell (`SpellCompiler`) turns the synthesis result into, with the built-in numbers (all of them in *Balance ▸ Spells*, so tune them there):

| Spell stat | Becomes | Built-in |
|---|---|---|
| Power | damage and healing multiplier | `power / 100` |
| Power | control duration multiplier | `√(power/100)` (at least 0.5) |
| Complexity, control penalty | cast time multiplier | `(0.8 + 0.6 × complexity) × (1 + control penalty)`; an ability is never quicker than 0.3 s |
| Complexity | cooldown multiplier | `0.6 + 0.8 × complexity` |
| Rank | range / area multiplier | `+5%` / `+8%` per rank above 1, times the form's own |
| Stability | duration multiplier | `0.8 + 0.4 × stability/100` |
| Stability | misfire chance | `(1 − stability/100) × 0.5` |
| The form | `canMoveWhileCasting` | the caster moves at 50% speed while casting (`mending`, `ritual`) |

## What a cast costs (design spec 16.1, 16.2, 14.1)

- **Rune Energy**, drawn from **the runes that anchor the spell** (and only those): `0.02 × (max energy of a rank-N rune) × (0.5 + complexity) × the spell's cost multiplier × the form's × the condition's`, at least 1. A rank 3 spell of complexity 0.5 draws 500 of the 25,000 a rank 3 rune holds (the spec's worked example, section 16.3); a rank 1 spell of the same complexity draws 20 of 1000. The draw takes the same amount from each anchored rune until one runs dry (no rune is drained while another is full). **The ability's mana, stamina and health costs are 0.**
- The cast **cannot start** without that energy (the key is tinted like any ability without resources; the tooltip says why: *Not enough Rune Energy: … needs 80 and its runes hold 50*).
- An **interrupted** cast (stun, silence, damage that interrupts, cancelling, replacing the key) before its release **gives the energy back** to the same runes, capped at their maximum. Releasing it keeps the energy spent.
- **Soul Strain** (Soul Damage): a cast that **reaches its release** causes `4 × (1 + 1 per rank above 1) × (0.5 + complexity) × the form's strain` (4, 8, 12, 16, 20 at complexity 0.5: a powerful rank 3 spell causes the spec's 12 Soul Damage, and two in a row leave the soul at 76, Strained) of the Soul Damage Kind `spell_cast`. The kind's multiplier and deep share, and the soul's condition (Soul Damage Multiplier), apply as to any Soul Damage, and the tooltip shows what the soul will actually lose (*Soul Strain: none* when it is 0). Set the `spell_cast` kind's Multiplier to 0, or switch off *Strain On Success*, to make casting free of Soul Damage.
- A **misfire** (below) causes `1.5 ×` that amount of the kind `spell_failure`, keeps the energy it drew (*Misfire Keeps Energy*), and is told to the player. An interruption by an enemy causes no Soul Damage unless *Damage On Interrupt* is on.
- Nothing is paid or strained when the ability is only shown (the hub city's *visual only* casts): the cast hooks check it.

### Casting debuffs by Soul Integrity condition (spec 16.2, decision D10)

They are the soul's conditions' own numbers (*Balance ▸ Soul Conditions*), baked into the slot's modifiers (so the cast time and power you see are what you get), recomputed and re-applied when the condition changes:

| Condition | Casting debuff | Cast time | Power | Energy cost | Misfire chance added | Other |
|---|---|---|---|---|---|---|
| Stable | — | ×1 | ×1 | ×1 | — | |
| Strained | Mild Strain | ×1.1 | ×1 | ×1 | — | |
| Damaged | Moderate Strain | ×1.15 | ×0.85 | ×1.15 | — | |
| Critical | Severe Strain | ×1.25 | ×0.75 | ×1.25 | +15% | Soul Damage ×1.25 |
| Near Collapse | Critical Strain | ×1.5 | ×0.6 | ×1.5 | +30% | only rank 1 spells; Soul Damage ×1.5 |
| Collapse | Soul Collapse | — | — | — | — | **cannot cast**, nor while locked after a collapse |

### Misfires

A cast misfires with probability `(1 − stability/100) × 0.5` plus the condition's own. The **server rolls once, when the cast starts** (`SpellCasting.Roll`, deterministic from the cast's seed): a misfire cuts the cast off at a random point between 50% and 90% of its cast time. A stable spell in a stable soul never misfires.

## For developers

| Piece | Where | Does |
|---|---|---|
| `SpellFormDef`, `SpellBalance` | `Core/Data/SpellDefs.cs` | The data: forms and every number above. |
| `SpellCompiler` | `Core/Spells/` | Spell record → `CompiledSpell` (form, energy, strain, misfire, modifiers); the ability id; the tooltip lines. |
| `SpellCasting` | `Core/Spells/` | `Plan` (what a cast costs and does in a condition), `Draw`/`Refund`, `Roll`, `Success`/`Failure`/`End` (strain). Pure. |
| `SpellSlotRules` | `Core/Spells/` | Assign / unassign / swap / normalize the keys. Pure. |
| `RuneSpellAbilities` | `Runtime/` | Builds the ability of a spell from its form; installs the resolver. |
| `RuneSoul.Spellcasting` | `Runtime/` | Assigning, keeping the slots in line with the soul, the cast moments, `CanCastSpell`, `DescribeSpell`, `GetSpellSlots`. |
| `SpellbookWindow` | `UI/` | A thin renderer; it only sends requests. |
| `ICastable` | `Essentials/Ability/Core/ICastable.cs` | **Generic**: knows nothing about runes. What a slot can hold besides an ordinary ability's own costs. |
| Requests | `Networking/Core/RuneMagic/` | `AssignSpell` (10: text = spell id, amount = key) and `UnassignSpell` (11: amount = key); the server checks everything again. |

### The existing files this phase touches (also read decision D16)

Small, additive and inert when no slot holds a castable (every ordinary ability and every non-Mage):

| File | Change |
|---|---|
| `Essentials/Ability/Core/AbilitySlot.cs` | `[NonSerialized] public ICastable Castable;` and `Set()` clears it. |
| `Essentials/Ability/Player Ability/PlayerAbilityController.cs` | Overrides `HasResources`, `PayCost` and `OnCastReleased` (each calls `base` first, then the slot's castable if it has one) and forwards the existing `CastInterrupted` event. |
| `Essentials/UI/HUD/HudAbilityBar.cs` | `KeySource.Describe()` appends the castable's tooltip lines. |

`AbilityEnums.cs`, `AbilityAbsorption.cs`, `NetAbilityCaster.cs` and every file about essences are **not** touched: a rune spell travels as an ordinary ability id plus modifiers.

### Mutual exclusion and other classes

Nothing here runs for a character that is not a rune user: no soul listens to its slots, `GetSpellSlots` is empty, assigning is refused, and no slot of theirs ever holds a castable, so the three overrides above fall straight through to the ability system's own code. A Mage still cannot absorb essences (Phase 1).

## How to test

Offline, without Unity (what I ran): 50 tests in `SpellsCoreTests` — forms (every effect has one, presets exist, ids valid, extensions), the ability id, compiling (cost by complexity, rank and form; power, stability), the plan in every soul condition (debuffs, rank cap, collapse, lock), energy (drawn only from anchored runes, equal shares, nothing taken when it cannot be paid, refund to the same runes and never above the maximum), misfires (never / always / rate / reproducible / range), Soul Strain (default, none by kind, none by switch, failure, refund and interruption options, condition scaling), key assignment (empty, one key per spell, swap, send back to the book, unassign, normalize, save round trip, old saves), and the data upgrade.

In Unity's Test Runner, category **RequiresEngine**: `SpellsRuntimeTests` (a slot has no castable by default and loses it when replaced; every form builds an ability that costs only energy; resolving by id; templates; a missing preset; the modifiers; a non rune user cannot cast, assign or open the Spellbook).

In play mode, as a Mage (the part I could not run here):

1. Bind `Spellbook` and the `Ability1…` actions. Make a spell (Synthesis window or the debug panel *Make spell*). The debug panel's **Refill spell energy** fills its runes; **Spells → free keys** assigns them.
2. Open the Spellbook, pick the spell, click a key: the ability bar shows it on that key with its name; hover: tooltip with the Rune Energy cost, Soul Strain, misfire chance, flaws.
3. Press the key: it casts (cast time, cooldown, effect of the form); the debug panel shows *Last cast*, the energy drawn and the strain. Get hit while casting: the energy comes back. Lower the soul with *Damage 30*: cast time and cost rise.
4. As a non-Mage: abilities, essences and the bar behave exactly as before; the Spellbook refuses to open.
5. Host + client: assign on the client; the key shows on both, the cast is seen by other players.

## Decisions and limitations of this phase

1. **One interface, not three.** The plan sketched `ICastable`, `ICastableCosts`, `ICastableInfo` and `AbilityGrant : ICastable`. One small `ICastable` is enough, and essences are left alone entirely (less risk to existing behaviour).
2. **Assignment goes through `ApplyReplicatedSlot`**, the existing public method that sets a slot's ability, modifiers and name at once, instead of new assign/swap methods on the controller: the server's slot content replicates by the existing code path.
3. **Debuffs are in the slot's modifiers**, not a caster-wide modifier layer (which would debuff every ability of the player, spells or not).
4. **A misfire cuts the cast off while it is being cast** (50–90% of its cast time) instead of "on release": the player sees and feels it; the energy stays spent.
5. **Meditation and rituals are unchanged.** Whether a character held still by them can start a cast is decided by the existing availability rules, as for any ability; this phase adds nothing there.
6. **No icon per spell** (the ability bar shows its initials), no HUD badge; the tooltip carries the information. **No sounds or particles of its own**: a spell looks like its form's preset.
7. **The `ritual` form does nothing in the world** until you give it a template (see above).
8. **Each cast changes the energy of the anchored runes**, which re-sends the owner's rune list (the same data the Rune Soul window shows). That is a few kilobytes per cast and cast rates are limited by the spell's cooldown.
9. **A spell that anchors runes can only be cast by its soul**: a key whose spell is not in the book (a stale save) is cleared.
10. **The Spelling System is Phase 8** ([08](08-Spelling-System.md)): `CompiledSpell.usesSpelling` is set by it (the tooltip says *Spelling System: yes* for marked spells); with its mode on None, a spell's cast is exactly as described here.
11. **Not checked in Unity.** The window uses the same pieces as the other Rune Magic windows and every rule is tested without Unity, but I could not look at it, nor run a cast: expect to adjust the layout in `SpellbookWindow.Build`, and to tune the numbers.
12. **Still open: D16.** This phase changes three of the ability files listed there: `AbilitySlot.cs`, `HudAbilityBar.cs` and `PlayerAbilityController.cs` (which Phase 1 already changed). If your local copies differ from git HEAD, merge the small changes above by hand instead of overwriting.
13. **Phase 9 review fixes**: a spell moved to another key keeps what is left of its cooldown (the cooldown is remembered by spell, so unassigning and assigning again is no way around it); assigning, moving or removing a spell is refused while the spell is being cast or a spelling sequence is open (the occupant of the slot is never swapped from under a cast); the misfire roll is made with the server's own seed; a soul whose component was added at runtime is hooked into the ability system at once; the hooks are removed when the soul is destroyed.
14. **Balance (Phase 9):** the defaults reproduce the spec's worked example (16.3): a powerful rank 3 spell of complexity 0.5 draws 500 Rune Energy and causes 12 Soul Damage, and two of them in a row leave the soul at 76, Strained, with a longer casting time and reduced stability. (The spec's second hit, 15, would need a damage multiplier the spec does not give to a Stable soul; it is not simulated.) A database created before Phase 9 keeps the values it was created with: set *Spells ▸ Cast Energy Fraction* to 0.02, *Strain Base* to 4 and *Strain Per Rank* to 1, or use *Reset to Built-in Content* in the database inspector (it restores every table to the built-in values; class lists and extensions are kept).
