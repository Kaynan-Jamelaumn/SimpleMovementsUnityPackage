using UnityEngine;

/// <summary>
/// Presentation only: makes a mob's model move like its body would. It squashes on landing and stretches while rising
/// or falling fast (a slime's bounce, a frog's leap, a rabbit's hops), and, while flying or swimming, banks into its
/// turns and pitches its nose up as it climbs or down as it dives.
/// <para>Put it on the mob's MODEL (a child of the mob), never on the root: the root carries the NavMeshAgent, the
/// colliders and the network transform. It reads only how the mob is seen to move, so it looks the same on the server
/// and on every client, and sends nothing.</para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Mobs/Mob Body Motion")]
public class MobBodyMotion : MonoBehaviour
{
    [Tooltip("Squash and stretch with the body's vertical motion (slimes, hoppers). 0 = off, 1 = rubbery.")]
    [Range(0f, 1f)] public float squashAndStretch = 0.5f;
    [Tooltip("Bank into turns while flying or swimming (degrees at a fast turn). 0 = off.")]
    [Range(0f, 60f)] public float bank = 25f;
    [Tooltip("Pitch the nose up when climbing and down when diving, while flying or swimming (degrees). 0 = off.")]
    [Range(0f, 60f)] public float pitch = 20f;
    [Tooltip("How quickly the model follows (higher = snappier).")]
    [Min(1f)] public float responsiveness = 8f;

    private Transform root;
    private MobMovementStateMachine machine;
    private Vector3 baseScale;
    private Quaternion baseRotation;
    private Vector3 lastPosition;
    private float lastYaw;
    private float verticalSpeed;
    private float landing;
    private float roll, nose;
    private bool warned;

    private void Awake()
    {
        machine = GetComponentInParent<MobMovementStateMachine>();
        root = machine != null ? machine.transform : transform.parent;
        baseScale = transform.localScale;
        baseRotation = transform.localRotation;
        if (root != null)
        {
            lastPosition = root.position;
            lastYaw = root.eulerAngles.y;
        }
    }

    private void OnDisable()
    {
        transform.localScale = baseScale;
        transform.localRotation = baseRotation;
    }

    private void LateUpdate()
    {
        if (root == null || root == transform)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning($"[{name}] Mob Body Motion belongs on the mob's model (a child of the mob), not on the mob itself.", this);
            }
            return;
        }
        float dt = Time.deltaTime;
        if (dt <= 1e-5f)
            return;

        Vector3 position = root.position;
        Vector3 delta = position - lastPosition;
        lastPosition = position;
        float yaw = root.eulerAngles.y;
        float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
        lastYaw = yaw;
        if (delta.sqrMagnitude > 25f)
            delta = Vector3.zero; // a teleport, a respawn

        // Squash on landing: the body was falling and has stopped.
        float vy = delta.y / dt;
        if (verticalSpeed < -1f && vy > -0.2f)
            landing = Mathf.Max(landing, Mathf.Clamp(-verticalSpeed * 0.08f, 0f, 0.35f));
        verticalSpeed = vy;
        landing *= Mathf.Exp(-dt * 10f);
        float stretch = 1f + Mathf.Clamp(Mathf.Abs(vy) * 0.06f, 0f, 0.3f);
        float s = Mathf.Lerp(1f, stretch * (1f - landing), squashAndStretch);
        float side = 1f / Mathf.Sqrt(Mathf.Max(0.2f, s)); // keeps its volume
        transform.localScale = Vector3.Scale(baseScale, new Vector3(side, s, side));

        // Banking and pitching, only off the ground.
        bool off = machine != null && machine.IsOffTheGround;
        float flat = new Vector2(delta.x, delta.z).magnitude / dt;
        float wantRoll = off ? -Mathf.Clamp(yawRate / 180f, -1f, 1f) * bank : 0f;
        float wantNose = off ? -Mathf.Clamp(vy / Mathf.Max(1f, flat), -1f, 1f) * pitch : 0f;
        float k = 1f - Mathf.Exp(-responsiveness * dt);
        roll = Mathf.Lerp(roll, wantRoll, k);
        nose = Mathf.Lerp(nose, wantNose, k);
        transform.localRotation = baseRotation * Quaternion.Euler(nose, 0f, roll);
    }
}
