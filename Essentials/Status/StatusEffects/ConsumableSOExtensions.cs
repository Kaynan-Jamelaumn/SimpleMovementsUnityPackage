using System.Collections.Generic;
using UnityEngine;

public static class ConsumableSOExtensions
{
    public static void ApplyUnifiedEffects(this ConsumableSO consumable, List<UnifiedStatusEffect> effects, PlayerStatusController statusController)
    {
        if (StatusEffectManager.Instance == null)
        {
            Debug.LogError("StatusEffectManager not found!");
            return;
        }

        string itemId = $"consumable_{consumable.name}_{Time.time}";

        foreach (var effect in effects)
        {
            StatusEffectManager.Instance.ApplyStatusEffect(effect, itemId);
        }
    }
}