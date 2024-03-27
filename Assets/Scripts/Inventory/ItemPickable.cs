
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

    private float rotationSpeed = 50f;
    //private float bounceHeight = 0.1f;
    //private float bounceSpeed = 1.5f;

    //private Vector3 originalPosition;

    void Start()
    {
        InteractionTime = itemScriptableObject.PickUpTime;
        // Salva a posição original do objeto
        //originalPosition = transform.position;
    }

    void Update()
    {
        // Aplica a rotação ao objeto
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

        // Aplica a pulsação (bounce) ao objeto
        //float bounceY = originalPosition.y + Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
        //transform.position = new Vector3(transform.position.x, bounceY, transform.position.z);
    }
}

