using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Who a character is: its race, its class and any other archetypes (background, subclass), its height and level.
/// It applies them through the systems the rest of the game already uses, so races, classes, traits, equipment and
/// buffs all stack the same way and each part is removed exactly:
/// <list type="bullet">
/// <item>combat stats, resistances, scaling and growth per level → <see cref="CombatStats"/> (one entry per part);</item>
/// <item>character stats (max health, move speed...) → <see cref="TraitManager"/> modifiers;</item>
/// <item>passives and abilities → the equipment effect system (abilities are granted as Innate, so "Innate" scoped
/// cooldown / mana cost stats apply to them);</item>
/// <item>innate traits → the Trait Manager (granted, cannot be removed by the player);</item>
/// <item>height → <see cref="CharacterBody"/>.</item>
/// </list>
/// Works on players and mobs (mobs simply have no Trait Manager / class). Only the race, class, height, level and
/// attribute upgrades need saving or sending over the network; everything else is derived from them.
/// </summary>
[DisallowMultipleComponent]
public class CharacterIdentity : MonoBehaviour
{
    [Tooltip("The character's race (any archetype; usually kind Race).")]
    [SerializeField] private CharacterArchetype race;
    [Tooltip("More archetypes: background, subclass, a class defined only as an archetype...")]
    [SerializeField] private List<CharacterArchetype> otherArchetypes = new List<CharacterArchetype>();
    [Tooltip("Use the class of the Player Status Controller: its combat stats and its archetype are applied too.")]
    [SerializeField] private bool useClassFromStatus = true;
    [Tooltip("Apply the class's Combat Stats block (Strength, Defense, Critical Chance, Attack Speed...) to Combat Stats.")]
    [SerializeField] private bool applyClassCombatStats = true;
    [Tooltip("Height in metres. 0 = the race's default height.")]
    [SerializeField, Min(0f)] private float height = 0f;
    [Tooltip("Level for growth per level. 0 = from the Experience Manager (1 without one). Set it on mobs.")]
    [SerializeField, Min(0)] private int level = 0;
    [Tooltip("Attribute points the player spent on level-up (Strength, Agility...). Saved with the character.")]
    [SerializeField] private List<CombatStatModifier> attributeUpgrades = new List<CombatStatModifier>();
    [SerializeField] private bool debugLog;

    private sealed class Applied
    {
        public CharacterArchetype archetype;
        public object stats, scaling, growth;
        public TraitManager.AppliedModifiers characterStats, characterGrowth;
        public readonly List<EquipmentEffectHandle> handles = new List<EquipmentEffectHandle>();
        public readonly List<Trait> grantedTraits = new List<Trait>();
    }

    private readonly List<Applied> applied = new List<Applied>();
    private readonly List<CharacterArchetype> archetypes = new List<CharacterArchetype>();
    private PlayerStatusController status;
    private ExperienceManager experience;
    private TraitManager traits;
    private CombatStats stats;
    private EquipmentContext ownContext;
    private PlayerClass appliedClass;
    private object classToken, upgradesToken;
    private int appliedLevel = -1;
    private bool started;

    /// <summary>Raised after the race, class, archetypes, height or level changed and were applied.</summary>
    public event Action Changed;

    public CharacterArchetype Race => race;
    public PlayerClass Class => useClassFromStatus && status != null ? status.CurrentPlayerClass : null;
    /// <summary>Every archetype in effect: race, the class's archetype, the others.</summary>
    public IList<CharacterArchetype> Archetypes
    {
        get
        {
            if (!started) BuildArchetypeList();
            return archetypes;
        }
    }
    public IReadOnlyList<CombatStatModifier> AttributeUpgrades => attributeUpgrades;

    /// <summary>Height in metres (the picked one, else the race's default).</summary>
    public float Height
    {
        get
        {
            CharacterArchetype h = HeightSource();
            if (height > 0f)
                return h != null ? h.ClampHeight(height) : height;
            return h != null ? h.defaultHeight : 0f;
        }
    }

    public int Level => level > 0 ? level : experience != null ? Mathf.Max(1, experience.CurrentLevel) : 1;

    /// <summary>The identity of a character (added when missing).</summary>
    public static CharacterIdentity For(Component anyPart, bool addIfMissing = true)
    {
        if (anyPart == null)
            return null;
        CharacterIdentity id = anyPart.GetComponentInParent<CharacterIdentity>();
        if (id != null || !addIfMissing)
            return id;
        CombatStats cs = CombatStats.For(anyPart, addIfMissing: true);
        return cs.gameObject.AddComponent<CharacterIdentity>();
    }

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        status = GetComponentInParent<PlayerStatusController>();
        experience = GetComponentInParent<ExperienceManager>();
        traits = status != null && status.TraitManager != null ? status.TraitManager : GetComponentInParent<TraitManager>();
        stats = CombatStats.For(this, addIfMissing: true);
    }

    private void OnEnable()
    {
        if (status != null) status.OnPlayerClassChanged += HandleClassChanged;
        if (experience != null) experience.OnLevelUp += HandleLevelUp;
        if (started) ApplyAll();
    }

    private void OnDisable()
    {
        if (status != null) status.OnPlayerClassChanged -= HandleClassChanged;
        if (experience != null) experience.OnLevelUp -= HandleLevelUp;
        RemoveAll();
    }

    private void Start()
    {
        started = true;
        ApplyAll();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < applied.Count; i++)
        {
            List<EquipmentEffectHandle> hs = applied[i].handles;
            for (int j = 0; j < hs.Count; j++)
            {
                EquipmentEffectHandle h = hs[j];
                if (h == null || h.Reverted || !h.NeedsTick)
                    continue;
                try { h.Tick(dt); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }
    }

    private void HandleClassChanged(PlayerClass c)
    {
        if (started && isActiveAndEnabled) ApplyAll();
    }

    private void HandleLevelUp(int newLevel)
    {
        if (started && isActiveAndEnabled) ApplyGrowth();
    }

    // ------------------------------------------------------------------ public API
    /// <summary>Sets the race (character creation, a transformation) and re-applies everything.</summary>
    public void SetRace(CharacterArchetype newRace)
    {
        race = newRace;
        Reapply();
    }

    public void AddArchetype(CharacterArchetype archetype)
    {
        if (archetype == null || otherArchetypes.Contains(archetype))
            return;
        otherArchetypes.Add(archetype);
        Reapply();
    }

    public void RemoveArchetype(CharacterArchetype archetype)
    {
        if (otherArchetypes.Remove(archetype))
            Reapply();
    }

    /// <summary>Sets the height in metres (clamped to the race's range).</summary>
    public void SetHeight(float metres)
    {
        height = Mathf.Max(0f, metres);
        ApplyHeight();
        Changed?.Invoke();
    }

    /// <summary>Overrides the level used for growth (mobs, scaled encounters). 0 = from the Experience Manager.</summary>
    public void SetLevel(int newLevel)
    {
        level = Mathf.Max(0, newLevel);
        ApplyGrowth();
    }

    /// <summary>Adds attribute points (a level-up stat point spent on Strength...). Kept and saved.</summary>
    public void AddAttribute(CombatStatType stat, float amount)
    {
        if (Mathf.Approximately(amount, 0f))
            return;
        CombatStatModifier existing = attributeUpgrades.Find(m => m != null && m.stat == stat && !m.IsFiltered && m.onlyWithWeapon == WeaponCategory.None);
        if (existing != null) existing.value += amount;
        else attributeUpgrades.Add(new CombatStatModifier(stat, amount));
        ApplyUpgrades();
        Changed?.Invoke();
    }

    /// <summary>Restores saved attribute upgrades.</summary>
    public void SetAttributeUpgrades(IEnumerable<CombatStatModifier> upgrades)
    {
        attributeUpgrades = upgrades != null ? new List<CombatStatModifier>(upgrades) : new List<CombatStatModifier>();
        ApplyUpgrades();
        Changed?.Invoke();
    }

    /// <summary>Removes and applies everything again (after editing archetype assets in Play Mode).</summary>
    public void Reapply()
    {
        if (!started || !isActiveAndEnabled)
            return;
        ApplyAll();
    }

    // ------------------------------------------------------------------ applying
    private void BuildArchetypeList()
    {
        archetypes.Clear();
        if (race != null) archetypes.Add(race);
        PlayerClass c = Class;
        if (c != null && c.archetype != null && !archetypes.Contains(c.archetype)) archetypes.Add(c.archetype);
        foreach (CharacterArchetype a in otherArchetypes)
            if (a != null && !archetypes.Contains(a)) archetypes.Add(a);
    }

    private void ApplyAll()
    {
        RemoveAll();
        BuildArchetypeList();
        string clash = TraitRules.WhyIncompatible(archetypes);
        if (clash != null)
            Debug.LogWarning($"[Identity] {name}: {clash} Both are applied anyway.", this);

        EquipmentContext ctx = Context();
        foreach (CharacterArchetype a in archetypes)
        {
            var ap = new Applied { archetype = a };
            string label = $"{a.kind}: {a.Name}";
            if (stats != null)
            {
                if (a.combatStats.Count > 0 || a.resistances.Count > 0)
                    ap.stats = stats.AddModifiers(label, a.combatStats, a.resistances);
                if (a.scalingRules.Count > 0)
                    ap.scaling = stats.AddScaling(label + " (scaling)", a.scalingRules);
            }
            if (traits != null && a.characterStats.Count > 0)
                ap.characterStats = traits.ApplyModifiers(a.characterStats, 1f, label);
            if (ctx != null && a.effects.Count > 0)
            {
                AbilitySlotSource previous = ctx.GrantSource;
                ctx.GrantSource = AbilitySlotSource.Innate;
                EquipmentEffect.ApplyAll(a.effects, ctx, 1f, label, ap.handles);
                ctx.GrantSource = previous;
            }
            if (traits != null)
            {
                foreach (Trait t in a.innateTraits)
                {
                    if (t == null) continue;
                    traits.AddTrait(t, ignoreRequirements: true);
                    traits.RegisterEquipmentGrant(t); // innate: the player cannot remove it
                    ap.grantedTraits.Add(t);
                }
            }
            applied.Add(ap);
        }

        appliedClass = Class;
        if (applyClassCombatStats && appliedClass != null && stats != null)
        {
            List<CombatStatModifier> mods = appliedClass.GetCombatStatModifiers();
            if (mods.Count > 0)
                classToken = stats.AddModifiers("Class: " + appliedClass.GetClassName(), mods);
        }
        ApplyUpgrades();
        ApplyGrowth(raiseChanged: false);
        ApplyHeight();
        if (debugLog)
            Debug.Log($"[Identity] {name}: {string.Join(" + ", archetypes.ConvertAll(a => a.Name))}{(appliedClass != null ? " / " + appliedClass.GetClassName() : "")}, level {Level}, {Height:0.00} m", this);
        Changed?.Invoke();
    }

    private void ApplyGrowth(bool raiseChanged = true)
    {
        int lv = Level;
        int steps = Mathf.Max(0, lv - 1);
        foreach (Applied ap in applied)
        {
            CharacterArchetype a = ap.archetype;
            if (stats != null && ap.growth != null) stats.RemoveModifiers(ap.growth);
            ap.growth = null;
            if (traits != null && ap.characterGrowth != null) traits.RevertModifiers(ap.characterGrowth);
            ap.characterGrowth = null;
            if (steps == 0)
                continue;
            if (stats != null && a.combatStatsPerLevel.Count > 0)
                ap.growth = stats.AddModifiers($"{a.Name} (level {lv})", a.combatStatsPerLevel, null, steps);
            if (traits != null && a.characterStatsPerLevel.Count > 0)
                ap.characterGrowth = traits.ApplyModifiers(a.characterStatsPerLevel, steps, $"{a.Name} (level {lv})");
        }
        appliedLevel = lv;
        if (raiseChanged)
            Changed?.Invoke();
    }

    private void ApplyUpgrades()
    {
        if (stats == null)
            return;
        if (upgradesToken != null) stats.RemoveModifiers(upgradesToken);
        upgradesToken = attributeUpgrades.Count > 0 ? stats.AddModifiers("Attribute points", attributeUpgrades) : null;
    }

    private CharacterArchetype HeightSource()
    {
        if (race != null && race.setsHeight) return race;
        foreach (CharacterArchetype a in Archetypes)
            if (a != null && a.setsHeight) return a;
        return null;
    }

    private void ApplyHeight()
    {
        float h = Height;
        if (h <= 0f)
            return;
        CharacterBody body = GetComponentInParent<CharacterBody>();
        if (body == null) body = GetComponentInChildren<CharacterBody>();
        if (body != null)
            body.SetHeight(h);
        else if (debugLog)
            Debug.Log($"[Identity] {name}: height {h:0.00} m is not shown - add a Character Body to scale the model.", this);
    }

    private void RemoveAll()
    {
        for (int i = applied.Count - 1; i >= 0; i--)
        {
            Applied ap = applied[i];
            EquipmentEffect.RevertAll(ap.handles);
            if (stats != null)
            {
                if (ap.stats != null) stats.RemoveModifiers(ap.stats);
                if (ap.scaling != null) stats.RemoveModifiers(ap.scaling);
                if (ap.growth != null) stats.RemoveModifiers(ap.growth);
            }
            if (traits != null)
            {
                if (ap.characterStats != null) traits.RevertModifiers(ap.characterStats);
                if (ap.characterGrowth != null) traits.RevertModifiers(ap.characterGrowth);
                foreach (Trait t in ap.grantedTraits)
                {
                    traits.UnregisterEquipmentGrant(t);
                    traits.RemoveTrait(t, forceRemove: true);
                }
            }
        }
        applied.Clear();
        if (stats != null)
        {
            if (classToken != null) stats.RemoveModifiers(classToken);
            if (upgradesToken != null) stats.RemoveModifiers(upgradesToken);
        }
        classToken = null;
        upgradesToken = null;
        appliedClass = null;
        appliedLevel = -1;
    }

    /// <summary>The equipment context (shared with the Equipment Manager so trait multipliers and mechanics combine).</summary>
    private EquipmentContext Context()
    {
        EquipmentManager equipment = GetComponentInParent<EquipmentManager>();
        if (equipment == null) equipment = GetComponentInChildren<EquipmentManager>();
        if (equipment != null)
            return equipment.Context;
        return ownContext ?? (ownContext = new EquipmentContext(this));
    }

    /// <summary>Lines describing what the identity gives (inspector, character sheet).</summary>
    public List<string> Describe()
    {
        var lines = new List<string>();
        foreach (CharacterArchetype a in Archetypes)
            if (a != null) lines.Add($"{a.kind}: {a.Name}");
        PlayerClass c = Class;
        if (c != null) lines.Add("Class: " + c.GetClassName());
        lines.Add($"Level {Level}" + (Height > 0f ? $", {Height:0.00} m" : ""));
        foreach (CombatStatModifier m in attributeUpgrades)
            if (m != null) lines.Add("Points: " + m.Describe());
        return lines;
    }
}
