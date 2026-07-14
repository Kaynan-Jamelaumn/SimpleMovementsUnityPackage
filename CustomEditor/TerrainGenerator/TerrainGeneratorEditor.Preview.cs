using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: the World Preview - a top-down map of the world drawn in the inspector, with several
// views, overlays, a readout of the spot under the mouse and a right-click menu (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor
{
    private enum PreviewMode { Combined, Height, Biomes, Water, Climate, Temperature, Moisture, Slope, TriPlanar, Landforms }

    private static readonly int[] PreviewResolutions = { 128, 256, 384, 512, 768 };
    private static readonly string[] PreviewResolutionLabels = { "128 x 128 (fast)", "256 x 256", "384 x 384", "512 x 512", "768 x 768 (slow)" };

    private bool showPreview = true;
    private PreviewMode previewMode = PreviewMode.Combined;
    private Vector2 previewCenter = Vector2.zero;
    private float previewSize = 4000f;
    private int previewResolutionIndex = 1;
    private Texture2D previewTexture;
    private PreviewData previewData;

    // Display settings (re-colour only, no re-sampling).
    private bool showPreviewSettings;
    private float previewShading = 1f;
    private float previewLightAngle = 315f;
    private bool previewShowContours;
    private float previewContourInterval = 25f;
    private bool previewShowChunkGrid;
    private bool previewShowBiomeBorders;
    private bool previewShowOrigin = true;
    private bool previewShowSceneView = true;
    private bool previewShowLandmarks;
    private bool previewAutoRegenerate;
    private float previewImageSize = 512f;

    // Where the last generated map was requested from (a section's inline button shows the map there too).
    private string inlinePreviewOwner;
    private string previewHover = "";
    private double autoRegenerateAt = -1;

    /// <summary>What a preview sampled, kept so switching the view mode or overlays only re-colors it.</summary>
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
        public LandformType[] Landforms;
        public int[] BiomeCounts;
        public float SeaLevel;
        public float MinHeight, MaxHeight;
        public int Lakes, Ponds, Rivers;
        public int ChunkSpan;
        public float TriplanarStart, TriplanarEnd, TriplanarStrength;
        public readonly List<ObjectPlacementEngine.LandmarkSpot> Landmarks = new List<ObjectPlacementEngine.LandmarkSpot>();
        public readonly List<string> LandmarkNames = new List<string>();
        public long Milliseconds;
    }

    /// <summary>Contents of the "World Preview" section.</summary>
    private void DrawPreviewSection(TerrainGenerator generator)
    {
        EditorGUILayout.HelpBox(
            "A top-down map of the world from the current settings - heights, biomes, oceans, lakes, ponds, rivers, volcanoes, climate, slopes and " +
            "landmarks - without entering Play mode. It samples the terrain before erosion (erosion only changes small-scale detail), one sample per " +
            "pixel, so very small features can fall between pixels. North (+Z) is up, east (+X) to the right.\n\n" +
            "Hover the map to read the spot under the mouse; double-click to center the map there; right-click for more.",
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
        previewSize = EditorGUILayout.Slider(new GUIContent("Area Size (world units)", "Width and height of the area shown. Larger areas show more of the world, with less detail per pixel."), previewSize, 250f, 60000f);
        previewResolutionIndex = EditorGUILayout.Popup(new GUIContent("Resolution", "Pixels per side. Time grows with the square of this."), previewResolutionIndex, ToContents(PreviewResolutionLabels));
        PreviewMode mode = (PreviewMode)EditorGUILayout.EnumPopup(new GUIContent("Show",
            "Combined: biome colours with hill shading and water.\nHeight: elevation (dark = low, light = high).\nBiomes: the biome layout.\n" +
            "Water: every water body by type.\nClimate: temperature (red = hot, blue = cold) and moisture (brighter green = wetter).\n" +
            "Temperature / Moisture: each on its own.\nSlope: steepness (green flat, yellow, orange, red steep).\n" +
            "Tri-Planar: where the terrain shader projects textures from the sides (orange), from Terrain Material's slope settings.\n" +
            "Landforms: which landform shapes the ground (from each biome's landform)."), previewMode);
        if (mode != previewMode)
        {
            previewMode = mode;
            if (previewData != null)
                Colorize();
        }

        showPreviewSettings = EditorGUILayout.Foldout(showPreviewSettings, new GUIContent("Map Settings & Overlays", "How the map is drawn: shading, lines and markers, image size and automatic updates."), true);
        if (showPreviewSettings)
            DrawPreviewSettings();

        float pixelSize = previewSize / PreviewResolutions[previewResolutionIndex];
        EditorGUILayout.LabelField($"{previewSize / 1000f:0.#} x {previewSize / 1000f:0.#} km, one pixel = {pixelSize:0.#} world units (a chunk is {generator.ChunkSize - 1} units)", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Generate Preview", "Samples the world with the current settings and draws the map. Can be cancelled from the progress bar.")))
        {
            inlinePreviewOwner = null;
            GeneratePreview(generator);
        }
        if (GUILayout.Button(new GUIContent("Zoom In", "Halves the area shown (same center) and draws it again."), GUILayout.Width(62)))
        {
            previewSize = Mathf.Max(250f, previewSize * 0.5f);
            GeneratePreview(generator);
        }
        if (GUILayout.Button(new GUIContent("Zoom Out", "Doubles the area shown (same center) and draws it again."), GUILayout.Width(70)))
        {
            previewSize = Mathf.Min(60000f, previewSize * 2f);
            GeneratePreview(generator);
        }
        using (new EditorGUI.DisabledScope(previewTexture == null))
        {
            if (GUILayout.Button(new GUIContent("Save PNG", "Saves the map as it is shown to a PNG file."), GUILayout.Width(70)))
                SavePreviewPng();
            if (GUILayout.Button(new GUIContent("Clear", "Removes the preview image."), GUILayout.Width(50)))
                ClearPreview();
        }
        EditorGUILayout.EndHorizontal();

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("In Play mode the preview uses the running world's caches, so settings changed since the world started may not show until you press Clear Voronoi / Biome Cache.", MessageType.None);
        }

        if (previewTexture == null || previewData == null)
            return;

        DrawPreviewImage(previewImageSize);
        DrawPreviewLegend();
    }

    /// <summary>Shading, overlays, image size and automatic updates.</summary>
    private void DrawPreviewSettings()
    {
        EditorGUI.indentLevel++;
        EditorGUI.BeginChangeCheck();
        previewShading = EditorGUILayout.Slider(new GUIContent("Hill Shading", "How strongly slopes are shaded by the light. 0 = flat colours."), previewShading, 0f, 2f);
        previewLightAngle = EditorGUILayout.Slider(new GUIContent("Light From (degrees)", "Direction the light comes from, clockwise from north. 315 = north-west, as on printed maps."), previewLightAngle, 0f, 360f);
        previewShowContours = EditorGUILayout.Toggle(new GUIContent("Contour Lines", "Thin lines at every Contour Interval of height, like a hiking map."), previewShowContours);
        if (previewShowContours)
        {
            EditorGUI.indentLevel++;
            previewContourInterval = Mathf.Max(1f, EditorGUILayout.FloatField(new GUIContent("Contour Interval", "Height (world units) between two contour lines."), previewContourInterval));
            EditorGUI.indentLevel--;
        }
        previewShowChunkGrid = EditorGUILayout.Toggle(new GUIContent("Chunk Grid", "Lines along chunk borders, to see how big chunks are compared to the features."), previewShowChunkGrid);
        previewShowBiomeBorders = EditorGUILayout.Toggle(new GUIContent("Biome Borders", "Dark lines where one biome meets another."), previewShowBiomeBorders);
        previewShowOrigin = EditorGUILayout.Toggle(new GUIContent("World Origin", "A red cross at world 0,0."), previewShowOrigin);
        previewShowSceneView = EditorGUILayout.Toggle(new GUIContent("Scene View Position", "A white ring where the Scene view camera is looking."), previewShowSceneView);
        bool changed = EditorGUI.EndChangeCheck();
        previewShowLandmarks = EditorGUILayout.Toggle(new GUIContent("Landmarks",
            "Marks where landmark objects (Limits & Landmarks > Placement Mode = Landmark) will stand - chosen from the world's shape, before any chunk exists. " +
            "Found when the map is generated; large areas with many regions take longer."), previewShowLandmarks);
        previewImageSize = EditorGUILayout.Slider(new GUIContent("Image Size (pixels)", "How big the map is drawn in the inspector."), previewImageSize, 128f, 1024f);
        previewAutoRegenerate = EditorGUILayout.Toggle(new GUIContent("Update Automatically",
            "Draw the map again by itself, half a second after any Terrain Generator setting changes. Best with a small Resolution - each update samples the whole map."), previewAutoRegenerate);
        EditorGUI.indentLevel--;
        if (changed && previewData != null)
            Colorize();
    }

    /// <summary>Draws the map with the hover readout, double-click to center and the right-click menu.</summary>
    private void DrawPreviewImage(float maxSize)
    {
        float width = Mathf.Min(EditorGUIUtility.currentViewWidth - 40f, maxSize);
        Rect rect = GUILayoutUtility.GetRect(width, width, GUILayout.ExpandWidth(false));
        EditorGUI.DrawPreviewTexture(rect, previewTexture);

        Event e = Event.current;
        if (e.type != EventType.Layout && rect.Contains(e.mousePosition) && previewData != null)
        {
            // The inspector only gets mouse-move events (for the readout) when it asks for them.
            if (EditorWindow.mouseOverWindow != null && !EditorWindow.mouseOverWindow.wantsMouseMove)
                EditorWindow.mouseOverWindow.wantsMouseMove = true;
            PreviewData data = previewData;
            int n = data.Resolution;
            int i = Mathf.Clamp(Mathf.FloorToInt((e.mousePosition.x - rect.x) / rect.width * n), 0, n - 1);
            int j = Mathf.Clamp(Mathf.FloorToInt((1f - (e.mousePosition.y - rect.y) / rect.height) * n), 0, n - 1);
            Vector2 world = data.Min + new Vector2((i + 0.5f) * data.Step, (j + 0.5f) * data.Step);
            previewHover = HoverText(data, i, j, world);

            if (e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 2)
            {
                previewCenter = world;
                GeneratePreview((TerrainGenerator)target);
                e.Use();
            }
            else if (e.type == EventType.ContextClick)
            {
                ShowPreviewMenu(world, data.Height[j * n + i]);
                e.Use();
            }
            else if (e.type == EventType.MouseMove)
            {
                Repaint();
            }
        }
        // Fixed height, so the layout doesn't jump as the text changes.
        Rect readout = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2f);
        EditorGUI.LabelField(readout, string.IsNullOrEmpty(previewHover) ? "Hover the map to read a spot; double-click to center it there; right-click for more." : previewHover, EditorStyles.wordWrappedMiniLabel);
    }

    private string HoverText(PreviewData data, int i, int j, Vector2 world)
    {
        int n = data.Resolution, index = j * n + i;
        string biome = data.Biome[index] >= 0 ? data.Biomes[data.Biome[index]].name : "(no biome)";
        string water = data.Water[index] != 0 ? ((WaterBodyType)data.Water[index]).ToString() : "dry land";
        int span = Mathf.Max(1, data.ChunkSpan);
        return $"({world.x:0}, {world.y:0}) - chunk ({Mathf.FloorToInt(world.x / span)}, {Mathf.FloorToInt(world.y / span)}) - height {data.Height[index]:0.#} - " +
               $"slope {SlopeAt(data, i, j):0}° - {biome}" + (data.Biome[index] >= 0 && data.Landforms != null ? $" ({data.Landforms[data.Biome[index]]})" : "") +
               $" - {water} - temperature {data.Temperature[index]:0.00}, moisture {data.Moisture[index]:0.00}";
    }

    private void ShowPreviewMenu(Vector2 world, float height)
    {
        var menu = new GenericMenu();
        TerrainGenerator generator = (TerrainGenerator)target;
        menu.AddItem(new GUIContent("Center Map Here"), false, () => { previewCenter = world; GeneratePreview(generator); });
        menu.AddItem(new GUIContent("Zoom In Here"), false, () => { previewCenter = world; previewSize = Mathf.Max(250f, previewSize * 0.5f); GeneratePreview(generator); });
        menu.AddItem(new GUIContent("Move Scene View Here"), false, () =>
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
                view.LookAt(new Vector3(world.x, height, world.y));
        });
        menu.AddItem(new GUIContent("Test Object Placement Here"), false, () =>
        {
            RunPlacementTest(generator, world);
            EditorUtility.DisplayDialog("Test Placement", (testSummary ?? "Done.") + "\n\nEach object's own result is shown in its box in the Biomes section.", "OK");
        });
        menu.AddItem(new GUIContent("Copy Position"), false, () => EditorGUIUtility.systemCopyBuffer = $"{world.x:0.##}, {world.y:0.##}");
        menu.ShowAsContext();
    }

    private void SavePreviewPng()
    {
        string path = EditorUtility.SaveFilePanel("Save World Preview", "", $"World Preview {previewMode}.png", "png");
        if (string.IsNullOrEmpty(path))
            return;
        File.WriteAllBytes(path, previewTexture.EncodeToPNG());
    }

    /// <summary>
    /// A button in another section that draws the World Preview in a given view around the Scene view position, and
    /// shows the map right under the button.
    /// </summary>
    private void InlinePreviewButton(string label, string tooltip, PreviewMode mode, float size)
    {
        if (GUILayout.Button(new GUIContent(label, tooltip + " Also updates the World Preview section.")))
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
                previewCenter = new Vector2(view.pivot.x, view.pivot.z);
            previewMode = mode;
            previewSize = size;
            inlinePreviewOwner = label;
            GeneratePreview((TerrainGenerator)target);
        }
        if (inlinePreviewOwner == label && previewTexture != null && previewData != null)
        {
            DrawPreviewImage(320f);
            DrawPreviewLegend();
            if (GUILayout.Button(new GUIContent("Hide Map", "Hides this map here (it stays in the World Preview section).")))
                inlinePreviewOwner = null;
        }
    }

    /// <summary>Call at the end of OnInspectorGUI: regenerates the preview a moment after settings change, when Update Automatically is on.</summary>
    private void SchedulePreviewUpdate(bool settingsChanged)
    {
        if (!previewAutoRegenerate || previewData == null || !settingsChanged)
            return;
        bool waiting = autoRegenerateAt > 0;
        autoRegenerateAt = EditorApplication.timeSinceStartup + 0.5;
        if (!waiting)
            EditorApplication.update += AutoRegenerateTick;
    }

    private void AutoRegenerateTick()
    {
        if (autoRegenerateAt < 0 || EditorApplication.timeSinceStartup < autoRegenerateAt)
            return;
        EditorApplication.update -= AutoRegenerateTick;
        autoRegenerateAt = -1;
        if (target != null)
        {
            GeneratePreview((TerrainGenerator)target);
            Repaint();
        }
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
        inlinePreviewOwner = null;
        if (autoRegenerateAt > 0)
        {
            EditorApplication.update -= AutoRegenerateTick;
            autoRegenerateAt = -1;
        }
    }

    private void DrawPreviewLegend()
    {
        PreviewData data = previewData;
        int land = 0;
        foreach (int count in data.BiomeCounts)
            land += count;

        switch (previewMode)
        {
            case PreviewMode.Combined:
            case PreviewMode.Biomes:
                for (int i = 0; i < data.Biomes.Count; i++)
                {
                    if (data.BiomeCounts[i] == 0)
                        continue;
                    Swatch(data.BiomeColors[i], $"{data.Biomes[i].name}  ({100f * data.BiomeCounts[i] / Mathf.Max(1, land):0.#}% of land)");
                }
                break;
            case PreviewMode.Height:
                EditorGUILayout.LabelField($"Land height {data.MinHeight:0} (dark) to {data.MaxHeight:0} (light); sea level {data.SeaLevel:0}.", EditorStyles.miniLabel);
                break;
            case PreviewMode.Climate:
                EditorGUILayout.LabelField("Red = hot, blue = cold; brighter green = wetter. Includes rain shadows, altitude cooling and coastal moisture when on.", EditorStyles.wordWrappedMiniLabel);
                break;
            case PreviewMode.Temperature:
                Swatch(TemperatureColor(0f), "Cold (0)");
                Swatch(TemperatureColor(0.5f), "Mild (0.5)");
                Swatch(TemperatureColor(1f), "Hot (1)");
                break;
            case PreviewMode.Moisture:
                Swatch(MoistureColor(0f), "Dry (0)");
                Swatch(MoistureColor(0.5f), "Average (0.5)");
                Swatch(MoistureColor(1f), "Wet (1)");
                break;
            case PreviewMode.Slope:
                Swatch(SlopeColor(3f), "Flat (under 10°)");
                Swatch(SlopeColor(17f), "Gentle (10-25°)");
                Swatch(SlopeColor(32f), "Steep (25-40°)");
                Swatch(SlopeColor(50f), "Very steep (over 40°)");
                EditorGUILayout.LabelField("Slopes are averaged over one pixel - zoom in to see small cliffs.", EditorStyles.miniLabel);
                break;
            case PreviewMode.TriPlanar:
                Swatch(new Color(0.55f, 0.55f, 0.55f), "Textures from above only");
                Swatch(new Color(1f, 0.55f, 0.1f), $"Projected from the sides too (slopes over {data.TriplanarStart:0}°, fully from {data.TriplanarEnd:0}°)");
                EditorGUILayout.LabelField("Slopes are averaged over one pixel, so real cliffs are steeper than they look here.", EditorStyles.miniLabel);
                break;
            case PreviewMode.Landforms:
                var seen = new HashSet<LandformType>();
                for (int i = 0; i < data.Biomes.Count; i++)
                    if (data.BiomeCounts[i] > 0 && seen.Add(data.Landforms[i]))
                        Swatch(LandformColor(data.Landforms[i]), data.Landforms[i].ToString());
                break;
        }
        if (previewMode == PreviewMode.Combined || previewMode == PreviewMode.Water)
        {
            Swatch(WaterColor(WaterBodyType.Ocean, 0.6f), "Ocean");
            Swatch(WaterColor(WaterBodyType.Lake, 0f), $"Lakes ({data.Lakes})");
            Swatch(WaterColor(WaterBodyType.Pond, 0f), $"Ponds ({data.Ponds})");
            Swatch(WaterColor(WaterBodyType.River, 0f), $"Rivers ({data.Rivers})");
        }
        if (previewShowLandmarks && data.Landmarks.Count > 0)
        {
            var counts = new Dictionary<int, int>();
            foreach (ObjectPlacementEngine.LandmarkSpot spot in data.Landmarks)
            {
                counts.TryGetValue(spot.Type, out int c);
                counts[spot.Type] = c + 1;
            }
            foreach (KeyValuePair<int, int> entry in counts)
                Swatch(LandmarkColor(entry.Key), $"{data.LandmarkNames[entry.Key]} ({entry.Value})");
        }
        else if (previewShowLandmarks)
        {
            EditorGUILayout.LabelField("No landmark spots in this area.", EditorStyles.miniLabel);
        }

        EditorGUILayout.LabelField($"Generated in {data.Milliseconds / 1000f:0.0} s." + (previewAutoRegenerate ? " Updates by itself when settings change." : " The map doesn't update by itself - press Generate Preview again after changing settings (or turn on Update Automatically)."), EditorStyles.wordWrappedMiniLabel);
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

            if (!cancelled && previewShowLandmarks && generator.ShouldSpawnObjects)
            {
                cancelled = EditorUtility.DisplayCancelableProgressBar("World Preview", "Finding landmark spots", 0.94f);
                if (!cancelled)
                {
                    PlacementPlan plan = PlacementPlan.Build(generator, PrefabShapeCache.Get);
                    ObjectPlacementEngine.FindLandmarkSpots(plan, data.Min, data.Min + Vector2.one * previewSize, data.Landmarks);
                    foreach (PlacementType type in plan.Types)
                        data.LandmarkNames.Add(type.Prefab != null ? type.Prefab.name : type.Tag);
                }
            }

            if (cancelled)
                return;

            data.BiomeColors = BiomeColors(data.Biomes);
            data.Landforms = new LandformType[data.Biomes.Count];
            for (int b = 0; b < data.Biomes.Count; b++)
                data.Landforms[b] = LandformGenerator.Effective(data.Biomes[b], generator.TerrainShapeMode);
            data.ChunkSpan = generator.ChunkSize - 1;
            data.TriplanarStart = generator.TriplanarSlopeStart;
            data.TriplanarEnd = generator.TriplanarSlopeEnd;
            data.TriplanarStrength = generator.TriplanarStrength;
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

    private static Color TemperatureColor(float t) => Color.Lerp(Color.Lerp(new Color(0.15f, 0.3f, 0.95f), new Color(0.9f, 0.9f, 0.85f), Mathf.Clamp01(t * 2f)), new Color(0.95f, 0.25f, 0.1f), Mathf.Clamp01(t * 2f - 1f));
    private static Color MoistureColor(float m) => Color.Lerp(new Color(0.85f, 0.7f, 0.4f), new Color(0.1f, 0.55f, 0.25f), Mathf.Clamp01(m));

    private static Color SlopeColor(float degrees)
    {
        if (degrees < 10f) return Color.Lerp(new Color(0.2f, 0.55f, 0.2f), new Color(0.55f, 0.75f, 0.25f), degrees / 10f);
        if (degrees < 25f) return Color.Lerp(new Color(0.55f, 0.75f, 0.25f), new Color(0.95f, 0.85f, 0.2f), (degrees - 10f) / 15f);
        if (degrees < 40f) return Color.Lerp(new Color(0.95f, 0.85f, 0.2f), new Color(0.95f, 0.5f, 0.1f), (degrees - 25f) / 15f);
        return Color.Lerp(new Color(0.95f, 0.5f, 0.1f), new Color(0.75f, 0.1f, 0.1f), Mathf.Clamp01((degrees - 40f) / 20f));
    }

    private static Color LandformColor(LandformType landform)
    {
        switch (landform)
        {
            case LandformType.Classic: return new Color(0.65f, 0.65f, 0.6f);
            case LandformType.Plains: return new Color(0.6f, 0.8f, 0.4f);
            case LandformType.Hills: return new Color(0.4f, 0.65f, 0.3f);
            case LandformType.Mountains: return new Color(0.55f, 0.45f, 0.4f);
            case LandformType.Dunes: return new Color(0.93f, 0.8f, 0.5f);
            case LandformType.Wetland: return new Color(0.3f, 0.55f, 0.5f);
            case LandformType.Plateau: return new Color(0.8f, 0.5f, 0.35f);
            case LandformType.Highlands: return new Color(0.35f, 0.45f, 0.25f);
            case LandformType.Glacial: return new Color(0.85f, 0.9f, 0.95f);
            case LandformType.SeaPlain: return new Color(0.2f, 0.35f, 0.6f);
            case LandformType.SeaRavines: return new Color(0.15f, 0.25f, 0.5f);
            case LandformType.SeaReef: return new Color(0.3f, 0.7f, 0.75f);
            case LandformType.SeaRocky: return new Color(0.3f, 0.35f, 0.45f);
            default: return new Color(0.7f, 0.7f, 0.7f);
        }
    }

    private static readonly Color[] LandmarkColors =
    {
        new Color(1f, 0.2f, 0.8f), new Color(1f, 1f, 0.2f), new Color(0.2f, 1f, 1f), new Color(1f, 0.5f, 0f), new Color(0.6f, 0.3f, 1f), Color.white,
    };

    private static Color LandmarkColor(int type) => LandmarkColors[type % LandmarkColors.Length];

    /// <summary>Slope (degrees) at a pixel from the height gradient over one pixel.</summary>
    private static float SlopeAt(PreviewData data, int i, int j)
    {
        int n = data.Resolution;
        float dx = (data.Height[j * n + Mathf.Min(n - 1, i + 1)] - data.Height[j * n + Mathf.Max(0, i - 1)]) / (2f * data.Step);
        float dy = (data.Height[Mathf.Min(n - 1, j + 1) * n + i] - data.Height[Mathf.Max(0, j - 1) * n + i]) / (2f * data.Step);
        return Mathf.Atan(Mathf.Sqrt(dx * dx + dy * dy)) * Mathf.Rad2Deg;
    }

    /// <summary>(Re)draws the preview texture from the sampled data in the current view mode and overlays.</summary>
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
        float angle = (90f - previewLightAngle) * Mathf.Deg2Rad;   // compass (clockwise from north) to maths angle
        Vector3 light = new Vector3(Mathf.Cos(angle) * 0.8f, 0.75f, Mathf.Sin(angle) * 0.8f).normalized;
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
                float rawShade = Mathf.Clamp(0.55f + 0.6f * Vector3.Dot(normal, light), 0.3f, 1.25f);
                float shade = Mathf.Lerp(1f, rawShade, previewShading);

                byte water = data.Water[index];
                bool ocean = water == (byte)WaterBodyType.Ocean;
                Color landColor = data.Biome[index] >= 0 ? data.BiomeColors[data.Biome[index]] : Color.gray;
                float elevation = Mathf.Clamp01((h - data.MinHeight) / heightRange);
                float slope = Mathf.Atan(Mathf.Sqrt(dx * dx + dy * dy)) * Mathf.Rad2Deg;
                Color color;
                switch (previewMode)
                {
                    case PreviewMode.Height:
                        color = ocean
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
                        if (ocean)
                            color = Color.Lerp(color, new Color(0.1f, 0.1f, 0.2f), 0.6f);
                        break;
                    case PreviewMode.Temperature:
                        color = TemperatureColor(data.Temperature[index]) * Mathf.Lerp(1f, shade, 0.3f);
                        if (ocean)
                            color = Color.Lerp(color, new Color(0.1f, 0.1f, 0.2f), 0.5f);
                        break;
                    case PreviewMode.Moisture:
                        color = MoistureColor(data.Moisture[index]) * Mathf.Lerp(1f, shade, 0.3f);
                        if (ocean)
                            color = Color.Lerp(color, new Color(0.1f, 0.1f, 0.2f), 0.5f);
                        break;
                    case PreviewMode.Slope:
                        color = water != 0 ? new Color(0.25f, 0.35f, 0.5f) : SlopeColor(slope) * Mathf.Lerp(1f, shade, 0.4f);
                        break;
                    case PreviewMode.TriPlanar:
                        float amount = data.TriplanarStrength * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(data.TriplanarStart, Mathf.Max(data.TriplanarEnd, data.TriplanarStart + 0.01f), slope));
                        color = water != 0 ? new Color(0.25f, 0.35f, 0.5f) : Color.Lerp(new Color(0.55f, 0.55f, 0.55f), new Color(1f, 0.55f, 0.1f), amount) * Mathf.Lerp(1f, shade, 0.5f);
                        break;
                    case PreviewMode.Landforms:
                        color = data.Biome[index] >= 0 ? LandformColor(data.Landforms[data.Biome[index]]) * Mathf.Lerp(1f, shade, 0.4f) : Color.gray;
                        if (ocean && (data.Biome[index] < 0 || data.Biomes[data.Biome[index]].placement != BiomePlacement.Ocean))
                            color = Color.Lerp(color, WaterColor(WaterBodyType.Ocean, 0.5f), 0.7f);
                        break;
                    default:
                        color = water != 0
                            ? WaterColor((WaterBodyType)water, Mathf.Clamp01((data.SeaLevel - h) / 40f))
                            : landColor * shade;
                        break;
                }

                // Line overlays.
                if (previewShowContours && water == 0)
                {
                    int band = Mathf.FloorToInt(h / previewContourInterval);
                    if ((i + 1 < n && Mathf.FloorToInt(data.Height[index + 1] / previewContourInterval) != band) ||
                        (j + 1 < n && Mathf.FloorToInt(data.Height[index + n] / previewContourInterval) != band))
                        color = Color.Lerp(color, new Color(0.2f, 0.12f, 0.05f), 0.55f);
                }
                if (previewShowBiomeBorders && !ocean &&
                    ((i + 1 < n && data.Biome[index + 1] != data.Biome[index]) || (j + 1 < n && data.Biome[index + n] != data.Biome[index])))
                    color = Color.Lerp(color, Color.black, 0.6f);
                if (previewShowChunkGrid && data.ChunkSpan > 0)
                {
                    float x0 = data.Min.x + i * data.Step, z0 = data.Min.y + j * data.Step;
                    if (Mathf.FloorToInt(x0 / data.ChunkSpan) != Mathf.FloorToInt((x0 + data.Step) / data.ChunkSpan) ||
                        Mathf.FloorToInt(z0 / data.ChunkSpan) != Mathf.FloorToInt((z0 + data.Step) / data.ChunkSpan))
                        color = Color.Lerp(color, Color.white, 0.35f);
                }
                color.a = 1f;
                pixels[index] = color;
            }
        }

        // Markers.
        if (previewShowOrigin)
            DrawCross(pixels, data, Vector2.zero, Color.red, 3);
        if (previewShowSceneView && SceneView.lastActiveSceneView != null)
        {
            Vector3 pivot = SceneView.lastActiveSceneView.pivot;
            DrawRing(pixels, data, new Vector2(pivot.x, pivot.z), Color.white, 4);
        }
        if (previewShowLandmarks)
            foreach (ObjectPlacementEngine.LandmarkSpot spot in data.Landmarks)
                DrawRing(pixels, data, spot.Position, LandmarkColor(spot.Type), 3);

        previewTexture.SetPixels(pixels);
        previewTexture.Apply();
    }

    private static bool ToPixel(PreviewData data, Vector2 world, out int i, out int j)
    {
        i = Mathf.FloorToInt((world.x - data.Min.x) / data.Step);
        j = Mathf.FloorToInt((world.y - data.Min.y) / data.Step);
        return i >= 0 && j >= 0 && i < data.Resolution && j < data.Resolution;
    }

    private static void DrawCross(Color[] pixels, PreviewData data, Vector2 world, Color color, int arm)
    {
        if (!ToPixel(data, world, out int ci, out int cj))
            return;
        int n = data.Resolution;
        for (int k = -arm; k <= arm; k++)
        {
            if (ci + k >= 0 && ci + k < n) pixels[cj * n + ci + k] = color;
            if (cj + k >= 0 && cj + k < n) pixels[(cj + k) * n + ci] = color;
        }
    }

    private static void DrawRing(Color[] pixels, PreviewData data, Vector2 world, Color color, int radius)
    {
        if (!ToPixel(data, world, out int ci, out int cj))
            return;
        int n = data.Resolution;
        for (int dj = -radius - 1; dj <= radius + 1; dj++)
        {
            for (int di = -radius - 1; di <= radius + 1; di++)
            {
                float d = Mathf.Sqrt(di * di + dj * dj);
                int i = ci + di, j = cj + dj;
                if (i < 0 || j < 0 || i >= n || j >= n)
                    continue;
                if (Mathf.Abs(d - radius) < 0.8f)
                    pixels[j * n + i] = color;
                else if (Mathf.Abs(d - radius) < 1.6f)
                    pixels[j * n + i] = Color.black;
            }
        }
    }
}
