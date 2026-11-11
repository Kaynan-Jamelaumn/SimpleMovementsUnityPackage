using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// Runs the Rune Magic System's background work while playing: rune users without a <see cref="RuneSoul"/> get one where
    /// the rules run (Settings ▸ Add Missing Soul Components; with a warning, since the prefab should have it for the HUD
    /// gauge, meditation input and networking); the Rune Soul window exists, and the debug panel in development builds
    /// (Settings ▸ Debug Panel).
    /// </summary>
    public static class RuneMagicRuntime
    {
        private static Runner runner;
        private static bool warned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            runner = null;
            warned = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            if (runner != null) return;
            var go = new GameObject("[Rune Magic]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
            go.AddComponent<RuneSoulWindow>();
            go.AddComponent<SynthesisWindow>();
            go.AddComponent<SpellbookWindow>();
            go.AddComponent<GrimoireWindow>();
            go.AddComponent<SpellingOverlay>();
            go.AddComponent<DiscoveryWindow>();
            if (Application.isEditor || Debug.isDebugBuild)
                go.AddComponent<RuneMagicDebugPanel>();
        }

        /// <summary>Gives every rune user where the rules run a soul (when the setting is on). Called once a second.</summary>
        public static void EnsureSouls()
        {
            RuneMagicSettings settings = RuneMagicDatabase.Instance.Catalog.Settings;
            if (!settings.enabled || !settings.addMissingSoulComponents)
                return;
            var players = CombatEntity.Players;
            for (int i = 0; i < players.Count; i++)
            {
                CombatEntity p = players[i];
                if (p == null || !GameNet.NetAuthority.RunsRules(p.gameObject) || !RuneMagicGate.IsRuneUser(p) || RuneSoul.For(p) != null)
                    continue;
                p.gameObject.AddComponent<RuneSoul>();
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning($"[Rune Magic] '{p.name}' is a rune user without a Rune Soul: one was added for this session. " +
                                     "Add Rune Magic to the player prefab (Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune Magic to Selected Player) " +
                                     "for the HUD's Soul gauge, the Meditate input and network replication.", p);
                }
            }
        }

        private sealed class Runner : MonoBehaviour
        {
            private float next;

            private void Update()
            {
                if (Time.unscaledTime < next) return;
                next = Time.unscaledTime + 1f;
                EnsureSouls();
            }
        }
    }
}
