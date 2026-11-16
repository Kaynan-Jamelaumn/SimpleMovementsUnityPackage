#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates every disability trait at once (Tools ▸ SimpleMovements ▸ Player ▸ Create Disability Traits): one Trait asset
/// per preset of <see cref="DisabilityTraitPresets"/>, in Characters/Traits/Disabilities (or the selected folder),
/// added to the Trait Database, with the degrees and sides of one thing (Limp / Bad Leg / Lame, the two blind eyes, Hard
/// Of Hearing / Deaf...) made mutually exclusive. Traits that already exist (by name) are kept as they are - only
/// their exclusive links are completed - so it can be run again after an update to get the new ones.
/// </summary>
public static class DisabilityTraitTools
{
    [MenuItem(SMMenu.Root + "Player/Create Disability Traits", false, SMMenu.Player + 31)]
    private static void CreateAll()
    {
        string folder = SMAssetsEditor.SelectedFolderOr(SMAssets.Traits + "/Disabilities");
        TraitDatabase database = TraitDatabase.Instance;

        // What exists already, by name: in the database and anywhere in the project.
        var byName = new Dictionary<string, Trait>(System.StringComparer.OrdinalIgnoreCase);
        foreach (string guid in AssetDatabase.FindAssets("t:Trait"))
        {
            var t = AssetDatabase.LoadAssetAtPath<Trait>(AssetDatabase.GUIDToAssetPath(guid));
            if (t != null && !byName.ContainsKey(t.Name))
                byName[t.Name] = t;
        }

        int created = 0, kept = 0, linked = 0;
        var groups = new Dictionary<string, List<Trait>>();
        foreach (TraitPresets.Preset preset in DisabilityTraitPresets.Build())
        {
            if (!byName.TryGetValue(preset.name, out Trait trait))
            {
                trait = TraitPresets.CreateAsset(preset, folder);
                byName[preset.name] = trait;
                created++;
            }
            else
            {
                kept++;
            }
            if (database != null)
                database.AddTrait(trait);
            if (string.IsNullOrEmpty(preset.group))
                continue;
            if (!groups.TryGetValue(preset.group, out List<Trait> list))
                groups[preset.group] = list = new List<Trait>();
            list.Add(trait);
        }

        // Degrees and sides of one thing: only one of each group can be taken.
        foreach (List<Trait> list in groups.Values)
        {
            foreach (Trait a in list)
            {
                bool changed = false;
                foreach (Trait b in list)
                {
                    if (a == b || a.mutuallyExclusiveTraits.Contains(b))
                        continue;
                    a.mutuallyExclusiveTraits.Add(b);
                    changed = true;
                    linked++;
                }
                if (changed)
                    EditorUtility.SetDirty(a);
            }
        }
        if (database != null)
            EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        string where = database != null ? "and added to the Trait Database" : "(no Trait Database found: Project Setup ▸ Create Trait Database adds them)";
        string message = $"{created} disability trait(s) created in {folder}, {kept} already there, {linked} exclusive link(s) set {where}.\n\n" +
                         "They have a negative cost: picking one gives trait points. A class that lists its Selectable Traits needs them " +
                         "added to that list to offer them at character creation.";
        Debug.Log("[Traits] " + message.Replace("\n\n", " "));
        EditorUtility.DisplayDialog("Disability traits", message, "OK");
    }
}
#endif
