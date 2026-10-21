using System;
using System.Collections.Generic;
using UnityEngine;

// ====================================================================== stats by chance
/// <summary>One stat a <see cref="StatGambleEffect"/> can change.</summary>
[Serializable]
public class GambleStat
{
    [Tooltip("Name shown in the buff (Strength, Move Speed...).")]
    public string name = "Strength";
    [Tooltip("A combat stat (points or percent, as the stat itself), or a character stat (percent).")]
    public bool characterStat = false;
    public CombatStatType combatStat = CombatStatType.Strength;
    public TraitStat trait = TraitStat.MoveSpeed;
    [Tooltip("How much it goes up or down.")]
    public float amount = 8f;

    public GambleStat() { }
    public GambleStat(string name, CombatStatType stat, float amount) { this.name = name; combatStat = stat; this.amount = amount; }
    public GambleStat(string name, TraitStat stat, float amount) { this.name = name; characterStat = true; trait = stat; this.amount = amount; }

    public static List<GambleStat> Defaults() => new List<GambleStat>
    {
        new GambleStat("Strength", CombatStatType.Strength, 8f),
        new GambleStat("Agility", CombatStatType.Agility, 8f),
        new GambleStat("Intelligence", CombatStatType.Intelligence, 8f),
        new GambleStat("Endurance", CombatStatType.Endurance, 8f),
        new GambleStat("Defense", CombatStatType.Defense, 12f),
        new GambleStat("Critical Chance", CombatStatType.CriticalChance, 10f),
        new GambleStat("Attack Speed", CombatStatType.AttackSpeed, 15f),
        new GambleStat("Weapon Damage", CombatStatType.WeaponDamage, 15f),
        new GambleStat("Move Speed", TraitStat.MoveSpeed, 15f),
        new GambleStat("Max Health", TraitStat.MaxHealth, 15f),
    };
}

/// <summary>How a <see cref="StatGambleEffect"/> plays.</summary>
public enum StatGambleMode
{
    /// <summary>Lucky Coin: one random stat goes up (heads) or down (tails).</summary>
    CoinFlip,
    /// <summary>Equivalent Exchange: one random stat goes up and another goes down.</summary>
    Exchange,
}

/// <summary>
/// Lucky Coin and Equivalent Exchange: raises or lowers random stats of the target for a while (a buff or debuff shown
/// with the others, so it reaches every client like any buff).
/// </summary>
[Serializable, AbilityMenu("Random/Stat Gamble (Lucky Coin, Equivalent Exchange)", "Coin Flip: a random stat goes up or down. Exchange: a random stat goes up and another goes down.", 0)]
public class StatGambleEffect : AbilityEffect
{
    public StatGambleMode mode = StatGambleMode.CoinFlip;
    [Tooltip("Coin Flip: chance (0-1) the stat goes up.")]
    [Range(0f, 1f)] public float luck = 0.5f;
    [Tooltip("Exchange: the stat that goes down loses this much of its amount (1 = as much as the other gains).")]
    [Range(0f, 2f)] public float exchangeCost = 1f;
    [Tooltip("Seconds it lasts.")]
    [Min(0.5f)] public float duration = 20f;
    [Tooltip("The stats it picks from.")]
    public List<GambleStat> stats = GambleStat.Defaults();

    public StatGambleEffect() { onlyAffects = TargetFilter.Self | TargetFilter.Allies; }

    public override void Apply(ref EffectContext ctx) => ApplyTo(ctx.target, ctx.caster, Mathf.Max(0.1f, ctx.stats.duration));

    /// <summary>Plays the gamble on <paramref name="target"/> (from code, traits).</summary>
    public void ApplyTo(CombatEntity target, CombatEntity source, float durationScale = 1f)
    {
        if (target == null || stats == null || stats.Count == 0)
            return;
        float time = duration * durationScale;
        GambleStat up, down = null;
        bool good;
        if (mode == StatGambleMode.CoinFlip)
        {
            up = stats[UnityEngine.Random.Range(0, stats.Count)];
            good = UnityEngine.Random.value < luck;
        }
        else
        {
            int a = UnityEngine.Random.Range(0, stats.Count);
            int b = stats.Count > 1 ? (a + UnityEngine.Random.Range(1, stats.Count)) % stats.Count : a;
            up = stats[a];
            down = stats[b];
            good = true;
        }
        var combat = new List<CombatStatModifier>();
        var character = new List<TraitModifier>();
        Add(up, good ? 1f : -1f, combat, character);
        if (down != null)
            Add(down, -exchangeCost, combat, character);
        string label = mode == StatGambleMode.CoinFlip
            ? $"{(good ? "Heads" : "Tails")}: {(good ? "+" : "-")}{up.amount:0.#} {up.name}"
            : $"Exchange: +{up.amount:0.#} {up.name}, -{down.amount * exchangeCost:0.#} {down.name}";
        TimedStatModifiers.For(target).Apply("gamble:" + mode, label, source, !good, ElementType.None, time, 1f, combat, null, character);
    }

    private static void Add(GambleStat s, float sign, List<CombatStatModifier> combat, List<TraitModifier> character)
    {
        if (s.characterStat)
            character.Add(TraitModifier.Percent(s.trait, s.amount * sign));
        else
            combat.Add(new CombatStatModifier(s.combatStat, s.amount * sign));
    }

    public override string Describe(in AbilityStats s) =>
        mode == StatGambleMode.CoinFlip
            ? $"flip a coin: a random stat goes up ({luck:P0}) or down for {duration * s.duration:0.#} s"
            : $"a random stat goes up and another goes down for {duration * s.duration:0.#} s";

    public override AbilityEffect Clone()
    {
        var c = (StatGambleEffect)base.Clone();
        c.stats = stats != null ? new List<GambleStat>(stats) : new List<GambleStat>();
        return c;
    }
}

/// <summary>Mutual Exchange: the caster and the target swap the value of a random combat stat for a while.</summary>
[Serializable, AbilityMenu("Random/Swap Stat (Mutual Exchange)", "The caster and the target swap a random combat stat (Strength, Defense, Critical Chance...) for a while.", 0)]
public class StatSwapEffect : AbilityEffect
{
    [Tooltip("Seconds the swap lasts.")]
    [Min(0.5f)] public float duration = 15f;
    [Tooltip("The stats it can swap.")]
    public List<CombatStatType> stats = new List<CombatStatType>
    {
        CombatStatType.Strength, CombatStatType.Agility, CombatStatType.Intelligence, CombatStatType.Endurance,
        CombatStatType.Defense, CombatStatType.MagicResistance, CombatStatType.CriticalChance, CombatStatType.AttackSpeed,
    };
    [Tooltip("Pick the stat where the target is better than the caster when there is one (a fair trade never happens).")]
    public bool preferGains = true;

    public StatSwapEffect() { onlyAffects = TargetFilter.All & ~TargetFilter.Self; }

    public override bool IsHarmful => true;

    public override void Apply(ref EffectContext ctx)
    {
        CombatEntity a = ctx.caster, b = ctx.target;
        if (a == null || b == null || a == b || stats == null || stats.Count == 0)
            return;
        CombatStats sa = a.Stats ?? CombatStats.For(a, addIfMissing: true), sb = b.Stats ?? CombatStats.For(b, addIfMissing: true);
        var pool = new List<CombatStatType>();
        if (preferGains)
            foreach (CombatStatType s in stats)
                if (sb.Get(s) > sa.Get(s) + 0.01f)
                    pool.Add(s);
        if (pool.Count == 0)
            pool.AddRange(stats);
        CombatStatType stat = pool[UnityEngine.Random.Range(0, pool.Count)];
        float va = sa.Get(stat), vb = sb.Get(stat);
        float time = duration * Mathf.Max(0.1f, ctx.stats.duration);
        string name = CombatStatInfo.Get(stat).name;
        TimedStatModifiers.For(a).Apply(b, $"Exchanged {name} ({va:0.#} → {vb:0.#})", a, vb < va, ElementType.None, time, 1f,
            new List<CombatStatModifier> { new CombatStatModifier(stat, vb - va) }, null, null);
        TimedStatModifiers.For(b).Apply(a, $"Exchanged {name} ({vb:0.#} → {va:0.#})", a, va < vb, ElementType.None, time, 1f,
            new List<CombatStatModifier> { new CombatStatModifier(stat, va - vb) }, null, null);
    }

    public override string Describe(in AbilityStats s) => $"swap a random stat with the target for {duration * s.duration:0.#} s";

    public override AbilityEffect Clone()
    {
        var c = (StatSwapEffect)base.Clone();
        c.stats = stats != null ? new List<CombatStatType>(stats) : new List<CombatStatType>();
        return c;
    }
}

// ====================================================================== items
/// <summary>
/// Acid: wears down the hit character's equipped items (durability). Use it on the caster too for a price (the acid
/// blood also eats your own gear).
/// </summary>
[Serializable, AbilityMenu("Item/Corrode Equipment", "Wears down the durability of the target's equipped items (acid, rust).", 0)]
public class CorrodeEquipmentEffect : AbilityEffect
{
    [Tooltip("Durability points removed from each item hit.")]
    [Min(0)] public int amount = 8;
    [Tooltip("Plus this percentage of each item's max durability.")]
    [Range(0f, 100f)] public float percentOfMax = 0f;
    [Tooltip("How many equipped items are hit (random ones). 0 = all of them.")]
    [Min(0)] public int items = 2;

    public override bool IsHarmful => true;

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.target != null)
            Corrode(ctx.target, amount, percentOfMax, items);
    }

    /// <summary>Removes durability from <paramref name="count"/> random equipped items of <paramref name="who"/> (0 = all). Returns how many were hit.</summary>
    public static int Corrode(CombatEntity who, int points, float percent, int count)
    {
        if (who == null || who.IsReplica)
            return 0;
        EquipmentManager eq = EquipmentManager.For(who, addIfMissing: false);
        if (eq == null)
            return 0;
        var candidates = new List<InventoryItem>();
        foreach (EquipmentManager.Entry e in eq.Entries)
        {
            InventoryItem it = e.InventoryItem;
            if (it != null && e.Item != null && e.Item.MaxDurability > 0 && it.durability > 0f && !candidates.Contains(it))
                candidates.Add(it);
        }
        int hit = 0;
        while (candidates.Count > 0 && (count <= 0 || hit < count))
        {
            int i = UnityEngine.Random.Range(0, candidates.Count);
            InventoryItem it = candidates[i];
            candidates.RemoveAt(i);
            float loss = points + it.itemScriptableObject.MaxDurability * percent / 100f;
            it.durability = Mathf.Max(0f, it.durability - loss);
            if (it.DurabilityList != null && it.DurabilityList.Count > 0)
                it.DurabilityList[it.DurabilityList.Count - 1] = Mathf.RoundToInt(it.durability);
            it.RefreshUI();
            hit++;
        }
        if (hit > 0)
            InventoryManager.For(who)?.NotifyInventoryChanged();
        return hit;
    }

    public override string Describe(in AbilityStats s) =>
        $"corrodes {(items <= 0 ? "every" : items.ToString())} equipped item{(items == 1 ? "" : "s")} ({amount}{(percentOfMax > 0f ? $" + {percentOfMax:0}%" : "")} durability)";
}

/// <summary>Heist: steals a random item from the target's bag into the caster's.</summary>
[Serializable, AbilityMenu("Item/Steal Item (Heist)", "Takes a random item from the target's bag (players) and puts it in the caster's.", 0)]
public class StealItemEffect : AbilityEffect
{
    [Tooltip("Take the whole stack instead of one.")]
    public bool wholeStack = false;
    [Tooltip("Only items that can be sold (leaves quest items alone).")]
    public bool onlySellable = true;
    [Tooltip("Also take from the hotbar.")]
    public bool includeHotbar = false;

    public override bool IsHarmful => true;

    public StealItemEffect() { onlyAffects = TargetFilter.All & ~TargetFilter.Self; }

    public override void Apply(ref EffectContext ctx)
    {
        CombatEntity thief = ctx.caster, mark = ctx.target;
        if (thief == null || mark == null || thief == mark || mark.IsReplica)
            return;
        InventoryManager from = InventoryManager.For(mark), to = InventoryManager.For(thief);
        if (from == null || to == null)
        {
            to?.ShowMessage("There is nothing to steal.");
            return;
        }
        List<InventoryItem> bag = from.GetBagItems(null, includeHotbar);
        bag.RemoveAll(i => i == null || i.itemScriptableObject == null || i.stackCurrent <= 0 || (onlySellable && !i.itemScriptableObject.CanBeSold));
        if (bag.Count == 0)
        {
            to.ShowMessage("Their pockets are empty.");
            return;
        }
        InventoryItem pick = bag[UnityEngine.Random.Range(0, bag.Count)];
        ItemSO item = pick.itemScriptableObject;
        int amount = wholeStack ? pick.stackCurrent : 1;
        if (!to.HasEnoughSpace(item, amount))
        {
            to.ShowMessage("No room for the loot.");
            return;
        }
        var durabilities = item.MaxDurability > 0 ? new List<int> { Mathf.RoundToInt(pick.durability) } : null;
        int removed = from.RemoveItems(item, amount);
        if (removed <= 0)
            return;
        to.AddItem(item, removed, durabilities);
        to.ShowMessage($"Stole {item.Name}{(removed > 1 ? $" x{removed}" : "")}!", InventoryFeedback.Kind.Info);
        from.ShowMessage($"{item.Name} was stolen!");
    }

    public override string Describe(in AbilityStats s) => wholeStack ? "steals a random stack of items" : "steals a random item";
}

// ====================================================================== links
/// <summary>Soul Link: links the caster to the target; damage either one takes, the other takes too.</summary>
[Serializable, AbilityMenu("Link/Soul Link", "Links the caster and the target: the damage either takes is also dealt to the other (both ways).", 0)]
public class SoulLinkEffect : AbilityEffect
{
    [Min(0.5f)] public float duration = 10f;
    [Tooltip("Percentage of the damage passed on (100 = the same amount).")]
    [Range(1f, 200f)] public float sharePercent = 100f;
    [Tooltip("The link breaks beyond this distance (metres).")]
    [Min(1f)] public float maxDistance = 20f;
    public Color lineColor = new Color(0.7f, 0.3f, 1f, 0.9f);

    public SoulLinkEffect() { onlyAffects = TargetFilter.All & ~TargetFilter.Self; }

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.caster == null || ctx.target == null || ctx.caster == ctx.target)
            return;
        var p = new SoulLinkPower { sharePercent = sharePercent, maxDistance = maxDistance, lineColor = lineColor };
        p.SetPartner(ctx.target);
        ActivePowers.For(ctx.caster).Run("soul link", "Soul Link", new CharacterPower[] { p }, duration * Mathf.Max(0.1f, ctx.stats.duration), 1f, ctx.caster);
    }

    public override string Describe(in AbilityStats s) => $"links souls with the target for {duration * s.duration:0.#} s: {sharePercent:0}% of the damage either takes is dealt to the other";
}

/// <summary>Leech: a tether that keeps draining the target's health into the caster.</summary>
[Serializable, AbilityMenu("Link/Leech Life", "A tether from the caster to the target that keeps draining its health and healing the caster.", 0)]
public class LeechEffect : AbilityEffect
{
    [Min(0.5f)] public float duration = 5f;
    [Min(0f)] public float damagePerSecond = 8f;
    [Tooltip("Percentage of the damage the caster gets back as health.")]
    [Range(0f, 200f)] public float healPercent = 100f;
    [Tooltip("The tether breaks beyond this distance (metres).")]
    [Min(1f)] public float maxDistance = 14f;
    public ElementType element = ElementType.Dark;
    public Color lineColor = new Color(0.8f, 0.05f, 0.1f, 0.9f);

    public LeechEffect() { onlyAffects = TargetFilter.Enemies | TargetFilter.Neutral; }

    public override bool IsHarmful => true;

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.caster == null || ctx.target == null || ctx.caster == ctx.target)
            return;
        var p = new LeechPower { damagePerSecond = damagePerSecond * Mathf.Max(0f, ctx.stats.damage), healPercent = healPercent, maxDistance = maxDistance, element = element, lineColor = lineColor };
        p.SetPartner(ctx.target);
        ActivePowers.For(ctx.caster).Run("leech", "Leech Life", new CharacterPower[] { p }, duration * Mathf.Max(0.1f, ctx.stats.duration), 1f, ctx.caster);
    }

    public override float EstimateDamage(in AbilityStats s) => damagePerSecond * duration * s.damage;
    public override string Describe(in AbilityStats s) => $"drains {damagePerSecond * s.damage:0.#} health per second for {duration * s.duration:0.#} s ({healPercent:0}% heals the caster)";
}

/// <summary>What <see cref="DebtEffect"/> makes the next ability cost.</summary>
public enum DebtKind
{
    /// <summary>Picks one of the two at random.</summary>
    Random,
    /// <summary>The next ability costs more mana, stamina and health.</summary>
    Cost,
    /// <summary>The next ability's cooldown is longer.</summary>
    Cooldown,
}

/// <summary>Incoming Debt: the target's next ability costs more or recharges slower.</summary>
[Serializable, AbilityMenu("Debuff/Incoming Debt (next ability)", "The target's next ability costs more mana/stamina, or its cooldown is longer.", 0)]
public class DebtEffect : AbilityEffect
{
    public DebtKind kind = DebtKind.Random;
    [Tooltip("Multiplies the cost or the cooldown of that ability.")]
    [Range(1f, 5f)] public float multiplier = 2f;
    [Tooltip("Seconds the debt waits for the next ability before it expires.")]
    [Min(1f)] public float window = 20f;

    public DebtEffect() { onlyAffects = TargetFilter.All & ~TargetFilter.Self; }

    public override bool IsHarmful => true;

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.target == null)
            return;
        DebtKind k = kind == DebtKind.Random ? (UnityEngine.Random.value < 0.5f ? DebtKind.Cost : DebtKind.Cooldown) : kind;
        var p = new DebtPower { kind = k, multiplier = multiplier };
        ActivePowers.For(ctx.target).Run("debt", k == DebtKind.Cost ? "Debt: costs" : "Debt: cooldown", new CharacterPower[] { p }, window, 1f, ctx.caster);
    }

    public override string Describe(in AbilityStats s) =>
        kind == DebtKind.Cost ? $"the target's next ability costs x{multiplier:0.#}" :
        kind == DebtKind.Cooldown ? $"the target's next ability's cooldown is x{multiplier:0.#}" :
        $"the target's next ability costs x{multiplier:0.#} or its cooldown is x{multiplier:0.#}";
}

// ====================================================================== gates
/// <summary>
/// Twin Gates: opens a gate in front of the caster and another at the aimed point; any character (player, mob, summon)
/// that walks into one comes out of the other.
/// </summary>
[Serializable, AbilityMenu("Ground/Twin Gates (Portals)", "Opens two linked gates: one in front of the caster, one at the aimed point. Any creature that enters one comes out of the other.", 0)]
public class TwinGatesAction : CastAction
{
    [Min(1f)] public float duration = 12f;
    [Tooltip("How close to the centre of a gate a character must get (metres).")]
    [Min(0.3f)] public float gateRadius = 1.1f;
    [Tooltip("Metres in front of the caster the first gate opens.")]
    [Min(0f)] public float nearDistance = 2f;
    [Tooltip("Seconds before the same character can go through again.")]
    [Min(0.2f)] public float reuseDelay = 1.5f;
    [Tooltip("Optional gate prefab (a ring, particles). Empty = a simple glowing disc.")]
    public GameObject gatePrefab;
    public Color gateColor = new Color(0.3f, 0.6f, 1f, 0.6f);

    public override void Execute(AbilityCastInstance cast)
    {
        if (cast.Cosmetic || cast.CasterEntity == null)
            return;
        CombatEntity caster = cast.CasterEntity;
        Vector3 fwd = cast.AimDirection;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = caster.transform.forward;
        fwd.Normalize();
        Vector3 a = MirageCopy.Ground(caster.BasePosition + fwd * nearDistance, caster.transform);
        Vector3 b = MirageCopy.Ground(cast.AimPoint, caster.transform);
        var p = new TwinGatesPower { gateRadius = gateRadius, reuseDelay = reuseDelay, gatePrefab = gatePrefab, gateColor = gateColor };
        p.SetGates(a, b);
        ActivePowers.For(caster).Run("twin gates", "Twin Gates", new CharacterPower[] { p }, duration * Mathf.Max(0.1f, cast.Stats.duration), 1f, caster);
    }

    public override string Describe(AbilityDefinition def, in AbilityStats s) => $"opens two linked gates for {duration * s.duration:0.#} s";
}

/// <summary>Transposition: the caster and the target swap places.</summary>
[Serializable, AbilityMenu("Displacement/Swap Places (Transposition)", "The caster and the target swap places instantly.", 0)]
public class SwapPlacesEffect : AbilityEffect
{
    [Tooltip("Played at both places.")]
    public GameObject vfx;

    public SwapPlacesEffect() { onlyAffects = TargetFilter.All & ~TargetFilter.Self; }

    public override void Apply(ref EffectContext ctx)
    {
        CombatEntity a = ctx.caster, b = ctx.target;
        if (a == null || b == null || a == b || !b.IsAlive)
            return;
        if ((b.ActiveImmunities & ControlImmunity.Knockback) != 0 && CombatRelations.IsEnemy(a, b))
            return; // immovable targets cannot be moved
        Vector3 pa = a.BasePosition, pb = b.BasePosition;
        if (vfx != null)
        {
            UnityEngine.Object.Instantiate(vfx, pa, Quaternion.identity);
            UnityEngine.Object.Instantiate(vfx, pb, Quaternion.identity);
        }
        MovementAction.Teleport(a, pb);
        MovementAction.Teleport(b, pa);
    }

    public override string Describe(in AbilityStats s) => "swap places with the target";
}
