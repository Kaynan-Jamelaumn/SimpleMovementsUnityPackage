using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Keeps the animals of the open world (made on demand, kept across scenes). Where the rules run (offline, the server)
/// it holds the world's <see cref="AnimalBook"/> - every animal and every pen - and:
/// <list type="bullet">
/// <item><b>lives their lives</b> by the farm clock (<see cref="FarmClock"/>: game hours, saved with the world, never
/// backwards): each pen with its animals at most every <see cref="AnimalRules.tickHours"/>, round robin, within
/// <see cref="AnimalRules.millisPerFrame"/> and <see cref="AnimalRules.stepsPerFrame"/> a frame. A pen nobody is near is
/// simulated like any other - it is only data - so animals live on while their owners are away, and a jump of the clock
/// (sleeping) is lived in steps. Bodies are made only near a player.</item>
/// <item><b>does the players' requests</b>: checks them (<see cref="AnimalCommands.Plan"/>), takes the money (the
/// veterinarian's fee), the items and the water, records them (<see cref="AnimalCommands.Commit"/>), gives everything back
/// if the record could not be made; then does the world's part - lays out a carcass for butchering, wears the tool used,
/// gives what came of the work (manure) and saves the purse. A request done before (a retry, by its token) is answered
/// and never done again.</item>
/// <item><b>keeps the pens' keepers right</b>: an owner who can no longer keep animals in a pen (the lease ended) has them
/// boarded; a pen removed from its loaded scene has its animals boarded.</item>
/// <item><b>saves</b> the world's animals into Worlds/&lt;world&gt;/Animals/animals.json (gzip), whole - aside first, then
/// swapped in, the previous copy kept - every <see cref="AnimalRules.autosaveSeconds"/> while something changed, at once
/// after a trade (a release, a move, remains removed or butchered, a fee paid), and when the world closes. A save that cannot be read is never
/// written over: the animals office stays closed until it is mended.</item>
/// </list>
/// On a network client it only shows: it asks the server for the pictures of the pens near its player and makes their
/// bodies.
/// </summary>
[DefaultExecutionOrder(-45)]
public class AnimalDirector : MonoBehaviour
{
    private static AnimalDirector instance;
    private static readonly List<AnimalPen> pens = new List<AnimalPen>();
    private static readonly Dictionary<string, AnimalPen> pensById = new Dictionary<string, AnimalPen>(StringComparer.Ordinal);
    private static AnimalBook book;
    private static int bookWorld = -1;
    private static string bookFolder;
    private static bool hooked, triedLoad;
    /// <summary>Why the animals office is closed here (empty: open).</summary>
    public static string Closed { get; private set; } = "";

    private readonly Stopwatch watch = new Stopwatch();
    private readonly List<AnimalNotice> notices = new List<AnimalNotice>();
    /// <summary>Scratch: the crates whose wait ran out at the last count (their crater is told).</summary>
    private readonly List<AnimalCrated> prunedCrates = new List<AnimalCrated>();
    private readonly HashSet<string> online = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> missingSince = new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly Dictionary<AnimalPen, (int revision, float at)> shownLocally = new Dictionary<AnimalPen, (int, float)>();
    private readonly HashSet<string> watching = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> askedAt = new Dictionary<string, float>(StringComparer.Ordinal);
    private IAnimalNetRelay watchRelay;
    private AnimalSimContext ctx;
    private Func<bool> outOfTime;
    private float nextPresence, nextReconcile, nextSpecs, nextLocalViews, nextForget, saveTimer, retryAt, lastSaveAt = -100f;
    /// <summary>When the owners near their pens were last looked for (real seconds), and the game hour of it.</summary>
    private float nextTogether;
    private double togetherAt = -1.0;
    private int savedRevision = -1, reconcileCursor;

    public static IReadOnlyList<AnimalPen> Pens => pens;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        pens.Clear();
        pensById.Clear();
        existing.Clear();
        book = null;
        bookWorld = -1;
        bookFolder = null;
        hooked = triedLoad = false;
        Closed = "";
        AnyChanged = null;
        unknownGifts.Clear();
    }

    /// <summary>Where the rules run, something changed in a pen (by id): the network relay and open windows follow.</summary>
    public static event Action<string> AnyChanged;

    public static void Ensure()
    {
        if (instance != null || !Application.isPlaying) return;
        var go = new GameObject("[Animal Director]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AnimalDirector>();
    }

    public static void Register(AnimalPen p)
    {
        if (p == null || pens.Contains(p)) return;
        pens.Add(p);
        if (pensById.TryGetValue(p.PenId, out AnimalPen twin) && twin != null && twin != p)
            Debug.LogError($"Animals: '{p.name}' and '{twin.name}' share the pen id '{p.PenId}' (a duplicated object): the second is ignored until it gets a New Id.", p);
        else pensById[p.PenId] = p;
        Ensure();
        if (instance != null) instance.nextSpecs = 0f;
    }

    public static void Unregister(AnimalPen p)
    {
        pens.Remove(p);
        if (p != null && pensById.TryGetValue(p.PenId, out AnimalPen had) && had == p) pensById.Remove(p.PenId);
        AnimalHousing.Forget(p);
    }

    public static AnimalPen FindPen(string id) => !string.IsNullOrEmpty(id) && pensById.TryGetValue(id, out AnimalPen p) && p != null ? p : null;

    /// <summary>Pens that exist in a loaded scene, active or not (a scene's roots switched off for a while do not make its pens gone).</summary>
    private static readonly HashSet<AnimalPen> existing = new HashSet<AnimalPen>();

    public static void Exists(AnimalPen p)
    {
        if (p != null) existing.Add(p);
    }

    public static void Gone(AnimalPen p)
    {
        existing.Remove(p);
        Unregister(p);
    }

    /// <summary>Does a pen with this id still exist in its scene (perhaps switched off)?</summary>
    private static bool StillExists(string id)
    {
        existing.RemoveWhere(x => x == null);
        foreach (AnimalPen p in existing)
            if (p.PenId == id) return true;
        return false;
    }

    /// <summary>Where the rules run (offline, the server).</summary>
    public static bool RulesHere => !GameNet.NetAuthority.IsNetworked || GameNet.NetAuthority.IsServer;

    /// <summary>The world's animals (null on a network client, outside play, or while the office is closed).</summary>
    public static AnimalBook Book
    {
        get
        {
            if (!RulesHere || !Application.isPlaying) return null;
            Ensure();
            EnsureBook();
            return book;
        }
    }

    public static double Now => FarmClock.Now;

    /// <summary>A player's lasting key (the housing books' key; "local" offline).</summary>
    public static string KeyOf(CombatEntity player) => player != null ? HousingDirector.KeyOf(player) : null;

    /// <summary>Real hours (UTC) - the clock of remembered requests.</summary>
    public static double RealHours => DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerHour;

    // ------------------------------------------------------------------ the frame
    private void Awake()
    {
        outOfTime = () => watch.Elapsed.TotalMilliseconds >= AnimalDatabase.Instance.Rules.millisPerFrame;
        Hook();
        // Pens that exist in the loaded scenes, even under objects switched off since the scene loaded (Unity wakes no
        // component of an inactive object): a pen switched off is not a pen gone.
        for (int i = 0; i < SceneManager.sceneCount; i++) ScanScene(SceneManager.GetSceneAt(i));
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => ScanScene(s);

    private static void ScanScene(Scene s)
    {
        if (!s.IsValid() || !s.isLoaded) return;
        foreach (GameObject root in s.GetRootGameObjects())
            foreach (AnimalPen p in root.GetComponentsInChildren<AnimalPen>(true))
                existing.Add(p);
    }

    // On quit the world's last commit saves the animals (WorldStore.Committing while Closing), while the world's lock is
    // still held: nothing is written after it is let go, so quitting and destroying save nothing of their own.
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
        {
            WaitForWrite();
            instance = null;
        }
    }

    // A phone may end a game in the background without telling it: what changed is written at once, before that (as the
    // world's own commit on pause does).
    private void OnApplicationPause(bool paused)
    {
        if (paused && !WorldStore.Closing) Save(true);
    }

    private void Update()
    {
        // The farm clock runs only while its director does: there may be pens and no garden.
        FarmDirector.Ensure();
        float t = Time.unscaledTime;
        if (RulesHere) RulesFrame(t);
        else ClientFrame(t);
        AnimalBodies.Tick(Time.deltaTime);
    }

    private void RulesFrame(float t)
    {
        EnsureBook();
        if (book == null) return;
        AnimalRules rules = book.Rules;
        double now = Now;
        Context();
        if (t >= nextSpecs)
        {
            nextSpecs = t + 5f;
            SyncPens(now);
        }
        if (t >= nextPresence)
        {
            nextPresence = t + 1f;
            RefreshOnline();
        }
        if (t >= nextReconcile)
        {
            nextReconcile = t + 2f;
            Reconcile(now, t);
        }
        watch.Restart();
        book.Advance(now, ctx, outOfTime, Mathf.Max(1, rules.stepsPerFrame));
        watch.Stop();
        Deliver();
        if (t >= nextTogether)
        {
            nextTogether = t + 2f;
            TimeTogether(now, rules);
        }
        if (t >= nextForget)
        {
            nextForget = t + 60f;
            book.ForgetOldRequests(RealHours);
            // Crated animals nobody let out within the rules' wait are gone (the carrier sold them on): whoever crated them is told.
            prunedCrates.Clear();
            if (book.PruneCrates(RealHours, prunedCrates) > 0)
            {
                book.Urgent = true;
                foreach (AnimalCrated gone in prunedCrates)
                {
                    AnimalSpeciesData sp = gone.animal != null ? AnimalDatabase.Instance.Species(gone.animal.species) : null;
                    if (gone.animal != null && !string.IsNullOrEmpty(gone.by))
                        TellOwner(gone.by, $"{AnimalSim.NameOf(gone.animal, sp)}, the {AnimalSim.Stage(gone.animal.stage).ToLowerInvariant()} you crated, was never let out: the carrier sold it on.");
                }
            }
        }
        // Saving: at once after a trade, else every so often while something changed; a failed save is tried again.
        saveTimer += Time.unscaledDeltaTime;
        CheckWrite(); // a write that failed on the worker is due again
        bool changed = book.Revision != savedRevision;
        // A trade is saved at once - but at most every two seconds, so nobody can make the server write every frame.
        if (changed && t >= retryAt && ((book.Urgent && t - lastSaveAt >= 2f) || saveTimer >= rules.autosaveSeconds)) Save(false);
        // The local player's own pictures (offline, the host): pens near them, refreshed when they change.
        if (t >= nextLocalViews)
        {
            nextLocalViews = t + 0.25f;
            LocalViews(t);
        }
    }

    /// <summary>
    /// Owners near their pens (where the server sees them: within the rules' Near Metres of the fence) spend time with
    /// their animals there: each one's bond grows a little (see <see cref="AnimalSocial.TimeTogether"/>), for the game time
    /// since the last look (at most an hour of it). Only pens up to date are counted.
    /// </summary>
    private void TimeTogether(double now, AnimalRules rules)
    {
        double hours = togetherAt < 0.0 ? 0.0 : now - togetherAt;
        togetherAt = now;
        AnimalSocialRules social = rules.social;
        if (hours <= 0.0 || social == null || !social.enabled) return;
        float h = (float)Math.Min(hours, 1.0);
        IReadOnlyList<CombatEntity> players = CombatEntity.Players;
        if (players == null || players.Count == 0) return;
        bool any = false;
        foreach (AnimalPen pen in pens)
        {
            if (pen == null || !pen.isActiveAndEnabled) continue;
            AnimalHerd herd = book.Herd(pen.PenId);
            if (herd == null || herd.animals.Count == 0 || now - herd.pen.livedTo > 2.0 * Math.Max(0.05, rules.tickHours)) continue;
            foreach (CombatEntity p in players)
            {
                if (p == null || !p.IsAlive || !SameWorld(pen, p)) continue;
                if (pen.DistanceFromOutline(p.transform.position) > social.nearMetres) continue;
                string key = KeyOf(p);
                if (string.IsNullOrEmpty(key)) continue;
                foreach (AnimalRecord a in herd.animals)
                    if (a != null && a.Alive && a.owner == key && AnimalSocial.TimeTogether(a, h, now, rules) > 0f) any = true;
            }
        }
        if (any) book.Touch();
    }

    private void ClientFrame(float t)
    {
        if (t < nextLocalViews) return;
        nextLocalViews = t + 0.5f;
        // A new session (or none yet): nothing is watched until the relay is there to tell the server.
        if (!ReferenceEquals(AnimalNet.Relay, watchRelay))
        {
            watchRelay = AnimalNet.Relay;
            watching.Clear();
            askedAt.Clear();
        }
        if (watchRelay == null) return;
        CombatEntity me = LocalPlayer();
        AnimalRules rules = AnimalDatabase.Instance.Rules;
        foreach (AnimalPen p in pens)
        {
            if (p == null) continue;
            float d = me != null && SameWorld(p, me) ? p.DistanceFromOutline(me.transform.position) : float.MaxValue;
            bool near = d <= rules.showRadius, far = d > rules.hideRadius;
            if (near && watching.Add(p.PenId))
            {
                AnimalNet.Watch(p.PenId, true);
                askedAt[p.PenId] = t;
            }
            else if (near && p.View == null && (!askedAt.TryGetValue(p.PenId, out float at) || t - at > 5f))
            {
                // Watched but no picture came (the server had not seen us near yet): ask again now and then.
                AnimalNet.Watch(p.PenId, true);
                askedAt[p.PenId] = t;
            }
            else if (far && watching.Remove(p.PenId))
            {
                askedAt.Remove(p.PenId);
                AnimalNet.Watch(p.PenId, false);
                if (!AnimalPenWindow.IsShowing(p)) p.ForgetView();
            }
        }
    }

    private static bool SameWorld(AnimalPen p, CombatEntity e)
    {
        Scene a = p.gameObject.scene, b = e.gameObject.scene;
        return a == b || !MobRealms.AreScenesSeparated(a, b);
    }

    /// <summary>The local player of this machine (null on a dedicated server).</summary>
    public static CombatEntity LocalPlayer()
    {
        IReadOnlyList<CombatEntity> list = CombatEntity.Players;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && CardTable.IsLocalPlayer(list[i])) return list[i];
        return null;
    }

    private void Context()
    {
        ctx ??= new AnimalSimContext { notices = notices };
        ctx.catalog = AnimalDatabase.Instance;
        ctx.climate ??= new CityClimateSource(AgricultureDatabase.Instance.Settings);
        ctx.ownerPresent ??= (key, time) => online.Contains(key) || !GameNet.NetAuthority.IsNetworked;
        // Without a veterinarian in the world a keeper's medicine can cure everything (nobody is told to send for one).
        ctx.vetAvailable = AnimalVeterinarian.Current != null;
        // A crate's wait is counted in real days (Phase 5).
        ctx.realHours = RealHours;
    }

    /// <summary>The simulation's surroundings (where the rules run).</summary>
    public static AnimalSimContext SimContext
    {
        get
        {
            Ensure();
            if (instance == null) return null;
            instance.Context();
            return instance.ctx;
        }
    }

    // ------------------------------------------------------------------ pens and players
    private void SyncPens(double now)
    {
        foreach (AnimalPen p in pens)
        {
            if (p == null || !p.isActiveAndEnabled || FindPen(p.PenId) != p) continue;
            book.EnsurePen(p.Spec(), p.gameObject.scene.name, AnimalHousing.PropertyIdOf(p), now);
        }
    }

    private void RefreshOnline()
    {
        online.Clear();
        IReadOnlyList<CombatEntity> list = CombatEntity.Players;
        for (int i = 0; i < list.Count; i++)
        {
            CombatEntity e = list[i];
            if (e == null || e.Kind != CombatEntity.EntityKind.Player) continue;
            string key = KeyOf(e);
            if (!string.IsNullOrEmpty(key)) online.Add(key);
        }
    }

    public static bool IsOnline(string key) => instance != null && !string.IsNullOrEmpty(key) && (instance.online.Contains(key) || !GameNet.NetAuthority.IsNetworked);

    /// <summary>
    /// A few pens a look: animals whose owner may no longer keep them there are boarded (the housing books decide, by key);
    /// the animals of a pen gone from its loaded scene for a while are boarded too.
    /// </summary>
    private void Reconcile(double now, float t)
    {
        if (!book.Rules.boardWhenUnreachable) return;
        IReadOnlyList<AnimalHerd> herds = book.Herds;
        if (herds.Count == 0) return;
        for (int n = 0; n < 8 && n < herds.Count; n++)
        {
            if (reconcileCursor >= herds.Count) reconcileCursor = 0;
            AnimalHerd h = herds[reconcileCursor++];
            if (h == null || h.animals.Count == 0) continue;
            string reason = null;
            AnimalPen p = FindPen(h.pen.id);
            if (p == null && !StillExists(h.pen.id))
            {
                Scene s = string.IsNullOrEmpty(h.pen.scene) ? default : SceneManager.GetSceneByName(h.pen.scene);
                if (s.IsValid() && s.isLoaded)
                {
                    if (!missingSince.TryGetValue(h.pen.id, out float since)) missingSince[h.pen.id] = t;
                    else if (t - since > 15f) reason = "its pen is gone";
                }
            }
            else missingSince.Remove(h.pen.id);
            for (int i = h.animals.Count - 1; i >= 0; i--)
            {
                AnimalRecord a = h.animals[i];
                if (a == null || !a.Alive) continue;
                string why = reason;
                if (why == null && AnimalHousing.TryRightsOfKey(h.pen.property, a.owner, out AnimalRights r) && (r & AnimalRights.Keep) == 0)
                    why = "its owner no longer keeps that pen";
                if (why != null) AnimalCommands.Board(book, a, now, ctx, why);
            }
        }
    }

    /// <summary>Tells owners what happened to their animals (those online), and lets the pens and the network know.</summary>
    private void Deliver()
    {
        if (notices.Count == 0) return;
        var changed = new HashSet<string>();
        foreach (AnimalNotice n in notices)
        {
            if (!string.IsNullOrEmpty(n.pen)) changed.Add(n.pen);
            switch (n.kind)
            {
                case AnimalEventKind.Starving:
                case AnimalEventKind.Thirsty:
                case AnimalEventKind.Poisoned:
                case AnimalEventKind.Died:
                case AnimalEventKind.StageReached:
                case AnimalEventKind.Boarded:
                // Phase 2: an illness shows (a symptom, not its name), reaches its worst, is over; an animal in a bad way;
                // the veterinarian saw it (sent for by someone else).
                case AnimalEventKind.FellIll:
                case AnimalEventKind.Worsened:
                case AnimalEventKind.Healed:
                case AnimalEventKind.NeedsHelp:
                case AnimalEventKind.SeenByVet:
                // Phase 3: a full nest, udder or fleece; someone else collected. Phase 4: a birth, a loss, a companion missed.
                case AnimalEventKind.ProductsReady:
                case AnimalEventKind.Collected:
                case AnimalEventKind.GaveBirth:
                case AnimalEventKind.Miscarried:
                case AnimalEventKind.MissesCompanion:
                    TellOwner(n.owner, n.text);
                    break;
            }
        }
        notices.Clear();
        foreach (string id in changed) AnyChanged?.Invoke(id);
    }

    private static void TellOwner(string key, string text)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(text)) return;
        IReadOnlyList<CombatEntity> list = CombatEntity.Players;
        for (int i = 0; i < list.Count; i++)
        {
            CombatEntity e = list[i];
            if (e == null || KeyOf(e) != key) continue;
            if (CardTable.IsLocalPlayer(e)) InventoryManager.For(e)?.ShowMessage(text, InventoryFeedback.Kind.Info);
            else AnimalNet.Tell(e, text);
        }
    }

    private void LocalViews(float t)
    {
        CombatEntity me = LocalPlayer();
        if (me == null) return;
        AnimalRules rules = book.Rules;
        foreach (AnimalPen p in pens)
        {
            if (p == null || !p.isActiveAndEnabled || FindPen(p.PenId) != p) continue; // a duplicated pen is out of order
            float d = SameWorld(p, me) ? p.DistanceFromOutline(me.transform.position) : float.MaxValue;
            if (d > rules.hideRadius && !AnimalPenWindow.IsShowing(p))
            {
                if (shownLocally.Remove(p)) p.ForgetView();
                continue;
            }
            if (d > rules.showRadius && !shownLocally.ContainsKey(p) && !AnimalPenWindow.IsShowing(p)) continue;
            AnimalHerd h = book.Herd(p.PenId);
            int revision = h != null ? h.pen.revision : -1;
            if (shownLocally.TryGetValue(p, out (int revision, float at) shown) && shown.revision == revision && t - shown.at < 5f) continue;
            p.ReceiveView(ViewFor(p, me));
            // The revision after the picture was made (making it may have registered the pen).
            h = book.Herd(p.PenId);
            shownLocally[p] = (h != null ? h.pen.revision : -1, t);
        }
    }

    /// <summary>
    /// The picture of a pen for a player (where the rules run): what they see, and what they may do - with the
    /// veterinarian's fee for the animals they may send for one (when there is a vet) and the weather now.
    /// </summary>
    public static AnimalPenView ViewFor(AnimalPen pen, CombatEntity player)
    {
        AnimalBook b = Book;
        if (b == null || pen == null || FindPen(pen.PenId) != pen) return null; // a pen sharing another's id is out of order
        // The picture shows the pen as last lived (within a tick): looking never steps the simulation, so a pen watched
        // closely lives the same hours as one nobody watches.
        double now = Now;
        AnimalHerd h = b.Herd(pen.PenId) ?? b.EnsurePen(pen.Spec(), pen.gameObject.scene.name, AnimalHousing.PropertyIdOf(pen), now);
        AnimalVeterinarian vet = AnimalVeterinarian.Current;
        Func<AnimalRecord, AnimalSpeciesData, long> fee = null;
        if (vet != null) fee = vet.FeeFor;
        string weather = AnimalObservation.WeatherWord(SimContext, now);
        return AnimalObservation.PenView(b, h, KeyOf(player), AnimalHousing.RightsOf(pen, player), now, fee, vet != null, weather);
    }

    /// <summary>The revision of a pen's picture (where the rules run; -1 unknown): it moves when anything in it changed.</summary>
    public static int RevisionOf(string penId)
    {
        AnimalHerd h = book?.Herd(penId);
        return h != null ? h.pen.revision : -1;
    }

    // ------------------------------------------------------------------ requests
    /// <summary>Item ids the commands gave that no item answers to (warned about once each).</summary>
    private static readonly HashSet<string> unknownGifts = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Does a player's request where the rules run (the pen has checked reach and its access hook): plan; take the money
    /// (the veterinarian's fee), then the items (each copy's quality read first) and the water; commit - all or nothing:
    /// a commit that fails or throws gives everything back, copy by copy, and leaves the records as they were. Then what
    /// only the world can do: lay out a carcass for butchering (its record goes only once it lies there), wear the tool,
    /// give what came of it (manure), and save the purse. Remembered so a retry is answered, never done twice.
    /// </summary>
    public static AnimalResult Perform(AnimalPen pen, AnimalRequest request, CombatEntity player)
    {
        AnimalBook b = Book;
        if (b == null) return AnimalResult.Fail(string.IsNullOrEmpty(Closed) ? "The animals are not kept here." : Closed);
        if (FindPen(pen.PenId) != pen) return AnimalResult.Fail("This pen is out of order (it shares its id with another).");
        string key = KeyOf(player);
        if (string.IsNullOrEmpty(key)) return AnimalResult.Fail("Your character is not known here yet.");
        if (b.WasDone(key, request.token, out AnimalDoneRequest was))
            return new AnimalResult { ok = was.ok, message = was.message, animal = was.animal, repeated = true };
        double now = Now;
        AnimalSimContext c = SimContext;
        b.EnsurePen(pen.Spec(), pen.gameObject.scene.name, AnimalHousing.PropertyIdOf(pen), now);
        AnimalActor actor = AnimalToolkit.ActorFor(player, AnimalHousing.RightsOf(pen, player));
        actor.key = key;
        AnimalPlan plan = AnimalCommands.Plan(b, request, actor, now, c);
        if (plan.Refused) return plan.refusal;
        if (plan.answer != null) return plan.answer;
        // A crate is a livestock item the Item Database must know, and it goes into the player's bag (never onto the ground,
        // where anyone could take the animal in it).
        if (request.kind == AnimalActionKind.Crate)
        {
            string noCrate = CrateRefusal(b, request.animal, InventoryManager.For(player));
            if (noCrate != null) return Answer(AnimalResult.Fail(noCrate), request);
        }

        // The money first: nothing else is taken when it cannot be paid.
        InventoryManager inv = InventoryManager.For(player);
        CurrencyWallet wallet = null;
        CurrencyDefinition money = null;
        long paid = 0L;
        if (plan.fee > 0L)
        {
            AnimalVeterinarian vet = AnimalVeterinarian.Current;
            money = vet != null ? vet.Currency : null;
            if (!AnimalToolkit.TakeFee(player, plan.fee, money, out wallet, out string why))
                return Answer(AnimalResult.Fail(string.IsNullOrEmpty(why) ? "You cannot pay." : why), request);
            paid = plan.fee;
        }

        // Then what the plan needs: the items (their qualities read first, each copy's item kept to give it back as it
        // was) and the water.
        AnimalTaken taken = default;
        List<ItemSO> takenItems = null;
        InventoryItem container = null;
        float poured = 0f;
        // (A crate chosen by its code, Phase 5: that copy and no other.)
        int chosen = request.kind == AnimalActionKind.Release ? request.code : 0;
        if (!string.IsNullOrEmpty(plan.takeItem) && plan.takeCount > 0) taken = AnimalToolkit.Take(inv, plan.takeItem, plan.takeCount, out _, out takenItems, chosen);
        if (plan.takeWater > 0f)
        {
            container = AnimalToolkit.WaterOf(inv, out _, out WaterSourceKind kind);
            poured = AnimalToolkit.Pour(inv, container, plan.takeWater);
            taken.litres = poured;
            taken.waterKind = (int)kind;
        }
        // (After the take: it makes the whole struct anew.)
        taken.paid = paid;

        // What the commit may change, kept aside: a commit that fails leaves the world as it was. A request for the whole
        // pen (mucking out, care or medicine for every animal) may change every animal in it.
        bool wholePen = request.kind == AnimalActionKind.CleanPen || request.kind == AnimalActionKind.Breed
                        || ((request.kind == AnimalActionKind.Care || request.kind == AnimalActionKind.Treat || request.kind == AnimalActionKind.Collect)
                            && string.IsNullOrEmpty(request.animal));
        AnimalRecord animal = b.Find(request.animal);
        AnimalPenRecord penRecord = b.Pen(pen.PenId);
        var kept = new List<(AnimalRecord rec, string json)>();
        if (animal != null) kept.Add((animal, JsonUtility.ToJson(animal)));
        if (wholePen)
        {
            AnimalHerd herd = b.Herd(pen.PenId);
            if (herd != null)
                foreach (AnimalRecord m in herd.animals)
                    if (m != null && !ReferenceEquals(m, animal)) kept.Add((m, JsonUtility.ToJson(m)));
        }
        string penBefore = penRecord != null ? JsonUtility.ToJson(penRecord) : null;
        // A crate being let out: the animal it holds, as it is (a failed commit puts it back in its crate).
        AnimalCrated crateBefore = null;
        if (request.kind == AnimalActionKind.Release && AnimalItemCode.TryCrated(taken.Code(0), out _, out _, out _, out long crateSerial))
        {
            crateBefore = b.Crated(crateSerial);
            if (crateBefore?.animal != null) kept.Add((crateBefore.animal, JsonUtility.ToJson(crateBefore.animal)));
        }
        AnimalResult result;
        try
        {
            result = AnimalCommands.Commit(b, plan, taken, now, c);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            foreach ((AnimalRecord rec, string json) in kept) JsonUtility.FromJsonOverwrite(json, rec);
            if (penRecord != null) JsonUtility.FromJsonOverwrite(penBefore, penRecord);
            // The book's side too: an animal crated by the failed commit lives in its pen again, one let out goes back in its crate.
            try
            {
                if (request.kind == AnimalActionKind.Crate && animal != null) b.UndoCrate(animal, pen.PenId);
                if (crateBefore != null) b.UndoUncrate(crateBefore);
            }
            catch (Exception undo)
            {
                Debug.LogException(undo);
            }
            result = Answer(AnimalResult.Fail("Something went wrong: nothing was done."), request);
        }
        if (!result.ok)
        {
            GiveBack(player, inv, takenItems, taken, container, poured, wallet, money, paid);
            return result;
        }

        // ---------------------------------------------------------------- after the commit (the world's part)
        // 1 Butchering: the carcass is laid out first; the record goes only once it lies there (nothing to undo otherwise).
        if (plan.remains != null)
        {
            string why = null;
            bool laid;
            try
            {
                laid = AnimalCarcass.LayOut(plan.remains, pen, player, out why);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                laid = false;
                why = null;
            }
            if (!laid)
            {
                GiveBack(player, inv, takenItems, taken, container, poured, wallet, money, paid);
                return Answer(AnimalResult.Fail(string.IsNullOrEmpty(why) ? "The carcass could not be laid out: nothing was done." : why), request);
            }
            try
            {
                AnimalRecord dead = animal ?? b.Find(plan.remains.animal);
                if (dead != null && !dead.Alive && b.Remove(dead))
                {
                    AnimalHerd here = b.Herd(pen.PenId);
                    if (here != null) here.pen.revision++;
                }
                b.Urgent = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        // 2 The tool is worn by the work done (a pen's care: the animals really done, which the commit set).
        try
        {
            if (!string.IsNullOrEmpty(plan.wearKey) && plan.wearUses > 0) AnimalToolkit.Wear(inv, plan.wearKey, plan.wearUses);
            if (plan.moreWear != null)
                foreach (AnimalItemChange w in plan.moreWear)
                    if (!string.IsNullOrEmpty(w.item) && w.count > 0) AnimalToolkit.Wear(inv, w.item, w.count);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        // 3 What came of it (the manure forked up, the eggs collected with their codes, a crate), as the Item Database knows it.
        bool crateGiven = request.kind != AnimalActionKind.Crate;
        try
        {
            if (plan.give != null)
                foreach (AnimalItemChange g in plan.give)
                {
                    if (string.IsNullOrEmpty(g.item) || g.count <= 0) continue;
                    ItemSO item = ItemDatabase.Item(g.item);
                    if (item == null)
                    {
                        if (unknownGifts.Add(g.item)) Debug.LogWarning($"Animals: no item answers to '{g.item}' (the rules give it): it is not given.");
                        continue;
                    }
                    AnimalToolkit.Give(player, item, g.count, g.codes);
                    if (request.kind == AnimalActionKind.Crate) crateGiven = true;
                }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        // A crate that could not be given: its animal is not left in a crate nobody holds - it lives in its pen again.
        if (!crateGiven)
        {
            try
            {
                if (animal != null && b.UndoCrate(animal, pen.PenId)) b.Urgent = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            result = AnimalResult.Fail("The crate could not be given to you: the animal stays in the pen.");
            b.RememberDone(key, request.token, request.seq, RealHours, false, result.message, "");
            if (instance != null) instance.Deliver();
            AnyChanged?.Invoke(pen.PenId);
            return Answer(result, request);
        }
        // 4 Money changed hands: the purse is saved at once, the book soon (a crash between them only loses the payment).
        try
        {
            if (paid > 0L)
            {
                if (wallet != null) WalletSaves.SaveNow(wallet);
                b.Urgent = true;
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        b.RememberDone(key, request.token, request.seq, RealHours, true, result.message, result.animal);
        if (instance != null) instance.Deliver();
        AnyChanged?.Invoke(pen.PenId);
        return result;
    }

    /// <summary>Why an animal cannot be crated into the player's bag (null: it can): no livestock item of its breed, or no room for it.</summary>
    private static string CrateRefusal(AnimalBook b, string animal, InventoryManager inv)
    {
        AnimalRecord a = b?.Find(animal);
        AnimalSpeciesData sp = a != null ? AnimalDatabase.Instance.Species(a.species) : null;
        if (sp == null) return "It cannot be crated now.";
        AnimalBreedData breed = sp.BreedOrDefault(a.breed);
        try
        {
            ItemSO crate = ItemDatabase.Item(AnimalIds.Livestock(sp.id, breed != null ? breed.id : ""));
            if (crate == null) return "There is no crate for it (no livestock item of its breed).";
            if (inv == null || !inv.HasEnoughSpace(crate, 1)) return "You have no room in your bag for its crate.";
            return null;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return "It cannot be crated now.";
        }
    }

    /// <summary>Everything a request took, back to the player: the items copy by copy, the water, the money.</summary>
    private static void GiveBack(CombatEntity player, InventoryManager inv, List<ItemSO> takenItems, AnimalTaken taken, InventoryItem container, float poured,
        CurrencyWallet wallet, CurrencyDefinition money, long paid)
    {
        try
        {
            AnimalToolkit.GiveBack(player, inv, takenItems, taken);
            if (poured > 0f) AnimalToolkit.Refill(inv, container, poured);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        if (paid > 0L) AnimalToolkit.GiveBackFee(wallet, money, paid);
    }

    /// <summary>An answer to a request (with its number).</summary>
    private static AnimalResult Answer(AnimalResult r, AnimalRequest request)
    {
        r.seq = request.seq;
        return r;
    }

    // ------------------------------------------------------------------ the world and saving
    private static void Hook()
    {
        if (hooked) return;
        hooked = true;
        WorldStore.Committing += OnWorldCommitting;
        WorldStore.WorldChanged += OnWorldChanged;
    }

    private static void OnWorldCommitting()
    {
        if (WorldStore.Closing && instance != null) instance.Save(true);
    }

    /// <summary>Another world opened: the old one's animals were saved as it closed; the new one's are read when next needed.</summary>
    private static void OnWorldChanged()
    {
        book = null;
        bookWorld = -1;
        bookFolder = null;
        triedLoad = false;
        Closed = "";
        foreach (AnimalPen p in pens) if (p != null) p.ForgetView();
        if (instance != null)
        {
            instance.savedRevision = -1;
            instance.shownLocally.Clear();
            instance.missingSince.Clear();
        }
    }

    /// <summary>The open world's animals folder (null where nothing is kept: a network client, a world open in another window).</summary>
    public static string SaveFolder => FarmClock.SaveFolder != null ? Path.Combine(WorldStore.Folder, "Animals") : null;

    public static string SavePath => SaveFolder != null ? Path.Combine(SaveFolder, "animals.json") : null;

    private static void EnsureBook()
    {
        if (book != null && bookWorld == FarmClock.World) return;
        if (book != null && bookWorld != FarmClock.World) OnWorldChanged();
        if (triedLoad) return;
        triedLoad = true;
        AnimalDatabase db = AnimalDatabase.Instance;
        bookWorld = FarmClock.World;
        bookFolder = db.Rules.saveAnimals ? SaveFolder : null;
        var fresh = new AnimalBook(db, Application.isPlaying ? WorldStore.WorldId : "");
        string path = bookFolder != null ? Path.Combine(bookFolder, "animals.json") : null;
        if (path == null)
        {
            book = fresh;
            return;
        }
        // The newest whole copy: the main one, or a write that was not swapped in when it is newer, else the backup.
        string[] copies = FarmClock.CopiesOf(path); // main, .tmp, .bak
        var read = new AnimalWorldSave[copies.Length];
        var errors = new string[copies.Length];
        bool anyCopy = false;
        for (int i = 0; i < copies.Length; i++)
        {
            if (!File.Exists(copies[i])) continue;
            anyCopy = true;
            try
            {
                read[i] = JsonUtility.FromJson<AnimalWorldSave>(Decode(File.ReadAllBytes(copies[i])));
                if (read[i] == null) throw new InvalidDataException("empty");
            }
            catch (Exception e)
            {
                read[i] = null;
                errors[i] = e.Message;
                Debug.LogWarning($"Animals: could not read '{Path.GetFileName(copies[i])}' ({e.Message}).");
            }
        }
        if (!anyCopy)
        {
            book = fresh;
            return;
        }
        // A copy written by a newer version of the game is never read half, rolled back from, nor set aside.
        for (int i = 0; i < read.Length; i++)
            if (read[i] != null && read[i].version > AnimalWorldSave.CurrentVersion)
            {
                Closed = "The animals office is closed: the world's animals were saved by a newer version of the game.";
                Debug.LogError($"Animals: '{Path.GetFileName(copies[i])}' was saved by a newer version ({read[i].version}); nothing is read or written until that version runs.");
                book = null;
                return;
            }
        int chosen = -1;
        if (read[0] != null) chosen = 0;
        if (read[1] != null && (chosen < 0 || read[1].savedUtc > read[chosen].savedUtc)) chosen = 1;
        if (chosen < 0 && read[2] != null) chosen = 2;
        if (chosen < 0)
        {
            // No copy can be read: nothing is written over it, and nothing is done until it is mended.
            Closed = "The animals office is closed: the world's animals cannot be read.";
            Debug.LogError($"Animals: no copy of '{path}' can be read ({errors[0] ?? errors[1] ?? errors[2]}). It is left as it is; nothing is saved over it.");
            book = null;
            return;
        }
        try
        {
            fresh.Import(read[chosen]);
        }
        catch (Exception e)
        {
            Closed = "The animals office is closed: the world's animals cannot be read.";
            Debug.LogError($"Animals: '{Path.GetFileName(copies[chosen])}' could not be taken in ({e.Message}). Nothing is saved over it.");
            book = null;
            return;
        }
        if (File.Exists(copies[0]) && read[0] == null)
        {
            // The main copy is unreadable and an older copy was used: the bad one is set aside first (never thrown away). If it
            // cannot be, the office stays closed rather than ever writing over it.
            Debug.LogWarning($"Animals: the world's animals were read from '{Path.GetFileName(copies[chosen])}' ({errors[0]}).");
            if (!SetAside(path))
            {
                Closed = "The animals office is closed: an unreadable save could not be set aside.";
                book = null;
                return;
            }
        }
        else if (chosen == 1) Debug.LogWarning("Animals: the last save had not been swapped in; it is used (it is newer).");
        book = fresh;
    }

    /// <summary>The unreadable main copy is kept aside (never thrown away), so the backup can be written in its place. False when it could not be.</summary>
    private static bool SetAside(string path)
    {
        try
        {
            if (File.Exists(path)) File.Move(path, path + ".unreadable-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            return !File.Exists(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Animals: could not set the unreadable save aside ({e.Message}).");
            return false;
        }
    }

    private static string Decode(byte[] data)
    {
        if (data.Length >= 2 && data[0] == 0x1f && data[1] == 0x8b)
        {
            using (var input = new MemoryStream(data))
            using (var gz = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gz.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }
        return Encoding.UTF8.GetString(data);
    }

    private static byte[] Encode(string json)
    {
        byte[] raw = Encoding.UTF8.GetBytes(json);
        using (var output = new MemoryStream())
        {
            using (var gz = new GZipStream(output, System.IO.Compression.CompressionLevel.Fastest, true)) gz.Write(raw, 0, raw.Length);
            return output.ToArray();
        }
    }

    /// <summary>Saves now (where the rules run: only into the world the animals were read from).</summary>
    public static void SaveNow() => instance?.Save(false);

    private System.Threading.Tasks.Task writing;
#if UNITY_WEBGL && !UNITY_EDITOR
    private const bool Threaded = false; // no worker threads in a browser: every save is written at once
#else
    private const bool Threaded = true;
#endif
    private volatile string writeError;
    private int writingRevision = -1;

    /// <summary>
    /// The book is written down here (on the main thread, as it is now); packing and writing the file happen on a worker,
    /// one write at a time (the next one waits for it). A world that is closing is written at once, after any write in
    /// flight, before the world's lock is let go.
    /// </summary>
    private bool Save(bool now)
    {
        if (book == null || bookFolder == null || bookWorld != FarmClock.World || !Application.isPlaying) return false;
        CheckWrite();
        if (writing != null && !writing.IsCompleted)
        {
            if (!now) return false; // still writing the last one: this one goes next frame
            WaitForWrite();
        }
        saveTimer = 0f;
        lastSaveAt = Time.unscaledTime;
        string json;
        try
        {
            json = JsonUtility.ToJson(book.Export(Now, DateTime.UtcNow.Ticks));
        }
        catch (Exception e)
        {
            retryAt = Time.unscaledTime + 30f;
            Debug.LogWarning($"Animals: could not write down the world's animals ({e.Message}); trying again soon.");
            return false;
        }
        string file = Path.Combine(bookFolder, "animals.json");
        int revision = book.Revision;
        savedRevision = revision;
        writingRevision = revision;
        book.Urgent = false;
        if (now || !Threaded)
        {
            try
            {
                FarmClock.WriteWhole(file, Encode(json));
                return true;
            }
            catch (Exception e)
            {
                savedRevision = -1;
                retryAt = Time.unscaledTime + 30f;
                Debug.LogWarning($"Animals: could not save the world's animals ({e.Message}); trying again soon.");
                return false;
            }
        }
        writing = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                FarmClock.WriteWhole(file, Encode(json));
            }
            catch (Exception e)
            {
                writeError = e.Message;
            }
        });
        return true;
    }

    /// <summary>A write on the worker failed: it is tried again (the book is still as it was, or newer).</summary>
    private void CheckWrite()
    {
        string error = writeError;
        if (error == null) return;
        writeError = null;
        if (savedRevision == writingRevision) savedRevision = -1;
        retryAt = Time.unscaledTime + 30f;
        Debug.LogWarning($"Animals: could not save the world's animals ({error}); trying again soon.");
    }

    private void WaitForWrite()
    {
        try
        {
            writing?.Wait(10000);
        }
        catch (Exception)
        {
            // its error was kept in writeError
        }
        CheckWrite();
    }
}
