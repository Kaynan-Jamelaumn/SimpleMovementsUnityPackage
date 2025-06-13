using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class ActiveTraitInfo
{
    public Trait trait;
    public bool isActive;
    public bool isTemporary;
    public float remainingDuration;
    public float cooldownRemaining;
    public DateTime activatedTime;
}

public class TraitManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private TraitDatabase traitDatabase;
    [SerializeField] private int availableTraitPoints = 10;
    [SerializeField] private int maxTraits = 10;
    [SerializeField] private bool allowNegativeTraits = true;

    [Header("Starting Traits")]
    [SerializeField] private List<Trait> startingTraits = new List<Trait>();

    [Header("Active Traits")]
    [SerializeField] private List<ActiveTraitInfo> activeTraits = new List<ActiveTraitInfo>();

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    public event Action<Trait> OnTraitAdded;
    public event Action<Trait> OnTraitRemoved;
    public event Action<int> OnTraitPointsChanged;
    public event Action<Trait> OnTraitExpired;

    // Armor Set Integration
    [Header("Armor Set Integration")]
    [SerializeField] private ArmorSetManager armorSetManager;
    [SerializeField] private Dictionary<Trait, float> traitMultipliers = new Dictionary<Trait, float>();
    [SerializeField] private List<Trait> armorAppliedTraits = new List<Trait>();

    // Events for trait enhancement
    public event Action<Trait, float> OnTraitMultiplierChanged;
    public event Action<Trait, List<TraitEffect>> OnTraitEffectsAdded;

    // Properties
    public List<Trait> ActiveTraits => activeTraits.Where(t => t.isActive).Select(t => t.trait).ToList();
    public List<ActiveTraitInfo> ActiveTraitInfos => activeTraits.Where(t => t.isActive).ToList();
    public int AvailableTraitPoints => availableTraitPoints;
    public TraitDatabase Database => traitDatabase;

    // Methods for armor set integration
    public void NotifyTraitMultiplierChanged(Trait trait, float multiplier)
    {
        OnTraitMultiplierChanged?.Invoke(trait, multiplier);
    }

    public void NotifyTraitEffectsAdded(Trait trait, List<TraitEffect> effects)
    {
        OnTraitEffectsAdded?.Invoke(trait, effects);
    }

    // Public method to apply a trait effect
    public void ApplyTraitEffect(TraitEffect effect, Trait trait)
    {
        ApplyTraitEffect(effect, trait, effect.value);
    }

    private PlayerStatusController playerController;

    private void Awake()
    {
        playerController = GetComponent<PlayerStatusController>();

        if (traitDatabase == null)
            traitDatabase = TraitDatabase.Instance;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        ApplyStartingTraits();
        StartCoroutine(TraitUpdateCoroutine());
    }

    // Add trait with enhanced options
    public bool AddTrait(Trait trait, bool ignoreRequirements = false, bool isTemporary = false, float duration = 0f)
    {
        if (trait == null) return false;

        // Check if can add trait
        if (!ignoreRequirements && !CanAddTrait(trait))
            return false;

        // Check if already has trait
        if (HasTrait(trait))
            return false;

        // Create trait info
        var traitInfo = new ActiveTraitInfo
        {
            trait = trait,
            isActive = true,
            isTemporary = isTemporary,
            remainingDuration = duration,
            cooldownRemaining = 0f,
            activatedTime = DateTime.Now
        };

        // Add to active traits
        activeTraits.Add(traitInfo);

        // Apply trait effects through effect registry
        ApplyTraitEffects(trait);

        // Update trait points
        if (!ignoreRequirements)
        {
            availableTraitPoints -= trait.cost;
            OnTraitPointsChanged?.Invoke(availableTraitPoints);
        }

        // Play sound
        PlayTraitSound(trait);

        // Fire event
        OnTraitAdded?.Invoke(trait);

        Debug.Log($"Added trait: {trait.Name}");
        return true;
    }

    public bool RemoveTrait(Trait trait, bool forceRemove = false)
    {
        if (trait == null) return false;

        var traitInfo = activeTraits.FirstOrDefault(t => t.trait == trait);
        if (traitInfo == null) return false;

        // Check if can remove
        if (!forceRemove && !CanRemoveTrait(trait))
            return false;

        // Remove trait effects through effect registry
        RemoveTraitEffects(trait);

        // Remove from active list
        activeTraits.Remove(traitInfo);

        // Restore trait points
        if (!armorAppliedTraits.Contains(trait))
        {
            availableTraitPoints += trait.cost;
            OnTraitPointsChanged?.Invoke(availableTraitPoints);
        }

        // Fire event
        OnTraitRemoved?.Invoke(trait);

        Debug.Log($"Removed trait: {trait.Name}");
        return true;
    }

    public bool HasTrait(Trait trait)
    {
        return activeTraits.Any(t => t.trait == trait && t.isActive);
    }

    public bool CanAddTrait(Trait trait)
    {
        if (trait == null) return false;

        // Check trait points
        if (trait.cost > availableTraitPoints) return false;

        // Check max traits
        if (activeTraits.Count >= maxTraits) return false;

        // Check negative traits
        if (!allowNegativeTraits && trait.IsNegative) return false;

        // Check dependencies
        foreach (var required in trait.requiredTraits)
        {
            if (required != null && !HasTrait(required))
                return false;
        }

        // Check incompatibilities
        foreach (var incompatible in trait.incompatibleTraits)
        {
            if (incompatible != null && HasTrait(incompatible))
                return false;
        }

        // Check mutually exclusive
        foreach (var exclusive in trait.mutuallyExclusiveTraits)
        {
            if (exclusive != null && HasTrait(exclusive))
                return false;
        }

        return true;
    }

    public bool CanRemoveTrait(Trait trait)
    {
        if (trait == null) return false;

        // Check if trait is required by other traits
        foreach (var activeInfo in activeTraits)
        {
            if (activeInfo.trait.requiredTraits.Contains(trait))
                return false;
        }

        // Check if applied by armor
        if (armorAppliedTraits.Contains(trait))
            return false;

        return true;
    }

    // Apply trait effects using hardcoded mappings for stats
    private void ApplyTraitEffects(Trait trait)
    {
        if (playerController == null || trait == null) return;

        float multiplier = GetTraitMultiplier(trait);

        foreach (var effect in trait.effects)
        {
            ApplyTraitEffectWithMultiplier(effect, trait, multiplier);
        }
    }

    private void RemoveTraitEffects(Trait trait)
    {
        if (playerController == null || trait == null) return;

        foreach (var effect in trait.effects)
        {
            RemoveTraitEffect(effect, trait);
        }
    }

    private void ApplyTraitEffectWithMultiplier(TraitEffect effect, Trait trait, float multiplier = 1f)
    {
        float modifiedValue = effect.value * multiplier;
        ApplyTraitEffect(effect, trait, modifiedValue);
    }

    private void ApplyTraitEffect(TraitEffect effect, Trait trait, float value = -1f)
    {
        if (value < 0) value = effect.value;

        switch (effect.targetStat.ToLower())
        {
            case "health":
            case "hp":
                ApplyHealthEffect(effect, value);
                break;
            case "stamina":
                ApplyStaminaEffect(effect, value);
                break;
            case "mana":
                ApplyManaEffect(effect, value);
                break;
            case "speed":
                ApplySpeedEffect(effect, value);
                break;
            case "damage":
                ApplyDamageEffect(effect, trait, value);
                break;
            case "defense":
                ApplyDefenseEffect(effect, value);
                break;
            default:
                Debug.LogWarning($"Trait effect for {effect.targetStat} not implemented yet");
                break;
        }
    }

    public void RemoveTraitEffect(TraitEffect effect, Trait trait)
    {
        // This explicitly removes an effect by applying its negative
        var reverseEffect = new TraitEffect
        {
            effectType = effect.effectType,
            value = -effect.value,
            targetStat = effect.targetStat,
            effectDescription = effect.effectDescription
        };

        ApplyTraitEffect(reverseEffect, trait, reverseEffect.value);
    }


    // Health effects
    private void ApplyHealthEffect(TraitEffect effect, float value)
    {
        switch (effect.effectType)
        {
            case TraitEffectType.StatMultiplier:
                float bonus = playerController.HpManager.MaxValue * (value - 1f);
                playerController.HpManager.ModifyMaxValue(bonus);
                break;
            case TraitEffectType.StatAddition:
                playerController.HpManager.ModifyMaxValue(value);
                break;
            case TraitEffectType.RegenerationRate:
                playerController.HpManager.ModifyIncrementValue(value);
                break;
        }
    }

    private void RemoveHealthEffect(TraitEffect effect)
    {
        switch (effect.effectType)
        {
            case TraitEffectType.StatMultiplier:
                float penalty = playerController.HpManager.MaxValue * (effect.value - 1f);
                playerController.HpManager.ModifyMaxValue(-penalty);
                break;
            case TraitEffectType.StatAddition:
                playerController.HpManager.ModifyMaxValue(-effect.value);
                break;
            case TraitEffectType.RegenerationRate:
                playerController.HpManager.ModifyIncrementValue(-effect.value);
                break;
        }
    }

    // Stamina effects
    private void ApplyStaminaEffect(TraitEffect effect, float value)
    {
        switch (effect.effectType)
        {
            case TraitEffectType.StatMultiplier:
                float bonus = playerController.StaminaManager.MaxValue * (value - 1f);
                playerController.StaminaManager.ModifyMaxValue(bonus);
                break;
            case TraitEffectType.StatAddition:
                playerController.StaminaManager.ModifyMaxValue(value);
                break;
            case TraitEffectType.RegenerationRate:
                playerController.StaminaManager.ModifyIncrementValue(value);
                break;
            case TraitEffectType.ConsumptionRate:
                playerController.StaminaManager.ModifyDecrementFactor(value);
                break;
        }
    }

    private void RemoveStaminaEffect(TraitEffect effect)
    {
        switch (effect.effectType)
        {
            case TraitEffectType.StatMultiplier:
                float penalty = playerController.StaminaManager.MaxValue * (effect.value - 1f);
                playerController.StaminaManager.ModifyMaxValue(-penalty);
                break;
            case TraitEffectType.StatAddition:
                playerController.StaminaManager.ModifyMaxValue(-effect.value);
                break;
            case TraitEffectType.RegenerationRate:
                playerController.StaminaManager.ModifyIncrementValue(-effect.value);
                break;
            case TraitEffectType.ConsumptionRate:
                playerController.StaminaManager.ModifyDecrementFactor(-effect.value);
                break;
        }
    }

    // Mana effects
    private void ApplyManaEffect(TraitEffect effect, float value)
    {
        switch (effect.effectType)
        {
            case TraitEffectType.StatMultiplier:
                float bonus = playerController.ManaManager.MaxValue * (value - 1f);
                playerController.ManaManager.ModifyMaxValue(bonus);
                break;
            case TraitEffectType.StatAddition:
                playerController.ManaManager.ModifyMaxValue(value);
                break;
            case TraitEffectType.RegenerationRate:
                playerController.ManaManager.ModifyIncrementValue(value);
                break;
        }
    }

    private void RemoveManaEffect(TraitEffect effect)
    {
        switch (effect.effectType)
        {
            case TraitEffectType.StatMultiplier:
                float penalty = playerController.ManaManager.MaxValue * (effect.value - 1f);
                playerController.ManaManager.ModifyMaxValue(-penalty);
                break;
            case TraitEffectType.StatAddition:
                playerController.ManaManager.ModifyMaxValue(-effect.value);
                break;
            case TraitEffectType.RegenerationRate:
                playerController.ManaManager.ModifyIncrementValue(-effect.value);
                break;
        }
    }

    // Speed effects
    private void ApplySpeedEffect(TraitEffect effect, float value)
    {
        switch (effect.effectType)
        {
            case TraitEffectType.StatMultiplier:
                float bonus = playerController.SpeedManager.BaseSpeed * (value - 1f);
                playerController.SpeedManager.ModifyBaseSpeed(bonus);
                break;
            case TraitEffectType.StatAddition:
                playerController.SpeedManager.ModifyBaseSpeed(value);
                break;
        }
    }

    private void RemoveSpeedEffect(TraitEffect effect)
    {
        switch (effect.effectType)
        {
            case TraitEffectType.StatMultiplier:
                float penalty = playerController.SpeedManager.BaseSpeed * (effect.value - 1f);
                playerController.SpeedManager.ModifyBaseSpeed(-penalty);
                break;
            case TraitEffectType.StatAddition:
                playerController.SpeedManager.ModifyBaseSpeed(-effect.value);
                break;
        }
    }

    // Combat effects (placeholder implementations)
    private void ApplyDamageEffect(TraitEffect effect, Trait trait, float value)
    {
        Debug.Log($"Applied damage effect from trait {trait.Name}: {value}");
        // TODO: Implement when damage system is available
    }

    private void RemoveDamageEffect(TraitEffect effect, Trait trait)
    {
        Debug.Log($"Removed damage effect from trait {trait.Name}: {effect.value}");
        // TODO: Implement when damage system is available
    }

    private void ApplyDefenseEffect(TraitEffect effect, float value)
    {
        Debug.Log($"Applied defense effect: {value}");
        // TODO: Implement when defense system is available
    }

    private void RemoveDefenseEffect(TraitEffect effect)
    {
        Debug.Log($"Removed defense effect: {effect.value}");
        // TODO: Implement when defense system is available
    }

    // Get trait multiplier (for armor set enhancements)
    public float GetTraitMultiplier(Trait trait)
    {
        return traitMultipliers.TryGetValue(trait, out float multiplier) ? multiplier : 1f;
    }

    // Coroutine to update temporary traits
    private IEnumerator TraitUpdateCoroutine()
    {
        while (true)
        {
            UpdateTemporaryTraits();
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void UpdateTemporaryTraits()
    {
        var traitsToRemove = new List<Trait>();

        foreach (var traitInfo in activeTraits)
        {
            if (traitInfo.isTemporary && traitInfo.remainingDuration > 0)
            {
                traitInfo.remainingDuration -= 0.1f;

                if (traitInfo.remainingDuration <= 0)
                {
                    traitsToRemove.Add(traitInfo.trait);
                    OnTraitExpired?.Invoke(traitInfo.trait);
                }
            }
        }

        foreach (var trait in traitsToRemove)
        {
            RemoveTrait(trait, true);
        }
    }

    // Starting traits
    private void ApplyStartingTraits()
    {
        foreach (var trait in startingTraits)
        {
            if (trait != null)
            {
                AddTrait(trait, true);
            }
        }
    }

    // Get available traits
    public List<Trait> GetAvailableTraits()
    {
        if (traitDatabase == null) return new List<Trait>();

        return traitDatabase.GetAllTraits()
            .Where(t => t.cost <= availableTraitPoints)
            .Where(CanAddTrait).ToList();
    }

    // Trait points management
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

    // Temporary trait system
    public bool AddTemporaryTrait(Trait trait, float duration)
    {
        return AddTrait(trait, true, true, duration);
    }

    // Play trait sound
    private void PlayTraitSound(Trait trait)
    {
        if (audioSource != null && trait.acquisitionSound != null)
        {
            audioSource.PlayOneShot(trait.acquisitionSound);
        }
    }

    // Utility methods
    public int GetTotalTraitCost()
    {
        return ActiveTraits.Sum(t => t.cost);
    }

    public List<Trait> GetPositiveTraits()
    {
        return ActiveTraits.Where(t => t.IsPositive).ToList();
    }

    public List<Trait> GetNegativeTraits()
    {
        return ActiveTraits.Where(t => t.IsNegative).ToList();
    }

    public void ClearAllTraits()
    {
        var traitsToRemove = ActiveTraits.ToList();
        foreach (var trait in traitsToRemove)
        {
            RemoveTrait(trait, true);
        }
    }

    // Validation for armor set trait conflicts
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
                // Check for trait conflicts
                foreach (var enhancement in effect.traitEnhancements)
                {
                    if (enhancement.originalTrait == null) continue;

                    bool hasOriginalTrait = activeTraitsList.Contains(enhancement.originalTrait);

                    if (enhancement.enhancementType == TraitEnhancementType.Replace && !hasOriginalTrait)
                    {
                        issues.Add($"Set {armorSet.SetName} wants to replace trait {enhancement.originalTrait.Name} but player doesn't have it");
                    }

                    if (enhancement.enhancementType == TraitEnhancementType.Upgrade && enhancement.enhancedTrait == null)
                    {
                        issues.Add($"Set {armorSet.SetName} enhancement for {enhancement.originalTrait.Name} has no enhanced trait specified");
                    }
                }

                // Check for conflicting trait applications
                foreach (var newTrait in effect.traitsToApply)
                {
                    if (newTrait == null) continue;

                    // Check incompatibilities
                    foreach (var activeTrait in activeTraitsList)
                    {
                        if (newTrait.incompatibleTraits.Contains(activeTrait))
                        {
                            issues.Add($"Set trait {newTrait.Name} is incompatible with active trait {activeTrait.Name}");
                        }

                        if (newTrait.mutuallyExclusiveTraits.Contains(activeTrait))
                        {
                            issues.Add($"Set trait {newTrait.Name} is mutually exclusive with active trait {activeTrait.Name}");
                        }
                    }
                }
            }
        }

        return issues;
    }

    // Debug methods
    [ContextMenu("Add 5 Trait Points")]
    private void DebugAddTraitPoints()
    {
        AddTraitPoints(5);
    }

    [ContextMenu("Log Active Traits")]
    private void DebugLogActiveTraits()
    {
        Debug.Log("=== Active Traits ===");
        foreach (var trait in ActiveTraits)
        {
            Debug.Log($"- {trait.Name} (Cost: {trait.cost})");
        }
        Debug.Log($"Total Cost: {GetTotalTraitCost()}");
        Debug.Log($"Available Points: {availableTraitPoints}");
    }

    [ContextMenu("Validate Trait Compatibility")]
    private void DebugValidateCompatibility()
    {
        var issues = ValidateTraitCompatibilityWithArmorSets();
        if (issues.Count == 0)
        {
            Debug.Log("No trait compatibility issues found!");
        }
        else
        {
            Debug.Log("=== Trait Compatibility Issues ===");
            foreach (var issue in issues)
            {
                Debug.LogWarning(issue);
            }
        }
    }


}