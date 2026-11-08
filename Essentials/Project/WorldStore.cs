using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

/// <summary>
/// The lasting state of one world, where the rules run (offline, the server): every system that keeps value between
/// sessions - players' kept money, the housing books with their chests and rent accounts - writes its part into ONE
/// file, committed whole (written aside, then swapped in, the previous copy kept as a backup). A crash loses at most the
/// last few seconds of everything together, never half of a payment (the money left a purse but never reached the books).
/// <list type="bullet">
/// <item>Worlds are apart: single player is the "offline" world, a hosted or dedicated server the "online" one (or the
/// one named on the command line: <c>-world &lt;id&gt;</c>). Playing alone never touches the friends' city. The world
/// that is open stays the one written to until the session changes: only a change of session closes it (written one
/// last time) and opens the other.</item>
/// <item>One process at a time: the world's lock file is held with a lock of the operating system (released when the
/// process ends, even by a crash). A second game window or server on the same machine finds the world locked and keeps
/// nothing (it says so) rather than overwriting the first one's files.</item>
/// <item>A file that cannot be read is never written over: the newest complete copy is used instead (a write that was
/// being swapped in, else the backup) and the unreadable file is set aside; if none can be read, nothing is kept until it
/// is fixed.</item>
/// <item>The disk is written by a worker thread (the game never waits for it), except when a world closes or the game
/// quits: then the world is written before anything else happens.</item>
/// </list>
/// A network client keeps nothing here (the server's world is the truth).
/// </summary>
public static class WorldStore
{
    [Serializable]
    private class Section
    {
        public string name = "";
        public string json = "";
    }

    [Serializable]
    private class StoreFile
    {
        public int version = 1;
        public string world = "";
        public long savedUtc;
        public List<Section> sections = new List<Section>();
    }

    private static StoreFile file;
    private static string openWorld;
    private static FileStream lockStream;
    private static bool refused, dirty, subscribed, committing, closing, shutDown, partFailed;
    private static float commitAt = -1f;
    private static float commitDelay = CommitDelay;
    private static Runner runner;

    /// <summary>Real seconds a change waits before the world is written, unless set otherwise (<see cref="CommitDelaySeconds"/>).</summary>
    public const float CommitDelay = 2f;

    /// <summary>Before each write: every system puts its latest part in (<see cref="Put"/>).</summary>
    public static event Action Committing;
    /// <summary>The world changed (a session started or ended): systems drop what they loaded and read the new world.</summary>
    public static event Action WorldChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Close(false);
        file = null;
        openWorld = null;
        refused = dirty = subscribed = committing = closing = shutDown = partFailed = false;
        commitAt = -1f;
        commitDelay = CommitDelay;
        runner = null;
        Committing = null;
        WorldChanged = null;
        Interlocked.Exchange(ref writeFailures, 0);
        lastWriteError = null;
    }

    /// <summary>The world of this session: "-world &lt;id&gt;" on the command line, else "online" in a network session, else "offline".</summary>
    public static string WorldId
    {
        get
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-world" && IsSafe(args[i + 1])) return args[i + 1];
            return GameNet.NetAuthority.IsNetworked ? "online" : "offline";
        }
    }

    public static string Folder => Path.Combine(Application.persistentDataPath, "Worlds", WorldId);
    private static string PathOf(string world) => Path.Combine(Application.persistentDataPath, "Worlds", world, "world.json");

    /// <summary>
    /// Real seconds a change waits before the world is written (changes in between go together; 0.5 to 60). Housing sets
    /// it from its Save Delay Seconds; the world is also written when the session changes and on quit.
    /// </summary>
    public static float CommitDelaySeconds
    {
        get => commitDelay;
        set => commitDelay = float.IsNaN(value) ? CommitDelay : Mathf.Clamp(value, 0.5f, 60f);
    }

    /// <summary>
    /// True while the open world is written for the last time before it closes (the session changed, the game quits):
    /// systems put their whole part in (a part that is usually put piece by piece is put entire).
    /// </summary>
    public static bool Closing => closing;

    /// <summary>Is the world open here (loaded, locked for this process)? False on a network client and when refused.</summary>
    public static bool IsOpen
    {
        get
        {
            Ensure();
            return file != null;
        }
    }

    /// <summary>Why the world is not kept here (empty when it is open, or on a client).</summary>
    public static string Problem { get; private set; } = "";

    /// <summary>A system's part of the world (null when it has none yet, or the world is not open).</summary>
    public static string Get(string section)
    {
        Ensure();
        if (file == null) return null;
        foreach (Section s in file.sections)
            if (s != null && s.name == section) return s.json;
        return null;
    }

    /// <summary>Puts a system's part in (written at the next commit). Always into the world that is open, even while it closes.</summary>
    public static void Put(string section, string json)
    {
        Ensure();
        if (file == null || string.IsNullOrEmpty(section)) return;
        Section s = file.sections.Find(x => x != null && x.name == section);
        if (s == null)
        {
            s = new Section { name = section };
            file.sections.Add(s);
        }
        if (s.json == json) return;
        s.json = json ?? "";
        MarkDirty();
    }

    /// <summary>Something changed: the world is written in a moment.</summary>
    public static void MarkDirty()
    {
        if (file == null) return;
        dirty = true;
        if (commitAt < 0f) commitAt = Time.unscaledTime + commitDelay;
        EnsureRunner();
    }

    /// <summary>Writes the world now (every system's latest part; the disk is written by a worker a moment later).</summary>
    public static void Commit() => Commit(false);

    /// <summary>
    /// Every system puts its part in, then the whole world is written - unless a part could not be put: then the world on
    /// disk stays as it was (whole) and the write is tried again at the next change. <paramref name="now"/>: written before
    /// this returns (a world closing, the game quitting or going to the background).
    /// </summary>
    private static void Commit(bool now)
    {
        if (file == null || openWorld == null || committing) return; // (a part that commits while putting itself in changes nothing)
        committing = true;
        try
        {
            bool whole = true;
            Action parts = Committing;
            if (parts != null)
                foreach (Delegate part in parts.GetInvocationList())
                {
                    try
                    {
                        ((Action)part)();
                    }
                    catch (Exception e)
                    {
                        whole = false;
                        Debug.LogException(e);
                    }
                }
            if (file == null) return;
            if (!whole)
            {
                if (!partFailed)
                    Debug.LogError($"[World] A part of the world '{openWorld}' could not be put in: the world on disk is kept as it was (whole), and the write is tried again at the next change.");
                partFailed = true;
                dirty = true;
                commitAt = Time.unscaledTime + commitDelay;
                return;
            }
            partFailed = false;
            dirty = false;
            commitAt = -1f;
            file.world = openWorld;
            file.savedUtc = DateTime.UtcNow.Ticks;
            string json = JsonUtility.ToJson(file);
            string path = PathOf(openWorld);
            if (now || closing || !Threaded) WriteNow(path, json);
            else Queue(path, json);
        }
        finally
        {
            committing = false;
        }
    }

    // ---------------------------------------------------------------- writing the disk
    // One writer at a time: the newest snapshot waiting replaces an older one (only the latest state matters). A world that
    // closes (or the game quitting) waits for the writer, then writes its last state itself, so nothing of a world is
    // written after its lock was let go and nothing older lands after something newer.
    private static readonly object writeGate = new object();
    private static string pendingPath, pendingJson;
    private static bool writerBusy;
    private static int writeFailures;
    private static volatile string lastWriteError;

#if UNITY_WEBGL && !UNITY_EDITOR
    private static readonly bool Threaded = false; // no worker threads in a browser
#else
    private static readonly bool Threaded = true;
#endif

    private static void Queue(string path, string json)
    {
        lock (writeGate)
        {
            pendingPath = path;
            pendingJson = json;
            if (writerBusy) return;
            writerBusy = true;
        }
        if (!ThreadPool.QueueUserWorkItem(WriterLoop))
        {
            // No worker today: written here instead.
            lock (writeGate)
            {
                writerBusy = false;
                pendingPath = pendingJson = null;
                Monitor.PulseAll(writeGate);
            }
            WriteNow(path, json);
        }
    }

    private static void WriterLoop(object _)
    {
        while (true)
        {
            string path, json;
            lock (writeGate)
            {
                if (pendingJson == null)
                {
                    writerBusy = false;
                    Monitor.PulseAll(writeGate);
                    return;
                }
                path = pendingPath;
                json = pendingJson;
                pendingPath = pendingJson = null;
            }
            string error = WriteFile(path, json);
            if (error != null)
            {
                lastWriteError = error;
                Interlocked.Increment(ref writeFailures);
            }
        }
    }

    /// <summary>Waits for the writer to finish what it is writing; <paramref name="dropPending"/>: a newer state is about to be written here.</summary>
    private static void DrainWriter(bool dropPending)
    {
        lock (writeGate)
        {
            if (dropPending) pendingPath = pendingJson = null;
            while (writerBusy || pendingJson != null)
            {
                if (!writerBusy)
                {
                    // Waiting with nobody to write it (should not happen): written by the caller below or dropped.
                    pendingPath = pendingJson = null;
                    break;
                }
                Monitor.Wait(writeGate, 50);
            }
        }
    }

    private static void WriteNow(string path, string json)
    {
        DrainWriter(true);
        string error = WriteFile(path, json);
        if (error == null) return;
        Debug.LogWarning($"[World] Could not write the world '{openWorld}' ({error}). It is tried again at the next change.");
        if (file != null)
        {
            dirty = true;
            if (commitAt < 0f) commitAt = Time.unscaledTime + commitDelay;
        }
    }

    /// <summary>Writes a whole world file: aside (flushed to the disk), then swapped in, the previous copy kept as the backup. Any thread.</summary>
    private static string WriteFile(string path, string json)
    {
        try
        {
            string temp = path + ".tmp", backup = path + ".bak";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var w = new StreamWriter(fs))
            {
                w.Write(json);
                w.Flush();
                fs.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, backup);
            else File.Move(temp, path);
            return null;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    // ---------------------------------------------------------------- opening and closing
    private static void Ensure()
    {
        if (!Application.isPlaying || shutDown) return;
        if (!subscribed)
        {
            subscribed = true;
            GameNet.NetAuthority.ModeChanged += OnModeChanged;
        }
        // The open world is never switched here: only a change of session closes it (OnModeChanged). What is put while it
        // is written and closed goes into it - never into the next world.
        if (file != null) return;
        if (!GameNet.NetAuthority.IsServer) return; // a network client keeps nothing
        string world = WorldId;
        if (refused && openWorld == world) return;
        Open(world);
    }

    private static void Open(string world)
    {
        openWorld = world;
        refused = false;
        Problem = "";
        string path = PathOf(world);
        string folder = Path.GetDirectoryName(path);
        try
        {
            Directory.CreateDirectory(folder);
            lockStream = new FileStream(Path.Combine(folder, "lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (Exception)
        {
            Refuse($"The world '{world}' is open in another window of the game: nothing is kept here.");
            return;
        }
        if (!LockAcrossProcesses())
        {
            Refuse($"The world '{world}' is open in another window of the game (or another server): nothing is kept here.");
            return;
        }
        // The newest complete copy: the world, unless a newer write was left aside before it could be swapped in (a crash
        // between the two steps of the swap); without a readable world, that write, else the backup.
        string temp = path + ".tmp", backup = path + ".bak";
        bool mainExists = File.Exists(path);
        StoreFile main = Read(path), aside = Read(temp);
        StoreFile loaded = null;
        string from = null;
        if (main != null)
        {
            loaded = main;
            if (aside != null && aside.savedUtc > main.savedUtc)
            {
                loaded = aside;
                from = "its last write, which had not been swapped in";
            }
        }
        else if (aside != null)
        {
            loaded = aside;
            from = "its last write, which had not been swapped in";
        }
        else
        {
            loaded = Read(backup);
            if (loaded != null) from = "its backup";
        }
        // (A write left aside that cannot be read, with no world and no backup, is a first write that never finished.)
        if (loaded == null && (mainExists || File.Exists(backup)))
        {
            Refuse($"The world '{world}' cannot be read ({path}): nothing is kept until it is fixed (it is not written over).");
            return;
        }
        if (main == null && mainExists)
        {
            // The unreadable file is kept for whoever wants to look at it, and the good backup is not pushed out by it.
            string aside2 = path + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            try
            {
                File.Move(path, aside2);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[World] The unreadable '{path}' could not be set aside ({e.Message}).");
                aside2 = null;
            }
            Debug.LogWarning($"[World] The world '{world}' could not be read: {from} was used." + (aside2 != null ? $" The unreadable file is kept as '{Path.GetFileName(aside2)}'." : ""));
        }
        else if (from != null) Debug.LogWarning($"[World] The world '{world}' was taken from {from}.");
        file = loaded ?? new StoreFile { world = world };
        file.sections ??= new List<Section>();
        file.sections.RemoveAll(s => s == null || string.IsNullOrEmpty(s.name));
        EnsureRunner();
        if (from != null) MarkDirty(); // written back whole in a moment
    }

    /// <summary>
    /// Locks the lock file against other processes: an operating-system lock (fcntl on macOS and Linux, LockFileEx on
    /// Windows), released when the process ends. The file's sharing alone only holds inside this process on macOS and Linux.
    /// </summary>
    private static bool LockAcrossProcesses()
    {
        try
        {
            lockStream.Lock(0, 1);
            return true;
        }
        catch (IOException)
        {
            return false; // another process holds it
        }
        catch (Exception)
        {
            return true; // no such locks on this platform: the file's sharing is all there is
        }
    }

    private static StoreFile Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            StoreFile f = JsonUtility.FromJson<StoreFile>(File.ReadAllText(path));
            return f != null && f.version >= 1 ? f : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void Refuse(string why)
    {
        refused = true;
        file = null;
        Problem = why;
        Debug.LogError("[World] " + why);
        ReleaseLock();
    }

    /// <summary>Closes the open world (<paramref name="commit"/>: written one last time first, whole, before this returns).</summary>
    private static void Close(bool commit)
    {
        if (commit && file != null)
        {
            closing = true;
            try
            {
                Commit(true);
            }
            finally
            {
                closing = false;
            }
        }
        DrainWriter(false);
        file = null;
        dirty = false;
        commitAt = -1f;
        ReleaseLock();
    }

    private static void ReleaseLock()
    {
        try { lockStream?.Dispose(); } catch (Exception) { }
        lockStream = null;
    }

    private static void OnModeChanged(GameNet.NetMode mode)
    {
        // A session started or ended: what was open is written and closed; the other world opens when it is next needed.
        if (file != null) Close(true);
        refused = false;
        openWorld = null;
        try
        {
            WorldChanged?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>The game quits: the world is written and closed, and nothing opens it again while the scenes are torn down.</summary>
    private static void Shut()
    {
        if (shutDown) return;
        if (file != null) Close(true);
        else DrainWriter(false);
        shutDown = true;
    }

    private static bool IsSafe(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64) return false;
        foreach (char c in id)
            if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) return false;
        return true;
    }

    private static void EnsureRunner()
    {
        if (runner != null || !Application.isPlaying) return;
        var go = new GameObject("[World Store]") { hideFlags = HideFlags.HideInHierarchy };
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
    }

    /// <summary>Writes the world a moment after a change, when the game goes to the background, and on quit.</summary>
    private sealed class Runner : MonoBehaviour
    {
        private void Update()
        {
            if (Interlocked.Exchange(ref writeFailures, 0) > 0)
            {
                Debug.LogWarning($"[World] Could not write the world '{openWorld}' ({lastWriteError}). It is tried again at the next change.");
                if (file != null)
                {
                    dirty = true;
                    if (commitAt < 0f) commitAt = Time.unscaledTime + commitDelay;
                }
            }
            if (dirty && commitAt >= 0f && Time.unscaledTime >= commitAt) Commit(false);
        }

        // A phone may end a game in the background without telling it: what changed is written before that.
        private void OnApplicationPause(bool paused)
        {
            if (paused && dirty && file != null) Commit(true);
        }

        private void OnApplicationQuit() => Shut();
    }
}
