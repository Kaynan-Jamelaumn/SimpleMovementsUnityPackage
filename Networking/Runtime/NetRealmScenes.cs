#if FISHNET
using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using HubWorld;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace GameNet
{
    /// <summary>
    /// The scenes of a networked session, through Fish-Net's scene manager:
    /// <list type="bullet">
    /// <item>The server loads the City when it starts and keeps it; every client that joins loads it, and gets its
    /// player there (<see cref="ClientEnteredCity"/>, used by <see cref="NetPlayerSpawner"/>).</item>
    /// <item>Realm travel and dungeons move players with <see cref="NetScenes.MoveToScene"/>: the server moves the object
    /// at once, the owner's client loads the destination (a stacked dungeon instance has its own handle) and follows,
    /// then unloads the scenes it no longer needs. The server keeps its scenes until the hub world or the dungeon
    /// session unloads them.</item>
    /// <item>Players placed in scenes for single player (no NetworkObject) are removed in network sessions.</item>
    /// </list>
    /// Implements <see cref="NetScenes.IProvider"/> while the server runs.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetRealmScenes : MonoBehaviour, NetScenes.IProvider
    {
        [Tooltip("City scene name. Empty = the one in the Hub World Settings.")]
        public string citySceneName = "";

        public static NetRealmScenes Instance { get; private set; }

        /// <summary>A client finished loading the City and is in it (server side).</summary>
        public event Action<NetworkConnection> ClientEnteredCity;

        /// <summary>A client finished loading a scene and was added to it (server side).</summary>
        public event Action<NetworkConnection, Scene> ClientEnteredScene;

        /// <summary>This client finished loading a scene (client side).</summary>
        public event Action<Scene> LocalSceneLoaded;

        /// <summary>The City scene on the server (default until loaded).</summary>
        public Scene CityScene { get; private set; }

        private NetworkManager manager;
        private FishNet.Managing.Scened.SceneManager scenes;
        private readonly Dictionary<SceneLoadData, Action<Scene>> pendingLoads = new Dictionary<SceneLoadData, Action<Scene>>();
        private readonly Dictionary<SceneUnloadData, Action> pendingUnloads = new Dictionary<SceneUnloadData, Action>();
        /// <summary>Per connection: the scene its player is heading for, and the scenes to leave once it arrived.</summary>
        private readonly Dictionary<int, (Scene target, List<Scene> leave)> moves = new Dictionary<int, (Scene, List<Scene>)>();
        private bool cityRequested;

        private string CityName => !string.IsNullOrEmpty(citySceneName) ? citySceneName : HubWorldSettings.Instance.citySceneName;

        private void Awake()
        {
            Instance = this;
            manager = GetComponentInParent<NetworkManager>();
        }

        private void OnEnable()
        {
            if (manager == null)
                manager = GetComponentInParent<NetworkManager>();
            if (manager == null)
                return;
            scenes = manager.SceneManager;
            scenes.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
            scenes.OnClientPresenceChangeEnd += OnPresenceChanged;
            scenes.OnLoadEnd += OnLoadEnd;
            scenes.OnUnloadEnd += OnUnloadEnd;
            UnitySceneManager.sceneLoaded += OnUnitySceneLoaded;
            NetAuthority.ModeChanged += OnModeChanged;
            OnModeChanged(NetAuthority.Mode);
        }

        private void OnDisable()
        {
            if (scenes != null)
            {
                scenes.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
                scenes.OnClientPresenceChangeEnd -= OnPresenceChanged;
                scenes.OnLoadEnd -= OnLoadEnd;
                scenes.OnUnloadEnd -= OnUnloadEnd;
            }
            UnitySceneManager.sceneLoaded -= OnUnitySceneLoaded;
            NetAuthority.ModeChanged -= OnModeChanged;
            if (ReferenceEquals(NetScenes.Provider, this))
                NetScenes.Provider = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnModeChanged(NetMode mode)
        {
            bool server = mode == NetMode.Host || mode == NetMode.Server;
            if (server)
            {
                NetScenes.Provider = this;
                LoadCityOnServer();
            }
            else
            {
                if (ReferenceEquals(NetScenes.Provider, this))
                    NetScenes.Provider = null;
                cityRequested = false;
                moves.Clear();
            }
        }

        // ------------------------------------------------------------------ the City

        private void LoadCityOnServer()
        {
            if (cityRequested || scenes == null)
                return;
            cityRequested = true;
            string city = CityName;
            if (string.IsNullOrEmpty(city))
            {
                Debug.LogError("[Net] No City scene named (Hub World Settings ▸ City Scene Name).", this);
                return;
            }
            Scene loaded = FindLoaded(city);
            if (loaded.IsValid())
            {
                CityScene = loaded; // started from the City scene itself (testing): it is already there
                RemoveOfflinePlayers(loaded);
            }
            var sld = new SceneLoadData(city) { ReplaceScenes = ReplaceOption.None };
            sld.Options.AutomaticallyUnload = false;
            sld.PreferredActiveScene = new PreferredScene(new SceneLookupData(city));
            Load(sld, s =>
            {
                if (s.IsValid())
                    CityScene = s;
                else if (!CityScene.IsValid())
                    Debug.LogError($"[Net] The City scene '{city}' could not be loaded - add it to File ▸ Build Settings.", this);
            });
        }

        private void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (!asServer || conn == null || !conn.IsActive)
                return;
            string city = CityName;
            var sld = new SceneLoadData(CityScene.IsValid() ? new SceneLookupData(CityScene) : new SceneLookupData(city)) { ReplaceScenes = ReplaceOption.None };
            sld.Options.AutomaticallyUnload = false;
            sld.PreferredActiveScene = new PreferredScene(sld.SceneLookupDatas[0]);
            scenes.LoadConnectionScenes(conn, sld);
        }

        private void OnPresenceChanged(ClientPresenceChangeEventArgs args)
        {
            if (!args.Added || args.Connection == null)
                return;
            NetworkConnection conn = args.Connection;
            ClientEnteredScene?.Invoke(conn, args.Scene);
            if (args.Scene == CityScene || (!CityScene.IsValid() && args.Scene.name == CityName))
                ClientEnteredCity?.Invoke(conn);

            // A player arrived where it was sent: its client may drop the scenes it left.
            if (moves.TryGetValue(conn.ClientId, out var move) && move.target == args.Scene)
            {
                moves.Remove(conn.ClientId);
                LeaveScenes(conn, move.leave);
            }
        }

        private void LeaveScenes(NetworkConnection conn, List<Scene> leave)
        {
            if (conn == null || !conn.IsActive || leave == null || leave.Count == 0)
                return;
            var still = new List<Scene>();
            foreach (Scene s in leave)
                if (s.IsValid() && conn.Scenes.Contains(s) && !OwnsObjectIn(conn, s))
                    still.Add(s);
            if (still.Count == 0)
                return;
            var sud = new SceneUnloadData(still.ToArray());
            sud.Options.Mode = UnloadOptions.ServerUnloadMode.KeepUnused; // the server decides itself when a realm goes
            scenes.UnloadConnectionScenes(conn, sud);
        }

        private static bool OwnsObjectIn(NetworkConnection conn, Scene scene)
        {
            foreach (NetworkObject nob in conn.Objects)
                if (nob != null && nob.gameObject.scene == scene)
                    return true;
            return false;
        }

        // ------------------------------------------------------------------ NetScenes.IProvider

        public bool Handles(GameObject root)
        {
            if (root == null || manager == null || !manager.ServerManager.Started)
                return false;
            NetworkObject nob = root.GetComponent<NetworkObject>();
            return nob != null && nob.IsServerInitialized;
        }

        public void MoveToScene(GameObject root, Scene scene)
        {
            NetworkObject nob = root != null ? root.GetComponent<NetworkObject>() : null;
            if (nob == null || !scene.IsValid() || !scene.isLoaded)
                return;
            if (root.scene != scene)
                UnitySceneManager.MoveGameObjectToScene(root, scene);

            NetworkConnection conn = nob.Owner;
            if (conn != null && conn.IsActive)
            {
                var leave = conn.Scenes.Where(s => s != scene).ToList();
                moves[conn.ClientId] = (scene, leave);
                var sld = new SceneLoadData(scene) { ReplaceScenes = ReplaceOption.None, MovedNetworkObjects = new[] { nob } };
                sld.Options.AutomaticallyUnload = false;
                sld.Options.AllowStacking = ProceduralDungeon.DungeonScenes.Find(scene) != null;
                sld.Options.LocalPhysics = scene.GetPhysicsScene() != Physics.defaultPhysicsScene ? LocalPhysicsMode.Physics3D : LocalPhysicsMode.None;
                sld.PreferredActiveScene = new PreferredScene(new SceneLookupData(scene));
                if (conn.Scenes.Contains(scene))
                {
                    moves.Remove(conn.ClientId);
                    LeaveScenes(conn, leave);
                }
                else
                    scenes.LoadConnectionScenes(conn, sld);
            }
            manager.ServerManager.Objects.RebuildObservers(nob);
        }

        public void Load(string sceneName, LocalPhysicsMode physics, bool allowStacking, Action<Scene> done)
        {
            var sld = new SceneLoadData(sceneName) { ReplaceScenes = ReplaceOption.None };
            sld.Options.AutomaticallyUnload = false;
            sld.Options.AllowStacking = allowStacking;
            sld.Options.LocalPhysics = physics;
            Load(sld, done);
        }

        private void Load(SceneLoadData sld, Action<Scene> done)
        {
            if (scenes == null)
            {
                done?.Invoke(default);
                return;
            }
            pendingLoads[sld] = done;
            scenes.LoadConnectionScenes(sld); // server only; clients load it when one of their players is moved in
        }

        public void Unload(Scene scene, Action done)
        {
            if (scenes == null || !scene.IsValid())
            {
                done?.Invoke();
                return;
            }
            // Clients still in the scene unload it too; the server unloads it once nobody needs it.
            if (scenes.SceneConnections.TryGetValue(scene, out HashSet<NetworkConnection> conns) && conns.Count > 0)
            {
                var forClients = new SceneUnloadData(scene);
                forClients.Options.Mode = UnloadOptions.ServerUnloadMode.KeepUnused;
                scenes.UnloadConnectionScenes(conns.ToArray(), forClients);
            }
            var sud = new SceneUnloadData(scene);
            sud.Options.Mode = UnloadOptions.ServerUnloadMode.UnloadUnused;
            pendingUnloads[sud] = done;
            scenes.UnloadConnectionScenes(sud);
        }

        private void OnLoadEnd(SceneLoadEndEventArgs args)
        {
            if (!args.QueueData.AsServer)
            {
                if (args.LoadedScenes != null)
                    foreach (Scene s in args.LoadedScenes)
                    {
                        RemoveOfflinePlayers(s);
                        LocalSceneLoaded?.Invoke(s);
                    }
                return;
            }
            SceneLoadData sld = args.QueueData.SceneLoadData;
            if (args.LoadedScenes != null)
                foreach (Scene s in args.LoadedScenes)
                    RemoveOfflinePlayers(s);
            if (sld == null || !pendingLoads.TryGetValue(sld, out Action<Scene> done))
                return;
            pendingLoads.Remove(sld);
            Scene result = default;
            if (args.LoadedScenes != null && args.LoadedScenes.Length > 0)
                result = args.LoadedScenes[0];
            else if (sld.SceneLookupDatas.Length > 0)
                result = sld.SceneLookupDatas[0].GetScene(out _);
            try
            {
                done?.Invoke(result);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void OnUnloadEnd(SceneUnloadEndEventArgs args)
        {
            if (!args.QueueData.AsServer)
                return;
            SceneUnloadData sud = args.QueueData.SceneUnloadData;
            if (sud == null || !pendingUnloads.TryGetValue(sud, out Action done))
                return;
            pendingUnloads.Remove(sud);
            try
            {
                done?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        // ------------------------------------------------------------------ helpers

        private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (NetAuthority.IsNetworked)
                RemoveOfflinePlayers(scene);
        }

        /// <summary>
        /// Removes players placed in a scene for single player during network sessions: every player comes from the
        /// Net Player Spawner. A placed player is removed even when its prefab was made networked (it then has a
        /// NetworkObject and would be spawned as a scene object, standing where the real players appear).
        /// </summary>
        private void RemoveOfflinePlayers(Scene scene)
        {
            if (!NetAuthority.IsNetworked || !scene.IsValid() || !scene.isLoaded)
                return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!IsPlayer(root))
                    continue;
                NetworkObject nob = root.GetComponent<NetworkObject>();
                if (nob == null)
                    nob = root.GetComponentInChildren<NetworkObject>(true);
                if (nob != null && !nob.IsSceneObject)
                    continue; // a spawned player
                Debug.Log($"[Net] Removing the player '{root.name}' placed in '{scene.name}' for single player: in network sessions players come from the Net Player Spawner.", this);
                if (nob == null)
                {
                    Destroy(root);
                }
                else
                {
                    // Fish-Net registers scene objects right after the load: hide it now, despawn (server) or drop it once that is done.
                    root.SetActive(false);
                    StartCoroutine(RemoveScenePlayer(root, nob));
                }
            }
        }

        private System.Collections.IEnumerator RemoveScenePlayer(GameObject root, NetworkObject nob)
        {
            yield return null;
            if (root == null)
                yield break;
            if (nob != null && nob.IsSpawned && manager != null && manager.ServerManager.Started)
                manager.ServerManager.Despawn(root, DespawnType.Destroy);
            else if (nob == null || !nob.IsSpawned)
                Destroy(root);
            // A spawned scene object on a client is despawned by the server.
        }

        private static bool IsPlayer(GameObject root)
        {
            if (root.GetComponentInChildren<PlayerStatusController>(true) != null || root.GetComponentInChildren<NetPlayer>(true) != null)
                return true;
            foreach (CombatEntity e in root.GetComponentsInChildren<CombatEntity>(true))
                if (e.Kind == CombatEntity.EntityKind.Player)
                    return true;
            return false;
        }

        private static Scene FindLoaded(string nameOrPath)
        {
            for (int i = UnitySceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = UnitySceneManager.GetSceneAt(i);
                if (s.isLoaded && (s.name == nameOrPath || s.path == nameOrPath || s.path.EndsWith("/" + nameOrPath + ".unity", StringComparison.Ordinal)))
                    return s;
            }
            return default;
        }
    }
}
#endif
