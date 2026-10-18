using UnityEngine;

/// <summary>
/// A place to sleep: a bed, a bench, a bedroll. Players use it with Interact ("[E] Sleep  Bed") when it is free; the
/// sleeper lies at <see cref="sleepPoint"/> and the spot stays occupied until they wake up. The kind decides the sleep
/// quality (set on the player's Sleep Manager), or give the spot its own quality (Custom).
/// <para>Put it on the bed object (it needs a collider, or one on a child, for the Player Interactor to find it).
/// Network sessions: the interaction runs on the server, which decides who sleeps where.</para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Sleep Spot")]
public class SleepSpot : MonoBehaviour, IInteractable
{
    [Tooltip("What kind of place this is (its sleep quality comes from the sleeper's Sleep Manager). Custom = the quality below.")]
    [SerializeField] private SleepSpotKind kind = SleepSpotKind.Bed;
    [Tooltip("Custom only: the sleep quality of this spot.")]
    [SerializeField] private SleepQuality customQuality = new SleepQuality("Custom", 1f);
    [Tooltip("Name in the prompt (empty = the object's name).")]
    [SerializeField] private string displayName = "";
    [Tooltip("Where the sleeper lies (position and facing). Empty = the sleeper stays where they are.")]
    [SerializeField] private Transform sleepPoint;
    [Tooltip("How close the player must be to use it (metres).")]
    [SerializeField, Min(0.5f)] private float range = 2.5f;
    [Tooltip("Seconds Interact must be held (0 = a press).")]
    [SerializeField, Min(0f)] private float holdSeconds = 0.4f;
    [Tooltip("Only this many sleepers at once (1 = a bed for one).")]
    [SerializeField, Min(1)] private int capacity = 1;

    private readonly System.Collections.Generic.List<CombatEntity> occupants = new System.Collections.Generic.List<CombatEntity>();

    public SleepSpotKind Kind => kind;
    /// <summary>Is there room for one more sleeper?</summary>
    public bool IsFree
    {
        get
        {
            occupants.RemoveAll(o => o == null || !o.IsSleeping);
            return occupants.Count < capacity;
        }
    }

    /// <summary>The quality of this spot for <paramref name="sleep"/>.</summary>
    public SleepQuality QualityFor(SleepManager sleep) => kind == SleepSpotKind.Custom || sleep == null ? customQuality : sleep.QualityFor(kind);

    /// <summary>Takes a place (called by the Sleep Manager). False when full.</summary>
    public bool Occupy(CombatEntity sleeper)
    {
        if (sleeper == null) return true;
        if (occupants.Contains(sleeper)) return true;
        if (!IsFree) return false;
        occupants.Add(sleeper);
        if (sleepPoint != null)
            HubWorld.RealmTravel.Place(sleeper, new Pose(sleepPoint.position, sleepPoint.rotation));
        return true;
    }

    /// <summary>Frees the place (called by the Sleep Manager when the sleeper wakes up).</summary>
    public void Release(CombatEntity sleeper) => occupants.Remove(sleeper);

    // ------------------------------------------------------------------ interaction
    public string InteractionName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public string InteractionVerb => "Sleep";
    public Transform InteractionPoint => transform;
    public float InteractionRange => range;
    public int InteractionPriority => 5;
    public float HoldDuration => holdSeconds;
    public bool AllowsClick => true;

    public bool CanInteract(PlayerInteractor interactor, out string reason)
    {
        reason = "";
        SleepManager sleep = SleepOf(interactor);
        if (sleep == null)
            return false;
        if (sleep.IsSleeping) return false;
        if (!IsFree) { reason = "Occupied"; return false; }
        return sleep.CanSleepNow(out reason);
    }

    public void Interact(PlayerInteractor interactor)
    {
        SleepManager sleep = SleepOf(interactor);
        if (sleep == null || !IsFree)
            return;
        sleep.StartSleeping(QualityFor(sleep), this);
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }

    private static SleepManager SleepOf(PlayerInteractor interactor)
    {
        if (interactor == null || interactor.PlayerObject == null)
            return null;
        return interactor.PlayerObject.GetComponentInChildren<SleepManager>();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.6f, 1f, 0.6f);
        Transform p = sleepPoint != null ? sleepPoint : transform;
        Gizmos.DrawWireCube(p.position + Vector3.up * 0.25f, new Vector3(0.9f, 0.5f, 2f));
        Gizmos.DrawLine(p.position, p.position + p.forward);
    }
}
