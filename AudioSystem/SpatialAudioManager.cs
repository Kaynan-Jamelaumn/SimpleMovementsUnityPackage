using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System.Linq;

// Manages spatial audio features including 3D positioning and distance-based effects
public class SpatialAudioManager
{
    // Spatial audio sources management
    private List<SpatialAudioSource> spatialAudioSources;
    private Queue<SpatialAudioSource> availableSpatialSources;
    private int maxSpatialSources = 20;

    // Global spatial settings
    private float globalMaxDistance;
    private AudioRolloffMode defaultRolloffMode;
    private SpatialAudioConfig globalSpatialConfig;

    // References
    private MonoBehaviour monoBehaviourRef;
    private Transform listenerTransform;
    private AudioMixerGroup sfxMixerGroup;

    // Performance tracking
    private Dictionary<SpatialAudioSource, float> sourceLastUsed;

    public void Initialize(MonoBehaviour monoBehaviour, float maxDistance, AudioRolloffMode rolloffMode)
    {
        monoBehaviourRef = monoBehaviour;
        globalMaxDistance = maxDistance;
        defaultRolloffMode = rolloffMode;

        spatialAudioSources = new List<SpatialAudioSource>();
        availableSpatialSources = new Queue<SpatialAudioSource>();
        sourceLastUsed = new Dictionary<SpatialAudioSource, float>();

        // Set default spatial configuration
        globalSpatialConfig = new SpatialAudioConfig
        {
            maxDistance = maxDistance,
            rolloffMode = rolloffMode,
            spatialBlend = 1f,
            dopplerLevel = 1f,
            spread = 0f,
            volumeRolloff = AnimationCurve.Linear(0f, 1f, 1f, 0f)
        };

        InitializeSpatialSources();
    }

    private void InitializeSpatialSources()
    {
        for (int i = 0; i < maxSpatialSources; i++)
        {
            CreateSpatialAudioSource(i);
        }
    }

    private void CreateSpatialAudioSource(int index)
    {
        GameObject spatialObject = new GameObject($"SpatialAudio_Source_{index}");
        spatialObject.transform.SetParent(monoBehaviourRef.transform);

        AudioSource audioSource = spatialObject.AddComponent<AudioSource>();
        audioSource.outputAudioMixerGroup = sfxMixerGroup;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D by default

        SpatialAudioSource spatialSource = new SpatialAudioSource
        {
            audioSource = audioSource,
            gameObject = spatialObject,
            isPlaying = false,
            soundName = "",
            maxDistance = globalMaxDistance,
            startTime = 0f
        };

        spatialAudioSources.Add(spatialSource);
        availableSpatialSources.Enqueue(spatialSource);
        sourceLastUsed[spatialSource] = 0f;
    }

    public void SetListenerTransform(Transform listener)
    {
        listenerTransform = listener;
    }

    public void SetGlobalSpatialConfig(SpatialAudioConfig config)
    {
        globalSpatialConfig = config;
    }

    // Play spatial SFX with custom configuration
    public void PlaySpatialSFX(string soundName, Vector3 position, SpatialAudioConfig spatialConfig = null)
    {
        SoundEffect soundEffect = GetSoundEffect(soundName);
        if (soundEffect == null) return;

        PlaySpatialSFX(soundEffect, position, spatialConfig);
    }

    public void PlaySpatialSFX(SoundEffect soundEffect, Vector3 position, SpatialAudioConfig spatialConfig = null)
    {
        if (soundEffect?.clip == null) return;

        SpatialAudioSource spatialSource = GetAvailableSpatialSource();
        if (spatialSource == null) return;

        // Use provided config or global config
        SpatialAudioConfig config = spatialConfig ?? globalSpatialConfig;

        ConfigureSpatialAudioSource(spatialSource, soundEffect, position, config);
        spatialSource.audioSource.Play();

        spatialSource.isPlaying = true;
        spatialSource.soundName = soundEffect.name;
        spatialSource.startTime = Time.time;
        sourceLastUsed[spatialSource] = Time.time;

        if (!soundEffect.loop)
        {
            monoBehaviourRef.StartCoroutine(ReturnSpatialSourceWhenFinished(spatialSource));
        }
    }

    // Play ambient sound at specific position
    public void PlayAmbientAtPosition(AudioClip clip, Vector3 position, float maxDistance = 50f, bool fadeIn = true)
    {
        if (clip == null) return;

        SpatialAudioSource spatialSource = GetAvailableSpatialSource();
        if (spatialSource == null) return;

        // Configure for ambient playback
        spatialSource.audioSource.clip = clip;
        spatialSource.audioSource.volume = fadeIn ? 0f : 1f;
        spatialSource.audioSource.loop = true;
        spatialSource.audioSource.spatialBlend = 1f;
        spatialSource.audioSource.maxDistance = maxDistance;
        spatialSource.audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        spatialSource.gameObject.transform.position = position;

        spatialSource.audioSource.Play();
        spatialSource.isPlaying = true;
        spatialSource.soundName = $"Ambient_{clip.name}";
        spatialSource.startTime = Time.time;
        spatialSource.maxDistance = maxDistance;

        if (fadeIn)
        {
            monoBehaviourRef.StartCoroutine(FadeInSpatialSource(spatialSource, 2f));
        }
    }

    private void ConfigureSpatialAudioSource(SpatialAudioSource spatialSource, SoundEffect soundEffect, Vector3 position, SpatialAudioConfig config)
    {
        AudioSource source = spatialSource.audioSource;

        // Basic configuration
        source.clip = soundEffect.clip;
        source.volume = soundEffect.volume;
        source.pitch = soundEffect.pitch;
        source.loop = soundEffect.loop;
        source.priority = (int)soundEffect.priority;

        // Spatial configuration
        source.spatialBlend = config.spatialBlend;
        source.maxDistance = config.maxDistance;
        source.rolloffMode = config.rolloffMode;
        source.dopplerLevel = config.dopplerLevel;
        source.spread = config.spread;

        // Position
        spatialSource.gameObject.transform.position = position;
        spatialSource.maxDistance = config.maxDistance;

        // Apply custom volume rolloff curve if provided
        if (config.volumeRolloff != null && config.volumeRolloff.keys.Length > 0)
        {
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, config.volumeRolloff);
        }

        // Pitch variation
        if (soundEffect.randomizePitch)
        {
            float pitchVariation = Random.Range(-soundEffect.pitchVariation, soundEffect.pitchVariation);
            source.pitch = soundEffect.pitch + pitchVariation;
        }
    }

    // Update spatial audio calculations each frame
    public void UpdateSpatialAudio()
    {
        if (listenerTransform == null) return;

        foreach (var spatialSource in spatialAudioSources)
        {
            if (spatialSource.isPlaying && spatialSource.audioSource.isPlaying)
            {
                UpdateSpatialSourceDistance(spatialSource);
            }
        }
    }

    private void UpdateSpatialSourceDistance(SpatialAudioSource spatialSource)
    {
        float distance = Vector3.Distance(listenerTransform.position, spatialSource.gameObject.transform.position);

        // Automatically stop sounds that are too far away for performance
        if (distance > spatialSource.maxDistance * 1.5f)
        {
            spatialSource.audioSource.Stop();
            ReturnSpatialSource(spatialSource);
        }
    }

    private SpatialAudioSource GetAvailableSpatialSource()
    {
        // Try to get an available source
        if (availableSpatialSources.Count > 0)
        {
            return availableSpatialSources.Dequeue();
        }

        // Find the oldest non-looping source
        SpatialAudioSource oldestSource = null;
        float oldestTime = float.MaxValue;

        foreach (var source in spatialAudioSources)
        {
            if (source.isPlaying && !source.audioSource.loop && sourceLastUsed[source] < oldestTime)
            {
                oldestTime = sourceLastUsed[source];
                oldestSource = source;
            }
        }

        if (oldestSource != null)
        {
            oldestSource.audioSource.Stop();
            ReturnSpatialSource(oldestSource);
            return oldestSource;
        }

        return null;
    }

    private void ReturnSpatialSource(SpatialAudioSource spatialSource)
    {
        spatialSource.isPlaying = false;
        spatialSource.soundName = "";
        spatialSource.audioSource.clip = null;
        spatialSource.audioSource.Stop();

        if (!availableSpatialSources.Contains(spatialSource))
        {
            availableSpatialSources.Enqueue(spatialSource);
        }
    }

    public void StopAllSpatialSounds()
    {
        foreach (var spatialSource in spatialAudioSources)
        {
            if (spatialSource.isPlaying)
            {
                spatialSource.audioSource.Stop();
                ReturnSpatialSource(spatialSource);
            }
        }
    }

    public void PauseAllSpatialSounds()
    {
        foreach (var spatialSource in spatialAudioSources)
        {
            if (spatialSource.isPlaying && spatialSource.audioSource.isPlaying)
            {
                spatialSource.audioSource.Pause();
            }
        }
    }

    public void ResumeAllSpatialSounds()
    {
        foreach (var spatialSource in spatialAudioSources)
        {
            if (spatialSource.isPlaying)
            {
                spatialSource.audioSource.UnPause();
            }
        }
    }

    public void OnGamePaused()
    {
        PauseAllSpatialSounds();
    }

    public void OnGameResumed()
    {
        ResumeAllSpatialSounds();
    }

    // Cleanup unused spatial sources
    public void CleanupSpatialSources()
    {
        List<SpatialAudioSource> toCleanup = new List<SpatialAudioSource>();

        foreach (var spatialSource in spatialAudioSources)
        {
            if (spatialSource.isPlaying && !spatialSource.audioSource.isPlaying)
            {
                toCleanup.Add(spatialSource);
            }
        }

        foreach (var source in toCleanup)
        {
            ReturnSpatialSource(source);
        }
    }

    public int GetActiveSpatialSourceCount()
    {
        return spatialAudioSources.Count(s => s.isPlaying);
    }

    private SoundEffect GetSoundEffect(string soundName)
    {
        // This should access the sound effects array from SoundManager
        // For now, return null - this would be implemented by accessing the main sound library
        return null;
    }

    private IEnumerator ReturnSpatialSourceWhenFinished(SpatialAudioSource spatialSource)
    {
        yield return new WaitWhile(() => spatialSource.audioSource.isPlaying);
        ReturnSpatialSource(spatialSource);
    }

    private IEnumerator FadeInSpatialSource(SpatialAudioSource spatialSource, float fadeTime)
    {
        float elapsed = 0f;
        AudioSource source = spatialSource.audioSource;

        while (elapsed < fadeTime && source.isPlaying)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / fadeTime;
            source.volume = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        source.volume = 1f;
    }
}