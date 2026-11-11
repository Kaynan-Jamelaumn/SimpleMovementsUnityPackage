using System.Collections.Generic;

namespace RuneMagic
{
    /// <summary>
    /// Which spell is on which ability key slot (design spec 16.4: "assigning, unassigning and swapping spells in slots works
    /// like assigning abilities"). A slot holds at most one spell and a spell sits in at most one slot. Assigning a spell
    /// to a slot that holds another one swaps them when the spell was already in a slot, and otherwise takes the slot (the
    /// displaced spell goes back to the spellbook). Pure functions over <see cref="RuneSoulState"/>.
    /// </summary>
    public static class SpellSlotRules
    {
        public static SpellSlotAssignment At(RuneSoulState s, int slot)
        {
            if (s?.spellSlots == null) return null;
            foreach (SpellSlotAssignment a in s.spellSlots)
                if (a != null && a.slot == slot) return a;
            return null;
        }

        public static SpellSlotAssignment Of(RuneSoulState s, string spellId)
        {
            if (s?.spellSlots == null || string.IsNullOrEmpty(spellId)) return null;
            foreach (SpellSlotAssignment a in s.spellSlots)
                if (a != null && a.spellId == spellId) return a;
            return null;
        }

        /// <summary>The slot a spell is in (-1 when it is only in the spellbook).</summary>
        public static int SlotOf(RuneSoulState s, string spellId) => Of(s, spellId)?.slot ?? -1;

        /// <summary>
        /// Puts the spell <paramref name="spellId"/> in slot <paramref name="slot"/>. <paramref name="touched"/> lists every
        /// slot whose content changed (to be applied to the player's abilities). False (with the reason) when there is no
        /// such spell or slot.
        /// </summary>
        public static bool Assign(RuneSoulState s, string spellId, int slot, List<int> touched, out string why)
        {
            why = "";
            if (SpellRules.Find(s, spellId) == null) { why = "That spell is no longer in your spellbook."; return false; }
            if (slot < 0) { why = "That is not an ability slot."; return false; }
            int from = SlotOf(s, spellId);
            if (from == slot) return true;
            SpellSlotAssignment occupant = At(s, slot);
            SpellSlotAssignment mine = Of(s, spellId);
            if (occupant != null)
            {
                if (from >= 0) occupant.slot = from; // a swap: the other spell takes the slot this one leaves
                else s.spellSlots.Remove(occupant);   // the displaced spell goes back to the spellbook
                touched?.Add(occupant.slot);
            }
            if (mine != null) mine.slot = slot;
            else s.spellSlots.Add(new SpellSlotAssignment(slot, spellId));
            touched?.Add(slot);
            if (from >= 0 && touched != null && !touched.Contains(from)) touched.Add(from);
            return true;
        }

        /// <summary>Takes the spell out of its slot (it stays in the spellbook). False when the slot holds none.</summary>
        public static bool Unassign(RuneSoulState s, int slot)
        {
            SpellSlotAssignment a = At(s, slot);
            if (a == null) return false;
            s.spellSlots.Remove(a);
            return true;
        }

        /// <summary>Exchanges what two slots hold (either may be empty). False when both are empty.</summary>
        public static bool Swap(RuneSoulState s, int slotA, int slotB)
        {
            SpellSlotAssignment a = At(s, slotA), b = At(s, slotB);
            if (a == null && b == null) return false;
            if (a != null) a.slot = slotB;
            if (b != null) b.slot = slotA;
            return true;
        }

        /// <summary>Drops assignments of spells that are gone, of slots below 0, and duplicates. Returns the slots that were emptied.</summary>
        public static List<int> Normalize(RuneSoulState s)
        {
            var emptied = new List<int>();
            if (s?.spellSlots == null) return emptied;
            var slots = new HashSet<int>();
            var spells = new HashSet<string>();
            for (int i = 0; i < s.spellSlots.Count; i++)
            {
                SpellSlotAssignment a = s.spellSlots[i];
                bool bad = a == null || a.slot < 0 || string.IsNullOrEmpty(a.spellId) || SpellRules.Find(s, a.spellId) == null ||
                           !slots.Add(a.slot) || !spells.Add(a.spellId);
                if (!bad) continue;
                if (a != null && a.slot >= 0) emptied.Add(a.slot);
                s.spellSlots.RemoveAt(i--);
            }
            return emptied;
        }
    }
}
