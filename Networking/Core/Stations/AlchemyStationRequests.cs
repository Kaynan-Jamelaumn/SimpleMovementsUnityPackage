using System;

namespace GameNet
{
    /// <summary>An <see cref="AlchemyRequest"/> as it travels from a client to the server (the item by its id).</summary>
    [Serializable]
    public struct AlchemyNetRequest
    {
        public byte kind;
        public string itemId, form, id;
        public int count;
        public float value, evenness;

        public static AlchemyNetRequest From(in AlchemyRequest r, string itemId) => new AlchemyNetRequest
        {
            kind = (byte)r.kind,
            itemId = itemId ?? r.itemId ?? "",
            form = r.form ?? "",
            id = r.id ?? "",
            count = r.count,
            value = r.value,
            evenness = r.evenness,
        };

        public AlchemyRequest ToRequest() => new AlchemyRequest((AlchemyRequestKind)kind, value, count, null, string.IsNullOrEmpty(form) ? null : form, evenness)
        {
            itemId = string.IsNullOrEmpty(itemId) ? null : itemId,
            id = string.IsNullOrEmpty(id) ? null : id,
        };
    }

    /// <summary>
    /// What a server lets a remote player's alchemy requests claim: their shape (known kind, short ids, finite numbers
    /// in range), how often (stirring, the bellows, adding items) and how fast ingredients can be ground in the mortar
    /// (a client cannot tip in a powder it never ground).
    /// </summary>
    public sealed class AlchemyRequestGuard
    {
        public const int MaxIdLength = 64;
        public const int MaxCount = 50;

        private CraftRequestGuard.Allowance requests = new CraftRequestGuard.Allowance(20, 30);
        private CraftRequestGuard.Allowance stirs = new CraftRequestGuard.Allowance(6, 4);
        private CraftRequestGuard.Allowance pumps = new CraftRequestGuard.Allowance(5, 4);
        private CraftRequestGuard.Allowance grinding = new CraftRequestGuard.Allowance(0.6, 5);
        private CraftRequestGuard.Allowance filters = new CraftRequestGuard.Allowance(0.5, 1);

        /// <summary>Checks and bounds <paramref name="r"/>; false (with a reason, empty when not worth telling) = refused.</summary>
        public bool Check(ref AlchemyRequest r, double now, out string reason)
        {
            reason = "";
            if (!Enum.IsDefined(typeof(AlchemyRequestKind), r.kind) || TooLong(r.itemId) || TooLong(r.form) || TooLong(r.id)
                || !Finite(r.value) || !Finite(r.evenness) || r.count < 0)
                return false;
            if (!requests.TakeWhole(1, now))
            {
                reason = "Slow down.";
                return false;
            }
            r.evenness = Math.Max(0f, Math.Min(1f, r.evenness));
            switch (r.kind)
            {
                case AlchemyRequestKind.AddItem:
                    r.count = Math.Max(1, Math.Min(MaxCount, r.count));
                    if (!string.IsNullOrEmpty(r.form))
                    {
                        int n = (int)Math.Floor(grinding.Take(r.count, now) + 1e-9);
                        if (n < 1)
                        {
                            reason = "Grinding takes time.";
                            return false;
                        }
                        r.count = n;
                    }
                    return true;
                case AlchemyRequestKind.AddWater:
                    r.value = Math.Max(0f, Math.Min(100f, r.value));
                    return true;
                case AlchemyRequestKind.AddFuel:
                    r.count = Math.Max(1, Math.Min(20, r.count));
                    return true;
                case AlchemyRequestKind.Stir:
                    r.value = Math.Max(0f, Math.Min(1f, r.value));
                    return stirs.TakeWhole(1, now);
                case AlchemyRequestKind.Bellows:
                    r.value = Math.Max(0f, Math.Min(1f, r.value));
                    return pumps.TakeWhole(1, now);
                case AlchemyRequestKind.SetHeat:
                case AlchemyRequestKind.SetDamper:
                case AlchemyRequestKind.SetStirring:
                    r.value = Math.Max(0f, Math.Min(1f, r.value));
                    return true;
                case AlchemyRequestKind.SetBath:
                    r.value = Math.Max(0f, Math.Min(2f, (float)Math.Round(r.value)));
                    return true;
                case AlchemyRequestKind.Filter:
                    r.value = Math.Max(0f, Math.Min(1f, r.value));
                    return filters.TakeWhole(1, now);
                case AlchemyRequestKind.Bottle:
                    r.count = Math.Max(0, Math.Min(1000, r.count));
                    return true;
                default:
                    return true;
            }
        }

        private static bool TooLong(string s) => s != null && s.Length > MaxIdLength;
        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
