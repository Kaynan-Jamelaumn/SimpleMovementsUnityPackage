using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Cold or hot side of the comfortable range.</summary>
public enum TemperatureSide { Cold, Hot }

/// <summary>One temperature effect: what happens when the body is this far outside its comfortable range.</summary>
[Serializable]
public class TemperatureTier
{
    [Tooltip("Too cold or too hot.")]
    public TemperatureSide side = TemperatureSide.Cold;
    [Tooltip("Starts this many °C beyond the edge of the comfortable range (0 = as soon as the body leaves it).")]
    [Min(0f)] public float degreesBeyondComfort = 0f;
    [Tooltip("What it does (status icon, stats, health loss, hunger / thirst drain, shivering / fainting).")]
    public StatusConsequence effect = new StatusConsequence("Chilly");

    public TemperatureTier Clone()
    {
        var c = (TemperatureTier)MemberwiseClone();
        c.effect = effect != null ? effect.Clone() : new StatusConsequence("");
        return c;
    }
}

/// <summary>
/// The body temperature of a race or class: its ideal temperature, how far from it it stays comfortable, and optionally its
/// own temperature effects (a desert race that shrugs off heat but suffers badly in the cold). Heat and cold resistance
/// (and immunity at 100%) come from the Heat Resistance / Cold Resistance combat stats - set them in the archetype's Base
/// Combat Stats, a trait, equipment or a buff.
/// </summary>
[Serializable]
public class TemperatureProfile
{
    [Tooltip("This archetype has its own ideal temperature (off: the Body Heat Manager's defaults, or another archetype's).")]
    public bool customTemperature = false;
    [Tooltip("Air temperature (°C) the body feels best at. Humans: about 18-20. A desert race: 30. A frost giant: 0.")]
    public float idealCelsius = 18f;
    [Tooltip("The body stays comfortable this many °C around the ideal (8 = comfortable from 10 to 26 °C for an ideal of 18).")]
    [Min(0f)] public float toleranceCelsius = 8f;
    [Tooltip("How fast the body temperature follows the air (1 = normal, 0.5 = adapts slowly, 2 = quickly).")]
    [Min(0f)] public float adaptSpeed = 1f;
    [Tooltip("Use the effects below instead of the Body Heat Manager's.")]
    public bool customEffects = false;
    [Tooltip("This archetype's temperature effects (with Custom Effects).")]
    public List<TemperatureTier> effects = new List<TemperatureTier>();

    /// <summary>Does it change anything?</summary>
    public bool IsUsed => customTemperature || customEffects;

    /// <summary>"Ideal 30 °C (comfortable 22-38 °C)".</summary>
    public string Describe() =>
        $"Ideal {idealCelsius:0} °C (comfortable {idealCelsius - toleranceCelsius:0} to {idealCelsius + toleranceCelsius:0} °C)";
}
