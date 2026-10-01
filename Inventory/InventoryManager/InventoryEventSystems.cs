using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Keeps exactly one active EventSystem at runtime. UI clicks need one; two of them (one in the scene and one
/// inside a player prefab, a duplicated player...) make Unity log "There are 2 event systems in the scene" every
/// frame and send events twice. Extra ones are turned off (not destroyed) and named in a warning.
/// </summary>
public static class InventoryEventSystems
{
    public static void EnsureSingle()
    {
        EventSystem[] all = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude);
        if (all.Length == 0)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            return;
        }

        int enabledCount = 0;
        foreach (EventSystem es in all)
            if (es.enabled) enabledCount++;
        if (enabledCount <= 1)
            return;

        EventSystem keep = EventSystem.current != null && EventSystem.current.enabled ? EventSystem.current : null;
        foreach (EventSystem es in all)
        {
            if (keep == null && es.enabled) keep = es;
        }
        foreach (EventSystem es in all)
        {
            if (es == keep || !es.enabled)
                continue;
            es.enabled = false;
            foreach (BaseInputModule module in es.GetComponents<BaseInputModule>())
                module.enabled = false;
            Debug.LogWarning($"[Inventory] More than one EventSystem was active: turned off the one on '{Path(es.transform)}' and kept '{Path(keep.transform)}'. " +
                             "Remove the extra EventSystem from the scene or prefab (keep a single one in the scene).", es);
        }
    }

    private static string Path(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
