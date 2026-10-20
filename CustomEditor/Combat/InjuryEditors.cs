#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tools of the injury system: the Injury Settings asset, medical item presets, severed limb items, adding the
/// injury controller to players. Menus: Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Injury Settings and
/// Tools ▸ SimpleMovements ▸ Player ▸ Injuries.
/// </summary>
public static class InjuryEditorTools
{
    public const string MenuRoot = SMMenu.Root + "Player/Injuries/";

    // ------------------------------------------------------------------ settings
    [MenuItem(SMMenu.Root + "Project Setup/Create Injury Settings (Resources)", false, SMMenu.ProjectSetup + 7)]
    public static void CreateSettingsMenu() => CreateSettings(true);

    /// <summary>The project's Injury Settings (created with the defaults - the system on - when missing).</summary>
    public static InjurySettings CreateSettings(bool select)
    {
        InjurySettings s = SMAssetsEditor.FindOrCreate<InjurySettings>(SMAssets.Settings, InjurySettings.ResourcesName, InjuryDefaults.Fill, select);
        InjurySettings.Override(null);
        return s;
    }

    [MenuItem(MenuRoot + "Open Injury Settings", false, SMMenu.Player + 60)]
    private static void OpenSettings() => CreateSettings(true);

    [MenuItem(MenuRoot + "Create Medical Items (Presets)", false, SMMenu.Player + 61)]
    private static void CreateMedicalMenu() => CreateMedicalItems(true);

    [MenuItem(MenuRoot + "Create Severed Limb Items", false, SMMenu.Player + 62)]
    private static void CreateLimbItemsMenu() => CreateLimbItems(CreateSettings(false), true);

    [MenuItem(MenuRoot + "Add Injury Controller To Selection", false, SMMenu.Player + 63)]
    private static void AddToSelection()
    {
        int added = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null || go.GetComponentInChildren<InjuryController>(true) != null)
                continue;
            Undo.AddComponent<InjuryController>(go);
            added++;
        }
        Debug.Log(added > 0 ? $"[Injuries] Added an Injury Controller to {added} object(s)." : "[Injuries] Select a player (or mob) without an Injury Controller.");
    }

    [MenuItem(MenuRoot + "Add Injury Controller To Selection", true)]
    private static bool CanAddToSelection() => Selection.gameObjects.Length > 0;

    [MenuItem(MenuRoot + "Add City Healer (placeholder) To Selection", false, SMMenu.Player + 64)]
    private static void AddHealer()
    {
        foreach (GameObject go in Selection.gameObjects)
            if (go != null && go.GetComponent<CityHealer>() == null)
                Undo.AddComponent<CityHealer>(go);
    }

    [MenuItem(MenuRoot + "Add City Healer (placeholder) To Selection", true)]
    private static bool CanAddHealer() => Selection.gameObjects.Length > 0;

    // ------------------------------------------------------------------ medical items
    /// <summary>Creates (or updates) the medical item presets in Items/Medical. Returns them.</summary>
    public static List<MedicalItemSO> CreateMedicalItems(bool select)
    {
        string folder = SMAssetsEditor.Folder(SMAssets.Medical);
        GameObject model = MedicalModel(folder);
        var made = new List<MedicalItemSO>();
        foreach (InjuryDefaults.MedicalPreset p in InjuryDefaults.MedicalPresets())
        {
            string path = folder + "/" + SMAssetsEditor.CleanFileName(p.name) + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<MedicalItemSO>(path);
            bool created = item == null;
            if (created)
            {
                item = ScriptableObject.CreateInstance<MedicalItemSO>();
                AssetDatabase.CreateAsset(item, path);
            }
            Undo.RecordObject(item, "Medical item preset");
            item.SetPresetInfo(p.name, p.description, p.stackMax, p.weight, p.price);
            item.SetTreatments(p.treatments);
            if (item.Icon == null || item.Prefab == null)
                item.SetLook(item.Icon != null ? item.Icon : MedicalIcon(folder, p.name), item.Prefab != null ? item.Prefab : model);
            EditorUtility.SetDirty(item);
            made.Add(item);
        }
        AssetDatabase.SaveAssets();
        RegisterInItemDatabase(made.Cast<ItemSO>());
        Debug.Log($"[Injuries] {made.Count} medical items in {folder} (Bandage, Sterile Bandage, Suture Kit, Splint, Disinfectant, Antibiotics, Burn Salve, Surgical Kit).");
        if (select && made.Count > 0)
        {
            Selection.objects = made.ToArray();
            EditorGUIUtility.PingObject(made[0]);
        }
        return made;
    }

    /// <summary>Applies preset <paramref name="p"/> to an existing medical item (inspector Presets menu).</summary>
    public static void ApplyPreset(MedicalItemSO item, InjuryDefaults.MedicalPreset p)
    {
        Undo.RecordObject(item, "Medical item preset");
        item.SetPresetInfo(p.name, p.description, p.stackMax, p.weight, p.price);
        item.SetTreatments(p.treatments);
        EditorUtility.SetDirty(item);
    }

    private static GameObject MedicalModel(string folder)
    {
        string path = folder + "/Medical Item.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing;
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Medical Item";
        go.transform.localScale = new Vector3(0.22f, 0.08f, 0.16f);
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
            r.sharedMaterial = SaveMaterial(folder + "/Medical Item.mat", new Color(0.92f, 0.92f, 0.9f));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    /// <summary>A white rounded square with a red cross, coloured by the item's name.</summary>
    private static Sprite MedicalIcon(string folder, string itemName)
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color bg = itemName.Contains("Splint") ? new Color(0.75f, 0.6f, 0.4f) : itemName.Contains("Antibiotic") || itemName.Contains("Disinfect") ? new Color(0.55f, 0.8f, 0.95f)
            : itemName.Contains("Surgical") ? new Color(0.7f, 0.75f, 0.8f) : itemName.Contains("Salve") ? new Color(0.95f, 0.8f, 0.5f) : new Color(0.96f, 0.95f, 0.92f);
        Color cross = new Color(0.85f, 0.12f, 0.15f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, 14f, 50f), cy = Mathf.Clamp(fy, 14f, 50f);
                float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                float a = Mathf.Clamp01(10f - d + 0.5f);
                bool inCross = (Mathf.Abs(fx - 32f) < 6f && Mathf.Abs(fy - 32f) < 18f) || (Mathf.Abs(fy - 32f) < 6f && Mathf.Abs(fx - 32f) < 18f);
                Color c = inCross ? cross : bg;
                c.a = a;
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return SaveSprite(tex, folder + "/" + SMAssetsEditor.CleanFileName(itemName) + " Icon.png");
    }

    // ------------------------------------------------------------------ severed limbs
    /// <summary>
    /// Creates a Severed Limb item (icon, model) for every part of <paramref name="s"/> that can be lost, in Items/Limbs,
    /// and assigns them to the parts (network sessions and saves need them). Returns how many parts got one.
    /// </summary>
    public static int CreateLimbItems(InjurySettings s, bool select)
    {
        if (s == null)
            return 0;
        string folder = SMAssetsEditor.Folder(SMAssets.Limbs);
        Material skin = SaveMaterial(folder + "/Severed Limb.mat", LimbArt.Skin);
        var made = new List<ItemSO>();
        Undo.RecordObject(s, "Create severed limb items");
        foreach (InjuryBodyPart part in s.bodyParts)
        {
            if (part == null || part.limbLoss == null || !part.limbLoss.canBeLost)
                continue;
            string file = SMAssetsEditor.CleanFileName("Severed " + part.Name);
            string path = folder + "/" + file + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<SeveredLimbItemSO>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<SeveredLimbItemSO>();
                AssetDatabase.CreateAsset(item, path);
            }
            Sprite icon = item.Icon != null ? item.Icon : SaveSprite(LimbIconTexture(part.kind), folder + "/" + file + " Icon.png");
            GameObject model = item.Prefab != null ? item.Prefab : LimbModel(folder + "/" + file + ".prefab", part.kind, skin);
            item.Setup(part.id, part.Name, icon, model, SeveredLimbItemSO.DefaultWeight(part.kind));
            EditorUtility.SetDirty(item);
            part.limbLoss.limbItem = item;
            made.Add(item);
        }
        EditorUtility.SetDirty(s);
        AssetDatabase.SaveAssets();
        RegisterInItemDatabase(made);
        Debug.Log($"[Injuries] {made.Count} severed limb items in {folder}, assigned to their body parts.", s);
        if (select && made.Count > 0)
            EditorGUIUtility.PingObject(made[0]);
        return made.Count;
    }

    private static Texture2D LimbIconTexture(BodyPartKind kind)
    {
        Sprite runtime = LimbArt.Icon(kind);
        var tex = new Texture2D(runtime.texture.width, runtime.texture.height, TextureFormat.RGBA32, false);
        tex.SetPixels(runtime.texture.GetPixels());
        tex.Apply();
        return tex;
    }

    private static GameObject LimbModel(string path, BodyPartKind kind, Material skin)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = Path.GetFileNameWithoutExtension(path);
        Vector2 size = LimbArt.Size(kind);
        go.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.x);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Renderer r = go.GetComponent<Renderer>();
        if (r != null && skin != null)
            r.sharedMaterial = skin;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ------------------------------------------------------------------ helpers
    private static Material SaveMaterial(string path, Color color)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;
        var m = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static Sprite SaveSprite(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>Adds items to the project's Item Database (stable ids for the network and saves) when there is one.</summary>
    /// <summary>Gives new items an id in the project's Item Database (saves, network), when it has one.</summary>
    public static void RegisterInItemDatabase(IEnumerable<ItemSO> items)
    {
        ItemDatabase db = SMAssets.Load<ItemDatabase>(SMAssets.Databases, ItemDatabase.ResourcesName);
        if (db == null)
            return;
        Undo.RecordObject(db, "Register items");
        foreach (ItemSO item in items)
        {
            if (item == null || !string.IsNullOrEmpty(db.IdOf(item)))
                continue;
            db.Register(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item)), item);
        }
        EditorUtility.SetDirty(db);
    }

    /// <summary>Medical items of the project.</summary>
    public static List<MedicalItemSO> FindMedicalItems()
    {
        var list = new List<MedicalItemSO>();
        foreach (string guid in AssetDatabase.FindAssets("t:MedicalItemSO"))
        {
            var m = AssetDatabase.LoadAssetAtPath<MedicalItemSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (m != null) list.Add(m);
        }
        return list;
    }

    /// <summary>Abilities of the project with a Treat Injuries effect (scans their serialized effects).</summary>
    public static List<AbilityDefinition> FindHealingAbilities()
    {
        var list = new List<AbilityDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:AbilityDefinition"))
        {
            var a = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (a == null)
                continue;
            SerializedProperty it = new SerializedObject(a).GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType == SerializedPropertyType.ManagedReference &&
                    !string.IsNullOrEmpty(it.managedReferenceFullTypename) && it.managedReferenceFullTypename.EndsWith(nameof(TreatInjuriesEffect)))
                {
                    list.Add(a);
                    break;
                }
            }
        }
        return list;
    }
}

/// <summary>
/// Inspector of the Injury Settings: the on/off switch and problems on top, then one page per area - general rules,
/// body parts (with the health panel's body diagram), injury types and severities, treatments (the medical items and
/// healing abilities of the project), limb loss and reattachment, and the city treatment placeholder.
/// </summary>
[CustomEditor(typeof(InjurySettings))]
public class InjurySettingsEditor : Editor
{
    private static int page;
    private static readonly string[] Pages = { "General", "Body Parts", "Injuries", "Treatments", "Limbs", "Movement", "City" };
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private double nextValidation;
    private List<MedicalItemSO> medical;
    private List<AbilityDefinition> abilities;

    private InjurySettings Settings => (InjurySettings)target;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        InjurySettings s = Settings;
        if (EditorApplication.timeSinceStartup >= nextValidation)
        {
            nextValidation = EditorApplication.timeSinceStartup + 0.5;
            errors.Clear();
            warnings.Clear();
            s.Validate(errors, warnings);
        }

        DrawSwitch(s);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        if (!AssetDatabase.GetAssetPath(s).Contains("/Resources/"))
            EditorGUILayout.HelpBox("This asset is outside every Resources folder: the game does not load it. Tools ▸ SimpleMovements ▸ Project Setup ▸ " +
                                    "Create Injury Settings moves it to Resources/SimpleMovements/Settings.", MessageType.Warning);

        EditorGUILayout.Space(4);
        page = GUILayout.Toolbar(page, Pages);
        EditorGUILayout.Space(4);
        switch (page)
        {
            case 0: DrawGeneral(); break;
            case 1: DrawBody(s); break;
            case 2: DrawInjuries(s); break;
            case 3: DrawTreatments(s); break;
            case 4: DrawLimbs(s); break;
            case 5: DrawMovement(s); break;
            case 6: DrawCity(); break;
        }
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Reset Everything To Defaults (Project Zomboid-like)") &&
            EditorUtility.DisplayDialog("Reset injury settings", "Replace every body part, injury type and rule with the defaults?", "Reset", "Cancel"))
        {
            Undo.RecordObject(s, "Reset injury settings");
            InjuryDefaults.Fill(s);
            EditorUtility.SetDirty(s);
        }
    }

    private void DrawSwitch(InjurySettings s)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        var big = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        EditorGUILayout.LabelField(s.enabled ? "Injury system: ON" : "Injury system: OFF", big);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enabled"), new GUIContent("Enabled"));
        EditorGUILayout.LabelField(s.enabled
            ? $"{s.bodyParts.Count} body parts, {s.injuryTypes.Count} injury types, {s.severityLevels.Count} severities. Players{(s.mobs ? " and mobs" : "")} get an Injury Controller automatically; " +
              "the Health tab appears in the inventory's character panel."
            : "No body-part condition, injuries, bleeding, limb loss or Health tab. Medical items do nothing.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
    }

    private void Props(params string[] names)
    {
        foreach (string n in names)
        {
            SerializedProperty p = serializedObject.FindProperty(n);
            if (p != null) EditorGUILayout.PropertyField(p, true);
        }
    }

    // ------------------------------------------------------------------ pages
    private void DrawGeneral()
    {
        Props("players", "mobs", "mainHandSide", "conditionPerDamage", "injuryChanceMultiplier", "maxInjuriesPerHit", "healingSpeedMultiplier",
            "clearOnRespawn", "showHealthPanel", "showHudAlert");
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Treatment", EditorStyles.boldLabel);
        Props("treatmentTimeMultiplier", "damageInterruptsTreatment", "treatmentNeedsAHand");
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Bleeding", EditorStyles.boldLabel);
        Props("bleeding");
        EditorGUILayout.Space(6);
        Props("logInjuries");
    }

    private void DrawBody(InjurySettings s)
    {
        EditorGUILayout.HelpBox("Each part has its own condition. Hits land on parts from the Body Part Controller's location (Hit Parts) or the hit's " +
                                "height (Height Range); left / right parts by the side of the hit. Impaired parts apply their consequence and stop " +
                                "actions; Attached To loses children with their parent (a hand with its arm).", MessageType.None);
        EditorGUILayout.BeginHorizontal();
        Rect r = GUILayoutUtility.GetRect(200f, 280f, GUILayout.Width(200f), GUILayout.Height(280f));
        DrawDiagram(r, s);
        EditorGUILayout.BeginVertical();
        foreach (InjuryBodyPart p in s.bodyParts)
        {
            if (p == null) continue;
            var bits = new List<string> { p.kind.ToString() };
            if (p.side != BodyPartSide.Center) bits.Add(p.side.ToString());
            if (p.limbLoss != null && p.limbLoss.canBeLost) bits.Add($"lost at {p.limbLoss.singleHitThreshold * 100f:0}% hit");
            if (!string.IsNullOrEmpty(p.attachedTo)) bits.Add("on " + p.attachedTo);
            EditorGUILayout.LabelField($"{p.Name}: {string.Join(" · ", bits)}", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        Props("defaultPart", "bodyParts");
        if (GUILayout.Button("Reset Body Parts To Defaults"))
        {
            Undo.RecordObject(s, "Reset body parts");
            s.bodyParts = InjuryDefaults.BodyParts();
            EditorUtility.SetDirty(s);
        }
    }

    private static void DrawDiagram(Rect r, InjurySettings s)
    {
        EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.2f));
        var style = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
        for (int i = 0; i < s.bodyParts.Count; i++)
        {
            InjuryBodyPart p = s.bodyParts[i];
            if (p == null) continue;
            Rect d = p.diagramRect;
            var box = new Rect(r.x + d.x * r.width, r.y + (1f - d.yMax) * r.height, d.width * r.width, d.height * r.height);
            BodyPartLayout.Region region = BodyPartLayout.Classify(p.id);
            Color c = region != BodyPartLayout.Region.Other ? BodyPartLayout.RegionColor(region) : BodyPartController.PartColor(i);
            EditorGUI.DrawRect(box, new Color(c.r, c.g, c.b, p.limbLoss != null && p.limbLoss.canBeLost ? 0.65f : 0.4f));
            GUI.Label(box, p.Name.Replace(" ", "\n"), style);
        }
        GUI.Label(new Rect(r.x + 2f, r.yMax - 14f, r.width, 14f), "front view (as in the Health tab)", EditorStyles.centeredGreyMiniLabel);
    }

    /// <summary>Is this an asset made before damage forms (built-in types without their odds by form, new types missing)?</summary>
    private static bool NeedsDamageForms(InjurySettings s)
    {
        foreach (InjuryTypeDefinition d in InjuryDefaults.InjuryTypes())
        {
            InjuryTypeDefinition t = s.FindType(d.id);
            if (t == null)
                return true;
            if (t.trigger != null && d.trigger != null && !d.trigger.formChances.IsNeutral &&
                (t.trigger.formChances == null || t.trigger.formChances.IsNeutral))
                return true;
        }
        return false;
    }

    private static void ApplyDamageForms(InjurySettings s)
    {
        Undo.RecordObject(s, "Apply damage form defaults");
        List<string> done = InjuryDefaults.ApplyDamageForms(s);
        EditorUtility.SetDirty(s);
        EditorUtility.DisplayDialog("Damage forms", done.Count == 0 ? "Nothing to change." : "• " + string.Join("\n• ", done), "OK");
    }

    private void DrawInjuries(InjurySettings s)
    {
        if (NeedsDamageForms(s))
        {
            EditorGUILayout.HelpBox("This asset was made before damage forms: slashes, piercing and blunt hits cause the same injuries. Apply the " +
                                    "defaults to give the built-in injuries their odds by form (slashes cut, maces break bones...), add the new " +
                                    "injuries (bruise, puncture, bite wound, concussion, frostbite) and the severing odds of the limbs. Your other " +
                                    "settings are kept.", MessageType.Info);
            if (GUILayout.Button("Apply Damage Form Defaults"))
                ApplyDamageForms(s);
        }
        Props("severityLevels");
        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox("Hits try the types in order (at most Max Injuries Per Hit). A hit creates the strongest stage it reaches. Stages bleed, " +
                                "impair, heal (faster when treated), get worse when untreated, and apply their consequence.", MessageType.None);
        foreach (InjuryTypeDefinition t in s.injuryTypes)
        {
            if (t == null) continue;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(t.Name + (t.tags.Count > 0 ? $"   [{string.Join(", ", t.tags)}]" : ""), EditorStyles.boldLabel);
            if (t.causedByHits && t.trigger != null)
                EditorGUILayout.LabelField($"Hits: {t.trigger.damageKinds} ≥ {t.trigger.minHitFraction * 100f:0}% of the part, {t.trigger.chance * 100f:0}% chance" +
                                           (t.trigger.elements.Count > 0 ? ", " + string.Join("/", t.trigger.elements) : ""), EditorStyles.miniLabel);
            else
                EditorGUILayout.LabelField("Not caused by hits (progression, limb loss, scripts, abilities).", EditorStyles.miniLabel);
            if (t.causedByHits && t.trigger != null && t.trigger.formChances != null && !t.trigger.formChances.IsNeutral)
                EditorGUILayout.LabelField("By form: " + t.trigger.formChances.Describe(), EditorStyles.wordWrappedMiniLabel);
            for (int i = 0; i < t.stages.Count; i++)
            {
                InjuryStage st = t.stages[i];
                if (st == null) continue;
                var bits = new List<string> { $"{t.StageName(i)} ({s.SeverityName(st.severity)})" };
                if (st.bleedingPerSecond > 0f) bits.Add($"bleeds {st.bleedingPerSecond:0.##}/s" + (st.clotsAfterSeconds > 0f ? $" for {st.clotsAfterSeconds:0}s" : " until treated"));
                if (st.impairment > 0f) bits.Add($"impairs {st.impairment * 100f:0}%");
                bits.Add(st.needsTreatmentToHeal ? $"heals {st.healSeconds:0}s once treated" : $"heals {st.healSeconds:0}s");
                if (st.worsenAfterSeconds > 0f) bits.Add($"worse after {st.worsenAfterSeconds:0}s{(string.IsNullOrEmpty(st.worsensInto) ? "" : " → " + st.worsensInto)}");
                EditorGUILayout.LabelField("  • " + string.Join(", ", bits), EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndVertical();
        }
        Props("injuryTypes");
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Injury Types To Defaults"))
        {
            Undo.RecordObject(s, "Reset injury types");
            s.injuryTypes = InjuryDefaults.InjuryTypes();
            EditorUtility.SetDirty(s);
        }
        if (GUILayout.Button(new GUIContent("Apply Damage Form Defaults", "Odds by form on the built-in injuries, the missing new injuries, the limbs' " +
                                                                          "severing odds. Everything else is kept.")))
            ApplyDamageForms(s);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTreatments(InjurySettings s)
    {
        Props("treatmentTimeMultiplier", "damageInterruptsTreatment", "treatmentNeedsAHand");
        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox("Medical items hold treatments (what they treat by injury type, tag, severity and body part, and what they do: stop " +
                                "bleeding, splint, stitch, cure, restore condition, reattach a limb). Abilities treat with the Injury ▸ Treat Injuries " +
                                "effect (it can ignore a lost limb's time limit). Players apply items from the Health tab, or from the hotbar.", MessageType.None);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Create Medical Item Presets"))
            medical = InjuryEditorTools.CreateMedicalItems(true);
        if (GUILayout.Button("Refresh Lists"))
        {
            medical = InjuryEditorTools.FindMedicalItems();
            abilities = InjuryEditorTools.FindHealingAbilities();
        }
        EditorGUILayout.EndHorizontal();
        if (medical == null)
            medical = InjuryEditorTools.FindMedicalItems();
        EditorGUILayout.LabelField($"Medical items ({medical.Count})", EditorStyles.boldLabel);
        if (medical.Count == 0)
            EditorGUILayout.LabelField("None yet: Create Medical Item Presets, or Assets ▸ Create ▸ SimpleMovements ▸ Items ▸ Medical Item.", EditorStyles.wordWrappedMiniLabel);
        foreach (MedicalItemSO m in medical)
        {
            if (m == null) continue;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.ObjectField(m, typeof(MedicalItemSO), false);
            foreach (InjuryTreatment t in m.Treatments)
                if (t != null) EditorGUILayout.LabelField(t.Describe(s), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Healing abilities", EditorStyles.boldLabel);
        if (abilities == null)
            EditorGUILayout.LabelField("Press Refresh Lists to find abilities with a Treat Injuries effect.", EditorStyles.wordWrappedMiniLabel);
        else if (abilities.Count == 0)
            EditorGUILayout.LabelField("None: add Injury ▸ Treat Injuries to an ability's effects.", EditorStyles.wordWrappedMiniLabel);
        else
            foreach (AbilityDefinition a in abilities)
                EditorGUILayout.ObjectField(a, typeof(AbilityDefinition), false);
    }

    private void DrawLimbs(InjurySettings s)
    {
        Props("reattachment");
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Cleanup", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Lost limbs and the armour / items that fell with them are removed from the world after a while (picked up, " +
                                   "they stay in the inventory), so the world does not fill up with objects.", EditorStyles.wordWrappedMiniLabel);
        Props("cleanup");
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Parts that can be lost", EditorStyles.boldLabel);
        bool missingItems = false;
        foreach (InjuryBodyPart p in s.bodyParts)
        {
            if (p == null || p.limbLoss == null || !p.limbLoss.canBeLost) continue;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{p.Name}: one hit ≥ {p.limbLoss.singleHitThreshold * 100f:0}%{(p.limbLoss.atZeroCondition ? " or 0 condition" : "")}, " +
                                       $"{p.limbLoss.chance * 100f:0}%, reattach within {s.TimeLimitFor(p):0}s", EditorStyles.miniLabel);
            var item = (SeveredLimbItemSO)EditorGUILayout.ObjectField(p.limbLoss.limbItem, typeof(SeveredLimbItemSO), false, GUILayout.Width(150f));
            if (item != p.limbLoss.limbItem)
            {
                Undo.RecordObject(s, "Limb item");
                p.limbLoss.limbItem = item;
                EditorUtility.SetDirty(s);
            }
            if (item == null) missingItems = true;
            EditorGUILayout.EndHorizontal();
        }
        if (missingItems)
            EditorGUILayout.HelpBox("Parts without a Severed Limb item get one made at runtime: fine offline, but a network session cannot drop those limbs.", MessageType.Info);
        if (GUILayout.Button("Create Severed Limb Items (icons, models) For Every Part"))
            InjuryEditorTools.CreateLimbItems(s, true);
    }

    private void DrawMovement(InjurySettings s)
    {
        EditorGUILayout.HelpBox("How the legs let the character move. A hurt leg or foot makes it limp (the leg's consequence slows it; past " +
                                "Pain From Limp walking hurts); a missing foot means walking on the stump; a leg that cannot carry weight " +
                                "(missing, or a compound fracture) means hopping on the other, in bursts; no usable leg means crawling with " +
                                "the arms. Without legs the collider shrinks to the waist and the model is lowered. The player's Animator " +
                                "gets LocomotionMode, IsLimping, LimpAmount, LimpSide, IsHopping, HopCycle, IsCrawling, IsLegless, MissingLegs " +
                                "and MissingArms: Tools ▸ SimpleMovements ▸ Player ▸ Complete Player Animator Controller adds them with empty " +
                                "Limp, Hop and Crawl states for your clips.", MessageType.None);
        Props("locomotion");
    }

    private void DrawCity()
    {
        EditorGUILayout.HelpBox("PLACEHOLDER. City healers are not implemented yet. The settings below, the City treatment source, the " +
                                "ICityTreatmentProvider interface and the City Healer component (Tools ▸ SimpleMovements ▸ Player ▸ Injuries) are " +
                                "ready for them; today a City Healer only tells players the note below.", MessageType.Info);
        Props("cityTreatment");
    }
}

/// <summary>The injury controller while playing: every part's condition and injuries, with test buttons.</summary>
[CustomEditor(typeof(InjuryController))]
public class InjuryControllerEditor : Editor
{
    private int testPart;
    private int testType;
    private int testStage;
    private float testDamage = 20f;
    private DamageForm testForm = DamageForm.Slash;

    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var c = (InjuryController)target;
        InjurySettings s = c.Settings;
        if (!s.enabled)
        {
            EditorGUILayout.HelpBox("The injury system is off (Injury Settings ▸ Enabled).", MessageType.Info);
            if (GUILayout.Button("Open Injury Settings"))
                InjuryEditorTools.CreateSettings(true);
            return;
        }
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox($"Tracks {s.bodyParts.Count} body parts. Enter Play Mode to see them and test injuries.", MessageType.None);
            return;
        }

        foreach (InjuryPartState p in c.Parts)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 18f);
            string label = p.Lost ? $"{p.Name}: LOST" : $"{p.Name}: {p.Condition:0}/{p.MaxCondition:0}" + (p.Impairment > 0.01f ? $"  (impaired {p.Impairment * 100f:0}%)" : "");
            EditorGUI.ProgressBar(r, p.Condition01, label);
            foreach (Injury i in p.Injuries)
                EditorGUILayout.LabelField($"   • {i.Name} ({s.SeverityName(i.Severity)})" + (i.IsBleeding ? $" bleeding {i.BleedingPerSecond:0.##}/s" : "") +
                                           (i.Treated || i.Bandaged ? $" [{i.TreatmentLabel}]" : "") + $" heal {i.HealProgress * 100f:0}%", EditorStyles.miniLabel);
        }
        EditorGUILayout.LabelField($"Bleeding {c.BleedingPerSecond:0.##}/s · stops: {BodyActions.DescribeAll(c.Blocked)}", EditorStyles.wordWrappedMiniLabel);
        string resist = c.Resistances != null ? c.Resistances.Describe() : "";
        if (!string.IsNullOrEmpty(resist))
            EditorGUILayout.LabelField("Resistances:\n" + resist, EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Test", EditorStyles.boldLabel);
        string[] partNames = c.Parts.Select(p => p.Name).ToArray();
        if (partNames.Length == 0)
            return;
        testPart = EditorGUILayout.Popup("Part", Mathf.Clamp(testPart, 0, partNames.Length - 1), partNames);
        InjuryPartState part = c.Parts[Mathf.Clamp(testPart, 0, c.Parts.Count - 1)];
        EditorGUILayout.BeginHorizontal();
        testDamage = EditorGUILayout.FloatField("Hit damage", testDamage);
        testForm = (DamageForm)EditorGUILayout.EnumPopup(testForm, GUILayout.Width(70f));
        if (GUILayout.Button("Hit", GUILayout.Width(60f)) && c.Entity != null)
            c.ApplyHit(part, new DamageInfo { amount = testDamage, target = c.Entity, point = c.LimbPosition(part), type = DamageType.Physical, delivery = DamageDelivery.Melee, form = testForm });
        EditorGUILayout.EndHorizontal();
        string[] typeNames = s.injuryTypes.Select(t => t != null ? t.Name : "?").ToArray();
        if (typeNames.Length > 0)
        {
            EditorGUILayout.BeginHorizontal();
            testType = EditorGUILayout.Popup("Injury", Mathf.Clamp(testType, 0, typeNames.Length - 1), typeNames);
            testStage = EditorGUILayout.IntField(testStage, GUILayout.Width(40f));
            if (GUILayout.Button("Add", GUILayout.Width(60f)))
                c.AddInjury(part, s.injuryTypes[Mathf.Clamp(testType, 0, typeNames.Length - 1)], testStage, null, false);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(part.Lost || part.Def.limbLoss == null || !part.Def.limbLoss.canBeLost))
            if (GUILayout.Button("Lose This Limb"))
                c.LoseLimb(part.Id);
        if (GUILayout.Button("Heal All"))
            c.HealAll(true);
        EditorGUILayout.EndHorizontal();
    }
}

/// <summary>The injury resistances of a character: its own list, creature presets, and what is in effect while playing.</summary>
[CustomEditor(typeof(InjuryResistances))]
public class InjuryResistancesEditor : Editor
{
    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Percent changes: negative = better for this character (fewer, milder injuries, less bleeding, less damage). Traits, " +
                                "races, classes, items and buffs add their own while playing (and the character stats Wound Chance, Fracture " +
                                "Chance, Bleeding, Slash / Pierce / Blunt Damage Taken...).", MessageType.None);
        DrawDefaultInspector();
        var r = (InjuryResistances)target;
        if (GUILayout.Button("Add Creature Preset..."))
        {
            var menu = new GenericMenu();
            foreach (InjuryDefaults.ResistancePreset p in InjuryDefaults.ResistancePresets())
            {
                InjuryDefaults.ResistancePreset captured = p;
                menu.AddItem(new GUIContent(p.name, p.description), false, () =>
                {
                    Undo.RecordObject(r, "Add injury resistance preset");
                    r.Innate.AddRange(captured.resistances);
                    r.NotifyChanged();
                    EditorUtility.SetDirty(r);
                });
            }
            menu.ShowAsContext();
        }
        if (Application.isPlaying)
        {
            string text = r.Describe();
            EditorGUILayout.LabelField("In effect", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.IsNullOrEmpty(text) ? "Nothing." : text, EditorStyles.wordWrappedMiniLabel);
        }
    }
}

/// <summary>Medical items: the generic item inspector plus medical presets and what the item treats.</summary>
[CustomEditor(typeof(MedicalItemSO), true)]
[CanEditMultipleObjects]
public class MedicalItemSOEditor : ItemSOEditor
{
    protected override void DrawToolsTop()
    {
        if (!InjurySettings.Instance.enabled)
            EditorGUILayout.HelpBox("The injury system is off in this project (Injury Settings): medical items do nothing until it is on.", MessageType.Info);
    }

    protected override void AddPresets(GenericMenu menu)
    {
        foreach (InjuryDefaults.MedicalPreset p in InjuryDefaults.MedicalPresets())
        {
            InjuryDefaults.MedicalPreset captured = p;
            menu.AddItem(new GUIContent("Medical/" + p.name), false, () =>
            {
                foreach (Object o in targets)
                    if (o is MedicalItemSO m) InjuryEditorTools.ApplyPreset(m, captured);
            });
        }
    }
}
#endif
