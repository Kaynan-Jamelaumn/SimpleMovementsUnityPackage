using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Second half of <see cref="TerrainGeneratorEditor"/>: the detailed per-field tooltips shown when
/// hovering a field label, and the "Copy Settings" export that turns every configurable value (plus the
/// assigned biomes and their contents) into plain text on the clipboard, e.g. to paste into a note, a
/// bug report or a chat when comparing or sharing a terrain setup.
/// </summary>
public partial class TerrainGeneratorEditor
{
    // ------------------------------------------------------------------ tooltips

    /// <summary>
    /// Detailed hover text per serialized field name: what the value does and what changing it implies.
    /// Fields not listed here fall back to their own [Tooltip] attribute in TerrainGenerator.cs.
    /// </summary>
    private static readonly Dictionary<string, string> FieldTips = new Dictionary<string, string>
    {
        // --- Terrain configuration
        { "terrainSize",
            "Heightmap cells per chunk side (Small 61, Medium 121, Large 181, Extra Large 241). Cells are one world unit apart, so a chunk spans Size - 1 world units (240 for Extra Large).\n\n" +
            "Implications: larger chunks mean fewer chunks and fewer seams, but each one takes longer to generate (cost grows with the square of the size). " +
            "Biome noise is sampled per chunk width, so changing this also changes how big every biome's hills are (see Biome Frequency)." },

        // --- Noise
        { "octaves",
            "How many noise layers are stacked for every biome's height. Layer 1 gives the big shapes; each extra layer adds finer detail on top.\n\n" +
            "Implications: more octaves = more small-scale roughness and slower generation (every cell samples each octave for each blended biome). " +
            "Layers stop early once they become too faint to matter (amplitude below 0.001). Typical: 4-6.\n\nOnly affects biomes using the Classic landform." },
        { "lacunarity",
            "How much faster each noise layer changes than the previous one (frequency multiplier per octave). 2 = each layer's bumps are half the size of the one before.\n\n" +
            "Implications: higher values make the detail layers much finer and noisier; values near 1 make all layers similar in size, which looks blurry. Typical: 1.8-2.2.\n\nOnly affects biomes using the Classic landform." },

        // --- Height & texture
        { "terrainTextureBasedOnVoronoiPoints",
            "ON: each cell is textured by the biome it belongs to (recommended, matches biome borders).\n" +
            "OFF: texture is picked by height, using each biome's Min/Max Height band and the tracked Min/Max Height below." },
        { "minHeight",
            "Lowest terrain height seen so far. Updated automatically while generating and only used when textures are height-based (Texture Based On Voronoi Points OFF). It only ever grows outward and isn't reset between Play sessions." },
        { "maxHeight",
            "Highest terrain height seen so far. Updated automatically while generating and only used when textures are height-based (Texture Based On Voronoi Points OFF)." },

        // --- Texture variations
        { "enableTextureVariations",
            "Master switch for every texture anti-tiling option below. OFF = textures repeat identically across the whole world, which shows as an obvious grid pattern. Strongly recommended ON." },
        { "enableUVRotation",
            "Rotates each chunk's texture coordinates by a seed-based angle so neighboring chunks don't show the same tile orientation. No runtime cost." },
        { "enableUVNoise",
            "Adds a smooth noise offset to texture coordinates so the texture grid wobbles slightly instead of forming straight repeating rows." },
        { "uvNoiseStrength",
            "How far (in texture tiles) the UV noise can shift the texture. 0 = none. Too high looks smeared or swimming; 0.2-0.4 usually hides tiling well." },
        { "uvNoiseScale",
            "Size of the UV noise pattern. Lower = large, slow wobbles; higher = small, busy distortions." },
        { "enableTextureScaleVariation",
            "Gives each chunk a slightly different texture scale so repeated tiles don't line up between chunks." },
        { "textureScaleVariationRange",
            "How much texture scale may vary per chunk (0.3 = up to +/-30%). Large values make neighboring chunks' texture sizes visibly different." },
        { "enableShaderEnhancements",
            "Passes extra variation parameters to the terrain shader (rotation, scale, blend sharpness). Only has an effect if the terrain material's shader supports these properties." },
        { "shaderUVRotationStrength", "Shader-side texture rotation strength (0 = none, 1 = full). Requires a shader that reads it." },
        { "shaderUVScaleVariation", "Shader-side texture scale variation. Requires a shader that reads it." },
        { "shaderTextureBlendSharpness", "How hard the edge between two blended textures is in the shader. Low = soft, wide blends; high = crisp transitions." },

        // --- Voronoi
        { "NumVoronoiPoints",
            "How many biome seed points are scattered in each Voronoi grid cell (a square of Voronoi Scale x Voronoi Scale world units). Every point becomes one biome region.\n\n" +
            "Implications: typical region width is about Voronoi Scale / sqrt(points). More points = smaller, more numerous biome patches (before clustering merges neighbors of the same type). " +
            "Also caps how wide biome transitions can get. Changing this reshuffles the whole biome layout." },
        { "VoronoiSeed",
            "Seed for the entire world: biome layout, climate, oceans, lakes, ponds and rivers all derive from it. Same seed + same settings = same world. Change it to get a different world." },
        { "VoronoiScale",
            "Size (world units) of one Voronoi grid cell. This is the base unit of the world's large-scale layout: climate, border warp, cluster radius, continents and islands are all set as multipliers of it.\n\n" +
            "Implications: doubling it doubles the size of biomes, climate zones, continents and oceans together, keeping their proportions." },
        { "useWeightedBiome",
            "ON: each biome's Weight makes it more or less likely to be picked for a seed point (Weight 2 = twice as likely as Weight 1). OFF: all biomes are equally likely (climate still applies)." },

        // --- Natural placement
        { "biomeClusterStrength",
            "How strongly a new seed point copies the biome of nearby points. 0 = every point rolls its biome independently (patchy confetti); 1 = large contiguous territories of one biome." },
        { "biomeClusterRadiusMultiplier",
            "How far (Voronoi Scale x this) a point looks for neighbors when clustering. Larger = territories influence each other from further away, producing bigger regions." },
        { "biomeRepeatPenalty",
            "Discourages the same biome from appearing again just outside its own territory, so the world alternates biomes instead of repeating one. 0 = off." },
        { "voronoiWarpStrength",
            "How far (world units) biome borders are pushed around by noise. 0 = straight polygon edges; higher = wavy, organic borders. Very high values can split regions into odd shapes." },
        { "voronoiWarpScaleMultiplier",
            "Size of the border warp pattern (Voronoi Scale x this). Small = tight, frequent wiggles; large = broad, gentle curves." },
        { "biomeBlendRange",
            "Width of the transition between biomes, as a fraction of Voronoi Scale (0.2 x 300 = a 60-unit band). 0 = hard cut (can leave cliffs at borders).\n\n" +
            "Implications: keep the band narrower than a biome region (about Voronoi Scale / sqrt(points)); wider than that, every point becomes a mix of several biomes and they all look alike. " +
            "Boundary Max Walkable Slope widens only the borders that need it. Landform relief uses its own, narrower fade (Relief Transition Width)." },
        { "useBiomeBlendedTexturing",
            "Also blends the two biome textures across the transition instead of switching abruptly at the border." },
        { "biomeBoundaryMaxSlopeDegrees",
            "Steepest slope (degrees) allowed where two biomes meet. If two neighbors differ a lot in height (mountains next to plains), the transition is widened until it is no steeper than this, so borders stay walkable. 0 = never widen. The widening is capped so it never exceeds a biome region's own size." },

        // --- Climate
        { "useNaturalClimatePlacement",
            "ON: each world position gets a temperature and moisture, and biomes are placed where their Ideal Temperature/Moisture match (deserts where hot and dry, tundra where cold). OFF: only Weight and clustering decide biomes.\n" +
            "Moisture still drives hydraulic erosion strength either way." },
        { "climateScaleMultiplier",
            "Size of climate zones (Voronoi Scale x this). Larger = broad climate belts spanning many biome regions; smaller = climate changes quickly, so biomes mix more." },

        // --- Water (summary tips; the fields also carry their own [Tooltip])
        { "enableWater", "Master switch for oceans, lakes, ponds and rivers. OFF = terrain is generated with no water shaping or water meshes at all (and no water cost)." },
        { "waterLevel", "Sea level (world Y) for oceans only. Lakes, ponds and rivers each find their own water level from the land around them. Land below this height is not automatically flooded; only the continent field decides where oceans are." },
        { "continentScaleMultiplier", "Size of continents and oceans (Voronoi Scale x this). Larger = bigger landmasses and bigger oceans, further apart. Smaller = more, smaller seas." },
        { "oceanThreshold", "How much of the world is ocean. Lower = rarer oceans (-0.2 is about 15% of the world, 0 about 50%). Changing it moves every coastline." },
        { "spawnLandRadius", "Radius (world units) around the world origin that is kept on land so players never spawn in the sea. 0 = no guarantee." },
        { "lakeSpacing", "Grid size (world units) for lake placement, at most one lake per cell. Doubling it means about a quarter as many lakes. Must be comfortably bigger than 2 x Max Radius." },
        { "pondSpacing", "Grid size (world units) for pond placement, at most one pond per cell. Must be comfortably bigger than 2 x Max Radius." },
        { "riverSpacing", "Grid size (world units) for river springs, at most one spring per cell. Smaller = more rivers (and more tracing work the first time an area is visited)." },
        { "riverMaxLength", "Longest a river can be traced (world units). Also how far each chunk searches for rivers that might pass through it, so very large values cost more on first visit." },
        { "riverSourceWidth", "River width at its source (world units). At high Level Of Detail, stretches narrower than about two mesh vertices are not visible." },

        // --- Erosion
        { "enableErosion", "Master switch for thermal (slope collapse) and hydraulic (water droplet) erosion. Erosion is the most expensive part of generation; turning it off is the quickest speed-up." },
        { "erosionPadding", "Extra cells generated around each chunk so erosion near the edge has neighbors to work with, then thrown away. Too small = visible seams between chunks; larger = slower (the padded area grows with its square). Should exceed Droplet Lifetime." },
        { "thermalIterations", "Number of slope-collapse passes. More = smoother, more settled slopes, at a cost per pass." },
        { "talusAngle", "Slope angle (degrees) above which material slides downhill. Lower = flatter, gentler terrain overall; higher = steep cliffs survive. Each biome's Erosion Resistance raises or lowers this locally." },
        { "thermalErosionRate", "Fraction of the excess slope moved per pass. Higher = faster smoothing per pass (fewer passes needed), but can look blocky." },
        { "hydraulicDropletDensity", "Water droplets simulated per cell. Higher = deeper valleys and gullies but slower generation. 0 = no hydraulic erosion." },
        { "dropletLifetime", "Maximum steps a droplet travels. Longer = longer carved channels; keep it below Erosion Padding or chunk seams can appear." },
        { "dropletInertia", "How much a droplet keeps going straight instead of following the slope. Higher = straighter, smoother channels; lower = twisty, wandering paths." },
        { "sedimentCapacityFactor", "How much sediment fast, steep droplets can carry. Higher = more carving on slopes and more deposition in flats." },
        { "minSedimentCapacity", "Minimum carrying capacity, even on flat ground. Keeps droplets eroding slightly on gentle terrain." },
        { "erodeSpeed", "How quickly droplets pick up material. Higher = sharper, deeper cuts." },
        { "depositSpeed", "How quickly droplets drop material when overloaded. Higher = sediment settles close to where it was picked up." },
        { "evaporateSpeed", "Fraction of water lost per step. Higher = droplets die sooner, so erosion stays local." },
        { "erosionGravity", "How strongly slopes accelerate droplets. Higher = faster droplets that carry more sediment on steep ground." },
        { "erosionRadius", "Radius (cells) over which each droplet erodes. Larger = wider, softer channels; smaller = narrow, sharp gullies." },

        // --- Other
        { "levelOfDetail",
            "Mesh simplification. 0 = one vertex per cell (full detail). Higher values skip cells: vertices are 2 x LOD cells apart (LOD 6 = every 12th cell).\n\n" +
            "Implications: higher LOD = far fewer triangles and faster meshes, but small features (narrow rivers, small ponds, sharp ridges) disappear. Heights are still generated at full resolution." },

        // --- Biomes & objects
        { "biomeDefinitions", "Biomes the world can use. Each entry points to a Biome asset (its height shape, climate, erosion and water settings) and holds the objects that spawn in it. At least one is required." },
        { "shouldSpawnObjects", "Whether biome objects (trees, rocks, etc. listed in each biome's objects) are spawned on generated chunks." },
        { "clusterBaseFrequency", "Frequency of the noise that groups spawned objects into clusters. Higher = smaller, more frequent clumps." },
        { "clusterAmplitude", "Strength of the object clustering noise. Higher = sharper contrast between dense clumps and empty ground." },
    };

    /// <summary>Hover text for a property: the detailed text above, else its own [Tooltip].</summary>
    private static string TipFor(SerializedProperty property)
    {
        return FieldTips.TryGetValue(property.name, out string tip) ? tip : property.tooltip;
    }

    /// <summary>
    /// Draws a property with a custom label and its tooltip. Plain PropertyField(prop, new GUIContent(label))
    /// drops the field's tooltip, which is why labels here previously showed nothing on hover.
    /// </summary>
    private static void Field(SerializedProperty property, string label, bool includeChildren = false)
    {
        if (property == null)
            return;
        EditorGUILayout.PropertyField(property, new GUIContent(label, TipFor(property)), includeChildren);
    }

    // ------------------------------------------------------------------ copy settings

    private const string IncludeDescriptionsPrefKey = "TerrainGeneratorEditor.CopyIncludeDescriptions";

    private void DrawCopySettingsBar(TerrainGenerator generator)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(new GUIContent("Copy Settings",
            "Copies configuration as readable text to the clipboard, e.g. to keep a record of a world you like, compare two setups, or share them."),
            EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Copy All",
            "Every Terrain Generator setting, the derived world sizes, and every assigned biome with all of its values (height shape, climate, erosion, water, textures and spawnable objects).")))
        {
            CopyToClipboard(BuildSettingsReport(generator, true, true), "all terrain and biome settings");
        }
        if (GUILayout.Button(new GUIContent("Copy Generator",
            "Only the Terrain Generator's own settings and derived world sizes (biome list shown by name only).")))
        {
            CopyToClipboard(BuildSettingsReport(generator, true, false), "terrain generator settings");
        }
        if (GUILayout.Button(new GUIContent("Copy Biomes",
            "Only the assigned biomes, with every value of each Biome asset and its spawnable objects.")))
        {
            CopyToClipboard(BuildSettingsReport(generator, false, true), "biome settings");
        }
        EditorGUILayout.EndHorizontal();

        bool includeDescriptions = EditorPrefs.GetBool(IncludeDescriptionsPrefKey, false);
        bool newValue = EditorGUILayout.ToggleLeft(new GUIContent("Include descriptions",
            "Adds each field's description as a comment line above its value. Makes the copied text much longer but self-explanatory."),
            includeDescriptions);
        if (newValue != includeDescriptions)
            EditorPrefs.SetBool(IncludeDescriptionsPrefKey, newValue);
        EditorGUILayout.EndVertical();
    }

    private static void CopyToClipboard(string text, string what)
    {
        EditorGUIUtility.systemCopyBuffer = text;
        int lines = text.Split('\n').Length;
        Debug.Log($"[TerrainGenerator] Copied {what} to the clipboard ({lines} lines).");
    }

    /// <summary>Copies one biome asset's full configuration (used by the per-biome Copy button).</summary>
    private static void CopyBiome(Biome biome)
    {
        var sb = new StringBuilder();
        var writer = new SettingsWriter(sb, EditorPrefs.GetBool(IncludeDescriptionsPrefKey, false));
        writer.Line(0, $"# Biome: {DisplayName(biome)}");
        writer.WriteObject(biome, 0);
        CopyToClipboard(sb.ToString(), $"biome '{DisplayName(biome)}'");
    }

    private string BuildSettingsReport(TerrainGenerator generator, bool includeGenerator, bool includeBiomes)
    {
        serializedObject.ApplyModifiedProperties();
        var sb = new StringBuilder();
        var writer = new SettingsWriter(sb, EditorPrefs.GetBool(IncludeDescriptionsPrefKey, false));

        writer.Line(0, $"# Terrain configuration - '{generator.gameObject.name}' ({System.DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)})");

        if (includeGenerator)
        {
            writer.Line(0, "");
            writer.Line(0, "## Derived values (computed from the settings below)");
            WriteDerived(writer, generator);

            writer.Line(0, "");
            writer.Line(0, "## Terrain Generator");
            SerializedObject so = new SerializedObject(generator);
            SerializedProperty it = so.GetIterator();
            string lastHeader = null;
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (it.name == "m_Script")
                    continue;

                string header = HeaderOf(typeof(TerrainGenerator), it.name);
                if (header != null && header != lastHeader)
                {
                    writer.Line(0, "");
                    writer.Line(0, $"### {header}");
                    lastHeader = header;
                }

                if (it.name == "biomeDefinitions")
                {
                    // Listed by name here; full contents go in the Biomes section.
                    writer.Describe(0, TipFor(it));
                    BiomeInstance[] list = generator.BiomeDefinitions;
                    writer.Line(0, $"biomeDefinitions: {(list == null ? 0 : list.Length)} biome(s)");
                    if (list != null)
                    {
                        for (int i = 0; i < list.Length; i++)
                            writer.Line(1, $"[{i}] {(list[i] != null && list[i].BiomePrefab != null ? DisplayName(list[i].BiomePrefab) : "(none)")}");
                    }
                    continue;
                }

                writer.Describe(0, TipFor(it));
                writer.WriteProperty(it.Copy(), 0, false);
            }
        }

        if (includeBiomes)
        {
            writer.Line(0, "");
            writer.Line(0, "## Biomes");
            BiomeInstance[] list = generator.BiomeDefinitions;
            if (list == null || list.Length == 0)
            {
                writer.Line(0, "(no biomes assigned)");
            }
            else
            {
                for (int i = 0; i < list.Length; i++)
                {
                    BiomeInstance instance = list[i];
                    writer.Line(0, "");
                    if (instance == null || instance.BiomePrefab == null)
                    {
                        writer.Line(0, $"### [{i}] (missing biome asset)");
                        continue;
                    }

                    Biome biome = instance.BiomePrefab;
                    writer.Line(0, $"### [{i}] {DisplayName(biome)}");
                    string path = AssetDatabase.GetAssetPath(biome);
                    if (!string.IsNullOrEmpty(path))
                        writer.Line(0, $"asset: {path}");
                    writer.WriteObject(biome, 0);

                    // The per-instance part (spawnable objects) lives on the generator, not the Biome asset.
                    SerializedObject so = new SerializedObject(generator);
                    SerializedProperty entry = so.FindProperty("biomeDefinitions").GetArrayElementAtIndex(i);
                    SerializedProperty objects = entry.FindPropertyRelative("runtimeObjects");
                    if (objects != null)
                    {
                        writer.Describe(0, TipFor(objects));
                        writer.WriteProperty(objects, 0, false, "objects");
                    }
                }
            }
        }

        return sb.ToString();
    }

    private static void WriteDerived(SettingsWriter writer, TerrainGenerator g)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        int chunk = g.ChunkSize;
        int lod = g.LevelOfDetail;
        int step = lod > 0 ? lod * 2 : 1;
        float regionSize = g.VoronoiScale / Mathf.Sqrt(Mathf.Max(1, g.NumVoronoiPoints));

        writer.Line(0, $"chunkSize: {chunk} cells = {chunk - 1} world units per side");
        writer.Line(0, $"meshVertexSpacing: every {step} cell(s) at Level Of Detail {lod} ({(chunk - 1) / step + 1} vertices per side)");
        writer.Line(0, $"voronoiCellSize: {Num(g.VoronoiScale)} world units, {g.NumVoronoiPoints} biome points each");
        writer.Line(0, $"typicalBiomeRegionWidth: ~{regionSize.ToString("0", ci)} world units (before clustering)");
        writer.Line(0, $"biomeNoisePeriod: ~{chunk - 1} / biome frequency world units (frequency 1 = one hill per chunk width)");
        writer.Line(0, $"clusterRadius: {Num(g.BiomeClusterRadius)} world units");
        writer.Line(0, $"borderWarpScale: {Num(g.VoronoiWarpScale)} world units");
        writer.Line(0, $"climateScale: {Num(g.ClimateNoiseScale)} world units");
        writer.Line(0, $"continentScale: {Num(g.ContinentScale)} world units");
    }

    private static string HeaderOf(System.Type type, string fieldName)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null)
            return null;
        object[] headers = field.GetCustomAttributes(typeof(HeaderAttribute), false);
        return headers.Length > 0 ? ((HeaderAttribute)headers[headers.Length - 1]).header : null;
    }

    private static string DisplayName(Object obj)
    {
        if (obj is Biome biome && !string.IsNullOrEmpty(biome.name))
            return biome.name;
        return obj != null ? obj.name : "(none)";
    }

    private static string Num(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes serialized properties as indented "name: value" lines. Works from Unity's serialized data
    /// rather than a hand-kept field list, so fields added to TerrainGenerator/Biome later are included
    /// automatically. ScriptableObject references (e.g. a Biome asset) are expanded when asked, once each.
    /// </summary>
    private sealed class SettingsWriter
    {
        private readonly StringBuilder _sb;
        private readonly bool _descriptions;
        private readonly HashSet<Object> _expanded = new HashSet<Object>();

        public SettingsWriter(StringBuilder sb, bool descriptions)
        {
            _sb = sb;
            _descriptions = descriptions;
        }

        public void Line(int indent, string text)
        {
            _sb.Append(' ', indent * 2).Append(text).Append('\n');
        }

        public void Describe(int indent, string description)
        {
            if (!_descriptions || string.IsNullOrEmpty(description))
                return;
            foreach (string line in description.Split('\n'))
            {
                if (line.Length > 0)
                    Line(indent, "# " + line);
            }
        }

        /// <summary>Writes every visible serialized field of a Unity object (e.g. a Biome asset).</summary>
        public void WriteObject(Object obj, int indent)
        {
            if (obj == null)
                return;
            _expanded.Add(obj);
            SerializedObject so = new SerializedObject(obj);
            SerializedProperty it = so.GetIterator();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (it.name == "m_Script")
                    continue;
                Describe(indent, it.tooltip);
                WriteProperty(it.Copy(), indent, true);
            }
        }

        public void WriteProperty(SerializedProperty p, int indent, bool expandAssets, string labelOverride = null)
        {
            string label = labelOverride ?? p.name;

            if (p.isArray && p.propertyType != SerializedPropertyType.String)
            {
                Line(indent, $"{label}: [{p.arraySize}]");
                for (int i = 0; i < p.arraySize; i++)
                    WriteProperty(p.GetArrayElementAtIndex(i), indent + 1, expandAssets, $"[{i}]");
                return;
            }

            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer:
                    Line(indent, $"{label}: {p.intValue}");
                    return;
                case SerializedPropertyType.Boolean:
                    Line(indent, $"{label}: {(p.boolValue ? "true" : "false")}");
                    return;
                case SerializedPropertyType.Float:
                    Line(indent, $"{label}: {Num(p.floatValue)}");
                    return;
                case SerializedPropertyType.String:
                    Line(indent, $"{label}: \"{p.stringValue}\"");
                    return;
                case SerializedPropertyType.Enum:
                    string[] names = p.enumDisplayNames;
                    int index = p.enumValueIndex;
                    Line(indent, $"{label}: {(index >= 0 && index < names.Length ? names[index] : p.intValue.ToString(CultureInfo.InvariantCulture))}");
                    return;
                case SerializedPropertyType.Color:
                    Color c = p.colorValue;
                    Line(indent, $"{label}: RGBA({Num(c.r)}, {Num(c.g)}, {Num(c.b)}, {Num(c.a)})");
                    return;
                case SerializedPropertyType.Vector2:
                    Line(indent, $"{label}: ({Num(p.vector2Value.x)}, {Num(p.vector2Value.y)})");
                    return;
                case SerializedPropertyType.Vector3:
                    Vector3 v = p.vector3Value;
                    Line(indent, $"{label}: ({Num(v.x)}, {Num(v.y)}, {Num(v.z)})");
                    return;
                case SerializedPropertyType.Vector2Int:
                    Line(indent, $"{label}: ({p.vector2IntValue.x}, {p.vector2IntValue.y})");
                    return;
                case SerializedPropertyType.LayerMask:
                    Line(indent, $"{label}: {p.intValue}");
                    return;
                case SerializedPropertyType.ObjectReference:
                    WriteReference(p, indent, expandAssets, label);
                    return;
                case SerializedPropertyType.Generic:
                    Line(indent, $"{label}:");
                    SerializedProperty child = p.Copy();
                    SerializedProperty end = p.GetEndProperty();
                    bool enterChildren = true;
                    while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                    {
                        enterChildren = false;
                        Describe(indent + 1, child.tooltip);
                        WriteProperty(child.Copy(), indent + 1, expandAssets);
                    }
                    return;
                default:
                    Line(indent, $"{label}: ({p.propertyType})");
                    return;
            }
        }

        private void WriteReference(SerializedProperty p, int indent, bool expandAssets, string label)
        {
            Object obj = p.objectReferenceValue;
            if (obj == null)
            {
                Line(indent, $"{label}: none");
                return;
            }

            string path = AssetDatabase.GetAssetPath(obj);
            string type = obj.GetType().Name;
            string where = string.IsNullOrEmpty(path) ? "" : $" ({path})";

            // Expand nested data assets (a Biome, or any other ScriptableObject) so their values are
            // included too; textures, prefabs and materials are just named.
            if (expandAssets && obj is ScriptableObject && !_expanded.Contains(obj))
            {
                Line(indent, $"{label}: {type} '{DisplayName(obj)}'{where}");
                WriteObject(obj, indent + 1);
                return;
            }
            Line(indent, $"{label}: {type} '{DisplayName(obj)}'{where}");
        }
    }
}
