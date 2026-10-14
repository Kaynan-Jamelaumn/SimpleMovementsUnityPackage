using UnityEditor;
using UnityEngine;

/// <summary>Menus that set up targeting, parties and player social data.</summary>
public static class SocialSetupMenu
{
    private const string Menu = "Tools/SimpleMovements/Social/";

    [MenuItem(SMMenu.Root + "Social/Create Player Targeting And Party System (open scene)", false, SMMenu.Social + 0)]
    private static void CreateTargeting()
    {
#if UNITY_6000_4_OR_NEWER
        PlayerTargeting existing = Object.FindAnyObjectByType<PlayerTargeting>();
#else
        PlayerTargeting existing = Object.FindAnyObjectByType<PlayerTargeting>(FindObjectsInactive.Include);
#endif
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorUtility.DisplayDialog("Player Targeting", "This scene already has a Player Targeting (selected).", "OK");
            return;
        }
        var go = new GameObject("Player Targeting");
        go.AddComponent<PlayerTargeting>();
        var partiesGo = new GameObject("Party System");
        partiesGo.AddComponent<PartySystem>();
        Undo.RegisterCreatedObjectUndo(go, "Create Player Targeting");
        Undo.RegisterCreatedObjectUndo(partiesGo, "Create Party System");
        Selection.activeGameObject = go;
        EditorUtility.DisplayDialog("Player Targeting",
            "Created 'Player Targeting' and 'Party System'. Put them in a scene that stays loaded (the City, or the Bootstrap " +
            "scene); both keep themselves across scene changes. Without them both are created at runtime with the default " +
            "settings, so this is only needed to change the settings (keys, colours, party size).\n\n" +
            "Network sessions also need the Net Social relay on the Network World object: Tools ▸ SimpleMovements ▸ Network ▸ " +
            "Add Missing Relays To Network World.", "OK");
    }

    [MenuItem(SMMenu.Root + "Player/Add Player Social (selected player prefab)", false, SMMenu.Player + 41)]
    private static void AddSocial()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Player Social", "Select the player prefab (or the player in the scene) first.", "OK");
            return;
        }
        string path = AssetDatabase.GetAssetPath(selected);
        if (!string.IsNullOrEmpty(path))
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string msg = Add(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                EditorUtility.DisplayDialog("Player Social", msg, "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return;
        }
        Undo.RegisterFullObjectHierarchyUndo(selected, "Add Player Social");
        EditorUtility.DisplayDialog("Player Social", Add(selected), "OK");
    }

    private static string Add(GameObject root)
    {
        PlayerStatusController status = root.GetComponentInChildren<PlayerStatusController>(true);
        GameObject host = status != null ? status.gameObject : root;
        if (host.GetComponentInChildren<PlayerSocial>(true) != null || root.GetComponentInChildren<PlayerSocial>(true) != null)
            return "The player already has a Player Social.";
        host.AddComponent<PlayerSocial>();
        return $"Added a Player Social to '{host.name}': guild, tag, alliance, fame, title and Allow Inspect. (Players get one " +
               "automatically at runtime; add it to set values for testing.)";
    }
}
