# Rune Magic

A knowledge-based magic system for the **Mage** class. Mages do not absorb abilities (essences) from monsters. Instead they:

1. Absorb **Runes** into their soul.
2. Discover the hidden **Concepts** of each rune.
3. Combine runes in a **synthesis** to make new runes and spells. A synthesis is chosen by **Intention** (purpose) and **Desired Effect** (result), with an order and energy for each rune.
4. Cast those spells from the normal ability bar.

The magic is limited by **Rune Energy**, **Soul Rune Capacity** and **Soul Integrity**.

Every other class keeps the ability system exactly as before.

> The system was built in nine phases and is complete. This folder documents it; the [integration plan](INTEGRATION_PLAN.md)
> describes the whole design and what each phase added, and [09 — Acceptance report](09-Acceptance-Report.md) says what was verified and what only Unity can confirm.

## Status

| Phase | Content | State |
|---|---|---|
| 0 | Analysis and integration plan | Done ([plan](INTEGRATION_PLAN.md)) |
| 1 | Data foundation (runes, alignments, concepts, intentions, desired effects, compatibility rules, balance tables) and Mage-only gating | Done ([data and gating](01-Data-and-Gating.md)) |
| 2 | Soul resources: Soul Rune Capacity, Soul Integrity (conditions, deep damage, Soul Collapse), recovery (rest, meditation, items, rituals, sanctuaries, modifiers), saving, network, debug panel | Done ([soul resources](02-Soul-Resources.md)) |
| 3 | Rune acquisition (drop profiles, mob hook, kill rewards), rune items, absorption with capacity checks, release, Rune Energy (pending pool, player-chosen rune, full/overflow rules), Rune Soul window; input only through Input Actions | Done ([acquisition and energy](03-Acquisition-and-Energy.md)) |
| 4 | Concept discovery in dungeons: knowledge sources, rune-tracing minigame, riddles (written and generated), permanent unlock, burned sources, five difficulty bands | Done ([concept discovery](04-Concept-Discovery.md)) |
| 5 | Synthesis engine: scoring and tiers, order and energy, insufficient energy against invalid formulas, spells that anchor runes, composite runes, rank advancement, flaws and failure penalties, Soul Damage, preview, the Synthesis Lab | Done ([synthesis engine](05-Synthesis-Engine.md)) |
| 6 | Synthesis UI: the nine-step window (purpose, effect, runes, concepts, order, energy, review, confirm, result), the preview, risk and collapse warnings, the validated client request and the result report | Done ([synthesis window](06-Synthesis-UI.md)) |
| 7 | Spells and shared ability slots: spell forms (all 101 effects), the Spellbook, spells on the existing ability keys and bar, Rune Energy cost from the anchored runes, Soul Strain, misfires, casting debuffs by Soul Integrity condition | Done ([spells and slots](07-Spells-and-Slots.md)) |
| 8 | Spelling System: key-sequence casting set by the developer / server (mode None / Selected / All, rules by form, effect or archetype), a window per key, rhythm, speed bonus, Strict and Lenient models, Soul Damage on mistakes (configurable, never automatic), a key pool that never uses movement keys (rebinds included), server-validated, accessible overlay | Done ([spelling system](08-Spelling-System.md)) |
| 9 | Integration, balance and regression QA: the whole loop end to end, the acceptance checklist (37 of 37), regression guards for non-Mage classes, essences, abilities, mobs and saves, an independent review of the runtime flows (12 defects fixed), the Balance Report and the spec's worked example in the defaults, the data reference, the extending guide | Done ([acceptance report](09-Acceptance-Report.md), [data reference](09-Data-Reference.md), [extending guide](09-Extending-Guide.md)) |
| 10 | After Phase 9: the **Grimoire** (every Mage begins with a book; a rune of the soul is imprinted on a page at a cost in energy, a print is absorbed once with no energy, another Mage who holds the book reads it), the player validators that find and add the Rune Magic scripts (and the crafting journals), the energy-only network update | Done ([the grimoire](10-Grimoire.md)) |
| 11 | After the grimoire (batch 1): **hold to charge** abilities and spells, the **spell simulator**, the grimoire's **anti-exploit guards and audit log** | Done ([charge, simulator, guards](11-Charge-Simulator-and-Guards.md)) |

## Quick start

1. *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Rune Magic Database (Resources)*.
   - This creates `Assets/Resources/SimpleMovements/Settings/RuneMagicDatabase.asset`, filled with the built-in content.
   - Without the asset, the built-in content is used anyway.
2. Make sure your Mage class is recognised. By default, any class whose **asset name**, **display name** or **Class archetype name** is `Mage` is a rune user. You can also:
   - list other names in *Settings ▸ Rune User Class Names*, or
   - drag class assets or Class archetypes into *Rune User Classes* / *Rune User Archetypes*.
3. *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Validate Rune Magic Database* reports content problems. A fresh database reports none.
4. Select the player prefab and use *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune Magic to Selected Player*. This adds the Rune Soul (the HUD's Soul gauge), the Meditation Controller and, when networked, NetRuneMagic. It is inert on other classes.
5. *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune Magic Input Actions* adds the `Meditate`, `RuneSoul`, `Synthesis`, `Spellbook`, `Grimoire` and `RuneMagicDebug` actions to your Input Actions asset **without keys**: bind them in the Input Actions editor. No key is fixed in code; players rebind them in Key Bindings.
6. To let dungeons teach concepts: *Rune Magic ▸ Create Concept Discovery Source Prefab*, then list the prefab in a Dungeon Prop Table. Players interact with it (the normal Interact input) and play a minigame and a riddle in the Discovery window (pointer only).
7. To see how the pace of the whole game is set by your tables: *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Balance Report* (energy per kill, kills per drop, the cost of every archetype, casts per rune, recovery time). To add content, read [09 — Extending the system](09-Extending-Guide.md). To see how the synthesis engine judges a formula: *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Synthesis Lab* (no scene needed). In play mode the debug panel can synthesize the sample formulas.
8. In play mode, the `RuneSoul` action opens the Rune Soul window (runes, energy, absorbing); its **Synthesis** button (or the `Synthesis` action) opens the Rune Synthesis window, and its **Spellbook** button (or the `Spellbook` action) opens the Spellbook, where spells are put on your ability keys; spells are cast with the ordinary `Ability1…` actions. A spell the Spelling System marks asks for a few letters first (nothing to bind: the letters are never keys you use for anything else). In the editor and development builds, `RuneMagicDebug` (or *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Toggle Debug Panel*) opens the debug panel.

## Folder map

| Folder | Contents |
|---|---|
| `RuneMagic/Core/` | Plain data and rules, no MonoBehaviours: definitions, balance tables, settings, the compiled catalog, the built-in content (`Defaults/`), the saved player state (`State/`), the soul rules (`Rules/`) |
| `RuneMagic/Runtime/` | The database asset, the Mage gate, the Rune Soul and its status, saves, meditation, sanctuaries, ritual sites, attunements, input, rune items, the Rune Source mob hook, the kill listener |
| `RuneMagic/Items/` | Soul Restorative consumables, rune items |
| `RuneMagic/Core/Discovery/` | The pure discovery rules: minigame, concept choice, riddles, state machine |
| `RuneMagic/Core/Synthesis/` | The pure synthesis engine: resolver (what may be attempted), evaluator (the score), archetype matching, outcomes (flaws, spells, composite runes, failure penalties, Soul Damage), the engine (preview, execute) |
| `RuneMagic/Core/Spells/` | The pure spellcasting rules: the spell compiler (form, cost, strain, modifiers), casting (plan, energy draw and refund, misfires, strain), the key assignment rules |
| `RuneMagic/Core/Spelling/` | The pure Spelling System: spell parameters (generated and by rule), the key pool, the sequence, the replay and score, the server's validation and the wire format |
| `RuneMagic/UI/` | The Rune Soul window, the Synthesis window, the Spellbook, the Grimoire window, the Spelling overlay, the Discovery window, the debug panel |
| `Networking/Core/RuneMagic/`, `Networking/Runtime/RuneMagic/` | Requests and their guard (always compiled); NetRuneMagic (FishNet) |
| `Essentials/Ability/Core/AbsorptionGate.cs` | The generic hook the ability system consults to keep a character out of ability absorption (knows nothing about runes) |
| `Essentials/Ability/Core/ICastable.cs` | The generic interface a slot can hold for costs of its own (rune spells use it; knows nothing about runes) |
| `RuneMagic/Core/Balance/` | The Balance Report: the pace of the whole loop read off the tables |
| `CustomEditor/RuneMagic/` | Menus, the database inspector, the Synthesis Lab, the Balance Report menu, Edit Mode tests |
