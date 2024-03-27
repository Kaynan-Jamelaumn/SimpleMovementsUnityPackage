using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StatusManager: MonoBehaviour
{
    protected float ApplyFactor(float amount, float factor)
    {
        float percentageFactor = 1.0f + factor / 100.0f; // Convert percentage to a factor

        amount *= percentageFactor;

        return amount;
    }

    protected void AddEffect(List<CoroutineInfo> effectList, string effectName, float amount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false, Func<string, float, float, float, bool, bool, IEnumerator> applyEffectRoutine = null)
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
            isBuff = (amount > 0) ? true : false
        };

        effectList.Add(coroutineInfo);

        // Order list elements by effectName
        effectList = effectList.OrderBy(c => c.effectName).ToList();
    }

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
    protected void StopAllEffects(List<CoroutineInfo> effectRoutines)
    {
        foreach (CoroutineInfo routine in effectRoutines)
        {
            StopCoroutine(routine.coroutine);
        }
        effectRoutines.Clear();
    }

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

    protected void UpdateBar(float value, float maxValue, UnityEngine.UI.Image barImage)
    {
        // Ensure that the value does not exceed limits
        value = Mathf.Clamp(value, 0f, maxValue);

        // Calculate the percentage of the remaining value
        float percentage = value / maxValue;
        barImage.fillAmount = percentage;
    }
}
