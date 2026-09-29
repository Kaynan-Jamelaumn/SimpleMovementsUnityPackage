#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Mob status inspector: what it handles, Health/Speed Manager auto-assign and add buttons, checks, the effective
/// destroy delay, and a live health bar with Damage / Kill test buttons in Play mode.
/// </summary>
[CustomEditor(typeof(MobStatusController))]
public class MobStatusControllerEditor : Editor
{
    private static bool showHelp;

    public override void OnInspectorGUI()
    {
        var s = (MobStatusController)target;
        GameObject go = s.gameObject;

        showHelp = EditorGUILayout.BeginFoldoutHeaderGroup(showHelp, "What this component does");
        if (showHelp)
        {
            EditorGUILayout.HelpBox(
                "Health, speed and death of the mob. Ability effects (damage, heal, slows, 'Status Effect' changes such as " +
                "regeneration or damage factors) go through it. On death it tells the AI, plays the death animation, drops " +
                "items (ItemSpawner), lets the player absorb abilities (MobAbilityController ▸ Absorption) and removes the body " +
                "after the destroy delay.", MessageType.Info);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Auto-assign Managers", "Fill Health Manager and Speed Manager from this object or its children.")))
        {
            Undo.RecordObject(s, "Auto-assign Managers");
            s.AutoAssignReferences();
            EditorUtility.SetDirty(s);
            if (PrefabUtility.IsPartOfPrefabInstance(s))
                PrefabUtility.RecordPrefabInstancePropertyModifications(s);
        }
        if (!AbilityEditorUI.Has<HealthManager>(go, true) && GUILayout.Button(new GUIContent("Add Health Manager", "Add a HealthManager to this object.")))
            Undo.AddComponent<HealthManager>(go);
        if (!AbilityEditorUI.Has<SpeedManager>(go, true) && GUILayout.Button(new GUIContent("Add Speed Manager", "Add a SpeedManager to this object.")))
            Undo.AddComponent<SpeedManager>(go);
        EditorGUILayout.EndHorizontal();

        // Checks.
        var warnings = new List<string>();
        var errors = new List<string>();
        if (s.HealthManager == null)
        {
            if (AbilityEditorUI.Has<HealthManager>(go, true))
                warnings.Add("Health Manager is not assigned (it will be found at runtime). Press Auto-assign Managers.");
            else
                errors.Add("No HealthManager: the mob cannot be hurt or die.");
        }
        if (s.SpeedManager == null && !AbilityEditorUI.Has<SpeedManager>(go, true))
            warnings.Add("No SpeedManager: the mob moves at the NavMeshAgent's speed and ignores slows/hastes.");
        if (!AbilityEditorUI.Has<ItemSpawner>(go))
            EditorGUILayout.LabelField("No ItemSpawner: this mob drops no items (optional).", EditorStyles.miniLabel);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);

        Mob mob = go.GetComponent<Mob>();
        float delay = s.DestroyDelayOverride >= 0f ? s.DestroyDelayOverride : (mob != null ? mob.Profile.destroyDelay : 0f);
        EditorGUILayout.LabelField(new GUIContent($"Body removed {delay:0.#}s after death" + (s.DestroyDelayOverride >= 0f ? " (override)" : " (Mob Profile ▸ Destroy Delay)"),
            "Set Destroy Delay Override to a value ≥ 0 to use a different delay for this mob only."), EditorStyles.miniLabel);

        if (Application.isPlaying && s.HealthManager != null)
        {
            HealthManager h = s.HealthManager;
            Rect r = GUILayoutUtility.GetRect(18, 18, GUILayout.ExpandWidth(true));
            float max = Mathf.Max(1f, h.MaxValue);
            EditorGUI.ProgressBar(r, Mathf.Clamp01(h.CurrentValue / max), s.IsDead ? "Dead" : $"HP {h.CurrentValue:0.#} / {max:0.#}");
            using (new EditorGUI.DisabledScope(s.IsDead))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("Damage 25%", "Test: hurt the mob by a quarter of its max HP (as if hit by the nearest player).")))
                {
                    CombatEntity e = CombatEntity.GetOrAdd(go);
                    CombatEntity player = CombatEntity.Players.Count > 0 ? CombatEntity.Players[0] : null;
                    e.ApplyDamage(new DamageInfo { amount = max * 0.25f, source = player, target = e, point = e.Center });
                }
                if (GUILayout.Button(new GUIContent("Kill", "Test: kill the mob now (as if killed by the first player, so absorption can roll).")))
                    s.Kill(CombatEntity.Players.Count > 0 ? CombatEntity.Players[0] : null);
                EditorGUILayout.EndHorizontal();
            }
            AbilityEditorUI.KeepRepainting(this);
        }
    }
}
#endif
