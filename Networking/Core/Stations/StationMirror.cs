using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// How a station that runs on the server (a forge, a tannery, an alchemist's cauldron) is shown on the clients: each
    /// client keeps its own copy of the simulation and follows the server's. The simulations are deterministic - the same
    /// operations at the same steps give the same result - so the server sends only what players do (operations, numbered)
    /// and its clock; every copy simulates the rest itself. A full snapshot is sent when a client starts following (or
    /// falls out of step), and a checksum now and then tells a copy it still shows the same thing.
    /// <para>Pure timing and bookkeeping: the feeds and followers below do the work, the network components only move bytes.</para>
    /// </summary>
    public static class StationMirror
    {
        /// <summary>Most steps a copy simulates to catch up before it asks for a snapshot instead (5 minutes).</summary>
        public const int MaxCatchUp = 3000;
        /// <summary>Least seconds between two snapshots a client asks for.</summary>
        public const float ResyncInterval = 2f;
    }

    /// <summary>
    /// When a server sends a station's clock to the clients following it: every <see cref="Interval"/> while someone works
    /// there, every <see cref="IdleInterval"/> otherwise, at once when there are operations to send; a checksum with every
    /// <see cref="ChecksumEvery"/>-th beat.
    /// </summary>
    public sealed class StationBeat
    {
        public float Interval = 0.1f;
        public float IdleInterval = 0.5f;
        public int ChecksumEvery = 10;
        private float next = float.NegativeInfinity;
        private int beats;

        /// <summary>Is a beat due at <paramref name="now"/>?</summary>
        public bool Due(float now, bool pendingOperations) => pendingOperations || now >= next;

        /// <summary>A beat was sent at <paramref name="now"/>; true when it should carry a checksum.</summary>
        public bool Sent(float now, bool busy)
        {
            next = now + (busy ? Interval : IdleInterval);
            beats++;
            return ChecksumEvery <= 1 || beats % ChecksumEvery == 0;
        }
    }

    /// <summary>What happened when a copy took a packet.</summary>
    public enum FollowResult
    {
        /// <summary>Applied: the copy is at the server's step.</summary>
        Followed,
        /// <summary>No snapshot yet: the packet was ignored.</summary>
        Waiting,
        /// <summary>The copy is out of step (a gap, too far behind, a checksum that differs): it needs a snapshot.</summary>
        NeedSnapshot,
    }

    /// <summary>The client side's bookkeeping: is a snapshot in, which operation came last, when to ask for a snapshot again.</summary>
    public sealed class StationFollowState
    {
        private float nextAsk = float.NegativeInfinity;

        public bool Ready { get; private set; }
        public int LastSeq { get; private set; }
        public bool WantsSnapshot { get; private set; } = true;

        public void Restored(int seq)
        {
            Ready = true;
            LastSeq = seq;
            WantsSnapshot = false;
        }

        public void Took(int seq) => LastSeq = seq;

        /// <summary>Out of step: the copy waits for a snapshot.</summary>
        public void Lost()
        {
            Ready = false;
            WantsSnapshot = true;
        }

        /// <summary>Should the client ask for a snapshot now (at most every <see cref="StationMirror.ResyncInterval"/>)?</summary>
        public bool ShouldAsk(float now)
        {
            if (!WantsSnapshot || now < nextAsk)
                return false;
            nextAsk = now + StationMirror.ResyncInterval;
            return true;
        }

        public void Clear()
        {
            Ready = false;
            LastSeq = 0;
            WantsSnapshot = true;
            nextAsk = float.NegativeInfinity;
        }
    }

    // ====================================================================== crafting stations

    /// <summary>What every client shows of a crafting station besides its workshop: who works there, which items were tried.</summary>
    [Serializable]
    public class CraftStationMeta
    {
        public int version;
        /// <summary>The network id (+1) of the player working there; 0 = nobody.</summary>
        public int tender;
        public List<string> inspected = new List<string>();
    }

    /// <summary>A crafting station's workshop as it is now, with the number of the last operation it includes.</summary>
    [Serializable]
    public class CraftMirrorSnapshot
    {
        public int seq;
        public bool hasWorkshop;
        public CraftWorkshopState state = new CraftWorkshopState();
        public CraftStationMeta meta = new CraftStationMeta();

        public string ToJson() => JsonUtility.ToJson(this);

        public static CraftMirrorSnapshot FromJson(string json)
        {
            try { return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<CraftMirrorSnapshot>(json); }
            catch (Exception) { return null; }
        }
    }

    /// <summary>The operations recorded since the last packet (numbered from <see cref="firstSeq"/>), the server's step, maybe a checksum and the station's meta.</summary>
    [Serializable]
    public class CraftMirrorPacket
    {
        public int firstSeq;
        public List<CraftOperation> ops = new List<CraftOperation>();
        public long step;
        public bool hasChecksum;
        public long checksum;
        public bool hasMeta;
        public CraftStationMeta meta = new CraftStationMeta();

        public string ToJson() => JsonUtility.ToJson(this);

        public static CraftMirrorPacket FromJson(string json)
        {
            try { return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<CraftMirrorPacket>(json); }
            catch (Exception) { return null; }
        }
    }

    /// <summary>
    /// The server side: numbers every operation a workshop records and hands out packets (the operations since the last
    /// one, the step, a checksum) and snapshots (the whole workshop and the last number it includes).
    /// </summary>
    public sealed class CraftWorkshopFeed
    {
        private CraftWorkshop workshop;
        private readonly List<CraftOperation> pending = new List<CraftOperation>();
        private int seq;

        /// <summary>The workshop fed (null = none: the station is empty).</summary>
        public CraftWorkshop Workshop => workshop;
        /// <summary>The number of the last operation recorded.</summary>
        public int Seq => seq;
        public bool HasPending => pending.Count > 0;

        /// <summary>Feeds <paramref name="w"/> from now on (a new or restored workshop: the clients need a snapshot).</summary>
        public void Attach(CraftWorkshop w)
        {
            if (workshop != null)
                workshop.OperationRecorded -= OnRecorded;
            workshop = w;
            pending.Clear();
            if (workshop != null)
                workshop.OperationRecorded += OnRecorded;
        }

        private void OnRecorded(CraftOperation op)
        {
            seq++;
            pending.Add(op.Clone());
        }

        public CraftMirrorSnapshot Snapshot(CraftStationMeta meta) => new CraftMirrorSnapshot
        {
            seq = seq,
            hasWorkshop = workshop != null,
            state = workshop != null ? workshop.Capture(false) : new CraftWorkshopState(),
            meta = meta ?? new CraftStationMeta(),
        };

        /// <summary>The operations since the last packet, the step and (when <paramref name="withChecksum"/>) the checksum.</summary>
        public CraftMirrorPacket TakePacket(bool withChecksum, CraftStationMeta meta = null)
        {
            var p = new CraftMirrorPacket
            {
                firstSeq = seq - pending.Count + 1,
                step = workshop != null ? workshop.Step : 0,
                hasChecksum = withChecksum && workshop != null,
                checksum = withChecksum && workshop != null ? workshop.Checksum() : 0,
                hasMeta = meta != null,
                meta = meta ?? new CraftStationMeta(),
            };
            p.ops.AddRange(pending);
            pending.Clear();
            return p;
        }

        /// <summary>Drops the operations not sent yet (a snapshot that includes them goes to everyone instead).</summary>
        public void DropPending() => pending.Clear();
    }

    /// <summary>The client side: a copy of the server's workshop, restored from snapshots and moved on by packets.</summary>
    public sealed class CraftWorkshopFollower
    {
        private readonly CraftRules rules;
        private readonly StationFollowState state = new StationFollowState();

        public CraftWorkshopFollower(CraftRules rules) { this.rules = rules; }

        /// <summary>The copy (null before the first snapshot, or when the station's workshop is empty).</summary>
        public CraftWorkshop Workshop { get; private set; }
        public StationFollowState State => state;
        public CraftStationMeta Meta { get; private set; } = new CraftStationMeta();

        /// <summary>Raised when the copy is replaced by a snapshot.</summary>
        public event Action<CraftWorkshop> Replaced;

        public void Restore(CraftMirrorSnapshot s)
        {
            if (s == null)
                return;
            Workshop = s.hasWorkshop && s.state != null ? CraftWorkshop.Restore(rules, s.state) : null;
            if (s.meta != null)
                Meta = s.meta;
            state.Restored(s.seq);
            Replaced?.Invoke(Workshop);
        }

        public FollowResult Apply(CraftMirrorPacket p)
        {
            if (p == null)
                return FollowResult.Followed;
            if (p.hasMeta && p.meta != null)
                Meta = p.meta;
            if (!state.Ready)
                return FollowResult.Waiting;
            CraftWorkshop w = Workshop;
            for (int i = 0; i < p.ops.Count; i++)
            {
                int n = p.firstSeq + i;
                if (n <= state.LastSeq)
                    continue; // already in the snapshot
                if (n != state.LastSeq + 1 || w == null || !w.ApplyRemote(p.ops[i], StationMirror.MaxCatchUp))
                    return Lose();
                state.Took(n);
            }
            if (w == null)
                return FollowResult.Followed;
            if (!w.RunTo(p.step, StationMirror.MaxCatchUp))
                return Lose();
            if (p.hasChecksum && unchecked((uint)p.checksum) != w.Checksum())
                return Lose();
            w.ForgetHistory(64);
            return FollowResult.Followed;
        }

        private FollowResult Lose()
        {
            state.Lost();
            return FollowResult.NeedSnapshot;
        }
    }

    // ====================================================================== alchemy stations

    /// <summary>What every client shows of an alchemy station besides its preparation: who brews there, the residue in the vessel.</summary>
    [Serializable]
    public class AlchemyStationMeta
    {
        public int version;
        public int tender;
        public List<SubstanceAmount> residue = new List<SubstanceAmount>();
    }

    [Serializable]
    public class AlchemyMirrorSnapshot
    {
        public int seq;
        public bool hasBrew;
        public AlchemyBrewState state = new AlchemyBrewState();
        public AlchemyStationMeta meta = new AlchemyStationMeta();

        public string ToJson() => JsonUtility.ToJson(this);

        public static AlchemyMirrorSnapshot FromJson(string json)
        {
            try { return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<AlchemyMirrorSnapshot>(json); }
            catch (Exception) { return null; }
        }
    }

    [Serializable]
    public class AlchemyMirrorPacket
    {
        public int firstSeq;
        public List<AlchemyOperation> ops = new List<AlchemyOperation>();
        public long step;
        public bool hasChecksum;
        public long checksum;
        public bool hasMeta;
        public AlchemyStationMeta meta = new AlchemyStationMeta();

        public string ToJson() => JsonUtility.ToJson(this);

        public static AlchemyMirrorPacket FromJson(string json)
        {
            try { return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<AlchemyMirrorPacket>(json); }
            catch (Exception) { return null; }
        }
    }

    /// <summary>The server side of an alchemy station (see <see cref="CraftWorkshopFeed"/>).</summary>
    public sealed class AlchemyBrewFeed
    {
        private AlchemyBrew brew;
        private readonly List<AlchemyOperation> pending = new List<AlchemyOperation>();
        private int seq;

        public AlchemyBrew Brew => brew;
        public int Seq => seq;
        public bool HasPending => pending.Count > 0;

        public void Attach(AlchemyBrew b)
        {
            if (brew != null)
                brew.OperationRecorded -= OnRecorded;
            brew = b;
            pending.Clear();
            if (brew != null)
                brew.OperationRecorded += OnRecorded;
        }

        private void OnRecorded(AlchemyOperation op)
        {
            seq++;
            pending.Add(op.Clone());
        }

        public AlchemyMirrorSnapshot Snapshot(AlchemyStationMeta meta) => new AlchemyMirrorSnapshot
        {
            seq = seq,
            hasBrew = brew != null,
            state = brew != null ? brew.Capture(false) : new AlchemyBrewState(),
            meta = meta ?? new AlchemyStationMeta(),
        };

        public AlchemyMirrorPacket TakePacket(bool withChecksum, AlchemyStationMeta meta = null)
        {
            var p = new AlchemyMirrorPacket
            {
                firstSeq = seq - pending.Count + 1,
                step = brew != null ? brew.Step : 0,
                hasChecksum = withChecksum && brew != null,
                checksum = withChecksum && brew != null ? brew.Checksum() : 0,
                hasMeta = meta != null,
                meta = meta ?? new AlchemyStationMeta(),
            };
            p.ops.AddRange(pending);
            pending.Clear();
            return p;
        }

        public void DropPending() => pending.Clear();
    }

    /// <summary>The client side of an alchemy station (see <see cref="CraftWorkshopFollower"/>).</summary>
    public sealed class AlchemyBrewFollower
    {
        private readonly AlchemyRules rules;
        private readonly StationFollowState state = new StationFollowState();

        public AlchemyBrewFollower(AlchemyRules rules) { this.rules = rules; }

        public AlchemyBrew Brew { get; private set; }
        public StationFollowState State => state;
        public AlchemyStationMeta Meta { get; private set; } = new AlchemyStationMeta();

        public event Action<AlchemyBrew> Replaced;

        public void Restore(AlchemyMirrorSnapshot s)
        {
            if (s == null)
                return;
            Brew = s.hasBrew && s.state != null ? AlchemyBrew.Restore(rules, s.state) : null;
            if (s.meta != null)
                Meta = s.meta;
            state.Restored(s.seq);
            Replaced?.Invoke(Brew);
        }

        public FollowResult Apply(AlchemyMirrorPacket p)
        {
            if (p == null)
                return FollowResult.Followed;
            if (p.hasMeta && p.meta != null)
                Meta = p.meta;
            if (!state.Ready)
                return FollowResult.Waiting;
            AlchemyBrew b = Brew;
            for (int i = 0; i < p.ops.Count; i++)
            {
                int n = p.firstSeq + i;
                if (n <= state.LastSeq)
                    continue;
                if (n != state.LastSeq + 1 || b == null || !b.ApplyRemote(p.ops[i], StationMirror.MaxCatchUp))
                    return Lose();
                state.Took(n);
            }
            if (b == null)
                return FollowResult.Followed;
            if (!b.RunTo(p.step, StationMirror.MaxCatchUp))
                return Lose();
            if (p.hasChecksum && unchecked((uint)p.checksum) != b.Checksum())
                return Lose();
            return FollowResult.Followed;
        }

        private FollowResult Lose()
        {
            state.Lost();
            return FollowResult.NeedSnapshot;
        }
    }
}
