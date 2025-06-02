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

    // Enhanced trait validation
    public bool CanAddTrait(Trait trait)
    {
        if (trait == null) return false;
        if (!traitDatabase.IsValidTrait(trait)) return false;
        if (HasTrait(trait)) return false;

        // Check trait points
        if (trait.cost > availableTraitPoints) return false;

        // Check negative trait permission
        if (!allowNegativeTraits && trait.IsNegative) return false;

        // Check class restrictions
        if (enforceClassRestrictions && playerController?.CurrentPlayerClass != null)
        {
            if (!trait.IsAvailableForClass(playerController.CurrentPlayerClass))
                return false;
        }

        // Check level restrictions
        if (experienceManager != null)
        {
            if (!trait.IsAvailableForLevel(experienceManager.CurrentLevel))
                return false;
        }

        // Check stat requirements
        if (!trait.MeetsStatRequirements(playerController))
            return false;

        // Check trait type limits
        var traitsOfSameType = GetTraitsByType(trait.type);
        if (traitsOfSameType.Count >= maxTraitsPerType)
            return false;

        // Check incompatible traits
        var activeTraits = ActiveTraits;
        foreach (var activeTrait in activeTraits)
        {
            if (!traitDatabase.AreTraitsCompatible(trait, activeTrait))
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

        // Check required traits
        if (!traitDatabase.HasRequiredTraits(trait, activeTraits))
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
            availableTraitPoints -= trait.cost;

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
        int refund = traitInfo.isTemporary ? 0 : Mathf.RoundToInt(trait.cost * traitRemovalCostMultiplier);

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

    public List<Trait> GetAvailableTraitsForClass(PlayerClass playerClass)
    {
        if (!enforceClassRestrictions || playerClass == null)
            return GetAvailableTraits();

        return traitDatabase.GetAllTraits()
            .Where(t => t.IsAvailableForClass(playerClass) && CanAddTrait(t))
            .ToList();
    }

    public List<Trait> GetAvailableTraits()
    {
        return traitDatabase.GetAllTraits().Where(CanAddTrait).ToList();
    }

    public List<Trait> GetAvailableTraitsByType(TraitType type)
    {
        return traitDatabase.GetTraitsByType(type).Where(CanAddTrait).ToList();
    }

    public List<Trait> GetAffordableTraits()
    {
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
}