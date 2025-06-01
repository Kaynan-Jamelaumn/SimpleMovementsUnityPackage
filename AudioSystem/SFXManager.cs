using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System.Linq;
using System;

public class SFXManager
{
    // Events
    public static event System.Action<string> OnSoundPlayed;

    // Audio source pools with priority system
    private Queue<AudioSource> availableSFXSources;
    private List<AudioSource> allSFXSources;
    private Dictionary<AudioSource, float> audioSourcePriorities;
    private Dictionary<string, AudioClip> audioClipCache;

    // Performance settings
    private int maxCachedClips;
    private bool enableAudioOcclusion;
    private LayerMask occlusionLayers;
    private float occlusionDamping;

    // Sound effects library
    private SoundEffect[] soundEffects;

    // Audio mixer groups
    private AudioMixerGroup sfxMixerGroup;

    // Parent transform for audio sources
    private Transform parentTransform;

    // MonoBehaviour reference for coroutines
    private MonoBehaviour monoBehaviourRef;

    public void Initialize(MonoBehaviour monoBehaviour, Transform parent, AudioMixerGroup sfxGroup,
                          int maxSFXSources, SoundEffect[] effects, int maxCached, bool occlusion,
                          LayerMask occlusionMask, float damping)
    {
        monoBehaviourRef = monoBehaviour;
        parentTransform = parent;
        sfxMixerGroup = sfxGroup;
        soundEffects = effects;
        maxCachedClips = maxCached;
        enableAudioOcclusion = occlusion;
        occlusionLayers = occlusionMask;
        occlusionDamping = damping;

        InitializeAudioSources(maxSFXSources);
        InitializeAudioCache();
    }

    private void InitializeAudioSources(int maxSimultaneousSFX)
    {
        availableSFXSources = new Queue<AudioSource>();
        allSFXSources = new List<AudioSource>();
        audioSourcePriorities = new Dictionary<AudioSource, float>();

        for (int i = 0; i < maxSimultaneousSFX; i++)
        {
            GameObject sfxObject = new GameObject($"SFX_AudioSource_{i}");
            sfxObject.transform.SetParent(parentTransform);

            AudioSource source = sfxObject.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = sfxMixerGroup;
            source.playOnAwake = false;

            availableSFXSources.Enqueue(source);
            allSFXSources.Add(source);
            audioSourcePriorities[source] = 0f;
        }
    }

    private void InitializeAudioCache()
    {
        audioClipCache = new Dictionary<string, AudioClip>();

        foreach (var soundEffect in soundEffects)
        {
            if (soundEffect.clip != null && !audioClipCache.ContainsKey(soundEffect.name))
            {
                audioClipCache[soundEffect.name] = soundEffect.clip;
            }
        }
    }

    // Original method - play by sound name
    public void PlaySFX(string soundName, Vector3? position = null, float volumeMultiplier = 1f)
    {
        var soundEffect = GetSoundEffect(soundName);
        if (soundEffect != null)
        {
            // Check cooldown
            if (Time.time < soundEffect.lastPlayTime + soundEffect.cooldownTime)
                return;

            soundEffect.lastPlayTime = Time.time;
            PlaySFX(soundEffect, position, volumeMultiplier);
            OnSoundPlayed?.Invoke(soundName);
        }
        else
        {
            Debug.LogWarning($"Sound effect '{soundName}' not found!");
        }
    }

    // Original method - play AudioClip directly (RESTORED)
    public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;

        var source = GetAvailableSFXSource();
        if (source != null)
        {
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.loop = false;
            source.spatialBlend = 0f; // 2D by default
            source.priority = (int)AudioPriority.Normal;
            source.Play();

            monoBehaviourRef.StartCoroutine(ReturnSFXSourceWhenFinished(source));
        }
    }

    // Enhanced method - play SoundEffect with advanced features
    public void PlaySFX(SoundEffect soundEffect, Vector3? position = null, float volumeMultiplier = 1f)
    {
        if (soundEffect?.clip == null) return;

        var source = GetAvailableSFXSource((int)soundEffect.priority);
        if (source != null)
        {
            ConfigureAudioSource(source, soundEffect, position, volumeMultiplier);
            source.Play();

            if (!soundEffect.loop)
            {
                monoBehaviourRef.StartCoroutine(ReturnSFXSourceWhenFinished(source));
            }
        }
    }

    // Enhanced audio source configuration
    private void ConfigureAudioSource(AudioSource source, SoundEffect soundEffect, Vector3? position, float volumeMultiplier)
    {
        source.clip = soundEffect.clip;
        source.volume = soundEffect.volume * volumeMultiplier;
        source.loop = soundEffect.loop;
        source.priority = (int)soundEffect.priority;
        source.maxDistance = soundEffect.maxDistance;
        source.bypassEffects = soundEffect.bypassEffects;
        source.ignoreListenerPause = soundEffect.ignoreListenerPause;

        // Position handling
        if (position.HasValue)
        {
            source.transform.position = position.Value;
            source.spatialBlend = 1f; // 3D

            // Audio occlusion check
            if (enableAudioOcclusion && Camera.main != null)
            {
                CheckAudioOcclusion(source, position.Value);
            }
        }
        else
        {
            source.spatialBlend = 0f; // 2D
        }

        // Pitch variation
        if (soundEffect.randomizePitch)
        {
            float pitchVariation = UnityEngine.Random.Range(-soundEffect.pitchVariation, soundEffect.pitchVariation);
            source.pitch = soundEffect.pitch + pitchVariation;
        }
        else
        {
            source.pitch = soundEffect.pitch;
        }

        // Set volume falloff curve
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, soundEffect.volumeFalloff);
    }

    // Audio occlusion system
    private void CheckAudioOcclusion(AudioSource source, Vector3 soundPosition)
    {
        if (Camera.main == null) return;

        Vector3 listenerPosition = Camera.main.transform.position;
        Vector3 direction = soundPosition - listenerPosition;
        float distance = direction.magnitude;

        if (Physics.Raycast(listenerPosition, direction.normalized, out RaycastHit hit, distance, occlusionLayers))
        {
            // Apply occlusion damping
            source.volume *= (1f - occlusionDamping);

            // Apply low-pass filter effect
            AudioLowPassFilter lowPass = source.GetComponent<AudioLowPassFilter>();
            if (lowPass == null)
                lowPass = source.gameObject.AddComponent<AudioLowPassFilter>();

            lowPass.cutoffFrequency = Mathf.Lerp(22000f, 1000f, occlusionDamping);
        }
    }

    // Priority-based audio source allocation with default fallback
    private AudioSource GetAvailableSFXSource(int priority = (int)AudioPriority.Normal)
    {
        // First try to get an available source
        if (availableSFXSources.Count > 0)
        {
            return availableSFXSources.Dequeue();
        }

        // Find lowest priority playing source
        AudioSource lowestPrioritySource = null;
        int lowestPriority = int.MaxValue;

        foreach (var source in allSFXSources)
        {
            if (source.isPlaying && source.priority < lowestPriority && source.priority < priority)
            {
                lowestPriority = source.priority;
                lowestPrioritySource = source;
            }
        }

        if (lowestPrioritySource != null)
        {
            lowestPrioritySource.Stop();
            return lowestPrioritySource;
        }

        // If no sources available and can't interrupt, try to find any non-looping source
        foreach (var source in allSFXSources)
        {
            if (source.isPlaying && !source.loop)
            {
                return source;
            }
        }

        return null;
    }

    public void StopAllSFX()
    {
        foreach (var source in allSFXSources)
        {
            if (source.isPlaying)
            {
                source.Stop();
                ReturnSFXSource(source);
            }
        }
    }

    public void PauseAllSFX()
    {
        foreach (var source in allSFXSources)
        {
            if (source.isPlaying)
                source.Pause();
        }
    }

    public void ResumeAllSFX()
    {
        foreach (var source in allSFXSources)
        {
            source.UnPause();
        }
    }

    public float GetCurrentAudioLoad()
    {
        int playingSources = allSFXSources.Count(s => s.isPlaying);
        return (float)playingSources / allSFXSources.Count;
    }

    // Audio memory management
    public void CleanupAudioCache()
    {
        if (audioClipCache.Count > maxCachedClips)
        {
            // Remove least recently used clips
            var sortedCache = audioClipCache.OrderBy(x => Time.time).Take(maxCachedClips);
            audioClipCache = sortedCache.ToDictionary(x => x.Key, x => x.Value);
        }
    }

    private SoundEffect GetSoundEffect(string soundName)
    {
        foreach (var soundEffect in soundEffects)
        {
            if (soundEffect.name == soundName)
            {
                return soundEffect;
            }
        }
        return null;
    }

    private void ReturnSFXSource(AudioSource source)
    {
        if (!availableSFXSources.Contains(source))
        {
            source.clip = null;
            source.spatialBlend = 0f;
            audioSourcePriorities[source] = 0f;
            availableSFXSources.Enqueue(source);
        }
    }

    private IEnumerator ReturnSFXSourceWhenFinished(AudioSource source)
    {
        yield return new WaitWhile(() => source.isPlaying);
        ReturnSFXSource(source);
    }
}