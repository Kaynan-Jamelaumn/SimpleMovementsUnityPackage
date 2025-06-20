using System.Collections.Generic;
using UnityEngine;

public static class EquippableSOExtensions
{
    public static void ApplyUnifiedEffects(this EquippableSO equippable, List<UnifiedStatusEffect> effects, PlayerStatusController statusController, bool isApplying = true)
    {
        if (StatusEffectManager.Instance == null)
        {
            Debug.LogError("StatusEffectManager not found!");
            return;
        }

        string itemId = $"equippable_{equippable.name}";

        if (isApplying)
        {
            foreach (var effect in effects)
            {
                StatusEffectManager.Instance.ApplyStatusEffect(effect, itemId);
            }
        }
        else
        {
            StatusEffectManager.Instance.RemoveEffectsFromSource(itemId);
        }
    }
}