#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Menus that add the movement sounds to the player and the realm music to a scene.</summary>
public static class PlayerAudioSetup
{
    [MenuItem(SMMenu.Root + "Player/Add Movement Audio (selected player prefab)", false, SMMenu.Player + 40)]
    private static void AddMovementAudio()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Movement Audio", "Select the player prefab (or the player in the scene) first.", "OK");
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
                EditorUtility.DisplayDialog("Movement Audio", msg, "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return;
        }
        Undo.RegisterFullObjectHierarchyUndo(selected, "Add Movement Audio");
        EditorUtility.DisplayDialog("Movement Audio", Add(selected), "OK");
    }

    private static string Add(GameObject root)
    {
        MovementStateMachine machine = root.GetComponentInChildren<MovementStateMachine>(true);
        GameObject host = machine != null ? machine.gameObject : root;
        if (host.GetComponent<PlayerMovementAudio>() != null)
            return "The player already has a Player Movement Audio. Drag your clips into its lists (footsteps, jump, land, dash, roll, water).";
        host.AddComponent<PlayerMovementAudio>();
        return $"Added a Player Movement Audio to '{host.name}'. Drag your clips into its lists (footsteps, jump, land, dash, roll, " +
               "splash, swim strokes, gasp, underwater loop) and set its Output to your SFX mixer group. For footsteps in time with the " +
               "animation, add Animation Events named OnFootstep to your walk/run clips.";
    }

    [MenuItem(SMMenu.Root + "Scene/Create Realm Music (open scene)", false, SMMenu.Scene + 20)]
    private static void CreateRealmMusic()
    {
        if (Object.FindAnyObjectByType<HubWorld.RealmAudio>() != null)
        {
            EditorUtility.DisplayDialog("Realm Music", "This scene already has a Realm Audio.", "OK");
            return;
        }
        var go = new GameObject("Realm Music");
        go.AddComponent<HubWorld.RealmAudio>();
        if (Object.FindAnyObjectByType<SoundManager>() == null)
            go.AddComponent<SoundManager>();
        Undo.RegisterCreatedObjectUndo(go, "Create Realm Music");
        Selection.activeGameObject = go;
        EditorUtility.DisplayDialog("Realm Music", "Created 'Realm Music'. Put it in the City scene and drag your music and ambience " +
            "clips in: City, World (and per zone ring), Dungeons. It plays the music of where your own player is.", "OK");
    }
}
#endif
