#if FISHNET
using FishNet.Connection;
using FishNet.Object;
using RuneMagic;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Networks a rune user's soul (<see cref="RuneSoul"/>): the server runs every soul rule (damage, recovery, meditation,
    /// rituals, collapse, absorbing and releasing runes, magical energy, saving) and sends the soul's view to the player's
    /// owner only, whenever it changes - at most every Send Interval - and the absorbed runes when they change. Other
    /// players never learn them (decision D18). The owner's client shows them (the HUD's Soul gauge, the Rune Soul window,
    /// the meditation screen, the debug panel) and sends its requests (meditate, absorb, release, give energy) to the
    /// server, which checks them again.
    /// <para>Put it on the player prefab root next to the NetworkObject (Network ▸ Make Networked adds it). Offline it does
    /// nothing. Players that are not rune users send nothing.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class NetRuneMagic : NetworkBehaviour, IRuneMagicNet
    {
        [Tooltip("Least seconds between two soul updates while the soul changes (recovery, meditation progress).")]
        [SerializeField, Min(0.05f)] private float sendInterval = 0.25f;
        [Tooltip("Requests the owner may send per second (sustained) and at once (burst); more are dropped.")]
        [SerializeField, Min(0.5f)] private float requestsPerSecond = 4f;
        [SerializeField, Min(1)] private int requestBurst = 6;

        private RuneSoul soul;
        private int sentVersion = int.MinValue;
        private int sentRunesVersion = int.MinValue;
        private int sentDiscoveryVersion = int.MinValue;
        private int sentSpellingVersion = int.MinValue;
        private int sentGrimoireVersion = int.MinValue;
        private int sentEnergyVersion = int.MinValue;
        private float nextSend;
        private RuneRequestGuard guard;

        private RuneSoul Soul
        {
            get
            {
                if (soul == null)
                    soul = GetComponentInChildren<RuneSoul>(true);
                return soul;
            }
        }

        // ------------------------------------------------------------------ server
        public override void OnStartServer()
        {
            base.OnStartServer();
            sentVersion = int.MinValue;
            sentRunesVersion = int.MinValue;
            sentDiscoveryVersion = int.MinValue;
            sentSpellingVersion = int.MinValue;
            sentGrimoireVersion = int.MinValue;
            sentEnergyVersion = int.MinValue;
            guard = new RuneRequestGuard(requestsPerSecond, requestBurst);
        }

        public override void OnOwnershipServer(NetworkConnection prevOwner)
        {
            base.OnOwnershipServer(prevOwner);
            sentVersion = int.MinValue; // a new owner gets the whole view and the runes
            sentRunesVersion = int.MinValue;
            sentDiscoveryVersion = int.MinValue;
            sentSpellingVersion = int.MinValue;
            sentGrimoireVersion = int.MinValue;
            sentEnergyVersion = int.MinValue;
            guard?.Reset();
        }

        private void Update()
        {
            if (!IsServerInitialized)
                return;
            RuneSoul s = Soul;
            NetworkConnection owner = Owner;
            if (s == null || owner == null || !owner.IsActive || Time.unscaledTime < nextSend)
                return;
            // The whole rune list goes when runes were gained, lost or changed; when only energy changed (a cast, giving energy, an imprint)
            // just the energies go.
            bool viewChanged = s.Version != sentVersion, runesChanged = s.StructureVersion != sentRunesVersion;
            bool energyChanged = !runesChanged && s.EnergyVersion != sentEnergyVersion;
            bool discoveryChanged = s.DiscoveryVersion != sentDiscoveryVersion;
            bool spellingChanged = s.SpellingVersion != sentSpellingVersion;
            bool grimoireChanged = s.GrimoireVersion != sentGrimoireVersion;
            if (!viewChanged && !runesChanged && !energyChanged && !discoveryChanged && !spellingChanged && !grimoireChanged)
                return;
            nextSend = Time.unscaledTime + sendInterval;
            sentVersion = s.Version;
            sentRunesVersion = s.StructureVersion;
            sentEnergyVersion = s.EnergyVersion;
            sentDiscoveryVersion = s.DiscoveryVersion;
            sentSpellingVersion = s.SpellingVersion;
            sentGrimoireVersion = s.GrimoireVersion;
            if (owner.IsLocalClient)
                return; // the host's own player reads the server's soul directly
            // The runes first, so the view that names their version finds them.
            if (runesChanged)
                TargetRuneList(owner, JsonUtility.ToJson(s.BuildRuneList()));
            else if (energyChanged)
                TargetRuneEnergies(owner, JsonUtility.ToJson(s.BuildEnergyView()));
            if (viewChanged)
                TargetSoulView(owner, JsonUtility.ToJson(s.View));
            // The discovery view holds no secret (no concept, no answer): it is the only thing of a discovery that is sent.
            if (discoveryChanged)
                TargetDiscoveryView(owner, JsonUtility.ToJson(s.DiscoveryView));
            // The owner's key sequence (the letters it must press and how it ended): nobody else's.
            if (spellingChanged)
                TargetSpellingView(owner, JsonUtility.ToJson(s.SpellingView));
            // The grimoire the owner has open (its pages, nothing of the soul): to the one who holds it and asked.
            if (grimoireChanged)
                TargetGrimoireView(owner, JsonUtility.ToJson(s.GrimoireView));
        }

        /// <summary>The grimoire the owner has open (pages and their prints), to the owner only.</summary>
        [TargetRpc]
        private void TargetGrimoireView(NetworkConnection conn, string json)
        {
            if (IsServerInitialized || string.IsNullOrEmpty(json) || json.Length > RuneNetLimits.MaxGrimoireViewLength)
                return;
            RuneSoul s = Soul;
            if (s == null)
                { s = soul = gameObject.AddComponent<RuneSoul>(); Hook(); }
            GrimoireView v;
            try { v = JsonUtility.FromJson<GrimoireView>(json); }
            catch (System.ArgumentException) { return; }
            s.ApplyReplicatedGrimoire(v);
        }

        /// <summary>The owner's Spelling System sequence (letters, windows, result), to the owner only.</summary>
        [TargetRpc]
        private void TargetSpellingView(NetworkConnection conn, string json)
        {
            if (IsServerInitialized || string.IsNullOrEmpty(json) || json.Length > RuneNetLimits.MaxSpellingViewLength)
                return;
            RuneSoul s = Soul;
            if (s == null)
                { s = soul = gameObject.AddComponent<RuneSoul>(); Hook(); }
            SpellingView v;
            try { v = JsonUtility.FromJson<SpellingView>(json); }
            catch (System.ArgumentException) { return; }
            s.ApplyReplicatedSpelling(v);
        }

        /// <summary>The discovery in progress (minigame or riddle, never the answer), to the owner only.</summary>
        [TargetRpc]
        private void TargetDiscoveryView(NetworkConnection conn, string json)
        {
            if (IsServerInitialized || string.IsNullOrEmpty(json) || json.Length > RuneNetLimits.MaxDiscoveryViewLength)
                return;
            RuneSoul s = Soul;
            if (s == null)
                { s = soul = gameObject.AddComponent<RuneSoul>(); Hook(); }
            DiscoveryView v;
            try { v = JsonUtility.FromJson<DiscoveryView>(json); }
            catch (System.ArgumentException) { return; }
            s.ApplyReplicatedDiscovery(v);
        }

        /// <summary>The energy of the absorbed runes, to the owner only (when only energy changed).</summary>
        [TargetRpc]
        private void TargetRuneEnergies(NetworkConnection conn, string json)
        {
            if (IsServerInitialized || string.IsNullOrEmpty(json) || json.Length > RuneNetLimits.MaxRuneEnergiesLength)
                return;
            RuneSoul s = Soul;
            if (s == null)
                return; // no list has arrived yet: the full list will
            RuneEnergyView v;
            try { v = JsonUtility.FromJson<RuneEnergyView>(json); }
            catch (System.ArgumentException) { return; }
            s.ApplyReplicatedEnergies(v);
        }

        /// <summary>The absorbed runes and the concept knowledge, to the owner only (when they change).</summary>
        [TargetRpc]
        private void TargetRuneList(NetworkConnection conn, string json)
        {
            if (IsServerInitialized || string.IsNullOrEmpty(json) || json.Length > RuneNetLimits.MaxRuneListLength)
                return;
            RuneSoul s = Soul;
            if (s == null)
                { s = soul = gameObject.AddComponent<RuneSoul>(); Hook(); }
            RuneListView v;
            try { v = JsonUtility.FromJson<RuneListView>(json); }
            catch (System.ArgumentException) { return; }
            s.ApplyReplicatedRunes(v);
        }

        /// <summary>The soul's view, to the owner only.</summary>
        [TargetRpc]
        private void TargetSoulView(NetworkConnection conn, string json)
        {
            if (IsServerInitialized || string.IsNullOrEmpty(json) || json.Length > RuneNetLimits.MaxViewLength)
                return;
            RuneSoul s = Soul;
            if (s == null)
                { s = soul = gameObject.AddComponent<RuneSoul>(); Hook(); } // the prefab lacks it: the server added one there
            RuneSoulView v;
            try { v = JsonUtility.FromJson<RuneSoulView>(json); }
            catch (System.ArgumentException) { return; }
            s.ApplyReplicatedView(v);
        }

        // ------------------------------------------------------------------ owner requests
        public override void OnStartClient()
        {
            base.OnStartClient();
            Hook();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            Hook();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            RuneSoul s = Soul;
            if (s != null && ReferenceEquals(s.Net, this))
                s.Net = null;
        }

        /// <summary>The owner's client copy sends its soul requests to the server (not the host: its rules run directly).</summary>
        private void Hook()
        {
            RuneSoul s = Soul;
            if (s == null)
                return;
            if (IsOwner && !IsServerInitialized) s.Net = this;
            else if (ReferenceEquals(s.Net, this)) s.Net = null;
        }

        bool IRuneMagicNet.Send(RuneSoulRequest request)
        {
            if (!IsClientInitialized || !IsOwner || request.kind == RuneRequestKind.None)
                return false;
            string text = request.text ?? "";
            if (text.Length > RuneNetLimits.MaxTextFor(request.kind))
                return false;
            ServerSoulRequest((byte)request.kind, request.seq, request.flag, text, request.amount);
            return true;
        }

        /// <summary>A request of the owner; the server checks everything as for a local player.</summary>
        [ServerRpc]
        private void ServerSoulRequest(byte kind, uint seq, bool flag, string text, float amount)
        {
            if (guard == null) guard = new RuneRequestGuard(requestsPerSecond, requestBurst);
            if (!guard.Allow(Time.unscaledTime))
                return;
            RuneSoul s = Soul;
            if (s == null || kind == (byte)RuneRequestKind.None || kind > (byte)RuneNetLimits.LastKind)
                return;
            if (text != null && text.Length > RuneNetLimits.MaxTextFor((RuneRequestKind)kind))
                return;
            if (float.IsNaN(amount) || float.IsInfinity(amount))
                amount = 0f;
            s.ServerHandleRequest(new RuneSoulRequest { kind = (RuneRequestKind)kind, seq = seq, flag = flag, text = text ?? "", amount = amount });
        }
    }
}
#endif
