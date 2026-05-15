using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: the Water section - oceans, coasts, lakes, ponds, rivers and waterfalls (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor : Editor
{
    /// <summary>Contents of the "Water (Oceans, Lakes, Ponds, Rivers)" section.</summary>
    private void DrawWaterSection(TerrainGenerator generator)
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
            EditorGUILayout.LabelField(new GUIContent("Materials (optional)", "Leave empty to use the package's own water shader (SimpleMovements/Water: depth colour, waves, flowing ripples and foam; URP and Built-in) with a colour per water type. Under HDRP, or if that shader is missing, a plain tinted transparent material is used instead."), EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            DrawProp("waterMaterial", "Default");
            DrawProp("oceanMaterial", "Ocean");
            DrawProp("lakeMaterial", "Lake");
            DrawProp("pondMaterial", "Pond");
            DrawProp("riverMaterial", "River");
            DrawProp("waterfallMaterial", "Waterfall");
            EditorGUI.indentLevel--;

            DrawWaterGroup("Oceans", "enableOceans", WaterOceanFields);
            DrawOceanBiomeStatus(generator);
            using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("enableOceans").boolValue))
                DrawWaterGroup("Coasts (cliffs & sea stacks)", null, WaterCoastFields);
            EditorGUILayout.LabelField($"Continent scale = {generator.ContinentScale:0} world units", EditorStyles.miniLabel);
            DrawWaterGroup("Lakes", "enableLakes", WaterLakeFields);
            DrawWaterGroup("Ponds", "enablePonds", WaterPondFields);
            DrawWaterGroup("Shorelines (lakes & ponds)", null, WaterShoreFields);
            DrawWaterGroup("Rivers", "enableRivers", WaterRiverFields);
            using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("enableRivers").boolValue))
            {
                DrawWaterGroup("Waterfalls", "enableWaterfalls", WaterWaterfallFields);
                DrawWaterGroup("River Junctions & Bends", null, WaterJunctionFields);
            }
            DrawWaterGroup("Wetness & Snowmelt", null, WaterWetnessFields);

            DrawWaterInfo(generator);

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

    private static readonly string[,] WaterCoastFields =
    {
        { "coastCliffFrequency", "Cliff Frequency" },
        { "coastCliffHeight", "Cliff Height" },
        { "coastCliffTerraces", "Cliff Terraces" },
        { "seaStackChance", "Sea Stack Chance" },
        { "seaStackSpacing", "Sea Stack Spacing" },
        { "seaStackMaxHeight", "Sea Stack Max Height" },
    };

    private static readonly string[,] WaterWaterfallFields =
    {
        { "waterfallMinDrop", "Min Drop" },
        { "waterfallTierHeight", "Max Tier Height" },
    };

    private static readonly string[,] WaterJunctionFields =
    {
        { "enableRiverJunctions", "River Junctions" },
        { "enableMeanderCutoffs", "Meander Cutoffs & Level Bends" },
    };

    private static readonly string[,] WaterWetnessFields =
    {
        { "wetnessDistance", "Wet Ground Distance" },
        { "wetnessHeight", "Wet Ground Height" },
        { "snowLineHeight", "Snow Line Height" },
        { "snowmeltSprings", "Snowmelt Springs" },
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
        { "coastCliffFrequency", 0.35f }, { "coastCliffHeight", 26f }, { "coastCliffTerraces", 0.5f },
        { "seaStackChance", 0.3f }, { "seaStackSpacing", 220f }, { "seaStackMaxHeight", 30f },
        { "enableWaterfalls", 1f }, { "waterfallMinDrop", 4f }, { "waterfallTierHeight", 12f },
        { "enableRiverJunctions", 1f }, { "enableMeanderCutoffs", 1f },
        { "wetnessDistance", 14f }, { "wetnessHeight", 4f }, { "snowLineHeight", 90f }, { "snowmeltSprings", 1f },
    };

    private static readonly string[] WaterMaterialFields = { "waterMaterial", "oceanMaterial", "lakeMaterial", "pondMaterial", "riverMaterial", "waterfallMaterial" };

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

    /// <summary>
    /// Explains what the current biome setup means for oceans: whether Ocean biomes exist to shape and paint
    /// the seafloor, and what the seafloor looks like when none do.
    /// </summary>
    private void DrawOceanBiomeStatus(TerrainGenerator generator)
    {
        List<string> ocean = BiomesWithPlacement(generator, BiomePlacement.Ocean);
        if (!serializedObject.FindProperty("enableOceans").boolValue)
        {
            if (ocean.Count > 0)
                EditorGUILayout.HelpBox($"Oceans are off, so the Ocean biomes ({string.Join(", ", ocean)}) are never used.", MessageType.Warning);
            return;
        }
        if (ocean.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No biome has Placement = Ocean. Oceans still generate: the seafloor is a plain shelf sloping down to Ocean Depth, " +
                "with gentle bumps copied from the land biome above it, and it keeps the texture of whatever land biome's region " +
                "it falls in - so you may see desert or grass under the water. Coasts, cliffs, sea stacks and islands still work.\n\n" +
                "For a proper seabed, add biomes with Placement = Ocean and a Sea landform (Deep Plain, Ravines, Coral Reef, Rocky " +
                "Seabed - the Ocean presets set this up). They form their own layout under the sea, in regions about 2.5x larger " +
                "than land biome regions, shape the seafloor and take over texturing within about 10 units of the waterline.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"Ocean biomes: {string.Join(", ", ocean)}. They shape and texture the seafloor in their own layout under the sea " +
                "and take over from the land biomes within about 10 units of the waterline. Their Weight and Ideal Temperature/Moisture " +
                "decide which ocean biome goes where, the same way as for land biomes.",
                MessageType.None);
        }
    }

    /// <summary>What the water settings mean in world terms.</summary>
    private void DrawWaterInfo(TerrainGenerator generator)
    {
        var info = new StringBuilder();
        if (serializedObject.FindProperty("enableOceans").boolValue)
        {
            float threshold = serializedObject.FindProperty("oceanThreshold").floatValue;
            // The continent field is roughly normally distributed (spread ~0.19): -0.2 gives ~15% sea, 0 gives ~50%.
            float z = threshold / 0.19f;
            float oceanShare = 1f / (1f + Mathf.Exp(-1.702f * z));
            float cliffs = serializedObject.FindProperty("coastCliffFrequency").floatValue;
            info.AppendLine($"Oceans: continents and seas are roughly {generator.ContinentScale / 1000f:0.#}-{2f * generator.ContinentScale / 1000f:0.#} km across; " +
                            $"about {oceanShare * 100f:0}% of the world is sea (rough estimate). Coasts: about {Mathf.Min(100f, cliffs * 100f):0}% cliffs or rocky shore, the rest beaches.");
        }
        if (serializedObject.FindProperty("enableLakes").boolValue)
            info.AppendLine(Density("Lakes", "lakeSpacing", "lakeChance"));
        if (serializedObject.FindProperty("enablePonds").boolValue)
            info.AppendLine(Density("Ponds", "pondSpacing", "pondChance"));
        if (serializedObject.FindProperty("enableRivers").boolValue)
            info.AppendLine(Density("River springs", "riverSpacing", "riverChance") + " (many are discarded as too short, and lake outlets add more)");
        info.Append("These are upper bounds: every candidate is then scaled by the biome's likelihood and rejected on unsuitable ground " +
                    "(too steep, too low, under the sea), so the real count is lower.");
        EditorGUILayout.HelpBox(info.ToString(), MessageType.None);
    }

    private string Density(string label, string spacingField, string chanceField)
    {
        float spacing = serializedObject.FindProperty(spacingField).floatValue;
        float chance = serializedObject.FindProperty(chanceField).floatValue;
        if (chance <= 0f || spacing <= 0f)
            return $"{label}: none (chance is 0).";
        return $"{label}: at most about {chance * 1e6f / (spacing * spacing):0.#} per km² (one every {spacing / Mathf.Sqrt(chance):0} units)";
    }
}
