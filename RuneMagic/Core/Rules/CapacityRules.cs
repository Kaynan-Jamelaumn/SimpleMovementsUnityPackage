using System;

namespace RuneMagic
{
    /// <summary>
    /// Soul Rune Capacity (design spec 4): how many soul slots a character has (its progression stage plus bonuses) and
    /// whether runes fit. The slots used are the slot costs of the runes held.
    /// </summary>
    public static class CapacityRules
    {
        /// <summary>Sum of the lasting capacity bonuses.</summary>
        public static int LastingBonus(SoulState s)
        {
            int sum = 0;
            if (s?.capacityBonuses == null) return 0;
            foreach (CapacityBonus x in s.capacityBonuses)
                if (x != null) sum += x.slots;
            return sum;
        }

        /// <summary>The capacity of a character of <paramref name="level"/>: its stage, lasting bonuses and <paramref name="runtimeBonus"/>.</summary>
        public static int Capacity(RuneBalance b, int level, SoulState s, int runtimeBonus = 0)
        {
            if (b == null) return 0;
            return b.CapacityAt(Math.Max(1, level), LastingBonus(s) + runtimeBonus);
        }

        /// <summary>Free slots (never below 0, even when more is held than fits after a bonus ended).</summary>
        public static int Free(int capacity, int used) => Math.Max(0, capacity - used);

        /// <summary>Can runes costing <paramref name="cost"/> slots join <paramref name="used"/> slots?</summary>
        public static bool CanFit(int capacity, int used, int cost) => cost <= 0 || used + cost <= capacity;

        /// <summary>More is held than fits (a bonus ended): nothing more can be added until slots are freed.</summary>
        public static bool Overfull(int capacity, int used) => used > capacity;

        /// <summary>The next capacity stage above <paramref name="level"/> (null at the last one).</summary>
        public static CapacityStage NextStage(RuneBalance b, int level)
        {
            CapacityStage next = null;
            if (b?.capacityStages == null) return null;
            foreach (CapacityStage s in b.capacityStages)
                if (s != null && s.fromLevel > level && (next == null || s.fromLevel < next.fromLevel)) next = s;
            return next;
        }

        /// <summary>Sets the lasting capacity bonus of <paramref name="source"/> (0 removes it). Returns true when it changed.</summary>
        public static bool SetBonus(SoulState s, string source, int slots)
        {
            if (s == null || string.IsNullOrEmpty(source)) return false;
            s.EnsureLists();
            int i = s.capacityBonuses.FindIndex(x => x != null && x.source == source);
            if (slots == 0)
            {
                if (i < 0) return false;
                s.capacityBonuses.RemoveAt(i);
                return true;
            }
            if (i >= 0)
            {
                if (s.capacityBonuses[i].slots == slots) return false;
                s.capacityBonuses[i].slots = slots;
                return true;
            }
            s.capacityBonuses.Add(new CapacityBonus { source = source, slots = slots });
            return true;
        }
    }
}
