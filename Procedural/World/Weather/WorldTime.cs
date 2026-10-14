using UnityEngine;

/// <summary>The part of the day.</summary>
public enum DayPhase
{
    Night,
    Dawn,
    Day,
    Dusk,
}

/// <summary>
/// The time of day as every system reads it (spawn rules, weather effects, music): the <see cref="DayNightCycle"/> when
/// there is one, else the Weather System's Time Of Day, else the old <see cref="DayNightManager"/>. In a network session
/// the server's clock decides (spawning runs there); clients show the time the server sends with the weather.
/// </summary>
public static class WorldTime
{
    /// <summary>Hour of the day (0-24), or -1 when the scene has no clock at all.</summary>
    public static float Hour
    {
        get
        {
            if (DayNightCycle.Instance != null && DayNightCycle.Instance.isActiveAndEnabled)
                return DayNightCycle.Instance.Hour;
            if (WeatherSystem.Instance != null && WeatherSystem.Instance.TimeOfDay >= 0f)
                return WeatherSystem.Instance.TimeOfDay;
            if (DayNightManager.instance != null)
                return DayNightManager.instance.isDayTime ? 12f : 0f;
            return -1f;
        }
    }

    /// <summary>Is there a clock (a Day Night Cycle, a Weather System day cycle or a Day Night Manager)?</summary>
    public static bool HasClock => Hour >= 0f;

    /// <summary>Hour the sun rises (from the Day Night Cycle; 6 without one).</summary>
    public static float SunriseHour => DayNightCycle.Instance != null ? DayNightCycle.Instance.sunriseHour : 6f;

    /// <summary>Hour the sun sets (from the Day Night Cycle; 19 without one).</summary>
    public static float SunsetHour => DayNightCycle.Instance != null ? DayNightCycle.Instance.sunsetHour : 19f;

    /// <summary>Day (between sunrise and sunset)? True when there is no clock.</summary>
    public static bool IsDay => !HasClock || IsDayAt(Hour, SunriseHour, SunsetHour);

    /// <summary>Night? False when there is no clock.</summary>
    public static bool IsNight => HasClock && !IsDay;

    /// <summary>Day (true), night (false), or null when there is no clock (time-based rules then let everything through).</summary>
    public static bool? IsDayOrNull => HasClock ? IsDay : (bool?)null;

    /// <summary>The part of the day now (Day when there is no clock).</summary>
    public static DayPhase Phase => HasClock ? PhaseAt(Hour, SunriseHour, SunsetHour) : DayPhase.Day;

    /// <summary>Is <paramref name="hour"/> between sunrise and sunset?</summary>
    public static bool IsDayAt(float hour, float sunrise, float sunset)
    {
        hour = Mathf.Repeat(hour, 24f);
        return sunrise <= sunset ? hour >= sunrise && hour < sunset : hour >= sunrise || hour < sunset;
    }

    /// <summary>The part of the day at <paramref name="hour"/>: dawn and dusk are the hour around sunrise and sunset.</summary>
    public static DayPhase PhaseAt(float hour, float sunrise, float sunset)
    {
        hour = Mathf.Repeat(hour, 24f);
        if (Mathf.Abs(Mathf.DeltaAngle(hour * 15f, sunrise * 15f)) <= 7.5f) return DayPhase.Dawn;
        if (Mathf.Abs(Mathf.DeltaAngle(hour * 15f, sunset * 15f)) <= 7.5f) return DayPhase.Dusk;
        return IsDayAt(hour, sunrise, sunset) ? DayPhase.Day : DayPhase.Night;
    }

    /// <summary>Is <paramref name="hour"/> inside [from, to) (wrapping past midnight: 22-4 is the night)?</summary>
    public static bool InHours(float hour, float from, float to)
    {
        hour = Mathf.Repeat(hour, 24f);
        from = Mathf.Repeat(from, 24f);
        to = Mathf.Repeat(to, 24f);
        if (Mathf.Approximately(from, to)) return true;
        return from < to ? hour >= from && hour < to : hour >= from || hour < to;
    }
}
