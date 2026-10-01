using UnityEngine;

/// <summary>Visual and sound feedback of weapon attacks: trails, hit particles, combo effects.</summary>
public class WeaponEffectsManager
{
    private readonly WeaponController controller;
    private GameObject trail;

    public WeaponEffectsManager(WeaponController controller)
    {
        this.controller = controller;
    }

    /// <summary>Attaches the attack's trail to the weapon hand for the duration of the attack.</summary>
    public void StartTrail(IAttackComponent component)
    {
        StopTrail();
        if (component?.TrailEffect == null || controller.HandTransform == null)
            return;
        trail = Object.Instantiate(component.TrailEffect, controller.HandTransform, false);
    }

    public void StopTrail()
    {
        if (trail == null)
            return;
        // Let particle trails fade out instead of popping.
        foreach (ParticleSystem ps in trail.GetComponentsInChildren<ParticleSystem>())
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        trail.transform.SetParent(null, true);
        Object.Destroy(trail, 1.5f);
        trail = null;
    }

    public void PlayHitEffects(IAttackComponent component, Vector3 hitPosition)
    {
        if (component?.AttackParticles != null)
            PlayParticles(component.AttackParticles, hitPosition, Quaternion.identity, null);
    }

    public void PlayElementParticles(ParticleSystem particles, Vector3 position)
    {
        if (particles != null)
            PlayParticles(particles, position, Quaternion.identity, null);
    }

    public void PlayComboFinisherEffects(GameObject player, ComboSequence combo)
    {
        if (combo == null) return;
        controller.PlaySound(combo.comboFinisherSound);
        if (combo.comboFinisherParticles != null)
            PlayParticles(combo.comboFinisherParticles, controller.HandTransform != null ? controller.HandTransform.position : controller.transform.position, Quaternion.identity, controller.HandTransform);
    }

    public void PlayBranchEffects(GameObject player, ComboBranch branch)
    {
        if (branch == null) return;
        controller.PlaySound(branch.branchSound);
        if (branch.branchParticles != null)
            PlayParticles(branch.branchParticles, controller.HandTransform != null ? controller.HandTransform.position : controller.transform.position, Quaternion.identity, controller.HandTransform);
    }

    /// <summary>Spawns a particle system and removes it when it is done.</summary>
    public static void PlayParticles(ParticleSystem prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        ParticleSystem instance = parent != null ? Object.Instantiate(prefab, parent) : Object.Instantiate(prefab, position, rotation);
        instance.Play();
        var main = instance.main;
        float life = main.loop ? 3f : main.duration + main.startLifetime.constantMax;
        Object.Destroy(instance.gameObject, Mathf.Max(0.5f, life + 0.5f));
    }
}
