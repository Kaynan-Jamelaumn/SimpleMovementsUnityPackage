#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Mob Profile inspector: a plain-language summary, a guide to every section, presets, setup checks, the mobs that
/// use this profile, then the fields (each has a tooltip).
/// </summary>
[CustomEditor(typeof(MobProfile))]
[CanEditMultipleObjects]
public class MobProfileEditor : Editor
{
    private static bool showGuide;
    private static bool showUsers;
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly AbilityEditorUI.Throttle checks = new AbilityEditorUI.Throttle(); // checks twice per second, not every redraw
    private List<GameObject> users;

    public override void OnInspectorGUI()
    {
        var p = (MobProfile)target;

        EditorGUILayout.HelpBox(p.DescribeBehaviour(), MessageType.None);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Apply Preset ▾", "Overwrite every field with a ready-made behaviour (Undo restores it).")))
            ShowPresetMenu();
        if (GUILayout.Button(new GUIContent("Find Mobs Using It", "List the mob prefabs whose Mob ▸ Profile is this asset.")))
        {
            users = FindUsers(p);
            showUsers = true;
        }
        EditorGUILayout.EndHorizontal();

        if (showUsers && users != null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(users.Count == 0 ? "No mob prefab uses this profile." : $"Used by {users.Count} mob prefab(s):", EditorStyles.miniBoldLabel);
            foreach (GameObject g in users)
                if (g != null)
                    EditorGUILayout.ObjectField(g, typeof(GameObject), false);
            EditorGUILayout.EndVertical();
        }

        showGuide = EditorGUILayout.BeginFoldoutHeaderGroup(showGuide, "What each section does");
        if (showGuide)
        {
            EditorGUILayout.HelpBox(
                "TEMPERAMENT - whether it starts fights (Aggression), what it does when hit (When Attacked), and when it runs away " +
                "(Courage + Flee Health Threshold, Fears, Flee From Players Within for skittish animals).\n\n" +
                "PERCEPTION - Sight Range + Field Of View (cone in front), Close Sense Radius (notices anyone this close, even behind), " +
                "hearing, how fast suspicion turns into detection (Awareness Gain Rate; sneaking players are slower), Memory Duration " +
                "(how long it hunts someone it lost), Reaction Time, and calling allies for help.\n\n" +
                "MOVEMENT - speeds (multipliers of the Speed Manager's speed), wandering and patrol, Leash Distance (how far from home " +
                "it follows before giving up), returning home, avoiding hazards, flee distance.\n\n" +
                "COMBAT - Combat Style (Auto reads its abilities: ranged ones make it keep distance), preferred distance band, " +
                "Aggressiveness (how eagerly it attacks), Attack Tokens (only a few mobs attack one target at once; the others circle).\n\n" +
                "DODGING - chance to sidestep telegraphed areas and melee swings, cooldown, distance, invulnerability while dodging.\n\n" +
                "ABILITIES - randomness of ability choice, how often it re-decides, the automatic basic attack, and the absorb chance " +
                "multiplier (multiplies MobAbilityController ▸ Absorb Chance Per Kill).\n\n" +
                "ANIMATION - Animator parameter names (missing parameters are ignored). DEATH / PERFORMANCE - corpse removal, full-rate thinking for bosses.",
                MessageType.Info);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        if (checks.Due)
        {
            errors.Clear();
            warnings.Clear();
            p.Validate(errors, warnings);
            ExtraChecks(p);
        }
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        EditorGUILayout.Space();

        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
    }

    private void ExtraChecks(MobProfile p)
    {
        if (p.dodgeChance > 0f && p.dodgeDistance < 1f)
            warnings.Add("Dodge Distance under 1m rarely gets it out of an area.");
        if (p.combatStyle == MobCombatStyle.Brute && p.dodgeChance > 0.4f)
            warnings.Add("Brutes are meant to rarely dodge; Dodge Chance is high for this style.");
        if (p.fleeHealthThreshold > 0f && p.courage >= 1f)
            warnings.Add("Courage 1 means it never flees, so Flee Health Threshold has no effect.");
        if (p.memoryDuration < p.reactionTime * 3f)
            warnings.Add("Memory Duration is very short compared with Reaction Time: it forgets targets almost as soon as it notices them.");
        if (p.absorbChanceMultiplier <= 0f)
            warnings.Add("Absorb Chance Multiplier is 0: players can never absorb abilities from mobs using this profile.");
    }

    private void ShowPresetMenu()
    {
        var menu = new GenericMenu();
        foreach (MobProfile.Preset preset in System.Enum.GetValues(typeof(MobProfile.Preset)))
        {
            MobProfile.Preset captured = preset;
            menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(preset.ToString())), false, () =>
            {
                foreach (Object t in targets)
                {
                    Undo.RecordObject(t, "Apply Mob Preset");
                    ((MobProfile)t).ApplyPreset(captured);
                    EditorUtility.SetDirty(t);
                }
            });
        }
        menu.ShowAsContext();
    }

    private static List<GameObject> FindUsers(MobProfile p)
    {
        var result = new List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
                continue;
            Mob m = go.GetComponent<Mob>();
            if (m != null && m.ProfileAsset == p)
                result.Add(go);
        }
        return result;
    }
}
#endif
