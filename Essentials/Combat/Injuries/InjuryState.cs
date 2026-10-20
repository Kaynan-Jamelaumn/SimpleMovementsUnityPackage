using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One injury on a body part (a laceration on the left arm): its type and stage, bleeding, treatment and healing.</summary>
public sealed class Injury
{
    private static int nextUid = 1;

    /// <summary>Unique id while the game runs (treatment requests and the health panel refer to it).</summary>
    public int Uid { get; internal set; } = nextUid++;
    public InjuryTypeDefinition Type { get; internal set; }
    /// <summary>Index of the stage in the type's stages (0 = mildest).</summary>
    public int StageIndex { get; internal set; }
    public InjuryPartState Part { get; internal set; }

    /// <summary>Seconds since it appeared or last got worse.</summary>
    public float Age { get; internal set; }
    /// <summary>Seconds it has gone untreated (it gets worse when this reaches the stage's Worsen After Seconds).</summary>
    public float UntreatedTime { get; internal set; }
    /// <summary>Seconds it has bled (it clots after the stage's Clots After Seconds).</summary>
    public float BleedTime { get; internal set; }
    /// <summary>0-1: healed when it reaches 1.</summary>
    public float HealProgress { get; internal set; }

    /// <summary>A bandage (or another bleeding treatment) is on it.</summary>
    public bool Bandaged { get; internal set; }
    /// <summary>Share of the bleeding the bandage stops (0-1).</summary>
    public float BandageEffectiveness { get; internal set; }
    /// <summary>A treatment was applied (splinted, stitched, disinfected...).</summary>
    public bool Treated { get; internal set; }
    /// <summary>What treated it ("Splinted", "Stitched"), shown in the health panel.</summary>
    public string TreatmentLabel { get; internal set; } = "";
    /// <summary>Healing speed multiplier given by its treatment.</summary>
    public float TreatedHealMultiplier { get; internal set; } = 1f;
    /// <summary>Share of its impairment its treatment removes (a splinted leg limps less).</summary>
    public float ImpairmentReduction { get; internal set; }
    /// <summary>A treatment stopped it from getting worse (disinfected, stitched, splinted).</summary>
    public bool WorseningStopped { get; internal set; }
    /// <summary>A treatment allows it to heal (stages with Needs Treatment To Heal).</summary>
    public bool HealingAllowed { get; internal set; }
    /// <summary>Who caused it (may be null).</summary>
    public CombatEntity Cause { get; internal set; }
    /// <summary>The form of the hit that caused it (a slash, a bite...; None = not from a hit, or formless).</summary>
    public DamageForm Form { get; internal set; }
    /// <summary>The character's resistances on its bleeding (thick blood 0.7; a skeleton 0). Kept up to date by the controller.</summary>
    public float BleedingFactor { get; internal set; } = 1f;
    /// <summary>The character's resistances on its healing and clotting speed (a fast healer 1.5).</summary>
    public float HealingFactor { get; internal set; } = 1f;
    /// <summary>
    /// The character's resistances on its getting worse (where the rules run): above 1 it worsens sooner, below 1 it is a
    /// chance to fight it off when the time comes.
    /// </summary>
    public float WorseningFactor { get; internal set; } = 1f;

    public InjuryStage Stage => Type != null ? Type.Stage(StageIndex) : null;
    public int Severity => Stage != null ? Stage.severity : 0;
    /// <summary>The stage's name ("Broken Bone"), else the type's.</summary>
    public string Name => Type != null ? Type.StageName(StageIndex) : "Injury";

    /// <summary>It stopped bleeding by itself.</summary>
    public bool Clotted
    {
        get
        {
            InjuryStage s = Stage;
            return s != null && s.clotsAfterSeconds > 0f && BleedTime >= s.clotsAfterSeconds;
        }
    }

    /// <summary>Health lost per second right now (bandages and clotting counted, before the global bleeding rules).</summary>
    public float BleedingPerSecond
    {
        get
        {
            InjuryStage s = Stage;
            if (s == null || s.bleedingPerSecond <= 0f || Clotted)
                return 0f;
            float b = s.bleedingPerSecond * Mathf.Max(0f, BleedingFactor);
            if (Bandaged)
                b *= 1f - Mathf.Clamp01(BandageEffectiveness);
            return b;
        }
    }

    public bool IsBleeding => BleedingPerSecond > 1e-4f;

    /// <summary>How much it impairs its part (0-1), after its treatment.</summary>
    public float Impairment
    {
        get
        {
            InjuryStage s = Stage;
            return s != null ? Mathf.Clamp01(s.impairment * (1f - Mathf.Clamp01(ImpairmentReduction))) : 0f;
        }
    }

    /// <summary>It is healing right now (some healing speed).</summary>
    public bool CanHeal
    {
        get
        {
            InjuryStage s = Stage;
            if (s == null)
                return false;
            if (s.needsTreatmentToHeal && !HealingAllowed)
                return false;
            return Treated || Bandaged || s.untreatedHealRate > 0f;
        }
    }

    /// <summary>Healing speed multiplier right now (0 = not healing).</summary>
    public float HealRate
    {
        get
        {
            InjuryStage s = Stage;
            if (s == null || !CanHeal)
                return 0f;
            float rate = Treated || Bandaged ? Mathf.Max(0.01f, TreatedHealMultiplier) : s.untreatedHealRate;
            return rate * Mathf.Max(0f, HealingFactor);
        }
    }

    /// <summary>It will get worse if left alone.</summary>
    public bool CanWorsen
    {
        get
        {
            InjuryStage s = Stage;
            return s != null && s.worsenAfterSeconds > 0f && !WorseningStopped;
        }
    }

    /// <summary>Seconds before it gets worse (infinity when it cannot).</summary>
    public float WorsensIn => CanWorsen ? Mathf.Max(0f, Stage.worsenAfterSeconds - UntreatedTime) / Mathf.Max(1f, WorseningFactor) : float.PositiveInfinity;

    /// <summary>Seconds left before it heals at the current rate (infinity when it does not heal).</summary>
    public float HealsIn(float speedMultiplier)
    {
        InjuryStage s = Stage;
        float rate = HealRate * Mathf.Max(0f, speedMultiplier);
        if (s == null || rate <= 0f)
            return float.PositiveInfinity;
        return (1f - HealProgress) * s.healSeconds / rate;
    }

    /// <summary>Clears the treatment state (a new hit opened it again).</summary>
    internal void ClearTreatment()
    {
        Bandaged = false;
        BandageEffectiveness = 0f;
        Treated = false;
        TreatmentLabel = "";
        TreatedHealMultiplier = 1f;
        ImpairmentReduction = 0f;
        WorseningStopped = false;
        HealingAllowed = false;
    }

    /// <summary>Moves to another stage (or type), keeping what makes sense: bleeding and healing start over.</summary>
    internal void SetStage(InjuryTypeDefinition type, int stage)
    {
        Type = type;
        StageIndex = Mathf.Max(0, stage);
        Age = 0f;
        UntreatedTime = 0f;
        BleedTime = 0f;
        HealProgress = 0f;
    }

    internal static void ResetUids() => nextUid = 1;
}

/// <summary>The state of one body part of a character: condition, injuries, and whether it is lost.</summary>
public sealed class InjuryPartState
{
    public InjuryBodyPart Def { get; internal set; }
    public float Condition { get; internal set; }
    public bool Lost { get; internal set; }
    /// <summary>The limb record while the part is lost (null when it was lost with its parent: the parent's record has it).</summary>
    public SeveredLimbRecord LostLimb { get; internal set; }
    /// <summary>The part it was lost with (a hand lost with its arm), or null.</summary>
    public InjuryPartState LostWith { get; internal set; }
    /// <summary>Impairment 0-1 (1 = useless or lost), worked out after every change.</summary>
    public float Impairment { get; internal set; }

    internal readonly List<Injury> injuries = new List<Injury>(2);
    public IReadOnlyList<Injury> Injuries => injuries;

    public string Id => Def != null ? Def.id : "";
    public string Name => Def != null ? Def.Name : "?";
    public float MaxCondition => Def != null ? Mathf.Max(1f, Def.maxCondition) : 100f;
    public float Condition01 => Lost ? 0f : Mathf.Clamp01(Condition / MaxCondition);

    public bool IsBleeding
    {
        get
        {
            for (int i = 0; i < injuries.Count; i++)
                if (injuries[i].IsBleeding) return true;
            return false;
        }
    }

    public float BleedingPerSecond
    {
        get
        {
            float b = 0f;
            for (int i = 0; i < injuries.Count; i++)
                b += injuries[i].BleedingPerSecond;
            return b;
        }
    }

    /// <summary>The worst severity among its injuries (-1 = none).</summary>
    public int WorstSeverity
    {
        get
        {
            int w = -1;
            for (int i = 0; i < injuries.Count; i++)
                w = Mathf.Max(w, injuries[i].Severity);
            return w;
        }
    }

    public bool HasTag(string tag)
    {
        for (int i = 0; i < injuries.Count; i++)
            if (injuries[i].Type != null && injuries[i].Type.HasTag(tag)) return true;
        return false;
    }

    public Injury Find(int uid)
    {
        for (int i = 0; i < injuries.Count; i++)
            if (injuries[i].Uid == uid) return injuries[i];
        return null;
    }
}

/// <summary>
/// A limb that was cut off: whose it was, which part(s), its condition when it came off, when, and the time limit for
/// reattaching it. The world / inventory item of the limb carries the record's id (<see cref="SeveredLimbItemSO"/>);
/// records live in <see cref="SeveredLimbRegistry"/> where the game rules run.
/// </summary>
public sealed class SeveredLimbRecord
{
    public int Id { get; internal set; }
    /// <summary>The lost part (the arm when an arm with its hand came off).</summary>
    public string PartId { get; internal set; }
    public string PartName { get; internal set; }
    /// <summary>Parts that came off with it (the hand of a lost arm).</summary>
    public List<string> IncludedParts { get; } = new List<string>();
    /// <summary>The character it belongs to (null once destroyed).</summary>
    public InjuryController Owner { get; internal set; }
    public string OwnerName { get; internal set; }
    /// <summary>Condition (0-1) when it came off.</summary>
    public float ConditionAtLoss { get; internal set; }
    /// <summary>Time.time when it came off.</summary>
    public float LostAt { get; internal set; }
    /// <summary>Seconds it can be reattached normally.</summary>
    public float TimeLimit { get; internal set; }
    /// <summary>Condition (fraction) lost per second off the body.</summary>
    public float DecayPerSecond { get; internal set; }
    /// <summary>Items that fell with it (armour, the item it held): names, for the health panel.</summary>
    public List<string> DroppedItems { get; } = new List<string>();
    /// <summary>It was reattached (or its owner respawned with it): the item is only a leftover now.</summary>
    public bool Consumed { get; internal set; }
    /// <summary>
    /// The limb lay in the world too long and was removed (Injury Settings ▸ Limbs ▸ Cleanup): only abilities that regrow
    /// a limb can bring the part back.
    /// </summary>
    public bool Gone { get; internal set; }
    /// <summary>Time (Time.time) the limb lying in the world is removed (0 = never, or it is not lying in the world).</summary>
    public float DespawnAt { get; internal set; }

    /// <summary>Condition (0-1) now.</summary>
    public float Condition => Mathf.Clamp01(ConditionAtLoss - DecayPerSecond * Mathf.Max(0f, Time.time - LostAt));
    /// <summary>Seconds left to reattach it normally (0 = too late).</summary>
    public float Remaining => Mathf.Max(0f, LostAt + TimeLimit - Time.time);
    public bool Expired => Time.time >= LostAt + TimeLimit;

    /// <summary>"Left Arm of Kaynan".</summary>
    public string Title => string.IsNullOrEmpty(OwnerName) ? PartName : $"{PartName} of {OwnerName}";

    /// <summary>Tooltip lines (inventory hover, health panel).</summary>
    public void Describe(List<string> lines)
    {
        lines.Add($"<b>{Title}</b>");
        if (Consumed)
        {
            lines.Add("<color=#9aa0a6>Its owner has it back. Only a leftover.</color>");
            return;
        }
        if (Gone)
        {
            lines.Add("<color=#f07a6e>The limb is gone (it lay too long). Only an ability that regrows limbs can bring it back.</color>");
            return;
        }
        lines.Add($"Condition  {Condition * 100f:0}%");
        if (DespawnAt > 0f && Time.time < DespawnAt)
            lines.Add($"<color=#9aa0a6>Rots away (removed from the world) in {FormatTime(DespawnAt - Time.time)} unless picked up.</color>");
        if (Expired)
            lines.Add("<color=#f07a6e>Too late to reattach normally: only special abilities (or a city surgeon) can.</color>");
        else
            lines.Add($"<color=#7fd37f>Reattach within {FormatTime(Remaining)}</color> (a surgical kit or a healing ability).");
        if (IncludedParts.Count > 0)
            lines.Add("With: " + string.Join(", ", IncludedParts));
        if (DroppedItems.Count > 0)
            lines.Add("Dropped with it: " + string.Join(", ", DroppedItems));
    }

    /// <summary>"4:05", "45 s".</summary>
    public static string FormatTime(float seconds)
    {
        if (float.IsInfinity(seconds) || float.IsNaN(seconds))
            return "∞";
        seconds = Mathf.Max(0f, seconds);
        if (seconds < 60f)
            return $"{Mathf.CeilToInt(seconds)} s";
        int s = Mathf.CeilToInt(seconds);
        return s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
    }
}

/// <summary>Every severed limb record of the game (where the game rules run).</summary>
public static class SeveredLimbRegistry
{
    private static readonly Dictionary<int, SeveredLimbRecord> records = new Dictionary<int, SeveredLimbRecord>();
    private static int nextId = 1;

    /// <summary>A record was added or changed (reattached, rotted).</summary>
    public static event Action<SeveredLimbRecord> Changed;

    public static SeveredLimbRecord Create()
    {
        var r = new SeveredLimbRecord { Id = nextId++ };
        records[r.Id] = r;
        return r;
    }

    public static SeveredLimbRecord Get(int id) => id > 0 && records.TryGetValue(id, out SeveredLimbRecord r) ? r : null;

    public static IEnumerable<SeveredLimbRecord> All => records.Values;

    internal static void NotifyChanged(SeveredLimbRecord r) => Changed?.Invoke(r);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        records.Clear();
        nextId = 1;
        Changed = null;
        Injury.ResetUids();
    }
}
