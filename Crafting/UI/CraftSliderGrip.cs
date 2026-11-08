using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The hand on a work area's slider (<see cref="CraftMode.Slide"/>, <see cref="CraftMode.SettingSlider"/>): knows when the
/// handle is held (uGUI's slider does not say), shows the slider's value beside it, and for a setting the hand turns
/// paces what is sent to the station (<see cref="CraftSettingPace"/>: at once, at most 5 times a second while dragged,
/// the last value when let go) and shows the workshop's value again only once it is not under the hand.
/// </summary>
[DisallowMultipleComponent]
public class CraftSliderGrip : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private Slider slider;
    private TextMeshProUGUI shown;
    private Func<float, string> format;
    private readonly CraftSettingPace pace = new CraftSettingPace();
    private Func<float> read;
    private Action<float> write;
    private float tolerance;

    /// <summary>Is the hand on the slider (pressed on it and not yet let go)?</summary>
    public bool Held => pace.Held;

    /// <summary>The grip of <paramref name="s"/> (added the first time).</summary>
    public static CraftSliderGrip On(Slider s)
    {
        CraftSliderGrip g = s.GetComponent<CraftSliderGrip>();
        if (g == null)
        {
            g = s.gameObject.AddComponent<CraftSliderGrip>();
            g.slider = s;
            s.onValueChanged.AddListener(g.OnChanged);
        }
        return g;
    }

    /// <summary>Shows the slider's value in <paramref name="label"/>, worded by <paramref name="text"/>.</summary>
    public void ShowValue(TextMeshProUGUI label, Func<float, string> text)
    {
        shown = label;
        format = text;
        Show(slider != null ? slider.value : 0f);
    }

    /// <summary>
    /// Makes it a setting the hand turns: <paramref name="send"/> is called at the pace of <see cref="CraftSettingPace"/>,
    /// and the workshop's value (<paramref name="current"/>) is shown when it may be (within <paramref name="near"/> of the
    /// value sent counts as arrived).
    /// </summary>
    public void Setting(Func<float> current, Action<float> send, float near)
    {
        read = current;
        write = send;
        tolerance = near;
    }

    /// <summary>Updates the value shown beside the slider.</summary>
    public void Show(float value)
    {
        if (shown == null)
            return;
        string text = format != null ? format(value) : value.ToString("0.##");
        if (shown.text != text) shown.text = text;
    }

    private void OnChanged(float value)
    {
        Show(value);
        if (write == null)
            return;
        pace.Changed(value);
        if (pace.Due(Time.unscaledTime, out float v)) write(v);
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) pace.Grab();
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && pace.Held) LetGo();
    }

    /// <summary>The hand comes off the slider (let go, or the area closes under it): a value still waiting is sent now.</summary>
    public void LetGo()
    {
        if (pace.LetGo(Time.unscaledTime, out float v) && write != null) write(v);
    }

    private void OnDisable() => LetGo();

    private void Update()
    {
        if (write == null || slider == null)
            return;
        if (pace.Due(Time.unscaledTime, out float v)) write(v);
        if (read == null)
            return;
        float now = read();
        if (float.IsNaN(now))
            return;
        if (pace.MayShow(Time.unscaledTime, now, tolerance) && Mathf.Abs(slider.value - now) > tolerance)
        {
            slider.SetValueWithoutNotify(now);
            Show(slider.value);
        }
    }
}
