using UnityEngine;
using System.Collections.Generic;

// Component that automatically registers audio areas with the sound manager
[RequireComponent(typeof(AudioArea))]
public class AudioAreaTrigger : MonoBehaviour
{
    [Header("Auto Registration")]
    public bool autoRegisterOnStart = true;
    public bool autoUnregisterOnDestroy = true;

    [Header("Debug")]
    public bool showDebugInfo = false;

    private AudioArea audioArea;
    private bool isRegistered = false;
    private List<GameObject> playersInArea;

    void Start()
    {
        audioArea = GetComponent<AudioArea>();
        playersInArea = new List<GameObject>();

        if (autoRegisterOnStart)
        {
            RegisterWithSoundManager();
        }
    }

    void OnDestroy()
    {
        if (autoUnregisterOnDestroy && isRegistered)
        {
            UnregisterFromSoundManager();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if the entering object is on the player layer
        if (IsPlayerLayer(other.gameObject.layer))
        {
            if (!playersInArea.Contains(other.gameObject))
            {
                playersInArea.Add(other.gameObject);

                if (showDebugInfo)
                {
                    Debug.Log($"Player entered audio area: {audioArea.areaName}");
                }

                // The AudioAreaManager will handle the actual audio logic
                // This trigger just helps with collision detection for complex shapes
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsPlayerLayer(other.gameObject.layer))
        {
            if (playersInArea.Contains(other.gameObject))
            {
                playersInArea.Remove(other.gameObject);

                if (showDebugInfo)
                {
                    Debug.Log($"Player exited audio area: {audioArea.areaName}");
                }
            }
        }
    }

    private bool IsPlayerLayer(int layer)
    {
        return (audioArea.playerLayers.value & (1 << layer)) != 0;
    }

    // Manual registration methods
    public void RegisterWithSoundManager()
    {
        if (SoundManager.Instance != null && !isRegistered)
        {
            SoundManager.Instance.RegisterAudioArea(audioArea);
            isRegistered = true;

            if (showDebugInfo)
            {
                Debug.Log($"Registered audio area: {audioArea.areaName}");
            }
        }
    }

    public void UnregisterFromSoundManager()
    {
        if (SoundManager.Instance != null && isRegistered)
        {
            SoundManager.Instance.UnregisterAudioArea(audioArea);
            isRegistered = false;

            if (showDebugInfo)
            {
                Debug.Log($"Unregistered audio area: {audioArea.areaName}");
            }
        }
    }

    // Utility methods
    public bool HasPlayersInArea()
    {
        return playersInArea.Count > 0;
    }

    public int GetPlayerCount()
    {
        return playersInArea.Count;
    }

    public List<GameObject> GetPlayersInArea()
    {
        return new List<GameObject>(playersInArea);
    }

    // Test if a specific position is within the area
    public bool IsPositionInArea(Vector3 position)
    {
        return audioArea.IsPlayerInArea(position);
    }

    // Play a sound in this specific area
    public void PlaySoundInThisArea(string soundName, float volumeMultiplier = 1f)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFXInArea(soundName, audioArea.areaName, volumeMultiplier);
        }
    }

    // Editor helper methods
    void OnValidate()
    {
        if (audioArea == null)
        {
            audioArea = GetComponent<AudioArea>();
        }
    }
}