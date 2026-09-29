using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for <see cref="EndlessTerrain"/>: the usual fields, plus a status box that lists what is missing or
/// inconsistent (viewer, TerrainGenerator, portal and mob settings, NavMesh distances), buttons to reset the portal
/// and mob settings to recommended values, and - in Play mode - the portal sites planned around the viewer.
/// </summary>
[CustomEditor(typeof(EndlessTerrain))]
public class EndlessTerrainEditor : Editor
{
    private GameObject portalToAdd;
    private bool showSites;

    public override void OnInspectorGUI()
    {
        var terrain = (EndlessTerrain)target;
        DrawStatus(terrain);

        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Portals & Mobs", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            portalToAdd = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Add Portal Prefab", "Adds this prefab to Portal Settings > Prefabs with recommended values."), portalToAdd, typeof(GameObject), false);
            using (new EditorGUI.DisabledScope(portalToAdd == null))
            {
                if (GUILayout.Button("Add", GUILayout.Width(50f)))
                {
                    AddPortalPrefab(terrain, portalToAdd);
                    portalToAdd = null;
                }
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Recommended Portal Settings", "Resets every Portal Settings value (not the prefab list) to the recommended defaults.")))
            {
                Undo.RecordObject(terrain, "Reset Portal Settings");
                terrain.Portals.ResetToDefaults();
                EditorUtility.SetDirty(terrain);
            }
            if (GUILayout.Button(new GUIContent("Recommended Mob Settings", "Resets every Mob Settings value (not the prefab list or safe zones) to the recommended defaults.")))
            {
                Undo.RecordObject(terrain, "Reset Mob Settings");
                terrain.Mobs.ResetToDefaults();
                EditorUtility.SetDirty(terrain);
            }
        }

        if (Application.isPlaying)
            DrawPlayModeInfo(terrain);
    }

    private static void DrawStatus(EndlessTerrain terrain)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        if (terrain.viewer == null)
            errors.Add("Viewer is empty: assign the player (or its camera) - nothing is generated without it.");
        if (Object.FindAnyObjectByType<TerrainGenerator>() == null)
            errors.Add("No TerrainGenerator in the scene: add one (it holds the world's biomes and generation settings).");

        foreach (string p in terrain.Portals.GetProblems())
            warnings.Add("Portals: " + p);
        foreach (string p in terrain.Mobs.GetProblems(terrain.navMeshDistance, terrain.bakeNavMesh))
            warnings.Add("Mobs: " + p);
        if (terrain.bakeNavMesh && terrain.navMeshDistance > terrain.maxViewDst)
            warnings.Add("NavMesh Distance is beyond Max View Dst: only chunks within the view distance get a NavMesh anyway.");
        if (!HasTag(terrain.Portals.playerTag))
            warnings.Add($"The tag '{terrain.Portals.playerTag}' is not defined (Project Settings > Tags and Layers): spawners fall back to the Viewer.");

        if (errors.Count == 0 && warnings.Count == 0)
        {
            EditorGUILayout.HelpBox("Setup looks complete: viewer, TerrainGenerator, portals and mobs are configured.", MessageType.Info);
            return;
        }
        if (errors.Count > 0)
            EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
        if (warnings.Count > 0)
            EditorGUILayout.HelpBox(string.Join("\n", warnings), MessageType.Warning);
    }

    private static bool HasTag(string tag)
    {
        if (string.IsNullOrEmpty(tag))
            return false;
        foreach (string t in UnityEditorInternal.InternalEditorUtility.tags)
            if (t == tag)
                return true;
        return false;
    }

    /// <summary>Adds a portal prefab to the terrain's Portal Settings with recommended values.</summary>
    public static void AddPortalPrefab(EndlessTerrain terrain, GameObject prefab)
    {
        if (terrain == null || prefab == null)
            return;
        var so = new SerializedObject(terrain);
        SerializedProperty list = so.FindProperty("portalSettings.prefabs");
        if (list == null)
        {
            Debug.LogError("EndlessTerrain has no portalSettings.prefabs list.");
            return;
        }
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue == prefab)
            {
                Debug.Log($"'{prefab.name}' is already in the portal list.", terrain);
                return;
            }
        }
        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        SerializedProperty e = list.GetArrayElementAtIndex(index);
        // A new element copies its neighbour (or is zeroed): set everything explicitly.
        e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        e.FindPropertyRelative("baseSpawnWeight").floatValue = 1f;
        e.FindPropertyRelative("rarityLevel").intValue = 5;
        e.FindPropertyRelative("maxInstances").intValue = 0;
        e.FindPropertyRelative("preferredBiomes").arraySize = 0;
        e.FindPropertyRelative("limitHeight").boolValue = false;
        e.FindPropertyRelative("minPreferredHeight").floatValue = 0f;
        e.FindPropertyRelative("maxPreferredHeight").floatValue = 100f;
        e.FindPropertyRelative("spawnTime").floatValue = 60f;
        e.FindPropertyRelative("minSpawnTime").floatValue = 30f;
        e.FindPropertyRelative("maxSpawnTime").floatValue = 120f;
        e.FindPropertyRelative("shouldHaveRandomSpawnTime").boolValue = false;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(terrain);
        Debug.Log($"Added '{prefab.name}' to {terrain.name}'s Portal Settings > Prefabs.", terrain);
    }

    private void DrawPlayModeInfo(EndlessTerrain terrain)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Play Mode", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Loaded chunks", terrain.LoadedChunkCount.ToString());
        EditorGUILayout.LabelField("World mobs / portals", $"{WorldSpawnRegistry.MobCount} / {WorldSpawnRegistry.PortalCount}");
        showSites = EditorGUILayout.Foldout(showSites, "Portal sites planned within 1500 units of the viewer", true);
        if (showSites && terrain.viewer != null)
        {
            Vector3 v = terrain.viewer.position;
            var sites = new List<PortalSitePlanner.Site>();
            terrain.FindPortalSites(new Vector2(v.x - 1500f, v.z - 1500f), new Vector2(v.x + 1500f, v.z + 1500f), sites);
            sites.Sort((a, b) => (a.Point - new Vector2(v.x, v.z)).sqrMagnitude.CompareTo((b.Point - new Vector2(v.x, v.z)).sqrMagnitude));
            foreach (PortalSitePlanner.Site s in sites)
            {
                float d = Vector2.Distance(s.Point, new Vector2(v.x, v.z));
                string type = s.Type >= 0 && s.Type < terrain.Portals.prefabs.Count && terrain.Portals.prefabs[s.Type].prefab != null ? terrain.Portals.prefabs[s.Type].prefab.name : "?";
                string closed = WorldSpawnRegistry.IsPortalSiteClosed(s.Id) ? " (closed)" : "";
                EditorGUILayout.LabelField($"{s.Id}", $"{type} at ({s.Point.x:0}, {s.Point.y:0}), {d:0} m{closed}");
            }
            if (sites.Count == 0)
                EditorGUILayout.LabelField("None - raise Spawn Chance or lower Region Size.");
        }
        Repaint();
    }
}
