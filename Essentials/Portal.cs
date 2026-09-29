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

    [Tooltip("Where the dungeon is built: far away from the world so the two never overlap. Keep it well below the lowest terrain (default y = -10000).")]
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
    [Tooltip("Tag of the player. The collider that enters may be on a child object: the tagged object (or the one with the CharacterController) is the one moved.")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("How far in front of the portal the player comes back (meters) - outside the trigger, so they aren't sent straight back in.")]
    [SerializeField, Min(0f)] private float returnDistance = 2.5f;

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

    /// <summary>True when this portal was placed by a PortalSpawner (see <see cref="Site"/>).</summary>
    public bool HasSite => hasSite;
    /// <summary>The world site this portal stands on (valid when <see cref="HasSite"/>).</summary>
    public PortalSiteId Site => site;
    public DungeonProfile Profile => dungeonProfile;
    public DungeonManager Manager => dungeonManager;
    public bool LeadsToDungeon => shouldInstantiateDungeon;
    public float Difficulty => dungeonDifficulty;

    private void Awake()
    {
        EnsureTrigger();
        StartCoroutine(ActivatePortal());
    }

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

    /// <summary>Handles the portal's lifespan by waiting for a specified time and then destroying it.</summary>
    private IEnumerator ActivatePortal()
    {
        float waitTime = shouldHaveRandomDespawnTime ? Random.Range(minDespawnTime, Mathf.Max(minDespawnTime, maxDespawnTime)) : despawnTime;
        if (waitTime <= 0f)
            yield break;   // 0 = the portal stays
        yield return new WaitForSeconds(waitTime);
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        GameObject player = FindPlayer(other);
        if (player == null)
            return;

        if (shouldInstantiateDungeon)
        {
            if (DungeonSession.InDungeon || DungeonSession.IsEntering || !DungeonSession.CanEnter)
                return;
            DungeonManager manager = ResolveManager();
            if (dungeonProfile == null && (manager == null || manager.profile == null))
            {
                Debug.LogError($"Portal '{name}': no dungeon to build - assign a Dungeon Manager prefab (with a profile) or a Dungeon Profile on the Portal component.", this);
                return;
            }
            DungeonSession.Enter(player, BuildRequest(), manager, dungeonProfile, dungeonOrigin, ReturnPose(player.transform));
            if (DungeonSession.IsEntering)
                OnEntered();
            return;
        }

        // Scene loading.
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning($"Portal '{name}': Scene To Load is empty - putting the player at Position.", this);
            Teleport(player, position, player.transform.rotation);
            return;
        }
        SetControls(player, false);
        DontDestroyOnLoad(player);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene(sceneToLoad);
    }

    // ------------------------------------------------------------------ closing after use

    private static bool pendingCompletion;
    private static PortalSiteId pendingSite;
    private static float pendingReopen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pendingCompletion = false;
    }

    private void OnEntered()
    {
        if (!hasSite)
            return;
        float reopen = reopenAfterUse > 0f ? reopenAfterUse : -1f;
        if (useRule == PortalUseRule.CloseAfterEntering)
        {
            WorldSpawnRegistry.ClosePortalSite(site, reopen);
        }
        else if (useRule == PortalUseRule.CloseAfterCompleting)
        {
            // This portal may be gone by the time the dungeon is finished: remember the site statically.
            pendingCompletion = true;
            pendingSite = site;
            pendingReopen = reopen;
            DungeonSession.Exited -= OnDungeonExited;
            DungeonSession.Exited += OnDungeonExited;
            DungeonSession.Failed -= OnDungeonFailed;
            DungeonSession.Failed += OnDungeonFailed;
        }
    }

    private static void OnDungeonExited(bool completed)
    {
        DungeonSession.Exited -= OnDungeonExited;
        DungeonSession.Failed -= OnDungeonFailed;
        if (pendingCompletion && completed)
            WorldSpawnRegistry.ClosePortalSite(pendingSite, pendingReopen);
        pendingCompletion = false;
    }

    private static void OnDungeonFailed(string message)
    {
        DungeonSession.Exited -= OnDungeonExited;
        DungeonSession.Failed -= OnDungeonFailed;
        pendingCompletion = false;
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>The player object behind a collider: the tagged ancestor, or the one with the CharacterController.</summary>
    private GameObject FindPlayer(Collider other)
    {
        if (other == null)
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
            return go.CompareTag(string.IsNullOrEmpty(playerTag) ? "Player" : playerTag);
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
            if (dungeonProfile == null && (manager == null || manager.profile == null))
                problems.Add("No dungeon: assign the Dungeon Manager prefab (with a profile) and/or a Dungeon Profile.");
            if (dungeonOrigin.y > -1000f)
                problems.Add("Dungeon Origin is close to the world: keep it far below the terrain (e.g. y = -10000).");
        }
        else if (string.IsNullOrEmpty(sceneToLoad))
        {
            problems.Add("Scene mode: Scene To Load is empty.");
        }
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

    private DungeonRequest BuildRequest()
    {
        int worldSeed = 0;
        TerrainGenerator terrain = FindAnyObjectByType<TerrainGenerator>();
        if (terrain != null)
            worldSeed = terrain.VoronoiSeed;
        DungeonRequest request = dungeonSeed != 0
            ? new DungeonRequest { seed = dungeonSeed }
            : DungeonRequest.FromWorldPosition(worldSeed, transform.position);
        request.size = dungeonSize;
        request.difficulty = dungeonDifficulty;
        request.label = $"Dungeon ({name})";
        return request;
    }

    /// <summary>A scene with a DungeonManager (Generate On Start): place the player when it's ready.</summary>
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
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
