#if FISHNET
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// The network identity of a player (put it on the root of the player prefab, next to its NetworkObject). It makes
    /// the copy owned by this machine the local player (camera, input, UI, <see cref="PlayerLocator.Local"/>) and turns
    /// all of that off on every other copy - other players seen from here, and every player on a dedicated server - so
    /// a copy never reacts to this machine's keyboard or takes over its camera. It also carries the player's name.
    /// </summary>
    /// <remarks>
    /// Owner-only parts are found automatically (Unity's PlayerInput component, the camera rig and its Cinemachine
    /// cameras, AudioListener, PlayerMovementView, the emote wheel, screen-space canvases under the player, the
    /// interaction prompt) plus anything listed in <see cref="ownerOnly"/> / <see cref="ownerOnlyObjects"/>. They are
    /// switched off in Awake whenever a network session runs and switched back on for the owner only.
    /// </remarks>
    [DefaultExecutionOrder(-2000)]
    [DisallowMultipleComponent]
    public partial class NetPlayer : NetworkBehaviour
    {
        [Tooltip("Extra components only the owning machine runs (camera scripts, local UI...).")]
        public Behaviour[] ownerOnly = new Behaviour[0];

        [Tooltip("Extra objects only the owning machine shows (first-person arms, local HUD...).")]
        public GameObject[] ownerOnlyObjects = new GameObject[0];

        [Tooltip("Objects only OTHER machines show (a third-person body hidden from its own first-person camera...).")]
        public GameObject[] othersOnlyObjects = new GameObject[0];

        private static readonly HashSet<string> OwnerOnlyTypeNames = new HashSet<string>
        {
            "PlayerInput", "PlayerCameraController", "PlayerCameraView", "PlayerMovementView", "EmoteWheel",
            "CinemachineBrain", "CinemachineCamera", "CinemachineVirtualCamera", "CinemachineFreeLook",
            "CinemachineInputAxisController", "CinemachineInputProvider", "Player",
        };

        /// <summary>The player's name, the same on every machine (set by the server, asked for by the owner).</summary>
        public readonly SyncVar<string> DisplayName = new SyncVar<string>("");

        /// <summary>This machine's player (null until it spawned, and on a dedicated server).</summary>
        public static NetPlayer Local { get; private set; }

        private static readonly List<NetPlayer> all = new List<NetPlayer>();

        /// <summary>Every spawned player on this machine.</summary>
        public static IReadOnlyList<NetPlayer> All => all;

        /// <summary>The player's combat entity.</summary>
        public CombatEntity Entity { get; private set; }

        private readonly List<Behaviour> disabled = new List<Behaviour>();
        private readonly List<GameObject> hidden = new List<GameObject>();
        private bool presentationApplied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Local = null;
            all.Clear();
        }

        private void Awake()
        {
            // The entity lives on the PlayerStatusController's object (it registers itself there in its Awake); create
            // it there now rather than on the root, or the player would be listed twice.
            PlayerStatusController status = GetComponentInChildren<PlayerStatusController>(true);
            Entity = status != null ? CombatEntity.GetOrAdd(status.gameObject) : GetComponentInChildren<CombatEntity>(true);
            if (Entity == null)
                Entity = PlayerLocator.Register(gameObject);
            DisplayName.OnChange += OnNameChanged;
            CharacterCode.OnChange += OnCharacterChanged;
            // Before any Start: copies must not grab the camera, the cursor or the keyboard. The owner gets them back.
            if (NetAuthority.IsNetworked)
                SwitchOffOwnerParts();
        }

        private void OnDestroy()
        {
            DisplayName.OnChange -= OnNameChanged;
            CharacterCode.OnChange -= OnCharacterChanged;
            all.Remove(this);
            if (Local == this)
                Local = null;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!all.Contains(this))
                all.Add(this);
            if (!presentationApplied && disabled.Count == 0)
                SwitchOffOwnerParts();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            all.Remove(this);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            GameObject mover = PlayerLocator.MovableRoot(Entity);
            if (mover != null && mover.GetComponent<LagCompensated>() == null)
                mover.AddComponent<LagCompensated>();
            if (string.IsNullOrEmpty(DisplayName.Value))
                DisplayName.Value = "Player " + (Owner != null ? Owner.ClientId + 1 : 0);
            ApplyPresentation();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyPresentation();
            if (IsOwner)
            {
                string wanted = NetBootstrap.LocalPlayerName;
                if (!string.IsNullOrWhiteSpace(wanted))
                    ServerSetName(wanted);
                SendCharacter(); // the character picked on the start screens (race, class, traits, look)
            }
            else if (!IsServerInitialized)
            {
                ShowSyncedCharacter(); // another player who joined before this machine
            }
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            ApplyPresentation();
        }

        /// <summary>Owner asks for a name; the server cleans it up and applies it.</summary>
        [ServerRpc]
        private void ServerSetName(string wanted)
        {
            DisplayName.Value = CleanName(wanted, Owner != null ? Owner.ClientId : 0);
        }

        /// <summary>Trims, shortens and strips markup from a requested name.</summary>
        public static string CleanName(string wanted, int clientId)
        {
            if (string.IsNullOrWhiteSpace(wanted))
                return "Player " + (clientId + 1);
            var sb = new System.Text.StringBuilder();
            foreach (char c in wanted.Trim())
            {
                if (c == '<' || c == '>' || char.IsControl(c))
                    continue;
                sb.Append(c);
                if (sb.Length >= 20)
                    break;
            }
            string s = sb.ToString().Trim();
            return s.Length == 0 ? "Player " + (clientId + 1) : s;
        }

        private void OnNameChanged(string prev, string next, bool asServer)
        {
            if (string.IsNullOrEmpty(next))
                return;
            PlayerNameComponent n = GetComponentInChildren<PlayerNameComponent>(true);
            if (n != null && n.PlayerName != next)
                n.SetPlayerName(next);
            Player p = GetComponentInChildren<Player>(true);
            if (p != null)
                p.PlayerName = next;
            gameObject.name = "Player [" + next + "]";
        }

        // ------------------------------------------------------------------ presentation

        /// <summary>Is this copy the one controlled from this machine?</summary>
        public bool IsLocalPlayer => IsOwner && NetAuthority.HasLocalPlayer;

        private void ApplyPresentation()
        {
            bool local = IsLocalPlayer;
            presentationApplied = true;
            SharedPlayerInput.SetBlocked(this, !local);
            if (local)
            {
                Local = this;
                if (Entity != null)
                    PlayerLocator.Local = Entity;
                foreach (Behaviour b in disabled)
                    if (b != null)
                        b.enabled = true;
                disabled.Clear();
                foreach (GameObject go in hidden)
                    if (go != null)
                        go.SetActive(true);
                hidden.Clear();
                foreach (GameObject go in othersOnlyObjects)
                    if (go != null)
                        go.SetActive(false);
            }
            else
            {
                if (Local == this)
                    Local = null;
                SwitchOffOwnerParts();
                foreach (GameObject go in othersOnlyObjects)
                    if (go != null)
                        go.SetActive(true);
            }
        }

        private void SwitchOffOwnerParts()
        {
            SharedPlayerInput.SetBlocked(this, true);
            foreach (Behaviour b in GetComponentsInChildren<Behaviour>(true))
            {
                if (b == null || b == this || !b.enabled || disabled.Contains(b))
                    continue;
                if (!IsOwnerOnly(b))
                    continue;
                b.enabled = false;
                disabled.Add(b);
            }
            foreach (Behaviour b in ownerOnly)
            {
                if (b == null || !b.enabled || disabled.Contains(b))
                    continue;
                b.enabled = false;
                disabled.Add(b);
            }
            foreach (GameObject go in ownerOnlyObjects)
            {
                if (go == null || !go.activeSelf || hidden.Contains(go))
                    continue;
                go.SetActive(false);
                hidden.Add(go);
            }
        }

        private static bool IsOwnerOnly(Behaviour b)
        {
            if (b is Camera || b is AudioListener)
                return true;
            if (b is Canvas canvas)
                return canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace;
            return OwnerOnlyTypeNames.Contains(b.GetType().Name);
        }
    }
}
#endif
