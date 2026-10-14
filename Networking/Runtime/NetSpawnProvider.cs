#if FISHNET
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Fish-Net implementation of <see cref="NetSpawn"/>: spawns objects whose prefab root has a <c>NetworkObject</c>
    /// (registered in Fish-Net's spawnable prefabs) on the server, in the scene they were created in, so the observer
    /// rules decide who sees them. Installed by <see cref="NetSession"/> while the server runs.
    /// </summary>
    public sealed class NetSpawnProvider : NetSpawn.IProvider
    {
        private readonly NetworkManager manager;

        public NetSpawnProvider(NetworkManager manager) => this.manager = manager;

        public bool CanSpawn(GameObject go)
        {
            if (go == null || manager == null || !manager.ServerManager.Started)
                return false;
            NetworkObject nob = go.GetComponent<NetworkObject>();
            return nob != null && !nob.IsSpawned && nob.PrefabId != NetworkObject.UNSET_PREFABID_VALUE;
        }

        public bool Spawn(GameObject go, GameObject owner)
        {
            NetworkObject nob = go != null ? go.GetComponent<NetworkObject>() : null;
            if (nob == null || nob.IsSpawned)
                return false;
            NetworkConnection conn = null;
            if (owner != null)
            {
                NetworkObject ownerNob = NetObjects.Of(owner);
                if (ownerNob != null && ownerNob.Owner != null && ownerNob.Owner.IsValid)
                    conn = ownerNob.Owner;
            }
            manager.ServerManager.Spawn(nob, conn, go.scene);
            return nob.IsSpawned;
        }

        public bool Despawn(GameObject go)
        {
            NetworkObject nob = go != null ? go.GetComponent<NetworkObject>() : null;
            if (nob == null || !nob.IsServerInitialized)
                return false;
            manager.ServerManager.Despawn(nob, DespawnType.Destroy);
            return true;
        }
    }
}
#endif
