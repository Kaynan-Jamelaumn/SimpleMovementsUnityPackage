using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


public class StatusModifierRegistry : MonoBehaviour
{
    [Header("Registered Modifiers")]
    [SerializeField] private Dictionary<string, IStatusModifier> registeredModifiers = new Dictionary<string, IStatusModifier>();
    [SerializeField] private Dictionary<UnifiedStatusType, List<StatusModification>> activeModifications = new Dictionary<UnifiedStatusType, List<StatusModification>>();

    [Header("References")]
    [SerializeField] private PlayerStatusController playerStatusController;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = false;

    // Events
    public event System.Action<IStatusModifier> OnModifierRegistered;
    public event System.Action<IStatusModifier> OnModifierUnregistered;
    public event System.Action<UnifiedStatusType, float> OnStatusModified;

    private void Awake()
    {
        if (playerStatusController == null)
            playerStatusController = GetComponent<PlayerStatusController>();

        InitializeModificationDictionary();
    }

    private void InitializeModificationDictionary()
    {
        foreach (UnifiedStatusType statusType in System.Enum.GetValues(typeof(UnifiedStatusType)))
        {
            activeModifications[statusType] = new List<StatusModification>();
        }
    }

    public void RegisterModifier(IStatusModifier modifier)
    {
        if (modifier == null || registeredModifiers.ContainsKey(modifier.ModifierId))
            return;

        registeredModifiers[modifier.ModifierId] = modifier;

        if (modifier.IsActive)
        {
            ApplyModifierEffects(modifier);
        }

        OnModifierRegistered?.Invoke(modifier);

        if (enableDebugLogging)
            Debug.Log($"[StatusModifierRegistry] Registered modifier: {modifier.ModifierName} ({modifier.ModifierId})");
    }

    public void UnregisterModifier(IStatusModifier modifier)
    {
        if (modifier == null || !registeredModifiers.ContainsKey(modifier.ModifierId))
            return;

        RemoveModifierEffects(modifier);
        registeredModifiers.Remove(modifier.ModifierId);

        OnModifierUnregistered?.Invoke(modifier);

        if (enableDebugLogging)
            Debug.Log($"[StatusModifierRegistry] Unregistered modifier: {modifier.ModifierName} ({modifier.ModifierId})");
    }

    public void UpdateModifier(IStatusModifier modifier)
    {
        if (modifier == null || !registeredModifiers.ContainsKey(modifier.ModifierId))
            return;

        // Remove old effects
        RemoveModifierEffects(modifier);

        // Apply new effects if active
        if (modifier.IsActive)
        {
            ApplyModifierEffects(modifier);
        }
    }

    private void ApplyModifierEffects(IStatusModifier modifier)
    {
        var modifications = modifier.GetStatusModifications();
        if (modifications == null) return;

        foreach (var modification in modifications)
        {
            if (!activeModifications.ContainsKey(modification.statusType))
                activeModifications[modification.statusType] = new List<StatusModification>();

            activeModifications[modification.statusType].Add(modification);
            ApplyModificationToStatus(modification);
            modifier.OnStatusModificationApplied(modification);
        }

        RecalculateAllStatuses();
    }

    private void RemoveModifierEffects(IStatusModifier modifier)
    {
        var modifications = modifier.GetStatusModifications();
        if (modifications == null) return;

        foreach (var modification in modifications)
        {
            if (activeModifications.ContainsKey(modification.statusType))
            {
                activeModifications[modification.statusType].RemoveAll(m => m.source == modifier.ModifierId);
                RemoveModificationFromStatus(modification);
                modifier.OnStatusModificationRemoved(modification);
            }
        }

        RecalculateAllStatuses();
    }

    private void ApplyModificationToStatus(StatusModification modification)
    {
        if (playerStatusController == null) return;

        switch (modification.statusType)
        {
            case UnifiedStatusType.MaxHp:
                playerStatusController.HpManager?.ModifyMaxValue(modification.currentValue);
                break;
            case UnifiedStatusType.MaxStamina:
                playerStatusController.StaminaManager?.ModifyMaxValue(modification.currentValue);
                break;
            case UnifiedStatusType.MaxMana:
                playerStatusController.ManaManager?.ModifyMaxValue(modification.currentValue);
                break;
            case UnifiedStatusType.Speed:
                playerStatusController.SpeedManager?.ModifySpeed(modification.currentValue);
                break;
            case UnifiedStatusType.MaxWeight:
                playerStatusController.WeightManager?.ModifyMaxWeight(modification.currentValue);
                break;
                // Add more cases as needed
        }

        OnStatusModified?.Invoke(modification.statusType, modification.currentValue);
    }

    private void RemoveModificationFromStatus(StatusModification modification)
    {
        if (playerStatusController == null) return;

        // Apply negative value to remove the effect
        var removeModification = new StatusModification(
            modification.statusType,
            -modification.currentValue,
            modification.modificationType,
            modification.source
        );

        ApplyModificationToStatus(removeModification);
    }

    public void RecalculateAllStatuses()
    {
        // This method can be expanded to recalculate all status values
        // based on base values and all active modifications
        foreach (var statusType in activeModifications.Keys)
        {
            RecalculateStatus(statusType);
        }
    }

    public void RecalculateStatus(UnifiedStatusType statusType)
    {
        var modifications = GetActiveModificationsForStatus(statusType);
        // Implement full recalculation logic here if needed
    }

    public List<StatusModification> GetActiveModificationsForStatus(UnifiedStatusType statusType)
    {
        return activeModifications.ContainsKey(statusType)
            ? new List<StatusModification>(activeModifications[statusType])
            : new List<StatusModification>();
    }

    public float GetTotalModificationForStatus(UnifiedStatusType statusType)
    {
        if (!activeModifications.ContainsKey(statusType))
            return 0f;

        float total = 0f;
        foreach (var mod in activeModifications[statusType])
        {
            if (mod.modificationType == ModificationType.Flat)
                total += mod.currentValue;
        }

        return total;
    }

    public List<IStatusModifier> GetAllActiveModifiers()
    {
        return registeredModifiers.Values.Where(m => m.IsActive).ToList();
    }

    public IStatusModifier GetModifier(string modifierId)
    {
        return registeredModifiers.ContainsKey(modifierId) ? registeredModifiers[modifierId] : null;
    }

    public string GetStatusReport()
    {
        var report = "=== Status Modifier Registry Report ===\n";
        report += $"Registered Modifiers: {registeredModifiers.Count}\n";
        report += $"Active Modifiers: {GetAllActiveModifiers().Count}\n\n";

        foreach (var statusType in activeModifications.Keys)
        {
            var mods = activeModifications[statusType];
            if (mods.Count > 0)
            {
                report += $"{statusType}: {mods.Count} modifications (Total: {GetTotalModificationForStatus(statusType)})\n";
            }
        }

        return report;
    }
}