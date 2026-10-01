#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector of the Armor Sets window: shows what is not wired yet and builds / repairs the whole window
/// (canvas included when the player has none) with one button. Play Mode: open / close the window.
/// </summary>
[CustomEditor(typeof(ArmorSetUIManager))]
public class ArmorSetUIManagerEditor : Editor
{
    private static readonly string[] Required =
    {
        "armorSetManager", "inventoryManager", "armorSetCanvas", "windowPanel", "setInfoPanel", "setListPanel", "setEffectPanel",
        "setNameText", "setDescriptionText", "setIconImage", "setProgressSlider", "setProgressText",
        "activeEffectsContainer", "availableEffectsContainer", "setEffectPrefab", "setListContainer", "setListItemPrefab",
        "equipmentSlotsContainer", "equipmentSlotPrefab", "uiAudioSource",
    };

    public override void OnInspectorGUI()
    {
        var ui = (ArmorSetUIManager)target;
        serializedObject.Update();

        var missing = new List<string>();
        foreach (string name in Required)
        {
            SerializedProperty p = serializedObject.FindProperty(name);
            if (p != null && p.objectReferenceValue == null)
                missing.Add(p.displayName);
        }

        EditorGUILayout.HelpBox(
            "Shows the armor sets the player is wearing: the list of worn sets, the selected set (icon, name, pieces worn, " +
            "description), its pieces and which bonuses are active or still to unlock. Opens with the Sets button of the " +
            "inventory or the 'ArmorSetUI' input action, and by itself when a set is completed.", MessageType.None);

        if (missing.Count == 0)
            EditorGUILayout.HelpBox("Everything is assigned.", MessageType.Info);
        else
            EditorGUILayout.HelpBox("Not assigned: " + string.Join(", ", missing) +
                                    (missing.Count > 3 ? "\nPress Build / Repair UI to create and wire the window." : ""),
                                    missing.Count > 3 ? MessageType.Warning : MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button(new GUIContent("Build / Repair UI",
                        "Creates what is missing - window, set list, set details, piece slots, bonus lists, close button, the three entry " +
                        "prefabs, audio source, a Sets button on the inventory, and a canvas in the player when there is none - and " +
                        "assigns every field. Existing parts are kept."), GUILayout.Height(26)))
                {
                    InventoryUIBuilder.BuildArmorSetUIFor(ui);
                    GUIUtility.ExitGUI();
                }
            }
            var window = serializedObject.FindProperty("windowPanel").objectReferenceValue as GameObject;
            using (new EditorGUI.DisabledScope(window == null))
            {
                if (GUILayout.Button(new GUIContent("Select Window", "Selects the window in the Hierarchy."), GUILayout.Height(26)))
                    EditorGUIUtility.PingObject(Selection.activeObject = window);
                if (!Application.isPlaying && window != null &&
                    GUILayout.Button(new GUIContent(window.activeSelf ? "Hide Window" : "Show Window",
                        "Shows the window while editing so you can arrange it (it is hidden again when the game starts)."), GUILayout.Height(26)))
                {
                    Undo.RecordObject(window, "Toggle Armor Sets Window");
                    window.SetActive(!window.activeSelf);
                }
            }
        }

        if (Application.isPlaying)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(ui.IsVisible ? "Close" : "Open")) ui.ToggleUI();
            }
        }

        EditorGUILayout.Space();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
