# Rune Magic 09 — Extending the system

Everything the Rune Magic System does is **data** (design directives 4 and 5): a new rune, concept, intention, desired effect, spell form or Spelling rule is an entry in the Rune Magic Database, never a code change. This guide says what to fill in for each, what the system then does by itself, and how to check it. The [data reference](09-Data-Reference.md) lists every field.

## Where content lives

| | |
|---|---|
| **The database asset** | `Assets/Resources/SimpleMovements/Settings/RuneMagicDatabase.asset` (*Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Rune Magic Database (Resources)*). Without it, the built-in content (`RuneMagic/Core/Defaults/`) is used. In a network session **the server's database decides**. |
| **A content pack** | Another Rune Magic Database (*Assets ▸ Create ▸ SimpleMovements ▸ Rune Magic ▸ Rune Magic Database*) listed in the main database's **Extensions**. Its entries are added to the main database's; an entry with the **same id** replaces the main one. Packs may list further packs. |
| **The built-in content** | `RuneDefaults*.cs`: edit it only to change what a *new* database starts with. A database that already exists keeps its values (*Reset to Built-in Content* in the inspector restores every table; class lists and extensions are kept). |

Three tools check what you did, none of them needs a scene:

1. *Rune Magic ▸ **Validate** Rune Magic Database*: errors (broken ids, an effect no concept can produce, an effect with no spell form, an archetype naming an unknown rune…) and warnings.
2. *Rune Magic ▸ **Balance Report***: the pace of the whole loop in numbers (energy of a kill, kills per drop, energy and Soul Damage of every archetype, casts per rune, strain per cast, recovery time, letters of a sequence, concepts nothing can teach).
3. *Rune Magic ▸ **Synthesis Lab***: try a formula against the data (the score, the tier, the reasons).

**Ids** are lowercase letters, digits, `.`, `_`, `-`. Saves and network messages store ids; **never change the id of an entry players may already hold** (change its display name instead).

## Add a Rune

*Runes ▸ +*

1. `id` (`tide`), `displayName`, `description`, `kind` (`Base` for runes monsters carry, `Composite` for runes only synthesis makes, `Primordial` for the rarest), `alignments` (ids from *Alignments*; add one first if it is a new domain), `slotCost` (soul slots it takes), `minRank`/`maxRank`, `rarity`, `magicalProperties` (free text shown to the player), `color`.
2. **Concepts**: give it 3–8 (see below). A rune with no concepts can be absorbed but never used in a formula.
3. **Make it appear**: add it to a **Drop Profile** (`runes` list: rune id, `weight`, and the rank range it drops at) so monsters carry it — match by monster words or tags (*Drop Profiles ▸ matchWords / matchTags*); a rune nothing drops can still be given by a quest or a pickup item (`RuneItems`).
4. **Make its concepts learnable**: a *Discovery Source* teaches a concept if it lists the rune or the concept (or is generic). The Balance Report line *Concepts no source can teach* must stay 0.

What the system then does by itself: the rune is absorbed with the capacity check, holds `Energy By Rank` energy, can be given energy, appears in the Synthesis window, its concepts take part in every formula through their aspects, and rank advancement (7 runes of the same rank make the next rank) applies.

## Add a Concept

To an existing rune: its **Concepts** list. To a rune defined elsewhere (a content pack): **Concept Additions** with the rune's id. The concept's id is the rune id, a dot and a key: `wind.pressure`.

Fill in `aspects` (what the concept *is*, as weights over the aspect vocabulary: `pressure 1, force 0.5, containment 0.3`), `rarity` and `complexity` (how hard it is to discover: it chooses the band of difficulty of the dungeon source that can teach it), `minimumRankToUse` (0 = any rank), `description`.

There is **no recipe to write**: the evaluator reads aspects, so the concept works with every intention and desired effect that asks for those aspects. Optional: a written **Riddle** (`riddles` list: `concept`, `text`, `distractors`, bands) — concepts without one get a generated riddle.

## Add an Intention

*Intentions ▸ +* — `id`, names, `aspectBias` (what the purpose favours), `primaryCategories` (effect categories it supports fully), `secondaryCategories` + `secondarySupport` (partly), `effectOverrides` (support for one effect). Nothing else: it appears in step 1 of the Synthesis window. Validation warns when it supports no effect.

## Add a Desired Effect

*Effects ▸ +* — `id` with its category prefix (`offense.`, `defense.`, `movement.`, `control.`, `creation.`, `utility.`), `category`, `required` (stages: each asks `minCoverage` on any of several aspects), `supporting` aspects, `idealChain` (best order of aspects; empty = the order of the requirements), `powerDemand`, `complexity`, `tags`.

**It also needs a spell form**, because the result is a spell: either name it in a form's `effects` list (*Spell Forms*), or let its category fall back to a form (`fallbackFor`). The built-in `ritual` form takes everything that has no combat equivalent: it costs energy and strains the soul and does nothing in the world until you give it a template (see *Add a Spell Form*). Validation refuses an effect with no form.

## Add a Spell Form

A form is the executable shape of spells: `preset` (the name of a built-in ability preset of the existing ability system: projectile, lobbed, wall, dash…), `effects` it serves, `fallbackFor` categories, `canMoveWhileCasting`, and multipliers for energy, strain, range and area.

To give a form a real behaviour of your own, create an `AbilityDefinition` asset with the existing ability tools and assign it in **Rune Magic Database ▸ Spell Form Templates** with the form's id. Its mana/stamina/health costs are ignored (a spell costs Rune Energy). The spell's own numbers (power, stability, cast time, cooldown, range, area, soul condition debuffs, Spelling performance) are applied on top as the slot's modifiers, so the template is shared by every spell of the form.

## Add an Archetype (a named reference formula)

*Archetypes ▸ +* — `intention`, `effect`, `mode` (`Any`, `Spell` or `Rune`: what the formula may produce), `stages` (rune id, concept id, count, energy weight — in order), `orderMatters`, `strictness`, `countTolerance`, `matchThreshold`, and the result's name and properties. An archetype is **not a recipe**: it *names* a result and refines the score when a player's formula resembles it. A formula that is not any archetype still works, and still scores by its aspects. Add one for a result you want named, taught by a book, or tested by the Balance Report.

## Add compatibility rules and aspects

- **Aspect**: *Aspects ▸ +*, then use it in concepts, alignments, effects.
- **Rule**: *Rules ▸ +* — `scope` (`AspectPair`, `ConceptPair`, `AlignmentPair`, `IntentionEffect`, `ConceptIntention` or `ConceptEffect`), `a`, `b`, `value` (+ synergy, − conflict), `ordered` (only in the order `a` then `b`), `incoherent` (the combination always fails completely).

## Add drops, sources, riddles

- **Drop Profile**: which monsters (`matchWords`, `matchTags`, `priority`) carry which runes (`runes` with weights), `dropChance`, `maxDrops`, `baseRank`, `qualityRange`, `startingEnergy`, `energy` (the magical energy a kill of that monster gives). Global multipliers per rarity of monster and per dungeon tier are in *Acquisition*.
- **Discovery Source**: a kind of thing in a dungeon that teaches (`kind`, `runes`/`concepts` it teaches, `minTier`/`maxTier`, `minigame`, `intro`). Put the prefab (*Rune Magic ▸ Create Concept Discovery Source Prefab*) in a Dungeon Prop Table.
- **Riddle**: see *Add a Concept*.

## Add Spelling rules

*Spelling Rules ▸ +* (the database's `spellingRules`) — `scope` (`Form`, `Effect`, `Archetype`), `id` of that scope, `use` (`Inherit`, `Yes`, `No`), and any of: `letters` or `lettersDelta`, `keyWindow`/`keyWindows`/`windowMultiplier`, `totalLimit`, `rhythm` + `rhythmTempo`/`rhythmTolerance`, `failureModel` (Strict/Lenient), `maxMistakes`, `speedBonusThreshold`, `damageMode`/`damageAmount` (Soul Damage for mistakes: never automatic). The global mode (None / Selected / All) is *Balance ▸ Spelling ▸ Mode*; with Selected, only spells a rule marks use it. The server decides which spells use it and how hard it is; the client only follows. See [08](08-Spelling-System.md).

## Add a Soul condition, damage kind, failure severity or flaw

All are tables in *Balance* (`soulConditions`, `soulDamageKinds`, `failureSeverities`; the flaws are the database's `synthesisFlaws`): add an entry with its thresholds and modifiers. Validation keeps the invariants (exactly one Soul Collapse condition; a condition at full integrity).

## Change the numbers

Every number is in the **Balance** tables (see the [data reference](09-Data-Reference.md)). Change one, run *Balance Report*, read the line it moved. The tests in `BalanceReportTests` keep the built-in numbers inside the ranges that make the loop work (a first rune charges in 3–20 normal kills; a rank 3 spell costs about 500 energy and 12 Soul Damage; recovery from Strained takes minutes, not hours); if you retune on purpose, change the range in the test too.

## Data formats

| What | Format | Versioning |
|---|---|---|
| The database | Unity serialization of `RuneMagicData` | `RuneMagicData.version` (now 9); `Upgrade` converts older assets and only ever adds fields. |
| A character's soul | JSON of `RuneSoulState` (`RuneSoulSaves`): soul (integrity, deep damage, collapse), runes (instance id, rune id, rank, quality, energy, flags), knowledge, discovery state, spells (`SpellRecord`), spell slots, synthesis log, statistics | `RuneSoulState.version` (now 6); older saves load and fill what they lack. Dictionaries are never used (JsonUtility); ids are strings. |
| Network requests | `RuneRequestKind` (byte, 1…17) + text/amount/flag; text limits in `RuneNetLimits` | Append-only: a kind never changes its value (`RegressionGuardTests` pins them). |
| A synthesis request | JSON (`SynthesisRequestCodec`): mode, intention, effect, slots (rune instance, concept, energy), optional name; **the seed never travels** | Decoded with every size and number checked. |
| A spelling result | `seq\|endMs\|KEY=ms,KEY=ms…` (`SpellingResultCodec`), ≤ 1024 characters | The server recomputes the score; the client never says how well it did. |
| Replicated views | JSON, owner only (`SpellingView`, the rune list, an energies-only update when only energy changed, the discovery view, the open grimoire) | Limits in `RuneNetLimits`. |
| A grimoire | The item id `grimoire:<serial>:<author>` (every machine makes the item from it) and the world's ledger of pages under the serial (`GrimoireLedger` JSON, section `runeMagicGrimoires` of the world's store) | See [10 — The Grimoire](10-Grimoire.md). |

## Add a new network request (developer)

Append a value to `RuneRequestKind` (never reuse or reorder), raise `RuneNetLimits.LastKind`, give it a text limit in `MaxTextFor`, handle it in `RuneSoul` (the server side, through `NetAuthority`) and send it from `NetRuneMagic`. Update the guard in `RegressionGuardTests` on purpose. Every handler starts with the same checks: is the sender a rune user, is the soul usable, is the text valid.

## Add a class that uses runes

List its name in *Settings ▸ Rune User Class Names*, or drag class assets / archetypes into the lists. Rune users cannot absorb essences, and nobody else gets a soul; both rules are code, not settings.
