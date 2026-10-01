using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Turns armor set bonuses on and off. After every equipment change it counts, for each set, the DIFFERENT pieces
/// the character wears (from the <see cref="EquipmentManager"/>), works out which bonus tiers are reached (2/4, 3/4,
/// 4/4...; tiers of the same Upgrade Group replace each other) and applies only the tiers that became active and
/// removes only those that stopped - exactly, through effect handles. Nothing is added or subtracted by hand, so
/// swapping pieces in any order can never stack a bonus twice or leave one behind.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(61)]
public class ArmorSetManager : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("Found automatically when empty.")]
    [SerializeField] private PlayerStatusController playerStatusController;
    [Tooltip("Found automatically when empty.")]
    [SerializeField] private TraitManager traitManager;
    [Tooltip("Optional (reports and tools). Found automatically when empty.")]
    [SerializeField] private InventoryManager inventoryManager;
    [Tooltip("Plays the set sounds. Empty = the AudioSource on the character.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Set Management")]
    [Tooltip("The sets the character currently wears pieces of (runtime view).")]
    [SerializeField] private List<ArmorSetTracker> trackedSets = new List<ArmorSetTracker>();

    [Header("Audio")]
    [Tooltip("Played when a set becomes complete.")]
    [SerializeField] private AudioClip setActivatedSound;
    [Tooltip("Played when a complete set is broken.")]
    [SerializeField] private AudioClip setDeactivatedSound;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = false;

    // Events
    public event Action<ArmorSet, int> OnSetPiecesChanged;
    public event Action<ArmorSet, bool> OnSetCompleted;
    public event Action<ArmorSet> OnSetBroken;
    public event Action<ArmorSetEffect> OnSetEffectActivated;
    public event Action<ArmorSetEffect> OnSetEffectDeactivated;
    public event Action<SpecialMechanic> OnSpecialMechanicActivated;
    public event Action<SpecialMechanic> OnSpecialMechanicDeactivated;
    /// <summary>A bonus tier of a set turned on (true) or off (false).</summary>
    public event Action<ArmorSet, ArmorSetEffect, bool> SetBonusChanged;
    /// <summary>Raised once after any set changed (pieces, tiers, completion).</summary>
    public event Action SetsChanged;

    private sealed class ActiveTier
    {
        public ArmorSet set;
        public ArmorSetEffect effect;
        public readonly List<EquipmentEffectHandle> handles = new List<EquipmentEffectHandle>();
        public float removeAt = float.PositiveInfinity; // lingering (Persist Duration) when finite
    }

    private sealed class SetState
    {
        public ArmorSet set;
        public int count;
        public bool complete;
        public readonly List<EquipmentEffectHandle> visuals = new List<EquipmentEffectHandle>();
        public bool visualsOn;
        public bool completeVisualOn;
        public readonly List<EquipmentEffectHandle> completeVisual = new List<EquipmentEffectHandle>();
    }

    private readonly Dictionary<ArmorSetEffect, ActiveTier> activeTiers = new Dictionary<ArmorSetEffect, ActiveTier>(ReferenceComparer<ArmorSetEffect>.Instance);
    private readonly Dictionary<ArmorSet, SetState> states = new Dictionary<ArmorSet, SetState>(ReferenceComparer<ArmorSet>.Instance);
    private readonly List<ItemSO> worn = new List<ItemSO>();
    private readonly List<ArmorSetEffect> desiredBuffer = new List<ArmorSetEffect>();
    private readonly HashSet<ArmorSetEffect> desired = new HashSet<ArmorSetEffect>(ReferenceComparer<ArmorSetEffect>.Instance);
    private readonly List<ArmorSet> setBuffer = new List<ArmorSet>();
    private readonly List<ActiveTier> tierBuffer = new List<ActiveTier>();
    private EquipmentManager equipment;
    private bool subscribed;
    private bool refreshing;

    public EquipmentManager Equipment => equipment;

    private void Awake()
    {
        if (playerStatusController == null)
            playerStatusController = GetComponentInParent<PlayerStatusController>();
        if (traitManager == null)
            traitManager = playerStatusController != null && playerStatusController.TraitManager != null ? playerStatusController.TraitManager : GetComponent<TraitManager>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable() => Subscribe();

    private void Start()
    {
        Subscribe();
        if (inventoryManager == null && equipment != null)
            inventoryManager = equipment.Inventory;
    }

    private void OnDisable()
    {
        if (equipment != null && subscribed)
        {
            equipment.Changed -= Refresh;
            equipment.Context.Mechanics.Toggled -= HandleMechanicToggled;
        }
        subscribed = false;
    }

    private void OnDestroy()
    {
        foreach (ActiveTier t in activeTiers.Values)
            EquipmentEffect.RevertAll(t.handles);
        activeTiers.Clear();
        foreach (SetState s in states.Values)
        {
            EquipmentEffect.RevertAll(s.visuals);
            EquipmentEffect.RevertAll(s.completeVisual);
        }
        states.Clear();
    }

    private void Subscribe()
    {
        if (subscribed)
            return;
        if (equipment == null)
            equipment = EquipmentManager.For(this);
        if (equipment == null)
            return;
        equipment.Changed += Refresh;
        equipment.Context.Mechanics.Toggled += HandleMechanicToggled;
        subscribed = true;
        if (equipment.IsInitialized)
            Refresh();
    }

    private void HandleMechanicToggled(SpecialMechanic m, bool on)
    {
        if (on) OnSpecialMechanicActivated?.Invoke(m);
        else OnSpecialMechanicDeactivated?.Invoke(m);
    }

    private void Update()
    {
        if (activeTiers.Count == 0)
            return;
        float now = Time.time;
        float dt = Time.deltaTime;
        tierBuffer.Clear();
        tierBuffer.AddRange(activeTiers.Values);
        for (int i = 0; i < tierBuffer.Count; i++)
        {
            ActiveTier t = tierBuffer[i];
            if (now >= t.removeAt)
            {
                Deactivate(t);
                SetsChanged?.Invoke();
                continue;
            }
            List<EquipmentEffectHandle> hs = t.handles;
            for (int h = 0; h < hs.Count; h++)
            {
                if (!hs[h].NeedsTick)
                    continue;
                try { hs[h].Tick(dt); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }
        tierBuffer.Clear();
    }

    // ------------------------------------------------------------------ reconcile
    /// <summary>Recounts the worn pieces and applies/removes set bonus tiers to match. Safe to call any time.</summary>
    public void Refresh()
    {
        if (refreshing)
            return;
        if (equipment == null)
        {
            Subscribe();
            if (equipment == null)
                return;
        }
        refreshing = true;
        try
        {
            worn.Clear();
            equipment.GetEquippedItems(worn);

            // Sets worn now, plus sets that were worn before (so they can be switched off).
            setBuffer.Clear();
            foreach (ItemSO item in worn)
                if (item is ArmorSO a && a.BelongsToSet != null && a.BelongsToSet.ContainsPiece(a) && !setBuffer.Contains(a.BelongsToSet))
                    setBuffer.Add(a.BelongsToSet);
            foreach (ArmorSet s in states.Keys)
                if (!setBuffer.Contains(s))
                    setBuffer.Add(s);

            desired.Clear();
            bool anyChange = false;
            foreach (ArmorSet set in setBuffer)
            {
                int count = set.CountPieces(worn);
                desiredBuffer.Clear();
                set.GetActiveEffects(count, desiredBuffer);
                foreach (ArmorSetEffect e in desiredBuffer)
                    desired.Add(e);
                anyChange |= UpdateSetState(set, count);
            }

            // Tiers no longer reached: removed now, or after their Persist Duration.
            tierBuffer.Clear();
            tierBuffer.AddRange(activeTiers.Values);
            foreach (ActiveTier t in tierBuffer)
            {
                if (desired.Contains(t.effect))
                {
                    t.removeAt = float.PositiveInfinity; // back in time: keep it
                    continue;
                }
                if (t.effect.persistDuration > 0f)
                {
                    if (float.IsPositiveInfinity(t.removeAt))
                        t.removeAt = Time.time + t.effect.persistDuration;
                }
                else
                {
                    Deactivate(t);
                    anyChange = true;
                }
            }
            tierBuffer.Clear();

            // Newly reached tiers.
            foreach (ArmorSet set in setBuffer)
            {
                if (set.SetEffects == null)
                    continue;
                foreach (ArmorSetEffect e in set.SetEffects)
                {
                    if (e == null || !desired.Contains(e) || activeTiers.ContainsKey(e))
                        continue;
                    Activate(set, e);
                    anyChange = true;
                }
            }

            foreach (ArmorSet set in setBuffer)
                UpdateSetVisuals(set);

            RebuildTrackers();
            if (anyChange)
                SetsChanged?.Invoke();
        }
        finally
        {
            refreshing = false;
        }
    }

    private bool UpdateSetState(ArmorSet set, int count)
    {
        if (!states.TryGetValue(set, out SetState st))
        {
            if (count == 0)
                return false;
            states[set] = st = new SetState { set = set };
        }
        int previous = st.count;
        bool wasComplete = st.complete;
        st.count = count;
        st.complete = count > 0 && set.IsSetComplete(count);
        if (previous == count && wasComplete == st.complete)
            return false;

        LogDebug($"{set.SetName}: {previous} -> {count} piece(s)");
        OnSetPiecesChanged?.Invoke(set, count);
        if (wasComplete != st.complete)
        {
            OnSetCompleted?.Invoke(set, st.complete);
            if (!st.complete)
                OnSetBroken?.Invoke(set);
            if (equipment.IsInitialized)
                PlaySetSound(st.complete, set);
        }
        return true;
    }

    private void UpdateSetVisuals(ArmorSet set)
    {
        if (!states.TryGetValue(set, out SetState st))
            return;
        bool anyTier = false;
        foreach (ActiveTier t in activeTiers.Values)
            if (t.set == set) { anyTier = true; break; }

        // Each worn piece's "Set Visual Effect" while a bonus of its set is active.
        if (anyTier != st.visualsOn)
        {
            EquipmentEffect.RevertAll(st.visuals);
            st.visualsOn = anyTier;
            if (anyTier)
            {
                var fx = new List<EquipmentEffect>();
                foreach (ItemSO item in worn)
                    if (item is ArmorSO a && set.ContainsPiece(a) && a.SetVisualEffect != null)
                        fx.Add(new AttachedVisualEffect(a.SetVisualEffect));
                EquipmentEffect.ApplyAll(fx, equipment.Context, 1f, set.SetName, st.visuals);
            }
        }
        if (st.complete != st.completeVisualOn)
        {
            EquipmentEffect.RevertAll(st.completeVisual);
            st.completeVisualOn = st.complete;
            if (st.complete && set.SetCompleteEffect != null)
                EquipmentEffect.ApplyAll(new List<EquipmentEffect> { new AttachedVisualEffect(set.SetCompleteEffect) }, equipment.Context, 1f, set.SetName, st.completeVisual);
        }

        if (st.count == 0 && !anyTier)
        {
            EquipmentEffect.RevertAll(st.visuals);
            EquipmentEffect.RevertAll(st.completeVisual);
            states.Remove(set);
        }
    }

    private void Activate(ArmorSet set, ArmorSetEffect effect)
    {
        var tier = new ActiveTier { set = set, effect = effect };
        activeTiers[effect] = tier;
        var effects = new List<EquipmentEffect>();
        try { effect.CollectEffects(effects); }
        catch (Exception e) { Debug.LogException(e, set); }
        EquipmentEffect.ApplyAll(effects, equipment.Context, 1f, $"{set.SetName} ({effect.piecesRequired}) {effect.effectName}", tier.handles);

        if (equipment.IsInitialized)
        {
            if (effect.setActivationSound != null)
                equipment.Context.PlayOneShot(effect.setActivationSound);
            if (effect.setActivationParticles != null && equipment.Context.Body != null)
            {
                ParticleSystem ps = Instantiate(effect.setActivationParticles, equipment.Context.Body.position, Quaternion.identity);
                ps.Play();
                var main = ps.main;
                Destroy(ps.gameObject, main.duration + main.startLifetime.constantMax + 0.5f);
            }
        }
        LogDebug($"Activated {set.SetName} bonus '{effect.effectName}' ({tier.handles.Count} effect(s))");
        OnSetEffectActivated?.Invoke(effect);
        SetBonusChanged?.Invoke(set, effect, true);
    }

    private void Deactivate(ActiveTier tier)
    {
        if (!activeTiers.Remove(tier.effect))
            return;
        EquipmentEffect.RevertAll(tier.handles);
        LogDebug($"Deactivated {tier.set.SetName} bonus '{tier.effect.effectName}'");
        OnSetEffectDeactivated?.Invoke(tier.effect);
        SetBonusChanged?.Invoke(tier.set, tier.effect, false);
        UpdateSetVisuals(tier.set);
        RebuildTrackers();
    }

    private void RebuildTrackers()
    {
        trackedSets.Clear();
        foreach (SetState st in states.Values)
        {
            var tracker = new ArmorSetTracker { armorSet = st.set };
            foreach (ItemSO item in worn)
                if (item is ArmorSO a && st.set.ContainsPiece(a))
                    tracker.AddPiece(a);
            foreach (ActiveTier t in activeTiers.Values)
                if (t.set == st.set)
                    tracker.activeEffects.Add(t.effect);
            trackedSets.Add(tracker);
        }
    }

    // ------------------------------------------------------------------ public API
    /// <summary>Old notification from armor pieces: the counts now come from the equipment manager, so this only refreshes.</summary>
    public void OnArmorEquipmentChanged(ArmorSO armor, bool equipped) => Refresh();

    /// <summary>Sets the character wears at least one piece of.</summary>
    public List<ArmorSet> GetActiveSets()
    {
        return states.Values.Where(s => s.count > 0).Select(s => s.set).ToList();
    }

    public int GetEquippedPiecesCount(ArmorSet armorSet)
    {
        return armorSet != null && states.TryGetValue(armorSet, out SetState st) ? st.count : 0;
    }

    public bool IsSetComplete(ArmorSet armorSet)
    {
        return armorSet != null && states.TryGetValue(armorSet, out SetState st) && st.complete;
    }

    /// <summary>The bonus tiers of a set that are active right now (including ones lingering for their Persist Duration).</summary>
    public List<ArmorSetEffect> GetActiveSetEffects(ArmorSet armorSet)
    {
        var list = new List<ArmorSetEffect>();
        foreach (ActiveTier t in activeTiers.Values)
            if (t.set == armorSet)
                list.Add(t.effect);
        return list.OrderBy(e => e.piecesRequired).ToList();
    }

    public bool IsBonusActive(ArmorSetEffect effect) => effect != null && activeTiers.ContainsKey(effect);

    public bool HasSpecialMechanic(string mechanicId) => equipment != null && equipment.Context.Mechanics.IsActive(mechanicId);

    public List<string> GetActiveSpecialMechanics() => equipment != null ? equipment.Context.Mechanics.ActiveIds.ToList() : new List<string>();

    /// <summary>Recounts everything (kept for older callers; equipment changes refresh automatically).</summary>
    public void ScanEquippedArmor()
    {
        if (equipment != null && equipment.IsInitialized)
            equipment.SyncFromInventory();
        Refresh();
    }

    // Get a status report of all armor sets
    public string GetSetStatusReport()
    {
        var report = new StringBuilder();
        report.AppendLine("=== Armor Set Status Report ===");

        if (states.Count == 0)
        {
            report.AppendLine("No armor sets currently worn.");
            return report.ToString();
        }

        foreach (SetState st in states.Values)
        {
            report.AppendLine($"\n{st.set.SetName}:");
            report.AppendLine($"  Pieces Equipped: {st.count}/{st.set.SetPieces.Count}");
            report.AppendLine($"  Is Complete: {st.complete}");
            foreach (string line in st.set.DescribeTiers(st.count))
                report.AppendLine("  " + line);
        }

        var mechanics = GetActiveSpecialMechanics();
        if (mechanics.Count > 0)
        {
            report.AppendLine("\nActive Special Mechanics:");
            foreach (var mechanic in mechanics)
                report.AppendLine($"  - {mechanic}");
        }

        return report.ToString();
    }

    private void PlaySetSound(bool activated, ArmorSet set)
    {
        AudioClip clip = activated ? (set.SetCompleteSound != null ? set.SetCompleteSound : setActivatedSound) : setDeactivatedSound;
        if (clip == null)
            return;
        if (audioSource != null)
            audioSource.PlayOneShot(clip);
        else
            equipment?.Context.PlayOneShot(clip);
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogging)
            Debug.Log($"[ArmorSetManager] {message}", this);
    }

    [ContextMenu("Log Set Status")]
    private void DebugLogStatus() => Debug.Log(GetSetStatusReport(), this);
}
