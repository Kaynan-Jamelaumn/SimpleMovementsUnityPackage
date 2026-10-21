using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The configurable rules of Truco (on the <see cref="TrucoGameDefinition"/>): points to win, what a hand is worth after
/// each call, the rank sequence (it orders the ordinary cards and gives the manilha after the vira) and the order of the
/// manilhas' suits.
/// </summary>
[Serializable]
public class TrucoSettings
{
    [Tooltip("The first team to reach this many points wins the game.")]
    [Min(1)] public int pointsToWin = 12;
    [Tooltip("What a hand is worth: [0] at the start, then after each accepted call (Truco 3, Six 6, Nine 9, Twelve 12).")]
    public List<int> handValues = new List<int> { 1, 3, 6, 9, 12 };
    [Tooltip("Name of each call (same order as Hand Values; the first is unused).")]
    public List<string> callNames = new List<string> { "", "Truco", "Six", "Nine", "Twelve" };
    [Tooltip("The ranks from weakest to strongest. The rank after the vira's (wrapping around) is the manilha. " +
             "Default (the 40-card Truco deck): 4 5 6 7 Q J K A 2 3. A rank that is not in the list is not in the deck.")]
    public List<CardRank> rankSequence = new List<CardRank>
    {
        CardRank.Four, CardRank.Five, CardRank.Six, CardRank.Seven,
        CardRank.Queen, CardRank.Jack, CardRank.King, CardRank.Ace, CardRank.Two, CardRank.Three,
    };
    [Tooltip("Play with the 40-card deck: no 8, 9 or 10 (after the 7 comes the Q; a vira 7 makes the Q the manilha). " +
             "Off = the 8, 9 and 10 of the Rank Sequence are played too.")]
    public bool fortyCardDeck = true;

    [NonSerialized] private List<CardRank> playedRanks;
    [NonSerialized] private int playedFrom = -1;

    /// <summary>The ranks played, weakest to strongest (the Rank Sequence without 8, 9 and 10 with the 40-card deck).</summary>
    public List<CardRank> PlayedRanks
    {
        get
        {
            int key = (rankSequence != null ? rankSequence.Count : 0) * 2 + (fortyCardDeck ? 1 : 0);
            if (playedRanks == null || playedFrom != key)
            {
                playedRanks = new List<CardRank>();
                if (rankSequence != null)
                    foreach (CardRank r in rankSequence)
                        if (!playedRanks.Contains(r) && !(fortyCardDeck && (r == CardRank.Eight || r == CardRank.Nine || r == CardRank.Ten)))
                            playedRanks.Add(r);
                playedFrom = key;
            }
            return playedRanks;
        }
    }

    /// <summary>Is this rank in the deck?</summary>
    public bool InDeck(CardRank rank) => PlayedRanks.Contains(rank);
    [Tooltip("The manilhas' suits from strongest to weakest. Default: Clubs, Hearts, Spades, Diamonds.")]
    public List<CardSuit> manilhaSuits = new List<CardSuit> { CardSuit.Clubs, CardSuit.Hearts, CardSuit.Spades, CardSuit.Diamonds };

    [Header("Hand of eleven (mão de onze)")]
    [Tooltip("When one team is one point from winning, that hand is played 'trucada': its players see each other's cards " +
             "and decide to play it (worth Hand Of Eleven Value) or run (the other team scores 1). Nobody can call Truco " +
             "in that hand. When both teams are one point from winning (mão de ferro), nobody sees anybody else's cards and " +
             "nobody can call.")]
    public bool handOfEleven = true;
    [Tooltip("What a hand of eleven is worth when the team plays it.")]
    [Min(1)] public int handOfElevenValue = 3;
    [Tooltip("What the other team gets when the team at eleven runs.")]
    [Min(0)] public int handOfElevenRunValue = 1;

    [Header("Pacing")]
    [Tooltip("Seconds the cards of a finished trick stay on the table.")]
    [Min(0f)] public float trickPauseSeconds = 1.6f;
    [Tooltip("Seconds between the end of a hand and the next deal.")]
    [Min(0f)] public float handPauseSeconds = 2.5f;

    [Header("Bots")]
    [Tooltip("How bold bots are with calls and raises (0 = careful, 1 = bluffs a lot).")]
    [Range(0f, 1f)] public float botBoldness = 0.45f;

    /// <summary>The value after <paramref name="index"/> calls (clamped to the list).</summary>
    public int Value(int index) => handValues == null || handValues.Count == 0 ? 1 : handValues[Mathf.Clamp(index, 0, handValues.Count - 1)];

    public int MaxIndex => handValues == null ? 0 : handValues.Count - 1;

    public string CallName(int index)
    {
        if (callNames != null && index >= 0 && index < callNames.Count && !string.IsNullOrWhiteSpace(callNames[index]))
            return callNames[index];
        return Value(index).ToString();
    }

    public void Validate(List<string> errors, List<string> warnings)
    {
        if (handValues == null || handValues.Count < 1) errors.Add("Truco: Hand Values needs at least the starting value.");
        else
            for (int i = 1; i < handValues.Count; i++)
                if (handValues[i] <= handValues[i - 1])
                    warnings.Add($"Truco: Hand Values should grow ({handValues[i - 1]} → {handValues[i]}).");
        if (rankSequence == null || PlayedRanks.Count < 4) errors.Add("Truco: Rank Sequence needs at least 4 ranks that are played.");
        else if (new HashSet<CardRank>(rankSequence).Count != rankSequence.Count) errors.Add("Truco: Rank Sequence has a rank twice.");
        if (rankSequence != null && PlayedRanks.Count * 4 < 13)
            errors.Add("Truco: the deck is too small to deal 3 cards to 4 players and a vira.");
        if (manilhaSuits == null || manilhaSuits.Count != 4 || new HashSet<CardSuit>(manilhaSuits).Count != 4)
            errors.Add("Truco: Manilha Suits must hold the 4 suits once each.");
        if (pointsToWin < 1) errors.Add("Truco: Points To Win must be at least 1.");
    }
}

/// <summary>
/// Card strength in Truco. <see cref="Strength"/> is a number where <b>lower is stronger</b>: the four manilhas are
/// 0-3 (by suit), then the ordinary ranks from the strongest of the sequence (3) to the weakest (4); suits do not matter
/// for ordinary cards, so two of the same rank tie.
/// </summary>
public sealed class TrucoRules
{
    /// <summary>A trick nobody won (the strongest cards of both teams tie).</summary>
    public const int Tie = -1;
    /// <summary>A hand still being played.</summary>
    public const int Undecided = -2;
    /// <summary>A hand where every trick tied: nobody scores.</summary>
    public const int Draw = -3;

    private readonly TrucoSettings settings;

    public TrucoRules(TrucoSettings settings)
    {
        this.settings = settings ?? new TrucoSettings();
    }

    public TrucoSettings Settings => settings;

    /// <summary>The manilha rank for a vira: the next rank played, wrapping (vira 7 → Q; vira 3 → 4).</summary>
    public CardRank ManilhaRankFor(PlayingCard vira)
    {
        List<CardRank> seq = settings.PlayedRanks;
        int i = seq.IndexOf(vira.rank);
        if (i < 0)
            return vira.rank;
        return seq[(i + 1) % seq.Count];
    }

    public bool IsManilha(PlayingCard card, CardRank manilha) => card.rank == manilha;

    /// <summary>Lower = stronger: manilhas 0-3 by suit, then ordinary ranks from 4 (the strongest, 3) to <see cref="WeakestStrength"/> (4).</summary>
    public int Strength(PlayingCard card, CardRank manilha)
    {
        if (card.rank == manilha)
        {
            int s = settings.manilhaSuits.IndexOf(card.suit);
            return s < 0 ? 3 : s;
        }
        List<CardRank> seq = settings.PlayedRanks;
        int i = seq.IndexOf(card.rank);
        return 4 + (seq.Count - 1 - Mathf.Max(0, i));
    }

    /// <summary>The strength number of the weakest ordinary card (the higher end of the scale).</summary>
    public int WeakestStrength => 4 + Mathf.Max(0, settings.PlayedRanks.Count - 1);

    /// <summary>A new Truco deck: every suit of the ranks played (40 cards with the default rules).</summary>
    public CardDeck NewDeck()
    {
        var cards = new List<PlayingCard>();
        foreach (PlayingCard c in CardDeck.Standard52())
            if (settings.InDeck(c.rank))
                cards.Add(c);
        return new CardDeck(cards);
    }

    /// <summary>Negative when <paramref name="a"/> beats <paramref name="b"/>, positive when it loses, 0 for a tie.</summary>
    public int Compare(PlayingCard a, PlayingCard b, CardRank manilha) => Strength(a, manilha).CompareTo(Strength(b, manilha));

    /// <summary>
    /// The trick's result: the team of the strongest card, or <see cref="Tie"/> when the strongest cards of both teams
    /// are equal. <paramref name="bestSeat"/> is the first seat that played the strongest card.
    /// </summary>
    public int TrickWinner(IReadOnlyList<(int seat, PlayingCard card)> plays, CardRank manilha, Func<int, int> teamOf, out int bestSeat)
    {
        bestSeat = -1;
        int best = int.MaxValue;
        for (int i = 0; i < plays.Count; i++)
        {
            int s = Strength(plays[i].card, manilha);
            if (s < best)
            {
                best = s;
                bestSeat = plays[i].seat;
            }
        }
        if (bestSeat < 0)
            return Tie;
        int team = teamOf(bestSeat);
        for (int i = 0; i < plays.Count; i++)
            if (Strength(plays[i].card, manilha) == best && teamOf(plays[i].seat) != team)
                return Tie;
        return team;
    }

    /// <summary>
    /// Who wins the hand after <paramref name="played"/> tricks (results: a team, or <see cref="Tie"/>):
    /// <list type="bullet">
    /// <item>the first two tricks → their winner; the first trick and a tie in the second → the first trick's winner;</item>
    /// <item>a tied first trick → the second trick's winner;</item>
    /// <item>two tied tricks → the third trick's winner; three ties → <see cref="Draw"/>;</item>
    /// <item>one trick each → the third trick's winner; if the third ties, the first trick's winner.</item>
    /// </list>
    /// <see cref="Undecided"/> while it needs another trick.
    /// </summary>
    public static int HandWinner(IReadOnlyList<int> results, int played)
    {
        if (played < 2)
            return Undecided;
        int r0 = results[0], r1 = results[1];
        if (played == 2)
        {
            if (r0 >= 0 && (r1 == r0 || r1 == Tie)) return r0;
            if (r0 == Tie && r1 >= 0) return r1;
            return Undecided;
        }
        int r2 = results[2];
        if (r0 == Tie && r1 == Tie)
            return r2 >= 0 ? r2 : Draw;
        if (r2 >= 0)
            return r2;
        return r0 >= 0 ? r0 : r1 >= 0 ? r1 : Draw;
    }
}
