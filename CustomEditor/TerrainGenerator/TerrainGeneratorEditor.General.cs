using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: terrain size, noise, height range, texture variations, level of detail, biome list and objects sections (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor : Editor
{
    /// <summary>Contents of the "Terrain Configuration" section.</summary>
    private void DrawTerrainConfigSection(TerrainGenerator generator)
    {
        Field(terrainSizeProp, "Terrain Size");
        EditorGUILayout.LabelField($"Resulting chunk size: {generator.ChunkSize} x {generator.ChunkSize} cells", EditorStyles.miniLabel);
    }

    /// <summary>Contents of the "Noise Configuration" section.</summary>
    private void DrawNoiseSection()
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
    }

    /// <summary>Contents of the "Height Range & Texture" section.</summary>
    private void DrawHeightAndTextureSection(TerrainGenerator generator)
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
    }

    /// <summary>Contents of the "Texture Variations" section.</summary>
    private void DrawTextureVariationsSection(TerrainGenerator generator)
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
    }

    /// <summary>Contents of the "Other Configuration" section.</summary>
    private void DrawOtherSection()
    {
        Field(levelOfDetailProp, "Level Of Detail");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Distance Level Of Detail", EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;
        DrawProp("distanceLod", "Enabled");
        using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("distanceLod").boolValue))
        {
            DrawProp("lodFullDetailDistance", "Full Detail Distance");
            DrawProp("lodDistanceStep", "Distance Per Level");
            DrawProp("lodMaxLevel", "Coarsest Level");
            DrawProp("lodSkirtDepth", "Extra Skirt Depth");
        }
        EditorGUI.indentLevel--;
        DrawLodInfo();
    }

    /// <summary>Which level of detail chunks get at which distance, and what that saves.</summary>
    private void DrawLodInfo()
    {
        int baseLevel = levelOfDetailProp.intValue;
        if (!serializedObject.FindProperty("distanceLod").boolValue)
        {
            EditorGUILayout.HelpBox($"Every chunk uses Level Of Detail {baseLevel} whatever its distance.", MessageType.None);
            return;
        }

        float full = Mathf.Max(0f, serializedObject.FindProperty("lodFullDetailDistance").floatValue);
        float step = Mathf.Max(1f, serializedObject.FindProperty("lodDistanceStep").floatValue);
        int coarsest = serializedObject.FindProperty("lodMaxLevel").intValue;
        if (baseLevel >= coarsest)
        {
            EditorGUILayout.HelpBox($"Level Of Detail ({baseLevel}) is already at or past the Coarsest Level ({coarsest}), so distance LOD changes nothing. " +
                                    "Lower Level Of Detail (0-2 is typical with distance LOD) or raise Coarsest Level.", MessageType.Info);
            return;
        }

        var info = new StringBuilder();
        info.Append($"Up to {full:0} units: level {baseLevel} ({Triangles(baseLevel)} triangles per chunk)");
        for (int level = baseLevel + 1; level <= coarsest; level++)
        {
            float from = full + (level - baseLevel - 1) * step;
            info.Append(level < coarsest ? $"\n{from:0}-{from + step:0}: level {level} ({Triangles(level)})" : $"\nBeyond {from:0}: level {level} ({Triangles(level)})");
        }
        info.Append("\n\nDistances are from the viewer to a chunk's nearest edge. Collision, the NavMesh and object placement always use Level Of Detail. " +
                    "Coarser levels only take effect for chunks within EndlessTerrain's Max View Distance - raise it to see further for the same cost.");
        EditorGUILayout.HelpBox(info.ToString(), MessageType.None);
    }

    private string Triangles(int level)
    {
        int factor = level > 0 ? level * 2 : 1;
        TerrainGenerator generator = (TerrainGenerator)target;
        int cells = (generator.ChunkSize) / factor;
        return $"{2 * cells * cells:N0}";
    }

    /// <summary>Contents of the "Biomes" section.</summary>
    private void DrawBiomesSection()
    {
        Field(biomeDefinitionsProp, "Biome Definitions", true);
        if (biomeDefinitionsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No biomes assigned - terrain generation has nothing to draw from and will fail.", MessageType.Error);
        }
    }

    /// <summary>Contents of the "Objects" section.</summary>
    private void DrawObjectsSection()
    {
        Field(shouldSpawnObjectsProp, "Should Spawn Objects");
        using (new EditorGUI.DisabledScope(!shouldSpawnObjectsProp.boolValue))
        {
            EditorGUI.indentLevel++;
            Field(clusterBaseFrequencyProp, "Cluster Base Frequency");
            Field(clusterAmplitudeProp, "Cluster Amplitude");
            EditorGUI.indentLevel--;
        }
    }
}
