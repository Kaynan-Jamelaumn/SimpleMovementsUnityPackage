using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: the Biomes section - each biome and the objects that spawn in it, with every placement
// rule grouped, explained and checked, presets, auto-configuration from the prefab, copy/paste and a one-chunk
// placement test (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor
{
    private static readonly Dictionary<string, bool> ObjectFoldouts = new Dictionary<string, bool>();
    private static string copiedObjectJson;

    // Last "Test Placement" run: per object definition, what happened in the test chunk.
    private static readonly Dictionary<BiomeObject, ObjectTestResult> TestResults = new Dictionary<BiomeObject, ObjectTestResult>();
    private static string testSummary;

    private sealed class ObjectTestResult
    {
        public int Placed, Tried;
        public int[] Rejected;
    }

    private static bool Foldout(string key, string label, bool defaultOpen = false, string tooltip = null, GUIStyle style = null)
    {
        if (!ObjectFoldouts.TryGetValue(key, out bool open))
            open = defaultOpen;
        open = EditorGUILayout.Foldout(open, new GUIContent(label, tooltip), true, style ?? EditorStyles.foldout);
        ObjectFoldouts[key] = open;
        return open;
    }

    // ------------------------------------------------------------------ biomes

    /// <summary>Contents of the "Biomes" section.</summary>
    private void DrawBiomesSection()
    {
        TerrainGenerator generator = (TerrainGenerator)target;
        EditorGUILayout.HelpBox(
            "Each biome entry points to a Biome asset and lists the objects that spawn in it. Open an object to edit its rules - every " +
            "field explains itself on hover. Start from a preset (Presets button) or let Auto-Configure set it up from the prefab's size and shape.",
            MessageType.None);

        if (biomeDefinitionsProp.arraySize == 0)
            EditorGUILayout.HelpBox("No biomes assigned - terrain generation has nothing to draw from and will fail.", MessageType.Error);

        for (int b = 0; b < biomeDefinitionsProp.arraySize; b++)
        {
            SerializedProperty biomeProp = biomeDefinitionsProp.GetArrayElementAtIndex(b);
            SerializedProperty prefabProp = biomeProp.FindPropertyRelative("BiomePrefab");
            SerializedProperty objectsProp = biomeProp.FindPropertyRelative("runtimeObjects");
            Biome biome = prefabProp.objectReferenceValue as Biome;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            string title = (biome != null ? biome.name : "(no biome asset)") + $"  -  {objectsProp.arraySize} object{(objectsProp.arraySize == 1 ? "" : "s")}";
            bool open = Foldout($"biome/{b}", title, false, null, EditorStyles.foldout);
            if (GUILayout.Button(new GUIContent("▲", "Move this biome up."), EditorStyles.miniButton, GUILayout.Width(22)) && b > 0)
                biomeDefinitionsProp.MoveArrayElement(b, b - 1);
            if (GUILayout.Button(new GUIContent("▼", "Move this biome down."), EditorStyles.miniButton, GUILayout.Width(22)) && b < biomeDefinitionsProp.arraySize - 1)
                biomeDefinitionsProp.MoveArrayElement(b, b + 1);
            if (GUILayout.Button(new GUIContent("✕", "Remove this biome (and its object list) from the generator."), EditorStyles.miniButton, GUILayout.Width(22)))
            {
                if (EditorUtility.DisplayDialog("Remove biome", $"Remove '{title}' and its object list from the generator?", "Remove", "Cancel"))
                {
                    biomeDefinitionsProp.DeleteArrayElementAtIndex(b);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (open)
            {
                EditorGUILayout.PropertyField(prefabProp, new GUIContent("Biome Asset", "The Biome asset: its height shape, climate, erosion and water settings, and texture."));
                if (biome == null)
                    EditorGUILayout.HelpBox("Assign a Biome asset.", MessageType.Warning);

                for (int o = 0; o < objectsProp.arraySize; o++)
                {
                    if (!DrawBiomeObject(generator, b, o, objectsProp.GetArrayElementAtIndex(o), biome))
                        break;   // the list changed; drawn again next frame
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Add Object ▾", "Add an object to this biome, empty or from a preset."), GUILayout.Width(120)))
                    ShowAddObjectMenu(generator, b);
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(copiedObjectJson)))
                {
                    if (GUILayout.Button(new GUIContent("Paste As New", "Add the copied object (prefab and rules) to this biome."), GUILayout.Width(100)))
                        ChangeGenerator(generator, "Paste Biome Object", g =>
                        {
                            var pasted = new BiomeObject();
                            EditorJsonUtility.FromJsonOverwrite(copiedObjectJson, pasted);
                            g.BiomeDefinitions[b].runtimeObjects.Add(pasted);
                        });
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Add Biome", "Add an empty biome entry; then assign its Biome asset.")))
        {
            biomeDefinitionsProp.InsertArrayElementAtIndex(biomeDefinitionsProp.arraySize);
            SerializedProperty added = biomeDefinitionsProp.GetArrayElementAtIndex(biomeDefinitionsProp.arraySize - 1);
            added.FindPropertyRelative("BiomePrefab").objectReferenceValue = null;
            added.FindPropertyRelative("runtimeObjects").ClearArray();
        }
        if (GUILayout.Button(new GUIContent("Test Placement", "Generates one chunk (where the Scene view is looking) and places every object in it, without entering Play mode. " +
            "Each object then shows how many it got and which rules rejected the rest.")))
            RunPlacementTest(generator);
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(testSummary))
            EditorGUILayout.HelpBox(testSummary, MessageType.None);
    }

    // ------------------------------------------------------------------ one object

    /// <summary>Draws one biome object. False when the list was changed (stop drawing it this frame).</summary>
    private bool DrawBiomeObject(TerrainGenerator generator, int b, int o, SerializedProperty obj, Biome owner)
    {
        BiomeObject def = generator.BiomeDefinitions[b].runtimeObjects[o];
        SerializedProperty prefab = obj.FindPropertyRelative("terrainObject");
        GameObject prefabObject = prefab.objectReferenceValue as GameObject;
        string key = $"obj/{b}/{o}";

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        string name = prefabObject != null ? prefabObject.name : "(no prefab)";
        bool open = Foldout(key, name, false, Summary(generator, def));
        GUILayout.Label(Summary(generator, def), EditorStyles.miniLabel, GUILayout.MinWidth(40));
        if (GUILayout.Button(new GUIContent("Presets ▾", "Replace this object's rules with a preset (its prefab and Group Tag are kept)."), EditorStyles.miniButton, GUILayout.Width(66)))
            ShowPresetMenu(generator, b, o);
        if (GUILayout.Button(new GUIContent("⋯", "Copy, paste, duplicate, move or remove this object."), EditorStyles.miniButton, GUILayout.Width(22)))
            ShowObjectMenu(generator, b, o);
        EditorGUILayout.EndHorizontal();

        if (open)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(prefab, new GUIContent("Prefab", "The prefab to place."));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(obj.FindPropertyRelative("groupTag"), new GUIContent("Group Tag", obj.FindPropertyRelative("groupTag").tooltip));
            if (GUILayout.Button(new GUIContent("Auto-Configure", "Sets Group Tag, footprint, tilt and ground-contact limits from the prefab's measured size and shape (tall and thin like a tree, flat like a rug, round like a rock...). Density and the other rules are left alone."), EditorStyles.miniButton, GUILayout.Width(100)))
                ChangeGenerator(generator, "Auto-Configure Biome Object", g => AutoConfigure(g.BiomeDefinitions[b].runtimeObjects[o]));
            EditorGUILayout.EndHorizontal();

            DrawObjectInfo(generator, def, owner);

            DrawDensityGroup(obj, key, generator, def);
            DrawBiomeGroup(obj, key, generator, owner);
            DrawHeightGroup(obj, key);
            DrawSlopeGroup(obj, key, def);
            DrawWaterGroup(obj, key, generator);
            DrawClimateGroup(obj, key);
            DrawFeatureGroup(obj, key);
            DrawClusterGroup(obj, key, generator, def);
            DrawRelationGroup(obj, key, generator, def);
            DrawOrientationGroup(obj, key);
            DrawGroundGroup(obj, key, def);
            DrawLimitGroup(obj, key);
            if (Foldout(key + "/seed", "Random Seed"))
                DrawChildren(obj.FindPropertyRelative("seed"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();
        return true;
    }

    /// <summary>One line describing an object's main rules.</summary>
    private static string Summary(TerrainGenerator generator, BiomeObject def)
    {
        var parts = new List<string>();
        if (def.limits.mode == PlacementMode.Landmark)
            parts.Add(def.limits.unique ? "unique landmark" : "landmark");
        else
            parts.Add($"{def.probabilityToSpawn:0.##}%");
        switch (def.water.placement)
        {
            case WaterPlacement.DryLand: parts.Add("dry land"); break;
            case WaterPlacement.Anywhere: parts.Add("anywhere"); break;
            case WaterPlacement.NearWater: parts.Add("near water"); break;
            case WaterPlacement.AwayFromWater: parts.Add("away from water"); break;
            case WaterPlacement.Shoreline: parts.Add("shoreline"); break;
            case WaterPlacement.InWater: parts.Add("in water (" + def.water.heightInWater + ")"); break;
        }
        float maxSlope = Mathf.Clamp(def.slopeThreshold * def.slopeAvoidance, 0f, 90f);
        parts.Add(def.slope.minSlope > 0f ? $"slope {def.slope.minSlope:0}-{maxSlope:0}°" : $"slope ≤ {maxSlope:0}°");
        if (def.isClusterable)
            parts.Add("clusters");
        if (def.relations.Count > 0)
            parts.Add($"{def.relations.Count} relation{(def.relations.Count == 1 ? "" : "s")}");
        return string.Join(" · ", parts);
    }

    /// <summary>What the object measures, what its rules add up to, and anything that looks wrong.</summary>
    private void DrawObjectInfo(TerrainGenerator generator, BiomeObject def, Biome owner)
    {
        var info = new StringBuilder();
        var warnings = new List<string>();
        GameObject prefab = def.terrainObject;
        PrefabShape shape = prefab != null ? PrefabShapeCache.Get(prefab) : null;

        float maxScale = Mathf.Max(0.01f, Mathf.Max(def.scaleRange.x, def.scaleRange.y));
        float sizeRadius = 0.5f;
        if (shape != null && shape.TryGetSizeBox(out Vector3 min, out Vector3 max))
        {
            Vector3 size = Vector3.Scale(max - min, shape.RootScale);
            float halfX = Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x)) * Mathf.Abs(shape.RootScale.x);
            float halfZ = Mathf.Max(Mathf.Abs(min.z), Mathf.Abs(max.z)) * Mathf.Abs(shape.RootScale.z);
            sizeRadius = Mathf.Sqrt(halfX * halfX + halfZ * halfZ);
            info.Append($"Size: {size.x:0.##} x {size.y:0.##} x {size.z:0.##} (width x height x depth, scale 1)");
            info.Append(shape.HasBase ? $", base {(shape.BaseMax.x - shape.BaseMin.x) * shape.RootScale.x:0.##} x {(shape.BaseMax.z - shape.BaseMin.z) * shape.RootScale.z:0.##}" : "");
            info.Append(shape.HasColliders ? ", has colliders" : ", no colliders");
            if (!shape.HasBase && def.ground.footprint == FootprintSource.Automatic)
            {
                bool tall = size.y > 2f * Mathf.Max(size.x, size.z);
                info.Append(shape.HasColliders ? ". Footprint: its colliders" : ". Footprint: its whole bounds");
                if (tall && !shape.HasColliders && def.ground.footprintScale > 0.5f)
                    warnings.Add("Tall object measured by its whole bounds (meshes not Read/Write-enabled, no colliders): trees will be rejected on slopes. Use Auto-Configure, lower Footprint Scale (Ground Contact) to about 0.25, or enable Read/Write on the model.");
            }
        }
        else if (prefab != null)
        {
            info.Append("No renderers or colliders found: the pivot is treated as a point on the ground.");
        }

        float spacing = def.minSpacing > 0f ? def.minSpacing : Mathf.Max(0.5f, 1.8f * sizeRadius * maxScale);
        float span = generator.ChunkSize - 1;
        if (def.limits.mode == PlacementMode.Scatter)
        {
            float raw = Mathf.Max(0f, def.probabilityToSpawn) * 0.01f * span * span;
            float cap = span * span / Mathf.Max(0.01f, spacing * spacing * 1.15f);
            info.Append($"\nSpacing: {spacing:0.##} units{(def.minSpacing > 0f ? "" : " (from its size)")}. " +
                        $"Up to ~{Mathf.Min(raw, cap):N0} per chunk before the other rules filter spots{(raw > cap ? " (capped by spacing - raising the chance won't add more)" : "")}.");
        }
        else
        {
            info.Append($"\nLandmark: {(def.limits.unique ? "one in the world" : $"up to {Mathf.Max(1, def.limits.maxPerRegion)} per {def.limits.regionSize:0} x {def.limits.regionSize:0} region")}.");
        }

        // Checks.
        if (prefab == null)
            warnings.Add("No prefab: nothing is placed.");
        if (def.probabilityToSpawn <= 0f && !(def.limits.mode == PlacementMode.Landmark && (def.limits.guaranteed || def.limits.unique)))
            warnings.Add("Probability To Spawn is 0: this object never spawns.");
        float maxSlope = Mathf.Clamp(def.slopeThreshold * def.slopeAvoidance, 0f, 90f);
        if (def.slope.minSlope > maxSlope)
            warnings.Add($"Min Slope ({def.slope.minSlope:0}°) is above the maximum ({maxSlope:0}° = Slope Threshold x Slope Avoidance): no ground qualifies.");
        if (def.slope.surfaces == 0)
            warnings.Add("No surface kinds allowed (Slope > Surfaces): no ground qualifies.");
        if (def.water.placement != WaterPlacement.DryLand && def.water.placement != WaterPlacement.Anywhere && !generator.EnableWater)
            warnings.Add("Water is turned off on the generator, so this water rule can never be met.");
        if (def.water.placement != WaterPlacement.DryLand && def.water.placement != WaterPlacement.Anywhere && def.water.bodies == 0)
            warnings.Add("No water bodies chosen (Water > Bodies).");
        if (def.scaleRange.x > def.scaleRange.y)
            warnings.Add("Scale Range: x (min) is above y (max).");
        if (owner != null && def.biomes.forbiddenIn.Contains(owner))
            warnings.Add("Its own biome is in Forbidden In: it can only spawn in its Also Allowed In biomes.");
        HashSet<string> tags = AllTags(generator);
        foreach (ObjectRelation relation in def.relations)
        {
            if (string.IsNullOrEmpty(relation.otherTag))
                warnings.Add("A relationship has no Other Tag.");
            else if (!tags.Contains(relation.otherTag))
                warnings.Add($"Relationship to '{relation.otherTag}': no object has that Group Tag (or prefab name).");
            else if (relation.otherTag == def.EffectiveTag && (relation.kind == RelationKind.CannotSpawnNear || relation.kind == RelationKind.PreventsNearby))
                warnings.Add($"'{relation.otherTag}' is this object's own tag - use Min Spacing for distance between copies.");
        }
        foreach (FeatureDistanceRule rule in def.featureDistances)
        {
            if (rule.feature == PlacementFeature.Custom && string.IsNullOrEmpty(rule.customTag))
                warnings.Add("A Custom distance rule has no tag.");
            if (rule.maxDistance > 0f && rule.minDistance > rule.maxDistance)
                warnings.Add($"Distance rule ({rule.feature}): min is above max.");
        }
        if (def.limits.mode == PlacementMode.Landmark && def.limits.unique && def.limits.uniqueSearchRadius < def.limits.regionSize)
            warnings.Add("Unique landmark: the search radius is smaller than one region - it may find no spot.");

        if (TestResults.TryGetValue(def, out ObjectTestResult test))
        {
            info.Append($"\nLast test: {test.Placed} placed in the test chunk, {test.Tried} spots tried.");
            var top = Enumerable.Range(0, test.Rejected.Length).Where(s => test.Rejected[s] > 0).OrderByDescending(s => test.Rejected[s]).Take(3).ToList();
            if (top.Count > 0)
                info.Append(" Most rejected by: " + string.Join(", ", top.Select(s => StageName((ObjectPlacementEngine.Stage)s) + " " + test.Rejected[s])) + ".");
        }

        EditorGUILayout.HelpBox(info.ToString(), MessageType.None);
        foreach (string warning in warnings)
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
    }

    private static HashSet<string> AllTags(TerrainGenerator generator)
    {
        var tags = new HashSet<string>();
        if (generator.BiomeDefinitions == null)
            return tags;
        foreach (BiomeInstance instance in generator.BiomeDefinitions)
            if (instance?.runtimeObjects != null)
                foreach (BiomeObject o in instance.runtimeObjects)
                    if (o != null && !string.IsNullOrEmpty(o.EffectiveTag))
                        tags.Add(o.EffectiveTag);
        return tags;
    }

    // ------------------------------------------------------------------ rule groups

    private void DrawDensityGroup(SerializedProperty obj, string key, TerrainGenerator generator, BiomeObject def)
    {
        if (!Foldout(key + "/density", "Density", true, "How often it spawns, patchiness, per-chunk limit and priority."))
            return;
        EditorGUI.indentLevel++;
        Rel(obj, "probabilityToSpawn", "Probability To Spawn (%)");
        EditorGUILayout.LabelField($"   = about {Mathf.Max(0f, def.probabilityToSpawn):0.##} per 100 square units, {Mathf.Max(0f, def.probabilityToSpawn) * 0.01f * (generator.ChunkSize - 1) * (generator.ChunkSize - 1):N0} per chunk at most (before rules and spacing)", EditorStyles.miniLabel);
        SerializedProperty noise = obj.FindPropertyRelative("densityNoise");
        Rel(noise, "enabled", "Density Noise (patches)");
        if (noise.FindPropertyRelative("enabled").boolValue)
        {
            EditorGUI.indentLevel++;
            Rel(noise, "scale", "Patch Size");
            Rel(noise, "octaves", "Octaves");
            Rel(noise, "strength", "Strength");
            Rel(noise, "coverage", "Coverage");
            EditorGUI.indentLevel--;
        }
        Rel(obj, "hasMaxNumberOfObjects", "Limit Per Chunk");
        if (obj.FindPropertyRelative("hasMaxNumberOfObjects").boolValue)
        {
            EditorGUI.indentLevel++;
            Rel(obj, "maxNumberOfThisObject", "Max Per Chunk");
            EditorGUI.indentLevel--;
        }
        Rel(obj, "placementPriority", "Placement Priority");
        EditorGUI.indentLevel--;
    }

    private void DrawBiomeGroup(SerializedProperty obj, string key, TerrainGenerator generator, Biome owner)
    {
        if (!Foldout(key + "/biomes", "Biomes & Borders", false, "Which biomes it may also spawn in or must avoid, and how it treats biome borders."))
            return;
        EditorGUI.indentLevel++;
        SerializedProperty biomes = obj.FindPropertyRelative("biomes");
        BiomeMask(generator, biomes.FindPropertyRelative("alsoAllowedIn"), "Also Allowed In", owner);
        BiomeMask(generator, biomes.FindPropertyRelative("forbiddenIn"), "Forbidden In", null);
        Rel(biomes, "borderMode", "Border Mode");
        Rel(obj, "biomeCenterPreference", "Center Preference");
        Rel(biomes, "borderDistance", "Border Distance");
        BiomeMask(generator, biomes.FindPropertyRelative("incompatibleNeighbours"), "Incompatible Neighbours", owner);
        if (biomes.FindPropertyRelative("incompatibleNeighbours").arraySize > 0)
        {
            EditorGUI.indentLevel++;
            Rel(biomes, "incompatibleDistance", "Keep Away By");
            EditorGUI.indentLevel--;
        }
        Rel(biomes, "footprintInBiome", "Whole Footprint In Biome");
        EditorGUI.indentLevel--;
    }

    private void DrawHeightGroup(SerializedProperty obj, string key)
    {
        if (!Foldout(key + "/height", "Height & Land Shape", false, "Altitude limits, height above water, and valleys / slopes / ridges."))
            return;
        EditorGUI.indentLevel++;
        SerializedProperty a = obj.FindPropertyRelative("altitude");
        Toggled(a, "limitAltitude", "Limit Altitude", () => MinMax(a, "minAltitude", "maxAltitude", "Altitude (world Y)"));
        Toggled(a, "limitRelativeAltitude", "Limit Height In Biome", () => MinMax(a, "minRelativeAltitude", "maxRelativeAltitude", "Band (0 = biome min, 1 = max)"));
        Rel(a, "useBiomeHeightBand", "Prefer Biome's Height Band");
        Toggled(a, "limitHeightAboveWater", "Limit Height Above Water", () => MinMax(a, "minHeightAboveWater", "maxHeightAboveWater", "Above water level"));
        Toggled(obj, "useCustomHeightPreference", "Preferred Height (soft)", () =>
        {
            MinMax(obj, "preferredMinHeight", "preferredMaxHeight", "Band");
            Rel(obj, "preferredOptimalHeight", "Most Likely At");
            Rel(obj, "heightPreferenceStrength", "Strictness");
        });

        EditorGUILayout.Space(2);
        Rel(a, "terrainPosition", "Land Shape");
        if (a.FindPropertyRelative("terrainPosition").enumValueIndex != (int)TerrainPosition.Any)
        {
            EditorGUI.indentLevel++;
            Rel(a, "terrainPositionRequired", "Required (hard)");
            if (!a.FindPropertyRelative("terrainPositionRequired").boolValue)
                Rel(a, "positionStrength", "Preference Strength");
            EditorGUI.indentLevel--;
        }
        Rel(a, "reliefRadius", "Shape Radius");
        Toggled(a, "limitRelief", "Limit Relief", () => MinMax(a, "minRelief", "maxRelief", "Relief"));
        Toggled(a, "limitRoughness", "Limit Roughness", () => MinMax(a, "minRoughness", "maxRoughness", "Roughness"));
        Toggled(a, "limitCurvature", "Limit Curvature", () => MinMax(a, "minCurvature", "maxCurvature", "Curvature"));
        EditorGUI.indentLevel--;
    }

    private void DrawSlopeGroup(SerializedProperty obj, string key, BiomeObject def)
    {
        if (!Foldout(key + "/slope", "Slope & Surface", false, "Steepness limits, preferred slope and allowed surface kinds."))
            return;
        EditorGUI.indentLevel++;
        Rel(obj, "slopeThreshold", "Max Slope (degrees)");
        Rel(obj, "slopeAvoidance", "Slope Avoidance (x)");
        EditorGUILayout.LabelField($"   = steepest allowed: {Mathf.Clamp(def.slopeThreshold * def.slopeAvoidance, 0f, 90f):0}°", EditorStyles.miniLabel);
        SerializedProperty s = obj.FindPropertyRelative("slope");
        Rel(s, "minSlope", "Min Slope");
        Rel(s, "surfaces", "Surfaces");
        Toggled(s, "usePreferredSlope", "Preferred Slope (soft)", () =>
        {
            MinMax(s, "preferredMinSlope", "preferredMaxSlope", "Range (degrees)");
            Rel(s, "slopeFalloff", "Falloff");
        });
        EditorGUI.indentLevel--;
    }

    private void DrawWaterGroup(SerializedProperty obj, string key, TerrainGenerator generator)
    {
        if (!Foldout(key + "/water", "Water", false, "Dry land, near/away from water, shoreline or in water - with distance and depth rules."))
            return;
        EditorGUI.indentLevel++;
        SerializedProperty w = obj.FindPropertyRelative("water");
        Rel(w, "placement", "Placement");
        var mode = (WaterPlacement)w.FindPropertyRelative("placement").enumValueIndex;
        if (mode != WaterPlacement.DryLand && mode != WaterPlacement.Anywhere)
            Rel(w, "bodies", "Water Bodies");
        switch (mode)
        {
            case WaterPlacement.NearWater:
                Rel(w, "maxWaterDistance", "Within (units, 0 = 10)");
                break;
            case WaterPlacement.AwayFromWater:
                Rel(w, "minWaterDistance", "At Least (units, 0 = 10)");
                break;
            case WaterPlacement.Shoreline:
                Rel(w, "maxShoreDistance", "Within Shore (units, 0 = 3)");
                break;
            case WaterPlacement.InWater:
                Rel(w, "heightInWater", "Height In Water");
                if (w.FindPropertyRelative("heightInWater").enumValueIndex == (int)WaterHeightMode.Submerged)
                    Rel(w, "submergedFraction", "Bottom (0) - Surface (1)");
                MinMax(w, "minDepth", "maxDepth", "Depth (0 max = any)");
                MinMax(w, "minShoreDistance", "maxShoreDistance", "From Shore (0 max = any)");
                MinMax(w, "shallowDepth", "deepDepth", "Shallow below / deep above");
                Rel(w, "shallowWeight", "Shallow Chance");
                Rel(w, "mediumWeight", "Medium Chance");
                Rel(w, "deepWeight", "Deep Chance");
                Rel(w, "avoidFastFlow", "Avoid Fast Rivers");
                break;
        }
        if (mode == WaterPlacement.DryLand || mode == WaterPlacement.Shoreline)
            MinMax(w, "minWaterDistance", "maxWaterDistance", "Extra Water Distance (0 = none)");
        if (!generator.EnableWater && mode != WaterPlacement.DryLand && mode != WaterPlacement.Anywhere)
            EditorGUILayout.HelpBox("Water is off on the generator (Water section).", MessageType.Warning);
        EditorGUI.indentLevel--;
    }

    private void DrawClimateGroup(SerializedProperty obj, string key)
    {
        if (!Foldout(key + "/climate", "Climate", false, "Moisture, temperature and ground wetness ranges."))
            return;
        EditorGUI.indentLevel++;
        SerializedProperty c = obj.FindPropertyRelative("climate");
        Toggled(c, "useMoisture", "Limit Moisture", () => MinMax(c, "minMoisture", "maxMoisture", "Moisture (0 dry - 1 wet)"));
        Toggled(c, "useTemperature", "Limit Temperature", () => MinMax(c, "minTemperature", "maxTemperature", "Temperature (0 cold - 1 hot)"));
        Toggled(c, "useGroundWetness", "Limit Ground Wetness", () => MinMax(c, "minWetness", "maxWetness", "Wetness (0 dry - 1 shore)"));
        Rel(c, "softness", "Softness (0 = hard)");
        EditorGUI.indentLevel--;
    }

    private void DrawFeatureGroup(SerializedProperty obj, string key)
    {
        SerializedProperty list = obj.FindPropertyRelative("featureDistances");
        if (!Foldout(key + "/features", $"Distances To Features ({list.arraySize})", false, "Keep within or away from water bodies, cliffs, or roads/paths/settlements your game registers with PlacementFeatures."))
            return;
        EditorGUI.indentLevel++;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty rule = list.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("feature"), new GUIContent("Feature", rule.FindPropertyRelative("feature").tooltip));
            if (GUILayout.Button(new GUIContent("✕", "Remove this rule."), EditorStyles.miniButton, GUILayout.Width(22)))
            {
                list.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel++;
            if (rule.FindPropertyRelative("feature").enumValueIndex == (int)PlacementFeature.Custom)
                Rel(rule, "customTag", "Feature Tag");
            MinMax(rule, "minDistance", "maxDistance", "Distance (0 max = any)");
            Rel(rule, "soft", "Soft Edges");
            if (rule.FindPropertyRelative("soft").boolValue)
                Rel(rule, "softFalloff", "Falloff");
            EditorGUI.indentLevel--;
        }
        if (GUILayout.Button(new GUIContent("Add Distance Rule", "E.g. at least 20 units from roads, or within 30 of a river.")))
            list.InsertArrayElementAtIndex(list.arraySize);
        EditorGUI.indentLevel--;
    }

    private void DrawClusterGroup(SerializedProperty obj, string key, TerrainGenerator generator, BiomeObject def)
    {
        if (!Foldout(key + "/clusters", "Clusters & Spacing", false, "Groups (forests, rock fields), how close copies may be, and growth near others."))
            return;
        EditorGUI.indentLevel++;
        Rel(obj, "isClusterable", "Clusters");
        if (obj.FindPropertyRelative("isClusterable").boolValue)
        {
            EditorGUI.indentLevel++;
            Rel(obj, "clusterCount", "Clusters Per Chunk");
            Rel(obj, "clusterRadius", "Cluster Radius");
            SerializedProperty c = obj.FindPropertyRelative("clustering");
            Rel(c, "insideDensity", "Density Inside");
            Rel(c, "outsideDensity", "Density Outside");
            Rel(c, "desiredClusterSize", "Aim Per Cluster (0 = off)");
            Rel(c, "maxClusterSize", "Max Per Cluster (0 = any)");
            EditorGUI.indentLevel--;
        }
        SerializedProperty cl = obj.FindPropertyRelative("clustering");
        Rel(cl, "growthRadius", "Growth Radius (0 = off)");
        if (cl.FindPropertyRelative("growthRadius").floatValue > 0f)
        {
            EditorGUI.indentLevel++;
            Rel(cl, "growthPerNeighbour", "Growth Per Neighbour");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(2);
        Rel(obj, "minSpacing", "Min Spacing (0 = auto)");
        SerializedProperty sp = obj.FindPropertyRelative("spacing");
        Rel(sp, "softSpacing", "Soft Spacing (0 = off)");
        if (sp.FindPropertyRelative("softSpacing").floatValue > 0f)
        {
            EditorGUI.indentLevel++;
            Rel(sp, "softSpacingStrength", "Strength");
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
    }

    private void DrawRelationGroup(SerializedProperty obj, string key, TerrainGenerator generator, BiomeObject def)
    {
        SerializedProperty list = obj.FindPropertyRelative("relations");
        if (!Foldout(key + "/relations", $"Relationships ({list.arraySize})", false, "How it relates to other objects by their Group Tag: exclusions, avoidance, attraction, requirements."))
            return;
        EditorGUI.indentLevel++;
        string[] tags = AllTags(generator).OrderBy(t => t).ToArray();
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty rel = list.GetArrayElementAtIndex(i);
            SerializedProperty tag = rel.FindPropertyRelative("otherTag");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(rel.FindPropertyRelative("kind"), new GUIContent("Relationship", rel.FindPropertyRelative("kind").tooltip));
            if (GUILayout.Button(new GUIContent("✕", "Remove this relationship."), EditorStyles.miniButton, GUILayout.Width(22)))
            {
                list.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(tag, new GUIContent("Other Object", tag.tooltip));
            if (tags.Length > 0 && GUILayout.Button(new GUIContent("▾", "Pick one of the objects' Group Tags."), EditorStyles.miniButton, GUILayout.Width(22)))
            {
                var menu = new GenericMenu();
                foreach (string t in tags)
                {
                    string chosen = t;
                    menu.AddItem(new GUIContent(t), t == tag.stringValue, () =>
                    {
                        tag.serializedObject.Update();
                        tag.stringValue = chosen;
                        tag.serializedObject.ApplyModifiedProperties();
                    });
                }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
            Rel(rel, "radius", "Radius");
            var kind = (RelationKind)rel.FindPropertyRelative("kind").enumValueIndex;
            if (kind == RelationKind.Avoids || kind == RelationKind.AttractedTo)
                Rel(rel, "strength", "Strength");
            EditorGUILayout.LabelField("   " + RelationSentence(kind, def.EffectiveTag, tag.stringValue, rel.FindPropertyRelative("radius").floatValue), EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }
        if (GUILayout.Button(new GUIContent("Add Relationship", "E.g. mushrooms require a tree within 4 units; rocks can't spawn within 2 units of a tree.")))
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("otherTag").stringValue = "";
            added.FindPropertyRelative("radius").floatValue = 5f;
            added.FindPropertyRelative("strength").floatValue = 0.5f;
        }
        EditorGUI.indentLevel--;
    }

    private static string RelationSentence(RelationKind kind, string self, string other, float radius)
    {
        if (string.IsNullOrEmpty(other))
            other = "(choose an object)";
        switch (kind)
        {
            case RelationKind.CannotSpawnNear: return $"{self} never spawns within {radius:0.#} of {other}.";
            case RelationKind.PreventsNearby: return $"{other} never spawns within {radius:0.#} of {self}.";
            case RelationKind.Avoids: return $"{self} is less likely within {radius:0.#} of {other}.";
            case RelationKind.AttractedTo: return $"{self} is more likely within {radius:0.#} of {other}.";
            default: return $"{self} only spawns within {radius:0.#} of {other} (in the same chunk).";
        }
    }

    private void DrawOrientationGroup(SerializedProperty obj, string key)
    {
        if (!Foldout(key + "/orientation", "Orientation & Scale", false, "Rotation mode, tilt with the ground, random turn and size."))
            return;
        EditorGUI.indentLevel++;
        SerializedProperty r = obj.FindPropertyRelative("orientation");
        Rel(r, "mode", "Rotation");
        var mode = (OrientationMode)r.FindPropertyRelative("mode").enumValueIndex;
        if (mode != OrientationMode.Upright && mode != OrientationMode.FullyRandom)
        {
            Rel(r, "terrainAlignment", "Follow Ground Tilt");
            Rel(r, "maxTilt", "Max Tilt");
        }
        if (mode != OrientationMode.FullyRandom)
        {
            Rel(r, "yawJitter", mode == OrientationMode.RandomYaw || mode == OrientationMode.Upright ? "Random Turn" : "Turn Variation");
            Rel(r, "randomTilt", "Random Extra Tilt");
        }
        Rel(r, "maxNormalDeviation", "Max Angle From Ground Normal");
        Rel(obj, "scaleRange", "Scale Range (min, max)");
        EditorGUI.indentLevel--;
    }

    private void DrawGroundGroup(SerializedProperty obj, string key, BiomeObject def)
    {
        if (!Foldout(key + "/ground", "Ground Contact", false, "How it sits on the ground: anchor point, footprint, allowed sinking/floating."))
            return;
        EditorGUI.indentLevel++;
        SerializedProperty g = obj.FindPropertyRelative("ground");
        Rel(g, "anchor", "Anchor");
        if (g.FindPropertyRelative("anchor").enumValueIndex == (int)GroundAnchor.CustomHeight)
            Rel(g, "anchorHeight", "Anchor Height");
        Rel(g, "footprint", "Footprint");
        if (g.FindPropertyRelative("footprint").enumValueIndex == (int)FootprintSource.CustomRadius)
            Rel(g, "customRadius", "Radius");
        else
            Rel(g, "footprintScale", "Footprint Scale");
        Rel(g, "sinkDepth", "Sink Into Ground");
        Rel(g, "maxPenetration", "Max Buried");
        Rel(g, "maxFloating", "Max Floating");
        Rel(g, "minValidFootprint", "Valid Footprint Share");
        Rel(g, "verticalOffset", "Height Offset");
        Rel(g, "verifyAfterSpawn", "Verify After Spawn");
        float tolerance = def.ground.maxPenetration + def.ground.sinkDepth + def.ground.maxFloating;
        EditorGUILayout.LabelField($"   Ground under the footprint may vary by up to {tolerance:0.##} units.", EditorStyles.miniLabel);
        EditorGUI.indentLevel--;
    }

    private void DrawLimitGroup(SerializedProperty obj, string key)
    {
        if (!Foldout(key + "/limits", "Limits & Landmarks", false, "Per-region limits, and rare landmark placement (best spots per region, guaranteed, unique)."))
            return;
        EditorGUI.indentLevel++;
        SerializedProperty l = obj.FindPropertyRelative("limits");
        Rel(l, "mode", "Placement Mode");
        bool landmark = l.FindPropertyRelative("mode").enumValueIndex == (int)PlacementMode.Landmark;
        Rel(l, "regionSize", "Region Size");
        Rel(l, "maxPerRegion", landmark ? "Per Region (0 = 1)" : "Max Per Region (0 = any)");
        if (landmark)
        {
            Rel(l, "minDistanceBetween", "Min Distance Between");
            Rel(l, "guaranteed", "Guaranteed");
            Rel(l, "unique", "Unique In World");
            if (l.FindPropertyRelative("unique").boolValue)
            {
                EditorGUI.indentLevel++;
                Rel(l, "uniqueSearchCenter", "Search Center");
                Rel(l, "uniqueSearchRadius", "Search Radius");
                EditorGUI.indentLevel--;
            }
            Rel(l, "candidatesPerRegion", "Spots Tried Per Region");
            EditorGUILayout.HelpBox("Landmarks are chosen from the world's rough shape, then confirmed on the chunk's exact terrain. Their spots are known before " +
                                    "the chunks exist, so a quest or map marker can point at them.", MessageType.None);
        }
        EditorGUI.indentLevel--;
    }

    // ------------------------------------------------------------------ field helpers

    private static void Rel(SerializedProperty parent, string name, string label)
    {
        SerializedProperty p = parent.FindPropertyRelative(name);
        if (p != null)
            EditorGUILayout.PropertyField(p, new GUIContent(label, p.tooltip), true);
    }

    /// <summary>A toggle and, when on, what it enables (indented).</summary>
    private static void Toggled(SerializedProperty parent, string toggle, string label, Action drawWhenOn)
    {
        Rel(parent, toggle, label);
        SerializedProperty p = parent.FindPropertyRelative(toggle);
        if (p != null && p.boolValue)
        {
            EditorGUI.indentLevel++;
            drawWhenOn();
            EditorGUI.indentLevel--;
        }
    }

    /// <summary>A min and a max field on one row.</summary>
    private static void MinMax(SerializedProperty parent, string minName, string maxName, string label)
    {
        SerializedProperty min = parent.FindPropertyRelative(minName), max = parent.FindPropertyRelative(maxName);
        if (min == null || max == null)
            return;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(new GUIContent(label, min.tooltip + "\n" + max.tooltip));
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        EditorGUILayout.PropertyField(min, GUIContent.none, GUILayout.MinWidth(40));
        GUILayout.Label("to", GUILayout.Width(16));
        EditorGUILayout.PropertyField(max, GUIContent.none, GUILayout.MinWidth(40));
        EditorGUI.indentLevel = indent;
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawChildren(SerializedProperty parent)
    {
        SerializedProperty it = parent.Copy(), end = parent.GetEndProperty();
        if (!it.NextVisible(true))
            return;
        while (!SerializedProperty.EqualContents(it, end))
        {
            EditorGUILayout.PropertyField(it, new GUIContent(ObjectNames.NicifyVariableName(it.name), it.tooltip), true);
            if (!it.NextVisible(false))
                break;
        }
    }

    /// <summary>A multi-select of the generator's biomes, stored in a Biome list.</summary>
    private static void BiomeMask(TerrainGenerator generator, SerializedProperty list, string label, Biome exclude)
    {
        var biomes = new List<Biome>();
        if (generator.BiomeDefinitions != null)
            foreach (BiomeInstance instance in generator.BiomeDefinitions)
                if (instance?.BiomePrefab != null && instance.BiomePrefab != exclude && !biomes.Contains(instance.BiomePrefab) && biomes.Count < 31)
                    biomes.Add(instance.BiomePrefab);

        int mask = 0;
        for (int i = 0; i < list.arraySize; i++)
        {
            int index = biomes.IndexOf(list.GetArrayElementAtIndex(i).objectReferenceValue as Biome);
            if (index >= 0)
                mask |= 1 << index;
        }
        int updated = EditorGUILayout.MaskField(new GUIContent(label, list.tooltip), mask, biomes.Select(x => x.name).ToArray());
        if (updated == mask)
            return;
        list.ClearArray();
        for (int i = 0; i < biomes.Count; i++)
        {
            if ((updated & (1 << i)) == 0)
                continue;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = biomes[i];
        }
    }

    // ------------------------------------------------------------------ menus and changes

    /// <summary>Applies a change straight to the generator's objects, with undo, outside the serialized properties.</summary>
    private void ChangeGenerator(TerrainGenerator generator, string undoName, Action<TerrainGenerator> change)
    {
        serializedObject.ApplyModifiedProperties();
        Undo.RecordObject(generator, undoName);
        change(generator);
        EditorUtility.SetDirty(generator);
        serializedObject.Update();
        Repaint();
    }

    private void ShowAddObjectMenu(TerrainGenerator generator, int b)
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Empty"), false, () => ChangeGenerator(generator, "Add Biome Object", g => g.BiomeDefinitions[b].runtimeObjects.Add(new BiomeObject())));
        menu.AddSeparator("");
        foreach (ObjectPreset preset in ObjectPresets.All)
        {
            ObjectPreset chosen = preset;
            menu.AddItem(new GUIContent(preset.Menu), false, () => ChangeGenerator(generator, "Add " + chosen.Name, g =>
            {
                var created = new BiomeObject();
                chosen.Apply(created);
                g.BiomeDefinitions[b].runtimeObjects.Add(created);
                ObjectFoldouts[$"obj/{b}/{g.BiomeDefinitions[b].runtimeObjects.Count - 1}"] = true;
            }));
        }
        menu.ShowAsContext();
    }

    private void ShowPresetMenu(TerrainGenerator generator, int b, int o)
    {
        var menu = new GenericMenu();
        foreach (ObjectPreset preset in ObjectPresets.All)
        {
            ObjectPreset chosen = preset;
            menu.AddItem(new GUIContent(preset.Menu), false, () => ChangeGenerator(generator, "Apply " + chosen.Name + " Preset", g =>
            {
                BiomeObject old = g.BiomeDefinitions[b].runtimeObjects[o];
                var fresh = new BiomeObject { terrainObject = old.terrainObject, groupTag = old.groupTag };
                chosen.Apply(fresh);
                g.BiomeDefinitions[b].runtimeObjects[o] = fresh;
            }));
        }
        menu.ShowAsContext();
    }

    private void ShowObjectMenu(TerrainGenerator generator, int b, int o)
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Copy"), false, () => copiedObjectJson = EditorJsonUtility.ToJson(generator.BiomeDefinitions[b].runtimeObjects[o]));
        if (!string.IsNullOrEmpty(copiedObjectJson))
        {
            menu.AddItem(new GUIContent("Paste Rules (keep prefab and tag)"), false, () => ChangeGenerator(generator, "Paste Object Rules", g =>
            {
                BiomeObject target = g.BiomeDefinitions[b].runtimeObjects[o];
                GameObject prefab = target.terrainObject;
                string tag = target.groupTag;
                EditorJsonUtility.FromJsonOverwrite(copiedObjectJson, target);
                target.terrainObject = prefab;
                target.groupTag = tag;
            }));
        }
        else
        {
            menu.AddDisabledItem(new GUIContent("Paste Rules (keep prefab and tag)"));
        }
        menu.AddItem(new GUIContent("Duplicate"), false, () => ChangeGenerator(generator, "Duplicate Biome Object", g =>
        {
            var copy = new BiomeObject();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(g.BiomeDefinitions[b].runtimeObjects[o]), copy);
            g.BiomeDefinitions[b].runtimeObjects.Insert(o + 1, copy);
        }));
        menu.AddSeparator("");
        if (o > 0)
            menu.AddItem(new GUIContent("Move Up"), false, () => ChangeGenerator(generator, "Move Biome Object", g => Swap(g.BiomeDefinitions[b].runtimeObjects, o, o - 1)));
        if (o < generator.BiomeDefinitions[b].runtimeObjects.Count - 1)
            menu.AddItem(new GUIContent("Move Down"), false, () => ChangeGenerator(generator, "Move Biome Object", g => Swap(g.BiomeDefinitions[b].runtimeObjects, o, o + 1)));
        for (int other = 0; other < generator.BiomeDefinitions.Length; other++)
        {
            if (other == b || generator.BiomeDefinitions[other]?.BiomePrefab == null)
                continue;
            int to = other;
            menu.AddItem(new GUIContent("Move To Biome/" + generator.BiomeDefinitions[other].BiomePrefab.name), false, () => ChangeGenerator(generator, "Move Biome Object", g =>
            {
                BiomeObject moved = g.BiomeDefinitions[b].runtimeObjects[o];
                g.BiomeDefinitions[b].runtimeObjects.RemoveAt(o);
                g.BiomeDefinitions[to].runtimeObjects.Add(moved);
            }));
        }
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Remove"), false, () => ChangeGenerator(generator, "Remove Biome Object", g => g.BiomeDefinitions[b].runtimeObjects.RemoveAt(o)));
        menu.ShowAsContext();
    }

    private static void Swap(List<BiomeObject> list, int a, int b)
    {
        BiomeObject t = list[a];
        list[a] = list[b];
        list[b] = t;
    }

    // ------------------------------------------------------------------ auto-configure

    /// <summary>Group Tag, footprint, tilt and ground-contact limits from the prefab's measured shape.</summary>
    private static void AutoConfigure(BiomeObject def)
    {
        GameObject prefab = def.terrainObject;
        if (prefab == null)
            return;
        if (string.IsNullOrEmpty(def.groupTag))
            def.groupTag = prefab.name;

        PrefabShapeCache.Clear();   // measure again, in case the prefab changed
        PrefabShape shape = PrefabShapeCache.Get(prefab);
        if (!shape.TryGetSizeBox(out Vector3 min, out Vector3 max))
            return;
        Vector3 size = Vector3.Scale(max - min, shape.RootScale);
        float width = Mathf.Max(size.x, size.z), height = size.y;

        bool tall = height > 2f * width;
        bool flat = height < 0.35f * width;
        bool large = width > 4f;

        // What rests on the ground.
        def.ground.anchor = GroundAnchor.BoundsBottom;
        if (shape.HasBase)
        {
            def.ground.footprint = FootprintSource.Automatic;
            def.ground.footprintScale = 1f;
        }
        else if (tall && shape.HasColliders && (shape.ColliderMax.x - shape.ColliderMin.x) < 0.6f * (max.x - min.x))
        {
            def.ground.footprint = FootprintSource.Colliders;
            def.ground.footprintScale = 1f;
        }
        else
        {
            def.ground.footprint = FootprintSource.Automatic;
            def.ground.footprintScale = tall ? 0.25f : 1f;
        }

        // Tilt and ground contact by shape.
        if (tall)
        {
            // Trees, poles, pillars: stay upright.
            def.orientation.mode = OrientationMode.RandomYaw;
            def.orientation.terrainAlignment = 0.1f;
            def.orientation.maxTilt = 8f;
            def.ground.sinkDepth = 0.1f;
            def.ground.maxPenetration = 0.4f;
            def.ground.maxFloating = 0.1f;
        }
        else if (flat)
        {
            // Rugs, lily pads, debris: lie on the ground.
            def.orientation.mode = OrientationMode.RandomYaw;
            def.orientation.terrainAlignment = 1f;
            def.orientation.maxTilt = 90f;
            def.ground.sinkDepth = 0.02f;
            def.ground.maxPenetration = 0.15f;
            def.ground.maxFloating = 0.05f;
        }
        else
        {
            // Rocks, bushes, stumps: follow the ground.
            def.orientation.mode = OrientationMode.RandomYaw;
            def.orientation.terrainAlignment = 0.7f;
            def.orientation.maxTilt = 35f;
            def.ground.sinkDepth = Mathf.Clamp(height * 0.08f, 0.03f, 0.4f);
            def.ground.maxPenetration = Mathf.Clamp(height * 0.3f, 0.2f, 1.5f);
            def.ground.maxFloating = 0.1f;
        }
        if (large)
        {
            def.ground.maxFloating = Mathf.Max(def.ground.maxFloating, 0.15f);
            def.biomes.footprintInBiome = true;
        }
        def.ground.verifyAfterSpawn = height > 0.5f;
        def.minSpacing = 0f;   // worked out from the measured size
    }

    // ------------------------------------------------------------------ test placement

    private void RunPlacementTest(TerrainGenerator generator)
    {
        TestResults.Clear();
        testSummary = null;
        try
        {
            EditorUtility.DisplayCancelableProgressBar("Test Placement", "Generating a chunk", 0.2f);
            Vector3 focus = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            int span = generator.ChunkSize - 1;
            int cx = Mathf.FloorToInt(focus.x / span), cz = Mathf.FloorToInt(focus.z / span);
            var offset = new Vector2(cx * span, cz * span);

            generator.RefreshGenerationCaches();
            float[,] heights = HeightGenerator.GenerateHeightMap(generator, offset, true, out _, out WaterMapData water, out PlacementFields fields);
            EditorUtility.DisplayCancelableProgressBar("Test Placement", "Finding biomes", 0.6f);
            Biome[,] biomeMap = generator.GenerateBiomeMap(offset, heights);
            EditorUtility.DisplayCancelableProgressBar("Test Placement", "Placing objects", 0.8f);
            PlacementPlan plan = generator.GetPlacementPlan();
            var input = new ObjectPlacementEngine.ChunkInput
            {
                OriginX = Mathf.RoundToInt(offset.x),
                OriginY = Mathf.RoundToInt(offset.y),
                Span = span,
                HeightMap = heights,
                BiomeMap = biomeMap,
                Water = water,
                Fields = fields,
                LodFactor = generator.LevelOfDetail > 0 ? generator.LevelOfDetail * 2 : 1,
            };
            var watch = System.Diagnostics.Stopwatch.StartNew();
            PlacementResult result = ObjectPlacementEngine.Place(plan, input);
            long ms = watch.ElapsedMilliseconds;

            if (plan.Types.Length == 0 || result.Tried == null)
            {
                testSummary = "Nothing to place: Should Spawn Objects is off, or no object has a prefab and a chance above 0.";
                return;
            }
            for (int t = 0; t < plan.Types.Length; t++)
            {
                TestResults[plan.Types[t].Def] = new ObjectTestResult
                {
                    Placed = result.Accepted[t],
                    Tried = result.Tried[t],
                    Rejected = result.Rejected[t],
                };
            }
            var biomeCounts = new Dictionary<string, int>();
            foreach (Biome biome in biomeMap)
            {
                string n = biome != null ? biome.name : "(none)";
                biomeCounts.TryGetValue(n, out int c);
                biomeCounts[n] = c + 1;
            }
            int cells = biomeMap.Length;
            testSummary = $"Test chunk ({cx}, {cz}) at world ({offset.x:0}, {offset.y:0}): {result.Objects.Count} objects placed in {ms} ms.\nBiomes there: " +
                          string.Join(", ", biomeCounts.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {100f * p.Value / cells:0}%")) +
                          "\nEach object's box shows its own result. Move the Scene view to test another place.";
            foreach (string warning in plan.Warnings)
                testSummary += "\n" + warning;
        }
        catch (Exception e)
        {
            testSummary = "Test failed: " + e.Message;
            Debug.LogException(e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
}
