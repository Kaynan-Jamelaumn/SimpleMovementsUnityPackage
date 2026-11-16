using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What the needs look like to the local player (each condition's Screen Effect, see <see cref="NeedStage"/>):
/// <list type="bullet">
/// <item><b>Rest</b>: the eyelids. They droop as the character gets sleepy, and close in blinks that get longer and more
/// frequent - falling asleep, the screen keeps going black for a moment.</item>
/// <item><b>Hunger and thirst</b>: light-headed. The edges of the picture darken in slow pulses.</item>
/// </list>
/// Purely visual: what a condition does is its effect. Drawn with plain UI overlays (no post-processing, any render
/// pipeline). The need managers add it to the player by themselves; put it on the player yourself to change its
/// settings, or switch it off there.
/// </summary>
[DisallowMultipleComponent]
public class NeedScreenEffect : MonoBehaviour
{
    [Tooltip("Sleepiness closes the eyes on the screen.")]
    [SerializeField] private bool eyelids = true;
    [Tooltip("Hunger and thirst darken the edges of the screen in slow pulses.")]
    [SerializeField] private bool lightHeaded = true;
    [Tooltip("Scales both effects (0 = none).")]
    [SerializeField, Range(0f, 1f)] private float strength = 1f;
    [Tooltip("Colour of the eyelids and the darkened edges.")]
    [SerializeField] private Color dark = new Color(0.01f, 0.01f, 0.015f, 1f);

    private PlayerStatusController status;
    private CombatEntity entity;
    private Canvas canvas;
    private Image top, bottom, dim, edges;
    private float drowsy, faint, blink, blinkTime, blinkLength, nextBlink;
    private float nextLocalCheck;
    private bool local;

    private static Sprite lidSprite, vignetteSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        lidSprite = null;
        vignetteSprite = null;
    }

    /// <summary>Makes sure the player <paramref name="onPlayer"/> belongs to has one (the need managers call it when they start).</summary>
    public static void Ensure(Component onPlayer)
    {
        if (onPlayer == null || !Application.isPlaying)
            return;
        PlayerStatusController controller = onPlayer.GetComponentInParent<PlayerStatusController>();
        GameObject host = controller != null ? controller.gameObject : onPlayer.gameObject;
        if (host.GetComponentInChildren<NeedScreenEffect>(true) == null && host.GetComponentInParent<NeedScreenEffect>() == null)
            host.AddComponent<NeedScreenEffect>();
    }

    private void OnDestroy()
    {
        if (canvas != null)
            Destroy(canvas.gameObject);
    }

    private void OnDisable()
    {
        drowsy = faint = blink = 0f;
        if (canvas != null)
            canvas.gameObject.SetActive(false);
    }

    private bool IsLocal()
    {
        if (Time.unscaledTime < nextLocalCheck)
            return local;
        nextLocalCheck = Time.unscaledTime + 1f;
        if (entity == null)
            entity = CombatEntity.Resolve(gameObject) ?? GetComponentInParent<CombatEntity>() ?? GetComponentInChildren<CombatEntity>();
        if (status == null)
            status = GetComponentInParent<PlayerStatusController>() ?? GetComponentInChildren<PlayerStatusController>();
        if (entity == null)
            return local = false;
        local = GameNet.NetAuthority.IsNetworkedObject(entity.gameObject)
            ? GameNet.NetAuthority.IsLocallyControlled(entity.gameObject)
            : GameNet.NetAuthority.HasLocalPlayer;
        return local;
    }

    private static float ScreenOf(INeedStages need)
    {
        NeedStage stage = need != null ? need.CurrentStage : null;
        return stage != null ? stage.screenEffect : 0f;
    }

    private void Update()
    {
        if (!IsLocal() || status == null)
        {
            if (canvas != null && canvas.gameObject.activeSelf)
                canvas.gameObject.SetActive(false);
            return;
        }
        bool down = entity != null && (entity.IsDead || entity.IsUnconscious);
        SleepManager sleep = status.SleepManager;
        float wantDrowsy = 0f, wantFaint = 0f;
        if (!down)
        {
            // Asleep the Sleep Controller dims the screen itself.
            if (eyelids && sleep != null && sleep.isActiveAndEnabled && !sleep.IsSleeping)
                wantDrowsy = ScreenOf(sleep) * strength;
            if (lightHeaded)
            {
                HungerManager hunger = status.HungerManager;
                ThirstManager thirst = status.ThirstManager;
                if (hunger != null && hunger.isActiveAndEnabled) wantFaint = ScreenOf(hunger);
                if (thirst != null && thirst.isActiveAndEnabled) wantFaint = Mathf.Max(wantFaint, ScreenOf(thirst));
                wantFaint *= strength;
            }
        }
        float dt = Time.unscaledDeltaTime;
        drowsy = Mathf.MoveTowards(drowsy, wantDrowsy, dt * 0.35f);
        faint = Mathf.MoveTowards(faint, wantFaint, dt * 0.35f);
        if (drowsy <= 0.001f && faint <= 0.001f && blink <= 0.001f)
        {
            if (canvas != null && canvas.gameObject.activeSelf)
                canvas.gameObject.SetActive(false);
            return;
        }
        if (canvas == null)
            Build();
        if (!canvas.gameObject.activeSelf)
            canvas.gameObject.SetActive(true);

        // Blinks: rarer and short when a little sleepy, long and frequent when falling asleep.
        float t = Time.unscaledTime;
        float closed = 0f;
        if (blinkLength > 0f)
        {
            blinkTime += dt;
            float k = Mathf.Clamp01(blinkTime / blinkLength);
            // Closes quickly, stays shut for a while, opens slowly.
            closed = k < 0.25f ? Mathf.SmoothStep(0f, 1f, k / 0.25f) : k < 0.6f ? 1f : Mathf.SmoothStep(1f, 0f, (k - 0.6f) / 0.4f);
            if (k >= 1f)
            {
                blinkLength = 0f;
                nextBlink = t + Mathf.Lerp(11f, 2.2f, drowsy) * Random.Range(0.7f, 1.3f);
            }
        }
        else if (drowsy > 0.05f && t >= nextBlink)
        {
            blinkTime = 0f;
            blinkLength = Mathf.Lerp(0.35f, 1.9f, drowsy) * Random.Range(0.85f, 1.2f);
        }
        float droop = drowsy * 0.2f;
        blink = Mathf.Max(droop, closed * Mathf.Lerp(0.55f, 1f, drowsy));
        if (drowsy <= 0.05f && blinkLength <= 0f)
            blink = Mathf.MoveTowards(blink, 0f, dt);

        // Each lid covers its half of the screen as it closes (a little more than half: they overlap when shut).
        float reach = blink * 0.56f;
        top.rectTransform.anchorMin = new Vector2(0f, 1f - reach);
        bottom.rectTransform.anchorMax = new Vector2(1f, reach);
        Show(top, reach > 0.002f ? 1f : 0f);
        Show(bottom, reach > 0.002f ? 1f : 0f);
        Show(dim, Mathf.Clamp01(drowsy * 0.16f + closed * drowsy * 0.3f));
        Show(edges, Mathf.Clamp01(faint * (0.5f + 0.18f * Mathf.Sin(t * 1.1f))));
    }

    private void Show(Image image, float alpha)
    {
        bool on = alpha > 0.004f;
        if (image.enabled != on)
            image.enabled = on;
        if (on)
            image.color = new Color(dark.r, dark.g, dark.b, alpha);
    }

    private void Build()
    {
        canvas = UITheme.Canvas("Need Screen", 44);
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;
        edges = HudStyle.Image("Light-headed", canvas.transform, Clear(), Vignette());
        HudStyle.Stretch(edges.rectTransform);
        dim = HudStyle.Image("Heavy Eyes", canvas.transform, Clear());
        HudStyle.Stretch(dim.rectTransform);
        top = HudStyle.Image("Upper Lid", canvas.transform, Clear(), Lid());
        UITheme.Place(top.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        bottom = HudStyle.Image("Lower Lid", canvas.transform, Clear(), Lid());
        UITheme.Place(bottom.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
        bottom.rectTransform.localScale = new Vector3(1f, -1f, 1f);
        nextBlink = Time.unscaledTime + 1.5f;
    }

    private Color Clear() => new Color(dark.r, dark.g, dark.b, 0f);

    /// <summary>An eyelid: solid from its outer edge, a soft rim where it meets the eye (the bottom of the texture).</summary>
    private static Sprite Lid()
    {
        if (lidSprite != null)
            return lidSprite;
        const int height = 64;
        var tex = new Texture2D(2, height, TextureFormat.RGBA32, false) { name = "Eyelid", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[2 * height];
        for (int y = 0; y < height; y++)
        {
            float f = (y + 0.5f) / height; // 0 = the rim, 1 = the outer edge
            byte a = (byte)Mathf.RoundToInt(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.22f, f)) * 255f);
            px[y * 2] = px[y * 2 + 1] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        lidSprite = Sprite.Create(tex, new Rect(0, 0, 2, height), new Vector2(0.5f, 0.5f), 100f);
        lidSprite.hideFlags = HideFlags.DontSave;
        return lidSprite;
    }

    /// <summary>A clear centre that darkens toward the edges.</summary>
    private static Sprite Vignette()
    {
        if (vignetteSprite != null)
            return vignetteSprite;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Need Vignette", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                byte a = (byte)Mathf.RoundToInt(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.95f, d)) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        vignetteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        vignetteSprite.hideFlags = HideFlags.DontSave;
        return vignetteSprite;
    }
}
