using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Helper script for additional Character Creation UI functionality
public class CharacterCreationUIHelpers : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterCreationUI mainUI;

    private void Awake()
    {
        if (mainUI == null)
            mainUI = FindAnyObjectByType<CharacterCreationUI>();
    }

    // Close trait details panel - call this from Close button
    public void CloseTraitDetails()
    {
        if (mainUI != null)
            mainUI.PlayButtonClickSound();

        var traitDetailPanel = GameObject.Find("TraitDetailPanel");
        if (traitDetailPanel != null)
            traitDetailPanel.SetActive(false);
    }

    // Reset entire character creation - call this from Reset button
    public void ResetCharacterCreation()
    {
        if (mainUI != null)
        {
            mainUI.PlayButtonClickSound();
            mainUI.ResetCharacterCreation();
        }
    }

    // Cancel character creation - call this from Cancel button
    public void CancelCharacterCreation()
    {
        if (mainUI != null)
        {
            mainUI.PlayButtonClickSound();
            mainUI.ResetCharacterCreation();
            mainUI.gameObject.SetActive(false);
        }
    }

    // Generic button click sound for any UI button
    public void PlayButtonClick()
    {
        if (mainUI != null)
            mainUI.PlayButtonClickSound();
    }
}

// Simple confirmation dialog for character creation actions

// Tooltip system for showing trait information on hover

// Extensions for UI animation and visual effects
public static class UIAnimationExtensions
{
    // Simple fade in animation for UI panels
    public static void FadeIn(this CanvasGroup canvasGroup, float duration = 0.3f)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = 0f;
        canvasGroup.gameObject.SetActive(true);

        var component = canvasGroup.GetComponent<MonoBehaviour>();
        if (component != null)
            component.StartCoroutine(FadeCoroutine(canvasGroup, 0f, 1f, duration));
    }

    // Simple fade out animation for UI panels
    public static void FadeOut(this CanvasGroup canvasGroup, float duration = 0.3f, bool deactivateAfter = true)
    {
        if (canvasGroup == null) return;

        var component = canvasGroup.GetComponent<MonoBehaviour>();
        if (component != null)
            component.StartCoroutine(FadeCoroutine(canvasGroup, canvasGroup.alpha, 0f, duration, deactivateAfter));
    }

    private static System.Collections.IEnumerator FadeCoroutine(CanvasGroup canvasGroup, float startAlpha, float endAlpha, float duration, bool deactivateAfter = false)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = endAlpha;

        if (deactivateAfter && endAlpha <= 0f)
            canvasGroup.gameObject.SetActive(false);
    }
}

// Enhanced audio feedback for UI interactions - now works with SoundManager
