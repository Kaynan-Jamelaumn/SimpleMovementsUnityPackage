using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a crafting station looks and sounds like in the world, read from its workshop: the forge's coals glowing with
/// the fire's heat (and roaring with the bellows), flames and smoke, the furnace's glow, sparks off the anvil when hot
/// metal is struck, off the wheel when steel is ground and out of the fire when it burns, steam (or an oil flare) when
/// hot metal goes into the tank, the ring of a cold blow and the thud of a hot one, the ping of a crack. Built from
/// primitives at the points you set (Auto Build), or driving the lights, renderers and particle systems you assign.
/// <para>It also says where each work place worked by hand in 3D (<see cref="CraftHandsOn"/>) stands - the points of the
/// station's own anvil, wheel, tank, whetstone, carving bench, ore bench and mould - and which of the station's own models
/// stand there: those are hidden while the hands work at that place (it brings its own), never its effects.</para>
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
    [Tooltip("Where the whetstone is worked by hand in 3D (its stone sits on this point). Empty: in front of the station, on the player's side.")]
    [SerializeField] private Transform whetstonePoint;
    [Tooltip("Where wood is carved by hand in 3D (the blank lies on this point). Empty: in front of the station, on the player's side.")]
    [SerializeField] private Transform carvingPoint;
    [Tooltip("Where ore is brushed by hand in 3D (the stone lies on this point). Empty: in front of the station, on the player's side.")]
    [SerializeField] private Transform oreBenchPoint;
    [Tooltip("Where moulds are poured by hand in 3D (the mould's sand is level with this point). Empty: in front of the station, on the player's side.")]
    [SerializeField] private Transform mouldPoint;

    [Header("Worked by hand")]
    [Tooltip("The station's own model of each work place worked by hand in 3D (its anvil, its wheel, its tank...): its mesh renderers are hidden while the hands work there, where the 3D work place brings its own. Particle systems and these visuals' own effects are never hidden.")]
    [SerializeField] private WorkPlaceModel[] workPlaceModels = new WorkPlaceModel[0];

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
    [Tooltip("The sparks metal throws as it burns in the fire or the furnace (moved to where it burns). Auto Build makes one.")]
    [SerializeField] private ParticleSystem burnSparks;

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

    /// <summary>A work place worked by hand and the station's own model of it (hidden while the hands work there).</summary>
    [Serializable]
    public struct WorkPlaceModel
    {
        public CraftHandsOnMode place;
        public Transform model;

        public WorkPlaceModel(CraftHandsOnMode place, Transform model)
        {
            this.place = place;
            this.model = model;
        }
    }

    private CraftStation subscribed;
    private bool built;
    /// <summary>What Auto Build made (the effects): never hidden with a work place's model.</summary>
    private Transform builtRoot;
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
        builtRoot = root;
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
        if ((station.Has(CraftTools.Forge) || station.Has(CraftTools.Furnace)) && burnSparks == null)
        {
            // Bright, crackling sparks out of the coals where the metal burns (placed there when it does).
            Transform b = AlchemyVesselParts.Group("Burning", root);
            b.position = station.Has(CraftTools.Forge) ? PointOr(forgePoint, 0) : PointOr(furnacePoint, 4);
            burnSparks = AlchemyVesselParts.Particles("Burning Sparks", b, new Color(1f, 0.92f, 0.65f, 1f), 0.08f, 0.7f, 1.8f, 0.014f, 0.45f, 200);
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
            {
                // It burns where it is heated: in the forge's coals, or in the furnace.
                CraftPiece p = station.Workshop?.Piece(e.Piece);
                bool furnace = p != null ? p.location == CraftLocation.Furnace : !station.Has(CraftTools.Forge);
                if (burnSparks == null)
                    break;
                burnSparks.transform.position = (furnace ? PointOr(furnacePoint, 4) : PointOr(forgePoint, 0)) + Vector3.up * 0.04f;
                Burst(burnSparks, 30);
                break;
            }
        }
    }

    /// <summary>
    /// Where the work of <paramref name="tool"/> is: its point, or - with none set - the station's row in front of it
    /// where Auto Build puts the effects (the forge, the anvil, the tank, the wheel, the furnace). The whetstone, the
    /// assembly (carving) bench, the ore bench and the crucible's mould have only the points they are given
    /// (<see cref="HandsOnPoint"/>); without one, this is the station's own position.
    /// </summary>
    public Vector3 PointFor(CraftTools tool)
    {
        switch (tool)
        {
            case CraftTools.Forge: return PointOr(forgePoint, 0);
            case CraftTools.Anvil: return PointOr(anvilPoint, 1);
            case CraftTools.QuenchTank: return PointOr(quenchPoint, 2);
            case CraftTools.Grinder: return PointOr(grinderPoint, 3);
            case CraftTools.Furnace: return PointOr(furnacePoint, 4);
            case CraftTools.Whetstone: return whetstonePoint != null ? whetstonePoint.position : transform.position;
            case CraftTools.AssemblyBench: return carvingPoint != null ? carvingPoint.position : transform.position;
            case CraftTools.OreBench: return oreBenchPoint != null ? oreBenchPoint.position : transform.position;
            case CraftTools.Crucible: return mouldPoint != null ? mouldPoint.position : transform.position;
            default: return transform.position;
        }
    }

    /// <summary>
    /// The station's own point for the work place <paramref name="place"/> worked by hand: the anvil's, the forge's, the
    /// wheel's, the tank's, the whetstone's, the carving bench's, the ore bench's, the mould's - false when none is set
    /// (the forge, the anvil and the tank then stand in the row their effects use: <see cref="PointFor"/>).
    /// </summary>
    public bool HandsOnPoint(CraftHandsOnMode place, out Vector3 point)
    {
        Transform t = place switch
        {
            CraftHandsOnMode.Hammer => anvilPoint,
            CraftHandsOnMode.Bellows => forgePoint,
            CraftHandsOnMode.Grind => grinderPoint,
            CraftHandsOnMode.Sharpen => whetstonePoint,
            CraftHandsOnMode.Quench => quenchPoint,
            CraftHandsOnMode.Pour => mouldPoint,
            CraftHandsOnMode.Carve => carvingPoint,
            CraftHandsOnMode.Brush => oreBenchPoint,
            _ => null,
        };
        point = t != null ? t.position : Vector3.zero;
        return t != null;
    }

    /// <summary>
    /// Hides the station's own model of <paramref name="place"/> while it is worked by hand (its mesh renderers that show,
    /// none of its particles), adding each to <paramref name="hidden"/> to be shown again by <see cref="ShowAgain"/>.
    /// </summary>
    public void HideModel(CraftHandsOnMode place, List<Renderer> hidden)
    {
        if (workPlaceModels == null)
            return;
        foreach (WorkPlaceModel w in workPlaceModels)
        {
            if (w.place != place || w.model == null) continue;
            foreach (MeshRenderer r in w.model.GetComponentsInChildren<MeshRenderer>())
                if (r != null && r.enabled && !r.forceRenderingOff && !IsEffect(r))
                {
                    r.forceRenderingOff = true;
                    hidden.Add(r);
                }
        }
    }

    /// <summary>Is the station's own model of <paramref name="place"/> set (<see cref="workPlaceModels"/>, Worked by hand)?</summary>
    public bool HasModel(CraftHandsOnMode place)
    {
        if (workPlaceModels != null)
            foreach (WorkPlaceModel w in workPlaceModels)
                if (w.place == place && w.model != null)
                    return true;
        return false;
    }

    /// <summary>
    /// For a work place whose own model is not set (<see cref="HasModel"/>): hides the station's mesh renderers that stand
    /// right where the 3D work place is built - their middle inside <paramref name="area"/> (world), none bigger than a few
    /// times it (an anvil, a wheel, a tank on the spot; never the smithy round it), never these visuals' effects - adding
    /// each to <paramref name="hidden"/> to be shown again by <see cref="ShowAgain"/>.
    /// </summary>
    public void HideOverlapping(Bounds area, List<Renderer> hidden)
    {
        Transform top = station != null ? station.transform : transform;
        float most = area.size.magnitude * 3f;
        foreach (MeshRenderer r in top.GetComponentsInChildren<MeshRenderer>())
        {
            if (r == null || !r.enabled || r.forceRenderingOff || IsEffect(r)) continue;
            Bounds b = r.bounds;
            if (!area.Contains(b.center) || b.size.magnitude > most) continue;
            r.forceRenderingOff = true;
            hidden.Add(r);
        }
    }

    /// <summary>
    /// The work places worked by hand whose point is set but whose own model is not (<see cref="HasModel"/>): the 3D work
    /// place is built there, among the station's own models - the inspector says so.
    /// </summary>
    public void UnmodelledPlaces(List<CraftHandsOnMode> into)
    {
        foreach (CraftHandsOnMode place in new[] { CraftHandsOnMode.Hammer, CraftHandsOnMode.Grind, CraftHandsOnMode.Sharpen, CraftHandsOnMode.Quench, CraftHandsOnMode.Pour, CraftHandsOnMode.Carve, CraftHandsOnMode.Brush })
            if (HandsOnPoint(place, out _) && !HasModel(place))
                into.Add(place);
    }

    /// <summary>The glow and effects these visuals drive (the coal bed, the furnace's mouth, what Auto Build made): they stay.</summary>
    private bool IsEffect(Renderer r) => r == forgeCoals || r == furnaceMouth || builtRoot != null && r.transform.IsChildOf(builtRoot);

    /// <summary>Shows again what <see cref="HideModel"/> hid (those still there), and forgets them.</summary>
    public static void ShowAgain(List<Renderer> hidden)
    {
        foreach (Renderer r in hidden)
            if (r != null) r.forceRenderingOff = false;
        hidden.Clear();
    }

    /// <summary>The way the station faces; its work places are seen from whichever side the player stands on.</summary>
    public Vector3 Front => transform.forward;
    public Vector3 Right => transform.right;

    /// <summary>
    /// A burst of sparks off the wheel where the blade touches it (the hands-on wheel), thrown from <paramref name="at"/>
    /// (world) - the grinder's sparks themselves stay where they are, so the window's grinding still throws them off the
    /// station's own wheel.
    /// </summary>
    public void GrindSparks(Vector3 at, int count)
    {
        if (grinderSparks == null || count <= 0) return;
        ParticleSystem.MainModule main = grinderSparks.main;
        Transform space = main.simulationSpace == ParticleSystemSimulationSpace.World ? null
            : main.simulationSpace == ParticleSystemSimulationSpace.Custom && main.customSimulationSpace != null ? main.customSimulationSpace : grinderSparks.transform;
        var emit = new ParticleSystem.EmitParams { position = space != null ? space.InverseTransformPoint(at) : at, applyShapeToPosition = true };
        if (!grinderSparks.isPlaying) grinderSparks.Play();
        grinderSparks.Emit(emit, count);
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
