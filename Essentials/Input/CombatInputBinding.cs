using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>A mouse button used when the input actions have no action for an input.</summary>
public enum MouseFallback
{
    None,
    LeftButton,
    RightButton,
    MiddleButton,
}

/// <summary>
/// One gameplay input (block, reload, off-hand attack, quickslot...): an assigned Input Action reference, else an action
/// found BY NAME in the player's input actions (the generated PlayerInput shared by the player's components), else a key /
/// mouse button fallback. A fallback that is already bound to an action of the input actions is not used (and a warning
/// says so), so the same key never triggers two things.
/// </summary>
public sealed class CombatInputBinding : IDisposable
{
    private InputAction action;
    private bool enabledByUs;
    private PlayerInput shared;
    private Key key = Key.None;
    private MouseFallback mouse = MouseFallback.None;

    /// <summary>What the input comes from (for logs and inspectors).</summary>
    public string Source { get; private set; } = "none";
    public bool HasAction => action != null;
    public bool HasAny => action != null || key != Key.None || mouse != MouseFallback.None;
    public bool UsesMouseFallback => action == null && mouse != MouseFallback.None;
    public InputAction Action => action;

    private CombatInputBinding() { }

    /// <summary>
    /// Resolves an input for <paramref name="owner"/> (a component of a player). <paramref name="label"/> names it in warnings.
    /// </summary>
    /// <param name="fallbackMayShare">The fallback is used even when the input actions already bind that key or button
    /// (for inputs read only while gameplay input is paused, e.g. in the open inventory).</param>
    public static CombatInputBinding Create(Component owner, InputActionReference reference, string[] names, Key fallbackKey,
        MouseFallback fallbackMouse, string label, bool fallbackMayShare = false)
    {
        var b = new CombatInputBinding();
        InputActionAsset asset = null;
        if (reference != null && reference.action != null)
        {
            b.action = reference.action;
            b.Source = $"'{reference.action.name}' (assigned)";
        }
        else if (owner != null)
        {
            try
            {
                b.shared = SharedPlayerInput.Acquire(owner);
                SharedPlayerInput.Enable(b.shared);
                asset = b.shared.asset;
            }
            catch (Exception e)
            {
                b.shared = null;
                Debug.LogWarning($"[Input] Could not read the player's input actions for {label} ({e.Message}); using the fallback.", owner);
            }
            if (asset != null && names != null)
            {
                foreach (string n in names)
                {
                    if (string.IsNullOrWhiteSpace(n)) continue;
                    InputAction a = asset.FindAction(n, false);
                    if (a == null) continue;
                    b.action = a;
                    b.Source = $"'{a.actionMap?.name}/{a.name}'";
                    break;
                }
            }
        }

        if (b.action != null)
        {
            if (!b.action.enabled)
            {
                b.action.Enable();
                b.enabledByUs = true;
            }
            return b;
        }

        if (fallbackKey != Key.None)
        {
            if (!fallbackMayShare && asset != null && IsBound(asset, "<Keyboard>/" + KeyPath(fallbackKey), out string by))
                Debug.LogWarning($"[Input] {label}: the fallback key {fallbackKey} is already used by '{by}' in the input actions, so it is not used. " +
                                 $"Add an action named {(names != null && names.Length > 0 ? names[0] : label)} to the input actions.", owner);
            else
                b.key = fallbackKey;
        }
        if (fallbackMouse != MouseFallback.None)
        {
            if (!fallbackMayShare && asset != null && IsBound(asset, "<Mouse>/" + MousePath(fallbackMouse), out string by))
                Debug.LogWarning($"[Input] {label}: the fallback {fallbackMouse} is already used by '{by}' in the input actions, so it is not used.", owner);
            else
                b.mouse = fallbackMouse;
        }
        b.Source = b.key != Key.None && b.mouse != MouseFallback.None ? $"{b.key} / {b.mouse} (fallback)"
                 : b.key != Key.None ? $"{b.key} (fallback)"
                 : b.mouse != MouseFallback.None ? $"{b.mouse} (fallback)" : "none";
        return b;
    }

    public bool Pressed
    {
        get
        {
            if (action != null)
                return action.WasPressedThisFrame();
            if (key != Key.None)
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && kb[key] != null && kb[key].wasPressedThisFrame)
                    return true;
            }
            UnityEngine.InputSystem.Controls.ButtonControl m = MouseButton();
            return m != null && m.wasPressedThisFrame;
        }
    }

    public bool Released
    {
        get
        {
            if (action != null)
                return action.WasReleasedThisFrame();
            if (key != Key.None)
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && kb[key] != null && kb[key].wasReleasedThisFrame)
                    return true;
            }
            UnityEngine.InputSystem.Controls.ButtonControl m = MouseButton();
            return m != null && m.wasReleasedThisFrame;
        }
    }

    public bool Held
    {
        get
        {
            if (action != null)
                return action.IsPressed();
            if (key != Key.None)
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && kb[key] != null && kb[key].isPressed)
                    return true;
            }
            UnityEngine.InputSystem.Controls.ButtonControl m = MouseButton();
            return m != null && m.isPressed;
        }
    }

    private UnityEngine.InputSystem.Controls.ButtonControl MouseButton()
    {
        if (mouse == MouseFallback.None)
            return null;
        Mouse m = Mouse.current;
        if (m == null)
            return null;
        switch (mouse)
        {
            case MouseFallback.LeftButton: return m.leftButton;
            case MouseFallback.RightButton: return m.rightButton;
            default: return m.middleButton;
        }
    }

    public void Dispose()
    {
        if (action != null && enabledByUs)
            action.Disable();
        action = null;
        enabledByUs = false;
        if (shared != null)
        {
            SharedPlayerInput.Disable(shared);
            SharedPlayerInput.Release(shared);
            shared = null;
        }
    }

    // ------------------------------------------------------------------ helpers
    /// <summary>The control name of a key in binding paths: Key.R → "r", Key.Digit1 → "1", Key.LeftShift → "leftShift".</summary>
    public static string KeyPath(Key key)
    {
        string n = key.ToString();
        if (n.StartsWith("Digit", StringComparison.Ordinal) && n.Length == 6)
            return n.Substring(5);
        return n.Length > 0 ? char.ToLowerInvariant(n[0]) + n.Substring(1) : n;
    }

    public static string MousePath(MouseFallback m)
    {
        switch (m)
        {
            case MouseFallback.LeftButton: return "leftButton";
            case MouseFallback.RightButton: return "rightButton";
            case MouseFallback.MiddleButton: return "middleButton";
            default: return "";
        }
    }

    /// <summary>Is <paramref name="controlPath"/> (e.g. "&lt;Keyboard&gt;/r") bound in a non-UI map of the asset? <paramref name="by"/> = the action.</summary>
    public static bool IsBound(InputActionAsset asset, string controlPath, out string by)
    {
        by = "";
        if (asset == null || string.IsNullOrEmpty(controlPath))
            return false;
        // actionMaps, actions and bindings are ReadOnlyArray structs (never null; empty when there are none).
        foreach (InputActionMap map in asset.actionMaps)
        {
            if (map == null || string.Equals(map.name, "UI", StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (InputAction a in map.actions)
            {
                if (a == null)
                    continue;
                foreach (InputBinding b in a.bindings)
                {
                    string p = !string.IsNullOrEmpty(b.effectivePath) ? b.effectivePath : b.path;
                    if (!string.IsNullOrEmpty(p) && string.Equals(p.Trim(), controlPath, StringComparison.OrdinalIgnoreCase))
                    {
                        by = $"{map.name}/{a.name}";
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>Is <paramref name="owner"/> part of a player (only players read input; mobs are driven by AI)?</summary>
    public static bool IsPlayer(Component owner) => owner != null && owner.GetComponentInParent<PlayerStatusController>() != null;
}
