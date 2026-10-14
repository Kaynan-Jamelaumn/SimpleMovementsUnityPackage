using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HubWorld
{
    /// <summary>
    /// What this machine's player sees of the dimensional cycle: during the last minutes before the jump the sky of the
    /// city and the world drifts toward the rift colours (fog, ambient light, a dimmer sun), and in the world the current
    /// dimensional modifier tints it (a frozen haze, an eclipse). Presentation only - it reads the director's clock.
    /// </summary>
    /// <remarks>
    /// Runs after everything else each frame and blends on top of what the scene or the Weather System set, remembering
    /// what it wrote so its blend never accumulates. When the player leaves for a dungeon (which has its own atmosphere)
    /// everything it changed is put back.
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public class CycleAtmosphere : MonoBehaviour
    {
        private HubWorldDirector director;
        private Scene scene;
        private bool active;

        private bool hadFog;
        private Color baseFogColor, writtenFogColor;
        private float baseFogDensity, writtenFogDensity;
        private Color baseAmbient, writtenAmbient;
        private float baseAmbientIntensity, writtenAmbientIntensity;
        private readonly Dictionary<Light, float> sunBase = new Dictionary<Light, float>();
        private readonly Dictionary<Light, float> sunWritten = new Dictionary<Light, float>();
        private readonly List<Light> suns = new List<Light>();
        private float nextSunSearch;

        /// <summary>0..1: how far the sky has drifted toward the rift (for other effects, e.g. a post-processing volume).</summary>
        public float RiftAmount { get; private set; }

        private void Awake() => director = GetComponent<HubWorldDirector>();

        private void LateUpdate()
        {
            if (director == null || director.Settings == null || GameNet.NetAuthority.IsHeadless)
                return; // a dedicated server shows nothing
            HubWorldSettings settings = director.Settings;
            PlayerRealmState local = director.LocalState();
            RealmKind realm = local != null ? local.Realm : RealmKind.City;
            if (realm != RealmKind.City && realm != RealmKind.World)
            {
                Release();
                return;
            }

            Scene current = SceneManager.GetActiveScene();
            if (active && current != scene)
                Release();
            if (!active)
            {
                scene = current;
                Capture();
            }

            float ramp = 0f;
            if (settings.atmosphereRampSeconds > 0f)
            {
                float t = 1f - Mathf.Clamp01((float)(director.SecondsUntilJump / settings.atmosphereRampSeconds));
                ramp = t * t; // gentle at first, strong near the end
            }
            RiftAmount = ramp;
            DimensionalModifier mod = realm == RealmKind.World ? director.CurrentModifier : null;
            bool tint = mod != null && mod.tintAtmosphere;

            // Fog colour / density.
            if (RenderSettings.fogColor != writtenFogColor) baseFogColor = RenderSettings.fogColor;
            if (!Mathf.Approximately(RenderSettings.fogDensity, writtenFogDensity)) baseFogDensity = RenderSettings.fogDensity;
            Color fogColor = baseFogColor;
            float density = baseFogDensity;
            if (tint)
            {
                fogColor = Color.Lerp(fogColor, mod.fogColor, 0.65f);
                density *= mod.fogDensityMultiplier;
            }
            fogColor = Color.Lerp(fogColor, settings.riftFogColor, ramp);
            density = Mathf.Lerp(density, Mathf.Max(density, settings.riftFogDensity), ramp);
            if ((ramp > 0.001f || tint) && !RenderSettings.fog)
                RenderSettings.fog = true;
            RenderSettings.fogColor = writtenFogColor = fogColor;
            RenderSettings.fogDensity = writtenFogDensity = density;

            // Ambient light.
            if (RenderSettings.ambientLight != writtenAmbient) baseAmbient = RenderSettings.ambientLight;
            if (!Mathf.Approximately(RenderSettings.ambientIntensity, writtenAmbientIntensity)) baseAmbientIntensity = RenderSettings.ambientIntensity;
            Color ambient = tint ? baseAmbient * mod.ambientTint : baseAmbient;
            RenderSettings.ambientLight = writtenAmbient = Color.Lerp(ambient, settings.riftAmbientColor, ramp * 0.8f);
            RenderSettings.ambientIntensity = writtenAmbientIntensity = baseAmbientIntensity * Mathf.Lerp(1f, 0.75f, ramp);

            // Sun.
            if (Time.unscaledTime >= nextSunSearch)
                FindSuns();
            float sunFactor = Mathf.Lerp(1f, settings.riftSunMultiplier, ramp) * (tint ? mod.sunIntensityMultiplier : 1f);
            foreach (Light sun in suns)
            {
                if (sun == null)
                    continue;
                if (!sunWritten.TryGetValue(sun, out float written) || !Mathf.Approximately(sun.intensity, written))
                    sunBase[sun] = sun.intensity;
                float value = sunBase[sun] * sunFactor;
                sun.intensity = value;
                sunWritten[sun] = value;
            }
        }

        private void Capture()
        {
            active = true;
            hadFog = RenderSettings.fog;
            baseFogColor = writtenFogColor = RenderSettings.fogColor;
            baseFogDensity = writtenFogDensity = RenderSettings.fogDensity;
            baseAmbient = writtenAmbient = RenderSettings.ambientLight;
            baseAmbientIntensity = writtenAmbientIntensity = RenderSettings.ambientIntensity;
            sunBase.Clear();
            sunWritten.Clear();
            FindSuns();
        }

        private void FindSuns()
        {
            nextSunSearch = Time.unscaledTime + 2f;
            suns.Clear();
            if (RenderSettings.sun != null)
            {
                suns.Add(RenderSettings.sun);
                return;
            }
            if (!scene.IsValid() || !scene.isLoaded)
                return;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Light l in root.GetComponentsInChildren<Light>())
                    if (l.type == LightType.Directional && l.enabled)
                        suns.Add(l);
        }

        /// <summary>Puts back everything this changed (when leaving for a dungeon, or the scene changes).</summary>
        private void Release()
        {
            if (!active)
                return;
            active = false;
            RiftAmount = 0f;
            if (scene.IsValid() && scene.isLoaded && SceneManager.GetActiveScene() == scene)
            {
                RenderSettings.fog = hadFog;
                RenderSettings.fogColor = baseFogColor;
                RenderSettings.fogDensity = baseFogDensity;
                RenderSettings.ambientLight = baseAmbient;
                RenderSettings.ambientIntensity = baseAmbientIntensity;
            }
            foreach (KeyValuePair<Light, float> pair in sunBase)
                if (pair.Key != null)
                    pair.Key.intensity = pair.Value;
            sunBase.Clear();
            sunWritten.Clear();
            suns.Clear();
        }

        private void OnDisable() => Release();
    }
}
