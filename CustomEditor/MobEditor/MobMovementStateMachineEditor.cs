#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Mob AI inspector: what the component does, its references with auto-assign / add buttons, setup checks, a guide
/// to the 13 states, and a live view with test buttons in Play mode.
/// </summary>
[CustomEditor(typeof(MobMovementStateMachine))]
public class MobMovementStateMachineEditor : Editor
{
    private static bool showHelp;
    private static bool showStates;
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    private static readonly string[,] StateGuide =
    {
        { "Idle", "Stands still for the profile's idle time, looks around, then patrols or wanders." },
        { "Moving", "Wanders to a random safe point around home (summons follow their summoner)." },
        { "Patrol", "Walks to the next patrol point (Mob ▸ Patrol Points or Patrol Route)." },
        { "Investigate", "Checks a noise, a hit from out of sight, a help call or where it lost its target." },
        { "Chasing", "Runs after a target that is too far or out of sight but remembered." },
        { "Combat", "In range: circles/strafes, keeps its preferred distance, waits for an attack token." },
        { "Attacking", "Casting the ability the brain chose (committed until it ends)." },
        { "Dodging", "Sidesteps out of a telegraphed area or a swing (profile ▸ Dodging)." },
        { "Retreating", "Ranged/kiter mobs back off; skirmishers disengage after hitting." },
        { "Fleeing", "Low health + failed courage roll, feared type nearby, or a skittish animal." },
        { "Returning", "Leashed, chased too long or fight over: walks home (optionally healing)." },
        { "Stunned", "Stunned by an ability: does nothing until it ends." },
        { "Dead", "Death animation, drops, absorption; removed after the destroy delay." },
    };

    // Checks (including the NavMesh lookup) run at most twice per second instead of on every redraw.
    private readonly AbilityEditorUI.Throttle checks = new AbilityEditorUI.Throttle();

    public override void OnInspectorGUI()
    {
        var m = (MobMovementStateMachine)target;
        GameObject go = m.gameObject;
        if (checks.Due)
        {
            errors.Clear();
            warnings.Clear();
            Validate(m, go);
        }

        showHelp = EditorGUILayout.BeginFoldoutHeaderGroup(showHelp, "What this component does");
        if (showHelp)
        {
            EditorGUILayout.HelpBox(
                "This is the mob's AI. Every frame it senses (sight, hearing, damage), the brain decides what to do, and the " +
                "current state carries it out through the NavMeshAgent and the MobAbilityController.\n\n" +
                "Behaviour is configured on the Mob ▸ Profile (a Mob Profile asset). Abilities are configured on the " +
                "MobAbilityController. This component only needs its references, which are found automatically.",
                MessageType.Info);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // References.
        serializedObject.Update();
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("actionsController"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mob"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animator"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("navMeshAgent"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("statusController"));
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Auto-assign References", "Fill every empty reference from this object (Animator from Model/VisualModel or the children).")))
        {
            Undo.RecordObject(m, "Auto-assign Mob AI References");
            m.AutoAssignReferences();
            MarkDirty(m);
            checks.Invalidate();
        }
        if (GUILayout.Button(new GUIContent("Configure Whole Mob", "Run Mob Quick Setup: adds and wires every component a mob needs.")))
            MobSetupUtility.SetupMob(go, false);
        EditorGUILayout.EndHorizontal();

        // Missing components with one-click fixes.
        AddButton<NavMeshAgent>(go, "NavMeshAgent", "Required: moves the mob on the baked NavMesh.");
        AddButton<MobStatusController>(go, "MobStatusController", "Required: health, speed and death.");
        AddButton<MobAbilityController>(go, "MobAbilityController", "Needed for ANY attack, including the automatic basic attack.");

        // Checks.
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        if (errors.Count + warnings.Count == 0)
            EditorGUILayout.LabelField("✓ AI setup looks good.", EditorStyles.miniLabel);

        showStates = EditorGUILayout.Foldout(showStates, new GUIContent("The 13 states", "What each state does and when the brain picks it."), true);
        if (showStates)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            for (int i = 0; i < StateGuide.GetLength(0); i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(StateGuide[i, 0], EditorStyles.boldLabel, GUILayout.Width(84));
                EditorGUILayout.LabelField(StateGuide[i, 1], EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        // Debug fields.
        serializedObject.Update();
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("debugLog"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("drawGizmos"));
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("debugState"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("debugTarget"));
        }
        DrawPropertiesExcluding(serializedObject, "m_Script", "actionsController", "mob", "animator", "navMeshAgent",
            "statusController", "debugLog", "drawGizmos", "debugState", "debugTarget");
        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
        {
            DrawLive(m);
            AbilityEditorUI.KeepRepainting(this);
        }
        if (GUI.changed)
            checks.Invalidate();
    }

    private void Validate(MobMovementStateMachine m, GameObject go)
    {
        if (go.GetComponent<Mob>() == null)
            errors.Add("No Mob / MobActionsController on this object: the AI has nothing to drive.");
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (agent == null)
            errors.Add("No NavMeshAgent: the mob cannot move.");
        if (go.GetComponent<MobStatusController>() == null)
            errors.Add("No MobStatusController: the mob cannot take damage or die.");
        if (go.GetComponent<MobAbilityController>() == null)
            warnings.Add("No MobAbilityController: the mob can move but never attacks.");
        Animator a = m.AnimatorReference != null ? m.AnimatorReference : MobMovementStateMachine.FindAnimator(go.transform);
        if (a == null)
            warnings.Add("No Animator found: the mob works but is not animated.");
        else if (a.runtimeAnimatorController == null)
            warnings.Add($"The Animator on '{a.name}' has no Animator Controller.");
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
            warnings.Add("The Rigidbody is not kinematic: physics will fight the NavMeshAgent (it is made kinematic at runtime).");
        if (agent != null && go.scene.IsValid() && !EditorUtility.IsPersistent(go) && !Application.isPlaying &&
            !NavMesh.SamplePosition(go.transform.position, out _, 2f, NavMesh.AllAreas))
            warnings.Add("This mob is not on (or near) a baked NavMesh. Bake the NavMesh or move the mob onto it.");
    }

    private static void AddButton<T>(GameObject go, string label, string why) where T : Component
    {
        if (AbilityEditorUI.Has<T>(go))
            return;
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField(new GUIContent($"✗ {label} missing", why), EditorStyles.boldLabel);
        if (GUILayout.Button("Add", GUILayout.Width(50)))
            Undo.AddComponent<T>(go);
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawLive(MobMovementStateMachine m)
    {
        MobMovementContext c = m.Context;
        if (c == null)
            return;
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("State", $"{m.CurrentStateKey}  ({m.TimeInState:0.0}s, before: {m.PreviousStateKey})");
        EditorGUILayout.LabelField("Mode / LOD", $"{c.Brain.Mode} / {c.Scheduler.Current}");
        EditorGUILayout.LabelField("Target", c.Brain.Target != null ? $"{c.Brain.Target.name}  {c.Brain.TargetDistance:0.0}m" : "-");
        EditorGUILayout.LabelField("Threat", c.Brain.Threat != null ? c.Brain.Threat.name : "-");
        EditorGUILayout.LabelField("Style / range", $"{c.Brain.Style}  {c.Brain.PreferredMin:0.#}-{c.Brain.PreferredMax:0.#}m{(c.Brain.RangedRole ? " (ranged)" : "")}");
        EditorGUILayout.LabelField("Flags", $"{(c.Brain.InDanger ? "in danger  " : "")}{(c.Brain.Cornered ? "cornered  " : "")}{(c.Brain.Leashed ? "leashed  " : "")}{(c.Entity.IsStunned ? "stunned" : "")}");
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Target Nearest Player", "Make the mob attack the closest player.")))
        {
            CombatEntity best = null;
            float bestD = float.MaxValue;
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null || !p.IsAlive)
                    continue;
                float d = (p.Position - m.transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            if (best != null)
                m.SetTarget(best);
        }
        if (GUILayout.Button(new GUIContent("Investigate Scene Camera", "Send the mob to check the point under the Scene view camera.")))
        {
            SceneView sv = SceneView.lastActiveSceneView;
            if (sv != null && Physics.Raycast(sv.camera.transform.position, sv.camera.transform.forward, out RaycastHit hit, 500f))
                m.Investigate(hit.point, 0.8f);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private static void MarkDirty(Object o)
    {
        EditorUtility.SetDirty(o);
        if (PrefabUtility.IsPartOfPrefabInstance(o))
            PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }
}
#endif
