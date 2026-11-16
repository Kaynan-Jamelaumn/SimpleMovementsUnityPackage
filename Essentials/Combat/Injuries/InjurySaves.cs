using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps players' injuries between sessions (Injury Settings ▸ Keep Between Sessions): every part's condition, the
/// injuries and illnesses with their treatment and progress, missing limbs and immunities, where the rules run (offline,
/// the server), per player key (<see cref="PlayerKeys"/>), in the world's store (<see cref="WorldStore"/>: the file the
/// wallets and the housing books are in). A player who leaves with a broken leg comes back with it - logging out cures
/// nothing - and the injuries also survive the player object being made again (another scene).
/// <list type="bullet">
/// <item>A controller's kept state comes back as soon as its player is known (the character arrived). What happened to
/// the body before that is replaced by it.</item>
/// <item>The state is kept at once after something that matters (an injury, a treatment, a lost limb, a respawn) and
/// every few seconds while injuries heal; a crash loses at most that much healing.</item>
/// <item>A limb that was missing stays missing; the limb item itself does not last (the inventory is not kept): only an
/// ability that regrows limbs brings it back.</item>
/// <item>A character that leaves dead, when a respawn starts healthy (Clear On Respawn), keeps nothing.</item>
/// </list>
/// A client keeps nothing: the server's state is the truth. Each world keeps its own.
/// </summary>
public static class InjurySaves
{
    [Serializable]
    private class Record
    {
        public string key = "";
        public string name = "";
        /// <summary>The state (<see cref="InjuryController.CaptureSave"/>) as base64.</summary>
        public string state = "";
    }

    [Serializable]
    private class Part
    {
        public List<Record> players = new List<Record>();
    }

    private sealed class Tracked
    {
        public string key;
        public bool restored;
        /// <summary>The world changed: a player the new world does not know starts healthy.</summary>
        public bool fresh;
        public bool dirty;
        /// <summary>Not a player (a mob): never kept.</summary>
        public bool ignored;
    }

    private const string Section = "injuries";

    /// <summary>A switch for the whole game (tests, a game with its own saves): off = injuries are never kept or restored.</summary>
    public static bool Enabled = true;

    /// <summary>Real seconds between two writes of injuries that only heal (a new injury or a treatment is kept at once).</summary>
    public const float KeepEverySeconds = 15f;

    private static readonly Dictionary<InjuryController, Tracked> tracked = new Dictionary<InjuryController, Tracked>();
    private static readonly List<InjuryController> scratch = new List<InjuryController>();
    private static Dictionary<string, Record> records;
    private static Runner runner;
    private static float nextKeep;
    private static bool urgent;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Enabled = true;
        tracked.Clear();
        scratch.Clear();
        records = null;
        runner = null;
        nextKeep = 0f;
        urgent = false;
    }

    /// <summary>A controller started (called by <see cref="InjuryController"/>): its kept state comes back once its player is known.</summary>
    internal static void Track(InjuryController controller)
    {
        if (!Enabled || !Application.isPlaying || controller == null || tracked.ContainsKey(controller))
            return;
        if (runner == null)
        {
            var go = new GameObject("[Injury Saves]") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
            WorldStore.Committing += Write;
            WorldStore.WorldChanged += OnWorldChanged;
        }
        tracked[controller] = new Tracked();
    }

    /// <summary>A controller goes (its player left, or the object is made again in another scene): its state is kept.</summary>
    internal static void Untrack(InjuryController controller)
    {
        if (controller == null || !tracked.TryGetValue(controller, out Tracked t))
            return;
        if (t.restored)
            Keep(controller, t);
        tracked.Remove(controller);
    }

    /// <summary>
    /// Something changed on a controller (called by <see cref="InjuryController"/>): it is kept soon, or at once when
    /// <paramref name="now"/> (something that matters: nothing of it may be lost).
    /// </summary>
    internal static void Touch(InjuryController controller, bool now)
    {
        if (tracked.Count == 0 || controller == null || !tracked.TryGetValue(controller, out Tracked t) || !t.restored)
            return;
        t.dirty = true;
        if (now)
            urgent = true;
    }

    /// <summary>Keeps a controller's state now (it is written with the world).</summary>
    public static void SaveNow(InjuryController controller)
    {
        if (controller != null && tracked.TryGetValue(controller, out Tracked t) && t.restored)
            Keep(controller, t);
    }

    /// <summary>Keeps every known player's state (they are written with the world).</summary>
    public static void SaveAll()
    {
        foreach (KeyValuePair<InjuryController, Tracked> kv in tracked)
            if (kv.Key != null && kv.Value.restored)
                Keep(kv.Key, kv.Value);
    }

    /// <summary>Is this controller's state kept (its player has a key and the kept state came back)?</summary>
    public static bool IsKept(InjuryController controller) =>
        controller != null && tracked.TryGetValue(controller, out Tracked t) && t.restored;

    // ---------------------------------------------------------------- the world's part
    private static Dictionary<string, Record> Records()
    {
        if (records != null)
            return records;
        if (!WorldStore.IsOpen)
            return null;
        records = new Dictionary<string, Record>();
        string json = WorldStore.Get(Section);
        if (!string.IsNullOrEmpty(json))
            try
            {
                Part part = JsonUtility.FromJson<Part>(json);
                if (part?.players != null)
                    foreach (Record r in part.players)
                        if (r != null && !string.IsNullOrEmpty(r.key))
                            records[r.key] = r;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Injuries] The kept injuries could not be read ({e.Message}).");
            }
        return records;
    }

    /// <summary>The world is being written: the injuries' part goes in.</summary>
    private static void Write()
    {
        if (records == null)
            return;
        SaveAll();
        var part = new Part();
        part.players.AddRange(records.Values);
        WorldStore.Put(Section, JsonUtility.ToJson(part));
    }

    private static void OnWorldChanged()
    {
        // Another world: what was read belongs to the old one. Every controller is restored again from the new world.
        records = null;
        foreach (KeyValuePair<InjuryController, Tracked> kv in tracked)
        {
            kv.Value.restored = false;
            kv.Value.key = null;
            kv.Value.dirty = false;
            kv.Value.fresh = true;
        }
    }

    private static void TryRestore(InjuryController controller, Tracked t)
    {
        if (!controller.RulesHere)
            return; // a client's copy keeps nothing
        CombatEntity player = controller.Entity;
        if (player == null)
            return;
        if (player.Kind != CombatEntity.EntityKind.Player)
        {
            t.ignored = true;
            return;
        }
        InjurySettings s = controller.Settings;
        if (!s.enabled || !s.keepBetweenSessions)
            return;
        string key = PlayerKeys.Of(player);
        if (key == null)
            return; // not known yet (the character has not arrived) - or a player who keeps nothing
        Dictionary<string, Record> all = Records();
        if (all == null)
            return; // the world is not open here
        t.key = key;
        if (all.TryGetValue(key, out Record r) && !string.IsNullOrEmpty(r.state))
        {
            bool ok = false;
            try
            {
                ok = controller.RestoreSave(Convert.FromBase64String(r.state));
            }
            catch (FormatException)
            {
            }
            if (!ok)
                Debug.LogWarning($"[Injuries] The kept injuries of {r.name} could not be read: they start healthy.");
        }
        else if (t.fresh)
        {
            controller.HealAll(true); // another world, where this player has no injuries
        }
        t.restored = true;
        t.fresh = false;
        t.dirty = false;
    }

    private static void Keep(InjuryController controller, Tracked t)
    {
        t.dirty = false;
        Dictionary<string, Record> all = Records();
        string key = t.key;
        if (all == null || controller == null || string.IsNullOrEmpty(key) || !PlayerKeys.IsValid(key) || !controller.RulesHere)
            return;
        InjurySettings s = controller.Settings;
        if (!s.enabled || !s.keepBetweenSessions)
            return;
        CombatEntity player = controller.Entity;
        // Nothing to keep: healthy, or dead when a respawn starts healthy.
        if (!controller.HasLastingState || (player != null && player.IsDead && s.clearOnRespawn))
        {
            if (all.Remove(key))
                WorldStore.MarkDirty();
            return;
        }
        string state = Convert.ToBase64String(controller.CaptureSave());
        if (!all.TryGetValue(key, out Record r))
        {
            r = new Record { key = key };
            all[key] = r;
        }
        if (player != null)
            r.name = PartySystem.NameOf(player);
        if (r.state == state)
            return;
        r.state = state;
        WorldStore.MarkDirty();
    }

    private sealed class Runner : MonoBehaviour
    {
        private float nextLook;

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now < nextLook)
                return;
            nextLook = now + 0.5f;
            scratch.Clear();
            bool anyDirty = false;
            foreach (KeyValuePair<InjuryController, Tracked> kv in tracked)
            {
                InjuryController c = kv.Key;
                Tracked t = kv.Value;
                if (c == null || t.ignored)
                {
                    scratch.Add(c);
                    continue;
                }
                if (!t.restored)
                    TryRestore(c, t);
                else if (t.dirty)
                    anyDirty = true;
            }
            foreach (InjuryController c in scratch)
                tracked.Remove(c);
            scratch.Clear();
            if (!anyDirty || (!urgent && now < nextKeep))
                return;
            urgent = false;
            nextKeep = now + KeepEverySeconds;
            foreach (KeyValuePair<InjuryController, Tracked> kv in tracked)
                if (kv.Key != null && kv.Value.restored && kv.Value.dirty)
                    Keep(kv.Key, kv.Value);
        }
    }
}
