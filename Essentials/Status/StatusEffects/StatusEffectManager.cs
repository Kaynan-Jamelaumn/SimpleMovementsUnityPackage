// Status effect manager - central hub for all status modifications
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


[System.Serializable]
public class ActiveStatusEffect
{
    public UnifiedStatusEffect effect;
    public string sourceId;
    public string effectName;
    public float startTime;
    public float duration;

    public bool IsExpired => duration > 0 && Time.time - startTime >= duration;
    public float RemainingTime => Mathf.Max(0, duration - (Time.time - startTime));
}

public class StatusEffectManager : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private PlayerStatusController playerController;

    [Header("Effect Tracking")]
    [SerializeField] private List<ActiveStatusEffect> activeEffects = new List<ActiveStatusEffect>();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = false;

    private Dictionary<UnifiedStatusType, System.Action<float, bool>> statusActions;
    private Dictionary<UnifiedStatusType, System.Action<string, float, float, float, bool, bool>> timedStatusActions;

    public static StatusEffectManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (playerController == null)
            playerController = GetComponent<PlayerStatusController>();

        InitializeStatusActions();
    }

    private void InitializeStatusActions()
    {
        // Immediate effect actions
        statusActions = new Dictionary<UnifiedStatusType, System.Action<float, bool>>
        {
            { UnifiedStatusType.Hp, (amount, apply) => SafeApply(() => playerController.HpManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.Stamina, (amount, apply) => SafeApply(() => playerController.StaminaManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.Mana, (amount, apply) => SafeApply(() => playerController.ManaManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.Food, (amount, apply) => SafeApply(() => playerController.HungerManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.Drink, (amount, apply) => SafeApply(() => playerController.ThirstManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.Sleep, (amount, apply) => SafeApply(() => playerController.SleepManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.Sanity, (amount, apply) => SafeApply(() => playerController.SanityManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.BodyHeat, (amount, apply) => SafeApply(() => playerController.BodyHeatManager.ModifyBodyHeat(apply ? amount : -amount)) },
            { UnifiedStatusType.Oxygen, (amount, apply) => SafeApply(() => playerController.OxygenManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.Speed, (amount, apply) => SafeApply(() => playerController.SpeedManager.ModifySpeed(apply ? amount : -amount)) },
            
            // Max Values
            { UnifiedStatusType.MaxHp, (amount, apply) => SafeApply(() => playerController.HpManager.ModifyMaxValue(apply ? amount : -amount)) },
            { UnifiedStatusType.MaxStamina, (amount, apply) => SafeApply(() => playerController.StaminaManager.ModifyMaxValue(apply ? amount : -amount)) },
            { UnifiedStatusType.MaxMana, (amount, apply) => SafeApply(() => playerController.ManaManager.ModifyMaxValue(apply ? amount : -amount)) }
        };

        // Timed effect actions
        timedStatusActions = new Dictionary<UnifiedStatusType, System.Action<string, float, float, float, bool, bool>>
        {
            { UnifiedStatusType.Hp, (name, amount, duration, cooldown, procedural, stackable) => playerController.HpManager.AddHpEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.Stamina, (name, amount, duration, cooldown, procedural, stackable) => playerController.StaminaManager.AddStaminaEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.Mana, (name, amount, duration, cooldown, procedural, stackable) => playerController.ManaManager.AddManaEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.Food, (name, amount, duration, cooldown, procedural, stackable) => playerController.HungerManager.AddFoodEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.Drink, (name, amount, duration, cooldown, procedural, stackable) => playerController.ThirstManager.AddDrinkEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.Sleep, (name, amount, duration, cooldown, procedural, stackable) => playerController.SleepManager.AddSleepEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.HpRegeneration, (name, amount, duration, cooldown, procedural, stackable) => playerController.HpManager.AddHpRegenEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.StaminaRegeneration, (name, amount, duration, cooldown, procedural, stackable) => playerController.StaminaManager.AddStaminaRegenEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.HpHealFactor, (name, amount, duration, cooldown, procedural, stackable) => playerController.HpManager.AddHpHealFactorEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.SpeedFactor, (name, amount, duration, cooldown, procedural, stackable) => playerController.SpeedManager.AddSpeedFactorEffect(name, amount, duration, cooldown, procedural, stackable) }
        };
    }

    // Main method to apply any status effect
    public void ApplyStatusEffect(UnifiedStatusEffect effect, string sourceId = "")
    {
        if (effect == null || !effect.ShouldApply(playerController.XPManager?.CurrentLevel ?? 1))
        {
            LogDebug($"Effect {effect?.GetEffectName()} not applied - failed conditions");
            return;
        }

        float amount = effect.GetAmount();
        float duration = effect.GetDuration();
        float cooldown = effect.GetTickCooldown();
        string effectName = string.IsNullOrEmpty(sourceId) ? effect.GetEffectName() : $"{sourceId}_{effect.GetEffectName()}";

        LogDebug($"Applying effect: {effectName}, Amount: {amount}, Duration: {duration}");

        // Play effects
        PlayEffectAudio(effect);
        ShowEffectVisual(effect);

        // Apply the effect
        if (duration <= 0)
        {
            ApplyImmediateStatusEffect(effect.statusType, amount);
        }
        else
        {
            ApplyTimedStatusEffect(effect.statusType, effectName, amount, duration, cooldown, effect.IsProcedural(), effect.IsStackable());
            TrackActiveEffect(effect, sourceId, effectName);
        }
    }

    public void RemoveEffectsFromSource(string sourceId)
    {
        var effectsToRemove = activeEffects.Where(e => e.sourceId == sourceId).ToList();
        foreach (var effect in effectsToRemove)
        {
            RemoveStatusEffect(effect.effectName);
        }
    }

    private void ApplyImmediateStatusEffect(UnifiedStatusType statusType, float amount)
    {
        if (statusActions.TryGetValue(statusType, out var action))
        {
            action.Invoke(amount, true);
        }
        else
        {
            LogDebug($"No immediate action defined for status type: {statusType}");
        }
    }

    private void ApplyTimedStatusEffect(UnifiedStatusType statusType, string effectName, float amount, float duration, float cooldown, bool isProcedural, bool isStackable)
    {
        if (timedStatusActions.TryGetValue(statusType, out var action))
        {
            action.Invoke(effectName, amount, duration, cooldown, isProcedural, isStackable);
        }
        else
        {
            LogDebug($"No timed action defined for status type: {statusType}");
        }
    }

    private void RemoveStatusEffect(string effectName)
    {
        // Remove from all managers
        playerController.HpManager.RemoveHpEffect(effectName);
        playerController.StaminaManager.RemoveStaminaEffect(effectName);
        playerController.ManaManager.RemoveManaEffect(effectName);

        activeEffects.RemoveAll(e => e.effectName == effectName);
    }

    private void TrackActiveEffect(UnifiedStatusEffect effect, string sourceId, string effectName)
    {
        var activeEffect = new ActiveStatusEffect
        {
            effect = effect,
            sourceId = sourceId,
            effectName = effectName,
            startTime = Time.time,
            duration = effect.GetDuration()
        };

        activeEffects.Add(activeEffect);
    }

    private void SafeApply(System.Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error applying status effect: {e.Message}");
        }
    }

    private void PlayEffectAudio(UnifiedStatusEffect effect)
    {
        if (effect.effectSound != null)
        {
            var audioSource = GetComponent<AudioSource>() ?? playerController.GetComponent<AudioSource>();
            if (audioSource != null)
                audioSource.PlayOneShot(effect.effectSound);
        }
    }

    private void ShowEffectVisual(UnifiedStatusEffect effect)
    {
        if (effect.effectPrefab != null)
        {
            var visual = Instantiate(effect.effectPrefab, playerController.transform.position, Quaternion.identity);
            Destroy(visual, 3f);
        }
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogging)
            Debug.Log($"[StatusEffectManager] {message}");
    }

    public List<ActiveStatusEffect> GetActiveEffects() => new List<ActiveStatusEffect>(activeEffects);
}