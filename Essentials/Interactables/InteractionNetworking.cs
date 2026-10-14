/// <summary>
/// Sends a player's interactions to a server instead of running them on this machine. Set on a client's own player by
/// the network layer (<c>GameNet.NetInteractor</c>, see <see cref="PlayerInteractor.Relay"/>); null offline and on the
/// server, where the <see cref="PlayerInteractor"/> calls <see cref="IInteractable.Interact"/> itself as before.
/// </summary>
public interface IInteractionRelay
{
    /// <summary>
    /// Asks the server to interact with <paramref name="target"/>. Returns false when the target cannot be named to the
    /// server (an object that exists only on this machine): the interactor then uses it locally.
    /// </summary>
    bool TryRelay(PlayerInteractor interactor, IInteractable target);

    /// <summary>The player started (true) or stopped (false) holding the Interact input on <paramref name="target"/>.</summary>
    void HoldChanged(PlayerInteractor interactor, IInteractable target, bool holding);
}

/// <summary>
/// An interactable whose state every player must see the same: a door open or closed, a lever, a station in use. With a
/// <c>GameNet.NetInteractable</c> (and a NetworkObject) on the object, the server's <see cref="ReplicatedState"/> is
/// sent to every client, which applies it with <see cref="ApplyReplicatedState"/>. Offline it is never used.
/// </summary>
public interface IReplicatedInteractable
{
    /// <summary>The state to replicate (read on the server; any integer the component understands).</summary>
    int ReplicatedState { get; }

    /// <summary>A client receives the server's state: show it (open the door) without running gameplay rules.</summary>
    void ApplyReplicatedState(int state);
}
