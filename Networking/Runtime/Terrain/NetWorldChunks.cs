#if FISHNET
using System.Collections.Generic;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// The open world's global relay: keeps the world's server-decided state the same on every client. Put it on the
    /// "Network World" object of the Bootstrap scene (a scene NetworkObject observed by every client); it finds the
    /// World's <see cref="EndlessTerrain"/> and <see cref="WeatherSystem"/> by itself whenever they are loaded.
    /// </summary>
    /// <remarks>
    /// <para><b>What is not sent.</b> The terrain, its water and its placed objects (trees, rocks, props) are generated
    /// locally on every machine from the WORLD seed (which the hub world relay sends) - deterministically, so every
    /// machine has the same objects with the same ids (<see cref="PlacedObjectId"/>: chunk coordinate + placement index).
    /// World portals are network objects of their own (spawned by the server's <see cref="PortalSpawner"/>).</para>
    /// <para><b>Removed placed objects.</b> When the server removes a placed object for good (harvested, destroyed:
    /// <see cref="EndlessTerrain.RemovePlacedObject"/> / <see cref="EndlessTerrain.RemovePlacedObjectById"/>), its id is
    /// added to <see cref="Removed"/> for the current world (<see cref="RemovedSeed"/>); each client removes it from its
    /// own copy of the terrain (also in chunks generated later, and for clients that join later). The list follows the
    /// server's World terrain: a new terrain (a new cycle, or the World reloaded) starts from that terrain's own set.</para>
    /// <para><b>Weather.</b> The server's weather clock, forced weather and time of day (<see cref="Weather"/>) - sent when
    /// they change and every few seconds - so every client shows the server's weather. Clients run the clock themselves
    /// in between.</para>
    /// <para>Clients send nothing to this relay (no ServerRpc), so it has nothing to validate.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class NetWorldChunks : NetworkBehaviour
    {
        /// <summary>The relay of this session (null outside one).</summary>
        public static NetWorldChunks Instance { get; private set; }

        /// <summary>The world seed the <see cref="Removed"/> ids belong to (a client applies them only to a terrain of that world).</summary>
        public readonly SyncVar<int> RemovedSeed = new SyncVar<int>(new SyncTypeSettings(0f));

        /// <summary>Placed objects the server removed in the current world.</summary>
        public readonly SyncList<PlacedObjectId> Removed = new SyncList<PlacedObjectId>(new SyncTypeSettings(0f));

        /// <summary>The server's weather (invalid while the server has no weather system).</summary>
        public readonly SyncVar<WeatherSyncState> Weather = new SyncVar<WeatherSyncState>(new SyncTypeSettings(0f));

        /// <summary>One shared time of day (the server's Day Night Cycle), sent whether or not a client is in the World.</summary>
        public struct DayClockSync
        {
            public bool valid;
            public float hour;
        }

        /// <summary>The server's time of day (<see cref="DayNightCycle"/>), so every player has the same day and night everywhere.</summary>
        public readonly SyncVar<DayClockSync> DayClock = new SyncVar<DayClockSync>(new SyncTypeSettings(0f));

        [Tooltip("Seconds between sends of the server's time of day (a jump in time is sent at once).")]
        [SerializeField, Min(0.5f)] private float clockSendSeconds = 2f;

        [Tooltip("Send the server's weather to the clients, so everyone has the same weather.")]
        [SerializeField] private bool syncWeather = true;

        [Tooltip("Seconds between re-sends of the server's weather clock (changes of the forced weather or time of day are sent at once).")]
        [SerializeField, Min(1f)] private float weatherResendSeconds = 10f;

        // Server.
        private readonly HashSet<PlacedObjectId> serverRemoved = new HashSet<PlacedObjectId>();
        private EndlessTerrain boundTerrain;
        private bool terrainEventsHooked;
        private WeatherSystem sentWeather;
        private float nextWeatherSend;
        private float nextClockSend;

        // Client.
        private int removedVersion;
        private readonly Dictionary<EndlessTerrain, int> appliedVersions = new Dictionary<EndlessTerrain, int>(ReferenceComparer<EndlessTerrain>.Instance);
        private readonly List<EndlessTerrain> forgetScratch = new List<EndlessTerrain>();
        private readonly List<PlacedObjectId> removedScratch = new List<PlacedObjectId>();
        private bool weatherDirty;
        private WeatherSystem appliedWeather;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        /// <summary>The network clock in seconds: the server's tick time (a client's estimate of it).</summary>
        private double NetworkTime =>
            TimeManager != null ? TimeManager.TicksToTime(TimeManager.GetPreciseTick(TickType.Tick)) : Time.unscaledTimeAsDouble;

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning($"[Net] Two NetWorldChunks relays ('{Instance.name}', '{name}') - keep one on the Network World object.", this);
            Instance = this;
            RemovedSeed.OnChange += OnRemovedSeedChanged;
            Removed.OnChange += OnRemovedChanged;
            Weather.OnChange += OnWeatherChanged;
        }

        private void OnDestroy()
        {
            RemovedSeed.OnChange -= OnRemovedSeedChanged;
            Removed.OnChange -= OnRemovedChanged;
            Weather.OnChange -= OnWeatherChanged;
            UnhookTerrainEvents();
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            boundTerrain = null;
            serverRemoved.Clear();
            sentWeather = null;
            nextWeatherSend = 0f;
            if (!terrainEventsHooked)
            {
                terrainEventsHooked = true;
                EndlessTerrain.PlacedObjectRemoved += OnServerObjectRemoved;
                EndlessTerrain.RemovedObjectsReplaced += OnServerRemovedReplaced;
            }
            BindTerrain();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            UnhookTerrainEvents();
            boundTerrain = null;
            serverRemoved.Clear();
            sentWeather = null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            appliedVersions.Clear();
            removedVersion++;
            appliedWeather = null;
            weatherDirty = true;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            appliedVersions.Clear();
            appliedWeather = null;
        }

        private void UnhookTerrainEvents()
        {
            if (!terrainEventsHooked)
                return;
            terrainEventsHooked = false;
            EndlessTerrain.PlacedObjectRemoved -= OnServerObjectRemoved;
            EndlessTerrain.RemovedObjectsReplaced -= OnServerRemovedReplaced;
        }

        private void Update()
        {
            if (IsServerInitialized)
            {
                BindTerrain();
                SendWeather();
                SendClock();
            }
            else if (IsClientInitialized)
            {
                ApplyRemovedObjects();
                ApplyWeather();
                if (DayClock.Value.valid)
                    DayNightCycle.ApplyNetworkHour(DayClock.Value.hour);
            }
        }

        // ------------------------------------------------------------------ time of day (server)

        /// <summary>The server's clock, every few seconds or at once when it jumped (a sleep, a command, a cycle).</summary>
        private void SendClock()
        {
            DayNightCycle cycle = DayNightCycle.Instance;
            if (cycle == null || !cycle.isActiveAndEnabled)
            {
                if (DayClock.Value.valid)
                    DayClock.Value = default;
                return;
            }
            DayClockSync sent = DayClock.Value;
            bool jumped = sent.valid && Mathf.Abs(Mathf.DeltaAngle(sent.hour * 15f, cycle.Hour * 15f)) > 15f; // more than an hour
            if (!sent.valid || jumped || Time.unscaledTime >= nextClockSend)
            {
                nextClockSend = Time.unscaledTime + clockSendSeconds;
                DayClock.Value = new DayClockSync { valid = true, hour = cycle.Hour };
            }
        }

        // ------------------------------------------------------------------ removed placed objects (server)

        /// <summary>The World's terrain on this machine: the first enabled, ready one.</summary>
        private static EndlessTerrain PrimaryTerrain()
        {
            IReadOnlyList<EndlessTerrain> terrains = EndlessTerrain.Active;
            for (int i = 0; i < terrains.Count; i++)
            {
                EndlessTerrain t = terrains[i];
                if (t != null && t.IsReady && t.isActiveAndEnabled)
                    return t;
            }
            return null;
        }

        /// <summary>Follows the server's World terrain: a new one replaces the list with its own removed objects.</summary>
        private void BindTerrain()
        {
            EndlessTerrain terrain = PrimaryTerrain();
            if (terrain == null || terrain == boundTerrain)
                return;
            boundTerrain = terrain;
            ResetRemoved(terrain);
        }

        private void ResetRemoved(EndlessTerrain terrain)
        {
            serverRemoved.Clear();
            Removed.Clear();
            RemovedSeed.Value = terrain.WorldSeed;
            foreach (PlacedObjectId id in terrain.RemovedObjects)
                if (serverRemoved.Add(id))
                    Removed.Add(id);
        }

        private void OnServerObjectRemoved(EndlessTerrain terrain, PlacedObjectId id)
        {
            if (!IsServerInitialized || terrain == null)
                return;
            BindTerrain();
            // Only the World's terrain is shared (an extra terrain elsewhere stays local to the server).
            if (terrain != boundTerrain)
                return;
            if (serverRemoved.Add(id))
                Removed.Add(id);
        }

        private void OnServerRemovedReplaced(EndlessTerrain terrain)
        {
            if (!IsServerInitialized || terrain == null)
                return;
            BindTerrain();
            if (terrain == boundTerrain)
                ResetRemoved(terrain);
        }

        // ------------------------------------------------------------------ removed placed objects (client)

        private void OnRemovedSeedChanged(int previous, int next, bool asServer)
        {
            if (!asServer)
                removedVersion++;
        }

        private void OnRemovedChanged(SyncListOperation op, int index, PlacedObjectId oldItem, PlacedObjectId newItem, bool asServer)
        {
            if (!asServer)
                removedVersion++;
        }

        /// <summary>Gives every terrain of the server's world on this client the server's removed objects (when they changed, or the terrain is new).</summary>
        private void ApplyRemovedObjects()
        {
            IReadOnlyList<EndlessTerrain> terrains = EndlessTerrain.Active;
            int seed = RemovedSeed.Value;
            bool copied = false;
            for (int i = 0; i < terrains.Count; i++)
            {
                EndlessTerrain t = terrains[i];
                if (t == null || !t.IsReady || t.WorldSeed != seed)
                    continue;
                if (appliedVersions.TryGetValue(t, out int applied) && applied == removedVersion)
                    continue;
                if (!copied)
                {
                    copied = true;
                    removedScratch.Clear();
                    for (int k = 0; k < Removed.Count; k++)
                        removedScratch.Add(Removed[k]);
                }
                t.SyncRemovedObjects(removedScratch);
                appliedVersions[t] = removedVersion;
            }

            // Forget terrains that were destroyed (a world unloaded).
            if (appliedVersions.Count > terrains.Count)
            {
                forgetScratch.Clear();
                foreach (EndlessTerrain t in appliedVersions.Keys)
                    if (t == null)
                        forgetScratch.Add(t);
                for (int i = 0; i < forgetScratch.Count; i++)
                    appliedVersions.Remove(forgetScratch[i]);
                forgetScratch.Clear();
            }
        }

        // ------------------------------------------------------------------ weather

        private void SendWeather()
        {
            if (!syncWeather)
                return;
            WeatherSystem weather = WeatherSystem.Instance;
            if (weather == null || !weather.isActiveAndEnabled)
            {
                sentWeather = null;
                if (Weather.Value.valid)
                    Weather.Value = default;
                return;
            }
            WeatherSyncState state = weather.CaptureSyncState(NetworkTime);
            if (weather != sentWeather || Time.unscaledTime >= nextWeatherSend || state.DiffersBeyondClock(Weather.Value))
            {
                sentWeather = weather;
                nextWeatherSend = Time.unscaledTime + Mathf.Max(1f, weatherResendSeconds);
                Weather.Value = state;
            }
        }

        private void OnWeatherChanged(WeatherSyncState previous, WeatherSyncState next, bool asServer)
        {
            if (!asServer)
                weatherDirty = true;
        }

        /// <summary>Writes the server's weather into this client's weather system (when it changed, or the weather system is new).</summary>
        private void ApplyWeather()
        {
            if (!syncWeather)
                return;
            WeatherSyncState state = Weather.Value;
            WeatherSystem weather = WeatherSystem.Instance;
            if (!state.valid || weather == null || (!weatherDirty && weather == appliedWeather))
                return;
            // Still in another world (a cycle jump in progress): wait until this client's world is the server's.
            TerrainGenerator generator = weather.Generator;
            if (state.worldSeed != 0 && generator != null && generator.VoronoiSeed != state.worldSeed)
                return;
            weather.ApplySyncState(state, NetworkTime);
            appliedWeather = weather;
            weatherDirty = false;
        }
    }
}
#endif
