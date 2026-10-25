using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TraitTooltip : MonoBehaviour
{
    [Header("Tooltip Settings")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private float showDelay = 0.5f;

    [Header("Audio Integration")]
    [SerializeField] private CharacterCreationUI mainUI;
    [SerializeField] private bool playHoverSound = false;
    [SerializeField] private string hoverSoundName = "UI_Hover";

    private Coroutine showCoroutine;

    private void Awake()
    {
        if (mainUI == null)
            mainUI = FindAnyObjectByType<CharacterCreationUI>();
    }

    public void ShowTooltip(string text, Vector3 position)
    {
        if (showCoroutine != null)
            StopCoroutine(showCoroutine);

        showCoroutine = StartCoroutine(ShowTooltipDelayed(text, position));
    }

    public void HideTooltip()
    {
        if (showCoroutine != null)
        {
            StopCoroutine(showCoroutine);
            showCoroutine = null;
        }

        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    private System.Collections.IEnumerator ShowTooltipDelayed(string text, Vector3 position)
    {
        yield return new WaitForSeconds(showDelay);

        if (tooltipText != null)
            tooltipText.text = text;

        if (tooltipPanel != null)
        {
            tooltipPanel.transform.position = position;
            tooltipPanel.SetActive(true);
        }

        // Play hover sound if enabled
        if (playHoverSound && mainUI != null && !string.IsNullOrEmpty(hoverSoundName))
        {
            mainUI.PlayUISound(hoverSoundName);
        }

        showCoroutine = null;
    }
}
