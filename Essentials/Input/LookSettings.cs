using System;
using UnityEngine;

/// <summary>
/// Mouse / stick look sensitivity and Y inversion chosen in the settings menu (saved in PlayerPrefs). The camera
/// multiplies its look input by <see cref="Apply"/>; other cameras can read the values or listen to <see cref="Changed"/>.
/// </summary>
public static class LookSettings
{
    public const string SensitivityKey = "MouseSensitivity";
    public const string InvertKey = "InvertMouse";
    /// <summary>Sensitivity that leaves the look input unchanged (the settings slider's default).</summary>
    public const float DefaultSensitivity = 2f;

    private static bool loaded;
    private static float sensitivity = DefaultSensitivity;
    private static bool invertY;

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = false;
        Changed = null;
    }

    private static void Load()
    {
        if (loaded)
            return;
        loaded = true;
        try
        {
            sensitivity = PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
            invertY = PlayerPrefs.GetInt(InvertKey, 0) == 1;
        }
        catch
        {
            sensitivity = DefaultSensitivity;
            invertY = false;
        }
    }

    /// <summary>Settings value (default 2). The look input is scaled by Sensitivity / 2.</summary>
    public static float Sensitivity
    {
        get { Load(); return sensitivity; }
        set
        {
            Load();
            sensitivity = Mathf.Max(0.01f, value);
            try { PlayerPrefs.SetFloat(SensitivityKey, sensitivity); } catch { }
            Changed?.Invoke();
        }
    }

    public static bool InvertY
    {
        get { Load(); return invertY; }
        set
        {
            Load();
            invertY = value;
            try { PlayerPrefs.SetInt(InvertKey, value ? 1 : 0); } catch { }
            Changed?.Invoke();
        }
    }

    /// <summary>Multiplier applied to look input (1 at the default sensitivity).</summary>
    public static float Multiplier => Sensitivity / DefaultSensitivity;

    /// <summary>Look input after sensitivity and inversion.</summary>
    public static Vector2 Apply(Vector2 lookDelta)
    {
        lookDelta *= Multiplier;
        if (InvertY)
            lookDelta.y = -lookDelta.y;
        return lookDelta;
    }
}
