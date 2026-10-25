using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Texas Hold'em for a <see cref="CardTable"/> (2-8 seats). Create it with Assets ▸ Create ▸ SimpleMovements ▸
/// Minigames ▸ Poker, or Tools ▸ SimpleMovements ▸ Minigames ▸ Create Poker Table. Empty seats stay empty (unless the
/// table's bots fill them); at least two players are needed.
/// </summary>
[CreateAssetMenu(fileName = "Poker", menuName = "SimpleMovements/Minigames/Poker", order = 3)]
public class PokerDefinition : CardGameDefinition
{
    [Tooltip("Seats at the table.")]
    [Range(2, 8)] [SerializeField] private int seats = 6;
    [SerializeField] private PokerSettings rules = new PokerSettings();

    public PokerSettings Rules => rules;
    public override int SeatCount => seats;
    public override int TeamCount => seats;
    public override int TeamOfSeat(int seat) => seat;
    public override string TeamName(int team) => $"Seat {team + 1}";
    public override bool PlaysWithEmptySeats => true;
    public override int MinPlayers => 2;

    public override CardGameSession CreateSession(CardGameSetup setup) => new PokerGame(rules, setup, seats);

    public static PokerDefinition CreateDefault()
    {
        var d = CreateInstance<PokerDefinition>();
        d.name = "Poker (built-in)";
        d.hideFlags = HideFlags.DontSave;
        d.Reset();
        return d;
    }

    private void Reset()
    {
        displayName = "Texas Hold'em";
        description = "Two cards each and five on the board: make the best five-card hand. Bet, call, raise or fold over four " +
                      "rounds; the blinds rise. Your stake buys chips; you leave with what your chips are worth.";
        botThinkSeconds = new Vector2(0.9f, 2f);
    }
}
