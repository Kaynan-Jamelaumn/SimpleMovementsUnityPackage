using UnityEngine;
using System;

public class ExperienceManager : MonoBehaviour
{
    [SerializeField] private int currentExperience;
    [SerializeField] private int experienceToNextSkillPoint;
    [SerializeField] private int skillPoints;
    public event Action OnSkillPointGained;
    public int CurrentExperience { get => currentExperience; set => currentExperience = value; }
    public int SkillPoints { get => skillPoints; set => skillPoints = value; }

    // This method is called to add experience
    public void AddExperience(int xp)
    {
        currentExperience += xp;

        // Check if the experience threshold has been reached to award a skill point
        while (currentExperience >= experienceToNextSkillPoint)
        {
            currentExperience -= experienceToNextSkillPoint;
            skillPoints++;
            OnSkillPointGained?.Invoke();
            IncreaseExperienceThreshold();
        }
    }

    private void IncreaseExperienceThreshold()
    {
        experienceToNextSkillPoint += CalculateIncrement();
    }

    // Placeholder for a function that calculates the next experience increment
    private int CalculateIncrement()
    {
        // For example, increase by a fixed amount, or by a percentage:
        return experienceToNextSkillPoint / 10; // Increase by 10%
    }
}