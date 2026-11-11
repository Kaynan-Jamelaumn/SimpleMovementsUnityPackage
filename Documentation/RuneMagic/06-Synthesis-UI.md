# Rune Magic 06 — The synthesis window

The **Rune Synthesis window** is how a Mage uses the [synthesis engine](05-Synthesis-Engine.md): the nine steps of design spec 11.1, from choosing a purpose to reading the result. It only *asks*: every choice is kept in a small pure model, the preview is the engine's own (so it says exactly what the server will say), and the server decides and answers.

## Opening it

- The **Synthesis** button of the **Rune Soul window**, or
- the Input Action named in *Rune Magic Database ▸ Settings ▸ Synthesis Action* (default `Synthesis`). **No key is fixed in code.** *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune Magic Input Actions* adds the action to your Input Actions asset without a key: bind it there; players can rebind it in Key Bindings. An action that is missing does nothing (the button still works).
- In the editor: *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Toggle Synthesis Window (Play Mode)*.

Only rune users (Mages) can open it. It is pointer UI; Escape closes it, and it frees the cursor while open. Closing keeps your formula: reopening continues where you left off.

## The nine steps (design spec 11.1)

The step bar at the top shows the steps that apply and takes you back to any step you have completed. *Make:* (Spell / New rune / Rank advancement) is chosen on the first steps.

| # | Step | What you do |
|---|---|---|
| 1 | **Purpose** | Choose the **intention**: what you are trying to accomplish. |
| 2 | **Effect** | Choose the **desired effect**, listed by category. By default only the effects your purpose supports are shown (*fully supported* / *partly supported*); one click shows them all. An effect too complex for your soul's present condition is listed but unavailable, with the reason. |
| 3 | **Runes** | Pick the runes from your soul. Each shows its **rank, quality, energy / maximum** and how many of its concepts you know. A rune that **anchors a spell** is marked; in a synthesis that uses runes up it cannot be chosen (the reason says "dismiss the spell first"). The count rule is shown (*Choose 2 to 12 runes*; *exactly 7 runes of the same rank*). |
| 4 | **Concepts** | For each rune choose **one concept you have discovered**. A rune with a single discovered concept is chosen for you; one with none blocks the step ("find knowledge sources in dungeons"); a concept that needs a higher rank is shown, unavailable. |
| 5 | **Order** | Arrange the runes with ▲ ▼. The order changes the outcome: finding the right sequence is part of your research. |
| 6 | **Energy** | A slider for each rune from 0 to what it holds, with **Least** (the minimum the preview says it needs) and **All**. Each rune says *needs at least N*, *not enough* or *far too much*. Runes start at half of what they hold. |
| 7 | **Review** | The estimated requirements: what each rune gives, needs at least and holds; a fuzzy estimate of the score and its tiers; the **risk** level and the chance the magic falters; the **Soul Damage** range; warnings; what the soul's condition asks. |
| 8 | **Confirm** | An optional name, the warnings, and **Synthesize**. |
| 9 | **Result** | The tier, the score, what was made, the flaws, the energy used or lost, the Soul Damage taken, destroyed runes, and a hint about what held it back. |

The panel on the right always shows **your formula so far** and what your **Soul Integrity** allows (its condition, how many runes, whether complex effects are available, the extra chance of faltering): design spec 11.1 asks the UI to show each rune's energy, concepts and rank, and the Soul Integrity requirements.

### Rank advancement

Seven runes of **one kind** need no formula, so the steps are only **Runes → Energy → Review → Confirm → Result**. Seven different runes of one rank make a composite rune of the next rank: the runes come first, then the purpose, effect and concepts.

### Warnings and the second click

If the preview says **runes could be destroyed** (the risk is in destruction territory) or that a bad outcome **could collapse your soul**, the Confirm step shows a red warning and *Synthesize (risky)* needs a **second click within five seconds** (*Confirm: runes could be destroyed!*). The button is disabled with the reason when you cannot synthesize at all (in combat, meditating, soul collapsed, no room for the result...).

### The result

After you confirm, the window waits for the server (up to 12 seconds). If the synthesis was **refused**, nothing was spent: you are returned to the Confirm step with the reason. If it was **performed**, the Result step shows what happened. *Try again with the same formula* keeps the purpose, the effect and the runes that are still there; *Start a new synthesis* clears everything.

## What the server decides

The window never decides anything.

- **Preview.** `RuneSoul.PreviewSynthesis` runs the same engine on the runes the server sent (on a client, a copy rebuilt only when they change). A test checks that a client's preview equals the server's, field for field, and that what the preview announces is what the server then does (the estimate holds the score, destruction was warned of, the damage is within the announced range).
- **Request.** A client sends the formula as JSON (`RuneRequestKind.Synthesize`, wire value 9, up to **8192** characters: 24 runes with the longest ids fit). The server decodes it with `SynthesisRequestCodec.TryDecode`: too long, unreadable, an unknown mode, no runes or more than 24, an invalid energy (negative, NaN, absurd), ids that are too long or hold control characters: all refused. The **seed never travels**: the server chooses the chance. Then the ordinary resolver and engine check the request against the soul again. Requests are rate-limited per player, like every other.
- **Answer.** A refusal is the soul's message (shown in the window). A performed synthesis sends a **report** with the rune list, so the result and the changed runes arrive together; the owner never receives anything about other players' souls (decision D18). The report holds names, not ids, and nothing secret: the score is shown *after* the attempt.
- **Host and offline.** The rules run directly: the same window, no network.

## For developers

| Piece | Where | Does |
|---|---|---|
| `SynthesisDraft` | `RuneMagic/Core/Synthesis/SynthesisDraft.cs` | The nine steps as pure logic: which apply, what each offers, what completes it, the request it builds. Knows only what the player may know. |
| `SynthesisRequestCodec` | `SynthesisNetwork.cs` | Encodes a request; decodes and validates what a client sent. |
| `SynthesisReport` | `SynthesisNetwork.cs` | The result for the result screen (also in `RuneListView.report`). |
| `SynthesisReplica` | `SynthesisNetwork.cs` | The state a client previews with, from what the server sent. |
| `SynthesisWindow` | `RuneMagic/UI/SynthesisWindow.cs` | A thin renderer over the above. Built from the same pieces as the Rune Soul window (`InventoryUIFactory`, `SocialUI`, `UITheme`). |
| `RuneSoul.RequestSynthesis / LastReport / SynthesisSnapshot` | `RuneSoul.Synthesis.cs` | Ask (any machine), the latest result, the runes a synthesis reads. |

To add a step or change what a step offers, change `SynthesisDraft` (and its tests) first; the window follows `draft.Steps()`.

## Decisions and limitations of this phase

1. **Rank advancement starts with the runes** (a pure advancement needs no formula), instead of the purpose as in the nine steps of a spell.
2. **The preview is what the engine allows a player to know**: the least energy per rune, a fuzzy estimate, the risk. The ideal energy and order are never shown, and *Least* sets only the minimum.
3. **A request is capped at 24 runes** by the network limit; *Max Runes* above it raises a validation warning.
4. **The score is shown after the attempt** (design spec 12.2: the exact percentage matters); the result screen shows only the latest result. The saved research log exists (the last 30 attempts) but has no screen yet.
5. **Pointer only.** Like the discovery window, no key is read except the named opening action; no keyboard navigation of the steps.
6. **Not checked in Unity.** The layout is built from the same pieces as the Rune Soul window, and everything it depends on is tested without Unity, but I could not look at it on screen: expect to adjust sizes and spacing in `SynthesisWindow.Build` once you see it.
7. **Still open: D16** (are your local copies of the ability files identical to git HEAD?). This phase touches none of them.
8. **No spellbook or casting yet** (Phase 7): spells exist, anchor their runes and can be dismissed from the debug panel; assigning them to ability slots is the next phase.

## Tests

`CustomEditor/RuneMagic/Tests/SynthesisUiCoreTests.cs` (pure): the nine steps and which apply, step gating, effects by support and soul condition, runes and concepts offered (anchored, rank, discovered), ordering and energy editing, a draft built with the window's own operations making a Perfect synthesis, the codec's round trip and everything it refuses, the report, **a client's preview equal to the server's**, and **the preview announcing what the server then does**. `SynthesisRuntimeTests` (needs the Unity engine) checks that the window and the soul refuse non rune users.
