using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;



[System.Serializable]
public class SpecialMechanicRegistry
{
    [SerializeField] private Dictionary<string, System.Type> mechanicTypes = new Dictionary<string, System.Type>();
    [SerializeField] private Dictionary<string, Component> mechanicHandlers = new Dictionary<string, Component>();

    public void RegisterMechanic(string mechanicId, System.Type handlerType)
    {
        mechanicTypes[mechanicId] = handlerType;
    }

    public void RegisterMechanicHandler(string mechanicId, Component handler)
    {
        mechanicHandlers[mechanicId] = handler;
    }

    public Component GetMechanicHandler(string mechanicId)
    {
        return mechanicHandlers.TryGetValue(mechanicId, out Component handler) ? handler : null;
    }

    public bool HasHandler(string mechanicId)
    {
        return mechanicHandlers.ContainsKey(mechanicId);
    }
}

public class ArmorSetManager : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private PlayerStatusController playerStatusController;
    [SerializeField] private TraitManager traitManager;
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private AudioSource audioSource;

    [Header("Special Mechanics Handlers")]
    [SerializeField] private PlayerMovementController movementController;


    [Header("Set Management")]
    [SerializeField] private List<ArmorSetTracker> trackedSets = new List<ArmorSetTracker>();
    [SerializeField] private Dictionary<ArmorSet, ArmorSetTracker> setTrackers = new Dictionary<ArmorSet, ArmorSetTracker>();

    [Header("Enhanced Trait System")]
    [SerializeField] private Dictionary<Trait, List<Trait>> enhancedTraitMappings = new Dictionary<Trait, List<Trait>>();
    [SerializeField] private List<Trait> temporarySetTraits = new List<Trait>();

    [Header("Special Mechanics System")]
    [SerializeField] private SpecialMechanicRegistry mechanicRegistry = new SpecialMechanicRegistry();
    [SerializeField] private Dictionary<string, bool> activeMechanics = new Dictionary<string, bool>();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = true;
    [SerializeField] private bool validateSetEffects = true;

    // Events for UI and other systems
    public event Action<ArmorSet, int> OnSetPiecesChanged;
    public event Action<ArmorSet> OnSetCompleted;
    public event Action<ArmorSet> OnSetBroken;
    public event Action<ArmorSetEffect> OnSetEffectActivated;
    public event Action<ArmorSetEffect> OnSetEffectDeactivated;
    public event Action<SpecialMechanic> OnSpecialMechanicActivated;
    public event Action<SpecialMechanic> OnSpecialMechanicDeactivated;

    // Properties
    public List<ArmorSetTracker> TrackedSets => trackedSets;
    public PlayerStatusController PlayerStatusController => playerStatusController;
    public Dictionary<string, bool> ActiveMechanics => new Dictionary<string, bool>(activeMechanics);

    private void Awake()
    {
        ValidateComponents();
        InitializeSetTrackers();
        InitializeMechanicRegistry();
    }

    private void Start()
    {
        ScanEquippedArmor();
    }

    private void ValidateComponents()
    {
        if (playerStatusController == null)
            playerStatusController = GetComponent<PlayerStatusController>();

        if (traitManager == null)
            traitManager = GetComponent<TraitManager>();

        if (inventoryManager == null)
            inventoryManager = GetComponent<InventoryManager>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void InitializeSetTrackers()
    {
        setTrackers.Clear();
        trackedSets.Clear();
        enhancedTraitMappings.Clear();
        temporarySetTraits.Clear();
    }

    private void InitializeMechanicRegistry()
    {
        // Register built-in special mechanics
        RegisterBuiltInMechanics();
    }

    private void RegisterBuiltInMechanics()
    {
        // Register movement-related mechanics
        if (movementController != null)
        {
            mechanicRegistry.RegisterMechanicHandler("water_walking", movementController);
            mechanicRegistry.RegisterMechanicHandler("gravity_reduction", movementController);
        }

    }

    // Called when armor is equipped or unequipped
    public void OnArmorEquipmentChanged(ArmorSO armor, bool isEquipping)
    {
        if (armor == null) return;

        LogDebug($"Armor {(isEquipping ? "equipped" : "unequipped")}: {armor.name}");

        if (armor.IsPartOfSet())
        {
            UpdateSetTracking(armor, isEquipping);
        }

        // Also scan all equipped armor to ensure consistency
        ScanEquippedArmor();
    }

    private void UpdateSetTracking(ArmorSO armor, bool isEquipping)
    {
        ArmorSet armorSet = armor.BelongsToSet;
        if (armorSet == null) return;

        // Get or create set tracker
        ArmorSetTracker tracker = GetOrCreateSetTracker(armorSet);
        int previousCount = tracker.equippedCount;
        bool wasComplete = tracker.isSetComplete;

        // Update equipped pieces
        if (isEquipping)
        {
            tracker.AddPiece(armor);
        }
        else
        {
            tracker.RemovePiece(armor);
        }

        // Update active effects
        var previousEffects = new List<ArmorSetEffect>(tracker.activeEffects);
        tracker.UpdateActiveEffects();

        // Handle effect changes with enhanced system
        HandleEffectChanges(tracker, previousEffects);

        // Handle set completion changes
        HandleSetCompletionChanges(tracker, wasComplete, previousCount);

        // Notify listeners
        OnSetPiecesChanged?.Invoke(armorSet, tracker.equippedCount);

        LogDebug($"Set {armorSet.SetName}: {tracker.equippedCount}/{armorSet.SetPieces.Count} pieces equipped");
    }

    private ArmorSetTracker GetOrCreateSetTracker(ArmorSet armorSet)
    {
        if (!setTrackers.TryGetValue(armorSet, out ArmorSetTracker tracker))
        {
            tracker = new ArmorSetTracker(armorSet);
            setTrackers[armorSet] = tracker;
            trackedSets.Add(tracker);
        }
        return tracker;
    }

    private void HandleEffectChanges(ArmorSetTracker tracker, List<ArmorSetEffect> previousEffects)
    {
        // Find newly activated effects
        var newEffects = tracker.activeEffects.Except(previousEffects).ToList();
        foreach (var effect in newEffects)
        {
            ApplySetEffect(effect, tracker.armorSet);
            OnSetEffectActivated?.Invoke(effect);
            LogDebug($"Activated set effect: {effect.effectName}");
        }

        // Find deactivated effects
        var removedEffects = previousEffects.Except(tracker.activeEffects).ToList();
        foreach (var effect in removedEffects)
        {
            RemoveSetEffect(effect, tracker.armorSet);
            OnSetEffectDeactivated?.Invoke(effect);
            LogDebug($"Deactivated set effect: {effect.effectName}");
        }
    }

    private void HandleSetCompletionChanges(ArmorSetTracker tracker, bool wasComplete, int previousCount)
    {
        bool isNowComplete = tracker.isSetComplete;

        if (!wasComplete && isNowComplete)
        {
            OnSetCompleted?.Invoke(tracker.armorSet);
            PlaySetCompleteEffects(tracker.armorSet);
            LogDebug($"Set completed: {tracker.armorSet.SetName}");
        }
        else if (wasComplete && !isNowComplete)
        {
            OnSetBroken?.Invoke(tracker.armorSet);
            LogDebug($"Set broken: {tracker.armorSet.SetName}");
        }
    }

    private void ApplySetEffect(ArmorSetEffect effect, ArmorSet armorSet)
    {
        if (playerStatusController == null) return;

        // Validate effect if enabled
        if (validateSetEffects)
        {
            var issues = effect.ValidateConfiguration();
            if (issues.Count > 0)
            {
                LogDebug($"Warning: Set effect {effect.effectName} has validation issues: {string.Join(", ", issues)}");
            }
        }

        // Apply new traits
        ApplySetTraits(effect);

        // Apply trait enhancements
        ApplyTraitEnhancements(effect);

        // Apply stat bonuses
        ApplyStatBonuses(effect);

        // Apply special mechanics
        ApplySpecialMechanics(effect);

        // Play visual/audio effects
        PlaySetEffectActivation(effect, armorSet);
    }

    private void RemoveSetEffect(ArmorSetEffect effect, ArmorSet armorSet)
    {
        if (playerStatusController == null) return;

        // Remove traits
        RemoveSetTraits(effect);

        // Remove trait enhancements
        RemoveTraitEnhancements(effect);

        // Remove stat bonuses
        RemoveStatBonuses(effect);

        // Remove special mechanics
        RemoveSpecialMechanics(effect);
    }

    private void ApplySetTraits(ArmorSetEffect effect)
    {
        if (traitManager == null) return;

        foreach (var trait in effect.traitsToApply)
        {
            if (trait != null)
            {
                traitManager.AddTrait(trait, true); // Free application for set bonuses
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
                traitManager.RemoveTrait(trait, true); // Force removal
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

            // Check if the player has the original trait
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

            LogDebug($"Removed trait enhancement: {enhancement.originalTrait.Name}");
        }
    }

    private void ApplyTraitMultiplier(TraitEnhancement enhancement)
    {
        // This would require modifying trait effects dynamically
        // For now, we'll store the enhancement and let other systems query it
        if (!enhancedTraitMappings.ContainsKey(enhancement.originalTrait))
        {
            enhancedTraitMappings[enhancement.originalTrait] = new List<Trait>();
        }

        // Create a temporary enhanced version
        // In a full implementation, you'd want to create actual enhanced trait objects
        LogDebug($"Multiplying effects of {enhancement.originalTrait.Name} by {enhancement.effectMultiplier}");
    }

    private void RemoveTraitMultiplier(TraitEnhancement enhancement)
    {
        if (enhancedTraitMappings.ContainsKey(enhancement.originalTrait))
        {
            enhancedTraitMappings.Remove(enhancement.originalTrait);
        }
    }

    private void ApplyAdditionalTraitEffects(TraitEnhancement enhancement)
    {
        // Apply additional effects as temporary traits
        // In practice, you'd want to modify the existing trait's effects
        LogDebug($"Adding additional effects to {enhancement.originalTrait.Name}");
    }

    private void RemoveAdditionalTraitEffects(TraitEnhancement enhancement)
    {
        LogDebug($"Removing additional effects from {enhancement.originalTrait.Name}");
    }

    private void ReplaceTraitTemporarily(TraitEnhancement enhancement)
    {
        // Remove original and add enhanced temporarily
        traitManager.RemoveTrait(enhancement.originalTrait, true);
        if (enhancement.enhancedTrait != null)
        {
            traitManager.AddTrait(enhancement.enhancedTrait, true);
            temporarySetTraits.Add(enhancement.enhancedTrait);
        }
    }

    private void RestoreOriginalTrait(TraitEnhancement enhancement)
    {
        // Remove enhanced and restore original
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

        // Similar to replace but conceptually different
        ReplaceTraitTemporarily(enhancement);
    }

    private void RevertFromEnhancedTrait(TraitEnhancement enhancement)
    {
        RestoreOriginalTrait(enhancement);
    }

    private void ApplyStatBonuses(ArmorSetEffect effect)
    {
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

    private void ApplySpecialMechanics(ArmorSetEffect effect)
    {
        // Apply new special mechanics system
        foreach (var mechanic in effect.specialMechanics)
        {
            ApplySpecialMechanic(mechanic);
        }

    }

    private void RemoveSpecialMechanics(ArmorSetEffect effect)
    {
        // Remove new special mechanics
        foreach (var mechanic in effect.specialMechanics)
        {
            RemoveSpecialMechanic(mechanic);
        }

    }

    private void ApplySpecialMechanic(SpecialMechanic mechanic)
    {
        if (string.IsNullOrEmpty(mechanic.mechanicId)) return;

        var handler = mechanicRegistry.GetMechanicHandler(mechanic.mechanicId);
        if (handler != null)
        {
            // Use reflection or interface to apply the mechanic
            ApplyMechanicToHandler(handler, mechanic, true);
        }
        else
        {
            // Handle built-in mechanics or log unknown mechanic
            HandleBuiltInMechanic(mechanic, true);
        }

        activeMechanics[mechanic.mechanicId] = true;
        OnSpecialMechanicActivated?.Invoke(mechanic);
        LogDebug($"Applied special mechanic: {mechanic.mechanicName}");
    }

    private void RemoveSpecialMechanic(SpecialMechanic mechanic)
    {
        if (string.IsNullOrEmpty(mechanic.mechanicId)) return;

        var handler = mechanicRegistry.GetMechanicHandler(mechanic.mechanicId);
        if (handler != null)
        {
            ApplyMechanicToHandler(handler, mechanic, false);
        }
        else
        {
            HandleBuiltInMechanic(mechanic, false);
        }

        activeMechanics.Remove(mechanic.mechanicId);
        OnSpecialMechanicDeactivated?.Invoke(mechanic);
        LogDebug($"Removed special mechanic: {mechanic.mechanicName}");
    }

    private void ApplyMechanicToHandler(Component handler, SpecialMechanic mechanic, bool apply)
    {
        // This would use interfaces or reflection to apply mechanics
        // For example, if handler implements ISpecialMechanicHandler:
        // ((ISpecialMechanicHandler)handler).ApplyMechanic(mechanic, apply);

        LogDebug($"Applying mechanic {mechanic.mechanicName} to handler {handler.GetType().Name}");
    }

    private void HandleBuiltInMechanic(SpecialMechanic mechanic, bool apply)
    {
        switch (mechanic.mechanicId.ToLower())
        {
            case "water_walking":
                HandleWaterWalking(apply);
                break;
            case "double_jump":
                HandleDoubleJump(apply);
                break;
            case "gravity_reduction":
                HandleGravityReduction(mechanic, apply);
                break;
            default:
                LogDebug($"Unknown built-in mechanic: {mechanic.mechanicId}");
                break;
        }
    }

   

    private void HandleWaterWalking(bool enable)
    {
        if (movementController != null)
        {
            // Assuming movementController has a method to enable/disable water walking
            // movementController.SetWaterWalking(enable);
            LogDebug($"Water walking {(enable ? "enabled" : "disabled")}");
        }
    }

    private void HandleDoubleJump(bool enable)
    {
        //if (jumpController != null)
        //{
        //    // jumpController.SetDoubleJump(enable);
        //    LogDebug($"Double jump {(enable ? "enabled" : "disabled")}");
        //}
    }

    private void HandleGravityReduction(SpecialMechanic mechanic, bool apply)
    {
        var reductionAmount = mechanic.parameters.FirstOrDefault(p => p.parameterName == "reduction")?.value ?? 0.5f;
        HandleGravityReduction(reductionAmount, apply);
    }

    private void HandleGravityReduction(float reduction, bool apply)
    {
        if (movementController != null)
        {
            // movementController.SetGravityMultiplier(apply ? (1f - reduction) : 1f);
            LogDebug($"Gravity reduction {(apply ? $"applied ({reduction * 100:F0}%)" : "removed")}");
        }
    }

    private void ApplyStatBonus(EquippableEffect statBonus, bool isApplying)
    {
        float amount = statBonus.amount * (isApplying ? 1f : -1f);

        switch (statBonus.effectType)
        {
            case EquippableEffectType.MaxHp:
                playerStatusController.HpManager.ModifyMaxValue(amount);
                break;
            case EquippableEffectType.MaxStamina:
                playerStatusController.StaminaManager.ModifyMaxValue(amount);
                break;
            case EquippableEffectType.MaxWeight:
                playerStatusController.WeightManager.ModifyMaxWeight(amount);
                break;
            case EquippableEffectType.Speed:
                playerStatusController.SpeedManager.ModifyBaseSpeed(amount);
                break;
            case EquippableEffectType.HpRegeneration:
                playerStatusController.HpManager.ModifyIncrementValue(amount);
                break;
            case EquippableEffectType.StaminaRegeneration:
                playerStatusController.StaminaManager.ModifyIncrementValue(amount);
                break;
            case EquippableEffectType.HpHealFactor:
                playerStatusController.HpManager.ModifyIncrementFactor(amount);
                break;
            case EquippableEffectType.StaminaHealFactor:
                playerStatusController.StaminaManager.ModifyIncrementFactor(amount);
                break;
            case EquippableEffectType.HpDamageFactor:
                playerStatusController.HpManager.ModifyDecrementFactor(amount);
                break;
            case EquippableEffectType.StaminaDamageFactor:
                playerStatusController.StaminaManager.ModifyDecrementFactor(amount);
                break;
        }
    }

    private void PlaySetEffectActivation(ArmorSetEffect effect, ArmorSet armorSet)
    {
        // Play activation sound
        if (audioSource != null && effect.setActivationSound != null)
        {
            audioSource.PlayOneShot(effect.setActivationSound);
        }

        // Spawn activation particles
        if (effect.setActivationParticles != null)
        {
            var particles = Instantiate(effect.setActivationParticles, transform.position, transform.rotation);
            particles.Play();
        }

        // Spawn effect prefab
        if (effect.setEffectPrefab != null)
        {
            Instantiate(effect.setEffectPrefab, transform.position, transform.rotation, transform);
        }
    }

    private void PlaySetCompleteEffects(ArmorSet armorSet)
    {
        // Play set complete sound
        if (audioSource != null && armorSet.SetCompleteSound != null)
        {
            audioSource.PlayOneShot(armorSet.SetCompleteSound);
        }

        // Spawn set complete effect
        if (armorSet.SetCompleteEffect != null)
        {
            Instantiate(armorSet.SetCompleteEffect, transform.position, transform.rotation);
        }
    }

    // Scan all equipped armor to rebuild set tracking
    public void ScanEquippedArmor()
    {
        if (inventoryManager == null) return;

        // Clear existing tracking
        foreach (var tracker in trackedSets)
        {
            tracker.equippedPieces.Clear();
        }

        // Get all equipped armor slots
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
                    var tracker = GetOrCreateSetTracker(armor.BelongsToSet);
                    tracker.AddPiece(armor);
                }
            }
        }

        // Update all active effects
        foreach (var tracker in trackedSets)
        {
            tracker.UpdateActiveEffects();
        }

        LogDebug("Armor scan completed");
    }

    // Public API for querying enhanced set information
    public bool IsTraitEnhanced(Trait trait)
    {
        return enhancedTraitMappings.ContainsKey(trait);
    }

    public List<Trait> GetEnhancedTraitsForTrait(Trait originalTrait)
    {
        return enhancedTraitMappings.TryGetValue(originalTrait, out List<Trait> enhanced) ?
               new List<Trait>(enhanced) : new List<Trait>();
    }

    public bool HasSpecialMechanic(string mechanicId)
    {
        return activeMechanics.ContainsKey(mechanicId) && activeMechanics[mechanicId];
    }

    public List<string> GetActiveSpecialMechanics()
    {
        return activeMechanics.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
    }

    // Register external special mechanic handlers
    public void RegisterSpecialMechanicHandler(string mechanicId, Component handler)
    {
        mechanicRegistry.RegisterMechanicHandler(mechanicId, handler);
        LogDebug($"Registered special mechanic handler: {mechanicId} -> {handler.GetType().Name}");
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
               new List<ArmorSetEffect>(tracker.activeEffects) :
               new List<ArmorSetEffect>();
    }

    public List<ArmorSet> GetActiveSets()
    {
        return trackedSets.Where(t => t.equippedCount > 0).Select(t => t.armorSet).ToList();
    }

    public List<ArmorSet> GetCompleteSets()
    {
        return trackedSets.Where(t => t.isSetComplete).Select(t => t.armorSet).ToList();
    }

    // Debug and utility methods
    public string GetSetStatusReport()
    {
        string report = "=== Enhanced Armor Set Status ===\n";

        foreach (var tracker in trackedSets.Where(t => t.equippedCount > 0))
        {
            report += $"{tracker.armorSet.SetName}: {tracker.equippedCount}/{tracker.armorSet.SetPieces.Count} pieces\n";

            if (tracker.activeEffects.Count > 0)
            {
                report += "  Active effects:\n";
                foreach (var effect in tracker.activeEffects)
                {
                    report += $"    - {effect.effectName}\n";

                    if (effect.traitEnhancements.Count > 0)
                    {
                        report += "      Enhanced traits:\n";
                        foreach (var enhancement in effect.traitEnhancements)
                        {
                            report += $"        • {enhancement.originalTrait?.Name} ({enhancement.enhancementType})\n";
                        }
                    }

                    if (effect.specialMechanics.Count > 0)
                    {
                        report += "      Special mechanics:\n";
                        foreach (var mechanic in effect.specialMechanics)
                        {
                            report += $"        • {mechanic.mechanicName}\n";
                        }
                    }
                }
            }
            report += "\n";
        }

        if (temporarySetTraits.Count > 0)
        {
            report += "Temporary Set Traits:\n";
            foreach (var trait in temporarySetTraits)
            {
                report += $"  • {trait.Name}\n";
            }
        }

        if (activeMechanics.Count > 0)
        {
            report += "Active Special Mechanics:\n";
            foreach (var mechanic in activeMechanics.Where(kvp => kvp.Value))
            {
                report += $"  • {mechanic.Key}\n";
            }
        }

        return report;
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[ArmorSetManager] {message}");
        }
    }

    // Force refresh all set effects (useful for debugging)
    [ContextMenu("Force Refresh All Sets")]
    public void ForceRefreshAllSets()
    {
        ScanEquippedArmor();

        foreach (var tracker in trackedSets)
        {
            // Remove all current effects
            foreach (var effect in tracker.activeEffects)
            {
                RemoveSetEffect(effect, tracker.armorSet);
            }

            // Reapply all effects
            tracker.UpdateActiveEffects();
            foreach (var effect in tracker.activeEffects)
            {
                ApplySetEffect(effect, tracker.armorSet);
            }
        }

        LogDebug("Force refresh completed");
    }

    private void OnDestroy()
    {
        // Clean up all active set effects
        foreach (var tracker in trackedSets)
        {
            foreach (var effect in tracker.activeEffects)
            {
                RemoveSetEffect(effect, tracker.armorSet);
            }
        }

        // Clear temporary traits
        if (traitManager != null)
        {
            foreach (var trait in temporarySetTraits)
            {
                traitManager.RemoveTrait(trait, true);
            }
        }
    }
}