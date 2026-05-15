#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class InventoryEditorComponentDetection
{
    private InventoryManager inventoryManager;
    private SerializedObject serializedObject;
    private InventoryEditorUtilities utils;

    // Auto-detection results
    private List<Camera> detectedCameras = new List<Camera>();
    private List<GameObject> detectedPlayers = new List<GameObject>();
    private List<PlayerStatusController> detectedPlayerControllers = new List<PlayerStatusController>();
    private List<WeaponController> detectedWeaponControllers = new List<WeaponController>();
    private List<Transform> detectedHandParents = new List<Transform>();
    private List<GameObject> detectedItemPrefabs = new List<GameObject>();
    private List<SlotManager> detectedSlotManagers = new List<SlotManager>();
    private List<UILayoutManager> detectedUILayoutManagers = new List<UILayoutManager>();

    public struct DetectedComponents
    {
        public List<Camera> cameras;
        public List<GameObject> players;
        public List<PlayerStatusController> playerControllers;
        public List<WeaponController> weaponControllers;
        public List<Transform> handParents;
        public List<GameObject> itemPrefabs;
        public List<SlotManager> slotManagers;
        public List<UILayoutManager> uiLayoutManagers;
    }

    public struct DetectedCounts
    {
        public int cameras;
        public int players;
        public int playerControllers;
        public int weaponControllers;
        public int handParents;
        public int itemPrefabs;
        public int slotManagers;
        public int uiLayoutManagers;
    }

    public InventoryEditorComponentDetection(InventoryManager manager, SerializedObject serialized, InventoryEditorUtilities utilities)
    {
        inventoryManager = manager;
        serializedObject = serialized;
        utils = utilities;
    }

    public void Initialize()
    {
        // Initial detection if enabled
        if (EditorPrefs.GetBool("InventoryEditor_AutoDetect", true))
        {
            RunAutoDetection();
        }
    }

    public DetectedComponents GetDetectedComponents()
    {
        return new DetectedComponents
        {
            cameras = detectedCameras,
            players = detectedPlayers,
            playerControllers = detectedPlayerControllers,
            weaponControllers = detectedWeaponControllers,
            handParents = detectedHandParents,
            itemPrefabs = detectedItemPrefabs,
            slotManagers = detectedSlotManagers,
            uiLayoutManagers = detectedUILayoutManagers
        };
    }

    public DetectedCounts GetDetectedCounts()
    {
        return new DetectedCounts
        {
            cameras = detectedCameras.Count,
            players = detectedPlayers.Count,
            playerControllers = detectedPlayerControllers.Count,
            weaponControllers = detectedWeaponControllers.Count,
            handParents = detectedHandParents.Count,
            itemPrefabs = detectedItemPrefabs.Count,
            slotManagers = detectedSlotManagers.Count,
            uiLayoutManagers = detectedUILayoutManagers.Count
        };
    }

    public void DrawComponentDetectionSection(InventoryEditorStyles styles)
    {
        EditorGUILayout.BeginVertical(styles.BoxStyle);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔄 Refresh Detection", styles.ButtonStyle))
        {
            RunAutoDetection();
        }

        if (GUILayout.Button("📋 Detection Report", styles.ButtonStyle))
        {
            GenerateDetectionReport();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Detection results
        DrawDetectionResults();

        EditorGUILayout.EndVertical();
    }

    private void DrawDetectionResults()
    {
        EditorGUILayout.LabelField("🔎 Detection Results", EditorStyles.boldLabel);

        // Cameras
        utils.DrawDetectionCategory("Cameras", detectedCameras.Count, () => {
            foreach (var cam in detectedCameras)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(cam, typeof(Camera), true);
                if (GUILayout.Button("Use", GUILayout.Width(50)))
                {
                    utils.CamProp.objectReferenceValue = cam;
                }
                EditorGUILayout.EndHorizontal();
            }
        });

        // Players
        utils.DrawDetectionCategory("Players", detectedPlayers.Count, () => {
            foreach (var player in detectedPlayers)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(player, typeof(GameObject), true);
                if (GUILayout.Button("Use", GUILayout.Width(50)))
                {
                    utils.PlayerProp.objectReferenceValue = player;
                    AutoAssignPlayerRelatedComponents(player);
                }
                EditorGUILayout.EndHorizontal();
            }
        });

        // Player Controllers
        utils.DrawDetectionCategory("Player Status Controllers", detectedPlayerControllers.Count, () => {
            foreach (var controller in detectedPlayerControllers)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(controller, typeof(PlayerStatusController), true);
                if (GUILayout.Button("Use", GUILayout.Width(50)))
                {
                    utils.PlayerStatusControllerProp.objectReferenceValue = controller;
                }
                EditorGUILayout.EndHorizontal();
            }
        });

        // Item Prefabs
        utils.DrawDetectionCategory("Item Prefabs", detectedItemPrefabs.Count, () => {
            foreach (var prefab in detectedItemPrefabs)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
                if (GUILayout.Button("Use", GUILayout.Width(50)))
                {
                    utils.ItemPrefabProp.objectReferenceValue = prefab;
                }
                EditorGUILayout.EndHorizontal();
            }
        });
    }

    public void RunAutoDetection()
    {
        DetectCameras();
        DetectPlayers();
        DetectPlayerControllers();
        DetectWeaponControllers();
        DetectHandParents();
        DetectItemPrefabs();
        DetectSlotManagers();
        DetectUILayoutManagers();
    }

    private void DetectCameras()
    {
        detectedCameras.Clear();
        detectedCameras.AddRange(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
            .Where(cam => cam.gameObject.activeInHierarchy)
            .OrderByDescending(cam => cam.tag == "MainCamera" ? 1 : 0));
    }

    private void DetectPlayers()
    {
        detectedPlayers.Clear();

        // Look for objects with Player component
        var playersWithComponent = Object.FindObjectsByType<Player>(FindObjectsSortMode.None)
            .Select(p => p.gameObject)
            .Where(go => go.activeInHierarchy);
        detectedPlayers.AddRange(playersWithComponent);

        // Look for GameObjects tagged as "Player"
        var taggedPlayers = GameObject.FindGameObjectsWithTag("Player")
            .Where(go => go.activeInHierarchy && !detectedPlayers.Contains(go));
        detectedPlayers.AddRange(taggedPlayers);

        // Look for GameObjects with "Player" in name
        var namedPlayers = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
            .Where(go => go.name.ToLower().Contains("player") &&
                        go.activeInHierarchy &&
                        !detectedPlayers.Contains(go));
        detectedPlayers.AddRange(namedPlayers);
    }

    private void DetectPlayerControllers()
    {
        detectedPlayerControllers.Clear();
        detectedPlayerControllers.AddRange(Object.FindObjectsByType<PlayerStatusController>(FindObjectsSortMode.None)
            .Where(psc => psc.gameObject.activeInHierarchy));
    }

    private void DetectWeaponControllers()
    {
        detectedWeaponControllers.Clear();
        detectedWeaponControllers.AddRange(Object.FindObjectsByType<WeaponController>(FindObjectsSortMode.None)
            .Where(wc => wc.gameObject.activeInHierarchy));
    }

    private void DetectHandParents()
    {
        detectedHandParents.Clear();

        // Look for transforms with "hand" in name
        var handTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Where(t => t.name.ToLower().Contains("hand") && t.gameObject.activeInHierarchy);
        detectedHandParents.AddRange(handTransforms);

        // Look in player objects for likely hand parent candidates
        foreach (var player in detectedPlayers)
        {
            var candidates = player.GetComponentsInChildren<Transform>()
                .Where(t => t.name.ToLower().Contains("hand") ||
                           t.name.ToLower().Contains("hold") ||
                           t.name.ToLower().Contains("grip"));
            detectedHandParents.AddRange(candidates);
        }

        detectedHandParents = detectedHandParents.Distinct().ToList();
    }

    private void DetectItemPrefabs()
    {
        detectedItemPrefabs.Clear();

        // Search in Assets folder for prefabs with InventoryItem component
        string[] guids = AssetDatabase.FindAssets("t:Prefab");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab?.GetComponent<InventoryItem>() != null)
            {
                detectedItemPrefabs.Add(prefab);
            }
        }
    }

    private void DetectSlotManagers()
    {
        detectedSlotManagers.Clear();

        //if (utils.SlotManagerProp != null)
        //{
        //    Debug.Log("🎪 SlotManager property found and appears to be configured");
        //}
    }

    private void DetectUILayoutManagers()
    {
        detectedUILayoutManagers.Clear();

        //if (utils.UILayoutManagerProp != null)
        //{
        //    Debug.Log("📐 UILayoutManager property found and appears to be configured");
        //}
    }

    public void GenerateDetectionReport()
    {
        Debug.Log("📋 Component Detection Report:");
        Debug.Log($"Cameras: {detectedCameras.Count}");
        Debug.Log($"Players: {detectedPlayers.Count}");
        Debug.Log($"Player Status Controllers: {detectedPlayerControllers.Count}");
        Debug.Log($"Weapon Controllers: {detectedWeaponControllers.Count}");
        Debug.Log($"Hand Parents: {detectedHandParents.Count}");
        Debug.Log($"Item Prefabs: {detectedItemPrefabs.Count}");

        EditorUtility.DisplayDialog("Detection Report",
            $"Detection Results:\n\n" +
            $"Cameras: {detectedCameras.Count}\n" +
            $"Players: {detectedPlayers.Count}\n" +
            $"Controllers: {detectedPlayerControllers.Count}\n" +
            $"Item Prefabs: {detectedItemPrefabs.Count}\n\n" +
            "Check console for detailed report.", "OK");
    }

    private void AutoAssignPlayerRelatedComponents(GameObject player)
    {
        // Try to find and assign player-related components
        var statusController = player.GetComponent<PlayerStatusController>();
        if (statusController != null && utils.PlayerStatusControllerProp.objectReferenceValue == null)
        {
            utils.PlayerStatusControllerProp.objectReferenceValue = statusController;
        }

        var weaponController = player.GetComponent<WeaponController>();
        if (weaponController != null && utils.WeaponControllerProp.objectReferenceValue == null)
        {
            utils.WeaponControllerProp.objectReferenceValue = weaponController;
        }
    }
}
#endif