#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Player availability inspector: what the states mean, and in Play mode the live state, active status effects with
/// their remaining time, and test buttons (stun, silence, clear, kill, revive).
/// </summary>
[CustomEditor(typeof(AvailabilityStateMachine))]
public class AvailabilityStateMachineEditor : Editor
{
    private static bool showHelp = true;
    private readonly AbilityEditorUI.Throttle checks = new AbilityEditorUI.Throttle(1.0);
    private bool hasAbilities = true;

    public override void OnInspectorGUI()
    {
        var a = (AvailabilityStateMachine)target;
        if (checks.Due)
            hasAbilities = a.GetComponent<AbilitiesStateMachine>() != null || a.GetComponentInChildren<AbilitiesStateMachine>(true) != null ||
                           a.GetComponentInParent<AbilitiesStateMachine>() != null;

        showHelp = AbilityEditorUI.Help(showHelp, "What this component does",
            "Decides whether the player can act.\n" +
            "• Unaffected - normal.\n" +
            "• Stunned - cannot move, attack or cast (from Stun effects).\n" +
            "• Death - nothing works until Revive() is called.\n" +
            "Silence (from Silence effects) blocks abilities only, and Slow / Poison are tracked as flags. Ability effects from " +
            "mobs apply these automatically; scripts can call ApplyStun, ApplySilence, Kill and Revive.");

        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        if (!hasAbilities)
            EditorGUILayout.HelpBox("No AbilitiesStateMachine on the player: this component will not block any abilities.", MessageType.Info);

        if (!Application.isPlaying)
        {
            EditorGUILayout.LabelField("Enter Play mode to see the live state and test buttons.", EditorStyles.miniLabel);
            return;
        }

        AbilityEditorUI.Section("Live");
        EditorGUILayout.LabelField("State", a.CurrentState != null ? a.CurrentState.StateKey.ToString() : "-");
        EditorGUILayout.LabelField("Can", $"move {Yes(a.CanMove())} · act {Yes(a.CanAct())} · cast {Yes(a.CanCastSpells())}");
        foreach (EStatusEffect e in new[] { EStatusEffect.Stunned, EStatusEffect.Silenced, EStatusEffect.Slowed, EStatusEffect.Poisoned })
        {
            if (!a.HasStatusEffect(e))
                continue;
            Rect r = GUILayoutUtility.GetRect(18, 16, GUILayout.ExpandWidth(true));
            float remaining = a.GetStatusEffectRemainingTime(e);
            EditorGUI.ProgressBar(r, Mathf.Clamp01(remaining / 5f), $"{e}  {remaining:0.0}s");
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Stun 2s", "Test: stun the player for 2 seconds."))) a.ApplyStun(2f);
        if (GUILayout.Button(new GUIContent("Silence 3s", "Test: silence the player for 3 seconds."))) a.ApplySilence(3f);
        if (GUILayout.Button(new GUIContent("Clear", "Remove stun, silence, slow and poison.")))
        {
            a.RemoveStatusEffect(EStatusEffect.Stunned);
            a.RemoveStatusEffect(EStatusEffect.Silenced);
            a.RemoveStatusEffect(EStatusEffect.Slowed);
            a.RemoveStatusEffect(EStatusEffect.Poisoned);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Kill", "Test: put the player in the Death state."))) a.Kill();
        if (GUILayout.Button(new GUIContent("Revive", "Test: leave the Death state."))) a.Revive();
        EditorGUILayout.EndHorizontal();
        AbilityEditorUI.KeepRepainting(this);
    }

    private static string Yes(bool b) => b ? "✓" : "✗";
}
#endif
