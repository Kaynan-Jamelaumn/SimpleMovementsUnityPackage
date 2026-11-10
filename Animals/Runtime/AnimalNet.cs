using UnityEngine;

/// <summary>
/// What the network layer does for animal husbandry (the <c>GameNet.NetAnimals</c> relay on the Network World implements
/// it). Requests go up from a client; pictures of pens, answers, messages and "open the window" come down from the server
/// to one player. Nothing hidden ever travels: pictures carry what can be seen (<see cref="AnimalPenView"/>).
/// </summary>
public interface IAnimalNetRelay
{
    /// <summary>Client: asks the server to do something. False when it cannot be sent.</summary>
    bool SendRequest(AnimalRequest request);
    /// <summary>Client: its player is near a pen (watch: send me its picture as it changes) or walked away from it.</summary>
    void Watch(string penId, bool watching);
    /// <summary>Server: the picture of a pen for one player.</summary>
    void SendView(CombatEntity player, AnimalPenView view);
    /// <summary>Server: what came of a player's request.</summary>
    void SendResult(CombatEntity player, string penId, AnimalResult result);
    /// <summary>Server: open the pen's window on the player's screen (it comes with the pen's picture).</summary>
    void Open(CombatEntity player, string penId);
    /// <summary>Server: a message to one player.</summary>
    void Tell(CombatEntity player, string text);
}

/// <summary>
/// The animals' side of the network: calls go to the relay when a session has one (on a client and on the server), and
/// do nothing offline. Gameplay code never names a network type.
/// </summary>
public static class AnimalNet
{
    /// <summary>Installed by <c>GameNet.NetAnimals</c> while a network session runs (null offline).</summary>
    public static IAnimalNetRelay Relay { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Relay = null;

    public static bool SendRequest(AnimalRequest r) => Relay != null && Relay.SendRequest(r);
    public static void Watch(string penId, bool watching) => Relay?.Watch(penId, watching);
    public static void SendView(CombatEntity player, AnimalPenView view) => Relay?.SendView(player, view);
    public static void SendResult(CombatEntity player, string penId, AnimalResult result) => Relay?.SendResult(player, penId, result);

    public static void Open(CombatEntity player, AnimalPen pen)
    {
        if (pen == null || player == null) return;
        if (Relay != null) Relay.Open(player, pen.PenId);
        else pen.Tell(player, "The animals are not set up for multiplayer: only the host can tend them.");
    }

    public static void Tell(CombatEntity player, string text) => Relay?.Tell(player, text);
}
