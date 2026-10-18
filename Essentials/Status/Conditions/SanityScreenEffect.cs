using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What low sanity looks and sounds like to the local player: the edges of the screen darken and pulse, and whispers
/// play now and then (stronger at deeper stages, see each Sanity Stage's Screen Effect). Purely visual: the gameplay
/// consequences are the Sanity Manager's stages. Put it on the player (Player Setup Validator adds it); it builds its own
/// overlay at runtime.
/// </summary>
[DisallowMultipleComponent]
public class SanityScreenEffect : MonoBehaviour
{
    [Tooltip("Colour of the darkened edges.")]
    [SerializeField] private Color tint = new Color(0.12f, 0.02f, 0.16f, 1f);
    [Tooltip("Most opacity of the edges (at a Screen Effect of 1).")]
    [SerializeField, Range(0f, 1f)] private float maxOpacity = 0.75f;
    [Tooltip("Pulse speed (per second).")]
    [SerializeField, Min(0f)] private float pulseSpeed = 1.3f;
    [Tooltip("Whispers played at random while sanity is low (optional).")]
    [SerializeField] private AudioClip[] whispers = new AudioClip[0];
    [Tooltip("Average seconds between two whispers at the strongest stage (weaker stages wait longer).")]
    [SerializeField, Min(1f)] private float whisperInterval = 25f;
    [SerializeField, Range(0f, 1f)] private float whisperVolume = 0.6f;

    private SanityManager sanity;
    private CombatEntity entity;
    private Image overlay;
    private AudioSource audioSource;
    private float shown;
    private float nextWhisper;

    private void Awake()
    {
        sanity = GetComponentInChildren<SanityManager>() ?? GetComponentInParent<SanityManager>();
    }

    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay.canvas.gameObject);
    }

    private bool IsLocal()
    {
        if (entity == null) entity = CombatEntity.Resolve(gameObject) ?? GetComponentInParent<CombatEntity>();
        if (entity == null) return false;
        if (!GameNet.NetAuthority.IsNetworkedObject(entity.gameObject)) return GameNet.NetAuthority.HasLocalPlayer;
        return GameNet.NetAuthority.IsLocallyControlled(entity.gameObject);
    }

    private void Update()
    {
        if (sanity == null || !IsLocal())
            return;
        float target = entity != null && entity.IsDead ? 0f : sanity.ScreenEffect;
        shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 0.5f);
        if (shown <= 0.001f && overlay == null)
            return;
        if (overlay == null) Build();
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f);
        overlay.color = new Color(tint.r, tint.g, tint.b, maxOpacity * shown * pulse);
        overlay.enabled = shown > 0.001f;
        Whisper(target);
    }

    private void Whisper(float strength)
    {
        if (whispers == null || whispers.Length == 0 || strength <= 0.05f)
            return;
        if (nextWhisper <= 0f) nextWhisper = Time.time + whisperInterval / strength;
        if (Time.time < nextWhisper) return;
        nextWhisper = Time.time + Random.Range(0.5f, 1.5f) * whisperInterval / strength;
        AudioClip clip = whispers[Random.Range(0, whispers.Length)];
        if (clip == null) return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        audioSource.panStereo = Random.Range(-0.8f, 0.8f);
        audioSource.PlayOneShot(clip, whisperVolume * strength);
    }

    private void Build()
    {
        Canvas canvas = UITheme.Canvas("Sanity Screen", 50, null);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;
        overlay = HudStyle.Image("Edges", canvas.transform, new Color(0f, 0f, 0f, 0f), Vignette());
        HudStyle.Stretch(overlay.rectTransform);
    }

    private static Sprite vignette;

    // A transparent centre that darkens toward the edges.
    private static Sprite Vignette()
    {
        if (vignette != null) return vignette;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, d));
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        vignette = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        vignette.hideFlags = HideFlags.DontSave;
        return vignette;
    }
}
