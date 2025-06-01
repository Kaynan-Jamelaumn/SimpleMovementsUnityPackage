using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// Manages area-specific audio playback and transitions
public class AudioAreaManager
{
    // Events
    public static event System.Action<string> OnAudioAreaEntered;
    public static event System.Action<string> OnAudioAreaExited;

    // Area management
    private List<AudioArea> registeredAreas;
    private List<AudioArea> activeAreas;
    private Dictionary<string, List<AudioSource>> areaAudioSources;
    private Dictionary<string, AudioAreaState> areaStates;

    // Player tracking
    private Transform playerTransform;
    private Vector3 lastPlayerPosition;

    // Transition settings
    private float transitionSpeed;

    // References
    private MonoBehaviour monoBehaviourRef;
    private SoundManager soundManagerRef;

    public void Initialize(MonoBehaviour monoBehaviour, float areaTransitionSpeed)
    {
        monoBehaviourRef = monoBehaviour;
        soundManagerRef = monoBehaviour as SoundManager;
        transitionSpeed = areaTransitionSpeed;

        registeredAreas = new List<AudioArea>();
        activeAreas = new List<AudioArea>();
        areaAudioSources = new Dictionary<string, List<AudioSource>>();
        areaStates = new Dictionary<string, AudioAreaState>();

        lastPlayerPosition = Vector3.zero;
    }

    public void SetPlayerTransform(Transform player)
    {
        playerTransform = player;
        if (player != null)
        {
            lastPlayerPosition = player.position;
        }
    }

    public void RegisterAudioArea(AudioArea audioArea)
    {
        if (audioArea == null || registeredAreas.Contains(audioArea)) return;

        registeredAreas.Add(audioArea);
        areaAudioSources[audioArea.areaName] = new List<AudioSource>();
        areaStates[audioArea.areaName] = new AudioAreaState
        {
            areaName = audioArea.areaName,
            isActive = false,
            transitionProgress = 0f,
            lastEnterTime = 0f,
            playingSounds = new List<string>()
        };

        // Initialize area audio sources if needed
        InitializeAreaAudioSources(audioArea);
    }

    public void UnregisterAudioArea(AudioArea audioArea)
    {
        if (audioArea == null || !registeredAreas.Contains(audioArea)) return;

        // Stop all sounds in this area
        StopAllSoundsInArea(audioArea.areaName);

        // Cleanup
        if (areaAudioSources.ContainsKey(audioArea.areaName))
        {
            foreach (var source in areaAudioSources[audioArea.areaName])
            {
                if (source != null)
                    Object.DestroyImmediate(source.gameObject);
            }
            areaAudioSources.Remove(audioArea.areaName);
        }

        areaStates.Remove(audioArea.areaName);
        registeredAreas.Remove(audioArea);
        activeAreas.Remove(audioArea);
    }

    private void InitializeAreaAudioSources(AudioArea audioArea)
    {
        // Create dedicated audio sources for this area
        for (int i = 0; i < audioArea.maxSimultaneousSounds; i++)
        {
            GameObject areaAudioObject = new GameObject($"AreaAudio_{audioArea.areaName}_{i}");
            areaAudioObject.transform.SetParent(monoBehaviourRef.transform);

            AudioSource areaSource = areaAudioObject.AddComponent<AudioSource>();
            areaSource.outputAudioMixerGroup = soundManagerRef.sfxMixerGroup;
            areaSource.playOnAwake = false;
            areaSource.volume = 0f; // Start muted

            areaAudioSources[audioArea.areaName].Add(areaSource);
        }
    }

    // Update player position and check area transitions
    public void UpdatePlayerPosition()
    {
        if (playerTransform == null) return;

        Vector3 currentPosition = playerTransform.position;

        // Only update if player has moved significantly
        if (Vector3.Distance(currentPosition, lastPlayerPosition) > 0.1f)
        {
            CheckAreaTransitions(currentPosition);
            lastPlayerPosition = currentPosition;
        }

        // Update area transition states
        UpdateAreaTransitions();
    }

    private void CheckAreaTransitions(Vector3 playerPosition)
    {
        List<AudioArea> newActiveAreas = new List<AudioArea>();

        // Check which areas the player is currently in
        foreach (var area in registeredAreas)
        {
            if (area.IsPlayerInArea(playerPosition))
            {
                newActiveAreas.Add(area);

                // Trigger enter event if not already active
                if (!activeAreas.Contains(area))
                {
                    OnAudioAreaEntered?.Invoke(area.areaName);
                    areaStates[area.areaName].isActive = true;
                    areaStates[area.areaName].lastEnterTime = Time.time;

                    // Start area transition in
                    monoBehaviourRef.StartCoroutine(TransitionAreaIn(area));
                }
            }
        }

        // Check for areas the player has left
        foreach (var area in activeAreas.ToList())
        {
            if (!newActiveAreas.Contains(area))
            {
                OnAudioAreaExited?.Invoke(area.areaName);
                areaStates[area.areaName].isActive = false;

                // Start area transition out
                monoBehaviourRef.StartCoroutine(TransitionAreaOut(area));
                activeAreas.Remove(area);
            }
        }

        // Update active areas
        foreach (var area in newActiveAreas)
        {
            if (!activeAreas.Contains(area))
            {
                activeAreas.Add(area);
            }
        }
    }

    private void UpdateAreaTransitions()
    {
        foreach (var areaState in areaStates.Values)
        {
            // Update transition progress for smooth volume changes
            if (areaState.isActive && areaState.transitionProgress < 1f)
            {
                areaState.transitionProgress = Mathf.MoveTowards(areaState.transitionProgress, 1f, transitionSpeed * Time.deltaTime);
            }
            else if (!areaState.isActive && areaState.transitionProgress > 0f)
            {
                areaState.transitionProgress = Mathf.MoveTowards(areaState.transitionProgress, 0f, transitionSpeed * Time.deltaTime);
            }
        }
    }

    // Play sound only in specific area
    public void PlaySoundInArea(string soundName, string areaName, float volumeMultiplier = 1f)
    {
        if (!areaStates.ContainsKey(areaName)) return;

        var areaState = areaStates[areaName];
        if (!areaState.isActive) return; // Only play if player is in area

        // Get available audio source for this area
        var audioSources = areaAudioSources[areaName];
        AudioSource availableSource = null;

        foreach (var source in audioSources)
        {
            if (!source.isPlaying)
            {
                availableSource = source;
                break;
            }
        }

        if (availableSource == null) return; // No available sources

        // Get sound effect and configure source
        SoundEffect soundEffect = GetSoundEffect(soundName);
        if (soundEffect?.clip == null) return;

        availableSource.clip = soundEffect.clip;
        availableSource.volume = soundEffect.volume * volumeMultiplier * areaState.transitionProgress;
        availableSource.pitch = soundEffect.pitch;
        availableSource.loop = soundEffect.loop;
        availableSource.Play();

        // Track playing sound
        areaState.playingSounds.Add(soundName);
        monoBehaviourRef.StartCoroutine(RemoveSoundFromAreaWhenFinished(availableSource, areaName, soundName));
    }

    // Transition effects
    private IEnumerator TransitionAreaIn(AudioArea area)
    {
        var audioSources = areaAudioSources[area.areaName];
        var areaState = areaStates[area.areaName];

        // Play area enter sound if specified
        if (area.enterSound != null)
        {
            PlayAreaSound(area.enterSound, area.areaName, area.enterSoundVolume);
        }

        // Start area ambient sound if specified
        if (area.ambientSound != null)
        {
            PlayAreaAmbientSound(area.ambientSound, area.areaName, area.ambientVolume);
        }

        // Fade in area sounds
        float fadeTime = 1f / transitionSpeed;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;

            foreach (var source in audioSources)
            {
                if (source.isPlaying)
                {
                    // Fade volume based on transition progress
                    float targetVolume = source.clip != null ? 1f : 0f;
                    source.volume = Mathf.Lerp(0f, targetVolume, t);
                }
            }

            yield return null;
        }
    }

    private IEnumerator TransitionAreaOut(AudioArea area)
    {
        var audioSources = areaAudioSources[area.areaName];
        var areaState = areaStates[area.areaName];

        // Play area exit sound if specified
        if (area.exitSound != null)
        {
            PlayAreaSound(area.exitSound, area.areaName, area.exitSoundVolume);
        }

        // Fade out area sounds
        float fadeTime = 1f / transitionSpeed;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;

            foreach (var source in audioSources)
            {
                if (source.isPlaying)
                {
                    source.volume = Mathf.Lerp(source.volume, 0f, t);
                }
            }

            yield return null;
        }

        // Stop all sounds in this area
        StopAllSoundsInArea(area.areaName);
    }

    private void PlayAreaSound(AudioClip clip, string areaName, float volume)
    {
        var audioSources = areaAudioSources[areaName];

        foreach (var source in audioSources)
        {
            if (!source.isPlaying)
            {
                source.clip = clip;
                source.volume = volume;
                source.loop = false;
                source.Play();
                break;
            }
        }
    }

    private void PlayAreaAmbientSound(AudioClip clip, string areaName, float volume)
    {
        var audioSources = areaAudioSources[areaName];

        // Use first available source for ambient (looping) sound
        foreach (var source in audioSources)
        {
            if (!source.isPlaying)
            {
                source.clip = clip;
                source.volume = volume;
                source.loop = true;
                source.Play();
                break;
            }
        }
    }

    public void StopAllSoundsInArea(string areaName)
    {
        if (!areaAudioSources.ContainsKey(areaName)) return;

        foreach (var source in areaAudioSources[areaName])
        {
            source.Stop();
        }

        if (areaStates.ContainsKey(areaName))
        {
            areaStates[areaName].playingSounds.Clear();
        }
    }

    public void StopAllAreaSounds()
    {
        foreach (var areaSources in areaAudioSources.Values)
        {
            foreach (var source in areaSources)
            {
                source.Stop();
            }
        }

        foreach (var areaState in areaStates.Values)
        {
            areaState.playingSounds.Clear();
        }
    }

    // Getters
    public string GetCurrentAreaName()
    {
        return activeAreas.Count > 0 ? activeAreas[0].areaName : "";
    }

    public List<string> GetActiveAreaNames()
    {
        return activeAreas.Select(area => area.areaName).ToList();
    }

    public bool IsPlayerInArea(string areaName)
    {
        return areaStates.ContainsKey(areaName) && areaStates[areaName].isActive;
    }

    private SoundEffect GetSoundEffect(string soundName)
    {
        // This should access the sound effects array from SoundManager
        // Implementation depends on how SoundManager exposes its sound library
        return null;
    }

    private IEnumerator RemoveSoundFromAreaWhenFinished(AudioSource source, string areaName, string soundName)
    {
        yield return new WaitWhile(() => source.isPlaying);

        if (areaStates.ContainsKey(areaName))
        {
            areaStates[areaName].playingSounds.Remove(soundName);
        }
    }
}