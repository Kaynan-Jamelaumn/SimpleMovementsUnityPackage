#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the power abilities (Gigantification, Minimization, Shockwave Leap, Mirage, Greater Mirage, Immovable Force,
/// Vanish, Sky Walk...) as assets in Resources/SimpleMovements/Abilities/Powers. Existing ones are left as they are.
/// </summary>
public static class PowerAbilityGenerator
{
    /// <summary>Every preset the generator creates.</summary>
    public static System.Collections.Generic.IEnumerable<AbilityPresets.Preset> Presets =>
        System.Linq.Enumerable.Concat(PowerAbilityPresets.All, TricksterAbilityPresets.All);

    /// <summary>The power ability asset called <paramref name="name"/>, generating the missing ones first.</summary>
    public static AbilityDefinition GetOrCreate(string name)
    {
        string path = $"{SMAssetsEditor.Folder($"{SMAssets.Abilities}/Powers")}/{SMAssetsEditor.CleanFileName(name)}.asset";
        var a = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
        if (a == null)
        {
            Generate(false);
            a = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
        }
        return a;
    }

    /// <summary>Creates the "Fate & Tricks" trait presets (Fate's Debt, Acid Blood, Born Gambler, Skywalker...) as Trait assets.</summary>
    [MenuItem(SMMenu.Root + "Player/Generate Power Traits", false, SMMenu.Player + 31)]
    private static void GenerateTraits()
    {
        string folder = SMAssetsEditor.Folder($"{SMAssets.Traits}/Powers");
        int created = 0, skipped = 0;
        foreach (TraitPresets.Preset p in TraitPresets.All)
        {
            if (p.category != "Fate & Tricks")
                continue;
            string path = $"{folder}/{p.name}.asset";
            Trait existing = AssetDatabase.LoadAssetAtPath<Trait>(path);
            if (existing != null && !HasLostAbility(existing))
            {
                skipped++;
                continue;
            }
            if (existing != null)
                AssetDatabase.DeleteAsset(path); // made by an earlier version whose ability could not be saved: made again
            TraitPresets.CreateAsset(p, folder);
            created++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Powers] Created {created} traits in {folder} ({skipped} already there). Add them to the Trait Database to offer them at character creation.");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(folder);
    }

    /// <summary>A trait whose ability on a key is missing (not saved as an asset).</summary>
    private static bool HasLostAbility(Trait t)
    {
        foreach (TraitBehaviour b in t.behaviours)
            if (b is ActiveAbilityTrait a && (a.ability == null || !EditorUtility.IsPersistent(a.ability)))
                return true;
        return false;
    }

    [MenuItem(SMMenu.Root + "Player/Generate Power Abilities", false, SMMenu.Player + 30)]
    private static void GenerateMenu() => Generate(true);

    public static void Generate(bool select)
    {
        string folder = SMAssetsEditor.Folder($"{SMAssets.Abilities}/Powers");
        int created = 0, skipped = 0;
        var log = new System.Text.StringBuilder();
        foreach (AbilityPresets.Preset p in Presets)
        {
            string path = $"{folder}/{SMAssetsEditor.CleanFileName(p.name)}.asset";
            if (AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path) != null)
            {
                skipped++;
                continue;
            }
            AbilityDefinition def = p.create();
            AssetDatabase.CreateAsset(def, path);
            def.SetId(AssetDatabase.AssetPathToGUID(path));
            EditorUtility.SetDirty(def);
            created++;
            log.AppendLine($"  {p.name}: {p.description}");
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Powers] Created {created} power abilities in {folder} ({skipped} already there).\n{log}");
        if (!select)
            return;
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(folder);
    }
}
#endif
