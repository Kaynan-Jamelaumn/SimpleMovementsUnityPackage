#if FISHNET
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Networks a character's injuries (<see cref="InjuryController"/>): the server runs every injury rule and sends the
    /// state whenever it changes - at most every Send Interval.
    /// <list type="bullet">
    /// <item>The character's own client gets everything (body-part condition, injuries and illnesses, missing limbs, the
    /// treatment being applied): its Health tab, HUD and screen effects show it, and it uses it for what it predicts
    /// itself (movement limits, the hands).</item>
    /// <item>Everyone else gets what they can see of someone else's body: which limbs are missing and how impaired each
    /// part is (enough to limp, hop and crawl, and to pick up a lost limb) - a few dozen bytes, sent only when that
    /// changes, not on every tick of healing. Turn Share Injuries With Everyone on to send them everything too (a game
    /// where players examine each other).</item>
    /// </list>
    /// The owner's treatments (Health tab, medical items from the hotbar) are sent to the server as requests, checked
    /// there like local ones.
    /// <para>Put it on the player prefab root next to the NetworkObject (Network ▸ Make Networked adds it). Offline, or
    /// with the injury system off, it does nothing.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class NetInjuries : NetworkBehaviour
    {
        [Tooltip("Least seconds between two state updates while the injuries change (bleeding, healing progress).")]
        [SerializeField, Min(0.05f)] private float sendInterval = 0.25f;
        [Tooltip("Least seconds between two treatment requests of the owner.")]
        [SerializeField, Min(0f)] private float requestInterval = 0.3f;
        [Tooltip("Every client is sent every character's injuries. Off (less traffic): only the character's own client is; the " +
                 "others get the missing limbs and how impaired each part is.")]
        [SerializeField] private bool shareInjuriesWithEveryone = false;

        private InjuryController injuries;
        private int sentVersion = int.MinValue;
        private float nextSend;
        private float lastRequest = float.NegativeInfinity;
        private float lastStateRequest = float.NegativeInfinity;
        private bool requestHooked;
        private byte[] lastPublic;

        private InjuryController Injuries
        {
            get
            {
                if (injuries == null)
                    injuries = GetComponentInChildren<InjuryController>(true);
                return injuries;
            }
        }

        // ------------------------------------------------------------------ server
        public override void OnStartServer()
        {
            base.OnStartServer();
            sentVersion = int.MinValue;
            lastPublic = null;
        }

        private void Update()
        {
            if (!IsServerInitialized)
            {
                HookRequests();
                return;
            }
            InjuryController c = Injuries;
            if (c == null || c.Version == sentVersion || Time.unscaledTime < nextSend)
                return;
            sentVersion = c.Version;
            nextSend = Time.unscaledTime + sendInterval;
            if (shareInjuriesWithEveryone)
            {
                ObserversInjuries(c.CaptureSnapshot());
                return;
            }
            if (Owner != null && Owner.IsActive)
                TargetInjuries(Owner, c.CaptureSnapshot());
            // What the others may know changes far less often than the injuries heal: sent only when it did.
            byte[] visible = c.CapturePublicSnapshot();
            if (!Same(visible, lastPublic))
            {
                lastPublic = visible;
                ObserversVisibleInjuries(visible);
            }
        }

        private static bool Same(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        /// <summary>The whole injury state, to every client that sees the character (the last one is kept for late joiners).</summary>
        [ObserversRpc(ExcludeServer = true, BufferLast = true)]
        private void ObserversInjuries(byte[] state) => Apply(state);

        /// <summary>What everyone may know of the character's body, to the clients that are not its own (kept for late joiners).</summary>
        [ObserversRpc(ExcludeServer = true, ExcludeOwner = true, BufferLast = true)]
        private void ObserversVisibleInjuries(byte[] state) => Apply(state);

        /// <summary>The whole injury state, to the character's own client.</summary>
        [TargetRpc]
        private void TargetInjuries(NetworkConnection conn, byte[] state) => Apply(state);

        private void Apply(byte[] state)
        {
            InjuryController c = Injuries;
            if (c != null && state != null && state.Length <= 65536)
                c.ApplySnapshot(state);
        }

        /// <summary>
        /// The owner's client asks for the whole state (it just started, or just became the owner): nothing is kept for
        /// it the way the observers' last state is.
        /// </summary>
        [ServerRpc]
        private void ServerRequestState()
        {
            if (!StatusReplication.Allow(ref lastStateRequest, Time.unscaledTime, 1f))
                return;
            sentVersion = int.MinValue;
            nextSend = 0f;
        }

        // ------------------------------------------------------------------ owner requests
        public override void OnStartClient()
        {
            base.OnStartClient();
            HookRequests();
            AskForState();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            AskForState();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (injuries != null && requestHooked)
                injuries.TreatmentRequest = null;
            requestHooked = false;
        }

        private void AskForState()
        {
            if (IsClientInitialized && IsOwner && !IsServerInitialized)
                ServerRequestState();
        }

        /// <summary>The owner's client copy sends its treatments to the server (the controller may appear after the spawn).</summary>
        private void HookRequests()
        {
            if (requestHooked || !IsClientInitialized || IsServerInitialized || !IsOwner)
                return;
            InjuryController c = Injuries;
            if (c == null)
                return;
            c.TreatmentRequest = SendTreatment;
            requestHooked = true;
        }

        private void SendTreatment(InjuryTreatmentRequest r)
        {
            if (!IsClientInitialized || !IsOwner)
                return;
            ServerTreat(r.partId ?? "", r.injuryUid, r.itemId ?? "", r.treatmentIndex);
        }

        /// <summary>The owner asks for a treatment with a medical item; the server checks everything as for a local player.</summary>
        [ServerRpc]
        private void ServerTreat(string partId, int injuryUid, string itemId, int treatmentIndex)
        {
            InjuryController c = Injuries;
            if (c == null || !c.Settings.enabled)
                return;
            if (!StatusReplication.Allow(ref lastRequest, Time.unscaledTime, requestInterval))
                return;
            if (partId != null && partId.Length > StatusReplication.MaxNameLength)
                return;
            // Treating every bleeding wound names no item; every other request must.
            bool allBleeding = treatmentIndex == InjuryTreatmentRequest.AllBleeding;
            if (!allBleeding && (string.IsNullOrEmpty(itemId) || itemId.Length > 128))
                return;
            c.ServerHandleRequest(new InjuryTreatmentRequest { partId = partId, injuryUid = injuryUid, itemId = allBleeding ? "" : itemId, treatmentIndex = treatmentIndex });
        }
    }
}
#endif
