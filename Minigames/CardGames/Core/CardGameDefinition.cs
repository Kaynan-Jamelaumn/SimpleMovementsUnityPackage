using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a table gives a new game: who sits where, and the seed every random choice comes from.</summary>
public struct CardGameSetup
{
    /// <summary>Seed of the game's random numbers (shuffles, bots): the same seed plays the same game.</summary>
    public int seed;
    /// <summary>The name of the player at each seat.</summary>
    public string[] names;
    /// <summary>Seats played by the computer.</summary>
    public bool[] bots;
    /// <summary>The seat that deals first.</summary>
    public int firstDealer;
    /// <summary>Seats taking part (null = all). Games that play with empty seats skip the others.</summary>
    public bool[] seated;
    /// <summary>Each seat's strength for games that use it (arm wrestling power, drinking tolerance); 1 = average.</summary>
    public float[] power;

    /// <summary>Does <paramref name="seat"/> take part?</summary>
    public bool IsSeated(int seat) => seated == null || (seat >= 0 && seat < seated.Length && seated[seat]);

    /// <summary>The strength of <paramref name="seat"/> (1 when not given).</summary>
    public float PowerOf(int seat) => power != null && seat >= 0 && seat < power.Length && power[seat] > 0f ? power[seat] : 1f;
}

/// <summary>
/// A kind of card game a <see cref="CardTable"/> can host: Truco today, poker or anything else later. It says how many
/// seats and teams the game has and makes a <see cref="CardGameSession"/> for each game played. Write a new game by
/// deriving from this (the asset) and from <see cref="CardGameSession"/> (the rules); the table, its seats, the stakes,
/// the bots, the UI, the 3D cards and the network work with it unchanged.
/// </summary>
public abstract class CardGameDefinition : ScriptableObject
{
    [Tooltip("Name shown to players (\"Truco\").")]
    [SerializeField] protected string displayName = "Card Game";
    [Tooltip("What the game is, shown in the table's lobby.")]
    [TextArea(2, 5)]
    [SerializeField] protected string description = "";
    [Tooltip("Seconds bots wait before they act (a range: they think a little).")]
    [SerializeField] protected Vector2 botThinkSeconds = new Vector2(0.8f, 1.8f);

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Vector2 BotThinkSeconds => botThinkSeconds;

    /// <summary>Seats at the table (Truco: 4).</summary>
    public abstract int SeatCount { get; }

    /// <summary>Teams (Truco: 2; a free-for-all game: one team per seat).</summary>
    public virtual int TeamCount => 2;

    /// <summary>The team of a seat (Truco: teammates sit opposite each other, seats 0 + 2 and 1 + 3).</summary>
    public virtual int TeamOfSeat(int seat) => seat % TeamCount;

    public virtual string TeamName(int team) => team == 0 ? "Team A" : team == 1 ? "Team B" : $"Team {team + 1}";

    /// <summary>
    /// The game can be played with empty seats (blackjack, poker): the table's bots fill them only with Bots Fill Empty
    /// Seats. False (Truco): every seat must be played, by a player or a bot.
    /// </summary>
    public virtual bool PlaysWithEmptySeats => false;

    /// <summary>Fewest players (and bots) a game needs.</summary>
    public virtual int MinPlayers => SeatCount;

    /// <summary>Can <paramref name="player"/> take part (arm wrestling needs an arm)? Checked when sitting and on Ready.</summary>
    public virtual bool CanPlay(CombatEntity player, out string reason)
    {
        reason = "";
        return true;
    }

    /// <summary>The player's strength for games that use it (arm wrestling power, drinking tolerance). 1 = average.</summary>
    public virtual float PowerOf(CombatEntity player) => 1f;

    /// <summary>A bot's strength (a little random).</summary>
    public virtual float BotPower(System.Random rng) => 0.85f + (float)rng.NextDouble() * 0.3f;

    /// <summary>
    /// After a game, where the rules run: what it does to the real players (a strained arm, drunkenness).
    /// <paramref name="players"/> holds the player of each seat (null for bots and empty seats).
    /// </summary>
    public virtual void AfterGame(CardGameSession session, CombatEntity[] players, System.Random rng, Action<string> log) { }

    /// <summary>A new game for these players.</summary>
    public abstract CardGameSession CreateSession(CardGameSetup setup);

    /// <summary>Problems with the settings, for the inspector and validators.</summary>
    public virtual void Validate(List<string> errors, List<string> warnings)
    {
        if (SeatCount < 2)
            errors.Add($"{DisplayName}: a card game needs at least 2 seats.");
    }
}

/// <summary>
/// One game being played (the rules and the state), on the machine where the game rules run (offline, or the server).
/// It never touches Unity objects: the <see cref="CardTable"/> feeds it the players' actions, ticks its timers, asks it
/// what bots do, and shows what it fills into a <see cref="CardTableView"/>.
/// </summary>
public abstract class CardGameSession
{
    /// <summary>The action kind every game uses for playing a card from the hand (arg = the card's index in the hand).</summary>
    public const int PlayCard = 0;

    /// <summary>Counts every change (the table sends views only when it moved).</summary>
    public int Version { get; private set; }

    /// <summary>A line for the table's log ("Bram plays Q♥").</summary>
    public event Action<string> Message;

    public abstract int SeatCount { get; }
    public abstract bool IsOver { get; }
    /// <summary>The team that won (−1 = none yet, or a draw).</summary>
    public abstract int WinningTeam { get; }
    public abstract int TeamOf(int seat);

    /// <summary>Deals the first hand.</summary>
    public abstract void Start();

    /// <summary>Advances timers (pauses between tricks and hands, speech bubbles).</summary>
    public abstract void Tick(float dt);

    /// <summary>The player at <paramref name="seat"/> does <paramref name="action"/>. False (and why) when not allowed now.</summary>
    public abstract bool TryAct(int seat, CardAction action, out string reason);

    /// <summary>Has the bot at <paramref name="seat"/> something to do now (its turn, a bet to answer)?</summary>
    public abstract bool WantsBotAction(int seat);

    /// <summary>What the bot at <paramref name="seat"/> does (only asked when <see cref="WantsBotAction"/>).</summary>
    public abstract CardAction BotDecide(int seat);

    /// <summary>Ends the game now: <paramref name="team"/> gives up (a player left), the others win.</summary>
    public abstract void Forfeit(int team, string why);

    /// <summary>
    /// The player at <paramref name="seat"/> left during the game. Default: their team forfeits. Games where players
    /// play for themselves (blackjack, poker) take the seat out and go on.
    /// </summary>
    public virtual void PlayerLeft(int seat, string playerName) => Forfeit(TeamOf(seat), $"{playerName} left the table.");

    /// <summary>
    /// Stake units each seat gets back when the game ends (chip games: 2 = doubled the stake, 1 = even, 0 = lost it).
    /// Null (team games) = the winning team shares the pot.
    /// </summary>
    public virtual float[] Payouts => null;

    /// <summary>
    /// The house pays <see cref="Payouts"/> (blackjack: everyone plays against the house). False = the seats share the
    /// stakes put in (poker: what one wins another lost).
    /// </summary>
    public virtual bool PayoutsFromHouse => false;

    /// <summary>Fills the game's part of what <paramref name="viewerSeat"/> sees (−1 = an onlooker: no hand).</summary>
    public abstract void FillView(CardTableView view, int viewerSeat);

    protected void MarkChanged() => Version++;

    protected void Say(string text)
    {
        MarkChanged();
        if (string.IsNullOrEmpty(text))
            return;
        try { Message?.Invoke(text); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
