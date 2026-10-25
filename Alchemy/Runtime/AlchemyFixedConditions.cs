using System;
using System.Collections.Generic;
using UnityEngine;

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
