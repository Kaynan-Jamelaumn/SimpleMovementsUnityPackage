#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Importance = PlayerSetupValidator.Importance;
using Check = PlayerSetupValidator.Check;

/// <summary>
/// Player Setup Validator inspector: every script the player needs with its status and an Add button, Add All,
/// the setup buttons of the other components, reference checks (hands, animator, layer) and the project / scene
/// setup (Combat Settings, Trait Database, EventSystem, pause menu, character creation).
/// </summary>
[CustomEditor(typeof(PlayerSetupValidator))]
public class PlayerSetupValidatorEditor : Editor
{
    private static readonly Dictionary<Importance, bool> open = new Dictionary<Importance, bool>
    {
        { Importance.Required, true }, { Importance.Recommended, true }, { Importance.Optional, false }, { Importance.Automatic, false },
    };
    private static bool showReferences = true, showProject = true, showHud = true, showSurvival = true;
    private static GUIStyle ok, bad, warn, muted;

    private static void Styles()
    {
        if (ok != null) return;
        ok = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.35f, 0.8f, 0.4f) } };
        bad = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.95f, 0.4f, 0.35f) } };
        warn = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.95f, 0.75f, 0.3f) } };
        muted = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
    }

    public override void OnInspectorGUI()
    {
        Styles();
        var v = (PlayerSetupValidator)target;
        GameObject go = v.gameObject;

        int requiredMissing = PlayerSetupValidator.Missing(go, Importance.Required).Count;
        int recommendedMissing = PlayerSetupValidator.Missing(go, Importance.Recommended).Count;
        if (requiredMissing == 0 && recommendedMissing == 0)
            EditorGUILayout.HelpBox("Every required and recommended script is on the player.", MessageType.Info);
        else
            EditorGUILayout.HelpBox($"{requiredMissing} required and {recommendedMissing} recommended script(s) missing. Use Add, or Add All below.",
                requiredMissing > 0 ? MessageType.Error : MessageType.Warning);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(requiredMissing == 0))
                    if (GUILayout.Button(new GUIContent("Add All Required", "Adds every missing required script (then runs the status setup wizard)."), GUILayout.Height(26f)))
                        AddAll(go, Importance.Required);
                using (new EditorGUI.DisabledScope(recommendedMissing == 0))
                    if (GUILayout.Button(new GUIContent("Add All Recommended", "Adds every missing recommended script."), GUILayout.Height(26f)))
                        AddAll(go, Importance.Recommended);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(go.GetComponent<PlayerStatusController>() == null))
                    if (GUILayout.Button(new GUIContent("Run Status Setup Wizard", "Creates and wires every status manager, the experience and trait managers and the dash / roll models (same as the Player Status Controller's Complete Setup Wizard).")))
                        RunStatusWizard(go);
                if (GUILayout.Button(new GUIContent("Auto-assign References", "Runs Auto-assign References on the movement and ability components.")))
                    AutoAssign(go);
            }
        }

        EditorGUILayout.Space();
        foreach (Importance imp in (Importance[])Enum.GetValues(typeof(Importance)))
            DrawGroup(go, imp);

        EditorGUILayout.Space();
        showReferences = EditorGUILayout.Foldout(showReferences, "References", true);
        if (showReferences)
            DrawReferences(go);

        showHud = EditorGUILayout.Foldout(showHud, "Player HUD", true);
        if (showHud)
            DrawHud(go);

        showSurvival = EditorGUILayout.Foldout(showSurvival, "Survival, Death & Respawn", true);
        if (showSurvival)
            DrawSurvival(go);

        showProject = EditorGUILayout.Foldout(showProject, "Project & Scene", true);
        if (showProject)
            DrawProject(go);

        EditorGUILayout.Space();
        DrawDefaultInspector();
    }

    // ------------------------------------------------------------------ scripts
    private void DrawGroup(GameObject go, Importance imp)
    {
        int missing = PlayerSetupValidator.Missing(go, imp).Count;
        string title = imp switch
        {
            Importance.Required => "Required",
            Importance.Recommended => "Recommended",
            Importance.Optional => "Optional",
            _ => "Added automatically at runtime",
        };
        open[imp] = EditorGUILayout.Foldout(open[imp], imp == Importance.Automatic ? title : $"{title} ({(missing == 0 ? "all present" : missing + " missing")})", true);
        if (!open[imp])
            return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        foreach (Check c in PlayerSetupValidator.Checks)
        {
            if (c.importance != imp)
                continue;
            bool has = PlayerSetupValidator.Has(go, c);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUIStyle style = has ? ok : imp == Importance.Required ? bad : imp == Importance.Automatic ? muted : warn;
                GUILayout.Label(has ? "✔" : imp == Importance.Automatic ? "·" : "✖", style, GUILayout.Width(16f));
                EditorGUILayout.LabelField(new GUIContent(ObjectNames.NicifyVariableName(c.Name), c.purpose), GUILayout.MinWidth(120f));
                if (!has)
                {
                    using (new EditorGUI.DisabledScope(Application.isPlaying))
                        if (GUILayout.Button(new GUIContent("Add", "Adds " + c.Name + " to the player."), GUILayout.Width(52f)))
                            Add(go, c);
                }
                else if (GUILayout.Button(new GUIContent("Select", "Selects the component's object."), GUILayout.Width(52f)))
                {
                    Component comp = go.GetComponent(c.type);
                    if (comp == null) comp = go.GetComponentInChildren(c.type, true); // Unity null: no ??
                    if (comp != null) EditorGUIUtility.PingObject(comp);
                }
            }
            EditorGUILayout.LabelField(c.purpose, muted);
        }
        EditorGUILayout.EndVertical();
    }

    private static void Add(GameObject go, Check c)
    {
        if (c.byStatusWizard && go.GetComponent<PlayerStatusController>() != null)
        {
            RunStatusWizard(go);
            if (PlayerSetupValidator.Has(go, c))
                return;
        }
        Undo.AddComponent(go, c.type);
        if (c.type == typeof(PlayerStatusController))
            RunStatusWizard(go);
        MarkDirty(go);
    }

    private static void AddAll(GameObject go, Importance imp)
    {
        Undo.SetCurrentGroupName("Add Player Scripts");
        int group = Undo.GetCurrentGroup();
        foreach (Check c in PlayerSetupValidator.Missing(go, imp))
            if (!c.byStatusWizard)
                Undo.AddComponent(go, c.type);
        if (go.GetComponent<PlayerStatusController>() != null && PlayerSetupValidator.Missing(go, imp).Exists(c => c.byStatusWizard))
            RunStatusWizard(go);
        AutoAssign(go);
        Undo.CollapseUndoOperations(group);
        MarkDirty(go);
    }

    private static void RunStatusWizard(GameObject go)
    {
        var status = go.GetComponent<PlayerStatusController>();
        if (status == null)
            return;
        var helper = new PlayerStatusSetupHelper(status);
        Undo.RegisterFullObjectHierarchyUndo(go, "Status Setup Wizard");
        helper.CreateMissingComponents();
        helper.AutoAssignComponents();
        EditorUtility.SetDirty(status);
        MarkDirty(go);
    }

    private static void AutoAssign(GameObject go)
    {
        Undo.RegisterFullObjectHierarchyUndo(go, "Auto-assign References");
        go.GetComponent<MovementStateMachine>()?.AutoAssignReferences();
        go.GetComponent<PlayerMovementController>()?.AutoAssignReferences();
        go.GetComponentInChildren<AbilitiesStateMachine>(true)?.AutoAssignReferences();
        MarkDirty(go);
    }

    // ------------------------------------------------------------------ references
    private void DrawReferences(GameObject go)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        var animator = go.GetComponentInChildren<Animator>(true);
        Row(animator != null, "Animator on the model", animator != null ? (animator.isHuman ? "Humanoid" : "Generic rig") : "Add the model with its Animator as a child.", null, null);

        var weapon = go.GetComponent<WeaponController>();
        if (weapon != null)
        {
            Row(weapon.handGameObject != null, "Weapon Controller ▸ Hand", weapon.handGameObject != null ? weapon.handGameObject.name : "Empty: weapons have no hand.",
                animator != null && animator.isHuman ? "Use Right Hand" : null, () =>
                {
                    Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    if (hand == null) return;
                    Undo.RecordObject(weapon, "Assign Hand");
                    weapon.handGameObject = hand.gameObject;
                    EditorUtility.SetDirty(weapon);
                });
        }
        var inventory = go.GetComponent<InventoryManager>();
        if (inventory != null)
        {
            Row(inventory.HandParent != null, "Inventory Manager ▸ Hand Parent", inventory.HandParent != null ? inventory.HandParent.name : "Empty: held items have nowhere to appear.",
                weapon != null && weapon.handGameObject != null ? "Same As Weapon Hand" : null, () =>
                {
                    var so = new SerializedObject(inventory);
                    so.FindProperty("handParent").objectReferenceValue = weapon.handGameObject.transform;
                    so.ApplyModifiedProperties();
                });
        }

        CombatSettings settings = SMAssets.Load<CombatSettings>(SMAssets.Settings, "CombatSettings");
        if (settings != null)
        {
            bool inLayers = (settings.characterLayers.value & (1 << go.layer)) != 0;
            Row(inLayers, "Layer in Combat Settings ▸ Character Layers", $"Layer '{LayerMask.LayerToName(go.layer)}'" + (inLayers ? "" : " is not included: nothing can hit the player."),
                inLayers ? null : "Add Layer", () =>
                {
                    Undo.RecordObject(settings, "Add Character Layer");
                    settings.characterLayers |= 1 << go.layer;
                    EditorUtility.SetDirty(settings);
                });
        }

        var bodyParts = go.GetComponentInChildren<BodyPartController>(true);
        if (bodyParts != null)
        {
            int hitboxes = go.GetComponentsInChildren<BodyPartHitbox>(true).Length;
            Row(hitboxes > 0, "Body part hitboxes", hitboxes > 0 ? $"{hitboxes} hitboxes" : "None: hits are located by height. Body Part Controller ▸ Add Hitboxes (Humanoid).",
                hitboxes > 0 ? null : "Select Controller", () => Selection.activeObject = bodyParts);
        }
        EditorGUILayout.EndVertical();
    }

    // ------------------------------------------------------------------ HUD
    private void DrawHud(GameObject go)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        PlayerHUD hud = go.GetComponentInChildren<PlayerHUD>(true);
        Row(hud != null, "Player HUD", hud != null ? "On '" + hud.gameObject.name + "'." : "Added by the Inventory Manager when the game starts; add it now to build and customise the UI.",
            hud != null ? null : "Add Player HUD", () => { PlayerHUD h = PlayerHUDMenu.HudOf(go, true); if (h != null) Selection.activeObject = h; });
        if (hud != null)
        {
            Row(hud.statusUI != null, "Status UI", hud.statusUI != null ? "Built: select it in the hierarchy to move or restyle it." : "Not built: generated when the game starts.",
                hud.statusUI != null ? null : "Build Status UI", () => PlayerHUDEditor.Run(hud, "Build Status UI", h => h.BuildStatusUI()));
            Row(hud.abilityUI != null, "Abilities UI", hud.abilityUI != null ? "Built: select it in the hierarchy to move or restyle it." : "Not built: generated when the game starts.",
                hud.abilityUI != null ? null : "Build Abilities UI", () => PlayerHUDEditor.Run(hud, "Build Abilities UI", h => h.BuildAbilityUI()));
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                if (GUILayout.Button("Open Player HUD Settings"))
                    Selection.activeObject = hud;
        }
        EditorGUILayout.EndVertical();
    }

    // ------------------------------------------------------------------ survival
    private void DrawSurvival(GameObject go)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        Unconsciousness downed = go.GetComponentInChildren<Unconsciousness>(true);
        DownedRules r = downed != null ? downed.Rules : CombatSettings.Instance.downedRules;
        Row(downed != null, "Unconsciousness", downed != null
                ? $"At 0 health: unconscious for {r.unconsciousSeconds:0} s (+ Unconscious Time stat), dies below -{r.deathThreshold:0} (+ Death Threshold stat). Rules: Combat Settings ▸ Downed Rules or the component's own."
                : "Missing: the player dies at 0 health at once.",
            downed != null ? null : "Add Unconsciousness", () => Undo.AddComponent<Unconsciousness>(go));
        DeathPenalty penalty = go.GetComponentInChildren<DeathPenalty>(true);
        Row(penalty != null, "Death Penalty", penalty != null ? $"Mode: {ObjectNames.NicifyVariableName(penalty.Mode.ToString())}. Needs refilled on respawn by its Status Rules."
                : "Missing: no penalty, no offline respawn (needs are still refilled on revive with the default rules).",
            penalty != null ? null : "Add Death Penalty", () => Undo.AddComponent<DeathPenalty>(go));
        SleepController sleepInput = go.GetComponentInChildren<SleepController>(true);
        Row(sleepInput != null, "Sleep input", sleepInput != null ? "Press Sleep (Z by default) to sleep on the ground; beds and benches: a Sleep Spot + Interact."
                : "Missing: the player can only sleep at Sleep Spots.",
            sleepInput != null ? null : "Add Sleep Controller", () => Undo.AddComponent<SleepController>(go));
        int spots = UnityEngine.Object.FindObjectsByType<SleepSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        Row(spots > 0, "Sleep spots in the scene", spots > 0 ? $"{spots} (beds, benches, bedrolls)." : "None: add a Sleep Spot to a bed (Add Component ▸ SimpleMovements ▸ Sleep Spot).", null, null);
        BodyHeatManager heat = go.GetComponentInChildren<BodyHeatManager>(true);
        if (heat != null)
        {
            TemperatureProfile prof = Application.isPlaying ? heat.Profile : null;
            Row(true, "Body temperature", (prof != null ? "Race / class profile: " + prof.Describe() : $"Ideal {heat.IdealCelsius:0} °C ± {heat.ToleranceCelsius:0} °C (a race / class Temperature profile replaces it)") +
                ". Heat / Cold Resistance stats (100 = immune).", null, null);
        }
        AbsorptionMilestones m = AbsorptionMilestones.For(go.transform);
        Row(true, "Absorption milestones", m != null ? m.Describe() : "No level limit", null, null);
        EditorGUILayout.EndVertical();
    }

    // ------------------------------------------------------------------ project and scene
    private void DrawProject(GameObject go)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool combat = SMAssets.Load<CombatSettings>(SMAssets.Settings, "CombatSettings") != null;
        Row(combat, "Combat Settings (Resources)", combat ? "Found." : "Defaults are used; layers, PvP, body parts and threat cannot be changed.",
            combat ? null : "Create", () => EditorApplication.ExecuteMenuItem("Tools/SimpleMovements/Project Setup/Create Combat Settings (Resources)"));
        if (go.GetComponent<TraitManager>() != null)
        {
            bool traits = SMAssets.Load<TraitDatabase>(SMAssets.Databases, "TraitDatabase") != null;
            Row(traits, "Trait Database (Resources)", traits ? "Found." : "Needed for traits at character creation.",
                traits ? null : "Create", () => EditorApplication.ExecuteMenuItem("Tools/SimpleMovements/Project Setup/Create Trait Database (Resources)"));
        }

        bool inScene = !EditorUtility.IsPersistent(go) && PrefabStageUtility.GetCurrentPrefabStage() == null;
        if (!inScene)
        {
            EditorGUILayout.LabelField("Open a scene with this player to check the scene setup (EventSystem, pause menu, character creation).", muted);
            EditorGUILayout.EndVertical();
            return;
        }
        bool eventSystem = UIBuildKit.HasEventSystem();
        Row(eventSystem, "EventSystem in the scene", eventSystem ? "Found." : "UI cannot be clicked without one.",
            eventSystem ? null : "Create", UIBuildKit.EnsureEventSystem);
        bool pause = UnityEngine.Object.FindAnyObjectByType<PauseMenuManager>() != null;
        Row(pause, "Pause & settings menu", pause ? "Found: Escape opens it in game." : "No pause menu: Escape does nothing.",
            pause ? null : "Build", () => GameMenuBuilder.Build());
        bool creation = UnityEngine.Object.FindAnyObjectByType<CharacterCreationUI>() != null;
        Row(creation, "Character creation screen (optional)", creation ? "Found." : "Only needed when players create their character in this scene.",
            creation ? null : "Build", () => CharacterCreationSceneBuilder.Build(null));
        bool camera = UnityEngine.Object.FindAnyObjectByType<Camera>() != null;
        Row(camera, "A camera", camera ? "Found." : "Add a camera (with a CinemachineBrain for the player's Cinemachine cameras).", null, null);
        EditorGUILayout.EndVertical();
    }

    private static void Row(bool good, string label, string detail, string fixLabel, Action fix)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(good ? "✔" : "✖", good ? ok : warn, GUILayout.Width(16f));
            EditorGUILayout.LabelField(new GUIContent(label, detail), GUILayout.MinWidth(120f));
            if (!good && fix != null && fixLabel != null)
            {
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                    if (GUILayout.Button(fixLabel, GUILayout.Width(150f)))
                        fix();
            }
        }
        EditorGUILayout.LabelField(detail, muted);
    }

    private static void MarkDirty(GameObject go)
    {
        EditorUtility.SetDirty(go);
        if (!EditorUtility.IsPersistent(go) && go.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(go.scene);
    }
}

/// <summary>Menu: adds the validator to the selected player.</summary>
public static class PlayerSetupValidatorMenu
{
    [MenuItem(SMMenu.Root + "Player/Add Setup Validator To Selection", false, SMMenu.Player + 0)]
    private static void AddToSelection()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Player Setup Validator", "Select the player (in the scene or its prefab) first.", "OK");
            return;
        }
        PlayerSetupValidator v = go.GetComponent<PlayerSetupValidator>();
        if (v == null)
            v = Undo.AddComponent<PlayerSetupValidator>(go);
        Selection.activeObject = v;
    }
}
#endif
