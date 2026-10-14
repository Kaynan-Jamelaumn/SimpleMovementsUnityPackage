using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The second step of every input lookup: when the player's input actions have no action for an input, the action is
/// CREATED at runtime in the gameplay map with the script's default controls (any device: keyboard, mouse, gamepad,
/// joystick...). Created actions behave like the ones in the asset: they are paused with gameplay input, listed in the
/// Key Bindings menu, rebindable, and their rebinds are saved (their bindings get stable ids). Every copy of the input
/// actions gets the same created actions (<see cref="InputBindingStore"/>).
/// </summary>
public static class InputActionResolver
{
    /// <summary>The map new actions are added to.</summary>
    public const string DefaultMap = "Player";

    /// <summary>The first action of <paramref name="asset"/> named like one of <paramref name="names"/>, or null.</summary>
    public static InputAction Find(InputActionAsset asset, IList<string> names)
    {
        if (asset == null || names == null)
            return null;
        foreach (string n in names)
        {
            if (string.IsNullOrWhiteSpace(n)) continue;
            InputAction a = asset.FindAction(n, false);
            if (a != null) return a;
        }
        return null;
    }

    /// <summary>
    /// Creates a button action <paramref name="name"/> in <paramref name="mapName"/> (else the first non-UI map) with
    /// <paramref name="paths"/> as its bindings. Returns null (and why in <paramref name="error"/>) when it cannot.
    /// </summary>
    public static InputAction Create(InputActionAsset asset, string name, IList<string> paths, out string error, string mapName = DefaultMap, bool remember = true)
    {
        error = null;
        if (asset == null) { error = "no input actions"; return null; }
        if (string.IsNullOrWhiteSpace(name)) { error = "no action name"; return null; }
        InputAction existing = asset.FindAction(name, false);
        if (existing != null)
            return existing;

        InputActionMap map = asset.FindActionMap(mapName, false);
        if (map == null)
            foreach (InputActionMap m in asset.actionMaps)
                if (m != null && !string.Equals(m.name, "UI", StringComparison.OrdinalIgnoreCase)) { map = m; break; }
        if (map == null) { error = "the input actions have no gameplay map"; return null; }

        // Actions can only be added while their asset is disabled, and adding one throws away the asset's shared state:
        // until the asset resolves its bindings again, asking ANY of its actions whether it is enabled (FindAction does)
        // fails with "Action map must have state at this point". So: remember what was on, switch the whole asset off,
        // add the action, then switch the whole asset on and off once (that rebuilds the state, even when nothing was
        // on), and switch back on exactly the actions that were on.
        var wasEnabled = new List<InputAction>();
        foreach (InputActionMap m in asset.actionMaps)
        {
            if (m == null) continue;
            foreach (InputAction a in m.actions)
            {
                try { if (a != null && a.enabled) wasEnabled.Add(a); }
                catch (Exception) { } // the state was already lost elsewhere: rebuilt below
            }
        }
        InputAction created = null;
        try
        {
            try { asset.Disable(); }
            catch (Exception) { }
            created = map.AddAction(name, InputActionType.Button);
            if (paths != null)
                foreach (string p in paths)
                    if (!string.IsNullOrWhiteSpace(p))
                        created.AddBinding(new InputBinding { path = p.Trim(), id = StableId(map.name, name, p.Trim()) });
        }
        catch (Exception e)
        {
            error = e.Message;
            created = null;
        }
        finally
        {
            Restore(asset, wasEnabled);
        }
        if (created != null && remember)
            InputBindingStore.RememberCreatedAction(map.name, name, paths);
        return created;
    }

    /// <summary>
    /// Rebuilds the asset's shared state (enabling the whole asset resolves every binding; disabling keeps the state),
    /// then switches on exactly the actions that were on before.
    /// </summary>
    private static void Restore(InputActionAsset asset, List<InputAction> wasEnabled)
    {
        try
        {
            asset.Enable();
            asset.Disable();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Input] Could not rebuild the input actions after adding one: {e.Message}");
        }
        foreach (InputAction a in wasEnabled)
        {
            if (a == null) continue;
            try { a.Enable(); }
            catch (Exception e) { Debug.LogWarning($"[Input] Could not switch '{a.name}' back on: {e.Message}"); }
        }
    }

    /// <summary>
    /// The third step: a standalone action on <paramref name="paths"/> (any device) owned by the caller, used when the
    /// action can neither be found nor created. Dispose it when done. Null when there is no path.
    /// </summary>
    public static InputAction Standalone(string name, IList<string> paths)
    {
        if (paths == null)
            return null;
        InputAction a = null;
        foreach (string p in paths)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            if (a == null) a = new InputAction(name, InputActionType.Button);
            a.AddBinding(p.Trim());
        }
        a?.Enable();
        return a;
    }

    /// <summary>The same id for the same map / action / control everywhere, so saved rebinds find created bindings again.</summary>
    public static Guid StableId(string map, string action, string path)
    {
        using (MD5 md5 = MD5.Create())
            return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes($"{map}/{action}/{path}".ToLowerInvariant())));
    }

    /// <summary>"&lt;Keyboard&gt;/j", "&lt;Gamepad&gt;/select" → "J / Select".</summary>
    public static string Describe(IList<string> paths)
    {
        var parts = new List<string>();
        if (paths != null)
            foreach (string p in paths)
                if (!string.IsNullOrWhiteSpace(p))
                    parts.Add(InputControlPath.ToHumanReadableString(p.Trim(), InputControlPath.HumanReadableStringOptions.OmitDevice));
        return parts.Count > 0 ? string.Join(" / ", parts) : "none";
    }

    /// <summary>Control path of a keyboard key: Key.R → "&lt;Keyboard&gt;/r", Key.Digit1 → "&lt;Keyboard&gt;/1".</summary>
    public static string KeyPath(Key key)
    {
        if (key == Key.None) return null;
        string n = key.ToString();
        if (n.StartsWith("Digit", StringComparison.Ordinal) && n.Length == 6)
            n = n.Substring(5);
        else if (n.Length > 0)
            n = char.ToLowerInvariant(n[0]) + n.Substring(1);
        return "<Keyboard>/" + n;
    }
}
