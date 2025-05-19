using UnityEngine;
using static GenericMethods;

/// <summary>
/// Base class for interactable objects in the game.
/// </summary>
public class Interactable : MonoBehaviour
{
    [SerializeField] private float interactionTime; // The time required for the interaction
    [SerializeField] private bool hasRandomInteractionTime; // Indicates if the interaction time should be randomized
    [SerializeField] private float minInteractionTime; // Minimum interaction time when randomized
    [SerializeField] private float maxInteractionTime; // Maximum interaction time when randomized

    /// <summary>
    /// Gets or sets the interaction time.
    /// </summary>
    public float InteractionTime
    {
        get => interactionTime;
        set => interactionTime = value;
    }

    /// <summary>
    /// This method is called when the player interacts with the object.
    /// Should be overridden in derived classes.
    /// </summary>
    public virtual void Interact()
    {
    }

    /// <summary>
    /// Awake is called when the script instance is being loaded.
    /// Initializes the interaction time if it should be randomized.
    /// </summary>
    private void Awake()
    {
        if (hasRandomInteractionTime)
            interactionTime = GenericMethods.GetRandomValue(interactionTime, true, minInteractionTime, maxInteractionTime);
    }
}
