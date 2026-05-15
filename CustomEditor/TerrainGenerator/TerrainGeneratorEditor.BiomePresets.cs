using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;

// TerrainGeneratorEditor: biome presets and saving/loading a backup of a biome's settings (see TerrainGeneratorEditor.cs).
public partial class TerrainGeneratorEditor : Editor
{
    // --- Biome presets: recommended starting points for common archetypes, covering only the
    // scale-independent fields (climate niche, erosion, noise shape). Weight and Min/Max Height are left
    // untouched since those depend on how this biome is balanced against the others in THIS project. ---

    private struct BiomePresetDefinition
    {
        public string Name;
        public float IdealTemperature, IdealMoisture, TemperatureTolerance, MoistureTolerance;
        public float ErosionResistance, RainfallErosionMultiplier;
        public float Amplitude, Frequency, Persistence;
        public LandformType Landform;
        public BiomePlacement Placement;
    }

    private static readonly BiomePresetDefinition[] BiomePresets =
    {
        new BiomePresetDefinition { Name = "Mountain",        IdealTemperature = 0.35f, IdealMoisture = 0.45f, TemperatureTolerance = 0.35f, MoistureTolerance = 0.40f, ErosionResistance = 0.85f, RainfallErosionMultiplier = 0.8f, Amplitude = 60f, Frequency = 1.5f, Persistence = 0.50f, Landform = LandformType.Mountains },
        new BiomePresetDefinition { Name = "Tundra",          IdealTemperature = 0.08f, IdealMoisture = 0.35f, TemperatureTolerance = 0.20f, MoistureTolerance = 0.30f, ErosionResistance = 0.55f, RainfallErosionMultiplier = 0.6f, Amplitude = 8f,  Frequency = 1.2f, Persistence = 0.45f, Landform = LandformType.Plains },
        new BiomePresetDefinition { Name = "Grassland",       IdealTemperature = 0.55f, IdealMoisture = 0.45f, TemperatureTolerance = 0.30f, MoistureTolerance = 0.30f, ErosionResistance = 0.35f, RainfallErosionMultiplier = 1.0f, Amplitude = 4f,  Frequency = 1.0f, Persistence = 0.40f, Landform = LandformType.Plains },
        new BiomePresetDefinition { Name = "Forest",          IdealTemperature = 0.50f, IdealMoisture = 0.60f, TemperatureTolerance = 0.30f, MoistureTolerance = 0.30f, ErosionResistance = 0.40f, RainfallErosionMultiplier = 1.1f, Amplitude = 10f, Frequency = 1.3f, Persistence = 0.45f, Landform = LandformType.Hills },
        new BiomePresetDefinition { Name = "Desert",          IdealTemperature = 0.85f, IdealMoisture = 0.10f, TemperatureTolerance = 0.25f, MoistureTolerance = 0.20f, ErosionResistance = 0.20f, RainfallErosionMultiplier = 0.3f, Amplitude = 12f, Frequency = 0.8f, Persistence = 0.35f, Landform = LandformType.Dunes },
        new BiomePresetDefinition { Name = "Swamp",           IdealTemperature = 0.60f, IdealMoisture = 0.90f, TemperatureTolerance = 0.30f, MoistureTolerance = 0.25f, ErosionResistance = 0.25f, RainfallErosionMultiplier = 1.4f, Amplitude = 2f,  Frequency = 1.0f, Persistence = 0.30f, Landform = LandformType.Wetland },
        new BiomePresetDefinition { Name = "Jungle",          IdealTemperature = 0.85f, IdealMoisture = 0.85f, TemperatureTolerance = 0.25f, MoistureTolerance = 0.25f, ErosionResistance = 0.35f, RainfallErosionMultiplier = 1.6f, Amplitude = 14f, Frequency = 1.4f, Persistence = 0.50f, Landform = LandformType.Hills },
        new BiomePresetDefinition { Name = "Beach / Coastal", IdealTemperature = 0.65f, IdealMoisture = 0.55f, TemperatureTolerance = 0.35f, MoistureTolerance = 0.35f, ErosionResistance = 0.15f, RainfallErosionMultiplier = 1.2f, Amplitude = 3f,  Frequency = 0.9f, Persistence = 0.30f, Landform = LandformType.Plains },
        new BiomePresetDefinition { Name = "Highland Forest",  IdealTemperature = 0.45f, IdealMoisture = 0.65f, TemperatureTolerance = 0.30f, MoistureTolerance = 0.30f, ErosionResistance = 0.50f, RainfallErosionMultiplier = 1.1f, Amplitude = 16f, Frequency = 1.0f, Persistence = 0.45f, Landform = LandformType.Highlands },
        new BiomePresetDefinition { Name = "Glacial Valleys",  IdealTemperature = 0.10f, IdealMoisture = 0.45f, TemperatureTolerance = 0.20f, MoistureTolerance = 0.35f, ErosionResistance = 0.80f, RainfallErosionMultiplier = 0.7f, Amplitude = 55f, Frequency = 1.2f, Persistence = 0.50f, Landform = LandformType.Glacial },
        new BiomePresetDefinition { Name = "Ocean - Deep Plain", IdealTemperature = 0.50f, IdealMoisture = 0.50f, TemperatureTolerance = 0.50f, MoistureTolerance = 0.50f, ErosionResistance = 0.50f, RainfallErosionMultiplier = 1.0f, Amplitude = 10f, Frequency = 0.8f, Persistence = 0.50f, Landform = LandformType.SeaPlain, Placement = BiomePlacement.Ocean },
        new BiomePresetDefinition { Name = "Ocean - Ravines", IdealTemperature = 0.40f, IdealMoisture = 0.50f, TemperatureTolerance = 0.50f, MoistureTolerance = 0.50f, ErosionResistance = 0.50f, RainfallErosionMultiplier = 1.0f, Amplitude = 22f, Frequency = 0.8f, Persistence = 0.50f, Landform = LandformType.SeaRavines, Placement = BiomePlacement.Ocean },
        new BiomePresetDefinition { Name = "Ocean - Coral Reef", IdealTemperature = 0.85f, IdealMoisture = 0.50f, TemperatureTolerance = 0.25f, MoistureTolerance = 0.50f, ErosionResistance = 0.50f, RainfallErosionMultiplier = 1.0f, Amplitude = 30f, Frequency = 0.8f, Persistence = 0.50f, Landform = LandformType.SeaReef, Placement = BiomePlacement.Ocean },
        new BiomePresetDefinition { Name = "Ocean - Rocky Seabed", IdealTemperature = 0.30f, IdealMoisture = 0.50f, TemperatureTolerance = 0.50f, MoistureTolerance = 0.50f, ErosionResistance = 0.50f, RainfallErosionMultiplier = 1.0f, Amplitude = 8f, Frequency = 1.2f, Persistence = 0.50f, Landform = LandformType.SeaRocky, Placement = BiomePlacement.Ocean },
        new BiomePresetDefinition { Name = "Volcanic", IdealTemperature = 0.60f, IdealMoisture = 0.30f, TemperatureTolerance = 0.50f, MoistureTolerance = 0.50f, ErosionResistance = 0.90f, RainfallErosionMultiplier = 0.5f, Amplitude = 10f, Frequency = 1.0f, Persistence = 0.50f, Landform = LandformType.Plains, Placement = BiomePlacement.Volcanic },
    };

    private static void ShowBiomePresetMenu(Biome biome)
    {
        GenericMenu menu = new GenericMenu();
        foreach (BiomePresetDefinition preset in BiomePresets)
        {
            BiomePresetDefinition capturedPreset = preset;
            menu.AddItem(new GUIContent(capturedPreset.Name), false, () => ApplyBiomePreset(biome, capturedPreset));
        }
        menu.ShowAsContext();
    }

    private static void ApplyBiomePreset(Biome biome, BiomePresetDefinition preset)
    {
        Undo.RecordObject(biome, $"Apply {preset.Name} Biome Preset");
        biome.idealTemperature = preset.IdealTemperature;
        biome.idealMoisture = preset.IdealMoisture;
        biome.temperatureTolerance = preset.TemperatureTolerance;
        biome.moistureTolerance = preset.MoistureTolerance;
        biome.erosionResistance = preset.ErosionResistance;
        biome.rainfallErosionMultiplier = preset.RainfallErosionMultiplier;
        biome.amplitude = preset.Amplitude;
        biome.frequency = preset.Frequency;
        biome.persistence = preset.Persistence;
        biome.landform = preset.Landform;
        biome.placement = preset.Placement;
        EditorUtility.SetDirty(biome);
        Debug.Log($"[TerrainGenerator] Applied '{preset.Name}' preset to biome '{biome.name}'. Amplitude/Frequency are relative starting points - rescale them to match your world's overall height scale.");
    }

    // --- Save/Load a full backup of a biome's tunable fields, as JSON under Assets/BiomeBackups/. Keyed
    // by the biome asset's GUID (falling back to its name) so a rename doesn't orphan its backup. ---

    [System.Serializable]
    private class BiomeStateSnapshot
    {
        public float minHeight, maxHeight, amplitude, frequency, weight, persistence;
        public float idealTemperature, idealMoisture, temperatureTolerance, moistureTolerance;
        public float erosionResistance, rainfallErosionMultiplier;
        // Added with the water system; version 0 = older backup without them (left untouched on load).
        public int version;
        public float baseElevation;
        public bool allowsWaterBodies;
        public float lakeLikelihood, pondLikelihood, riverSpringLikelihood;
        // Version 2 adds the landform.
        public int landform;
        // Version 3 adds the placement role.
        public int placement;
    }

    private const string BiomeBackupFolder = "Assets/BiomeBackups";

    private static string GetBiomeBackupPath(Biome biome)
    {
        string assetPath = AssetDatabase.GetAssetPath(biome);
        string guid = !string.IsNullOrEmpty(assetPath) ? AssetDatabase.AssetPathToGUID(assetPath) : null;
        string safeName = string.IsNullOrEmpty(biome.name) ? "UnnamedBiome" : biome.name;
        string fileKey = !string.IsNullOrEmpty(guid) ? guid : safeName;
        return $"{BiomeBackupFolder}/{safeName}_{fileKey}.json";
    }

    private static bool HasSavedBiomeState(Biome biome)
    {
        return biome != null && File.Exists(GetBiomeBackupPath(biome));
    }

    private static void SaveBiomeState(Biome biome)
    {
        if (!AssetDatabase.IsValidFolder(BiomeBackupFolder))
        {
            AssetDatabase.CreateFolder("Assets", "BiomeBackups");
        }

        BiomeStateSnapshot snapshot = new BiomeStateSnapshot
        {
            minHeight = biome.minHeight,
            maxHeight = biome.maxHeight,
            amplitude = biome.amplitude,
            frequency = biome.frequency,
            weight = biome.weight,
            persistence = biome.persistence,
            idealTemperature = biome.idealTemperature,
            idealMoisture = biome.idealMoisture,
            temperatureTolerance = biome.temperatureTolerance,
            moistureTolerance = biome.moistureTolerance,
            erosionResistance = biome.erosionResistance,
            rainfallErosionMultiplier = biome.rainfallErosionMultiplier,
            version = 3,
            landform = (int)biome.landform,
            placement = (int)biome.placement,
            baseElevation = biome.baseElevation,
            allowsWaterBodies = biome.allowsWaterBodies,
            lakeLikelihood = biome.lakeLikelihood,
            pondLikelihood = biome.pondLikelihood,
            riverSpringLikelihood = biome.riverSpringLikelihood
        };

        string path = GetBiomeBackupPath(biome);
        File.WriteAllText(path, JsonUtility.ToJson(snapshot, true));
        AssetDatabase.ImportAsset(path);
        Debug.Log($"[TerrainGenerator] Saved biome state for '{biome.name}' to {path}");
    }

    private static void LoadBiomeState(Biome biome)
    {
        string path = GetBiomeBackupPath(biome);
        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog("No Saved State", $"No saved state found for '{biome.name}'. Use Save State first.", "OK");
            return;
        }

        BiomeStateSnapshot snapshot = JsonUtility.FromJson<BiomeStateSnapshot>(File.ReadAllText(path));
        if (snapshot == null)
        {
            Debug.LogError($"[TerrainGenerator] Failed to parse saved state at {path}");
            return;
        }

        Undo.RecordObject(biome, "Load Biome State");
        biome.minHeight = snapshot.minHeight;
        biome.maxHeight = snapshot.maxHeight;
        biome.amplitude = snapshot.amplitude;
        biome.frequency = snapshot.frequency;
        biome.weight = snapshot.weight;
        biome.persistence = snapshot.persistence;
        biome.idealTemperature = snapshot.idealTemperature;
        biome.idealMoisture = snapshot.idealMoisture;
        biome.temperatureTolerance = snapshot.temperatureTolerance;
        biome.moistureTolerance = snapshot.moistureTolerance;
        biome.erosionResistance = snapshot.erosionResistance;
        biome.rainfallErosionMultiplier = snapshot.rainfallErosionMultiplier;
        if (snapshot.version >= 1)
        {
            biome.baseElevation = snapshot.baseElevation;
            biome.allowsWaterBodies = snapshot.allowsWaterBodies;
            biome.lakeLikelihood = snapshot.lakeLikelihood;
            biome.pondLikelihood = snapshot.pondLikelihood;
            biome.riverSpringLikelihood = snapshot.riverSpringLikelihood;
        }
        if (snapshot.version >= 2)
        {
            biome.landform = (LandformType)snapshot.landform;
        }
        if (snapshot.version >= 3)
        {
            biome.placement = (BiomePlacement)snapshot.placement;
        }
        EditorUtility.SetDirty(biome);
        Debug.Log($"[TerrainGenerator] Loaded biome state for '{biome.name}' from {path}");
    }
}
