using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// The materials a dungeon is built with: the theme's, or flat-colour copies of the render pipeline's default
    /// material (taken from a primitive, so it works in the built-in pipeline, URP and HDRP without shader lookups).
    /// Owns and destroys the materials it creates. Main thread only.
    /// </summary>
    public sealed class DungeonMaterials
    {
        public readonly Material[] BySurface = new Material[(int)DungeonSurface.Count];
        private readonly Material template;
        private readonly Dictionary<long, Material> byColor = new Dictionary<long, Material>();
        private readonly List<Material> created = new List<Material>();

        public DungeonTheme Theme { get; }

        public DungeonMaterials(DungeonTheme theme)
        {
            Theme = theme;
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template = probe.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(probe);

            BySurface[(int)DungeonSurface.BuiltFloor] = Pick(theme != null ? theme.builtFloor : null, theme != null ? theme.builtFloorColor : new Color(0.36f, 0.34f, 0.31f));
            BySurface[(int)DungeonSurface.BuiltWall] = Pick(theme != null ? theme.builtWall : null, theme != null ? theme.builtWallColor : new Color(0.52f, 0.49f, 0.44f));
            BySurface[(int)DungeonSurface.BuiltCeiling] = Pick(theme != null ? theme.builtCeiling : null, theme != null ? theme.builtCeilingColor : new Color(0.28f, 0.27f, 0.25f));
            BySurface[(int)DungeonSurface.CaveFloor] = Pick(theme != null ? theme.caveFloor : null, theme != null ? theme.caveFloorColor : new Color(0.30f, 0.26f, 0.22f));
            BySurface[(int)DungeonSurface.CaveWall] = Pick(theme != null ? theme.caveWall : null, theme != null ? theme.caveWallColor : new Color(0.38f, 0.33f, 0.28f));
            BySurface[(int)DungeonSurface.CaveCeiling] = Pick(theme != null ? theme.caveCeiling : null, theme != null ? theme.caveCeilingColor : new Color(0.24f, 0.21f, 0.18f));
            BySurface[(int)DungeonSurface.Trim] = Pick(theme != null ? theme.trim : null, theme != null ? theme.trimColor : new Color(0.25f, 0.18f, 0.12f));
            BySurface[(int)DungeonSurface.Stairs] = Pick(theme != null ? theme.stairs : null, theme != null ? theme.stairsColor : new Color(0.45f, 0.42f, 0.38f));
            BySurface[(int)DungeonSurface.Liquid] = theme != null && theme.liquid != null ? theme.liquid : Water(theme != null ? theme.liquidColor : new Color(0.1f, 0.24f, 0.28f));
            BySurface[(int)DungeonSurface.Void] = Pick(theme != null ? theme.voidMaterial : null, theme != null ? theme.voidColor : new Color(0.01f, 0.01f, 0.025f));
        }

        private Material Pick(Material fromTheme, Color fallback) => fromTheme != null ? fromTheme : Flat(fallback);

        /// <summary>A glossy water material from the pipeline's default material (opaque, so it works everywhere).</summary>
        private Material Water(Color color)
        {
            Material m = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
            m.name = "Dungeon Water";
            m.color = color;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.92f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.92f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.1f);
            created.Add(m);
            return m;
        }

        /// <summary>A flat-colour material (cached per colour and glow).</summary>
        public Material Flat(Color color, float emission = 0f)
        {
            long key = ((long)Mathf.RoundToInt(color.r * 255f) << 24) | ((long)Mathf.RoundToInt(color.g * 255f) << 16) |
                       ((long)Mathf.RoundToInt(color.b * 255f) << 8) | (long)Mathf.RoundToInt(Mathf.Clamp01(emission / 8f) * 255f);
            if (byColor.TryGetValue(key, out Material m) && m != null)
                return m;
            m = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
            m.name = emission > 0f ? "Dungeon Glow" : "Dungeon Flat";
            m.color = color;
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", color * emission);
            }
            byColor[key] = m;
            created.Add(m);
            return m;
        }

        /// <summary>
        /// A see-through colour (the void's fog): the pipeline's default material switched to alpha blending - URP / HDRP
        /// Lit (Surface Type Transparent) and the built-in Standard shader (Fade) are both set up. Unlit look: no gloss.
        /// </summary>
        public Material Fog(Color color, float alpha)
        {
            Material m = template != null ? new Material(template) : new Material(Shader.Find("Standard"));
            m.name = "Dungeon Fog";
            color.a = alpha;
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            // URP / HDRP Lit.
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_SurfaceType")) m.SetFloat("_SurfaceType", 1f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // Built-in Standard (Fade).
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 2f);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            created.Add(m);
            return m;
        }

        public void Dispose()
        {
            foreach (Material m in created)
                if (m != null)
                    Destroy(m);
            created.Clear();
            byColor.Clear();
        }

        internal static void Destroy(Object o)
        {
            if (o == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(o);
            else
                Object.DestroyImmediate(o);
        }
    }
}
