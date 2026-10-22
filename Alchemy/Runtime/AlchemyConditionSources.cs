using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What the surroundings of a station are like at one moment: the ambient temperature and named variables.</summary>
public sealed class AlchemyConditionSample
{
    /// <summary>Air temperature around the vessel (°C); null = the database's default.</summary>
    public double? Ambient;
    public readonly SortedDictionary<string, double> Variables = new SortedDictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    public void Set(string name, double value)
    {
        if (!string.IsNullOrWhiteSpace(name))
            Variables[name] = value;
    }

    public void Clear()
    {
        Ambient = null;
        Variables.Clear();
    }
}

/// <summary>
/// A component that feeds crafting conditions to the <see cref="AlchemyStation"/> it is on (or under): the weather,
/// the time of day, a shrine's ley-line strength, a forge's heat... Reactions and outcomes read the variables by name
/// (Variable Range conditions), so a new kind of condition is one small component and some data - no change to the
/// core. Sources are read in hierarchy order; later ones override earlier ones.
/// </summary>
public interface IAlchemyConditionSource
{
    void Collect(AlchemyConditionSample sample);
}

/// <summary>
/// The world around a station: ambient temperature and humidity from the Weather System (or a fixed indoor
/// temperature), rain, and night from the world clock. Added to stations automatically when they have no condition
/// source of their own.
/// </summary>
[AddComponentMenu("SimpleMovements/Alchemy/Alchemy Surroundings")]
public class AlchemySurroundings : MonoBehaviour, IAlchemyConditionSource
{
    [Tooltip("Indoors: the ambient temperature is fixed (Indoor Temperature) and rain does not count.")]
    [SerializeField] private bool indoors = true;
    [Tooltip("Ambient temperature indoors (°C).")]
    [SerializeField] private float indoorTemperature = 20f;
    [Tooltip("Outdoors: added to the weather's temperature (a sheltered spot is warmer).")]
    [SerializeField] private float outdoorOffset;
    [Tooltip("Publish 'night' (1 at night, 0 by day) from the world clock.")]
    [SerializeField] private bool useWorldClock = true;

    public void Collect(AlchemyConditionSample sample)
    {
        WeatherSystem w = WeatherSystem.Instance;
        if (indoors || w == null)
        {
            sample.Ambient = indoors ? indoorTemperature : sample.Ambient;
        }
        else
        {
            WeatherState s = w.Current;
            sample.Ambient = s.Temperature + outdoorOffset;
            sample.Set(AlchemyVariables.Humidity, s.Humidity);
            sample.Set(AlchemyVariables.Rain, s.Rain);
        }
        if (w != null && indoors)
            sample.Set(AlchemyVariables.Humidity, w.Current.Humidity);
        if (useWorldClock && WorldTime.HasClock)
            sample.Set(AlchemyVariables.Night, WorldTime.IsNight ? 1 : 0);
    }
}

/// <summary>
/// Fixed condition variables for a station: a shrine's "ley" 0.8, a cold cellar's ambient −2 °C... Use it to give a
/// place its own alchemy without code; reactions and outcomes read the variables by name.
/// </summary>
[AddComponentMenu("SimpleMovements/Alchemy/Alchemy Fixed Conditions")]
public class AlchemyFixedConditions : MonoBehaviour, IAlchemyConditionSource
{
    [Tooltip("Variables published to the station's preparations.")]
    [SerializeField] private List<AlchemyVariable> variables = new List<AlchemyVariable>();
    [Tooltip("Override the ambient temperature.")]
    [SerializeField] private bool overrideAmbient;
    [SerializeField] private float ambientTemperature = 20f;

    public List<AlchemyVariable> Variables => variables;

    public void Collect(AlchemyConditionSample sample)
    {
        if (overrideAmbient)
            sample.Ambient = ambientTemperature;
        if (variables != null)
            foreach (AlchemyVariable v in variables)
                if (v != null) sample.Set(v.name, v.value);
    }
}
