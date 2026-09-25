using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: warnings for settings that are valid but known to cause visible problems (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor : Editor
{
    /// <summary>
    /// Flags parameter combinations that are individually valid (won't throw/crash) but are documented -
    /// in this class or its collaborators - to produce visible artifacts, so misconfigurations surface
    /// in the Inspector instead of only after noticing something looks wrong in-game.
    /// </summary>
    private static void DrawValidationWarnings(TerrainGenerator generator)
    {
        if (generator.BiomeDefinitions == null || generator.BiomeDefinitions.Length == 0)
        {
            EditorGUILayout.HelpBox("No biomes assigned - see the Biomes section below.", MessageType.Error);
        }

        if (generator.EnableErosion && generator.HydraulicDropletDensity > 0f && generator.ErosionPadding <= generator.DropletLifetime)
        {
            EditorGUILayout.HelpBox(
                $"Erosion Padding ({generator.ErosionPadding}) should comfortably exceed Droplet Lifetime " +
                $"({generator.DropletLifetime}) so a droplet's full travel stays inside the padded region. " +
                "Otherwise neighboring chunks can erode inconsistently near their shared edge, producing a visible seam.",
                MessageType.Warning);
        }

        if (generator.UseNaturalClimatePlacement && generator.ClimateNoiseScale < generator.VoronoiScale * 3f)
        {
            EditorGUILayout.HelpBox(
                "Climate Scale Multiplier is low relative to Voronoi Scale: climate will vary almost cell-to-cell " +
                "instead of spanning several biome cells, producing a 'salt and pepper' patchwork instead of " +
                "natural clustered territories. Keep the multiplier around 5-10.",
                MessageType.Warning);
        }

        float regionWidth = generator.VoronoiScale / Mathf.Sqrt(Mathf.Max(1, generator.NumVoronoiPoints));
        float blendBand = generator.VoronoiScale * generator.BiomeBlendRange;
        if (blendBand > regionWidth * 0.6f)
        {
            EditorGUILayout.HelpBox(
                $"Height Blend Range is wide for this layout: biome transitions are ~{blendBand:0} world units wide, " +
                $"but biome regions are only ~{regionWidth:0} across. Almost every point is then a mix of several " +
                "biomes, so each biome's own terrain is averaged away and they all look alike (Classic biomes are " +
                $"affected most). Keep it below about {0.5f / Mathf.Sqrt(Mathf.Max(1, generator.NumVoronoiPoints)):0.00}, " +
                "or use Boundary Max Walkable Slope to widen only the borders that need it.",
                MessageType.Warning);
        }

        if (generator.VoronoiWarpStrength > 0f && generator.VoronoiWarpScale < generator.VoronoiScale)
        {
            EditorGUILayout.HelpBox(
                "Border Warp Scale is smaller than Voronoi Scale: the warp can oscillate multiple times across " +
                "a single cell edge, tearing borders into jagged, self-crossing shapes. Keep the scale multiplier at or above 1.",
                MessageType.Warning);
        }
    }
}
