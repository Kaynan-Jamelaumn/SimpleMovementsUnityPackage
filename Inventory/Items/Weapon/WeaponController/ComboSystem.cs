using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ComboSystem
{
    private WeaponController controller;
    private readonly List<AttackType> currentComboSequence = new List<AttackType>();
    private float lastAttackTime;
    private float comboWindow = 3.0f;

    // Dependencies
    private AttackExecutor attackExecutor;
    private WeaponEffectsManager effectsManager;

    public ComboSystem(WeaponController controller)
    {
        this.controller = controller;
    }

    public void SetDependencies(AttackExecutor attackExecutor, WeaponEffectsManager effectsManager)
    {
        this.attackExecutor = attackExecutor;
        this.effectsManager = effectsManager;
    }

    public bool TryExecuteCombo(GameObject player, AttackType attackType)
    {
        // Add current attack to sequence
        var testSequence = new List<AttackType>(currentComboSequence) { attackType };

        var matchingCombo = controller.EquippedWeapon.GetMatchingComboSequence(testSequence.ToArray());
        if (matchingCombo != null)
        {
            controller.LogDebug($"Executing combo: {matchingCombo.comboName}");
            PerformComboFinisher(player, matchingCombo);
            return true;
        }

        return false;
    }

    public void PerformComboFinisher(GameObject player, ComboSequence combo)
    {
        controller.LogDebug("Performing combo finisher");
        effectsManager.PlayComboFinisherEffects(player, combo);
        attackExecutor.StartAttack(player, combo.specialAction, null, combo);
        ClearComboSequence();
    }

    public void UpdateComboSequence(AttackType attackType)
    {
        if (!controller.EnableComboSystem || !controller.EquippedWeapon.HasCombos()) return;

        // Reset sequence if too much time has passed
        if (Time.time - lastAttackTime > comboWindow)
        {
            ClearComboSequence();
        }

        currentComboSequence.Add(attackType);
        lastAttackTime = Time.time;

        controller.LogDebug($"Combo sequence: {string.Join(" -> ", currentComboSequence)}");
    }

    public void UpdateComboTimer()
    {
        if (currentComboSequence.Count > 0 && Time.time - lastAttackTime > comboWindow)
        {
            ClearComboSequence();
        }
    }

    public void ClearComboSequence()
    {
        currentComboSequence.Clear();
        controller.LogDebug("Combo sequence cleared");
    }

    public List<AttackType> GetCurrentComboSequence() => new List<AttackType>(currentComboSequence);

    public void Reset()
    {
        ClearComboSequence();
        lastAttackTime = 0f;
    }
}