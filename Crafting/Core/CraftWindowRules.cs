using System;

/// <summary>
/// Small timing rules of the station window, kept apart from Unity so that they can be tested: how long a message stays
/// in the message row (<see cref="MessageSeconds"/>), the second click that confirms what cannot be undone
/// (<see cref="CraftConfirm"/>) and the pace at which a setting the hand turns is sent (<see cref="CraftSettingPace"/>).
/// </summary>
public static class CraftWindowRules
{
    /// <summary>A message stays at least this long (seconds)...</summary>
    public const float MessageMinimum = 6f;
    /// <summary>...or, for a long one, this long plus <see cref="MessagePerCharacter"/> for each character seen.</summary>
    public const float MessageBase = 2f;
    public const float MessagePerCharacter = 0.06f;

    /// <summary>How long a message stays in the message row: long enough to read it - max(6 s, 2 s + 60 ms a character).</summary>
    public static float MessageSeconds(string text) =>
        Math.Max(MessageMinimum, MessageBase + MessagePerCharacter * VisibleLength(text));

    /// <summary>The characters of a rich text that are seen (its &lt;tags&gt; left out).</summary>
    public static int VisibleLength(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;
        int n = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                int close = text.IndexOf('>', i + 1), next = text.IndexOf('<', i + 1);
                if (close > i && (next < 0 || close < next))
                {
                    i = close;
                    continue;
                }
            }
            n++;
        }
        return n;
    }
}

/// <summary>
/// What cannot be undone is asked twice: <see cref="Ask"/> is true on the second click on the same thing within
/// <see cref="Window"/> seconds; the first click (or a click on something else, or one too late) only asks again.
/// </summary>
public sealed class CraftConfirm
{
    /// <summary>Seconds the question stands.</summary>
    public const float Window = 2.5f;

    private string key;
    private float askedAt;

    /// <summary>A click on <paramref name="what"/> at <paramref name="now"/>: true when it confirms the click before.</summary>
    public bool Ask(string what, float now)
    {
        if (what != null && what == key && now >= askedAt && now - askedAt <= Window)
        {
            key = null;
            return true;
        }
        key = what;
        askedAt = now;
        return false;
    }

    /// <summary>Forgets the question (the window closed).</summary>
    public void Clear() => key = null;
}

/// <summary>
/// The pace of a setting the hand turns (an air vent, a vat's fire, the oven's dial): a change is sent at once, then at
/// most <see cref="PerSecond"/> times a second while the hand moves it, and the last value the moment the hand lets go.
/// The workshop's value is not shown on the control while the hand holds it, while a value waits to be sent, nor until
/// the workshop shows the value sent (or <see cref="Patience"/> seconds have passed) - so the handle never jumps back
/// under the hand while the change is on its way.
/// </summary>
public sealed class CraftSettingPace
{
    /// <summary>Sends a second at most while the hand moves it.</summary>
    public const float PerSecond = 5f;
    /// <summary>Seconds the control waits for the workshop to show the value sent.</summary>
    public const float Patience = 1.5f;

    private bool held, waiting, awaiting;
    private float value, sent, sentAt = float.NegativeInfinity;

    /// <summary>Is the hand on the control?</summary>
    public bool Held => held;
    /// <summary>The last value sent.</summary>
    public float Sent => sent;

    /// <summary>The hand took the control.</summary>
    public void Grab() => held = true;

    /// <summary>The control moved to <paramref name="v"/> (by the hand, or the keys).</summary>
    public void Changed(float v)
    {
        value = v;
        waiting = true;
    }

    /// <summary>The hand let go: true (with the value) when a value is still to be sent - it is sent now.</summary>
    public bool LetGo(float now, out float v)
    {
        held = false;
        v = value;
        if (!waiting)
            return false;
        Send(now);
        return true;
    }

    /// <summary>Is a value due to be sent at <paramref name="now"/> (asked after each change and once a frame)?</summary>
    public bool Due(float now, out float v)
    {
        v = value;
        if (!waiting || now - sentAt < 1f / PerSecond)
            return false;
        Send(now);
        return true;
    }

    /// <summary>
    /// May the control show the workshop's value <paramref name="current"/> now? Not while the hand holds it or a value
    /// waits; after a send, only once the workshop shows it (within <paramref name="tolerance"/>) or <see cref="Patience"/> passed.
    /// </summary>
    public bool MayShow(float now, float current, float tolerance)
    {
        if (held || waiting)
            return false;
        if (awaiting && (Math.Abs(current - sent) <= tolerance || now - sentAt >= Patience))
            awaiting = false;
        return !awaiting;
    }

    private void Send(float now)
    {
        waiting = false;
        sent = value;
        sentAt = now;
        awaiting = true;
    }
}
