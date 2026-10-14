using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameNet
{
    /// <summary>
    /// How every mob spawner hands a new mob to the network. Spawners create mobs only where
    /// <see cref="NetSpawn.ShouldSpawnHere"/> (single player, host, dedicated server), instantiate them as before, and
    /// then call <see cref="Spawn(GameObject, Scene)"/> with the scene of the spawner (its chunk, its dungeon run). Offline
    /// that changes nothing; on a server the mob is moved into that scene - the server's active scene is the City, and
    /// interest management, physics and the realm rules are per scene - and network-spawned so the players there see it.
    /// Removing a mob goes through <see cref="Despawn"/> (<see cref="NetSpawn.Destroy"/>).
    /// </summary>
    /// <remarks>
    /// Fish-Net only spawns root objects into a scene, so a mob that will be network-spawned is unparented first (its
    /// world position is kept); offline the spawner's parenting (chunk containers, dungeon floor roots) is untouched.
    /// Always compiled; the Fish-Net layer installs <see cref="NetworkIdentityResolver"/>.
    /// </remarks>
    public static class MobNetSpawn
    {
        /// <summary>
        /// Installed by the network layer: does the object carry a network identity (a NetworkObject on its root)? Used
        /// on clients, where a networked mob must never run its own AI, even before its spawn message is processed.
        /// </summary>
        public static Func<GameObject, bool> NetworkIdentityResolver;

        private static readonly HashSet<string> warnedUnnetworked = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            NetworkIdentityResolver = null;
            warnedUnnetworked.Clear();
        }

        /// <summary>Does <paramref name="go"/> carry a network identity? False offline without Fish-Net.</summary>
        public static bool HasNetworkIdentity(GameObject go)
        {
            Func<GameObject, bool> resolver = NetworkIdentityResolver;
            return go != null && resolver != null && resolver(go);
        }

        /// <summary>Would <see cref="Spawn(GameObject, Scene)"/> network-spawn <paramref name="mob"/> right now (a server runs and the prefab is networked)?</summary>
        public static bool WillNetworkSpawn(GameObject mob) =>
            mob != null && NetAuthority.IsNetworked && NetAuthority.IsServer && NetSpawn.Provider != null && NetSpawn.Provider.CanSpawn(mob);

        /// <summary>
        /// Hands a freshly instantiated mob to the network: on a server it is unparented (when networked), moved into
        /// <paramref name="scene"/> and network-spawned. Offline, or for a mob whose prefab is not networked, only a root
        /// mob is moved into <paramref name="scene"/>. Returns true when the mob was network-spawned. Call it after the
        /// spawner has finished setting the mob up (strength, tags, names), so the first state clients receive is complete.
        /// </summary>
        public static bool Spawn(GameObject mob, Scene scene)
        {
            if (mob == null)
                return false;
            bool network = WillNetworkSpawn(mob);
            Transform t = mob.transform;
            if (network && t.parent != null)
                t.SetParent(null, true);
            if (t.parent == null && CanMoveInto(mob, scene))
                SceneManager.MoveGameObjectToScene(mob, scene);
            if (network)
                return NetSpawn.Spawn(mob);
            if (NetAuthority.IsNetworked && NetAuthority.IsServer && NetSpawn.Provider != null && warnedUnnetworked.Add(mob.name))
                Debug.LogWarning($"[{mob.name}] This mob prefab is not networked (no NetworkObject on its root, or not in the " +
                                 "spawnable prefabs): it exists on the server only, invisible to clients. Use Tools > " +
                                 "SimpleMovements > Network to network the mob prefabs.", mob);
            return false;
        }

        /// <summary>Same as <see cref="Spawn(GameObject, Scene)"/> with the scene of <paramref name="spawner"/>.</summary>
        public static bool Spawn(GameObject mob, Component spawner) =>
            Spawn(mob, spawner != null ? spawner.gameObject.scene : default);

        /// <summary>
        /// Removes a mob: network-despawned on a server, destroyed offline (after <paramref name="delay"/> seconds); a
        /// client never destroys a networked mob (the server does).
        /// </summary>
        public static void Despawn(GameObject mob, float delay = 0f) => NetSpawn.Destroy(mob, delay);

        private static bool CanMoveInto(GameObject go, Scene scene) =>
            scene.IsValid() && scene.isLoaded && go.scene != scene &&
            !(scene.buildIndex < 0 && scene.name == "DontDestroyOnLoad");
    }
}
