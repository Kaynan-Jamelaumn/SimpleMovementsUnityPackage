using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System;

public class VoiceManager
{
    // Audio mixer groups
    private AudioMixerGroup voiceMixerGroup;

    // Dynamic audio adjustment
    private bool isDuckingEnabled = true;
    private float duckingAmount = 0.3f;
    private List<AudioSource> voiceSourcesForDucking;

    // Parent transform for voice sources
    private Transform parentTransform;

    // MonoBehaviour reference for coroutines
    private MonoBehaviour monoBehaviourRef;

    // References to other managers for ducking
    private MusicManager musicManager;
    private AmbientManager ambientManager;

    // Ducking state: how many voice lines are playing, and what the music and ambience were at before the first one.
    private int activeDucks;
    private bool duckCaptured;
    private float musicBeforeDuck = 1f;
    private float ambientBeforeDuck = 1f;

    public void Initialize(MonoBehaviour monoBehaviour, Transform parent, AudioMixerGroup voiceGroup,
                          MusicManager music, AmbientManager ambient)
    {
        monoBehaviourRef = monoBehaviour;
        parentTransform = parent;
        voiceMixerGroup = voiceGroup;
        musicManager = music;
        ambientManager = ambient;

        voiceSourcesForDucking = new List<AudioSource>();
    }

    // Audio ducking system for voice/dialogue
    public void EnableDucking(bool enable, float duckAmount = 0.3f)
    {
        isDuckingEnabled = enable;
        duckingAmount = duckAmount;
    }

    public void PlayVoice(AudioClip voiceClip, float volume = 1f, System.Action onComplete = null)
    {
        if (voiceClip == null) return;

        // Create temporary voice source
        GameObject voiceObject = new GameObject("Voice_Temp");
        voiceObject.transform.SetParent(parentTransform);
        AudioSource voiceSource = voiceObject.AddComponent<AudioSource>();
        voiceSource.outputAudioMixerGroup = voiceMixerGroup;
        voiceSource.clip = voiceClip;
        GameAudio.SetLevel(voiceSource, volume, AudioCategory.Voice);
        voiceSource.Play();

        voiceSourcesForDucking.Add(voiceSource);

        // Duck other audio (it calls onComplete once the music is back up)
        bool ducks = isDuckingEnabled;
        if (ducks)
        {
            monoBehaviourRef.StartCoroutine(DuckAudioForVoice(voiceSource, onComplete));
        }

        // Cleanup when finished (and onComplete, when nothing was ducked)
        monoBehaviourRef.StartCoroutine(CleanupVoiceSource(voiceSource, ducks ? null : onComplete));
    }

    public void StopAllVoice()
    {
        foreach (var voiceSource in voiceSourcesForDucking)
        {
            if (voiceSource != null)
                voiceSource.Stop();
        }
    }

    private IEnumerator DuckAudioForVoice(AudioSource voiceSource, System.Action onComplete)
    {
        AudioSource music = GetMusicAudioSource();
        AudioSource ambient = GetAmbientAudioSource();

        // What to come back to is taken once, before the first line: a second line starting while the music is
        // already down (or coming back up) must not take that lowered level for the normal one.
        if (!duckCaptured)
        {
            duckCaptured = true;
            musicBeforeDuck = GetMusicVolume();
            ambientBeforeDuck = GetAmbientVolume();
        }
        activeDucks++;

        // Fade down
        yield return monoBehaviourRef.StartCoroutine(FadeAudioSource(music, GetMusicVolume(), musicBeforeDuck * duckingAmount, 0.5f, AudioCategory.Music, false));
        yield return monoBehaviourRef.StartCoroutine(FadeAudioSource(ambient, GetAmbientVolume(), ambientBeforeDuck * duckingAmount, 0.5f, AudioCategory.Ambient, false));

        // Wait for voice to finish
        yield return new WaitWhile(() => voiceSource != null && voiceSource.isPlaying);

        // Fade back up, once the last line has ended (and unless another one starts meanwhile)
        activeDucks = Mathf.Max(0, activeDucks - 1);
        if (activeDucks == 0)
        {
            yield return monoBehaviourRef.StartCoroutine(FadeAudioSource(music, GetMusicVolume(), musicBeforeDuck, 0.5f, AudioCategory.Music, true));
            yield return monoBehaviourRef.StartCoroutine(FadeAudioSource(ambient, GetAmbientVolume(), ambientBeforeDuck, 0.5f, AudioCategory.Ambient, true));
            if (activeDucks == 0)
                duckCaptured = false;
        }

        onComplete?.Invoke();
    }

    private IEnumerator CleanupVoiceSource(AudioSource voiceSource, System.Action onComplete)
    {
        yield return new WaitWhile(() => voiceSource != null && voiceSource.isPlaying);

        voiceSourcesForDucking.Remove(voiceSource);
        if (voiceSource != null)
        {
            GameAudio.Forget(voiceSource);
            UnityEngine.Object.Destroy(voiceSource.gameObject);
        }
        onComplete?.Invoke();
    }

    private IEnumerator FadeAudioSource(AudioSource source, float startVolume, float endVolume, float duration, AudioCategory category, bool restoring)
    {
        if (source == null)
            yield break;
        float elapsed = 0f;
        GameAudio.SetLevel(source, startVolume, category);

        while (elapsed < duration)
        {
            // Coming back up while a new voice line started: that line's own fade takes over.
            if (source == null || (restoring && activeDucks > 0))
                yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            GameAudio.SetLevel(source, Mathf.Lerp(startVolume, endVolume, t), category);
            yield return null;
        }

        if (source != null)
            GameAudio.SetLevel(source, endVolume, category);
    }

    // The music and ambience sources of the SoundManager this belongs to (whatever their objects are called).
    private AudioSource GetMusicAudioSource()
    {
        SoundManager manager = monoBehaviourRef as SoundManager;
        return manager != null ? manager.musicAudioSource : null;
    }

    private AudioSource GetAmbientAudioSource()
    {
        SoundManager manager = monoBehaviourRef as SoundManager;
        return manager != null ? manager.ambientAudioSource : null;
    }

    private float GetMusicVolume()
    {
        var source = GetMusicAudioSource();
        return source != null ? GameAudio.GetLevel(source) : 1f;
    }

    private float GetAmbientVolume()
    {
        var source = GetAmbientAudioSource();
        return source != null ? GameAudio.GetLevel(source) : 1f;
    }
}