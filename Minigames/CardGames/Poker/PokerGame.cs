using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The rules of Texas Hold'em at a <see cref="CardTable"/> (on the <see cref="PokerDefinition"/>).</summary>
[Serializable]
public class PokerSettings
{
    [Tooltip("Chips each player's stake buys (what one player wins another lost; at the end everyone gets their chips' worth).")]
    [Min(20)] public int startingChips = 100;
    [Tooltip("The small blind at the start (the big blind is twice it).")]
    [Min(1)] public int smallBlind = 1;
    [Tooltip("The blinds double every this many hands (0 = never).")]
    [Min(0)] public int blindsDoubleEvery = 8;
    [Tooltip("The game ends after this many hands (everyone keeps their chips), or sooner when one player has them all.")]
    [Min(1)] public int maxHands = 30;
    [Tooltip("Seconds the end of a hand stays on the table (the cards shown).")]
    [Min(0f)] public float handPauseSeconds = 4f;
    [Tooltip("Seconds between dealing the flop, turn and river when everyone is all-in.")]
    [Min(0f)] public float runOutSeconds = 1f;
    [Tooltip("How bold bots are (0 = careful, 1 = bluffs and raises a lot).")]
    [Range(0f, 1f)] public float botBoldness = 0.35f;
}

/// <summary>
/// No-limit Texas Hold'em: the blinds, two hole cards each, four betting rounds (pre-flop, flop, turn, river) with
/// fold, check, call, raise and all-in, side pots when players are all-in, and the showdown (the best five of seven
/// cards; split pots on ties). The button moves every hand; busted players are out.
/// </summary>
public sealed class PokerGame : ChipGameSession
{
    public const int Fold = 1;
    public const int Check = 2;
    public const int Call = 3;
    /// <summary>Raise: arg = what the player's bet this round becomes.</summary>
    public const int Raise = 4;
    public const int AllIn = 5;

    public enum Phase { Betting, RunOut, Pause }

    private static readonly string[] StreetNames = { "Pre-flop", "Flop", "Turn", "River", "Showdown" };

    private readonly PokerSettings settings;
    private readonly List<PlayingCard>[] hole;
    private readonly List<PlayingCard> board = new List<PlayingCard>(5);
    private readonly bool[] inHand, folded, allIn, acted;
    private readonly int[] streetBet, committed;
    private readonly int[] shownScore;
    private CardDeck deck;
    private int button = -1, turn = -1, street;
    private int currentBet, minRaise;
    private float timer;
    private bool showdown;

    public Phase State { get; private set; } = Phase.Pause;
    public int HandNumber { get; private set; }
    public int Turn => turn;
    public int Button => button;
    public int Street => street;
    public IReadOnlyList<PlayingCard> Board => board;
    public IReadOnlyList<PlayingCard> Hole(int seat) => hole[seat];
    public bool Folded(int seat) => folded[seat];
    public int Pot { get { int p = 0; foreach (int c in committed) p += c; return p; } }
    public int CurrentBet => currentBet;
    public int StreetBet(int seat) => streetBet[seat];
    public int ToCall(int seat) => Math.Max(0, currentBet - streetBet[seat]);
    public int SmallBlind => settings.smallBlind * (settings.blindsDoubleEvery > 0 ? 1 << Math.Min(10, (HandNumber - 1) / settings.blindsDoubleEvery) : 1);
    public int BigBlind => SmallBlind * 2;
    public PokerSettings Settings => settings;

    protected override int MinPlayersToGoOn => 2;

    public PokerGame(PokerSettings settings, CardGameSetup setup, int seats)
        : base(setup, seats, (settings ?? new PokerSettings()).startingChips)
    {
        this.settings = settings ?? new PokerSettings();
        hole = new List<PlayingCard>[seats];
        for (int i = 0; i < seats; i++) hole[i] = new List<PlayingCard>(2);
        inHand = new bool[seats];
        folded = new bool[seats];
        allIn = new bool[seats];
        acted = new bool[seats];
        streetBet = new int[seats];
        committed = new int[seats];
        shownScore = new int[seats];
        button = setup.firstDealer - 1;
    }

    // ------------------------------------------------------------------ hands
    public override void Start() => NewHand();

    private int WithChips()
    {
        int n = 0;
        for (int i = 0; i < seatCount; i++)
            if (inGame[i] && chips[i] > 0) n++;
        return n;
    }

    private void NewHand()
    {
        if (WithChips() < 2) { End(WithChips() == 1 ? "One player has all the chips." : ""); return; }
        if (HandNumber >= settings.maxHands) { End($"{settings.maxHands} hands played."); return; }
        HandNumber++;
        deck = CardDeck.Standard();
        deck.Shuffle(rng);
        board.Clear();
        showdown = false;
        for (int i = 0; i < seatCount; i++)
        {
            hole[i].Clear();
            inHand[i] = inGame[i] && chips[i] > 0;
            folded[i] = allIn[i] = acted[i] = false;
            streetBet[i] = committed[i] = 0;
            shownScore[i] = -1;
        }
        button = Next(button, s => inHand[s]);
        int players = WithChips();
        int sb = players == 2 ? button : Next(button, s => inHand[s]);
        int bb = Next(sb, s => inHand[s]);
        Commit(sb, SmallBlind);
        Commit(bb, BigBlind);
        Bubble(sb, $"Small blind {streetBet[sb]}");
        Bubble(bb, $"Big blind {streetBet[bb]}");
        currentBet = Math.Max(streetBet[sb], streetBet[bb]);
        minRaise = BigBlind;
        for (int c = 0; c < 2; c++)
            for (int k = 1; k <= seatCount; k++)
            {
                int s = (button + k) % seatCount;
                if (inHand[s]) hole[s].Add(deck.Draw());
            }
        street = 0;
        State = Phase.Betting;
        lastEvent = $"Hand {HandNumber}: blinds {SmallBlind}/{BigBlind}, {names[button]} has the button.";
        Say(lastEvent);
        turn = Next(bb, CanAct);
        if (turn < 0) EndBettingRound();
    }

    private bool CanAct(int s) => inHand[s] && !folded[s] && !allIn[s] && inGame[s];

    private int NotFolded()
    {
        int n = 0;
        for (int i = 0; i < seatCount; i++)
            if (inHand[i] && !folded[i]) n++;
        return n;
    }

    private void Commit(int seat, int amount)
    {
        amount = Math.Max(0, Math.Min(amount, chips[seat]));
        chips[seat] -= amount;
        streetBet[seat] += amount;
        committed[seat] += amount;
        if (chips[seat] == 0 && inHand[seat])
            allIn[seat] = true;
    }

    public override void Tick(float dt)
    {
        TickBubbles(dt);
        if (IsOver || State == Phase.Betting)
            return;
        timer -= dt;
        if (timer > 0f)
            return;
        if (State == Phase.RunOut)
            NextStreet();
        else
            NewHand();
    }

    public override bool TryAct(int seat, CardAction a, out string reason)
    {
        reason = "";
        if (IsOver) { reason = "The game is over"; return false; }
        if (State != Phase.Betting || seat != turn) { reason = "Not your turn"; return false; }
        int toCall = ToCall(seat);
        switch (a.kind)
        {
            case Fold:
                folded[seat] = true;
                Bubble(seat, "Fold");
                Say($"{names[seat]} folds.");
                break;
            case Check:
                if (toCall > 0) { reason = $"You must call {toCall}, raise or fold"; return false; }
                Bubble(seat, "Check");
                Say($"{names[seat]} checks.");
                break;
            case Call:
                if (toCall <= 0) { reason = "Nothing to call: check"; return false; }
                Commit(seat, toCall);
                Bubble(seat, allIn[seat] ? "All-in!" : $"Call {toCall}");
                Say($"{names[seat]} calls {Math.Min(toCall, streetBet[seat])}{(allIn[seat] ? " (all-in)" : "")}.");
                break;
            case Raise:
            case AllIn:
            {
                int target = a.kind == AllIn ? streetBet[seat] + chips[seat] : a.arg;
                int max = streetBet[seat] + chips[seat];
                if (target > max) target = max;
                if (target <= currentBet) { reason = "That is not a raise"; return false; }
                if (target < currentBet + minRaise && target < max) { reason = $"Raise to at least {currentBet + minRaise}"; return false; }
                int raisedBy = target - currentBet;
                Commit(seat, target - streetBet[seat]);
                if (raisedBy >= minRaise) minRaise = raisedBy;
                currentBet = streetBet[seat];
                for (int i = 0; i < seatCount; i++)
                    if (i != seat) acted[i] = false;
                Bubble(seat, allIn[seat] ? "All-in!" : $"Raise to {currentBet}");
                Say($"{names[seat]} {(allIn[seat] ? "goes all-in with" : "raises to")} {currentBet}.");
                break;
            }
            default:
                reason = "Unknown action";
                return false;
        }
        acted[seat] = true;
        Advance(seat);
        return true;
    }

    private void Advance(int from)
    {
        if (NotFolded() <= 1)
        {
            AwardUncontested();
            return;
        }
        bool roundDone = true;
        for (int i = 0; i < seatCount; i++)
            if (CanAct(i) && (!acted[i] || streetBet[i] < currentBet)) { roundDone = false; break; }
        if (roundDone)
        {
            EndBettingRound();
            return;
        }
        turn = Next(from, s => CanAct(s) && (!acted[s] || streetBet[s] < currentBet));
        MarkChanged();
    }

    private void EndBettingRound()
    {
        turn = -1;
        int canAct = 0;
        for (int i = 0; i < seatCount; i++)
            if (CanAct(i)) canAct++;
        if (canAct <= 1 && street < 4)
        {
            // Everyone (or all but one) is all-in: deal the rest of the board one card at a time.
            State = Phase.RunOut;
            timer = settings.runOutSeconds;
            MarkChanged();
            if (timer <= 0f) NextStreet();
            return;
        }
        NextStreet();
    }

    private void NextStreet()
    {
        street++;
        for (int i = 0; i < seatCount; i++)
        {
            streetBet[i] = 0;
            acted[i] = false;
        }
        currentBet = 0;
        minRaise = BigBlind;
        if (street == 1) { board.Add(deck.Draw()); board.Add(deck.Draw()); board.Add(deck.Draw()); }
        else if (street == 2 || street == 3) board.Add(deck.Draw());
        if (street >= 4)
        {
            Showdown();
            return;
        }
        Say($"{StreetNames[street]}: {string.Join(" ", board)}.");
        int canAct = 0;
        for (int i = 0; i < seatCount; i++)
            if (CanAct(i)) canAct++;
        if (canAct <= 1)
        {
            State = Phase.RunOut;
            timer = settings.runOutSeconds;
            MarkChanged();
            if (timer <= 0f) NextStreet();
            return;
        }
        State = Phase.Betting;
        turn = Next(button, CanAct);
        MarkChanged();
    }

    private void AwardUncontested()
    {
        int winner = -1;
        for (int i = 0; i < seatCount; i++)
            if (inHand[i] && !folded[i]) winner = i;
        int pot = Pot;
        if (winner >= 0)
        {
            chips[winner] += pot;
            Bubble(winner, $"+{pot}");
            lastEvent = $"{names[winner]} wins {pot} (everyone else folded).";
            Say(lastEvent);
        }
        ClearCommitted();
        EndHand();
    }

    /// <summary>Shows the hands and pays every pot (side pots when players went all-in for less).</summary>
    private void Showdown()
    {
        showdown = true;
        var all = new List<PlayingCard>(7);
        for (int i = 0; i < seatCount; i++)
        {
            if (!inHand[i] || folded[i]) continue;
            all.Clear();
            all.AddRange(hole[i]);
            all.AddRange(board);
            shownScore[i] = PokerHands.Best(all);
        }
        var levels = new List<int>();
        for (int i = 0; i < seatCount; i++)
            if (committed[i] > 0 && !levels.Contains(committed[i])) levels.Add(committed[i]);
        levels.Sort();
        int prev = 0;
        var won = new int[seatCount];
        foreach (int level in levels)
        {
            int slice = 0;
            for (int i = 0; i < seatCount; i++)
                slice += Math.Max(0, Math.Min(committed[i], level) - prev);
            var eligible = new List<int>();
            int best = -1;
            for (int i = 0; i < seatCount; i++)
            {
                if (!inHand[i] || folded[i] || committed[i] < level) continue;
                if (shownScore[i] > best) { best = shownScore[i]; eligible.Clear(); }
                if (shownScore[i] == best) eligible.Add(i);
            }
            if (eligible.Count == 0)
            {
                // Nobody still in reached this level (they folded): it goes to the best hand still in.
                for (int i = 0; i < seatCount; i++)
                {
                    if (!inHand[i] || folded[i]) continue;
                    if (shownScore[i] > best) { best = shownScore[i]; eligible.Clear(); }
                    if (shownScore[i] == best) eligible.Add(i);
                }
            }
            if (eligible.Count > 0)
            {
                int share = slice / eligible.Count, rest = slice % eligible.Count;
                for (int k = 0; k < eligible.Count; k++)
                    won[eligible[k]] += share + (k == 0 ? rest : 0);
            }
            prev = level;
        }
        var lines = new List<string>();
        for (int i = 0; i < seatCount; i++)
        {
            if (won[i] <= 0) continue;
            chips[i] += won[i];
            Bubble(i, $"+{won[i]}");
            lines.Add($"{names[i]} wins {won[i]} with {PokerHands.Name(shownScore[i])}");
        }
        lastEvent = string.Join("; ", lines) + ".";
        Say(lastEvent);
        ClearCommitted();
        EndHand();
    }

    private void ClearCommitted()
    {
        for (int i = 0; i < seatCount; i++)
            committed[i] = streetBet[i] = 0;
        currentBet = 0;
    }

    private void EndHand()
    {
        turn = -1;
        for (int i = 0; i < seatCount; i++)
            if (inGame[i] && chips[i] <= 0)
            {
                Say($"{names[i]} is out of chips.");
                inGame[i] = false;
            }
        if (WithChips() < 2 || !AnyHumanLeft)
        {
            State = Phase.Pause;
            End(WithChips() == 1 ? "One player has all the chips." : "");
            return;
        }
        State = Phase.Pause;
        timer = settings.handPauseSeconds;
        MarkChanged();
        if (timer <= 0f) NewHand();
    }

    protected override void BeforeEnd()
    {
        // Ended in the middle of a hand: every bet goes back to whoever is still in the game.
        for (int i = 0; i < seatCount; i++)
            if (inGame[i]) chips[i] += committed[i];
        ClearCommitted();
    }

    protected override void OnLeaving(int seat)
    {
        if (inHand[seat]) folded[seat] = true; // what they bet stays in the pot
    }

    protected override void AfterLeaving(int seat)
    {
        if (State == Phase.Betting && (turn == seat || NotFolded() <= 1))
            Advance(seat);
    }

    // ------------------------------------------------------------------ bots
    public override bool WantsBotAction(int seat) =>
        !IsOver && State == Phase.Betting && seat == turn && seat >= 0 && bots[seat] && inGame[seat];

    public override CardAction BotDecide(int seat)
    {
        float strength = BotStrength(seat);
        float bold = settings.botBoldness;
        int toCall = ToCall(seat);
        int pot = Pot;
        float r = (float)rng.NextDouble();
        bool bluff = r < bold * 0.12f;
        if (strength > 0.78f - bold * 0.1f || bluff)
        {
            int target = currentBet + Math.Max(minRaise, pot / 2);
            if (strength > 0.95f && r < 0.3f) return new CardAction(AllIn);
            if (target < streetBet[seat] + chips[seat]) return new CardAction(Raise, target);
            return new CardAction(toCall > 0 ? Call : Check);
        }
        if (toCall == 0)
            return new CardAction(Check);
        float price = toCall / (float)Math.Max(1, pot + toCall);
        if (strength > 0.45f || strength > price + 0.15f || toCall <= BigBlind && strength > 0.3f)
            return new CardAction(Call);
        return new CardAction(Fold);
    }

    private float BotStrength(int seat)
    {
        if (hole[seat].Count < 2) return 0f;
        if (board.Count == 0) return PokerHands.Preflop(hole[seat][0], hole[seat][1]);
        var all = new List<PlayingCard>(hole[seat]);
        all.AddRange(board);
        int cat = PokerHands.Category(PokerHands.Best(all));
        float[] byCategory = { 0.18f, 0.5f, 0.66f, 0.76f, 0.83f, 0.87f, 0.93f, 0.98f, 1f };
        return byCategory[Mathf.Clamp(cat, 0, byCategory.Length - 1)];
    }

    // ------------------------------------------------------------------ view
    public override void FillView(CardTableView v, int viewerSeat)
    {
        FillCommon(v);
        v.turnSeat = State == Phase.Betting ? turn : -1;
        for (int i = 0; i < seatCount && i < v.seats.Count; i++)
        {
            CardSeatView s = v.seats[i];
            s.isTurn = i == v.turnSeat;
            s.cardCount = inHand[i] && !folded[i] && !(showdown && shownScore[i] >= 0) ? hole[i].Count : 0;
            if (i == button && string.IsNullOrEmpty(s.bubble)) s.bubble = "Button";
            if (streetBet[i] > 0 && string.IsNullOrEmpty(s.bubble)) s.bubble = $"Bet {streetBet[i]}";
        }
        v.hand.Clear();
        v.handPlayable.Clear();
        if (viewerSeat >= 0 && viewerSeat < seatCount && !(showdown && shownScore[viewerSeat] >= 0))
            foreach (PlayingCard c in hole[viewerSeat])
            {
                v.hand.Add(c.Code);
                v.handPlayable.Add(false);
            }
        v.played.Clear();
        if (showdown)
        {
            int best = -1;
            for (int i = 0; i < seatCount; i++) best = Math.Max(best, shownScore[i]);
            for (int i = 0; i < seatCount; i++)
                if (shownScore[i] >= 0)
                    foreach (PlayingCard c in hole[i])
                        v.played.Add(new CardPlayView { seat = i, card = c.Code, best = shownScore[i] == best });
        }
        v.shared.Clear();
        foreach (PlayingCard c in board) v.shared.Add(c.Code);
        v.sharedLabel = $"{StreetNames[Mathf.Clamp(street, 0, 4)]} · pot {Pot}" + (showdown ? "" : currentBet > 0 ? $" · bet {currentBet}" : "");
        v.deckCount = deck != null ? deck.Count : 0;
        v.valueText = $"Hand {HandNumber} of {settings.maxHands} · blinds {SmallBlind}/{BigBlind}";
        if (viewerSeat >= 0 && viewerSeat < seatCount && hole[viewerSeat].Count == 2)
        {
            var mine = new List<PlayingCard>(hole[viewerSeat]);
            mine.AddRange(board);
            if (mine.Count >= 5) v.valueText += $" · you have {PokerHands.Name(PokerHands.Best(mine))}";
        }
        v.status = Status(viewerSeat);
        v.actions.Clear();
        if (IsOver || State != Phase.Betting || viewerSeat != turn || viewerSeat < 0)
            return;
        int toCall = ToCall(viewerSeat);
        int max = streetBet[viewerSeat] + chips[viewerSeat];
        v.actions.Add(new CardActionOption(new CardAction(Fold), "Fold", true, "", 2));
        if (toCall == 0) v.actions.Add(new CardActionOption(new CardAction(Check), "Check", true, "", 1));
        else v.actions.Add(new CardActionOption(new CardAction(Call), toCall >= chips[viewerSeat] ? $"Call all-in ({chips[viewerSeat]})" : $"Call {toCall}", true, "", 1));
        int minTarget = currentBet + minRaise;
        if (max > currentBet)
        {
            if (minTarget < max)
                v.actions.Add(new CardActionOption(new CardAction(Raise, minTarget), currentBet == 0 ? $"Bet {minTarget}" : $"Raise to {minTarget}"));
            int half = currentBet + Math.Max(minRaise, Pot / 2);
            if (half > minTarget && half < max)
                v.actions.Add(new CardActionOption(new CardAction(Raise, half), $"Raise ½ pot ({half})"));
            int full = currentBet + Math.Max(minRaise, Pot + toCall);
            if (full > half && full < max)
                v.actions.Add(new CardActionOption(new CardAction(Raise, full), $"Raise pot ({full})"));
            v.actions.Add(new CardActionOption(new CardAction(AllIn), $"All-in ({chips[viewerSeat]})"));
        }
    }

    private string Status(int viewer)
    {
        if (IsOver || State == Phase.Pause) return lastEvent;
        if (State == Phase.RunOut) return "All-in: the rest of the board is dealt.";
        if (turn == viewer) return ToCall(viewer) > 0 ? $"Your turn: {ToCall(viewer)} to call." : "Your turn: check or bet.";
        return turn >= 0 ? $"{names[turn]} is thinking." : "";
    }
}

