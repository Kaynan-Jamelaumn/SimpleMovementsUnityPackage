using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: the Terrain Shape (Landforms) section and the per-biome landform table (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor : Editor
{
    /// <summary>
    /// Terrain Shape section: the mode switch, the landform transition/placement settings, and a per-biome
    /// landform picker showing which landform each biome actually ends up using under the current mode.
    /// </summary>
    private void DrawLandformSection(TerrainGenerator generator)
    {
        EditorGUILayout.HelpBox(
            "A biome's landform decides the shape of the ground: Mountains (ranges of connected peaks and ridges " +
            "with valleys), Hills (rounded, rolling), Plains (broad, low swells), Dunes, Wetland, Plateau (flat tops " +
            "with cliff steps and canyons), or Classic (the original terrain). The biome still decides everything " +
            "else: textures, climate, water, objects. Amplitude sets a landform's height, Frequency its feature " +
            "size, Persistence its roughness.\n\n" +
            "Landform terrain is computed from world position and seed only, so it continues seamlessly across " +
            "chunks. At borders the relief (peaks, hills) sinks into foothills before the neighbor begins, and " +
            "mountain fronts rise at most ~50 degrees (other landforms: Boundary Max Walkable Slope).",
            MessageType.Info);

        DrawProp("terrainShapeMode", "Terrain Shape Mode");
        TerrainShapeMode mode = (TerrainShapeMode)serializedObject.FindProperty("terrainShapeMode").enumValueIndex;
        using (new EditorGUI.DisabledScope(mode == TerrainShapeMode.ClassicOnly))
        {
            DrawProp("landformTransitionWidth", "Relief Transition Width");
            DrawProp("mountainBeltStrength", "Mountain Belt Strength");
            using (new EditorGUI.DisabledScope(serializedObject.FindProperty("mountainBeltStrength").floatValue <= 0f))
            {
                DrawProp("mountainBeltScaleMultiplier", "Mountain Belt Scale Multiplier");
                EditorGUILayout.LabelField($"= {generator.MountainBeltScale:0} world units between belts", EditorStyles.miniLabel);
            }
        }

        BiomeInstance[] biomes = generator.BiomeDefinitions;
        if (biomes == null || biomes.Length == 0)
            return;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(new GUIContent("Biome Landforms & Placement",
            "Each biome's Landform (the shape of its ground) and Placement (Land = normal biome layout, Ocean = only on the " +
            "seafloor, Volcanic = only painted over volcanoes), and the landform it actually uses under the current Terrain Shape Mode."),
            EditorStyles.miniBoldLabel);

        bool anyClassic = false;
        bool anyMountain = false;
        bool anyOcean = false;
        bool anyVolcanic = false;
        foreach (BiomeInstance instance in biomes)
        {
            Biome biome = instance != null ? instance.BiomePrefab : null;
            if (biome == null)
                continue;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.IsNullOrEmpty(biome.name) ? "(unnamed)" : biome.name, GUILayout.Width(110));
            EditorGUI.BeginChangeCheck();
            LandformType chosen = (LandformType)EditorGUILayout.EnumPopup(biome.landform, GUILayout.Width(110));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(biome, "Change Biome Landform");
                biome.landform = chosen;
                EditorUtility.SetDirty(biome);
            }
            EditorGUI.BeginChangeCheck();
            BiomePlacement placement = (BiomePlacement)EditorGUILayout.EnumPopup(biome.placement, GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(biome, "Change Biome Placement");
                biome.placement = placement;
                EditorUtility.SetDirty(biome);
            }

            LandformType effective = LandformGenerator.Effective(biome, mode);
            string note = effective == biome.landform ? ""
                : mode == TerrainShapeMode.ClassicOnly ? "uses Classic (Classic Only mode)"
                : $"uses {effective} (suggested)";
            EditorGUILayout.LabelField(note, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            anyClassic |= biome.landform == LandformType.Classic;
            anyMountain |= effective == LandformType.Mountains || effective == LandformType.Plateau || effective == LandformType.Glacial;
            anyOcean |= biome.placement == BiomePlacement.Ocean;
            anyVolcanic |= biome.placement == BiomePlacement.Volcanic;
        }

        if (anyOcean && !generator.EnableOceans)
            EditorGUILayout.HelpBox("Some biomes are Ocean biomes, but Oceans are turned off (Water section), so they are never used.", MessageType.Warning);
        if (anyVolcanic && !generator.EnableVolcanoes)
            EditorGUILayout.HelpBox("A biome is set to Volcanic, but Volcanoes are turned off, so it is never used.", MessageType.Warning);
        bool anyLand = false;
        foreach (BiomeInstance instance in biomes)
            anyLand |= instance != null && instance.BiomePrefab != null && instance.BiomePrefab.placement == BiomePlacement.Land;
        if (!anyLand)
            EditorGUILayout.HelpBox("No biome has Placement = Land, so all biomes are used for land as a fallback.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(!anyClassic))
        {
            if (GUILayout.Button(new GUIContent("Set Classic Biomes To Suggested Landforms",
                "Gives every biome still set to Classic the landform suggested from its settings (Ocean biomes = Sea Plain, " +
                "hot and dry = Dunes, very wet and flat = Wetland, amplitude 35+ = Mountains (Glacial if cold), 18+ = Highlands, " +
                "9+ = Hills, otherwise Plains). Biomes that already have a landform are left alone. Undoable.")))
            {
                foreach (BiomeInstance instance in biomes)
                {
                    Biome biome = instance != null ? instance.BiomePrefab : null;
                    if (biome == null || biome.landform != LandformType.Classic)
                        continue;
                    Undo.RecordObject(biome, "Set Suggested Landforms");
                    biome.landform = LandformGenerator.Suggest(biome);
                    EditorUtility.SetDirty(biome);
                }
            }
        }

        float regionWidth = generator.VoronoiScale / Mathf.Sqrt(Mathf.Max(1, generator.NumVoronoiPoints));
        if (anyMountain && regionWidth < 180f)
        {
            EditorGUILayout.HelpBox(
                $"Biome regions are only ~{regionWidth:0} world units across. Mountains keep their full height only " +
                "far enough from their border to come down again, so in small territories they stay low. For " +
                "large ranges, raise Voronoi Scale, lower Num Voronoi Points, or raise Cluster Strength / Mountain " +
                "Belt Strength so mountain territories merge.",
                MessageType.Info);
        }
    }
}
