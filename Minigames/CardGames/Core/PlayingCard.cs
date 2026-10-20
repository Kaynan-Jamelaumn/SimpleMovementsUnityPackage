using System;
using System.Collections.Generic;

// Cards and decks shared by every card game (Truco, and later poker or anything else): a standard 52-card French deck
// with no jokers. Pure data, no Unity objects, so games can be simulated on a server and tested in Edit Mode.

/// <summary>The four suits. Serialized as numbers: append only.</summary>
public enum CardSuit : byte
{
    Clubs,
    Diamonds,
    Hearts,
    Spades,
}

/// <summary>The thirteen ranks (Ace = 1 ... King = 13). Serialized as numbers: append only.</summary>
public enum CardRank : byte
{
    Ace = 1,
    Two,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King,
}

/// <summary>
/// One playing card. Its <see cref="Code"/> (0-51) is how it travels over the network and in snapshots;
/// <see cref="Hidden"/> (255) stands for a card whose face this viewer may not see.
/// </summary>
[Serializable]
public struct PlayingCard : IEquatable<PlayingCard>
{
    /// <summary>A card shown face down (another player's hand).</summary>
    public const byte Hidden = 255;
    /// <summary>No card.</summary>
    public const byte None = 254;

    public CardRank rank;
    public CardSuit suit;

    public PlayingCard(CardRank rank, CardSuit suit)
    {
        this.rank = rank;
        this.suit = suit;
    }

    /// <summary>0-51: suit × 13 + rank − 1.</summary>
    public byte Code => (byte)((int)suit * 13 + ((int)rank - 1));

    public static PlayingCard FromCode(byte code) => new PlayingCard((CardRank)(code % 13 + 1), (CardSuit)Math.Min(3, code / 13));

    public static bool IsCard(byte code) => code < 52;

    public bool IsRed => suit == CardSuit.Diamonds || suit == CardSuit.Hearts;

    /// <summary>"A", "7", "10", "Q".</summary>
    public string RankLabel => RankShort(rank);

    /// <summary>"Q♥".</summary>
    public string Short => RankLabel + SuitSymbol(suit);

    /// <summary>"Queen of Hearts".</summary>
    public string Name => $"{RankName(rank)} of {suit}";

    public override string ToString() => Short;

    public static string RankShort(CardRank r)
    {
        switch (r)
        {
            case CardRank.Ace: return "A";
            case CardRank.Jack: return "J";
            case CardRank.Queen: return "Q";
            case CardRank.King: return "K";
            default: return ((int)r).ToString();
        }
    }

    public static string RankName(CardRank r)
    {
        switch (r)
        {
            case CardRank.Ace: return "Ace";
            case CardRank.Jack: return "Jack";
            case CardRank.Queen: return "Queen";
            case CardRank.King: return "King";
            default: return ((int)r).ToString();
        }
    }

    /// <summary>♣ ♦ ♥ ♠.</summary>
    public static string SuitSymbol(CardSuit s)
    {
        switch (s)
        {
            case CardSuit.Clubs: return "♣";
            case CardSuit.Diamonds: return "♦";
            case CardSuit.Hearts: return "♥";
            default: return "♠";
        }
    }

    public bool Equals(PlayingCard other) => rank == other.rank && suit == other.suit;
    public override bool Equals(object obj) => obj is PlayingCard c && Equals(c);
    public override int GetHashCode() => Code;
    public static bool operator ==(PlayingCard a, PlayingCard b) => a.Equals(b);
    public static bool operator !=(PlayingCard a, PlayingCard b) => !a.Equals(b);
}

/// <summary>
/// A pile of cards: made (the standard 52), shuffled with a seeded random (the same seed deals the same game everywhere)
/// and dealt from the top.
/// </summary>
public sealed class CardDeck
{
    private readonly List<PlayingCard> cards;

    public CardDeck(IEnumerable<PlayingCard> cards)
    {
        this.cards = new List<PlayingCard>(cards);
    }

    /// <summary>A new 52-card deck (A-K of every suit, no jokers), in order.</summary>
    public static CardDeck Standard() => new CardDeck(Standard52());

    /// <summary>The 52 cards in order (clubs, diamonds, hearts, spades; ace to king).</summary>
    public static List<PlayingCard> Standard52()
    {
        var list = new List<PlayingCard>(52);
        for (int s = 0; s < 4; s++)
            for (int r = 1; r <= 13; r++)
                list.Add(new PlayingCard((CardRank)r, (CardSuit)s));
        return list;
    }

    public int Count => cards.Count;

    public IReadOnlyList<PlayingCard> Cards => cards;

    /// <summary>Fisher-Yates shuffle.</summary>
    public void Shuffle(Random rng)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            PlayingCard t = cards[i];
            cards[i] = cards[j];
            cards[j] = t;
        }
    }

    /// <summary>Takes the top card.</summary>
    public PlayingCard Draw()
    {
        if (cards.Count == 0)
            throw new InvalidOperationException("The deck is empty.");
        PlayingCard c = cards[cards.Count - 1];
        cards.RemoveAt(cards.Count - 1);
        return c;
    }

    /// <summary>The top card, without taking it.</summary>
    public PlayingCard Peek() => cards[cards.Count - 1];
}
