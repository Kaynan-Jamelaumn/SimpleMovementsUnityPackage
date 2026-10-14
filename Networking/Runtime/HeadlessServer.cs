#if FISHNET
using FishNet.Managing;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Settings for a dedicated server process (batch mode, server builds, or "Dedicated server" from the menu): caps the
    /// frame rate to the tick rate, silences audio, and logs a status line now and then. Nothing renders in batch mode,
    /// so no scene changes are needed - presentation code checks <see cref="NetAuthority.HasLocalPlayer"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class HeadlessServer : MonoBehaviour
    {
        [Tooltip("Frames per second of a dedicated server (0 = the network tick rate).")]
        [Min(0)] public int serverFrameRate = 0;

        [Tooltip("Seconds between status lines in the log (0 = never).")]
        [Min(0f)] public float statusInterval = 60f;

        private NetworkManager manager;
        private float nextStatus;
        private bool applied;

        private void Awake() => manager = GetComponentInParent<NetworkManager>();

        private void OnEnable()
        {
            NetAuthority.ModeChanged += OnModeChanged;
            OnModeChanged(NetAuthority.Mode);
        }

        private void OnDisable() => NetAuthority.ModeChanged -= OnModeChanged;

        // What the machine had before the server settings (a host's server starts a moment before its client, so the
        // session looks like a dedicated server for a frame: everything is given back when the client joins).
        private int savedFrameRate, savedVSync;
        private float savedVolume;
        private bool savedPause;

        private void OnModeChanged(NetMode mode)
        {
            if (applied && mode != NetMode.Server)
            {
                Restore();
                return;
            }
            if (mode != NetMode.Server || applied)
                return;
            applied = true;
            savedFrameRate = Application.targetFrameRate;
            savedVSync = QualitySettings.vSyncCount;
            savedVolume = AudioListener.volume;
            savedPause = AudioListener.pause;
            int fps = serverFrameRate > 0 ? serverFrameRate : manager != null ? manager.TimeManager.TickRate : 30;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Mathf.Max(10, fps);
            AudioListener.volume = 0f;
            AudioListener.pause = true;
            Application.runInBackground = true;
            Debug.Log($"[Net] Dedicated server running at {Application.targetFrameRate} fps" + (NetAuthority.IsHeadless ? " (headless)." : "."));
            nextStatus = Time.unscaledTime + statusInterval;
        }

        /// <summary>Not (or no longer) a dedicated server - a host, a client, offline: the frame rate and audio come back.</summary>
        private void Restore()
        {
            applied = false;
            Application.targetFrameRate = savedFrameRate;
            QualitySettings.vSyncCount = savedVSync;
            AudioListener.volume = savedVolume;
            AudioListener.pause = savedPause;
            Debug.Log("[Net] Not a dedicated server (host or client): frame rate and audio restored.");
        }

        private void Update()
        {
            if (!applied || statusInterval <= 0f || Time.unscaledTime < nextStatus || manager == null)
                return;
            nextStatus = Time.unscaledTime + statusInterval;
            Debug.Log($"[Net] {manager.ServerManager.Clients.Count} client(s) connected, {CombatEntity.Players.Count} player(s), tick {manager.TimeManager.Tick}.");
        }
    }
}
#endif
