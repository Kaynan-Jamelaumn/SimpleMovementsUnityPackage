using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// The equipment effects available in the "Equip Effects" lists of items and in armor set bonuses. Each one returns a
// handle from Apply that removes exactly what it did (see EquipmentEffect). Grouped by the menu path of the dropdown.

// ====================================================================== stats
[Serializable, AbilityMenu("Stats/Combat Stats", "Defense, magic resistance, attributes, critical chance and damage, attack speed, elemental resistances.", 0)]
public class CombatStatsEffect : EquipmentEffect
{
    [Tooltip("Combat stat changes (Defense, Strength, Critical Chance, Attack Speed...). Hover a stat's value for its units.")]
    public List<CombatStatModifier> stats = new List<CombatStatModifier>();
    [Tooltip("Less (or more, when negative) damage from an element.")]
    public List<ElementalResistance> resistances = new List<ElementalResistance>();

    public CombatStatsEffect() { }

    public CombatStatsEffect(IEnumerable<CombatStatModifier> stats, IEnumerable<ElementalResistance> resistances = null)
    {
        if (stats != null) this.stats.AddRange(stats);
        if (resistances != null) this.resistances.AddRange(resistances);
    }

    public override string Describe(float strength = 1f)
    {
        var parts = new List<string>();
        foreach (CombatStatModifier m in stats)
            if (m != null && !Mathf.Approximately(m.value, 0f)) parts.Add(m.Describe(strength));
        foreach (ElementalResistance r in resistances)
            if (r != null && !Mathf.Approximately(r.percent, 0f)) parts.Add(r.Describe(strength));
        return string.Join(", ", parts);
    }

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        if ((stats == null || stats.Count == 0) && (resistances == null || resistances.Count == 0))
            return null;
        CombatStats cs = ctx.Stats;
        if (cs == null)
            return null;
        object token = cs.AddModifiers(ctx.CurrentSource, stats, resistances, strength);
        return new ActionHandle(() => { if (cs != null) cs.RemoveModifiers(token); });
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if ((stats == null || stats.Count == 0) && (resistances == null || resistances.Count == 0))
            warnings.Add($"{owner}: no stats or resistances configured.");
        if (stats != null)
            foreach (CombatStatModifier m in stats)
                if (m != null && Mathf.Approximately(m.value, 0f))
                    warnings.Add($"{owner}: {CombatStatInfo.Get(m.stat).name} is 0.");
        if (resistances != null)
            foreach (ElementalResistance r in resistances)
                if (r != null && r.element == ElementType.None)
                    errors.Add($"{owner}: a resistance has no element (pick Fire, Ice...).");
    }

    public override EquipmentEffect Clone()
    {
        var c = (CombatStatsEffect)base.Clone();
        c.stats = new List<CombatStatModifier>();
        foreach (CombatStatModifier m in stats)
            if (m != null) c.stats.Add(new CombatStatModifier(m.stat, m.value, m.onlyWithWeapon));
        c.resistances = new List<ElementalResistance>();
        foreach (ElementalResistance r in resistances)
            if (r != null) c.resistances.Add(new ElementalResistance(r.element, r.percent));
        return c;
    }
}

[Serializable, AbilityMenu("Stats/Character Stats", "Max health, stamina and mana, regeneration, move/sprint speed, jump, carry weight, damage taken, healing, ability damage, cooldowns... (the same stats traits use).", 1)]
public class CharacterStatsEffect : EquipmentEffect
{
    [Tooltip("Stat changes, exactly like a trait's passive modifiers (percent bonuses follow the base value when it changes).")]
    public List<TraitModifier> modifiers = new List<TraitModifier>();

    public CharacterStatsEffect() { }
    public CharacterStatsEffect(IEnumerable<TraitModifier> modifiers)
    {
        if (modifiers != null) this.modifiers.AddRange(modifiers);
    }

    public override string Describe(float strength = 1f)
    {
        var parts = new List<string>();
        foreach (TraitModifier m in modifiers)
            if (m != null) parts.Add(m.Describe(strength));
        return string.Join(", ", parts);
    }

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        if (modifiers == null || modifiers.Count == 0)
            return null;
        TraitManager tm = ctx.Traits;
        if (tm == null)
        {
            ctx.WarnOnce("char-stats-no-traits", $"[Equipment] {ctx.Owner?.name}: 'Character Stats' effects need a TraitManager on the character; they were skipped.");
            return null;
        }
        TraitManager.AppliedModifiers applied = tm.ApplyModifiers(modifiers, strength, ctx.CurrentSource);
        return new ActionHandle(() => { if (tm != null) tm.RevertModifiers(applied); });
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (modifiers == null || modifiers.Count == 0)
            warnings.Add($"{owner}: no modifiers configured.");
        else
            foreach (TraitModifier m in modifiers)
                if (m != null && Mathf.Approximately(m.value, 0f))
                    warnings.Add($"{owner}: {TraitStats.Get(m.stat).name} is 0.");
    }

    public override EquipmentEffect Clone()
    {
        var c = (CharacterStatsEffect)base.Clone();
        c.modifiers = new List<TraitModifier>();
        foreach (TraitModifier m in modifiers)
            if (m != null) c.modifiers.Add(new TraitModifier(m.stat, m.mode, m.value));
        return c;
    }
}

[Serializable, AbilityMenu("Stats/Classic Status Values", "The classic equipment stats (Max Hp, Stamina Regeneration, Hunger Heal Factor, Speed Factor...) with their stacking, level and duration options.", 2)]
public class ClassicStatsEffect : EquipmentEffect
{
    [Tooltip("Classic stat changes. Same settings as an item's 'Effects' list.")]
    public List<EquippableEffect> effects = new List<EquippableEffect>();

    public ClassicStatsEffect() { }
    public ClassicStatsEffect(IEnumerable<EquippableEffect> effects)
    {
        if (effects != null) this.effects.AddRange(effects);
    }

    public override string Describe(float strength = 1f)
    {
        var parts = new List<string>();
        foreach (EquippableEffect e in effects)
            if (e != null && !Mathf.Approximately(e.amount, 0f)) parts.Add(e.GetFormattedDescription(strength));
        return string.Join(", ", parts);
    }

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        if (effects == null || effects.Count == 0)
            return null;
        var h = new ClassicStatsHandle(ctx, strength);
        foreach (EquippableEffect e in effects)
            if (e != null && !Mathf.Approximately(e.amount, 0f))
                h.Add(e);
        return h.Count > 0 ? h : null;
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (effects == null)
            return;
        for (int i = 0; i < effects.Count; i++)
        {
            EquippableEffect e = effects[i];
            if (e == null)
                continue;
            e.Validate($"{owner} #{i + 1}", errors, warnings);
        }
    }

    /// <summary>One entry per classic effect: applied through the shared pool while its conditions hold.</summary>
    private sealed class ClassicStatsHandle : EquipmentEffectHandle
    {
        private sealed class Item
        {
            public EquippableEffect effect;
            public object token;
            public bool rolledOut;
            public float expiresAt;
            public GameObject vfx;
        }

        private readonly EquipmentContext ctx;
        private readonly float strength;
        private readonly List<Item> items = new List<Item>();
        private bool anyTick;

        public int Count => items.Count;

        public ClassicStatsHandle(EquipmentContext ctx, float strength)
        {
            this.ctx = ctx;
            this.strength = strength;
        }

        public void Add(EquippableEffect e)
        {
            var it = new Item
            {
                effect = e,
                // "Application Chance" is rolled once, when the item is equipped.
                rolledOut = e.applicationChance < 1f && UnityEngine.Random.value > e.applicationChance,
                expiresAt = e.IsTimed ? Time.time + e.duration : float.PositiveInfinity,
            };
            items.Add(it);
            anyTick |= e.IsTimed || e.minimumLevel > 1;
            Refresh(it, true);
        }

        private int Level
        {
            get
            {
                ExperienceManager xp = ctx.Status != null ? ctx.Status.XPManager : null;
                return xp != null ? xp.CurrentLevel : int.MaxValue;
            }
        }

        private void Refresh(Item it, bool first)
        {
            bool want = !it.rolledOut && Time.time < it.expiresAt && Level >= it.effect.minimumLevel;
            if (want && it.token == null)
            {
                it.token = ctx.LegacyStats.Add(it.effect.effectType, it.effect.amount * strength, it.effect.canStack, it.effect.maxStacks);
                if (it.effect.effectSound != null)
                    ctx.PlayOneShot(it.effect.effectSound);
                if (it.effect.effectPrefab != null && ctx.Body != null)
                    it.vfx = UnityEngine.Object.Instantiate(it.effect.effectPrefab, ctx.Body, false);
            }
            else if (!want && it.token != null)
            {
                Remove(it);
            }
        }

        private void Remove(Item it)
        {
            if (it.token != null)
                ctx.LegacyStats.Remove(it.token);
            it.token = null;
            if (it.vfx != null)
                UnityEngine.Object.Destroy(it.vfx);
            it.vfx = null;
        }

        public override bool NeedsTick => anyTick;

        public override void Tick(float dt)
        {
            for (int i = 0; i < items.Count; i++)
                Refresh(items[i], false);
        }

        public override string LiveStatus
        {
            get
            {
                int on = 0;
                foreach (Item it in items) if (it.token != null) on++;
                return on == items.Count ? null : $"{on}/{items.Count} active";
            }
        }

        protected override void OnRevert()
        {
            for (int i = items.Count - 1; i >= 0; i--)
                Remove(items[i]);
            items.Clear();
        }
    }
}

// ====================================================================== traits
[Serializable, AbilityMenu("Traits/Grant Traits", "Gives traits while active. They are free, the player cannot remove them, and they stay if the character also has them from somewhere else.", 0)]
public class GrantTraitsEffect : EquipmentEffect
{
    [Tooltip("Traits given while this is active.")]
    public List<Trait> traits = new List<Trait>();

    public GrantTraitsEffect() { }
    public GrantTraitsEffect(IEnumerable<Trait> traits)
    {
        if (traits != null) this.traits.AddRange(traits);
    }

    public override string Describe(float strength = 1f)
    {
        var names = new List<string>();
        foreach (Trait t in traits)
            if (t != null) names.Add(t.Name);
        return names.Count == 0 ? "" : "Grants " + string.Join(", ", names);
    }

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        if (traits == null || traits.Count == 0)
            return null;
        TraitManager tm = ctx.Traits;
        if (tm == null)
        {
            ctx.WarnOnce("grant-no-traits", $"[Equipment] {ctx.Owner?.name}: granting traits needs a TraitManager on the character; they were skipped.");
            return null;
        }
        var granted = new List<Trait>(traits.Count);
        foreach (Trait t in traits)
        {
            if (t == null)
                continue;
            tm.AddTrait(t, true); // a free grant; counted if the character already has the trait
            tm.RegisterEquipmentGrant(t);
            granted.Add(t);
        }
        if (granted.Count == 0)
            return null;
        return new ActionHandle(() =>
        {
            if (tm == null)
                return;
            for (int i = granted.Count - 1; i >= 0; i--)
            {
                tm.UnregisterEquipmentGrant(granted[i]);
                tm.RemoveTrait(granted[i], true); // releases only this grant
            }
        });
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (traits == null || traits.Count == 0)
            warnings.Add($"{owner}: no traits assigned.");
        else if (traits.Contains(null))
            warnings.Add($"{owner}: has empty trait entries.");
    }

    public override EquipmentEffect Clone()
    {
        var c = (GrantTraitsEffect)base.Clone();
        c.traits = new List<Trait>(traits);
        return c;
    }
}

[Serializable, AbilityMenu("Traits/Enhance Trait", "Strengthens, extends or replaces one of the character's traits while active.", 1)]
public class EnhanceTraitEffect : EquipmentEffect
{
    public enum Mode
    {
        /// <summary>The trait's modifiers and behaviours get stronger (x Multiplier).</summary>
        Strengthen,
        /// <summary>Extra modifiers while the character has the trait.</summary>
        AddModifiers,
        /// <summary>The trait is switched off and the replacement given instead (nothing = just switched off).</summary>
        Replace,
    }

    [Tooltip("The trait to enhance. Nothing happens while the character does not have it (Strengthen still applies as soon as it gets it).")]
    public Trait trait;
    [Tooltip("Strengthen: multiply the trait's effects. Add Modifiers: extra stats while the character has the trait. Replace: swap it for another trait while active.")]
    public Mode mode = Mode.Strengthen;
    [Tooltip("Strengthen: 1.5 = 50% stronger.")]
    [Min(0f)] public float multiplier = 1.5f;
    [Tooltip("Add Modifiers: applied while the character has the trait.")]
    public List<TraitModifier> extraModifiers = new List<TraitModifier>();
    [Tooltip("Replace: the trait given instead. Empty = the trait is only switched off.")]
    public Trait replacement;
    [Tooltip("Strengthen: multiplies with other stacking enhancements of the same trait. Off = only the strongest non-stacking enhancement counts.")]
    public bool stacks = false;
    [Tooltip("When several sources strengthen (without stacking) or replace the same trait, the highest priority wins.")]
    public int priority = 0;
    [Tooltip("Optional text for tooltips (empty = generated).")]
    public string description = "";

    public override string Describe(float strength = 1f)
    {
        if (!string.IsNullOrEmpty(description))
            return description;
        string n = trait != null ? trait.Name : "(no trait)";
        switch (mode)
        {
            case Mode.Strengthen: return $"{n} is {(1f + (multiplier - 1f) * strength - 1f) * 100f:+0;-0}% stronger";
            case Mode.AddModifiers:
                var parts = new List<string>();
                foreach (TraitModifier m in extraModifiers) if (m != null) parts.Add(m.Describe(strength));
                return $"{n}: {string.Join(", ", parts)}";
            default:
                return replacement != null ? $"{n} becomes {replacement.Name}" : $"{n} is suppressed";
        }
    }

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        if (trait == null)
            return null;
        TraitManager tm = ctx.Traits;
        if (tm == null)
        {
            ctx.WarnOnce("enhance-no-traits", $"[Equipment] {ctx.Owner?.name}: trait enhancements need a TraitManager on the character; they were skipped.");
            return null;
        }
        if (mode == Mode.Strengthen)
        {
            object token = ctx.TraitMultipliers.Push(trait, 1f + (multiplier - 1f) * strength, stacks, priority);
            return token == null ? null : new ActionHandle(() => ctx.TraitMultipliers.Pop(token));
        }
        var h = new EnhanceHandle(ctx, tm, this, strength);
        h.Start();
        return h;
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (trait == null)
            errors.Add($"{owner}: no trait selected.");
        if (mode == Mode.Strengthen && multiplier <= 0f)
            errors.Add($"{owner}: the multiplier must be above 0.");
        if (mode == Mode.Strengthen && Mathf.Approximately(multiplier, 1f))
            warnings.Add($"{owner}: a multiplier of 1 changes nothing.");
        if (mode == Mode.AddModifiers && (extraModifiers == null || extraModifiers.Count == 0))
            warnings.Add($"{owner}: no extra modifiers.");
        if (mode == Mode.Replace && replacement != null && replacement == trait)
            errors.Add($"{owner}: a trait cannot replace itself.");
    }

    public override EquipmentEffect Clone()
    {
        var c = (EnhanceTraitEffect)base.Clone();
        c.extraModifiers = new List<TraitModifier>();
        foreach (TraitModifier m in extraModifiers)
            if (m != null) c.extraModifiers.Add(new TraitModifier(m.stat, m.mode, m.value));
        return c;
    }

    /// <summary>Converts an old armor-set trait enhancement (Multiply, Add Effects, Replace, Upgrade).</summary>
    public static EnhanceTraitEffect FromLegacy(TraitEnhancement e, bool stacks, int priority)
    {
        if (e == null || e.originalTrait == null)
            return null;
        var fx = new EnhanceTraitEffect
        {
            trait = e.originalTrait,
            stacks = stacks,
            priority = priority,
            description = e.enhancedDescription ?? "",
        };
        switch (e.enhancementType)
        {
            case TraitEnhancementType.Multiply:
                fx.mode = Mode.Strengthen;
                fx.multiplier = e.effectMultiplier;
                break;
            case TraitEnhancementType.AddEffects:
                fx.mode = Mode.AddModifiers;
                if (e.additionalEffects != null)
                    foreach (TraitEffect te in e.additionalEffects)
                        if (TraitStats.TryMapLegacy(te, out TraitStat stat, out TraitModifierMode m, out float v))
                            fx.extraModifiers.Add(new TraitModifier(stat, m, v));
                break;
            case TraitEnhancementType.Upgrade:
                if (e.enhancedTrait == null)
                    return null; // an upgrade without a trait did nothing before either
                fx.mode = Mode.Replace;
                fx.replacement = e.enhancedTrait;
                break;
            default:
                fx.mode = Mode.Replace;
                fx.replacement = e.enhancedTrait;
                break;
        }
        return fx;
    }

    /// <summary>Add Modifiers / Replace: follows the character's traits (applies when it gets the trait, undoes when it loses it).</summary>
    private sealed class EnhanceHandle : EquipmentEffectHandle
    {
        private readonly EquipmentContext ctx;
        private readonly TraitManager tm;
        private readonly EnhanceTraitEffect fx;
        private readonly float strength;
        private TraitManager.AppliedModifiers mods;
        private object replaceToken;
        private bool busy, pending, subscribed;

        public EnhanceHandle(EquipmentContext ctx, TraitManager tm, EnhanceTraitEffect fx, float strength)
        {
            this.ctx = ctx;
            this.tm = tm;
            this.fx = fx;
            this.strength = strength;
        }

        public void Start()
        {
            tm.OnTraitsChanged += Evaluate;
            subscribed = true;
            Evaluate();
        }

        public override string LiveStatus => mods != null || replaceToken != null ? "active" : $"waiting for {fx.trait.Name}";

        private void Evaluate()
        {
            if (Reverted)
                return;
            if (busy)
            {
                // Suspending or granting traits raises the change event again: evaluate once more afterwards.
                pending = true;
                return;
            }
            busy = true;
            try
            {
                for (int guard = 0; guard < 4; guard++)
                {
                    pending = false;
                    if (fx.mode == Mode.AddModifiers)
                    {
                        bool want = tm.HasTrait(fx.trait);
                        if (want && mods == null)
                            mods = tm.ApplyModifiers(fx.extraModifiers, strength, $"{ctx.CurrentSource} ({fx.trait.Name})");
                        else if (!want && mods != null)
                        {
                            tm.RevertModifiers(mods);
                            mods = null;
                        }
                    }
                    else
                    {
                        // Owned counts suspended traits too (the trait is suspended BY this replacement).
                        bool want = tm.GetTraitInfo(fx.trait) != null;
                        if (want && replaceToken == null)
                            replaceToken = ctx.TraitReplacements.Push(fx.trait, fx.replacement, fx.priority);
                        else if (!want && replaceToken != null)
                        {
                            ctx.TraitReplacements.Pop(replaceToken);
                            replaceToken = null;
                        }
                    }
                    if (!pending)
                        break;
                }
            }
            finally
            {
                busy = false;
            }
        }

        protected override void OnRevert()
        {
            if (subscribed && tm != null)
                tm.OnTraitsChanged -= Evaluate;
            subscribed = false;
            if (mods != null && tm != null)
                tm.RevertModifiers(mods);
            mods = null;
            if (replaceToken != null)
                ctx.TraitReplacements.Pop(replaceToken);
            replaceToken = null;
        }
    }
}

[Serializable, AbilityMenu("Traits/Passive Behaviour", "Runs a trait behaviour while active - double jump, glide, wall climb, life steal, thorns, second wind, cheat death, out-of-combat regeneration, conditional modifiers... - without creating a Trait asset.", 2)]
public class PassiveBehaviourEffect : EquipmentEffect
{
    [Tooltip("The behaviour to run while active (the same list traits use).")]
    [SerializeReference, SubclassSelector] public TraitBehaviour behaviour;

    public override string Describe(float strength = 1f) => behaviour != null ? behaviour.Describe() : "";

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f) => BehaviourHandle.Start(ctx, behaviour, strength);

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (behaviour == null)
        {
            errors.Add($"{owner}: no behaviour selected.");
            return;
        }
        var e = new List<string>();
        var w = new List<string>();
        behaviour.Validate(e, w);
        foreach (string s in e) errors.Add($"{owner}: {s}");
        foreach (string s in w) warnings.Add($"{owner}: {s}");
    }

    public override EquipmentEffect Clone()
    {
        var c = (PassiveBehaviourEffect)base.Clone();
        c.behaviour = behaviour != null ? behaviour.CreateRuntimeCopy() : null;
        return c;
    }
}

[Serializable, AbilityMenu("Abilities/Ability On A Key", "Gives an ability (a barrier, a blink, a heal...) cast with its own key while active.", 0)]
public class AbilityOnKeyEffect : EquipmentEffect
{
    [Tooltip("The ability given (an Ability Definition asset).")]
    public AbilityDefinition ability;
    [Tooltip("Changes applied on top of the ability.")]
    public AbilityModifierSet modifiers = new AbilityModifierSet();
    [Tooltip("The key (input action) that casts it. Empty = only scripts/UI can use it.")]
    public InputActionReference input;

    public override string Describe(float strength = 1f) =>
        ability != null ? $"Ability: {ability.DisplayName}" + (input != null && input.action != null ? $" ({input.action.name})" : "") : "";

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        if (ability == null)
            return null;
        var runtime = new ActiveAbilityTrait { ability = ability, modifiers = modifiers, input = input };
        return BehaviourHandle.Start(ctx, runtime, strength);
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (ability == null)
            errors.Add($"{owner}: no ability assigned.");
        if (input == null)
            warnings.Add($"{owner}: no key assigned; only scripts or UI can cast it.");
    }

    public override EquipmentEffect Clone()
    {
        var c = (AbilityOnKeyEffect)base.Clone();
        c.modifiers = modifiers != null ? modifiers.Clone() : new AbilityModifierSet();
        return c;
    }
}

/// <summary>Runs a per-character copy of a <see cref="TraitBehaviour"/> (passive behaviours and abilities on keys).</summary>
internal sealed class BehaviourHandle : EquipmentEffectHandle
{
    private TraitBehaviour runtime;
    private TraitContext context;

    public static EquipmentEffectHandle Start(EquipmentContext ctx, TraitBehaviour template, float strength)
    {
        if (template == null)
            return null;
        TraitManager tm = ctx.Traits;
        if (tm == null)
        {
            ctx.WarnOnce("behaviour-no-traits", $"[Equipment] {ctx.Owner?.name}: passive behaviours need a TraitManager on the character; they were skipped.");
            return null;
        }
        var h = new BehaviourHandle { context = tm.Context, runtime = template.CreateRuntimeCopy() };
        h.runtime.multiplier = strength;
        try { h.runtime.OnAdded(h.context); }
        catch (Exception e) { Debug.LogException(e, ctx.Host); }
        return h;
    }

    public override bool NeedsTick => true;
    public override void Tick(float dt) => runtime?.Tick(context, dt);
    public override string LiveStatus => runtime?.LiveStatus;

    /// <summary>Uses the behaviour's active part (abilities), for UI buttons and scripts.</summary>
    public bool TryActivate() => runtime != null && runtime.TryActivate(context);

    protected override void OnRevert()
    {
        runtime?.OnRemoved(context);
        runtime = null;
    }
}

// ====================================================================== triggered
/// <summary>Which hits trigger an "On Hit" effect.</summary>
public enum ProcTrigger
{
    /// <summary>Weapon attacks only.</summary>
    WeaponHits,
    /// <summary>Abilities only.</summary>
    AbilityHits,
    /// <summary>Any damage the wearer deals directly (not damage over time).</summary>
    AnyDamage,
}

/// <summary>Shared by the triggered effects: applies "on hit" style effects without them triggering each other forever.</summary>
public static class EquipmentProcs
{
    /// <summary>Above 0 while triggered effects are being applied: damage dealt then never triggers other effects.</summary>
    public static int Depth { get; private set; }

    private static readonly HashSet<AbilityDefinition> procAbilities = new HashSet<AbilityDefinition>();

    /// <summary>Abilities cast by triggered effects: their (possibly delayed) damage never triggers effects again.</summary>
    public static bool IsProcAbility(AbilityDefinition ability) => ability != null && procAbilities.Contains(ability);

    public static void RegisterProcAbility(AbilityDefinition ability)
    {
        if (ability != null)
            procAbilities.Add(ability);
    }

    /// <summary>
    /// Applies <paramref name="hit"/> to <paramref name="target"/> as if <paramref name="source"/> cast it, casts
    /// <paramref name="castAbility"/> at it, and plays the feedback.
    /// </summary>
    public static void Apply(CombatEntity source, CombatEntity target, HitSettings hit, AbilityDefinition castAbility, float strength, GameObject vfx, AudioClip sound)
    {
        if (target == null)
            return;
        Depth++;
        try
        {
            AbilityModifierSet mods = Mathf.Approximately(strength, 1f) ? null : new AbilityModifierSet
            {
                label = "Equipment",
                damageMultiplier = Mathf.Max(0f, strength),
                healMultiplier = Mathf.Max(0f, strength),
                durationMultiplier = Mathf.Max(0f, strength),
            };
            Vector3 from = source != null ? source.Center : target.Center;
            Vector3 dir = CombatQuery.FlatDirection(from, target.Center, Vector3.forward);
            if (hit != null && hit.effects != null && hit.effects.Count > 0 && target.IsAlive)
            {
                var cast = new AbilityCastInstance(null, source, null, null, mods);
                if (source == null)
                    cast.SetOrigin(target.BasePosition, target.Center, Quaternion.identity);
                cast.ApplyHit(hit, target, from, dir);
            }
            if (castAbility != null)
                AbilityCaster.ExecuteInstant(castAbility, source, target.BasePosition, dir, target, mods);
            if (vfx != null)
                AbilityPool.PlayVfx(vfx, target.Center, Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : Vector3.forward), 0f);
            if (sound != null)
                AbilityPool.PlaySound(sound, target.Center, 1f);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            Depth--;
        }
    }

    public static string DescribeEffects(List<AbilityEffect> effects)
    {
        if (effects == null || effects.Count == 0)
            return "";
        var sb = new StringBuilder();
        AbilityStats s = AbilityStats.Identity;
        foreach (AbilityEffect e in effects)
        {
            if (e == null) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(e.Describe(s));
        }
        return sb.ToString();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Depth = 0;
        procAbilities.Clear();
    }
}

[Serializable, AbilityMenu("Triggered/On Hit: Affect Target", "When the wearer hits someone: a chance to apply effects to it (burn, poison, slow, stun, bonus damage...) or cast an ability at it.", 0)]
public class OnHitEffect : EquipmentEffect
{
    [Tooltip("Which hits can trigger it.")]
    public ProcTrigger trigger = ProcTrigger.WeaponHits;
    [Tooltip("Chance (0-1) per hit.")]
    [Range(0f, 1f)] public float chance = 0.2f;
    [Tooltip("Seconds before it can trigger again (0 = every hit can).")]
    [Min(0f)] public float cooldown = 0f;
    [Tooltip("Only critical hits trigger it.")]
    public bool onlyCriticalHits = false;
    [Tooltip("Weapon hits: only with this weapon category (None = any weapon).")]
    public WeaponCategory onlyWithWeapon = WeaponCategory.None;
    [Tooltip("Applied to the character that was hit (an effect whose Recipient is Caster goes to the wearer instead).")]
    [SerializeReference, SubclassSelector] public List<AbilityEffect> effects = new List<AbilityEffect>();
    [Tooltip("Optional ability cast at the character that was hit.")]
    public AbilityDefinition castAbility;
    [Tooltip("Effect spawned on the target when it triggers.")]
    public GameObject procVfx;
    [Tooltip("Sound played when it triggers.")]
    public AudioClip procSound;

    public override string Describe(float strength = 1f)
    {
        string what = EquipmentProcs.DescribeEffects(effects);
        if (castAbility != null)
            what = string.IsNullOrEmpty(what) ? castAbility.DisplayName : $"{what}, {castAbility.DisplayName}";
        string when = trigger == ProcTrigger.WeaponHits ? "weapon hits" : trigger == ProcTrigger.AbilityHits ? "ability hits" : "hits";
        return $"{chance * 100f:0}% chance on {(onlyCriticalHits ? "critical " : "")}{when}: {what}" + (cooldown > 0f ? $" (every {cooldown:0.#}s at most)" : "");
    }

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        if ((effects == null || effects.Count == 0) && castAbility == null)
            return null;
        EquipmentProcs.RegisterProcAbility(castAbility);
        return new Handle(ctx, this, strength);
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if ((effects == null || effects.Count == 0) && castAbility == null)
            errors.Add($"{owner}: add effects or an ability to cast.");
        if (chance <= 0f)
            warnings.Add($"{owner}: 0% chance, it never triggers.");
        if (effects != null)
            foreach (AbilityEffect e in effects)
                e?.Validate(owner, errors, warnings);
    }

    private sealed class Handle : EquipmentEffectHandle
    {
        private readonly EquipmentContext ctx;
        private readonly OnHitEffect fx;
        private readonly float strength;
        private readonly HitSettings hit;
        private float readyAt;

        public Handle(EquipmentContext ctx, OnHitEffect fx, float strength)
        {
            this.ctx = ctx;
            this.fx = fx;
            this.strength = strength;
            hit = new HitSettings { filter = TargetFilter.All, blockedByObstacles = false, effects = fx.effects ?? new List<AbilityEffect>() };
            CombatEvents.Damaged += OnDamaged;
        }

        public override string LiveStatus => Time.time >= readyAt ? "ready" : $"cooldown {readyAt - Time.time:0.0}s";

        private void OnDamaged(DamageInfo info)
        {
            CombatEntity self = ctx.Entity;
            if (self == null || info.source != self || info.target == null || info.target == self)
                return;
            if (info.amount <= 0f || info.isPeriodic || info.isReflected || EquipmentProcs.Depth > 0 || EquipmentProcs.IsProcAbility(info.ability))
                return;
            switch (fx.trigger)
            {
                case ProcTrigger.WeaponHits:
                    if (info.weapon == null) return;
                    if (fx.onlyWithWeapon != WeaponCategory.None && info.weapon.Category != fx.onlyWithWeapon) return;
                    break;
                case ProcTrigger.AbilityHits:
                    if (info.ability == null) return;
                    break;
            }
            if (fx.onlyCriticalHits && !info.isCritical)
                return;
            if (Time.time < readyAt || (fx.chance < 1f && UnityEngine.Random.value > fx.chance))
                return;
            readyAt = Time.time + fx.cooldown;
            EquipmentProcs.Apply(self, info.target, hit, fx.castAbility, strength, fx.procVfx, fx.procSound);
        }

        protected override void OnRevert() => CombatEvents.Damaged -= OnDamaged;
    }
}

[Serializable, AbilityMenu("Triggered/When Hit: Affect Attacker", "When an enemy hurts the wearer: a chance to apply effects to it (spikes, frost, a knockback...) or cast an ability. Effects whose Recipient is Caster go to the wearer (a shield when hit).", 1)]
public class WhenHitEffect : EquipmentEffect
{
    [Tooltip("Chance (0-1) per hit taken.")]
    [Range(0f, 1f)] public float chance = 0.25f;
    [Tooltip("Seconds before it can trigger again (0 = every hit can).")]
    [Min(0f)] public float cooldown = 1f;
    [Tooltip("Only attackers within this distance (metres). 0 = any distance (arrows and spells too).")]
    [Min(0f)] public float maxDistance = 4f;
    [Tooltip("Damage over time ticks (poison, burning) can trigger it too.")]
    public bool includeDamageOverTime = false;
    [Tooltip("Applied to the attacker (Recipient = Caster: applied to the wearer).")]
    [SerializeReference, SubclassSelector] public List<AbilityEffect> effects = new List<AbilityEffect>();
    [Tooltip("Optional ability cast at the attacker.")]
    public AbilityDefinition castAbility;
    public GameObject procVfx;
    public AudioClip procSound;

    public override string Describe(float strength = 1f)
    {
        string what = EquipmentProcs.DescribeEffects(effects);
        if (castAbility != null)
            what = string.IsNullOrEmpty(what) ? castAbility.DisplayName : $"{what}, {castAbility.DisplayName}";
        return $"When hit{(maxDistance > 0f ? $" within {maxDistance:0.#} m" : "")}: {chance * 100f:0}% chance - {what}";
    }

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        if ((effects == null || effects.Count == 0) && castAbility == null)
            return null;
        EquipmentProcs.RegisterProcAbility(castAbility);
        var h = new Handle(ctx, this, strength);
        h.TrySubscribe();
        return h;
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if ((effects == null || effects.Count == 0) && castAbility == null)
            errors.Add($"{owner}: add effects or an ability to cast.");
        if (chance <= 0f)
            warnings.Add($"{owner}: 0% chance, it never triggers.");
        if (effects != null)
            foreach (AbilityEffect e in effects)
                e?.Validate(owner, errors, warnings);
    }

    private sealed class Handle : EquipmentEffectHandle
    {
        private readonly EquipmentContext ctx;
        private readonly WhenHitEffect fx;
        private readonly float strength;
        private readonly HitSettings hit;
        private CombatEntity subscribed;
        private float readyAt;

        public Handle(EquipmentContext ctx, WhenHitEffect fx, float strength)
        {
            this.ctx = ctx;
            this.fx = fx;
            this.strength = strength;
            hit = new HitSettings { filter = TargetFilter.All, blockedByObstacles = false, effects = fx.effects ?? new List<AbilityEffect>() };
        }

        // The combat entity can appear after the equipment (it is created by the combat system).
        public override bool NeedsTick => subscribed == null;
        public override void Tick(float dt) => TrySubscribe();
        public override string LiveStatus => subscribed == null ? "waiting for the combat entity" : Time.time >= readyAt ? "ready" : $"cooldown {readyAt - Time.time:0.0}s";

        public void TrySubscribe()
        {
            CombatEntity e = ctx.Entity;
            if (e == null || e == subscribed)
                return;
            if (subscribed != null)
                subscribed.Damaged -= OnDamaged;
            subscribed = e;
            e.Damaged += OnDamaged;
        }

        private void OnDamaged(DamageInfo info)
        {
            CombatEntity self = subscribed;
            CombatEntity attacker = info.source;
            if (self == null || attacker == null || attacker == self || !attacker.IsAlive || info.amount <= 0f)
                return;
            if (info.isReflected || EquipmentProcs.Depth > 0 || (info.isPeriodic && !fx.includeDamageOverTime))
                return;
            if (CombatRelations.Get(self, attacker) != CombatRelation.Enemy)
                return;
            if (fx.maxDistance > 0f && CombatQuery.FlatDistance(self.Position, attacker.Position) > fx.maxDistance + attacker.Radius)
                return;
            if (Time.time < readyAt || (fx.chance < 1f && UnityEngine.Random.value > fx.chance))
                return;
            readyAt = Time.time + fx.cooldown;
            EquipmentProcs.Apply(self, attacker, hit, fx.castAbility, strength, fx.procVfx, fx.procSound);
        }

        protected override void OnRevert()
        {
            if (subscribed != null)
                subscribed.Damaged -= OnDamaged;
            subscribed = null;
        }
    }
}

[Serializable, AbilityMenu("Triggered/Pulse Around Wearer", "Every few seconds, affects characters around the wearer: a burning aura for enemies, a healing pulse for allies...", 2)]
public class PulseEffect : EquipmentEffect
{
    [Tooltip("Seconds between pulses.")]
    [Min(0.1f)] public float interval = 2f;
    [Tooltip("Radius around the wearer (metres).")]
    [Min(0.1f)] public float radius = 4f;
    [Tooltip("Who is affected, relative to the wearer.")]
    public TargetFilter affects = TargetFilter.Enemies;
    [Tooltip("Maximum characters per pulse (closest first). 0 = no limit.")]
    [Min(0)] public int maxTargets = 0;
    [Tooltip("Only pulses while the wearer is fighting (took or dealt damage in the last few seconds).")]
    public bool onlyInCombat = false;
    [Tooltip("Applied to each character in range.")]
    [SerializeReference, SubclassSelector] public List<AbilityEffect> effects = new List<AbilityEffect>();
    [Tooltip("Effect spawned on the wearer at each pulse.")]
    public GameObject pulseVfx;
    public AudioClip pulseSound;

    public override string Describe(float strength = 1f) =>
        $"Every {interval:0.#}s, {EquipmentProcs.DescribeEffects(effects)} to {Who()} within {radius:0.#} m" + (onlyInCombat ? " (in combat)" : "");

    private string Who()
    {
        if (affects == TargetFilter.Enemies) return "enemies";
        if (affects == TargetFilter.Allies) return "allies";
        if ((affects & TargetFilter.Self) != 0 && (affects & TargetFilter.Allies) != 0) return "you and allies";
        return "characters";
    }

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        if (effects == null || effects.Count == 0)
            return null;
        return new Handle(ctx, this, strength);
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (effects == null || effects.Count == 0)
            errors.Add($"{owner}: no effects to apply.");
        if (affects == TargetFilter.None)
            errors.Add($"{owner}: 'Affects' is empty.");
        if (effects != null)
            foreach (AbilityEffect e in effects)
                e?.Validate(owner, errors, warnings);
    }

    private sealed class Handle : EquipmentEffectHandle
    {
        private static readonly List<CombatEntity> buffer = new List<CombatEntity>(16);
        private readonly EquipmentContext ctx;
        private readonly PulseEffect fx;
        private readonly float strength;
        private readonly HitSettings hit;
        private float next;

        public Handle(EquipmentContext ctx, PulseEffect fx, float strength)
        {
            this.ctx = ctx;
            this.fx = fx;
            this.strength = strength;
            hit = new HitSettings { filter = fx.affects, blockedByObstacles = true, effects = fx.effects };
            next = Time.time + fx.interval;
        }

        public override bool NeedsTick => true;
        public override string LiveStatus => $"next pulse in {Mathf.Max(0f, next - Time.time):0.0}s";

        public override void Tick(float dt)
        {
            if (Time.time < next)
                return;
            next = Time.time + fx.interval;
            CombatEntity self = ctx.Entity;
            if (self == null || !self.IsAlive)
                return;
            if (fx.onlyInCombat && Time.time - self.LastDamagedTime > 5f && Time.time - self.LastDealtDamageTime > 5f)
                return;
            buffer.Clear();
            CombatQuery.InRadius(self.Position, fx.radius, self, fx.affects, buffer);
            if (fx.maxTargets > 0 && buffer.Count > fx.maxTargets)
                CombatQuery.SortByDistance(buffer, self.Position);
            int n = fx.maxTargets > 0 ? Mathf.Min(fx.maxTargets, buffer.Count) : buffer.Count;
            for (int i = 0; i < n; i++)
                EquipmentProcs.Apply(self, buffer[i], hit, null, strength, null, null);
            buffer.Clear();
            if (fx.pulseVfx != null)
                AbilityPool.PlayVfx(fx.pulseVfx, self.BasePosition, Quaternion.identity, 0f, 1f, self.transform);
            if (fx.pulseSound != null)
                AbilityPool.PlaySound(fx.pulseSound, self.Center, 1f);
        }

        protected override void OnRevert() { }
    }
}

// ====================================================================== conditional
[Serializable, AbilityMenu("Conditional/While...", "Effects that apply only in a situation: low health, in combat, wielding a certain weapon...", 0)]
public class WhileConditionEffect : EquipmentEffect
{
    public enum Condition
    {
        HealthBelow,
        HealthAbove,
        ManaBelow,
        StaminaBelow,
        InCombat,
        OutOfCombat,
        /// <summary>Wielding a weapon of the category (None = any weapon).</summary>
        WieldingWeapon,
        /// <summary>No weapon in hand.</summary>
        Unarmed,
    }

    [Tooltip("When the effects below apply.")]
    public Condition condition = Condition.HealthBelow;
    [Tooltip("Health/Mana/Stamina: the fraction of the maximum (0.3 = 30%).")]
    [Range(0f, 1f)] public float threshold = 0.3f;
    [Tooltip("In/Out Of Combat: seconds since the wearer last took or dealt damage.")]
    [Min(0.5f)] public float combatWindow = 5f;
    [Tooltip("Wielding Weapon: the weapon category (None = any weapon).")]
    public WeaponCategory weapon = WeaponCategory.None;
    [Tooltip("Keeps the effects this many seconds after the condition stops, so a flickering condition does not switch them every frame.")]
    [Min(0f)] public float lingerSeconds = 0.25f;
    [Tooltip("Effects active while the condition holds.")]
    [SerializeReference, SubclassSelector] public List<EquipmentEffect> effects = new List<EquipmentEffect>();

    public override string Describe(float strength = 1f)
    {
        var lines = new List<string>();
        DescribeAll(effects, strength, lines);
        return $"While {ConditionText()}: {string.Join(", ", lines)}";
    }

    private string ConditionText()
    {
        switch (condition)
        {
            case Condition.HealthBelow: return $"health is below {threshold * 100f:0}%";
            case Condition.HealthAbove: return $"health is above {threshold * 100f:0}%";
            case Condition.ManaBelow: return $"mana is below {threshold * 100f:0}%";
            case Condition.StaminaBelow: return $"stamina is below {threshold * 100f:0}%";
            case Condition.InCombat: return "in combat";
            case Condition.OutOfCombat: return "out of combat";
            case Condition.WieldingWeapon: return weapon == WeaponCategory.None ? "wielding a weapon" : $"wielding a {weapon}";
            default: return "unarmed";
        }
    }

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        if (effects == null || effects.Count == 0)
            return null;
        var h = new Handle(ctx, this, strength, ctx.CurrentSource);
        h.Tick(0f);
        return h;
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (effects == null || effects.Count == 0)
            warnings.Add($"{owner}: no effects.");
        ValidateAll(effects, owner, errors, warnings);
    }

    public override EquipmentEffect Clone()
    {
        var c = (WhileConditionEffect)base.Clone();
        c.effects = new List<EquipmentEffect>();
        foreach (EquipmentEffect e in effects)
            c.effects.Add(e?.Clone());
        return c;
    }

    private bool Holds(EquipmentContext ctx)
    {
        PlayerStatusController s = ctx.Status;
        CombatEntity e = ctx.Entity;
        switch (condition)
        {
            case Condition.HealthBelow: return Ratio(e, s != null ? s.HpManager : null) < threshold;
            case Condition.HealthAbove: return Ratio(e, s != null ? s.HpManager : null) > threshold;
            case Condition.ManaBelow: return Ratio(null, s != null ? s.ManaManager : null) < threshold;
            case Condition.StaminaBelow: return Ratio(null, s != null ? s.StaminaManager : null) < threshold;
            case Condition.InCombat: return InCombat(e);
            case Condition.OutOfCombat: return !InCombat(e);
            case Condition.WieldingWeapon:
            {
                CombatStats cs = ctx.Stats;
                if (cs == null || cs.WieldedWeapon == null) return false;
                return weapon == WeaponCategory.None || cs.WieldedCategory == weapon;
            }
            default:
            {
                CombatStats cs = ctx.Stats;
                return cs == null || cs.WieldedWeapon == null;
            }
        }
    }

    private static float Ratio(CombatEntity e, StatusManager m)
    {
        if (e != null && m == null)
            return e.HealthRatio;
        return m != null && m.MaxValue > 0f ? m.CurrentValue / m.MaxValue : 1f;
    }

    private bool InCombat(CombatEntity e)
    {
        if (e == null) return false;
        float now = Time.time;
        return now - e.LastDamagedTime < combatWindow || now - e.LastDealtDamageTime < combatWindow;
    }

    private sealed class Handle : CompositeHandle
    {
        private readonly EquipmentContext ctx;
        private readonly WhileConditionEffect fx;
        private readonly float strength;
        private readonly string source;
        private bool active;
        private float lastHeld = -999f;

        public Handle(EquipmentContext ctx, WhileConditionEffect fx, float strength, string source)
        {
            this.ctx = ctx;
            this.fx = fx;
            this.strength = strength;
            this.source = source;
        }

        public override bool NeedsTick => true;
        public override string LiveStatus => active ? "ACTIVE" : "inactive";

        public override void Tick(float dt)
        {
            if (fx.Holds(ctx))
            {
                lastHeld = Time.time;
                if (!active)
                {
                    active = true;
                    EquipmentEffect.ApplyAll(fx.effects, ctx, strength, source + " (conditional)", children);
                }
            }
            else if (active && Time.time - lastHeld >= fx.lingerSeconds)
            {
                active = false;
                EquipmentEffect.RevertAll(children);
            }
            if (active)
                base.Tick(dt);
        }

        protected override void OnRevert()
        {
            active = false;
            base.OnRevert();
        }
    }
}

// ====================================================================== special & visuals
[Serializable, AbilityMenu("Special/Special Mechanic", "Turns on a special mechanic handled by a Special Mechanic Handler component (water walking, gravity reduction...). Stays on while any item still provides it.", 0)]
public class SpecialMechanicEffect : EquipmentEffect
{
    [Tooltip("The mechanic (its ID must match a handler's supported mechanics).")]
    public SpecialMechanic mechanic = new SpecialMechanic();

    public SpecialMechanicEffect() { }
    public SpecialMechanicEffect(SpecialMechanic mechanic) => this.mechanic = mechanic;

    public override string Describe(float strength = 1f) =>
        mechanic == null || string.IsNullOrWhiteSpace(mechanic.mechanicId) ? "" :
        string.IsNullOrEmpty(mechanic.mechanicName) ? mechanic.mechanicId : mechanic.mechanicName;

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        object token = ctx.Mechanics.Push(mechanic);
        return token == null ? null : new ActionHandle(() => ctx.Mechanics.Pop(token));
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (mechanic == null || string.IsNullOrWhiteSpace(mechanic.mechanicId))
            errors.Add($"{owner}: the mechanic has no ID (e.g. 'double_jump', 'water_walking').");
        else if (string.IsNullOrEmpty(mechanic.mechanicName))
            warnings.Add($"{owner}: '{mechanic.mechanicId}' has no display name.");
    }
}

[Serializable, AbilityMenu("Visual/Attached Effect", "Shows a prefab (glow, aura, particles) on the wearer while active.", 0)]
public class AttachedVisualEffect : EquipmentEffect
{
    [Tooltip("Spawned on the wearer while active and removed afterwards.")]
    public GameObject prefab;
    [Tooltip("Name of a child transform (a bone such as 'Hand_R' or 'Spine') to attach to. Empty = the character's body.")]
    public string attachTo = "";
    [Tooltip("Local position offset from the attach point.")]
    public Vector3 localOffset = Vector3.zero;
    [Tooltip("Played once when it appears.")]
    public AudioClip sound;

    public AttachedVisualEffect() { }
    public AttachedVisualEffect(GameObject prefab, AudioClip sound = null)
    {
        this.prefab = prefab;
        this.sound = sound;
    }

    public override string Describe(float strength = 1f) => "";

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        if (prefab == null && sound == null)
            return null;
        ctx.PlayOneShot(sound);
        if (prefab == null)
            return null;
        Transform parent = ctx.Body;
        if (parent == null)
            return null;
        if (!string.IsNullOrEmpty(attachTo))
        {
            Transform bone = FindDeep(parent.root, attachTo);
            if (bone != null)
                parent = bone;
        }
        GameObject go = UnityEngine.Object.Instantiate(prefab, parent, false);
        go.transform.localPosition += localOffset;
        return new ActionHandle(() => { if (go != null) UnityEngine.Object.Destroy(go); });
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name)
            return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform r = FindDeep(t.GetChild(i), name);
            if (r != null)
                return r;
        }
        return null;
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (prefab == null && sound == null)
            warnings.Add($"{owner}: no prefab or sound.");
    }
}
