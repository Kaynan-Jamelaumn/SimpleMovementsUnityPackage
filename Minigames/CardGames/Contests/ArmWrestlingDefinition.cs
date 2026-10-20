using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Arm wrestling for a two-seat <see cref="CardTable"/> (Tools ▸ SimpleMovements ▸ Minigames ▸ Create Arm Wrestling
/// Table). A player's strength comes from their body size, health and stamina and the state of their main arm (injury
/// system): a broken arm is weak, a missing arm cannot wrestle. Bet a limb and the loser loses it.
/// </summary>
[CreateAssetMenu(fileName = "ArmWrestling", menuName = "SimpleMovements/Minigames/Arm Wrestling", order = 4)]
public class ArmWrestlingDefinition : CardGameDefinition
{
    [SerializeField] private ArmWrestlingSettings rules = new ArmWrestlingSettings();

    public ArmWrestlingSettings Rules => rules;
    public override int SeatCount => 2;
    public override int TeamCount => 2;
    public override int TeamOfSeat(int seat) => seat;
    public override string TeamName(int team) => $"Seat {team + 1}";

    public override CardGameSession CreateSession(CardGameSetup setup) => new ArmWrestlingGame(rules, setup);

    /// <summary>The main arm's parts (injury system), or none.</summary>
    private static List<InjuryPartState> MainArm(CombatEntity player, out InjuryController c)
    {
        var list = new List<InjuryPartState>();
        c = InjuryController.For(player);
        if (c == null || !c.Settings.enabled)
            return list;
        BodyPartSide side = c.Settings.mainHandSide;
        foreach (InjuryPartState p in c.Parts)
            if (p.Def.side == side && p.Def.HoldsAHand)
                list.Add(p);
        return list;
    }

    public override bool CanPlay(CombatEntity player, out string reason)
    {
        reason = "";
        foreach (InjuryPartState p in MainArm(player, out _))
            if (p.Lost)
            {
                reason = $"You need your {p.Name.ToLowerInvariant()} to arm wrestle";
                return false;
            }
        return true;
    }

    /// <summary>Strength: body size × health × stamina × the main arm's condition.</summary>
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
        float stamina = status != null && status.StaminaManager != null ? Mathf.Clamp(Mathf.Pow(Mathf.Max(1f, status.StaminaManager.MaxValue) / 100f, 0.3f), 0.7f, 1.4f) : 1f;
        float arm = 1f;
        foreach (InjuryPartState p in MainArm(player, out _))
            arm = Mathf.Min(arm, 1f - p.Impairment * 0.8f);
        return size * health * stamina * Mathf.Max(0.15f, arm);
    }

    public override void AfterGame(CardGameSession session, CombatEntity[] players, System.Random rng, Action<string> log)
    {
        int winner = session.WinningTeam;
        if (winner < 0 || players == null || rules.loserStrainChance <= 0f || string.IsNullOrWhiteSpace(rules.strainInjury))
            return;
        for (int i = 0; i < players.Length; i++)
        {
            if (i == winner || players[i] == null || rng.NextDouble() > rules.loserStrainChance) continue;
            List<InjuryPartState> arm = MainArm(players[i], out InjuryController c);
            InjuryPartState forearm = null;
            foreach (InjuryPartState p in arm)
                if (!p.Lost && (forearm == null || p.Def.kind == BodyPartKind.Arm)) forearm = p;
            InjuryTypeDefinition type = c != null ? c.Settings.FindType(rules.strainInjury) : null;
            if (forearm == null || type == null) continue;
            if (c.AddInjury(forearm, type, 0, null, false) != null)
                log?.Invoke($"The loser's {forearm.Name.ToLowerInvariant()} is strained: {type.StageName(0)}.");
        }
    }

    public static ArmWrestlingDefinition CreateDefault()
    {
        var d = CreateInstance<ArmWrestlingDefinition>();
        d.name = "Arm Wrestling (built-in)";
        d.hideFlags = HideFlags.DontSave;
        d.Reset();
        return d;
    }

    private void Reset()
    {
        displayName = "Arm Wrestling";
        description = "Grip, wait for GO!, then mash Push. Your strength (size, health, stamina, the state of your arm) decides " +
                      "how far each push goes; tired arms push weaker. Grit gives a big push for a lot of stamina. Best of three.";
        botThinkSeconds = new Vector2(0.14f, 0.3f);
    }
}
