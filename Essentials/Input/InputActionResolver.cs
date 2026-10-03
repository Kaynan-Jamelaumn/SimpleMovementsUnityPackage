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

        // Maps can only be changed while disabled: the actions that were on are turned on again afterwards (only those).
        var wasEnabled = new List<InputAction>();
        foreach (InputAction a in map.actions)
            if (a != null && a.enabled) wasEnabled.Add(a);
        InputAction created = null;
        try
        {
            if (wasEnabled.Count > 0) map.Disable();
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
            foreach (InputAction a in wasEnabled) a.Enable();
            if (created != null && wasEnabled.Count > 0 && wasEnabled.Count == map.actions.Count - 1)
                created.Enable(); // the whole map was on: so is the new action
        }
        if (created != null && remember)
            InputBindingStore.RememberCreatedAction(map.name, name, paths);
        return created;
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
