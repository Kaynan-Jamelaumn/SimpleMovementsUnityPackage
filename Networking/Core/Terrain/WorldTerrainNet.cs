using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameNet
{
    /// <summary>
    /// Always-compiled glue between the open-world terrain and the network session:
    /// <list type="bullet">
    /// <item>installs <see cref="HubWorld.HubWorldDirector.TerrainViewers"/>, so during a network session the world terrain
    /// streams around every player the hub world names (all players out in the World on a server, this machine's player
    /// on a client) through <see cref="EndlessTerrain.SetViewers"/>; offline the hook declines and the hub world sets the
    /// single <see cref="EndlessTerrain.viewer"/> exactly as before;</item>
    /// <item><see cref="SpawnWorldObject"/>: how chunk spawners (world portals) hand a freshly created gameplay object to
    /// the network.</item>
    /// </list>
    /// </summary>
    public static class WorldTerrainNet
    {
        private static readonly HashSet<string> warnedUnnetworked = new HashSet<string>();

        // After the hub world resets its statics (SubsystemRegistration), before any scene wakes up.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            warnedUnnetworked.Clear();
            global::HubWorld.HubWorldDirector.TerrainViewers = StreamAroundViewers;
        }

        /// <summary>
        /// The hub world's terrain-viewer hook: in a network session every given transform becomes a viewer of
        /// <paramref name="terrain"/> (true = handled). Offline it returns false, so single player keeps its one viewer.
        /// </summary>
        public static bool StreamAroundViewers(EndlessTerrain terrain, IReadOnlyList<Transform> viewers)
        {
            if (terrain == null || !NetAuthority.IsNetworked)
                return false;
            terrain.SetViewers(viewers);
            return true;
        }

        /// <summary>
        /// Hands a gameplay object a chunk spawner has just created and set up to the network. On a server (host or
        /// dedicated) an object whose prefab is networked leaves the chunk's hierarchy (Fish-Net spawns root objects only;
        /// its world pose is kept), goes into the spawner's scene - the World, while a server's active scene is the City -
        /// and is network-spawned; true is returned. Offline, on clients and for prefabs without a network identity nothing
        /// changes (the object stays under the chunk); a server warns once per prefab that the object is server-only.
        /// </summary>
        /// <param name="go">The new object (a prefab instance).</param>
        /// <param name="spawner">The spawner (its scene is where the object goes).</param>
        /// <param name="what">What it is, for the warning ("world portal").</param>
        public static bool SpawnWorldObject(GameObject go, Component spawner, string what)
        {
            if (go == null || !NetAuthority.IsNetworked || !NetAuthority.IsServer || NetSpawn.Provider == null)
                return false;
            if (!NetSpawn.Provider.CanSpawn(go))
            {
                if (warnedUnnetworked.Add(go.name))
                    Debug.LogWarning($"[{go.name}] This {what} prefab is not networked (no NetworkObject on its root, or not in the " +
                                     "spawnable prefabs): it exists on the server only and clients cannot see it. Add a NetworkObject " +
                                     "to the prefab root (Tools > SimpleMovements > Network).", go);
                return false;
            }
            Transform t = go.transform;
            if (t.parent != null)
                t.SetParent(null, true);
            Scene scene = spawner != null ? spawner.gameObject.scene : default;
            if (scene.IsValid() && scene.isLoaded && go.scene != scene)
                SceneManager.MoveGameObjectToScene(go, scene);
            return NetSpawn.Spawn(go);
        }
    }
}
