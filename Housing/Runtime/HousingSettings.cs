using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The project's housing: the rules (what a lease day is, what sizes are worth, the districts, the market, the inns,
/// the limits per player, what happens to what tenants leave behind) and the editor's checks of the city's areas.
/// Create it with Tools ▸ SimpleMovements ▸ Housing ▸ Create Housing Settings (it lives at
/// Resources/SimpleMovements/Settings/HousingSettings); without one the built-in rules are used.
/// </summary>
[CreateAssetMenu(fileName = "HousingSettings", menuName = "SimpleMovements/Housing/Housing Settings", order = 0)]
public class HousingSettings : ScriptableObject
{
    public const string ResourcesName = "HousingSettings";

    [Tooltip("The rules of housing.")]
    public HousingRules rules = HousingRules.Defaults();

    [Header("Places")]
    [Tooltip("The money rents and bids are paid in (empty: the project's default currency). A number currency: rent accounts and refunds are numbers.")]
    public CurrencyDefinition currency;
    [Tooltip("The doors of a house nobody rents open for anyone, so players can look round it before they bid.")]
    public bool vacantDoorsOpen = true;
    [Tooltip("Real seconds a change waits before the world is saved (the housing books with the kept wallets; changes in between go together). It is also saved when the session changes and on quit.")]
    [Range(0.5f, 60f)] public float saveDelaySeconds = 2f;
    [Tooltip("Real milliseconds a frame may spend on the market's clearings. A long jump of the clock (a sleep or a skip on game " +
             "time, days of clearings) is then cleared over a few frames instead of freezing one; requests wait meanwhile (0: all at once).")]
    [Range(0f, 50f)] public float clearingBudgetMs = 4f;

    [Header("Editor checks")]
    [Tooltip("Layers of objects that may lie on a property's land without counting as standing on it (the ground, roads, decals).")]
    public LayerMask groundLayers = 0;
    [Tooltip("Square metres an object of the city may cover of a property's land before the validator warns (it errs from 1 m²).")]
    [Min(0.01f)] public float intrusionWarnArea = 0.05f;

    private static HousingSettings instance;
    private static bool searched;

    public static bool HasInstance => instance != null;

    /// <summary>The project's settings (Resources/SimpleMovements/Settings/HousingSettings), or the built-in rules.</summary>
    public static HousingSettings Instance
    {
        get
        {
            if (instance == null)
            {
                if (!searched)
                {
                    searched = true;
                    instance = SMAssets.Load<HousingSettings>(SMAssets.Settings, ResourcesName);
                }
                if (instance == null)
                {
                    instance = CreateInstance<HousingSettings>();
                    instance.name = "HousingSettings (built-in)";
                    instance.hideFlags = HideFlags.DontSave;
                }
            }
            return instance;
        }
    }

    /// <summary>Uses specific settings (tests, bootstrap scripts). Null = find them again.</summary>
    public static void Override(HousingSettings settings)
    {
        instance = settings;
        searched = settings != null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        searched = false;
    }

    public HousingRules Rules => rules ??= HousingRules.Defaults();

    /// <summary>The currency of rents and bids.</summary>
    public CurrencyDefinition Currency => currency != null ? currency : CurrencyDefinition.Default;

    /// <summary>Fresh defaults (the Create menu).</summary>
    public void Defaults() => rules = HousingRules.Defaults();

    /// <summary>The names of the districts (the editor's district picker).</summary>
    public List<string> DistrictNames()
    {
        var names = new List<string>();
        if (Rules.districts != null)
            foreach (HousingDistrict d in Rules.districts)
                if (d != null && !string.IsNullOrWhiteSpace(d.name) && !names.Contains(d.name.Trim())) names.Add(d.name.Trim());
        return names;
    }

    /// <summary>Problems with the settings.</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        HousingRules r = Rules;
        if (r.districts == null || r.districts.Count == 0) warnings.Add("No districts: every property counts as an ordinary district.");
        var seen = new HashSet<string>();
        if (r.districts != null)
            foreach (HousingDistrict d in r.districts)
            {
                if (d == null || string.IsNullOrWhiteSpace(d.name)) { warnings.Add("A district has no name."); continue; }
                if (!seen.Add(d.name.Trim().ToLowerInvariant())) errors.Add($"Two districts are called '{d.name}'.");
            }
        if (r.monthDays <= r.weekDays) warnings.Add("A month is not longer than a week.");
        CurrencyDefinition money = Currency;
        if (money != null && !money.KeepBetweenSessions)
            warnings.Add($"Housing is paid in {money.DisplayName}, which is not kept between sessions (a coin item, or Keep Between Sessions off): " +
                         "in a network session the housing office refuses it (money that comes back every session would buy houses for free).");
        if (r.clock == HousingClockKind.GameTime)
            warnings.Add("Leases run on game days in every world: online, a week lasts about " +
                         $"{r.weekDays * r.gameDayMinutes / 60f:0.#} real hours - players who log off for a night lose their homes. Auto (real days online) is advised.");
        if (r.zonePerSquareMetre == null || r.zonePerSquareMetre.Length < 8) warnings.Add("Zone rates should have one value per Land Use (8).");
    }
}
