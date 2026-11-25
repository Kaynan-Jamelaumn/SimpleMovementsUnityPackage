using System;
using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>
    /// A built cave (on its dungeon's root, next to the <see cref="DungeonInstance"/>): its name, how deep each place is,
    /// what was mined in it, and its memory between visits (<see cref="CaveStore"/>: mined deposits, slain creatures,
    /// emptied chests and containers). The gameplay side of caves: quests and UI read it, the cave's build fills it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class CaveInstance : MonoBehaviour
    {
        private static readonly List<CaveInstance> live = new List<CaveInstance>();
        private readonly Dictionary<string, int> minedByResource = new Dictionary<string, int>();
        private readonly List<(DungeonBreakable breakable, int id)> breakables = new List<(DungeonBreakable, int)>();
        private readonly List<(ItemPickable pickup, int id)> pickups = new List<(ItemPickable, int)>();
        private bool holdsRecord;

        /// <summary>A lump was mined in a cave (cave, deposit, player - null when nobody took it -, item): quests, statistics.</summary>
        public static event Action<CaveInstance, CaveDeposit, CombatEntity, CraftStockItemSO> Mined;

        /// <summary>A cave was built and set up (its deposits restored).</summary>
        public static event Action<CaveInstance> Opened;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            live.Clear();
            Mined = null;
            Opened = null;
        }

        /// <summary>Every cave that is built now.</summary>
        public static IReadOnlyList<CaveInstance> All => live;

        /// <summary>The cave this machine's player is in (single player: the cave the player is in), or null.</summary>
        public static CaveInstance Current
        {
            get
            {
                DungeonInstance d = DungeonSession.Current;
                return d != null ? d.GetComponent<CaveInstance>() : null;
            }
        }

        /// <summary>The cave <paramref name="inside"/> belongs to (a deposit, a creature, a prop), or null.</summary>
        public static CaveInstance Of(Component inside)
        {
            if (inside == null)
                return null;
            DungeonInstance d = DungeonNet.DungeonOf(inside);
            return d != null ? d.GetComponent<CaveInstance>() : null;
        }

        public DungeonInstance Dungeon { get; private set; }
        public CaveRules Rules { get; private set; }
        /// <summary>The cave's name ("Whispering Grotto", "Whispering Grotto - Depth 2"): the same cave always has the same name.</summary>
        public string DisplayName { get; private set; } = "Cave";
        /// <summary>The name of the cave system (without its depth): the name its entrance shows.</summary>
        public string BaseName { get; private set; } = "Cave";

        /// <summary>A request label that names a cave (a portal's default label, "Dungeon (...)", does not).</summary>
        public static bool IsCaveName(string label) =>
            !string.IsNullOrWhiteSpace(label) && !label.StartsWith("Dungeon (", StringComparison.Ordinal) && label.Length <= 64;
        /// <summary>The key of its record (profile and seed).</summary>
        public string Key { get; private set; }
        /// <summary>Its memory between visits (null on a network client's copy, or with Between Visits > Keep State off).</summary>
        public CaveStore.Record Record { get; private set; }
        /// <summary>A network client's presentation-only copy.</summary>
        public bool IsReplica { get; private set; }
        /// <summary>How deep this cave is in its chain of deeper caves (0 = entered from the world).</summary>
        public int ChainDepth { get; private set; }
        /// <summary>Lumps mined in this visit.</summary>
        public int LumpsMined { get; private set; }
        /// <summary>Deposits it holds (built so far).</summary>
        public int DepositCount { get; internal set; }
        /// <summary>Every floor is built.</summary>
        public bool IsBuilt { get; private set; }

        /// <summary>Lumps mined in this visit of a resource (by its entry name).</summary>
        public int MinedOf(string resourceName) => resourceName != null && minedByResource.TryGetValue(resourceName, out int n) ? n : 0;

        internal void Setup(DungeonInstance dungeon, CaveRules rules, bool replica)
        {
            // A fresh start (the same root may be built again).
            ReleaseRecord();
            Record = null;
            IsBuilt = false;
            LumpsMined = 0;
            DepositCount = 0;
            minedByResource.Clear();
            breakables.Clear();
            pickups.Clear();

            Dungeon = dungeon;
            Rules = rules;
            IsReplica = replica;
            DungeonLayout layout = dungeon != null ? dungeon.Layout : null;
            ChainDepth = layout != null && layout.Request != null ? layout.Request.depth : 0;
            int seed = layout != null ? layout.Seed : 0;
            // The name its entrance gave it (a client's copy gets it too), else one from its seed.
            string label = layout != null && layout.Request != null ? layout.Request.label : "";
            BaseName = IsCaveName(label) ? label : CaveDefaults.NameFor(seed != 0 ? seed : layout != null ? layout.Width * 131 + layout.Height : 1);
            DisplayName = ChainDepth > 0 ? $"{BaseName} - Depth {ChainDepth + 1}" : BaseName;
            string profile = dungeon != null && dungeon.Profile != null && dungeon.Profile.Source != null ? dungeon.Profile.Source.name : "caves";
            Key = $"{profile}:{seed}:{ChainDepth}";
            // A client's copy keeps nothing (the server's cave is the truth), and its layout has no seed.
            if (!replica && layout != null && (rules == null || rules.Persistence.keepState))
            {
                Record = CaveStore.Open(Key, Fingerprint(layout, rules), DisplayName);
                holdsRecord = Record != null;
            }
            if (!live.Contains(this))
                live.Add(this);
        }

        /// <summary>The visit is over (the cave's teardown): its record may be forgotten again. It stays readable here.</summary>
        internal void ReleaseRecord()
        {
            if (!holdsRecord)
                return;
            holdsRecord = false;
            CaveStore.Release(Record);
        }

        /// <summary>
        /// What a cave's layout is made of - its size and every placement (what, where, which entry; for a deposit its
        /// resource) - so a record kept for another layout (an edited profile or resource table, another world) is not
        /// applied to this one. The same on every build of a cave: generation is a function of its seed.
        /// </summary>
        public static int Fingerprint(DungeonLayout layout, CaveRules rules = null)
        {
            if (layout == null)
                return 0;
            unchecked
            {
                uint h = 2166136261u;
                void Mix(int v) => h = (h ^ (uint)v) * 16777619u;
                Mix(layout.AttemptSeed);
                Mix(layout.Floors.Count);
                Mix(layout.Width);
                Mix(layout.Height);
                Mix(layout.Placements.Count);
                foreach (Placement p in layout.Placements)
                {
                    Mix((int)p.Kind);
                    Mix((int)p.Table);
                    Mix(p.Entry);
                    Mix(p.Floor);
                    Mix(Mathf.FloorToInt(p.Cell.x));
                    Mix(Mathf.FloorToInt(p.Cell.y));
                    Mix(p.Link);
                    if (p.Kind == PlacementKind.Resource && rules != null)
                    {
                        // The same index in an edited table may be another resource.
                        CaveResourceEntry e = rules.Resource(p.Entry);
                        Mix(e != null ? PlacementRandom.StableHash(e.craftId ?? "") ^ (int)e.kind * 7919 : -1);
                    }
                }
                return (int)h;
            }
        }

        internal void MarkBuilt()
        {
            IsBuilt = true;
            try
            {
                Opened?.Invoke(this);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void OnDestroy()
        {
            ReleaseRecord();
            live.Remove(this);
        }

        // ------------------------------------------------------------------ depth

        /// <summary>The depth score of a place on floor <paramref name="floor"/> in area <paramref name="area"/> (-1 = a tunnel).</summary>
        public float DepthOf(int floor, int area)
        {
            float across = 0.5f;
            DungeonLayout layout = Dungeon != null ? Dungeon.Layout : null;
            if (layout != null && floor >= 0 && floor < layout.Floors.Count && layout.Floors[floor] != null)
            {
                FloorLayout f = layout.Floors[floor];
                // A client's copy has no walking distances: the middle of the level.
                if (f.DistanceFromArrival != null && area >= 0 && area < f.Areas.Count)
                {
                    int farthest = 0;
                    foreach (int d in f.DistanceFromArrival)
                        if (d > farthest)
                            farthest = d;
                    int c = f.Areas[area].CenterCell;
                    if (farthest > 0 && c >= 0 && f.DistanceFromArrival[c] >= 0)
                        across = f.DistanceFromArrival[c] / (float)farthest;
                }
            }
            float difficulty = layout != null && layout.Request != null ? layout.Request.difficulty : 1f;
            return CaveDepth.Of(Rules != null ? Rules.Depth : null, floor, across, ChainDepth, difficulty);
        }

        /// <summary>The depth score at a world position (0 outside the cave).</summary>
        public float DepthAt(Vector3 world)
        {
            if (Dungeon == null || Dungeon.Layout == null)
                return 0f;
            int floor = Dungeon.FloorAt(world);
            int area = Dungeon.TryGetArea(world, out Area a) ? a.Id : -1;
            return DepthOf(floor, area);
        }

        /// <summary>The depth band at a world position.</summary>
        public CaveDepthBand BandAt(Vector3 world) => (Rules != null ? Rules.Depth : new CaveDepthSettings()).Band(DepthAt(world));

        // ------------------------------------------------------------------ memory

        internal bool TryGetDeposit(int id, out OreDeposit.SavedState state, out float secondsAgo)
        {
            state = default;
            secondsAgo = 0f;
            return Record != null && Record.TryGetDeposit(id, out state, out secondsAgo);
        }

        internal void KeepDeposit(int id, OreDeposit.SavedState state)
        {
            if (Record == null || id < 0)
                return;
            Record.SetDeposit(id, state);
            CaveStore.Changed();
        }

        /// <summary>The number of deposit <paramref name="id"/>'s next lump: one count per seam for every visit of this cave going at once.</summary>
        internal int NextLump(int id, OreDeposit deposit)
        {
            if (deposit == null)
                return 0;
            if (Record == null || id < 0)
                return deposit.LumpsIssued;
            int n = Record.NextLump(id, deposit.LumpsIssued, deposit.CaptureState());
            CaveStore.Changed();
            return n;
        }

        internal bool IsSlain(int id) => Record != null && Record.IsSlain(id, Minutes(Rules != null ? Rules.Persistence.creaturesReturnMinutes : 45f));

        internal bool IsTaken(int id) => Record != null && Record.IsTaken(id, Minutes(Rules != null ? Rules.Persistence.lootReturnMinutes : 0f));

        internal void KeepSlain(int id)
        {
            if (Record == null || id < 0)
                return;
            Record.Slay(id);
            CaveStore.Changed();
        }

        internal void KeepTaken(int id)
        {
            if (Record == null || id < 0)
                return;
            Record.Take(id);
            CaveStore.Changed();
        }

        private static double Minutes(float minutes) => minutes > 0f ? minutes * 60.0 : 0.0;

        /// <summary>A breakable container of placement <paramref name="id"/>: once broken it stays broken between visits.</summary>
        internal void WatchBreakable(DungeonBreakable breakable, int id)
        {
            if (breakable != null && Record != null && id >= 0)
                breakables.Add((breakable, id));
        }

        /// <summary>An item lying in the cave (placement <paramref name="id"/>): once picked up it stays gone between visits.</summary>
        internal void WatchPickup(ItemPickable pickup, int id)
        {
            if (pickup != null && Record != null && id >= 0)
                pickups.Add((pickup, id));
        }

        /// <summary>
        /// The cave is being cleared: what was broken or picked up up to now is kept (this very frame too), what disappears
        /// from here on is not taken by anyone. Only objects that still exist count: a teardown during a scene unload may
        /// find them destroyed already, which says nothing about players.
        /// </summary>
        internal void StopWatching()
        {
            foreach ((DungeonBreakable b, int id) in breakables)
                if (b != null && b.Broken)
                    KeepTaken(id);
            foreach ((ItemPickable p, int id) in pickups)
                if (p != null && p.Taken)
                    KeepTaken(id);
            breakables.Clear();
            pickups.Clear();
        }

        internal void NoteMined(CaveDeposit deposit, CombatEntity player, CraftStockItemSO item)
        {
            LumpsMined++;
            string name = deposit != null && deposit.Resource != null ? deposit.Resource.name : "";
            minedByResource.TryGetValue(name, out int n);
            minedByResource[name] = n + 1;
            if (Record != null)
                Record.NoteLump();
            try
            {
                Mined?.Invoke(this, deposit, player, item);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void Update()
        {
            // Every frame (a few dozen objects): what was taken is remembered before the visit can end.
            if (breakables.Count == 0 && pickups.Count == 0)
                return;
            for (int i = breakables.Count - 1; i >= 0; i--)
            {
                (DungeonBreakable b, int id) = breakables[i];
                if (b == null || b.Broken)
                {
                    if (b != null)
                        KeepTaken(id);
                    breakables.RemoveAt(i);
                }
            }
            // An item that is gone (picked up: taken, then despawned) is remembered as taken - its room's reward included.
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                (ItemPickable p, int id) = pickups[i];
                if (p == null || p.Taken)
                {
                    KeepTaken(id);
                    pickups.RemoveAt(i);
                }
            }
        }
    }
}
