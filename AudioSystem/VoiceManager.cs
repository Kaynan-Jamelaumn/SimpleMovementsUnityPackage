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
        voiceSource.volume = volume;
        voiceSource.Play();

        voiceSourcesForDucking.Add(voiceSource);

        // Duck other audio
        if (isDuckingEnabled)
        {
            monoBehaviourRef.StartCoroutine(DuckAudioForVoice(voiceSource, onComplete));
        }

        // Cleanup when finished
        monoBehaviourRef.StartCoroutine(CleanupVoiceSource(voiceSource, onComplete));
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
        // Get current volumes
        float originalMusicVolume = GetMusicVolume();
        float originalAmbientVolume = GetAmbientVolume();

        // Fade down
        yield return monoBehaviourRef.StartCoroutine(FadeAudioSource(GetMusicAudioSource(), originalMusicVolume, originalMusicVolume * duckingAmount, 0.5f));
        yield return monoBehaviourRef.StartCoroutine(FadeAudioSource(GetAmbientAudioSource(), originalAmbientVolume, originalAmbientVolume * duckingAmount, 0.5f));

        // Wait for voice to finish
        yield return new WaitWhile(() => voiceSource.isPlaying);

        // Fade back up
        yield return monoBehaviourRef.StartCoroutine(FadeAudioSource(GetMusicAudioSource(), GetMusicAudioSource().volume, originalMusicVolume, 0.5f));
        yield return monoBehaviourRef.StartCoroutine(FadeAudioSource(GetAmbientAudioSource(), GetAmbientAudioSource().volume, originalAmbientVolume, 0.5f));

        onComplete?.Invoke();
    }

    private IEnumerator CleanupVoiceSource(AudioSource voiceSource, System.Action onComplete)
    {
        yield return new WaitWhile(() => voiceSource.isPlaying);

        voiceSourcesForDucking.Remove(voiceSource);
        if (voiceSource != null)
            UnityEngine.Object.DestroyImmediate(voiceSource.gameObject);
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

    // Helper methods to access other managers' audio sources
    private AudioSource GetMusicAudioSource()
    {
        // This will be set through the main SoundManager
        return GameObject.Find("Music_AudioSource")?.GetComponent<AudioSource>();
    }

    private AudioSource GetAmbientAudioSource()
    {
        // This will be set through the main SoundManager
        return GameObject.Find("Ambient_AudioSource")?.GetComponent<AudioSource>();
    }

    private float GetMusicVolume()
    {
        var source = GetMusicAudioSource();
        return source != null ? source.volume : 1f;
    }

    private float GetAmbientVolume()
    {
        var source = GetAmbientAudioSource();
        return source != null ? source.volume : 1f;
    }
}