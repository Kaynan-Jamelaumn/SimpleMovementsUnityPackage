using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a crafting station looks and sounds like in the world, read from its workshop: the forge's coals glowing with
/// the fire's heat (and roaring with the bellows), flames and smoke, the furnace's glow, sparks off the anvil when hot
/// metal is struck and off the wheel when steel is ground, steam (or an oil flare) when hot metal goes into the tank,
/// the ring of a cold blow and the thud of a hot one, the ping of a crack. Built from primitives at the points you set
/// (Auto Build), or driving the lights, renderers and particle systems you assign.
/// </summary>
[AddComponentMenu("SimpleMovements/Crafting/Craft Station Visuals")]
public class CraftStationVisuals : MonoBehaviour
{
    [Tooltip("Empty = the station on this object or a parent.")]
    [SerializeField] private CraftStation station;
    [Tooltip("Build glowing coals, lights and effects from primitives at the points below (those that are set, or in a row in front of the station).")]
    [SerializeField] private bool autoBuild = true;

    [Header("Where")]
    [SerializeField] private Transform forgePoint;
    [SerializeField] private Transform anvilPoint;
    [SerializeField] private Transform quenchPoint;
    [SerializeField] private Transform furnacePoint;
    [SerializeField] private Transform grinderPoint;
    [SerializeField] private Transform vatPoint;

    [Header("Glow")]
    [Tooltip("The forge's coal bed (its material's emission follows the fire). Auto Build makes one.")]
    [SerializeField] private Renderer forgeCoals;
    [SerializeField] private Light forgeLight;
    [SerializeField] private Renderer furnaceMouth;
    [SerializeField] private Light furnaceLight;
    [SerializeField, Min(0f)] private float lightIntensity = 3f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem forgeFlames;
    [SerializeField] private ParticleSystem forgeSmoke;
    [SerializeField] private ParticleSystem anvilSparks;
    [SerializeField] private ParticleSystem grinderSparks;
    [SerializeField] private ParticleSystem quenchSteam;
    [SerializeField] private ParticleSystem furnaceSmoke;

    [Header("Sounds")]
    [SerializeField] private AudioSource oneShotSource;
    [SerializeField] private AudioSource fireLoop;
    [SerializeField] private AudioClip hammerRing;
    [SerializeField] private AudioClip hammerThud;
    [SerializeField] private AudioClip crackPing;
    [SerializeField] private AudioClip quenchHiss;
    [SerializeField] private AudioClip oilFlare;
    [SerializeField] private AudioClip grinding;
    [SerializeField] private AudioClip bellowsPuff;
    [SerializeField] private AudioClip pour;

    private CraftStation subscribed;
    private bool built;
    private float steamBurst;
    private static readonly Color Ember = new Color(1f, 0.42f, 0.12f);

    private void Awake()
    {
        if (station == null) station = GetComponentInParent<CraftStation>();
    }

    private void OnEnable()
    {
        if (station == null) station = GetComponentInParent<CraftStation>();
        if (station != null)
        {
            station.WorkshopEvent += OnEvent;
            subscribed = station;
        }
        if (autoBuild && !built) Build();
    }

    private void OnDisable()
    {
        if (subscribed != null) subscribed.WorkshopEvent -= OnEvent;
        subscribed = null;
    }

    private Vector3 PointOr(Transform t, int slot) =>
        t != null ? t.position : transform.position + transform.forward * 0.9f + transform.right * (slot - 2) * 0.9f + Vector3.up * 0.9f;

    private void Build()
    {
        built = true;
        if (station == null)
            return;
        Transform root = AlchemyVesselParts.Group("Craft Visuals", transform);
        if (station.Has(CraftTools.Forge))
        {
            Transform f = AlchemyVesselParts.Group("Forge", root);
            f.position = PointOr(forgePoint, 0);
            if (forgeCoals == null)
            {
                GameObject coals = AlchemyVesselParts.Primitive(PrimitiveType.Cylinder, "Coals", f, new Color(0.12f, 0.1f, 0.09f));
                coals.transform.localScale = new Vector3(0.5f, 0.04f, 0.5f);
                Collider c = coals.GetComponent<Collider>();
                if (c != null) Destroy(c);
                forgeCoals = coals.GetComponent<Renderer>();
            }
            if (forgeLight == null) forgeLight = NewLight("Forge Light", f, 0.35f, 6f);
            if (forgeFlames == null) forgeFlames = AlchemyVesselParts.Particles("Flames", f, new Color(1f, 0.55f, 0.18f, 0.9f), 0.2f, 0.45f, 0.5f, 0.12f, -0.2f);
            if (forgeSmoke == null) forgeSmoke = AlchemyVesselParts.Particles("Smoke", f, new Color(0.2f, 0.19f, 0.18f, 0.45f), 0.18f, 3f, 0.5f, 0.35f, -0.02f);
        }
        if (station.Has(CraftTools.Anvil) && anvilSparks == null)
        {
            Transform a = AlchemyVesselParts.Group("Anvil", root);
            a.position = PointOr(anvilPoint, 1);
            anvilSparks = AlchemyVesselParts.Particles("Sparks", a, new Color(1f, 0.78f, 0.35f, 1f), 0.05f, 0.5f, 2.2f, 0.02f, 0.7f, 200);
        }
        if (station.Has(CraftTools.QuenchTank) && quenchSteam == null)
        {
            Transform q = AlchemyVesselParts.Group("Quench", root);
            q.position = PointOr(quenchPoint, 2);
            quenchSteam = AlchemyVesselParts.Particles("Steam", q, new Color(0.9f, 0.92f, 0.95f, 0.4f), 0.2f, 2f, 0.8f, 0.3f, -0.04f);
        }
        if (station.Has(CraftTools.Grinder) && grinderSparks == null)
        {
            Transform g = AlchemyVesselParts.Group("Grinder", root);
            g.position = PointOr(grinderPoint, 3);
            grinderSparks = AlchemyVesselParts.Particles("Grinder Sparks", g, new Color(1f, 0.85f, 0.45f, 1f), 0.02f, 0.4f, 3f, 0.015f, 0.8f, 300);
        }
        if (station.Has(CraftTools.Furnace))
        {
            Transform fu = AlchemyVesselParts.Group("Furnace", root);
            fu.position = PointOr(furnacePoint, 4);
            if (furnaceLight == null) furnaceLight = NewLight("Furnace Light", fu, 0.4f, 7f);
            if (furnaceSmoke == null) furnaceSmoke = AlchemyVesselParts.Particles("Furnace Smoke", fu, new Color(0.25f, 0.23f, 0.2f, 0.5f), 0.25f, 4f, 0.7f, 0.5f, -0.02f);
        }
        if (oneShotSource == null)
        {
            oneShotSource = gameObject.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.spatialBlend = 1f;
        }
    }

    private static Light NewLight(string name, Transform parent, float height, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = range;
        l.intensity = 0f;
        l.shadows = LightShadows.None;
        l.color = Ember;
        return l;
    }

    private void Update()
    {
        CraftWorkshop w = station != null ? station.Workshop : null;
        if (w == null)
            return;
        Fire(w.Forge, forgeCoals, forgeLight, forgeFlames, forgeSmoke);
        Fire(w.Furnace.fire, furnaceMouth, furnaceLight, null, furnaceSmoke);
        // Steam while something hot sits in the tank.
        double heat = 0;
        foreach (CraftPiece p in w.Pieces)
            if (p.location == CraftLocation.Quench && p.kind == CraftPieceKind.Metal)
                heat += System.Math.Max(0, p.MaxTemperature - 100);
        steamBurst = Mathf.Max(0f, steamBurst - Time.deltaTime * 40f);
        SetRate(quenchSteam, Mathf.Min(80f, (float)heat * 0.08f + steamBurst));
        if (fireLoop != null)
        {
            fireLoop.volume = Mathf.Lerp(fireLoop.volume, Mathf.Clamp01((float)(w.Forge.level * (0.5 + w.Forge.air))), Time.deltaTime * 3f);
            if (fireLoop.volume > 0.02f && !fireLoop.isPlaying) fireLoop.Play();
        }
    }

    private void Fire(CraftFireState f, Renderer bed, Light light, ParticleSystem flames, ParticleSystem smoke)
    {
        Color glow = CraftMetallurgy.HeatColor(f.temperature);
        float k = glow.a;
        glow.a = 1f;
        if (bed != null) AlchemyVesselParts.SetEmission(bed.material, glow * (k * 2.5f));
        if (light != null)
        {
            light.color = k > 0.01f ? glow : Ember;
            light.intensity = Mathf.Lerp(light.intensity, lightIntensity * Mathf.Max(k, (float)f.level * 0.6f), Time.deltaTime * 4f);
        }
        SetRate(flames, (float)(f.level * 40 * (0.6 + f.air)));
        SetRate(smoke, (float)(f.smoke * 20));
    }

    private void OnEvent(CraftEvent e)
    {
        switch (e.Kind)
        {
            case CraftEventKind.Struck:
            {
                CraftPiece p = station.Workshop?.Piece(e.Piece);
                bool hot = p != null && p.MaxTemperature > 650;
                if (hot) Burst(anvilSparks, 8 + (int)(p.MaxTemperature / 120));
                Play(e.Id == "crack" ? crackPing : e.Id == "thud" ? hammerThud : hammerRing);
                break;
            }
            case CraftEventKind.Ground:
                Burst(grinderSparks, 25);
                Play(grinding);
                break;
            case CraftEventKind.Quenched:
                steamBurst = 120f;
                Play(quenchHiss);
                break;
            case CraftEventKind.QuenchFlare:
                Play(oilFlare);
                break;
            case CraftEventKind.Cracked:
                Play(crackPing);
                break;
            case CraftEventKind.Poured:
                Play(pour);
                break;
            case CraftEventKind.Burning:
                Burst(anvilSparks, 30);
                break;
        }
    }

    /// <summary>The bellows were worked (the window calls this for the puff of air).</summary>
    public void Puff() => Play(bellowsPuff);

    private void Play(AudioClip clip)
    {
        if (clip != null && oneShotSource != null)
            oneShotSource.PlayOneShot(clip);
    }

    private static void Burst(ParticleSystem ps, int count)
    {
        if (ps == null)
            return;
        if (!ps.isPlaying) ps.Play();
        ps.Emit(count);
    }

    private static void SetRate(ParticleSystem ps, float rate)
    {
        if (ps == null)
            return;
        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = rate;
        if (rate > 0.01f && !ps.isPlaying) ps.Play();
    }
}
