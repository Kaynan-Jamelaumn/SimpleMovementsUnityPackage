using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System;

// Audio categories with priority
public enum AudioCategory
{
    Music,
    SFX,
    Ambient,
    Voice,
    UI,
    Critical,    // High priority sounds that should always play
    Dialogue,    // Character dialogue
    Footsteps,   // Movement sounds
    Combat,      // Battle sounds
    Environment  // World interaction sounds
}

// Audio priority levels
public enum AudioPriority
{
    Low = 0,
    Normal = 128,
    High = 200,
    Critical = 255
}

// Enhanced sound effect data structure
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

    // Enhanced properties
    public AudioPriority priority = AudioPriority.Normal;
    [Range(0f, 500f)]
    public float maxDistance = 50f;
    public AnimationCurve volumeFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    public bool bypassEffects = false;
    public bool ignoreListenerPause = false;
    [Range(0f, 5f)]
    public float cooldownTime = 0f; // Prevent spam playing

    [HideInInspector]
    public float lastPlayTime;
}

// Audio state for save/restore functionality
[System.Serializable]
public class AudioState
{
    public string name;
    public AudioClip musicClip;
    public AudioClip ambientClip;
    public float musicVolume;
    public float ambientVolume;
    public bool musicPaused;
    public bool ambientPaused;
    public float timestamp;
}

// Audio snapshot for quick audio profile switching
[System.Serializable]
public class AudioSnapshot
{
    public string name;
    public AudioMixerSnapshot mixerSnapshot;
    public float transitionTime = 1f;
}

// NEW: Spatial audio configuration
[System.Serializable]
public class SpatialAudioConfig
{
    [Range(0f, 500f)]
    public float maxDistance = 100f;

    [Range(0f, 1f)]
    public float spatialBlend = 1f; // 0 = 2D, 1 = 3D

    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    [Range(0f, 5f)]
    public float dopplerLevel = 1f;

    [Range(0f, 360f)]
    public float spread = 0f;

    public AnimationCurve volumeRolloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    // Distance-based effects
    public bool enableDistanceFiltering = true;
    [Range(1000f, 22000f)]
    public float maxFilterFrequency = 22000f;
    [Range(200f, 1000f)]
    public float minFilterFrequency = 1000f;

    // Reverb settings
    public bool enableDistanceReverb = false;
    [Range(0f, 1f)]
    public float reverbZoneMultiplier = 1f;
}

// NEW: Spatial audio source wrapper
[System.Serializable]
public class SpatialAudioSource
{
    public AudioSource audioSource;
    public GameObject gameObject;
    public bool isPlaying;
    public string soundName;
    public float maxDistance;
    public float startTime;

    // Additional spatial properties
    public Vector3 velocity;
    public bool trackMovement;
    public Transform followTarget;
}

// NEW: Audio area definition

// NEW: Area shape enumeration
public enum AreaShape
{
    Sphere,
    Box,
    Cylinder
}

// NEW: Audio area state tracking
[System.Serializable]
public class AudioAreaState
{
    public string areaName;
    public bool isActive;
    public float transitionProgress; // 0-1 for smooth transitions
    public float lastEnterTime;
    public List<string> playingSounds;
}