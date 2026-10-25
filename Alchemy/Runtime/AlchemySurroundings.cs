using System;
using System.Collections.Generic;
using UnityEngine;

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
