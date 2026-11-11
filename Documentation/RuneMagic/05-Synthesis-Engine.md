# Rune Magic 05 — The synthesis engine

A **synthesis** combines runes into something new (design spec 6.1, 11–13, 18, 19). This phase is the **engine**: the rules that decide whether a combination works, how well, and what it costs. It has no screens of its own: the window that drives it (choosing runes, concepts and energy) is described in [06 — The synthesis window](06-Synthesis-UI.md), and casting the spells is Phase 7. You can also try the engine in the **Synthesis Lab** (editor window), the **debug panel** (play mode) and the tests.

**What a synthesis makes:**

| Mode | Runes | Result |
|---|---|---|
| **Spell** | 1 to *Max Runes* (12) | A **spell**. The runes **stay in the soul** and *anchor* the spell (decision D3). |
| **Composite rune** | 2 to *Max Runes* | A **new rune** with its own name, essence and slot cost. The input runes are **used up** and free their slots. |
| **Rank advancement** | **exactly seven**, all the **same rank** | One rune of the **next rank**. Seven of the *same* rune give that rune one rank higher; seven different runes give a **composite** rune of the next rank. |

Everything that tunes it is data in the *Rune Magic Database*: **Data ▸ Balance ▸ Synthesis Engine**, **Data ▸ Archetypes**, **Data ▸ Synthesis Flaws**. The numbers are **proposals**; the spec gives rules, not numbers.

## What the player chooses (design spec 11.1)

An **Intention** (the purpose: Attack, Defense, Control, Creation…), a **Desired Effect** (the result: Explosion, Pierce, Barrier…), and the runes **in order**, each with a **concept** the player has *discovered* of that rune, and the **energy** it gives. Nothing else. There is no recipe book.

A rank advancement of seven runes of the same kind needs no formula: only energy.

## How a formula is judged (design spec 11, 12)

The engine reads the **aspects** of the chosen concepts (every concept is described by weighted aspects: pressure, combustion, wave…) and gives six **factors**, each 0–1. The **Synthesis Quality Score** (0–100%) blends them, and the weakest factor drags it down.

| Factor | Weight | Question |
|---|---|---|
| **Coverage** | 28% | Do the concepts together provide what the effect *requires*? |
| **Energy** | 22% | Does each rune get about what its place asks, and nothing overpowers the rest? |
| **Order** | 16% | Do the runes act in the sequence the effect's chain asks for? |
| **Compatibility** | 14% | Do the concepts and the runes' alignments work together, or against each other? Runes that add nothing are noise. |
| **Intention** | 10% | Does the purpose support the effect? |
| **Execution** | 10% | Rune quality, rank harmony, concepts within the runes' rank, how much of the rune the player understands. |

Score = 60% weighted mean + 40% the weakest of the first five. **Perfect (100%) needs every factor at 1**; bonuses (synergy, supporting aspects, intention bias) feed the *power* of the result, not the score.

### Tiers (design spec 12.2)

| Score | Tier | What happens |
|---|---|---|
| 100% | **Perfect** | The best spell or rune; no flaws; the least Soul Damage. |
| 70–99% | **Successful** | Works as intended, a little weaker the lower the score. |
| 30–69% | **Flawed** | A spell or rune **with flaws**. |
| below 30% | **Complete failure** | Nothing is made; penalties (below). |

### Order (design spec 11.3)

Each concept acts at some point in the effect's **ideal chain** (for an explosion: compress → ignite → release). A rune's *place* in the chain comes from its aspects; the order score is how far the sequence is from that, in proportion: swapping two runes that act far apart costs a lot, swapping two that act together costs nothing. A sequence that is fully **reversed** is fundamentally wrong (see incoherent formulas).

### Energy (design spec 11.4, 11.5)

Each rune has an **ideal** energy that depends on the effect's *power demand*, the concept's *complexity*, the rune's *rank* and how much that rune matters to the formula (the **ratios** between runes matter, not only the amounts). The player never sees the ideal.

| Given | State | Result |
|---|---|---|
| below 50% of the ideal | **Insufficient** | The synthesis **cannot proceed**: nothing is spent, nothing is risked. |
| 50% to the optimal band | Low | Weaker, less stable. |
| about ±25% of the ideal (narrower for complex concepts) | **Optimal** | Full credit: the optimum is a band, not a point. |
| up to about 1.75× | High | A little worse. |
| beyond | **Excessive** | The rune **interferes**: it overpowers the others and destabilizes the synthesis; the chance that the magic *falters* grows. |

### Incoherent formulas: fail whatever the energy (design spec 13.2)

Some formulas can never work, and fail **completely with penalties** however well the rest is done:

- an **incoherent** rule (data: *incoherent* on a compatibility rule) between concepts, aspects or alignments;
- an intention that **does not support** the effect at all;
- a requirement of the effect that **nothing provides** (an explosion with nothing that burns);
- a sequence **fully reversed** from the effect's chain.

The player is told *which* kind of problem it was (research), never what would have worked.

**Insufficient energy is not an invalid formula.** It is checked *first*, costs nothing and reveals nothing about whether the formula is valid.

## Not recipes (design spec 18, 19)

The built-in **archetypes** are *named reference formulas*: Pyroclastic Resonance (3 Wind, 1 Ash, 3 Vibration) and the ten original combinations of spec 19. An archetype never decides whether a formula works. When a formula **resembles** one (same intention and effect, most of the same runes) it additionally:

- takes the archetype's **name** (and, for a rune, its identity);
- uses its **ideal energy ratios**;
- is scored for **fidelity**: the ideal counts and order score best, near variants score less.

So 3 Wind + 1 Ash + 3 Vibration at the right energies is a flawless Pyroclastic Resonance, 3 + 2 + 3 is a good one, 2 + 2 + 3 is **flawed**, and *Wind + Ash + Vibration* (one of each) is a different, equally sound explosion with a generated name. Remove every archetype from the database and the engine still judges every formula the same way (a test checks this).

| Archetype | Intention / effect | Runes (in the order the effect's chain wants) |
|---|---|---|
| Pyroclastic Resonance | Attack / Explosion | 3 Wind (pressure), 1 Ash (combustion), 3 Vibration (resonance) |
| Gravity Well | Control / Attract | Gravity, Matter, Space |
| Storm Lance | Attack / Pierce | Lightning, Wind, Momentum |
| Crystal Bastion | Defense / Barrier | Earth, Crystal, Armor |
| Soul Chain | Control / Bind | Soul, Shadow, Binding |
| Temporal Echo | Creation / Duplicate | Space, Time, Vibration |
| Void Collapse | Destruction / Implode | Null, Space, Gravity |
| Living Forge | Creation / Forge | Matter, Metal, Fire |
| Eternal Bloom | Creation / Grow | Plant, Restoration, Growth |
| Astral Ruin | Attack / Crush | Fire, Metal, Gravity, Space |
| Fractured Reality | Movement / Warp | Space, Shift, Matter |

Their ideal formulas score 100% (Pyroclastic, Storm Lance, Crystal Bastion, Living Forge, Eternal Bloom, Gravity Well) or 99.6–99.9% (the others use concepts too complex for a **rank 1** rune: *concept overreach*, which a higher-rank rune avoids).

## Outcomes

### Perfect, Successful, Flawed

The **energy given is used up** (in every mode). A spell or rune is made, with **power, stability, an energy cost multiplier and quality** from the score (curves in the balance: Perfect 100% power ×1.3, 70% ×0.85, 30% ×0.45), the rune ranks, the effect, bonuses, and the number of runes (more runes carry more raw magic, with diminishing returns). The soul's condition scales the stability.

A **Flawed** result carries one to three **flaws** (design spec 13.1): the factors that held the score back choose the causes, the depth in the Flawed band chooses how many (top third: one; bottom third: three) and how hard they bite.

| Flaw | Cause | Effect |
|---|---|---|
| Thin, Hollow | Coverage (too little of what the effect needs) | less power |
| Discordant | Compatibility | less stable, harder to control, *backlash* |
| Misdirected | Intention | less power, harder to control, *drift* |
| Disordered | Order | less stable, costs more energy |
| Wasteful, Lopsided | Energy spread badly | costs more energy, less stable |
| Volatile | Excessive energy | much less stable, *unstable release* |
| Crude | Execution (poor runes, concepts beyond the rank) | less power and stability |

A flawed rune keeps its flaws (shown in the Rune Soul window). The *side effects* and *control penalty* of a spell are data for the casting phase.

### Complete failure (design spec 13.3)

Nothing is made. The **severity** comes from the score *and the risk*, with a near miss punished less:

| Severity | From score | Energy lost (of the energy given) | Runes destroyed |
|---|---|---|---|
| Minor | 20% | 25% | no |
| Moderate | 10% | 60% | no |
| Severe | 0% | 100% | no |
| Catastrophic | 0% (and risk 0.7+) | 100% | **yes**: each input rune with chance 0.6, **at least one** |

An **incoherent** formula is judged at its score × 0.5 (and below the Flawed threshold), so the worse the formula the harsher the penalty. Runes that **anchor a spell** and are destroyed take the spell with them.

## Spells anchor their runes (decision D3)

A spell keeps the runes it was made from in the soul and draws their energy when cast (Phase 7). While a rune anchors a spell it **cannot be released or used up** (rank advancement, composite): *dismiss the spell first* (the runes keep their energy). The same rune can anchor several spells (data: *Runes May Anchor Several Spells*). A soul holds up to *Max Spells* (40).

## Composite runes (design spec 6.1, 18)

A rune made by synthesis is not in the database, so it carries what it is:

- its **name** (the archetype's, the player's, or one made from its runes: *Wind-Vibration Explosion*);
- its **alignments** (those of its ingredients);
- its **essence**: the aspects of its concepts, saturating below 2 and weaker for a worse synthesis. A later synthesis can use it as the concept **`@essence`**;
- its **slot cost** (the archetype's, else 1), the effect and intention it was made for, its **ingredients** and score.

A new rune starts with **10% of its maximum energy × the score**: energy is separate from rank and quality (directive 7). Seven identical composite runes (the same archetype) advance together into the same composite one rank higher.

## Risk, faltering and the soul (design spec 13.3, 14)

**Risk** (0–1) comes from the effect's complexity, the concepts' complexity (and overreach), Primordial runes, interference and the number of runes (+ rank advancement). Levels: Low, Moderate, High, and **Extreme** when runes can be destroyed.

A synthesis **falters** with a chance from the soul's condition, the risk and the interference: the score drops by 8–25 points. This is the only chance in the engine, rolled from a seed the **server** chooses.

The soul's condition can **forbid** a synthesis (collapse lock; condition not allowing it), cap the **runes** or the **effect complexity**, add to the chance of faltering, and scale the stability (design spec 14.4). Not in combat by default (*Allow In Combat*), and only for **rune users**.

## Soul Damage (design spec 14.1)

The engine **plans** the damage; the soul applies it (kind multipliers and the condition's multiplier included). Applying it stops at a **collapse**.

| Kind | When | Amount |
|---|---|---|
| `synthesis` | any successful or flawed synthesis | base 6 × runes (+8% each beyond the first) × rank (+25% each above 1) × effect complexity × **×2 for a rank advancement** × tier (Perfect 0.5, Successful 0.9, Flawed 1.3) × risk (up to +50%) |
| `synthesis_failure` | a failure (instead of `synthesis`) | the same × 1.6 |
| `primordial` | per Primordial rune | 12 (×1.5 in a failure) |
| `concept_overreach` | per point of complexity a concept exceeds its rune's rank | 30 |
| `experiment` | risk above High | up to half the base |

## The preview (design spec 11.1, step 7)

Before confirming, the player may know: whether it **can proceed**, the **least energy each rune needs**, what each holds and was given, a **fuzzy estimate** of the score (a range ±18 points whose middle is *not* the score; the same request always previews the same), its tiers, the **risk level**, whether runes could be destroyed, the **Soul Damage range**, and whether the soul **could collapse**. It never shows the exact score, the ideal energy, the ideal order, or why a formula is incoherent. (*Preview Fuzz* 0 makes it exact; *Refuse When Soul Could Collapse* makes a risky synthesis impossible.)

## For developers

### The Synthesis Lab (editor)

*Tools ▸ SimpleMovements ▸ Rune Magic ▸ Synthesis Lab* — no scene, no play mode. Choose a mode, intention, effect, and add runes (concept, rank, quality, energy as a multiple of the ideal); *Load formula* fills in an archetype. It shows the **exact** evaluation (score, tier, factors, why a formula is incoherent, each rune's relevance, stage, minimum / ideal / safe energy), what the player's **preview** would show, and *Execute on a copy of the sandbox* shows the whole result (spell or rune, flaws, energy lost, Soul Damage, destroyed runes). It runs on the database's current content: edit an effect or a rule and see what changes.

### The soul component (runtime)

On `RuneSoul` (where the rules run: offline, host, server):

| Member | Does |
|---|---|
| `PreviewSynthesis(request)` | The player's preview. Works on a client too (from the runes the server sent). Changes nothing. |
| `Synthesize(request)` | Performs a synthesis: rune users only, awake, not in combat, not meditating. Applies the Soul Damage plan, saves, replicates, tells the player. The request's seed is replaced. |
| `CanSynthesize(out reason)` | The quick check. |
| `ExplainSynthesis(request, out reason)` | The **exact** evaluation, for tools (reveals the optimum). |
| `DismissSpell(id)` / `RequestDismissSpell(id)` | Frees a spell's runes; the request form works from a client. |
| `Spells`, `FindSpell(id)` | The spellbook (replicated to the owner with the rune list). |
| events `SynthesisPerformed`, `SpellDismissed` | For other systems. |

The pure engine is `SynthesisEngine.Preview / Execute / Evaluate` (`RuneMagic/Core/Synthesis/`): no Unity objects, so the server, tests and tools run it.

### Debug panel (play mode)

Under the discovery rows (where the rules run): *Sample ▸* picks an archetype; *Make spell* absorbs its runes (new ones, every concept discovered, with room made in the soul), gives each the ideal energy and synthesizes; *Make rune* / *Advance rune* does the same as a composite rune or a rank advancement; *Dismiss spells*. The panel lists the spells and shows which runes anchor one.

### Data and saves

- **Database** data version **5** (older databases get the archetypes, flaws and engine numbers on upgrade; nothing set is replaced).
- **Soul save** version **4**: `spells`, `synthesisLog`, outcome counters and, per rune, an optional `composite` profile. Older saves load unchanged (the new fields default). A saved spell whose runes are gone (a damaged or hand-edited save) is dropped on load.
- The validation (*Validate Rune Magic Database*) checks archetypes (ids, intention supports the effect, stage runes and concepts exist and belong together, no clash with a database rune), flaws (a flaw for every cause, as a warning) and the engine numbers.

### Network

Dismissing a spell is a request (`DismissSpell`, wire value 8). Spells travel with the rune list to the owner only. A client's request to synthesize (`Synthesize`, wire value 9, added with the UI in Phase 6) carries up to 12 runes, more than an ordinary request's 160-character text holds, so it has its own limit and is decoded and validated by the server: see [06](06-Synthesis-UI.md).

## Decisions and limitations of this phase

These are proposals, all data or flags you can change:

1. **Archetypes are not recipes.** They name, set the ideal energy ratios and add a fidelity factor (*Strictness* 0.8).
2. **The ideal energy is capped at 90% of a rune's maximum** so the optimum can always be reached.
3. **Energy a consumed rune holds beyond what it gives is lost** (the preview warns). Spells do not consume their runes.
4. **A fundamentally incoherent formula fails whatever its score**, with the severity judged at score × 0.5.
5. **Excessive energy can make the magic falter** (0.35 per point of interference): "potential synthesis failure" (design spec 11.5).
6. **Not in combat** (*Allow In Combat* off), like releasing a rune.
7. **Quality only matters through the execution factor and the result's quality**: rank, quality, energy and knowledge stay separate.
8. **The preview's estimate is fuzzy, not secret-proof**: a patient player can probe it with many previews. Raise *Preview Fuzz* or add a cost to previewing if that matters to your game.
9. **No spell can be cast yet** (Phase 7).
10. **Question D16** (are your local copies of the ability files identical to git HEAD?) is still open: Phase 5 touches none of them.

## Tests

`CustomEditor/RuneMagic/Tests/`: `SynthesisEngineTests` (scores, tiers, order, energy, insufficient energy against invalid formulas, outcomes, penalties, rank advancement, composites, anchoring, determinism, the original combinations), `SynthesisPreviewAndDataTests` (the preview and what it hides, Soul Damage, saving and upgrading, validation, a **fuzz run** of 600 random syntheses checking every invariant, perturbations of the original formulas) and `SynthesisRuntimeTests` (needs the Unity engine; the soul component's gating). Run them in *Window ▸ General ▸ Test Runner ▸ EditMode*.
