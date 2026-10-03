using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Which kind of device the player is using right now (the device of the last input action performed), so prompts
/// show only its keys: "F1" with a keyboard, "D-Pad Up" with a gamepad, never both.
/// </summary>
public static class InputDeviceTracker
{
    public enum Kind
    {
        KeyboardMouse,
        Gamepad,
    }

    private static Kind current = Kind.KeyboardMouse;
    private static bool hooked;

    /// <summary>Raised when the player switches between keyboard / mouse and a gamepad.</summary>
    public static event Action Changed;

    /// <summary>The device kind in use (keyboard and mouse until a gamepad is used).</summary>
    public static Kind Current
    {
        get
        {
            Hook();
            return current;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        if (hooked)
            InputSystem.onActionChange -= OnActionChange;
        hooked = false;
        current = Kind.KeyboardMouse;
        Changed = null;
    }

    private static void Hook()
    {
        if (hooked)
            return;
        hooked = true;
        InputSystem.onActionChange += OnActionChange;
    }

    private static void OnActionChange(object obj, InputActionChange change)
    {
        if (change != InputActionChange.ActionPerformed && change != InputActionChange.ActionStarted)
            return;
        InputDevice device = (obj as InputAction)?.activeControl?.device;
        if (device == null)
            return;
        Kind kind;
        if (device is Gamepad || device is Joystick)
            kind = Kind.Gamepad;
        else if (device is Keyboard || device is Mouse)
            kind = Kind.KeyboardMouse;
        else
            return;
        if (kind == current)
            return;
        current = kind;
        Changed?.Invoke();
    }

    /// <summary>Is a binding path ("&lt;Keyboard&gt;/f1", "&lt;Gamepad&gt;/dpad/up") for <paramref name="kind"/>?</summary>
    public static bool IsFor(string path, Kind kind)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        bool keyboardMouse = path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase) || path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase) ||
                             path.StartsWith("<Pointer>", StringComparison.OrdinalIgnoreCase);
        return kind == Kind.KeyboardMouse ? keyboardMouse : !keyboardMouse && !path.StartsWith("<Touchscreen>", StringComparison.OrdinalIgnoreCase) &&
                                                             !path.StartsWith("<XRController>", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The key / button of <paramref name="action"/> for the device in use ("F1", "R", "Right Shoulder"); empty when the
    /// action has no binding for it.
    /// </summary>
    public static string DisplayString(InputAction action) => DisplayString(action, Current);

    public static string DisplayString(InputAction action, Kind kind)
    {
        if (action == null)
            return "";
        foreach (InputBinding b in action.bindings)
        {
            if (b.isComposite || b.isPartOfComposite)
                continue;
            string p = !string.IsNullOrEmpty(b.effectivePath) ? b.effectivePath : b.path;
            if (IsFor(p, kind))
                return InputControlPath.ToHumanReadableString(p, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }
        return "";
    }
}
