using UnityEngine;

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
            interactionTime = Random.Range(minInteractionTime, maxInteractionTime);
        
    }
}

