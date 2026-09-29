#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Menu commands for the ability system (Tools > Abilities and Assets > Create).</summary>
public static class AbilityToolsMenu
{
    private const string ResourcesFolder = "Assets/Resources";

    // ------------------------------------------------------------------ legacy conversion
    [MenuItem("Tools/Abilities/Convert Selected Legacy Abilities/For Player")]
    private static void ConvertForPlayer() => ConvertSelected(false);

    [MenuItem("Tools/Abilities/Convert Selected Legacy Abilities/For Mobs")]
    private static void ConvertForMobs() => ConvertSelected(true);

    [MenuItem("Tools/Abilities/Convert Selected Legacy Abilities/For Player", true)]
    [MenuItem("Tools/Abilities/Convert Selected Legacy Abilities/For Mobs", true)]
    private static bool CanConvert()
    {
        foreach (Object o in Selection.objects)
            if (o is AbilityEffectSO) return true;
        return false;
    }

    private static void ConvertSelected(bool forMob)
    {
        var created = new List<Object>();
        foreach (Object o in Selection.objects)
        {
            if (o is AbilityEffectSO legacy)
            {
                AbilityDefinition def = ConvertToAsset(legacy, forMob);
                if (def != null)
                    created.Add(def);
            }
        }
        if (created.Count > 0)
        {
            Selection.objects = created.ToArray();
            Debug.Log($"Converted {created.Count} legacy abilit{(created.Count == 1 ? "y" : "ies")} to Ability Definitions ({(forMob ? "mob" : "player")} targeting).");
        }
    }

    /// <summary>Saves a converted copy of a legacy ability next to it and returns it.</summary>
    public static AbilityDefinition ConvertToAsset(AbilityEffectSO legacy, bool forMob) => AbilityToolsMenuBridge.Convert(legacy, forMob);

    // ------------------------------------------------------------------ settings assets
    [MenuItem("Tools/Abilities/Create Combat Settings (Resources)")]
    private static void CreateCombatSettings() => CreateInResources<CombatSettings>("CombatSettings");

    [MenuItem("Tools/Abilities/Create Absorption Settings (Resources)")]
    private static void CreateAbsorptionSettings() => CreateInResources<AbsorptionSettings>("AbsorptionSettings");

    [MenuItem("Tools/Abilities/Create Ability Database (Resources)")]
    private static void CreateDatabase()
    {
        AbilityDatabase db = CreateInResources<AbilityDatabase>("AbilityDatabase");
        if (db == null)
            return;
        foreach (string guid in AssetDatabase.FindAssets("t:AbilityDefinition"))
        {
            var a = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null && !db.abilities.Contains(a))
                db.abilities.Add(a);
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }

    private static T CreateInResources<T>(string assetName) where T : ScriptableObject
    {
        string path = $"{ResourcesFolder}/{assetName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return existing;
        }
        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            AssetDatabase.CreateFolder("Assets", "Resources");
        T asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        return asset;
    }

    // ------------------------------------------------------------------ validation
    [MenuItem("Tools/Abilities/Validate All Abilities")]
    private static void ValidateAll()
    {
        int count = 0, errorCount = 0, warningCount = 0;
        var errors = new List<string>();
        var warnings = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:AbilityDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var a = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            if (a == null)
                continue;
            count++;
            errors.Clear();
            warnings.Clear();
            a.Validate(errors, warnings);
            foreach (string e in errors) Debug.LogError($"{e}  ({path})", a);
            foreach (string w in warnings) Debug.LogWarning($"{w}  ({path})", a);
            errorCount += errors.Count;
            warningCount += warnings.Count;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:MobProfile"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var p = AssetDatabase.LoadAssetAtPath<MobProfile>(path);
            if (p == null)
                continue;
            errors.Clear();
            warnings.Clear();
            p.Validate(errors, warnings);
            foreach (string e in errors) Debug.LogError($"{e}  ({path})", p);
            foreach (string w in warnings) Debug.LogWarning($"{w}  ({path})", p);
            errorCount += errors.Count;
            warningCount += warnings.Count;
        }
        Debug.Log($"Validated {count} abilities and the mob profiles: {errorCount} errors, {warningCount} warnings.");
    }

    // ------------------------------------------------------------------ presets
    [MenuItem("Assets/Create/Scriptable Objects/Ability/Ability From Preset...", false, -9)]
    private static void CreateFromPreset()
    {
        string folder = SelectedFolder();
        var menu = new GenericMenu();
        foreach (AbilityPresets.Preset p in AbilityPresets.All)
        {
            AbilityPresets.Preset captured = p;
            menu.AddItem(new GUIContent(p.name), false, () =>
            {
                AbilityDefinition def = captured.create();
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{def.DisplayName}.asset");
                AssetDatabase.CreateAsset(def, path);
                def.SetId(AssetDatabase.AssetPathToGUID(path));
                EditorUtility.SetDirty(def);
                AssetDatabase.SaveAssets();
                Selection.activeObject = def;
            });
        }
        menu.ShowAsContext();
    }

    [MenuItem("Assets/Create/Scriptable Objects/Mob/Mob Profile From Preset...", false, 1)]
    private static void CreateMobProfilePreset()
    {
        string folder = SelectedFolder();
        var menu = new GenericMenu();
        foreach (MobProfile.Preset preset in System.Enum.GetValues(typeof(MobProfile.Preset)))
        {
            MobProfile.Preset captured = preset;
            menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(preset.ToString())), false, () =>
            {
                MobProfile p = MobProfile.CreatePreset(captured);
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{p.name}.asset");
                AssetDatabase.CreateAsset(p, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = p;
            });
        }
        menu.ShowAsContext();
    }

    private static string SelectedFolder()
    {
        string path = "Assets";
        foreach (Object o in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
        {
            string p = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(p))
                continue;
            path = AssetDatabase.IsValidFolder(p) ? p : Path.GetDirectoryName(p).Replace('\\', '/');
            break;
        }
        return path;
    }
}
#endif
