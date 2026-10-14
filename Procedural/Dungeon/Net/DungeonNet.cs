using System;
using System.Collections.Generic;
using GameNet;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Implemented by the parts of a dungeon object whose state the server sends to clients (open, used, broken...):
    /// a client's copy shows the state it receives, without making any gameplay decision of its own.
    /// </summary>
    public interface IDungeonNetState
    {
        /// <summary>Shows <paramref name="state"/> (published by the server's copy) on a client's copy.</summary>
        void ApplyNetState(int state, byte[] payload);
    }

    /// <summary>
    /// Implemented by dungeon objects players can use through the network: the server calls it for a client's validated
    /// request (the requesting player is a participant of the run, alive and within <see cref="NetInteractRange"/>).
    /// </summary>
    public interface IDungeonNetInteract
    {
        /// <summary>How close (metres, from the server's positions) a player must be to use it.</summary>
        float NetInteractRange { get; }

        /// <summary>Uses the object for <paramref name="player"/> on the server. Returns true when something happened.</summary>
        bool NetInteract(CombatEntity player);
    }

    /// <summary>
    /// The dungeon side of the networking (always compiled). Offline - Fish-Net missing or no session - every member is
    /// a no-op and the dungeon behaves exactly as in single player. In a session the server's dungeon is the truth: its
    /// objects publish their state changes here and the network layer (<see cref="Relay"/>, installed by
    /// GameNet.NetDungeonRun) sends them to the players of that run; clients build a presentation-only copy (a "replica",
    /// see <see cref="DungeonInstance.IsReplica"/>) whose objects show the received states and ask the server to be used.
    /// </summary>
    public static class DungeonNet
    {
        /// <summary>What a published state is about.</summary>
        public enum Channel : byte
        {
            /// <summary>The object's own state (see <see cref="IDungeonNetState"/>).</summary>
            State = 0,
            /// <summary>Whether the object is shown (see <see cref="Presence"/>).</summary>
            Presence = 1,
        }

        /// <summary>Values of the <see cref="Channel.Presence"/> channel.</summary>
        public static class Presence
        {
            /// <summary>Hidden until its room's event reveals it (dormant).</summary>
            public const int Hidden = 0;
            /// <summary>Revealed (the payload holds the position it appeared at).</summary>
            public const int Revealed = 1;
            /// <summary>Gone (a key picked up...).</summary>
            public const int Removed = 2;
        }

        /// <summary>The network layer's part (installed while a session runs).</summary>
        public interface IRelay
        {
            /// <summary>Server: sends an element's new state to the players of its run.</summary>
            void PublishState(DungeonElement element, Channel channel, int state, byte[] payload);

            /// <summary>Server: shows a message to the players of a dungeon's run.</summary>
            void Message(DungeonInstance dungeon, string text, bool important);

            /// <summary>Owning client: asks the server to use an element for this machine's player. True when sent.</summary>
            bool RequestInteract(DungeonElement element, GameObject player);

            /// <summary>
            /// Server: was floor <paramref name="floor"/> of <paramref name="dungeon"/> already sent to its run's clients?
            /// True when no relay serves the dungeon's scene (nothing to wait for).
            /// </summary>
            bool IsFloorRevealed(DungeonInstance dungeon, int floor);
        }

        /// <summary>Installed by the network layer (GameNet.NetDungeonRun); null offline.</summary>
        public static IRelay Relay { get; set; }

        /// <summary>
        /// Components that make gameplay decisions and nothing else a client would see (room events, puzzles, falls,
        /// teleports, gravity flips, shifting walls, traps that fire, nests that hatch, respawns): switched off on a
        /// client's copy of the dungeon - the server runs them and replicates what they change.
        /// </summary>
        public static readonly Type[] ServerOnlyOnReplicas =
        {
            typeof(DungeonRoomEvent), typeof(DungeonPuzzle), typeof(DungeonShiftingFloor), typeof(DungeonGravityShift),
            typeof(DungeonChasm), typeof(DungeonPressurePlate), typeof(DungeonTripwire), typeof(DungeonTeleporter),
            typeof(DungeonDartTrap), typeof(DungeonRespawnDirector), typeof(DungeonNest), typeof(DungeonSleeper),
            typeof(DungeonRoamer),
        };

        private static readonly HashSet<Type> serverOnly = new HashSet<Type>(ServerOnlyOnReplicas);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Relay = null;

        /// <summary>A network session in which this machine is the server (host or dedicated).</summary>
        public static bool ServerSession => NetAuthority.IsNetworked && NetAuthority.IsServer;

        /// <summary>
        /// Is <paramref name="c"/> part of a client's presentation-only copy of a dungeon? Such objects show what the
        /// server sends them and decide nothing (no opening, no damage, no pickups, no travel).
        /// </summary>
        public static bool IsReplica(Component c)
        {
            if (c == null || !NetAuthority.IsClientOnly)
                return false;
            DungeonInstance d = DungeonOf(c);
            return d != null && d.IsReplica;
        }

        /// <summary>
        /// Server: <paramref name="c"/>'s element changed state. Remembered on the element (players who arrive later get
        /// it) and, in a session, sent to the players of its run. Offline nothing is sent.
        /// </summary>
        public static void Publish(Component c, int state, byte[] payload = null, Channel channel = Channel.State)
        {
            if (c == null || IsReplica(c))
                return;
            DungeonElement e = DungeonNetIds.ElementOf(c);
            if (e == null || e.Id < 0 || e.Dungeon == null)
                return;
            Record(e, channel, state, payload);
            if (ServerSession)
                Relay?.PublishState(e, channel, state, payload);
        }

        /// <summary>
        /// Client: shows a state the server published on the replica's element (and remembers it, so objects built or
        /// re-enabled later show it too).
        /// </summary>
        public static void Apply(DungeonElement e, Channel channel, int state, byte[] payload)
        {
            if (e == null)
                return;
            Record(e, channel, state, payload);
            if (channel == Channel.Presence)
            {
                ApplyPresence(e, state, payload);
                return;
            }
            var sinks = new List<IDungeonNetState>(2);
            e.GetComponentsInChildren(true, sinks);
            foreach (IDungeonNetState sink in sinks)
            {
                try
                {
                    sink.ApplyNetState(state, payload);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, e);
                }
            }
        }

        /// <summary>
        /// Client: <paramref name="target"/> (found by the player's interaction ray) belongs to a replica dungeon: asks
        /// the server to use it. False when it is not a dungeon element or nothing could be sent.
        /// </summary>
        public static bool TryRelayInteract(Component target, GameObject player)
        {
            if (target == null || Relay == null || !IsReplica(target))
                return false;
            DungeonElement e = DungeonNetIds.ElementOf(target);
            return e != null && e.Id >= 0 && Relay.RequestInteract(e, player);
        }

        /// <summary>The dungeon <paramref name="c"/> belongs to (through its element, its placement tag or its parents), or null.</summary>
        public static DungeonInstance DungeonOf(Component c)
        {
            if (c == null)
                return null;
            DungeonElement e = c.GetComponentInParent<DungeonElement>(true);
            if (e != null && e.Dungeon != null)
                return e.Dungeon;
            var tag = c.GetComponentInParent<DungeonSpawned>(true);
            if (tag != null && tag.Dungeon != null)
                return tag.Dungeon;
            return c.GetComponentInParent<DungeonInstance>(true);
        }

        /// <summary>Server: shows a message to the players of <paramref name="dungeon"/>'s run.</summary>
        internal static void Message(DungeonInstance dungeon, string text, bool important)
        {
            if (dungeon != null && ServerSession && !dungeon.IsReplica)
                Relay?.Message(dungeon, text, important);
        }

        /// <summary>
        /// A hidden (dormant) object was revealed by its room's event. On a server, a mob is network-spawned now (Fish-Net
        /// does not replicate SetActive, so dormant mobs are kept back until they appear), an item pickup is published as
        /// a world item, and any other element tells the clients where it appeared. Offline nothing is sent.
        /// </summary>
        internal static void OnRevealed(DungeonSpawned s)
        {
            if (s == null || s.Dungeon == null || s.Dungeon.IsReplica)
                return;
            if (s.kind == PlacementKind.Mob || s.kind == PlacementKind.Boss)
            {
                SpawnContent(s.Dungeon, s.gameObject, s.floor, true);
                return;
            }
            DungeonElement e = s.GetComponent<DungeonElement>();
            if (e == null)
            {
                // Not built on clients (an item pickup): the networked world item shows up by itself.
                if (s.transform.parent == null)
                    SpawnContent(s.Dungeon, s.gameObject, s.floor, false);
                return;
            }
            Publish(e, Presence.Revealed, PositionPayload(s.Dungeon.transform.InverseTransformPoint(s.transform.position)), Channel.Presence);
        }

        /// <summary>
        /// Hands a mob (<paramref name="mob"/>) or an item pickup that <paramref name="dungeon"/> created on floor
        /// <paramref name="floor"/> to the network. On a server in a session that happens at once when the floor was
        /// already sent to the run's clients, and otherwise when it is (<see cref="FloorRevealed"/>): network objects are
        /// seen by every client in the run's scene, so spawning them early would tell clients where the mobs of deeper
        /// floors stand and which items lie there - what the progressive reveal keeps from them. Until then the object
        /// exists on the server only (mobs stay under their floor). Offline, or without a relay for the dungeon's scene,
        /// it is handed over at once, which offline changes nothing (see <see cref="MobNetSpawn.Spawn(GameObject, UnityEngine.SceneManagement.Scene)"/>
        /// and <see cref="WorldItems.Publish"/>).
        /// </summary>
        public static void SpawnContent(DungeonInstance dungeon, GameObject go, int floor, bool mob)
        {
            if (go == null)
                return;
            IRelay relay = Relay;
            if (dungeon != null && !dungeon.IsReplica && ServerSession && relay != null && !relay.IsFloorRevealed(dungeon, floor))
            {
                dungeon.HoldBack(go, floor, mob);
                return;
            }
            SpawnNow(dungeon, go, mob);
        }

        /// <summary>
        /// Server (called by the network layer once floor <paramref name="floor"/> of <paramref name="dungeon"/> was sent
        /// to its run's clients): the mobs and item pickups kept back for that floor are network-spawned now.
        /// </summary>
        public static void FloorRevealed(DungeonInstance dungeon, int floor)
        {
            if (dungeon == null || dungeon.IsReplica)
                return;
            foreach (DungeonInstance.HeldContent h in dungeon.TakeHeld(floor))
            {
                if (h.Object == null)
                    continue;   // killed or taken on the server meanwhile
                try
                {
                    SpawnNow(dungeon, h.Object, h.Mob);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, h.Object);
                }
            }
        }

        private static void SpawnNow(DungeonInstance dungeon, GameObject go, bool mob)
        {
            if (mob)
                MobNetSpawn.Spawn(go, dungeon != null ? dungeon.gameObject.scene : go.scene);
            else
                WorldItems.Publish(go, dungeon != null ? dungeon.gameObject : null);
        }

        /// <summary>Switches off the server-only components (see <see cref="ServerOnlyOnReplicas"/>) under <paramref name="root"/>.</summary>
        public static void PrepareReplica(GameObject root)
        {
            if (root == null)
                return;
            foreach (MonoBehaviour b in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (b != null && IsServerOnly(b.GetType()))
                    b.enabled = false;
        }

        /// <summary>Is <paramref name="type"/> one of the server-only components (or derived from one)?</summary>
        public static bool IsServerOnly(Type type)
        {
            for (Type t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                if (serverOnly.Contains(t))
                    return true;
            return false;
        }

        /// <summary>The payload of a revealed object: where it appeared (local to its dungeon).</summary>
        public static byte[] PositionPayload(Vector3 local)
        {
            var bytes = new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes(local.x), 0, bytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(local.y), 0, bytes, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(local.z), 0, bytes, 8, 4);
            return bytes;
        }

        /// <summary>Reads <see cref="PositionPayload"/>; false when it holds no valid position.</summary>
        public static bool TryReadPosition(byte[] payload, out Vector3 local)
        {
            local = default;
            if (payload == null || payload.Length < 12)
                return false;
            local = new Vector3(BitConverter.ToSingle(payload, 0), BitConverter.ToSingle(payload, 4), BitConverter.ToSingle(payload, 8));
            return !(float.IsNaN(local.x) || float.IsNaN(local.y) || float.IsNaN(local.z) ||
                     float.IsInfinity(local.x) || float.IsInfinity(local.y) || float.IsInfinity(local.z));
        }

        private static void Record(DungeonElement e, Channel channel, int state, byte[] payload)
        {
            if (channel == Channel.Presence)
            {
                e.Presence = state;
                e.PresencePayload = payload;
                e.HasPresence = true;
            }
            else
            {
                e.State = state;
                e.StatePayload = payload;
                e.HasState = true;
            }
        }

        private static void ApplyPresence(DungeonElement e, int state, byte[] payload)
        {
            GameObject go = e.gameObject;
            switch (state)
            {
                case Presence.Revealed:
                    if (e.Dungeon != null && TryReadPosition(payload, out Vector3 local))
                        go.transform.position = e.Dungeon.transform.TransformPoint(local);
                    var tag = go.GetComponent<DungeonSpawned>();
                    if (tag != null)
                        tag.Reveal();
                    else if (!go.activeSelf)
                        go.SetActive(true);
                    break;
                case Presence.Removed:
                    if (go.activeSelf)
                        go.SetActive(false);
                    break;
                default:
                    if (go.activeSelf)
                        go.SetActive(false);
                    break;
            }
        }
    }
}
