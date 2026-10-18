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

/// <summary>What one level gave (kept so a lost level can be taken back exactly).</summary>
[System.Serializable]
public struct LevelGain
{
    public int level;
    public int skillPoints;
    public int attributePoints;
    public int statUpgradePoints;
}

/// <summary>One stat upgrade point spent (kept so a lost level can undo it).</summary>
[System.Serializable]
public struct StatUpgradeRecord
{
    public string stat;
    public float amount;
    /// <summary>The level the character was when it spent the point.</summary>
    public int level;
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
    /// <summary>The character lost levels (death penalties): the new level.</summary>
    public event Action<int> OnLevelDown;
    /// <summary>Experience was lost (amount).</summary>
    public event Action<int> OnExperienceLost;

    // What each level gave and which stat points were spent (this play session), so regressions undo them exactly.
    [System.NonSerialized] private readonly List<LevelGain> levelGains = new List<LevelGain>();
    [System.NonSerialized] private readonly List<StatUpgradeRecord> upgradeHistory = new List<StatUpgradeRecord>();

    /// <summary>The stat points spent so far (this session), oldest first.</summary>
    public IReadOnlyList<StatUpgradeRecord> UpgradeHistory => upgradeHistory;

    // Properties
    public int CurrentExperience { get => currentExperience; set => currentExperience = value; }
    public int CurrentLevel { get => currentLevel; set => currentLevel = value; }
    public int SkillPoints { get => skillPoints; set => skillPoints = value; }
    public int AttributePoints { get => attributePoints; set => attributePoints = value; }
    public int StatUpgradePoints { get => statUpgradePoints; set => statUpgradePoints = value; }
    public PlayerClass PlayerClass { get => playerClass; set => playerClass = value; }

    /// <summary>Experience needed to reach the next level (the XP bar's maximum).</summary>
    public int ExperienceToNextLevel => experienceToNextLevel;

    /// <summary>
    /// Network layer (owning client): sends a stat upgrade request to the server, which spends the point and applies it.
    /// Used by <see cref="UpgradeStat"/> where the rules do not run; returns true when the request was sent.
    /// </summary>
    public Func<string, bool> UpgradeRequest { get; set; }

    // True once a client's copy received the server's progression (Start must not recompute it).
    private bool replicated;

    // Available stats for upgrading
    public List<string> AvailableStats { get; private set; } = new List<string>
    {
        "health", "stamina", "mana", "speed", "strength", "agility",
        "intelligence", "endurance", "hunger", "thirst", "sleep", "sanity", "weight"
    };

    private void Start()
    {
        if (!replicated)
            experienceToNextLevel = CalculateExperienceForLevel(currentLevel + 1);
    }

    // Add experience and handle leveling
    public void AddExperience(int xp)
    {
        // Experience of a networked player is granted on the server; a client's copy receives the result.
        if (!GameNet.NetAuthority.RunsRules(gameObject))
            return;
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
        int skillPointsGained = CalculateSkillPointsGained(currentLevel);
        int attributePointsGained = CalculateAttributePointsGained(currentLevel);
        int statPointsGained = CalculateStatUpgradePointsGained(currentLevel);
        var gain = new LevelGain { level = currentLevel, skillPoints = skillPointsGained, attributePoints = attributePointsGained, statUpgradePoints = statPointsGained };

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
            gain.skillPoints += specialReward.skillPoints;
            gain.attributePoints += specialReward.attributePoints;
            gain.statUpgradePoints += specialReward.statUpgradePoints;

            // Handle unlocked traits if needed
            foreach (var trait in specialReward.unlockedTraits)
            {
                Debug.Log($"Unlocked new trait: {trait.Name}");
            }
        }

        experienceToNextLevel = CalculateExperienceForLevel(currentLevel + 1);
        levelGains.RemoveAll(g => g.level == currentLevel);
        levelGains.Add(gain);

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
        // Spending points is a server decision: a client asks for it (the result arrives through replication).
        if (!GameNet.NetAuthority.RunsRules(gameObject))
            return UpgradeRequest != null && !string.IsNullOrEmpty(statType) && UpgradeRequest(statType);
        if (statUpgradePoints <= 0)
        {
            Debug.Log("Not enough stat upgrade points!");
            return false;
        }
        if (string.IsNullOrEmpty(statType))
            return false;

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
            upgradeHistory.Add(new StatUpgradeRecord { stat = statType.ToLower(), amount = upgradeAmount, level = currentLevel });
            OnStatUpgraded?.Invoke(statType);
            Debug.Log($"Upgraded {statType} by {upgradeAmount:F1}. Remaining stat points: {statUpgradePoints}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Client copies of a networked player: applies the progression the server decided (level, experience, points).
    /// Raises <see cref="OnLevelUp"/> once when the level went up, so level-based growth and effects follow. Negative
    /// values are ignored.
    /// </summary>
    public void ApplyReplicated(int level, int experience, int toNextLevel, int skill, int attribute, int statUpgrade)
    {
        if (level < 1 || experience < 0 || toNextLevel < 0 || skill < 0 || attribute < 0 || statUpgrade < 0)
            return;
        int previousLevel = currentLevel;
        int previousStatPoints = statUpgradePoints;
        replicated = true;
        currentLevel = level;
        currentExperience = experience;
        experienceToNextLevel = toNextLevel;
        skillPoints = skill;
        attributePoints = attribute;
        statUpgradePoints = statUpgrade;
        if (level > previousLevel)
            OnLevelUp?.Invoke(level);
        else if (level < previousLevel)
            OnLevelDown?.Invoke(level);
        if (statUpgrade > previousStatPoints)
            OnStatUpgradePointsGained?.Invoke(statUpgrade - previousStatPoints);
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
                    // Combat attributes go to Combat Stats through the character's identity (kept for saving).
                    CombatStatType attribute = statType == "strength" ? CombatStatType.Strength
                        : statType == "agility" ? CombatStatType.Agility
                        : statType == "intelligence" ? CombatStatType.Intelligence : CombatStatType.Endurance;
                    CharacterIdentity.For(controller).AddAttribute(attribute, amount);
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
    private int CalculateSkillPointsGained(int level)
    {
        // Every level gives 1 skill point, every 5 levels gives bonus
        return 1 + (level % 5 == 0 ? 1 : 0);
    }

    // Calculate attribute points gained per level
    private int CalculateAttributePointsGained(int level)
    {
        // Every 3 levels gives 1 attribute point
        return level % 3 == 0 ? 1 : 0;
    }

    // Calculate stat upgrade points gained per level
    private int CalculateStatUpgradePointsGained(int level)
    {
        // Every level gives 1 stat upgrade point, with bonuses at certain levels
        int basePoints = 1;
        int bonusPoints = 0;

        // Bonus points at every 10 levels
        if (level % 10 == 0) bonusPoints += 2;
        // Small bonus at every 5 levels
        else if (level % 5 == 0) bonusPoints += 1;

        return basePoints + bonusPoints;
    }

    /// <summary>What reaching <paramref name="level"/> gave (recorded this session, else the level formulas and special rewards).</summary>
    public LevelGain GainOf(int level)
    {
        for (int i = levelGains.Count - 1; i >= 0; i--)
            if (levelGains[i].level == level) return levelGains[i];
        var g = new LevelGain
        {
            level = level,
            skillPoints = CalculateSkillPointsGained(level),
            attributePoints = CalculateAttributePointsGained(level),
            statUpgradePoints = CalculateStatUpgradePointsGained(level),
        };
        LevelUpReward r = GetSpecialLevelReward(level);
        if (r != null)
        {
            g.skillPoints += r.skillPoints;
            g.attributePoints += r.attributePoints;
            g.statUpgradePoints += r.statUpgradePoints;
        }
        return g;
    }

    // ------------------------------------------------------------------ losing experience and levels (death penalties)

    /// <summary>
    /// Removes <paramref name="amount"/> experience. With <paramref name="allowDelevel"/> the character can drop levels
    /// (never below <paramref name="minimumLevel"/>): each lost level takes back its points and undoes the stat points spent
    /// at it (<see cref="RegressToLevel"/>). Without, experience stops at 0 of the current level. Where the rules run only.
    /// </summary>
    public void LoseExperience(int amount, bool allowDelevel, int minimumLevel = 1)
    {
        if (amount <= 0 || !GameNet.NetAuthority.RunsRules(gameObject))
            return;
        minimumLevel = Mathf.Max(1, minimumLevel);
        currentExperience -= amount;
        int guard = 0;
        while (currentExperience < 0 && guard++ < 1000)
        {
            if (!allowDelevel || currentLevel <= minimumLevel)
            {
                currentExperience = 0;
                break;
            }
            int deficit = -currentExperience;
            RegressToLevel(currentLevel - 1, true);
            currentExperience = experienceToNextLevel - deficit;
        }
        OnExperienceLost?.Invoke(amount);
    }

    /// <summary>
    /// Sends the character back to <paramref name="targetLevel"/> (a death penalty): the skill, attribute and stat points of
    /// every lost level are taken back, and with <paramref name="undoStatUpgrades"/> the stat points spent at those levels
    /// are undone (max health, stamina... go back down; if more points were spent than are left, the latest upgrades are
    /// undone too). Experience restarts at 0 of the new level. Raises <see cref="OnLevelDown"/>. Where the rules run only.
    /// </summary>
    public void RegressToLevel(int targetLevel, bool undoStatUpgrades = true)
    {
        if (!GameNet.NetAuthority.RunsRules(gameObject))
            return;
        targetLevel = Mathf.Max(1, targetLevel);
        if (targetLevel >= currentLevel)
            return;
        var controller = GetComponent<PlayerStatusController>();
        int lostSkill = 0, lostAttribute = 0, lostStat = 0;
        for (int lv = currentLevel; lv > targetLevel; lv--)
        {
            LevelGain g = GainOf(lv);
            lostSkill += g.skillPoints;
            lostAttribute += g.attributePoints;
            lostStat += g.statUpgradePoints;
        }

        if (undoStatUpgrades && controller != null)
        {
            // The points spent above the new level are refunded (then taken back with the level).
            for (int i = upgradeHistory.Count - 1; i >= 0; i--)
            {
                if (upgradeHistory[i].level <= targetLevel) continue;
                UndoUpgrade(upgradeHistory[i], controller);
                upgradeHistory.RemoveAt(i);
                statUpgradePoints++;
            }
        }
        skillPoints = Mathf.Max(0, skillPoints - lostSkill);
        attributePoints = Mathf.Max(0, attributePoints - lostAttribute);
        statUpgradePoints -= lostStat;
        // More points were spent than the remaining levels gave: undo the latest upgrades.
        while (statUpgradePoints < 0 && undoStatUpgrades && controller != null && upgradeHistory.Count > 0)
        {
            UndoUpgrade(upgradeHistory[upgradeHistory.Count - 1], controller);
            upgradeHistory.RemoveAt(upgradeHistory.Count - 1);
            statUpgradePoints++;
        }
        statUpgradePoints = Mathf.Max(0, statUpgradePoints);

        int from = currentLevel;
        currentLevel = targetLevel;
        currentExperience = 0;
        experienceToNextLevel = CalculateExperienceForLevel(currentLevel + 1);
        levelGains.RemoveAll(g => g.level > targetLevel);
        Debug.Log($"[Experience] {name} went back from level {from} to {targetLevel}.", this);
        OnLevelDown?.Invoke(currentLevel);
    }

    private void UndoUpgrade(StatUpgradeRecord r, PlayerStatusController controller)
    {
        if (string.IsNullOrEmpty(r.stat) || Mathf.Approximately(r.amount, 0f))
            return;
        ApplyStatUpgrade(r.stat, -r.amount, controller);
    }

    /// <summary>Experience needed to go from <paramref name="level"/> - 1 to <paramref name="level"/>.</summary>
    public int ExperienceForLevel(int level) => CalculateExperienceForLevel(level);

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