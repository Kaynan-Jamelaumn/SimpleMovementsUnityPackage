using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.Build;
#endif
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Window > SimpleMovements > Generation Stats: how long each part of terrain generation takes (Voronoi points,
/// water, heights, erosion, biome map, textures, meshes, objects, colliders, NavMesh...), while the game runs,
/// with buttons to copy the numbers - optionally with this PC's specs, the project settings that affect speed and
/// the terrain settings, so a report says everything needed to compare or diagnose it.
/// </summary>
public class GenerationStatsWindow : EditorWindow
{
    private Vector2 scroll;
    private bool includePc = true;
    private bool includeProject = true;
    private bool includeTerrain = true;
    private bool showPc;
    private bool showProject;
    private static GUIStyle rightAligned;

    [MenuItem("Window/SimpleMovements/Generation Stats")]
    public static void Open()
    {
        var window = GetWindow<GenerationStatsWindow>("Generation Stats");
        window.minSize = new Vector2(560f, 320f);
        window.Show();
    }

    private void OnInspectorUpdate()
    {
        if (Application.isPlaying)
            Repaint();
    }

    private void OnGUI()
    {
        if (rightAligned == null)
            rightAligned = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GenerationStats.Enabled = GUILayout.Toggle(GenerationStats.Enabled, new GUIContent("Recording", "Time every stage of terrain generation (costs almost nothing). Off: nothing is recorded."), EditorStyles.toolbarButton, GUILayout.Width(75f));
        if (GUILayout.Button(new GUIContent("Reset", "Clear all numbers and restart the clock (the 'first chunk' times count from here)."), EditorStyles.toolbarButton, GUILayout.Width(50f)))
            GenerationStats.Reset();
        GUILayout.FlexibleSpace();
        includePc = GUILayout.Toggle(includePc, new GUIContent("+ PC specs", "Include this computer's CPU, memory, GPU and OS when copying the stats."), EditorStyles.toolbarButton, GUILayout.Width(80f));
        includeProject = GUILayout.Toggle(includeProject, new GUIContent("+ Project settings", "Include the project settings that affect speed (scripting backend, quality, VSync...) when copying."), EditorStyles.toolbarButton, GUILayout.Width(115f));
        includeTerrain = GUILayout.Toggle(includeTerrain, new GUIContent("+ Terrain settings", "Include the Terrain Generator / Endless Terrain settings that affect speed when copying."), EditorStyles.toolbarButton, GUILayout.Width(115f));
        if (GUILayout.Button(new GUIContent("Copy Stats", "Copy the stats (and whatever is ticked) as text, e.g. to paste in a message."), EditorStyles.toolbarButton, GUILayout.Width(75f)))
            Copy(GenerationStatsReport.Full(includePc, includeProject, includeTerrain), "Stats copied");
        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSummary();
        EditorGUILayout.Space(4);
        DrawTable();
        EditorGUILayout.Space(8);
        DrawTextSection(ref showPc, "Your PC", GenerationStatsReport.PcSpecs(), "Copy PC Specs");
        DrawTextSection(ref showProject, "Project settings that affect speed", GenerationStatsReport.ProjectSettings(), "Copy Project Settings");
        EditorGUILayout.HelpBox(GenerationStatsReport.ProjectSettingsAdvice, MessageType.None);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Open Player Settings"))
            SettingsService.OpenProjectSettings("Project/Player");
        if (GUILayout.Button("Open Quality Settings"))
            SettingsService.OpenProjectSettings("Project/Quality");
        if (GUILayout.Button("Open Editor Settings"))
            SettingsService.OpenProjectSettings("Project/Editor");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    private void DrawSummary()
    {
        double first = GenerationStats.FirstChunkMs, firstObjects = GenerationStats.FirstObjectsMs;
        var line = new StringBuilder();
        line.Append($"Recording for {GenerationStats.SessionSeconds:0.0} s.   First chunk visible after: {(first >= 0 ? (first / 1000.0).ToString("0.00") + " s" : "-")}");
        line.Append($"   First chunk with objects after: {(firstObjects >= 0 ? (firstObjects / 1000.0).ToString("0.00") + " s" : "-")}");
        EditorGUILayout.LabelField(line.ToString(), EditorStyles.boldLabel);

        var counters = new List<string>();
        foreach (KeyValuePair<string, long> pair in GenerationStats.Counters())
            counters.Add($"{pair.Key}: {pair.Value}");
        TerrainGenerator generator = FindAnyObjectByType<TerrainGenerator>();
        if (generator != null && Application.isPlaying)
        {
            counters.Add($"Jobs waiting for a worker: {generator.PendingWorkerJobs}");
            counters.Add($"Results waiting for the main thread: {generator.PendingMainThreadWork}");
        }
        if (counters.Count > 0)
            EditorGUILayout.LabelField(string.Join("    ", counters), EditorStyles.wordWrappedMiniLabel);
        if (!Application.isPlaying && GenerationStats.Snapshot().Count == 0)
            EditorGUILayout.HelpBox("Enter Play Mode: the numbers fill in as chunks generate. Press Reset right before something you want to measure " +
                "(e.g. at the start, or before walking into a new area). Times measured in the Editor are slower than in a build.", MessageType.Info);
    }

    private void DrawTable()
    {
        List<GenerationStats.Stage> stages = GenerationStats.Snapshot();
        if (stages.Count == 0)
            return;
        double slowest = 0.0;
        foreach (GenerationStats.Stage stage in stages)
            slowest = Math.Max(slowest, stage.AverageMs);

        const float count = 55f, number = 70f;
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Stage", EditorStyles.miniBoldLabel);
        GUILayout.Label("Count", EditorStyles.miniBoldLabel, GUILayout.Width(count));
        GUILayout.Label(new GUIContent("Avg ms", "Average time of one run of this stage."), EditorStyles.miniBoldLabel, GUILayout.Width(number));
        GUILayout.Label("Min ms", EditorStyles.miniBoldLabel, GUILayout.Width(number));
        GUILayout.Label("Max ms", EditorStyles.miniBoldLabel, GUILayout.Width(number));
        GUILayout.Label("Last ms", EditorStyles.miniBoldLabel, GUILayout.Width(number));
        GUILayout.Label(new GUIContent("Total s", "All runs added up (stages on worker threads run side by side, so these add up to more than the time passed)."), EditorStyles.miniBoldLabel, GUILayout.Width(number));
        EditorGUILayout.EndHorizontal();

        string group = null;
        foreach (GenerationStats.Stage stage in stages)
        {
            if (stage.Group != group)
            {
                group = stage.Group;
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField(group, EditorStyles.boldLabel);
            }
            Rect row = EditorGUILayout.BeginHorizontal();
            if (slowest > 0.0 && Event.current.type == EventType.Repaint)
            {
                // A bar behind the row: how slow this stage is on average compared to the slowest one.
                var bar = new Rect(row.x, row.y + 1f, row.width * (float)(stage.AverageMs / slowest), row.height - 2f);
                EditorGUI.DrawRect(bar, new Color(0.3f, 0.55f, 0.95f, 0.18f));
            }
            GUILayout.Label(new GUIContent(stage.Name, stage.Help ?? GenerationStats.Describe(stage.Name)));
            GUILayout.Label(stage.Count.ToString(), rightAligned, GUILayout.Width(count));
            GUILayout.Label(stage.AverageMs.ToString("0.00"), rightAligned, GUILayout.Width(number));
            GUILayout.Label((stage.Count > 0 ? stage.MinMs : 0.0).ToString("0.00"), rightAligned, GUILayout.Width(number));
            GUILayout.Label(stage.MaxMs.ToString("0.00"), rightAligned, GUILayout.Width(number));
            GUILayout.Label(stage.LastMs.ToString("0.00"), rightAligned, GUILayout.Width(number));
            GUILayout.Label((stage.TotalMs / 1000.0).ToString("0.00"), rightAligned, GUILayout.Width(number));
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawTextSection(ref bool open, string title, string text, string copyLabel)
    {
        EditorGUILayout.BeginHorizontal();
        open = EditorGUILayout.Foldout(open, title, true);
        if (GUILayout.Button(copyLabel, EditorStyles.miniButton, GUILayout.Width(150f)))
            Copy(text, copyLabel.Replace("Copy ", "") + " copied");
        EditorGUILayout.EndHorizontal();
        if (open)
            EditorGUILayout.HelpBox(text.TrimEnd(), MessageType.None);
    }

    private void Copy(string text, string message)
    {
        EditorGUIUtility.systemCopyBuffer = text;
        ShowNotification(new GUIContent(message));
    }
}

/// <summary>The text the Generation Stats window copies: stats, PC specs, project settings, terrain settings.</summary>
public static class GenerationStatsReport
{
    public const string ProjectSettingsAdvice =
        "What you can change for speed: Player > Other Settings > Scripting Backend: IL2CPP makes builds 2-3x faster at generating " +
        "(the Editor always uses Mono). Managed Stripping Level and API Compatibility don't change speed. Incremental GC smooths out " +
        "garbage-collection hitches. Quality: VSync / Target Frame Rate cap the frame rate (not generation); shadow distance and " +
        "anti-aliasing cost rendering time. Editor > Enter Play Mode Settings: turning off domain reload makes entering Play Mode " +
        "much faster (static caches are cleared by the terrain on start). In the Terrain Generator: Worker Threads, Main Thread " +
        "Budget, Object Spawn Budget, River Max Length, Level Of Detail / distance LOD and the view distance matter most.";

    public static string Full(bool pc, bool project, bool terrain)
    {
        var text = new StringBuilder();
        text.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}, {(Application.isPlaying ? "Play Mode" : "Edit Mode")} in Unity {Application.unityVersion} (Editor: slower than a build)");
        text.AppendLine();
        text.Append(GenerationStats.Report());
        if (pc)
        {
            text.AppendLine();
            text.Append(PcSpecs());
        }
        if (project)
        {
            text.AppendLine();
            text.Append(ProjectSettings());
        }
        if (terrain)
        {
            text.AppendLine();
            text.Append(TerrainSettings());
        }
        return text.ToString();
    }

    public static string PcSpecs()
    {
        var text = new StringBuilder();
        text.AppendLine("=== PC ===");
        text.AppendLine($"OS: {SystemInfo.operatingSystem}");
        text.AppendLine($"CPU: {SystemInfo.processorType}");
        text.AppendLine($"CPU threads: {SystemInfo.processorCount} (clock {SystemInfo.processorFrequency} MHz)");
        text.AppendLine($"RAM: {SystemInfo.systemMemorySize} MB");
        text.AppendLine($"GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB, {SystemInfo.graphicsDeviceType}, shader level {SystemInfo.graphicsShaderLevel})");
        text.AppendLine($"Screen: {Screen.currentResolution.width}x{Screen.currentResolution.height}");
        text.AppendLine($"Device: {SystemInfo.deviceModel}");
        return text.ToString();
    }

    public static string ProjectSettings()
    {
        var text = new StringBuilder();
        text.AppendLine("=== Project settings ===");
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
        text.AppendLine($"Unity: {Application.unityVersion}");
        text.AppendLine($"Build target: {target}{(EditorUserBuildSettings.development ? " (development build)" : "")}");
#if UNITY_2021_2_OR_NEWER
        NamedBuildTarget named = NamedBuildTarget.FromBuildTargetGroup(group);
        ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(named);
        text.AppendLine($"Scripting backend: {backend}{(backend == ScriptingImplementation.IL2CPP ? $" ({PlayerSettings.GetIl2CppCompilerConfiguration(named)})" : "")}");
        text.AppendLine($"API compatibility: {PlayerSettings.GetApiCompatibilityLevel(named)}");
        text.AppendLine($"Managed stripping: {PlayerSettings.GetManagedStrippingLevel(named)}");
#else
        ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(group);
        text.AppendLine($"Scripting backend: {backend}");
        text.AppendLine($"API compatibility: {PlayerSettings.GetApiCompatibilityLevel(group)}");
        text.AppendLine($"Managed stripping: {PlayerSettings.GetManagedStrippingLevel(group)}");
#endif
        text.AppendLine($"Incremental GC: {PlayerSettings.gcIncremental}");
        text.AppendLine($"Color space: {PlayerSettings.colorSpace}");
        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
        text.AppendLine($"Render pipeline: {(pipeline != null ? pipeline.GetType().Name + " (" + pipeline.name + ")" : "Built-in")}");
        int level = QualitySettings.GetQualityLevel();
        string[] names = QualitySettings.names;
        text.AppendLine($"Quality level: {(level >= 0 && level < names.Length ? names[level] : level.ToString())}");
        text.AppendLine($"VSync count: {QualitySettings.vSyncCount}, target frame rate: {Application.targetFrameRate}");
        text.AppendLine($"Shadows: {QualitySettings.shadows}, distance {QualitySettings.shadowDistance}; anti-aliasing {QualitySettings.antiAliasing}x; LOD bias {QualitySettings.lodBias}");
        text.AppendLine($"Fixed timestep: {Time.fixedDeltaTime} s");
        // (Read by reflection: newer Unity versions retire the on/off switch and only keep the options.)
        var enabledProperty = typeof(EditorSettings).GetProperty("enterPlayModeOptionsEnabled");
        bool optionsOn = enabledProperty == null || (bool)enabledProperty.GetValue(null);
        text.AppendLine($"Enter Play Mode options: {(optionsOn ? EditorSettings.enterPlayModeOptions.ToString() : "off (full domain and scene reload)")}");
        text.AppendLine($"Unity job worker threads: {Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount}");
        text.AppendLine($"Burst package: {(Type.GetType("Unity.Burst.BurstCompiler, Unity.Burst") != null ? "installed" : "not installed")}");
        return text.ToString();
    }

    public static string TerrainSettings()
    {
        var text = new StringBuilder();
        text.AppendLine("=== Terrain settings ===");
        TerrainGenerator tg = UnityEngine.Object.FindAnyObjectByType<TerrainGenerator>();
        if (tg == null)
        {
            text.AppendLine("(no Terrain Generator in the open scene)");
            return text.ToString();
        }
        text.AppendLine($"Terrain size: {tg.TerrainSizeValue} (chunk {tg.ChunkSize} cells), scale {tg.ScaleFactor}");
        text.AppendLine($"Worker threads: {tg.WorkerThreads} (default for this PC {TerrainWorkerPool.DefaultThreadCount}); main thread budget {tg.MainThreadBudgetMs} ms; object spawn budget {tg.ObjectSpawnBudgetMs} ms");
        text.AppendLine($"Level of detail: {tg.LevelOfDetail}; distance LOD {(tg.DistanceLod ? $"on (full detail to {tg.LodFullDetailDistance}, step {tg.LodDistanceStep}, max level {tg.LodMaxLevel})" : "off")}");
        text.AppendLine($"Biomes: {(tg.BiomeDefinitions != null ? tg.BiomeDefinitions.Length : 0)}, Voronoi scale {tg.VoronoiScale}, points per cell {tg.NumVoronoiPoints}, octaves {tg.Octaves}, shape mode {tg.TerrainShapeMode}");
        text.AppendLine($"Water: {(tg.EnableWater ? "on" : "off")}; oceans {tg.EnableOceans}, lakes {tg.EnableLakes}, ponds {tg.EnablePonds}, rivers {tg.EnableRivers} (max length {tg.RiverMaxLength})");
        text.AppendLine($"Erosion: {(tg.EnableErosion ? "on" : "off")}");
        text.AppendLine($"Objects: {(tg.ShouldSpawnObjects ? "on" : "off")}, pooling {tg.PoolObjects}; meshes prepared on workers {tg.PrepareMeshesOnWorkers}; textures per pixel {tg.SplatTexturesPerPixel}");
        EndlessTerrain endless = UnityEngine.Object.FindAnyObjectByType<EndlessTerrain>();
        if (endless != null)
            text.AppendLine($"Endless Terrain: view distance {endless.maxViewDst}, unload distance {(endless.unloadDistance > 0f ? endless.unloadDistance.ToString() : "auto")}, data cache {endless.chunkDataCacheSize} chunks, NavMesh {(endless.bakeNavMesh ? "on" : "off")}");
        return text.ToString();
    }
}
