# Rune Magic 08 — The Spelling System (key-sequence casting)

An optional mechanic (design spec 16.5, directive 9): before some spells are cast, the player must press a few **letters shown on screen**, within time windows. How well they do changes the spell. It is **configured by the developer / server, never by the player**: there is no player-facing option to turn it on or off.

```
press the spell's ability key ──▶ [PreCast hook] ──▶ the server issues a sequence (letters, windows)
        │                                                        │ to the owner only
        │                         the overlay shows the letters ◀┘
        │                         the player's machine records the key presses (times after the first prompt)
        │                                                        │
        ▼                         the server validates, replays and scores them
   the cast starts, as ◀── success: good for 8 s; the owner's machine casts ── failure: costs what the spell's data says; no cast
   any ability's
```

## Who uses it: the global mode and the rules

*Rune Magic Database ▸ Balance ▸ Spelling ▸ Mode* (the developer's / server's choice):

| Mode | Behaviour |
|---|---|
| **None** | No spell uses it: casting works like a normal ability. |
| **Selected** *(default, decision D9)* | Only spells that a **rule** marks use it. |
| **All** | Every spell must use it. |

Spells are synthesized, so the spec's "per-spell flag in the ScriptableObject or data table" is a **data table of rules** (*Rune Magic Database ▸ Spelling Rules*). A rule applies to a spell **form** (`blast`, `wall`…), a **desired effect** (`offense.explosion`…) or an **archetype** (a recognised formula such as Pyroclastic Resonance). The most specific rule wins **field by field**: archetype, then effect, then form. A rule has `use` (Inherit / Yes / No) and optional overrides of the difficulty and of the evaluation; a number left at −1 (or Inherit) is generated.

The built-in examples mark the heavier forms: `blast` and `vortex` (Soul Damage 3 once, on failure), `beam` (with rhythm), `wall` (**Strict**, with wider windows) and `cage` (Soul Damage 1 per mistake). Everything else is cast without spelling until you add a rule or switch the mode to All. Extensions (content packs) can add rules.

The server decides which spells use it. The player's machine only follows: and a spelled spell **cannot be cast without a good sequence**: the server's cast check refuses it (`ICastable.CanStart`), whatever a modified client does.

## The sequence (what the player sees)

1. The player presses the spell's **ability key** (the ordinary `Ability1…` actions, the HUD click, a trait's key: every input-driven cast goes through the same method, `PlayerAbilityController.TryCastFromInput`, which asks the Spelling System first through the generic `PreCast` hook).
2. Everything a cast needs but the sequence is checked first (Rune Energy, the soul's condition, cooldown, silence…): a spell that could not be cast anyway never asks for letters.
3. A few **random letters** appear (the **Spelling overlay**, above the ability bar). The first is asked for now; each key has its **own time window** that opens when the key before it was hit. A time bar shows the key's window and the total time, with the seconds as a number.
4. The player presses the letters. When the sequence is over (all answered, the spell failed, or the time ran out) the key presses are sent to the server.
5. The overlay shows the result. On a success the cast starts by itself (aimed as usual: a spell that needs a click to confirm its target opens its preview).

Keys that are **not** in the sequence's pool (walking, other actions) are ignored: **moving while casting is never a mistake.** Typing in a text field is never read.

### Interruption

Stun, silence, death cancel the sequence; so does damage when the spell's ability says it is interrupted by damage (and the hit is above its threshold): the same rules as `AbilityCaster`, which means that damage over time (periodic ticks) does not interrupt it. Pressing the ability key of a spell while a sequence runs does nothing. A cancelled sequence draws no energy; it causes the Spell Balance's failure damage only if *Damage On Interrupt* is on (never automatic).

## Which keys can be asked for (never movement keys)

The letters come from **data** (*Spelling ▸ Candidate Keys*, A–Z by default, names of the Input System's `Key` enum). The pool of a player's machine is the candidates **minus**:

- the keys the game reads outside actions (*Reserved Keys*: digits and quick slots, Y/N, T, Esc, Tab, Enter, Space, F-keys, modifiers);
- the keys of the player's **ability slots** (always) and of **every other gameplay action** (interact, inventory… unless *Exclude Gameplay Keys* is off);
- the **movement keys**: the keys of the actions named in *Movement Actions* ("Move"…) and of any action with a keyboard 2D-vector composite. **They are read from the live bindings, so a player's rebind (WASD → ESDF, arrows…) is followed.** The pool is looked at again whenever bindings change.

A spell that **roots** the caster (its ability's *Movement While Casting* = Stop) may also use the movement keys (*Allow Movement Keys When Rooted*), but never the keys of abilities.

The pool is worked out on the player's machine from a private copy of the Input Actions with the saved rebinds applied (the way every other input of the project reads its bindings). Keys are logic by their physical `Key` and **shown by the layout's own display name** (an AZERTY keyboard shows the letters it has). With fewer keys than *Min Pool Size* (3), or without a keyboard, the Spelling System is skipped for that cast and a warning is logged once: the spell is cast with **the worst result a success can have** (see limitations): skipping the sequence is never a way to a better cast.

## How hard it is (generated, per spell)

| Value | Built-in |
|---|---|
| Letters | `3 + 0.5 × (rank − 1) + 1 × (power/reference − 1, at least 0) + 2 × complexity`, between 2 and 10, plus the soul condition's extra letters, never more than the pool |
| Window of a key | `max(0.5, 1.6 − 0.6 × complexity)` s, ±25% from key to key (a different window for each key), the first key +0.5 s |
| Total limit | `0.9 ×` the windows added up |
| Mistakes the Lenient model allows | `1 + ⌊letters × 0.34⌋` |
| Rhythm | off (a rule turns it on): beat every 0.6 s, tolerance 0.25 s; windows are widened so a beat fits |

A **rule** can fix the letters (`letters`, `lettersDelta`), give **each key its own window** (`keyWindows`: the spec's example 1.5 s, 0.8 s, 1.2 s), scale them (`windowMultiplier`), set the total limit, the rhythm, the failure model, the allowed mistakes, the speed-bonus threshold and the Soul Damage.

**The soul's condition** (*Balance ▸ Soul Conditions*, spec 16.5 "Soul Integrity interaction", consistent with 16.2):

| Condition | Extra letters | Windows | Penalties (power, time, stability per mistake) |
|---|---|---|---|
| Stable | — | ×1 | ×1 |
| Strained | +1 | ×0.95 | ×1.1 |
| Damaged | +1 | ×0.9 | ×1.25 |
| Critical | +2 | ×0.8 | ×1.5 |
| Near Collapse | +2 | ×0.7 | ×2 |

**Forgiveness:** knowing the concepts of the spell's runes (share of discovered concepts) and the spell's synthesis quality make it easier: up to 25% (15% from knowledge, 10% from quality at the built-in values): windows wider by that share, up to 2 more mistakes allowed.

## The score and what it does to the spell

The sequence is replayed from the key presses (`SpellingTracker`, the same rules in the overlay and on the server):

- A key pressed in time hits its letter. A **wrong key** is a mistake (the letter stays asked for). A letter whose window closes before it is hit is **missed**; a key pressed after the window is therefore wrong or late. With rhythm, a key off its beat by more than the tolerance is a mistake too. After the total limit nothing counts.
- **Failure models:** **Strict** — one mistake fails the spell. **Lenient** — each mistake costs power, stability and cast time; more than the allowed number fails the spell.
- **Performance (0–1)** = the weighted **accuracy, speed, precision (how early in each window) and rhythm** (weights in *Spelling ▸ Weights*: 0.5 / 0.2 / 0.2 / 0.1). The evaluation is **extensible**: `SpellingEvaluator.Register("id", term)` adds a term, and a weight with that id in the balance gives it a say; no core code changes.
- **A success** earns: power between −10% and +10% by performance, −6% power, +10% cast time and −5 stability **per mistake** (Lenient); and, when finished within 55% of the limit **without mistakes**, the **speed bonus**: +10% power, −10% Rune Energy cost, −20% Soul Strain, +5 stability. Power and cast time are put in the spell's slot (so the cast time you see is the cast time you get); cost, strain and stability are applied to the cast when it starts. A success is good for 8 s.
- **A failure** (a failed Strict sequence, too many mistakes, or a sequence never answered in time) costs what the spell's data says: **Rune Energy** (50% of what a cast would have drawn is lost from the anchored runes), **Soul Damage** (below), a recovery of 1 s before the same spell can be spelled again; and the cast never starts.

### Soul Damage is always configurable, never automatic

For each spell, three separate settings; any combination, including none, is valid:

| Moment | Where it is set |
|---|---|
| (a) on a **successful cast** | the form's Strain Multiplier and *Spell Balance ▸ Strain On Success* (Phase 7); the speed bonus lowers it |
| (b) on **mistakes** | the rule's `damageMode`: **None** *(the default)*, **FlatPerMistake**, **ScalingPerMistake** (each further mistake costs 50% more than the one before), **OnceOnFailure**, with `damageAmount` and the kind `spelling_mistake` |
| (c) on a **failed cast** | *Spelling ▸ Failure Damage Factor* × the spell's failure damage (kind `spell_failure`); 0 = none |

The kinds' multipliers and deep shares and the soul's condition (Soul Damage Multiplier) apply as to any Soul Damage.

## What the server checks (plan risk R7)

- The client's **pool** is cleaned: only candidate keys, none reserved, no duplicates. (Only the client knows its own movement bindings, so those it must have left out itself; a pool that lies only changes which letters it is asked for, never how well it can do.)
- The server generates the letters, the windows and the parameters itself and sends them to the owner only.
- The result is a short text (`seq|end|KEY=ms,…`, at most 1024 characters, at most 4 presses per letter): refused when malformed, with an unknown sequence number, out of order, **faster than a hand** (70 ms between keys, from the first prompt too), **longer than the real time that passed** since the server issued the sequence (1 s of latency forgiven), **reported too long after it ended** (more than *Max Report Lag*, 3 s, between the end the client claims and the moment the result arrives: it could only be hiding its times), or too many presses. A refused result counts as an unanswered sequence (a failure), never as an interruption. The **score is computed by the server**; the client never says how well it did.
- A sequence not answered before its time (plus 4 s) **fails**, and so does one abandoned by asking for a new sequence while it is still open (so stopping after a mistake is not free).
- Starting the same sequence again within one second is ignored (a double press of the ability key), and the client waits a second for the server's answer before it asks again.
- The client's **key pool never shortens the letters**: the server asks for the letters of the spell's rank and complexity whatever the pool the client reports (a pool that lies only changes which letters it is asked for).
- Requests are rate-limited like every other.

## Accessibility

Large letters; the state of each letter is a **shape and a word** besides colour (`>` now, `OK`, `X` wrong key, `MISS` with the letter struck through) and an **outline style** (thick for the current letter); a **blue / orange** palette that reads the same for the common colour vision deficiencies; the time left as a **bar and a number**; the mistakes allowed in words; optional **short tones** for right and wrong keys (*Spelling ▸ Sound Cues*, generated: no audio assets). Keyboard layout differences are handled as above.

## For developers

| Piece | Where | Does |
|---|---|---|
| `SpellingBalance`, `SpellingRule` | `Core/Data/SpellingDefs.cs` | The data: global mode, rules, every number above. |
| `SpellingGenerator` | `Core/Spelling/` | Which spells use it (mode + rules); a spell's parameters. Pure. |
| `SpellingKeyPool`, `SpellingSequence` | `Core/Spelling/SpellingKeyPool.cs` | The pool from bindings; cleaning a reported pool; the seeded letters. Pure. |
| `SpellingTracker`, `SpellingEvaluator` | `Core/Spelling/SpellingEvaluator.cs` | The replay, the score (extensible terms), the consequences, the damage of mistakes. Pure. |
| `SpellingView`, `SpellingResultCodec`, `SpellingValidator` | `Core/Spelling/SpellingNetwork.cs` | What the owner is shown; the result on the wire; what the server accepts. |
| `RuneSoul.Spelling` | `Runtime/` | The hook, the challenge, validation, failure, interruption, the ready sequence, the owner's auto-cast. |
| `SpellingKeyProvider` | `Runtime/` | The live bindings → the pool; key names, labels and reads. |
| `SpellingOverlay` | `UI/` | The accessible overlay. |
| Requests | `Networking/Core/RuneMagic/` | `SpellingStart` (12: amount = key slot, text = the usable keys) and `SpellingResult` (13); the owner's view travels like the soul's (`TargetSpellingView`). |
| `PlayerAbilityController.PreCast` | `Essentials/Ability/Player Ability/` | **Generic** hook: asked with the slot before a cast; knows nothing about runes. |

### Existing files this phase touches

`PlayerAbilityController.cs` (changed in Phases 1 and 7 too) gets the generic `PreCast` hook (one line in `TryCastFromInput`) and a `CheckExtra` override that asks the slot's `ICastable.CanStart`; `ICastable.cs` (Phase 7) gains `CanStart`. No other existing ability file changes. **Decision D16 is still open:** if your local copy of `PlayerAbilityController.cs` differs from git HEAD, merge the small diffs of the manifest by hand.

## How to test

Offline, without Unity (what I ran): 52 tests in `SpellingCoreTests` — the three modes and rule precedence; difficulty by rank, power and complexity, a window per key, rules' explicit windows, soul condition and forgiveness; the pool (default; movement keys default and rebound; ability and gameplay keys; rooted spells; too small; what the server keeps of a client's report; text round trip); the seeded sequence; replay and score (clean/fast with bonus, slow, lenient and strict mistakes, late keys, limit, rhythm, live tracker = server replay, extensible terms, determinism); Soul Damage on mistakes (none by default, flat, scaling, once on failure); the effects on cost, strain, stability, power and cast time; the server's validation and the wire format; the data (valid, upgrade from version 6, JSON, bad data, extensions); the request kinds.

In Unity's Test Runner (**RequiresEngine**): `SpellingRuntimeTests`.

In play mode, as a Mage with a spell (the part I could not run here):

1. *Debug panel ▸ Spelling mode* → **All** (or keep Selected and use a `blast`/`wall`/`beam`/`vortex`/`cage` spell). Make a spell, **Refill spell energy**, **Spells → free keys**.
2. Press its ability key: the overlay appears above the bar with the letters; none of them is a key you walk with (try rebinding movement in Key Bindings: the letters follow). Press them; walk while doing it: nothing counts against you.
3. A clean sequence: the spell casts by itself; the debug panel shows *READY* then the cast. A wrong key: *Lenient* counts it; *Strict* fails the spell. Let the time run out: it fails, energy is lost, the soul may take damage by the rule.
4. Get stunned or hurt (damage-interruptible spell) mid-sequence: it is cancelled.
5. Mode **None**: the spell casts like a normal ability. A non-Mage: no overlay, nothing changes.
6. Host + client: the client's sequence is issued, validated and scored by the host; a second client sees nothing of it.

## Decisions and limitations of this phase

1. **A pre-cast challenge**, as planned (§9.7), instead of letters appearing *during* the cast time: the ability's own phases are untouched. The cast begins right after the sequence.
2. **Keyboard only.** The Spelling System needs letter keys. Without a keyboard (or with fewer usable keys than the minimum) the cast gets **the worst result a success can have**: as if the sequence had been played with the most mistakes the spell allows (at least one), so weaker and slower, with no speed bonus, and no Soul Damage for the mistakes (nothing was played) — a warning is logged once; set *Skip Without Keyboard* off to make the server refuse spelled spells to such players instead. Because the server cannot know whether a client has a keyboard, a modified client could also ask for the skip: it earns the worst result, never a bonus, so there is nothing to gain from it. *(Phase 9 review: the first version gave the skip a neutral result, which was better than playing the sequence badly.)*
3. **Only the client knows its movement bindings**, so the server cannot check that the movement keys were left out (it checks everything else).
4. **The overlay reads physical keys through the Input System device**, because the keys it asks for are dynamic (they come from data and from what is free in your bindings): there is no fixed key in code, and no action to name.
5. **Not checked in Unity**: the overlay's layout and the feel of the windows. Expect to tune the numbers (windows, letters, penalties) and the overlay's size and position (`SpellingOverlay.Build`).
6. **No per-letter animation** beyond the state styles; tones are generated sine tones.
7. **Failure consequences are generic** (energy lost, Soul Damage, a short lock); a spell cannot yet have a different lock or energy loss from the balance's (add fields to `SpellingRule` the same way as the others).
8. **Still open: D16**, now for `PlayerAbilityController.cs`'s third change.
9. **Phase 9 review fixes**: a result reported long after it ended is refused (*Max Report Lag*); a validator refusal is an unanswered sequence; a repeated start is ignored; the owner's client waits for the server's answer before asking again; periodic damage does not cancel a sequence; moving or removing a spell is refused while a sequence is open (*Finish spelling first.*) or a cast is in progress.
10. **The server is the authority, with these limits** (the limits of any client-side input): the key pool and the bindings are the client's, the press times are the client's reading of its own keyboard (the server checks them against real time, human speed and order, never against the key itself), and the server cannot know whether a keyboard is attached. A stricter check (a challenge letter sent only at the moment it is due, with the answer measured by the round trip) can be added on top; it is not part of the design spec.
