#if UNITY_EDITOR
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
        int choice = EditorUtility.DisplayDialogComplex("Tools ▸ SimpleMovements",
            "CHECKING AND FIXING\n" +
            "• Project Validator: every check of the project, with Fix buttons. Check Everything: open scenes + all prefabs + assets.\n" +
            "• Scene and Prefab Setup: one page per scene / prefab the game needs (Bootstrap, Hub City, World, Dungeon scenes; Player, Mob, " +
            "Item prefabs) - what it is for, what it needs, why, and a button that adds what is missing.\n" +
            "• Validate: one check per area (players, abilities, traits, NPCs, portals, hub world, network, input, scene setup).\n" +
            "• Auto-Assign: fill empty references automatically (selection, open scenes, all prefabs).\n\n" +
            "PROJECT\n" +
            "• Project Setup: settings and databases in Resources. Input: check / complete the Input Actions asset, saved key bindings.\n\n" +
            "CHARACTERS AND ITEMS\n" +
            "• Player, Inventory, Mobs, NPC, Social (targeting, parties, guilds).\n\n" +
            "WORLD\n" +
            "• Hub World, Weather (day / night, weather conditions), Dungeon, Scene (menus, character creation, realm music).\n\n" +
            "MULTIPLAYER\n" +
            "• Network: bootstrap scene, networked prefabs, relays, builds, running locally.\n\n" +
            "Where things go: a tool that makes or changes one kind of thing is in that thing's section; Validate only checks; Auto-Assign only " +
            "fills references. Most sections end with Help. Asset templates: Assets ▸ Create ▸ SimpleMovements; scene objects: GameObject ▸ SimpleMovements.",
            "Open Scene and Prefab Setup", "Close", "Open Documentation");
        if (choice == 0)
            SimpleMovements.Setup.SceneAndPrefabSetupWindow.ShowOverview();
        else if (choice == 2)
            OpenDocs();
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
#endif
