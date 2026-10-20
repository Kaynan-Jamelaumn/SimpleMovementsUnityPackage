using UnityEngine;

/// <summary>
/// A place at a <see cref="CardTable"/> (a bench, a chair). Players use it with Interact ("[E] Sit  Truco · 10 gold
/// each"): they sit at <see cref="SitPoint"/>, facing the table, and the table's window opens. Put it on the bench (it
/// needs a collider, or one on a child, for the Player Interactor to find it) under the table; the seat order goes
/// clockwise around the table, and in Truco teammates sit opposite each other (seats 1 + 3, 2 + 4).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Minigames/Card Seat")]
public class CardSeat : MonoBehaviour, IInteractable
{
    [Tooltip("Position of this seat around the table, clockwise from 0. -1 = its order among the table's seats.")]
    [SerializeField] private int seatIndex = -1;
    [Tooltip("Where the sitter's feet are (on the floor in front of the bench, facing the table): the Animator's Sit state " +
             "(IsSitting) lowers the hips onto the bench. Empty = this object.")]
    [SerializeField] private Transform sitPoint;
    [Tooltip("Where the player stands up to (empty = a step back from the sit point).")]
    [SerializeField] private Transform standPoint;
    [Tooltip("Where this seat holds its cards when the player has no hand to hold them (or a bot without a body). Empty = " +
             "in front of the sitter's chest.")]
    [SerializeField] private Transform handPoint;
    [Tooltip("How close the player must be to sit (metres).")]
    [SerializeField, Min(0.5f)] private float range = 2.2f;

    private CardTable table;

    public CardTable Table => table != null ? table : (table = GetComponentInParent<CardTable>());
    public int SeatIndex { get => seatIndex; internal set => seatIndex = value; }
    public Transform SitPoint => sitPoint != null ? sitPoint : transform;

    /// <summary>The pose the player sits in.</summary>
    public Pose SitPose
    {
        get
        {
            Transform t = SitPoint;
            Vector3 fwd = Facing(t.position);
            return new Pose(t.position, Quaternion.LookRotation(fwd, Vector3.up));
        }
    }

    /// <summary>Where the player stands when they leave.</summary>
    public Pose StandPose
    {
        get
        {
            if (standPoint != null)
                return new Pose(standPoint.position, standPoint.rotation);
            Vector3 fwd = Facing(SitPoint.position);
            return new Pose(SitPoint.position - fwd * 0.75f, Quaternion.LookRotation(-fwd, Vector3.up));
        }
    }

    /// <summary>Where cards are held without a hand (in front of the sitter's chest).</summary>
    public Pose HandPose
    {
        get
        {
            if (handPoint != null)
                return new Pose(handPoint.position, handPoint.rotation);
            Vector3 fwd = Facing(SitPoint.position);
            return new Pose(SitPoint.position + Vector3.up * 1.05f + fwd * 0.3f, Quaternion.LookRotation(fwd, Vector3.up));
        }
    }

    /// <summary>Flat direction from the seat toward the table's centre.</summary>
    public Vector3 Facing(Vector3 from)
    {
        CardTable t = Table;
        Vector3 d = t != null ? t.Center.position - from : SitPoint.forward;
        d.y = 0f;
        return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
    }

    // ------------------------------------------------------------------ interaction
    public string InteractionName
    {
        get
        {
            CardTable t = Table;
            return t != null ? t.PromptName : name;
        }
    }

    public string InteractionVerb => "Sit";
    public Transform InteractionPoint => SitPoint;
    public float InteractionRange => range;
    public int InteractionPriority => 6;
    public float HoldDuration => 0f;
    public bool AllowsClick => true;

    public bool CanInteract(PlayerInteractor interactor, out string reason)
    {
        reason = "";
        CardTable t = Table;
        CombatEntity player = PlayerOf(interactor);
        if (t == null || player == null)
            return false;
        return t.CanSit(SeatIndex, player, out reason);
    }

    public void Interact(PlayerInteractor interactor)
    {
        CardTable t = Table;
        CombatEntity player = PlayerOf(interactor);
        if (t == null || player == null)
            return;
        if (!t.TrySit(SeatIndex, player, out string reason) && !string.IsNullOrEmpty(reason))
            t.Tell(player, reason);
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }

    private static CombatEntity PlayerOf(PlayerInteractor interactor) =>
        interactor != null && interactor.PlayerObject != null ? CombatEntity.Resolve(interactor.PlayerObject) : null;

    private void OnDrawGizmos()
    {
        Pose p = SitPose;
        Gizmos.color = new Color(0.3f, 0.85f, 0.5f, 0.8f);
        Gizmos.DrawWireCube(p.position + Vector3.up * 0.45f, new Vector3(0.45f, 0.9f, 0.45f));
        Gizmos.DrawLine(p.position + Vector3.up * 0.9f, p.position + Vector3.up * 0.9f + p.forward * 0.5f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(p.position + Vector3.up * 1.3f, $"Seat {SeatIndex + 1}");
#endif
    }
}
