using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: small pictures and explanations inside the sections - the noise layers, the Voronoi
// region size, the tri-planar slope curve, the biome textures and what each texture variation affects
// (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor
{
    private Texture2D noiseSingleLayer, noiseAllLayers;
    private int noiseCacheOctaves = -1;
    private float noiseCacheLacunarity = -1f;

    /// <summary>Two squares of the same noise: its first layer only, and every Octaves layer together.</summary>
    private void DrawNoisePreview()
    {
        int octaves = Mathf.Clamp(octavesProp.intValue, 1, 12);
        float lacunarity = Mathf.Max(1f, lacunarityProp.floatValue);
        if (noiseAllLayers == null || noiseCacheOctaves != octaves || !Mathf.Approximately(noiseCacheLacunarity, lacunarity))
        {
            noiseSingleLayer = NoiseTexture(noiseSingleLayer, 1, lacunarity);
            noiseAllLayers = NoiseTexture(noiseAllLayers, octaves, lacunarity);
            noiseCacheOctaves = octaves;
            noiseCacheLacunarity = lacunarity;
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("What the layers add (top-down, light = high)", EditorStyles.miniBoldLabel);
        const float size = 110f;
        Rect row = GUILayoutUtility.GetRect(size * 2f + 12f, size + 16f, GUILayout.ExpandWidth(false));
        row.x += EditorGUI.indentLevel * 15f;
        EditorGUI.DrawPreviewTexture(new Rect(row.x, row.y, size, size), noiseSingleLayer);
        EditorGUI.DrawPreviewTexture(new Rect(row.x + size + 12f, row.y, size, size), noiseAllLayers);
        EditorGUI.LabelField(new Rect(row.x, row.y + size, size, 16f), "1 layer", EditorStyles.centeredGreyMiniLabel);
        EditorGUI.LabelField(new Rect(row.x + size + 12f, row.y + size, size, 16f), $"{octaves} layers", EditorStyles.centeredGreyMiniLabel);

        float finest = Mathf.Pow(lacunarity, octaves - 1);
        EditorGUILayout.LabelField(
            $"Each layer's bumps are {lacunarity:0.##}x smaller than the layer before, so the last layer's are {finest:0.#}x smaller than the first. " +
            "More octaves = more fine detail (and a little more generation time). Drawn with a persistence of 0.5; each biome's own persistence sets how strong the small layers are.",
            EditorStyles.wordWrappedMiniLabel);
    }

    private static Texture2D NoiseTexture(Texture2D texture, int octaves, float lacunarity)
    {
        const int n = 96;
        if (texture == null)
            texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[n * n];
        float min = float.MaxValue, max = float.MinValue;
        var values = new float[n * n];
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                float value = 0f, amplitude = 1f, frequency = 3f / n;
                for (int o = 0; o < octaves; o++)
                {
                    value += (Mathf.PerlinNoise(i * frequency + 13.7f + o * 31.1f, j * frequency + 7.3f + o * 17.9f) - 0.5f) * amplitude;
                    amplitude *= 0.5f;
                    frequency *= lacunarity;
                }
                values[j * n + i] = value;
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
            }
        }
        for (int k = 0; k < values.Length; k++)
        {
            float t = Mathf.InverseLerp(min, max, values[k]);
            pixels[k] = Color.Lerp(new Color(0.12f, 0.16f, 0.1f), new Color(0.92f, 0.9f, 0.82f), t);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>How big the biome regions come out, in world units and chunks.</summary>
    private void DrawVoronoiInfo()
    {
        TerrainGenerator generator = (TerrainGenerator)target;
        int points = Mathf.Max(1, numVoronoiPointsProp.intValue);
        float scale = Mathf.Max(1f, voronoiScaleProp.floatValue);
        float region = scale / Mathf.Sqrt(points);
        float chunk = Mathf.Max(1, generator.ChunkSize - 1);
        EditorGUILayout.LabelField(
            $"The world is cut into {scale:0} x {scale:0} unit cells with {points} biome point{(points == 1 ? "" : "s")} each, so one biome region is about " +
            $"{region:0} units across ({region / chunk:0.#} chunks). Neighbouring regions that pick the same biome (Natural Biome Placement > Cluster Strength) " +
            "join into larger areas. Fewer points or a larger scale = larger biomes.",
            EditorStyles.wordWrappedMiniLabel);

        // A small sketch of the cells and their points, to the same proportions.
        const float size = 90f;
        Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
        rect.x += EditorGUI.indentLevel * 15f;
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
        int grid = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(points)));
        var random = new System.Random(voronoiSeedProp.intValue);
        for (int cell = 0; cell < 4; cell++)
        {
            float cx = rect.x + (cell % 2) * size * 0.5f, cy = rect.y + (cell / 2) * size * 0.5f;
            EditorGUI.DrawRect(new Rect(cx, cy, size * 0.5f, 1f), new Color(0.5f, 0.5f, 0.5f));
            EditorGUI.DrawRect(new Rect(cx, cy, 1f, size * 0.5f), new Color(0.5f, 0.5f, 0.5f));
            for (int p = 0; p < Mathf.Min(points, grid * grid); p++)
            {
                float sub = size * 0.5f / grid;
                float x = cx + (p % grid) * sub + (float)random.NextDouble() * sub;
                float y = cy + (p / grid) * sub + (float)random.NextDouble() * sub;
                EditorGUI.DrawRect(new Rect(x - 1.5f, y - 1.5f, 3f, 3f), Color.HSVToRGB((float)random.NextDouble(), 0.6f, 0.95f));
            }
        }
        EditorGUI.LabelField(new Rect(rect.xMax + 8f, rect.y, 260f, 16f), $"4 cells of {scale:0} units, {points} points each", EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(rect.xMax + 8f, rect.y + 16f, 260f, 16f), "Each point becomes one biome region", EditorStyles.miniLabel);
    }

    /// <summary>Graph of how much the sides project onto the ground at each slope.</summary>
    private void DrawTriplanarCurve(TerrainGenerator generator)
    {
        float strength = serializedObject.FindProperty("triplanarStrength").floatValue;
        float a = serializedObject.FindProperty("triplanarSlopeStart").floatValue;
        float b = serializedObject.FindProperty("triplanarSlopeEnd").floatValue;
        float start = Mathf.Min(a, b), end = Mathf.Max(Mathf.Max(a, b), Mathf.Min(a, b) + 0.01f);

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField(new GUIContent("Side Projection By Slope", "How much of the texture comes from the side projections (tri-planar) on ground of each steepness. At 0 the texture is only projected from above, which stretches it on steep ground."), EditorStyles.miniBoldLabel);
        Rect rect = GUILayoutUtility.GetRect(200f, 60f, GUILayout.ExpandWidth(true));
        rect.xMin += EditorGUI.indentLevel * 15f + 24f;
        rect.xMax -= 8f;
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));
        for (int deg = 15; deg < 90; deg += 15)
            EditorGUI.DrawRect(new Rect(rect.x + rect.width * deg / 90f, rect.y, 1f, rect.height), new Color(0.25f, 0.25f, 0.25f));

        int columns = Mathf.Max(1, Mathf.FloorToInt(rect.width / 2f));
        for (int c = 0; c < columns; c++)
        {
            float slope = 90f * (c + 0.5f) / columns;
            float amount = strength * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, slope));
            float h = rect.height * Mathf.Clamp01(amount);
            EditorGUI.DrawRect(new Rect(rect.x + c * rect.width / columns, rect.yMax - h, Mathf.Ceil(rect.width / columns), h), new Color(1f, 0.55f, 0.1f, 0.85f));
        }
        EditorGUI.DrawRect(new Rect(rect.x + rect.width * start / 90f, rect.y, 1f, rect.height), Color.white);
        EditorGUI.DrawRect(new Rect(rect.x + rect.width * Mathf.Min(end, 90f) / 90f, rect.y, 1f, rect.height), Color.white);

        EditorGUI.LabelField(new Rect(rect.x - 24f, rect.y - 2f, 24f, 14f), "all", EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(rect.x - 24f, rect.yMax - 12f, 24f, 14f), "top", EditorStyles.miniLabel);
        Rect axis = GUILayoutUtility.GetRect(200f, 14f, GUILayout.ExpandWidth(true));
        axis.xMin = rect.xMin;
        axis.xMax = rect.xMax;
        for (int deg = 0; deg <= 90; deg += 15)
            EditorGUI.LabelField(new Rect(axis.x + axis.width * deg / 90f - 10f, axis.y, 30f, 14f), deg + "°", EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            $"Ground under {start:0}° uses the top projection only; from {start:0}° the sides fade in, reaching {Mathf.Clamp01(strength) * 100f:0}% at {end:0}°. " +
            "Walls and cliffs (70-90°) look best fully tri-planar. The Slope view of the World Preview shows how much of your world is that steep.",
            EditorStyles.wordWrappedMiniLabel);
    }

    /// <summary>The biome textures in the shared texture array, with how large one repeat is in the world.</summary>
    private void DrawBiomeTextureStrip(TerrainGenerator generator)
    {
        if (generator.BiomeDefinitions == null || generator.BiomeDefinitions.Length == 0)
            return;
        EditorGUILayout.Space(2);
        float repeat = serializedObject.FindProperty("terrainTextureSize").floatValue;
        if (repeat <= 0f)
            repeat = generator.TerrainTextureWorldSize;
        EditorGUILayout.LabelField(new GUIContent($"Biome Textures (one repeat = {repeat:0.#} world units)",
            "Each biome's ground texture as the terrain shader receives it (the textures come from the Biome assets). Click one to select its texture asset."), EditorStyles.miniBoldLabel);

        const float size = 52f;
        float width = EditorGUIUtility.currentViewWidth - 50f - EditorGUI.indentLevel * 15f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt(width / (size + 6f)));
        int count = generator.BiomeDefinitions.Length;
        for (int start = 0; start < count; start += perRow)
        {
            Rect row = GUILayoutUtility.GetRect(width, size + 14f, GUILayout.ExpandWidth(false));
            row.x += EditorGUI.indentLevel * 15f;
            for (int k = start; k < Mathf.Min(count, start + perRow); k++)
            {
                Biome biome = generator.BiomeDefinitions[k]?.BiomePrefab;
                Rect box = new Rect(row.x + (k - start) * (size + 6f), row.y, size, size);
                if (biome == null || biome.texture == null)
                {
                    EditorGUI.DrawRect(box, new Color(0.35f, 0.1f, 0.1f));
                    EditorGUI.LabelField(box, new GUIContent("none", biome == null ? "No Biome asset." : $"{biome.name} has no texture."), EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    EditorGUI.DrawPreviewTexture(box, biome.texture);
                    string tip = $"{biome.name}: {biome.texture.name}, {biome.texture.width} x {biome.texture.height} {biome.texture.format}";
                    if (GUI.Button(box, new GUIContent("", tip), GUIStyle.none))
                        EditorGUIUtility.PingObject(biome.texture);
                }
                EditorGUI.LabelField(new Rect(box.x - 3f, box.yMax, size + 6f, 14f), biome != null ? biome.name : "-", EditorStyles.centeredGreyMiniLabel);
            }
        }
    }

    /// <summary>Which of the texture variation settings reach the terrain shader in use, and how big the variations are.</summary>
    private void DrawTextureVariationInfo(TerrainGenerator generator)
    {
        var mode = (TerrainShaderMode)serializedObject.FindProperty("terrainShader").enumValueIndex;
        bool package = mode != TerrainShaderMode.ProjectShader;
        var info = new StringBuilder();
        info.Append(package
            ? "The package tri-planar shader is in use (Terrain Material section). It projects textures in world space, so:\n" +
              "• UV Noise Offset and the Shader-Based Enhancements apply.\n" +
              "• UV Rotation and Texture Scale Variation change the mesh's own UVs, which this shader doesn't read - use Shader UV Rotation Strength and Shader UV Scale Variation instead."
            : "The project's own splat-map shader is in use (Terrain Material section). It reads the mesh's UVs, so:\n" +
              "• UV Rotation, UV Noise Offset and Texture Scale Variation apply (they are baked into the mesh).\n" +
              "• The Shader-Based Enhancements apply only if that shader has the matching properties.");

        if (enableUVNoiseProp.boolValue && generator.EnableTextureVariations)
        {
            float scale = Mathf.Max(0.0001f, uvNoiseScaleProp.floatValue);
            info.Append($"\n\nUV noise: the texture shifts by up to {uvNoiseStrengthProp.floatValue * 0.5f:0.##} of a repeat, in waves about {1f / scale:0.#} world units wide.");
        }
        EditorGUILayout.HelpBox(info.ToString(), MessageType.None);
    }
}
