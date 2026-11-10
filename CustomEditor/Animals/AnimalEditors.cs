#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleMovements.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Menus of animal husbandry (Tools ▸ SimpleMovements ▸ Animals): pens (on the selected property's land, like its garden,
/// or on their own), the city's veterinarian (one NPC that is also a merchant of the keeper's goods) and the goods of
/// animal husbandry for any merchant, the project's Animal Database, editable species and food assets (and bringing
/// species assets made before Phase 5 up to date), the network prefab of a butchered animal's carcass, validation, a
/// keeper's starter kit and care kit in play, and the documentation. Pens on a house's land are also added from the
/// Property inspector (+ Pasture, + Coop, + Sty).
/// </summary>
public static class AnimalEditors
{
    private const string Menu = SMMenu.Root + "Animals/";

    [MenuItem(SMMenu.Root + "Project Setup/Create Animal Database (Resources)", false, SMMenu.ProjectSetup + 13)]
    private static void CreateDatabaseSetupMenu() => ProjectDatabase(true);

    [MenuItem(Menu + "Create Pasture On Selected Property", false, SMMenu.Animals)]
    private static void PastureMenu() => AddToSelected(AnimalHabitatKind.Pasture);

    [MenuItem(Menu + "Create Coop On Selected Property", false, SMMenu.Animals + 1)]
    private static void CoopMenu() => AddToSelected(AnimalHabitatKind.Coop);

    [MenuItem(Menu + "Create Sty On Selected Property", false, SMMenu.Animals + 2)]
    private static void StyMenu() => AddToSelected(AnimalHabitatKind.Sty);

    [MenuItem(Menu + "Create Pasture On Selected Property", true)]
    [MenuItem(Menu + "Create Coop On Selected Property", true)]
    [MenuItem(Menu + "Create Sty On Selected Property", true)]
    private static bool AddValidate() => SelectedProperty() != null;

    [MenuItem(Menu + "Create Standalone Pen", false, SMMenu.Animals + 3)]
    private static void StandaloneMenu()
    {
        var go = new GameObject("Animal Pen");
        if (SceneView.lastActiveSceneView != null) go.transform.position = SceneView.lastActiveSceneView.pivot;
        AnimalPen pen = go.AddComponent<AnimalPen>();
        pen.outline = HousingGeometry.Rectangle(Vector2.zero, new Vector2(6f, 4f));
        Undo.RegisterCreatedObjectUndo(go, "Create Animal Pen");
        Selection.activeGameObject = go;
        Debug.Log("[Animals] A public pen (on no property: anyone may tend it). For a house's pen use the Property inspector's + Pasture / + Coop / + Sty.", go);
    }

    [MenuItem(Menu + "Create Veterinarian In City", false, SMMenu.Animals + 4)]
    private static void VeterinarianMenu()
    {
        AnimalVeterinarian existing = FirstVeterinarian();
        if (existing != null)
        {
            int choice = EditorUtility.DisplayDialogComplex("Animals",
                $"There is a veterinarian already: '{existing.name}' (in '{existing.gameObject.scene.name}'). The first one enabled is the city's: another would stand by, never sent for.",
                "Select It", "Cancel", "Create Another");
            if (choice == 1) return;
            if (choice == 0)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }
        }
        CreateVeterinarian();
    }

    [MenuItem(Menu + "Add Animal Goods To Selected Merchant", false, SMMenu.Animals + 5)]
    private static void GoodsMenu()
    {
        Merchant merchant = SelectedMerchant();
        if (merchant == null) return;
        if (merchant.GetComponent<AnimalMerchantStock>() != null)
        {
            Selection.activeGameObject = merchant.gameObject;
            EditorUtility.DisplayDialog("Animals", $"'{merchant.name}' sells the goods of animal husbandry already: what it sells is set in its Animal Goods component.", "OK");
            return;
        }
        AddGoods(merchant.gameObject);
        Selection.activeGameObject = merchant.gameObject;
        Debug.Log($"[Animals] '{merchant.name}' sells the goods of animal husbandry now: care tools, shears and a milking pail, straw and soap, feed, young animals and medicine, added to its shop " +
                  "as the scene wakes (Animal Goods: turn off what it should not sell).", merchant);
    }

    [MenuItem(Menu + "Add Animal Goods To Selected Merchant", true)]
    private static bool GoodsValidate() => SelectedMerchant() != null;

    [MenuItem(Menu + "Create Animal Database", false, SMMenu.Animals + 20)]
    private static void CreateDatabaseMenu() => ProjectDatabase(true);

    [MenuItem(Menu + "Create Species Assets", false, SMMenu.Animals + 21)]
    private static void SpeciesMenu()
    {
        AnimalDatabase db = ProjectDatabase(false);
        string folder = SMAssets.FolderPath("Animals/Species");
        EnsureFolder(folder);
        int made = 0;
        foreach (AnimalSpeciesData s in AnimalDefaults.Species())
        {
            if (db.species.Exists(x => x != null && x.Id == s.id)) continue;
            var asset = ScriptableObject.CreateInstance<AnimalSpecies>();
            asset.data = s;
            asset.name = s.DisplayName;
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{s.DisplayName}.asset"));
            db.species.Add(asset);
            made++;
        }
        Finish(db);
        int old = 0;
        foreach (AnimalSpecies s in db.species)
            if (s != null && s.data != null && s.data.dataVersion < AnimalDatabase.SpeciesDataVersion) old++;
        string stale = old > 0 ? $"\n\n{old} species asset(s) of the database were made before Phase 5: Animals ▸ Update Species Assets To Phase 5 gives them what they lack (care, products)." : "";
        EditorUtility.DisplayDialog("Animals", $"{made} species asset(s) in {folder}, listed in the Animal Database. Edit them (or copy one for a new species): " +
            "an asset replaces the built-in species with its id. Give one a Body Prefab (a model) to show its animals with it." + stale, "OK");
    }

    [MenuItem(Menu + "Create Food Assets", false, SMMenu.Animals + 22)]
    private static void FoodsMenu()
    {
        AnimalDatabase db = ProjectDatabase(false);
        string folder = SMAssets.FolderPath("Animals/Foods");
        EnsureFolder(folder);
        int made = 0;
        foreach (AnimalFoodData f in AnimalDefaults.Foods())
        {
            if (db.foods.Exists(x => x != null && x.Id == f.id)) continue;
            var asset = ScriptableObject.CreateInstance<AnimalFood>();
            asset.data = f;
            asset.name = f.DisplayName;
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{f.DisplayName}.asset"));
            db.foods.Add(asset);
            made++;
        }
        Finish(db);
        EditorUtility.DisplayDialog("Animals", $"{made} food asset(s) in {folder}, listed in the Animal Database. What a food is to each species is that species' diet.", "OK");
    }

    [MenuItem(Menu + "Update Species Assets To Phase 5", false, SMMenu.Animals + 23)]
    private static void UpdateSpeciesMenu()
    {
        HashSet<string> builtIn = BuiltInSpeciesIds();
        var update = new List<AnimalSpecies>();
        var alone = new List<AnimalSpecies>();
        foreach (AnimalSpecies s in SpeciesAssets())
        {
            if (s.data == null || s.data.dataVersion >= AnimalDatabase.SpeciesDataVersion) continue;
            if (!string.IsNullOrWhiteSpace(s.data.id) && builtIn.Contains(s.data.id.Trim())) update.Add(s);
            else alone.Add(s);
        }
        if (update.Count == 0 && alone.Count == 0)
        {
            EditorUtility.DisplayDialog("Animals", $"Every species asset of the project is up to date (Data Version {AnimalDatabase.SpeciesDataVersion}).", "OK");
            return;
        }
        string aloneText = alone.Count > 0
            ? $"\n\n{alone.Count} of them have no built-in species of their id ({Names(alone)}): give them what they lack yourself (care, susceptibility and anatomy before Data Version 2; products before 3), then set their Data Version to {AnimalDatabase.SpeciesDataVersion}."
            : "";
        if (update.Count == 0)
        {
            EditorUtility.DisplayDialog("Animals", "No species asset made before Phase 5 has a built-in species of its id." + aloneText, "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Animals",
                $"Write what the built-in species of their id has and they lack into {update.Count} species asset(s) made before Phase 5 ({Names(update)})? " +
                "Below Data Version 2 that is the care, susceptibility and anatomy (care you set in them yourself is replaced); below 3 the products (an asset that lists its own keeps them) " +
                $"and the seniors' production. For an asset you made complete already, set its Data Version to {AnimalDatabase.SpeciesDataVersion} instead." + aloneText,
                "Update", "Cancel"))
            return;
        int done = UpdateSpeciesAssets(update);
        EditorUtility.DisplayDialog("Animals", $"{done} species asset(s) updated to Phase 5 (undoable)." + aloneText, "OK");
    }

    [MenuItem(Menu + "Create Animal Carcass Prefab (Network)", false, SMMenu.Animals + 24)]
    private static void CarcassPrefabMenu() => GameNet.EditorTools.NetworkSetupMenu.CreateAnimalCarcassPrefab();

    [MenuItem(Menu + "Give Me A Keeper's Starter Kit (Play Mode)", false, SMMenu.Animals + 30)]
    private static void StarterKitMenu() => GiveKit(false, true);

    [MenuItem(Menu + "Give Me A Keeper's Starter Kit (Play Mode)", true)]
    private static bool StarterKitValidate() => Application.isPlaying;

    [MenuItem(Menu + "Give Me A Care Kit (Play Mode)", false, SMMenu.Animals + 31)]
    private static void CareKitMenu() => GiveKit(true, true);

    [MenuItem(Menu + "Give Me A Care Kit (Play Mode)", true)]
    private static bool CareKitValidate() => Application.isPlaying;

    [MenuItem(Menu + "Validate Animals", false, SMMenu.Animals + 40)]
    private static void ValidateMenu()
    {
        var issues = new List<ValidationIssue>();
        AnimalValidation.CheckProject(issues);
        AnimalValidation.CheckWorld(issues);
        AnimalValidation.CheckCarcassPrefab(issues);
        foreach (GameObject root in OpenSceneRoots()) AnimalValidation.CheckRoot(root, issues);
        foreach (ValidationIssue i in issues) i.Log();
        AnimalDatabase db = AnimalDatabase.Instance;
        Debug.Log($"[Animals] {db.AllSpecies.Count} species, {db.AllFoods.Count} foods, {db.AllConditions.Count} illnesses, {db.AllTreatments.Count} medicines, " +
                  $"{db.AllTools.Count} tools and supplies, {Pens().Count} pen(s) open: " +
                  $"{issues.Count(i => i.severity == IssueSeverity.Error)} error(s), {issues.Count(i => i.severity == IssueSeverity.Warning)} warning(s).");
    }

    [MenuItem(Menu + "Help", false, SMMenu.Animals + 60)]
    private static void HelpMenu()
    {
        foreach (string g in AssetDatabase.FindAssets("README"))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (path.Replace('\\', '/').Contains("Documentation/Animals/"))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(path);
                EditorGUIUtility.PingObject(Selection.activeObject);
                return;
            }
        }
        EditorUtility.DisplayDialog("Animals", "See Documentation/Animals/README.md.", "OK");
    }

    // ------------------------------------------------------------------ helpers
    /// <summary>The project's Animal Database (Resources/SimpleMovements/Settings), with the default rules when made.</summary>
    public static AnimalDatabase ProjectDatabase(bool select)
    {
        AnimalDatabase db = SMAssetsEditor.FindOrCreate<AnimalDatabase>(SMAssets.Settings, AnimalDatabase.ResourcesName, d => d.rules = AnimalRules.Defaults(), select);
        AnimalDatabase.Override(null);
        return db;
    }

    private static void Finish(AnimalDatabase db)
    {
        db.MarkChanged();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AnimalDatabase.Override(null);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static Property SelectedProperty()
    {
        GameObject go = Selection.activeGameObject;
        return go != null && !EditorUtility.IsPersistent(go) ? go.GetComponentInParent<Property>(true) : null;
    }

    private static void AddToSelected(AnimalHabitatKind kind)
    {
        Property p = SelectedProperty();
        if (p == null) return;
        AnimalPen pen = HousingEditors.AddPen(p, kind);
        if (pen != null) Selection.activeGameObject = pen.gameObject;
    }

    public static IEnumerable<GameObject> OpenSceneRoots()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            foreach (GameObject root in s.GetRootGameObjects()) yield return root;
        }
    }

    public static List<AnimalPen> Pens()
    {
        var list = new List<AnimalPen>();
        foreach (GameObject root in OpenSceneRoots()) list.AddRange(root.GetComponentsInChildren<AnimalPen>(true));
        return list;
    }

    /// <summary>Is the scene of this name open (loaded) in the editor?</summary>
    public static bool SceneOpen(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        Scene s = SceneManager.GetSceneByName(sceneName);
        return s.IsValid() && s.isLoaded;
    }

    /// <summary>The property whose land a pen lies on, other than its own (an error: it would sit on someone else's plot).</summary>
    public static Property ForeignLand(AnimalPen pen, out float shared)
    {
        shared = 0f;
        if (pen == null || pen.outline == null || pen.outline.Count < 3) return null;
        Property own = pen.GetComponentInParent<Property>(true);
        foreach (GameObject root in OpenSceneRoots())
            foreach (Property p in root.GetComponentsInChildren<Property>(true))
            {
                if (p == own || p.land == null || p.land.Count < 3) continue;
                if (p.pens != null && p.pens.Contains(pen)) continue;
                List<Vector2> local = HousingEditors.Convert(pen.outline, pen.transform, p.transform);
                float s = HousingGeometry.SharedArea(p.land, local);
                if (s > HousingGeometry.DefaultOverlapArea)
                {
                    shared = s;
                    return p;
                }
            }
        return null;
    }

    public static bool NewId(AnimalPen pen)
    {
        if (pen == null) return false;
        var so = new SerializedObject(pen);
        SerializedProperty id = so.FindProperty("penId");
        if (id == null) return false;
        id.stringValue = AnimalPen.NewId();
        so.ApplyModifiedProperties();
        return true;
    }

    // ------------------------------------------------------------------ kits
    /// <summary>
    /// Gives the local player a keeper's starter kit (<paramref name="care"/> false: a crate of each species and feed) or a
    /// care kit (the care tools, straw and soap, medicine) - in play, where the rules run. <paramref name="dialogs"/>: say
    /// in a dialog why it cannot be done (from a menu), else in the Console (from an inspector).
    /// </summary>
    public static void GiveKit(bool care, bool dialogs)
    {
        if (!Application.isPlaying) return;
        string why = null;
        CombatEntity me = null;
        if (!AnimalDirector.RulesHere) why = "Only where the rules run (offline, the host) can items be given.";
        else if ((me = AnimalDirector.LocalPlayer()) == null) why = "There is no local player here to give it to.";
        if (why != null)
        {
            if (dialogs) EditorUtility.DisplayDialog("Animals", why, "OK");
            else Debug.LogWarning("[Animals] " + why);
            return;
        }
        if (care) AnimalToolkit.GiveCareKit(me);
        else AnimalToolkit.GiveStarterKit(me);
    }

    // ------------------------------------------------------------------ the veterinarian and animal goods
    /// <summary>
    /// One NPC that is both the city's veterinarian (<see cref="AnimalVeterinarian"/>: keepers send for the vet from a pen's
    /// window) and a merchant of the keeper's goods (a <see cref="Merchant"/> with <see cref="AnimalMerchantStock"/>: care
    /// tools, straw and soap, feed, young animals, medicine) - at the Scene view's pivot, on the ground, in the City scene
    /// when it is open. Undoable; selected.
    /// </summary>
    public static AnimalVeterinarian CreateVeterinarian()
    {
        Vector3 at = HousingEditors.GroundAt(Pivot(), null);
        GameObject go = NPCSetupMenu.CreateNPC(null, true);
        go.name = "Veterinarian";
        NameNpc(go, "Veterinarian", "Animal doctor · keeper's goods");
        AnimalVeterinarian vet = go.GetComponent<AnimalVeterinarian>();
        if (vet == null) vet = Undo.AddComponent<AnimalVeterinarian>(go);
        SetOption(vet, "Veterinarian", 0);
        AddGoods(go);
        Merchant shop = go.GetComponent<Merchant>();
        if (shop != null)
        {
            // Its shop after its own option, under a name of its own (the NPC is new: its shop's label is set, not left to Reset).
            SetOption(shop, "Trade", 1);
            var so = new SerializedObject(shop);
            SerializedProperty name = so.FindProperty("shopName");
            if (name != null && string.IsNullOrWhiteSpace(name.stringValue)) name.stringValue = "Keeper's goods";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        go.transform.position = at;
        InCity(go);
        Selection.activeGameObject = go;
        Debug.Log("[Animals] Made the city's veterinarian, who also sells the keeper's goods (care tools, straw and soap, feed, young animals and medicine: " +
                  "its Animal Goods). Keepers send for the vet from a pen's window (an animal's Health tab), for the fee shown there. Replace the capsule with your model.", go);
        return vet;
    }

    /// <summary>
    /// Makes an NPC sell the goods of animal husbandry: a <see cref="Merchant"/> (added, as its second option, when it has
    /// none) with an <see cref="AnimalMerchantStock"/> (everything on). Undoable. A component it has already is kept as it is.
    /// </summary>
    public static AnimalMerchantStock AddGoods(GameObject go)
    {
        if (go == null) return null;
        Merchant shop = go.GetComponent<Merchant>();
        if (shop == null)
        {
            shop = Undo.AddComponent<Merchant>(go);
            SetOption(shop, "Trade", 1);
        }
        AnimalMerchantStock goods = go.GetComponent<AnimalMerchantStock>();
        if (goods == null) goods = Undo.AddComponent<AnimalMerchantStock>(go);
        return goods;
    }

    /// <summary>The validator's fix: the city's veterinarian (with the keeper's goods), unless one is in the open scenes. True: one was made.</summary>
    public static bool EnsureVeterinarian()
    {
        foreach (AnimalVeterinarian v in Veterinarians())
            if (Serves(v)) return false;
        return CreateVeterinarian() != null;
    }

    /// <summary>
    /// The validator's fix: a merchant selling care tools, unless one in the open scenes does - the veterinarian's own shop
    /// (its goods are added, or their Tools turned on), else a new veterinarian with the goods. True: something changed.
    /// </summary>
    public static bool EnsureAnimalGoods()
    {
        foreach (AnimalMerchantStock g in AnimalGoods())
            if (Sells(g) && g.tools) return false;
        foreach (AnimalVeterinarian v in Veterinarians())
        {
            if (!Serves(v)) continue;
            AnimalMerchantStock had = v.GetComponent<AnimalMerchantStock>();
            if (had == null)
            {
                AddGoods(v.gameObject);
                return true;
            }
            Undo.RecordObject(had, "Sell Care Tools");
            had.tools = true;
            Merchant shop = v.GetComponent<Merchant>();
            if (shop != null && !shop.enabled)
            {
                Undo.RecordObject(shop, "Sell Care Tools");
                shop.enabled = true;
            }
            EditorUtility.SetDirty(had);
            return true;
        }
        return CreateVeterinarian() != null;
    }

    /// <summary>Every veterinarian of the open scenes (inactive ones too).</summary>
    public static List<AnimalVeterinarian> Veterinarians()
    {
        var list = new List<AnimalVeterinarian>();
        foreach (GameObject root in OpenSceneRoots()) list.AddRange(root.GetComponentsInChildren<AnimalVeterinarian>(true));
        return list;
    }

    /// <summary>Every merchant's animal goods of the open scenes (inactive ones too).</summary>
    public static List<AnimalMerchantStock> AnimalGoods()
    {
        var list = new List<AnimalMerchantStock>();
        foreach (GameObject root in OpenSceneRoots()) list.AddRange(root.GetComponentsInChildren<AnimalMerchantStock>(true));
        return list;
    }

    /// <summary>Does this NPC option work in play (enabled, on an active object)?</summary>
    public static bool Serves(Behaviour b) => b != null && b.enabled && b.gameObject.activeInHierarchy;

    /// <summary>Do these goods reach a shop in play (an active object whose Merchant is enabled)?</summary>
    public static bool Sells(AnimalMerchantStock g)
    {
        if (g == null || !g.gameObject.activeInHierarchy) return false;
        Merchant shop = g.GetComponent<Merchant>();
        return shop != null && shop.enabled;
    }

    private static AnimalVeterinarian FirstVeterinarian()
    {
        AnimalVeterinarian any = null;
        foreach (AnimalVeterinarian v in Veterinarians())
        {
            if (Serves(v)) return v;
            if (any == null) any = v;
        }
        return any;
    }

    private static Merchant SelectedMerchant()
    {
        GameObject go = Selection.activeGameObject;
        return go != null && !EditorUtility.IsPersistent(go) ? go.GetComponentInParent<Merchant>(true) : null;
    }

    private static Vector3 Pivot() => SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;

    private static void NameNpc(GameObject npcObject, string displayName, string title)
    {
        NPC npc = npcObject.GetComponent<NPC>();
        if (npc == null) return;
        var so = new SerializedObject(npc);
        SerializedProperty n = so.FindProperty("displayName"), t = so.FindProperty("title");
        if (n != null) n.stringValue = displayName;
        if (t != null) t.stringValue = title;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>An NPC option's label (null: kept) and its place among the NPC's options.</summary>
    private static void SetOption(NPCBehaviour option, string label, int order)
    {
        if (option == null) return;
        var so = new SerializedObject(option);
        SerializedProperty l = so.FindProperty("optionLabel"), o = so.FindProperty("optionOrder");
        if (l != null && !string.IsNullOrEmpty(label)) l.stringValue = label;
        if (o != null) o.intValue = order;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Moves a new object into the City scene when that is open (and says so when it is not).</summary>
    private static void InCity(GameObject go)
    {
        if (go == null || go.transform.parent != null) return;
        string city = AgricultureEditors.CitySceneName();
        if (string.IsNullOrEmpty(city)) return;
        Scene scene = SceneManager.GetSceneByName(city);
        if (scene.IsValid() && scene.isLoaded)
        {
            if (go.scene != scene) Undo.MoveGameObjectToScene(go, scene, "Move To The City");
            return;
        }
        Debug.Log($"[Animals] '{go.name}' is in '{go.scene.name}', not the City scene ('{city}'). The veterinarian belongs in the permanent city: " +
                  "the hub world's other scenes are made again every cycle.", go);
    }

    // ------------------------------------------------------------------ species assets made before Phase 5
    /// <summary>The ids of the built-in species (any case).</summary>
    public static HashSet<string> BuiltInSpeciesIds()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (AnimalSpeciesData s in AnimalDefaults.Species())
            if (s != null && !string.IsNullOrWhiteSpace(s.id)) ids.Add(s.id.Trim());
        return ids;
    }

    /// <summary>Every species asset of the project.</summary>
    public static List<AnimalSpecies> SpeciesAssets()
    {
        var list = new List<AnimalSpecies>();
        foreach (string guid in AssetDatabase.FindAssets("t:AnimalSpecies"))
        {
            var s = AssetDatabase.LoadAssetAtPath<AnimalSpecies>(AssetDatabase.GUIDToAssetPath(guid));
            if (s != null && !list.Contains(s)) list.Add(s);
        }
        return list;
    }

    /// <summary>
    /// Writes into species assets made before Phase 5 (Data Version below 5) what the built-in species of their id has and
    /// they lack (<see cref="AnimalDatabase.UpdateFromBuiltIn"/>), and their new Data Version. Undoable.
    /// Assets with no built-in species of their id are left as they are. Returns how many were updated.
    /// </summary>
    public static int UpdateSpeciesAssets(IList<AnimalSpecies> assets)
    {
        if (assets == null || assets.Count == 0) return 0;
        var record = new List<Object>();
        foreach (AnimalSpecies s in assets)
            if (s != null) record.Add(s);
        if (record.Count == 0) return 0;
        Undo.RecordObjects(record.ToArray(), "Update Species Assets To Phase 5");
        int updated = 0;
        foreach (AnimalSpecies s in assets)
        {
            if (s == null || s.data == null || s.data.dataVersion >= AnimalDatabase.SpeciesDataVersion) continue;
            if (!AnimalDatabase.UpdateFromBuiltIn(s.data)) continue;
            EditorUtility.SetDirty(s);
            updated++;
        }
        if (updated > 0)
        {
            AssetDatabase.SaveAssets();
            // The database compiles its content once: make it read the assets again.
            AnimalDatabase db = SMAssets.Load<AnimalDatabase>(SMAssets.Settings, AnimalDatabase.ResourcesName);
            if (db != null) db.MarkChanged();
            if (AnimalDatabase.HasInstance) AnimalDatabase.Instance.MarkChanged();
        }
        return updated;
    }

    /// <summary>One species asset brought up to Phase 5 (the validator's fix). True: it was.</summary>
    public static bool UpdateSpeciesAsset(AnimalSpecies asset) => asset != null && UpdateSpeciesAssets(new List<AnimalSpecies> { asset }) > 0;

    private static string Names(List<AnimalSpecies> assets)
    {
        var names = new List<string>();
        for (int i = 0; i < assets.Count && i < 4; i++)
            if (assets[i] != null) names.Add(assets[i].name);
        string text = string.Join(", ", names);
        return assets.Count > 4 ? text + $" and {assets.Count - 4} more" : text;
    }
}

/// <summary>
/// Animal husbandry's part of the Project Validator: the Animal Database (rules and welfare, species - those made before
/// Phase 5 reported once each, with a fix that updates them - foods, illnesses, medicines and the Alchemy outcomes they
/// name, care tools, item bindings, and how they fit together), species assets made before Phase 5 it does not list, every pen (its outline, kind, shelter and stations, a unique id, scale 1, on its
/// own property's land and nobody else's), the city's veterinarian and a merchant of care tools where animals are kept in
/// the open scenes, Alchemy stations there whose own database lacks the veterinary medicines, and with Fish-Net the network
/// prefab of a butchered animal's carcass.
/// </summary>
[InitializeOnLoad]
public static class AnimalValidation
{
    private const string Category = "Animals";

    static AnimalValidation()
    {
        ProjectValidator.ValidateRoot += CheckRoot;
        ProjectValidator.ValidateProject += (checks, issues) =>
        {
            if ((checks & ValidationChecks.ProjectAssets) != 0) CheckProject(issues);
            if ((checks & (ValidationChecks.ProjectAssets | ValidationChecks.GameSetup)) != 0) CheckWorld(issues);
            if ((checks & (ValidationChecks.ProjectAssets | ValidationChecks.Network)) != 0) CheckCarcassPrefab(issues);
        };
    }

    public static void CheckProject(List<ValidationIssue> issues)
    {
        AnimalDatabase db = SMAssets.Load<AnimalDatabase>(SMAssets.Settings, AnimalDatabase.ResourcesName);
        if (db == null)
        {
            if (AnimalEditors.Pens().Count > 0)
                issues.Add(new ValidationIssue(IssueSeverity.Info, Category, "No Animal Database: the pens use the built-in rules, species and foods.")
                    .WithFix("Create", () => AnimalEditors.ProjectDatabase(false) != null));
        }
        else
        {
            // The veterinary medicines are Alchemy outcomes animal husbandry adds (in play, before the first scene): add them
            // now, so the medicines that name them are not reported as never brewable.
            try
            {
                AnimalAlchemy.EnsureInstalled();
            }
            catch (Exception ex)
            {
                issues.Add(new ValidationIssue(IssueSeverity.Warning, Category,
                    $"The veterinary medicines could not be added to Alchemy ({ex.Message}): the medicines that name them may be reported as never brewable.", db));
            }
            db.MarkChanged();
            var errors = new List<string>();
            var warnings = new List<string>();
            db.Validate(errors, warnings);
            int first = issues.Count;
            foreach (string e in errors) issues.Add(new ValidationIssue(IssueSeverity.Error, Category, e, db));
            foreach (string w in warnings) issues.Add(new ValidationIssue(IssueSeverity.Warning, Category, w, db));
            OldSpecies(db, issues, first);
        }
        UnlistedOldSpecies(db, issues);
        var seen = new Dictionary<string, AnimalPen>();
        foreach (AnimalPen p in AnimalEditors.Pens())
        {
            if (seen.TryGetValue(p.PenId, out AnimalPen other))
            {
                AnimalPen copy = p;
                issues.Add(new ValidationIssue(IssueSeverity.Error, Category,
                        $"'{p.name}' has the same Pen Id as '{other.name}' (a duplicated object?): their animals would be one herd in the save.", p, ValidationUtility.PathOf(p.transform))
                    .WithFix("New Id", () => AnimalEditors.NewId(copy)));
            }
            else seen[p.PenId] = p;
        }
    }

    /// <summary>
    /// Each species asset of the database made before Phase 5 is reported once: the database's warning about it (or a new
    /// one, should the database give none) points at the asset - with a fix that writes the built-in care, susceptibility
    /// and anatomy into it (Animals ▸ Update Species Assets To Phase 5) when a built-in species has its id. An asset no
    /// built-in species can fill in has no care of its own yet: the warnings that only say so ("it takes no care actions")
    /// are left out, as its one warning tells what to give it.
    /// </summary>
    private static void OldSpecies(AnimalDatabase db, List<ValidationIssue> issues, int first)
    {
        if (db.species == null) return;
        HashSet<string> builtIn = AnimalEditors.BuiltInSpeciesIds();
        int end = issues.Count;
        var taken = new bool[Math.Max(0, end - first)];
        var drop = new bool[taken.Length];
        var done = new HashSet<AnimalSpecies>();
        var prefixes = new List<string>();
        foreach (AnimalSpecies s in db.species)
        {
            if (s == null || s.data == null || s.data.dataVersion >= AnimalDatabase.SpeciesDataVersion || !done.Add(s)) continue;
            bool twin = !string.IsNullOrWhiteSpace(s.data.id) && builtIn.Contains(s.data.id.Trim());
            // The database's own words for it (see AnimalDatabase.Validate), with or without a built-in species of its id.
            string prefix = $"{s.name}: made before Phase 5 (Data Version {s.data.dataVersion})";
            prefixes.Add(prefix);
            ValidationIssue issue = null;
            for (int i = first; i < end; i++)
            {
                if (taken[i - first] || issues[i].message == null || !issues[i].message.StartsWith(prefix, StringComparison.Ordinal)) continue;
                taken[i - first] = true;
                issue = issues[i];
                break;
            }
            if (issue == null)
            {
                issue = new ValidationIssue(IssueSeverity.Warning, Category, OldSpeciesText(s, twin));
                issues.Add(issue);
            }
            AnimalSpecies asset = s;
            issue.context = asset;
            if (twin)
            {
                issue.WithFix("Update", () => AnimalEditors.UpdateSpeciesAsset(asset));
                continue;
            }
            // No built-in species fills it in: its own (empty) care is what the database checked.
            List<string> noise = CareWarnings(s);
            for (int i = first; i < end; i++)
                if (!drop[i - first] && issues[i].severity == IssueSeverity.Warning && issues[i].message != null && noise.Contains(issues[i].message))
                    drop[i - first] = true;
        }
        // An asset listed twice is warned about twice by the database: once is enough.
        for (int i = first; i < end; i++)
            if (!taken[i - first] && issues[i].message != null)
                foreach (string prefix in prefixes)
                    if (issues[i].message.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        drop[i - first] = true;
                        break;
                    }
        for (int i = end - 1; i >= first; i--)
            if (drop[i - first]) issues.RemoveAt(i);
    }

    /// <summary>
    /// Species assets of the project made before Phase 5 that the Animal Database does not list (so nothing uses them yet):
    /// one note each, with the fix that updates it when a built-in species has its id.
    /// </summary>
    private static void UnlistedOldSpecies(AnimalDatabase db, List<ValidationIssue> issues)
    {
        HashSet<string> builtIn = null;
        foreach (AnimalSpecies s in AnimalEditors.SpeciesAssets())
        {
            if (s.data == null || s.data.dataVersion >= AnimalDatabase.SpeciesDataVersion) continue;
            if (db != null && db.species != null && db.species.Contains(s)) continue;
            builtIn ??= AnimalEditors.BuiltInSpeciesIds();
            bool twin = !string.IsNullOrWhiteSpace(s.data.id) && builtIn.Contains(s.data.id.Trim());
            var issue = new ValidationIssue(IssueSeverity.Info, Category,
                OldSpeciesText(s, twin) + " It is not listed in the Animal Database (nothing uses it yet).", s, AssetDatabase.GetAssetPath(s));
            AnimalSpecies asset = s;
            if (twin) issue.WithFix("Update", () => AnimalEditors.UpdateSpeciesAsset(asset));
            issues.Add(issue);
        }
    }

    /// <summary>What is said of a species asset made before Phase 5 (in the database's words, see <see cref="AnimalDatabase.Validate"/>).</summary>
    private static string OldSpeciesText(AnimalSpecies s, bool twin) =>
        twin
            ? $"{s.name}: made before Phase 5 (Data Version {s.data.dataVersion}): its {AnimalDatabase.OldDataWhat(s.data.dataVersion)} are taken from the built-in '{s.data.id.Trim()}' " +
              "until it is updated (Animals ▸ Update Species Assets To Phase 5)."
            : $"{s.name}: made before Phase 5 (Data Version {s.data.dataVersion}) and no built-in species has its id: give it its {AnimalDatabase.OldDataWhat(s.data.dataVersion)}, " +
              $"then set its Data Version to {AnimalDatabase.SpeciesDataVersion}.";

    /// <summary>
    /// The warnings the database gives about a species asset's care, as <see cref="AnimalSpeciesData.Validate"/> words them
    /// ("Cow: 'cow': it takes no care actions."): those an asset made before Phase 5 gives only because it has no care yet.
    /// </summary>
    private static List<string> CareWarnings(AnimalSpecies s)
    {
        var list = new List<string>();
        AnimalSpeciesData d = s != null ? s.data : null;
        if (d == null) return list;
        string who = string.IsNullOrEmpty(d.id) ? "(a species without an id)" : $"'{d.id}'";
        var care = new List<string>();
        if (d.care == null) care.Add($"{who}: no care data (the defaults are used).");
        else
        {
            d.care.Validate(who, null, care);
            if (d.care.actions == null || d.care.actions.Count == 0) care.Add($"{who}: it takes no care actions.");
        }
        foreach (string w in care) list.Add($"{s.name}: {w}");
        return list;
    }

    /// <summary>
    /// Where animals are kept in the open scenes: the city's veterinarian (without one nobody can be sent for, and a keeper's
    /// medicine cures every illness), a merchant selling the care tools (without them brushing, hoof care and mucking out
    /// cannot be done), medicine and supplies - in the City scene when the hub world names one. While that scene is closed,
    /// what is missing is only noted (it may be there). Also the Alchemy stations that cannot make the veterinary medicines
    /// (<see cref="CheckStations"/>).
    /// </summary>
    public static void CheckWorld(List<ValidationIssue> issues)
    {
        int pens = AnimalEditors.Pens().Count;
        CheckStations(issues, pens);
        if (pens == 0) return;
        string city = AgricultureEditors.CitySceneName();
        bool cityClosed = !string.IsNullOrEmpty(city) && !AnimalEditors.SceneOpen(city);
        string closed = cityClosed ? $" (the City scene '{city}' is not open: it may be there)" : "";
        IssueSeverity missing = cityClosed ? IssueSeverity.Info : IssueSeverity.Warning;

        var vets = new List<AnimalVeterinarian>();
        foreach (AnimalVeterinarian v in AnimalEditors.Veterinarians())
            if (AnimalEditors.Serves(v)) vets.Add(v);
        if (vets.Count == 0)
        {
            var issue = new ValidationIssue(missing, Category,
                $"{pens} pen{(pens == 1 ? "" : "s")} but no Veterinarian in the open scenes{closed}: nobody can be sent for to see a sick animal, and a keeper's " +
                "medicine cures every illness (Animals ▸ Create Veterinarian In City).");
            if (!cityClosed) issue.WithFix("Create Veterinarian", AnimalEditors.EnsureVeterinarian);
            issues.Add(issue);
        }
        else if (vets.Count > 1)
            issues.Add(new ValidationIssue(IssueSeverity.Info, Category,
                $"{vets.Count} veterinarians in the open scenes: only the first one enabled is sent for (the others stand by).", vets[1], ValidationUtility.PathOf(vets[1].transform)));

        var goods = new List<AnimalMerchantStock>();
        foreach (AnimalMerchantStock g in AnimalEditors.AnimalGoods())
            if (AnimalEditors.Sells(g)) goods.Add(g);
        bool tools = false, medicine = false, supplies = false;
        foreach (AnimalMerchantStock g in goods)
        {
            tools |= g.tools;
            medicine |= g.medicine;
            supplies |= g.supplies;
        }
        if (!tools)
        {
            var issue = new ValidationIssue(missing, Category,
                $"No merchant in the open scenes sells care tools{closed}: brushing, hoof care and mucking out cannot be done " +
                "(Animals ▸ Add Animal Goods To Selected Merchant, or Create Veterinarian In City: the vet sells them).");
            if (!cityClosed) issue.WithFix("Add Animal Goods", AnimalEditors.EnsureAnimalGoods);
            issues.Add(issue);
        }
        if (goods.Count > 0 && !medicine)
            issues.Add(new ValidationIssue(IssueSeverity.Info, Category,
                "No merchant in the open scenes sells medicine (Medicine is off in their Animal Goods): keepers must brew it, or find the plain remedies elsewhere.", goods[0]));
        if (goods.Count > 0 && !supplies)
            issues.Add(new ValidationIssue(IssueSeverity.Info, Category,
                "No merchant in the open scenes sells straw or soap (Supplies is off in their Animal Goods): bedding and soap must come from elsewhere.", goods[0]));

        if (string.IsNullOrEmpty(city)) return;
        var told = new HashSet<GameObject>();
        foreach (AnimalVeterinarian v in vets)
            if (told.Add(v.gameObject)) OutsideCity(v, "The veterinarian", city, issues);
        foreach (AnimalMerchantStock g in goods)
            if (told.Add(g.gameObject)) OutsideCity(g, "The merchant of animal goods", city, issues);
    }

    /// <summary>
    /// Where animals are kept (an Animal Database, or pens in the open scenes): every Alchemy station of the open scenes whose
    /// own Database field is set brews with that database alone, and the veterinary medicines are added only to the
    /// project's database (<see cref="AnimalAlchemy.EnsureInstalled"/>) - one that lacks them cannot make them there.
    /// </summary>
    private static void CheckStations(List<ValidationIssue> issues, int pens)
    {
        if (pens == 0 && SMAssets.Load<AnimalDatabase>(SMAssets.Settings, AnimalDatabase.ResourcesName) == null) return;
        foreach (GameObject root in AnimalEditors.OpenSceneRoots())
            foreach (AlchemyStation station in root.GetComponentsInChildren<AlchemyStation>(true))
            {
                // The field itself, not station.Database: outside play mode the project's asset is not wrapped, so an asset
                // in the field is the very AlchemyDatabase.Instance - yet in play the station brews with it alone.
                var so = new SerializedObject(station);
                var own = so.FindProperty("database")?.objectReferenceValue as AlchemyDatabase;
                if (own == null) continue;
                AlchemyRules rules = own.Rules;
                if (rules == null || !AnimalAlchemy.VetOutcomes(rules).Exists(o => rules.Outcome(o.id) == null)) continue;
                issues.Add(new ValidationIssue(IssueSeverity.Warning, Category,
                    $"'{station.name}' brews with its own Alchemy Database '{own.name}', which lacks the veterinary medicines: they cannot be made there " +
                    "(leave its Database empty, or add AnimalAlchemy.VetOutcomes to that database).", station, ValidationUtility.PathOf(station.transform)));
            }
    }

    private static void OutsideCity(Component c, string who, string city, List<ValidationIssue> issues)
    {
        if (c == null || !c.gameObject.scene.IsValid() || c.gameObject.scene.name == city) return;
        issues.Add(new ValidationIssue(IssueSeverity.Info, Category,
            $"{who} '{c.name}' is in '{c.gameObject.scene.name}', not the City scene ('{city}'): it belongs in the permanent city " +
            "(the hub world's other scenes are made again every cycle).", c, ValidationUtility.PathOf(c.transform)));
    }

    /// <summary>
    /// With Fish-Net, where animals are kept (an Animal Database, or pens in the open scenes): the network prefab a carcass
    /// is laid out with in a network session (Resources/SimpleMovements/Networking/AnimalCarcass), complete and - in a
    /// session, where that can be told - registered (<see cref="AnimalCarcass.PrefabReady"/>): without it no animal can be
    /// butchered in a network session. Offline nothing is needed.
    /// </summary>
    public static void CheckCarcassPrefab(List<ValidationIssue> issues)
    {
#if FISHNET
        if (SMAssets.Load<AnimalDatabase>(SMAssets.Settings, AnimalDatabase.ResourcesName) == null && AnimalEditors.Pens().Count == 0) return;
        List<string> lacks = CarcassPrefabLacks(out GameObject prefab);
        if (lacks == null)
        {
            issues.Add(new ValidationIssue(IssueSeverity.Warning, Category,
                    $"No animal carcass prefab (Resources/{SMAssets.ResourcePath(SMAssets.Networking, AnimalCarcass.PrefabName)}): in a network session no animal can be " +
                    $"butchered (its owner is told \"{AnimalCarcass.NotSetUp}\").")
                .WithFix("Create", EnsureCarcassPrefab));
            return;
        }
        string path = AssetDatabase.GetAssetPath(prefab);
        if (lacks.Count > 0)
        {
            issues.Add(new ValidationIssue(IssueSeverity.Warning, Category,
                    $"The animal carcass prefab has no {string.Join(", ", lacks)}: a carcass laid out in a network session would not work for every player " +
                    "(it needs a Network Object, a Harvestable Creature, a Net Carcass and an Animal Carcass Look).", prefab, path)
                .WithFix("Add", EnsureCarcassPrefab));
            return;
        }
        if (!AnimalCarcass.PrefabReady)
            issues.Add(new ValidationIssue(IssueSeverity.Warning, Category,
                $"The animal carcass prefab is not one of this session's network prefabs (its Network Object has no prefab id): no animal can be butchered " +
                $"(its owner is told \"{AnimalCarcass.NotSetUp}\"). Fish-Net's spawnable prefabs must include it.", prefab, path));
#endif
    }

#if FISHNET
    /// <summary>
    /// The parts the network prefab of a butchered animal's carcass lacks (empty: none) - a Network Object, a Harvestable
    /// Creature, a Net Carcass, an Animal Carcass Look - or null when there is no such prefab. Also used by the Scene &amp;
    /// Prefab Setup window.
    /// </summary>
    public static List<string> CarcassPrefabLacks(out GameObject prefab)
    {
        prefab = AnimalCarcass.Prefab;
        if (prefab == null) return null;
        var lacks = new List<string>();
        if (prefab.GetComponent<FishNet.Object.NetworkObject>() == null) lacks.Add("Network Object");
        if (prefab.GetComponent<HarvestableCreature>() == null) lacks.Add("Harvestable Creature");
        if (prefab.GetComponent<GameNet.NetCarcass>() == null) lacks.Add("Net Carcass");
        if (prefab.GetComponent<AnimalCarcassLook>() == null) lacks.Add("Animal Carcass Look");
        return lacks;
    }

    /// <summary>
    /// The fix: the carcass prefab made when there is none (Network ▸ Create Animal Carcass Prefab), or given the parts it
    /// lacks. True: something changed.
    /// </summary>
    public static bool EnsureCarcassPrefab()
    {
        List<string> lacks = CarcassPrefabLacks(out GameObject prefab);
        if (lacks == null)
        {
            GameNet.EditorTools.NetworkSetupMenu.CreateAnimalCarcassPrefab();
            return AnimalCarcass.Prefab != null;
        }
        return lacks.Count > 0 && ValidationUtility.EditPrefab(AssetDatabase.GetAssetPath(prefab), AddCarcassParts);
    }

    private static bool AddCarcassParts(GameObject root)
    {
        bool changed = false;
        if (root.GetComponent<FishNet.Object.NetworkObject>() == null)
        {
            root.AddComponent<FishNet.Object.NetworkObject>();
            changed = true;
        }
        if (root.GetComponent<HarvestableCreature>() == null)
        {
            root.AddComponent<HarvestableCreature>();
            changed = true;
        }
        if (root.GetComponent<GameNet.NetCarcass>() == null)
        {
            root.AddComponent<GameNet.NetCarcass>();
            changed = true;
        }
        if (root.GetComponent<AnimalCarcassLook>() == null)
        {
            root.AddComponent<AnimalCarcassLook>();
            changed = true;
        }
        return changed;
    }
#endif

    public static void CheckRoot(GameObject root, List<ValidationIssue> issues)
    {
        if (root == null) return;
        foreach (AnimalPen pen in root.GetComponentsInChildren<AnimalPen>(true))
        {
            string path = ValidationUtility.PathOf(pen.transform);
            var errors = new List<string>();
            var warnings = new List<string>();
            pen.Validate(errors, warnings);
            foreach (string e in errors) issues.Add(new ValidationIssue(IssueSeverity.Error, Category, e, pen, path));
            foreach (string w in warnings) issues.Add(new ValidationIssue(IssueSeverity.Warning, Category, w, pen, path));
            if (EditorUtility.IsPersistent(pen.gameObject))
                issues.Add(new ValidationIssue(IssueSeverity.Warning, Category, "An animal pen in a prefab: every copy has the prefab's Pen Id unless it is given its own.", pen, path));
            Property own = pen.GetComponentInParent<Property>(true);
            Property land = AnimalEditors.ForeignLand(pen, out float shared);
            if (land != null)
                issues.Add(new ValidationIssue(IssueSeverity.Error, Category,
                    $"The pen lies on the land of '{land.DisplayName}' ({shared:0.#} m²) but is not one of its pens: make it a child of that property (or list it in its Pens), or move it.", pen, path));
            if (own == null)
                issues.Add(new ValidationIssue(IssueSeverity.Info, Category, "The pen is on no property: it is public (anyone may tend it and keep animals in it).", pen, path));
        }
    }
}

/// <summary>
/// The Animal Pen inspector: its id, name and kind, its outline (Scene view: drag points, Shift+click an edge to add one,
/// Ctrl/Cmd+click a point to remove it; the square moves it), its stations (the feeder and trough handles), its
/// checks, and in Play mode its animals as the rules see them - their needs, coat, comfort, stress, welfare, hooves,
/// illnesses and the build-up of the next ones, and the pen's floor, bedding, water and worms - with time skips, the kits,
/// and debug tools where the rules run (a filthy pen, an illness taking hold, healing every animal, a death by illness),
/// which change the world's records as a command would, so views and saves follow.
/// </summary>
[CustomEditor(typeof(AnimalPen))]
public class AnimalPenEditor : Editor
{
    private static readonly int GroundHash = "AnimalPenGround".GetHashCode();
    private static bool editing;
    private static float snapStep = 0.25f;
    private readonly List<string> errors = new List<string>(), warnings = new List<string>();
    private float nextCheck;
    /// <summary>Scratch for the build-ups shown (the largest first).</summary>
    private static readonly List<AnimalExposure> buildUps = new List<AnimalExposure>();

    private void OnDisable() => GroundAreaHandles.HideTransformTool(typeof(AnimalPenEditor), false);

    /// <summary>In play its animals change with every step: keep the numbers live.</summary>
    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        var pen = (AnimalPen)target;
        serializedObject.Update();
        EditorGUILayout.LabelField("Pen", $"{pen.PenId} · {pen.Area:0.#} m² · {pen.kind}");
        Property p = pen.GetComponentInParent<Property>(true);
        EditorGUILayout.LabelField("Property", p != null ? p.DisplayName : "none (public)");
        using (new EditorGUILayout.HorizontalScope())
        {
            bool now = GUILayout.Toggle(editing, new GUIContent("Edit Outline", "Shape the pen in the Scene view."), "Button");
            if (now != editing)
            {
                editing = now;
                GroundAreaHandles.HideTransformTool(typeof(AnimalPenEditor), editing);
                SceneView.RepaintAll();
            }
            snapStep = EditorGUILayout.FloatField(new GUIContent("Snap", "Metres (0 = off)."), snapStep, GUILayout.Width(160f));
            if (GUILayout.Button("New Id", GUILayout.Width(70f)) &&
                EditorUtility.DisplayDialog("Animals", "Give this pen a new id? Its animals in saved worlds stay with the old id (they are boarded).", "New Id", "Cancel"))
                AnimalEditors.NewId(pen);
        }
        DrawDefaultInspector();
        if (Time.realtimeSinceStartup >= nextCheck)
        {
            nextCheck = Time.realtimeSinceStartup + 0.5f;
            errors.Clear();
            warnings.Clear();
            pen.Validate(errors, warnings);
            Property land = AnimalEditors.ForeignLand(pen, out float shared);
            if (land != null) errors.Add($"It lies on the land of '{land.DisplayName}' ({shared:0.#} m²) but is not one of its pens.");
        }
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        if (Application.isPlaying) PlayInfo(pen);
        serializedObject.ApplyModifiedProperties();
    }

    private static void PlayInfo(AnimalPen pen)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("In play", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Time", FarmClock.Date.Full());
        // Null on a network client (the server keeps the animals): nothing here may change them.
        AnimalBook book = AnimalDirector.Book;
        if (book == null)
        {
            EditorGUILayout.HelpBox(AnimalDirector.RulesHere ? (string.IsNullOrEmpty(AnimalDirector.Closed) ? "No animals kept here." : AnimalDirector.Closed) : "A network client: the server keeps the animals.", MessageType.Info);
            return;
        }
        AnimalHerd h = book.Herd(pen.PenId);
        string penId = h != null ? h.pen.id : null;
        if (h == null)
            EditorGUILayout.HelpBox("Not known to the world yet: the Animal Director takes in each active, enabled pen (one per Pen Id) as it runs.", MessageType.Info);
        else
        {
            IAnimalCatalog cat = book.Catalog;
            AnimalRules rules = book.Rules;
            double now = AnimalDirector.Now;
            AnimalPenSpec spec = h.spec ?? h.pen.spec;
            AnimalVeterinarian vet = AnimalVeterinarian.Current;
            EditorGUILayout.LabelField("Feeder", $"{h.pen.FeederPortions:0.#} / {pen.feederCapacity:0} portions");
            EditorGUILayout.LabelField("Water", $"{h.pen.water:0.#} / {pen.waterCapacity:0} L · fouled {h.pen.waterDirt:0}");
            if (spec != null && spec.GrassMax > 0f) EditorGUILayout.LabelField("Grass", $"{h.pen.grass:0} / {spec.GrassMax:0} portions");
            EditorGUILayout.LabelField("Floor", FloorText(h.pen, spec, now));
            EditorGUILayout.LabelField("Upgrades", UpgradesText(h.pen, spec, book.Rules));
            EditorGUILayout.LabelField("Veterinarian", vet != null ? vet.name : "none: a keeper's medicine cures every illness");
            foreach (AnimalRecord a in h.animals)
            {
                if (a == null) continue;
                AnimalInfo(penId, a, cat != null ? cat.Species(a.species) : null, cat, rules, now, vet != null);
            }
        }
        // Where the rules run (offline, the host, a server), for a pen with no herd too.
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+6 h")) FarmClock.Skip(6.0);
            if (GUILayout.Button("+1 day")) FarmClock.Skip(24.0);
            if (GUILayout.Button("+1 week")) FarmClock.Skip(24.0 * 7.0);
            if (GUILayout.Button("Save now")) AnimalDirector.SaveNow();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Starter kit", "A crate of each species' first breed and some of every feed, in your bag.")))
                AnimalEditors.GiveKit(false, false);
            if (GUILayout.Button(new GUIContent("Care kit", "The care tools (the shears and the milking pail too), straw and soap, and medicine, in your bag.")))
                AnimalEditors.GiveKit(true, false);
            if (h != null)
            {
                // These change what is shown here: done after this frame's inspector (never in the middle of its layout).
                if (GUILayout.Button(new GUIContent("Dirty pen", "Debug: a filthy floor, spent bedding, fouled water and (a grazing pen) worms in the ground.")))
                    Later(() => DirtyPen(penId));
                if (GUILayout.Button(new GUIContent("Heal all", "Debug: every living animal here loses its illnesses and their build-up, its toxins and upset, and is at full health.")))
                    Later(() => HealAll(penId));
                if (GUILayout.Button(new GUIContent("Ripen", "Debug: every cycle under way here completes now (eggs, milk, wool), judged by how each animal lived through it so far, and waits to be collected - where there is room.")))
                    Later(() => RipenAll(penId));
            }
        }
        if (h != null)
            using (new EditorGUILayout.HorizontalScope())
            {
                string id = penId;
                if (GUILayout.Button(new GUIContent("Due now", "Debug: every mother expecting here is due now: her young are born at the next step.")))
                    Later(() => DueNow(id));
                if (GUILayout.Button(new GUIContent("Bond +25", "Debug: every living animal here is 25 closer to its owner (its perks unlock as its bond grows).")))
                    Later(() => BondUp(id));
                if (GUILayout.Button(new GUIContent("Ready to breed", "Debug: every animal here may be bred again now (cooldowns and failed tries forgotten).")))
                    Later(() => ReadyToBreed(id));
            }
        if (h != null)
            using (new EditorGUILayout.HorizontalScope())
            {
                string id = penId;
                if (GUILayout.Button(new GUIContent("Owe a week", "Debug: the pen owes a week of upkeep more (past its grace days it falls into disrepair: its machines stop).")))
                    Later(() => OweWeek(id));
                if (GUILayout.Button(new GUIContent("Fit all", "Debug: the pen is at its top level and fitted with every machine, for nothing (upkeep falls due from now).")))
                    Later(() => FitAll(id));
            }
    }

    // ------------------------------------------------------------------ what is shown
    private static void AnimalInfo(string penId, AnimalRecord a, AnimalSpeciesData sp, IAnimalCatalog cat, AnimalRules rules, double now, bool vet)
    {
        EditorGUILayout.LabelField($"{AnimalSim.NameOf(a, sp)} ({a.species}, {a.stage}, {a.status})",
            $"HP {a.health:0} · full {a.satiety:0} · water {a.hydration:0} · nutrition {a.nutrition:0} · toxin {a.toxin:0.#} · dev {a.development:0}h");
        using (new EditorGUI.IndentLevelScope())
        {
            if (!a.Alive)
            {
                string ago = a.diedAt >= 0.0 ? $", {Math.Max(0.0, now - a.diedAt):0.#} h ago" : "";
                EditorGUILayout.LabelField("Remains", AnimalRemains.CauseText(a, sp, cat) + ago);
                return;
            }
            EditorGUILayout.LabelField("Coat and mood", $"clean {a.hygiene:0} · comfort {a.comfort:0}{Why(a.discomfort)} · stress {a.stress:0}{Why(a.stressor)}");
            string hooves = AnimalCare.CareOf(sp).HasHooves ? $" · hooves {a.hooves:0}" : "";
            EditorGUILayout.LabelField("Welfare", $"{a.welfare:0} {AnimalWelfare.WordOf(a, rules)}{hooves} · affinity {a.affinity:0}");
            if (a.conditions != null)
                foreach (AnimalCondition k in a.conditions)
                    if (k != null) EditorGUILayout.LabelField(k.id, ConditionText(k, cat != null ? cat.Condition(k.id) : null, rules, now, vet));
            string built = BuildUpText(a, cat, now, 3);
            if (built.Length > 0) EditorGUILayout.LabelField("Build-up", built);
            EditorGUILayout.LabelField("Family and bond", FamilyText(a, sp, rules, now));
            if (a.bonds != null && a.bonds.Count > 0) EditorGUILayout.LabelField("Bonds", BondsText(a));
            if (sp?.products != null)
                foreach (AnimalProductSpec p in sp.products)
                {
                    if (p == null) continue;
                    AnimalProductState st = AnimalProduction.State(a, p.id, false);
                    if (st == null && !AnimalProduction.Makes(a, sp, p)) continue;
                    EditorGUILayout.LabelField(p.DisplayName, ProductText(a, p, st, rules));
                }
            bool ill = WorstIllness(a, cat, out _) != null;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Infect…", "Debug: an illness takes hold, at the stage you choose, as the simulation would make it."),
                        EditorStyles.miniButtonLeft, GUILayout.Width(72f)))
                    InfectMenu(penId, a, sp, cat);
                using (new EditorGUI.DisabledScope(!ill))
                {
                    string id = a.id;
                    if (GUILayout.Button(new GUIContent("Kill (illness)", ill ? "Debug: it dies of its worst illness now." : "It has no illness to die of: Infect… it first."),
                            EditorStyles.miniButtonRight, GUILayout.Width(92f)))
                        Later(() => Kill(penId, id));
                }
            }
        }
    }

    /// <summary>"affinity 62 (friendly) · gen 1 · 2 young · in calf, due in 40 h · ready to breed": its family and bond as the rules see them.</summary>
    private static string FamilyText(AnimalRecord a, AnimalSpeciesData sp, AnimalRules rules, double now)
    {
        var sb = new StringBuilder();
        sb.Append($"affinity {a.affinity:0} ({AnimalSocial.BondWord(a.affinity)})");
        if (a.generation > 0) sb.Append($" · gen {a.generation}");
        if (a.offspring > 0) sb.Append($" · {a.offspring} young");
        if (AnimalBreeding.Expecting(a))
            sb.Append($" · {AnimalBreeding.SpecOf(sp).expectingWord}, {a.pregnancy.litter} due in {Math.Max(0.0, a.pregnancy.dueAt - now):0} h (kin {a.pregnancy.kinship:0.###})");
        else if (AnimalBreeding.Ready(a, sp, rules, now, out string why)) sb.Append(" · ready to breed");
        else sb.Append($" · {why}");
        return sb.ToString();
    }

    /// <summary>"Bella companion 64 · Ferdinand rival 30 (apart)": its bonds as the rules hold them.</summary>
    private static string BondsText(AnimalRecord a)
    {
        var parts = new List<string>();
        foreach (AnimalBond b in a.bonds)
            if (b != null && parts.Count < 5)
                parts.Add($"{(string.IsNullOrWhiteSpace(b.name) ? b.other : b.name)} {b.kind.ToString().ToLowerInvariant()} {b.level:0}{(b.apart ? " (apart)" : "")}");
        return string.Join(" · ", parts);
    }

    /// <summary>"2 waiting (Good 61%) · cycle 45% of 24 h · lot 7 · Producing": a product as the rules see it.</summary>
    private static string ProductText(AnimalRecord a, AnimalProductSpec p, AnimalProductState st, AnimalRules rules)
    {
        if (st == null) return "not begun";
        var sb = new StringBuilder();
        int n = st.Waiting;
        if (n > 0)
        {
            float q = 0f;
            foreach (AnimalProductLot l in st.waiting)
                if (l != null) q += l.quality * l.units;
            q /= n;
            AnimalProductTier tier = AnimalProduction.Tier(q, AnimalProduction.Rare(a), rules?.production);
            sb.Append($"{n}/{p.capacity} waiting ({tier} {q * 100f:0}%) · ");
        }
        sb.Append($"cycle {AnimalMath.Clamp01(st.progress) * 100f:0}% of {p.cycleHours:0.#} h · lot {st.cycle + 1} · {st.status}");
        return sb.ToString();
    }

    /// <summary>What made it least comfortable, or stressed it most (empty: nothing).</summary>
    private static string Why(AnimalDiscomfort d) => d == AnimalDiscomfort.None ? "" : $" ({d})";

    /// <summary>"Improved (2) · hopper 120 · waterer · trays 4 · owes 6 (disrepair)": a pen's Phase 5 state.</summary>
    private static string UpgradesText(AnimalPenRecord p, AnimalPenSpec spec, AnimalRules rules)
    {
        int level = AnimalUpgrades.Level(p, rules);
        string text = $"{AnimalUpgrades.LevelName(level, rules)} ({level})";
        if (p.hopper) text += $" · hopper {p.HopperPortions:0}";
        if (p.waterer) text += " · waterer";
        if (p.trays) text += $" · trays {p.TrayUnits}";
        float day = AnimalUpgrades.UpkeepPerDay(p, spec, rules);
        if (day > 0f || p.upkeepOwed > 0f) text += $" · {day:0.#}/day, owes {p.upkeepOwed:0.#}{(AnimalUpgrades.InDisrepair(p, spec, rules) ? " (disrepair)" : "")}";
        return text;
    }

    /// <summary>Debug: the pen owes a week of upkeep more.</summary>
    private static void OweWeek(string penId)
    {
        if (!Live(penId, out AnimalBook book, out AnimalHerd h, out AnimalSimContext _, out double _)) return;
        AnimalPenSpec spec = h.spec ?? h.pen.spec;
        float day = Mathf.Max(1f, AnimalUpgrades.UpkeepPerDay(h.pen, spec, book.Rules));
        h.pen.upkeepOwed += day * 7f;
        Changed(book, h, null);
        Debug.Log($"[Animals] Debug: pen '{penId}' owes {h.pen.upkeepOwed:0.#} in upkeep{(AnimalUpgrades.InDisrepair(h.pen, spec, book.Rules) ? ": it is in disrepair" : "")}.");
    }

    /// <summary>Debug: the pen at its top level with every machine.</summary>
    private static void FitAll(string penId)
    {
        if (!Live(penId, out AnimalBook book, out AnimalHerd h, out AnimalSimContext _, out double now)) return;
        h.pen.level = AnimalUpgrades.MaxLevel(book.Rules);
        h.pen.hopper = h.pen.waterer = h.pen.trays = true;
        h.pen.upgradedAt = now;
        h.pen.upgradedBy = "editor";
        Changed(book, h, null);
        Debug.Log($"[Animals] Debug: pen '{penId}' is {AnimalUpgrades.LevelName(h.pen.level, book.Rules).ToLowerInvariant()} and fitted with a feed hopper, an automatic waterer and egg trays.");
    }

    private static string FloorText(AnimalPenRecord p, AnimalPenSpec spec, double now)
    {
        string bedding = spec != null && !spec.UsesBedding ? "none (not a bedded pen)" : p.bedding < 0f ? "not laid yet" : p.bedding.ToString("0");
        string text = $"dirt {p.dirt:0} · bedding {bedding} · worms {p.parasites:0}";
        if (p.lastCleanedAt >= 0.0) text += $" · mucked out {Math.Max(0.0, now - p.lastCleanedAt):0.#} h ago";
        return text;
    }

    /// <summary>"23 Mild · from DirtyHabitat · named · treated (antiseptic)": an illness as the rules see it.</summary>
    private static string ConditionText(AnimalCondition k, AnimalConditionData c, AnimalRules rules, double now, bool vet)
    {
        var sb = new StringBuilder();
        sb.Append(k.severity.ToString("0")).Append(' ').Append(c != null ? c.StageName(k) : "(not in the Animal Database)");
        if (k.cause != AnimalCause.None) sb.Append(" · from ").Append(k.cause.ToString());
        sb.Append(!k.shown ? " · hidden" : k.diagnosed ? " · named" : " · showing");
        if (k.Treated(now)) sb.Append(string.IsNullOrEmpty(k.treatment) ? " · treated" : $" · treated ({k.treatment})");
        if (k.byVet) sb.Append(" · seen by the vet");
        if (c != null && AnimalHealth.Gated(k, c, rules, vet)) sb.Append(" · needs the vet");
        if (k.Recovering) sb.Append($" · recovering, {Math.Max(0.0, k.recoveringUntil - now):0} h left");
        return sb.ToString();
    }

    /// <summary>The largest build-ups of illnesses it does not have yet, against the exposure at which each takes hold in it ("infection 4.1/22.7").</summary>
    private static string BuildUpText(AnimalRecord a, IAnimalCatalog cat, double now, int most)
    {
        if (a.exposures == null || a.exposures.Count == 0) return "";
        buildUps.Clear();
        foreach (AnimalExposure e in a.exposures)
            if (e != null && (e.value >= 0.01f || e.immuneUntil > now)) buildUps.Add(e);
        buildUps.Sort((x, y) => y.value.CompareTo(x.value));
        var sb = new StringBuilder();
        for (int i = 0; i < buildUps.Count && i < most; i++)
        {
            AnimalExposure e = buildUps[i];
            AnimalConditionData c = cat != null ? cat.Condition(e.condition) : null;
            if (sb.Length > 0) sb.Append(" · ");
            sb.Append(e.condition).Append(' ').Append(e.value.ToString("0.#"));
            if (c != null) sb.Append('/').Append(AnimalHealth.Threshold(a, c).ToString("0.#"));
            if (e.immuneUntil > now) sb.Append($" (immune {e.immuneUntil - now:0} h)");
        }
        if (buildUps.Count > most) sb.Append($" · {buildUps.Count - most} more");
        buildUps.Clear();
        return sb.ToString();
    }

    /// <summary>Its worst active illness (not recovering) the catalog knows, and its data; null when it has none.</summary>
    private static AnimalCondition WorstIllness(AnimalRecord a, IAnimalCatalog cat, out AnimalConditionData data)
    {
        data = null;
        AnimalCondition best = null;
        if (a == null || a.conditions == null || cat == null) return null;
        foreach (AnimalCondition k in a.conditions)
        {
            if (k == null || k.Recovering) continue;
            AnimalConditionData c = cat.Condition(k.id);
            if (c == null || (best != null && k.severity <= best.severity)) continue;
            best = k;
            data = c;
        }
        return best;
    }

    // ------------------------------------------------------------------ debug tools (where the rules run)
    /// <summary>Runs a change after this frame's inspector has drawn (a change in the middle of its layout breaks it).</summary>
    private static void Later(Action change)
    {
        if (change != null) EditorApplication.delayCall += () => change();
    }

    /// <summary>
    /// The illnesses this animal can get, each with its stages from the one it starts in: choosing one makes it take hold
    /// there (<see cref="Infect"/>). Those its species cannot have, and those it has, are listed greyed.
    /// </summary>
    private static void InfectMenu(string penId, AnimalRecord a, AnimalSpeciesData sp, IAnimalCatalog cat)
    {
        var menu = new GenericMenu();
        string animal = a.id;
        string kind = sp != null ? sp.DisplayName.ToLowerInvariant() : "animal";
        IReadOnlyList<AnimalConditionData> all = cat != null ? cat.AllConditions : null;
        int listed = 0;
        if (all != null)
            foreach (AnimalConditionData c in all)
            {
                if (c == null || string.IsNullOrEmpty(c.id)) continue;
                listed++;
                string label = c.DisplayName.Replace('/', '-');
                if (sp != null && sp.SusceptibilityTo(c) <= 0f)
                {
                    menu.AddDisabledItem(new GUIContent($"{label} (a {kind} cannot have it)"));
                    continue;
                }
                if (a.Condition(c.id) != null)
                {
                    menu.AddDisabledItem(new GUIContent($"{label} (it has it)"));
                    continue;
                }
                string id = c.id;
                int count = c.stages != null ? c.stages.Count : 0;
                if (count == 0)
                {
                    float onset = AnimalMath.Clamp100(c.startSeverity);
                    menu.AddItem(new GUIContent(label), false, () => Infect(penId, animal, id, onset));
                    continue;
                }
                int start = c.StageAt(c.startSeverity);
                for (int i = start; i < count; i++)
                {
                    AnimalConditionStage st = c.Stage(i);
                    float severity = i == start ? AnimalMath.Clamp100(c.startSeverity) : Middle(c, i);
                    string stage = string.IsNullOrWhiteSpace(st.name) ? $"Stage {i + 1}" : st.name.Replace('/', '-');
                    string hidden = st.visible ? "" : ", hidden";
                    menu.AddItem(new GUIContent($"{label}/{stage} (severity {severity:0}{hidden})"), false, () => Infect(penId, animal, id, severity));
                }
            }
        if (listed == 0) menu.AddDisabledItem(new GUIContent("No illnesses in the Animal Database"));
        menu.ShowAsContext();
    }

    /// <summary>The middle of a stage (from its start to the next stage's, or to 100), never below the illness's start severity.</summary>
    private static float Middle(AnimalConditionData c, int index)
    {
        float from = c.Stage(index).from;
        float to = c.stages != null && index + 1 < c.stages.Count ? c.Stage(index + 1).from : 100f;
        return AnimalMath.Clamp100(Mathf.Max(c.startSeverity, 0.5f * (from + to)));
    }

    /// <summary>
    /// The world's book and this pen's herd, brought up to now first (a change is made now, after the hours it had to live,
    /// as a command makes it). False outside play (play may have stopped since the click), on a network client (only the
    /// server changes the animals), or for a pen the world does not know.
    /// </summary>
    private static bool Live(string penId, out AnimalBook book, out AnimalHerd h, out AnimalSimContext ctx, out double now)
    {
        book = null;
        h = null;
        ctx = null;
        now = 0.0;
        if (!Application.isPlaying || !AnimalDirector.RulesHere || string.IsNullOrEmpty(penId)) return false;
        book = AnimalDirector.Book;
        h = book != null ? book.Herd(penId) : null;
        now = AnimalDirector.Now;
        if (h == null) return false;
        ctx = AnimalDirector.SimContext;
        book.BringUpToDate(h, now, ctx);
        return true;
    }

    /// <summary>Views and saves follow a change: the animal's and the pen's revisions, the book's.</summary>
    private static void Changed(AnimalBook book, AnimalHerd h, AnimalRecord a)
    {
        if (a != null) a.revision++;
        h.pen.revision++;
        book.Touch();
    }

    /// <summary>What the simulation tells its owner (its history and the world's notices: the director delivers them).</summary>
    private static void Tell(AnimalRecord a, AnimalSimContext ctx, AnimalRules rules, AnimalEventKind kind, double at, string text)
    {
        a.Log(kind, at, text, "", rules.keepEvents);
        if (ctx != null) ctx.Notice(a, kind, text);
    }

    /// <summary>
    /// Debug: the illness takes hold in the animal (<see cref="AnimalHealth.Begin"/>, its cause "nothing in particular"),
    /// raised to <paramref name="severity"/> the way the simulation's worsening would raise it (its stage, the history, the
    /// symptom its owner is told when it first shows).
    /// </summary>
    private static void Infect(string penId, string animalId, string conditionId, float severity)
    {
        if (!Live(penId, out AnimalBook book, out AnimalHerd h, out AnimalSimContext ctx, out double now)) return;
        IAnimalCatalog cat = book.Catalog;
        AnimalRecord a = book.Find(animalId);
        AnimalConditionData c = cat != null ? cat.Condition(conditionId) : null;
        if (a == null || !a.Alive || a.pen != penId || c == null) return;
        AnimalRules rules = book.Rules;
        AnimalSpeciesData sp = cat.Species(a.species);
        string name = AnimalSim.NameOf(a, sp);
        if (a.Condition(c.id) != null)
        {
            Debug.Log($"[Animals] {name} has {c.DisplayName.ToLowerInvariant()} already.");
            return;
        }
        var outcome = new AnimalHealthOutcome();
        AnimalCondition k = AnimalHealth.Begin(a, c, AnimalCause.None, now, rules, sp, outcome);
        if (k == null) return;
        // Its build-up is spent, as at an onset in the simulation.
        AnimalExposure e = a.Exposure(c.id);
        if (e != null) e.value = 0f;
        Worsen(a, name, c, k, severity, now, rules, outcome);
        foreach (AnimalHealthNotice n in outcome.notices) Tell(a, ctx, rules, n.kind, n.at, n.text);
        a.absorbFactor = AnimalHealth.Effects(a, cat).absorption;
        Changed(book, h, a);
        string shows = k.shown ? "" : ": it does not show yet";
        Debug.Log($"[Animals] Debug: {name} has {c.DisplayName.ToLowerInvariant()} ({c.StageName(k)}, severity {k.severity:0}){shows}.");
    }

    /// <summary>
    /// Raises a new illness to <paramref name="severity"/> as the simulation's worsening does: its stage and peak, a Worsened
    /// entry when its stage rose, Showed and its owner told the symptom when the stage first shows, and "getting worse" at its last stage.
    /// </summary>
    private static void Worsen(AnimalRecord a, string name, AnimalConditionData c, AnimalCondition k, float severity, double now, AnimalRules rules, AnimalHealthOutcome outcome)
    {
        severity = AnimalMath.Clamp100(severity);
        if (severity <= k.severity) return;
        int old = k.stage;
        k.severity = severity;
        k.stage = c.StageAt(severity);
        k.peak = Mathf.Max(k.peak, severity);
        if (k.stage <= old) return;
        AnimalHealth.LogEvent(a, c, k, AnimalHealthEventKind.Worsened, now, "", rules);
        AnimalConditionStage reached = c.Stage(k.stage);
        if (!k.shown && reached.visible)
        {
            k.shown = true;
            AnimalHealth.LogEvent(a, c, k, AnimalHealthEventKind.Showed, now, "", rules);
            outcome.notices.Add(new AnimalHealthNotice { kind = AnimalEventKind.FellIll, at = now, text = $"{name}: {reached.Symptom}" });
        }
        if (c.IsLastStage(k.stage) && k.shown && c.stages.Count > 1)
            outcome.notices.Add(new AnimalHealthNotice { kind = AnimalEventKind.Worsened, at = now, text = $"{name} is getting worse." });
    }

    /// <summary>Debug: the animal dies of its worst illness now, recorded as the simulation records such a death (its owner is told).</summary>
    private static void Kill(string penId, string animalId)
    {
        if (!Live(penId, out AnimalBook book, out AnimalHerd h, out AnimalSimContext ctx, out double now)) return;
        IAnimalCatalog cat = book.Catalog;
        AnimalRecord a = book.Find(animalId);
        if (a == null || !a.Alive || a.pen != penId) return;
        AnimalSpeciesData sp = cat != null ? cat.Species(a.species) : null;
        AnimalCondition k = WorstIllness(a, cat, out AnimalConditionData killer);
        if (k == null || killer == null)
        {
            Debug.Log($"[Animals] {AnimalSim.NameOf(a, sp)} has no illness to die of: Infect… it first.");
            return;
        }
        AnimalRules rules = book.Rules;
        a.deathCondition = killer.id;
        AnimalHealth.LogEvent(a, killer, k, AnimalHealthEventKind.Died, now, "", rules);
        AnimalSim.Die(a, killer.deathCause != AnimalDeathCause.None ? killer.deathCause : AnimalDeathCause.Illness, now, ctx, sp);
        book.Urgent = true;
        Changed(book, h, a);
        Debug.Log($"[Animals] Debug: {AnimalSim.NameOf(a, sp)} died of {killer.DisplayName.ToLowerInvariant()} ({killer.StageName(k)}).");
    }

    /// <summary>Debug: every living animal of the pen loses its illnesses (logged as over) and their build-up, its toxins and upset, and is at full health.</summary>
    private static void HealAll(string penId)
    {
        if (!Live(penId, out AnimalBook book, out AnimalHerd h, out AnimalSimContext _, out double now)) return;
        int healed = 0;
        foreach (AnimalRecord a in h.animals)
            if (HealNow(a, book.Catalog, book.Rules, now)) healed++;
        Changed(book, h, null);
        Debug.Log($"[Animals] Debug: {healed} animal(s) of pen '{penId}' healed.");
    }

    /// <summary>
    /// Debug, for one living animal: its illnesses are gone (each its keepers knew of, one that showed or was named, logged
    /// as over, as when it clears by itself), with their build-up, its toxins and upset, and it is at full health. False for
    /// none or a dead one.
    /// </summary>
    internal static bool HealNow(AnimalRecord a, IAnimalCatalog cat, AnimalRules rules, double now)
    {
        if (a == null || !a.Alive) return false;
        if (a.conditions != null)
        {
            foreach (AnimalCondition k in a.conditions)
            {
                if (k == null || !(k.shown || k.diagnosed)) continue;
                k.shown = true;
                AnimalHealth.LogEvent(a, cat != null ? cat.Condition(k.id) : null, k, AnimalHealthEventKind.Cleared, now, "", rules);
            }
            a.conditions.Clear();
        }
        if (a.exposures != null) a.exposures.Clear();
        a.health = 100f;
        a.toxin = 0f;
        a.upset = 0f;
        a.absorbFactor = 1f;
        a.revision++;
        return true;
    }

    /// <summary>Debug: every cycle under way in the pen completes now, and its lot waits (see <see cref="AnimalProduction.Ripen"/>).</summary>
    private static void RipenAll(string penId)
    {
        if (!Live(penId, out AnimalBook book, out AnimalHerd h, out AnimalSimContext _, out double now)) return;
        int lots = 0;
        foreach (AnimalRecord a in h.animals)
            if (a != null) lots += AnimalProduction.Ripen(a, book.Catalog?.Species(a.species), book.Rules, now, h.pen.dirt);
        Changed(book, h, null);
        Debug.Log(lots > 0 ? $"[Animals] Debug: {lots} lot(s) ripened in pen '{penId}': collect them in its window." : $"[Animals] Debug: nothing in pen '{penId}' could ripen (none makes anything now, or what waits is full).");
    }

    /// <summary>Debug: every mother expecting in the pen is due now (her young are born at the next step).</summary>
    private static void DueNow(string penId)
    {
        if (!Live(penId, out AnimalBook book, out AnimalHerd h, out AnimalSimContext _, out double now)) return;
        int n = 0;
        foreach (AnimalRecord a in h.animals)
            if (a != null && a.Alive && AnimalBreeding.Expecting(a))
            {
                a.pregnancy.dueAt = Math.Min(a.pregnancy.dueAt, now);
                n++;
            }
        Changed(book, h, null);
        Debug.Log(n > 0 ? $"[Animals] Debug: {n} mother(s) in pen '{penId}' are due: their young are born at the next step." : $"[Animals] Debug: no mother in pen '{penId}' is expecting.");
    }

    /// <summary>Debug: every living animal of the pen 25 closer to its owner.</summary>
    private static void BondUp(string penId)
    {
        if (!Live(penId, out AnimalBook book, out AnimalHerd h, out AnimalSimContext _, out double _)) return;
        foreach (AnimalRecord a in h.animals)
            if (a != null && a.Alive)
            {
                a.affinity = Mathf.Min(100f, a.affinity + 25f);
                a.revision++;
            }
        Changed(book, h, null);
        Debug.Log($"[Animals] Debug: the animals of pen '{penId}' are closer to their owners.");
    }

    /// <summary>Debug: every animal of the pen may be bred again now.</summary>
    private static void ReadyToBreed(string penId)
    {
        if (!Live(penId, out AnimalBook book, out AnimalHerd h, out AnimalSimContext _, out double now)) return;
        foreach (AnimalRecord a in h.animals)
            if (a != null && a.Alive && !AnimalBreeding.Expecting(a))
            {
                a.breedReadyAt = Math.Min(a.breedReadyAt, now);
                a.revision++;
            }
        Changed(book, h, null);
        Debug.Log($"[Animals] Debug: the animals of pen '{penId}' may be bred again.");
    }

    /// <summary>Debug: the pen at its worst - a filthy floor, spent bedding (a bedded pen), fouled water (a trough with water), worms in the ground (a grazing pen).</summary>
    private static void DirtyPen(string penId)
    {
        if (!Live(penId, out AnimalBook book, out AnimalHerd h, out AnimalSimContext _, out double _)) return;
        AnimalPenRecord p = h.pen;
        AnimalPenSpec spec = h.spec ?? p.spec;
        p.dirt = Mathf.Max(p.dirt, 90f);
        if (spec != null && spec.UsesBedding) p.bedding = p.bedding < 0f ? 5f : Mathf.Min(p.bedding, 5f);
        if (p.water > 0f) p.waterDirt = Mathf.Max(p.waterDirt, 60f);
        if (spec != null && (spec.grassGrowth > 0f || AnimalPenSpec.MainKind(spec.kind) == AnimalHabitatKind.Pasture)) p.parasites = Mathf.Max(p.parasites, 60f);
        Changed(book, h, null);
        Debug.Log($"[Animals] Debug: pen '{penId}' is filthy (dirt {p.dirt:0}, water dirt {p.waterDirt:0}, worms {p.parasites:0}).");
    }

    private void OnSceneGUI()
    {
        var pen = (AnimalPen)target;
        if (pen == null || pen.outline == null) return;
        Event e = Event.current;
        Transform t = pen.transform;
        int ground = GUIUtility.GetControlID(GroundHash, FocusType.Passive);
        if (editing && !GroundAreaHandles.Viewing && e.type == EventType.Layout) HandleUtility.AddDefaultControl(ground);
        using (new Handles.DrawingScope(Matrix4x4.TRS(t.position, t.rotation, Vector3.one)))
        {
            Color line = PropertyEditor.PenLine;
            if (e.type == EventType.Repaint)
            {
                GroundAreaHandles.DrawFill(pen.outline, new Color(line.r, line.g, line.b, 0.12f), 0.04f);
                GroundAreaHandles.DrawOutline(pen.outline, line, editing ? 4f : 2.5f, 0.06f);
                GroundAreaHandles.Label(GroundAreaHandles.Centre(pen.outline), $"{pen.DisplayName}\n{pen.kind} · {GroundAreaHandles.SizeText(pen.outline)}", 0.1f);
            }
            if (editing)
            {
                bool changed = GroundAreaHandles.EditPoints(pen, pen.outline, 3, "Animal Pen Outline", line, ground, t, snapStep);
                changed |= GroundAreaHandles.MoveAll(pen, pen.outline, "Move Animal Pen", line, snapStep);
                EditorGUI.BeginChangeCheck();
                Vector3 f = Handles.FreeMoveHandle(new Vector3(pen.feederPoint.x, 0.3f, pen.feederPoint.y), 0.25f, Vector3.zero, Handles.CubeHandleCap);
                Vector3 w = Handles.FreeMoveHandle(new Vector3(pen.troughPoint.x, 0.25f, pen.troughPoint.y), 0.25f, Vector3.zero, Handles.CubeHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(pen, "Move Pen Station");
                    pen.feederPoint = new Vector2(f.x, f.z);
                    pen.troughPoint = new Vector2(w.x, w.z);
                    changed = true;
                }
                if (changed) EditorUtility.SetDirty(pen);
            }
        }
        if (editing) GroundAreaHandles.ReleaseClick(e, ground);
    }
}
#endif
