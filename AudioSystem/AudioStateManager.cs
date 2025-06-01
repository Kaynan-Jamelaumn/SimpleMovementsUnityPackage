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

    public void Initialize(MusicManager music, AmbientManager ambient)
    {
        musicManager = music;
        ambientManager = ambient;

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
    private float GetMusicVolume()
    {
        var source = GameObject.Find("Music_AudioSource")?.GetComponent<AudioSource>();
        return source != null ? source.volume : 1f;
    }

    private float GetAmbientVolume()
    {
        var source = GameObject.Find("Ambient_AudioSource")?.GetComponent<AudioSource>();
        return source != null ? source.volume : 1f;
    }

    private void SetMusicVolume(float volume)
    {
        var source = GameObject.Find("Music_AudioSource")?.GetComponent<AudioSource>();
        if (source != null) source.volume = volume;
    }

    private void SetAmbientVolume(float volume)
    {
        var source = GameObject.Find("Ambient_AudioSource")?.GetComponent<AudioSource>();
        if (source != null) source.volume = volume;
    }
}