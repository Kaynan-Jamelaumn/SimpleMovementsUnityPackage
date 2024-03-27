using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollectableItem : ItemSpawnner
{
    [SerializeField] public ToolType toolTypeRequired;
    [SerializeField] private float health;
    //[SerializeField] private ItemSpawnner itemSpawnner = new ItemSpawnner();
    //[SerializeField] private List<ItemDrop> itemDrops = new List<ItemDrop>();


    [Header("Animation")]
    [SerializeField] protected AnimationClip useAnimation;

    [Header("Audio")]
    [SerializeField] protected AudioClip useAudioClip;
    [Header("Particles")]
    [SerializeField] protected ParticleSystem useParticles;
    //void Awake()
    //{
    //    itemSpawnner.
    //}
    public void TakeDamage(float damage)
    {
        health -= damage;
        InteractionEffects.ApplyEffects(gameObject, useAnimation, useAudioClip, useParticles);

        if (health <= 0)
        {
            //itemSpawnner.
            SpawnItem(transform.position);
            Destroy(gameObject);
        }
    }

}
