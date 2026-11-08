using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a "day" of a lease is. Either way, housing time stops while the game or the server is not running.</summary>
public enum HousingClockKind
{
    /// <summary>Real Time in a network session (people log off for nights and weekends), Game Time offline.</summary>
    Auto = 0,
    /// <summary>A day of the game's calendar (it runs with the Day Night Cycle: 24 real minutes by default).</summary>
    GameTime = 1,
    /// <summary>A stretch of real time (Real Hours Per Day).</summary>
    RealTime = 2,
}

/// <summary>A part of the city, and how wanted it is.</summary>
[Serializable]
public class HousingDistrict
{
    public string name = "City";
    [Tooltip("Multiplies the base rent of its properties (1 = ordinary; a fine quarter 1.5, the docks 0.7).")]
    [Range(0.1f, 10f)] public float desirability = 1f;
}

/// <summary>
/// The rules of housing: what a day of a lease is, what sizes are worth, the market (auctions, the district price
/// index, renewals), the inns' prices, the limits per player, and what happens to what tenants leave behind. Held by the
/// <see cref="HousingSettings"/> asset; pure data for the rules in Housing/Core.
/// </summary>
[Serializable]
public class HousingRules
{
    [Header("Time")]
    [Tooltip("What a day of a lease is: Auto = real days in a network session (players log off for nights and weekends), game days " +
             "offline; or always one or the other. Housing time stops while the game or server is not running, so nothing ends while it is down.")]
    public HousingClockKind clock = HousingClockKind.Auto;
    [Tooltip("Real Time: real hours in a lease day.")]
    [Range(0.05f, 168f)] public float realHoursPerDay = 24f;
    [Tooltip("Game Time: real minutes in a lease day (a steady pace: the sky's night speed, pauses and skips do not change it).")]
    [Range(0.5f, 1440f)] public float gameDayMinutes = 24f;
    [Tooltip("Real minutes housing time waits after a restart (at most as long as the game was down): players have time to come back before anything ends.")]
    [Range(0f, 240f)] public float restartHoldMinutes = 15f;
    [Tooltip("Days in a weekly lease.")]
    [Range(1, 60)] public int weekDays = 7;
    [Tooltip("Days in a monthly lease.")]
    [Range(2, 365)] public int monthDays = 28;

    [Header("What sizes are worth (base rent per day)")]
    [Tooltip("Per square metre of the house's footprint, for the ground floor.")]
    [Min(0f)] public float housePerSquareMetre = 0.12f;
    [Tooltip("Each floor above the ground floor is worth this share of the ground floor.")]
    [Range(0f, 2f)] public float extraFloorShare = 0.6f;
    [Tooltip("Per square metre of land that is neither house, garden nor a zone with its own rate.")]
    [Min(0f)] public float landPerSquareMetre = 0.02f;
    [Tooltip("Per square metre of garden (its Soil Fields).")]
    [Min(0f)] public float gardenPerSquareMetre = 0.06f;
    [Tooltip("Per square metre of each kind of zone (indexed by Land Use: Garden, Yard, Orchard, Pasture, Workshop, Storage, Courtyard, Other). " +
             "The Garden rate is for garden zones without a Soil Field; Soil Fields use Garden Per Square Metre.")]
    public float[] zonePerSquareMetre = { 0.06f, 0.025f, 0.05f, 0.02f, 0.05f, 0.04f, 0.03f, 0.02f };
    [Tooltip("The least base rent of any property, per day.")]
    [Min(1)] public int minDailyRent = 2;
    [Tooltip("The city's districts and how wanted each is (a property's district not listed counts as 1).")]
    public List<HousingDistrict> districts = new List<HousingDistrict> { new HousingDistrict() };

    [Header("The market of houses")]
    public HousingMarketRules market = new HousingMarketRules();

    [Header("Inns")]
    public InnRules inns = new InnRules();

    [Header("Limits per player")]
    [Tooltip("Inn rooms a player may have at once in all the inns (one per inn at most).")]
    [Range(1, 10)] public int maxInnRooms = 2;
    [Tooltip("People a tenant may let into one place.")]
    [Range(1, 64)] public int maxGrants = 16;

    [Header("What tenants leave behind")]
    public HeldRules held = new HeldRules();

    public static HousingRules Defaults() => new HousingRules();

    /// <summary>The clock in use (Auto resolved: real time when networked).</summary>
    public HousingClockKind ClockIn(bool networked) => clock == HousingClockKind.Auto ? (networked ? HousingClockKind.RealTime : HousingClockKind.GameTime) : clock;

    public HousingMarketRules Market() => market ??= new HousingMarketRules();
    public InnRules Inns() => inns ??= new InnRules();
    public HeldRules Held() => held ??= new HeldRules();

    /// <summary>A lease term in days.</summary>
    public int Days(LeaseTerm term) => term == LeaseTerm.Week ? Math.Max(1, weekDays) : Math.Max(2, monthDays);

    /// <summary>How wanted a district is (1 when it is not listed).</summary>
    public float Desirability(string district)
    {
        if (districts != null && !string.IsNullOrEmpty(district))
            foreach (HousingDistrict d in districts)
                if (d != null && string.Equals(d.name?.Trim(), district.Trim(), StringComparison.OrdinalIgnoreCase))
                    return Mathf.Max(0.01f, d.desirability);
        return 1f;
    }

    public float ZoneRate(int use) => zonePerSquareMetre != null && use >= 0 && use < zonePerSquareMetre.Length ? Mathf.Max(0f, zonePerSquareMetre[use]) : landPerSquareMetre;
}

/// <summary>How inns price their rooms and how long a stay may be.</summary>
[Serializable]
public class InnRules
{
    [Tooltip("How full an inn is meant to be (0-1): fuller lately, its prices are higher; emptier, lower.")]
    [Range(0.2f, 0.95f)] public float targetOccupancy = 0.7f;
    [Tooltip("How strongly prices follow occupancy: the price level is e^(this x (occupancy - target)), within the bounds.")]
    [Range(0f, 6f)] public float priceResponse = 2f;
    [Tooltip("Days over which occupancy is averaged (a busy evening does not move prices much; a busy week does).")]
    [Range(0.1f, 30f)] public float occupancyDays = 2f;
    [Tooltip("A room counts as occupied while its guest (or someone they let in) was in it or slept in its bed within this many hours " +
             "(and, just let, for Just Let Hours): rooms booked only to push prices up do not count.")]
    [Range(1f, 240f)] public float activeHours = 24f;
    [Tooltip("A room just let counts as occupied for this many hours even before its guest comes (then only while they use it).")]
    [Range(0f, 24f)] public float justLetHours = 2f;
    [Tooltip("The lowest and highest price level, times the inn's own nightly price.")]
    [Range(0.1f, 1f)] public float minLevel = 0.6f;
    [Range(1f, 10f)] public float maxLevel = 2f;
    [Tooltip("The last free rooms cost more: up to this much more (0.5 = +50 %) for the last room, from the target occupancy up.")]
    [Range(0f, 3f)] public float scarcity = 0.5f;
    [Tooltip("Most nights a stay may run ahead (paid in advance).")]
    [Range(1, 120)] public int maxNights = 14;
    [Tooltip("Game Time only: the hour of the day a stay ends (a night lasts until this hour of the next morning).")]
    [Range(0f, 23.99f)] public float checkoutHour = 10f;
    [Tooltip("Game Time only: a night lasts at least this many hours (renting at 9:00 runs until the next morning, not one hour).")]
    [Range(0f, 23f)] public float minNightHours = 6f;
}

/// <summary>What happens to what a tenant left in a place's chests when the stay or lease ended.</summary>
[Serializable]
public class HeldRules
{
    [Tooltip("Days the items are held for free; after that a claim costs Claim Fee.")]
    [Min(0f)] public float freeDays = 3f;
    [Tooltip("What a claim costs after the free days (0 = always free).")]
    [Min(0)] public int fee = 0;
    [Tooltip("Days after which unclaimed items are thrown away (0 = kept until claimed).")]
    [Min(0f)] public float keepDays = 0f;
    [Tooltip("Lease days a player may stay away (counted only while the game or server runs) before what is held for them is thrown away (0 = never).")]
    [Min(0f)] public float forgetAfterDays = 0f;
    [Tooltip("Stacks a player may have held at once: above it they must claim before they rent again (nothing is thrown away).")]
    [Range(1, 2000)] public int maxStacks = 120;
}

/// <summary>The base rent of a property: what its sizes, district and quality are worth before the market moves it.</summary>
public static class HousingPricing
{
    /// <summary>
    /// Base rent per day (currency, fractional). Set by hand (Base Rent Per Day) or from the sizes: the house's footprint
    /// (the ground floor in full, each floor above at Extra Floor Share), the gardens (Soil Fields, at Garden Per Square
    /// Metre), each zone at its rate (garden zones without a Soil Field at the Garden zone rate) and the rest of the land -
    /// times the quality and the district's desirability; never below Min Daily Rent.
    /// </summary>
    public static double BaseDaily(PropertyFacts f, HousingRules r)
    {
        if (f == null || r == null) return 1.0;
        if (f.baseRentPerDay > 0) return f.baseRentPerDay;
        double house = f.houseFootprint * r.housePerSquareMetre * (1.0 + r.extraFloorShare * Math.Max(0, f.floors - 1));
        double zones = 0.0, zoneArea = 0.0;
        if (f.zoneArea != null)
            for (int u = 0; u < f.zoneArea.Length; u++)
            {
                zones += f.zoneArea[u] * r.ZoneRate(u); // a garden zone without a Soil Field at the Garden zone rate
                zoneArea += f.zoneArea[u];
            }
        double garden = f.gardenArea * r.gardenPerSquareMetre;
        double rest = Math.Max(0.0, f.landArea - f.houseFootprint - f.gardenArea - zoneArea) * r.landPerSquareMetre;
        double value = (house + garden + zones + rest) * Math.Max(0.01, f.quality) * r.Desirability(f.district);
        return Math.Max(Math.Max(1, r.minDailyRent), value);
    }
}
