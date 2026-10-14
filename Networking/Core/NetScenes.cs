using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameNet
{
    /// <summary>
    /// Scene operations that must go through the network when a server runs: loading a realm or dungeon scene,
    /// unloading it, and moving a player's object from one scene to another. Offline they are plain Unity calls (the
    /// Stage 1 behaviour); with Fish-Net the <see cref="Provider"/> loads scenes through Fish-Net's scene manager, so the
    /// clients concerned load them too and the observer rules follow each player into their scene. Always compiled.
    /// </summary>
    public static class NetScenes
    {
        /// <summary>The network layer's scene operations (installed while a server runs).</summary>
        public interface IProvider
        {
            /// <summary>Does the provider handle moving this object (a networked player on the server)?</summary>
            bool Handles(GameObject root);

            /// <summary>
            /// Moves a networked object into <paramref name="scene"/> now (server) and has its owner's client load that
            /// scene and follow; scenes the owner no longer needs are unloaded for it once it arrived.
            /// </summary>
            void MoveToScene(GameObject root, Scene scene);

            /// <summary>Loads a scene on the server only (clients load it when a player of theirs is moved in).</summary>
            void Load(string sceneName, LocalPhysicsMode physics, bool allowStacking, Action<Scene> done);

            /// <summary>Unloads a scene on the server (and on the clients that still have it).</summary>
            void Unload(Scene scene, Action done);
        }

        /// <summary>Installed by the network layer while a server runs.</summary>
        public static IProvider Provider;

        /// <summary>Scenes are loaded through the network (a server with the provider installed).</summary>
        public static bool ThroughNetwork => Provider != null && NetAuthority.IsNetworked && NetAuthority.IsServer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Provider = null;

        /// <summary>
        /// Moves the top-level object of <paramref name="who"/> into <paramref name="scene"/>: through the network for
        /// networked players on a server, otherwise with <see cref="SceneManager.MoveGameObjectToScene"/>.
        /// </summary>
        public static void MoveToScene(GameObject who, Scene scene)
        {
            if (who == null || !scene.IsValid() || !scene.isLoaded)
                return;
            GameObject root = who.transform.root.gameObject;
            if (ThroughNetwork && Provider.Handles(root))
            {
                Provider.MoveToScene(root, scene);
                return;
            }
            if (root.scene != scene)
                SceneManager.MoveGameObjectToScene(root, scene);
        }

        /// <summary>
        /// May <paramref name="who"/> be parked in DontDestroyOnLoad while travelling? Not networked objects on a server:
        /// they stay in a scene the network knows (the provider keeps them alive).
        /// </summary>
        public static bool CanPark(GameObject who)
        {
            if (who == null)
                return false;
            if (NetAuthority.IsNetworkedObject(who))
                return false;
            return !(ThroughNetwork && Provider.Handles(who.transform.root.gameObject));
        }

        /// <summary>Loads a scene additively; <paramref name="done"/> gets it (default on failure).</summary>
        public static void LoadAdditive(string sceneName, LocalPhysicsMode physics, bool allowStacking, Action<Scene> done)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                done?.Invoke(default);
                return;
            }
            if (ThroughNetwork)
            {
                Provider.Load(sceneName, physics, allowStacking, done);
                return;
            }
            Runner.Run(LoadLocal(sceneName, physics, done));
        }

        /// <summary>Unloads a scene; <paramref name="done"/> once it is gone.</summary>
        public static void Unload(Scene scene, Action done)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                done?.Invoke();
                return;
            }
            if (ThroughNetwork)
            {
                Provider.Unload(scene, done);
                return;
            }
            Runner.Run(UnloadLocal(scene, done));
        }

        private static IEnumerator LoadLocal(string sceneName, LocalPhysicsMode physics, Action<Scene> done)
        {
            AsyncOperation op;
            try
            {
                op = SceneManager.LoadSceneAsync(sceneName, new LoadSceneParameters(LoadSceneMode.Additive, physics));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                op = null;
            }
            if (op == null)
            {
                done?.Invoke(default);
                yield break;
            }
            while (!op.isDone)
                yield return null;
            // The newest loaded scene of that name (several may exist when stacked).
            Scene found = default;
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && (s.name == sceneName || s.path == sceneName || s.path.EndsWith("/" + sceneName + ".unity", StringComparison.Ordinal)))
                {
                    found = s;
                    break;
                }
            }
            done?.Invoke(found);
        }

        private static IEnumerator UnloadLocal(Scene scene, Action done)
        {
            AsyncOperation op = SceneManager.UnloadSceneAsync(scene);
            while (op != null && !op.isDone)
                yield return null;
            done?.Invoke();
        }

        /// <summary>Runs coroutines for the static helpers (one hidden object in DontDestroyOnLoad).</summary>
        private sealed class Runner : MonoBehaviour
        {
            private static Runner instance;

            public static void Run(IEnumerator routine)
            {
                if (instance == null)
                {
                    var go = new GameObject("[Net Scenes]") { hideFlags = HideFlags.HideInHierarchy };
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Runner>();
                }
                instance.StartCoroutine(routine);
            }
        }
    }
}
