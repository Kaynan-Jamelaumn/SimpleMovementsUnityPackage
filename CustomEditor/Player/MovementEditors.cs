#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Player movement inspector: the 7 states and their inputs, references with auto-assign / add buttons, setup checks
/// with Fix buttons (everything the states need at runtime), and a live view (state, ground, speed, stamina, traits).
/// </summary>
[CustomEditor(typeof(MovementStateMachine))]
public class MovementStateMachineEditor : Editor
{
    private static bool showHelp;
    private static bool showStates;
    private static bool showChecks = true;
    private readonly List<AbilityEditorUI.Issue> issues = new List<AbilityEditorUI.Issue>();
    private readonly AbilityEditorUI.Throttle checks = new AbilityEditorUI.Throttle();
    // Found by the throttled refresh (searching the hierarchy on every redraw made the inspector slow).
    private PlayerMovementModel liveModel;
    private PlayerMovementController liveController;
    private PlayerStatusController liveStatus;
    private TraitManager liveTraits;
    private bool hasStatus;

    private static readonly string[,] States =
    {
        { "Idle", "No movement input.", "-" },
        { "Walking", "Moving at the Speed Manager's speed.", "Movement" },
        { "Running", "Sprinting: speed × Running multiplier, spends stamina.", "Movement + Sprint (held)" },
        { "Crouching", "Slower: speed × Crouching multiplier.", "Movement + Crouch (held)" },
        { "Jumping", "In the air until grounded (running jumps are 1.75x higher).", "Jump" },
        { "Dashing", "Quick burst forward (Dash Model), cooldown + stamina.", "Dash" },
        { "Rolling", "Short evasive roll (Roll Model), cooldown + stamina.", "Roll" },
    };

    public override void OnInspectorGUI()
    {
        var m = (MovementStateMachine)target;
        GameObject go = m.gameObject;
        if (checks.Due)
            Refresh(m, go);

        showHelp = AbilityEditorUI.Help(showHelp, "What this component does",
            "The player's movement. Each frame the current state reads the Player input actions, moves the CharacterController and " +
            "picks the next state. Speeds come from the SpeedManager (× the running/crouching multipliers and the carried weight), " +
            "jump and gravity from the PlayerMovementModel, dash and roll from their models, stamina from the StaminaManager. " +
            "Stuns and death (AvailabilityStateMachine) stop movement. Traits add Double Jump, Wall Climb and Glide on top.");

        showStates = EditorGUILayout.Foldout(showStates, new GUIContent("The 7 states", "What each state does and which input starts it."), true);
        if (showStates)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            for (int i = 0; i < States.GetLength(0); i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(States[i, 0], EditorStyles.boldLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField(States[i, 1], EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(States[i, 2], EditorStyles.miniLabel, GUILayout.Width(150));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
        if (GUILayout.Button(new GUIContent("Auto-assign References", "Fill every empty reference from this object, its children and parents.")))
        {
            Undo.RecordObject(m, "Auto-assign Movement References");
            m.AutoAssignReferences();
            AbilityEditorUI.MarkDirty(m);
            checks.Invalidate();
        }

        AbilityEditorUI.Section("Components");
        AbilityEditorUI.ComponentRow<CharacterController>(go, "CharacterController", "Moves and collides the player.", true, true);
        AbilityEditorUI.ComponentRow<PlayerMovementModel>(go, "PlayerMovementModel", "Gravity, jump force, stamina costs.", true, true);
        AbilityEditorUI.ComponentRow<PlayerMovementController>(go, "PlayerMovementController", "Gravity, rotation, ground check.", true, true);
        AbilityEditorUI.ComponentRow<PlayerDashModel>(go, "PlayerDashModel", "Dash settings.", true, true);
        AbilityEditorUI.ComponentRow<PlayerRollModel>(go, "PlayerRollModel", "Roll settings.", true, true);
        AbilityEditorUI.ComponentRow<AvailabilityStateMachine>(go, "AvailabilityStateMachine", "Stops movement while stunned or dead.", true, true);
        AbilityEditorUI.StatusRow(hasStatus, "PlayerStatusController (Speed, Stamina, Weight managers)", "Speeds, stamina and carried weight.");

        System.Action fix = AbilityEditorUI.DrawIssues(issues, ref showChecks);

        if (Application.isPlaying)
        {
            DrawLive(m);
            AbilityEditorUI.KeepRepainting(this);
        }
        if (GUI.changed)
            checks.Invalidate();
        if (fix != null)
        {
            fix();
            checks.Invalidate();
            GUIUtility.ExitGUI();
        }
    }

    private void Refresh(MovementStateMachine m, GameObject go)
    {
        Transform root = go.transform.root;
        liveModel = root.GetComponentInChildren<PlayerMovementModel>(true);
        liveController = root.GetComponentInChildren<PlayerMovementController>(true);
        liveStatus = root.GetComponentInChildren<PlayerStatusController>(true);
        liveTraits = root.GetComponentInChildren<TraitManager>(true);
        hasStatus = liveStatus != null || go.GetComponentInParent<PlayerStatusController>() != null;
        issues.Clear();
        Validate(m, go);
    }

    private void Validate(MovementStateMachine m, GameObject go)
    {
        Transform root = go.transform.root;
        var status = root.GetComponentInChildren<PlayerStatusController>(true);
        if (status == null)
        {
            AbilityEditorUI.Add(issues, MessageType.Error, "No PlayerStatusController on the player: every state reads speed and stamina from it.");
        }
        else
        {
            GameObject s = status.gameObject;
            if (s.GetComponentInChildren<SpeedManager>(true) == null)
                AbilityEditorUI.Add(issues, MessageType.Error, "No SpeedManager: the player has no movement speed.", "Add", () => Undo.AddComponent<SpeedManager>(s));
            if (s.GetComponentInChildren<StaminaManager>(true) == null)
                AbilityEditorUI.Add(issues, MessageType.Error, "No StaminaManager: sprinting, jumping, dashing and rolling need it.", "Add", () => Undo.AddComponent<StaminaManager>(s));
            if (s.GetComponentInChildren<WeightManager>(true) == null)
                AbilityEditorUI.Add(issues, MessageType.Error, "No WeightManager: movement speed is scaled by carried weight.", "Add", () => Undo.AddComponent<WeightManager>(s));
            if (root.GetComponentInChildren<PlayerDashModel>(true) == null)
                AbilityEditorUI.Add(issues, MessageType.Error, "No PlayerDashModel: every state checks whether a dash is possible.", "Add", () => Undo.AddComponent<PlayerDashModel>(s));
            if (root.GetComponentInChildren<PlayerRollModel>(true) == null)
                AbilityEditorUI.Add(issues, MessageType.Error, "No PlayerRollModel: every state checks whether a roll is possible.", "Add", () => Undo.AddComponent<PlayerRollModel>(s));
        }
        var model = root.GetComponentInChildren<PlayerMovementModel>(true);
        if (model != null)
        {
            if (model.Gravity >= 0f)
                AbilityEditorUI.Add(issues, MessageType.Error, $"PlayerMovementModel ▸ Gravity is {model.Gravity}: it must be negative (e.g. -9.81) or the player floats away.", "Set -9.81",
                    () => { Undo.RecordObject(model, "Fix Gravity"); model.Gravity = -9.81f; });
            if (model.JumpForce <= 0f)
                AbilityEditorUI.Add(issues, MessageType.Warning, "PlayerMovementModel ▸ Jump Force is 0: the player cannot jump.");
        }
        if (root.GetComponentInChildren<PlayerCameraModel>(true) == null)
            AbilityEditorUI.Add(issues, MessageType.Warning, "No PlayerCameraModel: movement uses it to decide whether to rotate with the camera.");
    }

    private void DrawLive(MovementStateMachine m)
    {
        AbilityEditorUI.Section("Live");
        PlayerMovementModel model = liveModel;
        PlayerMovementController ctrl = liveController;
        PlayerStatusController status = liveStatus;
        EditorGUILayout.LabelField("State", m.CurrentState != null ? $"{m.CurrentState.StateKey}  ({m.TimeInState:0.0}s, before: {m.PreviousStateKey})" : "-");
        if (ctrl != null) EditorGUILayout.LabelField("On ground", ctrl.IsGrounded() ? "yes" : "no");
        if (model != null)
        {
            EditorGUILayout.LabelField("Speed", $"{model.CurrentSpeed:0.0} m/s   vertical {model.VerticalVelocity:0.0} m/s");
            if (model.SuspendGravity) EditorGUILayout.LabelField("Gravity", "suspended by a trait (climbing)");
        }
        if (status != null && status.StaminaManager != null)
        {
            Rect r = GUILayoutUtility.GetRect(18, 16, GUILayout.ExpandWidth(true));
            StaminaManager st = status.StaminaManager;
            EditorGUI.ProgressBar(r, st.MaxValue > 0f ? Mathf.Clamp01(st.CurrentValue / st.MaxValue) : 0f, $"Stamina {st.CurrentValue:0}/{st.MaxValue:0}");
        }
        TraitManager traits = liveTraits;
        if (traits != null)
            foreach (Trait t in traits.ActiveTraits)
                foreach (string line in traits.DescribeLive(t))
                {
                    string l = line.ToLowerInvariant();
                    if (l.Contains("jump") || l.Contains("climb") || l.Contains("glid"))
                        EditorGUILayout.LabelField("  " + t.Name, line, EditorStyles.miniLabel);
                }
    }
}

/// <summary>Movement settings inspector: jump height and air time computed from the numbers, reference auto-assign, checks.</summary>
[CustomEditor(typeof(PlayerMovementModel))]
public class PlayerMovementModelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var m = (PlayerMovementModel)target;
        EditorGUILayout.HelpBox("Movement settings read by the MovementStateMachine states: stamina costs, gravity and jump. Speeds live in the SpeedManager.", MessageType.None);

        float g = Mathf.Abs(m.Gravity * (Mathf.Approximately(m.GravityMultiplier, 0f) ? 1f : m.GravityMultiplier));
        if (g > 0.01f && m.JumpForce > 0f)
        {
            float t = 2f * m.JumpForce / g;
            float runV = m.JumpForce * 1.75f;
            EditorGUILayout.LabelField($"Jump: about {m.EstimatedJumpHeight:0.00} m high, {t:0.00}s in the air · running jump {runV * runV / (2f * g):0.00} m",
                EditorStyles.boldLabel);
        }

        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Auto-assign References", "Controller = the CharacterController here, Player Transform = this transform, Shell = this object (only when empty).")))
        {
            var so = new SerializedObject(m);
            SetIfNull(so, "controller", m.GetComponentInParent<CharacterController>());
            SetIfNull(so, "playerTransform", m.transform);
            SetIfNull(so, "playerShellObject", m.gameObject);
            so.ApplyModifiedProperties();
        }
        if (GUILayout.Button(new GUIContent("Typical Values", "Gravity -9.81, multiplier 2, jump force 6.5 (about a 1 m jump).")))
        {
            Undo.RecordObject(m, "Typical Movement Values");
            m.Gravity = -9.81f;
            m.GravityMultiplier = 2f;
            m.JumpForce = 6.5f;
            EditorUtility.SetDirty(m);
        }
        EditorGUILayout.EndHorizontal();

        if (m.Gravity >= 0f)
            EditorGUILayout.HelpBox("Gravity must be negative (e.g. -9.81).", MessageType.Error);
        if (m.GravityMultiplier <= 0f)
            EditorGUILayout.HelpBox("Gravity Multiplier should be above 0 (1 = normal).", MessageType.Warning);
        if (m.Controller == null && m.GetComponent<CharacterController>() == null)
            EditorGUILayout.HelpBox("No CharacterController assigned or on this object.", MessageType.Error);
        if (m.PlayerTransform == null || m.PlayerShellObject == null)
            EditorGUILayout.HelpBox("Player Transform / Shell Object are empty (press Auto-assign References).", MessageType.Warning);
    }

    private static void SetIfNull(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null && p.objectReferenceValue == null) p.objectReferenceValue = value;
    }
}

/// <summary>Dash / roll settings inspector: the resulting distance, cooldown and stamina checks.</summary>
[CustomEditor(typeof(PlayerActionModelBase), true)]
public class PlayerActionModelEditor : Editor
{
    private readonly AbilityEditorUI.Throttle lookup = new AbilityEditorUI.Throttle(1.0);
    private SpeedManager speed;
    private StaminaManager stamina;

    public override void OnInspectorGUI()
    {
        var m = (PlayerActionModelBase)target;
        if (lookup.Due)
        {
            speed = m.transform.root.GetComponentInChildren<SpeedManager>(true);
            stamina = m.transform.root.GetComponentInChildren<StaminaManager>(true);
        }
        float baseSpeed = speed != null ? speed.BaseSpeed : 5f;

        if (m is PlayerDashModel d)
        {
            EditorGUILayout.HelpBox("Dash: a quick burst forward with the Dash input. Moves Dash Speed × current speed per second for Dash Duration.", MessageType.None);
            float dist = d.DashSpeed * baseSpeed * d.DashDuration;
            EditorGUILayout.LabelField($"Dash distance ≈ {dist:0.#} m at base speed {baseSpeed:0.#} m/s", EditorStyles.boldLabel);
            if (dist > 40f)
                EditorGUILayout.HelpBox($"About {dist:0} m is a very long dash: lower Dash Speed or Dash Duration (e.g. speed 4, duration 0.25s).", MessageType.Warning);
        }
        else if (m is PlayerRollModel r)
        {
            EditorGUILayout.HelpBox("Roll: a short evasive roll with the Roll input. Moves Roll Speed Modifier × current speed for Roll Duration.", MessageType.None);
            EditorGUILayout.LabelField($"Roll distance ≈ {r.RollSpeedModifier * baseSpeed * r.RollDuration:0.#} m at base speed {baseSpeed:0.#} m/s", EditorStyles.boldLabel);
        }

        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        if (stamina != null && m.StaminaCost > stamina.MaxValue)
            EditorGUILayout.HelpBox($"Costs {m.StaminaCost} stamina but max stamina is {stamina.MaxValue}: it can never be used.", MessageType.Error);
        if (m.CooldownDuration <= 0f)
            EditorGUILayout.HelpBox("No cooldown: it can be spammed every frame the input is pressed.", MessageType.Warning);
        if (Application.isPlaying)
        {
            float left = m.CooldownDuration - (Time.time - m.LastActionTime);
            EditorGUILayout.LabelField("Cooldown", left > 0f ? $"{left:0.0}s" : "ready");
            AbilityEditorUI.KeepRepainting(this);
        }
    }
}

/// <summary>Movement controller inspector: references with auto-assign, the ground check, and its ray in the Scene view.</summary>
[CustomEditor(typeof(PlayerMovementController))]
public class PlayerMovementControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var c = (PlayerMovementController)target;
        EditorGUILayout.HelpBox("Applies gravity (FixedUpdate) and camera rotation (LateUpdate), and checks the ground with a short ray from the feet (drawn in the Scene view).", MessageType.None);
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
        if (GUILayout.Button(new GUIContent("Auto-assign References", "Fill the movement model, camera model and camera controller from this object, its parents and children.")))
        {
            Undo.RecordObject(c, "Auto-assign Movement Controller");
            c.AutoAssignReferences();
            AbilityEditorUI.MarkDirty(c);
        }
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("On ground", c.IsGrounded() ? "yes" : "no");
            AbilityEditorUI.KeepRepainting(this);
        }
    }

    private void OnSceneGUI()
    {
        var c = (PlayerMovementController)target;
        var model = c.GetComponentInParent<PlayerMovementModel>();
        if (model == null || model.PlayerShellObject == null)
            return;
        CharacterController cc = model.Controller != null ? model.Controller : c.GetComponentInParent<CharacterController>();
        Vector3 origin = model.PlayerShellObject.transform.position + Vector3.up * (cc != null ? cc.stepOffset : 0.3f);
        Handles.color = Application.isPlaying && c.IsGrounded() ? Color.green : Color.red;
        Handles.DrawLine(origin, origin + Vector3.down * c.GroundCheckDistance);
        Handles.Label(origin, "ground check");
    }
}
#endif
