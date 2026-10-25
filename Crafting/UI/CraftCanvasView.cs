using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>What the pointer did on the work surface.</summary>
public enum CraftPointer { Down, Drag, Up, Move, Scroll }

/// <summary>
/// The work surface of the station window: a picture drawn into a <see cref="CraftPixels"/> buffer (the bar on the
/// anvil, the hide on the beam, the crucible...) and the pointer on it, turned into the buffer's pixel coordinates
/// (origin bottom-left) for the active minigame.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class CraftCanvasView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IPointerMoveHandler, IScrollHandler, IPointerExitHandler
{
    private RawImage image;
    private CraftPixels pixels;

    /// <summary>The pointer: (phase, position in pixels, right button, scroll delta).</summary>
    public event Action<CraftPointer, Vector2, bool, float> Pointer;

    public CraftPixels Pixels => pixels;
    /// <summary>Where the pointer is over the surface (pixels), or (-1, -1) when it is not.</summary>
    public Vector2 Hover { get; private set; } = new Vector2(-1, -1);
    public bool Held { get; private set; }

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

    public void OnPointerDown(PointerEventData e)
    {
        if (!ToPixels(e, out Vector2 p)) return;
        Held = true;
        Hover = p;
        Pointer?.Invoke(CraftPointer.Down, p, e.button == PointerEventData.InputButton.Right, 0f);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!ToPixels(e, out Vector2 p)) return;
        Hover = p;
        Pointer?.Invoke(CraftPointer.Drag, p, e.button == PointerEventData.InputButton.Right, 0f);
    }

    public void OnPointerUp(PointerEventData e)
    {
        Held = false;
        if (!ToPixels(e, out Vector2 p)) p = Hover;
        Pointer?.Invoke(CraftPointer.Up, p, e.button == PointerEventData.InputButton.Right, 0f);
    }

    public void OnPointerMove(PointerEventData e)
    {
        if (!ToPixels(e, out Vector2 p)) return;
        Hover = p;
        if (!Held) Pointer?.Invoke(CraftPointer.Move, p, false, 0f);
    }

    public void OnScroll(PointerEventData e)
    {
        if (!ToPixels(e, out Vector2 p)) return;
        Pointer?.Invoke(CraftPointer.Scroll, p, false, e.scrollDelta.y);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (!Held) Hover = new Vector2(-1, -1);
    }
}
