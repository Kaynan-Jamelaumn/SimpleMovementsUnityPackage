using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What injuries do to the local player's screen (Injury Settings ▸ Show Screen Effects):
/// <list type="bullet">
/// <item>a hurt eye darkens its half of the screen - the left eye the left half - up to black for a blinded eye; with
/// both eyes blinded only a dim glimmer is left;</item>
/// <item>a haze (a swollen eye, a fever), the edges closing in (blood loss, shock, a concussion), a dizzy pulse;</item>
/// <item>losing a lot of blood closes the edges in by itself, redder the lower the health;</item>
/// <item>a short red flash at the edges when a new injury appears.</item>
/// </list>
/// Purely visual: the gameplay side is the injury stages' consequences. Drawn with plain UI overlays, so it needs no
/// post-processing and works in every render pipeline. Created by the player's <see cref="InjuryController"/>, which
/// works out the sight (<see cref="InjuryController.Vision"/>) from each stage's Vision.
/// </summary>
public class InjuryScreenEffect : MonoBehaviour
{
    [SerializeField] private InjuryController injuries;
    [SerializeField] private Image leftEye;
    [SerializeField] private Image rightEye;
    [SerializeField] private Image blind;
    [SerializeField] private Image haze;
    [SerializeField] private Image tunnel;
    [SerializeField] private Image flash;

    private float left, right, hazeShown, tunnelShown, swayShown, flashShown, bloodShown;
    private int knownInjuries = -1;

    private static Sprite sideGradient, vignette;

    private static readonly Color Dark = new Color(0.01f, 0.01f, 0.015f, 1f);
    private static readonly Color HazeColor = new Color(0.78f, 0.8f, 0.82f, 1f);
    private static readonly Color BloodDark = new Color(0.16f, 0.01f, 0.02f, 1f);
    private static readonly Color FlashColor = new Color(0.75f, 0.05f, 0.05f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sideGradient = null;
        vignette = null;
    }

    public static InjuryScreenEffect Create(InjuryController owner)
    {
        if (owner == null)
            return null;
        Canvas canvas = UITheme.Canvas("Injury Screen", 45);
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;
        var fx = canvas.gameObject.AddComponent<InjuryScreenEffect>();
        fx.injuries = owner;

        // Each eye's half: dark at its edge of the screen, fading out past the middle.
        fx.leftEye = HudStyle.Image("Left Eye", canvas.transform, Clear(Dark), SideGradient());
        UITheme.Place(fx.leftEye.rectTransform, new Vector2(0f, 0f), new Vector2(0.64f, 1f), Vector2.zero, Vector2.zero);
        fx.rightEye = HudStyle.Image("Right Eye", canvas.transform, Clear(Dark), SideGradient());
        UITheme.Place(fx.rightEye.rectTransform, new Vector2(0.36f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        fx.rightEye.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        fx.blind = HudStyle.Image("Blind", canvas.transform, Clear(Dark));
        HudStyle.Stretch(fx.blind.rectTransform);
        fx.haze = HudStyle.Image("Haze", canvas.transform, Clear(HazeColor));
        HudStyle.Stretch(fx.haze.rectTransform);
        fx.tunnel = HudStyle.Image("Tunnel", canvas.transform, Clear(Dark), Vignette());
        HudStyle.Stretch(fx.tunnel.rectTransform);
        fx.flash = HudStyle.Image("Flash", canvas.transform, Clear(FlashColor), Vignette());
        HudStyle.Stretch(fx.flash.rectTransform);
        return fx;
    }

    private static Color Clear(Color c) => new Color(c.r, c.g, c.b, 0f);

    /// <summary>Redraws (called by the controller every frame).</summary>
    public void Tick()
    {
        if (injuries == null || tunnel == null)
            return;
        CombatEntity e = injuries.Entity;
        bool dead = e != null && e.IsDead;
        InjuryVision v = dead ? default : injuries.Vision;

        // Blood loss: the less health is left while bleeding, the more the edges close in.
        float blood = 0f;
        if (!dead && e != null && injuries.IsBleeding)
        {
            float heavy = Mathf.Clamp01(injuries.BleedingPerSecond / Mathf.Max(0.01f, injuries.Settings.bleeding.heavyBleedingPerSecond));
            blood = Mathf.InverseLerp(0.75f, 0.15f, e.HealthRatio) * Mathf.Lerp(0.6f, 1f, heavy);
        }

        // A new injury: a short red flash at the edges.
        int count = injuries.VisibleInjuryCount + injuries.LostCount;
        if (knownInjuries >= 0 && count > knownInjuries && !dead)
            flashShown = 1f;
        knownInjuries = count;

        float dt = Time.unscaledDeltaTime;
        left = Mathf.MoveTowards(left, v.left, dt * 1.5f);
        right = Mathf.MoveTowards(right, v.right, dt * 1.5f);
        hazeShown = Mathf.MoveTowards(hazeShown, v.haze, dt * 0.6f);
        tunnelShown = Mathf.MoveTowards(tunnelShown, v.tunnel, dt * 0.6f);
        swayShown = Mathf.MoveTowards(swayShown, v.sway, dt * 0.6f);
        bloodShown = Mathf.MoveTowards(bloodShown, blood, dt * 0.4f);
        flashShown = Mathf.MoveTowards(flashShown, 0f, dt * 1.6f);

        float t = Time.unscaledTime;
        // Dizziness: everything breathes in and out, slowly and a little off beat.
        float sway = swayShown * (0.5f + 0.5f * Mathf.Sin(t * 1.7f) * Mathf.Sin(t * 0.63f + 1.3f));
        float heartbeat = 0.85f + 0.15f * Mathf.Sin(t * 5.2f);

        Set(leftEye, Dark, Mathf.Clamp01(left * 1.05f));
        Set(rightEye, Dark, Mathf.Clamp01(right * 1.05f));
        // Both eyes: the middle the two halves leave lit goes too (a glimmer stays, the game can still be played out).
        Set(blind, Dark, Mathf.Min(left, right) * 0.9f);
        Set(haze, HazeColor, Mathf.Clamp01(hazeShown * 0.42f + sway * 0.12f));
        float edges = Mathf.Clamp01(Mathf.Max(tunnelShown, bloodShown * heartbeat) + sway * 0.35f);
        Set(tunnel, bloodShown > tunnelShown ? BloodDark : Dark, edges);
        if (tunnel.enabled)
        {
            // The more it closes in, the further the dark reaches toward the middle.
            float reach = 1f + (1f - edges) * 0.35f + (1f - sway) * 0.06f;
            tunnel.rectTransform.localScale = new Vector3(reach, reach, 1f);
        }
        Set(flash, FlashColor, flashShown * 0.55f);
    }

    private static void Set(Image img, Color color, float alpha)
    {
        if (img == null)
            return;
        bool on = alpha > 0.004f;
        if (img.enabled != on)
            img.enabled = on;
        if (on)
            img.color = new Color(color.r, color.g, color.b, alpha);
    }

    private void OnDisable()
    {
        left = right = hazeShown = tunnelShown = swayShown = flashShown = bloodShown = 0f;
        knownInjuries = -1;
        Set(leftEye, Dark, 0f);
        Set(rightEye, Dark, 0f);
        Set(blind, Dark, 0f);
        Set(haze, HazeColor, 0f);
        Set(tunnel, Dark, 0f);
        Set(flash, FlashColor, 0f);
    }

    // ------------------------------------------------------------------ sprites
    /// <summary>Opaque at the left edge, fading out to the right (an eye's half of the screen).</summary>
    private static Sprite SideGradient()
    {
        if (sideGradient != null)
            return sideGradient;
        const int width = 128;
        var tex = new Texture2D(width, 2, TextureFormat.RGBA32, false) { name = "Injury Eye Gradient", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[width * 2];
        for (int x = 0; x < width; x++)
        {
            float f = (x + 0.5f) / width;
            // Solid over the outer three quarters of the half, then a soft edge.
            float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, f));
            var c = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            px[x] = c;
            px[width + x] = c;
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        sideGradient = Sprite.Create(tex, new Rect(0, 0, width, 2), new Vector2(0.5f, 0.5f), 100f);
        sideGradient.hideFlags = HideFlags.DontSave;
        return sideGradient;
    }

    /// <summary>A clear centre that darkens toward the edges.</summary>
    private static Sprite Vignette()
    {
        if (vignette != null)
            return vignette;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Injury Vignette", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 0.92f, d));
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        vignette = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        vignette.hideFlags = HideFlags.DontSave;
        return vignette;
    }
}
