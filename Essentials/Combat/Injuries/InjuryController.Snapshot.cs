using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// The injury state as bytes.
// - For the network layer: the server captures it when it changes, client copies apply it. A client's copy never runs
//   the rules; it only shows the state (health panel, missing limbs) and uses it for what it predicts itself (movement,
//   hands). The character's own client gets everything (CaptureSnapshot); everyone else only what they can see of
//   someone else's body (CapturePublicSnapshot: missing limbs, how impaired each part is - enough to limp and to pick
//   up a lost limb), a few dozen bytes that change far less often.
// - For saves: the rules side keeps a player's injuries between sessions (CaptureSave / RestoreSave, see InjurySaves).
public partial class InjuryController
{
    private const byte SnapshotVersion = 4;
    private const byte SaveVersion = 1;

    private const byte PartLost = 1, PartHasRecord = 2, PartHasLostWith = 4;
    private const byte InjuryBandaged = 1, InjuryTreated = 2, InjuryWorseningStopped = 4, InjuryHealingAllowed = 8, InjuryReopened = 16;

    private MemoryStream snapshotStream;
    private BinaryWriter snapshotWriter;
    private readonly Dictionary<int, Injury> snapshotReuse = new Dictionary<int, Injury>();
    private bool warnedSnapshotMismatch;

    /// <summary>True on a client copy that was only sent the public state (someone else's character): no injuries are known here.</summary>
    public bool PublicOnly { get; private set; }

    private BinaryWriter BeginWrite()
    {
        if (snapshotStream == null)
        {
            snapshotStream = new MemoryStream(256);
            snapshotWriter = new BinaryWriter(snapshotStream);
        }
        snapshotStream.SetLength(0);
        snapshotStream.Position = 0;
        return snapshotWriter;
    }

    private byte[] EndWrite()
    {
        snapshotWriter.Flush();
        return snapshotStream.ToArray();
    }

    /// <summary>The whole injury state (parts, injuries, the treatment being applied) as bytes, for the character's own client.</summary>
    public byte[] CaptureSnapshot()
    {
        BinaryWriter w = BeginWrite();
        WriteParts(w, true);
        int count = 0;
        foreach (InjuryPartState p in parts)
            count += p.injuries.Count;
        w.Write((ushort)Mathf.Min(count, ushort.MaxValue));
        foreach (InjuryPartState p in parts)
        {
            foreach (Injury i in p.injuries)
            {
                w.Write(i.Uid);
                w.Write((byte)p.Index);
                w.Write(i.Type != null ? i.Type.id ?? "" : "");
                w.Write((byte)Mathf.Clamp(i.StageIndex, 0, 255));
                w.Write((byte)i.Side);
                w.Write(i.UntreatedTime);
                w.Write(i.BleedTime);
                w.Write(i.HealProgress);
                w.Write(Flags(i));
                w.Write(i.BandageEffectiveness);
                w.Write(i.TreatedHealMultiplier);
                w.Write(i.ImpairmentReduction);
                w.Write(i.TreatmentLabel ?? "");
                w.Write((byte)i.Form);
                w.Write(i.BleedingFactor);
                w.Write(i.HealingFactor);
                w.Write(i.WorseningFactor);
            }
        }
        ActiveTreatment a = activeTreatment;
        w.Write(a != null);
        if (a != null)
        {
            w.Write(a.Treatment != null ? a.Treatment.name ?? "" : "");
            w.Write((byte)(a.Part != null ? a.Part.Index : 0));
            w.Write(a.InjuryUid);
            w.Write(a.Duration);
            w.Write(a.Elapsed);
        }
        return EndWrite();
    }

    /// <summary>
    /// What everyone may know of this character's body, as bytes: which parts are missing (with the lost limbs' records)
    /// and how impaired each part is. No injuries, no condition, no treatment.
    /// </summary>
    public byte[] CapturePublicSnapshot()
    {
        BinaryWriter w = BeginWrite();
        WriteParts(w, false);
        return EndWrite();
    }

    private void WriteParts(BinaryWriter w, bool full)
    {
        w.Write(SnapshotVersion);
        w.Write(full);
        w.Write(partsSignature);
        w.Write((byte)Mathf.Min(parts.Count, 255));
        for (int k = 0; k < parts.Count && k < 255; k++)
        {
            InjuryPartState p = parts[k];
            SeveredLimbRecord r = p.LostLimb;
            byte flags = 0;
            if (p.Lost) flags |= PartLost;
            if (r != null) flags |= PartHasRecord;
            if (p.LostWith != null) flags |= PartHasLostWith;
            w.Write(flags);
            if (full)
                w.Write(p.Condition);
            else
                w.Write((byte)Mathf.RoundToInt(Mathf.Clamp01(p.Lost ? 1f : p.Impairment) * 255f));
            if (p.LostWith != null)
                w.Write((byte)p.LostWith.Index);
            if (r != null)
            {
                w.Write(r.Id);
                w.Write(r.Remaining);
                w.Write(r.Condition);
                w.Write(r.Gone);
                w.Write(r.DespawnAt > 0f ? Mathf.Max(0.01f, r.DespawnAt - Time.time) : 0f);
            }
        }
    }

    private static byte Flags(Injury i)
    {
        byte flags = 0;
        if (i.Bandaged) flags |= InjuryBandaged;
        if (i.Treated) flags |= InjuryTreated;
        if (i.WorseningStopped) flags |= InjuryWorseningStopped;
        if (i.HealingAllowed) flags |= InjuryHealingAllowed;
        if (i.Reopened) flags |= InjuryReopened;
        return flags;
    }

    private static void ReadFlags(Injury i, byte flags)
    {
        i.Bandaged = (flags & InjuryBandaged) != 0;
        i.Treated = (flags & InjuryTreated) != 0;
        i.WorseningStopped = (flags & InjuryWorseningStopped) != 0;
        i.HealingAllowed = (flags & InjuryHealingAllowed) != 0;
        i.Reopened = (flags & InjuryReopened) != 0;
    }

    /// <summary>
    /// Client copies: shows the state the server captured (<see cref="CaptureSnapshot"/> or
    /// <see cref="CapturePublicSnapshot"/>). Ignored where the rules run. Injuries the copy already knows are updated
    /// in place (the Health tab keeps its rows). Returns false when the bytes could not be read.
    /// </summary>
    public bool ApplySnapshot(byte[] data)
    {
        if (RulesHere || data == null || data.Length < 7)
            return false;
        try
        {
            using (var ms = new MemoryStream(data, false))
            using (var r = new BinaryReader(ms))
            {
                if (r.ReadByte() != SnapshotVersion)
                    return false;
                bool full = r.ReadBoolean();
                int signature = r.ReadInt32();
                int partCount = r.ReadByte();
                if (signature != partsSignature || partCount != Mathf.Min(parts.Count, 255))
                {
                    // Not the same body parts as the server's: its Injury Settings differ from this machine's.
                    if (!warnedSnapshotMismatch)
                        Debug.LogWarning($"[Injuries] {name}: the injury state sent by the server does not fit this machine's body parts " +
                                         "(different Injury Settings?). It is ignored.", this);
                    warnedSnapshotMismatch = true;
                    return false;
                }
                InjurySettings s = Settings;
                for (int k = 0; k < partCount; k++)
                {
                    InjuryPartState p = parts[k];
                    byte flags = r.ReadByte();
                    bool lost = (flags & PartLost) != 0;
                    float before = p.Condition;
                    if (full)
                    {
                        p.Condition = r.ReadSingle();
                        p.ReplicatedImpairment = -1f;
                    }
                    else
                    {
                        p.ReplicatedImpairment = r.ReadByte() / 255f;
                    }
                    p.LostWith = (flags & PartHasLostWith) != 0 ? PartAt(r.ReadByte()) : null;
                    SeveredLimbRecord record = null;
                    if ((flags & PartHasRecord) != 0)
                    {
                        int recordId = r.ReadInt32();
                        float remaining = r.ReadSingle();
                        float limbCondition = r.ReadSingle();
                        bool gone = r.ReadBoolean();
                        float despawnIn = r.ReadSingle();
                        // The record this copy already holds keeps counting; a new one starts from what was sent.
                        record = p.LostLimb != null && p.LostLimb.Id == recordId ? p.LostLimb : new SeveredLimbRecord { Id = recordId, Owner = this };
                        record.OwnerName = OwnerName();
                        record.PartId = p.Id;
                        record.PartName = p.Name;
                        record.LostAt = Time.time;
                        record.TimeLimit = Mathf.Max(0f, remaining);
                        record.ConditionAtLoss = limbCondition;
                        record.DecayPerSecond = s.reattachment.limbDecayPerSecond;
                        record.Rots = s.reattachment.expiredLimbsRot;
                        record.Gone = gone;
                        record.DespawnAt = despawnIn > 0f ? Time.time + despawnIn : 0f;
                    }
                    if (lost && !p.Lost) HideVisuals(p);
                    else if (!lost && p.Lost) RestoreVisuals(p);
                    p.Lost = lost;
                    p.LostLimb = record;
                    if (full && !lost && p.Condition < before - 0.01f)
                    {
                        // Its condition dropped: a hit landed there (the HUD's figure flashes it).
                        p.LastHitTime = Time.time;
                        PartHit?.Invoke(p, before - p.Condition);
                    }
                }
                PublicOnly = !full;
                if (!full)
                {
                    // Someone else's character: nothing more is told.
                    for (int k = 0; k < parts.Count; k++)
                        parts[k].injuries.Clear();
                    activeTreatment = null;
                }
                else
                {
                    ReadInjuries(r, s);
                }
            }
        }
        catch (EndOfStreamException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        effectsDirty = true;
        MarkChanged();
        return true;
    }

    private InjuryPartState PartAt(int index) => index >= 0 && index < parts.Count ? parts[index] : null;

    private void ReadInjuries(BinaryReader r, InjurySettings s)
    {
        // The injuries this copy already shows are kept and updated (the Health tab's rows and hover texts point at them).
        snapshotReuse.Clear();
        for (int k = 0; k < parts.Count; k++)
        {
            List<Injury> list = parts[k].injuries;
            for (int i = 0; i < list.Count; i++)
                snapshotReuse[list[i].Uid] = list[i];
            list.Clear();
        }
        int injuryCount = r.ReadUInt16();
        for (int k = 0; k < injuryCount; k++)
        {
            int uid = r.ReadInt32();
            InjuryPartState p = PartAt(r.ReadByte());
            InjuryTypeDefinition type = s.FindType(r.ReadString());
            int stage = r.ReadByte();
            var side = (BodyPartSide)r.ReadByte();
            float untreated = r.ReadSingle(), bleedTime = r.ReadSingle(), heal = r.ReadSingle();
            byte flags = r.ReadByte();
            float bandage = r.ReadSingle(), healMul = r.ReadSingle(), impRed = r.ReadSingle();
            string label = r.ReadString();
            var form = (DamageForm)r.ReadByte();
            float bleedFactor = r.ReadSingle(), healFactor = r.ReadSingle(), worsenFactor = r.ReadSingle();
            if (p == null || type == null)
                continue;
            if (!snapshotReuse.TryGetValue(uid, out Injury injury) || injury.Type != type)
                injury = new Injury { Uid = uid };
            injury.Type = type;
            injury.StageIndex = stage;
            injury.Part = p;
            injury.Side = side;
            injury.UntreatedTime = untreated;
            injury.BleedTime = bleedTime;
            injury.HealProgress = heal;
            ReadFlags(injury, flags);
            injury.BandageEffectiveness = bandage;
            injury.TreatedHealMultiplier = healMul;
            injury.ImpairmentReduction = impRed;
            injury.TreatmentLabel = label;
            injury.Form = form;
            injury.BleedingFactor = bleedFactor;
            injury.HealingFactor = healFactor;
            injury.WorseningFactor = worsenFactor;
            p.injuries.Add(injury);
        }
        snapshotReuse.Clear();
        activeTreatment = null;
        if (r.ReadBoolean())
        {
            string tName = r.ReadString();
            InjuryPartState tp = PartAt(r.ReadByte());
            int uid = r.ReadInt32();
            float duration = r.ReadSingle(), elapsed = r.ReadSingle();
            if (tp != null)
                activeTreatment = new ActiveTreatment
                {
                    Treatment = new InjuryTreatment { name = tName }, Part = tp, InjuryUid = uid, Injury = tp.Find(uid),
                    Duration = duration, Elapsed = elapsed,
                };
        }
    }

    // ------------------------------------------------------------------ saves
    /// <summary>
    /// The lasting injury state as bytes, where the rules run: every part's condition and whether it is missing, the
    /// injuries and illnesses with their treatment and progress, and the immunities. Parts and injury types are named by
    /// their ids, so a save survives changes to the settings (a part or type that no longer exists is dropped).
    /// </summary>
    public byte[] CaptureSave()
    {
        BinaryWriter w = BeginWrite();
        w.Write(SaveVersion);
        w.Write(parts.Count);
        foreach (InjuryPartState p in parts)
        {
            w.Write(p.Id ?? "");
            w.Write(p.Condition);
            w.Write(p.Lost);
            w.Write(p.LostWith != null ? p.LostWith.Id ?? "" : "");
        }
        int count = 0;
        foreach (InjuryPartState p in parts)
            count += p.injuries.Count;
        w.Write(count);
        foreach (InjuryPartState p in parts)
        {
            foreach (Injury i in p.injuries)
            {
                w.Write(p.Id ?? "");
                w.Write(i.Type != null ? i.Type.id ?? "" : "");
                w.Write((byte)Mathf.Clamp(i.StageIndex, 0, 255));
                w.Write((byte)i.Side);
                w.Write(i.Age);
                w.Write(i.UntreatedTime);
                w.Write(i.BleedTime);
                w.Write(i.HealProgress);
                w.Write(Flags(i));
                w.Write(i.BandageEffectiveness);
                w.Write(i.TreatedHealMultiplier);
                w.Write(i.ImpairmentReduction);
                w.Write(i.TreatmentLabel ?? "");
                w.Write((byte)i.Form);
            }
        }
        int immune = 0;
        foreach (KeyValuePair<string, float> kv in immunity)
            if (kv.Value > Time.time) immune++;
        w.Write(immune);
        foreach (KeyValuePair<string, float> kv in immunity)
        {
            if (kv.Value <= Time.time)
                continue;
            w.Write(kv.Key);
            w.Write(kv.Value - Time.time);
        }
        return EndWrite();
    }

    /// <summary>Has the character anything worth keeping (an injury, a missing or worn part, an immunity)?</summary>
    public bool HasLastingState
    {
        get
        {
            if (immunity.Count > 0)
                return true;
            for (int i = 0; i < parts.Count; i++)
            {
                InjuryPartState p = parts[i];
                if (p.Lost || p.injuries.Count > 0 || p.Condition < p.MaxCondition - 0.01f)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Puts a kept state back (<see cref="CaptureSave"/>), replacing the current one, where the rules run. A limb that was
    /// missing stays missing; the limb itself did not last (only abilities that regrow one, or a healer, bring it back).
    /// Returns false when the bytes could not be read (nothing is changed then).
    /// </summary>
    public bool RestoreSave(byte[] data)
    {
        if (!RulesHere || data == null || data.Length < 5)
            return false;
        var partIds = new List<string>();
        var conditions = new List<float>();
        var lost = new List<bool>();
        var lostWith = new List<string>();
        var saved = new List<SavedInjury>();
        var immune = new List<KeyValuePair<string, float>>();
        try
        {
            using (var ms = new MemoryStream(data, false))
            using (var r = new BinaryReader(ms))
            {
                if (r.ReadByte() != SaveVersion)
                    return false;
                int partCount = r.ReadInt32();
                if (partCount < 0 || partCount > 4096)
                    return false;
                for (int k = 0; k < partCount; k++)
                {
                    partIds.Add(r.ReadString());
                    conditions.Add(r.ReadSingle());
                    lost.Add(r.ReadBoolean());
                    lostWith.Add(r.ReadString());
                }
                int injuryCount = r.ReadInt32();
                if (injuryCount < 0 || injuryCount > 4096)
                    return false;
                for (int k = 0; k < injuryCount; k++)
                {
                    var si = new SavedInjury
                    {
                        part = r.ReadString(), type = r.ReadString(), stage = r.ReadByte(), side = (BodyPartSide)r.ReadByte(),
                        age = r.ReadSingle(), untreated = r.ReadSingle(), bleedTime = r.ReadSingle(), heal = r.ReadSingle(),
                        flags = r.ReadByte(), bandage = r.ReadSingle(), healMul = r.ReadSingle(), impRed = r.ReadSingle(),
                        label = r.ReadString(), form = (DamageForm)r.ReadByte(),
                    };
                    saved.Add(si);
                }
                int immuneCount = r.ReadInt32();
                if (immuneCount < 0 || immuneCount > 4096)
                    return false;
                for (int k = 0; k < immuneCount; k++)
                {
                    string id = r.ReadString();
                    immune.Add(new KeyValuePair<string, float>(id, r.ReadSingle()));
                }
            }
        }
        catch (EndOfStreamException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }

        // Everything was read: the current state goes, the kept one comes back.
        CancelTreatment(null);
        InjurySettings s = Settings;
        for (int i = 0; i < parts.Count; i++)
        {
            InjuryPartState p = parts[i];
            for (int j = p.injuries.Count - 1; j >= 0; j--)
                RemoveRunner(p.injuries[j]);
            p.injuries.Clear();
            if (p.Lost)
                RestorePart(p);
            p.Condition = p.MaxCondition;
        }
        for (int k = 0; k < partIds.Count; k++)
        {
            InjuryPartState p = GetPart(partIds[k]);
            if (p == null)
                continue;
            p.Condition = Mathf.Clamp(conditions[k], 0f, p.MaxCondition);
            if (!lost[k] || p.IsWholeBody)
                continue;
            p.Lost = true;
            p.Condition = 0f;
            HideVisuals(p);
        }
        for (int k = 0; k < partIds.Count; k++)
        {
            InjuryPartState p = GetPart(partIds[k]);
            if (p == null || !p.Lost)
                continue;
            InjuryPartState with = string.IsNullOrEmpty(lostWith[k]) ? null : GetPart(lostWith[k]);
            p.LostWith = with != null && with.Lost && with != p ? with : null;
            if (p.LostWith != null)
                continue;
            // The limb did not last between sessions: its record says it is gone.
            SeveredLimbRecord record = SeveredLimbRegistry.Create();
            record.PartId = p.Id;
            record.PartName = p.Name;
            record.Owner = this;
            record.OwnerName = OwnerName();
            record.ConditionAtLoss = 0.05f;
            record.LostAt = Time.time;
            record.TimeLimit = 0f;
            record.DecayPerSecond = 0f;
            record.Rots = s.reattachment.expiredLimbsRot;
            record.Gone = true;
            p.LostLimb = record;
            expiredNotified.Add(record.Id);
        }
        foreach (SavedInjury si in saved)
        {
            InjuryTypeDefinition type = s.FindType(si.type);
            InjuryPartState p = type != null && type.systemic ? wholeBody : GetPart(si.part);
            if (type == null || p == null || type.stages == null || type.stages.Count == 0)
                continue;
            var injury = new Injury
            {
                Type = type, StageIndex = Mathf.Clamp(si.stage, 0, type.stages.Count - 1), Part = p, Side = si.side, Age = si.age,
                UntreatedTime = si.untreated, BleedTime = si.bleedTime, HealProgress = Mathf.Clamp01(si.heal),
                BandageEffectiveness = si.bandage, TreatedHealMultiplier = si.healMul, ImpairmentReduction = si.impRed,
                TreatmentLabel = si.label ?? "", Form = si.form,
            };
            ReadFlags(injury, si.flags);
            UpdateFactors(injury);
            p.injuries.Add(injury);
        }
        immunity.Clear();
        foreach (KeyValuePair<string, float> kv in immune)
            if (!string.IsNullOrEmpty(kv.Key) && kv.Value > 0f)
                immunity[kv.Key] = Time.time + kv.Value;
        bleedDebt = 0f;
        effectsDirty = true;
        MarkChanged();
        RecomputeEffects();
        return true;
    }

    private struct SavedInjury
    {
        public string part, type, label;
        public int stage;
        public BodyPartSide side;
        public float age, untreated, bleedTime, heal, bandage, healMul, impRed;
        public byte flags;
        public DamageForm form;
    }
}
