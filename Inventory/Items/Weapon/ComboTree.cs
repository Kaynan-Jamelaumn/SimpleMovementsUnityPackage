using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ComboTree", menuName = "Scriptable Objects/Weapon/ComboTree")]
public class ComboTree : ScriptableObject
{
    [Header("Tree Configuration")]
    public string treeName;
    public WeaponSO associatedWeapon;

    [Header("Combo Branches")]
    public List<ComboBranch> branches = new List<ComboBranch>();

    [Header("Tree Properties")]
    [Tooltip("The combo resets after this many attacks.")]
    public int maxComboLength = 10;
    [Tooltip("Seconds after an attack during which the next one continues the combo.")]
    public float baseComboWindow = 2.0f;
    [Tooltip("Seconds the combo window shrinks per attack (never below 0.5 s).")]
    public float comboWindowDecayRate = 0.1f;

    [Header("Tree Bonuses")]
    [Tooltip("Bonus damage per combo count")]
    public float comboDamageMultiplierPerHit = 0.05f;
    [Tooltip("Experience multiplier for completing full tree")]
    public float treeCompletionExpMultiplier = 2.0f;

    // Runtime data
    private Dictionary<string, List<ComboBranch>> branchMap = new Dictionary<string, List<ComboBranch>>();

    private void OnEnable()
    {
        BuildBranchMap();
    }

    private void OnValidate()
    {
        BuildBranchMap();
    }

    private void BuildBranchMap()
    {
        if (branchMap == null)
            branchMap = new Dictionary<string, List<ComboBranch>>();
        branchMap.Clear();
        foreach (var branch in branches)
        {
            if (branch == null || branch.branchAction == null) continue;

            string key = GenerateBranchKey(branch.triggerInput);
            if (!branchMap.ContainsKey(key))
                branchMap[key] = new List<ComboBranch>();
            branchMap[key].Add(branch);
        }
    }

    private string GenerateBranchKey(AttackType input)
    {
        return input.ToString();
    }

    public List<ComboBranch> GetAvailableBranches(AttackType input, PlayerStatusController player, GameObject target, int comboCount, WeaponController weaponController)
    {
        if (branchMap == null || branchMap.Count == 0)
            BuildBranchMap();
        string key = GenerateBranchKey(input);
        if (!branchMap.ContainsKey(key))
            return new List<ComboBranch>();

        var availableBranches = new List<ComboBranch>();
        foreach (var branch in branchMap[key])
        {
            if (branch != null && branch.CanExecute(player, target, comboCount, weaponController))
                availableBranches.Add(branch);
        }

        // Sort: highest priority first, then non-finishers before finishers, then the biggest damage bonus.
        availableBranches.Sort((a, b) =>
        {
            if (a.priority != b.priority)
                return b.priority.CompareTo(a.priority);
            if (a.isFinisher != b.isFinisher)
                return a.isFinisher ? 1 : -1;
            return b.damageBonus.CompareTo(a.damageBonus);
        });

        return availableBranches;
    }

    public ComboBranch GetDefaultBranch(AttackType input)
    {
        // Return a branch with no conditions as fallback
        string key = GenerateBranchKey(input);
        if (!branchMap.ContainsKey(key))
            return null;

        foreach (var branch in branchMap[key])
        {
            if (branch.conditions.Count == 0)
                return branch;
        }

        return null;
    }

    public float GetComboWindow(int currentComboCount)
    {
        return Mathf.Max(0.5f, baseComboWindow - (comboWindowDecayRate * currentComboCount));
    }

    public float GetComboDamageMultiplier(int comboCount)
    {
        return 1f + (comboDamageMultiplierPerHit * comboCount);
    }

    public bool HasBranchForInput(AttackType input)
    {
        return branchMap.ContainsKey(GenerateBranchKey(input));
    }

    // Debug helpers
    [ContextMenu("Log Branch Map")]
    private void LogBranchMap()
    {
        BuildBranchMap();
        Debug.Log($"=== Combo Tree: {treeName} ===");
        foreach (var kvp in branchMap)
        {
            Debug.Log($"Input: {kvp.Key} -> {kvp.Value.Count} branches");
            foreach (var branch in kvp.Value)
            {
                Debug.Log($"  - {branch.branchName} (Conditions: {branch.conditions.Count})");
            }
        }
    }

    public int GetTotalBranchCount()
    {
        return branches.Count;
    }

    public List<ComboBranch> GetFinisherBranches()
    {
        return branches.FindAll(b => b != null && b.isFinisher);
    }

    /// <summary>Configuration problems (inspector and validation window).</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        if (branches == null || branches.Count == 0)
            warnings.Add($"Combo tree '{name}' has no branches.");
        else
            for (int i = 0; i < branches.Count; i++)
            {
                if (branches[i] == null) { warnings.Add($"Combo tree '{name}': branch {i + 1} is empty."); continue; }
                branches[i].Validate($"Combo tree '{name}'", errors, warnings);
            }
        if (maxComboLength < 1)
            errors.Add($"Combo tree '{name}': Max Combo Length must be at least 1.");
        if (baseComboWindow <= 0f)
            errors.Add($"Combo tree '{name}': Base Combo Window must be above 0.");
    }
}