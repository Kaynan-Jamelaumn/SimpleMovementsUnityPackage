using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs every field (made on demand, kept across scenes): advances the <see cref="FarmClock"/>, samples each field's
/// weather now and then, and updates the plants - each at most once per <see cref="AgriSettings.tickHours"/>, round
/// robin over all fields, at most <see cref="AgriSettings.plantsPerFrame"/> a frame (catching up after time away is spread
/// over frames). Saves changed fields every few minutes and on quit. Where the rules do not run (a network client) it only
/// keeps the clock.
/// </summary>
[DefaultExecutionOrder(-50)]
public class FarmDirector : MonoBehaviour
{
    private static FarmDirector instance;
    private static readonly List<SoilField> fields = new List<SoilField>();
    private static readonly List<GrowingStructure> structures = new List<GrowingStructure>();

    private readonly PlantScratch scratch = new PlantScratch();
    private int fieldCursor, plantCursor;

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

    private void Update()
    {
        FarmClock.Tick(Time.deltaTime);
        double now = FarmClock.Now;
        AgriSettings s = AgricultureDatabase.Instance.Settings;
        for (int i = fields.Count - 1; i >= 0; i--)
        {
            SoilField f = fields[i];
            if (f == null) { fields.RemoveAt(i); continue; }
            f.Tick(now);
        }
        StepPlants(now, s);
    }

    /// <summary>
    /// Round robin over every plant of every field: a plant due (not updated for Tick Hours) is advanced to now. At most
    /// Plants Per Frame are advanced and a bounded number looked at per frame; a pass with nothing due ends the frame's work.
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
            FieldState state = f != null && f.RulesHere ? f.State : null;
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
