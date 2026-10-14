#if FISHNET
using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using HubWorld;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Gives every client a player when it arrives in the City (server side): the player prefab (it needs a
    /// NetworkObject and a <see cref="NetPlayer"/>) is spawned at the City's spawn point, owned by that client. Use
    /// this instead of Fish-Net's PlayerSpawner: the City is loaded per connection, so the player can only be spawned
    /// once the client is in it.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetPlayerSpawner : MonoBehaviour
    {
        [Tooltip("The networked player prefab (root: NetworkObject + NetPlayer).")]
        [RequiredReference("no player is spawned without it")]
        public NetworkObject playerPrefab;

        /// <summary>A player was spawned for a client (server side).</summary>
        public event Action<NetworkConnection, NetworkObject> PlayerSpawned;

        private NetworkManager manager;
        private NetRealmScenes realmScenes;
        private int spawnIndex;
        private readonly HashSet<int> spawning = new HashSet<int>();

        private void Awake()
        {
            manager = GetComponentInParent<NetworkManager>();
            realmScenes = GetComponentInParent<NetRealmScenes>();
            if (realmScenes == null)
                realmScenes = FindAnyObjectByType<NetRealmScenes>();
        }

        private void OnEnable()
        {
            if (realmScenes != null)
                realmScenes.ClientEnteredCity += OnClientEnteredCity;
            if (NetSession.Instance != null)
                NetSession.Instance.ClientLeft += OnClientLeft;
        }

        private void OnDisable()
        {
            if (realmScenes != null)
                realmScenes.ClientEnteredCity -= OnClientEnteredCity;
            if (NetSession.Instance != null)
                NetSession.Instance.ClientLeft -= OnClientLeft;
        }

        private void OnClientLeft(NetworkConnection conn)
        {
            if (conn != null)
                spawning.Remove(conn.ClientId);
        }

        private void OnClientEnteredCity(NetworkConnection conn)
        {
            if (conn == null || !conn.IsActive || manager == null || !manager.ServerManager.Started)
                return;
            if (NetObjects.PlayerOf(conn) != null || spawning.Contains(conn.ClientId))
                return; // already has a player (it came back to the city)
            if (playerPrefab == null)
            {
                Debug.LogError("[Net] Net Player Spawner has no player prefab.", this);
                return;
            }
            spawning.Add(conn.ClientId);
            try
            {
                RealmScene city = RealmScene.Find(RealmKind.City);
                Pose pose = city != null ? city.SpawnPose(spawnIndex++ % 12) : new Pose(Vector3.zero, Quaternion.identity);
                NetworkObject nob = manager.GetPooledInstantiated(playerPrefab, pose.position, pose.rotation, true);
                UnityEngine.SceneManagement.Scene scene = city != null ? city.Scene : (realmScenes != null ? realmScenes.CityScene : default);
                if (scene.IsValid() && nob.gameObject.scene != scene)
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(nob.gameObject, scene);
                manager.ServerManager.Spawn(nob, conn, scene);
                PlayerSpawned?.Invoke(conn, nob);
                Debug.Log($"[Net] Spawned a player for client {conn.ClientId}.");
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
            finally
            {
                spawning.Remove(conn.ClientId);
            }
        }
    }
}
#endif
