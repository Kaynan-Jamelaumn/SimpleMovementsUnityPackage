using System;

namespace RuneMagic
{
    /// <summary>What the owner of a rune user asks the server to do (one byte on the wire; values are only ever added).</summary>
    public enum RuneRequestKind : byte
    {
        None = 0,
        /// <summary>Start meditating (flag on), or stop meditating or the ritual being performed (flag off).</summary>
        Meditate = 1,
        /// <summary>Absorb a rune item of the inventory (text: the item's id).</summary>
        Absorb = 2,
        /// <summary>Release an absorbed rune (text: its instance id). The UI confirms first.</summary>
        Release = 3,
        /// <summary>Give pending energy to a rune (text: its instance id; amount: how much, 0 = all).</summary>
        AssignEnergy = 4,
        /// <summary>The minigame played (text: the seconds each glyph was sealed at, separated by ';').</summary>
        DiscoveryMinigame = 5,
        /// <summary>The riddle's answer (amount: the index of the option chosen).</summary>
        DiscoveryAnswer = 6,
        /// <summary>The player leaves the discovery.</summary>
        DiscoveryAbandon = 7,
        /// <summary>Dismiss a spell: its runes are free again (text: the spell's id).</summary>
        DismissSpell = 8,
        /// <summary>
        /// Perform a synthesis (text: the request as JSON, see <c>SynthesisRequestCodec</c>; longer than other requests, up to
        /// <see cref="RuneNetLimits.MaxSynthesisRequestText"/>). The server checks everything and chooses the chance itself.
        /// </summary>
        Synthesize = 9,
        /// <summary>Put a spell on an ability key (text: the spell's id; amount: the key's slot index).</summary>
        AssignSpell = 10,
        /// <summary>Take the spell off an ability key (amount: the key's slot index).</summary>
        UnassignSpell = 11,
        /// <summary>
        /// Start the key sequence of the spell on a key (amount: the key's slot index; text: the keys the player's machine can use, as
        /// names separated by commas, left out of it the keys its bindings use). An empty text asks to skip it (no keyboard).
        /// </summary>
        SpellingStart = 12,
        /// <summary>The key presses of the sequence (text: see <c>SpellingResultCodec</c>).</summary>
        SpellingResult = 13,
        /// <summary>Open a grimoire the player holds (text: the grimoire item's id). The server sends the book to the owner.</summary>
        GrimoireOpen = 14,
        /// <summary>Imprint a rune of the soul on a page of a grimoire (text: item id, '|', the rune's instance id; amount: the page).</summary>
        GrimoireImprint = 15,
        /// <summary>Absorb the print of a page of a grimoire the player holds (text: the item's id; amount: the page).</summary>
        GrimoireAbsorb = 16,
        /// <summary>Erase the print of a page of the author's own grimoire (text: the item's id; amount: the page).</summary>
        GrimoireErase = 17,
    }

    /// <summary>A request from the owning client to the server about its soul. The server checks everything again.</summary>
    public struct RuneSoulRequest
    {
        public RuneRequestKind kind;
        /// <summary>Counts the owner's requests (answers and logs name it).</summary>
        public uint seq;
        public bool flag;
        /// <summary>An item id or a rune instance id.</summary>
        public string text;
        public float amount;
    }

    /// <summary>
    /// The network side of a player's Rune Magic, installed on the owning client by the network layer (NetRuneMagic). Offline,
    /// on the host and on the server there is none: the rules run directly.
    /// </summary>
    public interface IRuneMagicNet
    {
        /// <summary>Sends <paramref name="request"/> to the server. False when it could not be sent.</summary>
        bool Send(RuneSoulRequest request);
    }

    /// <summary>
    /// A token bucket per player, where requests arrive (the server): at most <see cref="Burst"/> at once, refilled at
    /// <see cref="PerSecond"/>. Requests above it are dropped: an owner cannot flood the server.
    /// </summary>
    public sealed class RuneRequestGuard
    {
        public float PerSecond { get; }
        public int Burst { get; }

        private float tokens;
        private float last = float.NaN;

        public RuneRequestGuard(float perSecond = 4f, int burst = 8)
        {
            PerSecond = Math.Max(0.01f, perSecond);
            Burst = Math.Max(1, burst);
            tokens = Burst;
        }

        /// <summary>May one more request be handled at <paramref name="now"/> (seconds, rising)? Uses a token when yes.</summary>
        public bool Allow(float now)
        {
            if (float.IsNaN(now) || float.IsInfinity(now))
                return false;
            if (!float.IsNaN(last) && now > last)
                tokens = Math.Min(Burst, tokens + (now - last) * PerSecond);
            if (float.IsNaN(last) || now > last)
                last = now;
            if (tokens < 1f)
                return false;
            tokens -= 1f;
            return true;
        }

        /// <summary>Full again (a new owner, tests).</summary>
        public void Reset()
        {
            tokens = Burst;
            last = float.NaN;
        }
    }

    /// <summary>Limits of what crosses the network for Rune Magic.</summary>
    public static class RuneNetLimits
    {
        /// <summary>Longest soul view (JSON characters) a client accepts.</summary>
        public const int MaxViewLength = 8192;
        /// <summary>Longest rune list (JSON characters) a client accepts.</summary>
        public const int MaxRuneListLength = 262144;
        /// <summary>Longest text (item or rune id) of a request the server accepts.</summary>
        public const int MaxRequestText = 160;
        /// <summary>The highest request kind this version understands.</summary>
        public const RuneRequestKind LastKind = RuneRequestKind.GrimoireErase;
        /// <summary>Longest synthesis request (JSON characters) the server accepts.</summary>
        public const int MaxSynthesisRequestText = 8192;
        /// <summary>Most runes (slots) a synthesis request may carry on the network (the balance's Max Runes must not exceed it).</summary>
        public const int MaxSynthesisSlots = 24;

        /// <summary>The longest text a request of <paramref name="kind"/> may carry.</summary>
        public static int MaxTextFor(RuneRequestKind kind) =>
            kind == RuneRequestKind.Synthesize ? MaxSynthesisRequestText
            : kind == RuneRequestKind.SpellingResult ? MaxSpellingResultText
            : kind == RuneRequestKind.SpellingStart ? MaxSpellingPoolText
            : MaxRequestText;
        /// <summary>Longest key sequence result (characters) the server accepts.</summary>
        public const int MaxSpellingResultText = 1024;
        /// <summary>Longest list of usable keys (characters) the server accepts with a spelling start.</summary>
        public const int MaxSpellingPoolText = 512;
        /// <summary>Longest spelling view (JSON characters) a client accepts.</summary>
        public const int MaxSpellingViewLength = 4096;
        /// <summary>Longest discovery view (JSON characters) a client accepts.</summary>
        public const int MaxDiscoveryViewLength = 16384;
        /// <summary>Longest energies update (JSON characters) a client accepts (the energy of every rune: 64 runes at most).</summary>
        public const int MaxRuneEnergiesLength = 16384;
        /// <summary>Longest grimoire view (JSON characters) a client accepts (at most 64 pages).</summary>
        public const int MaxGrimoireViewLength = 32768;
    }
}
