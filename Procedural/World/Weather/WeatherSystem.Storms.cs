using System.Collections;
using UnityEngine;

// WeatherSystem, part 3: lightning and tornadoes.
public partial class WeatherSystem
{
    // Lightning.
    private float nextStrike = -1f;
    private float flashTime = -1f;
    private float flashStrength;
    private LineRenderer bolt;
    private float boltUntil;

    // Tornado.
    private Transform tornado;
    private ParticleSystem tornadoFunnel, tornadoDebris;
    private Vector3 tornadoVelocity;
    private float tornadoLife;
    private float tornadoForceTimer;
    private static readonly Collider[] TornadoHits = new Collider[64];

    /// <summary>Where the tornado is (its base), when there is one.</summary>
    public Vector3? TornadoPosition => tornado != null ? tornado.position : (Vector3?)null;

    /// <summary>Raised on every lightning strike: where it struck (world position) and its distance to the viewer.</summary>
    public event System.Action<Vector3, float> LightningStruck;

    private void UpdateStorms(Vector3 viewerPosition, float dt)
    {
        UpdateLightning(viewerPosition);
        UpdateTornado(viewerPosition, dt);
    }

    // ------------------------------------------------------------------ lightning

    private void UpdateLightning(Vector3 viewerPosition)
    {
        if (bolt != null && Time.time >= boltUntil)
            bolt.enabled = false;

        float storm = current.Thunder;
        if (storm < 0.15f)
        {
            nextStrike = -1f;
            return;
        }
        float perMinute = Mathf.Max(0.1f, maxLightningPerMinute * storm);
        if (nextStrike < 0f)
            nextStrike = Time.time + Random.Range(0.3f, 1.5f) * 60f / perMinute;
        if (Time.time < nextStrike)
            return;
        // Strikes at random intervals (about Poisson), averaging the storm's rate.
        nextStrike = Time.time - Mathf.Log(Mathf.Max(1e-4f, Random.value)) * 60f / perMinute;

        float distance = Random.Range(60f, 700f);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 ground = viewerPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
        ground.y = GroundHeight(ground, viewerPosition.y);

        flashStrength = Mathf.Clamp01(1.4f / (1f + distance / 250f));
        flashTime = Time.time;
        if (lightningBolts && !SuppressEffects)
            ShowBolt(ground);
        if (thunderAudio != null && thunderClips != null && thunderClips.Length > 0)
        {
            AudioClip clip = thunderClips[Random.Range(0, thunderClips.Length)];
            if (clip != null)
                StartCoroutine(Thunder(clip, Mathf.Clamp(distance / 343f, 0.1f, 4f), volume * Mathf.Clamp01(1.2f / (1f + distance / 350f))));
        }
        LightningStruck?.Invoke(ground, distance);
    }

    /// <summary>The current lightning flash (0-1): a bright flicker fading within half a second.</summary>
    private float LightningFlash(float dt)
    {
        if (flashTime < 0f)
            return 0f;
        float age = Time.time - flashTime;
        if (age > 0.5f)
        {
            flashTime = -1f;
            return 0f;
        }
        // Two quick pulses, then fading.
        float pulse = age < 0.06f ? 1f : age < 0.12f ? 0.25f : age < 0.2f ? 0.8f : Mathf.Lerp(0.5f, 0f, (age - 0.2f) / 0.3f);
        return flashStrength * pulse;
    }

    private IEnumerator Thunder(AudioClip clip, float delay, float loudness)
    {
        yield return new WaitForSeconds(delay);
        if (thunderAudio != null)
            thunderAudio.PlayOneShot(clip, loudness);
    }

    private void ShowBolt(Vector3 ground)
    {
        if (bolt == null)
        {
            EnsureEffects();
            var holder = new GameObject("Lightning Bolt") { hideFlags = HideFlags.DontSave };
            holder.transform.SetParent(effectsRoot, false);
            bolt = holder.AddComponent<LineRenderer>();
            bolt.useWorldSpace = true;
            bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bolt.receiveShadows = false;
            bolt.numCapVertices = 2;
            if (lightningMaterial == null && runtimeLightning == null)
                runtimeLightning = MakeParticleMaterial(dotTexture, true);
            bolt.sharedMaterial = lightningMaterial != null ? lightningMaterial : runtimeLightning;
            bolt.startColor = new Color(0.9f, 0.93f, 1f, 1f);
            bolt.endColor = new Color(0.8f, 0.85f, 1f, 0.9f);
        }

        // A jagged path from the cloud base down to the ground, with a short side branch.
        const int Segments = 14;
        float top = ground.y + Random.Range(160f, 260f);
        var points = new Vector3[Segments + 1];
        Vector3 wander = Vector3.zero;
        for (int i = 0; i <= Segments; i++)
        {
            float t = i / (float)Segments;
            if (i > 0 && i < Segments)
                wander += new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 9f;
            points[i] = new Vector3(ground.x, Mathf.Lerp(top, ground.y, t), ground.z) + wander * (1f - t * 0.6f);
        }
        points[Segments] = ground;
        bolt.positionCount = points.Length;
        bolt.SetPositions(points);
        bolt.widthMultiplier = Random.Range(1.2f, 2.5f);
        bolt.enabled = true;
        boltUntil = Time.time + Random.Range(0.12f, 0.25f);
    }

    /// <summary>Height of the ground below a position (the terrain's colliders), else the fallback.</summary>
    private static float GroundHeight(Vector3 position, float fallback)
    {
        if (Physics.Raycast(new Vector3(position.x, fallback + 1000f, position.z), Vector3.down, out RaycastHit hit, 3000f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return fallback;
    }

    // ------------------------------------------------------------------ tornado

    private void UpdateTornado(Vector3 viewerPosition, float dt)
    {
        float strength = current.Tornado;
        if (tornado == null)
        {
            if (strength > 0.35f && !SuppressEffects)
                SpawnTornado(viewerPosition);
            return;
        }

        // Moves with the storm's wind, wandering; weakens and leaves when the storm does.
        tornadoLife += dt;
        Vector3 windDirection = new Vector3(current.WindDirection.x, 0f, current.WindDirection.y);
        Vector3 wander = new Vector3(Mathf.Sin(tornadoLife * 0.23f), 0f, Mathf.Cos(tornadoLife * 0.17f)) * 4f;
        tornadoVelocity = Vector3.Lerp(tornadoVelocity, windDirection * (6f + 0.3f * current.WindSpeed) + wander, dt * 0.2f);
        Vector3 position = tornado.position + tornadoVelocity * dt;
        position.y = Mathf.Lerp(tornado.position.y, GroundHeight(position, tornado.position.y), dt * 2f);
        tornado.position = position;

        bool fading = strength < 0.2f || (position - viewerPosition).sqrMagnitude > 900f * 900f;
        SetEmission(tornadoFunnel, fading ? 0f : 350f * Mathf.Clamp01(strength * 1.5f));
        SetEmission(tornadoDebris, fading ? 0f : 120f * Mathf.Clamp01(strength * 1.5f));
        if (fading && tornadoFunnel.particleCount == 0 && tornadoDebris.particleCount == 0)
        {
            DestroyTornado();
            return;
        }

        tornadoForceTimer -= dt;
        if (tornadoForce > 0f && tornadoForceTimer <= 0f && !fading)
        {
            tornadoForceTimer = 0.1f;
            PushRigidbodies(position, strength);
        }
    }

    private void SpawnTornado(Vector3 viewerPosition)
    {
        EnsureEffects();
        // Somewhere ahead of the storm (upwind), off to one side, so it may pass by.
        Vector3 windDirection = new Vector3(current.WindDirection.x, 0f, current.WindDirection.y).normalized;
        if (windDirection.sqrMagnitude < 1e-4f)
            windDirection = Vector3.right;
        Vector3 side = new Vector3(-windDirection.z, 0f, windDirection.x);
        Vector3 position = viewerPosition - windDirection * Random.Range(250f, 450f) + side * Random.Range(-180f, 180f);
        position.y = GroundHeight(position, viewerPosition.y);

        var root = new GameObject("Tornado") { hideFlags = HideFlags.DontSave };
        tornado = root.transform;
        tornado.position = position;
        tornadoVelocity = windDirection * 6f;
        tornadoLife = 0f;
        Material soft = dustMaterial != null ? dustMaterial : runtimeSoft;

        // The funnel: particles rising in a spiral, widening as they climb.
        tornadoFunnel = MakeVortex("Funnel", soft, 2200, new Vector2(3.2f, 4.2f), 1.5f, new Vector2(2f, 3.5f), new Color(0.42f, 0.4f, 0.38f, 0.42f), 14f, 4f, 2.2f, 3f);
        // Debris and dust whirling around the base.
        tornadoDebris = MakeVortex("Debris", soft, 700, new Vector2(1.2f, 2.2f), 7f, new Vector2(0.3f, 1.4f), new Color(0.35f, 0.3f, 0.22f, 0.85f), 4f, 3f, 1f, 1f);
    }

    private ParticleSystem MakeVortex(string name, Material material, int max, Vector2 lifetime, float radius, Vector2 size, Color color,
        float rise, float spin, float spread, float growth)
    {
        var holder = new GameObject(name) { hideFlags = HideFlags.DontSave };
        holder.SetActive(false);
        holder.transform.SetParent(tornado, false);
        // Local Z points up, so the circle lies on the ground and orbiting around Z spins around the vertical.
        holder.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var system = holder.AddComponent<ParticleSystem>();

        var main = system.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1f);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(color);
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = system.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;

        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        // Unity needs the three axes (and the three orbital axes) in the same curve mode: two constants each.
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(rise * 0.8f, rise * 1.2f);
        velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(spin * 0.8f, spin * 1.2f);
        velocity.radial = new ParticleSystem.MinMaxCurve(spread * 0.5f, spread);

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, growth));

        FadeInOut(system);

        var renderer = holder.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.maxParticleSize = 3f;

        holder.SetActive(true);
        return system;
    }

    private static void SetEmission(ParticleSystem system, float rate)
    {
        if (system == null)
            return;
        var emission = system.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);
    }

    /// <summary>Swirls, pulls in and lifts rigidbodies near the tornado.</summary>
    private void PushRigidbodies(Vector3 center, float strength)
    {
        const float Radius = 35f;
        int count = Physics.OverlapSphereNonAlloc(center + Vector3.up * 10f, Radius, TornadoHits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Rigidbody body = TornadoHits[i].attachedRigidbody;
            if (body == null || body.isKinematic)
                continue;
            Vector3 offset = body.worldCenterOfMass - center;
            Vector3 flat = new Vector3(offset.x, 0f, offset.z);
            float distance = flat.magnitude;
            if (distance > Radius)
                continue;
            float falloff = 1f - distance / Radius;
            Vector3 inward = distance > 0.01f ? -flat / distance : Vector3.zero;
            Vector3 around = Vector3.Cross(Vector3.up, inward);
            Vector3 force = (around * 1.2f + inward * 0.6f + Vector3.up * 0.9f) * (tornadoForce * strength * falloff);
            body.AddForce(force, ForceMode.Acceleration);
        }
    }

    private float TornadoLoudness()
    {
        if (tornado == null)
            return 0f;
        Transform follow = Viewer;
        float distance = follow != null ? Vector3.Distance(follow.position, tornado.position) : 500f;
        return current.Tornado * Mathf.Clamp01(1.3f - distance / 600f);
    }

    private void DestroyTornado()
    {
        if (tornado != null)
            Destroy(tornado.gameObject);
        tornado = null;
        tornadoFunnel = tornadoDebris = null;
    }

    private void DestroyStorms()
    {
        DestroyTornado();
        if (bolt != null)
            Destroy(bolt.gameObject);
        bolt = null;
    }
}
