using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What a character's disabilities do to the sight and the hearing of the player who controls it. Trait behaviours
/// (<see cref="VisionImpairmentTrait"/>, <see cref="HearingImpairmentTrait"/>, <see cref="EpisodicDisabilityTrait"/>)
/// register what they take away; this adds it up and, for the local player only, draws it:
/// <list type="bullet">
/// <item><b>Sight</b>: an eye's half of the screen dark (the left eye the left half; both eyes: nearly black), a haze
/// (poor eyesight), the edges closed in (tunnel vision), a dizzy pulse.</item>
/// <item><b>Hearing</b>: the game's volume, down to silence.</item>
/// </list>
/// Purely what the player perceives: what a disability does to the character is its trait's modifiers and limits.
/// Drawn with plain UI overlays (no post-processing, any render pipeline). Added to the player by the traits that need
/// it; it works with the injury system on or off (injuries have their own overlay, and both add up on the screen).
/// </summary>
[DisallowMultipleComponent]
public class DisabilityEffects : MonoBehaviour
{
    private struct Sight
    {
        public BodyPartSide eye;
        public VisionEffect effect;
        public float strength;
    }

    [Tooltip("Scales what disabilities do to the screen (0 = nothing is drawn; the traits' other effects stay).")]
    [SerializeField, Range(0f, 1f)] private float sightStrength = 1f;
    [Tooltip("Disabilities change the game's volume for the local player (off = hearing traits only keep their other effects).")]
    [SerializeField] private bool changeVolume = true;

    private readonly Dictionary<object, Sight> sights = new Dictionary<object, Sight>();
    private readonly Dictionary<object, float> hearings = new Dictionary<object, float>();
    private InjuryVision vision;
    private float hearing = 1f;
    private bool dirty = true;

    private CombatEntity entity;
    private bool local;
    private float nextLocalCheck;
    private Canvas canvas;
    private Image leftEye, rightEye, blind, haze, tunnel;
    private float left, right, hazeShown, tunnelShown, swayShown, volumeShown = 1f;

    private static Sprite sideSprite, vignetteSprite;
    private static DisabilityEffects volumeOwner;
    private static float baseVolume = 1f;

    private static readonly Color Dark = new Color(0.01f, 0.01f, 0.015f, 1f);
    private static readonly Color HazeColor = new Color(0.78f, 0.8f, 0.82f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sideSprite = null;
        vignetteSprite = null;
        volumeOwner = null;
        baseVolume = 1f;
    }

    /// <summary>The character's (added when <paramref name="create"/> and it has none).</summary>
    public static DisabilityEffects For(Component onPlayer, bool create = true)
    {
        if (onPlayer == null)
            return null;
        PlayerStatusController controller = onPlayer.GetComponentInParent<PlayerStatusController>();
        GameObject host = controller != null ? controller.gameObject : onPlayer.transform.root.gameObject;
        DisabilityEffects fx = host.GetComponentInChildren<DisabilityEffects>(true);
        if (fx == null && create && Application.isPlaying)
            fx = host.AddComponent<DisabilityEffects>();
        return fx;
    }

    /// <summary>What the disabilities take from the sight right now, every source together.</summary>
    public InjuryVision Vision
    {
        get
        {
            Refresh();
            return vision;
        }
    }

    /// <summary>How much of the sound is left (1 = all, 0 = deaf), every source together.</summary>
    public float Hearing
    {
        get
        {
            Refresh();
            return hearing;
        }
    }

    /// <summary>
    /// Sets what <paramref name="key"/> does to the sight: <paramref name="eye"/> Left or Right for one eye's darkness,
    /// Center for both; <paramref name="strength"/> scales it (0 removes it). The same key replaces its own.
    /// </summary>
    public void SetSight(object key, BodyPartSide eye, VisionEffect effect, float strength)
    {
        if (key == null)
            return;
        if (effect == null || !effect.HasAny || strength <= 0.001f)
        {
            ClearSight(key);
            return;
        }
        if (sights.TryGetValue(key, out Sight old) && old.eye == eye && ReferenceEquals(old.effect, effect) && Mathf.Abs(old.strength - strength) < 0.005f)
            return;
        sights[key] = new Sight { eye = eye, effect = effect, strength = Mathf.Clamp01(strength) };
        dirty = true;
    }

    public void ClearSight(object key)
    {
        if (key != null && sights.Remove(key))
            dirty = true;
    }

    /// <summary>Sets how much sound <paramref name="key"/> leaves (1 = all, 0 = none). Several multiply.</summary>
    public void SetHearing(object key, float volume)
    {
        if (key == null)
            return;
        volume = Mathf.Clamp01(volume);
        if (volume >= 0.999f)
        {
            ClearHearing(key);
            return;
        }
        if (hearings.TryGetValue(key, out float old) && Mathf.Abs(old - volume) < 0.005f)
            return;
        hearings[key] = volume;
        dirty = true;
    }

    public void ClearHearing(object key)
    {
        if (key != null && hearings.Remove(key))
            dirty = true;
    }

    private void Refresh()
    {
        if (!dirty)
            return;
        dirty = false;
        var v = new InjuryVision();
        foreach (Sight s in sights.Values)
        {
            float k = s.strength;
            if (s.eye != BodyPartSide.Right) v.left = Mathf.Max(v.left, s.effect.darkness * k);
            if (s.eye != BodyPartSide.Left) v.right = Mathf.Max(v.right, s.effect.darkness * k);
            v.haze = Mathf.Max(v.haze, s.effect.haze * k);
            v.tunnel = Mathf.Max(v.tunnel, s.effect.tunnel * k);
            v.sway = Mathf.Max(v.sway, s.effect.sway * k);
        }
        vision = v;
        float h = 1f;
        foreach (float f in hearings.Values)
            h *= f;
        hearing = h;
    }

    private bool IsLocal()
    {
        if (Time.unscaledTime < nextLocalCheck)
            return local;
        nextLocalCheck = Time.unscaledTime + 1f;
        if (entity == null)
            entity = CombatEntity.Resolve(gameObject) ?? GetComponentInParent<CombatEntity>() ?? GetComponentInChildren<CombatEntity>();
        if (entity == null)
            return local = false;
        local = GameNet.NetAuthority.IsNetworkedObject(entity.gameObject)
            ? GameNet.NetAuthority.IsLocallyControlled(entity.gameObject)
            : GameNet.NetAuthority.HasLocalPlayer;
        return local;
    }

    private void Update()
    {
        bool mine = IsLocal();
        Refresh();
        bool dead = entity != null && entity.IsDead;
        InjuryVision want = mine && !dead ? vision : default;
        float k = sightStrength;
        float dt = Time.unscaledDeltaTime;
        left = Mathf.MoveTowards(left, want.left * k, dt * 1.2f);
        right = Mathf.MoveTowards(right, want.right * k, dt * 1.2f);
        hazeShown = Mathf.MoveTowards(hazeShown, want.haze * k, dt * 0.6f);
        tunnelShown = Mathf.MoveTowards(tunnelShown, want.tunnel * k, dt * 0.6f);
        swayShown = Mathf.MoveTowards(swayShown, want.sway * k, dt * 0.6f);

        UpdateVolume(mine && changeVolume ? hearing : 1f, dt);

        bool any = left > 0.002f || right > 0.002f || hazeShown > 0.002f || tunnelShown > 0.002f || swayShown > 0.002f;
        if (!any)
        {
            if (canvas != null && canvas.gameObject.activeSelf)
                canvas.gameObject.SetActive(false);
            return;
        }
        if (canvas == null)
            Build();
        if (!canvas.gameObject.activeSelf)
            canvas.gameObject.SetActive(true);

        float t = Time.unscaledTime;
        float sway = swayShown * (0.5f + 0.5f * Mathf.Sin(t * 1.7f) * Mathf.Sin(t * 0.63f + 1.3f));
        Show(leftEye, Dark, Mathf.Clamp01(left * 1.05f));
        Show(rightEye, Dark, Mathf.Clamp01(right * 1.05f));
        // Both eyes: the middle the two halves leave lit goes too (a glimmer stays: the game can still be played).
        Show(blind, Dark, Mathf.Min(left, right) * 0.9f);
        Show(haze, HazeColor, Mathf.Clamp01(hazeShown * 0.5f + sway * 0.12f));
        float edges = Mathf.Clamp01(tunnelShown + sway * 0.35f);
        Show(tunnel, Dark, edges);
        if (tunnel.enabled)
        {
            float reach = 1f + (1f - edges) * 0.35f;
            tunnel.rectTransform.localScale = new Vector3(reach, reach, 1f);
        }
    }

    /// <summary>The game's volume follows the hearing, for the one local player whose hearing is impaired.</summary>
    private void UpdateVolume(float target, float dt)
    {
        if (target >= 0.999f && volumeOwner != this)
            return;
        if (volumeOwner == null)
        {
            volumeOwner = this;
            baseVolume = AudioListener.volume;
            volumeShown = 1f;
        }
        if (volumeOwner != this)
            return;
        volumeShown = Mathf.MoveTowards(volumeShown, target, dt * 1.5f);
        AudioListener.volume = baseVolume * volumeShown;
        if (target >= 0.999f && volumeShown >= 0.999f)
            ReleaseVolume();
    }

    private void ReleaseVolume()
    {
        if (volumeOwner != this)
            return;
        AudioListener.volume = baseVolume;
        volumeOwner = null;
        volumeShown = 1f;
    }

    private void OnDisable()
    {
        ReleaseVolume();
        left = right = hazeShown = tunnelShown = swayShown = 0f;
        if (canvas != null)
            canvas.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        ReleaseVolume();
        if (canvas != null)
            Destroy(canvas.gameObject);
    }

    private static void Show(Image image, Color color, float alpha)
    {
        bool on = alpha > 0.004f;
        if (image.enabled != on)
            image.enabled = on;
        if (on)
            image.color = new Color(color.r, color.g, color.b, alpha);
    }

    private void Build()
    {
        canvas = UITheme.Canvas("Disability Screen", 43);
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;
        Color clear = new Color(Dark.r, Dark.g, Dark.b, 0f);
        leftEye = HudStyle.Image("Left Eye", canvas.transform, clear, Side());
        UITheme.Place(leftEye.rectTransform, new Vector2(0f, 0f), new Vector2(0.64f, 1f), Vector2.zero, Vector2.zero);
        rightEye = HudStyle.Image("Right Eye", canvas.transform, clear, Side());
        UITheme.Place(rightEye.rectTransform, new Vector2(0.36f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        rightEye.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        blind = HudStyle.Image("Blind", canvas.transform, clear);
        HudStyle.Stretch(blind.rectTransform);
        haze = HudStyle.Image("Haze", canvas.transform, new Color(HazeColor.r, HazeColor.g, HazeColor.b, 0f));
        HudStyle.Stretch(haze.rectTransform);
        tunnel = HudStyle.Image("Tunnel", canvas.transform, clear, Vignette());
        HudStyle.Stretch(tunnel.rectTransform);
    }

    /// <summary>Opaque at the left edge, fading out to the right (an eye's half of the screen).</summary>
    private static Sprite Side()
    {
        if (sideSprite != null)
            return sideSprite;
        const int width = 128;
        var tex = new Texture2D(width, 2, TextureFormat.RGBA32, false) { name = "Disability Eye", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[width * 2];
        for (int x = 0; x < width; x++)
        {
            float f = (x + 0.5f) / width;
            byte a = (byte)Mathf.RoundToInt((1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, f))) * 255f);
            px[x] = px[width + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        sideSprite = Sprite.Create(tex, new Rect(0, 0, width, 2), new Vector2(0.5f, 0.5f), 100f);
        sideSprite.hideFlags = HideFlags.DontSave;
        return sideSprite;
    }

    /// <summary>A clear centre that darkens toward the edges.</summary>
    private static Sprite Vignette()
    {
        if (vignetteSprite != null)
            return vignetteSprite;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Disability Vignette", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                byte a = (byte)Mathf.RoundToInt(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 0.92f, d)) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        vignetteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        vignetteSprite.hideFlags = HideFlags.DontSave;
        return vignetteSprite;
    }
}
