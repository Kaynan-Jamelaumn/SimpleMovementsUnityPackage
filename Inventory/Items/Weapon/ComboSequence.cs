using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A fixed input string (e.g. Normal, Normal, Heavy) that performs a special attack when its last input is pressed.
/// It matches the END of the inputs of the current combo, so it also triggers in the middle of a longer string.
/// </summary>
[System.Serializable]
public class ComboSequence
{
    [Header("Combo Definition")]
    public string comboName;
    [Tooltip("Sequence of attack types that trigger this combo (the last one is the input that performs it).")]
    public AttackType[] requiredSequence;

    [Header("Combo Result")]
    [Tooltip("The special action executed when combo is completed")]
    public AttackAction specialAction;

    [Header("Visual & Audio")]
    public ParticleSystem comboFinisherParticles;
    public AudioClip comboFinisherSound;

    [Header("Bonuses")]
    [Tooltip("Damage multiplier of the finisher.")]
    public float damageMultiplier = 1.5f;
    [Tooltip("Extra critical chance (0-1) of the finisher.")]
    public float criticalChanceBonus = 0.1f;
    [Tooltip("Experience given when the combo is performed.")]
    public int experienceBonus = 10;

    /// <summary>Exact match of the whole input list (old behaviour).</summary>
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

    /// <summary>True when the last inputs of <paramref name="inputs"/> are this combo's sequence.</summary>
    public bool MatchesEnd(IReadOnlyList<AttackType> inputs)
    {
        if (requiredSequence == null || requiredSequence.Length == 0 || inputs == null || inputs.Count < requiredSequence.Length)
            return false;
        int offset = inputs.Count - requiredSequence.Length;
        for (int i = 0; i < requiredSequence.Length; i++)
            if (inputs[offset + i] != requiredSequence[i])
                return false;
        return true;
    }

    public bool IsValid()
    {
        return requiredSequence != null && requiredSequence.Length > 0 && specialAction != null;
    }

    public string SequenceText => requiredSequence == null ? "" : string.Join(" → ", requiredSequence);

    public void Validate(string owner, List<string> errors, List<string> warnings)
    {
        string me = $"{owner} ▸ {(string.IsNullOrEmpty(comboName) ? "combo" : comboName)}";
        if (requiredSequence == null || requiredSequence.Length == 0)
            errors.Add($"{me}: the sequence is empty.");
        else if (requiredSequence.Length == 1)
            warnings.Add($"{me}: a one-input sequence replaces that input's attack every time.");
        if (specialAction == null)
            errors.Add($"{me}: no Special Action.");
        else
            specialAction.Validate(me, errors, warnings);
    }
}
