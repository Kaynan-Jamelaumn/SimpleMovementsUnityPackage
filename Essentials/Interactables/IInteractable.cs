using UnityEngine;

/// <summary>
/// Something a player can interact with through the <see cref="PlayerInteractor"/>: NPCs (<see cref="NPC"/>) and any of
/// your own objects. The interactor finds it under the mouse, at the centre of the screen or nearby, checks
/// <see cref="CanInteract"/> and the range, shows a prompt ("[E] Trade  Bram the Smith") and calls <see cref="Interact"/>
/// with the Interact key or a click.
/// <para>Put it on the object that has the collider, or on one of its parents (the interactor looks up the hierarchy).</para>
/// </summary>
public interface IInteractable
{
    /// <summary>Name shown in the prompt ("Bram the Smith").</summary>
    string InteractionName { get; }

    /// <summary>What interacting does, shown in the prompt ("Talk", "Trade", "Open").</summary>
    string InteractionVerb { get; }

    /// <summary>Where the range is measured from and the prompt is shown above.</summary>
    Transform InteractionPoint { get; }

    /// <summary>How close the player must be (metres, measured on the ground plane from the Interaction Point).</summary>
    float InteractionRange { get; }

    /// <summary>When several targets are near, the highest priority is chosen first (then the nearest).</summary>
    int InteractionPriority { get; }

    /// <summary>Seconds the Interact key must be held (0 = a press).</summary>
    float HoldDuration { get; }

    /// <summary>Can it be used by clicking it with the mouse?</summary>
    bool AllowsClick { get; }

    /// <summary>Is it usable by <paramref name="interactor"/> right now? <paramref name="reason"/> explains why not ("Busy").</summary>
    bool CanInteract(PlayerInteractor interactor, out string reason);

    /// <summary>The player interacts (range and <see cref="CanInteract"/> were checked).</summary>
    void Interact(PlayerInteractor interactor);

    /// <summary>It became (or stopped being) the target of <paramref name="interactor"/>: show or hide a highlight.</summary>
    void OnFocusChanged(PlayerInteractor interactor, bool focused);
}
