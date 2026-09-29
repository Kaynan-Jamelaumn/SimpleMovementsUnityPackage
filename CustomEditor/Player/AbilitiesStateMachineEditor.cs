#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Player input inspector: the same ABILITY KEYS list as the PlayerAbilityController (input ➜ ability, edited inline),
/// repair / cleanup buttons, references with auto-assign, checks with Fix buttons, and the live Available /
/// Unavailable state. Checks are refreshed twice per second, not on every redraw.
/// </summary>
[CustomEditor(typeof(AbilitiesStateMachine))]
public class AbilitiesStateMachineEditor : Editor
{
    private static bool showHelp;
    private static bool showChecks = true;
    private readonly List<AbilityEditorUI.Issue> issues = new List<AbilityEditorUI.Issue>();
    private readonly AbilityEditorUI.Throttle checks = new AbilityEditorUI.Throttle();
    private System.Action pendingFix;

    public override void OnInspectorGUI()
    {
        var a = (AbilitiesStateMachine)target;
        pendingFix = null;

        if (checks.Due)
        {
            issues.Clear();
            Validate(a);
        }

        showHelp = AbilityEditorUI.Help(showHelp, "What this component does",
            "It listens to the player's ability keys. Each line of its list is one ABILITY KEY: the Input to press and the " +
            "AbilityStateMachine that stores the ability to cast. This is the same list you see in the PlayerAbilityController " +
            "inspector; edit it in either place.\n\n" +
            "While abilities are Available, pressing a key casts it. Abilities become Unavailable while one of them blocks the " +
            "others (see the ability's Casting Rules), or while the player is stunned, silenced or dead (AvailabilityStateMachine).");

        AbilityEditorUI.Section("Ability Keys (press ➜ cast)", "Each key: an Input and the Ability it casts. Same list as in the PlayerAbilityController.");
        int remove = -1;
        bool deleteObject = false;
        for (int i = 0; i < a.AbilityActions.Count; i++)
        {
            if (PlayerAbilitySetup.DrawBindingEditor(a, i, a, out bool del))
            {
                remove = i;
                deleteObject = del;
            }
        }
        if (remove >= 0)
        {
            int captured = remove;
            bool delCaptured = deleteObject;
            pendingFix = () => PlayerAbilitySetup.RemoveBinding(a, captured, delCaptured);
        }
        if (a.AbilityActions.Count == 0)
            EditorGUILayout.LabelField("No ability keys yet.", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Repair Ability Keys", "Fix everything at once: keys not connected to an AbilityStateMachine get an unused one (or are removed), duplicates removed, unused AbilityStateMachines get a key, empty inputs filled (the project's AbilityN action).")))
            pendingFix = () => PlayerAbilitySetup.RepairBindings(a);
        if (GUILayout.Button(new GUIContent("Add Keys For Unused", "Add a key for every AbilityStateMachine under the player that no key uses yet.")))
            pendingFix = () =>
            {
                Undo.RecordObject(a, "Add Ability Keys");
                int n = a.AddMissingBindings();
                AbilityEditorUI.MarkDirty(a);
                Debug.Log($"[{a.name}] Added {n} ability key(s).", a);
            };
        if (GUILayout.Button(new GUIContent("Remove Broken", "Delete keys that are not connected to an AbilityStateMachine.")))
            pendingFix = () =>
            {
                var so = new SerializedObject(a);
                SerializedProperty list = so.FindProperty("abilityAction");
                for (int i = list.arraySize - 1; i >= 0; i--)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("abilityStateMachine").objectReferenceValue == null)
                        list.DeleteArrayElementAtIndex(i);
                so.ApplyModifiedProperties();
            };
        EditorGUILayout.EndHorizontal();
        PlayerAbilitySetup.DrawNewSlotPanel(a);

        serializedObject.Update();
        AbilityEditorUI.Section("Input", "How presses are handled.");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("inputBufferTime"), new GUIContent("Input Buffer (s)",
            "A key pressed up to this many seconds too early (another ability busy, global cooldown, end of its cooldown) is cast as soon as it can. 0 = early presses are ignored."));
        if (Application.isPlaying && a.BufferedKey >= 0)
            EditorGUILayout.LabelField($"Waiting in the buffer: key {a.BufferedKey + 1}", EditorStyles.miniLabel);

        AbilityEditorUI.Section("References", "Found automatically when empty.");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("abilityController"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animationModel"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("availabilityStateMachine"));
        if (GUILayout.Button(new GUIContent("Auto-assign References", "Fill the controller, animation model and availability machine from this object, its children and parents.")))
            pendingFix = () =>
            {
                Undo.RecordObject(a, "Auto-assign Ability References");
                a.AutoAssignReferences();
                AbilityEditorUI.MarkDirty(a);
            };
        DrawPropertiesExcluding(serializedObject, "m_Script", "abilityAction", "abilityController", "animationModel", "availabilityStateMachine", "inputBufferTime");
        serializedObject.ApplyModifiedProperties();

        System.Action fix = AbilityEditorUI.DrawIssues(issues, ref showChecks);
        if (fix != null) pendingFix = fix;

        if (Application.isPlaying)
        {
            AbilityEditorUI.Section("Live");
            EditorGUILayout.LabelField("Abilities", a.CurrentState != null ? a.CurrentState.StateKey.ToString() : "-");
            AvailabilityStateMachine av = a.Availability;
            if (av != null)
                EditorGUILayout.LabelField("Player", $"{(av.CurrentState != null ? av.CurrentState.StateKey.ToString() : "-")}  {av.GetCurrentStatusFlags()}");
            AbilityEditorUI.KeepRepainting(this);
        }

        if (GUI.changed)
            checks.Invalidate();
        if (pendingFix != null)
        {
            System.Action run = pendingFix;
            pendingFix = null;
            run();
            checks.Invalidate();
            GUIUtility.ExitGUI();
        }
    }

    private void Validate(AbilitiesStateMachine a)
    {
        GameObject go = a.gameObject;
        serializedObject.Update();
        if (go.GetComponent<PlayerAbilityController>() == null && go.GetComponentInParent<PlayerAbilityController>() == null &&
            serializedObject.FindProperty("abilityController").objectReferenceValue == null)
            AbilityEditorUI.Add(issues, MessageType.Error, "No PlayerAbilityController on this object or a parent.", "Add", () => Undo.AddComponent<PlayerAbilityController>(go));
        if (go.GetComponent<AvailabilityStateMachine>() == null && go.GetComponentInParent<AvailabilityStateMachine>() == null &&
            serializedObject.FindProperty("availabilityStateMachine").objectReferenceValue == null)
            AbilityEditorUI.Add(issues, MessageType.Warning, "No AvailabilityStateMachine: stuns, silences and death will not block abilities.", "Add",
                () => Undo.AddComponent<AvailabilityStateMachine>(go));

        PlayerAbilitySetup.ValidateBindings(a, issues);
    }
}
#endif
