using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Key bindings screen of the settings menu: one row per action of the player's input actions (WASD parts as their
/// own rows), for the keyboard and mouse or the gamepad. Click a key, press the new one (Escape cancels); Reset puts a
/// row back, Reset All every key. Changes are saved and applied to every player at once (<see cref="InputBindingStore"/>).
/// Rows are made from <see cref="rowTemplate"/>: a child "Action" text, a "Binding" button (with a text) and a
/// "Reset" button. Built by Tools ▸ SimpleMovements ▸ Scene UI ▸ Build Pause &amp; Settings Menu.
/// </summary>
public class KeyRebindingMenu : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Where the rows are created.")]
    [SerializeField] private Transform rowContainer;
    [Tooltip("One row (kept inactive): children 'Action' (text), 'Binding' (button with a text) and 'Reset' (button).")]
    [SerializeField] private GameObject rowTemplate;
    [Tooltip("Shows the keyboard and mouse keys.")]
    [SerializeField] private Button keyboardTab;
    [Tooltip("Shows the gamepad buttons.")]
    [SerializeField] private Button gamepadTab;
    [Tooltip("Puts every key back to its default.")]
    [SerializeField] private Button resetAllButton;
    [Tooltip("Shown while waiting for the new key ('Press a key for Jump...').")]
    [SerializeField] private GameObject waitingOverlay;
    [SerializeField] private TextMeshProUGUI waitingText;
    [Tooltip("Messages: conflicts ('also used by Dash'), saved, reset.")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Actions")]
    [Tooltip("Action map listed.")]
    [SerializeField] private string actionMap = "Player";
    [Tooltip("Actions not listed (pointer and look input cannot be rebound to a key).")]
    [SerializeField] private string[] hiddenActions = { "Look", "Point", "Click", "ScrollWheel", "MiddleClick", "RightClick", "TrackedDevicePosition", "TrackedDeviceOrientation" };

    private PlayerInput input;
    private InputActionAsset asset;
    private InputDeviceTracker.Kind shownDevice = InputDeviceTracker.Kind.KeyboardMouse;
    private InputActionRebindingExtensions.RebindingOperation operation;
    private readonly List<GameObject> rows = new List<GameObject>();
    private static int busyUntilFrame;

    /// <summary>
    /// True while waiting for a new key, and for a frame after: the pause menu ignores Escape then (it cancels the
    /// rebind instead of closing the menu).
    /// </summary>
    public static bool IsRebinding => Time.frameCount <= busyUntilFrame;

    private void Awake()
    {
        if (rowTemplate != null)
            rowTemplate.SetActive(false);
        if (waitingOverlay != null)
            waitingOverlay.SetActive(false);
        if (keyboardTab != null)
            keyboardTab.onClick.AddListener(() => Show(InputDeviceTracker.Kind.KeyboardMouse));
        if (gamepadTab != null)
            gamepadTab.onClick.AddListener(() => Show(InputDeviceTracker.Kind.Gamepad));
        if (resetAllButton != null)
            resetAllButton.onClick.AddListener(ResetAll);
    }

    private void OnEnable()
    {
        EnsureInput();
        shownDevice = InputDeviceTracker.Current;
        Rebuild();
    }

    private void OnDisable()
    {
        CancelRebind();
    }

    private void OnDestroy()
    {
        CancelRebind();
        input?.Dispose();
        input = null;
        asset = null;
    }

    private void Update()
    {
        if (operation != null)
            busyUntilFrame = Time.frameCount + 1;
    }

    private void EnsureInput()
    {
        if (asset != null)
            return;
        // A private copy of the actions: edited here, then saved and applied to every player.
        input = new PlayerInput();
        asset = input.asset;
        InputBindingStore.Apply(asset);
    }

    /// <summary>Lists the keys of one device kind.</summary>
    public void Show(InputDeviceTracker.Kind device)
    {
        shownDevice = device;
        Rebuild();
    }

    /// <summary>Rebuilds the rows (after a change).</summary>
    public void Rebuild()
    {
        foreach (GameObject r in rows)
            if (r != null) Destroy(r);
        rows.Clear();
        if (asset == null || rowContainer == null || rowTemplate == null)
            return;
        InputActionMap map = asset.FindActionMap(actionMap, false);
        if (map == null)
        {
            SetStatus($"The input actions have no '{actionMap}' map.");
            return;
        }
        SetTab(keyboardTab, shownDevice == InputDeviceTracker.Kind.KeyboardMouse);
        SetTab(gamepadTab, shownDevice == InputDeviceTracker.Kind.Gamepad);

        foreach (InputAction action in map.actions)
        {
            if (IsHidden(action.name))
                continue;
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding b = bindings[i];
                if (b.isComposite)
                    continue;
                if (!InputDeviceTracker.IsFor(b.path, shownDevice))
                    continue;
                AddRow(action, i, b.isPartOfComposite ? $"{Nice(action.name)} ({Nice(b.name)})" : Nice(action.name));
            }
        }
    }

    private void AddRow(InputAction action, int bindingIndex, string label)
    {
        GameObject row = Instantiate(rowTemplate, rowContainer);
        row.name = "Row " + label;
        row.SetActive(true);
        rows.Add(row);
        SetText(row.transform, "Action", label);
        Transform bindingT = row.transform.Find("Binding");
        if (bindingT != null)
        {
            SetText(bindingT, null, Display(action, bindingIndex));
            Button b = bindingT.GetComponent<Button>();
            if (b != null)
                b.onClick.AddListener(() => StartRebind(action, bindingIndex, label));
        }
        Transform resetT = row.transform.Find("Reset");
        if (resetT != null)
        {
            Button r = resetT.GetComponent<Button>();
            if (r != null)
                r.onClick.AddListener(() => ResetBinding(action, bindingIndex, label));
        }
    }

    private static string Display(InputAction action, int bindingIndex)
    {
        string s = action.GetBindingDisplayString(bindingIndex, default(InputBinding.DisplayStringOptions));
        return string.IsNullOrEmpty(s) ? "—" : s;
    }

    /// <summary>Waits for the new key of one binding.</summary>
    public void StartRebind(InputAction action, int bindingIndex, string label)
    {
        if (action == null || operation != null)
            return;
        action.Disable(); // a rebind needs the action off (this private copy is never enabled anyway)
        if (waitingOverlay != null)
            waitingOverlay.SetActive(true);
        if (waitingText != null)
            waitingText.text = $"Press a {(shownDevice == InputDeviceTracker.Kind.Gamepad ? "button" : "key")} for {label}\n<size=70%>Escape cancels</size>";
        busyUntilFrame = Time.frameCount + 1;

        var op = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Pointer>/position")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f);
        if (shownDevice == InputDeviceTracker.Kind.Gamepad)
            op = op.WithControlsHavingToMatchPath("<Gamepad>");
        else
            op = op.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");
        operation = op
            .OnComplete(o => FinishRebind(action, bindingIndex, label, false))
            .OnCancel(o => FinishRebind(action, bindingIndex, label, true))
            .Start();
    }

    private void FinishRebind(InputAction action, int bindingIndex, string label, bool canceled)
    {
        operation?.Dispose();
        operation = null;
        busyUntilFrame = Time.frameCount + 1;
        if (waitingOverlay != null)
            waitingOverlay.SetActive(false);
        if (canceled)
        {
            SetStatus("");
            return;
        }
        InputBindingStore.Save(asset);
        string conflict = FindConflict(action, bindingIndex);
        SetStatus(conflict != null ? $"{label}: {Display(action, bindingIndex)} is also used by {conflict}." : $"{label}: {Display(action, bindingIndex)}");
        Rebuild();
    }

    /// <summary>Another action of the map with the same key (shown as a warning; both keep working).</summary>
    private string FindConflict(InputAction changed, int bindingIndex)
    {
        string path = changed.bindings[bindingIndex].effectivePath;
        if (string.IsNullOrEmpty(path))
            return null;
        InputActionMap map = asset.FindActionMap(actionMap, false);
        if (map == null)
            return null;
        var names = new StringBuilder();
        foreach (InputAction a in map.actions)
        {
            var bindings = a.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (a == changed && i == bindingIndex)
                    continue;
                if (string.Equals(bindings[i].effectivePath, path, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (names.Length > 0) names.Append(", ");
                    names.Append(Nice(a.name));
                    break;
                }
            }
        }
        return names.Length > 0 ? names.ToString() : null;
    }

    private void ResetBinding(InputAction action, int bindingIndex, string label)
    {
        if (operation != null)
            return;
        action.RemoveBindingOverride(bindingIndex);
        InputBindingStore.Save(asset);
        SetStatus($"{label}: back to {Display(action, bindingIndex)}");
        Rebuild();
    }

    /// <summary>Every key back to its default, for every player.</summary>
    public void ResetAll()
    {
        CancelRebind();
        if (asset != null)
            asset.RemoveAllBindingOverrides();
        InputBindingStore.ResetToDefaults();
        SetStatus("All keys are back to their defaults.");
        Rebuild();
    }

    private void CancelRebind()
    {
        if (operation == null)
            return;
        operation.Cancel();
        operation?.Dispose();
        operation = null;
        if (waitingOverlay != null)
            waitingOverlay.SetActive(false);
    }

    private bool IsHidden(string actionName)
    {
        if (hiddenActions == null)
            return false;
        foreach (string h in hiddenActions)
            if (string.Equals(h, actionName, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }

    private static void SetTab(Button tab, bool selected)
    {
        if (tab == null)
            return;
        var img = tab.targetGraphic as Image;
        if (img != null)
            img.color = selected ? InventoryUIFactory.AccentColor : InventoryUIFactory.ButtonColor;
    }

    private static void SetText(Transform parent, string child, string text)
    {
        Transform t = child != null ? parent.Find(child) : parent;
        if (t == null)
            return;
        TMP_Text tmp = t.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
            tmp.text = text;
    }

    /// <summary>"OpenEmoteWheel" → "Open Emote Wheel".</summary>
    private static string Nice(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        var sb = new StringBuilder(name.Length + 6);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                sb.Append(' ');
            sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
        }
        return sb.ToString();
    }
}
