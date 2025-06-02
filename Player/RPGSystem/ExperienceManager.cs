using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public class LevelUpReward
{
    public int level;
    public int skillPoints;
    public int attributePoints;
    public int statUpgradePoints;
    public List<Trait> unlockedTraits = new List<Trait>();
}

public class ExperienceManager : MonoBehaviour
{
    [Header("Experience Settings")]
    [SerializeField] private int currentExperience;
    [SerializeField] private int experienceToNextLevel;
    [SerializeField] private int currentLevel = 1;

    [Header("Points")]
    [SerializeField] private int skillPoints;
    [SerializeField] private int attributePoints;
    [SerializeField] private int statUpgradePoints; // Points for upgrading stats

    [Header("Class Reference")]
    [SerializeField] private PlayerClass playerClass;

    [Header("Level Up Rewards")]
    [SerializeField] private List<LevelUpReward> specialLevelRewards = new List<LevelUpReward>();

    // Events
    public event Action<int> OnLevelUp;
    public event Action OnSkillPointGained;
    public event Action OnAttributePointGained;
    public event Action<int> OnStatUpgradePointsGained;
    public event Action<string> OnStatUpgraded; // Fired when a stat is actually upgraded

    // Properties
    public int CurrentExperience { get => currentExperience; set => currentExperience = value; }
    public int CurrentLevel { get => currentLevel; set => currentLevel = value; }
    public int SkillPoints { get => skillPoints; set => skillPoints = value; }
    public int AttributePoints { get => attributePoints; set => attributePoints = value; }
    public int StatUpgradePoints { get => statUpgradePoints; set => statUpgradePoints = value; }
    public PlayerClass PlayerClass { get => playerClass; set => playerClass = value; }

    // Available stats for upgrading
    public List<string> AvailableStats { get; private set; } = new List<string>
    {
        "health", "stamina", "mana", "speed", "strength", "agility",
        "intelligence", "endurance", "hunger", "thirst", "sleep", "sanity", "weight"
    };

    private void Start()
    {
        experienceToNextLevel = CalculateExperienceForLevel(currentLevel + 1);
    }

    // Add experience and handle leveling
    public void AddExperience(int xp)
    {
        currentExperience += xp;

        while (currentExperience >= experienceToNextLevel)
        {
            LevelUp();
        }
    }

    // Level up process
    private void LevelUp()
    {
        currentExperience -= experienceToNextLevel;
        currentLevel++;

        // Award standard points
        int skillPointsGained = CalculateSkillPointsGained();
        int attributePointsGained = CalculateAttributePointsGained();
        int statPointsGained = CalculateStatUpgradePointsGained();

        skillPoints += skillPointsGained;
        attributePoints += attributePointsGained;
        statUpgradePoints += statPointsGained;

        // Check for special level rewards
        var specialReward = GetSpecialLevelReward(currentLevel);
        if (specialReward != null)
        {
            skillPoints += specialReward.skillPoints;
            attributePoints += specialReward.attributePoints;
            statUpgradePoints += specialReward.statUpgradePoints;

            // Handle unlocked traits if needed
            foreach (var trait in specialReward.unlockedTraits)
            {
                Debug.Log($"Unlocked new trait: {trait.Name}");
            }
        }

        experienceToNextLevel = CalculateExperienceForLevel(currentLevel + 1);

        // Fire events
        OnLevelUp?.Invoke(currentLevel);
        if (skillPointsGained > 0) OnSkillPointGained?.Invoke();
        if (attributePointsGained > 0) OnAttributePointGained?.Invoke();
        if (statPointsGained > 0) OnStatUpgradePointsGained?.Invoke(statPointsGained);

        Debug.Log($"Level Up! Now level {currentLevel}. Gained {statPointsGained} stat points.");
    }

    // Player manually upgrades a stat
    public bool UpgradeStat(string statType)
    {
        if (statUpgradePoints <= 0)
        {
            Debug.Log("Not enough stat upgrade points!");
            return false;
        }

        if (!AvailableStats.Contains(statType.ToLower()))
        {
            Debug.Log($"Invalid stat type: {statType}");
            return false;
        }

        var playerController = GetComponent<PlayerStatusController>();
        if (playerController == null)
        {
            Debug.LogError("PlayerStatusController not found!");
            return false;
        }

        float upgradeAmount = GetStatUpgradeAmount(statType);

        // Apply the stat upgrade based on type
        bool success = ApplyStatUpgrade(statType.ToLower(), upgradeAmount, playerController);

        if (success)
        {
            statUpgradePoints--;
            OnStatUpgraded?.Invoke(statType);
            Debug.Log($"Upgraded {statType} by {upgradeAmount:F1}. Remaining stat points: {statUpgradePoints}");
            return true;
        }

        return false;
    }

    // Get the amount a stat will be upgraded by
    public float GetStatUpgradeAmount(string statType)
    {
        if (playerClass == null)
        {
            Debug.LogWarning("No player class assigned!");
            return 1f;
        }

        return playerClass.GetStatUpgradeAmount(statType);
    }

    // Apply the actual stat upgrade
    private bool ApplyStatUpgrade(string statType, float amount, PlayerStatusController controller)
    {
        try
        {
            switch (statType)
            {
                case "health":
                    controller.HpManager.ModifyMaxValue(amount);
                    break;
                case "stamina":
                    controller.StaminaManager.ModifyMaxValue(amount);
                    break;
                case "mana":
                    controller.ManaManager.ModifyMaxValue(amount);
                    break;
                case "speed":
                    controller.SpeedManager.ModifyBaseSpeed(amount);
                    break;
                case "hunger":
                    controller.HungerManager.ModifyMaxValue(amount);
                    break;
                case "thirst":
                    controller.ThirstManager.ModifyMaxValue(amount);
                    break;
                case "sleep":
                    controller.SleepManager.ModifyMaxValue(amount);
                    break;
                case "sanity":
                    controller.SanityManager.ModifyMaxValue(amount);
                    break;
                case "weight":
                    controller.WeightManager.ModifyMaxValue(amount);
                    break;
                case "strength":
                case "agility":
                case "intelligence":
                case "endurance":
                    // These might need special handling depending on your implementation
                    Debug.Log($"Upgraded {statType} by {amount} (combat stat - implement as needed)");
                    break;
                default:
                    Debug.LogWarning($"Stat upgrade for {statType} not implemented yet");
                    return false;
            }
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error upgrading {statType}: {e.Message}");
            return false;
        }
    }

    // Get available stats that can be upgraded
    public List<string> GetUpgradeableStats()
    {
        return new List<string>(AvailableStats);
    }

    // Get preview of stat upgrade
    public string GetStatUpgradePreview(string statType)
    {
        float upgradeAmount = GetStatUpgradeAmount(statType);
        string classBonus = playerClass != null ? $" ({playerClass.GetClassName()} bonus)" : "";
        return $"+{upgradeAmount:F1}{classBonus}";
    }

    // Calculate experience needed for a specific level
    private int CalculateExperienceForLevel(int level)
    {
        // Exponential scaling: level^2 * 100 + level * 50
        return level * level * 100 + level * 50;
    }

    // Calculate skill points gained per level
    private int CalculateSkillPointsGained()
    {
        // Every level gives 1 skill point, every 5 levels gives bonus
        return 1 + (currentLevel % 5 == 0 ? 1 : 0);
    }

    // Calculate attribute points gained per level
    private int CalculateAttributePointsGained()
    {
        // Every 3 levels gives 1 attribute point
        return currentLevel % 3 == 0 ? 1 : 0;
    }

    // Calculate stat upgrade points gained per level
    private int CalculateStatUpgradePointsGained()
    {
        // Every level gives 1 stat upgrade point, with bonuses at certain levels
        int basePoints = 1;
        int bonusPoints = 0;

        // Bonus points at every 10 levels
        if (currentLevel % 10 == 0) bonusPoints += 2;
        // Small bonus at every 5 levels
        else if (currentLevel % 5 == 0) bonusPoints += 1;

        return basePoints + bonusPoints;
    }

    // Get special reward for a specific level
    private LevelUpReward GetSpecialLevelReward(int level)
    {
        return specialLevelRewards.Find(r => r.level == level);
    }

    // Set player class
    public void SetPlayerClass(PlayerClass newClass)
    {
        playerClass = newClass;
        Debug.Log($"Player class set to: {playerClass.GetClassName()}");
    }

    // Add a special level reward
    public void AddSpecialLevelReward(LevelUpReward reward)
    {
        // Remove existing reward for this level if it exists
        specialLevelRewards.RemoveAll(r => r.level == reward.level);
        specialLevelRewards.Add(reward);
        specialLevelRewards.Sort((a, b) => a.level.CompareTo(b.level));
    }

    // Debug methods
    [ContextMenu("Add 100 XP")]
    private void DebugAddXP()
    {
        AddExperience(100);
    }

    [ContextMenu("Level Up")]
    private void DebugLevelUp()
    {
        AddExperience(experienceToNextLevel);
    }
}