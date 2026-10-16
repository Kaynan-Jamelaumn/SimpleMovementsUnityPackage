#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Tools ▸ SimpleMovements ▸ Inventory ▸ UI Builder: options and the build button.</summary>
public class InventoryUIBuilderWindow : EditorWindow
{
    [SerializeField] private InventoryManager manager;
    [SerializeField] private GameObject player;
    [SerializeField] private InventoryUIBuildSettings settings = new InventoryUIBuildSettings();
    private Vector2 scroll;
    private bool showEquipment = true;

    [MenuItem(SMMenu.Root + "Inventory/UI Builder", false, SMMenu.Inventory + 0)]
    public static void OpenFromMenu() => Open(Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<InventoryManager>() : null);

    public static void Open(InventoryManager target)
    {
        var w = GetWindow<InventoryUIBuilderWindow>("Inventory UI Builder");
        w.minSize = new Vector2(380f, 460f);
        if (target == null)
            target = Object.FindAnyObjectByType<InventoryManager>(FindObjectsInactive.Include);
        w.manager = target;
        if (target != null && InventoryUIBuilder.PrefabAssetPathOf(target.gameObject) != null)
        {
            // A prefab asset: its own player, never one from the open scene.
            PlayerStatusController inAsset = target.transform.root.GetComponentInChildren<PlayerStatusController>(true);
            w.player = inAsset != null ? inAsset.gameObject : null;
        }
        else if (w.player == null || InventoryUIBuilder.PrefabAssetPathOf(w.player) != null)
        {
            PlayerStatusController ps = Object.FindAnyObjectByType<PlayerStatusController>();
            w.player = ps != null ? ps.gameObject : null;
        }
        w.Show();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Creates what the inventory needs and wires it: canvas, event system, hotbar, inventory grid, equipment slots with labels, storage, item tooltip, drop zone, slot and item prefabs. Parts that already exist and are assigned are kept.", MessageType.Info);

        manager = (InventoryManager)EditorGUILayout.ObjectField(new GUIContent("Inventory Manager", "Empty = one is created on the generated UI root."), manager, typeof(InventoryManager), true);
        player = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Player", "The player object (with the PlayerStatusController)."), player, typeof(GameObject), true);
        if (manager != null && InventoryUIBuilder.PrefabAssetPathOf(manager.gameObject) != null)
            EditorGUILayout.HelpBox("This Inventory Manager is in a prefab asset: the UI is built inside the prefab (saved), so every player spawned from it has it.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Slots", EditorStyles.boldLabel);
        settings.slotSize = EditorGUILayout.Slider("Slot Size", settings.slotSize, 32f, 160f);
        settings.spacing = EditorGUILayout.Slider("Spacing", settings.spacing, 0f, 24f);
        settings.hotbarSlots = EditorGUILayout.IntSlider("Hotbar Slots", settings.hotbarSlots, 1, 9);
        settings.inventorySlots = EditorGUILayout.IntSlider("Inventory Slots", settings.inventorySlots, 1, 120);
        settings.inventoryColumns = EditorGUILayout.IntSlider("Inventory Columns", settings.inventoryColumns, 1, 16);
        if (manager != null)
        {
            settings.ReadGrid(new SerializedObject(manager));
            if (settings.gridInventory)
                EditorGUILayout.HelpBox($"Grid inventory is on (Inventory Manager ▸ Grid Inventory): the inventory panel is built as its grid, " +
                                        $"{settings.gridColumns} × {settings.gridRows} cells of {settings.InventoryCellSize:0} px. Inventory Slots / Columns " +
                                        "above are the classic layout, used when the grid is off.", MessageType.Info);
        }

        EditorGUILayout.Space();
        settings.buildEquipment = EditorGUILayout.ToggleLeft("Equipment panel", settings.buildEquipment, EditorStyles.boldLabel);
        if (settings.buildEquipment)
        {
            EditorGUI.indentLevel++;
            showEquipment = EditorGUILayout.Foldout(showEquipment, $"Equipment slots ({settings.equipmentSlots.Count})", true);
            if (showEquipment)
            {
                for (int i = 0; i < settings.equipmentSlots.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    settings.equipmentSlots[i] = (SlotType)EditorGUILayout.EnumPopup(settings.equipmentSlots[i]);
                    if (GUILayout.Button("−", GUILayout.Width(24f)))
                    {
                        settings.equipmentSlots.RemoveAt(i);
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                if (GUILayout.Button("Add slot"))
                    settings.equipmentSlots.Add(SlotType.Ring);
            }
            EditorGUI.indentLevel--;
        }
        settings.buildStorage = EditorGUILayout.ToggleLeft("Storage panel", settings.buildStorage);
        if (settings.buildStorage)
            settings.storageSlots = EditorGUILayout.IntSlider("   Storage Slots", settings.storageSlots, 1, 64);
        settings.buildItemInfo = EditorGUILayout.ToggleLeft("Item info tooltip", settings.buildItemInfo);
        settings.buildDropZone = EditorGUILayout.ToggleLeft("Drop zone (drop items into the world)", settings.buildDropZone);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Look", EditorStyles.boldLabel);
        settings.panelColor = EditorGUILayout.ColorField("Panel", settings.panelColor);
        settings.slotColor = EditorGUILayout.ColorField("Slot", settings.slotColor);
        settings.equipmentSlotColor = EditorGUILayout.ColorField("Equipment Slot", settings.equipmentSlotColor);
        settings.textColor = EditorGUILayout.ColorField("Text", settings.textColor);
        settings.prefabFolder = EditorGUILayout.TextField(new GUIContent("Prefab Folder", "Where the slot and item prefabs are saved (existing ones are reused)."), settings.prefabFolder);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            GUI.backgroundColor = new Color(0.55f, 0.9f, 0.55f);
            bool build = GUILayout.Button(manager == null ? "Build Inventory UI" : "Build / Repair Inventory UI", GUILayout.Height(34f));
            GUI.backgroundColor = Color.white;
            if (build)
            {
                // Built after this GUI pass: a build changes the hierarchy (and may save a prefab asset), which must not
                // happen in the middle of a GUILayout pass ("Invalid GUILayout state").
                InventoryManager m = manager;
                GameObject p = player;
                InventoryUIBuildSettings s = settings;
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        manager = InventoryUIBuilder.Build(m, p, s);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                    }
                    Repaint();
                };
            }
        }
        if (EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("Exit Play Mode to build.", MessageType.Warning);

        if (manager != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current setup", EditorStyles.boldLabel);
            foreach (InventoryIssue issue in InventoryValidator.Validate(manager))
                EditorGUILayout.HelpBox(issue.message, issue.severity);
        }
        EditorGUILayout.EndScrollView();
    }
}
#endif
