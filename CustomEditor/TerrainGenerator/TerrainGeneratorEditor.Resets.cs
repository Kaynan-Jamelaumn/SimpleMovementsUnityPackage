using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: the Reset To Recommended / Reset To Default buttons shared by the sections, what each
// section recommends, and the performance auto-detection (see TerrainGeneratorEditor.cs).
//
// Reset To Default copies the fields from a freshly created TerrainGenerator, so it always matches the field
// initializers exactly. Reset To Recommended applies the values below: the tuned starting point, which leaves
// choices that belong to your world (seeds, materials, whether a big feature is on) alone.
public partial class TerrainGeneratorEditor
{
    // ------------------------------------------------------------------ section field lists and recommended values

    private static readonly string[] NoiseFields = { "octaves", "lacunarity" };
    private static readonly Dictionary<string, object> NoiseRecommended = new Dictionary<string, object>
    {
        { "octaves", 5 }, { "lacunarity", 2f },
    };

    private static readonly string[] VoronoiFields = { "NumVoronoiPoints", "VoronoiSeed", "VoronoiScale", "useWeightedBiome" };
    private static readonly Dictionary<string, object> VoronoiRecommended = new Dictionary<string, object>
    {
        { "NumVoronoiPoints", 8 }, { "VoronoiScale", 350f }, { "useWeightedBiome", true },
    };

    private static readonly string[] LandformFields = { "terrainShapeMode", "landformTransitionWidth", "mountainBeltStrength", "mountainBeltScaleMultiplier" };
    private static readonly Dictionary<string, object> LandformRecommended = new Dictionary<string, object>
    {
        { "terrainShapeMode", (int)TerrainShapeMode.PerBiome }, { "landformTransitionWidth", 0.35f },
        { "mountainBeltStrength", 0.5f }, { "mountainBeltScaleMultiplier", 6f },
    };

    private static readonly string[] TextureVariationFields =
    {
        "enableTextureVariations", "enableUVRotation", "enableUVNoise", "uvNoiseStrength", "uvNoiseScale", "enableTextureScaleVariation",
        "textureScaleVariationRange", "enableShaderEnhancements", "shaderUVRotationStrength", "shaderUVScaleVariation", "shaderTextureBlendSharpness",
    };
    private static readonly Dictionary<string, object> TextureVariationRecommended = new Dictionary<string, object>
    {
        { "enableTextureVariations", true }, { "enableUVRotation", true }, { "enableUVNoise", true }, { "uvNoiseStrength", 0.3f },
        { "uvNoiseScale", 0.1f }, { "enableTextureScaleVariation", true }, { "textureScaleVariationRange", 0.3f },
        { "enableShaderEnhancements", true }, { "shaderUVRotationStrength", 0.5f }, { "shaderUVScaleVariation", 1.2f },
        { "shaderTextureBlendSharpness", 1.5f },
    };

    private static readonly string[] MaterialFields =
    {
        "terrainShader", "customTerrainMaterial", "terrainTextureSize", "triplanarStrength", "triplanarSlopeStart", "triplanarSlopeEnd",
        "triplanarSharpness", "terrainSmoothness", "wetnessDarkening", "wetnessSmoothness",
    };
    private static readonly Dictionary<string, object> MaterialRecommended = new Dictionary<string, object>
    {
        { "terrainTextureSize", 0f }, { "triplanarStrength", 1f }, { "triplanarSlopeStart", 25f }, { "triplanarSlopeEnd", 45f },
        { "triplanarSharpness", 6f }, { "terrainSmoothness", 0.08f }, { "wetnessDarkening", 0.35f }, { "wetnessSmoothness", 0.55f },
    };

    private static readonly string[] ObjectFields = { "shouldSpawnObjects", "objectCliffAngle", "objectSpawnBudgetMs", "maxObjectsPerFrame", "fullObjectDistance", "farObjectParts" };
    private static readonly Dictionary<string, object> ObjectRecommended = new Dictionary<string, object>
    {
        { "objectCliffAngle", 45f }, { "objectSpawnBudgetMs", 2f }, { "maxObjectsPerFrame", 300 }, { "fullObjectDistance", 150f },
        { "farObjectParts", (int)(FarObjectParts.Colliders | FarObjectParts.Scripts | FarObjectParts.Animators | FarObjectParts.Audio) },
    };

    private static readonly string[] LodFields = { "levelOfDetail", "distanceLod", "lodFullDetailDistance", "lodDistanceStep", "lodMaxLevel", "lodSkirtDepth" };
    private static readonly Dictionary<string, object> LodRecommended = new Dictionary<string, object>
    {
        { "levelOfDetail", 2 }, { "distanceLod", true }, { "lodFullDetailDistance", 300f }, { "lodDistanceStep", 300f },
        { "lodMaxLevel", 4 }, { "lodSkirtDepth", 2f },
    };

    private static readonly string[] PerformanceFields =
    {
        "workerThreads", "mainThreadBudgetMs", "prepareMeshesOnWorkers", "poolObjects", "maxPooledObjects",
        "biomeTextureQuality", "biomeTextureResolution", "objectSpawnBudgetMs", "maxObjectsPerFrame",
    };
    private static readonly Dictionary<string, object> PerformanceRecommended = new Dictionary<string, object>
    {
        { "workerThreads", 0 }, { "mainThreadBudgetMs", 4f }, { "prepareMeshesOnWorkers", true }, { "poolObjects", true },
        { "maxPooledObjects", 4000 }, { "biomeTextureQuality", (int)BiomeTextureQuality.Automatic }, { "biomeTextureResolution", 0 },
        { "objectSpawnBudgetMs", 2f }, { "maxObjectsPerFrame", 300 },
    };

    private static readonly string[] BiomePlacementFields =
    {
        "biomeClusterStrength", "biomeClusterRadiusMultiplier", "biomeRepeatPenalty", "voronoiWarpStrength", "voronoiWarpScaleMultiplier",
        "biomeBlendRange", "useBiomeBlendedTexturing", "splatTexturesPerPixel", "biomeBoundaryMaxSlopeDegrees", "orderIndependentBiomeLayout",
    };

    private static readonly string[] ClimateFields =
    {
        "useNaturalClimatePlacement", "climateScaleMultiplier", "terrainAwareClimate", "prevailingWindAngle", "rainShadowStrength",
        "altitudeCooling", "coastalMoisture",
    };

    private static readonly string[] ErosionFields =
    {
        "enableErosion", "erosionPadding", "seamlessErosion", "thermalIterations", "talusAngle", "thermalErosionRate", "hydraulicDropletDensity",
        "dropletLifetime", "dropletInertia", "sedimentCapacityFactor", "minSedimentCapacity", "erodeSpeed", "depositSpeed", "evaporateSpeed",
        "erosionGravity", "erosionRadius",
    };

    // ------------------------------------------------------------------ buttons

    // Set when a button changed settings (they apply their own changes), so the World Preview can update.
    private bool settingsChangedByButton;

    /// <summary>A row with Reset To Recommended (when given) and Reset To Default, plus an optional first button.</summary>
    private void ResetButtons(string section, Dictionary<string, object> recommended, string[] defaultFields, string recommendedNote = null,
        string extraLabel = null, string extraTip = null, Action extra = null)
    {
        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        if (extra != null && GUILayout.Button(new GUIContent(extraLabel, extraTip)))
            extra();
        if (recommended != null && GUILayout.Button(new GUIContent("Reset To Recommended",
                $"Sets the {section} settings to their tuned starting values: {Describe(recommended)}." + (recommendedNote != null ? " " + recommendedNote : ""))))
            ApplyValues(recommended);
        if (GUILayout.Button(new GUIContent("Reset To Default",
                $"Restores every {section} setting to its factory default (the value a new Terrain Generator starts with): {string.Join(", ", Nicify(defaultFields))}.")))
            ResetToDefault(defaultFields);
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>Sets the given fields (bools, ints, floats, enums by value) and applies them with undo.</summary>
    private void ApplyValues(Dictionary<string, object> values)
    {
        foreach (KeyValuePair<string, object> entry in values)
        {
            SerializedProperty property = serializedObject.FindProperty(entry.Key);
            if (property == null)
                continue;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: property.boolValue = Convert.ToBoolean(entry.Value); break;
                case SerializedPropertyType.Integer: property.intValue = Convert.ToInt32(entry.Value); break;
                case SerializedPropertyType.Enum: property.intValue = Convert.ToInt32(entry.Value); break;
                case SerializedPropertyType.Float: property.floatValue = Convert.ToSingle(entry.Value); break;
            }
        }
        serializedObject.ApplyModifiedProperties();
        settingsChangedByButton = true;
    }

    /// <summary>Copies the given fields from a freshly created Terrain Generator (its field initializers), with undo.</summary>
    private void ResetToDefault(string[] fields)
    {
        // The component is added to an inactive object, so it never wakes up: in Play mode an active one would run
        // Awake (clearing the running world's biome and water caches) and, when destroyed, free the shared textures.
        GameObject temporary = EditorUtility.CreateGameObjectWithHideFlags("Terrain Generator Defaults", HideFlags.HideAndDontSave);
        temporary.SetActive(false);
        try
        {
            var defaults = new SerializedObject(temporary.AddComponent<TerrainGenerator>());
            foreach (string field in fields)
            {
                SerializedProperty source = defaults.FindProperty(field);
                if (source != null)
                    serializedObject.CopyFromSerializedProperty(source);
            }
            serializedObject.ApplyModifiedProperties();
            settingsChangedByButton = true;
        }
        finally
        {
            DestroyImmediate(temporary);
        }
    }

    private static string Describe(Dictionary<string, object> values)
    {
        var parts = new List<string>();
        foreach (KeyValuePair<string, object> entry in values)
        {
            object value = entry.Value;
            string text = value is bool b ? (b ? "on" : "off") : value is float f ? f.ToString("0.###") : value.ToString();
            parts.Add($"{ObjectNames.NicifyVariableName(entry.Key)} {text}");
        }
        return string.Join(", ", parts);
    }

    private static IEnumerable<string> Nicify(string[] fields)
    {
        foreach (string field in fields)
            yield return ObjectNames.NicifyVariableName(field);
    }

    // ------------------------------------------------------------------ performance auto-detection

    /// <summary>Performance settings suited to this computer (the one the editor runs on).</summary>
    private void AutoDetectPerformance()
    {
        int cores = SystemInfo.processorCount;
        int memory = SystemInfo.systemMemorySize;       // MB
        int video = SystemInfo.graphicsMemorySize;       // MB
        bool small = cores <= 4;
        var values = new Dictionary<string, object>
        {
            { "workerThreads", 0 },
            { "mainThreadBudgetMs", small ? 3f : 4f },
            { "objectSpawnBudgetMs", small ? 1.5f : 2.5f },
            { "maxObjectsPerFrame", small ? 200 : 400 },
            { "prepareMeshesOnWorkers", true },
            { "poolObjects", true },
            { "maxPooledObjects", memory >= 16000 ? 8000 : memory >= 8000 ? 4000 : 1500 },
            { "biomeTextureQuality", (int)(video > 0 && video < 3000 ? BiomeTextureQuality.Compressed : BiomeTextureQuality.Automatic) },
            { "biomeTextureResolution", video > 0 && video < 2000 ? 512 : 0 },
        };
        ApplyValues(values);

        var report = new StringBuilder();
        report.AppendLine($"This computer: {cores} CPU threads, {memory / 1024f:0.#} GB memory, {video / 1024f:0.#} GB video memory.");
        report.AppendLine();
        report.AppendLine($"Worker Threads: automatic ({TerrainWorkerPool.DefaultThreadCount} here).");
        report.AppendLine($"Main Thread Budget {values["mainThreadBudgetMs"]} ms, Spawn Budget {values["objectSpawnBudgetMs"]} ms, {values["maxObjectsPerFrame"]} objects per frame at most.");
        report.AppendLine($"Pooling on, up to {values["maxPooledObjects"]} objects.");
        report.AppendLine($"Biome textures: {(BiomeTextureQuality)(int)values["biomeTextureQuality"]}, resolution {((int)values["biomeTextureResolution"] > 0 ? values["biomeTextureResolution"].ToString() : "automatic")}.");
        report.AppendLine();
        report.Append("These suit the computer the editor runs on. If your players' machines are weaker, lower the budgets and the pool size.");
        EditorUtility.DisplayDialog("Performance settings for this computer", report.ToString(), "OK");
    }
}
