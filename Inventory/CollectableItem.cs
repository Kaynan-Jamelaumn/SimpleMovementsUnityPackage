using UnityEngine;

/// <summary>
/// A harvestable object (tree, rock, bush): hit it with a weapon of the required tool type to damage it; when its
/// health runs out it drops its items and disappears.
/// </summary>
public class CollectableItem : ItemSpawner, IWeaponHittable
{
    [Tooltip("Tool type needed to harvest it (None = any weapon).")]
    [SerializeField] public ToolType toolTypeRequired;
    [Tooltip("Tool damage needed to harvest it.")]
    [SerializeField] private float health;

    [Header("Animation")]
    [SerializeField] protected AnimationClip useAnimation;

    [Header("Audio")]
    [SerializeField] protected AudioClip useAudioClip;
    [Header("Particles")]
    [SerializeField] protected ParticleSystem useParticles;

    public float Health => health;

    public void TakeDamage(float damage)
    {
        if (health <= 0f)
            return;
        health -= damage;
        InteractionEffects.ApplyEffects(gameObject, useAnimation, useAudioClip, useParticles);

        if (health <= 0)
        {
            SpawnItem(transform.position);
            Destroy(gameObject);
        }
    }

    /// <summary>Weapons harvest it when their tool type matches (their Tool Damage is used).</summary>
    public bool OnWeaponHit(in WeaponHitInfo hit)
    {
        if (hit.weapon == null)
            return false;
        if (toolTypeRequired != ToolType.None && hit.weapon.ToolType != toolTypeRequired)
            return false;
        float damage = hit.weapon.ToolDamage > 0f ? hit.weapon.ToolDamage : 0f;
        if (damage <= 0f)
            return false;
        TakeDamage(damage);
        return true;
    }
}
