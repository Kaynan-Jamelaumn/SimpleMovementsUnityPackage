#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>
/// Every check of a mob's setup, in one place: components, references between them, identity (type, preys), physics
/// (collider, rigidbody, agent, layers, NavMesh), the model and its Animator (controller, root motion, the parameters and
/// states the AI drives), profile, patrol, attacks, health and speed, item drops and networking. Each problem says what
/// breaks and, when it can, offers a fix; <see cref="Issue.safe"/> fixes are applied by Fix All. Used by the mob
/// inspector, the Mob Setup Validator window and the Project Validator (Mobs checks).
/// </summary>
public static class MobSetupValidator
{
    public enum Severity { Error, Warning, Tip }

    public sealed class Issue
    {
        public Severity severity;
        /// <summary>Group shown in the window ("Components", "Physics", "Animation"...).</summary>
        public string area;
        /// <summary>Stable key of the problem (finds the same issue again in a reloaded prefab).</summary>
        public string id;
        public string message;
        public string fixLabel;
        public Action fix;
        /// <summary>Fix All may apply it (it only adds, wires or corrects; it never deletes or changes a design choice).</summary>
        public bool safe;
        public Object context;
    }

    // ------------------------------------------------------------------ running
    /// <summary>The mobs under <paramref name="root"/> (one per Mob component).</summary>
    public static IEnumerable<Mob> MobsIn(GameObject root) =>
        root == null ? Enumerable.Empty<Mob>() : root.GetComponentsInChildren<Mob>(true);

    /// <summary>Every problem of the mob on <paramref name="go"/> (the object with the Mob component, or its root).</summary>
    public static List<Issue> Check(GameObject go)
    {
        var issues = new List<Issue>();
        if (go == null)
            return issues;
        Mob mob = go.GetComponent<Mob>() ?? go.GetComponentInChildren<Mob>(true);
        if (mob == null)
        {
            Add(issues, Severity.Error, "Components", "mob.none", "No Mob component (MobActionsController): this is not a mob yet.",
                "Configure", () => MobSetupUtility.SetupMob(go, false), true, go);
            return issues;
        }
        go = mob.gameObject;
        CheckComponents(go, mob, issues);
        CheckReferences(go, mob, issues);
        CheckIdentity(mob, issues);
        CheckPhysics(go, issues);
        CheckModel(go, mob, issues);
        CheckProfile(mob, issues);
        CheckPatrol(mob, issues);
        CheckAttacks(go, mob, issues);
        CheckStatus(go, issues);
        CheckDrops(go, issues);
        CheckNetwork(go, issues);
        return issues;
    }

    /// <summary>Applies every safe fix (re-checking after each, as one fix often solves others). Returns how many were applied.</summary>
    public static int FixAllSafe(GameObject go)
    {
        int applied = 0;
        var done = new HashSet<string>();
        while (applied < 60)
        {
            // Each problem is fixed once; checking again after every fix (one fix often solves others).
            Issue next = Check(go).FirstOrDefault(i => i.safe && i.fix != null && !done.Contains(i.id));
            if (next == null)
                break;
            done.Add(next.id);
            next.fix();
            applied++;
        }
        return applied;
    }

    private static void Add(List<Issue> issues, Severity s, string area, string id, string message,
        string fixLabel = null, Action fix = null, bool safe = false, Object context = null)
    {
        issues.Add(new Issue { severity = s, area = area, id = id, message = message, fixLabel = fixLabel, fix = fix, safe = safe && fix != null, context = context });
    }

    // ------------------------------------------------------------------ components
    private static void CheckComponents(GameObject go, Mob mob, List<Issue> issues)
    {
        Action configure = () => MobSetupUtility.SetupMob(go, false, false);
        if (go.transform.root.GetComponentsInChildren<Mob>(true).Length > 1)
            Add(issues, Severity.Error, "Components", "mob.multiple",
                "More than one Mob component in this hierarchy: each mob needs its own root object (the AI and abilities pick the wrong one).", context: go);

        int missingScripts = go.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        if (missingScripts > 0)
            Add(issues, Severity.Error, "Components", "mob.missingScripts",
                $"{missingScripts} component(s) whose script is missing (deleted or renamed): they do nothing and break the prefab's serialization.",
                "Remove", () =>
                {
                    foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                }, false, go);

        if (go.GetComponent<NavMeshAgent>() == null)
            Add(issues, Severity.Error, "Components", "agent.missing", "No NavMeshAgent: the mob cannot move.", "Add", configure, true, go);
        if (go.GetComponent<MobMovementStateMachine>() == null)
            Add(issues, Severity.Error, "Components", "ai.missing", "No MobMovementStateMachine: the mob does not think, move or fight.", "Add", configure, true, go);
        if (go.GetComponent<MobStatusController>() == null)
            Add(issues, Severity.Error, "Components", "status.missing", "No MobStatusController: no health, no death, no status effects.", "Add", configure, true, go);
        if (go.GetComponentInChildren<HealthManager>(true) == null)
            Add(issues, Severity.Error, "Components", "health.missing", "No HealthManager: the mob cannot take damage or die.", "Add", configure, true, go);
        if (go.GetComponentInChildren<SpeedManager>(true) == null)
            Add(issues, Severity.Warning, "Components", "speed.missing", "No SpeedManager: slows and hastes do nothing and the speed comes only from the agent.", "Add", configure, true, go);
        if (go.GetComponent<MobAbilityController>() == null)
            Add(issues, Severity.Warning, "Components", "abilities.missing", "No MobAbilityController: the mob never attacks (not even the basic attack).", "Add", configure, true, go);
        if (go.GetComponentInParent<CombatEntity>() == null && go.GetComponentInChildren<CombatEntity>(true) == null)
            Add(issues, Severity.Error, "Components", "entity.missing",
                "No CombatEntity: players and mobs cannot target it, it gives no threat, kills are not counted and it drops no items.", "Add", configure, true, go);
        if (go.GetComponentInChildren<Collider>(true) == null)
            Add(issues, Severity.Error, "Components", "collider.missing", "No Collider: weapons, abilities and projectiles cannot hit this mob.",
                "Add Fitted Capsule", () => FitCollider(go, true), true, go);
        if (go.GetComponent<Rigidbody>() == null)
            Add(issues, Severity.Warning, "Components", "rigidbody.missing",
                "No Rigidbody: trigger hit boxes (melee swings, hazards) do not detect a collider without one.", "Add Kinematic", () =>
                {
                    Rigidbody rb = Undo.AddComponent<Rigidbody>(go);
                    MakeKinematic(rb);
                }, true, go);
    }

    // ------------------------------------------------------------------ references
    private static void CheckReferences(GameObject go, Mob mob, List<Issue> issues)
    {
        MobMovementStateMachine ai = go.GetComponent<MobMovementStateMachine>();
        if (ai != null)
        {
            var missing = new List<string>();
            if (ai.MobReference == null) missing.Add("Mob");
            if (ai.AgentReference == null && go.GetComponent<NavMeshAgent>() != null) missing.Add("NavMeshAgent");
            if (ai.StatusReference == null && go.GetComponent<MobStatusController>() != null) missing.Add("Status Controller");
            if (ai.AnimatorReference == null && MobMovementStateMachine.FindAnimator(go.transform) != null) missing.Add("Animator");
            if (missing.Count > 0)
                Add(issues, Severity.Warning, "References", "wire.ai",
                    $"The AI (MobMovementStateMachine) has no {string.Join(", ", missing)} assigned (found again at runtime, but slower and it can pick the wrong one).",
                    "Wire", () =>
                    {
                        Undo.RecordObject(ai, "Wire Mob AI");
                        ai.AutoAssignReferences();
                        EditorUtility.SetDirty(ai);
                    }, true, ai);
        }
        MobStatusController status = go.GetComponent<MobStatusController>();
        if (status != null)
        {
            var so = new SerializedObject(status);
            bool noHealth = so.FindProperty("healthManager")?.objectReferenceValue == null && go.GetComponentInChildren<HealthManager>(true) != null;
            bool noSpeed = so.FindProperty("speedManager")?.objectReferenceValue == null && go.GetComponentInChildren<SpeedManager>(true) != null;
            if (noHealth || noSpeed)
                Add(issues, Severity.Tip, "References", "wire.status",
                    $"The Status Controller's {(noHealth && noSpeed ? "Health and Speed Managers are" : noHealth ? "Health Manager is" : "Speed Manager is")} not assigned (found at runtime).",
                    "Wire", () =>
                    {
                        var s = new SerializedObject(status);
                        SetIfEmpty(s, "healthManager", go.GetComponentInChildren<HealthManager>(true));
                        SetIfEmpty(s, "speedManager", go.GetComponentInChildren<SpeedManager>(true));
                        s.ApplyModifiedProperties();
                    }, true, status);
        }
        MobAbilityController abilities = go.GetComponent<MobAbilityController>();
        if (abilities != null)
        {
            var so = new SerializedObject(abilities);
            Animator anim = MobMovementStateMachine.FindAnimator(go.transform);
            bool noMob = so.FindProperty("mobActionController")?.objectReferenceValue == null && mob is MobActionsController;
            bool noAnim = so.FindProperty("animator")?.objectReferenceValue == null && anim != null;
            if (noMob || noAnim)
                Add(issues, Severity.Tip, "References", "wire.abilities",
                    $"The Ability Controller's {(noMob ? "Mob" : "")}{(noMob && noAnim ? " and " : "")}{(noAnim ? "Animator" : "")} not assigned (attack animations may not play).",
                    "Wire", () =>
                    {
                        var s = new SerializedObject(abilities);
                        SetIfEmpty(s, "mobActionController", mob as MobActionsController);
                        SetIfEmpty(s, "animator", anim);
                        s.ApplyModifiedProperties();
                    }, true, abilities);
        }
    }

    // ------------------------------------------------------------------ identity
    private static void CheckIdentity(Mob mob, List<Issue> issues)
    {
        if (string.IsNullOrWhiteSpace(mob.type))
            Add(issues, Severity.Warning, "Identity", "type.empty",
                "Type is empty: other mobs recognise prey, predators and allies by Type, and its assets are filed by Type.",
                "Use Name", () => ModifyMob(mob, "Set Mob Type", so => so.FindProperty("type").stringValue = CleanName(mob.name)), true, mob);
        else if (mob.mobTypes != null && !mob.mobTypes.Contains(mob.type))
            Add(issues, Severity.Tip, "Identity", "type.list", $"Type '{mob.type}' is not in the Mob Types helper list.",
                "Add To List", () => AddToList(mob, "mobTypes", mob.type), true, mob);

        List<string> preys = mob.PreysReference;
        if (preys == null)
            return;
        if (!string.IsNullOrEmpty(mob.type) && preys.Contains(mob.type))
            Add(issues, Severity.Warning, "Identity", "prey.self",
                $"The mob hunts its own type '{mob.type}', but mobs of the same type are allies unless a Team Override is set.", context: mob);
        for (int i = 0; i < preys.Count; i++)
        {
            string p = preys[i];
            if (string.IsNullOrWhiteSpace(p))
                Add(issues, Severity.Warning, "Identity", "prey.empty" + i, "Preys has an empty entry.",
                    "Remove", () => RemoveFromList(mob, "Preys", p), true, mob);
            else if (p != "Player" && mob.mobTypes != null && !mob.mobTypes.Contains(p))
                Add(issues, Severity.Tip, "Identity", "prey.unknown" + i,
                    $"Prey '{p}' is not in the Mob Types list: check the spelling (it must match the other mob's Type exactly).",
                    "Add To List", () => AddToList(mob, "mobTypes", p), false, mob);
        }
    }

    // ------------------------------------------------------------------ physics
    private static void CheckPhysics(GameObject go, List<Issue> issues)
    {
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null && (!rb.isKinematic || rb.useGravity))
            Add(issues, Severity.Warning, "Physics", "rb.kinematic",
                "The Rigidbody is not kinematic (or uses gravity): it fights the NavMeshAgent - jitter, sliding, falling through floors.",
                "Make Kinematic", () => MakeKinematic(rb), true, rb);

        Collider[] cols = go.GetComponentsInChildren<Collider>(true);
        Collider main = go.GetComponent<Collider>() ?? cols.FirstOrDefault();
        if (cols.Length > 0 && cols.All(c => c.isTrigger))
            Add(issues, Severity.Warning, "Physics", "collider.trigger",
                "The mob's only collider is a trigger: it does not block players or other mobs and ray hits ignore it.",
                "Make Solid", () => { Undo.RecordObject(main, "Solid Collider"); main.isTrigger = false; }, false, main);

        CapsuleCollider capsule = go.GetComponent<CapsuleCollider>();
        if (agent != null && capsule != null)
        {
            float scale = Mathf.Max(Mathf.Abs(go.transform.lossyScale.x), Mathf.Abs(go.transform.lossyScale.z));
            if (Mathf.Abs(agent.radius - capsule.radius) > 0.3f * Mathf.Max(1f, capsule.radius))
                Add(issues, Severity.Warning, "Physics", "agent.radius",
                    $"NavMeshAgent radius ({agent.radius:0.##}) and collider radius ({capsule.radius:0.##}) differ a lot: mobs overlap or keep too far apart and get stuck in doorways.",
                    "Match Agent", () => MatchAgent(agent, capsule), true, agent);
            if (Mathf.Abs(agent.height - capsule.height) > 0.5f * Mathf.Max(1f, capsule.height))
                Add(issues, Severity.Tip, "Physics", "agent.height",
                    $"NavMeshAgent height ({agent.height:0.##}) and collider height ({capsule.height:0.##}) differ: low ceilings and other agents are judged wrongly.",
                    "Match Agent", () => MatchAgent(agent, capsule), true, agent);
            if (scale > 0.0001f && Mathf.Abs(scale - 1f) > 0.01f)
                Add(issues, Severity.Tip, "Physics", "agent.scale",
                    $"The mob's root is scaled ×{scale:0.##}: the agent and capsule sizes are multiplied by it (the inspector shows unscaled values).", context: go);
        }

        Vector3 s = go.transform.localScale;
        if (Mathf.Abs(s.x - s.y) > 0.01f || Mathf.Abs(s.x - s.z) > 0.01f)
            Add(issues, Severity.Warning, "Physics", "scale.nonUniform",
                $"Non-uniform scale on the mob's root ({s.x:0.##}, {s.y:0.##}, {s.z:0.##}): capsules and the agent stay round, so hits and pathing do not match the model. Scale the model child instead.",
                context: go);

        Bounds? model = ModelBounds(go);
        if (capsule != null && model.HasValue)
        {
            float modelH = model.Value.size.y, capH = capsule.height * Mathf.Abs(go.transform.lossyScale.y);
            if (modelH > 0.05f && (capH < modelH * 0.5f || capH > modelH * 2f))
                Add(issues, Severity.Warning, "Physics", "collider.fit",
                    $"The capsule ({capH:0.##} m tall) does not match the model ({modelH:0.##} m): hits miss the body or land in the air.",
                    "Fit To Model", () => FitCollider(go, false), false, capsule);
        }

        if (main != null)
        {
            int layers = CombatSettings.Instance.characterLayers.value;
            if (layers != ~0 && layers != 0 && (layers & (1 << main.gameObject.layer)) == 0)
            {
                int first = FirstLayer(layers);
                Add(issues, Severity.Warning, "Physics", "collider.layer",
                    $"The collider is on layer '{LayerMask.LayerToName(main.gameObject.layer)}', which is not in Combat Settings ▸ Character Layers: abilities, hit boxes and AI perception ignore this mob.",
                    first >= 0 ? $"Use '{LayerMask.LayerToName(first)}'" : null,
                    first >= 0 ? () => SetLayer(go, first) : (Action)null, false, main);
            }
        }

        if (agent != null)
        {
            if (NavMesh.GetSettingsByID(agent.agentTypeID).agentTypeID != agent.agentTypeID)
                Add(issues, Severity.Error, "Physics", "agent.type",
                    "The NavMeshAgent's Agent Type no longer exists (deleted in Navigation ▸ Agents): it never finds a NavMesh.",
                    "Use Humanoid", () => { Undo.RecordObject(agent, "Agent Type"); agent.agentTypeID = 0; }, true, agent);
            if (agent.speed <= 0f)
                Add(issues, Severity.Warning, "Physics", "agent.speed", "NavMeshAgent Speed is 0: the mob never moves (the AI scales this speed).",
                    "Set 3.5", () => { Undo.RecordObject(agent, "Agent Speed"); agent.speed = 3.5f; }, true, agent);
            if (go.scene.IsValid() && !EditorUtility.IsPersistent(go) && !Application.isPlaying && !IsPrefabStage(go) &&
                !NavMesh.SamplePosition(go.transform.position, out _, 2f, NavMesh.AllAreas))
                Add(issues, Severity.Warning, "Physics", "agent.navmesh",
                    "This mob is not on (or near) a baked NavMesh: bake one (a NavMeshSurface) or move the mob onto it. Dungeons and the terrain bake their own.", context: go);
        }
    }

    // ------------------------------------------------------------------ model and animation
    private static void CheckModel(GameObject go, Mob mob, List<Issue> issues)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();
        if (renderers.Length == 0)
            Add(issues, Severity.Warning, "Model", "model.none", "The mob has no mesh: it is invisible. Put your model under Model / VisualModel.", context: go);
        else if (renderers.All(r => r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null && r.GetComponent<MeshFilter>().sharedMesh.name == "Capsule"))
            Add(issues, Severity.Tip, "Model", "model.placeholder", "The model is still the placeholder capsule: replace it with your mob's model (under Model / VisualModel).", context: go);

        Animator anim = MobMovementStateMachine.FindAnimator(go.transform);
        if (anim == null)
        {
            Add(issues, Severity.Warning, "Animation", "anim.none", "No Animator: the mob slides around frozen.",
                "Add", () => MobSetupUtility.SetupMob(go, false, false), !EditorUtility.IsPersistent(go), go);
            return;
        }
        if (anim.applyRootMotion)
            Add(issues, Severity.Warning, "Animation", "anim.rootMotion",
                "Apply Root Motion is on: the animation moves the mob too, fighting the NavMeshAgent (drifting, rubber-banding).",
                "Turn Off", () => { Undo.RecordObject(anim, "Root Motion"); anim.applyRootMotion = false; }, true, anim);
        if (anim.runtimeAnimatorController == null)
        {
            Add(issues, Severity.Warning, "Animation", "anim.controller",
                "The Animator has no Animator Controller: the mob does not animate. Assign one (Speed / IsMoving parameters, Hit / Die triggers, or Idle / Moving / Death states).",
                context: anim);
            return;
        }
        if (anim.avatar == null && anim.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
            Add(issues, Severity.Tip, "Animation", "anim.avatar", "The Animator has no Avatar: humanoid clips do not play (generic clips made for this rig still do).", context: anim);

        AnimatorController controller = ControllerOf(anim.runtimeAnimatorController);
        if (controller == null)
            return;
        var parameters = new HashSet<string>(controller.parameters.Select(p => p.name));
        var states = new HashSet<string>();
        if (controller.layers.Length > 0)
            CollectStates(controller.layers[0].stateMachine, states);
        MobProfile p = mob.ProfileAsset;
        string speed = p != null ? p.speedParameter : "Speed", moving = p != null ? p.movingBool : "IsMoving";
        string hit = p != null ? p.hitTrigger : "Hit", die = p != null ? p.deathTrigger : "Die";
        bool movementParams = Has(parameters, speed) || Has(parameters, moving) || Has(parameters, p != null ? p.normalizedSpeedParameter : "SpeedPercent");
        bool movementStates = states.Contains("Moving") || states.Contains("Chasing") || states.Contains("Patrol");
        if (!movementParams && !movementStates)
            Add(issues, Severity.Warning, "Animation", "anim.movement",
                $"'{controller.name}' has none of the movement parameters the AI sets ({Join(speed, moving)}) and no Moving / Chasing / Patrol state: the mob plays one animation whatever it does.",
                context: controller);
        bool deathState = states.Contains("Death") || states.Contains("Die");
        if (!Has(parameters, die) && !deathState)
            Add(issues, Severity.Tip, "Animation", "anim.death",
                $"No '{die}' trigger and no Death / Die state: the mob does not play a death animation.", context: controller);
        if (!Has(parameters, hit))
            Add(issues, Severity.Tip, "Animation", "anim.hit", $"No '{hit}' trigger: the mob does not flinch when hit.", context: controller);
        // Parameters with the right name but the wrong type are ignored by the driver.
        foreach (AnimatorControllerParameter param in controller.parameters)
        {
            if (param.name == speed && param.type != AnimatorControllerParameterType.Float)
                Add(issues, Severity.Warning, "Animation", "anim.type.speed", $"'{speed}' is a {param.type}: the AI sets it as a Float.", context: controller);
            else if (param.name == moving && param.type != AnimatorControllerParameterType.Bool)
                Add(issues, Severity.Warning, "Animation", "anim.type.moving", $"'{moving}' is a {param.type}: the AI sets it as a Bool.", context: controller);
            else if ((param.name == hit || param.name == die) && param.type != AnimatorControllerParameterType.Trigger)
                Add(issues, Severity.Warning, "Animation", "anim.type." + param.name, $"'{param.name}' is a {param.type}: the AI fires it as a Trigger.", context: controller);
        }
    }

    // ------------------------------------------------------------------ profile, patrol, attacks
    private static void CheckProfile(Mob mob, List<Issue> issues)
    {
        MobProfile profile = mob.ProfileAsset;
        if (profile == null)
        {
            Add(issues, Severity.Tip, "Profile", "profile.none",
                "No Mob Profile: the behaviour comes from the old fields on the mob. A profile asset (shared by the type) is easier to tune.",
                "Create Profile", () => MobSetupUtility.EnsureProfile(mob), true, mob);
            return;
        }
        var e = new List<string>();
        var w = new List<string>();
        profile.Validate(e, w);
        for (int i = 0; i < e.Count; i++) Add(issues, Severity.Error, "Profile", "profile.e" + i, "Profile: " + e[i], context: profile);
        for (int i = 0; i < w.Count; i++) Add(issues, Severity.Warning, "Profile", "profile.w" + i, "Profile: " + w[i], context: profile);
        List<string> preys = mob.PreysReference;
        if (profile.aggression == MobAggression.Aggressive && (preys == null || preys.Count == 0))
            Add(issues, Severity.Warning, "Profile", "profile.noPreys", "The mob is Aggressive but Preys is empty: it only fights back when attacked.",
                "+ Player", () => AddToList(mob, "Preys", "Player"), false, mob);
    }

    private static void CheckPatrol(Mob mob, List<Issue> issues)
    {
        Transform route = mob.PatrolRoute;
        if (route != null)
        {
            if (route.IsChildOf(mob.transform))
                Add(issues, Severity.Error, "Patrol", "patrol.child",
                    "Patrol Route is a child of the mob: it moves with the mob, so the mob never reaches a point. Put the route outside the mob.", context: route);
            else if (route.childCount == 0)
                Add(issues, Severity.Warning, "Patrol", "patrol.empty", "Patrol Route has no children: each child is one patrol point.", context: route);
            if (EditorUtility.IsPersistent(mob) && !EditorUtility.IsPersistent(route))
                Add(issues, Severity.Warning, "Patrol", "patrol.sceneRoute", "A prefab cannot keep a scene object as its Patrol Route.", context: mob);
        }
        else if (mob.PatrolPoints != null && mob.PatrolPoints.Length > 0 && EditorUtility.IsPersistent(mob) && !mob.PatrolPointsRelativeToHome)
            Add(issues, Severity.Warning, "Patrol", "patrol.absolute",
                "Absolute patrol points on a prefab: every spawned copy walks to the same world positions.", "Make Relative", () => ModifyMob(mob, "Relative Patrol Points", so =>
                {
                    SerializedProperty pts = so.FindProperty("patrolPoints");
                    Vector3 home = mob.transform.position;
                    for (int i = 0; i < pts.arraySize; i++)
                        pts.GetArrayElementAtIndex(i).vector3Value -= home;
                    so.FindProperty("patrolPointsRelativeToHome").boolValue = true;
                }), true, mob);
    }

    private static void CheckAttacks(GameObject go, Mob mob, List<Issue> issues)
    {
        MobAbilityController abilities = go.GetComponent<MobAbilityController>();
        if (abilities == null)
            return;
        var e = new List<string>();
        var w = new List<string>();
        abilities.Validate(e, w);
        for (int i = 0; i < e.Count; i++) Add(issues, Severity.Error, "Attacks", "abilities.e" + i, "Abilities: " + e[i], context: abilities);
        for (int i = 0; i < w.Count; i++) Add(issues, Severity.Warning, "Attacks", "abilities.w" + i, "Abilities: " + w[i], context: abilities);

        bool anySlot = abilities.Slots.Any(s => s != null && s.ability != null);
        bool anyLegacy = abilities.LegacyAbilities.Any(h => h != null && h.abilityEffect != null);
        MobProfile profile = mob.ProfileAsset;
        bool passive = profile != null && profile.aggression == MobAggression.Passive;
        if (!anySlot && !anyLegacy)
        {
            if (mob.BiteDamage <= 0 && !passive)
                Add(issues, Severity.Warning, "Attacks", "attack.none",
                    "No ability and Bite Damage 0: the mob cannot hurt anything. Add an ability (MobAbilityController ▸ Add Ability From Preset) or set Bite Damage.", context: abilities);
            else if (mob.BiteDamage > 0)
                Add(issues, Severity.Tip, "Attacks", "attack.hidden",
                    "The mob attacks with the automatic bite built at runtime: save it as a '<Type> Basic Attack' asset to see and edit it.",
                    "Create Asset", () => MobSetupUtility.EnsureBasicAttack(abilities, mob), true, abilities);
        }
    }

    // ------------------------------------------------------------------ health and speed
    private static void CheckStatus(GameObject go, List<Issue> issues)
    {
        HealthManager health = go.GetComponentInChildren<HealthManager>(true);
        if (health != null)
        {
            var so = new SerializedObject(health);
            SerializedProperty max = so.FindProperty("maxValue"), cur = so.FindProperty("currentValue");
            if (max != null && max.floatValue <= 0f)
                Add(issues, Severity.Error, "Status", "health.max", "Max health is 0: the mob dies as soon as it spawns.",
                    "Set 100", () => { var s = new SerializedObject(health); s.FindProperty("maxValue").floatValue = 100f; s.FindProperty("currentValue").floatValue = 100f; s.ApplyModifiedProperties(); }, true, health);
            else if (max != null && cur != null && cur.floatValue > max.floatValue)
                Add(issues, Severity.Tip, "Status", "health.current", $"Current health ({cur.floatValue:0}) is above max ({max.floatValue:0}).",
                    "Clamp", () => { var s = new SerializedObject(health); s.FindProperty("currentValue").floatValue = s.FindProperty("maxValue").floatValue; s.ApplyModifiedProperties(); }, true, health);
        }
        SpeedManager speed = go.GetComponentInChildren<SpeedManager>(true);
        if (speed != null)
        {
            var so = new SerializedObject(speed);
            SerializedProperty baseSpeed = so.FindProperty("baseSpeed");
            if (baseSpeed != null && baseSpeed.floatValue <= 0f)
                Add(issues, Severity.Warning, "Status", "speed.base", "The Speed Manager's Base Speed is 0: the mob stands still.",
                    "Set 3.5", () => { var s = new SerializedObject(speed); s.FindProperty("baseSpeed").floatValue = 3.5f; s.ApplyModifiedProperties(); }, true, speed);
        }
    }

    // ------------------------------------------------------------------ drops
    private static void CheckDrops(GameObject go, List<Issue> issues)
    {
        ItemDropOnDeath own = go.GetComponentInChildren<ItemDropOnDeath>(true) ?? go.GetComponentInParent<ItemDropOnDeath>();
        ItemSpawner spawner = go.GetComponentInChildren<ItemSpawner>(true) ?? go.GetComponentInParent<ItemSpawner>();
        if (own != null && spawner != null && spawner.HasDrops)
            Add(issues, Severity.Warning, "Drops", "drops.double",
                "Both an Item Drop On Death and an Item Spawner with drops: the mob drops twice. Keep one.", context: own);
        ItemDropTable table = own != null ? own.Table : null;
        ItemDropManager m = ItemDropManager.Instance;
        if (table == null && !(spawner != null && spawner.HasDrops))
        {
            if (m.mobTable == null && m.eliteTable == null && m.bossTable == null)
                Add(issues, Severity.Tip, "Drops", "drops.none",
                    "The mob drops nothing: give it an Item Drop On Death with a table, or set the Item Drop Manager's Mob table.",
                    "Add Drop On Death", () => Undo.AddComponent<ItemDropOnDeath>(go), false, go);
            else if (!m.dropFromEveryMob && own == null)
                Add(issues, Severity.Tip, "Drops", "drops.off",
                    "The Item Drop Manager has Drop From Every Mob off and this mob has no Item Drop On Death: it drops nothing.", context: go);
        }
        if (table != null)
        {
            var e = new List<string>();
            var w = new List<string>();
            table.Validate(e, w);
            for (int i = 0; i < e.Count; i++) Add(issues, Severity.Warning, "Drops", "drops.e" + i, $"Drop table '{table.Name}': {e[i]}", context: table);
        }
    }

    // ------------------------------------------------------------------ network
    private static Type networkObjectType;
    private static bool networkSearched;

    private static void CheckNetwork(GameObject go, List<Issue> issues)
    {
        if (!networkSearched)
        {
            networkSearched = true;
            networkObjectType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("FishNet.Object.NetworkObject", false)).FirstOrDefault(t => t != null);
        }
        if (networkObjectType == null || !EditorUtility.IsPersistent(go) && PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.NotAPrefab)
            return;
        if (go.transform.root.GetComponent(networkObjectType) == null)
            Add(issues, Severity.Tip, "Network", "net.object",
                "Not networked (no NetworkObject): in multiplayer only the server sees this mob. Tools ▸ SimpleMovements ▸ Network ▸ Make All Mob Prefabs Networked.",
                context: go);
    }

    /// <summary>Applies the fix of problem <paramref name="id"/> inside a prefab file (its contents are loaded, fixed and saved).</summary>
    public static bool FixInPrefab(string prefabPath, string id)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            foreach (Mob m in root.GetComponentsInChildren<Mob>(true))
            {
                Issue same = Check(m.gameObject).FirstOrDefault(i => i.id == id && i.fix != null);
                if (same == null) continue;
                same.fix();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return true;
            }
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------------------------------ fixes and helpers
    private static void ModifyMob(Mob mob, string undo, Action<SerializedObject> change)
    {
        var so = new SerializedObject(mob);
        so.Update();
        change(so);
        so.ApplyModifiedProperties();
    }

    private static void AddToList(Mob mob, string field, string value) => ModifyMob(mob, "Add " + value, so =>
    {
        SerializedProperty list = so.FindProperty(field);
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).stringValue == value) return;
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = value;
    });

    private static void RemoveFromList(Mob mob, string field, string value) => ModifyMob(mob, "Remove " + value, so =>
    {
        SerializedProperty list = so.FindProperty(field);
        for (int i = list.arraySize - 1; i >= 0; i--)
            if (list.GetArrayElementAtIndex(i).stringValue == value)
            {
                list.DeleteArrayElementAtIndex(i);
                return;
            }
    });

    private static void SetIfEmpty(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null && p.objectReferenceValue == null && value != null) p.objectReferenceValue = value;
    }

    private static void MakeKinematic(Rigidbody rb)
    {
        Undo.RecordObject(rb, "Kinematic Rigidbody");
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private static void MatchAgent(NavMeshAgent agent, CapsuleCollider capsule)
    {
        Undo.RecordObject(agent, "Match Agent Size");
        agent.radius = capsule.radius;
        agent.height = capsule.height;
    }

    /// <summary>Sizes (or adds, with <paramref name="add"/>) the root capsule to the model's meshes, and the agent with it.</summary>
    public static void FitCollider(GameObject go, bool add)
    {
        CapsuleCollider c = go.GetComponent<CapsuleCollider>();
        if (c == null)
        {
            if (!add) return;
            c = Undo.AddComponent<CapsuleCollider>(go);
        }
        else
            Undo.RecordObject(c, "Fit Collider");
        Bounds? b = ModelBounds(go);
        float k = Mathf.Max(1e-4f, Mathf.Abs(go.transform.lossyScale.y));
        if (b.HasValue)
        {
            Vector3 size = b.Value.size / k;
            Vector3 centre = go.transform.InverseTransformPoint(b.Value.center);
            c.height = Mathf.Max(0.2f, size.y);
            // The narrower side: arms in a T-pose or a long body do not make the capsule too fat.
            c.radius = Mathf.Clamp(Mathf.Min(size.x, size.z) * 0.5f, 0.1f, c.height * 0.5f);
            c.center = new Vector3(0f, centre.y, 0f);
        }
        else
        {
            c.radius = 0.5f;
            c.height = 2f;
            c.center = new Vector3(0f, 1f, 0f);
        }
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (agent != null)
            MatchAgent(agent, c);
    }

    /// <summary>The model's meshes in world space (null when there are none).</summary>
    public static Bounds? ModelBounds(GameObject go)
    {
        Bounds? b = null;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            Bounds rb = r is SkinnedMeshRenderer smr && smr.sharedMesh != null
                ? TransformBounds(smr.transform, smr.sharedMesh.bounds) : r.bounds;
            if (rb.size.sqrMagnitude < 1e-6f) continue;
            if (b == null) b = rb;
            else { Bounds x = b.Value; x.Encapsulate(rb); b = x; }
        }
        return b;
    }

    private static Bounds TransformBounds(Transform t, Bounds local)
    {
        var b = new Bounds(t.TransformPoint(local.center), Vector3.zero);
        Vector3 e = local.extents;
        for (int i = 0; i < 8; i++)
            b.Encapsulate(t.TransformPoint(local.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
        return b;
    }

    private static void SetLayer(GameObject go, int layer)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
        {
            Undo.RecordObject(c.gameObject, "Mob Layer");
            c.gameObject.layer = layer;
        }
        Undo.RecordObject(go, "Mob Layer");
        go.layer = layer;
    }

    private static int FirstLayer(int mask)
    {
        for (int i = 0; i < 32; i++)
            if ((mask & (1 << i)) != 0 && !string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;
        return -1;
    }

    private static AnimatorController ControllerOf(RuntimeAnimatorController rac)
    {
        while (rac is AnimatorOverrideController o) rac = o.runtimeAnimatorController;
        return rac as AnimatorController;
    }

    private static void CollectStates(AnimatorStateMachine sm, HashSet<string> into)
    {
        if (sm == null) return;
        foreach (ChildAnimatorState s in sm.states) if (s.state != null) into.Add(s.state.name);
        foreach (ChildAnimatorStateMachine c in sm.stateMachines) CollectStates(c.stateMachine, into);
    }

    private static bool Has(HashSet<string> set, string name) => !string.IsNullOrEmpty(name) && set.Contains(name);

    private static string Join(params string[] names) => string.Join(", ", names.Where(n => !string.IsNullOrEmpty(n)));

    private static bool IsPrefabStage(GameObject go) =>
        UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(go) != null;

    public static string CleanName(string n)
    {
        int i = n.IndexOf('(');
        if (i > 0) n = n.Substring(0, i);
        return n.Trim();
    }
}

/// <summary>The mob checks in the Project Validator (Window ▸ SimpleMovements ▸ Project Validator, checks: Mobs).</summary>
[InitializeOnLoad]
internal static class MobProjectValidation
{
    static MobProjectValidation()
    {
        SimpleMovements.Validation.ProjectValidator.ValidateRootWithChecks += Validate;
    }

    private static void Validate(GameObject root, SimpleMovements.Validation.ValidationChecks checks, List<SimpleMovements.Validation.ValidationIssue> issues)
    {
        if ((checks & SimpleMovements.Validation.ValidationChecks.Mobs) == 0 || root == null)
            return;
        string prefabPath = EditorUtility.IsPersistent(root) ? AssetDatabase.GetAssetPath(root) : null;
        foreach (Mob mob in root.GetComponentsInChildren<Mob>(true))
        {
            string where = (prefabPath != null ? prefabPath + " ▸ " : "") + mob.name;
            foreach (MobSetupValidator.Issue i in MobSetupValidator.Check(mob.gameObject))
            {
                var severity = i.severity == MobSetupValidator.Severity.Error ? SimpleMovements.Validation.IssueSeverity.Error
                    : i.severity == MobSetupValidator.Severity.Warning ? SimpleMovements.Validation.IssueSeverity.Warning
                    : SimpleMovements.Validation.IssueSeverity.Info;
                var issue = new SimpleMovements.Validation.ValidationIssue(severity, "Mob " + i.area, i.message, i.context != null ? i.context : mob.gameObject, where);
                if (i.fix != null)
                {
                    MobSetupValidator.Issue captured = i;
                    string path = prefabPath;
                    issue.WithFix(i.fixLabel ?? "Fix", () =>
                    {
                        if (path != null) return MobSetupValidator.FixInPrefab(path, captured.id);
                        captured.fix();
                        return true;
                    });
                }
                issues.Add(issue);
            }
        }
    }
}
#endif
