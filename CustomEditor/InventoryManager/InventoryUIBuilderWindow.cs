#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Tools ▸ Inventory ▸ UI Builder: options and the build button.</summary>
public class InventoryUIBuilderWindow : EditorWindow
{
    [SerializeField] private InventoryManager manager;
    [SerializeField] private GameObject player;
    [SerializeField] private InventoryUIBuildSettings settings = new InventoryUIBuildSettings();
    private Vector2 scroll;
    private bool showEquipment = true;

    [MenuItem("Tools/Inventory/UI Builder", priority = 0)]
    public static void OpenFromMenu() => Open(Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<InventoryManager>() : null);

    public static void Open(InventoryManager target)
    {
        var w = GetWindow<InventoryUIBuilderWindow>("Inventory UI Builder");
        w.minSize = new Vector2(380f, 460f);
        if (target == null)
            target = Object.FindAnyObjectByType<InventoryManager>(FindObjectsInactive.Include);
        w.manager = target;
        if (w.player == null)
        {
            PlayerStatusController ps = Object.FindAnyObjectByType<PlayerStatusController>();
            if (ps != null) w.player = ps.gameObject;
        }
        w.Show();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Creates what the inventory needs and wires it: canvas, event system, hotbar, inventory grid, equipment slots with labels, storage, item tooltip, drop zone, slot and item prefabs. Parts that already exist and are assigned are kept.", MessageType.Info);

        manager = (InventoryManager)EditorGUILayout.ObjectField(new GUIContent("Inventory Manager", "Empty = one is created on the generated UI root."), manager, typeof(InventoryManager), true);
        player = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Player", "The player object (with the PlayerStatusController)."), player, typeof(GameObject), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Slots", EditorStyles.boldLabel);
        settings.slotSize = EditorGUILayout.Slider("Slot Size", settings.slotSize, 32f, 160f);
        settings.spacing = EditorGUILayout.Slider("Spacing", settings.spacing, 0f, 24f);
        settings.hotbarSlots = EditorGUILayout.IntSlider("Hotbar Slots", settings.hotbarSlots, 1, 9);
        settings.inventorySlots = EditorGUILayout.IntSlider("Inventory Slots", settings.inventorySlots, 1, 120);
        settings.inventoryColumns = EditorGUILayout.IntSlider("Inventory Columns", settings.inventoryColumns, 1, 16);

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
            if (GUILayout.Button(manager == null ? "Build Inventory UI" : "Build / Repair Inventory UI", GUILayout.Height(34f)))
                manager = InventoryUIBuilder.Build(manager, player, settings);
            GUI.backgroundColor = Color.white;
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
