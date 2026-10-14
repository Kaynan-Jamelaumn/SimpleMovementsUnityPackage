#if FISHNET
using System;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using HubWorld;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Replicates a player's <see cref="PlayerRealmState"/> - realm, destination, the cycle they went out in, their zone
    /// and PvP flag, and the arrival they are waiting for - from the server's director to every client that sees the
    /// player. Put it on the root of the player prefab, next to <see cref="NetPlayer"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Server.</b> The director decides the realm (gates, travel, the jump, dungeons) and the zone; this
    /// component copies the result into <see cref="View"/> whenever it changes.</para>
    /// <para><b>Clients.</b> The client's director writes the view into the player's <see cref="PlayerRealmState"/>
    /// (<see cref="HubWorldDirector.ApplyReplicatedRealm"/>), so the zone label ("Safe Zone" / "Dangerous Zone - PvP
    /// Active"), the PvP verdicts used by targeting and UI, duel checks and the City's local hiding read the server's
    /// decisions.</para>
    /// <para><b>Arrivals.</b> After the server moved a remote player into another realm it keeps them frozen until their
    /// client has loaded the destination and has the ground under them: the owning client then confirms the
    /// <see cref="RealmView.arrivalToken"/> (<see cref="ServerConfirmArrival"/>). The server gives the controls back
    /// after <see cref="HubWorldDirector.ClientArrivalTimeout"/> seconds anyway.</para>
    /// <para><b>Gate requests.</b> Gates are triggers decided on the server, but a client may also ask to use the gate its
    /// player stands at (<see cref="RequestGate"/>, installed as <see cref="HubWorldDirector.ClientTravelRequest"/>): the
    /// server checks the player is alive and at such a gate in their own scene, then decides as for the trigger.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class NetPlayerRealm : NetworkBehaviour
    {
        /// <summary>A player's realm as sent to the clients.</summary>
        public struct RealmView : IEquatable<RealmView>
        {
            public RealmKind realm;
            /// <summary>While in transit: where the player is going.</summary>
            public RealmKind destination;
            /// <summary>The world cycle the player went out in (-1 = has not left the city).</summary>
            public int worldCycle;
            public int zoneIndex;
            public string zoneName;
            public bool pvp;
            public float dungeonDifficulty;
            public float mobStrength;
            public float rewardMultiplier;
            public Color zoneColor;
            /// <summary>Non-zero while the server waits for the owner's client to confirm an arrival.</summary>
            public int arrivalToken;

            /// <summary>The view of a state (server side).</summary>
            public static RealmView Of(PlayerRealmState s)
            {
                if (s == null)
                    return new RealmView { worldCycle = -1, zoneIndex = -4, zoneName = "" };
                ZoneInfo z = s.Zone;
                return new RealmView
                {
                    realm = s.Realm,
                    destination = s.Destination,
                    worldCycle = s.WorldCycle,
                    zoneIndex = z.index,
                    zoneName = z.name ?? "",
                    pvp = z.pvp,
                    dungeonDifficulty = z.dungeonDifficulty,
                    mobStrength = z.mobStrength,
                    rewardMultiplier = z.rewardMultiplier,
                    zoneColor = z.color,
                    arrivalToken = s.ArrivalToken,
                };
            }

            /// <summary>
            /// The zone as a <see cref="ZoneInfo"/>: the server's values, with the distance from the city measured here
            /// (<paramref name="zones"/> may be null).
            /// </summary>
            public ZoneInfo ToZone(Vector3 position, ZoneMap zones) => new ZoneInfo
            {
                index = zoneIndex,
                name = zoneName ?? "",
                pvp = pvp,
                distance = realm == RealmKind.World && zones != null ? zones.DistanceFromCity(position) : 0f,
                depth = 0f,
                dungeonDifficulty = dungeonDifficulty,
                mobStrength = mobStrength,
                rewardMultiplier = rewardMultiplier,
                color = zoneColor,
            };

            public bool Equals(RealmView o) =>
                realm == o.realm && destination == o.destination && worldCycle == o.worldCycle && zoneIndex == o.zoneIndex &&
                string.Equals(zoneName, o.zoneName, StringComparison.Ordinal) && pvp == o.pvp &&
                dungeonDifficulty.Equals(o.dungeonDifficulty) && mobStrength.Equals(o.mobStrength) &&
                rewardMultiplier.Equals(o.rewardMultiplier) && zoneColor == o.zoneColor && arrivalToken == o.arrivalToken;

            public override bool Equals(object obj) => obj is RealmView o && Equals(o);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = (int)realm;
                    h = h * 397 ^ (int)destination;
                    h = h * 397 ^ worldCycle;
                    h = h * 397 ^ zoneIndex;
                    h = h * 397 ^ (pvp ? 1 : 0);
                    return h * 397 ^ arrivalToken;
                }
            }
        }

        /// <summary>The player's realm as the server decided it (every observer receives it).</summary>
        public readonly SyncVar<RealmView> View = new SyncVar<RealmView>(new RealmView { worldCycle = -1, zoneIndex = -4, zoneName = "" }, new SyncTypeSettings(0f));

        [Tooltip("Metres a player may stand from a gate's trigger when their client asks to use that gate (server check).")]
        [SerializeField, Min(0f)] private float gateTolerance = 4f;

        [Tooltip("Seconds between two gate requests of the same player (both on the client and on the server).")]
        [SerializeField, Min(0.05f)] private float requestInterval = 0.5f;

        [Tooltip("Seconds the owner's client waits for the ground under its player after an arrival before confirming anyway.")]
        [SerializeField, Min(1f)] private float groundWaitLimit = 15f;

        private CombatEntity entity;
        private HubWorldDirector appliedDirector;
        private bool viewDirty = true;
        private float nextClientRequest, nextServerRequest;
        private int confirmedToken, waitingToken;
        private float waitingSince;

        /// <summary>The player's combat entity.</summary>
        public CombatEntity Entity
        {
            get
            {
                if (entity == null)
                {
                    NetPlayer player = GetComponent<NetPlayer>();
                    entity = player != null && player.Entity != null ? player.Entity : GetComponentInChildren<CombatEntity>(true);
                }
                return entity;
            }
        }

        // ------------------------------------------------------------------ lifecycle

        private void Awake() => View.OnChange += OnViewChanged;

        private void OnDestroy() => View.OnChange -= OnViewChanged;

        public override void OnStartClient()
        {
            base.OnStartClient();
            viewDirty = true;
            appliedDirector = null;
            if (IsOwner && !IsServerInitialized)
                HubWorldDirector.ClientTravelRequest = SendGateRequest;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsOwner && HubWorldDirector.ClientTravelRequest == (Action<CombatEntity, RealmGate.GateKind>)SendGateRequest)
                HubWorldDirector.ClientTravelRequest = null;
        }

        private void OnViewChanged(RealmView previous, RealmView next, bool asServer)
        {
            if (!asServer)
                viewDirty = true;
        }

        private void Update()
        {
            if (IsServerInitialized)
            {
                ServerUpdate();
                return;
            }
            if (!IsClientInitialized)
                return;
            ClientApply();
            if (IsOwner)
                ConfirmArrivalWhenReady();
        }

        // ------------------------------------------------------------------ server

        private void ServerUpdate()
        {
            CombatEntity e = Entity;
            PlayerRealmState s = e != null ? PlayerRealmState.Of(e) : null;
            if (s == null)
                return; // the director adds it within half a second of the spawn
            RealmView v = RealmView.Of(s);
            if (!v.Equals(View.Value))
                View.Value = v;
        }

        /// <summary>The owner's client has loaded the destination and has the ground under its player.</summary>
        [ServerRpc]
        private void ServerConfirmArrival(int token)
        {
            HubWorldDirector director = HubWorldDirector.Instance;
            if (director != null && Entity != null && token != 0)
                director.ConfirmArrival(Entity, token);
        }

        /// <summary>The owner asks to use the gate its player stands at; the server checks and decides.</summary>
        [ServerRpc]
        private void ServerUseGate(RealmGate.GateKind gate)
        {
            if (Time.unscaledTime < nextServerRequest)
                return;
            nextServerRequest = Time.unscaledTime + requestInterval;
            HubWorldDirector director = HubWorldDirector.Instance;
            CombatEntity player = Entity;
            if (director == null || player == null || !player.IsAlive)
                return;
            if (gate != RealmGate.GateKind.CityGate && gate != RealmGate.GateKind.ReturnGate)
                return;
            // Like the gate's own trigger: nothing while travelling or waiting for this client to arrive.
            PlayerRealmState state = PlayerRealmState.Of(player);
            if (state == null || state.InTransit || state.AwaitingArrival)
                return;
            if (!RealmGate.IsNear(player, gate, gateTolerance))
            {
                HubWorldMessages.Send(player, "Walk up to the gate first.", HubMessageKind.Warning);
                return;
            }
            string reason;
            bool ok = gate == RealmGate.GateKind.CityGate
                ? director.RequestLeaveCity(player, out reason)
                : director.RequestReturnToCity(player, out reason);
            if (!ok && !string.IsNullOrEmpty(reason))
                HubWorldMessages.Send(player, reason, HubMessageKind.Warning);
        }

        // ------------------------------------------------------------------ clients

        private void ClientApply()
        {
            HubWorldDirector director = HubWorldDirector.Instance;
            CombatEntity e = Entity;
            if (director == null || e == null)
                return; // applied once the City (and its director) is loaded here
            if (appliedDirector != director)
            {
                appliedDirector = director;
                viewDirty = true;
            }
            if (!viewDirty)
                return;
            viewDirty = false;
            RealmView v = View.Value;
            director.ApplyReplicatedRealm(e, v.realm, v.destination, v.worldCycle, v.ToZone(e.Position, director.Zones), v.arrivalToken);
        }

        /// <summary>Owner: confirms an arrival once the destination is loaded here with ground under the player (or after a while).</summary>
        private void ConfirmArrivalWhenReady()
        {
            int token = View.Value.arrivalToken;
            if (token == 0 || token == confirmedToken)
            {
                waitingToken = 0;
                return;
            }
            if (waitingToken != token)
            {
                waitingToken = token;
                waitingSince = Time.unscaledTime;
            }
            if (!ArrivedHere(View.Value.realm) && Time.unscaledTime - waitingSince < groundWaitLimit)
                return;
            confirmedToken = token;
            waitingToken = 0;
            ServerConfirmArrival(token);
        }

        /// <summary>Is the player in a loaded scene of <paramref name="realm"/> here, standing over that scene's ground?</summary>
        private bool ArrivedHere(RealmKind realm)
        {
            GameObject mover = Entity != null ? RealmTravel.Mover(Entity) : null;
            if (mover == null)
                return false;
            RealmScene scene = RealmScene.ForScene(mover.scene);
            if (scene == null || scene.realm != realm)
                return false;
            return RealmTravel.TryFindGroundIn(mover.transform.position, mover.scene, out _);
        }

        /// <summary>Owner side: asks the server to use the gate the player stands at (rate-limited).</summary>
        public void RequestGate(RealmGate.GateKind gate)
        {
            if (!IsOwner || !IsClientInitialized || Time.unscaledTime < nextClientRequest)
                return;
            nextClientRequest = Time.unscaledTime + requestInterval;
            ServerUseGate(gate);
        }

        /// <summary><see cref="HubWorldDirector.ClientTravelRequest"/>: finds the asking player's relay and sends the request.</summary>
        private static void SendGateRequest(CombatEntity player, RealmGate.GateKind gate)
        {
            NetworkObject nob = NetObjects.Of(player);
            NetPlayerRealm realm = nob != null ? nob.GetComponent<NetPlayerRealm>() : null;
            if (realm != null)
                realm.RequestGate(gate);
        }
    }
}
#endif
