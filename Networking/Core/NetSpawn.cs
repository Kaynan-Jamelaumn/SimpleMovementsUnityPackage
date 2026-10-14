using System;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Spawning hooks for objects that every player must see (mobs, loot drops, chests, network-visible props). Gameplay
    /// code creates the object as before (<c>Instantiate</c>) and then calls <see cref="Spawn"/>; offline that does
    /// nothing, on a server it spawns the object on the clients that observe it. Always compiled; the Fish-Net layer
    /// installs the <see cref="Provider"/>.
    /// </summary>
    /// <remarks>
    /// Only objects whose prefab has a network identity (Fish-Net: a <c>NetworkObject</c> on the prefab root) can be
    /// spawned; others stay local to the machine that created them (offline behaviour). Clients never spawn gameplay
    /// objects themselves: spawners check <see cref="NetAuthority.IsServer"/> (or <see cref="ShouldSpawnHere"/>).
    /// </remarks>
    public static class NetSpawn
    {
        /// <summary>What the network layer does on spawn / despawn.</summary>
        public interface IProvider
        {
            /// <summary>Spawns <paramref name="go"/> for observers. <paramref name="owner"/> (a player object) may own it.</summary>
            bool Spawn(GameObject go, GameObject owner);

            /// <summary>Despawns (and destroys, or pools) a spawned object. False when it was not networked.</summary>
            bool Despawn(GameObject go);

            /// <summary>Can <paramref name="go"/> be spawned (has a network identity)?</summary>
            bool CanSpawn(GameObject go);
        }

        /// <summary>Installed by the network layer while a server runs.</summary>
        public static IProvider Provider;

        /// <summary>An object was spawned through these hooks (server side).</summary>
        public static event Action<GameObject> Spawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Provider = null;
            Spawned = null;
        }

        /// <summary>
        /// Should gameplay objects be created on this machine? True offline and on the server; false on clients (the
        /// server's copies arrive over the network).
        /// </summary>
        public static bool ShouldSpawnHere => NetAuthority.IsServer;

        /// <summary>
        /// Makes a freshly instantiated object visible to the players (server); does nothing offline. Returns true when
        /// the object was network-spawned.
        /// </summary>
        public static bool Spawn(GameObject go, GameObject owner = null)
        {
            if (go == null || !NetAuthority.IsNetworked || !NetAuthority.IsServer || Provider == null)
                return false;
            if (!Provider.CanSpawn(go))
                return false;
            bool ok = Provider.Spawn(go, owner);
            if (ok)
                Spawned?.Invoke(go);
            return ok;
        }

        /// <summary>
        /// Removes an object: network-despawns it when it was spawned, otherwise destroys it (after
        /// <paramref name="delay"/> seconds when given).
        /// </summary>
        public static void Destroy(GameObject go, float delay = 0f)
        {
            if (go == null)
                return;
            if (NetAuthority.IsClientOnly && NetAuthority.IsNetworkedObject(go))
                return; // the server owns its lifetime and will despawn it
            if (delay <= 0f && NetAuthority.IsNetworked && NetAuthority.IsServer && Provider != null && Provider.Despawn(go))
                return;
            if (delay > 0f && NetAuthority.IsNetworked && NetAuthority.IsServer && Provider != null && NetAuthority.IsNetworkedObject(go))
            {
                DelayedDespawn.Schedule(go, delay);
                return;
            }
            UnityEngine.Object.Destroy(go, delay);
        }

        /// <summary>Despawns a networked object after a delay (server).</summary>
        private sealed class DelayedDespawn : MonoBehaviour
        {
            private float at;

            public static void Schedule(GameObject go, float delay)
            {
                DelayedDespawn d = go.GetComponent<DelayedDespawn>();
                if (d == null)
                    d = go.AddComponent<DelayedDespawn>();
                d.at = Time.time + delay;
            }

            private void Update()
            {
                if (Time.time < at)
                    return;
                enabled = false;
                if (Provider == null || !Provider.Despawn(gameObject))
                    UnityEngine.Object.Destroy(gameObject);
            }
        }
    }
}
