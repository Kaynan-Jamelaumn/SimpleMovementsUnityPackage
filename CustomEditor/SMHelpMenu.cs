using UnityEditor;
using UnityEngine;

/// <summary>Tools ▸ SimpleMovements ▸ Help: the documentation and what each section of the menu is for.</summary>
public static class SMHelpMenu
{
    [MenuItem(SMMenu.Root + "Help/Open Documentation", false, SMMenu.Help + 0)]
    private static void OpenDocs()
    {
        string path = FindDocs("README.md");
        if (path == null)
        {
            EditorUtility.DisplayDialog("Documentation", "The Documentation folder (with README.md) was not found in the project.", "OK");
            return;
        }
        EditorUtility.OpenWithDefaultApp(path);
    }

    [MenuItem(SMMenu.Root + "Help/Show Documentation Folder", false, SMMenu.Help + 1)]
    private static void ShowDocsFolder()
    {
        string path = FindDocs("README.md");
        Object folder = path != null ? AssetDatabase.LoadAssetAtPath<Object>(System.IO.Path.GetDirectoryName(path).Replace('\\', '/')) : null;
        if (folder == null)
        {
            EditorUtility.DisplayDialog("Documentation", "The Documentation folder was not found in the project.", "OK");
            return;
        }
        Selection.activeObject = folder;
        EditorGUIUtility.PingObject(folder);
    }

    [MenuItem(SMMenu.Root + "Help/What Is In This Menu", false, SMMenu.Help + 20)]
    private static void MenuOverview()
    {
        EditorUtility.DisplayDialog("Tools ▸ SimpleMovements",
            "CHECKING AND FIXING\n" +
            "• Project Validator: the window with every check and Fix buttons.\n" +
            "• Check Everything: open scenes + all prefabs + project assets + game setup.\n" +
            "• Validate: one area at a time (players, abilities, traits, NPCs, portals, hub world, network, input actions, scene setup).\n" +
            "• Auto-Assign: fill empty references, set up players, add missing input actions, fix the scene setup.\n\n" +
            "PROJECT\n" +
            "• Project Setup: settings and databases in Resources (combat, abilities, traits, items, currency, hub world).\n" +
            "• Input: check / complete your Input Actions asset, saved key bindings.\n\n" +
            "CHARACTERS AND ITEMS\n" +
            "• Player, Inventory, Mobs, NPC, Social (targeting and parties).\n\n" +
            "WORLD\n" +
            "• Hub World, Weather (day/night, weather conditions), Dungeon, Scene (menus, character creation, realm music).\n\n" +
            "MULTIPLAYER\n" +
            "• Network: bootstrap scene, networked prefabs, relays, builds, running locally.\n\n" +
            "Asset templates are in Assets ▸ Create ▸ SimpleMovements; scene objects in GameObject ▸ SimpleMovements.", "OK");
    }

    private static string FindDocs(string file)
    {
        foreach (string guid in AssetDatabase.FindAssets("README"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.EndsWith("Documentation/" + file))
                return p;
        }
        return null;
    }
}
