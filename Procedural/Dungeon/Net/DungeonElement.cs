using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// The network identity of an interactive object a dungeon builds (chest, door, key, portal, lever, shrine...): an id
    /// that is the same on the server and on every client, because it comes from the layout and not from the order or
    /// the machine the object was created on. Added by the builder; see <see cref="DungeonNetIds"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class DungeonElement : MonoBehaviour
    {
        /// <summary>The element's id within its dungeon (see <see cref="DungeonNetIds"/>).</summary>
        public int Id { get; internal set; } = -1;

        /// <summary>The dungeon that built it.</summary>
        public DungeonInstance Dungeon { get; internal set; }

        /// <summary>The floor it stands on.</summary>
        public int Floor { get; internal set; }

        /// <summary>The object's own state (open, used, broken...) as last published or applied.</summary>
        public int State { get; internal set; }

        /// <summary>Optional data that goes with <see cref="State"/>.</summary>
        public byte[] StatePayload { get; internal set; }

        /// <summary>Has <see cref="State"/> been published or applied since the object was built?</summary>
        public bool HasState { get; internal set; }

        /// <summary>Whether the object is shown (see <see cref="DungeonNet.Presence"/>) as last published or applied.</summary>
        public int Presence { get; internal set; }

        /// <summary>Optional data that goes with <see cref="Presence"/> (the position it was revealed at).</summary>
        public byte[] PresencePayload { get; internal set; }

        /// <summary>Has <see cref="Presence"/> been published or applied since the object was built?</summary>
        public bool HasPresence { get; internal set; }

        /// <summary>Forgets the published states (the object goes back to a pool or is built again).</summary>
        internal void ResetStates()
        {
            State = 0;
            StatePayload = null;
            HasState = false;
            Presence = 0;
            PresencePayload = null;
            HasPresence = false;
        }
    }

    /// <summary>
    /// Deterministic ids of the interactive objects of a dungeon. A placement's object uses the index of its placement in
    /// the server's layout (which clients receive with each floor); objects that are not placements get an id from their
    /// kind and position in the layout (secret doors by floor and cell, climbs by link). So the
    /// ids do not depend on the build order, on objects only the server creates (mobs, item pickups) or on which floors a
    /// client was sent.
    /// </summary>
    public static class DungeonNetIds
    {
        /// <summary>Ids from here on are secret doors (floor and cell).</summary>
        public const int SecretDoorBase = 0x10000000;
        /// <summary>Ids from here on are floors' own mechanics (see <see cref="ForFloor"/>).</summary>
        public const int FloorMechanicBase = 0x20000000;
        /// <summary>Ids from here on are vertical links (climbs).</summary>
        public const int LinkBase = 0x30000000;
        /// <summary>Largest grid cell index a secret door id can hold.</summary>
        public const int MaxCell = (1 << 20) - 1;
        /// <summary>Largest floor index an id can hold.</summary>
        public const int MaxFloor = 255;

        /// <summary>The id of the object built for placement <paramref name="placementId"/> (its index in the server's layout).</summary>
        public static int ForPlacement(int placementId) => placementId >= 0 && placementId < SecretDoorBase ? placementId : -1;

        /// <summary>The id of the secret door in cell <paramref name="cell"/> of floor <paramref name="floor"/> (-1 when out of range).</summary>
        public static int ForSecretDoor(int floor, int cell) =>
            floor >= 0 && floor <= MaxFloor && cell >= 0 && cell <= MaxCell ? SecretDoorBase + (floor << 20) + cell : -1;

        /// <summary>
        /// The id reserved for floor <paramref name="floor"/>'s own mechanics (chasm falls, gravity flips, shifting walls).
        /// Not assigned yet: those components sit on the floor's root, whose children must not inherit an element; a
        /// follow-up networking them puts them on a child of their own and gives it this id.
        /// </summary>
        public static int ForFloor(int floor) => floor >= 0 && floor <= MaxFloor ? FloorMechanicBase + floor : -1;

        /// <summary>The id of the object built for vertical link <paramref name="linkId"/> (climbs).</summary>
        public static int ForLink(int linkId) => linkId >= 0 && linkId < SecretDoorBase ? LinkBase + linkId : -1;

        /// <summary>The element <paramref name="c"/> belongs to (itself or a parent), or null.</summary>
        public static DungeonElement ElementOf(Component c) => c != null ? c.GetComponentInParent<DungeonElement>(true) : null;

        /// <summary>The id of the element <paramref name="c"/> belongs to, or -1.</summary>
        public static int Of(Component c)
        {
            DungeonElement e = ElementOf(c);
            return e != null ? e.Id : -1;
        }

        /// <summary>The element <paramref name="id"/> of <paramref name="dungeon"/>, or null.</summary>
        public static DungeonElement Find(DungeonInstance dungeon, int id) => dungeon != null ? dungeon.Element(id) : null;

        /// <summary>Gives <paramref name="go"/> the id <paramref name="id"/> in <paramref name="dungeon"/> (the builder calls it in network sessions).</summary>
        internal static DungeonElement Assign(DungeonInstance dungeon, GameObject go, int id, int floor)
        {
            // Single player has nobody to tell: its dungeons stay exactly as they were (no extra component).
            if (dungeon == null || go == null || id < 0 || !GameNet.NetAuthority.IsNetworked)
                return null;
            DungeonElement e = go.GetComponent<DungeonElement>();
            if (e == null)
                e = go.AddComponent<DungeonElement>();
            e.ResetStates();
            e.Id = id;
            e.Dungeon = dungeon;
            e.Floor = floor;
            dungeon.RegisterElement(e);
            return e;
        }
    }
}
