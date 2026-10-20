using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The rules of the drinking contest (on the <see cref="DrinkingContestDefinition"/>).</summary>
[Serializable]
public class DrinkingSettings
{
    [Tooltip("How drunk one mug makes an average drinker (0-100; 100 = passed out). Each round's mug is a little stronger.")]
    [Range(1f, 60f)] public float mugStrength = 13f;
    [Tooltip("Each round's mug is this much stronger than the last (0.08 = 8%).")]
    [Range(0f, 0.5f)] public float strongerEachRound = 0.08f;
    [Tooltip("Seconds everyone has to down their mug each round (too slow = out).")]
    [Min(1f)] public float secondsToDrink = 7f;
    [Tooltip("From this drunkenness a mug can come back up (out of the contest). The chance grows to 100% at 100.")]
    [Range(0f, 100f)] public float sickFrom = 70f;
    [Tooltip("From this drunkenness the Drink button has look-alikes (Dirnk, Drnik...): clicking one spills the mug and wastes time.")]
    [Range(0f, 100f)] public float blurryFrom = 35f;
    [Tooltip("Seconds between rounds.")]
    [Min(0f)] public float roundPauseSeconds = 2f;
}

/// <summary>
/// A drinking contest for two to six: every round everyone must down a mug within the time; each mug makes a drinker
/// drunker by its strength over their tolerance (body size, health, a full stomach). Drunk drinkers see look-alike
/// buttons (clicking one spills the mug), and past Sick From a mug may come back up. Passing out (100), being sick, too
/// slow or giving up is out; the last one standing wins. Everyone stays drunk for a while afterwards.
/// </summary>
public sealed class DrinkingContestGame : CardGameSession
{
    public const int Drink = 1;
    /// <summary>A look-alike of Drink (arg = which): spills the mug.</summary>
    public const int Spill = 2;
    public const int GiveUp = 3;

    public enum Phase { Drinking, Pause, Over }

    private static readonly string[] Decoys = { "Dirnk", "Drnik", "Drink?", "Drinkk", "Dink", "Rdink" };

    private readonly DrinkingSettings settings;
    private readonly int seats;
    private readonly string[] names;
    private readonly bool[] bots, inGame, drankThisRound;
    private readonly float[] tolerance, drunk;
    private readonly int[] mugs;
    private readonly string[] bubbles;
    private readonly System.Random rng;
    private float timer;
    private int winner = -1;
    private string lastEvent = "";

    public Phase State { get; private set; } = Phase.Drinking;
    public int Round { get; private set; }
    public float Drunkenness(int seat) => drunk[seat];
    public bool InGame(int seat) => inGame[seat];
    public int Mugs(int seat) => mugs[seat];

    public override int SeatCount => seats;
    public override bool IsOver => State == Phase.Over;
    public override int WinningTeam => winner;
    public override int TeamOf(int seat) => seat;

    public DrinkingContestGame(DrinkingSettings settings, CardGameSetup setup, int seats)
    {
        this.settings = settings ?? new DrinkingSettings();
        this.seats = seats;
        names = new string[seats];
        bots = new bool[seats];
        inGame = new bool[seats];
        drankThisRound = new bool[seats];
        tolerance = new float[seats];
        drunk = new float[seats];
        mugs = new int[seats];
        bubbles = new string[seats];
        rng = new System.Random(setup.seed);
        for (int i = 0; i < seats; i++)
        {
            names[i] = setup.names != null && i < setup.names.Length && !string.IsNullOrWhiteSpace(setup.names[i]) ? setup.names[i] : $"Seat {i + 1}";
            bots[i] = setup.bots != null && i < setup.bots.Length && setup.bots[i];
            inGame[i] = setup.IsSeated(i);
            tolerance[i] = Mathf.Clamp(setup.PowerOf(i), 0.4f, 3f);
            bubbles[i] = "";
        }
    }

    private int Standing
    {
        get
        {
            int n = 0;
            for (int i = 0; i < seats; i++) if (inGame[i]) n++;
            return n;
        }
    }

    public override void Start() => NewRound();

    private void NewRound()
    {
        Round++;
        for (int i = 0; i < seats; i++)
        {
            drankThisRound[i] = false;
            if (inGame[i]) bubbles[i] = "";
        }
        State = Phase.Drinking;
        timer = settings.secondsToDrink;
        lastEvent = $"Round {Round}: drink up!";
        Say(lastEvent);
    }

    public override void Tick(float dt)
    {
        if (State == Phase.Over)
            return;
        int before = Mathf.CeilToInt(timer);
        timer -= dt;
        if (Mathf.CeilToInt(timer) != before)
            MarkChanged();
        if (timer > 0f)
            return;
        if (State == Phase.Pause)
        {
            NewRound();
            return;
        }
        // Time is up: whoever has not finished their mug is out.
        for (int i = 0; i < seats; i++)
            if (inGame[i] && !drankThisRound[i])
                Out(i, "could not finish the mug");
        EndRound();
    }

    public override bool TryAct(int seat, CardAction a, out string reason)
    {
        reason = "";
        if (seat < 0 || seat >= seats || !inGame[seat]) { reason = "You are out"; return false; }
        if (State != Phase.Drinking) { reason = "Wait for the next round"; return false; }
        if (drankThisRound[seat] && a.kind != GiveUp) { reason = "You already drank this round"; return false; }
        switch (a.kind)
        {
            case Drink:
            {
                float strength = settings.mugStrength * (1f + settings.strongerEachRound * (Round - 1));
                float gain = strength / tolerance[seat] * (0.85f + (float)rng.NextDouble() * 0.3f);
                drunk[seat] = Mathf.Min(100f, drunk[seat] + gain);
                mugs[seat]++;
                drankThisRound[seat] = true;
                bubbles[seat] = "*glug*";
                Say($"{names[seat]} downs mug {mugs[seat]} ({drunk[seat]:0}% drunk).");
                if (drunk[seat] >= 100f) Out(seat, "passes out");
                else if (drunk[seat] > settings.sickFrom && rng.NextDouble() < (drunk[seat] - settings.sickFrom) / Mathf.Max(1f, 100f - settings.sickFrom) * 0.5f)
                    Out(seat, "is sick");
                break;
            }
            case Spill:
                bubbles[seat] = "*spills*";
                Say($"{names[seat]} spills the mug and needs a new one.");
                timer = Mathf.Max(0.5f, timer - 1.5f); // a refill costs time for everybody
                break;
            case GiveUp:
                Out(seat, "gives up");
                break;
            default:
                reason = "Unknown action";
                return false;
        }
        bool allDone = true;
        for (int i = 0; i < seats; i++)
            if (inGame[i] && !drankThisRound[i]) allDone = false;
        if (allDone || Standing <= 1)
            EndRound();
        MarkChanged();
        return true;
    }

    private void Out(int seat, string why)
    {
        if (!inGame[seat]) return;
        inGame[seat] = false;
        bubbles[seat] = why == "passes out" ? "Zzz..." : "Out";
        Say($"{names[seat]} {why}!");
    }

    private void EndRound()
    {
        if (State != Phase.Drinking)
            return;
        int standing = Standing;
        if (standing <= 1)
        {
            if (standing == 1)
            {
                for (int i = 0; i < seats; i++)
                    if (inGame[i]) winner = i;
            }
            else
            {
                // Everyone went down in the same round: the one who drank most wins (a tie is a draw).
                int best = -1, bestMugs = -1;
                bool tie = false;
                for (int i = 0; i < seats; i++)
                {
                    if (mugs[i] > bestMugs) { best = i; bestMugs = mugs[i]; tie = false; }
                    else if (mugs[i] == bestMugs) tie = true;
                }
                winner = tie ? -1 : best;
            }
            State = Phase.Over;
            lastEvent = winner >= 0 ? $"{names[winner]} wins the contest after {mugs[winner]} mugs!" : "Nobody is left standing: a draw.";
            Say(lastEvent);
            return;
        }
        State = Phase.Pause;
        timer = settings.roundPauseSeconds;
        MarkChanged();
    }

    public override void Forfeit(int team, string why)
    {
        if (State == Phase.Over)
            return;
        if (team >= 0 && team < seats) Out(team, "leaves the table");
        if (Standing <= 1)
        {
            State = Phase.Drinking; // end now, even between rounds
            EndRound();
            return;
        }
        bool allDone = true;
        for (int i = 0; i < seats; i++)
            if (inGame[i] && !drankThisRound[i]) allDone = false;
        if (State == Phase.Drinking && allDone)
            EndRound();
        MarkChanged();
    }

    public override void PlayerLeft(int seat, string playerName) => Forfeit(seat, "");

    public override bool WantsBotAction(int seat) =>
        seat >= 0 && seat < seats && bots[seat] && inGame[seat] && State == Phase.Drinking && !drankThisRound[seat];

    public override CardAction BotDecide(int seat)
    {
        // Bots sometimes grab a look-alike when drunk, and very drunk ones may give up.
        if (drunk[seat] > settings.blurryFrom && rng.NextDouble() < (drunk[seat] - settings.blurryFrom) / 200f)
            return new CardAction(Spill, 0);
        if (drunk[seat] > 88f && rng.NextDouble() < 0.25)
            return new CardAction(GiveUp);
        return new CardAction(Drink);
    }

    public override void FillView(CardTableView v, int viewerSeat)
    {
        v.teamNames.Clear();
        v.teamScores.Clear();
        for (int i = 0; i < seats; i++)
        {
            bool playing = inGame[i] || mugs[i] > 0;
            v.teamNames.Add(playing ? names[i] : "");
            v.teamScores.Add(mugs[i]);
        }
        v.winningTeam = winner;
        v.valueText = $"Round {Round} · mug strength ×{1f + settings.strongerEachRound * (Round - 1):0.00} · last one standing wins";
        v.meters.Clear();
        if (State == Phase.Drinking)
            v.meters.Add(new CardMeter($"Time to drink: {Mathf.CeilToInt(Mathf.Max(0f, timer))} s", Mathf.Clamp01(timer / settings.secondsToDrink), false, 3));
        for (int i = 0; i < seats && i < v.seats.Count; i++)
        {
            CardSeatView s = v.seats[i];
            s.team = i;
            s.value = drunk[i] / 100f;
            s.isTurn = State == Phase.Drinking && inGame[i] && !drankThisRound[i];
            s.bubble = bubbles[i] ?? "";
            if (inGame[i] || mugs[i] > 0)
                v.meters.Add(new CardMeter($"{names[i]} — {drunk[i]:0}% drunk{(inGame[i] ? "" : " (out)")}", drunk[i] / 100f, false, (byte)(drunk[i] > settings.sickFrom ? 2 : 0)));
        }
        v.status = State == Phase.Over ? lastEvent
            : State == Phase.Pause ? "Next round..."
            : viewerSeat >= 0 && viewerSeat < seats && inGame[viewerSeat] && !drankThisRound[viewerSeat] ? "Drink up!"
            : "Waiting for the others to finish their mugs.";
        v.actions.Clear();
        if (viewerSeat < 0 || viewerSeat >= seats || !inGame[viewerSeat] || State == Phase.Over)
            return;
        bool can = State == Phase.Drinking && !drankThisRound[viewerSeat];
        // The drunker you are, the more the button doubles; the order stays the same for the whole round.
        int decoys = drunk[viewerSeat] < settings.blurryFrom ? 0 : 1 + Mathf.FloorToInt((drunk[viewerSeat] - settings.blurryFrom) / 20f);
        var options = new List<CardActionOption> { new CardActionOption(new CardAction(Drink), "Drink", can, "", 1) };
        var order = new System.Random(Round * 7919 + viewerSeat * 104729);
        for (int d = 0; d < Mathf.Min(decoys, Decoys.Length); d++)
            options.Add(new CardActionOption(new CardAction(Spill, d), Decoys[(d + Round) % Decoys.Length], can, "", 1));
        for (int i = options.Count - 1; i > 0; i--)
        {
            int j = order.Next(i + 1);
            CardActionOption t = options[i];
            options[i] = options[j];
            options[j] = t;
        }
        v.actions.AddRange(options);
        v.actions.Add(new CardActionOption(new CardAction(GiveUp), "Give up", true, "", 2));
    }
}
