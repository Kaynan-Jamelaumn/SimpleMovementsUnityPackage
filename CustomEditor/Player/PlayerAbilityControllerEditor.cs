#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Player ability inspector: the player's ABILITY KEYS (input ➜ ability, edited inline), aiming / preview / casting
/// settings with auto-assign buttons, setup checks with Fix buttons, and in Play mode the live cooldowns, the
/// click-to-confirm state and absorbed abilities. Checks are refreshed twice per second, not on every redraw.
/// </summary>
[CustomEditor(typeof(PlayerAbilityController))]
public class PlayerAbilityControllerEditor : AbilityCasterEditor
{
    private static bool showHelp;
    private static bool showChecks = true;
    private readonly List<AbilityEditorUI.Issue> issues = new List<AbilityEditorUI.Issue>();
    private readonly AbilityEditorUI.Throttle checks = new AbilityEditorUI.Throttle();
    private System.Action pendingFix;

    // Cached by the throttled refresh (searching the hierarchy on every redraw made the inspector slow).
    private AbilitiesStateMachine abilities;
    private int machineCount;
    private bool hasStatus, hasAnimationModel, hasAvailability;

    private static readonly string[] Handled =
    {
        "m_Script", "slots", "maxSlots", "castPoint", "animator", "globalCooldown", "turnSpeed", "showTelegraphs", "debugLog",
        "aimCamera", "aimLayers", "maxAimDistance", "aimWithMouseWhenCursorFree", "snapTurnToAim", "previewColor", "cancelCastInput",
    };

    public override void OnInspectorGUI()
    {
        var c = (PlayerAbilityController)target;
        pendingFix = null;

        if (checks.Due)
            Refresh(c);

        showHelp = AbilityEditorUI.Help(showHelp, "How the player's abilities work (read me)",
            "You only configure ABILITY KEYS: one per key the player can press. A key = an Input (which key) + an Ability " +
            "(what it casts) + optional Modifiers. Set them in 'Ability Keys' below. That is all.\n\n" +
            "Behind the scenes Unity stores each key in two components, which is why you saw two 'slot' lists before:\n" +
            "• AbilityStateMachine (on a child under 'Ability Keys'): stores the key's ABILITY and modifiers.\n" +
            "• AbilitiesStateMachine (one on the player): its list stores each key's INPUT and points to its AbilityStateMachine.\n" +
            "Both are edited together here, so you never have to keep them in sync by hand.\n\n" +
            "This component is the CASTER: aiming, cooldowns, mana, previews. Its internal slot list is NOT a setting: when you " +
            "press Play it copies every key into a live entry (cooldown, charges, absorbed ability), shown under 'Live' while playing.\n\n" +
            "Abilities aim at the camera crosshair (or the mouse when the cursor is free). Abilities with 'Player Confirms Target' " +
            "show a preview first: left click confirms, right click / Escape cancels (that click does not also use your held item). " +
            "AvailabilityStateMachine blocks abilities while stunned, silenced or dead.\n\n" +
            "INPUT: a key pressed a little too early (another ability busy, global cooldown) is cast as soon as it can (Input Buffer on " +
            "the AbilitiesStateMachine). 'Hold To Repeat' on a key keeps casting while held. 'Cancel Input' (Casting section) stops a " +
            "wind-up. For hotbar UI, each AbilityStateMachine has KeyLabel, Icon, CooldownRemaining, Charges and CastNow().");

        DrawComponents();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Auto-assign References", "Animator from the PlayerAnimationModel (or the first Animator in the children), aim camera = Main Camera.")))
            pendingFix = () =>
            {
                Undo.RecordObject(c, "Auto-assign Player Ability References");
                c.AutoAssignReferences();
                AbilityEditorUI.MarkDirty(c);
            };
        using (new EditorGUI.DisabledScope(abilities == null))
        {
            if (GUILayout.Button(new GUIContent("Repair Ability Keys", "Fix everything at once: keys not connected to an AbilityStateMachine get an unused one (or are removed), duplicates removed, unused AbilityStateMachines get a key, empty inputs filled (the project's AbilityN action).")))
            {
                AbilitiesStateMachine a = abilities;
                pendingFix = () => PlayerAbilitySetup.RepairBindings(a);
            }
        }
        EditorGUILayout.EndHorizontal();

        System.Action fix = AbilityEditorUI.DrawIssues(issues, ref showChecks);
        if (fix != null) pendingFix = fix;

        if (Application.isPlaying)
            DrawLive(c);
        else
            DrawKeys(c);

        serializedObject.Update();
        DrawAiming();
        DrawPreview();
        DrawCasting(c);
        DrawPropertiesExcluding(serializedObject, Handled);
        serializedObject.ApplyModifiedProperties();

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
        AbilityEditorUI.KeepRepainting(this);
    }

    // ------------------------------------------------------------------ cached state + checks
    private void Refresh(PlayerAbilityController c)
    {
        GameObject go = c.gameObject;
        abilities = PlayerAbilitySetup.FindBindings(c);
        Transform root = AbilitiesStateMachine.PlayerRoot(go.transform);
        AbilityStateMachine[] machines = root.GetComponentsInChildren<AbilityStateMachine>(true);
        machineCount = machines.Length;
        hasStatus = go.GetComponentInParent<PlayerStatusController>() != null || root.GetComponentInChildren<PlayerStatusController>(true) != null;
        hasAnimationModel = go.GetComponentInParent<PlayerAnimationModel>() != null || root.GetComponentInChildren<PlayerAnimationModel>(true) != null;
        hasAvailability = go.GetComponentInParent<AvailabilityStateMachine>() != null || root.GetComponentInChildren<AvailabilityStateMachine>(true) != null;

        issues.Clear();
        Validate(c, machines);
    }

    private void Validate(PlayerAbilityController c, AbilityStateMachine[] machines)
    {
        GameObject go = c.gameObject;
        if (abilities == null)
            AbilityEditorUI.Add(issues, MessageType.Error, "No AbilitiesStateMachine on the player: key presses never cast anything.", "Add", () => Undo.AddComponent<AbilitiesStateMachine>(go));
        if (machines.Length == 0 && !Application.isPlaying)
            AbilityEditorUI.Add(issues, MessageType.Warning, "The player has no ability keys yet. Use '＋ New Ability Key' below.");

        if (abilities != null)
            PlayerAbilitySetup.ValidateBindings(abilities, issues);

        var e = new List<string>();
        var w = new List<string>();
        foreach (AbilityStateMachine m in machines)
        {
            PlayerAbilityHolder h = m.AbilityHolder;
            if (h == null || (h.ability == null && h.abilityEffect == null))
                AbilityEditorUI.Add(issues, MessageType.Info, $"'{m.name}' has no ability (the key stays empty until the player absorbs one).", "Select", () => Selection.activeObject = m);
            else if (h.ability == null && h.abilityEffect != null)
            {
                AbilityStateMachine captured = m;
                AbilityEditorUI.Add(issues, MessageType.Info, $"'{m.name}' uses the legacy ability '{h.abilityEffect.name}'. Convert it to an Ability Definition to edit shapes, projectiles and previews.",
                    "Convert", () => AbilityStateMachineEditor.ConvertHolder(captured));
            }
            else if (h.ability != null)
            {
                e.Clear();
                w.Clear();
                h.ability.Validate(e, w);
                foreach (string s in e) AbilityEditorUI.Add(issues, MessageType.Error, $"'{m.name}' ▸ {s}");
            }
        }

        if (c.Animator == null && !hasAnimationModel && c.GetComponentInChildren<Animator>() == null)
            AbilityEditorUI.Add(issues, MessageType.Warning, "No Animator found: cast/release animation triggers are skipped.");
        if (c.AssignedAimCamera == null && Camera.main == null && go.scene.IsValid() && !EditorUtility.IsPersistent(go))
            AbilityEditorUI.Add(issues, MessageType.Warning, "No aim camera assigned and no camera tagged MainCamera in the scene: abilities aim straight ahead.");

        Collider own = c.GetComponentInParent<Collider>();
        if (own == null) own = c.GetComponentInChildren<Collider>();
        serializedObject.Update();
        SerializedProperty layers = serializedObject.FindProperty("aimLayers");
        if (own != null && own.gameObject.layer != 0 && (layers.intValue & (1 << own.gameObject.layer)) != 0)
        {
            int layer = own.gameObject.layer;
            AbilityEditorUI.Add(issues, MessageType.Warning, $"Aim Layers include the player's own layer '{LayerMask.LayerToName(layer)}': the aim ray may stop on the player.", "Remove Layer",
                () => { serializedObject.Update(); serializedObject.FindProperty("aimLayers").intValue &= ~(1 << layer); serializedObject.ApplyModifiedProperties(); });
        }
        int manual = serializedObject.FindProperty("slots").arraySize;
        if (manual > 0 && !Application.isPlaying)
            AbilityEditorUI.Add(issues, MessageType.Warning,
                $"{manual} entr{(manual == 1 ? "y was" : "ies were")} typed into this component's internal slot list. That list is filled automatically at Play from the Ability Keys; manual entries have no input and cannot be cast.",
                "Clear", () => { serializedObject.Update(); serializedObject.FindProperty("slots").arraySize = 0; serializedObject.ApplyModifiedProperties(); });
    }

    // ------------------------------------------------------------------ components
    private void DrawComponents()
    {
        AbilityEditorUI.Section("Components");
        AbilityEditorUI.StatusRow(abilities != null, "AbilitiesStateMachine (turns key presses into casts)", "Stores the input of every ability key. Required.");
        AbilityEditorUI.StatusRow(machineCount > 0, $"{machineCount} ability key(s) (AbilityStateMachine components)", "One per key, on child objects under 'Ability Keys'.");
        AbilityEditorUI.StatusRow(hasAvailability, "AvailabilityStateMachine", "Blocks abilities while stunned, silenced or dead.");
        AbilityEditorUI.StatusRow(hasStatus, "PlayerStatusController", "Health, mana and stamina (ability costs, damage taken).");
        AbilityEditorUI.StatusRow(hasAnimationModel, "PlayerAnimationModel", "Holds the player's Animator (cast / release triggers).");
    }

    // ------------------------------------------------------------------ keys
    private void DrawKeys(PlayerAbilityController c)
    {
        AbilityEditorUI.Section("Ability Keys (press ➜ cast)",
            "Everything the player can cast. Each key: an Input and the Ability it casts. Edit them right here.");
        if (abilities == null)
        {
            EditorGUILayout.HelpBox("No AbilitiesStateMachine yet. Creating a key below adds one.", MessageType.Info);
        }
        else
        {
            int remove = -1;
            bool deleteObject = false;
            for (int i = 0; i < abilities.AbilityActions.Count; i++)
            {
                if (PlayerAbilitySetup.DrawBindingEditor(abilities, i, c, out bool del))
                {
                    remove = i;
                    deleteObject = del;
                }
            }
            if (remove >= 0)
            {
                AbilitiesStateMachine a = abilities;
                int captured = remove;
                bool delCaptured = deleteObject;
                pendingFix = () => PlayerAbilitySetup.RemoveBinding(a, captured, delCaptured);
            }
            if (abilities.AbilityActions.Count == 0)
                EditorGUILayout.LabelField("No ability keys yet.", EditorStyles.miniLabel);
        }
        PlayerAbilitySetup.DrawNewSlotPanel(c);
    }

    // ------------------------------------------------------------------ settings
    private void DrawAiming()
    {
        AbilityEditorUI.Section("Aiming", "Abilities aim where the camera crosshair points (or the mouse when the cursor is free). Unit abilities auto-aim at the best enemy near the crosshair.");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("aimCamera"));
        if (GUILayout.Button(new GUIContent("Main", "Assign the camera tagged MainCamera."), GUILayout.Width(50)))
        {
            if (Camera.main != null)
                serializedObject.FindProperty("aimCamera").objectReferenceValue = Camera.main;
            else
                Debug.LogWarning("No camera tagged MainCamera in the open scene.");
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("aimLayers"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxAimDistance"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("aimWithMouseWhenCursorFree"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("snapTurnToAim"));
    }

    private void DrawPreview()
    {
        AbilityEditorUI.Section("Click-To-Confirm Preview",
            "Abilities with Targeting ▸ Player Confirms Target show their area first; left click casts, right click or Escape cancels.");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("previewColor"));
    }

    private void DrawCasting(PlayerAbilityController c)
    {
        AbilityEditorUI.Section("Casting");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("castPoint"), new GUIContent("Cast Point",
            "Where projectiles and beams come out (hand, staff tip). Empty = in front of the chest."));
        if (GUILayout.Button(new GUIContent("Create", "Create a 'CastPoint' child at chest height in front of the player and assign it."), GUILayout.Width(60)))
        {
            var cp = new GameObject("CastPoint");
            Undo.RegisterCreatedObjectUndo(cp, "Create Cast Point");
            cp.transform.SetParent(c.transform, false);
            CharacterController cc = c.GetComponentInParent<CharacterController>();
            float h = cc != null ? cc.height : 1.8f;
            cp.transform.localPosition = new Vector3(0.25f, h * 0.72f, 0.45f);
            serializedObject.FindProperty("castPoint").objectReferenceValue = cp.transform;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animator"), new GUIContent("Animator",
            "Receives the abilities' cast/release triggers. Empty = the PlayerAnimationModel's Animator."));
        if (GUILayout.Button(new GUIContent("Find", "Assign the PlayerAnimationModel's Animator (or the first Animator in the children)."), GUILayout.Width(60)))
        {
            PlayerAnimationModel model = c.GetComponentInChildren<PlayerAnimationModel>();
            Animator a = model != null ? model.GetComponentInChildren<Animator>() : null;
            if (a == null) a = c.GetComponentInChildren<Animator>();
            serializedObject.FindProperty("animator").objectReferenceValue = a;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cancelCastInput"), new GUIContent("Cancel Input",
            "Optional: cancels the ability being wound up (Casting phase) and closes click-to-confirm previews. The ability's own interrupt rules apply (refund, charge back). Empty = none."));
        if (GUILayout.Button(new GUIContent("▾", "Pick an input action from the project's Input Actions assets."), EditorStyles.miniButton, GUILayout.Width(22)))
        {
            SerializedObject so = serializedObject;
            PlayerAbilitySetup.InputMenu(r => { so.Update(); so.FindProperty("cancelCastInput").objectReferenceValue = r; so.ApplyModifiedProperties(); });
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("globalCooldown"), new GUIContent("Global Cooldown (s)", "Minimum time between the start of two abilities."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("turnSpeed"), new GUIContent("Turn Speed (°/s)", "How fast the player turns toward the aim while casting."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxSlots"), new GUIContent("Max Ability Keys", "0 = no limit."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showTelegraphs"), new GUIContent("Show Aim Previews", "Draw the areas of the player's own abilities while casting."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("debugLog"), new GUIContent("Debug Log", "Log casts, failures and interruptions."));
    }

    // ------------------------------------------------------------------ live
    private void DrawLive(PlayerAbilityController c)
    {
        AbilityEditorUI.Section("Live (Play mode)", "The live copy of every ability key: phase, cooldown and charges. Filled automatically; edit keys outside Play mode.");
        if (c.IsTargeting)
        {
            AbilitySlot targeting = c.GetSlot(c.TargetingSlot);
            EditorGUILayout.HelpBox($"Click-to-confirm preview open for {(targeting != null ? targeting.DisplayName : "an ability")}. Left click casts, right click / Escape cancels.", MessageType.Info);
        }
        DrawSlotsOverview(c);
        List<AbilityGrantSaveData> absorbed = c.GetAbsorbedAbilities();
        EditorGUILayout.LabelField($"Absorbed abilities: {absorbed.Count}", EditorStyles.boldLabel);
        foreach (AbilityGrantSaveData g in absorbed)
            EditorGUILayout.LabelField($"  {g.displayName} ({g.tier})", EditorStyles.miniLabel);
    }
}
#endif
