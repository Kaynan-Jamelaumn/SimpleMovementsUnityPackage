# Rune Magic 09 — Acceptance, regression and balance report

Phase 9 closes the build: the whole gameplay loop (design spec section 21) was walked end to end, every line of the acceptance checklist (section 22) was turned into a test, the existing systems were audited for regressions, the balance tables were read and tuned, and an independent review of the runtime flows was made and its defects fixed.

**What "pass" means here.** I could not run Unity in this environment. Every rule of the system is plain C# that does not need the engine, and it runs on a .NET host against stubs of the Unity assemblies (the *offline harness*, with the project's own analyzers). A line marked **PASS (offline)** means the rule is proven by a passing test; the 30 tests that call the native engine (`GameObject`, `Resources`…) are listed as **NEEDS UNITY** and are in the Test Runner under category *RequiresEngine*. Whatever involves looking at a screen, a real network or a real controller is in [What only Unity can confirm](#what-only-unity-can-confirm). Nothing in this report is claimed that was not run.

## Result at a glance

| | |
|---|---|
| Rune Magic tests | **450** — 420 pass offline, 30 need the engine (0 fail) |
| Whole project, offline | 1,723 tests run, **0 regressions** against the recorded baseline (the tests that fail are the baseline's own, e.g. `AgricultureCoreTests.QuietStepsEndAsTheEveryStepLook`, a defect of git HEAD found in Phase 0) |
| Compile, 6 configurations (Player, Editor, EditorTests, each with and without FishNet) | **0 new errors** (the baseline's known errors are the unrelated files of Phase 0 finding P1) |
| Analyzers (Unity's own) | **none** in `RuneMagic/`; project total unchanged (922) |
| Hard-coded keys | none: every input is a named Input Action or a key read from live bindings |
| Acceptance checklist | **37 of 37 lines PASS (offline)**; 6 lines also need a visual check in Unity |

## The complete gameplay loop (spec 21)

`GameplayLoopTests.TheCompleteGameplayLoopRunsEndToEnd` walks the 27 steps with one player's state and the real rules, no fixtures:

kill → drop roll and energy reward → capacity check → absorb → give energy to a chosen rune → dungeon source: minigame → riddle → permanent unlock → choose intention and desired effect → select runes, concepts, order, energy → soul check → synthesis → evaluation (tier) → new spell → Soul Damage → recovery → assign to an ability key → Spelling sequence → cast with Rune Energy cost, Soul Strain and casting debuffs → return to combat (energy refilled by kills) → save and load, identical.

Findings of the loop (kept as documented behaviour, asserted in the test):

- After a synthesis the **anchor runes of the new spell are empty** (the energy was spent): a Mage must earn the energy back with kills before the first cast. This is the intended link between combat and magic (spec 21: "the loop must continuously connect combat progression … and soul management"), and the Balance Report shows the price (≈ 2.5 to 10 normal kills of energy per archetype).
- A formula of only the concepts the player understands scores *Successful*, not *Perfect*: Perfect needs breadth of knowledge (spec 23).

The other two tests of that class check **independent resources** (spec 20: full health with a critical soul; a full rune with a damaged soul; integrity without energy; powerful runes with no slot; concepts without energy) and the **design philosophy** (spec 23: an unknown concept cannot be used; order and ratio change the score; more and better-understood concepts raise it; a failed experiment costs the soul; collecting high-rank runes alone does not make a strong spell).

## Acceptance checklist (spec 22)

Test = `AcceptanceChecklistTests.<id>` unless stated. **Unity** = a visual or live check listed below that the offline tests cannot do.

### Rune acquisition and management

| # | Requirement | Result | Evidence |
|---|---|---|---|
| AC01 | Runes come from monsters, dungeons and other sources | PASS (offline) | Drop profiles roll for monsters (`DropRules`); dungeon sources teach concepts and grant nothing by accident; rune items (pickups, rewards) carry runes. Unity: kill a mob in play. |
| AC02 | Runes can be absorbed into the soul | PASS (offline) | `RuneRules.Absorb` creates an instance with rank, quality, energy. |
| AC03 | Limited capacity; each rune occupies a slot | PASS (offline) | Absorption refused at capacity (`NoCapacity`); a rune of slot cost 2 occupies 2. |
| AC04 | Player manages the collection | PASS (offline) | Release (refused in combat, and refused while the rune anchors a spell until the spell is dismissed; the UI confirms first), energy assignment, the list. Unity: the Rune Soul window. |
| AC05 | Combining runes can free slots | PASS (offline) | Rank advancement turns seven runes into one: six slots are free again. |
| AC06 | Rank and quality supported | PASS (offline) | Separate values; a higher rank holds more energy; rank titles and quality tiers exist; quality counts in a synthesis; rank advancement is an explicit act (AC09). |

### Rune energy

| # | Requirement | Result | Evidence |
|---|---|---|---|
| AC07 | Kills grant energy; the player chooses the receiving rune | PASS (offline) | Pending pool; `AssignEnergy` to an instance chosen by the player. |
| AC08 | Max energy per rune; a full rune cannot receive more | PASS (offline) | `MaxEnergy(rank)`; overflow stays in the pool. |
| AC09 | Full energy does not auto-trigger rank advancement | PASS (offline) | Advancement only by an explicit synthesis of 7 same-rank runes. |
| AC10 | Combinations consume energy from the input runes | PASS (offline) | Execute lowers each input rune's energy by what the formula allocated. |
| AC11 | Insufficient energy: synthesis cannot proceed | PASS (offline) | `InsufficientEnergy` is a refusal, not a failed attempt: nothing is consumed (distinct from an invalid formula). |
| AC12 | Excess energy may interfere; ratios influence the outcome | PASS (offline) | The evaluator penalises energy above the safe threshold and rewards the ideal ratio. |

### Rune synthesis

| # | Requirement | Result | Evidence |
|---|---|---|---|
| AC13 | Select intention, effect, runes, one concept per rune | PASS (offline) | The nine-step `SynthesisDraft`; a rune used twice carries a concept per use; unknown concepts refused (`ConceptNotKnown`). Unity: the window. |
| AC14 | Order and energy allocation by the player, and they matter | PASS (offline) | Reversed order and shifted energy give different scores. |
| AC15 | Compatibility and feasibility evaluated | PASS (offline) | Rules (synergy/conflict/incoherent) and aspect coverage; the Lab and the preview show the reasons. |
| AC16 | Perfect 100 % / Successful 70–99 % / Flawed 30–69 % / Failure < 30 %, with consequences | PASS (offline) | Tier boundaries and the consequences of each (spell, flawed spell, loss, Soul Damage). |
| AC17 | Complex combinations beyond fixed recipes | PASS (offline) | A formula that is no archetype still scores by its aspects; archetypes only name and refine. |

### Concept discovery

| # | Requirement | Result | Evidence |
|---|---|---|---|
| AC18 | Multiple hidden concepts per rune, discovered individually | PASS (offline) | 6–8 per built-in rune; unlock by concept. |
| AC19 | Dungeon exploration, minigames and riddles | PASS (offline) | Source → minigame (rune tracing) → riddle (written or generated). Unity: the prefab in a dungeon and the window. |
| AC20 | Correct answers unlock permanently; incorrect ones invalidate the source | PASS (offline) | Permanent knowledge survives save/load; a wrong answer burns the source for the concept. |
| AC21 | Retry requires another source | PASS (offline) | A burned source refuses; another source works. |
| AC22 | Rare concepts need harder challenges | PASS (offline) | Rarity → band → more glyphs, steps, fewer tries (also in the Balance Report). |

### Soul integrity

| # | Requirement | Result | Evidence |
|---|---|---|---|
| AC23 | Independent of physical health and rune energy | PASS (offline) | A separate resource with its own state and rules. |
| AC24 | Synthesis causes Soul Damage; casting causes Soul Strain | PASS (offline) | Damage kinds `synthesis_*`, `spell_cast`, `spell_failure`. |
| AC25 | Low integrity restricts synthesis and weakens casting | PASS (offline) | Condition tiers restrict rune count/complexity/synthesis and add casting debuffs. |
| AC26 | Recoverable; severe damage needs specialised healing | PASS (offline) | Rest, meditation, items, sanctuaries and rituals restore integrity up to a ceiling; deep damage (a severe kind, 40 points) lowers that ceiling and only specialised healing removes it. |

### Spellcasting

| # | Requirement | Result | Evidence |
|---|---|---|---|
| AC27 | Created effects can be cast | PASS (offline) | Compile → plan → draw → release. Unity: a real cast needs the engine (ability pipeline). |
| AC28 | Rune Energy cost and Soul Strain; powerful spells strain more | PASS (offline) | Also `BalanceReportTests.TheSpecsWorkedExampleOfCastingHoldsWithTheDefaults` (500 energy, 12 Soul Damage). |
| AC29 | Low integrity causes casting debuffs | PASS (offline) | Cast time, power, energy cost, instability, rank cap per condition. |
| AC30 | Power, stability, cast time, range, duration, area | PASS (offline) | All compiled into the slot's modifiers. |
| AC31 | Spells go on the existing ability UI slots | PASS (offline) core / Unity for the bar | `ICastable` on `AbilitySlot`; assignment through `ApplyReplicatedSlot`; the cast uses `Ability1…`. Unity: the ability bar shows cost and strain. |
| AC32 | Spelling System configured by developer/server, not a player option | PASS (offline) | Mode and rules are data; no setting a player can change; the server decides. |
| AC33 | Per-spell difficulty: letters, windows, rhythm, speed bonus, extensible parameters | PASS (offline) | `SpellingParams`; rules by form, effect, archetype. |
| AC34 | Per-key windows differ within and between spells | PASS (offline) | `windows` list per sequence. |
| AC35 | Soul Damage on success, mistakes, failure configurable per spell, never automatic | PASS (offline) | Each defaults to none; a rule sets any combination. |
| AC36 | Random letters; right input activates; wrong input fails or slows (configurable) | PASS (offline) | Strict and Lenient models; wrong key fails or lengthens the cast. Unity: type it. |
| AC37 | Spells that allow movement never use movement keys | PASS (offline) | The pool excludes the movement actions, any 2D-vector composite and gameplay keys, from live bindings (rebinds included). Unity: with a rebound layout. |

## Regression audit

The system was added without changing behaviour for anything that is not a rune user. What was checked:

| Existing system | How it stays unchanged | Evidence |
|---|---|---|
| **Non-Mage classes** | `RuneMagicGate` makes a character a rune user only by class; nobody else gets a soul, a window or a request. | `RuneMagicGateTests`; every Rune Magic request/handler starts with the rune-user check (`…Nobody…CanSynthesize`, `…CannotCastRuneSpells…`). |
| **Essence (ability) absorption** | `AbsorptionGate` is a generic hook with **no rule registered unless Rune Magic is on**; with no rule it returns exactly what it did. Mages are blocked from absorbing (directive 3); the rolls still drop for other players. | `RegressionGuardTests.TheExtensionsOfTheAbilitySystemAreInertUntilSomethingUsesThem`; `TheExistingSystemsKnowNothingAboutRunes` (no member of the six existing types mentions a Rune Magic type, by reflection). |
| **Abilities and the ability bar** | `AbilitySlot.Castable` is null for every ordinary ability; `PlayerAbilityController.PreCast` is null unless a Rune Soul registers; `ICastable` is six methods that rune spells implement. | The same tests; the diffs of the six existing files are in the master manifest (small, additive). |
| **Mobs, loot, XP** | The kill hook (`RuneKillListener`, `RuneSource`) only adds rewards; the mob's own drops and XP are untouched. | Existing mob code not modified. Unity: kill a mob as Warrior and as Mage. |
| **Saves** | The soul has its own store keyed by the character (`RuneSoulState` JSON); no field of any existing save changed; versions 1…5 load. | `RegressionGuardTests.ARuneMagicSavePutsNothingInTheCharacterSave`. |
| **Network** | Request kinds only ever append (1…13); an unknown kind is refused. | `TheNetworkRequestKindsOnlyEverGrow`, `AClientCannotAskForAKindTheServerDoesNotKnow`. |
| **Input** | No key in code; actions added without keys; players rebind in Key Bindings. | Key scan over every added file: 0 hits. |

## Independent review: defects found and fixed in Phase 9

A review of the runtime flows (cast, spelling, assignment, network) found these real defects. All are fixed and have tests (offline) or are listed for Unity.

| # | Defect | Fix |
|---|---|---|
| 1 | **The skip path was an exploit**: a player who could "not spell" got a *neutral* result, better than playing the sequence badly. | The skip is now the worst a success can be (`SpellingEvaluator.Skipped`): weaker, slower, no bonus, no Soul Damage for mistakes. Test: `ASequenceThatWasNotPlayedIsTheWorstASuccessCanBe…` |
| 2 | **The client's key pool could shorten the sequence** (a pool reported with few keys asked for fewer letters). | The server resolves the letters for the spell's rank and complexity whatever the pool. |
| 3 | A result could be **held back and reported long after it ended**, hiding its real times. | `maxReportLag` (3 s) in the validator. Test: `AResultReportedLongAfterItEndedIsRefused…` |
| 4 | A validator refusal **interrupted** the cast (and cost nothing); it is a failure to answer. | A refusal counts as an unanswered sequence. |
| 5 | An **undecodable** result left the sequence open until it timed out. | It finishes at once as a failure. |
| 6 | **Double start**: a second press of the ability key re-issued the sequence. | The server ignores a repeat for the same slot within one second; the client waits one second for the answer. |
| 7 | Damage over time **cancelled** a sequence. | Periodic ticks are ignored, like `AbilityCaster`. |
| 8 | The auto-cast of a succeeded sequence used the **spell's slot recorded earlier** (stale after a move). | The slot is looked up when the cast starts. |
| 9 | The misfire roll used a **predictable seed**. | The server's own seed. |
| 10 | **Moving a spell reset its cooldown** (a way around cooldowns); and a spell could be moved or removed **during its own cast** or open sequence. | Cooldowns are remembered by spell and restored; moves are refused during a cast or sequence. |
| 11 | A soul added at runtime was not hooked into the ability system; hooks were not removed on destroy. | `Hook()` after `AddComponent`; `UnhookAbilities()` in `OnDestroy`. |
| 12 | The overlay accepted presses faster than a human (a held key). | Debounce at the minimum press interval. |

**Found and not fixed (documented, decisions for you):**

- **Saved essence grants of a character that becomes a rune user are dropped on load** (decision D15, class changes into or out of a rune class). The project has no class-change flow I could hook; the plan's default (existing essences removed with confirmation; out of the class, runes go dormant) is not implemented as a flow. A new character is unaffected.
- **Every cast re-sends the owner's rune list** (the anchored runes changed energy): a few kilobytes per cast, bounded by the spell's cooldown. Delta updates would be a net optimisation, not a correctness issue.

## Balance pass

The Balance Report (*Rune Magic ▸ Balance Report*) reads every table and prints the pace of the loop. The built-in numbers:

| | Value | Reading |
|---|---|---|
| Soul slots | 7 at levels 1–19, 14 at 20, 21 at 40, 28 at 60 | The spec's seven-rune example fits from the start. |
| Rune energy by rank | 1,000 · 5,000 · 25,000 · 125,000 · 625,000 | The spec's table. |
| Energy of a normal kill | 140 (tier 0) · 245 (tier 3) · boss 840 | About the spec's 150. |
| Kills to fill a rank 1 rune | ≈ 7 | A first rune charges in an evening, not a minute or a week. |
| Normal kills per rune drop | ≈ 8 (tier 0) · ≈ 6 (tier 3); a boss always drops | |
| Synthesis (ideal energy; Soul Damage of a perfect one / a botched one) | The 7-rune Pyroclastic Resonance: 1,338 energy (≈ 10 normal kills), 10 % / 18 %. The 3-rune archetypes: 336–717 energy, 6–29 % / 6–46 %; the darkest (Void Collapse, Astral Ruin) cost the most. | Magic always costs the soul; one botched attempt never wrecks it. |
| **Casting** (changed in Phase 9) | A rank 3 spell of complexity 0.5 draws **500** energy and causes **12** Soul Damage; rank 1: 20 and 4; rank 5: 12,500 and 20. | **The spec's worked example (16.3)**: two powerful rank 3 spells leave the soul at 76, Strained. Before the pass the defaults gave 2,000 energy and 5 Soul Damage (four times too dear in energy, too gentle on the soul). |
| Casts from one full rune | ≈ 50 at complexity 0.5 | Soul Strain, not energy, is what stops a Mage first (as in the spec). |
| Recovery | Meditation 10 integrity/min (out of Strained in 2 min); rest 3/min (from 50 % in about 17 min) | Worth doing, not instant. |
| Spelling | rank 1: 4 letters, 4.7 s · rank 3: 6, 7 s · rank 5: 8, 9 s | A human can type it in a fight; heavier spells are a decision. |
| Discovery | every concept can be taught by ≥ 1 source (0 unreachable, 0 single-source); bands 3 to 7 glyphs, 1 to 3 riddle steps, 3 down to 1 tries | |

What changed in the defaults: *Cast Energy Fraction* 0.08 → **0.02**, *Strain Base* 2.5 → **4**, *Strain Per Rank* 0.5 → **1**. These are fields of the database: a database created before Phase 9 keeps its values (see [07](07-Spells-and-Slots.md), decision 14). `BalanceReportTests` pin the **ranges** (not every number) so a retune that breaks the pace fails a test.

## What only Unity can confirm

I could not run these; each is a short manual check.

1. **Compile in Unity 6** (the offline compile is against stubs of the Unity assemblies; the project's analyzers ran clean).
2. **The 30 engine tests** (Test Runner, Edit Mode, category *RequiresEngine*).
3. **Windows**: the Rune Soul window, Synthesis window (nine steps), Discovery window, Spellbook and Spelling overlay: layout, fonts, scaling, controller/pointer use. Expect to adjust `…Window.Build` sizes.
4. **A real cast**: assign a spell to `Ability1`, cast it: the form's preset plays, the cost appears in the tooltip, the energy and integrity move, the cooldown shows on the bar.
5. **The Spelling sequence**: letters appear, a wrong key fails or slows, the movement keys are never asked for (try with rebound movement), the sound cues.
6. **Multiplayer (FishNet)**: a client sends requests, the server decides, the owner's views arrive, other players receive nothing of a soul; the server's database decides.
7. **Mage vs other classes in play**: a Warrior still absorbs essences and sees no Rune UI; a Mage cannot absorb an essence but still sees the pickups drop for others.
8. **Dungeon sources**: the discovery prefab in a Dungeon Prop Table; the minigame and riddle through the window.
9. **D16**: whether your local copies of the six existing ability/network files equal git HEAD. If not, merge the small changes of the master manifest by hand instead of overwriting.

## Known limitations

- The Spelling System needs a keyboard (a gamepad-only player takes the skip result or is refused, as configured).
- The server cannot see what the player's hands did: press times are the client's reading of its own keyboard, checked for order, human speed and real time, never for the key. A stricter protocol (a challenge letter sent at the moment it is due) can be added; it is outside the design spec.
- No per-spell icon (the bar shows initials), no particles or sounds of its own: a spell looks like its form's preset.
- Effects with no combat equivalent use the `ritual` form: they cost and strain but do nothing in the world until you give the form a template.
- The balance numbers are a first pass from the spec; they need play-testing with real combat numbers.

## Update after Phase 9 (the grimoire, the validators, D16)

After the nine phases, the project's own commits (up to `aaac23a`) were merged under the Rune Magic work and three things were added. Nothing in the acceptance checklist changed; the checklist tests all still pass.

| | |
|---|---|
| **D16 is closed** | The five ability files Rune Magic patches (`AbilityAbsorption`, `AbilityPickup`, `AbilitySlot`, `PlayerAbilityController`, `HudAbilityBar`) are byte-identical in your commit to the versions the patches were made against, so they apply with no hand merge; `NetworkSetupMenu.cs` merged with your animal-carcass changes (one line of ours: the player's component list). Your commit also defines what git HEAD `6400e9c` lacked (`PlantSurroundings`, `PlantScratch.kept/quiet`, `SleepSpot.AccessCheck`), so the harness no longer needs its shims; its baseline was re-recorded on `aaac23a`. |
| **The grimoire** | [10 — The Grimoire](10-Grimoire.md): every Mage begins with a book; a rune of the soul is imprinted on a page for a cost in its energy (it must hold it), a print is absorbed once with no energy, a Mage who holds another Mage's book reads it. 27 tests without Unity, 4 with. |
| **Validators** | The Player Setup Validator now lists the crafting, alchemy and harvesting journals and lets a system register its own scripts; Rune Magic registers `RuneSoul`, `MeditationController` and (networked prefabs) `NetRuneMagic`, with one Add that does the whole setup. |
| **Optimisation** | When only the energy of runes changes (a cast, giving energy, an imprint) the server sends a few numbers instead of the whole rune list (review finding #12 of Phase 9). |
| **Result** | 488 Rune Magic tests: 453 pass, 35 need the engine, 0 fail; 0 regressions against the baseline of `aaac23a`; 0 new compile errors in the six configurations; no analyzer diagnostics under `RuneMagic/`. |
