using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Linq;

// Enhanced sound manager with spatial audio and area-specific playback - ENHANCED
public class SoundManager : MonoBehaviour
{
    [Header("Audio Mixer Setup")]
    public AudioMixerGroup masterMixerGroup;
    public AudioMixerGroup musicMixerGroup;
    public AudioMixerGroup sfxMixerGroup;
    public AudioMixerGroup ambientMixerGroup;
    public AudioMixerGroup voiceMixerGroup;

    [Header("Audio Sources")]
    public AudioSource musicAudioSource;
    public AudioSource ambientAudioSource;
    [Range(2, 20)]
    public int maxSimultaneousSFX = 8;

    [Header("Fade Settings")]
    [Range(0.1f, 10f)]
    public float musicFadeSpeed = 2f;
    [Range(0.1f, 10f)]
    public float ambientFadeSpeed = 1.5f;

    [Header("Volume Settings")]
    [Range(0f, 1f)]
    public float defaultMasterVolume = 0.75f;
    [Range(0f, 1f)]
    public float defaultMusicVolume = 0.75f;
    [Range(0f, 1f)]
    public float defaultSFXVolume = 0.75f;
    [Range(0f, 1f)]
    public float defaultAmbientVolume = 0.5f;
    [Range(0f, 1f)]
    public float defaultVoiceVolume = 0.8f;

    [Header("Performance Settings")]
    [Range(1, 100)]
    public int maxCachedClips = 50;
    [Range(0.1f, 2f)]
    public float audioMemoryCleanupInterval = 30f;
    public bool enableAudioPooling = true;
    public bool enableAudioCompression = true;

    [Header("Advanced Features")]
    public bool enableDynamicRange = true;
    public bool enableAudioOcclusion = false;
    public LayerMask occlusionLayers = -1;
    [Range(0f, 1f)]
    public float occlusionDamping = 0.7f;
    public bool enableReverbZones = true;

    [Header("Spatial Audio Settings")]
    public bool enableSpatialAudio = true;
    [Range(1f, 500f)]
    public float globalMaxAudioDistance = 100f;
    [Range(0.1f, 10f)]
    public float spatialAudioUpdateRate = 0.1f;
    public AudioRolloffMode defaultRolloffMode = AudioRolloffMode.Logarithmic;

    [Header("Area Audio System")]
    public bool enableAreaAudio = true;
    [Range(0.1f, 5f)]
    public float areaTransitionSpeed = 1f;

    [Header("Sound Effects Library")]
    public SoundEffect[] soundEffects;

    // Singleton pattern
    public static SoundManager Instance { get; private set; }

    // Manager components
    private VolumeManager volumeManager;
    private SFXManager sfxManager;
    private MusicManager musicManager;
    private AmbientManager ambientManager;
    private VoiceManager voiceManager;
    private AudioStateManager audioStateManager;
    private SpatialAudioManager spatialAudioManager;
    private AudioAreaManager audioAreaManager;

    // Events
    public static event System.Action<string> OnSoundPlayed;
    public static event System.Action<string> OnMusicChanged;
    public static event System.Action<float> OnVolumeChanged;
    public static event System.Action<string> OnAudioAreaEntered;
    public static event System.Action<string> OnAudioAreaExited;

    // INITIALIZATION SYSTEM

    void Awake()
    {
        InitializeSingleton();
        InitializeManagers();
        InitializeAudioSources();
    }

    void Start()
    {
        volumeManager.Initialize(masterMixerGroup, defaultMasterVolume, defaultMusicVolume,
                               defaultSFXVolume, defaultAmbientVolume, defaultVoiceVolume);
        StartCoroutine(AudioMemoryCleanup());

        if (enableSpatialAudio)
        {
            StartCoroutine(SpatialAudioUpdate());
        }
    }

    void OnDestroy()
    {
        volumeManager?.SaveVolumeSettings();
        CleanupSingleton();
    }

    private void InitializeSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeManagers()
    {
        volumeManager = new VolumeManager();
        sfxManager = new SFXManager();
        musicManager = new MusicManager();
        ambientManager = new AmbientManager();
        voiceManager = new VoiceManager();
        audioStateManager = new AudioStateManager();
        spatialAudioManager = new SpatialAudioManager();
        audioAreaManager = new AudioAreaManager();

        // Subscribe to events
        VolumeManager.OnVolumeChanged += (volume) => OnVolumeChanged?.Invoke(volume);
        SFXManager.OnSoundPlayed += (soundName) => OnSoundPlayed?.Invoke(soundName);
        MusicManager.OnMusicChanged += (musicName) => OnMusicChanged?.Invoke(musicName);
        AudioAreaManager.OnAudioAreaEntered += (areaName) => OnAudioAreaEntered?.Invoke(areaName);
        AudioAreaManager.OnAudioAreaExited += (areaName) => OnAudioAreaExited?.Invoke(areaName);
    }

    private void InitializeAudioSources()
    {
        // Initialize audio sources
        if (musicAudioSource == null)
        {
            GameObject musicObject = new GameObject("Music_AudioSource");
            musicObject.transform.SetParent(transform);
            musicAudioSource = musicObject.AddComponent<AudioSource>();
            musicAudioSource.outputAudioMixerGroup = musicMixerGroup;
            musicAudioSource.playOnAwake = false;
            musicAudioSource.loop = true;
        }

        if (ambientAudioSource == null)
        {
            GameObject ambientObject = new GameObject("Ambient_AudioSource");
            ambientObject.transform.SetParent(transform);
            ambientAudioSource = ambientObject.AddComponent<AudioSource>();
            ambientAudioSource.outputAudioMixerGroup = ambientMixerGroup;
            ambientAudioSource.playOnAwake = false;
            ambientAudioSource.loop = true;
        }

        // Initialize manager components
        sfxManager.Initialize(this, transform, sfxMixerGroup, maxSimultaneousSFX, soundEffects,
                            maxCachedClips, enableAudioOcclusion, occlusionLayers, occlusionDamping);
        musicManager.Initialize(this, musicAudioSource, musicFadeSpeed);
        ambientManager.Initialize(this, ambientAudioSource, ambientFadeSpeed);
        voiceManager.Initialize(this, transform, voiceMixerGroup, musicManager, ambientManager);
        audioStateManager.Initialize(musicManager, ambientManager);
        spatialAudioManager.Initialize(this, globalMaxAudioDistance, defaultRolloffMode);
        audioAreaManager.Initialize(this, areaTransitionSpeed);
    }

    private void CleanupSingleton()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Audio memory management
    private IEnumerator AudioMemoryCleanup()
    {
        while (true)
        {
            yield return new WaitForSeconds(audioMemoryCleanupInterval);
            sfxManager?.CleanupAudioCache();
            spatialAudioManager?.CleanupSpatialSources();
            Resources.UnloadUnusedAssets();
        }
    }

    // Spatial audio update loop
    private IEnumerator SpatialAudioUpdate()
    {
        while (true)
        {
            yield return new WaitForSeconds(spatialAudioUpdateRate);
            spatialAudioManager?.UpdateSpatialAudio();
            audioAreaManager?.UpdatePlayerPosition();
        }
    }

    // PUBLIC API - SOUND EFFECTS (enhanced with spatial features)

    public void PlaySFX(string soundName, Vector3? position = null, float volumeMultiplier = 1f)
    {
        sfxManager?.PlaySFX(soundName, position, volumeMultiplier);
    }

    public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        sfxManager?.PlaySFX(clip, volume, pitch);
    }

    public void PlaySFX(SoundEffect soundEffect, Vector3? position = null, float volumeMultiplier = 1f)
    {
        sfxManager?.PlaySFX(soundEffect, position, volumeMultiplier);
    }

    // NEW: Play sound with spatial configuration
    public void PlaySFXSpatial(string soundName, Vector3 position, SpatialAudioConfig spatialConfig = null)
    {
        spatialAudioManager?.PlaySpatialSFX(soundName, position, spatialConfig);
    }

    // NEW: Play sound in specific area only
    public void PlaySFXInArea(string soundName, string areaName, float volumeMultiplier = 1f)
    {
        audioAreaManager?.PlaySoundInArea(soundName, areaName, volumeMultiplier);
    }

    // NEW: Play looping ambient sound at position
    public void PlayAmbientAtPosition(AudioClip clip, Vector3 position, float maxDistance = 50f, bool fadeIn = true)
    {
        spatialAudioManager?.PlayAmbientAtPosition(clip, position, maxDistance, fadeIn);
    }

    public void StopAllSFX()
    {
        sfxManager?.StopAllSFX();
        spatialAudioManager?.StopAllSpatialSounds();
    }

    // PUBLIC API - MUSIC SYSTEM (unchanged)
    public void PlayMusic(AudioClip musicClip, bool fadeIn = true, float fadeTime = -1f)
    {
        musicManager?.PlayMusic(musicClip, fadeIn, fadeTime);
    }

    public void StopMusic(bool fadeOut = true, float fadeTime = -1f)
    {
        musicManager?.StopMusic(fadeOut, fadeTime);
    }

    public void PauseMusic()
    {
        musicManager?.PauseMusic();
    }

    public void ResumeMusic()
    {
        musicManager?.ResumeMusic();
    }

    public void SetMusicPitch(float pitch)
    {
        musicManager?.SetMusicPitch(pitch);
    }

    public void AddToPlaylist(AudioClip clip)
    {
        musicManager?.AddToPlaylist(clip);
    }

    public void PlayNextInPlaylist()
    {
        musicManager?.PlayNextInPlaylist();
    }

    public void ShufflePlaylist(bool enable)
    {
        musicManager?.ShufflePlaylist(enable);
    }

    // PUBLIC API - AMBIENT SYSTEM (unchanged)
    public void PlayAmbient(AudioClip ambientClip, bool fadeIn = true, float fadeTime = -1f)
    {
        ambientManager?.PlayAmbient(ambientClip, fadeIn, fadeTime);
    }

    public void StopAmbient(bool fadeOut = true, float fadeTime = -1f)
    {
        ambientManager?.StopAmbient(fadeOut, fadeTime);
    }

    public void PauseAmbient()
    {
        ambientManager?.PauseAmbient();
    }

    public void ResumeAmbient()
    {
        ambientManager?.ResumeAmbient();
    }

    // PUBLIC API - VOICE SYSTEM (unchanged)
    public void EnableDucking(bool enable, float duckAmount = 0.3f)
    {
        voiceManager?.EnableDucking(enable, duckAmount);
    }

    public void PlayVoice(AudioClip voiceClip, float volume = 1f, System.Action onComplete = null)
    {
        voiceManager?.PlayVoice(voiceClip, volume, onComplete);
    }

    // PUBLIC API - VOLUME MANAGEMENT (unchanged)
    public void SetMasterVolume(float volume)
    {
        volumeManager?.SetMasterVolume(volume);
    }

    public void SetMusicVolume(float volume)
    {
        volumeManager?.SetMusicVolume(volume);
    }

    public void SetSFXVolume(float volume)
    {
        volumeManager?.SetSFXVolume(volume);
    }

    public void SetAmbientVolume(float volume)
    {
        volumeManager?.SetAmbientVolume(volume);
    }

    public void SetVoiceVolume(float volume)
    {
        volumeManager?.SetVoiceVolume(volume);
    }

    public float GetMasterVolume() => volumeManager?.GetMasterVolume() ?? 0f;
    public float GetMusicVolume() => volumeManager?.GetMusicVolume() ?? 0f;
    public float GetSFXVolume() => volumeManager?.GetSFXVolume() ?? 0f;
    public float GetAmbientVolume() => volumeManager?.GetAmbientVolume() ?? 0f;
    public float GetVoiceVolume() => volumeManager?.GetVoiceVolume() ?? 0f;

    // PUBLIC API - AREA AUDIO MANAGEMENT (NEW)
    public void RegisterAudioArea(AudioArea audioArea)
    {
        audioAreaManager?.RegisterAudioArea(audioArea);
    }

    public void UnregisterAudioArea(AudioArea audioArea)
    {
        audioAreaManager?.UnregisterAudioArea(audioArea);
    }

    public void SetPlayerTransform(Transform playerTransform)
    {
        audioAreaManager?.SetPlayerTransform(playerTransform);
        spatialAudioManager?.SetListenerTransform(playerTransform);
    }

    public string GetCurrentAudioArea()
    {
        return audioAreaManager?.GetCurrentAreaName() ?? "";
    }

    public List<string> GetActiveAudioAreas()
    {
        return audioAreaManager?.GetActiveAreaNames() ?? new List<string>();
    }

    // PUBLIC API - SPATIAL AUDIO MANAGEMENT (NEW)
    public void SetSpatialAudioConfig(SpatialAudioConfig config)
    {
        spatialAudioManager?.SetGlobalSpatialConfig(config);
    }

    public void EnableSpatialAudio(bool enable)
    {
        enableSpatialAudio = enable;
        if (enable && !IsInvoking(nameof(SpatialAudioUpdate)))
        {
            StartCoroutine(SpatialAudioUpdate());
        }
    }

    public int GetActiveSpatialSounds()
    {
        return spatialAudioManager?.GetActiveSpatialSourceCount() ?? 0;
    }

    // PUBLIC API - AUDIO STATE MANAGEMENT (unchanged)
    public void PushAudioState(string stateName)
    {
        audioStateManager?.PushAudioState(stateName);
    }

    public void PopAudioState()
    {
        audioStateManager?.PopAudioState();
    }

    public void RegisterAudioSnapshot(string name, AudioMixerSnapshot snapshot, float transitionTime = 1f)
    {
        audioStateManager?.RegisterAudioSnapshot(name, snapshot, transitionTime);
    }

    public void TransitionToSnapshot(string snapshotName)
    {
        audioStateManager?.TransitionToSnapshot(snapshotName);
    }

    // PUBLIC API - UTILITY METHODS (enhanced)
    public void StopAllAudio()
    {
        musicManager?.StopMusic(false);
        ambientManager?.StopAmbient(false);
        sfxManager?.StopAllSFX();
        voiceManager?.StopAllVoice();
        spatialAudioManager?.StopAllSpatialSounds();
        audioAreaManager?.StopAllAreaSounds();
    }

    public void PauseAllAudio()
    {
        musicManager?.PauseMusic();
        ambientManager?.PauseAmbient();
        sfxManager?.PauseAllSFX();
        spatialAudioManager?.PauseAllSpatialSounds();
    }

    public void ResumeAllAudio()
    {
        musicManager?.ResumeMusic();
        ambientManager?.ResumeAmbient();
        sfxManager?.ResumeAllSFX();
        spatialAudioManager?.ResumeAllSpatialSounds();
    }

    public Dictionary<string, int> GetSoundPlayCounts()
    {
        return audioStateManager?.GetSoundPlayCounts() ?? new Dictionary<string, int>();
    }

    public float GetCurrentAudioLoad()
    {
        return sfxManager?.GetCurrentAudioLoad() ?? 0f;
    }

    // PUBLIC API - STATUS GETTERS (unchanged)
    public bool IsMusicPlaying() => musicManager?.IsMusicPlaying() ?? false;
    public bool IsAmbientPlaying() => ambientManager?.IsAmbientPlaying() ?? false;
    public bool IsMusicPaused() => musicManager?.IsMusicPaused() ?? false;
    public bool IsAmbientPaused() => ambientManager?.IsAmbientPaused() ?? false;

    public AudioClip GetCurrentMusicClip() => musicManager?.GetCurrentMusicClip();
    public AudioClip GetCurrentAmbientClip() => ambientManager?.GetCurrentAmbientClip();

    // PUBLIC API - PAUSE SYSTEM INTEGRATION (unchanged)
    public void OnGamePaused()
    {
        musicManager?.OnGamePaused();
        ambientManager?.OnGamePaused();
        spatialAudioManager?.OnGamePaused();
    }

    public void OnGameResumed()
    {
        musicManager?.OnGameResumed();
        ambientManager?.OnGameResumed();
        spatialAudioManager?.OnGameResumed();
    }

    public void PlayUISound(string soundName)
    {
        PlaySFX(soundName);
    }
}