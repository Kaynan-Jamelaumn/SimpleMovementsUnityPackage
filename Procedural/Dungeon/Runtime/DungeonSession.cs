using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ProceduralDungeon
{
    /// <summary>
    /// Moves the player (and, optionally, the party members with them) between the world and a dungeon:
    /// <list type="number">
    /// <item>Enter: remembers where the player stood, freezes their controls, pauses the world (terrain streaming,
    /// weather, world spawners - so nothing keeps generating around the dungeon's position), starts generation.</item>
    /// <item>Ready: moves the player to the spawn in front of the entrance portal, applies the theme's atmosphere,
    /// gives controls back.</item>
    /// <item>Failure: undoes all of it and puts the player back - controls are never left disabled.</item>
    /// <item>Exit (portals): clears the dungeon, restores atmosphere and world, returns the player.</item>
    /// </list>
    /// </summary>
    public static class DungeonSession
    {
        public static bool InDungeon { get; private set; }
        public static bool IsEntering { get; private set; }
        public static DungeonManager Manager { get; private set; }
        public static DungeonRequest CurrentRequest { get; private set; }
        public static DungeonInstance Current => Manager != null ? Manager.Current : null;

        /// <summary>Height above the spawn point the player is placed at (they drop onto the floor).</summary>
        public static float SpawnLift = 1f;

        /// <summary>Seconds after leaving a dungeon during which portals don't send the player back in.</summary>
        public static float ReentryCooldown = 3f;

        private static float lastExit = -100f;

        /// <summary>False right after leaving a dungeon (so standing in the world portal doesn't re-enter it).</summary>
        public static bool CanEnter => Time.time - lastExit >= ReentryCooldown;

        /// <summary>The player is in a freshly built dungeon.</summary>
        public static event Action<DungeonInstance> Entered;
        /// <summary>The player left (true = through the exit portal after completing it).</summary>
        public static event Action<bool> Exited;
        public static event Action<string> Failed;

        /// <summary>One player in the dungeon: the object that is moved and where it goes back to.</summary>
        private struct Traveller
        {
            public GameObject root;
            public Vector3 returnPosition;
            public Quaternion returnRotation;
        }

        private static readonly List<Traveller> travellers = new List<Traveller>(4);
        private static readonly List<GameObject> participants = new List<GameObject>(4);
        private static bool ownsManager;

        /// <summary>The players in (or entering) the dungeon - the one who entered first. Empty outside dungeons.</summary>
        public static IReadOnlyList<GameObject> Participants => participants;

        /// <summary>Is <paramref name="go"/> (or the player it belongs to) in the dungeon?</summary>
        public static bool IsParticipant(GameObject go)
        {
            if (go == null)
                return false;
            for (int i = 0; i < participants.Count; i++)
            {
                GameObject p = participants[i];
                if (p != null && (go == p || go.transform.IsChildOf(p.transform) || p.transform.IsChildOf(go.transform)))
                    return true;
            }
            return false;
        }

        /// <summary>The participant closest to <paramref name="position"/> (null when nobody is in the dungeon).</summary>
        public static Transform NearestParticipant(Vector3 position)
        {
            Transform best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < participants.Count; i++)
            {
                GameObject p = participants[i];
                if (p == null) continue;
                float d = (p.transform.position - position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = p.transform; }
            }
            return best;
        }

        // Static state survives play-mode entry when domain reload is disabled: start clean every time.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            InDungeon = false;
            IsEntering = false;
            Manager = null;
            CurrentRequest = null;
            travellers.Clear();
            participants.Clear();
            ownsManager = false;
            lastExit = -100f;
            Entered = null;
            Exited = null;
            Failed = null;
        }

        /// <summary>
        /// Sends <paramref name="who"/> into a dungeon. <paramref name="managerOrPrefab"/> may be a DungeonManager in the
        /// scene, a prefab with one (instantiated at <paramref name="origin"/>), or null (a manager is created using
        /// <paramref name="profile"/>).
        /// </summary>
        public static void Enter(GameObject who, DungeonRequest request, DungeonManager managerOrPrefab = null, DungeonProfile profile = null,
            Vector3? origin = null, Pose? returnPose = null)
        {
            if (who == null)
                return;
            Enter(new[] { who }, request, managerOrPrefab, profile, origin, returnPose);
        }

        /// <summary>
        /// Sends a group into a dungeon (the player who entered first, then the party members travelling with them).
        /// Everyone is placed around the spawn and comes back around <paramref name="returnPose"/>.
        /// </summary>
        public static void Enter(IReadOnlyList<GameObject> group, DungeonRequest request, DungeonManager managerOrPrefab = null,
            DungeonProfile profile = null, Vector3? origin = null, Pose? returnPose = null)
        {
            if (group == null || group.Count == 0 || group[0] == null || IsEntering || !CanEnter)
                return;
            bool switching = InDungeon;
            if (switching)
                ClearDungeon(false);

            IsEntering = true;
            if (!switching)
            {
                travellers.Clear();
                participants.Clear();
                GameObject first = group[0];
                Vector3 basePos = returnPose.HasValue ? returnPose.Value.position : first.transform.position;
                Quaternion baseRot = returnPose.HasValue ? returnPose.Value.rotation : first.transform.rotation;
                for (int i = 0; i < group.Count; i++)
                {
                    GameObject g = group[i];
                    if (g == null || participants.Contains(g))
                        continue;
                    travellers.Add(new Traveller { root = g, returnPosition = basePos + Offset(i, baseRot), returnRotation = baseRot });
                    participants.Add(g);
                }
            }
            SetControls(false);

            Manager = ResolveManager(managerOrPrefab, profile, origin ?? new Vector3(0f, -10000f, 0f));
            if (profile != null)
                Manager.profile = profile;
            if (!switching)
                DungeonWorldPause.Pause(Manager.pauseWhileInside, Manager.hideWhileInside);

            CurrentRequest = request != null ? request.Clone() : new DungeonRequest();
            Manager.Ready -= OnReady;
            Manager.Failed -= OnFailed;
            Manager.Ready += OnReady;
            Manager.Failed += OnFailed;
            Manager.Generate(CurrentRequest);
        }

        /// <summary>Leaves the dungeon and returns the player where they entered.</summary>
        public static void Exit(bool completed)
        {
            if (!InDungeon && !IsEntering)
                return;
            SetControls(false);
            ClearDungeon(true);
            DungeonAtmosphere.Restore();
            DungeonWorldPause.Resume();
            TeleportHome();
            SetControls(true);
            InDungeon = false;
            IsEntering = false;
            lastExit = Time.time;
            travellers.Clear();
            participants.Clear();
            Exited?.Invoke(completed);
        }

        /// <summary>
        /// Called by <see cref="DungeonPortal"/>. Only a participant can use a dungeon portal; the whole group travels
        /// with them (leaving, completing, or going deeper).
        /// </summary>
        public static void UsePortal(DungeonPortal portal, GameObject who)
        {
            if (!InDungeon || IsEntering || !IsParticipant(who))
                return;
            switch (portal.action)
            {
                case DungeonPortal.PortalAction.NextDungeon:
                    Enter(new List<GameObject>(participants), (CurrentRequest ?? new DungeonRequest()).Next(), Manager);
                    break;
                case DungeonPortal.PortalAction.CompleteDungeon:
                    Exit(true);
                    break;
                default:
                    Exit(false);
                    break;
            }
        }

        private static DungeonManager ResolveManager(DungeonManager managerOrPrefab, DungeonProfile profile, Vector3 origin)
        {
            if (Manager != null && (managerOrPrefab == null || managerOrPrefab == Manager))
                return Manager;
            if (managerOrPrefab != null && managerOrPrefab.gameObject.scene.IsValid())
            {
                ownsManager = false;
                return managerOrPrefab;
            }
            DungeonManager m;
            if (managerOrPrefab != null)
            {
                m = Object.Instantiate(managerOrPrefab, origin, Quaternion.identity);
            }
            else
            {
                var go = new GameObject("Dungeon");
                go.transform.position = origin;
                m = go.AddComponent<DungeonManager>();
                m.profile = profile;
            }
            ownsManager = true;
            return m;
        }

        private static void OnReady(DungeonInstance dungeon)
        {
            if (Manager != null)
            {
                Manager.Ready -= OnReady;
                Manager.Failed -= OnFailed;
            }
            Pose spawn = dungeon.PlayerSpawn;
            for (int i = 0; i < travellers.Count; i++)
                Teleport(travellers[i].root, spawn.position + Offset(i, spawn.rotation) + Vector3.up * SpawnLift, spawn.rotation);
            DungeonAtmosphere.Apply(dungeon.Profile.Theme);
            SetControls(true);
            InDungeon = true;
            IsEntering = false;
            Entered?.Invoke(dungeon);
        }

        private static void OnFailed(string message)
        {
            if (Manager != null)
            {
                Manager.Ready -= OnReady;
                Manager.Failed -= OnFailed;
            }
            ClearDungeon(true);
            DungeonAtmosphere.Restore();
            DungeonWorldPause.Resume();
            TeleportHome();
            SetControls(true);
            InDungeon = false;
            IsEntering = false;
            lastExit = Time.time;
            travellers.Clear();
            participants.Clear();
            Failed?.Invoke(message);
        }

        private static void ClearDungeon(bool destroyManager)
        {
            if (Manager == null)
                return;
            Manager.Cancel();
            Manager.Clear();
            if (destroyManager && ownsManager)
            {
                Object.Destroy(Manager.gameObject);
                Manager = null;
            }
        }

        /// <summary>Spot of the i-th traveller around a point: the first on it, the others on a ring beside it.</summary>
        private static Vector3 Offset(int index, Quaternion facing)
        {
            if (index <= 0)
                return Vector3.zero;
            float angle = 90f + (index - 1) * 60f; // right, then around
            return Quaternion.Euler(0f, angle, 0f) * (facing * Vector3.forward) * 1.4f;
        }

        private static void SetControls(bool on)
        {
            foreach (Traveller t in travellers)
            {
                if (t.root == null)
                    continue;
                var movement = t.root.GetComponent<PlayerMovementController>();
                if (movement != null)
                    movement.enabled = on;
                var cc = t.root.GetComponent<CharacterController>();
                if (cc != null)
                    cc.enabled = on;
            }
        }

        private static void TeleportHome()
        {
            foreach (Traveller t in travellers)
                Teleport(t.root, t.returnPosition, t.returnRotation);
        }

        private static void Teleport(GameObject who, Vector3 position, Quaternion rotation)
        {
            if (who == null)
                return;
            var cc = who.GetComponent<CharacterController>();
            bool was = cc != null && cc.enabled;
            if (cc != null)
                cc.enabled = false;
            who.transform.SetPositionAndRotation(position, rotation);
            if (cc != null)
                cc.enabled = was;
            Physics.SyncTransforms();
        }
    }

    /// <summary>
    /// Pauses the open world while the player is underground: terrain streaming (EndlessTerrain follows the viewer's
    /// x/z and would keep generating chunks above the dungeon), weather, the chunk spawners (see
    /// <see cref="WorldSpawnRegistry.Paused"/>: no new portals or mobs, world mobs switched off) and the SpawnerManager,
    /// plus anything listed on the DungeonManager. Resume restores exactly what it paused.
    /// </summary>
    public static class DungeonWorldPause
    {
        private static readonly List<Behaviour> Paused = new List<Behaviour>();
        private static readonly List<GameObject> Hidden = new List<GameObject>();

        public static bool IsPaused { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Paused.Clear();
            Hidden.Clear();
            IsPaused = false;
        }

        public static void Pause(IEnumerable<Behaviour> extra = null, IEnumerable<GameObject> hide = null)
        {
            if (IsPaused)
                return;
            IsPaused = true;
            // Chunk spawners (portals, mobs) stop spawning and switch their mobs off until Resume.
            WorldSpawnRegistry.Paused = true;
            Disable(FindAll<EndlessTerrain>());
            Disable(FindAll<WeatherSystem>());
            Disable(FindAll<SpawnerManager>());
            if (extra != null)
                Disable(extra);
            if (hide != null)
            {
                foreach (GameObject go in hide)
                {
                    if (go != null && go.activeSelf)
                    {
                        go.SetActive(false);
                        Hidden.Add(go);
                    }
                }
            }
        }

        public static void Resume()
        {
            foreach (GameObject go in Hidden)
                if (go != null)
                    go.SetActive(true);
            Hidden.Clear();
            foreach (Behaviour b in Paused)
                if (b != null)
                    b.enabled = true;
            Paused.Clear();
            IsPaused = false;
            WorldSpawnRegistry.Paused = false;
        }

        /// <summary>Every active object of a type (Unity 6.4 deprecated the overloads taking a sort mode).</summary>
        internal static T[] FindAll<T>() where T : Object
        {
#if UNITY_6000_4_OR_NEWER
            return Object.FindObjectsByType<T>();
#else
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#endif
        }

        private static void Disable<T>(IEnumerable<T> behaviours) where T : Behaviour
        {
            foreach (T b in behaviours)
            {
                if (b != null && b.enabled)
                {
                    b.enabled = false;
                    Paused.Add(b);
                }
            }
        }
    }

    /// <summary>Applies a theme's ambient light and fog (and turns off the sun) while inside; restores the scene's afterwards.</summary>
    public static class DungeonAtmosphere
    {
        private static bool applied;
        private static AmbientMode ambientMode;
        private static Color ambientLight;
        private static bool fog;
        private static Color fogColor;
        private static FogMode fogMode;
        private static float fogDensity;
        private static readonly List<Light> Suns = new List<Light>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            applied = false;
            Suns.Clear();
        }

        public static void Apply(DungeonTheme theme)
        {
            if (theme == null || !theme.applyAtmosphere)
                return;
            if (!applied)
            {
                ambientMode = RenderSettings.ambientMode;
                ambientLight = RenderSettings.ambientLight;
                fog = RenderSettings.fog;
                fogColor = RenderSettings.fogColor;
                fogMode = RenderSettings.fogMode;
                fogDensity = RenderSettings.fogDensity;
                foreach (Light l in DungeonWorldPause.FindAll<Light>())
                {
                    if (l.type == LightType.Directional && l.enabled)
                    {
                        l.enabled = false;
                        Suns.Add(l);
                    }
                }
                applied = true;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = theme.ambientLight;
            RenderSettings.fog = theme.fog;
            RenderSettings.fogColor = theme.fogColor;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = theme.fogDensity;
        }

        public static void Restore()
        {
            if (!applied)
                return;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambientLight;
            RenderSettings.fog = fog;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            foreach (Light l in Suns)
                if (l != null)
                    l.enabled = true;
            Suns.Clear();
            applied = false;
        }
    }
}
