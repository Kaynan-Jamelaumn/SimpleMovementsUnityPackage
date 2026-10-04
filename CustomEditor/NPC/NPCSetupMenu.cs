#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click setup for NPCs: create a merchant or a talking NPC (or turn the selected model into one), add the
/// Player Interactor to the players, check every NPC of the scene, and create the project's item categories and
/// default currency in Resources.
/// </summary>
public static class NPCSetupMenu
{
    private const string ResourcesFolder = "Assets/Resources";

    // ------------------------------------------------------------------ creating NPCs
    [MenuItem("GameObject/SimpleMovements/NPC/Merchant NPC", false, 20)]
    private static void CreateMerchantFromHierarchy(MenuCommand command) => CreateNPC(command.context as GameObject, true);

    [MenuItem("GameObject/SimpleMovements/NPC/Talking NPC", false, 21)]
    private static void CreateTalkerFromHierarchy(MenuCommand command) => CreateNPC(command.context as GameObject, false);

    [MenuItem("Tools/SimpleMovements/NPC/Create Merchant NPC (or make the selection one)", false, 0)]
    private static void CreateMerchantFromTools() => CreateNPC(SceneSelection(), true);

    [MenuItem("Tools/SimpleMovements/NPC/Create Talking NPC (or make the selection one)", false, 1)]
    private static void CreateTalkerFromTools() => CreateNPC(SceneSelection(), false);

    private static GameObject SceneSelection()
    {
        GameObject go = Selection.activeGameObject;
        return go != null && !EditorUtility.IsPersistent(go) ? go : null;
    }

    /// <summary>
    /// Makes <paramref name="target"/> (a model in the scene) an NPC - or creates a placeholder NPC in front of the scene
    /// view - with a collider, the NPC component and a Merchant or NPC Dialogue. Undoable.
    /// </summary>
    public static GameObject CreateNPC(GameObject target, bool merchant)
    {
        GameObject go = target;
        if (go == null || EditorUtility.IsPersistent(go))
        {
            go = new GameObject(merchant ? "Merchant" : "Villager");
            Undo.RegisterCreatedObjectUndo(go, "Create NPC");
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body (placeholder - replace with your model)";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
                go.transform.position = view.pivot;
        }

        if (go.GetComponentInChildren<Collider>(true) == null)
        {
            var capsule = Undo.AddComponent<CapsuleCollider>(go);
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.height = 2f;
            capsule.radius = 0.4f;
        }
        NPC npc = go.GetComponent<NPC>();
        if (npc == null)
        {
            npc = Undo.AddComponent<NPC>(go);
            var so = new SerializedObject(npc);
            so.FindProperty("displayName").stringValue = merchant ? "Merchant" : go.name;
            so.FindProperty("title").stringValue = merchant ? "Trader" : "";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (merchant && go.GetComponent<Merchant>() == null)
            Undo.AddComponent<Merchant>(go);
        if (!merchant && go.GetComponent<NPCDialogue>() == null)
            Undo.AddComponent<NPCDialogue>(go);

        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
        OfferInteractor();
        return go;
    }

    /// <summary>Asks to add a Player Interactor when no player in the scene has one.</summary>
    private static void OfferInteractor()
    {
        PlayerStatusController[] players = Object.FindObjectsByType<PlayerStatusController>(FindObjectsInactive.Exclude);
        if (players.Length == 0)
            return;
        foreach (PlayerStatusController p in players)
            if (p.GetComponentInChildren<PlayerInteractor>(true) != null)
                return;
        if (EditorUtility.DisplayDialog("NPC", "No player in the scene has a Player Interactor, which finds NPCs and opens them with the Interact key or a click.\n\nAdd one to the player(s) now?", "Add", "Not now"))
            AddInteractorToPlayers();
    }

    // ------------------------------------------------------------------ players
    [MenuItem("Tools/SimpleMovements/NPC/Add Player Interactor To Players", false, 20)]
    private static void AddInteractorMenu()
    {
        int n = AddInteractorToPlayers();
        EditorUtility.DisplayDialog("NPC", n > 0 ? $"Added a Player Interactor to {n} player(s)." :
            "Every player in the scene (and every selected player prefab) already has a Player Interactor - or there is no player (a Player Status Controller).", "OK");
    }

    /// <summary>Adds a Player Interactor to every player of the scene and every selected player prefab that has none.</summary>
    public static int AddInteractorToPlayers()
    {
        int added = 0;
        foreach (PlayerStatusController p in Object.FindObjectsByType<PlayerStatusController>(FindObjectsInactive.Exclude))
        {
            if (p == null || p.GetComponentInChildren<PlayerInteractor>(true) != null)
                continue;
            Undo.AddComponent<PlayerInteractor>(p.gameObject);
            added++;
        }
        foreach (GameObject asset in Selection.gameObjects)
        {
            if (asset == null || !EditorUtility.IsPersistent(asset) || asset.GetComponentInChildren<PlayerStatusController>(true) == null)
                continue;
            string path = AssetDatabase.GetAssetPath(asset);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                PlayerStatusController status = root.GetComponentInChildren<PlayerStatusController>(true);
                if (status != null && root.GetComponentInChildren<PlayerInteractor>(true) == null)
                {
                    status.gameObject.AddComponent<PlayerInteractor>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    added++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return added;
    }

    // ------------------------------------------------------------------ validation
    [MenuItem("Tools/SimpleMovements/NPC/Validate NPCs In Scene", false, 21)]
    private static void ValidateScene()
    {
        NPC[] npcs = Object.FindObjectsByType<NPC>(FindObjectsInactive.Exclude);
        int problems = 0;
        var errors = new List<string>();
        var warnings = new List<string>();
        foreach (NPC npc in npcs)
        {
            errors.Clear();
            warnings.Clear();
            npc.Validate(errors, warnings);
            CheckInteractorReach(npc, warnings);
            foreach (string e in errors) { Debug.LogError($"[NPC] {npc.name}: {e}", npc); problems++; }
            foreach (string w in warnings) { Debug.LogWarning($"[NPC] {npc.name}: {w}", npc); problems++; }
        }
        bool interactor = false;
        foreach (PlayerStatusController p in Object.FindObjectsByType<PlayerStatusController>(FindObjectsInactive.Exclude))
            interactor |= p.GetComponentInChildren<PlayerInteractor>(true) != null;
        if (npcs.Length > 0 && !interactor)
        {
            Debug.LogWarning("[NPC] No player in the scene has a Player Interactor: only Player.OnInteract can open NPCs (Tools ▸ SimpleMovements ▸ NPC ▸ Add Player Interactor To Players).");
            problems++;
        }
        EditorUtility.DisplayDialog("NPC", npcs.Length == 0 ? "There is no NPC in the scene." :
            problems == 0 ? $"{npcs.Length} NPC(s) checked: no problems." : $"{npcs.Length} NPC(s) checked: {problems} problem(s), listed in the Console.", "OK");
    }

    /// <summary>Warns when an NPC's colliders are on layers the scene's interactors do not search.</summary>
    public static void CheckInteractorReach(NPC npc, List<string> warnings)
    {
        Collider col = npc.GetComponentInChildren<Collider>(true);
        if (col == null)
            return;
        foreach (PlayerInteractor interactor in Object.FindObjectsByType<PlayerInteractor>(FindObjectsInactive.Exclude))
        {
            var so = new SerializedObject(interactor);
            SerializedProperty layers = so.FindProperty("interactableLayers");
            if (layers != null && (layers.intValue & (1 << col.gameObject.layer)) == 0)
            {
                warnings.Add($"Its collider is on the layer '{LayerMask.LayerToName(col.gameObject.layer)}', which the Player Interactor of '{interactor.name}' does not search (Interactable Layers).");
                return;
            }
        }
    }

    // ------------------------------------------------------------------ project assets
    [MenuItem("Tools/SimpleMovements/Project Setup/Create Item Category Database (Resources)")]
    public static ItemCategoryDatabase CreateCategoryDatabase()
    {
        string path = $"{ResourcesFolder}/{ItemCategoryDatabase.ResourcesName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ItemCategoryDatabase>(path);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return existing;
        }
        EnsureResourcesFolder();
        var db = ScriptableObject.CreateInstance<ItemCategoryDatabase>();
        AssetDatabase.CreateAsset(db, path);
        FillWithDefaults(db);
        Selection.activeObject = db;
        EditorGUIUtility.PingObject(db);
        Debug.Log($"[Categories] Created {path} with the standard categories (Weapons, Armor, Accessories, Consumables, Materials, Miscellaneous and their subcategories). Edit them in its inspector.", db);
        return db;
    }

    /// <summary>Adds the standard categories to <paramref name="db"/> as sub-assets (kept in the same file).</summary>
    public static void FillWithDefaults(ItemCategoryDatabase db)
    {
        List<ItemCategory> list = ItemCategoryDatabase.BuildDefaultTree(() => ScriptableObject.CreateInstance<ItemCategory>());
        foreach (ItemCategory c in list)
            AssetDatabase.AddObjectToAsset(c, db);
        db.SetCategories(list, list.Find(c => c.DisplayName == "Miscellaneous"));
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/SimpleMovements/Project Setup/Create Default Currency (Resources)")]
    public static CurrencyDefinition CreateDefaultCurrency()
    {
        string path = $"{ResourcesFolder}/{CurrencyDefinition.ResourcesName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<CurrencyDefinition>(path);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return existing;
        }
        EnsureResourcesFolder();
        var currency = ScriptableObject.CreateInstance<CurrencyDefinition>();
        AssetDatabase.CreateAsset(currency, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = currency;
        EditorGUIUtility.PingObject(currency);
        return currency;
    }

    private static void EnsureResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");
    }

    // ------------------------------------------------------------------ items
    /// <summary>Every item asset in <paramref name="folder"/> (and its subfolders).</summary>
    public static List<ItemSO> ItemsIn(string folder)
    {
        var list = new List<ItemSO>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemSO", new[] { folder }))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null && !list.Contains(item))
                list.Add(item);
        }
        return list;
    }

    /// <summary>Every item asset of the project.</summary>
    public static List<ItemSO> AllItems() => ItemsIn("Assets");

    [MenuItem("Tools/SimpleMovements/NPC/Help", false, 40)]
    private static void Help()
    {
        EditorUtility.DisplayDialog("NPC & Merchants",
            "1. GameObject ▸ SimpleMovements ▸ NPC ▸ Merchant NPC (or select your model and use the Tools menu).\n" +
            "2. In the Merchant: add Goods (Add Selected Items / Add Items From Folder), set prices (Pricing), currency and categories.\n" +
            "3. The player needs a Player Interactor (offered automatically). It uses the 'Interact' action (created with E when missing) and clicks.\n" +
            "4. Optional: Project Setup ▸ Create Item Category Database / Create Default Currency to edit categories and money.\n" +
            "5. Optional: a Merchant UI Skin (Create ▸ SimpleMovements ▸ NPC) for your own prefabs, sprites, fonts and colours.\n\n" +
            "Full guide: Documentation/NPC/README.md", "OK");
    }
}
#endif
