using System.Collections.Generic;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>
    /// Flat-colour materials for the cave mouths in the open world (a dungeon's build has its own, destroyed with it):
    /// copies of the render pipeline's default material (taken from a primitive, as the dungeon does, so they work in the
    /// built-in pipeline, URP and HDRP without shader lookups), one per colour and glow, kept for the session. Main thread.
    /// </summary>
    public static class CaveMaterials
    {
        private static Material template;
        private static readonly Dictionary<long, Material> cache = new Dictionary<long, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            template = null;
            cache.Clear();
        }

        /// <summary>A flat <paramref name="color"/> material, glowing with <paramref name="emission"/> (0 = not at all).</summary>
        public static Material Flat(Color color, float emission = 0f)
        {
            long key = ((long)Mathf.RoundToInt(color.r * 255f) << 24) | ((long)Mathf.RoundToInt(color.g * 255f) << 16) |
                       ((long)Mathf.RoundToInt(color.b * 255f) << 8) | (long)Mathf.RoundToInt(Mathf.Clamp01(emission / 8f) * 255f);
            if (cache.TryGetValue(key, out Material m) && m != null)
                return m;
            if (template == null)
            {
                GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                template = probe.GetComponent<Renderer>().sharedMaterial;
                Object.DestroyImmediate(probe);
            }
            // Not DontSave: a scene saved with something using it keeps it, and once the cache lets go of it (a new
            // session) an unused one is unloaded like any other.
            m = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
            m.name = emission > 0f ? "Cave Glow" : "Cave Rock";
            m.color = color;
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness"))
                m.SetFloat("_Smoothness", 0.08f);
            if (m.HasProperty("_Glossiness"))
                m.SetFloat("_Glossiness", 0.08f);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", color * emission);
            }
            cache[key] = m;
            return m;
        }

        /// <summary>Darker shades of a rock colour for a tunnel going into the dark (1 = the rock itself).</summary>
        public static Color Shade(Color rock, float light) => new Color(rock.r * light, rock.g * light, rock.b * light, 1f);
    }
}
