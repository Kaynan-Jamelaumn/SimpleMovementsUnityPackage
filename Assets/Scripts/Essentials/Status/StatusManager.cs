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
    /// Adjusts an amount by applying a percentage factor.
    /// </summary>
    /// <param name="amount">The original amount.</param>
    /// <param name="factor">The percentage factor to apply.</param>
    /// <returns>The adjusted amount.</returns>
    protected float ApplyFactor(float amount, float factor)
    {
        return amount * (1.0f + factor / 100.0f);
    }

    /// <summary>
    /// Adds a new effect to the active effect list.
    /// </summary>
    /// <param name="effectList">The current list of effects.</param>
    /// <param name="effectName">The name of the effect.</param>
    /// <param name="amount">The effect value.</param>
    /// <param name="duration">Duration of the effect in seconds.</param>
    /// <param name="tickInterval">Time between effect ticks.</param>
    /// <param name="isProcedural">True if the effect is procedural.</param>
    /// <param name="isStackable">True if the effect can stack.</param>
    /// <param name="effectRoutineFunc">Coroutine function to apply the effect.</param>
    protected void AddEffect(
        List<CoroutineInfo> effectList,
        string effectName,
        float amount,
        float duration,
        float tickInterval,
        bool isProcedural = false,
        bool isStackable = false,
        Func<string, float, float, float, bool, bool, IEnumerator> effectRoutineFunc = null
    )
    {
        if (effectRoutineFunc == null)
        {
            Debug.LogError("Effect routine function cannot be null.");
            return;
        }

        if (!isStackable)
        {
            // Remove existing effect with the same name
            var existingEffect = effectList.FirstOrDefault(e => e.effectName == effectName);
            if (existingEffect != null)
            {
                StopAndRemoveEffect(effectList, existingEffect);
            }
        }

        Coroutine effectCoroutine = StartCoroutine(effectRoutineFunc(effectName, amount, duration, tickInterval, isProcedural, isStackable));
        effectList.Add(new CoroutineInfo
        {
            coroutine = effectCoroutine,
            effectName = effectName,
            isBuff = amount > 0
        });
    }

    /// <summary>
    /// Removes an effect by its name or coroutine.
    /// </summary>
    /// <param name="effectList">The current list of effects.</param>
    /// <param name="effectName">The name of the effect to remove (optional).</param>
    /// <param name="effectRoutine">The coroutine of the effect to remove (optional).</param>
    protected void RemoveEffect(List<CoroutineInfo> effectList, string effectName = null, Coroutine effectRoutine = null)
    {
        var effectToRemove = effectList.FirstOrDefault(e =>
            (effectName != null && e.effectName == effectName) ||
            (effectRoutine != null && e.coroutine == effectRoutine));

        if (effectToRemove != null)
        {
            StopAndRemoveEffect(effectList, effectToRemove);
        }
    }

    /// <summary>
    /// Stops all effects and clears the effect list.
    /// </summary>
    /// <param name="effectList">The list of effects to stop.</param>
    protected void StopAllEffects(List<CoroutineInfo> effectList)
    {
        foreach (var effect in effectList)
        {
            StopCoroutine(effect.coroutine);
        }
        effectList.Clear();
    }

    /// <summary>
    /// Stops all effects of a specific type (buff or debuff).
    /// </summary>
    /// <param name="effectList">The list of effects.</param>
    /// <param name="isBuff">True to stop buffs, false to stop debuffs.</param>
    public void StopAllEffectsByType(List<CoroutineInfo> effectList, bool isBuff)
    {
        var effectsToStop = effectList.Where(e => e.isBuff == isBuff).ToList();
        foreach (var effect in effectsToStop)
        {
            StopAndRemoveEffect(effectList, effect);
        }
    }

    /// <summary>
    /// Updates a UI bar to represent a value as a fraction of a maximum value.
    /// </summary>
    /// <param name="value">The current value.</param>
    /// <param name="maxValue">The maximum value.</param>
    /// <param name="barImage">The UI Image component representing the bar.</param>
    protected void UpdateBar(float value, float maxValue, UnityEngine.UI.Image barImage)
    {
        if (barImage == null)
        {
            Debug.LogError("Bar image cannot be null.");
            return;
        }

        barImage.fillAmount = Mathf.Clamp01(value / maxValue);
    }

    /// <summary>
    /// Stops and removes an effect from the list.
    /// </summary>
    /// <param name="effectList">The list of effects.</param>
    /// <param name="effect">The effect to stop and remove.</param>
    private void StopAndRemoveEffect(List<CoroutineInfo> effectList, CoroutineInfo effect)
    {
        StopCoroutine(effect.coroutine);
        effectList.Remove(effect);
    }
}
