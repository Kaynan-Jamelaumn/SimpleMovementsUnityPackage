using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: the objects, terrain material and performance sections (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor
{
    /// <summary>Contents of the "Objects" section.</summary>
    private void DrawObjectsSection(TerrainGenerator generator)
    {
        Field(shouldSpawnObjectsProp, "Should Spawn Objects");
        using (new EditorGUI.DisabledScope(!shouldSpawnObjectsProp.boolValue))
        {
            EditorGUI.indentLevel++;
            DrawProp("objectCliffAngle", "Cliff Angle");
            DrawProp("objectSpawnBudgetMs", "Spawn Budget (ms per frame)");
            DrawProp("maxObjectsPerFrame", "Max Objects Per Frame");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(new GUIContent("Far Objects", "Only chunks near the viewer get full objects; further away, objects keep their looks but lose their colliders, scripts and so on."), EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            DrawProp("fullObjectDistance", "Full Object Distance (0 = off)");
            using (new EditorGUI.DisabledScope(serializedObject.FindProperty("fullObjectDistance").floatValue <= 0f))
                DrawProp("farObjectParts", "Far Object Parts");
            DrawFarObjectInfo(generator);
            EditorGUI.indentLevel--;
        }
        ResetButtons("object", ObjectRecommended, ObjectFields, "Leaves Should Spawn Objects as it is.");

        EditorGUILayout.HelpBox(
            "Each object's rules are on its entry in a biome's object list (Biomes section > a biome > Runtime Objects): density, biomes and " +
            "borders, height, slope, water, climate, distances to features, clusters, spacing, relationships to other objects, orientation, " +
            "ground contact and landmark options. Every rule group starts as 'no constraint'.\n\n" +
            "Placement runs on the worker threads and is deterministic: the same seed and settings always give the same objects in the same " +
            "places, whatever order chunks load in - so a chunk that is unloaded and comes back gets exactly the same objects. Objects are then " +
            "created a few per frame, nearest chunks first (Spawn Budget).\n\n" +
            "Cluster Base Frequency and Cluster Amplitude are no longer used (each object has its own Density Noise and Clustering).",
            MessageType.None);

        if (Application.isPlaying)
            DrawPlacementReport(generator);
        else
            EditorGUILayout.HelpBox("In Play mode, a report here shows how many of each object were placed in the loaded chunks, and why the rest of their candidate spots were rejected - the quickest way to see which rule keeps an object from spawning.", MessageType.None);
    }

    /// <summary>What Far Objects means with the current settings, and in Play mode how many chunks are far.</summary>
    private void DrawFarObjectInfo(TerrainGenerator generator)
    {
        float distance = serializedObject.FindProperty("fullObjectDistance").floatValue;
        if (distance <= 0f)
        {
            EditorGUILayout.LabelField("Off: every chunk keeps its objects fully working.", EditorStyles.wordWrappedMiniLabel);
            return;
        }
        var parts = (FarObjectParts)serializedObject.FindProperty("farObjectParts").intValue;
        int span = Mathf.Max(1, generator.ChunkSize - 1);
        var names = new List<string>();
        if ((parts & FarObjectParts.Colliders) != 0) names.Add("colliders (Rigidbodies kinematic)");
        if ((parts & FarObjectParts.Scripts) != 0) names.Add("scripts");
        if ((parts & FarObjectParts.Animators) != 0) names.Add("animators");
        if ((parts & FarObjectParts.Audio) != 0) names.Add("audio");
        if ((parts & FarObjectParts.Lights) != 0) names.Add("lights");
        float margin = Mathf.Max(20f, distance * 0.2f);
        string text = names.Count == 0
            ? "Far Object Parts is empty: nothing is switched off."
            : $"Chunks whose nearest edge is within {distance:0} units of the viewer (your chunk and about {Mathf.CeilToInt(distance / span)} ring{(distance > span ? "s" : "")} around it) keep full objects. " +
              $"Beyond {distance + margin:0} units, objects switch off their {string.Join(", ", names)} - they still look the same. " +
              "Physics, raycasts and AI further away than that pass through them. Scripts implementing IFarTerrainObject stay on and are told instead; objects with Terrain Object Keep Full are never touched.";
        EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);
        if (Application.isPlaying)
            EditorGUILayout.LabelField($"Now: {generator.FarObjects.FarChunks} chunk(s) with far objects, {generator.FarObjects.SwitchingChunks} switching.", EditorStyles.miniLabel);
    }

    /// <summary>Per object type over the loaded chunks: placed, tried, and the stages that rejected the most spots.</summary>
    private void DrawPlacementReport(TerrainGenerator generator)
    {
        var placed = new Dictionary<string, int>();
        var tried = new Dictionary<string, int>();
        var rejected = new Dictionary<string, int[]>();
        int chunks = 0;
        foreach (LoadedTerrain.Chunk chunk in LoadedTerrain.All)
        {
            PlacementResult result = chunk.Placements;
            if (result == null || result.Plan == null || result.Tried == null)
                continue;
            chunks++;
            PlacementPlan plan = result.Plan;
            for (int t = 0; t < plan.Types.Length; t++)
            {
                string name = plan.Types[t].Prefab != null ? plan.Types[t].Prefab.name : plan.Types[t].Tag;
                placed.TryGetValue(name, out int p);
                placed[name] = p + result.Accepted[t];
                tried.TryGetValue(name, out int tr);
                tried[name] = tr + result.Tried[t];
                if (!rejected.TryGetValue(name, out int[] stages))
                    rejected[name] = stages = new int[(int)ObjectPlacementEngine.Stage.Count];
                if (result.Rejected != null && t < result.Rejected.Length)
                    for (int s = 0; s < stages.Length; s++)
                        stages[s] += result.Rejected[t][s];
            }
        }

        if (chunks == 0)
        {
            EditorGUILayout.HelpBox("No chunk has placed its objects yet.", MessageType.None);
            return;
        }

        var report = new StringBuilder();
        report.Append($"Loaded chunks with objects: {chunks}");
        foreach (string name in placed.Keys.OrderBy(n => n))
        {
            report.Append($"\n\n{name}: {placed[name]} placed of {tried[name]} spots tried");
            int[] stages = rejected[name];
            var top = Enumerable.Range(0, stages.Length).Where(s => stages[s] > 0).OrderByDescending(s => stages[s]).Take(3).ToList();
            if (top.Count > 0)
                report.Append("\n   most rejected by: " + string.Join(", ", top.Select(s => StageName((ObjectPlacementEngine.Stage)s) + " " + stages[s])));
        }
        report.Append("\n\n(Spots whose first chance roll failed aren't counted as tried.)");
        EditorGUILayout.HelpBox(report.ToString(), MessageType.None);
        Repaint();
    }

    private static string StageName(ObjectPlacementEngine.Stage stage)
    {
        switch (stage)
        {
            case ObjectPlacementEngine.Stage.Chance: return "chance";
            case ObjectPlacementEngine.Stage.Biome: return "biome/borders";
            case ObjectPlacementEngine.Stage.Slope: return "slope";
            case ObjectPlacementEngine.Stage.Water: return "water";
            case ObjectPlacementEngine.Stage.Altitude: return "height/land shape";
            case ObjectPlacementEngine.Stage.Climate: return "climate";
            case ObjectPlacementEngine.Stage.Feature: return "feature distances";
            case ObjectPlacementEngine.Stage.Relations: return "relationships";
            case ObjectPlacementEngine.Stage.Spacing: return "spacing";
            case ObjectPlacementEngine.Stage.Limits: return "limits";
            case ObjectPlacementEngine.Stage.Orientation: return "orientation";
            case ObjectPlacementEngine.Stage.Ground: return "ground fit (footprint too uneven)";
            default: return stage.ToString();
        }
    }

    /// <summary>Contents of the "Terrain Material" section.</summary>
    private void DrawTerrainMaterialSection(TerrainGenerator generator)
    {
        SerializedProperty mode = serializedObject.FindProperty("terrainShader");
        Field(mode, "Terrain Shader");
        var shaderMode = (TerrainShaderMode)mode.enumValueIndex;
        if (shaderMode == TerrainShaderMode.CustomMaterial)
        {
            EditorGUI.indentLevel++;
            DrawProp("customTerrainMaterial", "Custom Terrain Material");
            EditorGUI.indentLevel--;
            if (serializedObject.FindProperty("customTerrainMaterial").objectReferenceValue == null)
                EditorGUILayout.HelpBox("No material assigned: the package shader is used instead.", MessageType.Warning);
        }

        bool packageSettings = shaderMode == TerrainShaderMode.PackageTriplanar || shaderMode == TerrainShaderMode.CustomMaterial;
        using (new EditorGUI.DisabledScope(!packageSettings))
        {
            DrawProp("terrainTextureSize", "Texture Size (world units)");
            if (serializedObject.FindProperty("terrainTextureSize").floatValue <= 0f)
                EditorGUILayout.LabelField($"Automatic: {generator.TerrainTextureWorldSize:0.#} world units per texture repeat", EditorStyles.miniLabel);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Tri-Planar Mapping (steep ground)", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            DrawProp("triplanarStrength", "Strength");
            DrawProp("triplanarSlopeStart", "From Slope");
            DrawProp("triplanarSlopeEnd", "Full At Slope");
            DrawProp("triplanarSharpness", "Blend Sharpness");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Surface", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            DrawProp("terrainSmoothness", "Smoothness");
            DrawProp("wetnessDarkening", "Wet Ground Darkening");
            DrawProp("wetnessSmoothness", "Wet Ground Smoothness");
            EditorGUI.indentLevel--;
        }
        DrawTriplanarCurve(generator);
        DrawBiomeTextureStrip(generator);
        InlinePreviewButton("Preview Tri-Planar Areas", "Shows where the terrain's textures are projected from the sides (orange) on the World Preview, around the Scene view position.", PreviewMode.TriPlanar, 4000f);
        ResetButtons("terrain material", MaterialRecommended, MaterialFields, "Leaves the Terrain Shader choice and the Custom Terrain Material alone.");

        var info = new StringBuilder();
        info.Append("Package (Tri-Planar) projects the biome textures in world space: from above on gentle ground, and from the sides as well " +
                    "on slopes steeper than From Slope - fully from Full At Slope - so cliffs, mountain faces and abrupt rises and drops don't " +
                    "stretch them. World-space projection also hides the seams between chunks. It supports URP and the Built-in pipeline.");
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        bool hdrp = pipeline != null && !pipeline.GetType().Name.Contains("Universal");
        if (hdrp && shaderMode == TerrainShaderMode.PackageTriplanar)
            info.Append("\n\nThis project uses HDRP, which the package shader doesn't support: the project's HDRP terrain shader is used.");
        if (shaderMode == TerrainShaderMode.ProjectShader && Shader.Find("Custom/TerrainSplatMapShaderURP") == null && Shader.Find("Custom/TerrainSplatMapShaderHDRP") == null)
            info.Append("\n\nNo 'Custom/TerrainSplatMapShader...' shader was found in this project: the package shader is used instead.");
        if (!generator.TerrainTextureBasedOnVoronoiPoints)
            info.Append("\n\nTexture Based On Voronoi Points is off: chunks get no splat maps, so no terrain material is made.");
        EditorGUILayout.HelpBox(info.ToString(), MessageType.None);
    }

    /// <summary>Contents of the "Performance & Threading" section.</summary>
    private void DrawPerformanceSection(TerrainGenerator generator)
    {
        EditorGUILayout.LabelField("Threads & Frame Budget", EditorStyles.miniBoldLabel);
        DrawProp("workerThreads", "Worker Threads");
        if (serializedObject.FindProperty("workerThreads").intValue <= 0)
            EditorGUILayout.LabelField($"Automatic: {TerrainWorkerPool.DefaultThreadCount} threads on this machine", EditorStyles.miniLabel);
        DrawProp("mainThreadBudgetMs", "Main Thread Budget (ms per frame)");
        DrawProp("objectSpawnBudgetMs", "Object Spawn Budget (ms per frame)");
        DrawProp("maxObjectsPerFrame", "Max Objects Per Frame");
        DrawProp("prepareMeshesOnWorkers", "Prepare Meshes On Workers");
        float frame = serializedObject.FindProperty("mainThreadBudgetMs").floatValue + serializedObject.FindProperty("objectSpawnBudgetMs").floatValue;
        EditorGUILayout.LabelField($"   Up to {frame:0.#} ms of each frame goes to the terrain while chunks load ({100f * frame / 16.7f:0}% of a 60 fps frame).", EditorStyles.miniLabel);

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Object Pooling", EditorStyles.miniBoldLabel);
        DrawProp("poolObjects", "Reuse Objects Of Unloaded Chunks");
        using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("poolObjects").boolValue))
            DrawProp("maxPooledObjects", "Max Pooled Objects");

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Biome Textures", EditorStyles.miniBoldLabel);
        DrawProp("biomeTextureQuality", "Texture Quality");
        DrawProp("biomeTextureResolution", "Texture Resolution (0 = auto)");
        DrawBiomeTextureMemory(generator);

        EditorGUILayout.HelpBox(
            "Chunks are generated on a fixed set of worker threads, nearest to the viewer first (re-checked as the viewer moves), instead of one " +
            "new thread per request. Finished data is applied on the main thread within Main Thread Budget per frame; terrain colliders are " +
            "cooked on a job thread, NavMeshes are built in the background, and objects are created within the Objects section's Spawn Budget.\n\n" +
            "Unloading, the data cache of recently unloaded chunks and the NavMesh distance are set on the EndlessTerrain component (Chunk Lifecycle).",
            MessageType.None);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                $"Worker jobs waiting: {generator.PendingWorkerJobs} ({generator.WorkerThreads} threads)\n" +
                $"Results waiting for the main thread: {generator.PendingMainThreadWork}\n" +
                $"Objects waiting to be created: {generator.ObjectInstantiator.PendingObjects}\n" +
                $"Objects in the pool: {generator.ObjectInstantiator.PooledObjects}\n" +
                $"Chunks loaded: {LoadedTerrain.Count}",
                MessageType.None);
            Repaint();
        }

        ResetButtons("performance", PerformanceRecommended, PerformanceFields, null, "Auto-Detect For This Computer",
            "Chooses the budgets, pool size and biome texture storage from this computer's CPU, memory and video memory, and tells you what it picked.",
            AutoDetectPerformance);
    }

    /// <summary>What the biome texture array will cost with the current Texture Quality and textures.</summary>
    private void DrawBiomeTextureMemory(TerrainGenerator generator)
    {
        if (generator.BiomeDefinitions == null)
            return;
        var textures = new List<Texture2D>();
        foreach (BiomeInstance instance in generator.BiomeDefinitions)
            if (instance?.BiomePrefab != null)
                textures.Add(instance.BiomePrefab.texture);
        if (textures.Count == 0)
            return;

        var quality = (BiomeTextureQuality)serializedObject.FindProperty("biomeTextureQuality").intValue;
        int resolutionSetting = serializedObject.FindProperty("biomeTextureResolution").intValue;
        int resolution = resolutionSetting > 0 ? Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolutionSetting), 64, 4096)
            : Mathf.Clamp(Mathf.RoundToInt(1024f * Mathf.Sqrt((float)generator.ChunkSize / TerrainGenerator.MaxChunkSize)), 512, 2048);
        bool matching = textures.TrueForAll(t => t != null && textures[0] != null && t.width == textures[0].width && t.height == textures[0].height && t.format == textures[0].format && t.mipmapCount == textures[0].mipmapCount);

        string how;
        float megabytes;
        if (quality == BiomeTextureQuality.Automatic && matching)
        {
            megabytes = 0f;
            foreach (Texture2D t in textures)
                megabytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t) / (1024f * 1024f);
            how = $"all {textures.Count} textures are {textures[0].width} x {textures[0].height} {textures[0].format}, so they are copied as imported (no quality loss)";
        }
        else
        {
            bool compressed = quality == BiomeTextureQuality.Compressed;
            megabytes = textures.Count * resolution * resolution * (compressed ? 1f : 4f) * 1.333f / (1024f * 1024f);
            how = (quality == BiomeTextureQuality.Automatic ? "the textures differ in size or format, so they are " : "") +
                  $"resized to {resolution} x {resolution}, {(compressed ? "compressed (DXT5)" : "uncompressed")}";
        }
        EditorGUILayout.LabelField($"   Biome texture array: {how} - about {megabytes:0.#} MB of video memory, shared by every chunk.", EditorStyles.wordWrappedMiniLabel);
    }
}
