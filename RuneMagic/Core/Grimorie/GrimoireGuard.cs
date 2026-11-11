using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RuneMagic
{
    /// <summary>The grimoire actions that can be limited.</summary>
    public enum GrimoireAction
    {
        Imprint = 0,
        Absorb = 1,
        Erase = 2,
    }

    /// <summary>
    /// Keeps one Mage from copying runes faster than the balance allows (a cooldown between two imprints, two absorbs, and a count per hour):
    /// copying runes costs energy and a slot, so a loop of imprint and absorb is how a bug or a script would duplicate runes. Pure: the caller
    /// gives the time (seconds, rising). One guard per soul, kept in memory (a limit that resets when the game restarts is no weaker than
    /// the cooldown the Mage waits through anyway).
    /// </summary>
    public sealed class GrimoireGuard
    {
        private const double Hour = 3600.0;
        private readonly Dictionary<GrimoireAction, Queue<double>> recent = new Dictionary<GrimoireAction, Queue<double>>();
        private readonly Dictionary<GrimoireAction, double> last = new Dictionary<GrimoireAction, double>();

        /// <summary>May <paramref name="action"/> be done now? When not: why, and how many seconds until it may.</summary>
        public bool Allow(GrimoireBalance g, GrimoireAction action, double now, out string reason, out float wait)
        {
            reason = "";
            wait = 0f;
            if (g == null || action == GrimoireAction.Erase) return true;
            float cooldown = action == GrimoireAction.Imprint ? g.imprintCooldownSeconds : g.absorbCooldownSeconds;
            int perHour = action == GrimoireAction.Imprint ? g.maxImprintsPerHour : g.maxAbsorbsPerHour;
            string verb = action == GrimoireAction.Imprint ? "imprint" : "absorb";
            if (cooldown > 0f && last.TryGetValue(action, out double at) && now - at < cooldown)
            {
                wait = (float)(cooldown - (now - at));
                reason = $"Your hand is still steadying: you can {verb} again in {Math.Ceiling(wait):0} second(s).";
                return false;
            }
            if (perHour > 0)
            {
                Queue<double> q = Queue(action);
                while (q.Count > 0 && now - q.Peek() >= Hour) q.Dequeue();
                if (q.Count >= perHour)
                {
                    wait = (float)(Hour - (now - q.Peek()));
                    reason = $"You have done all you can {verb} for now: {perHour} in an hour. Try again in {Math.Ceiling(wait / 60f):0} minute(s).";
                    return false;
                }
            }
            return true;
        }

        /// <summary>Notes that <paramref name="action"/> was done at <paramref name="now"/>.</summary>
        public void Record(GrimoireAction action, double now)
        {
            if (action == GrimoireAction.Erase) return;
            last[action] = now;
            Queue(action).Enqueue(now);
        }

        /// <summary>How many of the action were done in the last hour.</summary>
        public int CountInLastHour(GrimoireAction action, double now)
        {
            Queue<double> q = Queue(action);
            while (q.Count > 0 && now - q.Peek() >= Hour) q.Dequeue();
            return q.Count;
        }

        public void Clear()
        {
            recent.Clear();
            last.Clear();
        }

        private Queue<double> Queue(GrimoireAction a)
        {
            if (!recent.TryGetValue(a, out Queue<double> q)) recent[a] = q = new Queue<double>();
            return q;
        }
    }

    /// <summary>The audit log of a book as text (who imprinted, absorbed, erased or was refused, and when).</summary>
    public static class GrimoireAudit
    {
        public static string Text(GrimoireData book, int last = 50)
        {
            if (book == null) return "";
            var sb = new StringBuilder();
            sb.Append($"Grimoire {book.serial} of {book.authorName}: {book.FilledCount()} of {book.pages.Count} pages written, {book.log.Count} log entries\n");
            int from = Math.Max(0, book.log.Count - Math.Max(1, last));
            for (int i = from; i < book.log.Count; i++)
            {
                GrimoireLogEntry e = book.log[i];
                string when = new DateTime(Math.Max(DateTime.MinValue.Ticks, Math.Min(DateTime.MaxValue.Ticks, e.utc)), DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                sb.Append($"  {when} {e.action} by {e.playerName} ({e.playerKey}) page {e.page + 1}");
                if (!string.IsNullOrEmpty(e.runeId)) sb.Append($" {e.runeId} r{e.rank} q{e.quality.ToString("0", CultureInfo.InvariantCulture)}");
                if (!string.IsNullOrEmpty(e.note)) sb.Append(" — " + e.note);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>An entry for an action on <paramref name="page"/> (the rune of the print, when there is one).</summary>
        public static GrimoireLogEntry Entry(string action, string playerKey, string playerName, int page, RuneInstance print, long utc, string note = "")
        {
            return new GrimoireLogEntry
            {
                utc = utc, action = action ?? "", playerKey = playerKey ?? "", playerName = playerName ?? "", page = page,
                runeId = print != null ? (print.IsComposite ? "composite:" + print.composite.name : print.runeId) : "",
                rank = print != null ? print.rank : 0, quality = print != null ? print.quality : 0f, note = note ?? "",
            };
        }
    }
}
