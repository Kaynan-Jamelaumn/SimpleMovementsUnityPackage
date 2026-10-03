# Mobs 03 — Combat & Abilities

**Scripts:** `Mob/Controller/MobAbilityController.cs`, `Mob/AI/MobAbilitySelector.cs`, `MobCombatCoordinator.cs`,
`Mob/Controller/MovementStateMachine/States/MobCombatState.cs`, `MobAttackingState.cs`, `MobDodgingState.cs`,
`Essentials/Ability/Core/AbilityDefinition.cs`, `AbilityAbsorption.cs`.

---

## 1. What a mob attacks with

| Source | When | Set up |
|---|---|---|
| **Ability slots** (`MobAbilityController`) | Always, when it has any | Ability Definition assets: melee bites, claws, projectiles, novas, charges, summons, heals… |
| **Basic attack** | When it has no melee ability and the profile's *Auto Basic Attack* is on | Built from the mob's *Bite Damage*, *Attack Distance*, *Bite Cooldown*; **Make It An Asset** in the abilities inspector turns it into an editable ability |
| Legacy abilities | Old `AbilityEffectSO` holders | **Convert All To Ability Definitions** in the inspector |

Mob and player abilities are the **same assets**: an ability made for the player works on a mob and the other way
round.

### Adding abilities

1. Select the mob's `MobAbilityController`.
2. **Add Ability From Preset ▾** — creates an Ability Definition next to the prefab and puts it in a new slot — or drag
   an existing Ability Definition into a slot.
3. Set the ability's **Mob AI** hints (below). **Remove Empty Slots** cleans up.
4. A **Cast Point** (a child at chest height: *Create*) is where projectiles start; **Find** assigns the Animator.

---

## 2. How the mob chooses an ability

`MobAbilitySelector` scores every ready ability for the current moment: in range, line of sight, predicted to hit,
enough targets for an area, suited to the situation (gap closer when far, escape when in danger, control on a
casting target, heal when hurt, opener at the start of a fight), weighted by the ability's **AI priority**, the
slot's weight and the profile's *Ability Randomness*.

The **Mob AI** section of an Ability Definition:

| Field | Meaning |
|---|---|
| Priority | 0 = never used automatically, 1 = normal, 3+ = favourite |
| Preferred Min / Max Range | Distance band it is used in (0 = from the ability's ranges) |
| Require Predicted Hit | Only when the target is predicted inside the hit area (melee, areas) |
| Min Targets | Area abilities: only when at least this many enemies would be hit |
| Use Below Own Health | Heals, escapes, enrage |
| Use Below Target Health | Executes |
| Defensive / Opener | Preferred when in danger / at the start of a fight |
| Extra Cooldown | Extra wait for mobs (rarer big attacks) |
| Not While Ally Casting | Avoids several mobs stacking the same wall or cage |

The ability's **tags** (Gap Closer, Escape, Defensive…) tell the AI what it is for.

---

## 3. Fighting in groups

`MobCombatCoordinator` stops a group from stacking on one target:

* Only *Combat Settings ▸ Max Simultaneous Melee Attackers* (default 3) attack in melee at once, and
  *Max Simultaneous Ranged Attackers* (4) cast ranged abilities; the others circle at *Circle Distance* and wait.
  Turn *Use Attack Tokens* off in the profile for mobs that ignore the queue (bosses).
* Each mob gets its own angle around the target, so a pack **surrounds** it.
* **Call For Help**: when a mob spots an enemy or is hurt, allies within its *Call For Help Radius* join (if they
  answer from *Respond To Help Radius*).

## 4. Dodging and positioning

* **Combat Style** decides positioning: Melee closes in; Ranged keeps its range and backs away; Skirmisher hits then
  backs off; Kiter retreats while the target chases; Brute never backs off. Auto picks from its abilities.
* **Dodging** (profile): chance to jump out of telegraphed areas, chance to jump back from weapon swings,
  cooldown, distance, invulnerability, cancelling its own wind-up.
* Telegraphs: hostile abilities show their area on the ground during the wind-up
  (*Combat Settings ▸ Show Enemy Telegraphs*), so players can dodge too.

## 5. Who mob attacks can hit

A mob's ability or attack hits characters by **relation** (Hit Filter: Self, Allies, Enemies, Neutral, Party) and
optional **Target Rules** (friendly fire, kinds, factions) — never by physics layer. Pack members and allied factions
are never harmed unless friendly fire allows it. See
[Inventory 09](../Inventory/09-Teams-Factions-and-Targeting.md).

## 6. Absorption (the player learns abilities from kills)

`MobAbilityController ▸ Absorption`:

| Field | Meaning |
|---|---|
| Can Be Absorbed | Killing this mob can give the player one of its abilities (summons never can) |
| Absorb Chance Per Kill | % chance per kill (× the profile's *Absorb Chance Multiplier*) |
| Shares | Which ability is granted when the roll succeeds; buttons: **Split Evenly**, **Fit to 100%**, **By AI Priority**, **All 0%** |

Global rules are in *Absorption Settings* (*Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Absorption Settings (Resources)*).

## 7. Blocking, body parts and ranged weapons on mobs

Mobs share the player's combat components ([Inventory 10](../Inventory/10-Hands-Shields-Ranged-and-Body-Parts.md)):

* **Blocking:** add a `BlockController`. A mob without a Weapon Controller blocks with its *Shield Item* (a shield
  armor asset) or *Innate Defense* (on with *Use Innate Defense*). It raises its guard when it sees a melee swing
  coming (*AI Block Chance*, *Reaction Time*, *Hold Time*), sometimes when an enemy is close, and lowers it to attack.
  Arrows, spells and true damage follow the shield's rules like for the player.
* **Body parts:** add a `BodyPartController` (or turn on *Combat Settings ▸ Body Parts For Every Character*). The
  built-in humanoid profile is used unless you assign one; give non-humanoids (wolves, spiders) their own profile.
  `BodyPartHitbox` colliders on bones make hits exact.
* **Shields block mob attacks too:** a mob's melee hit from inside the player's shield arc, and its projectiles, are
  blocked; *Guard Damage* on its attacks breaks guards faster; *Unblockable* attacks ignore shields.
