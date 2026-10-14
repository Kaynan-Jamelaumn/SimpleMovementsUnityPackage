/// <summary>
/// The network side of NPC sessions, installed by the network layer (<c>GameNet.NetInteractor</c>) as
/// <see cref="NPC.NetRelay"/>; null offline, where sessions open their windows on this machine as before.
/// <list type="bullet">
/// <item>Server: a session of a player controlled from another machine (<see cref="NPCInteractionSession.IsRemote"/>) runs
/// here - range, "Busy", events, purchases - and its windows open on that player's machine through the first three calls.</item>
/// <item>Client: the windows of a session the server runs (<see cref="NPCInteractionSession.IsMirror"/>) send the player's
/// choices to the server through the last three calls; the server answers by changing or ending the session.</item>
/// </list>
/// </summary>
public interface INPCSessionRelay
{
    /// <summary>Server: show the NPC's menu (with the server's <paramref name="greeting"/>) on the player's machine.</summary>
    void ShowMenu(NPCInteractionSession session, string greeting);

    /// <summary>Server: open <paramref name="behaviour"/> (shop, dialogue) on the player's machine.</summary>
    void OpenBehaviour(NPCInteractionSession session, NPCBehaviour behaviour);

    /// <summary>Server: the session ended (walked away, closed, died): close its windows on the player's machine.</summary>
    void SessionEnded(NPCInteractionSession session, string reason);

    /// <summary>Client: the player picked <paramref name="behaviour"/> in the NPC's menu.</summary>
    void PickOption(NPCInteractionSession session, NPCBehaviour behaviour);

    /// <summary>Client: the player finished <paramref name="behaviour"/> (read the lines, closed the shop).</summary>
    void BehaviourFinished(NPCInteractionSession session, NPCBehaviour behaviour);

    /// <summary>Client: the player closed the conversation (or walked away from it).</summary>
    void Close(NPCInteractionSession session);
}
