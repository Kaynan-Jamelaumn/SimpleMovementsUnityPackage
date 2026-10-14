using UnityEditor;
using UnityEngine;

/// <summary>
/// Menus (and Weather System inspector buttons) that add the day/night clock and the weather's effects on players.
/// </summary>
public static class WeatherSetupMenu
{
    [MenuItem(SMMenu.Root + "Weather/Add Day Night Cycle (open scene)", false, SMMenu.Weather + 0)]
    private static void AddDayNightCycle()
    {
        DayNightCycle existing = Object.FindAnyObjectByType<DayNightCycle>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorUtility.DisplayDialog("Day Night Cycle", "This scene already has a Day Night Cycle (selected).", "OK");
            return;
        }
        var go = new GameObject("Day Night Cycle");
        go.AddComponent<DayNightCycle>();
        Undo.RegisterCreatedObjectUndo(go, "Add Day Night Cycle");
        Selection.activeGameObject = go;
        EditorUtility.DisplayDialog("Day Night Cycle",
            "Created 'Day Night Cycle'. Put it in a scene that stays loaded (the City, or the Bootstrap scene in a network session); " +
            "it keeps itself across scene changes. Set Day Length Minutes, Sunrise and Sunset. It turns the sun, darkens the night, " +
            "tells the Weather System the hour, and is what 'Day only' / 'Night only' spawn rules read.", "OK");
    }

    [MenuItem(SMMenu.Root + "Weather/Add Player Weather Effects (to the Weather System)", false, SMMenu.Weather + 1)]
    private static void AddPlayerEffects()
    {
        WeatherSystem weather = Object.FindAnyObjectByType<WeatherSystem>();
        if (weather == null)
        {
            EditorUtility.DisplayDialog("Player Weather Effects", "Open the scene with the Weather System (the World scene) first.", "OK");
            return;
        }
        AddPlayerEffects(weather);
    }

    private static void AddPlayerEffects(WeatherSystem weather)
    {
        PlayerWeatherEffects effects = weather.GetComponent<PlayerWeatherEffects>();
        if (effects == null)
            effects = Undo.AddComponent<PlayerWeatherEffects>(weather.gameObject);
        Selection.activeGameObject = weather.gameObject;
        EditorGUIUtility.PingObject(effects);
    }

    private const string ConditionFolder = "Assets/SimpleMovements/Weather Conditions";

    /// <summary>
    /// Writes the default conditions (Soaked, Chilled, Frostbite, Heatstroke, Sandblasted, Windswept, Pelted by Hail) as
    /// Weather Condition assets you edit in the Inspector, and gives them to the Player Weather Effects of the open scene
    /// (its own copies of the defaults are removed so nothing applies twice).
    /// </summary>
    [MenuItem(SMMenu.Root + "Weather/Create Default Weather Condition Assets", false, SMMenu.Weather + 20)]
    private static void CreateConditionAssets()
    {
        if (!AssetDatabase.IsValidFolder("Assets/SimpleMovements"))
            AssetDatabase.CreateFolder("Assets", "SimpleMovements");
        if (!AssetDatabase.IsValidFolder(ConditionFolder))
            AssetDatabase.CreateFolder("Assets/SimpleMovements", "Weather Conditions");
        var created = new System.Collections.Generic.List<WeatherCondition>();
        int existing = 0;
        foreach (WeatherExposureRule rule in PlayerWeatherEffects.DefaultRules())
        {
            string path = $"{ConditionFolder}/{rule.label}.asset";
            WeatherCondition asset = AssetDatabase.LoadAssetAtPath<WeatherCondition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WeatherCondition>();
                asset.rule = rule.Clone();
                AssetDatabase.CreateAsset(asset, path);
            }
            else
                existing++;
            created.Add(asset);
        }
        AssetDatabase.SaveAssets();

        PlayerWeatherEffects effects = Object.FindAnyObjectByType<PlayerWeatherEffects>();
        string assigned = "";
        if (effects != null)
        {
            Undo.RecordObject(effects, "Use Weather Condition Assets");
            foreach (WeatherCondition c in created)
                if (!effects.conditions.Contains(c))
                    effects.conditions.Add(c);
            // The component's own copies of the same defaults would apply twice: removed.
            effects.rules.RemoveAll(r => r != null && created.Exists(c => c.rule.label == r.label));
            EditorUtility.SetDirty(effects);
            assigned = $"\n\nThey were added to the Player Weather Effects on '{effects.name}' (its own copies of the defaults were removed). Save the scene.";
        }
        Selection.activeObject = created.Count > 0 ? created[0] : null;
        EditorUtility.DisplayDialog("Weather Conditions",
            $"{created.Count - existing} condition asset(s) created in {ConditionFolder}" + (existing > 0 ? $" ({existing} already existed)." : ".") +
            " Select one to change when it happens and what it does: stats, resistances, character stats, damage, status bars, body heat, " +
            "an effect, events. Make new ones with Create ▸ SimpleMovements ▸ Weather ▸ Weather Condition." + assigned, "OK");
    }

    /// <summary>Weather System inspector: is there a Day Night Cycle and Player Weather Effects (with Add buttons)?</summary>
    public static void DrawCompanions(WeatherSystem weather)
    {
        bool hasEffects = weather.GetComponent<PlayerWeatherEffects>() != null;
        bool hasClock = Object.FindAnyObjectByType<DayNightCycle>() != null;
        if (hasEffects && hasClock)
            return;
        EditorGUILayout.Space();
        if (!hasEffects)
        {
            EditorGUILayout.HelpBox("The weather is only visual: add Player Weather Effects so players get Soaked, Chilled, " +
                                    "Heatstroke, Frostbite... from the weather where they stand.", MessageType.Info);
            if (GUILayout.Button("Add Player Weather Effects"))
                AddPlayerEffects(weather);
        }
        if (!hasClock)
        {
            EditorGUILayout.HelpBox("No Day Night Cycle in this scene (it may be in another scene, such as the City or Bootstrap). " +
                                    "Without one there is no night, and 'Day only' / 'Night only' spawn rules let everything through.",
                                    MessageType.None);
            if (GUILayout.Button("Add Day Night Cycle Here"))
                AddDayNightCycle();
        }
    }
}
