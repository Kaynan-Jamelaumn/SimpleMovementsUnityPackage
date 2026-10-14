using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProceduralDungeon
{
    /// <summary>Where dungeon scenes are placed and how they simulate physics (see <see cref="DungeonScenes"/>).</summary>
    [Serializable]
    public class DungeonSceneSettings
    {
        [Tooltip("Each dungeon scene runs its OWN physics simulation (LocalPhysicsMode.Physics3D): nothing in one dungeon " +
                 "touches the world or another dungeon. Gameplay queries are routed by ScenePhysics. Off = the scenes share " +
                 "the default physics scene and are only kept apart by their slots.")]
        public bool ownPhysics = true;

        [Tooltip("Centre of the first dungeon slot. Every running dungeon gets its own slot, so their navigation meshes, " +
                 "lights and renderers never overlap each other - nor the world, when it is loaded in the same process " +
                 "(keep the slots below the lowest terrain). A dedicated dungeon host without a world can use (0, 0, 0).")]
        public Vector3 slotOrigin = new Vector3(0f, -4096f, 0f);

        [Tooltip("Size of one slot (metres): a cube this wide around the slot centre belongs to that dungeon. Must hold the " +
                 "largest dungeon (floors of ~100-200 m and their stacked floors).")]
        [Min(256f)] public float slotSize = 2048f;

        [Tooltip("Slots per row; the rows are laid out on a square grid centred on Slot Origin. 16 = up to 256 dungeons.")]
        [Range(1, 64)] public int slotsPerRow = 16;

        [Tooltip("Network sessions: the (empty) scene loaded once per dungeon run through Fish-Net's scene manager (scene " +
                 "stacking), so the clients of the run load the same instance. Must be in File ▸ Build Settings " +
                 "(Tools ▸ SimpleMovements ▸ Network ▸ Create Dungeon Instance Scene). Single player creates scenes at runtime instead.")]
        public string networkSceneName = "DungeonInstance";
    }

    /// <summary>
    /// One dungeon per scene. When <see cref="Enabled"/>, <see cref="DungeonSession"/> builds every dungeon run in a
    /// scene of its own (created at runtime), placed in a free slot, with its own physics simulation when
    /// <see cref="DungeonSceneSettings.ownPhysics"/> is on; the scene is unloaded when the run ends. Off (the default),
    /// dungeons are built in the current scene at the portal's Dungeon Origin, as before.
    /// </summary>
    /// <remarks>
    /// A dungeon scene with its own physics is simulated here (fixed timestep) and registers its slot with
    /// <see cref="ScenePhysics"/> so raycasts and overlaps from inside it reach its colliders. NavMesh is not per scene
    /// in Unity, which is why each dungeon also gets its own place in space.
    /// <para>Network sessions: a server loads <see cref="DungeonSceneSettings.networkSceneName"/> once per run through
    /// Fish-Net (scene stacking, <see cref="CreateAsync"/>), so the clients of the run load the same instance when their
    /// player is moved in. A client adopts such a scene when it loads (<see cref="Lease.Remote"/>), so <see cref="Find"/>
    /// works there too, and learns its slot from the run's header (<see cref="PlaceRemote"/>).</para>
    /// </remarks>
    public static class DungeonScenes
    {
        /// <summary>A scene lent to one dungeon run.</summary>
        public sealed class Lease
        {
            public Scene Scene { get; internal set; }
            /// <summary>Slot index (0 = first).</summary>
            public int Slot { get; internal set; }
            /// <summary>Where the dungeon is built (its manager's position).</summary>
            public Vector3 Origin { get; internal set; }
            /// <summary>The space that belongs to this dungeon (registered with <see cref="ScenePhysics"/>).</summary>
            public Bounds Bounds { get; internal set; }
            public bool OwnPhysics { get; internal set; }
            public bool Released { get; internal set; }
            /// <summary>The scene is still loading through the network (<see cref="CreateAsync"/>).</summary>
            public bool Pending { get; internal set; }
            /// <summary>Loaded through the network (unloaded through it too).</summary>
            public bool Networked { get; internal set; }
            /// <summary>A network client's copy of a server's dungeon scene (the server decides when it goes).</summary>
            public bool Remote { get; internal set; }
        }

        /// <summary>
        /// Build dungeons in scenes of their own (set by your game setup, e.g. the hub world director). Always true during
        /// a network session that has a dungeon instance scene (<see cref="DungeonSceneSettings.networkSceneName"/>): there
        /// every run gets a scene of its own whatever was set, since a dungeon built into a shared realm scene could not be
        /// shown to its own party only.
        /// </summary>
        public static bool Enabled
        {
            get => enabled || NetworkRuns;
            set => enabled = value;
        }

        /// <summary>A network session with a dungeon instance scene set: runs use stacked copies of it (server and clients).</summary>
        private static bool NetworkRuns =>
            GameNet.NetAuthority.IsNetworked && !string.IsNullOrEmpty((Settings ?? new DungeonSceneSettings()).networkSceneName);

        private static bool enabled;

        public static DungeonSceneSettings Settings { get; set; } = new DungeonSceneSettings();

        private static readonly List<Lease> leases = new List<Lease>();
        private static int created;
        private static PhysicsDriver driver;

        /// <summary>The scenes currently lent.</summary>
        public static IReadOnlyList<Lease> Active => leases;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            enabled = false;
            Settings = new DungeonSceneSettings();
            leases.Clear();
            created = 0;
            driver = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        /// <summary>
        /// Are run scenes loaded through the network right now (a server in a session, with the network scene set)? Then
        /// leases come from <see cref="CreateAsync"/> only.
        /// </summary>
        public static bool LoadsThroughNetwork =>
            GameNet.NetScenes.ThroughNetwork && !string.IsNullOrEmpty((Settings ?? new DungeonSceneSettings()).networkSceneName);

        /// <summary>
        /// Gets a scene for one dungeon run. Offline it is created at once (<see cref="Create"/>) and
        /// <paramref name="done"/> runs before this returns. On a server in a network session the template scene is loaded
        /// through Fish-Net as a stacked instance of its own and <paramref name="done"/> runs once it exists. Null when
        /// every slot is taken or the scene could not be loaded.
        /// </summary>
        public static void CreateAsync(string label, Action<Lease> done)
        {
            if (!LoadsThroughNetwork)
            {
                done?.Invoke(Create(label));
                return;
            }
            DungeonSceneSettings s = Settings ?? new DungeonSceneSettings();
            int capacity = Mathf.Max(1, s.slotsPerRow) * Mathf.Max(1, s.slotsPerRow);
            int slot = FreeSlot(capacity);
            if (slot < 0)
            {
                Debug.LogError($"DungeonScenes: all {capacity} dungeon slots are in use.");
                done?.Invoke(null);
                return;
            }
            created++;
            // The slot is taken now, so two runs starting together never share one.
            var lease = new Lease
            {
                Slot = slot,
                Origin = SlotCenter(slot, s),
                Bounds = SlotBounds(slot, s),
                OwnPhysics = s.ownPhysics,
                Pending = true,
                Networked = true,
            };
            leases.Add(lease);
            string sceneName = s.networkSceneName;
            GameNet.NetScenes.LoadAdditive(sceneName, s.ownPhysics ? LocalPhysicsMode.Physics3D : LocalPhysicsMode.None, true, scene =>
            {
                lease.Pending = false;
                if (lease.Released)
                {
                    // The run ended while its scene was loading.
                    if (scene.IsValid() && scene.isLoaded)
                        GameNet.NetScenes.Unload(scene, null);
                    return;
                }
                if (!scene.IsValid() || !scene.isLoaded || Find(scene) != null)
                {
                    leases.Remove(lease);
                    lease.Released = true;
                    Debug.LogError($"DungeonScenes: the dungeon instance scene '{sceneName}' could not be loaded through the network " +
                                   "(add it to File > Build Settings: Tools > SimpleMovements > Network > Create Dungeon Instance Scene).");
                    done?.Invoke(null);
                    return;
                }
                lease.Scene = scene;
                if (lease.OwnPhysics)
                {
                    ScenePhysics.RegisterRegion(scene, lease.Bounds);
                    EnsureDriver();
                }
                if (!HasNetworkRelay(scene))
                    Debug.LogError($"DungeonScenes: the dungeon instance scene '{sceneName}' has no GameNet.NetDungeonRun (on a NetworkObject): " +
                                   "clients will not see the dungeon. Recreate it with Tools > SimpleMovements > Network > Create Dungeon Instance Scene.");
                done?.Invoke(lease);
            });
        }

        /// <summary>
        /// A network client: the lease of a dungeon instance scene it loaded because its player was moved in (adopted when
        /// the scene loads; null when it is not one).
        /// </summary>
        public static Lease Adopt(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;
            Lease existing = Find(scene);
            if (existing != null)
                return existing;
            var lease = new Lease
            {
                Scene = scene,
                Slot = -1,
                Origin = Vector3.zero,
                Bounds = new Bounds(Vector3.zero, Vector3.zero),
                OwnPhysics = scene.GetPhysicsScene() != Physics.defaultPhysicsScene,
                Networked = true,
                Remote = true,
            };
            leases.Add(lease);
            // A scene with physics of its own is only simulated by somebody stepping it (rigidbodies, debris, platforms).
            if (lease.OwnPhysics)
                EnsureDriver();
            return lease;
        }

        /// <summary>
        /// A network client: where the server placed the dungeon of an adopted scene (from the run's header), so physics
        /// queries made inside it reach its colliders as on the server.
        /// </summary>
        public static void PlaceRemote(Scene scene, int slot, Vector3 origin, float slotSize)
        {
            Lease lease = Find(scene) ?? Adopt(scene);
            if (lease == null || !lease.Remote)
                return;
            lease.Slot = slot;
            lease.Origin = origin;
            lease.Bounds = new Bounds(origin, Vector3.one * Mathf.Max(256f, slotSize));
            if (lease.OwnPhysics)
                ScenePhysics.RegisterRegion(scene, lease.Bounds);
        }

        /// <summary>Does <paramref name="scene"/> hold the run relay (GameNet.NetDungeonRun, found by name: it only exists with Fish-Net)?</summary>
        private static bool HasNetworkRelay(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour b in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (b != null && b.GetType().Name == "NetDungeonRun")
                        return true;
            return false;
        }

        /// <summary>Is <paramref name="scene"/> a copy of the network dungeon instance scene?</summary>
        public static bool IsInstanceScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return false;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponent<GameNet.DungeonInstanceSceneMarker>() != null)
                    return true;
            string name = (Settings ?? new DungeonSceneSettings()).networkSceneName;
            return !string.IsNullOrEmpty(name) && scene.name == name;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GameNet.NetAuthority.IsClientOnly && Find(scene) == null && IsInstanceScene(scene))
                Adopt(scene);
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            for (int i = leases.Count - 1; i >= 0; i--)
            {
                Lease l = leases[i];
                if (!l.Remote || l.Scene != scene)
                    continue;
                l.Released = true;
                leases.RemoveAt(i);
                ScenePhysics.UnregisterRegion(scene);
            }
        }

        /// <summary>The centre of slot <paramref name="slot"/>.</summary>
        public static Vector3 SlotCenter(int slot, DungeonSceneSettings s = null)
        {
            s = s ?? Settings;
            int row = Mathf.Max(1, s.slotsPerRow);
            int x = slot % row, z = slot / row;
            float half = (row - 1) * 0.5f;
            return s.slotOrigin + new Vector3((x - half) * s.slotSize, 0f, (z - half) * s.slotSize);
        }

        /// <summary>The space of slot <paramref name="slot"/> (a cube of Slot Size around its centre).</summary>
        public static Bounds SlotBounds(int slot, DungeonSceneSettings s = null)
        {
            s = s ?? Settings;
            return new Bounds(SlotCenter(slot, s), Vector3.one * s.slotSize);
        }

        /// <summary>Creates a scene for one dungeon (Play mode only). Returns null when every slot is taken.</summary>
        public static Lease Create(string label)
        {
            DungeonSceneSettings s = Settings ?? new DungeonSceneSettings();
            int capacity = Mathf.Max(1, s.slotsPerRow) * Mathf.Max(1, s.slotsPerRow);
            int slot = FreeSlot(capacity);
            if (slot < 0)
            {
                Debug.LogError($"DungeonScenes: all {capacity} dungeon slots are in use.");
                return null;
            }
            string name = $"Dungeon {++created}" + (string.IsNullOrEmpty(label) ? "" : $" - {label}");
            var parameters = new CreateSceneParameters(s.ownPhysics ? LocalPhysicsMode.Physics3D : LocalPhysicsMode.None);
            Scene scene = SceneManager.CreateScene(name, parameters);
            // The dungeon is built from its manager's position: the slot centre, so it grows into its own cube.
            var lease = new Lease
            {
                Scene = scene,
                Slot = slot,
                Origin = SlotCenter(slot, s),
                Bounds = SlotBounds(slot, s),
                OwnPhysics = s.ownPhysics,
            };
            leases.Add(lease);
            if (lease.OwnPhysics)
            {
                ScenePhysics.RegisterRegion(scene, lease.Bounds);
                EnsureDriver();
            }
            return lease;
        }

        /// <summary>Unloads the scene of a lease (move the players out first: everything left in it is destroyed).</summary>
        public static void Release(Lease lease)
        {
            if (lease == null || lease.Released)
                return;
            lease.Released = true;
            leases.Remove(lease);
            if (lease.Pending)
                return;   // still loading: unloaded as soon as it arrives (see CreateAsync)
            ScenePhysics.UnregisterRegion(lease.Scene);
            if (lease.Remote)
                return;   // a client's copy goes when the server unloads it
            if (lease.Networked)
            {
                // Through the network: the clients still in it unload it too (offline again: a plain unload).
                if (lease.Scene.IsValid() && lease.Scene.isLoaded)
                    GameNet.NetScenes.Unload(lease.Scene, null);
                return;
            }
            if (lease.Scene.IsValid() && lease.Scene.isLoaded)
            {
                if (SceneManager.GetActiveScene() == lease.Scene)
                    SetActiveToAnyOther(lease.Scene);
                SceneManager.UnloadSceneAsync(lease.Scene);
            }
        }

        /// <summary>The lease of the scene <paramref name="scene"/>, or null.</summary>
        public static Lease Find(Scene scene)
        {
            if (!scene.IsValid())
                return null;
            for (int i = 0; i < leases.Count; i++)
                if (!leases[i].Pending && leases[i].Scene == scene)
                    return leases[i];
            return null;
        }

        private static int FreeSlot(int capacity)
        {
            for (int slot = 0; slot < capacity; slot++)
            {
                bool used = false;
                for (int i = 0; i < leases.Count && !used; i++)
                    used = !leases[i].Remote && leases[i].Slot == slot;
                if (!used)
                    return slot;
            }
            return -1;
        }

        private static void SetActiveToAnyOther(Scene leaving)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s != leaving && s.isLoaded)
                {
                    SceneManager.SetActiveScene(s);
                    return;
                }
            }
        }

        private static void EnsureDriver()
        {
            if (driver != null)
                return;
            var go = new GameObject("Dungeon Scenes (physics)") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
            UnityEngine.Object.DontDestroyOnLoad(go);
            driver = go.AddComponent<PhysicsDriver>();
        }

        /// <summary>Steps the physics of the dungeon scenes that have their own (Unity only simulates the default one).</summary>
        private sealed class PhysicsDriver : MonoBehaviour
        {
            private void FixedUpdate()
            {
                float dt = Time.fixedDeltaTime;
                for (int i = 0; i < leases.Count; i++)
                {
                    Lease l = leases[i];
                    if (l.OwnPhysics && !l.Released && l.Scene.IsValid() && l.Scene.isLoaded)
                        l.Scene.GetPhysicsScene().Simulate(dt);
                }
            }

            private void OnDestroy()
            {
                if (driver == this)
                    driver = null;
            }
        }
    }
}
