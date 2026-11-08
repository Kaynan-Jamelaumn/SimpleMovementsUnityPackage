using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How a kind of mob gets about: where it lives (ground, air, water or both land and water), how fast it goes in each,
/// and how it moves in each - its gait (a walk, a slime's bounce, a frog's leaps, a snake's slither, a zigzag, a fish's
/// darting, a hawk's glide...). Part of the <see cref="MobProfile"/>; the <see cref="MobMotor"/> applies it on top of
/// the NavMesh path, so the AI (wandering, chasing, fleeing, flanking) is the same for every creature.
/// </summary>
/// <remarks>
/// Flyers and swimmers follow the NavMesh of the ground under them (the lake or sea bed included) and hold their body
/// at a height above it: a flyer's altitude, a swimmer's depth below the surface. Water is the same water the player
/// swims in: the generated terrain's oceans, lakes and rivers, and SwimmableWater volumes.
/// </remarks>
[Serializable]
public class MobLocomotion
{
    [Tooltip("Ground: walks, and keeps out of water deeper than its Max Wade Depth.\n" +
             "Flying: flies at its cruising altitude over land and water, comes down to fight, may land to rest.\n" +
             "Aquatic: lives only in water; never leaves it, and suffocates out of it.\n" +
             "Amphibious: walks on land and swims in water.")]
    public MobHabitat habitat = MobHabitat.Ground;

    [Header("Speed in each medium (× its walking and running speed)")]
    [Tooltip("On the ground.")]
    [Range(0f, 3f)] public float landSpeed = 1f;
    [Tooltip("In water (a land animal paddling, a fish swimming).")]
    [Range(0f, 4f)] public float waterSpeed = 0.6f;
    [Tooltip("In the air.")]
    [Range(0f, 4f)] public float airSpeed = 1.3f;

    [Header("On land")]
    [Tooltip("Walk; Hop (small hops); Bounce (a slime); Leap (long jumps); Zigzag (sharp turns from side to side); Crawl (low and slow, never runs); Slither (an S-shaped path).")]
    public MobLandGait landGait = MobLandGait.Walk;
    [Tooltip("Set the gait's height, length, air share, width and interval yourself. Off = the gait's usual shape for the mob's size.")]
    public bool customLandShape;
    public MobGaitShape landShape;
    [Tooltip("Ground mobs keep out of water deeper than this (metres): they wade through shallows and never walk along a lake bed. 0 = 80% of its height.")]
    [Min(0f)] public float maxWadeDepth = 0f;
    [Tooltip("Crawlers never run faster than this × their walking speed.")]
    [Range(1f, 3f)] public float crawlRunLimit = 1.3f;

    [Header("In water")]
    [Tooltip("Swim (steady); Undulate (a sinuous wave: eels, sea serpents); Dart (bursts and glides: small fish); Paddle (at the surface).")]
    public MobWaterGait waterGait = MobWaterGait.Swim;
    public bool customWaterShape;
    public MobGaitShape waterShape;
    [Tooltip("How far below the surface it swims (metres; 0 = at the surface). Shallower water than that: as deep as it can.")]
    [Min(0f)] public float swimDepth = 1f;
    [Tooltip("Water shallower than this (metres) is not swum in: an aquatic mob keeps to deeper water, others wade.")]
    [Min(0.1f)] public float minSwimDepth = 0.7f;
    [Tooltip("Aquatic mobs out of water: health lost a second once Stranded Grace has run out. 0 = none (it just cannot move well).")]
    [Min(0f)] public float strandedDamagePerSecond = 6f;
    [Tooltip("Seconds an aquatic mob survives out of water before it starts to suffocate.")]
    [Min(0f)] public float strandedGrace = 4f;

    [Header("In the air")]
    [Tooltip("Flap (steady); Hover (holds its place: insects, wisps); Glide (long glides, then a climb: birds of prey); Flutter (erratic: bats, moths).")]
    public MobAirGait airGait = MobAirGait.Flap;
    public bool customAirShape;
    public MobGaitShape airShape;
    [Tooltip("Height it flies at above the ground (or the water) when travelling or wandering (metres).")]
    [Min(0f)] public float cruiseAltitude = 6f;
    [Tooltip("Height it comes down to when it fights at close range, so its bites and claws reach (metres).")]
    [Min(0f)] public float combatAltitude = 1f;
    [Tooltip("Within this distance of its target (metres) it comes down to Combat Altitude.")]
    [Min(0f)] public float descendWithin = 8f;
    [Tooltip("How fast it climbs (metres a second).")]
    [Min(0.1f)] public float climbRate = 3f;
    [Tooltip("How fast it dives or comes down (metres a second).")]
    [Min(0.1f)] public float diveRate = 6f;
    [Tooltip("Lands and walks when it has nothing to do (birds); off = always in the air (bats, insects, wisps).")]
    public bool landsToRest = true;
    [Tooltip("Seconds without a destination before it lands to rest.")]
    [Min(0f)] public float restAfter = 6f;

    // ------------------------------------------------------------------ derived
    /// <summary>Can it ever be in water on purpose?</summary>
    public bool Swims => habitat == MobHabitat.Aquatic || habitat == MobHabitat.Amphibious;

    public bool Flies => habitat == MobHabitat.Flying;

    /// <summary>The deepest water it walks into (metres), for a mob <paramref name="height"/> tall.</summary>
    public float WadeDepth(float height) => maxWadeDepth > 0f ? maxWadeDepth : Mathf.Max(0.3f, height * 0.8f);

    public MobGaitShape LandShape(float height) => customLandShape ? landShape : MobGait.DefaultShape(landGait, height);
    public MobGaitShape WaterShape(float height) => customWaterShape ? waterShape : MobGait.DefaultShape(waterGait, height);
    public MobGaitShape AirShape(float height) => customAirShape ? airShape : MobGait.DefaultShape(airGait, height);

    /// <summary>The speed multiplier of <paramref name="medium"/>.</summary>
    public float SpeedIn(MobMedium medium)
    {
        switch (medium)
        {
            case MobMedium.Water: return waterSpeed;
            case MobMedium.Air: return airSpeed;
            default: return landSpeed;
        }
    }

    /// <summary>
    /// May it go to a place with water of <paramref name="depth"/> metres (0 = dry land)? Aquatic: only water at least
    /// Min Swim Depth deep; Ground: only water no deeper than its wade depth; Flying and Amphibious: anywhere.
    /// </summary>
    public bool AllowsDepth(float depth, float height)
    {
        switch (habitat)
        {
            case MobHabitat.Aquatic: return depth >= minSwimDepth;
            case MobHabitat.Ground: return depth <= WadeDepth(height);
            default: return true;
        }
    }

    public MobLocomotion Clone()
    {
        var c = (MobLocomotion)MemberwiseClone();
        return c;
    }

    /// <summary>One line for the inspector: "Flies (glides) at 6 m; lands to rest".</summary>
    public string Describe()
    {
        switch (habitat)
        {
            case MobHabitat.Flying:
                return $"Flies ({Word(airGait)}) at {cruiseAltitude:0.#} m, down to {combatAltitude:0.#} m to fight" + (landsToRest ? $"; lands to rest ({Word(landGait)} on the ground)" : "; never lands") + ".";
            case MobHabitat.Aquatic:
                return $"Lives in water ({Word(waterGait)}, {swimDepth:0.#} m under the surface); out of it it suffocates" + (strandedDamagePerSecond > 0f ? $" after {strandedGrace:0.#} s" : "") + ".";
            case MobHabitat.Amphibious:
                return $"Land and water: {Word(landGait)} on land (× {landSpeed:0.##}), {Word(waterGait)} in water (× {waterSpeed:0.##}).";
            default:
                return $"Ground: {Word(landGait)}" + (landSpeed != 1f ? $" (× {landSpeed:0.##})" : "") + "; keeps out of deep water.";
        }
    }

    private static string Word(Enum e) => e.ToString().ToLowerInvariant();

    public void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (Flies && airSpeed <= 0f)
            errors.Add($"{owner}: a flyer with Air Speed 0 cannot move.");
        if (habitat == MobHabitat.Aquatic && waterSpeed <= 0f)
            errors.Add($"{owner}: an aquatic mob with Water Speed 0 cannot move.");
        if (habitat != MobHabitat.Aquatic && landSpeed <= 0f && !Flies)
            warnings.Add($"{owner}: Land Speed is 0: it never moves on land.");
        if (Flies && combatAltitude > cruiseAltitude)
            warnings.Add($"{owner}: Combat Altitude is above Cruise Altitude: it climbs to fight.");
        if (Flies && cruiseAltitude > 40f)
            warnings.Add($"{owner}: Cruise Altitude {cruiseAltitude:0} m: players will hardly see or reach it.");
        if (Swims && swimDepth > 0f && swimDepth < 0.3f && waterGait != MobWaterGait.Paddle)
            warnings.Add($"{owner}: Swim Depth under 0.3 m keeps its back out of the water: use Paddle for a surface swimmer.");
        if (customLandShape && (landGait == MobLandGait.Hop || landGait == MobLandGait.Bounce || landGait == MobLandGait.Leap) && landShape.length <= 0f)
            errors.Add($"{owner}: a hopping gait needs a Length (the distance of one hop).");
    }

    // ------------------------------------------------------------------ presets
    /// <summary>Typical creatures (Assets ▸ Create presets and the Mob Profile's Locomotion Preset menu).</summary>
    public enum Preset
    {
        Walker,
        Hopper,
        Slime,
        Frog,
        Snake,
        Crawler,
        Skittering,
        Bird,
        BirdOfPrey,
        Bat,
        Insect,
        Fish,
        Shark,
        SeaSerpent,
        Crocodile,
        Turtle,
    }

    public static MobLocomotion Create(Preset preset)
    {
        var l = new MobLocomotion();
        l.Apply(preset);
        return l;
    }

    /// <summary>Fills it as a typical <paramref name="preset"/> (shapes from the mob's size: customise them after).</summary>
    public void Apply(Preset preset)
    {
        var d = new MobLocomotion();
        habitat = d.habitat; landSpeed = d.landSpeed; waterSpeed = d.waterSpeed; airSpeed = d.airSpeed;
        landGait = d.landGait; customLandShape = false; maxWadeDepth = d.maxWadeDepth; crawlRunLimit = d.crawlRunLimit;
        waterGait = d.waterGait; customWaterShape = false; swimDepth = d.swimDepth; minSwimDepth = d.minSwimDepth;
        strandedDamagePerSecond = d.strandedDamagePerSecond; strandedGrace = d.strandedGrace;
        airGait = d.airGait; customAirShape = false; cruiseAltitude = d.cruiseAltitude; combatAltitude = d.combatAltitude;
        descendWithin = d.descendWithin; climbRate = d.climbRate; diveRate = d.diveRate; landsToRest = d.landsToRest; restAfter = d.restAfter;
        switch (preset)
        {
            case Preset.Hopper:
                landGait = MobLandGait.Hop;
                waterGait = MobWaterGait.Paddle;
                break;
            case Preset.Slime:
                landGait = MobLandGait.Bounce;
                landSpeed = 0.8f;
                waterGait = MobWaterGait.Paddle;
                waterSpeed = 0.3f;
                break;
            case Preset.Frog:
                habitat = MobHabitat.Amphibious;
                landGait = MobLandGait.Leap;
                waterGait = MobWaterGait.Swim;
                waterSpeed = 1.1f;
                swimDepth = 0.4f;
                minSwimDepth = 0.3f;
                break;
            case Preset.Snake:
                landGait = MobLandGait.Slither;
                waterGait = MobWaterGait.Undulate;
                waterSpeed = 0.9f;
                swimDepth = 0.3f;
                break;
            case Preset.Crawler:
                landGait = MobLandGait.Crawl;
                landSpeed = 0.7f;
                crawlRunLimit = 1.2f;
                break;
            case Preset.Skittering:
                landGait = MobLandGait.Zigzag;
                landSpeed = 1.15f;
                break;
            case Preset.Bird:
                habitat = MobHabitat.Flying;
                airGait = MobAirGait.Flap;
                landGait = MobLandGait.Hop;
                landSpeed = 0.5f;
                cruiseAltitude = 5f;
                combatAltitude = 1f;
                landsToRest = true;
                break;
            case Preset.BirdOfPrey:
                habitat = MobHabitat.Flying;
                airGait = MobAirGait.Glide;
                landGait = MobLandGait.Walk;
                landSpeed = 0.4f;
                airSpeed = 1.6f;
                cruiseAltitude = 12f;
                combatAltitude = 1.2f;
                descendWithin = 14f;
                diveRate = 12f;
                landsToRest = true;
                restAfter = 10f;
                break;
            case Preset.Bat:
                habitat = MobHabitat.Flying;
                airGait = MobAirGait.Flutter;
                cruiseAltitude = 3f;
                combatAltitude = 1.4f;
                landsToRest = false;
                break;
            case Preset.Insect:
                habitat = MobHabitat.Flying;
                airGait = MobAirGait.Hover;
                cruiseAltitude = 2f;
                combatAltitude = 1.2f;
                climbRate = 4f;
                landsToRest = false;
                break;
            case Preset.Fish:
                habitat = MobHabitat.Aquatic;
                waterGait = MobWaterGait.Dart;
                waterSpeed = 1.2f;
                swimDepth = 0.8f;
                minSwimDepth = 0.5f;
                break;
            case Preset.Shark:
                habitat = MobHabitat.Aquatic;
                waterGait = MobWaterGait.Swim;
                waterSpeed = 1.5f;
                swimDepth = 1.5f;
                minSwimDepth = 1.5f;
                strandedGrace = 6f;
                break;
            case Preset.SeaSerpent:
                habitat = MobHabitat.Aquatic;
                waterGait = MobWaterGait.Undulate;
                waterSpeed = 1.4f;
                swimDepth = 2f;
                minSwimDepth = 2f;
                strandedDamagePerSecond = 10f;
                break;
            case Preset.Crocodile:
                habitat = MobHabitat.Amphibious;
                landGait = MobLandGait.Crawl;
                landSpeed = 0.75f;
                crawlRunLimit = 1.8f;
                waterGait = MobWaterGait.Undulate;
                waterSpeed = 1.3f;
                swimDepth = 0.3f;
                break;
            case Preset.Turtle:
                habitat = MobHabitat.Amphibious;
                landGait = MobLandGait.Crawl;
                landSpeed = 0.35f;
                crawlRunLimit = 1.1f;
                waterGait = MobWaterGait.Paddle;
                waterSpeed = 0.9f;
                swimDepth = 0f;
                break;
        }
    }
}
