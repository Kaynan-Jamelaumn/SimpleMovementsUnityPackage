#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Options of the Inventory UI Builder.</summary>
[System.Serializable]
public class InventoryUIBuildSettings
{
    public float slotSize = 72f;
    public float spacing = 6f;
    public int inventoryColumns = 6;
    public int hotbarSlots = 5;
    public int inventorySlots = 24;
    public bool buildEquipment = true;
    public List<SlotType> equipmentSlots = new List<SlotType>
    {
        SlotType.Helmet, SlotType.Amulet, SlotType.Shoulders, SlotType.Cloak, SlotType.Armor, SlotType.Wrist,
        SlotType.Gloves, SlotType.Belt, SlotType.Leggings, SlotType.Ring, SlotType.Boots, SlotType.Ring,
        SlotType.Shield, SlotType.Trinket,
    };
    public bool buildStorage = true;
    public int storageSlots = 16;
    public bool buildItemInfo = true;
    public bool buildDropZone = true;
    [Tooltip("One bar per status manager of the player (health, stamina, mana, hunger...), assigned to it.")]
    public bool buildStatusBars = true;
    [Tooltip("The Player's interaction prompt (progress circle and text) when it is not assigned.")]
    public bool buildInteractionPrompt = true;
    [Tooltip("The Armor Sets window (worn sets, set details, pieces, bonuses) wired to an ArmorSetUIManager, and a Sets button on the inventory.")]
    public bool buildArmorSetUI = true;
    [Tooltip("Lay the inventory out as the Inventory Manager's grid (Use Grid Inventory): Grid Columns × Grid Rows cells. " +
             "Taken from the manager; the hotbar, equipment and storage stay slots.")]
    public bool gridInventory;
    public int gridColumns = 10;
    public int gridRows = 6;
    [Tooltip("0 = the Slot Size.")]
    public float gridCellSize = 56f;
    public float gridSpacing = 2f;
    public float gridPadding = 8f;
    public string prefabFolder = "Assets/Inventory/Generated";
    public Color panelColor = new Color(0.08f, 0.09f, 0.11f, 0.92f);
    public Color slotColor = new Color(1f, 1f, 1f, 0.12f);
    public Color equipmentSlotColor = new Color(0.55f, 0.75f, 1f, 0.18f);
    public Color textColor = new Color(0.92f, 0.92f, 0.92f, 1f);

    /// <summary>Default settings that keep the slot counts already configured on <paramref name="manager"/>.</summary>
    public static InventoryUIBuildSettings FromManager(InventoryManager manager)
    {
        var s = new InventoryUIBuildSettings();
        if (manager != null)
        {
            var so = new SerializedObject(manager);
            s.hotbarSlots = Mathf.Clamp(so.FindProperty("numberOfHotBarSlots").intValue, 1, 9);
            s.inventorySlots = Mathf.Max(1, so.FindProperty("numberOfInventorySlots").intValue);
            s.ReadGrid(so);
        }
        return s;
    }

    /// <summary>Takes the grid options (Use Grid Inventory, Grid Settings) of the manager.</summary>
    public void ReadGrid(SerializedObject managerObject)
    {
        SerializedProperty use = managerObject.FindProperty("useGridInventory");
        SerializedProperty g = managerObject.FindProperty("gridSettings");
        if (use == null || g == null)
            return;
        gridInventory = use.boolValue;
        gridColumns = Mathf.Clamp(g.FindPropertyRelative("columns").intValue, 2, 30);
        gridRows = Mathf.Clamp(g.FindPropertyRelative("rows").intValue, 2, 30);
        gridCellSize = Mathf.Max(0f, g.FindPropertyRelative("cellSize").floatValue);
        gridSpacing = Mathf.Max(0f, g.FindPropertyRelative("spacing").floatValue);
        gridPadding = Mathf.Max(0f, g.FindPropertyRelative("padding").floatValue);
    }

    /// <summary>Cells across (grid) or columns of slots.</summary>
    public int InventoryColumns => gridInventory ? gridColumns : inventoryColumns;
    /// <summary>Cells (grid) or slots of the inventory.</summary>
    public int InventoryCells => gridInventory ? gridColumns * gridRows : inventorySlots;
    public float InventoryCellSize => gridInventory && gridCellSize > 0f ? gridCellSize : slotSize;
    public float InventorySpacing => gridInventory ? gridSpacing : spacing;
    public float InventoryPadding => gridInventory ? gridPadding : spacing;
}

/// <summary>
/// Builds (or repairs) the player's UI: canvas, hotbar, inventory grid, equipment panel with typed and labelled slots,
/// storage panel, item info tooltip, drop zone, status bars (one per status manager of the player, assigned to it),
/// the interaction prompt of the Player component, the slot and item prefabs - and wires every reference of the
/// <see cref="InventoryManager"/>. Existing, already assigned parts are kept; only what is missing is created.
/// Works on scene objects and inside Prefab Mode (the canvas is placed inside the player so the prefab carries its
/// own UI). Everything is undoable. <see cref="InventoryAutoSetup"/> runs it automatically when something is missing.
/// </summary>
public static class InventoryUIBuilder
{
    public const string RootName = "Inventory UI";

    // Set while building inside a prefab asset loaded with PrefabUtility.LoadPrefabContents (no Undo there).
    private static GameObject isolatedRoot;
    private static bool noUndo;

    private static void Created(Object o, string label) { if (!noUndo) Undo.RegisterCreatedObjectUndo(o, label); }
    private static T AddComp<T>(GameObject go) where T : Component => noUndo ? go.AddComponent<T>() : Undo.AddComponent<T>(go);
    private static void Record(Object o, string label) { if (!noUndo) Undo.RecordObject(o, label); }
    private static void DestroyObj(Object o) { if (noUndo) Object.DestroyImmediate(o); else Undo.DestroyObjectImmediate(o); }

    /// <summary>
    /// Builds what is missing directly in a prefab asset (the player prefab of a scene instance), so the UI lives in
    /// the prefab once instead of being added to every instance as overrides. Returns true when the asset was changed.
    /// </summary>
    public static bool BuildInPrefabAsset(string assetPath)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(assetPath);
        try
        {
            isolatedRoot = contents;
            noUndo = true;
            InventoryManager manager = contents.GetComponentInChildren<InventoryManager>(true);
            GameObject player = ResolvePlayer(manager, null);
            if (player == null && manager == null)
                return false;
            if (MissingParts(manager, player).Count == 0)
                return false;
            Build(manager, player, InventoryUIBuildSettings.FromManager(manager), select: false);
            PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
            return true;
        }
        finally
        {
            isolatedRoot = null;
            noUndo = false;
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    /// <summary>Builds what is missing for <paramref name="manager"/> (creates a manager when null). Returns the manager.</summary>
    public static InventoryManager Build(InventoryManager manager, GameObject player, InventoryUIBuildSettings s, bool select = true)
    {
        int group = 0;
        if (!noUndo)
        {
            Undo.IncrementCurrentGroup();
            group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Inventory UI");
        }

        player = ResolvePlayer(manager, player);
        GameObject stageRoot = StageRoot(manager != null ? manager.gameObject : player);

        // An EventSystem belongs to the scene, never to a prefab (a player prefab with its own EventSystem gives
        // "There are 2 event systems" as soon as the scene has one too).
        if (stageRoot == null)
            EnsureEventSystem();
        Canvas canvas = FindOrCreateCanvas(manager, player, stageRoot);
        Transform root = FindOrCreateChild(canvas.transform, RootName, stretch: true);

        if (manager == null)
        {
            manager = root.GetComponent<InventoryManager>();
            if (manager == null)
                manager = AddComp<InventoryManager>(root.gameObject);
        }

        // Prefabs and sprites
        GameObject slotPrefab = GetOrCreateSlotPrefab(s);
        GameObject itemPrefab = GetOrCreateItemPrefab(s);

        var so = new SerializedObject(manager);
        SerializedProperty slotManager = so.FindProperty("slotManager");
        SerializedProperty creation = slotManager.FindPropertyRelative("creationManager");

        // Hotbar
        Transform hotbarPanel = FindOrCreateChild(root, "Hotbar", false);
        Panel(hotbarPanel, s.panelColor, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 16f), new Vector2(s.hotbarSlots * (s.slotSize + s.spacing) + s.spacing * 3f, s.slotSize + s.spacing * 4f));
        Transform hotbarSlotsParent = ExistingOr(creation.FindPropertyRelative("hotbarSlotsParent"), () => FindOrCreateChild(hotbarPanel, "HotbarSlots", true));
        GridLayoutGroup hotbarGrid = Grid(hotbarSlotsParent, s, s.hotbarSlots);

        // Inventory panel
        GameObject inventoryPanelGo = so.FindProperty("inventoryParent").objectReferenceValue as GameObject;
        Transform inventoryPanel = inventoryPanelGo != null ? inventoryPanelGo.transform : FindOrCreateChild(root, "InventoryPanel", false);
        // Grid inventory (Use Grid Inventory on the manager): Grid Columns × Grid Rows cells of the grid's size; else the slots.
        s.ReadGrid(so);
        int columns = Mathf.Max(1, s.InventoryColumns);
        int rows = Mathf.CeilToInt(s.InventoryCells / (float)columns);
        float cellSize = s.InventoryCellSize, cellSpacing = s.InventorySpacing, cellPadding = s.InventoryPadding;
        Vector2 gridSize = new Vector2(columns * cellSize + (columns - 1) * cellSpacing + cellPadding * 2f,
                                       rows * cellSize + (rows - 1) * cellSpacing + cellPadding * 2f);
        if (inventoryPanelGo != null && s.gridInventory)
        {
            // An existing panel takes the grid's size (the grid resizes it the same way when the game starts).
            var prt = inventoryPanel as RectTransform;
            if (prt != null && Mathf.Approximately(prt.anchorMin.x, prt.anchorMax.x) && Mathf.Approximately(prt.anchorMin.y, prt.anchorMax.y))
            {
                Record(prt, "Fit inventory panel to the grid");
                prt.sizeDelta = gridSize + new Vector2(24f, 64f);
            }
        }
        if (inventoryPanelGo == null)
        {
            // The panel itself has no image: its Background child is drawn above the drop zone, so releasing an item
            // on the panel (between slots) returns it instead of dropping it into the world.
            Panel(inventoryPanel, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 20f), gridSize + new Vector2(24f, 64f), withImage: false);
            Transform bg = FindOrCreateChild(inventoryPanel, "Background", true);
            GetOrAdd<Image>(bg.gameObject).color = s.panelColor;
            GetOrAdd<LayoutElement>(bg.gameObject).ignoreLayout = true;
            bg.SetAsFirstSibling();
        }
        Label(inventoryPanel, "Title", "Inventory", s, 22, new Vector2(0f, -8f), TextAlignmentOptions.Center, top: true);
        Transform inventorySlotsParent = ExistingOr(creation.FindPropertyRelative("inventorySlotsParent"), () =>
        {
            Transform t = FindOrCreateChild(inventoryPanel, "InventorySlots", false);
            var rt = (RectTransform)t;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(12f, 12f); rt.offsetMax = new Vector2(-12f, -52f);
            return t;
        });
        GridLayoutGroup inventoryGrid = Grid(inventorySlotsParent, cellSize, cellSpacing, cellPadding, columns);
        if (s.gridInventory)
            inventoryGrid.childAlignment = TextAnchor.UpperLeft; // as the grid places its cells (from the top-left)

        // Preview slots, so the hotbar and the grid are visible while editing. At runtime the inventory replaces them
        // with the configured number of slots (Number Of Hot Bar Slots / Number Of Inventory Slots, or the grid's cells).
        PreviewSlots(hotbarSlotsParent, slotPrefab, s.hotbarSlots, "HotbarSlot_", true);
        PreviewSlots(inventorySlotsParent, slotPrefab, s.InventoryCells, "InventorySlot_", false);

        // Drop zone (behind the panel content, larger than the screen)
        if (s.buildDropZone && inventoryPanel.GetComponentInChildren<InventoryDropZone>(true) == null && inventoryPanel.Find("DropItem") == null)
        {
            Transform drop = FindOrCreateChild(inventoryPanel, "DropItem", false);
            drop.SetAsFirstSibling(); // behind the Background and the slots
            var rt = (RectTransform)drop;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(8000f, 8000f);
            var img = GetOrAdd<Image>(drop.gameObject);
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;
            GetOrAdd<InventoryDropZone>(drop.gameObject);
            GetOrAdd<LayoutElement>(drop.gameObject).ignoreLayout = true;
        }

        // Equipment panel
        Transform equipmentPanel = null;
        Transform equipmentSlotsParent = creation.FindPropertyRelative("equipmentSlotsParent").objectReferenceValue as Transform;
        GameObject equipmentPanelGo = so.FindProperty("equippableInventory").objectReferenceValue as GameObject;
        if (s.buildEquipment)
        {
            equipmentPanel = equipmentPanelGo != null ? equipmentPanelGo.transform : FindOrCreateChild(root, "EquipmentPanel", false);
            int eqRows = Mathf.CeilToInt(s.equipmentSlots.Count / 2f);
            if (equipmentPanelGo == null)
                Panel(equipmentPanel, s.panelColor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 20f),
                    new Vector2(2f * (s.slotSize + 110f) + 36f, eqRows * (s.slotSize + s.spacing) + 76f));
            Label(equipmentPanel, "Title", "Equipment", s, 22, new Vector2(0f, -8f), TextAlignmentOptions.Center, top: true);
            if (equipmentSlotsParent == null)
            {
                equipmentSlotsParent = FindOrCreateChild(equipmentPanel, "EquipmentSlots", false);
                var rt = (RectTransform)equipmentSlotsParent;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(12f, 12f); rt.offsetMax = new Vector2(-12f, -52f);
            }
            var eqGrid = GetOrAdd<GridLayoutGroup>(equipmentSlotsParent.gameObject);
            eqGrid.cellSize = new Vector2(s.slotSize + 110f, s.slotSize);
            eqGrid.spacing = new Vector2(s.spacing, s.spacing);
            eqGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            eqGrid.constraintCount = 2;
            eqGrid.childAlignment = TextAnchor.UpperCenter;
            BuildEquipmentSlots(equipmentSlotsParent, slotPrefab, s);
        }

        // Storage panel (child 0 = background, child 1 = slots: the layout StorageManager expects)
        GameObject storageGo = so.FindProperty("storageParent").objectReferenceValue as GameObject;
        if (s.buildStorage && storageGo == null)
        {
            Transform storage = FindOrCreateChild(root, "StoragePanel", false);
            var rt = (RectTransform)storage;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            Transform bg = FindOrCreateChild(storage, "Background", false);
            bg.SetSiblingIndex(0);
            Panel(bg, s.panelColor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-615f, 130f), new Vector2(4 * (s.slotSize + s.spacing) + 24f, 4 * (s.slotSize + s.spacing) + 24f));
            Transform slots = FindOrCreateChild(storage, "Slots", false);
            slots.SetSiblingIndex(1);
            var srt = (RectTransform)slots;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(-615f, 130f);
            srt.sizeDelta = new Vector2(4 * (s.slotSize + s.spacing) + s.spacing, 4 * (s.slotSize + s.spacing) + s.spacing);
            Grid(slots, s, 4);
            for (int i = slots.childCount; i < s.storageSlots; i++)
            {
                GameObject slot = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, slots);
                slot.name = $"StorageSlot_{i}";
                Created(slot, "Create storage slot");
            }
            storage.gameObject.SetActive(false);
            storageGo = storage.gameObject;
        }

        // Item info tooltip
        ItemInfo info = so.FindProperty("itemInfo").objectReferenceValue as ItemInfo;
        if (s.buildItemInfo && info == null)
            info = BuildItemInfo(root, manager, s);

        // Player HUD: status bars and the interaction prompt
        if (player != null)
        {
            if (s.buildStatusBars)
                BuildStatusBars(root, player, s);
            if (s.buildInteractionPrompt)
                BuildInteractionPrompt(root, player, manager, s);
        }
        if (s.buildArmorSetUI)
        {
            ArmorSetUIManager armorUI = BuildArmorSetUI(so.FindProperty("armorSetUIManager").objectReferenceValue as ArmorSetUIManager,
                root, manager, player, inventoryPanel, s);
            so.FindProperty("armorSetUIManager").objectReferenceValue = armorUI;
        }

        // ---------------------------------------------------------------- wire the manager
        so.FindProperty("itemPrefab").objectReferenceValue = itemPrefab;
        so.FindProperty("inventoryParent").objectReferenceValue = inventoryPanel.gameObject;
        if (equipmentPanel != null) so.FindProperty("equippableInventory").objectReferenceValue = equipmentPanel.gameObject;
        if (storageGo != null) so.FindProperty("storageParent").objectReferenceValue = storageGo;
        if (info != null) so.FindProperty("itemInfo").objectReferenceValue = info;
        so.FindProperty("numberOfHotBarSlots").intValue = s.hotbarSlots;
        so.FindProperty("numberOfInventorySlots").intValue = s.inventorySlots;
        slotManager.FindPropertyRelative("slotPrefab").objectReferenceValue = slotPrefab;
        slotManager.FindPropertyRelative("hotbarGridLayout").objectReferenceValue = hotbarGrid;
        slotManager.FindPropertyRelative("inventoryGridLayout").objectReferenceValue = inventoryGrid;
        creation.FindPropertyRelative("hotbarSlotsParent").objectReferenceValue = hotbarSlotsParent;
        creation.FindPropertyRelative("inventorySlotsParent").objectReferenceValue = inventorySlotsParent;
        if (equipmentSlotsParent != null) creation.FindPropertyRelative("equipmentSlotsParent").objectReferenceValue = equipmentSlotsParent;
        so.ApplyModifiedProperties();
        if (player != null)
            InventoryValidator.AssignPlayer(manager, player);

        // Clicks reach the manager even when it is not a parent of the UI.
        var relay = GetOrAdd<InventoryPointerRelay>(canvas.gameObject);
        relay.Target = manager;
        EditorUtility.SetDirty(relay);

        // Drawing order: panels below, then the hotbar, the HUD and the tooltip on top (the inventory's full-screen drop
        // zone must never cover the hotbar).
        hotbarPanel.SetAsLastSibling();
        foreach (string hud in new[] { "StatusBars", "InteractionPrompt", "ItemInfo" })
        {
            Transform t = root.Find(hud);
            if (t != null) t.SetAsLastSibling();
        }

        // The panels start closed (the inventory key opens them); the hotbar and the status bars stay visible.
        inventoryPanel.gameObject.SetActive(false);
        if (equipmentPanel != null) equipmentPanel.gameObject.SetActive(false);

        EditorUtility.SetDirty(manager);
        if (stageRoot != null && isolatedRoot == null)
            EditorSceneManager.MarkSceneDirty(stageRoot.scene);
        if (!noUndo)
            Undo.CollapseUndoOperations(group);
        if (select)
            Selection.activeGameObject = manager.gameObject;
        Debug.Log($"[Inventory UI Builder] Built the inventory UI under '{canvas.name}/{RootName}' and wired '{manager.name}'" +
                  (stageRoot != null ? " (in Prefab Mode: save the prefab to keep it)." : "."), manager);
        return manager;
    }

    /// <summary>What is still missing for this inventory (empty = nothing to build).</summary>
    public static List<string> MissingParts(InventoryManager manager, GameObject player)
    {
        var missing = new List<string>();
        if (manager == null)
        {
            missing.Add("inventory manager");
        }
        else
        {
            var so = new SerializedObject(manager);
            SerializedProperty sm = so.FindProperty("slotManager");
            SerializedProperty cm = sm.FindPropertyRelative("creationManager");
            if (so.FindProperty("itemPrefab").objectReferenceValue == null) missing.Add("item prefab");
            if (sm.FindPropertyRelative("slotPrefab").objectReferenceValue == null) missing.Add("slot prefab");
            if (cm.FindPropertyRelative("hotbarSlotsParent").objectReferenceValue == null) missing.Add("hotbar");
            if (cm.FindPropertyRelative("inventorySlotsParent").objectReferenceValue == null) missing.Add("inventory slots");
            if (so.FindProperty("inventoryParent").objectReferenceValue == null) missing.Add("inventory panel");
            if (cm.FindPropertyRelative("equipmentSlotsParent").objectReferenceValue == null && so.FindProperty("equippableInventory").objectReferenceValue == null)
                missing.Add("equipment slots");
            if (so.FindProperty("itemInfo").objectReferenceValue == null) missing.Add("item info");
            if (!(so.FindProperty("armorSetUIManager").objectReferenceValue is ArmorSetUIManager armorUI) || ArmorSetUIMissing(armorUI))
                missing.Add("armor set UI");
            if (player == null) player = so.FindProperty("player").objectReferenceValue as GameObject;
        }
        if (player != null)
        {
            foreach (StatusManager status in DistinctStatuses(player))
            {
                if (new SerializedObject(status).FindProperty("uiImage")?.objectReferenceValue == null)
                {
                    missing.Add("status bars");
                    break;
                }
            }
            Player p = FindPlayerComponent(player);
            if (p != null)
            {
                var pso = new SerializedObject(p);
                if (pso.FindProperty("interactionUI").objectReferenceValue == null || pso.FindProperty("fillingCircle").objectReferenceValue == null)
                    missing.Add("interaction prompt");
            }
        }
        return missing;
    }

    /// <summary>The player of the inventory: the given one, the manager's, or the one in the same hierarchy / stage / scene.</summary>
    public static GameObject ResolvePlayer(InventoryManager manager, GameObject player)
    {
        if (player != null)
            return player;
        if (manager != null)
        {
            player = new SerializedObject(manager).FindProperty("player").objectReferenceValue as GameObject;
            if (player != null)
                return player;
            PlayerStatusController up = manager.GetComponentInParent<PlayerStatusController>(true);
            if (up != null)
                return up.gameObject;
            PlayerStatusController down = manager.transform.root.GetComponentInChildren<PlayerStatusController>(true);
            if (down != null)
                return down.gameObject;
        }
        if (isolatedRoot != null)
        {
            PlayerStatusController inAsset = isolatedRoot.GetComponentInChildren<PlayerStatusController>(true);
            return inAsset != null ? inAsset.gameObject : null;
        }
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && (manager == null || StageRoot(manager.gameObject) == stage.prefabContentsRoot))
        {
            PlayerStatusController inStage = stage.prefabContentsRoot.GetComponentInChildren<PlayerStatusController>(true);
            return inStage != null ? inStage.gameObject : null;
        }
        PlayerStatusController ps = Object.FindAnyObjectByType<PlayerStatusController>();
        return ps != null ? ps.gameObject : null;
    }

    /// <summary>The root of the prefab being edited in Prefab Mode that contains <paramref name="go"/> (null outside Prefab Mode).</summary>
    public static GameObject StageRoot(GameObject go)
    {
        if (go == null)
            return null;
        if (isolatedRoot != null && go.transform.IsChildOf(isolatedRoot.transform))
            return isolatedRoot;
        var stage = PrefabStageUtility.GetPrefabStage(go);
        return stage != null ? stage.prefabContentsRoot : null;
    }

    private static Player FindPlayerComponent(GameObject player)
    {
        Player p = player.GetComponentInChildren<Player>(true);
        return p != null ? p : player.GetComponentInParent<Player>(true);
    }

    // ------------------------------------------------------------------ player HUD
    private static readonly string[] StatusOrder = { "Health", "Mana", "Stamina", "Hunger", "Thirst", "Sleep", "Sanity", "BodyHeat", "Oxygen", "Weight" };

    private static Color StatusColor(string key)
    {
        switch (key)
        {
            case "Health": return new Color(0.85f, 0.2f, 0.22f);
            case "Mana": return new Color(0.25f, 0.45f, 0.95f);
            case "Stamina": return new Color(0.3f, 0.8f, 0.35f);
            case "Hunger": return new Color(0.9f, 0.6f, 0.2f);
            case "Thirst": return new Color(0.25f, 0.75f, 0.9f);
            case "Sleep": return new Color(0.6f, 0.5f, 0.9f);
            case "Sanity": return new Color(0.75f, 0.4f, 0.8f);
            case "BodyHeat": return new Color(0.95f, 0.4f, 0.2f);
            case "Oxygen": return new Color(0.6f, 0.9f, 1f);
            case "Weight": return new Color(0.75f, 0.7f, 0.6f);
            default: return new Color(0.8f, 0.8f, 0.8f);
        }
    }

    /// <summary>
    /// The player's status managers, one per type (health, stamina...). A second component of the same type is a
    /// setup mistake (it would get a second bar): it is reported and skipped.
    /// </summary>
    public static List<StatusManager> DistinctStatuses(GameObject player)
    {
        var result = new List<StatusManager>();
        var seen = new HashSet<System.Type>();
        foreach (StatusManager st in player.GetComponentsInChildren<StatusManager>(true))
        {
            if (st == null || new SerializedObject(st).FindProperty("uiImage") == null)
                continue;
            if (seen.Add(st.GetType()))
                result.Add(st);
            else
                Debug.LogWarning($"[Inventory UI Builder] '{player.name}' has more than one {st.GetType().Name} (another one is on '{st.gameObject.name}'). " +
                                 "Only the first gets a status bar - remove the extra component.", st);
        }
        return result;
    }

    /// <summary>
    /// One bar per status of the player (top left), each assigned as that manager's UI Image. Bars that already exist
    /// are reused (never duplicated), and duplicate bars left by earlier builds are removed.
    /// </summary>
    private static void BuildStatusBars(Transform root, GameObject player, InventoryUIBuildSettings s)
    {
        List<StatusManager> statuses = DistinctStatuses(player);
        if (statuses.Count == 0)
            return;
        statuses.Sort((a, b) => Order(a).CompareTo(Order(b)));

        Transform panel = FindOrCreateChild(root, "StatusBars", false);
        var prt = (RectTransform)panel;
        bool fresh = panel.GetComponent<StatusBarsUI>() == null;
        if (fresh)
        {
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(20f, -20f);
            prt.sizeDelta = new Vector2(340f, 0f);
            var layout = GetOrAdd<VerticalLayoutGroup>(panel.gameObject);
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = GetOrAdd<ContentSizeFitter>(panel.gameObject);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        var bars = GetOrAdd<StatusBarsUI>(panel.gameObject);
        Record(bars, "Add status bars");
        Sprite white = GetOrCreateSprite(s, "UIWhite", false);

        RemoveDuplicateBars(panel, statuses);

        foreach (StatusManager status in statuses)
        {
            var sso = new SerializedObject(status);
            SerializedProperty uiImage = sso.FindProperty("uiImage");
            string key = Key(status);
            Transform existingRow = panel.Find(key + "Bar");
            Image assigned = uiImage.objectReferenceValue as Image;

            if (assigned != null)
            {
                // Already has a bar: only make sure the value text / visibility row is registered when it is ours.
                if (existingRow != null && assigned.transform.IsChildOf(existingRow) && !bars.HasRow(status))
                    bars.AddRow(status, assigned, existingRow.GetComponentInChildren<TMP_Text>(true) is TMP_Text t0 && t0.name == "Value" ? t0 : FindValue(existingRow), existingRow.gameObject);
                continue;
            }

            if (existingRow != null)
            {
                // A bar from an earlier build whose link was lost: reuse it instead of adding another one.
                Image oldFill = existingRow.Find("Background/Fill")?.GetComponent<Image>();
                if (oldFill != null)
                {
                    uiImage.objectReferenceValue = oldFill;
                    sso.ApplyModifiedProperties();
                    bars.AddRow(status, oldFill, FindValue(existingRow), existingRow.gameObject);
                    continue;
                }
                DestroyObj(existingRow.gameObject);
            }

            var row = new GameObject(key + "Bar", typeof(RectTransform));
            Created(row, "Create status bar");
            row.transform.SetParent(panel, false);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 24f;

            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(row.transform, false);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(90f, 0f);
            var ltmp = label.AddComponent<TextMeshProUGUI>();
            ltmp.text = ObjectNames.NicifyVariableName(key);
            ltmp.fontSize = 15f;
            ltmp.alignment = TextAlignmentOptions.MidlineLeft;
            ltmp.color = s.textColor;
            ltmp.raycastTarget = false;

            var back = new GameObject("Background", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(row.transform, false);
            var brt = (RectTransform)back.transform;
            brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 1f);
            brt.offsetMin = new Vector2(94f, 2f); brt.offsetMax = new Vector2(0f, -2f);
            var bimg = back.GetComponent<Image>();
            bimg.sprite = white;
            bimg.color = new Color(0f, 0f, 0f, 0.55f);
            bimg.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(back.transform, false);
            var frt = (RectTransform)fillGo.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);
            var fill = fillGo.GetComponent<Image>();
            fill.sprite = white;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.color = StatusColor(key);
            fill.raycastTarget = false;

            var valueGo = new GameObject("Value", typeof(RectTransform));
            valueGo.transform.SetParent(back.transform, false);
            var vrt = (RectTransform)valueGo.transform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(6f, 0f); vrt.offsetMax = new Vector2(-6f, 0f);
            var vtmp = valueGo.AddComponent<TextMeshProUGUI>();
            vtmp.text = "100 / 100";
            vtmp.fontSize = 13f;
            vtmp.alignment = TextAlignmentOptions.MidlineRight;
            vtmp.color = Color.white;
            vtmp.raycastTarget = false;

            uiImage.objectReferenceValue = fill;
            sso.ApplyModifiedProperties();
            bars.AddRow(status, fill, vtmp, row);
        }
        bars.RemoveMissingRows();
        EditorUtility.SetDirty(bars);

        int Order(StatusManager st)
        {
            int i = System.Array.IndexOf(StatusOrder, Key(st));
            return i < 0 ? StatusOrder.Length : i;
        }
    }

    private static TMP_Text FindValue(Transform row)
    {
        Transform v = row.Find("Background/Value");
        return v != null ? v.GetComponent<TMP_Text>() : null;
    }

    /// <summary>
    /// Removes extra bars with the same name (left by builds that ran twice): the one a status manager uses is kept,
    /// otherwise the first one.
    /// </summary>
    private static void RemoveDuplicateBars(Transform panel, List<StatusManager> statuses)
    {
        var used = new HashSet<Transform>();
        foreach (StatusManager st in statuses)
        {
            var img = new SerializedObject(st).FindProperty("uiImage").objectReferenceValue as Image;
            if (img != null && img.transform.IsChildOf(panel))
            {
                Transform row = img.transform;
                while (row.parent != panel) row = row.parent;
                used.Add(row);
            }
        }
        var byName = new Dictionary<string, List<Transform>>();
        foreach (Transform child in panel)
        {
            if (!child.name.EndsWith("Bar")) continue;
            if (!byName.TryGetValue(child.name, out List<Transform> list)) byName[child.name] = list = new List<Transform>();
            list.Add(child);
        }
        int removed = 0;
        foreach (List<Transform> group in byName.Values)
        {
            if (group.Count < 2) continue;
            Transform keep = group.Find(t => used.Contains(t)) ?? group[0];
            foreach (Transform t in group)
            {
                if (t == keep) continue;
                DestroyObj(t.gameObject);
                removed++;
            }
        }
        if (removed > 0)
            Debug.Log($"[Inventory UI Builder] Removed {removed} duplicate status bar(s) under '{panel.name}'.", panel);
    }

    private static string Key(StatusManager status)
    {
        string n = status.GetType().Name;
        return n.EndsWith("Manager") ? n.Substring(0, n.Length - "Manager".Length) : n;
    }

    /// <summary>The "look at something to interact" prompt of the Player component: a progress circle and a text.</summary>
    private static void BuildInteractionPrompt(Transform root, GameObject player, InventoryManager manager, InventoryUIBuildSettings s)
    {
        Player p = FindPlayerComponent(player);
        if (p == null)
            return;
        var pso = new SerializedObject(p);
        SerializedProperty ui = pso.FindProperty("interactionUI");
        SerializedProperty circle = pso.FindProperty("fillingCircle");
        SerializedProperty text = pso.FindProperty("interactionText");
        SerializedProperty inv = pso.FindProperty("inventoryManager");
        if (inv != null && inv.objectReferenceValue == null)
            inv.objectReferenceValue = manager;

        if (ui.objectReferenceValue == null || circle.objectReferenceValue == null || (text != null && text.objectReferenceValue == null))
        {
            Sprite round = GetOrCreateSprite(s, "UICircle", true);
            Transform prompt = FindOrCreateChild(root, "InteractionPrompt", false);
            var rt = (RectTransform)prompt;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -70f);
            rt.sizeDelta = new Vector2(240f, 90f);

            Transform ring = FindOrCreateChild(prompt, "Ring", false);
            var ringRt = (RectTransform)ring;
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 1f);
            ringRt.pivot = new Vector2(0.5f, 1f);
            ringRt.sizeDelta = new Vector2(44f, 44f);
            var ringImg = GetOrAdd<Image>(ring.gameObject);
            ringImg.sprite = round;
            ringImg.color = new Color(0f, 0f, 0f, 0.5f);
            ringImg.raycastTarget = false;

            Transform fillT = FindOrCreateChild(ring, "FillingCircle", true);
            var fill = GetOrAdd<Image>(fillT.gameObject);
            fill.sprite = round;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 1f;
            fill.color = new Color(1f, 1f, 1f, 0.9f);
            fill.raycastTarget = false;

            TextMeshProUGUI label = Label(prompt, "Text", "Interact", s, 18, Vector2.zero, TextAlignmentOptions.Center, top: false);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f);
            lrt.pivot = new Vector2(0.5f, 0f);
            lrt.sizeDelta = new Vector2(0f, 30f);

            if (ui.objectReferenceValue == null) ui.objectReferenceValue = prompt.gameObject;
            if (circle.objectReferenceValue == null) circle.objectReferenceValue = fill;
            if (text != null && text.objectReferenceValue == null) text.objectReferenceValue = label;
            prompt.gameObject.SetActive(false); // the Player shows it while looking at something interactable
        }
        pso.ApplyModifiedProperties();
    }

    /// <summary>Adds (or removes) preview slots so the grid shows the configured number while editing.</summary>
    private static void PreviewSlots(Transform parent, GameObject slotPrefab, int count, string prefix, bool hotbar)
    {
        var existing = new List<InventorySlot>(parent.GetComponentsInChildren<InventorySlot>(true));
        for (int i = existing.Count; i < count; i++)
        {
            GameObject slot = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, parent);
            slot.name = prefix + i;
            Created(slot, "Create slot");
            var c = slot.GetComponent<InventorySlot>();
            c.SlotType = SlotType.Common;
            c.SetAsHotbarSlot(hotbar);
        }
        for (int i = existing.Count - 1; i >= count; i--)
        {
            if (existing[i] != null && existing[i].heldItem == null)
                DestroyObj(existing[i].gameObject);
        }
    }

    // ------------------------------------------------------------------ scene objects
    public static void EnsureEventSystem()
    {
        // Inactive ones count too: enabling them later would give two.
        if (Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include).Length > 0)
            return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
        Created(go, "Create EventSystem");
    }

    /// <summary>
    /// The canvas of the inventory: the one its UI already uses; else a screen-space canvas inside the player (so a
    /// player prefab carries its own UI); else a new one inside the player (or in the scene when there is no player).
    /// </summary>
    private static Canvas FindOrCreateCanvas(InventoryManager manager, GameObject player, GameObject stageRoot)
    {
        if (manager != null)
        {
            var so = new SerializedObject(manager);
            var hotbar = so.FindProperty("slotManager.creationManager.hotbarSlotsParent").objectReferenceValue as Transform;
            Canvas c = InventoryValidator.FindCanvas(manager, hotbar, so.FindProperty("inventoryParent").objectReferenceValue as GameObject);
            if (c != null) return c;
        }
        Transform owner = player != null ? player.transform : null;
        if (owner != null)
        {
            foreach (Canvas c in owner.GetComponentsInChildren<Canvas>(true))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                    return c;
        }
        else if (stageRoot == null)
        {
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.name.Contains("Inventory"))
                    return c;
        }
        else
        {
            owner = stageRoot.transform;
        }

        var go = new GameObject("Inventory Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Created(go, "Create Canvas");
        if (owner != null)
            go.transform.SetParent(owner, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    /// <summary>A white square or a white disc sprite saved next to the generated prefabs (filled bars need a sprite).</summary>
    public static Sprite GetOrCreateSprite(InventoryUIBuildSettings s, string name, bool circle)
    {
        string folder = EnsureFolder(s.prefabFolder);
        string path = $"{folder}/{name}.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;

        int size = circle ? 128 : 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                byte a = 255;
                if (circle)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    a = (byte)Mathf.RoundToInt(Mathf.Clamp01(r - d) * 255f); // anti-aliased edge
                }
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Transform FindOrCreateChild(Transform parent, string name, bool stretch)
    {
        Transform t = parent.Find(name);
        if (t != null)
            return t;
        var go = new GameObject(name, typeof(RectTransform));
        Created(go, "Create " + name);
        go.transform.SetParent(parent, false);
        if (stretch)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        return go.transform;
    }

    private static Transform ExistingOr(SerializedProperty prop, System.Func<Transform> create)
    {
        return prop.objectReferenceValue is Transform t && t != null ? t : create();
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : AddComp<T>(go);
    }

    private static void Panel(Transform t, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, bool withImage = true)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        if (!withImage)
            return;
        var img = GetOrAdd<Image>(t.gameObject);
        img.color = color;
    }

    private static GridLayoutGroup Grid(Transform parent, InventoryUIBuildSettings s, int columns) => Grid(parent, s.slotSize, s.spacing, s.spacing, columns);

    private static GridLayoutGroup Grid(Transform parent, float cellSize, float spacing, float padding, int columns)
    {
        var grid = GetOrAdd<GridLayoutGroup>(parent.gameObject);
        grid.cellSize = new Vector2(cellSize, cellSize);
        grid.spacing = new Vector2(spacing, spacing);
        int pad = Mathf.RoundToInt(padding);
        grid.padding = new RectOffset(pad, pad, pad, pad);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        grid.childAlignment = TextAnchor.MiddleCenter;
        return grid;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string text, InventoryUIBuildSettings s, float size, Vector2 offset, TextAlignmentOptions align, bool top)
    {
        Transform t = parent.Find(name);
        bool created = t == null;
        if (created)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Created(go, "Create " + name);
            go.transform.SetParent(parent, false);
            t = go.transform;
        }
        var tmp = GetOrAdd<TextMeshProUGUI>(t.gameObject);
        if (created)
        {
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = s.textColor;
            tmp.raycastTarget = false;
            var rt = (RectTransform)t;
            if (top)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = offset;
                rt.sizeDelta = new Vector2(0f, size + 12f);
            }
            GetOrAdd<LayoutElement>(t.gameObject).ignoreLayout = top;
        }
        return tmp;
    }

    private static void BuildEquipmentSlots(Transform parent, GameObject slotPrefab, InventoryUIBuildSettings s)
    {
        // Existing slots of each type are kept; missing ones (counting duplicates such as two rings) are added.
        var existing = new Dictionary<SlotType, int>();
        foreach (InventorySlot slot in parent.GetComponentsInChildren<InventorySlot>(true))
        {
            existing[slot.SlotType] = (existing.TryGetValue(slot.SlotType, out int n) ? n : 0) + 1;
            // The Shield slot is the Off Hand now (shields and dual-wielded weapons): older built labels are renamed.
            if (slot.SlotType == SlotType.Shield && slot.transform.parent != null)
            {
                Transform label = slot.transform.parent.Find("Label");
                TextMeshProUGUI tmp = label != null ? label.GetComponent<TextMeshProUGUI>() : null;
                if (tmp != null && tmp.text == "Shield")
                {
                    Record(tmp, "Rename the Shield slot");
                    tmp.text = SlotTypeHelper.GetDisplayName(SlotType.Shield);
                    EditorUtility.SetDirty(tmp);
                }
            }
        }
        var wanted = new Dictionary<SlotType, int>();
        foreach (SlotType type in s.equipmentSlots)
        {
            if (type == SlotType.Common) continue;
            wanted[type] = (wanted.TryGetValue(type, out int w) ? w : 0) + 1;
            existing.TryGetValue(type, out int have);
            if (have >= wanted[type]) continue;
            existing[type] = have + 1;

            // A row: the slot and its label.
            GameObject row = new GameObject($"{type}Row", typeof(RectTransform));
            Created(row, "Create equipment slot");
            row.transform.SetParent(parent, false);
            GameObject slotGo = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, row.transform);
            slotGo.name = $"{type}Slot" + (wanted[type] > 1 ? $"_{wanted[type]}" : "");
            var srt = (RectTransform)slotGo.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.anchoredPosition = Vector2.zero;
            srt.sizeDelta = new Vector2(s.slotSize, s.slotSize);
            var slotComponent = slotGo.GetComponent<InventorySlot>();
            slotComponent.SlotType = type;
            slotComponent.SetAsHotbarSlot(false);
            var img = slotGo.GetComponent<Image>();
            if (img != null) img.color = s.equipmentSlotColor;
            EditorUtility.SetDirty(slotComponent);

            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(row.transform, false);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(s.slotSize + 8f, 0f);
            lrt.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = SlotTypeHelper.GetDisplayName(type);
            tmp.fontSize = 18f;
            tmp.color = s.textColor;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;
        }
    }

    private static ItemInfo BuildItemInfo(Transform root, InventoryManager manager, InventoryUIBuildSettings s)
    {
        Transform panel = FindOrCreateChild(root, "ItemInfo", false);
        Panel(panel, new Color(0.02f, 0.02f, 0.03f, 0.95f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 420f));
        var layout = GetOrAdd<VerticalLayoutGroup>(panel.gameObject);
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 4f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI Line(string n, float size, string sample)
        {
            TextMeshProUGUI t = Label(panel, n, sample, s, size, Vector2.zero, TextAlignmentOptions.Left, false);
            GetOrAdd<LayoutElement>(t.gameObject).ignoreLayout = false;
            return t;
        }

        var info = GetOrAdd<ItemInfo>(panel.gameObject);
        var so = new SerializedObject(info);
        so.FindProperty("inventoryManager").objectReferenceValue = manager;
        so.FindProperty("itemName").objectReferenceValue = Line("Name", 22f, "Item Name");
        so.FindProperty("itemType").objectReferenceValue = Line("Type", 15f, "Type");
        so.FindProperty("itemQuantity").objectReferenceValue = Line("Quantity", 15f, "Quantity");
        so.FindProperty("itemWeight").objectReferenceValue = Line("Weight", 15f, "Weight");
        so.FindProperty("itemPrice").objectReferenceValue = Line("Price", 15f, "Price");
        so.FindProperty("itemDescription").objectReferenceValue = Line("Description", 15f, "Description");
        so.FindProperty("itemStats").objectReferenceValue = Line("Stats", 14f, "Stats");
        so.ApplyModifiedProperties();

        // Split button
        if (panel.Find("SplitButton") == null)
        {
            var btnGo = new GameObject("SplitButton", typeof(RectTransform), typeof(Image), typeof(Button));
            Created(btnGo, "Create split button");
            btnGo.transform.SetParent(panel, false);
            btnGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            var le = btnGo.AddComponent<LayoutElement>();
            le.minHeight = 32f;
            var label = new GameObject("Text", typeof(RectTransform));
            label.transform.SetParent(btnGo.transform, false);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = "Split Stack";
            tmp.fontSize = 16f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = s.textColor;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btnGo.GetComponent<Button>().onClick, info.SplitItem);
        }

        panel.gameObject.SetActive(false);
        return info;
    }

    // ------------------------------------------------------------------ armor sets window
    private static readonly string[] ArmorSetUICore = { "setListContainer", "setListItemPrefab", "equipmentSlotsContainer", "equipmentSlotPrefab", "setEffectPrefab" };

    /// <summary>
    /// True when <paramref name="ui"/> has no window wired at all (a new component). A UI you wired yourself, even partly,
    /// is never rebuilt automatically; its inspector's Build / Repair fills the gaps.
    /// </summary>
    public static bool ArmorSetUIMissing(ArmorSetUIManager ui) => CountAssigned(new SerializedObject(ui), ArmorSetUICore) == 0;

    private static int CountAssigned(SerializedObject so, string[] names)
    {
        int n = 0;
        foreach (string name in names)
            if (so.FindProperty(name)?.objectReferenceValue != null) n++;
        return n;
    }

    private static void SetIfEmpty(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null && p.objectReferenceValue == null)
            p.objectReferenceValue = value;
    }

    /// <summary>
    /// Build / Repair for one ArmorSetUIManager (its inspector button): uses the canvas it is in, else the inventory's
    /// canvas, else the player's, and creates a canvas inside the player (or its root object) when there is none.
    /// </summary>
    public static ArmorSetUIManager BuildArmorSetUIFor(ArmorSetUIManager ui)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Armor Set UI");

        GameObject root = ui.transform.root.gameObject;
        InventoryManager manager = root.GetComponentInChildren<InventoryManager>(true);
        PlayerStatusController ps = ui.GetComponentInParent<PlayerStatusController>(true);
        if (ps == null) ps = root.GetComponentInChildren<PlayerStatusController>(true);
        GameObject player = ps != null ? ps.gameObject : ResolvePlayer(manager, null);
        GameObject stageRoot = StageRoot(root);

        Transform parent;
        Canvas canvas = ui.GetComponentInParent<Canvas>(true);
        if (canvas != null)
        {
            parent = canvas.rootCanvas.transform;
        }
        else
        {
            if (stageRoot == null)
                EnsureEventSystem();
            canvas = FindOrCreateCanvas(manager, player != null ? player : root, stageRoot);
            Transform inventoryRoot = canvas.transform.Find(RootName);
            parent = inventoryRoot != null ? inventoryRoot : canvas.transform;
        }

        Transform inventoryPanel = null;
        if (manager != null && new SerializedObject(manager).FindProperty("inventoryParent").objectReferenceValue is GameObject panel)
            inventoryPanel = panel.transform;

        ui = BuildArmorSetUI(ui, parent, manager, player, inventoryPanel, InventoryUIBuildSettings.FromManager(manager), repair: true);
        if (manager != null)
        {
            var so = new SerializedObject(manager);
            so.FindProperty("armorSetUIManager").objectReferenceValue = ui;
            so.ApplyModifiedProperties();
        }
        Undo.CollapseUndoOperations(group);
        return ui;
    }

    /// <summary>
    /// Builds (or repairs) the Armor Sets window and wires every reference of <paramref name="ui"/> (created on an
    /// "ArmorSetUI" object under <paramref name="parent"/> when null): window, set list, set details (icon, name, progress
    /// bar, description), piece slots, active / next bonuses, the three entry prefabs, audio source, the player's
    /// ArmorSetManager and InventoryManager, a close button and a "Sets" button on the inventory panel.
    /// </summary>
    public static ArmorSetUIManager BuildArmorSetUI(ArmorSetUIManager ui, Transform parent, InventoryManager inventory, GameObject player,
        Transform inventoryPanel, InventoryUIBuildSettings s, bool repair = false)
    {
        if (ui == null)
            ui = parent.GetComponentInChildren<ArmorSetUIManager>(true);
        if (ui == null && player != null)
            ui = player.GetComponentInChildren<ArmorSetUIManager>(true);
        Transform holder;
        if (ui == null)
        {
            holder = FindOrCreateChild(parent, "ArmorSetUI", stretch: true);
            ui = AddComp<ArmorSetUIManager>(holder.gameObject);
        }
        else
        {
            // A manager placed on a non-UI object keeps its place; its window is built under the canvas.
            holder = ui.transform is RectTransform && ui.GetComponentInParent<Canvas>(true) != null ? ui.transform : FindOrCreateChild(parent, "ArmorSetUI", stretch: true);
        }
        var uso = new SerializedObject(ui);

        // Build the window for a new (unwired) manager, or on an explicit repair when parts are missing. Fields that are
        // already assigned are never replaced, so a UI wired by hand keeps its own objects.
        GameObject window = uso.FindProperty("windowPanel").objectReferenceValue as GameObject;
        int assigned = CountAssigned(uso, ArmorSetUICore);
        if (assigned == 0 || (repair && assigned < ArmorSetUICore.Length))
        {
            Sprite white = GetOrCreateSprite(s, "UIWhite", false);
            GameObject listItemPrefab = GetOrCreateUIPrefab(s, "ArmorSetListItem", () => MakeSetListItem(s));
            GameObject effectPrefab = GetOrCreateUIPrefab(s, "ArmorSetBonus", () => MakeSetBonus(s));
            GameObject pieceSlotPrefab = GetOrCreateUIPrefab(s, "ArmorSetPieceSlot", () => MakeSetPieceSlot(s));

            // Window
            Transform win = FindOrCreateChild(holder, "Window", false);
            Panel(win, s.panelColor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 520f));
            window = win.gameObject;
            TextMeshProUGUI title = Label(win, "Title", "Armor Sets", s, 24, new Vector2(0f, -10f), TextAlignmentOptions.Center, top: true);

            Button close = MakeButton(win, "CloseButton", "X", s);
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-10f, -10f);
            crt.sizeDelta = new Vector2(36f, 36f);
            if (close.onClick.GetPersistentEventCount() == 0)
                UnityEditor.Events.UnityEventTools.AddPersistentListener(close.onClick, ui.HideUI);

            // Left column: worn sets
            Transform listPanel = Area(win, "SetListPanel", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 16f), new Vector2(236f, -56f));
            GetOrAdd<Image>(listPanel.gameObject).color = new Color(1f, 1f, 1f, 0.04f);
            Label(listPanel, "Header", "Worn sets", s, 16, new Vector2(0f, -6f), TextAlignmentOptions.Center, top: true);
            Transform list = Area(listPanel, "SetList", Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -34f));
            var listLayout = GetOrAdd<VerticalLayoutGroup>(list.gameObject);
            listLayout.spacing = 6f; listLayout.childControlHeight = false; listLayout.childControlWidth = true; listLayout.childForceExpandHeight = false;

            // Right: details of the selected set
            Transform info = Area(win, "SetInfoPanel", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(252f, 16f), new Vector2(-16f, -56f));
            Transform iconT = Area(info, "SetIcon", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -64f), new Vector2(64f, 0f));
            var icon = GetOrAdd<Image>(iconT.gameObject); icon.preserveAspect = true; icon.raycastTarget = false;
            TextMeshProUGUI setName = Label(info, "SetName", "Set Name", s, 22, Vector2.zero, TextAlignmentOptions.Left, top: false);
            Place(setName.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(76f, -34f), new Vector2(-60f, 0f));
            TextMeshProUGUI progressText = Label(info, "ProgressText", "0/4", s, 16, Vector2.zero, TextAlignmentOptions.Right, top: false);
            Place(progressText.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-56f, -64f), new Vector2(0f, -38f));
            Slider progress = MakeProgressBar(info, white);
            Place(progress.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(76f, -58f), new Vector2(-64f, -44f));
            TextMeshProUGUI description = Label(info, "Description", "", s, 14, Vector2.zero, TextAlignmentOptions.TopLeft, top: false);
            Place(description.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -170f), new Vector2(0f, -72f));

            TextMeshProUGUI piecesHeader = Label(info, "PiecesHeader", "Pieces", s, 16, Vector2.zero, TextAlignmentOptions.Left, top: false);
            Place(piecesHeader.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -196f), new Vector2(0f, -174f));
            Transform pieces = Area(info, "Pieces", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -290f), new Vector2(0f, -200f));
            var piecesGrid = GetOrAdd<GridLayoutGroup>(pieces.gameObject);
            piecesGrid.cellSize = new Vector2(128f, 40f); piecesGrid.spacing = new Vector2(6f, 6f);

            // Bonuses (inside the details area)
            Transform bonuses = Area(info, "BonusesPanel", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -298f));
            Transform activeCol = Area(bonuses, "Active", new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-4f, 0f));
            Label(activeCol, "Header", "Active bonuses", s, 15, new Vector2(0f, 0f), TextAlignmentOptions.Left, top: true);
            Transform activeList = Area(activeCol, "List", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -26f));
            Transform nextCol = Area(bonuses, "Next", new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(4f, 0f), Vector2.zero);
            Label(nextCol, "Header", "Next bonuses", s, 15, new Vector2(0f, 0f), TextAlignmentOptions.Left, top: true);
            Transform nextList = Area(nextCol, "List", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -26f));
            foreach (Transform l in new[] { activeList, nextList })
            {
                var v = GetOrAdd<VerticalLayoutGroup>(l.gameObject);
                v.spacing = 4f; v.childControlHeight = false; v.childControlWidth = true; v.childForceExpandHeight = false;
            }

            SetIfEmpty(uso, "windowPanel", window);
            SetIfEmpty(uso, "setListPanel", listPanel.gameObject);
            SetIfEmpty(uso, "setInfoPanel", info.gameObject);
            SetIfEmpty(uso, "setEffectPanel", bonuses.gameObject);
            SetIfEmpty(uso, "setNameText", setName);
            SetIfEmpty(uso, "setDescriptionText", description);
            SetIfEmpty(uso, "setIconImage", icon);
            SetIfEmpty(uso, "setProgressSlider", progress);
            SetIfEmpty(uso, "setProgressText", progressText);
            SetIfEmpty(uso, "activeEffectsContainer", activeList);
            SetIfEmpty(uso, "availableEffectsContainer", nextList);
            SetIfEmpty(uso, "setEffectPrefab", effectPrefab);
            SetIfEmpty(uso, "setListContainer", list);
            SetIfEmpty(uso, "setListItemPrefab", listItemPrefab);
            SetIfEmpty(uso, "equipmentSlotsContainer", pieces);
            SetIfEmpty(uso, "equipmentSlotPrefab", pieceSlotPrefab);
            window = uso.FindProperty("windowPanel").objectReferenceValue as GameObject ?? window;
            window.SetActive(false); // opened with the Sets button / ArmorSetUI key, or when a set is completed
        }

        // References that can always be refreshed
        AudioSource audio = ui.GetComponent<AudioSource>();
        if (audio == null) { audio = AddComp<AudioSource>(ui.gameObject); audio.playOnAwake = false; }
        if (uso.FindProperty("uiAudioSource").objectReferenceValue == null) uso.FindProperty("uiAudioSource").objectReferenceValue = audio;
        Canvas canvas = ui.GetComponentInParent<Canvas>(true);
        if (canvas == null && window != null) canvas = window.GetComponentInParent<Canvas>(true);
        if (canvas != null) uso.FindProperty("armorSetCanvas").objectReferenceValue = canvas.rootCanvas;
        if (inventory != null && uso.FindProperty("inventoryManager").objectReferenceValue == null) uso.FindProperty("inventoryManager").objectReferenceValue = inventory;
        if (player != null && uso.FindProperty("armorSetManager").objectReferenceValue == null)
        {
            ArmorSetManager asm = player.GetComponentInChildren<ArmorSetManager>(true);
            if (asm == null)
            {
                // Normally added at runtime by the EquipmentManager; adding it now lets the reference be saved.
                PlayerStatusController ps = player.GetComponentInChildren<PlayerStatusController>(true);
                asm = AddComp<ArmorSetManager>(ps != null ? ps.gameObject : player);
            }
            uso.FindProperty("armorSetManager").objectReferenceValue = asm;
        }
        uso.ApplyModifiedProperties();

        // "Sets" button on the inventory panel
        if (inventoryPanel != null && inventoryPanel.Find("SetsButton") == null)
        {
            Button sets = MakeButton(inventoryPanel, "SetsButton", "Sets", s);
            var srt = (RectTransform)sets.transform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(1f, 1f);
            srt.anchoredPosition = new Vector2(-10f, -10f);
            srt.sizeDelta = new Vector2(64f, 30f);
            GetOrAdd<LayoutElement>(sets.gameObject).ignoreLayout = true;
            UnityEditor.Events.UnityEventTools.AddPersistentListener(sets.onClick, ui.ToggleUI);
        }
        EditorUtility.SetDirty(ui);
        return ui;
    }

    private static Transform Area(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Transform t = FindOrCreateChild(parent, name, false);
        Place(t, anchorMin, anchorMax, offsetMin, offsetMax);
        return t;
    }

    private static void Place(Transform t, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
    }

    private static Button MakeButton(Transform parent, string name, string label, InventoryUIBuildSettings s)
    {
        Transform t = FindOrCreateChild(parent, name, false);
        var img = GetOrAdd<Image>(t.gameObject);
        img.color = new Color(1f, 1f, 1f, 0.15f);
        var button = GetOrAdd<Button>(t.gameObject);
        if (t.Find("Text") == null)
        {
            var textGo = new GameObject("Text", typeof(RectTransform));
            Created(textGo, "Create Text");
            textGo.transform.SetParent(t, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 16f; tmp.alignment = TextAlignmentOptions.Center; tmp.color = s.textColor; tmp.raycastTarget = false;
        }
        return button;
    }

    private static Slider MakeProgressBar(Transform parent, Sprite white)
    {
        Transform t = FindOrCreateChild(parent, "Progress", false);
        var bg = GetOrAdd<Image>(t.gameObject);
        bg.sprite = white; bg.color = new Color(0f, 0f, 0f, 0.4f);
        Transform area = FindOrCreateChild(t, "Fill Area", true);
        Transform fill = FindOrCreateChild(area, "Fill", true);
        var fimg = GetOrAdd<Image>(fill.gameObject);
        fimg.sprite = white; fimg.color = new Color(0.35f, 0.75f, 0.45f);
        var slider = GetOrAdd<Slider>(t.gameObject);
        slider.fillRect = (RectTransform)fill;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 0.5f;
        return slider;
    }

    // Entry prefabs (child names are the ones ArmorSetUIManager looks up).
    private static GameObject MakeSetListItem(InventoryUIBuildSettings s)
    {
        var go = new GameObject("ArmorSetListItem", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        ((RectTransform)go.transform).sizeDelta = new Vector2(200f, 58f);
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
        go.GetComponent<LayoutElement>().preferredHeight = 58f;
        PrefabText(go.transform, "SetName", "Set", 16f, FontStyles.Bold, new Vector2(0f, 0.5f), new Vector2(1f, 1f), TextAlignmentOptions.Left, s);
        PrefabText(go.transform, "Progress", "0/4", 13f, FontStyles.Normal, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), TextAlignmentOptions.Left, s);
        PrefabText(go.transform, "Status", "Incomplete", 13f, FontStyles.Italic, new Vector2(0.5f, 0f), new Vector2(1f, 0.5f), TextAlignmentOptions.Right, s);
        return go;
    }

    private static GameObject MakeSetBonus(InventoryUIBuildSettings s)
    {
        var go = new GameObject("ArmorSetBonus", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        ((RectTransform)go.transform).sizeDelta = new Vector2(260f, 64f);
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
        go.GetComponent<LayoutElement>().preferredHeight = 64f;
        PrefabText(go.transform, "EffectName", "Bonus", 15f, FontStyles.Bold, new Vector2(0f, 0.62f), new Vector2(0.7f, 1f), TextAlignmentOptions.Left, s);
        PrefabText(go.transform, "PiecesRequired", "Requires 2 pieces", 12f, FontStyles.Italic, new Vector2(0.55f, 0.62f), new Vector2(1f, 1f), TextAlignmentOptions.Right, s);
        PrefabText(go.transform, "EffectDescription", "What the bonus does", 12f, FontStyles.Normal, new Vector2(0f, 0f), new Vector2(1f, 0.62f), TextAlignmentOptions.TopLeft, s);
        return go;
    }

    private static GameObject MakeSetPieceSlot(InventoryUIBuildSettings s)
    {
        var go = new GameObject("ArmorSetPieceSlot", typeof(RectTransform), typeof(Image));
        ((RectTransform)go.transform).sizeDelta = new Vector2(128f, 40f);
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(go.transform, false);
        var irt = (RectTransform)icon.transform;
        irt.anchorMin = new Vector2(0f, 0f); irt.anchorMax = new Vector2(0f, 1f); irt.pivot = new Vector2(0f, 0.5f);
        irt.offsetMin = new Vector2(4f, 4f); irt.offsetMax = new Vector2(36f, -4f);
        icon.GetComponent<Image>().preserveAspect = true;
        PrefabText(go.transform, "Name", "Helmet", 13f, FontStyles.Normal, new Vector2(0f, 0f), new Vector2(1f, 1f), TextAlignmentOptions.Left, s, 40f);
        return go;
    }

    private static void PrefabText(Transform parent, string name, string text, float size, FontStyles style, Vector2 min, Vector2 max,
        TextAlignmentOptions align, InventoryUIBuildSettings s, float left = 8f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = new Vector2(left, 2f); rt.offsetMax = new Vector2(-8f, -2f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style; tmp.alignment = align; tmp.color = s.textColor; tmp.raycastTarget = false;
    }

    private static GameObject GetOrCreateUIPrefab(InventoryUIBuildSettings s, string fileName, System.Func<GameObject> build)
    {
        string path = EnsureFolder(s.prefabFolder) + "/" + fileName + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing;
        GameObject go = build();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ------------------------------------------------------------------ prefabs
    private static string EnsureFolder(string folder)
    {
        folder = string.IsNullOrWhiteSpace(folder) ? "Assets/Inventory/Generated" : folder.Replace('\\', '/').TrimEnd('/');
        if (!folder.StartsWith("Assets"))
            folder = "Assets/" + folder;
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
        return folder;
    }

    public static GameObject GetOrCreateSlotPrefab(InventoryUIBuildSettings s)
    {
        string folder = EnsureFolder(s.prefabFolder);
        string path = folder + "/InventorySlot.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing;

        var go = new GameObject("InventorySlot", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(InventorySlot));
        ((RectTransform)go.transform).sizeDelta = new Vector2(s.slotSize, s.slotSize);
        var img = go.GetComponent<Image>();
        img.color = s.slotColor;
        img.raycastTarget = true;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    public static GameObject GetOrCreateItemPrefab(InventoryUIBuildSettings s)
    {
        string folder = EnsureFolder(s.prefabFolder);
        string path = folder + "/InventoryItem.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing;

        var go = new GameObject("InventoryItem", typeof(RectTransform), typeof(CanvasGroup));
        ((RectTransform)go.transform).sizeDelta = new Vector2(s.slotSize, s.slotSize);

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(go.transform, false);
        var irt = (RectTransform)icon.transform;
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(6f, 6f); irt.offsetMax = new Vector2(-6f, -6f);
        var iconImage = icon.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        var stack = new GameObject("StackText", typeof(RectTransform), typeof(Text));
        stack.transform.SetParent(go.transform, false);
        var srt = (RectTransform)stack.transform;
        srt.anchorMin = new Vector2(0.4f, 0f); srt.anchorMax = new Vector2(1f, 0.4f);
        srt.offsetMin = Vector2.zero; srt.offsetMax = new Vector2(-4f, 0f);
        var text = stack.GetComponent<Text>();
        text.font = LoadBuiltinFont();
        text.fontSize = 18;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.LowerRight;
        text.color = s.textColor;
        text.raycastTarget = false;
        stack.AddComponent<Outline>();

        var item = go.AddComponent<InventoryItem>();
        item.IconImage = iconImage;
        item.StackText = text;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static Font LoadBuiltinFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null)
        {
            try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return f;
    }
}
#endif
