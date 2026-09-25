using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for <see cref="TerrainGenerator"/>. The default inspector dumps ~60 fields in one
/// flat list, which makes it hard to tell which settings belong together (or which sub-settings only do
/// something when their master toggle is on) - especially for Climate and Erosion, where several fields
/// on this component interact with fields on each <see cref="Biome"/> asset. This editor groups
/// everything into collapsible, labeled sections, greys out settings that currently have no effect,
/// explains how Climate and Erosion actually work (and how they influence each other) in plain language,
/// and flags known-bad parameter combinations before they produce visible seams/artifacts.
/// </summary>
[CustomEditor(typeof(TerrainGenerator))]
public partial class TerrainGeneratorEditor : Editor
{
    private static GUIStyle _sectionFoldoutStyle;
    private static GUIStyle SectionFoldoutStyle
    {
        get
        {
            if (_sectionFoldoutStyle == null)
            {
                _sectionFoldoutStyle = new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold
                };
            }
            return _sectionFoldoutStyle;
        }
    }

    // Foldout open/closed state. Kept as simple instance fields (reset when the object is reselected) -
    // this is a tuning tool, not something that needs to remember layout across sessions.
    private bool showTerrainConfig = true;
    private bool showNoise = true;
    private bool showHeightAndTexture = false;
    private bool showTextureVariations = false;
    private bool showVoronoi = true;
    private bool showLandforms = true;
    private bool showVolcanoes = true;
    private bool showNaturalPlacement = true;
    private bool showClimate = true;
    private bool showBiomeClimateSummary = false;
    private bool showErosionThermal = true;
    private bool showErosionHydraulic = true;
    private bool showWater = true;
    private bool showErosionDebug = true;
    private bool showOther = false;
    private bool showBiomes = true;
    private bool showObjects = false;

    // Cached properties, found once in OnEnable rather than FindProperty'd every OnInspectorGUI call.
    private SerializedProperty terrainSizeProp;
    private SerializedProperty octavesProp, lacunarityProp;
    private SerializedProperty minHeightProp, maxHeightProp, terrainTextureBasedOnVoronoiPointsProp;
    private SerializedProperty enableTextureVariationsProp;
    private SerializedProperty enableUVRotationProp;
    private SerializedProperty enableUVNoiseProp, uvNoiseStrengthProp, uvNoiseScaleProp;
    private SerializedProperty enableTextureScaleVariationProp, textureScaleVariationRangeProp;
    private SerializedProperty enableShaderEnhancementsProp, shaderUVRotationStrengthProp, shaderUVScaleVariationProp, shaderTextureBlendSharpnessProp;
    private SerializedProperty numVoronoiPointsProp, voronoiSeedProp, voronoiScaleProp, useWeightedBiomeProp;
    private SerializedProperty biomeClusterStrengthProp, biomeClusterRadiusMultiplierProp, biomeRepeatPenaltyProp;
    private SerializedProperty voronoiWarpStrengthProp, voronoiWarpScaleMultiplierProp, biomeBlendRangeProp, useBiomeBlendedTexturingProp;
    private SerializedProperty biomeBoundaryMaxSlopeDegreesProp;
    private SerializedProperty useNaturalClimatePlacementProp, climateScaleMultiplierProp;
    private SerializedProperty enableErosionProp, erosionPaddingProp, thermalIterationsProp, talusAngleProp, thermalErosionRateProp;
    private SerializedProperty hydraulicDropletDensityProp, dropletLifetimeProp, dropletInertiaProp, sedimentCapacityFactorProp, minSedimentCapacityProp;
    private SerializedProperty erodeSpeedProp, depositSpeedProp, evaporateSpeedProp, erosionGravityProp, erosionRadiusProp;
    private SerializedProperty visualizeErosionDebugProp, erosionDebugMinDeltaProp, erosionDebugMaxDeltaProp, erosionDebugStrideProp;
    private SerializedProperty erosionDebugGizmoSizeProp, erosionDebugHeightOffsetProp, erosionDebugMaxGizmosPerChunkProp;
    private SerializedProperty levelOfDetailProp;
    private SerializedProperty biomeDefinitionsProp;
    private SerializedProperty shouldSpawnObjectsProp, clusterBaseFrequencyProp, clusterAmplitudeProp;

    // Cached embedded sub-editors for the "edit a biome inline" feature in the climate/erosion summary
    // table - created lazily per biome, and explicitly destroyed in OnDisable (Editor instances are not
    // garbage-collected like plain objects; leaving them alive leaks native resources).
    private readonly Dictionary<Biome, Editor> _biomeEditorCache = new Dictionary<Biome, Editor>();
    private readonly HashSet<Biome> _expandedBiomeEditors = new HashSet<Biome>();

    private void OnEnable()
    {
        terrainSizeProp = serializedObject.FindProperty("terrainSize");
        octavesProp = serializedObject.FindProperty("octaves");
        lacunarityProp = serializedObject.FindProperty("lacunarity");

        minHeightProp = serializedObject.FindProperty("minHeight");
        maxHeightProp = serializedObject.FindProperty("maxHeight");
        terrainTextureBasedOnVoronoiPointsProp = serializedObject.FindProperty("terrainTextureBasedOnVoronoiPoints");

        enableTextureVariationsProp = serializedObject.FindProperty("enableTextureVariations");
        enableUVRotationProp = serializedObject.FindProperty("enableUVRotation");
        enableUVNoiseProp = serializedObject.FindProperty("enableUVNoise");
        uvNoiseStrengthProp = serializedObject.FindProperty("uvNoiseStrength");
        uvNoiseScaleProp = serializedObject.FindProperty("uvNoiseScale");
        enableTextureScaleVariationProp = serializedObject.FindProperty("enableTextureScaleVariation");
        textureScaleVariationRangeProp = serializedObject.FindProperty("textureScaleVariationRange");
        enableShaderEnhancementsProp = serializedObject.FindProperty("enableShaderEnhancements");
        shaderUVRotationStrengthProp = serializedObject.FindProperty("shaderUVRotationStrength");
        shaderUVScaleVariationProp = serializedObject.FindProperty("shaderUVScaleVariation");
        shaderTextureBlendSharpnessProp = serializedObject.FindProperty("shaderTextureBlendSharpness");

        numVoronoiPointsProp = serializedObject.FindProperty("NumVoronoiPoints");
        voronoiSeedProp = serializedObject.FindProperty("VoronoiSeed");
        voronoiScaleProp = serializedObject.FindProperty("VoronoiScale");
        useWeightedBiomeProp = serializedObject.FindProperty("useWeightedBiome");

        biomeClusterStrengthProp = serializedObject.FindProperty("biomeClusterStrength");
        biomeClusterRadiusMultiplierProp = serializedObject.FindProperty("biomeClusterRadiusMultiplier");
        biomeRepeatPenaltyProp = serializedObject.FindProperty("biomeRepeatPenalty");
        voronoiWarpStrengthProp = serializedObject.FindProperty("voronoiWarpStrength");
        voronoiWarpScaleMultiplierProp = serializedObject.FindProperty("voronoiWarpScaleMultiplier");
        biomeBlendRangeProp = serializedObject.FindProperty("biomeBlendRange");
        useBiomeBlendedTexturingProp = serializedObject.FindProperty("useBiomeBlendedTexturing");
        biomeBoundaryMaxSlopeDegreesProp = serializedObject.FindProperty("biomeBoundaryMaxSlopeDegrees");

        useNaturalClimatePlacementProp = serializedObject.FindProperty("useNaturalClimatePlacement");
        climateScaleMultiplierProp = serializedObject.FindProperty("climateScaleMultiplier");

        enableErosionProp = serializedObject.FindProperty("enableErosion");
        erosionPaddingProp = serializedObject.FindProperty("erosionPadding");
        thermalIterationsProp = serializedObject.FindProperty("thermalIterations");
        talusAngleProp = serializedObject.FindProperty("talusAngle");
        thermalErosionRateProp = serializedObject.FindProperty("thermalErosionRate");

        hydraulicDropletDensityProp = serializedObject.FindProperty("hydraulicDropletDensity");
        dropletLifetimeProp = serializedObject.FindProperty("dropletLifetime");
        dropletInertiaProp = serializedObject.FindProperty("dropletInertia");
        sedimentCapacityFactorProp = serializedObject.FindProperty("sedimentCapacityFactor");
        minSedimentCapacityProp = serializedObject.FindProperty("minSedimentCapacity");
        erodeSpeedProp = serializedObject.FindProperty("erodeSpeed");
        depositSpeedProp = serializedObject.FindProperty("depositSpeed");
        evaporateSpeedProp = serializedObject.FindProperty("evaporateSpeed");
        erosionGravityProp = serializedObject.FindProperty("erosionGravity");
        erosionRadiusProp = serializedObject.FindProperty("erosionRadius");


        visualizeErosionDebugProp = serializedObject.FindProperty("visualizeErosionDebug");
        erosionDebugMinDeltaProp = serializedObject.FindProperty("erosionDebugMinDelta");
        erosionDebugMaxDeltaProp = serializedObject.FindProperty("erosionDebugMaxDelta");
        erosionDebugStrideProp = serializedObject.FindProperty("erosionDebugStride");
        erosionDebugGizmoSizeProp = serializedObject.FindProperty("erosionDebugGizmoSize");
        erosionDebugHeightOffsetProp = serializedObject.FindProperty("erosionDebugHeightOffset");
        erosionDebugMaxGizmosPerChunkProp = serializedObject.FindProperty("erosionDebugMaxGizmosPerChunk");

        levelOfDetailProp = serializedObject.FindProperty("levelOfDetail");
        biomeDefinitionsProp = serializedObject.FindProperty("biomeDefinitions");

        shouldSpawnObjectsProp = serializedObject.FindProperty("shouldSpawnObjects");
        clusterBaseFrequencyProp = serializedObject.FindProperty("clusterBaseFrequency");
        clusterAmplitudeProp = serializedObject.FindProperty("clusterAmplitude");
    }

    private void OnDisable()
    {
        foreach (Editor cachedEditor in _biomeEditorCache.Values)
        {
            if (cachedEditor != null)
                DestroyImmediate(cachedEditor);
        }
        _biomeEditorCache.Clear();
        _expandedBiomeEditors.Clear();
        ClearPreview();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        TerrainGenerator generator = (TerrainGenerator)target;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Terrain Generator", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Procedural terrain: Voronoi biome placement (optionally climate-driven), fractal-noise heights, " +
            "and thermal + hydraulic erosion. Sections below are grouped by what they configure - collapse " +
            "the ones you're not tuning right now.",
            EditorStyles.wordWrappedMiniLabel);

        DrawValidationWarnings(generator);
        DrawCopySettingsBar(generator);

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Clear Voronoi / Biome Cache", "Voronoi points and their assigned biomes are cached per chunk coordinate for the lifetime of the process. If you change VoronoiSeed, VoronoiScale, biome list, or any Natural Placement/Climate setting while already in Play Mode (or with 'Reload Domain' disabled), old chunks keep their stale biome layout until this cache is cleared. TerrainGenerator.Awake() already does this automatically at the start of Play - use this button to force it on demand, e.g. after tweaking values mid-Play.")))
        {
            VoronoiBiomeGenerator.ClearCache();
            WaterGenerator.ClearCaches();
            Debug.Log("[TerrainGenerator] Voronoi/biome and water feature caches cleared.");
        }
        EditorGUILayout.EndHorizontal();

        showPreview = Section("World Preview", showPreview, () => DrawPreviewSection(generator));

        showTerrainConfig = Section("Terrain Configuration", showTerrainConfig, () => DrawTerrainConfigSection(generator));

        showNoise = Section("Noise Configuration", showNoise, DrawNoiseSection);

        showLandforms = Section("Terrain Shape (Landforms)", showLandforms, () => DrawLandformSection(generator));

        showVolcanoes = Section("Volcanoes & Calderas", showVolcanoes, () => DrawVolcanoSection(generator));

        showHeightAndTexture = Section("Height Range & Texture", showHeightAndTexture, () => DrawHeightAndTextureSection(generator));

        showTextureVariations = Section("Texture Variations", showTextureVariations, () => DrawTextureVariationsSection(generator));

        showVoronoi = Section("Voronoi / Biome Grid", showVoronoi, DrawVoronoiSection);

        showNaturalPlacement = Section("Natural Biome Placement", showNaturalPlacement, () => DrawNaturalPlacementSection(generator));

        showClimate = Section("Climate (Temperature & Moisture)", showClimate, () => DrawClimateSection(generator));

        showErosionThermal = Section("Erosion - Thermal (Slopes/Talus)", showErosionThermal, DrawErosionThermalSection);

        showErosionHydraulic = Section("Erosion - Hydraulic (Water/Droplets)", showErosionHydraulic, () => DrawErosionHydraulicSection(generator));

        showWater = Section("Water (Oceans, Lakes, Ponds, Rivers)", showWater, () => DrawWaterSection(generator));

        showErosionDebug = Section("Erosion Debug Visualization", showErosionDebug, DrawErosionDebugSection);

        showOther = Section("Other Configuration", showOther, DrawOtherSection);

        showBiomes = Section("Biomes", showBiomes, DrawBiomesSection);

        showObjects = Section("Objects", showObjects, DrawObjectsSection);

        EditorGUILayout.Space(6);
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Draws a labeled foldout in a boxed section and, when expanded, runs <paramref name="drawContents"/>
    /// indented inside it. Returns the (possibly toggled) expanded state for the caller to store.
    /// </summary>
    private static bool Section(string title, bool expanded, System.Action drawContents)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        expanded = EditorGUILayout.Foldout(expanded, title, true, SectionFoldoutStyle);
        if (expanded)
        {
            EditorGUI.indentLevel++;
            drawContents();
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();
        return expanded;
    }

    private void DrawProp(string field, string label)
    {
        Field(serializedObject.FindProperty(field), label);
    }

    private static List<string> BiomesWithPlacement(TerrainGenerator generator, BiomePlacement placement)
    {
        var names = new List<string>();
        if (generator.BiomeDefinitions == null)
            return names;
        foreach (BiomeInstance instance in generator.BiomeDefinitions)
        {
            if (instance != null && instance.BiomePrefab != null && instance.BiomePrefab.placement == placement)
                names.Add(instance.BiomePrefab.name);
        }
        return names;
    }
}
