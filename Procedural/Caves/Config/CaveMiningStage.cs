using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>
    /// What makes a dungeon a cave to explore and mine: list it in a Dungeon Profile's Custom Stages (Tools >
    /// SimpleMovements > Caves > Create Cave Setup does it). It runs after the Analysis stage, before Population, and
    /// decides - as data, on the dungeon's worker thread - the cave's special chambers (rich veins, crystal grottos,
    /// guarded hoards), its deposits (every resource of its <see cref="resources"/> by depth and rarity, in veins, in the
    /// walls and on the floor) and its standing rock formations (see <see cref="CaveMiningPlanner"/>). When the dungeon is
    /// built it builds them too (see <see cref="CaveBuildSession"/>): the deposits as <see cref="OreDeposit"/>s mined with
    /// a pick into the inventory, the formations as batched meshes with colliders, the way out and the way deeper as
    /// natural passages; it makes the cave's creatures tougher with depth and keeps the cave's state between visits.
    /// <para>The asset is shared by every cave of the profile: what one visit changes lives in that visit, never here.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "SimpleMovements/Caves/Cave Mining Stage", fileName = "CaveMiningStage")]
    public class CaveMiningStage : DungeonStageAsset, IDungeonBuildExtension
    {
        [Header("Resources")]
        [Tooltip("The deposits the cave can hold (depth ranges, commonness, rarity, veins, mining values). Empty = the built-in resources: every ore, fuel, stone, clay, salt and gemstone of the Crafting Database.")]
        public CaveResourceTable resources;

        [Header("Depth")]
        [Tooltip("How deep each place counts: per level, across a level, per deeper cave, per point of difficulty; where the bands begin.")]
        public CaveDepthSettings depth = new CaveDepthSettings();

        [Header("Deposits")]
        public CaveDepositSettings deposits = new CaveDepositSettings();

        [Header("Special Chambers")]
        public CaveChamberSettings chambers = new CaveChamberSettings();

        [Header("Rock Formations")]
        public CaveFormationSettings formations = new CaveFormationSettings();

        [Header("Creatures")]
        public CaveEnemySettings creatures = new CaveEnemySettings();

        [Header("Between Visits")]
        public CavePersistenceSettings persistence = new CavePersistenceSettings();

        [Header("Way Out and Way Deeper")]
        public CaveAccessSettings passages = new CaveAccessSettings();

        // The settings as plain data, made on the main thread (Prepare) and read by generations on worker threads. A
        // generation keeps the copy it started with (one reference read), so editing the asset never changes it midway.
        private volatile CaveRules prepared;

        /// <summary>The last prepared rules (made now when the asset was never prepared). Main thread.</summary>
        public CaveRules Rules => prepared ?? (prepared = MakeRules());

        public override void Prepare() => prepared = MakeRules();

        /// <summary>The settings and the resource table as plain data (main thread: it reads the table asset).</summary>
        public CaveRules MakeRules()
        {
            bool hasTable = !ReferenceEquals(resources, null) && resources != null && resources.entries != null && resources.entries.Count > 0;
            return CaveRules.Create(hasTable ? resources.entries : null, depth, deposits, chambers, formations, creatures, persistence, passages);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            // A cave stage runs between Analysis (walking and wall distances) and Population (which must keep clear of what it places).
            slot = StageSlot.AfterAnalysis;
        }

        private void OnValidate()
        {
            slot = StageSlot.AfterAnalysis;
            prepared = null;
            DungeonProfile.MarkEdited();
        }

        /// <summary>
        /// Is <paramref name="stage"/> the first Cave Mining Stage of <paramref name="profile"/>? Only that one plans and builds
        /// (a cave has one resource table: put every resource in it); any other does nothing. Thread-safe.
        /// </summary>
        public static bool IsFirstIn(CompiledProfile profile, CaveMiningStage stage)
        {
            if (profile == null || stage == null)
                return true;
            foreach (DungeonStageAsset s in profile.CustomStages)
                if (s is CaveMiningStage cave)
                    return ReferenceEquals(cave, stage);
            return true;
        }

        /// <summary>Cave Mining Stages in a profile's Custom Stages (the setup checks warn about more than one).</summary>
        public static int CountIn(DungeonProfile profile)
        {
            int n = 0;
            if (profile != null && profile.customStages != null)
                foreach (DungeonStageAsset s in profile.customStages)
                    if (s is CaveMiningStage)
                        n++;
            return n;
        }

        /// <summary>Worker thread: plans the cave's chambers, deposits and formations.</summary>
        public override void Run(DungeonContext context)
        {
            if (!IsFirstIn(context.Profile, this))
            {
                // Its placements would not say which stage's table they index: one stage per cave.
                context.Warn("More than one Cave Mining Stage in the profile: only the first is used (put every resource in its Resource Table).");
                return;
            }
            CaveRules rules = prepared;
            if (rules == null)
            {
                // Never prepared (a profile compiled before this stage existed): the built-in resources and the defaults.
                rules = CaveRules.Create(null, depth, deposits, chambers, formations, creatures, persistence, passages);
            }
            CaveMiningPlanner.Plan(context, rules);
        }

        /// <summary>Main thread: the part of a build that makes this stage's deposits, formations and passages (the profile's first cave stage only).</summary>
        public IDungeonBuildSession BeginBuild(DungeonBuildContext context) =>
            context == null || IsFirstIn(context.Profile, this) ? new CaveBuildSession(this, Rules, context) : null;
    }
}
