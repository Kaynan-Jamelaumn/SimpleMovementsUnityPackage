using System;
using System.Collections.Generic;

/// <summary>
/// Base of card games where everyone plays for themselves with chips (blackjack, poker). Each player's stake buys
/// <see cref="StartChips"/> chips; at the end they get back their chips' worth (<see cref="Payouts"/>: 2 = doubled the
/// stake, 0 = lost it). Players who leave or go broke are out; the game ends when too few players are left (or the game
/// says so). Every seat is its own "team".
/// </summary>
public abstract class ChipGameSession : CardGameSession
{
    protected readonly int seatCount;
    protected readonly string[] names;
    protected readonly bool[] bots;
    /// <summary>Seats taking part (not empty, not left).</summary>
    protected readonly bool[] inGame;
    protected readonly int[] chips;
    protected readonly int startChips;
    protected readonly System.Random rng;
    private readonly string[] bubbles;
    private readonly float[] bubbleTimes;
    private bool over;
    protected string lastEvent = "";

    protected ChipGameSession(CardGameSetup setup, int seats, int startChips)
    {
        seatCount = seats;
        this.startChips = Math.Max(1, startChips);
        names = new string[seats];
        bots = new bool[seats];
        inGame = new bool[seats];
        chips = new int[seats];
        bubbles = new string[seats];
        bubbleTimes = new float[seats];
        rng = new System.Random(setup.seed);
        for (int i = 0; i < seats; i++)
        {
            names[i] = setup.names != null && i < setup.names.Length && !string.IsNullOrWhiteSpace(setup.names[i]) ? setup.names[i] : $"Seat {i + 1}";
            bots[i] = setup.bots != null && i < setup.bots.Length && setup.bots[i];
            inGame[i] = setup.IsSeated(i);
            chips[i] = inGame[i] ? this.startChips : 0;
            bubbles[i] = "";
        }
    }

    public int StartChips => startChips;
    public int Chips(int seat) => seat >= 0 && seat < seatCount ? chips[seat] : 0;
    public bool InGame(int seat) => seat >= 0 && seat < seatCount && inGame[seat];
    public string NameOf(int seat) => names[seat];

    public override int SeatCount => seatCount;
    public override bool IsOver => over;
    public override int TeamOf(int seat) => seat;

    /// <summary>The richest player at the end (−1 = a tie or still playing).</summary>
    public override int WinningTeam
    {
        get
        {
            if (!over)
                return -1;
            int best = -1, bestChips = -1;
            bool tie = false;
            for (int i = 0; i < seatCount; i++)
            {
                if (chips[i] > bestChips) { best = i; bestChips = chips[i]; tie = false; }
                else if (chips[i] == bestChips) tie = true;
            }
            return tie ? -1 : best;
        }
    }

    /// <summary>Each seat's chips in stakes (chips ÷ starting chips).</summary>
    public override float[] Payouts
    {
        get
        {
            var p = new float[seatCount];
            for (int i = 0; i < seatCount; i++)
                p[i] = chips[i] / (float)startChips;
            return p;
        }
    }

    /// <summary>How many players are still in.</summary>
    protected int PlayersLeft
    {
        get
        {
            int n = 0;
            for (int i = 0; i < seatCount; i++)
                if (inGame[i]) n++;
            return n;
        }
    }

    protected bool AnyHumanLeft
    {
        get
        {
            for (int i = 0; i < seatCount; i++)
                if (inGame[i] && !bots[i]) return true;
            return false;
        }
    }

    /// <summary>A player leaves: their chips are lost (what they bet stays in play), and the game goes on without them.</summary>
    public override void PlayerLeft(int seat, string playerName)
    {
        if (seat < 0 || seat >= seatCount || !inGame[seat] || over)
            return;
        OnLeaving(seat);
        inGame[seat] = false;
        chips[seat] = 0;
        Say($"{playerName} leaves the game.");
        if (!AnyHumanLeft || PlayersLeft < MinPlayersToGoOn)
            End("Not enough players left.");
        else
            AfterLeaving(seat);
    }

    /// <summary>Ends the game: everyone keeps their chips.</summary>
    public override void Forfeit(int team, string why) => End(why);

    /// <summary>Fewest players for the game to go on.</summary>
    protected virtual int MinPlayersToGoOn => 1;

    /// <summary>Before a player is taken out (fold their hand...).</summary>
    protected virtual void OnLeaving(int seat) { }

    /// <summary>After a player was taken out (move the turn on...).</summary>
    protected virtual void AfterLeaving(int seat) { }

    /// <summary>The game ends now (chips still in play, bets on the table, go back to their owners here).</summary>
    protected virtual void BeforeEnd() { }

    protected void End(string why)
    {
        if (over)
            return;
        BeforeEnd();
        over = true;
        var parts = new List<string>();
        for (int i = 0; i < seatCount; i++)
            if (names[i] != null && (inGame[i] || chips[i] > 0))
                parts.Add($"{names[i]} {chips[i]}");
        lastEvent = (string.IsNullOrEmpty(why) ? "" : why + " ") + "Final chips: " + string.Join(", ", parts) + ".";
        Say(lastEvent);
    }

    // ------------------------------------------------------------------ bubbles and the common view
    protected void Bubble(int seat, string text)
    {
        bubbles[seat] = text ?? "";
        bubbleTimes[seat] = 2.5f;
        MarkChanged();
    }

    protected void TickBubbles(float dt)
    {
        for (int i = 0; i < seatCount; i++)
        {
            if (bubbleTimes[i] <= 0f) continue;
            bubbleTimes[i] -= dt;
            if (bubbleTimes[i] <= 0f)
            {
                bubbles[i] = "";
                MarkChanged();
            }
        }
    }

    /// <summary>Names and chips as the "teams", the seats' bubbles and who is out.</summary>
    protected void FillCommon(CardTableView v)
    {
        v.teamNames.Clear();
        v.teamScores.Clear();
        for (int i = 0; i < seatCount; i++)
        {
            bool shown = inGame[i] || chips[i] > 0;
            v.teamNames.Add(shown ? names[i] : "");
            v.teamScores.Add(chips[i]);
        }
        v.winningTeam = WinningTeam;
        for (int i = 0; i < seatCount && i < v.seats.Count; i++)
        {
            CardSeatView s = v.seats[i];
            s.team = i;
            s.bubble = !inGame[i] && s.occupied ? "Out" : bubbles[i] ?? "";
        }
    }

    /// <summary>The next seat after <paramref name="from"/> (clockwise) for which <paramref name="ok"/> holds, or −1.</summary>
    protected int Next(int from, Func<int, bool> ok)
    {
        for (int k = 1; k <= seatCount; k++)
        {
            int s = ((from + k) % seatCount + seatCount) % seatCount;
            if (ok(s)) return s;
        }
        return -1;
    }
}
