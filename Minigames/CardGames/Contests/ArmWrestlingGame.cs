using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The rules of arm wrestling (on the <see cref="ArmWrestlingDefinition"/>).</summary>
[Serializable]
public class ArmWrestlingSettings
{
    [Tooltip("Rounds needed to win the match (2 = best of three).")]
    [Min(1)] public int roundsToWin = 2;
    [Tooltip("How far one push moves the arms (the arms go from the middle to the table at 1).")]
    [Range(0.01f, 0.3f)] public float pushStrength = 0.05f;
    [Tooltip("Least seconds between two pushes of one player (everyone mashes at the same speed at most).")]
    [Range(0.05f, 1f)] public float pushCooldown = 0.15f;
    [Tooltip("How fast the stronger arm wins ground by itself (per second, per point of strength difference).")]
    [Min(0f)] public float strengthDrift = 0.35f;
    [Tooltip("Stamina a push costs (0-100). A tired arm pushes weaker.")]
    [Min(0f)] public float staminaPerPush = 5f;
    [Tooltip("Stamina recovered per second.")]
    [Min(0f)] public float staminaRegen = 10f;
    [Tooltip("Grit: a big push that costs this much stamina.")]
    [Min(0f)] public float gritCost = 40f;
    [Tooltip("How far a Grit push moves the arms.")]
    [Range(0.05f, 0.6f)] public float gritStrength = 0.2f;
    [Tooltip("Seconds of the countdown before each round.")]
    [Min(0f)] public float countdownSeconds = 3f;
    [Tooltip("Seconds between rounds.")]
    [Min(0f)] public float roundPauseSeconds = 2.5f;

    [Header("Injuries (when the injury system is on)")]
    [Tooltip("Chance the loser's arm is strained (a cracked bone) after the match.")]
    [Range(0f, 1f)] public float loserStrainChance = 0.2f;
    [Tooltip("Injury type the strain causes (Injury Settings id).")]
    public string strainInjury = "Fracture";
}

/// <summary>
/// Arm wrestling for two: after a countdown both players mash Push (at most one push every Push Cooldown, so everybody
/// is equally fast); each push moves the arms by the player's strength (body size, health, stamina, and the arm's
/// injuries) and costs stamina, which makes later pushes weaker; Grit is a big push for a lot of stamina. The stronger
/// arm also wins ground by itself. Pinning the other arm to the table wins the round; best of three wins the match.
/// </summary>
public sealed class ArmWrestlingGame : CardGameSession
{
    public const int Push = 1;
    public const int Grit = 2;

    public enum Phase { Countdown, Pulling, Pause, Over }

    private readonly ArmWrestlingSettings settings;
    private readonly string[] names = new string[2];
    private readonly bool[] bots = new bool[2];
    private readonly float[] power = new float[2];
    private readonly float[] stamina = { 100f, 100f };
    private readonly float[] lastPush = { -10f, -10f };
    private readonly int[] wins = new int[2];
    private readonly System.Random rng;
    private float position; // +1 = seat 1's arm on the table (seat 0 wins the round), −1 = seat 0's
    private float clock, timer;
    private int winner = -1;
    private string lastEvent = "";

    public Phase State { get; private set; } = Phase.Countdown;
    public float Position => position;
    public int Wins(int seat) => wins[seat];
    public float Stamina(int seat) => stamina[seat];
    public float Power(int seat) => power[seat];

    public override int SeatCount => 2;
    public override bool IsOver => State == Phase.Over;
    public override int WinningTeam => winner;
    public override int TeamOf(int seat) => seat;

    public ArmWrestlingGame(ArmWrestlingSettings settings, CardGameSetup setup)
    {
        this.settings = settings ?? new ArmWrestlingSettings();
        rng = new System.Random(setup.seed);
        for (int i = 0; i < 2; i++)
        {
            names[i] = setup.names != null && i < setup.names.Length && !string.IsNullOrWhiteSpace(setup.names[i]) ? setup.names[i] : $"Player {i + 1}";
            bots[i] = setup.bots != null && i < setup.bots.Length && setup.bots[i];
            power[i] = Mathf.Clamp(setup.PowerOf(i), 0.2f, 3f);
        }
    }

    public override void Start() => NewRound();

    private void NewRound()
    {
        position = 0f;
        stamina[0] = stamina[1] = 100f;
        State = Phase.Countdown;
        timer = settings.countdownSeconds;
        lastEvent = $"Round {wins[0] + wins[1] + 1}: grip...";
        Say(lastEvent);
    }

    public override void Tick(float dt)
    {
        clock += dt;
        switch (State)
        {
            case Phase.Countdown:
            {
                int before = Mathf.CeilToInt(timer);
                timer -= dt;
                if (timer <= 0f)
                {
                    State = Phase.Pulling;
                    Say("GO!");
                }
                else if (Mathf.CeilToInt(timer) != before)
                    MarkChanged();
                break;
            }
            case Phase.Pulling:
                for (int i = 0; i < 2; i++)
                    stamina[i] = Mathf.Min(100f, stamina[i] + settings.staminaRegen * dt);
                position += (Force(0) - Force(1)) * settings.strengthDrift * dt;
                CheckPin();
                MarkChanged();
                break;
            case Phase.Pause:
                timer -= dt;
                if (timer <= 0f) NewRound();
                break;
        }
    }

    /// <summary>The strength behind a seat's arm now (its power, weaker when tired).</summary>
    private float Force(int seat) => power[seat] * (0.45f + 0.55f * stamina[seat] / 100f);

    public override bool TryAct(int seat, CardAction a, out string reason)
    {
        reason = "";
        if (seat < 0 || seat > 1) { reason = "Not wrestling"; return false; }
        if (State != Phase.Pulling) { reason = State == Phase.Countdown ? "Wait for GO!" : "Wait"; return false; }
        if (clock - lastPush[seat] < settings.pushCooldown) { reason = "Too fast"; return false; }
        float sign = seat == 0 ? 1f : -1f;
        if (a.kind == Grit)
        {
            if (stamina[seat] < settings.gritCost) { reason = "Not enough stamina"; return false; }
            stamina[seat] -= settings.gritCost;
            position += sign * settings.gritStrength * Force(seat);
            Say($"{names[seat]} grits their teeth!");
        }
        else if (a.kind == Push)
        {
            float tired = stamina[seat] < settings.staminaPerPush ? 0.3f : 1f;
            stamina[seat] = Mathf.Max(0f, stamina[seat] - settings.staminaPerPush);
            position += sign * settings.pushStrength * Force(seat) * tired;
        }
        else
        {
            reason = "Unknown action";
            return false;
        }
        lastPush[seat] = clock;
        CheckPin();
        MarkChanged();
        return true;
    }

    private void CheckPin()
    {
        if (State != Phase.Pulling || Mathf.Abs(position) < 1f)
            return;
        int roundWinner = position >= 1f ? 0 : 1;
        position = Mathf.Clamp(position, -1f, 1f);
        wins[roundWinner]++;
        lastEvent = $"{names[roundWinner]} pins {names[1 - roundWinner]}'s arm! ({wins[0]} - {wins[1]})";
        Say(lastEvent);
        if (wins[roundWinner] >= settings.roundsToWin)
        {
            winner = roundWinner;
            State = Phase.Over;
            lastEvent = $"{names[roundWinner]} wins the match {wins[roundWinner]} - {wins[1 - roundWinner]}!";
            Say(lastEvent);
            return;
        }
        State = Phase.Pause;
        timer = settings.roundPauseSeconds;
    }

    public override void Forfeit(int team, string why)
    {
        if (State == Phase.Over) return;
        winner = team == 0 ? 1 : 0;
        State = Phase.Over;
        lastEvent = $"{why} {names[winner]} wins.";
        Say(lastEvent);
    }

    public override bool WantsBotAction(int seat) =>
        seat >= 0 && seat < 2 && bots[seat] && State == Phase.Pulling && clock - lastPush[seat] >= settings.pushCooldown;

    public override CardAction BotDecide(int seat)
    {
        bool losing = seat == 0 ? position < -0.4f : position > 0.4f;
        if (losing && stamina[seat] >= settings.gritCost + 10f && rng.NextDouble() < 0.25)
            return new CardAction(Grit);
        return new CardAction(Push);
    }

    public override void FillView(CardTableView v, int viewerSeat)
    {
        v.teamNames.Clear();
        v.teamScores.Clear();
        v.teamNames.AddRange(names);
        v.teamScores.Add(wins[0]);
        v.teamScores.Add(wins[1]);
        v.winningTeam = winner;
        v.valueText = $"Best of {settings.roundsToWin * 2 - 1} · mash Push (Grit: a big push for {settings.gritCost:0} stamina)";
        for (int i = 0; i < 2 && i < v.seats.Count; i++)
        {
            v.seats[i].team = i;
            v.seats[i].value = i == 0 ? position : -position;
            v.seats[i].isTurn = State == Phase.Pulling;
        }
        // The arms from the viewer's side: their own arm pushes to the right.
        bool flip = viewerSeat == 1;
        int left = flip ? 0 : 1, right = flip ? 1 : 0;
        v.meters.Clear();
        v.meters.Add(new CardMeter($"{names[left]}   ◀  arms  ▶   {names[right]}", flip ? -position : position, true, 3));
        v.meters.Add(new CardMeter($"{names[right]} — stamina {stamina[right]:0}", stamina[right] / 100f, false, 1));
        v.meters.Add(new CardMeter($"{names[left]} — stamina {stamina[left]:0}", stamina[left] / 100f, false, 2));
        v.status = State == Phase.Countdown ? $"{Mathf.CeilToInt(timer)}..." : State == Phase.Pulling ? "PUSH!" : lastEvent;
        v.actions.Clear();
        if (viewerSeat < 0 || viewerSeat > 1 || State == Phase.Over)
            return;
        v.actions.Add(new CardActionOption(new CardAction(Push), "PUSH!", State == Phase.Pulling, "Wait for GO!", 1));
        v.actions.Add(new CardActionOption(new CardAction(Grit), $"Grit ({settings.gritCost:0} stamina)",
            State == Phase.Pulling && stamina[viewerSeat] >= settings.gritCost, "Not enough stamina"));
    }
}
