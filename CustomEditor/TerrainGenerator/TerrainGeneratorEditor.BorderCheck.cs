using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: the Border Check in Natural Biome Placement - settings that make biome borders abrupt, a
// measurement of how steep the real borders are, and one-click fixes (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor
{
    private static readonly LandformType[] ReliefLandforms =
    {
        LandformType.Mountains, LandformType.Hills, LandformType.Dunes, LandformType.Plateau, LandformType.Highlands, LandformType.Glacial,
    };

    /// <summary>Typical height difference between two biomes at their border - the same estimate the border widening plans for.</summary>
    private static float BorderHeightGap(Biome a, Biome b, int octaves)
    {
        return 0.5f * (a.EstimateMaxHeightAmplitude(octaves) + b.EstimateMaxHeightAmplitude(octaves)) + Mathf.Abs(a.baseElevation - b.baseElevation);
    }

    /// <summary>Whether some biome uses a landform with relief (mountains, hills...) whose fade into its neighbors matters.</summary>
    private static bool UsesReliefLandforms(TerrainGenerator generator)
    {
        if (generator.TerrainShapeMode == TerrainShapeMode.ClassicOnly || generator.BiomeDefinitions == null)
            return false;
        foreach (BiomeInstance instance in generator.BiomeDefinitions)
        {
            Biome biome = instance != null ? instance.BiomePrefab : null;
            if (biome != null && System.Array.IndexOf(ReliefLandforms, LandformGenerator.Effective(biome, generator.TerrainShapeMode)) >= 0)
                return true;
        }
        return false;
    }

    /// <summary>A Height Blend Range that suits the layout: wide enough to smooth borders, narrower than a biome region.</summary>
    private static float SuggestedBlendRange(int points)
    {
        return Mathf.Round(Mathf.Min(0.25f, 0.42f / Mathf.Sqrt(Mathf.Max(1, points))) * 100f) / 100f;
    }

    /// <summary>A measurement of the real terrain's biome borders (see <see cref="MeasureBorders"/>).</summary>
    private sealed class BorderMeasurement
    {
        public int BorderSamples, SteepSamples;
        public Biome Tall, Other;
        public int PairSteep;
        public Vector2 Center;
        public float Size, Step;
        public long Milliseconds;
        public string Settings;
    }

    private BorderMeasurement borderMeasurement;
    private const float SteepBorderDegrees = 40f;

    /// <summary>The settings a border measurement depends on, to tell when it is out of date.</summary>
    private static string BorderSettingsKey(TerrainGenerator generator)
    {
        var key = new StringBuilder();
        key.Append(generator.VoronoiScale).Append('/').Append(generator.NumVoronoiPoints).Append('/').Append(generator.VoronoiSeed).Append('/')
           .Append(generator.BiomeBlendRange).Append('/').Append(generator.BiomeBoundaryMaxSlopeDegrees).Append('/').Append(generator.LandformTransitionWidth)
           .Append('/').Append(generator.TerrainShapeMode).Append('/').Append(generator.VoronoiWarpStrength);
        if (generator.BiomeDefinitions != null)
            foreach (BiomeInstance instance in generator.BiomeDefinitions)
                if (instance != null && instance.BiomePrefab != null)
                    key.Append('/').Append(instance.BiomePrefab.amplitude).Append(',').Append(instance.BiomePrefab.landform).Append(',').Append(instance.BiomePrefab.baseElevation);
        return key.ToString();
    }

    /// <summary>
    /// Samples the terrain (before erosion and water) around a point every few units and measures how steep the
    /// ground along biome borders is: the share of border ground steeper than 40 degrees, and the pair of biomes
    /// with the most of it.
    /// </summary>
    private static BorderMeasurement MeasureBorders(TerrainGenerator generator, Vector2 center)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        if (!Application.isPlaying)
            VoronoiBiomeGenerator.ClearCache();
        var sampler = new TerrainHeightSampler(generator, null);
        float region = generator.VoronoiScale / Mathf.Sqrt(Mathf.Max(1, generator.NumVoronoiPoints));
        float size = Mathf.Clamp(region * 8f, 800f, 3000f);
        const int n = 320;
        float step = size / n;
        var heights = new float[n * n];
        var biomes = new Biome[n * n];
        Vector2 min = center - Vector2.one * (size * 0.5f);
        System.Threading.Tasks.Parallel.For(0, n, j =>
        {
            for (int i = 0; i < n; i++)
            {
                float x = min.x + i * step, y = min.y + j * step;
                heights[j * n + i] = sampler.SampleBaseHeight(x, y);
                biomes[j * n + i] = sampler.SampleBiome(x, y);
            }
        });

        var result = new BorderMeasurement { Center = center, Size = size, Step = step, Settings = BorderSettingsKey(generator) };
        var pairs = new Dictionary<KeyValuePair<Biome, Biome>, int>();
        for (int j = 2; j < n - 2; j++)
        {
            for (int i = 2; i < n - 2; i++)
            {
                int k = j * n + i;
                // Border ground: another biome within two samples.
                Biome other = null;
                for (int d = 1; d <= 2 && other == null; d++)
                {
                    if (biomes[k + d] != biomes[k]) other = biomes[k + d];
                    else if (biomes[k - d] != biomes[k]) other = biomes[k - d];
                    else if (biomes[k + d * n] != biomes[k]) other = biomes[k + d * n];
                    else if (biomes[k - d * n] != biomes[k]) other = biomes[k - d * n];
                }
                if (other == null || biomes[k] == null)
                    continue;
                result.BorderSamples++;
                float dx = (heights[k + 1] - heights[k - 1]) / (2f * step), dy = (heights[k + n] - heights[k - n]) / (2f * step);
                if (Mathf.Atan(Mathf.Sqrt(dx * dx + dy * dy)) * Mathf.Rad2Deg <= SteepBorderDegrees)
                    continue;
                result.SteepSamples++;
                Biome a = biomes[k], b = other;
                if (string.CompareOrdinal(a.name, b.name) > 0) { Biome t = a; a = b; b = t; }
                var pair = new KeyValuePair<Biome, Biome>(a, b);
                pairs.TryGetValue(pair, out int count);
                pairs[pair] = count + 1;
            }
        }
        foreach (KeyValuePair<KeyValuePair<Biome, Biome>, int> entry in pairs)
        {
            if (entry.Value <= result.PairSteep)
                continue;
            result.PairSteep = entry.Value;
            bool firstTaller = entry.Key.Key.EstimateMaxHeightAmplitude(generator.Octaves) + entry.Key.Key.baseElevation >=
                               entry.Key.Value.EstimateMaxHeightAmplitude(generator.Octaves) + entry.Key.Value.baseElevation;
            result.Tall = firstTaller ? entry.Key.Key : entry.Key.Value;
            result.Other = firstTaller ? entry.Key.Value : entry.Key.Key;
        }
        result.Milliseconds = watch.ElapsedMilliseconds;
        return result;
    }

    /// <summary>The "Border Check" box of the Natural Biome Placement section.</summary>
    private void DrawBorderCheck(TerrainGenerator generator)
    {
        int points = Mathf.Max(1, generator.NumVoronoiPoints);
        bool shortFade = UsesReliefLandforms(generator) && generator.LandformTransitionWidth < 0.15f;
        float blendUnits = generator.VoronoiScale * generator.BiomeBlendRange;
        bool hardCut = blendUnits < 10f;
        bool hardTextures = !generator.UseBiomeBlendedTexturing;

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField(new GUIContent("Border Check", "How steep the borders between biomes are with the current settings and biomes, and what to change if they turn into walls."), EditorStyles.miniBoldLabel);

        var text = new StringBuilder();
        if (hardCut)
            text.Append($"Height Blend Range {generator.BiomeBlendRange:0.###} is only {blendUnits:0.#} units: borders between biomes of different height are abrupt steps or walls. Around {SuggestedBlendRange(points):0.##} suits this layout.\n");
        if (shortFade)
            text.Append($"Relief Transition Width {generator.LandformTransitionWidth:0.##} (Terrain Shape) is very narrow: mountains and hills keep their full height almost up to their border and then drop. 0.35 is recommended.\n");
        if (hardTextures)
            text.Append("Blend Texturing Too is off: the ground texture changes in a hard line at every border.\n");
        if (text.Length > 0)
            EditorGUILayout.HelpBox(text.ToString().TrimEnd(), hardCut || shortFade ? MessageType.Warning : MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Measure Borders",
                $"Samples the terrain around the Scene view position (about 8 biome regions across) and measures how much of the ground along biome borders is steeper than {SteepBorderDegrees:0}°. Takes a second or two.")))
        {
            SceneView view = SceneView.lastActiveSceneView;
            Vector2 center = view != null ? new Vector2(view.pivot.x, view.pivot.z) : Vector2.zero;
            try
            {
                EditorUtility.DisplayProgressBar("Border Check", "Sampling the terrain", 0.5f);
                borderMeasurement = MeasureBorders(generator, center);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
        if ((hardCut || shortFade || hardTextures) && GUILayout.Button(new GUIContent("Soften Borders",
                $"Height Blend Range {SuggestedBlendRange(points):0.##}, Blend Texturing Too on, Relief Transition Width at least 0.35 and " +
                "Boundary Max Walkable Slope 28 (if it was off). Doesn't move any biome.")))
        {
            biomeBlendRangeProp.floatValue = SuggestedBlendRange(points);
            useBiomeBlendedTexturingProp.boolValue = true;
            SerializedProperty fade = serializedObject.FindProperty("landformTransitionWidth");
            fade.floatValue = Mathf.Max(0.35f, fade.floatValue);
            if (biomeBoundaryMaxSlopeDegreesProp.floatValue <= 0f)
                biomeBoundaryMaxSlopeDegreesProp.floatValue = 28f;
            serializedObject.ApplyModifiedProperties();
            settingsChangedByButton = true;
        }
        EditorGUILayout.EndHorizontal();

        BorderMeasurement m = borderMeasurement;
        if (m != null)
        {
            bool stale = m.Settings != BorderSettingsKey(generator);
            float share = m.BorderSamples > 0 ? 100f * m.SteepSamples / m.BorderSamples : 0f;
            string where = $"around ({m.Center.x:0}, {m.Center.y:0}), {m.Size:0} x {m.Size:0} units, every {m.Step:0.#} units, {m.Milliseconds / 1000f:0.0} s";
            if (share < 5f)
            {
                EditorGUILayout.HelpBox($"Measured: {share:0.#}% of the ground along biome borders is steeper than {SteepBorderDegrees:0}° - borders are walkable ({where})." +
                                        (stale ? "\nSettings changed since - measure again." : ""), MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox($"Measured: {share:0}% of the ground along biome borders is steeper than {SteepBorderDegrees:0}° ({where}), most of it between " +
                                        $"'{m.Tall.name}' and '{m.Other.name}'." + (stale ? "\nSettings changed since - measure again." : "") +
                                        (hardCut || shortFade ? "\nStart with Soften Borders, then measure again." : ""),
                                        share >= 15f ? MessageType.Warning : MessageType.Info);
                if (!hardCut && !shortFade && m.Tall != null)
                    DrawBorderFixes(generator, m.Tall, m.Other);
            }
        }
        EditorGUILayout.LabelField("Settings changed in Play mode only apply to chunks generated afterwards - chunks made before and after a change don't match at their edges. Restart Play mode to see the result.", EditorStyles.wordWrappedMiniLabel);
    }

    /// <summary>Bigger regions or a lower amplitude for the pair with the steepest borders (once the borders are already softened).</summary>
    private void DrawBorderFixes(TerrainGenerator generator, Biome tall, Biome other)
    {
        int points = Mathf.Max(1, generator.NumVoronoiPoints);
        float tangent = generator.BiomeBoundaryMaxSlopeTangent > 0f ? generator.BiomeBoundaryMaxSlopeTangent : Mathf.Tan(28f * Mathf.Deg2Rad);
        float gap = BorderHeightGap(tall, other, generator.Octaves);
        float needed = 1.333f * gap / tangent;
        float region = generator.VoronoiScale / Mathf.Sqrt(points);
        EditorGUILayout.BeginHorizontal();
        if (needed > region)
        {
            float scale = Mathf.Ceil(needed * Mathf.Sqrt(points) / 50f) * 50f;
            if (GUILayout.Button(new GUIContent($"Bigger Regions (Voronoi Scale {scale:0})",
                    $"Makes biome regions about {needed:0} units across, room enough for '{tall.name}' to meet '{other.name}' at a walkable slope. " +
                    "Everything sized from Voronoi Scale grows with it (climate zones, continents, border warp), and the biome layout is reshuffled.")) &&
                EditorUtility.DisplayDialog("Bigger biome regions",
                    $"Set Voronoi Scale to {scale:0}? Biome regions become about {needed:0} units across. The biome layout changes (same seed, new arrangement), " +
                    "and climate zones, continents and border warp grow with it.", "Change", "Cancel"))
            {
                voronoiScaleProp.floatValue = scale;
                serializedObject.ApplyModifiedProperties();
                settingsChangedByButton = true;
            }
        }

        // The tall biome's amplitude that fits the current region size (keeping its persistence).
        float allowedGap = region * tangent / 1.333f;
        float tallRoughness = tall.EstimateMaxHeightAmplitude(generator.Octaves);
        float target = 2f * (allowedGap - Mathf.Abs(tall.baseElevation - other.baseElevation)) - other.EstimateMaxHeightAmplitude(generator.Octaves);
        if (tallRoughness > 0f && target > 1f && target < tallRoughness)
        {
            float amplitude = Mathf.Floor(tall.amplitude * target / tallRoughness);
            if (amplitude >= 1f && GUILayout.Button(new GUIContent($"Lower {tall.name} To {amplitude:0}",
                    $"Sets '{tall.name}'s Amplitude (on its Biome asset) from {tall.amplitude:0.#} to {amplitude:0}, so it meets its neighbors at a walkable slope within the current region size. Its other settings are kept.")))
            {
                Undo.RecordObject(tall, "Lower Biome Amplitude");
                tall.amplitude = amplitude;
                EditorUtility.SetDirty(tall);
                settingsChangedByButton = true;
            }
        }
        EditorGUILayout.EndHorizontal();
    }
}
