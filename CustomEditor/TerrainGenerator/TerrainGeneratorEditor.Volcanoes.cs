using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: the Volcanoes & Calderas section, its reset buttons, readouts and biome setup note (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor : Editor
{
    /// <summary>Contents of the "Volcanoes & Calderas" section.</summary>
    private void DrawVolcanoSection(TerrainGenerator generator)
    {
        EditorGUILayout.HelpBox(
            "Rare, very large landmarks: a stratovolcano (tall cone with a summit crater) or a caldera (a broad " +
            "volcanic massif whose top collapsed into a wide, flat-floored depression with steep stepped walls, " +
            "sometimes with a young cone inside). Both have radial gullies, lava-flow lobes and a wide apron of " +
            "lava plains that buries the land around them. They are part of the base terrain, so rivers run down " +
            "their flanks and lakes can settle in calderas, and they can rise out of the sea as volcanic islands.\n\n" +
            "Give a biome Placement = Volcanic to paint volcanic rock/ash over them.",
            MessageType.Info);
        DrawProp("enableVolcanoes", "Enable Volcanoes");
        using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("enableVolcanoes").boolValue))
        {
            DrawProp("volcanoSpacing", "Spacing (world units)");
            DrawProp("volcanoChance", "Chance per Cell");
            DrawProp("volcanoMinRadius", "Min Radius");
            DrawProp("volcanoMaxRadius", "Max Radius");
            DrawProp("volcanoMinHeight", "Min Height");
            DrawProp("volcanoMaxHeight", "Max Height");
            DrawProp("calderaChance", "Caldera Chance");
            DrawVolcanoInfo(generator);
        }
        DrawVolcanoBiomeStatus(generator);

        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Reset To Recommended", "Restores Spacing, Chance, Min/Max Radius, Min/Max Height and Caldera Chance to their recommended values. Leaves Enable Volcanoes as it is.")))
        {
            ResetVolcanoesToRecommended();
        }
        if (GUILayout.Button(new GUIContent("Reset To Default", "Restores every volcano setting, including Enable Volcanoes (ON), to its original factory default.")))
        {
            ResetVolcanoesToDefault();
        }
        EditorGUILayout.EndHorizontal();
    }

    private static readonly Dictionary<string, float> VolcanoRecommended = new Dictionary<string, float>
    {
        { "volcanoSpacing", 5000f }, { "volcanoChance", 0.35f },
        { "volcanoMinRadius", 450f }, { "volcanoMaxRadius", 1000f },
        { "volcanoMinHeight", 110f }, { "volcanoMaxHeight", 230f },
        { "calderaChance", 0.4f },
    };

    /// <summary>Restores the volcano tuning values, leaving the Enable Volcanoes toggle as it is.</summary>
    private void ResetVolcanoesToRecommended()
    {
        foreach (KeyValuePair<string, float> entry in VolcanoRecommended)
            serializedObject.FindProperty(entry.Key).floatValue = entry.Value;
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>Restores every volcano setting, including the Enable Volcanoes toggle, to its factory default.</summary>
    private void ResetVolcanoesToDefault()
    {
        ResetVolcanoesToRecommended();
        serializedObject.FindProperty("enableVolcanoes").boolValue = true;
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>Plain-language numbers derived from the volcano settings, plus warnings for odd combinations.</summary>
    private void DrawVolcanoInfo(TerrainGenerator generator)
    {
        float spacing = serializedObject.FindProperty("volcanoSpacing").floatValue;
        float chance = serializedObject.FindProperty("volcanoChance").floatValue;
        float minRadius = serializedObject.FindProperty("volcanoMinRadius").floatValue;
        float maxRadius = serializedObject.FindProperty("volcanoMaxRadius").floatValue;
        float minHeight = serializedObject.FindProperty("volcanoMinHeight").floatValue;
        float maxHeight = serializedObject.FindProperty("volcanoMaxHeight").floatValue;
        float caldera = serializedObject.FindProperty("calderaChance").floatValue;

        var info = new StringBuilder();
        if (chance > 0f)
        {
            info.AppendLine($"About one volcano every {spacing / Mathf.Sqrt(chance) / 1000f:0.#} km (one per {spacing * spacing / chance / 1e6f:0} km²); " +
                            $"{caldera * 100f:0}% of them calderas, the rest cones.");
            info.AppendLine($"Each one is {2f * minRadius:0}-{2f * maxRadius:0} units across and {minHeight:0}-{maxHeight:0} units tall above the land around it; " +
                            $"its lava plain reaches {VolcanoFeature.ApronReach * minRadius:0}-{VolcanoFeature.ApronReach * maxRadius:0} units from the center.");
            float gentle = Mathf.Atan(minHeight / Mathf.Max(1f, maxRadius)) * Mathf.Rad2Deg;
            float steep = Mathf.Atan(maxHeight / Mathf.Max(1f, minRadius)) * Mathf.Rad2Deg;
            info.Append($"Average flank slope {gentle:0}-{steep:0}° (steeper near the summit; caldera walls are near-vertical). " +
                        $"The area within {minRadius * VolcanoFeature.ApronReach * 1.15f + 150f:0}-{maxRadius * VolcanoFeature.ApronReach * 1.15f + 150f:0} units of the world origin is kept clear so the spawn is never buried.");
        }
        else
        {
            info.Append("Chance per Cell is 0, so no volcanoes are generated.");
        }
        EditorGUILayout.HelpBox(info.ToString(), MessageType.None);

        if (minRadius > maxRadius || minHeight > maxHeight)
            EditorGUILayout.HelpBox("A Min value is larger than its Max value - sizes are picked between the two, so this effectively uses the Min.", MessageType.Warning);
        if (maxRadius * VolcanoFeature.ApronReach * 1.15f > 0.45f * spacing)
            EditorGUILayout.HelpBox(
                $"Max Radius is large for this Spacing: a volcano plus its lava plain ({maxRadius * VolcanoFeature.ApronReach * 1.15f:0} units) needs about " +
                $"{2.2f * maxRadius * VolcanoFeature.ApronReach * 1.15f:0} units of Spacing to fit in its cell. Volcanoes are kept inside their cell, so they " +
                "will all sit near the middle of their cells (more regular placement). Raise Spacing or lower Max Radius.",
                MessageType.Warning);
        if (maxHeight / Mathf.Max(1f, minRadius) > 0.6f)
            EditorGUILayout.HelpBox("Height is large compared to Radius, so small volcanoes will be very steep spikes. Raise Min Radius or lower Max Height for more natural cones.", MessageType.Info);
        if (generator.EnableErosion && maxHeight > 150f)
            EditorGUILayout.HelpBox("Thermal erosion softens slopes steeper than the Talus Angle, so very tall volcanoes lose some of their crater and caldera-wall sharpness. That's expected.", MessageType.None);
    }

    /// <summary>
    /// Explains what the current biome setup means for volcanoes: whether a Volcanic biome exists to paint
    /// them, and what they look like when none does.
    /// </summary>
    private void DrawVolcanoBiomeStatus(TerrainGenerator generator)
    {
        List<string> volcanic = BiomesWithPlacement(generator, BiomePlacement.Volcanic);
        bool enabled = serializedObject.FindProperty("enableVolcanoes").boolValue;
        if (!enabled)
        {
            if (volcanic.Count > 0)
                EditorGUILayout.HelpBox($"Volcanoes are off, so the Volcanic biome ({string.Join(", ", volcanic)}) is never used.", MessageType.Warning);
            return;
        }
        if (volcanic.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No biome has Placement = Volcanic. Volcanoes are still generated with exactly the same shape (their height " +
                "doesn't depend on biomes), but they are textured, and get objects, from whatever land biomes they stand on - " +
                "e.g. a grassy or snowy volcano, possibly with several biomes' borders crossing it. " +
                "To get lava rock/ash, add a biome, set its Placement to Volcanic and give it rock textures (its height settings are ignored).",
                MessageType.Info);
        }
        else
        {
            string extra = volcanic.Count > 1 ? $" Only the first one ({volcanic[0]}) is used; the others ({string.Join(", ", volcanic.GetRange(1, volcanic.Count - 1))}) are ignored." : "";
            EditorGUILayout.HelpBox(
                $"Volcanic biome: {volcanic[0]}. It is painted over each volcano and most of its lava plain, fading out at an " +
                "irregular edge, and its objects spawn there. Its height/landform settings are ignored - the volcano shapes the ground." + extra,
                MessageType.None);
        }
    }
}
