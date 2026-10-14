using System;
using UnityEngine;

namespace GameNet
{
    /// <summary>How this process takes part in the game.</summary>
    public enum NetMode
    {
        /// <summary>Single player: no network session; this process decides everything (the Stage 1 behaviour).</summary>
        Offline,
        /// <summary>Server and client in one process (listen server): decides everything and has a local player.</summary>
        Host,
        /// <summary>Dedicated server: decides everything, has no local player (usually headless).</summary>
        Server,
        /// <summary>Client connected to a server: sends intentions, shows what the server decided.</summary>
        Client,
    }

    /// <summary>
    /// The one question gameplay code asks about networking: "does this machine decide?". Always compiled (no Fish-Net
    /// types), so the gameplay scripts work the same with or without Fish-Net installed. The Fish-Net layer
    /// (<c>Networking/Runtime</c>, compiled when the <c>FISHNET</c> define exists) sets the mode and installs the
    /// per-object resolvers.
    /// </summary>
    /// <remarks>
    /// Rules for gameplay code:
    /// <list type="bullet">
    /// <item>Anything that changes gameplay state (damage, loot, inventory, cooldowns, spawning, AI, the world cycle)
    /// runs only where <see cref="IsServer"/> is true (offline, host or dedicated server) - or, per object,
    /// where <see cref="RunsRules"/> is true.</item>
    /// <item>Read the mode when acting, never cache it in Awake: a client learns it is a client when it connects.</item>
    /// <item>Presentation (camera, UI, audio, local input) belongs to the player <see cref="IsLocallyControlled"/>.</item>
    /// </list>
    /// </remarks>
    public static class NetAuthority
    {
        private static NetMode mode = NetMode.Offline;

        /// <summary>The current mode (Offline until a network session starts).</summary>
        public static NetMode Mode => mode;

        /// <summary>A network session is running (host, server or client).</summary>
        public static bool IsNetworked => mode != NetMode.Offline;

        /// <summary>This process decides gameplay: offline, host or dedicated server.</summary>
        public static bool IsServer => mode != NetMode.Client;

        /// <summary>This process only shows what a remote server decides.</summary>
        public static bool IsClientOnly => mode == NetMode.Client;

        /// <summary>This process has a player of its own (everything except a dedicated server).</summary>
        public static bool HasLocalPlayer => mode != NetMode.Server;

        /// <summary>Dedicated server running without graphics (batch mode or a server build).</summary>
        public static bool IsHeadless => mode == NetMode.Server && (Application.isBatchMode || IsServerBuild);

        /// <summary>Was this built as a dedicated-server player (Unity's Server build target)?</summary>
        public static bool IsServerBuild
        {
            get
            {
#if UNITY_SERVER
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>The mode changed (a session started or stopped).</summary>
        public static event Action<NetMode> ModeChanged;

        /// <summary>Set by the network layer: is this object networked (has a spawned network identity)?</summary>
        public static Func<GameObject, bool> NetworkedObjectResolver;

        /// <summary>Set by the network layer: is this networked object owned by this machine's client?</summary>
        public static Func<GameObject, bool> LocalOwnerResolver;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            mode = NetMode.Offline;
            ModeChanged = null;
            NetworkedObjectResolver = null;
            LocalOwnerResolver = null;
        }

        /// <summary>Called by the network layer when a session starts or stops.</summary>
        public static void SetMode(NetMode value)
        {
            if (value == mode)
                return;
            mode = value;
            ModeChanged?.Invoke(value);
        }

        /// <summary>Is <paramref name="go"/> (or the object it belongs to) networked?</summary>
        public static bool IsNetworkedObject(GameObject go) =>
            go != null && IsNetworked && NetworkedObjectResolver != null && NetworkedObjectResolver(go);

        /// <summary>
        /// Should the gameplay rules of <paramref name="go"/> run on this machine? True offline and on the server; on a
        /// client only for objects that are not networked (local-only effects).
        /// </summary>
        public static bool RunsRules(GameObject go) => IsServer || !IsNetworkedObject(go);

        /// <summary>
        /// Is <paramref name="go"/> controlled from this machine - the one whose input, camera and UI live here? Offline:
        /// true. Networked: true for the object owned by this machine's client; false on a dedicated server.
        /// </summary>
        public static bool IsLocallyControlled(GameObject go)
        {
            if (go == null)
                return false;
            if (!IsNetworked || !IsNetworkedObject(go))
                return HasLocalPlayer;
            return LocalOwnerResolver != null && LocalOwnerResolver(go);
        }

        /// <summary>
        /// Is <paramref name="go"/> a copy of something controlled elsewhere (another client's player seen from here,
        /// or any networked object on a client)? Such copies show replicated state and run no input or rules.
        /// </summary>
        public static bool IsRemoteCopy(GameObject go) => IsNetworkedObject(go) && !IsLocallyControlled(go) && !IsServer;
    }
}
