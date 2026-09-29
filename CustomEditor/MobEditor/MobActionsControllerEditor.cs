using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.AI;
#endif

#if UNITY_EDITOR
/// <summary>
/// Mob inspector. Top: one-click setup, the behaviour profile, component status with Add buttons, setup checks with
/// Fix buttons, an abilities/absorption summary and (in Play mode) a live view of the AI. Below: the Mob fields in
/// labelled groups (identity and targets, home and patrol, basic attack, fallback behaviour, runtime, compatibility)
/// with helper buttons.
/// </summary>
[CustomEditor(typeof(MobActionsController))]
public class MobActionsControllerEditor : Editor
{
    private static bool showHelp;
    private static bool showStatus = true;
    private static bool showChecks = true;
    private static bool showLive = true;
    private static bool showFallback;
    private static bool showRuntime;
    private static bool showCompat;
    private static bool showProfileInline;

    private struct Issue
    {
        public MessageType type;
        public string message;
        public string fixLabel;
        public System.Action fix;
    }

    private readonly List<Issue> issues = new List<Issue>();
    private System.Action pendingFix;

    // Checks (NavMesh lookup, profile and ability validation) run at most twice per second, only on Layout events,
    // instead of on every redraw. Play mode refreshes about 8 times per second instead of every editor frame.
    private double nextCheck;
    private bool forceCheck = true;
    private static readonly HashSet<MobActionsControllerEditor> live = new HashSet<MobActionsControllerEditor>();
    private static double nextLiveRepaint;

    [InitializeOnLoadMethod]
    private static void HookLiveRepaint()
    {
        EditorApplication.update -= TickLiveRepaint;
        EditorApplication.update += TickLiveRepaint;
    }

    private static void TickLiveRepaint()
    {
        if (live.Count == 0 || EditorApplication.timeSinceStartup < nextLiveRepaint)
            return;
        nextLiveRepaint = EditorApplication.timeSinceStartup + 0.12;
        var copy = new List<MobActionsControllerEditor>(live);
        live.Clear();
        foreach (MobActionsControllerEditor e in copy)
            if (e != null)
                e.Repaint();
    }

    private bool ChecksDue()
    {
        if (Event.current == null || Event.current.type != EventType.Layout)
            return false;
        double now = EditorApplication.timeSinceStartup;
        if (!forceCheck && now < nextCheck)
            return false;
        forceCheck = false;
        nextCheck = now + 0.5;
        return true;
    }

    private static readonly string[] Handled =
    {
        "m_Script", "profile", "teamOverride", "type", "Preys", "mobTypes",
        "patrolRoute", "patrolPoints", "patrolPointsRelativeToHome", "currentPatrolPoint",
        "biteDamage", "attackDistance", "biteCooldown", "isPartialWait",
        "wanderDistance", "maxWalkTime", "idleTime", "detectionRange", "escapeMaxDistance", "maxChaseTime", "playerHasMaxChaseTime",
        "currentPredator", "currentChaseTarget", "currentPlayerTarget",
        "detectionCast", "detectionDistance", "offSetDetectionDistance", "mobTransform", "stoppingMargin", "statusController",
    };

    public override void OnInspectorGUI()
    {
        var mob = (MobActionsController)target;
        GameObject go = mob.gameObject;
        pendingFix = null;
        if (ChecksDue())
        {
            issues.Clear();
            Validate(mob);
        }

        DrawHelp();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Quick Setup", EditorStyles.boldLabel);
        if (GUILayout.Button(new GUIContent("Auto-Configure All Components",
                "Adds and wires everything a mob needs: NavMeshAgent, kinematic Rigidbody, MobStatusController + Health/Speed Managers, " +
                "MobMovementStateMachine (AI), MobAbilityController, CombatEntity, Animator references. It also creates (or reuses) a " +
                "'<Type> Profile' asset if none is assigned, and a '<Type> Basic Attack' ability asset when the mob would otherwise " +
                "use the invisible automatic bite, so both can be seen and edited. Existing settings are kept."),
                GUILayout.Height(26)))
            MobSetupUtility.SetupMob(go, false);
        EditorGUILayout.EndVertical();

        DrawProfile(mob);
        DrawComponents(go);
        DrawChecks(mob);
        DrawAbilitySummary(go);
        if (Application.isPlaying)
            DrawLive(mob);

        serializedObject.Update();
        DrawIdentity(mob);
        DrawPatrol(mob);
        DrawBasicAttack(mob);
        DrawFallback(mob);
        DrawRuntimeAndCompat();
        DrawPropertiesExcluding(serializedObject, Handled);
        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
            forceCheck = true;
        if (Application.isPlaying)
            live.Add(this);
        if (pendingFix != null)
        {
            System.Action fix = pendingFix;
            pendingFix = null;
            fix();
            forceCheck = true;
            GUIUtility.ExitGUI();
        }
    }

    // ------------------------------------------------------------------ help
    private static void DrawHelp()
    {
        showHelp = EditorGUILayout.BeginFoldoutHeaderGroup(showHelp, "How a mob is put together");
        if (showHelp)
        {
            EditorGUILayout.HelpBox(
                "THIS COMPONENT (Mob) - identity (Type, Preys, Team), home and patrol, the automatic basic attack, and the Profile.\n" +
                "MOB PROFILE (asset) - how it behaves: temperament, senses, movement, combat style, dodging, fleeing, animation names.\n" +
                "MobAbilityController - its abilities (① Ability Slots, ② Legacy Abilities) and ③ what the player can absorb.\n" +
                "MobMovementStateMachine - the AI itself (needs no settings besides references).\n" +
                "MobStatusController + Health/Speed Managers - health, speed and death.\n" +
                "NavMeshAgent - movement on the baked NavMesh. CombatEntity (added automatically) - team, body size, crowd control.",
                MessageType.Info);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void Section(string title, string help)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(help))
            EditorGUILayout.LabelField(help, EditorStyles.wordWrappedMiniLabel);
    }

    // ------------------------------------------------------------------ profile
    private void DrawProfile(MobActionsController mob)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Behaviour Profile", EditorStyles.boldLabel);
        serializedObject.Update();
        SerializedProperty profileProp = serializedObject.FindProperty("profile");
        EditorGUILayout.PropertyField(profileProp, new GUIContent("Profile",
            "Mob Profile asset: temperament, senses, movement, combat style, dodging, fleeing, animation. Share one profile between all mobs of a type."));
        serializedObject.ApplyModifiedProperties();
        var profile = profileProp.objectReferenceValue as MobProfile;
        if (profile == null)
        {
            EditorGUILayout.HelpBox("No profile: behaviour is built from the 'Fallback Behaviour' fields below (Detection Range, Wander Distance, Preys...). " +
                                    "Create a profile to tune senses, combat style, dodging, fleeing and animation.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Create From These Settings", "Save a new Mob Profile next to this prefab, filled from the fallback fields, and assign it.")))
                MobSetupUtility.CreateProfileFor(mob, null);
            if (GUILayout.Button(new GUIContent("Create From Preset ▾", "Save a new Mob Profile from a ready-made behaviour and assign it.")))
            {
                var menu = new GenericMenu();
                foreach (MobProfile.Preset p in System.Enum.GetValues(typeof(MobProfile.Preset)))
                {
                    MobProfile.Preset captured = p;
                    menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(p.ToString())), false, () => MobSetupUtility.CreateProfileFor(mob, captured));
                }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.LabelField(profile.DescribeBehaviour(), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.BeginHorizontal();
            showProfileInline = EditorGUILayout.Foldout(showProfileInline, new GUIContent("Edit Profile here",
                "Show every profile setting inline. You are editing the shared asset: every mob using this profile changes."), true);
            if (GUILayout.Button(new GUIContent("Make Unique Copy", "Duplicate the profile for this mob only (next to the original) and assign the copy."),
                    EditorStyles.miniButton, GUILayout.Width(112)))
                pendingFix = () => MakeUniqueProfile(mob, profile);
            if (GUILayout.Button(new GUIContent("Select", "Select the profile asset in the Project window."), EditorStyles.miniButton, GUILayout.Width(52)))
            {
                Selection.activeObject = profile;
                EditorGUIUtility.PingObject(profile);
            }
            EditorGUILayout.EndHorizontal();
            if (showProfileInline)
                DrawInline(profile, "Shared asset: changes apply to every mob that uses this profile. Use 'Make Unique Copy' to change only this mob.");
        }
        if (profile == null && Application.isPlaying)
        {
            EditorGUILayout.LabelField("Runtime profile (built from the fallback fields while playing):", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(mob.Profile.DescribeBehaviour(), EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button(new GUIContent("Save Runtime Profile As Asset", "Save what the mob is using right now as a Mob Profile asset (assign it after leaving Play mode).")))
            {
                MobProfile copy = Instantiate(mob.Profile);
                copy.hideFlags = HideFlags.None;
                string path = AssetDatabase.GenerateUniqueAssetPath($"{MobSetupUtility.FolderFor(mob.gameObject)}/{(string.IsNullOrEmpty(mob.type) ? mob.name : mob.type)} Profile.asset");
                AssetDatabase.CreateAsset(copy, path);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(copy);
            }
        }
        EditorGUILayout.EndVertical();
    }

    private Editor inlineEditor;

    private void DrawInline(Object asset, string note)
    {
        CreateCachedEditor(asset, null, ref inlineEditor);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(note, EditorStyles.wordWrappedMiniLabel);
        EditorGUI.indentLevel++;
        inlineEditor.OnInspectorGUI();
        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
    }

    private void OnDisable()
    {
        if (inlineEditor != null)
            DestroyImmediate(inlineEditor);
    }

    private void MakeUniqueProfile(MobActionsController mob, MobProfile profile)
    {
        string src = AssetDatabase.GetAssetPath(profile);
        string folder = string.IsNullOrEmpty(src) ? MobSetupUtility.FolderFor(mob.gameObject) : System.IO.Path.GetDirectoryName(src).Replace('\\', '/');
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{CleanName(mob.name)} Profile.asset");
        if (!AssetDatabase.CopyAsset(src, path))
            return;
        var copy = AssetDatabase.LoadAssetAtPath<MobProfile>(path);
        Modify(mob, "Unique Mob Profile", so => so.FindProperty("profile").objectReferenceValue = copy);
        EditorGUIUtility.PingObject(copy);
    }

    // ------------------------------------------------------------------ components
    private static void DrawComponents(GameObject go)
    {
        showStatus = EditorGUILayout.BeginFoldoutHeaderGroup(showStatus, "Components");
        if (showStatus)
        {
            Row<NavMeshAgent>(go, "NavMeshAgent", "Moves the mob on the baked NavMesh. Required.");
            Row<MobStatusController>(go, "MobStatusController", "Health, speed and death. Required.");
            RowInChildren<HealthManager>(go, "Health Manager", "HP. Without it the mob cannot be hurt or die.");
            RowInChildren<SpeedManager>(go, "Speed Manager", "Base speed and slows/hastes. Optional but recommended.");
            Row<MobMovementStateMachine>(go, "MobMovementStateMachine (AI)", "The AI. Without it the mob does nothing.");
            Row<MobAbilityController>(go, "MobAbilityController", "Abilities and absorption. Without it the mob never attacks.");
            Row<CombatEntity>(go, "CombatEntity", "Team, body size and crowd control. Added automatically at runtime if missing.");
            bool hasCollider = go.GetComponentInChildren<Collider>() != null;
            EditorGUILayout.LabelField(new GUIContent((hasCollider ? "✓ " : "✗ ") + "Collider", "Abilities, projectiles and the player hit the mob through its collider."),
                hasCollider ? EditorStyles.label : EditorStyles.boldLabel);
            Animator a = MobMovementStateMachine.FindAnimator(go.transform);
            string animLabel = a == null ? "Animator (none - the mob is not animated)" : (a.runtimeAnimatorController == null ? $"Animator on '{a.name}' (no controller)" : $"Animator on '{a.name}'");
            EditorGUILayout.LabelField(new GUIContent((a != null && a.runtimeAnimatorController != null ? "✓ " : "✗ ") + animLabel,
                "Found in Model/VisualModel (old layout) or the first Animator in the children."));
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void Row<T>(GameObject go, string label, string tooltip) where T : Component
    {
        bool ok = go.GetComponent<T>() != null;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(new GUIContent((ok ? "✓ " : "✗ ") + label, tooltip), ok ? EditorStyles.label : EditorStyles.boldLabel);
        if (!ok && GUILayout.Button(new GUIContent("Add", "Add " + label), GUILayout.Width(50)))
            Undo.AddComponent<T>(go);
        EditorGUILayout.EndHorizontal();
    }

    private static void RowInChildren<T>(GameObject go, string label, string tooltip) where T : Component
    {
        bool ok = go.GetComponentInChildren<T>() != null;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(new GUIContent((ok ? "✓ " : "✗ ") + label, tooltip), ok ? EditorStyles.label : EditorStyles.boldLabel);
        if (!ok && GUILayout.Button(new GUIContent("Add", "Add " + label), GUILayout.Width(50)))
            Undo.AddComponent<T>(go);
        EditorGUILayout.EndHorizontal();
    }

    // ------------------------------------------------------------------ checks
    private void DrawChecks(MobActionsController mob)
    {
        int errors = 0, warnings = 0;
        foreach (Issue i in issues)
        {
            if (i.type == MessageType.Error) errors++;
            else if (i.type == MessageType.Warning) warnings++;
        }
        if (issues.Count == 0)
        {
            EditorGUILayout.LabelField("✓ Setup checks passed.", EditorStyles.miniLabel);
            return;
        }
        showChecks = EditorGUILayout.BeginFoldoutHeaderGroup(showChecks, $"Setup checks ({errors} errors, {warnings} warnings, {issues.Count - errors - warnings} tips)");
        if (showChecks)
        {
            foreach (Issue i in issues)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(i.message, i.type);
                if (i.fix != null && GUILayout.Button(new GUIContent(i.fixLabel ?? "Fix", "Apply this fix (Undo reverts it)."), GUILayout.Width(96), GUILayout.MinHeight(38)))
                    pendingFix = i.fix;
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void Add(MessageType type, string message, string fixLabel = null, System.Action fix = null)
    {
        issues.Add(new Issue { type = type, message = message, fixLabel = fixLabel, fix = fix });
    }

    private void Validate(MobActionsController mob)
    {
        GameObject go = mob.gameObject;
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (agent == null)
            Add(MessageType.Error, "Missing NavMeshAgent: the mob cannot move.", "Configure", () => MobSetupUtility.SetupMob(go, false));
        if (go.GetComponent<MobMovementStateMachine>() == null)
            Add(MessageType.Error, "Missing MobMovementStateMachine: the mob will not think or move.", "Add AI", () => Undo.AddComponent<MobMovementStateMachine>(go));
        if (go.GetComponent<MobAbilityController>() == null)
            Add(MessageType.Warning, "Missing MobAbilityController: the mob never attacks (not even the basic attack).", "Add", () => Undo.AddComponent<MobAbilityController>(go));

        MobStatusController status = go.GetComponent<MobStatusController>();
        if (status != null && status.HealthManager == null && go.GetComponentInChildren<HealthManager>() == null)
            Add(MessageType.Error, "No HealthManager: the mob cannot take damage or die.", "Add", () => Undo.AddComponent<HealthManager>(go));

        Collider col = go.GetComponentInChildren<Collider>();
        if (col == null)
            Add(MessageType.Error, "No Collider: abilities and projectiles cannot hit this mob.", "Add Capsule", () => AddCapsule(go, agent));

        // Identity.
        if (string.IsNullOrEmpty(mob.type))
            Add(MessageType.Warning, "Type is empty. Other mobs recognise prey, predators and allies by Type.", "Use Name",
                () => SetType(mob, CleanName(go.name)));
        else if (mob.mobTypes != null && !mob.mobTypes.Contains(mob.type))
            Add(MessageType.Info, $"Type '{mob.type}' is not in the Mob Types helper list.", "Add To List", () => AddToTypes(mob, mob.type));

        List<string> preys = mob.PreysReference;
        if (preys != null)
        {
            if (!string.IsNullOrEmpty(mob.type) && preys.Contains(mob.type))
                Add(MessageType.Warning, $"The mob hunts its own type '{mob.type}', but mobs of the same type are allies unless a Team Override is set.");
            foreach (string p in preys)
            {
                if (string.IsNullOrEmpty(p))
                    Add(MessageType.Warning, "Preys has an empty entry.", "Remove", () => RemovePrey(mob, p));
                else if (p != "Player" && mob.mobTypes != null && !mob.mobTypes.Contains(p))
                    Add(MessageType.Info, $"Prey '{p}' is not in the Mob Types list - check the spelling (it must match the other mob's Type exactly).", "Add To List",
                        () => AddToTypes(mob, p));
            }
        }

        // Physics.
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
            Add(MessageType.Warning, "The Rigidbody is not kinematic: it fights the NavMeshAgent (it is made kinematic at runtime).", "Make Kinematic",
                () => { Undo.RecordObject(rb, "Kinematic Rigidbody"); rb.isKinematic = true; rb.useGravity = false; });
        CapsuleCollider capsule = go.GetComponent<CapsuleCollider>();
        if (agent != null && capsule != null && Mathf.Abs(agent.radius - capsule.radius) > 0.3f)
            Add(MessageType.Warning, $"NavMeshAgent radius ({agent.radius:0.##}) and collider radius ({capsule.radius:0.##}) differ a lot: mobs may overlap or keep too far apart.",
                "Match Agent", () => { Undo.RecordObject(agent, "Match Agent Size"); agent.radius = capsule.radius; agent.height = capsule.height; });
        if (col != null && (CombatSettings.Instance.characterLayers.value & (1 << col.gameObject.layer)) == 0)
            Add(MessageType.Warning, $"The collider's layer '{LayerMask.LayerToName(col.gameObject.layer)}' is not in Combat Settings ▸ Character Layers: abilities will not hit this mob.");
        if (agent != null && go.scene.IsValid() && !EditorUtility.IsPersistent(go) && !Application.isPlaying &&
            !NavMesh.SamplePosition(go.transform.position, out _, 2f, NavMesh.AllAreas))
            Add(MessageType.Warning, "This mob is not on (or near) a baked NavMesh. Bake the NavMesh or move the mob onto it.");

        // Profile.
        MobProfile profile = mob.ProfileAsset;
        if (profile != null)
        {
            var e = new List<string>();
            var w = new List<string>();
            profile.Validate(e, w);
            foreach (string s in e) Add(MessageType.Error, "Profile: " + s);
            foreach (string s in w) Add(MessageType.Warning, "Profile: " + s);
        }
        MobAggression aggression = profile != null ? profile.aggression : MobAggression.Aggressive;
        bool noPreys = preys == null || preys.Count == 0;
        if (aggression == MobAggression.Aggressive && noPreys)
            Add(MessageType.Warning, "The mob is Aggressive but Preys is empty: it only fights back when attacked.", "+ Player", () => AddPrey(mob, "Player"));

        // Patrol.
        Transform route = RouteOf(mob);
        if (route != null)
        {
            if (route.IsChildOf(go.transform))
                Add(MessageType.Error, "Patrol Route is a child of the mob: it moves with the mob, so the mob never reaches a point. Put the route outside the mob.");
            else if (route.childCount == 0)
                Add(MessageType.Warning, "Patrol Route has no children. Each child is one patrol point.");
        }
        else if (mob.PatrolPoints != null && mob.PatrolPoints.Length > 0 && EditorUtility.IsPersistent(go) && !RelativePatrol())
            Add(MessageType.Warning, "Absolute patrol points on a prefab: every spawned copy walks to the same world positions.", "Make Relative",
                () => MakeRelative(mob));

        // Attacks.
        MobAbilityController abilities = go.GetComponent<MobAbilityController>();
        if (abilities != null)
        {
            var e = new List<string>();
            var w = new List<string>();
            abilities.Validate(e, w);
            foreach (string s in e) Add(MessageType.Error, "Abilities: " + s);
            foreach (string s in w) Add(MessageType.Warning, "Abilities: " + s);
        }
    }

    // ------------------------------------------------------------------ abilities summary
    private static void DrawAbilitySummary(GameObject go)
    {
        MobAbilityController c = go.GetComponent<MobAbilityController>();
        if (c == null)
            return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Abilities & Absorption (edit on MobAbilityController)", EditorStyles.boldLabel);
        int slots = 0;
        foreach (AbilitySlot s in c.Slots)
            if (s != null && s.ability != null && s.Grant == null) slots++;
        int legacy = 0;
        foreach (AbilityHolder h in c.LegacyAbilities)
            if (h != null && h.abilityEffect != null) legacy++;
        EditorGUILayout.LabelField($"{slots} ability slot(s), {legacy} legacy abilit{(legacy == 1 ? "y" : "ies")}" +
                                   (slots + legacy == 0 ? " - only the automatic basic attack" : ""), EditorStyles.miniLabel);
        if (!c.Absorbable)
        {
            EditorGUILayout.LabelField("Absorption: off", EditorStyles.miniLabel);
        }
        else
        {
            float perKill = c.EffectiveChancePerKill * 100f;
            var parts = new List<string>();
            foreach (MobAbsorptionEntry e in c.AbsorptionTable)
                if (e != null && e.sharePercent > 0f && MobAbilityController.EntryAllowed(e))
                    parts.Add($"{e.DisplayName} {e.sharePercent:0.#}% ({perKill * e.sharePercent / 100f:0.##}%/kill)");
            string table = c.AbsorptionTable.Count == 0 ? "equal shares (table not set up yet - open MobAbilityController)" :
                (parts.Count == 0 ? "no ability has a share" : string.Join(", ", parts));
            EditorGUILayout.LabelField($"Absorption: {perKill:0.##}% per kill → {table}", EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.EndVertical();
    }

    // ------------------------------------------------------------------ field groups
    private void DrawIdentity(MobActionsController mob)
    {
        Section("Identity & Targets",
            "Type = what this mob is. Preys = what it hunts ('Player' for players, or other mobs' Types). Mobs of the same " +
            "Type (or Team Override) are allies and answer each other's calls for help.");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("type"));
        if (GUILayout.Button(new GUIContent("▾", "Pick a type from the Mob Types list, or use the object's name."), GUILayout.Width(24)))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent($"Use Object Name ({CleanName(mob.name)})"), false, () => SetType(mob, CleanName(mob.name)));
            menu.AddSeparator("");
            if (mob.mobTypes != null)
                foreach (string t in mob.mobTypes)
                    if (!string.IsNullOrEmpty(t) && t != "Player")
                    {
                        string captured = t;
                        menu.AddItem(new GUIContent(t), mob.type == t, () => SetType(mob, captured));
                    }
            menu.ShowAsContext();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("teamOverride"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Preys"), new GUIContent("Preys",
            "What this mob hunts: 'Player' for players, and mob Types (e.g. 'Sheep'). Must match the other mob's Type exactly."), true);

        EditorGUILayout.BeginHorizontal();
        List<string> preys = mob.PreysReference;
        using (new EditorGUI.DisabledScope(preys != null && preys.Contains("Player")))
            if (GUILayout.Button(new GUIContent("+ Player", "This mob attacks players."))) pendingFix = () => AddPrey(mob, "Player");
        if (GUILayout.Button(new GUIContent("+ Prey Type ▾", "Add a mob type from the Mob Types list.")))
        {
            var menu = new GenericMenu();
            if (mob.mobTypes != null)
                foreach (string t in mob.mobTypes)
                    if (!string.IsNullOrEmpty(t) && t != "Player" && t != mob.type)
                    {
                        string captured = t;
                        bool has = preys != null && preys.Contains(t);
                        if (has) menu.AddDisabledItem(new GUIContent(t));
                        else menu.AddItem(new GUIContent(t), false, () => AddPrey(mob, captured));
                    }
            menu.ShowAsContext();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mobTypes"), new GUIContent("Mob Types (helper list)",
            "Just a list of the type names used in your game, for the pickers above and spelling checks. It changes no behaviour."), true);
    }

    private void DrawPatrol(MobActionsController mob)
    {
        Section("Home & Patrol",
            "Home is where the mob spawns (it returns there after fights). With patrol points it walks between them; " +
            "without, it wanders around home (Profile ▸ Wander Radius).");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("patrolRoute"));
        using (new EditorGUI.DisabledScope(RouteOf(mob) != null))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("patrolPoints"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("patrolPointsRelativeToHome"));
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("currentPatrolPoint"));

        EditorGUILayout.BeginHorizontal();
        bool isAsset = EditorUtility.IsPersistent(mob.gameObject);
        using (new EditorGUI.DisabledScope(isAsset || RouteOf(mob) != null))
            if (GUILayout.Button(new GUIContent("Create Route Object", "Scene mobs: create a separate 'Patrol Route' object with 4 points around the mob (move the children to edit the route) and assign it.")))
                pendingFix = () => CreateRoute(mob);
        if (GUILayout.Button(new GUIContent("Square Around Home", "Set 4 patrol points in a square around the spawn point (relative to home, so it works on prefabs).")))
            pendingFix = () => SquarePatrol(mob);
        using (new EditorGUI.DisabledScope(RouteOf(mob) == null))
            if (GUILayout.Button(new GUIContent("Route → Points", "Copy the route's children into Patrol Points and clear the route (relative to home if that option is on).")))
                pendingFix = () => BakeRoute(mob);
        EditorGUILayout.EndHorizontal();
        int n = mob.PatrolPointCount;
        EditorGUILayout.LabelField(n > 0 ? $"{n} patrol point(s) - drawn in the Scene view when the mob is selected." : "No patrol points: the mob wanders around home.",
            EditorStyles.miniLabel);
    }

    private void DrawBasicAttack(MobActionsController mob)
    {
        Section("Automatic Basic Attack",
            "A simple melee bite made from these numbers. It is used only when the MobAbilityController has no melee ability " +
            "(and Profile ▸ Auto Basic Attack is on). Set Bite Damage to 0 to disable it.");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("biteDamage"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackDistance"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("biteCooldown"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isPartialWait"));
        MobAbilityController c = mob.GetComponent<MobAbilityController>();
        if (Application.isPlaying && c != null)
            EditorGUILayout.LabelField(c.BasicAttack != null ? $"In use: '{c.BasicAttack.DisplayName}'." : "Not in use (the mob has a melee ability, or Bite Damage is 0).", EditorStyles.miniLabel);
        using (new EditorGUI.DisabledScope(c == null || Application.isPlaying))
        {
            if (GUILayout.Button(new GUIContent("Save As Ability Asset ➜ Ability Slots",
                    "Turn the automatic bite into a real Ability Definition asset (from the numbers above), put it in the MobAbilityController's " +
                    "Ability Slots, and edit it like any other ability (shape, effects, animation, AI hints). The automatic one is then no longer created.")))
                pendingFix = () =>
                {
                    AbilityDefinition def = MobSetupUtility.CreateBasicAttackAsset(c, mob);
                    EditorGUIUtility.PingObject(def);
                };
        }
    }

    private void DrawFallback(MobActionsController mob)
    {
        bool hasProfile = mob.ProfileAsset != null;
        EditorGUILayout.Space(6);
        showFallback = EditorGUILayout.Foldout(showFallback, new GUIContent(hasProfile ? "Fallback Behaviour (ignored: a Profile is assigned)" : "Fallback Behaviour (used: no Profile assigned)",
            "Old per-mob settings. Used to build the behaviour only when no Mob Profile is assigned."), true, EditorStyles.foldoutHeader);
        if (!showFallback)
            return;
        if (hasProfile)
            EditorGUILayout.HelpBox("These are ignored while a Profile is assigned - edit the Profile instead.", MessageType.None);
        using (new EditorGUI.DisabledScope(hasProfile))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("detectionRange"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wanderDistance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxWalkTime"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("idleTime"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("escapeMaxDistance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxChaseTime"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playerHasMaxChaseTime"));
        }
    }

    private void DrawRuntimeAndCompat()
    {
        EditorGUILayout.Space(4);
        showRuntime = EditorGUILayout.Foldout(showRuntime, new GUIContent("Runtime (read only)", "Set by the AI while playing."), true, EditorStyles.foldoutHeader);
        if (showRuntime)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("currentPlayerTarget"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("currentChaseTarget"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("currentPredator"));
            }
        }
        showCompat = EditorGUILayout.Foldout(showCompat, new GUIContent("Compatibility (old detection box and references)",
            "Kept for old scripts; the AI uses the Profile's sight, field of view and hearing instead."), true, EditorStyles.foldoutHeader);
        if (showCompat)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("statusController"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mobTransform"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("detectionCast"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("detectionDistance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("offSetDetectionDistance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("stoppingMargin"));
        }
    }

    // ------------------------------------------------------------------ live
    private static void DrawLive(MobActionsController mob)
    {
        MobMovementStateMachine machine = mob.AI;
        if (machine == null || machine.Context == null)
            return;
        MobMovementContext c = machine.Context;
        showLive = EditorGUILayout.BeginFoldoutHeaderGroup(showLive, "AI (live)");
        if (showLive)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("State", $"{machine.CurrentStateKey}  ({machine.TimeInState:0.0}s)");
            EditorGUILayout.LabelField("Mode", $"{c.Brain.Mode}   LOD {c.Scheduler.Current}");
            EditorGUILayout.LabelField("Target", c.Brain.Target != null ? $"{c.Brain.Target.name} ({c.Brain.TargetDistance:0.0}m, {(c.Brain.TargetVisible ? "visible" : "hidden")})" : "-");
            EditorGUILayout.LabelField("Threat", c.Brain.Threat != null ? c.Brain.Threat.name : "-");
            EditorGUILayout.LabelField("Role", $"{c.Brain.Style}  range {c.Brain.PreferredMin:0.#}-{c.Brain.PreferredMax:0.#}m");
            EditorGUILayout.LabelField("Health", $"{c.Entity.HealthRatio:P0}  {(c.Entity.IsStunned ? "STUNNED " : "")}{(c.Entity.IsRooted ? "ROOTED " : "")}{(c.Brain.InDanger ? "IN DANGER" : "")}");
            foreach (MobMemoryEntry e in c.Memory.Entries)
            {
                if (e.entity == null)
                    continue;
                EditorGUILayout.LabelField("  " + e.entity.name, $"{e.relation}  aware {e.awareness:0.00}{(e.detected ? " ✓" : "")}  {(e.visible ? "seen" : $"last seen {e.TimeSinceSeen:0}s")}", EditorStyles.miniLabel);
            }
            if (c.Memory.HasInvestigatePoint)
                EditorGUILayout.LabelField("Investigating", c.Memory.InvestigatePoint.ToString("F1"));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ------------------------------------------------------------------ fixes
    private void Modify(MobActionsController mob, string undo, System.Action<SerializedObject> change)
    {
        var so = new SerializedObject(mob);
        so.Update();
        change(so);
        so.ApplyModifiedProperties();
        serializedObject.Update();
    }

    private static string CleanName(string n)
    {
        int i = n.IndexOf('(');
        if (i > 0) n = n.Substring(0, i);
        return n.Trim();
    }

    private void SetType(MobActionsController mob, string type) =>
        Modify(mob, "Set Mob Type", so => so.FindProperty("type").stringValue = type);

    private void AddToTypes(MobActionsController mob, string type) => Modify(mob, "Add Mob Type", so =>
    {
        SerializedProperty list = so.FindProperty("mobTypes");
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = type;
    });

    private void AddPrey(MobActionsController mob, string prey) => Modify(mob, "Add Prey", so =>
    {
        SerializedProperty list = so.FindProperty("Preys");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).stringValue == prey)
                return;
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = prey;
    });

    private void RemovePrey(MobActionsController mob, string prey) => Modify(mob, "Remove Prey", so =>
    {
        SerializedProperty list = so.FindProperty("Preys");
        for (int i = list.arraySize - 1; i >= 0; i--)
            if (list.GetArrayElementAtIndex(i).stringValue == prey)
            {
                list.DeleteArrayElementAtIndex(i);
                return;
            }
    });

    private Transform RouteOf(MobActionsController mob) => serializedObject.FindProperty("patrolRoute").objectReferenceValue as Transform;

    private bool RelativePatrol() => serializedObject.FindProperty("patrolPointsRelativeToHome").boolValue;

    private void MakeRelative(MobActionsController mob) => Modify(mob, "Relative Patrol Points", so =>
    {
        SerializedProperty pts = so.FindProperty("patrolPoints");
        Vector3 home = mob.transform.position;
        for (int i = 0; i < pts.arraySize; i++)
            pts.GetArrayElementAtIndex(i).vector3Value -= home;
        so.FindProperty("patrolPointsRelativeToHome").boolValue = true;
    });

    private void SquarePatrol(MobActionsController mob)
    {
        MobProfile p = mob.ProfileAsset;
        float half = Mathf.Clamp((p != null ? p.wanderRadius : mob.WanderDistance) * 0.5f, 2f, 15f);
        Modify(mob, "Square Patrol", so =>
        {
            so.FindProperty("patrolRoute").objectReferenceValue = null;
            SerializedProperty pts = so.FindProperty("patrolPoints");
            pts.arraySize = 4;
            pts.GetArrayElementAtIndex(0).vector3Value = new Vector3(half, 0f, half);
            pts.GetArrayElementAtIndex(1).vector3Value = new Vector3(half, 0f, -half);
            pts.GetArrayElementAtIndex(2).vector3Value = new Vector3(-half, 0f, -half);
            pts.GetArrayElementAtIndex(3).vector3Value = new Vector3(-half, 0f, half);
            so.FindProperty("patrolPointsRelativeToHome").boolValue = true;
            so.FindProperty("currentPatrolPoint").intValue = 0;
        });
    }

    private void CreateRoute(MobActionsController mob)
    {
        var route = new GameObject(CleanName(mob.name) + " Patrol Route");
        Undo.RegisterCreatedObjectUndo(route, "Create Patrol Route");
        route.transform.position = mob.transform.position;
        if (mob.transform.parent != null)
            route.transform.SetParent(mob.transform.parent, true);
        float r = 5f;
        Vector3[] offsets = { new Vector3(r, 0, r), new Vector3(r, 0, -r), new Vector3(-r, 0, -r), new Vector3(-r, 0, r) };
        for (int i = 0; i < offsets.Length; i++)
        {
            var point = new GameObject("Point " + (i + 1));
            point.transform.SetParent(route.transform, false);
            point.transform.localPosition = offsets[i];
        }
        Modify(mob, "Assign Patrol Route", so => so.FindProperty("patrolRoute").objectReferenceValue = route.transform);
        Selection.activeGameObject = route;
    }

    private void BakeRoute(MobActionsController mob)
    {
        Transform route = RouteOf(mob);
        if (route == null)
            return;
        Modify(mob, "Route To Patrol Points", so =>
        {
            bool relative = so.FindProperty("patrolPointsRelativeToHome").boolValue;
            Vector3 home = mob.transform.position;
            SerializedProperty pts = so.FindProperty("patrolPoints");
            pts.arraySize = route.childCount;
            for (int i = 0; i < route.childCount; i++)
                pts.GetArrayElementAtIndex(i).vector3Value = route.GetChild(i).position - (relative ? home : Vector3.zero);
            so.FindProperty("patrolRoute").objectReferenceValue = null;
        });
    }

    private static void AddCapsule(GameObject go, NavMeshAgent agent)
    {
        CapsuleCollider c = Undo.AddComponent<CapsuleCollider>(go);
        float h = agent != null ? agent.height : 2f;
        c.radius = agent != null ? agent.radius : 0.5f;
        c.height = h;
        c.center = new Vector3(0f, h * 0.5f, 0f);
    }
}
#endif
