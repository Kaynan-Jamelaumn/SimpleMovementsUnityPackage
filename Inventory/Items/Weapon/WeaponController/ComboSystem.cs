using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The current combo of a weapon controller: the inputs of the attacks made in a row (each within the combo window
/// of the previous one), the hits and damage, and the rewards. It decides which combo attack an input performs:
/// a <see cref="ComboTree"/> branch whose conditions hold, or a <see cref="ComboSequence"/> whose inputs end the
/// current string.
/// </summary>
public class ComboSystem
{
    /// <summary>The attack chosen for an input by the combo rules.</summary>
    public struct ComboChoice
    {
        public AttackAction action;
        public ComboBranch branch;
        public ComboSequence sequence;
        public float damageMultiplier;
        public float critChanceBonus;
        public bool IsValid => action != null;
    }

    private readonly WeaponController controller;
    private readonly List<AttackType> currentComboSequence = new List<AttackType>();
    private readonly List<ComboBranch> executedBranches = new List<ComboBranch>();
    private float lastAttackTime = -999f;
    private float comboStartTime;
    private float comboWindow = 3.0f;
    private int totalDamageDealt;
    private float comboScore;
    private float elementalStreak;
    private GameObject currentTarget;
    private ComboTree currentComboTree;

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

    // Properties for external access (UI, conditions)
    public float ComboScore => comboScore;
    public int ComboLength => currentComboSequence.Count;
    public List<ComboBranch> ExecutedBranches => new List<ComboBranch>(executedBranches);
    public IReadOnlyList<AttackType> Inputs => currentComboSequence;
    /// <summary>Input of the previous attack in the current combo (null when no combo is running).</summary>
    public AttackType? PreviousInput => currentComboSequence.Count > 0 ? currentComboSequence[currentComboSequence.Count - 1] : (AttackType?)null;
    public float TimeSinceLastAttack => Time.time - lastAttackTime;
    /// <summary>Hits in a row with the weapon's element (scaled by its Elemental Buildup Rate).</summary>
    public float ElementalHitStreak => elementalStreak;
    public GameObject CurrentTarget => currentTarget;
    public int TotalDamageDealt => totalDamageDealt;

    /// <summary>
    /// The combo attack for <paramref name="input"/>, if any: a combo tree branch first (they have conditions), then
    /// the longest combo sequence ending with the current inputs + this one.
    /// </summary>
    public ComboChoice Choose(GameObject player, AttackType input)
    {
        var choice = new ComboChoice { damageMultiplier = 1f };
        WeaponSO weapon = controller.EquippedWeapon;
        if (!controller.EnableComboSystem || weapon == null)
            return choice;
        ExpireIfLate();

        if (weapon.ComboTree != null)
        {
            PlayerStatusController ps = player != null ? player.GetComponentInParent<PlayerStatusController>() : null;
            List<ComboBranch> branches = weapon.ComboTree.GetAvailableBranches(input, ps, currentTarget, currentComboSequence.Count, controller);
            if (branches.Count > 0)
            {
                ComboBranch b = branches[0];
                TraitManager tm = ps != null ? ps.TraitManager : null;
                choice.action = b.branchAction;
                choice.branch = b;
                choice.damageMultiplier = 1f + Mathf.Max(-0.99f, b.GetModifiedDamageBonus(tm, weapon));
                return choice;
            }
        }

        if (weapon.ComboSequences != null && weapon.ComboSequences.Count > 0)
        {
            var test = new List<AttackType>(currentComboSequence) { input };
            ComboSequence seq = weapon.GetComboEndingWith(test);
            if (seq != null)
            {
                choice.action = seq.specialAction;
                choice.sequence = seq;
                choice.damageMultiplier = Mathf.Max(0f, seq.damageMultiplier);
                choice.critChanceBonus = seq.criticalChanceBonus;
            }
        }
        return choice;
    }

    /// <summary>Records an attack that started (called for every attack, combo or not).</summary>
    public void RegisterAttack(GameObject player, AttackType input, in ComboChoice choice)
    {
        if (!controller.EnableComboSystem)
            return;
        WeaponSO weapon = controller.EquippedWeapon;
        currentComboTree = weapon != null ? weapon.ComboTree : null;
        comboWindow = currentComboTree != null ? currentComboTree.GetComboWindow(currentComboSequence.Count) : controller.DefaultComboWindow;
        ExpireIfLate();
        if (currentComboSequence.Count == 0)
            comboStartTime = Time.time;

        currentComboSequence.Add(input);
        lastAttackTime = Time.time;
        controller.LogDebug($"Combo: {GetComboString()}");

        if (choice.branch != null)
            OnBranchStarted(player, choice.branch);
        else if (choice.sequence != null)
            OnSequenceStarted(player, choice.sequence);

        if (currentComboTree != null && currentComboSequence.Count >= Mathf.Max(1, currentComboTree.maxComboLength))
        {
            controller.LogDebug("Max combo length reached, resetting");
            ClearComboSequence();
        }
    }

    private void OnBranchStarted(GameObject player, ComboBranch branch)
    {
        controller.LogDebug($"Combo branch: {branch.branchName}");
        effectsManager.PlayBranchEffects(player, branch);

        PlayerStatusController ps = player != null ? player.GetComponentInParent<PlayerStatusController>() : null;
        if (ps != null && branch.bonusEffects != null)
            foreach (AttackEffect effect in branch.bonusEffects)
                if (effect != null)
                    ps.ApplyEffect(effect, effect.amount, effect.timeBuffEffect, effect.tickCooldown);

        executedBranches.Add(branch);
        if (branch.isFinisher || branch.resetsCombo)
            FinishCombo(player, branch);
    }

    private void OnSequenceStarted(GameObject player, ComboSequence combo)
    {
        controller.LogDebug($"Combo sequence: {combo.comboName}");
        effectsManager.PlayComboFinisherEffects(player, combo);
        if (combo.experienceBonus > 0)
            GiveExperience(player, combo.experienceBonus);
        comboScore = CalculateComboScore();
        ClearComboSequence();
    }

    private void FinishCombo(GameObject player, ComboBranch finisherBranch)
    {
        comboScore = CalculateComboScore();
        PlayerStatusController ps = player != null ? player.GetComponentInParent<PlayerStatusController>() : null;
        int exp = finisherBranch.GetModifiedExperienceBonus(ps != null ? ps.TraitManager : null, controller.EquippedWeapon);
        if (exp > 0)
        {
            if (currentComboTree != null && executedBranches.Count >= 5)
                exp = Mathf.RoundToInt(exp * currentComboTree.treeCompletionExpMultiplier);
            GiveExperience(player, exp);
        }
        controller.LogDebug($"Combo finished! Score: {comboScore:0}, branches: {executedBranches.Count}");
        if (finisherBranch.resetsCombo || finisherBranch.isFinisher)
            Reset();
    }

    private static void GiveExperience(GameObject player, int amount)
    {
        PlayerStatusController ps = player != null ? player.GetComponentInParent<PlayerStatusController>() : null;
        if (ps != null && ps.XPManager != null)
            ps.XPManager.AddExperience(amount);
    }

    private float CalculateComboScore()
    {
        float score = currentComboSequence.Count * 100f + executedBranches.Count * 200f;
        score += executedBranches.Where(b => b != null).Select(b => b.branchName).Distinct().Count() * 150f;
        score *= 1f + totalDamageDealt / 1000f;
        float duration = Mathf.Max(0.1f, Time.time - comboStartTime);
        float timeBonus = Mathf.Max(0f, 2f - duration / (currentComboSequence.Count + 1));
        return score * (1f + timeBonus);
    }

    /// <summary>A hit landed (damage dealt, whether it carried the weapon's element).</summary>
    public void RegisterHit(GameObject target, float damage, bool elemental)
    {
        currentTarget = target;
        totalDamageDealt += Mathf.RoundToInt(damage);
        WeaponSO weapon = controller.EquippedWeapon;
        if (elemental)
            elementalStreak += weapon != null ? Mathf.Max(0f, weapon.ElementalBuildupRate) : 1f;
        else
            elementalStreak = 0f;
    }

    /// <summary>Damage multiplier from the combo tree's per-hit bonus.</summary>
    public float GetCurrentComboDamageMultiplier()
    {
        return currentComboTree != null ? currentComboTree.GetComboDamageMultiplier(currentComboSequence.Count) : 1f;
    }

    public void UpdateComboTimer() => ExpireIfLate();

    private void ExpireIfLate()
    {
        if (currentComboSequence.Count > 0 && !attackExecutor.IsAttacking && Time.time - lastAttackTime > comboWindow)
            ClearComboSequence();
    }

    public void SetCurrentTarget(GameObject target) => currentTarget = target;
    public void AddDamageDealt(int damage) => totalDamageDealt += damage;

    public void ClearComboSequence()
    {
        currentComboSequence.Clear();
        elementalStreak = 0f;
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
        lastAttackTime = -999f;
    }

    // Helper methods for UI display
    public string GetComboString() => currentComboSequence.Count == 0 ? "" : string.Join(" → ", currentComboSequence);

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
