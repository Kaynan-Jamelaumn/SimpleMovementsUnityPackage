# Rune Magic 09 — Data reference

Every table the Rune Magic System reads, field by field, with its built-in default. **Generated** from the definitions in `RuneMagic/Core/Data/` (the source of truth: a field is added there, with its tooltip, and the next generation lists it). All of it is edited in the **Rune Magic Database** asset (*Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Rune Magic Database (Resources)*), is validated by *Rune Magic ▸ Validate Rune Magic Database*, and is shown in numbers by *Rune Magic ▸ Balance Report*.

Reading the tables: **Default** is the value of a new database; **Range** is the clamp of the inspector (the rules clamp too); a list's **Default** is empty here, because the built-in content is created by `RuneDefaults` (see the [extending guide](09-Extending-Guide.md)). Ids are lowercase and stable: saves and network messages store ids, never positions.


## The database (`RuneMagicData`, the content of the Rune Magic Database asset)

`RuneMagic/Core/Data/RuneMagicData.cs`


### `RuneMagicData`

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `version` | `int` | `9 (CurrentVersion)` |  | Format version of the data; Upgrade converts older assets (9 now: 2 soul damage kinds, 3 drops and acquisition, 4 concept discovery, 5 synthesis engine, 6 spellcasting, 7 spelling, 8 the grimoire, 9 charging). |
| **Settings** | | | | |
| `settings` | `RuneMagicSettings` | `new RuneMagicSettings()` |  | *Settings.* |
| `balance` | `RuneBalance` | `new RuneBalance()` |  | *Balance.* |
| **Content** | | | | |
| `aspects` | `List<AspectDef>` | `(empty)` |  | The vocabulary that describes what magic does. |
| `alignments` | `List<AlignmentDef>` | `(empty)` |  | Fundamental magical domains. |
| `runes` | `List<RuneDef>` | `(empty)` |  | Rune definitions, each with its concepts. |
| `conceptAdditions` | `List<RuneConceptAddition>` | `(empty)` |  | Concepts added to runes defined elsewhere (content packs). |
| `intentions` | `List<IntentionDef>` | `(empty)` |  | General purposes of a synthesis. |
| `effects` | `List<DesiredEffectDef>` | `(empty)` |  | Specific results of a synthesis. |
| `rules` | `List<CompatibilityRule>` | `(empty)` |  | Synergies, conflicts, supports and incoherences. |
| `dropProfiles` | `List<RuneDropProfile>` | `(empty)` |  | Which monsters carry which runes, and the magical energy they give. |
| `discoverySources` | `List<DiscoverySourceDef>` | `(empty)` |  | Kinds of knowledge source found in dungeons (inscriptions, artifacts, sealed chambers...). |
| `riddles` | `List<RiddleDef>` | `(empty)` |  | Riddles written for concepts. Concepts without one get generated riddles. |
| `archetypes` | `List<OutcomeArchetype>` | `(empty)` |  | Named reference formulas (Pyroclastic Resonance, Gravity Well...): they name a result and refine its score. Not recipes. |
| `synthesisFlaws` | `List<SynthesisFlawDef>` | `(empty)` |  | Ways a flawed synthesis can go wrong. |
| `spellForms` | `List<SpellFormDef>` | `(empty)` |  | The executable shapes of spells (a projectile, an area, a wall...) and the desired effects each serves. |
| `spellingRules` | `List<SpellingRule>` | `(empty)` |  | Which spells use the Spelling System (key-sequence casting) and how hard it is for them: rules by form, desired effect or archetype. |

## Settings: who is a rune user, switches

`RuneMagic/Core/Data/RuneMagicSettings.cs`


### `RuneMagicSettings`

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `enabled` | `bool` | `true` |  | Turn the whole Rune Magic System on or off (off: nobody is a rune user, everyone absorbs abilities as before). |
| `runeUserClassNames` | `List<string>` | `new List<string> { "Mage" }` |  | Classes whose players use Rune Magic, by name: a class matches by its asset name, its display name or the name of its Class archetype (case does not matter). The Rune Magic Database can also list class assets. |
| `matchClassArchetypes` | `bool` | `true` |  | Also match the Class archetypes a character has (classes defined only as an archetype, or a class whose archetype is the Mage preset). |
| `essenceRollsOnRuneUserKills` | `bool` | `true` |  | When a rune user kills a mob, its absorbable abilities still roll and drop as pickups for the other players (the rune user is never the one who takes them). Off: a rune user's kills roll no absorption at all. |
| `addMissingSoulComponents` | `bool` | `true` |  | A rune user whose player object has no Rune Soul gets one when the game starts (with a warning): their soul is kept and its rules run. Add Rune Magic to the player prefab (Tools ▸ SimpleMovements ▸ Rune Magic) for the HUD gauge, meditation input and network replication. |
| `debugPanel` | `bool` | `true` |  | Development builds and the editor: the Debug Panel input action shows the Rune Magic debug panel (the local player's soul; tools to damage, heal and test it where the rules run). |
| **Input actions (names in the player's Input Actions asset)** | | | | |
| `meditateAction` | `string` | `"Meditate"` |  | Rune Magic reads its inputs only from these actions of the Input Actions asset: no key is fixed in code. Add them to the Player map and bind them there (Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune Magic Input Actions adds missing ones without a key); players rebind them in Key Bindings. An action that is missing does nothing. |
| `runeSoulAction` | `string` | `"RuneSoul"` |  | Opens and closes the Rune Soul window. |
| `debugPanelAction` | `string` | `"RuneMagicDebug"` |  | Opens and closes the debug panel (development builds and the editor). |
| `synthesisAction` | `string` | `"Synthesis"` |  | Opens and closes the Rune Synthesis window (the Rune Soul window also has a button for it). |
| `spellbookAction` | `string` | `"Spellbook"` |  | Opens and closes the Spellbook (the Rune Soul window also has a button for it). Spells are CAST with the ability keys (Ability1, Ability2... of the Input Actions asset): this action only opens the book where they are put on those keys. |
| `grimoireAction` | `string` | `"Grimoire"` |  | Opens and closes the Grimoire window (the Rune Soul window also has a button for it): the book a Mage starts with, where runes of the soul are imprinted and prints absorbed. |

### `RuneUserRules`

The rule that decides from a character's class whether it is a rune user (pure, testable).


## Enumerations

`RuneMagic/Core/Data/RuneEnums.cs`


### `RuneKind`

What a rune is made of.

| Value | Meaning |
|---|---|
| `Base` | A rune of one alignment, as monsters carry them (Greater Wind, Greater Fire...). |
| `Composite` | A rune made by synthesis from other runes (Pyroclastic Resonance...): several alignments. |
| `Primordial` | A rare, dangerous rune: synthesis and casting with it strain the soul more and leave deep damage. |

### `RuneRarity`

How rare a rune or a concept is (drops, discovery difficulty, value).

| Value | Meaning |
|---|---|
| `Common` |  |
| `Uncommon` |  |
| `Rare` |  |
| `Epic` |  |
| `Legendary` |  |

### `EffectCategory`

The families of Desired Effects (design spec section 10).

| Value | Meaning |
|---|---|
| `Offensive` |  |
| `Defensive` |  |
| `Movement` |  |
| `Control` |  |
| `CreationTransformation` |  |
| `UtilityExploration` |  |

### `CompatibilityScope`

What a `CompatibilityRule` relates.

| Value | Meaning |
|---|---|
| `AspectPair` | Two aspects (most rules: they apply to every concept that carries them). |
| `ConceptPair` | Two specific concepts. |
| `AlignmentPair` | Two alignments. |
| `IntentionEffect` | An intention and a desired effect: `value` is the support (0-1). |
| `ConceptIntention` | A concept and an intention (a bonus or a penalty). |
| `ConceptEffect` | A concept and a desired effect (a bonus or a penalty). |

## Content definitions: aspects, alignments, runes, concepts, intentions, desired effects, rules

`RuneMagic/Core/Data/RuneDefinitions.cs`


### `AspectDef`

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id (lowercase): 'pressure', 'thermal'... |
| `displayName` | `string` | `""` |  | The name the player sees (empty: the id is shown). |
| `description` | `string` | `""` |  | The text the player reads. |

### `AspectWeight`

An aspect and how strongly something carries it (0-1, more is allowed for dominant aspects).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `aspect` | `string` | `""` |  | Aspect id. |
| `weight` | `float` | `1` | 0 … 2 | *Weight.* |

### `AlignmentDef`

A fundamental magical domain (Fire: flames and combustion; Wind: air movement and pressure...). Base runes have one; composite runes (made by synthesis) can have several.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id (lowercase): 'fire', 'wind'... |
| `displayName` | `string` | `""` |  | The name the player sees (empty: the id is shown). |
| `domain` | `string` | `""` |  | The domain in a few words (design spec 7.2 / 7.3). |
| `aspects` | `List<AspectWeight>` | `(empty)` |  | What the domain is about, as aspects. |
| `element` | `ElementType` | `ElementType.None` |  | Combat element used by spells of this alignment (None = magical damage without an element). |
| `color` | `Color` | `Color.white` |  | Colour used by the UI for this entry. |

### `ConceptDef`

A deeper principle of a rune that the player discovers and can use in synthesis (Wind ▸ Pressure, Fire ▸ Ignition). Its id is the rune id, a dot and the concept's own key: 'wind.pressure'.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id: '.' (e.g. 'wind.pressure'). |
| `displayName` | `string` | `""` |  | The name the player sees (empty: the id is shown). |
| `description` | `string` | `""` |  | The text the player reads. |
| `aspects` | `List<AspectWeight>` | `(empty)` |  | What the concept does, as aspects: the synthesis evaluation reads these. |
| `rarity` | `RuneRarity` | `RuneRarity.Common` |  | How rare the knowledge is: rarer concepts are found in harder dungeons. |
| `complexity` | `float` | `0.3` | 0 … 1 | How hard the principle is to manipulate (0-1): raises synthesis risk and spell complexity. |
| `minimumRankToUse` | `int` | `0` | ≥ 0 | Explicit coupling (optional): the lowest rune rank that can use this concept. 0 = any rank. Rank never grants concepts; this only restricts a known one. |
| `tags` | `List<string>` | `(empty)` |  | Free keywords for rules and tools. |

### `RuneDef`

A rune definition: the kind of rune (Wind, Fire, Pyroclastic Resonance...). Rank, quality and energy belong to each rune the player owns, not to the definition; the concepts belong to the definition, and which of them a player knows is the player's knowledge.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id (lowercase): 'wind', 'pyroclastic_resonance'... |
| `displayName` | `string` | `""` |  | Name without the rank title ('Wind'; shown as 'Greater Wind' at rank 1). |
| `description` | `string` | `""` |  | The text the player reads. |
| `kind` | `RuneKind` | `RuneKind.Base` |  | The kind it belongs to (see the enumeration of the same name). |
| `alignments` | `List<string>` | `(empty)` |  | Alignment ids: one for base runes, several for composites. |
| `slotCost` | `int` | `1` | ≥ 1 | Soul slots it occupies once absorbed. |
| `minRank` | `int` | `1` | ≥ 1 | Lowest and highest rank this rune can have. |
| `maxRank` | `int` | `5` | ≥ 1 | *Max rank.* |
| `useRankTitle` | `bool` | `true` |  | Show the rank title before the name ('Greater Wind'). Composite runes usually have names of their own. |
| `rarity` | `RuneRarity` | `RuneRarity.Common` |  | How rare it is (the enumeration's order is the rarity order). |
| `magicalProperties` | `string` | `""` |  | Inherent characteristics and limitations (shown to the player). |
| `tags` | `List<string>` | `(empty)` |  | Keywords of its properties ('volatile', 'stable', 'heavy'...). |
| `concepts` | `List<ConceptDef>` | `(empty)` |  | The hidden principles of this rune, discovered one by one. |
| `color` | `Color` | `Color.white` |  | Colour used by the UI for this entry. |

### `EffectSupport`

Support of an intention for one desired effect (0 = not supported, 1 = fully).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `effect` | `string` | `""` |  | Desired effect id. |
| `support` | `float` | `1` | 0 … 1 | *Support.* |

### `IntentionDef`

The general purpose of a synthesis ("What am I trying to accomplish?"), chosen before the desired effect. It supports some desired effects and favours some aspects; it never guarantees a result.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id (lowercase): 'attack', 'defense'... |
| `displayName` | `string` | `""` |  | The name the player sees (empty: the id is shown). |
| `description` | `string` | `""` |  | The text the player reads. |
| `aspectBias` | `List<AspectWeight>` | `(empty)` |  | Aspects this purpose favours (concepts carrying them fit the intention better). |
| `primaryCategories` | `List<EffectCategory>` | `(empty)` |  | Desired-effect categories this intention fully supports. |
| `secondaryCategories` | `List<EffectCategory>` | `(empty)` |  | Desired-effect categories it partly supports (Secondary Support). |
| `secondarySupport` | `float` | `0.5` | 0 … 1 | Support of effects in the secondary categories. |
| `effectOverrides` | `List<EffectSupport>` | `(empty)` |  | Specific effects with their own support (outside the categories, or overriding them; 0 = not supported). |

### `AspectRequirement`

One requirement of a desired effect: the selected concepts together must carry at least `minCoverage` of any of these aspects.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `anyOf` | `List<string>` | `(empty)` |  | Aspect ids: any of them counts. |
| `minCoverage` | `float` | `0.5` | ≥ 0 | Total weight the selected concepts must reach on those aspects. |

### `ChainStage`

One stage of a desired effect's ideal sequence (the aspects that should act at that point).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `aspects` | `List<string>` | `(empty)` |  | What it is about, as weighted aspects (the vocabulary that relates every piece of content to every other). |

### `DesiredEffectDef`

The specific result of a synthesis ("What exactly should this magic do?"): Explosion, Pierce, Barrier, Teleport... Whether it can be produced depends on the selected runes, concepts, order and energy, never on a fixed recipe.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id: '.' (e.g. 'offense.explosion'). |
| `displayName` | `string` | `""` |  | The name the player sees (empty: the id is shown). |
| `category` | `EffectCategory` | `EffectCategory.Offensive` |  | *Category.* |
| `description` | `string` | `""` |  | The text the player reads. |
| `required` | `List<AspectRequirement>` | `(empty)` |  | What the selected concepts must provide (every requirement must be met). |
| `supporting` | `List<AspectWeight>` | `(empty)` |  | Aspects that improve the result when present. |
| `idealChain` | `List<ChainStage>` | `(empty)` |  | Ideal order of the aspects (the rune order is compared with it). Empty = the order of the requirements. |
| `powerDemand` | `float` | `1` | ≥ 0.05 | Energy the effect asks for, relative to a normal effect (1). |
| `complexity` | `float` | `0.4` | 0 … 1 | How hard the effect is to shape (0-1). |
| `tags` | `List<string>` | `(empty)` |  | Free keywords for rules and tools. |

### `CompatibilityRule`

A relation between two things the synthesis evaluation should know about: a synergy (positive value) or a conflict (negative), optionally only in one order, or a hard incoherence (always a complete failure). For `IntentionEffect` the value is the support (0-1).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id, e.g. 'aspect:pressure+combustion' (scope, the two ids, '>' when ordered). |
| `scope` | `CompatibilityScope` | `CompatibilityScope.AspectPair` |  | *Scope.* |
| `a` | `string` | `""` |  | First id (aspect, concept, alignment or intention, by scope). |
| `b` | `string` | `""` |  | Second id (aspect, concept, alignment or desired effect, by scope). |
| `value` | `float` | `0` | -1 … 1 | Synergy (+) or conflict (-), -1 to 1. IntentionEffect: the support, 0 to 1. |
| `ordered` | `bool` | `false` |  | Only when A comes before B in the rune order. |
| `incoherent` | `bool` | `false` |  | The combination can never be coherent: a synthesis with both always fails completely. |
| `note` | `string` | `""` |  | Why (for designers and the synthesis log). |

### `RuneConceptAddition`

Concepts a content pack adds to (or replaces in) a rune defined elsewhere, without redefining the rune.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `rune` | `string` | `""` |  | Id of the rune that receives the concepts. |
| `concepts` | `List<ConceptDef>` | `(empty)` |  | Concepts to add (same id = replaces the existing one). |

## Balance tables: soul, capacity, energy, kills, conditions

`RuneMagic/Core/Data/RuneBalance.cs`


### `CapacityStage`

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `name` | `string` | `""` |  | The name the player sees. |
| `fromLevel` | `int` | `1` | ≥ 1 | Character level at which the stage starts. |
| `capacity` | `int` | `7` | ≥ 0 | Soul slots available at this stage (before bonuses from items, abilities, breakthroughs). |

### `RankEnergy`

The energy a rune of a rank can store (design spec 5.3).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `rank` | `int` | `1` | ≥ 1 | *Rank.* |
| `maxEnergy` | `float` | `1000` | ≥ 0 | *Max energy.* |

### `RankTitle`

The title shown before a rune's name at a rank ("Greater" Wind).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `rank` | `int` | `1` | ≥ 1 | *Rank.* |
| `title` | `string` | `""` |  | *Title.* |

### `QualityTier`

A named band of rune quality (stability and potential, 0-100).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `name` | `string` | `""` |  | The name the player sees. |
| `minQuality` | `float` | `0` | 0 … 100 | Lowest quality (0-100) of the band. |

### `SynthesisThresholds`

Score thresholds of the synthesis outcome tiers (design spec 12.2).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `perfect` | `float` | `100` | 0 … 100 | Score (%) needed for a Perfect Synthesis. |
| `successful` | `float` | `70` | 0 … 100 | Lowest score (%) of a Successful Synthesis. |
| `flawed` | `float` | `30` | 0 … 100 | Lowest score (%) of a Flawed Synthesis; below it the synthesis fails completely. |

### `SynthesisTier`

The outcome tier of a synthesis score.

| Value | Meaning |
|---|---|
| `CompleteFailure` |  |
| `Flawed` |  |
| `Successful` |  |
| `Perfect` |  |

### `FailureSeverity`

A level of complete-failure severity and its consequence (design spec 13.3).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `name` | `string` | `""` |  | The name the player sees. |
| `scoreAtLeast` | `float` | `0` | 0 … 100 | Applies when the score is at least this (%); the most lenient level that matches is used. |
| `minimumRisk` | `float` | `0` | 0 … 1 | Applies only when the synthesis risk (0-1) is at least this (destruction is reserved for risky attempts). |
| `energyLoss` | `float` | `0.25` | 0 … 1 | Share of the energy allocated by each input rune that is lost. |
| `destroysRunes` | `bool` | `false` |  | The input runes are destroyed (removed from the soul). |

### `SoulCondition`

A Soul Integrity condition (design spec 14.3) and what it does to synthesis and casting (14.4, 16.2).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id: 'stable', 'strained', 'damaged', 'critical', 'near_collapse', 'collapse'. |
| `name` | `string` | `""` |  | The name the player sees. |
| `minPercent` | `float` | `0` | 0 … 100 | Lowest Soul Integrity (% of the maximum) of the condition. |
| `isCollapse` | `bool` | `false` |  | This is Soul Collapse: the condition at 0 integrity. |
| `castingDebuff` | `string` | `""` |  | Name of the casting debuff tier (design spec 16.2): Mild / Moderate / Severe / Critical Strain, Soul Collapse. |
| `effects` | `string` | `""` |  | *Effects.* |
| **Synthesis** | | | | |
| `synthesisAllowed` | `bool` | `true` |  | *Synthesis allowed.* |
| `synthesisFailureChance` | `float` | `0` | 0 … 1 | Added to the chance that a synthesis fails (0-1). |
| `synthesisStability` | `float` | `1` | 0 … 2 | Multiplies the stability of a synthesis. |
| `maxSynthesisRunes` | `int` | `0` | ≥ 0 | Most runes one synthesis may use (0 = no limit). |
| `maxSynthesisComplexity` | `float` | `1` | 0 … 1 | Most complex desired effect (complexity 0-1) a synthesis may aim for; 1 = any (design spec 14.4: complex combinations become unavailable). |
| **Casting** | | | | |
| `castingAllowed` | `bool` | `true` |  | *Casting allowed.* |
| `castTimeMultiplier` | `float` | `1` | 0.1 … 5 | *Cast time multiplier.* |
| `powerMultiplier` | `float` | `1` | 0 … 2 | *Power multiplier.* |
| `energyCostMultiplier` | `float` | `1` | 0.1 … 5 | *Energy cost multiplier.* |
| `instabilityChance` | `float` | `0` | 0 … 1 | Chance (0-1) that a spell behaves unstably. |
| `maxCastableRank` | `int` | `0` | ≥ 0 | Highest spell rank that can still be cast (0 = any). |
| `soulDamageMultiplier` | `float` | `1` | 0 … 5 | Multiplies Soul Damage taken from casting and synthesis while in this condition. |
| **Spelling System (design spec 16.5)** | | | | |
| `spellingExtraLetters` | `int` | `0` | ≥ 0 | Letters added to every sequence of a spell that uses the Spelling System. |
| `spellingWindowMultiplier` | `float` | `1` | 0.1 … 2 | Multiplies the time window of every key (below 1 = shorter windows). |
| `spellingPenaltyMultiplier` | `float` | `1` | 0.1 … 5 | Multiplies what a mistake costs (power, time, stability). |

### `RuneBalance`

All the balancing tables of the Rune Magic System, with the lookups the rules use.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| **Soul Rune Capacity (design spec 4.1, proposed values)** | | | | |
| `capacityStages` | `List<CapacityStage>` | `(empty)` |  | Capacity stages (a list; each entry is documented in its own table). |
| **Rune Energy (design spec 5.3, illustrative values)** | | | | |
| `energyByRank` | `List<RankEnergy>` | `(empty)` |  | Energy by rank (a list; each entry is documented in its own table). |
| `energyGrowthBeyondTable` | `float` | `5` | ≥ 1 | Ranks above the table: the last rank's maximum × this, per rank. |
| `qualityAffectsMaxEnergy` | `bool` | `false` |  | Explicit coupling, off by default: quality raises the maximum energy. |
| `maxEnergyBonusAtFullQuality` | `float` | `0` | 0 … 2 | With the coupling on: extra maximum energy at quality 100 (0.2 = +20%). |
| **Ranks** | | | | |
| `rankTitles` | `List<RankTitle>` | `(empty)` |  | Rank titles (a list; each entry is documented in its own table). |
| `runesPerRankAdvancement` | `int` | `7` | ≥ 2 | Runes of the same rank a rank advancement combines (design spec 6.1). |
| **Quality (0-100)** | | | | |
| `qualityTiers` | `List<QualityTier>` | `(empty)` |  | Quality tiers (a list; each entry is documented in its own table). |
| **Synthesis outcomes (design spec 12.2, 13.3)** | | | | |
| `synthesis` | `SynthesisThresholds` | `new SynthesisThresholds()` |  | *Synthesis.* |
| `failureSeverities` | `List<FailureSeverity>` | `(empty)` |  | Failure severities (a list; each entry is documented in its own table). |
| **Soul Integrity (design spec 14)** | | | | |
| `maxSoulIntegrity` | `float` | `100` | ≥ 1 | *Max soul integrity.* |
| `soulConditions` | `List<SoulCondition>` | `(empty)` |  | Soul conditions (a list; each entry is documented in its own table). |
| `soulDamageKinds` | `List<SoulDamageKind>` | `(empty)` |  | Sources of Soul Damage (design spec 14.1): how much of each becomes deep damage, and multipliers. |
| `soulCollapse` | `SoulCollapseRules` | `new SoulCollapseRules()` |  | What happens when Soul Integrity reaches 0. |
| **Soul recovery (design spec 15)** | | | | |
| `soulRecovery` | `SoulRecoveryBalance` | `new SoulRecoveryBalance()` |  | *Soul recovery.* |
| **Acquisition, absorption and energy (design spec 3, 5)** | | | | |
| `acquisition` | `RuneAcquisitionBalance` | `new RuneAcquisitionBalance()` |  | *Acquisition.* |
| **Concept discovery (design spec 17)** | | | | |
| `discovery` | `DiscoveryBalance` | `new DiscoveryBalance()` |  | *Discovery.* |
| **Synthesis engine (design spec 11-13)** | | | | |
| `synthesisEngine` | `SynthesisBalance` | `new SynthesisBalance()` |  | *Synthesis engine.* |
| **Spellcasting (design spec 16)** | | | | |
| `spells` | `SpellBalance` | `new SpellBalance()` |  | *Spells.* |
| **Spelling System (design spec 16.5)** | | | | |
| `spelling` | `SpellingBalance` | `new SpellingBalance()` |  | *Spelling.* |
| **Grimoire** | | | | |
| `grimoire` | `GrimoireBalance` | `new GrimoireBalance()` |  | *Grimoire.* |

## Soul balance: integrity, damage kinds, recovery

`RuneMagic/Core/Data/SoulBalance.cs`


### `SoulRecoveryBalance`

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| **Natural recovery** | | | | |
| `passivePerMinute` | `float` | `0` | ≥ 0 | Soul Integrity recovered per minute without resting or meditating. 0 = none: the soul does not heal by itself and does not return to full after a fight (design spec 15.2). |
| `passiveOnlyOutOfCombat` | `bool` | `true` |  | Natural recovery stops while in combat. |
| **Rest (sleeping)** | | | | |
| `restPerMinute` | `float` | `3` | ≥ 0 | Recovered per minute while the character sleeps (gradual), before the sleep spot's quality. |
| `restUsesSleepQuality` | `bool` | `true` |  | Rest is scaled by the sleep quality's Recovery Multiplier (the ground is worse than a bed). |
| **Meditation** | | | | |
| `meditationPerMinute` | `float` | `10` | ≥ 0 | Recovered per minute while meditating, once settled. Meditation is the efficient way to stabilize the soul. |
| `meditationSettleSeconds` | `float` | `3` | ≥ 0 | Seconds of meditation before recovery starts (settling in). Breaking off early recovers nothing. |
| `meditationAllowedInCombat` | `bool` | `false` |  | Meditation can be started while in combat. |
| `meditationAllowedWhenFull` | `bool` | `false` |  | Meditation can be started when the soul has nothing ordinary to recover. |
| `meditationLocksMovement` | `bool` | `false` |  | The character cannot move while meditating; the Meditate key ends it. Off: moving ends it. |
| `channelMoveTolerance` | `float` | `0.4` | ≥ 0 | Metres the character may be moved (by itself or pushed) before meditation or a ritual breaks. |
| `channelBrokenByDamage` | `bool` | `true` |  | Taking damage breaks meditation and rituals. |
| `channelBrokenByControl` | `bool` | `true` |  | Crowd control (stun, knockback, root...) breaks meditation and rituals. |
| `channelBrokenByAttacking` | `bool` | `true` |  | Hitting something breaks meditation and rituals. |
| **Combat** | | | | |
| `combatWindowSeconds` | `float` | `6` | ≥ 0 | Seconds after the last hit (taken or dealt) during which the character counts as in combat. |
| **Places** | | | | |
| `sanctuaryMultiplier` | `float` | `2` | ≥ 0 | Recovery multiplier of a Soul Sanctuary that does not set its own (rest and meditation recover this many times faster). |
| `sanctuaryPassivePerMinute` | `float` | `1` | ≥ 0 | Recovered per minute just by being in a sanctuary out of combat, even without resting or meditating. |
| `cityMultiplier` | `float` | `1` | ≥ 0 | The hub world's city counts as a sanctuary with this multiplier (1 = it does not). |
| **Deep damage** | | | | |
| `maxDeepDamageFraction` | `float` | `0.6` | 0 … 1 | Most of the maximum Soul Integrity that deep damage can scar (0-1). Deep damage lowers the ceiling that ordinary recovery (rest, meditation, sanctuaries) can reach; only specialized healing removes it: soul items that heal deep damage, healing rituals, sanctuaries that heal it. |
| `unknownKindDeepShare` | `float` | `0` | 0 … 1 | Deep share of damage whose kind is not in Soul Damage Kinds. |
| **Modifiers (abilities, traits, equipment)** | | | | |
| `minModifierMultiplier` | `float` | `0` | ≥ 0 | Lowest total recovery multiplier the modifiers can make. |
| `maxModifierMultiplier` | `float` | `10` | ≥ 0 | Highest total recovery multiplier the modifiers can make. |

### `SoulDamageKind`

A source of Soul Damage (design spec 14.1) and how its damage behaves. The systems that hurt the soul (synthesis, casting, the Spelling System, absorption...) name the kind; the damage itself scales with their complexity and risk.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id the systems use: synthesis, synthesis_failure, incompatible_absorption, overuse, spell_cast, spell_failure, spelling_mistake, concept_overreach, primordial, experiment (add your own). |
| `name` | `string` | `""` |  | The name the player sees. |
| `multiplier` | `float` | `1` | ≥ 0 | Multiplies the damage of this kind. |
| `deepShare` | `float` | `0` | 0 … 1 | Share of the damage that becomes deep damage (0-1), which only specialized healing removes. |
| `scaledByCondition` | `bool` | `true` |  | The current soul condition's Soul Damage Multiplier applies (a weakened soul is hurt more). |
| `description` | `string` | `""` |  | The text the player reads. |

### `SoulCollapseRules`

What a Soul Collapse (Soul Integrity reaching 0) does (design spec 14.3, decision D8).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `incapacitateSeconds` | `float` | `8` | ≥ 0 | Seconds the character is incapacitated when the soul collapses (0 = not at all). |
| `lockUntilPercent` | `float` | `20` | 0 … 100 | After a collapse, casting and synthesis stay locked until Soul Integrity is back to this percent of the maximum. |
| `losePendingEnergy` | `bool` | `true` |  | Magical energy not yet given to a rune is lost. |
| `deepDamagePercent` | `float` | `5` | 0 … 100 | Deep damage the collapse itself adds (% of the maximum Soul Integrity). |

## Drops and acquisition

`RuneMagic/Core/Data/RuneDrops.cs`


### `MonsterRarity`

How rare a monster is for rune drops (Auto = from the dungeon placement: normal, elite or boss).

| Value | Meaning |
|---|---|
| `Auto` |  |
| `Normal` |  |
| `Elite` |  |
| `Rare` |  |
| `Boss` |  |

### `RuneDropKillers`

Which kills roll rune drops.

| Value | Meaning |
|---|---|
| `RuneUsers` | Only kills by rune users (Mages): other classes never see runes they cannot use. |
| `AnyPlayer` | Kills by any player: runes drop for everyone and can be traded or sold to Mages. |

### `RuneDropEntry`

One rune a profile can drop, and how often compared with its other runes.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `runeId` | `string` | `""` |  | Rune id (wind, fire...). |
| `weight` | `float` | `1` | ≥ 0 | Relative weight among the profile's runes. |
| `minRank` | `int` | `0` | ≥ 0 | Lowest rank this entry drops at (0 = no limit). |
| `maxRank` | `int` | `0` | ≥ 0 | Highest rank this entry drops at (0 = no limit). |

### `RuneDropProfile`

The runes a kind of monster carries (design spec 3.1): matched to monsters by words of their species, name or faction and by their special properties (tags), or named by a monster's Rune Source. The highest-priority match is used; monsters nothing matches use the default profile.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id (fire_elemental, stone_golem...). |
| `displayName` | `string` | `""` |  | The name the player sees (empty: the id is shown). |
| `matchWords` | `List<string>` | `(empty)` |  | Matches monsters whose species (mob type), name or faction contains one of these words (whole words, any case). |
| `matchTags` | `List<string>` | `(empty)` |  | Matches monsters with one of these special properties (Rune Source ▸ Tags). |
| `priority` | `int` | `0` |  | When several profiles match, the highest priority wins. |
| **Drops** | | | | |
| `runes` | `List<RuneDropEntry>` | `(empty)` |  | The runes it carries (their alignments: Fire Elemental → Fire, Heat, Energy). |
| `dropChance` | `float` | `0.08` | 0 … 1 | Chance (0-1) per kill of each possible drop, before the rarity and difficulty multipliers. |
| `maxDrops` | `int` | `1` | ≥ 0 | Most runes one kill can drop (before a boss's extra drops). |
| `baseRank` | `int` | `1` | ≥ 1 | Rank of its runes before rank bonuses (dungeon difficulty, elite, boss, the monster's rank). |
| `qualityRange` | `Vector2` | `new Vector2(10, 55)` |  | Quality (0-100) of its runes before bonuses: a random value in this range. |
| `startingEnergy` | `Vector2` | `new Vector2(0, 0.1)` |  | Energy a dropped rune already holds, as a fraction (0-1) of its maximum: a random value in this range. |
| **Magical energy** | | | | |
| `energy` | `float` | `100` | ≥ 0 | Magical energy the killer gains (design spec 5.1), before multipliers. |

### `RarityWeight`

A rarity's weight on which runes drop: common runes are found more often than legendary ones.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `rarity` | `RuneRarity` |  |  | How rare it is (the enumeration's order is the rarity order). |
| `normal` | `float` | `1` | ≥ 0 | Weight against the other rarities for ordinary monsters. |
| `rareMonsters` | `float` | `1` | ≥ 0 | Weight for rare monsters and bosses (they carry unusual, powerful runes: design spec 3.1). |

### `RuneAcquisitionBalance`

How runes and magical energy are obtained, absorbed and released (design spec 3, 4.2, 5).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| **Who gets drops** | | | | |
| `dropKillers` | `RuneDropKillers` | `RuneDropKillers.RuneUsers` |  | Which kills roll rune drops. Energy always goes to the killer only, if a rune user (decision D5). |
| `defaultProfile` | `string` | `"generic"` |  | Profile of monsters no profile matches (empty = they drop nothing). |
| **Drop chance** | | | | |
| `globalChanceMultiplier` | `float` | `1` | ≥ 0 | *Global chance multiplier.* |
| `chancePerTier` | `float` | `0.1` | ≥ 0 | Each dungeon difficulty tier adds this share to the chance (0.1 = +10% per tier). |
| `eliteChanceMultiplier` | `float` | `2` | ≥ 0 | *Elite chance multiplier.* |
| `rareChanceMultiplier` | `float` | `3` | ≥ 0 | *Rare chance multiplier.* |
| `bossChanceMultiplier` | `float` | `5` | ≥ 0 | *Boss chance multiplier.* |
| `bossAlwaysDrops` | `bool` | `true` |  | A boss always drops at least one rune. |
| `bossExtraDrops` | `int` | `1` | ≥ 0 | Extra drops a boss can have above its profile's Max Drops. |
| **Which runes** | | | | |
| `rarityWeights` | `List<RarityWeight>` | `(empty)` |  | How often each rune rarity drops, for ordinary and for rare monsters / bosses. |
| `alignmentWeightMultiplier` | `float` | `3` | ≥ 0 | Runes of the monster's own magical alignment (Rune Source ▸ Alignments) weigh this many times more. |
| `alignmentExtraWeight` | `float` | `1` | ≥ 0 | Runes of the monster's alignment that its profile does not list can drop too, with this weight. |
| **Rank** | | | | |
| `rankUpChancePerTier` | `float` | `0.15` | 0 … 1 | Each dungeon difficulty tier adds this chance (0-1) of one rank more. |
| `eliteRankBonus` | `int` | `0` | ≥ 0 | *Elite rank bonus.* |
| `rareRankBonus` | `int` | `1` | ≥ 0 | *Rare rank bonus.* |
| `bossRankBonus` | `int` | `1` | ≥ 0 | *Boss rank bonus.* |
| `maxDropRank` | `int` | `5` | ≥ 1 | Highest rank a drop can have. |
| **Quality** | | | | |
| `qualityPerTier` | `float` | `4` | ≥ 0 | Quality added per dungeon difficulty tier. |
| `eliteQualityBonus` | `float` | `5` | ≥ 0 | *Elite quality bonus.* |
| `rareQualityBonus` | `float` | `10` | ≥ 0 | *Rare quality bonus.* |
| `bossQualityBonus` | `float` | `15` | ≥ 0 | *Boss quality bonus.* |
| **Magical energy from kills** | | | | |
| `energyPerTier` | `float` | `0.25` | ≥ 0 | Each dungeon difficulty tier adds this share to the energy (0.25 = +25% per tier). |
| `eliteEnergyMultiplier` | `float` | `2` | ≥ 0 | *Elite energy multiplier.* |
| `rareEnergyMultiplier` | `float` | `3` | ≥ 0 | *Rare energy multiplier.* |
| `bossEnergyMultiplier` | `float` | `6` | ≥ 0 | *Boss energy multiplier.* |
| `useZoneRewardMultiplier` | `bool` | `true` |  | The hub world zone's reward multiplier (richer far from the city) scales the energy. |
| `globalEnergyMultiplier` | `float` | `1` | ≥ 0 | *Global energy multiplier.* |
| `maxPendingEnergy` | `float` | `0` | ≥ 0 | Most magical energy that can wait unassigned (0 = no limit); more is lost. |
| **Absorption** | | | | |
| `conflictDamage` | `float` | `25` | ≥ 0 | Soul Damage per unit of alignment conflict when absorbing a rune whose alignment conflicts with runes held (kind incompatible_absorption; design spec 14.1). 0 = absorbing is always safe. |
| **Release (design spec 4.2, decision D12)** | | | | |
| `allowRelease` | `bool` | `true` |  | Runes can be released from the soul (their energy is lost). |
| `releaseInCombat` | `bool` | `false` |  | Releasing is allowed in combat. |
| **Rune items** | | | | |
| `itemWeight` | `float` | `0.1` | ≥ 0 | Weight of one rune in the inventory. |
| `itemStack` | `int` | `5` | ≥ 1 | How many identical runes stack in one slot. |
| `itemPricePerRank` | `float` | `60` | ≥ 0 | Merchant price of a rune per rank (× rarity: common 1, uncommon 2, rare 4, epic 8, legendary 16). |
| `itemsCanBeSold` | `bool` | `true` |  | Merchants buy runes. |

## Concept discovery: sources, riddles, minigame, bands

`RuneMagic/Core/Data/DiscoveryDefs.cs`


### `DiscoverySourceKind`

What kind of place a knowledge source is (flavour and filtering; they all work the same).

| Value | Meaning |
|---|---|
| `Inscription` |  |
| `Artifact` |  |
| `SealedChamber` |  |
| `Library` |  |
| `Shrine` |  |
| `Other` |  |

### `DiscoveryTierDef`

A difficulty band (design spec 17.2: Beginner, Intermediate, Advanced, Expert, Legendary). A source's tier comes from the dungeon it is in; the band sets the minigame and the riddle, and which concept rarities it can teach.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id (lowercase, no spaces): saves and network messages store it. |
| `name` | `string` | `""` |  | The name the player sees. |
| `minDungeonTier` | `int` | `0` | ≥ 0 | The lowest dungeon tier (DungeonSpawned.tier) that makes a source of this band; the highest band whose value is reached is used. |
| **Minigame (rune tracing)** | | | | |
| `minigameNodes` | `int` | `3` | 1 … 16 | Glyphs to seal, one after another. |
| `minigameSpeed` | `float` | `0.35` | ≥ 0.05 | Laps of the marker per second. |
| `nodeWidth` | `float` | `0.18` | 0.02 … 0.5 | Width of each target as a fraction of the circle (0-1); concepts of higher complexity shrink it. |
| `minScore` | `float` | `0.6` | 0 … 1 | Share of the glyphs (with partial credit for near misses) needed to pass (0-1). |
| `timeLimit` | `float` | `40` | ≥ 5 | Seconds the minigame may take. |
| `attempts` | `int` | `2` | ≥ 1 | Tries at the minigame per source and concept; failing the last one burns the source for that concept. |
| **Riddle** | | | | |
| `steps` | `int` | `1` | 1 … 5 | Questions to answer, all correctly (multi-step challenges at the advanced bands). A wrong answer ends it. |
| `options` | `int` | `4` | 2 … 8 | Answers to choose from. |
| `clues` | `int` | `3` | 1 … 6 | Clues of a generated riddle (fewer clues = harder). |
| **Which concepts** | | | | |
| `rarityWeights` | `List<float>` | `new List<float> { 1, 1, 0, 0, 0 }` |  | Relative chance of teaching a concept of each rarity (Common, Uncommon, Rare, Epic, Legendary) at this band; 0 = never. |

### `DiscoveryBalance`

The balance of concept discovery (design spec 17), part of the Rune Magic balance.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `tiers` | `List<DiscoveryTierDef>` | `(empty)` |  | The difficulty bands, easiest first. |
| `onlyRunesInSoul` | `bool` | `true` |  | Only concepts of runes the player has absorbed can be discovered (off: any rune's concepts). |
| `abandonBurns` | `bool` | `true` |  | Leaving a riddle unanswered (walking away, closing the window, running out of time) counts as a wrong answer: otherwise a player could look at the riddle and come back for another. |
| `sessionSeconds` | `float` | `240` | ≥ 30 | Seconds a whole discovery may take before it is abandoned. |
| `leaveDistance` | `float` | `6` | ≥ 0 | Metres the player may move away from the source before the discovery is abandoned (0 = no limit). |
| `complexityNarrowing` | `float` | `0.35` | 0 … 0.9 | Concepts of higher complexity (0-1) make the targets of the minigame narrower by up to this share. |
| `minPressInterval` | `float` | `0.25` | ≥ 0 | Least seconds between two seals (a hand cannot be faster). |
| `clockSlack` | `float` | `1.5` | ≥ 0 | Seconds a client's clock may run ahead of the server's before its seals are refused. |
| `avoidSeenRiddles` | `bool` | `true` |  | Riddles already seen are avoided while others remain. |
| `logLength` | `int` | `30` | 0 … 200 | How many discovery attempts the saved log keeps. |

### `DiscoverySourceDef`

A kind of knowledge source (design spec 17.1: an inscription, a magical artifact, a sealed chamber, a library). The object in the dungeon names one by id. A wrong answer burns the source definition for that concept and player (decision D7): another source of that concept has to be found.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id (ancient_inscription...). Burned sources are saved by it. |
| `displayName` | `string` | `""` |  | The name the player sees (empty: the id is shown). |
| `kind` | `DiscoverySourceKind` | `DiscoverySourceKind.Inscription` |  | The kind it belongs to (see the enumeration of the same name). |
| `description` | `string` | `""` |  | The text the player reads. |
| `runes` | `List<string>` | `(empty)` |  | Runes whose concepts it can teach (empty = any rune). |
| `concepts` | `List<string>` | `(empty)` |  | Concept ids it can teach, in addition to its runes' concepts. |
| `minTier` | `int` | `0` | ≥ 0 | Lowest difficulty band index it works at (0 = Beginner). |
| `maxTier` | `int` | `-1` |  | Highest band index (-1 = no limit). |
| `minigame` | `string` | `""` |  | The minigame (id of a registered minigame; empty = rune tracing). |
| `intro` | `string` | `""` |  | Shown when the player first approaches. |

### `RiddleDef`

A riddle written for a concept (design spec 17.3): its text, and optionally the wrong answers to offer. Without riddles a concept gets generated ones, built from what its aspects mean. Riddles must reason about magical principles, never ask for memorization.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id (wind.pressure.1). The riddles a player has seen are saved by it. |
| `concept` | `string` | `""` |  | The concept it is about (the answer): wind.pressure. |
| `text` | `string` | `""` |  | The text shown to the player. |
| `distractors` | `List<string>` | `(empty)` |  | Wrong answers to offer, as concept ids (empty = the rune's other concepts). Each must be a concept. |
| `minTier` | `int` | `0` | ≥ 0 | Lowest difficulty band index it is used at. |
| `maxTier` | `int` | `-1` |  | Highest band index (-1 = no limit). |

## Synthesis: engine balance, archetypes, flaws, failure penalties

`RuneMagic/Core/Data/SynthesisDefs.cs`


### `SynthesisMode`

What a synthesis makes.

| Value | Meaning |
|---|---|
| `Spell` | A spell (design spec 11): any number of runes; the runes stay in the soul, anchored by the spell. |
| `CompositeRune` | A new composite rune: the input runes are consumed (they free their slots), the rune takes one. |
| `RankAdvancement` | Rank advancement (design spec 6.1): exactly seven runes of the same rank become one rune of the next rank. |

### `ArchetypeMode`

Which synthesis modes an archetype can name.

| Value | Meaning |
|---|---|
| `Any` |  |
| `Spell` |  |
| `Rune` |  |

### `FlawCause`

What makes a flawed result flawed (design spec 13.1).

| Value | Meaning |
|---|---|
| `Coverage` | The concepts only partly provide what the effect needs ("insufficient Concept understanding"). |
| `Compatibility` | The concepts work against each other ("weak Concept compatibility"). |
| `Intention` | The intention only partly supports the effect. |
| `Order` | The runes are in a poor order ("poor ordering"). |
| `EnergyImbalance` | The energy was spread badly ("incorrect energy ratios", "inadequate stabilization"). |
| `EnergyExcess` | A rune was given too much energy ("excessive energy in one or more Runes"). |
| `Execution` | Poor runes, concepts beyond the runes' rank, or poor understanding. |

### `CurvePoint`

One point of a piecewise-linear curve (score 0-100 on x).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `x` | `float` |  |  | *X.* |
| `y` | `float` |  |  | *Y.* |

### `ArchetypeStage`

One stage of an archetype: a rune (and optionally a concept) used a number of times, with the energy weight each of its runes ideally gets (design spec 18: 3 Wind, 1 Ash, 3 Vibration).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `runeId` | `string` | `""` |  | Rune id (wind, ash...). For a composite result of an earlier synthesis, its rune id. |
| `conceptId` | `string` | `""` |  | Concept id the rune should use (empty = any concept of the rune). |
| `count` | `int` | `1` | ≥ 1 | How many runes of this kind the ideal formula has. |
| `energyWeight` | `float` | `1` | ≥ 0.05 | Relative energy each rune of this stage ideally gets (the ideal ratio between stages). |

### `OutcomeArchetype`

A named reference formula (design spec 18, 19): the Pyroclastic Resonance adaptation, the original combinations. It is not a recipe: a synthesis that does not match is judged by the general evaluation, and one that does match is still judged by it. A matching formula (same effect and intention, mostly the same runes) additionally takes the archetype's name, its ideal energy ratios, and a fidelity factor: the ideal counts and order score best, near variants score less (3 Wind + 1 Ash + 3 Vibration flawless, 2 + 2 + 3 flawed).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id (pyroclastic_resonance, gravity_well...). |
| `displayName` | `string` | `""` |  | The name the player sees (empty: the id is shown). |
| `description` | `string` | `""` |  | The text the player reads. |
| `intention` | `string` | `""` |  | Intention of the formula (id). |
| `effect` | `string` | `""` |  | Desired effect of the formula (id). |
| `mode` | `ArchetypeMode` | `ArchetypeMode.Any` |  | Which synthesis modes it names (Any, Spell or Rune). |
| `stages` | `List<ArchetypeStage>` | `(empty)` |  | The ideal formula, in order. |
| `orderMatters` | `bool` | `true` |  | The order of the stages is part of the ideal formula (off: any order matches). |
| `strictness` | `float` | `0.8` | 0 … 1 | Share of the score the fidelity to the ideal counts and order decides (0 = none, 1 = all). |
| `countTolerance` | `float` | `0.5` | 0.1 … 2 | Counts within this share of the ideal total still score (0.5 = a difference of half the ideal total scores 0). |
| `matchThreshold` | `float` | `0.6` | 0.1 … 1 | Share of the ideal runes a formula needs to be recognised as this archetype. |
| **Result** | | | | |
| `resultName` | `string` | `""` |  | The name of a rune made from it (empty = the display name). |
| `resultRuneId` | `string` | `""` |  | Rune id of the composite rune it makes (saves and the window use it; empty = a generated composite). |
| `resultProperties` | `string` | `""` |  | *Result properties.* |
| `resultSlotCost` | `int` | `1` | ≥ 1 | Soul slots the composite rune occupies. |
| `tags` | `List<string>` | `(empty)` |  | Free keywords for tools. |

### `SynthesisFlawDef`

A way a flawed synthesis can go wrong (design spec 13.1): reduced power, increased energy consumption, reduced stability, a result that is hard to control, unintended secondary effects. Picked by what was wrong with the formula; its strength scales with how low the score is inside the Flawed band.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id (lowercase, no spaces): saves and network messages store it. |
| `displayName` | `string` | `""` |  | The name the player sees (empty: the id is shown). |
| `description` | `string` | `""` |  | The text the player reads. |
| `cause` | `FlawCause` | `FlawCause.Coverage` |  | What makes a result carry this flaw. |
| `weight` | `float` | `1` | ≥ 0 | Chance weight among the flaws of the same cause. |
| **At full severity (the score at the bottom of the Flawed band)** | | | | |
| `powerMultiplier` | `float` | `1` | 0.1 … 1.5 | *Power multiplier.* |
| `stabilityMultiplier` | `float` | `1` | 0.1 … 1.5 | *Stability multiplier.* |
| `costMultiplier` | `float` | `1` | 0.5 … 3 | *Cost multiplier.* |
| `controlPenalty` | `float` | `0` | 0 … 1 | Added to the spell's control difficulty (harder to control; the Spelling System uses it). |
| `sideEffect` | `string` | `""` |  | An unintended secondary effect (a tag the spell compiler understands; empty = none). |

### `SynthesisBalance`

The numbers of the synthesis engine. Every value is a proposal (the spec gives rules, not numbers): the energy a formula asks for, how forgiving the optimum is, the weights of the evaluation factors, the risk, the outcome curves, the soul damage. Held by `RuneBalance`; tune it on the Rune Magic Database.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| **Runes** | | | | |
| `minRunes` | `int` | `1` | ≥ 1 | Fewest runes of a custom spell. |
| `maxRunes` | `int` | `12` | ≥ 1 | Most runes of a custom spell or composite rune. |
| `minRunesComposite` | `int` | `2` | ≥ 2 | Fewest runes of a composite rune (combining runes is how slots are freed). |
| `maxSpells` | `int` | `40` | ≥ 1 | Most spells a soul can hold (dismiss one to make another). |
| `runesMayAnchorSeveralSpells` | `bool` | `true` |  | The same rune can anchor several spells (they share its energy). Off: a rune anchors one spell. |
| `maxRuneRank` | `int` | `5` | ≥ 1 | Highest rank a composite rune can reach (a rune of the database stops at its own highest rank). |
| `logLength` | `int` | `30` | 0 … 200 | How many synthesis attempts the saved research log keeps. |
| `allowInCombat` | `bool` | `false` |  | A synthesis may be made in combat (off: like releasing a rune, it needs a moment of calm). |
| **Energy asked (a share of each rune's maximum energy, for a normal effect)** | | | | |
| `energyFractionSpell` | `float` | `0.15` | 0.01 … 1 | *Energy fraction spell.* |
| `energyFractionComposite` | `float` | `0.30` | 0.01 … 1 | *Energy fraction composite.* |
| `energyFractionAdvancement` | `float` | `0.40` | 0.01 … 1 | *Energy fraction advancement.* |
| `minEnergyShare` | `float` | `0.5` | 0 … 1 | A rune that gets less than this share of its ideal energy cannot take part: the synthesis cannot proceed (no penalty). |
| `optimalTolerance` | `float` | `0.25` | 0.02 … 0.9 | Energy within this share of the ideal is optimal (more or less is worse). |
| `maxSafeShare` | `float` | `1.75` | 1.1 … 4 | Energy above this multiple of the ideal is excessive: it interferes (overpowers the others, destabilizes). |
| `complexityNarrowing` | `float` | `0.5` | 0 … 0.9 | Complex concepts need precise control: their optimal band is narrower and their safe maximum lower by up to this share. |
| `complexityEnergy` | `float` | `0.5` | 0 … 1 | Complex concepts ask for more energy (the ideal is × (1 - this/2 + this × complexity)). |
| `irrelevantShareFloor` | `float` | `0.05` | 0 … 0.3 | The least share of the energy a rune that adds nothing to the formula is ideally given. |
| `interferenceWeight` | `float` | `0.5` | 0 … 1 | How much interference (excess energy) lowers the energy factor (0-1). |
| **Evaluation weights (design spec 12.1)** | | | | |
| `weightCoverage` | `float` | `0.28` | ≥ 0 | *Weight coverage.* |
| `weightCompatibility` | `float` | `0.14` | ≥ 0 | *Weight compatibility.* |
| `weightIntention` | `float` | `0.10` | ≥ 0 | *Weight intention.* |
| `weightOrder` | `float` | `0.16` | ≥ 0 | *Weight order.* |
| `weightEnergy` | `float` | `0.22` | ≥ 0 | *Weight energy.* |
| `weightExecution` | `float` | `0.10` | ≥ 0 | *Weight execution.* |
| `minBlend` | `float` | `0.4` | 0 … 1 | How much the weakest factor drags the score (0 = a plain weighted mean, 1 = the weakest factor decides). |
| `hardCoverageFloor` | `float` | `0.2` | 0 … 0.9 | The desired effect is not producible (the synthesis fails completely) when a requirement is met by less than this share. |
| `orderIncoherentBelow` | `float` | `0.08` | 0 … 0.5 | A fundamentally wrong sequence (order score at or below this) fails completely, when the order matters. |
| `orderMinSpread` | `float` | `0.75` | ≥ 0 | The order only matters when the runes' places in the effect's chain are spread at least this far (in stages). |
| `deadWeightRelevance` | `float` | `0.15` | 0 … 1 | A rune adding less than this share of an average rune's relevance counts as dead weight. |
| `incoherentAspectMin` | `float` | `0.5` | 0 … 1 | A pair of concepts with this much of each aspect triggers an aspect rule marked incoherent. |
| **Execution (rank, quality and understanding)** | | | | |
| `fullCreditQuality` | `float` | `55` | 1 … 100 | Average rune quality (0-100) at which quality stops costing score. |
| `fullCreditKnowledge` | `float` | `0.5` | 0.05 … 1 | Share of a rune's concepts the player knows at which understanding stops costing score. |
| `rankHarmonyLoss` | `float` | `0.25` | 0 … 0.5 | Score of the execution factor lost per rank of difference between the highest and lowest rune. |
| `conceptCapacityBase` | `float` | `0.5` | 0 … 1 | The concept complexity a rank-1 rune handles safely (more complex concepts overreach). |
| `conceptCapacityPerRank` | `float` | `0.12` | 0 … 0.5 | Complexity added to the safe capacity per rank above 1. |
| **Preview** | | | | |
| `previewFuzz` | `float` | `18` | 0 … 50 | The preview's estimate is this many points wide on each side (hides the exact score: the optimum is research). |
| `previewShowsEnergyStates` | `bool` | `false` |  | The preview tells whether each rune's energy is low, optimal or high (off: only insufficient and excessive are shown). |
| `refuseWhenSoulCouldCollapse` | `bool` | `false` |  | Refuse a synthesis that could collapse the soul (the preview always warns). |
| **Risk (design spec 13.3)** | | | | |
| `riskEffectComplexity` | `float` | `0.30` | 0 … 1 | *Risk effect complexity.* |
| `riskConceptComplexity` | `float` | `0.20` | 0 … 1 | *Risk concept complexity.* |
| `riskPrimordial` | `float` | `0.25` | 0 … 1 | *Risk primordial.* |
| `riskInterference` | `float` | `0.15` | 0 … 1 | *Risk interference.* |
| `riskRuneCount` | `float` | `0.10` | 0 … 1 | *Risk rune count.* |
| `riskHigh` | `float` | `0.5` | 0 … 1 | Risk (0-1) at which the preview calls it High; Extreme is destruction territory. |
| **Instability** | | | | |
| `riskFailureChance` | `float` | `0.10` | 0 … 1 | Chance a synthesis falters, per point of risk, on top of the soul condition's own chance. |
| `interferenceFailureChance` | `float` | `0.35` | 0 … 1 | Chance a synthesis falters per point of interference (energy beyond what the runes can hold): excessive energy can make a synthesis fail (design spec 11.5). |
| `falterMin` | `float` | `8` | ≥ 0 | A faltering synthesis loses this many points of score (random between the two). |
| `falterMax` | `float` | `25` | ≥ 0 | *Falter max.* |
| **Failure (design spec 13.3)** | | | | |
| `destroyChancePerRune` | `float` | `0.6` | 0 … 1 | Catastrophic failure: each input rune is destroyed with this chance (at least one is). |
| `incoherentScoreFactor` | `float` | `0.5` | 0 … 1 | A fundamentally incoherent formula fails whatever its score; the severity of its failure is judged by its score × this (at most just under the Flawed threshold): the worse the formula, the harsher the penalty. |
| **Outcome curves (score 0-100)** | | | | |
| `powerCurve` | `List<CurvePoint>` | `(empty)` |  | Power of the result against the score. |
| `stabilityCurve` | `List<CurvePoint>` | `(empty)` |  | Stability of the result against the score (0-1). |
| `costCurve` | `List<CurvePoint>` | `(empty)` |  | Energy cost multiplier of the result against the score (lower is better). |
| `baseSpellPower` | `float` | `100` | ≥ 1 | Power of a rank-1 spell at score 100 (the spell compiler scales its effect with it). |
| `powerPerRank` | `float` | `1.6` | ≥ 1 | Power multiplier per rank above 1. |
| `bonusPower` | `float` | `0.5` | 0 … 1 | Share of the power the synergy, supporting aspects and intention bias can add together. |
| `powerPerRune` | `float` | `0.10` | 0 … 1 | Power added per rune beyond the first (a share of the base): more runes carry more raw magic, at the price of risk and Soul Damage. |
| `maxIdealShare` | `float` | `0.9` | 0.3 … 1 | The ideal energy of a rune never exceeds this share of its maximum (the optimum must always be reachable). |
| `resultEnergyShare` | `float` | `0.10` | 0 … 1 | Energy a new rune from a rank advancement or composite starts with, as a share of its maximum, at score 100. |
| **Soul damage (design spec 14.1)** | | | | |
| `soulDamageBase` | `float` | `6` | ≥ 0 | *Soul damage base.* |
| `soulDamagePerRune` | `float` | `0.08` | ≥ 0 | Added per extra rune (a share of the base). |
| `soulDamagePerRank` | `float` | `0.25` | ≥ 0 | Added per rank above 1 (a share of the base). |
| `soulDamageAdvancement` | `float` | `2` | ≥ 0 | Rank advancement multiplies the damage. |
| `soulDamagePerfect` | `float` | `0.5` | ≥ 0 | *Soul damage perfect.* |
| `soulDamageSuccessful` | `float` | `0.9` | ≥ 0 | *Soul damage successful.* |
| `soulDamageFlawed` | `float` | `1.3` | ≥ 0 | *Soul damage flawed.* |
| `soulDamageFailure` | `float` | `1.6` | ≥ 0 | *Soul damage failure.* |
| `soulDamageRisk` | `float` | `0.5` | ≥ 0 | Risk raises the damage by up to this share. |
| `primordialDamage` | `float` | `12` | ≥ 0 | Soul Damage of kind 'primordial' per Primordial rune used. |
| `overreachDamage` | `float` | `30` | ≥ 0 | Soul Damage of kind 'concept_overreach' per point of complexity a concept exceeds its rune's rank. |

## Spells: forms, balance (cost, strain, casting)

`RuneMagic/Core/Data/SpellDefs.cs`


### `SpellFormDef`

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `id` | `string` | `""` |  | Stable id (lowercase): 'bolt', 'wall'... It becomes part of the spell's ability id (rune::). |
| `displayName` | `string` | `""` |  | The name the player sees (empty: the id is shown). |
| `description` | `string` | `""` |  | The text the player reads. |
| `preset` | `string` | `""` |  | The ability preset it is built from (a name of Assets > Create > SimpleMovements > Abilities > Ability From Preset, e.g. 'Firebolt (Projectile)'). Empty = an empty self cast (no game effect: costs and strain only). A designer can assign a template ability to the form on the Rune Magic Database component instead. |
| `effects` | `List<string>` | `(empty)` |  | The desired effects (ids) this form serves. |
| `fallbackFor` | `List<EffectCategory>` | `(empty)` |  | Effects not listed by any form use the first form of their category listed here. |
| `canMoveWhileCasting` | `bool` | `false` |  | The caster may move (slowly) while casting (spec 16.1 'canMoveWhileCasting'). |
| `energyMultiplier` | `float` | `1` | ≥ 0 | Multiplies the Rune Energy cost of spells of this form. |
| `strainMultiplier` | `float` | `1` | ≥ 0 | Multiplies the Soul Strain of spells of this form (0 = this form never strains the soul). |
| `rangeMultiplier` | `float` | `1` | ≥ 0.1 | *Range multiplier.* |
| `areaMultiplier` | `float` | `1` | ≥ 0.1 | *Area multiplier.* |
| `chargeable` | `bool` | `false` |  | Spells of this form can be charged by holding their key (see Spells ▸ Charge), unless they use the Spelling System. |
| `tags` | `List<string>` | `(empty)` |  | Free keywords for tools and rules. |

### `SpellBalance`

The numbers of spellcasting (design spec 16.1, 16.2, 14.1): what a cast costs in Rune Energy and Soul Strain, how the spell's stats become cast time, power and reach, how stability becomes the chance of a misfire. Held by `RuneBalance`; tune it on the Rune Magic Database.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| **Spell slots (spec 16.4)** | | | | |
| `replaceOtherAbilities` | `bool` | `false` |  | A spell may be put on an ability slot that holds another (non-spell) ability and replaces it (it is not given back when the spell is taken off). Off: only empty slots and slots that hold spells take spells. |
| **Rune Energy cost (spec 16.1)** | | | | |
| `castEnergyFraction` | `float` | `0.02` | 0.001 … 1 | A cast draws this share of the maximum energy of a rune of the spell's rank (× (0.5 + complexity) × the spell's cost multiplier and the soul condition's): the spec's worked example, a powerful rank 3 spell of complexity 0.5, draws 2% of 25,000 = 500. |
| `minEnergyCost` | `float` | `1` | ≥ 0 | The least energy one cast draws. |
| **Soul Strain (spec 14.1, 16.3)** | | | | |
| `strainBase` | `float` | `4` | ≥ 0 | Soul Damage a successful cast causes at rank 1 and complexity 0.5 (0 = casting never strains the soul). With the per-rank share below, the spec's worked example holds: a powerful rank 3 spell (complexity 0.5) causes 12 Soul Damage, and two of them in a row leave the soul Strained. |
| `strainPerRank` | `float` | `1` | ≥ 0 | Added per rank above 1 (a share of the base): 1 = the strain grows by the base for each rank (4, 8, 12, 16, 20). |
| `strainOnSuccess` | `bool` | `true` |  | Casting strains the soul (off: spells are free of Soul Damage; only synthesis and failures strain). |
| `strainKind` | `string` | `"spell_cast"` |  | The Soul Damage Kind of a successful cast (see Soul Damage Kinds: its multiplier, deep share and whether the soul's condition scales it apply). Set the kind's Multiplier to 0 to make casting free of Soul Damage. |
| `failureKind` | `string` | `"spell_failure"` |  | The Soul Damage Kind of a failed cast (the built-in 'spell_failure' kind sends a small share of the damage to deep damage). |
| `failureDamageFactor` | `float` | `1.5` | ≥ 0 | A failed cast (a misfire) causes this multiple of the cast's strain (0 = none). |
| `damageOnInterrupt` | `bool` | `false` |  | A cast interrupted by an enemy also causes the failure damage (off: an interrupted cast is only refunded). |
| **Casting time, cooldown and power** | | | | |
| `castTimeBase` | `float` | `0.8` | ≥ 0.1 | Cast time multiplier at complexity 0 (the form's own time is multiplied by this + complexity × the next value). |
| `castTimeComplexity` | `float` | `0.6` | ≥ 0 | *Cast time complexity.* |
| `minCastTime` | `float` | `0.3` | ≥ 0 | An instant form still takes at least this long to cast (a spell is shaped, not clicked). |
| `cooldownBase` | `float` | `0.6` | ≥ 0.1 | Cooldown multiplier at complexity 0 (+ complexity × the next value). |
| `cooldownComplexity` | `float` | `0.8` | ≥ 0 | *Cooldown complexity.* |
| `referencePower` | `float` | `100` | ≥ 1 | The power (spell stat) at which the form's own damage is dealt: damage × power / this. Synthesis power is about 100 × 1.3 for a perfect rank 1 spell, before bonuses. |
| `castMoveSpeed` | `float` | `0.5` | 0 … 1 | Speed multiplier while casting for a form that lets the caster move. |
| **Reach (spec 16.1 range, area, duration)** | | | | |
| `rangePerRank` | `float` | `0.05` | ≥ 0 | *Range per rank.* |
| `areaPerRank` | `float` | `0.08` | ≥ 0 | *Area per rank.* |
| `durationBase` | `float` | `0.8` | ≥ 0.1 | Duration multiplier at stability 0 (+ stability/100 × the next value). |
| `durationStability` | `float` | `0.4` | ≥ 0 | *Duration stability.* |
| **Stability (spec 16.1) and misfires** | | | | |
| `instabilityScale` | `float` | `0.5` | 0 … 1 | The chance a cast misfires is (1 - stability/100) × this, plus the soul condition's own. |
| `fizzleMin` | `float` | `0.5` | 0.05 … 1 | A misfire cuts the cast off between these shares of its cast time. |
| `fizzleMax` | `float` | `0.9` | 0.05 … 1 | *Fizzle max.* |
| `misfireKeepsEnergy` | `bool` | `true` |  | A misfired cast keeps the energy it drew (off: it is refunded). |
| **Charging (hold the key)** | | | | |
| `charge` | `ChargeProfile` | `new ChargeProfile()` |  | How a chargeable spell (a form with Chargeable on) is charged by holding its key: the longest hold, what a full charge multiplies, and what a short tap does. Its Cost At Full also scales the Rune Energy a cast draws. Spells that use the Spelling System are not charged. |
| `chargeStrainAtFull` | `float` | `1.5` | ≥ 0 | A full charge multiplies the Soul Strain of the cast by this (a partial charge in between). |
| `chargeInstabilityAtFull` | `float` | `0.12` | 0 … 1 | A full charge adds this to the chance the cast misfires (a partial charge in between): a held spell is harder to keep stable. |

## The Spelling System: balance, rules, models

`RuneMagic/Core/Data/SpellingDefs.cs`


### `SpellingMode`

Which spells use the Spelling System (the developer's / server's choice, never the player's).

| Value | Meaning |
|---|---|
| `None` | No spell uses it: casting works like a normal ability. |
| `Selected` | Only spells marked by a rule use it. |
| `All` | Every spell must use it. |

### `SpellingFailureModel`

What a wrong, missed or late key does (design spec 16.5 failure models).

| Value | Meaning |
|---|---|
| `Strict` | One mistake fails the spell. |
| `Lenient` | Each mistake costs power, stability and time; too many still fail it. |

### `SpellingDamageMode`

When mistakes cost Soul Damage (design spec 16.5: configurable per spell, never automatic).

| Value | Meaning |
|---|---|
| `None` |  |
| `FlatPerMistake` | The same amount for every mistake. |
| `ScalingPerMistake` | Each further mistake costs more than the one before. |
| `OnceOnFailure` | One amount, only when the sequence fails. |

### `SpellingUse`

A rule's choice about using the Spelling System.

| Value | Meaning |
|---|---|
| `Inherit` |  |
| `Yes` |  |
| `No` |  |

### `SpellingFailureChoice`

A rule's choice of failure model.

| Value | Meaning |
|---|---|
| `Inherit` |  |
| `Strict` |  |
| `Lenient` |  |

### `SpellingDamageChoice`

A rule's choice of when mistakes cost Soul Damage.

| Value | Meaning |
|---|---|
| `Inherit` |  |
| `None` |  |
| `FlatPerMistake` |  |
| `ScalingPerMistake` |  |
| `OnceOnFailure` |  |

### `SpellingScope`

What a spelling rule applies to.

| Value | Meaning |
|---|---|
| `Form` | A spell form (`SpellFormDef`). |
| `Effect` | A desired effect. |
| `Archetype` | An outcome archetype (a recognised formula such as Pyroclastic Resonance). |

### `SpellingWeight`

How much one evaluation term counts in the performance score (see `SpellingEvaluator.Terms`).

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `term` | `string` | `""` |  | The term's id: accuracy, speed, precision, rhythm (more can be registered in code). |
| `weight` | `float` | `1` | ≥ 0 | *Weight.* |

### `SpellingRule`

Marks spells that use the Spelling System and tunes how hard it is for them: the data-table form of the spec's per-spell flag and parameters (spells are synthesized, so they are marked by what they are: their form, their desired effect or the archetype they were recognised as). The most specific rule wins field by field: archetype, then effect, then form. A negative number (or Inherit) leaves the value to the generated one.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| `scope` | `SpellingScope` | `SpellingScope.Form` |  | *Scope.* |
| `id` | `string` | `""` |  | The id of the form, desired effect or archetype this rule applies to. |
| `use` | `SpellingUse` | `SpellingUse.Inherit` |  | Selected mode: Yes makes these spells use the Spelling System (All mode: every spell does, None mode: none). |
| **Difficulty (−1 = generated from the spell's rank, power and complexity)** | | | | |
| `letters` | `int` | `-1` |  | Exactly this many letters (before the soul's condition and the pool allow fewer or more). |
| `lettersDelta` | `int` | `0` |  | Added to the generated number of letters. |
| `keyWindow` | `float` | `-1` |  | The time window of every key, in seconds. |
| `keyWindows` | `List<float>` | `(empty)` |  | One time window per key, in seconds (key 1, key 2...): different windows for different keys. Empty = generated. |
| `windowMultiplier` | `float` | `1` | ≥ 0.1 | Multiplies every window (a rule can make its spells easier or harder than their numbers say). |
| `totalLimit` | `float` | `-1` |  | Total time limit for the sequence, in seconds (−1 = a share of the windows' sum). |
| **Rhythm** | | | | |
| `rhythm` | `SpellingUse` | `SpellingUse.Inherit` |  | *Rhythm.* |
| `rhythmTempo` | `float` | `-1` |  | Seconds between beats (−1 = the balance's). |
| `rhythmTolerance` | `float` | `-1` |  | How far from a beat a key may be pressed, in seconds (−1 = the balance's). |
| **Evaluation** | | | | |
| `failureModel` | `SpellingFailureChoice` | `SpellingFailureChoice.Inherit` |  | *Failure model.* |
| `maxMistakes` | `int` | `-1` |  | Lenient: mistakes beyond this many fail the spell (−1 = generated). |
| `speedBonusThreshold` | `float` | `-1` |  | Finishing within this share of the time limit earns the speed bonus (−1 = the balance's). |
| **Soul Damage on mistakes (never automatic)** | | | | |
| `damageMode` | `SpellingDamageChoice` | `SpellingDamageChoice.Inherit` |  | *Damage mode.* |
| `damageAmount` | `float` | `-1` |  | Soul Damage per mistake (flat or scaling) or once (on failure); −1 = the balance's. |
| `tags` | `List<string>` | `(empty)` |  | Free keywords for tools and rules. |

### `SpellingBalance`

The numbers of the Spelling System (design spec 16.5): the global mode, which keys may be used, how a spell's difficulty is generated from its rank, power and complexity, how the sequence is scored, what mistakes cost and what the server accepts. Held by `RuneBalance`; tune it on the Rune Magic Database.

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| **Who uses it (set by the developer / server, never by the player)** | | | | |
| `mode` | `SpellingMode` | `SpellingMode.Selected` |  | None: no spell uses the Spelling System. Selected: only spells a rule marks. All: every spell does. |
| `failureModel` | `SpellingFailureModel` | `SpellingFailureModel.Lenient` |  | The default failure model (a rule can choose its own). |
| `skipWithoutKeyboard` | `bool` | `true` |  | A player without a keyboard (a gamepad only) can still cast spelled spells: the Spelling System needs letter keys, so the cast is made as if the sequence had been played with the most mistakes the spell allows (weaker and slower, no bonus, no Soul Damage for the mistakes). Off: the server refuses spelled spells to such players. |
| **Keys (names of the Input System's Key enum: 'A', 'Digit1', 'Space'...)** | | | | |
| `candidateKeys` | `List<string>` | `(empty)` |  | The keys a sequence may ask for. |
| `reservedKeys` | `List<string>` | `(empty)` |  | Keys the game reads outside the Input System's actions (digits, quick slots, Y/N prompts, T, F-keys, Esc, Tab, Enter, Space): never asked for. |
| `movementActions` | `List<string>` | `(empty)` |  | Actions whose keys are the movement keys (their keys, as the player has bound them now, are never used while the caster can move). Any action with a keyboard composite (2D Vector) counts as well. |
| `excludeGameplayKeys` | `bool` | `true` |  | Never ask for a key that any gameplay action uses (ability keys, interact, inventory...): pressing it would do that too. |
| `allowMovementKeysWhenRooted` | `bool` | `true` |  | A spell that roots the caster may ask for movement keys (never for ability keys). |
| `minPoolSize` | `int` | `3` | ≥ 2 | With fewer keys than this the Spelling System is skipped (a warning says why). |
| **Letters (spec: scaled by rank, power and concept complexity)** | | | | |
| `baseLetters` | `int` | `3` | ≥ 1 | *Base letters.* |
| `lettersPerRank` | `float` | `0.5` | ≥ 0 | Added per spell rank above 1. |
| `lettersPerPower` | `float` | `1` | ≥ 0 | Added per +100% of power above the reference power of the Spell Balance. |
| `lettersPerComplexity` | `float` | `2` | ≥ 0 | Added at complexity 1 (× the spell's complexity 0-1). |
| `minLetters` | `int` | `2` | ≥ 1 | *Min letters.* |
| `maxLetters` | `int` | `10` | ≥ 1 | *Max letters.* |
| **Time (seconds)** | | | | |
| `baseKeyWindow` | `float` | `1.6` | ≥ 0.2 | *Base key window.* |
| `windowPerComplexity` | `float` | `0.6` | ≥ 0 | Taken off each window at complexity 1 (× the spell's complexity). |
| `minKeyWindow` | `float` | `0.5` | ≥ 0.1 | *Min key window.* |
| `firstKeyExtra` | `float` | `0.5` | ≥ 0 | Added to the first key's window (reading the letters takes a moment). |
| `windowVariation` | `float` | `0.25` | 0 … 0.9 | How much the generated windows differ from key to key (0 = all alike, 0.25 = ±25%). |
| `totalLimitFactor` | `float` | `0.9` | 0.2 … 3 | The total time limit as a share of the sum of the windows (1 = every key may take all of its window). |
| **Rhythm (when a rule asks for it)** | | | | |
| `rhythmTempo` | `float` | `0.6` | ≥ 0.1 | *Rhythm tempo.* |
| `rhythmTolerance` | `float` | `0.25` | ≥ 0.02 | *Rhythm tolerance.* |
| **Score (accuracy, speed, precision, rhythm)** | | | | |
| `weights` | `List<SpellingWeight>` | `(empty)` |  | Weights (a list; each entry is documented in its own table). |
| `speedBonusThreshold` | `float` | `0.55` | 0.05 … 1 | Finishing within this share of the time limit (and with few enough mistakes) earns the speed bonus. |
| `speedBonusMaxMistakes` | `int` | `0` | ≥ 0 | Most mistakes a sequence may have and still earn the speed bonus. |
| `speedBonusPower` | `float` | `0.1` | ≥ 0 | Speed bonus: more power (0.1 = +10%), lower energy cost (0.1 = −10%), less Soul Strain (0.2 = −20%) and more stability (points). |
| `speedBonusCost` | `float` | `0.1` | 0 … 0.9 | *Speed bonus cost.* |
| `speedBonusStrain` | `float` | `0.2` | 0 … 1 | *Speed bonus strain.* |
| `speedBonusStability` | `float` | `5` | ≥ 0 | *Speed bonus stability.* |
| `powerSwing` | `float` | `0.1` | 0 … 0.9 | The performance score moves the spell's power between 1 − this and 1 + this. |
| **Mistakes (a missed, wrong or late key; with rhythm also a key off the beat)** | | | | |
| `powerPerMistake` | `float` | `0.06` | 0 … 0.5 | Lenient: power lost per mistake (0.06 = −6%). |
| `castTimePerMistake` | `float` | `0.1` | ≥ 0 | Lenient: cast time added per mistake (0.1 = +10%). |
| `stabilityPerMistake` | `float` | `5` | ≥ 0 | Lenient: stability lost per mistake (points; stability decides the misfire chance). |
| `mistakesPerLetterShare` | `float` | `0.34` | ≥ 0 | Lenient: mistakes beyond this fail the spell: 1 + this share of the letters (rounded down), unless a rule says. |
| **Failure** | | | | |
| `energyLossOnFailure` | `float` | `0.5` | 0 … 1 | A spell that fails its sequence loses this share of the Rune Energy a cast would have drawn (0 = none). |
| `failureLockSeconds` | `float` | `1` | ≥ 0 | Seconds before the same spell may be spelled again after a failed sequence. |
| `failureDamageFactor` | `float` | `1` | ≥ 0 | A failed sequence also causes the spell's misfire damage of the Spell Balance times this (0 = none). |
| **Soul Damage on mistakes (the default: none; a rule may set it per spell)** | | | | |
| `damageMode` | `SpellingDamageMode` | `SpellingDamageMode.None` |  | *Damage mode.* |
| `damageAmount` | `float` | `2` | ≥ 0 | *Damage amount.* |
| `damageScaling` | `float` | `0.5` | ≥ 0 | Scaling mode: each further mistake costs this much more than the one before (0.5 = +50%). |
| `damageKind` | `string` | `"spelling_mistake"` |  | The Soul Damage Kind of Soul Damage from mistakes. |
| **Forgiveness (concept knowledge and rune quality make it easier)** | | | | |
| `knowledgeForgiveness` | `float` | `0.15` | 0 … 1 | Easier by this much (0-1) when every concept of the spell's runes is known. |
| `qualityForgiveness` | `float` | `0.10` | 0 … 1 | Easier by this much (0-1) at synthesis quality 100. |
| `forgivenessWindow` | `float` | `1` | ≥ 0 | Forgiveness 1 widens every window by this share and allows up to this many more mistakes. |
| `forgivenessMistakes` | `int` | `2` | ≥ 0 | *Forgiveness mistakes.* |
| **What the server accepts** | | | | |
| `minPressInterval` | `float` | `0.07` | ≥ 0 | Keys pressed closer together than this are not human (seconds). |
| `latencyAllowance` | `float` | `1` | ≥ 0 | Seconds of latency and clock error the server forgives when it checks that a sequence fits the real time that passed. |
| `maxReportLag` | `float` | `3` | ≥ 1 | The most seconds between the end of a sequence (as its machine says) and the server receiving its result: the result is sent at once, so a result that waits can only be hiding the times it reports. Covers the network and the update interval. |
| `answerGraceSeconds` | `float` | `4` | ≥ 0 | Seconds after its time limit a sequence may still be answered. |
| `readySeconds` | `float` | `8` | ≥ 1 | A successful sequence stays good for this long: the cast must start within it. |
| `maxPressesPerLetter` | `int` | `4` | ≥ 1 | Most key presses (right and wrong) a sequence may report, per letter. |
| **Presentation** | | | | |
| `soundCues` | `bool` | `true` |  | Short tones for right and wrong keys (in addition to the shapes and text). |

## The Grimoire: balance

`RuneMagic/Core/Data/GrimoireDefs.cs`


### `GrimoireBalance`

| Field | Type | Default | Range | Meaning |
|---|---|---|---|---|
| **Who has one** | | | | |
| `giveOnSpawn` | `bool` | `true` |  | A new Mage begins with a grimoire in their inventory (the soul remembers it was given). Off: grimoires only come from items you give out yourself (id 'grimoire::', see the guide). |
| `reissueIfMissing` | `bool` | `true` |  | A Mage who enters the game holding none of their own grimoires gets it again, with its pages (the inventory may not be kept between sessions in your game). While another player holds it too, both copies show the same pages. |
| **Pages** | | | | |
| `pages` | `int` | `10` | ≥ 1 | Pages of a new grimoire: how many prints it can hold at once. |
| **Imprinting (a rune of the soul is copied on a page)** | | | | |
| `imprintEnergyFraction` | `float` | `0.25` | 0.001 … 1 | What an imprint costs the rune of the soul, as a share of that rune's maximum energy (it must have that much energy to be imprinted). |
| `minImprintCost` | `float` | `10` | ≥ 0 | The least energy an imprint costs. |
| `allowComposite` | `bool` | `true` |  | Runes made by synthesis can be imprinted too (their profile is copied with them). |
| `onlyAuthorImprints` | `bool` | `true` |  | Only the Mage who made the grimoire (its author) writes in it; a Mage who holds it only reads it and absorbs from it. |
| `allowErase` | `bool` | `true` |  | The author may erase a print from a page (nothing is given back). |
| **Absorbing a print** | | | | |
| `absorbedEnergyFraction` | `float` | `0` | 0 … 1 | The energy an absorbed print holds, as a share of its maximum (0 = none: the spec of this feature). |
| `pageFreeAfterAbsorb` | `bool` | `true` |  | A page whose print was absorbed is blank again and can be written once more. Off: the page is spent for good. |
| **Against duplication and abuse (0 = no limit)** | | | | |
| `imprintCooldownSeconds` | `float` | `3` | ≥ 0 | Seconds a Mage must wait between two imprints. |
| `absorbCooldownSeconds` | `float` | `2` | ≥ 0 | Seconds a Mage must wait between two absorbs. |
| `maxImprintsPerHour` | `int` | `40` | ≥ 0 | Most imprints one Mage may make in an hour. |
| `maxAbsorbsPerHour` | `int` | `40` | ≥ 0 | Most prints one Mage may absorb in an hour. |
| `logEntries` | `int` | `200` | ≥ 0 | How many actions (imprints, absorbs, erasures and refusals) each book remembers; the audit log shows who did what. |
| `logToConsole` | `bool` | `false` |  | Every action is also written to the Console (the server's log). |
| **The book as an item** | | | | |
| `bookName` | `string` | `"Grimoire"` |  | The name of the item ('Grimoire of '). |
| `itemWeight` | `float` | `1` | ≥ 0 | *Item weight.* |
| `itemPrice` | `float` | `0` | ≥ 0 | *Item price.* |
| `canBeSold` | `bool` | `false` |  | A merchant buys it (its prints would be sold with it). |

---

*63 types, 514 fields listed. A meaning in italics has no tooltip in the source: it is the field's name read as words.*
