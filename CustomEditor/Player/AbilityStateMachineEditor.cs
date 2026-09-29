#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector of the component that stores one player ABILITY KEY: the key's input (edited in place, it lives in the
/// AbilitiesStateMachine list), the ability (asset, preset, inline editing, modifiers), the legacy ability with a
/// one-click conversion, references with auto-assign, checks with Fix buttons, and in Play mode the live phase with a
/// Cast Now test button. Checks are refreshed twice per second, not on every redraw.
/// </summary>
[CustomEditor(typeof(AbilityStateMachine))]
public class AbilityStateMachineEditor : Editor
{
    private static bool showHelp;
    private static bool showChecks = true;
    private static bool showLegacyFields;
    private readonly List<AbilityEditorUI.Issue> issues = new List<AbilityEditorUI.Issue>();
    private readonly AbilityEditorUI.Throttle checks = new AbilityEditorUI.Throttle();
    private System.Action pendingFix;
    private AbilitiesStateMachine abilities; // cached by the throttled refresh

    public override void OnInspectorGUI()
    {
        var m = (AbilityStateMachine)target;
        pendingFix = null;

        if (checks.Due)
        {
            abilities = FindAbilities(m);
            issues.Clear();
            Validate(m, abilities);
        }
        int keyIndex = FindBindingIndex(abilities, m);

        showHelp = AbilityEditorUI.Help(showHelp, "What this component does",
            "It stores ONE ABILITY KEY of the player: the ability (plus modifiers) that the key casts. The key's input is kept " +
            "in the AbilitiesStateMachine list, but you can edit it right here.\n\n" +
            "You can also edit every key at once in the PlayerAbilityController inspector ('Ability Keys'); it is the same data.\n\n" +
            "In Play mode the key gets a live entry in the PlayerAbilityController (cooldown, charges) and its states (Ready, " +
            "Casting, Launching, Active, InCooldown) follow it. An ability the player absorbs from a mob can replace this key's " +
            "ability at runtime (depending on Absorption Settings).");

        serializedObject.Update();
        SerializedProperty holder = serializedObject.FindProperty("abilityHolder");

        // Which key this is.
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        string title = keyIndex >= 0
            ? $"Ability Key {keyIndex + 1}:  {PlayerAbilitySetup.InputLabel(abilities.AbilityActions[keyIndex].abilityActionReference, keyIndex)}  ➜  {PlayerAbilitySetup.AbilityLabel(m)}"
            : $"Not a key yet  ➜  {PlayerAbilitySetup.AbilityLabel(m)}";
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        PlayerAbilityController pc = m.AbilityController;
        if (pc != null && GUILayout.Button(new GUIContent("All Keys", "Select the PlayerAbilityController, which lists every ability key."), EditorStyles.miniButton, GUILayout.Width(60)))
        {
            Selection.activeObject = pc.gameObject;
            EditorGUIUtility.PingObject(pc.gameObject);
        }
        EditorGUILayout.EndHorizontal();

        // 1. Input.
        if (abilities == null)
        {
            EditorGUILayout.LabelField("No AbilitiesStateMachine on the player, so no input can cast this.", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button(new GUIContent("Add AbilitiesStateMachine And Make This A Key", "Add the input component to the player (next to the PlayerAbilityController) and add this as a key.")))
                pendingFix = () =>
                {
                    Transform root = AbilitiesStateMachine.PlayerRoot(m);
                    PlayerAbilityController c = root.GetComponentInChildren<PlayerAbilityController>(true);
                    AbilitiesStateMachine created = Undo.AddComponent<AbilitiesStateMachine>(c != null ? c.gameObject : root.gameObject);
                    PlayerAbilitySetup.AddBinding(created, m, m.AbilityHolder != null ? m.AbilityHolder.AbilityActionReference : null);
                };
        }
        else if (keyIndex < 0)
        {
            EditorGUILayout.LabelField("This is not in the key list yet, so no input casts it.", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button(new GUIContent("Make This A Key", "Add this to the AbilitiesStateMachine key list (it reuses a broken key first).")))
            {
                AbilitiesStateMachine a = abilities;
                pendingFix = () => { Undo.RecordObject(a, "Add Ability Key"); a.AddMissingBindings(); AbilityEditorUI.MarkDirty(a); };
            }
        }
        else
        {
            SerializedObject bso = AbilityEditorUI.Serialized(abilities);
            SerializedProperty inputP = bso.FindProperty("abilityAction").GetArrayElementAtIndex(keyIndex).FindPropertyRelative("abilityActionReference");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(inputP, new GUIContent("Input", "The input action (key / button) that casts this ability."));
            int captured = keyIndex;
            AbilitiesStateMachine capturedB = abilities;
            if (GUILayout.Button(new GUIContent("▾", "Pick an input action from the project."), EditorStyles.miniButton, GUILayout.Width(22)))
                PlayerAbilitySetup.InputMenu(r =>
                {
                    var s2 = new SerializedObject(capturedB);
                    s2.FindProperty("abilityAction").GetArrayElementAtIndex(captured).FindPropertyRelative("abilityActionReference").objectReferenceValue = r;
                    s2.ApplyModifiedProperties();
                });
            EditorGUILayout.EndHorizontal();
            if (inputP.objectReferenceValue == null)
                EditorGUILayout.LabelField($"Empty = uses the '{AbilitiesStateMachine.DefaultActionName(keyIndex)}' action of the player's input actions (if it exists).",
                    EditorStyles.wordWrappedMiniLabel);
            bso.ApplyModifiedProperties();
        }
        EditorGUILayout.EndVertical();

        // 2. Ability.
        AbilityEditorUI.Section("Ability", "An Ability Definition asset plus Modifiers (player-only changes: weaker, stronger, 1 projectile...).");
        SerializedProperty ability = holder.FindPropertyRelative("ability");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(ability, new GUIContent("Ability", "What this key casts. Empty = the legacy Ability Effect below is converted automatically."));
        if (GUILayout.Button(new GUIContent("Preset ▾", "Create a new Ability Definition asset from a preset (saved next to the player prefab) and assign it."), GUILayout.Width(70)))
        {
            AbilityEditorUI.PresetMenu(AbilityEditorUI.FolderFor(m), def =>
            {
                var so = new SerializedObject(m);
                so.FindProperty("abilityHolder").FindPropertyRelative("ability").objectReferenceValue = def;
                so.ApplyModifiedProperties();
            });
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(holder.FindPropertyRelative("modifiers"), new GUIContent("Modifiers", "Changes on top of the ability for this key only."), true);
        var def0 = ability.objectReferenceValue as AbilityDefinition;
        PlayerAbilityHolder h = m.AbilityHolder;
        if (def0 != null)
        {
            EditorGUILayout.LabelField(AbilityEditorUI.AbilityLine(def0, h != null ? h.modifiers : null), EditorStyles.miniLabel);
            if (h != null && h.modifiers != null && !h.modifiers.IsIdentity)
                EditorGUILayout.LabelField("Modifiers: " + h.modifiers.Describe(), EditorStyles.miniLabel);
        }

        // Legacy.
        SerializedProperty legacy = holder.FindPropertyRelative("abilityEffect");
        if (legacy.objectReferenceValue != null && def0 == null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Uses the legacy ability '{legacy.objectReferenceValue.name}' (converted at Play).", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button(new GUIContent("Convert", "Save it as an Ability Definition asset and assign it to Ability (shapes, projectiles, previews, absorption variants)."),
                    EditorStyles.miniButton, GUILayout.Width(60)))
            {
                serializedObject.ApplyModifiedProperties();
                pendingFix = () => ConvertHolder(m);
            }
            EditorGUILayout.EndHorizontal();
        }
        showLegacyFields = EditorGUILayout.Foldout(showLegacyFields,
            new GUIContent("Old ability system", "The legacy Ability Effect and the old holder fields. Only used when Ability above is empty."), true);
        if (showLegacyFields)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(legacy, new GUIContent("Ability Effect (legacy)", "Old AbilityEffectSO, converted automatically at Play when Ability is empty."));
            if (legacy.objectReferenceValue != null &&
                GUILayout.Button(new GUIContent("Convert To Ability Definition ➜ Ability", "Save an Ability Definition asset next to the AbilityEffectSO (attack casts and particle included) and assign it above.")))
            {
                serializedObject.ApplyModifiedProperties();
                pendingFix = () => ConvertHolder(m);
            }
            EditorGUILayout.PropertyField(holder.FindPropertyRelative("abilityActionReference"), new GUIContent("Input (old copy)",
                "Old input field. Only used to fill the key's input when it is empty (Repair). The Input at the top is the one that casts."));
            EditorGUILayout.PropertyField(holder.FindPropertyRelative("attackCast"), new GUIContent("Attack Casts", "Old hit areas, used when converting the legacy ability."), true);
            EditorGUILayout.PropertyField(holder.FindPropertyRelative("particle"), new GUIContent("Particle", "Old effect prefab, used when converting the legacy ability."));
            EditorGUILayout.PropertyField(holder.FindPropertyRelative("activeTime"), new GUIContent("Active Time (old)", "Not used by the new system (the ability asset has its own timings)."));
            EditorGUILayout.PropertyField(holder.FindPropertyRelative("cooldownTime"), new GUIContent("Cooldown (old)", "Not used by the new system."));
            EditorGUILayout.PropertyField(holder.FindPropertyRelative("abilityState"), new GUIContent("State (old)", "Not used by the new system."));
            EditorGUILayout.PropertyField(holder.FindPropertyRelative("targetTransform"), new GUIContent("Target (old)", "Not used by the new system."));
            EditorGUI.indentLevel--;
        }

        // References.
        AbilityEditorUI.Section("References", "Found automatically when empty.");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("abilityController"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animationModel"));
        if (GUILayout.Button(new GUIContent("Auto-assign References", "Fill the PlayerAbilityController and PlayerAnimationModel from this object, its children and parents.")))
            pendingFix = () =>
            {
                Undo.RecordObject(m, "Auto-assign Ability Key References");
                m.AutoAssignReferences();
                AbilityEditorUI.MarkDirty(m);
            };
        serializedObject.ApplyModifiedProperties();

        if (def0 != null)
            AbilityEditorUI.InlineAssetEditor(def0, System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(m) * 7 + 1, "ability",
                "Shared asset: changes apply to every character that uses this ability. Use Modifiers for player-only changes.");

        System.Action fix = AbilityEditorUI.DrawIssues(issues, ref showChecks);
        if (fix != null) pendingFix = fix;

        if (Application.isPlaying)
        {
            DrawLive(m);
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

    private static AbilitiesStateMachine FindAbilities(AbilityStateMachine m)
    {
        AbilitiesStateMachine a = m.GetComponentInParent<AbilitiesStateMachine>();
        if (a == null) a = AbilitiesStateMachine.PlayerRoot(m).GetComponentInChildren<AbilitiesStateMachine>(true);
        return a;
    }

    private static int FindBindingIndex(AbilitiesStateMachine abilities, AbilityStateMachine m)
    {
        if (abilities == null)
            return -1;
        for (int i = 0; i < abilities.AbilityActions.Count; i++)
            if (abilities.AbilityActions[i] != null && abilities.AbilityActions[i].AbilityStateMachine == m)
                return i;
        return -1;
    }

    /// <summary>Converts the key's legacy Ability Effect into an asset and assigns it to the key's Ability.</summary>
    public static void ConvertHolder(AbilityStateMachine m)
    {
        PlayerAbilityHolder h = m.AbilityHolder;
        if (h == null || h.abilityEffect == null)
            return;
        AbilityDefinition def = AbilityEditorUI.ConvertLegacy(h.abilityEffect, false, h.attackCast, h.particle, AbilityEditorUI.FolderFor(m));
        var so = new SerializedObject(m);
        so.FindProperty("abilityHolder").FindPropertyRelative("ability").objectReferenceValue = def;
        so.ApplyModifiedProperties();
        Debug.Log($"[{m.name}] Converted '{h.abilityEffect.name}' into '{def.name}' and assigned it. The legacy Ability Effect is kept but no longer used.", m);
    }

    private void Validate(AbilityStateMachine m, AbilitiesStateMachine abilities)
    {
        PlayerAbilityHolder h = m.AbilityHolder;
        int index = FindBindingIndex(abilities, m);
        AbilityAction binding = index >= 0 ? abilities.AbilityActions[index] : null;
        serializedObject.Update();
        if (m.GetComponentInParent<PlayerAbilityController>() == null && m.GetComponent<PlayerAbilityController>() == null &&
            serializedObject.FindProperty("abilityController").objectReferenceValue == null)
            AbilityEditorUI.Add(issues, MessageType.Error, "No PlayerAbilityController on this object or a parent: this key cannot be cast.");
        if (abilities == null)
            AbilityEditorUI.Add(issues, MessageType.Error, "No AbilitiesStateMachine on the player: no input can cast this.");
        else if (binding == null)
            AbilityEditorUI.Add(issues, MessageType.Warning, "This is not in the key list, so no input casts it.", "Make Key",
                () => { Undo.RecordObject(abilities, "Add Ability Key"); abilities.AddMissingBindings(); AbilityEditorUI.MarkDirty(abilities); });
        else if (binding.abilityActionReference == null)
        {
            string def = AbilitiesStateMachine.DefaultActionName(index);
            UnityEngine.InputSystem.InputActionReference found = PlayerAbilitySetup.FindInputReference(def);
            if (h != null && h.AbilityActionReference != null)
                AbilityEditorUI.Add(issues, MessageType.Warning, "The key has no input, but the old input copy is set.", "Copy Input",
                    () => { Undo.RecordObject(abilities, "Copy Input"); binding.abilityActionReference = h.AbilityActionReference; AbilityEditorUI.MarkDirty(abilities); });
            else if (found != null)
                AbilityEditorUI.Add(issues, MessageType.Warning, $"The key has no input. The project has an '{def}' action for it.", $"Use {def}",
                    () => { Undo.RecordObject(abilities, "Assign Input"); binding.abilityActionReference = found; AbilityEditorUI.MarkDirty(abilities); });
            else
                AbilityEditorUI.Add(issues, MessageType.Info, $"The key has no input: at runtime it uses the '{def}' action if the player's input actions have one. Pick an input above to be sure.");
        }

        if (h == null || (h.ability == null && h.abilityEffect == null))
            AbilityEditorUI.Add(issues, MessageType.Info, "No ability: the key stays empty until the player absorbs one from a mob.");
        if (h != null && h.ability != null)
        {
            var e = new List<string>();
            var w = new List<string>();
            h.ability.Validate(e, w);
            foreach (string s in e) AbilityEditorUI.Add(issues, MessageType.Error, s);
            foreach (string s in w) AbilityEditorUI.Add(issues, MessageType.Warning, s);
            if (h.ability.targeting.mode == AbilityTargetingMode.Unit && h.ability.targeting.autoAimAngle <= 0f)
                AbilityEditorUI.Add(issues, MessageType.Warning, "Unit ability with Auto Aim Angle 0: the player must point the crosshair exactly at the enemy.");
        }
        if (h != null && h.ability != null && h.abilityEffect != null)
            AbilityEditorUI.Add(issues, MessageType.Info, "Both Ability and the legacy Ability Effect are set; only Ability is used.");
    }

    private static void DrawLive(AbilityStateMachine m)
    {
        AbilityEditorUI.Section("Live");
        AbilitySlot s = m.Slot;
        EditorGUILayout.LabelField("State", m.CurrentState != null ? m.CurrentState.StateKey.ToString() : "-");
        EditorGUILayout.LabelField("Key", m.KeyIndex >= 0 ? $"{m.KeyIndex + 1} ({(string.IsNullOrEmpty(m.KeyLabel) ? "no input" : m.KeyLabel)})" : "not a key");
        EditorGUILayout.LabelField("Live entry", m.SlotIndex >= 0 ? $"#{m.SlotIndex} {(s != null ? s.DisplayName : "")}" : "not registered");
        if (s != null && s.ability != null)
            EditorGUILayout.LabelField("Can start", m.ReadyReason == CastFailReason.None ? "yes" : m.ReadyReason.ToString());
        if (s != null && s.ability != null)
        {
            Rect r = GUILayoutUtility.GetRect(18, 18, GUILayout.ExpandWidth(true));
            float fill = s.Phase == AbilityPhase.Ready ? 1f - s.CooldownFraction : s.PhaseProgress;
            EditorGUI.ProgressBar(r, fill, $"{s.Phase}  charges {s.Charges}/{s.MaxCharges}  cd {s.CooldownRemaining:0.0}s");
            if (s.Grant != null)
                EditorGUILayout.LabelField($"Absorbed: {s.Grant.Name} ({s.Grant.tier})", EditorStyles.miniLabel);
            if (GUILayout.Button(new GUIContent("Cast Now", "Test: cast this key as if it was pressed (aimed at the crosshair).")) && m.AbilityController != null)
                m.AbilityController.TryCastFromInput(m.SlotIndex);
        }
    }
}
#endif
