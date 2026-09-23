using System.Collections.Generic;
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
public class TerrainGeneratorEditor : Editor
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
    private bool showNaturalPlacement = true;
    private bool showClimate = true;
    private bool showBiomeClimateSummary = false;
    private bool showErosionThermal = true;
    private bool showErosionHydraulic = true;
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

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Clear Voronoi / Biome Cache", "Voronoi points and their assigned biomes are cached per chunk coordinate for the lifetime of the process. If you change VoronoiSeed, VoronoiScale, biome list, or any Natural Placement/Climate setting while already in Play Mode (or with 'Reload Domain' disabled), old chunks keep their stale biome layout until this cache is cleared. TerrainGenerator.Awake() already does this automatically at the start of Play - use this button to force it on demand, e.g. after tweaking values mid-Play.")))
        {
            VoronoiBiomeGenerator.ClearCache();
            Debug.Log("[TerrainGenerator] Voronoi/biome cache cleared.");
        }
        EditorGUILayout.EndHorizontal();

        showTerrainConfig = Section("Terrain Configuration", showTerrainConfig, () =>
        {
            EditorGUILayout.PropertyField(terrainSizeProp, new GUIContent("Terrain Size"));
            EditorGUILayout.LabelField($"Resulting chunk size: {generator.ChunkSize} x {generator.ChunkSize} cells", EditorStyles.miniLabel);
        });

        showNoise = Section("Noise Configuration", showNoise, () =>
        {
            EditorGUILayout.PropertyField(octavesProp, new GUIContent("Octaves"));
            EditorGUILayout.PropertyField(lacunarityProp, new GUIContent("Lacunarity"));
            EditorGUILayout.HelpBox(
                "Each biome also defines its own amplitude/frequency/persistence (on the Biome asset) - " +
                "these two fields only control how many fractal layers are combined (Octaves) and how much " +
                "each successive layer's frequency grows (Lacunarity), shared by every biome.",
                MessageType.None);
        });

        showHeightAndTexture = Section("Height Range & Texture", showHeightAndTexture, () =>
        {
            EditorGUILayout.PropertyField(terrainTextureBasedOnVoronoiPointsProp, new GUIContent("Texture Based On Voronoi Points"));
            using (new EditorGUI.DisabledScope(generator.TerrainTextureBasedOnVoronoiPoints))
            {
                EditorGUILayout.PropertyField(minHeightProp, new GUIContent("Min Height (tracked)"));
                EditorGUILayout.PropertyField(maxHeightProp, new GUIContent("Max Height (tracked)"));
            }
            EditorGUILayout.HelpBox(
                generator.TerrainTextureBasedOnVoronoiPoints
                    ? "Min/Max Height are unused while textures are based on Voronoi points - texturing uses each biome's own height range instead."
                    : "Min/Max Height are updated automatically from every generated cell (used to normalize non-Voronoi texturing) and only ever grow - they are not reset between Play sessions.",
                MessageType.None);
        });

        showTextureVariations = Section("Texture Variations", showTextureVariations, () =>
        {
            EditorGUILayout.PropertyField(enableTextureVariationsProp, new GUIContent("Enable Texture Variations (master)"));
            using (new EditorGUI.DisabledScope(!generator.EnableTextureVariations))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(enableUVRotationProp, new GUIContent("UV Rotation"));
                EditorGUILayout.PropertyField(enableUVNoiseProp, new GUIContent("UV Noise Offset"));
                using (new EditorGUI.DisabledScope(!enableUVNoiseProp.boolValue))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(uvNoiseStrengthProp, new GUIContent("Noise Strength"));
                    EditorGUILayout.PropertyField(uvNoiseScaleProp, new GUIContent("Noise Scale"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(enableTextureScaleVariationProp, new GUIContent("Texture Scale Variation"));
                using (new EditorGUI.DisabledScope(!enableTextureScaleVariationProp.boolValue))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(textureScaleVariationRangeProp, new GUIContent("Scale Variation Range"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(enableShaderEnhancementsProp, new GUIContent("Shader-Based Enhancements"));
                using (new EditorGUI.DisabledScope(!enableShaderEnhancementsProp.boolValue))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(shaderUVRotationStrengthProp, new GUIContent("Shader UV Rotation Strength"));
                    EditorGUILayout.PropertyField(shaderUVScaleVariationProp, new GUIContent("Shader UV Scale Variation"));
                    EditorGUILayout.PropertyField(shaderTextureBlendSharpnessProp, new GUIContent("Shader Blend Sharpness"));
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
            }
            if (!generator.EnableTextureVariations)
            {
                EditorGUILayout.HelpBox(
                    "Master toggle is OFF: textures tile with zero variation (a plain linear UV map) and will " +
                    "read as an obviously repeating pattern. Strongly recommended ON.",
                    MessageType.Warning);
            }
        });

        showVoronoi = Section("Voronoi / Biome Grid", showVoronoi, () =>
        {
            EditorGUILayout.PropertyField(numVoronoiPointsProp, new GUIContent("Num Voronoi Points"));
            EditorGUILayout.PropertyField(voronoiSeedProp, new GUIContent("Voronoi Seed"));
            EditorGUILayout.PropertyField(voronoiScaleProp, new GUIContent("Voronoi Scale"));
            EditorGUILayout.PropertyField(useWeightedBiomeProp, new GUIContent("Use Weighted Biome"));
            EditorGUILayout.HelpBox(
                "Voronoi Scale is the base unit several other settings below (Climate Noise Scale, Warp Scale, " +
                "Cluster Radius) derive their own scale from as a multiplier, so they automatically stay " +
                "proportioned if you change this.",
                MessageType.None);
        });

        showNaturalPlacement = Section("Natural Biome Placement", showNaturalPlacement, () =>
        {
            EditorGUILayout.PropertyField(biomeClusterStrengthProp, new GUIContent("Cluster Strength"));
            EditorGUILayout.PropertyField(biomeClusterRadiusMultiplierProp, new GUIContent("Cluster Radius Multiplier"));
            EditorGUILayout.LabelField($"= {generator.BiomeClusterRadius:0.#} world units", EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(biomeRepeatPenaltyProp, new GUIContent("Repeat Penalty"));
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(voronoiWarpStrengthProp, new GUIContent("Border Warp Strength"));
            EditorGUILayout.PropertyField(voronoiWarpScaleMultiplierProp, new GUIContent("Border Warp Scale Multiplier"));
            EditorGUILayout.LabelField($"= {generator.VoronoiWarpScale:0.#} world units", EditorStyles.miniLabel);
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(biomeBlendRangeProp, new GUIContent("Height Blend Range"));
            EditorGUILayout.PropertyField(useBiomeBlendedTexturingProp, new GUIContent("Blend Texturing Too"));
            EditorGUILayout.HelpBox(
                "Cluster Strength/Repeat Penalty turn scattered Voronoi points into contiguous biome " +
                "territories purely by proximity - this works even with Climate below turned off. Border Warp " +
                "bends cell edges into organic coastlines instead of straight polygon lines. Blend Range " +
                "smooths height (and optionally texture) across the transition instead of a hard cut.",
                MessageType.None);

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Reset To Recommended", "Restores Cluster Strength, Cluster Radius Multiplier, Repeat Penalty, Border Warp Strength/Scale, Blend Range and Blend Texturing to their recommended default values.")))
            {
                ResetBiomePlacementToRecommended();
            }
            if (GUILayout.Button(new GUIContent("Turn Off", "Disables natural biome placement's effect: sets Cluster Strength, Repeat Penalty and Border Warp Strength to 0 (their 'fully disabled' value per the code's own design - see tooltips) and Blend Range to 0 (hard biome borders, no smoothing). Cluster Radius Multiplier, Warp Scale Multiplier and Blend Texturing are left as-is since they have no effect once their associated strength is 0.")))
            {
                DisableBiomePlacement();
            }
            EditorGUILayout.EndHorizontal();
        });

        showClimate = Section("Climate (Temperature & Moisture)", showClimate, () =>
        {
            EditorGUILayout.PropertyField(useNaturalClimatePlacementProp, new GUIContent("Use Natural Climate Placement"));
            using (new EditorGUI.DisabledScope(!useNaturalClimatePlacementProp.boolValue))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(climateScaleMultiplierProp, new GUIContent("Climate Scale Multiplier"));
                EditorGUILayout.LabelField($"= {generator.ClimateNoiseScale:0.#} world units", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.HelpBox(
                "How it works: every world position gets a Temperature [0,1] (multi-octave noise blended " +
                "with a gentle latitude gradient - colder further from world Y=0) and a Moisture [0,1] " +
                "(pure multi-octave noise). When enabled, each biome is scored by how close that position's " +
                "climate is to the biome's own Ideal Temperature/Moisture (see each Biome asset's 'Climate' " +
                "section), so deserts cluster somewhere hot & dry, tundra somewhere cold, etc., instead of " +
                "being placed at random. This stacks with Natural Biome Placement above, it does not replace it.\n\n" +
                "Climate also feeds Erosion below: Moisture directly scales how much water (and therefore " +
                "carving power) hydraulic erosion droplets get, per-biome-adjusted by that biome's Rainfall " +
                "Erosion Multiplier.",
                MessageType.Info);

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Reset To Recommended", "Restores Use Natural Climate Placement (ON) and Climate Scale Multiplier to their recommended default values.")))
            {
                ResetClimateToRecommended();
            }
            if (GUILayout.Button(new GUIContent("Turn Off", "Disables climate-driven biome placement (Use Natural Climate Placement = OFF). Note: Moisture still feeds hydraulic erosion's rainfall strength regardless of this setting, whenever Erosion is enabled.")))
            {
                DisableClimate();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Preview Sample Climate At Origin (0,0)"))
            {
                float temp = ClimateGenerator.GetTemperature(Vector2.zero, generator.VoronoiSeed, generator.ClimateNoiseScale);
                float moisture = ClimateGenerator.GetMoisture(Vector2.zero, generator.VoronoiSeed, generator.ClimateNoiseScale);
                Debug.Log($"[TerrainGenerator] Climate at world (0,0): Temperature={temp:0.000}, Moisture={moisture:0.000}");
            }

            showBiomeClimateSummary = EditorGUILayout.Foldout(showBiomeClimateSummary, "Assigned Biomes - Climate & Erosion Summary", true);
            if (showBiomeClimateSummary)
            {
                DrawBiomeClimateSummary(generator);
            }
        });

        showErosionThermal = Section("Erosion - Thermal (Slopes/Talus)", showErosionThermal, () =>
        {
            EditorGUILayout.PropertyField(enableErosionProp, new GUIContent("Enable Erosion (master, thermal + hydraulic)"));
            using (new EditorGUI.DisabledScope(!enableErosionProp.boolValue))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(erosionPaddingProp, new GUIContent("Erosion Padding (cells)"));
                EditorGUILayout.PropertyField(thermalIterationsProp, new GUIContent("Thermal Iterations"));
                EditorGUILayout.PropertyField(talusAngleProp, new GUIContent("Talus Angle (degrees)"));
                EditorGUILayout.PropertyField(thermalErosionRateProp, new GUIContent("Thermal Erosion Rate"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.HelpBox(
                "Thermal erosion collapses slopes steeper than the Talus Angle, sliding material to lower " +
                "neighbors each pass - it's what softens raw noise into settled scree slopes and ridgelines. " +
                "Each biome's Erosion Resistance (on the Biome asset) scales the effective talus angle: hard " +
                "rock tolerates steeper slopes before it slides, soft material (sand, loose soil) gives way sooner.",
                MessageType.None);

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Reset To Recommended", "Restores Enable Erosion (ON) and every thermal + hydraulic erosion field below (in both this section and 'Erosion - Hydraulic') to their recommended default values.")))
            {
                ResetErosionToRecommended();
            }
            if (GUILayout.Button(new GUIContent("Turn Off", "Disables erosion entirely (Enable Erosion = OFF). Every other erosion field is left as-is so your tuning is preserved if you turn it back on.")))
            {
                DisableErosion();
            }
            EditorGUILayout.EndHorizontal();
        });

        showErosionHydraulic = Section("Erosion - Hydraulic (Water/Droplets)", showErosionHydraulic, () =>
        {
            using (new EditorGUI.DisabledScope(!enableErosionProp.boolValue))
            {
                EditorGUILayout.PropertyField(hydraulicDropletDensityProp, new GUIContent("Droplet Density"));
                using (new EditorGUI.DisabledScope(hydraulicDropletDensityProp.floatValue <= 0f))
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(dropletLifetimeProp, new GUIContent("Droplet Lifetime (steps)"));
                    EditorGUILayout.PropertyField(dropletInertiaProp, new GUIContent("Droplet Inertia"));
                    EditorGUILayout.PropertyField(sedimentCapacityFactorProp, new GUIContent("Sediment Capacity Factor"));
                    EditorGUILayout.PropertyField(minSedimentCapacityProp, new GUIContent("Min Sediment Capacity"));
                    EditorGUILayout.PropertyField(erodeSpeedProp, new GUIContent("Erode Speed"));
                    EditorGUILayout.PropertyField(depositSpeedProp, new GUIContent("Deposit Speed"));
                    EditorGUILayout.PropertyField(evaporateSpeedProp, new GUIContent("Evaporate Speed"));
                    EditorGUILayout.PropertyField(erosionGravityProp, new GUIContent("Gravity"));
                    EditorGUILayout.PropertyField(erosionRadiusProp, new GUIContent("Erosion Brush Radius (cells)"));
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.HelpBox(
                "Simulates water droplets flowing downhill, picking up sediment on fast/steep stretches and " +
                "depositing it where they slow down - this is what carves valleys, gullies and alluvial fans. " +
                "Wetter climate (higher Moisture, scaled by each biome's Rainfall Erosion Multiplier) gives " +
                "droplets more water and therefore more carrying/carving power. Droplet spawn points are " +
                "derived deterministically from world position (not a shared RNG stream), so neighboring " +
                "chunks reproduce identical droplets - and therefore identical erosion - wherever their " +
                "padded regions overlap, keeping chunk seams consistent.",
                MessageType.None);
        });

        showErosionDebug = Section("Erosion Debug Visualization", showErosionDebug, () =>
        {
            EditorGUILayout.PropertyField(visualizeErosionDebugProp, new GUIContent("Visualize Erosion (Scene-view gizmos)"));
            using (new EditorGUI.DisabledScope(!visualizeErosionDebugProp.boolValue))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(erosionDebugMinDeltaProp, new GUIContent("Min Delta To Show"));
                EditorGUILayout.PropertyField(erosionDebugMaxDeltaProp, new GUIContent("Delta At Full Intensity"));
                EditorGUILayout.PropertyField(erosionDebugStrideProp, new GUIContent("Cell Stride (sample every N)"));
                EditorGUILayout.PropertyField(erosionDebugGizmoSizeProp, new GUIContent("Gizmo Cube Size"));
                EditorGUILayout.PropertyField(erosionDebugHeightOffsetProp, new GUIContent("Height Offset"));
                EditorGUILayout.PropertyField(erosionDebugMaxGizmosPerChunkProp, new GUIContent("Max Gizmos Per Chunk"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(2);
            DrawErosionLegendSwatch("Erosion (material removed)", new Color(1f, 0f, 0f, 0.95f));
            DrawErosionLegendSwatch("Deposition (material added)", new Color(0.1f, 0.1f, 1f, 0.95f));

            EditorGUILayout.HelpBox(
                "Turn this on, enter Play Mode, and look in the Scene view (not the Game view) once terrain " +
                "chunks around the viewer have generated: every eroded cell gets a small cube, orange/red " +
                "where erosion carved material away and cyan/blue where it deposited material, colored and " +
                "sized by how much that cell changed. This is the fastest way to confirm erosion is actually " +
                "running (and roughly where) versus just eyeballing the mesh. It only draws for chunks that " +
                "already exist, so it is empty before you enter Play Mode.\n\n" +
                "If everything looks pale/tiny, lower 'Delta At Full Intensity'. If the Scene view gets slow, " +
                "raise 'Cell Stride' or lower 'Max Gizmos Per Chunk'.",
                MessageType.Info);

            if (visualizeErosionDebugProp.boolValue && !enableErosionProp.boolValue)
            {
                EditorGUILayout.HelpBox("Erosion itself is disabled above, so there is nothing to visualize.", MessageType.Warning);
            }
        });

        showOther = Section("Other Configuration", showOther, () =>
        {
            EditorGUILayout.PropertyField(levelOfDetailProp, new GUIContent("Level Of Detail"));
        });

        showBiomes = Section("Biomes", showBiomes, () =>
        {
            EditorGUILayout.PropertyField(biomeDefinitionsProp, new GUIContent("Biome Definitions"), true);
            if (biomeDefinitionsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No biomes assigned - terrain generation has nothing to draw from and will fail.", MessageType.Error);
            }
        });

        showObjects = Section("Objects", showObjects, () =>
        {
            EditorGUILayout.PropertyField(shouldSpawnObjectsProp, new GUIContent("Should Spawn Objects"));
            using (new EditorGUI.DisabledScope(!shouldSpawnObjectsProp.boolValue))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(clusterBaseFrequencyProp, new GUIContent("Cluster Base Frequency"));
                EditorGUILayout.PropertyField(clusterAmplitudeProp, new GUIContent("Cluster Amplitude"));
                EditorGUI.indentLevel--;
            }
        });

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

    private static void DrawErosionLegendSwatch(string label, Color color)
    {
        Rect line = EditorGUILayout.GetControlRect(false, 16f);
        Rect swatch = new Rect(line.x + EditorGUI.indentLevel * 15f, line.y, 16f, 16f);
        EditorGUI.DrawRect(swatch, color);
        Rect text = new Rect(swatch.xMax + 6f, line.y, line.width - swatch.width - 6f, line.height);
        EditorGUI.LabelField(text, label);
    }

    /// <summary>
    /// Lists each assigned biome's climate niche and erosion settings side by side, read straight off
    /// the Biome assets, so mismatches (e.g. two biomes fighting over the same climate niche, or a
    /// "desert" with a high rainfall multiplier) are visible without opening every Biome asset individually.
    /// Each row can also be expanded into a full embedded Biome inspector, so a biome's climate/erosion
    /// (or any other) field can be tweaked right here without hunting down and selecting the asset.
    /// </summary>
    private void DrawBiomeClimateSummary(TerrainGenerator generator)
    {
        BiomeInstance[] biomes = generator.BiomeDefinitions;
        if (biomes == null || biomes.Length == 0)
        {
            EditorGUILayout.HelpBox("No biomes assigned.", MessageType.None);
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(20);
        EditorGUILayout.LabelField("Biome", EditorStyles.miniBoldLabel, GUILayout.Width(90));
        EditorGUILayout.LabelField("Ideal Temp", EditorStyles.miniBoldLabel, GUILayout.Width(75));
        EditorGUILayout.LabelField("Ideal Moist.", EditorStyles.miniBoldLabel, GUILayout.Width(75));
        EditorGUILayout.LabelField("Erosion Res.", EditorStyles.miniBoldLabel, GUILayout.Width(75));
        EditorGUILayout.LabelField("Rain Erosion x", EditorStyles.miniBoldLabel, GUILayout.Width(90));
        EditorGUILayout.EndHorizontal();

        foreach (BiomeInstance instance in biomes)
        {
            Biome biome = instance != null ? instance.BiomePrefab : null;

            EditorGUILayout.BeginHorizontal();
            if (biome == null)
            {
                GUILayout.Space(20);
                EditorGUILayout.LabelField("(missing biome prefab)", GUILayout.Width(340));
                EditorGUILayout.EndHorizontal();
                continue;
            }

            bool wasExpanded = _expandedBiomeEditors.Contains(biome);
            bool isExpanded = GUILayout.Toggle(wasExpanded, wasExpanded ? "▾" : "▸", EditorStyles.miniButton, GUILayout.Width(20));
            EditorGUILayout.LabelField(string.IsNullOrEmpty(biome.name) ? "(unnamed)" : biome.name, GUILayout.Width(90));
            EditorGUILayout.LabelField(biome.idealTemperature.ToString("0.00"), GUILayout.Width(75));
            EditorGUILayout.LabelField(biome.idealMoisture.ToString("0.00"), GUILayout.Width(75));
            EditorGUILayout.LabelField(biome.erosionResistance.ToString("0.00"), GUILayout.Width(75));
            EditorGUILayout.LabelField(biome.rainfallErosionMultiplier.ToString("0.00"), GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            if (isExpanded != wasExpanded)
            {
                if (isExpanded) _expandedBiomeEditors.Add(biome);
                else _expandedBiomeEditors.Remove(biome);
            }

            if (isExpanded)
            {
                DrawEmbeddedBiomeEditor(biome);
            }
        }
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Draws (creating and caching on first use) a full embedded inspector for one Biome asset, inline
    /// under its summary row, plus a small toolbar above it: apply a recommended preset for a common
    /// biome archetype, or save/load a full backup of this biome's tunable fields. The cached sub-editor
    /// is reused across repaints and only rebuilt if the asset it points at changes; all cached editors
    /// are destroyed in <see cref="OnDisable"/>.
    /// </summary>
    private void DrawEmbeddedBiomeEditor(Biome biome)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUI.indentLevel++;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Apply Preset...",
            "Overwrites this biome's Climate (Ideal Temperature/Moisture, tolerances), Erosion (Resistance, " +
            "Rainfall Multiplier) and noise shape (Amplitude, Frequency, Persistence) with a recommended " +
            "starting point for a common biome archetype. Does NOT touch Weight, Min/Max Height, or textures. " +
            "Amplitude/Frequency are relative starting points, not tied to your world's actual scale - rescale " +
            "them to match after applying."), EditorStyles.miniButton))
        {
            ShowBiomePresetMenu(biome);
        }
        if (GUILayout.Button(new GUIContent("Save State",
            "Writes every tunable field on this biome (height range, weight, noise shape, climate, erosion) to " +
            "a backup file under Assets/BiomeBackups/, so you can restore it later after applying a preset or " +
            "experimenting with values."), EditorStyles.miniButton))
        {
            SaveBiomeState(biome);
        }
        using (new EditorGUI.DisabledScope(!HasSavedBiomeState(biome)))
        {
            if (GUILayout.Button(new GUIContent("Load Saved State", "Restores this biome's fields from its last Save State backup."), EditorStyles.miniButton))
            {
                LoadBiomeState(biome);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (!HasSavedBiomeState(biome))
        {
            EditorGUILayout.LabelField("No saved state yet for this biome - click Save State to create one.", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(2);

        if (!_biomeEditorCache.TryGetValue(biome, out Editor biomeEditor) || biomeEditor == null || biomeEditor.target != biome)
        {
            if (biomeEditor != null)
                DestroyImmediate(biomeEditor);
            biomeEditor = Editor.CreateEditor(biome);
            _biomeEditorCache[biome] = biomeEditor;
        }
        biomeEditor.OnInspectorGUI();

        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
    }

    // --- Biome presets: recommended starting points for common archetypes, covering only the
    // scale-independent fields (climate niche, erosion, noise shape). Weight and Min/Max Height are left
    // untouched since those depend on how this biome is balanced against the others in THIS project. ---

    private struct BiomePresetDefinition
    {
        public string Name;
        public float IdealTemperature, IdealMoisture, TemperatureTolerance, MoistureTolerance;
        public float ErosionResistance, RainfallErosionMultiplier;
        public float Amplitude, Frequency, Persistence;
    }

    private static readonly BiomePresetDefinition[] BiomePresets =
    {
        new BiomePresetDefinition { Name = "Mountain",        IdealTemperature = 0.35f, IdealMoisture = 0.45f, TemperatureTolerance = 0.35f, MoistureTolerance = 0.40f, ErosionResistance = 0.85f, RainfallErosionMultiplier = 0.8f, Amplitude = 60f, Frequency = 1.5f, Persistence = 0.50f },
        new BiomePresetDefinition { Name = "Tundra",          IdealTemperature = 0.08f, IdealMoisture = 0.35f, TemperatureTolerance = 0.20f, MoistureTolerance = 0.30f, ErosionResistance = 0.55f, RainfallErosionMultiplier = 0.6f, Amplitude = 8f,  Frequency = 1.2f, Persistence = 0.45f },
        new BiomePresetDefinition { Name = "Grassland",       IdealTemperature = 0.55f, IdealMoisture = 0.45f, TemperatureTolerance = 0.30f, MoistureTolerance = 0.30f, ErosionResistance = 0.35f, RainfallErosionMultiplier = 1.0f, Amplitude = 4f,  Frequency = 1.0f, Persistence = 0.40f },
        new BiomePresetDefinition { Name = "Forest",          IdealTemperature = 0.50f, IdealMoisture = 0.60f, TemperatureTolerance = 0.30f, MoistureTolerance = 0.30f, ErosionResistance = 0.40f, RainfallErosionMultiplier = 1.1f, Amplitude = 10f, Frequency = 1.3f, Persistence = 0.45f },
        new BiomePresetDefinition { Name = "Desert",          IdealTemperature = 0.85f, IdealMoisture = 0.10f, TemperatureTolerance = 0.25f, MoistureTolerance = 0.20f, ErosionResistance = 0.20f, RainfallErosionMultiplier = 0.3f, Amplitude = 12f, Frequency = 0.8f, Persistence = 0.35f },
        new BiomePresetDefinition { Name = "Swamp",           IdealTemperature = 0.60f, IdealMoisture = 0.90f, TemperatureTolerance = 0.30f, MoistureTolerance = 0.25f, ErosionResistance = 0.25f, RainfallErosionMultiplier = 1.4f, Amplitude = 2f,  Frequency = 1.0f, Persistence = 0.30f },
        new BiomePresetDefinition { Name = "Jungle",          IdealTemperature = 0.85f, IdealMoisture = 0.85f, TemperatureTolerance = 0.25f, MoistureTolerance = 0.25f, ErosionResistance = 0.35f, RainfallErosionMultiplier = 1.6f, Amplitude = 14f, Frequency = 1.4f, Persistence = 0.50f },
        new BiomePresetDefinition { Name = "Beach / Coastal", IdealTemperature = 0.65f, IdealMoisture = 0.55f, TemperatureTolerance = 0.35f, MoistureTolerance = 0.35f, ErosionResistance = 0.15f, RainfallErosionMultiplier = 1.2f, Amplitude = 3f,  Frequency = 0.9f, Persistence = 0.30f },
    };

    private static void ShowBiomePresetMenu(Biome biome)
    {
        GenericMenu menu = new GenericMenu();
        foreach (BiomePresetDefinition preset in BiomePresets)
        {
            BiomePresetDefinition capturedPreset = preset;
            menu.AddItem(new GUIContent(capturedPreset.Name), false, () => ApplyBiomePreset(biome, capturedPreset));
        }
        menu.ShowAsContext();
    }

    private static void ApplyBiomePreset(Biome biome, BiomePresetDefinition preset)
    {
        Undo.RecordObject(biome, $"Apply {preset.Name} Biome Preset");
        biome.idealTemperature = preset.IdealTemperature;
        biome.idealMoisture = preset.IdealMoisture;
        biome.temperatureTolerance = preset.TemperatureTolerance;
        biome.moistureTolerance = preset.MoistureTolerance;
        biome.erosionResistance = preset.ErosionResistance;
        biome.rainfallErosionMultiplier = preset.RainfallErosionMultiplier;
        biome.amplitude = preset.Amplitude;
        biome.frequency = preset.Frequency;
        biome.persistence = preset.Persistence;
        EditorUtility.SetDirty(biome);
        Debug.Log($"[TerrainGenerator] Applied '{preset.Name}' preset to biome '{biome.name}'. Amplitude/Frequency are relative starting points - rescale them to match your world's overall height scale.");
    }

    // --- Save/Load a full backup of a biome's tunable fields, as JSON under Assets/BiomeBackups/. Keyed
    // by the biome asset's GUID (falling back to its name) so a rename doesn't orphan its backup. ---

    [System.Serializable]
    private class BiomeStateSnapshot
    {
        public float minHeight, maxHeight, amplitude, frequency, weight, persistence;
        public float idealTemperature, idealMoisture, temperatureTolerance, moistureTolerance;
        public float erosionResistance, rainfallErosionMultiplier;
    }

    private const string BiomeBackupFolder = "Assets/BiomeBackups";

    private static string GetBiomeBackupPath(Biome biome)
    {
        string assetPath = AssetDatabase.GetAssetPath(biome);
        string guid = !string.IsNullOrEmpty(assetPath) ? AssetDatabase.AssetPathToGUID(assetPath) : null;
        string safeName = string.IsNullOrEmpty(biome.name) ? "UnnamedBiome" : biome.name;
        string fileKey = !string.IsNullOrEmpty(guid) ? guid : safeName;
        return $"{BiomeBackupFolder}/{safeName}_{fileKey}.json";
    }

    private static bool HasSavedBiomeState(Biome biome)
    {
        return biome != null && File.Exists(GetBiomeBackupPath(biome));
    }

    private static void SaveBiomeState(Biome biome)
    {
        if (!AssetDatabase.IsValidFolder(BiomeBackupFolder))
        {
            AssetDatabase.CreateFolder("Assets", "BiomeBackups");
        }

        BiomeStateSnapshot snapshot = new BiomeStateSnapshot
        {
            minHeight = biome.minHeight,
            maxHeight = biome.maxHeight,
            amplitude = biome.amplitude,
            frequency = biome.frequency,
            weight = biome.weight,
            persistence = biome.persistence,
            idealTemperature = biome.idealTemperature,
            idealMoisture = biome.idealMoisture,
            temperatureTolerance = biome.temperatureTolerance,
            moistureTolerance = biome.moistureTolerance,
            erosionResistance = biome.erosionResistance,
            rainfallErosionMultiplier = biome.rainfallErosionMultiplier
        };

        string path = GetBiomeBackupPath(biome);
        File.WriteAllText(path, JsonUtility.ToJson(snapshot, true));
        AssetDatabase.ImportAsset(path);
        Debug.Log($"[TerrainGenerator] Saved biome state for '{biome.name}' to {path}");
    }

    private static void LoadBiomeState(Biome biome)
    {
        string path = GetBiomeBackupPath(biome);
        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog("No Saved State", $"No saved state found for '{biome.name}'. Use Save State first.", "OK");
            return;
        }

        BiomeStateSnapshot snapshot = JsonUtility.FromJson<BiomeStateSnapshot>(File.ReadAllText(path));
        if (snapshot == null)
        {
            Debug.LogError($"[TerrainGenerator] Failed to parse saved state at {path}");
            return;
        }

        Undo.RecordObject(biome, "Load Biome State");
        biome.minHeight = snapshot.minHeight;
        biome.maxHeight = snapshot.maxHeight;
        biome.amplitude = snapshot.amplitude;
        biome.frequency = snapshot.frequency;
        biome.weight = snapshot.weight;
        biome.persistence = snapshot.persistence;
        biome.idealTemperature = snapshot.idealTemperature;
        biome.idealMoisture = snapshot.idealMoisture;
        biome.temperatureTolerance = snapshot.temperatureTolerance;
        biome.moistureTolerance = snapshot.moistureTolerance;
        biome.erosionResistance = snapshot.erosionResistance;
        biome.rainfallErosionMultiplier = snapshot.rainfallErosionMultiplier;
        EditorUtility.SetDirty(biome);
        Debug.Log($"[TerrainGenerator] Loaded biome state for '{biome.name}' from {path}");
    }

    // --- Recommended defaults, matching TerrainGenerator's own field initializers - kept here rather
    // than reflected/read back so a reset always restores a known-good baseline even if the live fields
    // have drifted far from it. "Turn Off" only flips/zeroes the fields whose value is documented (in
    // TerrainGenerator's own field comments) to fully disable that feature, leaving everything else as
    // the user tuned it, so turning a section back on doesn't lose prior work. ---

    private void ResetBiomePlacementToRecommended()
    {
        biomeClusterStrengthProp.floatValue = 0.5f;
        biomeClusterRadiusMultiplierProp.floatValue = 1.5f;
        biomeRepeatPenaltyProp.floatValue = 0.6f;
        voronoiWarpStrengthProp.floatValue = 50f;
        voronoiWarpScaleMultiplierProp.floatValue = 1.5f;
        biomeBlendRangeProp.floatValue = 0.25f;
        useBiomeBlendedTexturingProp.boolValue = true;
        serializedObject.ApplyModifiedProperties();
    }

    private void DisableBiomePlacement()
    {
        biomeClusterStrengthProp.floatValue = 0f;
        biomeRepeatPenaltyProp.floatValue = 0f;
        voronoiWarpStrengthProp.floatValue = 0f;
        biomeBlendRangeProp.floatValue = 0f;
        serializedObject.ApplyModifiedProperties();
    }

    private void ResetClimateToRecommended()
    {
        useNaturalClimatePlacementProp.boolValue = true;
        climateScaleMultiplierProp.floatValue = 6f;
        serializedObject.ApplyModifiedProperties();
    }

    private void DisableClimate()
    {
        useNaturalClimatePlacementProp.boolValue = false;
        serializedObject.ApplyModifiedProperties();
    }

    private void ResetErosionToRecommended()
    {
        enableErosionProp.boolValue = true;
        erosionPaddingProp.intValue = 40;
        thermalIterationsProp.intValue = 5;
        talusAngleProp.floatValue = 33f;
        thermalErosionRateProp.floatValue = 0.5f;
        hydraulicDropletDensityProp.floatValue = 0.12f;
        dropletLifetimeProp.intValue = 30;
        dropletInertiaProp.floatValue = 0.05f;
        sedimentCapacityFactorProp.floatValue = 4f;
        minSedimentCapacityProp.floatValue = 0.01f;
        erodeSpeedProp.floatValue = 0.3f;
        depositSpeedProp.floatValue = 0.3f;
        evaporateSpeedProp.floatValue = 0.02f;
        erosionGravityProp.floatValue = 4f;
        erosionRadiusProp.floatValue = 3f;
        serializedObject.ApplyModifiedProperties();
    }

    private void DisableErosion()
    {
        enableErosionProp.boolValue = false;
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Flags parameter combinations that are individually valid (won't throw/crash) but are documented -
    /// in this class or its collaborators - to produce visible artifacts, so misconfigurations surface
    /// in the Inspector instead of only after noticing something looks wrong in-game.
    /// </summary>
    private static void DrawValidationWarnings(TerrainGenerator generator)
    {
        if (generator.BiomeDefinitions == null || generator.BiomeDefinitions.Length == 0)
        {
            EditorGUILayout.HelpBox("No biomes assigned - see the Biomes section below.", MessageType.Error);
        }

        if (generator.EnableErosion && generator.HydraulicDropletDensity > 0f && generator.ErosionPadding <= generator.DropletLifetime)
        {
            EditorGUILayout.HelpBox(
                $"Erosion Padding ({generator.ErosionPadding}) should comfortably exceed Droplet Lifetime " +
                $"({generator.DropletLifetime}) so a droplet's full travel stays inside the padded region. " +
                "Otherwise neighboring chunks can erode inconsistently near their shared edge, producing a visible seam.",
                MessageType.Warning);
        }

        if (generator.UseNaturalClimatePlacement && generator.ClimateNoiseScale < generator.VoronoiScale * 3f)
        {
            EditorGUILayout.HelpBox(
                "Climate Scale Multiplier is low relative to Voronoi Scale: climate will vary almost cell-to-cell " +
                "instead of spanning several biome cells, producing a 'salt and pepper' patchwork instead of " +
                "natural clustered territories. Keep the multiplier around 5-10.",
                MessageType.Warning);
        }

        if (generator.VoronoiWarpStrength > 0f && generator.VoronoiWarpScale < generator.VoronoiScale)
        {
            EditorGUILayout.HelpBox(
                "Border Warp Scale is smaller than Voronoi Scale: the warp can oscillate multiple times across " +
                "a single cell edge, tearing borders into jagged, self-crossing shapes. Keep the scale multiplier at or above 1.",
                MessageType.Warning);
        }
    }
}
