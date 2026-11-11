using UnityEngine;

/// <summary>
/// What an ability slot can hold besides an ordinary ability's own costs: a system (rune spells, for one) attaches an
/// <see cref="ICastable"/> to the slot (<see cref="AbilitySlot.Castable"/>) and the player's ability controller then asks it
/// before a cast, tells it when the cast starts, is released or is cut short, and lets it describe itself on the ability
/// bar's tooltip. The slot, its key, its cooldown, its cast time and its effects are all the ordinary ability pipeline
/// (there is no second one): the castable only adds what that pipeline knows nothing about (an energy drawn from somewhere
/// else, a strain, a chance of failing).
/// <para>Everything but <see cref="HasResources"/> and <see cref="Describe"/> is called where the cast is run (the server
/// in a network session, never for the copies of other players' casts that clients show). <see cref="HasResources"/> and
/// <see cref="Describe"/> also run on the owning client, which only knows what the server told it.</para>
/// </summary>
public interface ICastable
{
    /// <summary>Does the caster have what this cast needs (it is not the only check: mana, stamina and the rest still apply)?</summary>
    bool HasResources(AbilityCaster caster);

    /// <summary>
    /// May a cast be started now (a condition beyond the resources, such as a key sequence that has to come first)? Asked when a cast is
    /// attempted, not for the state of a key on the ability bar (that is <see cref="HasResources"/>).
    /// </summary>
    bool CanStart(AbilityCaster caster);

    /// <summary>The cast starts: pay what it costs (the ability is not "only shown" for visuals: nothing is paid then).</summary>
    void OnCastStarted(AbilityCastInstance cast);

    /// <summary>The cast reached its release: it succeeded (called for "only shown" casts too, check <see cref="AbilityCastInstance.VisualOnly"/>).</summary>
    void OnCastReleased(AbilityCastInstance cast);

    /// <summary>The cast was cut short (interrupted by an enemy, cancelled, replaced, or failed on its own). Before release the cost may be given back.</summary>
    void OnCastInterrupted(AbilityCastInstance cast, CastInterruptReason reason);

    /// <summary>Lines for the ability bar's tooltip (costs, strain...), separated by new lines. Empty = nothing to add.</summary>
    string Describe();
}
