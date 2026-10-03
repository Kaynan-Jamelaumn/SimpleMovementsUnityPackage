#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the player's UI automatically in the editor - canvas, hotbar, inventory grid, equipment slots, storage,
/// item info, drop zone, status bars, interaction prompt and armor sets window, all wired - when you:
/// <list type="bullet">
/// <item>add an InventoryManager, Player, PlayerStatusController or ArmorSetUIManager component,</item>
/// <item>open a player prefab in Prefab Mode,</item>
/// <item>select a player or an inventory whose UI is missing.</item>
/// </list>
/// Only what is missing is created (see <see cref="InventoryUIBuilder.MissingParts"/>), once per object and editor
/// session, and it can be undone. Turn it off with Tools ▸ SimpleMovements ▸ Inventory ▸ Auto-Build UI When Missing.
/// </summary>
[InitializeOnLoad]
public static class InventoryAutoSetup
{
    private const string PrefKey = "Inventory.AutoBuildUI";
    private const string MenuPath = "Tools/SimpleMovements/Inventory/Auto-Build UI When Missing";

    private static readonly HashSet<GameObject> handled = new HashSet<GameObject>();
    private static readonly HashSet<GameObject> queue = new HashSet<GameObject>();
    private static bool scheduled;

    static InventoryAutoSetup()
    {
        Selection.selectionChanged += () => Schedule(Selection.activeGameObject);
        PrefabStage.prefabStageOpened += stage => Schedule(stage.prefabContentsRoot);
        ObjectFactory.componentWasAdded += OnComponentAdded;
    }

    public static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    [MenuItem(MenuPath, priority = 1)]
    private static void ToggleEnabled()
    {
        Enabled = !Enabled;
        if (Enabled)
        {
            handled.Clear();
            Schedule(Selection.activeGameObject);
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleEnabledValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    private static void OnComponentAdded(Component c)
    {
        if (c is ArmorSetUIManager armorUI)
        {
            handled.Remove(c.transform.root.gameObject);
            if (!Enabled)
                return;
            // With a player / inventory the normal build creates and wires the window; a manager on its own gets
            // its window (and a canvas when there is none) built directly.
            EditorApplication.delayCall += () =>
            {
                if (armorUI == null || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                try
                {
                    TryBuild(armorUI.gameObject);
                    if (armorUI != null && InventoryUIBuilder.ArmorSetUIMissing(armorUI))
                    {
                        InventoryUIBuilder.BuildArmorSetUIFor(armorUI);
                        Debug.Log($"[Inventory] Built the Armor Sets window for '{armorUI.name}'.", armorUI);
                    }
                }
                catch (System.Exception e) { Debug.LogException(e); }
            };
            return;
        }
        if (c is InventoryManager || c is Player || c is PlayerStatusController)
        {
            handled.Remove(c.transform.root.gameObject); // a new part: look again even if this object was handled
            Schedule(c.gameObject);
        }
    }

    private static void Schedule(GameObject target)
    {
        if (target == null || !Enabled)
            return;
        queue.Add(target);
        if (scheduled)
            return;
        scheduled = true;
        // Later, outside the selection / component callbacks (creating objects there is not allowed).
        EditorApplication.delayCall += RunQueued;
    }

    private static void RunQueued()
    {
        scheduled = false;
        var targets = new List<GameObject>(queue);
        queue.Clear();
        foreach (GameObject go in targets)
        {
            try { TryBuild(go); }
            catch (System.Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>Builds what is missing for the player / inventory that <paramref name="go"/> belongs to. Returns true if it built.</summary>
    public static bool TryBuild(GameObject go, bool force = false)
    {
        if (go == null || (!force && !Enabled))
            return false;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return false;
        if (EditorUtility.IsPersistent(go) || !go.scene.IsValid())
            return false; // a prefab asset in the Project window: it is built when opened in Prefab Mode

        GameObject root = go.transform.root.gameObject;
        if (!force && handled.Contains(root))
            return false;

        InventoryManager manager = root.GetComponentInChildren<InventoryManager>(true);
        GameObject player = FindPlayer(root);
        if (manager == null && player == null)
            return false; // neither a player nor an inventory

        GameObject stageRoot = InventoryUIBuilder.StageRoot(root);
        if (manager == null && stageRoot == null)
        {
            // The inventory may live elsewhere in the scene (on its own canvas) and point to this player.
            foreach (InventoryManager m in Object.FindObjectsByType<InventoryManager>(FindObjectsInactive.Include))
            {
                if (m.gameObject.scene == root.scene && new SerializedObject(m).FindProperty("player").objectReferenceValue == player)
                {
                    manager = m;
                    break;
                }
            }
        }
        if (player == null)
            player = InventoryUIBuilder.ResolvePlayer(manager, null);

        List<string> missing = InventoryUIBuilder.MissingParts(manager, player);
        handled.Add(root);
        if (missing.Count == 0)
            return false;

        // A player placed in a scene from a prefab: build the UI in the prefab asset itself. The instance gets it
        // from the prefab. (Building on the instance added the UI as overrides; opening the prefab later built it
        // again, and the instance ended up with two or three copies - e.g. several Weight bars.)
        bool managerInPlayer = manager == null || manager.transform.IsChildOf(root.transform);
        if (stageRoot == null && managerInPlayer && PrefabUtility.IsPartOfPrefabInstance(root))
        {
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            if (!string.IsNullOrEmpty(assetPath) && InventoryUIBuilder.BuildInPrefabAsset(assetPath))
            {
                Debug.Log($"[Inventory] Built the missing UI in the prefab '{assetPath}' ({string.Join(", ", missing)}); '{root.name}' and every other " +
                          "instance get it from the prefab. (Turn this off with Tools ▸ SimpleMovements ▸ Inventory ▸ Auto-Build UI When Missing.)",
                          AssetDatabase.LoadAssetAtPath<GameObject>(assetPath));
                return true;
            }
            // The prefab already has everything: what is missing is an override of this instance. Only an explicit
            // Build / Repair (force) adds it to the instance; automatic runs never add UI as instance overrides.
            if (!force)
            {
                Debug.Log($"[Inventory] '{root.name}' misses {string.Join(", ", missing)} although its prefab has them (instance overrides). " +
                          "Revert the instance's overrides, or press Build / Repair UI Now on its Inventory Manager.", root);
                return false;
            }
        }

        InventoryManager built = InventoryUIBuilder.Build(manager, player, InventoryUIBuildSettings.FromManager(manager), select: false);
        string where = stageRoot != null ? "Save the prefab to keep it." : "";
        Debug.Log($"[Inventory] Built the missing UI for '{root.name}': {string.Join(", ", missing)}. {where} " +
                  "(Turn this off with Tools ▸ SimpleMovements ▸ Inventory ▸ Auto-Build UI When Missing.)", built);
        return true;
    }

    private static GameObject FindPlayer(GameObject root)
    {
        PlayerStatusController ps = root.GetComponentInChildren<PlayerStatusController>(true);
        if (ps != null)
            return ps.gameObject;
        Player p = root.GetComponentInChildren<Player>(true);
        return p != null ? p.gameObject : null;
    }
}
#endif
