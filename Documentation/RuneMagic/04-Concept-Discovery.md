# Rune Magic 04 — Concept discovery in dungeons

A rune holds several hidden **concepts**. A Mage does not learn them by absorbing the rune: each concept is **discovered**, one at a time, at a knowledge source in a dungeon (design spec 8, 17).

**The process:**
1. Find a source (an ancient inscription, a magical artifact, a sealed chamber...) and interact with it.
2. Play a short minigame.
3. Answer a riddle about the hidden concept.

**The result:**
- **Correct:** the concept is added to your knowledge **permanently**. It stays when you release the rune, and it is yours for synthesis.
- **Wrong:** the concept stays undiscovered, and **that source is burned for that concept**. There are no retries on the same source: find another dungeon with the same kind of knowledge.

Everything that tunes it is data in the *Rune Magic Database*:
- **Data ▸ Balance ▸ Discovery** (the difficulty bands and the rules);
- **Data ▸ Discovery Sources**;
- **Data ▸ Riddles**.

## For the player

- **Using a source.** Interact with it, like any dungeon object: the player's `Interact` input. Only rune users (Mages) get anything from it.
- **The minigame (rune tracing).** A marker runs around a rune circle. Click **Seal** while it is inside the lit arc, once for each glyph. Closer clicks score better; you need enough of the score to pass. The window has no key: it is pointer UI.
- **The riddle.** Choose the answer from the options. Riddles reason about magical principles rather than testing memory.
- **Leaving.** Closing the window with Escape only hides it; using the source again shows it. **Give up**, walking away from the source, or running out of time counts as a wrong answer, so you cannot read a riddle and come back for another.
- **Dying, or the source disappearing, costs nothing.**

## Difficulty (design spec 17.2)

A source's **band** comes from the dungeon it is in (`DungeonSpawned.tier`; a source can override it). The band sets the minigame, the riddle and which concepts it can teach.

| Band | From dungeon tier | Minigame | Riddle | Teaches |
|---|---|---|---|---|
| Beginner | 0 | 3 glyphs, wide arcs, 3 tries | 1 question, 3 options, plain clues | Common (and a few uncommon) |
| Intermediate | 1 | 4 glyphs | 1 question, 4 options | Common, uncommon |
| Advanced | 2 | 5 glyphs | **2 questions** (multi-step), 4 options, fewer clues | Common (rarely), uncommon, **rare** |
| Expert | 3 | 6 glyphs, narrow, 1 try | 2 questions, 5 options | Uncommon, rare, **epic** |
| Legendary | 4 | 7 glyphs, very narrow, 1 try | **3 questions**, 6 options, one clue | Rare, epic, **legendary** |

- Harder concepts (higher *complexity*) make the targets narrower.
- Rare concepts therefore need harder dungeons.

**Per band (*Balance ▸ Discovery ▸ Tiers*):**
- glyphs, speed, target width, pass score, time limit, tries;
- riddle steps, options, clues;
- the **rarity weights** (chance of teaching Common ... Legendary concepts).

**Common to all bands (*Balance ▸ Discovery*):**
- *Only Runes In Soul* (default on: a source only teaches concepts of runes you carry);
- *Abandon Burns*;
- *Session Seconds*, *Leave Distance*;
- *Min Press Interval*, *Clock Slack*;
- *Avoid Seen Riddles*, *Log Length*.

## Sources

A **source definition** is a kind of place: id, name, kind (inscription, artifact, sealed chamber, library, shrine), the runes (or concepts) it teaches, the bands it works at, an optional minigame id and an intro.

**Built-in sources:**
- five general ones: Ancient Inscription, Shrine of Principles, Forgotten Library, Magical Artifact, Sealed Chamber;
- six themed inscriptions: Gale, Ember, Stone, Depth, Living, Mind.

**Coverage.** Every rune is covered by at least two sources (the general ones plus its theme), because a wrong answer burns the **source definition** for that concept (decision D7). The database inspector reports concepts that nothing teaches or that only one source teaches; *Rune Magic ▸ Report Concept Coverage* prints a full report.

### Putting sources in dungeons

1. *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Create Concept Discovery Source Prefab*, or *Add Concept Discovery Source to Selected* on your own model. This gives it a collider and the **Concept Discovery Source** component.
2. Pick its **Source Id** (a kind from the database) in the inspector. *Tier Override* is −1 to follow the dungeon; *Fixed Concept* forces one concept (for hand-placed content).
3. Add the prefab to a **Dungeon Prop Table** entry (kind *Interactable*; areas such as Library, Shrine, Crypt, Secret). The dungeon spawns it and its band follows the dungeon's tier.
4. Use several different source kinds in one dungeon, so a wrong answer never closes every door.

A source is never used up: what a player can still learn there is their own, saved with their soul.

## Riddles

**Written riddles** (*Data ▸ Riddles*): id, the concept, the text, optional wrong answers (concept ids; empty = the rune's other concepts), and the bands it is used at. The Greater Wind **Pressure** riddle of the design spec (17.3) is built in, verbatim, with its four options Flow, Pressure, Movement, Turbulence. Eight more are written for Wind, Fire and Vibration concepts.

**Generated riddles** cover every other concept, built from the concept's aspects:
- clues about what it is bound to (the aspects' own descriptions, strongest first);
- at the lower bands, what it is *not* (the strongest aspect of a wrong answer);
- a plain "I let a Mage ..." clue only at Beginner.

Rules for generated riddles:
- They never contain the answer's name.
- Higher bands give fewer clues.
- Later steps use other aspects.
- The options are the rune's other concepts, topped up from related runes. They have unique names, in shuffled order.

**Riddles already seen are avoided.** When every written riddle of a concept has been seen, a generated one is used.

## What the server decides

Everything is decided where the rules run (offline, or the server):
- **The concept** is picked in secret, weighted by rarity for the band, from what the player can still learn: not known, not burned for this source, of a rune they carry.
- **The minigame** is issued from a seed. The client reports the seconds at which it sealed each glyph; the server replays them against the same pure model.
  - Reports are refused when the count is wrong, the timing is impossible (faster than a hand, past the time limit, ahead of the server's clock), or a number is not finite.
  - An invalid report counts as a failed try.
- **The answer** is evaluated on the server. The player's machine receives the riddle text and shuffled option labels only. It never receives the concept, the correct option, the later steps or the burn keys.

**Failing the minigame** uses one try (*Attempts*); the last one burns the source for that concept.

**Network:** the owner's client gets the discovery view and sends `minigame`, `answer` and `abandon` requests. They are rate-limited and checked again.

## Saving

**Learned concepts** live in the player's knowledge, per rune definition (decision D13).

**Discovery state** is saved with the soul:
- burned `source|concept` keys;
- minigame tries used;
- riddles seen;
- a log of the last attempts (outcome: learned, wrong, minigame_failed, abandoned, expired);
- counters.

A save from before discovery loads with an empty discovery state.

## Content packs and your own minigames

- **More content:** put sources and riddles in a content-pack database (*Extensions*). The same id replaces.
- **Your own minigame:** implement `IDiscoveryMinigame` (create a challenge from a seed, evaluate a report), register it with `DiscoveryMinigames.Register`, and name its id in a source definition. The server side is pure data. The built-in window plays the rune-tracing challenge only, so a different minigame needs its own view of its challenge.

## Debug

**Debug panel** (the `RuneMagicDebug` input action, or the editor menu):
- *Band ▸* picks a band;
- *Start discovery* runs one without a dungeon;
- *Reset discovery* forgets burned sources and the log;
- the panel shows the learned, failed and burned counts;
- *Learn a concept* unlocks one directly.

**Logging:** the Rune Soul's *Log Events* logs each start (the concept is never logged).

## Tests

*Window ▸ General ▸ Test Runner ▸ Edit Mode*:
- **`RuneMagic.Tests.ConceptDiscoveryTests`** (pure; 34 tests) covers:
  - the bands and the spec's sample riddle;
  - candidate rules: known, burned, rarity, held runes;
  - option building: unique, shuffled, name collisions;
  - generated riddles never naming the answer, across all concepts and bands;
  - the minigame and its server-side validation;
  - pluggable minigames;
  - the state machine: unlock, burn, tries, multi-step, abandon, interruption;
  - the view holding no secret;
  - content packs, validation, coverage, upgrade, saving, the log.
- **`RuneMagic.Tests.ConceptDiscoveryRuntimeTests`** (category `RequiresEngine`) covers the source component and a soul that is not a rune user's.
