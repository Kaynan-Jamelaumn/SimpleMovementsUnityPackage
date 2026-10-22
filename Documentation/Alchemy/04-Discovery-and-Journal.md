# Alchemy 04 — Discovery and the Journal

**Scripts:** `Alchemy/Core/AlchemyJournal.cs` (the rules of learning), `Alchemy/Runtime/AlchemyKnowledge.cs` (the
player's journal, saved), `Alchemy/UI/AlchemyStationUI.cs`.

No recipe is ever revealed. Players learn the way an alchemist does: by trying things, watching, and keeping notes.

---

## 1. What the player knows

Every player has an **Alchemy Knowledge** component (added the first time they use a station) holding an
`AlchemyJournalData`:

| Knowledge | Learnt by |
|---|---|
| **Results made** (discoveries) | Bottling, collecting a distillate or emptying a vessel: the result is recorded with its best quality and **the steps of the best preparation** |
| **Reactions seen / understood** | Seeing a reaction happen in a preparation they tend (at least *Observe Amount* of it). Seen in *Observations To Understand* (2) different preparations, it is **understood**: named, with its equation and conditions |
| **Identified substances** | Understanding a reaction identifies the substances it names; bottling a result identifies its compound; a **transparent vessel** identifies whatever is in it |
| **How substances behave** | Seeing an identified substance settle out of solution notes how it dissolves in each solvent ("Dissolves well in Water, poorly in Grain Spirit, hardly in Lamp Oil — more when hot"); understanding its breakdown reactions notes its limits ("Breaks down above 85 °C", "Acid destroys it (below pH 3)"); identifying its crystals notes how to grow them; understanding a clash notes what it cannot coexist with |
| **Ingredients used** | Each use; the journal shows what they contain, as far as the substances are identified |
| **Experiments** | The last 40 finished preparations: result, quality, vessel, steps, notes |

It is saved per character in `persistentDataPath/Alchemy/<character name>.json` (*Save To Disk*; *Save Key* to choose the
file).

## 2. What the window shows

Everything goes through that knowledge:

* An unidentified substance is shown by **what it looks like** ("a deep rose liquid that smells of summer"); identified,
  by name and share.
* A reaction not yet understood is shown by **what is seen** ("the rose darkens to amber and warmth rises from the
  vessel"), with how many preparations it was seen in and between which temperatures; understood, by its rule
  ("Kindling: Vital Essence → Ardent Essence · 75–400 °C · faster when hotter · faster with Ember Dust · slowed by Brine ·
  stopped by Lead Dust").
* **If bottled now** names the result only if the player has made it before; otherwise *Unknown result — bottle it to find
  out*. The notes (diluted, impure, ingredients still giving up their substances, part of it undissolved, residues) are
  always shown: they describe the mixture, not a recipe.
* Undissolved matter is shown as what it looks like until identified; *Something will not stay dissolved: a silvery,
  shimmering dew* in the log tells the player the solvent or the amount is wrong.
* The log speaks the player's language: *Something new appears: a calm teal liquid* until the substance is identified.
* What the minigames do is shown as it happens, never as a recipe: the mortar names the fineness reached and what it does
  ("gives up its substances ×3 as fast, brings out the locked-in parts too"); the fire's trace says whether the
  temperature is *held steady*, *wavering* or *swinging*; the stirring is *steady*, *uneven* or *erratic*; the status
  shows the pH and the layers; the log says when crystals start forming, the brew slops over, the fire goes out, the
  filter catches something.

## 3. Learning patterns

The rules are consistent, so experiments teach:

* The same ingredients and steps always give the same result (*Discoveries* lists the steps; doing them again works).
* Changing one thing at a time shows what it does: the fire (the window shows the target temperature), the timing, the
  order, the amounts, the vessel, the grind, stirring (how fast, how evenly), a bath, a splash of vinegar, filtering, the
  time of day.
* Understood reactions say their temperature windows and what speeds them up or stops them — the player can plan:
  "keep it below 75 °C and the healing essence stays", "add frost to the amber stage while it is hot".
* Ingredients the player has used show what they contain once those substances are identified, so new combinations can
  be reasoned about before trying them.
* Breakdown reactions are learnt like the others: after seeing "the hum fades as the violet pales" in two preparations,
  the player knows mana essence breaks down above 85 °C — and that more fire would not have helped.
* The *Procedure* of a discovery says how each ingredient went in ("Added 3 × Sunpetal (partially ground, unevenly)"),
  how the fire was tended and the vessel stirred, so the whole process is part of what is reproduced:

  | Operation | Procedure line |
  |---|---|
  | Ingredient | `0:00  Added 3 × Sunpetal (finely ground)` — with *unevenly* / *very unevenly* for an uneven grind |
  | Fuel, bellows, air vent | `0:00  Put 2 × Firewood on the fire` · `0:40  Worked the bellows × 6` · `1:10  Air vent 40% open` / `closed (fire choked)` |
  | Burner | `0:00  Fire 24% (→ 73 °C)` |
  | Stirring | `0:20  Stirred at about 45% for 0:40, unevenly` · `1:00  Stirred × 3` (single stirs) |
  | Bath | `2:00  Stood it in a water bath` / `an ice bath` / `Took it out of the bath` |
  | Processing | `4:00  Filtered through the filter paper` · `Poured off the top layer` · `Skimmed the surface` |
  | Residue | `0:00  (0.4 u of Bitter Sludge left in the vessel by the last preparation)` |
  | Results | `3:00  Bottled 7 × Healing Draught (Potent)` · `Collected the distillate (Honey Brandy)` |

## 4. Failed experiments

Failures are results with their own names and items (*Cloudy Infusion* — taken off too soon; *Spoiled Mixture* — went
too far; *Unstable Concoction*; *Murky Mixture*; *Plain Liquid* — nothing to bottle). They are recorded as discoveries
and experiments like any other result, with the notes explaining why: a failure is information.

## 5. Reproducing a discovery

A discovery keeps the steps of its best preparation ("0:00 Added 2 × Spring Water · 0:00 Added 3 × Sunpetal · 0:00 Fire
24% (→ 73 °C) · 3:00 Bottled 7 × Healing Draught (Potent)"). Same steps, same vessel, same surroundings ⇒ the same result: the
simulation is deterministic ([02 §14](02-Simulation-Rules.md#14-reproducibility-and-randomness)). The Alchemy Lab can replay a
live preparation's exact history ([07](07-Editor-Tools-and-Testing.md)).
