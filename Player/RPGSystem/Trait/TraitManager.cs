using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

[System.Serializable]
public class ActiveTraitInfo
{
    public Trait trait;
    public float timeAcquired;
    public bool isActive = true;
    public bool isTemporary = false;
    public float duration = 0f; // For temporary traits

    public ActiveTraitInfo(Trait trait, bool temporary = false, float duration = 0f)
    {
        this.trait = trait;
        this.timeAcquired = Time.time;
        this.isActive = true;
        this.isTemporary = temporary;
        this.duration = duration;
    }
}

public class TraitManager : MonoBehaviour
{
    [Header("Trait Settings")]
    [SerializeField] private TraitDatabase traitDatabase;
    [SerializeField] private List<ActiveTraitInfo> activeTraits = new List<ActiveTraitInfo>();
    [SerializeField] private int availableTraitPoints = 10;
    [SerializeField] private bool allowNegativeTraits = true;
    [SerializeField] private int maxTraitsPerType = 10;


    [Header("Starting Traits")]
    [SerializeField] private List<Trait> startingTraits = new List<Trait>();

    [Header("Class Integration")]
    [SerializeField] private bool enforceClassRestrictions = true;
    [SerializeField] private bool allowTraitRemoval = true;
    [SerializeField] private float traitRemovalCostMultiplier = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    // Events
    public event Action<Trait> OnTraitAdded;
    public event Action<Trait> OnTraitRemoved;
    public event Action<int> OnTraitPointsChanged;
    public event Action<Trait> OnTraitExpired;

    [Header("Armor Set Integration")]
    [SerializeField] private ArmorSetManager armorSetManager;
    [SerializeField] private Dictionary<Trait, float> traitMultipliers = new Dictionary<Trait, float>();
    [SerializeField] private List<Trait> armorAppliedTraits = new List<Trait>();

    // Events for trait enhancement
    public event Action<Trait, float> OnTraitMultiplierChanged;
    public event Action<Trait, List<TraitEffect>> OnTraitEffectsAdded;

    // Properties for armor set integration
    public ArmorSetManager ArmorSetManager => armorSetManager;
    public Dictionary<Trait, float> TraitMultipliers => new Dictionary<Trait, float>(traitMultipliers);


    // Properties
    public List<Trait> ActiveTraits => activeTraits.Where(t => t.isActive).Select(t => t.trait).ToList();
    public List<ActiveTraitInfo> ActiveTraitInfos => activeTraits.Where(t => t.isActive).ToList();
    public int AvailableTraitPoints => availableTraitPoints;
    public TraitDatabase Database => traitDatabase;

    private PlayerStatusController playerController;
    private ExperienceManager experienceManager;

    private void Awake()
    {
        playerController = GetComponent<PlayerStatusController>();
        experienceManager = GetComponent<ExperienceManager>();

        if (traitDatabase == null)
            traitDatabase = TraitDatabase.Instance;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        ApplyStartingTraits();
        StartCoroutine(UpdateTemporaryTraits());
    }

    private void ApplyStartingTraits()
    {
        foreach (var trait in startingTraits)
        {
            if (trait != null)
                AddTrait(trait, true);
        }
    }

    private System.Collections.IEnumerator UpdateTemporaryTraits()
    {
        while (true)
        {
            var expiredTraits = activeTraits.Where(t =>
                t.isTemporary &&
                t.isActive &&
                Time.time - t.timeAcquired >= t.duration
            ).ToList();

            foreach (var expiredTrait in expiredTraits)
            {
                RemoveTemporaryTrait(expiredTrait);
            }

            yield return new WaitForSeconds(1f);
        }
    }

    // Enhanced trait validation - SIMPLIFIED VERSION
    public bool CanAddTrait(Trait trait)
    {
        if (trait == null) return false;
        if (traitDatabase != null && !traitDatabase.IsValidTrait(trait)) return false;
        if (HasTrait(trait)) return false;

        // Check trait points
        if (trait.cost > availableTraitPoints) return false;

        // Check negative trait permission
        if (!allowNegativeTraits && trait.IsNegative) return false;

        // SIMPLIFIED: Check class restrictions using PlayerClass as source of truth
        if (enforceClassRestrictions && playerController?.CurrentPlayerClass != null)
        {
            if (!playerController.CurrentPlayerClass.CanSelectTrait(trait))
                return false;
        }

        // Check trait type limits
        var traitsOfSameType = GetTraitsByType(trait.type);
        if (traitsOfSameType.Count >= maxTraitsPerType)
            return false;

        // Check incompatible traits using TraitDatabase methods
        var activeTraits = ActiveTraits;
        foreach (var activeTrait in activeTraits)
        {
            if (traitDatabase != null && !traitDatabase.AreTraitsCompatible(trait, activeTrait))
                return false;
        }

        // Check mutually exclusive traits
        if (trait.mutuallyExclusiveTraits != null)
        {
            foreach (var exclusiveTrait in trait.mutuallyExclusiveTraits)
            {
                if (HasTrait(exclusiveTrait))
                    return false;
            }
        }

        // Check required traits using TraitDatabase methods
        if (traitDatabase != null && !traitDatabase.HasRequiredTraits(trait, activeTraits))
            return false;

        return true;
    }

    // Add a trait with enhanced validation
    public bool AddTrait(Trait trait, bool skipCost = false, bool temporary = false, float duration = 0f)
    {
        if (!CanAddTrait(trait) && !skipCost)
        {
            Debug.LogWarning($"Cannot add trait {trait.Name}: Requirements not met");
            return false;
        }

        var traitInfo = new ActiveTraitInfo(trait, temporary, duration);
        activeTraits.Add(traitInfo);

        if (!skipCost)
        {
            // Use PlayerClass to get the actual cost (considering class modifiers)
            int actualCost = playerController?.CurrentPlayerClass?.GetTraitCost(trait) ?? trait.cost;
            availableTraitPoints -= actualCost;
        }

        ApplyTraitEffects(trait);
        PlayTraitSound(trait);

        OnTraitAdded?.Invoke(trait);
        OnTraitPointsChanged?.Invoke(availableTraitPoints);

        Debug.Log($"Added trait: {trait.Name} (Cost: {trait.cost})");
        return true;
    }

    // Enhanced trait removal with cost consideration
    public bool RemoveTrait(Trait trait, bool forceRemove = false)
    {
        if (!allowTraitRemoval && !forceRemove)
        {
            Debug.LogWarning("Trait removal is not allowed");
            return false;
        }

        var traitInfo = activeTraits.FirstOrDefault(t => t.trait == trait && t.isActive);
        if (traitInfo == null) return false;

        // Check if other traits depend on this one
        var dependentTraits = ActiveTraits.Where(t =>
            t.requiredTraits != null && t.requiredTraits.Contains(trait)
        ).ToList();

        if (dependentTraits.Count > 0 && !forceRemove)
        {
            Debug.LogWarning($"Cannot remove trait {trait.Name} - other traits depend on it: {string.Join(", ", dependentTraits.Select(t => t.Name))}");
            return false;
        }

        // Calculate refund (partial for non-starting traits)
        int actualCost = playerController?.CurrentPlayerClass?.GetTraitCost(trait) ?? trait.cost;
        int refund = traitInfo.isTemporary ? 0 : Mathf.RoundToInt(actualCost * traitRemovalCostMultiplier);

        traitInfo.isActive = false;
        availableTraitPoints += refund;

        RemoveTraitEffects(trait);

        OnTraitRemoved?.Invoke(trait);
        OnTraitPointsChanged?.Invoke(availableTraitPoints);

        Debug.Log($"Removed trait: {trait.Name} (Refunded: {refund} points)");
        return true;
    }

    private void RemoveTemporaryTrait(ActiveTraitInfo traitInfo)
    {
        traitInfo.isActive = false;
        RemoveTraitEffects(traitInfo.trait);
        OnTraitExpired?.Invoke(traitInfo.trait);
        Debug.Log($"Temporary trait expired: {traitInfo.trait.Name}");
    }

    // Enhanced trait queries
    public bool HasTrait(Trait trait)
    {
        return activeTraits.Any(t => t.trait == trait && t.isActive);
    }

    public List<Trait> GetTraitsByType(TraitType type)
    {
        return ActiveTraits.Where(t => t.type == type).ToList();
    }

    public List<Trait> GetTraitsByRarity(TraitRarity rarity)
    {
        return ActiveTraits.Where(t => t.rarity == rarity).ToList();
    }

    // SIMPLIFIED: Get available traits from PlayerClass instead of complex validation
    public List<Trait> GetAvailableTraitsForClass(PlayerClass playerClass)
    {
        if (!enforceClassRestrictions || playerClass == null)
            return GetAvailableTraits();

        return playerClass.GetSelectableTraits().Where(CanAddTrait).ToList();
    }

    public List<Trait> GetAvailableTraits()
    {
        if (traitDatabase == null) return new List<Trait>();
        return traitDatabase.GetAllTraits().Where(CanAddTrait).ToList();
    }

    public List<Trait> GetAvailableTraitsByType(TraitType type)
    {
        if (traitDatabase == null) return new List<Trait>();
        return traitDatabase.GetTraitsByType(type).Where(CanAddTrait).ToList();
    }

    public List<Trait> GetAffordableTraits()
    {
        if (traitDatabase == null) return new List<Trait>();
        return traitDatabase.GetTraitsByCostRange(int.MinValue, availableTraitPoints)
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

    // Apply/Remove trait effects (enhanced version)
    private void ApplyTraitEffects(Trait trait)
    {
        if (playerController == null) return;

        foreach (var effect in trait.effects)
        {
            ApplyTraitEffect(effect, trait);
        }
    }

    private void RemoveTraitEffects(Trait trait)
    {
        if (playerController == null) return;

        foreach (var effect in trait.effects)
        {
            RemoveTraitEffect(effect, trait);
        }
    }

    private void ApplyTraitEffect(TraitEffect effect, Trait trait)
    {
        switch (effect.targetStat.ToLower())
        {
            case "health":
                ApplyHealthEffect(effect);
                break;
            case "stamina":
                ApplyStaminaEffect(effect);
                break;
            case "mana":
                ApplyManaEffect(effect);
                break;
            case "speed":
                ApplySpeedEffect(effect);
                break;
            case "damage":
                ApplyDamageEffect(effect, trait);
                break;
            case "defense":
                ApplyDefenseEffect(effect);
                break;
            default:
                Debug.LogWarning($"Trait effect for {effect.targetStat} not implemented yet");
                break;
        }
    }

    private void RemoveTraitEffect(TraitEffect effect, Trait trait)
    {
        switch (effect.targetStat.ToLower())
        {
            case "health":
                RemoveHealthEffect(effect);
                break;
            case "stamina":
                RemoveStaminaEffect(effect);
                break;
            case "mana":
                RemoveManaEffect(effect);
                break;
            case "speed":
                RemoveSpeedEffect(effect);
                break;
        }
    }

    // Specific effect methods (same as before but with enhanced trait reference)
    private void ApplyHealthEffect(TraitEffect effect)
    {
        switch (effect.effectType)
        {
            case TraitEffectType.StatMultiplier:
                float bonus = playerController.HpManager.MaxValue * (effect.value - 1f);
                playerController.HpManager.ModifyMaxValue(bonus);
                break;
            case TraitEffectType.StatAddition:
                playerController.HpManager.ModifyMaxValue(effect.value);
                break;
            case TraitEffectType.RegenerationRate:
                playerController.HpManager.ModifyIncrementFactor(effect.value - 1f);
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
                playerController.HpManager.ModifyIncrementFactor(-(effect.value - 1f));
                break;
        }
    }

    private void ApplyStaminaEffect(TraitEffect effect)
    {
        switch (effect.effectType)
        {
            case TraitEffectType.StatMultiplier:
                float bonus = playerController.StaminaManager.MaxValue * (effect.value - 1f);
                playerController.StaminaManager.ModifyMaxValue(bonus);
                break;
            case TraitEffectType.StatAddition:
                playerController.StaminaManager.ModifyMaxValue(effect.value);
                break;
            case TraitEffectType.RegenerationRate:
                playerController.StaminaManager.ModifyIncrementFactor(effect.value - 1f);
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
        }
    }

    private void ApplyManaEffect(TraitEffect effect)
    {
        switch (effect.effectType)
        {
            case TraitEffectType.StatMultiplier:
                float bonus = playerController.ManaManager.MaxValue * (effect.value - 1f);
                playerController.ManaManager.ModifyMaxValue(bonus);
                break;
            case TraitEffectType.StatAddition:
                playerController.ManaManager.ModifyMaxValue(effect.value);
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
        }
    }

    private void ApplySpeedEffect(TraitEffect effect)
    {
        switch (effect.effectType)
        {
            case TraitEffectType.StatMultiplier:
                float bonus = playerController.SpeedManager.BaseSpeed * (effect.value - 1f);
                playerController.SpeedManager.ModifyBaseSpeed(bonus);
                break;
            case TraitEffectType.StatAddition:
                playerController.SpeedManager.ModifyBaseSpeed(effect.value);
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

    private void ApplyDamageEffect(TraitEffect effect, Trait trait)
    {
        Debug.Log($"Applied damage effect from trait {trait.Name}: {effect.value}");
    }

    private void ApplyDefenseEffect(TraitEffect effect)
    {
        Debug.Log($"Applied defense effect: {effect.value}");
    }

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

    // Debug methods
    [ContextMenu("Add 5 Trait Points")]
    private void DebugAddTraitPoints()
    {
        AddTraitPoints(5);
    }

    [ContextMenu("Clear All Traits")]
    private void DebugClearTraits()
    {
        ClearAllTraits();
    }













    // Enhanced trait validation that considers armor sets
    public bool CanAddTraitWithArmorSets(Trait trait)
    {
        if (!CanAddTrait(trait)) return false;

        // Check if this trait would conflict with any active armor set enhancements
        if (armorSetManager != null)
        {
            var activeSets = armorSetManager.GetActiveSets();
            foreach (var armorSet in activeSets)
            {
                var activeEffects = armorSetManager.GetActiveSetEffects(armorSet);
                foreach (var effect in activeEffects)
                {
                    // Check if this trait conflicts with any set enhancements
                    var affectedTraits = effect.GetAffectedTraits();
                    if (affectedTraits.Contains(trait))
                    {
                        // Allow it if it's being enhanced, deny if it conflicts
                        var enhancement = effect.traitEnhancements.FirstOrDefault(e => e.originalTrait == trait);
                        if (enhancement != null && enhancement.enhancementType == TraitEnhancementType.Replace)
                        {
                            return false; // Can't add trait that's being replaced
                        }
                    }
                }
            }
        }

        return true;
    }

    // Add trait with armor consideration
    public bool AddTraitFromArmor(Trait trait, ArmorSO armor = null)
    {
        if (trait == null) return false;

        // Armor traits are always free and don't count against limits
        var traitInfo = new ActiveTraitInfo(trait, false, 0f);
        activeTraits.Add(traitInfo);
        armorAppliedTraits.Add(trait);

        ApplyTraitEffects(trait);

        OnTraitAdded?.Invoke(trait);

        Debug.Log($"Added armor trait: {trait.Name}" + (armor != null ? $" from {armor.name}" : ""));
        return true;
    }

    // Remove trait applied by armor
    public bool RemoveArmorTrait(Trait trait)
    {
        if (!armorAppliedTraits.Contains(trait)) return false;

        var traitInfo = activeTraits.FirstOrDefault(t => t.trait == trait && t.isActive);
        if (traitInfo == null) return false;

        traitInfo.isActive = false;
        armorAppliedTraits.Remove(trait);

        RemoveTraitEffects(trait);

        OnTraitRemoved?.Invoke(trait);

        Debug.Log($"Removed armor trait: {trait.Name}");
        return true;
    }

    // Set trait multiplier (for armor set enhancements)
    public void SetTraitMultiplier(Trait trait, float multiplier)
    {
        if (trait == null) return;

        if (multiplier == 1f)
        {
            traitMultipliers.Remove(trait);
        }
        else
        {
            traitMultipliers[trait] = multiplier;
        }

        OnTraitMultiplierChanged?.Invoke(trait, multiplier);

        // Reapply trait effects with new multiplier
        if (HasTrait(trait))
        {
            RefreshTraitEffects(trait);
        }
    }

    // Get effective trait multiplier
    public float GetTraitMultiplier(Trait trait)
    {
        if (trait == null) return 1f;
        return traitMultipliers.TryGetValue(trait, out float multiplier) ? multiplier : 1f;
    }

    // Refresh trait effects (useful when multipliers change)
    public void RefreshTraitEffects(Trait trait)
    {
        if (!HasTrait(trait)) return;

        // Remove and reapply effects
        RemoveTraitEffects(trait);
        ApplyTraitEffects(trait);
    }

    // Check if trait is applied by armor
    public bool IsArmorTrait(Trait trait)
    {
        return armorAppliedTraits.Contains(trait);
    }

    // Get all armor-applied traits
    public List<Trait> GetArmorTraits()
    {
        return new List<Trait>(armorAppliedTraits);
    }

    // Enhanced trait effect application that considers multipliers
    private void ApplyTraitEffectWithMultiplier(TraitEffect effect, Trait trait)
    {
        float multiplier = GetTraitMultiplier(trait);
        float adjustedValue = effect.value * multiplier;

        switch (effect.targetStat.ToLower())
        {
            case "health":
                ApplyHealthEffectWithValue(effect, adjustedValue);
                break;
            case "stamina":
                ApplyStaminaEffectWithValue(effect, adjustedValue);
                break;
            case "mana":
                ApplyManaEffectWithValue(effect, adjustedValue);
                break;
            case "speed":
                ApplySpeedEffectWithValue(effect, adjustedValue);
                break;
            default:
                Debug.LogWarning($"Trait effect for {effect.targetStat} not implemented yet");
                break;
        }
    }

    // Helper methods for applying effects with custom values
    private void ApplyHealthEffectWithValue(TraitEffect effect, float value)
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
                playerController.HpManager.ModifyIncrementFactor(value - 1f);
                break;
        }
    }

    private void ApplyStaminaEffectWithValue(TraitEffect effect, float value)
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
                playerController.StaminaManager.ModifyIncrementFactor(value - 1f);
                break;
        }
    }

    private void ApplyManaEffectWithValue(TraitEffect effect, float value)
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
        }
    }

    private void ApplySpeedEffectWithValue(TraitEffect effect, float value)
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

    // Override the existing ApplyTraitEffects method to use multipliers
    private void ApplyTraitEffectsEnhanced(Trait trait)
    {
        if (playerController == null) return;

        foreach (var effect in trait.effects)
        {
            ApplyTraitEffectWithMultiplier(effect, trait);
        }
    }

    // Add validation for armor set trait conflicts
    public List<string> ValidateTraitCompatibilityWithArmorSets()
    {
        var issues = new List<string>();

        if (armorSetManager == null) return issues;

        var activeSets = armorSetManager.GetActiveSets();
        var activeTraits = ActiveTraits;

        foreach (var armorSet in activeSets)
        {
            var activeEffects = armorSetManager.GetActiveSetEffects(armorSet);

            foreach (var effect in activeEffects)
            {
                // Check for trait conflicts
                foreach (var enhancement in effect.traitEnhancements)
                {
                    if (enhancement.originalTrait == null) continue;

                    bool hasOriginalTrait = activeTraits.Contains(enhancement.originalTrait);

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

                    var conflicts = activeTraits.Where(t =>
                        t.incompatibleTraits?.Contains(newTrait) == true ||
                        newTrait.incompatibleTraits?.Contains(t) == true
                    ).ToList();

                    if (conflicts.Count > 0)
                    {
                        issues.Add($"Set {armorSet.SetName} trait {newTrait.Name} conflicts with: {string.Join(", ", conflicts.Select(t => t.Name))}");
                    }
                }
            }
        }

        return issues;
    }

    // Get enhanced trait description (includes multipliers and enhancements)
    public string GetEnhancedTraitDescription(Trait trait)
    {
        if (trait == null) return "";

        string description = trait.GetFormattedDescription();

        float multiplier = GetTraitMultiplier(trait);
        if (multiplier != 1f)
        {
            description += $"\n\n[SET BONUS] Effects multiplied by {multiplier:F1}x";
        }

        // Check if this trait is enhanced by any armor sets
        if (armorSetManager != null)
        {
            bool hasEnhancements = false;
            var activeSets = armorSetManager.GetActiveSets();

            foreach (var armorSet in activeSets)
            {
                var activeEffects = armorSetManager.GetActiveSetEffects(armorSet);

                foreach (var effect in activeEffects)
                {
                    var enhancement = effect.traitEnhancements.FirstOrDefault(e => e.originalTrait == trait);
                    if (enhancement != null)
                    {
                        if (!hasEnhancements)
                        {
                            description += "\n\n[SET ENHANCEMENTS]";
                            hasEnhancements = true;
                        }

                        string enhancementDesc = enhancement.enhancementType switch
                        {
                            TraitEnhancementType.Multiply => $"Enhanced by {armorSet.SetName} (x{enhancement.effectMultiplier})",
                            TraitEnhancementType.AddEffects => $"Additional effects from {armorSet.SetName}",
                            TraitEnhancementType.Replace => $"Temporarily replaced by {armorSet.SetName}",
                            TraitEnhancementType.Upgrade => $"Upgraded by {armorSet.SetName}",
                            _ => $"Modified by {armorSet.SetName}"
                        };

                        description += $"\n• {enhancementDesc}";
                    }
                }
            }
        }

        return description;
    }

    // Clear all armor-applied traits (useful when removing all armor)
    public void ClearAllArmorTraits()
    {
        var armorTraitsToRemove = new List<Trait>(armorAppliedTraits);

        foreach (var trait in armorTraitsToRemove)
        {
            RemoveArmorTrait(trait);
        }

        Debug.Log($"Cleared {armorTraitsToRemove.Count} armor traits");
    }

    // Debug method to show trait enhancement status
    [ContextMenu("Log Trait Enhancement Status")]
    private void LogTraitEnhancementStatus()
    {
        Debug.Log("=== Trait Enhancement Status ===");

        foreach (var trait in ActiveTraits)
        {
            float multiplier = GetTraitMultiplier(trait);
            bool isArmorTrait = IsArmorTrait(trait);

            string status = $"{trait.Name}: ";
            if (isArmorTrait) status += "[ARMOR] ";
            if (multiplier != 1f) status += $"[ENHANCED x{multiplier:F1}] ";

            Debug.Log(status);
        }

        if (traitMultipliers.Count > 0)
        {
            Debug.Log("\nActive Multipliers:");
            foreach (var kvp in traitMultipliers)
            {
                Debug.Log($"  {kvp.Key.Name}: {kvp.Value:F1}x");
            }
        }
    }
}