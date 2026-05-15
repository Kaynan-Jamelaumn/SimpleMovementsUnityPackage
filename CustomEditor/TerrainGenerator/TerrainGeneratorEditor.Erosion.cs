using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: the thermal, hydraulic and debug-visualization erosion sections (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor : Editor
{
    /// <summary>Contents of the "Erosion - Thermal (Slopes/Talus)" section.</summary>
    private void DrawErosionThermalSection()
    {
        Field(enableErosionProp, "Enable Erosion (master, thermal + hydraulic)");
        using (new EditorGUI.DisabledScope(!enableErosionProp.boolValue))
        {
            EditorGUI.indentLevel++;
            Field(erosionPaddingProp, "Erosion Padding (cells)");
            DrawProp("seamlessErosion", "Seamless Erosion");
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
    }

    /// <summary>Contents of the "Erosion - Hydraulic (Water/Droplets)" section.</summary>
    private void DrawErosionHydraulicSection(TerrainGenerator generator)
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
        DrawErosionInfo(generator);
    }

    /// <summary>Contents of the "Erosion Debug Visualization" section.</summary>
    private void DrawErosionDebugSection()
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
    }

    private static void DrawErosionLegendSwatch(string label, Color color)
    {
        Rect line = EditorGUILayout.GetControlRect(false, 16f);
        Rect swatch = new Rect(line.x + EditorGUI.indentLevel * 15f, line.y, 16f, 16f);
        EditorGUI.DrawRect(swatch, color);
        Rect text = new Rect(swatch.xMax + 6f, line.y, line.width - swatch.width - 6f, line.height);
        EditorGUI.LabelField(text, label);
    }

    private void ResetErosionToRecommended()
    {
        enableErosionProp.boolValue = true;
        erosionPaddingProp.intValue = 40;
        serializedObject.FindProperty("seamlessErosion").boolValue = true;
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

    /// <summary>What the erosion settings cost and imply, with warnings for combinations that cause seams.</summary>
    private void DrawErosionInfo(TerrainGenerator generator)
    {
        if (!enableErosionProp.boolValue)
        {
            EditorGUILayout.HelpBox("Erosion is off: terrain keeps its raw shapes (sharper, noisier), and chunks generate roughly 40% faster.", MessageType.None);
            return;
        }
        int chunk = generator.ChunkSize;
        int padding = erosionPaddingProp.intValue;
        int padded = chunk + 2 * padding;
        float density = hydraulicDropletDensityProp.floatValue;
        long droplets = (long)(padded * (long)padded * density);
        float overhead = (float)padded * padded / Mathf.Max(1, chunk * chunk);
        EditorGUILayout.HelpBox(
            $"Per chunk: {padded} x {padded} cells are eroded ({overhead:0.#}x the chunk area, because of the {padding}-cell padding), " +
            $"with about {droplets:N0} droplets of up to {dropletLifetimeProp.intValue} steps each, and {thermalIterationsProp.intValue} thermal passes. " +
            "Droplets, lifetime and padding are what cost the most time.\n\n" +
            $"Slopes steeper than {talusAngleProp.floatValue:0}° slide (per biome, Erosion Resistance raises or lowers this). " +
            "Erosion runs after sea cliffs, sea stacks, caldera walls, waterfall lips and Highlands ledges are built, so those rock " +
            "faces get softened a little (more with a low Talus Angle). Only the water guarantees (closed shores, river banks, " +
            "coastline above the sea) are re-applied after erosion.",
            MessageType.None);
        if (padding < dropletLifetimeProp.intValue)
            EditorGUILayout.HelpBox(
                $"Erosion Padding ({padding}) is smaller than Droplet Lifetime ({dropletLifetimeProp.intValue}): droplets can travel from outside the padded " +
                "area into the chunk, so neighbouring chunks erode their shared border differently and small steps (seams) can appear. " +
                "Raise the padding to at least the lifetime.",
                MessageType.Warning);
    }
}
