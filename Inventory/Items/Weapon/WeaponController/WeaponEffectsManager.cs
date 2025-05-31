using System.Collections;
using UnityEngine;

public class WeaponEffectsManager
{
    private WeaponController controller;

    public WeaponEffectsManager(WeaponController controller)
    {
        this.controller = controller;
    }

    public void PlayHitEffects(IAttackComponent component, Vector3 hitPosition)
    {
        if (component.AttackParticles != null)
        {
            var particleInstance = Object.Instantiate(component.AttackParticles, hitPosition, Quaternion.identity);
            particleInstance.Play();
        }

        if (component.TrailEffect != null)
        {
            var trailInstance = Object.Instantiate(component.TrailEffect, controller.HandGameObject.transform);
            controller.StartCoroutine(DestroyAfterDelay(trailInstance, 2f));
        }
    }

    public void PlayComboFinisherEffects(GameObject player, ComboSequence combo)
    {
        if (combo.comboFinisherSound != null)
        {
            player.GetComponent<Player>()?.PlayerAudioSource?.PlayOneShot(combo.comboFinisherSound);
        }

        if (combo.comboFinisherParticles != null)
        {
            var particles = Object.Instantiate(combo.comboFinisherParticles, controller.HandGameObject.transform);
            particles.Play();
        }
    }

    private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null) Object.Destroy(obj);
    }
}