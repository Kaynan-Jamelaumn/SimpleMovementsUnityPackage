using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A Truco player: their seat, team, name and the cards in their hand.</summary>
public sealed class TrucoPlayer
{
    public int Seat;
    public int Team;
    public string Name = "";
    public bool Bot;
    public readonly List<PlayingCard> Hand = new List<PlayingCard>(3);
}

/// <summary>One hand being played: up to three tricks, the cards of the current trick and each trick's result.</summary>
public sealed class TrucoRound
{
    /// <summary>Each trick's result: a team, <see cref="TrucoRules.Tie"/>, or <see cref="TrucoRules.Undecided"/> (not played yet).</summary>
    public readonly int[] Results = { TrucoRules.Undecided, TrucoRules.Undecided, TrucoRules.Undecided };
    /// <summary>The cards of the trick being played, in the order they were played.</summary>
    public readonly List<(int seat, PlayingCard card)> Plays = new List<(int seat, PlayingCard card)>(4);
    public int TricksPlayed;
    public int TrickStarter;
    /// <summary>The seat with the strongest card of the last finished trick.</summary>
    public int LastBestSeat = -1;
    /// <summary>The hand's winner once decided (a team, <see cref="TrucoRules.Draw"/>) or <see cref="TrucoRules.Undecided"/>.</summary>
    public int Winner = TrucoRules.Undecided;
}

/// <summary>
/// Truco calls (Truco → Six → Nine → Twelve): the accepted value, the call waiting for an answer, and who may call next
/// (never the team that made the last accepted call).
/// </summary>
public sealed class TrucoBetting
{
    /// <summary>Index in the settings' Hand Values of the value both teams accepted (0 = the starting value).</summary>
    public int AcceptedIndex;
    /// <summary>Index of the value being proposed (−1 = no call waiting).</summary>
    public int PendingIndex = -1;
    /// <summary>The team whose call (or raise) waits for an answer.</summary>
    public int CallerTeam = -1;
    public int CallerSeat = -1;
    /// <summary>The team that made the last accepted call: only the other team may call next.</summary>
    public int LastRaiseTeam = -1;

    public bool Pending => PendingIndex >= 0;
    public int RespondingTeam => CallerTeam < 0 ? -1 : 1 - CallerTeam;

    public bool CanCall(int team, TrucoSettings s, out string reason)
    {
        reason = "";
        if (Pending) { reason = "A call is waiting for an answer"; return false; }
        if (AcceptedIndex >= s.MaxIndex) { reason = "The hand is already worth the most"; return false; }
        if (LastRaiseTeam == team) { reason = "Your team made the last call"; return false; }
        return true;
    }

    public void Reset()
    {
        AcceptedIndex = 0;
        PendingIndex = -1;
        CallerTeam = -1;
        CallerSeat = -1;
        LastRaiseTeam = -1;
    }
}

/// <summary>
/// The Truco game (four players, two teams of two sitting opposite each other, first to 12): deals three cards each,
/// reveals the vira (its next rank is the manilha), plays up to three tricks per hand, handles Truco calls, raises,
/// acceptance and folding, and scores. It is the "Truco game manager"; the rules of strength live in
/// <see cref="TrucoRules"/>, the calls in <see cref="TrucoBetting"/>, the tricks in <see cref="TrucoRound"/>.
/// </summary>
public sealed class TrucoGame : CardGameSession
{
    // Action kinds (CardGameSession.PlayCard = 0 plays a card from the hand).
    public const int Call = 1;
    public const int Accept = 2;
    public const int Raise = 3;
    public const int Fold = 4;
    /// <summary>Hand of eleven: the team at eleven plays the hand.</summary>
    public const int PlayEleven = 5;
    /// <summary>Hand of eleven: the team at eleven runs (the other team scores).</summary>
    public const int RunEleven = 6;

    public enum Phase { Playing, Responding, TrickPause, HandPause, Over, ElevenDecision }

    /// <summary>The kind of hand being played.</summary>
    public enum HandKind
    {
        Normal,
        /// <summary>One team is one point from winning: it sees both hands and decides; no calls.</summary>
        HandOfEleven,
        /// <summary>Both teams are one point from winning: no calls, nobody sees anybody else's cards.</summary>
        IronHand,
    }

    private readonly TrucoSettings settings;
    private readonly TrucoRules rules;
    private readonly string[] teamNames;
    private readonly System.Random rng;
    private readonly TrucoPlayer[] players = new TrucoPlayer[4];
    private readonly int[] scores = new int[2];
    private readonly string[] bubbles = new string[4];
    private readonly float[] bubbleTimes = new float[4];
    private readonly bool[] botConsideredCall = new bool[4];
    private CardDeck deck;
    private PlayingCard vira;
    private CardRank manilha;
    private int dealer;
    private int turn;
    private float timer;
    private int winningTeam = -1;
    private string lastEvent = "";
    private int forcedValue;

    public Phase State { get; private set; } = Phase.HandPause;
    public TrucoRound Round { get; private set; } = new TrucoRound();
    public TrucoBetting Betting { get; } = new TrucoBetting();
    public TrucoRules Rules => rules;
    public TrucoSettings Settings => settings;
    public int HandNumber { get; private set; }
    /// <summary>Normal, hand of eleven or iron hand (both teams at eleven).</summary>
    public HandKind Kind { get; private set; }
    /// <summary>The team at eleven in a hand of eleven (−1 otherwise).</summary>
    public int ElevenTeam { get; private set; } = -1;
    public int Turn => turn;
    public int Dealer => dealer;
    public PlayingCard Vira => vira;
    public CardRank Manilha => manilha;
    public int Score(int team) => scores[Mathf.Clamp(team, 0, 1)];
    public TrucoPlayer Player(int seat) => players[seat];

    public override int SeatCount => 4;
    public override bool IsOver => State == Phase.Over;
    public override int WinningTeam => winningTeam;
    public override int TeamOf(int seat) => ((seat % 4) + 4) % 2;

    /// <summary>What the hand is worth now (the accepted value).</summary>
    public int HandValue => forcedValue > 0 ? forcedValue : settings.Value(Betting.AcceptedIndex);

    /// <summary>Can <paramref name="team"/> call Truco now? Never in a hand of eleven or an iron hand.</summary>
    public bool CanCallNow(int team, out string reason)
    {
        if (Kind != HandKind.Normal)
        {
            reason = Kind == HandKind.IronHand ? "Nobody can call when both teams are at eleven" : "Nobody can call in a hand of eleven";
            return false;
        }
        return Betting.CanCall(team, settings, out reason);
    }

    public TrucoGame(TrucoSettings settings, CardGameSetup setup, string teamA = "Team A", string teamB = "Team B")
    {
        this.settings = settings ?? new TrucoSettings();
        rules = new TrucoRules(this.settings);
        teamNames = new[] { teamA, teamB };
        rng = new System.Random(setup.seed);
        for (int i = 0; i < 4; i++)
        {
            players[i] = new TrucoPlayer
            {
                Seat = i,
                Team = TeamOf(i),
                Name = setup.names != null && i < setup.names.Length && !string.IsNullOrWhiteSpace(setup.names[i]) ? setup.names[i] : $"Player {i + 1}",
                Bot = setup.bots != null && i < setup.bots.Length && setup.bots[i],
            };
            bubbles[i] = "";
        }
        dealer = ((setup.firstDealer % 4) + 4) % 4;
    }

    // ------------------------------------------------------------------ the game
    public override void Start()
    {
        scores[0] = scores[1] = 0;
        winningTeam = -1;
        HandNumber = 0;
        dealer = (dealer + 3) % 4; // NewHand moves the deal on: the first hand is dealt by the setup's dealer
        NewHand();
    }

    /// <summary>Shuffles, deals three cards each (starting left of the dealer), reveals the vira.</summary>
    private void NewHand()
    {
        HandNumber++;
        dealer = (dealer + 1) % 4;
        deck = rules.NewDeck();
        deck.Shuffle(rng);
        foreach (TrucoPlayer p in players)
            p.Hand.Clear();
        int first = (dealer + 1) % 4;
        for (int c = 0; c < 3; c++)
            for (int k = 0; k < 4; k++)
                players[(first + k) % 4].Hand.Add(deck.Draw());
        vira = deck.Draw();
        manilha = rules.ManilhaRankFor(vira);
        Round = new TrucoRound { TrickStarter = first };
        Betting.Reset();
        Array.Clear(botConsideredCall, 0, 4);
        turn = first;
        forcedValue = 0;
        ElevenTeam = -1;
        Kind = HandKind.Normal;
        State = Phase.Playing;
        lastEvent = $"Hand {HandNumber}: the vira is {vira}, so the {PlayingCard.RankName(manilha)}s are manilhas.";
        Say(lastEvent);

        // Hand of eleven: a team one point from winning plays "trucada".
        int eleven = settings.pointsToWin - 1;
        if (settings.handOfEleven && eleven > 0)
        {
            bool a = scores[0] == eleven, b = scores[1] == eleven;
            if (a && b)
            {
                Kind = HandKind.IronHand;
                lastEvent = "Both teams are at eleven: no calls, nobody sees anybody else's cards.";
                Say(lastEvent);
            }
            else if (a || b)
            {
                Kind = HandKind.HandOfEleven;
                ElevenTeam = a ? 0 : 1;
                forcedValue = Mathf.Max(1, settings.handOfElevenValue);
                State = Phase.ElevenDecision;
                lastEvent = $"Hand of eleven: {teamNames[ElevenTeam]} sees both hands and decides to play (worth {forcedValue}) or run.";
                Say(lastEvent);
            }
        }
    }

    public override void Tick(float dt)
    {
        for (int i = 0; i < 4; i++)
        {
            if (bubbleTimes[i] <= 0f) continue;
            bubbleTimes[i] -= dt;
            if (bubbleTimes[i] <= 0f)
            {
                bubbles[i] = "";
                MarkChanged();
            }
        }
        if (State != Phase.TrickPause && State != Phase.HandPause)
            return;
        timer -= dt;
        if (timer > 0f)
            return;
        if (State == Phase.TrickPause)
            AfterTrick();
        else
            NewHand();
    }

    public override bool TryAct(int seat, CardAction action, out string reason)
    {
        reason = "";
        if (seat < 0 || seat >= 4) { reason = "Not seated"; return false; }
        if (State == Phase.Over) { reason = "The game is over"; return false; }
        int team = TeamOf(seat);
        switch (action.kind)
        {
            case PlayCard:
                if (State != Phase.Playing)
                {
                    reason = State == Phase.Responding ? "Waiting for an answer to the call"
                        : State == Phase.ElevenDecision ? "The team at eleven is deciding whether to play" : "Wait";
                    return false;
                }
                if (seat != turn) { reason = "Not your turn"; return false; }
                if (action.arg < 0 || action.arg >= players[seat].Hand.Count) { reason = "You do not have that card"; return false; }
                PlayCardAt(seat, action.arg);
                return true;

            case PlayEleven:
            case RunEleven:
                if (State != Phase.ElevenDecision) { reason = "Not a hand of eleven"; return false; }
                if (team != ElevenTeam) { reason = "The team at eleven decides"; return false; }
                if (action.kind == PlayEleven)
                {
                    State = Phase.Playing;
                    Bubble(seat, "We play!");
                    lastEvent = $"{players[seat].Name} plays the hand of eleven: it is worth {HandValue}.";
                    Say(lastEvent);
                }
                else
                {
                    Bubble(seat, "We run");
                    forcedValue = Mathf.Max(0, settings.handOfElevenRunValue);
                    lastEvent = $"{players[seat].Name} runs from the hand of eleven.";
                    Say(lastEvent);
                    if (forcedValue > 0)
                        EndHand(1 - team, folded: true);
                    else
                        EndHand(TrucoRules.Draw, folded: true);
                }
                return true;

            case Call:
                if (State != Phase.Playing || seat != turn) { reason = "You can call only on your turn"; return false; }
                if (!CanCallNow(team, out reason)) return false;
                Betting.PendingIndex = Betting.AcceptedIndex + 1;
                Betting.CallerTeam = team;
                Betting.CallerSeat = seat;
                State = Phase.Responding;
                Bubble(seat, settings.CallName(Betting.PendingIndex).ToUpperInvariant() + "!");
                lastEvent = $"{players[seat].Name} calls {settings.CallName(Betting.PendingIndex)}! ({settings.Value(Betting.PendingIndex)} points)";
                Say(lastEvent);
                return true;

            case Accept:
                if (!Answering(team, out reason)) return false;
                Betting.AcceptedIndex = Betting.PendingIndex;
                Betting.LastRaiseTeam = Betting.CallerTeam;
                Betting.PendingIndex = -1;
                Betting.CallerTeam = -1;
                State = Phase.Playing;
                Bubble(seat, "Accepted!");
                lastEvent = $"{players[seat].Name} accepts: the hand is worth {HandValue}.";
                Say(lastEvent);
                return true;

            case Raise:
                if (!Answering(team, out reason)) return false;
                if (Betting.PendingIndex >= settings.MaxIndex) { reason = "It cannot go higher"; return false; }
                Betting.PendingIndex++;
                Betting.CallerTeam = team;
                Betting.CallerSeat = seat;
                Bubble(seat, settings.CallName(Betting.PendingIndex).ToUpperInvariant() + "!");
                lastEvent = $"{players[seat].Name} raises to {settings.CallName(Betting.PendingIndex)}! ({settings.Value(Betting.PendingIndex)} points)";
                Say(lastEvent);
                return true;

            case Fold:
                if (!Answering(team, out reason)) return false;
                int winner = Betting.CallerTeam;
                Bubble(seat, "Fold");
                Betting.PendingIndex = -1;
                lastEvent = $"{players[seat].Name} folds.";
                Say(lastEvent);
                EndHand(winner, folded: true);
                return true;
        }
        reason = "Unknown action";
        return false;
    }

    private bool Answering(int team, out string reason)
    {
        reason = "";
        if (State != Phase.Responding || !Betting.Pending) { reason = "Nothing to answer"; return false; }
        if (team != Betting.RespondingTeam) { reason = "The other team must answer"; return false; }
        return true;
    }

    private void PlayCardAt(int seat, int index)
    {
        TrucoPlayer p = players[seat];
        PlayingCard card = p.Hand[index];
        p.Hand.RemoveAt(index);
        Round.Plays.Add((seat, card));
        Say($"{p.Name} plays {card}.");
        if (Round.Plays.Count >= 4)
            ResolveTrick();
        else
            turn = (seat + 1) % 4;
    }

    private void ResolveTrick()
    {
        int result = rules.TrickWinner(Round.Plays, manilha, TeamOf, out int best);
        int t = Round.TricksPlayed;
        Round.Results[t] = result;
        Round.TricksPlayed++;
        Round.LastBestSeat = best;
        Round.Winner = TrucoRules.HandWinner(Round.Results, Round.TricksPlayed);
        if (Round.Winner == TrucoRules.Undecided && Round.TricksPlayed >= 3)
            Round.Winner = TrucoRules.Draw;
        lastEvent = result == TrucoRules.Tie
            ? $"Trick {t + 1} is tied."
            : $"{players[best].Name} wins trick {t + 1} for {teamNames[result]}.";
        Say(lastEvent);
        State = Phase.TrickPause;
        timer = settings.trickPauseSeconds;
        if (timer <= 0f)
            AfterTrick();
    }

    private void AfterTrick()
    {
        if (Round.Winner != TrucoRules.Undecided)
        {
            EndHand(Round.Winner, folded: false);
            return;
        }
        // The winner of the trick leads the next one; after a tie, the one who led the tied trick leads again.
        int last = Round.Results[Round.TricksPlayed - 1];
        int next = last == TrucoRules.Tie ? Round.TrickStarter : Round.LastBestSeat;
        Round.Plays.Clear();
        Round.TrickStarter = next;
        turn = next;
        State = Phase.Playing;
        Array.Clear(botConsideredCall, 0, 4);
        MarkChanged();
    }

    private void EndHand(int winner, bool folded)
    {
        int value = HandValue;
        if (winner >= 0)
        {
            scores[winner] += value;
            lastEvent = folded
                ? $"{teamNames[winner]} gets {value} point{(value == 1 ? "" : "s")} (the other team folded)."
                : $"{teamNames[winner]} wins the hand: +{value} point{(value == 1 ? "" : "s")}.";
        }
        else
        {
            lastEvent = "Every trick tied: nobody scores.";
        }
        Say(lastEvent);
        Round.Winner = winner >= 0 ? winner : TrucoRules.Draw;
        if (winner >= 0 && scores[winner] >= settings.pointsToWin)
        {
            winningTeam = winner;
            State = Phase.Over;
            lastEvent = $"{teamNames[winner]} wins the game {scores[winner]} to {scores[1 - winner]}!";
            Say(lastEvent);
            return;
        }
        State = Phase.HandPause;
        timer = settings.handPauseSeconds;
        if (timer <= 0f)
            NewHand();
    }

    public override void Forfeit(int team, string why)
    {
        if (State == Phase.Over)
            return;
        winningTeam = team == 0 ? 1 : 0;
        State = Phase.Over;
        lastEvent = $"{why} {teamNames[winningTeam]} wins the game.";
        Say(lastEvent);
    }

    private void Bubble(int seat, string text)
    {
        bubbles[seat] = text;
        bubbleTimes[seat] = 2.5f;
    }

    // ------------------------------------------------------------------ bots
    public override bool WantsBotAction(int seat)
    {
        if (seat < 0 || seat >= 4 || !players[seat].Bot)
            return false;
        if (State == Phase.Playing)
            return seat == turn;
        if (State == Phase.ElevenDecision)
        {
            // A human of the team at eleven decides; otherwise its first bot.
            if (TeamOf(seat) != ElevenTeam)
                return false;
            for (int s = 0; s < 4; s++)
            {
                if (TeamOf(s) != ElevenTeam) continue;
                if (!players[s].Bot) return false;
                return s == seat;
            }
            return false;
        }
        if (State != Phase.Responding)
            return false;
        int team = Betting.RespondingTeam;
        if (TeamOf(seat) != team)
            return false;
        // A human of the answering team decides; otherwise the first bot of the team after the caller answers.
        for (int k = 1; k <= 4; k++)
        {
            int s = (Betting.CallerSeat + k) % 4;
            if (TeamOf(s) != team) continue;
            if (!players[s].Bot) return false;
            return s == seat;
        }
        return false;
    }

    public override CardAction BotDecide(int seat)
    {
        TrucoPlayer me = players[seat];
        float strength = HandStrength(me);
        float bold = settings.botBoldness;
        if (State == Phase.ElevenDecision)
        {
            // Both hands of the team are known: play when together they are good enough.
            float both = (strength + HandStrength(players[(seat + 2) % 4])) * 0.5f;
            return new CardAction(both > 0.42f - bold * 0.12f ? PlayEleven : RunEleven);
        }
        if (State == Phase.Responding)
        {
            float need = 0.34f - bold * 0.15f + 0.05f * Betting.PendingIndex;
            if (strength > need + 0.25f && Betting.PendingIndex < settings.MaxIndex && rng.NextDouble() < 0.45 + bold * 0.3)
                return new CardAction(Raise);
            if (strength > need || rng.NextDouble() < bold * 0.15)
                return new CardAction(Accept);
            return new CardAction(Fold);
        }
        if (!botConsideredCall[seat] && CanCallNow(me.Team, out _))
        {
            botConsideredCall[seat] = true;
            bool strong = strength > 0.72f - bold * 0.2f;
            bool bluff = rng.NextDouble() < bold * 0.07;
            if ((strong && rng.NextDouble() < 0.65) || bluff)
                return new CardAction(Call);
        }
        return new CardAction(PlayCard, ChooseCard(me));
    }

    /// <summary>0-1: how good a hand is (manilhas count most).</summary>
    public float HandStrength(TrucoPlayer p)
    {
        if (p.Hand.Count == 0)
            return 0f;
        float sum = 0f;
        foreach (PlayingCard c in p.Hand)
        {
            int s = rules.Strength(c, manilha);
            float span = Mathf.Max(1, rules.WeakestStrength - 4);
            sum += s <= 3 ? 1f - s * 0.04f : (rules.WeakestStrength - s) / span * 0.75f;
        }
        return Mathf.Clamp01(sum / 3f + (Round.Results[0] == p.Team ? 0.15f : 0f));
    }

    private int ChooseCard(TrucoPlayer me)
    {
        List<PlayingCard> hand = me.Hand;
        int weakest = 0, strongest = 0;
        for (int i = 1; i < hand.Count; i++)
        {
            if (rules.Strength(hand[i], manilha) > rules.Strength(hand[weakest], manilha)) weakest = i;
            if (rules.Strength(hand[i], manilha) < rules.Strength(hand[strongest], manilha)) strongest = i;
        }
        if (Round.Plays.Count == 0)
        {
            if (Round.TricksPlayed == 0 && hand.Count == 3)
            {
                // Open the hand with the middle card: keep the best for later.
                for (int i = 0; i < 3; i++)
                    if (i != weakest && i != strongest) return i;
            }
            return strongest;
        }
        int bestStrength = int.MaxValue, bestSeat = -1;
        foreach ((int seat, PlayingCard card) in Round.Plays)
        {
            int s = rules.Strength(card, manilha);
            if (s < bestStrength) { bestStrength = s; bestSeat = seat; }
        }
        if (bestSeat >= 0 && TeamOf(bestSeat) == me.Team)
            return weakest; // the partner is winning: keep the good cards
        int pick = -1;
        for (int i = 0; i < hand.Count; i++)
        {
            int s = rules.Strength(hand[i], manilha);
            if (s < bestStrength && (pick < 0 || s > rules.Strength(hand[pick], manilha)))
                pick = i; // the weakest card that still wins
        }
        return pick >= 0 ? pick : weakest;
    }

    // ------------------------------------------------------------------ view
    public override void FillView(CardTableView v, int viewerSeat)
    {
        v.teamNames.Clear();
        v.teamScores.Clear();
        v.teamNames.AddRange(teamNames);
        v.teamScores.Add(scores[0]);
        v.teamScores.Add(scores[1]);
        v.turnSeat = State == Phase.Playing ? turn : -1;
        v.winningTeam = winningTeam;

        string value = $"Hand worth {HandValue}" + (Betting.AcceptedIndex > 0 ? $" ({settings.CallName(Betting.AcceptedIndex)})" : "");
        if (Betting.Pending)
            value += $" · {settings.CallName(Betting.PendingIndex)} ({settings.Value(Betting.PendingIndex)}) called by {teamNames[Betting.CallerTeam]}";
        if (Kind == HandKind.HandOfEleven) value += " · HAND OF ELEVEN (no calls)";
        else if (Kind == HandKind.IronHand) value += " · BOTH AT ELEVEN (no calls)";
        v.valueText = value + $" · first to {settings.pointsToWin}";

        for (int i = 0; i < 4 && i < v.seats.Count; i++)
        {
            CardSeatView s = v.seats[i];
            s.team = TeamOf(i);
            s.cardCount = players[i].Hand.Count;
            s.isTurn = State == Phase.Playing && turn == i;
            s.bubble = bubbles[i] ?? "";
            s.revealed.Clear();
            // Hand of eleven: the team at eleven sees its partner's cards while it decides.
            if (State == Phase.ElevenDecision && viewerSeat >= 0 && viewerSeat < 4 && i != viewerSeat &&
                TeamOf(viewerSeat) == ElevenTeam && TeamOf(i) == ElevenTeam)
                foreach (PlayingCard c in players[i].Hand)
                    s.revealed.Add(c.Code);
        }

        v.hand.Clear();
        v.handPlayable.Clear();
        if (viewerSeat >= 0 && viewerSeat < 4)
        {
            bool canPlay = State == Phase.Playing && turn == viewerSeat;
            foreach (PlayingCard c in players[viewerSeat].Hand)
            {
                v.hand.Add(c.Code);
                v.handPlayable.Add(canPlay);
            }
        }

        v.played.Clear();
        int bestSeat = -1, bestS = int.MaxValue;
        foreach ((int seat, PlayingCard card) in Round.Plays)
        {
            int s = rules.Strength(card, manilha);
            if (s < bestS) { bestS = s; bestSeat = seat; }
        }
        bool tied = State == Phase.TrickPause && Round.TricksPlayed > 0 && Round.Results[Round.TricksPlayed - 1] == TrucoRules.Tie;
        foreach ((int seat, PlayingCard card) in Round.Plays)
            v.played.Add(new CardPlayView { seat = seat, card = card.Code, best = !tied && seat == bestSeat });

        v.shared.Clear();
        v.shared.Add(vira.Code);
        v.sharedLabel = $"Vira {vira} · manilhas: {PlayingCard.RankShort(manilha)}";
        v.deckCount = deck != null ? deck.Count : 0;

        v.status = Status(viewerSeat);
        v.actions.Clear();
        if (viewerSeat < 0 || viewerSeat >= 4 || State == Phase.Over)
            return;
        int team = TeamOf(viewerSeat);
        if (State == Phase.ElevenDecision && team == ElevenTeam)
        {
            v.actions.Add(new CardActionOption(new CardAction(PlayEleven), $"Play the hand ({HandValue})", true, "", 1));
            v.actions.Add(new CardActionOption(new CardAction(RunEleven), $"Run (they get {settings.handOfElevenRunValue})", true, "", 2));
        }
        if (State == Phase.Playing && turn == viewerSeat && CanCallNow(team, out _))
        {
            int next = Betting.AcceptedIndex + 1;
            v.actions.Add(new CardActionOption(new CardAction(Call), $"{settings.CallName(next)}! ({settings.Value(next)})", true, "", 1));
        }
        if (State == Phase.Responding && Betting.RespondingTeam == team)
        {
            v.actions.Add(new CardActionOption(new CardAction(Accept), $"Accept ({settings.Value(Betting.PendingIndex)})", true, "", 1));
            if (Betting.PendingIndex < settings.MaxIndex)
            {
                int next = Betting.PendingIndex + 1;
                v.actions.Add(new CardActionOption(new CardAction(Raise), $"{settings.CallName(next)}! ({settings.Value(next)})"));
            }
            v.actions.Add(new CardActionOption(new CardAction(Fold), $"Fold (they get {HandValue})", true, "", 2));
        }
    }

    private string Status(int viewerSeat)
    {
        switch (State)
        {
            case Phase.Over:
                return lastEvent;
            case Phase.Responding:
            {
                string who = Betting.CallerSeat >= 0 ? players[Betting.CallerSeat].Name : teamNames[Betting.CallerTeam];
                bool mine = viewerSeat >= 0 && TeamOf(viewerSeat) == Betting.RespondingTeam;
                return $"{who} calls {settings.CallName(Betting.PendingIndex).ToUpperInvariant()}! " +
                       (mine ? "Accept, raise or fold." : $"Waiting for {teamNames[Betting.RespondingTeam]} to answer.");
            }
            case Phase.TrickPause:
            case Phase.HandPause:
                return lastEvent;
            case Phase.ElevenDecision:
                return viewerSeat >= 0 && TeamOf(viewerSeat) == ElevenTeam
                    ? $"Hand of eleven: you see your partner's cards. Play the hand (worth {HandValue}) or run?"
                    : $"Hand of eleven: {teamNames[ElevenTeam]} is deciding whether to play or run.";
            default:
                return turn == viewerSeat ? "Your turn: play a card" + (CanCallNow(TeamOf(viewerSeat), out _) ? " or call." : ".")
                    : $"{players[turn].Name}'s turn.";
        }
    }
}
