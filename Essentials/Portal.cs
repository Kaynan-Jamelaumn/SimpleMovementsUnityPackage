using System.Collections;
using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A world portal. When the player walks into its trigger collider it either sends them into a procedural dungeon
/// through <see cref="DungeonSession"/> (which pauses the world, builds the dungeon on a background thread, places
/// the player at the entrance, and brings them back in front of this portal when they leave - or if generation
/// fails), or loads a scene.
///
/// Setup (Tools > SimpleMovements > Dungeon > Create World Portal Prefab builds a ready one):
/// <list type="bullet">
/// <item>a Collider with Is Trigger on (added automatically at runtime when missing);</item>
/// <item>a Dungeon Manager prefab (made by Create Default Setup) and/or a Dungeon Profile;</item>
/// <item>a player tagged Player that moves with a CharacterController or has a Rigidbody (trigger events need one).</item>
/// </list>
/// The dungeon's seed comes from the world seed and this portal's position, so the same portal always leads to the
/// same dungeon (set Dungeon Seed to force one). Portals placed by the <see cref="PortalSpawner"/> also get their
/// site (so they can close after use) and a difficulty that grows with distance.
///
/// Players: <b>Max Players</b> limits how many can be in the portal's dungeon (1 = solo). With <b>Separate Runs</b>
/// every group gets a dungeon of its own; with <b>Shared Run</b> everyone entering while its dungeon is going joins it.
/// <b>Linked portals</b> (the same Link Id, or a spawner type with Linked Portals above 1) are several entrances to one
/// shared dungeon, each with its own arrival point if wanted - teams that cooperate or fight each other.
/// <b>After Use</b> decides whether the portal stays, closes once entered, or closes once its dungeon is completed.
/// </summary>
[DisallowMultipleComponent]
public class Portal : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("ON (recommended): build a procedural dungeon with the Dungeon Manager / Dungeon Profile below.\nOFF: load the scene named in Scene To Load instead.")]
    [SerializeField] private bool shouldInstantiateDungeon = true;

    [Tooltip("Scene mode only (Should Instantiate Dungeon off): name of the scene to load. It must be in Build Settings > Scenes In Build. If that scene has a DungeonManager with Generate On Start, the player is placed at its entrance.")]
    [SerializeField] private string sceneToLoad = null;

    [Tooltip("Scene mode only: where the player is put back when Scene To Load is empty or invalid.")]
    [SerializeField] private Vector3 position;

    [Header("Dungeon Configuration")]
    [Tooltip("RECOMMENDED. The Dungeon Manager prefab made by Tools > SimpleMovements > Dungeon > Create Default Setup (or a Dungeon Manager already in the scene). A prefab is instantiated at Dungeon Origin for each visit and destroyed afterwards. Empty = a manager is created from Dungeon Profile.")]
    [SerializeField] private DungeonManager dungeonManager;

    [Tooltip("Legacy: a prefab holding a DungeonManager (old dungeon prefabs without one fall back to Dungeon Profile). Leave empty for new setups.")]
    [SerializeField] private GameObject dungeonObject;

    [Tooltip("The kind of dungeon this portal leads to. Optional when the Dungeon Manager has a profile; set it to override that profile for this portal (e.g. a cave portal and a crypt portal sharing one manager). One of the two is REQUIRED.")]
    [SerializeField] private DungeonProfile dungeonProfile;

    [Tooltip("OPTIONAL. Several kinds of dungeon (e.g. a Crypt, a Deep Caves and a Fortress profile - Tools > SimpleMovements > " +
             "Dungeon > Create Dungeon Type): this portal leads to one of them, picked from its dungeon seed, so the same portal " +
             "always leads to the same kind and different portals to different kinds. Empty = Dungeon Profile.")]
    [SerializeField] private DungeonProfile[] randomProfiles = new DungeonProfile[0];

    [Tooltip("Classic mode only: where the dungeon is built - far away from the world so the two never overlap. Keep it well below the lowest terrain (default y = -10000). When dungeons are built in scenes of their own (DungeonScenes, used by the Hub World) each one gets its own scene and slot instead.")]
    [SerializeField] private Vector3 dungeonOrigin = new Vector3(0f, -10000f, 0f);

    [Tooltip("0 (recommended) = derived from the world seed and this portal's position, so each portal always leads to its own, stable dungeon. Any other value forces that seed.")]
    [SerializeField] private int dungeonSeed;

    [Tooltip("Size class of the dungeon: Small (1-2 floors of ~46 cells), Medium, Large or Huge (4-7 floors of ~112 cells). The profile's Size Classes define each one.")]
    [SerializeField] private SizeClass dungeonSize = SizeClass.Medium;

    [Tooltip("Difficulty of the dungeon (1 = normal): scales mob budgets and loot tiers. Replaced by the spawner's distance-based difficulty when Use Spawner Difficulty is on and this portal was placed by a PortalSpawner.")]
    [SerializeField, Min(0.1f)] private float dungeonDifficulty = 1f;

    [Tooltip("When placed by a PortalSpawner, use its difficulty (grows with distance from the world origin - see Portal Settings > Dungeon Difficulty) instead of Dungeon Difficulty.")]
    [SerializeField] private bool useSpawnerDifficulty = true;

    [Header("Player")]
    [Tooltip("Players are recognised by their Combat Entity (any character with a PlayerStatusController), so every " +
             "player of a multiplayer game can use the portal and the one who entered is the one sent. This tag is only a " +
             "fallback for player objects without a status controller (empty = no fallback).")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Party members of the entering player within this distance (metres) travel with them into the dungeon. " +
             "0 = only the player who entered.")]
    [SerializeField, Min(0f)] private float bringPartyWithin = 0f;

    [Tooltip("How far in front of the portal the player comes back (meters) - outside the trigger, so they aren't sent straight back in.")]
    [SerializeField, Min(0f)] private float returnDistance = 2.5f;

    [Tooltip("Most players in this portal's dungeon at once (0 = no limit, 1 = solo only). Separate Runs: the group going " +
             "in together is cut to it (the player who entered first, then the nearest party members). Shared Run and linked " +
             "portals: players are turned away while the dungeon is full. A spawner type's Max Players (above 0) replaces it.")]
    [SerializeField, Min(0)] private int maxPlayers;

    [Tooltip("Separate Runs: every group that enters gets a dungeon of its own. Shared Run: everyone who enters this portal " +
             "while its dungeon is going joins that same dungeon (up to Max Players) - to cooperate, or to fight each other " +
             "where PvP is on. The dungeon is free again once its last group left.")]
    [SerializeField] private PortalSharing sharing = PortalSharing.SeparateRuns;

    [Header("Linked Portals")]
    [Tooltip("Portals with the same Link Id (not empty) are entrances to ONE dungeon: whoever enters any of them while it " +
             "is going joins it (up to the Max Players of the portal entered first, whose dungeon it is). Use it for two " +
             "portals in different places that lead to the same dungeon. Portals placed by a spawner type with Linked " +
             "Portals above 1 are linked automatically.")]
    [SerializeField] private string linkId = "";

    [Tooltip("Where the players of this portal arrive: 0 = the dungeon's entrance; 1, 2... = other arrival points on the " +
             "first floor, far from the entrance and from each other. Two linked portals with 0 and 1 bring two teams to " +
             "opposite sides of the dungeon; both 0 = they start together.")]
    [SerializeField, Min(0)] private int arrivalPoint;

    [Header("After Use")]
    [Tooltip("What happens to this portal once used.\nUse Spawner Rule: a spawned portal follows its type / Portal " +
             "Settings; a hand-placed one stays open.\nStays Open: it stays.\nClose After Entering: it closes once its " +
             "dungeon can take nobody else - right after the first group for a private dungeon; when full (or over) for a " +
             "shared or linked one.\nClose After Completing: it closes when its dungeon is completed.")]
    [SerializeField] private PortalAfterUse afterUse = PortalAfterUse.UseSpawnerRule;

    [Tooltip("With an After Use of its own: seconds before the closed portal comes back (0 = never, for this session). A " +
             "hand-placed portal in a network session that reopens later stays visible on the clients while closed (it lets " +
             "nobody in); one that never reopens is removed for everyone.")]
    [SerializeField, Min(0f)] private float reopenAfter;

    [Header("Despawn")]
    [Tooltip("Seconds until the portal disappears by itself. 0 = it stays (recommended for spawned portals; the PortalSpawner removes it with its chunk). When a spawned portal disappears, its site reappears after its type's reappear delay.")]
    [SerializeField, Min(0f)] private float despawnTime;

    [Tooltip("Shortest random despawn time (seconds), with Should Have Random Despawn Time.")]
    [SerializeField, Min(0f)] private float minDespawnTime;

    [Tooltip("Longest random despawn time (seconds), with Should Have Random Despawn Time.")]
    [SerializeField, Min(0f)] private float maxDespawnTime;

    [Tooltip("Pick the despawn time between Min and Max Despawn Time instead of using Despawn Time.")]
    [SerializeField] private bool shouldHaveRandomDespawnTime;

    // Delegate and event to notify listeners when the portal is destroyed
    public delegate void PortalDestroyedHandler();
    public event PortalDestroyedHandler OnPortalDestroyed;

    private bool hasSite;
    private PortalSiteId site;
    private PortalUseRule useRule = PortalUseRule.StaysOpen;
    private float reopenAfterUse;
    // Set by the spawner: the type's player limit and sharing, and the link of a linked site's portals.
    private int typeMaxPlayers;
    private PortalSharingOverride typeSharing = PortalSharingOverride.UsePortalSetting;
    private string spawnLinkKey;
    private int spawnArrival;
    private bool hasSeedPosition;
    private Vector3 seedPosition;
    private string localKey;
    private bool closed;
    private readonly Dictionary<int, Closing> closings = new Dictionary<int, Closing>();
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private readonly List<Collider> hiddenColliders = new List<Collider>();
    private readonly List<Behaviour> hiddenBehaviours = new List<Behaviour>();
    private Coroutine reopening;
    private float lastRefusal = -100f;

    /// <summary>True when this portal was placed by a PortalSpawner (see <see cref="Site"/>).</summary>
    public bool HasSite => hasSite;
    /// <summary>The world site this portal stands on (valid when <see cref="HasSite"/>).</summary>
    public PortalSiteId Site => site;
    public DungeonProfile Profile => dungeonProfile;
    public DungeonManager Manager => dungeonManager;
    public bool LeadsToDungeon => shouldInstantiateDungeon;
    public float Difficulty => dungeonDifficulty;

    /// <summary>Most players in this portal's dungeon (0 = no limit): the spawner type's when it sets one, else this portal's.</summary>
    public int MaxPlayers => typeMaxPlayers > 0 ? typeMaxPlayers : maxPlayers;

    /// <summary>Whether players entering share one dungeon (linked portals always do).</summary>
    public PortalSharing Sharing
    {
        get
        {
            if (IsLinked)
                return PortalSharing.SharedRun;
            switch (typeSharing)
            {
                case PortalSharingOverride.SeparateRuns: return PortalSharing.SeparateRuns;
                case PortalSharingOverride.SharedRun: return PortalSharing.SharedRun;
                default: return sharing;
            }
        }
    }

    /// <summary>This portal is one of several entrances to one dungeon (Link Id, or a linked spawner type).</summary>
    public bool IsLinked => !string.IsNullOrEmpty(spawnLinkKey) || !string.IsNullOrWhiteSpace(linkId);

    /// <summary>Where this portal's players arrive in the dungeon (0 = its entrance, see <see cref="DungeonEntrances"/>).</summary>
    public int ArrivalPoint => !string.IsNullOrEmpty(spawnLinkKey) ? spawnArrival : arrivalPoint;

    /// <summary>
    /// The key of the dungeon run this portal's players share (see <see cref="DungeonEntryOptions.ShareKey"/>): the link
    /// for linked portals, the portal itself for a Shared Run, null when every group gets its own dungeon.
    /// </summary>
    public string ShareKey
    {
        get
        {
            if (!string.IsNullOrEmpty(spawnLinkKey))
                return spawnLinkKey;
            if (!string.IsNullOrWhiteSpace(linkId))
                return "link:" + linkId.Trim();
            if (Sharing != PortalSharing.SharedRun)
                return null;
            if (hasSite)
                return "site:" + site;
            if (localKey == null)
                localKey = "portal#" + (++portalCounter);
            return localKey;
        }
    }

    /// <summary>False while the portal is closed after use (it lets nobody in, and is hidden or gone).</summary>
    public bool IsOpen => !closed;

    /// <summary>A player was turned away (portal, player, reason) - e.g. its dungeon is full. Server side.</summary>
    public static event System.Action<Portal, GameObject, string> EntryRefused;

    private void Awake()
    {
        EnsureTrigger();
        StartCoroutine(ActivatePortal());
    }

    private void OnEnable()
    {
        if (!live.Contains(this))
            live.Add(this);
    }

    private void OnDisable() => live.Remove(this);

    /// <summary>Called by <see cref="PortalSpawner"/> right after spawning this portal.</summary>
    public void AssignSite(PortalSiteId siteId, float difficulty, PortalUseRule rule, float reopenAfter)
    {
        hasSite = true;
        site = siteId;
        useRule = rule;
        reopenAfterUse = reopenAfter;
        if (useSpawnerDifficulty)
            dungeonDifficulty = Mathf.Max(0.1f, difficulty);
    }

    /// <summary>
    /// Called by <see cref="PortalSpawner"/> after <see cref="AssignSite"/>: the type's player limit and sharing, and -
    /// for linked portals - the link they share (<paramref name="link"/>, null for a single portal), this portal's
    /// arrival point and where the dungeon's seed is taken from (the site's main portal, so every linked portal leads
    /// to the same dungeon whichever is entered first).
    /// </summary>
    public void AssignSpawnOptions(int typeMaxPlayers, PortalSharingOverride typeSharing, string link, int arrival, Vector3? seedFrom)
    {
        this.typeMaxPlayers = Mathf.Max(0, typeMaxPlayers);
        this.typeSharing = typeSharing;
        spawnLinkKey = string.IsNullOrEmpty(link) ? null : link;
        spawnArrival = Mathf.Max(0, arrival);
        hasSeedPosition = seedFrom.HasValue;
        seedPosition = seedFrom ?? Vector3.zero;
    }

    /// <summary>Handles the portal's lifespan by waiting for a specified time and then destroying it.</summary>
    private IEnumerator ActivatePortal()
    {
        float waitTime = shouldHaveRandomDespawnTime ? Random.Range(minDespawnTime, Mathf.Max(minDespawnTime, maxDespawnTime)) : despawnTime;
        if (waitTime <= 0f)
            yield break;   // 0 = the portal stays
        yield return new WaitForSeconds(waitTime);
        // The server decides when a portal closes (offline: this machine; the mode is read now, not when the portal
        // appeared); network-spawned portals despawn for everyone, a client never removes one by itself.
        if (!GameNet.NetAuthority.IsServer)
            yield break;
        GameNet.NetSpawn.Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only the server acts, for its copies of the players: a client's (predicted) player never starts a run.
        if (!GameNet.NetAuthority.IsServer)
            return;
        GameObject player = FindPlayer(other);
        if (player == null)
            return;

        if (shouldInstantiateDungeon)
        {
            if (closed)
                return;
            // A shared or linked portal whose dungeon is going: join it.
            string key = ShareKey;
            DungeonRun shared = DungeonSession.FindShared(key);
            if (shared != null)
            {
                if (!DungeonSession.CanJoin(shared, player))
                {
                    if (shared.IsFull && DungeonSession.RunOf(player) == null)
                        Refuse(player, $"this dungeon is full ({shared.MaxPlayers} players)");
                    return;
                }
                if (DungeonSession.Join(shared, Group(player), ReturnPose(player.transform), ArrivalPoint) > 0)
                    OnEntered(shared);
                return;
            }

            // This player only: other groups may be in dungeons of their own (see DungeonScenes).
            if (!DungeonSession.CanEnterFor(player))
                return;
            DungeonManager manager = ResolveManager();
            DungeonRequest request = BuildRequest();
            Vector3 typeAt = hasSeedPosition ? seedPosition : transform.position;
            DungeonProfile profile = WorldSpawnHooks.ApplyDungeonType(typeAt, request.seed, PickProfile(request.seed));
            if (profile == null && (manager == null || manager.profile == null))
            {
                Debug.LogError($"Portal '{name}': no dungeon to build - assign a Dungeon Manager prefab (with a profile) or a Dungeon Profile on the Portal component.", this);
                return;
            }
            var options = new DungeonEntryOptions { ShareKey = key, MaxPlayers = MaxPlayers, Entrance = ArrivalPoint };
            DungeonRun run = DungeonSession.Enter(Group(player), request, manager, profile, dungeonOrigin, ReturnPose(player.transform), options);
            if (run != null && run.State != DungeonRunState.Ended)
                OnEntered(run);
            return;
        }

        // Scene loading (single player only: a network session moves players between scenes through the network).
        if (GameNet.NetAuthority.IsNetworked && !string.IsNullOrEmpty(sceneToLoad))
        {
            if (!warnedSceneLoad)
            {
                warnedSceneLoad = true;
                Debug.LogWarning($"Portal '{name}': 'Scene To Load' portals only work in single player (a network session cannot replace every player's scene). Use a dungeon portal or a realm gate.", this);
            }
            return;
        }
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning($"Portal '{name}': Scene To Load is empty - putting the player at Position.", this);
            Teleport(player, position, player.transform.rotation);
            return;
        }
        SetControls(player, false);
        DontDestroyOnLoad(player);
        sceneTraveller = player;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene(sceneToLoad);
    }

    // ------------------------------------------------------------------ closing after use

    /// <summary>The player carried through the last scene load (placed in the new scene).</summary>
    private static GameObject sceneTraveller;
    private static bool warnedSceneLoad;
    private static int portalCounter;
    private static readonly List<GameObject> groupBuffer = new List<GameObject>(4);
    private static readonly List<CombatEntity> partyBuffer = new List<CombatEntity>(4);
    private static readonly List<Portal> live = new List<Portal>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sceneTraveller = null;
        warnedSceneLoad = false;
        portalCounter = 0;
        live.Clear();
        EntryRefused = null;
    }

    /// <summary>The player who entered, then the party members travelling with them (Bring Party Within).</summary>
    private List<GameObject> Group(GameObject player)
    {
        groupBuffer.Clear();
        groupBuffer.Add(player);
        CombatEntity entity = PlayerLocator.FromObject(player);
        if (entity == null || bringPartyWithin <= 0f)
            return groupBuffer;
        PlayerLocator.PartyMembersNear(entity, bringPartyWithin, partyBuffer);
        for (int i = 1; i < partyBuffer.Count; i++)
        {
            GameObject root = PlayerLocator.MovableRoot(partyBuffer[i]);
            if (root != null && !groupBuffer.Contains(root))
                groupBuffer.Add(root);
        }
        return groupBuffer;
    }

    /// <summary>The rule this portal follows after use, and its reopen delay (seconds, 0 = never).</summary>
    public PortalUseRule EffectiveUseRule(out float reopenSeconds)
    {
        switch (afterUse)
        {
            case PortalAfterUse.StaysOpen:
                reopenSeconds = reopenAfter;
                return PortalUseRule.StaysOpen;
            case PortalAfterUse.CloseAfterEntering:
                reopenSeconds = reopenAfter;
                return PortalUseRule.CloseAfterEntering;
            case PortalAfterUse.CloseAfterCompleting:
                reopenSeconds = reopenAfter;
                return PortalUseRule.CloseAfterCompleting;
            default:
                reopenSeconds = hasSite ? reopenAfterUse : 0f;
                return hasSite ? useRule : PortalUseRule.StaysOpen;
        }
    }

    // A closing waiting for its run (once per run, however many players joined it through this portal).
    private sealed class Closing
    {
        public bool Done;
    }

    private void OnEntered(DungeonRun run)
    {
        PortalUseRule rule = EffectiveUseRule(out float reopenSeconds);
        if (rule == PortalUseRule.StaysOpen)
            return;
        float reopen = reopenSeconds > 0f ? reopenSeconds : -1f;
        // This portal may be gone by the time the run ends (its chunk unloaded): the closing remembers what to close.
        bool siteClose = hasSite;
        PortalSiteId closingSite = site;
        string link = IsLinked ? ShareKey : null;
        Portal self = this;
        if (!closings.TryGetValue(run.Id, out Closing closing))
        {
            closing = new Closing();
            closings[run.Id] = closing;
            Dictionary<int, Closing> table = closings;
            bool onlyWhenCompleted = rule == PortalUseRule.CloseAfterCompleting;
            run.Ended += (r, how) =>
            {
                table.Remove(r.Id);
                if (closing.Done || how == DungeonRunEnd.Failed || (onlyWhenCompleted && how != DungeonRunEnd.Completed))
                    return;
                closing.Done = true;
                Close(siteClose, closingSite, link, self, reopen);
            };
        }
        // Close After Entering: a private dungeon closes the portal at once, a shared or linked one once it is full
        // (or, above, when it is over).
        if (rule == PortalUseRule.CloseAfterEntering && !closing.Done && (run.ShareKey == null || run.IsFull))
        {
            closing.Done = true;
            Close(siteClose, closingSite, link, self, reopen);
        }
    }

    /// <summary>
    /// Closes a used portal: a spawned one's site (its spawner removes the portal - and its linked portals - and
    /// remembers it until it reopens); a hand-placed one (with the hand-placed portals linked to it) is hidden, or
    /// removed for good in a network session when it never reopens.
    /// </summary>
    private static void Close(bool siteClose, PortalSiteId closingSite, string link, Portal portal, float reopen)
    {
        if (siteClose)
        {
            WorldSpawnRegistry.ClosePortalSite(closingSite, reopen);
            return;
        }
        if (portal != null)
            portal.CloseHere(reopen);
        if (string.IsNullOrEmpty(link))
            return;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            Portal other = live[i];
            if (other != null && other != portal && !other.hasSite && other.IsLinked && other.ShareKey == link)
                other.CloseHere(reopen);
        }
    }

    /// <summary>Closes this portal (server; offline: this machine): nobody can enter until <see cref="Open"/>.</summary>
    /// <param name="reopenAfterSeconds">Opens again after this many seconds (0 or less = stays closed).</param>
    public void Close(float reopenAfterSeconds = 0f)
    {
        if (hasSite)
            WorldSpawnRegistry.ClosePortalSite(site, reopenAfterSeconds > 0f ? reopenAfterSeconds : -1f);
        else
            CloseHere(reopenAfterSeconds);
    }

    private void CloseHere(float reopen)
    {
        if (this == null || closed)
            return;
        closed = true;
        // Networked and closed for good: removed for every player. Otherwise hidden (in a network session only on the
        // server - its clients keep seeing it, but it lets nobody in).
        if (reopen <= 0f && GameNet.NetAuthority.IsNetworked && GameNet.NetAuthority.IsNetworkedObject(gameObject))
        {
            GameNet.NetSpawn.Destroy(gameObject);
            return;
        }
        SetHidden(true);
        if (reopen > 0f && isActiveAndEnabled)
            reopening = StartCoroutine(ReopenAfter(reopen));
    }

    private IEnumerator ReopenAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        reopening = null;
        Open();
    }

    /// <summary>Opens a closed portal again (a spawned one's site reopens).</summary>
    public void Open()
    {
        if (hasSite)
        {
            WorldSpawnRegistry.OpenPortalSite(site);
            return;
        }
        if (reopening != null)
        {
            StopCoroutine(reopening);
            reopening = null;
        }
        if (!closed)
            return;
        closed = false;
        SetHidden(false);
    }

    // Hides the portal without switching its object off (so it can open again by itself): renderers, colliders
    // (its trigger included), lights and sounds.
    private void SetHidden(bool hide)
    {
        if (hide)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }
            foreach (Collider c in GetComponentsInChildren<Collider>())
                if (c.enabled) { c.enabled = false; hiddenColliders.Add(c); }
            foreach (Light l in GetComponentsInChildren<Light>())
                if (l.enabled) { l.enabled = false; hiddenBehaviours.Add(l); }
            foreach (AudioSource a in GetComponentsInChildren<AudioSource>())
                if (a.enabled) { a.enabled = false; hiddenBehaviours.Add(a); }
            return;
        }
        foreach (Renderer r in hiddenRenderers)
            if (r != null) r.enabled = true;
        foreach (Collider c in hiddenColliders)
            if (c != null) c.enabled = true;
        foreach (Behaviour b in hiddenBehaviours)
            if (b != null) b.enabled = true;
        hiddenRenderers.Clear();
        hiddenColliders.Clear();
        hiddenBehaviours.Clear();
    }

    private void Refuse(GameObject player, string reason)
    {
        if (Time.time - lastRefusal < 3f)
            return;
        lastRefusal = Time.time;
        Debug.Log($"Portal '{name}': {player.name} cannot enter - {reason}.", this);
        EntryRefused?.Invoke(this, player, reason);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// The player object behind a collider: the player's Combat Entity (any player, multiplayer-safe), moved through
    /// the object with its CharacterController; the tagged ancestor only as a fallback.
    /// </summary>
    private GameObject FindPlayer(Collider other)
    {
        if (other == null)
            return null;
        CombatEntity entity = PlayerLocator.FromCollider(other);
        if (entity != null)
            return PlayerLocator.MovableRoot(entity);
        if (string.IsNullOrEmpty(playerTag))
            return null;
        Transform tagged = null;
        for (Transform t = other.transform; t != null; t = t.parent)
        {
            if (HasTag(t.gameObject))
                tagged = t;
        }
        if (tagged == null)
            return null;
        // The object that moves the player: prefer the one with the CharacterController, else the tagged root.
        CharacterController cc = other.GetComponentInParent<CharacterController>();
        if (cc != null && (cc.transform == tagged || cc.transform.IsChildOf(tagged) || tagged.IsChildOf(cc.transform)))
            return cc.gameObject;
        return tagged.gameObject;
    }

    private bool HasTag(GameObject go)
    {
        try
        {
            return !string.IsNullOrEmpty(playerTag) && go.CompareTag(playerTag);
        }
        catch (UnityException)
        {
            return false;   // tag not defined in this project
        }
    }

    /// <summary>Adds a trigger box around the renderers when the portal has no trigger collider (the player could never enter it otherwise).</summary>
    private void EnsureTrigger()
    {
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
            if (c.isTrigger)
                return;

        var box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            Vector3 s = transform.lossyScale;
            box.center = transform.InverseTransformPoint(b.center);
            box.size = new Vector3(b.size.x / Mathf.Max(0.001f, Mathf.Abs(s.x)), b.size.y / Mathf.Max(0.001f, Mathf.Abs(s.y)), b.size.z / Mathf.Max(0.001f, Mathf.Abs(s.z)));
        }
        else
        {
            box.center = new Vector3(0f, 1.5f, 0f);
            box.size = new Vector3(2f, 3f, 2f);
        }
        Debug.LogWarning($"Portal '{name}' had no trigger collider - added a trigger box around it. Add a Collider with Is Trigger on to the prefab to control its shape.", this);
    }

    /// <summary>Setup problems that stop this portal from working (empty = ready). Used by the inspector.</summary>
    public List<string> GetSetupProblems()
    {
        var problems = new List<string>();
        bool trigger = false;
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
            trigger |= c.isTrigger;
        if (!trigger)
            problems.Add("No trigger collider: add a Collider (Box/Capsule) with Is Trigger on - one is added at runtime, but its size is only a guess.");
        if (shouldInstantiateDungeon)
        {
            DungeonManager manager = ResolveManager();
            if (dungeonProfile == null && (manager == null || manager.profile == null) && PickProfile(1) == null)
                problems.Add("No dungeon: assign the Dungeon Manager prefab (with a profile) and/or a Dungeon Profile.");
            if (dungeonOrigin.y > -1000f)
                problems.Add("Dungeon Origin is close to the world: keep it far below the terrain (e.g. y = -10000). " +
                             "(Ignored when dungeons are built in scenes of their own - see DungeonScenes / the Hub World.)");
        }
        else if (string.IsNullOrEmpty(sceneToLoad))
        {
            problems.Add("Scene mode: Scene To Load is empty.");
        }
        if (!shouldInstantiateDungeon && (!string.IsNullOrWhiteSpace(linkId) || sharing == PortalSharing.SharedRun || maxPlayers > 0))
            problems.Add("Max Players, Shared Run and Link Id only work for dungeon portals (Should Instantiate Dungeon on).");
        return problems;
    }

    /// <summary>Where the player comes back: a step in front of the portal (not inside its trigger), facing away from it.</summary>
    private Pose ReturnPose(Transform player)
    {
        Vector3 away = player.position - transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
            away = -player.forward;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = transform.forward;
        away.Normalize();
        Vector3 back = player.position + away * returnDistance;
        if (LoadedTerrain.TryGetHeight(back.x, back.z, out float ground))
            back.y = Mathf.Max(back.y, ground + 0.1f);
        return new Pose(back, Quaternion.LookRotation(away, Vector3.up));
    }

    private DungeonManager ResolveManager()
    {
        if (dungeonManager != null)
            return dungeonManager;
        if (dungeonObject != null)
        {
            DungeonManager fromObject = dungeonObject.GetComponent<DungeonManager>();
            if (fromObject != null)
                return fromObject;
        }
        return null;
    }

    /// <summary>The kind of dungeon for a seed: one of Random Profiles (stable per seed), else Dungeon Profile.</summary>
    public DungeonProfile PickProfile(int seed)
    {
        int count = 0;
        if (randomProfiles != null)
            foreach (DungeonProfile p in randomProfiles)
                if (p != null)
                    count++;
        if (count == 0)
            return dungeonProfile;
        int pick = (int)(PlacementRandom.Hash(seed, PlacementRandom.StableHash("PortalProfile"), 0, 0, 0) % (uint)count);
        foreach (DungeonProfile p in randomProfiles)
        {
            if (p == null)
                continue;
            if (pick-- == 0)
                return p;
        }
        return dungeonProfile;
    }

    private DungeonRequest BuildRequest()
    {
        int worldSeed = 0;
        TerrainGenerator terrain = FindAnyObjectByType<TerrainGenerator>();
        if (terrain != null)
            worldSeed = terrain.VoronoiSeed;
        // Linked spawned portals all take the seed from their site's main portal: the same dungeon whichever is entered first.
        Vector3 from = hasSeedPosition ? seedPosition : transform.position;
        DungeonRequest request = dungeonSeed != 0
            ? new DungeonRequest { seed = dungeonSeed }
            : DungeonRequest.FromWorldPosition(worldSeed, from);
        request.size = dungeonSize;
        // A hand-placed portal follows the game's difficulty rule too (a spawned one already got it from its spawner).
        request.difficulty = useSpawnerDifficulty && !hasSite
            ? WorldSpawnHooks.ApplyPortalDifficulty(from, dungeonDifficulty)
            : dungeonDifficulty;
        request.label = $"Dungeon ({name})";
        return request;
    }

    /// <summary>A scene with a DungeonManager (Generate On Start): place the player when it's ready.</summary>
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        // The player who went through the portal (kept with DontDestroyOnLoad), not "whoever has the Player tag".
        GameObject player = sceneTraveller;
        sceneTraveller = null;
        if (player == null)
            return;
        DungeonManager manager = FindAnyObjectByType<DungeonManager>();
        if (manager == null)
        {
            SetControls(player, true);
            return;
        }
        if (manager.Current != null)
        {
            Place(player, manager.Current);
            return;
        }
        void Ready(DungeonInstance dungeon)
        {
            manager.Ready -= Ready;
            Place(player, dungeon);
        }
        manager.Ready += Ready;
    }

    private static void Place(GameObject player, DungeonInstance dungeon)
    {
        Pose spawn = dungeon.PlayerSpawn;
        Teleport(player, spawn.position + Vector3.up * DungeonSession.SpawnLift, spawn.rotation);
        SetControls(player, true);
    }

    private static void Teleport(GameObject player, Vector3 to, Quaternion rotation)
    {
        var cc = player.GetComponent<CharacterController>();
        bool was = cc != null && cc.enabled;
        if (cc != null)
            cc.enabled = false;
        player.transform.SetPositionAndRotation(to, rotation);
        Physics.SyncTransforms();
        if (cc != null)
            cc.enabled = was;
        // A predicted (networked) movement snaps instead of smoothing across the jump.
        if (GameNet.NetAuthority.IsNetworked)
        {
            MovementStateMachine movement = player.GetComponentInChildren<MovementStateMachine>(true);
            if (movement != null)
                movement.NotifyTeleported();
        }
    }

    private static void SetControls(GameObject player, bool on)
    {
        var movement = player.GetComponent<PlayerMovementController>();
        if (movement != null)
            movement.enabled = on;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = on;
    }

    private void OnDrawGizmosSelected()
    {
        // Where the player comes back when approaching from the front.
        Gizmos.color = new Color(0.6f, 0.3f, 1f);
        Vector3 back = transform.position + transform.forward * returnDistance;
        Gizmos.DrawLine(transform.position, back);
        Gizmos.DrawWireSphere(back, 0.3f);
    }

    private void OnDestroy()
    {
        OnPortalDestroyed?.Invoke();
    }
}
