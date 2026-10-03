using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// The player's key bindings, shared by every copy of the input actions. Each player component works on its own copy
/// of the generated input class (shared input, movement, emote wheel) and the Player Input component has one more,
/// so rebinding one copy is not enough: the changes are saved once (PlayerPrefs, as JSON overrides) and applied to
/// every registered copy, now and whenever a new one is created.
/// <para>It also pauses gameplay input (the pause menu): every registered Player map is turned off and restored after.</para>
/// </summary>
public static class InputBindingStore
{
    /// <summary>PlayerPrefs key holding the binding overrides (JSON).</summary>
    public const string PrefsKey = "InputBindingOverrides";
    /// <summary>The action map paused by <see cref="SetGameplayInputEnabled"/>.</summary>
    public const string GameplayMap = "Player";

    private static readonly List<InputActionAsset> assets = new List<InputActionAsset>();
    private static readonly List<InputActionMap> pausedMaps = new List<InputActionMap>();

    /// <summary>An action created at runtime because the input actions did not have it (added to every copy).</summary>
    private struct CreatedAction
    {
        public string map, name;
        public string[] paths;
    }

    private static readonly List<CreatedAction> createdActions = new List<CreatedAction>();
    private static bool hooked;

    /// <summary>Raised after the bindings changed (rebind, reset): key prompts refresh.</summary>
    public static event Action Changed;

    /// <summary>True while gameplay input is paused (pause menu open).</summary>
    public static bool GameplayInputPaused { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        assets.Clear();
        pausedMaps.Clear();
        createdActions.Clear();
        GameplayInputPaused = false;
        Changed = null;
        if (hooked)
            SceneManager.sceneLoaded -= OnSceneLoaded;
        hooked = false;
    }

    private static void Hook()
    {
        if (hooked)
            return;
        hooked = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToPlayerInputComponents();

    /// <summary>The saved overrides (empty = default bindings).</summary>
    public static string SavedJson
    {
        get
        {
            try { return PlayerPrefs.GetString(PrefsKey, ""); }
            catch { return ""; }
        }
    }

    /// <summary>
    /// Registers a copy of the input actions: the saved bindings are applied to it now and after every change.
    /// Call it right after creating the copy (e.g. <c>new PlayerInput()</c>).
    /// </summary>
    public static void Register(InputActionAsset asset)
    {
        Hook();
        if (asset == null)
            return;
        assets.RemoveAll(a => a == null);
        if (!assets.Contains(asset))
            assets.Add(asset);
        Apply(asset);
        if (GameplayInputPaused)
            PauseMap(asset);
        ApplyToPlayerInputComponents();
    }

    /// <summary>
    /// Records an action created at runtime (<see cref="InputActionResolver.Create"/>) and adds it to every other copy of
    /// the input actions, so all the player's components, the Player Input component and the Key Bindings menu see it.
    /// </summary>
    public static void RememberCreatedAction(string map, string name, IList<string> paths)
    {
        foreach (CreatedAction c in createdActions)
            if (string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase))
                return;
        createdActions.Add(new CreatedAction { map = map, name = name, paths = paths != null ? new List<string>(paths).ToArray() : new string[0] });
        assets.RemoveAll(a => a == null);
        foreach (InputActionAsset a in assets)
            AddCreatedActions(a);
        foreach (UnityEngine.InputSystem.PlayerInput pi in UnityEngine.Object.FindObjectsByType<UnityEngine.InputSystem.PlayerInput>(FindObjectsInactive.Exclude))
            if (pi != null && pi.actions != null) AddCreatedActions(pi.actions);
        Changed?.Invoke();
    }

    private static void AddCreatedActions(InputActionAsset asset)
    {
        if (asset == null)
            return;
        foreach (CreatedAction c in createdActions)
            if (asset.FindAction(c.name, false) == null)
                InputActionResolver.Create(asset, c.name, c.paths, out _, c.map, remember: false);
    }

    /// <summary>Applies the saved bindings (and the actions created at runtime) to one copy of the input actions.</summary>
    public static void Apply(InputActionAsset asset)
    {
        if (asset == null)
            return;
        AddCreatedActions(asset);
        string json = SavedJson;
        try
        {
            if (string.IsNullOrEmpty(json))
                asset.RemoveAllBindingOverrides();
            else
                asset.LoadBindingOverridesFromJson(json, true);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Input] Saved key bindings could not be applied ({e.Message}); default keys are used.");
        }
    }

    /// <summary>Saves the overrides of <paramref name="edited"/> (the rebinding menu's copy) and applies them everywhere.</summary>
    public static void Save(InputActionAsset edited)
    {
        if (edited == null)
            return;
        try
        {
            PlayerPrefs.SetString(PrefsKey, edited.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Input] Key bindings could not be saved: {e.Message}");
        }
        ApplyToAll();
    }

    /// <summary>Back to the default keys everywhere.</summary>
    public static void ResetToDefaults()
    {
        try
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }
        catch { }
        ApplyToAll();
    }

    private static void ApplyToAll()
    {
        assets.RemoveAll(a => a == null);
        foreach (InputActionAsset a in assets)
            Apply(a);
        ApplyToPlayerInputComponents();
        Changed?.Invoke();
    }

    /// <summary>The Player Input components (camera look, interact events) get the same bindings.</summary>
    private static void ApplyToPlayerInputComponents()
    {
        foreach (UnityEngine.InputSystem.PlayerInput pi in UnityEngine.Object.FindObjectsByType<UnityEngine.InputSystem.PlayerInput>(FindObjectsInactive.Exclude))
            if (pi != null && pi.actions != null)
                Apply(pi.actions);
    }

    /// <summary>
    /// Turns gameplay input off (false) or back on (true) for every registered copy and Player Input component: the
    /// pause menu uses it so clicks on its buttons never attack, jump or open the inventory.
    /// </summary>
    public static void SetGameplayInputEnabled(bool enabled)
    {
        if (enabled == !GameplayInputPaused)
            return;
        GameplayInputPaused = !enabled;
        if (!enabled)
        {
            assets.RemoveAll(a => a == null);
            foreach (InputActionAsset a in assets)
                PauseMap(a);
            foreach (UnityEngine.InputSystem.PlayerInput pi in UnityEngine.Object.FindObjectsByType<UnityEngine.InputSystem.PlayerInput>(FindObjectsInactive.Exclude))
                if (pi != null && pi.actions != null)
                    PauseMap(pi.actions);
        }
        else
        {
            foreach (InputActionMap m in pausedMaps)
                if (m != null) m.Enable();
            pausedMaps.Clear();
        }
    }

    private static void PauseMap(InputActionAsset asset)
    {
        InputActionMap map = asset != null ? asset.FindActionMap(GameplayMap, false) : null;
        if (map == null || !map.enabled || pausedMaps.Contains(map))
            return;
        map.Disable();
        pausedMaps.Add(map);
    }
}
