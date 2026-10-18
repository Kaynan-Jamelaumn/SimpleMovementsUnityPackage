#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Editor side of <see cref="SMAssets"/>: every tool that generates an asset (settings, databases, profiles, abilities,
/// prefabs, materials, meshes) asks here for its folder, so everything lands in Assets/Resources/SimpleMovements in
/// one folder per kind. Also the menu that moves assets made by older versions there.
/// </summary>
public static class SMAssetsEditor
{
    // ------------------------------------------------------------------ folders
    /// <summary>The organised folder (created with its parents), e.g. "Assets/Resources/SimpleMovements/Settings".</summary>
    public static string Folder(string folder) => EnsureFolder(SMAssets.FolderPath(folder));

    /// <summary>A mob type's folder: Mobs/&lt;Type&gt; (its profile, basic attack and abilities).</summary>
    public static string MobFolder(string mobType) => Folder(SMAssets.Mobs + "/" + CleanName(string.IsNullOrWhiteSpace(mobType) ? "Mob" : mobType));

    /// <summary>A free path for a new asset in an organised folder ("Wolf Profile.asset" → "... /Wolf Profile 1.asset" when taken).</summary>
    public static string UniquePath(string folder, string fileName) =>
        AssetDatabase.GenerateUniqueAssetPath(Folder(folder) + "/" + CleanFileName(fileName));

    /// <summary>
    /// The folder selected in the Project window (a folder, or the folder of a selected asset) when the user picked one
    /// below Assets; otherwise the organised folder. Used by the Assets ▸ Create menus.
    /// </summary>
    public static string SelectedFolderOr(string folder)
    {
        foreach (Object o in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
        {
            string p = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(p)) continue;
            string dir = AssetDatabase.IsValidFolder(p) ? p : Path.GetDirectoryName(p).Replace('\\', '/');
            if (dir != "Assets" && dir.StartsWith("Assets/", StringComparison.Ordinal)) return dir;
            break;
        }
        return Folder(folder);
    }

    /// <summary>Creates <paramref name="path"/> ("Assets/A/B") and its parents if needed; returns it.</summary>
    public static string EnsureFolder(string path)
    {
        path = path.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(path) || path == "Assets")
            return path;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        return path;
    }

    public static string CleanFileName(string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c, '_');
        return fileName.Trim();
    }

    private static string CleanName(string name) => CleanFileName(name).Replace('/', '_');

    // ------------------------------------------------------------------ project-wide assets (settings, databases)
    /// <summary>
    /// The project's <typeparamref name="T"/> named <paramref name="name"/>: in its organised folder, at the root of a
    /// Resources folder (older versions), else anywhere in the project. Null when there is none.
    /// </summary>
    public static T Find<T>(string folder, string name) where T : Object
    {
        T found = SMAssets.Load<T>(folder, name);
        if (found != null && found.name == name)
            return found;
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) != name) continue;
            T a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
        }
        return found;
    }

    /// <summary>
    /// The project-wide <typeparamref name="T"/> named <paramref name="name"/> (Combat Settings, Trait Database...):
    /// the existing one (moved into its organised folder when it sits where the game cannot load it), else a new one
    /// created there and passed to <paramref name="init"/>. Selected and pinged when <paramref name="select"/>.
    /// </summary>
    public static T FindOrCreate<T>(string folder, string name, Action<T> init = null, bool select = true) where T : ScriptableObject
    {
        T asset = Find<T>(folder, name);
        if (asset != null)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (!path.Contains("/Resources/"))
            {
                // Outside every Resources folder the game never loads it: move it where it belongs.
                string to = SMAssets.AssetPath(folder, name + Path.GetExtension(path));
                Folder(folder);
                if (AssetDatabase.LoadAssetAtPath<Object>(to) == null && string.IsNullOrEmpty(AssetDatabase.MoveAsset(path, to)))
                    Debug.Log($"[SimpleMovements] Moved {path} to {to} (the game loads it from there).", asset);
            }
        }
        else
        {
            asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            Folder(folder);
            AssetDatabase.CreateAsset(asset, SMAssets.AssetPath(folder, name + ".asset"));
            init?.Invoke(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            SMAssets.ClearCache();
            Debug.Log($"[SimpleMovements] Created {AssetDatabase.GetAssetPath(asset)}.", asset);
        }
        if (select)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
        return asset;
    }

    // ------------------------------------------------------------------ organising older projects
    /// <summary>Project-wide assets older versions put at the root of a Resources folder: type, name, extension, new folder.</summary>
    private static readonly (Type type, string name, string ext, string folder)[] Singletons =
    {
        (typeof(CombatSettings), "CombatSettings", ".asset", SMAssets.Settings),
        (typeof(AbsorptionSettings), "AbsorptionSettings", ".asset", SMAssets.Settings),
        (typeof(HubWorld.HubWorldSettings), "HubWorldSettings", ".asset", SMAssets.Settings),
        (typeof(ElementalSystem), "ElementalSystem", ".asset", SMAssets.Settings),
        (typeof(AbilityDatabase), "AbilityDatabase", ".asset", SMAssets.Databases),
        (typeof(TraitDatabase), "TraitDatabase", ".asset", SMAssets.Databases),
        (typeof(ItemDatabase), ItemDatabase.ResourcesName, ".asset", SMAssets.Databases),
        (typeof(ItemCategoryDatabase), ItemCategoryDatabase.ResourcesName, ".asset", SMAssets.Databases),
        (typeof(CharacterCatalog), "CharacterCatalog", ".asset", SMAssets.Databases),
        (typeof(CurrencyDefinition), CurrencyDefinition.ResourcesName, ".asset", SMAssets.Economy),
        (typeof(ItemDropManager), ItemDropManager.ResourcesName, ".asset", SMAssets.ItemDrops),
        (typeof(GameObject), "NetWorldItem", ".prefab", SMAssets.Networking),
    };

    /// <summary>Folders older versions generated into, and where their contents go now.</summary>
    private static readonly (string from, string to)[] LegacyFolders =
    {
        ("Assets/Generated/Mobs", SMAssets.Mobs),
        ("Assets/Generated", SMAssets.Generated),
        ("Assets/HubWorld Materials", SMAssets.HubWorld),
        ("Assets/SimpleMovements/Weather Conditions", SMAssets.Weather),
        ("Assets/SimpleMovements/Item Drops", SMAssets.DropTables),
        ("Assets/BiomeBackups", SMAssets.BiomeBackups),
        ("Assets/UI/HUD", SMAssets.HUD),
        ("Assets/Inventory/Generated", SMAssets.InventoryUI),
        ("Assets/Characters/Races", SMAssets.Races),
    };

    /// <summary>Single files older versions generated, and where they go now.</summary>
    private static readonly (string from, string to)[] LegacyFiles =
    {
        ("Assets/Networking/RealmObserverCondition.asset", SMAssets.Networking),
    };

    [MenuItem(SMMenu.Root + "Project Setup/Organise Generated Assets...", false, SMMenu.ProjectSetup + 40)]
    public static void OrganiseMenu()
    {
        List<(string from, string to)> moves = PlanMoves();
        if (moves.Count == 0)
        {
            EditorUtility.DisplayDialog("Organise Generated Assets",
                $"Nothing to move: every generated asset is already in {SMAssets.ProjectRoot}.", "OK");
            return;
        }
        var sb = new StringBuilder($"{moves.Count} asset(s) will move into {SMAssets.ProjectRoot} " +
                                   "(references in scenes and prefabs are kept):\n\n");
        foreach ((string from, string to) in moves.Take(18))
            sb.Append(from).Append("\n   → ").Append(to.Substring(SMAssets.ProjectRoot.Length + 1)).Append('\n');
        if (moves.Count > 18) sb.Append($"... and {moves.Count - 18} more (listed in the Console).\n");
        foreach ((string from, string to) in moves)
            Debug.Log($"[SimpleMovements] Organise: {from} → {to}");
        if (!EditorUtility.DisplayDialog("Organise Generated Assets", sb.ToString(), "Move", "Cancel"))
            return;
        int moved = Organise(moves, out List<string> failed);
        string report = $"Moved {moved} asset(s) into {SMAssets.ProjectRoot}." +
                        (failed.Count > 0 ? $"\n{failed.Count} could not move (see the Console)." : "");
        foreach (string f in failed) Debug.LogWarning("[SimpleMovements] Organise: " + f);
        EditorUtility.DisplayDialog("Organise Generated Assets", report, "OK");
    }

    [MenuItem(SMMenu.Root + "Project Setup/Show Generated Assets Folder", false, SMMenu.ProjectSetup + 41)]
    private static void ShowFolder()
    {
        string path = Folder("");
        Object folder = AssetDatabase.LoadAssetAtPath<Object>(path);
        Selection.activeObject = folder;
        EditorGUIUtility.PingObject(folder);
    }

    /// <summary>Every move that would put an older generated asset into its organised folder (nothing is moved).</summary>
    public static List<(string from, string to)> PlanMoves()
    {
        var moves = new List<(string from, string to)>();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string from, string to)
        {
            if (from == to || taken.Contains(to) || moves.Exists(m => m.from == from)) return;
            if (AssetDatabase.LoadAssetAtPath<Object>(to) != null) return;   // the organised one exists: keep both, nothing overwritten
            taken.Add(to);
            moves.Add((from, to));
        }

        foreach ((Type type, string name, string ext, string folder) in Singletons)
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{type.Name}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != name || !path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) continue;
                if (path.StartsWith(SMAssets.ProjectRoot + "/", StringComparison.Ordinal)) continue;
                // Only where older versions put them: the root of a Resources folder (Assets/Resources,
                // Assets/SimpleMovements/Resources...). Copies elsewhere are not loaded by the game and stay where they are.
                string dir = Path.GetDirectoryName(path).Replace('\\', '/');
                if (!dir.EndsWith("/Resources", StringComparison.Ordinal)) continue;
                Add(path, SMAssets.AssetPath(folder, name + ext));
            }
        }

        foreach ((string from, string to) in LegacyFolders)
        {
            if (!AssetDatabase.IsValidFolder(from)) continue;
            foreach (string guid in AssetDatabase.FindAssets("", new[] { from }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue;
                if (from == "Assets/Generated" && path.StartsWith("Assets/Generated/Mobs/", StringComparison.Ordinal)) continue;
                string rel = path.Substring(from.Length + 1);
                string target = to;
                if (from == "Assets/Generated" && AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path) != null)
                    target = SMAssets.Abilities;
                Add(path, SMAssets.AssetPath(target, rel));
            }
        }

        foreach ((string from, string to) in LegacyFiles)
            if (AssetDatabase.LoadAssetAtPath<Object>(from) != null)
                Add(from, SMAssets.AssetPath(to, Path.GetFileName(from)));
        return moves;
    }

    /// <summary>Moves the assets (GUIDs, so every reference, are kept) and removes the folders left empty.</summary>
    public static int Organise(List<(string from, string to)> moves, out List<string> failed)
    {
        failed = new List<string>();
        int moved = 0;
        var emptied = new HashSet<string>();
        foreach ((string from, string to) in moves)
        {
            EnsureFolder(Path.GetDirectoryName(to).Replace('\\', '/'));
            string error = AssetDatabase.MoveAsset(from, to);
            if (string.IsNullOrEmpty(error))
            {
                moved++;
                emptied.Add(Path.GetDirectoryName(from).Replace('\\', '/'));
            }
            else
                failed.Add($"{from} → {to}: {error}");
        }
        // Folders the move left empty (and their empty parents, up to Assets).
        foreach (string dir in emptied.OrderByDescending(d => d.Length))
            for (string d = dir; d != null && d != "Assets" && d.StartsWith("Assets/", StringComparison.Ordinal); d = Path.GetDirectoryName(d)?.Replace('\\', '/'))
            {
                if (!AssetDatabase.IsValidFolder(d) || d == SMAssets.ProjectRoot || SMAssets.ProjectRoot.StartsWith(d + "/", StringComparison.Ordinal)) break;
                if (AssetDatabase.FindAssets("", new[] { d }).Length > 0 || Directory.GetFiles(d).Any(f => !f.EndsWith(".meta"))) break;
                AssetDatabase.DeleteAsset(d);
            }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SMAssets.ClearCache();
        return moved;
    }
}
#endif
