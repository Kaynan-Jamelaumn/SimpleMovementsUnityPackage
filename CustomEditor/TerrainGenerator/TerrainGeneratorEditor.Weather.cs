using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: the Weather section - adding the Weather System, and each biome's weather.
public partial class TerrainGeneratorEditor
{
    private bool showWeatherSection = true;
    private readonly Dictionary<Biome, float[]> biomeWeatherEstimates = new Dictionary<Biome, float[]>();
    private readonly HashSet<Biome> openBiomeWeather = new HashSet<Biome>();
    private readonly Dictionary<Biome, SerializedObject> biomeWeatherObjects = new Dictionary<Biome, SerializedObject>();

    private void DrawWeatherSection(TerrainGenerator generator)
    {
        EditorGUILayout.HelpBox(
            "Weather follows the biomes: rain, thunderstorms, hail, snow, blizzards, sandstorms, heat waves, wind, fog, mist and " +
            "tornadoes. Each biome's Ideal Temperature and Moisture (Climate section of the Biome asset) and the altitude decide " +
            "what it gets - deserts sandstorms and heat waves, cold biomes snow and blizzards, wet ones rain and fog - and its " +
            "Weather settings make any kind more or less likely (presets below). Weather systems drift with the Prevailing Wind. " +
            "The Weather System component does the rest (effects, sounds, snow and wet ground on the terrain).",
            MessageType.Info);

        WeatherSystem weather = UnityEngine.Object.FindFirstObjectByType<WeatherSystem>();
        EditorGUILayout.BeginHorizontal();
        if (weather == null)
        {
            if (GUILayout.Button(new GUIContent("Add Weather System", "Adds a Weather System to this GameObject (undoable). It follows Endless Terrain's viewer.")))
            {
                weather = Undo.AddComponent<WeatherSystem>(generator.gameObject);
                Selection.activeObject = weather;
            }
        }
        else
        {
            if (GUILayout.Button(new GUIContent("Select Weather System", "Shows the Weather System's settings, the live weather and a forecast.")))
                Selection.activeObject = weather;
            if (Application.isPlaying)
                GUILayout.Label($"Now: {WeatherSystemEditor.Nice(weather.CurrentType)}, {weather.Current.Temperature:0} °C", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndHorizontal();

        BiomeInstance[] biomes = generator.BiomeDefinitions;
        if (biomes == null || biomes.Length == 0)
            return;

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(new GUIContent("Biome Weather", "Each biome's Weather settings: 12 multipliers (0 = never, 1 = as the climate gives it, 3 = much more often) for rain, snow, storms... A preset sets all 12 for a kind of biome. The estimate on the right is the biome's typical weather over a day."), EditorStyles.miniBoldLabel);
        if (GUILayout.Button(new GUIContent("Estimate Weather Per Biome", "Works out how often each kind of weather happens in each biome with its current settings (over a day of weather at a dozen places)."), EditorStyles.miniButton, GUILayout.Width(170f)))
        {
            biomeWeatherEstimates.Clear();
            foreach (BiomeInstance instance in biomes)
            {
                Biome biome = instance != null ? instance.BiomePrefab : null;
                if (biome != null && biome.placement != BiomePlacement.Ocean && !biomeWeatherEstimates.ContainsKey(biome))
                    biomeWeatherEstimates[biome] = WeatherModel.TypicalWeather(biome, generator.TerrainShapeMode, generator.VoronoiSeed);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Open a biome to see and change its multipliers. A preset sets all of them at once; the estimate updates to show what it does.", EditorStyles.wordWrappedMiniLabel);

        var seen = new HashSet<Biome>();
        foreach (BiomeInstance instance in biomes)
        {
            Biome biome = instance != null ? instance.BiomePrefab : null;
            if (biome == null || biome.placement == BiomePlacement.Ocean || !seen.Add(biome))
                continue;

            EditorGUILayout.BeginHorizontal();
            bool open = openBiomeWeather.Contains(biome);
            bool nowOpen = EditorGUILayout.Foldout(open, new GUIContent(string.IsNullOrEmpty(biome.name) ? "(unnamed)" : biome.name,
                $"Ideal temperature {biome.idealTemperature:0.00} (about {Mathf.Lerp(WeatherModel.ColdestCelsius, WeatherModel.HottestCelsius, biome.idealTemperature):0} °C), moisture {biome.idealMoisture:0.00}. Click to show its weather multipliers."), true);
            if (nowOpen != open)
            {
                if (nowOpen) openBiomeWeather.Add(biome);
                else openBiomeWeather.Remove(biome);
            }

            string current = MatchingPreset(biome.weather);
            if (EditorGUILayout.DropdownButton(new GUIContent(current, "This biome's weather preset (Custom = changed by hand). Pick one to set all 12 multipliers (undoable)."), FocusType.Passive, GUILayout.Width(120f)))
            {
                var menu = new GenericMenu();
                foreach (BiomeWeatherPreset preset in (BiomeWeatherPreset[])Enum.GetValues(typeof(BiomeWeatherPreset)))
                {
                    BiomeWeatherPreset chosen = preset;
                    Biome target = biome;
                    TerrainShapeMode mode = generator.TerrainShapeMode;
                    int seed = generator.VoronoiSeed;
                    menu.AddItem(new GUIContent(PresetLabel(preset)), PresetLabel(preset) == current, () =>
                    {
                        target.ApplyWeatherPreset(chosen);
                        if (biomeWeatherObjects.TryGetValue(target, out SerializedObject so) && so != null)
                            so.Update();
                        // Show straight away what the preset does to this biome's weather.
                        biomeWeatherEstimates[target] = WeatherModel.TypicalWeather(target, mode, seed);
                        Repaint();
                    });
                }
                menu.ShowAsContext();
            }
            if (biomeWeatherEstimates.TryGetValue(biome, out float[] shares))
                GUILayout.Label(new GUIContent(Summary(shares), Summary(shares, 20)), EditorStyles.miniLabel);
            else
                GUILayout.Label(new GUIContent("-", "Press Estimate Weather Per Biome (or pick a preset) to see this biome's typical weather."), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (biomeWeatherEstimates.TryGetValue(biome, out float[] estimate))
            {
                string hint = ClimateHint(biome, estimate);
                if (hint != null)
                    EditorGUILayout.HelpBox(hint, MessageType.None);
            }

            if (nowOpen)
                DrawBiomeWeatherSettings(biome, generator);
        }
    }

    /// <summary>The biome's 12 weather multipliers, edited on the biome asset itself (undoable).</summary>
    private void DrawBiomeWeatherSettings(Biome biome, TerrainGenerator generator)
    {
        if (!biomeWeatherObjects.TryGetValue(biome, out SerializedObject so) || so == null || so.targetObject == null)
        {
            so = new SerializedObject(biome);
            biomeWeatherObjects[biome] = so;
        }
        so.Update();
        SerializedProperty weather = so.FindProperty("weather");
        if (weather == null)
            return;

        EditorGUI.indentLevel++;
        EditorGUI.BeginChangeCheck();
        SerializedProperty end = weather.GetEndProperty();
        SerializedProperty child = weather.Copy();
        bool enter = true;
        while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
        {
            enter = false;
            EditorGUILayout.PropertyField(child);
        }
        if (EditorGUI.EndChangeCheck())
        {
            so.ApplyModifiedProperties();
            biomeWeatherEstimates.Remove(biome);   // out of date now
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(2);
    }

    /// <summary>
    /// Why weather the biome asks for more of (multiplier above 1) still never happens: its climate rules it out.
    /// Null when there's nothing to say.
    /// </summary>
    private static string ClimateHint(Biome biome, float[] estimate)
    {
        BiomeWeather w = biome.weather;
        if (w == null)
            return null;
        var hints = new List<string>();
        Func<WeatherType, bool> Never = type => estimate[(int)type] < 0.005f;
        if (w.sandstorms > 1f && Never(WeatherType.Sandstorm))
            hints.Add("sandstorms need a drier biome (Ideal Moisture below about 0.45, the lower the more often) that isn't cold");
        if (w.heatWaves > 1f && Never(WeatherType.HeatWave))
            hints.Add("heat waves need a hot biome (Ideal Temperature above about 0.6)");
        if ((w.snow > 1f || w.blizzards > 1f) && Never(WeatherType.Snow) && Never(WeatherType.Blizzard))
            hints.Add("snow needs a cold biome (Ideal Temperature below about 0.4, or high up)");
        if ((w.fog > 1f || w.mist > 1f) && Never(WeatherType.Fog) && Never(WeatherType.Mist))
            hints.Add("fog and mist need a humid biome (raise Ideal Moisture) with calm air");
        if ((w.rain > 1f || w.thunderstorms > 1f) && Never(WeatherType.Rain) && Never(WeatherType.Thunderstorm))
            hints.Add("rain needs moisture (raise Ideal Moisture or Precipitation) and warmth above freezing");
        if (hints.Count == 0)
            return null;
        return $"{biome.name}: " + string.Join("; ", hints) + ". Its climate: Ideal Temperature " +
            $"{biome.idealTemperature:0.00} (about {Mathf.Lerp(WeatherModel.ColdestCelsius, WeatherModel.HottestCelsius, biome.idealTemperature):0} °C), Ideal Moisture {biome.idealMoisture:0.00}.";
    }

    private static string PresetLabel(BiomeWeatherPreset preset)
    {
        return preset == BiomeWeatherPreset.FromClimate ? "From Climate (all 1)" : ObjectNames.NicifyVariableName(preset.ToString());
    }

    /// <summary>The name of the preset whose multipliers these are, or "Custom".</summary>
    private static string MatchingPreset(BiomeWeather weather)
    {
        if (weather == null)
            return PresetLabel(BiomeWeatherPreset.FromClimate);
        var probe = new BiomeWeather();
        foreach (BiomeWeatherPreset preset in (BiomeWeatherPreset[])Enum.GetValues(typeof(BiomeWeatherPreset)))
        {
            probe.ApplyPreset(preset);
            if (Same(probe, weather))
                return PresetLabel(preset);
        }
        return "Custom";
    }

    private static bool Same(BiomeWeather a, BiomeWeather b)
    {
        return Mathf.Approximately(a.precipitation, b.precipitation) && Mathf.Approximately(a.rain, b.rain)
            && Mathf.Approximately(a.thunderstorms, b.thunderstorms) && Mathf.Approximately(a.hail, b.hail)
            && Mathf.Approximately(a.snow, b.snow) && Mathf.Approximately(a.blizzards, b.blizzards)
            && Mathf.Approximately(a.sandstorms, b.sandstorms) && Mathf.Approximately(a.heatWaves, b.heatWaves)
            && Mathf.Approximately(a.wind, b.wind) && Mathf.Approximately(a.fog, b.fog)
            && Mathf.Approximately(a.mist, b.mist) && Mathf.Approximately(a.tornadoes, b.tornadoes);
    }

    /// <summary>The most common kinds of weather in a biome's estimate, as "Clear 55%, Rain 20%...".</summary>
    private static string Summary(float[] shares, int most = 4)
    {
        var order = new List<int>();
        for (int k = 0; k < shares.Length; k++)
            if (shares[k] >= 0.005f)
                order.Add(k);
        order.Sort((a, b) => shares[b].CompareTo(shares[a]));
        var parts = new List<string>();
        for (int i = 0; i < order.Count && i < most; i++)
            parts.Add($"{WeatherSystemEditor.Nice((WeatherType)order[i])} {shares[order[i]] * 100f:0}%");
        return string.Join(", ", parts);
    }
}
