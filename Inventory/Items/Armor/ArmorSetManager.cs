using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;


public class ArmorSetManager : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private PlayerStatusController playerStatusController;
    [SerializeField] private TraitManager traitManager;
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private AudioSource audioSource;

    [Header("Set Management")]
    [SerializeField] private List<ArmorSetTracker> trackedSets = new List<ArmorSetTracker>();
    [SerializeField] private Dictionary<ArmorSet, ArmorSetTracker> setTrackers = new Dictionary<ArmorSet, ArmorSetTracker>();

    [Header("Enhanced Trait System")]
    [SerializeField] private Dictionary<Trait, float> traitMultipliers = new Dictionary<Trait, float>();
    [SerializeField] private List<Trait> temporarySetTraits = new List<Trait>();

    [Header("Active Mechanics")]
    [SerializeField] private Dictionary<string, bool> activeMechanics = new Dictionary<string, bool>();

    [Header("Audio")]
    [SerializeField] private AudioClip setActivatedSound;
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

    private void Awake()
    {
        if (playerStatusController == null)
            playerStatusController = GetComponent<PlayerStatusController>();

        if (traitManager == null)
            traitManager = GetComponent<TraitManager>();
    }

    private void Start()
    {
        // Initial scan for equipped armor
        ScanForEquippedArmor();
    }

    public void OnArmorEquipmentChanged(ArmorSO armor, bool equipped)
    {
        if (armor == null || !armor.IsPartOfSet()) return;

        var tracker = GetOrCreateSetTracker(armor.BelongsToSet);
        int previousCount = tracker.equippedCount;

        if (equipped)
        {
            tracker.AddPiece(armor);
        }
        else
        {
            tracker.RemovePiece(armor);
        }

        // Check if equipped count changed
        if (tracker.equippedCount != previousCount)
        {
            HandleSetChange(tracker, previousCount);
        }
    }

    private ArmorSetTracker GetOrCreateSetTracker(ArmorSet armorSet)
    {
        if (!setTrackers.TryGetValue(armorSet, out ArmorSetTracker tracker))
        {
            tracker = new ArmorSetTracker { armorSet = armorSet };
            setTrackers[armorSet] = tracker;
            trackedSets.Add(tracker);
        }
        return tracker;
    }

    private void HandleSetChange(ArmorSetTracker tracker, int previousCount)
    {
        tracker.UpdateActiveEffects();

        // Deactivate effects that no longer meet requirements
        var previousEffects = tracker.armorSet.SetEffects
            .Where(e => e.ShouldBeActive(previousCount))
            .ToList();

        var currentEffects = tracker.activeEffects;

        // Remove effects that are no longer active
        foreach (var effect in previousEffects)
        {
            if (!currentEffects.Contains(effect))
            {
                DeactivateSetEffect(effect);
            }
        }

        // Activate new effects
        foreach (var effect in currentEffects)
        {
            if (!previousEffects.Contains(effect))
            {
                ActivateSetEffect(effect);
            }
        }

        // Fire events
        OnSetPiecesChanged?.Invoke(tracker.armorSet, tracker.equippedCount);

        // Check for set completion
        bool wasComplete = previousCount >= GetRequiredPiecesForFullSet(tracker.armorSet);
        bool isComplete = tracker.isSetComplete;

        if (wasComplete != isComplete)
        {
            OnSetCompleted?.Invoke(tracker.armorSet, isComplete);
            if (!isComplete)
            {
                OnSetBroken?.Invoke(tracker.armorSet);
            }
            PlaySetSound(isComplete);
        }
    }

    private void ActivateSetEffect(ArmorSetEffect effect)
    {
        LogDebug($"Activating set effect: {effect.effectName}");

        // Apply new traits
        ApplySetTraits(effect);

        // Apply trait enhancements
        ApplyTraitEnhancements(effect);

        // Apply stat bonuses
        ApplyStatBonuses(effect);

        // Apply special mechanics
        ApplySpecialMechanics(effect);

        OnSetEffectActivated?.Invoke(effect);
    }

    private void DeactivateSetEffect(ArmorSetEffect effect)
    {
        LogDebug($"Deactivating set effect: {effect.effectName}");

        // Remove special mechanics first
        RemoveSpecialMechanics(effect);

        // Remove stat bonuses
        RemoveStatBonuses(effect);

        // Remove trait enhancements
        RemoveTraitEnhancements(effect);

        // Remove set traits
        RemoveSetTraits(effect);

        OnSetEffectDeactivated?.Invoke(effect);
    }

    private void ApplySetTraits(ArmorSetEffect effect)
    {
        if (traitManager == null) return;

        foreach (var trait in effect.traitsToApply)
        {
            if (trait != null)
            {
                traitManager.AddTrait(trait, true);
                temporarySetTraits.Add(trait);
                LogDebug($"Applied set trait: {trait.Name}");
            }
        }
    }

    private void RemoveSetTraits(ArmorSetEffect effect)
    {
        if (traitManager == null) return;

        foreach (var trait in effect.traitsToApply)
        {
            if (trait != null)
            {
                traitManager.RemoveTrait(trait, true);
                temporarySetTraits.Remove(trait);
                LogDebug($"Removed set trait: {trait.Name}");
            }
        }
    }

    private void ApplyTraitEnhancements(ArmorSetEffect effect)
    {
        if (traitManager == null) return;

        foreach (var enhancement in effect.traitEnhancements)
        {
            if (enhancement.originalTrait == null) continue;

            if (!traitManager.HasTrait(enhancement.originalTrait)) continue;

            switch (enhancement.enhancementType)
            {
                case TraitEnhancementType.Multiply:
                    ApplyTraitMultiplier(enhancement);
                    break;

                case TraitEnhancementType.AddEffects:
                    ApplyAdditionalTraitEffects(enhancement);
                    break;

                case TraitEnhancementType.Replace:
                    ReplaceTraitTemporarily(enhancement);
                    break;

                case TraitEnhancementType.Upgrade:
                    UpgradeToEnhancedTrait(enhancement);
                    break;
            }

            LogDebug($"Applied trait enhancement: {enhancement.originalTrait.Name} -> {enhancement.enhancementType}");
        }
    }

    private void RemoveTraitEnhancements(ArmorSetEffect effect)
    {
        if (traitManager == null) return;

        foreach (var enhancement in effect.traitEnhancements)
        {
            if (enhancement.originalTrait == null) continue;

            switch (enhancement.enhancementType)
            {
                case TraitEnhancementType.Multiply:
                    RemoveTraitMultiplier(enhancement);
                    break;

                case TraitEnhancementType.AddEffects:
                    RemoveAdditionalTraitEffects(enhancement);
                    break;

                case TraitEnhancementType.Replace:
                    RestoreOriginalTrait(enhancement);
                    break;

                case TraitEnhancementType.Upgrade:
                    RevertFromEnhancedTrait(enhancement);
                    break;
            }
        }
    }

    private void ApplyTraitMultiplier(TraitEnhancement enhancement)
    {
        traitMultipliers[enhancement.originalTrait] = enhancement.effectMultiplier;
        // Notify TraitManager of the multiplier change
        if (traitManager != null)
        {
            traitManager.NotifyTraitMultiplierChanged(enhancement.originalTrait, enhancement.effectMultiplier);
        }
    }

    private void RemoveTraitMultiplier(TraitEnhancement enhancement)
    {
        traitMultipliers.Remove(enhancement.originalTrait);
        if (traitManager != null)
        {
            traitManager.NotifyTraitMultiplierChanged(enhancement.originalTrait, 1f);
        }
    }

    private void ApplyAdditionalTraitEffects(TraitEnhancement enhancement)
    {
        // Apply additional effects through the trait itself
        foreach (var effect in enhancement.additionalEffects)
        {
            if (traitManager != null && playerStatusController != null)
            {
                // Apply the effect directly through TraitManager
                var traitInfo = traitManager.ActiveTraitInfos.FirstOrDefault(t => t.trait == enhancement.originalTrait);
                if (traitInfo != null)
                {
                    // Apply the additional effect
                    traitManager.ApplyTraitEffect(effect, enhancement.originalTrait);
                }
            }
        }

        if (traitManager != null)
        {
            traitManager.NotifyTraitEffectsAdded(enhancement.originalTrait, enhancement.additionalEffects);
        }
    }

    private void RemoveAdditionalTraitEffects(TraitEnhancement enhancement)
    {
        foreach (var effect in enhancement.additionalEffects)
        {
            if (traitManager != null && playerStatusController != null)
            {
                // Use the dedicated removal method
                traitManager.RemoveTraitEffect(effect, enhancement.originalTrait);
            }
        }

        Debug.Log($"Removed {enhancement.additionalEffects.Count} additional effects from trait: {enhancement.originalTrait.Name}");
    }
    private void ReplaceTraitTemporarily(TraitEnhancement enhancement)
    {
        traitManager.RemoveTrait(enhancement.originalTrait, true);
        if (enhancement.enhancedTrait != null)
        {
            traitManager.AddTrait(enhancement.enhancedTrait, true);
            temporarySetTraits.Add(enhancement.enhancedTrait);
        }
    }

    private void RestoreOriginalTrait(TraitEnhancement enhancement)
    {
        if (enhancement.enhancedTrait != null)
        {
            traitManager.RemoveTrait(enhancement.enhancedTrait, true);
            temporarySetTraits.Remove(enhancement.enhancedTrait);
        }
        traitManager.AddTrait(enhancement.originalTrait, true);
    }

    private void UpgradeToEnhancedTrait(TraitEnhancement enhancement)
    {
        if (enhancement.enhancedTrait == null) return;
        ReplaceTraitTemporarily(enhancement);
    }

    private void RevertFromEnhancedTrait(TraitEnhancement enhancement)
    {
        RestoreOriginalTrait(enhancement);
    }

    private void ApplyStatBonuses(ArmorSetEffect effect)
    {
        // Use the EquippableSO system for stat bonuses
        foreach (var statBonus in effect.statBonuses)
        {
            ApplyStatBonus(statBonus, true);
        }
    }

    private void RemoveStatBonuses(ArmorSetEffect effect)
    {
        foreach (var statBonus in effect.statBonuses)
        {
            ApplyStatBonus(statBonus, false);
        }
    }

    private void ApplyStatBonus(EquippableEffect statBonus, bool isApplying)
    {
        // Create a temporary EquippableSO to use its effect system
        var tempEquippable = ScriptableObject.CreateInstance<EquippableSO>();
        tempEquippable.Effects.Add(statBonus);
        tempEquippable.ApplyEquippedStats(isApplying, playerStatusController);
        DestroyImmediate(tempEquippable);
    }

    private void ApplySpecialMechanics(ArmorSetEffect effect)
    {
        foreach (var mechanic in effect.specialMechanics)
        {
            ApplySpecialMechanic(mechanic);
        }
    }

    private void RemoveSpecialMechanics(ArmorSetEffect effect)
    {
        foreach (var mechanic in effect.specialMechanics)
        {
            RemoveSpecialMechanic(mechanic);
        }
    }

    private void ApplySpecialMechanic(SpecialMechanic mechanic)
    {
        if (string.IsNullOrEmpty(mechanic.mechanicId)) return;

        // Get or create the effect registry
        var registry = EffectRegistry.Instance;
        if (registry != null)
        {
            registry.ApplySpecialMechanic(mechanic, true);
        }

        activeMechanics[mechanic.mechanicId] = true;
        OnSpecialMechanicActivated?.Invoke(mechanic);
        LogDebug($"Applied special mechanic: {mechanic.mechanicName}");
    }

    private void RemoveSpecialMechanic(SpecialMechanic mechanic)
    {
        if (string.IsNullOrEmpty(mechanic.mechanicId)) return;

        var registry = EffectRegistry.Instance;
        if (registry != null)
        {
            registry.ApplySpecialMechanic(mechanic, false);
        }

        activeMechanics.Remove(mechanic.mechanicId);
        OnSpecialMechanicDeactivated?.Invoke(mechanic);
        LogDebug($"Removed special mechanic: {mechanic.mechanicName}");
    }

    // Public API methods
    public List<ArmorSet> GetActiveSets()
    {
        return trackedSets
            .Where(t => t.equippedCount > 0)
            .Select(t => t.armorSet)
            .ToList();
    }

    public int GetEquippedPiecesCount(ArmorSet armorSet)
    {
        return setTrackers.TryGetValue(armorSet, out ArmorSetTracker tracker) ? tracker.equippedCount : 0;
    }

    public bool IsSetComplete(ArmorSet armorSet)
    {
        return setTrackers.TryGetValue(armorSet, out ArmorSetTracker tracker) && tracker.isSetComplete;
    }

    public List<ArmorSetEffect> GetActiveSetEffects(ArmorSet armorSet)
    {
        return setTrackers.TryGetValue(armorSet, out ArmorSetTracker tracker) ?
            new List<ArmorSetEffect>(tracker.activeEffects) : new List<ArmorSetEffect>();
    }

    public bool HasSpecialMechanic(string mechanicId)
    {
        return activeMechanics.ContainsKey(mechanicId) && activeMechanics[mechanicId];
    }

    public List<string> GetActiveSpecialMechanics()
    {
        return activeMechanics.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
    }

    // Utility methods
    private void ScanForEquippedArmor()
    {
        if (inventoryManager == null) return;

        trackedSets.Clear();
        setTrackers.Clear();

        var slots = inventoryManager.Slots;
        if (slots == null) return;

        foreach (var slotObj in slots)
        {
            if (slotObj == null) continue;

            var slot = slotObj.GetComponent<InventorySlot>();
            if (slot?.heldItem == null) continue;

            var inventoryItem = slot.heldItem.GetComponent<InventoryItem>();
            if (inventoryItem?.itemScriptableObject is ArmorSO armor && inventoryItem.isEquipped)
            {
                if (armor.IsPartOfSet())
                {
                    OnArmorEquipmentChanged(armor, true);
                }
            }
        }
    }

    // Public method for manual scanning
    public void ScanEquippedArmor()
    {
        ScanForEquippedArmor();
    }

    // Get a status report of all armor sets
    public string GetSetStatusReport()
    {
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        report.AppendLine("=== Armor Set Status Report ===");

        if (trackedSets.Count == 0)
        {
            report.AppendLine("No armor sets currently tracked.");
            return report.ToString();
        }

        foreach (var tracker in trackedSets)
        {
            if (tracker.equippedCount > 0)
            {
                report.AppendLine($"\n{tracker.armorSet.SetName}:");
                report.AppendLine($"  Pieces Equipped: {tracker.equippedCount}");
                report.AppendLine($"  Is Complete: {tracker.isSetComplete}");

                if (tracker.activeEffects.Count > 0)
                {
                    report.AppendLine("  Active Effects:");
                    foreach (var effect in tracker.activeEffects)
                    {
                        report.AppendLine($"    - {effect.effectName} ({effect.piecesRequired} pieces)");
                    }
                }
            }
        }

        var activeMechanics = GetActiveSpecialMechanics();
        if (activeMechanics.Count > 0)
        {
            report.AppendLine("\nActive Special Mechanics:");
            foreach (var mechanic in activeMechanics)
            {
                report.AppendLine($"  - {mechanic}");
            }
        }

        return report.ToString();
    }

    private int GetRequiredPiecesForFullSet(ArmorSet armorSet)
    {
        if (armorSet == null) return 3;

        // Use the highest pieces required from effects as the full set requirement
        int maxRequired = 0;
        foreach (var effect in armorSet.SetEffects)
        {
            if (effect.piecesRequired > maxRequired)
                maxRequired = effect.piecesRequired;
        }
        return maxRequired > 0 ? maxRequired : 3;
    }

    private void PlaySetSound(bool activated)
    {
        if (audioSource == null) return;

        var clip = activated ? setActivatedSound : setDeactivatedSound;
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[ArmorSetManager] {message}");
        }
    }

    // Validation
    private void OnValidate()
    {
        if (trackedSets != null)
        {
            trackedSets.RemoveAll(t => t == null || t.armorSet == null);
        }
    }
}