using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class ActiveTraitInfo
{
    public Trait trait;
    [Tooltip("Off while an armor set has replaced the trait (its effects are off; points and grants are kept).")]
    public bool isActive;
    public bool isTemporary;
    public float remainingDuration;
    public float cooldownRemaining;
    public DateTime activatedTime;
    [Tooltip("Trait points paid for it. Refunded exactly when it is removed (0 when it was granted for free: armor, sets, the class, character creation).")]
    public int pointsSpent;
    [Tooltip("How many OTHER sources grant it too (armor pieces, sets, the class...). It is only removed when the last one lets go.")]
    public int extraGrants;

    /// <summary>Turned off for a while (an armor set replaced it). Its effects are off; points and grants are kept.</summary>
    public bool IsSuspended => !isActive;
}

/// <summary>
/// The traits a character has. Applies each trait's passive <see cref="Trait.modifiers"/> exactly (every change is
/// recorded and undone precisely when the trait is removed or its strength changes), runs its active/triggered
/// <see cref="Trait.behaviours"/> (double jump, wall climb, abilities on keys, second wind...), handles trait points,
/// dependencies, temporary traits and armor-set enhancements. Old string-based <see cref="TraitEffect"/>s still work.
/// <para>
/// Points and sources: a trait records the points actually paid for it and refunds exactly those. When armor, a set
/// or the class grants a trait the character already has, the extra grant is counted, and taking that armor off
/// releases only its own grant. Percent bonuses on max health/stamina/mana, carry weight and move speed follow the
/// base value when it changes (level-ups, equipment), and are re-applied after the class resets it.
/// </para>
/// </summary>
[DefaultExecutionOrder(50)]
public class TraitManager : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("All traits of the game (for the character creation screen and lookups). Empty = Resources/TraitDatabase or the first one in the project (editor).")]
    [SerializeField] private TraitDatabase traitDatabase;
    [Tooltip("Points left to spend on traits. Positive traits cost points, negative traits give points back.")]
    [SerializeField] private int availableTraitPoints = 10;
    [Tooltip("Maximum number of traits the character can have at once.")]
    [SerializeField] private int maxTraits = 10;
    [Tooltip("Traits with an active skill on their own key (an ability) a character may pick. Movement traits (double jump, " +
             "wall climb, glide) do not count; traits given by the race, class or equipment do not count either.")]
    [SerializeField, Min(0)] private int maxActiveTraits = 1;
    [Tooltip("Allow picking negative traits (drawbacks that give points back).")]
    [SerializeField] private bool allowNegativeTraits = true;

    [Header("Starting Traits")]
    [Tooltip("Traits the character always starts with (free, requirements ignored). Applied one frame after start so class stats are set first.")]
    [SerializeField] private List<Trait> startingTraits = new List<Trait>();

    [Header("Active Traits")]
    [Tooltip("Traits the character has right now (filled at runtime).")]
    [SerializeField] private List<ActiveTraitInfo> activeTraits = new List<ActiveTraitInfo>();

    [Header("Audio")]
    [Tooltip("Plays the traits' acquisition sounds. Empty = the AudioSource on this object.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Debug")]
    [Tooltip("Log added/removed traits and applied modifiers to the Console.")]
    [SerializeField] private bool debugLog = false;

    public event Action<Trait> OnTraitAdded;
    public event Action<Trait> OnTraitRemoved;
    public event Action<int> OnTraitPointsChanged;
    public event Action<Trait> OnTraitExpired;
    /// <summary>Raised after any change to the character's traits (added, removed, suspended, resumed, strength changed) - refresh trait UI here.</summary>
    public event Action OnTraitsChanged;

    // Armor Set Integration
    [Header("Armor Set Integration")]
    [Tooltip("Armor sets can strengthen, extend or replace traits.")]
    [SerializeField] private ArmorSetManager armorSetManager;
    [SerializeField] private Dictionary<Trait, float> traitMultipliers = new Dictionary<Trait, float>();
    [Tooltip("Traits currently granted by equipment (filled at runtime, one entry per grant). The player cannot remove them while they are listed.")]
    [SerializeField] private List<Trait> armorAppliedTraits = new List<Trait>();

    public event Action<Trait, float> OnTraitMultiplierChanged;
    public event Action<Trait, List<TraitEffect>> OnTraitEffectsAdded;

    // ------------------------------------------------------------------ applied modifiers
    /// <summary>A group of modifiers applied together (one trait's passives, a conditional set...). Revert with <see cref="RevertModifiers"/>.</summary>
    public sealed class AppliedModifiers
    {
        public string source;
        internal readonly List<AppliedMod> mods = new List<AppliedMod>();
        public int Count => mods.Count;
    }

    internal sealed class AppliedMod
    {
        public TraitStat stat;
        public TraitModifierMode mode;
        public float value;  // after the strength multiplier
        public float delta;  // exact amount written into the manager (reverted on removal)
    }

    private sealed class TraitRuntime
    {
        public Trait trait;
        public float multiplier = 1f;
        public AppliedModifiers passives;
        public AppliedModifiers legacy;
        public object combatToken;
        public readonly List<TraitBehaviour> behaviours = new List<TraitBehaviour>();
        public readonly List<TraitBehaviour> templates = new List<TraitBehaviour>();
    }

    private readonly Dictionary<Trait, TraitRuntime> runtimes = new Dictionary<Trait, TraitRuntime>(ReferenceComparer<Trait>.Instance);
    private readonly Dictionary<Trait, ActiveTraitInfo> infoByTrait = new Dictionary<Trait, ActiveTraitInfo>(ReferenceComparer<Trait>.Instance);
    private readonly List<Trait> activeList = new List<Trait>();
    private readonly Dictionary<TraitEffect, AppliedModifiers> extraEffects = new Dictionary<TraitEffect, AppliedModifiers>();
    private readonly List<AppliedModifiers> allApplied = new List<AppliedModifiers>();
    private readonly Dictionary<TraitStat, float> aggregate = new Dictionary<TraitStat, float>();
    private readonly Dictionary<TraitStat, float> aggregateSum = new Dictionary<TraitStat, float>();
    private readonly List<TraitRuntime> tickBuffer = new List<TraitRuntime>();
    private readonly List<ActiveTraitInfo> expiredBuffer = new List<ActiveTraitInfo>();

    private PlayerStatusController playerController;
    private PlayerInput input;
    private TraitContext context;
    private float baseControlTaken = -1f;
    private CombatEntity aggregateEntity;
    private bool aggregatesDirty;
    private bool startingApplied;
    private float nextBaseCheck;

    // Properties
    /// <summary>The active traits (a new list each call; use <see cref="Traits"/> in per-frame code).</summary>
    public List<Trait> ActiveTraits => new List<Trait>(activeList);
    /// <summary>The active traits without allocating (do not modify it).</summary>
    public IReadOnlyList<Trait> Traits => activeList;
    public List<ActiveTraitInfo> ActiveTraitInfos => activeTraits.Where(t => t.isActive).ToList();
    /// <summary>Every trait record, suspended ones included (do not modify it).</summary>
    public IReadOnlyList<ActiveTraitInfo> AllTraitInfos => activeTraits;
    public int AvailableTraitPoints => availableTraitPoints;
    public int MaxTraits => maxTraits;
    public TraitDatabase Database => traitDatabase;
    public IReadOnlyList<Trait> StartingTraits => startingTraits;

    /// <summary>The player's components as seen by trait behaviours.</summary>
    public TraitContext Context
    {
        get
        {
            if (context == null)
                context = new TraitContext(this, input);
            return context;
        }
    }

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        playerController = GetComponent<PlayerStatusController>();
        if (playerController == null)
            playerController = GetComponentInParent<PlayerStatusController>();
        if (traitDatabase == null)
            traitDatabase = TraitDatabase.Instance;
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (armorSetManager == null)
            armorSetManager = GetComponent<ArmorSetManager>();
        // The same copy of the input actions as the player's ability keys (one per player).
        input = SharedPlayerInput.Acquire(this);
        RebuildIndex();
    }

    private void OnEnable()
    {
        SharedPlayerInput.Enable(input);
        if (playerController != null)
        {
            playerController.OnPlayerClassChanged += HandleClassChanged;
            playerController.OnStatsInitialized += HandleStatsInitialized;
        }
    }

    private void OnDisable()
    {
        SharedPlayerInput.Disable(input);
        if (playerController != null)
        {
            playerController.OnPlayerClassChanged -= HandleClassChanged;
            playerController.OnStatsInitialized -= HandleStatsInitialized;
        }
    }

    private void Start()
    {
        // One frame later: the status controller has applied the class stats (which SET max values).
        StartCoroutine(ApplyStartingTraitsNextFrame());
    }

    private IEnumerator ApplyStartingTraitsNextFrame()
    {
        yield return null;
        ApplyStartingTraits();
    }

    private void OnDestroy()
    {
        foreach (TraitRuntime rt in runtimes.Values)
            foreach (TraitBehaviour b in rt.behaviours)
                SafeCall(() => b.OnRemoved(Context), b);
        runtimes.Clear();
        SharedPlayerInput.Release(input);
        input = null;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        UpdateTemporaryTraits(dt);

        // Level-ups, equipment and buffs change max values without telling the traits: look twice per second.
        if (Time.time >= nextBaseCheck)
        {
            nextBaseCheck = Time.time + 0.5f;
            CheckBaseChanges();
        }

        if (aggregatesDirty || (aggregateEntity == null && Context.Entity != null))
            RecomputeAggregates();

        tickBuffer.Clear();
        tickBuffer.AddRange(runtimes.Values);
        for (int i = 0; i < tickBuffer.Count; i++)
        {
            List<TraitBehaviour> list = tickBuffer[i].behaviours;
            for (int b = 0; b < list.Count; b++)
            {
                TraitBehaviour beh = list[b];
                try { beh.Tick(Context, dt); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }
    }

    /// <summary>Indexes the serialized list (entries present before the game started keep working as before).</summary>
    private void RebuildIndex()
    {
        infoByTrait.Clear();
        activeList.Clear();
        activeTraits.RemoveAll(t => t == null || t.trait == null);
        foreach (ActiveTraitInfo info in activeTraits)
        {
            if (infoByTrait.ContainsKey(info.trait))
                continue;
            infoByTrait[info.trait] = info;
            if (info.isActive)
                activeList.Add(info.trait);
        }
    }

    // ------------------------------------------------------------------ add / remove
    /// <summary>
    /// Adds a trait. With <paramref name="ignoreRequirements"/> it is a free grant (armor, set, class, creation): no
    /// points are spent and, if the character already has the trait, the extra grant is counted instead (so removing
    /// that armor later releases only its grant). Re-adding a temporary trait refreshes its duration. Returns true only
    /// when the trait was newly added.
    /// </summary>
    public bool AddTrait(Trait trait, bool ignoreRequirements = false, bool isTemporary = false, float duration = 0f)
    {
        if (trait == null)
            return false;

        if (infoByTrait.TryGetValue(trait, out ActiveTraitInfo owned))
        {
            if (isTemporary)
            {
                if (owned.isTemporary)
                    owned.remainingDuration = Mathf.Max(owned.remainingDuration, duration);
            }
            else if (ignoreRequirements)
            {
                if (owned.isTemporary)
                {
                    // A permanent grant makes a temporary trait permanent.
                    owned.isTemporary = false;
                    owned.remainingDuration = 0f;
                }
                else
                {
                    owned.extraGrants++;
                }
            }
            return false;
        }

        if (!ignoreRequirements && !CanAddTrait(trait))
            return false;

        var info = new ActiveTraitInfo
        {
            trait = trait,
            isActive = true,
            isTemporary = isTemporary,
            remainingDuration = duration,
            cooldownRemaining = 0f,
            activatedTime = DateTime.Now,
            pointsSpent = ignoreRequirements ? 0 : trait.cost,
        };
        activeTraits.Add(info);
        infoByTrait[trait] = info;
        activeList.Add(trait);

        ApplyTraitEffects(trait);

        if (!ignoreRequirements)
        {
            availableTraitPoints -= trait.cost;
            OnTraitPointsChanged?.Invoke(availableTraitPoints);
        }

        PlayTraitSound(trait);
        OnTraitAdded?.Invoke(trait);
        OnTraitsChanged?.Invoke();
        if (debugLog)
            Debug.Log($"[Traits] Added {trait.Name}{(info.pointsSpent != 0 ? $" ({info.pointsSpent} points)" : " (free)")}.", this);
        return true;
    }

    /// <summary>
    /// Removes a trait. <paramref name="forceRemove"/> is used by systems (armor, sets, scripts): when other sources
    /// still grant the trait, only one grant is released and the trait stays (returns false). Without it, the
    /// player's own removal is refused while dependencies or equipment still need the trait. Refunds exactly the
    /// points paid for it.
    /// </summary>
    public bool RemoveTrait(Trait trait, bool forceRemove = false)
    {
        if (trait == null || !infoByTrait.TryGetValue(trait, out ActiveTraitInfo info))
            return false;
        if (forceRemove && info.extraGrants > 0)
        {
            info.extraGrants--;
            if (debugLog)
                Debug.Log($"[Traits] {trait.Name}: one grant released, still granted by {info.extraGrants + 1} source(s).", this);
            return false;
        }
        if (!forceRemove && !CanRemoveTrait(trait))
            return false;
        RemoveInternal(info, true);
        return true;
    }

    private void RemoveInternal(ActiveTraitInfo info, bool refund)
    {
        Trait trait = info.trait;
        if (info.isActive)
            RemoveTraitEffects(trait);
        activeTraits.Remove(info);
        infoByTrait.Remove(trait);
        activeList.Remove(trait);

        if (refund && !armorAppliedTraits.Contains(trait))
        {
            availableTraitPoints += info.pointsSpent;
            OnTraitPointsChanged?.Invoke(availableTraitPoints);
        }

        OnTraitRemoved?.Invoke(trait);
        OnTraitsChanged?.Invoke();
        if (debugLog)
            Debug.Log($"[Traits] Removed {trait.Name}{(refund && info.pointsSpent != 0 ? $" (refunded {info.pointsSpent} points)" : "")}.", this);
    }

    public bool HasTrait(Trait trait) => trait != null && infoByTrait.TryGetValue(trait, out ActiveTraitInfo info) && info.isActive;

    /// <summary>
    /// Records that equipment (an item or an armor set) grants <paramref name="trait"/>: the player cannot remove it
    /// while the equipment is worn. Call once per grant, together with <c>AddTrait(trait, true)</c>.
    /// </summary>
    public void RegisterEquipmentGrant(Trait trait)
    {
        if (trait != null)
            armorAppliedTraits.Add(trait);
    }

    /// <summary>Releases one grant recorded by <see cref="RegisterEquipmentGrant"/> (before <c>RemoveTrait(trait, true)</c>).</summary>
    public void UnregisterEquipmentGrant(Trait trait)
    {
        if (trait != null)
            armorAppliedTraits.Remove(trait);
    }

    /// <summary>Is the trait currently granted by worn equipment?</summary>
    public bool IsGrantedByEquipment(Trait trait) => trait != null && armorAppliedTraits.Contains(trait);

    /// <summary>The runtime record of a trait (points paid, grants, temporary time...), or null.</summary>
    public ActiveTraitInfo GetTraitInfo(Trait trait) => trait != null && infoByTrait.TryGetValue(trait, out ActiveTraitInfo info) ? info : null;

    /// <summary>How many sources grant the trait (0 = not owned, 1 = one, 2+ = e.g. picked and also given by armor).</summary>
    public int GetGrantCount(Trait trait) => trait != null && infoByTrait.TryGetValue(trait, out ActiveTraitInfo info) ? 1 + info.extraGrants : 0;

    /// <summary>Why a trait cannot be added right now (null when it can).</summary>
    public string WhyCannotAdd(Trait trait)
    {
        if (trait == null) return "No trait.";
        if (HasTrait(trait)) return "Already has it.";
        if (trait.cost > availableTraitPoints) return $"Needs {trait.cost} points ({availableTraitPoints} left).";
        if (activeTraits.Count >= maxTraits) return $"Already has the maximum of {maxTraits} traits.";
        if (!allowNegativeTraits && trait.IsNegative) return "Negative traits are not allowed.";
        if (trait.HasActiveSkill && PickedActiveTraits() >= maxActiveTraits)
            return maxActiveTraits <= 1 ? "Only one active trait per character." : $"Only {maxActiveTraits} active traits per character.";
        CharacterIdentity identity = Identity;
        string archetypeRule = TraitRules.WhyNot(trait, identity != null ? identity.Archetypes : null);
        if (archetypeRule != null) return archetypeRule;
        foreach (Trait r in trait.requiredTraits)
            if (r != null && !HasTrait(r)) return $"Requires {r.Name}.";
        foreach (Trait i in trait.incompatibleTraits)
            if (i != null && HasTrait(i)) return $"Incompatible with {i.Name}.";
        foreach (Trait x in trait.mutuallyExclusiveTraits)
            if (x != null && HasTrait(x)) return $"Cannot be taken with {x.Name}.";
        foreach (ActiveTraitInfo a in activeTraits)
            if (a.trait != null && (a.trait.incompatibleTraits.Contains(trait) || a.trait.mutuallyExclusiveTraits.Contains(trait)))
                return $"{a.trait.Name} does not allow it.";
        return null;
    }

    public bool CanAddTrait(Trait trait) => WhyCannotAdd(trait) == null;

    /// <summary>How many active-skill traits are allowed per character.</summary>
    public int MaxActiveTraits => maxActiveTraits;

    /// <summary>Active-skill traits the character picked (not those granted by race, class or equipment).</summary>
    public int PickedActiveTraits()
    {
        int n = 0;
        foreach (ActiveTraitInfo info in activeTraits)
            if (info.trait != null && info.trait.HasActiveSkill && !armorAppliedTraits.Contains(info.trait))
                n++;
        return n;
    }

    /// <summary>The character's race / class (null when it has no Character Identity).</summary>
    public CharacterIdentity Identity
    {
        get
        {
            CharacterIdentity id = GetComponentInParent<CharacterIdentity>();
            return id != null ? id : GetComponentInChildren<CharacterIdentity>();
        }
    }

    public bool CanRemoveTrait(Trait trait)
    {
        if (trait == null) return false;
        foreach (ActiveTraitInfo activeInfo in activeTraits)
            if (activeInfo.trait != null && activeInfo.trait.requiredTraits.Contains(trait))
                return false;
        if (armorAppliedTraits.Contains(trait))
            return false;
        // Still granted by something else (armor, a set, the class): it cannot be taken away by the player.
        if (infoByTrait.TryGetValue(trait, out ActiveTraitInfo info) && info.extraGrants > 0)
            return false;
        return true;
    }

    /// <summary>Uses a trait's active part from a script or UI button (e.g. its ability). Returns true if something happened.</summary>
    public bool TryActivate(Trait trait)
    {
        if (trait == null || !runtimes.TryGetValue(trait, out TraitRuntime rt))
            return false;
        bool any = false;
        foreach (TraitBehaviour b in rt.behaviours)
            any |= b.TryActivate(Context);
        return any;
    }

    // ------------------------------------------------------------------ suspend / resume
    /// <summary>
    /// Turns a trait's effects off without removing it: the points paid and the grants are kept. Used by armor sets
    /// that replace a trait for a while (removing and re-adding it used to refund its points for free). Returns true
    /// if it was active.
    /// </summary>
    public bool SuspendTrait(Trait trait)
    {
        if (trait == null || !infoByTrait.TryGetValue(trait, out ActiveTraitInfo info) || !info.isActive)
            return false;
        RemoveTraitEffects(trait);
        info.isActive = false;
        activeList.Remove(trait);
        OnTraitRemoved?.Invoke(trait);
        OnTraitsChanged?.Invoke();
        if (debugLog)
            Debug.Log($"[Traits] Suspended {trait.Name}.", this);
        return true;
    }

    /// <summary>Turns a suspended trait back on. Returns true if it was suspended.</summary>
    public bool ResumeTrait(Trait trait)
    {
        if (trait == null || !infoByTrait.TryGetValue(trait, out ActiveTraitInfo info) || info.isActive)
            return false;
        info.isActive = true;
        activeList.Add(trait);
        ApplyTraitEffects(trait);
        OnTraitAdded?.Invoke(trait);
        OnTraitsChanged?.Invoke();
        if (debugLog)
            Debug.Log($"[Traits] Resumed {trait.Name}.", this);
        return true;
    }

    public bool IsSuspended(Trait trait) => trait != null && infoByTrait.TryGetValue(trait, out ActiveTraitInfo info) && !info.isActive;

    // ------------------------------------------------------------------ applying traits
    private void ApplyTraitEffects(Trait trait)
    {
        if (runtimes.ContainsKey(trait))
            RemoveTraitEffects(trait);
        var rt = new TraitRuntime { trait = trait, multiplier = GetTraitMultiplier(trait) };
        runtimes[trait] = rt;
        rt.passives = ApplyModifiers(trait.modifiers, rt.multiplier, trait.Name);
        rt.legacy = ApplyModifiers(MapLegacy(trait.effects, trait), rt.multiplier, trait.Name + " (legacy)");
        ApplyCombatStats(rt);
        foreach (TraitBehaviour template in trait.behaviours)
        {
            if (template == null)
                continue;
            TraitBehaviour b = template.CreateRuntimeCopy();
            b.multiplier = rt.multiplier;
            rt.behaviours.Add(b);
            rt.templates.Add(template);
            SafeCall(() => b.OnAdded(Context), b);
        }
    }

    private void RemoveTraitEffects(Trait trait)
    {
        if (!runtimes.TryGetValue(trait, out TraitRuntime rt))
            return;
        foreach (TraitBehaviour b in rt.behaviours)
            SafeCall(() => b.OnRemoved(Context), b);
        RevertModifiers(rt.legacy);
        RevertModifiers(rt.passives);
        RevertCombatStats(rt);
        runtimes.Remove(trait);
    }

    /// <summary>
    /// Applies a new strength to a trait in place: its passives are reverted exactly and re-applied, and behaviours
    /// that support it keep their state (an ability's cooldown, jumps used); the others are restarted.
    /// </summary>
    private void ChangeStrength(TraitRuntime rt, float multiplier)
    {
        rt.multiplier = multiplier;
        RevertModifiers(rt.legacy);
        RevertModifiers(rt.passives);
        rt.passives = ApplyModifiers(rt.trait.modifiers, multiplier, rt.trait.Name);
        rt.legacy = ApplyModifiers(MapLegacy(rt.trait.effects, rt.trait), multiplier, rt.trait.Name + " (legacy)");
        RevertCombatStats(rt);
        ApplyCombatStats(rt);
        for (int i = 0; i < rt.behaviours.Count; i++)
        {
            TraitBehaviour b = rt.behaviours[i];
            bool handled = false;
            try { handled = b.OnStrengthChanged(Context, multiplier); }
            catch (Exception e) { Debug.LogException(e, this); }
            if (handled)
                continue;
            SafeCall(() => b.OnRemoved(Context), b);
            TraitBehaviour fresh = rt.templates[i].CreateRuntimeCopy();
            fresh.multiplier = multiplier;
            rt.behaviours[i] = fresh;
            SafeCall(() => fresh.OnAdded(Context), fresh);
        }
    }

    // A trait's combat stats, resistances and scaling go to the character's Combat Stats as one entry (exact removal).
    private void ApplyCombatStats(TraitRuntime rt)
    {
        Trait t = rt.trait;
        if (t == null || !t.HasCombatStats)
            return;
        CombatStats cs = CombatStats.For(this, addIfMissing: true);
        if (cs == null)
            return;
        bool hasStats = (t.combatStats != null && t.combatStats.Count > 0) || (t.resistances != null && t.resistances.Count > 0);
        object stats = hasStats ? cs.AddModifiers(t.Name, t.combatStats, t.resistances, rt.multiplier) : null;
        object scaling = t.scalingRules != null && t.scalingRules.Count > 0 ? cs.AddScaling(t.Name + " (scaling)", t.scalingRules, rt.multiplier) : null;
        rt.combatToken = new object[] { cs, stats, scaling };
    }

    private static void RevertCombatStats(TraitRuntime rt)
    {
        if (rt.combatToken is object[] a && a[0] is CombatStats cs && cs != null)
        {
            if (a[1] != null) cs.RemoveModifiers(a[1]);
            if (a[2] != null) cs.RemoveModifiers(a[2]);
        }
        rt.combatToken = null;
    }

    private List<TraitModifier> MapLegacy(List<TraitEffect> effects, Trait trait)
    {
        var list = new List<TraitModifier>();
        if (effects == null)
            return list;
        foreach (TraitEffect e in effects)
        {
            if (TraitStats.TryMapLegacy(e, out TraitStat stat, out TraitModifierMode mode, out float value))
                list.Add(new TraitModifier(stat, mode, value));
            else if (e != null && debugLog && !TraitStats.IsWeaponLegacyKey(e.targetStat))
                Debug.LogWarning($"[Traits] {trait.Name}: legacy effect '{e.targetStat}' ({e.effectType}) is not supported.", this);
        }
        return list;
    }

    /// <summary>Applies a set of modifiers and returns a handle to revert them exactly later.</summary>
    public AppliedModifiers ApplyModifiers(IList<TraitModifier> mods, float strength, string source)
    {
        var handle = new AppliedModifiers { source = source };
        if (mods == null || mods.Count == 0)
            return handle;
        CheckBaseChanges();
        foreach (TraitModifier m in mods)
        {
            if (m == null)
                continue;
            var am = new AppliedMod { stat = m.stat, mode = m.EffectiveMode, value = m.value * strength };
            if (!TraitStats.IsAggregate(am.stat))
                am.delta = WriteStat(am.stat, am.mode, am.value);
            handle.mods.Add(am);
            if (debugLog)
                Debug.Log($"[Traits] {source}: {m.Describe(strength)} (applied {am.delta:0.##}).", this);
        }
        allApplied.Add(handle);
        RememberResettable();
        aggregatesDirty = true;
        return handle;
    }

    /// <summary>
    /// Undoes exactly what <see cref="ApplyModifiers"/> did. Percent bonuses of the same stats that were stacked on
    /// top of the removed ones are recomputed, so the result never depends on the order traits are removed in.
    /// </summary>
    public void RevertModifiers(AppliedModifiers handle)
    {
        if (handle == null || !allApplied.Remove(handle))
            return;
        CheckBaseChanges();
        for (int i = handle.mods.Count - 1; i >= 0; i--)
        {
            AppliedMod am = handle.mods[i];
            if (!TraitStats.IsAggregate(am.stat))
                AddRaw(am.stat, -am.delta);
        }
        for (int i = 0; i < handle.mods.Count; i++)
        {
            TraitStat stat = handle.mods[i].stat;
            if (!IsPercentOfBase(stat) || IndexOfStat(handle.mods, stat) != i)
                continue;
            float remaining = SumDeltas(stat, out bool anyPercent);
            float now = PercentBase(stat);
            if (anyPercent && !float.IsNaN(now))
                Recompute(stat, now - remaining, remaining);
        }
        handle.mods.Clear();
        RememberResettable();
        aggregatesDirty = true;
    }

    private static int IndexOfStat(List<AppliedMod> mods, TraitStat stat)
    {
        for (int i = 0; i < mods.Count; i++)
            if (mods[i].stat == stat)
                return i;
        return -1;
    }

    /// <summary>Stats whose percent modifiers are a percentage of the stat's own value (not an additive factor).</summary>
    private static bool IsPercentOfBase(TraitStat stat)
    {
        switch (stat)
        {
            case TraitStat.MaxHealth:
            case TraitStat.MaxStamina:
            case TraitStat.MaxMana:
            case TraitStat.CarryWeight:
            case TraitStat.MoveSpeed:
            case TraitStat.SprintSpeed:
            case TraitStat.CrouchSpeed:
            case TraitStat.JumpForce:
                return true;
            default:
                return false;
        }
    }

    // ------------------------------------------------------------------ stat writers
    private StatusManager Manager(TraitStat stat)
    {
        if (playerController == null) return null;
        switch (stat)
        {
            case TraitStat.MaxHealth: case TraitStat.HealthRegen: case TraitStat.HealingReceived: case TraitStat.DamageTaken: return playerController.HpManager;
            case TraitStat.MaxStamina: case TraitStat.StaminaRegen: case TraitStat.StaminaCost: return playerController.StaminaManager;
            case TraitStat.MaxMana: case TraitStat.ManaRegen: return playerController.ManaManager;
            case TraitStat.CarryWeight: return playerController.WeightManager;
            default: return null;
        }
    }

    /// <summary>The value a percent modifier of <paramref name="stat"/> is a percentage of (NaN = not available).</summary>
    private float PercentBase(TraitStat stat)
    {
        switch (stat)
        {
            case TraitStat.MaxHealth:
            case TraitStat.MaxStamina:
            case TraitStat.MaxMana:
            case TraitStat.CarryWeight:
                {
                    StatusManager sm = Manager(stat);
                    return sm != null ? sm.MaxValue : float.NaN;
                }
            case TraitStat.MoveSpeed:
                return playerController != null && playerController.SpeedManager != null ? playerController.SpeedManager.BaseSpeed : float.NaN;
            case TraitStat.SprintSpeed:
                return playerController != null && playerController.SpeedManager != null ? playerController.SpeedManager.SpeedWhileRunningMultiplier : float.NaN;
            case TraitStat.CrouchSpeed:
                return playerController != null && playerController.SpeedManager != null ? playerController.SpeedManager.SpeedWhileCrouchingMultiplier : float.NaN;
            case TraitStat.JumpForce:
                return Context.Movement != null ? Mathf.Abs(Context.Movement.JumpForce) : float.NaN;
            default:
                return float.NaN;
        }
    }

    /// <summary>Writes a modifier into the player's managers and returns the exact amount written.</summary>
    private float WriteStat(TraitStat stat, TraitModifierMode mode, float v)
    {
        switch (stat)
        {
            case TraitStat.MaxHealth:
            case TraitStat.MaxStamina:
            case TraitStat.MaxMana:
            case TraitStat.CarryWeight:
            case TraitStat.MoveSpeed:
            case TraitStat.SprintSpeed:
            case TraitStat.CrouchSpeed:
            case TraitStat.JumpForce:
                {
                    bool pct = mode == TraitModifierMode.Percent || stat == TraitStat.SprintSpeed || stat == TraitStat.CrouchSpeed;
                    float b = PercentBase(stat);
                    if (float.IsNaN(b))
                        return 0f;
                    return AddRaw(stat, pct ? b * v / 100f : v);
                }
            default:
                // Regeneration, healing received, damage taken, stamina cost: plain additive values.
                return AddRaw(stat, v);
        }
    }

    /// <summary>Adds <paramref name="amount"/> to the stat's manager and returns what was actually added.</summary>
    private float AddRaw(TraitStat stat, float amount)
    {
        if (Mathf.Approximately(amount, 0f))
            return 0f;
        StatusManager sm = Manager(stat);
        SpeedManager speed = playerController != null ? playerController.SpeedManager : null;
        PlayerMovementModel move = Context.Movement;
        switch (stat)
        {
            case TraitStat.MaxHealth:
            case TraitStat.MaxStamina:
            case TraitStat.MaxMana:
                if (sm == null) return 0f;
                sm.ModifyMaxValue(amount);
                return amount;
            case TraitStat.CarryWeight:
                if (!(sm is WeightManager wm)) return 0f;
                wm.ModifyMaxWeight(amount);
                return amount;
            case TraitStat.HealthRegen:
            case TraitStat.StaminaRegen:
            case TraitStat.ManaRegen:
                if (sm == null) return 0f;
                sm.ModifyIncrementValue(amount);
                return amount;
            case TraitStat.HealingReceived:
                if (sm == null) return 0f;
                sm.ModifyIncrementFactor(amount);
                return amount;
            case TraitStat.DamageTaken:
            case TraitStat.StaminaCost:
                if (sm == null) return 0f;
                sm.ModifyDecrementFactor(amount);
                return amount;
            case TraitStat.MoveSpeed:
                {
                    if (speed == null) return 0f;
                    float before = speed.BaseSpeed;
                    speed.ModifyBaseSpeed(amount); // clamps at 0: record what really changed
                    return speed.BaseSpeed - before;
                }
            case TraitStat.SprintSpeed:
                if (speed == null) return 0f;
                speed.SpeedWhileRunningMultiplier += amount;
                return amount;
            case TraitStat.CrouchSpeed:
                if (speed == null) return 0f;
                speed.SpeedWhileCrouchingMultiplier += amount;
                return amount;
            case TraitStat.JumpForce:
                if (move == null) return 0f;
                move.JumpForce += amount;
                return amount;
            default:
                return 0f;
        }
    }

    // ------------------------------------------------------------------ base value changes
    // The class system SETS max health/stamina/mana/weight and base speed; level-ups, equipment and buffs ADD to them.
    // These stats are watched: after a class reset the trait bonuses are applied again on top of the class value, and
    // after any other change the percent bonuses are recomputed from the new base (+20% of 150, not of the old 100).
    private static readonly TraitStat[] Resettable = { TraitStat.MaxHealth, TraitStat.MaxStamina, TraitStat.MaxMana, TraitStat.CarryWeight, TraitStat.MoveSpeed };
    private readonly Dictionary<TraitStat, float> expectedValue = new Dictionary<TraitStat, float>();

    private float ClassValue(TraitStat stat)
    {
        PlayerClass c = playerController != null ? playerController.CurrentPlayerClass : null;
        if (c == null) return float.NaN;
        switch (stat)
        {
            case TraitStat.MaxHealth: return c.health;
            case TraitStat.MaxStamina: return c.stamina;
            case TraitStat.MaxMana: return c.mana;
            case TraitStat.MoveSpeed: return c.speed;
            case TraitStat.CarryWeight: return c.weight;
            default: return float.NaN;
        }
    }

    private void RememberResettable()
    {
        foreach (TraitStat s in Resettable)
            expectedValue[s] = PercentBase(s);
    }

    /// <summary>What the traits currently add to <paramref name="stat"/>, and whether any modifier of it is a percentage.</summary>
    private float SumDeltas(TraitStat stat, out bool anyPercent)
    {
        float sum = 0f;
        anyPercent = false;
        foreach (AppliedModifiers h in allApplied)
        {
            foreach (AppliedMod am in h.mods)
            {
                if (am.stat != stat)
                    continue;
                sum += am.delta;
                anyPercent |= am.mode == TraitModifierMode.Percent;
            }
        }
        return sum;
    }

    /// <summary>Detects outside changes of the watched stats and keeps the trait bonuses correct (see above).</summary>
    private void CheckBaseChanges()
    {
        foreach (TraitStat s in Resettable)
        {
            float now = PercentBase(s);
            if (float.IsNaN(now) || !expectedValue.TryGetValue(s, out float expected) || float.IsNaN(expected) || Mathf.Approximately(now, expected))
                continue;
            float traitSum = SumDeltas(s, out bool anyPercent);
            float cls = ClassValue(s);
            bool classReset = !Mathf.Approximately(traitSum, 0f) && !float.IsNaN(cls) && Mathf.Approximately(now, cls);
            if (classReset)
            {
                // The class set the value: the bonuses are gone, apply them again on top of it.
                Recompute(s, now, 0f);
                if (debugLog)
                    Debug.Log($"[Traits] {s} was reset by the class; trait bonuses re-applied.", this);
            }
            else if (anyPercent)
            {
                // Level-up, equipment, buff: percent bonuses follow the new base value.
                Recompute(s, now - traitSum, traitSum);
                if (debugLog)
                    Debug.Log($"[Traits] {s} base changed to {now - traitSum:0.##}; percent bonuses recomputed.", this);
            }
        }
        RememberResettable();
    }

    /// <summary>
    /// Recomputes every modifier of <paramref name="stat"/> on top of <paramref name="baseValue"/> (in the order they
    /// were applied, like when they were added) and writes the difference to what is applied now.
    /// </summary>
    private void Recompute(TraitStat stat, float baseValue, float alreadyApplied)
    {
        float running = baseValue;
        float total = 0f;
        foreach (AppliedModifiers h in allApplied)
        {
            foreach (AppliedMod am in h.mods)
            {
                if (am.stat != stat)
                    continue;
                am.delta = am.mode == TraitModifierMode.Percent ? running * am.value / 100f : am.value;
                running += am.delta;
                total += am.delta;
            }
        }
        AddRaw(stat, total - alreadyApplied);
    }

    private void HandleClassChanged(PlayerClass c) => CheckBaseChanges();
    private void HandleStatsInitialized() => CheckBaseChanges();

    // ------------------------------------------------------------------ aggregate stats
    private void RecomputeAggregates()
    {
        aggregatesDirty = false;
        aggregate.Clear();
        aggregateSum.Clear();
        foreach (AppliedModifiers h in allApplied)
        {
            foreach (AppliedMod am in h.mods)
            {
                if (!TraitStats.IsAggregate(am.stat))
                    continue;
                if (am.mode == TraitModifierMode.Flat)
                {
                    aggregateSum[am.stat] = (aggregateSum.TryGetValue(am.stat, out float sum) ? sum : 0f) + am.value;
                    continue;
                }
                float f = Mathf.Max(0.05f, 1f + am.value / 100f);
                aggregate[am.stat] = (aggregate.TryGetValue(am.stat, out float cur) ? cur : 1f) * f;
            }
        }

        PlayerAbilityController caster = Context.Abilities;
        if (caster != null)
        {
            caster.SetCharacterModifiers(new AbilityModifierSet
            {
                label = "Traits",
                damageMultiplier = GetStatMultiplier(TraitStat.AbilityDamage),
                healMultiplier = GetStatMultiplier(TraitStat.AbilityHealing),
                cooldownMultiplier = GetStatMultiplier(TraitStat.AbilityCooldown),
                castTimeMultiplier = GetStatMultiplier(TraitStat.AbilityCastTime),
                areaMultiplier = GetStatMultiplier(TraitStat.AbilityArea),
                rangeMultiplier = GetStatMultiplier(TraitStat.AbilityRange),
                controlMultiplier = GetStatMultiplier(TraitStat.ControlDealt),
                costMultiplier = GetStatMultiplier(TraitStat.AbilityCost),
                durationMultiplier = GetStatMultiplier(TraitStat.AbilityDuration),
                projectileSpeedMultiplier = GetStatMultiplier(TraitStat.ProjectileSpeed),
                extraCharges = Mathf.RoundToInt(GetStatSum(TraitStat.AbilityCharges)),
                projectileCountDelta = Mathf.RoundToInt(GetStatSum(TraitStat.ExtraProjectiles)),
            });
        }

        CombatEntity e = Context.Entity;
        aggregateEntity = e;
        if (e != null)
        {
            if (baseControlTaken < 0f)
                baseControlTaken = e.controlDurationMultiplier;
            e.controlDurationMultiplier = baseControlTaken * GetStatMultiplier(TraitStat.ControlTaken);
        }
    }

    /// <summary>
    /// The combined multiplier of an aggregate stat (weapon/ability damage, cooldowns, control...): 1 = unchanged,
    /// 1.2 = +20%. Other systems (the weapon attack code) read it.
    /// </summary>
    public float GetStatMultiplier(TraitStat stat)
    {
        if (aggregatesDirty)
            RecomputeAggregates();
        return aggregate.TryGetValue(stat, out float v) ? v : 1f;
    }

    /// <summary>The summed value of a flat aggregate stat (Ability Charges, Extra Projectiles): 0 = unchanged.</summary>
    public float GetStatSum(TraitStat stat)
    {
        if (aggregatesDirty)
            RecomputeAggregates();
        return aggregateSum.TryGetValue(stat, out float v) ? v : 0f;
    }

    /// <summary>Sum of the values of every active modifier of a stat (e.g. total "+%" of Max Health).</summary>
    public float GetStatTotal(TraitStat stat)
    {
        float t = 0f;
        foreach (AppliedModifiers h in allApplied)
            foreach (AppliedMod am in h.mods)
                if (am.stat == stat) t += am.value;
        return t;
    }

    // ------------------------------------------------------------------ old API (armor sets, weapons)
    public void NotifyTraitMultiplierChanged(Trait trait, float multiplier)
    {
        if (trait == null)
            return;
        if (Mathf.Approximately(multiplier, 1f)) traitMultipliers.Remove(trait);
        else traitMultipliers[trait] = multiplier;
        if (runtimes.TryGetValue(trait, out TraitRuntime rt))
        {
            // In place: an ability's cooldown or the jumps used are kept (re-adding the trait used to reset them).
            ChangeStrength(rt, multiplier);
            OnTraitsChanged?.Invoke();
        }
        OnTraitMultiplierChanged?.Invoke(trait, multiplier);
    }

    public void NotifyTraitEffectsAdded(Trait trait, List<TraitEffect> effects)
    {
        OnTraitEffectsAdded?.Invoke(trait, effects);
    }

    /// <summary>Applies one extra old-style effect (armor set "add effects"). Undo with <see cref="RemoveTraitEffect"/>.</summary>
    public void ApplyTraitEffect(TraitEffect effect, Trait trait)
    {
        if (effect == null)
            return;
        if (extraEffects.TryGetValue(effect, out AppliedModifiers old))
            RevertModifiers(old);
        extraEffects[effect] = ApplyModifiers(MapLegacy(new List<TraitEffect> { effect }, trait), GetTraitMultiplier(trait), (trait != null ? trait.Name : "?") + " (extra)");
    }

    public void RemoveTraitEffect(TraitEffect effect, Trait trait)
    {
        if (effect != null && extraEffects.TryGetValue(effect, out AppliedModifiers applied))
        {
            RevertModifiers(applied);
            extraEffects.Remove(effect);
        }
    }

    public float GetTraitMultiplier(Trait trait)
    {
        return trait != null && traitMultipliers.TryGetValue(trait, out float multiplier) ? multiplier : 1f;
    }

    // ------------------------------------------------------------------ temporary & starting traits
    private void UpdateTemporaryTraits(float dt)
    {
        expiredBuffer.Clear();
        foreach (ActiveTraitInfo traitInfo in activeTraits)
        {
            if (!traitInfo.isTemporary || traitInfo.remainingDuration <= 0f)
                continue;
            traitInfo.remainingDuration -= dt;
            if (traitInfo.remainingDuration <= 0f)
                expiredBuffer.Add(traitInfo);
        }
        for (int i = 0; i < expiredBuffer.Count; i++)
        {
            ActiveTraitInfo info = expiredBuffer[i];
            if (!infoByTrait.ContainsKey(info.trait))
                continue;
            OnTraitExpired?.Invoke(info.trait);
            if (infoByTrait.TryGetValue(info.trait, out ActiveTraitInfo still) && still == info && info.isTemporary)
                RemoveInternal(info, true);
        }
        expiredBuffer.Clear();
    }

    private void ApplyStartingTraits()
    {
        if (startingApplied)
            return;
        startingApplied = true;
        foreach (Trait trait in startingTraits)
            if (trait != null)
                AddTrait(trait, true);
    }

    public List<Trait> GetAvailableTraits()
    {
        if (traitDatabase == null) return new List<Trait>();
        return traitDatabase.GetAllTraits().Where(t => t != null && t.cost <= availableTraitPoints).Where(CanAddTrait).ToList();
    }

    public void AddTraitPoints(int points)
    {
        availableTraitPoints += points;
        OnTraitPointsChanged?.Invoke(availableTraitPoints);
    }

    public void SetTraitPoints(int points)
    {
        availableTraitPoints = points;
        OnTraitPointsChanged?.Invoke(availableTraitPoints);
    }

    public bool AddTemporaryTrait(Trait trait, float duration) => AddTrait(trait, true, true, duration);

    private void PlayTraitSound(Trait trait)
    {
        if (audioSource != null && trait.acquisitionSound != null)
            audioSource.PlayOneShot(trait.acquisitionSound);
    }

    public int GetTotalTraitCost()
    {
        int total = 0;
        for (int i = 0; i < activeList.Count; i++)
            total += activeList[i].cost;
        return total;
    }

    public List<Trait> GetPositiveTraits() => activeList.Where(t => t.IsPositive).ToList();
    public List<Trait> GetNegativeTraits() => activeList.Where(t => t.IsNegative).ToList();

    /// <summary>Removes every trait (suspended ones and extra grants included) and refunds the points paid.</summary>
    public void ClearAllTraits()
    {
        foreach (ActiveTraitInfo info in activeTraits.ToList())
        {
            info.extraGrants = 0;
            if (infoByTrait.ContainsKey(info.trait))
                RemoveInternal(info, true);
        }
    }

    // ------------------------------------------------------------------ inspector helpers
    /// <summary>What a trait is doing right now: its applied modifiers and its behaviours' status (inspector).</summary>
    public List<string> DescribeLive(Trait trait)
    {
        var lines = new List<string>();
        if (trait == null)
            return lines;
        if (infoByTrait.TryGetValue(trait, out ActiveTraitInfo info))
        {
            if (!info.isActive)
                lines.Add("Suspended (replaced by an armor set)");
            if (info.pointsSpent != 0)
                lines.Add($"Paid {info.pointsSpent} point(s)");
            if (info.extraGrants > 0)
                lines.Add($"Also granted by {info.extraGrants} other source(s)");
        }
        if (!runtimes.TryGetValue(trait, out TraitRuntime rt))
            return lines;
        foreach (AppliedModifiers h in new[] { rt.passives, rt.legacy })
        {
            if (h == null) continue;
            foreach (AppliedMod am in h.mods)
            {
                var m = new TraitModifier(am.stat, am.mode, am.value);
                lines.Add(TraitStats.IsAggregate(am.stat) ? m.Describe() : $"{m.Describe()}  (applied {am.delta:+0.##;-0.##})");
            }
        }
        foreach (TraitBehaviour b in rt.behaviours)
            lines.Add($"{b.MenuName}: {b.LiveStatus ?? "running"}");
        if (!Mathf.Approximately(rt.multiplier, 1f))
            lines.Add($"Strength x{rt.multiplier:0.##} (armor set)");
        return lines;
    }

    private void SafeCall(Action a, TraitBehaviour b)
    {
        try { a(); }
        catch (Exception e) { Debug.LogException(e, this); }
    }

    // ------------------------------------------------------------------ armor validation
    public List<string> ValidateTraitCompatibilityWithArmorSets()
    {
        var issues = new List<string>();
        if (armorSetManager == null) return issues;

        var activeSets = armorSetManager.GetActiveSets();
        var activeTraitsList = ActiveTraits;
        foreach (var armorSet in activeSets)
        {
            var activeEffects = armorSetManager.GetActiveSetEffects(armorSet);
            foreach (var effect in activeEffects)
            {
                foreach (var enhancement in effect.traitEnhancements)
                {
                    if (enhancement.originalTrait == null) continue;
                    bool hasOriginalTrait = activeTraitsList.Contains(enhancement.originalTrait);
                    if (enhancement.enhancementType == TraitEnhancementType.Replace && !hasOriginalTrait)
                        issues.Add($"Set {armorSet.SetName} wants to replace trait {enhancement.originalTrait.Name} but player doesn't have it");
                    if (enhancement.enhancementType == TraitEnhancementType.Upgrade && enhancement.enhancedTrait == null)
                        issues.Add($"Set {armorSet.SetName} enhancement for {enhancement.originalTrait.Name} has no enhanced trait specified");
                }
                foreach (var newTrait in effect.traitsToApply)
                {
                    if (newTrait == null) continue;
                    foreach (var activeTrait in activeTraitsList)
                    {
                        if (newTrait.incompatibleTraits.Contains(activeTrait))
                            issues.Add($"Set trait {newTrait.Name} is incompatible with active trait {activeTrait.Name}");
                        if (newTrait.mutuallyExclusiveTraits.Contains(activeTrait))
                            issues.Add($"Set trait {newTrait.Name} is mutually exclusive with active trait {activeTrait.Name}");
                    }
                }
            }
        }
        return issues;
    }

    // ------------------------------------------------------------------ debug
    [ContextMenu("Add 5 Trait Points")]
    private void DebugAddTraitPoints() => AddTraitPoints(5);

    [ContextMenu("Log Active Traits")]
    private void DebugLogActiveTraits()
    {
        Debug.Log("=== Active Traits ===");
        foreach (var trait in ActiveTraits)
        {
            Debug.Log($"- {trait.Name} (Cost: {trait.cost})");
            foreach (string line in DescribeLive(trait))
                Debug.Log("    " + line);
        }
        Debug.Log($"Total Cost: {GetTotalTraitCost()}  Available Points: {availableTraitPoints}");
    }

    [ContextMenu("Validate Trait Compatibility")]
    private void DebugValidateCompatibility()
    {
        var issues = ValidateTraitCompatibilityWithArmorSets();
        if (issues.Count == 0)
            Debug.Log("No trait compatibility issues found!");
        else
            foreach (var issue in issues) Debug.LogWarning(issue);
    }
}
