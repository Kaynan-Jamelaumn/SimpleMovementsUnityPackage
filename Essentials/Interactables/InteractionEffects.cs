using UnityEngine;

/// <summary>
/// Static class that provides methods to apply various interaction effects to a GameObject.
/// </summary>
public static class InteractionEffects
{
    /// <summary>
    /// Applies animation, audio, and particle effects to the specified GameObject.
    /// </summary>
    /// <param name="gameObject">The GameObject to apply the effects to.</param>
    /// <param name="useAnimation">The animation clip to play.</param>
    /// <param name="useAudioClip">The audio clip to play.</param>
    /// <param name="useParticles">The particle system to play.</param>
    public static void ApplyEffects(GameObject gameObject, AnimationClip useAnimation, AudioClip useAudioClip, ParticleSystem useParticles)
    {
        ApplyAnimation(gameObject, useAnimation);
        ApplyAudio(gameObject, useAudioClip);
        ApplyParticles(gameObject, useParticles);
    }

    /// <summary>
    /// Applies an animation effect to the specified GameObject.
    /// </summary>
    /// <param name="gameObject">The GameObject to apply the animation to.</param>
    /// <param name="useAnimation">The animation clip to play.</param>
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

    /// <summary>
    /// Applies an audio effect to the specified GameObject.
    /// </summary>
    /// <param name="gameObject">The GameObject to apply the audio to.</param>
    /// <param name="useAudioClip">The audio clip to play.</param>
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

    /// <summary>
    /// Applies a particle effect to the specified GameObject.
    /// </summary>
    /// <param name="gameObject">The GameObject to apply the particles to.</param>
    /// <param name="useParticles">The particle system to play.</param>
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
