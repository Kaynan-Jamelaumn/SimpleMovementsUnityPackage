using UnityEngine;

/// <summary>
/// Lets a full-screen activity (a card table, a contest) take the screen: while one holds the focus, the gameplay HUD
/// that would sit on top of it (the hotbar, the quick slots, the ability bar, the city and zone messages) hides. The
/// activity releases it when the inventory opens, so those come back while the inventory is open.
/// </summary>
public static class HudFocus
{
    private static int holders;

    /// <summary>Should the gameplay HUD step aside now?</summary>
    public static bool Hidden => holders > 0;

    public static void Take() => holders++;

    public static void Release()
    {
        if (holders > 0)
            holders--;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => holders = 0;

    /// <summary>Shows or hides <paramref name="go"/> with a CanvasGroup (its scripts keep running).</summary>
    public static void Apply(GameObject go, bool visible)
    {
        if (go == null)
            return;
        CanvasGroup g = go.GetComponent<CanvasGroup>();
        if (g == null)
        {
            if (visible)
                return; // never hidden: nothing to add
            g = go.AddComponent<CanvasGroup>();
        }
        float a = visible ? 1f : 0f;
        if (g.alpha != a) g.alpha = a;
        g.blocksRaycasts = visible;
        g.interactable = visible;
    }
}
