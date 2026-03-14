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

        showTerrainConfig = Section("Terrain Configuration", showTerrainConfig, () =>
        {
            Field(terrainSizeProp, "Terrain Size");
            EditorGUILayout.LabelField($"Resulting chunk size: {generator.ChunkSize} x {generator.ChunkSize} cells", EditorStyles.miniLabel);
        });

        showNoise = Section("Noise Configuration", showNoise, () =>
        {
            Field(octavesProp, "Octaves");
            Field(lacunarityProp, "Lacunarity");
            EditorGUILayout.HelpBox(
                "Each biome also defines its own amplitude/frequency/persistence (on the Biome asset) - " +
                "these two fields only control how many fractal layers are combined (Octaves) and how much " +
                "each successive layer's frequency grows (Lacunarity), shared by every biome.\n\n" +
                "Both only affect biomes using the Classic landform. Other landforms choose their own layers " +
                "(down to a few world units of detail) - see Terrain Shape (Landforms).",
                MessageType.None);
        });

        showLandforms = Section("Terrain Shape (Landforms)", showLandforms, () => DrawLandformSection(generator));

        showHeightAndTexture = Section("Height Range & Texture", showHeightAndTexture, () =>
        {
            Field(terrainTextureBasedOnVoronoiPointsProp, "Texture Based On Voronoi Points");
            using (new EditorGUI.DisabledScope(generator.TerrainTextureBasedOnVoronoiPoints))
            {
                Field(minHeightProp, "Min Height (tracked)");
                Field(maxHeightProp, "Max Height (tracked)");
            }
            EditorGUILayout.HelpBox(
                generator.TerrainTextureBasedOnVoronoiPoints
                    ? "Min/Max Height are unused while textures are based on Voronoi points - texturing uses each biome's own height range instead."
                    : "Min/Max Height are updated automatically from every generated cell (used to normalize non-Voronoi texturing) and only ever grow - they are not reset between Play sessions.",
                MessageType.None);
        });

        showTextureVariations = Section("Texture Variations", showTextureVariations, () =>
        {
            Field(enableTextureVariationsProp, "Enable Texture Variations (master)");
            using (new EditorGUI.DisabledScope(!generator.EnableTextureVariations))
            {
                EditorGUI.indentLevel++;
                Field(enableUVRotationProp, "UV Rotation");
                Field(enableUVNoiseProp, "UV Noise Offset");
                using (new EditorGUI.DisabledScope(!enableUVNoiseProp.boolValue))
                {
                    EditorGUI.indentLevel++;
                    Field(uvNoiseStrengthProp, "Noise Strength");
                    Field(uvNoiseScaleProp, "Noise Scale");
                    EditorGUI.indentLevel--;
                }
                Field(enableTextureScaleVariationProp, "Texture Scale Variation");
                using (new EditorGUI.DisabledScope(!enableTextureScaleVariationProp.boolValue))
                {
                    EditorGUI.indentLevel++;
                    Field(textureScaleVariationRangeProp, "Scale Variation Range");
                    EditorGUI.indentLevel--;
                }
                Field(enableShaderEnhancementsProp, "Shader-Based Enhancements");
                using (new EditorGUI.DisabledScope(!enableShaderEnhancementsProp.boolValue))
                {
                    EditorGUI.indentLevel++;
                    Field(shaderUVRotationStrengthProp, "Shader UV Rotation Strength");
                    Field(shaderUVScaleVariationProp, "Shader UV Scale Variation");
                    Field(shaderTextureBlendSharpnessProp, "Shader Blend Sharpness");
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
            Field(numVoronoiPointsProp, "Num Voronoi Points");
            Field(voronoiSeedProp, "Voronoi Seed");
            Field(voronoiScaleProp, "Voronoi Scale");
            Field(useWeightedBiomeProp, "Use Weighted Biome");
            EditorGUILayout.HelpBox(
                "Voronoi Scale is the base unit several other settings below (Climate Noise Scale, Warp Scale, " +
                "Cluster Radius) derive their own scale from as a multiplier, so they automatically stay " +
                "proportioned if you change this.",
                MessageType.None);
        });

        showNaturalPlacement = Section("Natural Biome Placement", showNaturalPlacement, () =>
        {
            Field(biomeClusterStrengthProp, "Cluster Strength");
            Field(biomeClusterRadiusMultiplierProp, "Cluster Radius Multiplier");
            EditorGUILayout.LabelField($"= {generator.BiomeClusterRadius:0.#} world units", EditorStyles.miniLabel);
            Field(biomeRepeatPenaltyProp, "Repeat Penalty");
            EditorGUILayout.Space(2);
            Field(voronoiWarpStrengthProp, "Border Warp Strength");
            Field(voronoiWarpScaleMultiplierProp, "Border Warp Scale Multiplier");
            EditorGUILayout.LabelField($"= {generator.VoronoiWarpScale:0.#} world units", EditorStyles.miniLabel);
            EditorGUILayout.Space(2);
            Field(biomeBlendRangeProp, "Height Blend Range");
            Field(useBiomeBlendedTexturingProp, "Blend Texturing Too");
            Field(biomeBoundaryMaxSlopeDegreesProp, "Boundary Max Walkable Slope");
            DrawProp("orderIndependentBiomeLayout", "Order-Independent Layout");
            EditorGUILayout.HelpBox(
                "Cluster Strength/Repeat Penalty turn scattered Voronoi points into contiguous biome " +
                "territories purely by proximity - this works even with Climate below turned off. Border Warp " +
                "bends cell edges into organic coastlines instead of straight polygon lines. Blend Range " +
                "smooths height (and optionally texture) across the transition instead of a hard cut.\n\n" +
                "Boundary Max Walkable Slope widens that blend band further, beyond Blend Range if needed, " +
                "whenever two neighboring biomes' height ranges differ enough (e.g. Mountains next to Plains) " +
                "that the border would otherwise be steeper than this - preventing unwalkable artificial " +
                "walls at biome borders without touching either biome's own natural terrain elsewhere. " +
                "0 disables this and leaves border width exactly as Blend Range specifies.",
                MessageType.None);

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Reset To Recommended", "Restores Cluster Strength, Cluster Radius Multiplier, Repeat Penalty, Border Warp Strength/Scale, Blend Range, Blend Texturing and Boundary Max Walkable Slope to their recommended default values.")))
            {
                ResetBiomePlacementToRecommended();
            }
            if (GUILayout.Button(new GUIContent("Turn Off", "Disables natural biome placement's effect: sets Cluster Strength, Repeat Penalty and Border Warp Strength to 0 (their 'fully disabled' value per the code's own design - see tooltips) and Blend Range and Boundary Max Walkable Slope to 0 (hard biome borders, no smoothing). Cluster Radius Multiplier, Warp Scale Multiplier and Blend Texturing are left as-is since they have no effect once their associated strength is 0.")))
            {
                DisableBiomePlacement();
            }
            EditorGUILayout.EndHorizontal();
        });

        showClimate = Section("Climate (Temperature & Moisture)", showClimate, () =>
        {
            Field(useNaturalClimatePlacementProp, "Use Natural Climate Placement");
            using (new EditorGUI.DisabledScope(!useNaturalClimatePlacementProp.boolValue))
            {
                EditorGUI.indentLevel++;
                Field(climateScaleMultiplierProp, "Climate Scale Multiplier");
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
            Field(enableErosionProp, "Enable Erosion (master, thermal + hydraulic)");
            using (new EditorGUI.DisabledScope(!enableErosionProp.boolValue))
            {
                EditorGUI.indentLevel++;
                Field(erosionPaddingProp, "Erosion Padding (cells)");
                Field(thermalIterationsProp, "Thermal Iterations");
                Field(talusAngleProp, "Talus Angle (degrees)");
                Field(thermalErosionRateProp, "Thermal Erosion Rate");
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
                Field(hydraulicDropletDensityProp, "Droplet Density");
                using (new EditorGUI.DisabledScope(hydraulicDropletDensityProp.floatValue <= 0f))
                {
                    EditorGUI.indentLevel++;
                    Field(dropletLifetimeProp, "Droplet Lifetime (steps)");
                    Field(dropletInertiaProp, "Droplet Inertia");
                    Field(sedimentCapacityFactorProp, "Sediment Capacity Factor");
                    Field(minSedimentCapacityProp, "Min Sediment Capacity");
                    Field(erodeSpeedProp, "Erode Speed");
                    Field(depositSpeedProp, "Deposit Speed");
                    Field(evaporateSpeedProp, "Evaporate Speed");
                    Field(erosionGravityProp, "Gravity");
                    Field(erosionRadiusProp, "Erosion Brush Radius (cells)");
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

        showWater = Section("Water (Oceans, Lakes, Ponds, Rivers)", showWater, () =>
        {
            EditorGUILayout.HelpBox(
                "Each water type is its own geographic feature with its own rules and water level - none of them " +
                "is simply 'terrain below a height', and none is decided by biome (biomes only make lakes, ponds " +
                "and river springs more or less likely to start there - see each Biome asset's Water section).\n\n" +
                "Oceans: large-scale, from a low-frequency continent field. Lakes/Ponds: sparse sites on gentle, " +
                "preferably sunken ground; each carves a basin and keeps a closed rim above its own water level. " +
                "Rivers: traced downhill from springs (or lake outlets) to the ocean or a lake, carving a channel " +
                "and valley; they overflow terrain pits through their lowest saddle. All of it is computed from " +
                "world position + seed and shared by every chunk, so water is identical across chunk borders.",
                MessageType.Info);

            DrawProp("enableWater", "Enable Water (master)");
            using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("enableWater").boolValue))
            {
                DrawProp("waterLevel", "Sea Level (oceans only)");
                DrawProp("enableSwimDetection", "Swim Detection (OxygenManager)");

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Materials (optional - built-in tinted fallbacks otherwise)", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                DrawProp("waterMaterial", "Default");
                DrawProp("oceanMaterial", "Ocean");
                DrawProp("lakeMaterial", "Lake");
                DrawProp("pondMaterial", "Pond");
                DrawProp("riverMaterial", "River");
                EditorGUI.indentLevel--;

                DrawWaterGroup("Oceans", "enableOceans", WaterOceanFields);
                EditorGUILayout.LabelField($"Continent scale = {generator.ContinentScale:0} world units", EditorStyles.miniLabel);
                DrawWaterGroup("Lakes", "enableLakes", WaterLakeFields);
                DrawWaterGroup("Ponds", "enablePonds", WaterPondFields);
                DrawWaterGroup("Shorelines (lakes & ponds)", null, WaterShoreFields);
                DrawWaterGroup("Rivers", "enableRivers", WaterRiverFields);

                // Water can't be finer than the terrain mesh it sits in.
                int lod = levelOfDetailProp.intValue;
                int vertexSpacing = lod > 0 ? lod * 2 : 1;
                float narrowest = serializedObject.FindProperty("riverSourceWidth").floatValue;
                if (serializedObject.FindProperty("enableRivers").boolValue && narrowest < vertexSpacing * 2f)
                {
                    EditorGUILayout.HelpBox(
                        $"At Level Of Detail {lod} terrain vertices are {vertexSpacing} units apart, so river stretches " +
                        $"narrower than ~{vertexSpacing * 2} units (River Source Width is {narrowest:0.#}) can't be " +
                        "represented and won't show - rivers will appear to start further downstream. Lower the Level " +
                        "Of Detail or widen the rivers if that matters.",
                        MessageType.Warning);
                }
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Reset To Recommended", "Restores every water tuning parameter (toggles, ocean/lake/pond/shore/river settings) to its recommended value. Leaves Sea Level and the water materials untouched, since those are world/art-specific choices rather than tuning parameters.")))
            {
                ResetWaterToRecommended();
            }
            if (GUILayout.Button(new GUIContent("Reset To Default", "Restores every water setting - including Sea Level and the water materials - to its original factory default.")))
            {
                ResetWaterToDefault();
            }
            EditorGUILayout.EndHorizontal();
        });

        showErosionDebug = Section("Erosion Debug Visualization", showErosionDebug, () =>
        {
            Field(visualizeErosionDebugProp, "Visualize Erosion (Scene-view gizmos)");
            using (new EditorGUI.DisabledScope(!visualizeErosionDebugProp.boolValue))
            {
                EditorGUI.indentLevel++;
                Field(erosionDebugMinDeltaProp, "Min Delta To Show");
                Field(erosionDebugMaxDeltaProp, "Delta At Full Intensity");
                Field(erosionDebugStrideProp, "Cell Stride (sample every N)");
                Field(erosionDebugGizmoSizeProp, "Gizmo Cube Size");
                Field(erosionDebugHeightOffsetProp, "Height Offset");
                Field(erosionDebugMaxGizmosPerChunkProp, "Max Gizmos Per Chunk");
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
            Field(levelOfDetailProp, "Level Of Detail");
        });

        showBiomes = Section("Biomes", showBiomes, () =>
        {
            Field(biomeDefinitionsProp, "Biome Definitions", true);
            if (biomeDefinitionsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No biomes assigned - terrain generation has nothing to draw from and will fail.", MessageType.Error);
            }
        });

        showObjects = Section("Objects", showObjects, () =>
        {
            Field(shouldSpawnObjectsProp, "Should Spawn Objects");
            using (new EditorGUI.DisabledScope(!shouldSpawnObjectsProp.boolValue))
            {
                EditorGUI.indentLevel++;
                Field(clusterBaseFrequencyProp, "Cluster Base Frequency");
                Field(clusterAmplitudeProp, "Cluster Amplitude");
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
        EditorGUILayout.LabelField(new GUIContent("Biome", "Biome name. Click the arrow to edit the biome inline."), EditorStyles.miniBoldLabel, GUILayout.Width(90));
        EditorGUILayout.LabelField(new GUIContent("Ideal Temp", "Temperature (0 = coldest, 1 = hottest) where this biome fits best."), EditorStyles.miniBoldLabel, GUILayout.Width(75));
        EditorGUILayout.LabelField(new GUIContent("Ideal Moist.", "Moisture (0 = driest, 1 = wettest) where this biome fits best."), EditorStyles.miniBoldLabel, GUILayout.Width(75));
        EditorGUILayout.LabelField(new GUIContent("Erosion Res.", "0 = soft ground that erodes and slumps easily, 1 = hard rock that holds steep slopes."), EditorStyles.miniBoldLabel, GUILayout.Width(75));
        EditorGUILayout.LabelField(new GUIContent("Rain Erosion x", "Multiplier on water-erosion strength in this biome (higher = deeper gullies)."), EditorStyles.miniBoldLabel, GUILayout.Width(90));
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
            "Rainfall Multiplier), Landform and noise shape (Amplitude, Frequency, Persistence) with a recommended " +
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
        if (GUILayout.Button(new GUIContent("Copy", "Copies every value of this biome asset to the clipboard as text."), EditorStyles.miniButton))
        {
            CopyBiome(biome);
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
        public LandformType Landform;
    }

    private static readonly BiomePresetDefinition[] BiomePresets =
    {
        new BiomePresetDefinition { Name = "Mountain",        IdealTemperature = 0.35f, IdealMoisture = 0.45f, TemperatureTolerance = 0.35f, MoistureTolerance = 0.40f, ErosionResistance = 0.85f, RainfallErosionMultiplier = 0.8f, Amplitude = 60f, Frequency = 1.5f, Persistence = 0.50f, Landform = LandformType.Mountains },
        new BiomePresetDefinition { Name = "Tundra",          IdealTemperature = 0.08f, IdealMoisture = 0.35f, TemperatureTolerance = 0.20f, MoistureTolerance = 0.30f, ErosionResistance = 0.55f, RainfallErosionMultiplier = 0.6f, Amplitude = 8f,  Frequency = 1.2f, Persistence = 0.45f, Landform = LandformType.Plains },
        new BiomePresetDefinition { Name = "Grassland",       IdealTemperature = 0.55f, IdealMoisture = 0.45f, TemperatureTolerance = 0.30f, MoistureTolerance = 0.30f, ErosionResistance = 0.35f, RainfallErosionMultiplier = 1.0f, Amplitude = 4f,  Frequency = 1.0f, Persistence = 0.40f, Landform = LandformType.Plains },
        new BiomePresetDefinition { Name = "Forest",          IdealTemperature = 0.50f, IdealMoisture = 0.60f, TemperatureTolerance = 0.30f, MoistureTolerance = 0.30f, ErosionResistance = 0.40f, RainfallErosionMultiplier = 1.1f, Amplitude = 10f, Frequency = 1.3f, Persistence = 0.45f, Landform = LandformType.Hills },
        new BiomePresetDefinition { Name = "Desert",          IdealTemperature = 0.85f, IdealMoisture = 0.10f, TemperatureTolerance = 0.25f, MoistureTolerance = 0.20f, ErosionResistance = 0.20f, RainfallErosionMultiplier = 0.3f, Amplitude = 12f, Frequency = 0.8f, Persistence = 0.35f, Landform = LandformType.Dunes },
        new BiomePresetDefinition { Name = "Swamp",           IdealTemperature = 0.60f, IdealMoisture = 0.90f, TemperatureTolerance = 0.30f, MoistureTolerance = 0.25f, ErosionResistance = 0.25f, RainfallErosionMultiplier = 1.4f, Amplitude = 2f,  Frequency = 1.0f, Persistence = 0.30f, Landform = LandformType.Wetland },
        new BiomePresetDefinition { Name = "Jungle",          IdealTemperature = 0.85f, IdealMoisture = 0.85f, TemperatureTolerance = 0.25f, MoistureTolerance = 0.25f, ErosionResistance = 0.35f, RainfallErosionMultiplier = 1.6f, Amplitude = 14f, Frequency = 1.4f, Persistence = 0.50f, Landform = LandformType.Hills },
        new BiomePresetDefinition { Name = "Beach / Coastal", IdealTemperature = 0.65f, IdealMoisture = 0.55f, TemperatureTolerance = 0.35f, MoistureTolerance = 0.35f, ErosionResistance = 0.15f, RainfallErosionMultiplier = 1.2f, Amplitude = 3f,  Frequency = 0.9f, Persistence = 0.30f, Landform = LandformType.Plains },
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
        biome.landform = preset.Landform;
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
        // Added with the water system; version 0 = older backup without them (left untouched on load).
        public int version;
        public float baseElevation;
        public bool allowsWaterBodies;
        public float lakeLikelihood, pondLikelihood, riverSpringLikelihood;
        // Version 2 adds the landform.
        public int landform;
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
            rainfallErosionMultiplier = biome.rainfallErosionMultiplier,
            version = 2,
            landform = (int)biome.landform,
            baseElevation = biome.baseElevation,
            allowsWaterBodies = biome.allowsWaterBodies,
            lakeLikelihood = biome.lakeLikelihood,
            pondLikelihood = biome.pondLikelihood,
            riverSpringLikelihood = biome.riverSpringLikelihood
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
        if (snapshot.version >= 1)
        {
            biome.baseElevation = snapshot.baseElevation;
            biome.allowsWaterBodies = snapshot.allowsWaterBodies;
            biome.lakeLikelihood = snapshot.lakeLikelihood;
            biome.pondLikelihood = snapshot.pondLikelihood;
            biome.riverSpringLikelihood = snapshot.riverSpringLikelihood;
        }
        if (snapshot.version >= 2)
        {
            biome.landform = (LandformType)snapshot.landform;
        }
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
        biomeBoundaryMaxSlopeDegreesProp.floatValue = 28f;
        serializedObject.ApplyModifiedProperties();
    }

    private void DisableBiomePlacement()
    {
        biomeClusterStrengthProp.floatValue = 0f;
        biomeRepeatPenaltyProp.floatValue = 0f;
        voronoiWarpStrengthProp.floatValue = 0f;
        biomeBlendRangeProp.floatValue = 0f;
        biomeBoundaryMaxSlopeDegreesProp.floatValue = 0f;
        serializedObject.ApplyModifiedProperties();
    }

    // (field name, label) per water sub-section, drawn by DrawWaterGroup.
    private static readonly string[,] WaterOceanFields =
    {
        { "continentScaleMultiplier", "Continent Scale Multiplier" },
        { "oceanThreshold", "Ocean Threshold (lower = rarer)" },
        { "beachWidth", "Beach Width" },
        { "beachHeight", "Beach Height" },
        { "coastBlendWidth", "Coast Blend Width" },
        { "continentalShelfWidth", "Continental Shelf Width" },
        { "oceanDepth", "Ocean Depth" },
        { "inlandRise", "Inland Rise" },
        { "inlandRiseDistance", "Inland Rise Distance" },
        { "islandFrequency", "Island Frequency" },
        { "islandScaleMultiplier", "Island Scale Multiplier" },
        { "islandPeakHeight", "Island Peak Height" },
        { "spawnLandRadius", "Spawn Land Radius" },
    };

    private static readonly string[,] WaterLakeFields =
    {
        { "lakeSpacing", "Lake Spacing" },
        { "lakeChance", "Lake Chance" },
        { "lakeMinRadius", "Min Radius" },
        { "lakeMaxRadius", "Max Radius" },
        { "lakeMaxDepth", "Max Depth" },
        { "lakeMaxSiteSlope", "Max Site Slope" },
        { "lakeOutletChance", "Outlet River Chance" },
    };

    private static readonly string[,] WaterPondFields =
    {
        { "pondSpacing", "Pond Spacing" },
        { "pondChance", "Pond Chance" },
        { "pondMinRadius", "Min Radius" },
        { "pondMaxRadius", "Max Radius" },
        { "pondDepth", "Depth" },
        { "pondMaxSiteSlope", "Max Site Slope" },
    };

    private static readonly string[,] WaterShoreFields =
    {
        { "shoreRimWidth", "Rim Width" },
        { "shoreFreeboard", "Rim Freeboard" },
    };

    private static readonly string[,] WaterRiverFields =
    {
        { "riverSpacing", "Spring Spacing" },
        { "riverChance", "Spring Chance" },
        { "riverMinSpringElevation", "Min Spring Elevation" },
        { "riverMinLength", "Min Length" },
        { "riverMaxLength", "Max Length" },
        { "riverSourceWidth", "Source Width" },
        { "riverMouthWidth", "Mouth Width" },
        { "riverWidthVariation", "Width Variation" },
        { "riverMeander", "Meander" },
        { "riverMeanderWavelength", "Meander Wavelength" },
        { "riverDepth", "Depth" },
        { "riverValleySlope", "Valley Wall Slope (deg)" },
        { "riverMaxValleyWidth", "Max Valley Half-Width" },
        { "riverBankFreeboard", "Bank Freeboard" },
    };

    // Recommended values for every water tuning field (booleans as 1/0). Sea level and the materials are
    // deliberately not here - see ResetWaterToRecommended.
    private static readonly Dictionary<string, float> WaterRecommended = new Dictionary<string, float>
    {
        { "enableWater", 1f }, { "enableSwimDetection", 1f },
        { "enableOceans", 1f }, { "continentScaleMultiplier", 18f }, { "oceanThreshold", -0.2f }, { "beachWidth", 30f },
        { "beachHeight", 2.5f }, { "coastBlendWidth", 140f }, { "continentalShelfWidth", 260f }, { "oceanDepth", 35f },
        { "inlandRise", 40f }, { "inlandRiseDistance", 3000f }, { "islandFrequency", 0.3f }, { "islandScaleMultiplier", 1.4f },
        { "islandPeakHeight", 14f }, { "spawnLandRadius", 700f },
        { "enableLakes", 1f }, { "lakeSpacing", 900f }, { "lakeChance", 0.35f }, { "lakeMinRadius", 45f }, { "lakeMaxRadius", 130f },
        { "lakeMaxDepth", 10f }, { "lakeMaxSiteSlope", 0.3f }, { "lakeOutletChance", 0.5f },
        { "enablePonds", 1f }, { "pondSpacing", 220f }, { "pondChance", 0.25f }, { "pondMinRadius", 8f }, { "pondMaxRadius", 22f },
        { "pondDepth", 2f }, { "pondMaxSiteSlope", 0.45f },
        { "shoreRimWidth", 12f }, { "shoreFreeboard", 0.6f },
        { "enableRivers", 1f }, { "riverSpacing", 1000f }, { "riverChance", 0.35f }, { "riverMinSpringElevation", 10f },
        { "riverMinLength", 350f }, { "riverMaxLength", 2600f }, { "riverSourceWidth", 5f }, { "riverMouthWidth", 26f },
        { "riverWidthVariation", 0.35f }, { "riverMeander", 0.55f }, { "riverMeanderWavelength", 180f }, { "riverDepth", 3f },
        { "riverValleySlope", 28f }, { "riverMaxValleyWidth", 150f }, { "riverBankFreeboard", 0.8f },
    };

    private static readonly string[] WaterMaterialFields = { "waterMaterial", "oceanMaterial", "lakeMaterial", "pondMaterial", "riverMaterial" };

    private void DrawProp(string field, string label)
    {
        Field(serializedObject.FindProperty(field), label);
    }

    private void DrawWaterGroup(string title, string toggleField, string[,] fields)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;
        bool enabled = true;
        if (toggleField != null)
        {
            DrawProp(toggleField, "Enabled");
            enabled = serializedObject.FindProperty(toggleField).boolValue;
        }
        using (new EditorGUI.DisabledScope(!enabled))
        {
            for (int i = 0; i < fields.GetLength(0); i++)
                DrawProp(fields[i, 0], fields[i, 1]);
        }
        EditorGUI.indentLevel--;
    }

    /// <summary>
    /// Restores every water tuning parameter to its recommended value, WITHOUT touching Sea Level or the
    /// water materials - those are world/art-specific choices (how high the sea sits in this particular
    /// world, what the water should look like), not tuning knobs this preset should silently override.
    /// </summary>
    private void ResetWaterToRecommended()
    {
        foreach (KeyValuePair<string, float> entry in WaterRecommended)
        {
            SerializedProperty property = serializedObject.FindProperty(entry.Key);
            if (property == null)
                continue;
            if (property.propertyType == SerializedPropertyType.Boolean)
                property.boolValue = entry.Value != 0f;
            else
                property.floatValue = entry.Value;
        }
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Restores every water setting, including Sea Level and the water materials, to its original factory
    /// default - a full reset rather than just the tuning parameters (see <see cref="ResetWaterToRecommended"/>).
    /// </summary>
    private void ResetWaterToDefault()
    {
        ResetWaterToRecommended();
        serializedObject.FindProperty("waterLevel").floatValue = 0f;
        foreach (string field in WaterMaterialFields)
            serializedObject.FindProperty(field).objectReferenceValue = null;
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
    /// <summary>
    /// Terrain Shape section: the mode switch, the landform transition/placement settings, and a per-biome
    /// landform picker showing which landform each biome actually ends up using under the current mode.
    /// </summary>
    private void DrawLandformSection(TerrainGenerator generator)
    {
        EditorGUILayout.HelpBox(
            "A biome's landform decides the shape of the ground: Mountains (ranges of connected peaks and ridges " +
            "with valleys), Hills (rounded, rolling), Plains (broad, low swells), Dunes, Wetland, Plateau (flat tops " +
            "with cliff steps and canyons), or Classic (the original terrain). The biome still decides everything " +
            "else: textures, climate, water, objects. Amplitude sets a landform's height, Frequency its feature " +
            "size, Persistence its roughness.\n\n" +
            "Landform terrain is computed from world position and seed only, so it continues seamlessly across " +
            "chunks. At borders the relief (peaks, hills) sinks into foothills before the neighbor begins, and " +
            "mountain fronts rise at most ~50 degrees (other landforms: Boundary Max Walkable Slope).",
            MessageType.Info);

        DrawProp("terrainShapeMode", "Terrain Shape Mode");
        TerrainShapeMode mode = (TerrainShapeMode)serializedObject.FindProperty("terrainShapeMode").enumValueIndex;
        using (new EditorGUI.DisabledScope(mode == TerrainShapeMode.ClassicOnly))
        {
            DrawProp("landformTransitionWidth", "Relief Transition Width");
            DrawProp("mountainBeltStrength", "Mountain Belt Strength");
            using (new EditorGUI.DisabledScope(serializedObject.FindProperty("mountainBeltStrength").floatValue <= 0f))
            {
                DrawProp("mountainBeltScaleMultiplier", "Mountain Belt Scale Multiplier");
                EditorGUILayout.LabelField($"= {generator.MountainBeltScale:0} world units between belts", EditorStyles.miniLabel);
            }
        }

        BiomeInstance[] biomes = generator.BiomeDefinitions;
        if (biomes == null || biomes.Length == 0)
            return;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(new GUIContent("Biome Landforms",
            "Each biome's own Landform setting, and the landform it actually uses under the current Terrain Shape Mode."),
            EditorStyles.miniBoldLabel);

        bool anyClassic = false;
        bool anyMountain = false;
        foreach (BiomeInstance instance in biomes)
        {
            Biome biome = instance != null ? instance.BiomePrefab : null;
            if (biome == null)
                continue;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.IsNullOrEmpty(biome.name) ? "(unnamed)" : biome.name, GUILayout.Width(110));
            EditorGUI.BeginChangeCheck();
            LandformType chosen = (LandformType)EditorGUILayout.EnumPopup(biome.landform, GUILayout.Width(110));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(biome, "Change Biome Landform");
                biome.landform = chosen;
                EditorUtility.SetDirty(biome);
            }

            LandformType effective = LandformGenerator.Effective(biome, mode);
            string note = effective == biome.landform ? ""
                : mode == TerrainShapeMode.ClassicOnly ? "uses Classic (Classic Only mode)"
                : $"uses {effective} (suggested)";
            EditorGUILayout.LabelField(note, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            anyClassic |= biome.landform == LandformType.Classic;
            anyMountain |= effective == LandformType.Mountains || effective == LandformType.Plateau;
        }

        using (new EditorGUI.DisabledScope(!anyClassic))
        {
            if (GUILayout.Button(new GUIContent("Set Classic Biomes To Suggested Landforms",
                "Gives every biome still set to Classic the landform suggested from its settings (hot and dry = Dunes, " +
                "very wet and flat = Wetland, amplitude 35+ = Mountains, 9+ = Hills, otherwise Plains). Biomes that " +
                "already have a landform are left alone. Undoable.")))
            {
                foreach (BiomeInstance instance in biomes)
                {
                    Biome biome = instance != null ? instance.BiomePrefab : null;
                    if (biome == null || biome.landform != LandformType.Classic)
                        continue;
                    Undo.RecordObject(biome, "Set Suggested Landforms");
                    biome.landform = LandformGenerator.Suggest(biome);
                    EditorUtility.SetDirty(biome);
                }
            }
        }

        float regionWidth = generator.VoronoiScale / Mathf.Sqrt(Mathf.Max(1, generator.NumVoronoiPoints));
        if (anyMountain && regionWidth < 180f)
        {
            EditorGUILayout.HelpBox(
                $"Biome regions are only ~{regionWidth:0} world units across. Mountains keep their full height only " +
                "far enough from their border to come down again, so in small territories they stay low. For " +
                "large ranges, raise Voronoi Scale, lower Num Voronoi Points, or raise Cluster Strength / Mountain " +
                "Belt Strength so mountain territories merge.",
                MessageType.Info);
        }
    }

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

        float regionWidth = generator.VoronoiScale / Mathf.Sqrt(Mathf.Max(1, generator.NumVoronoiPoints));
        float blendBand = generator.VoronoiScale * generator.BiomeBlendRange;
        if (blendBand > regionWidth * 0.6f)
        {
            EditorGUILayout.HelpBox(
                $"Height Blend Range is wide for this layout: biome transitions are ~{blendBand:0} world units wide, " +
                $"but biome regions are only ~{regionWidth:0} across. Almost every point is then a mix of several " +
                "biomes, so each biome's own terrain is averaged away and they all look alike (Classic biomes are " +
                $"affected most). Keep it below about {0.5f / Mathf.Sqrt(Mathf.Max(1, generator.NumVoronoiPoints)):0.00}, " +
                "or use Boundary Max Walkable Slope to widen only the borders that need it.",
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
