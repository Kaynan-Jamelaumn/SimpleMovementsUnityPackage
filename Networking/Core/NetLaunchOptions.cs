using System;
using System.Collections.Generic;
using System.Globalization;

namespace GameNet
{
    /// <summary>
    /// How a process was asked to start, read from the command line. Everything defaults to this machine
    /// (<c>127.0.0.1</c>), so a local dedicated server and local clients find each other with no arguments at all.
    /// </summary>
    /// <remarks>
    /// Arguments (case-insensitive, <c>-name value</c> or <c>-name=value</c>):
    /// <list type="bullet">
    /// <item><c>-server</c> (or <c>-dedicated</c>): dedicated server. Batch mode / server builds imply it.</item>
    /// <item><c>-host</c>: server plus a local player.</item>
    /// <item><c>-client</c>: connect to <c>-address</c>.</item>
    /// <item><c>-offline</c>: single player (the Stage 1 game).</item>
    /// <item><c>-address 127.0.0.1</c>, <c>-port 7770</c>, <c>-maxplayers 16</c>, <c>-name Player2</c>,
    /// <c>-tickrate 30</c>, <c>-shard 1234</c> (shard seed of the world cycles).</item>
    /// </list>
    /// </remarks>
    [Serializable]
    public sealed class NetLaunchOptions
    {
        public const string DefaultAddress = "127.0.0.1";
        public const ushort DefaultPort = 7770;

        /// <summary>The mode asked for, or null when the command line says nothing (show the menu).</summary>
        public NetMode? mode;
        public string address = DefaultAddress;
        public ushort port = DefaultPort;
        public int maxPlayers = 16;
        public string playerName;
        public ushort tickRate;
        public int? shardSeed;

        /// <summary>Unknown arguments, kept for diagnostics.</summary>
        public readonly List<string> ignored = new List<string>();

        /// <summary>Parses <see cref="Environment.GetCommandLineArgs"/>, with batch mode meaning "dedicated server".</summary>
        public static NetLaunchOptions FromEnvironment(bool batchMode, bool serverBuild)
        {
            string[] args;
            try
            {
                args = Environment.GetCommandLineArgs();
            }
            catch (NotSupportedException)
            {
                args = Array.Empty<string>();
            }
            NetLaunchOptions o = Parse(args, 1);
            if (o.mode == null && (batchMode || serverBuild))
                o.mode = NetMode.Server;
            return o;
        }

        /// <summary>Parses arguments starting at <paramref name="first"/> (skip the executable path with 1).</summary>
        public static NetLaunchOptions Parse(IList<string> args, int first = 0)
        {
            var o = new NetLaunchOptions();
            if (args == null)
                return o;
            for (int i = Math.Max(0, first); i < args.Count; i++)
            {
                string raw = args[i];
                if (string.IsNullOrEmpty(raw) || (raw[0] != '-' && raw[0] != '+'))
                    continue;
                string name = raw.TrimStart('-', '+');
                string value = null;
                int eq = name.IndexOf('=');
                if (eq >= 0)
                {
                    value = name.Substring(eq + 1);
                    name = name.Substring(0, eq);
                }
                name = name.ToLowerInvariant();

                string Value()
                {
                    if (value != null)
                        return value;
                    if (i + 1 < args.Count && !string.IsNullOrEmpty(args[i + 1]) && args[i + 1][0] != '-')
                        return args[++i];
                    return null;
                }

                switch (name)
                {
                    case "server":
                    case "dedicated":
                    case "dedicatedserver":
                        o.mode = NetMode.Server;
                        break;
                    case "host":
                        o.mode = NetMode.Host;
                        break;
                    case "client":
                    case "join":
                        o.mode = NetMode.Client;
                        if (value != null)
                            o.address = value;
                        break;
                    case "offline":
                    case "singleplayer":
                        o.mode = NetMode.Offline;
                        break;
                    case "address":
                    case "ip":
                    case "connect":
                    {
                        string v = Value();
                        if (!string.IsNullOrWhiteSpace(v))
                            o.address = v.Trim();
                        break;
                    }
                    case "port":
                    {
                        if (ushort.TryParse(Value(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort p) && p > 0)
                            o.port = p;
                        break;
                    }
                    case "maxplayers":
                    case "maxclients":
                    {
                        if (int.TryParse(Value(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int m) && m > 0)
                            o.maxPlayers = m;
                        break;
                    }
                    case "name":
                    case "playername":
                    {
                        string v = Value();
                        if (!string.IsNullOrWhiteSpace(v))
                            o.playerName = v.Trim();
                        break;
                    }
                    case "tickrate":
                    {
                        if (ushort.TryParse(Value(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort t) && t > 0)
                            o.tickRate = t;
                        break;
                    }
                    case "shard":
                    case "shardseed":
                    {
                        if (int.TryParse(Value(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s))
                            o.shardSeed = s;
                        break;
                    }
                    case "batchmode":
                    case "nographics":
                    case "logfile":
                        if (name == "logfile")
                            Value(); // Unity's own: skip its path
                        break;
                    default:
                        o.ignored.Add(raw);
                        break;
                }
            }
            return o;
        }

        /// <summary>Builds the command line for launching a local process in <paramref name="m"/>.</summary>
        public static string BuildArguments(NetMode m, string address, ushort port, string playerName = null, bool headless = false)
        {
            var parts = new List<string>();
            if (headless)
            {
                parts.Add("-batchmode");
                parts.Add("-nographics");
            }
            switch (m)
            {
                case NetMode.Server: parts.Add("-server"); break;
                case NetMode.Host: parts.Add("-host"); break;
                case NetMode.Client: parts.Add("-client"); break;
                default: parts.Add("-offline"); break;
            }
            if (m == NetMode.Client && !string.IsNullOrWhiteSpace(address))
            {
                parts.Add("-address");
                parts.Add(address.Trim());
            }
            if (m != NetMode.Offline)
            {
                parts.Add("-port");
                parts.Add(port.ToString(CultureInfo.InvariantCulture));
            }
            if (!string.IsNullOrWhiteSpace(playerName))
            {
                parts.Add("-name");
                parts.Add(Quote(playerName.Trim()));
            }
            return string.Join(" ", parts);
        }

        private static string Quote(string s) => s.IndexOf(' ') >= 0 ? "\"" + s.Replace("\"", "") + "\"" : s;
    }
}
