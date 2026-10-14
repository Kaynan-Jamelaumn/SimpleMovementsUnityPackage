using UnityEngine;

/// <summary>
/// One weather condition as an asset (Soaked, Chilled, Frostbite...): when it happens (the measure and threshold, open
/// sky, underwater) and everything it does to the player (stats, resistances, character stats, damage, status bars,
/// body heat, an effect, events). Add it to the Player Weather Effects' Conditions list. Edit it in the Inspector -
/// nothing in the code needs changing - and use the same asset in several scenes.
/// </summary>
[CreateAssetMenu(fileName = "Weather Condition", menuName = "SimpleMovements/Weather/Weather Condition", order = 10)]
public class WeatherCondition : ScriptableObject
{
    [Tooltip("When this condition happens and what it does.")]
    public WeatherExposureRule rule = new WeatherExposureRule();

    private void OnValidate()
    {
        if (rule != null && (string.IsNullOrEmpty(rule.label) || rule.label == "Weather"))
            rule.label = name;
    }
}
