using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Quick setup for mobs: adds and wires every component the mob AI needs.
/// Tools > SimpleMovements > Mobs > Create New Mob, or right-click a GameObject > SimpleMovements > Quick Setup Mob.
/// </summary>
public class MobQuickSetup : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem(SMMenu.Root + "Mobs/Create New Mob", false, SMMenu.Mobs + 0)]
    private static void CreateNewMob()
    {
        GameObject mobObject = new GameObject("NewMob");
        Undo.RegisterCreatedObjectUndo(mobObject, "Create Mob");
        MobSetupUtility.SetupMob(mobObject, true);
        Selection.activeGameObject = mobObject;
        EditorGUIUtility.PingObject(mobObject);
    }

    [MenuItem("GameObject/SimpleMovements/Quick Setup Mob", false, 0)]
    private static void QuickSetupSelectedMob(MenuCommand menuCommand)
    {
        GameObject mobObject = menuCommand.context as GameObject;
        if (mobObject != null)
            MobSetupUtility.SetupMob(mobObject, false);
    }

    [MenuItem("GameObject/SimpleMovements/Quick Setup Mob", true)]
    private static bool ValidateQuickSetupMob() => Selection.activeGameObject != null;

    [MenuItem(SMMenu.Root + "Mobs/Add Missing Components", false, SMMenu.Mobs + 1)]
    private static void AddMissingComponents()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select a GameObject first.", "OK");
            return;
        }
        MobSetupUtility.SetupMob(selected, false);
    }

    [MenuItem(SMMenu.Root + "Mobs/Help", false, SMMenu.Mobs + 40)]
    private static void ShowHelp()
    {
        EditorUtility.DisplayDialog("Mob Setup Help", @"MOB QUICK SETUP

Right-click a GameObject > SimpleMovements > Quick Setup Mob, or Tools > SimpleMovements > Mobs > Create New Mob.

ADDS (only what is missing):
- CapsuleCollider, kinematic Rigidbody (a physics Rigidbody fights the NavMeshAgent)
- NavMeshAgent
- Model / VisualModel hierarchy with an Animator
- MobStatusController + HealthManager + SpeedManager
- MobActionsController (the mob), MobMovementStateMachine (the AI)
- MobAbilityController (abilities), CombatEntity (team, body, crowd control)

THEN:
1. Assign an Animator Controller (parameters like Speed, IsMoving, Hit, Die are used when present;
   old controllers with Idle/Moving/Chasing/Patrol states keep working).
2. Set the mob Type and its Preys ('Player' to attack players).
3. Assign or create a Mob Profile (temperament, senses, movement, combat, dodging).
4. Add Ability Definitions to the MobAbilityController (a basic attack is created from Bite Damage otherwise).
5. Bake the NavMesh.", "Got it!");
    }
#endif
}

#if UNITY_EDITOR
/// <summary>Adds and wires the components of a mob (used by the menus and the mob inspector).</summary>
public static class MobSetupUtility
{
    /// <summary>
    /// Adds and wires every component. With <paramref name="createAssets"/> it also gives the mob a Mob Profile asset
    /// and (when it would use the automatic bite) a Basic Attack asset, so nothing important is hidden in runtime code.
    /// </summary>
    public static void SetupMob(GameObject mobObject, bool isNew, bool createAssets = true)
    {
        if (settingUp)
            return;   // a fix of the validator below asked for the setup again
        settingUp = true;
        try
        {
            SetupMobNow(mobObject, isNew, createAssets);
        }
        finally
        {
            settingUp = false;
        }
    }

    private static bool settingUp;

    private static void SetupMobNow(GameObject mobObject, bool isNew, bool createAssets)
    {
        Undo.RegisterFullObjectHierarchyUndo(mobObject, "Setup Mob");
        int added = 0;

        // The model first, so the collider and the agent can be sized to it.
        Animator animator = SetupModelHierarchy(mobObject, ref added);

        if (mobObject.GetComponent<Collider>() == null)
        {
            MobSetupValidator.FitCollider(mobObject, true);   // fitted to the model's meshes
            added++;
        }

        Rigidbody rb = mobObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = Undo.AddComponent<Rigidbody>(mobObject);
            added++;
        }
        // Kinematic: the NavMeshAgent moves the mob; physics only detects hits.
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        NavMeshAgent agent = mobObject.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = Undo.AddComponent<NavMeshAgent>(mobObject);
            agent.speed = 3.5f;
            agent.angularSpeed = 540f;
            agent.acceleration = 24f;
            agent.stoppingDistance = 0.3f;
            agent.autoBraking = true;
            CapsuleCollider capsule = mobObject.GetComponent<CapsuleCollider>();
            agent.radius = capsule != null ? capsule.radius : 0.5f;
            agent.height = capsule != null ? capsule.height : 2f;
            added++;
        }

        MobStatusController status = mobObject.GetComponent<MobStatusController>();
        if (status == null)
        {
            status = Undo.AddComponent<MobStatusController>(mobObject);
            added++;
        }
        HealthManager health = mobObject.GetComponent<HealthManager>();
        if (health == null)
        {
            health = Undo.AddComponent<HealthManager>(mobObject);
            added++;
        }
        SpeedManager speed = mobObject.GetComponent<SpeedManager>();
        if (speed == null)
        {
            speed = Undo.AddComponent<SpeedManager>(mobObject);
            added++;
        }
        var statusSo = new SerializedObject(status);
        SetRef(statusSo, "healthManager", health);
        SetRef(statusSo, "speedManager", speed);
        statusSo.ApplyModifiedProperties();

        MobActionsController mob = mobObject.GetComponent<MobActionsController>();
        if (mob == null)
        {
            mob = Undo.AddComponent<MobActionsController>(mobObject);
            added++;
            var so = new SerializedObject(mob);
            SetRef(so, "mobTransform", mobObject.transform);
            SetString(so, "type", "Mob");
            SerializedProperty preys = so.FindProperty("Preys");
            if (preys != null && preys.arraySize == 0)
            {
                preys.arraySize = 1;
                preys.GetArrayElementAtIndex(0).stringValue = "Player";
            }
            if (isNew)
            {
                SetFloat(so, "wanderDistance", 12f);
                SetFloat(so, "detectionRange", 15f);
                SetInt(so, "biteDamage", 8);
                SetFloat(so, "biteCooldown", 1.5f);
                SetFloat(so, "attackDistance", 1.8f);
            }
            so.ApplyModifiedProperties();
        }

        MobMovementStateMachine machine = mobObject.GetComponent<MobMovementStateMachine>();
        if (machine == null)
        {
            machine = Undo.AddComponent<MobMovementStateMachine>(mobObject);
            added++;
        }
        var machineSo = new SerializedObject(machine);
        SetRef(machineSo, "actionsController", mob);
        SetRef(machineSo, "mob", mob);
        SetRef(machineSo, "animator", animator);
        SetRef(machineSo, "navMeshAgent", agent);
        SetRef(machineSo, "statusController", status);
        machineSo.ApplyModifiedProperties();

        MobAbilityController abilities = mobObject.GetComponent<MobAbilityController>();
        if (abilities == null)
        {
            abilities = Undo.AddComponent<MobAbilityController>(mobObject);
            added++;
        }
        var abilitiesSo = new SerializedObject(abilities);
        SetRef(abilitiesSo, "mobActionController", mob);
        SetRef(abilitiesSo, "animator", animator);
        abilitiesSo.ApplyModifiedProperties();
        Undo.RecordObject(abilities, "Sync Absorption Table");
        abilities.SyncAbsorptionTable();
        EditorUtility.SetDirty(abilities);
        if (mobObject.GetComponent<CombatEntity>() == null)
        {
            Undo.AddComponent<CombatEntity>(mobObject);
            added++;
        }

        // Make what the mob would otherwise build at runtime visible and editable: a Profile asset and a Basic Attack asset.
        string created = "";
        if (createAssets)
        {
            bool hadProfile = mob.ProfileAsset != null;
            MobProfile profile = EnsureProfile(mob);
            if (!hadProfile && profile != null)
                created += $" Profile: '{profile.name}'.";
            AbilityDefinition bite = EnsureBasicAttack(abilities, mob);
            if (bite != null)
                created += $" Basic attack: '{bite.name}'.";
        }

        // Then everything the validator can correct safely (wiring, kinematic body, agent size, root motion...).
        int fixedCount = MobSetupValidator.FixAllSafe(mobObject);
        List<MobSetupValidator.Issue> left = MobSetupValidator.Check(mobObject);
        int errors = left.Count(i => i.severity == MobSetupValidator.Severity.Error);
        int warnings = left.Count(i => i.severity == MobSetupValidator.Severity.Warning);
        string fixedText = fixedCount > 0 ? $" Fixed {fixedCount} problem(s)." : "";
        string leftText = errors + warnings > 0
            ? $" {errors} error(s) and {warnings} warning(s) left: see the mob inspector ▸ Setup checks (or Tools ▸ SimpleMovements ▸ Mobs ▸ Mob Setup Validator)."
            : " No problems left.";

        EditorUtility.SetDirty(mobObject);
        Debug.Log(added > 0
            ? $"<color=green>Mob setup: added {added} component(s) to {mobObject.name}.</color>{created}{fixedText}{leftText}"
            : $"Mob setup: {mobObject.name} already has every component (references re-wired).{created}{fixedText}{leftText}", mobObject);
    }

    /// <summary>
    /// Assigns a Mob Profile if the mob has none: an existing '&lt;Type&gt; Profile' asset (shared by the type) or a new
    /// one built from the mob's fields. Returns the profile.
    /// </summary>
    public static MobProfile EnsureProfile(Mob mob)
    {
        if (mob == null)
            return null;
        if (mob.ProfileAsset != null)
            return mob.ProfileAsset;
        if (!string.IsNullOrEmpty(mob.type) && mob.type != "Mob")
        {
            MobProfile existing = FindAssetNamed<MobProfile>(mob.type + " Profile");
            if (existing != null)
            {
                AssignProfile(mob, existing);
                return existing;
            }
        }
        return CreateProfileFor(mob, null);
    }

    /// <summary>
    /// When the mob would get the automatic basic attack (no melee ability, no legacy abilities, Bite Damage &gt; 0),
    /// puts it in a slot as a real asset instead (an existing '&lt;Type&gt; Basic Attack' asset, or a new one), so it
    /// can be seen and edited. Returns the ability added, or null.
    /// </summary>
    public static AbilityDefinition EnsureBasicAttack(MobAbilityController abilities, Mob mob)
    {
        if (abilities == null || mob == null || mob.BiteDamage <= 0 || abilities.HasMeleeSlot())
            return null;
        foreach (AbilityHolder h in abilities.LegacyAbilities)
            if (h != null && h.abilityEffect != null)
                return null; // legacy abilities may already be melee; the runtime decides
        string fileName = MobAbilityController.BasicAttackName(mob);
        AbilityDefinition def = !string.IsNullOrEmpty(mob.type) && mob.type != "Mob" ? FindAssetNamed<AbilityDefinition>(fileName) : null;
        if (def == null)
            def = SaveAbility(MobAbilityController.CreateBasicAttackDefinition(mob), FolderFor(mob.gameObject), fileName);
        AddSlot(abilities, def);
        return def;
    }

    /// <summary>Saves a new basic attack asset from the mob's Bite settings and adds it to a slot (inspector button).</summary>
    public static AbilityDefinition CreateBasicAttackAsset(MobAbilityController abilities, Mob mob)
    {
        AbilityDefinition def = SaveAbility(MobAbilityController.CreateBasicAttackDefinition(mob), FolderFor(mob.gameObject), MobAbilityController.BasicAttackName(mob));
        if (abilities != null)
            AddSlot(abilities, def);
        return def;
    }

    /// <summary>Adds <paramref name="def"/> to a new slot (with Undo) and syncs the absorption table.</summary>
    public static void AddSlot(MobAbilityController abilities, AbilityDefinition def)
    {
        var so = new SerializedObject(abilities);
        SerializedProperty slots = so.FindProperty("slots");
        int i = slots.arraySize;
        slots.arraySize++;
        SerializedProperty s = slots.GetArrayElementAtIndex(i);
        s.FindPropertyRelative("ability").objectReferenceValue = def;
        s.FindPropertyRelative("enabled").boolValue = true;
        s.FindPropertyRelative("aiWeight").floatValue = 1f;
        s.FindPropertyRelative("label").stringValue = "";
        so.ApplyModifiedProperties();
        Undo.RecordObject(abilities, "Sync Absorption Table");
        abilities.SyncAbsorptionTable();
        EditorUtility.SetDirty(abilities);
    }

    private static AbilityDefinition SaveAbility(AbilityDefinition def, string folder, string fileName)
    {
        foreach (char ch in System.IO.Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(ch, '_');
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
        def.hideFlags = HideFlags.None;
        AssetDatabase.CreateAsset(def, path);
        def.SetId(AssetDatabase.AssetPathToGUID(path));
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        return def;
    }

    private static T FindAssetNamed<T>(string fileName) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets($"\"{fileName}\" t:{typeof(T).Name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == fileName)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                    return asset;
            }
        }
        return null;
    }

    private static void AssignProfile(Mob mob, MobProfile p)
    {
        var so = new SerializedObject(mob);
        so.FindProperty("profile").objectReferenceValue = p;
        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// Where the assets generated for a mob go (its profile, basic attack, abilities, body parts):
    /// Resources/SimpleMovements/Mobs/&lt;Type&gt; (the mob's Type, else the object's name).
    /// </summary>
    public static string FolderFor(GameObject go)
    {
        Mob mob = go != null ? go.GetComponentInParent<Mob>() ?? go.GetComponentInChildren<Mob>(true) : null;
        string type = mob != null && !string.IsNullOrWhiteSpace(mob.type) ? mob.type : go != null ? PrefabRootName(go) : "Mob";
        return SMAssetsEditor.MobFolder(ObjectNames.NicifyVariableName(type));
    }

    /// <summary>The name of the prefab the object belongs to (without "(Clone)" or a variant suffix), else its own.</summary>
    private static string PrefabRootName(GameObject go)
    {
        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        string n = (root != null ? root : go.transform.root.gameObject).name;
        return n.Replace("(Clone)", "").Trim();
    }

    private static Animator SetupModelHierarchy(GameObject mobObject, ref int added)
    {
        Animator existing = MobMovementStateMachine.FindAnimator(mobObject.transform);
        if (existing != null)
            return existing;

        // A model of your own already there (a mesh anywhere under the mob): the Animator goes on its root, no placeholder.
        foreach (Renderer r in mobObject.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                continue;
            Transform host = r.transform;
            while (host != mobObject.transform && host.parent != null && host.parent != mobObject.transform)
                host = host.parent;
            Animator a = Undo.AddComponent<Animator>(host.gameObject);
            a.applyRootMotion = false;
            added++;
            return a;
        }

        Transform model = mobObject.transform.Find("Model");
        if (model == null)
        {
            var m = new GameObject("Model");
            Undo.RegisterCreatedObjectUndo(m, "Create Model");
            m.transform.SetParent(mobObject.transform, false);
            model = m.transform;
            added++;
        }
        Transform visual = model.Find("VisualModel");
        if (visual == null)
        {
            var v = new GameObject("VisualModel");
            Undo.RegisterCreatedObjectUndo(v, "Create Visual Model");
            v.transform.SetParent(model, false);
            visual = v.transform;
            added++;

            // Placeholder capsule (replace with your model).
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var mf = v.AddComponent<MeshFilter>();
            var mr = v.AddComponent<MeshRenderer>();
            mf.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            mr.sharedMaterial = temp.GetComponent<MeshRenderer>().sharedMaterial;
            Object.DestroyImmediate(temp);
            v.transform.localPosition = new Vector3(0f, 1f, 0f);
        }
        Animator animator = visual.GetComponent<Animator>();
        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(visual.gameObject);
            animator.applyRootMotion = false;
            added++;
        }
        return animator;
    }

    /// <summary>Creates a Mob Profile asset (from the mob's current fields, or a preset) and assigns it.</summary>
    public static MobProfile CreateProfileFor(Mob mob, MobProfile.Preset? preset)
    {
        MobProfile p;
        if (preset.HasValue)
        {
            p = MobProfile.CreatePreset(preset.Value);
        }
        else
        {
            p = ScriptableObject.CreateInstance<MobProfile>();
            MobProfile legacy = MobProfile.FromLegacy(mob);
            EditorUtility.CopySerialized(legacy, p);
            Object.DestroyImmediate(legacy);
            p.hideFlags = HideFlags.None;
        }
        string folder = FolderFor(mob.gameObject);
        string niceType = string.IsNullOrEmpty(mob.type) ? mob.name : mob.type;
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{niceType} Profile.asset");
        AssetDatabase.CreateAsset(p, path);
        AssetDatabase.SaveAssets();

        AssignProfile(mob, p);
        EditorGUIUtility.PingObject(p);
        return p;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null && value != null && p.objectReferenceValue == null)
            p.objectReferenceValue = value;
    }

    private static void SetString(SerializedObject so, string field, string value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null && string.IsNullOrEmpty(p.stringValue))
            p.stringValue = value;
    }

    private static void SetFloat(SerializedObject so, string field, float value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.floatValue = value;
    }

    private static void SetInt(SerializedObject so, string field, int value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) p.intValue = value;
    }
}
#endif
