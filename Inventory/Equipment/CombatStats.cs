using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A combat stat a character, its equipment or its buffs can raise.</summary>
/// <remarks>Serialized as numbers: only add new stats at the end.</remarks>
public enum CombatStatType
{
    /// <summary>Points. Raises the damage of weapons that scale with Strength.</summary>
    Strength,
    /// <summary>Points. Raises attack speed and critical chance (and weapons that scale with Agility).</summary>
    Agility,
    /// <summary>Points. Raises ability damage (and weapons that scale with Intelligence).</summary>
    Intelligence,
    /// <summary>Points. Raises max health.</summary>
    Endurance,
    /// <summary>Points. Reduces Physical damage taken (diminishing returns).</summary>
    Defense,
    /// <summary>Points. Reduces Magical damage taken (diminishing returns).</summary>
    MagicResistance,
    /// <summary>Percentage points added to the critical hit chance of weapon attacks.</summary>
    CriticalChance,
    /// <summary>Percent added to the critical damage multiplier (+50 = crits deal 0.5x more).</summary>
    CriticalDamage,
    /// <summary>Percent faster weapon attacks.</summary>
    AttackSpeed,
    /// <summary>Percent faster ability casting (shorter cast times).</summary>
    CastingSpeed,
    /// <summary>Percent more weapon damage.</summary>
    WeaponDamage,
    /// <summary>Percent stamina spent by weapon attacks (negative = cheaper).</summary>
    AttackStaminaCost,
    /// <summary>Percent faster charging of charged (hold) attacks.</summary>
    ChargeSpeed,
    /// <summary>Percent longer knockback dealt by weapon hits.</summary>
    Knockback,
    /// <summary>Percent more damage from elemental weapon hits.</summary>
    ElementalDamage,
}

/// <summary>One change to a combat stat, e.g. "+12 Defense" or "+15% Weapon Damage while wielding a Sword".</summary>
[Serializable]
public class CombatStatModifier
{
    [Tooltip("Which stat changes. Hover the value for its units.")]
    public CombatStatType stat = CombatStatType.Defense;

    [Tooltip("Amount. Attributes, Defense and Magic Resistance are points; the others are percentages (+15 = 15% more).")]
    public float value = 10f;

    [Tooltip("Only counts while the character wields a weapon of this category (e.g. +20% Attack Speed with Daggers). None = always.")]
    public WeaponCategory onlyWithWeapon = WeaponCategory.None;

    public CombatStatModifier() { }

    public CombatStatModifier(CombatStatType stat, float value, WeaponCategory onlyWithWeapon = WeaponCategory.None)
    {
        this.stat = stat;
        this.value = value;
        this.onlyWithWeapon = onlyWithWeapon;
    }

    /// <summary>"+12 Defense", "+15% Weapon Damage (Sword)".</summary>
    public string Describe(float strength = 1f)
    {
        CombatStatInfo info = CombatStatInfo.Get(stat);
        float v = value * strength;
        string text = info.isPercent ? $"{v:+0.#;-0.#}% {info.name}" : $"{v:+0.#;-0.#} {info.name}";
        return onlyWithWeapon == WeaponCategory.None ? text : $"{text} ({onlyWithWeapon})";
    }
}

/// <summary>Less (or more) damage from one element.</summary>
[Serializable]
public class ElementalResistance
{
    public ElementType element = ElementType.Fire;

    [Tooltip("Percent less damage of this element. Negative = a weakness (takes more).")]
    [Range(-100f, 100f)] public float percent = 20f;

    public ElementalResistance() { }

    public ElementalResistance(ElementType element, float percent)
    {
        this.element = element;
        this.percent = percent;
    }

    public string Describe(float strength = 1f) => $"{percent * strength:+0.#;-0.#}% {element} Resistance";
}

/// <summary>Display name, unit and meaning of a <see cref="CombatStatType"/> (inspectors and tooltips).</summary>
public readonly struct CombatStatInfo
{
    public readonly string name;
    public readonly bool isPercent;
    public readonly string description;

    private CombatStatInfo(string name, bool isPercent, string description)
    {
        this.name = name;
        this.isPercent = isPercent;
        this.description = description;
    }

    public static CombatStatInfo Get(CombatStatType stat)
    {
        switch (stat)
        {
            case CombatStatType.Strength: return new CombatStatInfo("Strength", false, "Points. Weapons that scale with Strength deal more damage.");
            case CombatStatType.Agility: return new CombatStatInfo("Agility", false, "Points. Faster attacks, higher critical chance, and more damage for Agility weapons.");
            case CombatStatType.Intelligence: return new CombatStatInfo("Intelligence", false, "Points. More ability damage, and more damage for Intelligence weapons.");
            case CombatStatType.Endurance: return new CombatStatInfo("Endurance", false, "Points. More max health.");
            case CombatStatType.Defense: return new CombatStatInfo("Defense", false, "Points. Less Physical damage taken (Defense / (Defense + Defense Half Value)).");
            case CombatStatType.MagicResistance: return new CombatStatInfo("Magic Resistance", false, "Points. Less Magical damage taken (same curve as Defense).");
            case CombatStatType.CriticalChance: return new CombatStatInfo("Critical Chance", true, "Percentage points added to the critical chance of weapon attacks.");
            case CombatStatType.CriticalDamage: return new CombatStatInfo("Critical Damage", true, "Percent added to the critical multiplier (+50 = crits deal 0.5x more).");
            case CombatStatType.AttackSpeed: return new CombatStatInfo("Attack Speed", true, "Percent faster weapon attacks (animation and timing).");
            case CombatStatType.CastingSpeed: return new CombatStatInfo("Casting Speed", true, "Percent faster ability casting (shorter cast times).");
            case CombatStatType.WeaponDamage: return new CombatStatInfo("Weapon Damage", true, "Percent more weapon damage.");
            case CombatStatType.AttackStaminaCost: return new CombatStatInfo("Attack Stamina Cost", true, "Percent stamina spent by weapon attacks. Negative = cheaper.");
            case CombatStatType.ChargeSpeed: return new CombatStatInfo("Charge Speed", true, "Percent faster charging of hold attacks.");
            case CombatStatType.Knockback: return new CombatStatInfo("Knockback", true, "Percent longer knockback dealt by weapon hits.");
            case CombatStatType.ElementalDamage: return new CombatStatInfo("Elemental Damage", true, "Percent more damage from elemental weapon hits.");
            default: return new CombatStatInfo(stat.ToString(), false, "");
        }
    }
}

/// <summary>
/// A character's combat stats: attributes, defense, resistances, critical and attack-speed bonuses. Equipment and
/// buffs add modifiers with <see cref="AddModifiers"/> and remove them exactly with <see cref="RemoveModifiers"/>
/// (nothing is ever added and subtracted by hand, so values never drift). Damage taken is reduced automatically:
/// the component registers itself on the character's <see cref="CombatEntity"/>.
/// <para>
/// Attributes also feed other systems: Endurance raises max health and Intelligence ability damage (through the
/// <see cref="TraitManager"/> when there is one, so percent bonuses stay correct), Casting Speed shortens ability
/// cast times. Added automatically to the player by the <see cref="EquipmentManager"/>; add it to mobs to give them
/// defense or resistances.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-40)]
public class CombatStats : MonoBehaviour, IDamageTakenModifier
{
    [Header("Innate Values (before equipment and buffs)")]
    [Tooltip("The character's own stats, e.g. +20 Defense for an armored mob.")]
    [SerializeField] private List<CombatStatModifier> baseStats = new List<CombatStatModifier>();
    [Tooltip("The character's own elemental resistances and weaknesses.")]
    [SerializeField] private List<ElementalResistance> baseResistances = new List<ElementalResistance>();

    [Header("Damage Reduction")]
    [Tooltip("Defense (or Magic Resistance) that halves the damage: reduction = value / (value + this). 100 = 100 Defense takes 50% less.")]
    [SerializeField, Min(1f)] private float defenseHalfValue = 100f;
    [Tooltip("Most damage Defense or Magic Resistance can remove (0.8 = at most 80% less).")]
    [SerializeField, Range(0f, 0.95f)] private float maxDamageReduction = 0.8f;
    [Tooltip("Highest elemental resistance in percent (weaknesses can go down to -100).")]
    [SerializeField, Range(0f, 100f)] private float maxElementalResistance = 80f;

    [Header("Attribute Effects (per point)")]
    [Tooltip("Percent weapon damage per point of the weapon's scaling attribute (Strength, Agility or Intelligence).")]
    [SerializeField, Min(0f)] private float weaponDamagePercentPerAttributePoint = 1f;
    [Tooltip("Percent attack speed per point of Agility.")]
    [SerializeField, Min(0f)] private float attackSpeedPercentPerAgility = 0.5f;
    [Tooltip("Critical chance (percentage points) per point of Agility.")]
    [SerializeField, Min(0f)] private float critChancePerAgility = 0.2f;
    [Tooltip("Percent ability damage per point of Intelligence.")]
    [SerializeField, Min(0f)] private float abilityDamagePercentPerIntelligence = 1f;
    [Tooltip("Max health per point of Endurance.")]
    [SerializeField, Min(0f)] private float maxHealthPerEndurance = 5f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private static readonly int StatCount = Enum.GetValues(typeof(CombatStatType)).Length;
    private static readonly int ElementCount = Enum.GetValues(typeof(ElementType)).Length;

    private sealed class Entry
    {
        public string label;
        public CombatStatModifier[] mods;
        public ElementalResistance[] resists;
        public float strength;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly Dictionary<WeaponCategory, float[]> totalsByWeapon = new Dictionary<WeaponCategory, float[]>();
    private float[] resistTotals;
    private Entry baseEntry;

    private CombatEntity entity;
    private TraitManager traits;
    private HealthManager health;
    private TraitManager.AppliedModifiers derivedHandle;
    private float appliedDerivedHealth;
    private float derivedEndurance = float.NaN, derivedIntelligence = float.NaN, derivedCasting = float.NaN;
    private bool derivedDirty = true;

    /// <summary>Raised after any modifier was added or removed.</summary>
    public event Action Changed;

    /// <summary>
    /// Category of the weapon the character wields right now. Modifiers limited to a weapon category only count
    /// while it matches. Set by the weapon controller (<see cref="SetWielded"/>).
    /// </summary>
    public WeaponCategory WieldedCategory => wieldedCategory;
    private WeaponCategory wieldedCategory = WeaponCategory.None;

    /// <summary>The weapon the character wields right now (null = unarmed or a non-weapon item).</summary>
    public WeaponSO WieldedWeapon { get; private set; }

    /// <summary>Tells the stats which weapon is in hand (null = none). Weapon-limited modifiers follow it.</summary>
    public void SetWielded(WeaponSO weapon, WeaponCategory category)
    {
        if (weapon == null)
            category = WeaponCategory.None;
        if (WieldedWeapon == weapon && wieldedCategory == category)
            return;
        WieldedWeapon = weapon;
        wieldedCategory = category;
        derivedDirty = true;
        Changed?.Invoke();
    }

    public float DefenseHalfValue => defenseHalfValue;
    public float MaxDamageReduction => maxDamageReduction;

    /// <summary>The CombatStats of a character (added to characters with a status controller when missing).</summary>
    public static CombatStats For(Component anyPart, bool addIfMissing = true)
    {
        if (anyPart == null)
            return null;
        CombatStats s = anyPart.GetComponentInParent<CombatStats>();
        if (s != null || !addIfMissing)
            return s;
        BaseStatusController status = anyPart.GetComponentInParent<BaseStatusController>();
        GameObject host = status != null ? status.gameObject : anyPart.gameObject;
        s = host.GetComponent<CombatStats>();
        return s != null ? s : host.AddComponent<CombatStats>();
    }

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        RebuildBaseEntry();
        CacheSystems();
    }

    private void OnEnable()
    {
        ResolveEntity();
        derivedDirty = true;
    }

    private void OnDisable()
    {
        if (entity != null)
            entity.RemoveDamageTakenModifier(this);
        entity = null;
    }

    private void OnDestroy()
    {
        RevertDerived();
    }

    private void Update()
    {
        if (entity == null)
            ResolveEntity();
        if (derivedDirty)
            ApplyDerived();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;
        RebuildBaseEntry();
        Invalidate();
    }

    private void CacheSystems()
    {
        traits = GetComponentInParent<TraitManager>();
        if (traits == null)
            traits = GetComponentInChildren<TraitManager>();
        BaseStatusController status = GetComponentInParent<BaseStatusController>();
        if (status is PlayerStatusController player)
            health = player.HpManager;
        else if (status is MobStatusController mob)
            health = mob.HealthManager;
        if (health == null)
            health = GetComponentInChildren<HealthManager>();
    }

    private void ResolveEntity()
    {
        if (entity != null)
            return;
        entity = GetComponent<CombatEntity>();
        if (entity == null)
            entity = CombatEntity.Resolve(gameObject);
        if (entity == null && GetComponent<BaseStatusController>() != null)
            entity = CombatEntity.GetOrAdd(gameObject);
        if (entity != null)
            entity.AddDamageTakenModifier(this);
    }

    private void RebuildBaseEntry()
    {
        if (baseEntry != null)
            entries.Remove(baseEntry);
        baseEntry = new Entry
        {
            label = "Innate",
            mods = baseStats != null ? baseStats.ToArray() : Array.Empty<CombatStatModifier>(),
            resists = baseResistances != null ? baseResistances.ToArray() : Array.Empty<ElementalResistance>(),
            strength = 1f,
        };
        entries.Insert(0, baseEntry);
    }

    // ------------------------------------------------------------------ modifiers
    /// <summary>
    /// Adds modifiers and returns a token; pass it to <see cref="RemoveModifiers"/> to remove exactly these again.
    /// The lists are copied, so later edits to them do not change what is applied.
    /// </summary>
    public object AddModifiers(string label, IList<CombatStatModifier> modifiers, IList<ElementalResistance> resistances = null, float strength = 1f)
    {
        var e = new Entry
        {
            label = string.IsNullOrEmpty(label) ? "?" : label,
            mods = Copy(modifiers),
            resists = Copy(resistances),
            strength = strength,
        };
        entries.Add(e);
        Invalidate();
        if (debugLog)
            Debug.Log($"[CombatStats] {name}: +{e.label} ({e.mods.Length} stats, {e.resists.Length} resistances)", this);
        return e;
    }

    /// <summary>Removes modifiers added with <see cref="AddModifiers"/>. Unknown tokens are ignored.</summary>
    public void RemoveModifiers(object token)
    {
        if (token is Entry e && e != baseEntry && entries.Remove(e))
        {
            Invalidate();
            if (debugLog)
                Debug.Log($"[CombatStats] {name}: -{e.label}", this);
        }
    }

    private static T[] Copy<T>(IList<T> list) where T : class
    {
        if (list == null || list.Count == 0)
            return Array.Empty<T>();
        var copy = new List<T>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
                copy.Add(list[i]);
        }
        return copy.ToArray();
    }

    private void Invalidate()
    {
        totalsByWeapon.Clear();
        resistTotals = null;
        derivedDirty = true;
        Changed?.Invoke();
    }

    // ------------------------------------------------------------------ queries
    /// <summary>The total of a stat, counting modifiers limited to the wielded weapon's category.</summary>
    public float Get(CombatStatType stat) => Get(stat, wieldedCategory);

    /// <summary>The total of a stat as if a weapon of <paramref name="weapon"/> category were wielded.</summary>
    public float Get(CombatStatType stat, WeaponCategory weapon)
    {
        if (!totalsByWeapon.TryGetValue(weapon, out float[] totals))
        {
            totals = new float[StatCount];
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                for (int m = 0; m < e.mods.Length; m++)
                {
                    CombatStatModifier mod = e.mods[m];
                    if (mod.onlyWithWeapon != WeaponCategory.None && mod.onlyWithWeapon != weapon)
                        continue;
                    int index = (int)mod.stat;
                    if (index >= 0 && index < totals.Length)
                        totals[index] += mod.value * e.strength;
                }
            }
            totalsByWeapon[weapon] = totals;
        }
        int s = (int)stat;
        return s >= 0 && s < totals.Length ? totals[s] : 0f;
    }

    /// <summary>Percent resistance to an element (clamped: weaknesses down to -100, resistances up to the maximum).</summary>
    public float GetResistance(ElementType element)
    {
        if (element == ElementType.None)
            return 0f;
        if (resistTotals == null)
        {
            resistTotals = new float[ElementCount];
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                for (int r = 0; r < e.resists.Length; r++)
                {
                    int index = (int)e.resists[r].element;
                    if (index > 0 && index < resistTotals.Length)
                        resistTotals[index] += e.resists[r].percent * e.strength;
                }
            }
        }
        int el = (int)element;
        float v = el >= 0 && el < resistTotals.Length ? resistTotals[el] : 0f;
        return Mathf.Clamp(v, -100f, maxElementalResistance);
    }

    /// <summary>Fraction of Physical damage removed by Defense (negative Defense makes it negative: more damage).</summary>
    public float PhysicalReduction => Reduction(Get(CombatStatType.Defense));

    /// <summary>Fraction of Magical damage removed by Magic Resistance.</summary>
    public float MagicalReduction => Reduction(Get(CombatStatType.MagicResistance));

    /// <summary>The damage-reduction curve: value / (value + half value), capped; negative values add damage.</summary>
    public float Reduction(float value)
    {
        float r = value >= 0f ? value / (value + defenseHalfValue) : value / defenseHalfValue;
        return Mathf.Clamp(r, -1f, maxDamageReduction);
    }

    /// <summary>Attribute points of a weapon's scaling attribute.</summary>
    public float ScalingPoints(WeaponScaling scaling, WeaponCategory weapon)
    {
        switch (scaling)
        {
            case WeaponScaling.Strength: return Get(CombatStatType.Strength, weapon);
            case WeaponScaling.Agility: return Get(CombatStatType.Agility, weapon);
            case WeaponScaling.Intelligence: return Get(CombatStatType.Intelligence, weapon);
            default: return 0f;
        }
    }

    /// <summary>Weapon damage multiplier from Weapon Damage, the scaling attribute and (for elemental hits) Elemental Damage.</summary>
    public float WeaponDamageMultiplier(WeaponCategory weapon, WeaponScaling scaling, bool elemental)
    {
        float m = 1f + Get(CombatStatType.WeaponDamage, weapon) / 100f;
        m *= 1f + ScalingPoints(scaling, weapon) * weaponDamagePercentPerAttributePoint / 100f;
        if (elemental)
            m *= 1f + Get(CombatStatType.ElementalDamage, weapon) / 100f;
        return Mathf.Max(0f, m);
    }

    /// <summary>Extra critical chance (0-1) for weapon attacks.</summary>
    public float CritChanceBonus(WeaponCategory weapon) =>
        (Get(CombatStatType.CriticalChance, weapon) + Get(CombatStatType.Agility, weapon) * critChancePerAgility) / 100f;

    /// <summary>Extra critical multiplier (0.5 = crits deal 0.5x more).</summary>
    public float CritDamageBonus(WeaponCategory weapon) => Get(CombatStatType.CriticalDamage, weapon) / 100f;

    /// <summary>Weapon attack speed multiplier (1.2 = 20% faster).</summary>
    public float AttackSpeedMultiplier(WeaponCategory weapon) =>
        Mathf.Max(0.1f, 1f + (Get(CombatStatType.AttackSpeed, weapon) + Get(CombatStatType.Agility, weapon) * attackSpeedPercentPerAgility) / 100f);

    public float AttackStaminaMultiplier(WeaponCategory weapon) => Mathf.Max(0f, 1f + Get(CombatStatType.AttackStaminaCost, weapon) / 100f);
    public float ChargeSpeedMultiplier(WeaponCategory weapon) => Mathf.Max(0.1f, 1f + Get(CombatStatType.ChargeSpeed, weapon) / 100f);
    public float KnockbackMultiplier(WeaponCategory weapon) => Mathf.Max(0f, 1f + Get(CombatStatType.Knockback, weapon) / 100f);

    /// <summary>Every active modifier as text (inspector, debug UI).</summary>
    public List<string> Describe()
    {
        var lines = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            for (int m = 0; m < e.mods.Length; m++)
                lines.Add($"{e.mods[m].Describe(e.strength)}  [{e.label}]");
            for (int r = 0; r < e.resists.Length; r++)
                lines.Add($"{e.resists[r].Describe(e.strength)}  [{e.label}]");
        }
        return lines;
    }

    // ------------------------------------------------------------------ damage taken
    float IDamageTakenModifier.ModifyDamageTaken(in DamageInfo info, float amount)
    {
        if (info.type == DamageType.True)
            return amount;
        if (info.type == DamageType.Physical)
            amount *= 1f - PhysicalReduction;
        else if (info.type == DamageType.Magical)
            amount *= 1f - MagicalReduction;
        if (info.element != ElementType.None)
            amount *= 1f - GetResistance(info.element) / 100f;
        return amount;
    }

    // ------------------------------------------------------------------ attribute side effects
    /// <summary>
    /// Endurance, Intelligence and Casting Speed change stats owned by other systems. They are written as ONE set of
    /// trait modifiers (reverted exactly and re-applied whenever they change), or straight into the health manager for
    /// characters without a trait manager.
    /// </summary>
    private void ApplyDerived()
    {
        derivedDirty = false;
        float end = Get(CombatStatType.Endurance);
        float intel = Get(CombatStatType.Intelligence);
        float cast = Get(CombatStatType.CastingSpeed);
        if (Mathf.Approximately(end, derivedEndurance) && Mathf.Approximately(intel, derivedIntelligence) && Mathf.Approximately(cast, derivedCasting))
            return;
        derivedEndurance = end;
        derivedIntelligence = intel;
        derivedCasting = cast;

        if (traits != null)
        {
            if (derivedHandle != null)
                traits.RevertModifiers(derivedHandle);
            derivedHandle = null;
            var list = new List<TraitModifier>(3);
            if (!Mathf.Approximately(end * maxHealthPerEndurance, 0f))
                list.Add(TraitModifier.Flat(TraitStat.MaxHealth, end * maxHealthPerEndurance));
            if (!Mathf.Approximately(intel * abilityDamagePercentPerIntelligence, 0f))
                list.Add(TraitModifier.Percent(TraitStat.AbilityDamage, intel * abilityDamagePercentPerIntelligence));
            if (!Mathf.Approximately(cast, 0f))
            {
                // +X% casting speed = cast time divided by (1 + X/100).
                float castTimePercent = (1f / Mathf.Max(0.1f, 1f + cast / 100f) - 1f) * 100f;
                list.Add(TraitModifier.Percent(TraitStat.AbilityCastTime, castTimePercent));
            }
            if (list.Count > 0)
                derivedHandle = traits.ApplyModifiers(list, 1f, "Combat Stats");
        }
        else if (health != null)
        {
            float wanted = end * maxHealthPerEndurance;
            float delta = wanted - appliedDerivedHealth;
            if (!Mathf.Approximately(delta, 0f))
            {
                health.ModifyMaxValue(delta);
                appliedDerivedHealth = wanted;
            }
        }
    }

    private void RevertDerived()
    {
        if (traits != null && derivedHandle != null)
            traits.RevertModifiers(derivedHandle);
        derivedHandle = null;
        if (health != null && !Mathf.Approximately(appliedDerivedHealth, 0f))
            health.ModifyMaxValue(-appliedDerivedHealth);
        appliedDerivedHealth = 0f;
    }
}
