#if FISHNET
using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using HubWorld;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Keeps <see cref="NetAuthority"/> in step with Fish-Net: sets the mode when the server or client starts and stops,
    /// and installs the per-object resolvers (networked? owned here? network id). Put it on the NetworkManager object
    /// (the network setup menu does). Everything else in the network layer goes through it to reach Fish-Net.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public class NetSession : MonoBehaviour
    {
        public static NetSession Instance { get; private set; }

        /// <summary>The Fish-Net manager this session follows.</summary>
        public NetworkManager Manager { get; private set; }

        /// <summary>A client connection finished joining (server side; its player may not exist yet).</summary>
        public event Action<NetworkConnection> ClientJoined;

        /// <summary>A client connection left (server side).</summary>
        public event Action<NetworkConnection> ClientLeft;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Manager = GetComponentInParent<NetworkManager>();
            if (Manager == null)
                Manager = InstanceFinder.NetworkManager;
            if (Manager == null)
            {
                Debug.LogError("[Net] NetSession needs a Fish-Net NetworkManager on the same object (or a parent).", this);
                enabled = false;
                return;
            }
            Manager.ServerManager.OnServerConnectionState += OnServerState;
            Manager.ClientManager.OnClientConnectionState += OnClientState;
            Manager.ServerManager.OnRemoteConnectionState += OnRemoteState;
            InstallResolvers();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;
            Instance = null;
            if (Manager != null)
            {
                Manager.ServerManager.OnServerConnectionState -= OnServerState;
                Manager.ClientManager.OnClientConnectionState -= OnClientState;
                Manager.ServerManager.OnRemoteConnectionState -= OnRemoteState;
            }
            NetAuthority.NetworkedObjectResolver = null;
            NetAuthority.LocalOwnerResolver = null;
            PlayerIds.NetworkIdResolver = null;
            if (NetSpawn.Provider is NetSpawnProvider)
                NetSpawn.Provider = null;
            NetAuthority.SetMode(NetMode.Offline);
        }

        private static void InstallResolvers()
        {
            NetAuthority.NetworkedObjectResolver = go =>
            {
                NetworkObject nob = NetObjects.Of(go);
                return nob != null && (nob.IsServerInitialized || nob.IsClientInitialized);
            };
            NetAuthority.LocalOwnerResolver = go =>
            {
                NetworkObject nob = NetObjects.Of(go);
                return nob != null && nob.IsOwner;
            };
            PlayerIds.NetworkIdResolver = e =>
            {
                NetworkObject nob = e != null ? NetObjects.Of(e.gameObject) : null;
                return nob != null && (nob.IsServerInitialized || nob.IsClientInitialized) ? nob.ObjectId + 1 : 0;
            };
        }

        private void OnServerState(ServerConnectionStateArgs args) => RefreshMode();

        private void OnClientState(ClientConnectionStateArgs args) => RefreshMode();

        private void OnRemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
                ClientJoined?.Invoke(conn);
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                if (NetLagCompensation.Instance != null)
                    NetLagCompensation.Instance.Forget(conn);
                ClientLeft?.Invoke(conn);
            }
        }

        /// <summary>Recomputes <see cref="NetAuthority.Mode"/> from Fish-Net's state.</summary>
        public void RefreshMode()
        {
            if (Manager == null)
                return;
            bool server = Manager.ServerManager.Started;
            bool client = Manager.ClientManager.Started;
            NetMode m = server && client ? NetMode.Host : server ? NetMode.Server : client ? NetMode.Client : NetMode.Offline;
            if (server && !(NetSpawn.Provider is NetSpawnProvider))
                NetSpawn.Provider = new NetSpawnProvider(Manager);
            else if (!server && NetSpawn.Provider is NetSpawnProvider)
                NetSpawn.Provider = null;
            NetAuthority.SetMode(m);
        }

        /// <summary>Connections of every client (server side).</summary>
        public IEnumerable<NetworkConnection> Clients
        {
            get
            {
                if (Manager == null || !Manager.ServerManager.Started)
                    yield break;
                foreach (NetworkConnection c in Manager.ServerManager.Clients.Values)
                    if (c != null && c.IsActive)
                        yield return c;
            }
        }
    }

    /// <summary>Lookups between gameplay objects and Fish-Net objects.</summary>
    public static class NetObjects
    {
        /// <summary>The network object <paramref name="go"/> belongs to (itself or a parent), or null.</summary>
        public static NetworkObject Of(GameObject go) => go != null ? go.GetComponentInParent<NetworkObject>() : null;

        /// <summary>The network object of a combat entity (its movable root, the entity or a parent), or null.</summary>
        public static NetworkObject Of(CombatEntity e)
        {
            if (e == null)
                return null;
            NetworkObject nob = e.GetComponentInParent<NetworkObject>();
            if (nob != null)
                return nob;
            GameObject root = PlayerLocator.MovableRoot(e);
            return root != null ? root.GetComponentInParent<NetworkObject>() : null;
        }

        /// <summary>Is the object spawned on the network (server or client side)?</summary>
        public static bool IsSpawned(NetworkObject nob) => nob != null && (nob.IsServerInitialized || nob.IsClientInitialized);

        /// <summary>The connection owning the player entity, or null (server side).</summary>
        public static NetworkConnection OwnerOf(CombatEntity e)
        {
            NetworkObject nob = Of(e);
            return nob != null && nob.Owner != null && nob.Owner.IsValid ? nob.Owner : null;
        }

        /// <summary>The player entity owned by <paramref name="conn"/> (its first player object), or null.</summary>
        public static CombatEntity PlayerOf(NetworkConnection conn)
        {
            if (conn == null || !conn.IsValid)
                return null;
            if (conn.FirstObject != null)
            {
                CombatEntity e = conn.FirstObject.GetComponentInChildren<CombatEntity>();
                if (e != null && e.Kind == CombatEntity.EntityKind.Player)
                    return e;
            }
            foreach (NetworkObject nob in conn.Objects)
            {
                if (nob == null)
                    continue;
                CombatEntity e = nob.GetComponentInChildren<CombatEntity>();
                if (e != null && e.Kind == CombatEntity.EntityKind.Player)
                    return e;
            }
            return null;
        }
    }
}
#endif
