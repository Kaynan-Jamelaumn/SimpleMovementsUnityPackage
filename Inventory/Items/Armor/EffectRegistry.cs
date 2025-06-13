using System;
using System.Collections.Generic;
using UnityEngine;

//  Registry for special mechanics with automatic initialization
public class EffectRegistry : MonoBehaviour
{
    private static EffectRegistry _instance;
    public static EffectRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<EffectRegistry>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("EffectRegistry");
                    _instance = go.AddComponent<EffectRegistry>();
                    _instance.InitializeRegistry();
                }
            }
            return _instance;
        }
    }

    private Dictionary<string, ISpecialMechanicHandler> mechanicHandlers = new Dictionary<string, ISpecialMechanicHandler>();
    private bool isInitialized = false;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeRegistry();
    }

    private void InitializeRegistry()
    {
        if (isInitialized) return;

        isInitialized = true;
        Debug.Log("EffectRegistry initialized");

        // Find and register all existing handlers in the scene
        FindAndRegisterExistingHandlers();
    }

    private void FindAndRegisterExistingHandlers()
    {
        // Find all special mechanic handlers in the scene
        var handlers = FindObjectsByType<SpecialMechanicHandlerBase>(FindObjectsSortMode.None);
        foreach (var handler in handlers)
        {
            foreach (var mechanicId in handler.GetSupportedMechanics())
            {
                RegisterMechanicHandler(mechanicId, handler);
            }
        }

        Debug.Log($"Found and registered {handlers.Length} special mechanic handlers");
    }

    // Register a special mechanic handler
    public void RegisterMechanicHandler(string mechanicId, ISpecialMechanicHandler handler)
    {
        if (string.IsNullOrEmpty(mechanicId) || handler == null) return;

        string lowerId = mechanicId.ToLower();
        mechanicHandlers[lowerId] = handler;
        Debug.Log($"Registered mechanic handler: {mechanicId} -> {handler.GetType().Name}");
    }

    // Apply a special mechanic
    public void ApplySpecialMechanic(SpecialMechanic mechanic, bool enable)
    {
        if (mechanic == null || string.IsNullOrEmpty(mechanic.mechanicId))
        {
            Debug.LogWarning("Invalid special mechanic");
            return;
        }

        string mechanicId = mechanic.mechanicId.ToLower();

        if (mechanicHandlers.TryGetValue(mechanicId, out var handler))
        {
            handler.ApplyMechanic(mechanic, enable);
            Debug.Log($"Applied special mechanic: {mechanic.mechanicId} (enabled: {enable})");
        }
        else
        {
            Debug.LogWarning($"No handler registered for special mechanic: {mechanic.mechanicId}");

            // Try to find a handler that can handle this mechanic
            foreach (var kvp in mechanicHandlers)
            {
                if (kvp.Value.CanHandleMechanic(mechanicId))
                {
                    kvp.Value.ApplyMechanic(mechanic, enable);
                    Debug.Log($"Found alternative handler for mechanic: {mechanic.mechanicId}");
                    return;
                }
            }
        }
    }

    // Get mechanic handler
    public ISpecialMechanicHandler GetMechanicHandler(string mechanicId)
    {
        if (string.IsNullOrEmpty(mechanicId)) return null;

        mechanicHandlers.TryGetValue(mechanicId.ToLower(), out var handler);
        return handler;
    }

    // Check if handler exists
    public bool HasMechanicHandler(string mechanicId)
    {
        if (string.IsNullOrEmpty(mechanicId)) return false;

        return mechanicHandlers.ContainsKey(mechanicId.ToLower());
    }

    // Get all registered mechanics
    public List<string> GetRegisteredMechanics()
    {
        return new List<string>(mechanicHandlers.Keys);
    }

    // Clear all handlers (useful for testing)
    public void ClearHandlers()
    {
        mechanicHandlers.Clear();
    }

    // Refresh handler registrations (useful when new handlers are added at runtime)
    public void RefreshHandlerRegistrations()
    {
        mechanicHandlers.Clear();
        FindAndRegisterExistingHandlers();
    }

    // Debug method to log all registered handlers
    [ContextMenu("Log Registered Handlers")]
    public void LogRegisteredHandlers()
    {
        Debug.Log("=== Registered Special Mechanic Handlers ===");
        foreach (var kvp in mechanicHandlers)
        {
            Debug.Log($"{kvp.Key} -> {kvp.Value.GetType().Name}");
        }
        Debug.Log($"Total: {mechanicHandlers.Count} handlers");
    }
}