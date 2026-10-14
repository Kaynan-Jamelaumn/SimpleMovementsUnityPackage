using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Keeps the player's CharacterController from bumping into the player's own colliders, and says in the Console why the
/// player does not move when it should. Added by the <see cref="MovementStateMachine"/> next to the CharacterController.
/// </summary>
/// <remarks>
/// <para>Own colliders: weapons and shields in the hands, body-part hit boxes on the bones, equipment. When one is
/// solid (not a trigger) the controller collides with it. In single player that is a bump at most, but in a network
/// session the model (and everything in its hands) is smoothed between ticks and trails the body slightly, so walking
/// forward runs into it every step: the player does not walk forward and shakes. Every own collider is ignored by the
/// controller from the start, and any the controller touches later (an item equipped afterwards) as soon as it does.</para>
/// <para>Diagnostics, on the machine that controls the player: when a move key is held but the Movement action reads
/// nothing, and when the player is asked to move but stays in place for a moment, a warning names the reason (frozen,
/// unable to move, what it hit, a Rigidbody or NetworkTransform fighting the controller).</para>
/// </remarks>
[DisallowMultipleComponent]
[AddComponentMenu("")]
public class MovementGuard : MonoBehaviour
{
    private const float StuckSeconds = 0.5f;
    private const float RepeatSeconds = 8f;

    private MovementStateMachine machine;
    private CharacterController controller;
    private Transform playerRoot;
    private Collider lastHit;
    private Vector3 lastHitNormal;
    private float lastHitTime = float.NegativeInfinity;
    private float stuckTime;
    private float keyMismatchTime;
    private float nextStuckReport;
    private float nextKeyReport;
    private Collider[] own = new Collider[0];
    private float ownListedAt = float.NegativeInfinity;

    /// <summary>Warnings in the Console (the own-collider fix works either way).</summary>
    public bool LogWarnings { get; set; } = true;

    /// <summary>Adds (or finds) the guard on the controller's object.</summary>
    public static MovementGuard Attach(MovementStateMachine machine, CharacterController controller)
    {
        if (machine == null || controller == null)
            return null;
        MovementGuard guard = controller.GetComponent<MovementGuard>();
        if (guard == null)
            guard = controller.gameObject.AddComponent<MovementGuard>();
        guard.Initialize(machine, controller);
        return guard;
    }

    private void Initialize(MovementStateMachine owner, CharacterController cc)
    {
        machine = owner;
        controller = cc;
        playerRoot = CommonRoot(cc.transform, owner.transform);
        IgnoreOwnColliders();
    }

    /// <summary>Makes the controller ignore every solid collider of the player (lists them again after equipping something).</summary>
    public void IgnoreOwnColliders()
    {
        if (controller == null || playerRoot == null)
            return;
        own = playerRoot.GetComponentsInChildren<Collider>(true);
        ownListedAt = Time.time;
        Reapply();
    }

    /// <summary>
    /// Ignores the listed own colliders again. Unity forgets an ignored pair when either collider is switched off, and
    /// a network correction switches the controller off and on, so the machine calls this after each one.
    /// </summary>
    public void Reapply()
    {
        if (Time.time - ownListedAt > 2f)
        {
            IgnoreOwnColliders();
            return;
        }
        for (int i = 0; i < own.Length; i++)
            Ignore(own[i]);
    }

    private void Ignore(Collider c)
    {
        // Both must be active: Unity refuses (and logs an error) otherwise.
        if (c == null || c == controller || c.isTrigger || controller == null || !c.enabled || !c.gameObject.activeInHierarchy
            || !controller.enabled || !controller.gameObject.activeInHierarchy)
            return;
        Physics.IgnoreCollision(controller, c, true);
    }

    private bool IsOwn(Collider c) => c != null && playerRoot != null && (c.transform == playerRoot || c.transform.IsChildOf(playerRoot));

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider == null)
            return;
        if (IsOwn(hit.collider))
        {
            // Something equipped after the start: ignore it from now on.
            Ignore(hit.collider);
            return;
        }
        if (hit.normal.y > 0.7f)
            return; // the ground
        lastHit = hit.collider;
        lastHitNormal = hit.normal;
        lastHitTime = Time.time;
    }

    /// <summary>
    /// Called by the machine after each (non-replayed) step: what was asked, where the player was before and after.
    /// </summary>
    public void Observe(in MovementInput asked, Vector3 before, Vector3 after, float dt, string state, bool frozen, bool canMove, float speed)
    {
        if (!LogWarnings || dt <= 0f || machine == null || !machine.DeviceInputEnabled)
            return;
        CheckKeys(asked, dt);

        Vector3 delta = after - before;
        delta.y = 0f;
        float expected = asked.Move.magnitude * Mathf.Max(0f, speed) * dt;
        if (expected < 0.005f || delta.magnitude > expected * 0.2f)
        {
            stuckTime = 0f;
            return;
        }
        stuckTime += dt;
        if (stuckTime < StuckSeconds || Time.time < nextStuckReport)
            return;
        nextStuckReport = Time.time + RepeatSeconds;
        Debug.LogWarning(StuckReport(asked, state, frozen, canMove, speed), this);
    }

    // A move key held while the Movement action reads nothing: the binding is wrong or something else takes the key.
    private void CheckKeys(in MovementInput asked, float dt)
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;
        bool w = kb.wKey.isPressed, s = kb.sKey.isPressed, a = kb.aKey.isPressed, d = kb.dKey.isPressed;
        bool mismatch = (w && !s && asked.Move.y <= 0.01f) || (s && !w && asked.Move.y >= -0.01f)
                        || (d && !a && asked.Move.x <= 0.01f) || (a && !d && asked.Move.x >= -0.01f);
        if (!mismatch)
        {
            keyMismatchTime = 0f;
            return;
        }
        keyMismatchTime += dt;
        if (keyMismatchTime < 0.3f || Time.time < nextKeyReport)
            return;
        nextKeyReport = Time.time + RepeatSeconds;
        string keys = (w ? "W" : "") + (a ? "A" : "") + (s ? "S" : "") + (d ? "D" : "");
        Debug.LogWarning($"[Movement] '{playerRoot.name}': {keys} is held but the Movement action reads ({asked.Move.x:0.##}, {asked.Move.y:0.##}).\n" +
                         DescribeMovementBindings(kb, w ? kb.wKey : s ? kb.sKey : a ? kb.aKey : kb.dKey), this);
    }

    // What the Movement action is really bound to on this machine: the bindings after saved overrides, whether the held
    // key is one of its controls, and the other enabled actions that use that key.
    private string DescribeMovementBindings(Keyboard kb, UnityEngine.InputSystem.Controls.KeyControl held)
    {
        var sb = new StringBuilder();
        InputAction action = machine != null ? machine.MovementAction : null;
        if (action == null)
            return "The Movement action was not found.";
        bool bound = false;
        foreach (InputControl c in action.controls)
            if (c == held)
                bound = true;
        sb.Append(bound
            ? $"'{held.displayName}' IS one of the Movement controls, but its value does not arrive"
            : $"'{held.displayName}' is NOT bound to the Movement action on this machine");
        sb.Append($" (action enabled: {action.enabled}, gameplay input paused: {InputBindingStore.GameplayInputPaused}).\nMovement bindings:");
        bool overridden = false;
        foreach (InputBinding b in action.bindings)
        {
            sb.Append("\n  ").Append(b.isPartOfComposite ? "    " + b.name + ": " : b.isComposite ? b.name + " (composite)" : "binding: ");
            if (!b.isComposite)
                sb.Append(string.IsNullOrEmpty(b.effectivePath) ? "<none>" : b.effectivePath);
            if (!string.IsNullOrEmpty(b.overridePath) && b.overridePath != b.path)
            {
                overridden = true;
                sb.Append($"   <- SAVED OVERRIDE (the asset says {b.path})");
            }
        }
        foreach (InputAction other in InputSystem.ListEnabledActions())
        {
            if (other == null || other == action)
                continue;
            foreach (InputControl c in other.controls)
                if (c == held)
                {
                    sb.Append($"\nAlso used by the enabled action '{other.actionMap?.name}/{other.name}'.");
                    break;
                }
        }
        if (overridden || !string.IsNullOrEmpty(InputBindingStore.SavedJson))
            sb.Append("\nSaved key bindings are applied on top of your Input Actions asset: reset them in the pause menu's Key Bindings, " +
                      "or with Tools ▸ SimpleMovements ▸ Input ▸ Reset Saved Key Bindings.");
        if (!bound)
            sb.Append("\nIf you changed the asset, make sure its C# class is regenerated (the asset's inspector: Generate C# Class, then Apply): " +
                      "the movement uses the generated PlayerInput class, not the asset file.");
        return sb.ToString();
    }

    private string StuckReport(in MovementInput asked, string state, bool frozen, bool canMove, float speed)
    {
        var sb = new StringBuilder($"[Movement] '{playerRoot.name}' is asked to move ({asked.Move.x:0.##}, {asked.Move.y:0.##}) but stays in place (state {state}, speed {speed:0.##}). ");
        if (frozen)
            sb.Append("The movement is FROZEN (a control lock: travel, trading, a menu, a cutscene...). ");
        if (!canMove)
            sb.Append("The Availability State Machine says the player CANNOT MOVE (stunned, casting, dead...). ");
        if (controller == null || !controller.enabled || !controller.gameObject.activeInHierarchy)
            sb.Append("The CharacterController is switched off. ");
        if (lastHit != null && Time.time - lastHitTime < 0.5f)
            sb.Append($"It is running into '{Path(lastHit.transform)}' ({lastHit.GetType().Name}, normal {lastHitNormal}). ");
        Rigidbody rb = playerRoot.GetComponentInChildren<Rigidbody>();
        if (rb != null && !rb.isKinematic)
            sb.Append($"A non-kinematic Rigidbody on '{rb.name}' fights the CharacterController: make it kinematic. ");
        foreach (MonoBehaviour b in playerRoot.GetComponentsInChildren<MonoBehaviour>(true))
            if (b != null && b.enabled && b.GetType().Name == "NetworkTransform")
                sb.Append($"A NetworkTransform on '{b.name}' moves the player too: remove it (the predicted movement synchronizes the player). ");
        if (speed <= 0.01f)
            sb.Append("Its speed is 0 (Speed Manager / weight). ");
        return sb.ToString();
    }

    private static string Path(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent)
            path = p.name + "/" + path;
        return path;
    }

    private static Transform CommonRoot(Transform a, Transform b)
    {
        if (b.IsChildOf(a))
            return a;
        if (a.IsChildOf(b))
            return b;
        for (Transform p = a.parent; p != null; p = p.parent)
            if (b.IsChildOf(p))
                return p;
        return a;
    }
}
