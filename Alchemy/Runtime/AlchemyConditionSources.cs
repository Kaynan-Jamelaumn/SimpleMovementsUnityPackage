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

