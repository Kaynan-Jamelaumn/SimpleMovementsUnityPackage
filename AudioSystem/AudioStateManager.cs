using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System.Linq;

public class AudioStateManager
{
    // Audio state management
    private Stack<AudioState> audioStateStack;
    private Dictionary<string, AudioSnapshot> audioSnapshots;

    // References to other managers
    private MusicManager musicManager;
    private AmbientManager ambientManager;

    // The sources whose levels a state remembers (the SoundManager's, whatever their objects are called)
    private AudioSource musicSource;
    private AudioSource ambientSource;

    public void Initialize(MusicManager music, AmbientManager ambient, AudioSource musicAudioSource = null, AudioSource ambientAudioSource = null)
    {
        musicManager = music;
        ambientManager = ambient;
        musicSource = musicAudioSource;
        ambientSource = ambientAudioSource;

        audioStateStack = new Stack<AudioState>();
        audioSnapshots = new Dictionary<string, AudioSnapshot>();
    }

    // Audio state management
    public void PushAudioState(string stateName)
    {
        AudioState state = new AudioState
        {
            name = stateName,
            musicClip = musicManager.GetCurrentMusicClip(),
            ambientClip = ambientManager.GetCurrentAmbientClip(),
            musicVolume = GetMusicVolume(),
            ambientVolume = GetAmbientVolume(),
            musicPaused = musicManager.IsMusicPaused(),
            ambientPaused = ambientManager.IsAmbientPaused(),
            timestamp = Time.time
        };

        audioStateStack.Push(state);
    }

    public void PopAudioState()
    {
        if (audioStateStack.Count > 0)
        {
            AudioState state = audioStateStack.Pop();

            // Restore audio state
            if (state.musicClip != null)
                musicManager.PlayMusic(state.musicClip, false);
            if (state.ambientClip != null)
                ambientManager.PlayAmbient(state.ambientClip, false);

            SetMusicVolume(state.musicVolume);
            SetAmbientVolume(state.ambientVolume);

            if (state.musicPaused) musicManager.PauseMusic();
            if (state.ambientPaused) ambientManager.PauseAmbient();
        }
    }

    // Audio snapshot system
    public void RegisterAudioSnapshot(string name, AudioMixerSnapshot snapshot, float transitionTime = 1f)
    {
        audioSnapshots[name] = new AudioSnapshot
        {
            name = name,
            mixerSnapshot = snapshot,
            transitionTime = transitionTime
        };
    }

    public void TransitionToSnapshot(string snapshotName)
    {
        if (audioSnapshots.ContainsKey(snapshotName))
        {
            var snapshot = audioSnapshots[snapshotName];
            snapshot.mixerSnapshot.TransitionTo(snapshot.transitionTime);
        }
    }

    // Audio analytics
    public Dictionary<string, int> GetSoundPlayCounts()
    {
        // This would require tracking play counts - implement as needed
        return new Dictionary<string, int>();
    }

    // Helper methods to access audio source volumes
    private float GetMusicVolume() => musicSource != null ? GameAudio.GetLevel(musicSource) : 1f;

    private float GetAmbientVolume() => ambientSource != null ? GameAudio.GetLevel(ambientSource) : 1f;

    private void SetMusicVolume(float volume)
    {
        if (musicSource != null) GameAudio.SetLevel(musicSource, volume, AudioCategory.Music);
    }

    private void SetAmbientVolume(float volume)
    {
        if (ambientSource != null) GameAudio.SetLevel(ambientSource, volume, AudioCategory.Ambient);
    }
}