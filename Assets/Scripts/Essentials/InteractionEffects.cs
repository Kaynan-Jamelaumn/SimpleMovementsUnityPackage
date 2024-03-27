using UnityEngine;

public static class InteractionEffects
{
    public static void ApplyEffects(GameObject gameObject, AnimationClip useAnimation, AudioClip useAudioClip, ParticleSystem useParticles)
    {
        ApplyAnimation(gameObject, useAnimation);
        ApplyAudio(gameObject, useAudioClip);
        ApplyParticles(gameObject, useParticles);
    }

    private static void ApplyAnimation(GameObject gameObject, AnimationClip useAnimation)
    {
        if (useAnimation != null)
        {
            Animation animation = gameObject.GetComponent<Animation>();
            if (animation == null)
            {
                animation = gameObject.AddComponent<Animation>();
            }
            animation.Play(useAnimation.name);
        }
    }

    private static void ApplyAudio(GameObject gameObject, AudioClip useAudioClip)
    {
        if (useAudioClip != null)
        {
            AudioSource audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.clip = useAudioClip;
            audioSource.Play();
        }
    }

    private static void ApplyParticles(GameObject gameObject, ParticleSystem useParticles)
    {
        if (useParticles != null)
        {
            ParticleSystem prefabParticles = gameObject.GetComponent<ParticleSystem>();
            if (prefabParticles == null)
            {
                prefabParticles = gameObject.AddComponent<ParticleSystem>();
            }
            prefabParticles.Play();
        }
    }
}
