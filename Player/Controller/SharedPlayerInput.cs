using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One generated <see cref="PlayerInput"/> per player, shared by the components that read it: every ability key
/// (<see cref="AbilityStateMachine"/>), the <see cref="AbilitiesStateMachine"/> and the <see cref="TraitManager"/>.
/// Every <c>new PlayerInput()</c> builds and enables a complete copy of the input actions, so one copy per ability
/// key cost memory at start and input processing every frame for nothing.
/// <para>Holders are counted (the copy is disposed when the last holder is destroyed) and so are enables (the Player
/// map stays on while any holder is enabled), so one component turning off never cuts the input of the others.</para>
/// </summary>
public static class SharedPlayerInput
{
    private sealed class Entry
    {
        public Transform root;
        public PlayerInput input;
        public int holders;
        public int enables;
    }

    private static readonly Dictionary<Transform, Entry> byRoot = new Dictionary<Transform, Entry>(ReferenceComparer<Transform>.Instance);
    private static readonly Dictionary<PlayerInput, Entry> byInput = new Dictionary<PlayerInput, Entry>(ReferenceComparer<PlayerInput>.Instance);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Play Mode without a domain reload keeps statics: start clean.
        byRoot.Clear();
        byInput.Clear();
    }

    /// <summary>
    /// The shared input of <paramref name="owner"/>'s player (created on first use). Pair every call with
    /// <see cref="Release"/> (usually in OnDestroy).
    /// </summary>
    public static PlayerInput Acquire(Component owner)
    {
        Transform root = AbilitiesStateMachine.PlayerRoot(owner);
        if (!byRoot.TryGetValue(root, out Entry e) || e.input == null)
        {
            e = new Entry { root = root, input = new PlayerInput() };
            byRoot[root] = e;
            byInput[e.input] = e;
        }
        e.holders++;
        return e.input;
    }

    /// <summary>Gives back an input from <see cref="Acquire"/>; the last holder disposes it.</summary>
    public static void Release(PlayerInput input)
    {
        if (input == null || !byInput.TryGetValue(input, out Entry e))
            return;
        if (--e.holders > 0)
            return;
        byInput.Remove(input);
        byRoot.Remove(e.root);
        if (e.enables > 0)
            input.Player.Disable();
        input.Dispose();
    }

    /// <summary>Turns the Player map on (counted). An input that is not shared is simply enabled, as before.</summary>
    public static void Enable(PlayerInput input)
    {
        if (input == null)
            return;
        if (!byInput.TryGetValue(input, out Entry e))
        {
            input.Player.Enable();
            return;
        }
        if (e.enables++ == 0)
            input.Player.Enable();
    }

    /// <summary>Turns the Player map off when no holder needs it any more. An input that is not shared is simply disabled.</summary>
    public static void Disable(PlayerInput input)
    {
        if (input == null)
            return;
        if (!byInput.TryGetValue(input, out Entry e))
        {
            input.Player.Disable();
            return;
        }
        if (e.enables > 0 && --e.enables == 0)
            input.Player.Disable();
    }

    /// <summary>True if <paramref name="input"/> is a shared copy (the owner must Release it, not Dispose it).</summary>
    public static bool IsShared(PlayerInput input) => input != null && byInput.ContainsKey(input);
}
