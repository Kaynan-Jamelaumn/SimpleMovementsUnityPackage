using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps players' money between sessions: the balances of every number currency with Keep Between Sessions, where the
/// rules run (offline, the server), per player key (<see cref="PlayerKeys"/>), in the world's store
/// (<see cref="WorldStore"/>: one file with the housing books, written whole - money can never be half moved). A wallet's
/// kept balances come back as soon as its player is known (the character arrived); until then it refuses to spend or
/// add, so a fresh session never overwrites what was kept and nothing spent meanwhile comes back with it. A kept currency
/// that is not loaded at that moment (only a later scene uses it) comes back as soon as it is - when a scene loads, or the
/// first time the wallet holds it - so the owner's client is told of it too; until then its kept amount stays as it was.
/// <para>Coin-item currencies are items in the inventory and are not kept here. A client keeps nothing: the server's
/// wallets are the truth. Each world keeps its own: money of single player never reaches the online city.</para>
/// </summary>
public static class WalletSaves
{
    [Serializable]
    private class Entry
    {
        public string currency = "";
        public int amount;
    }

    [Serializable]
    private class Record
    {
        public string key = "";
        public string name = "";
        public List<Entry> balances = new List<Entry>();
    }

    [Serializable]
    private class Part
    {
        public List<Record> wallets = new List<Record>();
    }

    private sealed class Tracked
    {
        public string key;
        public bool restored;
        public float giveUpAt;
        /// <summary>
        /// What each kept currency held when the wait for the kept balances began (the wallet was tracked, or the world
        /// changed): what it gains or spends before they are back (after the wait gave up, or offline) is added to them.
        /// </summary>
        public Dictionary<CurrencyDefinition, int> baseline;
        /// <summary>The world changed: a player the new world does not know starts with the currencies' starting amounts.</summary>
        public bool fresh;
        /// <summary>
        /// The kept currencies the restore could not give back: not loaded here then, so the wallet could not hold them.
        /// Each is given back once, when the wallet first holds it (<see cref="Added"/>); until then its kept amount stays
        /// in the record as it was, even after it loads.
        /// </summary>
        public HashSet<string> pending;
    }

    private const string Section = "wallets";

    /// <summary>A switch for the whole game (tests, a game with its own saves): off = balances are never kept or restored.</summary>
    public static bool Enabled = true;

    /// <summary>
    /// Real seconds a networked wallet waits for its player's key before it gives up: then it may spend what it holds (kept
    /// only if the key still comes: what was spent or got meanwhile is then added to the kept amounts).
    /// </summary>
    public const float RestoreTimeout = 20f;

    private static readonly Dictionary<CurrencyWallet, Tracked> tracked = new Dictionary<CurrencyWallet, Tracked>();
    private static readonly List<CurrencyWallet> scratch = new List<CurrencyWallet>();
    private static Dictionary<string, Record> records;
    private static Runner runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Enabled = true;
        tracked.Clear();
        records = null;
        runner = null;
    }

    /// <summary>A wallet started (called by <see cref="CurrencyWallet"/>): its balances come back once its player is known.</summary>
    internal static void Track(CurrencyWallet wallet)
    {
        if (!Enabled || !Application.isPlaying || wallet == null || wallet.IsReplica) return;
        if (tracked.ContainsKey(wallet)) return;
        if (runner == null)
        {
            var go = new GameObject("[Wallet Saves]") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
            CurrencyWallet.AnyBalanceChanged += OnBalanceChanged;
            WorldStore.Committing += Write;
            WorldStore.WorldChanged += OnWorldChanged;
        }
        var t = new Tracked { giveUpAt = Time.unscaledTime + RestoreTimeout, baseline = Snapshot(wallet) };
        tracked[wallet] = t;
        TryRestore(wallet, t);
        // On a server the character (and so the key) arrives a moment after the player: until the kept balances are back,
        // nothing is spent or added (or what was spent would come back with them).
        if (!t.restored && GameNet.NetAuthority.IsNetworked) wallet.awaitingRestore = true;
    }

    /// <summary>A wallet goes (its player left): what it holds is kept.</summary>
    internal static void Untrack(CurrencyWallet wallet)
    {
        if (wallet == null || !tracked.TryGetValue(wallet, out Tracked t)) return;
        if (t.restored) Keep(wallet, t);
        tracked.Remove(wallet);
    }

    /// <summary>
    /// A wallet holds a currency for the first time (called by <see cref="CurrencyWallet"/>, right after it made the entry
    /// at the starting amount). A kept currency that was not loaded when the wallet's balances came back gets its kept
    /// amount now - once: from now on the wallet holds it, and what it holds is kept.
    /// </summary>
    internal static void Added(CurrencyWallet wallet, CurrencyDefinition currency)
    {
        if (wallet == null || currency == null || !tracked.TryGetValue(wallet, out Tracked t)) return;
        // Only what this world's restore left waiting, and only once (taken out before the balance is set).
        if (!t.restored || t.pending == null || !t.pending.Remove(currency.NetworkKey)) return;
        Entry kept = KeptEntry(t.key, currency.NetworkKey);
        if (kept == null || !currency.KeepBetweenSessions) return;
        // The restore's rule: the kept amount, plus what the wallet gained or spent of it since it held its starting
        // amount (nothing: the entry is new).
        long amount = (long)kept.amount + wallet.GetBalance(currency) - currency.StartingAmount;
        wallet.SetBalance(currency, (int)Math.Max(0L, Math.Min(amount, currency.MaxAmount)));
    }

    /// <summary>Keeps a wallet's balances now (money moved between it and the books: written with them, in the same file).</summary>
    public static void SaveNow(CurrencyWallet wallet)
    {
        if (wallet != null && tracked.TryGetValue(wallet, out Tracked t) && t.restored) Keep(wallet, t);
    }

    /// <summary>Keeps every known wallet's balances (they are written with the world).</summary>
    public static void SaveAll()
    {
        foreach (KeyValuePair<CurrencyWallet, Tracked> kv in tracked)
            if (kv.Key != null && kv.Value.restored) Keep(kv.Key, kv.Value);
    }

    /// <summary>Are this wallet's balances kept (its player has a key and they were restored)?</summary>
    public static bool IsKept(CurrencyWallet wallet) => wallet != null && tracked.TryGetValue(wallet, out Tracked t) && t.restored;

    /// <summary>
    /// Are this wallet's kept balances still to come back (tracked, not restored yet - tried again now)? Money moved in or
    /// out of it meanwhile would not be saved with what it paid for: the housing office waits until this is false.
    /// </summary>
    public static bool IsRestoring(CurrencyWallet wallet)
    {
        if (wallet == null || !tracked.TryGetValue(wallet, out Tracked t) || t.restored) return false;
        TryRestore(wallet, t);
        return !t.restored;
    }

    private static void OnBalanceChanged(CurrencyWallet wallet, CurrencyDefinition currency, int old, int now)
    {
        if (wallet == null || currency == null || !currency.KeepBetweenSessions) return;
        if (tracked.TryGetValue(wallet, out Tracked t) && t.restored) Keep(wallet, t);
    }

    // ---------------------------------------------------------------- the world's part
    private static Dictionary<string, Record> Records()
    {
        if (records != null) return records;
        if (!WorldStore.IsOpen) return null;
        records = new Dictionary<string, Record>();
        string json = WorldStore.Get(Section);
        if (!string.IsNullOrEmpty(json))
            try
            {
                Part part = JsonUtility.FromJson<Part>(json);
                if (part?.wallets != null)
                    foreach (Record r in part.wallets)
                        if (r != null && !string.IsNullOrEmpty(r.key)) records[r.key] = r;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Currency] The kept wallets could not be read ({e.Message}).");
            }
        return records;
    }

    /// <summary>The world is being written: the wallets' part goes in.</summary>
    private static void Write()
    {
        if (records == null) return;
        SaveAll();
        var part = new Part();
        part.wallets.AddRange(records.Values);
        WorldStore.Put(Section, JsonUtility.ToJson(part));
    }

    private static void OnWorldChanged()
    {
        // Another world: what was read belongs to the old one. Every wallet is restored again from the new world.
        records = null;
        foreach (KeyValuePair<CurrencyWallet, Tracked> kv in tracked)
        {
            kv.Value.restored = false;
            kv.Value.key = null;
            kv.Value.giveUpAt = Time.unscaledTime + RestoreTimeout;
            // What the wallet holds now is the old world's: the new one's comes back (or its starting amounts).
            kv.Value.baseline = kv.Key != null ? Snapshot(kv.Key) : null;
            kv.Value.fresh = true;
            kv.Value.pending = null; // what waited was the old world's (kept there): the new world's restore says what waits
            if (kv.Key != null && GameNet.NetAuthority.IsNetworked && !kv.Key.IsReplica) kv.Key.awaitingRestore = true;
        }
    }

    private static void TryRestore(CurrencyWallet wallet, Tracked t)
    {
        if (wallet.IsReplica) return;
        CombatEntity player = PlayerOf(wallet);
        string key = PlayerKeys.Of(player);
        if (key == null) return; // not known yet (the character has not arrived) - or a player who keeps nothing
        Dictionary<string, Record> all = Records();
        if (all == null) return; // the world is not open here (another window has it, or it cannot be read)
        t.key = key;
        wallet.awaitingRestore = false;
        // The kept amounts, plus whatever the wallet gained or spent since the wait began (nothing, while it was locked).
        // A player new to this world keeps what they hold - or, after a change of world, starts at the starting amounts.
        // A kept currency that is not loaded here yet (only a later scene uses it) waits: the wallet cannot hold it before
        // it loads, so it has neither gained nor spent any, and it gets its kept amount the first time it holds it.
        // The wallet counts as restored only afterwards: setting a balance must not rewrite the record being read.
        var target = new Dictionary<CurrencyDefinition, int>();
        HashSet<string> pending = null;
        if (all.TryGetValue(key, out Record r) && r.balances != null)
            foreach (Entry e in r.balances.ToArray())
            {
                if (e == null || string.IsNullOrEmpty(e.currency)) continue;
                CurrencyDefinition c = CurrencyDefinition.FindByKey(e.currency);
                if (c == null)
                {
                    if (pending == null) pending = new HashSet<string>();
                    pending.Add(e.currency);
                }
                else if (c.KeepBetweenSessions) target[c] = e.amount;
            }
        if (t.fresh)
            foreach (CurrencyBalance b in Copy(wallet.Balances))
                if (b != null && b.currency != null && b.currency.KeepBetweenSessions && !target.ContainsKey(b.currency))
                    target[b.currency] = b.currency.StartingAmount;
        foreach (KeyValuePair<CurrencyDefinition, int> kv in target)
        {
            CurrencyDefinition c = kv.Key;
            int before = t.baseline != null && t.baseline.TryGetValue(c, out int b) ? b : c.StartingAmount;
            long amount = (long)kv.Value + wallet.GetBalance(c) - before;
            wallet.SetBalance(c, (int)Math.Max(0L, Math.Min(amount, c.MaxAmount)));
        }
        t.restored = true;
        t.baseline = null;
        t.fresh = false;
        t.pending = pending;
        Keep(wallet, t); // from now on what it holds is kept
    }

    private static Dictionary<CurrencyDefinition, int> Snapshot(CurrencyWallet wallet)
    {
        var d = new Dictionary<CurrencyDefinition, int>();
        if (wallet == null || wallet.Balances == null) return d;
        foreach (CurrencyBalance b in wallet.Balances)
            if (b != null && b.currency != null && b.currency.KeepBetweenSessions) d[b.currency] = b.amount;
        return d;
    }

    private static List<CurrencyBalance> Copy(IReadOnlyList<CurrencyBalance> balances) =>
        balances != null ? new List<CurrencyBalance>(balances) : new List<CurrencyBalance>();

    /// <summary>The kept entry of one currency in a player's record (null when there is none).</summary>
    private static Entry KeptEntry(string key, string currency)
    {
        Dictionary<string, Record> all = Records();
        if (all == null || string.IsNullOrEmpty(key) || !all.TryGetValue(key, out Record r) || r.balances == null) return null;
        Entry found = null;
        foreach (Entry e in r.balances)
            if (e != null && e.currency == currency) found = e; // the last one, as the restore reads them
        return found;
    }

    private static void Keep(CurrencyWallet wallet, Tracked t)
    {
        Dictionary<string, Record> all = Records();
        string key = t?.key;
        if (all == null || wallet == null || string.IsNullOrEmpty(key) || !PlayerKeys.IsValid(key)) return;
        if (!all.TryGetValue(key, out Record r))
        {
            r = new Record { key = key };
            all[key] = r;
        }
        CombatEntity p = PlayerOf(wallet);
        if (p != null) r.name = PartySystem.NameOf(p);
        // Rebuilt from the wallet. A kept amount the wallet does not hold stays as it was: one the restore left waiting (not
        // loaded then - loaded since or not, it is given back when the wallet first holds it), or one of a currency this
        // game does not know now (removed, renamed, not loaded).
        var balances = new List<Entry>();
        var held = new HashSet<string>();
        foreach (CurrencyBalance b in wallet.Balances)
            if (b != null && b.currency != null && b.currency.KeepBetweenSessions && held.Add(b.currency.NetworkKey))
                balances.Add(new Entry { currency = b.currency.NetworkKey, amount = b.amount });
        if (r.balances != null)
            foreach (Entry e in r.balances)
                if (e != null && !string.IsNullOrEmpty(e.currency) && !held.Contains(e.currency) &&
                    ((t.pending != null && t.pending.Contains(e.currency)) || CurrencyDefinition.FindByKey(e.currency) == null))
                    balances.Add(e);
        r.balances = balances;
        WorldStore.MarkDirty();
    }

    private static CombatEntity PlayerOf(CurrencyWallet wallet)
    {
        CombatEntity e = CombatEntity.Resolve(wallet.gameObject);
        if (e == null) e = wallet.GetComponentInChildren<CombatEntity>(true);
        return e != null && e.Kind == CombatEntity.EntityKind.Player ? e : null;
    }

    /// <summary>Restores wallets whose player became known.</summary>
    /// <summary>
    /// Kept currencies the restores left waiting that are loaded now are given back now (the wallet holds them: its owner's
    /// client is told, and can spend them), not only when the server first uses them for that player.
    /// </summary>
    private static void GiveBackLoaded()
    {
        List<CurrencyWallet> waiting = null;
        foreach (KeyValuePair<CurrencyWallet, Tracked> kv in tracked)
            if (kv.Key != null && kv.Value.restored && kv.Value.pending != null && kv.Value.pending.Count > 0)
                (waiting ??= new List<CurrencyWallet>()).Add(kv.Key);
        if (waiting == null) return;
        foreach (CurrencyWallet w in waiting)
        {
            if (w == null || !tracked.TryGetValue(w, out Tracked t) || t.pending == null) continue;
            foreach (string key in new List<string>(t.pending))
            {
                CurrencyDefinition c = CurrencyDefinition.FindByKey(key);
                if (c != null) w.GetBalance(c); // makes its entry: Added gives the kept amount back, once
            }
        }
    }

    private sealed class Runner : MonoBehaviour
    {
        private float nextLook;
        private float nextLoadedLook;
        private bool sceneLoaded;

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => sceneLoaded = true;

        private void Update()
        {
            float now = Time.unscaledTime;
            // A scene came (its currencies with it), and now and then for currencies loaded some other way.
            if (sceneLoaded || now >= nextLoadedLook)
            {
                sceneLoaded = false;
                nextLoadedLook = now + 5f;
                GiveBackLoaded();
            }
            if (now < nextLook) return;
            nextLook = now + 0.5f;
            scratch.Clear();
            foreach (KeyValuePair<CurrencyWallet, Tracked> kv in tracked)
            {
                CurrencyWallet w = kv.Key;
                Tracked t = kv.Value;
                if (w == null)
                {
                    scratch.Add(w);
                    continue;
                }
                if (t.restored) continue;
                TryRestore(w, t);
                // No key came: this player may spend what they have (kept only if their key comes later: then what they
                // spent or got meanwhile is added to their kept amounts).
                if (!t.restored && w.awaitingRestore && now >= t.giveUpAt)
                    w.awaitingRestore = false;
            }
            foreach (CurrencyWallet w in scratch) tracked.Remove(w);
        }
    }
}
