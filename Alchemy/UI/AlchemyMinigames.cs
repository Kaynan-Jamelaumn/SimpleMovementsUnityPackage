using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A round pad the player drags circles on (the pestle in the mortar, the spoon in the vessel). It follows the pointer
/// around its centre and reports how fast and how steadily it turns: the angular speed (turns per second, smoothed),
/// the full turns made and how long each took. Presentation only: what the turns do is decided by the window that
/// reads it (grinding progress, stirring speed).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class AlchemyCirclePad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const int KeptTurns = 8;

    private RectTransform rect;
    private RectTransform marker;
    private float lastAngle, accumulated, lastMove, turnStarted;
    private bool hasAngle;
    private float speed;
    private readonly List<float> turnTimes = new List<float>();

    /// <summary>The pointer is held on the pad.</summary>
    public bool Dragging { get; private set; }
    /// <summary>Turns per second (smoothed; falls to 0 when the pointer stops).</summary>
    public float TurnsPerSecond => speed;
    /// <summary>Full turns since <see cref="ResetTurns"/>.</summary>
    public int Turns { get; private set; }
    /// <summary>Seconds each of the last full turns took (oldest first).</summary>
    public IReadOnlyList<float> TurnTimes => turnTimes;
    /// <summary>Raised on every full turn.</summary>
    public event System.Action Turned;

    /// <summary>The image that follows the pointer (the pestle, the spoon).</summary>
    public void SetMarker(RectTransform m) => marker = m;

    private void Awake() => rect = (RectTransform)transform;

    public void ResetTurns()
    {
        Turns = 0;
        accumulated = 0f;
        turnTimes.Clear();
        turnStarted = Time.unscaledTime;
    }

    /// <summary>A quarter turn without dragging (the Pound / Stir buttons, keyboard play).</summary>
    public void Nudge()
    {
        accumulated += Mathf.PI / 2f;
        lastMove = Time.unscaledTime;
        CountTurns();
    }

    public void OnPointerDown(PointerEventData e)
    {
        Dragging = true;
        hasAngle = false;
        if (turnTimes.Count == 0 && Turns == 0) turnStarted = Time.unscaledTime;
        Track(e);
    }

    public void OnDrag(PointerEventData e) => Track(e);

    public void OnPointerUp(PointerEventData e)
    {
        Dragging = false;
        hasAngle = false;
    }

    private void Track(PointerEventData e)
    {
        if (rect == null) rect = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out Vector2 local))
            return;
        local -= rect.rect.center;
        float radius = Mathf.Min(rect.rect.width, rect.rect.height) * 0.5f;
        if (marker != null)
            marker.anchoredPosition = Vector2.ClampMagnitude(local, radius * 0.85f);
        if (local.magnitude < radius * 0.12f)
            return; // too close to the centre to read a direction
        float angle = Mathf.Atan2(local.y, local.x);
        float now = Time.unscaledTime;
        if (hasAngle)
        {
            float delta = Mathf.DeltaAngle(lastAngle * Mathf.Rad2Deg, angle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float dt = Mathf.Max(0.005f, now - lastMove);
            float instant = Mathf.Abs(delta) / (2f * Mathf.PI) / dt;
            speed = Mathf.Lerp(speed, Mathf.Min(instant, 6f), Mathf.Clamp01(dt * 6f));
            accumulated += Mathf.Abs(delta);
            CountTurns();
        }
        lastAngle = angle;
        lastMove = now;
        hasAngle = true;
    }

    private void CountTurns()
    {
        while (accumulated >= 2f * Mathf.PI)
        {
            accumulated -= 2f * Mathf.PI;
            Turns++;
            float now = Time.unscaledTime;
            turnTimes.Add(Mathf.Max(0.05f, now - turnStarted));
            turnStarted = now;
            if (turnTimes.Count > KeptTurns) turnTimes.RemoveAt(0);
            Turned?.Invoke();
        }
    }

    private void Update()
    {
        // The pointer resting (or released): the turning slows down.
        if (Time.unscaledTime - lastMove > 0.12f)
            speed = Mathf.MoveTowards(speed, 0f, Time.unscaledDeltaTime * 4f);
    }

    /// <summary>How steady the last turns were: 1 = every turn took the same time, 0 = erratic (fewer than three turns: 0.5).</summary>
    public float Rhythm
    {
        get
        {
            if (turnTimes.Count < 3)
                return 0.5f;
            float mean = 0f;
            foreach (float t in turnTimes) mean += t;
            mean /= turnTimes.Count;
            float var = 0f;
            foreach (float t in turnTimes) var += (t - mean) * (t - mean);
            float cv = Mathf.Sqrt(var / turnTimes.Count) / Mathf.Max(0.01f, mean);
            return Mathf.Clamp01(1f - 2f * cv);
        }
    }
}

/// <summary>A row of thin bars showing the last minute of a value (the temperature): how well the fire is held.</summary>
public class AlchemyTrace : MonoBehaviour
{
    private readonly List<Image> bars = new List<Image>();
    private readonly List<float> values = new List<float>();
    private int count;

    /// <summary>Builds <paramref name="n"/> bars inside this rect (once).</summary>
    public void Build(int n)
    {
        count = n;
        var rt = (RectTransform)transform;
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(rt, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2((float)i / n, 0f);
            r.anchorMax = new Vector2((float)(i + 1) / n, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.offsetMin = new Vector2(0.5f, 0f);
            r.offsetMax = new Vector2(-0.5f, 0f);
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0f);
            bars.Add(img);
        }
    }

    public void Clear() => values.Clear();

    public void Push(float v)
    {
        values.Add(v);
        if (values.Count > count) values.RemoveAt(0);
    }

    /// <summary>Redraws between <paramref name="min"/> and <paramref name="max"/>, coloured by <paramref name="color"/>.</summary>
    public void Redraw(float min, float max, System.Func<float, Color> color)
    {
        var rt = (RectTransform)transform;
        float h = rt.rect.height;
        int offset = count - values.Count;
        for (int i = 0; i < bars.Count; i++)
        {
            int k = i - offset;
            Image b = bars[i];
            if (k < 0)
            {
                b.color = new Color(1f, 1f, 1f, 0.04f);
                b.rectTransform.sizeDelta = new Vector2(b.rectTransform.sizeDelta.x, 2f);
                continue;
            }
            float t = Mathf.InverseLerp(min, max, values[k]);
            b.color = color(values[k]);
            b.rectTransform.sizeDelta = new Vector2(b.rectTransform.sizeDelta.x, Mathf.Max(2f, t * h));
        }
    }
}
