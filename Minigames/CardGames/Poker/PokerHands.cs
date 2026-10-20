using System;
using System.Collections.Generic;

/// <summary>
/// Poker hand values. <see cref="Best"/> finds the best five cards out of five to seven and returns a number where
/// bigger is better: the category (straight flush … high card) in the top bits, then the ranks that break ties
/// (kickers). Aces are high, and also low in the A-2-3-4-5 straight.
/// </summary>
public static class PokerHands
{
    public const int HighCard = 0, Pair = 1, TwoPair = 2, Trips = 3, Straight = 4, Flush = 5, FullHouse = 6, Quads = 7, StraightFlush = 8;

    private static readonly string[] Names =
    {
        "High Card", "Pair", "Two Pair", "Three of a Kind", "Straight", "Flush", "Full House", "Four of a Kind", "Straight Flush",
    };

    public static int Category(int score) => score >> 20;

    /// <summary>"Full House", "Pair"...</summary>
    public static string Name(int score)
    {
        int c = Category(score);
        if (c == StraightFlush && ((score >> 16) & 15) == 14) return "Royal Flush";
        return c >= 0 && c < Names.Length ? Names[c] : "?";
    }

    private static int RankValue(PlayingCard c) => c.rank == CardRank.Ace ? 14 : (int)c.rank;

    /// <summary>The value of exactly five cards.</summary>
    public static int Evaluate5(IReadOnlyList<PlayingCard> five)
    {
        var ranks = new int[5];
        bool flush = true;
        for (int i = 0; i < 5; i++)
        {
            ranks[i] = RankValue(five[i]);
            if (five[i].suit != five[0].suit) flush = false;
        }
        Array.Sort(ranks);
        Array.Reverse(ranks); // high to low

        bool straight = true;
        for (int i = 1; i < 5; i++)
            if (ranks[i] != ranks[i - 1] - 1) { straight = false; break; }
        int straightHigh = ranks[0];
        if (!straight && ranks[0] == 14 && ranks[1] == 5 && ranks[2] == 4 && ranks[3] == 3 && ranks[4] == 2)
        {
            straight = true;
            straightHigh = 5; // the wheel: the ace plays low
        }
        if (straight && flush) return Score(StraightFlush, straightHigh);

        // Group by rank: bigger groups first, then higher ranks.
        var groups = new List<(int rank, int count)>();
        for (int i = 0; i < 5;)
        {
            int j = i;
            while (j < 5 && ranks[j] == ranks[i]) j++;
            groups.Add((ranks[i], j - i));
            i = j;
        }
        groups.Sort((a, b) => a.count != b.count ? b.count.CompareTo(a.count) : b.rank.CompareTo(a.rank));
        var kick = new List<int>(5);
        foreach ((int rank, int count) in groups) kick.Add(rank);

        if (groups[0].count == 4) return Score(Quads, kick);
        if (groups[0].count == 3 && groups[1].count == 2) return Score(FullHouse, kick);
        if (flush) return Score(Flush, ranks);
        if (straight) return Score(Straight, straightHigh);
        if (groups[0].count == 3) return Score(Trips, kick);
        if (groups[0].count == 2 && groups[1].count == 2) return Score(TwoPair, kick);
        if (groups[0].count == 2) return Score(Pair, kick);
        return Score(HighCard, ranks);
    }

    private static int Score(int category, int high) => (category << 20) | (high << 16);

    private static int Score(int category, IReadOnlyList<int> kickers)
    {
        int s = category << 20;
        for (int i = 0; i < 5; i++)
            s |= (i < kickers.Count ? kickers[i] : 0) << (16 - i * 4);
        return s;
    }

    private static readonly PlayingCard[] five = new PlayingCard[5];

    /// <summary>The best value of any five of <paramref name="cards"/> (5 to 7 cards).</summary>
    public static int Best(IReadOnlyList<PlayingCard> cards)
    {
        int n = cards.Count;
        if (n < 5)
            return -1;
        int best = -1;
        for (int a = 0; a < n - 4; a++)
            for (int b = a + 1; b < n - 3; b++)
                for (int c = b + 1; c < n - 2; c++)
                    for (int d = c + 1; d < n - 1; d++)
                        for (int e = d + 1; e < n; e++)
                        {
                            five[0] = cards[a]; five[1] = cards[b]; five[2] = cards[c]; five[3] = cards[d]; five[4] = cards[e];
                            int s = Evaluate5(five);
                            if (s > best) best = s;
                        }
        return best;
    }

    /// <summary>
    /// 0-1: how good two hole cards are before the flop (pairs, high cards, suited and connected cards count).
    /// </summary>
    public static float Preflop(PlayingCard a, PlayingCard b)
    {
        int ra = RankValue(a), rb = RankValue(b);
        int hi = Math.Max(ra, rb), lo = Math.Min(ra, rb);
        float s = (hi + lo) / 28f * 0.55f;
        if (ra == rb) s += 0.3f + hi / 14f * 0.15f;
        if (a.suit == b.suit) s += 0.06f;
        int gap = hi - lo;
        if (gap == 1) s += 0.05f;
        else if (gap == 2) s += 0.02f;
        return Math.Min(1f, s);
    }
}
