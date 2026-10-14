#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for the day/night clock maths, the time and weather spawn rules and the weather's player statuses.
/// </summary>
public class WeatherRuleTests
{
    // ------------------------------------------------------------------ the clock

    [Test]
    public void DayIsBetweenSunriseAndSunset()
    {
        Assert.IsTrue(WorldTime.IsDayAt(12f, 6f, 19f));
        Assert.IsFalse(WorldTime.IsDayAt(2f, 6f, 19f));
        Assert.IsFalse(WorldTime.IsDayAt(19f, 6f, 19f), "sunset itself is night");
        Assert.IsTrue(WorldTime.IsDayAt(6f, 6f, 19f), "sunrise itself is day");
        Assert.IsTrue(WorldTime.IsDayAt(36f, 6f, 19f), "hours wrap around 24");
    }

    [Test]
    public void HourRangesWrapPastMidnight()
    {
        Assert.IsTrue(WorldTime.InHours(23f, 20f, 4f));
        Assert.IsTrue(WorldTime.InHours(2f, 20f, 4f));
        Assert.IsFalse(WorldTime.InHours(12f, 20f, 4f));
        Assert.IsTrue(WorldTime.InHours(10f, 9f, 17f));
        Assert.IsFalse(WorldTime.InHours(17f, 9f, 17f), "the end hour is excluded");
        Assert.IsTrue(WorldTime.InHours(5f, 8f, 8f), "an empty range means all day");
    }

    [Test]
    public void PhasesAroundSunriseAndSunset()
    {
        Assert.AreEqual(DayPhase.Dawn, WorldTime.PhaseAt(6.2f, 6f, 19f));
        Assert.AreEqual(DayPhase.Day, WorldTime.PhaseAt(12f, 6f, 19f));
        Assert.AreEqual(DayPhase.Dusk, WorldTime.PhaseAt(18.8f, 6f, 19f));
        Assert.AreEqual(DayPhase.Night, WorldTime.PhaseAt(0f, 6f, 19f));
    }

    // ------------------------------------------------------------------ spawn conditions

    [Test]
    public void DefaultConditionsAllowEverything()
    {
        var c = new SpawnConditions();
        Assert.IsFalse(c.IsUsed);
        Assert.IsTrue(c.Allows(Vector3.zero));
        Assert.IsTrue(c.TimeAllows(3f));
        Assert.IsTrue(c.WeatherAllows(WeatherType.Blizzard));
    }

    [Test]
    public void TimeRules()
    {
        var night = new SpawnConditions { time = SpawnTimeRule.NightOnly };
        Assert.IsTrue(night.TimeAllows(1f));
        Assert.IsFalse(night.TimeAllows(12f));
        Assert.IsTrue(night.TimeAllows(-1f), "no clock in the scene: the rule lets it through");

        var day = new SpawnConditions { time = SpawnTimeRule.DayOnly };
        Assert.IsTrue(day.TimeAllows(12f));
        Assert.IsFalse(day.TimeAllows(23f));

        var hours = new SpawnConditions { time = SpawnTimeRule.Hours, fromHour = 22f, toHour = 3f };
        Assert.IsTrue(hours.TimeAllows(0.5f));
        Assert.IsFalse(hours.TimeAllows(20f));
    }

    [Test]
    public void WeatherRules()
    {
        var storm = new SpawnConditions { weather = SpawnWeatherRule.OnlyIn, weathers = new List<WeatherType> { WeatherType.Thunderstorm, WeatherType.Rain } };
        Assert.IsTrue(storm.IsUsed);
        Assert.IsTrue(storm.WeatherAllows(WeatherType.Rain));
        Assert.IsFalse(storm.WeatherAllows(WeatherType.Clear));

        var noSnow = new SpawnConditions { weather = SpawnWeatherRule.NeverIn, weathers = new List<WeatherType> { WeatherType.Snow, WeatherType.Blizzard } };
        Assert.IsFalse(noSnow.WeatherAllows(WeatherType.Blizzard));
        Assert.IsTrue(noSnow.WeatherAllows(WeatherType.HeatWave));

        var emptyList = new SpawnConditions { weather = SpawnWeatherRule.OnlyIn };
        Assert.IsFalse(emptyList.IsUsed, "a weather rule without weathers is ignored");
        Assert.IsTrue(emptyList.WeatherAllows(WeatherType.Fog));
    }

    // ------------------------------------------------------------------ player statuses

    [Test]
    public void ExposureRulesMatchTheirMeasure()
    {
        var soaked = new WeatherExposureRule { measure = WeatherMeasure.Rain, threshold = 0.35f };
        Assert.IsFalse(soaked.Matches(new WeatherState { Rain = 0.2f }, out _));
        Assert.IsTrue(soaked.Matches(new WeatherState { Rain = 1f }, out float full));
        Assert.AreEqual(1f, full, 1e-4f);
        Assert.AreEqual(1f, soaked.StrengthFor(full), 1e-4f);
        Assert.AreEqual(0.5f, soaked.StrengthFor(0f), 1e-4f, "half strength at the threshold");

        var cold = new WeatherExposureRule { measure = WeatherMeasure.ColderThan, threshold = 2f };
        Assert.IsTrue(cold.Matches(new WeatherState { Temperature = -5f }, out _));
        Assert.IsFalse(cold.Matches(new WeatherState { Temperature = 10f }, out _));

        var hot = new WeatherExposureRule { measure = WeatherMeasure.HotterThan, threshold = 35f };
        Assert.IsTrue(hot.Matches(new WeatherState { Temperature = 40f }, out _));

        var wind = new WeatherExposureRule { measure = WeatherMeasure.WindSpeed, threshold = 14f };
        Assert.IsTrue(wind.Matches(new WeatherState { WindSpeed = 20f }, out _));
        Assert.IsFalse(wind.Matches(new WeatherState { WindSpeed = 5f }, out _));
    }

    [Test]
    public void StrengthIsConfigurable()
    {
        var cold = new WeatherExposureRule
        {
            measure = WeatherMeasure.ColderThan, threshold = 0f, customFullStrength = true, fullStrengthAt = -10f,
            strengthAtThreshold = 0.2f,
        };
        Assert.IsTrue(cold.Matches(new WeatherState { Temperature = -5f }, out float half));
        Assert.AreEqual(0.5f, half, 1e-4f, "halfway between the threshold and Full Strength At");
        Assert.AreEqual(0.6f, cold.StrengthFor(half), 1e-4f);
        Assert.AreEqual(0.2f, cold.StrengthFor(0f), 1e-4f, "Strength At Threshold");
        cold.scaleWithIntensity = false;
        Assert.AreEqual(1f, cold.StrengthFor(0f), 1e-4f);

        var rain = new WeatherExposureRule { measure = WeatherMeasure.Rain, threshold = 0.2f, customFullStrength = true, fullStrengthAt = 0.6f };
        Assert.IsTrue(rain.Matches(new WeatherState { Rain = 0.6f }, out float full));
        Assert.AreEqual(1f, full, 1e-4f);
    }

    [Test]
    public void BodyHeatFollowsTheAirTemperature()
    {
        var go = new GameObject("weather effects test");
        try
        {
            PlayerWeatherEffects fx = go.AddComponent<PlayerWeatherEffects>();
            Assert.AreEqual(50f, fx.BodyHeatFor(new WeatherState { Temperature = fx.comfortTemperature }, false, false), 1e-3f);
            Assert.Less(fx.BodyHeatFor(new WeatherState { Temperature = -10f }, false, false), 10f, "freezing air = extremely cold");
            Assert.Greater(fx.BodyHeatFor(new WeatherState { Temperature = 45f }, false, false), 90f, "scorching air = extremely hot");
            float open = fx.BodyHeatFor(new WeatherState { Temperature = 0f, WindSpeed = 15f }, false, false);
            float roof = fx.BodyHeatFor(new WeatherState { Temperature = 0f, WindSpeed = 15f }, true, false);
            Assert.Greater(roof, open, "shelter keeps the wind chill out");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
#endif
