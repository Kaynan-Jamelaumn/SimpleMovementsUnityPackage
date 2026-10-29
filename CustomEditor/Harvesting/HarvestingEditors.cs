#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SimpleMovements.Validation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Menus of the harvesting system (Tools ▸ SimpleMovements ▸ Harvesting): the project's Harvest Database, making mobs
/// harvestable (with their network component when Fish-Net is in the project), practice carcasses to try the work on,
/// editable creature anatomy assets, harvesting tools and item assets with generated icons, validation (with the
/// Crafting and Alchemy databases the materials feed) and the documentation.
/// </summary>
public static class HarvestingEditorTools
{
    private const string Menu = SMMenu.Root + "Harvesting/";
    private const string Practice = Menu + "Create Practice Carcass/";
    private const string IconFolder = SMAssets.HarvestItems + "/Icons";

    [MenuItem(SMMenu.Root + "Project Setup/Create Harvest Database (Resources)", false, SMMenu.ProjectSetup + 10)]
    private static void CreateDatabaseSetupMenu() => ProjectDatabase(true);

    [MenuItem(Menu + "Create Harvest Database", false, SMMenu.Harvesting)]
    private static void CreateDatabaseMenu() => ProjectDatabase(true);

    [MenuItem(Menu + "Make Selected Mob Harvestable", false, SMMenu.Harvesting + 1)]
    private static void MakeHarvestableMenu()
    {
        var report = new StringBuilder();
        foreach (GameObject go in Selection.gameObjects)
            report.AppendLine(MakeHarvestable(go));
        if (report.Length == 0)
            report.Append("Select mobs in the scene or mob prefabs in the Project window first.");
        EditorUtility.DisplayDialog("Harvesting", report.ToString().TrimEnd(), "OK");
    }

    [MenuItem(Menu + "Make Selected Mob Harvestable", true)]
    private static bool MakeHarvestableValidate() => Selection.gameObjects.Length > 0;

    [MenuItem(Practice + "Bear (four legs)", false, SMMenu.Harvesting + 2)] private static void PracticeBear() => CreatePracticeCarcass("bear");
    [MenuItem(Practice + "Wolf", false, SMMenu.Harvesting + 3)] private static void PracticeWolf() => CreatePracticeCarcass("wolf");
    [MenuItem(Practice + "Deer", false, SMMenu.Harvesting + 4)] private static void PracticeDeer() => CreatePracticeCarcass("deer");
    [MenuItem(Practice + "Drake (four legs and wings)", false, SMMenu.Harvesting + 5)] private static void PracticeDrake() => CreatePracticeCarcass("drake");
    [MenuItem(Practice + "Troll (two legs)", false, SMMenu.Harvesting + 6)] private static void PracticeTroll() => CreatePracticeCarcass("troll");
    [MenuItem(Practice + "Eagle (winged)", false, SMMenu.Harvesting + 7)] private static void PracticeEagle() => CreatePracticeCarcass("eagle");
    [MenuItem(Practice + "Salmon (fish)", false, SMMenu.Harvesting + 8)] private static void PracticeSalmon() => CreatePracticeCarcass("salmon");
    [MenuItem(Practice + "Giant Serpent", false, SMMenu.Harvesting + 9)] private static void PracticeSerpent() => CreatePracticeCarcass("giant_serpent");
    [MenuItem(Practice + "Giant Spider (many legs)", false, SMMenu.Harvesting + 10)] private static void PracticeSpider() => CreatePracticeCarcass("giant_spider");

    [MenuItem(Menu + "Create Creature Anatomy Assets", false, SMMenu.Harvesting + 20)]
    private static void AnatomiesMenu()
    {
        List<CreatureAnatomy> made = CreateAnatomyAssets(ProjectDatabase(false), true);
        EditorUtility.DisplayDialog("Harvesting", $"{made.Count} creature anatomy asset(s) in {SMAssets.FolderPath(SMAssets.Anatomies)}, registered in the " +
            "Harvest Database. Edit them (or copy one for a new creature): an asset replaces the built-in creature with its id.", "OK");
    }

    [MenuItem(Menu + "Create Tools", false, SMMenu.Harvesting + 21)]
    private static void ToolsMenu()
    {
        List<HarvestToolSO> made = CreateTools(ProjectDatabase(false), true);
        EditorUtility.DisplayDialog("Harvesting", $"{made.Count} harvesting tool(s) in {SMAssets.FolderPath(SMAssets.HarvestTools)}. Give them to players " +
            "(merchants, starting items, loot): the harvest picks the fittest tool in the bag for each piece of work.", "OK");
    }

    [MenuItem(Menu + "Create Items", false, SMMenu.Harvesting + 22)]
    private static void ItemsMenu()
    {
        int n = CreateItems(ProjectDatabase(false), true);
        EditorUtility.DisplayDialog("Harvesting", $"{n} item asset(s) in {SMAssets.FolderPath(SMAssets.HarvestItems)}, bound in the Harvest Database. " +
            "Without them every material still gives an item made at runtime; assets let you give them your own icons, models and prices, " +
            "and use them in recipes, merchants and quests.", "OK");
    }

    [MenuItem(Menu + "Validate Harvest Database", false, SMMenu.Harvesting + 40)]
    private static void ValidateMenu()
    {
        HarvestDatabase db = HarvestDatabase.Instance;
        var errors = new List<string>();
        var warnings = new List<string>();
        db.MarkChanged();
        Validate(db, errors, warnings);
        foreach (string e in errors) Debug.LogError("[Harvesting] " + e, db);
        foreach (string w in warnings) Debug.LogWarning("[Harvesting] " + w, db);
        Debug.Log($"[Harvesting] {db.name}: {db.Anatomies.Count} creature(s), {errors.Count} error(s), {warnings.Count} warning(s).", db);
    }

    [MenuItem(Menu + "Add Harvested Hides to the Crafting Database", false, SMMenu.Harvesting + 41)]
    private static void HidesMenu()
    {
        int n = AddMissingCraftHides();
        EditorUtility.DisplayDialog("Harvesting", n > 0
            ? $"Added {n} hide(s) to the project's Crafting Database: skinned creatures now give hides for the tannery."
            : "Nothing to add: the Crafting Database has every hide the creatures give (or the project uses the built-in crafting).", "OK");
    }

    [MenuItem(Menu + "Help", false, SMMenu.Harvesting + 60)]
    private static void HelpMenu()
    {
        foreach (string g in AssetDatabase.FindAssets("README"))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (path.Replace('\\', '/').Contains("Documentation/Harvesting/"))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(path);
                EditorGUIUtility.PingObject(Selection.activeObject);
                return;
            }
        }
        EditorUtility.DisplayDialog("Harvesting", "See Documentation/Harvesting/README.md.", "OK");
    }

    // ------------------------------------------------------------------ the database
    /// <summary>The project's Harvest Database (Resources/SimpleMovements/Settings), with the default rules when made.</summary>
    public static HarvestDatabase ProjectDatabase(bool select)
    {
        HarvestDatabase db = SMAssetsEditor.FindOrCreate<HarvestDatabase>(SMAssets.Settings, HarvestDatabase.ResourcesName, d => d.settings = HarvestSettings.Defaults(), select);
        HarvestDatabase.Override(null);
        return db;
    }

    /// <summary>The database's own checks, and what the creatures need from the Crafting and Alchemy databases.</summary>
    public static void Validate(HarvestDatabase db, List<string> errors, List<string> warnings)
    {
        db.Validate(errors, warnings);
        foreach (string id in MissingCraftHides())
            warnings.Add($"The Crafting Database has no hide '{id}': those skins become plain hide items, not hides for the tannery " +
                         "(Harvesting ▸ Add Harvested Hides to the Crafting Database).");
        CheckAlchemy(db, warnings);
    }

    /// <summary>Substances the creatures release that the Alchemy Database does not know (a warning for each).</summary>
    public static void CheckAlchemy(HarvestDatabase db, List<string> warnings)
    {
        AlchemyRules alchemy = AlchemyDatabase.Instance.Rules;
        var reported = new HashSet<string>();
        foreach (HarvestAnatomyData a in db.Anatomies)
        foreach (HarvestMaterial m in a.materials)
        {
            if (m?.alchemy == null) continue;
            foreach (HarvestAlchemyRelease r in m.alchemy)
                if (r != null && !string.IsNullOrWhiteSpace(r.substance) && alchemy.Substance(r.substance.Trim()) == null && reported.Add(r.substance))
                    warnings.Add($"{a.Name} {m.Name}: the Alchemy Database has no substance '{r.substance}' (it gives nothing in a vessel).");
        }
    }

    /// <summary>Hides the creatures give that the Crafting Database does not know (by id).</summary>
    public static List<string> MissingCraftHides()
    {
        var missing = new List<string>();
        CraftRules rules = CraftDatabase.Instance.Rules;
        foreach (HarvestAnatomyData a in HarvestDatabase.Instance.Anatomies)
        foreach (HarvestMaterial m in a.materials)
        {
            if (m == null || string.IsNullOrWhiteSpace(m.craftHide)) continue;
            string id = m.craftHide.Trim();
            if (rules.Hide(id) == null && !missing.Contains(id))
                missing.Add(id);
        }
        return missing;
    }

    /// <summary>
    /// Adds the built-in definitions of the hides the creatures give and the project's Crafting Database lacks (a database
    /// made before harvesting has no troll hide or snakeskin). Returns how many were added.
    /// </summary>
    public static int AddMissingCraftHides()
    {
        CraftDatabase db = SMAssets.Load<CraftDatabase>(SMAssets.Settings, CraftDatabase.ResourcesName);
        if (db == null)
            return 0;
        List<string> missing = MissingCraftHides();
        if (missing.Count == 0)
            return 0;
        Undo.RecordObject(db, "Add Harvested Hides");
        db.content ??= new CraftContent();
        int added = 0;
        foreach (CraftHideDefinition h in CraftDefaults.Hides().Concat(HarvestCraftHides.All()))
            if (missing.Contains(h.id) && !db.content.hides.Any(x => x != null && x.id == h.id))
            {
                db.content.hides.Add(h);
                added++;
            }
        if (added > 0)
        {
            db.MarkChanged();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }
        return added;
    }

    // ------------------------------------------------------------------ mobs
    /// <summary>
    /// Makes a mob harvestable: a Harvestable Creature next to its status controller (its anatomy found from the mob, or
    /// <paramref name="anatomyId"/>), and a NetCarcass when it is a network object. Works on scene objects and prefab
    /// assets. Returns what was done, in words.
    /// </summary>
    public static string MakeHarvestable(GameObject go, string anatomyId = null)
    {
        if (go == null)
            return "";
        string path = AssetDatabase.GetAssetPath(go);
        if (!string.IsNullOrEmpty(path))
        {
            string result = "";
            ValidationUtility.EditPrefab(path, root =>
            {
                result = Apply(root, anatomyId, false, out bool changed);
                return changed;
            });
            return result;
        }
        return Apply(go, anatomyId, true, out _);
    }

    private static string Apply(GameObject root, string anatomyId, bool undo, out bool changed)
    {
        changed = false;
        MobStatusController status = root.GetComponentInChildren<MobStatusController>(true);
        CombatEntity entity = root.GetComponentInChildren<CombatEntity>(true);
        Mob mob = root.GetComponentInChildren<Mob>(true);
        // Next to the status controller: it asks the creature whether to keep the body when the mob dies.
        GameObject at = status != null ? status.gameObject : entity != null ? entity.gameObject : mob != null ? mob.gameObject : root;
        var notes = new List<string>();
        HarvestableCreature creature = at.GetComponent<HarvestableCreature>();
        if (creature == null)
        {
            creature = undo ? Undo.AddComponent<HarvestableCreature>(at) : at.AddComponent<HarvestableCreature>();
            changed = true;
            notes.Add("added a Harvestable Creature");
        }
        if (!string.IsNullOrWhiteSpace(anatomyId))
        {
            var so = new SerializedObject(creature);
            so.FindProperty("anatomyId").stringValue = anatomyId.Trim();
            so.ApplyModifiedProperties();
            changed = true;
        }
        Type nob = SimpleMovements.Setup.SetupUtil.TypeByName("FishNet.Object.NetworkObject");
        Type net = SimpleMovements.Setup.SetupUtil.TypeByName("GameNet.NetCarcass");
        if (nob != null && net != null && at.GetComponentInParent(nob, true) != null && at.GetComponent(net) == null)
        {
            if (undo) Undo.AddComponent(at, net);
            else at.AddComponent(net);
            changed = true;
            notes.Add("added a NetCarcass (clients harvest through the server)");
        }
        if (status == null)
            notes.Add("note: it has no Mob Status Controller - it never dies (a practice carcass needs none)");
        HarvestAnatomyData a = !string.IsNullOrWhiteSpace(anatomyId) ? HarvestDatabase.Instance.Anatomy(anatomyId)
            : HarvestDatabase.Instance.AnatomyForMob(mob != null ? mob.ProfileAsset : null, mob != null ? mob.type : "", root.name);
        notes.Add(a != null ? $"it is a {a.Name.ToLowerInvariant()}" : "no creature matches it yet: name one in its Anatomy Id (or give the mob a type the database knows)");
        return $"{root.name}: {string.Join("; ", notes)}.";
    }

    /// <summary>
    /// A practice carcass of <paramref name="anatomyId"/> at the Scene view's pivot: a body built from primitives (replace it
    /// with your own art) that is a carcass from the start, with a few wounds - walk up to it and press Interact to try the
    /// work. A fresh one is laid out each time it is emptied.
    /// </summary>
    public static HarvestableCreature CreatePracticeCarcass(string anatomyId)
    {
        HarvestAnatomyData a = HarvestDatabase.Instance.Anatomy(anatomyId) ?? HarvestDatabase.Instance.Anatomies.FirstOrDefault();
        if (a == null)
        {
            EditorUtility.DisplayDialog("Harvesting", "The Harvest Database has no creatures.", "OK");
            return null;
        }
        Vector3 at = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        var root = new GameObject($"Practice Carcass ({a.Name})");
        Undo.RegisterCreatedObjectUndo(root, "Create Practice Carcass");
        root.transform.position = at;
        BuildBody(root.transform, a);
        var c = root.AddComponent<HarvestableCreature>();
        var so = new SerializedObject(c);
        so.FindProperty("anatomyId").stringValue = a.id;
        so.FindProperty("practiceCarcass").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        Selection.activeGameObject = root;
        Debug.Log($"[Harvesting] Created a practice {a.Name.ToLowerInvariant()} carcass. Press Play, walk up to it and press Interact. " +
                  "Harvesting tools help (Harvesting ▸ Create Tools): give a few to the player.", root);
        return c;
    }

    /// <summary>A body lying on the ground, from primitives, shaped by the creature's plan and sized by its height.</summary>
    private static void BuildBody(Transform root, HarvestAnatomyData a)
    {
        float h = Mathf.Max(0.1f, a.referenceHeight);
        Color hide = a.hideColor, hair = Color.Lerp(a.hairColor, a.hideColor, 0.3f);
        switch (a.plan)
        {
            case HarvestBodyPlan.Biped:
            {
                // On its back: the head towards +x.
                float r = h * 0.13f;
                Part(PrimitiveType.Capsule, "Body", root, new Vector3(0f, r, 0f), new Vector3(r * 2.2f, h * 0.22f, r * 1.6f), new Vector3(0f, 0f, 90f), hide);
                Part(PrimitiveType.Sphere, "Head", root, new Vector3(h * 0.3f, r * 0.9f, 0f), Vector3.one * h * 0.14f, Vector3.zero, hide);
                for (int s = -1; s <= 1; s += 2)
                {
                    Part(PrimitiveType.Capsule, "Leg", root, new Vector3(-h * 0.32f, r * 0.6f, s * r * 0.7f), new Vector3(r, h * 0.22f, r), new Vector3(0f, 0f, 90f), hair);
                    Part(PrimitiveType.Capsule, "Arm", root, new Vector3(h * 0.06f, r * 0.5f, s * r * 1.9f), new Vector3(r * 0.8f, h * 0.18f, r * 0.8f), new Vector3(0f, s * 20f, 90f), hair);
                }
                break;
            }
            case HarvestBodyPlan.Avian:
            {
                float r = h * 0.25f;
                Part(PrimitiveType.Sphere, "Body", root, new Vector3(0f, r * 0.8f, 0f), new Vector3(r * 2.6f, r * 1.6f, r * 1.8f), Vector3.zero, hide);
                Part(PrimitiveType.Sphere, "Head", root, new Vector3(r * 1.6f, r * 0.9f, 0f), Vector3.one * r * 0.9f, Vector3.zero, hide);
                Part(PrimitiveType.Cube, "Beak", root, new Vector3(r * 2.15f, r * 0.85f, 0f), new Vector3(r * 0.5f, r * 0.2f, r * 0.2f), Vector3.zero, new Color(0.85f, 0.7f, 0.3f));
                for (int s = -1; s <= 1; s += 2)
                    Part(PrimitiveType.Cube, "Wing", root, new Vector3(-r * 0.2f, r * 0.2f, s * r * 2.2f), new Vector3(r * 2f, r * 0.08f, r * 2.8f), new Vector3(0f, s * 10f, 0f), hair);
                Part(PrimitiveType.Cube, "Tail", root, new Vector3(-r * 1.8f, r * 0.4f, 0f), new Vector3(r * 1.2f, r * 0.08f, r * 0.9f), Vector3.zero, hair);
                break;
            }
            case HarvestBodyPlan.Fish:
            {
                float len = h * 4f;
                Part(PrimitiveType.Sphere, "Body", root, new Vector3(0f, h * 0.3f, 0f), new Vector3(len, h * 0.6f, h), new Vector3(90f, 0f, 0f), hide);
                Part(PrimitiveType.Cube, "Tail", root, new Vector3(-len * 0.55f, h * 0.3f, 0f), new Vector3(h * 0.5f, h * 0.1f, h * 1.1f), Vector3.zero, hair);
                break;
            }
            case HarvestBodyPlan.Serpent:
            {
                // Coiled loosely: a few segments in an S.
                float r = h * 0.18f, seg = h * 1.2f;
                for (int i = 0; i < 6; i++)
                {
                    float x = (i - 2.5f) * seg * 0.8f, z = Mathf.Sin(i * 1.3f) * seg * 0.5f;
                    Part(PrimitiveType.Capsule, "Body", root, new Vector3(x, r, z), new Vector3(r * 2f, seg * 0.55f, r * 2f), new Vector3(0f, Mathf.Cos(i * 1.3f) * 35f, 90f), i % 2 == 0 ? hide : hair);
                }
                Part(PrimitiveType.Sphere, "Head", root, new Vector3(3.2f * seg * 0.8f, r, Mathf.Sin(6 * 1.3f) * seg * 0.5f), new Vector3(r * 3f, r * 1.8f, r * 2.4f), Vector3.zero, hide);
                break;
            }
            case HarvestBodyPlan.Arthropod:
            {
                float r = h * 0.45f;
                Part(PrimitiveType.Sphere, "Abdomen", root, new Vector3(-r * 0.9f, r * 0.6f, 0f), new Vector3(r * 2f, r * 1.3f, r * 1.7f), Vector3.zero, hide);
                Part(PrimitiveType.Sphere, "Body", root, new Vector3(r * 0.5f, r * 0.45f, 0f), new Vector3(r * 1.2f, r * 0.8f, r * 1.1f), Vector3.zero, hide);
                for (int i = 0; i < 4; i++)
                for (int s = -1; s <= 1; s += 2)
                    Part(PrimitiveType.Capsule, "Leg", root, new Vector3(r * (0.9f - i * 0.4f), r * 0.25f, s * r * 1.3f), new Vector3(r * 0.15f, r * 0.9f, r * 0.15f),
                        new Vector3(90f, 0f, s * (35f - i * 22f)), hair);
                break;
            }
            default:
            {
                // On its side: the head towards +x, the legs out towards +z.
                float r = h * 0.3f, len = h * 1.5f;
                Part(PrimitiveType.Capsule, "Body", root, new Vector3(0f, r, 0f), new Vector3(r * 2f, len * 0.5f, r * 2f), new Vector3(0f, 0f, 90f), hide);
                Part(PrimitiveType.Sphere, "Head", root, new Vector3(len * 0.62f, r * 0.8f, 0f), new Vector3(r * 1.5f, r * 1.1f, r * 1.1f), Vector3.zero, hide);
                for (int i = 0; i < 4; i++)
                {
                    float x = i < 2 ? len * 0.28f : -len * 0.28f, z = r * (1.3f + (i % 2) * 0.25f);
                    Part(PrimitiveType.Capsule, "Leg", root, new Vector3(x + (i % 2) * r * 0.3f, r * 0.45f, z), new Vector3(r * 0.4f, h * 0.3f, r * 0.4f), new Vector3(90f, 0f, 0f), hair);
                }
                if (a.regions.Any(x => x != null && x.Slot.IndexOf("wing", StringComparison.OrdinalIgnoreCase) >= 0))
                    Part(PrimitiveType.Cube, "Wing", root, new Vector3(0f, r * 1.6f, -r * 0.8f), new Vector3(len * 0.6f, r * 0.06f, len * 0.9f), new Vector3(25f, 0f, 0f), hair);
                break;
            }
        }
    }

    private static void Part(PrimitiveType type, string partName, Transform parent, Vector3 localPos, Vector3 size, Vector3 euler, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = partName;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = size;
        // No collider: the carcass makes its own trigger for the interaction prompt.
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return;
        var m = new Material(shader) { color = color };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        r.sharedMaterial = m;
    }

    // ------------------------------------------------------------------ assets
    /// <summary>
    /// A Creature Anatomy asset for every built-in creature, registered in the database. A creature the database (or the
    /// project) already has an asset of - by its id, whatever the file is called - keeps it.
    /// </summary>
    public static List<CreatureAnatomy> CreateAnatomyAssets(HarvestDatabase db, bool select)
    {
        var made = new List<CreatureAnatomy>();
        Undo.RecordObject(db, "Create Creature Anatomy Assets");
        foreach (HarvestAnatomyData d in HarvestDefaults.Anatomies())
        {
            CreatureAnatomy asset = db.anatomies.FirstOrDefault(c => c != null && string.Equals(c.Id, d.id, StringComparison.OrdinalIgnoreCase)) ?? ProjectAnatomy(d.id);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CreatureAnatomy>();
                asset.data = d;
                AssetDatabase.CreateAsset(asset, SMAssetsEditor.UniquePath(SMAssets.Anatomies, d.Name + ".asset"));
            }
            if (!db.anatomies.Contains(asset))
                db.anatomies.Add(asset);
            made.Add(asset);
        }
        db.MarkChanged();
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        if (select && made.Count > 0)
        {
            Selection.objects = made.Cast<Object>().ToArray();
            EditorGUIUtility.PingObject(made[0]);
        }
        return made;
    }

    /// <summary>
    /// A harvesting tool asset of every preset with a drawn icon and a model, listed in the database. A tool the database
    /// lists by that name, or the one in the Tools folder, is kept (given an icon and a model when it has none).
    /// </summary>
    public static List<HarvestToolSO> CreateTools(HarvestDatabase db, bool select)
    {
        var made = new List<HarvestToolSO>();
        Undo.RecordObject(db, "Create Harvesting Tools");
        foreach (HarvestToolPreset p in Enum.GetValues(typeof(HarvestToolPreset)))
        {
            HarvestToolSO probe = ScriptableObject.CreateInstance<HarvestToolSO>();
            probe.ApplyPreset(p);
            string name = probe.Name, file = SMAssetsEditor.CleanFileName(name);
            Object.DestroyImmediate(probe);
            HarvestToolSO tool = db.tools.FirstOrDefault(t => t != null && t.Name == name) ??
                                 AssetDatabase.LoadAssetAtPath<HarvestToolSO>($"{SMAssetsEditor.Folder(SMAssets.HarvestTools)}/{file}.asset");
            if (tool == null)
            {
                tool = ScriptableObject.CreateInstance<HarvestToolSO>();
                tool.ApplyPreset(p);
                ((Object)tool).name = file;
                AssetDatabase.CreateAsset(tool, SMAssetsEditor.UniquePath(SMAssets.HarvestTools, file + ".asset"));
            }
            if (tool.Icon == null)
            {
                Sprite drawn = ToolIcon(p);
                var so = new SerializedObject(tool);
                so.FindProperty("icon").objectReferenceValue = SaveIcon(file, drawn);
                so.ApplyModifiedPropertiesWithoutUndo();
                // The saved icon is the PNG asset: the drawing is not needed any more.
                if (drawn != null)
                {
                    Texture2D texture = drawn.texture;
                    Object.DestroyImmediate(drawn);
                    if (texture != null) Object.DestroyImmediate(texture);
                }
            }
            // Without a model a tool cannot be dropped, nor be loot.
            if (tool.Prefab == null)
                SetPrefab(tool, SaveToolModel(tool, AssetFile(tool, file)));
            if (!db.tools.Contains(tool))
                db.tools.Add(tool);
            EditorUtility.SetDirty(tool);
            made.Add(tool);
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        if (select && made.Count > 0)
        {
            Selection.objects = made.Cast<Object>().ToArray();
            EditorGUIUtility.PingObject(made[0]);
        }
        return made;
    }

    /// <summary>
    /// An item asset for every material of every creature (Items/Harvesting/&lt;creature&gt;), with its generated icon and a
    /// model to lie on the ground (Items/Harvesting/Models), bound in the database (existing assets and bindings are kept;
    /// bound harvested items without a model get one). Returns how many there are.
    /// </summary>
    public static int CreateItems(HarvestDatabase db, bool select)
    {
        var made = new List<ItemSO>();
        Undo.RecordObject(db, "Create Harvested Items");
        foreach (HarvestAnatomyData a in db.Anatomies)
        foreach (HarvestMaterial m in a.materials)
        {
            if (m == null)
                continue;
            string file = SMAssetsEditor.CleanFileName($"{a.Name} {m.Name}");
            ItemSO bound = db.BoundItem(a.id, m.id);
            if (bound != null)
            {
                // Made before items had models: without one a piece cannot be dropped, nor left on the ground when the pack is full.
                if (bound.Prefab == null && bound is IHarvestedItem)
                    SetPrefab(bound, SaveModel(a, m, bound, AssetFile(bound, file)));
                continue;
            }
            // The asset of this material in its creature's folder (never another item that only shares the file name).
            string folder = SMAssets.HarvestItems + "/" + SMAssetsEditor.CleanFileName(a.Name);
            string path = $"{SMAssetsEditor.Folder(folder)}/{file}.asset";
            ItemSO item = AssetDatabase.LoadAssetAtPath<ItemSO>(path);
            if (!(item is IHarvestedItem h) || !string.Equals(h.AnatomyId, a.id, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(h.MaterialId, m.id, StringComparison.OrdinalIgnoreCase))
            {
                // Its own file (a creature sharing another's names gets "... 1"), and its icon and model named after it.
                string assetPath = AssetDatabase.LoadAssetAtPath<Object>(path) == null ? path : AssetDatabase.GenerateUniqueAssetPath(path);
                string name = Path.GetFileNameWithoutExtension(assetPath);
                Sprite icon = SaveIcon(name, HarvestArt.Icon(a, m));
                if (m.IsFood)
                {
                    var food = ScriptableObject.CreateInstance<HarvestedFoodSO>();
                    food.Setup(a, m, icon, null);
                    item = food;
                }
                else
                {
                    var material = ScriptableObject.CreateInstance<HarvestedMaterialSO>();
                    material.Setup(a, m, icon, null);
                    item = material;
                }
                ((Object)item).name = name;
                AssetDatabase.CreateAsset(item, assetPath);
            }
            if (item.Prefab == null)
                SetPrefab(item, SaveModel(a, m, item, AssetFile(item, file)));
            db.items.Add(new HarvestItemBinding { anatomy = a.id, material = m.id, item = item });
            EditorUtility.SetDirty(item);
            made.Add(item);
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        if (select && made.Count > 0)
        {
            Selection.objects = made.Cast<Object>().ToArray();
            EditorGUIUtility.PingObject(made[0]);
        }
        return db.items.Count(b => b != null && b.item != null);
    }

    /// <summary>The file name (without extension) of <paramref name="item"/>'s asset, else <paramref name="fallback"/>.</summary>
    private static string AssetFile(Object item, string fallback)
    {
        string path = AssetDatabase.GetAssetPath(item);
        return string.IsNullOrEmpty(path) ? fallback : Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>
    /// The model prefab at <paramref name="path"/>, when it can be <paramref name="item"/>'s: its pickable gives that item,
    /// gives nothing (the item it gave was deleted: it is pointed at this one), or it has none. Null when there is no
    /// prefab there, or when it is another item's; <paramref name="free"/> is then where a new one goes.
    /// </summary>
    private static GameObject ExistingModel(string path, ItemSO item, out string free)
    {
        free = path;
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing == null)
            return null;
        ItemPickable p = existing.GetComponent<ItemPickable>();
        if (p == null || p.itemScriptableObject == item)
            return existing;
        if (p.itemScriptableObject == null)
        {
            p.itemScriptableObject = item;
            EditorUtility.SetDirty(p);
            PrefabUtility.SavePrefabAsset(existing);
            return existing;
        }
        free = AssetDatabase.GenerateUniqueAssetPath(path);
        return null;
    }

    /// <summary>
    /// A model prefab for a harvested item (Items/Harvesting/Models, named after the item's asset): the shape the runtime
    /// items use, its material saved as an asset, with an Item Pickable giving <paramref name="item"/>. An existing one is
    /// kept when it is that item's (or nobody's); another item's model is never taken over.
    /// </summary>
    public static GameObject SaveModel(HarvestAnatomyData a, HarvestMaterial m, ItemSO item, string file)
    {
        string folder = SMAssetsEditor.Folder(SMAssets.HarvestItems + "/Models");
        GameObject existing = ExistingModel($"{folder}/{SMAssetsEditor.CleanFileName(file)}.prefab", item, out string path);
        if (existing != null)
            return existing;
        GameObject copy = Object.Instantiate(HarvestArt.Template(a, m));
        copy.name = Path.GetFileNameWithoutExtension(path);
        copy.SetActive(true);
        copy.hideFlags = HideFlags.None;
        Renderer r = copy.GetComponent<Renderer>();
        if (r != null && r.sharedMaterial != null)
        {
            // The runtime material lives only while the game runs: the prefab keeps a copy of its own.
            string matPath = Path.ChangeExtension(path, ".mat");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(r.sharedMaterial);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            r.sharedMaterial = mat;
        }
        ItemPickable pick = copy.GetComponent<ItemPickable>();
        if (pick == null)
            pick = copy.AddComponent<ItemPickable>();
        pick.itemScriptableObject = item;
        pick.quantity = 1;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(copy, path);
        Object.DestroyImmediate(copy);
        return prefab;
    }

    /// <summary>
    /// A model prefab for a harvesting tool (Items/Harvesting/Models): a wooden handle and a steel head, with an Item
    /// Pickable giving <paramref name="tool"/>. An existing one is kept when it is that tool's (or nobody's); another
    /// tool's model is never taken over.
    /// </summary>
    public static GameObject SaveToolModel(HarvestToolSO tool, string file)
    {
        string folder = SMAssetsEditor.Folder(SMAssets.HarvestItems + "/Models");
        GameObject existing = ExistingModel($"{folder}/{SMAssetsEditor.CleanFileName(file)}.prefab", tool, out string path);
        if (existing != null)
            return existing;
        var root = new GameObject(Path.GetFileNameWithoutExtension(path));
        try
        {
            void Part(PrimitiveType type, Vector3 at, Vector3 size, Material mat)
            {
                GameObject go = GameObject.CreatePrimitive(type);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = at;
                go.transform.localScale = size;
                Renderer r = go.GetComponent<Renderer>();
                if (r != null && mat != null) r.sharedMaterial = mat;
            }
            Part(PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.025f, 0.06f, 0.025f), ToolMaterial(folder, "Tool Handle", new Color(0.45f, 0.28f, 0.15f)));
            Part(PrimitiveType.Cube, new Vector3(0f, 0.19f, 0f), new Vector3(0.008f, 0.14f, 0.035f), ToolMaterial(folder, "Tool Steel", new Color(0.78f, 0.8f, 0.84f)));
            ItemPickable pick = root.AddComponent<ItemPickable>();
            pick.itemScriptableObject = tool;
            pick.quantity = 1;
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>A plain material asset of <paramref name="color"/> in <paramref name="folder"/> (kept when it exists).</summary>
    private static Material ToolMaterial(string folder, string name, Color color)
    {
        string path = $"{folder}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
            return mat;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return null;
        mat = new Material(shader) { color = color };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>The project's Creature Anatomy asset of <paramref name="id"/>, wherever it is, or null.</summary>
    private static CreatureAnatomy ProjectAnatomy(string id)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CreatureAnatomy)))
        {
            var c = AssetDatabase.LoadAssetAtPath<CreatureAnatomy>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null && string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    /// <summary>
    /// Where Auto Attach makes a mob prefab (<paramref name="root"/>) harvestable when it plays - the database knows the
    /// mob and it has no Harvestable Creature of its own: the object the creature goes on; null when it will not.
    /// </summary>
    public static GameObject AutoAttachHost(GameObject root)
    {
        if (root == null || root.GetComponentInChildren<HarvestableCreature>(true) != null || !HarvestDatabase.Instance.Settings.autoAttach)
            return null;
        MobStatusController status = root.GetComponentInChildren<MobStatusController>(true);
        CombatEntity entity = root.GetComponentInChildren<CombatEntity>(true);
        Mob mob = root.GetComponentInChildren<Mob>(true);
        if (status == null && entity == null && mob == null)
            return null;
        HarvestAnatomyData a = HarvestDatabase.Instance.AnatomyForMob(mob != null ? mob.ProfileAsset : null, mob != null ? mob.type : "", root.name);
        if (a == null)
            return null;
        // Where the game adds it: on the mob's combat entity (made next to its status controller).
        return entity != null ? entity.gameObject : status != null ? status.gameObject : mob.gameObject;
    }

    private static void SetPrefab(ItemSO item, GameObject model)
    {
        if (item == null || model == null)
            return;
        var so = new SerializedObject(item);
        SerializedProperty p = so.FindProperty("prefab");
        if (p == null)
            return;
        p.objectReferenceValue = model;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
    }

    /// <summary>Saves a generated sprite as a PNG in Items/Harvesting/Icons (an existing one is kept) and returns the asset.</summary>
    public static Sprite SaveIcon(string name, Sprite generated)
    {
        if (generated == null || generated.texture == null)
            return null;
        string path = $"{SMAssetsEditor.Folder(IconFolder)}/{SMAssetsEditor.CleanFileName(name)}.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;
        File.WriteAllBytes(path, generated.texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        if (AssetImporter.GetAtPath(path) is TextureImporter ti)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>A drawn icon of a harvesting tool (128 px): a blade, a saw, a scraper or pliers on a handle.</summary>
    public static Sprite ToolIcon(HarvestToolPreset kind)
    {
        var c = new CraftPixels(128, 128);
        c.Clear(new Color(0f, 0f, 0f, 0f));
        Color steel = new Color(0.78f, 0.8f, 0.84f), edge = new Color(0.95f, 0.96f, 0.98f), wood = new Color(0.45f, 0.28f, 0.15f), dark = new Color(0.2f, 0.14f, 0.1f);
        void Handle(Vector2 a, Vector2 b, float t)
        {
            c.Line(a.x, a.y, b.x, b.y, dark, t + 3f);
            c.Line(a.x, a.y, b.x, b.y, wood, t);
        }
        switch (kind)
        {
            case HarvestToolPreset.BoneSaw:
                Handle(new Vector2(14f, 20f), new Vector2(40f, 46f), 10f);
                c.Polygon(new[] { new Vector2(36f, 52f), new Vector2(112f, 112f), new Vector2(122f, 100f), new Vector2(48f, 38f) }, steel);
                for (int i = 0; i < 9; i++)
                {
                    float x = 50f + i * 8f, y = 38f + i * 6.6f;
                    c.Polygon(new[] { new Vector2(x, y), new Vector2(x + 7f, y + 5.5f), new Vector2(x + 7f, y - 2f) }, edge);
                }
                break;
            case HarvestToolPreset.Scraper:
                c.Polygon(new[] { new Vector2(24f, 70f), new Vector2(64f, 52f), new Vector2(104f, 70f), new Vector2(104f, 80f), new Vector2(64f, 64f), new Vector2(24f, 80f) }, steel);
                c.Line(24f, 71f, 64f, 53f, edge, 2f);
                c.Line(64f, 53f, 104f, 71f, edge, 2f);
                Handle(new Vector2(6f, 84f), new Vector2(26f, 76f), 10f);
                Handle(new Vector2(102f, 76f), new Vector2(122f, 84f), 10f);
                break;
            case HarvestToolPreset.Pliers:
                Handle(new Vector2(30f, 12f), new Vector2(58f, 60f), 8f);
                Handle(new Vector2(98f, 12f), new Vector2(70f, 60f), 8f);
                c.Polygon(new[] { new Vector2(56f, 56f), new Vector2(66f, 116f), new Vector2(72f, 114f), new Vector2(70f, 60f) }, steel);
                c.Polygon(new[] { new Vector2(72f, 56f), new Vector2(62f, 116f), new Vector2(56f, 114f), new Vector2(58f, 60f) }, steel);
                c.Disc(64f, 60f, 6f, edge);
                break;
            case HarvestToolPreset.Cleaver:
                Handle(new Vector2(14f, 16f), new Vector2(44f, 46f), 10f);
                c.Polygon(new[] { new Vector2(40f, 46f), new Vector2(84f, 112f), new Vector2(120f, 84f), new Vector2(72f, 26f) }, steel);
                c.Line(84f, 112f, 120f, 84f, edge, 3f);
                c.Disc(98f, 92f, 4f, new Color(0.1f, 0.1f, 0.12f, 0.8f));
                break;
            default:
            {
                // Knives: the length and the curve of the blade tell them apart.
                float len = kind == HarvestToolPreset.ButcherKnife ? 80f : kind == HarvestToolPreset.Lancet ? 44f : 64f;
                float width = kind == HarvestToolPreset.ButcherKnife ? 14f : kind == HarvestToolPreset.Lancet ? 6f : 11f;
                float belly = kind == HarvestToolPreset.SkinningKnife ? 9f : kind == HarvestToolPreset.HuntingKnife ? 5f : 2f;
                Handle(new Vector2(12f, 12f), new Vector2(40f, 40f), kind == HarvestToolPreset.Lancet ? 6f : 10f);
                Vector2 dir = new Vector2(1f, 1f).normalized, side = new Vector2(1f, -1f).normalized, b0 = new Vector2(40f, 40f);
                Vector2 tip = b0 + dir * len;
                var blade = new List<Vector2> { b0 - side * (width * 0.3f) };
                for (int i = 1; i <= 6; i++)
                {
                    float t = i / 6f;
                    blade.Add(b0 + dir * (len * t) + side * (width * (1f - t * t) + belly * Mathf.Sin(t * Mathf.PI)));
                }
                blade.Add(tip);
                blade.Add(b0 + dir * (len * 0.9f) - side * (width * 0.25f));
                c.Polygon(blade, steel);
                for (int i = 1; i < 7; i++)
                    c.Line(blade[i].x, blade[i].y, blade[i + 1].x, blade[i + 1].y, edge, 2f);
                c.Line(b0.x - side.x * 9f, b0.y - side.y * 9f, b0.x + side.x * 12f, b0.y + side.y * 12f, new Color(0.55f, 0.5f, 0.35f), 5f);
                break;
            }
        }
        c.Apply();
        return c.ToSprite(kind.ToString());
    }
}

// ====================================================================== inspectors
[CustomEditor(typeof(HarvestDatabase))]
public class HarvestDatabaseEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private bool showWarnings;

    public override void OnInspectorGUI()
    {
        var db = (HarvestDatabase)target;
        errors.Clear();
        warnings.Clear();
        HarvestingEditorTools.Validate(db, errors, warnings);
        int assets = db.anatomies != null ? db.anatomies.Count(a => a != null) : 0;
        EditorGUILayout.HelpBox($"{db.Anatomies.Count} creature(s): {assets} from assets{(db.includeBuiltIn ? ", the rest built in" : "")} · " +
                                $"{db.items.Count(b => b != null && b.item != null)} item asset(s) · {db.tools.Count(t => t != null)} tool(s).\n" +
                                "Hits mark the body by where they land and what dealt them; the carcass is harvested material by material, " +
                                "each with its own hands-on work.", MessageType.None);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        if (warnings.Count > 0)
        {
            showWarnings = EditorGUILayout.Foldout(showWarnings, $"{warnings.Count} warning(s)", true);
            if (showWarnings)
                foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Anatomy Assets", "An editable Creature Anatomy asset for every built-in creature, registered here.")))
                HarvestingEditorTools.CreateAnatomyAssets(db, true);
            if (GUILayout.Button(new GUIContent("Create Tools", "Skinning knife, butcher's knife, cleaver, hunting knife, lancet, bone saw, scraper and pliers, with icons.")))
                HarvestingEditorTools.CreateTools(db, true);
            if (GUILayout.Button(new GUIContent("Create Items", "An item asset (with its icon) for every material of every creature, bound here.")))
                HarvestingEditorTools.CreateItems(db, true);
        }
        if (HarvestingEditorTools.MissingCraftHides().Count > 0 &&
            GUILayout.Button(new GUIContent("Add Harvested Hides to the Crafting Database", "The hides the creatures give that the project's Crafting Database lacks.")))
            HarvestingEditorTools.AddMissingCraftHides();
        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
            db.MarkChanged();
    }
}

[CustomEditor(typeof(CreatureAnatomy))]
public class CreatureAnatomyEditor : Editor
{
    private static readonly string[] BuiltIn = HarvestDefaults.Anatomies().Select(a => a.id).ToArray();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private int startFrom;
    private HarvestInspectLayer layer = HarvestInspectLayer.Overall;
    private CraftPixels canvas;
    private string drawn;

    private void OnDisable()
    {
        canvas?.Release();
        canvas = null;
    }

    public override void OnInspectorGUI()
    {
        var asset = (CreatureAnatomy)target;
        HarvestAnatomyData a = asset.data;
        using (new EditorGUILayout.HorizontalScope())
        {
            startFrom = EditorGUILayout.Popup(new GUIContent("Start From", "Replace everything with a built-in creature, then make it your own."), startFrom, BuiltIn);
            if (GUILayout.Button("Apply", GUILayout.Width(60f)) &&
                EditorUtility.DisplayDialog("Creature Anatomy", $"Replace this anatomy with the built-in {BuiltIn[startFrom]}?", "Replace", "Cancel"))
            {
                Undo.RecordObject(asset, "Start From Built-in Creature");
                asset.StartFrom(BuiltIn[startFrom]);
                EditorUtility.SetDirty(asset);
                HarvestDatabase.Instance.MarkChanged();
                drawn = null;
            }
        }
        if (a != null)
        {
            errors.Clear();
            warnings.Clear();
            asset.Validate(errors, warnings);
            foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
            foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
            DrawPreview(a);
            EditorGUILayout.HelpBox(Summary(a), MessageType.None);
        }
        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
        {
            HarvestDatabase.Instance.MarkChanged();
            drawn = null;
        }
    }

    /// <summary>The body as the carcass window shows it (whole), coloured by one tissue; weak points ringed.</summary>
    private void DrawPreview(HarvestAnatomyData a)
    {
        HarvestInspectLayer l = (HarvestInspectLayer)GUILayout.Toolbar((int)layer, Enum.GetNames(typeof(HarvestInspectLayer)));
        if (l != layer)
        {
            layer = l;
            drawn = null;
        }
        Rect r = GUILayoutUtility.GetAspectRect(720f / 400f, GUILayout.MaxWidth(520f));
        if (Event.current.type != EventType.Repaint)
            return;
        canvas ??= new CraftPixels(720, 400);
        string key = layer + "|" + EditorUtility.GetDirtyCount(target);
        if (key != drawn)
        {
            drawn = key;
            canvas.Clear(new Color(0.11f, 0.12f, 0.14f));
            try
            {
                HarvestCondition whole = HarvestCondition.Create(a, 1);
                HarvestInspectView.Draw(canvas, a, whole, layer, null, -1, 0f);
            }
            catch (Exception)
            {
                // A half-edited anatomy may not draw; the checks above say why.
            }
            canvas.Apply();
        }
        GUI.DrawTexture(r, canvas.Texture, ScaleMode.ScaleToFit);
    }

    private static string Summary(HarvestAnatomyData a)
    {
        var sb = new StringBuilder();
        sb.Append($"{a.Name}: {a.plan.ToString().ToLowerInvariant()}, {a.mass:0.#} kg, {a.referenceHeight:0.##} m · {a.regions.Count} regions · {a.materials.Count} materials\n");
        string[] weak = a.regions.Where(r => r != null && r.weakPoint).Select(r => $"{r.Name} ×{r.damageMultiplier:0.0}").ToArray();
        sb.Append(weak.Length > 0 ? $"Weak points: {string.Join(", ", weak)}\n" : "No weak points.\n");
        foreach (HarvestMethod method in Enum.GetValues(typeof(HarvestMethod)))
        {
            string[] names = a.materials.Where(m => m != null && m.method == method).Select(m => m.Name).ToArray();
            if (names.Length > 0)
                sb.Append($"{HarvestText.MethodName(method)}: {string.Join(", ", names)}\n");
        }
        return sb.ToString().TrimEnd();
    }
}

[CustomEditor(typeof(HarvestableCreature))]
[CanEditMultipleObjects]
public class HarvestableCreatureEditor : Editor
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty it = serializedObject.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (it.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(it);
            }
            else if (it.propertyPath == "anatomyId")
                AnatomyPopup(it);
            else
                EditorGUILayout.PropertyField(it, true);
        }
        serializedObject.ApplyModifiedProperties();
        if (targets.Length > 1)
            return;

        var c = (HarvestableCreature)target;
        errors.Clear();
        warnings.Clear();
        c.Validate(errors, warnings);
        foreach (string e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);
        foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        NetworkHint(c);
        if (Application.isPlaying)
            LiveState(c);
    }

    /// <summary>The anatomy id as a choice among the database's creatures (or found from the mob).</summary>
    private static void AnatomyPopup(SerializedProperty p)
    {
        var ids = new List<string> { "" };
        var labels = new List<string> { "(found from the mob)" };
        foreach (HarvestAnatomyData a in HarvestDatabase.Instance.Anatomies)
        {
            ids.Add(a.id);
            labels.Add($"{a.Name} ({a.id})");
        }
        int index = p.hasMultipleDifferentValues ? -1 : ids.FindIndex(x => string.Equals(x, p.stringValue, StringComparison.OrdinalIgnoreCase));
        if (index < 0 && !p.hasMultipleDifferentValues)
        {
            ids.Add(p.stringValue);
            labels.Add($"{p.stringValue} (not in the database)");
            index = ids.Count - 1;
        }
        EditorGUI.showMixedValue = p.hasMultipleDifferentValues;
        int chosen = EditorGUILayout.Popup(new GUIContent("Anatomy Id", "A creature of the Harvest Database, when no asset is assigned."), index, labels.ToArray());
        EditorGUI.showMixedValue = false;
        if (chosen != index && chosen >= 0)
            p.stringValue = ids[chosen];
    }

    private static void NetworkHint(HarvestableCreature c)
    {
        Type nob = SimpleMovements.Setup.SetupUtil.TypeByName("FishNet.Object.NetworkObject");
        Type net = SimpleMovements.Setup.SetupUtil.TypeByName("GameNet.NetCarcass");
        if (nob == null || net == null || c.GetComponentInParent(nob, true) == null || c.GetComponent(net) != null)
            return;
        EditorGUILayout.HelpBox("It is a network object without a NetCarcass: in a network session its body is not kept and clients cannot harvest it.", MessageType.Warning);
        if (GUILayout.Button("Add NetCarcass"))
            Undo.AddComponent(c.gameObject, net);
    }

    private void LiveState(HarvestableCreature c)
    {
        HarvestAnatomyData a = c.Anatomy;
        HarvestCondition k = c.Condition;
        var sb = new StringBuilder();
        if (a == null)
            sb.Append("No anatomy: it cannot be harvested.");
        else
        {
            sb.Append(c.IsCarcass ? $"A carcass ({(c.IsPractice ? "practice" : HarvestText.Minutes(c.MinutesLeft) + " left")})" : "Alive");
            sb.Append($" · {a.Name}");
            if (k != null)
                sb.Append($" · {k.age}, {k.sizeScale:0.00}× size · {k.hits} hit(s), {k.wounds.Count} wound(s)");
            HarvestCarcass carcass = c.Carcass;
            if (carcass != null)
                sb.Append($"\nDead {HarvestText.Minutes(carcass.age / 60.0)} · {carcass.temperature:0} °C · {carcass.taken.Count} taken · {carcass.locks.Count} being worked" +
                          (carcass.hideOff ? " · hide off" : "") + (carcass.gutted ? " · gutted" : ""));
        }
        EditorGUILayout.HelpBox(sb.ToString(), MessageType.Info);
        CombatEntity me = PlayerLocator.Local;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!c.IsCarcass || me == null))
                if (GUILayout.Button(new GUIContent("Open Carcass Window", "For the local player (who must stand by it: walking away closes it).")))
                    HarvestWindow.Show(c, me);
            MobStatusController status = c.GetComponent<MobStatusController>();
            using (new EditorGUI.DisabledScope(c.IsCarcass || status == null))
                if (GUILayout.Button(new GUIContent("Kill", "Kill the mob now (its body becomes a carcass with the wounds it took).")))
                    status.Kill(me);
        }
        Repaint();
    }
}
#endif
