using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The rules of blackjack at a <see cref="CardTable"/> (on the <see cref="BlackjackDefinition"/>).</summary>
[Serializable]
public class BlackjackSettings
{
    [Tooltip("Chips each player's stake buys. At the end everyone gets back their chips' worth (twice the chips = twice the stake).")]
    [Min(10)] public int startingChips = 100;
    [Tooltip("The bets a player can make each round (chips).")]
    public List<int> bets = new List<int> { 10, 25, 50 };
    [Tooltip("Rounds in a game (it ends sooner when everyone is broke).")]
    [Min(1)] public int rounds = 6;
    [Tooltip("Decks in the shoe (reshuffled when it runs low).")]
    [Range(1, 8)] public int decks = 2;
    [Tooltip("A blackjack (an ace and a ten-card on the first two cards) pays this much of the bet (1.5 = 3:2).")]
    [Min(1f)] public float blackjackPays = 1.5f;
    [Tooltip("The dealer also hits a soft 17 (an ace counted as 11 making 17).")]
    public bool dealerHitsSoft17 = false;
    [Tooltip("Seconds the result of a round stays on the table.")]
    [Min(0f)] public float roundPauseSeconds = 3f;
    [Tooltip("Seconds between two dealer cards.")]
    [Min(0f)] public float dealerCardSeconds = 0.7f;
}

/// <summary>
/// Blackjack against the house: every player bets, gets two cards face up and plays in turn — Hit, Stand, or Double
/// (one more card for twice the bet). The dealer's second card stays face down until the players are done; the dealer
/// draws to 17. Closer to 21 than the dealer wins the bet, a blackjack pays 3:2, a bust loses. No splits or insurance.
/// </summary>
public sealed class BlackjackGame : ChipGameSession
{
    public const int Bet = 1;
    public const int Hit = 2;
    public const int Stand = 3;
    public const int Double = 4;

    public enum Phase { Betting, Players, Dealer, Pause }

    private readonly BlackjackSettings settings;
    private readonly List<PlayingCard>[] hands;
    private readonly List<PlayingCard> dealer = new List<PlayingCard>();
    private readonly int[] bets;
    private readonly bool[] done;
    private readonly bool[] inRound;
    private readonly string[] results;
    private CardDeck shoe;
    private int turn = -1;
    private float timer;

    public Phase State { get; private set; } = Phase.Betting;
    public int Round { get; private set; }
    public int Turn => turn;
    public IReadOnlyList<PlayingCard> Hand(int seat) => hands[seat];
    public IReadOnlyList<PlayingCard> DealerHand => dealer;
    public int BetOf(int seat) => bets[seat];
    public BlackjackSettings Settings => settings;

    public override bool PayoutsFromHouse => true;

    public BlackjackGame(BlackjackSettings settings, CardGameSetup setup, int seats)
        : base(setup, seats, (settings ?? new BlackjackSettings()).startingChips)
    {
        this.settings = settings ?? new BlackjackSettings();
        hands = new List<PlayingCard>[seats];
        for (int i = 0; i < seats; i++) hands[i] = new List<PlayingCard>();
        bets = new int[seats];
        done = new bool[seats];
        inRound = new bool[seats];
        results = new string[seats];
    }

    /// <summary>The best total of <paramref name="cards"/> (aces 11 when it does not bust); <paramref name="soft"/> = an ace counts 11.</summary>
    public static int Total(IReadOnlyList<PlayingCard> cards, out bool soft)
    {
        int total = 0, aces = 0;
        foreach (PlayingCard c in cards)
        {
            int r = (int)c.rank;
            if (r == 1) { aces++; total += 1; }
            else total += Math.Min(10, r);
        }
        soft = false;
        if (aces > 0 && total + 10 <= 21)
        {
            total += 10;
            soft = true;
        }
        return total;
    }

    public static bool IsBlackjack(IReadOnlyList<PlayingCard> cards) => cards.Count == 2 && Total(cards, out _) == 21;

    // ------------------------------------------------------------------ the game
    public override void Start()
    {
        NewShoe();
        NewRound();
    }

    private void NewShoe()
    {
        var cards = new List<PlayingCard>();
        for (int d = 0; d < settings.decks; d++)
            cards.AddRange(CardDeck.Standard52());
        shoe = new CardDeck(cards);
        shoe.Shuffle(rng);
    }

    private PlayingCard Draw()
    {
        if (shoe.Count < 12)
        {
            NewShoe();
            Say("The dealer shuffles a new shoe.");
        }
        return shoe.Draw();
    }

    private void NewRound()
    {
        Round++;
        dealer.Clear();
        for (int i = 0; i < seatCount; i++)
        {
            hands[i].Clear();
            bets[i] = 0;
            done[i] = false;
            results[i] = "";
            inRound[i] = inGame[i] && chips[i] > 0;
        }
        if (!AnyInRound())
        {
            End("Everyone is out of chips.");
            return;
        }
        State = Phase.Betting;
        turn = -1;
        lastEvent = $"Round {Round} of {settings.rounds}: place your bets.";
        Say(lastEvent);
    }

    private bool AnyInRound()
    {
        for (int i = 0; i < seatCount; i++)
            if (inRound[i] && inGame[i]) return true;
        return false;
    }

    public override void Tick(float dt)
    {
        TickBubbles(dt);
        if (IsOver)
            return;
        if (State == Phase.Dealer)
        {
            timer -= dt;
            if (timer <= 0f)
                DealerStep();
        }
        else if (State == Phase.Pause)
        {
            timer -= dt;
            if (timer <= 0f)
            {
                if (Round >= settings.rounds) End($"{settings.rounds} rounds played.");
                else NewRound();
            }
        }
    }

    public override bool TryAct(int seat, CardAction a, out string reason)
    {
        reason = "";
        if (IsOver) { reason = "The game is over"; return false; }
        if (seat < 0 || seat >= seatCount || !inGame[seat]) { reason = "You are not playing"; return false; }
        switch (a.kind)
        {
            case Bet:
                if (State != Phase.Betting) { reason = "Bets are closed"; return false; }
                if (!inRound[seat]) { reason = "You have no chips"; return false; }
                if (bets[seat] > 0) { reason = "You already bet"; return false; }
                if (a.arg <= 0 || a.arg > chips[seat]) { reason = "You do not have that many chips"; return false; }
                bets[seat] = a.arg;
                chips[seat] -= a.arg;
                Bubble(seat, $"Bet {a.arg}");
                Say($"{names[seat]} bets {a.arg}.");
                if (AllBet()) Deal();
                return true;

            case Hit:
            case Stand:
            case Double:
                if (State != Phase.Players || seat != turn) { reason = "Not your turn"; return false; }
                if (a.kind == Double)
                {
                    if (hands[seat].Count != 2) { reason = "Double only on your first two cards"; return false; }
                    if (chips[seat] < bets[seat]) { reason = "Not enough chips to double"; return false; }
                    chips[seat] -= bets[seat];
                    bets[seat] *= 2;
                    hands[seat].Add(Draw());
                    Bubble(seat, "Double!");
                    Say($"{names[seat]} doubles: {hands[seat][2]} ({Total(hands[seat], out _)}).");
                    FinishPlayer(seat);
                    return true;
                }
                if (a.kind == Stand)
                {
                    Bubble(seat, "Stand");
                    Say($"{names[seat]} stands on {Total(hands[seat], out _)}.");
                    FinishPlayer(seat);
                    return true;
                }
                hands[seat].Add(Draw());
                int t = Total(hands[seat], out _);
                Say($"{names[seat]} hits: {hands[seat][hands[seat].Count - 1]} ({t}).");
                if (t > 21) { Bubble(seat, "Bust!"); FinishPlayer(seat); }
                else if (t == 21) FinishPlayer(seat);
                else MarkChanged();
                return true;
        }
        reason = "Unknown action";
        return false;
    }

    private bool AllBet()
    {
        for (int i = 0; i < seatCount; i++)
            if (inRound[i] && inGame[i] && bets[i] <= 0) return false;
        return true;
    }

    private void Deal()
    {
        for (int c = 0; c < 2; c++)
        {
            for (int i = 0; i < seatCount; i++)
                if (inRound[i] && inGame[i]) hands[i].Add(Draw());
            dealer.Add(Draw());
        }
        for (int i = 0; i < seatCount; i++)
            if (inRound[i] && IsBlackjack(hands[i]))
            {
                done[i] = true;
                Bubble(i, "Blackjack!");
            }
        State = Phase.Players;
        Say($"The dealer shows {dealer[0]}.");
        turn = Next(-1, CanPlay);
        if (turn < 0) StartDealer();
    }

    private bool CanPlay(int s) => inRound[s] && inGame[s] && !done[s];

    private void FinishPlayer(int seat)
    {
        done[seat] = true;
        turn = Next(seat, CanPlay);
        if (turn < 0) StartDealer();
        MarkChanged();
    }

    private void StartDealer()
    {
        turn = -1;
        State = Phase.Dealer;
        timer = settings.dealerCardSeconds;
        Say($"The dealer turns over {dealer[1]}: {Total(dealer, out _)}.");
    }

    private bool AnyStanding()
    {
        for (int i = 0; i < seatCount; i++)
            if (inRound[i] && inGame[i] && hands[i].Count > 0 && Total(hands[i], out _) <= 21 && !IsBlackjack(hands[i])) return true;
        return false;
    }

    private void DealerStep()
    {
        int total = Total(dealer, out bool soft);
        bool hits = AnyStanding() && (total < 17 || (total == 17 && soft && settings.dealerHitsSoft17));
        if (hits)
        {
            dealer.Add(Draw());
            Say($"The dealer draws {dealer[dealer.Count - 1]}: {Total(dealer, out _)}.");
            timer = settings.dealerCardSeconds;
            return;
        }
        Settle();
    }

    private void Settle()
    {
        int d = Total(dealer, out _);
        bool dealerBj = IsBlackjack(dealer);
        for (int i = 0; i < seatCount; i++)
        {
            if (!inRound[i] || hands[i].Count == 0)
                continue;
            int p = Total(hands[i], out _);
            int bet = bets[i];
            int back;
            string what;
            if (!inGame[i]) { back = 0; what = "left"; }
            else if (p > 21) { back = 0; what = "busts"; }
            else if (IsBlackjack(hands[i]) && !dealerBj) { back = bet + Mathf.FloorToInt(bet * settings.blackjackPays); what = "blackjack"; }
            else if (dealerBj && !IsBlackjack(hands[i])) { back = 0; what = "loses to the dealer's blackjack"; }
            else if (d > 21 || p > d) { back = bet * 2; what = "wins"; }
            else if (p == d) { back = bet; what = "push"; }
            else { back = 0; what = "loses"; }
            chips[i] += back;
            results[i] = back > bet ? $"+{back - bet}" : back == bet ? "Push" : $"-{bet}";
            Bubble(i, results[i]);
            Say($"{names[i]} {what} ({p} against {d}): {results[i]}.");
        }
        State = Phase.Pause;
        timer = settings.roundPauseSeconds;
        lastEvent = d > 21 ? $"The dealer busts with {d}." : $"The dealer has {d}.";
        MarkChanged();
    }

    protected override void BeforeEnd()
    {
        // Ended in the middle of a round: the bets on the table go back.
        if (State == Phase.Pause)
            return;
        for (int i = 0; i < seatCount; i++)
        {
            if (inGame[i]) chips[i] += bets[i];
            bets[i] = 0;
        }
    }

    protected override void OnLeaving(int seat)
    {
        if (State == Phase.Betting && bets[seat] == 0)
            inRound[seat] = false;
    }

    protected override void AfterLeaving(int seat)
    {
        if (State == Phase.Betting && AllBet()) Deal();
        else if (State == Phase.Players && turn == seat)
        {
            done[seat] = true;
            turn = Next(seat, CanPlay);
            if (turn < 0) StartDealer();
        }
    }

    // ------------------------------------------------------------------ bots
    public override bool WantsBotAction(int seat)
    {
        if (IsOver || seat < 0 || seat >= seatCount || !bots[seat] || !inGame[seat])
            return false;
        if (State == Phase.Betting) return inRound[seat] && bets[seat] == 0;
        return State == Phase.Players && turn == seat;
    }

    public override CardAction BotDecide(int seat)
    {
        if (State == Phase.Betting)
        {
            var options = new List<int>();
            foreach (int b in settings.bets) if (b > 0 && b <= chips[seat]) options.Add(b);
            int bet = options.Count > 0 ? options[rng.Next(Math.Min(options.Count, 2))] : chips[seat];
            return new CardAction(Bet, Math.Max(1, bet));
        }
        // A simple basic strategy.
        int t = Total(hands[seat], out bool soft);
        int up = Math.Min(10, (int)dealer[0].rank);
        if (up == 1) up = 11;
        if (hands[seat].Count == 2 && (t == 11 || (t == 10 && up < 10)) && chips[seat] >= bets[seat])
            return new CardAction(Double);
        if (soft) return new CardAction(t <= 17 || (t == 18 && up >= 9) ? Hit : Stand);
        if (t <= 11) return new CardAction(Hit);
        if (t == 12) return new CardAction(up >= 4 && up <= 6 ? Stand : Hit);
        if (t <= 16) return new CardAction(up >= 7 ? Hit : Stand);
        return new CardAction(Stand);
    }

    // ------------------------------------------------------------------ view
    public override void FillView(CardTableView v, int viewerSeat)
    {
        FillCommon(v);
        v.turnSeat = State == Phase.Players ? turn : -1;
        for (int i = 0; i < seatCount && i < v.seats.Count; i++)
        {
            v.seats[i].isTurn = i == v.turnSeat;
            v.seats[i].cardCount = 0; // the cards lie face up on the table
            if (bets[i] > 0 && string.IsNullOrEmpty(v.seats[i].bubble))
                v.seats[i].bubble = $"{bets[i]} on the table";
        }
        v.played.Clear();
        for (int i = 0; i < seatCount; i++)
            foreach (PlayingCard c in hands[i])
                v.played.Add(new CardPlayView { seat = i, card = c.Code, best = results[i].StartsWith("+") });
        v.shared.Clear();
        bool hole = State == Phase.Betting || State == Phase.Players;
        for (int i = 0; i < dealer.Count; i++)
            v.shared.Add(i == 1 && hole ? PlayingCard.Hidden : dealer[i].Code);
        v.sharedLabel = dealer.Count == 0 ? "Dealer" : hole ? $"Dealer shows {dealer[0]}" : $"Dealer: {Total(dealer, out _)}";
        v.deckCount = shoe != null ? shoe.Count : 0;
        v.valueText = $"Round {Math.Min(Round, settings.rounds)} of {settings.rounds} · dealer stands on {(settings.dealerHitsSoft17 ? "hard " : "")}17 · blackjack pays {settings.blackjackPays + 1f:0.#}:1";
        v.hand.Clear();
        v.handPlayable.Clear();
        v.status = Status(viewerSeat);
        v.actions.Clear();
        if (IsOver || viewerSeat < 0 || viewerSeat >= seatCount || !inGame[viewerSeat])
            return;
        if (State == Phase.Betting && inRound[viewerSeat] && bets[viewerSeat] == 0)
        {
            foreach (int b in settings.bets)
                if (b > 0)
                    v.actions.Add(new CardActionOption(new CardAction(Bet, b), $"Bet {b}", b <= chips[viewerSeat], "Not enough chips", 1));
            if (chips[viewerSeat] > 0)
                v.actions.Add(new CardActionOption(new CardAction(Bet, chips[viewerSeat]), $"All in ({chips[viewerSeat]})"));
        }
        if (State == Phase.Players && turn == viewerSeat)
        {
            int t = Total(hands[viewerSeat], out _);
            v.actions.Add(new CardActionOption(new CardAction(Hit), $"Hit ({t})", true, "", 1));
            v.actions.Add(new CardActionOption(new CardAction(Stand), "Stand"));
            if (hands[viewerSeat].Count == 2)
                v.actions.Add(new CardActionOption(new CardAction(Double), $"Double (+{bets[viewerSeat]})", chips[viewerSeat] >= bets[viewerSeat], "Not enough chips"));
        }
    }

    private string Status(int viewer)
    {
        if (IsOver) return lastEvent;
        switch (State)
        {
            case Phase.Betting:
                return viewer >= 0 && viewer < seatCount && inRound[viewer] && bets[viewer] == 0 ? "Place your bet." : "Waiting for the bets.";
            case Phase.Players:
                if (turn == viewer) return $"Your turn: {Total(hands[viewer], out _)}. Hit, stand or double.";
                return turn >= 0 ? $"{names[turn]} plays." : "";
            case Phase.Dealer:
                return "The dealer plays.";
            default:
                return lastEvent;
        }
    }
}

