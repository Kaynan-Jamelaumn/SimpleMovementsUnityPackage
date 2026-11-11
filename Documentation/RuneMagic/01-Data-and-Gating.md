# Rune Magic 01 — Data and Mage gating

Everything in the Rune Magic System is data, held in one asset: the **Rune Magic Database**. That covers runes, concepts, intentions, desired effects, how they relate, every balancing number, and who may use runes.

New content needs no code: add entries to the database, or ship a content pack as another database in **Extensions**.

## The database

*Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Rune Magic Database (Resources)* creates
`Assets/Resources/SimpleMovements/Settings/RuneMagicDatabase.asset` with the built-in content.

| Field | Meaning |
|---|---|
| Rune User Classes / Rune User Archetypes | Class assets / Class archetypes whose players use Rune Magic (in addition to the names in Settings) |
| Data ▸ Settings | On/off switch, rune-user class names, whether rune users' kills still drop essence orbs for others |
| Data ▸ Balance | Every balancing table (below) |
| Data ▸ Aspects, Alignments, Runes, Intentions, Effects, Rules | The content |
| Data ▸ Concept Additions | Concepts added to runes defined elsewhere (content packs) |
| Extensions | More databases merged into this one: same id replaces, new ids are added |

**Inspector tools:**
- The inspector shows counts, the content hash and every validation problem.
- **Reset to Built-in Content** restores the built-in data. Class lists and extensions are kept.

**In a network session** the server's database decides.

## Ids

Every entry has a stable lowercase id. Allowed characters are letters, digits, `.`, `_` and `-`.

| Kind | Example id |
|---|---|
| Rune | `wind` |
| Concept | `wind.pressure` (rune id, a dot, the concept) |
| Intention | `attack` |
| Desired effect | `offense.explosion` (category prefix: `offense`, `defense`, `movement`, `control`, `creation`, `utility`) |
| Compatibility rule | `aspect:pressure+combustion` (`>` instead of `+` when the order matters) |

Saves and network messages only ever use ids. Renaming an entry's **display name** is always safe; changing its **id** makes saved data refer to something that no longer exists.

## Aspects: how everything relates

Concepts, alignments, intentions and desired effects each describe themselves with weighted **aspects**. These come from a small, extensible vocabulary: force, pressure, containment, expansion, compression, motion, rotation, thermal, cold, combustion, wave, disruption, spatial, matter, structure, fluid, life, decay, mind, sense, light, shadow, energy, binding, attraction, repulsion, absorption, reflection, suppression, focus, time and soul.

- **A concept** lists what it does. Wind ▸ Pressure is `pressure 1, force 0.5, containment 0.3`.
- **A desired effect** lists what the chosen concepts must provide together (**Required**).
  - Each requirement asks for a total weight (*Min Coverage*) on any of its aspects.
  - **Supporting** aspects improve the result.
  - **Ideal Chain** is the best order of aspects. When empty, it is the order of the requirements.
  - Example: Explosion needs pressure / compression / containment, then combustion / thermal / energy / wave, then expansion / force / wave. That is the Wind ▸ Pressure → Ash ▸ Combustion → Vibration ▸ Resonance order of Pyroclastic Resonance.
- **An intention** favours aspects (**Aspect Bias**) and supports desired effects:
  - fully in its **Primary Categories**;
  - partly in its **Secondary Categories** (**Secondary Support**);
  - per effect through **Effect Overrides**.
- **Compatibility rules** add synergies (+) and conflicts (−), between two aspects, two concepts or two alignments, optionally only in one order.
  - A rule marked **Incoherent** makes a combination always fail completely.
  - An **IntentionEffect** rule overrides an intention's support for one effect.

Because the evaluation reads aspects, a new rune or concept works with every intention and effect as soon as it has aspects. The synthesis engine (Phase 5) never needs a recipe.

**Validation** reports:
- a concept without aspects;
- an effect that no concept could ever produce;
- an intention that supports nothing;
- broken ids.

## Adding content

| To add | Do |
|---|---|
| A rune | Runes ▸ +: id, name, alignment ids, slot cost, ranks, rarity, properties, concepts (ids `<rune>.<concept>`, each with aspects) |
| A concept to an existing rune | Its rune's Concepts list, or (content pack) Concept Additions with the rune's id |
| An intention | Intentions ▸ +: categories it supports, aspect bias, overrides |
| A desired effect | Effects ▸ +: category, requirements (aspects), supporting aspects, optional ideal chain, power demand, complexity |
| An aspect | Aspects ▸ +, then use it in concepts and effects |
| A content pack | Create another Rune Magic Database (*Assets ▸ Create ▸ SimpleMovements ▸ Rune Magic ▸ Rune Magic Database*), add your entries, put it in the main database's Extensions |

## Balance tables

All values come from the design spec, where they are marked illustrative or proposed, so tune them freely.

| Table | Built-in values |
|---|---|
| Capacity Stages | Beginning 7 slots (level 1), Intermediate 14 (level 20), Advanced 21 (level 40), Endgame 28 (level 60). The levels are placeholders. |
| Energy By Rank | Rank 1 1,000 · 2 5,000 · 3 25,000 · 4 125,000 · 5 625,000. Ranks above the table: × Energy Growth Beyond Table (5) per rank. |
| Quality Affects Max Energy | Off: quality and maximum energy are independent unless you turn this on. |
| Rank Titles | 1 Greater (so rank 1 Wind is "Greater Wind"), 2 Grand, 3 Exalted, 4 Mythic, 5 Transcendent (placeholders). |
| Runes Per Rank Advancement | 7 runes of the same rank make a rune of the next rank. |
| Quality Tiers (0-100) | Crude 0, Flawed 15, Common 35, Fine 55, Superior 75, Exceptional 90, Flawless 100. |
| Synthesis thresholds | Perfect 100 %, Successful from 70 %, Flawed from 30 %, below that Complete Failure. |
| Failure Severities | Minor (score ≥ 20, loses 25 % of the allocated energy), Moderate (≥ 10, 60 %), Severe (100 %), Catastrophic (risk ≥ 0.7: runes destroyed). |
| Soul Integrity | Maximum 100. |
| Soul Conditions | Stable ≥ 80 %, Strained ≥ 60 % (Mild Strain), Damaged ≥ 40 % (Moderate), Critical ≥ 20 % (Severe), Near Collapse > 0 (Critical Strain: synthesis disabled), Soul Collapse at 0 (no casting). Each condition carries its synthesis and casting modifiers. |

Rank, quality, energy and concept knowledge are separate values; the only coupling between them is the optional quality → maximum energy switch above. A concept can optionally require a minimum rank (**Minimum Rank To Use**); none of the built-in ones do, and rank never grants concepts.

## Built-in content

**Runes**
- All runes of the design spec: 24 novel-inspired and 15 original.
- **Binding** and **Restoration** are added as original runes, so the spec's Soul Chain and Eternal Bloom combinations are possible.
- Each rune has its alignment (the domain text of the spec).

**Concepts**
- The concepts of Greater Wind, Fire, Vibration and Space are the spec's, verbatim.
- Every other rune has 6-8 proposed concepts (269 concepts in all). Rename or replace them as you like.

**Intentions and desired effects**
- The 31 intentions and 101 desired effects have the spec's names and descriptions.
- Their aspect requirements and intention supports are proposals.
- "Seal" exists twice: `defense.seal` and `utility.seal`.

## Mage-only gating

**Who is a rune user.** A player is a rune user when Rune Magic is enabled and their class is a rune-user class:
- listed in *Rune User Classes*;
- built on a listed Class archetype;
- or whose asset name, display name or Class archetype name is in *Settings ▸ Rune User Class Names*.

The default name is `Mage`, which also matches the Mage archetype preset. Matching ignores case; races are never matched.

**What a rune user can never do: absorb an ability (essence).** The block lives in the ability system as a generic hook, `AbsorptionGate`, so the ability code knows nothing about runes:

| Where | What happens for a rune user | Everyone else |
|---|---|---|
| `PlayerAbilityController.CanReceive` | Refuses every ability grant (pickups, instant delivery, rewards) | Unchanged |
| `PlayerAbilityController.RestoreAbsorbedAbilities` | Skips saved absorbed abilities | Unchanged |
| `AbilityPickup` | Never flies to or is taken by a rune user, so another player can collect it | Unchanged |
| `AbilityAbsorption` (Instant delivery) | The ability drops as a pickup instead of being lost | Unchanged |
| `AbilityAbsorption` (rolls) | With *Essence Rolls On Rune User Kills* off, the rune user's kills roll no absorption at all | Unchanged |

**Settings:**
- *Essence Rolls On Rune User Kills* is on by default: monsters a Mage kills still drop essence orbs for the other players.
- *Enabled* off turns the whole system off; nobody is a rune user.

The rule runs where the game rules run (offline, or on the server). The class comes from the character the server accepted.

**For your own code:** `RuneMagic.RuneMagicGate.IsRuneUser(component)` tells whether a player is a rune user; `RuneMagicGate.IsRuneUserClass(playerClass)` does the same for a class.

## Tests

*Window ▸ General ▸ Test Runner ▸ Edit Mode*:
- **`RuneMagic.Tests.RuneMagicDataTests`** checks:
  - the built-in content against the spec (names and descriptions verbatim);
  - the balance tables and lookups;
  - intention support;
  - content packs, validation and JSON round trips.
- **`RuneMagic.Tests.RuneMagicGateTests`** checks:
  - the class matching;
  - the absorption gate.

Tests in the `RequiresEngine` category create class and database assets in memory.
