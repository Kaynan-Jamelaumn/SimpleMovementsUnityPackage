using UnityEngine;

/// <summary>
/// How a station looks and sounds - presentation only, it never changes the rules. Reads the preparation every frame
/// (liquid colour and level, bubbling from boiling and reactions, steam, the fire) and plays effects for its events
/// (an ingredient splashing in, a hiss, a flash, an eruption, a burst vessel, bottling). Every reference is optional.
/// </summary>
[AddComponentMenu("SimpleMovements/Alchemy/Alchemy Station Visuals")]
public class AlchemyStationVisuals : MonoBehaviour
{
    [Tooltip("Empty = the station on this object or a parent.")]
    [SerializeField] private AlchemyStation station;

    [Header("Liquid")]
    [Tooltip("The liquid's surface (a flat disc): tinted with the mixture's colour, moved up with the fill level.")]
    [SerializeField] private Renderer liquid;
    [Tooltip("Local height of the surface when empty and when full.")]
    [SerializeField] private float emptyHeight = 0.1f;
    [SerializeField] private float fullHeight = 0.75f;
    [Tooltip("Seconds for the colour to follow the mixture.")]
    [SerializeField, Min(0f)] private float colorSmoothing = 0.6f;

    [Header("Particles")]
    [Tooltip("Bubbles: emission follows boiling and how much is reacting.")]
    [SerializeField] private ParticleSystem bubbles;
    [SerializeField, Min(0f)] private float maxBubbles = 40f;
    [Tooltip("Steam: emission follows how fast it boils off.")]
    [SerializeField] private ParticleSystem steam;
    [SerializeField, Min(0f)] private float maxSteam = 30f;
    [Tooltip("Bursts for smoky reactions, eruptions and bursts.")]
    [SerializeField] private ParticleSystem smoke;
    [Tooltip("Bursts for sparks and flashes.")]
    [SerializeField] private ParticleSystem sparks;

    [Header("Fire and glow")]
    [Tooltip("The fire under the vessel: intensity follows the heat setting.")]
    [SerializeField] private Light fireLight;
    [SerializeField, Min(0f)] private float maxFireIntensity = 3f;
    [Tooltip("Glows with the liquid's colour while glowing reactions run.")]
    [SerializeField] private Light glowLight;
    [SerializeField, Min(0f)] private float maxGlowIntensity = 1.5f;

    [Header("Sound")]
    [Tooltip("Plays the bubbling loop (its volume follows the bubbling).")]
    [SerializeField] private AudioSource loopSource;
    [Tooltip("Plays the one-shots.")]
    [SerializeField] private AudioSource oneShotSource;
    [SerializeField] private AudioClip bubblingLoop;
    [SerializeField] private AudioClip addClip;
    [SerializeField] private AudioClip hissClip;
    [SerializeField] private AudioClip flashClip;
    [SerializeField] private AudioClip eruptionClip;
    [SerializeField] private AudioClip ruptureClip;
    [SerializeField] private AudioClip bottleClip;

    private Color shown = new Color(0.5f, 0.55f, 0.6f, 0f);
    private MaterialPropertyBlock block;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public AlchemyStation Station => station != null ? station : station = GetComponentInParent<AlchemyStation>();

    private void OnEnable()
    {
        if (Station != null)
            Station.BrewEvent += OnEvent;
        if (loopSource != null && bubblingLoop != null)
        {
            loopSource.clip = bubblingLoop;
            loopSource.loop = true;
            loopSource.volume = 0f;
            loopSource.Play();
        }
    }

    private void OnDisable()
    {
        if (station != null)
            station.BrewEvent -= OnEvent;
    }

    private void Update()
    {
        AlchemyStation s = Station;
        if (s == null)
            return;
        AlchemyBrew brew = s.Brew;
        AlchemyMixture m = brew?.Mixture;
        float fill = m != null ? Mathf.Clamp01((float)(m.FilledVolume / Mathf.Max(1f, m.Equipment.capacity))) : 0f;

        // Liquid.
        Color target = m != null && m.LiquidVolume > 0.05 ? m.LiquidColor : new Color(shown.r, shown.g, shown.b, 0f);
        float k = colorSmoothing > 0f ? 1f - Mathf.Exp(-Time.deltaTime / colorSmoothing) : 1f;
        shown = Color.Lerp(shown, target, k);
        if (liquid != null)
        {
            bool visible = fill > 0.005f;
            if (liquid.enabled != visible) liquid.enabled = visible;
            Vector3 p = liquid.transform.localPosition;
            p.y = Mathf.Lerp(emptyHeight, fullHeight, fill);
            liquid.transform.localPosition = p;
            block ??= new MaterialPropertyBlock();
            liquid.GetPropertyBlock(block);
            Color c = new Color(shown.r, shown.g, shown.b, 1f);
            block.SetColor(BaseColor, c);
            block.SetColor(ColorId, c);
            liquid.SetPropertyBlock(block);
        }

        // Bubbles and steam.
        float boil = brew != null ? (float)brew.Tracker.BoilRate : 0f;
        float reacting = 0f;
        bool glowing = false;
        if (brew != null)
        {
            AlchemyRules rules = brew.Rules;
            for (int j = 0; j < rules.Reactions.Length; j++)
            {
                if (!brew.IsReacting(j)) continue;
                reacting += (float)brew.RateOf(j);
                glowing |= rules.Reactions[j].Definition.visual == ReactionVisual.Glow;
            }
        }
        float bubbling = Mathf.Clamp01(boil * 2f + reacting * 4f + (m != null && m.Temperature > 70 ? 0.1f : 0f));
        SetRate(bubbles, fill > 0.01f ? bubbling * maxBubbles : 0f);
        SetRate(steam, m != null && m.VaporMode == VaporHandling.Vent ? Mathf.Clamp01(boil * 3f) * maxSteam : 0f);
        if (loopSource != null && loopSource.clip != null)
            loopSource.volume = Mathf.MoveTowards(loopSource.volume, bubbling, Time.deltaTime);

        // Fire and glow.
        if (fireLight != null)
            fireLight.intensity = m != null ? Mathf.Max(0f, (float)m.EffectiveHeat) * maxFireIntensity : 0f;
        if (glowLight != null)
        {
            glowLight.color = shown;
            glowLight.intensity = Mathf.MoveTowards(glowLight.intensity, glowing ? maxGlowIntensity : 0f, Time.deltaTime * 2f);
        }
    }

    private static void SetRate(ParticleSystem ps, float rate)
    {
        if (ps == null)
            return;
        ParticleSystem.EmissionModule e = ps.emission;
        e.rateOverTime = rate;
        if (rate > 0.01f && !ps.isPlaying) ps.Play();
    }

    private void OnEvent(AlchemyEvent e)
    {
        switch (e.Kind)
        {
            case AlchemyEventKind.IngredientAdded:
            case AlchemyEventKind.SubstanceAdded:
                Play(addClip);
                break;
            case AlchemyEventKind.ReactionStarted:
            {
                AlchemyStation s = Station;
                CompiledReaction r = s != null ? s.Rules.Reaction(e.Id) : null;
                if (r == null) break;
                switch (r.Definition.visual)
                {
                    case ReactionVisual.Smoke: Burst(smoke, 20); break;
                    case ReactionVisual.Sparks: Burst(sparks, 25); break;
                    case ReactionVisual.Flash: Burst(sparks, 50); Play(flashClip); break;
                    case ReactionVisual.Hiss: Burst(smoke, 12); Play(hissClip); break;
                }
                break;
            }
            case AlchemyEventKind.Eruption:
                Burst(smoke, 40);
                Burst(sparks, 40);
                Play(eruptionClip);
                break;
            case AlchemyEventKind.VesselRuptured:
                Burst(smoke, 60);
                Play(ruptureClip);
                break;
            case AlchemyEventKind.Bottled:
            case AlchemyEventKind.DistillateCollected:
                Play(bottleClip);
                break;
        }
    }

    private static void Burst(ParticleSystem ps, int count)
    {
        if (ps != null)
            ps.Emit(count);
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && oneShotSource != null)
            oneShotSource.PlayOneShot(clip);
    }
}
