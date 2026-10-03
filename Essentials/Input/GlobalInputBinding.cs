using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// An input that is not one player's: the pause key, a popup's confirm / cancel, a debug toggle. Resolved like
/// <see cref="CombatInputBinding"/>, in three steps:
/// <list type="number">
/// <item>an action found BY NAME in the input actions (with the saved rebinds);</item>
/// <item>when there is none, the action is CREATED with the default controls (rebindable in Key Bindings) - unless the
/// input asks not to (debug and UI-only keys are never added to the players' key list);</item>
/// <item>otherwise a standalone action on the default controls - any device: keyboard, mouse, gamepad, joystick.</item>
/// </list>
/// All of them read one private copy of the input actions that pausing never turns off (the gameplay copies are), and
/// they follow every rebind (<see cref="InputBindingStore.Changed"/>).
/// </summary>
public sealed class GlobalInputBinding : IDisposable
{
    private static PlayerInput copy;
    private static readonly List<GlobalInputBinding> live = new List<GlobalInputBinding>();
    private static bool resolving, resolveAgain;

    private readonly string[] names;
    private readonly List<string> controls = new List<string>();
    private readonly string label;
    private readonly bool createAction;
    private readonly bool quietWhilePaused;
    private readonly UnityEngine.Object context;
    private InputAction action;
    private InputAction fallback;
    private bool loggedCreated;

    /// <summary>What the input comes from (for inspectors and logs).</summary>
    public string Source { get; private set; } = "none";
    /// <summary>The action of the input actions (found or created); null when the standalone fallback is used.</summary>
    public InputAction Action => action;
    /// <summary>Whatever is read: the action, else the standalone fallback (key prompts).</summary>
    public InputAction ReadAction => action ?? fallback;

    private GlobalInputBinding(string[] names, IList<string> defaultControls, string label, bool createAction, bool quietWhilePaused, UnityEngine.Object context)
    {
        this.names = names;
        if (defaultControls != null)
            foreach (string c in defaultControls)
                if (!string.IsNullOrWhiteSpace(c)) controls.Add(c.Trim());
        this.label = label;
        this.createAction = createAction;
        this.quietWhilePaused = quietWhilePaused;
        this.context = context;
    }

    /// <param name="names">Actions to look for (the first one found is used; the first name is the one created).</param>
    /// <param name="defaultControls">Control paths of any device: "&lt;Keyboard&gt;/escape", "&lt;Gamepad&gt;/start"...</param>
    /// <param name="createAction">Create the action when it does not exist (false = find it, else read the default controls).</param>
    /// <param name="quietWhilePaused">Read nothing while gameplay input is paused (the pause menu is open).</param>
    public static GlobalInputBinding Create(string[] names, IList<string> defaultControls, string label, bool createAction = true,
        bool quietWhilePaused = false, UnityEngine.Object context = null)
    {
        var b = new GlobalInputBinding(names, defaultControls, label, createAction, quietWhilePaused, context);
        if (live.Count == 0)
            InputBindingStore.Changed += OnStoreChanged;
        live.Add(b);
        b.Resolve();
        return b;
    }

    private bool Live => !(quietWhilePaused && InputBindingStore.GameplayInputPaused);

    public bool Pressed => Live && ReadAction != null && ReadAction.WasPressedThisFrame();

    public bool Released => Live && ReadAction != null && ReadAction.WasReleasedThisFrame();

    public bool Held => Live && ReadAction != null && ReadAction.IsPressed();

    public void Dispose()
    {
        if (!live.Remove(this))
            return;
        fallback?.Dispose();
        fallback = null;
        if (action != null && !live.Exists(b => b.action == action))
            action.Disable();
        action = null;
        if (live.Count == 0)
        {
            InputBindingStore.Changed -= OnStoreChanged;
            if (copy != null)
            {
                copy.Disable();
                copy.Dispose();
                copy = null;
            }
        }
    }

    // ------------------------------------------------------------------ resolving
    /// <summary>The private copy (created once, with the saved rebinds and the created actions).</summary>
    private static InputActionAsset Asset
    {
        get
        {
            if (copy == null)
            {
                try
                {
                    copy = new PlayerInput();
                    InputBindingStore.Apply(copy.asset);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Input] Could not read the input actions ({e.Message}); default controls are used.");
                    copy = null;
                }
            }
            return copy?.asset;
        }
    }

    private void Resolve()
    {
        fallback?.Dispose();
        fallback = null;
        action = null;
        InputActionAsset asset = Asset;
        action = InputActionResolver.Find(asset, names);

        string createdName = null;
        if (action == null && createAction && asset != null && controls.Count > 0 && names != null && names.Length > 0 &&
            !string.IsNullOrWhiteSpace(names[0]))
        {
            action = InputActionResolver.Create(asset, names[0], controls, out string error, remember: false);
            if (action != null)
                createdName = names[0];
            else if (error != null)
                Debug.LogWarning($"[Input] {label}: could not create the '{names[0]}' action ({error}); using {InputActionResolver.Describe(controls)} directly.", context);
        }

        if (action != null)
        {
            action.Enable(); // only this action: the rest of the private copy stays off
            Source = $"'{action.actionMap?.name}/{action.name}'";
        }
        else
        {
            fallback = InputActionResolver.Standalone(label, controls);
            Source = fallback != null ? $"{InputActionResolver.Describe(controls)} (fallback)" : "none";
        }

        if (createdName != null)
        {
            if (!loggedCreated)
                Debug.Log($"[Input] {label}: the input actions had no '{createdName}' action, so it was created with " +
                          $"{InputActionResolver.Describe(controls)} (change it in Settings ▸ Key Bindings).", context);
            loggedCreated = true;
            Source += " (created)";
            // Every other copy (the players', the Key Bindings menu) gets it too; this raises Changed (handled below).
            InputBindingStore.RememberCreatedAction(action.actionMap != null ? action.actionMap.name : "Player", createdName, controls);
        }
    }

    /// <summary>A rebind, a reset or a created action: the copy gets them and every input looks its action up again.</summary>
    private static void OnStoreChanged()
    {
        if (resolving)
        {
            resolveAgain = true;
            return;
        }
        resolving = true;
        try
        {
            do
            {
                resolveAgain = false;
                Reapply();
                foreach (GlobalInputBinding b in live.ToArray())
                    b.Resolve();
            }
            while (resolveAgain);
        }
        finally
        {
            resolving = false;
        }
    }

    private static void Reapply()
    {
        if (copy == null)
            return;
        var on = new List<InputAction>();
        foreach (InputAction a in copy.asset)
            if (a != null && a.enabled) on.Add(a);
        foreach (InputAction a in on) a.Disable();
        InputBindingStore.Apply(copy.asset);
        foreach (InputAction a in on) a.Enable();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        live.Clear();
        copy = null;
        resolving = resolveAgain = false;
        InputBindingStore.Changed -= OnStoreChanged;
    }
}
