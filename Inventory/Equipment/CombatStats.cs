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
    /// <summary>Percent more damage of an element (all hits of that element: weapons, abilities, damage over time). Can be limited to one element.</summary>
    ElementalDamage,

    // ---- Added later (append only) ----
    /// <summary>Percent taller (negative = shorter): scales the character's body (see CharacterBody).</summary>
    Height,
    /// <summary>Percent more threat the character generates on enemies (negative = less).</summary>
    ThreatGenerated,
    /// <summary>Percent longer crowd control (stun, root, silence, slow, taunt) the character applies.</summary>
    CrowdControlDuration,
    /// <summary>Percent shorter crowd control on the character (capped).</summary>
    CrowdControlResistance,
    /// <summary>Percent longer buffs the character applies (stat buffs, heals over time).</summary>
    BuffDuration,
    /// <summary>Percent stronger buffs the character applies (their stat values).</summary>
    BuffStrength,
    /// <summary>Percent longer debuffs and damage over time the character applies. Can be limited to one element (Poison...).</summary>
    DebuffDuration,
    /// <summary>Percent stronger debuffs (their stat values) and damage-over-time ticks the character applies. Can be limited to one element.</summary>
    DebuffStrength,
    /// <summary>Percentage points added to the chance of the damage over time and debuffs the character applies. Can be limited to one element.</summary>
    StatusChance,
    /// <summary>Percent shorter damage over time and debuffs on the character (capped). Can be limited to one element.</summary>
    StatusResistance,
    /// <summary>Percent less physical damage taken, after armour (Defense). Not reduced by penetration.</summary>
    PhysicalDefense,
    /// <summary>Percent less magical damage taken, after Magic Resistance. Not reduced by penetration.</summary>
    MagicDefense,
    /// <summary>Percent of the target's armour (Defense) the character's physical hits ignore.</summary>
    ArmorPenetration,
    /// <summary>Points of the target's armour (Defense) the character's physical hits ignore (after Armor Penetration).</summary>
    PhysicalPenetration,
    /// <summary>Percent of the target's Magic Resistance the character's magical hits ignore.</summary>
    MagicPenetration,
    /// <summary>Percent faster drawing of bows and winding up of throws.</summary>
    DrawSpeed,
    /// <summary>Percent faster reloading of crossbows and firearms.</summary>
    ReloadSpeed,
    /// <summary>Percent less mana spent by abilities (capped). Can be limited to skills, innate (trait / race / class) or item abilities.</summary>
    ManaCostReduction,
    /// <summary>Percent shorter cooldowns (capped). Can be limited to skills, innate or item abilities (items also shortens consumable cooldowns).</summary>
    CooldownReduction,
}

/// <summary>Which abilities a cost / cooldown modifier counts for.</summary>
public enum StatScope
{
    /// <summary>All abilities and items.</summary>
    Everything,
    /// <summary>The character's own abilities (ability keys; a mob's abilities).</summary>
    Skills,
    /// <summary>Abilities granted by traits, the race or the class.</summary>
    Innate,
    /// <summary>Abilities granted by items, and consumable cooldowns.</summary>
    Items,
}

/// <summary>
/// One attribute giving another stat, e.g. "+0.2% Critical Chance per Agility point" (races and classes scale
/// differently). Sources are counted without other rules (no chains).
/// </summary>
[Serializable]
public class StatScalingRule
{
    [Tooltip("The stat whose points give the bonus (usually Strength, Agility, Intelligence or Endurance).")]
    public CombatStatType from = CombatStatType.Agility;
    [Tooltip("The stat that receives the bonus.")]
    public CombatStatType to = CombatStatType.CriticalChance;
    [Tooltip("Amount of To per point of From (in To's units: points or percent).")]
    public float perPoint = 0.1f;

    public StatScalingRule() { }

    public StatScalingRule(CombatStatType from, CombatStatType to, float perPoint)
    {
        this.from = from;
        this.to = to;
        this.perPoint = perPoint;
    }

    public string Describe(float strength = 1f)
    {
        CombatStatInfo t = CombatStatInfo.Get(to);
        float v = perPoint * strength;
        return $"{v:+0.###;-0.###}{(t.isPercent ? "%" : "")} {t.name} per {CombatStatInfo.Get(from).name}";
    }
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

    [Tooltip("Element-aware stats only (Elemental Damage, Debuff Duration / Strength, Status Chance / Resistance): counts only for " +
             "this element - e.g. Status Resistance limited to Poison = poison resistance. None = every element.")]
    public ElementType onlyElement = ElementType.None;

    [Tooltip("Cost / cooldown stats only: which abilities it counts for (skills, innate abilities of traits / race / class, item abilities).")]
    public StatScope scope = StatScope.Everything;

    public CombatStatModifier() { }

    public CombatStatModifier(CombatStatType stat, float value, WeaponCategory onlyWithWeapon = WeaponCategory.None)
    {
        this.stat = stat;
        this.value = value;
        this.onlyWithWeapon = onlyWithWeapon;
    }

    /// <summary>A modifier limited to one element (poison resistance = Status Resistance + Poison).</summary>
    public static CombatStatModifier ForElement(CombatStatType stat, float value, ElementType element) =>
        new CombatStatModifier(stat, value) { onlyElement = element };

    /// <summary>A modifier limited to one kind of ability (cooldown reduction for item abilities only).</summary>
    public static CombatStatModifier ForScope(CombatStatType stat, float value, StatScope scope) =>
        new CombatStatModifier(stat, value) { scope = scope };

    /// <summary>Does it count only in some situations (an element, a kind of ability)?</summary>
    public bool IsFiltered =>
        (onlyElement != ElementType.None && CombatStatInfo.UsesElement(stat)) || (scope != StatScope.Everything && CombatStatInfo.UsesScope(stat));

    /// <summary>"+12 Defense", "+15% Weapon Damage (Sword)", "+20% Status Resistance (Poison)".</summary>
    public string Describe(float strength = 1f)
    {
        CombatStatInfo info = CombatStatInfo.Get(stat);
        float v = value * strength;
        string text = info.isPercent ? $"{v:+0.#;-0.#}% {info.name}" : $"{v:+0.#;-0.#} {info.name}";
        var tags = new List<string>(2);
        if (onlyWithWeapon != WeaponCategory.None) tags.Add(onlyWithWeapon.ToString());
        if (onlyElement != ElementType.None && CombatStatInfo.UsesElement(stat)) tags.Add(onlyElement.ToString());
        if (scope != StatScope.Everything && CombatStatInfo.UsesScope(stat)) tags.Add(scope + " abilities");
        return tags.Count == 0 ? text : $"{text} ({string.Join(", ", tags)})";
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

    /// <summary>Stats where a negative value helps (cheaper attacks): buff / debuff detection and tooltip colours.</summary>
    public static bool LowerIsBetter(CombatStatType stat) => stat == CombatStatType.AttackStaminaCost;

    public static CombatStatInfo Get(CombatStatType stat)
    {
        switch (stat)
        {
            case CombatStatType.Strength: return new CombatStatInfo("Strength", false, "Points. Weapons that scale with Strength deal more damage.");
            case CombatStatType.Agility: return new CombatStatInfo("Agility", false, "Points. Faster attacks, higher critical chance, and more damage for Agility weapons.");
            case CombatStatType.Intelligence: return new CombatStatInfo("Intelligence", false, "Points. More ability damage, and more damage for Intelligence weapons.");
            case CombatStatType.Endurance: return new CombatStatInfo("Endurance", false, "Points. More max health.");
            case CombatStatType.Defense: return new CombatStatInfo("Defense (Armor)", false, "Armour points. Less Physical damage taken (Defense / (Defense + Defense Half Value)); reduced by the attacker's Armor and Physical Penetration.");
            case CombatStatType.MagicResistance: return new CombatStatInfo("Magic Resistance", false, "Points. Less Magical damage taken (same curve as Defense).");
            case CombatStatType.CriticalChance: return new CombatStatInfo("Critical Chance", true, "Percentage points added to the critical chance of weapon attacks.");
            case CombatStatType.CriticalDamage: return new CombatStatInfo("Critical Damage", true, "Percent added to the critical multiplier (+50 = crits deal 0.5x more).");
            case CombatStatType.AttackSpeed: return new CombatStatInfo("Attack Speed", true, "Percent faster weapon attacks (animation and timing).");
            case CombatStatType.CastingSpeed: return new CombatStatInfo("Casting Speed", true, "Percent faster ability casting (shorter cast times).");
            case CombatStatType.WeaponDamage: return new CombatStatInfo("Weapon Damage", true, "Percent more weapon damage.");
            case CombatStatType.AttackStaminaCost: return new CombatStatInfo("Attack Stamina Cost", true, "Percent stamina spent by weapon attacks. Negative = cheaper.");
            case CombatStatType.ChargeSpeed: return new CombatStatInfo("Charge Speed", true, "Percent faster charging of hold attacks.");
            case CombatStatType.Knockback: return new CombatStatInfo("Knockback", true, "Percent longer knockback dealt by weapon hits.");
            case CombatStatType.ElementalDamage: return new CombatStatInfo("Elemental Damage", true, "Percent more damage of elemental hits (weapons, abilities, damage over time). Limit it to one element for e.g. Poison Damage.");
            case CombatStatType.Height: return new CombatStatInfo("Height", true, "Percent taller (negative = shorter). Scales the body (Character Body), its reach and hitboxes.");
            case CombatStatType.ThreatGenerated: return new CombatStatInfo("Threat", true, "Percent more threat generated on enemies by damage, healing and taunts (negative = stealthier).");
            case CombatStatType.CrowdControlDuration: return new CombatStatInfo("Crowd Control Duration", true, "Percent longer stuns, roots, silences, slows and taunts applied.");
            case CombatStatType.CrowdControlResistance: return new CombatStatInfo("Crowd Control Resistance", true, "Percent shorter crowd control on this character (capped by Max Control Resistance).");
            case CombatStatType.BuffDuration: return new CombatStatInfo("Buff Duration", true, "Percent longer buffs and heals over time applied.");
            case CombatStatType.BuffStrength: return new CombatStatInfo("Buff Strength", true, "Percent stronger buffs applied (their stat values).");
            case CombatStatType.DebuffDuration: return new CombatStatInfo("Debuff Duration", true, "Percent longer debuffs and damage over time applied. Limit it to an element (Poison) for that element only.");
            case CombatStatType.DebuffStrength: return new CombatStatInfo("Debuff Strength", true, "Percent stronger debuffs and damage-over-time ticks applied (poison strength = limited to Poison).");
            case CombatStatType.StatusChance: return new CombatStatInfo("Status Chance", true, "Percentage points added to the chance of damage over time and debuffs applied (poison chance = limited to Poison).");
            case CombatStatType.StatusResistance: return new CombatStatInfo("Status Resistance", true, "Percent shorter damage over time and debuffs on this character (poison resistance = limited to Poison; capped). Poison DAMAGE is reduced by the Poison elemental resistance.");
            case CombatStatType.PhysicalDefense: return new CombatStatInfo("Physical Defense", true, "Percent less physical damage taken, after armour. Penetration does not reduce it (capped).");
            case CombatStatType.MagicDefense: return new CombatStatInfo("Magic Defense", true, "Percent less magical damage taken, after Magic Resistance. Penetration does not reduce it (capped).");
            case CombatStatType.ArmorPenetration: return new CombatStatInfo("Armor Penetration", true, "Percent of the target's armour (Defense) ignored by physical hits.");
            case CombatStatType.PhysicalPenetration: return new CombatStatInfo("Physical Penetration", false, "Armour points ignored by physical hits (after Armor Penetration).");
            case CombatStatType.MagicPenetration: return new CombatStatInfo("Magic Penetration", true, "Percent of the target's Magic Resistance ignored by magical hits.");
            case CombatStatType.DrawSpeed: return new CombatStatInfo("Draw Speed", true, "Percent faster drawing of bows and winding up of throws.");
            case CombatStatType.ReloadSpeed: return new CombatStatInfo("Reload Speed", true, "Percent faster reloading of crossbows and firearms.");
            case CombatStatType.ManaCostReduction: return new CombatStatInfo("Mana Cost Reduction", true, "Percent less mana spent by abilities (capped). Can be limited to skills, innate or item abilities.");
            case CombatStatType.CooldownReduction: return new CombatStatInfo("Cooldown Reduction", true, "Percent shorter ability cooldowns (capped). Can be limited to skills, innate or item abilities; Items also shortens consumable cooldowns.");
            default: return new CombatStatInfo(stat.ToString(), false, "");
        }
    }

    /// <summary>Can a modifier of this stat be limited to one element?</summary>
    public static bool UsesElement(CombatStatType stat) =>
        stat == CombatStatType.ElementalDamage || stat == CombatStatType.DebuffDuration || stat == CombatStatType.DebuffStrength ||
        stat == CombatStatType.StatusChance || stat == CombatStatType.StatusResistance;

    /// <summary>Can a modifier of this stat be limited to some abilities?</summary>
    public static bool UsesScope(CombatStatType stat) => stat == CombatStatType.ManaCostReduction || stat == CombatStatType.CooldownReduction;
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
public class CombatStats : MonoBehaviour, IDamageTakenModifier, IDamageDealtModifier
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
    [Tooltip("Extra scaling of this character: attribute points giving other stats (\"+0.1% Critical Chance per Agility\"). " +
             "Races and classes add their own.")]
    [SerializeField] private List<StatScalingRule> scalingRules = new List<StatScalingRule>();

    [Header("Caps")]
    [Tooltip("Most Crowd Control Resistance counts (percent).")]
    [SerializeField, Range(0f, 95f)] private float maxControlResistance = 80f;
    [Tooltip("Most Status Resistance counts (percent).")]
    [SerializeField, Range(0f, 95f)] private float maxStatusResistance = 80f;
    [Tooltip("Most Cooldown Reduction counts (percent).")]
    [SerializeField, Range(0f, 95f)] private float maxCooldownReduction = 60f;
    [Tooltip("Most Mana Cost Reduction counts (percent).")]
    [SerializeField, Range(0f, 100f)] private float maxManaCostReduction = 80f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private static readonly int StatCount = Enum.GetValues(typeof(CombatStatType)).Length;
    private static readonly int ElementCount = Enum.GetValues(typeof(ElementType)).Length;

    private sealed class Entry
    {
        public string label;
        public CombatStatModifier[] mods;
        public ElementalResistance[] resists;
        public StatScalingRule[] rules = Array.Empty<StatScalingRule>();
        public float strength;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly Dictionary<WeaponCategory, float[]> totalsByWeapon = new Dictionary<WeaponCategory, float[]>();
    private float[] resistTotals;
    private Entry baseEntry;

    private CombatEntity entity;
    private ILocationalDefense locational;
    private TraitManager traits;
    private HealthManager health;
    private TraitManager.AppliedModifiers derivedHandle;
    private float appliedDerivedHealth;
    private float derivedEndurance = float.NaN;
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

    /// <summary>
    /// Makes Defense depend on where hits land (the <see cref="BodyPartController"/> registers itself). Null = every
    /// hit uses the full Defense, as before.
    /// </summary>
    public void SetLocationalDefense(ILocationalDefense provider) => locational = provider;

    /// <summary>The Defense / Magic Resistance points that protect against this hit (location-aware when body parts are on).</summary>
    public float DefenseAgainst(in DamageInfo info, CombatStatType stat)
    {
        float total = Get(stat);
        if (locational == null || info.bodyPart == null || (locational is UnityEngine.Object o && o == null))
            return total;
        return locational.DefenseAgainst(info, stat, total);
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
        {
            entity.RemoveDamageTakenModifier(this);
            entity.RemoveDamageDealtModifier(this);
            if (entity.Stats == this)
                entity.Stats = null;
            if (entity.Caster != null)
            {
                entity.Caster.SetModifierLayer(AbilityLayerKey, null);
                foreach (AbilitySlotSource src in AllSources)
                    entity.Caster.SetScopedModifiers(src, null);
            }
        }
        entity = null;
        pushedCaster = null;
    }

    private void OnDestroy()
    {
        RevertDerived();
    }

    private void Update()
    {
        if (entity == null)
            ResolveEntity();
        // The caster may appear after these stats (mobs build theirs at start).
        if (entity != null && entity.Caster != null && entity.Caster != pushedCaster)
            derivedDirty = true;
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
        {
            entity.AddDamageTakenModifier(this);
            entity.AddDamageDealtModifier(this);
            entity.Stats = this;
            derivedDirty = true;
        }
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
            rules = scalingRules != null ? scalingRules.ToArray() : Array.Empty<StatScalingRule>(),
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

    /// <summary>
    /// Adds scaling rules (attribute points giving other stats, e.g. a race's "+0.2% crit per Agility"); remove them with
    /// <see cref="RemoveModifiers"/> and the returned token.
    /// </summary>
    public object AddScaling(string label, IList<StatScalingRule> rules, float strength = 1f)
    {
        var e = new Entry
        {
            label = string.IsNullOrEmpty(label) ? "?" : label,
            mods = Array.Empty<CombatStatModifier>(),
            resists = Array.Empty<ElementalResistance>(),
            rules = Copy(rules),
            strength = strength,
        };
        entries.Add(e);
        Invalidate();
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
    /// <remarks>
    /// Stacking: every modifier of a stat ADDS (+10 and +15 = +25), whatever gives it (race, class, traits, items, set
    /// bonuses, buffs). Scaling rules then add attribute-based bonuses (from the totals before rules: no chains). Modifiers
    /// limited to an element or a kind of ability only count in <see cref="GetFor"/>.
    /// </remarks>
    public float Get(CombatStatType stat, WeaponCategory weapon)
    {
        if (!totalsByWeapon.TryGetValue(weapon, out float[] totals))
        {
            var raw = new float[StatCount];
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                for (int m = 0; m < e.mods.Length; m++)
                {
                    CombatStatModifier mod = e.mods[m];
                    if (mod.onlyWithWeapon != WeaponCategory.None && mod.onlyWithWeapon != weapon)
                        continue;
                    if (mod.IsFiltered)
                        continue;
                    int index = (int)mod.stat;
                    if (index >= 0 && index < raw.Length)
                        raw[index] += mod.value * e.strength;
                }
            }
            totals = (float[])raw.Clone();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                for (int r = 0; r < e.rules.Length; r++)
                {
                    StatScalingRule rule = e.rules[r];
                    int from = (int)rule.from, to = (int)rule.to;
                    if (from >= 0 && from < raw.Length && to >= 0 && to < totals.Length)
                        totals[to] += raw[from] * rule.perPoint * e.strength;
                }
            }
            totalsByWeapon[weapon] = totals;
        }
        int s = (int)stat;
        return s >= 0 && s < totals.Length ? totals[s] : 0f;
    }

    /// <summary>
    /// The total of a stat for one element and / or one kind of ability: the general modifiers plus those limited to
    /// <paramref name="element"/> or <paramref name="scope"/> (e.g. Status Resistance against Poison).
    /// </summary>
    public float GetFor(CombatStatType stat, ElementType element, StatScope scope = StatScope.Everything)
    {
        float total = Get(stat, wieldedCategory);
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            for (int m = 0; m < e.mods.Length; m++)
            {
                CombatStatModifier mod = e.mods[m];
                if (mod.stat != stat || !mod.IsFiltered)
                    continue;
                if (mod.onlyWithWeapon != WeaponCategory.None && mod.onlyWithWeapon != wieldedCategory)
                    continue;
                bool elementOk = mod.onlyElement == ElementType.None || !CombatStatInfo.UsesElement(stat) || mod.onlyElement == element;
                bool scopeOk = mod.scope == StatScope.Everything || !CombatStatInfo.UsesScope(stat) || mod.scope == scope;
                if (elementOk && scopeOk)
                    total += mod.value * e.strength;
            }
        }
        return total;
    }

    // ------------------------------------------------------------------ derived values (one place for every formula)
    private static float Percent(float v, float min = -100f, float max = 1000f) => Mathf.Clamp(v, min, max) / 100f;

    /// <summary>Body scale from Height (1.1 = 10% taller), between 0.5 and 2.</summary>
    public float HeightScale => Mathf.Clamp(1f + Percent(Get(CombatStatType.Height)), 0.5f, 2f);
    /// <summary>Threat multiplier (1.2 = 20% more threat).</summary>
    public float ThreatMultiplier => Mathf.Max(0f, 1f + Percent(Get(CombatStatType.ThreatGenerated)));
    /// <summary>Duration multiplier of the crowd control this character applies.</summary>
    public float ControlDealtMultiplier => Mathf.Max(0f, 1f + Percent(Get(CombatStatType.CrowdControlDuration)));
    /// <summary>Duration multiplier of crowd control on this character (Crowd Control Resistance, capped).</summary>
    public float ControlTakenMultiplier => 1f - Percent(Get(CombatStatType.CrowdControlResistance), -100f, maxControlResistance);
    public float BuffDurationMultiplier => Mathf.Max(0f, 1f + Percent(Get(CombatStatType.BuffDuration)));
    public float BuffStrengthMultiplier => Mathf.Max(0f, 1f + Percent(Get(CombatStatType.BuffStrength)));
    public float DebuffDurationMultiplier(ElementType element) => Mathf.Max(0f, 1f + Percent(GetFor(CombatStatType.DebuffDuration, element)));
    public float DebuffStrengthMultiplier(ElementType element) => Mathf.Max(0f, 1f + Percent(GetFor(CombatStatType.DebuffStrength, element)));
    /// <summary>Chance (0-1) added to the damage over time and debuffs this character applies.</summary>
    public float StatusChanceBonus(ElementType element) => Percent(GetFor(CombatStatType.StatusChance, element));
    /// <summary>Duration multiplier of damage over time and debuffs on this character (Status Resistance, capped).</summary>
    public float StatusTakenMultiplier(ElementType element) => 1f - Percent(GetFor(CombatStatType.StatusResistance, element), -100f, maxStatusResistance);
    public float DrawSpeedMultiplier => Mathf.Max(0.1f, 1f + Percent(Get(CombatStatType.DrawSpeed)));
    public float ReloadSpeedMultiplier => Mathf.Max(0.1f, 1f + Percent(Get(CombatStatType.ReloadSpeed)));
    /// <summary>Cooldown multiplier for one kind of ability (0.8 = 20% shorter; Cooldown Reduction, capped).</summary>
    public float CooldownMultiplier(StatScope scope) => 1f - Percent(GetFor(CombatStatType.CooldownReduction, ElementType.None, scope), -100f, maxCooldownReduction);
    /// <summary>Mana cost multiplier for one kind of ability (Mana Cost Reduction, capped).</summary>
    public float ManaCostMultiplier(StatScope scope) => 1f - Percent(GetFor(CombatStatType.ManaCostReduction, ElementType.None, scope), -100f, maxManaCostReduction);

    /// <summary>The cooldown of an item (a potion) after Cooldown Reduction for items, for whoever owns <paramref name="anyPart"/>.</summary>
    public static float ItemCooldown(Component anyPart, float baseCooldown)
    {
        if (baseCooldown <= 0f || anyPart == null)
            return baseCooldown;
        CombatStats s = For(anyPart, addIfMissing: false);
        return s != null ? baseCooldown * s.CooldownMultiplier(StatScope.Items) : baseCooldown;
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

    /// <summary>
    /// Weapon damage multiplier from Weapon Damage and the scaling attribute. (Elemental Damage now applies to every
    /// elemental hit - weapons, abilities, damage over time - when the damage is dealt; <paramref name="elemental"/> is kept
    /// for older callers.)
    /// </summary>
    public float WeaponDamageMultiplier(WeaponCategory weapon, WeaponScaling scaling, bool elemental)
    {
        float m = 1f + Get(CombatStatType.WeaponDamage, weapon) / 100f;
        m *= 1f + ScalingPoints(scaling, weapon) * weaponDamagePercentPerAttributePoint / 100f;
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

    /// <summary>
    /// Where one stat comes from: each source (race, class, trait, item, buff...) with its contribution, coloured
    /// good / bad (character sheet tooltips). Element / ability-limited parts and scaling rules are included.
    /// </summary>
    public List<StatText.Line> SourcesOf(CombatStatType stat)
    {
        var lines = new List<StatText.Line>();
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            for (int m = 0; m < e.mods.Length; m++)
                if (e.mods[m] != null && e.mods[m].stat == stat)
                    lines.Add(new StatText.Line($"{e.mods[m].Describe(e.strength)}  ({e.label})", StatText.Sign(e.mods[m])));
            for (int r = 0; r < e.rules.Length; r++)
            {
                StatScalingRule rule = e.rules[r];
                if (rule == null || rule.to != stat) continue;
                float amount = Get(rule.from) * rule.perPoint * e.strength;
                if (!Mathf.Approximately(amount, 0f))
                    lines.Add(new StatText.Line($"{amount:+0.##;-0.##} from {CombatStatInfo.Get(rule.from).name}  ({e.label})", amount > 0f ? 1 : -1));
            }
        }
        return lines;
    }

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

    // ------------------------------------------------------------------ damage dealt and taken
    /// <summary>
    /// Damage this character DEALS (any source: weapons, abilities, damage over time): Elemental Damage for the hit's
    /// element. Runs before the target's own modifiers.
    /// </summary>
    float IDamageDealtModifier.ModifyDamageDealt(in DamageInfo info, float amount)
    {
        if (info.element != ElementType.None)
            amount *= Mathf.Max(0f, 1f + Percent(GetFor(CombatStatType.ElementalDamage, info.element)));
        return amount;
    }

    /// <summary>
    /// Damage this character TAKES. True damage ignores all of it. Physical: armour (Defense, located by body part) minus
    /// the attacker's Armor Penetration (%) then Physical Penetration (points), through the reduction curve; then Physical
    /// Defense (%). Magical: Magic Resistance minus the attacker's Magic Penetration (%), then Magic Defense (%). Then the
    /// elemental resistance of the hit's element.
    /// </summary>
    float IDamageTakenModifier.ModifyDamageTaken(in DamageInfo info, float amount)
    {
        if (info.type == DamageType.True)
            return amount;
        CombatStats attacker = info.source != null ? info.source.Stats : null;
        if (attacker == this)
            attacker = null;
        if (info.type == DamageType.Physical)
        {
            float armor = DefenseAgainst(info, CombatStatType.Defense);
            if (attacker != null && armor > 0f)
            {
                armor *= 1f - Mathf.Clamp01(attacker.Get(CombatStatType.ArmorPenetration) / 100f);
                armor = Mathf.Max(0f, armor - Mathf.Max(0f, attacker.Get(CombatStatType.PhysicalPenetration)));
            }
            amount *= 1f - Reduction(armor);
            amount *= 1f - Percent(Get(CombatStatType.PhysicalDefense), -100f, maxDamageReduction * 100f);
        }
        else if (info.type == DamageType.Magical)
        {
            float resist = DefenseAgainst(info, CombatStatType.MagicResistance);
            if (attacker != null && resist > 0f)
                resist *= 1f - Mathf.Clamp01(attacker.Get(CombatStatType.MagicPenetration) / 100f);
            amount *= 1f - Reduction(resist);
            amount *= 1f - Percent(Get(CombatStatType.MagicDefense), -100f, maxDamageReduction * 100f);
        }
        if (info.element != ElementType.None)
            amount *= 1f - GetResistance(info.element) / 100f;
        return Mathf.Max(0f, amount);
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
        PushAbilityLayers();
        float end = Get(CombatStatType.Endurance);
        if (Mathf.Approximately(end, derivedEndurance))
            return;
        derivedEndurance = end;

        if (traits != null)
        {
            if (derivedHandle != null)
                traits.RevertModifiers(derivedHandle);
            derivedHandle = null;
            if (!Mathf.Approximately(end * maxHealthPerEndurance, 0f))
                derivedHandle = traits.ApplyModifiers(new[] { TraitModifier.Flat(TraitStat.MaxHealth, end * maxHealthPerEndurance) }, 1f, "Combat Stats");
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

    private const string AbilityLayerKey = "Combat Stats";
    private static readonly AbilitySlotSource[] AllSources = { AbilitySlotSource.Skill, AbilitySlotSource.Innate, AbilitySlotSource.Item };
    private AbilityCaster pushedCaster;

    private static StatScope ScopeOf(AbilitySlotSource source) =>
        source == AbilitySlotSource.Innate ? StatScope.Innate : source == AbilitySlotSource.Item ? StatScope.Items : StatScope.Skills;

    /// <summary>
    /// Gives every ability of the character (players and mobs) what its stats change: Intelligence → ability damage,
    /// Casting Speed → cast time; and per kind of ability, Cooldown Reduction and Mana Cost Reduction.
    /// </summary>
    private void PushAbilityLayers()
    {
        AbilityCaster caster = entity != null ? entity.Caster : null;
        if (caster == null)
            return;
        pushedCaster = caster;
        float intel = Get(CombatStatType.Intelligence);
        float cast = Get(CombatStatType.CastingSpeed);
        caster.SetModifierLayer(AbilityLayerKey, new AbilityModifierSet
        {
            label = AbilityLayerKey,
            damageMultiplier = Mathf.Max(0f, 1f + intel * abilityDamagePercentPerIntelligence / 100f),
            castTimeMultiplier = 1f / Mathf.Max(0.1f, 1f + cast / 100f), // +X% casting speed = cast time / (1 + X/100)
        });
        foreach (AbilitySlotSource src in AllSources)
        {
            StatScope scope = ScopeOf(src);
            caster.SetScopedModifiers(src, new AbilityModifierSet
            {
                label = AbilityLayerKey,
                cooldownMultiplier = Mathf.Max(0.05f, CooldownMultiplier(scope)),
                manaCostMultiplier = Mathf.Max(0.0001f, ManaCostMultiplier(scope)),
            });
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
