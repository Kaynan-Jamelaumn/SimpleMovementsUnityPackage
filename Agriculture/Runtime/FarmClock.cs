using System;
using System.IO;
using UnityEngine;

/// <summary>
/// The farming clock: game hours since the first day, for every field. It runs at the Day Night Cycle's pace (its Day
/// Length Minutes, faster at night with its Night Speed) and follows its hour - when the game skips time (sleeping), the
/// clock jumps forward with it and the plants catch up. Without a Day Night Cycle it runs at the Agriculture settings'
/// Day Length Minutes. It never runs backwards, and it is saved (where the rules run), so plants keep their age across
/// sessions. On a network client it follows the server's clock.
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

    // ---------------------------------------------------------------- saving
    [Serializable]
    private class ClockSave
    {
        public double now;
    }

    public static string SavePath => Path.Combine(Application.persistentDataPath, "Agriculture", "clock.json");

    public static void Save()
    {
        if (FollowsServerNow || followsServer || clientClock) return;
        Write();
    }

    /// <summary>Writes the clock as it is (this machine's own: the callers make sure).</summary>
    private static void Write()
    {
        if (now < 0.0 || !Application.isPlaying) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            File.WriteAllText(SavePath, JsonUtility.ToJson(new ClockSave { now = now }));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Agriculture: could not save the farm clock ({e.Message}).");
        }
    }

    private static void Load()
    {
        try
        {
            if (!File.Exists(SavePath)) return;
            ClockSave c = JsonUtility.FromJson<ClockSave>(File.ReadAllText(SavePath));
            if (c != null && c.now > now) now = c.now;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Agriculture: could not read the farm clock ({e.Message}).");
        }
    }
}
