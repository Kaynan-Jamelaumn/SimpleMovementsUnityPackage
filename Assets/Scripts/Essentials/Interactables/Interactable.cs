using UnityEngine;
using static GenericMethods;
public class Interactable : MonoBehaviour
{
    [SerializeField] private float interactionTime;

    [SerializeField] private bool hasRandomInteractionTime;
    [SerializeField] private float minInteractionTime;
    [SerializeField] private float maxInteractionTime;

    public float InteractionTime
    {
        get => interactionTime;
        set => interactionTime = value;
    }

    public virtual void Interact()
    {
    }
    private void Awake()
    {
        if (hasRandomInteractionTime)
            interactionTime = GenericMethods.GetRandomValue(interactionTime, true, minInteractionTime, maxInteractionTime);

    }
}

