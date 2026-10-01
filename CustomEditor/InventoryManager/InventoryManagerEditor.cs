#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector of the <see cref="InventoryManager"/>: a setup check with one-click fixes, the UI Builder, the
/// references grouped with explanations, layout presets, and live tools while playing (equipped items, combat stats,
/// give / remove items, sync). It replaces the previous multi-part editor and the second SlotManagerEditor that
/// competed for the same component.
/// </summary>
[CustomEditor(typeof(InventoryManager))]
public class InventoryManagerEditor : Editor
{
    private InventoryManager manager;
    private InventoryEditorStyles styles;
    private List<InventoryIssue> issues = new List<InventoryIssue>();
    private double nextValidation;
    private bool showReferences = true;
    private bool showSlotManager;
    private bool showLayout;
    private bool showRuntime = true;
    private bool showSettingsIo;

    // Runtime tools
    private ItemSO giveItem;
    private int giveQuantity = 1;
    private Vector2 runtimeScroll;

    private static readonly string[] ReferenceProperties =
    {
        "player", "playerStatusController", "weaponController", "armorSetManager", "armorSetUIManager",
        "itemPrefab", "inventoryParent", "equippableInventory", "storageParent", "handParent", "itemInfo", "cam",
    };

    private void OnEnable()
    {
        manager = (InventoryManager)target;
        styles = new InventoryEditorStyles();
        showReferences = SessionState.GetBool("InvEditor.refs", true);
        showRuntime = SessionState.GetBool("InvEditor.runtime", true);
        Revalidate();
    }

    private void Revalidate()
    {
        issues = InventoryValidator.Validate(manager);
        nextValidation = EditorApplication.timeSinceStartup + 1.0;
    }

    public override void OnInspectorGUI()
    {
        styles.InitializeStyles();
        serializedObject.Update();

        // Validation is cached (it searches the scene): refreshed at most once a second and after changes.
        if (EditorApplication.timeSinceStartup >= nextValidation)
            Revalidate();

        DrawHeaderBar();
        DrawIssues();

        EditorGUI.BeginChangeCheck();
        DrawReferences();
        DrawSlotConfiguration();
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            Revalidate();
        }

        DrawLayoutTools();
        DrawRuntimeTools();
        DrawSettingsIo();

        serializedObject.ApplyModifiedProperties();
        if (Application.isPlaying)
            Repaint();
    }

    // ------------------------------------------------------------------ header & issues
    private void DrawHeaderBar()
    {
        int errors = issues.Count(i => i.severity == MessageType.Error);
        int warnings = issues.Count(i => i.severity == MessageType.Warning);
        EditorGUILayout.BeginVertical(styles.BoxStyle);
        EditorGUILayout.LabelField("Inventory Manager", styles.HeaderStyle);
        string status = errors > 0 ? $"✗ {errors} problem(s) to fix" : warnings > 0 ? $"⚠ Ready, {warnings} warning(s)" : "✓ Setup complete";
        Color old = GUI.color;
        GUI.color = errors > 0 ? styles.ErrorColor : warnings > 0 ? styles.WarningColor : styles.SuccessColor;
        EditorGUILayout.LabelField(status, styles.CenteredStyle);
        GUI.color = old;

        if (EditorUtility.IsPersistent(manager))
            EditorGUILayout.HelpBox("This is the prefab asset. Open it (double-click) to build its UI: in Prefab Mode the canvas, hotbar, " +
                                    "inventory and equipment slots, status bars and interaction prompt are created and wired automatically.", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(Application.isPlaying || EditorUtility.IsPersistent(manager)))
        {
            if (GUILayout.Button(new GUIContent("Build / Repair UI Now", "Creates and wires everything that is missing (canvas, hotbar, inventory and equipment slots, storage, item info, status bars, interaction prompt) with the current slot counts.")))
            {
                InventoryAutoSetup.TryBuild(manager.gameObject, force: true);
                serializedObject.Update();
                Revalidate();
            }
            if (GUILayout.Button(new GUIContent("UI Builder…", "Opens the UI Builder window to choose sizes, slot counts, equipment slots and colours.")))
                InventoryUIBuilderWindow.Open(manager);
            if (GUILayout.Button(new GUIContent("Auto-Assign", "Fills every empty reference that can be found without guessing.")))
            {
                int n = InventoryValidator.AutoAssign(manager);
                serializedObject.Update();
                Revalidate();
                Debug.Log($"[Inventory] Auto-assign fixed {n} reference(s).", manager);
            }
        }
        if (GUILayout.Button(new GUIContent("Re-check", "Runs the setup check again.")))
            Revalidate();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawIssues()
    {
        if (issues.Count == 0)
            return;
        foreach (InventoryIssue issue in issues.OrderBy(i => i.severity == MessageType.Error ? 0 : i.severity == MessageType.Warning ? 1 : 2))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox(issue.message, issue.severity);
            if (issue.fix != null && !string.IsNullOrEmpty(issue.fixLabel))
            {
                using (new EditorGUI.DisabledScope(Application.isPlaying && !issue.fixLabel.EndsWith("…")))
                {
                    if (GUILayout.Button(issue.fixLabel, GUILayout.Width(118f), GUILayout.Height(38f)))
                    {
                        issue.fix();
                        serializedObject.Update();
                        Revalidate();
                        GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    // ------------------------------------------------------------------ references
    private void DrawReferences()
    {
        showReferences = EditorGUILayout.Foldout(showReferences, "References", true, styles.SubHeaderStyle);
        SessionState.SetBool("InvEditor.refs", showReferences);
        if (!showReferences)
            return;
        EditorGUILayout.BeginVertical(styles.BoxStyle);
        EditorGUILayout.LabelField("Player", EditorStyles.boldLabel);
        Field("player");
        Field("playerStatusController");
        Field("weaponController");
        Field("armorSetManager");
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Items & Panels", EditorStyles.boldLabel);
        Field("itemPrefab");
        Field("inventoryParent");
        Field("equippableInventory");
        Field("storageParent");
        Field("itemInfo");
        Field("armorSetUIManager");
        Field("handParent");
        Field("cam");
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        Field("enableArmorOptimization");
        Field("showArmorSetNotifications");
        EditorGUILayout.EndVertical();
    }

    private void Field(string name)
    {
        SerializedProperty p = serializedObject.FindProperty(name);
        if (p == null)
            return;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(p, true);
        if (p.propertyType == SerializedPropertyType.ObjectReference)
        {
            bool ok = p.objectReferenceValue != null;
            Color old = GUI.color;
            GUI.color = ok ? styles.SuccessColor : styles.WarningColor;
            GUILayout.Label(ok ? "✓" : "–", GUILayout.Width(14f));
            GUI.color = old;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSlotConfiguration()
    {
        showSlotManager = EditorGUILayout.Foldout(showSlotManager, "Slots (Slot Manager, Layout Manager)", true, styles.SubHeaderStyle);
        if (!showSlotManager)
            return;
        EditorGUILayout.BeginVertical(styles.BoxStyle);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("numberOfHotBarSlots"), new GUIContent("Hotbar Slots", "Hotbar slots created at start (number keys 1-9 select them)."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("numberOfInventorySlots"), new GUIContent("Inventory Slots", "Inventory slots created at start."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("slotManager"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("uiLayoutManager"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("specialEffectIds"), true);
        EditorGUILayout.EndVertical();
    }

    // ------------------------------------------------------------------ layout
    private void DrawLayoutTools()
    {
        showLayout = EditorGUILayout.Foldout(showLayout, "Layout Presets", true, styles.SubHeaderStyle);
        if (!showLayout)
            return;
        EditorGUILayout.BeginVertical(styles.BoxStyle);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Slots are created when the game starts: presets and slot counts apply in Play Mode. Use the UI Builder to change the edit-time look.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            foreach (UILayoutManager.LayoutPreset preset in System.Enum.GetValues(typeof(UILayoutManager.LayoutPreset)))
                if (GUILayout.Button(ObjectNames.NicifyVariableName(preset.ToString()), EditorStyles.miniButton))
                    manager.SetLayoutPreset(preset);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 Hotbar")) manager.AddHotbarSlots(1);
            if (GUILayout.Button("-1 Hotbar")) manager.RemoveHotbarSlots(1);
            if (GUILayout.Button("+5 Inventory")) manager.AddInventorySlots(5);
            if (GUILayout.Button("-5 Inventory")) manager.RemoveInventorySlots(5);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Adaptive Grid")) manager.ForceAdaptiveGrid();
            EditorGUILayout.LabelField($"Panel use: {manager.GetPanelUtilization():P0}");
        }
        EditorGUILayout.EndVertical();
    }

    // ------------------------------------------------------------------ runtime
    private void DrawRuntimeTools()
    {
        if (!Application.isPlaying)
            return;
        showRuntime = EditorGUILayout.Foldout(showRuntime, "Live (Play Mode)", true, styles.SubHeaderStyle);
        SessionState.SetBool("InvEditor.runtime", showRuntime);
        if (!showRuntime)
            return;

        EditorGUILayout.BeginVertical(styles.BoxStyle);
        EditorGUILayout.LabelField($"Slots: {manager.NumberOfHotBarSlots} hotbar, {manager.NumberOfInventorySlots} inventory, {manager.EquipmentSlots.Count} equipment");
        EditorGUILayout.LabelField($"Carried weight: {manager.GetTotalInventoryWeight():0.#} kg");

        // Give / remove items
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Items", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        giveItem = (ItemSO)EditorGUILayout.ObjectField(giveItem, typeof(ItemSO), false);
        giveQuantity = Mathf.Max(1, EditorGUILayout.IntField(giveQuantity, GUILayout.Width(50f)));
        using (new EditorGUI.DisabledScope(giveItem == null))
        {
            if (GUILayout.Button("Give", GUILayout.Width(50f)))
            {
                int left = manager.AddItem(giveItem, giveQuantity);
                if (left > 0) Debug.LogWarning($"[Inventory] No room for {left} x {giveItem.Name}.", manager);
            }
            if (GUILayout.Button("Remove", GUILayout.Width(62f)))
                manager.RemoveItems(giveItem, giveQuantity);
        }
        EditorGUILayout.EndHorizontal();
        if (giveItem != null)
            EditorGUILayout.LabelField($"In inventory: {manager.GetItemCount(giveItem)}", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(manager.IsInventoryOpened ? "Close Inventory" : "Open Inventory"))
        {
            if (manager.IsInventoryOpened) manager.CloseInventory();
            else manager.OpenInventory();
        }
        if (GUILayout.Button("Sync Equipment")) manager.NotifyInventoryChanged();
        if (GUILayout.Button("Log State")) { manager.LogInventoryState(); manager.LogArmorSummary(); }
        EditorGUILayout.EndHorizontal();

        // Equipment & stats
        EquipmentManager eq = manager.Equipment;
        runtimeScroll = EditorGUILayout.BeginScrollView(runtimeScroll, GUILayout.MaxHeight(260f));
        if (eq != null)
        {
            EditorGUILayout.LabelField("Equipped", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(eq.Describe(), EditorStyles.wordWrappedMiniLabel);
            CombatStats stats = eq.Context.Stats;
            if (stats != null)
            {
                EditorGUILayout.LabelField("Combat Stats", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Defense {stats.Get(CombatStatType.Defense):0.#} (−{stats.PhysicalReduction:P0} physical)   Magic Resistance {stats.Get(CombatStatType.MagicResistance):0.#} (−{stats.MagicalReduction:P0} magical)", EditorStyles.wordWrappedMiniLabel);
                foreach (string line in stats.Describe())
                    EditorGUILayout.LabelField("  " + line, EditorStyles.miniLabel);
            }
        }
        if (manager.ArmorSetManager != null)
        {
            EditorGUILayout.LabelField("Armor Sets", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(manager.ArmorSetManager.GetSetStatusReport(), EditorStyles.wordWrappedMiniLabel);
        }
        WeaponController wc = manager.WeaponController;
        if (wc != null && wc.EquippedWeapon != null)
        {
            EditorGUILayout.LabelField("Weapon", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{wc.EquippedWeapon.Name}: {wc.AttackPhase}" + (wc.IsCharging ? $" (charge {wc.PendingChargeRatio:P0})" : "") +
                                       (wc.Combo != null && wc.Combo.ComboLength > 0 ? $"  combo {wc.Combo.GetComboString()}" : ""), EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ------------------------------------------------------------------ settings export / import
    private void DrawSettingsIo()
    {
        showSettingsIo = EditorGUILayout.Foldout(showSettingsIo, "Settings Export / Import", true, styles.SubHeaderStyle);
        if (!showSettingsIo)
            return;
        EditorGUILayout.BeginVertical(styles.BoxStyle);
        EditorGUILayout.HelpBox("Exports the component's settings, including object references (EditorJsonUtility), to reuse them on another inventory.", MessageType.None);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Export…"))
        {
            string path = EditorUtility.SaveFilePanel("Export Inventory Settings", Application.dataPath, "InventorySettings", "json");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, EditorJsonUtility.ToJson(manager, true));
                Debug.Log($"[Inventory] Settings exported to {path}", manager);
            }
        }
        if (GUILayout.Button("Import…"))
        {
            string path = EditorUtility.OpenFilePanel("Import Inventory Settings", Application.dataPath, "json");
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                Undo.RecordObject(manager, "Import Inventory Settings");
                EditorJsonUtility.FromJsonOverwrite(System.IO.File.ReadAllText(path), manager);
                EditorUtility.SetDirty(manager);
                serializedObject.Update();
                Revalidate();
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }
}
#endif
