using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for the <see cref="WeatherSystem"/>: its settings in sections with Reset To Recommended buttons, the
/// live weather in Play mode with buttons to force any kind of weather, a forecast for the next hour at the viewer
/// and a weather map of the area around it.
/// </summary>
[CustomEditor(typeof(WeatherSystem))]
public class WeatherSystemEditor : Editor
{
    private static readonly string[] SetupFields = { "terrainGenerator", "viewer", "sun" };
    private static readonly string[] WeatherFields =
    {
        "systemSize", "driftSpeed", "weatherTimeScale", "precipitation", "windiness", "biomeInfluence", "climateSampleRadius",
        "transitionSeconds", "allowTornadoes", "startTime", "timeOfDay",
    };
    private static readonly Dictionary<string, object> WeatherRecommended = new Dictionary<string, object>
    {
        { "systemSize", 2500f }, { "driftSpeed", 4f }, { "weatherTimeScale", 1f }, { "precipitation", 1f }, { "windiness", 1f },
        { "biomeInfluence", 0.75f }, { "climateSampleRadius", 60f }, { "transitionSeconds", 12f }, { "allowTornadoes", true },
        { "startTime", 0f }, { "timeOfDay", -1f },
    };
    private static readonly string[] EffectFields =
    {
        "effectRadius", "maxParticles", "fogEffects", "maxFogDensity", "lightingEffects", "lightningBolts", "maxLightningPerMinute",
        "windZone", "groundEffects", "groundEffectRadius", "snowCaps", "tornadoForce",
    };
    private static readonly Dictionary<string, object> EffectRecommended = new Dictionary<string, object>
    {
        { "effectRadius", 35f }, { "maxParticles", 8000 }, { "fogEffects", true }, { "maxFogDensity", 0.03f }, { "lightingEffects", true },
        { "lightningBolts", true }, { "maxLightningPerMinute", 8f }, { "windZone", true }, { "groundEffects", true }, { "groundEffectRadius", 400f }, { "snowCaps", true },
        { "tornadoForce", 40f },
    };
    private static readonly string[] MaterialFields = { "rainMaterial", "snowMaterial", "dustMaterial", "lightningMaterial" };
    private static readonly string[] AudioFields = { "rainLoop", "windLoop", "hailLoop", "sandstormLoop", "tornadoLoop", "thunderClips", "volume" };

    private bool showSetup = true, showWeather = true, showEffects = true, showMaterials, showAudio, showLive = true, showForecast = true;

    // Weather map: the climate per pixel (it doesn't change with time), so moving the time slider only re-evaluates the weather.
    private const int MapResolution = 72;
    private Texture2D mapTexture;
    private WeatherClimate[] mapClimate;
    private Vector3 mapCenter;
    private float mapSize = 6000f;
    private float mapMinutes;
    private string mapStatus;

    /// <summary>A colour per kind of weather (forecast, map and legend).</summary>
    public static Color TypeColor(WeatherType type)
    {
        switch (type)
        {
            case WeatherType.Clear: return new Color(0.55f, 0.76f, 0.96f);
            case WeatherType.Cloudy: return new Color(0.7f, 0.72f, 0.76f);
            case WeatherType.Windy: return new Color(0.72f, 0.86f, 0.66f);
            case WeatherType.Mist: return new Color(0.86f, 0.9f, 0.9f);
            case WeatherType.Fog: return new Color(0.55f, 0.56f, 0.58f);
            case WeatherType.Rain: return new Color(0.22f, 0.42f, 0.86f);
            case WeatherType.Thunderstorm: return new Color(0.36f, 0.2f, 0.62f);
            case WeatherType.Hail: return new Color(0.55f, 0.92f, 1f);
            case WeatherType.Snow: return Color.white;
            case WeatherType.Blizzard: return new Color(0.8f, 0.92f, 1f);
            case WeatherType.Sandstorm: return new Color(0.86f, 0.66f, 0.36f);
            case WeatherType.HeatWave: return new Color(0.96f, 0.42f, 0.2f);
            case WeatherType.Tornado: return new Color(0.25f, 0.06f, 0.06f);
            default: return Color.gray;
        }
    }

    private void OnDisable()
    {
        if (mapTexture != null)
            DestroyImmediate(mapTexture);
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var weather = (WeatherSystem)target;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Weather System", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Rain, thunderstorms, hail, snow, blizzards, sandstorms, heat waves, wind, fog, mist and tornadoes, following the " +
            "biomes: each biome's Ideal Temperature and Moisture (and the altitude) decide what weather it gets, and its Weather " +
            "settings (in the Biome asset - right-click it for presets) make any kind more or less likely. Weather systems drift " +
            "across the world with the Terrain Generator's Prevailing Wind. Effects are made at runtime; no assets needed.",
            EditorStyles.wordWrappedMiniLabel);
        if (weather.Generator == null)
            EditorGUILayout.HelpBox("No Terrain Generator found - the weather needs one for its biomes and climate.", MessageType.Warning);

        showSetup = Section("Setup", showSetup, () => DrawFields(SetupFields, null, null));
        showWeather = Section("Weather", showWeather, () => DrawFields(WeatherFields, WeatherRecommended, "weather"));
        showEffects = Section("Effects", showEffects, () =>
        {
            DrawFields(EffectFields, EffectRecommended, "effects");
            EditorGUILayout.HelpBox(
                "Snow settling, wet ground and Snow Caps show on the package's terrain shader (SimpleMovements/Terrain). " +
                "Snow Caps need the Terrain Generator's Snow Line Height above 0. If your game sets the sun's light itself " +
                "(a day/night cycle), turn Lighting Effects off.", MessageType.None);
        });
        showMaterials = Section("Materials (optional)", showMaterials, () => DrawFields(MaterialFields, null, null));
        showAudio = Section("Audio (optional)", showAudio, () => DrawFields(AudioFields, null, null));
        serializedObject.ApplyModifiedProperties();

        showLive = Section(Application.isPlaying ? "Weather Now" : "Weather Now (in Play mode)", showLive, () => DrawLive(weather));
        showForecast = Section("Forecast & Weather Map", showForecast, () => DrawForecast(weather));
    }

    private static bool Section(string title, bool expanded, Action draw)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        expanded = EditorGUILayout.Foldout(expanded, title, true, EditorStyles.foldoutHeader);
        if (expanded)
        {
            EditorGUI.indentLevel++;
            draw();
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();
        return expanded;
    }

    private void DrawFields(string[] fields, Dictionary<string, object> recommended, string what)
    {
        foreach (string field in fields)
        {
            SerializedProperty property = serializedObject.FindProperty(field);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }
        if (recommended == null)
            return;
        if (GUILayout.Button(new GUIContent("Reset To Recommended", "Sets the " + what + " settings above to their recommended values (undoable).")))
        {
            foreach (KeyValuePair<string, object> entry in recommended)
            {
                SerializedProperty property = serializedObject.FindProperty(entry.Key);
                if (property == null)
                    continue;
                if (entry.Value is float f) property.floatValue = f;
                else if (entry.Value is int i) property.intValue = i;
                else if (entry.Value is bool b) property.boolValue = b;
            }
        }
    }

    // ------------------------------------------------------------------ live

    private void DrawLive(WeatherSystem weather)
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.LabelField("Enter Play mode to see the weather at the viewer and to force weather for testing. The forecast below works in the editor too.", EditorStyles.wordWrappedMiniLabel);
            return;
        }

        WeatherState now = weather.Current;
        EditorGUILayout.LabelField(new GUIContent($"{Nice(weather.CurrentType)}{(weather.IsForced ? " (forced)" : "")}", "The weather that stands out at the viewer."), EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{now.Temperature:0} °C, wind {now.WindSpeed:0} units/s toward {Compass(now.WindDirection)}, humidity {now.Humidity * 100f:0}%, visibility about {now.Visibility:0} units", EditorStyles.wordWrappedMiniLabel);
        Bar("Clouds", now.Clouds);
        Bar("Rain", now.Rain);
        Bar("Thunderstorm", now.Thunder);
        Bar("Hail", now.Hail);
        Bar("Snow", now.Snow);
        Bar("Sand / dust", now.Dust);
        Bar("Fog", now.Fog);
        Bar("Mist", now.Mist);
        Bar("Heat wave", now.Heat);
        Bar("Tornado", now.Tornado);
        Bar("Ground wetness", weather.GroundWetness);
        Bar("Settled snow", weather.SnowCover);
        if (weather.TornadoPosition.HasValue && weather.Viewer != null)
            EditorGUILayout.LabelField($"Tornado {Vector3.Distance(weather.Viewer.position, weather.TornadoPosition.Value):0} units away", EditorStyles.miniBoldLabel);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(new GUIContent("Force Weather", "Changes the weather to one kind (smoothly) until you press Natural Weather - for testing effects and sounds. Game code: WeatherSystem.ForceWeather."), EditorStyles.miniBoldLabel);
        var types = (WeatherType[])Enum.GetValues(typeof(WeatherType));
        for (int i = 0; i < types.Length; i += 4)
        {
            EditorGUILayout.BeginHorizontal();
            for (int k = i; k < Mathf.Min(i + 4, types.Length); k++)
                if (GUILayout.Button(Nice(types[k]), EditorStyles.miniButton))
                    weather.ForceWeather(types[k]);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!weather.IsForced))
            if (GUILayout.Button("Natural Weather"))
                weather.ClearForcedWeather();
        if (GUILayout.Button(new GUIContent("Skip 10 Minutes", "Moves weather time ahead - the weather systems drift on.")))
            weather.WeatherTime += 600;
        EditorGUILayout.EndHorizontal();
    }

    private static void Bar(string label, float value)
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 14f);
        rect = EditorGUI.PrefixLabel(rect, new GUIContent(label));
        EditorGUI.ProgressBar(rect, Mathf.Clamp01(value), value > 0.005f ? $"{value * 100f:0}%" : "");
    }

    // ------------------------------------------------------------------ forecast and map

    private Vector3 ForecastPosition(WeatherSystem weather)
    {
        if (Application.isPlaying && weather.Viewer != null)
            return weather.Viewer.position;
        SceneView view = SceneView.lastActiveSceneView;
        return view != null ? view.pivot : weather.transform.position;
    }

    private void DrawForecast(WeatherSystem weather)
    {
        if (weather.Generator == null)
            return;
        Vector3 position = ForecastPosition(weather);
        EditorGUILayout.LabelField(Application.isPlaying
            ? "At the viewer, from now:"
            : $"At the Scene view's position ({position.x:0}, {position.z:0}), from Start Time:", EditorStyles.wordWrappedMiniLabel);

        // One box per 2 minutes over the next hour.
        const int Steps = 30;
        WeatherClimate climate = weather.SampleClimate(position);
        double start = Application.isPlaying ? weather.WeatherTime : serializedObject.FindProperty("startTime").floatValue;
        Rect strip = EditorGUILayout.GetControlRect(false, 22f);
        strip = EditorGUI.IndentedRect(strip);
        float width = strip.width / Steps;
        var seen = new HashSet<WeatherType>();
        for (int i = 0; i < Steps; i++)
        {
            WeatherState state = weather.Evaluate(position, start + i * 120.0, climate);
            seen.Add(state.Type);
            var box = new Rect(strip.x + i * width, strip.y, Mathf.Ceil(width), strip.height);
            EditorGUI.DrawRect(box, TypeColor(state.Type));
            GUI.Label(box, new GUIContent("", $"+{i * 2} min: {Nice(state.Type)}, {state.Temperature:0} °C, wind {state.WindSpeed:0}"));
        }
        Rect scale = EditorGUILayout.GetControlRect(false, 12f);
        scale = EditorGUI.IndentedRect(scale);
        GUI.Label(new Rect(scale.x, scale.y, 60f, scale.height), "now", EditorStyles.miniLabel);
        GUI.Label(new Rect(scale.xMax - 60f, scale.y, 60f, scale.height), "+1 hour", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight });
        Legend(seen);
        EditorGUILayout.LabelField($"Climate here: {Mathf.Lerp(WeatherModel.ColdestCelsius, WeatherModel.HottestCelsius, climate.Temperature):0} °C on average, moisture {climate.Moisture * 100f:0}%, {climate.Altitude:0} units above sea level.", EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Weather Map", EditorStyles.miniBoldLabel);
        mapSize = EditorGUILayout.Slider(new GUIContent("Map Size", "Width of the area shown (world units)."), mapSize, 1000f, 20000f);
        if (GUILayout.Button(new GUIContent("Draw Weather Map", "The weather over the area around the same position - which kind stands out at each spot.")))
            BuildMap(weather, position);
        if (mapTexture != null)
        {
            EditorGUI.BeginChangeCheck();
            mapMinutes = EditorGUILayout.Slider(new GUIContent("In (minutes)", "Shows the weather this many minutes later: watch the systems drift with the wind."), mapMinutes, 0f, 180f);
            if (EditorGUI.EndChangeCheck())
                PaintMap(weather);
            Rect rect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxWidth(360f));
            GUI.DrawTexture(rect, mapTexture, ScaleMode.ScaleToFit);
            // The map's centre (the viewer or Scene view position).
            EditorGUI.DrawRect(new Rect(rect.center.x - 2f, rect.center.y - 2f, 4f, 4f), Color.black);
            if (!string.IsNullOrEmpty(mapStatus))
                EditorGUILayout.LabelField(mapStatus, EditorStyles.wordWrappedMiniLabel);
        }
    }

    /// <summary>A row of colour swatches naming the given kinds of weather.</summary>
    public static void Legend(IEnumerable<WeatherType> types)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(EditorGUI.indentLevel * 15f);
        foreach (WeatherType type in types)
        {
            Rect swatch = GUILayoutUtility.GetRect(10f, 10f, GUILayout.Width(10f), GUILayout.Height(10f));
            swatch.y += 3f;
            EditorGUI.DrawRect(swatch, TypeColor(type));
            GUILayout.Label(Nice(type), EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void BuildMap(WeatherSystem weather, Vector3 center)
    {
        mapCenter = center;
        mapClimate = new WeatherClimate[MapResolution * MapResolution];
        float step = mapSize / MapResolution;
        try
        {
            for (int y = 0; y < MapResolution; y++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Weather Map", "Looking up the climate...", y / (float)MapResolution))
                {
                    mapClimate = null;
                    return;
                }
                for (int x = 0; x < MapResolution; x++)
                {
                    var position = new Vector3(center.x + (x + 0.5f - MapResolution * 0.5f) * step, 0f, center.z + (y + 0.5f - MapResolution * 0.5f) * step);
                    mapClimate[y * MapResolution + x] = weather.SampleClimate(position);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        PaintMap(weather);
    }

    private void PaintMap(WeatherSystem weather)
    {
        if (mapClimate == null)
            return;
        if (mapTexture == null)
            mapTexture = new Texture2D(MapResolution, MapResolution, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
        double start = Application.isPlaying ? weather.WeatherTime : serializedObject.FindProperty("startTime").floatValue;
        double time = start + mapMinutes * 60.0;
        float step = mapSize / MapResolution;
        var pixels = new Color[MapResolution * MapResolution];
        var counts = new Dictionary<WeatherType, int>();
        for (int y = 0; y < MapResolution; y++)
        {
            for (int x = 0; x < MapResolution; x++)
            {
                var position = new Vector3(mapCenter.x + (x + 0.5f - MapResolution * 0.5f) * step, 0f, mapCenter.z + (y + 0.5f - MapResolution * 0.5f) * step);
                WeatherType type = weather.Evaluate(position, time, mapClimate[y * MapResolution + x]).Type;
                pixels[y * MapResolution + x] = TypeColor(type);
                counts[type] = counts.TryGetValue(type, out int c) ? c + 1 : 1;
            }
        }
        mapTexture.SetPixels(pixels);
        mapTexture.Apply(false);
        var parts = new List<string>();
        foreach (KeyValuePair<WeatherType, int> entry in counts)
            parts.Add($"{Nice(entry.Key)} {100f * entry.Value / pixels.Length:0}%");
        mapStatus = $"{mapSize / 1000f:0.#} km across, north up: " + string.Join(", ", parts);
    }

    // ------------------------------------------------------------------ text

    public static string Nice(WeatherType type)
    {
        switch (type)
        {
            case WeatherType.HeatWave: return "Heat Wave";
            default: return type.ToString();
        }
    }

    private static string Compass(Vector2 direction)
    {
        if (direction.sqrMagnitude < 1e-6f)
            return "-";
        // 0 degrees = +X (east), 90 = +Z (north).
        float angle = Mathf.Repeat(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, 360f);
        string[] names = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
        return names[Mathf.RoundToInt(angle / 45f) % 8];
    }
}
