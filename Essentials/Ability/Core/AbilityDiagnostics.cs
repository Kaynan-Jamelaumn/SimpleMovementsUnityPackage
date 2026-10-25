using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Warnings for abilities that run but change nothing (the cooldown starts, no effect shows): cast in the hub city
/// (only shown there), a size power on a character without a Character Body, a status effect on a character without a
/// status controller, an ability without actions. Each is written once per character and cause, so the Console says
/// why without filling up.
/// </summary>
public static class AbilityDiagnostics
{
    private static readonly HashSet<string> said = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => said.Clear();

    /// <summary>Writes <paramref name="message"/> once for this <paramref name="context"/> and <paramref name="cause"/>.</summary>
    public static void WarnOnce(Object context, string cause, string message)
    {
        string key = (context != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(context).ToString() : "-") + "/" + cause;
        if (!said.Add(key))
            return;
        Debug.LogWarning(message, context);
    }
}
