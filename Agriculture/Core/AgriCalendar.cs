using System;
using UnityEngine;

/// <summary>
/// The farming calendar, from the game time in hours since the first day: the day, the season and the day in it, the
/// year, the moon's phase, the length of the day. Pure arithmetic: every machine reads the same date from the same time.
/// </summary>
public readonly struct AgriDate
{
    public readonly double Time;
    public readonly int Day;
    public readonly float Hour;
    public readonly Season Season;
    public readonly int DayOfSeason;
    public readonly int DayOfYear;
    public readonly int Year;
    public readonly MoonPhase Moon;
    /// <summary>0-1 through the moon's cycle (0 = new moon).</summary>
    public readonly float MoonCycle;
    /// <summary>0-1 through the season.</summary>
    public readonly float SeasonProgress;
    /// <summary>Hours of daylight this day (9 in mid-winter to 15 in mid-summer).</summary>
    public readonly float DayLength;

    public AgriDate(double time, AgriSettings s)
    {
        s ??= AgriSettings.Defaults();
        Time = time;
        double days = Math.Max(0.0, time) / 24.0;
        Day = (int)Math.Floor(days);
        Hour = (float)((days - Day) * 24.0);
        int dps = Math.Max(1, s.daysPerSeason);
        int yearDays = dps * 4;
        int shifted = Day + (int)s.startSeason * dps;
        DayOfYear = shifted % yearDays;
        Season = (Season)(DayOfYear / dps);
        DayOfSeason = DayOfYear % dps;
        Year = shifted / yearDays + 1;
        SeasonProgress = (DayOfSeason + Hour / 24f) / dps;
        int lunar = Math.Max(4, s.lunarCycleDays);
        double cycle = ((days % lunar) + lunar) % lunar / lunar;
        MoonCycle = (float)cycle;
        // New moon centred on cycle 0: each phase is a quarter.
        int phase = (int)Math.Floor(((cycle + 0.125) % 1.0) * 4.0);
        Moon = (MoonPhase)Mathf.Clamp(phase, 0, 3);
        float yearPos = (DayOfYear + Hour / 24f - 0.5f * dps) / yearDays;
        DayLength = 12f + 3f * (float)Math.Sin(2.0 * Math.PI * yearPos);
    }

    public float Sunrise => 12f - DayLength * 0.5f;
    public float Sunset => 12f + DayLength * 0.5f;

    /// <summary>"Day 3 of Spring, year 1".</summary>
    public string Describe() => $"Day {DayOfSeason + 1} of {Season}, year {Year}";

    /// <summary>"Day 3 of Spring, year 1 · Waxing moon · 14:05".</summary>
    public string Full() => $"{Describe()} · {AgriText.Moon(Moon)} · {Mathf.FloorToInt(Hour):00}:{Mathf.FloorToInt((Hour % 1f) * 60f):00}";

    public static SeasonMask Mask(Season s) => (SeasonMask)(1 << (int)s);
    public static bool In(SeasonMask mask, Season s) => mask == SeasonMask.None || (mask & Mask(s)) != 0;
}
