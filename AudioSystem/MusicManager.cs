using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System.Linq;
using System;

public class MusicManager
{
    // Events
    public static event System.Action<string> OnMusicChanged;

    // Audio sources
    private AudioSource musicAudioSource;

    // Music playlist system
    private Queue<AudioClip> musicPlaylist;
    private bool shufflePlaylist = false;
    private bool loopPlaylist = true;

    // Music management
    private Coroutine musicFadeCoroutine;
    private AudioClip currentMusicClip;
    private bool isMusicPaused = false;

    // Fade settings
    private float musicFadeSpeed;

    // MonoBehaviour reference for coroutines
    private MonoBehaviour monoBehaviourRef;

    public void Initialize(MonoBehaviour monoBehaviour, AudioSource musicSource, float fadeSpeed)
    {
        monoBehaviourRef = monoBehaviour;
        musicAudioSource = musicSource;
        musicFadeSpeed = fadeSpeed;

        musicPlaylist = new Queue<AudioClip>();
    }

    // Enhanced music system with playlist support
    public void PlayMusic(AudioClip musicClip, bool fadeIn = true, float fadeTime = -1f)
    {
        if (musicClip == null) return;

        if (fadeTime < 0) fadeTime = musicFadeSpeed;

        currentMusicClip = musicClip;

        if (musicAudioSource.isPlaying && fadeIn)
        {
            monoBehaviourRef.StartCoroutine(CrossfadeMusic(musicClip, fadeTime));
        }
        else
        {
            musicAudioSource.clip = musicClip;
            musicAudioSource.volume = fadeIn ? 0f : 1f;
            musicAudioSource.Play();

            if (fadeIn)
            {
                if (musicFadeCoroutine != null) monoBehaviourRef.StopCoroutine(musicFadeCoroutine);
                musicFadeCoroutine = monoBehaviourRef.StartCoroutine(FadeAudioSource(musicAudioSource, 0f, 1f, fadeTime));
            }
        }

        isMusicPaused = false;
        OnMusicChanged?.Invoke(musicClip.name);
    }

    public void StopMusic(bool fadeOut = true, float fadeTime = -1f)
    {
        if (!musicAudioSource.isPlaying) return;

        if (fadeTime < 0) fadeTime = musicFadeSpeed;

        if (fadeOut)
        {
            if (musicFadeCoroutine != null) monoBehaviourRef.StopCoroutine(musicFadeCoroutine);
            musicFadeCoroutine = monoBehaviourRef.StartCoroutine(FadeOutAndStop(musicAudioSource, fadeTime));
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

    // Music playlist system
    public void AddToPlaylist(AudioClip clip)
    {
        if (clip != null)
            musicPlaylist.Enqueue(clip);
    }

    public void PlayNextInPlaylist()
    {
        if (musicPlaylist.Count > 0)
        {
            AudioClip nextClip = musicPlaylist.Dequeue();
            PlayMusic(nextClip);

            if (loopPlaylist)
                musicPlaylist.Enqueue(nextClip);
        }
    }

    public void ShufflePlaylist(bool enable)
    {
        shufflePlaylist = enable;
        if (enable && musicPlaylist.Count > 0)
        {
            // Convert to list, shuffle, convert back to queue
            var playlistList = musicPlaylist.ToList();
            for (int i = 0; i < playlistList.Count; i++)
            {
                var temp = playlistList[i];
                int randomIndex = UnityEngine.Random.Range(i, playlistList.Count);
                playlistList[i] = playlistList[randomIndex];
                playlistList[randomIndex] = temp;
            }

            musicPlaylist.Clear();
            foreach (var clip in playlistList)
                musicPlaylist.Enqueue(clip);
        }
    }

    // Additional playlist management methods
    public void ClearPlaylist()
    {
        musicPlaylist.Clear();
    }

    public int GetPlaylistCount()
    {
        return musicPlaylist.Count;
    }

    public bool IsPlaylistEmpty()
    {
        return musicPlaylist.Count == 0;
    }

    public void SetLoopPlaylist(bool loop)
    {
        loopPlaylist = loop;
    }

    public bool IsPlaylistLooping()
    {
        return loopPlaylist;
    }

    public bool IsPlaylistShuffled()
    {
        return shufflePlaylist;
    }

    // Pause system integration
    public void OnGamePaused()
    {
        if (musicAudioSource.isPlaying)
        {
            monoBehaviourRef.StartCoroutine(FadeAudioSource(musicAudioSource, musicAudioSource.volume, 0.3f, 0.5f));
        }
    }

    public void OnGameResumed()
    {
        if (musicAudioSource.isPlaying)
        {
            monoBehaviourRef.StartCoroutine(FadeAudioSource(musicAudioSource, musicAudioSource.volume, 1f, 0.5f));
        }
    }

    // Getters
    public bool IsMusicPlaying() => musicAudioSource.isPlaying;
    public bool IsMusicPaused() => isMusicPaused;
    public AudioClip GetCurrentMusicClip() => currentMusicClip;

    // Fade coroutines
    private IEnumerator CrossfadeMusic(AudioClip newClip, float fadeTime)
    {
        float halfFadeTime = fadeTime * 0.5f;

        yield return monoBehaviourRef.StartCoroutine(FadeAudioSource(musicAudioSource, musicAudioSource.volume, 0f, halfFadeTime));

        musicAudioSource.clip = newClip;
        musicAudioSource.Play();

        yield return monoBehaviourRef.StartCoroutine(FadeAudioSource(musicAudioSource, 0f, 1f, halfFadeTime));
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
        yield return monoBehaviourRef.StartCoroutine(FadeAudioSource(source, source.volume, 0f, fadeTime));
        source.Stop();
    }
}