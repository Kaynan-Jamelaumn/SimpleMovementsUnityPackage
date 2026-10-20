using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the SimpleMovements tools keep the assets they generate, and how the game finds them.
/// <para>Everything generated goes under <c>Assets/Resources/SimpleMovements/</c>, one folder per kind of asset:</para>
/// <code>
/// Resources/SimpleMovements/
///   Settings/      Combat, Absorption, Hub World, Elemental, Injury settings
///   Databases/     Ability, Trait, Item and Item Category databases, Character Catalog
///   Economy/       Default currency
///   Characters/    Races/, Classes/, Traits/
///   Abilities/     ability definitions made from presets and conversions
///   Mobs/          one folder per mob type (profile, basic attack, abilities)
///   Combat/        Body Parts/
///   Items/         Weapons/, Armor/, Consumables/, Ammo/, Medical/, Limbs/, Drops/ (Item Drop Manager + Tables/)
///   UI/            HUD/, Inventory/
///   World/         Weather/, Hub World/, Biome Backups/
///   Dungeons/      one folder per dungeon setup
///   Networking/    NetWorldItem prefab, observer conditions
///   Generated/     anything else
/// </code>
/// Assets made before this layout (at the root of a Resources folder, in Assets/Generated...) are still found;
/// Tools ▸ SimpleMovements ▸ Project Setup ▸ Organise Generated Assets moves them here (references are kept).
/// </summary>
public static class SMAssets
{
    /// <summary>The folder inside Resources (Resources.Load paths start with it).</summary>
    public const string ResourcesRoot = "SimpleMovements";
    /// <summary>The project folder the tools write to.</summary>
    public const string ProjectRoot = "Assets/Resources/" + ResourcesRoot;

    // Folders, relative to the root.
    public const string Settings = "Settings";
    public const string Databases = "Databases";
    public const string Economy = "Economy";
    public const string Characters = "Characters";
    public const string Races = "Characters/Races";
    public const string Classes = "Characters/Classes";
    public const string Traits = "Characters/Traits";
    public const string Abilities = "Abilities";
    public const string Mobs = "Mobs";
    public const string Combat = "Combat";
    public const string BodyParts = "Combat/Body Parts";
    public const string Items = "Items";
    public const string Weapons = "Items/Weapons";
    public const string Armor = "Items/Armor";
    public const string Consumables = "Items/Consumables";
    public const string Ammo = "Items/Ammo";
    public const string Medical = "Items/Medical";
    public const string Limbs = "Items/Limbs";
    public const string ItemDrops = "Items/Drops";
    public const string DropTables = "Items/Drops/Tables";
    public const string UI = "UI";
    public const string HUD = "UI/HUD";
    public const string InventoryUI = "UI/Inventory";
    public const string World = "World";
    public const string Weather = "World/Weather";
    public const string HubWorld = "World/Hub World";
    public const string BiomeBackups = "World/Biome Backups";
    public const string Dungeons = "Dungeons";
    public const string Minigames = "Minigames";
    public const string Networking = "Networking";
    public const string Generated = "Generated";

    /// <summary>"Settings", "CombatSettings" → "SimpleMovements/Settings/CombatSettings" (for Resources.Load).</summary>
    public static string ResourcePath(string folder, string name) =>
        string.IsNullOrEmpty(folder) ? ResourcesRoot + "/" + name : ResourcesRoot + "/" + folder + "/" + name;

    /// <summary>"Settings" → "Assets/Resources/SimpleMovements/Settings".</summary>
    public static string FolderPath(string folder) => string.IsNullOrEmpty(folder) ? ProjectRoot : ProjectRoot + "/" + folder;

    /// <summary>"Settings", "CombatSettings.asset" → "Assets/Resources/SimpleMovements/Settings/CombatSettings.asset".</summary>
    public static string AssetPath(string folder, string fileName) => FolderPath(folder) + "/" + fileName;

    /// <summary>
    /// A generated asset named <paramref name="name"/>: in its organised folder first, then where older versions put
    /// it (the root of any Resources folder), then (settings and databases) any asset of the type under
    /// Resources/SimpleMovements. Null when there is none.
    /// </summary>
    public static T Load<T>(string folder, string name) where T : UnityEngine.Object
    {
        T asset = Resources.Load<T>(ResourcePath(folder, name));
        if (asset != null)
            return asset;
        asset = Resources.Load<T>(name);
        if (asset != null)
            return asset;
        if (!typeof(ScriptableObject).IsAssignableFrom(typeof(T)))
            return null;   // prefabs and the like: only by their path
        string key = typeof(T).FullName + "/" + name;
        if (missing.Contains(key))
            return null;
        T[] all = Resources.LoadAll<T>(ResourcesRoot);
        foreach (T a in all)
            if (a != null && a.name == name) return a;
        if (all.Length > 0 && all[0] != null)
            return all[0];
        missing.Add(key);
        return null;
    }

    /// <summary>The folder of a project-wide asset by its name ("CombatSettings" → Settings); null when unknown.</summary>
    public static string FolderOf(string name)
    {
        switch (name)
        {
            case "CombatSettings": case "AbsorptionSettings": case "HubWorldSettings": case "ElementalSystem": case "InjurySettings": return Settings;
            case "AbilityDatabase": case "TraitDatabase": case "ItemDatabase": case "ItemCategoryDatabase": case "CharacterCatalog": return Databases;
            case "DefaultCurrency": return Economy;
            case "ItemDropManager": return ItemDrops;
            case "NetWorldItem": return Networking;
            default: return null;
        }
    }

    /// <summary><see cref="Load{T}(string,string)"/> for a project-wide asset known by name (see <see cref="FolderOf"/>).</summary>
    public static T Load<T>(string name) where T : UnityEngine.Object => Load<T>(FolderOf(name), name);

    /// <summary>Forget the assets found missing (call after creating one at runtime or in the editor).</summary>
    public static void ClearCache() => missing.Clear();

    private static readonly HashSet<string> missing = new HashSet<string>(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => missing.Clear();
}
