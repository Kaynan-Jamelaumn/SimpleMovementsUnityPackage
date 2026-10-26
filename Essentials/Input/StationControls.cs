using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>What a crafting or alchemy station worked with the hands is controlled with.</summary>
public enum StationControl
{
    /// <summary>The hand: hold to raise the hammer, press the blade to the wheel, dip the spoon...</summary>
    Work,
    /// <summary>Put the tool down and go back to the station's window.</summary>
    PutDown,
    /// <summary>Where the hand is on the screen (a value: the pointer's position).</summary>
    Point,
    /// <summary>The wheel: the next tool, the angle (a value: the scroll).</summary>
    Scroll,
    /// <summary>Move the hand with a stick (a value: a gamepad moves a virtual pointer).</summary>
    MoveHand,
    /// <summary>The next tool, stone or face.</summary>
    NextTool,
    /// <summary>A quick action: a squeeze of the bellows, a dip in the tank, a blow on the mortar.</summary>
    Quick,
    /// <summary>Turn the piece over.</summary>
    TurnOver,
    /// <summary>The piece into the fire and back.</summary>
    Fire,
    /// <summary>Strike on the face or on the edge.</summary>
    FaceEdge,
    /// <summary>Feed the fire.</summary>
    FeedFire,
    VentClose,
    VentOpen,
    /// <summary>Done: tip the mortar into the vessel.</summary>
    Confirm,
    /// <summary>In the station's window: work this area with the hands, in 3D.</summary>
    ByHand,
    Choice1, Choice2, Choice3, Choice4, Choice5, Choice6, Choice7, Choice8, Choice9, Choice10,
}

/// <summary>
/// The keys of the stations worked with the hands (the forge, the anvil, the wheel, the whetstone, the quench tank, the
/// crucible, the alchemy vessel, mortar and filter), as input actions of their own <see cref="Map"/>: found by name in
/// the player's input actions (add a "Station" map with actions named Station… to choose them in the asset), else
/// created with the defaults below. Either way they are rebindable in Settings ▸ Key Bindings, saved, shown in every
/// hint by the key actually bound (<see cref="Key"/>), and quiet while the game is paused.
/// <para>While the hands are at work the player's gameplay keys are off (<see cref="BeginHandsOn"/>), so the keys of
/// the anvil never also jump, dash, open the inventory or cast an ability.</para>
/// </summary>
public static class StationControls
{
    /// <summary>The action map of the station keys.</summary>
    public const string Map = "Station";

    private struct Def
    {
        public string action, label;
        public string[] controls;
        public bool value;
    }

    private static readonly Def[] Defs =
    {
        Button("StationWork", "Work (hold)", "<Mouse>/leftButton", "<Gamepad>/rightTrigger"),
        Button("StationPutDown", "Put the tool down", "<Mouse>/rightButton", "<Keyboard>/q", "<Gamepad>/buttonEast"),
        Value("StationPoint", "Hand position", "<Pointer>/position"),
        Value("StationScroll", "Wheel", "<Mouse>/scroll"),
        Value("StationMoveHand", "Move the hand", "<Gamepad>/leftStick"),
        Button("StationNextTool", "Next tool / face", "<Keyboard>/tab", "<Gamepad>/rightShoulder"),
        Button("StationQuick", "Quick action", "<Keyboard>/space", "<Gamepad>/buttonSouth"),
        Button("StationTurnOver", "Turn the piece over", "<Keyboard>/t", "<Gamepad>/buttonNorth"),
        Button("StationFire", "Into the fire and out", "<Keyboard>/r", "<Gamepad>/buttonWest"),
        Button("StationFaceEdge", "Strike on face / edge", "<Keyboard>/f", "<Gamepad>/leftShoulder"),
        Button("StationFeedFire", "Feed the fire", "<Keyboard>/f", "<Gamepad>/leftShoulder"),
        Button("StationVentClose", "Close the air vent", "<Keyboard>/leftBracket", "<Gamepad>/dpad/down"),
        Button("StationVentOpen", "Open the air vent", "<Keyboard>/rightBracket", "<Gamepad>/dpad/up"),
        Button("StationConfirm", "Done (tip it in)", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/start"),
        Button("StationByHand", "Work it by hand (3D)", "<Keyboard>/h", "<Gamepad>/leftStickPress"),
        Button("StationChoice1", "Choice 1", "<Keyboard>/1"),
        Button("StationChoice2", "Choice 2", "<Keyboard>/2"),
        Button("StationChoice3", "Choice 3", "<Keyboard>/3"),
        Button("StationChoice4", "Choice 4", "<Keyboard>/4"),
        Button("StationChoice5", "Choice 5", "<Keyboard>/5"),
        Button("StationChoice6", "Choice 6", "<Keyboard>/6"),
        Button("StationChoice7", "Choice 7", "<Keyboard>/7"),
        Button("StationChoice8", "Choice 8", "<Keyboard>/8"),
        Button("StationChoice9", "Choice 9", "<Keyboard>/9"),
        Button("StationChoice10", "Choice 10", "<Keyboard>/0"),
    };

    private static GlobalInputBinding[] bindings;
    private static readonly HashSet<object> handsOn = new HashSet<object>(ReferenceComparer<object>.Instance);
    private static Vector2 virtualPointer = new Vector2(-1f, -1f);
    private static int virtualFrame = -1;

    private static Def Button(string action, string label, params string[] controls) => new Def { action = action, label = label, controls = controls };
    private static Def Value(string action, string label, params string[] controls) => new Def { action = action, label = label, controls = controls, value = true };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        bindings = null; // GlobalInputBinding forgets its live inputs at the same time
        handsOn.Clear();
        virtualPointer = new Vector2(-1f, -1f);
        virtualFrame = -1;
    }

    private static GlobalInputBinding Get(StationControl c)
    {
        if (bindings == null)
        {
            bindings = new GlobalInputBinding[Defs.Length];
            for (int i = 0; i < Defs.Length; i++)
            {
                Def d = Defs[i];
                bindings[i] = d.value
                    ? GlobalInputBinding.CreateIn(Map, new[] { d.action }, d.controls, d.label, InputActionType.Value, "Vector2")
                    : GlobalInputBinding.CreateIn(Map, new[] { d.action }, d.controls, d.label);
            }
        }
        int k = (int)c;
        return k >= 0 && k < bindings.Length ? bindings[k] : null;
    }

    public static bool Pressed(StationControl c) => Get(c)?.Pressed ?? false;
    public static bool Held(StationControl c) => Get(c)?.Held ?? false;
    public static bool Released(StationControl c) => Get(c)?.Released ?? false;

    /// <summary>The key or button bound to <paramref name="c"/> for the device in use ("T", "Left Button", "Right Shoulder").</summary>
    public static string Key(StationControl c)
    {
        string s = Get(c)?.KeyLabel;
        return string.IsNullOrEmpty(s) ? "—" : s;
    }

    /// <summary>The key as it is written in hints: bold and coloured.</summary>
    public static string K(StationControl c) => $"<b><color=#FFD27A>{Key(c)}</color></b>";

    /// <summary>"1-5": the keys of the first <paramref name="count"/> choices.</summary>
    public static string Choices(int count)
    {
        count = Mathf.Clamp(count, 1, 10);
        return count == 1 ? K(StationControl.Choice1) : $"<b><color=#FFD27A>{Key(StationControl.Choice1)}-{Key((StationControl)((int)StationControl.Choice1 + count - 1))}</color></b>";
    }

    /// <summary>The choice key pressed this frame (0 = the first), among the first <paramref name="count"/>; -1 when none.</summary>
    public static int ChoicePressed(int count)
    {
        count = Mathf.Clamp(count, 0, 10);
        for (int i = 0; i < count; i++)
            if (Pressed((StationControl)((int)StationControl.Choice1 + i)))
                return i;
        return -1;
    }

    /// <summary>The scroll this frame (up is positive).</summary>
    public static float Scroll => Get(StationControl.Scroll)?.ReadVector2().y ?? 0f;

    /// <summary>
    /// Where the hand is on the screen (pixels): the pointer, or - with a gamepad - a pointer the stick moves (from the
    /// centre of the screen when the hands are taken up).
    /// </summary>
    public static Vector2 Pointer
    {
        get
        {
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            if (virtualPointer.x < 0f)
                virtualPointer = screen * 0.5f;
            if (InputDeviceTracker.Current == InputDeviceTracker.Kind.Gamepad)
            {
                if (virtualFrame != Time.frameCount)
                {
                    virtualFrame = Time.frameCount;
                    Vector2 stick = Get(StationControl.MoveHand)?.ReadVector2() ?? Vector2.zero;
                    virtualPointer += stick * (Mathf.Min(screen.x, screen.y) * 0.9f * Time.unscaledDeltaTime);
                    virtualPointer = Vector2.Min(Vector2.Max(virtualPointer, Vector2.zero), screen);
                }
                return virtualPointer;
            }
            GlobalInputBinding p = Get(StationControl.Point);
            if (p == null || p.ReadAction == null)
                return screen * 0.5f;
            virtualPointer = p.ReadVector2();
            return virtualPointer;
        }
    }

    /// <summary>
    /// <paramref name="owner"/> takes up a station's tools: the station keys are made ready and the player's gameplay
    /// keys go off until <see cref="EndHandsOn"/>.
    /// </summary>
    public static void BeginHandsOn(object owner)
    {
        if (owner == null)
            return;
        Get(StationControl.Work);
        if (handsOn.Count == 0)
            virtualPointer = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        handsOn.Add(owner);
        InputBindingStore.BlockGameplayInput(owner);
    }

    public static void EndHandsOn(object owner)
    {
        if (owner == null)
            return;
        handsOn.Remove(owner);
        InputBindingStore.UnblockGameplayInput(owner);
    }
}
