using System;
using System.Collections.Generic;
using UnityEngine;

public class StatusRegistry : MonoBehaviour
{
    private static StatusRegistry instance;
    public static StatusRegistry Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<StatusRegistry>();
            return instance;
        }
    }

    private Dictionary<string, List<StatusModification>> activeModifications = new Dictionary<string, List<StatusModification>>();
    private Dictionary<string, IStatusModifier> registeredModifiers = new Dictionary<string, IStatusModifier>();

    public event Action<string, StatusModification> OnStatusModified;
    public event Action<string, StatusModification> OnStatusModificationRemoved;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public void RegisterModifier(IStatusModifier modifier)
    {
        if (modifier == null || registeredModifiers.ContainsKey(modifier.ModifierId))
            return;

        registeredModifiers[modifier.ModifierId] = modifier;
        modifier.OnModificationChanged += HandleModificationChanged;
        modifier.OnModificationRemoved += HandleModificationRemoved;

        foreach (var modification in modifier.GetStatusModifications())
        {
            ApplyModification(modification);
        }
    }

    public void UnregisterModifier(IStatusModifier modifier)
    {
        if (modifier == null || !registeredModifiers.ContainsKey(modifier.ModifierId))
            return;

        modifier.OnModificationChanged -= HandleModificationChanged;
        modifier.OnModificationRemoved -= HandleModificationRemoved;

        foreach (var modification in modifier.GetStatusModifications())
        {
            RemoveModification(modification);
        }

        registeredModifiers.Remove(modifier.ModifierId);
    }

    private void HandleModificationChanged(IStatusModifier source, StatusModification modification)
    {
        ApplyModification(modification);
    }

    private void HandleModificationRemoved(IStatusModifier source, StatusModification modification)
    {
        RemoveModification(modification);
    }

    private void ApplyModification(StatusModification modification)
    {
        if (!activeModifications.ContainsKey(modification.statName))
            activeModifications[modification.statName] = new List<StatusModification>();

        activeModifications[modification.statName].Add(modification);
        OnStatusModified?.Invoke(modification.statName, modification);
    }

    private void RemoveModification(StatusModification modification)
    {
        if (!activeModifications.ContainsKey(modification.statName))
            return;

        activeModifications[modification.statName].Remove(modification);
        OnStatusModificationRemoved?.Invoke(modification.statName, modification);
    }

    public float GetTotalModification(string statName, float baseValue)
    {
        if (!activeModifications.ContainsKey(statName))
            return baseValue;

        float flatBonus = 0f;
        float percentageMultiplier = 1f;
        float overrideValue = -1f;

        foreach (var mod in activeModifications[statName])
        {
            switch (mod.type)
            {
                case StatusModification.ModificationType.Flat:
                    flatBonus += mod.value;
                    break;
                case StatusModification.ModificationType.Percentage:
                    percentageMultiplier += mod.value / 100f;
                    break;
                case StatusModification.ModificationType.Override:
                    overrideValue = mod.value;
                    break;
            }
        }

        if (overrideValue >= 0)
            return overrideValue;

        return (baseValue + flatBonus) * percentageMultiplier;
    }

    public List<StatusModification> GetActiveModifications(string statName)
    {
        return activeModifications.ContainsKey(statName) ?
            new List<StatusModification>(activeModifications[statName]) :
            new List<StatusModification>();
    }

    [ContextMenu("Log Active Modifications")]
    private void LogActiveModifications()
    {
        Debug.Log("=== Active Status Modifications ===");
        foreach (var kvp in activeModifications)
        {
            Debug.Log($"Stat: {kvp.Key}, Active Modifications: {kvp.Value.Count}");
            foreach (var mod in kvp.Value)
            {
                string modType = mod.type.ToString();
                string duration = mod.isPermanent ? "Permanent" : $"{mod.duration}s";
                Debug.Log($"  - {mod.value} ({modType}) Duration: {duration}, Source: {mod.sourceId}");
            }
        }
        Debug.Log("=================================");
    }
}