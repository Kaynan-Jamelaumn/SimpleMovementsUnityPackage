#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Mob ability inspector. Splits the component into clearly labelled parts:
/// 1. Ability Slots (new system), 2. Legacy Abilities (old system, with a one-click conversion),
/// 3. Absorption (chance per kill + a share per ability that never adds up to more than 100%),
/// 4. Casting settings, then setup checks. Scene preview and live cooldowns come from <see cref="AbilityCasterEditor"/>.
/// </summary>
[CustomEditor(typeof(MobAbilityController))]
public class MobAbilityControllerEditor : AbilityCasterEditor
{
    private static bool showHelp = true;
    private static bool showSlotSummary = true;
    private static readonly Color[] ShareColors =
    {
        new Color(0.91f, 0.42f, 0.23f), new Color(0.24f, 0.56f, 0.86f), new Color(0.36f, 0.72f, 0.42f),
        new Color(0.78f, 0.45f, 0.82f), new Color(0.93f, 0.73f, 0.25f), new Color(0.35f, 0.75f, 0.75f),
        new Color(0.85f, 0.35f, 0.5f), new Color(0.6f, 0.6f, 0.3f),
    };

    private readonly List<Object> configured = new List<Object>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private GUIStyle wrap;
    // The absorption-table sync and the checks run at most twice per second (running them on every redraw, with an
    // Undo snapshot each time, made the inspector slow).
    private readonly AbilityEditorUI.Throttle checks = new AbilityEditorUI.Throttle();
    private bool refreshNow;

    private static readonly string[] Handled =
    {
        "m_Script", "slots", "maxSlots", "castPoint", "animator", "globalCooldown", "turnSpeed", "showTelegraphs", "debugLog",
        "mobActionController", "legacyAbilities", "absorbable", "absorbChancePercent", "absorptionTable",
    };

    public override void OnInspectorGUI()
    {
        var c = (MobAbilityController)target;
        if (wrap == null)
            wrap = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };
        refreshNow = checks.Due;

        DrawHelp();
        serializedObject.Update();
        DrawMobLink(c);
        DrawSlots(c);
        DrawLegacy(c);
        serializedObject.ApplyModifiedProperties();

        DrawAbsorption(c);

        serializedObject.Update();
        DrawCasting(c);
        DrawPropertiesExcluding(serializedObject, Handled);
        serializedObject.ApplyModifiedProperties();

        DrawChecks(c);
        AbilityEditorUI.KeepRepainting(this);
        if (GUI.changed)
            checks.Invalidate();
    }

    // ------------------------------------------------------------------ help
    private void DrawHelp()
    {
        showHelp = EditorGUILayout.BeginFoldoutHeaderGroup(showHelp, "How this component works");
        if (showHelp)
        {
            EditorGUILayout.HelpBox(
                "① ABILITY SLOTS (new system) - what you should use. Each slot holds an Ability Definition asset " +
                "(shapes, projectiles, surges, walls, AI hints, absorption variants) plus per-slot Modifiers, AI Weight and Enabled.\n\n" +
                "② LEGACY ABILITIES (old system) - the old AbilityEffectSO list. It still works: every entry is converted when " +
                "the game starts and added after the slots. Press 'Convert all' to turn them into assets in ①.\n\n" +
                "Automatic basic attack - if the mob has no melee ability, one is made from Mob ▸ Bite Damage / Attack Distance / " +
                "Bite Cooldown. It is never absorbable.\n\n" +
                "③ ABSORPTION - when the player kills this mob: ONE roll with 'Absorb Chance Per Kill' decides IF an ability is " +
                "granted, then the table decides WHICH one by its share. Shares add up to 100% at most (with B at 90%, A can " +
                "be 10% at most); what is left is 'no ability'.\n\n" +
                "The AI decides when to use each ability (range, line of sight, cooldowns, AI hints on the ability asset).",
                MessageType.Info);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void Section(string title, string subtitle)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(subtitle))
            EditorGUILayout.LabelField(subtitle, EditorStyles.wordWrappedMiniLabel);
    }

    // ------------------------------------------------------------------ mob link
    private void DrawMobLink(MobAbilityController c)
    {
        Section("Mob", null);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mobActionController"), new GUIContent("Mob",
            "The MobActionsController this belongs to. Empty = found on this object at runtime."));
        if (GUILayout.Button(new GUIContent("Auto-assign", "Fill the Mob and Animator references from this object."), GUILayout.Width(90)))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(c, "Auto-assign Mob Ability References");
            c.AutoAssignReferences();
            MarkDirty(c);
            serializedObject.Update();
        }
        EditorGUILayout.EndHorizontal();
        Mob m = c.GetComponent<Mob>();
        if (m == null)
            EditorGUILayout.HelpBox("No Mob / MobActionsController on this object. Add one (or use Tools ▸ SimpleMovements ▸ Mobs ▸ Add Missing Components).", MessageType.Error);
    }

    // ------------------------------------------------------------------ 1. slots
    private void DrawSlots(MobAbilityController c)
    {
        Section("① Ability Slots - new system (Ability Definition assets)",
            "Put the mob's abilities here. Per slot: Ability (the asset), Modifiers (a weaker/stronger version without " +
            "editing the asset), Enabled, AI Weight (how often the AI picks it; 0 = never on its own) and Label.");
        SerializedProperty slots = serializedObject.FindProperty("slots");
        EditorGUILayout.PropertyField(slots, new GUIContent($"Ability Slots ({slots.arraySize})",
            "New-system abilities of this mob. Order does not matter for the AI."), true);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Add Ability From Preset ▾", "Create a new Ability Definition asset from a preset (saved next to this prefab) and put it in a new slot.")))
            ShowPresetMenu(c);
        using (new EditorGUI.DisabledScope(!HasEmptySlots(slots)))
        {
            if (GUILayout.Button(new GUIContent("Remove Empty Slots", "Delete slots that have no ability assigned.")))
            {
                for (int i = slots.arraySize - 1; i >= 0; i--)
                    if (slots.GetArrayElementAtIndex(i).FindPropertyRelative("ability").objectReferenceValue == null)
                        slots.DeleteArrayElementAtIndex(i);
            }
        }
        EditorGUILayout.EndHorizontal();

        Mob ownMob = c.GetComponent<Mob>();
        if (ownMob != null && !c.HasMeleeSlot() && !Application.isPlaying)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(ownMob.BiteDamage > 0
                    ? $"No melee ability in the slots: at runtime the mob gets an automatic bite ({ownMob.BiteDamage} dmg, {ownMob.AttackDistance:0.#}m, every {ownMob.BiteCooldown:0.#}s) that you cannot see or edit here."
                    : "No melee ability and Bite Damage is 0: the mob has no close-range attack.",
                EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(ownMob.BiteDamage <= 0))
            {
                if (GUILayout.Button(new GUIContent("Make It An Asset", "Save the automatic bite as an Ability Definition asset in a new slot so you can edit it (shape, effects, animation, AI hints)."),
                        GUILayout.Width(118), GUILayout.MinHeight(30)))
                {
                    serializedObject.ApplyModifiedProperties();
                    MobSetupUtility.CreateBasicAttackAsset(c, ownMob);
                    serializedObject.Update();
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        showSlotSummary = EditorGUILayout.Foldout(showSlotSummary, new GUIContent(Application.isPlaying ? "Slot summary (live cooldowns)" : "Slot summary, checks & Scene preview",
            "Timings, reach and damage of each slot; 'Preview' draws its hit areas around the mob in the Scene view."), true);
        if (showSlotSummary)
        {
            EditorGUI.indentLevel++;
            DrawSlotsOverview(c);
            EditorGUI.indentLevel--;
        }
    }

    private static bool HasEmptySlots(SerializedProperty slots)
    {
        for (int i = 0; i < slots.arraySize; i++)
            if (slots.GetArrayElementAtIndex(i).FindPropertyRelative("ability").objectReferenceValue == null)
                return true;
        return false;
    }

    private void ShowPresetMenu(MobAbilityController c)
    {
        var menu = new GenericMenu();
        foreach (AbilityPresets.Preset p in AbilityPresets.All)
        {
            AbilityPresets.Preset captured = p;
            menu.AddItem(new GUIContent(p.name, p.description), false, () =>
            {
                AbilityDefinition def = captured.create();
                AbilityDefinition asset = SaveAbilityAsset(def, FolderFor(c), def.DisplayName);
                AddSlot(c, asset);
            });
        }
        menu.ShowAsContext();
    }

    private void AddSlot(MobAbilityController c, AbilityDefinition ability)
    {
        serializedObject.Update();
        SerializedProperty slots = serializedObject.FindProperty("slots");
        int i = slots.arraySize;
        slots.arraySize++;
        SerializedProperty s = slots.GetArrayElementAtIndex(i);
        s.FindPropertyRelative("ability").objectReferenceValue = ability;
        s.FindPropertyRelative("enabled").boolValue = true;
        s.FindPropertyRelative("aiWeight").floatValue = 1f;
        s.FindPropertyRelative("label").stringValue = "";
        serializedObject.ApplyModifiedProperties();
    }

    // ------------------------------------------------------------------ 2. legacy
    private void DrawLegacy(MobAbilityController c)
    {
        Section("② Legacy Abilities - old system (AbilityEffectSO)",
            "Kept so existing prefabs keep working: each entry is converted when the game starts and used like an extra " +
            "slot. Don't add new abilities here - use ① instead.");
        SerializedProperty legacy = serializedObject.FindProperty("legacyAbilities");
        EditorGUILayout.PropertyField(legacy, new GUIContent($"Legacy Abilities ({legacy.arraySize})",
            "Old AbilityHolder entries (AbilityEffectSO + attack casts + particle)."), true);

        int convertible = 0;
        foreach (AbilityHolder h in c.LegacyAbilities)
            if (h != null && h.abilityEffect != null) convertible++;
        if (convertible > 0)
        {
            EditorGUILayout.HelpBox($"{convertible} legacy abilit{(convertible == 1 ? "y" : "ies")}. Converting saves an Ability Definition asset " +
                                    "next to each AbilityEffectSO (timings, effects, attack casts and particle included), puts it in a slot and " +
                                    "keeps its absorption share.", MessageType.None);
            if (GUILayout.Button(new GUIContent("Convert All To Ability Definitions ➜ ①",
                    "Creates the assets, moves every legacy entry into the Ability Slots and empties this list (Undo restores it).")))
            {
                serializedObject.ApplyModifiedProperties();
                ConvertLegacy(c);
                serializedObject.Update();
                GUIUtility.ExitGUI();
            }
        }
    }

    private void ConvertLegacy(MobAbilityController c)
    {
        var converted = new List<KeyValuePair<AbilityEffectSO, AbilityDefinition>>();
        foreach (AbilityHolder h in c.LegacyAbilities)
        {
            if (h == null || h.abilityEffect == null)
                continue;
            AbilityDefinition def = LegacyAbilityConverter.CreateDefinition(h.abilityEffect, true, h.attackCast, h.particle);
            string src = AssetDatabase.GetAssetPath(h.abilityEffect);
            string folder = string.IsNullOrEmpty(src) ? FolderFor(c) : System.IO.Path.GetDirectoryName(src).Replace('\\', '/');
            AbilityDefinition asset = SaveAbilityAsset(def, folder, h.abilityEffect.name + " (Mob)");
            converted.Add(new KeyValuePair<AbilityEffectSO, AbilityDefinition>(h.abilityEffect, asset));
        }
        if (converted.Count == 0)
            return;

        Undo.RecordObject(c, "Convert Legacy Mob Abilities");
        serializedObject.Update();
        SerializedProperty slots = serializedObject.FindProperty("slots");
        foreach (KeyValuePair<AbilityEffectSO, AbilityDefinition> kv in converted)
        {
            int i = slots.arraySize;
            slots.arraySize++;
            SerializedProperty s = slots.GetArrayElementAtIndex(i);
            s.FindPropertyRelative("ability").objectReferenceValue = kv.Value;
            s.FindPropertyRelative("enabled").boolValue = true;
            s.FindPropertyRelative("aiWeight").floatValue = 1f;
        }
        serializedObject.FindProperty("legacyAbilities").arraySize = 0;
        serializedObject.ApplyModifiedProperties();

        // Keep each ability's absorption share.
        foreach (MobAbsorptionEntry e in c.AbsorptionTable)
        {
            if (e == null || e.legacyAbility == null)
                continue;
            foreach (KeyValuePair<AbilityEffectSO, AbilityDefinition> kv in converted)
            {
                if (kv.Key != e.legacyAbility)
                    continue;
                e.ability = kv.Value;
                e.legacyAbility = null;
                break;
            }
        }
        MarkDirty(c);
        Debug.Log($"[{c.name}] Converted {converted.Count} legacy abilit{(converted.Count == 1 ? "y" : "ies")} into Ability Slots.", c);
    }

    // ------------------------------------------------------------------ 3. absorption
    private void DrawAbsorption(MobAbilityController c)
    {
        Section("③ Absorption - what the player can take from this mob",
            "Step 1: one roll with 'Absorb Chance Per Kill' decides IF the player gets an ability. " +
            "Step 2: the shares below decide WHICH one. The player receives a variant of it (Weaker / Same / Stronger / Altered).");

        // Keep the table in sync with the abilities (new abilities start at 0%, removed ones disappear).
        if (!Application.isPlaying && refreshNow)
        {
            Undo.RecordObject(c, "Sync Absorption Table");
            if (c.SyncAbsorptionTable(configured))
                MarkDirty(c);
        }

        EditorGUI.BeginChangeCheck();
        bool absorbable = EditorGUILayout.Toggle(new GUIContent("Absorbable",
            "ON: killing this mob can give the player one of its abilities. OFF: never. Summoned copies never can."), c.Absorbable);
        float chance;
        using (new EditorGUI.DisabledScope(!absorbable))
            chance = EditorGUILayout.Slider(new GUIContent("Absorb Chance Per Kill (%)",
                "Chance that a kill grants an ability AT ALL. Multiplied by the Mob Profile's Absorb Chance Multiplier and the global multiplier in Absorption Settings."),
                c.AbsorbChancePercent, 0f, 100f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(c, "Change Absorption");
            c.Absorbable = absorbable;
            c.AbsorbChancePercent = chance;
            MarkDirty(c);
        }
        if (!c.Absorbable)
            return;

        // Effective chance breakdown.
        Mob mob = c.GetComponent<Mob>();
        float profileMul = mob != null ? mob.Profile.absorbChanceMultiplier : 1f;
        AbsorptionSettings settings = AbsorptionSettings.Instance;
        float perKill = c.EffectiveChancePerKill;
        EditorGUILayout.LabelField(
            $"Per kill: {c.AbsorbChancePercent:0.#}% × profile {profileMul:0.##} × global {settings.globalChanceMultiplier:0.##}" +
            $"{(settings.enabled ? "" : " (absorption is OFF in Absorption Settings)")} = <b>{perKill * 100f:0.##}%</b>", wrap);

        List<MobAbsorptionEntry> table = c.AbsorptionTable;
        if (table.Count == 0)
        {
            EditorGUILayout.HelpBox("This mob has no abilities to absorb yet. Add abilities in ① or ②.", MessageType.Info);
            return;
        }

        // Stacked bar: each ability's share + the 'nothing' remainder.
        DrawShareBar(table);

        // Rows.
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        float total = c.TotalSharePercent;
        for (int i = 0; i < table.Count; i++)
        {
            MobAbsorptionEntry e = table[i];
            if (e == null)
                continue;
            bool allowed = MobAbilityController.EntryAllowed(e);
            EditorGUILayout.BeginHorizontal();
            Rect sw = GUILayoutUtility.GetRect(10, 16, GUILayout.Width(10));
            EditorGUI.DrawRect(new Rect(sw.x, sw.y + 3, 10, 10), allowed ? ShareColors[i % ShareColors.Length] : Color.gray);
            string kind = e.ability != null ? "slot" : "legacy";
            EditorGUILayout.LabelField(new GUIContent($"{e.DisplayName}", $"{kind}: {(e.ability != null ? (Object)e.ability : e.legacyAbility)}"),
                GUILayout.MinWidth(80), GUILayout.MaxWidth(170));
            using (new EditorGUI.DisabledScope(!allowed))
            {
                float others = total - Mathf.Max(0f, e.sharePercent);
                float max = Mathf.Max(0f, 100f - others);
                EditorGUI.BeginChangeCheck();
                float v = EditorGUILayout.Slider(GUIContent.none, e.sharePercent, 0f, 100f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(c, "Change Absorption Share");
                    float set = c.SetShare(i, v);
                    if (v > max + 0.001f)
                        ShowNotification(new GUIContent($"Max {set:0.#}% - the other abilities already use {others:0.#}%"));
                    MarkDirty(c);
                    total = c.TotalSharePercent;
                }
            }
            EditorGUILayout.LabelField(new GUIContent(allowed ? $"{perKill * e.sharePercent:0.##}%/kill" : "blocked",
                    allowed ? "Chance per kill of getting exactly this ability (chance per kill × share)."
                            : "The ability asset has Absorption ▸ Can Be Absorbed off."),
                EditorStyles.miniLabel, GUILayout.Width(74));
            EditorGUILayout.EndHorizontal();
        }
        float remainder = Mathf.Max(0f, 100f - total);
        EditorGUILayout.BeginHorizontal();
        Rect nr = GUILayoutUtility.GetRect(10, 16, GUILayout.Width(10));
        EditorGUI.DrawRect(new Rect(nr.x, nr.y + 3, 10, 10), new Color(0.5f, 0.5f, 0.5f, 0.35f));
        EditorGUILayout.LabelField(new GUIContent("No ability (left over)", "The part of 100% not given to any ability: the roll succeeds but nothing is granted."),
            GUILayout.MinWidth(80), GUILayout.MaxWidth(170));
        EditorGUILayout.LabelField($"{remainder:0.#}%", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField($"Total given to abilities: {total:0.#}% of 100%", total > 100.01f ? EditorStyles.boldLabel : EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Split Evenly", "Every absorbable ability gets the same share (100% in total).")))
            Change(c, "Split Absorption Shares", () => c.SplitSharesEvenly());
        if (GUILayout.Button(new GUIContent("Fit to 100%", "Scale the shares so they add up to exactly 100%, keeping their proportions.")))
            Change(c, "Normalize Absorption Shares", () => c.NormalizeShares());
        if (GUILayout.Button(new GUIContent("By AI Priority", "Shares proportional to each ability's Mob AI ▸ Priority (signature abilities are absorbed more often).")))
            Change(c, "Absorption Shares By Priority", () => ByPriority(c));
        if (GUILayout.Button(new GUIContent("All 0%", "Nothing can be absorbed until you give shares.")))
            Change(c, "Clear Absorption Shares", () => { foreach (MobAbsorptionEntry e in c.AbsorptionTable) if (e != null) e.sharePercent = 0f; });
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(VariantSummary(settings), EditorStyles.wordWrappedMiniLabel);
    }

    private static void DrawShareBar(List<MobAbsorptionEntry> table)
    {
        Rect r = GUILayoutUtility.GetRect(100, 14, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.25f));
        float x = r.x;
        for (int i = 0; i < table.Count; i++)
        {
            MobAbsorptionEntry e = table[i];
            if (e == null || e.sharePercent <= 0f)
                continue;
            float w = r.width * Mathf.Clamp01(e.sharePercent / 100f);
            w = Mathf.Min(w, r.xMax - x);
            if (w <= 0f)
                break;
            EditorGUI.DrawRect(new Rect(x, r.y, w, r.height),
                MobAbilityController.EntryAllowed(e) ? ShareColors[i % ShareColors.Length] : Color.gray);
            if (w > 40f)
                GUI.Label(new Rect(x + 3, r.y - 1, w - 3, r.height + 2), $"{e.sharePercent:0.#}%", EditorStyles.miniBoldLabel);
            x += w;
        }
    }

    private static string VariantSummary(AbsorptionSettings settings)
    {
        float total = 0f;
        foreach (AbilityVariant v in settings.defaultVariants)
            if (v != null && v.weight > 0f) total += v.weight;
        if (total <= 0f)
            return "The player receives the ability unchanged (no default variants in Absorption Settings).";
        var parts = new List<string>();
        foreach (AbilityVariant v in settings.defaultVariants)
        {
            if (v == null || v.weight <= 0f)
                continue;
            string label = !string.IsNullOrEmpty(v.name) ? v.name : v.tier.ToString();
            parts.Add($"{label} {v.weight / total * 100f:0}%");
        }
        return "What the player receives: a variant rolled from Absorption Settings (" + string.Join(" · ", parts) +
               "), plus each ability's own Custom Variants. Set them on the ability asset ▸ Absorption.";
    }

    private static void ByPriority(MobAbilityController c)
    {
        float total = 0f;
        foreach (MobAbsorptionEntry e in c.AbsorptionTable)
            if (MobAbilityController.EntryAllowed(e)) total += Priority(e);
        foreach (MobAbsorptionEntry e in c.AbsorptionTable)
        {
            if (e == null)
                continue;
            e.sharePercent = total > 0f && MobAbilityController.EntryAllowed(e) ? Priority(e) / total * 100f : 0f;
        }
        c.NormalizeShares(); // rounds to 0.01% and makes the total exactly 100%
    }

    private static float Priority(MobAbsorptionEntry e) => e.ability != null ? Mathf.Max(0.05f, e.ability.ai.priority) : 1f;

    private void Change(MobAbilityController c, string undo, System.Action action)
    {
        Undo.RecordObject(c, undo);
        action();
        MarkDirty(c);
    }

    private void ShowNotification(GUIContent message)
    {
        EditorWindow w = EditorWindow.focusedWindow;
        if (w != null)
            w.ShowNotification(message, 1.5f);
    }

    // ------------------------------------------------------------------ 4. casting
    private void DrawCasting(MobAbilityController c)
    {
        Section("④ Casting", "How abilities are cast: where projectiles come out, which Animator gets the triggers, pacing.");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("castPoint"), new GUIContent("Cast Point",
            "Where projectiles and beams come out (mouth, hand, staff tip). Empty = in front of the chest."));
        if (GUILayout.Button(new GUIContent("Create", "Create a 'CastPoint' child at chest height in front of the mob and assign it."), GUILayout.Width(60)))
            CreateCastPoint(c);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animator"), new GUIContent("Animator",
            "Receives the abilities' cast/release triggers. Empty = found automatically (Model/VisualModel or any child)."));
        if (GUILayout.Button(new GUIContent("Find", "Assign the mob's Animator (Model/VisualModel or the first Animator in the children)."), GUILayout.Width(60)))
        {
            Animator a = MobMovementStateMachine.FindAnimator(c.transform);
            if (a == null)
                ShowNotification(new GUIContent("No Animator found in the children"));
            serializedObject.FindProperty("animator").objectReferenceValue = a;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("globalCooldown"), new GUIContent("Global Cooldown (s)",
            "Minimum seconds between the start of two abilities. Mobs use at least 0.35 s so attacks do not chain instantly."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("turnSpeed"), new GUIContent("Turn Speed (°/s)",
            "How fast the mob turns toward its aim while casting."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxSlots"), new GUIContent("Max Slots",
            "Maximum number of slots (0 = no limit). Mobs rarely need a limit."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showTelegraphs"), new GUIContent("Show Telegraphs",
            "Draw ground warnings for this mob's abilities so the player can dodge them."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("debugLog"), new GUIContent("Debug Log",
            "Log casts, failures, interruptions and setup warnings to the Console."));
    }

    private void CreateCastPoint(MobAbilityController c)
    {
        float height = 1.6f;
        CapsuleCollider capsule = c.GetComponent<CapsuleCollider>();
        UnityEngine.AI.NavMeshAgent agent = c.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (capsule != null) height = capsule.height;
        else if (agent != null) height = agent.height;
        var go = new GameObject("CastPoint");
        Undo.RegisterCreatedObjectUndo(go, "Create Cast Point");
        go.transform.SetParent(c.transform, false);
        go.transform.localPosition = new Vector3(0f, height * 0.7f, Mathf.Max(0.3f, (capsule != null ? capsule.radius : 0.4f) + 0.1f));
        serializedObject.FindProperty("castPoint").objectReferenceValue = go.transform;
    }

    // ------------------------------------------------------------------ checks
    private void DrawChecks(MobAbilityController c)
    {
        if (refreshNow)
        {
            errors.Clear();
            warnings.Clear();
            c.Validate(errors, warnings);
        }
        if (errors.Count + warnings.Count == 0)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("✓ Setup looks good.", EditorStyles.miniLabel);
            return;
        }
        Section($"Setup checks ({errors.Count} errors, {warnings.Count} warnings)", null);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
    }

    // ------------------------------------------------------------------ helpers
    private static void MarkDirty(Object o)
    {
        EditorUtility.SetDirty(o);
        if (PrefabUtility.IsPartOfPrefabInstance(o))
            PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }

    private static AbilityDefinition SaveAbilityAsset(AbilityDefinition def, string folder, string fileName)
    {
        foreach (char ch in System.IO.Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(ch, '_');
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
        AssetDatabase.CreateAsset(def, path);
        def.SetId(AssetDatabase.AssetPathToGUID(path));
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(def);
        return def;
    }

    /// <summary>The folder of the mob's prefab (asset, instance or open prefab stage), else "Assets".</summary>
    private static string FolderFor(Component c)
    {
        string path = AssetDatabase.GetAssetPath(c.gameObject);
        if (string.IsNullOrEmpty(path))
            path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(c.gameObject);
#if UNITY_2021_2_OR_NEWER
        if (string.IsNullOrEmpty(path))
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                path = stage.assetPath;
        }
#endif
        return string.IsNullOrEmpty(path) ? "Assets" : System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
    }
}
#endif
