using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Truco for a <see cref="CardTable"/>: four seats, two teams of two sitting opposite each other (seats 1 + 3 and 2 + 4),
/// first to 12 points. Create it with Assets ▸ Create ▸ SimpleMovements ▸ Minigames ▸ Truco (or Tools ▸ SimpleMovements ▸
/// Minigames ▸ Create Truco Table); a table without a game uses these defaults.
/// </summary>
[CreateAssetMenu(fileName = "Truco", menuName = "SimpleMovements/Minigames/Truco", order = 1)]
public class TrucoGameDefinition : CardGameDefinition
{
    [Tooltip("Name of the team at seats 1 and 3.")]
    [SerializeField] private string teamAName = "Team A";
    [Tooltip("Name of the team at seats 2 and 4.")]
    [SerializeField] private string teamBName = "Team B";
    [SerializeField] private TrucoSettings rules = new TrucoSettings();

    public TrucoSettings Rules => rules;

    public override int SeatCount => 4;
    public override int TeamCount => 2;
    public override int TeamOfSeat(int seat) => seat % 2;
    public override string TeamName(int team) => team == 0 ? teamAName : teamBName;

    public override CardGameSession CreateSession(CardGameSetup setup) => new TrucoGame(rules, setup, teamAName, teamBName);

    public override void Validate(List<string> errors, List<string> warnings)
    {
        base.Validate(errors, warnings);
        rules.Validate(errors, warnings);
    }

    /// <summary>A Truco definition with the default rules, made in memory (tables without a game asset).</summary>
    public static TrucoGameDefinition CreateDefault()
    {
        var d = CreateInstance<TrucoGameDefinition>();
        d.name = "Truco (built-in)";
        d.hideFlags = HideFlags.DontSave;
        d.displayName = "Truco";
        d.description = DefaultDescription;
        return d;
    }

    private const string DefaultDescription =
        "Two teams of two; teammates sit opposite each other. Three cards each, the vira decides the manilhas (the next " +
        "rank), the strongest card wins the trick and two tricks win the hand. Call Truco (3), Six, Nine or Twelve to raise " +
        "the stakes: the other team accepts, raises or folds. First team to 12 points wins.";

    private void Reset()
    {
        displayName = "Truco";
        description = DefaultDescription;
    }
}
