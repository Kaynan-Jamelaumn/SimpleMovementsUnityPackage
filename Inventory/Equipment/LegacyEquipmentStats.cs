using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What each classic <see cref="EquippableEffectType"/> changes, its units and how it is described. Used by the
/// <see cref="LegacyStatPool"/> (which applies them) and by tooltips and inspectors.
/// </summary>
public static class LegacyEquipmentStats
{
    public enum Target
    {
        /// <summary>Written through the trait manager's exact modifier system (follows class resets and level-ups).</summary>
        Trait,
        /// <summary>Written into a status manager directly (the exact amount is recorded and removed later).</summary>
        Direct,
        /// <summary>A combat stat (<see cref="CombatStats"/>).</summary>
        Combat,
    }

    /// <summary>How the effect type is applied: trait stat, combat stat, or direct manager write.</summary>
    public static Target GetTarget(EquippableEffectType type, out TraitStat traitStat, out TraitModifierMode mode, out float valueScale, out CombatStatType combatStat)
    {
        traitStat = TraitStat.MaxHealth;
        mode = TraitModifierMode.Flat;
        valueScale = 1f;
        combatStat = CombatStatType.Defense;
        switch (type)
        {
            case EquippableEffectType.MaxHp: traitStat = TraitStat.MaxHealth; return Target.Trait;
            case EquippableEffectType.MaxStamina: traitStat = TraitStat.MaxStamina; return Target.Trait;
            case EquippableEffectType.MaxMana: traitStat = TraitStat.MaxMana; return Target.Trait;
            case EquippableEffectType.Speed: traitStat = TraitStat.MoveSpeed; return Target.Trait;
            case EquippableEffectType.MaxWeight: traitStat = TraitStat.CarryWeight; return Target.Trait;
            case EquippableEffectType.HpRegeneration: traitStat = TraitStat.HealthRegen; return Target.Trait;
            case EquippableEffectType.StaminaRegeneration: traitStat = TraitStat.StaminaRegen; return Target.Trait;
            case EquippableEffectType.ManaRegeneration: traitStat = TraitStat.ManaRegen; return Target.Trait;
            case EquippableEffectType.HpHealFactor: traitStat = TraitStat.HealingReceived; mode = TraitModifierMode.Percent; return Target.Trait;
            case EquippableEffectType.HpDamageFactor: traitStat = TraitStat.DamageTaken; mode = TraitModifierMode.Percent; return Target.Trait;
            case EquippableEffectType.StaminaDamageFactor: traitStat = TraitStat.StaminaCost; mode = TraitModifierMode.Percent; return Target.Trait;
            // Old behaviour: base speed * amount (0.2 = +20%) and base speed * (amount - 1) (1.2 = +20%).
            case EquippableEffectType.SpeedFactor: traitStat = TraitStat.MoveSpeed; mode = TraitModifierMode.Percent; valueScale = 100f; return Target.Trait;
            case EquippableEffectType.SpeedMultiplier: traitStat = TraitStat.MoveSpeed; mode = TraitModifierMode.Percent; valueScale = 100f; return Target.Trait;

            case EquippableEffectType.Strength: combatStat = CombatStatType.Strength; return Target.Combat;
            case EquippableEffectType.Agility: combatStat = CombatStatType.Agility; return Target.Combat;
            case EquippableEffectType.Intelligence: combatStat = CombatStatType.Intelligence; return Target.Combat;
            case EquippableEffectType.Endurance: combatStat = CombatStatType.Endurance; return Target.Combat;
            case EquippableEffectType.Defense: combatStat = CombatStatType.Defense; return Target.Combat;
            case EquippableEffectType.MagicResistance: combatStat = CombatStatType.MagicResistance; return Target.Combat;
            case EquippableEffectType.CriticalChance: combatStat = CombatStatType.CriticalChance; return Target.Combat;
            case EquippableEffectType.CriticalDamage: combatStat = CombatStatType.CriticalDamage; return Target.Combat;
            case EquippableEffectType.AttackSpeed: combatStat = CombatStatType.AttackSpeed; return Target.Combat;
            case EquippableEffectType.CastingSpeed: combatStat = CombatStatType.CastingSpeed; return Target.Combat;
            default: return Target.Direct;
        }
    }

    /// <summary>The value actually used for an amount (SpeedMultiplier 1.2 = +20%, the others as they are).</summary>
    public static float EffectiveAmount(EquippableEffectType type, float amount) =>
        type == EquippableEffectType.SpeedMultiplier ? amount - 1f : amount;

    /// <summary>"+20 Health", "+15% Healing Received", "+5 Defense".</summary>
    public static string Describe(EquippableEffectType type, float amount)
    {
        float v = EffectiveAmount(type, amount);
        string s = v >= 0f ? "+" : "";
        switch (type)
        {
            case EquippableEffectType.MaxHp: return $"{s}{v:0.##} Max Health";
            case EquippableEffectType.MaxStamina: return $"{s}{v:0.##} Max Stamina";
            case EquippableEffectType.MaxMana: return $"{s}{v:0.##} Max Mana";
            case EquippableEffectType.Speed: return $"{s}{v:0.##} Move Speed";
            case EquippableEffectType.SpeedFactor:
            case EquippableEffectType.SpeedMultiplier: return $"{s}{v * 100f:0.#}% Move Speed";
            case EquippableEffectType.MaxWeight: return $"{s}{v:0.##} Carry Weight";
            case EquippableEffectType.HpRegeneration: return $"{s}{v:0.##} Health Regen";
            case EquippableEffectType.StaminaRegeneration: return $"{s}{v:0.##} Stamina Regen";
            case EquippableEffectType.ManaRegeneration: return $"{s}{v:0.##} Mana Regen";
            case EquippableEffectType.HpHealFactor: return $"{s}{v:0.#}% Healing Received";
            case EquippableEffectType.HpDamageFactor: return $"{s}{v:0.#}% Damage Taken";
            case EquippableEffectType.StaminaHealFactor: return $"{s}{v:0.#}% Stamina Recovery";
            case EquippableEffectType.StaminaDamageFactor: return $"{s}{v:0.#}% Stamina Cost";
            case EquippableEffectType.ManaHealFactor: return $"{s}{v:0.#}% Mana Recovery";
            case EquippableEffectType.ManaDamageFactor: return $"{s}{v:0.#}% Mana Cost";
            case EquippableEffectType.CriticalChance: return $"{s}{v:0.#}% Critical Chance";
            case EquippableEffectType.CriticalDamage: return $"{s}{v:0.#}% Critical Damage";
            case EquippableEffectType.AttackSpeed: return $"{s}{v:0.#}% Attack Speed";
            case EquippableEffectType.CastingSpeed: return $"{s}{v:0.#}% Casting Speed";
            case EquippableEffectType.MagicResistance: return $"{s}{v:0.##} Magic Resistance";
        }
        string name = type.ToString();
        if (name.EndsWith("HealFactor")) return $"{s}{v:0.#}% {Nice(name.Substring(0, name.Length - 10))} Recovery";
        if (name.EndsWith("DamageFactor")) return $"{s}{v:0.#}% {Nice(name.Substring(0, name.Length - 12))} Loss";
        if (name.EndsWith("Regeneration")) return $"{s}{v:0.##} {Nice(name.Substring(0, name.Length - 12))} Regen";
        if (name.StartsWith("Max")) return $"{s}{v:0.##} Max {Nice(name.Substring(3))}";
        return $"{s}{v:0.##} {Nice(name)}";
    }

    /// <summary>Units and meaning of an effect type (inspector tooltips).</summary>
    public static string Explain(EquippableEffectType type)
    {
        switch (GetTarget(type, out TraitStat ts, out TraitModifierMode mode, out _, out CombatStatType cs))
        {
            case Target.Combat:
                return CombatStatInfo.Get(cs).description;
            case Target.Trait:
                if (type == EquippableEffectType.SpeedFactor) return "Move speed: 0.2 = +20% of the base speed.";
                if (type == EquippableEffectType.SpeedMultiplier) return "Move speed multiplier: 1.2 = +20% of the base speed.";
                return TraitStats.Get(ts).description + (mode == TraitModifierMode.Percent ? " Value in percent (+15 = 15%)." : "");
        }
        string n = type.ToString();
        if (n.EndsWith("Factor")) return "Percentage added to the manager's factor (+15 = 15%).";
        if (n.EndsWith("Regeneration")) return "Amount regenerated per tick.";
        return "Added to the maximum value.";
    }

    private static string Nice(string s)
    {
        if (s == "Hp") return "Health";
        var sb = new System.Text.StringBuilder(s.Length + 4);
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1]))
                sb.Append(' ');
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Writes <paramref name="amount"/> straight into the status manager of a classic effect type and returns what
    /// was really written (0 when the manager is missing). Used for the types without a trait stat, and for every
    /// type on characters without a trait manager.
    /// </summary>
    public static float WriteDirect(PlayerStatusController s, EquippableEffectType type, float amount)
    {
        if (s == null || Mathf.Approximately(amount, 0f))
            return 0f;
        switch (type)
        {
            case EquippableEffectType.MaxHp: return Max(s.HpManager, amount);
            case EquippableEffectType.MaxStamina: return Max(s.StaminaManager, amount);
            case EquippableEffectType.MaxMana: return Max(s.ManaManager, amount);
            case EquippableEffectType.MaxHunger: return Max(s.HungerManager, amount);
            case EquippableEffectType.MaxThirst: return Max(s.ThirstManager, amount);
            case EquippableEffectType.MaxSleep: return Max(s.SleepManager, amount);
            case EquippableEffectType.MaxSanity: return Max(s.SanityManager, amount);
            case EquippableEffectType.MaxBodyHeat: return Max(s.BodyHeatManager, amount);
            case EquippableEffectType.MaxOxygen: return Max(s.OxygenManager, amount);
            case EquippableEffectType.MaxWeight:
                if (s.WeightManager == null) return 0f;
                s.WeightManager.ModifyMaxWeight(amount);
                return amount;

            case EquippableEffectType.HpRegeneration: return Regen(s.HpManager, amount);
            case EquippableEffectType.StaminaRegeneration: return Regen(s.StaminaManager, amount);
            case EquippableEffectType.ManaRegeneration: return Regen(s.ManaManager, amount);
            case EquippableEffectType.HungerRegeneration: return Regen(s.HungerManager, amount);
            case EquippableEffectType.ThirstRegeneration: return Regen(s.ThirstManager, amount);
            case EquippableEffectType.SleepRegeneration: return Regen(s.SleepManager, amount);
            case EquippableEffectType.SanityRegeneration: return Regen(s.SanityManager, amount);
            case EquippableEffectType.BodyHeatRegeneration: return Regen(s.BodyHeatManager, amount);
            case EquippableEffectType.OxygenRegeneration: return Regen(s.OxygenManager, amount);

            case EquippableEffectType.HpHealFactor: return Heal(s.HpManager, amount);
            case EquippableEffectType.StaminaHealFactor: return Heal(s.StaminaManager, amount);
            case EquippableEffectType.ManaHealFactor: return Heal(s.ManaManager, amount);
            case EquippableEffectType.HungerHealFactor: return Heal(s.HungerManager, amount);
            case EquippableEffectType.ThirstHealFactor: return Heal(s.ThirstManager, amount);
            case EquippableEffectType.SleepHealFactor: return Heal(s.SleepManager, amount);
            case EquippableEffectType.SanityHealFactor: return Heal(s.SanityManager, amount);
            case EquippableEffectType.BodyHeatHealFactor: return Heal(s.BodyHeatManager, amount);
            case EquippableEffectType.OxygenHealFactor: return Heal(s.OxygenManager, amount);

            case EquippableEffectType.HpDamageFactor: return Dmg(s.HpManager, amount);
            case EquippableEffectType.StaminaDamageFactor: return Dmg(s.StaminaManager, amount);
            case EquippableEffectType.ManaDamageFactor: return Dmg(s.ManaManager, amount);
            case EquippableEffectType.HungerDamageFactor: return Dmg(s.HungerManager, amount);
            case EquippableEffectType.ThirstDamageFactor: return Dmg(s.ThirstManager, amount);
            case EquippableEffectType.SleepDamageFactor: return Dmg(s.SleepManager, amount);
            case EquippableEffectType.SanityDamageFactor: return Dmg(s.SanityManager, amount);
            case EquippableEffectType.BodyHeatDamageFactor: return Dmg(s.BodyHeatManager, amount);
            case EquippableEffectType.OxygenDamageFactor: return Dmg(s.OxygenManager, amount);

            case EquippableEffectType.Speed:
            case EquippableEffectType.SpeedFactor:
            case EquippableEffectType.SpeedMultiplier:
            {
                // Speed/SpeedFactor/SpeedMultiplier reach here with the absolute speed change already computed.
                SpeedManager sp = s.SpeedManager;
                if (sp == null) return 0f;
                float before = sp.BaseSpeed;
                sp.ModifyBaseSpeed(amount); // clamps at 0: record what really changed
                return sp.BaseSpeed - before;
            }
            default:
                return 0f;
        }
    }

    /// <summary>The max value a class resets (NaN when the type is not a max value or the manager is missing).</summary>
    public static float CurrentMax(PlayerStatusController s, EquippableEffectType type)
    {
        if (s == null) return float.NaN;
        switch (type)
        {
            case EquippableEffectType.MaxHp: return s.HpManager != null ? s.HpManager.MaxValue : float.NaN;
            case EquippableEffectType.MaxStamina: return s.StaminaManager != null ? s.StaminaManager.MaxValue : float.NaN;
            case EquippableEffectType.MaxMana: return s.ManaManager != null ? s.ManaManager.MaxValue : float.NaN;
            case EquippableEffectType.MaxHunger: return s.HungerManager != null ? s.HungerManager.MaxValue : float.NaN;
            case EquippableEffectType.MaxThirst: return s.ThirstManager != null ? s.ThirstManager.MaxValue : float.NaN;
            case EquippableEffectType.MaxSleep: return s.SleepManager != null ? s.SleepManager.MaxValue : float.NaN;
            case EquippableEffectType.MaxSanity: return s.SanityManager != null ? s.SanityManager.MaxValue : float.NaN;
            case EquippableEffectType.MaxBodyHeat: return s.BodyHeatManager != null ? s.BodyHeatManager.MaxValue : float.NaN;
            case EquippableEffectType.MaxOxygen: return s.OxygenManager != null ? s.OxygenManager.MaxValue : float.NaN;
            case EquippableEffectType.MaxWeight: return s.WeightManager != null ? s.WeightManager.MaxValue : float.NaN;
            case EquippableEffectType.Speed:
            case EquippableEffectType.SpeedFactor:
            case EquippableEffectType.SpeedMultiplier: return s.SpeedManager != null ? s.SpeedManager.BaseSpeed : float.NaN;
            default: return float.NaN;
        }
    }

    /// <summary>What the character's class sets a max value to (NaN when not a class stat or no class).</summary>
    public static float ClassValue(PlayerStatusController s, EquippableEffectType type)
    {
        PlayerClass c = s != null ? s.CurrentPlayerClass : null;
        if (c == null) return float.NaN;
        switch (type)
        {
            case EquippableEffectType.MaxHp: return c.health;
            case EquippableEffectType.MaxStamina: return c.stamina;
            case EquippableEffectType.MaxMana: return c.mana;
            case EquippableEffectType.MaxHunger: return c.hunger;
            case EquippableEffectType.MaxThirst: return c.thirst;
            case EquippableEffectType.MaxSleep: return c.sleep;
            case EquippableEffectType.MaxSanity: return c.sanity;
            case EquippableEffectType.MaxBodyHeat: return c.bodyHeat;
            case EquippableEffectType.MaxOxygen: return c.oxygen;
            case EquippableEffectType.MaxWeight: return c.weight;
            case EquippableEffectType.Speed:
            case EquippableEffectType.SpeedFactor:
            case EquippableEffectType.SpeedMultiplier: return c.speed;
            default: return float.NaN;
        }
    }

    private static float Max(StatusManager m, float a) { if (m == null) return 0f; m.ModifyMaxValue(a); return a; }
    private static float Regen(StatusManager m, float a) { if (m == null) return 0f; m.ModifyIncrementValue(a); return a; }
    private static float Heal(StatusManager m, float a) { if (m == null) return 0f; m.ModifyIncrementFactor(a); return a; }
    private static float Dmg(StatusManager m, float a) { if (m == null) return 0f; m.ModifyDecrementFactor(a); return a; }
}

/// <summary>
/// Applies the classic <see cref="EquippableEffect"/>s (an effect type and an amount) of every equipped item and set
/// bonus. Each effect type is written as ONE total that is removed exactly and re-applied whenever it changes, so
/// values never drift (percent speed bonuses used to be removed from a different base than they were added to).
/// <para>
/// Stacking follows each effect's settings: stacking effects add up (at most "Max Stacks" of them count when it is
/// above 1, strongest first); non-stacking effects of the same type do not add up - only the strongest one counts.
/// Max health/stamina/mana, carry weight, speed, regeneration and healing/damage factors go through the trait
/// manager's exact modifier system (so class resets and level-ups keep them); the combat stats (Strength, Defense,
/// Critical Chance...) go to <see cref="CombatStats"/> - they used to only print a log message.
/// </para>
/// </summary>
public sealed class LegacyStatPool
{
    private sealed class Entry
    {
        public EquippableEffectType type;
        public float amount;
        public bool stacks;
        public int maxStacks;
    }

    private sealed class Applied
    {
        public float total;
        public TraitManager.AppliedModifiers traitHandle;
        public object combatToken;
        public float direct;
    }

    private readonly EquipmentContext ctx;
    private readonly Dictionary<EquippableEffectType, List<Entry>> entries = new Dictionary<EquippableEffectType, List<Entry>>();
    private readonly Dictionary<EquippableEffectType, Applied> applied = new Dictionary<EquippableEffectType, Applied>();
    private readonly List<Entry> sortBuffer = new List<Entry>();
    private readonly List<EquippableEffectType> typeBuffer = new List<EquippableEffectType>();

    public LegacyStatPool(EquipmentContext ctx) => this.ctx = ctx;

    /// <summary>Adds one effect; returns the token for <see cref="Remove"/>.</summary>
    public object Add(EquippableEffectType type, float amount, bool canStack, int maxStacks)
    {
        if (!entries.TryGetValue(type, out List<Entry> list))
            entries[type] = list = new List<Entry>(2);
        var e = new Entry { type = type, amount = amount, stacks = canStack, maxStacks = maxStacks };
        list.Add(e);
        Refresh(type);
        return e;
    }

    public void Remove(object token)
    {
        if (!(token is Entry e) || !entries.TryGetValue(e.type, out List<Entry> list) || !list.Remove(e))
            return;
        Refresh(e.type);
        if (list.Count == 0)
            entries.Remove(e.type);
    }

    /// <summary>The total currently applied for a type (0 = nothing).</summary>
    public float GetTotal(EquippableEffectType type) => applied.TryGetValue(type, out Applied a) ? a.total : 0f;

    /// <summary>
    /// After the class SET the max values (class change, first initialization), direct writes to them are gone:
    /// write them again (never subtract what is no longer there).
    /// </summary>
    public void OnClassReset()
    {
        PlayerStatusController s = ctx.Status;
        if (s == null)
            return;
        typeBuffer.Clear();
        typeBuffer.AddRange(applied.Keys);
        foreach (EquippableEffectType type in typeBuffer)
        {
            Applied a = applied[type];
            if (Mathf.Approximately(a.direct, 0f))
                continue;
            float now = LegacyEquipmentStats.CurrentMax(s, type);
            float cls = LegacyEquipmentStats.ClassValue(s, type);
            if (float.IsNaN(now) || float.IsNaN(cls) || !Mathf.Approximately(now, cls))
                continue;
            a.direct = LegacyEquipmentStats.WriteDirect(s, type, a.direct);
        }
    }

    private float Total(List<Entry> list)
    {
        float stackTotal = 0f;
        Entry strongestUnique = null;
        int limit = int.MaxValue;
        sortBuffer.Clear();
        for (int i = 0; i < list.Count; i++)
        {
            Entry e = list[i];
            if (e.stacks)
            {
                sortBuffer.Add(e);
                if (e.maxStacks > 1)
                    limit = Mathf.Min(limit, e.maxStacks);
            }
            else if (strongestUnique == null || Mathf.Abs(e.amount) > Mathf.Abs(strongestUnique.amount))
            {
                strongestUnique = e;
            }
        }
        if (limit < sortBuffer.Count)
            sortBuffer.Sort((a, b) => Mathf.Abs(b.amount).CompareTo(Mathf.Abs(a.amount)));
        int n = Mathf.Min(limit, sortBuffer.Count);
        for (int i = 0; i < n; i++)
            stackTotal += sortBuffer[i].amount;
        sortBuffer.Clear();
        return stackTotal + (strongestUnique != null ? strongestUnique.amount : 0f);
    }

    private void Refresh(EquippableEffectType type)
    {
        float wanted = entries.TryGetValue(type, out List<Entry> list) ? Total(list) : 0f;
        if (applied.TryGetValue(type, out Applied current))
        {
            if (Mathf.Approximately(current.total, wanted))
                return;
            Undo(type, current);
            applied.Remove(type);
        }
        if (!Mathf.Approximately(wanted, 0f))
            applied[type] = Do(type, wanted);
    }

    private Applied Do(EquippableEffectType type, float total)
    {
        var a = new Applied { total = total };
        float amount = LegacyEquipmentStats.EffectiveAmount(type, total);
        switch (LegacyEquipmentStats.GetTarget(type, out TraitStat ts, out TraitModifierMode mode, out float scale, out CombatStatType cs))
        {
            case LegacyEquipmentStats.Target.Combat:
                CombatStats stats = ctx.Stats;
                if (stats != null)
                    a.combatToken = stats.AddModifiers("Equipment", new[] { new CombatStatModifier(cs, amount) });
                break;
            case LegacyEquipmentStats.Target.Trait when ctx.Traits != null:
                a.traitHandle = ctx.Traits.ApplyModifiers(new[] { new TraitModifier(ts, mode, amount * scale) }, 1f, "Equipment");
                break;
            default:
            {
                PlayerStatusController s = ctx.Status;
                if (s == null)
                {
                    ctx.WarnOnce("legacy-no-status", $"[Equipment] {ctx.Owner?.name}: classic equipment stats need a PlayerStatusController; they were skipped.");
                    break;
                }
                if (type == EquippableEffectType.SpeedFactor || type == EquippableEffectType.SpeedMultiplier)
                    amount = s.SpeedManager != null ? s.SpeedManager.BaseSpeed * amount : 0f;
                a.direct = LegacyEquipmentStats.WriteDirect(s, type, amount);
                break;
            }
        }
        return a;
    }

    private void Undo(EquippableEffectType type, Applied a)
    {
        if (a.combatToken != null && ctx.Stats != null)
            ctx.Stats.RemoveModifiers(a.combatToken);
        if (a.traitHandle != null && ctx.Traits != null)
            ctx.Traits.RevertModifiers(a.traitHandle);
        if (!Mathf.Approximately(a.direct, 0f))
            LegacyEquipmentStats.WriteDirect(ctx.Status, type, -a.direct);
    }

    /// <summary>Removes everything this pool applied (the equipment manager is being destroyed).</summary>
    public void Clear()
    {
        foreach (KeyValuePair<EquippableEffectType, Applied> kv in applied)
            Undo(kv.Key, kv.Value);
        applied.Clear();
        entries.Clear();
    }
}
