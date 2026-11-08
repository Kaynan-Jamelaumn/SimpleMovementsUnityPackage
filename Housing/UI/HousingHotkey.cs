using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// The key that opens (and closes) the player's own housing - their leases, renewals, who they let in, their rent
/// account, messages and what is held for them: "My Housing" in Key Bindings, P by default (rebindable like every key).
/// It makes itself as soon as the city has a property or an inn, on every machine with a screen (never on a headless
/// server). A game with its own menu can call <see cref="HousingWindows.ToggleMine"/> instead and turn the key off with
/// <see cref="Enabled"/>.
/// </summary>
[AddComponentMenu("")]
[DisallowMultipleComponent]
public class HousingHotkey : MonoBehaviour
{
    /// <summary>The action's name in the input actions (and the Key Bindings menu).</summary>
    public const string ActionName = "MyHousing";
    /// <summary>Its default control.</summary>
    public const string DefaultControl = "<Keyboard>/p";

    /// <summary>False: the key does nothing (a game that opens the window from its own menu).</summary>
    public static bool Enabled = true;

    private static HousingHotkey instance;
    private GlobalInputBinding input;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        Enabled = true;
    }

    /// <summary>Makes the key's object once (it lives across scenes). Called when a property or an inn wakes up.</summary>
    public static void Ensure()
    {
        if (instance != null || !Application.isPlaying || Application.isBatchMode) return;
        var go = new GameObject("[Housing Hotkey]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<HousingHotkey>();
    }

    private void OnEnable()
    {
        input?.Dispose();
        input = GlobalInputBinding.Create(new[] { ActionName }, new[] { DefaultControl }, "My Housing", true, true, this);
    }

    private void OnDisable()
    {
        input?.Dispose();
        input = null;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (!Enabled || input == null || !input.Pressed || Typing()) return;
        // Another window (an NPC's - a realtor, an innkeeper, a shop, a dialogue -, the inventory, a station), the chat or a
        // menu has the keys: the key only closes the window it opened.
        if (!MyHousingWindow.IsOpen && (InputBindingStore.GameplayInputBlocked || OtherWindowOpen())) return;
        HousingWindows.ToggleMine();
    }

    /// <summary>A window of the local player's is open: their inventory or a storage, or one registered with it (an NPC's window).</summary>
    private static bool OtherWindowOpen()
    {
        CombatEntity p = PlayerLocator.Local;
        InventoryManager inv = p != null ? InventoryManager.For(p) : null;
        return inv != null && inv.IsAnyPanelOpen;
    }

    /// <summary>A text field has the keyboard (typing a "p" there is not the key).</summary>
    private static bool Typing()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null) return false;
        TMP_InputField tmp = selected.GetComponent<TMP_InputField>();
        if (tmp != null && tmp.isFocused) return true;
        UnityEngine.UI.InputField legacy = selected.GetComponent<UnityEngine.UI.InputField>();
        return legacy != null && legacy.isFocused;
    }
}
