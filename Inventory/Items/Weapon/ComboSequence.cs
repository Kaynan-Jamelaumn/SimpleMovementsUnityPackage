using UnityEngine;

[System.Serializable]
public class ComboSequence
{
    [Header("Combo Definition")]
    public string comboName;
    [Tooltip("Sequence of attack types that trigger this combo")]
    public AttackType[] requiredSequence;

    [Header("Combo Result")]
    [Tooltip("The special action executed when combo is completed")]
    public AttackAction specialAction;

    [Header("Visual & Audio")]
    public ParticleSystem comboFinisherParticles;
    public AudioClip comboFinisherSound;

    [Header("Bonuses")]
    public float damageMultiplier = 1.5f;
    public float criticalChanceBonus = 0.1f;
    public int experienceBonus = 10;

    public bool IsSequenceMatch(AttackType[] playerSequence)
    {
        if (requiredSequence == null || playerSequence == null) return false;
        if (playerSequence.Length != requiredSequence.Length) return false;

        for (int i = 0; i < requiredSequence.Length; i++)
        {
            if (playerSequence[i] != requiredSequence[i]) return false;
        }
        return true;
    }

    public bool IsValid()
    {
        return requiredSequence != null && requiredSequence.Length > 0 && specialAction != null;
    }
}