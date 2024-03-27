using System;
using System.Collections.Generic;
using UnityEngine;

public class Interactable : MonoBehaviour
{
    [SerializeField] float interactionTime;
    public float InteractionTime
    {
        get => interactionTime;
        set => interactionTime = value;
    }

    public virtual void Interact()
    {
    }
}

