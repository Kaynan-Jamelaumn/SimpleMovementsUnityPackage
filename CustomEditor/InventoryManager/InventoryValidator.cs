#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One problem found by the <see cref="InventoryValidator"/>, with an optional automatic fix.</summary>
public sealed class InventoryIssue
{
    public MessageType severity;
    public string message;
    public string fixLabel;
    public Action fix;

    public InventoryIssue(MessageType severity, string message, string fixLabel = null, Action fix = null)
    {
        this.severity = severity;
        this.message = message;
        this.fixLabel = fixLabel;
        this.fix = fix;
    }
}

/// <summary>
/// Checks an <see cref="InventoryManager"/> setup - references, prefabs, slot parents, equipment slots, event
/// routing, the player's components - and offers fixes (auto-assign, add a component, open the UI Builder). Used by
/// the Inventory Manager inspector and the Inventory UI Builder.
/// </summary>
public static class InventoryValidator
{
    public static List<InventoryIssue> Validate(InventoryManager manager)
    {
        var issues = new List<InventoryIssue>();
        if (manager == null)
            return issues;
        var so = new SerializedObject(manager);

        // ------------------------------------------------ duplicated UI (a build that ran twice)
        CheckDuplicateUI(manager, so, issues);

        // ------------------------------------------------ prefabs
        var itemPrefab = so.FindProperty("itemPrefab").objectReferenceValue as GameObject;
        if (itemPrefab == null)
            issues.Add(new InventoryIssue(MessageType.Error, "Item Prefab is missing: items cannot be created.", "Build UI…", () => InventoryUIBuilderWindow.Open(manager)));
        else
        {
            if (itemPrefab.GetComponent<InventoryItem>() == null)
                issues.Add(new InventoryIssue(MessageType.Error, $"Item Prefab '{itemPrefab.name}' has no InventoryItem component.", "Add it", () => AddToPrefab<InventoryItem>(itemPrefab)));
            if (itemPrefab.GetComponentInChildren<Image>(true) == null)
                issues.Add(new InventoryIssue(MessageType.Warning, $"Item Prefab '{itemPrefab.name}' has no Image: icons will not show."));
            if (itemPrefab.GetComponentInChildren<Text>(true) == null)
                issues.Add(new InventoryIssue(MessageType.Info, $"Item Prefab '{itemPrefab.name}' has no Text: stack counts will not show."));
        }

        SerializedProperty slotManager = so.FindProperty("slotManager");
        var slotPrefab = slotManager.FindPropertyRelative("slotPrefab").objectReferenceValue as GameObject;
        if (slotPrefab == null)
            issues.Add(new InventoryIssue(MessageType.Error, "Slot Manager ▸ Slot Prefab is missing: no slots are created.", "Build UI…", () => InventoryUIBuilderWindow.Open(manager)));
        else if (slotPrefab.GetComponent<Image>() == null)
            issues.Add(new InventoryIssue(MessageType.Warning, $"Slot Prefab '{slotPrefab.name}' has no Image: slots cannot be clicked (one is added at runtime)."));

        // ------------------------------------------------ parents & layouts
        SerializedProperty creation = slotManager.FindPropertyRelative("creationManager");
        var hotbarParent = creation.FindPropertyRelative("hotbarSlotsParent").objectReferenceValue as Transform;
        var inventoryParentSlots = creation.FindPropertyRelative("inventorySlotsParent").objectReferenceValue as Transform;
        var equipmentParent = creation.FindPropertyRelative("equipmentSlotsParent").objectReferenceValue as Transform;
        if (hotbarParent == null)
            issues.Add(new InventoryIssue(MessageType.Error, "Slot Manager ▸ Hotbar Slots Parent is missing.", "Build UI…", () => InventoryUIBuilderWindow.Open(manager)));
        if (inventoryParentSlots == null)
            issues.Add(new InventoryIssue(MessageType.Error, "Slot Manager ▸ Inventory Slots Parent is missing.", "Build UI…", () => InventoryUIBuilderWindow.Open(manager)));

        CheckGrid(issues, manager, slotManager, "hotbarGridLayout", hotbarParent, "Hotbar Grid Layout");
        CheckGrid(issues, manager, slotManager, "inventoryGridLayout", inventoryParentSlots, "Inventory Grid Layout");

        var inventoryPanel = so.FindProperty("inventoryParent").objectReferenceValue as GameObject;
        if (inventoryPanel == null)
        {
            GameObject guess = inventoryParentSlots != null ? inventoryParentSlots.parent != null ? inventoryParentSlots.parent.gameObject : inventoryParentSlots.gameObject : null;
            issues.Add(new InventoryIssue(MessageType.Error, "Inventory Parent (the panel opened with the inventory key) is missing.",
                guess != null ? $"Use '{guess.name}'" : "Build UI…",
                guess != null ? (Action)(() => SetRef(manager, "inventoryParent", guess)) : () => InventoryUIBuilderWindow.Open(manager)));
        }

        // ------------------------------------------------ equipment
        var equipmentPanel = so.FindProperty("equippableInventory").objectReferenceValue as GameObject;
        List<InventorySlot> equipmentSlots = CollectEquipmentSlots(equipmentParent, equipmentPanel);
        if (equipmentPanel == null && equipmentParent == null)
            issues.Add(new InventoryIssue(MessageType.Info, "No equipment panel: armor and trinkets can be carried but not worn.", "Build UI…", () => InventoryUIBuilderWindow.Open(manager)));
        else
        {
            if (equipmentParent == null && equipmentSlots.Count > 0)
            {
                Transform guess = CommonParent(equipmentSlots) ?? equipmentPanel.transform;
                issues.Add(new InventoryIssue(MessageType.Error, "Slot Manager ▸ Equipment Slots Parent is missing: worn items cannot be displayed in the equipment slots.",
                    $"Use '{guess.name}'", () => SetNestedRef(manager, "slotManager.creationManager.equipmentSlotsParent", guess)));
            }
            if (equipmentSlots.Count == 0)
                issues.Add(new InventoryIssue(MessageType.Warning, "The equipment panel has no equipment slots (InventorySlot with a Slot Type other than Common).", "Build UI…", () => InventoryUIBuilderWindow.Open(manager)));
            foreach (InventorySlot s in equipmentSlots)
                if (s.IsHotbarSlot)
                    issues.Add(new InventoryIssue(MessageType.Warning, $"Equipment slot '{s.name}' is marked as a hotbar slot.", "Fix", () => { Undo.RecordObject(s, "Fix slot"); s.SetAsHotbarSlot(false); EditorUtility.SetDirty(s); }));
        }

        // ------------------------------------------------ player
        var player = so.FindProperty("player").objectReferenceValue as GameObject;
        var status = so.FindProperty("playerStatusController").objectReferenceValue as PlayerStatusController;
        if (player == null)
        {
            PlayerStatusController found = UnityEngine.Object.FindAnyObjectByType<PlayerStatusController>();
            issues.Add(new InventoryIssue(MessageType.Error, "Player is not assigned.", found != null ? $"Use '{found.name}'" : null,
                found != null ? (Action)(() => AssignPlayer(manager, found.gameObject)) : null));
        }
        else
        {
            if (status == null)
            {
                var ps = player.GetComponentInChildren<PlayerStatusController>();
                issues.Add(new InventoryIssue(ps != null ? MessageType.Info : MessageType.Error,
                    ps != null ? "Player Status Controller is empty (found automatically at runtime)." : "The player has no PlayerStatusController.",
                    ps != null ? "Assign" : null, ps != null ? (Action)(() => SetRef(manager, "playerStatusController", ps)) : null));
            }
            if (so.FindProperty("weaponController").objectReferenceValue == null)
            {
                var wc = player.GetComponentInChildren<WeaponController>();
                issues.Add(new InventoryIssue(MessageType.Warning,
                    wc != null ? "Weapon Controller is empty (found automatically at runtime)." : "The player has no WeaponController: weapons in the hotbar cannot attack.",
                    wc != null ? "Assign" : "Add to player",
                    wc != null ? (Action)(() => SetRef(manager, "weaponController", wc)) : () => { var added = Undo.AddComponent<WeaponController>(player); SetRef(manager, "weaponController", added); }));
            }
            else
            {
                var wc = so.FindProperty("weaponController").objectReferenceValue as WeaponController;
                if (wc != null && wc.handGameObject == null)
                    issues.Add(new InventoryIssue(MessageType.Warning, "Weapon Controller ▸ Hand Game Object is empty: trails and the weapons' Attack Cast have no hand to follow."));
            }

            AddPlayerComponentIssue<EquipmentManager>(issues, player, "Equipment Manager", "applies worn items and the weapon's passive effects");
            AddPlayerComponentIssue<ArmorSetManager>(issues, player, "Armor Set Manager", "turns armor set bonuses on and off");
            AddPlayerComponentIssue<CombatStats>(issues, player, "Combat Stats", "Defense, resistances, critical chance, attack speed");
        }
        if (so.FindProperty("handParent").objectReferenceValue == null)
            issues.Add(new InventoryIssue(MessageType.Warning, "Hand Parent is empty: the item in the selected hotbar slot is not shown in the hand.",
                player != null && so.FindProperty("weaponController").objectReferenceValue is WeaponController w && w.handGameObject != null ? "Use the weapon hand" : null,
                player != null && so.FindProperty("weaponController").objectReferenceValue is WeaponController w2 && w2.handGameObject != null ? (Action)(() => SetRef(manager, "handParent", w2.handGameObject.transform)) : null));

        // ------------------------------------------------ events & UI
        Canvas canvas = FindCanvas(manager, hotbarParent, inventoryPanel);
        if (canvas == null)
            issues.Add(new InventoryIssue(MessageType.Error, "The inventory UI is not under a Canvas.", "Build UI…", () => InventoryUIBuilderWindow.Open(manager)));
        else
        {
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                issues.Add(new InventoryIssue(MessageType.Error, $"Canvas '{canvas.name}' has no Graphic Raycaster: slots cannot be clicked.", "Add it", () => Undo.AddComponent<GraphicRaycaster>(canvas.gameObject)));
            bool managerIsAncestor = hotbarParent != null && hotbarParent.IsChildOf(manager.transform);
            bool relay = canvas.GetComponentInChildren<InventoryPointerRelay>(true) != null;
            if (!managerIsAncestor && !relay)
                issues.Add(new InventoryIssue(MessageType.Error, "The Inventory Manager is not a parent of the UI, so it never receives clicks: drag and drop does not work.",
                    "Add Pointer Relay", () => { var r = Undo.AddComponent<InventoryPointerRelay>(canvas.gameObject); r.Target = manager; EditorUtility.SetDirty(r); }));
        }
        // In Prefab Mode the EventSystem belongs to the scene (never put one in the player prefab).
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(manager.gameObject) == null &&
            UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include).Length == 0)
            issues.Add(new InventoryIssue(MessageType.Error, "The scene has no EventSystem: UI cannot be clicked.", "Create", () => InventoryUIBuilder.EnsureEventSystem()));

        if (so.FindProperty("itemInfo").objectReferenceValue == null)
            issues.Add(new InventoryIssue(MessageType.Info, "No Item Info panel: right clicking an item shows nothing.", "Build UI…", () => InventoryUIBuilderWindow.Open(manager)));

        bool dropZone = inventoryPanel != null && (inventoryPanel.GetComponentInChildren<InventoryDropZone>(true) != null ||
                                                    inventoryPanel.GetComponentsInChildren<Transform>(true).Any(t => t.name == "DropItem"));
        if (inventoryPanel != null && !dropZone)
            issues.Add(new InventoryIssue(MessageType.Info, "No drop zone (an InventoryDropZone or an object named 'DropItem'): items cannot be dropped into the world.", "Build UI…", () => InventoryUIBuilderWindow.Open(manager)));

        var storage = so.FindProperty("storageParent").objectReferenceValue as GameObject;
        if (storage != null && storage.transform.childCount < 2)
            issues.Add(new InventoryIssue(MessageType.Error, $"Storage panel '{storage.name}' needs two children: 0 = background, 1 = slots."));

        return issues;
    }

    // ------------------------------------------------------------------ helpers
    private static void CheckGrid(List<InventoryIssue> issues, InventoryManager manager, SerializedProperty slotManager, string field, Transform parent, string label)
    {
        var grid = slotManager.FindPropertyRelative(field).objectReferenceValue as GridLayoutGroup;
        if (grid != null)
            return;
        GridLayoutGroup onParent = parent != null ? parent.GetComponent<GridLayoutGroup>() : null;
        issues.Add(new InventoryIssue(MessageType.Error, $"Slot Manager ▸ {label} is missing.",
            parent == null ? null : onParent != null ? "Assign" : "Add to parent",
            parent == null ? null : (Action)(() =>
            {
                GridLayoutGroup g = onParent != null ? onParent : Undo.AddComponent<GridLayoutGroup>(parent.gameObject);
                SetNestedRef(manager, "slotManager." + field, g);
            })));
    }

    private static void AddPlayerComponentIssue<T>(List<InventoryIssue> issues, GameObject player, string label, string what) where T : Component
    {
        if (player.GetComponentInChildren<T>(true) != null)
            return;
        GameObject host = player.GetComponentInChildren<PlayerStatusController>() is PlayerStatusController ps ? ps.gameObject : player;
        issues.Add(new InventoryIssue(MessageType.Info, $"The player has no {label} ({what}); it is added automatically at runtime.", "Add now", () => Undo.AddComponent<T>(host)));
    }

    /// <summary>
    /// More than one generated "Inventory UI" under the player (the builder ran on the scene instance and again in the
    /// prefab): extra hotbars and extra status bars show on screen. Offers to remove the ones the inventory does not use.
    /// </summary>
    private static void CheckDuplicateUI(InventoryManager manager, SerializedObject so, List<InventoryIssue> issues)
    {
        GameObject player = so.FindProperty("player").objectReferenceValue as GameObject;
        Transform scope = player != null ? player.transform : manager.transform.root;
        List<Transform> uiRoots = scope.GetComponentsInChildren<Transform>(true).Where(t => t.name == InventoryUIBuilder.RootName).ToList();
        if (uiRoots.Count < 2)
            return;
        var hotbar = so.FindProperty("slotManager.creationManager.hotbarSlotsParent").objectReferenceValue as Transform;
        Transform keep = uiRoots.FirstOrDefault(r => manager.transform.IsChildOf(r))
                         ?? uiRoots.FirstOrDefault(r => hotbar != null && hotbar.IsChildOf(r))
                         ?? uiRoots[0];
        issues.Add(new InventoryIssue(MessageType.Warning,
            $"'{scope.name}' has {uiRoots.Count} inventory UIs (the UI was built more than once): extra hotbars and duplicated status bars are shown.",
            "Remove extras", () => RemoveExtraUI(manager, uiRoots, keep)));
    }

    private static void RemoveExtraUI(InventoryManager manager, List<Transform> uiRoots, Transform keep)
    {
        int removed = 0;
        var blocked = new List<string>();
        foreach (Transform r in uiRoots)
        {
            if (r == null || r == keep)
                continue;
            // A part of a prefab instance (not an override added to it) can only be removed in the prefab itself.
            if (PrefabUtility.IsPartOfPrefabInstance(r.gameObject) && !PrefabUtility.IsAddedGameObjectOverride(r.gameObject))
            {
                blocked.Add(r.name + " (in the prefab)");
                continue;
            }
            GameObject target = r.gameObject;
            // Remove the canvas too when it holds nothing but this UI.
            Canvas canvas = r.parent != null ? r.parent.GetComponent<Canvas>() : null;
            if (canvas != null && canvas.transform.childCount == 1 && canvas.GetComponentInChildren<InventoryManager>(true) == null)
                target = canvas.gameObject;
            Undo.DestroyObjectImmediate(target);
            removed++;
        }
        // Status managers that pointed at a removed bar get their bar back in the kept UI.
        InventoryAutoSetup.TryBuild(manager.gameObject, force: true);
        Debug.Log($"[Inventory] Removed {removed} extra inventory UI(s)." +
                  (blocked.Count > 0 ? $" Open the prefab to remove the others: {string.Join(", ", blocked)}." : ""), manager);
    }

    public static List<InventorySlot> CollectEquipmentSlots(Transform equipmentParent, GameObject equipmentPanel)
    {
        var list = new List<InventorySlot>();
        foreach (Transform root in new[] { equipmentParent, equipmentPanel != null ? equipmentPanel.transform : null })
        {
            if (root == null) continue;
            foreach (InventorySlot s in root.GetComponentsInChildren<InventorySlot>(true))
                if (s.SlotType != SlotType.Common && !list.Contains(s))
                    list.Add(s);
        }
        return list;
    }

    private static Transform CommonParent(List<InventorySlot> slots)
    {
        if (slots.Count == 0) return null;
        Transform p = slots[0].transform.parent;
        return slots.All(s => s.transform.parent == p) ? p : null;
    }

    public static Canvas FindCanvas(InventoryManager manager, Transform hotbarParent, GameObject inventoryPanel)
    {
        if (hotbarParent != null && hotbarParent.GetComponentInParent<Canvas>() is Canvas c1) return c1.rootCanvas;
        if (inventoryPanel != null && inventoryPanel.GetComponentInParent<Canvas>() is Canvas c2) return c2.rootCanvas;
        Canvas c3 = manager.GetComponentInParent<Canvas>();
        return c3 != null ? c3.rootCanvas : null;
    }

    public static void SetRef(InventoryManager manager, string property, UnityEngine.Object value)
    {
        var so = new SerializedObject(manager);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    public static void SetNestedRef(InventoryManager manager, string path, UnityEngine.Object value)
    {
        var so = new SerializedObject(manager);
        SerializedProperty p = so.FindProperty(path);
        if (p == null)
        {
            Debug.LogError($"[Inventory] Property '{path}' not found.", manager);
            return;
        }
        p.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    public static void AssignPlayer(InventoryManager manager, GameObject player)
    {
        var so = new SerializedObject(manager);
        so.FindProperty("player").objectReferenceValue = player;
        var ps = player.GetComponentInChildren<PlayerStatusController>();
        if (ps != null) so.FindProperty("playerStatusController").objectReferenceValue = ps;
        var wc = player.GetComponentInChildren<WeaponController>();
        if (wc != null) so.FindProperty("weaponController").objectReferenceValue = wc;
        var asm = player.GetComponentInChildren<ArmorSetManager>();
        if (asm != null) so.FindProperty("armorSetManager").objectReferenceValue = asm;
        if (so.FindProperty("handParent").objectReferenceValue == null && wc != null && wc.handGameObject != null)
            so.FindProperty("handParent").objectReferenceValue = wc.handGameObject.transform;
        if (so.FindProperty("cam").objectReferenceValue == null)
        {
            // A camera of the player itself, or the scene's main camera - never a scene camera from Prefab Mode
            // (a prefab cannot reference scene objects).
            Camera cam = player.GetComponentInChildren<Camera>(true);
            if (cam == null && Camera.main != null && Camera.main.gameObject.scene == manager.gameObject.scene)
                cam = Camera.main;
            if (cam != null)
                so.FindProperty("cam").objectReferenceValue = cam;
        }
        so.ApplyModifiedProperties();
    }

    /// <summary>Fills every empty reference that can be found without guessing (player parts, camera, grids).</summary>
    public static int AutoAssign(InventoryManager manager)
    {
        int fixedCount = 0;
        foreach (InventoryIssue issue in Validate(manager))
        {
            if (issue.fix == null || issue.fixLabel == null || issue.fixLabel.EndsWith("…") || issue.fixLabel.StartsWith("Add now") || issue.fixLabel == "Create")
                continue;
            issue.fix();
            fixedCount++;
        }
        return fixedCount;
    }

    private static void AddToPrefab<T>(GameObject prefab) where T : Component
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path))
        {
            Undo.AddComponent<T>(prefab);
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root.GetComponent<T>() == null) root.AddComponent<T>();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
#endif
