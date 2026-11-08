using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// The time of the housing books, in lease days: a counter kept in the world that runs only while the rules run (the
/// game offline, the server) - nothing ends while the server is down - at a steady pace fixed by the world's clock: real
/// days (Real Hours Per Day, the default of an online world) or game days (Game Day Minutes, the default of the offline
/// world; the sky's night speed, pauses and skips do not change it). After a restart it holds still for a few minutes
/// (at most as long as the game was down) so players can come back before anything ends. Where the rules run only.
/// </summary>
public static class HousingClock
{
    /// <summary>Most real seconds one frame may add (a longer gap is not housing time).</summary>
    public const float MaxSecondsPerFrame = 5f;

    private static double now = -1.0, hold;
    private static HousingClockKind kind = HousingClockKind.RealTime;
    private static readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
    private static long lastMs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        now = -1.0;
        hold = 0.0;
        kind = HousingClockKind.RealTime;
        watch.Reset();
        lastMs = 0;
    }

    /// <summary>Lease days now.</summary>
    public static double Now => now >= 0.0 ? now : 0.0;

    public static bool Started => now >= 0.0;

    /// <summary>The world's clock (fixed when the world was made).</summary>
    public static HousingClockKind Kind => kind;

    /// <summary>Starts from the time the world was saved at, on its clock; holds still for <paramref name="holdSeconds"/>.</summary>
    public static void Start(double saved, HousingClockKind worldKind, double holdSeconds)
    {
        now = Math.Max(0.0, saved);
        kind = worldKind == HousingClockKind.GameTime ? HousingClockKind.GameTime : HousingClockKind.RealTime;
        hold = Math.Max(0.0, holdSeconds);
        watch.Restart();
        lastMs = 0;
    }

    public static void Stop() => now = -1.0;

    /// <summary>A frame went by (the server measures real time itself; offline, a paused game stops the clock).</summary>
    public static void Tick(HousingRules r)
    {
        if (now < 0.0) return;
        long ms = watch.ElapsedMilliseconds;
        double seconds = GameNet.NetAuthority.IsNetworked ? (ms - lastMs) / 1000.0 : Time.deltaTime;
        lastMs = ms;
        seconds = Math.Max(0.0, Math.Min(seconds, MaxSecondsPerFrame));
        if (hold > 0.0)
        {
            double held = Math.Min(hold, seconds);
            hold -= held;
            seconds -= held;
            if (seconds <= 0.0) return;
        }
        now += seconds / SecondsPerDay(r);
    }

    /// <summary>Real seconds in a lease day.</summary>
    public static double SecondsPerDay(HousingRules r)
    {
        r ??= HousingRules.Defaults();
        return kind == HousingClockKind.GameTime ? Math.Max(0.5f, r.gameDayMinutes) * 60.0 : Math.Max(0.05f, r.realHoursPerDay) * 3600.0;
    }

    /// <summary>The hour of the housing day (0-24): stays end at its checkout hour.</summary>
    public static double HourOfDay => (Now - Math.Floor(Now)) * 24.0;
}

/// <summary>
/// Runs housing where the rules run (offline, the server): the books (<see cref="HousingBook"/>) and their save, the
/// time, what the rules decide that the world must follow - a stay or lease ended: its chests are emptied into held
/// items, its doors, beds, chests and gardens no longer open for the old tenant; money owed is paid into wallets of
/// players who are online; notices reach them - and the contents of every chest of a rented place, kept between
/// sessions. It knows every property and inn of the loaded scenes (they register themselves). On a network client it
/// only keeps that list (the server runs the rest).
/// </summary>
[DefaultExecutionOrder(-40)]
public class HousingDirector : MonoBehaviour
{
    private static HousingDirector instance;
    private static readonly List<Property> properties = new List<Property>();
    private static readonly List<Inn> inns = new List<Inn>();
    private static HousingBook book;
    private static bool worldDirty, chestsDirty;

    private readonly Dictionary<string, CombatEntity> online = new Dictionary<string, CombatEntity>();
    private readonly Dictionary<CombatEntity, string> keyOf = new Dictionary<CombatEntity, string>();
    /// <summary>Who may still open a place's doors from inside after their access ended (place key + player key: until when).</summary>
    private readonly Dictionary<string, float> exitGrace = new Dictionary<string, float>();
    /// <summary>How long a player without access has stood inside a rented interior (real seconds).</summary>
    private readonly Dictionary<CombatEntity, float> inside = new Dictionary<CombatEntity, float>();
    private float nextSweep;

    /// <summary>Real seconds a player whose access ended while inside may still open its doors to leave.</summary>
    public const float ExitGraceSeconds = 120f;
    /// <summary>Real seconds a player without access may stand inside a rented interior before they are shown out.</summary>
    public const float ShowOutSeconds = 3f;
    private float nextPlayers;

    public static IReadOnlyList<Property> Properties => properties;
    public static IReadOnlyList<Inn> Inns => inns;

    /// <summary>A player's access changed somewhere (their key): the network layer sends them their new summary.</summary>
    public static event Action<string> AccessChanged;
    /// <summary>The books changed (open windows refresh).</summary>
    public static event Action BooksChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        properties.Clear();
        inns.Clear();
        book = null;
        worldDirty = chestsDirty = false;
        AccessChanged = null;
        BooksChanged = null;
        // (With domain reload off the store's events were emptied: the books hook in again.)
        hooked = broken = false;
        Closed = "";
        quiet = 0;
        indexed = null;
        chestIndex.Clear();
        chestsOfPlace.Clear();
        sceneContents.Clear();
        fresh.Clear();
        nextFullCapture = 0f;
    }

    public static HousingSettings Settings => HousingSettings.Instance;
    public static HousingRules Rules => Settings.Rules;

    /// <summary>Lease days the books have reached (where the rules run).</summary>
    public static double Now => book != null ? book.Now : HousingClock.Now;

    /// <summary>The books (where the rules run; null on a network client). Loaded the first time.</summary>
    public static HousingBook Book
    {
        get
        {
            if (!GameNet.NetAuthority.IsServer || !Application.isPlaying) return null;
            if (book == null) LoadBook();
            return book;
        }
    }

    /// <summary>The housing books' part of the world's store.</summary>
    public const string StoreSection = "housing";

    /// <summary>Why the housing office is closed here (empty when it is open).</summary>
    public static string Closed { get; private set; } = "";
    private static bool broken, hooked;

    // ---------------------------------------------------------------- registration
    public static void Register(Property p)
    {
        if (p == null || properties.Contains(p)) return;
        HousingHotkey.Ensure(); // the player's own housing opens with a key
        Property twin = FindProperty(p.PropertyId);
        if (twin != null && twin != p)
        {
            // Two houses with one id would share one lease: the second is closed until the id is made unique (it owns no
            // chest records, and nothing of it opens).
            p.OutOfOrder = true;
            Debug.LogError($"Housing: '{p.DisplayName}' has the same Property Id as '{twin.DisplayName}' (a copied object or prefab): it is closed. Give it a New Id in its inspector.", p);
        }
        else UniqueChestIds(p.chests, p.DisplayName);
        properties.Add(p);
        HousingGate.Register(p);
        fresh.Add(p);
        Ensure();
        worldDirty = true;
    }

    public static void Unregister(Property p)
    {
        if (!properties.Remove(p)) return;
        fresh.Remove(p);
        // Only the place that leaves is recorded (nobody else's chest window is closed).
        if (book != null && !p.OutOfOrder) CaptureChests(new HousingPlace(p));
        ForgetScene(p.chests);
        HousingGate.Unregister(p);
        worldDirty = true;
    }

    /// <summary>Is the property registered (in a loaded scene)?</summary>
    public static bool IsRegistered(Property p) => p != null && properties.Contains(p);

    /// <summary>Is the inn registered (in a loaded scene)?</summary>
    public static bool IsRegistered(Inn inn) => inn != null && inns.Contains(inn);

    /// <summary>
    /// Two chests of one place with one id (a copied list entry) would share one record: the later ones are kept under an
    /// id made from their place in the scene (the same every session) until they are given their own.
    /// </summary>
    private static void UniqueChestIds(List<HousingChest> chests, string where)
    {
        if (chests == null) return;
        var ids = new HashSet<string>();
        foreach (HousingChest c in chests)
        {
            if (c == null || c.storage == null || string.IsNullOrEmpty(c.id) || ids.Add(c.id)) continue;
            string was = c.id;
            c.id = HousingIds.Stable(c.storage, "chest");
            ids.Add(c.id);
            Debug.LogWarning($"Housing: two chests of '{where}' have the id '{was}': '{c.storage.name}' is kept as '{c.id}' (from its place in the scene). Give it its own id in the inspector.", c.storage);
        }
    }

    public static void Register(Inn inn)
    {
        if (inn == null || inns.Contains(inn)) return;
        HousingHotkey.Ensure();
        Inn twin = FindInn(inn.InnId);
        if (twin != null && twin != inn)
        {
            inn.OutOfOrder = true;
            Debug.LogError($"Housing: '{inn.DisplayName}' has the same Inn Id as '{twin.DisplayName}' (a copied object or prefab): it is closed. Give it a New Id in its inspector.", inn);
        }
        else
        {
            // Two rooms with one id would share one stay and one chest: the later one is closed.
            var ids = new HashSet<string>();
            foreach (InnRoom r in inn.rooms)
            {
                if (r == null) continue;
                r.Collect();
                r.OutOfOrder = !ids.Add(r.RoomId);
                if (r.OutOfOrder)
                {
                    Debug.LogError($"Housing: two rooms of '{inn.DisplayName}' have the Room Id '{r.RoomId}' (a copied object): '{r.DisplayName}' is closed. Give it a New Id in its inspector.", r);
                    continue;
                }
                UniqueChestIds(r.chests, $"{r.DisplayName}, {inn.DisplayName}");
            }
        }
        inns.Add(inn);
        HousingGate.Register(inn);
        fresh.Add(inn);
        Ensure();
        worldDirty = true;
    }

    public static void Unregister(Inn inn)
    {
        if (!inns.Remove(inn)) return;
        fresh.Remove(inn);
        foreach (InnRoom r in inn.rooms)
        {
            if (r == null) continue;
            if (book != null) CaptureChests(new HousingPlace(inn, r));
            ForgetScene(r.chests);
        }
        HousingGate.Unregister(inn);
        worldDirty = true;
    }

    public static Property FindProperty(string id)
    {
        foreach (Property p in properties)
            if (p != null && !p.OutOfOrder && p.PropertyId == id) return p;
        return null;
    }

    public static Inn FindInn(string id)
    {
        foreach (Inn i in inns)
            if (i != null && !i.OutOfOrder && i.InnId == id) return i;
        return null;
    }

    /// <summary>The place named by ids (a property, or an inn's room), if it is loaded.</summary>
    public static HousingPlace FindPlace(string placeId, string roomId)
    {
        if (string.IsNullOrEmpty(roomId))
        {
            Property p = FindProperty(placeId);
            return p != null ? new HousingPlace(p) : default;
        }
        Inn inn = FindInn(placeId);
        InnRoom room = inn != null ? inn.Room(roomId) : null;
        return room != null ? new HousingPlace(inn, room) : default;
    }

    private static void Ensure()
    {
        if (instance != null || !Application.isPlaying) return;
        var go = new GameObject("[Housing Director]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<HousingDirector>();
    }

    // ---------------------------------------------------------------- the books and their save
    private static void LoadBook()
    {
        if (broken) return;
        Hook();
        if (!WorldStore.IsOpen)
        {
            Closed = string.IsNullOrEmpty(WorldStore.Problem) ? "The housing office is closed." : "The housing office is closed: " + WorldStore.Problem;
            return;
        }
        HousingSave save = null;
        string json = WorldStore.Get(StoreSection);
        bool resumed = !string.IsNullOrEmpty(json);
        if (resumed)
        {
            try
            {
                save = JsonUtility.FromJson<HousingSave>(json);
            }
            catch (Exception e)
            {
                save = null;
                Debug.LogError($"Housing: the housing books cannot be read ({e.Message}). The office stays closed, and nothing is written over them.");
            }
            if (save == null)
            {
                broken = true;
                Closed = "The housing office is closed (its books cannot be read).";
                return;
            }
        }
        save ??= new HousingSave();
        // The world runs on the clock the settings give it (Auto: real days online, game days offline). A world kept on
        // another clock changes to it, keeping what is left of every lease, stay and held bundle in lease days (a lease day
        // then lasts what the new clock says) - it is said once, in the log.
        HousingClockKind wanted = Rules.ClockIn(GameNet.NetAuthority.IsNetworked);
        HousingClockKind had = (HousingClockKind)save.clock == HousingClockKind.GameTime ? HousingClockKind.GameTime : HousingClockKind.RealTime;
        if (resumed && had != wanted)
            Debug.LogWarning($"Housing: the world '{WorldStore.WorldId}' was kept on {had} lease days and now runs on {wanted} (the settings' Clock): what is left of every lease and stay is kept in lease days.");
        save.clock = (int)wanted;
        WorldStore.CommitDelaySeconds = Settings.saveDelaySeconds;
        HousingClockKind kind = (HousingClockKind)save.clock == HousingClockKind.GameTime ? HousingClockKind.GameTime : HousingClockKind.RealTime;
        // Back after a pause: housing time holds still a few minutes (at most as long as the world was down).
        double down = save.savedUtc > 0 ? Math.Max(0.0, (DateTime.UtcNow.Ticks - save.savedUtc) / (double)TimeSpan.TicksPerSecond) : 0.0;
        double holdSeconds = resumed ? Math.Min(Rules.restartHoldMinutes * 60.0, down) : 0.0;
        HousingClock.Start(save.lastAdvance, kind, holdSeconds);
        book = new HousingBook(Rules);
        book.Load(save);
        book.HourOfDay = HourForBooks();
        Closed = "";
        worldDirty = true;
        // A new book: every place's chests are filled from it.
        fresh.Clear();
        foreach (Property p in properties) if (p != null) fresh.Add(p);
        foreach (Inn i in inns) if (i != null) fresh.Add(i);
        SetWorldNow();
    }

    /// <summary>
    /// The hour the books end stays at. On game days: the hour of the game's sky (its Day Night Cycle or Weather System;
    /// else the housing day's), so a room taken in the evening runs until the next morning the player sees. On real days
    /// none (-1): a stay is whole lease days from when it began.
    /// </summary>
    private static double HourForBooks()
    {
        if (HousingClock.Kind != HousingClockKind.GameTime) return -1.0;
        if (DayNightCycle.Instance != null && DayNightCycle.Instance.isActiveAndEnabled) return DayNightCycle.Instance.Hour;
        if (WeatherSystem.Instance != null && WeatherSystem.Instance.TimeOfDay >= 0f) return WeatherSystem.Instance.TimeOfDay;
        return HousingClock.HourOfDay;
    }

    private static void Hook()
    {
        if (hooked) return;
        hooked = true;
        WorldStore.Committing += PutBooks;
        WorldStore.WorldChanged += OnWorldChanged;
    }

    /// <summary>Another world (a session started or ended): the books of this one are put away; the other's load when needed.</summary>
    private static void OnWorldChanged()
    {
        book = null;
        broken = false;
        Closed = "";
        HousingClock.Stop();
        worldDirty = true;
        IndexChests();
        // What the chests hold is the old world's (written with it as it closed): each chest housing filled goes back to
        // what the scene gave it, and the next world fills it from its own books - belongings never cross between worlds.
        var gone = new List<Storage>();
        foreach (KeyValuePair<Storage, List<StorageItem>> kv in sceneContents)
        {
            Storage s = kv.Key;
            if (s == null)
            {
                gone.Add(s);
                continue;
            }
            CloseQuietly(s);
            s.items = CopyItems(kv.Value);
            while (s.items.Count < s.size) s.items.Add(new StorageItem(0, null));
        }
        foreach (Storage s in gone) sceneContents.Remove(s);
        if (instance != null)
        {
            instance.exitGrace.Clear();
            instance.inside.Clear();
        }
    }

    /// <summary>The books learn the world as it is now (properties and inns registered meanwhile), and the chests are filled.</summary>
    private static void SetWorldNow()
    {
        worldDirty = false;
        if (book == null) return;
        var facts = new List<PropertyFacts>();
        foreach (Property p in properties) if (p != null && !p.OutOfOrder) facts.Add(p.Facts());
        var innFacts = new List<InnFacts>();
        foreach (Inn i in inns) if (i != null && !i.OutOfOrder) innFacts.Add(i.Facts());
        book.SetWorld(facts, innFacts);
        // Only the places that came since (a scene loaded, a new book) are filled: the others' chests are as the books say
        // already, and nobody's open chest window is closed.
        foreach (Property p in properties) if (p != null && fresh.Contains(p)) RestoreChests(new HousingPlace(p));
        foreach (Inn i in inns)
            if (i != null && fresh.Contains(i))
                foreach (InnRoom r in i.rooms)
                    if (r != null) RestoreChests(new HousingPlace(i, r));
        fresh.Clear();
    }

    /// <summary>The books changed: they are written with the world in a moment (with the players' money, in one file).</summary>
    public static void SaveNow(bool closeChests = false)
    {
        if (book == null) return;
        if (closeChests) PutBooks(true);
        WorldStore.MarkDirty();
    }

    /// <summary>The world is being written: the books' part goes in, with the chests as they are now.</summary>
    private static void PutBooks() => PutBooks(false);

    private static void PutBooks(bool closeChests)
    {
        if (book == null || broken) return;
        // Every chest when the world is written for the last time (their windows closed: what they show is written back)
        // and about once a minute; otherwise those open in a window (a chest whose window closed was recorded then).
        bool closing = closeChests || WorldStore.Closing;
        bool all = closing || Time.unscaledTime >= nextFullCapture;
        if (all) nextFullCapture = Time.unscaledTime + FullCaptureSeconds;
        foreach (Property p in properties)
            if (p != null && !p.OutOfOrder) CaptureIf(new HousingPlace(p), all, closing);
        foreach (Inn i in inns)
            if (i != null && !i.OutOfOrder)
                foreach (InnRoom r in i.rooms)
                    if (r != null && !r.OutOfOrder) CaptureIf(new HousingPlace(i, r), all, closing);
        HousingSave save = book.Save;
        save.savedUtc = DateTime.UtcNow.Ticks;
        if (HousingClock.Started) save.lastAdvance = Math.Max(save.lastAdvance, HousingClock.Now);
        WorldStore.Put(StoreSection, JsonUtility.ToJson(save));
        chestsDirty = false;
    }

    // ---------------------------------------------------------------- chests
    // A chest's contents belong to a tenancy: every chest of a rented place has one record in the books, never deleted,
    // named by its place and its chest id (two places whose chests share an id never share a record), naming the tenancy
    // that owns what is in it. Whenever the place's tenancy is not the record's (a stay or lease ended - even if that
    // moment was missed by a crash, or the chest's scene was not loaded), what the record holds is held for its owner and
    // the chest starts empty for the new one. Doing it twice changes nothing. A closed place (its id is another's) owns no
    // chest: it is never read or written.

    /// <summary>What a reconcile took for one owner (one bundle).</summary>
    private sealed class Taken
    {
        public string key = "", name = "";
        public readonly List<HeldStack> stacks = new List<HeldStack>();
    }

    /// <summary>Above 0 while the director closes chest windows itself (what they show is recorded by the director, not on release).</summary>
    private static int quiet;
    private static HousingSave indexed;
    private static readonly Dictionary<string, ChestContents> chestIndex = new Dictionary<string, ChestContents>();
    private static readonly Dictionary<string, List<ChestContents>> chestsOfPlace = new Dictionary<string, List<ChestContents>>();
    /// <summary>What each chest held when housing first touched it (what the scene gave it): what it goes back to when the world changes.</summary>
    private static readonly Dictionary<Storage, List<StorageItem>> sceneContents = new Dictionary<Storage, List<StorageItem>>();
    /// <summary>Places whose chests are filled from the books at the next <see cref="SetWorldNow"/> (they came since).</summary>
    private static readonly HashSet<UnityEngine.Object> fresh = new HashSet<UnityEngine.Object>();
    private static float nextFullCapture;

    /// <summary>Real seconds between two saves that record every chest (the saves between record those open in a window).</summary>
    public const float FullCaptureSeconds = 60f;

    /// <summary>The chests of a place: those it lists that belong to it (not to a place registered before it).</summary>
    private static List<HousingChest> ChestsOf(HousingPlace place)
    {
        var list = new List<HousingChest>();
        if (!place.IsValid || place.OutOfOrder) return list;
        List<HousingChest> listed = place.property != null ? place.property.chests : place.room.chests;
        if (listed == null) return list;
        foreach (HousingChest c in listed)
            if (c != null && c.storage != null && !string.IsNullOrEmpty(c.id) && HousingGate.Owns(place, c.storage)) list.Add(c);
        return list;
    }

    /// <summary>The tenancy that owns a place's chests now (0: nobody rents it), with its tenant.</summary>
    private static (long id, string key, string name) OwnerOf(string placeId, string roomId)
    {
        Tenancy t = book?.TenancyOf(placeId, roomId, book.Now);
        return t != null ? (t.id, t.key, t.name) : (0L, "", "");
    }

    private static string ChestKey(string placeKey, string id) => placeKey + "\n" + id;

    /// <summary>The records indexed by place and chest id (made again when the books are another's).</summary>
    private static void IndexChests()
    {
        HousingSave save = book?.Save;
        if (save == indexed) return;
        indexed = save;
        chestIndex.Clear();
        chestsOfPlace.Clear();
        if (save?.chests == null) return;
        foreach (ChestContents rec in save.chests)
        {
            if (rec == null || string.IsNullOrEmpty(rec.id)) continue;
            rec.place ??= "";
            string k = ChestKey(rec.place, rec.id);
            if (chestIndex.ContainsKey(k)) continue; // (a second record of one chest is never made: the first one counts)
            chestIndex[k] = rec;
            if (!chestsOfPlace.TryGetValue(rec.place, out List<ChestContents> l)) chestsOfPlace[rec.place] = l = new List<ChestContents>();
            l.Add(rec);
        }
    }

    private static ChestContents Find(string placeKey, string id)
    {
        IndexChests();
        return chestIndex.TryGetValue(ChestKey(placeKey, id), out ChestContents rec) ? rec : null;
    }

    /// <summary>The record of a chest of a place (made when it has none).</summary>
    private static ChestContents Record(HousingChest c, string placeKey)
    {
        ChestContents saved = Find(placeKey, c.id);
        if (saved != null) return saved;
        saved = new ChestContents { id = c.id, place = placeKey, owner = -1 };
        book.Save.chests.Add(saved);
        chestIndex[ChestKey(placeKey, c.id)] = saved;
        if (!chestsOfPlace.TryGetValue(placeKey, out List<ChestContents> l)) chestsOfPlace[placeKey] = l = new List<ChestContents>();
        l.Add(saved);
        return saved;
    }

    private static void Relabel(ChestContents rec, (long id, string key, string name) owner)
    {
        rec.owner = owner.id;
        rec.ownerKey = owner.key;
        rec.ownerName = owner.name;
    }

    private static Taken TakenFor(List<Taken> taken, string key, string name)
    {
        foreach (Taken t in taken)
            if (t.key == key) return t;
        var n = new Taken { key = key, name = name ?? "" };
        taken.Add(n);
        return n;
    }

    /// <summary>
    /// Makes a place's chests and their records agree with its tenancy (<paramref name="placeId"/>, <paramref name="roomId"/>;
    /// <paramref name="place"/> when it is loaded): what belongs to an earlier tenancy is taken out for its tenant - from the
    /// chests that are here, and from the records of those that are not - and those chests are emptied. What nobody owned
    /// (the place was free) stays: the new tenant's. Returns what was taken, per owner.
    /// </summary>
    private static List<Taken> Reconcile(HousingPlace place, string placeId, string roomId)
    {
        var taken = new List<Taken>();
        if (book == null || (place.IsValid && place.OutOfOrder)) return taken;
        IndexChests();
        string placeKey = HousingGate.KeyOf(placeId, roomId);
        (long id, string key, string name) now = OwnerOf(placeId, roomId);
        var seen = new HashSet<string>();
        if (place.IsValid)
            foreach (HousingChest c in ChestsOf(place))
            {
                if (!seen.Add(c.id)) continue;
                Storage s = c.storage;
                RememberScene(s);
                ChestContents rec = Record(c, placeKey);
                if (rec.owner < 0)
                {
                    // Seen for the first time: what the scene put in it is the place's own, given to whoever rents it.
                    Relabel(rec, now);
                    continue;
                }
                if (rec.owner == now.id) continue;
                if (!string.IsNullOrEmpty(rec.ownerKey))
                {
                    CloseQuietly(s); // its window is written back first
                    Taken t = TakenFor(taken, rec.ownerKey, rec.ownerName);
                    for (int i = 0; i < s.items.Count; i++)
                    {
                        StorageItem it = s.items[i];
                        if (it == null || it.itemScriptableObject == null || it.currentStack <= 0) continue;
                        HeldStack h = ToHeld(it);
                        if (h == null) continue; // no id: it stays in the chest (warned when saved)
                        t.stacks.Add(h);
                        s.items[i] = new StorageItem(0, null);
                    }
                    t.stacks.AddRange(rec.pending);
                    rec.slots.Clear();
                    rec.pending.Clear();
                }
                Relabel(rec, now);
                chestsDirty = true;
            }
        // Chests of the place that are not here now (not loaded, or gone): what their records say they hold.
        if (chestsOfPlace.TryGetValue(placeKey, out List<ChestContents> records))
            foreach (ChestContents rec in records)
            {
                if (seen.Contains(rec.id) || rec.owner < 0 || rec.owner == now.id) continue;
                if (!string.IsNullOrEmpty(rec.ownerKey))
                {
                    Taken t = TakenFor(taken, rec.ownerKey, rec.ownerName);
                    foreach (ChestSlot slot in rec.slots) if (slot?.stack != null) t.stacks.Add(slot.stack);
                    t.stacks.AddRange(rec.pending);
                    rec.slots.Clear();
                    rec.pending.Clear();
                }
                Relabel(rec, now);
                chestsDirty = true;
            }
        return taken;
    }

    /// <summary>Holds what a reconcile took: one bundle per owner.</summary>
    private static void HoldTaken(List<Taken> taken, string source, double at)
    {
        foreach (Taken t in taken)
            if (t.stacks.Count > 0 && !string.IsNullOrEmpty(t.key)) book.Hold(t.key, t.name, source, t.stacks, at);
    }

    /// <summary>Fills a place's chests from the books (after reconciling them: an earlier tenant's things are held for them).</summary>
    private static void RestoreChests(HousingPlace place)
    {
        if (book == null || !place.IsValid || place.OutOfOrder) return;
        string placeKey = place.Key;
        HoldTaken(Reconcile(place, place.PlaceId, place.RoomId), place.Name, book.Now);
        foreach (HousingChest c in ChestsOf(place))
        {
            ChestContents saved = Find(placeKey, c.id);
            if (saved == null || saved.version == 0) continue; // never captured yet: the chest keeps what the scene gives it
            Storage s = c.storage;
            if (IsShown(s)) continue; // open in a window: what the window shows stands (recorded with the next save)
            int size = Mathf.Max(s.size, s.items.Count);
            var items = new List<StorageItem>(size);
            for (int i = 0; i < size; i++) items.Add(new StorageItem(0, null));
            var unresolved = new List<HeldStack>(saved.pending);
            foreach (ChestSlot slot in saved.slots)
            {
                ItemSO item = slot?.stack != null ? ItemDatabase.Item(slot.stack.itemId) : null;
                if (item == null || slot.index < 0 || slot.index >= size || items[slot.index].itemScriptableObject != null)
                {
                    if (slot?.stack != null) unresolved.Add(slot.stack);
                    continue;
                }
                items[slot.index] = ToStorage(slot.stack, item);
            }
            // What could not be placed waits (kept with the chest, held with it when the tenancy ends).
            for (int k = unresolved.Count - 1; k >= 0; k--)
            {
                ItemSO item = ItemDatabase.Item(unresolved[k].itemId);
                int free = items.FindIndex(x => x.itemScriptableObject == null || x.currentStack <= 0);
                if (item == null || free < 0) continue;
                items[free] = ToStorage(unresolved[k], item);
                unresolved.RemoveAt(k);
            }
            // The chest is exactly what the books say (never what the scene gave it added to what was saved).
            s.items = items;
            saved.pending = unresolved;
        }
    }

    /// <summary>Records a place's chests when everything is recorded (<paramref name="all"/>) or one of them is open in a window.</summary>
    private static void CaptureIf(HousingPlace place, bool all, bool close)
    {
        if (!all)
        {
            bool shown = false;
            List<HousingChest> listed = place.property != null ? place.property.chests : place.room != null ? place.room.chests : null;
            if (listed != null)
                foreach (HousingChest c in listed)
                    if (c != null && IsShown(c.storage)) { shown = true; break; }
            if (!shown) return;
        }
        CaptureChests(place, close);
    }

    /// <summary>Records what a place's chests hold (reconciled first; closing them when <paramref name="close"/>: their window is written back).</summary>
    private static void CaptureChests(HousingPlace place, bool close = false)
    {
        if (book == null || !place.IsValid || place.OutOfOrder) return;
        string placeKey = place.Key;
        HoldTaken(Reconcile(place, place.PlaceId, place.RoomId), place.Name, book.Now);
        foreach (HousingChest c in ChestsOf(place))
        {
            Storage s = c.storage;
            if (close) CloseQuietly(s);
            ChestContents saved = Record(c, placeKey);
            saved.version = 1;
            saved.slots.Clear();
            // An open chest's items are in its user's window (the chest's own list is what it held when opened).
            List<StorageItem> items = LiveItems(s);
            for (int i = 0; i < items.Count; i++)
            {
                StorageItem it = items[i];
                if (it == null || it.itemScriptableObject == null || it.currentStack <= 0) continue;
                HeldStack h = ToHeld(it);
                if (h == null)
                {
                    Debug.LogWarning($"Housing: '{it.itemScriptableObject.name}' in '{s.name}' has no item id (it is not in the Item Database): it cannot be kept.", s);
                    continue;
                }
                saved.slots.Add(new ChestSlot { index = i, stack = h });
            }
        }
    }

    /// <summary>Closes a chest's window (what it shows is written back into the chest) without the release recording it.</summary>
    private static void CloseQuietly(Storage s)
    {
        if (s == null || s.User == null) return;
        quiet++;
        try
        {
            s.User.CloseStorageIf(x => x == s);
        }
        finally
        {
            quiet--;
        }
    }

    /// <summary>Is the chest open in a player's window now (what it holds is in that window)?</summary>
    private static bool IsShown(Storage s)
    {
        InventoryManager user = s != null ? s.User : null;
        return user != null && user.IsAuthoritative && user.OpenedStorage == s;
    }

    /// <summary>Remembers what a chest holds the first time housing touches it: what the scene gave it.</summary>
    private static void RememberScene(Storage s)
    {
        if (s != null && !sceneContents.ContainsKey(s)) sceneContents[s] = CopyItems(s.items);
    }

    private static void ForgetScene(List<HousingChest> chests)
    {
        if (chests == null) return;
        foreach (HousingChest c in chests)
            if (c != null && !ReferenceEquals(c.storage, null)) sceneContents.Remove(c.storage);
    }

    private static List<StorageItem> CopyItems(List<StorageItem> items)
    {
        var copy = new List<StorageItem>(items != null ? items.Count : 0);
        if (items == null) return copy;
        foreach (StorageItem it in items)
            copy.Add(it == null ? new StorageItem(0, null) : new StorageItem(it.currentStack, it.itemScriptableObject)
            {
                durabilities = it.durabilities != null ? new List<int>(it.durabilities) : new List<int>(),
                durability = it.durability,
                loadedRounds = it.loadedRounds,
            });
        return copy;
    }

    /// <summary>
    /// What a chest holds right now: while a player has it open, its window's slots (the chest's own list is written back
    /// only when the window closes); otherwise its list.
    /// </summary>
    public static List<StorageItem> LiveItems(Storage s)
    {
        InventoryManager user = s != null ? s.User : null;
        if (user == null || !user.IsAuthoritative || user.OpenedStorage != s) return s != null ? s.items : new List<StorageItem>();
        var live = new List<StorageItem>();
        int size = Mathf.Max(s.size, s.items.Count);
        for (int i = 0; i < size; i++)
        {
            InventorySlot slot = user.SlotAt(new GameNet.InventorySlotRef(GameNet.InventoryArea.Storage, i));
            InventoryItem item = slot != null && slot.heldItem != null ? slot.heldItem.GetComponent<InventoryItem>() : null;
            if (item == null || item.itemScriptableObject == null || item.stackCurrent <= 0)
            {
                live.Add(new StorageItem(0, null));
                continue;
            }
            live.Add(new StorageItem(item.stackCurrent, item.itemScriptableObject)
            {
                durabilities = item.DurabilityList != null ? new List<int>(item.DurabilityList) : new List<int>(),
                durability = item.durability,
                loadedRounds = item.loadedRounds,
            });
        }
        return live;
    }

    private static HeldStack ToHeld(StorageItem it)
    {
        string id = ItemDatabase.Id(it.itemScriptableObject);
        if (string.IsNullOrEmpty(id)) return null;
        return new HeldStack
        {
            itemId = id,
            count = it.currentStack,
            durabilities = it.durabilities != null ? new List<int>(it.durabilities) : new List<int>(),
            durability = it.durability,
            loadedRounds = it.loadedRounds,
        };
    }

    private static StorageItem ToStorage(HeldStack h, ItemSO item) => new StorageItem(Mathf.Max(1, h.count), item)
    {
        durabilities = h.durabilities != null ? new List<int>(h.durabilities) : new List<int>(),
        durability = h.durability,
        loadedRounds = h.loadedRounds,
    };

    private static void OnChestReleased(Storage s)
    {
        if (quiet > 0) return; // closed by the director itself, which records it
        ChestChanged(s);
    }

    /// <summary>
    /// What a chest of a rented place holds was changed by a script (not through its window): it is recorded now and saved
    /// with the next save. (Windows are followed by themselves, and every chest is recorded about once a minute.)
    /// </summary>
    public static void ChestChanged(Storage s)
    {
        if (book == null || s == null || !HousingGate.PlaceOf(s, out HousingPlace place)) return;
        CaptureChests(place);
        chestsDirty = true;
    }

    // ---------------------------------------------------------------- claims
    /// <summary>
    /// Gives a player what is held for them in one bundle (as much as fits; the rest stays held). Where the rules run.
    /// The claim fee (after the free days) is paid from their wallet first. False with a reason when nothing was given.
    /// </summary>
    public static bool Claim(CombatEntity player, long bundleId, out string message)
    {
        message = "";
        HousingBook b = Book;
        string key = PlayerKeys.Of(player);
        HeldBundle bundle = b?.Held(bundleId);
        if (bundle == null || key == null || bundle.key != key) { message = "Nothing is held for you under that."; return false; }
        InventoryManager inv = InventoryManager.For(player);
        if (inv == null || !inv.IsAuthoritative) { message = "You cannot carry anything now."; return false; }
        long fee = b.ClaimFee(bundle, Now);
        CurrencyWallet wallet = fee > 0 ? CurrencyWallet.For(player) : null;
        CurrencyDefinition money = Settings.Currency;
        if (wallet != null && (wallet.AwaitingRestore || WalletSaves.IsRestoring(wallet))) { message = "One moment: your purse is being fetched."; return false; }
        if (fee > 0 && (wallet == null || !wallet.TrySpend(money, (int)Math.Min(int.MaxValue, fee))))
        {
            message = $"Keeping it this long costs {money.Format((int)Math.Min(int.MaxValue, fee))}.";
            return false;
        }
        var left = new List<HeldStack>();
        int given = 0;
        foreach (HeldStack h in bundle.stacks)
        {
            ItemSO item = h != null ? ItemDatabase.Item(h.itemId) : null;
            if (item == null || h.count <= 0)
            {
                if (h != null && h.count > 0) left.Add(h);
                continue;
            }
            // Exactly as it was kept: each unit's uses left, the uses left of the unit in use, the rounds loaded.
            int notAdded = inv.AddKeptStack(item, h.count, h.durabilities, h.durability, h.loadedRounds, out List<int> restUses, out bool restInUse);
            given += h.count - notAdded;
            if (notAdded <= 0) continue;
            left.Add(new HeldStack
            {
                itemId = h.itemId, count = notAdded, durabilities = restUses,
                durability = restInUse ? h.durability : 0f, loadedRounds = restInUse ? h.loadedRounds : -1,
            });
        }
        if (given == 0 && fee > 0) wallet.TryAdd(money, (int)Math.Min(int.MaxValue, fee)); // nothing given: nothing paid
        if (given == 0)
        {
            message = "You have no room for it.";
            return false;
        }
        b.Claimed(bundleId, left);
        message = left.Count == 0 ? "Here is everything that was kept for you." : "Here is what you can carry; the rest is still kept for you.";
        return true;
    }

    // ---------------------------------------------------------------- players
    /// <summary>The player online with this key (null: not online on this server).</summary>
    public static CombatEntity Online(string key)
    {
        if (instance == null || string.IsNullOrEmpty(key)) return null;
        return instance.online.TryGetValue(key, out CombatEntity e) && e != null ? e : null;
    }

    /// <summary>The key of a player online (null: none).</summary>
    public static string KeyOf(CombatEntity player)
    {
        if (player == null) return null;
        if (instance != null && instance.keyOf.TryGetValue(player, out string k) && k != null) return k;
        return PlayerKeys.Of(player);
    }

    private void RefreshPlayers()
    {
        online.Clear();
        keyOf.Clear();
        long utc = DateTime.UtcNow.Ticks;
        IReadOnlyList<CombatEntity> list = CombatEntity.Players;
        for (int i = 0; i < list.Count; i++)
        {
            CombatEntity e = list[i];
            if (e == null || e.Kind != CombatEntity.EntityKind.Player) continue;
            string key = PlayerKeys.Of(e);
            if (key == null) continue;
            online[key] = e;
            keyOf[e] = key;
            book?.Seen(key, PartySystem.NameOf(e), utc, 2.0);
            PayOut(key);
        }
    }

    /// <summary>Pays what is owed to a player into their wallet, when they are online and it fits.</summary>
    public static void PayOut(string key)
    {
        HousingAccount a = book?.Account(key);
        if (a == null || a.owed <= 0) return;
        CombatEntity p = Online(key);
        CurrencyWallet w = p != null ? CurrencyWallet.For(p) : null;
        // (Not while its kept balances are still to come back: what is paid in would not be saved with the books. Tried again in a moment.)
        if (w == null || w.IsReplica || w.AwaitingRestore || WalletSaves.IsRestoring(w)) return;
        CurrencyDefinition money = Settings.Currency;
        int amount = (int)Math.Min(a.owed, (long)Math.Max(0, money.MaxAmount - w.GetBalance(money)));
        if (amount <= 0 || !w.TryAdd(money, amount)) return;
        book.PaidOut(key, amount);
        Tell(key, $"{money.Format(amount)} the housing office owed you is back in your purse.");
    }

    /// <summary>A message for a player, if they are online.</summary>
    public static void Tell(string key, string text)
    {
        CombatEntity p = Online(key);
        if (p != null && !string.IsNullOrEmpty(text)) HubWorld.HubWorldMessages.Send(p, text);
    }

    // ---------------------------------------------------------------- running
    private void OnEnable() => Storage.AnyReleased += OnChestReleased;

    private void OnDisable() => Storage.AnyReleased -= OnChestReleased;

    private void Update()
    {
        if (!GameNet.NetAuthority.IsServer) return; // a client: the server runs the books
        HousingBook b = Book;
        if (b == null) return;
        if (worldDirty) SetWorldNow();
        b.Rules = Rules;
        HousingClock.Tick(Rules);
        float t = Time.unscaledTime;
        if (t >= nextPlayers)
        {
            nextPlayers = t + 2f;
            RefreshPlayers();
        }
        b.RequireResidency = GameNet.NetAuthority.IsNetworked;
        if (t >= nextSweep)
        {
            nextSweep = t + 1f;
            Sweep(b, t);
        }
        // The books move to now every frame, and the world follows what they decided, before any request is looked at.
        CatchUp();
        WorldStore.CommitDelaySeconds = Settings.saveDelaySeconds;
        if (b.Revision != lastRevision)
        {
            lastRevision = b.Revision;
            WorldStore.MarkDirty();
            BooksChanged?.Invoke();
        }
        if (chestsDirty)
        {
            // A chest changed: saved in a moment (no window shows what chests hold, so none is refreshed).
            chestsDirty = false;
            WorldStore.MarkDirty();
        }
    }

    private int lastRevision = -1;

    /// <summary>
    /// The books are brought to the clock's now and the world follows what they decided (chests emptied, doors, notices).
    /// Every request is done after this, so it sees exactly what the books say (a room whose stay just ended is free only
    /// once its guest's things are held for them).
    /// </summary>
    public static void CatchUp()
    {
        HousingBook b = Book;
        if (b == null) return;
        b.HourOfDay = HourForBooks();
        b.ClearingBudgetMs = Settings.clearingBudgetMs;
        try
        {
            b.Advance(HousingClock.Now, DateTime.UtcNow.Ticks);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        ApplyEvents(b);
    }

    /// <summary>What the rules decided, done in the world.</summary>
    private static void ApplyEvents(HousingBook b)
    {
        if (b.Events.Count == 0) return;
        var events = new List<HousingEvent>(b.Events);
        b.Events.Clear();
        foreach (HousingEvent e in events)
        {
            switch (e.kind)
            {
                case HousingEventKind.TenancyEnded:
                {
                    HousingPlace place = FindPlace(e.placeId, e.roomId);
                    var stacks = new List<HeldStack>();
                    foreach (Taken t in Reconcile(place, e.placeId, e.roomId))
                    {
                        if (t.key == e.key) stacks.AddRange(t.stacks);
                        // Things of an earlier tenancy found on the way (a moment missed before): theirs, not this tenant's.
                        else if (t.stacks.Count > 0 && !string.IsNullOrEmpty(t.key)) b.Hold(t.key, t.name, e.text, t.stacks, e.at);
                    }
                    chestsDirty = true;
                    // The tenant's ripe crop is theirs: picked for them with what they left in the chests (the gardens that
                    // are this house's - not one another property listed first).
                    if (place.property != null)
                        foreach (SoilField f in place.property.gardens)
                            if (f != null && HousingGate.Owns(place, f))
                                foreach (FarmItemChange g in f.PickRipe(e.name))
                                    stacks.Add(new HeldStack { itemId = g.item, count = g.count, durabilities = g.codes != null ? new List<int>(g.codes) : new List<int>() });
                    b.Hold(e.key, e.name, e.text, stacks, e.at);
                    break;
                }
                case HousingEventKind.AccessChanged:
                {
                    HousingPlace place = FindPlace(e.placeId, e.roomId);
                    // Someone whose access ended while inside may still open the doors to leave, for a while.
                    CombatEntity p = Online(e.key);
                    if (p != null && place.IsValid && place.Inside(p.transform.position) && (HousingGate.AccessOf(place, p) & HousingAccess.Enter) == 0)
                        instance.exitGrace[place.Key + "|" + e.key] = Time.unscaledTime + ExitGraceSeconds;
                    Evict(place, e.key);
                    AccessChanged?.Invoke(e.key);
                    break;
                }
                case HousingEventKind.Notice:
                    Tell(e.key, e.text);
                    break;
                case HousingEventKind.Owed:
                    PayOut(e.key);
                    break;
                case HousingEventKind.HeldDiscarded:
                    break; // (the books tell the player themselves)
            }
        }
        // Anything the events did (holding, paying) may have made more.
        if (b.Events.Count > 0) ApplyEvents(b);
        BooksChanged?.Invoke();
    }

    /// <summary>May this player still open the place's doors to leave (their access ended while they were inside)?</summary>
    public static bool InExitGrace(HousingPlace place, CombatEntity player)
    {
        if (instance == null || player == null || !place.IsValid) return false;
        string key = KeyOf(player);
        return key != null && instance.exitGrace.TryGetValue(place.Key + "|" + key, out float until) && Time.unscaledTime < until;
    }

    /// <summary>
    /// About once a second: the guests in their inn rooms (a room in use counts for the inn's prices), and anyone standing
    /// inside a rented house or room without being let in (who came in behind someone, or stayed after their lease) is
    /// shown out after a moment - unless the house is free to look round, or they are leaving. Anyone let in, with any
    /// right (a friend let in for the beds sleeps there), may stay.
    /// </summary>
    private void Sweep(HousingBook b, float t)
    {
        double now = Now;
        var expired = new List<string>();
        foreach (KeyValuePair<string, float> kv in exitGrace)
            if (t >= kv.Value) expired.Add(kv.Key);
        foreach (string k in expired) exitGrace.Remove(k);
        var seen = new HashSet<CombatEntity>();
        foreach (KeyValuePair<string, CombatEntity> kv in online)
        {
            CombatEntity p = kv.Value;
            if (p == null || !p.isActiveAndEnabled) continue;
            HousingPlace place = PlaceAt(p.transform.position, p.gameObject.scene);
            if (!place.IsValid) continue;
            HousingAccess a = b.AccessOf(place.PlaceId, place.RoomId, kv.Key, now);
            if (place.room != null && a != HousingAccess.None) b.Present(place.PlaceId, place.RoomId, now);
            bool allowed = a != HousingAccess.None || InExitGrace(place, p) || HousingGate.OpenToVisitors(place);
            if (allowed) continue;
            seen.Add(p);
            inside.TryGetValue(p, out float since);
            inside[p] = since + 1f;
            if (since + 1f >= ShowOutSeconds)
            {
                inside.Remove(p);
                ShowOut(p, place);
            }
        }
        var gone = new List<CombatEntity>();
        foreach (CombatEntity p in inside.Keys)
            if (p == null || !seen.Contains(p)) gone.Add(p);
        foreach (CombatEntity p in gone) inside.Remove(p);
    }

    /// <summary>
    /// The place a point is in: a house, else the inn room whose floor is nearest under it (rooms on different floors of
    /// one inn may lie over each other). Closed places count for nothing.
    /// </summary>
    private static HousingPlace PlaceAt(Vector3 at, UnityEngine.SceneManagement.Scene scene)
    {
        foreach (Property prop in properties)
            if (prop != null && !prop.OutOfOrder && prop.gameObject.scene == scene && prop.InHouse(at)) return new HousingPlace(prop);
        HousingPlace best = default;
        float bestY = float.MaxValue;
        foreach (Inn inn in inns)
        {
            if (inn == null || inn.OutOfOrder || inn.gameObject.scene != scene) continue;
            foreach (InnRoom r in inn.rooms)
            {
                if (r == null || r.OutOfOrder || !r.Contains(at)) continue;
                float y = Mathf.Abs(at.y - r.transform.position.y);
                if (y < bestY)
                {
                    bestY = y;
                    best = new HousingPlace(inn, r);
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Puts a player outside a place: just beyond the nearest of its doors that leads out (an inner door, a bedroom's, has
    /// the place on both sides), else just outside its walls - always on the ground, with room to stand, and outside every
    /// house and room (checked with physics). When no such spot is found the player is only asked to leave: nobody is put
    /// in a wall, in another place or off the ground.
    /// </summary>
    private static void ShowOut(CombatEntity p, HousingPlace place)
    {
        List<Vector2> area = place.property != null ? place.property.HouseWorld() : place.room != null ? place.room.AreaWorld() : null;
        if (area == null || area.Count < 3) return;
        Vector3 pos = p.transform.position;
        float floor = place.property != null ? place.property.transform.position.y : place.room.transform.position.y;
        (float radius, float height) = BodyOf(p);
        Vector3 best = default;
        float bestD = float.MaxValue;
        bool found = false;
        void Consider(Vector3 at)
        {
            if (!SafeSpot(at, radius, height, out Vector3 ground)) return;
            float d = (ground - pos).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = ground;
                found = true;
            }
        }
        Vector2 centre = HousingGeometry.Bounds(area).center;
        List<LockableDoor> doors = place.property != null ? place.property.doors : place.room.doors;
        if (doors != null)
            foreach (LockableDoor d in doors)
            {
                if (d == null || !HousingGate.Owns(place, d)) continue;
                Vector3 door = d.transform.position, f = d.transform.forward, r = d.transform.right;
                Vector2 away = new Vector2(door.x, door.z) - centre;
                foreach (Vector2 dir in new[] { away, new Vector2(f.x, f.z), new Vector2(-f.x, -f.z), new Vector2(r.x, r.z), new Vector2(-r.x, -r.z) })
                {
                    if (dir.sqrMagnitude < 1e-4f) continue;
                    Vector2 n = dir.normalized;
                    Consider(new Vector3(door.x + n.x * 1.5f, Mathf.Max(door.y, floor), door.z + n.y * 1.5f));
                }
            }
        if (!found)
        {
            // No door leads out: just outside the walls, every metre along them.
            float twice = 0f;
            for (int i = 0, j = area.Count - 1; i < area.Count; j = i++) twice += area[j].x * area[i].y - area[i].x * area[j].y;
            float side = twice >= 0f ? 1f : -1f; // the outline turns left (counter-clockwise): outside is on its right
            for (int i = 0, j = area.Count - 1; i < area.Count; j = i++)
            {
                Vector2 a = area[j], ab = area[i] - a;
                float len = ab.magnitude;
                if (len < 1e-3f) continue;
                Vector2 outward = new Vector2(ab.y, -ab.x) / len * side;
                int steps = Mathf.Clamp(Mathf.CeilToInt(len), 1, 64);
                for (int k = 0; k <= steps; k++)
                {
                    Vector2 q = a + ab * (k / (float)steps) + outward * 1.2f;
                    Consider(new Vector3(q.x, floor, q.y));
                }
            }
        }
        if (!found)
        {
            HubWorld.HubWorldMessages.Send(p, "This is not your place: please leave.");
            return;
        }
        HubWorld.RealmTravel.Place(p, new Pose(best, p.transform.rotation));
        HubWorld.HubWorldMessages.Send(p, "This is not your place: you were shown out.");
    }

    /// <summary>
    /// Is there ground under the point (from a metre and a half above it to two and a half below), flat enough to stand
    /// on, with room for a body there, outside every house and room? <paramref name="ground"/>: where the feet go.
    /// </summary>
    private static bool SafeSpot(Vector3 at, float radius, float height, out Vector3 ground)
    {
        ground = at;
        if (!Physics.Raycast(at + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
        if (Vector3.Angle(hit.normal, Vector3.up) > 50f) return false;
        ground = hit.point;
        if (InAnyPlace(ground + Vector3.up * 0.1f)) return false;
        float r = radius * 0.9f;
        Vector3 bottom = ground + Vector3.up * (r + 0.1f), top = ground + Vector3.up * Mathf.Max(r + 0.11f, height - r);
        return !Physics.CheckCapsule(bottom, top, r, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    private static bool InAnyPlace(Vector3 at)
    {
        foreach (Property prop in properties)
            if (prop != null && prop.InHouse(at)) return true;
        foreach (Inn inn in inns)
            if (inn != null)
                foreach (InnRoom r in inn.rooms)
                    if (r != null && r.Contains(at)) return true;
        return false;
    }

    /// <summary>The radius and height of a player's body (its character controller or capsule; else a person's).</summary>
    private static (float radius, float height) BodyOf(CombatEntity p)
    {
        CharacterController cc = p.GetComponentInChildren<CharacterController>();
        if (cc != null)
        {
            Vector3 s = cc.transform.lossyScale;
            return (Mathf.Max(0.1f, cc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z))), Mathf.Max(0.5f, cc.height * Mathf.Abs(s.y)));
        }
        CapsuleCollider cap = p.GetComponentInChildren<CapsuleCollider>();
        if (cap != null)
        {
            Vector3 s = cap.transform.lossyScale;
            return (Mathf.Max(0.1f, cap.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z))), Mathf.Max(0.5f, cap.height * Mathf.Abs(s.y)));
        }
        return (0.35f, 1.8f);
    }

    /// <summary>A player no longer allowed in a place has its chest closed on them.</summary>
    private static void Evict(HousingPlace place, string key)
    {
        if (!place.IsValid) return;
        CombatEntity p = Online(key);
        if (p == null) return;
        HousingAccess a = HousingGate.AccessOf(place, p);
        if ((a & HousingAccess.Storage) != 0) return;
        InventoryManager inv = InventoryManager.For(p);
        if (inv == null) return;
        foreach (HousingChest c in ChestsOf(place))
            if (c != null && c.storage != null && c.storage.User == inv) inv.CloseStorageIf(x => x == c.storage);
    }

    private void OnApplicationQuit()
    {
        // Open chests are closed (their windows written back), then the world is written whole.
        if (book != null && GameNet.NetAuthority.IsServer)
        {
            PutBooks(true);
            WorldStore.Commit();
        }
    }
}
