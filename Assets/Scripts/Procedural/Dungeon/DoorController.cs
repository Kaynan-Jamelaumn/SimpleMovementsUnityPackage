using UnityEngine;

public class DoorController : Interactable
{
    [SerializeField] private float openAngle = 90f; // The angle by which the door should rotate when opened
    [SerializeField] private float closeAngle = 0f; // The angle by which the door should rotate when closed
    [SerializeField] private float smooth = 2f; // The smoothness of door movement

    private Quaternion openRotation;
    private Quaternion closeRotation;
    [SerializeField] private bool isOpen = false;

    void Start()
    {
        openRotation = Quaternion.Euler(0, openAngle, 0); // Calculate the open rotation
        closeRotation = Quaternion.Euler(0, closeAngle, 0); // Calculate the close rotation
    }

    public override void Interact()
    {
        if (isOpen)
            CloseDoor();
        else
            OpenDoor();
    }

    public void OpenDoor()
    {
        isOpen = true;
    }

    public void CloseDoor()
    {
        isOpen = false;
    }

    void Update()
    {
        // Smoothly rotate the door towards its target rotation
        transform.localRotation = Quaternion.Slerp(transform.localRotation, isOpen ? openRotation : closeRotation, Time.deltaTime * smooth);
    }
}
