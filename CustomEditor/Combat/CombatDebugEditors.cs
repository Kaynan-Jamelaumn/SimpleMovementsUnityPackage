#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Combat Stats inspector: the settings, and in Play Mode the final value of every stat in use, the derived multipliers
/// (damage taken, crowd control, buffs / debuffs, cooldowns) and where each modifier comes from.
/// </summary>
[CustomEditor(typeof(CombatStats))]
public class CombatStatsEditor : Editor
{
    private static bool showTotals = true, showSources;

    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Base stats here add to race, class, traits, equipment and buffs. In Play Mode the totals and sources are shown below.", MessageType.None);
            return;
        }
        var s = (CombatStats)target;
        EditorGUILayout.Space();
        showTotals = EditorGUILayout.Foldout(showTotals, "Totals (Play Mode)", true);
        if (showTotals)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            bool any = false;
            foreach (CombatStatType stat in (CombatStatType[])Enum.GetValues(typeof(CombatStatType)))
            {
                float v = s.Get(stat);
                if (Mathf.Abs(v) < 0.001f)
                    continue;
                any = true;
                CombatStatInfo info = CombatStatInfo.Get(stat);
                EditorGUILayout.LabelField(new GUIContent(info.name, info.description), new GUIContent($"{v:+0.##;-0.##}{(info.isPercent ? "%" : "")}"));
            }
            if (!any)
                EditorGUILayout.LabelField("No stats (everything at 0).", EditorStyles.miniLabel);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Derived", EditorStyles.boldLabel);
            Row("Height scale", $"×{s.HeightScale:0.##}");
            Row("Threat", $"×{s.ThreatMultiplier:0.##}");
            Row("Control dealt / taken", $"×{s.ControlDealtMultiplier:0.##} / ×{s.ControlTakenMultiplier:0.##}");
            Row("Buff duration / strength", $"×{s.BuffDurationMultiplier:0.##} / ×{s.BuffStrengthMultiplier:0.##}");
            Row("Debuff duration / strength", $"×{s.DebuffDurationMultiplier(ElementType.None):0.##} / ×{s.DebuffStrengthMultiplier(ElementType.None):0.##}");
            Row("Status taken (poison)", $"×{s.StatusTakenMultiplier(ElementType.Poison):0.##}");
            Row("Cooldown skills / innate / items", $"×{s.CooldownMultiplier(StatScope.Skills):0.##} / ×{s.CooldownMultiplier(StatScope.Innate):0.##} / ×{s.CooldownMultiplier(StatScope.Items):0.##}");
            Row("Mana cost skills / innate / items", $"×{s.ManaCostMultiplier(StatScope.Skills):0.##} / ×{s.ManaCostMultiplier(StatScope.Innate):0.##} / ×{s.ManaCostMultiplier(StatScope.Items):0.##}");
            Row("Draw / reload speed", $"×{s.DrawSpeedMultiplier:0.##} / ×{s.ReloadSpeedMultiplier:0.##}");
            EditorGUILayout.EndVertical();
        }
        showSources = EditorGUILayout.Foldout(showSources, "Sources (Play Mode)", true);
        if (showSources)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            List<string> lines = s.Describe();
            if (lines.Count == 0) EditorGUILayout.LabelField("No modifiers.", EditorStyles.miniLabel);
            foreach (string l in lines) EditorGUILayout.LabelField(l, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }
    }

    private static void Row(string label, string value) => EditorGUILayout.LabelField(label, value, EditorStyles.miniLabel);
}

/// <summary>Combat Entity inspector: the settings, and in Play Mode the threat table, the target and the active buffs / debuffs.</summary>
[CustomEditor(typeof(CombatEntity))]
public class CombatEntityEditor : Editor
{
    private static bool showThreat = true;
    private readonly List<KeyValuePair<CombatEntity, float>> table = new List<KeyValuePair<CombatEntity, float>>();

    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying)
            return;
        var e = (CombatEntity)target;
        EditorGUILayout.Space();
        showThreat = EditorGUILayout.Foldout(showThreat, "Threat & Status (Play Mode)", true);
        if (!showThreat)
            return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        e.GetThreatTable(table);
        if (table.Count == 0)
            EditorGUILayout.LabelField("Nobody on the threat table.", EditorStyles.miniLabel);
        float top = table.Count > 0 ? Mathf.Max(0.01f, table[0].Value) : 1f;
        foreach (KeyValuePair<CombatEntity, float> kv in table)
        {
            Rect r = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            EditorGUI.ProgressBar(r, kv.Value / top, $"{(kv.Key != null ? kv.Key.name : "?")}  {kv.Value:0}");
        }
        // The brain is plain C# owned by the mob's movement state machine (not a component).
        Mob mob = e.GetComponentInParent<Mob>();
        MobBrain brain = mob != null && mob.AI != null && mob.AI.Context != null ? mob.AI.Context.Brain : null;
        if (brain != null)
            EditorGUILayout.LabelField("Target", brain.Target != null ? brain.Target.name : "none", EditorStyles.miniLabel);
        TimedStatModifiers timed = e.GetComponent<TimedStatModifiers>();
        if (timed != null && timed.All.Count > 0)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Buffs & debuffs", EditorStyles.boldLabel);
            foreach (TimedStatModifiers.Active a in timed.All)
                EditorGUILayout.LabelField($"{(a.debuff ? "▼" : "▲")} {a.name}", $"{a.Remaining:0.0}s ×{a.strength:0.##}", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();
    }
}
#endif
