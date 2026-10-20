using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A drinking contest for a <see cref="CardTable"/> (2-6 seats; Tools ▸ SimpleMovements ▸ Minigames ▸ Create Drinking
/// Contest Table). Tolerance comes from body size, health and how full the stomach is. Afterwards everyone is Drunk
/// for a while (the Drunk Effect, scaled by how drunk they got).
/// </summary>
[CreateAssetMenu(fileName = "DrinkingContest", menuName = "SimpleMovements/Minigames/Drinking Contest", order = 5)]
public class DrinkingContestDefinition : CardGameDefinition
{
    [Tooltip("Seats at the table.")]
    [Range(2, 6)] [SerializeField] private int seats = 4;
    [SerializeField] private DrinkingSettings rules = new DrinkingSettings();

    [Header("Afterwards")]
    [Tooltip("What drinking does to the players afterwards, scaled by how drunk they got (empty label = nothing).")]
    [SerializeField] private StatusConsequence drunkEffect = new StatusConsequence("Drunk")
    {
        combatStats = new List<CombatStatModifier>
        {
            new CombatStatModifier(CombatStatType.CriticalChance, -10f),
            new CombatStatModifier(CombatStatType.AttackSpeed, -10f),
        },
        characterStats = new List<TraitModifier> { TraitModifier.Percent(TraitStat.MoveSpeed, -10f) },
    };
    [Tooltip("Seconds the Drunk effect lasts at 100% drunkenness (less for less).")]
    [SerializeField, Min(0f)] private float drunkSeconds = 240f;

    public DrinkingSettings Rules => rules;
    public override int SeatCount => seats;
    public override int TeamCount => seats;
    public override int TeamOfSeat(int seat) => seat;
    public override string TeamName(int team) => $"Seat {team + 1}";
    public override bool PlaysWithEmptySeats => true;
    public override int MinPlayers => 2;

    public override CardGameSession CreateSession(CardGameSetup setup) => new DrinkingContestGame(rules, setup, seats);

    /// <summary>Tolerance: body size × health × a full stomach.</summary>
    public override float PowerOf(CombatEntity player)
    {
        if (player == null)
            return 1f;
        CharacterBody body = player.GetComponentInChildren<CharacterBody>();
        if (body == null) body = player.GetComponentInParent<CharacterBody>();
        float size = body != null ? Mathf.Clamp(body.Scale, 0.6f, 1.6f) : 1f;
        float health = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1f, player.MaxHealth) / 100f), 0.6f, 1.6f);
        PlayerStatusController status = player.GetComponentInChildren<PlayerStatusController>();
        if (status == null) status = player.GetComponentInParent<PlayerStatusController>();
        float fed = status != null && status.HungerManager != null && status.HungerManager.MaxValue > 0f
            ? status.HungerManager.CurrentValue / status.HungerManager.MaxValue : 0.5f;
        return size * health * (0.8f + 0.4f * Mathf.Clamp01(fed));
    }

    public override void AfterGame(CardGameSession session, CombatEntity[] players, System.Random rng, Action<string> log)
    {
        if (!(session is DrinkingContestGame g) || players == null || drunkEffect == null || drunkSeconds <= 0f)
            return;
        StatusConsequence e = drunkEffect;
        bool any = (e.combatStats != null && e.combatStats.Count > 0) || (e.characterStats != null && e.characterStats.Count > 0);
        if (string.IsNullOrEmpty(e.label) && !any)
            return;
        for (int i = 0; i < players.Length; i++)
        {
            CombatEntity p = players[i];
            float drunk01 = i < g.SeatCount ? g.Drunkenness(i) / 100f : 0f;
            if (p == null || drunk01 <= 0.05f) continue;
            TimedStatModifiers mods = TimedStatModifiers.For(p);
            mods?.Apply(this, string.IsNullOrEmpty(e.label) ? "Drunk" : e.label, null, e.debuff, ElementType.None,
                drunkSeconds * drunk01, drunk01, e.combatStats, null, e.characterStats, EffectStacking.Refresh);
        }
    }

    public static DrinkingContestDefinition CreateDefault()
    {
        var d = CreateInstance<DrinkingContestDefinition>();
        d.name = "Drinking Contest (built-in)";
        d.hideFlags = HideFlags.DontSave;
        d.Reset();
        return d;
    }

    private void Reset()
    {
        displayName = "Drinking Contest";
        description = "Every round, down your mug before the time runs out. Big, healthy, well-fed drinkers hold more. The drunker " +
                      "you are, the harder it is to find the Drink button - and a mug may come back up. Last one standing wins.";
        botThinkSeconds = new Vector2(1f, 4f);
    }
}
