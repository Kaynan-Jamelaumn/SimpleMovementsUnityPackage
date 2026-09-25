using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: the Voronoi grid, Natural Biome Placement and Climate sections, and the biome climate summary (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor : Editor
{
    /// <summary>Contents of the "Voronoi / Biome Grid" section.</summary>
    private void DrawVoronoiSection()
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
    }

    /// <summary>Contents of the "Natural Biome Placement" section.</summary>
    private void DrawNaturalPlacementSection(TerrainGenerator generator)
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
        using (new EditorGUI.DisabledScope(!useBiomeBlendedTexturingProp.boolValue))
        {
            EditorGUI.indentLevel++;
            DrawProp("splatTexturesPerPixel", "Textures Per Pixel");
            EditorGUI.indentLevel--;
        }
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
        if (GUILayout.Button(new GUIContent("Reset To Recommended", "Restores Cluster Strength, Cluster Radius Multiplier, Repeat Penalty, Border Warp Strength/Scale, Blend Range, Blend Texturing, Textures Per Pixel and Boundary Max Walkable Slope to their recommended default values.")))
        {
            ResetBiomePlacementToRecommended();
        }
        if (GUILayout.Button(new GUIContent("Turn Off", "Disables natural biome placement's effect: sets Cluster Strength, Repeat Penalty and Border Warp Strength to 0 (their 'fully disabled' value per the code's own design - see tooltips) and Blend Range and Boundary Max Walkable Slope to 0 (hard biome borders, no smoothing). Cluster Radius Multiplier, Warp Scale Multiplier and Blend Texturing are left as-is since they have no effect once their associated strength is 0.")))
        {
            DisableBiomePlacement();
        }
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>Contents of the "Climate (Temperature & Moisture)" section.</summary>
    private void DrawClimateSection(TerrainGenerator generator)
    {
        Field(useNaturalClimatePlacementProp, "Use Natural Climate Placement");
        using (new EditorGUI.DisabledScope(!useNaturalClimatePlacementProp.boolValue))
        {
            EditorGUI.indentLevel++;
            Field(climateScaleMultiplierProp, "Climate Scale Multiplier");
            EditorGUILayout.LabelField($"= {generator.ClimateNoiseScale:0.#} world units", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
            DrawClimateInfo(generator);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Terrain Influence (rain shadows, altitude, coasts)", EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;
        DrawProp("terrainAwareClimate", "Terrain-Aware Climate");
        using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("terrainAwareClimate").boolValue))
        {
            DrawProp("prevailingWindAngle", "Prevailing Wind (toward, degrees)");
            DrawProp("rainShadowStrength", "Rain Shadow");
            DrawProp("altitudeCooling", "Altitude Cooling");
            DrawProp("coastalMoisture", "Coastal Moisture");
        }
        EditorGUI.indentLevel--;
        DrawTerrainClimateInfo(generator);

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
        if (GUILayout.Button(new GUIContent("Reset To Recommended", "Restores Use Natural Climate Placement (ON), Climate Scale Multiplier and the Terrain Influence settings to their recommended default values.")))
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
            "Rainfall Multiplier), Landform, Placement (Land / Ocean / Volcanic) and noise shape (Amplitude, Frequency, Persistence) with a recommended " +
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
        serializedObject.FindProperty("splatTexturesPerPixel").intValue = 4;
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

    private void ResetClimateToRecommended()
    {
        useNaturalClimatePlacementProp.boolValue = true;
        climateScaleMultiplierProp.floatValue = 6f;
        serializedObject.FindProperty("terrainAwareClimate").boolValue = true;
        serializedObject.FindProperty("prevailingWindAngle").floatValue = 0f;
        serializedObject.FindProperty("rainShadowStrength").floatValue = 0.6f;
        serializedObject.FindProperty("altitudeCooling").floatValue = 0.5f;
        serializedObject.FindProperty("coastalMoisture").floatValue = 0.4f;
        serializedObject.ApplyModifiedProperties();
    }

    private void DisableClimate()
    {
        useNaturalClimatePlacementProp.boolValue = false;
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>What the terrain's influence on the climate will do with the current settings.</summary>
    private void DrawTerrainClimateInfo(TerrainGenerator generator)
    {
        if (!serializedObject.FindProperty("terrainAwareClimate").boolValue)
        {
            EditorGUILayout.HelpBox("Terrain influence is off: temperature and moisture are pure noise (plus the latitude pull).", MessageType.None);
            return;
        }

        TerrainClimate climate = TerrainClimate.From(generator);
        var info = new StringBuilder();
        float angle = serializedObject.FindProperty("prevailingWindAngle").floatValue;
        string[] compass = { "east (+X)", "north-east", "north (+Z)", "north-west", "west (-X)", "south-west", "south (-Z)", "south-east" };
        string toward = compass[Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 45f) % 8];
        if (climate != null && climate.BeltInfluence > 0f)
        {
            info.AppendLine($"Mountain belts (about every {generator.MountainBeltScale / 1000f:0.#} km) cast rain shadows: the wind blows toward the {toward}, " +
                            $"so the {toward} side of each range is drier, for about {0.3f * generator.MountainBeltScale / 1000f:0.#} km, and the side facing the wind wetter. " +
                            "Mountains themselves are colder, which favors cold biomes on them.");
        }
        else
        {
            info.AppendLine("No rain shadows or mountain cooling: they follow the mountain belts, which only gather real mountains when the " +
                            "Terrain Shape mode uses landforms, Mountain Belt Strength is above 0 and at least one land biome has a Mountains, Glacial or Plateau landform.");
        }
        if (climate != null && climate.Oceans != null)
            info.AppendLine("Coasts are wetter and deep continental interiors drier; the land that rises inland (Inland Rise) is a little colder.");
        else
            info.AppendLine("No coastal effects (oceans are off).");
        info.Append(useNaturalClimatePlacementProp.boolValue
            ? "Applies to climate-based biome placement and to how much rain erodes the terrain."
            : "Climate placement is off, so this only changes how much rain erodes the terrain.");
        EditorGUILayout.HelpBox(info.ToString(), MessageType.None);
    }

    /// <summary>What the climate settings mean in world terms.</summary>
    private void DrawClimateInfo(TerrainGenerator generator)
    {
        float scale = generator.ClimateNoiseScale;
        EditorGUILayout.HelpBox(
            $"Warm and cold / wet and dry patches are roughly {scale / 1000f:0.#}-{2f * scale / 1000f:0.#} km across " +
            $"(about {scale / Mathf.Max(1f, generator.VoronoiScale):0.#} biome regions). On top of that, temperature drifts " +
            $"warmer toward world Y = 0 and colder away from it, reaching its coldest {4f * scale / 1000f:0.#} km north or south " +
            "(a gentle 18% pull, so noise still dominates locally).\n\n" +
            "A biome's Ideal Temperature/Moisture (0-1) is where it is most likely: 0.5/0.5 is average, 0.85 temperature is " +
            "hot, 0.15 is cold, 0.1 moisture is desert-dry. Biomes whose ideals are far from every other biome's get fewer, " +
            "more distinct regions; biomes with identical ideals compete on Weight only. Ocean biomes use the same climate.",
            MessageType.None);
    }
}
