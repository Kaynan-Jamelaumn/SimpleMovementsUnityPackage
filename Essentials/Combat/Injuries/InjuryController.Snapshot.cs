using System.IO;
using UnityEngine;

// The injury state as bytes, for the network layer: the server captures it when it changes, client copies apply it.
// A client's copy never runs the rules; it only shows the state (health panel, missing limbs) and uses it for what it
// predicts itself (movement, hands).
public partial class InjuryController
{
    private const byte SnapshotVersion = 3;

    /// <summary>The whole injury state (parts, injuries, the treatment being applied) as bytes.</summary>
    public byte[] CaptureSnapshot()
    {
        using (var ms = new MemoryStream(256))
        using (var w = new BinaryWriter(ms))
        {
            w.Write(SnapshotVersion);
            w.Write(parts.Count);
            foreach (InjuryPartState p in parts)
            {
                w.Write(p.Id ?? "");
                w.Write(p.Condition);
                w.Write(p.Lost);
                w.Write(p.LostWith != null ? p.LostWith.Id : "");
                SeveredLimbRecord r = p.LostLimb;
                w.Write(r != null);
                if (r != null)
                {
                    w.Write(r.Id);
                    w.Write(r.Remaining);
                    w.Write(r.Condition);
                    w.Write(r.Gone);
                    w.Write(r.DespawnAt > 0f ? Mathf.Max(0.01f, r.DespawnAt - Time.time) : 0f);
                }
            }
            int count = 0;
            foreach (InjuryPartState p in parts)
                count += p.injuries.Count;
            w.Write(count);
            foreach (InjuryPartState p in parts)
            {
                foreach (Injury i in p.injuries)
                {
                    w.Write(i.Uid);
                    w.Write(p.Id ?? "");
                    w.Write(i.Type != null ? i.Type.id : "");
                    w.Write((byte)Mathf.Clamp(i.StageIndex, 0, 255));
                    w.Write(i.UntreatedTime);
                    w.Write(i.BleedTime);
                    w.Write(i.HealProgress);
                    byte flags = 0;
                    if (i.Bandaged) flags |= 1;
                    if (i.Treated) flags |= 2;
                    if (i.WorseningStopped) flags |= 4;
                    if (i.HealingAllowed) flags |= 8;
                    w.Write(flags);
                    w.Write(i.BandageEffectiveness);
                    w.Write(i.TreatedHealMultiplier);
                    w.Write(i.ImpairmentReduction);
                    w.Write(i.TreatmentLabel ?? "");
                    w.Write((byte)i.Form);
                    w.Write(i.BleedingFactor);
                    w.Write(i.HealingFactor);
                }
            }
            ActiveTreatment a = activeTreatment;
            w.Write(a != null);
            if (a != null)
            {
                w.Write(a.Treatment != null ? a.Treatment.name ?? "" : "");
                w.Write(a.Part != null ? a.Part.Id : "");
                w.Write(a.InjuryUid);
                w.Write(a.Duration);
                w.Write(a.Elapsed);
            }
            w.Flush();
            return ms.ToArray();
        }
    }

    /// <summary>
    /// Client copies: shows the state the server captured (<see cref="CaptureSnapshot"/>). Ignored where the rules run.
    /// Returns false when the bytes could not be read.
    /// </summary>
    public bool ApplySnapshot(byte[] data)
    {
        if (RulesHere || data == null || data.Length == 0)
            return false;
        try
        {
            using (var ms = new MemoryStream(data))
            using (var r = new BinaryReader(ms))
            {
                byte version = r.ReadByte();
                if (version != SnapshotVersion && version != 2)
                    return false; // 2: before damage forms and resistances
                InjurySettings s = Settings;
                int partCount = r.ReadInt32();
                var lostWith = new string[partCount];
                var ids = new string[partCount];
                for (int k = 0; k < partCount; k++)
                {
                    string id = r.ReadString();
                    float condition = r.ReadSingle();
                    bool lost = r.ReadBoolean();
                    lostWith[k] = r.ReadString();
                    ids[k] = id;
                    SeveredLimbRecord record = null;
                    if (r.ReadBoolean())
                    {
                        int recordId = r.ReadInt32();
                        float remaining = r.ReadSingle();
                        float limbCondition = r.ReadSingle();
                        bool gone = r.ReadBoolean();
                        float despawnIn = r.ReadSingle();
                        record = new SeveredLimbRecord
                        {
                            Id = recordId, Owner = this, OwnerName = OwnerName(), LostAt = Time.time, TimeLimit = Mathf.Max(0f, remaining),
                            ConditionAtLoss = limbCondition, DecayPerSecond = s.reattachment.limbDecayPerSecond,
                            Gone = gone, DespawnAt = despawnIn > 0f ? Time.time + despawnIn : 0f,
                        };
                    }
                    InjuryPartState p = GetPart(id);
                    if (p == null)
                        continue;
                    if (record != null)
                    {
                        record.PartId = p.Id;
                        record.PartName = p.Name;
                    }
                    p.Condition = condition;
                    if (lost && !p.Lost) HideVisuals(p);
                    else if (!lost && p.Lost) RestoreVisuals(p);
                    p.Lost = lost;
                    p.LostLimb = record;
                    p.injuries.Clear();
                }
                for (int k = 0; k < partCount; k++)
                {
                    InjuryPartState p = GetPart(ids[k]);
                    if (p != null)
                        p.LostWith = string.IsNullOrEmpty(lostWith[k]) ? null : GetPart(lostWith[k]);
                }
                int injuryCount = r.ReadInt32();
                for (int k = 0; k < injuryCount; k++)
                {
                    int uid = r.ReadInt32();
                    InjuryPartState p = GetPart(r.ReadString());
                    InjuryTypeDefinition type = s.FindType(r.ReadString());
                    int stage = r.ReadByte();
                    float untreated = r.ReadSingle(), bleedTime = r.ReadSingle(), heal = r.ReadSingle();
                    byte flags = r.ReadByte();
                    float bandage = r.ReadSingle(), healMul = r.ReadSingle(), impRed = r.ReadSingle();
                    string label = r.ReadString();
                    DamageForm form = DamageForm.None;
                    float bleedFactor = 1f, healFactor = 1f;
                    if (version >= 3)
                    {
                        form = (DamageForm)r.ReadByte();
                        bleedFactor = r.ReadSingle();
                        healFactor = r.ReadSingle();
                    }
                    if (p == null || type == null)
                        continue;
                    p.injuries.Add(new Injury
                    {
                        Uid = uid, Type = type, StageIndex = stage, Part = p, UntreatedTime = untreated, BleedTime = bleedTime, HealProgress = heal,
                        Bandaged = (flags & 1) != 0, Treated = (flags & 2) != 0, WorseningStopped = (flags & 4) != 0, HealingAllowed = (flags & 8) != 0,
                        BandageEffectiveness = bandage, TreatedHealMultiplier = healMul, ImpairmentReduction = impRed, TreatmentLabel = label,
                        Form = form, BleedingFactor = bleedFactor, HealingFactor = healFactor,
                    });
                }
                activeTreatment = null;
                if (r.ReadBoolean())
                {
                    string tName = r.ReadString();
                    InjuryPartState tp = GetPart(r.ReadString());
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
}
