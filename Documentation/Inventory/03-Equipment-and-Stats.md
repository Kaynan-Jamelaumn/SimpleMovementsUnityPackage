# 03 — Equipment & Stats

## 1. `EquipmentManager`

One per character (added automatically next to the status controller; `EquipmentManager.For(anyComponent)` finds or
adds it). It holds one **entry** per equipped item, keyed by the slot (or any object for custom equips), and each
entry holds the **handles** of everything the item applied.

```csharp
var equipment = EquipmentManager.For(player);
equipment.Equip(this, amuletSO, "Quest Reward");   // custom equip, any key
equipment.Unequip(this);                            // reverts exactly what Equip applied
bool worn = equipment.IsEquipped(amuletSO);
List<ArmorSO> armor = equipment.GetEquippedArmor(); // also: GetEquippedItems, GetArmorInSlot, GetArmorDefense
equipment.Changed += OnGearChanged;                 // batched: once per change set
```

`SyncFromSlots(slots)` is the reconcile step used by the inventory: it computes the desired entries (items in their
equipment slots), unequips entries that are gone or changed, and equips new ones. Calling it twice changes nothing.
The first sync runs one frame after `Start`, so every system on the player is initialised.

Handles that need updates (pulses, conditional effects, timed classic stats) are ticked by the manager.

## 2. Equipment effects

`EquipmentEffect` is the building block of everything an item or set bonus does. Pick them with the dropdown in the
**Equip Effects** list of an equippable/armor/weapon (weapon: **Passive Effects**) or in a set bonus's **Effects**.

| Menu | Effect | What it does |
|---|---|---|
| Stats | **Combat Stats** | Defense, Magic Resistance, Strength/Agility/Intelligence/Endurance, crit, attack/casting speed, weapon damage, stamina cost, charge speed, knockback, elemental damage, elemental resistances. Each can be limited to a weapon category |
| Stats | **Character Stats** | Trait-system stats (max health/stamina/mana, regeneration, move/sprint speed, jump, carry weight, damage taken, healing, ability damage, cooldowns...) |
| Stats | **Classic Status Values** | The old `EquippableEffect` list with its stacking, chance, minimum level and duration options |
| Traits | **Grant Traits** | Gives traits while equipped. Free, not removable by the player, and a trait the player also owns is kept when the item comes off (reference counted) |
| Traits | **Enhance Trait** | *Strengthen* (multiply), *Add Modifiers* or *Replace* one of the character's traits while equipped. Stacking and priority rules decide between several sources |
| Traits | **Passive Behaviour** | Runs any trait behaviour (double jump, glide, life steal, thorns, second wind, cheat death, out-of-combat regeneration...) without a Trait asset |
| Abilities | **Ability On A Key** | Gives an ability cast with its own key while equipped |
| Triggered | **On Hit: Affect Target** | Chance on weapon hits / ability hits / any damage to apply ability effects (burn, poison, slow, stun, bonus damage...) or cast an ability at the target |
| Triggered | **When Hit: Affect Attacker** | Chance when hurt to affect the attacker (spikes, frost, knockback) or yourself (a shield when hit) |
| Triggered | **Pulse Around Wearer** | Periodic area effects: burning aura, healing pulse |
| Conditional | **While...** | Inner effects that apply only while a condition holds: health/mana/stamina below/above, in/out of combat, wielding a weapon category, unarmed |
| Special | **Special Mechanic** | Turns on a `SpecialMechanic` handled by a `SpecialMechanicHandlerBase` (water walking, low gravity...). Stays on while any source provides it |
| Visual | **Attached Effect** | A prefab (glow, aura) on the wearer while equipped |

Triggered effects have an internal recursion guard, so a proc cannot trigger itself endlessly.

### Writing a new effect

```csharp
[Serializable, AbilityMenu("Stats/Lucky Charm", "More gold from enemies.", 10)]
public class LuckyCharmEffect : EquipmentEffect
{
    public float goldPercent = 10f;
    public override string Describe(float strength = 1f) => $"+{goldPercent * strength:0}% Gold";

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        var loot = ctx.Owner.GetComponent<LootBonus>();
        if (loot == null) { ctx.WarnOnce("loot", "Lucky Charm needs a LootBonus component."); return null; }
        float added = goldPercent * strength;
        loot.goldPercent += added;
        return new ActionHandle(() => loot.goldPercent -= added);   // reverts exactly what was added
    }
}
```

It appears in every effect dropdown immediately. Rules: never modify assets, always return a handle that undoes
exactly what `Apply` did (or `null` if nothing was applied), use `ctx.WarnOnce` for missing setup.

## 3. `CombatStats`

A component on every character that uses the combat stats (added automatically). It sums modifiers from all sources,
each added with a token and removed by that token:

```csharp
var stats = CombatStats.For(player);
object token = stats.AddModifiers("Rage", new[] { new CombatStatModifier(CombatStatType.WeaponDamage, 20f) });
stats.RemoveModifiers(token);
float defense = stats.Get(CombatStatType.Defense);
float fireRes = stats.GetResistance(ElementType.Fire);   // percent, clamped
```

| Stat | Units | Effect |
|---|---|---|
| Strength / Agility / Intelligence | points | +1% weapon damage per point for weapons scaling with it. Agility also +0.5% attack speed and +0.2 crit chance per point; Intelligence +1% ability damage per point |
| Endurance | points | +5 max health per point |
| Defense / Magic Resistance | points | Physical / Magical damage taken × (1 − value / (value + Defense Half Value)), capped by Max Damage Reduction. Negative values increase damage |
| Critical Chance | % points | Added to weapon crit chance (and ability crits with *Use Caster Critical*) |
| Critical Damage | % | Added to the crit multiplier |
| Attack Speed | % | Faster attack animations and timings |
| Casting Speed | % | Shorter ability cast times (cast time ÷ (1 + %)) |
| Weapon Damage / Elemental Damage | % | More weapon damage / more damage from elemental hits dealt (any source; limit to one element for e.g. Poison Damage) |
| Attack Stamina Cost | % | Stamina spent per attack (negative = cheaper) |
| Charge Speed | % | Faster charging of hold attacks |
| Knockback | % | Longer knockback |
| Elemental resistances | % | Less damage of that element (negative = weakness, down to −100%) |
| Height, Threat, Crowd Control Duration / Resistance, Buff / Debuff Duration / Strength, Status Chance / Resistance, Physical / Magic Defense, Armor / Physical / Magic Penetration, Draw / Reload Speed, Mana Cost / Cooldown Reduction | | See [Player 06 §3](../Player/06-Races-Classes-Stats-and-Threat.md#3-stat-reference) for formulas, filters (element, kind of ability) and caps |

Every source adds (+10 and +15 = +25); **scaling rules** (`Agility gives 0.2 Critical Chance per point`, on the
component, a race, a class or a trait) add attribute-based bonuses from the raw totals. `GetFor(stat, element, scope)`
counts modifiers limited to one element or one kind of ability; `AddScaling(label, rules)` adds rules with a token.
In Play Mode the inspector lists every total, the derived multipliers and each modifier's source.

All per-point values, the half value and the caps are fields on the component. Innate stats (an armored mob) go in
its **Innate Values**. Damage reduction is applied through `CombatEntity`'s `IDamageTakenModifier` hook, so it
applies to every source of damage (weapons, abilities, projectiles), by damage type (`Physical`, `Magical`, `True`)
and element.

## 4. Classic stats (`EquippableEffect`)

The original equipment stats keep working and keep their stacking options (`canStack`, `maxStacks`), chance,
minimum level and duration. They are applied by `LegacyStatPool`:

| Kind | Examples | Applied through |
|---|---|---|
| Trait stats | Max Hp/Stamina/Mana, Speed, Speed Factor, regeneration, heal/damage factors, Max Weight | `TraitManager` modifiers (exact revert, survive class resets) |
| Combat stats | Strength, Agility, Intelligence, Endurance, Defense, Magic Resistance, crit, attack/casting speed | `CombatStats` |
| Direct | Hunger/Thirst maximums and the remaining status values | The status managers, recording the exact applied amount |

Previously the combat ones only logged a message, and speed factors drifted after repeated equip/unequip.
