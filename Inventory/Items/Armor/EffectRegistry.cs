using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finds the handler of a special mechanic ("double_jump", "water_walking"...) and turns it on or off. Handlers are
/// <see cref="SpecialMechanicHandlerBase"/> components; they register themselves, and a mechanic can bring its own
/// handler prefab (spawned on the character the first time it is needed). IDs are matched ignoring case and spaces
/// around them. Equipment turns mechanics on and off through the reference-counted
/// <see cref="MechanicRegistry"/>, so this is only called when a mechanic really changes state.
/// </summary>
public class EffectRegistry : MonoBehaviour
{
    private static EffectRegistry _instance;

    public static EffectRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<EffectRegistry>();
                if (_instance == null && Application.isPlaying)
                {
                    GameObject go = new GameObject("EffectRegistry");
                    _instance = go.AddComponent<EffectRegistry>();
                    _instance.InitializeRegistry();
                }
            }
            return _instance;
        }
    }

    [Tooltip("Log registrations and activations to the Console.")]
    [SerializeField] private bool debugLog = false;

    private readonly Dictionary<string, ISpecialMechanicHandler> mechanicHandlers = new Dictionary<string, ISpecialMechanicHandler>();
    private readonly HashSet<string> warnedMissing = new HashSet<string>();
    private bool isInitialized = false;

    /// <summary>The form every mechanic ID is compared in.</summary>
    public static string Normalize(string mechanicId) => string.IsNullOrEmpty(mechanicId) ? "" : mechanicId.Trim().ToLowerInvariant();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
        InitializeRegistry();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => _instance = null;

    private void InitializeRegistry()
    {
        if (isInitialized) return;
        isInitialized = true;
        FindAndRegisterExistingHandlers();
    }

    private void FindAndRegisterExistingHandlers()
    {
        var handlers = FindObjectsByType<SpecialMechanicHandlerBase>(FindObjectsInactive.Exclude);
        foreach (var handler in handlers)
            foreach (var mechanicId in handler.GetSupportedMechanics())
                RegisterMechanicHandler(mechanicId, handler);
        if (debugLog)
            Debug.Log($"[EffectRegistry] Registered {handlers.Length} special mechanic handler(s).", this);
    }

    // Register a special mechanic handler
    public void RegisterMechanicHandler(string mechanicId, ISpecialMechanicHandler handler)
    {
        string id = Normalize(mechanicId);
        if (id.Length == 0 || handler == null) return;
        mechanicHandlers[id] = handler;
        if (debugLog)
            Debug.Log($"[EffectRegistry] {id} -> {handler.GetType().Name}", this);
    }

    // Apply a special mechanic
    public void ApplySpecialMechanic(SpecialMechanic mechanic, bool enable)
    {
        string id = mechanic != null ? Normalize(mechanic.mechanicId) : "";
        if (id.Length == 0)
        {
            Debug.LogWarning("[EffectRegistry] A special mechanic without an ID was ignored.", this);
            return;
        }

        if (!mechanicHandlers.TryGetValue(id, out var handler) || handler == null || (handler is Object o && o == null))
        {
            handler = null;
            // Look for any handler that says it can do it.
            foreach (var kvp in mechanicHandlers)
            {
                if (kvp.Value != null && !(kvp.Value is Object ko && ko == null) && kvp.Value.CanHandleMechanic(id))
                {
                    handler = kvp.Value;
                    mechanicHandlers[id] = handler;
                    break;
                }
            }
        }

        if (handler == null)
        {
            if (warnedMissing.Add(id))
                Debug.LogWarning($"[EffectRegistry] No handler for special mechanic '{mechanic.mechanicId}'. Add a SpecialMechanicHandler component that supports it, or give the mechanic a Handler Prefab.", this);
            return;
        }

        handler.ApplyMechanic(mechanic, enable);
        if (debugLog)
            Debug.Log($"[EffectRegistry] {mechanic.mechanicId} {(enable ? "enabled" : "disabled")}", this);
    }

    // Get mechanic handler
    public ISpecialMechanicHandler GetMechanicHandler(string mechanicId)
    {
        mechanicHandlers.TryGetValue(Normalize(mechanicId), out var handler);
        return handler;
    }

    // Check if handler exists
    public bool HasMechanicHandler(string mechanicId) => mechanicHandlers.ContainsKey(Normalize(mechanicId));

    // Get all registered mechanics
    public List<string> GetRegisteredMechanics() => new List<string>(mechanicHandlers.Keys);

    // Clear all handlers (useful for testing)
    public void ClearHandlers() => mechanicHandlers.Clear();

    // Refresh handler registrations (useful when new handlers are added at runtime)
    public void RefreshHandlerRegistrations()
    {
        mechanicHandlers.Clear();
        FindAndRegisterExistingHandlers();
    }

    [ContextMenu("Log Registered Handlers")]
    public void LogRegisteredHandlers()
    {
        var sb = new System.Text.StringBuilder("=== Registered Special Mechanic Handlers ===\n");
        foreach (var kvp in mechanicHandlers)
            sb.AppendLine($"{kvp.Key} -> {kvp.Value?.GetType().Name ?? "(destroyed)"}");
        sb.Append($"Total: {mechanicHandlers.Count}");
        Debug.Log(sb.ToString(), this);
    }
}
