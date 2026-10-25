using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIAudioFeedback : MonoBehaviour
{
    [Header("Sound Names (must exist in SoundManager)")]
    [SerializeField] private string buttonClickSoundName = "UI_ButtonClick";
    [SerializeField] private string traitSelectSoundName = "UI_TraitSelect";
    [SerializeField] private string traitAddSoundName = "UI_TraitAdd";
    [SerializeField] private string traitRemoveSoundName = "UI_TraitRemove";
    [SerializeField] private string characterCreateSoundName = "UI_CharacterCreate";
    [SerializeField] private string errorSoundName = "UI_Error";
    [SerializeField] private string classSelectSoundName = "UI_ClassSelect";

    [Header("Fallback Audio Clips (if SoundManager unavailable)")]
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip traitSelectClip;
    [SerializeField] private AudioClip traitAddClip;
    [SerializeField] private AudioClip traitRemoveClip;
    [SerializeField] private AudioClip characterCreateClip;
    [SerializeField] private AudioClip errorClip;
    [SerializeField] private AudioClip classSelectClip;

    [Header("Settings")]
    [SerializeField] private float fallbackVolume = 1f;
    [SerializeField] private bool enableDebugLogs = false;

    public void PlayButtonClick()
    {
        PlaySound(buttonClickSoundName, buttonClickClip);
    }

    public void PlayTraitSelect()
    {
        PlaySound(traitSelectSoundName, traitSelectClip);
    }

    public void PlayTraitAdd()
    {
        PlaySound(traitAddSoundName, traitAddClip);
    }

    public void PlayTraitRemove()
    {
        PlaySound(traitRemoveSoundName, traitRemoveClip);
    }

    public void PlayCharacterCreate()
    {
        PlaySound(characterCreateSoundName, characterCreateClip);
    }

    public void PlayError()
    {
        PlaySound(errorSoundName, errorClip);
    }

    public void PlayClassSelect()
    {
        PlaySound(classSelectSoundName, classSelectClip);
    }

    private void PlaySound(string soundName, AudioClip fallbackClip)
    {
        // Try SoundManager first
        if (!string.IsNullOrEmpty(soundName) && SoundManager.Instance != null)
        {
            try
            {
                SoundManager.Instance.PlayUISound(soundName);
                if (enableDebugLogs)
                    Debug.Log($"[UIAudioFeedback] Played sound via SoundManager: {soundName}");
                return;
            }
            catch (System.Exception e)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[UIAudioFeedback] SoundManager failed to play {soundName}: {e.Message}");
            }
        }

        // Fallback to AudioSource.PlayClipAtPoint
        if (fallbackClip != null)
        {
            var cameraTransform = Camera.main != null ? Camera.main.transform : transform;
            AudioSource.PlayClipAtPoint(fallbackClip, cameraTransform.position, fallbackVolume);

            if (enableDebugLogs)
                Debug.Log($"[UIAudioFeedback] Played fallback clip: {fallbackClip.name}");
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[UIAudioFeedback] No audio available for: {soundName}");
        }
    }

    // Utility method for testing all sounds
    [ContextMenu("Test All Sounds")]
    public void TestAllSounds()
    {
        StartCoroutine(TestSoundsSequence());
    }

    private System.Collections.IEnumerator TestSoundsSequence()
    {
        var sounds = new System.Collections.Generic.Dictionary<string, System.Action>
        {
            { "Button Click", PlayButtonClick },
            { "Class Select", PlayClassSelect },
            { "Trait Select", PlayTraitSelect },
            { "Trait Add", PlayTraitAdd },
            { "Trait Remove", PlayTraitRemove },
            { "Character Create", PlayCharacterCreate },
            { "Error", PlayError }
        };

        Debug.Log("[UIAudioFeedback] Testing all sounds...");

        foreach (var soundPair in sounds)
        {
            Debug.Log($"[UIAudioFeedback] Testing: {soundPair.Key}");
            soundPair.Value.Invoke();
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("[UIAudioFeedback] Sound testing complete!");
    }
}
