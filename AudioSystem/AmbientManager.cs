using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class AmbientManager
{
    // Audio sources
    private AudioSource ambientAudioSource;

    // Ambient management
    private Coroutine ambientFadeCoroutine;
    private AudioClip currentAmbientClip;
    private bool isAmbientPaused = false;

    // Fade settings
    private float ambientFadeSpeed;

    // MonoBehaviour reference for coroutines
    private MonoBehaviour monoBehaviourRef;

    public void Initialize(MonoBehaviour monoBehaviour, AudioSource ambientSource, float fadeSpeed)
    {
        monoBehaviourRef = monoBehaviour;
        ambientAudioSource = ambientSource;
        ambientFadeSpeed = fadeSpeed;
    }

    public void PlayAmbient(AudioClip ambientClip, bool fadeIn = true, float fadeTime = -1f)
    {
        if (ambientClip == null) return;

        if (fadeTime < 0) fadeTime = ambientFadeSpeed;

        currentAmbientClip = ambientClip;

        if (ambientAudioSource.isPlaying && fadeIn)
        {
            monoBehaviourRef.StartCoroutine(CrossfadeAmbient(ambientClip, fadeTime));
        }
        else
        {
            ambientAudioSource.clip = ambientClip;
            GameAudio.SetLevel(ambientAudioSource, fadeIn ? 0f : 1f, AudioCategory.Ambient);
            ambientAudioSource.Play();

            if (fadeIn)
            {
                if (ambientFadeCoroutine != null) monoBehaviourRef.StopCoroutine(ambientFadeCoroutine);
                ambientFadeCoroutine = monoBehaviourRef.StartCoroutine(FadeAudioSource(ambientAudioSource, 0f, 1f, fadeTime));
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
            if (ambientFadeCoroutine != null) monoBehaviourRef.StopCoroutine(ambientFadeCoroutine);
            ambientFadeCoroutine = monoBehaviourRef.StartCoroutine(FadeOutAndStop(ambientAudioSource, fadeTime));
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

    // Pause system integration
    public void OnGamePaused()
    {
        if (ambientAudioSource.isPlaying)
        {
            monoBehaviourRef.StartCoroutine(FadeAudioSource(ambientAudioSource, GameAudio.GetLevel(ambientAudioSource), 0.2f, 0.5f));
        }
    }

    public void OnGameResumed()
    {
        if (ambientAudioSource.isPlaying)
        {
            monoBehaviourRef.StartCoroutine(FadeAudioSource(ambientAudioSource, GameAudio.GetLevel(ambientAudioSource), 1f, 0.5f));
        }
    }

    // Getters
    public bool IsAmbientPlaying() => ambientAudioSource.isPlaying;
    public bool IsAmbientPaused() => isAmbientPaused;
    public AudioClip GetCurrentAmbientClip() => currentAmbientClip;

    // Fade coroutines
    private IEnumerator CrossfadeAmbient(AudioClip newClip, float fadeTime)
    {
        float halfFadeTime = fadeTime * 0.5f;

        yield return monoBehaviourRef.StartCoroutine(FadeAudioSource(ambientAudioSource, GameAudio.GetLevel(ambientAudioSource), 0f, halfFadeTime));

        ambientAudioSource.clip = newClip;
        ambientAudioSource.Play();

        yield return monoBehaviourRef.StartCoroutine(FadeAudioSource(ambientAudioSource, 0f, 1f, halfFadeTime));
    }

    private IEnumerator FadeAudioSource(AudioSource source, float startVolume, float endVolume, float duration)
    {
        float elapsed = 0f;
        GameAudio.SetLevel(source, startVolume, AudioCategory.Ambient);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            GameAudio.SetLevel(source, Mathf.Lerp(startVolume, endVolume, t), AudioCategory.Ambient);
            yield return null;
        }

        GameAudio.SetLevel(source, endVolume, AudioCategory.Ambient);
    }

    private IEnumerator FadeOutAndStop(AudioSource source, float fadeTime)
    {
        yield return monoBehaviourRef.StartCoroutine(FadeAudioSource(source, GameAudio.GetLevel(source), 0f, fadeTime));
        source.Stop();
    }
}