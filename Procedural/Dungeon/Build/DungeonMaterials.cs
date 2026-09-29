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
        }

        private Material Pick(Material fromTheme, Color fallback) => fromTheme != null ? fromTheme : Flat(fallback);

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
