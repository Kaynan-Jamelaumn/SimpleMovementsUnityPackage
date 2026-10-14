#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Completes the player's Animator Controller for everything the game drives:
/// <list type="bullet">
/// <item>adds every parameter the scripts set (movement, swimming, combat, blocking, actions, state) that is missing,
/// and reports parameters that exist with the wrong type;</item>
/// <item>adds the states the newer systems need when the controller has none: <b>Swim</b> (Any State → Swim on
/// IsSwimming, back out when it turns off), <b>Fall</b> (from Any State on IsFalling, out when grounded) and <b>Death</b>
/// (Any State on DeathTrigger, out on revive). They are created empty: drag your clips onto them.</item>
/// </list>
/// Nothing that exists is changed or removed. Select the player prefab (or the controller asset) and use
/// Tools ▸ SimpleMovements ▸ Player ▸ Complete Player Animator Controller. "Check" only reports.
/// </summary>
public static class PlayerAnimatorSetup
{
    private const string Menu = "Tools/SimpleMovements/Player/";

    /// <summary>Every parameter the game sets, with its type and what drives it.</summary>
    public static readonly (string name, AnimatorControllerParameterType type, string use)[] Parameters =
    {
        // Locomotion (MovementAnimationHandler)
        ("MovementX", AnimatorControllerParameterType.Float, "move stick x (strafe), smoothed"),
        ("MovementZ", AnimatorControllerParameterType.Float, "move stick y (forward/back), smoothed"),
        ("MovementSpeed", AnimatorControllerParameterType.Float, "length of the move stick (0-1)"),
        ("IsWalking", AnimatorControllerParameterType.Bool, "Walking state"),
        ("IsRunning", AnimatorControllerParameterType.Bool, "Running (sprint) state"),
        ("IsCrouching", AnimatorControllerParameterType.Bool, "Crouching state"),
        ("IsJumping", AnimatorControllerParameterType.Bool, "Jumping state"),
        ("IsDashing", AnimatorControllerParameterType.Bool, "Dashing state"),
        ("IsRolling", AnimatorControllerParameterType.Bool, "Rolling state"),
        // Ground and air (CharacterStateAnimationHandler)
        ("IsGrounded", AnimatorControllerParameterType.Bool, "on the ground (or swimming)"),
        ("IsFalling", AnimatorControllerParameterType.Bool, "in the air and going down"),
        ("AirTime", AnimatorControllerParameterType.Float, "seconds off the ground"),
        ("JumpTrigger", AnimatorControllerParameterType.Trigger, "a jump starts"),
        ("LandTrigger", AnimatorControllerParameterType.Trigger, "landing after a fall"),
        // Swimming
        ("IsSwimming", AnimatorControllerParameterType.Bool, "Swimming state"),
        ("IsUnderwater", AnimatorControllerParameterType.Bool, "swimming with the head under the surface"),
        ("SwimVertical", AnimatorControllerParameterType.Float, "up/down speed m/s (+ = up)"),
        ("SwimPitch", AnimatorControllerParameterType.Float, "swim angle in degrees (+ = rising, - = diving)"),
        ("SwimSpeed", AnimatorControllerParameterType.Float, "swim speed m/s"),
        // Actions
        ("DashTrigger", AnimatorControllerParameterType.Trigger, "a dash starts"),
        ("RollTrigger", AnimatorControllerParameterType.Trigger, "a roll starts"),
        ("IsInputLocked", AnimatorControllerParameterType.Bool, "an action locks the input"),
        // Attacks (weapons)
        ("IsAttacking", AnimatorControllerParameterType.Bool, "an attack plays"),
        ("AttackCombo", AnimatorControllerParameterType.Int, "combo step"),
        ("AttackTrigger", AnimatorControllerParameterType.Trigger, "generic attack"),
        ("LightAttackTrigger", AnimatorControllerParameterType.Trigger, "light attack"),
        ("HeavyAttackTrigger", AnimatorControllerParameterType.Trigger, "heavy attack"),
        ("ChargedAttackTrigger", AnimatorControllerParameterType.Trigger, "charged attack"),
        ("SpecialAttackTrigger", AnimatorControllerParameterType.Trigger, "special attack"),
        ("AlternateAttackTrigger", AnimatorControllerParameterType.Trigger, "alternate attack"),
        ("OffHandAttackTrigger", AnimatorControllerParameterType.Trigger, "off-hand attack"),
        // Blocking (BlockController)
        ("IsBlocking", AnimatorControllerParameterType.Bool, "block held"),
        ("BlockHitTrigger", AnimatorControllerParameterType.Trigger, "a hit was blocked"),
        ("ParryTrigger", AnimatorControllerParameterType.Trigger, "a parry"),
        ("GuardBreakTrigger", AnimatorControllerParameterType.Trigger, "the guard broke"),
        ("ShieldBashTrigger", AnimatorControllerParameterType.Trigger, "shield bash"),
        // State (CharacterStateAnimationHandler)
        ("HitTrigger", AnimatorControllerParameterType.Trigger, "took damage"),
        ("IsStunned", AnimatorControllerParameterType.Bool, "cannot move (stun) but alive"),
        ("IsDead", AnimatorControllerParameterType.Bool, "dead"),
        ("DeathTrigger", AnimatorControllerParameterType.Trigger, "died"),
    };

    [MenuItem(SMMenu.Root + "Player/Complete Player Animator Controller", false, SMMenu.Player + 20)]
    private static void CompleteMenu() => Run(fix: true);

    [MenuItem(SMMenu.Root + "Player/Check Player Animator Controller", false, SMMenu.Player + 21)]
    private static void CheckMenu() => Run(fix: false);

    private static void Run(bool fix)
    {
        AnimatorController controller = SelectedController(out string where);
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Player Animator", "Select the player prefab (or its Animator Controller asset) first. " +
                                        "The controller is the one on the player's Animator.", "OK");
            return;
        }
        string report = Complete(controller, fix);
        Debug.Log($"[Player Animator] {where}:\n{report}", controller);
        EditorUtility.DisplayDialog("Player Animator - " + controller.name, report.Length > 1800 ? report.Substring(0, 1800) + "\n... (full report in the Console)" : report, "OK");
    }

    private static AnimatorController SelectedController(out string where)
    {
        where = "";
        Object o = Selection.activeObject;
        if (o is AnimatorController ac)
        {
            where = AssetDatabase.GetAssetPath(ac);
            return ac;
        }
        if (o is AnimatorOverrideController oc && oc.runtimeAnimatorController is AnimatorController baseController)
        {
            where = AssetDatabase.GetAssetPath(baseController) + " (base of the override controller)";
            return baseController;
        }
        if (o is GameObject go)
        {
            foreach (Animator a in go.GetComponentsInChildren<Animator>(true))
            {
                RuntimeAnimatorController rac = a.runtimeAnimatorController;
                if (rac is AnimatorOverrideController aoc)
                    rac = aoc.runtimeAnimatorController;
                if (rac is AnimatorController found)
                {
                    where = $"{go.name} ▸ {a.name} ▸ {AssetDatabase.GetAssetPath(found)}";
                    return found;
                }
            }
        }
        return null;
    }

    /// <summary>Adds what is missing to <paramref name="controller"/> (or only reports with <paramref name="fix"/> false).</summary>
    public static string Complete(AnimatorController controller, bool fix)
    {
        var sb = new StringBuilder();
        var added = new List<string>();
        var wrongType = new List<string>();
        var existing = controller.parameters.ToDictionary(p => p.name, p => p.type);
        if (fix)
            Undo.RecordObject(controller, "Complete Player Animator Controller");

        foreach (var (name, type, use) in Parameters)
        {
            if (existing.TryGetValue(name, out AnimatorControllerParameterType has))
            {
                if (has != type)
                    wrongType.Add($"{name}: is {has}, the game sets a {type} ({use})");
                continue;
            }
            added.Add($"{name} ({type}) - {use}");
            if (fix)
                controller.AddParameter(name, type);
        }

        var states = new List<string>();
        if (controller.layers.Length > 0)
        {
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            if (!HasState(sm, "swim"))
            {
                states.Add("Swim: Any State → Swim when IsSwimming, back to the default state when it turns off. Put a swim blend tree here (SwimSpeed / SwimPitch).");
                if (fix) AddSwim(sm);
            }
            if (!HasState(sm, "fall"))
            {
                states.Add("Fall: Any State → Fall when IsFalling and AirTime > 0.3, back when IsGrounded. Put a falling loop here.");
                if (fix) AddFall(sm);
            }
            if (!HasState(sm, "death") && !HasState(sm, "die"))
            {
                states.Add("Death: Any State → Death on DeathTrigger, back when IsDead turns off (respawn). Put a death clip here (Loop Time off).");
                if (fix) AddDeath(sm);
            }
        }

        if (fix)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        string verb = fix ? "Added" : "Missing";
        sb.AppendLine(added.Count == 0 ? "Every parameter the game uses is there." : $"{verb} {added.Count} parameter(s):");
        foreach (string a in added) sb.AppendLine("  + " + a);
        if (wrongType.Count > 0)
        {
            sb.AppendLine("Parameters with the wrong type (change them by hand - the game skips them):");
            foreach (string w in wrongType) sb.AppendLine("  ! " + w);
        }
        if (states.Count > 0)
        {
            sb.AppendLine(fix ? "Added states (empty - drag your clips onto them in the Animator window):" : "States the game can use that the controller does not have:");
            foreach (string s in states) sb.AppendLine("  + " + s);
        }
        if (!fix && (added.Count > 0 || states.Count > 0))
            sb.AppendLine("Use 'Complete Player Animator Controller' to add them (nothing existing is changed).");
        sb.AppendLine("Footsteps: add an Animation Event named OnFootstep on the frames where a foot touches the ground (walk/run clips), and OnLanding on landing clips.");
        return sb.ToString();
    }

    private static bool HasState(AnimatorStateMachine sm, string namePart)
    {
        foreach (ChildAnimatorState s in sm.states)
            if (s.state != null && s.state.name.ToLowerInvariant().Contains(namePart))
                return true;
        foreach (ChildAnimatorStateMachine c in sm.stateMachines)
            if (c.stateMachine != null && (c.stateMachine.name.ToLowerInvariant().Contains(namePart) || HasState(c.stateMachine, namePart)))
                return true;
        return false;
    }

    private static Vector3 FreeSpot(AnimatorStateMachine sm, int row)
    {
        float maxX = 0f;
        foreach (ChildAnimatorState s in sm.states)
            maxX = Mathf.Max(maxX, s.position.x);
        return new Vector3(maxX + 300f, row * 80f, 0f);
    }

    private static void AddSwim(AnimatorStateMachine sm)
    {
        AnimatorState swim = sm.AddState("Swim", FreeSpot(sm, 0));
        AnimatorStateTransition enter = sm.AddAnyStateTransition(swim);
        Configure(enter, 0.2f);
        enter.canTransitionToSelf = false;
        enter.AddCondition(AnimatorConditionMode.If, 0f, "IsSwimming");
        AnimatorStateTransition leave = swim.AddExitTransition();
        Configure(leave, 0.2f);
        leave.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsSwimming");
    }

    private static void AddFall(AnimatorStateMachine sm)
    {
        AnimatorState fall = sm.AddState("Fall", FreeSpot(sm, 1));
        AnimatorStateTransition enter = sm.AddAnyStateTransition(fall);
        Configure(enter, 0.15f);
        enter.canTransitionToSelf = false;
        enter.AddCondition(AnimatorConditionMode.If, 0f, "IsFalling");
        enter.AddCondition(AnimatorConditionMode.Greater, 0.3f, "AirTime");
        enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsSwimming");
        enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        AnimatorStateTransition leave = fall.AddExitTransition();
        Configure(leave, 0.1f);
        leave.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
    }

    private static void AddDeath(AnimatorStateMachine sm)
    {
        AnimatorState death = sm.AddState("Death", FreeSpot(sm, 2));
        AnimatorStateTransition enter = sm.AddAnyStateTransition(death);
        Configure(enter, 0.1f);
        enter.canTransitionToSelf = false;
        enter.AddCondition(AnimatorConditionMode.If, 0f, "DeathTrigger");
        AnimatorStateTransition leave = death.AddExitTransition();
        Configure(leave, 0.2f);
        leave.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
    }

    private static void Configure(AnimatorStateTransition t, float duration)
    {
        t.hasExitTime = false;
        t.duration = duration;
        t.hasFixedDuration = true;
    }
}
#endif
