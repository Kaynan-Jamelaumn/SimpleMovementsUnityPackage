#if FISHNET
using FishNet.Object;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Networks a character's injuries (<see cref="InjuryController"/>): the server runs every injury rule and sends the
    /// state (body-part condition, injuries, missing limbs, the treatment being applied) to everyone who sees the character
    /// whenever it changes - at most every Send Interval. Client copies show it (Health tab, missing limbs, the HUD alert)
    /// and use it for what they predict themselves (movement limits, the hands). The owner's treatments (Health tab,
    /// medical items from the hotbar) are sent to the server as requests, checked there like local ones.
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

        private InjuryController injuries;
        private int sentVersion = int.MinValue;
        private float nextSend;
        private float lastRequest = float.NegativeInfinity;
        private bool requestHooked;

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
            ObserversInjuries(c.CaptureSnapshot());
        }

        /// <summary>The injury state, to every client that sees the character (the last one is kept for late joiners).</summary>
        [ObserversRpc(ExcludeServer = true, BufferLast = true)]
        private void ObserversInjuries(byte[] state)
        {
            InjuryController c = Injuries;
            if (c != null && state != null && state.Length <= 65536)
                c.ApplySnapshot(state);
        }

        // ------------------------------------------------------------------ owner requests
        public override void OnStartClient()
        {
            base.OnStartClient();
            HookRequests();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (injuries != null && requestHooked)
                injuries.TreatmentRequest = null;
            requestHooked = false;
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
            if ((partId != null && partId.Length > StatusReplication.MaxNameLength) || string.IsNullOrEmpty(itemId) || itemId.Length > 128)
                return;
            c.ServerHandleRequest(new InjuryTreatmentRequest { partId = partId, injuryUid = injuryUid, itemId = itemId, treatmentIndex = treatmentIndex });
        }
    }
}
#endif
