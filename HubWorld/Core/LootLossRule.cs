using System;
using System.Collections.Generic;

namespace HubWorld
{
    /// <summary>One stack of items a player carries, as the loot-loss rule sees it.</summary>
    public struct LootStack
    {
        /// <summary>Caller's id for the stack (index into its own list).</summary>
        public int id;
        public int quantity;
        /// <summary>Never lost (quest items, soul-bound items, equipped gear...).</summary>
        public bool isProtected;

        public LootStack(int id, int quantity, bool isProtected = false)
        {
            this.id = id;
            this.quantity = quantity;
            this.isProtected = isProtected;
        }
    }

    /// <summary>
    /// How much of what a player carries is lost between dimensions. Every unprotected stack loses
    /// <c>fraction × quantity</c> items on average: the whole part for sure, the remainder by a roll - so single items
    /// (a sword) are lost with that chance and large stacks lose almost exactly that share. Pure and deterministic for a
    /// given random generator, so the server decides and the client only shows the result.
    /// </summary>
    public static class LootLossRule
    {
        /// <summary>
        /// How many items each stack loses (same order as <paramref name="stacks"/>). <paramref name="fraction"/> is
        /// clamped to 0..1.
        /// </summary>
        public static int[] Plan(IReadOnlyList<LootStack> stacks, float fraction, Random random)
        {
            if (stacks == null)
                return Array.Empty<int>();
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            fraction = Math.Max(0f, Math.Min(1f, fraction));
            var lost = new int[stacks.Count];
            if (fraction <= 0f)
                return lost;
            for (int i = 0; i < stacks.Count; i++)
            {
                LootStack s = stacks[i];
                if (s.isProtected || s.quantity <= 0)
                    continue;
                double expected = s.quantity * (double)fraction;
                int whole = (int)Math.Floor(expected);
                double rest = expected - whole;
                if (rest > 1e-9 && random.NextDouble() < rest)
                    whole++;
                lost[i] = Math.Min(s.quantity, whole);
            }
            return lost;
        }

        /// <summary>Total items lost in a plan.</summary>
        public static int Total(int[] plan)
        {
            int n = 0;
            if (plan != null)
                foreach (int x in plan)
                    n += x;
            return n;
        }

        /// <summary>Currency lost: <paramref name="fraction"/> of <paramref name="amount"/>, rounded down.</summary>
        public static long CurrencyLoss(long amount, float fraction)
        {
            if (amount <= 0)
                return 0;
            fraction = Math.Max(0f, Math.Min(1f, fraction));
            return (long)Math.Floor(amount * (double)fraction);
        }
    }
}
