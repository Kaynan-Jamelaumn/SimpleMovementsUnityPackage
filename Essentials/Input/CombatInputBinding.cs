using System;
using System.Collections.Generic;
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
/// One gameplay input (block, reload, off-hand attack, quickslot, active trait, rotate...). Resolved in three steps:
/// <list type="number">
/// <item>an assigned Input Action reference, else an action found BY NAME in the player's input actions (the shared
/// generated PlayerInput) - rebindable in the Key Bindings menu;</item>
/// <item>when there is none, the action is CREATED in the gameplay map with the script's default controls, so it is
/// rebindable and saved like the others (<see cref="InputActionResolver"/>);</item>
/// <item>when it cannot be created, a standalone action on those default controls - any device: keyboard, mouse,
/// gamepad, joystick, touch.</item>
/// </list>
/// A default control that another action already uses is skipped (a warning says so), so one key never triggers two
/// things - unless the input may share it (read only while gameplay input is paused, e.g. in the open inventory).
/// </summary>
public sealed class CombatInputBinding : IDisposable
{
    private InputAction action;
    private bool enabledByUs;
    private PlayerInput shared;
    private InputAction fallback;
    private bool fallbackUsesMouse;

    /// <summary>What the input comes from (for logs and inspectors).</summary>
    public string Source { get; private set; } = "none";
    /// <summary>An action of the input actions (found or created): rebindable in the settings.</summary>
    public bool HasAction => action != null;
    public bool HasAny => action != null || fallback != null;
    /// <summary>The action was created at runtime (the input actions did not have it).</summary>
    public bool Created { get; private set; }
    /// <summary>The input comes from the script's default controls (created action or standalone) and they include a mouse button.</summary>
    public bool UsesMouseFallback => (action == null || Created) && fallbackUsesMouse;
    /// <summary>The action of the input actions (null when a standalone fallback is used).</summary>
    public InputAction Action => action;
    /// <summary>Whatever is read: the action, else the standalone fallback (key prompts).</summary>
    public InputAction ReadAction => action ?? fallback;

    private CombatInputBinding() { }

    /// <summary>Keyboard key + mouse button defaults (the older form). Prefer the control-path form for gamepads.</summary>
    public static CombatInputBinding Create(Component owner, InputActionReference reference, string[] names, Key fallbackKey,
        MouseFallback fallbackMouse, string label, bool fallbackMayShare = false)
    {
        var paths = new List<string>();
        if (fallbackKey != Key.None) paths.Add(InputActionResolver.KeyPath(fallbackKey));
        if (fallbackMouse != MouseFallback.None) paths.Add("<Mouse>/" + MousePath(fallbackMouse));
        return Create(owner, reference, names, paths, label, fallbackMayShare);
    }

    /// <summary>
    /// Resolves an input for <paramref name="owner"/> (a component of a player). <paramref name="defaultControls"/> are
    /// control paths of any device ("&lt;Keyboard&gt;/r", "&lt;Gamepad&gt;/buttonWest", "&lt;Mouse&gt;/rightButton",
    /// "&lt;Joystick&gt;/trigger"): the bindings of a created action, or the standalone fallback.
    /// <paramref name="label"/> names it in logs.
    /// </summary>
    /// <param name="fallbackMayShare">Default controls are used even when another action already binds them.</param>
    /// <param name="alsoFind">Another way to find an existing action when none has one of the names (e.g. any action on the left mouse button).</param>
    public static CombatInputBinding Create(Component owner, InputActionReference reference, string[] names, IList<string> defaultControls,
        string label, bool fallbackMayShare = false, Func<InputActionAsset, InputAction> alsoFind = null)
    {
        var b = new CombatInputBinding();
        InputActionAsset asset = null;

        // 1. Assigned, or found by name.
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
                Debug.LogWarning($"[Input] Could not read the player's input actions for {label} ({e.Message}); using the default controls.", owner);
            }
            InputAction found = InputActionResolver.Find(asset, names);
            if (found == null && alsoFind != null && asset != null)
                found = alsoFind(asset);
            if (found != null)
            {
                b.action = found;
                b.Source = $"'{found.actionMap?.name}/{found.name}'";
            }
        }

        // Default controls another action already uses are skipped.
        var usable = new List<string>();
        if (b.action == null && defaultControls != null)
        {
            foreach (string p in defaultControls)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                if (!fallbackMayShare && asset != null && IsBound(asset, p.Trim(), out string by))
                {
                    Debug.LogWarning($"[Input] {label}: the default control {InputActionResolver.Describe(new[] { p })} is already used by '{by}', so it is not used. " +
                                     $"Add an action named {(names != null && names.Length > 0 ? names[0] : label)} to the input actions to choose its key.", owner);
                    continue;
                }
                usable.Add(p.Trim());
            }
        }

        b.fallbackUsesMouse = usable.Exists(p => p.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase));

        // 2. Create the action in the input actions (rebindable, saved, paused with gameplay).
        if (b.action == null && asset != null && names != null && names.Length > 0 && !string.IsNullOrWhiteSpace(names[0]) && usable.Count > 0)
        {
            InputAction created = InputActionResolver.Create(asset, names[0], usable, out string error);
            if (created != null)
            {
                b.action = created;
                b.Created = true;
                b.Source = $"'{created.actionMap?.name}/{created.name}' (created: {InputActionResolver.Describe(usable)})";
                Debug.Log($"[Input] {label}: the input actions had no '{names[0]}' action, so it was created with {InputActionResolver.Describe(usable)}. " +
                          "It can be changed in Settings ▸ Key Bindings, or add it to the input actions asset.", owner);
            }
            else if (error != null)
            {
                Debug.LogWarning($"[Input] {label}: could not create the '{names[0]}' action ({error}); using the default controls directly.", owner);
            }
        }

        if (b.action != null)
        {
            if (!b.action.enabled && !InputBindingStore.GameplayInputPaused)
            {
                b.action.Enable();
                b.enabledByUs = true;
            }
            return b;
        }

        // 3. Standalone fallback on the default controls (any device).
        b.fallback = InputActionResolver.Standalone(label, usable);
        if (b.fallback != null)
        {
            b.Source = $"{InputActionResolver.Describe(usable)} (fallback)";
        }
        return b;
    }

    /// <summary>A standalone fallback is quiet while gameplay input is paused (pause menu), like the actions.</summary>
    private bool FallbackLive => fallback != null && !InputBindingStore.GameplayInputPaused;

    public bool Pressed => action != null ? action.WasPressedThisFrame() : FallbackLive && fallback.WasPressedThisFrame();

    public bool Released => action != null ? action.WasReleasedThisFrame() : FallbackLive && fallback.WasReleasedThisFrame();

    public bool Held => action != null ? action.IsPressed() : FallbackLive && fallback.IsPressed();

    public void Dispose()
    {
        if (action != null && enabledByUs)
            action.Disable();
        action = null;
        enabledByUs = false;
        fallback?.Dispose();
        fallback = null;
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
        string full = InputActionResolver.KeyPath(key);
        return full != null ? full.Substring("<Keyboard>/".Length) : "";
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
