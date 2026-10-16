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
    /// and server builds start a dedicated server; without them it shows the start screens (<see cref="FrontEndMenu"/>):
    /// Play Solo, Host, Join, Dedicated Server, Settings, then the character select / creation screen. A new player
    /// creates a character (race, class, traits, appearance) before the game starts; it is sent to the server when the
    /// player spawns. Everything defaults to this machine (127.0.0.1), so no remote server is ever needed.
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

        [Tooltip("Start screens with character select and creation (race, class, traits, appearance). Off = the old small menu, no character.")]
        public bool characterScreens = true;

        [Tooltip("Title on the start screen.")]
        public string gameTitle = "Simple Movements";

        [Tooltip("Started from the command line or Play In Editor As (no menu): play the character played last. " +
                 "Without one, the character screens open first.")]
        public bool autoUseLastCharacter = true;

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
        private FrontEndMenu frontEnd;
        private NetMode launchedMode = NetMode.Offline;
        private ushort launchedPort;
        private bool stopping;
        private bool returning;

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
            {
                manager.ClientManager.OnClientConnectionState -= OnClientState;
                manager.ServerManager.OnServerConnectionState -= OnServerState;
            }
        }

        private void Start()
        {
            if (manager == null)
            {
                Debug.LogError("[Net] No Fish-Net NetworkManager found: starting single player.", this);
                if (showMenu)
                    OpenMenu();
                else
                    BeginPlay(NetMode.Offline, true);
                return;
            }
            manager.ClientManager.OnClientConnectionState += OnClientState;
            manager.ServerManager.OnServerConnectionState += OnServerState;
            if (Options.tickRate > 0)
                manager.TimeManager.SetTickRate(Options.tickRate);
#if UNITY_EDITOR
            // Tools ▸ SimpleMovements ▸ Network ▸ Play In Editor As.
            string editorMode = UnityEditor.SessionState.GetString("GameNet.EditorPlayMode", "");
            if (!Options.mode.HasValue && System.Enum.TryParse(editorMode, out NetMode chosen))
                Options.mode = chosen;
#endif
            if (Options.mode.HasValue)
                BeginPlay(Options.mode.Value, true);
            else if (showMenu)
                OpenMenu();
            if (!menuOpen && !started)
                BeginPlay(NetMode.Offline, true);
        }

        /// <summary>
        /// Starts <paramref name="mode"/> once this machine has a character: a dedicated server needs none; without one
        /// the character screens open first (<paramref name="automatic"/>: the last character played is used if there is one).
        /// </summary>
        public void BeginPlay(NetMode mode, bool automatic = false)
        {
            if (started)
                return;
            bool needsCharacter = characterScreens && mode != NetMode.Server && !Application.isBatchMode && !NetAuthority.IsServerBuild;
            if (!needsCharacter || CharacterSession.Current != null)
            {
                Launch(mode);
                return;
            }
            CharacterSave last = automatic && autoUseLastCharacter ? CharacterSaveStore.LastPlayed() : null;
            if (last != null)
            {
                CharacterSaveStore.MarkPlayed(last);
                CharacterSession.Set(last);
                Launch(mode);
                return;
            }
            OpenMenu();
            if (frontEnd != null)
                frontEnd.ShowCharacters(mode);
            else
                Launch(mode);
        }

        /// <summary>Shows the start screens (or the old menu when Character Screens is off).</summary>
        public void OpenMenu()
        {
            menuOpen = true;
            if (!characterScreens || NetAuthority.IsHeadless || Application.isBatchMode)
                return;
            HideSceneCreationScreens();
            if (frontEnd == null)
            {
                frontEnd = FrontEndMenu.Create(gameTitle, manager != null, true, address, portText);
                frontEnd.PlayRequested += OnFrontEndPlay;
            }
            frontEnd.SetStatus(status);
        }

        private void CloseMenu()
        {
            menuOpen = false;
            if (frontEnd != null)
                Destroy(frontEnd.gameObject);
            frontEnd = null;
        }

        private void OnFrontEndPlay(NetMode mode)
        {
            if (frontEnd != null)
            {
                address = string.IsNullOrWhiteSpace(frontEnd.Address) ? defaultAddress : frontEnd.Address.Trim();
                portText = frontEnd.Port;
            }
            Launch(mode);
        }

        private void Update()
        {
            if (NetInput.KeyPressed(KeyCode.F9))
                statusVisible = !statusVisible;
        }

        /// <summary>
        /// The scene-built creation screen (Build Character Creation Screen) would spawn an offline player in the
        /// Bootstrap scene: with the start screens it is hidden (they create the character and start the game).
        /// </summary>
        private void HideSceneCreationScreens()
        {
            foreach (CharacterCreationUI old in FindObjectsByType<CharacterCreationUI>(FindObjectsInactive.Exclude))
            {
                if (old == null)
                    continue;
                Canvas c = old.GetComponentInParent<Canvas>();
                GameObject go = c != null ? c.rootCanvas.gameObject : old.gameObject;
                Debug.LogWarning($"[Start] '{go.name}' (a scene-built Character Creation UI) is hidden: the start screens create the character " +
                                 "and start the game. Delete it from the Bootstrap scene, or turn off Character Screens on the Net Bootstrap to use it.", go);
                go.SetActive(false);
            }
        }

        /// <summary>Starts the game as <paramref name="mode"/> (address and port from the menu or the command line).</summary>
        public void Launch(NetMode mode)
        {
            if (started)
                return;
            started = true;
            stopping = false;
            launchedMode = mode;
            try
            {
                LaunchNow(mode);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e, this);
                BackToMenu("The game could not start: " + e.Message);
            }
        }

        /// <summary>A start that failed: the start screens again, with the reason.</summary>
        private void BackToMenu(string reason)
        {
            started = false;
            status = reason;
            Debug.LogWarning("[Start] " + reason, this);
            if (showMenu || characterScreens)
                OpenMenu();
            if (frontEnd != null)
                frontEnd.SetStatus(reason);
        }

        private void LaunchNow(NetMode mode)
        {
            CloseMenu();
            CharacterSave character = CharacterSession.Current;
            LocalPlayerName = character != null && !string.IsNullOrWhiteSpace(character.characterName) ? character.characterName : nameText;
            if (!ushort.TryParse(portText, out ushort port) || port == 0)
                port = defaultPort;
            launchedPort = port;
            Debug.Log($"[Start] {mode}" + (character != null ? $" as '{character.characterName}' ({character.Summary()})" : " (no character: the prefab's own class and race)"), this);

            if (mode == NetMode.Offline || manager == null)
            {
                string scene = !string.IsNullOrEmpty(offlineScene) ? offlineScene : HubWorldSettings.Instance.citySceneName;
                if (string.IsNullOrEmpty(scene))
                {
                    Debug.LogError("[Net] No offline scene set (Net Bootstrap) and no City scene in the Hub World Settings.", this);
                    BackToMenu("No game scene to load: set the City scene in the Hub World Settings (or Offline Scene on the Net Bootstrap).");
                    return;
                }
                if (!Application.CanStreamedLevelBeLoaded(scene))
                {
                    BackToMenu($"Scene '{scene}' is not in File ▸ Build Settings: add it to play solo.");
                    return;
                }
                SceneManager.LoadScene(scene, LoadSceneMode.Single);
                return;
            }
            string city = HubWorldSettings.Instance.citySceneName;
            if ((mode == NetMode.Host || mode == NetMode.Server) && !string.IsNullOrEmpty(city) && !Application.CanStreamedLevelBeLoaded(city))
            {
                BackToMenu($"The City scene '{city}' is not in File ▸ Build Settings: the server cannot load it.");
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
            stopping = true;
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
            if (returning)
                yield break;
            returning = true;
            yield return null;
            returning = false;
            started = false;
            stopping = false;
            wasConnected = false;
            // The hub world director keeps itself in DontDestroyOnLoad: the next session's City brings a fresh one.
            if (HubWorldDirector.Instance != null)
                Destroy(HubWorldDirector.Instance.gameObject);
            // The manager lives in DontDestroyOnLoad; reload the first scene of the build for a clean slate.
            SceneManager.LoadScene(0, LoadSceneMode.Single);
            yield return null; // the start screens belong to the reloaded scene
            if (showMenu)
                OpenMenu();
            else
                menuOpen = false;
        }

        private void OnServerState(ServerConnectionStateArgs args)
        {
            // The server stopped on its own while hosting (the port is used by another window or program...).
            if (args.ConnectionState != LocalConnectionState.Stopped || !started || stopping)
                return;
            if (launchedMode != NetMode.Host && launchedMode != NetMode.Server)
                return;
            status = $"The server stopped (port {launchedPort} may be used by another game window or program).";
            Debug.LogWarning("[Net] " + status, this);
            if (manager != null && manager.ClientManager.Started)
                manager.ClientManager.StopConnection();
            StartCoroutine(ReturnToMenu());
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
                status = wasConnected ? "Disconnected from the server."
                    : launchedMode == NetMode.Host ? $"Could not start the host on port {launchedPort} (is another game window using it?)."
                    : $"Could not connect to {address}:{launchedPort}.";
                Debug.Log("[Net] " + status);
                if (frontEnd != null)
                    frontEnd.SetStatus(status);
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
                if (!characterScreens)
                    DrawMenu(); // the start screens draw themselves
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
