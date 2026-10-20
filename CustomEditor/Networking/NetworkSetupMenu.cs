#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
#if FISHNET
using FishNet.Managing;
using FishNet.Managing.Observing;
using FishNet.Object;
using FishNet.Observing;
using FishNet.Transporting.Tugboat;
#endif

namespace GameNet.EditorTools
{
    /// <summary>
    /// Everything needed to run the game networked on this machine, from the menu
    /// (Tools ▸ SimpleMovements ▸ Network): the Bootstrap scene with a configured NetworkManager, the dungeon instance
    /// scene, turning prefabs into network prefabs, building a local dedicated server and a client, launching them,
    /// and playing in the editor as host, client or server. No remote server is involved anywhere: everything uses
    /// 127.0.0.1.
    /// </summary>
    public static class NetworkSetupMenu
    {
        private const string Menu = "Tools/SimpleMovements/Network/";
        internal const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
        internal const string DungeonInstancePath = "Assets/Scenes/DungeonInstance.unity";
        private const string BuildsFolder = "Builds";
        internal const string EditorModeKey = "GameNet.EditorPlayMode";

        /// <summary>Components each kind of network prefab gets (by type name, so missing modules are just skipped).</summary>
        internal static readonly Dictionary<string, string[]> PrefabComponents = new Dictionary<string, string[]>
        {
            ["Player"] = new[]
            {
                "GameNet.NetPlayer", "GameNet.NetPlayerMovement", "GameNet.NetAbilityCaster", "GameNet.NetWeaponCombat",
                "GameNet.NetCombatStatus", "GameNet.NetInventory", "GameNet.NetWallet", "GameNet.NetPlayerRealm",
                "GameNet.NetInteractor", "GameNet.NetInjuries",
            },
            ["Mob"] = new[]
            {
                "GameNet.NetMob", "GameNet.NetCombatStatus", "GameNet.NetAbilityCaster",
                "FishNet.Component.Transforming.NetworkTransform",
            },
            ["Summon"] = new[]
            {
                "GameNet.NetMob", "GameNet.NetCombatStatus", "GameNet.NetAbilityCaster",
                "FishNet.Component.Transforming.NetworkTransform",
            },
            ["Pickup / Loot"] = new[] { "GameNet.NetWorldItem", "FishNet.Component.Transforming.NetworkTransform" },
            ["Ability Pickup"] = new string[0],
            ["World Portal"] = new string[0],
            ["Interactable"] = new[] { "GameNet.NetInteractable" },
        };

        // ------------------------------------------------------------------ scenes

#if FISHNET
        [MenuItem(SMMenu.Root + "Network/Create Bootstrap Scene", false, SMMenu.Network + 0)]
        public static void CreateBootstrapScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (File.Exists(BootstrapPath) &&
                !EditorUtility.DisplayDialog("Network - Bootstrap scene", $"{BootstrapPath} exists. Replace it?", "Replace", "Cancel"))
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject go = BuildNetworkManager();
            NetPlayerSpawner spawner = go.GetComponent<NetPlayerSpawner>();
            GameObject playerPrefab = spawner != null && spawner.playerPrefab != null ? spawner.playerPrefab.gameObject : null;
            BuildNetworkWorld();

            Directory.CreateDirectory(Path.GetDirectoryName(BootstrapPath) ?? "Assets/Scenes");
            EditorSceneManager.SaveScene(scene, BootstrapPath);
            AssignSceneIds(scene); // the objects were added while the scene had no name: Fish-Net could not id them
            CreateWorldItemPrefab();
            EnsureBuildScene(BootstrapPath, 0);
            EditorUtility.DisplayDialog("Network - Bootstrap scene",
                $"Saved {BootstrapPath} as the first scene of the build.\n\n" +
                (playerPrefab == null ? "No network player prefab found yet: select your player prefab and use 'Make Selected Prefab Networked ▸ Player', then assign it on the Net Player Spawner.\n\n" : "") +
                "Press Play in this scene for the start menu, or use Tools ▸ SimpleMovements ▸ Network ▸ Play In Editor As.", "OK");
        }

        /// <summary>
        /// Creates the "NetworkManager" object of the Bootstrap scene in the active scene: Fish-Net's managers (Tugboat on
        /// 127.0.0.1:7770, the server not started by Fish-Net on headless, the realm interest rule) and the package's session
        /// components (NetSession, NetBootstrap, NetRealmScenes, NetPlayerSpawner with the networked player prefab,
        /// NetLagCompensation, HeadlessServer).
        /// </summary>
        internal static GameObject BuildNetworkManager()
        {
            var go = new GameObject("NetworkManager");
            go.AddComponent<NetworkManager>();
            Tugboat tugboat = go.AddComponent<Tugboat>();
            tugboat.SetPort(NetLaunchOptions.DefaultPort);
            tugboat.SetClientAddress(NetLaunchOptions.DefaultAddress);
            go.AddComponent<FishNet.Managing.Transporting.TransportManager>();
            go.AddComponent<FishNet.Managing.Timing.TimeManager>();
            FishNet.Managing.Server.ServerManager serverManager = go.AddComponent<FishNet.Managing.Server.ServerManager>();
            // NetBootstrap starts the server (with the port from the command line); Fish-Net must not start it on its own.
            serverManager.SetStartOnHeadless(false);
            go.AddComponent<FishNet.Managing.Client.ClientManager>();
            go.AddComponent<FishNet.Managing.Scened.SceneManager>();
            go.AddComponent<FishNet.Managing.Predicting.PredictionManager>();
            ObserverManager observers = go.AddComponent<ObserverManager>();

            go.AddComponent<NetSession>();
            go.AddComponent<NetBootstrap>();
            go.AddComponent<NetRealmScenes>();
            NetPlayerSpawner spawner = go.AddComponent<NetPlayerSpawner>();
            go.AddComponent<NetLagCompensation>();
            go.AddComponent<HeadlessServer>();

            // The interest rule: same scene, distance-limited in the open world, owners see their own objects.
            AssignRealmCondition(observers);

            // The player prefab: the networked one in the project.
            GameObject playerPrefab = FindPlayerPrefab();
            if (playerPrefab != null)
                spawner.playerPrefab = playerPrefab.GetComponent<NetworkObject>();
            return go;
        }

        /// <summary>The project's RealmObserverCondition asset (created when missing).</summary>
        internal static RealmObserverCondition RealmCondition() =>
            LoadOrCreateAsset<RealmObserverCondition>(SMAssetsEditor.Folder(SMAssets.Networking) + "/RealmObserverCondition.asset");

        /// <summary>Makes the realm interest rule the only default condition of <paramref name="observers"/>.</summary>
        internal static void AssignRealmCondition(ObserverManager observers)
        {
            if (observers == null)
                return;
            RealmObserverCondition condition = RealmCondition();
            var so = new SerializedObject(observers);
            SerializedProperty list = so.FindProperty("_defaultConditions");
            if (list == null)
                return;
            list.ClearArray();
            list.InsertArrayElementAtIndex(0);
            list.GetArrayElementAtIndex(0).objectReferenceValue = condition;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Does <paramref name="observers"/> use a RealmObserverCondition as a default condition?</summary>
        internal static bool HasRealmCondition(ObserverManager observers)
        {
            if (observers == null)
                return false;
            SerializedProperty list = new SerializedObject(observers).FindProperty("_defaultConditions");
            if (list == null)
                return true; // Fish-Net changed the field: cannot tell, do not complain
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is RealmObserverCondition)
                    return true;
            return false;
        }

        /// <summary>
        /// Creates the "Network World" object in the active scene: a scene NetworkObject every client observes (its
        /// observer ignores the manager's conditions) carrying the global relays.
        /// </summary>
        internal static GameObject BuildNetworkWorld()
        {
            var world = new GameObject("Network World");
            world.AddComponent<NetworkObject>();
            ConfigureWorldObserver(world.AddComponent<NetworkObserver>());
            AddByName(world, GlobalRelayComponents, null);
            return world;
        }

        /// <summary>The Network World is seen by every client: its observer ignores the manager's (realm) conditions.</summary>
        internal static void ConfigureWorldObserver(NetworkObserver observer)
        {
            if (observer == null)
                return;
            var oso = new SerializedObject(observer);
            SerializedProperty overrideType = oso.FindProperty("_overrideType");
            if (overrideType != null)
            {
                overrideType.intValue = (int)NetworkObserver.ConditionOverrideType.IgnoreManager;
                oso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>Is the Network World's observer set to ignore the manager's conditions (seen by everyone)?</summary>
        internal static bool WorldObserverIgnoresManager(NetworkObserver observer)
        {
            if (observer == null)
                return false;
            SerializedProperty overrideType = new SerializedObject(observer).FindProperty("_overrideType");
            return overrideType == null || overrideType.intValue == (int)NetworkObserver.ConditionOverrideType.IgnoreManager;
        }
        /// <summary>
        /// Gives the scene NetworkObjects of <paramref name="scene"/> their scene ids (what Fish-Net's Reserialize Scenes
        /// does) and saves the scene. Fish-Net only creates an id while the object's scene has a name, so objects added to
        /// a new, unsaved scene by a tool have none ("is expected to be initialized but was not" in Play mode).
        /// </summary>
        internal static int AssignSceneIds(Scene scene)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                return 0;
            var nobs = new List<NetworkObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
                nobs.AddRange(root.GetComponentsInChildren<NetworkObject>(true));
            if (nobs.Count == 0)
                return 0;
            const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            System.Reflection.FieldInfo throttle = typeof(NetworkObject).GetField("_lastSceneIdAutomaticRebuildTime", Any);
            System.Reflection.MethodInfo reserialize = typeof(NetworkObject).GetMethod("ReserializeEditorSetValues", Any);
            if (reserialize == null)
            {
                Debug.LogWarning($"[Net] Could not assign scene ids in '{scene.name}' (Fish-Net API changed): use Fish-Networking ▸ Utility ▸ Reserialize NetworkObjects ▸ Reserialize Scenes.");
                return 0;
            }
            foreach (NetworkObject nob in nobs)
            {
                throttle?.SetValue(null, 0d); // Fish-Net rebuilds ids at most every 250 ms otherwise
                reserialize.Invoke(nob, new object[] { true, true });
                EditorUtility.SetDirty(nob);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            int missing = nobs.Count(n => !n.IsSceneObject);
            if (missing > 0)
                Debug.LogWarning($"[Net] {missing} NetworkObject(s) in '{scene.name}' still have no scene id: use Fish-Networking ▸ Utility ▸ Reserialize NetworkObjects ▸ Reserialize Scenes.");
            return nobs.Count - missing;
        }

        [MenuItem(SMMenu.Root + "Network/Fix Scene Network Ids (open scenes)", false, SMMenu.Network + 122)]
        private static void FixOpenSceneIds()
        {
            var report = new StringBuilder();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded)
                    continue;
                int done = AssignSceneIds(s);
                report.AppendLine($"{s.name}: {done} scene NetworkObject(s) with an id.");
            }
            EditorUtility.DisplayDialog("Network - scene ids", report.Length > 0 ? report.ToString() : "No open scene.", "OK");
        }

        /// <summary>Global relay components placed on the "Network World" object (by type name; missing ones are skipped).</summary>
        internal static readonly string[] GlobalRelayComponents =
        {
            "GameNet.NetHubWorld", "GameNet.NetDuels", "GameNet.NetTrading", "GameNet.NetWorldChunks", "GameNet.NetSocial",
            "GameNet.NetPortals",
        };

        /// <summary>
        /// Adds the global relays a "Network World" object of the open scenes is missing (a Bootstrap scene made before a
        /// relay existed, e.g. NetSocial for parties, guilds and inspection).
        /// </summary>
        [MenuItem(SMMenu.Root + "Network/Add Missing Relays To Network World (open scenes)", false, SMMenu.Network + 123)]
        private static void AddMissingRelays()
        {
            var report = new StringBuilder();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded)
                    continue;
                foreach (GameObject root in s.GetRootGameObjects())
                {
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name != "Network World" || t.GetComponent<NetworkObject>() == null)
                            continue;
                        var added = new List<string>();
                        Undo.RegisterFullObjectHierarchyUndo(t.gameObject, "Add Missing Relays");
                        AddByName(t.gameObject, GlobalRelayComponents, added);
                        report.AppendLine(added.Count > 0 ? $"{s.name}: added {string.Join(", ", added)}." : $"{s.name}: nothing missing.");
                        if (added.Count > 0)
                        {
                            EditorUtility.SetDirty(t.gameObject);
                            EditorSceneManager.MarkSceneDirty(s);
                        }
                    }
                }
            }
            EditorUtility.DisplayDialog("Network - relays", report.Length > 0 ? report + "\nSave the scene." :
                "No 'Network World' object in the open scenes. Open the Bootstrap scene (or create it with Create Bootstrap Scene).", "OK");
        }
#endif

        [MenuItem(SMMenu.Root + "Network/Create Dungeon Instance Scene", false, SMMenu.Network + 1)]
        public static void CreateDungeonInstanceScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Scene scene;
            if (!File.Exists(DungeonInstancePath))
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Dungeon Instance").AddComponent<DungeonInstanceSceneMarker>();
                Directory.CreateDirectory(Path.GetDirectoryName(DungeonInstancePath) ?? "Assets/Scenes");
            }
            else
                scene = EditorSceneManager.OpenScene(DungeonInstancePath, OpenSceneMode.Single);
            DungeonInstanceSceneMarker marker = UnityEngine.Object.FindAnyObjectByType<DungeonInstanceSceneMarker>(FindObjectsInactive.Include);
            if (marker == null)
                marker = new GameObject("Dungeon Instance").AddComponent<DungeonInstanceSceneMarker>();
#if FISHNET
            // The run's relay (layouts, element states, interactions) on a scene NetworkObject: every stacked copy has its own.
            if (marker.GetComponent<NetworkObject>() == null)
                marker.gameObject.AddComponent<NetworkObject>();
            AddByName(marker.gameObject, new[] { "GameNet.NetDungeonRun" }, null);
#endif
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, DungeonInstancePath);
#if FISHNET
            AssignSceneIds(scene);
#endif
            EnsureBuildScene(DungeonInstancePath, -1);
            HubWorld.HubWorldSettings settings = HubWorld.HubWorldSettings.Instance;
            if (settings != null && settings.dungeonScenes != null && string.IsNullOrEmpty(settings.dungeonScenes.networkSceneName))
            {
                settings.dungeonScenes.networkSceneName = Path.GetFileNameWithoutExtension(DungeonInstancePath);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            EditorUtility.DisplayDialog("Network - Dungeon instance scene",
                $"{DungeonInstancePath} is in the build. In network sessions every dungeon run loads its own copy of it (scene stacking, own physics); the server builds the dungeon inside and the clients get the layout of each floor as they reach it.", "OK");
        }

        // ------------------------------------------------------------------ world item carrier

        private static string WorldItemPrefabPath => SMAssets.AssetPath(SMAssets.Networking, "NetWorldItem.prefab");

        /// <summary>
        /// Creates Resources/SimpleMovements/Networking/NetWorldItem.prefab: the networked carrier of dropped items, loot and recovered thrown weapons
        /// (a NetworkObject plus NetWorldItem, which builds the item's model on every machine from its replicated id).
        /// </summary>
        [MenuItem(SMMenu.Root + "Network/Create World Item Prefab", false, SMMenu.Network + 12)]
        public static void CreateWorldItemPrefab()
        {
#if FISHNET
            GameObject existing = SMAssets.Load<GameObject>(SMAssets.Networking, "NetWorldItem");
            if (existing != null)
            {
                Debug.Log($"[Net] {AssetDatabase.GetAssetPath(existing)} already exists.", existing);
                return;
            }
            SMAssetsEditor.Folder(SMAssets.Networking);
            var go = new GameObject("NetWorldItem");
            try
            {
                go.AddComponent<NetworkObject>();
                var missing = new List<string>();
                AddByName(go, new[] { "GameNet.NetWorldItem" }, null, missing);
                if (missing.Count > 0)
                    Debug.LogWarning("[Net] GameNet.NetWorldItem was not found: the prefab has only a NetworkObject.");
                PrefabUtility.SaveAsPrefabAsset(go, WorldItemPrefabPath);
                Debug.Log($"[Net] Created {WorldItemPrefabPath}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
#else
            Debug.LogWarning("[Net] Install Fish-Net first.");
#endif
        }

        // ------------------------------------------------------------------ databases

        [MenuItem(SMMenu.Root + "Project Setup/Build Item Database", false, SMMenu.ProjectSetup + 20)]
        public static void BuildItemDatabase()
        {
            ItemDatabase db = SMAssets.Load<ItemDatabase>(SMAssets.Databases, ItemDatabase.ResourcesName);
            if (db == null)
                db = SMAssetsEditor.FindOrCreate<ItemDatabase>(SMAssets.Databases, ItemDatabase.ResourcesName, null, false);
            int before = db.items.Count;
            foreach (string guid in AssetDatabase.FindAssets("t:ItemSO"))
            {
                ItemSO item = AssetDatabase.LoadAssetAtPath<ItemSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null)
                    db.Register(guid, item);
            }
            db.items.RemoveAll(e => e.item == null);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Selection.activeObject = db;
            Debug.Log($"[Net] Item Database: {db.items.Count} item(s) (was {before}).");
        }

        [MenuItem(SMMenu.Root + "Project Setup/Build Ability Database", false, SMMenu.ProjectSetup + 21)]
        public static void BuildAbilityDatabase() =>
            EditorApplication.ExecuteMenuItem("Tools/SimpleMovements/Project Setup/Create Ability Database (Resources)");

        // ------------------------------------------------------------------ prefabs

        [MenuItem(SMMenu.Root + "Network/Make Selected Prefab Networked/Player", false, SMMenu.Network + 20)]
        private static void MakePlayer() => MakeSelected("Player");

        [MenuItem(SMMenu.Root + "Network/Make Selected Prefab Networked/Mob", false, SMMenu.Network + 21)]
        private static void MakeMob() => MakeSelected("Mob");

        [MenuItem(SMMenu.Root + "Network/Make Selected Prefab Networked/Pickup or Loot Drop", false, SMMenu.Network + 22)]
        private static void MakePickup() => MakeSelected("Pickup / Loot");

        [MenuItem(SMMenu.Root + "Network/Make Selected Prefab Networked/Summon", false, SMMenu.Network + 24)]
        private static void MakeSummon() => MakeSelected("Summon");

        [MenuItem(SMMenu.Root + "Network/Make Selected Prefab Networked/Ability Pickup", false, SMMenu.Network + 25)]
        private static void MakeAbilityPickup() => MakeSelected("Ability Pickup");

        [MenuItem(SMMenu.Root + "Network/Make Selected Prefab Networked/World Portal", false, SMMenu.Network + 26)]
        private static void MakeWorldPortal() => MakeSelected("World Portal");

        [MenuItem(SMMenu.Root + "Network/Make Selected Prefab Networked/Interactable (merchant, chest, door, gate)", false, SMMenu.Network + 23)]
        private static void MakeInteractable() => MakeSelected("Interactable");

        [MenuItem(SMMenu.Root + "Network/Make All Mob Prefabs Networked", false, SMMenu.Network + 30)]
        private static void MakeAllMobs()
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                    continue;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<Mob>() != null)
                    paths.Add(path);
            }
            if (paths.Count == 0)
            {
                EditorUtility.DisplayDialog("Network", "No prefab with a Mob component found under Assets.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Network - mobs", $"Add the network components to {paths.Count} mob prefab(s)?\n\n" + string.Join("\n", paths.Take(20)) + (paths.Count > 20 ? "\n..." : ""), "Add", "Cancel"))
                return;
            var report = new StringBuilder();
            foreach (string path in paths)
                report.AppendLine(MakePrefab(path, "Mob"));
            Debug.Log("[Net] " + report);
        }

        private static void MakeSelected(string kind)
        {
            GameObject selected = Selection.activeGameObject;
            string path = selected != null ? AssetDatabase.GetAssetPath(selected) : null;
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("Network", "Select a prefab asset in the Project window first.", "OK");
                return;
            }
            string result = MakePrefab(path, kind);
            EditorUtility.DisplayDialog("Network - " + kind, result, "OK");
        }

        /// <summary>Adds a NetworkObject (and the module components of <paramref name="kind"/>) to a prefab asset.</summary>
        internal static string MakePrefab(string path, string kind)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            var added = new List<string>();
            var missing = new List<string>();
            var notes = new List<string>();
            try
            {
#if FISHNET
                NetworkObject nob = root.GetComponent<NetworkObject>();
                if (nob == null)
                {
                    nob = root.AddComponent<NetworkObject>();
                    added.Add("NetworkObject");
                }
                if (kind == "Player")
                    ConfigurePredictedPlayer(nob, notes);
#endif
                if (PrefabComponents.TryGetValue(kind, out string[] types))
                    AddByName(root, types, added, missing);
                if (kind == "Player")
                    WireInteractor(root, added);
                if (kind != "Player")
                    ConfigureServerTransforms(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            var sb = new StringBuilder(path).Append(": ");
            sb.Append(added.Count > 0 ? "added " + string.Join(", ", added) : "already networked");
            if (missing.Count > 0)
                sb.Append(". Not available (module not installed): ").Append(string.Join(", ", missing));
            foreach (string note in notes)
                sb.Append("\n• Note: ").Append(note).Append('.');
            return sb.ToString();
        }

        /// <summary>
        /// The player's interaction (doors, chests, NPCs, merchants, items): a PlayerInteractor on the player root (added
        /// when missing) referenced by the NetInteractor that relays it to the server.
        /// </summary>
        private static void WireInteractor(GameObject root, List<string> added)
        {
            PlayerInteractor interactor = root.GetComponentInChildren<PlayerInteractor>(true);
            if (interactor == null)
            {
                interactor = root.AddComponent<PlayerInteractor>();
                added.Add("PlayerInteractor");
            }
            foreach (MonoBehaviour b in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (b == null || b.GetType().FullName != "GameNet.NetInteractor")
                    continue;
                var so = new SerializedObject(b);
                SerializedProperty p = so.FindProperty("interactor");
                if (p != null && p.objectReferenceValue == null)
                {
                    p.objectReferenceValue = interactor;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

#if FISHNET
        /// <summary>
        /// Prediction settings of a player NetworkObject: predicted (type Other, the character controller), the model as
        /// the smoothed graphical object, state forwarding so other players are animated from the simulation, and a
        /// teleport threshold so travel and dungeon teleports snap instead of sliding.
        /// </summary>
        /// <summary>Value of Fish-Net's (internal) NetworkObject.PredictionType.Other.</summary>
        private const int PredictionTypeOther = 0;

        private static void ConfigurePredictedPlayer(NetworkObject nob, List<string> notes)
        {
            var so = new SerializedObject(nob);
            SetBool(so, "_enablePrediction", true);
            SetEnum(so, "_predictionType", PredictionTypeOther);
            SetBool(so, "_enableStateForwarding", true);
            SetBool(so, "_enableTeleport", true);
            // A dash covers more than 2 m per tick: only real jumps (travel, dungeon teleports) should snap.
            SetFloat(so, "_teleportThreshold", 5f);
            if (nob.GetComponent<CharacterController>() == null)
                notes.Add("the CharacterController should be on the prefab root (next to the NetworkObject)");
            // The smoothed model: a visual child, never the simulated transform or one of its parents. Fish-Net 4.6+ smooths
            // it with a NetworkTickSmoother on that child (the NetworkObject's own Graphical Object field is obsolete and
            // smooths poorly for the host); older versions only have the field.
            SerializedProperty graphical = so.FindProperty("_graphicalObject");
            Transform visual = graphical != null ? graphical.objectReferenceValue as Transform : null;
            if (visual == nob.transform)
                visual = null;
            Type tickSmoother = FindType(TickSmootherType);
            if (visual == null && UseBodySmoothing(nob, tickSmoother, notes))
            {
                if (graphical != null)
                    graphical.objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
            if (visual == null)
                visual = ChooseVisualChild(nob);
            if (visual == null)
            {
                notes.Add("no single child holds the model: its meshes are directly on the root, or spread between the root and the body the " +
                          "movement turns. Put the visible model (meshes and skeleton) under one child object - the Animator can stay where it is " +
                          "only if the bone paths stay the same - and run this again");
            }
            else if (tickSmoother != null)
            {
                ConfigureTickSmoother(visual, nob.transform, tickSmoother);
                if (graphical != null)
                    graphical.objectReferenceValue = null; // one smoother only
                notes.Add($"'{visual.name}' is smoothed by a NetworkTickSmoother (target: the root)");
            }
            else if (graphical != null)
            {
                graphical.objectReferenceValue = visual;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
#endif

        private const string TickSmootherType = "FishNet.Component.Transforming.Beta.NetworkTickSmoother";

#if FISHNET
        // The object that holds what is seen: the smallest child containing every mesh renderer of the player (cameras
        // and their children aside), when it is neither the root nor the simulated body or one of its parents; else the
        // first such Animator child.
        private static Transform ChooseVisualChild(NetworkObject nob)
        {
            Transform root = nob.transform;
            PlayerMovementModel model = nob.GetComponentInChildren<PlayerMovementModel>(true);
            Transform simulated = model != null ? model.PlayerTransform : null;
            bool Usable(Transform t) => t != null && t != root && t.IsChildOf(root) && (simulated == null || (t != simulated && !simulated.IsChildOf(t)));

            Transform common = null;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is SkinnedMeshRenderer || r is MeshRenderer) || r.GetComponentInParent<Camera>(true) != null)
                    continue;
                common = common == null ? r.transform : CommonAncestor(common, r.transform);
            }
            if (Usable(common))
                return common;

            foreach (Animator animator in nob.GetComponentsInChildren<Animator>(true))
                if (Usable(animator.transform))
                    return animator.transform;
            return null;
        }

        /// <summary>
        /// The model's meshes sit directly under the body the movement turns (PlayerMovementModel ▸ Player Transform, e.g.
        /// PlayerShellObject): no child holds only the visuals, and moving meshes or bones would break the Animator's
        /// bindings. NetPlayerMovement then smooths that body itself between ticks (Smooth Body), so any
        /// NetworkTickSmoother is removed. Also undoes an earlier version of this tool that made the root the turned
        /// transform (the root must never turn: cameras placed under it measure their rotation from it).
        /// </summary>
        private static bool UseBodySmoothing(NetworkObject nob, Type tickSmootherType, List<string> notes)
        {
            Transform root = nob.transform;
            PlayerMovementModel model = nob.GetComponentInChildren<PlayerMovementModel>(true);
            if (model == null)
                return false;
            Transform meshes = null;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if ((r is SkinnedMeshRenderer || r is MeshRenderer) && r.GetComponentInParent<Camera>(true) == null)
                    meshes = meshes == null ? r.transform : CommonAncestor(meshes, r.transform);
            if (meshes == null || meshes == root || !meshes.IsChildOf(root))
                return false;
            Transform body = model.PlayerTransform;
            if (body == root && tickSmootherType != null && meshes.GetComponent(tickSmootherType) != null)
            {
                var mso = new SerializedObject(model);
                SerializedProperty p = mso.FindProperty("playerTransform");
                if (p != null)
                {
                    p.objectReferenceValue = meshes;
                    mso.ApplyModifiedPropertiesWithoutUndo();
                    body = meshes;
                    notes.Add($"PlayerMovementModel ▸ Player Transform is '{meshes.name}' again (the root must not turn)");
                }
            }
            if (body == null || body == root || meshes != body)
                return false;
            if (tickSmootherType != null)
                foreach (Component c in root.GetComponentsInChildren(tickSmootherType, true))
                    UnityEngine.Object.DestroyImmediate(c, true);
            notes.Add($"the model is directly under '{body.name}', the body the movement turns: NetPlayerMovement smooths '{body.name}' between ticks " +
                      "(no NetworkTickSmoother needed - removed if there was one). An empty leftover object with an Animator can be deleted");
            return true;
        }

#endif

        private static Transform CommonAncestor(Transform a, Transform b)
        {
            for (Transform p = a; p != null; p = p.parent)
                if (b == p || b.IsChildOf(p))
                    return p;
            return null;
        }

        /// <summary>
        /// A NetworkTickSmoother on <paramref name="visual"/> following <paramref name="root"/> (which moves every tick): the
        /// owner smooths over one tick (most responsive), other players over two; jumps over 5 m snap (travel, portals).
        /// </summary>
        private static void ConfigureTickSmoother(Transform visual, Transform root, Type smootherType)
        {
            foreach (Component other in root.GetComponentsInChildren(smootherType, true))
                if (other.transform != visual)
                    UnityEngine.Object.DestroyImmediate(other, true);
            Component smoother = visual.GetComponent(smootherType);
            if (smoother == null)
                smoother = visual.gameObject.AddComponent(smootherType);
            var so = new SerializedObject(smoother);
            SerializedProperty target = so.FindProperty("_initializationSettings.TargetTransform");
            if (target != null)
                target.objectReferenceValue = root;
            SetMovement(so, "_controllerMovementSettings", 1);
            SetMovement(so, "_spectatorMovementSettings", 2);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetMovement(SerializedObject so, string settings, int interpolation)
        {
            SetBool(so, settings + ".EnableTeleport", true);
            SetFloat(so, settings + ".TeleportThreshold", 5f);
            SetEnum(so, settings + ".AdaptiveInterpolationValue", 0); // off: a fixed, short delay
            SerializedProperty p = so.FindProperty(settings + ".InterpolationValue");
            if (p != null)
                p.intValue = interpolation;
        }

        /// <summary>
        /// NetworkTransforms of mobs, summons and items follow the server (Fish-Net's default is client authoritative),
        /// snap on teleports and do not extrapolate.
        /// </summary>
        private static void ConfigureServerTransforms(GameObject root)
        {
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c.GetType().FullName != "FishNet.Component.Transforming.NetworkTransform")
                    continue;
                var so = new SerializedObject(c);
                SetBool(so, "_clientAuthoritative", false);
                SetBool(so, "_enableTeleport", true);
                SetFloat(so, "_teleportThreshold", 5f);
                SerializedProperty extrapolation = so.FindProperty("_extrapolation");
                if (extrapolation != null)
                    extrapolation.intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null) p.boolValue = value;
        }

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null) p.floatValue = value;
        }

        private static void SetEnum(SerializedObject so, string name, int value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null) p.intValue = value;
        }

        /// <summary>Adds components by full type name when the type exists (modules can be missing).</summary>
        internal static void AddByName(GameObject go, IEnumerable<string> typeNames, List<string> added, List<string> missing = null)
        {
            foreach (string name in typeNames)
            {
                Type t = FindType(name);
                if (t == null || !typeof(Component).IsAssignableFrom(t))
                {
                    missing?.Add(name);
                    continue;
                }
                if (go.GetComponent(t) != null)
                    continue;
                go.AddComponent(t);
                added?.Add(t.Name);
            }
        }

        internal static Type FindType(string fullName)
        {
            // Every name looked up here is a component (Fish-Net's, the network modules'): Unity's type cache knows them all.
            foreach (Type t in TypeCache.GetTypesDerivedFrom<Component>())
                if (t.FullName == fullName)
                    return t;
            return Type.GetType(fullName, false);
        }

        internal static GameObject FindPlayerPrefab()
        {
#if FISHNET
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<NetPlayer>() != null && prefab.GetComponent<NetworkObject>() != null)
                    return prefab;
            }
#endif
            return null;
        }

        // ------------------------------------------------------------------ local builds and processes

        [MenuItem(SMMenu.Root + "Network/Build/Local Dedicated Server (headless)", false, SMMenu.Network + 40)]
        public static void BuildServer() => Build(true);

        [MenuItem(SMMenu.Root + "Network/Build/Client", false, SMMenu.Network + 41)]
        public static void BuildClient() => Build(false);

        [MenuItem(SMMenu.Root + "Network/Build/Server And Client", false, SMMenu.Network + 42)]
        public static void BuildBoth()
        {
            if (Build(true))
                Build(false);
        }

        private static bool Build(bool server)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Network - build", "No scenes in File ▸ Build Settings. Create the Bootstrap scene first.", "OK");
                return false;
            }
            BuildTarget target = DesktopTarget();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                target = target,
                locationPathName = ExecutablePath(server, target),
                options = server ? BuildOptions.None : BuildOptions.None,
            };
            if (server)
                options.subtarget = (int)StandaloneBuildSubtarget.Server;
            Directory.CreateDirectory(Path.GetDirectoryName(options.locationPathName) ?? BuildsFolder);
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            Debug.Log(ok ? $"[Net] Built the {(server ? "dedicated server" : "client")}: {options.locationPathName}"
                         : $"[Net] The {(server ? "server" : "client")} build failed ({report.summary.result}). For a server build install the 'Dedicated Server' module for your platform (Unity Hub ▸ Installs ▸ Add modules).");
            return ok;
        }

        private static BuildTarget DesktopTarget()
        {
            BuildTarget active = EditorUserBuildSettings.activeBuildTarget;
            switch (active)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneLinux64:
                case BuildTarget.StandaloneOSX:
                    return active;
            }
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor: return BuildTarget.StandaloneOSX;
                case RuntimePlatform.LinuxEditor: return BuildTarget.StandaloneLinux64;
                default: return BuildTarget.StandaloneWindows64;
            }
        }

        private static string ExecutablePath(bool server, BuildTarget target)
        {
            string name = server ? "Server" : "Client";
            string folder = Path.Combine(BuildsFolder, name);
            switch (target)
            {
                case BuildTarget.StandaloneOSX: return Path.Combine(folder, name + ".app");
                case BuildTarget.StandaloneLinux64: return Path.Combine(folder, name + ".x86_64");
                default: return Path.Combine(folder, name + ".exe");
            }
        }

        private static string LaunchPath(bool server)
        {
            BuildTarget target = DesktopTarget();
            string path = ExecutablePath(server, target);
            if (target == BuildTarget.StandaloneOSX)
            {
                string name = server ? "Server" : "Client";
                path = Path.Combine(path, "Contents", "MacOS", name);
            }
            return Path.GetFullPath(path);
        }

        private static readonly List<Process> launched = new List<Process>();

        [MenuItem(SMMenu.Root + "Network/Run/Local Dedicated Server", false, SMMenu.Network + 60)]
        public static void LaunchServer() => Launch(true, NetLaunchOptions.BuildArguments(NetMode.Server, null, NetLaunchOptions.DefaultPort, null, true), "Server");

        [MenuItem(SMMenu.Root + "Network/Run/Client (joins 127.0.0.1)", false, SMMenu.Network + 61)]
        public static void LaunchClient() => Launch(false, NetLaunchOptions.BuildArguments(NetMode.Client, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort, "Client" + (launched.Count + 1)), "Client");

        [MenuItem(SMMenu.Root + "Network/Run/Server And Two Clients", false, SMMenu.Network + 62)]
        public static void LaunchServerAndClients()
        {
            LaunchServer();
            LaunchClient();
            LaunchClient();
        }

        [MenuItem(SMMenu.Root + "Network/Run/Stop Launched Processes", false, SMMenu.Network + 80)]
        public static void StopLaunched()
        {
            int n = 0;
            foreach (Process p in launched)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        p.Kill();
                        n++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Net] " + e.Message);
                }
            }
            launched.Clear();
            Debug.Log($"[Net] Stopped {n} process(es).");
        }

        private static void Launch(bool server, string arguments, string label)
        {
            string exe = LaunchPath(server);
            if (!File.Exists(exe))
            {
                if (EditorUtility.DisplayDialog("Network - run", $"{exe} does not exist yet. Build it now?", "Build", "Cancel"))
                {
                    if (!Build(server))
                        return;
                }
                else
                    return;
            }
            try
            {
                string log = Path.GetFullPath(Path.Combine(BuildsFolder, $"{label}-{launched.Count + 1}.log"));
                var info = new ProcessStartInfo(exe, arguments + $" -logFile \"{log}\"")
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
                };
                Process p = Process.Start(info);
                if (p != null)
                    launched.Add(p);
                Debug.Log($"[Net] Launched {label}: {exe} {arguments} (log: {log})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Net] Could not launch {exe}: {e.Message}");
            }
        }

        // ------------------------------------------------------------------ play in the editor

        [MenuItem(SMMenu.Root + "Network/Play In Editor As/Host", false, SMMenu.Network + 100)]
        private static void PlayHost() => PlayAs(NetMode.Host);

        [MenuItem(SMMenu.Root + "Network/Play In Editor As/Client (joins 127.0.0.1)", false, SMMenu.Network + 101)]
        private static void PlayClient() => PlayAs(NetMode.Client);

        [MenuItem(SMMenu.Root + "Network/Play In Editor As/Dedicated Server", false, SMMenu.Network + 102)]
        private static void PlayServer() => PlayAs(NetMode.Server);

        [MenuItem(SMMenu.Root + "Network/Play In Editor As/Single Player", false, SMMenu.Network + 103)]
        private static void PlayOffline() => PlayAs(NetMode.Offline);

        private static void PlayAs(NetMode mode)
        {
            SceneAsset boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapPath);
            if (boot == null)
            {
                EditorUtility.DisplayDialog("Network", $"No Bootstrap scene at {BootstrapPath}. Use 'Create Bootstrap Scene' first.", "OK");
                return;
            }
            SessionState.SetString(EditorModeKey, mode.ToString());
            EditorSceneManager.playModeStartScene = boot;
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void ResetPlayModeStartScene()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && !string.IsNullOrEmpty(SessionState.GetString(EditorModeKey, "")))
                {
                    SessionState.EraseString(EditorModeKey);
                    EditorSceneManager.playModeStartScene = null;
                }
            };
        }

        // ------------------------------------------------------------------ validation

        [MenuItem(SMMenu.Root + "Validate/Network Setup", false, SMMenu.Validate + 26)]
        public static void Validate()
        {
            List<string> problems = CollectSetupProblems();
            EditorUtility.DisplayDialog("Network setup", problems.Count > 0
                ? string.Join("\n", problems.Select(p => "• " + p))
                : "Everything needed for local network sessions is in place.", "OK");
        }

        /// <summary>What is missing for local network sessions (also listed by the Project Validator window).</summary>
        internal static List<string> CollectSetupProblems()
        {
            var problems = new List<string>();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || !scenes[0].path.EndsWith("Bootstrap.unity", StringComparison.Ordinal))
                problems.Add("The Bootstrap scene should be the first scene in File ▸ Build Settings.");
            HubWorld.HubWorldSettings settings = HubWorld.HubWorldSettings.Instance;
            if (settings != null)
            {
                if (!scenes.Any(s => Path.GetFileNameWithoutExtension(s.path) == settings.citySceneName))
                    problems.Add($"The City scene '{settings.citySceneName}' is not in the build.");
                if (!string.IsNullOrEmpty(settings.worldSceneName) && !scenes.Any(s => Path.GetFileNameWithoutExtension(s.path) == settings.worldSceneName))
                    problems.Add($"The World scene '{settings.worldSceneName}' is not in the build.");
                string dungeonScene = settings.dungeonScenes != null ? settings.dungeonScenes.networkSceneName : null;
                if (string.IsNullOrEmpty(dungeonScene) || !scenes.Any(s => Path.GetFileNameWithoutExtension(s.path) == dungeonScene))
                    problems.Add("No dungeon instance scene in the build (Create Dungeon Instance Scene): networked dungeons need it.");
            }
#if FISHNET
            GameObject player = FindPlayerPrefab();
            if (player == null)
                problems.Add("No network player prefab (a prefab with NetworkObject + NetPlayer). Use Make Selected Prefab Networked ▸ Player.");
            else
            {
                foreach (string t in PrefabComponents["Player"])
                    if (FindType(t) != null && player.GetComponent(FindType(t)) == null)
                        problems.Add($"The player prefab '{player.name}' has no {t.Substring(t.LastIndexOf('.') + 1)}.");
            }
            GameObject carrier = SMAssets.Load<GameObject>(SMAssets.Networking, "NetWorldItem");
            if (carrier == null || carrier.GetComponent<NetworkObject>() == null)
                problems.Add("No networked NetWorldItem prefab in Resources/SimpleMovements/Networking (Create World Item Prefab): dropped items and loot stay on the server.");
            if (SMAssets.Load<ItemDatabase>(SMAssets.Databases, ItemDatabase.ResourcesName) == null)
                problems.Add("No Item Database in Resources/SimpleMovements/Databases (Build Item Database): items cannot be named over the network.");
            NetPlayerSpawner spawner = UnityEngine.Object.FindAnyObjectByType<NetPlayerSpawner>(FindObjectsInactive.Include);
            if (spawner != null && spawner.playerPrefab == null)
                problems.Add("The Net Player Spawner (open scene) has no player prefab.");
#else
            problems.Add("Fish-Net is not installed (no FISHNET define): the game runs in single player only.");
#endif
            return problems;
        }

        [MenuItem(SMMenu.Root + "Network/Help", false, SMMenu.Network + 140)]
        private static void Help()
        {
            EditorUtility.DisplayDialog("Network",
                "1. Install Fish-Net (Package Manager or Asset Store).\n" +
                "2. Create Bootstrap Scene, Create Dungeon Instance Scene.\n" +
                "3. Make your player prefab networked (Player) and your mob prefabs (Make All Mob Prefabs Networked).\n" +
                "4. Play In Editor As ▸ Host, or Build ▸ Server And Client, then Run ▸ Server And Two Clients.\n\n" +
                "Everything runs on this machine (127.0.0.1). See Documentation/Networking/README.md.", "OK");
        }

        // ------------------------------------------------------------------ helpers

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                AssetDatabase.CreateFolder(string.IsNullOrEmpty(parent) ? "Assets" : parent, Path.GetFileName(folder));
            }
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>Adds a scene to the build (at <paramref name="index"/>, or at the end with -1) if it is not there.</summary>
        internal static void EnsureBuildScene(string path, int index)
        {
            var list = EditorBuildSettings.scenes.ToList();
            list.RemoveAll(s => s.path == path);
            var entry = new EditorBuildSettingsScene(path, true);
            if (index < 0 || index > list.Count)
                list.Add(entry);
            else
                list.Insert(index, entry);
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
