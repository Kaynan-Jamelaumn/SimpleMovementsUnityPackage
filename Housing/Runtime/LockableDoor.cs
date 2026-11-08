using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A door only some may open: the tenant of the house or inn room it belongs to and those they let in (Housing decides,
/// through <see cref="AccessCheck"/>; anyone standing inside may always open it to leave). Without a check it is an
/// ordinary door anyone may use.
/// <para>Put it on the door leaf (with a collider, or one on a child). It swings the leaf itself, or drives a
/// <see cref="DoorController"/> on the same object. The server opens and closes it; every client shows the server's door
/// (through a NetInteractable on a NetworkObject above it, or the housing relay when there is none).</para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Housing/Lockable Door")]
public class LockableDoor : MonoBehaviour, IInteractable, IReplicatedInteractable
{
    [Tooltip("Identifies the door across machines and saves. Made automatically; keep it unique.")]
    [SerializeField] private string doorId = "";
    [Tooltip("Its name in the prompt.")]
    [SerializeField] private string displayName = "Door";
    [Tooltip("A Door Controller that swings the leaf (empty: one on this object, else this door swings itself).")]
    [SerializeField] private DoorController door;
    [Tooltip("Degrees the leaf turns about its up axis when open (when it swings itself).")]
    [SerializeField] private float openAngle = 90f;
    [Tooltip("How fast it swings (when it swings itself).")]
    [SerializeField, Min(0.1f)] private float smooth = 4f;
    [Tooltip("How close the player must be, metres.")]
    [SerializeField, Min(0.5f)] private float range = 2.2f;
    [Tooltip("Real seconds after which an open door closes by itself (0 = it stays open).")]
    [SerializeField, Min(0f)] private float autoCloseSeconds = 10f;

    private static readonly Dictionary<string, LockableDoor> byId = new Dictionary<string, LockableDoor>();
    private bool isOpen, started;
    private float closeAt;
    private Quaternion closedRotation;

    /// <summary>
    /// Asked before anyone opens or closes a door (where the rules run: the real check; on a client: the prompt) - a
    /// refusal ("Locked") or null when they may. Null hook: anyone may. Housing installs it.
    /// </summary>
    public static Func<LockableDoor, CombatEntity, string> AccessCheck;

    /// <summary>Raised where the rules run when a door opened or closed (the network layer shows it to every client).</summary>
    public static event Action<LockableDoor> AnyStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        AccessCheck = null;
        AnyStateChanged = null;
        byId.Clear();
    }

    public string DoorId
    {
        get
        {
            if (string.IsNullOrEmpty(doorId)) doorId = Application.isPlaying ? HousingIds.Stable(this, "door") : Guid.NewGuid().ToString("N").Substring(0, 12);
            return doorId;
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Door" : displayName;

    /// <summary>The door with this id on this machine (null when it is not loaded).</summary>
    public static LockableDoor Find(string id) => !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out LockableDoor d) && d != null ? d : null;

    public static IEnumerable<LockableDoor> All => byId.Values;

    public bool IsOpen => door != null ? door.IsOpen : isOpen;

    private void Reset()
    {
        // Reset keeps the id it had (Unity empties the fields first; the editor remembered it).
        doorId = HousingIds.Recall(this, "door") ?? Guid.NewGuid().ToString("N").Substring(0, 12);
        door = GetComponent<DoorController>();
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(doorId)) doorId = Guid.NewGuid().ToString("N").Substring(0, 12);
        HousingIds.Remember(this, "door", doorId);
    }

    private void Awake()
    {
        if (door == null) door = GetComponent<DoorController>();
        closedRotation = transform.localRotation;
        started = true;
    }

    private void OnEnable()
    {
        if (Application.isPlaying) byId[DoorId] = this;
    }

    private void OnDestroy()
    {
        if (byId.TryGetValue(DoorId, out LockableDoor d) && d == this) byId.Remove(DoorId);
    }

    // ------------------------------------------------------------------ the rules (offline, the server)
    /// <summary>Opens or closes the door (where the rules run; on a client it does nothing - the server decides).</summary>
    public void SetOpen(bool open)
    {
        if (!GameNet.NetAuthority.IsServer) return;
        if (IsOpen == open) return;
        Show(open);
        closeAt = open && autoCloseSeconds > 0f ? Time.time + autoCloseSeconds : 0f;
        AnyStateChanged?.Invoke(this);
    }

    /// <summary>A client shows the server's door (no rules).</summary>
    public void Show(bool open)
    {
        if (door != null)
        {
            if (open) door.OpenDoor();
            else door.CloseDoor();
        }
        else isOpen = open;
    }

    /// <summary>The access hook's refusal for <paramref name="player"/>, or null.</summary>
    public string Denied(CombatEntity player)
    {
        Func<LockableDoor, CombatEntity, string> check = AccessCheck;
        if (check == null) return null;
        try
        {
            return check(this, player);
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            return "Locked";
        }
    }

    private void Update()
    {
        if (door == null && started)
        {
            Quaternion target = isOpen ? closedRotation * Quaternion.Euler(0f, openAngle, 0f) : closedRotation;
            if (Quaternion.Angle(transform.localRotation, target) > 0.05f)
                transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * smooth);
        }
        if (closeAt > 0f && Time.time >= closeAt && GameNet.NetAuthority.IsServer)
        {
            closeAt = 0f;
            SetOpen(false);
        }
    }

    // ------------------------------------------------------------------ interaction
    public string InteractionName => DisplayName;
    public string InteractionVerb => IsOpen ? "Close" : "Open";
    public Transform InteractionPoint => transform;
    public float InteractionRange => range;
    public int InteractionPriority => 4;
    public float HoldDuration => 0f;
    public bool AllowsClick => true;

    public bool CanInteract(PlayerInteractor interactor, out string reason)
    {
        reason = "";
        if (!isActiveAndEnabled || interactor == null || interactor.PlayerObject == null) return false;
        string denied = Denied(CombatEntity.Resolve(interactor.PlayerObject));
        if (denied != null)
        {
            reason = denied;
            return false;
        }
        return true;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!GameNet.NetAuthority.IsServer || interactor == null || interactor.PlayerObject == null) return;
        if (Denied(CombatEntity.Resolve(interactor.PlayerObject)) != null) return;
        SetOpen(!IsOpen);
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }

    // ------------------------------------------------------------------ replication (NetInteractable)
    public int ReplicatedState => IsOpen ? 1 : 0;
    public void ApplyReplicatedState(int state) => Show(state != 0);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.7f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
