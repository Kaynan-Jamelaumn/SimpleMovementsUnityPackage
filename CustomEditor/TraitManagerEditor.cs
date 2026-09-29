#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Trait Manager inspector: starting traits as readable rows (add from the database or create from a preset), trait
/// points, the database, setup checks with Fix buttons, and in Play mode every active trait with what it applied and
/// what its behaviours are doing, plus add / remove / use buttons.
/// </summary>
[CustomEditor(typeof(TraitManager))]
public class TraitManagerEditor : Editor
{
    private static bool showHelp;
    private static bool showChecks = true;
    private static bool showRaw;
    private readonly List<AbilityEditorUI.Issue> issues = new List<AbilityEditorUI.Issue>();
    private readonly AbilityEditorUI.Throttle checks = new AbilityEditorUI.Throttle();
    private System.Action pendingFix;

    public override void OnInspectorGUI()
    {
        var m = (TraitManager)target;
        pendingFix = null;
        if (checks.Due)
        {
            issues.Clear();
            Validate(m);
        }

        showHelp = AbilityEditorUI.Help(showHelp, "What this component does",
            "Holds the character's traits and applies them: passive modifiers go into the Health/Stamina/Mana/Speed managers, " +
            "the movement model and the ability system (and are undone exactly on removal); behaviours such as Double Jump, " +
            "Wall Climb or an ability on a key run every frame.\n\n" +
            "Traits come from: Starting Traits below, the character creation screen (PlayerCreationManager), the class's " +
            "starting traits (PlayerStatusController), armor sets, or scripts (AddTrait / AddTemporaryTrait).\n\n" +
            "POINTS & SOURCES: each trait remembers the points actually paid for it and refunds exactly those. When armor, a set " +
            "or the class grants a trait the character already has, the extra grant is counted: taking that armor off keeps the " +
            "trait. Armor sets that replace a trait suspend it (effects off, points kept) instead of removing it. Percent bonuses " +
            "on max health/stamina/mana, carry weight and speed follow level-ups and are re-applied after a class reset.");

        serializedObject.Update();

        // Database & points.
        AbilityEditorUI.Section("Configuration");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("traitDatabase"));
        if (GUILayout.Button(new GUIContent("Find", "Use Resources/TraitDatabase, or the first Trait Database in the project."), GUILayout.Width(50)))
        {
            TraitDatabase db = Resources.Load<TraitDatabase>("TraitDatabase");
            if (db == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:TraitDatabase");
                if (guids.Length > 0) db = AssetDatabase.LoadAssetAtPath<TraitDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            serializedObject.FindProperty("traitDatabase").objectReferenceValue = db;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("availableTraitPoints"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxTraits"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("allowNegativeTraits"));

        // Starting traits.
        DrawStartingTraits(m);

        AbilityEditorUI.Section("Other");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("audioSource"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("armorSetManager"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("debugLog"));
        showRaw = EditorGUILayout.Foldout(showRaw, new GUIContent("Raw lists", "Active traits and armor-applied traits as stored (runtime)."), true);
        if (showRaw)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("activeTraits"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("armorAppliedTraits"), true);
            }
        }
        serializedObject.ApplyModifiedProperties();

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

    private void DrawStartingTraits(TraitManager m)
    {
        SerializedProperty list = serializedObject.FindProperty("startingTraits");
        int total = 0;
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue is Trait t) total += t.cost;
        AbilityEditorUI.Section($"Starting Traits ({list.arraySize}, total cost {total})",
            "Always given at start (free, requirements ignored). Traits picked in character creation are added on top.");

        int remove = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty el = list.GetArrayElementAtIndex(i);
            var t = el.objectReferenceValue as Trait;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(el, GUIContent.none);
            if (GUILayout.Button(new GUIContent("✕", "Remove from the starting traits."), EditorStyles.miniButton, GUILayout.Width(24)))
                remove = i;
            EditorGUILayout.EndHorizontal();
            if (t != null)
            {
                string cost = t.cost > 0 ? $"cost {t.cost}" : t.cost < 0 ? $"gives {-t.cost}" : "free";
                EditorGUILayout.LabelField($"{t.Kind} · {t.type} · {cost}", EditorStyles.miniBoldLabel);
                var lines = new List<string>();
                foreach (TraitModifier mod in t.modifiers) if (mod != null) lines.Add(mod.Describe());
                foreach (TraitBehaviour b in t.behaviours) if (b != null) lines.Add(b.Describe());
                if (lines.Count > 0)
                    EditorGUILayout.LabelField("• " + string.Join("\n• ", lines), EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndVertical();
        }
        if (remove >= 0)
        {
            list.GetArrayElementAtIndex(remove).objectReferenceValue = null;
            list.DeleteArrayElementAtIndex(remove);
        }

        EditorGUILayout.BeginHorizontal();
        TraitDatabase dbRef = serializedObject.FindProperty("traitDatabase").objectReferenceValue as TraitDatabase;
        if (GUILayout.Button(new GUIContent("+ Add Trait ▾", "Pick from the Trait Database (or every trait in the project).")))
        {
            var menu = new GenericMenu();
            List<Trait> all = dbRef != null ? dbRef.GetAllTraits() : AllTraitsInProject();
            var owned = new HashSet<Trait>();
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is Trait o) owned.Add(o);
            foreach (Trait t in all.Where(x => x != null).OrderBy(x => x.type).ThenBy(x => x.Name))
            {
                Trait captured = t;
                var label = new GUIContent($"{t.type}/{t.Name} ({(t.cost >= 0 ? "cost " + t.cost : "gives " + -t.cost)})");
                if (owned.Contains(t)) menu.AddDisabledItem(label);
                else menu.AddItem(label, false, () => AddStarting(m, captured));
            }
            if (all.Count == 0) menu.AddDisabledItem(new GUIContent("No traits yet - use Create From Preset"));
            menu.ShowAsContext();
        }
        if (GUILayout.Button(new GUIContent("+ Create From Preset ▾", "Create a new Trait asset from a preset (saved next to this prefab), add it to the database and to the starting traits.")))
        {
            var menu = new GenericMenu();
            foreach (TraitPresets.Preset p in TraitPresets.All)
            {
                TraitPresets.Preset captured = p;
                menu.AddItem(new GUIContent($"{p.category}/{p.name}", p.description), false, () =>
                {
                    Trait t = TraitPresets.CreateAsset(captured, AbilityEditorUI.FolderFor(m));
                    TraitDatabase db = dbRef != null ? dbRef : TraitDatabase.Instance;
                    if (db != null) db.AddTrait(t);
                    AddStarting(m, t);
                    EditorGUIUtility.PingObject(t);
                });
            }
            menu.ShowAsContext();
        }
        EditorGUILayout.EndHorizontal();
    }

    private static List<Trait> AllTraitsInProject()
    {
        var list = new List<Trait>();
        foreach (string guid in AssetDatabase.FindAssets("t:Trait"))
        {
            var t = AssetDatabase.LoadAssetAtPath<Trait>(AssetDatabase.GUIDToAssetPath(guid));
            if (t != null) list.Add(t);
        }
        return list;
    }

    private static void AddStarting(TraitManager m, Trait t)
    {
        var so = new SerializedObject(m);
        SerializedProperty list = so.FindProperty("startingTraits");
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = t;
        so.ApplyModifiedProperties();
    }

    // ------------------------------------------------------------------ checks
    private void Validate(TraitManager m)
    {
        GameObject go = m.gameObject;
        serializedObject.Update();
        bool hasMovementModel = go.transform.root.GetComponentInChildren<PlayerMovementModel>(true) != null;
        bool hasAbilityController = go.transform.root.GetComponentInChildren<PlayerAbilityController>(true) != null;
        var e = new List<string>();
        var w = new List<string>();
        if (go.GetComponent<PlayerStatusController>() == null && go.GetComponentInParent<PlayerStatusController>() == null)
            AbilityEditorUI.Add(issues, MessageType.Error, "No PlayerStatusController on this object or a parent: stat modifiers have nothing to change.");
        if (serializedObject.FindProperty("traitDatabase").objectReferenceValue == null && Resources.Load<TraitDatabase>("TraitDatabase") == null)
            AbilityEditorUI.Add(issues, MessageType.Warning, "No Trait Database assigned or in Resources: the character creation screen has no traits to show.", "Create",
                () => EditorApplication.ExecuteMenuItem("Tools/Traits/Create Trait Database (Resources)"));

        List<Trait> start = m.StartingTraits.Where(t => t != null).ToList();
        if (m.StartingTraits.Count != start.Count)
            AbilityEditorUI.Add(issues, MessageType.Warning, "Starting Traits has empty entries.");
        foreach (var dup in start.GroupBy(t => t).Where(g => g.Count() > 1))
            AbilityEditorUI.Add(issues, MessageType.Warning, $"'{dup.Key.Name}' is listed more than once (it is only applied once).");
        for (int i = 0; i < start.Count; i++)
        {
            Trait t = start[i];
            for (int j = i + 1; j < start.Count; j++)
            {
                Trait o = start[j];
                if (t.incompatibleTraits.Contains(o) || o.incompatibleTraits.Contains(t) || t.mutuallyExclusiveTraits.Contains(o) || o.mutuallyExclusiveTraits.Contains(t))
                    AbilityEditorUI.Add(issues, MessageType.Warning, $"'{t.Name}' and '{o.Name}' are incompatible, but starting traits ignore that and both are applied.");
            }
            foreach (Trait r in t.requiredTraits)
                if (r != null && !start.Contains(r))
                    AbilityEditorUI.Add(issues, MessageType.Info, $"'{t.Name}' normally requires '{r.Name}' (starting traits skip requirements).");

            e.Clear();
            w.Clear();
            t.Validate(e, w);
            foreach (string s in e) AbilityEditorUI.Add(issues, MessageType.Error, s);

            bool movement = t.behaviours.Any(b => b is DoubleJumpTrait || b is WallClimbTrait || b is GlideTrait);
            if (movement && !hasMovementModel)
                AbilityEditorUI.Add(issues, MessageType.Warning, $"'{t.Name}' is a movement trait but the player has no PlayerMovementModel.");
            if (t.behaviours.Any(b => b is ActiveAbilityTrait) && !hasAbilityController)
                AbilityEditorUI.Add(issues, MessageType.Warning, $"'{t.Name}' gives an ability but the player has no PlayerAbilityController.");
        }
    }

    // ------------------------------------------------------------------ live
    private void DrawLive(TraitManager m)
    {
        AbilityEditorUI.Section($"Live: {m.Traits.Count} trait(s), {m.AvailableTraitPoints} point(s) left");
        foreach (ActiveTraitInfo info in m.AllTraitInfos)
        {
            Trait t = info.trait;
            if (t == null) continue;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            string extra = (info.isTemporary ? $"  ({info.remainingDuration:0}s left)" : "") + (info.extraGrants > 0 ? $"  x{info.extraGrants + 1} sources" : "") +
                           (info.IsSuspended ? "  (suspended)" : "");
            EditorGUILayout.LabelField(new GUIContent(t.Name + extra, "Points paid: " + info.pointsSpent + (info.extraGrants > 0 ? $". Also granted by {info.extraGrants} other source(s) (armor, sets, class)." : ".")),
                EditorStyles.boldLabel);
            if (!info.IsSuspended && t.Kind != TraitKind.Passive && GUILayout.Button(new GUIContent("Use", "Activate its active part (abilities)."), EditorStyles.miniButton, GUILayout.Width(40)))
                m.TryActivate(t);
            if (GUILayout.Button(new GUIContent(info.IsSuspended ? "Resume" : "Suspend", "Test: turn its effects off / on without removing it (what armor sets do when they replace a trait)."),
                    EditorStyles.miniButton, GUILayout.Width(60)))
            {
                if (info.IsSuspended) m.ResumeTrait(t);
                else m.SuspendTrait(t);
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button(new GUIContent("Remove", "Remove the trait (every change is undone; one grant at a time when several sources grant it)."), EditorStyles.miniButton, GUILayout.Width(56)))
            {
                m.RemoveTrait(t, true);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
            foreach (string line in m.DescribeLive(t))
                EditorGUILayout.LabelField("  " + line, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }
        if (GUILayout.Button(new GUIContent("+ Give Trait ▾", "Test: add any trait now (free, requirements ignored).")))
        {
            var menu = new GenericMenu();
            List<Trait> all = m.Database != null ? m.Database.GetAllTraits() : AllTraitsInProject();
            foreach (Trait t in all.Where(x => x != null).OrderBy(x => x.Name))
            {
                Trait captured = t;
                if (m.HasTrait(t)) menu.AddDisabledItem(new GUIContent(t.Name));
                else menu.AddItem(new GUIContent($"{t.type}/{t.Name}"), false, () => m.AddTrait(captured, true));
            }
            menu.ShowAsContext();
        }
    }
}
#endif
