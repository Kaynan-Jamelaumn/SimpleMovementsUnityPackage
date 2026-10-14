using System;
using UnityEngine;

namespace HubWorld
{
    /// <summary>
    /// Where one player is in the hub world: realm (city, world, dungeon, in transit), zone, and the world cycle they went
    /// out in. Added to every player by the <see cref="HubWorldDirector"/>, which is the only writer (the authority); the
    /// combat rules, HUD and duels read it.
    /// </summary>
    /// <remarks>
    /// In a network session the server's director decides these values and the player's <c>NetPlayerRealm</c> replicates
    /// them; on a client the director writes them from that replication (<see cref="HubWorldDirector.ApplyReplicatedRealm"/>),
    /// so the HUD, the combat rules and the duels read the server's decisions there too.
    /// </remarks>
    [DisallowMultipleComponent]
    public class PlayerRealmState : MonoBehaviour
    {
        public CombatEntity Entity { get; internal set; }
        /// <summary>The realm the player is in.</summary>
        public RealmKind Realm { get; internal set; } = RealmKind.None;
        /// <summary>While in transit: where the player is going.</summary>
        public RealmKind Destination { get; internal set; } = RealmKind.None;
        /// <summary>The world cycle the player went out in (-1 = has not left the city in this session).</summary>
        public int WorldCycle { get; internal set; } = -1;
        /// <summary>The zone the player is in (City, a ring of the world, Dungeon).</summary>
        public ZoneInfo Zone { get; internal set; }
        /// <summary>Time.time when the realm last changed.</summary>
        public float RealmSince { get; internal set; }
        /// <summary>
        /// Network sessions: non-zero while the server keeps the player frozen after an arrival, until their client has
        /// loaded the destination and the ground under them (the client confirms this token). Always 0 offline.
        /// </summary>
        public int ArrivalToken { get; internal set; }
        /// <summary>Is the player waiting, frozen, for their client to finish arriving (see <see cref="ArrivalToken"/>)?</summary>
        public bool AwaitingArrival => ArrivalToken != 0;

        /// <summary>Realm changed (previous, current).</summary>
        public event Action<RealmKind, RealmKind> RealmChanged;
        /// <summary>Zone changed (previous, current).</summary>
        public event Action<ZoneInfo, ZoneInfo> ZoneChanged;

        public bool InCity => Realm == RealmKind.City;
        public bool InWorld => Realm == RealmKind.World;
        public bool InDungeon => Realm == RealmKind.Dungeon;
        public bool InTransit => Realm == RealmKind.Transit;

        /// <summary>The state of a player (null when the hub world does not know them).</summary>
        public static PlayerRealmState Of(CombatEntity player)
        {
            if (player == null)
                return null;
            PlayerRealmState s = player.GetComponent<PlayerRealmState>();
            return s != null ? s : player.GetComponentInParent<PlayerRealmState>();
        }

        internal void SetRealm(RealmKind realm, RealmKind destination = RealmKind.None)
        {
            RealmKind previous = Realm;
            Realm = realm;
            Destination = realm == RealmKind.Transit ? destination : RealmKind.None;
            if (previous == realm)
                return;
            RealmSince = Time.time;
            RealmChanged?.Invoke(previous, realm);
        }

        internal void SetZone(ZoneInfo zone)
        {
            ZoneInfo previous = Zone;
            Zone = zone;
            if (previous.index != zone.index || previous.pvp != zone.pvp || previous.name != zone.name)
                ZoneChanged?.Invoke(previous, zone);
        }
    }
}
