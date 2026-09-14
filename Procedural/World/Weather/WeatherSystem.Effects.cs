using UnityEngine;
using UnityEngine.Rendering;

// WeatherSystem, part 2: what the weather looks and sounds like around the viewer - particles, fog, light, wind,
// sounds and the terrain's wet ground and snow. Everything is made at runtime and removed when disabled; the
// scene's own fog and light settings are captured first and restored exactly.
public partial class WeatherSystem
{
    private static readonly int WetnessId = Shader.PropertyToID("_SMWeatherWetness");
    private static readonly int SnowId = Shader.PropertyToID("_SMWeatherSnow");
    private static readonly int SnowCapsId = Shader.PropertyToID("_SMSnowCaps");
    private static readonly int SnowLineId = Shader.PropertyToID("_SMSnowLine");
    private static readonly int AreaId = Shader.PropertyToID("_SMWeatherArea");

    private static readonly Color RainColor = new Color(0.78f, 0.83f, 0.9f, 0.32f);
    private static readonly Color SnowColor = new Color(1f, 1f, 1f, 0.9f);
    private static readonly Color HailColor = new Color(0.92f, 0.95f, 1f, 0.95f);
    private static readonly Color DustColor = new Color(0.78f, 0.63f, 0.43f, 0.3f);
    private static readonly Color MistColor = new Color(0.92f, 0.94f, 0.96f, 0.1f);
    private static readonly Color DebrisColor = new Color(0.45f, 0.4f, 0.28f, 0.9f);
    private static readonly Color FogGrey = new Color(0.62f, 0.66f, 0.7f);
    private static readonly Color FogWhite = new Color(0.86f, 0.88f, 0.92f);
    private static readonly Color FogSand = new Color(0.76f, 0.62f, 0.44f);
    private static readonly Color FogHeat = new Color(0.86f, 0.78f, 0.64f);

    // Effects (made on first use).
    private Transform effectsRoot;
    private ParticleSystem rainParticles, snowParticles, hailParticles, dustParticles, mistParticles, debrisParticles;
    private WindZone wind;
    private AudioSource rainAudio, windAudio, hailAudio, sandAudio, tornadoAudio, thunderAudio;
    private Material runtimeRain, runtimeSoft, runtimeLightning;
    private Texture2D streakTexture, dotTexture;

    // The scene's own settings, restored when the weather is disabled.
    private bool captured;
    private bool baseFog;
    private FogMode baseFogMode;
    private float baseFogDensity, baseFogStart, baseFogEnd;
    private Color baseFogColor;
    private AmbientMode baseAmbientMode;
    private float baseAmbientIntensity;
    private Color baseAmbientLight, baseAmbientSky, baseAmbientEquator, baseAmbientGround;
    private Light capturedSun;
    private float baseSunIntensity;
    private Color baseSunColor;
    private bool weatherFogActive;

    private void CaptureSceneSettings()
    {
        if (captured)
            return;
        baseFog = RenderSettings.fog;
        baseFogMode = RenderSettings.fogMode;
        baseFogDensity = RenderSettings.fogDensity;
        baseFogStart = RenderSettings.fogStartDistance;
        baseFogEnd = RenderSettings.fogEndDistance;
        baseFogColor = RenderSettings.fogColor;
        baseAmbientMode = RenderSettings.ambientMode;
        baseAmbientIntensity = RenderSettings.ambientIntensity;
        baseAmbientLight = RenderSettings.ambientLight;
        baseAmbientSky = RenderSettings.ambientSkyColor;
        baseAmbientEquator = RenderSettings.ambientEquatorColor;
        baseAmbientGround = RenderSettings.ambientGroundColor;
        capturedSun = FindSun();
        if (capturedSun != null)
        {
            baseSunIntensity = capturedSun.intensity;
            baseSunColor = capturedSun.color;
        }
        captured = true;
    }

    private void RestoreSceneSettings()
    {
        if (!captured)
            return;
        RestoreFog();
        RenderSettings.ambientIntensity = baseAmbientIntensity;
        RenderSettings.ambientLight = baseAmbientLight;
        RenderSettings.ambientSkyColor = baseAmbientSky;
        RenderSettings.ambientEquatorColor = baseAmbientEquator;
        RenderSettings.ambientGroundColor = baseAmbientGround;
        if (capturedSun != null)
        {
            capturedSun.intensity = baseSunIntensity;
            capturedSun.color = baseSunColor;
        }
        Shader.SetGlobalFloat(WetnessId, 0f);
        Shader.SetGlobalFloat(SnowId, 0f);
        Shader.SetGlobalFloat(SnowCapsId, 0f);
        Shader.SetGlobalVector(AreaId, Vector4.zero);
        captured = false;
    }

    private void RestoreFog()
    {
        RenderSettings.fog = baseFog;
        RenderSettings.fogMode = baseFogMode;
        RenderSettings.fogDensity = baseFogDensity;
        RenderSettings.fogStartDistance = baseFogStart;
        RenderSettings.fogEndDistance = baseFogEnd;
        RenderSettings.fogColor = baseFogColor;
        weatherFogActive = false;
    }

    private Light FindSun()
    {
        if (sun != null)
            return sun;
        if (RenderSettings.sun != null)
            return RenderSettings.sun;
        Light brightest = null;
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional && (brightest == null || light.intensity > brightest.intensity))
                brightest = light;
        return brightest;
    }

    // ------------------------------------------------------------------ every frame

    private void UpdateEffects(Vector3 viewerPosition, float dt)
    {
        EnsureEffects();
        effectsRoot.position = viewerPosition;

        Vector3 windVector = current.Wind;
        bool show = !SuppressEffects;
        float radius = Mathf.Max(5f, effectRadius);
        int budget = Mathf.Max(500, maxParticles);

        // Rain: streaks falling fast, slanted by the wind, from a box above (and upwind of) the viewer.
        float rain = show ? current.Rain : 0f;
        const float RainSpeed = 16f, RainHeight = 18f;
        UpdateFalling(rainParticles, rain, budget * 0.45f, 1.25f, RainSpeed, RainHeight, windVector * 0.8f, radius);
        // Snow: slow flakes drifting with the wind.
        float snow = show ? current.Snow : 0f;
        UpdateFalling(snowParticles, snow, budget * 0.3f, 7f, 1.6f, 11f, windVector * 0.9f, radius);
        // Hail: fast bright pellets that bounce.
        float hail = show ? current.Hail : 0f;
        UpdateFalling(hailParticles, hail, budget * 0.08f, 1.2f, 20f, 20f, windVector * 0.5f, radius * 0.7f);
        // Sand and dust: big soft clouds streaming with the wind.
        float dust = show ? current.Dust : 0f;
        UpdateDrifting(dustParticles, dust, budget * 0.08f, 3f, windVector, radius, 4f, 12f);
        // Mist: low, slow, soft patches over the ground.
        float mist = show ? Mathf.Max(current.Mist, 0.4f * current.Fog) : 0f;
        UpdateDrifting(mistParticles, mist, budget * 0.04f, 9f, windVector * 0.2f, radius * 1.4f, -1.5f, 2.5f);
        // Leaves and grit blown around in strong wind.
        float gusty = show ? Mathf.Clamp01((current.WindSpeed - 8f) / 12f) * (1f - current.Snow) : 0f;
        UpdateDrifting(debrisParticles, gusty, budget * 0.03f, 3f, windVector * 1.1f, radius, 0f, 6f);

        UpdateFog(dt);
        UpdateLighting(dt);
        UpdateWindZone();
        UpdateAudio();
        UpdateGround(viewerPosition);
        UpdateStorms(viewerPosition, dt);
    }

    /// <summary>Precipitation falling from a box above the viewer, moved upwind so the wind blows it over the viewer.</summary>
    private void UpdateFalling(ParticleSystem system, float amount, float maxAlive, float lifetime, float fallSpeed, float height, Vector3 wind, float radius)
    {
        if (system == null)
            return;
        var emission = system.emission;
        emission.rateOverTime = amount > 0.01f ? amount * maxAlive / lifetime : 0f;
        if (amount <= 0.01f)
            return;

        float fallTime = height / Mathf.Max(0.1f, fallSpeed);
        var shape = system.shape;
        shape.position = new Vector3(-wind.x * fallTime * 0.5f, height, -wind.z * fallTime * 0.5f);
        shape.scale = new Vector3(radius * 2f, 0.5f, radius * 2f);
        var velocity = system.velocityOverLifetime;
        // Two constants on every axis, like the drifting effects (Unity needs the three axes in the same mode).
        velocity.x = new ParticleSystem.MinMaxCurve(wind.x * 0.9f, wind.x * 1.1f);
        velocity.y = new ParticleSystem.MinMaxCurve(-fallSpeed * 1.1f, -fallSpeed * 0.9f);
        velocity.z = new ParticleSystem.MinMaxCurve(wind.z * 0.9f, wind.z * 1.1f);
    }

    /// <summary>Particles drifting with the wind in a flat box around the viewer (dust, mist, debris).</summary>
    private void UpdateDrifting(ParticleSystem system, float amount, float maxAlive, float lifetime, Vector3 wind, float radius, float bottom, float top)
    {
        if (system == null)
            return;
        var emission = system.emission;
        emission.rateOverTime = amount > 0.01f ? amount * maxAlive / lifetime : 0f;
        if (amount <= 0.01f)
            return;

        var shape = system.shape;
        // Spawn upwind, so what drifts past the viewer came from somewhere.
        shape.position = new Vector3(-wind.x * lifetime * 0.4f, 0.5f * (bottom + top), -wind.z * lifetime * 0.4f);
        shape.scale = new Vector3(radius * 2f, Mathf.Max(0.5f, top - bottom), radius * 2f);
        var velocity = system.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(wind.x * 0.8f, wind.x * 1.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.2f, 0.3f);
        velocity.z = new ParticleSystem.MinMaxCurve(wind.z * 0.8f, wind.z * 1.2f);
    }

    private void UpdateFog(float dt)
    {
        if (!fogEffects)
        {
            if (weatherFogActive)
                RestoreFog();
            return;
        }

        float fog = current.Fog, mist = current.Mist, dust = current.Dust, snow = current.Snow, rain = current.Rain, heat = current.Heat;
        float blizzard = snow * Mathf.Clamp01((current.WindSpeed - 8f) / 8f);
        float haze = Mathf.Clamp01(Mathf.Max(fog, Mathf.Max(0.35f * mist, Mathf.Max(0.3f * rain, Mathf.Max(0.45f * snow + 0.5f * blizzard, Mathf.Max(dust, 0.12f * heat))))));
        if (haze < 0.01f)
        {
            if (weatherFogActive)
                RestoreFog();
            return;
        }

        // The haze's colour: grey fog and rain, white snow, sand, a warm haze in the heat.
        float wGrey = fog + 0.35f * mist + 0.3f * rain + 0.01f, wWhite = 0.45f * snow + 0.5f * blizzard, wSand = dust, wHeat = 0.12f * heat;
        float total = wGrey + wWhite + wSand + wHeat;
        Color hazeColor = (FogGrey * wGrey + FogWhite * wWhite + FogSand * wSand + FogHeat * wHeat) * (1f / total);

        // From the scene's own fog (as an equivalent exponential-squared density) toward the weather's.
        float sceneDensity = !baseFog ? 0f
            : baseFogMode == FogMode.Linear ? 2f / Mathf.Max(10f, baseFogEnd)
            : baseFogDensity;
        float density = Mathf.Max(sceneDensity, Mathf.Lerp(sceneDensity, Mathf.Max(0.0005f, maxFogDensity), haze * haze));

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = density;
        RenderSettings.fogColor = Color.Lerp(baseFog ? baseFogColor : hazeColor, hazeColor, Mathf.Clamp01(haze * 1.5f));
        weatherFogActive = true;
    }

    private void UpdateLighting(float dt)
    {
        if (!lightingEffects)
            return;

        float flash = LightningFlash(dt);
        float dim = Mathf.Clamp01(0.55f * current.Clouds + 0.25f * current.Dust + 0.2f * current.Fog + 0.15f * current.Snow);
        Light light = capturedSun;
        if (light != null)
        {
            Color tint = Color.Lerp(Color.white, new Color(0.82f, 0.86f, 0.95f), current.Clouds);
            tint = Color.Lerp(tint, new Color(1f, 0.78f, 0.55f), current.Dust * 0.8f);
            tint = Color.Lerp(tint, new Color(1f, 0.92f, 0.8f), current.Heat * 0.6f);
            light.color = baseSunColor * tint;
            light.intensity = baseSunIntensity * (1f - 0.7f * dim) * (1f + 0.15f * current.Heat) + baseSunIntensity * 2.5f * flash;
        }

        float ambient = (1f - 0.4f * dim) + 1.5f * flash;
        if (baseAmbientMode == AmbientMode.Skybox)
        {
            RenderSettings.ambientIntensity = baseAmbientIntensity * ambient;
        }
        else
        {
            RenderSettings.ambientLight = baseAmbientLight * ambient;
            RenderSettings.ambientSkyColor = baseAmbientSky * ambient;
            RenderSettings.ambientEquatorColor = baseAmbientEquator * ambient;
            RenderSettings.ambientGroundColor = baseAmbientGround * ambient;
        }
    }

    private void UpdateWindZone()
    {
        if (wind == null)
            return;
        wind.gameObject.SetActive(windZone);
        if (!windZone)
            return;
        Vector3 direction = new Vector3(current.WindDirection.x, 0f, current.WindDirection.y);
        if (direction.sqrMagnitude > 1e-6f)
            wind.transform.rotation = Quaternion.LookRotation(direction);
        wind.windMain = Mathf.Clamp(current.WindSpeed / 8f, 0f, 3f);
        wind.windTurbulence = Mathf.Clamp(0.1f + current.WindSpeed / 25f + current.Thunder * 0.5f, 0f, 2f);
        wind.windPulseMagnitude = 0.5f + 0.5f * Mathf.Clamp01(current.WindSpeed / 15f);
        wind.windPulseFrequency = 0.05f + current.WindSpeed * 0.01f;
    }

    private void UpdateAudio()
    {
        SetLoop(rainAudio, current.Rain * (1f - 0.3f * current.Hail));
        SetLoop(windAudio, Mathf.Clamp01((current.WindSpeed - 2f) / 18f));
        SetLoop(hailAudio, current.Hail);
        SetLoop(sandAudio, current.Dust);
        SetLoop(tornadoAudio, TornadoLoudness());
    }

    private void SetLoop(AudioSource source, float amount)
    {
        if (source == null)
            return;
        float target = Mathf.Clamp01(amount) * volume;
        source.volume = Mathf.MoveTowards(source.volume, target, Time.deltaTime * 0.5f);
        if (source.volume > 0.001f && !source.isPlaying)
            source.Play();
        else if (source.volume <= 0.001f && source.isPlaying)
            source.Stop();
    }

    /// <summary>The terrain shader's wet ground, settled snow and snow caps (global shader values).</summary>
    private void UpdateGround(Vector3 viewerPosition)
    {
        float radius = Mathf.Max(0f, groundEffectRadius);
        Shader.SetGlobalVector(AreaId, radius > 0f
            ? new Vector4(viewerPosition.x, viewerPosition.z, radius, 1f / Mathf.Max(1f, 0.5f * radius))
            : Vector4.zero);
        Shader.SetGlobalFloat(WetnessId, groundEffects ? groundWetness : 0f);
        Shader.SetGlobalFloat(SnowId, groundEffects ? snowCover : 0f);
        TerrainGenerator generator = terrainGenerator;
        bool caps = snowCaps && generator != null && generator.SnowLineHeight > 0f;
        Shader.SetGlobalFloat(SnowCapsId, caps ? 1f : 0f);
        if (caps)
            Shader.SetGlobalFloat(SnowLineId, generator.SeaLevel + generator.SnowLineHeight);
    }

    // ------------------------------------------------------------------ making the effects

    private void EnsureEffects()
    {
        if (effectsRoot != null)
            return;

        var root = new GameObject("Weather Effects") { hideFlags = HideFlags.DontSave };
        effectsRoot = root.transform;

        streakTexture = MakeStreakTexture();
        dotTexture = MakeDotTexture();
        runtimeRain = rainMaterial != null ? null : MakeParticleMaterial(streakTexture, false);
        runtimeSoft = dustMaterial != null && snowMaterial != null ? null : MakeParticleMaterial(dotTexture, false);
        Material rainMat = rainMaterial != null ? rainMaterial : runtimeRain;
        Material snowMat = snowMaterial != null ? snowMaterial : runtimeSoft;
        Material softMat = dustMaterial != null ? dustMaterial : runtimeSoft;
        int budget = Mathf.Max(500, maxParticles);

        rainParticles = MakeParticles("Rain", rainMat, Mathf.CeilToInt(budget * 0.45f), 1.25f, new Vector2(0.03f, 0.05f), RainColor, ParticleSystemRenderMode.Stretch);
        var rainRenderer = rainParticles.GetComponent<ParticleSystemRenderer>();
        rainRenderer.velocityScale = 0.06f;
        rainRenderer.lengthScale = 1f;

        snowParticles = MakeParticles("Snow", snowMat, Mathf.CeilToInt(budget * 0.3f), 7f, new Vector2(0.06f, 0.16f), SnowColor, ParticleSystemRenderMode.Billboard);
        var snowNoise = snowParticles.noise;
        snowNoise.enabled = true;
        snowNoise.strength = new ParticleSystem.MinMaxCurve(0.6f);
        snowNoise.frequency = 0.35f;
        snowNoise.scrollSpeed = 0.2f;
        snowNoise.quality = ParticleSystemNoiseQuality.Low;

        hailParticles = MakeParticles("Hail", snowMat, Mathf.CeilToInt(budget * 0.08f), 1.2f, new Vector2(0.04f, 0.08f), HailColor, ParticleSystemRenderMode.Billboard);
        var hailCollision = hailParticles.collision;
        hailCollision.enabled = true;
        hailCollision.type = ParticleSystemCollisionType.World;
        hailCollision.mode = ParticleSystemCollisionMode.Collision3D;
        hailCollision.quality = ParticleSystemCollisionQuality.Low;
        hailCollision.bounce = new ParticleSystem.MinMaxCurve(0.35f);
        hailCollision.dampen = new ParticleSystem.MinMaxCurve(0.3f);
        hailCollision.lifetimeLoss = new ParticleSystem.MinMaxCurve(0.35f);

        dustParticles = MakeParticles("Dust", softMat, Mathf.CeilToInt(budget * 0.08f), 3f, new Vector2(4f, 10f), DustColor, ParticleSystemRenderMode.Billboard);
        FadeInOut(dustParticles);
        mistParticles = MakeParticles("Mist", softMat, Mathf.CeilToInt(budget * 0.04f), 9f, new Vector2(6f, 14f), MistColor, ParticleSystemRenderMode.Billboard);
        FadeInOut(mistParticles);
        debrisParticles = MakeParticles("Wind Debris", softMat, Mathf.CeilToInt(budget * 0.03f), 3f, new Vector2(0.06f, 0.14f), DebrisColor, ParticleSystemRenderMode.Billboard);
        var debrisNoise = debrisParticles.noise;
        debrisNoise.enabled = true;
        debrisNoise.strength = new ParticleSystem.MinMaxCurve(1.5f);
        debrisNoise.frequency = 0.8f;
        debrisNoise.quality = ParticleSystemNoiseQuality.Low;
        foreach (ParticleSystem big in new[] { dustParticles, mistParticles })
            big.GetComponent<ParticleSystemRenderer>().maxParticleSize = 3f;

        var windObject = new GameObject("Weather Wind") { hideFlags = HideFlags.DontSave };
        windObject.transform.SetParent(effectsRoot, false);
        wind = windObject.AddComponent<WindZone>();
        wind.mode = WindZoneMode.Directional;

        rainAudio = MakeLoop("Rain Sound", rainLoop);
        windAudio = MakeLoop("Wind Sound", windLoop);
        hailAudio = MakeLoop("Hail Sound", hailLoop);
        sandAudio = MakeLoop("Sandstorm Sound", sandstormLoop);
        tornadoAudio = MakeLoop("Tornado Sound", tornadoLoop);
        if (thunderClips != null && thunderClips.Length > 0)
        {
            thunderAudio = MakeLoop("Thunder Sound", null);
            thunderAudio.loop = false;
        }
    }

    private void DestroyEffects()
    {
        DestroyStorms();
        if (effectsRoot != null)
            Destroy(effectsRoot.gameObject);
        effectsRoot = null;
        rainParticles = snowParticles = hailParticles = dustParticles = mistParticles = debrisParticles = null;
        wind = null;
        rainAudio = windAudio = hailAudio = sandAudio = tornadoAudio = thunderAudio = null;
        foreach (Object asset in new Object[] { runtimeRain, runtimeSoft, runtimeLightning, streakTexture, dotTexture })
            if (asset != null)
                Destroy(asset);
        runtimeRain = runtimeSoft = runtimeLightning = null;
        streakTexture = dotTexture = null;
    }

    private ParticleSystem MakeParticles(string name, Material material, int max, float lifetime, Vector2 size, Color color, ParticleSystemRenderMode mode)
    {
        // Built inactive: a particle system's duration can only be set while it isn't playing.
        var holder = new GameObject(name) { hideFlags = HideFlags.DontSave };
        holder.SetActive(false);
        holder.transform.SetParent(effectsRoot, false);
        var system = holder.AddComponent<ParticleSystem>();

        var main = system.main;
        main.duration = 5f;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.85f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(color);
        main.maxParticles = Mathf.Max(10, max);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = system.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(40f, 1f, 40f);

        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var renderer = holder.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = mode;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        holder.SetActive(true);
        return system;
    }

    /// <summary>Fades particles in and out over their life, so big soft ones never pop.</summary>
    private static void FadeInOut(ParticleSystem system)
    {
        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private AudioSource MakeLoop(string name, AudioClip clip)
    {
        if (clip == null && name != "Thunder Sound")
            return null;
        var holder = new GameObject(name) { hideFlags = HideFlags.DontSave };
        holder.transform.SetParent(effectsRoot, false);
        var source = holder.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        return source;
    }

    /// <summary>
    /// A transparent particle material for the current render pipeline (URP's particle shader, else the Built-in
    /// pipeline's), or null when none can be found (then assign materials in the inspector).
    /// </summary>
    internal static Material MakeParticleMaterial(Texture2D texture, bool additive)
    {
        Shader shader = null;
        if (GraphicsSettings.currentRenderPipeline != null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        var material = new Material(shader) { name = "Weather Particles (runtime)", hideFlags = HideFlags.DontSave };
        if (texture != null)
        {
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        }
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));

        // Transparent, not writing depth; alpha blended (or additive for glows).
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", additive ? 2f : 0f);
        material.SetFloat("_Mode", additive ? 4f : 2f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword(additive ? "_ALPHAPREMULTIPLY_ON" : "_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    /// <summary>A soft vertical streak (rain drops, stretched along their motion).</summary>
    private static Texture2D MakeStreakTexture()
    {
        const int W = 8, H = 32;
        var texture = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "Weather Streak", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var pixels = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;
            float along = Mathf.Sin(v * Mathf.PI);
            for (int x = 0; x < W; x++)
            {
                float u = ((x + 0.5f) / W - 0.5f) * 2f;
                float a = Mathf.Clamp01(1f - u * u) * along;
                pixels[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    /// <summary>A soft round dot (snow, hail, dust, mist, debris).</summary>
    private static Texture2D MakeDotTexture()
    {
        const int N = 32;
        var texture = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "Weather Dot", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var pixels = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a = a * a * (3f - 2f * a);
                pixels[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        return texture;
    }
}
