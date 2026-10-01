#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Faction inspector: the faction's settings, then how it stands toward every other faction of the project (both
/// directions, so one-sided or contradictory lists are visible) and how it treats characters without a faction.
/// </summary>
[CustomEditor(typeof(CombatFaction))]
public class CombatFactionEditor : Editor
{
    private List<CombatFaction> all;
    private double nextScan;

    public override void OnInspectorGUI()
    {
        var faction = (CombatFaction)target;
        EditorGUILayout.HelpBox("A side in the world. Characters get it on their Combat Entity or Mob. Factions decide ally / enemy / " +
                                "neutral between characters of different teams; they have nothing to do with physics layers. " +
                                "Relations are symmetric: listing a faction on one side is enough.", MessageType.None);
        DrawDefaultInspector();

        if (all == null || EditorApplication.timeSinceStartup >= nextScan)
        {
            nextScan = EditorApplication.timeSinceStartup + 2.0;
            all = new List<CombatFaction>();
            foreach (string guid in AssetDatabase.FindAssets("t:CombatFaction"))
            {
                var f = AssetDatabase.LoadAssetAtPath<CombatFaction>(AssetDatabase.GUIDToAssetPath(guid));
                if (f != null) all.Add(f);
            }
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Relations", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        Row("Characters without a faction", faction.StanceTowards(null), null, faction);
        foreach (CombatFaction other in all)
        {
            if (other == null || other == faction) continue;
            Row(other.Name, faction.StanceTowards(other), other, faction);
        }
        if (all.Count <= 1)
            EditorGUILayout.LabelField("No other faction in the project yet.", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        // Contradictions: listed as ally on one side and enemy on the other
        foreach (CombatFaction other in all)
        {
            if (other == null || other == faction) continue;
            bool allyHere = faction.allies != null && faction.allies.Contains(other);
            bool enemyThere = other.enemies != null && other.enemies.Contains(faction);
            bool enemyHere = faction.enemies != null && faction.enemies.Contains(other);
            bool allyThere = other.allies != null && other.allies.Contains(faction);
            if ((allyHere && enemyThere) || (enemyHere && allyThere))
                EditorGUILayout.HelpBox($"{faction.Name} and {other.Name} list each other as ally on one side and enemy on the other: they are enemies (enemy wins).", MessageType.Warning);
        }
        if (faction.allies != null && faction.enemies != null)
            foreach (CombatFaction f in faction.allies)
                if (f != null && faction.enemies.Contains(f))
                    EditorGUILayout.HelpBox($"{f.Name} is in both Allies and Enemies: it is an enemy.", MessageType.Warning);
    }

    private static void Row(string label, FactionStance stance, CombatFaction other, CombatFaction self)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            Color c = stance == FactionStance.Enemy ? new Color(0.95f, 0.4f, 0.35f) : stance == FactionStance.Ally ? new Color(0.45f, 0.85f, 0.45f) : new Color(0.75f, 0.75f, 0.75f);
            Rect dot = GUILayoutUtility.GetRect(10f, 10f, GUILayout.Width(10f), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUI.DrawRect(new Rect(dot.x, dot.y + 4f, 8f, 8f), c);
            EditorGUILayout.LabelField(label, stance.ToString());
            if (other != null && GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(52f)))
                Selection.activeObject = other;
        }
    }
}
#endif
