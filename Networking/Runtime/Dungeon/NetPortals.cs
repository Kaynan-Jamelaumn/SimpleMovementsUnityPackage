#if FISHNET
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Dungeon portal slots over the network: a client's <see cref="Portal.Slots"/> asks the server (through
    /// <see cref="Portal.ClientSlotsRequest"/>) and this relay answers that client only with the server's
    /// <see cref="Portal.ComputeSlots"/> - the player limit (the spawner type's, which only the server knows), the players
    /// inside the portal's running shared or linked dungeon, whether it is open, and its difficulty. Put it on the
    /// "Network World" object of the Bootstrap scene, next to <see cref="NetHubWorld"/> (Tools ▸ SimpleMovements ▸
    /// Network ▸ Add Missing Relays To Network World).
    /// </summary>
    /// <remarks>
    /// Only portals that are network objects (world portals spawned by the server's <see cref="PortalSpawner"/>, or
    /// scene network objects) can be asked about; for others a client shows its own copy's settings. Requests are
    /// rate-limited per client and answered only for portals near the asking player.
    /// </remarks>
    [DisallowMultipleComponent]
    public class NetPortals : NetworkBehaviour
    {
        [Tooltip("Fewest seconds between two answers to the same client (extra requests are dropped).")]
        [SerializeField, Min(0.05f)] private float requestInterval = 0.2f;
        [Tooltip("Only portals within this distance (metres) of the asking player are answered.")]
        [SerializeField, Min(5f)] private float maxDistance = 120f;

        /// <summary>The relay of this session (null outside one).</summary>
        public static NetPortals Instance { get; private set; }

        private readonly Dictionary<int, float> nextRequest = new Dictionary<int, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning($"[Net] Two NetPortals relays ('{Instance.name}', '{name}') - keep one on the Network World object.", this);
            Instance = this;
        }

        private void OnDestroy()
        {
            UninstallHook();
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerInitialized)
                Portal.ClientSlotsRequest = SendRequest;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            UninstallHook();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            nextRequest.Clear();
        }

        private void UninstallHook()
        {
            if (Portal.ClientSlotsRequest == (System.Action<Portal>)SendRequest)
                Portal.ClientSlotsRequest = null;
        }

        /// <summary><see cref="Portal.ClientSlotsRequest"/>: asks about a portal by its network object.</summary>
        private void SendRequest(Portal portal)
        {
            if (!IsClientInitialized || portal == null)
                return;
            NetworkObject nob = NetObjects.Of(portal.gameObject);
            if (!NetObjects.IsSpawned(nob))
                return;
            ServerSlotsRequest(nob);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerSlotsRequest(NetworkObject portalObject, NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive || portalObject == null)
                return;
            float now = Time.unscaledTime;
            if (nextRequest.TryGetValue(conn.ClientId, out float allowedAt) && now < allowedAt)
                return;
            nextRequest[conn.ClientId] = now + requestInterval;

            Portal portal = portalObject.GetComponentInChildren<Portal>(true);
            CombatEntity me = NetObjects.PlayerOf(conn);
            if (portal == null || me == null || Vector3.Distance(me.Position, portal.transform.position) > maxDistance)
                return;
            PortalSlots s = portal.ComputeSlots();
            TargetSlots(conn, portalObject, s.open, s.maxPlayers, s.inside, s.shared, s.running, s.difficulty);
        }

        [TargetRpc]
        private void TargetSlots(NetworkConnection conn, NetworkObject portalObject, bool open, int maxPlayers, int inside, bool shared, bool running, float difficulty)
        {
            Portal portal = portalObject != null ? portalObject.GetComponentInChildren<Portal>(true) : null;
            if (portal == null)
                return;
            portal.ApplyReplicatedSlots(new PortalSlots
            {
                known = true,
                open = open,
                maxPlayers = Mathf.Max(0, maxPlayers),
                inside = Mathf.Max(0, inside),
                shared = shared,
                running = running,
                difficulty = float.IsNaN(difficulty) || float.IsInfinity(difficulty) ? 1f : difficulty,
            });
        }
    }
}
#endif
