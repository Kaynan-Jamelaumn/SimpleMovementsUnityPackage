using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;
using UnityEngine.AI;

namespace ProceduralCaves
{
    /// <summary>
    /// A cave mouth in the world: a natural opening in the rock - an arch of boulders, a short tunnel into the dark, a hill
    /// rising behind - fitted to the ground it stands on (generated; local +z faces out of the cave). Walking into the
    /// tunnel darkens the screen with the cave's name (<see cref="CavePassage"/>), then the cave behind it is generated
    /// (on a worker thread, or taken from the kept generations) and the players arrive in it; they come back out in front
    /// of the mouth, and the cave keeps what they did in it for their next visit.
    /// <para>Behind it is an ordinary world <see cref="Portal"/> (child "Way In", at the tunnel's end, its own trigger off):
    /// every rule of world portals - groups and parties, shared runs, player limits, the re-entry cooldown, dungeon
    /// scenes, the network - applies. The cave's seed comes from the world seed and the entrance's position, so the same
    /// entrance always leads to the same cave.</para>
    /// <para>Placed by <see cref="CaveEntranceSpawner"/> on the endless terrain (EndlessTerrain > Cave Entrances), or by
    /// hand (Tools > SimpleMovements > Caves > Add Cave Entrance Here): set <see cref="profile"/> to a cave profile.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SimpleMovements/Caves/Cave Entrance")]
    public sealed class CaveEntrance : MonoBehaviour
    {
        [Header("Where It Leads")]
        [Tooltip("REQUIRED. The cave: a Dungeon Profile with a Cave Mining Stage in its Custom Stages (Tools > SimpleMovements > Caves > Create Cave Setup makes one).")]
        public DungeonProfile profile;
        [Tooltip("OPTIONAL. Other caves this entrance may lead to: one is picked from its seed (always the same one). Empty = Profile.")]
        public DungeonProfile[] variants = new DungeonProfile[0];
        [Tooltip("OPTIONAL. A Dungeon Manager prefab (empty = one is made from the profile for each visit).")]
        public DungeonManager manager;
        [Tooltip("Size class of the cave (Small: 1-2 levels ... Huge: many).")]
        public SizeClass size = SizeClass.Medium;
        [Tooltip("Difficulty of the cave (1 = normal): tougher creatures, and - through its depth - richer ores.")]
        [Min(0.1f)] public float difficulty = 1f;
        [Tooltip("Apply the world's difficulty rule on top (Hub World modifiers...). A spawned entrance already has it in Difficulty.")]
        public bool worldDifficulty = true;
        [Tooltip("The cave's name, shown on the way in (empty = one from its position, always the same).")]
        public string caveName = "";
        [Tooltip("0 = from its position (recommended). Any other value: the mouth's shape and the cave's name come from it.")]
        public int seed;
        [Tooltip("Classic mode: where caves are built - far below the world (ignored when dungeons get scenes of their own).")]
        public Vector3 dungeonOrigin = new Vector3(0f, -10000f, 0f);

        [Header("Players")]
        [Tooltip("Separate Runs: every group gets its own visit. Shared Run: everyone entering while a visit is going joins it.")]
        public PortalSharing sharing = PortalSharing.SeparateRuns;
        [Tooltip("Most players in one visit (0 = no limit).")]
        [Min(0)] public int maxPlayers;
        [Tooltip("Party members within this distance (meters) go in together.")]
        [Min(0f)] public float bringPartyWithin = 10f;

        [Header("Mouth")]
        [Tooltip("Generate the rock mouth (off = only the way in: for a mouth modelled in the scene - put this where its tunnel ends).")]
        public bool buildMouth = true;
        [Min(1.6f)] public float width = 3.4f;
        [Min(2.2f)] public float height = 3.6f;
        [Tooltip("Length of the tunnel (meters).")]
        [Min(1.5f)] public float depth = 3.4f;
        [Tooltip("Colour of the rock (when the cave's theme has no cave wall material, or Use Theme Material is off).")]
        public Color rockColor = new Color(0.43f, 0.4f, 0.36f);
        [Tooltip("Use the cave theme's cave wall material for the rock.")]
        public bool useThemeMaterial = true;

        [Header("Going In")]
        [Tooltip("Seconds a player stands in the tunnel (the screen darkens) before going in.")]
        [Range(0.1f, 3f)] public float fadeSeconds = 0.7f;
        [Tooltip("OPTIONAL. Played on the way in.")]
        public AudioClip sound;

        private static readonly List<CaveEntrance> live = new List<CaveEntrance>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private GameObject built;
        private Portal portal;
        private CavePassage passage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => live.Clear();

        /// <summary>Every cave entrance in the loaded world (map markers, quests).</summary>
        public static IReadOnlyList<CaveEntrance> All => live;

        /// <summary>The portal behind the mouth (null until built).</summary>
        public Portal Portal => portal;
        /// <summary>The trigger players walk into.</summary>
        public CavePassage Passage => passage;
        /// <summary>Is the mouth built?</summary>
        public bool IsBuilt => built != null;
        /// <summary>The planned site it was spawned for (spawned entrances).</summary>
        public CaveSiteId Site { get; private set; }
        public bool HasSite { get; private set; }
        /// <summary>The planned point of its site (world x, z; spawned entrances): the cave behind it is seeded from it.</summary>
        public Vector2 SitePoint { get; private set; }

        /// <summary>The seed in use (its own, or one from its position).</summary>
        public int EffectiveSeed => seed != 0 ? seed : PositionSeed(transform.position);

        /// <summary>The name in use.</summary>
        public string EffectiveName => !string.IsNullOrWhiteSpace(caveName) ? caveName.Trim() : CaveDefaults.NameFor(EffectiveSeed);

        public static int PositionSeed(Vector3 p)
        {
            int s = (int)PlacementRandom.Hash(Mathf.RoundToInt(p.x * 4f), PlacementRandom.StableHash("CaveEntrance"), Mathf.RoundToInt(p.z * 4f), 0, 0);
            return s == 0 ? 1 : s;
        }

        /// <summary>
        /// A spawned entrance (<see cref="CaveEntranceSpawner"/>): everything from the settings, before it is built. The cave
        /// behind it is seeded from its site (<paramref name="sitePoint"/>, <paramref name="siteSeed"/>), not from the spot
        /// the mouth stands on, so the same site always leads to the same cave.
        /// </summary>
        public void Setup(CaveEntranceSettings s, CaveSiteId site, int siteSeed, Vector2 sitePoint, DungeonProfile[] profiles, SizeClass caveSize, float caveDifficulty)
        {
            Site = site;
            HasSite = true;
            SitePoint = sitePoint;
            seed = siteSeed;
            profile = profiles != null && profiles.Length > 0 ? profiles[0] : null;
            variants = profiles != null && profiles.Length > 1 ? profiles : new DungeonProfile[0];
            manager = s.dungeonManager;
            size = caveSize;
            difficulty = Mathf.Max(0.1f, caveDifficulty);
            worldDifficulty = false;   // already in it
            dungeonOrigin = s.dungeonOrigin;
            sharing = s.sharing;
            maxPlayers = s.maxPlayers;
            bringPartyWithin = s.bringPartyWithin;
            width = s.mouthWidth;
            height = s.mouthHeight;
            depth = s.mouthDepth;
            rockColor = s.rockColor;
            useThemeMaterial = s.useThemeMaterial;
            fadeSeconds = s.fadeSeconds;
            sound = s.enterSound;
        }

        private void OnEnable()
        {
            if (!live.Contains(this))
                live.Add(this);
        }

        private void OnDisable() => live.Remove(this);

        private void Start()
        {
            if (!IsBuilt)
                Build();
        }

        /// <summary>(Re)builds the mouth, its way in and its trigger, fitted to the ground under it now.</summary>
        public void Build()
        {
            Clear();
            int s = EffectiveSeed;
            string name = EffectiveName;
            built = new GameObject("Cave Mouth") { layer = gameObject.layer };
            built.transform.SetParent(transform, false);

            CaveMouthBuilder.Meshes m = null;
            if (buildMouth)
            {
                var shape = new CaveMouthBuilder.Shape { Style = CaveMouthBuilder.Style.Overworld, Width = width, Height = height, Depth = depth, Seed = s };
                m = CaveMouthBuilder.Build(shape, GroundHeight);
                DungeonTheme theme = profile != null ? profile.theme : null;
                bool themed = useThemeMaterial && theme != null && theme.caveWall != null;
                Material rock = themed ? theme.caveWall : CaveMaterials.Flat(rockColor);
                Material dim = CaveMaterials.Flat(CaveMaterials.Shade(rockColor, 0.42f));
                Material dark = CaveMaterials.Flat(CaveMaterials.Shade(rockColor, 0.12f));
                Material curtain = CaveMaterials.Flat(Color.black);
                // Tangents (a themed, normal-mapped material) are made before the rock's collider is cooked.
                CaveMouthBuilder.Create(built.transform, m, rock, dim, dark, curtain, true, null, meshes, gameObject.layer, themed);
                // World creatures walk round the rock (the terrain's NavMesh was baked before the mouth existed).
                var obstacle = built.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = new Vector3(0f, height * 0.5f, -depth * 0.55f - 0.8f);
                obstacle.size = new Vector3(width + 2.6f, height + 1f, depth + 3.2f);
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
            }

            // The way in: a world portal at the tunnel's end (its trigger off - the passage sends players through it).
            float endFloor = m != null ? GroundHeight(0f, -depth) + 0.05f : 0f;
            var wayIn = new GameObject("Way In") { layer = gameObject.layer };
            wayIn.SetActive(false);
            wayIn.transform.SetParent(built.transform, false);
            wayIn.transform.localPosition = new Vector3(0f, Mathf.Max(0f, endFloor), buildMouth ? -depth * 0.92f : -1.6f);
            wayIn.transform.localRotation = Quaternion.identity;
            var portalTrigger = wayIn.AddComponent<BoxCollider>();
            portalTrigger.isTrigger = true;
            portalTrigger.center = new Vector3(0f, 1.4f, 0f);
            portalTrigger.size = new Vector3(1.6f, 2.6f, 0.6f);
            portalTrigger.enabled = false;
            portal = wayIn.AddComponent<Portal>();
            DungeonProfile first = profile != null ? profile : FirstVariant();
            portal.Configure(first, manager, size, difficulty, worldDifficulty);
            if (variants != null && variants.Length > 0)
                portal.SetRandomProfiles(variants);
            portal.SetAfterUse(PortalAfterUse.StaysOpen);
            portal.SetPlayers(sharing, maxPlayers, bringPartyWithin);
            portal.SetDungeonOrigin(dungeonOrigin);
            portal.RequestLabel = name;
            portal.OwnDungeonTypeOnly = true;
            // A shared visit is found by a key of this cave's own: the mouth is built again when its chunk comes back, and
            // the players arriving then still join the visit that is going. A spawned entrance's cave is seeded from its
            // site's planned point (where the mouth itself stands can never change which cave it leads to).
            string shareKey = sharing == PortalSharing.SharedRun ? "cave:" + s : null;
            Vector3? seedFrom = HasSite ? new Vector3(SitePoint.x, 0f, SitePoint.y) : (Vector3?)null;
            if (shareKey != null || seedFrom.HasValue)
                portal.AssignSpawnOptions(maxPlayers, PortalSharingOverride.UsePortalSetting, shareKey, 0, seedFrom);
            // Players come back out in front of the opening, on the ground, facing out - never inside the tunnel, its
            // trigger or its rock, wherever in the tunnel they stood when they went in.
            float outZ = buildMouth ? CaveMouthBuilder.ReturnZ(width) : 2.5f;
            var wayOut = new GameObject("Way Out Point") { layer = gameObject.layer };
            wayOut.transform.SetParent(built.transform, false);
            wayOut.transform.localPosition = new Vector3(0f, (buildMouth ? GroundHeight(0f, outZ) : 0f) + 0.1f, outZ);
            wayOut.transform.localRotation = Quaternion.identity;
            portal.ReturnPoint = wayOut.transform;
            wayIn.SetActive(true);
            // The world's cave memory counts play time from now on (what grows back counts the whole time played).
            CaveStore.Begin();

            // The trigger players walk into.
            var triggerGo = new GameObject("Passage Trigger") { layer = gameObject.layer };
            triggerGo.transform.SetParent(built.transform, false);
            var box = triggerGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            if (m != null)
            {
                triggerGo.transform.localPosition = m.TriggerCenter;
                box.size = m.TriggerSize;
            }
            else
            {
                triggerGo.transform.localPosition = new Vector3(0f, 1.6f, -0.8f);
                box.size = new Vector3(width * 0.72f, height * 0.9f, 1.6f);
            }
            passage = triggerGo.AddComponent<CavePassage>();
            passage.LeadTo(portal);
            passage.delay = fadeSeconds;
            passage.title = name;
            passage.subtitle = SizeWords(size);
            passage.sound = sound;
        }

        private DungeonProfile FirstVariant()
        {
            if (variants != null)
                foreach (DungeonProfile p in variants)
                    if (p != null)
                        return p;
            return null;
        }

        private static string SizeWords(SizeClass s)
        {
            switch (s)
            {
                case SizeClass.Small: return "Going into a shallow cave";
                case SizeClass.Large: return "Going down into a deep cave system";
                case SizeClass.Huge: return "Going down into the great depths";
                default: return "Going into the cave";
            }
        }

        /// <summary>Local ground height at local (x, z): the loaded terrain's, else flat.</summary>
        private float GroundHeight(float x, float z)
        {
            Vector3 world = transform.TransformPoint(new Vector3(x, 0f, z));
            if (LoadedTerrain.TryGetHeight(world.x, world.z, out float h))
                return Mathf.Clamp(h - transform.position.y, -6f, 6f);
            return 0f;
        }

        private void Clear()
        {
            foreach (Mesh mesh in meshes)
                if (mesh != null)
                    Destroy(mesh);
            meshes.Clear();
            if (built != null)
            {
                if (Application.isPlaying)
                    Destroy(built);
                else
                    DestroyImmediate(built);
            }
            built = null;
            portal = null;
            passage = null;
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes)
                if (mesh != null)
                    Destroy(mesh);
            meshes.Clear();
        }

        /// <summary>Problems that stop the entrance from working (empty = ready). Used by the inspector and the validator.</summary>
        public List<string> GetSetupProblems()
        {
            var problems = new List<string>();
            DungeonProfile p = profile != null ? profile : FirstVariant();
            if (p == null)
                problems.Add("No cave: set Profile to a cave profile (Tools > SimpleMovements > Caves > Create Cave Setup makes one).");
            else
            {
                int stages = CaveMiningStage.CountIn(p);
                if (stages == 0)
                    problems.Add($"Profile '{p.name}' has no Cave Mining Stage in its Custom Stages: no deposits, formations or natural passages.");
                else if (stages > 1)
                    problems.Add($"Profile '{p.name}' has {stages} Cave Mining Stages: only the first is used - put every resource in its Resource Table.");
            }
            if (dungeonOrigin.y > -1000f)
                problems.Add("Dungeon Origin is close to the world: keep it far below the terrain (e.g. y = -10000).");
            return problems;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.9f, 0.7f, 0.3f, 0.9f);
            Gizmos.DrawWireCube(new Vector3(0f, height * 0.5f, -depth * 0.5f), new Vector3(width, height, depth));
            // Out of the cave: where players come back.
            float outZ = buildMouth ? CaveMouthBuilder.ReturnZ(width) : 2.5f;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawLine(new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.2f, outZ));
            Gizmos.DrawWireSphere(new Vector3(0f, 0.2f, outZ), 0.2f);
        }
    }
}
