using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameNet
{
    /// <summary>The part of a player's inventory a slot belongs to (network messages name slots by area and index).</summary>
    public enum InventoryArea : byte
    {
        /// <summary>The hotbar slots, in order.</summary>
        Hotbar = 0,
        /// <summary>The bag: the inventory slots, or the grid's cells row by row.</summary>
        Bag = 1,
        /// <summary>The equipment slots, in the order the inventory lists them.</summary>
        Equipment = 2,
        /// <summary>The slots of the storage window while a storage (chest) is open.</summary>
        Storage = 3,
    }

    /// <summary>
    /// A slot of a player's inventory as network messages name it: its area and its index in that area. The server and
    /// the client build the same inventory from the same prefab, so the same reference names the same slot on both.
    /// </summary>
    [Serializable]
    public struct InventorySlotRef : IEquatable<InventorySlotRef>
    {
        /// <summary>The <see cref="InventoryArea"/> (as a byte).</summary>
        public byte area;
        /// <summary>Index of the slot in its area; -1 = no slot.</summary>
        public short index;

        public InventorySlotRef(InventoryArea area, int index)
        {
            this.area = (byte)area;
            this.index = (short)Mathf.Clamp(index, -1, short.MaxValue);
        }

        /// <summary>No slot.</summary>
        public static InventorySlotRef None => new InventorySlotRef { area = 0, index = -1 };

        /// <summary>The area as an enum.</summary>
        public InventoryArea Area => (InventoryArea)area;

        /// <summary>Names a slot (a known area and an index in range)?</summary>
        public bool IsValid => area <= (byte)InventoryArea.Storage && index >= 0 && index < InventoryNetRules.MaxSlotsPerArea;

        public bool Equals(InventorySlotRef other) => area == other.area && index == other.index;
        public override bool Equals(object obj) => obj is InventorySlotRef r && Equals(r);
        public override int GetHashCode() => (area << 16) ^ index;
        public override string ToString() => IsValid ? $"{Area}[{index}]" : "none";
    }

    /// <summary>
    /// One stack of items in one slot, as the server sends it: which item (its <see cref="ItemDatabase"/> id), how many,
    /// the uses left of the unit in use and of each other unit, the rounds loaded, how it lies in the grid, and the
    /// seconds left on its use cooldown (shown only).
    /// </summary>
    [Serializable]
    public struct InventoryStackData : IEquatable<InventoryStackData>
    {
        /// <summary>The <see cref="InventoryArea"/> of the slot.</summary>
        public byte area;
        /// <summary>Index of the slot in its area (a grid item: the cell of its top-left corner).</summary>
        public short index;
        /// <summary>The item's <see cref="ItemDatabase"/> id.</summary>
        public string itemId;
        /// <summary>How many items the stack holds.</summary>
        public int count;
        /// <summary>Uses left of the unit in use.</summary>
        public float durability;
        /// <summary>Uses left of each other unit (the inventory item's durability list), may be null.</summary>
        public int[] durabilities;
        /// <summary>Rounds loaded in a magazine weapon (-1 = not loaded yet).</summary>
        public int loadedRounds;
        /// <summary>Turned sideways in the grid.</summary>
        public bool rotated;
        /// <summary>Seconds left before the stack can be used again (display only; not compared).</summary>
        public float cooldown;

        /// <summary>The slot this stack sits in.</summary>
        public InventorySlotRef Slot => new InventorySlotRef { area = area, index = index };

        /// <summary>Same item, amounts, uses, rounds and turn (the slot and the cooldown are not compared).</summary>
        public bool SameContent(InventoryStackData other)
        {
            return string.Equals(itemId ?? "", other.itemId ?? "", StringComparison.Ordinal)
                   && count == other.count
                   && durability.Equals(other.durability)
                   && loadedRounds == other.loadedRounds
                   && rotated == other.rotated
                   && InventoryNetRules.SameInts(durabilities, other.durabilities);
        }

        /// <summary>Same slot and same content (the cooldown, which changes every frame, is not compared).</summary>
        public bool Equals(InventoryStackData other) => area == other.area && index == other.index && SameContent(other);

        public override bool Equals(object obj) => obj is InventoryStackData d && Equals(d);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (area << 16) ^ index;
                h = h * 31 + (itemId != null ? itemId.GetHashCode() : 0);
                return h * 31 + count;
            }
        }

        public override string ToString() => $"{Slot}: {itemId} x{count}";
    }

    /// <summary>
    /// Everything the owner of an inventory sees, as the server has it: the layout (slot counts, grid), every occupied
    /// slot (hotbar, bag, equipment and, while one is open, the storage window), the selected hotbar slot and the
    /// quickslot assignments. Pure data: the server builds it from its copy of the inventory and sends it to the owning
    /// client only; the client shows it without running any gameplay (see <see cref="InventorySnapshotDiff"/>).
    /// <para>Always the whole inventory (not a delta): it is small (only occupied slots are listed), a lost or reordered
    /// update can never leave the client wrong, and a client that joins late needs nothing else.</para>
    /// </summary>
    [Serializable]
    public class InventorySnapshot
    {
        /// <summary>The last client request (its sequence number) the server had handled when it built this.</summary>
        public uint ack;
        /// <summary>Number of hotbar slots.</summary>
        public short hotbarCount;
        /// <summary>Number of bag slots (grid: cells).</summary>
        public short bagCount;
        /// <summary>The bag is a grid.</summary>
        public bool grid;
        /// <summary>Grid columns (grid only).</summary>
        public byte gridColumns;
        /// <summary>Grid rows (grid only).</summary>
        public byte gridRows;
        /// <summary>The selected hotbar slot (the item in hand).</summary>
        public byte selectedHotbar;
        /// <summary>Item id of each quickslot ("" = empty); may be null.</summary>
        public string[] quickSlots;
        /// <summary>Slots of the open storage window; -1 = no storage open.</summary>
        public short storageSize;
        /// <summary>Display name of the open storage.</summary>
        public string storageName;
        /// <summary>The occupied slots, ordered by area then index.</summary>
        public InventoryStackData[] stacks;

        /// <summary>Same layout, contents, selection, quickslots and storage as <paramref name="other"/> (the ack is not compared).</summary>
        public bool SameState(InventorySnapshot other)
        {
            if (other == null)
                return false;
            if (hotbarCount != other.hotbarCount || bagCount != other.bagCount || grid != other.grid
                || gridColumns != other.gridColumns || gridRows != other.gridRows || selectedHotbar != other.selectedHotbar
                || storageSize != other.storageSize || !string.Equals(storageName ?? "", other.storageName ?? "", StringComparison.Ordinal))
                return false;
            if (!InventoryNetRules.SameStrings(quickSlots, other.quickSlots))
                return false;
            int n = stacks != null ? stacks.Length : 0;
            int m = other.stacks != null ? other.stacks.Length : 0;
            if (n != m)
                return false;
            for (int i = 0; i < n; i++)
                if (!stacks[i].Equals(other.stacks[i]))
                    return false;
            return true;
        }

        /// <summary>The stacks sorted by area then index (the order <see cref="SameState"/> expects).</summary>
        public void SortStacks()
        {
            if (stacks != null && stacks.Length > 1)
                Array.Sort(stacks, CompareSlots);
        }

        /// <summary>Orders stacks by area, then index.</summary>
        public static int CompareSlots(InventoryStackData a, InventoryStackData b)
        {
            int c = a.area.CompareTo(b.area);
            return c != 0 ? c : a.index.CompareTo(b.index);
        }
    }

    /// <summary>
    /// What other players see of an inventory: the item in hand (and the hotbar slot it is in), the item of every
    /// equipment slot (armor, shield, rings...) and whether the weapons are drawn. Replicated to everyone who sees the
    /// player; their copies show it with the normal equipment visuals.
    /// </summary>
    [Serializable]
    public struct InventoryAppearance : IEquatable<InventoryAppearance>
    {
        /// <summary>Item id of the selected hotbar item ("" = empty hand).</summary>
        public string mainHand;
        /// <summary>The selected hotbar slot.</summary>
        public byte mainHandSlot;
        /// <summary>Item id per equipment slot index ("" = empty); may be null.</summary>
        public string[] equipment;
        /// <summary>The weapons are drawn (in hand) rather than sheathed.</summary>
        public bool drawn;

        public bool Equals(InventoryAppearance other) =>
            string.Equals(mainHand ?? "", other.mainHand ?? "", StringComparison.Ordinal)
            && mainHandSlot == other.mainHandSlot
            && drawn == other.drawn
            && InventoryNetRules.SameStrings(equipment, other.equipment);

        public override bool Equals(object obj) => obj is InventoryAppearance a && Equals(a);

        public override int GetHashCode() => (mainHand != null ? mainHand.GetHashCode() : 0) ^ (mainHandSlot << 8) ^ (drawn ? 1 : 0);

        /// <summary>
        /// The appearance as stacks (one item each): the hand item in its hotbar slot and each equipment item in its slot,
        /// so a copy can show it through the same apply path as a full snapshot.
        /// </summary>
        public List<InventoryStackData> ToStacks(List<InventoryStackData> into = null)
        {
            into = into ?? new List<InventoryStackData>();
            into.Clear();
            if (!string.IsNullOrEmpty(mainHand))
                into.Add(new InventoryStackData { area = (byte)InventoryArea.Hotbar, index = mainHandSlot, itemId = mainHand, count = 1, loadedRounds = -1 });
            if (equipment != null)
                for (int i = 0; i < equipment.Length && i < InventoryNetRules.MaxSlotsPerArea; i++)
                    if (!string.IsNullOrEmpty(equipment[i]))
                        into.Add(new InventoryStackData { area = (byte)InventoryArea.Equipment, index = (short)i, itemId = equipment[i], count = 1, loadedRounds = -1 });
            return into;
        }
    }

    /// <summary>What a client asks the server to do with its inventory (see <see cref="InventoryRequest"/>).</summary>
    public enum InventoryRequestKind : byte
    {
        /// <summary>Drag and drop: move, swap, merge, onto a quickslot, or into the world.</summary>
        Move = 1,
        /// <summary>Split <see cref="InventoryRequest.amount"/> items off a stack into a free slot.</summary>
        Split = 2,
        /// <summary>Turn a grid item in place.</summary>
        Rotate = 3,
        /// <summary>Select hotbar slot <see cref="InventoryRequest.amount"/> (the item in hand).</summary>
        SelectHotbar = 4,
        /// <summary>Use the item of the selected hotbar slot (not weapons: their attacks go through the weapon layer).</summary>
        UseSelected = 5,
        /// <summary>Use one item of a kind from the inventory without holding it (a consumable from a quickslot).</summary>
        UseItem = 6,
        /// <summary>Put an item in quickslot <see cref="InventoryRequest.amount"/> (empty id = clear it).</summary>
        AssignQuickSlot = 7,
        /// <summary>Pick up a world item.</summary>
        Pickup = 8,
        /// <summary>Started holding the interact key on a world item that takes time to pick up.</summary>
        PickupHold = 9,
        /// <summary>Open a storage (chest).</summary>
        OpenStorage = 10,
        /// <summary>Close the open storage.</summary>
        CloseStorage = 11,
        /// <summary>Put an armor piece of the inventory on (<see cref="InventoryRequest.itemId"/>).</summary>
        QuickEquipArmor = 12,
        /// <summary>Take every armor piece off.</summary>
        UnequipAllArmor = 13,
        /// <summary>Re-equip the most complete armor set.</summary>
        OptimizeArmor = 14,
        /// <summary>Weapons drawn (flag true) or sheathed.</summary>
        SetDrawn = 15,
        /// <summary>Send the whole inventory again.</summary>
        Resync = 16,
    }

    /// <summary>
    /// One inventory request from the owning client. Only what is needed to repeat the action on the server's copy:
    /// slots by reference, the item expected there (a stale request is refused), amounts and grid positions. Never
    /// amounts of anything the server decides (prices, stats, durability).
    /// </summary>
    [Serializable]
    public struct InventoryRequest
    {
        /// <summary>Increasing number of the request (the server echoes the last one handled in <see cref="InventorySnapshot.ack"/>).</summary>
        public uint seq;
        /// <summary>The <see cref="InventoryRequestKind"/>.</summary>
        public byte kind;
        /// <summary>The slot acted on (the dragged item's slot, the stack split or turned).</summary>
        public InventorySlotRef from;
        /// <summary>Where a dragged item was dropped (a slot; or the grid cell of its top-left corner).</summary>
        public InventorySlotRef to;
        /// <summary>The item expected in <see cref="from"/> (or the item to use / equip / assign).</summary>
        public string itemId;
        /// <summary>Split amount, hotbar slot, quickslot index.</summary>
        public int amount;
        /// <summary>Grid drop: the column of the item's top-left corner (may be outside the grid).</summary>
        public short gridX;
        /// <summary>Grid drop: the row of the item's top-left corner.</summary>
        public short gridY;
        /// <summary>The bag is a grid and the item lands at <see cref="gridX"/>, <see cref="gridY"/>.</summary>
        public bool toGrid;
        /// <summary>Grid drop: turned sideways.</summary>
        public bool rotated;
        /// <summary>Move: into the world. Use: include the hotbar. Open: true. Set drawn: drawn.</summary>
        public bool flag;
        /// <summary>Move: dropped on this quickslot (-1 = none).</summary>
        public sbyte quickIndex;
        /// <summary>Move into the world: where the client wants it (the server keeps it near the player).</summary>
        public Vector3 point;
        /// <summary>A world object without a network identity: its path in the player's scene (see <see cref="SceneObjectPath"/>).</summary>
        public string path;

        /// <summary>The kind as an enum.</summary>
        public InventoryRequestKind Kind => (InventoryRequestKind)kind;
    }

    /// <summary>
    /// The limits and checks the server applies to inventory requests, and small helpers shared by both sides. Pure: no
    /// scene needed.
    /// </summary>
    public static class InventoryNetRules
    {
        /// <summary>Most slots one area may have in a message.</summary>
        public const int MaxSlotsPerArea = 1024;
        /// <summary>Longest item id accepted from a client.</summary>
        public const int MaxItemIdLength = 64;
        /// <summary>Longest scene path accepted from a client.</summary>
        public const int MaxPathLength = 512;
        /// <summary>Most quickslots in a message.</summary>
        public const int MaxQuickSlots = 32;
        /// <summary>Most durability entries of one stack in a message.</summary>
        public const int MaxDurabilities = 4096;

        /// <summary>Is <paramref name="kind"/> a known <see cref="InventoryRequestKind"/>?</summary>
        public static bool IsKnownKind(byte kind) => kind >= (byte)InventoryRequestKind.Move && kind <= (byte)InventoryRequestKind.Resync;

        /// <summary>An item id a client may send: empty, or short enough.</summary>
        public static bool IsValidItemId(string id) => id == null || id.Length <= MaxItemIdLength;

        /// <summary>A scene path a client may send: empty, or short enough.</summary>
        public static bool IsValidPath(string path) => path == null || path.Length <= MaxPathLength;

        /// <summary>A finite number (no NaN, no infinity).</summary>
        public static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        /// <summary>A finite vector.</summary>
        public static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);

        /// <summary>
        /// The basic shape of a request: known kind, valid ids and path, slot references in range where the kind uses
        /// them. The server still checks everything against its own inventory afterwards.
        /// </summary>
        public static bool IsWellFormed(in InventoryRequest r)
        {
            if (!IsKnownKind(r.kind) || !IsValidItemId(r.itemId) || !IsValidPath(r.path))
                return false;
            switch (r.Kind)
            {
                case InventoryRequestKind.Move:
                    if (!r.from.IsValid)
                        return false;
                    if (r.flag)
                        return IsFinite(r.point);
                    if (r.quickIndex >= 0)
                        return r.quickIndex < MaxQuickSlots;
                    return r.toGrid ? Mathf.Abs(r.gridX) < MaxSlotsPerArea && Mathf.Abs(r.gridY) < MaxSlotsPerArea : r.to.IsValid;
                case InventoryRequestKind.Split:
                    return r.from.IsValid && r.amount >= 1;
                case InventoryRequestKind.Rotate:
                    return r.from.IsValid;
                case InventoryRequestKind.SelectHotbar:
                    return r.amount >= 0 && r.amount < MaxSlotsPerArea;
                case InventoryRequestKind.UseSelected:
                    return r.amount >= 0 && r.amount < MaxSlotsPerArea;
                case InventoryRequestKind.UseItem:
                case InventoryRequestKind.QuickEquipArmor:
                    return !string.IsNullOrEmpty(r.itemId);
                case InventoryRequestKind.AssignQuickSlot:
                    return r.amount >= 0 && r.amount < MaxQuickSlots;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Where an item dropped into the world lands: the client's point when it is finite and within
        /// <paramref name="maxDistance"/> of the player, else pulled back onto that distance (towards the point), else in
        /// front of the player.
        /// </summary>
        public static Vector3 ClampDropPoint(Vector3 wanted, Vector3 playerPosition, Vector3 playerForward, float maxDistance)
        {
            Vector3 fallback = playerPosition + Flat(playerForward) * 1.5f + Vector3.up;
            if (!IsFinite(wanted) || !IsFinite(playerPosition))
                return fallback;
            Vector3 offset = wanted - playerPosition;
            float d = offset.magnitude;
            if (d <= maxDistance)
                return wanted;
            if (d < 1e-4f || !IsFinite(d))
                return fallback;
            return playerPosition + offset * (maxDistance / d);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        /// <summary>
        /// Should a client apply a snapshot acknowledging <paramref name="snapshotAck"/> when its last request sent was
        /// <paramref name="lastSent"/>? Only when the server had handled every request sent: an older snapshot would
        /// briefly undo what the client already shows (a newer one is on its way). Wrap-around safe.
        /// </summary>
        public static bool ShouldApply(uint snapshotAck, uint lastSent) => (int)(snapshotAck - lastSent) >= 0;

        /// <summary>Same ints in the same order (null and empty are the same).</summary>
        public static bool SameInts(IList<int> a, IList<int> b)
        {
            int n = a != null ? a.Count : 0, m = b != null ? b.Count : 0;
            if (n != m)
                return false;
            for (int i = 0; i < n; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        /// <summary>Same strings in the same order (null and "" are the same, and so are a null and an empty array).</summary>
        public static bool SameStrings(IList<string> a, IList<string> b)
        {
            int n = a != null ? a.Count : 0, m = b != null ? b.Count : 0;
            int len = Mathf.Max(n, m);
            for (int i = 0; i < len; i++)
            {
                string x = i < n ? a[i] ?? "" : "";
                string y = i < m ? b[i] ?? "" : "";
                if (!string.Equals(x, y, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// When the server sends an inventory to its owner: right after a change (no more often than
    /// <see cref="MinInterval"/>, so a burst of changes is one message), and on a slow poll that catches changes made
    /// without an inventory event (a weapon losing durability, rounds loaded). Pure timing logic.
    /// </summary>
    public struct InventorySendSchedule
    {
        /// <summary>What to do now.</summary>
        public enum Decision
        {
            /// <summary>Nothing.</summary>
            Wait,
            /// <summary>Something changed: send now.</summary>
            Send,
            /// <summary>Build the snapshot and send it only when it differs from the last one sent.</summary>
            Poll,
        }

        /// <summary>Shortest time between two sends (seconds).</summary>
        public float MinInterval;
        /// <summary>Time between two polls (seconds).</summary>
        public float PollInterval;
        private bool dirty;
        private bool started;
        private float lastSent;
        private float nextPoll;

        public InventorySendSchedule(float minInterval, float pollInterval)
        {
            MinInterval = Mathf.Max(0f, minInterval);
            PollInterval = Mathf.Max(0.05f, pollInterval);
            dirty = true; // the first snapshot goes out as soon as possible
            started = false;
            lastSent = 0f;
            nextPoll = 0f;
        }

        /// <summary>Something changed: send at the next allowed moment.</summary>
        public void MarkDirty() => dirty = true;

        /// <summary>A change is waiting to be sent.</summary>
        public bool IsDirty => dirty;

        /// <summary>What to do at time <paramref name="now"/>.</summary>
        public Decision Evaluate(float now)
        {
            if (dirty && (!started || now - lastSent >= MinInterval))
                return Decision.Send;
            if (started && !dirty && now >= nextPoll)
                return Decision.Poll;
            return Decision.Wait;
        }

        /// <summary>A snapshot was sent at <paramref name="now"/>.</summary>
        public void Sent(float now)
        {
            started = true;
            dirty = false;
            lastSent = now;
            nextPoll = now + PollInterval;
        }

        /// <summary>A poll found nothing new at <paramref name="now"/>.</summary>
        public void Polled(float now) => nextPoll = now + PollInterval;
    }

    /// <summary>
    /// How a client turns what its inventory shows into what a snapshot says, touching as little as possible: a stack
    /// still in its slot is kept (only its numbers change), a stack that moved is moved (the same object, so an
    /// equipped item is not unequipped and equipped again), the rest is removed or created. Pure: works on stack data.
    /// </summary>
    public static class InventorySnapshotDiff
    {
        /// <summary>What to do with one stack.</summary>
        public enum StepKind
        {
            /// <summary>Same slot, same content: nothing to do.</summary>
            Keep,
            /// <summary>Same slot and item, new numbers (count, uses, rounds, turn).</summary>
            Update,
            /// <summary>The current stack moves to the target's slot (and takes its numbers).</summary>
            Move,
            /// <summary>The current stack is not in the snapshot: remove it.</summary>
            Remove,
            /// <summary>A new stack for the target.</summary>
            Create,
        }

        /// <summary>One step: indices into the current and target lists (-1 = not used).</summary>
        public struct Step
        {
            public StepKind kind;
            public int current;
            public int target;

            public override string ToString() => $"{kind} {current}->{target}";
        }

        /// <summary>
        /// The steps that turn <paramref name="current"/> into <paramref name="target"/>: removals first, then moves,
        /// updates, kept stacks and creations. Each current and each target stack appears in exactly one step.
        /// </summary>
        public static List<Step> Plan(IList<InventoryStackData> current, IList<InventoryStackData> target, List<Step> into = null)
        {
            into = into ?? new List<Step>();
            into.Clear();
            int n = current != null ? current.Count : 0;
            int m = target != null ? target.Count : 0;
            var currentUsed = new bool[n];
            var targetUsed = new bool[m];
            var bySlot = new Dictionary<InventorySlotRef, int>(n);
            for (int i = 0; i < n; i++)
            {
                InventorySlotRef key = current[i].Slot;
                if (!bySlot.ContainsKey(key))
                    bySlot.Add(key, i);
            }

            var keeps = new List<Step>();
            var moves = new List<Step>();
            // 1) Same slot, same item.
            for (int t = 0; t < m; t++)
            {
                if (!bySlot.TryGetValue(target[t].Slot, out int c) || currentUsed[c])
                    continue;
                if (!SameItem(current[c], target[t]))
                    continue;
                currentUsed[c] = targetUsed[t] = true;
                keeps.Add(new Step { kind = current[c].SameContent(target[t]) ? StepKind.Keep : StepKind.Update, current = c, target = t });
            }
            // 2) The same item elsewhere: move it (an identical stack first).
            for (int t = 0; t < m; t++)
            {
                if (targetUsed[t])
                    continue;
                int best = -1;
                for (int c = 0; c < n; c++)
                {
                    if (currentUsed[c] || !SameItem(current[c], target[t]))
                        continue;
                    if (best < 0)
                        best = c;
                    if (current[c].SameContent(target[t]))
                    {
                        best = c;
                        break;
                    }
                }
                if (best < 0)
                    continue;
                currentUsed[best] = targetUsed[t] = true;
                moves.Add(new Step { kind = StepKind.Move, current = best, target = t });
            }
            // 3) Everything left: removed or created.
            for (int c = 0; c < n; c++)
                if (!currentUsed[c])
                    into.Add(new Step { kind = StepKind.Remove, current = c, target = -1 });
            into.AddRange(moves);
            into.AddRange(keeps);
            for (int t = 0; t < m; t++)
                if (!targetUsed[t])
                    into.Add(new Step { kind = StepKind.Create, current = -1, target = t });
            return into;
        }

        private static bool SameItem(InventoryStackData a, InventoryStackData b) =>
            !string.IsNullOrEmpty(a.itemId) && string.Equals(a.itemId, b.itemId, StringComparison.Ordinal);
    }
}
