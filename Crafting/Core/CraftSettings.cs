using System;
using UnityEngine;

/// <summary>
/// The physics of the crafting simulation: the time step, heating and cooling, the fire, scale and decarburisation,
/// the steel model (hardening, tempering, grain), forging, the quench, abrasion, smelting and casting, leather and the
/// time scale of slow processes. Shared by every station; tune it on the Crafting Database.
/// </summary>
[Serializable]
public class CraftSimulationSettings
{
    [Header("Simulation")]
    [Tooltip("Seconds per simulation step. Workshops run in whole steps (and operations happen between steps), so the " +
             "same operations at the same times always give the same result.")]
    [Range(0.02f, 0.5f)] public float stepSeconds = 0.1f;
    [Tooltip("Room temperature (°C) when the surroundings give none.")]
    public float defaultAmbient = 18f;
    [Tooltip("Segments a metal or wooden piece is divided into along its length.")]
    [Range(8, 48)] public int segments = 24;
    [Tooltip("Size of a hide cell (cm).")]
    [Min(1f)] public float cellSize = 10f;

    [Header("Heat")]
    [Tooltip("Cooling in air: convection (per °C of difference) and radiation (per (K⁴ difference) / 1e9), × the surface over the volume (1/mm).")]
    [Min(0f)] public float airConvection = 0.004f;
    [Min(0f)] public float airRadiation = 0.01f;
    [Tooltip("Heating in the fire per °C of difference × surface/volume, at the heart of the fire.")]
    [Min(0f)] public float fireTransfer = 0.07f;
    [Tooltip("Heat flow along a bar between neighbouring segments.")]
    [Min(0f)] public float conduction = 40f;
    [Tooltip("Heat flow between the edge and the body of a segment.")]
    [Min(0f)] public float zoneConduction = 0.6f;
    [Tooltip("Heating in the oven per °C × surface/volume (gentle).")]
    [Min(0f)] public float ovenTransfer = 0.08f;
    [Tooltip("Cooling in warm ashes (very slow: annealing) per °C × surface/volume.")]
    [Min(0f)] public float ashTransfer = 0.0008f;
    [Tooltip("Ashes start at this temperature and slowly cool (°C).")]
    public float ashTemperature = 450f;
    [Tooltip("Quench heat transfer per °C × severity × surface/volume.")]
    [Min(0f)] public float quenchTransfer = 0.47f;
    [Tooltip("Quench severity × this when the piece is moved (any motion), and the vapour jacket is broken.")]
    [Min(1f)] public float quenchMotionBoost = 1.3f;

    [Header("Fire")]
    [Tooltip("Fuel (burn units) that gives about two thirds of the full fire.")]
    [Min(0.01f)] public float fuelScale = 2f;
    [Tooltip("Burn units per second at full fire (× the fuel's burn rate).")]
    [Min(0f)] public float fuelBurnRate = 0.01f;
    [Tooltip("Seconds for the flames to rise and to die down.")]
    [Min(0.1f)] public float fireRiseSeconds = 6f;
    [Min(0.1f)] public float fireFallSeconds = 14f;
    [Tooltip("Draft with the air vent open and no bellows (0-1); bellows air adds Bellows Draft per unit.")]
    [Range(0f, 1f)] public float draftBase = 0.5f;
    [Min(0f)] public float bellowsDraft = 0.7f;
    [Tooltip("Air added by one full pump of the bellows, and the seconds it takes to fade.")]
    [Range(0f, 1f)] public float bellowsGain = 0.3f;
    [Min(0.1f)] public float bellowsDecaySeconds = 3f;
    [Tooltip("Temperature of the fire's zones relative to its heart: the middle and the edge of the coals.")]
    [Range(0f, 1f)] public float fireMiddle = 0.9f;
    [Range(0f, 1f)] public float fireEdge = 0.72f;

    [Header("Scale and the surface")]
    [Tooltip("Scale (mm) grown per second at 1000 °C in air (×2 every Scale Doubling Degrees hotter), parabolic: slower as it thickens.")]
    [Min(0f)] public float scaleRate = 0.0015f;
    [Min(1f)] public float scaleDoublingDegrees = 90f;
    [Tooltip("Metal lost per mm of scale grown (mm of thickness, per face).")]
    [Min(0f)] public float scaleMetalLoss = 0.45f;
    [Tooltip("Surface carbon loss per second at 1000 °C in an oxidising fire (×2 every 100 °C).")]
    [Min(0f)] public float decarbRate = 0.002f;
    [Tooltip("Carbon (fraction) gained per second at 1000 °C buried in a reducing charcoal fire, for a 1 mm section.")]
    [Min(0f)] public float carburizeRate = 0.000002f;

    [Header("Steel")]
    [Tooltip("The base metal that hardens by quenching, and the element that makes it hardenable.")]
    public string steelBase = "iron";
    public string steelCarbon = "carbon";
    [Tooltip("Carbon (fraction) below which steel barely hardens, and the eutectoid.")]
    [Range(0f, 0.01f)] public float hardenableCarbon = 0.0025f;
    [Range(0.001f, 0.02f)] public float eutectoidCarbon = 0.0077f;
    [Tooltip("Above this carbon (fraction) it is cast iron: it cannot be forged.")]
    [Range(0.005f, 0.05f)] public float castIronCarbon = 0.021f;
    [Tooltip("Lower critical temperature (°C) and the upper one of pure iron.")]
    public float a1 = 727f;
    public float a3Iron = 912f;
    [Tooltip("Seconds pearlite takes to begin forming at its fastest (550 °C) in eutectoid steel: shorter = harder to harden.")]
    [Min(0.05f)] public float pearliteNoseSeconds = 5f;
    [Tooltip("Seconds of soaking above the critical temperature to turn fully into austenite.")]
    [Min(0.05f)] public float austenitizeSeconds = 6f;
    [Tooltip("Tempering: simulated time runs this many times faster than the clock (an hour at 200 °C is two minutes).")]
    [Min(1f)] public float temperTimeScale = 30f;
    [Tooltip("Grain growth per second at 100 °C above the metal's grain-growth temperature (×2 every 50 °C).")]
    [Min(0f)] public float grainGrowthRate = 0.01f;
    [Tooltip("Burning: per second at 50 °C above the burn temperature.")]
    [Min(0f)] public float burnRate = 0.05f;
    [Tooltip("Martensite forming builds residual stress × this (× carbon, × cooling speed); stress above Crack Stress cracks it.")]
    [Min(0f)] public float quenchStress = 2.2f;
    [Min(0.1f)] public float crackStress = 1f;
    [Tooltip("Warp (degrees over the whole piece) of a fully hardened piece quenched unevenly.")]
    [Min(0f)] public float quenchWarp = 12f;

    [Header("Forging")]
    [Tooltip("How much a full blow thins a 6 mm bar at forging heat (share of its thickness).")]
    [Range(0.01f, 0.3f)] public float strikeReduction = 0.07f;
    [Tooltip("Heat a blow takes into the anvil, per mm⁻¹ of thinness (°C).")]
    [Min(0f)] public float anvilChill = 9f;
    [Tooltip("Welding heat: this many °C below the burn temperature.")]
    [Min(0f)] public float weldingMargin = 130f;
    [Tooltip("Carbon (share of what it has) lost by each fold at welding heat.")]
    [Range(0f, 0.2f)] public float foldCarbonLoss = 0.03f;

    [Header("Abrasion")]
    [Tooltip("Grinding at full pressure heats a 1 mm edge this many °C per second × the wheel's heat.")]
    [Min(0f)] public float grindHeat = 60f;
    [Tooltip("Whetstone strokes: share of the way to the stone's apex per stroke at full pressure.")]
    [Range(0.001f, 0.5f)] public float sharpenRate = 0.08f;

    [Header("Hand work in play")]
    [Tooltip("At a station, a real second at the wheel, the file, the brush, the polish or the carving knife does this many " +
             "seconds of work (the rules count seconds of life: grinding a sword's bevels takes a smith an afternoon). " +
             "Scripts and tests that call the workshop directly count real seconds.")]
    [Min(0.1f)] public float handWorkPace = 4f;
    [Tooltip("Working from the station's window (the drawings) is careful work: this share of the pace for a novice...")]
    [Range(0.1f, 1f)] public float windowPaceNovice = 0.6f;
    [Tooltip("...and this share for a master (practice from the journal). By hand, in 3D, the pace is always full.")]
    [Range(0.1f, 1f)] public float windowPaceMaster = 0.9f;

    [Header("Smelting and casting")]
    [Tooltip("Mineral (g) reduced per second at full rate in a hot, reducing furnace (× the ore's rate).")]
    [Min(0f)] public float reductionRate = 4f;
    [Tooltip("Slag (g) the furnace holds before it starts to choke its draft.")]
    [Min(1f)] public float slagCapacity = 3000f;
    [Tooltip("Iron taken into the slag per gram of silica, unless fluxed.")]
    [Min(0f)] public float ironPerSilica = 1.5f;
    [Tooltip("Share of the loose rock's silica that reacts with the iron (lumps of rock only partly melt in); bound silica all reacts.")]
    [Range(0f, 1f)] public float gangueReactivity = 0.2f;
    [Tooltip("The crucible follows the furnace's temperature with this time constant (s).")]
    [Min(1f)] public float crucibleLag = 12f;
    [Tooltip("Superheat (°C above melting) a pour needs to fill a mould, and where gas porosity starts.")]
    public float pourSuperheat = 40f;
    public float porositySuperheat = 220f;

    [Header("Leather and slow processes")]
    [Tooltip("Hours of vat, drying and tanning time per second of the clock (a two-day tanning is eight minutes).")]
    [Min(0.001f)] public float processHoursPerSecond = 0.1f;
    [Tooltip("Hours for a wet hide to dry in still room air (faster when warm, with air moving, when the air is dry).")]
    [Min(0.1f)] public float dryingHours = 24f;
    [Tooltip("Hours a raw, wet hide lasts at 20 °C before it starts to rot.")]
    [Min(0.1f)] public float rotHours = 48f;

    [Header("Observation")]
    [Tooltip("Times a material class must be worked before the player recognises it without an assay.")]
    [Min(1)] public int workingsToRecognise = 3;
    [Tooltip("Times the player must read a thermometer at a heat colour before they learn its temperature.")]
    [Min(1)] public int readingsToLearnHeat = 2;
}
