using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: the Performance Stats section - a summary of the generation timings and the Generation Stats window.
public partial class TerrainGeneratorEditor
{
    private bool showStatsSection;

    private void DrawStatsSection()
    {
        EditorGUILayout.HelpBox(
            "How long each part of generation takes (Voronoi points, water, heights, erosion, biome map, textures, meshes, objects, " +
            "colliders, NavMesh), recorded while the game runs. The window shows every stage and copies the numbers, optionally " +
            "with your PC's specs and the project and terrain settings.", MessageType.Info);

        double first = GenerationStats.FirstChunkMs;
        GenerationStats.Stage chunk = null, visible = null;
        foreach (GenerationStats.Stage stage in GenerationStats.Snapshot())
        {
            if (stage.Name == GenerationStats.ChunkWorker) chunk = stage;
            else if (stage.Name == GenerationStats.ChunkVisible) visible = stage;
        }
        EditorGUILayout.LabelField("First chunk visible after", first >= 0 ? $"{first / 1000.0:0.00} s" : "-");
        EditorGUILayout.LabelField("Chunk on a worker thread", chunk != null ? $"{chunk.AverageMs:0} ms average ({chunk.Count} chunks)" : "-");
        EditorGUILayout.LabelField("Request to visible", visible != null ? $"{visible.AverageMs:0} ms average, {visible.MaxMs:0} ms at most" : "-");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Open Generation Stats", "Every stage's timings, with copy buttons (stats, PC specs, project settings).")))
            GenerationStatsWindow.Open();
        if (GUILayout.Button(new GUIContent("Copy All", "Copy the stats with your PC specs, project settings and terrain settings.")))
        {
            EditorGUIUtility.systemCopyBuffer = GenerationStatsReport.Full(true, true, true);
            Debug.Log("Generation stats copied to the clipboard.");
        }
        if (GUILayout.Button(new GUIContent("Reset", "Clear the numbers and restart the clock.")))
            GenerationStats.Reset();
        EditorGUILayout.EndHorizontal();
    }
}
