using System;

namespace GameNet
{
    /// <summary>
    /// What every player needs to know about an item lying in the world: which item (its <see cref="ItemDatabase"/> id),
    /// how many, the uses left of each unit, and whether it turns slowly in place. The server sets it; clients show it
    /// (the right model and label) and ask the server when they want to pick the item up.
    /// </summary>
    [Serializable]
    public struct WorldItemState : IEquatable<WorldItemState>
    {
        /// <summary>The item's <see cref="ItemDatabase"/> id ("" = none).</summary>
        public string itemId;
        /// <summary>How many items lie there.</summary>
        public int quantity;
        /// <summary>Uses left of each unit (may be null).</summary>
        public int[] durabilities;
        /// <summary>Turns slowly in place (off for an arrow stuck in a wall).</summary>
        public bool spin;

        public bool Equals(WorldItemState other) =>
            string.Equals(itemId ?? "", other.itemId ?? "", StringComparison.Ordinal)
            && quantity == other.quantity
            && spin == other.spin
            && InventoryNetRules.SameInts(durabilities, other.durabilities);

        public override bool Equals(object obj) => obj is WorldItemState s && Equals(s);

        public override int GetHashCode() => ((itemId != null ? itemId.GetHashCode() : 0) * 31 + quantity) * 2 + (spin ? 1 : 0);
    }
}
