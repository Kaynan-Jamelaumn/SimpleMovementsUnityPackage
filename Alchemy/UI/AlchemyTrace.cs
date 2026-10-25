using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
