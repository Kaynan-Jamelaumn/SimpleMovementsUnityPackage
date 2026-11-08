using System;
using System.IO;
using UnityEngine;

/// <summary>
/// The farming clock: game hours since the first day, for every field. It runs at the Day Night Cycle's pace (its Day
/// Length Minutes, faster at night with its Night Speed) and follows its hour - when the game skips time (sleeping), the
/// clock jumps forward with it and the plants catch up. Without a Day Night Cycle it runs at the Agriculture settings'
/// Day Length Minutes. It never runs backwards, and it is saved where the rules run, with the world it belongs to (see
/// <see cref="SaveFolder"/>): plants keep their age across sessions, and every world keeps its own time. On a network
/// client it follows the server's clock.
/// </summary>
public static class FarmClock
{
    private static double now = -1.0;
    private static bool loaded;
    private static double serverNow, serverAt;
    private static bool followsServer;
    /// <summary>The clock of a network session on this client: never saved, given back to this machine's own when it ends.</summary>
    private static bool clientClock;
    private static float saveTimer;
    // The world the farming saves belong to (see SaveFolder).
    private static bool hooked, folderKnown;
    private static string folder;
    private static int world;
    /// <summary>The world the clock was read from (-1: none): it is written back there only.</summary>
    private static int clockWorld = -1;

    /// <summary>Game hours since the first day.</summary>
    public static double Now
    {
        get
        {
            if (now < 0.0) Start();
            return now;
        }
    }

    public static AgriSettings Settings => AgricultureDatabase.Instance.Settings;
    public static AgriDate Date => new AgriDate(Now, Settings);

    /// <summary>Raised when the clock jumped forward (sleep, a skip): hours skipped.</summary>
    public static event Action<double> Skipped;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        now = -1.0;
        loaded = false;
        followsServer = false;
        clientClock = false;
        Skipped = null;
        hooked = false;
        folderKnown = false;
        folder = null;
        world = 0;
        clockWorld = -1;
    }

    private static void Start()
    {
        AgriSettings s = Settings;
        now = s.startHour;
        clientClock = FollowsServerNow;
        if (!loaded)
        {
            loaded = true;
            if (Application.isPlaying && !clientClock) Load();
        }
        if (WorldTime.HasClock) Follow(WorldTime.Hour);
    }

    private static bool FollowsServerNow => GameNet.NetAuthority.IsNetworked && !GameNet.NetAuthority.IsServer;

    /// <summary>Game hours per real second right now.</summary>
    public static float HoursPerSecond
    {
        get
        {
            DayNightCycle c = DayNightCycle.Instance;
            if (c != null && !c.paused)
                return 24f / (Mathf.Max(0.5f, c.dayLengthMinutes) * 60f) * (WorldTime.IsDay ? 1f : Mathf.Max(0.1f, c.nightSpeed));
            if (c != null && c.paused) return 0f;
            return 24f / (Mathf.Max(0.5f, Settings.dayLengthMinutes) * 60f);
        }
    }

    /// <summary>Advances the clock by a frame (the Farm Director calls it).</summary>
    public static void Tick(float deltaTime)
    {
        if (now < 0.0) Start();
        if (FollowsServerNow && !clientClock && !followsServer)
        {
            // Becoming a network client: this machine's own clock is put aside as it is (saved) and given back afterwards.
            Write();
            clientClock = true;
        }
        if ((followsServer || clientClock) && !FollowsServerNow)
        {
            // The session ended: this machine's own clock again (its save), not the session's carried on.
            followsServer = false;
            clientClock = false;
            now = -1.0;
            loaded = false;
            Start();
        }
        if (followsServer)
        {
            now = Math.Max(now, serverNow + (Time.unscaledTimeAsDouble - serverAt) * HoursPerSecond);
            return;
        }
        now += Math.Max(0f, deltaTime) * HoursPerSecond;
        if (WorldTime.HasClock) Follow(WorldTime.Hour);
        saveTimer += Time.unscaledDeltaTime;
        if (saveTimer > 30f)
        {
            saveTimer = 0f;
            Save();
        }
    }

    /// <summary>Keeps the time of day on the Day Night Cycle's hour, only ever moving forward.</summary>
    private static void Follow(float hour)
    {
        double day = Math.Floor(now / 24.0);
        double current = now - day * 24.0;
        double diff = hour - current;
        if (Math.Abs(diff) < 0.05 || Math.Abs(diff) > 23.95) return;
        double target = diff > 0.0 ? day * 24.0 + hour : (day + 1.0) * 24.0 + hour;
        double skipped = target - now;
        now = target;
        if (skipped > 0.25) Skipped?.Invoke(skipped);
    }

    /// <summary>Moves the clock forward (a debug skip, a custom sleep).</summary>
    public static void Skip(double hours)
    {
        if (hours <= 0.0) return;
        if (now < 0.0) Start();
        now += hours;
        Skipped?.Invoke(hours);
    }

    /// <summary>A network client takes the server's clock.</summary>
    public static void FromServer(double serverHours)
    {
        // The first picture of a session sets the clock outright (the server's may be behind this machine's own);
        // after that it only moves forward.
        if (!followsServer || now < serverHours) now = serverHours;
        followsServer = true;
        clientClock = true;
        serverNow = serverHours;
        serverAt = Time.unscaledTimeAsDouble;
    }

    // ---------------------------------------------------------------- the world and saving
    /// <summary>
    /// Where the open world keeps its farming: persistentDataPath/Worlds/&lt;world&gt;/Agriculture - its clock, and its
    /// fields under Fields. It is the world <see cref="WorldStore"/> has open here, so the offline world, the online one and
    /// every <c>-world</c> keep their own gardens and their own time. Null where nothing is kept: a network client, or a
    /// world that is open in another window of the game (it says so). Saves from before worlds were kept apart (under
    /// persistentDataPath/Agriculture) are copied into each world the first time it keeps farming; those old files are no
    /// longer written.
    /// </summary>
    public static string SaveFolder
    {
        get
        {
            if (!Application.isPlaying) return null;
            Hook();
            if (folderKnown) return folder;
            folderKnown = true;
            folder = WorldStore.IsOpen ? Path.Combine(WorldStore.Folder, "Agriculture") : null;
            if (folder != null) TakeOverOldSaves(folder);
            else if (GameNet.NetAuthority.IsServer && !string.IsNullOrEmpty(WorldStore.Problem))
                Debug.LogWarning("Agriculture: gardens and the farm clock are not kept here - " + WorldStore.Problem);
            return folder;
        }
    }

    /// <summary>Counts the worlds opened in this run: a garden remembers the one it was read from, and is saved only into it.</summary>
    public static int World
    {
        get
        {
            Hook();
            return world;
        }
    }

    public static string SavePath => SaveFolder != null ? Path.Combine(SaveFolder, "clock.json") : null;

    private static void Hook()
    {
        if (hooked) return;
        hooked = true;
        WorldStore.Committing += OnWorldCommitting;
        WorldStore.WorldChanged += OnWorldChanged;
    }

    /// <summary>The world is written one last time before it closes: its clock and its gardens are saved into it first (it is still this process's).</summary>
    private static void OnWorldCommitting()
    {
        if (!WorldStore.Closing) return;
        // This machine's own clock (a client's is the server's): the session may already have changed, so not Save().
        if (!followsServer && !clientClock) Write();
        FarmDirector.SaveForClosingWorld();
    }

    /// <summary>
    /// Another world opened (a session started or ended): the clock and the gardens of the old one were saved as it closed
    /// and are put away; the new world's are read when they are next needed (on a network client, the server's).
    /// </summary>
    private static void OnWorldChanged()
    {
        world++;
        folderKnown = false;
        folder = null;
        now = -1.0;
        loaded = false;
        followsServer = false;
        clientClock = false;
        clockWorld = -1;
        saveTimer = 0f;
        FarmDirector.ChangeWorld();
    }

    /// <summary>Saves from before worlds were kept apart are copied into a world that keeps no farming yet (once per world).</summary>
    private static void TakeOverOldSaves(string into)
    {
        try
        {
            if (Directory.Exists(into)) return; // this world keeps its own already
            string old = Path.Combine(Application.persistentDataPath, "Agriculture");
            string oldClock = Path.Combine(old, "clock.json"), oldFields = Path.Combine(old, "Fields");
            Directory.CreateDirectory(into);
            bool any = false;
            if (File.Exists(oldClock))
            {
                File.Copy(oldClock, Path.Combine(into, "clock.json"), false);
                any = true;
            }
            if (Directory.Exists(oldFields))
            {
                string fields = Path.Combine(into, "Fields");
                Directory.CreateDirectory(fields);
                foreach (string f in Directory.GetFiles(oldFields, "*.bin"))
                {
                    File.Copy(f, Path.Combine(fields, Path.GetFileName(f)), false);
                    any = true;
                }
            }
            if (any) Debug.Log($"Agriculture: the world '{WorldStore.WorldId}' takes over the gardens and the farm clock saved before worlds were kept apart (copied from {old}; those files are no longer written).");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Agriculture: the gardens saved before worlds were kept apart could not all be taken over ({e.Message}).");
        }
    }

    /// <summary>
    /// Writes a file whole: aside first (flushed to the disk), then swapped in, the previous copy kept as "&lt;file&gt;.bak" -
    /// a failed or torn write never costs the copy there was. Throws when it could not be written.
    /// </summary>
    internal static void WriteWhole(string path, byte[] data)
    {
        string temp = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(data, 0, data.Length);
            fs.Flush(true);
        }
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }

    /// <summary>The copies of a file written by <see cref="WriteWhole"/>, the newest first: itself, a write that was not swapped in, the backup.</summary>
    internal static string[] CopiesOf(string path) => new[] { path, path + ".tmp", path + ".bak" };

    [Serializable]
    private class ClockSave
    {
        public double now;
    }

    public static void Save()
    {
        if (FollowsServerNow || followsServer || clientClock) return;
        Write();
    }

    /// <summary>Writes the clock as it is (this machine's own: the callers make sure) - into the world it was read from only.</summary>
    private static void Write()
    {
        if (now < 0.0 || !Application.isPlaying || clockWorld != world || !folderKnown || folder == null) return;
        try
        {
            WriteWhole(Path.Combine(folder, "clock.json"), System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new ClockSave { now = now })));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Agriculture: could not save the farm clock ({e.Message}).");
        }
    }

    /// <summary>Reads the world's clock: the latest time any whole copy of it knew (the clock only moves forward).</summary>
    private static void Load()
    {
        string path = SavePath;
        if (path == null) return; // nothing is kept here: the clock starts afresh and is not written
        clockWorld = world;
        foreach (string copy in CopiesOf(path))
        {
            try
            {
                if (!File.Exists(copy)) continue;
                ClockSave c = JsonUtility.FromJson<ClockSave>(File.ReadAllText(copy));
                if (c != null && c.now > now) now = c.now;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Agriculture: could not read the farm clock '{Path.GetFileName(copy)}' ({e.Message}).");
            }
        }
    }
}
