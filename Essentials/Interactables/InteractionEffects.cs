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
        if (useAnimation == null) return;

        // First try to use the modern Animator system
        var animController = gameObject.GetComponent<PlayerAnimationController>();
        if (animController != null)
        {
            // Use the PlayerAnimationController for consistent animation handling
            animController.PlayAnimation(useAnimation);
            return;
        }

        // Fallback to legacy Animation component (for non-player objects)
        Animation animation = gameObject.GetComponent<Animation>();
        if (animation == null)
        {
            animation = gameObject.AddComponent<Animation>();
        }

        // Check if the animation clip is already added to the Animation component
        if (animation.GetClip(useAnimation.name) == null)
        {
            // Add the clip to the Animation component
            animation.AddClip(useAnimation, useAnimation.name);
        }

        // Now play the animation
        if (animation.GetClip(useAnimation.name) != null)
        {
            animation.Play(useAnimation.name);
        }
        else
        {
            Debug.LogWarning($"Failed to add animation clip '{useAnimation.name}' to Animation component on {gameObject.name}");
        }
    }

    private static void ApplyAudio(GameObject gameObject, AudioClip useAudioClip)
    {
        if (useAudioClip == null) return;

        AudioSource audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            // Try to get from Player component if available
            var player = gameObject.GetComponent<Player>();
            audioSource = player?.PlayerAudioSource;

            // If still null, add one
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Use PlayOneShot instead of setting clip and playing to avoid conflicts
        audioSource.PlayOneShot(useAudioClip);
    }

    private static void ApplyParticles(GameObject gameObject, ParticleSystem useParticles)
    {
        if (useParticles == null) return;

        // Instantiate the particle system as a separate object instead of trying to copy it
        var particles = Object.Instantiate(useParticles, gameObject.transform.position, gameObject.transform.rotation);
        particles.transform.SetParent(gameObject.transform);
        particles.Play();

        // Optionally destroy the particle system after it finishes
        if (!particles.main.loop)
        {
            Object.Destroy(particles.gameObject, particles.main.duration + particles.main.startLifetime.constantMax);
        }
    }
}