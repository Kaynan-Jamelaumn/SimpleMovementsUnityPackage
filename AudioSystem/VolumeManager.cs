using UnityEngine;
using UnityEngine.Audio;
using System;

public class VolumeManager
{
    // Events
    public static event System.Action<float> OnVolumeChanged;

    // Audio mixer groups
    private AudioMixerGroup masterMixerGroup;

    // Volume settings
    private float currentMasterVolume;
    private float currentMusicVolume;
    private float currentSFXVolume;
    private float currentAmbientVolume;
    private float currentVoiceVolume;

    // Default volumes
    private float defaultMasterVolume;
    private float defaultMusicVolume;
    private float defaultSFXVolume;
    private float defaultAmbientVolume;
    private float defaultVoiceVolume;

    // AudioMixer parameter names
    private const string MASTER_VOLUME_PARAM = "MasterVolume";
    private const string MUSIC_VOLUME_PARAM = "MusicVolume";
    private const string SFX_VOLUME_PARAM = "SFXVolume";
    private const string AMBIENT_VOLUME_PARAM = "AmbientVolume";
    private const string VOICE_VOLUME_PARAM = "VoiceVolume";

    // PlayerPrefs keys
    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string AMBIENT_VOLUME_KEY = "AmbientVolume";
    private const string VOICE_VOLUME_KEY = "VoiceVolume";

    public void Initialize(AudioMixerGroup masterGroup, float defaultMaster, float defaultMusic,
                          float defaultSFX, float defaultAmbient, float defaultVoice)
    {
        masterMixerGroup = masterGroup;
        defaultMasterVolume = defaultMaster;
        defaultMusicVolume = defaultMusic;
        defaultSFXVolume = defaultSFX;
        defaultAmbientVolume = defaultAmbient;
        defaultVoiceVolume = defaultVoice;

        LoadVolumeSettings();
        ApplyAllVolumeSettings();
    }

    public void SetMasterVolume(float volume)
    {
        currentMasterVolume = Mathf.Clamp01(volume);
        ApplyVolumeToMixer(MASTER_VOLUME_PARAM, currentMasterVolume);
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, currentMasterVolume);
        OnVolumeChanged?.Invoke(currentMasterVolume);
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

    public float GetMasterVolume() => currentMasterVolume;
    public float GetMusicVolume() => currentMusicVolume;
    public float GetSFXVolume() => currentSFXVolume;
    public float GetAmbientVolume() => currentAmbientVolume;
    public float GetVoiceVolume() => currentVoiceVolume;

    public void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, currentMasterVolume);
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, currentMusicVolume);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, currentSFXVolume);
        PlayerPrefs.SetFloat(AMBIENT_VOLUME_KEY, currentAmbientVolume);
        PlayerPrefs.SetFloat(VOICE_VOLUME_KEY, currentVoiceVolume);
        PlayerPrefs.Save();
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

    private void ApplyVolumeToMixer(string parameter, float volume)
    {
        if (masterMixerGroup?.audioMixer != null)
        {
            float dbValue = volume > 0 ? Mathf.Log10(volume) * 20f : -80f;
            masterMixerGroup.audioMixer.SetFloat(parameter, dbValue);
        }
    }
}