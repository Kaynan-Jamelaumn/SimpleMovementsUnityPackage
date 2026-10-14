#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;
using Importance = PlayerSetupValidator.Importance;
using Check = PlayerSetupValidator.Check;
#if FISHNET
using FishNet.Object;
using GameNet.EditorTools;
#endif

namespace SimpleMovements.Setup
{
    /// <summary>
    /// What each kind of prefab needs: the player, a mob and an item lying in the world. Shown by the Scene &amp; Prefab
    /// Setup window; the player's also by the Player Setup Validator's inspector.
    /// </summary>
    public static class PrefabProfiles
    {
        // ================================================================== Player

        /// <summary>Where each of the player's scripts is listed.</summary>
        private static string PlayerGroup(Check c)
        {
            if (c.importance == Importance.Automatic)
                return "Added automatically at runtime";
            Type t = c.type;
            if (t == typeof(CharacterController) || t == typeof(PlayerMovementModel) || t == typeof(PlayerMovementController) || t == typeof(MovementStateMachine) ||
                t == typeof(AvailabilityStateMachine) || t == typeof(PlayerDashModel) || t == typeof(PlayerRollModel))
                return "Movement";
            if (t == typeof(PlayerStatusController) || t == typeof(HealthManager) || t == typeof(StaminaManager) || t == typeof(SpeedManager) || t == typeof(ManaManager) ||
                t == typeof(WeightManager) || t == typeof(ExperienceManager) || t == typeof(TraitManager))
                return "Status & progression";
            if (t == typeof(PlayerAnimationModel) || t == typeof(PlayerAnimationController) || t == typeof(PlayerCameraModel) || t == typeof(PlayerCameraController) ||
                t == typeof(PlayerCameraView) || t == typeof(UnityEngine.InputSystem.PlayerInput))
                return "Animation, camera & input";
            if (t == typeof(PlayerNameComponent) || t == typeof(CharacterIdentity) || t == typeof(CharacterBody))
                return "Character";
            return "Items, combat & abilities";
        }

        public static SetupProfile Player()
        {
            var p = new SetupProfile
            {
                Id = "player",
                Name = "Player Prefab",
                Kind = SetupTargetKind.Prefab,
                Summary = "The playable character: movement, status, inventory, camera, networking.",
                Description =
                    "The player character. One prefab is used for single player (placed in the City at the spawn) and for " +
                    "multiplayer (spawned by the Net Player Spawner for each client, after Make Networked).\n\n" +
                    "Required scripts make it move, take damage and die; recommended ones give it the inventory, camera, " +
                    "animation and progression; optional ones add abilities, body parts and starting items. Scripts listed " +
                    "under 'Added automatically' appear at runtime by themselves - add them only to change their settings.",
                DocPath = "Player/01-Player-Setup.md",
                FindDefault = () =>
                {
#if FISHNET
                    GameObject networked = NetworkSetupMenu.FindPlayerPrefab();
                    if (networked != null)
                        return AssetDatabase.GetAssetPath(networked);
#endif
                    return SetupUtil.FindPrefab(IsPlayer);
                },
                IsCandidate = IsPlayer,
            };

            // Every script of the Player Setup Validator's list.
            foreach (Check c in PlayerSetupValidator.Checks)
                p.Add(PlayerScript(c));

            p.Add(SetupRequirement.Make("Status & progression", "Status bars wired", SetupLevel.Required)
                .Explain("The Player Status Controller links every status manager (health, stamina, mana, hunger...), the experience and trait managers, and the dash / roll models.",
                         "Player Status Controller ▸ Complete Setup Wizard.")
                .Checks(ctx =>
                {
                    var status = ctx.Root.GetComponentInChildren<PlayerStatusController>(true);
                    if (status == null)
                        return SetupResult.Skip("Needs the Player Status Controller first.");
                    SetupValidationResult v = new PlayerStatusSetupHelper(status, false).ValidateSetup();
                    if (v.ErrorCount > 0)
                        return SetupResult.Wrong(string.Join("\n", v.Errors.Take(4)), status);
                    if (v.WarningCount > 0)
                        return SetupResult.Ok("Wired. Notes: " + string.Join(" ", v.Warnings.Take(2)), status);
                    return SetupResult.Ok("Every manager is linked.", status);
                })
                .Fixes("Run Status Setup Wizard", ctx => RunStatusWizard(ctx)));

            // ---------------------------------------------------------------- object settings
            p.Add(SetupRequirement.Make("Object settings", "Tagged 'Player'", SetupLevel.Recommended)
                .Explain("Players are recognised by their Player Status Controller, but some systems (portals, triggers, your own scripts) fall back to the Player tag.",
                         "Select the prefab root ▸ Tag: Player.")
                .Checks(ctx => ctx.Root.CompareTag("Player") ? SetupResult.Ok("Tag: Player.", ctx.Root) : SetupResult.Wrong($"Tag: {ctx.Root.tag}.", ctx.Root))
                .Fixes("Set Tag", ctx =>
                {
                    ctx.Record(ctx.Root, "Player tag");
                    ctx.Root.tag = "Player";
                    ctx.Dirty(ctx.Root);
                    return true;
                }));
            p.Add(CharacterLayer("Object settings", "the player"));
            p.Add(SetupRequirement.Make("Object settings", "Character Controller on the root, sensible shape", SetupLevel.Required)
                .Explain("The movement moves the root with its Character Controller; networking predicts that root. Height must be at least twice the radius; Skin Width about 10% of the radius.",
                         "Put the Character Controller on the prefab root; Height ≥ 2 × Radius; Skin Width ≈ Radius × 0.1.")
                .Checks(ctx =>
                {
                    var cc = ctx.Root.GetComponentInChildren<CharacterController>(true);
                    if (cc == null)
                        return SetupResult.Skip("No Character Controller yet (see Movement).");
                    if (cc.gameObject != ctx.Root)
                        return SetupResult.Wrong($"It is on '{cc.name}', not on the root.", cc);
                    if (cc.height < cc.radius * 2f)
                        return SetupResult.Wrong($"Height {cc.height:0.##} is less than twice the radius {cc.radius:0.##}: the capsule is a sphere.", cc);
                    if (cc.skinWidth < 0.01f || cc.skinWidth > cc.radius * 0.5f)
                        return SetupResult.Wrong($"Skin Width {cc.skinWidth:0.###} (radius {cc.radius:0.##}): the player jitters or floats.", cc);
                    return SetupResult.Ok($"Height {cc.height:0.##}, radius {cc.radius:0.##}.", cc);
                })
                .Fixes("Fix Skin Width / Height", ctx =>
                {
                    var cc = ctx.Root.GetComponent<CharacterController>();
                    if (cc == null)
                        return false;
                    ctx.Record(cc, "Character Controller shape");
                    cc.height = Mathf.Max(cc.height, cc.radius * 2f);
                    cc.skinWidth = Mathf.Clamp(cc.radius * 0.1f, 0.01f, cc.radius * 0.5f);
                    ctx.Dirty(cc);
                    return true;
                }));
            p.Add(SetupRequirement.Make("Object settings", "No physics Rigidbody on the player", SetupLevel.Required)
                .Explain("The Character Controller moves the player. A non-kinematic Rigidbody next to it fights the movement (sliding, falling through floors, jitter).",
                         "Remove the Rigidbody, or tick Is Kinematic.")
                .Checks(ctx =>
                {
                    Rigidbody rb = ctx.Root.GetComponentsInChildren<Rigidbody>(true).FirstOrDefault(r => !r.isKinematic && r.GetComponent<CharacterController>() != null);
                    return rb == null ? SetupResult.Ok("None.") : SetupResult.Wrong($"'{rb.name}' has a Rigidbody that is not kinematic.", rb);
                })
                .Fixes("Make It Kinematic", ctx =>
                {
                    bool changed = false;
                    foreach (Rigidbody rb in ctx.Root.GetComponentsInChildren<Rigidbody>(true).Where(r => !r.isKinematic && r.GetComponent<CharacterController>() != null))
                    {
                        ctx.Record(rb, "Kinematic");
                        rb.isKinematic = true;
                        ctx.Dirty(rb);
                        changed = true;
                    }
                    return changed;
                }));
            p.Add(SetupRequirement.Make("Object settings", "No duplicated core scripts", SetupLevel.Required)
                .Explain("Two Character Controllers, status controllers, inventories or movement state machines in one player run twice and fight each other.",
                         "Delete the extra copies (usually one on the root and one on a child).")
                .Checks(ctx =>
                {
                    var doubled = new List<string>();
                    foreach (Type t in new[] { typeof(CharacterController), typeof(PlayerStatusController), typeof(InventoryManager), typeof(MovementStateMachine), typeof(PlayerMovementController), typeof(WeaponController) })
                    {
                        int n = ctx.Root.GetComponentsInChildren(t, true).Length;
                        if (n > 1)
                            doubled.Add($"{t.Name} ×{n}");
                    }
                    return doubled.Count == 0 ? SetupResult.Ok("Each one once.") : SetupResult.Wrong("Twice or more: " + string.Join(", ", doubled) + ".");
                }));

            // ---------------------------------------------------------------- references
            p.Add(SetupRequirement.Make("References", "Animator with a controller", SetupLevel.Recommended)
                .Explain("The model's Animator plays the movement, attack and hit animations. Without an Animator Controller the character stays in its bind pose.",
                         "Add your model with its Animator as a child; Player ▸ Complete Player Animator Controller adds the parameters the scripts use.")
                .Checks(ctx =>
                {
                    var animator = ctx.Root.GetComponentInChildren<Animator>(true);
                    if (animator == null)
                        return SetupResult.Missing("No Animator: add the model with its Animator as a child.");
                    if (animator.runtimeAnimatorController == null)
                        return SetupResult.Wrong($"'{animator.name}' has no Animator Controller.", animator);
                    return SetupResult.Ok($"'{animator.runtimeAnimatorController.name}' on '{animator.name}' ({(animator.isHuman ? "Humanoid" : "Generic")}).", animator);
                }));
            p.Add(SetupRequirement.Make("References", "Weapon Controller ▸ Hand", SetupLevel.Recommended)
                .Explain("Weapons are held at this object (the right hand bone): without it they float at the player's feet.",
                         "Weapon Controller ▸ Hand Game Object: the right hand bone of the model.")
                .Checks(ctx =>
                {
                    var weapon = ctx.Root.GetComponentInChildren<WeaponController>(true);
                    if (weapon == null)
                        return SetupResult.Skip("No Weapon Controller.");
                    return weapon.handGameObject != null ? SetupResult.Ok($"'{weapon.handGameObject.name}'.", weapon) : SetupResult.Missing("Empty: weapons have no hand.", weapon);
                })
                .Fixes("Use Right Hand Bone", ctx =>
                {
                    var weapon = ctx.Root.GetComponentInChildren<WeaponController>(true);
                    Transform hand = RightHand(ctx.Root);
                    if (weapon == null || hand == null)
                    {
                        EditorUtility.DisplayDialog("Setup", "The model is not a Humanoid rig: assign the hand by hand.", "OK");
                        return false;
                    }
                    ctx.Record(weapon, "Hand");
                    weapon.handGameObject = hand.gameObject;
                    ctx.Dirty(weapon);
                    return true;
                }, inFixAll: false));
            p.Add(SetupRequirement.Make("References", "Inventory ▸ Hand Parent", SetupLevel.Recommended)
                .Explain("The item in the hotbar's selected slot is shown here (usually the same hand as the weapon).",
                         "Inventory Manager ▸ Hand Parent: the hand bone.")
                .Checks(ctx =>
                {
                    var inv = ctx.Root.GetComponentInChildren<InventoryManager>(true);
                    if (inv == null)
                        return SetupResult.Skip("No Inventory Manager.");
                    return inv.HandParent != null ? SetupResult.Ok($"'{inv.HandParent.name}'.", inv) : SetupResult.Missing("Empty: held items have nowhere to appear.", inv);
                })
                .Fixes("Use The Weapon Hand", ctx =>
                {
                    var inv = ctx.Root.GetComponentInChildren<InventoryManager>(true);
                    var weapon = ctx.Root.GetComponentInChildren<WeaponController>(true);
                    Transform hand = weapon != null && weapon.handGameObject != null ? weapon.handGameObject.transform : RightHand(ctx.Root);
                    if (inv == null || hand == null)
                        return false;
                    return SetupUtil.SetReference(inv, "handParent", hand);
                }, inFixAll: false));
            p.Add(SetupRequirement.Make("References", "Body part hitboxes", SetupLevel.Optional)
                .Explain("With a Body Part Controller, hitboxes on the bones locate hits (head, torso, arms, legs). Without them hits are located by height.",
                         "Body Part Controller ▸ Add Hitboxes (Humanoid).")
                .Checks(ctx =>
                {
                    var parts = ctx.Root.GetComponentInChildren<BodyPartController>(true);
                    if (parts == null)
                        return SetupResult.Skip("No Body Part Controller.");
                    int n = ctx.Root.GetComponentsInChildren<BodyPartHitbox>(true).Length;
                    return n > 0 ? SetupResult.Ok($"{n} hitbox(es).", parts) : SetupResult.Missing("None: hits are located by height.", parts);
                }));
            p.Add(SetupRequirement.Make("References", "Camera", SetupLevel.Recommended)
                .Explain("The player brings its own camera rig (first / third person, Cinemachine). The camera needs a Cinemachine Brain to follow the player's Cinemachine cameras.",
                         "Put the camera rig under the player; add a CinemachineBrain to the Camera.")
                .Checks(ctx =>
                {
                    var cam = ctx.Root.GetComponentInChildren<Camera>(true);
                    if (cam == null)
                        return SetupResult.Missing("No Camera under the player: the scene must provide one.");
                    Type brain = SetupUtil.TypeByName("Unity.Cinemachine.CinemachineBrain") ?? SetupUtil.TypeByName("Cinemachine.CinemachineBrain");
                    if (brain != null && ctx.Root.GetComponentsInChildren(brain, true).Length == 0)
                        return SetupResult.Wrong($"'{cam.name}' has no CinemachineBrain: the Cinemachine cameras do nothing.", cam);
                    return SetupResult.Ok($"'{cam.name}'.", cam);
                }));

            // ---------------------------------------------------------------- input
            p.Add(SetupRequirement.Make("Input", "Player Input settings", SetupLevel.Recommended)
                .Explain("Unity's Player Input sends camera look, interact and pause: it needs the project's Input Actions asset, Behavior = Invoke Unity Events and Default Map = Player.",
                         "Player Input ▸ Actions: your Input Actions asset; Behavior: Invoke Unity Events; Default Map: Player.")
                .Checks(ctx =>
                {
                    var input = ctx.Root.GetComponentInChildren<UnityEngine.InputSystem.PlayerInput>(true);
                    if (input == null)
                        return SetupResult.Skip("No Player Input (see Animation, camera & input).");
                    var wrong = new List<string>();
                    if (input.actions == null) wrong.Add("no Actions asset");
                    if (input.notificationBehavior != PlayerNotifications.InvokeUnityEvents) wrong.Add($"Behavior is {input.notificationBehavior}");
                    if (input.actions != null && input.defaultActionMap != "Player" && input.actions.FindActionMap("Player", false) != null) wrong.Add($"Default Map is '{input.defaultActionMap}'");
                    return wrong.Count == 0 ? SetupResult.Ok($"'{input.actions.name}', Invoke Unity Events, map Player.", input) : SetupResult.Wrong(string.Join("; ", wrong) + ".", input);
                })
                .Fixes("Fix Player Input", ctx =>
                {
                    var input = ctx.Root.GetComponentInChildren<UnityEngine.InputSystem.PlayerInput>(true);
                    if (input == null)
                        return false;
                    ctx.Record(input, "Player Input");
                    if (input.actions == null)
                    {
                        string path = Validation.SetupChecks.InputAssetPaths().FirstOrDefault();
                        if (path != null)
                            input.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                    }
                    input.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
                    if (input.actions != null && input.actions.FindActionMap("Player", false) != null)
                        input.defaultActionMap = "Player";
                    ctx.Dirty(input);
                    return true;
                }));
            p.Add(SetupRequirement.Make("Input", "Input Actions asset has every action", SetupLevel.Required)
                .Explain("The scripts read their keys by action name (Movement, Look, Jump, Sprint, Use, Interact, Inventory, Pause, Dash...). A missing action means that control does nothing.",
                         "Tools ▸ SimpleMovements ▸ Input ▸ Add Missing Input Actions (adds them on free keys).")
                .Checks(_ =>
                {
                    var issues = new List<Validation.ValidationIssue>();
                    Validation.SetupChecks.CheckInputActions(issues);
                    var missing = issues.Where(i => i.fixLabel == "Add").ToList();
                    if (issues.Any(i => i.message.StartsWith("No Input Actions asset")))
                        return SetupResult.Missing("No Input Actions asset with a Player map under Assets/.");
                    int core = missing.Count(i => i.severity == Validation.IssueSeverity.Error);
                    if (missing.Count == 0)
                        return SetupResult.Ok("Every action the scripts read is there.");
                    return core > 0 ? SetupResult.Wrong($"{core} essential and {missing.Count - core} other action(s) missing.")
                        : SetupResult.Wrong($"{missing.Count} optional action(s) missing (dash, roll, quick slots, targeting...).");
                })
                .Fixes("Add Missing Actions", _ =>
                {
                    var issues = new List<Validation.ValidationIssue>();
                    Validation.SetupChecks.CheckInputActions(issues);
                    return issues.Where(i => i.CanFix && i.fixLabel == "Add").Count(i => i.fix()) > 0;
                }, fixesAsset: true));

            // ---------------------------------------------------------------- interaction and social
            p.Add(SetupRequirement.Make("Interaction & social", "Player Interactor", SetupLevel.Recommended)
                .Explain("Talking to NPCs, buying from merchants, opening chests and doors, picking up items with the Interact key or a click; it shows the prompt.",
                         "Add a Player Interactor to the object with the Player Status Controller (Tools ▸ SimpleMovements ▸ Player ▸ Add Player Interactor To Players In Scene).")
                .Checks(ctx =>
                {
                    var i = ctx.Root.GetComponentInChildren<PlayerInteractor>(true);
                    return SetupResult.Present(i != null, "Found.", "None: NPCs, merchants and interactables cannot be used.", i);
                })
                .Fixes("Add Player Interactor", ctx =>
                {
                    ctx.Add<PlayerInteractor>(StatusObject(ctx.Root));
                    return true;
                }));
            p.Add(SetupRequirement.Make("Interaction & social", "Player Social (guild, fame)", SetupLevel.Optional)
                .Explain("Guild, tag, alliance, fame, title and Allow Inspect. Added automatically at runtime; put it on the prefab only to set values for testing.",
                         "Tools ▸ SimpleMovements ▸ Player ▸ Add Player Social.")
                .Checks(ctx =>
                {
                    var s = ctx.Root.GetComponentInChildren<PlayerSocial>(true);
                    return s != null ? SetupResult.Ok(s.HasGuild ? $"Guild '{s.GuildLine}'." : "No guild set.", s) : SetupResult.Note("Not on the prefab: added at runtime (no guild).");
                })
                .Fixes("Add Player Social", ctx =>
                {
                    ctx.Add<PlayerSocial>(StatusObject(ctx.Root));
                    return true;
                }, inFixAll: false));

#if FISHNET
            p.Add(Networked("Player", "the player", SetupLevel.Recommended,
                "In a network session each client's character is a copy of this prefab: a NetworkObject with prediction (the owner moves at once, the server checks), " +
                "NetPlayer (whose copy is mine), and the relays of movement, abilities, weapons, status, inventory, wallet, realm and interaction.",
                "Network ▸ Make Selected Prefab Networked ▸ Player (configures prediction and smoothing too)."));
#endif
            p.Add(Project<CombatSettings>("CombatSettings", "Combat Settings", SetupLevel.Recommended,
                "Layers (characters, obstacles, ground), friendly fire, PvP, parties, body parts and threat. Without it the defaults are used.",
                () => EditorApplication.ExecuteMenuItem(SMMenu.Root + "Project Setup/Create Combat Settings (Resources)")));
            p.Add(Project<TraitDatabase>("TraitDatabase", "Trait Database", SetupLevel.Recommended,
                "The traits offered at character creation and given by the Trait Manager.",
                () => EditorApplication.ExecuteMenuItem(SMMenu.Root + "Project Setup/Create Trait Database (Resources)")));
            p.Add(Project<ItemDatabase>(ItemDatabase.ResourcesName, "Item Database", SetupLevel.Recommended,
                "Stable ids of every item: saving, merchants, the item panel, and the network.",
                () => { NetworkSetupMenuBridge.BuildItemDatabase(); return true; }));
            p.Add(SetupUtil.HierarchyHealth());
            return p;
        }

        private static bool IsPlayer(GameObject go) => go != null && go.GetComponentInChildren<PlayerStatusController>(true) != null;

        private static SetupRequirement PlayerScript(Check c)
        {
            SetupLevel level = c.importance == Importance.Required ? SetupLevel.Required
                : c.importance == Importance.Recommended ? SetupLevel.Recommended : SetupLevel.Optional;
            bool automatic = c.importance == Importance.Automatic;
            string how = c.byStatusWizard ? "Player Status Controller ▸ Complete Setup Wizard creates and wires it."
                : $"Add Component ▸ {ObjectNames.NicifyVariableName(c.Name)}" + (c.inChildren ? " (on the player or one of its children)." : " (on the player's root).");
            return SetupRequirement.Make(PlayerGroup(c), ObjectNames.NicifyVariableName(c.Name), level)
                .Explain(c.purpose + (automatic ? " Added at runtime when missing: add it only to change its settings." : ""), how)
                .Checks(ctx =>
                {
                    if (PlayerSetupValidator.Has(ctx.Root, c))
                    {
                        Component comp = ctx.Root.GetComponent(c.type);
                        if (comp == null)
                            comp = ctx.Root.GetComponentInChildren(c.type, true);
                        return SetupResult.Ok(comp != null && comp.gameObject != ctx.Root ? $"On '{comp.name}'." : "On the root.", comp);
                    }
                    return automatic ? SetupResult.Note("Not on the prefab: added automatically at runtime.") : SetupResult.Missing("Not on the player.");
                })
                .Fixes(automatic ? "Add (to change settings)" : c.byStatusWizard ? "Run Status Setup Wizard" : "Add", ctx =>
                {
                    if (c.byStatusWizard && ctx.Root.GetComponentInChildren<PlayerStatusController>(true) != null)
                    {
                        RunStatusWizard(ctx);
                        if (PlayerSetupValidator.Has(ctx.Root, c))
                            return true;
                    }
                    ctx.Add(ctx.Root, c.type);
                    if (c.type == typeof(PlayerStatusController))
                        RunStatusWizard(ctx);
                    return true;
                }, inFixAll: !automatic);
        }

        private static bool RunStatusWizard(SetupContext ctx)
        {
            var status = ctx.Root.GetComponentInChildren<PlayerStatusController>(true);
            if (status == null)
                return false;
            var helper = new PlayerStatusSetupHelper(status);
            if (ctx.UseUndo)
                Undo.RegisterFullObjectHierarchyUndo(status.gameObject, "Status Setup Wizard");
            helper.CreateMissingComponents();
            helper.AutoAssignComponents();
            ctx.Dirty(status);
            return true;
        }

        private static GameObject StatusObject(GameObject root)
        {
            var status = root.GetComponentInChildren<PlayerStatusController>(true);
            return status != null ? status.gameObject : root;
        }

        private static Transform RightHand(GameObject root)
        {
            var animator = root.GetComponentInChildren<Animator>(true);
            return animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        }

        // ================================================================== Mob

        public static SetupProfile MobPrefab()
        {
            var p = new SetupProfile
            {
                Id = "mob",
                Name = "Mob Prefab",
                Kind = SetupTargetKind.Prefab,
                Summary = "A creature with AI: senses, movement, combat, abilities.",
                Description =
                    "A mob: an AI-driven creature that wanders, notices players and other mobs, fights with its combat style and " +
                    "abilities, and drops loot. The NavMeshAgent moves it (the Rigidbody is kinematic: physics only detects hits); " +
                    "the Mob Profile asset holds its behaviour (temperament, senses, movement, fighting style, dodging, fleeing).\n\n" +
                    "World mobs are spawned by the terrain (EndlessTerrain ▸ Mob Settings ▸ Prefabs); dungeon mobs by the " +
                    "dungeon's encounter tables. In multiplayer only the server runs the AI; clients see a synchronized copy.",
                DocPath = "Mob/01-Mob-Setup.md",
                FindDefault = () => SetupUtil.FindPrefab(IsMob),
                IsCandidate = IsMob,
            };

            p.Add(SetupRequirement.Make("Core", "Mob (Mob Actions Controller)", SetupLevel.Required)
                .Explain("The mob itself: type, preys, team, loot, the AI's brain and its link to every other part.",
                         "GameObject ▸ SimpleMovements ▸ Quick Setup Mob adds and wires every component below.")
                .Checks(ctx =>
                {
                    Mob mob = ctx.Root.GetComponentInChildren<Mob>(true);
                    return SetupResult.Present(mob != null, $"'{(mob != null ? mob.type : "")}' on '{(mob != null ? mob.name : "")}'.", "No Mob component.", mob);
                })
                .Fixes("Run Quick Setup (adds and wires everything)", ctx => QuickSetup(ctx)));
            p.Add(MobComponents("Navigation & physics", "NavMesh Agent", SetupLevel.Required, "Moves the mob on the baked NavMesh (bake it in your scenes / the dungeon builder does it).", typeof(NavMeshAgent)));
            p.Add(MobComponents("Navigation & physics", "Collider", SetupLevel.Required, "Weapons, projectiles and abilities hit this collider.", typeof(Collider)));
            p.Add(SetupRequirement.Make("Navigation & physics", "Kinematic Rigidbody", SetupLevel.Required)
                .Explain("The NavMesh Agent moves the mob; a physics (non-kinematic) Rigidbody would fight it. Kinematic, no gravity: physics only detects hits.",
                         "Rigidbody ▸ Is Kinematic on, Use Gravity off, Freeze Rotation.")
                .Checks(ctx =>
                {
                    var rb = ctx.Root.GetComponent<Rigidbody>();
                    if (rb == null)
                        return SetupResult.Missing("No Rigidbody: trigger hits (weapons, projectiles) may be missed.");
                    return rb.isKinematic && !rb.useGravity ? SetupResult.Ok("Kinematic.", rb) : SetupResult.Wrong("Not kinematic (or uses gravity): it fights the NavMesh Agent.", rb);
                })
                .Fixes("Make It Kinematic", ctx =>
                {
                    var rb = ctx.Add<Rigidbody>(ctx.Root);
                    ctx.Record(rb, "Kinematic");
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.constraints = RigidbodyConstraints.FreezeRotation;
                    ctx.Dirty(rb);
                    return true;
                }));
            p.Add(MobComponents("Status", "Status (controller, health, speed)", SetupLevel.Required, "Health (damage, death, loot) and movement speeds (slows, roots).",
                typeof(MobStatusController), typeof(HealthManager), typeof(SpeedManager)));
            p.Add(Wired("Status", "Status wired", "MobStatusController", new[] { "healthManager", "speedManager" }, "The status controller reads health and speed through these fields."));
            p.Add(MobComponents("AI", "Mob Movement State Machine (the AI)", SetupLevel.Required, "Chooses what the mob does: wander, chase, attack, dodge, flee.", typeof(MobMovementStateMachine)));
            p.Add(Wired("AI", "AI wired", "MobMovementStateMachine", new[] { "navMeshAgent", "statusController", "animator" }, "The AI drives the agent, reads the status and plays the animations."));
            p.Add(MobComponents("AI", "Mob Ability Controller", SetupLevel.Recommended, "The mob's attacks and abilities (a basic bite is used when it has none).", typeof(MobAbilityController)));
            p.Add(MobComponents("AI", "Combat Entity", SetupLevel.Recommended, "Team, faction, crowd control and threat (added at runtime when missing).", typeof(CombatEntity)));
            p.Add(SetupRequirement.Make("Behaviour", "Mob Profile", SetupLevel.Recommended)
                .Explain("Temperament, senses, movement, fighting style, dodging, fleeing and animation names, in an asset shared by mobs of the same type. Without it one is built from the old fields at runtime (hidden, not editable).",
                         "Mob ▸ Profile: a Mob Profile asset (Assets ▸ Create ▸ SimpleMovements ▸ Mobs ▸ Mob Profile From Preset).")
                .Checks(ctx =>
                {
                    Mob mob = ctx.Root.GetComponentInChildren<Mob>(true);
                    if (mob == null)
                        return SetupResult.Skip("Needs the Mob first.");
                    return mob.ProfileAsset != null ? SetupResult.Ok($"'{mob.ProfileAsset.name}'.", mob.ProfileAsset) : SetupResult.Missing("None: built from the old fields at runtime.", mob);
                })
                .Fixes("Create Profile From The Mob's Fields", ctx =>
                {
                    Mob mob = ctx.Root.GetComponentInChildren<Mob>(true);
                    return mob != null && MobSetupUtility.EnsureProfile(mob) != null;
                }));
            p.Add(SetupRequirement.Make("Behaviour", "Type and preys", SetupLevel.Optional)
                .Explain("Type names the kind of mob (mobs of one type are allies). Preys lists what it hunts: 'Player' to attack players, other types to hunt mobs.",
                         "Mob ▸ Type and Preys.")
                .Checks(ctx =>
                {
                    Mob mob = ctx.Root.GetComponentInChildren<Mob>(true);
                    if (mob == null)
                        return SetupResult.Skip("Needs the Mob first.");
                    List<string> preys = mob.PreysReference ?? new List<string>();
                    string t = string.IsNullOrWhiteSpace(mob.type) ? "(no type)" : mob.type;
                    return preys.Count == 0
                        ? SetupResult.Note($"Type '{t}', hunts nothing (attacks only when provoked, if its temperament allows).", mob)
                        : SetupResult.Ok($"Type '{t}', hunts {string.Join(", ", preys)}.", mob);
                }));
            p.Add(SetupRequirement.Make("Behaviour", "Animator with a controller", SetupLevel.Recommended)
                .Explain("Plays walk / run / attack / hit / death animations (parameters Speed, IsMoving, Hit, Die... are used when present).",
                         "Assign an Animator Controller on the model's Animator.")
                .Checks(ctx =>
                {
                    var animator = ctx.Root.GetComponentInChildren<Animator>(true);
                    if (animator == null)
                        return SetupResult.Missing("No Animator.");
                    return animator.runtimeAnimatorController != null ? SetupResult.Ok($"'{animator.runtimeAnimatorController.name}'.", animator) : SetupResult.Wrong("No Animator Controller.", animator);
                }));
            p.Add(CharacterLayer("Navigation & physics", "the mob"));
#if FISHNET
            p.Add(Networked("Mob", "the mob", SetupLevel.Recommended,
                "In a network session only the server runs the AI. The mob needs a NetworkObject, NetMob (position, animation, AI state), NetCombatStatus (health, buffs, hits) and NetAbilityCaster; its transform is server-driven.",
                "Network ▸ Make Selected Prefab Networked ▸ Mob (or Make All Mob Prefabs Networked)."));
#endif
            p.Add(SetupUtil.HierarchyHealth());
            return p;
        }

        private static bool IsMob(GameObject go) => go != null && go.GetComponentInChildren<Mob>(true) != null;

        private static bool QuickSetup(SetupContext ctx)
        {
            MobSetupUtility.SetupMob(ctx.Root, false, true);
            ctx.Dirty(ctx.Root);
            return true;
        }

        private static SetupRequirement MobComponents(string group, string title, SetupLevel level, string why, params Type[] types) =>
            SetupRequirement.Make(group, title, level)
                .Explain(why, "GameObject ▸ SimpleMovements ▸ Quick Setup Mob adds and wires it.")
                .Checks(ctx =>
                {
                    List<string> missing = types.Where(t => ctx.Root.GetComponentInChildren(t, true) == null).Select(t => t.Name).ToList();
                    Component first = ctx.Root.GetComponentInChildren(types[0], true);
                    return missing.Count == 0 ? SetupResult.Ok("Present.", first) : SetupResult.Missing("Missing: " + string.Join(", ", missing) + ".");
                })
                .Fixes("Run Quick Setup", ctx => QuickSetup(ctx));

        private static SetupRequirement Wired(string group, string title, string typeName, string[] fields, string why) =>
            SetupRequirement.Make(group, title, SetupLevel.Required)
                .Explain(why, "GameObject ▸ SimpleMovements ▸ Quick Setup Mob re-wires the references.")
                .Checks(ctx =>
                {
                    Component c = ctx.Root.GetComponentsInChildren<Component>(true).FirstOrDefault(x => x != null && x.GetType().Name == typeName);
                    if (c == null)
                        return SetupResult.Skip($"No {typeName}.");
                    var so = new SerializedObject(c);
                    List<string> empty = fields.Where(f => so.FindProperty(f) is SerializedProperty p && p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue == null).ToList();
                    return empty.Count == 0 ? SetupResult.Ok("Linked.", c) : SetupResult.Wrong("Empty: " + string.Join(", ", empty.Select(ObjectNames.NicifyVariableName)) + ".", c);
                })
                .Fixes("Run Quick Setup", ctx => QuickSetup(ctx));

        // ================================================================== Item

        public static SetupProfile Item()
        {
            var p = new SetupProfile
            {
                Id = "item",
                Name = "Item Prefab",
                Kind = SetupTargetKind.Prefab,
                Summary = "The 3D model of an item: held in the hand and lying in the world.",
                Description =
                    "The model an item uses (Item ▸ Prefab): it is held in the player's hand when the item is selected, and " +
                    "spawned in the world when the item is dropped, looted or placed in a scene. Items lying in the world are " +
                    "picked up through their Item Pickable and its collider.\n\n" +
                    "Dropping adds a collider and an Item Pickable when missing, so a bare model works; add them yourself to shape " +
                    "the collider or to place the item directly in a scene. In multiplayer, item prefabs that are not networked are " +
                    "shown to everyone through the networked carrier (Resources/NetWorldItem).",
                DocPath = "Inventory/08-Setup-Guide.md",
                FindDefault = () =>
                {
                    HashSet<string> used = ItemPrefabPaths();
                    return used.FirstOrDefault() ?? SetupUtil.FindPrefab(go => go.GetComponent<ItemPickable>() != null);
                },
                IsCandidate = go => go != null && (go.GetComponent<ItemPickable>() != null || ItemPrefabPaths().Contains(AssetDatabase.GetAssetPath(go))),
            };

            p.Add(SetupRequirement.Make("Model", "A visible model", SetupLevel.Required)
                .Explain("Mesh Renderers or Skinned Mesh Renderers: what the player sees in the hand and on the ground.", "Put the model's meshes under the prefab.")
                .Checks(ctx =>
                {
                    Renderer r = ctx.Root.GetComponentsInChildren<Renderer>(true).FirstOrDefault(x => x is MeshRenderer || x is SkinnedMeshRenderer || x is SpriteRenderer);
                    return SetupResult.Present(r != null, $"'{(r != null ? r.name : "")}'.", "No renderer: the item is invisible.", r);
                }));
            p.Add(SetupRequirement.Make("Model", "Collider", SetupLevel.Recommended)
                .Explain("The interaction ray hits it to pick the item up; it also rests the item on the ground. Without one a box around the model is added when it is dropped.",
                         "Add a Box / Capsule / Mesh (convex) Collider fitted to the model.")
                .Checks(ctx =>
                {
                    Collider c = ctx.Root.GetComponentInChildren<Collider>(true);
                    return c != null ? SetupResult.Ok($"{c.GetType().Name} on '{c.name}'.", c) : SetupResult.Missing("None: a box is added when dropped (you cannot place it in a scene to pick up).");
                })
                .Fixes("Add Fitted Box Collider", ctx =>
                {
                    BoxCollider box = ctx.Add<BoxCollider>(ctx.Root);
                    Renderer[] renderers = ctx.Root.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length > 0)
                    {
                        Bounds b = renderers[0].bounds;
                        foreach (Renderer r in renderers)
                            b.Encapsulate(r.bounds);
                        Transform t = ctx.Root.transform;
                        Vector3 s = t.lossyScale;
                        box.center = t.InverseTransformPoint(b.center);
                        box.size = new Vector3(b.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)), b.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)), b.size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
                    }
                    ctx.Dirty(box);
                    return true;
                }));
            p.Add(SetupRequirement.Make("Model", "Not on the Ignore Raycast layer", SetupLevel.Required)
                .Explain("The interaction ray skips the Ignore Raycast layer: an item there can never be picked up.", "Set the layer of the prefab and its children to Default (or your items layer).")
                .Checks(ctx =>
                {
                    int ignore = LayerMask.NameToLayer("Ignore Raycast");
                    Transform bad = ctx.Root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.gameObject.layer == ignore && t.GetComponent<Collider>() != null);
                    return bad == null ? SetupResult.Ok("Colliders are on a hittable layer.") : SetupResult.Wrong($"'{bad.name}' is on Ignore Raycast.", bad);
                })
                .Fixes("Move To Default", ctx =>
                {
                    int ignore = LayerMask.NameToLayer("Ignore Raycast");
                    foreach (Transform t in ctx.Root.GetComponentsInChildren<Transform>(true))
                        if (t.gameObject.layer == ignore)
                        {
                            ctx.Record(t.gameObject, "Layer");
                            t.gameObject.layer = 0;
                            ctx.Dirty(t.gameObject);
                        }
                    return true;
                }));

            p.Add(SetupRequirement.Make("Item", "Used by an item", SetupLevel.Recommended)
                .Explain("An Item asset (Item ▸ Prefab) says which item this model is; without one the prefab is never held or dropped.",
                         "Select the Item asset (weapon, armor, consumable...) ▸ Prefab: this prefab.")
                .Checks(ctx =>
                {
                    List<ItemSO> items = ItemsUsing(ctx);
                    if (items.Count == 0)
                        return SetupResult.Missing("No Item asset uses this prefab.");
                    return SetupResult.Ok($"{string.Join(", ", items.Take(4).Select(i => i.name))}{(items.Count > 4 ? "…" : "")}.", items[0]);
                }));
            p.Add(SetupRequirement.Make("Item", "Item Pickable", SetupLevel.Recommended)
                .Explain("Lets players pick the item up (with its amount and durability). Needed to place the item directly in a scene; dropped items get one automatically.",
                         "Add Component ▸ Item Pickable on the prefab root.")
                .Checks(ctx =>
                {
                    var pick = ctx.Root.GetComponent<ItemPickable>();
                    return pick != null ? SetupResult.Ok("Found.", pick) : SetupResult.Missing("None: added when dropped, but a copy placed in a scene cannot be picked up.");
                })
                .Fixes("Add Item Pickable", ctx =>
                {
                    ItemPickable pick = ctx.Add<ItemPickable>(ctx.Root);
                    ItemSO item = ItemsUsing(ctx).FirstOrDefault();
                    if (item != null && pick.itemScriptableObject == null)
                    {
                        pick.itemScriptableObject = item;
                        ctx.Dirty(pick);
                    }
                    return true;
                }));
            p.Add(SetupRequirement.Make("Item", "Item Pickable ▸ Item", SetupLevel.Recommended)
                .Explain("Which item a copy placed in a scene gives (dropped items set it themselves).", "Item Pickable ▸ Item Scriptable Object: the item that uses this prefab.")
                .Checks(ctx =>
                {
                    var pick = ctx.Root.GetComponent<ItemPickable>();
                    if (pick == null)
                        return SetupResult.Skip("No Item Pickable.");
                    return pick.itemScriptableObject != null ? SetupResult.Ok($"'{pick.itemScriptableObject.name}'.", pick) : SetupResult.Missing("Empty: a copy placed in a scene gives nothing.", pick);
                })
                .Fixes("Assign The Item Using It", ctx =>
                {
                    var pick = ctx.Root.GetComponent<ItemPickable>();
                    ItemSO item = ItemsUsing(ctx).FirstOrDefault();
                    if (pick == null || item == null)
                        return false;
                    ctx.Record(pick, "Item");
                    pick.itemScriptableObject = item;
                    ctx.Dirty(pick);
                    return true;
                }));
            p.Add(SetupRequirement.Make("Item", "Items in the Item Database", SetupLevel.Recommended)
                .Explain("Items are saved, sold and sent over the network by their id in the Item Database.", "Tools ▸ SimpleMovements ▸ Project Setup ▸ Build Item Database.")
                .Checks(ctx =>
                {
                    List<ItemSO> items = ItemsUsing(ctx);
                    if (items.Count == 0)
                        return SetupResult.Skip("No item uses this prefab.");
                    ItemDatabase db = Resources.Load<ItemDatabase>(ItemDatabase.ResourcesName);
                    if (db == null)
                        return SetupResult.Missing("No Item Database in Resources.");
                    List<ItemSO> missing = items.Where(i => string.IsNullOrEmpty(db.IdOf(i))).ToList();
                    return missing.Count == 0 ? SetupResult.Ok("All registered.", db) : SetupResult.Missing($"Not registered: {string.Join(", ", missing.Take(4).Select(i => i.name))}.", db);
                })
                .Fixes("Build Item Database", _ =>
                {
                    NetworkSetupMenuBridge.BuildItemDatabase();
                    return true;
                }, fixesAsset: true));
            p.Add(SetupRequirement.Make("Item", "Icons", SetupLevel.Optional)
                .Explain("The picture shown in inventory slots and the item panel ('Icon From Prefab' on the item renders one from this model).", "Item ▸ Icon.")
                .Checks(ctx =>
                {
                    List<ItemSO> items = ItemsUsing(ctx);
                    if (items.Count == 0)
                        return SetupResult.Skip("No item uses this prefab.");
                    List<ItemSO> without = items.Where(i => i.Icon == null).ToList();
                    return without.Count == 0 ? SetupResult.Ok("Every item has an icon.") : SetupResult.Missing($"No icon: {string.Join(", ", without.Take(4).Select(i => i.name))}.", without[0]);
                }));
#if FISHNET
            p.Add(SetupRequirement.Make("Multiplayer", "World item carrier exists", SetupLevel.Recommended)
                .Explain("Item prefabs that are not networked (most of them) are shown to every client through the networked carrier Resources/NetWorldItem.",
                         "Tools ▸ SimpleMovements ▸ Network ▸ Create World Item Prefab.")
                .Checks(_ =>
                {
                    var carrier = Resources.Load<GameObject>("NetWorldItem");
                    return SetupResult.Present(carrier != null, "Found.", "Missing: dropped items only exist on the server.", carrier);
                })
                .Fixes("Create It", _ =>
                {
                    NetworkSetupMenu.CreateWorldItemPrefab();
                    return true;
                }, fixesAsset: true));
            p.Add(Networked("Pickup / Loot", "the item", SetupLevel.Optional,
                "Not needed: the carrier shows items whose prefab is not networked. Make it networked only for items with their own network behaviour (a physics loot drop that must move the same everywhere).",
                "Network ▸ Make Selected Prefab Networked ▸ Pickup or Loot Drop."));
#endif
            p.Add(SetupUtil.HierarchyHealth());
            return p;
        }

        private static float cacheTime = -10f;
        private static Dictionary<string, List<ItemSO>> itemsByPrefab = new Dictionary<string, List<ItemSO>>();

        /// <summary>Item assets by the asset path of their prefab (cached for two seconds).</summary>
        private static Dictionary<string, List<ItemSO>> ItemsByPrefab()
        {
            if (EditorApplication.timeSinceStartup - cacheTime < 2f)
                return itemsByPrefab;
            cacheTime = (float)EditorApplication.timeSinceStartup;
            itemsByPrefab = new Dictionary<string, List<ItemSO>>();
            foreach (string guid in AssetDatabase.FindAssets("t:ItemSO", new[] { "Assets" }))
            {
                ItemSO item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || item.Prefab == null)
                    continue;
                string path = AssetDatabase.GetAssetPath(item.Prefab);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (!itemsByPrefab.TryGetValue(path, out List<ItemSO> list))
                    itemsByPrefab[path] = list = new List<ItemSO>();
                list.Add(item);
            }
            return itemsByPrefab;
        }

        private static HashSet<string> ItemPrefabPaths() => new HashSet<string>(ItemsByPrefab().Keys);

        private static List<ItemSO> ItemsUsing(SetupContext ctx)
        {
            string path = ctx.AssetPath;
            if (string.IsNullOrEmpty(path) && ctx.Root != null)
                path = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(ctx.Root));
            return !string.IsNullOrEmpty(path) && ItemsByPrefab().TryGetValue(path, out List<ItemSO> list) ? list : new List<ItemSO>();
        }

        // ================================================================== shared

        private static SetupRequirement CharacterLayer(string group, string who) =>
            SetupRequirement.Make(group, "Layer hittable (Combat Settings ▸ Character Layers)", SetupLevel.Required)
                .Explain($"Attacks and abilities only look for characters on the Character Layers of the Combat Settings: if {who}'s layer is not there, nothing can hit {who}.",
                         "Combat Settings (Resources) ▸ Character Layers: include this layer.")
                .Checks(ctx =>
                {
                    CombatSettings settings = Resources.Load<CombatSettings>("CombatSettings");
                    string layer = LayerMask.LayerToName(ctx.Root.layer);
                    if (settings == null)
                        return SetupResult.Note($"Layer '{layer}'. No Combat Settings asset: every layer counts.");
                    return (settings.characterLayers.value & (1 << ctx.Root.layer)) != 0
                        ? SetupResult.Ok($"Layer '{layer}' is included.", settings)
                        : SetupResult.Wrong($"Layer '{layer}' is not in Character Layers: nothing can hit {who}.", settings);
                })
                .Fixes("Add Layer To Combat Settings", ctx =>
                {
                    CombatSettings settings = Resources.Load<CombatSettings>("CombatSettings");
                    if (settings == null)
                        return false;
                    Undo.RecordObject(settings, "Character layer");
                    settings.characterLayers |= 1 << ctx.Root.layer;
                    EditorUtility.SetDirty(settings);
                    return true;
                }, fixesAsset: true);

        private static SetupRequirement Project<T>(string resourcesName, string title, SetupLevel level, string why, Func<bool> create) where T : Object =>
            SetupUtil.ResourcesAsset<T>("Project", title, resourcesName, level, why, create);

#if FISHNET
        /// <summary>The network components of one kind of prefab (NetworkSetupMenu.PrefabComponents), with Make Networked as the fix.</summary>
        private static SetupRequirement Networked(string kind, string who, SetupLevel level, string why, string how) =>
            SetupRequirement.Make("Multiplayer", "Network components", level)
                .Explain(why, how)
                .Checks(ctx =>
                {
                    var missing = new List<string>();
                    if (ctx.Root.GetComponent<NetworkObject>() == null)
                        missing.Add("NetworkObject");
                    if (NetworkSetupMenu.PrefabComponents.TryGetValue(kind, out string[] types))
                        foreach (string t in types)
                        {
                            Type type = SetupUtil.TypeByName(t);
                            if (type != null && ctx.Root.GetComponentInChildren(type, true) == null)
                                missing.Add(SetupUtil.ShortName(t));
                        }
                    if (missing.Count == 0)
                        return SetupResult.Ok("Networked.", ctx.Root.GetComponent<NetworkObject>());
                    return SetupResult.Missing($"Not networked{(missing.Count < 6 ? " - missing " + string.Join(", ", missing) : "")}: {who} only exists offline.");
                })
                .Fixes("Make Networked", ctx =>
                {
                    if (ctx.IsPrefabAsset)
                    {
                        Debug.Log("[Setup] " + NetworkSetupMenu.MakePrefab(ctx.AssetPath, kind));
                        return true;
                    }
                    // An object in a scene or the prefab stage: the same components (prediction settings are made by Make Networked on the asset).
                    ctx.Add(ctx.Root, typeof(NetworkObject));
                    if (NetworkSetupMenu.PrefabComponents.TryGetValue(kind, out string[] list))
                        foreach (string t in list)
                            ctx.Add(ctx.Root, SetupUtil.TypeByName(t));
                    return true;
                }, fixesAsset: true);
#endif
    }

    /// <summary>The item database builder lives with the network menu; a stable name for the profiles.</summary>
    internal static class NetworkSetupMenuBridge
    {
        public static void BuildItemDatabase() => GameNet.EditorTools.NetworkSetupMenu.BuildItemDatabase();
    }
}
#endif
