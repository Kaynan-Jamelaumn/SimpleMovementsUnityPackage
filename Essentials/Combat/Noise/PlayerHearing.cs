using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What a player hears. Every noise made in the world (<see cref="CombatEvents.EmitNoise"/>: footsteps, jumps and
/// landings, dodges, weapon swings and blows, abilities, explosions, things being used, a mob's own steps) that reaches
/// this player's ears - its radius times the player's hearing, less far through walls, exactly as for mobs - is heard:
/// <see cref="Heard"/> fires, and the local player's screen shows an arc around its middle in the direction the noise
/// came from, brighter and wider the louder it arrives. So a player can tell that someone is running behind a wall, or
/// that another player is sneaking up - and can stay unheard by standing still or crouching.
/// <para>Only what the player can't already see is shown (Combat Settings ▸ Noise ▸ Hide Seen Sources), and not its own
/// party unless Hear Allies is on. It works by itself: players without the component get one (Combat Settings ▸ Noise ▸
/// Player Hearing). Only the machine that controls the player listens; on a network client the other characters' noises
/// are made there from what that machine sees of them.</para>
/// </summary>
[DisallowMultipleComponent]
public class PlayerHearing : MonoBehaviour
{
    /// <summary>One noise as it reached the player.</summary>
    public struct HeardNoise
    {
        public NoiseEvent noise;
        /// <summary>Metres from the player's ears.</summary>
        public float distance;
        /// <summary>1 next to it, 0 at the edge of earshot.</summary>
        public float loudness;
        /// <summary>From the player towards the noise, flat (zero when it is right on top).</summary>
        public Vector3 direction;
        /// <summary>Was it shown on screen (false: its maker is in plain sight, or an ally)?</summary>
        public bool shown;
    }

    [Tooltip("Multiplies how far this player hears (on top of Combat Settings ▸ Noise ▸ Player Hearing Multiplier and the Hearing stat).")]
    [SerializeField, Range(0f, 4f)] private float hearing = 1f;
    [Tooltip("Show where noises come from: arcs around the middle of the screen.")]
    [SerializeField] private bool showIndicators = true;
    [Tooltip("Most arcs shown at once (the oldest gives way).")]
    [SerializeField, Range(1, 16)] private int maxIndicators = 8;
    [Tooltip("Seconds an arc stays after its noise (it fades out).")]
    [SerializeField, Min(0.2f)] private float indicatorTime = 1.4f;
    [Tooltip("Radius of the ring of arcs, in pixels of a 1080p screen.")]
    [SerializeField, Range(80f, 500f)] private float ringRadius = 230f;
    [Tooltip("Noises quieter than this on arrival are heard (the event fires) but not shown.")]
    [SerializeField, Range(0f, 0.9f)] private float showAbove = 0.05f;

    /// <summary>A noise reached this player (only on the machine that controls it).</summary>
    public event Action<HeardNoise> Heard;
    /// <summary>A noise reached a player controlled on this machine.</summary>
    public static event Action<PlayerHearing, HeardNoise> AnyHeard;

    /// <summary>Multiplies how far this player hears.</summary>
    public float Hearing { get => hearing; set => hearing = Mathf.Max(0f, value); }

    /// <summary>The character that hears.</summary>
    public CombatEntity Entity
    {
        get
        {
            if (entity == null)
                entity = CombatEntity.Resolve(gameObject);
            if (entity == null)
                entity = GetComponentInParent<CombatEntity>();
            return entity;
        }
    }

    private sealed class Arc
    {
        public Image image;
        public CombatEntity source;
        public Vector3 position;
        public float until;
        public float strength;
        public Color color;
    }

    private CombatEntity entity;
    private readonly List<Arc> arcs = new List<Arc>();
    private Canvas canvas;
    private Camera cam;
    private bool local;
    private float localCheckedAt = -10f;
    private bool subscribed;

    /// <summary>Gives a player a Player Hearing when it has none and Combat Settings ▸ Noise ▸ Player Hearing is on.</summary>
    public static void EnsureFor(CombatEntity e)
    {
        if (e == null || !Application.isPlaying || e.Kind != CombatEntity.EntityKind.Player)
            return;
        if (!CombatSettings.Instance.noise.playerHearing)
            return;
        if (e.GetComponentInChildren<PlayerHearing>(true) != null || e.GetComponentInParent<PlayerHearing>() != null)
            return;
        e.gameObject.AddComponent<PlayerHearing>();
    }

    private void OnEnable()
    {
        if (subscribed)
            return;
        subscribed = true;
        CombatEvents.Noise += OnNoise;
    }

    private void OnDisable()
    {
        if (subscribed)
            CombatEvents.Noise -= OnNoise;
        subscribed = false;
        HideAll();
    }

    private void OnDestroy()
    {
        if (canvas != null)
            Destroy(canvas.gameObject);
    }

    /// <summary>Is this the player controlled on this machine (looked up again every second: ownership arrives after spawning)?</summary>
    private bool IsLocal()
    {
        if (Time.unscaledTime - localCheckedAt < 1f)
            return local;
        localCheckedAt = Time.unscaledTime;
        CombatEntity e = Entity;
        if (e == null)
            return local = false;
        if (!GameNet.NetAuthority.IsNetworkedObject(e.gameObject))
            return local = GameNet.NetAuthority.HasLocalPlayer;
        return local = GameNet.NetAuthority.IsLocallyControlled(e.gameObject);
    }

    /// <summary>Where the player's ears are.</summary>
    public Vector3 EarPosition
    {
        get
        {
            CombatEntity e = Entity;
            return e != null ? e.BasePosition + Vector3.up * (e.Height * 0.9f) : transform.position + Vector3.up * 1.6f;
        }
    }

    private void OnNoise(NoiseEvent n)
    {
        CombatEntity self = Entity;
        if (self == null || n.source == self || !IsLocal() || self.IsDead)
            return;
        CombatNoiseSettings ns = CombatSettings.Instance.noise;
        float h = hearing * ns.playerHearingMultiplier * CombatNoise.HearingStat(self);
        if (h <= 0f)
            return;
        // Out of earshot even in the open (most noises): nothing more to work out.
        float open = n.radius * h;
        Vector3 ear = EarPosition;
        if ((n.position - ear).sqrMagnitude > open * open)
            return;
        if (n.source != null && CombatRules.AreSeparated(self, n.source))
            return;   // made in another realm (they overlap in space)
        if (!CombatNoise.Reaches(n, ear, h, out float distance, out float loudness))
            return;

        Vector3 dir = n.position - ear;
        dir.y = 0f;
        var heard = new HeardNoise
        {
            noise = n,
            distance = distance,
            loudness = loudness,
            direction = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.zero,
        };
        heard.shown = showIndicators && loudness >= showAbove && Worth(self, n, ns);
        if (heard.shown)
            Show(n, loudness, ns);
        Heard?.Invoke(heard);
        AnyHeard?.Invoke(this, heard);
    }

    /// <summary>Is the noise worth an arc: not the player's own side (unless asked for), not something in plain sight?</summary>
    private bool Worth(CombatEntity self, in NoiseEvent n, CombatNoiseSettings ns)
    {
        CombatEntity source = n.source;
        if (source != null && !ns.hearAllies)
        {
            if (source.Summoner == self || CombatParties.SameParty(self, source))
                return false;
        }
        if (source != null && ns.hideSeenSources && InPlainSight(source))
            return false;
        return true;
    }

    /// <summary>Is the character on screen with nothing between it and the camera?</summary>
    private bool InPlainSight(CombatEntity source)
    {
        Camera c = Cam();
        if (c == null)
            return false;
        Vector3 at = source.Center;
        Vector3 view = c.WorldToViewportPoint(at);
        if (view.z <= 0f || view.x < 0f || view.x > 1f || view.y < 0f || view.y > 1f)
            return false;
        return CombatQuery.HasLineOfSight(c.transform.position, at);
    }

    private Camera Cam()
    {
        if (cam == null || !cam.isActiveAndEnabled)
            cam = Camera.main;
        return cam;
    }

    // ------------------------------------------------------------------ the arcs
    private void Show(in NoiseEvent n, float loudness, CombatNoiseSettings ns)
    {
        if (canvas == null)
            Build();
        // The same maker again (its next step): its arc moves there and is refreshed. Else a free arc, a new one, or
        // the one that has the least time left.
        float now = Time.time;
        Arc same = null, free = null, oldest = null;
        for (int i = 0; i < arcs.Count; i++)
        {
            Arc a = arcs[i];
            bool active = now < a.until;
            if (active && n.source != null && a.source == n.source)
            {
                same = a;
                break;
            }
            if (!active && free == null)
                free = a;
            if (oldest == null || a.until < oldest.until)
                oldest = a;
        }
        Arc arc = same != null ? same : free;
        if (arc == null && arcs.Count < maxIndicators)
        {
            arc = new Arc { image = NewArc() };
            arcs.Add(arc);
        }
        if (arc == null)
            arc = oldest;
        if (arc == null)
            return;
        arc.source = n.source;
        arc.position = n.position;
        arc.until = now + indicatorTime;
        // (A loud noise of the same maker is not hidden by its next quiet step.)
        arc.strength = same != null ? Mathf.Max(arc.strength * 0.7f, loudness) : loudness;
        arc.color = n.source == null ? ns.objectNoiseColor
            : n.source.Kind == CombatEntity.EntityKind.Player ? ns.playerNoiseColor
            : n.source.Kind == CombatEntity.EntityKind.Mob ? ns.mobNoiseColor : ns.objectNoiseColor;
    }

    private void LateUpdate()
    {
        if (arcs.Count == 0)
            return;
        if (canvas == null)
        {
            // Its scene was unloaded: the arcs are gone with it (built again by the next noise).
            arcs.Clear();
            return;
        }
        Camera c = Cam();
        Vector3 forward = c != null ? c.transform.forward : transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f)
            forward = transform.forward;
        Vector3 ear = EarPosition;
        float now = Time.time;
        for (int i = 0; i < arcs.Count; i++)
        {
            Arc a = arcs[i];
            if (a.image == null)
                continue;
            float left = a.until - now;
            if (left <= 0f)
            {
                if (a.image.enabled)
                    a.image.enabled = false;
                a.source = null;
                continue;
            }
            Vector3 to = a.position - ear;
            to.y = 0f;
            // Clockwise from straight ahead (0 = in front, 90 = to the right, 180 = behind).
            float angle = to.sqrMagnitude > 0.01f ? Vector3.SignedAngle(forward, to, Vector3.up) : 0f;
            float fill = Mathf.Lerp(0.045f, 0.13f, a.strength);
            float fade = Mathf.Clamp01(left / Mathf.Max(0.05f, indicatorTime * 0.6f));
            a.image.fillAmount = fill;
            // The filled part starts at the top and runs clockwise: turned so that its middle points at the noise.
            a.image.rectTransform.localEulerAngles = new Vector3(0f, 0f, -(angle - fill * 180f));
            Color col = a.color;
            col.a *= Mathf.Lerp(0.35f, 1f, a.strength) * fade;
            a.image.color = col;
            if (!a.image.enabled)
                a.image.enabled = true;
        }
    }

    private void HideAll()
    {
        for (int i = 0; i < arcs.Count; i++)
        {
            if (arcs[i].image != null)
                arcs[i].image.enabled = false;
            arcs[i].until = 0f;
            arcs[i].source = null;
        }
    }

    private void Build()
    {
        canvas = UITheme.Canvas("Hearing", 45, null);
        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;
        arcs.Clear();
    }

    private Image NewArc()
    {
        Image img = HudStyle.Image("Noise", canvas.transform, Color.clear, ArcSprite());
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Top;
        img.fillClockwise = true;
        img.fillAmount = 0.08f;
        img.enabled = false;
        HudStyle.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * ringRadius * 2f);
        return img;
    }

    private static Sprite arcSprite;

    /// <summary>A thin soft ring: a filled slice of it is one arc.</summary>
    private static Sprite ArcSprite()
    {
        if (arcSprite != null)
            return arcSprite;
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Hearing Ring", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[size * size];
        float c = size * 0.5f, radius = size * 0.5f - 8f, half = 4.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                // Solid in the band, fading over a few pixels on both sides.
                float a = Mathf.Clamp01(1f - (Mathf.Abs(d - radius) - half) / 3f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        arcSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        arcSprite.name = "Hearing Ring";
        arcSprite.hideFlags = HideFlags.DontSave;
        return arcSprite;
    }
}
