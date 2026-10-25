using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Blackjack for a <see cref="CardTable"/>: up to four players (or the table's seats) against the house dealer. Create
/// it with Assets ▸ Create ▸ SimpleMovements ▸ Minigames ▸ Blackjack, or Tools ▸ SimpleMovements ▸ Minigames ▸ Create
/// Blackjack Table. Empty seats stay empty (unless the table's bots fill them).
/// </summary>
[CreateAssetMenu(fileName = "Blackjack", menuName = "SimpleMovements/Minigames/Blackjack", order = 2)]
public class BlackjackDefinition : CardGameDefinition
{
    [Tooltip("Seats at the table.")]
    [Range(1, 7)] [SerializeField] private int seats = 4;
    [SerializeField] private BlackjackSettings rules = new BlackjackSettings();

    public BlackjackSettings Rules => rules;
    public override int SeatCount => seats;
    public override int TeamCount => seats;
    public override int TeamOfSeat(int seat) => seat;
    public override string TeamName(int team) => $"Seat {team + 1}";
    public override bool PlaysWithEmptySeats => true;
    public override int MinPlayers => 1;

    public override CardGameSession CreateSession(CardGameSetup setup) => new BlackjackGame(rules, setup, seats);

    public static BlackjackDefinition CreateDefault()
    {
        var d = CreateInstance<BlackjackDefinition>();
        d.name = "Blackjack (built-in)";
        d.hideFlags = HideFlags.DontSave;
        d.Reset();
        return d;
    }

    private void Reset()
    {
        displayName = "Blackjack";
        description = "Play against the dealer: get closer to 21 than the dealer without going over. Hit, stand or double; " +
                      "the dealer draws to 17. A blackjack pays 3:2. Your stake buys chips; you get back what your chips are worth.";
        botThinkSeconds = new Vector2(0.6f, 1.3f);
    }
}
