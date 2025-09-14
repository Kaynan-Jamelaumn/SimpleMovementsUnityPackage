using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ComboSystem
{
    private WeaponController controller;
    private readonly List<AttackType> currentComboSequence = new List<AttackType>();
    private float lastAttackTime;
    private float comboWindow = 3.0f;

    // New fields for enhanced combo system
    private ComboTree currentComboTree;
    private List<ComboBranch> executedBranches = new List<ComboBranch>();
    private int totalDamageDealt = 0;
    private float comboScore = 0f;
    private GameObject currentTarget;

    // Dependencies
    private AttackExecutor attackExecutor;
    private WeaponEffectsManager effectsManager;

    // Properties for external access
    public float ComboScore => comboScore;
    public int ComboLength => currentComboSequence.Count;
    public List<ComboBranch> ExecutedBranches => new List<ComboBranch>(executedBranches);

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
        // First check for branching combos
        if (TryExecuteBranch(player, attackType))
            return true;

        // Then check for traditional sequential combos
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

    public bool TryExecuteBranch(GameObject player, AttackType attackType)
    {
        if (controller.EquippedWeapon?.ComboTree == null) return false;

        var playerController = player.GetComponent<PlayerStatusController>();
        var availableBranches = controller.EquippedWeapon.ComboTree.GetAvailableBranches(
            attackType,
            playerController,
            currentTarget,
            currentComboSequence.Count,
            controller
        );

        if (availableBranches.Count > 0)
        {
            // Execute the highest priority branch (first in sorted list)
            var branch = availableBranches[0];
            ExecuteBranch(player, branch);
            return true;
        }

        return false;
    }

    private void ExecuteBranch(GameObject player, ComboBranch branch)
    {
        controller.LogDebug($"Executing combo branch: {branch.branchName}");

        // Apply weapon traits to the branch action
        var playerController = player.GetComponent<PlayerStatusController>();
        if (playerController != null && branch.branchAction != null)
        {
            controller.EquippedWeapon.ApplyWeaponTraitsToAttack(branch.branchAction, playerController);
        }

        // Apply branch damage bonus
        if (branch.damageBonus > 0 && branch.branchAction != null)
        {
            // Store the damage bonus to be applied during the attack
            StoreDamageBonus(branch.damageBonus);
        }

        // Play branch effects
        if (branch.branchParticles != null)
        {
            var particles = Object.Instantiate(branch.branchParticles, controller.HandGameObject.transform);
            particles.Play();
        }

        if (branch.branchSound != null)
        {
            player.GetComponent<Player>()?.PlayerAudioSource?.PlayOneShot(branch.branchSound);
        }

        // Execute the branch action
        attackExecutor.StartAttack(player, branch.branchAction, null, null);

        // Apply bonus effects to player
        foreach (var effect in branch.bonusEffects)
        {
            if (effect != null && playerController != null)
            {
                playerController.ApplyEffect(effect, effect.amount, effect.timeBuffEffect, effect.tickCooldown);
            }
        }

        // Track branch execution
        executedBranches.Add(branch);

        if (branch.isFinisher || branch.resetsCombo)
        {
            FinishCombo(player, branch);
        }
    }

    private void StoreDamageBonus(float bonus)
    {
        // This would need to be accessed by AttackExecutor during damage calculation
        // For now, we'll store it as a property that can be accessed
        controller.LogDebug($"Damage bonus stored: {bonus}");
    }

    public void PerformComboFinisher(GameObject player, ComboSequence combo)
    {
        controller.LogDebug("Performing combo finisher");
        effectsManager.PlayComboFinisherEffects(player, combo);
        attackExecutor.StartAttack(player, combo.specialAction, null, combo);
        ClearComboSequence();
    }

    private void FinishCombo(GameObject player, ComboBranch finisherBranch)
    {
        // Calculate combo score based on branches executed
        float score = CalculateComboScore();
        comboScore = score;

        // Apply bonus rewards
        var playerController = player.GetComponent<PlayerStatusController>();
        if (playerController?.XPManager != null && finisherBranch.experienceBonus > 0)
        {
            int finalExp = finisherBranch.experienceBonus;

            // Apply tree completion multiplier if applicable
            if (controller.EquippedWeapon?.ComboTree != null && executedBranches.Count >= 5)
            {
                finalExp = Mathf.RoundToInt(finalExp * controller.EquippedWeapon.ComboTree.treeCompletionExpMultiplier);
            }

            playerController.XPManager.AddExperience(finalExp);
        }

        // Visual feedback for combo completion
        controller.LogDebug($"Combo finished! Score: {score}, Branches: {executedBranches.Count}");

        // Reset combo state
        if (finisherBranch.resetsCombo)
        {
            Reset();
        }
    }

    private float CalculateComboScore()
    {
        float score = currentComboSequence.Count * 100;
        score += executedBranches.Count * 200;

        // Bonus for branch variety
        var uniqueBranches = executedBranches.Select(b => b.branchName).Distinct().Count();
        score += uniqueBranches * 150;

        // Damage dealt bonus
        score *= (1f + (totalDamageDealt / 1000f));

        // Time bonus (faster combos = higher score)
        float comboTime = Time.time - (lastAttackTime - (currentComboSequence.Count * comboWindow));
        float timeBonus = Mathf.Max(0, 2f - (comboTime / (currentComboSequence.Count + 1)));
        score *= (1f + timeBonus);

        return score;
    }

    public void UpdateComboSequence(AttackType attackType)
    {
        if (!controller.EnableComboSystem) return;

        // Update combo tree reference if needed
        if (controller.EquippedWeapon?.ComboTree != null)
        {
            currentComboTree = controller.EquippedWeapon.ComboTree;

            // Use dynamic combo window from tree
            comboWindow = currentComboTree.GetComboWindow(currentComboSequence.Count);
        }

        // Reset sequence if too much time has passed
        if (Time.time - lastAttackTime > comboWindow)
        {
            ClearComboSequence();
        }

        currentComboSequence.Add(attackType);
        lastAttackTime = Time.time;

        controller.LogDebug($"Combo sequence: {string.Join(" -> ", currentComboSequence)}");

        // Check if we've hit max combo length
        if (currentComboTree != null && currentComboSequence.Count >= currentComboTree.maxComboLength)
        {
            controller.LogDebug("Max combo length reached, resetting");
            Reset();
        }
    }

    public void UpdateComboTimer()
    {
        if (currentComboSequence.Count > 0 && Time.time - lastAttackTime > comboWindow)
        {
            ClearComboSequence();
        }
    }

    public void SetCurrentTarget(GameObject target)
    {
        currentTarget = target;
    }

    public void AddDamageDealt(int damage)
    {
        totalDamageDealt += damage;
    }

    public float GetCurrentComboDamageMultiplier()
    {
        if (currentComboTree != null)
        {
            return currentComboTree.GetComboDamageMultiplier(currentComboSequence.Count);
        }
        return 1f;
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
        executedBranches.Clear();
        totalDamageDealt = 0;
        comboScore = 0f;
        currentTarget = null;
        currentComboTree = null;
        lastAttackTime = 0f;
    }

    // Helper methods for UI display
    public string GetComboString()
    {
        if (currentComboSequence.Count == 0) return "";
        return string.Join(" → ", currentComboSequence);
    }

    public float GetComboTimeRemaining()
    {
        if (currentComboSequence.Count == 0) return 0f;
        return Mathf.Max(0f, comboWindow - (Time.time - lastAttackTime));
    }

    public float GetComboProgress()
    {
        if (currentComboTree == null || currentComboTree.maxComboLength == 0) return 0f;
        return (float)currentComboSequence.Count / currentComboTree.maxComboLength;
    }
}