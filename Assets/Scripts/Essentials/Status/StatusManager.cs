using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages status effects for game objects.
/// </summary>
public class StatusManager : MonoBehaviour
{
    /// <summary>
    /// Applies a factor to the given amount.
    /// </summary>
    /// <param name="amount">The original amount.</param>
    /// <param name="factor">The factor to apply.</param>
    /// <returns>The amount after applying the factor.</returns>
    protected float ApplyFactor(float amount, float factor)
    {
        float percentageFactor = 1.0f + factor / 100.0f; // Convert percentage to a factor
        amount *= percentageFactor;
        return amount;
    }

    /// <summary>
    /// Adds an effect to the list of effects.
    /// </summary>
    /// <param name="effectList">The list of current effects.</param>
    /// <param name="effectName">The name of the effect.</param>
    /// <param name="amount">The amount of the effect.</param>
    /// <param name="timeBuffEffect">Duration of the effect.</param>
    /// <param name="tickCooldown">Cooldown between ticks of the effect.</param>
    /// <param name="isProcedural">Whether the effect is procedural.</param>
    /// <param name="isStackable">Whether the effect is stackable.</param>
    /// <param name="applyEffectRoutine">The coroutine that applies the effect.</param>
    protected void AddEffect(
        List<CoroutineInfo> effectList,
        string effectName,
        float amount,
        float timeBuffEffect,
        float tickCooldown,
        bool isProcedural = false,
        bool isStackable = false,
        Func<string, float, float, float, bool, bool, IEnumerator> applyEffectRoutine = null
    )
    {
        if (!isStackable)
        {
            // Check if there is already an effect with the same effectName
            CoroutineInfo existingEffect = effectList.Find(e => e.effectName == effectName);
            if (existingEffect != null)
            {
                // Stop the existing coroutine and remove it from the list
                StopCoroutine(existingEffect.coroutine);
                effectList.Remove(existingEffect);
            }
        }

        Coroutine effectRoutine = null;
        effectRoutine = StartCoroutine(applyEffectRoutine(effectName, amount, timeBuffEffect, tickCooldown, isProcedural, isStackable));

        CoroutineInfo coroutineInfo = new CoroutineInfo
        {
            coroutine = effectRoutine,
            effectName = effectName,
            isBuff = (amount > 0)
        };

        effectList.Add(coroutineInfo);

        // Order list elements by effectName
        effectList = effectList.OrderBy(c => c.effectName).ToList();
    }

    /// <summary>
    /// Removes an effect from the list based on effect name or routine.
    /// </summary>
    /// <param name="effectList">The list of current effects.</param>
    /// <param name="effectName">The name of the effect to remove.</param>
    /// <param name="effectRoutine">The coroutine of the effect to remove.</param>
    protected void RemoveEffect(List<CoroutineInfo> effectList, string effectName = null, Coroutine effectRoutine = null)
    {
        // Remove effect based on effectName or effectRoutine
        CoroutineInfo effectToRemove = null;
        if (effectName != null)
        {
            effectToRemove = effectList.Find(e => e.effectName == effectName);
        }
        else if (effectRoutine != null)
        {
            effectToRemove = effectList.Find(e => e.coroutine == effectRoutine);
        }

        if (effectToRemove != null)
        {
            StopCoroutine(effectToRemove.coroutine);
            effectList.Remove(effectToRemove);
        }
    }

    /// <summary>
    /// Stops all effects in the list.
    /// </summary>
    /// <param name="effectRoutines">The list of effect routines.</param>
    protected void StopAllEffects(List<CoroutineInfo> effectRoutines)
    {
        foreach (CoroutineInfo routine in effectRoutines)
        {
            StopCoroutine(routine.coroutine);
        }
        effectRoutines.Clear();
    }

    /// <summary>
    /// Stops all effects of a certain type (buff or debuff).
    /// </summary>
    /// <param name="effectRoutines">The list of effect routines.</param>
    /// <param name="isBuff">Specifies if the effects to stop are buffs.</param>
    public void StopAllEffectsByType(List<CoroutineInfo> effectRoutines, bool isBuff = true)
    {
        List<CoroutineInfo> routinesToRemove = effectRoutines
            .Where(info => (isBuff && info.isBuff) || (!isBuff && !info.isBuff))
            .ToList();

        foreach (CoroutineInfo coroutineInfo in routinesToRemove)
        {
            StopCoroutine(coroutineInfo.coroutine);
            effectRoutines.Remove(coroutineInfo);
        }
    }

    /// <summary>
    /// Updates a UI bar to reflect a value relative to a maximum value.
    /// </summary>
    /// <param name="value">The current value.</param>
    /// <param name="maxValue">The maximum value.</param>
    /// <param name="barImage">The UI Image component representing the bar.</param>
    protected void UpdateBar(float value, float maxValue, UnityEngine.UI.Image barImage)
    {
        // Ensure that the value does not exceed limits
        value = Mathf.Clamp(value, 0f, maxValue);

        // Calculate the percentage of the remaining value
        float percentage = value / maxValue;
        barImage.fillAmount = percentage;
    }
}
