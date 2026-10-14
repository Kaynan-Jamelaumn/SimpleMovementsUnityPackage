using GameNet;
using UnityEngine;

/// <summary>
/// Controls the interaction and movement of a door in the game.
/// <para>Network sessions: the server opens and closes the door; with a NetworkObject and a <c>GameNet.NetInteractable</c>
/// on it, every client shows the server's state (<see cref="IReplicatedInteractable"/>). A client's copy of a networked
/// door ignores direct calls - its player's interaction is sent to the server.</para>
/// </summary>
public class DoorController : Interactable, IReplicatedInteractable
{
    [SerializeField] private float openAngle = 90f; // The angle by which the door should rotate when opened
    [SerializeField] private float closeAngle = 0f; // The angle by which the door should rotate when closed
    [SerializeField] private float smooth = 2f; // The smoothness of door movement

    private Quaternion openRotation; // The target rotation for the door when opened
    private Quaternion closeRotation; // The target rotation for the door when closed
    [SerializeField] private bool isOpen = false; // The current state of the door

    /// <summary>
    /// Start is called before the first frame update.
    /// Initializes the open and close rotations for the door.
    /// </summary>
    void Start()
    {
        openRotation = Quaternion.Euler(0, openAngle, 0); // Calculate the open rotation
        closeRotation = Quaternion.Euler(0, closeAngle, 0); // Calculate the close rotation
    }

    /// <summary>
    /// Called when the player interacts with the door.
    /// Toggles the door between open and closed states.
    /// </summary>
    public override void Interact()
    {
        if (!NetAuthority.RunsRules(gameObject))
            return; // a client's copy of a networked door: the server decides
        if (isOpen)
            CloseDoor();
        else
            OpenDoor();
    }

    /// <summary>
    /// Opens the door by setting its state to open.
    /// </summary>
    public void OpenDoor()
    {
        isOpen = true;
    }

    /// <summary>
    /// Closes the door by setting its state to closed.
    /// </summary>
    public void CloseDoor()
    {
        isOpen = false;
    }

    /// <summary>Is the door open (or opening)?</summary>
    public bool IsOpen => isOpen;

    /// <summary>1 = open, 0 = closed.</summary>
    public int ReplicatedState => isOpen ? 1 : 0;

    /// <summary>A client shows the server's door (it swings there smoothly).</summary>
    public void ApplyReplicatedState(int state) => isOpen = state != 0;

    /// <summary>
    /// Update is called once per frame.
    /// Smoothly rotates the door towards its target rotation based on its state.
    /// </summary>
    void Update()
    {
        // Smoothly rotate the door towards its target rotation
        transform.localRotation = Quaternion.Slerp(transform.localRotation, isOpen ? openRotation : closeRotation, Time.deltaTime * smooth);
    }
}
