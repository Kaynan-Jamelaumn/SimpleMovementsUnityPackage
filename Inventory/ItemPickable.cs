using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ItemPickable : Interactable
{
    public ItemSO itemScriptableObject;
    [Tooltip("the quantity the pickable obeject has")] public int quantity = 1;

    [SerializeField] private List<int> durabilityList = new List<int>();
    public List<int> DurabilityList
    {
        get { return durabilityList; }
        set { durabilityList = value; }
    }

    [Tooltip("Turn slowly in place while lying in the world (off for arrows stuck in a wall).")]
    public bool spinInWorld = true;

    /// <summary>
    /// Everything was picked up and the object is on its way out (destroyed or despawned at the end of the frame): it
    /// cannot be picked up again meanwhile (two players asking in the same frame would both get it).
    /// </summary>
    public bool Taken { get; internal set; }

    private float rotationSpeed = 50f;
    //private float bounceHeight = 0.1f;
    //private float bounceSpeed = 1.5f;

    //private Vector3 originalPosition;

    void Start()
    {
        // A networked world item may get its item a moment later (from the server); it sets the time itself then.
        if (itemScriptableObject != null)
            InteractionTime = itemScriptableObject.PickUpTime;
        // Salva a posiзгo original do objeto
        //originalPosition = transform.position;
    }

    void Update()
    {
        // Aplica a rotaзгo ao objeto
        if (spinInWorld)
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

        // Aplica a pulsaзгo (bounce) ao objeto
        //float bounceY = originalPosition.y + Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
        //transform.position = new Vector3(transform.position.x, bounceY, transform.position.z);
    }
}

