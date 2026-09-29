using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// One row of a mob's absorption table: an ability of the mob and its share of a successful absorption roll.
/// </summary>
[Serializable]
public class MobAbsorptionEntry
{
    [Tooltip("An Ability Definition from the Ability Slots list (new system). Filled automatically by the inspector.")]
    public AbilityDefinition ability;

    [Tooltip("An old AbilityEffectSO from the Legacy Abilities list. Filled automatically by the inspector.")]
    public AbilityEffectSO legacyAbility;

    [Tooltip("Share (%) of a successful absorption that gives THIS ability. All rows together can add up to 100% at most; " +
             "what is left over means 'no ability'. Example: A 10%, B 90%, C 0% → when the kill roll succeeds, " +
             "B is granted 9 times out of 10, A once, C never.")]
    [Range(0f, 100f)] public float sharePercent;

    public bool IsEmpty => ability == null && legacyAbility == null;

    public string DisplayName =>
        ability != null ? ability.DisplayName : (legacyAbility != null ? legacyAbility.name + " (legacy)" : "(missing)");

    public bool Refers(UnityEngine.Object asset) =>
        asset != null && (ReferenceEquals(ability, asset) || ReferenceEquals(legacyAbility, asset));
}

/// <summary>
/// A mob's abilities, and what the player can absorb from it.
/// <para><b>Ability Slots</b> (the "Slots" list under "Abilities"): the new system. Each slot holds an Ability
/// Definition asset plus per-slot modifiers, an AI weight and an enable toggle. Use this for every new ability.</para>
/// <para><b>Legacy Abilities</b>: the old AbilityHolder list (AbilityEffectSO + attack casts + particle). Each entry is
/// converted to an Ability Definition when the game starts and added as an extra slot, so old prefabs keep working.
/// The inspector can convert them to assets and move them into the slots.</para>
/// <para>A basic melee attack is created from the Mob's Bite settings when the mob has no melee ability.</para>
/// <para><b>Absorption</b>: when the player kills the mob, ONE roll with <see cref="AbsorbChancePercent"/> decides
/// whether an ability is granted; the absorption table then picks WHICH one by its share (shares add up to 100% at
/// most; the remainder means nothing).</para>
/// </summary>
[DisallowMultipleComponent]
public class MobAbilityController : AbilityCaster, IAssignmentsValidator, IAbsorptionTable
{
    [Tooltip("The Mob (MobActionsController) this controller belongs to. Empty = found on this object at runtime " +
             "(press 'Auto-assign' in the inspector to fill it now).")]
    [SerializeField] private MobActionsController mobActionController;

    [Tooltip("OLD SYSTEM. AbilityHolder entries (AbilityEffectSO + attack casts + particle). Each one is converted to an " +
             "Ability Definition at runtime and added after the Ability Slots. Prefer the Ability Slots list for new " +
             "abilities, and use the inspector's 'Convert legacy abilities' button to move these over.")]
    [FormerlySerializedAs("abilities")]
    [SerializeField] private List<AbilityHolder> legacyAbilities = new List<AbilityHolder>();

    [Header("Absorption (what the player can take from this mob)")]
    [Tooltip("ON: killing this mob can give the player one of its abilities. OFF: never (summoned copies never can).")]
    [SerializeField] private bool absorbable = true;

    [Tooltip("Chance (%) that killing this mob grants an ability at all. Multiplied by the Mob Profile's Absorb Chance " +
             "Multiplier and the global multiplier in Absorption Settings. When it succeeds, the table below picks which ability.")]
    [SerializeField, Range(0f, 100f)] private float absorbChancePercent = 10f;

    [Tooltip("Which ability is granted when the roll succeeds. One row per ability (the inspector keeps the rows in sync). " +
             "Shares add up to 100% at most; the remainder means 'no ability'. EMPTY table = every absorbable ability " +
             "gets an equal share.")]
    [SerializeField] private List<MobAbsorptionEntry> absorptionTable = new List<MobAbsorptionEntry>();

    private Mob mob;
    private MobMovementStateMachine machine;
    private AbilityDefinition basicAttack;
    private static readonly Dictionary<string, AbilityDefinition> basicAttacks = new Dictionary<string, AbilityDefinition>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => basicAttacks.Clear();

    public Mob Mob => mob;

    /// <summary>The old AbilityHolder list (converted at runtime).</summary>
    public List<AbilityHolder> LegacyAbilities => legacyAbilities;

    /// <summary>The absorption table rows (edit through the inspector or <see cref="SyncAbsorptionTable"/>).</summary>
    public List<MobAbsorptionEntry> AbsorptionTable => absorptionTable;

    public bool Absorbable { get => absorbable; set => absorbable = value; }

    /// <summary>Chance (0-100) that a kill grants an ability, before the profile and global multipliers.</summary>
    public float AbsorbChancePercent
    {
        get => absorbChancePercent;
        set => absorbChancePercent = Mathf.Clamp(value, 0f, 100f);
    }

    /// <summary>The automatic basic attack added at runtime (null if none).</summary>
    public AbilityDefinition BasicAttack => basicAttack;

    protected override void Awake()
    {
        if (globalCooldown <= 0f)
            globalCooldown = 0.35f; // mobs pause briefly between abilities
        base.Awake();
    }

    protected override void OnInitialized()
    {
        mob = mobActionController != null ? mobActionController : GetComponent<Mob>();
        machine = GetComponent<MobMovementStateMachine>();

        // Legacy abilities.
        if (legacyAbilities != null)
        {
            for (int i = 0; i < legacyAbilities.Count; i++)
            {
                AbilityHolder h = legacyAbilities[i];
                if (h == null || h.abilityEffect == null)
                    continue;
                AbilityDefinition def = LegacyAbilityConverter.Convert(h, true);
                if (def != null && IndexOf(def) < 0)
                    AddSlot(def);
            }
        }

        // Basic attack from the Mob's bite settings.
        if (mob != null && mob.Profile.autoBasicAttack && mob.BiteDamage > 0 && !HasMeleeAbility())
        {
            basicAttack = GetBasicAttack(mob);
            AddSlot(basicAttack);
        }
    }

    private bool HasMeleeAbility()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot s = slots[i];
            if (s?.ability != null && s.enabled && MobAbilitySelector.IsMelee(s))
                return true;
        }
        return false;
    }

    private static AbilityDefinition GetBasicAttack(Mob m)
    {
        float reach = Mathf.Max(0.8f, m.AttackDistance);
        float cooldown = Mathf.Max(0.3f, m.BiteCooldown);
        string key = $"{m.type}|{m.BiteDamage}|{reach:0.##}|{cooldown:0.##}|{m.IsPartialWait}";
        if (basicAttacks.TryGetValue(key, out AbilityDefinition def) && def != null)
            return def;
        def = CreateBasicAttackDefinition(m);
        AbilityPresets.AsRuntime(def, "basic:" + key);
        def.SetDisplayName(BasicAttackName(m));
        basicAttacks[key] = def;
        return def;
    }

    /// <summary>
    /// A new (unsaved) basic melee attack built from the Mob's Bite Damage / Attack Distance / Bite Cooldown / Is
    /// Partial Wait - the same ability the mob gets automatically. The editor saves it as an asset so it can be edited.
    /// </summary>
    public static AbilityDefinition CreateBasicAttackDefinition(Mob m)
    {
        float reach = Mathf.Max(0.8f, m.AttackDistance);
        float cooldown = Mathf.Max(0.3f, m.BiteCooldown);
        AbilityDefinition def = AbilityPresets.BasicMelee(Mathf.Max(1, m.BiteDamage), reach, cooldown);
        def.activeTime = m.IsPartialWait ? 0f : Mathf.Min(cooldown, 0.8f);
        def.movementWhileCasting = m.IsPartialWait ? CasterMovementRule.Slowed : CasterMovementRule.Stop;
        def.absorption.canBeAbsorbed = false; // a plain bite is not an ability the player can take
        def.SetDisplayName(BasicAttackName(m));
        def.name = BasicAttackName(m);
        return def;
    }

    public static string BasicAttackName(Mob m) => string.IsNullOrEmpty(m.type) ? "Basic Attack" : m.type + " Basic Attack";

    /// <summary>Does a slot (Ability Slots list) hold a melee ability? Legacy entries are not checked.</summary>
    public bool HasMeleeSlot()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot s = slots[i];
            if (s?.ability != null && s.enabled && s.Grant == null && MobAbilitySelector.IsMelee(s))
                return true;
        }
        return false;
    }

    protected override CombatEntity DefaultTarget =>
        machine != null && machine.Context != null ? machine.Context.Brain.Target : null;

    protected override void FaceDirection(Vector3 direction, float dt)
    {
        if (machine != null && machine.Context != null)
            machine.Context.Motor.FaceDirection(direction);
        else
            base.FaceDirection(direction, dt);
    }

    // ------------------------------------------------------------------ absorption
    public override bool CanBeAbsorbedFrom => absorbable && (mob == null || !mob.SummonedPreventAbsorption);

    /// <summary>The Mob Profile's multiplier (mobs do not use per-ability chances; see <see cref="ChancePerKill"/>).</summary>
    public override float AbsorbChanceMultiplier => ProfileMultiplier;

    private float ProfileMultiplier
    {
        get
        {
            Mob m = mob != null ? mob : (mobActionController != null ? mobActionController : GetComponent<Mob>());
            return m != null ? m.Profile.absorbChanceMultiplier : 1f;
        }
    }

    /// <summary>Chance (0-1) that a kill grants an ability, including the profile multiplier (not the global one).</summary>
    public float ChancePerKill => Mathf.Clamp01(absorbChancePercent / 100f * ProfileMultiplier);

    /// <summary>
    /// Chance (0-1) that a kill grants an ability, including the profile and the global (Absorption Settings)
    /// multipliers. 0 when absorption is off.
    /// </summary>
    public float EffectiveChancePerKill
    {
        get
        {
            AbsorptionSettings settings = AbsorptionSettings.Instance;
            if (!absorbable || !settings.enabled)
                return 0f;
            return Mathf.Clamp01(ChancePerKill * settings.globalChanceMultiplier);
        }
    }

    /// <summary>
    /// Every ability the designer configured on this mob (Ability Slots, then Legacy Abilities) as the asset that
    /// identifies it: an AbilityDefinition or an AbilityEffectSO. Runtime-only slots (basic attack, absorbed,
    /// converted legacy copies) are not included.
    /// </summary>
    public void GetConfiguredAbilities(List<UnityEngine.Object> into)
    {
        into.Clear();
        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot s = slots[i];
            if (s == null || s.ability == null || s.Grant != null || s.ability == basicAttack || into.Contains(s.ability))
                continue;
            if (s.ability.LegacySource != null && HasLegacy(s.ability.LegacySource) && !IsAsset(s.ability))
                continue; // runtime conversion of a legacy entry: listed below as the legacy asset
            into.Add(s.ability);
        }
        for (int i = 0; i < legacyAbilities.Count; i++)
        {
            AbilityHolder h = legacyAbilities[i];
            if (h != null && h.abilityEffect != null && !into.Contains(h.abilityEffect))
                into.Add(h.abilityEffect);
        }
    }

    private static bool IsAsset(AbilityDefinition def) => def != null && !def.Id.StartsWith("legacy:");

    private bool HasLegacy(AbilityEffectSO legacy)
    {
        for (int i = 0; i < legacyAbilities.Count; i++)
            if (legacyAbilities[i] != null && legacyAbilities[i].abilityEffect == legacy)
                return true;
        return false;
    }

    /// <summary>
    /// Makes the table match the configured abilities: adds a row (0%) for each new ability and removes rows whose
    /// ability is gone. A table that was empty gets equal shares. Returns true if anything changed.
    /// </summary>
    public bool SyncAbsorptionTable(List<UnityEngine.Object> configuredBuffer = null)
    {
        List<UnityEngine.Object> configured = configuredBuffer ?? new List<UnityEngine.Object>();
        GetConfiguredAbilities(configured);
        bool wasEmpty = absorptionTable.Count == 0;
        bool changed = false;

        for (int i = absorptionTable.Count - 1; i >= 0; i--)
        {
            MobAbsorptionEntry e = absorptionTable[i];
            bool keep = e != null && !e.IsEmpty &&
                        ((e.ability != null && configured.Contains(e.ability)) || (e.legacyAbility != null && configured.Contains(e.legacyAbility)));
            if (!keep)
            {
                absorptionTable.RemoveAt(i);
                changed = true;
            }
        }
        for (int i = 0; i < configured.Count; i++)
        {
            UnityEngine.Object asset = configured[i];
            if (FindEntry(asset) >= 0)
                continue;
            var e = new MobAbsorptionEntry { sharePercent = 0f };
            if (asset is AbilityDefinition def)
                e.ability = def;
            else
                e.legacyAbility = asset as AbilityEffectSO;
            absorptionTable.Add(e);
            changed = true;
        }
        if (changed && wasEmpty)
            SplitSharesEvenly();
        return changed;
    }

    /// <summary>Index of the row for an ability asset (AbilityDefinition or AbilityEffectSO), or -1.</summary>
    public int FindEntry(UnityEngine.Object asset)
    {
        for (int i = 0; i < absorptionTable.Count; i++)
            if (absorptionTable[i] != null && absorptionTable[i].Refers(asset))
                return i;
        return -1;
    }

    /// <summary>Sum of all shares (0-100; more only if set directly by a script).</summary>
    public float TotalSharePercent
    {
        get
        {
            float t = 0f;
            for (int i = 0; i < absorptionTable.Count; i++)
                if (absorptionTable[i] != null)
                    t += Mathf.Max(0f, absorptionTable[i].sharePercent);
            return t;
        }
    }

    /// <summary>Is this row's ability allowed to be absorbed (its asset's Absorption ▸ Can Be Absorbed)?</summary>
    public static bool EntryAllowed(MobAbsorptionEntry e)
    {
        if (e == null || e.IsEmpty)
            return false;
        if (e.ability != null)
            return e.ability.absorption.canBeAbsorbed;
        return true; // legacy abilities are converted with absorption allowed
    }

    /// <summary>
    /// Sets one row's share, clamped so the table never adds up to more than 100% (with B at 90%, A can be 10% at
    /// most). Returns the value actually set.
    /// </summary>
    public float SetShare(int index, float percent)
    {
        if (index < 0 || index >= absorptionTable.Count || absorptionTable[index] == null)
            return 0f;
        float others = TotalSharePercent - Mathf.Max(0f, absorptionTable[index].sharePercent);
        float value = Mathf.Clamp(percent, 0f, Mathf.Max(0f, 100f - others));
        absorptionTable[index].sharePercent = value;
        return value;
    }

    /// <summary>Gives every absorbable row the same share (100% in total) and 0% to the others.</summary>
    public void SplitSharesEvenly()
    {
        int n = 0;
        for (int i = 0; i < absorptionTable.Count; i++)
            if (EntryAllowed(absorptionTable[i])) n++;
        float each = n > 0 ? Mathf.Floor(10000f / n) / 100f : 0f;
        for (int i = 0; i < absorptionTable.Count; i++)
        {
            MobAbsorptionEntry e = absorptionTable[i];
            if (e != null)
                e.sharePercent = EntryAllowed(e) ? each : 0f;
        }
        GiveRoundingToLargest();
    }

    /// <summary>Adds the rounding left-over (e.g. 3 × 33.33% = 99.99%) to the largest share so the total is 100%.</summary>
    private void GiveRoundingToLargest()
    {
        int largest = -1;
        float total = 0f;
        for (int i = 0; i < absorptionTable.Count; i++)
        {
            MobAbsorptionEntry e = absorptionTable[i];
            if (e == null || !EntryAllowed(e) || e.sharePercent <= 0f)
                continue;
            total += e.sharePercent;
            if (largest < 0 || e.sharePercent > absorptionTable[largest].sharePercent)
                largest = i;
        }
        float leftover = 100f - total;
        if (largest >= 0 && leftover > 0f && leftover < 0.5f)
            absorptionTable[largest].sharePercent = Mathf.Round((absorptionTable[largest].sharePercent + leftover) * 100f) / 100f;
    }

    /// <summary>Scales the absorbable rows' shares so they add up to 100% (keeping their proportions).</summary>
    public void NormalizeShares()
    {
        float total = 0f;
        for (int i = 0; i < absorptionTable.Count; i++)
            if (EntryAllowed(absorptionTable[i])) total += Mathf.Max(0f, absorptionTable[i].sharePercent);
        if (total <= 0f)
        {
            SplitSharesEvenly();
            return;
        }
        for (int i = 0; i < absorptionTable.Count; i++)
        {
            MobAbsorptionEntry e = absorptionTable[i];
            if (e != null)
                e.sharePercent = EntryAllowed(e) ? Mathf.Floor(Mathf.Max(0f, e.sharePercent) / total * 10000f) / 100f : 0f;
        }
        GiveRoundingToLargest();
    }

    /// <summary>
    /// The abilities a kill can grant and their share of a successful roll (0-1, adding up to 1 at most).
    /// Called by <see cref="AbilityAbsorption"/> when the mob dies.
    /// </summary>
    public void GetAbsorptionShares(List<AbsorptionShare> into)
    {
        into.Clear();
        if (absorptionTable.Count > 0)
        {
            for (int i = 0; i < absorptionTable.Count; i++)
            {
                MobAbsorptionEntry e = absorptionTable[i];
                if (e == null || e.sharePercent <= 0f || !EntryAllowed(e))
                    continue;
                AbilityDefinition def = ResolveRuntime(e);
                if (def != null && def.absorption.canBeAbsorbed)
                    into.Add(new AbsorptionShare { ability = def, share = e.sharePercent / 100f });
            }
        }
        else
        {
            // No table: every absorbable configured ability gets an equal share.
            for (int i = 0; i < slots.Count; i++)
            {
                AbilitySlot s = slots[i];
                if (s == null || s.ability == null || s.Grant != null || s.ability == basicAttack || !s.ability.absorption.canBeAbsorbed)
                    continue;
                into.Add(new AbsorptionShare { ability = s.ability, share = 1f });
            }
            for (int i = 0; i < into.Count; i++)
                into[i] = new AbsorptionShare { ability = into[i].ability, share = 1f / into.Count };
        }

        // Never more than 100% in total (a script may have set the numbers directly).
        float total = 0f;
        for (int i = 0; i < into.Count; i++)
            total += into[i].share;
        if (total > 1f)
            for (int i = 0; i < into.Count; i++)
                into[i] = new AbsorptionShare { ability = into[i].ability, share = into[i].share / total };
    }

    private AbilityDefinition ResolveRuntime(MobAbsorptionEntry e)
    {
        if (e.ability != null)
            return e.ability;
        if (e.legacyAbility == null)
            return null;
        for (int i = 0; i < legacyAbilities.Count; i++)
        {
            AbilityHolder h = legacyAbilities[i];
            if (h != null && h.abilityEffect == e.legacyAbility)
                return LegacyAbilityConverter.Convert(h, true);
        }
        return LegacyAbilityConverter.Convert(e.legacyAbility, true);
    }

    // ------------------------------------------------------------------ validation & setup
    /// <summary>Setup problems of this controller (shown by the inspector, logged at startup).</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        Mob m = mobActionController != null ? mobActionController : GetComponent<Mob>();
        if (m == null)
            errors.Add("No Mob / MobActionsController on this object: the AI cannot use these abilities.");
        if (GetComponent<MobMovementStateMachine>() == null)
            warnings.Add("No MobMovementStateMachine: nothing decides when to use these abilities.");

        int configured = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            AbilitySlot s = slots[i];
            if (s == null || s.Grant != null)
                continue;
            if (s.ability == null)
            {
                warnings.Add($"Ability Slot {i} is empty (remove it or assign an Ability Definition).");
                continue;
            }
            configured++;
            if (!s.enabled)
                warnings.Add($"'{s.ability.DisplayName}' (slot {i}) is disabled.");
            else if (s.aiWeight <= 0f)
                warnings.Add($"'{s.ability.DisplayName}' (slot {i}) has AI Weight 0: the AI never uses it on its own.");
        }
        for (int i = 0; i < legacyAbilities.Count; i++)
        {
            AbilityHolder h = legacyAbilities[i];
            if (h == null || h.abilityEffect == null)
                warnings.Add($"Legacy Abilities entry {i} has no Ability Effect.");
            else
                configured++;
        }
        if (configured == 0)
            warnings.Add(m != null && m.BiteDamage > 0
                ? "No abilities configured: the mob only uses its automatic basic attack (Mob ▸ Bite Damage)."
                : "No abilities and Bite Damage is 0: this mob cannot attack.");

        if (absorbable)
        {
            float total = TotalSharePercent;
            if (absorbChancePercent <= 0f)
                warnings.Add("Absorbable is on but Absorb Chance Per Kill is 0%: nothing can ever be absorbed.");
            if (total > 100.01f)
                errors.Add($"Absorption shares add up to {total:0.#}% (more than 100%). Press 'Fit to 100%'.");
            else if (absorptionTable.Count > 0 && total <= 0f && absorbChancePercent > 0f)
                warnings.Add("Every absorption share is 0%: a successful roll gives nothing. Give at least one ability a share.");
            for (int i = 0; i < absorptionTable.Count; i++)
            {
                MobAbsorptionEntry e = absorptionTable[i];
                if (e != null && e.sharePercent > 0f && !EntryAllowed(e))
                    warnings.Add($"'{e.DisplayName}' has a {e.sharePercent:0.#}% share but its asset has Absorption ▸ Can Be Absorbed off, so it is skipped.");
            }
        }
    }

    public void ValidateAssignments()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        Validate(errors, warnings);
        foreach (string e in errors)
            Debug.LogWarning($"[{name}] {e}", this);
        if (debugLog)
            foreach (string w in warnings)
                Debug.Log($"[{name}] {w}", this);
    }

    /// <summary>Fills empty references from this object (inspector button and setup tools).</summary>
    public void AutoAssignReferences()
    {
        if (mobActionController == null)
            mobActionController = GetComponent<MobActionsController>();
        if (animator == null)
            animator = MobMovementStateMachine.FindAnimator(transform);
    }

    private void Start()
    {
        ValidateAssignments();
    }
}
