using System;
using UnityEngine;

namespace HubWorld
{
    public enum HubMessageKind
    {
        /// <summary>A short line above the hotbar.</summary>
        Info,
        /// <summary>A short line, highlighted.</summary>
        Warning,
        /// <summary>A large banner in the middle of the screen (cycle warnings, the jump, zone changes).</summary>
        Banner,
    }

    /// <summary>
    /// Messages from the hub world to players ("The city is about to jump dimensions!", "Entering Dangerous Zone - PvP
    /// active"). The server side calls <see cref="Send"/>; the HUD (or your UI) listens to <see cref="Sent"/> and shows
    /// what is for this machine's player.
    /// </summary>
    /// <remarks>
    /// In a network session the server's messages also travel to the clients: the networking layer installs
    /// <see cref="Remote"/>, which sends a message for one player to that player's client only and a message for everyone
    /// to every client; there it is raised again with <see cref="DeliverLocal"/>. Offline <see cref="Remote"/> is null and
    /// messages are only raised here, as before.
    /// </remarks>
    public static class HubWorldMessages
    {
        /// <summary>A message (recipient - null = everyone, text, kind).</summary>
        public static event Action<CombatEntity, string, HubMessageKind> Sent;

        /// <summary>Also log messages to the Console.</summary>
        public static bool LogToConsole;

        /// <summary>
        /// Network delivery (recipient - null = everyone, text, kind), installed by the networking layer while a server
        /// runs: it forwards each message to the clients concerned. Null offline and on clients.
        /// </summary>
        public static Action<CombatEntity, string, HubMessageKind> Remote;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Sent = null;
            Remote = null;
            LogToConsole = false;
        }

        /// <summary>Sends <paramref name="text"/> to <paramref name="to"/> (null = every player).</summary>
        public static void Send(CombatEntity to, string text, HubMessageKind kind = HubMessageKind.Info)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
            if (LogToConsole)
                Debug.Log($"[Hub World] {(to != null ? to.name + ": " : "")}{text}");
            if (Remote != null)
            {
                try { Remote(to, text, kind); }
                catch (Exception e) { Debug.LogException(e); }
            }
            Sent?.Invoke(to, text, kind);
        }

        /// <summary>
        /// Raises a message on this machine only, without forwarding it anywhere: used by the networking layer for the
        /// messages a client receives from the server (<paramref name="to"/> = this machine's player, or null).
        /// </summary>
        public static void DeliverLocal(CombatEntity to, string text, HubMessageKind kind)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
            Sent?.Invoke(to, text, kind);
        }

        /// <summary>Sends to every player.</summary>
        public static void Broadcast(string text, HubMessageKind kind = HubMessageKind.Info) => Send(null, text, kind);

        /// <summary>Is a message for <paramref name="to"/> meant for this machine's player?</summary>
        public static bool IsForLocal(CombatEntity to)
        {
            if (to == null)
                return true;
            CombatEntity local = HubWorldDirector.LocalPlayer;
            return local == null || local == to;
        }

        /// <summary>"1:30:00", "4:05", "0:09".</summary>
        public static string FormatTime(double seconds)
        {
            if (seconds < 0d)
                seconds = 0d;
            var t = TimeSpan.FromSeconds(Math.Ceiling(seconds));
            return t.TotalHours >= 1d ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
        }

        /// <summary>"10 minutes", "1 minute", "30 seconds".</summary>
        public static string FormatSpan(double seconds)
        {
            if (seconds >= 60d)
            {
                int m = (int)Math.Round(seconds / 60d);
                return m == 1 ? "1 minute" : $"{m} minutes";
            }
            int s = (int)Math.Round(seconds);
            return s == 1 ? "1 second" : $"{s} seconds";
        }
    }
}
