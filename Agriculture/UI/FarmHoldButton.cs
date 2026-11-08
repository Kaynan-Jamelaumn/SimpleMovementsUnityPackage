using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>A button that is held: it reports how long it was held when let go (digging, pouring).</summary>
public class FarmHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public event Action<float> Released;
    public float MaxSeconds = 12f;

    private float started = -1f;
    private Button button;

    public bool Holding => started >= 0f;
    public float Seconds => started >= 0f ? Mathf.Min(MaxSeconds, Time.unscaledTime - started) : 0f;

    private void Awake() => button = GetComponent<Button>();

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || (button != null && !button.interactable)) return;
        started = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData eventData) => Finish();

    public void OnPointerExit(PointerEventData eventData) => Finish();

    private void OnDisable() => started = -1f;

    private void Update()
    {
        if (Holding && Seconds >= MaxSeconds) Finish();
    }

    private void Finish()
    {
        if (started < 0f) return;
        float s = Seconds;
        started = -1f;
        Released?.Invoke(s);
    }
}
