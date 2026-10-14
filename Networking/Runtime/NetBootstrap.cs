#if FISHNET
using System.Collections;
using FishNet.Managing;
using FishNet.Transporting;
using HubWorld;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameNet
{
    /// <summary>
    /// Starts the game in the right mode. Lives in the Bootstrap scene (the first scene of the build) next to the
    /// NetworkManager. With command-line arguments (see <see cref="NetLaunchOptions"/>) it starts at once - batch mode
    /// and server builds start a dedicated server; without them it shows a small menu: Single player, Host, Join,
    /// Dedicated server. Everything defaults to this machine (127.0.0.1), so no remote server is ever needed.
    /// </summary>
    /// <remarks>
    /// Single player loads the City scene directly (the Stage 1 game, no network). The network modes start Fish-Net;
    /// <see cref="NetRealmScenes"/> then loads the City on the server and on every client that joins, and
    /// <see cref="NetPlayerSpawner"/> gives each client a player.
    /// </remarks>
    [DisallowMultipleComponent]
    public class NetBootstrap : MonoBehaviour
    {
        [Tooltip("Scene loaded for single player. Empty = the City scene named in the Hub World Settings.")]
        public string offlineScene = "";

        [Tooltip("Address clients connect to by default (this machine).")]
        public string defaultAddress = NetLaunchOptions.DefaultAddress;

        [Tooltip("Port of the server.")]
        public ushort defaultPort = NetLaunchOptions.DefaultPort;

        [Tooltip("Most players a server accepts.")]
        [Min(1)] public int maxPlayers = 16;

        [Tooltip("Show the start menu when the command line does not choose a mode.")]
        public bool showMenu = true;

        [Tooltip("Show a small connection status line while playing (toggle with F9).")]
        public bool showStatus = true;

        public static NetBootstrap Instance { get; private set; }

        /// <summary>Name this machine's player asked for (sent to the server when its player spawns).</summary>
        public static string LocalPlayerName { get; set; }

        /// <summary>The options this process was started with.</summary>
        public NetLaunchOptions Options { get; private set; }

        private NetworkManager manager;
        private bool menuOpen;
        private bool started;
        private bool wasConnected;
        private string address;
        private string portText;
        private string nameText;
        private string status = "";
        private bool statusVisible = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            manager = GetComponentInParent<NetworkManager>();
            if (manager == null)
                manager = FindAnyObjectByType<NetworkManager>();
            Options = NetLaunchOptions.FromEnvironment(Application.isBatchMode, NetAuthority.IsServerBuild);
            address = string.IsNullOrEmpty(Options.address) || Options.address == NetLaunchOptions.DefaultAddress ? defaultAddress : Options.address;
            portText = (Options.port != NetLaunchOptions.DefaultPort ? Options.port : defaultPort).ToString();
            nameText = !string.IsNullOrEmpty(Options.playerName) ? Options.playerName : "Player" + Random.Range(100, 1000);
            if (Options.maxPlayers != 16)
                maxPlayers = Options.maxPlayers;
            if (Options.shardSeed.HasValue)
                NetShardSeed.Value = Options.shardSeed.Value;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (manager != null)
                manager.ClientManager.OnClientConnectionState -= OnClientState;
        }

        private void Start()
        {
            if (manager == null)
            {
                Debug.LogError("[Net] No Fish-Net NetworkManager found: starting single player.", this);
                Launch(NetMode.Offline);
                return;
            }
            manager.ClientManager.OnClientConnectionState += OnClientState;
            if (Options.tickRate > 0)
                manager.TimeManager.SetTickRate(Options.tickRate);
#if UNITY_EDITOR
            // Tools ▸ SimpleMovements ▸ Network ▸ Play In Editor As.
            string editorMode = UnityEditor.SessionState.GetString("GameNet.EditorPlayMode", "");
            if (!Options.mode.HasValue && System.Enum.TryParse(editorMode, out NetMode chosen))
                Options.mode = chosen;
#endif
            if (Options.mode.HasValue)
                Launch(Options.mode.Value);
            else
                menuOpen = showMenu;
            if (!menuOpen && !started)
                Launch(NetMode.Offline);
        }

        private void Update()
        {
            if (NetInput.KeyPressed(KeyCode.F9))
                statusVisible = !statusVisible;
        }

        /// <summary>Starts the game as <paramref name="mode"/> (address and port from the menu or the command line).</summary>
        public void Launch(NetMode mode)
        {
            if (started)
                return;
            started = true;
            menuOpen = false;
            LocalPlayerName = nameText;
            if (!ushort.TryParse(portText, out ushort port) || port == 0)
                port = defaultPort;

            if (mode == NetMode.Offline || manager == null)
            {
                string scene = !string.IsNullOrEmpty(offlineScene) ? offlineScene : HubWorldSettings.Instance.citySceneName;
                if (string.IsNullOrEmpty(scene))
                {
                    Debug.LogError("[Net] No offline scene set (Net Bootstrap) and no City scene in the Hub World Settings.", this);
                    return;
                }
                SceneManager.LoadScene(scene, LoadSceneMode.Single);
                return;
            }

            Transport transport = manager.TransportManager.Transport;
            if (transport != null)
            {
                transport.SetPort(port);
                transport.SetMaximumClients(maxPlayers);
                transport.SetClientAddress(mode == NetMode.Client ? address : NetLaunchOptions.DefaultAddress);
            }

            switch (mode)
            {
                case NetMode.Server:
                    status = $"Dedicated server on port {port}";
                    if (!manager.ServerManager.Started) // a server build may have started it already (Start On Headless)
                        manager.ServerManager.StartConnection(port);
                    break;
                case NetMode.Host:
                    status = $"Hosting on port {port}";
                    if (!manager.ServerManager.Started)
                        manager.ServerManager.StartConnection(port);
                    manager.ClientManager.StartConnection(NetLaunchOptions.DefaultAddress, port);
                    break;
                case NetMode.Client:
                    status = $"Connecting to {address}:{port}...";
                    manager.ClientManager.StartConnection(address, port);
                    break;
            }
            Debug.Log("[Net] " + status);
        }

        /// <summary>Stops the session and goes back to the start menu (reloads the Bootstrap scene).</summary>
        public void Disconnect()
        {
            if (manager != null)
            {
                if (manager.ClientManager.Started)
                    manager.ClientManager.StopConnection();
                if (manager.ServerManager.Started)
                    manager.ServerManager.StopConnection(true);
            }
            StartCoroutine(ReturnToMenu());
        }

        private IEnumerator ReturnToMenu()
        {
            yield return null;
            started = false;
            wasConnected = false;
            // The hub world director keeps itself in DontDestroyOnLoad: the next session's City brings a fresh one.
            if (HubWorldDirector.Instance != null)
                Destroy(HubWorldDirector.Instance.gameObject);
            // The manager lives in DontDestroyOnLoad; reload the first scene of the build for a clean slate.
            SceneManager.LoadScene(0, LoadSceneMode.Single);
            menuOpen = showMenu;
        }

        private void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                wasConnected = true;
                status = manager.ServerManager.Started ? status : $"Connected to {address}";
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped && !manager.ServerManager.Started)
            {
                status = wasConnected ? "Disconnected from the server." : $"Could not connect to {address}.";
                Debug.Log("[Net] " + status);
                if (started)
                    StartCoroutine(ReturnToMenu());
            }
        }

        private void OnGUI()
        {
            if (NetAuthority.IsHeadless)
                return;
            if (menuOpen)
            {
                DrawMenu();
                return;
            }
            if (showStatus && statusVisible && NetAuthority.IsNetworked)
                DrawStatus();
        }

        private void DrawMenu()
        {
            const float w = 320f, h = 300f;
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("<b>Start</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 18, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(70f));
            nameText = GUILayout.TextField(nameText ?? "", 24);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Address", GUILayout.Width(70f));
            address = GUILayout.TextField(address ?? NetLaunchOptions.DefaultAddress, 64);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Port", GUILayout.Width(70f));
            portText = GUILayout.TextField(portText ?? "", 5);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            if (GUILayout.Button("Single player"))
                Launch(NetMode.Offline);
            if (GUILayout.Button("Host (server + play)"))
                Launch(NetMode.Host);
            if (GUILayout.Button("Join"))
                Launch(NetMode.Client);
            if (GUILayout.Button("Dedicated server (this window)"))
                Launch(NetMode.Server);
            if (!string.IsNullOrEmpty(status))
                GUILayout.Label(status);
            GUILayout.EndArea();
        }

        private void DrawStatus()
        {
            string line = NetAuthority.Mode.ToString();
            if (manager != null)
            {
                if (manager.ServerManager.Started)
                    line += $" · {manager.ServerManager.Clients.Count} client(s)";
                if (manager.ClientManager.Started)
                    line += $" · {manager.TimeManager.RoundTripTime} ms";
                line += $" · tick {manager.TimeManager.TickRate}/s";
            }
            GUI.Label(new Rect(8f, Screen.height - 24f, 600f, 20f), line + "   (F9)");
        }
    }

    /// <summary>The shard seed asked for on the command line (<c>-shard</c>), read by the hub world on the server.</summary>
    public static class NetShardSeed
    {
        public static int? Value;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Value = null;
    }
}
#endif
