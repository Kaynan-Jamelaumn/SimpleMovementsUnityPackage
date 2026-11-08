using System;

/// <summary>Where a mob lives and can move.</summary>
public enum MobHabitat
{
    /// <summary>Walks on the ground; wades through shallow water but keeps out of water deeper than its Max Wade Depth.</summary>
    Ground,
    /// <summary>Flies at its cruising altitude (over land and water), comes down to fight, and may land to rest.</summary>
    Flying,
    /// <summary>Lives only in water: it never leaves it on purpose, and out of it (washed up, knocked ashore) it suffocates.</summary>
    Aquatic,
    /// <summary>Walks on land and swims in water (crocodiles, frogs, turtles), each at its own speed.</summary>
    Amphibious,
}

/// <summary>What a mob is moving through right now.</summary>
public enum MobMedium
{
    Ground,
    Water,
    Air,
}

/// <summary>How a mob moves on the ground.</summary>
public enum MobLandGait
{
    /// <summary>An ordinary walk or run.</summary>
    Walk,
    /// <summary>Small quick hops (rabbits, small birds on the ground): it moves while in the air and pauses between hops.</summary>
    Hop,
    /// <summary>A slime's springy bounce: squashed on landing, stretched in the air, barely touching the ground between bounces.</summary>
    Bounce,
    /// <summary>Long, high leaps with a crouch between them (frogs, kangaroos, jumping spiders).</summary>
    Leap,
    /// <summary>Sharp changes of direction from side to side as it goes (scuttling insects, panicked prey).</summary>
    Zigzag,
    /// <summary>Low and steady (worms, beetles, crocodiles on land): it never breaks into a run.</summary>
    Crawl,
    /// <summary>A sinuous S-shaped path (snakes, eels on land).</summary>
    Slither,
}

/// <summary>How a mob moves in water.</summary>
public enum MobWaterGait
{
    /// <summary>A steady swim with a gentle rise and fall.</summary>
    Swim,
    /// <summary>A sinuous wave along the body (eels, sea serpents, crocodiles).</summary>
    Undulate,
    /// <summary>A burst, then a glide (small fish).</summary>
    Dart,
    /// <summary>Swims at the surface, head out (ducks, turtles, swimming land animals).</summary>
    Paddle,
}

/// <summary>How a mob moves in the air.</summary>
public enum MobAirGait
{
    /// <summary>Steady flapping flight with a slight rise and fall at each wingbeat.</summary>
    Flap,
    /// <summary>Holds its place in the air easily, bobbing (insects, wisps, small drakes).</summary>
    Hover,
    /// <summary>Long glides that slowly lose height, then a few wingbeats to climb back (birds of prey).</summary>
    Glide,
    /// <summary>Erratic, jinking flight (bats, moths).</summary>
    Flutter,
}

/// <summary>The settings of a gait (only the ones its kind uses matter).</summary>
[Serializable]
public struct MobGaitShape
{
    /// <summary>Height of a hop, bounce or leap (metres), or of the bob of a swim or flight.</summary>
    public float height;
    /// <summary>Distance covered by one hop, bounce or leap, or one wave of a slither (metres).</summary>
    public float length;
    /// <summary>Share of each hop spent in the air (0.2 - 0.95); the rest is a pause on the ground.</summary>
    public float airShare;
    /// <summary>How far to each side a zigzag, slither or undulation takes it (metres).</summary>
    public float width;
    /// <summary>Seconds between the changes of side of a zigzag (or a flutter's jinks).</summary>
    public float interval;
}

/// <summary>A mob's progress through its gait (kept by the motor between frames).</summary>
public struct MobGaitState
{
    /// <summary>Position in the current cycle (0 .. 1): a hop, a bounce, a wave, a dart, a glide.</summary>
    public float phase;
    /// <summary>Seconds since the last change of side (zigzag, flutter).</summary>
    public float sideTimer;
    /// <summary>+1 = moving to the right, -1 = to the left (zigzag, flutter).</summary>
    public float side;
    /// <summary>The seconds until the next change of side.</summary>
    public float nextSwitch;
    /// <summary>Random state (a mob's own sequence: two mobs never zigzag in step).</summary>
    public uint seed;
    /// <summary>Seconds of idle bob (swim, hover).</summary>
    public float bobTime;

    public static MobGaitState For(int mobId)
    {
        uint s = (uint)mobId * 2654435761u + 0x9E3779B9u;
        if (s == 0) s = 1;
        return new MobGaitState { side = 1f, seed = s, nextSwitch = -1f };
    }

    /// <summary>The next number of the mob's own random sequence (0 .. 1).</summary>
    public float Next01()
    {
        seed ^= seed << 13;
        seed ^= seed >> 17;
        seed ^= seed << 5;
        return (seed & 0xFFFFFF) / (float)0x1000000;
    }
}

/// <summary>What a gait does to the movement this frame.</summary>
public struct MobGaitStep
{
    /// <summary>The forward speed this frame as a share of the mob's pace (a hop moves only while in the air: more than 1 then, 0 on the ground).</summary>
    public float speedScale;
    /// <summary>Height of the body above where the gait would otherwise keep it (a hop's arc, a swimmer's bob; metres).</summary>
    public float lift;
    /// <summary>Sideways speed (metres a second, + = to the mob's right): a zigzag, a slither.</summary>
    public float lateral;
    /// <summary>In the air at this moment (a hop or a leap; not flying).</summary>
    public bool airborne;
    /// <summary>For the model: 1 = its normal shape, under 1 = squashed (landing), over 1 = stretched (a bounce in the air).</summary>
    public float squash;

    public static MobGaitStep Still => new MobGaitStep { speedScale = 1f, squash = 1f };
}

/// <summary>
/// The rules of every gait, as pure arithmetic (no Unity objects): given how fast the mob is going and the time that
/// passed, how much faster or slower it goes this frame, how high its body is lifted and how far it drifts sideways. A
/// hop covers its length while in the air and pauses on the ground, so on average it keeps the mob's pace; a zigzag
/// switches side at its own random moments; a slither follows a sine wave by the distance covered. The motor applies
/// it on top of the NavMesh path, so pathfinding, hazards and the AI are the same for every gait.
/// </summary>
public static class MobGait
{
    /// <summary>The usual shape of a land gait (scaled by the mob's height).</summary>
    public static MobGaitShape DefaultShape(MobLandGait gait, float height)
    {
        float h = Math.Max(0.2f, height);
        switch (gait)
        {
            case MobLandGait.Hop: return new MobGaitShape { height = 0.25f * h, length = 0.9f * h, airShare = 0.7f };
            case MobLandGait.Bounce: return new MobGaitShape { height = 0.6f * h, length = 1.2f * h, airShare = 0.85f };
            case MobLandGait.Leap: return new MobGaitShape { height = 0.8f * h, length = 2.5f * h, airShare = 0.6f };
            case MobLandGait.Zigzag: return new MobGaitShape { width = 0.8f * h, interval = 0.6f };
            case MobLandGait.Slither: return new MobGaitShape { width = 0.25f * h, length = 2.5f * h };
            default: return new MobGaitShape();
        }
    }

    public static MobGaitShape DefaultShape(MobWaterGait gait, float height)
    {
        float h = Math.Max(0.2f, height);
        switch (gait)
        {
            case MobWaterGait.Undulate: return new MobGaitShape { height = 0.05f * h, width = 0.3f * h, length = 3f * h };
            case MobWaterGait.Dart: return new MobGaitShape { height = 0.03f * h, interval = 1.4f };
            default: return new MobGaitShape { height = 0.06f * h, interval = 3f };
        }
    }

    public static MobGaitShape DefaultShape(MobAirGait gait, float height)
    {
        float h = Math.Max(0.2f, height);
        switch (gait)
        {
            case MobAirGait.Hover: return new MobGaitShape { height = 0.15f * h, interval = 2f };
            case MobAirGait.Glide: return new MobGaitShape { height = 1.5f, interval = 6f };
            case MobAirGait.Flutter: return new MobGaitShape { height = 0.3f * h, width = 0.6f * h, interval = 0.35f };
            default: return new MobGaitShape { height = 0.12f * h, interval = 0.7f };
        }
    }

    // ------------------------------------------------------------------ land
    /// <summary>
    /// One frame of a land gait at <paramref name="speed"/> metres a second (0 = standing). <paramref name="dt"/> in seconds.
    /// </summary>
    public static MobGaitStep Land(MobLandGait gait, MobGaitShape shape, ref MobGaitState s, float speed, float dt)
    {
        switch (gait)
        {
            case MobLandGait.Hop:
            case MobLandGait.Bounce:
            case MobLandGait.Leap:
                return Hops(gait, shape, ref s, speed, dt);
            case MobLandGait.Zigzag:
                return Zigzag(shape, ref s, speed, dt);
            case MobLandGait.Slither:
                return Wave(shape, ref s, speed, dt);
            default:
                s.phase = 0f;
                return MobGaitStep.Still;
        }
    }

    /// <summary>
    /// Hops: a cycle covers <see cref="MobGaitShape.length"/>; the mob is in the air for <see cref="MobGaitShape.airShare"/> of
    /// it, moving at pace / air share (so the average is its pace), and stands still for the rest. Height follows a parabola.
    /// A mob that stops mid-hop finishes the hop (it does not hang in the air).
    /// </summary>
    private static MobGaitStep Hops(MobLandGait gait, MobGaitShape shape, ref MobGaitState s, float speed, float dt)
    {
        float length = Math.Max(0.05f, shape.length);
        float air = Clamp(shape.airShare, 0.2f, 0.95f);
        // A cycle lasts as long as its length takes at the mob's pace; standing, a started hop still lands.
        float pace = speed > 0.05f ? speed : (s.phase > 0f && s.phase < air ? length / 0.6f : 0f);
        if (pace <= 0f)
        {
            s.phase = 0f;
            return new MobGaitStep { speedScale = 0f, squash = 1f };
        }
        float cycle = length / pace;
        s.phase += dt / cycle;
        if (s.phase >= 1f)
            s.phase -= (float)Math.Floor(s.phase);
        var step = new MobGaitStep { squash = 1f };
        if (s.phase < air)
        {
            float p = s.phase / air;
            step.airborne = true;
            step.lift = 4f * Math.Max(0f, shape.height) * p * (1f - p);
            step.speedScale = speed > 0.05f ? 1f / air : 0f;
            if (gait == MobLandGait.Bounce)
                step.squash = 1f + 0.25f * (float)Math.Sin(p * Math.PI); // stretched mid-air
        }
        else
        {
            float g = (s.phase - air) / (1f - air);
            step.speedScale = 0f;
            if (gait != MobLandGait.Hop)
                step.squash = 1f - (gait == MobLandGait.Bounce ? 0.35f : 0.2f) * (float)Math.Sin(g * Math.PI); // crouched on the ground
        }
        return step;
    }

    /// <summary>
    /// A zigzag: sideways at <see cref="MobGaitShape.width"/> per <see cref="MobGaitShape.interval"/>, switching side at
    /// moments drawn from the mob's own random sequence (between 0.6 and 1.4 intervals).
    /// </summary>
    private static MobGaitStep Zigzag(MobGaitShape shape, ref MobGaitState s, float speed, float dt)
    {
        if (speed <= 0.05f)
            return MobGaitStep.Still;
        float interval = Math.Max(0.1f, shape.interval);
        if (s.nextSwitch < 0f)
            s.nextSwitch = interval * (0.6f + 0.8f * s.Next01());
        s.sideTimer += dt;
        if (s.sideTimer >= s.nextSwitch)
        {
            s.sideTimer = 0f;
            s.side = s.side >= 0f ? -1f : 1f;
            s.nextSwitch = interval * (0.6f + 0.8f * s.Next01());
        }
        float lateral = Math.Max(0f, shape.width) / interval;
        // Never faster sideways than forwards: the path stays a zigzag, not a crab walk.
        lateral = Math.Min(lateral, speed * 0.9f);
        return new MobGaitStep { speedScale = 1f, lateral = s.side * lateral, squash = 1f };
    }

    /// <summary>
    /// A wave: the path follows a sine of amplitude <see cref="MobGaitShape.width"/> and wavelength
    /// <see cref="MobGaitShape.length"/>, by the distance covered (so a slower snake slithers more slowly).
    /// </summary>
    private static MobGaitStep Wave(MobGaitShape shape, ref MobGaitState s, float speed, float dt)
    {
        if (speed <= 0.05f)
            return MobGaitStep.Still;
        float wavelength = Math.Max(0.1f, shape.length);
        s.phase += speed * dt / wavelength;
        if (s.phase >= 1f)
            s.phase -= (float)Math.Floor(s.phase);
        // d/dt of A sin(2π d / λ) = A 2π v / λ cos(...)
        float lateral = Math.Max(0f, shape.width) * 2f * (float)Math.PI * speed / wavelength * (float)Math.Cos(2.0 * Math.PI * s.phase);
        return new MobGaitStep { speedScale = 1f, lateral = lateral, squash = 1f };
    }

    // ------------------------------------------------------------------ water
    /// <summary>One frame of a water gait at <paramref name="speed"/> metres a second.</summary>
    public static MobGaitStep Water(MobWaterGait gait, MobGaitShape shape, ref MobGaitState s, float speed, float dt)
    {
        s.bobTime += dt;
        float bobPeriod = Math.Max(0.5f, shape.interval > 0f ? shape.interval : 3f);
        float bob = Math.Max(0f, shape.height) * (float)Math.Sin(2.0 * Math.PI * s.bobTime / bobPeriod);
        switch (gait)
        {
            case MobWaterGait.Undulate:
            {
                MobGaitStep w = Wave(shape, ref s, speed, dt);
                w.lift = bob;
                return w;
            }
            case MobWaterGait.Dart:
            {
                if (speed <= 0.05f)
                    return new MobGaitStep { speedScale = 1f, lift = bob, squash = 1f };
                float cycle = Math.Max(0.3f, shape.interval > 0f ? shape.interval : 1.4f);
                s.phase += dt / cycle;
                if (s.phase >= 1f)
                    s.phase -= (float)Math.Floor(s.phase);
                return new MobGaitStep { speedScale = DartScale(s.phase), lift = bob, squash = 1f };
            }
            case MobWaterGait.Paddle:
                return new MobGaitStep { speedScale = 1f, lift = bob * 0.5f, squash = 1f };
            default:
                return new MobGaitStep { speedScale = 1f, lift = bob, squash = 1f };
        }
    }

    /// <summary>
    /// A dart's speed through its cycle: a burst at 2 × the pace for the first fifth, then a glide that slows from 1.15 ×
    /// to 0.35 ×. Over a whole cycle it averages the pace exactly (2 × 0.2 + 0.75 × 0.8 = 1).
    /// </summary>
    public static float DartScale(float phase)
    {
        if (phase < 0.2f)
            return 2f;
        float g = (phase - 0.2f) / 0.8f;
        return 1.15f - 0.8f * g;
    }

    // ------------------------------------------------------------------ air
    /// <summary>One frame of an air gait at <paramref name="speed"/> metres a second.</summary>
    public static MobGaitStep Air(MobAirGait gait, MobGaitShape shape, ref MobGaitState s, float speed, float dt)
    {
        s.bobTime += dt;
        float period = Math.Max(0.2f, shape.interval > 0f ? shape.interval : 0.7f);
        switch (gait)
        {
            case MobAirGait.Glide:
            {
                // A glide sinks slowly for most of the cycle and climbs back with a few wingbeats: a sawtooth of
                // Height metres. Only while it travels; hovering in place, a glider flaps (a small bob).
                if (speed <= 0.05f)
                    return new MobGaitStep { speedScale = 1f, lift = 0.1f * (float)Math.Sin(2.0 * Math.PI * s.bobTime / 0.7f), squash = 1f };
                s.phase += dt / period;
                if (s.phase >= 1f)
                    s.phase -= (float)Math.Floor(s.phase);
                float h = Math.Max(0f, shape.height);
                float lift = s.phase < 0.8f ? h * (1f - s.phase / 0.8f) : h * ((s.phase - 0.8f) / 0.2f);
                return new MobGaitStep { speedScale = 1f, lift = lift - h * 0.5f, squash = 1f };
            }
            case MobAirGait.Flutter:
            {
                MobGaitStep z = Zigzag(shape, ref s, Math.Max(speed, 1f), dt);
                z.lift = Math.Max(0f, shape.height) * (float)Math.Sin(2.0 * Math.PI * s.bobTime / 0.45f) * (0.6f + 0.4f * s.side);
                if (speed <= 0.05f)
                    z.lateral *= 0.3f; // a bat hanging in the air still jinks a little
                return z;
            }
            default:
                return new MobGaitStep
                {
                    speedScale = 1f,
                    lift = Math.Max(0f, shape.height) * (float)Math.Sin(2.0 * Math.PI * s.bobTime / period),
                    squash = 1f,
                };
        }
    }

    // ------------------------------------------------------------------ heights
    /// <summary>
    /// How high a swimmer's feet (its transform) are held above the ground under it (the lake or sea bed its NavMesh
    /// follows): the middle of its body <paramref name="swimDepth"/> below the surface, its back never out of the water;
    /// with <paramref name="atSurface"/> (paddling) three quarters under, head out. Water too shallow for that: on the
    /// bottom (0), wading.
    /// </summary>
    public static float SwimOffset(float surfaceY, float groundY, float swimDepth, float halfHeight, bool atSurface)
    {
        float top = surfaceY - groundY;
        if (top <= 0f)
            return 0f;
        float h = Math.Max(0.05f, halfHeight);
        float feet = atSurface ? surfaceY - 1.5f * h : surfaceY - Math.Max(0f, swimDepth) - h;
        float highest = atSurface ? top - 0.5f * h : top - 2.2f * h; // its back stays under (paddling: its head out)
        return Clamp(feet - groundY, 0f, Math.Max(0f, highest));
    }

    /// <summary>
    /// The height a flyer is held at above the ground under it: <paramref name="altitude"/> above whichever is higher, the
    /// ground or a water surface (<paramref name="surfaceY"/> = NaN when there is none).
    /// </summary>
    public static float FlyOffset(float groundY, float surfaceY, float altitude)
    {
        float over = float.IsNaN(surfaceY) ? 0f : Math.Max(0f, surfaceY - groundY);
        return over + Math.Max(0f, altitude);
    }

    /// <summary>
    /// Moves <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="up"/> metres a second up and
    /// <paramref name="down"/> down (climbing and diving at their own rates).
    /// </summary>
    public static float Approach(float current, float target, float up, float down, float dt)
    {
        if (target > current)
            return Math.Min(target, current + Math.Max(0f, up) * dt);
        return Math.Max(target, current - Math.Max(0f, down) * dt);
    }

    private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
}
