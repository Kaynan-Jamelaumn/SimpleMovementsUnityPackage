using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: the "Copy Settings" export of every value and biome as plain text (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor
{
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
