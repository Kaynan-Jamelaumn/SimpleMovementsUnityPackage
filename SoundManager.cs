using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System;


// Handles music, sound effects, ambient sounds, and volume control
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
    [Range(2, 10)]
    public int maxSimultaneousSFX = 5;

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

    // Singleton pattern
    public static SoundManager Instance { get; private set; }

    // Audio source pools for efficient SFX playback
    private Queue<AudioSource> availableSFXSources;
    private List<AudioSource> allSFXSources;
    private Dictionary<string, AudioClip> audioClipCache;

    // Music and ambient management
    private Coroutine musicFadeCoroutine;
    private Coroutine ambientFadeCoroutine;
    private AudioClip currentMusicClip;
    private AudioClip currentAmbientClip;
    private bool isMusicPaused = false;
    private bool isAmbientPaused = false;

    // Volume management
    private float currentMasterVolume;
    private float currentMusicVolume;
    private float currentSFXVolume;
    private float currentAmbientVolume;
    private float currentVoiceVolume;

    // AudioMixer parameter names - must match your mixer setup
    private const string MASTER_VOLUME_PARAM = "MasterVolume";
    private const string MUSIC_VOLUME_PARAM = "MusicVolume";
    private const string SFX_VOLUME_PARAM = "SFXVolume";
    private const string AMBIENT_VOLUME_PARAM = "AmbientVolume";
    private const string VOICE_VOLUME_PARAM = "VoiceVolume";

    // PlayerPrefs keys for persistence
    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string AMBIENT_VOLUME_KEY = "AmbientVolume";
    private const string VOICE_VOLUME_KEY = "VoiceVolume";

    // Audio categories for organized sound management
    public enum AudioCategory
    {
        Music,
        SFX,
        Ambient,
        Voice,
        UI
    }

    // Sound effect data structure
    [System.Serializable]
    public class SoundEffect
    {
        public string name;
        public AudioClip clip;
        public AudioCategory category;
        [Range(0f, 1f)]
        public float volume = 1f;
        [Range(0.1f, 3f)]
        public float pitch = 1f;
        public bool loop = false;
        public bool randomizePitch = false;
        [Range(0f, 0.5f)]
        public float pitchVariation = 0.1f;
    }

    [Header("Sound Effects Library")]
    public SoundEffect[] soundEffects;

    // INITIALIZATION SYSTEM

    void Awake()
    {
        InitializeSingleton();
        InitializeAudioSources();
        InitializeAudioCache();
    }

    void Start()
    {
        LoadVolumeSettings();
        ApplyAllVolumeSettings();
    }

    void OnDestroy()
    {
        SaveVolumeSettings();
        CleanupSingleton();
    }

    private void InitializeSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persist across scenes
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeAudioSources()
    {
        // Initialize SFX audio source pool
        availableSFXSources = new Queue<AudioSource>();
        allSFXSources = new List<AudioSource>();

        for (int i = 0; i < maxSimultaneousSFX; i++)
        {
            GameObject sfxObject = new GameObject($"SFX_AudioSource_{i}");
            sfxObject.transform.SetParent(transform);

            AudioSource source = sfxObject.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = sfxMixerGroup;
            source.playOnAwake = false;

            availableSFXSources.Enqueue(source);
            allSFXSources.Add(source);
        }

        // Initialize music audio source if not assigned
        if (musicAudioSource == null)
        {
            GameObject musicObject = new GameObject("Music_AudioSource");
            musicObject.transform.SetParent(transform);
            musicAudioSource = musicObject.AddComponent<AudioSource>();
            musicAudioSource.outputAudioMixerGroup = musicMixerGroup;
            musicAudioSource.playOnAwake = false;
            musicAudioSource.loop = true;
        }

        // Initialize ambient audio source if not assigned
        if (ambientAudioSource == null)
        {
            GameObject ambientObject = new GameObject("Ambient_AudioSource");
            ambientObject.transform.SetParent(transform);
            ambientAudioSource = ambientObject.AddComponent<AudioSource>();
            ambientAudioSource.outputAudioMixerGroup = ambientMixerGroup;
            ambientAudioSource.playOnAwake = false;
            ambientAudioSource.loop = true;
        }
    }

    private void InitializeAudioCache()
    {
        audioClipCache = new Dictionary<string, AudioClip>();

        // Cache all sound effects for quick access
        foreach (var soundEffect in soundEffects)
        {
            if (soundEffect.clip != null && !audioClipCache.ContainsKey(soundEffect.name))
            {
                audioClipCache[soundEffect.name] = soundEffect.clip;
            }
        }
    }

    private void CleanupSingleton()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // VOLUME MANAGEMENT SYSTEM

    public void SetMasterVolume(float volume)
    {
        currentMasterVolume = Mathf.Clamp01(volume);
        ApplyVolumeToMixer(MASTER_VOLUME_PARAM, currentMasterVolume);
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, currentMasterVolume);
    }

    public void SetMusicVolume(float volume)
    {
        currentMusicVolume = Mathf.Clamp01(volume);
        ApplyVolumeToMixer(MUSIC_VOLUME_PARAM, currentMusicVolume);
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, currentMusicVolume);
    }

    public void SetSFXVolume(float volume)
    {
        currentSFXVolume = Mathf.Clamp01(volume);
        ApplyVolumeToMixer(SFX_VOLUME_PARAM, currentSFXVolume);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, currentSFXVolume);
    }

    public void SetAmbientVolume(float volume)
    {
        currentAmbientVolume = Mathf.Clamp01(volume);
        ApplyVolumeToMixer(AMBIENT_VOLUME_PARAM, currentAmbientVolume);
        PlayerPrefs.SetFloat(AMBIENT_VOLUME_KEY, currentAmbientVolume);
    }

    public void SetVoiceVolume(float volume)
    {
        currentVoiceVolume = Mathf.Clamp01(volume);
        ApplyVolumeToMixer(VOICE_VOLUME_PARAM, currentVoiceVolume);
        PlayerPrefs.SetFloat(VOICE_VOLUME_KEY, currentVoiceVolume);
    }

    private void ApplyVolumeToMixer(string parameter, float volume)
    {
        if (masterMixerGroup?.audioMixer != null)
        {
            // Convert linear volume (0-1) to decibel scale
            float dbValue = volume > 0 ? Mathf.Log10(volume) * 20f : -80f;
            masterMixerGroup.audioMixer.SetFloat(parameter, dbValue);
        }
    }

    private void LoadVolumeSettings()
    {
        currentMasterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, defaultMasterVolume);
        currentMusicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, defaultMusicVolume);
        currentSFXVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, defaultSFXVolume);
        currentAmbientVolume = PlayerPrefs.GetFloat(AMBIENT_VOLUME_KEY, defaultAmbientVolume);
        currentVoiceVolume = PlayerPrefs.GetFloat(VOICE_VOLUME_KEY, defaultVoiceVolume);
    }

    private void ApplyAllVolumeSettings()
    {
        ApplyVolumeToMixer(MASTER_VOLUME_PARAM, currentMasterVolume);
        ApplyVolumeToMixer(MUSIC_VOLUME_PARAM, currentMusicVolume);
        ApplyVolumeToMixer(SFX_VOLUME_PARAM, currentSFXVolume);
        ApplyVolumeToMixer(AMBIENT_VOLUME_PARAM, currentAmbientVolume);
        ApplyVolumeToMixer(VOICE_VOLUME_PARAM, currentVoiceVolume);
    }

    private void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, currentMasterVolume);
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, currentMusicVolume);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, currentSFXVolume);
        PlayerPrefs.SetFloat(AMBIENT_VOLUME_KEY, currentAmbientVolume);
        PlayerPrefs.SetFloat(VOICE_VOLUME_KEY, currentVoiceVolume);
        PlayerPrefs.Save();
    }

    // SOUND EFFECT SYSTEM

    public void PlaySFX(string soundName)
    {
        var soundEffect = GetSoundEffect(soundName);
        if (soundEffect != null)
        {
            PlaySFX(soundEffect);
        }
        else
        {
            Debug.LogWarning($"Sound effect '{soundName}' not found!");
        }
    }

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
            source.Play();

            StartCoroutine(ReturnSFXSourceWhenFinished(source));
        }
    }

    public void PlaySFX(SoundEffect soundEffect)
    {
        if (soundEffect?.clip == null) return;

        var source = GetAvailableSFXSource();
        if (source != null)
        {
            source.clip = soundEffect.clip;
            source.volume = soundEffect.volume;
            source.loop = soundEffect.loop;

            // Apply pitch variation if enabled
            if (soundEffect.randomizePitch)
            {
                float pitchVariation = UnityEngine.Random.Range(-soundEffect.pitchVariation, soundEffect.pitchVariation);
                source.pitch = soundEffect.pitch + pitchVariation;
            }
            else
            {
                source.pitch = soundEffect.pitch;
            }

            source.Play();

            if (!soundEffect.loop)
            {
                StartCoroutine(ReturnSFXSourceWhenFinished(source));
            }
        }
    }

    // Flexible method that tries clip first, then falls back to name
    public void PlaySFXFlexible(AudioClip clip, string fallbackName = "", float volume = 1f, float pitch = 1f)
    {
        if (clip != null)
        {
            PlaySFX(clip, volume, pitch);
        }
        else if (!string.IsNullOrEmpty(fallbackName))
        {
            PlaySFX(fallbackName);
        }
        else
        {
            Debug.LogWarning("PlaySFXFlexible called with no clip and no fallback name!");
        }
    }

    public void PlaySFXAtPosition(string soundName, Vector3 position, float spatialBlend = 1f)
    {
        var soundEffect = GetSoundEffect(soundName);
        if (soundEffect?.clip == null) return;

        var source = GetAvailableSFXSource();
        if (source != null)
        {
            source.transform.position = position;
            source.spatialBlend = spatialBlend;
            source.clip = soundEffect.clip;
            source.volume = soundEffect.volume;
            source.pitch = soundEffect.pitch;
            source.loop = soundEffect.loop;
            source.Play();

            if (!soundEffect.loop)
            {
                StartCoroutine(ReturnSFXSourceWhenFinished(source));
            }
        }
    }

    // Overload for playing AudioClip at position
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float spatialBlend = 1f)
    {
        if (clip == null) return;

        var source = GetAvailableSFXSource();
        if (source != null)
        {
            source.transform.position = position;
            source.spatialBlend = spatialBlend;
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.loop = false;
            source.Play();

            StartCoroutine(ReturnSFXSourceWhenFinished(source));
        }
    }

    public void StopSFX(string soundName)
    {
        var soundEffect = GetSoundEffect(soundName);
        if (soundEffect?.clip == null) return;

        foreach (var source in allSFXSources)
        {
            if (source.clip == soundEffect.clip && source.isPlaying)
            {
                source.Stop();
                ReturnSFXSource(source);
            }
        }
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

    private AudioSource GetAvailableSFXSource()
    {
        if (availableSFXSources.Count > 0)
        {
            return availableSFXSources.Dequeue();
        }

        // If no sources available, find the oldest playing source and reuse it
        foreach (var source in allSFXSources)
        {
            if (source.isPlaying && !source.loop)
            {
                return source;
            }
        }

        return null;
    }

    private void ReturnSFXSource(AudioSource source)
    {
        if (!availableSFXSources.Contains(source))
        {
            source.clip = null;
            source.spatialBlend = 0f; // Reset to 2D
            availableSFXSources.Enqueue(source);
        }
    }

    private IEnumerator ReturnSFXSourceWhenFinished(AudioSource source)
    {
        yield return new WaitWhile(() => source.isPlaying);
        ReturnSFXSource(source);
    }

    // MUSIC SYSTEM

    public void PlayMusic(AudioClip musicClip, bool fadeIn = true, float fadeTime = -1f)
    {
        if (musicClip == null) return;

        if (fadeTime < 0) fadeTime = musicFadeSpeed;

        currentMusicClip = musicClip;

        if (musicAudioSource.isPlaying && fadeIn)
        {
            // Crossfade from current music to new music
            StartCoroutine(CrossfadeMusic(musicClip, fadeTime));
        }
        else
        {
            // Start new music directly
            musicAudioSource.clip = musicClip;
            musicAudioSource.volume = fadeIn ? 0f : 1f;
            musicAudioSource.Play();

            if (fadeIn)
            {
                if (musicFadeCoroutine != null) StopCoroutine(musicFadeCoroutine);
                musicFadeCoroutine = StartCoroutine(FadeAudioSource(musicAudioSource, 0f, 1f, fadeTime));
            }
        }

        isMusicPaused = false;
    }

    public void StopMusic(bool fadeOut = true, float fadeTime = -1f)
    {
        if (!musicAudioSource.isPlaying) return;

        if (fadeTime < 0) fadeTime = musicFadeSpeed;

        if (fadeOut)
        {
            if (musicFadeCoroutine != null) StopCoroutine(musicFadeCoroutine);
            musicFadeCoroutine = StartCoroutine(FadeOutAndStop(musicAudioSource, fadeTime));
        }
        else
        {
            musicAudioSource.Stop();
        }

        currentMusicClip = null;
        isMusicPaused = false;
    }

    public void PauseMusic()
    {
        if (musicAudioSource.isPlaying)
        {
            musicAudioSource.Pause();
            isMusicPaused = true;
        }
    }

    public void ResumeMusic()
    {
        if (isMusicPaused)
        {
            musicAudioSource.UnPause();
            isMusicPaused = false;
        }
    }

    public void SetMusicPitch(float pitch)
    {
        musicAudioSource.pitch = pitch;
    }

    // AMBIENT SOUND SYSTEM

    public void PlayAmbient(AudioClip ambientClip, bool fadeIn = true, float fadeTime = -1f)
    {
        if (ambientClip == null) return;

        if (fadeTime < 0) fadeTime = ambientFadeSpeed;

        currentAmbientClip = ambientClip;

        if (ambientAudioSource.isPlaying && fadeIn)
        {
            StartCoroutine(CrossfadeAmbient(ambientClip, fadeTime));
        }
        else
        {
            ambientAudioSource.clip = ambientClip;
            ambientAudioSource.volume = fadeIn ? 0f : 1f;
            ambientAudioSource.Play();

            if (fadeIn)
            {
                if (ambientFadeCoroutine != null) StopCoroutine(ambientFadeCoroutine);
                ambientFadeCoroutine = StartCoroutine(FadeAudioSource(ambientAudioSource, 0f, 1f, fadeTime));
            }
        }

        isAmbientPaused = false;
    }

    public void StopAmbient(bool fadeOut = true, float fadeTime = -1f)
    {
        if (!ambientAudioSource.isPlaying) return;

        if (fadeTime < 0) fadeTime = ambientFadeSpeed;

        if (fadeOut)
        {
            if (ambientFadeCoroutine != null) StopCoroutine(ambientFadeCoroutine);
            ambientFadeCoroutine = StartCoroutine(FadeOutAndStop(ambientAudioSource, fadeTime));
        }
        else
        {
            ambientAudioSource.Stop();
        }

        currentAmbientClip = null;
        isAmbientPaused = false;
    }

    public void PauseAmbient()
    {
        if (ambientAudioSource.isPlaying)
        {
            ambientAudioSource.Pause();
            isAmbientPaused = true;
        }
    }

    public void ResumeAmbient()
    {
        if (isAmbientPaused)
        {
            ambientAudioSource.UnPause();
            isAmbientPaused = false;
        }
    }

    // UTILITY COROUTINES

    private IEnumerator CrossfadeMusic(AudioClip newClip, float fadeTime)
    {
        float halfFadeTime = fadeTime * 0.5f;

        // Fade out current music
        yield return StartCoroutine(FadeAudioSource(musicAudioSource, musicAudioSource.volume, 0f, halfFadeTime));

        // Switch clips
        musicAudioSource.clip = newClip;
        musicAudioSource.Play();

        // Fade in new music
        yield return StartCoroutine(FadeAudioSource(musicAudioSource, 0f, 1f, halfFadeTime));
    }

    private IEnumerator CrossfadeAmbient(AudioClip newClip, float fadeTime)
    {
        float halfFadeTime = fadeTime * 0.5f;

        yield return StartCoroutine(FadeAudioSource(ambientAudioSource, ambientAudioSource.volume, 0f, halfFadeTime));

        ambientAudioSource.clip = newClip;
        ambientAudioSource.Play();

        yield return StartCoroutine(FadeAudioSource(ambientAudioSource, 0f, 1f, halfFadeTime));
    }

    private IEnumerator FadeAudioSource(AudioSource source, float startVolume, float endVolume, float duration)
    {
        float elapsed = 0f;
        source.volume = startVolume;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            source.volume = Mathf.Lerp(startVolume, endVolume, t);
            yield return null;
        }

        source.volume = endVolume;
    }

    private IEnumerator FadeOutAndStop(AudioSource source, float fadeTime)
    {
        yield return StartCoroutine(FadeAudioSource(source, source.volume, 0f, fadeTime));
        source.Stop();
    }

    // PUBLIC GETTERS

    public float GetMasterVolume() => currentMasterVolume;
    public float GetMusicVolume() => currentMusicVolume;
    public float GetSFXVolume() => currentSFXVolume;
    public float GetAmbientVolume() => currentAmbientVolume;
    public float GetVoiceVolume() => currentVoiceVolume;

    public bool IsMusicPlaying() => musicAudioSource.isPlaying;
    public bool IsAmbientPlaying() => ambientAudioSource.isPlaying;
    public bool IsMusicPaused() => isMusicPaused;
    public bool IsAmbientPaused() => isAmbientPaused;

    public AudioClip GetCurrentMusicClip() => currentMusicClip;
    public AudioClip GetCurrentAmbientClip() => currentAmbientClip;

    // INTEGRATION METHODS FOR EXISTING PAUSE SYSTEM


    // Call this from your pause menu when the game is paused

    public void OnGamePaused()
    {
        // Lower music and ambient volume when paused
        if (musicAudioSource.isPlaying)
        {
            StartCoroutine(FadeAudioSource(musicAudioSource, musicAudioSource.volume, 0.3f, 0.5f));
        }

        if (ambientAudioSource.isPlaying)
        {
            StartCoroutine(FadeAudioSource(ambientAudioSource, ambientAudioSource.volume, 0.2f, 0.5f));
        }
    }


    // Call this from your pause menu when the game is resumed

    public void OnGameResumed()
    {
        // Restore normal music and ambient volume when resumed
        if (musicAudioSource.isPlaying)
        {
            StartCoroutine(FadeAudioSource(musicAudioSource, musicAudioSource.volume, 1f, 0.5f));
        }

        if (ambientAudioSource.isPlaying)
        {
            StartCoroutine(FadeAudioSource(ambientAudioSource, ambientAudioSource.volume, 1f, 0.5f));
        }
    }



    public void PlayUISound(string soundName)
    {
        PlaySFX(soundName);
    }
}