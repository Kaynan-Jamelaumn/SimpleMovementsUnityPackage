using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: the World Preview - a top-down map of the world drawn in the inspector (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor
{
    private enum PreviewMode { Combined, Height, Biomes, Water, Climate }

    private static readonly int[] PreviewResolutions = { 128, 256, 384, 512 };
    private static readonly string[] PreviewResolutionLabels = { "128 x 128 (fast)", "256 x 256", "384 x 384", "512 x 512 (slow)" };

    private bool showPreview = true;
    private PreviewMode previewMode = PreviewMode.Combined;
    private Vector2 previewCenter = Vector2.zero;
    private float previewSize = 4000f;
    private int previewResolutionIndex = 1;
    private Texture2D previewTexture;
    private PreviewData previewData;

    /// <summary>What a preview sampled, kept so switching the view mode only re-colors it.</summary>
    private sealed class PreviewData
    {
        public int Resolution;
        public float Step;
        public Vector2 Min;
        public float[] Height;
        public int[] Biome;          // index into Biomes, -1 = none
        public byte[] Water;         // WaterBodyType (0 = dry)
        public float[] Temperature;
        public float[] Moisture;
        public List<Biome> Biomes = new List<Biome>();
        public Color[] BiomeColors;
        public int[] BiomeCounts;
        public float SeaLevel;
        public float MinHeight, MaxHeight;
        public int Lakes, Ponds, Rivers;
        public long Milliseconds;
    }

    /// <summary>Contents of the "World Preview" section.</summary>
    private void DrawPreviewSection(TerrainGenerator generator)
    {
        EditorGUILayout.HelpBox(
            "A top-down map of the world from the current settings - heights, biomes, oceans, lakes, ponds, rivers and volcanoes - " +
            "without entering Play mode. It samples the terrain before erosion (erosion only changes small-scale detail), " +
            "one sample per pixel, so very small features can fall between pixels. North (+Z) is up, east (+X) to the right.",
            MessageType.None);

        previewCenter = EditorGUILayout.Vector2Field(new GUIContent("Center (world X, Z)", "World position at the middle of the map. 0,0 is the usual spawn point."), previewCenter);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Use Scene View Position", "Centers the map on where the Scene view camera is looking.")))
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
                previewCenter = new Vector2(view.pivot.x, view.pivot.z);
        }
        if (GUILayout.Button(new GUIContent("Reset To Origin", "Centers the map on world 0,0.")))
            previewCenter = Vector2.zero;
        EditorGUILayout.EndHorizontal();
        previewSize = EditorGUILayout.Slider(new GUIContent("Area Size (world units)", "Width and height of the area shown. Larger areas show more of the world, with less detail per pixel."), previewSize, 500f, 30000f);
        previewResolutionIndex = EditorGUILayout.Popup(new GUIContent("Resolution", "Pixels per side. Time grows with the square of this."), previewResolutionIndex, ToContents(PreviewResolutionLabels));
        PreviewMode mode = (PreviewMode)EditorGUILayout.EnumPopup(new GUIContent("Show", "Combined: biome colours with hill shading and water. Height: elevation (dark = low, light = high). Biomes: the biome layout. Water: every water body by type. Climate: temperature (red = hot, blue = cold) and moisture (brighter green = wetter), including the terrain's influence."), previewMode);
        if (mode != previewMode)
        {
            previewMode = mode;
            if (previewData != null)
                Colorize();
        }

        float pixelSize = previewSize / PreviewResolutions[previewResolutionIndex];
        EditorGUILayout.LabelField($"{previewSize / 1000f:0.#} x {previewSize / 1000f:0.#} km, one pixel = {pixelSize:0.#} world units (a chunk is {generator.ChunkSize - 1} units)", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Generate Preview", "Samples the world with the current settings and draws the map. Can be cancelled from the progress bar.")))
            GeneratePreview(generator);
        using (new EditorGUI.DisabledScope(previewTexture == null))
        {
            if (GUILayout.Button(new GUIContent("Clear", "Removes the preview image."), GUILayout.Width(60)))
                ClearPreview();
        }
        EditorGUILayout.EndHorizontal();

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("In Play mode the preview uses the running world's caches, so settings changed since the world started may not show until you press Clear Voronoi / Biome Cache.", MessageType.None);
        }

        if (previewTexture == null || previewData == null)
            return;

        float width = Mathf.Min(EditorGUIUtility.currentViewWidth - 40f, 512f);
        Rect rect = GUILayoutUtility.GetRect(width, width, GUILayout.ExpandWidth(false));
        EditorGUI.DrawPreviewTexture(rect, previewTexture);
        DrawPreviewLegend();
    }

    private static GUIContent[] ToContents(string[] labels)
    {
        var contents = new GUIContent[labels.Length];
        for (int i = 0; i < labels.Length; i++)
            contents[i] = new GUIContent(labels[i]);
        return contents;
    }

    private void ClearPreview()
    {
        if (previewTexture != null)
            DestroyImmediate(previewTexture);
        previewTexture = null;
        previewData = null;
    }

    private void DrawPreviewLegend()
    {
        PreviewData data = previewData;
        int land = 0;
        foreach (int count in data.BiomeCounts)
            land += count;

        if (previewMode == PreviewMode.Combined || previewMode == PreviewMode.Biomes)
        {
            for (int i = 0; i < data.Biomes.Count; i++)
            {
                if (data.BiomeCounts[i] == 0)
                    continue;
                Swatch(data.BiomeColors[i], $"{data.Biomes[i].name}  ({100f * data.BiomeCounts[i] / Mathf.Max(1, land):0.#}% of land)");
            }
        }
        if (previewMode == PreviewMode.Combined || previewMode == PreviewMode.Water)
        {
            Swatch(WaterColor(WaterBodyType.Ocean, 0.6f), "Ocean");
            Swatch(WaterColor(WaterBodyType.Lake, 0f), $"Lakes ({data.Lakes})");
            Swatch(WaterColor(WaterBodyType.Pond, 0f), $"Ponds ({data.Ponds})");
            Swatch(WaterColor(WaterBodyType.River, 0f), $"Rivers ({data.Rivers})");
        }
        if (previewMode == PreviewMode.Height)
            EditorGUILayout.LabelField($"Land height {data.MinHeight:0} (dark) to {data.MaxHeight:0} (light); sea level {data.SeaLevel:0}.", EditorStyles.miniLabel);
        if (previewMode == PreviewMode.Climate)
            EditorGUILayout.LabelField("Red = hot, blue = cold; brighter green = wetter. Includes rain shadows, altitude cooling and coastal moisture when on.", EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.LabelField($"Generated in {data.Milliseconds / 1000f:0.0} s. The map doesn't update by itself - press Generate Preview again after changing settings.", EditorStyles.wordWrappedMiniLabel);
    }

    private static void Swatch(Color color, string label)
    {
        Rect line = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        Rect box = new Rect(line.x + EditorGUI.indentLevel * 15f, line.y + 2f, 14f, line.height - 4f);
        EditorGUI.DrawRect(box, color);
        EditorGUI.LabelField(new Rect(box.xMax + 6f, line.y, line.width - box.width - 6f, line.height), label, EditorStyles.miniLabel);
    }

    /// <summary>Samples the world over the preview area (in parallel, with a cancellable progress bar) and draws it.</summary>
    private void GeneratePreview(TerrainGenerator generator)
    {
        if (generator.BiomeDefinitions == null || generator.BiomeDefinitions.Length == 0)
        {
            EditorUtility.DisplayDialog("World Preview", "Add at least one biome first - there is nothing to generate from.", "OK");
            return;
        }

        var watch = Stopwatch.StartNew();
        bool cancelled = false;
        try
        {
            if (!Application.isPlaying)
            {
                // Fresh caches, so the preview reflects the current settings (outside Play mode nothing else uses them).
                VoronoiBiomeGenerator.ClearCache();
                WaterGenerator.ClearCaches();
            }

            int resolution = PreviewResolutions[previewResolutionIndex];
            var data = new PreviewData
            {
                Resolution = resolution,
                Step = previewSize / resolution,
                Min = previewCenter - Vector2.one * (previewSize * 0.5f),
                Height = new float[resolution * resolution],
                Biome = new int[resolution * resolution],
                Water = new byte[resolution * resolution],
                Temperature = new float[resolution * resolution],
                Moisture = new float[resolution * resolution],
            };

            WaterSettings water = generator.EnableWater ? WaterSettings.From(generator) : null;
            var sampler = new TerrainHeightSampler(generator, water);
            TerrainClimate climate = generator.TerrainClimate;
            data.SeaLevel = water != null ? water.SeaLevel : generator.SeaLevel;
            bool oceans = water != null && water.OceansEnabled;

            var biomeIndex = new Dictionary<Biome, int>();
            foreach (BiomeInstance instance in generator.BiomeDefinitions)
            {
                if (instance != null && instance.BiomePrefab != null && !biomeIndex.ContainsKey(instance.BiomePrefab))
                {
                    biomeIndex[instance.BiomePrefab] = data.Biomes.Count;
                    data.Biomes.Add(instance.BiomePrefab);
                }
            }

            // Terrain, biomes and climate, a band of rows at a time so the progress bar stays responsive.
            const int band = 8;
            for (int start = 0; start < resolution && !cancelled; start += band)
            {
                int end = Mathf.Min(resolution, start + band);
                Parallel.For(start, end, j =>
                {
                    for (int i = 0; i < resolution; i++)
                    {
                        int index = j * resolution + i;
                        float x = data.Min.x + (i + 0.5f) * data.Step, y = data.Min.y + (j + 0.5f) * data.Step;
                        float height = sampler.SampleBaseHeight(x, y);
                        data.Height[index] = height;

                        Biome biome = sampler.SampleBiome(x, y);
                        data.Biome[index] = biome != null && biomeIndex.TryGetValue(biome, out int b) ? b : -1;

                        Vector2 position = new Vector2(x, y);
                        float temperature = ClimateGenerator.GetTemperature(position, generator.VoronoiSeed, generator.ClimateNoiseScale);
                        float moisture = ClimateGenerator.GetMoisture(position, generator.VoronoiSeed, generator.ClimateNoiseScale);
                        if (climate != null)
                            climate.Apply(position, ref temperature, ref moisture);
                        data.Temperature[index] = temperature;
                        data.Moisture[index] = moisture;

                        if (oceans && height < water.SeaLevel && OceanGenerator.LandSide(water, x, y) < 0f)
                            data.Water[index] = (byte)WaterBodyType.Ocean;
                    }
                });
                cancelled = EditorUtility.DisplayCancelableProgressBar("World Preview", $"Sampling terrain and biomes ({end}/{resolution} rows)", 0.8f * end / resolution);
            }

            if (!cancelled && water != null)
            {
                cancelled = EditorUtility.DisplayCancelableProgressBar("World Preview", "Finding lakes and ponds", 0.82f);
                Vector2 max = data.Min + Vector2.one * previewSize;
                if (!cancelled)
                {
                    var lakes = new List<LakeFeature>();
                    LakeGenerator.GatherForRect(data.Min, max, water, sampler, lakes);
                    foreach (LakeFeature lake in lakes)
                    {
                        if (lake.Type == WaterBodyType.Pond) data.Ponds++; else data.Lakes++;
                        StampLake(data, lake);
                    }
                    cancelled = EditorUtility.DisplayCancelableProgressBar("World Preview", "Tracing rivers", 0.88f);
                }
                if (!cancelled)
                {
                    // River tracing dominates the preview's time: spread it over every core first.
                    RiverGenerator.Prefetch(data.Min, max, water, sampler);
                    var rivers = new List<RiverPath>();
                    RiverGenerator.Gather(data.Min, max, water, sampler, rivers);
                    data.Rivers = rivers.Count;
                    foreach (RiverPath river in rivers)
                        StampRiver(data, river);
                }
            }

            if (cancelled)
                return;

            data.BiomeColors = BiomeColors(data.Biomes);
            data.BiomeCounts = new int[data.Biomes.Count];
            data.MinHeight = float.MaxValue;
            data.MaxHeight = float.MinValue;
            for (int k = 0; k < data.Height.Length; k++)
            {
                if (data.Water[k] != 0)
                    continue;
                data.MinHeight = Mathf.Min(data.MinHeight, data.Height[k]);
                data.MaxHeight = Mathf.Max(data.MaxHeight, data.Height[k]);
                if (data.Biome[k] >= 0)
                    data.BiomeCounts[data.Biome[k]]++;
            }
            if (data.MinHeight > data.MaxHeight)
            {
                data.MinHeight = data.SeaLevel;
                data.MaxHeight = data.SeaLevel + 1f;
            }

            data.Milliseconds = watch.ElapsedMilliseconds;
            previewData = data;
            Colorize();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>Marks the pixels inside a lake's or pond's water outline.</summary>
    private static void StampLake(PreviewData data, LakeFeature lake)
    {
        int n = data.Resolution;
        float reach = lake.BoundRadius > 0f ? lake.BoundRadius : lake.Radius * 2f;
        int i0 = Mathf.Max(0, Mathf.FloorToInt((lake.Center.x - reach - data.Min.x) / data.Step));
        int i1 = Mathf.Min(n - 1, Mathf.CeilToInt((lake.Center.x + reach - data.Min.x) / data.Step));
        int j0 = Mathf.Max(0, Mathf.FloorToInt((lake.Center.y - reach - data.Min.y) / data.Step));
        int j1 = Mathf.Min(n - 1, Mathf.CeilToInt((lake.Center.y + reach - data.Min.y) / data.Step));
        bool any = false;
        for (int j = j0; j <= j1; j++)
        {
            for (int i = i0; i <= i1; i++)
            {
                float x = data.Min.x + (i + 0.5f) * data.Step, y = data.Min.y + (j + 0.5f) * data.Step;
                if (lake.TryGetLocal(x, y, out float rho, out _) && lake.IsInWaterZone(rho))
                {
                    data.Water[j * n + i] = (byte)lake.Type;
                    any = true;
                }
            }
        }

        // Smaller than a pixel: still show it as one.
        if (!any)
            StampDisc(data, lake.Center, 0f, (byte)lake.Type);
    }

    /// <summary>Marks the pixels a river's channel covers.</summary>
    private static void StampRiver(PreviewData data, RiverPath river)
    {
        for (int k = 0; k + 1 < river.Points.Length; k++)
        {
            Vector2 a = river.Points[k], b = river.Points[k + 1];
            float halfWidth = Mathf.Max(river.HalfWidth[k], river.HalfWidth[k + 1]);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / (data.Step * 0.5f)));
            for (int s = 0; s <= steps; s++)
                StampDisc(data, Vector2.Lerp(a, b, s / (float)steps), halfWidth, (byte)WaterBodyType.River);
        }
    }

    private static void StampDisc(PreviewData data, Vector2 center, float radius, byte type)
    {
        int n = data.Resolution;
        float ci = (center.x - data.Min.x) / data.Step - 0.5f, cj = (center.y - data.Min.y) / data.Step - 0.5f;
        float r = Mathf.Max(0.5f, radius / data.Step);
        int i0 = Mathf.Max(0, Mathf.FloorToInt(ci - r)), i1 = Mathf.Min(n - 1, Mathf.CeilToInt(ci + r));
        int j0 = Mathf.Max(0, Mathf.FloorToInt(cj - r)), j1 = Mathf.Min(n - 1, Mathf.CeilToInt(cj + r));
        for (int j = j0; j <= j1; j++)
        {
            for (int i = i0; i <= i1; i++)
            {
                float dx = i - ci, dy = j - cj;
                int index = j * n + i;
                // Rivers never overwrite the ocean or a lake they flow into.
                if (dx * dx + dy * dy <= r * r && (data.Water[index] == 0 || type != (byte)WaterBodyType.River))
                    data.Water[index] = type;
            }
        }
    }

    // Distinct earthy colours for biomes without a texture - no blues, so water always stands out.
    private static readonly Color[] FallbackBiomeColors =
    {
        new Color(0.55f, 0.75f, 0.35f), new Color(0.92f, 0.82f, 0.5f), new Color(0.6f, 0.52f, 0.45f), new Color(0.25f, 0.45f, 0.2f),
        new Color(0.95f, 0.62f, 0.3f), new Color(0.82f, 0.82f, 0.8f), new Color(0.72f, 0.45f, 0.62f), new Color(0.45f, 0.35f, 0.25f),
        new Color(0.85f, 0.42f, 0.38f), new Color(0.7f, 0.72f, 0.3f), new Color(0.5f, 0.6f, 0.45f), new Color(0.96f, 0.9f, 0.68f),
    };

    /// <summary>A colour per biome: the average colour of its texture when it has one, otherwise a distinct palette colour.</summary>
    private static Color[] BiomeColors(List<Biome> biomes)
    {
        var colors = new Color[biomes.Count];
        for (int i = 0; i < biomes.Count; i++)
        {
            colors[i] = FallbackBiomeColors[i % FallbackBiomeColors.Length];
            Texture2D texture = biomes[i].texture;
            if (texture == null)
                continue;
            try
            {
                colors[i] = AverageColor(texture);
            }
            catch (System.Exception)
            {
                // Unreadable or unusual texture format: keep the palette colour.
            }
        }

        // Two biomes with near-identical textures would be indistinguishable on the map: nudge the later one.
        for (int i = 1; i < colors.Length; i++)
        {
            for (int k = 0; k < i; k++)
            {
                if (Mathf.Abs(colors[i].r - colors[k].r) + Mathf.Abs(colors[i].g - colors[k].g) + Mathf.Abs(colors[i].b - colors[k].b) < 0.08f)
                {
                    Color.RGBToHSV(colors[i], out float h, out float s, out float v);
                    colors[i] = Color.HSVToRGB(Mathf.Repeat(h + 0.12f, 1f), Mathf.Max(0.35f, s), Mathf.Clamp(v, 0.35f, 0.9f));
                }
            }
        }
        return colors;
    }

    /// <summary>A texture's average colour, read through a tiny render texture so it also works for textures that aren't CPU-readable.</summary>
    private static Color AverageColor(Texture2D texture)
    {
        const int size = 8;
        RenderTexture target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        var readback = new Texture2D(size, size, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(texture, target);
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            readback.Apply();
            Color sum = new Color(0f, 0f, 0f, 0f);
            Color[] pixels = readback.GetPixels();
            foreach (Color pixel in pixels)
                sum += pixel;
            return pixels.Length > 0 ? new Color(sum.r / pixels.Length, sum.g / pixels.Length, sum.b / pixels.Length, 1f) : Color.gray;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            DestroyImmediate(readback);
        }
    }

    private static Color WaterColor(WaterBodyType type, float depth)
    {
        switch (type)
        {
            case WaterBodyType.Ocean: return Color.Lerp(new Color(0.2f, 0.45f, 0.7f), new Color(0.05f, 0.14f, 0.35f), depth);
            case WaterBodyType.Lake: return new Color(0.16f, 0.5f, 0.66f);
            case WaterBodyType.Pond: return new Color(0.25f, 0.5f, 0.42f);
            default: return new Color(0.1f, 0.45f, 1f);
        }
    }

    /// <summary>(Re)draws the preview texture from the sampled data in the current view mode.</summary>
    private void Colorize()
    {
        PreviewData data = previewData;
        int n = data.Resolution;
        if (previewTexture == null || previewTexture.width != n)
        {
            if (previewTexture != null)
                DestroyImmediate(previewTexture);
            previewTexture = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "World Preview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        var pixels = new Color[n * n];
        float heightRange = Mathf.Max(1f, data.MaxHeight - data.MinHeight);
        Vector3 light = new Vector3(-0.6f, 0.75f, 0.35f).normalized; // from the north-west, as on printed maps
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                int index = j * n + i;
                float h = data.Height[index];

                // Hill shading from the height gradient (exaggerated so gentle relief still reads at map scale).
                float dx = (data.Height[j * n + Mathf.Min(n - 1, i + 1)] - data.Height[j * n + Mathf.Max(0, i - 1)]) / (2f * data.Step);
                float dy = (data.Height[Mathf.Min(n - 1, j + 1) * n + i] - data.Height[Mathf.Max(0, j - 1) * n + i]) / (2f * data.Step);
                Vector3 normal = new Vector3(-dx * 3f, 1f, -dy * 3f).normalized;
                float shade = Mathf.Clamp(0.55f + 0.6f * Vector3.Dot(normal, light), 0.3f, 1.25f);

                byte water = data.Water[index];
                Color landColor = data.Biome[index] >= 0 ? data.BiomeColors[data.Biome[index]] : Color.gray;
                float elevation = Mathf.Clamp01((h - data.MinHeight) / heightRange);
                Color color;
                switch (previewMode)
                {
                    case PreviewMode.Height:
                        color = water == (byte)WaterBodyType.Ocean
                            ? WaterColor(WaterBodyType.Ocean, Mathf.Clamp01((data.SeaLevel - h) / 40f))
                            : Color.Lerp(new Color(0.1f, 0.1f, 0.1f), Color.white, elevation) * Mathf.Lerp(1f, shade, 0.5f);
                        break;
                    case PreviewMode.Biomes:
                        color = landColor * Mathf.Lerp(1f, shade, 0.25f);
                        break;
                    case PreviewMode.Water:
                        color = water != 0
                            ? WaterColor((WaterBodyType)water, Mathf.Clamp01((data.SeaLevel - h) / 40f))
                            : Color.Lerp(new Color(0.55f, 0.55f, 0.5f), new Color(0.85f, 0.85f, 0.82f), elevation) * shade;
                        break;
                    case PreviewMode.Climate:
                        float t = data.Temperature[index], m = data.Moisture[index];
                        color = new Color(0.15f + 0.85f * t, 0.15f + 0.7f * m, 0.15f + 0.85f * (1f - t)) * Mathf.Lerp(1f, shade, 0.3f);
                        if (water == (byte)WaterBodyType.Ocean)
                            color = Color.Lerp(color, new Color(0.1f, 0.1f, 0.2f), 0.6f);
                        break;
                    default:
                        color = water != 0
                            ? WaterColor((WaterBodyType)water, Mathf.Clamp01((data.SeaLevel - h) / 40f))
                            : landColor * shade;
                        break;
                }
                color.a = 1f;
                pixels[index] = color;
            }
        }

        // A small cross at world 0,0 when it is on the map.
        int oi = Mathf.FloorToInt((0f - data.Min.x) / data.Step), oj = Mathf.FloorToInt((0f - data.Min.y) / data.Step);
        for (int k = -3; k <= 3; k++)
        {
            if (oi + k >= 0 && oi + k < n && oj >= 0 && oj < n) pixels[oj * n + oi + k] = Color.red;
            if (oj + k >= 0 && oj + k < n && oi >= 0 && oi < n) pixels[(oj + k) * n + oi] = Color.red;
        }

        previewTexture.SetPixels(pixels);
        previewTexture.Apply();
    }
}
