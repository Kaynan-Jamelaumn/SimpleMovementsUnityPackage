using System;

namespace RuneMagic
{
    /// <summary>Why energy could not be given to a rune.</summary>
    public enum EnergyRefusal
    {
        None,
        NoSuchRune,
        /// <summary>The rune is full: choose another rune (design spec 5.4).</summary>
        Full,
        NothingPending,
    }

    /// <summary>What giving pending energy to a rune did.</summary>
    public struct EnergyTransfer
    {
        public float moved;
        public float pendingLeft;
        public float runeEnergy;
        public float runeMax;
        public EnergyRefusal refusal;

        public bool Ok => refusal == EnergyRefusal.None && moved > 0f;

        /// <summary>A sentence for the player.</summary>
        public string Describe(string runeName)
        {
            switch (refusal)
            {
                case EnergyRefusal.NoSuchRune: return "That rune is no longer in your soul.";
                case EnergyRefusal.Full: return $"{runeName} is full ({runeEnergy:N0} / {runeMax:N0}): choose another rune.";
                case EnergyRefusal.NothingPending: return "You have no magical energy to give.";
            }
            return $"{runeName} +{moved:N0} energy ({runeEnergy:N0} / {runeMax:N0})." + (pendingLeft > 0f ? $" {pendingLeft:N0} still waiting." : "");
        }
    }

    /// <summary>
    /// Rune Energy (design spec 5): each rune's reserve, independent of its rank, quality and concept knowledge. Energy from
    /// kills waits in a pending pool until the player gives it to the rune they choose; only that rune receives it. A full
    /// rune takes no more (choose another one), and nothing ever turns into a rank advancement or a higher-rank rune
    /// (design spec 5.2, 5.4).
    /// </summary>
    public static class EnergyRules
    {
        /// <summary>Most energy <paramref name="r"/> can store (from its rank; its quality only when that coupling is on).</summary>
        public static float MaxEnergy(RuneBalance b, RuneInstance r) => b == null || r == null ? 0f : b.MaxEnergy(r.rank, r.quality);

        /// <summary>Energy <paramref name="r"/> can still take.</summary>
        public static float Space(RuneBalance b, RuneInstance r) => r == null ? 0f : Math.Max(0f, MaxEnergy(b, r) - r.energy);

        /// <summary>Is <paramref name="r"/> full?</summary>
        public static bool IsFull(RuneBalance b, RuneInstance r) => Space(b, r) < 0.001f;

        /// <summary>
        /// Adds magical energy from a kill to the pending pool (up to Max Pending Energy when set; more is lost). Returns what
        /// was kept.
        /// </summary>
        public static float AddPending(RuneBalance b, RuneSoulState s, float amount)
        {
            if (s == null || !(amount > 0f) || float.IsInfinity(amount)) return 0f;
            float cap = b?.acquisition != null ? b.acquisition.maxPendingEnergy : 0f;
            float was = s.pendingEnergy;
            s.pendingEnergy = cap > 0f ? Math.Min(cap, was + amount) : was + amount;
            float kept = Math.Max(0f, s.pendingEnergy - was);
            s.lifetimeEnergyGained += kept;
            return kept;
        }

        /// <summary>
        /// Gives up to <paramref name="amount"/> pending energy (0 or less = all of it) to the rune <paramref name="instanceId"/>:
        /// it receives what fits; the rest stays pending. A full rune receives nothing.
        /// </summary>
        public static EnergyTransfer Assign(RuneBalance b, RuneSoulState s, string instanceId, float amount)
        {
            var t = new EnergyTransfer { pendingLeft = s != null ? s.pendingEnergy : 0f };
            RuneInstance r = s?.Rune(instanceId);
            if (r == null) { t.refusal = EnergyRefusal.NoSuchRune; return t; }
            t.runeEnergy = r.energy;
            t.runeMax = MaxEnergy(b, r);
            if (s.pendingEnergy <= 0f) { t.refusal = EnergyRefusal.NothingPending; return t; }
            float space = Space(b, r);
            if (space < 0.001f) { t.refusal = EnergyRefusal.Full; return t; }
            float want = float.IsNaN(amount) || amount <= 0f || float.IsInfinity(amount) ? s.pendingEnergy : Math.Min(amount, s.pendingEnergy);
            float moved = Math.Min(want, space);
            r.energy += moved;
            s.pendingEnergy = Math.Max(0f, s.pendingEnergy - moved);
            s.lifetimeEnergyAssigned += moved;
            t.moved = moved;
            t.pendingLeft = s.pendingEnergy;
            t.runeEnergy = r.energy;
            return t;
        }

        /// <summary>Takes up to <paramref name="amount"/> energy out of <paramref name="r"/> (synthesis, casting). Returns what was taken.</summary>
        public static float Consume(RuneInstance r, float amount)
        {
            if (r == null || !(amount > 0f)) return 0f;
            float taken = Math.Min(amount, r.energy);
            r.energy -= taken;
            return taken;
        }

        /// <summary>Keeps every rune's energy within its maximum (after the balance tables changed).</summary>
        public static void ClampAll(RuneBalance b, RuneSoulState s)
        {
            if (s?.runes == null) return;
            foreach (RuneInstance r in s.runes)
                if (r != null) r.energy = Math.Max(0f, Math.Min(r.energy, MaxEnergy(b, r)));
        }
    }
}
