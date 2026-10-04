using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Base of the windows an NPC session opens (dialogue, shop). While bound to a session it is registered on the player's
/// inventory as an open panel (<see cref="IGameplayPanel"/>): the cursor is free, clicks never attack, and Escape closes
/// it before the pause menu opens. It closes itself when the session ends, and pauses gameplay keys while the player
/// types in one of its text fields (so typing "e" in a search box does not interact or move).
/// </summary>
public abstract class NPCWindow : MonoBehaviour, IGameplayPanel
{
    private InventoryManager registeredWith;
    private bool managesCursor;
    private CursorLockMode previousLock;
    private bool previousVisible;
    private bool pausedForTyping;

    /// <summary>The session the window shows (null when closed).</summary>
    public NPCInteractionSession Session { get; private set; }
    public bool IsOpen => Session != null;
    public virtual string PanelName => Session != null && Session.Npc != null ? $"{GetType().Name}: {Session.Npc.DisplayName}" : GetType().Name;

    /// <summary>Shows the window for <paramref name="session"/> (registers it as an open panel).</summary>
    protected void Bind(NPCInteractionSession session)
    {
        if (session == null)
            return;
        if (Session == session)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            return;
        }
        if (Session != null)
            Unbind();
        Session = session;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        registeredWith = session.Inventory;
        if (registeredWith != null)
        {
            registeredWith.RegisterPanel(this);
        }
        else
        {
            // No inventory to manage the cursor: the window frees it and gives it back.
            managesCursor = true;
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        session.Ended += OnSessionEnded;
    }

    /// <summary>Hides the window without ending the session (another window of the session takes over).</summary>
    protected void Unbind() => Unbind(true);

    private void Unbind(bool deactivate)
    {
        NPCInteractionSession s = Session;
        Session = null;
        if (s != null)
            s.Ended -= OnSessionEnded;
        ResumeGameplayInput();
        if (registeredWith != null)
        {
            registeredWith.UnregisterPanel(this);
            registeredWith = null;
        }
        if (managesCursor)
        {
            managesCursor = false;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }
        OnUnbound();
        if (deactivate && this != null && gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    /// <summary>Called after the window was hidden (clear selections, stop listening to events).</summary>
    protected virtual void OnUnbound() { }

    private void OnSessionEnded(NPCInteractionSession s)
    {
        if (ReferenceEquals(s, Session))
            Unbind();
    }

    /// <summary>Escape or the pause menu: the same as the player closing the window.</summary>
    public void ClosePanel() => RequestClose();

    /// <summary>The player closes the window (close button, Escape). Default: the conversation ends.</summary>
    public virtual void RequestClose()
    {
        NPCInteractionSession s = Session;
        if (s == null)
        {
            Unbind();
            return;
        }
        s.End("closed");
        if (Session == s)
            Unbind(); // the session was already over
    }

    protected virtual void Update()
    {
        if (Session == null)
            return;
        if (Cursor.lockState != CursorLockMode.None)
        {
            // Something locked the cursor while the window is open (resuming from the pause menu): it stays usable.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        bool typing = IsTyping();
        UpdateTypingPause(typing);
        if (!typing && CancelPressed() && !PauseMenuClosesUs())
            RequestClose();
    }

    protected virtual void OnDisable()
    {
        if (Session != null)
            Unbind(false); // already being deactivated
        ResumeGameplayInput();
    }

    protected virtual void OnDestroy()
    {
        if (Session != null)
            Unbind(false);
    }

    // ------------------------------------------------------------------ keys
    /// <summary>The pause menu closes registered panels with Escape itself (so the window must not also react).</summary>
    private bool PauseMenuClosesUs()
    {
        PauseMenuManager pm = PauseMenuManager.Instance;
        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        return escape && registeredWith != null && pm != null && pm.isActiveAndEnabled && pm.closeInventoryFirst;
    }

    private static bool CancelPressed()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            return true;
        Gamepad pad = Gamepad.current;
        return pad != null && pad.buttonEast.wasPressedThisFrame;
    }

    /// <summary>Is a text field of this window being typed in?</summary>
    protected bool IsTyping()
    {
        EventSystem es = EventSystem.current;
        GameObject selected = es != null ? es.currentSelectedGameObject : null;
        if (selected == null || !selected.transform.IsChildOf(transform))
            return false;
        TMP_InputField field = selected.GetComponent<TMP_InputField>();
        return field != null && field.isFocused;
    }

    private void UpdateTypingPause(bool typing)
    {
        if (typing && !pausedForTyping && !InputBindingStore.GameplayInputPaused)
        {
            InputBindingStore.SetGameplayInputEnabled(false);
            pausedForTyping = true;
        }
        else if (!typing)
        {
            ResumeGameplayInput();
        }
    }

    private void ResumeGameplayInput()
    {
        if (!pausedForTyping)
            return;
        pausedForTyping = false;
        if (InputBindingStore.GameplayInputPaused && (PauseMenuManager.Instance == null || !PauseMenuManager.Instance.IsPaused))
            InputBindingStore.SetGameplayInputEnabled(true);
    }
}

/// <summary>
/// One instance of each NPC window per canvas (and per prefab / skin), created when first needed and reused by every NPC
/// whose session shows on that canvas.
/// </summary>
public static class NPCWindowCache
{
    /// <summary>Canvas + prefab or skin + window type, compared by reference.</summary>
    private readonly struct Key : IEquatable<Key>
    {
        private readonly Canvas canvas;
        private readonly UnityEngine.Object variant;
        private readonly Type type;

        public Key(Canvas canvas, UnityEngine.Object variant, Type type)
        {
            this.canvas = canvas;
            this.variant = variant;
            this.type = type;
        }

        public bool Equals(Key o) => ReferenceEquals(canvas, o.canvas) && ReferenceEquals(variant, o.variant) && type == o.type;
        public override bool Equals(object obj) => obj is Key k && Equals(k);
        public override int GetHashCode() =>
            (RuntimeHelpers.GetHashCode(canvas) * 397 ^ (variant != null ? RuntimeHelpers.GetHashCode(variant) : 0)) * 397 ^ type.GetHashCode();
    }

    private static readonly Dictionary<Key, Component> windows = new Dictionary<Key, Component>();
    private static readonly List<Key> stale = new List<Key>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => windows.Clear();

    /// <summary>
    /// The window of type <typeparamref name="T"/> on <paramref name="canvas"/> for <paramref name="variant"/> (a prefab or a
    /// skin; null = the default). Made from <paramref name="prefab"/> when one is given, else by <paramref name="build"/>.
    /// </summary>
    public static T Get<T>(Canvas canvas, T prefab, UnityEngine.Object variant, Func<Transform, T> build) where T : Component
    {
        if (canvas == null)
            canvas = GameplayUIRoot.Generated();
        var key = new Key(canvas, prefab != null ? prefab : variant, typeof(T));
        if (windows.TryGetValue(key, out Component existing) && existing != null)
            return (T)existing;
        ForgetDestroyed();
        T window = prefab != null ? UnityEngine.Object.Instantiate(prefab, canvas.transform, false) : build(canvas.transform);
        window.gameObject.SetActive(false);
        windows[key] = window;
        return window;
    }

    /// <summary>The window made by <see cref="Get{T}"/> for these keys, or null when none was made (nothing is created).</summary>
    public static T TryGet<T>(Canvas canvas, T prefab, UnityEngine.Object variant) where T : Component
    {
        if (canvas == null)
            return null;
        var key = new Key(canvas, prefab != null ? prefab : variant, typeof(T));
        return windows.TryGetValue(key, out Component existing) && existing != null ? (T)existing : null;
    }

    /// <summary>Drops windows whose canvas or window was destroyed (a scene change).</summary>
    private static void ForgetDestroyed()
    {
        stale.Clear();
        foreach (KeyValuePair<Key, Component> kv in windows)
            if (kv.Value == null)
                stale.Add(kv.Key);
        foreach (Key k in stale)
            windows.Remove(k);
    }
}
