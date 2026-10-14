using System;
using HubWorld;

/// <summary>
/// The things a player can do to another player from the target frame: party, trade, duel, inspect. Each one checks
/// what it can and answers with a message; the network layer installs what needs the server (<see cref="TradeHandler"/>).
/// </summary>
public static class PlayerActions
{
    /// <summary>
    /// Asks another player to trade (installed by the network layer's trading relay; null offline, where there is
    /// nobody to trade with).
    /// </summary>
    public static Action<CombatEntity> TradeHandler;

    /// <summary>A short feedback line for this machine's player.</summary>
    public static event Action<string> Feedback;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        TradeHandler = null;
        Feedback = null;
    }

    public static bool CanTrade => TradeHandler != null;

    public static bool CanDuel => DuelSystem.Instance != null;

    public static void InviteToParty(CombatEntity me, CombatEntity other)
    {
        PartySystem parties = PartySystem.Instance;
        if (parties == null || me == null || other == null)
            return;
        if (!parties.Invite(me, other, out string reason))
            Say(reason);
        else if (GameNet.NetAuthority.IsClientOnly)
            Say($"Party invitation sent to {PartySystem.NameOf(other)}.");
    }

    public static void LeaveParty(CombatEntity me) => PartySystem.Instance?.Leave(me);

    public static void Kick(CombatEntity me, CombatEntity other)
    {
        if (PartySystem.Instance != null && !PartySystem.Instance.Kick(me, other, out string reason))
            Say(reason);
    }

    public static void Promote(CombatEntity me, CombatEntity other)
    {
        if (PartySystem.Instance != null && !PartySystem.Instance.Promote(me, other, out string reason))
            Say(reason);
    }

    public static void Trade(CombatEntity me, CombatEntity other)
    {
        if (other == null)
            return;
        if (TradeHandler == null)
        {
            Say("Trading works in a network session.");
            return;
        }
        TradeHandler(other);
        Say($"Trade request sent to {PartySystem.NameOf(other)}.");
    }

    public static void Duel(CombatEntity me, CombatEntity other)
    {
        DuelSystem duels = DuelSystem.Instance;
        if (duels == null)
        {
            Say("Duels need the Hub World (no duel system in this scene).");
            return;
        }
        if (!duels.Challenge(me, other, out string reason))
            Say(reason);
    }

    public static void Inspect(CombatEntity me, CombatEntity other)
    {
        if (other == null)
            return;
        if (!GameNet.NetAuthority.IsClientOnly && !PlayerInspection.CanInspect(me, other, out string reason))
        {
            Say(reason);
            return;
        }
        PlayerInspection.Request(me, other);
    }

    /// <summary>Shows a short line to this machine's player.</summary>
    public static void Say(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        if (Feedback != null)
        {
            Feedback(text);
            return;
        }
        HubWorldMessages.DeliverLocal(PlayerLocator.Local, text, HubMessageKind.Warning);
    }
}
