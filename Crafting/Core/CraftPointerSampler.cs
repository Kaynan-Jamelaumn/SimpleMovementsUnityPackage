using System;
using UnityEngine;

/// <summary>What the pointer did on the work surface (sampled by <see cref="CraftPointerSampler"/>).</summary>
public enum CraftPointer
{
    /// <summary>The left button went down on the surface: a gesture begins, and the left button owns it until it comes up.</summary>
    Down,
    /// <summary>The held pointer moved: a sample every few pixels of the way (<see cref="CraftPointerSampler.MinStep"/> to <see cref="CraftPointerSampler.MaxStep"/>).</summary>
    Drag,
    /// <summary>The gesture's button came up (on the surface or off it).</summary>
    Up,
    /// <summary>The pointer moved over the surface with no gesture under way (sampled like a drag).</summary>
    Move,
    /// <summary>One notch of the wheel: the scroll value is +1 (turned away from you) or -1 (toward you).</summary>
    Scroll,
    /// <summary>
    /// The right button was pressed: put down or abort what the hands are doing - never commit, never destroy. Reported
    /// once, also in the middle of a gesture (whose own Drag and Up still follow: an area that aborted ignores them).
    /// </summary>
    Cancel,
}

/// <summary>Which button pressed (a finger on a touch screen is the left button).</summary>
public enum CraftPointerButton { Left, Right, Middle }

/// <summary>
/// The pointer on the station window's work surface, turned into what the hands did. These are the rules every work
/// area relies on, kept apart from Unity's event system so that they can be tested:
/// <list type="bullet">
/// <item><b>One gesture per button</b>: the left button's press begins a gesture (Down) that it owns until it comes up
/// (Up); no other button's press or release ends it or begins another. The right button never begins a gesture: its
/// press is reported once as <see cref="CraftPointer.Cancel"/>. The middle button does nothing.</item>
/// <item><b>Sampled by distance</b>, not once per rendered frame: a moving pointer gives a sample once it is
/// <see cref="MinStep"/> pixels from the last one, and a longer jump (a fast hand, a slow frame) is filled in with
/// samples at most <see cref="MaxStep"/> apart, each with its own time (<see cref="SampleTime"/>), so the work of the hands
/// hardly depends on the monitor's refresh rate. There is no drag threshold: the first pixels of a stroke count.</item>
/// <item><b>The wheel in whole notches</b>: +1 or -1 a notch, at most one a frame; a trackpad's small steps add up to a
/// notch, and a fast spin does not run on for long (<see cref="MaxNotchesWaiting"/>).</item>
/// </list>
/// </summary>
public sealed class CraftPointerSampler
{
    /// <summary>Samples of a moving pointer are at least this far apart (surface pixels)...</summary>
    public const float MinStep = 2f;
    /// <summary>...and a longer jump is filled in with samples at most this far apart.</summary>
    public const float MaxStep = 6f;
    /// <summary>Notches of the wheel kept waiting beyond the one a frame delivers.</summary>
    public const int MaxNotchesWaiting = 2;
    /// <summary>A part of a notch left over this long (seconds) is forgotten.</summary>
    public const float NotchMemory = 0.5f;

    /// <summary>What the hands did: (phase, where in surface pixels, the sample's time, notches of the wheel).</summary>
    public event Action<CraftPointer, Vector2, float, float> Sampled;

    /// <summary>Is a gesture under way (the left button held since it pressed on the surface)?</summary>
    public bool Held { get; private set; }
    /// <summary>The time of the sample being reported (a filled-in sample of a long jump has its own, between the frames).</summary>
    public float SampleTime { get; private set; }

    private int owner;
    private Vector2 dragAt, moveAt, scrollAt;
    private float dragRawAt, moveRawAt, scrolledAt = float.NegativeInfinity;
    private bool moveKnown;
    private float notches, learntNotch;
    private int notchFrame = int.MinValue;

    /// <summary>
    /// A button went down at <paramref name="at"/> at <paramref name="time"/>; <paramref name="pointer"/> tells the mouse
    /// from a finger. The left button begins a gesture (unless one is under way), the right one reports Cancel.
    /// </summary>
    public void Press(CraftPointerButton button, int pointer, Vector2 at, float time)
    {
        if (button == CraftPointerButton.Middle)
            return; // the middle button does nothing on the surface
        if (button == CraftPointerButton.Right)
        {
            Emit(CraftPointer.Cancel, at, time, 0f); // put down or abort: never a gesture of its own
            return;
        }
        if (Held)
        {
            if (pointer != owner)
                return; // another finger while the first holds: the gesture stays the first one's
            // The same pointer pressed again: its release never came (the window lost the focus) - that gesture ended there.
            Held = false;
            Emit(CraftPointer.Up, dragAt, time, 0f);
        }
        Held = true;
        owner = pointer;
        dragAt = at;
        dragRawAt = time;
        Emit(CraftPointer.Down, at, time, 0f);
    }

    /// <summary>
    /// The held pointer moved to <paramref name="at"/> at <paramref name="time"/> (<paramref name="frame"/>: the seconds
    /// since the last frame, when the hand made that way): Drag samples, if it is the gesture's own pointer.
    /// </summary>
    public void Drag(int pointer, Vector2 at, float time, float frame)
    {
        if (!Held || pointer != owner)
            return;
        Along(CraftPointer.Drag, ref dragAt, ref dragRawAt, at, time, frame);
    }

    /// <summary>The pointer moved over the surface with no gesture under way: Move samples.</summary>
    public void Hover(Vector2 at, float time, float frame)
    {
        if (Held)
            return;
        if (!moveKnown)
        {
            // Just come over the surface: a first sample where it is, nothing filled in from where it left.
            moveKnown = true;
            moveAt = at;
            moveRawAt = time;
            Emit(CraftPointer.Move, at, time, 0f);
            return;
        }
        Along(CraftPointer.Move, ref moveAt, ref moveRawAt, at, time, frame);
    }

    /// <summary>The pointer left the surface (no gesture under way): the next Move starts afresh where it comes back.</summary>
    public void Exit()
    {
        if (!Held)
            moveKnown = false;
    }

    /// <summary>
    /// A button came up at <paramref name="at"/>: only the gesture's own button ends it - the last of the way it went is
    /// sampled, then Up. <paramref name="overSurface"/>: it came up on the surface (else the next Move starts afresh).
    /// </summary>
    public void Release(CraftPointerButton button, int pointer, Vector2 at, float time, float frame, bool overSurface = true)
    {
        if (!Held || button != CraftPointerButton.Left || pointer != owner)
            return;
        Along(CraftPointer.Drag, ref dragAt, ref dragRawAt, at, time, frame);
        Held = false;
        moveKnown = overSurface;
        moveAt = at;
        moveRawAt = time;
        Emit(CraftPointer.Up, at, time, 0f);
    }

    /// <summary>The surface went away (the window closed or stepped aside): no gesture is under way any more, no notch waits.</summary>
    public void Reset()
    {
        Held = false;
        moveKnown = false;
        notches = 0f;
    }

    /// <summary>
    /// The wheel turned by <paramref name="delta"/> as the input module reports it, over <paramref name="at"/>, in frame
    /// <paramref name="frameCount"/>. <paramref name="notch"/> is what one notch reports (0 when it cannot be told: the
    /// largest single step seen is taken for a notch). Delivers a notch at once unless one was delivered this frame.
    /// </summary>
    public void Scroll(float delta, float notch, Vector2 at, float time, int frameCount)
    {
        if (delta == 0f || float.IsNaN(delta) || float.IsInfinity(delta))
            return;
        if (time - scrolledAt > NotchMemory)
            notches = (float)Math.Truncate(notches); // a part of a notch left from long ago is forgotten
        scrolledAt = time;
        learntNotch = Math.Max(learntNotch, Math.Abs(delta));
        float size = notch > 0f ? notch : Math.Max(1f, learntNotch);
        float step = delta / size;
        if (notches != 0f && Math.Sign(step) != Math.Sign(notches))
            notches = 0f; // turned back: what was left of the other way is dropped
        notches = Math.Max(-(MaxNotchesWaiting + 1), Math.Min(MaxNotchesWaiting + 1, notches + step));
        scrollAt = at;
        Tick(time, frameCount);
    }

    /// <summary>Once a frame: a whole notch still waiting is delivered (one a frame).</summary>
    public void Tick(float time, int frameCount)
    {
        if (frameCount == notchFrame || Math.Abs(notches) < 1f - 1e-4f)
            return;
        float s = Math.Sign(notches);
        notches -= s;
        notchFrame = frameCount;
        Emit(CraftPointer.Scroll, scrollAt, time, s);
    }

    /// <summary>
    /// The samples of a pointer that went from the last sample <paramref name="from"/> to <paramref name="to"/>: none until
    /// it is <see cref="MinStep"/> away, then evenly spaced at most <see cref="MaxStep"/> apart, their times spread over the
    /// last frame (when the hand made that way), the last one at <paramref name="time"/>.
    /// </summary>
    private void Along(CraftPointer phase, ref Vector2 from, ref float rawAt, Vector2 to, float time, float frame)
    {
        float t0 = Math.Min(time, Math.Max(rawAt, time - Math.Max(0f, frame)));
        rawAt = time;
        float d = Vector2.Distance(from, to);
        if (d < MinStep)
            return;
        int n = Math.Max(1, (int)Math.Ceiling(d / MaxStep - 1e-4f));
        Vector2 start = from;
        for (int k = 1; k <= n; k++)
        {
            float f = k / (float)n;
            from = k == n ? to : start + (to - start) * f;
            Emit(phase, from, k == n ? time : t0 + (time - t0) * f, 0f);
        }
    }

    private void Emit(CraftPointer phase, Vector2 at, float time, float scroll)
    {
        SampleTime = time;
        Sampled?.Invoke(phase, at, time, scroll);
    }
}
