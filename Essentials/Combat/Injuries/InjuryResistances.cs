using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// One resistance (or weakness) to injuries and damage forms: "Tough Skin: 40% fewer wounds", "Strong Bones: fractures
/// are milder", "Skeleton: no bleeding, blunt hits do 50% more", "Iron Stomach: infections are rarer". It applies to the
/// injuries listed (ids or tags), from hits of the forms chosen, on the kinds of parts chosen. Every value is a percent
/// change: negative = better for the character (less likely, milder, slower), positive = worse.
/// <para>Given by the <see cref="InjuryResistances"/> component on a character (mob prefabs), the trait behaviour
/// <see cref="InjuryResistanceTrait"/> (traits, and through innate traits races and classes), the equipment effect
/// <see cref="InjuryResistanceEquipEffect"/> (items, armour sets, races and classes' effects) and the ability effect
/// <see cref="InjuryResistanceBuffEffect"/> (a timed buff).</para>
/// </summary>
[Serializable]
public class InjuryResistance
{
    [Tooltip("Name shown in the Health tab and tooltips (\"Tough Skin\"). Empty = the source's name (the trait, the item).")]
    public string label = "";
    [Tooltip("Injury types (ids: Scratch, Fracture, Concussion...) or tags (wound, bleeding, fracture, burn, infection, bruise, " +
             "concussion...) it applies to. Empty = every injury.")]
    public List<string> injuries = new List<string>();
    [Tooltip("Only hits of these forms (Slash, Pierce, Blunt, Bite, Claw, Crush; Formless = magic, elements). Injury chance, " +
             "severity, limb loss and damage taken follow it.")]
    public DamageFormMask forms = DamageFormMask.All;
    [Tooltip("Only on these kinds of body parts (a thick skull: Head). Empty = every part.")]
    public List<BodyPartKind> partKinds = new List<BodyPartKind>();

    [Header("Changes (percent: negative = better)")]
    [Tooltip("Chance a hit causes the injuries above. -40 = 40% less likely, -100 = never (immune), +50 = 50% more likely.")]
    [Range(-100f, 300f)] public float injuryChance = 0f;
    [Tooltip("How bad they are when they happen: the hit counts this much softer (negative) or harder for the stage. -40 = a cracked " +
             "bone instead of a broken one.")]
    [Range(-100f, 300f)] public float severity = 0f;
    [Tooltip("Chance the injuries above get worse when untreated, or that a wound turns into them (infections). -100 = never.")]
    [Range(-100f, 300f)] public float worsening = 0f;
    [Tooltip("How fast the injuries above bleed. -50 = half as fast, -100 = they never bleed.")]
    [Range(-100f, 300f)] public float bleeding = 0f;
    [Tooltip("How fast the injuries above heal (and clot). +50 = 50% faster.")]
    [Range(-90f, 500f)] public float healing = 0f;
    [Tooltip("Chance of losing a limb to hits of the forms above (on the parts above; the injury list is ignored). -100 = never.")]
    [Range(-100f, 300f)] public float limbLoss = 0f;
    [Tooltip("Health damage of hits of the forms above (the injury and part lists are ignored). -25 = 25% less damage from them, " +
             "+50 = a weakness.")]
    [Range(-100f, 300f)] public float damageTaken = 0f;

    public InjuryResistance() { }

    public InjuryResistance(string label, params string[] injuries)
    {
        this.label = label;
        if (injuries != null)
            this.injuries.AddRange(injuries);
    }

    /// <summary>Does it apply to injuries of <paramref name="type"/> (by id or tag)?</summary>
    public bool AppliesTo(InjuryTypeDefinition type)
    {
        if (injuries == null || injuries.Count == 0)
            return true;
        if (type == null)
            return false;
        for (int i = 0; i < injuries.Count; i++)
        {
            string k = injuries[i];
            if (string.IsNullOrWhiteSpace(k))
                continue;
            k = k.Trim();
            if (string.Equals(k, type.id, StringComparison.OrdinalIgnoreCase) || type.HasTag(k))
                return true;
        }
        return false;
    }

    /// <summary>Does it apply on <paramref name="part"/> (null = unknown: only when no part kinds are set)?</summary>
    public bool AppliesOn(InjuryBodyPart part)
    {
        if (partKinds == null || partKinds.Count == 0)
            return true;
        return part != null && partKinds.Contains(part.kind);
    }

    public bool AppliesToForm(DamageForm form) => DamageForms.Contains(forms, form);

    /// <summary>Has it any effect at all?</summary>
    public bool IsEmpty =>
        Mathf.Approximately(injuryChance, 0f) && Mathf.Approximately(severity, 0f) && Mathf.Approximately(worsening, 0f) &&
        Mathf.Approximately(bleeding, 0f) && Mathf.Approximately(healing, 0f) && Mathf.Approximately(limbLoss, 0f) &&
        Mathf.Approximately(damageTaken, 0f);

    /// <summary>A percent change as a multiplier (strength scales the change; never below 0).</summary>
    public static float Factor(float percent, float strength) => Mathf.Max(0f, 1f + percent / 100f * strength);

    /// <summary>"-40% wound chance, -25% slash damage (Slash, Claw)".</summary>
    public string Describe(float strength = 1f)
    {
        var parts = new List<string>();
        string what = injuries == null || injuries.Count == 0 ? "injury" : string.Join("/", injuries);
        void Add(float v, string text)
        {
            if (Mathf.Approximately(v, 0f))
                return;
            v *= strength;
            parts.Add(v <= -99.5f ? $"no {text}" : $"{v:+0;-0}% {text}");
        }
        Add(injuryChance, what + " chance");
        Add(severity, what + " severity");
        Add(worsening, what + " worsening");
        Add(bleeding, "bleeding");
        Add(healing, what + " healing");
        Add(limbLoss, "limb loss");
        Add(damageTaken, "damage");
        if (parts.Count == 0)
            return string.IsNullOrEmpty(label) ? "(no effect)" : label;
        string s = string.Join(", ", parts);
        if ((forms & DamageFormMask.All) != DamageFormMask.All)
            s += $" from {DamageForms.Describe(forms)} hits";
        if (partKinds != null && partKinds.Count > 0)
            s += $" on the {string.Join("/", partKinds)}";
        return string.IsNullOrEmpty(label) ? s : $"{label}: {s}";
    }

    public InjuryResistance Clone()
    {
        var c = (InjuryResistance)MemberwiseClone();
        c.injuries = new List<string>(injuries ?? new List<string>());
        c.partKinds = new List<BodyPartKind>(partKinds ?? new List<BodyPartKind>());
        return c;
    }
}

/// <summary>
/// The injury resistances of one character: its own (the inspector list: put it on a mob prefab - a skeleton that never
/// bleeds and breaks under maces), and those given at runtime by traits, races, classes, items and buffs. The
/// <see cref="InjuryController"/> asks it how likely and how bad each injury is; it also changes the damage the character
/// takes from each <see cref="DamageForm"/>. The character stats Injury Chance, Wound / Fracture / Burn / Infection Chance,
/// Bleeding, Injury Healing and Limb Loss Chance (traits, items, buffs: see <see cref="TraitStat"/>) count here too.
/// Added to a character automatically when something gives it a resistance.
/// </summary>
[DisallowMultipleComponent]
public class InjuryResistances : MonoBehaviour, IDamageTakenModifier
{
    [Tooltip("This character's own resistances and weaknesses (a skeleton: Bleeding -100%, Blunt damage +50%; a troll: Healing +200%).")]
    [SerializeField] private List<InjuryResistance> innate = new List<InjuryResistance>();

    /// <summary>A set of resistances given by something (a trait, an item, a buff).</summary>
    public sealed class Entry
    {
        public InjuryResistance resistance;
        public float strength = 1f;
        public string source;
        public object token;
        /// <summary>Time.time it ends (buffs); infinity = while its source lasts.</summary>
        public float until = float.PositiveInfinity;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<Entry> view = new List<Entry>();
    private CombatEntity entity;
    private TraitManager traits;
    private bool registered;
    private bool timed;
    private bool viewDirty = true;

    /// <summary>Raised when resistances are added or removed.</summary>
    public event Action Changed;
    /// <summary>Counts every change (UI).</summary>
    public int Version { get; private set; }

    /// <summary>The innate list (editor tools, scripts). Call <see cref="NotifyChanged"/> after changing it.</summary>
    public List<InjuryResistance> Innate => innate ?? (innate = new List<InjuryResistance>());

    public CombatEntity Entity => entity != null ? entity : (entity = CombatEntity.Resolve(gameObject) ?? GetComponentInParent<CombatEntity>());

    // ------------------------------------------------------------------ finding
    /// <summary>The resistances of a character, or null when it has none yet.</summary>
    public static InjuryResistances Find(Component anyPart)
    {
        if (anyPart == null)
            return null;
        InjuryResistances r = anyPart.GetComponentInParent<InjuryResistances>();
        if (r != null)
            return r;
        CombatEntity e = anyPart as CombatEntity ?? CombatEntity.Resolve(anyPart.gameObject);
        return e != null ? e.GetComponentInChildren<InjuryResistances>(true) : anyPart.GetComponentInChildren<InjuryResistances>(true);
    }

    /// <summary>The resistances of a character (added on its Combat Entity's object when missing).</summary>
    public static InjuryResistances For(GameObject character)
    {
        if (character == null)
            return null;
        InjuryResistances r = Find(character.transform);
        if (r != null)
            return r;
        CombatEntity e = CombatEntity.Resolve(character);
        return (e != null ? e.gameObject : character).AddComponent<InjuryResistances>();
    }

    public static InjuryResistances For(Component character) => character != null ? For(character.gameObject) : null;

    // ------------------------------------------------------------------ lifecycle
    private void OnEnable() => Register();
    private void Start() => Register();

    private void OnDisable()
    {
        if (registered && entity != null)
            entity.RemoveDamageTakenModifier(this);
        registered = false;
    }

    private void Register()
    {
        if (registered || !isActiveAndEnabled)
            return;
        CombatEntity e = Entity;
        if (e == null)
            return;
        e.AddDamageTakenModifier(this);
        registered = true;
    }

    private void Update()
    {
        if (!timed)
            return;
        float now = Time.time;
        bool any = false, removed = false;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (float.IsPositiveInfinity(entries[i].until))
                continue;
            if (entries[i].until <= now)
            {
                entries.RemoveAt(i);
                removed = true;
            }
            else
                any = true;
        }
        timed = any;
        if (removed)
            NotifyChanged();
    }

    // ------------------------------------------------------------------ adding
    /// <summary>
    /// Adds a set of resistances (scaled by <paramref name="strength"/>) and returns the token that removes them
    /// (<see cref="Remove"/>). <paramref name="seconds"/> &gt; 0 = they end by themselves (buffs).
    /// </summary>
    public object Add(IList<InjuryResistance> list, float strength, string source, float seconds = 0f)
    {
        var token = new object();
        if (list == null)
            return token;
        float until = seconds > 0f ? Time.time + seconds : float.PositiveInfinity;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null)
                continue;
            entries.Add(new Entry { resistance = list[i], strength = strength, source = source, token = token, until = until });
        }
        if (seconds > 0f)
            timed = true;
        Register();
        NotifyChanged();
        return token;
    }

    /// <summary>Adds one resistance. See <see cref="Add(IList{InjuryResistance},float,string,float)"/>.</summary>
    public object Add(InjuryResistance resistance, float strength = 1f, string source = "", float seconds = 0f) =>
        Add(new[] { resistance }, strength, source, seconds);

    /// <summary>Removes what <see cref="Add(IList{InjuryResistance},float,string,float)"/> added (nothing for an unknown token).</summary>
    public void Remove(object token)
    {
        if (token == null)
            return;
        if (entries.RemoveAll(e => e.token == token) > 0)
            NotifyChanged();
    }

    /// <summary>Replaces a buff of the same source (refreshing its time) instead of stacking it.</summary>
    public object Refresh(IList<InjuryResistance> list, float strength, string source, float seconds)
    {
        if (entries.RemoveAll(e => e.source == source && !float.IsPositiveInfinity(e.until)) > 0)
            viewDirty = true;
        return Add(list, strength, source, seconds);
    }

    /// <summary>Call after changing the innate list from a script.</summary>
    public void NotifyChanged()
    {
        viewDirty = true;
        Version++;
        Changed?.Invoke();
    }

    /// <summary>Every resistance in effect (innate first), with its strength and source.</summary>
    public IReadOnlyList<Entry> All
    {
        get
        {
            if (viewDirty)
            {
                viewDirty = false;
                view.Clear();
                if (innate != null)
                    foreach (InjuryResistance r in innate)
                        if (r != null)
                            view.Add(new Entry { resistance = r, strength = 1f, source = "Innate" });
                view.AddRange(entries);
            }
            return view;
        }
    }

    private void OnValidate() => viewDirty = true;

    // ------------------------------------------------------------------ character stats
    private TraitManager Traits
    {
        get
        {
            if (traits == null)
            {
                traits = GetComponentInParent<TraitManager>();
                if (traits == null)
                    traits = GetComponentInChildren<TraitManager>();
                CombatEntity e = Entity;
                if (traits == null && e != null)
                    traits = e.GetComponentInParent<TraitManager>() ?? e.GetComponentInChildren<TraitManager>();
            }
            return traits;
        }
    }

    private float Stat(TraitStat stat)
    {
        TraitManager t = Traits;
        return t != null ? t.GetStatMultiplier(stat) : 1f;
    }

    /// <summary>The character stat for injuries of <paramref name="type"/>'s tags (wound, fracture, burn, infection).</summary>
    private float TypeStats(InjuryTypeDefinition type)
    {
        if (type == null || Traits == null)
            return 1f;
        float f = Stat(TraitStat.InjuryChance);
        if (type.HasTag(InjuryDefaults.TagWound)) f *= Stat(TraitStat.WoundChance);
        if (type.HasTag(InjuryDefaults.TagFracture)) f *= Stat(TraitStat.FractureChance);
        if (type.HasTag(InjuryDefaults.TagBurn)) f *= Stat(TraitStat.BurnChance);
        if (type.HasTag(InjuryDefaults.TagInfection)) f *= Stat(TraitStat.InfectionChance);
        return f;
    }

    // ------------------------------------------------------------------ queries (the injury controller)
    /// <summary>Multiplies the chance a hit of <paramref name="form"/> causes an injury of <paramref name="type"/> on <paramref name="part"/> (0 = immune).</summary>
    public float InjuryChanceMultiplier(InjuryTypeDefinition type, InjuryBodyPart part, DamageForm form)
    {
        float f = TypeStats(type);
        IReadOnlyList<Entry> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            InjuryResistance r = all[i].resistance;
            if (r.injuryChance != 0f && r.AppliesTo(type) && r.AppliesOn(part) && r.AppliesToForm(form))
                f *= InjuryResistance.Factor(r.injuryChance, all[i].strength);
        }
        return f;
    }

    /// <summary>Multiplies how hard a hit counts for the stage of an injury of <paramref name="type"/> (1 = unchanged).</summary>
    public float SeverityMultiplier(InjuryTypeDefinition type, InjuryBodyPart part, DamageForm form)
    {
        float f = 1f;
        IReadOnlyList<Entry> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            InjuryResistance r = all[i].resistance;
            if (r.severity != 0f && r.AppliesTo(type) && r.AppliesOn(part) && r.AppliesToForm(form))
                f *= InjuryResistance.Factor(r.severity, all[i].strength);
        }
        return f;
    }

    /// <summary>
    /// The chance (0-1+) that <paramref name="injury"/> gets worse now, into <paramref name="into"/> (null = its next
    /// stage). Infections count the Infection Chance stat.
    /// </summary>
    public float WorseningMultiplier(Injury injury, InjuryTypeDefinition into)
    {
        InjuryTypeDefinition from = injury != null ? injury.Type : null;
        InjuryBodyPart part = injury != null && injury.Part != null ? injury.Part.Def : null;
        float f = 1f;
        if (into != null && into != from && into.HasTag(InjuryDefaults.TagInfection))
            f *= Stat(TraitStat.InfectionChance);
        else if (into == null && from != null && from.HasTag(InjuryDefaults.TagInfection))
            f *= Stat(TraitStat.InfectionChance);
        IReadOnlyList<Entry> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            InjuryResistance r = all[i].resistance;
            if (r.worsening != 0f && (r.AppliesTo(from) || (into != null && r.AppliesTo(into))) && r.AppliesOn(part))
                f *= InjuryResistance.Factor(r.worsening, all[i].strength);
        }
        return f;
    }

    /// <summary>Multiplies the bleeding of an injury of <paramref name="type"/> on <paramref name="part"/>.</summary>
    public float BleedingMultiplier(InjuryTypeDefinition type, InjuryBodyPart part)
    {
        float f = Stat(TraitStat.BleedingRate);
        IReadOnlyList<Entry> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            InjuryResistance r = all[i].resistance;
            if (r.bleeding != 0f && r.AppliesTo(type) && r.AppliesOn(part))
                f *= InjuryResistance.Factor(r.bleeding, all[i].strength);
        }
        return f;
    }

    /// <summary>Multiplies the healing (and clotting) speed of an injury of <paramref name="type"/> on <paramref name="part"/>.</summary>
    public float HealingMultiplier(InjuryTypeDefinition type, InjuryBodyPart part)
    {
        float f = Stat(TraitStat.InjuryHealing);
        IReadOnlyList<Entry> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            InjuryResistance r = all[i].resistance;
            if (r.healing != 0f && r.AppliesTo(type) && r.AppliesOn(part))
                f *= Mathf.Max(0.1f, InjuryResistance.Factor(r.healing, all[i].strength));
        }
        return f;
    }

    /// <summary>Multiplies the chance of losing <paramref name="part"/> to a hit of <paramref name="form"/> (0 = never).</summary>
    public float LimbLossMultiplier(InjuryBodyPart part, DamageForm form)
    {
        float f = Stat(TraitStat.LimbLossChance);
        IReadOnlyList<Entry> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            InjuryResistance r = all[i].resistance;
            if (r.limbLoss != 0f && r.AppliesOn(part) && r.AppliesToForm(form))
                f *= InjuryResistance.Factor(r.limbLoss, all[i].strength);
        }
        return f;
    }

    /// <summary>Multiplies the health damage of a hit of <paramref name="form"/> (the resistances' Damage Taken).</summary>
    public float DamageMultiplier(DamageForm form)
    {
        float f = 1f;
        IReadOnlyList<Entry> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            InjuryResistance r = all[i].resistance;
            if (r.damageTaken != 0f && r.AppliesToForm(form))
                f *= InjuryResistance.Factor(r.damageTaken, all[i].strength);
        }
        return f;
    }

    float IDamageTakenModifier.ModifyDamageTaken(in DamageInfo info, float amount)
    {
        if (info.type == DamageType.True || amount <= 0f)
            return amount;
        return amount * DamageMultiplier(info.form);
    }

    /// <summary>A short list for the Health tab: the names of the resistances and changed stats ("Tough Skin, Thick Blood, -40% Bleeding").</summary>
    public string ShortSummary()
    {
        var names = new List<string>();
        IReadOnlyList<Entry> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            InjuryResistance r = all[i].resistance;
            if (r == null || r.IsEmpty)
                continue;
            string n = !string.IsNullOrEmpty(r.label) ? r.label : all[i].source != "Innate" ? all[i].source : r.Describe(all[i].strength);
            if (!string.IsNullOrEmpty(n) && !names.Contains(n))
                names.Add(n);
        }
        TraitManager t = Traits;
        if (t != null)
        {
            foreach (TraitStat s in InjuryStats)
            {
                float m = t.GetStatMultiplier(s);
                if (!Mathf.Approximately(m, 1f))
                    names.Add($"{(m - 1f) * 100f:+0;-0}% {TraitStats.Get(s).name}");
            }
        }
        return string.Join(", ", names);
    }

    /// <summary>The character stats of damage forms and injuries.</summary>
    public static readonly TraitStat[] InjuryStats =
    {
        TraitStat.SlashDamageTaken, TraitStat.PierceDamageTaken, TraitStat.BluntDamageTaken, TraitStat.InjuryChance, TraitStat.WoundChance,
        TraitStat.FractureChance, TraitStat.BurnChance, TraitStat.InfectionChance, TraitStat.BleedingRate, TraitStat.InjuryHealing,
        TraitStat.LimbLossChance,
    };

    /// <summary>One line per resistance in effect (the Health tab, inspectors).</summary>
    public string Describe()
    {
        var sb = new StringBuilder();
        IReadOnlyList<Entry> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            InjuryResistance r = all[i].resistance;
            if (r == null || r.IsEmpty)
                continue;
            if (sb.Length > 0)
                sb.Append('\n');
            string text = r.Describe(all[i].strength);
            sb.Append(string.IsNullOrEmpty(r.label) && !string.IsNullOrEmpty(all[i].source) && all[i].source != "Innate" ? $"{all[i].source}: {text}" : text);
            if (!float.IsPositiveInfinity(all[i].until))
                sb.Append($" ({Mathf.Max(0f, all[i].until - Time.time):0}s)");
        }
        TraitManager t = Traits;
        if (t != null)
        {
            void Stat(TraitStat s)
            {
                float m = t.GetStatMultiplier(s);
                if (Mathf.Approximately(m, 1f))
                    return;
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append($"{(m - 1f) * 100f:+0;-0}% {TraitStats.Get(s).name}");
            }
            foreach (TraitStat s in InjuryStats)
                Stat(s);
        }
        return sb.ToString();
    }
}
