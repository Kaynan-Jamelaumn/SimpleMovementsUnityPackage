using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A city healer, hospital or surgeon that can treat a character's injuries (planned: paid treatment, reattaching limbs
/// past their time limit). Implement it and register it with <see cref="CityInjuryTreatment.Register"/>; the health
/// panel lists what registered providers offer.
/// </summary>
public interface ICityTreatmentProvider
{
    /// <summary>Name shown in the health panel ("Temple of Light").</summary>
    string ProviderName { get; }

    /// <summary>Is the provider available to <paramref name="patient"/> right now (in range, open, in the city)?</summary>
    bool IsAvailableTo(InjuryController patient, out string reason);

    /// <summary>The price of treating <paramref name="injury"/> on <paramref name="part"/> (null injury = reattach its limb).</summary>
    float PriceFor(InjuryController patient, InjuryPartState part, Injury injury);

    /// <summary>Treats it (charging the price). Returns true when done.</summary>
    bool Treat(InjuryController patient, InjuryPartState part, Injury injury, out string message);
}

/// <summary>
/// City-based treatment - PLACEHOLDER. The injury system is ready for it: <see cref="TreatmentSource.City"/>, the city
/// settings (<see cref="CityTreatmentSettings"/>), this registry of <see cref="ICityTreatmentProvider"/>s and a
/// stand-in healer (<see cref="CityHealer"/>) that tells players city treatment is coming. The full mechanics (prices,
/// currencies, hub-world zones, waiting times) are not implemented yet.
/// </summary>
public static class CityInjuryTreatment
{
    private static readonly List<ICityTreatmentProvider> providers = new List<ICityTreatmentProvider>();

    /// <summary>The registered providers.</summary>
    public static IReadOnlyList<ICityTreatmentProvider> Providers => providers;

    /// <summary>Is city treatment usable at all (settings on and a provider registered)? Always false while it is a placeholder.</summary>
    public static bool IsAvailable => InjurySettings.Instance.cityTreatment.enabled && providers.Count > 0 && Implemented;

    /// <summary>False until the city treatment mechanics exist.</summary>
    public const bool Implemented = false;

    public static void Register(ICityTreatmentProvider provider)
    {
        if (provider != null && !providers.Contains(provider))
            providers.Add(provider);
    }

    public static void Unregister(ICityTreatmentProvider provider) => providers.Remove(provider);

    /// <summary>The line the health panel shows about city treatment (empty = nothing to show).</summary>
    public static string StatusLine
    {
        get
        {
            CityTreatmentSettings s = InjurySettings.Instance.cityTreatment;
            if (!s.enabled)
                return "";
            return string.IsNullOrWhiteSpace(s.placeholderNote) ? "City treatment: not available yet." : s.placeholderNote;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => providers.Clear();
}
