# Player 15: Powers: size, invisibility, mirage, immunity and sky walk

A **power** is something a character *becomes* for a while: a giant, tiny, invisible, immovable, surrounded by copies
of itself, able to walk on air. Abilities give powers through the **Buff ▸ Power** effect. Items and set bonuses give
them through **Special ▸ Power While Worn**.

## 1. The abilities

*Tools ▸ SimpleMovements ▸ Player ▸ Generate Power Abilities* creates these assets in
`Resources/SimpleMovements/Abilities/Powers`. They are also listed in *Create ▸ SimpleMovements ▸ Abilities ▸ Ability From
Preset*.

| Ability | What it does |
|---|---|
| **Gigantification** | 80% bigger for 15 s. The model, CharacterController, hit capsule and body-part hitboxes grow with it. Max health grows in proportion (the current health keeps its fraction), and so do Strength, weapon damage, knockback and jump |
| **Minimization** | Half size for 15 s: 40% faster, harder to hit, but takes 40% more damage |
| **Shockwave Leap** | Leap to a point; landing deals damage, knockback and a short stun. **The shockwave grows with the caster's size**: a giant's covers twice the area |
| **Mirage** | 4 copies of you appear around you, playing the idle animation; you are invisible for 1.5 s. A copy vanishes after 6 s or when hit |
| **Greater Mirage** | 6 copies that stand, walk or run around (each changes its mind); you swap places with one of them and are invisible for 2 s |
| **Immovable Force** | 8 s immune to stun, root, silence, slow, taunt, knockback and pull (the crowd control already on you is removed); 15% less damage |
| **Vanish** | Invisible for 6 s; taking a hit or attacking reveals you |
| **Sky Walk** | Toggle: walk on air while you have mana (8 per second in the air). Jump rises, Crouch descends. Cast again to stop; it stops by itself when the mana runs out |
| Shadow Step | Blink 8 m and stay invisible for 3 s |
| Phantom Veil | 2.5 s invisible and invulnerable |
| Stone Form | 6 s rooted, immune to control, 50% less damage |
| Titan's Wrath | Double size and immune to control for 12 s |
| Shrink Ray | A bolt that shrinks an enemy by 40% for 6 s: it deals less damage and takes 30% more |
| Enlarge Ally | An ally becomes 50% bigger and tougher for 12 s |
| Cloud Step | Walk on air for 6 s, free |
| Smoke Bomb | Invisible for 4 s in a cloud that slows enemies |
| Feather Form | 30% smaller, faster, double jump and glide for 20 s |

## 2. The powers (Buff ▸ Power)

A **Power** effect has a **Duration** (0 = until a power ends itself or the ability is cast again), **Toggle** (casting
again ends it) and a list of powers:

| Power | Fields |
|---|---|
| **Body ▸ Change Size** | Size % (−50 to +100), grow time, and how much health, weapon damage, knockback, Strength and jump **follow the size** (1 = in proportion), plus extra move speed, damage taken and attack speed |
| **Stealth ▸ Invisibility** | Break when hit, break when attacking (after a short grace), hide meshes, the local player still sees itself, vanish effect |
| **Defense ▸ Immune To Control** | Which crowd control and displacement are ignored, remove what is already on, damage taken %, an effect shown while it lasts |
| **Illusion ▸ Mirage Copies** | Number, radius, motion (Idle, Walk, Run, Mixed), lifetime, vanish when hit, decoys (enemies attack them), caster invisible seconds, swap places with a copy, walk/run speed |
| **Movement ▸ Movement Behaviour** | Runs Sky Walk, Glide, Double Jump, Wall Climb... while it lasts (players: needs the Trait Manager) |

Casting the same ability again restarts the power, or ends it when **Toggle** is on. A power ends early when one of its
parts finishes: invisibility broken, Sky Walk out of mana, the character dead.

### How each power works

* **Size** uses the **Height** stat, so the **Character Body** scales the model and the CharacterController (feet stay on
  the ground), and the CombatEntity measures its body again, so hits and hitboxes follow. The Height stat is limited
  to half or double size.
* **Invisibility**: mobs cannot see an invisible character (*MobPerception.CanSee*), so they go to where they last saw
  it. `CombatEntity.IsInvisible`, `AddInvisibility` and `RemoveInvisibility` are available to your own code.
* **Immunity**: `CombatEntity.AddImmunity` and `RemoveImmunity` add to the inspector's *Immunities* for a while (several
  sources stack), and `ActiveImmunities` returns both.
* **Mirage copies** are copies of the character's model with its equipment and Animator. Scripts, colliders and physics
  are removed. Each copy gets a capsule of the caster's size and a CombatEntity of the caster's team, with the caster as
  its summoner, so enemies treat it like the caster and attack it. Moving copies follow the ground, turn away from
  walls and drive *MovementX/Z, MovementSpeed, IsWalking, IsRunning* (and *Speed*) on the Animator when it has them.
* **Shockwave size**: a Movement action's **Landing Grows With Size** (0 to 2) scales the landing area by the caster's
  Character Body scale.

## 3. Items with powers (Special ▸ Power While Worn)

The same powers can be given while an item is worn or a set bonus is active. The starter library includes:

* **Belt Of The Titan**: 15% bigger.
* **Giant's Gauntlets**: 10% bigger.
* **Pixie Ring**: 20% smaller and faster.
* **Boots Of The Sky**: walk on air for stamina.
* **Anchor Pendant**: immune to knockback.
* **Ring Of The Unbound**: immune to roots and slows.
* **Helm Of The Unyielding**: immune to stuns.
* **Sets**: Colossus Plate (25% bigger at 4 pieces), Featherlight Regalia (sky walk), Sprite's Trinkets (fae size) and
  Juggernaut Titan, which grows with every piece.

For Sky Walk on an item, turn off *End When Empty*: running out only makes you fall, and the boots keep working.

## 4. Writing your own power

```csharp
[System.Serializable, AbilityMenu("Custom/Glow", "Makes the character glow.", 0)]
public class GlowPower : CharacterPower
{
    public Color color = Color.cyan;
    private Light light;

    public override string Describe() => "glows";
    public override void Start(PowerContext ctx) { light = ctx.Root.gameObject.AddComponent<Light>(); light.color = color; }
    public override void End(PowerContext ctx) { if (light != null) Object.Destroy(light); }
}
```

It appears in the Power effect's list. `PowerContext` gives the CombatEntity, root, Character Body, Combat Stats, Trait
Manager, the source, the strength and the elapsed time. Set `Finished = true` to end the power. `OnOwnerDamaged` and
`OnOwnerActed` are called when the character is hit or attacks. Give a power from code with
`ActivePowers.For(entity).Run(key, "Name", powers, seconds, 1f, source)`.

## 5. Multiplayer

Powers work in network sessions with no change to your prefabs. **NetCombatStatus**, already on networked players and
mobs, carries them:

* The **server** (or host) runs every power for real: health, stats, immunities, damage, teleports, the copies that
  take hits and the gates.
* Whenever a character's powers change (one starts or ends, a copy vanishes, a phantom swaps), the server sends their
  state to every client that sees the character. A late joiner gets the last state.
* Each **client's copy** of the character runs the same powers in *presentation* mode (`PowerContext.IsPresentation`):
  * **Size:** the body, the CharacterController and the hitboxes take the same size, so the owner's prediction
    matches the server.
  * **Invisibility:** the meshes hide (except for the player who is invisible) and so does the name plate.
  * **Mirage:** the copies appear at the server's spots and wander the same way, from the server's seed. Each one
    vanishes when the server's copy does.
  * **Gates, links and the recall ghost:** they appear the same as on the server.
  * **Movement powers:** Sky Walk and the like run in the owner's prediction.
* A client rebuilds a power from the ability asset it was cast from (`PowerSources`), so its prefabs and effects match.
  When that is not possible (powers from items or code), it uses a JSON copy of the settings without prefab references.
* Characters are named by network id (`PowerNet`), for example a soul link's partner.

To make your own power multiplayer-ready:

1. Skip gameplay when `ctx.IsPresentation` is set.
2. Return what clients need from `NetDynamic()` and read it in `ApplyNetDynamic`.
3. Return a small changing state from `NetState`; clients receive it in `ApplyNetState`.
4. Call `ctx.MarkDirty()` when the dynamic data changes.

## 6. Fate, tricks and curses

These are also created by *Generate Power Abilities*:

| Ability | What it does |
|---|---|
| **Acid Blood Burst** | Costs 25 health. Acid bursts out around you: poison damage, poison over time, and two of each enemy's equipped items lose durability. One of yours does too |
| **Lucky Coin** | Flips a coin: a random stat (Strength, Agility, Defense, Critical Chance, Move Speed, Max Health...) goes up, or down, for 20 s |
| **Equivalent Exchange** | A random stat goes up and another goes down for 20 s |
| **Phantom Exchange** | Leaves a still phantom (a decoy enemies attack). Cast again while it stands to swap places with it; it vanishes when hit or after 10 s |
| **Gravital Pull** | Drags every enemy within 10 m to you |
| **Gravity Well** | A well at the aimed point keeps pulling enemies to its centre for 4 s |
| **Soul Link** | Links you to a target for 10 s: the damage either of you takes, the other takes too (a purple line joins you) |
| **Incoming Debt** | The target's next ability costs double, or its cooldown is double |
| **Heist** | Steals a random item from a player's bag into yours |
| **Mutual Exchange** | You and the target swap a random combat stat for 15 s (the one they are better at, when there is one) |
| **Twin Gates** | Two linked gates for 12 s: in front of you and at the aimed point. Players, mobs and summons that walk into one come out of the other |
| **Leech Life** | A blood tether drains the target for 5 s and heals you as much; it snaps beyond 14 m |
| **Chronal Recall** | A ghost marks where you stand; in 4 s you return there with the health you had |
| **Mirror Ward** | For 5 s, half of every hit you take goes back to the attacker |
| **Transposition** | Swap places with the target |
| **Mana Burn** | Burns 30 of the target's mana and deals 10 damage |
| **Blood Pact** | Spend 30 health to heal every ally within 8 m for 40 |

New effects behind them, also available in your own abilities:

* **Random ▸ Stat Gamble**, **Random ▸ Swap Stat**
* **Item ▸ Corrode Equipment**, **Item ▸ Steal Item**
* **Link ▸ Soul Link**, **Link ▸ Leech Life**
* **Debuff ▸ Incoming Debt**
* **Displacement ▸ Swap Places**
* The **Ground ▸ Twin Gates** action
* Powers: **Phantom Exchange**, **Chronal Recall**, **Reflect Damage**, **Twin Gates**, **Soul Link**, **Leech Life**,
  **Incoming Debt**

### Traits

*Tools ▸ SimpleMovements ▸ Player ▸ Generate Power Traits* creates these in `Characters/Traits/Powers`. They are also in
*Trait From Preset ▸ Fate & Tricks*:

| Trait | What it does |
|---|---|
| **Fate's Debt** | Once per fight or duel, a killing blow is ignored (you keep the health you had). Fate collects: −20% damage for 15 s. Ready again when the duel ends or after 20 s out of combat |
| **Acid Blood** | The Corpse Golem's essence. Melee attackers get splashed (40%): poisoned, and one of their items corrodes. One of your own items corrodes a little each time. −50% infection |
| **Born Gambler** | In combat, every 20 s a coin flip raises (60%) or lowers a random stat for 12 s |
| **Coin Flipper**, **Light Fingers**, **Phase Walker**, **Soulbinder**, **Gatekeeper**, **Giant's Blood** | Lucky Coin, Heist, Phantom Exchange, Soul Link, Twin Gates or Gigantification on the trait key |
| **Skywalker** | Walk on air for 8 mana per second |
| **Unshakable** | Immune to knockback and pulls, −10% move speed |

### Items

*Generate Starter Item Library* adds items that carry these abilities on their key (in *Armor Pieces*):

* Corpse Golem Essence (Acid Blood Burst plus the Acid Blood passive)
* Gambler's Coin, Philosopher's Stone, Pickpocket's Gloves, Gate Stone, Fang Of The Leech
* Debt Collector's Ledger, Soulbinder Chain, Hourglass Of Return, Phantom Cloak, Gravity Gauntlets
* Giant's Heart, Shrinking Charm, Mirage Pendant, Vanishing Cloak, Anchor Boots
* Mana Burner's Ring, Blood Pact Dagger Charm, Mirror Shield, Gravity Well Orb, Twin Mirror, Displacer Ring
* Fate's Promise (the Fate's Debt passive)

An item's ability appears in an extra ability slot. Assign its key in the item's *Ability On A Key* effect if you want
one.

## 7. Limits

* Inventory messages from Heist and Corrode ("Stole Iron Sword!") are shown on the machine that runs the ability, which
  for a remote client is the server's screen. The items themselves move for everyone through the inventory sync.
* Character stats changed by a size power (move speed, jump) come from the server. The owner's prediction gets the
  speed through the usual speed sync, but not the jump bonus, which is corrected by the movement reconcile.
