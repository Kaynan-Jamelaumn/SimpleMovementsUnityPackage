// For WeaponSO
using System.Collections.Generic;
using UnityEngine;

public static class WeaponSOExtensions
{
    public static void ApplyUnifiedAttackEffects(this WeaponSO weapon, List<UnifiedStatusEffect> effects, PlayerStatusController targetController, PlayerStatusController sourceController)
    {
        if (StatusEffectManager.Instance == null || effects == null)
        {
            Debug.LogError("StatusEffectManager not found or no effects!");
            return;
        }

        string weaponId = $"weapon_{weapon.name}_{Time.time}";

        foreach (var effect in effects)
        {
            StatusEffectManager.Instance.ApplyStatusEffect(effect, weaponId);
        }
    }
}