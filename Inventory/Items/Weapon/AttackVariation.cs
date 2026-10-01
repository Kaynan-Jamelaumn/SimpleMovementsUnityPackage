using UnityEngine;

/// <summary>
/// One step of an attack chain: the 2nd, 3rd... attack played when the same input is pressed again within the
/// action's Variant Time. It has every setting of an attack (<see cref="AttackComponent"/>).
/// </summary>
[System.Serializable]
public class AttackVariation : AttackComponent
{
    [Header("Variation Configuration")]
    [Tooltip("Name shown in tooltips and debug output.")]
    public string variationName;

    public override string DisplayName => string.IsNullOrEmpty(variationName) ? "Variation" : variationName;
}
