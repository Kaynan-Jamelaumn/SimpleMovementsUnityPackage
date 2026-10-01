#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Armor set inspector: problems (with explanations), piece tools that keep both sides of the piece ↔ set link in sync,
/// a tier simulator ("what is active with N pieces"), and all the fields.
/// </summary>
[CustomEditor(typeof(ArmorSet))]
public class ArmorSetEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private double nextValidation;
    private int simulatedPieces = -1;
    private bool showSimulator = true;

    private ArmorSet Set => (ArmorSet)target;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        ArmorSet set = Set;

        if (EditorApplication.timeSinceStartup >= nextValidation)
        {
            nextValidation = EditorApplication.timeSinceStartup + 0.5;
            errors.Clear();
            warnings.Clear();
            try
            {
                set.Validate(errors, warnings);
                ValidateAcrossAssets(set);
            }
            catch (System.Exception e) { errors.Add("Validation failed: " + e.Message); }
        }
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);

        DrawPieceTools(set);
        EditorGUILayout.Space(2);
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
        DrawSimulator(set);
    }

    // The checks ArmorSet.Validate cannot do by itself: they need the other assets.
    private void ValidateAcrossAssets(ArmorSet set)
    {
        foreach (ArmorSet other in LoadAll<ArmorSet>())
            if (other != set && other.SetName == set.SetName)
            {
                warnings.Add($"Another set ('{AssetDatabase.GetAssetPath(other)}') is also named '{set.SetName}'. Players will not be able to tell them apart.");
                break;
            }
        int unlisted = LoadAll<ArmorSO>().Count(a => a.BelongsToSet == set && !set.ContainsPiece(a));
        if (unlisted > 0)
            warnings.Add($"{unlisted} armor piece(s) point to this set but are not in its Set Pieces, so they do not count. Use 'Add pieces that reference this set'.");
    }

    private void DrawPieceTools(ArmorSet set)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        int pieces = set.SetPieces.Count(p => p != null);
        EditorGUILayout.LabelField($"{set.SetName}: {pieces} piece(s), {set.SetEffects.Count} bonus tier(s), complete at {set.PiecesForCompletion}", EditorStyles.boldLabel);

        foreach (ArmorSO piece in set.SetPieces)
        {
            if (piece == null) continue;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{SlotTypeHelper.GetDisplayName(piece.ArmorSlotType)}", GUILayout.Width(90));
            EditorGUILayout.ObjectField(piece, typeof(ArmorSO), false);
            if (piece.BelongsToSet != set)
            {
                GUIContent fix = new GUIContent("Link", piece.BelongsToSet == null
                    ? "This piece does not reference the set yet. Click to set its 'Belongs To Armor Set'."
                    : $"This piece references '{piece.BelongsToSet.SetName}'. Click to move it to this set.");
                if (GUILayout.Button(fix, GUILayout.Width(44)))
                    LinkPiece(piece, set);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Add pieces that reference this set", "Finds every ArmorSO whose 'Belongs To Armor Set' is this set and lists it.")))
            AddReferencingPieces(set);
        if (GUILayout.Button(new GUIContent("Link all pieces", "Sets 'Belongs To Armor Set' on every listed piece.")))
            foreach (ArmorSO p in set.SetPieces.Where(p => p != null && p.BelongsToSet != set).ToList())
                LinkPiece(p, set);
        if (GUILayout.Button(new GUIContent("Remove empty", "Removes empty entries from Set Pieces and Set Effects.")))
            RemoveEmpty(set);
        EditorGUILayout.EndHorizontal();

        Rect drop = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
        GUI.Box(drop, "Drop armor pieces here to add and link them", EditorStyles.helpBox);
        HandleDrop(drop, set);
        EditorGUILayout.EndVertical();
    }

    private void DrawSimulator(ArmorSet set)
    {
        EditorGUILayout.Space(6);
        showSimulator = EditorGUILayout.Foldout(showSimulator, "Bonus Simulator (what is active with N pieces)", true, EditorStyles.foldoutHeader);
        if (!showSimulator)
            return;
        int max = Mathf.Max(1, set.SetPieces.Count(p => p != null), set.RequiredPiecesForFullSet);
        if (simulatedPieces < 0 || simulatedPieces > max)
            simulatedPieces = Mathf.Min(2, max);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        simulatedPieces = EditorGUILayout.IntSlider("Pieces worn", simulatedPieces, 0, max);
        string state = set.IsSetComplete(simulatedPieces) ? "complete" : set.IsSetActive(simulatedPieces) ? "active" : "inactive";
        EditorGUILayout.LabelField($"Set {state} · {set.GetCompletionPercentage(simulatedPieces) * 100f:0}%", EditorStyles.miniLabel);
        List<ArmorSetEffect> active = set.GetActiveEffects(simulatedPieces);
        foreach (string line in set.DescribeTiers(simulatedPieces))
            EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
        if (active.Count == 0)
            EditorGUILayout.LabelField("No bonus is active.", EditorStyles.miniLabel);
        int next = set.GetNextEffectThreshold(simulatedPieces);
        if (next > simulatedPieces)
            EditorGUILayout.LabelField($"Next bonus at {next} pieces.", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    // ------------------------------------------------------------------ actions
    private static void LinkPiece(ArmorSO piece, ArmorSet set)
    {
        ArmorSet previous = piece.BelongsToSet;
        Undo.RecordObject(piece, "Link armor piece");
        if (previous != null && previous != set)
        {
            Undo.RecordObject(previous, "Link armor piece");
            previous.RemovePiece(piece);
            EditorUtility.SetDirty(previous);
        }
        piece.SetArmorSet(set);
        EditorUtility.SetDirty(piece);
        if (!set.ContainsPiece(piece))
        {
            Undo.RecordObject(set, "Link armor piece");
            set.AddPiece(piece);
            EditorUtility.SetDirty(set);
        }
    }

    private static void AddReferencingPieces(ArmorSet set)
    {
        Undo.RecordObject(set, "Add set pieces");
        int added = 0;
        foreach (ArmorSO a in LoadAll<ArmorSO>())
            if (a.BelongsToSet == set && !set.ContainsPiece(a))
            {
                set.AddPiece(a);
                added++;
            }
        EditorUtility.SetDirty(set);
        Debug.Log(added > 0 ? $"[Armor Sets] Added {added} piece(s) to '{set.SetName}'." : $"[Armor Sets] No unlisted piece references '{set.SetName}'.", set);
    }

    private static void RemoveEmpty(ArmorSet set)
    {
        Undo.RecordObject(set, "Remove empty entries");
        set.SetPieces.RemoveAll(p => p == null);
        set.SetEffects.RemoveAll(e => e == null);
        EditorUtility.SetDirty(set);
    }

    private static void HandleDrop(Rect area, ArmorSet set)
    {
        Event e = Event.current;
        if (!area.Contains(e.mousePosition) || (e.type != EventType.DragUpdated && e.type != EventType.DragPerform))
            return;
        ArmorSO[] dropped = DragAndDrop.objectReferences.OfType<ArmorSO>().ToArray();
        DragAndDrop.visualMode = dropped.Length > 0 ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
        if (e.type == EventType.DragPerform && dropped.Length > 0)
        {
            DragAndDrop.AcceptDrag();
            foreach (ArmorSO a in dropped)
                LinkPiece(a, set);
        }
        e.Use();
    }

    internal static IEnumerable<T> LoadAll<T>() where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null)
                yield return asset;
        }
    }
}
#endif
