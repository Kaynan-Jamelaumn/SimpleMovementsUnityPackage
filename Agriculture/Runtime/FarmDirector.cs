using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs every field (made on demand, kept across scenes): advances the <see cref="FarmClock"/>, samples each awake
/// field's weather now and then, and updates the plants - each at most once per <see cref="AgriSettings.tickHours"/>,
/// round robin over all fields, at most <see cref="AgriSettings.plantsPerFrame"/> a frame. Saves changed fields every few
/// minutes and on quit. Where the rules do not run (a network client) it only keeps the clock.
/// <para><b>Sleeping gardens</b> (where the rules run, with <see cref="AgriSettings.sleepWhenAlone"/>): about once a
/// second it looks where the players are. A garden with nobody within <see cref="AgriSettings.wakeRadius"/> for
/// <see cref="AgriSettings.sleepDelaySeconds"/> (beyond <see cref="AgriSettings.sleepRadius"/>) falls asleep - saved, no
/// longer simulated or drawn, freed from memory once saved; a player coming near wakes it, and it is brought up to date
/// with the weather that fell meanwhile, hour by hour (<see cref="FieldCatchUp"/>), within
/// <see cref="AgriSettings.catchUpMillisPerFrame"/> a frame, the gardens nearest a player first. Wakes and sleeps are
/// spread over frames too: as many as fit in that budget each look (at least one each way), the rest a moment later. A
/// player counts near a garden unless their scenes are apart (<see cref="MobRealms"/>: a player kept in DontDestroyOnLoad
/// counts everywhere). A sleeping garden lagging more than <see cref="AgriSettings.maintenanceLagHours"/> behind is
/// brought up to date in the background, one at a time - so an abandoned garden keeps living and wakes at once.</para>
/// </summary>
[DefaultExecutionOrder(-50)]
public class FarmDirector : MonoBehaviour
{
    private static FarmDirector instance;
    private static readonly List<SoilField> fields = new List<SoilField>();
    private static readonly List<GrowingStructure> structures = new List<GrowingStructure>();

    private readonly PlantScratch scratch = new PlantScratch();
    private readonly Stopwatch watch = new Stopwatch();
    private readonly List<(Vector3 position, Scene scene)> players = new List<(Vector3, Scene)>();
    private readonly List<SoilField> toWake = new List<SoilField>(), toSleep = new List<SoilField>();
    private readonly Stopwatch presenceWatch = new Stopwatch();
    private Func<bool> outOfTime;
    private double budgetMs;
    private int fieldCursor, plantCursor;
    private float nextPresence, nextBackgroundPick;
    /// <summary>The sleeping garden being brought up to date in the background.</summary>
    private SoilField background;

    public static IReadOnlyList<SoilField> Fields => fields;
    public static IReadOnlyList<GrowingStructure> Structures => structures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        fields.Clear();
        structures.Clear();
    }

    public static void Ensure()
    {
        if (instance != null || !Application.isPlaying) return;
        var go = new GameObject("[Farm Director]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<FarmDirector>();
    }

    public static void Register(SoilField f)
    {
        if (f == null || fields.Contains(f)) return;
        fields.Add(f);
        Ensure();
    }

    public static void Unregister(SoilField f) => fields.Remove(f);

    /// <summary>The world is written one last time before it closes: every garden read from it is saved into it.</summary>
    internal static void SaveForClosingWorld()
    {
        foreach (SoilField f in fields)
            if (f != null) f.SaveForClosingWorld();
    }

    /// <summary>Another world opened (a session started or ended): every garden puts the old world's away.</summary>
    internal static void ChangeWorld()
    {
        if (instance != null) instance.background = null;
        foreach (SoilField f in fields)
            if (f != null) f.ChangeWorld();
    }

    public static void Register(GrowingStructure s)
    {
        if (s == null || structures.Contains(s)) return;
        structures.Add(s);
        CoversChanged(s);
    }

    public static void Unregister(GrowingStructure s)
    {
        if (structures.Remove(s)) CoversChanged(s);
    }

    /// <summary>A structure moved, appeared or went: the fields it touches work out their covers again.</summary>
    public static void CoversChanged(GrowingStructure s)
    {
        foreach (SoilField f in fields)
            if (f != null && (s == null || f.Touches(s))) f.MarkCoversDirty();
    }

    private void Awake()
    {
        outOfTime = () => watch.Elapsed.TotalMilliseconds >= budgetMs;
        FarmClock.Skipped += OnClockJumped;
    }

    private void OnDestroy()
    {
        FarmClock.Skipped -= OnClockJumped;
        if (instance == this) instance = null;
    }

    private void Update()
    {
        FarmClock.Tick(Time.deltaTime);
        double now = FarmClock.Now;
        AgriSettings s = AgricultureDatabase.Instance.Settings;
        float t = Time.unscaledTime;
        if (t >= nextPresence)
        {
            nextPresence = t + 1f;
            CheckPresence(s, t);
        }
        RunCatchUps(s);
        for (int i = fields.Count - 1; i >= 0; i--)
        {
            SoilField f = fields[i];
            if (f == null) { fields.RemoveAt(i); continue; }
            f.Tick(now);
        }
        StepPlants(now, s);
    }

    // ---------------------------------------------------------------- sleeping and waking
    /// <summary>
    /// Wakes the gardens a player is near and lets those nobody is near fall asleep - the nearest first, as many as fit in
    /// the catch-up budget (at least one each way); the rest at the next look, a moment later.
    /// </summary>
    private void CheckPresence(AgriSettings s, float t)
    {
        players.Clear();
        IReadOnlyList<CombatEntity> list = CombatEntity.Players;
        for (int i = 0; i < list.Count; i++)
        {
            CombatEntity e = list[i];
            if (e != null && e.isActiveAndEnabled) players.Add((e.transform.position, e.gameObject.scene));
        }
        toWake.Clear();
        toSleep.Clear();
        for (int i = 0; i < fields.Count; i++)
        {
            SoilField f = fields[i];
            if (f == null || !f.RulesHere || f.Mirror) continue;
            if (f.Asleep && f.Loaded && !f.CatchingUp) f.SettleAsleep(); // a save that failed is tried again
            if (!f.isActiveAndEnabled) continue; // hidden fields sleep (and live on in the background)
            if (!s.sleepWhenAlone)
            {
                f.NearestPlayerSeen = 0f;
                if (f.Asleep) toWake.Add(f);
                continue;
            }
            float d = NearestPlayer(f, Mathf.Max(s.wakeRadius, s.sleepRadius));
            f.NearestPlayerSeen = d;
            if (d <= s.wakeRadius)
            {
                f.LastNearAt = t;
                if (f.Asleep) toWake.Add(f);
            }
            else if (d <= s.sleepRadius) f.LastNearAt = t;
            else if (!f.Asleep && t - f.LastNearAt >= s.sleepDelaySeconds) toSleep.Add(f);
        }
        if (toWake.Count == 0 && toSleep.Count == 0) return;
        double budget = Mathf.Max(0.25f, s.catchUpMillisPerFrame);
        toWake.Sort((a, b) => a.NearestPlayerSeen.CompareTo(b.NearestPlayerSeen));
        presenceWatch.Restart();
        int woken = 0;
        while (woken < toWake.Count && (woken == 0 || presenceWatch.Elapsed.TotalMilliseconds < budget)) toWake[woken++].WakeUp();
        presenceWatch.Restart();
        int slept = 0;
        while (slept < toSleep.Count && (slept == 0 || presenceWatch.Elapsed.TotalMilliseconds < budget)) toSleep[slept++].FallAsleep();
        if (woken < toWake.Count || slept < toSleep.Count) nextPresence = t + 0.1f; // the rest in a moment
    }

    /// <summary>
    /// Metres from the field's outline to the nearest player whose scene is not apart from it (beyond
    /// <paramref name="farther"/>: a large number).
    /// </summary>
    private float NearestPlayer(SoilField f, float farther)
    {
        Scene scene = f.gameObject.scene;
        Vector3 centre = f.CentreWorld;
        float reach = f.OuterRadius + farther;
        float best = float.MaxValue;
        for (int i = 0; i < players.Count; i++)
        {
            (Vector3 p, Scene sc) = players[i];
            if (sc != scene && MobRealms.AreScenesSeparated(scene, sc)) continue; // another realm
            float dx = p.x - centre.x, dz = p.z - centre.z;
            if (dx * dx + dz * dz > reach * reach) continue;
            float d = f.DistanceFromOutline(p);
            if (d < best) best = d;
        }
        return best;
    }

    /// <summary>The clock jumped forward (sleeping, a skip): awake gardens catch up in lockstep (the weather and the plants together).</summary>
    private void OnClockJumped(double hours)
    {
        AgriSettings s = AgricultureDatabase.Instance.Settings;
        if (hours <= s.maxStepHours) return;
        foreach (SoilField f in fields)
            if (f != null) f.CatchUpAfterJump();
    }

    /// <summary>
    /// The catch-ups under way, within the frame's budget: gardens someone waits for first (the nearest player's first),
    /// then one in the background.
    /// </summary>
    private void RunCatchUps(AgriSettings s)
    {
        budgetMs = Mathf.Max(0.25f, s.catchUpMillisPerFrame);
        watch.Restart();
        for (int guard = 0; guard <= fields.Count; guard++)
        {
            if (outOfTime()) return;
            SoilField next = null;
            for (int i = 0; i < fields.Count; i++)
            {
                SoilField f = fields[i];
                if (f != null && f.CatchingUp && f.Waking && (next == null || f.NearestPlayerSeen < next.NearestPlayerSeen)) next = f;
            }
            if (next == null) break;
            if (!next.RunCatchUp(outOfTime)) return; // the frame's time is up
        }
        if (outOfTime()) return;
        if (background != null && (!background.CatchingUp || background.Waking)) background = null;
        if (background == null && Time.unscaledTime >= nextBackgroundPick)
        {
            nextBackgroundPick = Time.unscaledTime + 1f;
            background = PickBackground(s);
            if (background != null && !background.CatchingUp) background.BeginBackgroundCatchUp();
            if (background != null && !background.CatchingUp) background = null;
        }
        if (background != null && background.RunCatchUp(outOfTime)) background = null;
    }

    /// <summary>
    /// A sleeping garden whose catch-up was left under way (it fell asleep, or was hidden, before it ended) goes on first;
    /// else the sleeping garden lagging furthest behind, when it lags more than Maintenance Lag Hours.
    /// </summary>
    private static SoilField PickBackground(AgriSettings s)
    {
        double now = FarmClock.Now;
        SoilField best = null;
        double worst = s.maintenanceLagHours;
        foreach (SoilField f in fields)
        {
            if (f == null || !f.Asleep || !f.RulesHere || f.Mirror) continue;
            if (f.CatchingUp)
            {
                if (!f.Waking) return f;
                continue;
            }
            double lived = f.LivedTo;
            if (lived < 0.0) continue; // never saved: nothing to live through
            double lag = now - lived;
            if (lag > worst)
            {
                worst = lag;
                best = f;
            }
        }
        return best;
    }

    // ---------------------------------------------------------------- plants
    /// <summary>
    /// Round robin over every plant of every awake field: a plant due (not updated for Tick Hours) is advanced to now. At
    /// most Plants Per Frame are advanced and a bounded number looked at per frame; a pass with nothing due ends the
    /// frame's work.
    /// </summary>
    private void StepPlants(double now, AgriSettings s)
    {
        int budget = Mathf.Max(1, s.plantsPerFrame);
        int scans = budget * 16 + 64;
        int idleFields = 0;
        while (budget > 0 && scans > 0 && fields.Count > 0)
        {
            if (fieldCursor >= fields.Count) fieldCursor = 0;
            SoilField f = fields[fieldCursor];
            FieldState state = f != null && f.Simulated ? f.State : null;
            if (state == null || plantCursor >= state.Plants.Count)
            {
                if (state != null) f.Sweep(now); // the end of a pass over this field
                fieldCursor++;
                plantCursor = 0;
                if (++idleFields > fields.Count) break; // every field passed with nothing due
                continue;
            }
            PlantState p = state.Plants[plantCursor++];
            scans--;
            if (p != null && now - p.last >= s.tickHours)
            {
                PlantSim.Advance(state, p, now, scratch, f.Climate);
                budget--;
                idleFields = 0;
            }
        }
    }

    private void OnApplicationQuit()
    {
        FarmClock.Save();
        foreach (SoilField f in fields)
            if (f != null) f.SaveIfDirty(true);
    }
}
