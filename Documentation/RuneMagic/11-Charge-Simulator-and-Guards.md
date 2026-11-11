# Rune Magic 11 — Hold to charge, the spell simulator and the grimoire's guards

Three additions made after the grimoire (batch 1 of the second round of features). None is in the design spec.

## 1. Hold to charge (abilities and spells)

**What the player does:** press the key of a chargeable ability and **keep holding it**: a bar under the crosshair fills. **Let go** to cast with the charge: the longer the hold, the stronger the cast, up to a maximum (the cast goes off by itself there, or waits for the key, as you choose). A tap shorter than the minimum casts the ability as usual (or cancels it, as you choose). Stun, silence, death, a changed slot or an empty resource drop the charge; optionally taking damage does too.

**What a charge multiplies** (a *full* charge reaches the numbers below; a partial charge is in between along a curve): damage, healing, crowd-control duration, range, area, lingering duration and cost (`ChargeProfile`, `Essentials/Ability/Core/AbilityCharging.cs`). Defaults: damage ×2, healing ×2, control ×1.5, range ×1.25, area ×1.25, duration ×1.25, cost ×1.5; minimum 0.35 s, maximum 2.5 s.

### Any ability (not only the Mage's)
1. Add **Ability Charger** (*SimpleMovements ▸ Abilities ▸ Ability Charger*) to the player, next to the Player Ability Controller. The Player Setup Validator lists it (Optional) with an **Add** button.
2. In its **Rules** list, add the ability (an `AbilityDefinition`) and give it a profile. Abilities that are not listed, abilities cast from a button (they have no key to hold) and click-to-confirm abilities are cast as always.
3. The key is the ability's own Input System action (nothing is fixed in code).

### Rune spells
- *Rune Magic Database ▸ Balance ▸ Spells ▸ Charge* is the profile every chargeable spell uses; **Charge Strain At Full** (×1.5) and **Charge Instability At Full** (+12% misfire chance) are the Mage's extra price. The cost multiplier also scales the **Rune Energy** the cast draws.
- A **spell form** is chargeable when its **Chargeable** flag is on (built in: bolt, blast, cleave, shockwave, vortex; data version 9 gives older databases those five). A spell that uses the **Spelling System** is never charged (a key sequence and a hold would fight over the key).
- A charge the anchored runes cannot pay for goes off as an ordinary cast. The tooltip says *Hold the key to charge (…)* with the numbers. Rune Magic adds the Ability Charger to a Mage's player by itself.

### How it works (and its limits)
- The charger takes the key press through the player controller's pre-cast hook (now shared by several systems, in order: the Spelling System's key sequence, then the charge) and, at release, casts with `CastRequest.charge` (0–1).
- **Where the rules run** (offline, host, server) `AbilityCaster.StartCast` asks `AbilityCharging.Resolve` for the charge it will keep (clamped to 0–1, nothing if the ability cannot be charged, nothing if a source cannot pay) and puts its multipliers into the cast's stats; the cast, its cooldown, its effects and its network copies then are as for any ability. An uncharged cast is exactly what it was.
- **Network:** the client's request carries the charge (one byte). The server cannot see the key, so it keeps no more than the time since the slot's previous cast request allows (plus a quarter second): a modified client cannot get a full charge faster than a player who held the key, but it can claim a full charge without holding it after waiting. Holding is time the player spends, not a proof; if that matters for a competitive game, add a server-side press/release message.
- **Existing project files this changes** (all small, additive): `AbilityCastInstance.cs` (`CastRequest.charge`, `AbilityCastInstance.Charge`), `AbilityCaster.cs` (`StartCast`), `PlayerAbilityController.cs` (the pre-cast hook runs every subscriber; `InputOfSlot`), `NetAbilityMessages.cs` and `NetAbilityCaster.cs` (the charge byte and the plausibility bound), `PlayerSetupValidator.cs` (the Ability Charger check).
- Not done: moving slower while charging, a charge animation or effect, charging from a button.

## 2. The spell simulator

*Tools ▸ SimpleMovements ▸ Rune Magic ▸ **Spell Simulator*** builds 3,000 random spells from the database (a random effect, rank 1–5, complexity, power and stability), compiles each the way the game does and logs: the spread of **energy, strain, punch** (damage factor), **punch per second** (cast time and cooldown), **punch per 1,000 energy**, **punch per strain** and **casts to Strained**; the averages **by rank** and **by form**; and the **outliers**: builds more than three inter-quartile ranges beyond the quartiles of their rank. *Spell Simulator (fully charged)* does the same with every chargeable spell held to a full charge (the hold counts as time).

Reading it: punch is compared only among **attacks** (offense effects); the other effects are measured by cost and strain. An outlier on *punch per 1,000 energy* is a form or effect that is too cheap or too dear for what it does; on *punch per strain*, too safe or too risky. The built-in data has **no outlier** (a test keeps it so, charged or not); change a table, run it again, and read what moved. It is pure (`SpellSimulator`, `RuneMagic/Core/Balance/`): the same seed and data give the same report.

## 3. The grimoire's guards

Copying a rune costs energy and a slot, so a loop of imprint and absorb is how a script would duplicate runes. *Balance ▸ Grimoire ▸ Against duplication and abuse* (0 = no limit):

| Field | Built-in |
|---|---|
| Imprint Cooldown Seconds / Absorb Cooldown Seconds | 3 / 2 |
| Max Imprints Per Hour / Max Absorbs Per Hour | 40 / 40 (per Mage, per session of the game) |
| Log Entries | 200 per book |
| Log To Console | off |

- A Mage who goes too fast is told why and for how long (*Your hand is still steadying…*); the refusal is written in the book's log; nothing changes.
- **Every book keeps an audit log**: imprint, absorb, erase and refusals with the player's key and name, the page, the rune (rank, quality; a composite rune by name) and the time. *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Log Grimoire Audit (Play Mode)* prints the logs of every book of the world (where the rules run). The log is saved with the book.
- The limits are in memory per soul: they start again when the game restarts (the cooldown a Mage waits through is no weaker for that).

## Tests

Without Unity: `ChargeAndSimulatorTests` (the profile's math and curve, the modifiers, validation, the request's default, the spell plan with a charge, the chargeable forms, the data upgrade to version 9 and a designer's choice kept, the tooltip, the simulator: reproducible, ordered, no outlier in the built-in data, a planted too-cheap form found, percentiles) and `GrimoireGuardTests` (cooldown, hourly window, no limit, erasing never limited, the log's size, its JSON, the audit text, the balance's warnings). In Unity's Test Runner (category *RequiresEngine*): `ChargeRuntimeTests` (the registry with ability assets, a refused charge, the stats a charge builds).

## What to check in Unity
1. Add the Ability Charger to the player, list an ability, hold its key: the bar fills, the release casts it stronger and costs more; a tap casts it normally.
2. As a Mage, hold the key of a bolt-form spell: the Rune Energy drawn and the Soul Strain are higher; a spell with too little energy casts uncharged.
3. In a network session, charge as a client: the server's cast is charged and the others see its stronger effect.
4. Imprint twice in a row: the second is refused with the wait; check the log with the menu.
