using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How a station looks and sounds - presentation only, it never changes the rules. It reads the preparation's actual
/// state (<see cref="AlchemyLook"/>) and shows it in the vessel:
/// <list type="bullet">
/// <item>the liquid rises with what is poured in and takes the mixture's colour - clear, hazy or murky; two layers lie
/// one over the other (stirred, they swirl into one); froth on top; what floats bobs on the surface as chunks and
/// drifts round with the stirring; what sinks lies at the bottom as sediment; pieces of ingredients bob, soften and
/// sink as they give up their substances; streaks of colour curl through a poorly mixed brew;</item>
/// <item>bubbles follow the heat (a shimmer, then small bubbles, then a rolling boil) and the reactions; steam, smoke
/// (scorching when it is too hot), glow and drifting specks follow the state;</item>
/// <item>the fire bed shows the fuel feeding it - logs, charcoal, resin, a dish of oil - burning down, embers glowing
/// with the flames, smoke when the fire is choked;</item>
/// <item>the props worked with the hands (<see cref="AlchemyHandsOn"/>): the wooden spoon, the bellows by the fire, the
/// filter stand on the table.</item>
/// </list>
/// With Auto Build, all of it is made from primitives (place this on the vessel and set its inner size); assign your own
/// renderers, particle systems and lights to use your art instead - every reference is optional. Events play effects
/// and sounds (an ingredient splashing in, a hiss, a flash, an eruption, a burst vessel, bottling).
/// </summary>
[AddComponentMenu("SimpleMovements/Alchemy/Alchemy Station Visuals")]
public class AlchemyStationVisuals : MonoBehaviour
{
    [Tooltip("Empty = the station on this object or a parent.")]
    [SerializeField] private AlchemyStation station;

    [Header("Vessel")]
    [Tooltip("Build the contents (liquid, layers, froth, sediment, chunks, bubbles, steam, smoke, fire bed) and the props " +
             "(spoon, bellows, filter stand) from primitives. Off = only the renderers and effects assigned below.")]
    [SerializeField] private bool autoBuild = true;
    [Tooltip("Build the spoon, the bellows (manual fires) and the filter stand.")]
    [SerializeField] private bool buildProps = true;
    [Tooltip("The space the vessel's inner size is measured in: its inner bottom centre at height 0... (empty = the Liquid's " +
             "parent when one is assigned, else this object). Heights and radii below are in its local units.")]
    [SerializeField] private Transform vesselSpace;
    [Tooltip("Inner radius of the vessel at the brim (local units of the vessel space).")]
    [SerializeField, Min(0.02f)] private float innerRadius = 0.45f;
    [Tooltip("Inner radius at the bottom, as a share of the brim's (a round-bellied cauldron is narrower at the bottom).")]
    [SerializeField, Range(0.2f, 1f)] private float bottomRadiusScale = 0.75f;
    [Tooltip("A belly wider (or narrower) than the brim: its inner radius as a share of the brim's (0 = none: the walls run " +
             "straight from the bottom to the brim).")]
    [SerializeField, Range(0f, 2f)] private float bellyRadiusScale;
    [Tooltip("Where the belly is, between the inner bottom (0) and the brim (1).")]
    [SerializeField, Range(0.05f, 0.95f)] private float bellyAt = 0.45f;
    [Tooltip("Optional material for the liquid (cloned and tinted; a see-through one shows what has sunk).")]
    [SerializeField] private Material liquidMaterial;

    [Header("Liquid")]
    [Tooltip("The liquid's surface (a flat disc): tinted with the mixture's colour, moved up with the fill level. Auto Build makes one.")]
    [SerializeField] private Renderer liquid;
    [Tooltip("Local height of the surface when empty (the inner bottom) and when full (the brim).")]
    [SerializeField] private float emptyHeight = 0.1f;
    [SerializeField] private float fullHeight = 0.75f;
    [Tooltip("Seconds for the colour to follow the mixture.")]
    [SerializeField, Min(0f)] private float colorSmoothing = 0.6f;

    [Header("Particles")]
    [Tooltip("Bubbles: emission follows the heat, the boil and how much is reacting. Auto Build makes them.")]
    [SerializeField] private ParticleSystem bubbles;
    [SerializeField, Min(0f)] private float maxBubbles = 40f;
    [Tooltip("Steam: emission follows how fast it boils off.")]
    [SerializeField] private ParticleSystem steam;
    [SerializeField, Min(0f)] private float maxSteam = 30f;
    [Tooltip("Smoke: smoky reactions and scorching (and bursts for eruptions).")]
    [SerializeField] private ParticleSystem smoke;
    [Tooltip("Bursts for sparks and flashes.")]
    [SerializeField] private ParticleSystem sparks;

    [Header("Fire and glow")]
    [Tooltip("Where the fire burns (empty = under the vessel). The fuel feeding it is shown there.")]
    [SerializeField] private Transform fireBed;
    [Tooltip("The fire under the vessel: intensity follows the flames. Auto Build makes one.")]
    [SerializeField] private Light fireLight;
    [SerializeField, Min(0f)] private float maxFireIntensity = 3f;
    [Tooltip("Glows with the liquid's colour while glowing reactions run.")]
    [SerializeField] private Light glowLight;
    [SerializeField, Min(0f)] private float maxGlowIntensity = 1.5f;

    [Header("Props")]
    [Tooltip("Where the bellows lie (empty = beside the fire, on the side away from the vessel's front).")]
    [SerializeField] private Transform bellowsPoint;
    [Tooltip("Where the filter stand is (empty = on the table to the right of the vessel).")]
    [SerializeField] private Transform filterPoint;
    [Tooltip("Where the mortar and pestle are (stations with a mortar; empty = on the table to the left of the vessel).")]
    [SerializeField] private Transform mortarPoint;

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
    [SerializeField] private AudioClip bellowsClip;
    [SerializeField] private AudioClip pourClip;
    [SerializeField] private AudioClip poundClip;

    private Color shown = new Color(0.5f, 0.55f, 0.6f, 0f);
    private MaterialPropertyBlock block;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly AlchemyLook look = new AlchemyLook();
    private float lookTimer = 1f;
    private bool built, propsBuilt;

    // Smoothed state (the look is read a few times a second; everything moves smoothly between readings).
    private float level, fill, foam, sediment, floating, cloud, streaks, glow, smokeRate, emulsion, topShare;
    private Color topColor, bottomColor, floatColor, sedimentColor;
    private float flowAngle, flowSpeed;
    private float flicker;

    // Built pieces.
    private Transform root, fireRoot;
    private Renderer body, bottomBody, surface, foamDisc, sedimentDisc;
    private ParticleSystem simmer, specks, flames, fireSmoke;
    private readonly List<Floater> floaters = new List<Floater>();
    private readonly List<Transform> pieces = new List<Transform>();
    private readonly List<Renderer> streakArcs = new List<Renderer>();

    private struct Floater
    {
        public Transform t;
        public Renderer r;
        public float radius, angle, phase, size;
    }

    // The fuel on the fire, oldest first (rebuilt from the history; the oldest burns away first).
    private struct FuelPiece
    {
        public string id;
        public float units;
        public Transform t;
        public Renderer r;
        public Vector3 baseScale;
    }

    private readonly List<FuelPiece> fuel = new List<FuelPiece>();
    private int fuelHistory = -1;
    private AlchemyBrew fuelBrew;
    private Transform oilDish, oilLevel;

    // Props.
    private Transform spoon, bellows, bellowsTop, bellowsLeather, bellowsNozzle, filterStand, funnel, funnelLiquid, filterMedium,
        flask, flaskLiquid, jug, jugLiquid, stream;
    private ParticleSystem bellowsPuff, overflow;
    private Transform mortarProp, pestle;
    private ParticleSystem mortarDust;
    private float mortarUnit, mortarBottomY, mortarDepth;
    private bool pestleHeld;
    private Vector3 pestleHead;
    private bool spoonHeld;
    private Vector3 spoonTip, spoonDir;
    private float bellowsOpen = 1f, bellowsShown = 1f;
    private bool filtering;
    private float filterTilt, filterPour, funnelFill, flaskFill;
    private Color filterColor = Color.clear;

    public AlchemyStation Station => station != null ? station : station = GetComponentInParent<AlchemyStation>();

    /// <summary>Where the vessel's heights and radii are measured (see Vessel Space).</summary>
    private Transform Space => vesselSpace != null ? vesselSpace : liquid != null && liquid.transform.parent != null ? liquid.transform.parent : transform;

    /// <summary>The inner radius in this object's own units (the props are built under it, unscaled by the vessel).</summary>
    private float PropRadius => WorldRadius / Mathf.Max(1e-4f, Mathf.Abs(transform.lossyScale.x));

    // ------------------------------------------------------------------ geometry (for the hands-on modes)
    /// <summary>The liquid's surface height now (local).</summary>
    public float SurfaceLocalY => Mathf.Lerp(emptyHeight, fullHeight, fill);
    /// <summary>The middle of the liquid's surface (world).</summary>
    public Vector3 SurfaceCenter => Space.TransformPoint(new Vector3(0f, SurfaceLocalY, 0f));
    /// <summary>The middle of the brim (world).</summary>
    public Vector3 BrimCenter => Space.TransformPoint(new Vector3(0f, fullHeight, 0f));
    /// <summary>The inner radius at the brim (world units).</summary>
    public float WorldRadius => innerRadius * Mathf.Abs(Space.lossyScale.x);
    /// <summary>The inner radius at the surface now (world units).</summary>
    public float WorldRadiusAtSurface => RadiusAt(fill) * Mathf.Abs(Space.lossyScale.x);
    /// <summary>Where the fire burns (world).</summary>
    public Vector3 FireBedPoint => fireBed != null ? fireBed.position : Space.TransformPoint(new Vector3(0f, emptyHeight, 0f)) - Vector3.up * WorldRadius * 0.7f;
    /// <summary>Where the bellows are (world): their nozzle points at the fire.</summary>
    public Transform BellowsTransform => bellows;
    public Transform FilterStand => filterStand;
    public Transform Spoon => spoon;
    public Transform Funnel => funnel;
    /// <summary>The mortar on the table (stations with a mortar) and its pestle.</summary>
    public Transform Mortar => mortarProp;
    public Transform Pestle => pestle;
    /// <summary>The middle of the mortar's inner bottom (world).</summary>
    public Vector3 MortarBottom => mortarProp != null ? mortarProp.TransformPoint(new Vector3(0f, mortarBottomY, 0f)) : transform.position;
    /// <summary>The mortar bowl's working radius (world): the unit of <see cref="AlchemyMortar"/> positions.</summary>
    public float MortarRadius => mortarProp != null ? mortarUnit * Mathf.Abs(mortarProp.lossyScale.x) : 0.1f;

    /// <summary>
    /// A point in the mortar's bowl for <see cref="AlchemyMortar"/> coordinates (<paramref name="x"/>, <paramref name="y"/>),
    /// <paramref name="lift"/> above where the bowl curves up (world).
    /// </summary>
    public Vector3 MortarPoint(float x, float y, float lift = 0f)
    {
        float rr = x * x + y * y;
        return mortarProp.TransformPoint(new Vector3(x * mortarUnit, mortarBottomY + rr * mortarDepth * 0.45f + lift, y * mortarUnit));
    }

    /// <summary>A blow of the pestle: a knock and a puff of dust of <paramref name="color"/>.</summary>
    public void KnockMortar(Color color)
    {
        if (mortarDust != null && pestle != null)
        {
            mortarDust.transform.position = pestle.position;
            ParticleSystem.MainModule main = mortarDust.main;
            main.startColor = new Color(color.r, color.g, color.b, 0.6f);
            mortarDust.Emit(10);
        }
        Play(poundClip);
    }

    /// <summary>The pestle in the hand: its head at <paramref name="head"/> (world); not held = resting in the mortar.</summary>
    public void HoldPestle(bool held, Vector3 head)
    {
        pestleHeld = held;
        pestleHead = head;
    }
    /// <summary>The vessel's front: toward the player tending it (else this object's forward).</summary>
    public Vector3 Front
    {
        get
        {
            AlchemyStation s = Station;
            Vector3 f = s != null && s.Tender != null ? s.Tender.transform.position - transform.position : transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
        }
    }

    private float RadiusAt(float fillShare)
    {
        float f = Mathf.Clamp01(fillShare);
        if (bellyRadiusScale <= 0f)
            return innerRadius * Mathf.Lerp(bottomRadiusScale, 1f, f);
        return innerRadius * (f < bellyAt ? Mathf.Lerp(bottomRadiusScale, bellyRadiusScale, f / bellyAt)
                                          : Mathf.Lerp(bellyRadiusScale, 1f, (f - bellyAt) / (1f - bellyAt)));
    }

    // ------------------------------------------------------------------ lifecycle
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

    private void Start()
    {
        if (autoBuild && Application.isPlaying)
            Build();
    }

    private void Update()
    {
        AlchemyStation s = Station;
        if (s == null)
            return;
        AlchemyBrew brew = s.Brew;
        float dt = Time.deltaTime;
        lookTimer += dt;
        if (lookTimer >= 0.08f)
        {
            lookTimer = 0f;
            look.Read(brew);
        }
        AlchemyMixture m = brew?.Mixture;
        float k = colorSmoothing > 0f ? 1f - Mathf.Exp(-dt / colorSmoothing) : 1f;
        float quick = 1f - Mathf.Exp(-dt / 0.25f);

        // What the liquid is.
        fill = Mathf.Lerp(fill, look.Fill, quick);
        Color target = look.HasLiquid ? look.Color : new Color(shown.r, shown.g, shown.b, 0f);
        shown = Color.Lerp(shown, target, k);
        topColor = Color.Lerp(topColor, look.Layered ? look.TopColor : look.Color, k);
        bottomColor = Color.Lerp(bottomColor, look.Layered ? look.BottomColor : look.Color, k);
        floatColor = Color.Lerp(floatColor, look.FloatingColor, k);
        sedimentColor = Color.Lerp(sedimentColor, look.SedimentColor, k);
        foam = Mathf.Lerp(foam, look.Foam, quick);
        sediment = Mathf.Lerp(sediment, look.Sediment, quick * 0.5f);
        floating = Mathf.Lerp(floating, look.Floating, quick);
        cloud = Mathf.Lerp(cloud, look.Cloudiness, k);
        streaks = Mathf.Lerp(streaks, look.Streaks, quick);
        glow = Mathf.Lerp(glow, look.Glow, quick);
        smokeRate = Mathf.Lerp(smokeRate, look.Smoke, quick);
        emulsion = Mathf.Lerp(emulsion, look.Emulsion, quick);
        topShare = Mathf.Lerp(topShare, look.TopShare, quick);
        // The swirl: stirring sets it going, it slows by itself (faster in a thick brew); the spoon sets its direction.
        float agitation = m != null ? (float)m.Agitation : 0f;
        float wanted = agitation * 3.2f * (1f - look.Thickness * 0.6f) * (flowSpeed < 0f ? -1f : 1f);
        flowSpeed = Mathf.Lerp(flowSpeed, wanted, 1f - Mathf.Exp(-dt * (0.8f + look.Thickness * 2f)));
        flowAngle += flowSpeed * dt;

        level = Mathf.Lerp(emptyHeight, fullHeight, fill);
        if (built) DrawContents(dt);
        else DrawAssignedLiquid();

        // Bubbles, steam, smoke.
        float bubbling = look.Bubbling;
        SetRate(bubbles, look.HasLiquid && fill > 0.01f ? bubbling * maxBubbles : 0f);
        SetRate(simmer, look.HasLiquid ? (look.Simmer * 0.6f + look.ReactionBubbles) * maxBubbles * 0.6f : 0f);
        SetRate(steam, look.Steam * maxSteam);
        SetRate(smoke, smokeRate * 24f);
        SetRate(specks, look.HasLiquid ? cloud * 30f : 0f);
        if (specks != null)
        {
            ParticleSystem.MainModule main = specks.main;
            main.startColor = Color.Lerp(floatColor, sedimentColor, 0.5f);
        }
        if (loopSource != null && loopSource.clip != null)
            loopSource.volume = Mathf.MoveTowards(loopSource.volume, bubbling, dt);

        // Fire and glow.
        float flames01 = m != null ? Mathf.Max(0f, (float)m.EffectiveHeat) : 0f;
        flicker = Mathf.Lerp(flicker, Random.Range(0.8f, 1.2f), 1f - Mathf.Exp(-dt * 12f));
        if (fireLight != null)
            fireLight.intensity = flames01 * maxFireIntensity * (m != null && m.FireManual ? flicker : 1f);
        if (glowLight != null)
        {
            glowLight.color = shown;
            glowLight.intensity = Mathf.MoveTowards(glowLight.intensity, glow * maxGlowIntensity, dt * 2f);
        }
        if (built) DrawFire(brew, m, dt);
        if (propsBuilt) DrawProps(dt);
    }

    // ------------------------------------------------------------------ building
    /// <summary>Builds the contents and props from primitives (called on Start with Auto Build; safe to call again).</summary>
    public void Build()
    {
        if (built)
            return;
        built = true;
        root = AlchemyVesselParts.Group("Alchemy Contents", Space);
        float r = innerRadius;

        // The liquid: a column (see-through), a second one for the bottom layer, and its surface on top.
        body = Column("Liquid", root, 3000);
        bottomBody = Column("Bottom Layer", root, 2999);
        surface = liquid;
        if (surface == null)
        {
            GameObject disc = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "Surface", root, new Color(0.5f, 0.6f, 0.7f, 0.8f), true, 0.85f);
            surface = disc.GetComponent<Renderer>();
            surface.sharedMaterial.renderQueue = 3001;
        }
        foamDisc = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "Froth", root, new Color(0.95f, 0.93f, 0.85f, 0f), true, 0.1f).GetComponent<Renderer>();
        foamDisc.sharedMaterial.renderQueue = 3002;
        sedimentDisc = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "Sediment", root, new Color(0.35f, 0.3f, 0.25f), false, 0.05f).GetComponent<Renderer>();

        // What floats (chunks bobbing on the surface) and streaks of a poor mix.
        for (int i = 0; i < 28; i++)
        {
            GameObject chunk = AlchemyVesselParts.Primitive(i % 3 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere, "Floating " + i, root, new Color(0.4f, 0.35f, 0.3f), false, 0.1f);
            floaters.Add(new Floater
            {
                t = chunk.transform, r = chunk.GetComponent<Renderer>(),
                radius = Mathf.Sqrt(Random.value) * 0.85f, angle = Random.value * Mathf.PI * 2f, phase = Random.value * 10f, size = Random.Range(0.5f, 1.3f),
            });
            chunk.SetActive(false);
        }
        for (int i = 0; i < 9; i++)
        {
            GameObject piece = AlchemyVesselParts.Primitive(i % 2 == 0 ? PrimitiveType.Capsule : PrimitiveType.Cube, "Ingredient " + i, root, new Color(0.4f, 0.5f, 0.3f), false, 0.2f);
            pieces.Add(piece.transform);
            piece.SetActive(false);
        }
        for (int i = 0; i < 6; i++)
        {
            GameObject arc = AlchemyVesselParts.Primitive(PrimitiveType.Cube, "Streak " + i, root, new Color(1f, 1f, 1f, 0f), true, 0.6f);
            arc.GetComponent<Renderer>().sharedMaterial.renderQueue = 3001;
            streakArcs.Add(arc.GetComponent<Renderer>());
        }

        // Effects.
        Transform fx = AlchemyVesselParts.Group("Effects", root);
        if (bubbles == null) bubbles = AlchemyVesselParts.Particles("Bubbles", fx, new Color(1f, 1f, 1f, 0.8f), r * 0.85f, 0.35f, r * 0.25f, r * 0.08f, -0.05f);
        simmer = AlchemyVesselParts.Particles("Rising Bubbles", fx, new Color(1f, 1f, 1f, 0.5f), r * 0.7f, 0.9f, r * 0.6f, r * 0.04f);
        specks = AlchemyVesselParts.Particles("Specks", fx, new Color(0.5f, 0.45f, 0.4f, 0.8f), r * 0.75f, 3f, r * 0.08f, r * 0.03f, 0f, 200);
        if (steam == null) steam = AlchemyVesselParts.Particles("Steam", fx, new Color(0.9f, 0.92f, 0.95f, 0.35f), r * 0.7f, 2.4f, r * 0.9f, r * 0.6f, -0.03f);
        if (smoke == null) smoke = AlchemyVesselParts.Particles("Smoke", fx, new Color(0.22f, 0.21f, 0.2f, 0.55f), r * 0.6f, 3f, r * 0.8f, r * 0.7f, -0.02f);
        if (sparks == null)
        {
            sparks = AlchemyVesselParts.Particles("Sparks", fx, new Color(1f, 0.8f, 0.35f, 1f), r * 0.4f, 0.6f, r * 3f, r * 0.04f, 0.6f);
            SetRate(sparks, 0f);
        }
        if (glowLight == null)
        {
            var g = new GameObject("Glow");
            g.transform.SetParent(root, false);
            g.transform.localPosition = new Vector3(0f, fullHeight + r * 0.3f, 0f);
            glowLight = g.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.range = r * 6f;
            glowLight.intensity = 0f;
            glowLight.shadows = LightShadows.None;
        }

        // The fire bed (under this object: sized in its units).
        r = PropRadius;
        fireRoot = AlchemyVesselParts.Group("Fire Bed", fireBed != null ? fireBed : transform);
        if (fireBed == null) fireRoot.position = FireBedPoint;
        flames = AlchemyVesselParts.Particles("Flames", fireRoot, new Color(1f, 0.6f, 0.2f, 0.9f), r * 0.45f, 0.5f, r * 1.1f, r * 0.35f, -0.2f);
        fireSmoke = AlchemyVesselParts.Particles("Fire Smoke", fireRoot, new Color(0.18f, 0.17f, 0.16f, 0.5f), r * 0.4f, 3f, r * 0.6f, r * 0.6f, -0.02f);
        if (fireLight == null)
        {
            var f = new GameObject("Fire Light");
            f.transform.SetParent(fireRoot, false);
            f.transform.localPosition = new Vector3(0f, r * 0.3f, 0f);
            fireLight = f.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.6f, 0.3f);
            fireLight.range = r * 7f;
            fireLight.shadows = LightShadows.None;
        }

        if (buildProps) EnsureProps();
    }

    /// <summary>
    /// Makes sure the props worked with the hands exist (the spoon, the bellows, the filter stand): built from
    /// primitives - also when Auto Build is off (your own vessel art keeps its contents).
    /// </summary>
    public void EnsureProps()
    {
        if (autoBuild && !built)
            Build();
        if (propsBuilt)
            return;
        propsBuilt = true;
        BuildProps();
    }

    private Renderer Column(string name, Transform parent, int queue)
    {
        GameObject go = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, name, parent, new Color(0.5f, 0.6f, 0.7f, 0.6f), true, 0.8f);
        Renderer r = go.GetComponent<Renderer>();
        if (liquidMaterial != null)
            r.sharedMaterial = new Material(liquidMaterial) { hideFlags = HideFlags.DontSave };
        r.sharedMaterial.renderQueue = queue;
        return r;
    }

    private void BuildProps()
    {
        float r = PropRadius;
        Transform props = AlchemyVesselParts.Group("Alchemy Props", transform);

        // The spoon rests against the rim.
        spoon = AlchemyVesselParts.Spoon(props, r);

        // Bellows beside a manual fire, nozzle at the fire.
        AlchemyStation s = Station;
        if (s == null || s.HasManualFire)
        {
            bellows = AlchemyVesselParts.Bellows(bellowsPoint != null ? bellowsPoint : props, r * 1.3f, out bellowsTop, out bellowsLeather, out bellowsNozzle);
            if (bellowsPoint == null)
            {
                Vector3 side = -transform.right;
                Vector3 at = FireBedPoint + side * (r * 2.1f);
                bellows.position = at;
                bellows.rotation = Quaternion.LookRotation(FireBedPoint - at, Vector3.up);
            }
            bellowsPuff = AlchemyVesselParts.Particles("Puff", bellowsNozzle, new Color(0.8f, 0.8f, 0.8f, 0.25f), r * 0.03f, 0.4f, r * 2f, r * 0.15f); // along the nozzle
        }

        // The mortar and pestle on the table (stations with a mortar).
        if (s == null || s.HasMortar)
        {
            mortarProp = AlchemyVesselParts.Group("Mortar", mortarPoint != null ? mortarPoint : props);
            if (mortarPoint == null)
            {
                mortarProp.position = Space.TransformPoint(new Vector3(-innerRadius * 2.4f, emptyHeight, innerRadius * 0.4f)) - Vector3.up * WorldRadius * 0.1f;
                mortarProp.rotation = transform.rotation;
            }
            float rim = r * 0.3f, belly = r * 0.33f, foot = r * 0.22f, height = r * 0.28f, wall = r * 0.045f;
            AlchemyVesselParts.MeshObject("Bowl", mortarProp, AlchemyVesselParts.OpenVessel(rim, belly, foot, height, wall, 28), new Color(0.55f, 0.53f, 0.5f));
            mortarUnit = (foot - wall + rim - wall) * 0.5f;
            mortarBottomY = wall + height * 0.02f;
            mortarDepth = height;
            pestle = AlchemyVesselParts.Group("Pestle", props);
            GameObject knob = AlchemyVesselParts.Primitive(PrimitiveType.Sphere, "Head", pestle, new Color(0.62f, 0.6f, 0.56f), false, 0.4f);
            knob.transform.localScale = Vector3.one * r * 0.13f;
            GameObject shaft = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "Shaft", pestle, new Color(0.6f, 0.58f, 0.55f), false, 0.4f);
            shaft.transform.localScale = new Vector3(r * 0.07f, r * 0.22f, r * 0.07f);
            shaft.transform.localPosition = new Vector3(0f, r * 0.24f, 0f);
            mortarDust = AlchemyVesselParts.Particles("Mortar Dust", props, new Color(0.8f, 0.78f, 0.7f, 0.6f), r * 0.05f, 0.6f, r * 0.3f, r * 0.05f, 0.3f);
        }

        // The filter stand on the table: a funnel with its filter, a flask under it, a jug that pours.
        filterStand = AlchemyVesselParts.Group("Filter Stand", filterPoint != null ? filterPoint : props);
        if (filterPoint == null)
        {
            filterStand.position = Space.TransformPoint(new Vector3(innerRadius * 2.6f, emptyHeight, 0f)) - Vector3.up * WorldRadius * 0.1f;
            filterStand.rotation = transform.rotation;
        }
        float fs = r * 0.9f;
        GameObject flaskGo = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "Flask", filterStand, new Color(0.8f, 0.9f, 0.95f, 0.25f), true, 0.95f);
        flask = flaskGo.transform;
        flask.localScale = new Vector3(fs * 0.55f, fs * 0.35f, fs * 0.55f);
        flask.localPosition = new Vector3(0f, fs * 0.35f, 0f);
        flaskLiquid = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "Filtered", filterStand, new Color(0.5f, 0.6f, 0.7f, 0.6f), true, 0.8f).transform;
        flaskLiquid.gameObject.SetActive(false);
        GameObject funnelGo = AlchemyVesselParts.MeshObject("Funnel", filterStand, AlchemyVesselParts.Funnel(fs * 0.4f, fs * 0.05f, fs * 0.45f), new Color(0.6f, 0.62f, 0.65f));
        funnel = funnelGo.transform;
        funnel.localPosition = new Vector3(0f, fs * 0.72f, 0f);
        filterMedium = AlchemyVesselParts.MeshObject("Filter", funnel, AlchemyVesselParts.Funnel(fs * 0.37f, fs * 0.04f, fs * 0.42f), new Color(0.95f, 0.94f, 0.9f)).transform;
        filterMedium.localPosition = new Vector3(0f, fs * 0.02f, 0f);
        funnelLiquid = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "In The Funnel", funnel, new Color(0.5f, 0.6f, 0.7f, 0.8f), true, 0.8f).transform;
        funnelLiquid.gameObject.SetActive(false);
        jug = AlchemyVesselParts.Group("Jug", filterStand);
        jug.localPosition = new Vector3(-fs * 0.55f, fs * 1.45f, 0f);
        GameObject jugBody = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "Body", jug, new Color(0.55f, 0.35f, 0.25f), false, 0.3f);
        jugBody.transform.localScale = new Vector3(fs * 0.35f, fs * 0.25f, fs * 0.35f);
        jugLiquid = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "Brew", jug, new Color(0.5f, 0.6f, 0.7f, 1f), false, 0.8f).transform;
        jugLiquid.localScale = new Vector3(fs * 0.3f, 0.002f, fs * 0.3f);
        jugLiquid.localPosition = new Vector3(0f, fs * 0.2f, 0f);
        stream = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "Stream", filterStand, new Color(0.5f, 0.6f, 0.7f, 0.85f), true, 0.9f).transform;
        stream.gameObject.SetActive(false);
        overflow = AlchemyVesselParts.Particles("Overflow", funnel, new Color(0.5f, 0.6f, 0.7f, 0.9f), fs * 0.4f, 0.6f, fs * 0.2f, fs * 0.04f, 1f);
        overflow.transform.localPosition = new Vector3(0f, fs * 0.45f, 0f);
        jug.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ drawing the contents
    private void DrawAssignedLiquid()
    {
        if (liquid == null)
            return;
        bool visible = fill > 0.005f;
        if (liquid.enabled != visible) liquid.enabled = visible;
        Vector3 p = liquid.transform.localPosition;
        p.y = level;
        liquid.transform.localPosition = p;
        Tint(liquid, new Color(shown.r, shown.g, shown.b, 1f));
    }

    private void DrawContents(float dt)
    {
        bool wet = look.HasLiquid && fill > 0.005f;
        float bottom = emptyHeight;
        float depth = Mathf.Max(0.001f, level - bottom);
        float rBottom = innerRadius * bottomRadiusScale * 0.98f;
        float rSurface = RadiusAt(fill) * 0.98f;
        // Clear = see-through, murky = opaque; the bottom layer of two shows under the top one.
        float alpha = Mathf.Lerp(0.45f, 0.97f, Mathf.Clamp01(cloud + look.Grime * 0.5f + look.Thickness * 0.25f));
        bool layered = look.Layered && emulsion < 0.7f;
        float split = layered ? bottom + depth * (1f - topShare) : bottom;
        Color topNow = layered ? topColor : Color.Lerp(shown, Color.Lerp(topColor, bottomColor, 0.5f), look.Layered ? 0.5f : 0f);

        SetActive(body, wet);
        if (wet)
        {
            float h = level - split;
            body.transform.localScale = new Vector3(rBottom * 2f, Mathf.Max(0.0005f, h * 0.5f), rBottom * 2f);
            body.transform.localPosition = new Vector3(0f, split + h * 0.5f, 0f);
            TintMaterial(body, new Color(topNow.r, topNow.g, topNow.b, alpha));
        }
        SetActive(bottomBody, wet && layered);
        if (wet && layered)
        {
            float h = split - bottom;
            bottomBody.transform.localScale = new Vector3(rBottom * 2f, Mathf.Max(0.0005f, h * 0.5f), rBottom * 2f);
            bottomBody.transform.localPosition = new Vector3(0f, bottom + h * 0.5f, 0f);
            TintMaterial(bottomBody, new Color(bottomColor.r, bottomColor.g, bottomColor.b, Mathf.Min(1f, alpha + 0.1f)));
        }

        // The surface: the top colour, lighter where it shines; it trembles with a boil.
        SetActive(surface, wet);
        if (wet)
        {
            float tremble = look.Boiling ? Mathf.Sin(Time.time * 23f) * innerRadius * 0.006f * look.Bubbling : 0f;
            surface.transform.localScale = new Vector3(rSurface * 2f, 0.0015f, rSurface * 2f);
            surface.transform.localPosition = new Vector3(0f, level + tremble, 0f);
            Color sheen = Color.Lerp(topNow, Color.white, 0.12f);
            if (surface == liquid && !built) Tint(surface, sheen);
            else TintMaterial(surface, new Color(sheen.r, sheen.g, sheen.b, Mathf.Lerp(0.55f, 0.95f, alpha)));
        }

        // Froth.
        bool frothy = wet && foam > 0.03f;
        SetActive(foamDisc, frothy);
        if (frothy)
        {
            float fr = rSurface * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(foam * 1.6f));
            foamDisc.transform.localScale = new Vector3(fr * 2f, innerRadius * 0.01f * (0.5f + foam), fr * 2f);
            foamDisc.transform.localPosition = new Vector3(0f, level + innerRadius * 0.012f, 0f);
            TintMaterial(foamDisc, new Color(0.96f, 0.94f, 0.86f, Mathf.Clamp01(foam * 1.4f)));
        }

        // Sediment at the bottom: a mound that grows with what has settled; stirring lifts it into specks.
        bool settled = sediment > 0.01f;
        SetActive(sedimentDisc, settled);
        if (settled)
        {
            float lift = Mathf.Clamp01(1f - flowSpeed * flowSpeed * 0.08f);
            float sh = innerRadius * 0.06f * sediment * lift + 0.001f;
            sedimentDisc.transform.localScale = new Vector3(rBottom * 1.9f, sh, rBottom * 1.9f);
            sedimentDisc.transform.localPosition = new Vector3(0f, bottom + sh, 0f);
            TintMaterial(sedimentDisc, sedimentColor);
        }

        // Floating chunks: drift round with the swirl, bob with the boil, pushed aside by the spoon.
        int shownChunks = wet || floating > 0f ? Mathf.RoundToInt(floating * floaters.Count) : 0;
        Vector3 tipLocal = spoonHeld ? Space.InverseTransformPoint(spoonTip) : new Vector3(9e3f, 0f, 0f);
        for (int i = 0; i < floaters.Count; i++)
        {
            Floater f = floaters[i];
            bool on = i < shownChunks;
            if (f.t.gameObject.activeSelf != on) f.t.gameObject.SetActive(on);
            if (!on) continue;
            f.angle += flowSpeed * (0.5f + 0.5f * f.radius) * dt + Mathf.Sin(Time.time * 0.3f + f.phase) * 0.05f * dt;
            float rr = f.radius * rSurface * 0.9f;
            var p = new Vector3(Mathf.Cos(f.angle) * rr, level + innerRadius * 0.01f, Mathf.Sin(f.angle) * rr);
            Vector2 away = new Vector2(p.x - tipLocal.x, p.z - tipLocal.z);
            float push = innerRadius * 0.22f;
            if (away.sqrMagnitude < push * push && away.sqrMagnitude > 1e-6f)
            {
                // The spoon shoves it: the chunk moves to a new ring and angle.
                Vector2 to = new Vector2(tipLocal.x, tipLocal.z) + away.normalized * push;
                f.radius = Mathf.Clamp(to.magnitude / Mathf.Max(1e-4f, rSurface * 0.9f), 0.05f, 0.95f);
                f.angle = Mathf.Atan2(to.y, to.x);
                p = new Vector3(to.x, p.y, to.y);
            }
            p.y += Mathf.Sin(Time.time * (look.Boiling ? 9f : 1.5f) + f.phase) * innerRadius * (look.Boiling ? 0.02f : 0.006f);
            if (!wet) p.y = emptyHeight + innerRadius * 0.02f;
            f.t.localPosition = p;
            float size = innerRadius * 0.07f * f.size * (0.6f + floating * 0.6f);
            f.t.localScale = new Vector3(size, size * 0.45f, size * 0.8f);
            f.t.localRotation = Quaternion.Euler(0f, f.angle * Mathf.Rad2Deg + f.phase * 40f, 0f);
            TintMaterial(f.r, Color.Lerp(floatColor, Color.black, (i % 4) * 0.06f));
            floaters[i] = f;
        }

        // Pieces of ingredients: fresh plant matter bobs on top; as it gives up its substances it darkens and sinks.
        int pi = 0;
        foreach (AlchemyLook.Piece piece in look.Pieces)
        {
            int copies = Mathf.Clamp(Mathf.RoundToInt(piece.Size), 1, 3);
            for (int c = 0; c < copies && pi < pieces.Count; c++, pi++)
            {
                Transform t = pieces[pi];
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                float a = pi * 2.4f + flowAngle * (piece.Floats ? 0.8f : 0.2f);
                float rr = (piece.Floats ? rSurface : rBottom) * (0.3f + 0.12f * (pi % 4));
                float y = piece.Floats || !wet ? level + innerRadius * 0.015f : bottom + innerRadius * 0.05f + sediment * innerRadius * 0.05f;
                if (piece.Floats) y += Mathf.Sin(Time.time * 1.7f + pi) * innerRadius * 0.01f;
                if (!wet) y = bottom + innerRadius * 0.05f;
                t.localPosition = new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
                float s = innerRadius * 0.14f * (1f - piece.Progress * 0.45f);
                t.localScale = new Vector3(s * 0.5f, s * 0.7f, s * 0.5f);
                t.localRotation = Quaternion.Euler(90f, a * Mathf.Rad2Deg, pi * 30f);
                TintMaterial(t.GetComponent<Renderer>(), piece.Color);
            }
        }
        for (; pi < pieces.Count; pi++)
            if (pieces[pi].gameObject.activeSelf) pieces[pi].gameObject.SetActive(false);

        // Streaks: arcs of another colour curling round the surface (a poor mix, half-blended layers).
        Color streakColor = look.Pieces.Count > 0 ? look.Pieces[0].Color : look.Layered ? bottomColor : Color.Lerp(shown, Color.white, 0.35f);
        for (int i = 0; i < streakArcs.Count; i++)
        {
            Renderer arc = streakArcs[i];
            bool on = wet && streaks > 0.05f;
            SetActive(arc, on);
            if (!on) continue;
            float a = flowAngle * (0.7f + i * 0.07f) + i * 1.05f;
            float rr = rSurface * (0.25f + 0.11f * i);
            arc.transform.localPosition = new Vector3(Mathf.Cos(a) * rr, level + innerRadius * 0.004f, Mathf.Sin(a) * rr);
            arc.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            arc.transform.localScale = new Vector3(innerRadius * 0.05f, 0.001f, rr * 0.9f);
            TintMaterial(arc, new Color(streakColor.r, streakColor.g, streakColor.b, Mathf.Clamp01(streaks) * 0.75f));
        }

        // The effects follow the surface.
        PlaceAtSurface(bubbles, level, rSurface * 0.85f);
        PlaceAtSurface(steam, level + innerRadius * 0.05f, rSurface * 0.7f);
        PlaceAtSurface(smoke, level + innerRadius * 0.05f, rSurface * 0.6f);
        PlaceAtSurface(specks, bottom + depth * 0.3f, rBottom * 0.8f);
        PlaceAtSurface(simmer, bottom + innerRadius * 0.02f, rBottom * 0.8f);
        if (simmer != null)
        {
            ParticleSystem.MainModule main = simmer.main;
            main.startLifetime = Mathf.Max(0.1f, depth / Mathf.Max(0.01f, innerRadius * 0.6f));
        }
        if (glowLight != null && glowLight.transform.parent == root)
            glowLight.transform.localPosition = new Vector3(0f, level + innerRadius * 0.3f, 0f);
    }

    private void PlaceAtSurface(ParticleSystem ps, float y, float radius)
    {
        if (ps == null || ps.transform.parent == null || ps.transform.parent.parent != root)
            return;
        ps.transform.localPosition = new Vector3(0f, y, 0f);
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.radius = Mathf.Max(0.001f, radius);
    }

    // ------------------------------------------------------------------ the fire bed
    private void DrawFire(AlchemyBrew brew, AlchemyMixture m, float dt)
    {
        bool manual = m != null && m.FireManual;
        float flames01 = m != null ? Mathf.Max(0f, (float)m.FireLevel) : 0f;
        SetRate(flames, manual ? flames01 * 70f : m != null && m.EffectiveHeat > 0.01 ? (float)m.EffectiveHeat * 40f : 0f);
        SetRate(fireSmoke, manual && m.Damper < 0.25 && flames01 > 0.05f ? 12f * (1f - (float)m.Damper * 4f) : 0f);

        // Rebuild the fuel from the history when it changed (a new preparation, fuel added, a restored save).
        if (brew != fuelBrew || brew != null && brew.History.Count != fuelHistory)
        {
            fuelBrew = brew;
            fuelHistory = brew != null ? brew.History.Count : -1;
            RebuildFuel(brew);
        }
        // The oldest burns away first: trim to what is left on the fire.
        float left = m != null ? (float)m.Fuel : 0f;
        float total = 0f, oil = 0f;
        for (int i = fuel.Count - 1; i >= 0; i--)
        {
            FuelPiece p = fuel[i];
            float share = Mathf.Clamp01((left - total) / Mathf.Max(1e-4f, p.units));
            total += p.units;
            if (p.id == "lamp_oil")
            {
                oil += p.units * share; // all the oil is in one dish
                continue;
            }
            bool on = share > 0.02f;
            if (p.t.gameObject.activeSelf != on) p.t.gameObject.SetActive(on);
            if (!on) continue;
            p.t.localScale = p.baseScale * Mathf.Lerp(0.35f, 1f, share);
            // Wood chars as it burns; embers glow with the flames.
            Material mat = p.r.sharedMaterial;
            Color baseColor = FuelColor(p.id);
            AlchemyVesselParts.SetColor(mat, Color.Lerp(new Color(0.08f, 0.07f, 0.06f), baseColor, share));
            AlchemyVesselParts.SetEmission(mat, new Color(1f, 0.35f, 0.08f) * (flames01 * (p.id == "charcoal" ? 1.6f : 0.8f) * flicker));
        }
        if (oilDish != null)
        {
            bool any = oil > 0.01f;
            if (oilDish.gameObject.activeSelf != any) oilDish.gameObject.SetActive(any);
            oilLevel.localScale = new Vector3(0.85f, Mathf.Clamp01(oil / 3f) * 0.3f + 0.01f, 0.85f);
            AlchemyVesselParts.SetEmission(oilLevel.GetComponent<Renderer>().sharedMaterial, new Color(1f, 0.6f, 0.2f) * (flames01 * 0.6f * flicker));
        }
    }

    private void RebuildFuel(AlchemyBrew brew)
    {
        foreach (FuelPiece p in fuel)
            if (p.t != null && p.id != "lamp_oil") Destroy(p.t.gameObject);
        fuel.Clear();
        if (brew == null)
            return;
        float r = PropRadius;
        int n = 0;
        foreach (AlchemyOperation op in brew.History)
        {
            if (op.kind != AlchemyOpKind.AddFuel) continue;
            int count = Mathf.Max(1, op.count);
            float per = op.value / count;
            for (int c = 0; c < count; c++, n++)
            {
                if (fuel.Count >= 24)
                {
                    // Only the newest are drawn (the oldest have burnt away anyway).
                    if (fuel[0].id != "lamp_oil" && fuel[0].t != null) Destroy(fuel[0].t.gameObject);
                    fuel.RemoveAt(0);
                }
                fuel.Add(MakeFuel(op.id, per, n, r));
            }
        }
    }

    private FuelPiece MakeFuel(string id, float units, int n, float r)
    {
        var p = new FuelPiece { id = id, units = units };
        float a = n * 2.39996f; // spread round the bed
        float rr = r * 0.12f + r * 0.08f * (n % 4);
        Vector3 at = new Vector3(Mathf.Cos(a) * rr, r * 0.04f * (n / 6), Mathf.Sin(a) * rr);
        switch (id)
        {
            case "charcoal":
            {
                GameObject go = AlchemyVesselParts.Primitive(PrimitiveType.Cube, "Charcoal", fireRoot, FuelColor(id), false, 0.05f);
                p.baseScale = new Vector3(r * 0.12f, r * 0.08f, r * 0.1f);
                go.transform.localPosition = at;
                go.transform.localRotation = Quaternion.Euler(n * 17f, n * 53f, n * 29f);
                p.t = go.transform;
                break;
            }
            case "pine_resin":
            {
                GameObject go = AlchemyVesselParts.Primitive(PrimitiveType.Sphere, "Resin", fireRoot, FuelColor(id), false, 0.8f);
                p.baseScale = new Vector3(r * 0.08f, r * 0.06f, r * 0.08f);
                go.transform.localPosition = at + Vector3.up * r * 0.05f;
                p.t = go.transform;
                break;
            }
            case "lamp_oil":
            {
                if (oilDish == null)
                {
                    oilDish = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "Oil Dish", fireRoot, new Color(0.3f, 0.28f, 0.26f), false, 0.4f).transform;
                    oilDish.localScale = new Vector3(r * 0.5f, r * 0.03f, r * 0.5f);
                    oilDish.localPosition = new Vector3(r * 0.35f, 0f, 0f);
                    oilLevel = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "Oil", oilDish, FuelColor(id), false, 0.9f).transform;
                    oilLevel.localPosition = new Vector3(0f, 0.6f, 0f);
                }
                p.t = oilLevel;
                p.baseScale = oilLevel.localScale;
                break;
            }
            default:
            {
                GameObject go = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "Log", fireRoot, FuelColor(id), false, 0.1f);
                p.baseScale = new Vector3(r * 0.12f, r * 0.42f, r * 0.12f);
                go.transform.localPosition = at + Vector3.up * r * 0.04f;
                go.transform.localRotation = Quaternion.Euler(90f, a * Mathf.Rad2Deg, 0f);
                p.t = go.transform;
                break;
            }
        }
        p.r = p.t.GetComponent<Renderer>();
        p.t.localScale = p.baseScale;
        return p;
    }

    private static Color FuelColor(string id) => id switch
    {
        "charcoal" => new Color(0.1f, 0.1f, 0.1f),
        "pine_resin" => new Color(0.85f, 0.55f, 0.15f),
        "lamp_oil" => new Color(0.75f, 0.6f, 0.2f),
        _ => new Color(0.42f, 0.28f, 0.16f),
    };

    // ------------------------------------------------------------------ props
    /// <summary>The spoon is in the player's hand: its bowl at <paramref name="tip"/> (world), its handle along <paramref name="handleDir"/>.</summary>
    public void HoldSpoon(bool held, Vector3 tip, Vector3 handleDir, float direction)
    {
        spoonHeld = held;
        spoonTip = tip;
        spoonDir = handleDir;
        if (held && Mathf.Abs(direction) > 0.05f && Mathf.Sign(direction) != Mathf.Sign(flowSpeed == 0f ? 1f : flowSpeed))
            flowSpeed = -flowSpeed * 0.3f; // the swirl is turned round, with a slosh
    }

    /// <summary>How far open the bellows are (0 closed - 1 open).</summary>
    public void SetBellows(float opening) => bellowsOpen = Mathf.Clamp01(opening);

    /// <summary>A puff from the nozzle (the end of a squeeze).</summary>
    public void PuffBellows(float strength)
    {
        if (bellowsPuff != null) bellowsPuff.Emit(Mathf.RoundToInt(6 + strength * 24));
        if (flames != null) flames.Emit(Mathf.RoundToInt(strength * 20));
        Play(bellowsClip);
    }

    /// <summary>The filtering: the jug's tilt (0-1), how fast it pours (0-1), how full the funnel and the flask are (0-1), overflowing.</summary>
    public void SetFiltering(bool active, float tilt, float pour, float funnel01, float flask01, bool overflowing, Color brew, string filterId)
    {
        if (active && !filtering && jug != null)
        {
            jug.gameObject.SetActive(true);
            Color medium = filterId == "charcoal" ? new Color(0.12f, 0.12f, 0.12f) : filterId == "strainer" ? new Color(0.8f, 0.72f, 0.55f) : new Color(0.96f, 0.95f, 0.9f);
            if (filterMedium != null) AlchemyVesselParts.SetColor(filterMedium.GetComponent<Renderer>().sharedMaterial, medium);
            Play(pourClip);
        }
        if (!active && jug != null)
        {
            jug.gameObject.SetActive(false);
            stream.gameObject.SetActive(false);
            funnelLiquid.gameObject.SetActive(false);
            flaskLiquid.gameObject.SetActive(false);
        }
        filtering = active;
        filterTilt = tilt;
        filterPour = pour;
        funnelFill = funnel01;
        flaskFill = flask01;
        filterColor = brew;
        SetRate(overflow, active && overflowing ? 40f : 0f);
    }

    private void DrawProps(float dt)
    {
        float r = PropRadius;
        // The spoon: in the hand, or resting against the rim.
        if (spoon != null)
        {
            if (spoonHeld)
            {
                spoon.position = spoonTip;
                spoon.rotation = Quaternion.FromToRotation(Vector3.up, spoonDir.sqrMagnitude > 1e-6f ? spoonDir.normalized : Vector3.up);
            }
            else
            {
                Vector3 rim = Space.TransformPoint(new Vector3(innerRadius * 0.7f, Mathf.Max(SurfaceLocalY, emptyHeight + innerRadius * 0.2f), innerRadius * 0.3f));
                spoon.position = rim;
                spoon.rotation = Quaternion.FromToRotation(Vector3.up, (transform.up + transform.TransformDirection(new Vector3(0.7f, 0f, 0.3f)) * 0.6f).normalized);
            }
        }
        // The pestle: in the hand (leaning toward the view), or resting in the mortar.
        if (pestle != null)
        {
            Vector3 head = pestleHeld ? pestleHead : MortarPoint(0.45f, 0f, mortarUnit * 0.3f);
            Vector3 lean = Vector3.up + (pestleHeld ? (Front * 0.35f) : mortarProp.right * 0.6f);
            pestle.position = head;
            pestle.rotation = Quaternion.FromToRotation(Vector3.up, lean.normalized);
        }
        // The bellows open and close (they spring back open by themselves when let go).
        if (bellowsTop != null)
        {
            bellowsShown = Mathf.Lerp(bellowsShown, bellowsOpen, 1f - Mathf.Exp(-dt * 20f));
            bellowsTop.localRotation = Quaternion.Euler(-Mathf.Lerp(2f, 28f, bellowsShown), 0f, 0f);
            float size = r * 1.3f;
            bellowsLeather.localScale = new Vector3(size * 0.46f, size * Mathf.Lerp(0.03f, 0.42f, bellowsShown), size * 0.85f);
            bellowsLeather.localPosition = new Vector3(0f, size * Mathf.Lerp(0.03f, 0.21f, bellowsShown), -size * 0.55f);
        }
        // The filter stand.
        if (filtering && jug != null)
        {
            float fs = r * 0.9f;
            jug.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Lerp(5f, 110f, filterTilt));
            float jugLeft = Mathf.Clamp01(1f - flaskFill * 0.9f - funnelFill * 0.1f);
            jugLiquid.gameObject.SetActive(jugLeft > 0.02f);
            jugLiquid.localPosition = new Vector3(0f, Mathf.Lerp(-fs * 0.2f, fs * 0.2f, jugLeft), 0f);
            TintMaterial(jugLiquid.GetComponent<Renderer>(), filterColor);
            bool pouring = filterPour > 0.02f;
            stream.gameObject.SetActive(pouring);
            if (pouring)
            {
                Vector3 lip = jug.TransformPoint(new Vector3(fs * 0.35f, fs * 0.25f, 0f));
                Vector3 into = funnel.TransformPoint(new Vector3(0f, fs * 0.4f, 0f));
                Vector3 mid = (lip + into) * 0.5f;
                stream.position = mid;
                stream.rotation = Quaternion.FromToRotation(Vector3.up, lip - into);
                float w = fs * Mathf.Lerp(0.02f, 0.09f, filterPour);
                stream.localScale = new Vector3(w, (lip - into).magnitude * 0.5f / Mathf.Max(1e-4f, filterStand.lossyScale.y), w);
                TintMaterial(stream.GetComponent<Renderer>(), new Color(filterColor.r, filterColor.g, filterColor.b, 0.85f));
            }
            funnelLiquid.gameObject.SetActive(funnelFill > 0.01f);
            if (funnelFill > 0.01f)
            {
                float h = fs * 0.45f * funnelFill;
                float rad = Mathf.Lerp(fs * 0.05f, fs * 0.4f, funnelFill) * 1.9f;
                funnelLiquid.localScale = new Vector3(rad, 0.002f, rad);
                funnelLiquid.localPosition = new Vector3(0f, h, 0f);
                TintMaterial(funnelLiquid.GetComponent<Renderer>(), new Color(filterColor.r, filterColor.g, filterColor.b, 0.85f));
            }
            flaskLiquid.gameObject.SetActive(flaskFill > 0.01f);
            if (flaskFill > 0.01f)
            {
                float h = fs * 0.65f * flaskFill;
                flaskLiquid.localScale = new Vector3(fs * 0.52f, h * 0.5f, fs * 0.52f);
                flaskLiquid.localPosition = new Vector3(0f, h * 0.5f + fs * 0.01f, 0f);
                Color clear = Color.Lerp(filterColor, Color.white, 0.15f);
                TintMaterial(flaskLiquid.GetComponent<Renderer>(), new Color(clear.r, clear.g, clear.b, 0.6f));
            }
        }
    }

    // ------------------------------------------------------------------ fitting it to a vessel (Scene view)
    private void OnDrawGizmosSelected()
    {
        // The inside as it is set: the inner bottom, the belly and the brim (fit them to your vessel), and the fire bed.
        Transform sp = Space;
        Gizmos.matrix = sp.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        float last = 0f;
        for (int i = 0; i <= 10; i++)
        {
            float f = i / 10f;
            float y = Mathf.Lerp(emptyHeight, fullHeight, f), rad = RadiusAt(f);
            if (i == 0 || i == 10 || bellyRadiusScale > 0f && Mathf.Abs(f - bellyAt) < 0.05f)
                Circle(y, rad);
            if (i > 0)
            {
                float y0 = Mathf.Lerp(emptyHeight, fullHeight, (i - 1) / 10f);
                Gizmos.DrawLine(new Vector3(last, y0, 0f), new Vector3(rad, y, 0f));
                Gizmos.DrawLine(new Vector3(-last, y0, 0f), new Vector3(-rad, y, 0f));
            }
            last = rad;
        }
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(FireBedPoint, Mathf.Max(0.02f, WorldRadius * 0.25f));
    }

    private static void Circle(float y, float radius)
    {
        Vector3 prev = new Vector3(radius, y, 0f);
        for (int i = 1; i <= 32; i++)
        {
            float a = i * Mathf.PI * 2f / 32f;
            var p = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    // ------------------------------------------------------------------ helpers
    private static void SetActive(Renderer r, bool on)
    {
        if (r != null && r.gameObject.activeSelf != on) r.gameObject.SetActive(on);
    }

    private static void TintMaterial(Renderer r, Color c)
    {
        if (r != null) AlchemyVesselParts.SetColor(r.sharedMaterial, c);
    }

    private void Tint(Renderer r, Color c)
    {
        block ??= new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetColor(BaseColor, c);
        block.SetColor(ColorId, c);
        r.SetPropertyBlock(block);
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
                Burst(bubbles, 8);
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
                    case ReactionVisual.Bubbles: Burst(bubbles, 20); break;
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
            case AlchemyEventKind.Splashed:
                Burst(bubbles, 30);
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
