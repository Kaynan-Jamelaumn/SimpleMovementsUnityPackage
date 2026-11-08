using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The work surface of the station window: a picture drawn into a <see cref="CraftPixels"/> buffer (the bar on the
/// anvil, the hide on the beam, the crucible...) and the pointer on it, turned into the buffer's pixel coordinates
/// (origin bottom-left) and into what the hands did for the active minigame by a <see cref="CraftPointerSampler"/>: one
/// gesture per button (the left button's; the right button reports <see cref="CraftPointer.Cancel"/>, the middle button
/// nothing), samples by distance rather than once a frame, no drag threshold, the wheel in whole notches.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class CraftCanvasView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler, IDragHandler, IPointerMoveHandler, IScrollHandler, IPointerExitHandler
{
    private RawImage image;
    private CraftPixels pixels;
    private readonly CraftPointerSampler sampler = new CraftPointerSampler();
    private BaseInputModule notchModule;
    private float notchSize;

    /// <summary>The pointer: (phase, position in pixels, right button - true only for <see cref="CraftPointer.Cancel"/> -, notches of the wheel).</summary>
    public event Action<CraftPointer, Vector2, bool, float> Pointer;

    public CraftPixels Pixels => pixels;
    /// <summary>Where the pointer is over the surface (pixels), or (-1, -1) when it is not.</summary>
    public Vector2 Hover { get; private set; } = new Vector2(-1, -1);
    /// <summary>Is a gesture under way (the left button held since it pressed on the surface)? Other buttons do not count.</summary>
    public bool Held => sampler.Held;
    /// <summary>The time of the sample being reported (a filled-in sample of a long jump has its own, between the frames).</summary>
    public float SampleTime => sampler.SampleTime;

    private void Awake() => sampler.Sampled += (phase, at, time, scroll) => Pointer?.Invoke(phase, at, phase == CraftPointer.Cancel, scroll);

    public void Build(int width, int height)
    {
        image = GetComponent<RawImage>();
        pixels = new CraftPixels(width, height);
        pixels.Clear(new Color(0.05f, 0.05f, 0.07f, 1f));
        pixels.Apply();
        image.texture = pixels.Texture;
        image.raycastTarget = true;
    }

    /// <summary>Uploads what was drawn.</summary>
    public void Present()
    {
        if (pixels == null)
            return;
        pixels.Apply();
        if (image != null && image.texture != pixels.Texture) image.texture = pixels.Texture;
    }

    private void OnDestroy() => pixels?.Release();

    /// <summary>The window closed or stepped aside: a button let go there never comes back up here.</summary>
    private void OnDisable()
    {
        sampler.Reset();
        Hover = new Vector2(-1, -1);
    }

    /// <summary>A notch of the wheel still waiting is delivered (one a frame).</summary>
    private void Update() => sampler.Tick(Time.unscaledTime, Time.frameCount);

    private bool ToPixels(PointerEventData e, out Vector2 p)
    {
        p = default;
        var rt = (RectTransform)transform;
        if (pixels == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera != null ? e.pressEventCamera : e.enterEventCamera, out Vector2 local))
            return false;
        Rect r = rt.rect;
        p = new Vector2((local.x - r.xMin) / r.width * pixels.Width, (local.y - r.yMin) / r.height * pixels.Height);
        return true;
    }

    private bool OnSurface(Vector2 p) => pixels != null && p.x >= 0f && p.y >= 0f && p.x < pixels.Width && p.y < pixels.Height;

    private static CraftPointerButton ButtonOf(PointerEventData e) =>
        e.button == PointerEventData.InputButton.Right ? CraftPointerButton.Right
        : e.button == PointerEventData.InputButton.Middle ? CraftPointerButton.Middle : CraftPointerButton.Left;

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Middle || !ToPixels(e, out Vector2 p)) return;
        Hover = p;
        sampler.Press(ButtonOf(e), e.pointerId, p, Time.unscaledTime);
    }

    /// <summary>No drag threshold: the first pixels of a stroke count.</summary>
    public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

    public void OnDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || !ToPixels(e, out Vector2 p)) return;
        Hover = p;
        sampler.Drag(e.pointerId, p, Time.unscaledTime, Time.unscaledDeltaTime);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || !sampler.Held) return;
        if (!ToPixels(e, out Vector2 p)) p = Hover;
        bool over = OnSurface(p);
        Hover = over ? p : new Vector2(-1, -1); // let go off the surface: nothing is under the pointer any more
        sampler.Release(CraftPointerButton.Left, e.pointerId, p, Time.unscaledTime, Time.unscaledDeltaTime, over);
    }

    public void OnPointerMove(PointerEventData e)
    {
        if (!ToPixels(e, out Vector2 p)) return;
        Hover = p;
        if (!sampler.Held) sampler.Hover(p, Time.unscaledTime, Time.unscaledDeltaTime);
    }

    public void OnScroll(PointerEventData e)
    {
        if (!ToPixels(e, out Vector2 p)) return;
        sampler.Scroll(e.scrollDelta.y, NotchSize(), p, Time.unscaledTime, Time.frameCount);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (sampler.Held) return;
        Hover = new Vector2(-1, -1);
        sampler.Exit();
    }

    /// <summary>What one notch of the wheel reports in the input module in use (0 when it cannot be told: the sampler learns it).</summary>
    private float NotchSize()
    {
        BaseInputModule m = EventSystem.current != null ? EventSystem.current.currentInputModule : null;
        if (m == notchModule)
            return notchSize;
        notchModule = m;
        notchSize = 0f;
        if (m is StandaloneInputModule)
            notchSize = 1f; // the old input manager: one a notch
        else if (m != null)
        {
            // The Input System's UI module scales a notch by its scrollDeltaPerTick (in the versions that have it).
            PropertyInfo perTick = m.GetType().GetProperty("scrollDeltaPerTick", BindingFlags.Public | BindingFlags.Instance);
            if (perTick != null && perTick.PropertyType == typeof(float))
                notchSize = Mathf.Abs((float)perTick.GetValue(m));
        }
        return notchSize;
    }
}
