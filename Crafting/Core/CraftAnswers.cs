using System;
using System.Collections.Generic;

/// <summary>
/// A station's answer to one request: done or not, and why - a refusal's reason, or a note on work only partly granted
/// ("Too fast: 4 of those 6 strokes counted."). <see cref="firstOp"/>..<see cref="lastOp"/> are the numbers the server's
/// feed gave the operations the request recorded (none when <see cref="lastOp"/> is 0): a network client raises the
/// answer only once its copy of the workshop shows them.
/// </summary>
public struct CraftAnswer
{
    /// <summary>The request's number (<see cref="CraftRequest.seq"/>).</summary>
    public int seq;
    public bool ok;
    public string reason;
    public int firstOp, lastOp;

    public CraftAnswer(int seq, bool ok, string reason, int firstOp = 0, int lastOp = 0)
    {
        this.seq = seq;
        this.ok = ok;
        this.reason = reason ?? "";
        this.firstOp = lastOp > 0 ? firstOp : 0;
        this.lastOp = Math.Max(0, lastOp);
    }

    /// <summary>Does a copy that has applied every operation up to <paramref name="applied"/> show what the request did?</summary>
    public bool ShownBy(int applied) => lastOp <= 0 || applied >= lastOp;
}

/// <summary>
/// A network client's answers that came before its copy of the workshop shows what they did. Each waits until the copy
/// has applied the last operation its request recorded (or a snapshot past it), so whoever reads the workshop when the
/// answer is raised sees the result; they are raised in the order they came. The events the copy raised while applying
/// each operation are kept a little while, so an answer can say which ones its own operations raised (a blow's ring).
/// </summary>
public sealed class CraftAnswerQueue
{
    /// <summary>Most answers kept waiting (a copy that never catches up does not pile them up for ever).</summary>
    public const int MaxWaiting = 256;
    /// <summary>The last events kept with the operation that raised them.</summary>
    public const int EventsKept = 64;

    private readonly Queue<CraftAnswer> waiting = new Queue<CraftAnswer>();
    private readonly List<(int op, CraftEvent e)> raised = new List<(int, CraftEvent)>();

    /// <summary>Answers still waiting for the copy.</summary>
    public int Count => waiting.Count;

    /// <summary>An answer came from the server.</summary>
    public void Arrived(in CraftAnswer a)
    {
        if (waiting.Count >= MaxWaiting)
            waiting.Dequeue();
        waiting.Enqueue(a);
    }

    /// <summary>
    /// The next answer whose operations a copy that has applied every operation up to <paramref name="applied"/> shows;
    /// false when there is none yet (the oldest one still waits, and the later ones wait behind it).
    /// </summary>
    public bool Next(int applied, out CraftAnswer a)
    {
        if (waiting.Count > 0 && waiting.Peek().ShownBy(applied))
        {
            a = waiting.Dequeue();
            return true;
        }
        a = default;
        return false;
    }

    /// <summary>The copy raised <paramref name="e"/> while applying operation <paramref name="op"/> (0: while its clock ran - not kept).</summary>
    public void Raised(int op, in CraftEvent e)
    {
        if (op <= 0)
            return;
        raised.Add((op, e));
        if (raised.Count > EventsKept)
            raised.RemoveRange(0, raised.Count - EventsKept);
    }

    /// <summary>The events the operations of <paramref name="a"/> raised on the copy (in <paramref name="into"/>, cleared first).</summary>
    public void EventsOf(in CraftAnswer a, List<CraftEvent> into)
    {
        into.Clear();
        if (a.lastOp <= 0)
            return;
        foreach ((int op, CraftEvent e) in raised)
            if (op >= a.firstOp && op <= a.lastOp)
                into.Add(e);
    }

    /// <summary>Forgets everything (the client stopped following the station).</summary>
    public void Clear()
    {
        waiting.Clear();
        raised.Clear();
    }
}

/// <summary>
/// The requests a station window sent and is waiting to hear about, each with what to do with its answer. They are
/// numbered from 1 (never 0, which asks for no answer) and answered exactly once: with the station's answer, or with
/// (false, <see cref="NoAnswer"/>) after <see cref="Timeout"/> seconds or when the window closes. That one is not a
/// refusal: the outcome is unknown (the server may well have done the work - the window closed before the answer came),
/// so work an area keeps for a retry is kept only on a real refusal (<c>!ok &amp;&amp; reason != NoAnswer</c>), never on
/// this one.
/// </summary>
public sealed class CraftAnswerWaits
{
    /// <summary>Seconds a request is waited for.</summary>
    public const double Timeout = 10;
    /// <summary>
    /// What a request that was never answered is answered: its outcome is unknown, not refused (the station may have done
    /// it), so the work it carried is not kept for another try.
    /// </summary>
    public const string NoAnswer = "No answer from the station.";

    private struct Wait
    {
        public int seq;
        public double sent;
        public Action<bool, string> answered;
    }

    private readonly List<Wait> waits = new List<Wait>();
    private int last;

    /// <param name="numberFrom">
    /// The requests are numbered from the one after this: a second waiter for the same station's answers (the 3D work
    /// place beside the window) numbers in a range of its own, so neither takes the other's answers.
    /// </param>
    public CraftAnswerWaits(int numberFrom = 0)
    {
        last = Math.Max(0, Math.Min(int.MaxValue - 2, numberFrom));
    }

    /// <summary>Requests still waited for.</summary>
    public int Count => waits.Count;

    /// <summary>Is the request <paramref name="seq"/> still waited for?</summary>
    public bool Waiting(int seq) => IndexOf(seq) >= 0;

    /// <summary>A number for a request about to be sent at <paramref name="now"/>; <paramref name="answered"/> (may be null) gets its answer.</summary>
    public int Expect(Action<bool, string> answered, double now)
    {
        do
            last = last >= int.MaxValue - 1 ? 1 : last + 1;
        while (IndexOf(last) >= 0);
        waits.Add(new Wait { seq = last, sent = now, answered = answered });
        return last;
    }

    /// <summary>The request was not sent after all: it is forgotten, and nothing is called.</summary>
    public void Forget(int seq)
    {
        int i = IndexOf(seq);
        if (i >= 0)
            waits.RemoveAt(i);
    }

    /// <summary>The answer to <paramref name="seq"/> came: its callback runs (once). False when it is not waited for (late, or not ours).</summary>
    public bool Answer(int seq, bool ok, string reason)
    {
        int i = IndexOf(seq);
        if (i < 0)
            return false;
        Action<bool, string> answered = waits[i].answered;
        waits.RemoveAt(i);
        answered?.Invoke(ok, reason ?? "");
        return true;
    }

    /// <summary>The requests waited for longer than <see cref="Timeout"/> at <paramref name="now"/> are answered (false, <see cref="NoAnswer"/>); returns how many.</summary>
    public int Expire(double now)
    {
        List<Wait> due = null;
        for (int i = 0; i < waits.Count; i++)
            if (now - waits[i].sent >= Timeout)
                (due ??= new List<Wait>()).Add(waits[i]);
        if (due == null)
            return 0;
        waits.RemoveAll(w => now - w.sent >= Timeout);
        return Run(due);
    }

    /// <summary>The window closes: every request still waited for is answered (false, <see cref="NoAnswer"/>); returns how many.</summary>
    public int DropAll()
    {
        if (waits.Count == 0)
            return 0;
        var all = new List<Wait>(waits);
        waits.Clear();
        return Run(all);
    }

    /// <summary>Answers the requests given up on, the oldest first (a callback may send, and wait for, a new request meanwhile).</summary>
    private static int Run(List<Wait> list)
    {
        foreach (Wait w in list)
            w.answered?.Invoke(false, NoAnswer);
        return list.Count;
    }

    private int IndexOf(int seq)
    {
        if (seq <= 0)
            return -1;
        for (int i = 0; i < waits.Count; i++)
            if (waits[i].seq == seq) return i;
        return -1;
    }
}
