#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Trait inspector: what the player will see, kind and cost at a glance, presets, one-click "add modifier" and
/// "add behaviour" menus, conversion of old string effects, setup checks, and in Play mode give/remove/use buttons.
/// </summary>
[CustomEditor(typeof(Trait))]
public class TraitEditor : Editor
{
    private static bool showHelp;
    private static bool showPreview = true;
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly AbilityEditorUI.Throttle checks = new AbilityEditorUI.Throttle();
    private readonly AbilityEditorUI.Throttle managerSearch = new AbilityEditorUI.Throttle(2.0);
    private TraitManager[] managers = new TraitManager[0];

    public override void OnInspectorGUI()
    {
        var t = (Trait)target;
        if (checks.Due)
        {
            errors.Clear();
            warnings.Clear();
            t.Validate(errors, warnings);
        }

        showHelp = AbilityEditorUI.Help(showHelp, "What a trait is",
            "A trait is picked when the character is created (or granted later by armor, quests or scripts).\n\n" +
            "PASSIVE MODIFIERS change stats while the trait is owned: +20% Max Health, -15% Damage Taken, +10% Move Speed, " +
            "-15% Ability Cooldown... Negative values on Damage Taken / Stamina Cost / Cooldown / Cast Time / Control Taken are GOOD.\n\n" +
            "BEHAVIOURS are things the trait lets the character do or reacts with: Double Jump, Wall Climb, Glide, an ability on a " +
            "key (Guardian Barrier, Blink), Second Wind, Life Steal, Cheat Death, Thorns, regeneration out of combat, rewards on kill, " +
            "or extra modifiers only in a situation (below 30% health, in the air, in combat, while casting...).\n\n" +
            "COMBAT STATS (players and mobs): Critical Chance, Armor Penetration, Threat, Cooldown Reduction, Height... and element-" +
            "limited ones (Status Resistance + Poison = poison resistance). They stack with race, class, equipment and buffs.\n\n" +
            "ONLY FOR: races / classes that may take the trait (empty = everyone). Races and classes can also forbid traits, make " +
            "them cheaper or dearer, and make them stronger or weaker (Enhance Trait effect).\n\n" +
            "COST: positive = costs trait points; negative = a drawback that gives points back. Only points actually paid are " +
            "refunded when it is removed (armor, sets and the class grant traits for free).\n" +
            "Every change is undone exactly when the trait is removed; percent bonuses follow level-ups.");

        // Summary.
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(t.Name, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"{t.Kind} · {t.type} · {t.rarity}", EditorStyles.miniLabel, GUILayout.Width(210));
        EditorGUILayout.EndHorizontal();
        string cost = t.cost > 0 ? $"Costs {t.cost} trait point{(t.cost == 1 ? "" : "s")}" : t.cost < 0 ? $"Drawback: gives {-t.cost} point{(t.cost == -1 ? "" : "s")} back" : "Free";
        EditorGUILayout.LabelField(cost + (t.availableAtCreation ? " · pickable at character creation" : " · not pickable at creation (granted by armor/quests/scripts)"), EditorStyles.miniLabel);
        showPreview = EditorGUILayout.Foldout(showPreview, new GUIContent("What the player sees", "The description shown in the character creation screen."), true);
        if (showPreview)
            EditorGUILayout.LabelField(t.GetFormattedDescription(), EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndVertical();

        // Active traits: one per character.
        if (t.HasActiveSkill)
        {
            string key = "";
            foreach (TraitBehaviour bh in t.behaviours)
                if (bh is ActiveAbilityTrait at) key = at.KeyText;
            EditorGUILayout.HelpBox("ACTIVE TRAIT: gives an ability on its own key" + (key.Length > 0 ? $" ({key})" : "") +
                                    ". A character can pick only one active trait (Trait Manager ▸ Max Active Traits). Movement traits such as " +
                                    "Double Jump, Wall Climb and Glide use existing keys and do not count.", MessageType.Info);
        }
        else if (t.Kind != TraitKind.Passive)
            EditorGUILayout.HelpBox("Uses existing keys (jump, movement): not counted as an active trait, any number can be picked.", MessageType.None);
        CharacterArchetypeEditor.DrawChanges(StatText.Of(t));

        // Buttons.
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Apply Preset ▾", "Replace this trait's settings with a ready-made trait (Undo restores).")))
            PresetMenu(t);
        if (GUILayout.Button(new GUIContent("+ Modifier ▾", "Add a passive stat change.")))
            ModifierMenu();
        if (GUILayout.Button(new GUIContent("+ Behaviour ▾", "Add an active or triggered part: Double Jump, Wall Climb, Glide, ability on a key, Second Wind...")))
            BehaviourMenu();
        EditorGUILayout.EndHorizontal();

        int convertible = t.effects.Count(e => TraitStats.TryMapLegacy(e, out _, out _, out _));
        if (convertible > 0 &&
            GUILayout.Button(new GUIContent($"Convert {convertible} Legacy Effect(s) To Modifiers", "Turn old string effects (health x1.2, speed +1...) into typed modifiers. Weapon-only keys (damage, element_*, lifesteal...) are kept.")))
            ConvertLegacy(t);

        // Checks (refreshed twice per second).
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);

        // Fields.
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
        {
            // Searching every loaded object is expensive: only every 2 seconds.
            if (managerSearch.Due)
                managers = Resources.FindObjectsOfTypeAll<TraitManager>().Where(m => m != null && m.gameObject.scene.IsValid()).ToArray();
            DrawPlayMode(t, managers);
        }
        if (GUI.changed)
            checks.Invalidate();
    }

    // ------------------------------------------------------------------ menus
    private void PresetMenu(Trait t)
    {
        var menu = new GenericMenu();
        foreach (TraitPresets.Preset p in TraitPresets.All)
        {
            TraitPresets.Preset captured = p;
            menu.AddItem(new GUIContent($"{p.category}/{p.name}", p.description), false, () =>
            {
                Undo.RecordObject(t, "Apply Trait Preset");
                TraitPresets.Apply(t, captured);
                string path = AssetDatabase.GetAssetPath(t);
                if (!string.IsNullOrEmpty(path))
                    TraitPresets.SaveRuntimeAbilities(t, System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
                EditorUtility.SetDirty(t);
            });
        }
        menu.ShowAsContext();
    }

    private void ModifierMenu()
    {
        var menu = new GenericMenu();
        foreach (TraitStat stat in Enum.GetValues(typeof(TraitStat)))
        {
            TraitStat captured = stat;
            TraitStats.StatInfo info = TraitStats.Get(stat);
            menu.AddItem(new GUIContent($"{Group(stat)}/{info.name}", info.description), false, () =>
            {
                serializedObject.Update();
                SerializedProperty list = serializedObject.FindProperty("modifiers");
                list.arraySize++;
                SerializedProperty el = list.GetArrayElementAtIndex(list.arraySize - 1);
                el.FindPropertyRelative("stat").enumValueIndex = (int)captured;
                el.FindPropertyRelative("mode").enumValueIndex = info.modes == TraitStats.Modes.FlatOnly ? 0 : 1;
                el.FindPropertyRelative("value").floatValue = TraitStats.DefaultValue(captured);
                serializedObject.ApplyModifiedProperties();
            });
        }
        menu.ShowAsContext();
    }

    private static string Group(TraitStat s)
    {
        switch (s)
        {
            case TraitStat.MaxHealth: case TraitStat.HealthRegen: case TraitStat.HealingReceived: case TraitStat.DamageTaken: return "Health & Defence";
            case TraitStat.MaxStamina: case TraitStat.StaminaRegen: case TraitStat.StaminaCost: case TraitStat.MaxMana: case TraitStat.ManaRegen: return "Stamina & Mana";
            case TraitStat.MoveSpeed: case TraitStat.SprintSpeed: case TraitStat.CrouchSpeed: case TraitStat.JumpForce: case TraitStat.CarryWeight: return "Movement";
            case TraitStat.WeaponDamage: case TraitStat.ControlDealt: case TraitStat.ControlTaken: return "Combat";
            default: return "Abilities";
        }
    }

    private void BehaviourMenu()
    {
        var menu = new GenericMenu();
        foreach (Type type in SubclassSelectorDrawer.GetTypes(typeof(TraitBehaviour)))
        {
            Type captured = type;
            var attr = (AbilityMenuAttribute)Attribute.GetCustomAttribute(type, typeof(AbilityMenuAttribute), false);
            string path = attr != null ? attr.path : ObjectNames.NicifyVariableName(type.Name);
            menu.AddItem(new GUIContent(path, attr != null ? attr.tooltip : ""), false, () =>
            {
                serializedObject.Update();
                SerializedProperty list = serializedObject.FindProperty("behaviours");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = Activator.CreateInstance(captured);
                serializedObject.ApplyModifiedProperties();
            });
        }
        menu.ShowAsContext();
    }

    private void ConvertLegacy(Trait t)
    {
        Undo.RecordObject(t, "Convert Legacy Trait Effects");
        for (int i = t.effects.Count - 1; i >= 0; i--)
        {
            TraitEffect e = t.effects[i];
            if (!TraitStats.TryMapLegacy(e, out TraitStat stat, out TraitModifierMode mode, out float value))
                continue;
            t.modifiers.Add(new TraitModifier(stat, mode, value));
            // Weapons still read "damage"/"attack" from the legacy list: keep those.
            if (!TraitStats.IsWeaponLegacyKey(e.targetStat))
                t.effects.RemoveAt(i);
        }
        EditorUtility.SetDirty(t);
    }

    // ------------------------------------------------------------------ play mode
    private static void DrawPlayMode(Trait t, TraitManager[] managers)
    {
        AbilityEditorUI.Section("Test (Play mode)");
        if (managers.Length == 0)
        {
            EditorGUILayout.LabelField("No TraitManager in the scene.", EditorStyles.miniLabel);
            return;
        }
        foreach (TraitManager m in managers)
        {
            if (m == null)
                continue;
            EditorGUILayout.BeginHorizontal();
            bool has = m.HasTrait(t);
            EditorGUILayout.LabelField(m.name + (has ? " (has it)" : ""));
            if (!has && GUILayout.Button(new GUIContent("Give", "Add this trait to the player (free, requirements ignored)."), GUILayout.Width(60)))
                m.AddTrait(t, true);
            if (has && GUILayout.Button(new GUIContent("Remove", "Remove it (every change is undone)."), GUILayout.Width(60)))
                m.RemoveTrait(t, true);
            if (has && t.Kind != TraitKind.Passive && GUILayout.Button(new GUIContent("Use", "Activate its active part (abilities)."), GUILayout.Width(50)))
                m.TryActivate(t);
            EditorGUILayout.EndHorizontal();
        }
    }
}

/// <summary>Trait menus: create from preset, validate all traits, create the Resources trait database.</summary>
public static class TraitToolsMenu
{
    [MenuItem("Assets/Create/SimpleMovements/Character/Trait From Preset...", false, 0)]
    private static void CreateFromPreset()
    {
        string folder = SelectedFolder();
        var menu = new GenericMenu();
        foreach (TraitPresets.Preset p in TraitPresets.All)
        {
            TraitPresets.Preset captured = p;
            menu.AddItem(new GUIContent($"{p.category}/{p.name}", p.description), false, () =>
            {
                Trait t = TraitPresets.CreateAsset(captured, folder);
                TraitDatabase db = TraitDatabase.Instance;
                if (db != null)
                    db.AddTrait(t);
                Selection.activeObject = t;
            });
        }
        menu.ShowAsContext();
    }

    [MenuItem("Tools/SimpleMovements/Validate/Validate All Traits")]
    private static void ValidateAll()
    {
        int n = 0, errorCount = 0, warningCount = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Trait"))
        {
            var t = AssetDatabase.LoadAssetAtPath<Trait>(AssetDatabase.GUIDToAssetPath(guid));
            if (t == null) continue;
            n++;
            var e = new List<string>();
            var w = new List<string>();
            t.Validate(e, w);
            foreach (string s in e) Debug.LogError(s, t);
            foreach (string s in w) Debug.LogWarning(s, t);
            errorCount += e.Count;
            warningCount += w.Count;
        }
        Debug.Log($"Validated {n} traits: {errorCount} errors, {warningCount} warnings.");
    }

    [MenuItem("Tools/SimpleMovements/Project Setup/Create Trait Database (Resources)")]
    private static void CreateDatabase()
    {
        TraitDatabase existing = Resources.Load<TraitDatabase>("TraitDatabase");
        if (existing != null)
        {
            Selection.activeObject = existing;
            return;
        }
        AbilityEditorUI.EnsureFolder("Assets/Resources");
        var db = ScriptableObject.CreateInstance<TraitDatabase>();
        AssetDatabase.CreateAsset(db, "Assets/Resources/TraitDatabase.asset");
        foreach (string guid in AssetDatabase.FindAssets("t:Trait"))
        {
            var t = AssetDatabase.LoadAssetAtPath<Trait>(AssetDatabase.GUIDToAssetPath(guid));
            if (t != null) db.AddTrait(t);
        }
        AssetDatabase.SaveAssets();
        Selection.activeObject = db;
    }

    private static string SelectedFolder()
    {
        foreach (Object o in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
        {
            string p = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(p)) continue;
            return AssetDatabase.IsValidFolder(p) ? p : System.IO.Path.GetDirectoryName(p).Replace('\\', '/');
        }
        return "Assets";
    }
}
#endif
