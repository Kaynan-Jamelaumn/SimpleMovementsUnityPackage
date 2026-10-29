using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The network side of a harvestable creature (<c>GameNet.NetCarcass</c>; null offline, where the creature runs
/// everything itself). The server tracks the body and resolves every harvest; clients keep a copy of the carcass and
/// send what their player does.
/// </summary>
public interface IHarvestNet
{
    /// <summary>True where the harvesting rules run (the server).</summary>
    bool RunsRules { get; }
    /// <summary>Client: the player starts working a material (its pieces; empty = all). The server answers with its plan.</summary>
    void SendBegin(string material, List<int> units);
    /// <summary>Client: how the work went.</summary>
    void SendSubmit(HarvestTaskResult result);
    /// <summary>Client: the player stopped working a material without finishing it.</summary>
    void SendCancel(string material);
    /// <summary>Client: the player hacks the hide off to reach the meat.</summary>
    void SendStrip();
    /// <summary>Client: the player let go of everything they were working on here (the window closed).</summary>
    void SendRelease();
    /// <summary>Server: the carcass changed (send it to every client).</summary>
    void CarcassChanged();
    /// <summary>Server: the plan of the work a remote player started (what their hands play, and what it is checked against).</summary>
    void SendPlan(CombatEntity player, HarvestPlan plan);
    /// <summary>Server: what a harvest gave a remote player (or why it was refused).</summary>
    void SendReport(CombatEntity player, HarvestReport report);
    /// <summary>Server: a remote player's skills (after a harvest, when the window opens).</summary>
    void SendSkills(CombatEntity player, string json);
    /// <summary>Server: a message for a remote player.</summary>
    void Tell(CombatEntity player, string text);
    /// <summary>Server: opens the carcass window on a remote player's screen; false when the player cannot be reached.</summary>
    bool Open(CombatEntity player);
}

/// <summary>
/// A creature whose body can be harvested. While it lives, every hit marks its body where it landed and by what dealt
/// it (<see cref="HarvestCondition"/>): a slash cuts the hide and the tendons there, an arrow through the chest ruins
/// the heart, a mace breaks the skull and cracks the teeth, fire burns the pelt, poison spoils the meat, an explosion
/// tears up everything. When it dies its body stays as a carcass (<see cref="HarvestCarcass"/>) that players open
/// (Interact) to inspect it and take what they want, each material with its own hands-on work
/// (<see cref="HarvestWindow"/>) - while it spoils, faster in the heat. Empty or left alone long enough, it is removed.
/// <list type="bullet">
/// <item><b>Setup</b>: add it to a mob and name its anatomy (or let the Harvest Database find it from the mob's type
/// or name). With the database's Auto Attach on, mobs whose type matches a creature become harvestable by themselves,
/// with body parts (weak points, where hits land) made from the anatomy when they have none.</item>
/// <item><b>Rules</b>: hits, the carcass and every harvest are worked out where the rules run (offline, or the server);
/// the materials go into the player's inventory (or onto the ground), skills grow, tools wear.</item>
/// <item><b>Network sessions</b>: with a <c>NetCarcass</c> on the mob's prefab, clients see the carcass and harvest it
/// through the server.</item>
/// </list>
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-20)]
[AddComponentMenu("SimpleMovements/Harvesting/Harvestable Creature")]
public class HarvestableCreature : MonoBehaviour, IInteractable, IDeathRemovalHandler, IDamageInterceptor, IMobLootProvider
{
    public enum AgeMode { Random, Young, Adult, Old, Ancient }

    [Header("Creature")]
    [Tooltip("Its body and what it gives. Empty = the Harvest Database's creature named by Anatomy Id, else the one for this mob (its Mob " +
             "Profile, its type or its name).")]
    [SerializeField] private CreatureAnatomy anatomy;
    [Tooltip("A creature of the Harvest Database by id (bear, wolf, deer...) when no asset is assigned. Empty = found from the mob.")]
    [SerializeField] private string anatomyId = "";
    [Tooltip("Its age: young ones give tender meat and small trophies, old ones tougher meat, bigger horns and more rare finds.")]
    [SerializeField] private AgeMode age = AgeMode.Random;
    [Tooltip("Its size against the anatomy's typical adult (yields scale with it). 0 = from its height.")]
    [SerializeField, Min(0f)] private float sizeScale = 0f;
    [Tooltip("Rare finds (a bezoar, a heartstone) are this much likelier in it.")]
    [SerializeField, Min(0f)] private float rareFinds = 1f;
    [Tooltip("An elite: finer materials and more rare finds (automatic for dungeon champions and bosses).")]
    [SerializeField] private bool elite;

    [Header("Carcass")]
    [Tooltip("Minutes its carcass stays when nobody harvests it. 0 = the Harvest Database's.")]
    [SerializeField, Min(0f)] private float carcassMinutes = 0f;
    [Tooltip("Where the prompt shows and the range is measured from. Empty = the middle of the body.")]
    [SerializeField] private Transform interactionPoint;
    [Tooltip("The Item Drop Manager's Mob tables drop nothing for it: what it gives is harvested (its own Item Drop On Death still " +
             "drops). Needs the Harvest Database's Skip Generic Mob Drops too.")]
    [SerializeField] private bool replacesTableDrops = true;
    [Tooltip("A body that is a carcass from the start, with no mob needed (practice, a hunter's camp, trying the minigames): it does not " +
             "spoil away, and a fresh one is laid out once it has been emptied. Needs an anatomy (asset or id).")]
    [SerializeField] private bool practiceCarcass;
    [Tooltip("Practice carcass: the wounds it comes with (a cut, an arrow, a blow, a burn... on random parts of the body).")]
    [SerializeField, Range(0, 8)] private int practiceWounds = 2;
    [Tooltip("Log hits and harvests (debug).")]
    [SerializeField] private bool debugLog;

    private static readonly List<HarvestableCreature> all = new List<HarvestableCreature>();

    /// <summary>Every harvestable creature (alive or a carcass).</summary>
    public static IReadOnlyList<HarvestableCreature> All => all;

    /// <summary>
    /// Where the rules run (offline, the server): a harvest gave a player (any player, local or remote) what the report
    /// says - for quests, achievements and logs.
    /// </summary>
    public static event Action<HarvestableCreature, CombatEntity, HarvestReport> AnyHarvested;

    /// <summary>The carcass changed (a harvest, spoiling, a lock), or the creature died.</summary>
    public event Action Changed;
    /// <summary>A harvest's report for a player on this machine (the window shows it).</summary>
    public event Action<CombatEntity, HarvestReport> Harvested;
    /// <summary>The server's plan of the work a player on this network client started (the window plays it).</summary>
    public event Action<CombatEntity, HarvestPlan> Planned;

    /// <summary>The network side (set by <c>NetCarcass</c>).</summary>
    public IHarvestNet Net { get; set; }

    /// <summary>True where the rules run (offline, or the server).</summary>
    public bool RulesHere => Net != null ? Net.RunsRules : !GameNet.NetAuthority.IsNetworked || GameNet.NetAuthority.IsServer;

    private struct Pending
    {
        public int material;
        public HarvestPlan plan;
        /// <summary>When the work began (unscaled seconds, this machine's clock).</summary>
        public double began;
    }

    private CombatEntity entity;
    private HarvestAnatomyData data;
    private HarvestCondition condition;
    private HarvestCarcass carcass;
    private int seed;
    private GameObject carcassObject;
    private BoxCollider trigger;
    private float tickBuffer, temperatureTimer, regenTimer, syncTimer, practiceTimer;
    private float temperature = float.NaN;
    private bool removing;
    private float pendingAmount;
    private int pendingFrame = -1;
    private readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();
    private int version;
    private float bodyBack = -1f, bodyFront = 1f;

    // ------------------------------------------------------------------ what it is
    public HarvestSettings Settings => HarvestDatabase.Instance.Settings;
    /// <summary>Its anatomy (null when none could be found: it is then not harvestable).</summary>
    public HarvestAnatomyData Anatomy => data;
    /// <summary>The carcass (null while it lives).</summary>
    public HarvestCarcass Carcass => carcass;
    public bool IsCarcass => carcass != null;
    /// <summary>The state of its body (alive: what the fight did so far; dead: the carcass's).</summary>
    public HarvestCondition Condition => carcass != null ? carcass.condition : condition;
    public CombatEntity Entity => entity;
    /// <summary>Changes with every change of the carcass (windows redraw on it).</summary>
    public int Version => version;
    public float CarcassMinutes => carcassMinutes > 0f ? carcassMinutes : Settings.carcassMinutes;

    /// <summary>Minutes until the carcass is removed if nobody works on it (∞ while it lives, and for a practice carcass).</summary>
    public double MinutesLeft
    {
        get
        {
            if (carcass == null || practiceCarcass)
                return double.PositiveInfinity;
            double end = CarcassMinutes * 60.0;
            if (carcass.lastWork >= 0)
                end = Math.Max(end, carcass.lastWork + Settings.keepAfterWorkMinutes * 60.0);
            return Math.Max(0, (end - carcass.age) / 60.0);
        }
    }

    /// <summary>How a player is known in locks and plans: their player id (in a network session the same on every machine).</summary>
    public static int PlayerKey(CombatEntity player) => player != null ? HubWorld.PlayerIds.Of(player) : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all.Clear();
        AnyHarvested = null;
    }

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        entity = CombatEntity.Resolve(gameObject);
        if (entity != null)
            ResolveAnatomy();
    }

    private void OnEnable()
    {
        if (!all.Contains(this))
            all.Add(this);
        Hook();
    }

    private void OnDisable()
    {
        all.Remove(this);
        Unhook();
    }

    private void Start()
    {
        // The combat entity may have been added after this woke (a status controller adds it in its own Awake).
        Hook();
        if (data == null && (entity != null || practiceCarcass))
            ResolveAnatomy();
        if (data == null)
            return;
        if (entity != null)
        {
            BodyPartController.MeasureLength(entity.transform, entity.Radius, out bodyBack, out bodyFront);
            EnsureBodyParts();
        }
        if (practiceCarcass && carcass == null && RulesHere)
            LayOutPracticeBody();
    }

    /// <summary>Is it a practice carcass (a carcass from the start, laid out again when emptied)?</summary>
    public bool IsPractice => practiceCarcass;

    /// <summary>A practice carcass: a fresh body with its wounds (where the rules run).</summary>
    private void LayOutPracticeBody()
    {
        seed = 0;
        condition = NewCondition();
        var blows = new (DamageForm form, ElementType element, string source)[]
        {
            (DamageForm.Slash, ElementType.None, "Sword"), (DamageForm.Pierce, ElementType.None, "Arrow"), (DamageForm.Blunt, ElementType.None, "Mace"),
            (DamageForm.None, ElementType.Fire, "Fireball"), (DamageForm.Claw, ElementType.None, "Claws"), (DamageForm.Pierce, ElementType.Poison, "Poisoned arrow"),
            (DamageForm.Bite, ElementType.None, "Hound"), (DamageForm.Crush, ElementType.None, "Falling rock"),
        };
        for (int i = 0; i < practiceWounds && data.regions.Count > 0; i++)
        {
            (DamageForm form, ElementType element, string source) = blows[i % blows.Length];
            condition.ApplyHit(data, Settings, new HarvestHit
            {
                region = (int)(HarvestMath.Noise(condition.seed, i, 211) * data.regions.Count) % data.regions.Count,
                form = form,
                element = element,
                severity = 0.15f + 0.3f * (float)HarvestMath.Noise(condition.seed, i, 223),
                u = (float)HarvestMath.Noise(condition.seed, i, 227),
                v = (float)HarvestMath.Noise(condition.seed, i, 229),
                source = source,
            });
        }
        carcass = HarvestCarcass.From(data, condition);
        carcass.temperature = Temperature();
        removing = false;
        pending.Clear();
        StartCoroutine(MakeCarcass());
        Touch(true);
    }

    private bool hooked;

    private void Hook()
    {
        if (hooked)
            return;
        if (entity == null)
            entity = CombatEntity.Resolve(gameObject);
        if (entity == null)
            return;
        entity.AddDamageInterceptor(this);
        entity.Damaged += OnDamaged;
        entity.Died += OnDied;
        entity.Revived += OnRevived;
        hooked = true;
    }

    private void Unhook()
    {
        if (!hooked || entity == null)
            return;
        entity.RemoveDamageInterceptor(this);
        entity.Damaged -= OnDamaged;
        entity.Died -= OnDied;
        entity.Revived -= OnRevived;
        hooked = false;
    }

    /// <summary>Finds its anatomy: the asset, the id, else the database's creature for the mob.</summary>
    private void ResolveAnatomy()
    {
        if (anatomy != null && anatomy.data != null)
            data = anatomy.data;
        else if (!string.IsNullOrWhiteSpace(anatomyId))
            data = HarvestDatabase.Instance.Anatomy(anatomyId);
        else
            data = FindFor(entity);
        if (data == null && debugLog)
            Debug.LogWarning($"[Harvesting] {name}: no anatomy found - it cannot be harvested.", this);
    }

    /// <summary>The Harvest Database's creature for a mob (its Mob Profile, its type, its name), or null.</summary>
    public static HarvestAnatomyData FindFor(CombatEntity e)
    {
        if (e == null)
            return null;
        Mob mob = e.Mob;
        return HarvestDatabase.Instance.AnatomyForMob(mob != null ? mob.ProfileAsset : null, mob != null ? mob.type : "", e.name);
    }

    /// <summary>
    /// The creature the game will find for this mob when it plays, worked out without playing (a prefab, the inspector):
    /// its combat entity is made and its Mob cached only as it wakes, so this goes by the objects that are there.
    /// </summary>
    private HarvestAnatomyData FindForPrefab()
    {
        CombatEntity e = GetComponentInParent<CombatEntity>();
        BaseStatusController status = GetComponentInParent<BaseStatusController>();
        if (status == null) status = GetComponentInChildren<BaseStatusController>(true);
        GameObject host = e != null ? e.gameObject : status != null ? status.gameObject : gameObject;
        Mob mob = host.GetComponent<Mob>();
        if (mob == null) mob = host.GetComponentInParent<Mob>();
        return HarvestDatabase.Instance.AnatomyForMob(mob != null ? mob.ProfileAsset : null, mob != null ? mob.type : "", host.name);
    }

    /// <summary>
    /// Makes mobs harvestable when the Harvest Database's Auto Attach is on and a creature matches them (called as
    /// combat entities wake). With Add Body Parts, a mob without body parts of its own gets its anatomy's.
    /// </summary>
    internal static void EnsureFor(CombatEntity e)
    {
        if (e == null || e.Kind != CombatEntity.EntityKind.Mob || e.GetComponent<HarvestableCreature>() != null)
            return;
        HarvestSettings s = HarvestDatabase.Instance.Settings;
        if (!s.autoAttach)
            return;
        HarvestAnatomyData a = FindFor(e);
        if (a == null)
            return;
        e.gameObject.AddComponent<HarvestableCreature>();
    }

    /// <summary>Gives the mob its anatomy's body parts when it has none of its own (Add Body Parts).</summary>
    private void EnsureBodyParts()
    {
        if (data == null || entity == null || !Settings.addBodyParts)
            return;
        BodyPartController c = entity.GetComponent<BodyPartController>();
        if (c != null && c.HasOwnProfile)
            return;
        if (c == null)
            c = entity.gameObject.AddComponent<BodyPartController>();
        c.Profile = HarvestBodyParts.ProfileFor(data);
        AddHitboxes();
    }

    /// <summary>
    /// Hitboxes on the bones the anatomy names (its regions' Hitbox Bone), for exact hit locations: a trigger sphere with
    /// a Body Part Hitbox for the region's part. Only on a mob with no hitboxes of its own.
    /// </summary>
    private void AddHitboxes()
    {
        if (entity.GetComponentsInChildren<BodyPartHitbox>(true).Length > 0)
            return;
        Transform[] bones = null;
        foreach (HarvestRegion r in data.regions)
        {
            if (r == null || string.IsNullOrWhiteSpace(r.hitboxBone)) continue;
            bones ??= entity.GetComponentsInChildren<Transform>(true);
            Transform bone = Array.Find(bones, b => string.Equals(b.name, r.hitboxBone.Trim(), StringComparison.OrdinalIgnoreCase));
            if (bone == null) continue;
            string part = HarvestBodyParts.PartName(r);
            var go = new GameObject("Hitbox_" + part) { layer = entity.gameObject.layer };
            go.transform.SetParent(bone, false);
            var sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            float radius = r.hitboxRadius > 0f ? r.hitboxRadius : Mathf.Max(0.05f, entity.Height * 0.15f);
            sphere.radius = radius / Mathf.Max(1e-4f, Mathf.Abs(bone.lossyScale.x));
            go.AddComponent<BodyPartHitbox>().part = part;
        }
    }

    private HarvestCondition NewCondition()
    {
        if (seed == 0)
            seed = UnityEngine.Random.Range(1, int.MaxValue);
        float size = sizeScale > 0f ? sizeScale : entity != null && data.referenceHeight > 0f && entity.Height > 0.05f
            ? Mathf.Clamp(entity.Height / data.referenceHeight, 0.3f, 3f) : 1f;
        ItemDropOnDeath.Describe(entity, out _, out bool eliteHere, out bool boss);
        return HarvestCondition.Create(data, seed, size, AgeOf(seed), elite || eliteHere || boss, rareFinds);
    }

    private HarvestAge AgeOf(int s)
    {
        switch (age)
        {
            case AgeMode.Young: return HarvestAge.Young;
            case AgeMode.Adult: return HarvestAge.Adult;
            case AgeMode.Old: return HarvestAge.Old;
            case AgeMode.Ancient: return HarvestAge.Ancient;
        }
        double r = HarvestMath.Noise(s, 1, 97);
        return r < 0.15 ? HarvestAge.Young : r < 0.8 ? HarvestAge.Adult : r < 0.96 ? HarvestAge.Old : HarvestAge.Ancient;
    }

    private void OnRevived()
    {
        // A pooled mob is back: a new body.
        carcass = null;
        condition = null;
        seed = 0;
        removing = false;
        pending.Clear();
        if (carcassObject != null)
            Destroy(carcassObject);
        carcassObject = null;
        trigger = null;
        Touch(true);
    }

    // ------------------------------------------------------------------ hits
    public int InterceptOrder => 1000;

    /// <summary>Notes each hit's full amount before armour (the killing blow counts with its overkill).</summary>
    bool IDamageInterceptor.InterceptDamage(ref DamageInfo info)
    {
        pendingAmount = info.amount;
        pendingFrame = Time.frameCount;
        return true;
    }

    private void OnDamaged(DamageInfo info)
    {
        if (data == null || carcass != null || entity == null || entity.IsReplica || !RulesHere || entity.Summoner != null)
            return;
        condition ??= NewCondition();
        float maxHp = entity.MaxHealth > 0f ? entity.MaxHealth : 100f;
        float amount = info.amount;
        bool lethal = entity.Health != null ? entity.Health.CurrentValue <= 0f : entity.IsDead;
        // The amount noted for this very hit (a hit stopped or fully resisted after it was noted leaves a stale one).
        if (lethal && Settings.countOverkill && pendingFrame == Time.frameCount)
            amount = Mathf.Max(amount, pendingAmount);
        pendingFrame = -1;
        if (amount <= 0f)
            return;
        int region = RegionOf(info, out float u, out float v);
        var hit = new HarvestHit
        {
            region = region,
            form = info.form,
            element = info.element,
            area = info.delivery == DamageDelivery.Area,
            periodic = info.isPeriodic || info.delivery == DamageDelivery.Periodic,
            critical = info.isCritical,
            severity = amount / maxHp * Settings.severityPerHealth,
            u = u,
            v = v,
            source = SourceName(info),
            time = Time.time,
        };
        condition.ApplyHit(data, Settings, hit);
        if (debugLog)
            Debug.Log($"[Harvesting] {name}: {hit.form}/{hit.element} hit on {(region >= 0 ? data.regions[region].id : "?")} " +
                      $"(severity {hit.severity:0.00}) - {(condition.wounds.Count > 0 ? condition.wounds[condition.wounds.Count - 1].effect : "")}", this);
    }

    private static string SourceName(in DamageInfo info)
    {
        if (info.weapon != null) return info.weapon.Name;
        if (info.ability != null) return info.ability.name;
        return info.source != null ? info.source.name : "";
    }

    /// <summary>The region a hit landed on (its body part, else where it landed on the body) and where on its hide (0-1, -1 = unknown).</summary>
    private int RegionOf(in DamageInfo info, out float u, out float v)
    {
        u = v = -1f;
        bool hasPoint = info.point.sqrMagnitude > 1e-6f && !info.isPeriodic;
        float h01 = 0f, lateral = 0f, along = 0f, forward = 0f;
        if (hasPoint)
            Geometry(info.point, out h01, out lateral, out along, out forward);
        // A part several regions list ("Legs") goes to the one the hit landed on, when it is known where.
        int Part(string name) => hasPoint ? data.RegionOfPart(name, h01, lateral, along) : data.RegionOfPart(name);
        int region = -1;
        if (info.bodyPart != null)
        {
            region = Part(info.bodyPart.name);
            if (region < 0 && info.bodyPart.aliases != null)
                foreach (string alias in info.bodyPart.aliases)
                    if ((region = Part(alias)) >= 0) break;
        }
        if (!hasPoint)
            return region;
        if (region < 0)
        {
            BodyPartDefinition part = HarvestBodyParts.ProfileFor(data).Locate(h01, lateral, along);
            if (part != null) region = Part(part.name);
        }
        if (region < 0 || region >= data.regions.Count)
            return region;
        // Where on the hide laid flat: around the body (the back in the middle, the belly at the edges), along it from the head.
        bool upright = data.plan == HarvestBodyPlan.Biped || data.plan == HarvestBodyPlan.Avian;
        float around = upright ? Mathf.Atan2(lateral, forward) : Mathf.Atan2(lateral, (h01 - 0.65f) * 3f);
        float gu = 0.5f + around / (2f * Mathf.PI);
        float gv = upright ? 1f - h01 : 1f - along;
        Rect pelt = data.regions[region].pelt;
        if (pelt.width > 0f && pelt.height > 0f)
        {
            u = Mathf.Clamp01((gu - pelt.x) / pelt.width);
            v = Mathf.Clamp01((gv - pelt.y) / pelt.height);
        }
        return region;
    }

    /// <summary>A point on the body: its height (0 feet - 1 top), side (signed, radii), length (0 tail - 1 nose) and depth (+ front).</summary>
    private void Geometry(Vector3 point, out float h01, out float lateral, out float along, out float forward)
    {
        Vector3 basePos = entity.BasePosition;
        h01 = Mathf.Clamp01((point.y - basePos.y) / Mathf.Max(0.1f, entity.Height));
        Transform t = entity.transform;
        Vector3 right = t.right, fwd = t.forward;
        right.y = 0f;
        fwd.y = 0f;
        Vector3 off = point - basePos;
        off.y = 0f;
        float radius = Mathf.Max(0.05f, entity.Radius);
        lateral = right.sqrMagnitude > 1e-6f ? Vector3.Dot(off, right.normalized) / radius : 0f;
        forward = fwd.sqrMagnitude > 1e-6f ? Vector3.Dot(off, fwd.normalized) / radius : 0f;
        float z = fwd.sqrMagnitude > 1e-6f ? Vector3.Dot(point - t.position, fwd.normalized) : 0f;
        along = Mathf.Clamp01((z - bodyBack) / Mathf.Max(0.01f, bodyFront - bodyBack));
    }

    // ------------------------------------------------------------------ death
    private void OnDied(CombatEntity killer)
    {
        // Summoned creatures leave nothing to harvest (as they drop no loot): they vanish as usual.
        if (data == null || carcass != null || entity == null || entity.IsReplica || !RulesHere || entity.Summoner != null)
            return;
        condition ??= NewCondition();
        carcass = HarvestCarcass.From(data, condition);
        carcass.temperature = Temperature();
        if (debugLog)
            Debug.Log($"[Harvesting] {name} died: {condition.wounds.Count} wound(s), {condition.hits} hit(s).", this);
        StartCoroutine(MakeCarcass());
        Touch(true);
    }

    /// <summary>
    /// The body stays as a carcass (removed by this when it is empty or left alone long enough) - in a network session
    /// only when clients can harvest it too (it has a NetCarcass).
    /// </summary>
    public bool KeepsBody(float delay) =>
        data != null && carcass != null && RulesHere && !carcass.IsEmpty(data, Settings) && (Net != null || !GameNet.NetAuthority.IsNetworked);

    /// <summary>
    /// The carcass's own trigger collider, a moment after death (dead mobs turn their colliders off), sized to the body and
    /// fitted again once it has fallen. On the Ignore Raycast layer: nothing but the interaction prompt sees it.
    /// </summary>
    private IEnumerator MakeCarcass()
    {
        yield return new WaitForSeconds(0.3f);
        FitTrigger();
        yield return new WaitForSeconds(1.6f);
        FitTrigger();
    }

    private void FitTrigger()
    {
        if (this == null || carcass == null)
            return;
        Bounds b = WorldBounds();
        if (carcassObject == null)
        {
            carcassObject = new GameObject("Harvest Carcass");
            carcassObject.layer = 2; // Ignore Raycast
            carcassObject.transform.SetParent(transform, true);
            trigger = carcassObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
        }
        carcassObject.transform.position = b.center;
        carcassObject.transform.rotation = Quaternion.identity;
        Vector3 s = carcassObject.transform.lossyScale;
        trigger.size = new Vector3(b.size.x / Mathf.Max(1e-3f, Mathf.Abs(s.x)), Mathf.Max(0.3f, b.size.y) / Mathf.Max(1e-3f, Mathf.Abs(s.y)),
            b.size.z / Mathf.Max(1e-3f, Mathf.Abs(s.z)));
        trigger.center = Vector3.zero;
    }

    private Bounds WorldBounds()
    {
        var b = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
        bool any = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any && entity != null)
            b = new Bounds(entity.Center, new Vector3(entity.Radius * 2f, entity.Height, entity.Radius * 2f));
        b.Expand(0.2f);
        return b;
    }

    /// <summary>The middle of the carcass (the prompt, the drops).</summary>
    public Vector3 CarcassCentre => carcassObject != null ? carcassObject.transform.position : entity != null ? entity.Center : transform.position;

    // ------------------------------------------------------------------ time
    private void Update()
    {
        if (data == null)
            return;
        float dt = Time.deltaTime;
        if (carcass == null)
        {
            // A living regenerator heals its wounds (not its burns).
            if (condition != null && data.regeneration > 0f && RulesHere && entity != null && !entity.IsDead)
            {
                regenTimer += dt;
                if (regenTimer >= 1f)
                {
                    condition.Regenerate(data, regenTimer / 60.0);
                    regenTimer = 0f;
                }
            }
            return;
        }
        temperatureTimer -= dt;
        if (temperatureTimer <= 0f || float.IsNaN(temperature))
        {
            temperatureTimer = 2f;
            temperature = Temperature();
        }
        // Spoiling goes on everywhere (clients keep their copy moving between the server's updates).
        tickBuffer += dt;
        if (tickBuffer >= 0.5f)
        {
            carcass.Tick(Settings, tickBuffer, temperature);
            tickBuffer = 0f;
        }
        if (!RulesHere || removing)
            return;
        syncTimer += dt;
        if (syncTimer >= 10f)
        {
            // Now and then the clients' copies are set right (their clocks drift a little).
            syncTimer = 0f;
            Net?.CarcassChanged();
        }
        if (practiceCarcass)
        {
            // A practice body stays; emptied, a fresh one is laid out a moment later.
            if (carcass.locks.Count == 0 && carcass.IsEmpty(data, Settings))
            {
                practiceTimer += dt;
                if (practiceTimer >= 3f)
                {
                    practiceTimer = 0f;
                    LayOutPracticeBody();
                }
            }
            else
                practiceTimer = 0f;
            return;
        }
        bool expired = carcass.age > CarcassMinutes * 60.0 && (carcass.lastWork < 0 || carcass.age - carcass.lastWork > Settings.keepAfterWorkMinutes * 60.0) &&
                       carcass.locks.Count == 0;
        bool empty = Settings.removeWhenEmpty && carcass.locks.Count == 0 && carcass.IsEmpty(data, Settings);
        if (expired || empty)
            Remove(empty ? 3f : 0f);
    }

    /// <summary>Removes the carcass (where the rules run): network-despawned for everyone, or destroyed.</summary>
    public void Remove(float delay = 0f)
    {
        if (removing || !RulesHere)
            return;
        removing = true;
        GameNet.NetSpawn.Destroy(gameObject, delay);
    }

    /// <summary>The air temperature where the carcass lies (the Weather System's, else the database's default).</summary>
    private float Temperature()
    {
        WeatherSystem w = WeatherSystem.Instance;
        return w != null ? w.WeatherAt(transform.position).Temperature : Settings.defaultTemperature;
    }

    private void Touch(bool replicate)
    {
        version++;
        Changed?.Invoke();
        if (replicate && RulesHere)
            Net?.CarcassChanged();
    }

    // ------------------------------------------------------------------ the network copy
    /// <summary>The carcass as the server has it (network clients).</summary>
    public string ExportCarcass() => carcass != null ? JsonUtility.ToJson(carcass) : "";

    /// <summary>A client receives the server's carcass (empty = it is alive again).</summary>
    public void ApplyReplicated(string json)
    {
        if (RulesHere)
            return;
        if (string.IsNullOrEmpty(json))
        {
            if (carcass != null)
                OnRevived();
            return;
        }
        HarvestCarcass c;
        try { c = JsonUtility.FromJson<HarvestCarcass>(json); }
        catch (Exception e)
        {
            Debug.LogWarning($"[Harvesting] {name}: the server's carcass could not be read: {e.Message}", this);
            return;
        }
        if (c == null)
            return;
        bool first = carcass == null;
        carcass = c;
        tickBuffer = 0f;
        if (first)
            StartCoroutine(MakeCarcass());
        Touch(false);
    }

    // ------------------------------------------------------------------ the work
    /// <summary>Who is working and with what: the rules, the player's skills and tools.</summary>
    public HarvestWorker WorkerFor(CombatEntity player)
    {
        int key = PlayerKey(player);
        return new HarvestWorker
        {
            settings = Settings,
            skills = player != null ? HarvestKnowledge.For(player).Data : new HarvestSkillData(),
            tools = HarvestToolkit.For(player),
            player = key,
            seed = HarvestMath.Hash(key, carcass != null ? carcass.tasks : 0, 17),
        };
    }

    /// <summary>The plan of a material's work for a player (what the window plays).</summary>
    public HarvestPlan PlanFor(CombatEntity player, int material, List<int> units, HarvestWorker worker = null) =>
        carcass == null ? new HarvestPlan { refusal = "It is still alive." } : HarvestPlanner.Plan(data, carcass, material, units, worker ?? WorkerFor(player));

    /// <summary>
    /// The player starts working <paramref name="material"/> (its pieces <paramref name="units"/>, empty = all): it is
    /// planned and kept for them. False (with why) when it cannot be worked now. On a network client the server is asked
    /// (its refusal arrives as a report).
    /// </summary>
    public bool Begin(CombatEntity player, int material, List<int> units, out string reason)
    {
        reason = "";
        if (data == null || material < 0 || material >= data.materials.Count)
        {
            reason = "Nothing to harvest.";
            return false;
        }
        if (!RulesHere)
        {
            if (Net == null)
            {
                reason = "This creature is not set up for multiplayer (it needs a NetCarcass): only the host can harvest it.";
                return false;
            }
            Net.SendBegin(data.materials[material].id, units ?? new List<int>());
            return true;
        }
        if (carcass == null)
        {
            reason = "It is still alive.";
            return false;
        }
        HarvestPlan plan = PlanFor(player, material, units);
        if (!plan.Valid)
        {
            reason = plan.refusal;
            return false;
        }
        int key = PlayerKey(player);
        if (pending.TryGetValue(key, out Pending old) && old.material != material)
            carcass.Unlock(old.material, key);
        if (!carcass.Lock(material, key, 600))
        {
            reason = "Someone is working on it.";
            return false;
        }
        pending[key] = new Pending { material = material, plan = plan, began = Time.unscaledTimeAsDouble };
        if (Net != null && !CardTable.IsLocalPlayer(player))
            Net.SendPlan(player, plan);
        Touch(true);
        return true;
    }

    /// <summary>A server receives a remote player's start (refusals go back as a report).</summary>
    public void BeginFromRemote(CombatEntity player, string material, List<int> units)
    {
        int mi = data != null ? data.MaterialIndex(material) : -1;
        if (!Begin(player, mi, units, out string reason))
            Deliver(player, HarvestReport.Refused(material, mi >= 0 ? data.materials[mi].Name : material, reason));
    }

    /// <summary>The player stopped working a material without finishing it.</summary>
    public void Cancel(CombatEntity player, int material)
    {
        if (data == null || material < 0 || material >= data.materials.Count)
            return;
        if (!RulesHere)
        {
            Net?.SendCancel(data.materials[material].id);
            return;
        }
        int key = PlayerKey(player);
        if (pending.TryGetValue(key, out Pending p) && p.material == material)
            pending.Remove(key);
        if (carcass != null)
        {
            carcass.Unlock(material, key);
            Touch(true);
        }
    }

    /// <summary>Forgets every work of a player (they left, or closed the window). On a network client the server is asked.</summary>
    public void CancelAll(CombatEntity player)
    {
        if (player == null)
            return;
        if (!RulesHere)
        {
            Net?.SendRelease();
            return;
        }
        Release(PlayerKey(player));
    }

    /// <summary>Forgets every work of the player known as <paramref name="key"/> (<see cref="PlayerKey"/>; they may be gone).</summary>
    public void Release(int key)
    {
        if (!RulesHere || key == 0)
            return;
        pending.Remove(key);
        if (carcass == null)
            return;
        bool any = false;
        for (int i = carcass.locks.Count - 1; i >= 0; i--)
            if (carcass.locks[i].player == key)
            {
                carcass.locks.RemoveAt(i);
                any = true;
            }
        if (any)
            Touch(true);
    }

    /// <summary>
    /// How the work went: checked against its plan, resolved into pieces (into the player's inventory, the rest onto the
    /// ground), experience, and the carcass changed. The report goes to the player's window.
    /// </summary>
    public void Submit(CombatEntity player, HarvestTaskResult result)
    {
        if (result == null || data == null)
            return;
        if (!RulesHere)
        {
            Net?.SendSubmit(result);
            return;
        }
        int key = PlayerKey(player);
        int mi = data.MaterialIndex(result.material);
        string matName = mi >= 0 ? data.materials[mi].Name : result.material;
        if (carcass == null || !pending.TryGetValue(key, out Pending p) || p.material != mi)
        {
            Deliver(player, HarvestReport.Refused(result.material, matName, "That work was not started here."));
            return;
        }
        pending.Remove(key);
        HarvestMaterial m = data.materials[mi];
        HarvestRequestGuard.LimitToElapsed(result, Time.unscaledTimeAsDouble - p.began);
        if (!HarvestRequestGuard.Check(p.plan, result, carcass.condition.pelt.Count, out string why, out bool rushed))
        {
            carcass.Unlock(mi, key);
            Touch(true);
            Deliver(player, HarvestReport.Refused(result.material, matName, why));
            return;
        }
        HarvestKnowledge knowledge = HarvestKnowledge.For(player);
        double fresh = carcass.Freshness(m);
        HarvestOutcome o = HarvestResolver.Resolve(data, Settings, carcass, result, knowledge.Data, key);
        if (!o.accepted)
        {
            carcass.Unlock(mi, key);
            Touch(true);
            Deliver(player, HarvestReport.Refused(result.material, matName, o.refusal));
            return;
        }
        var report = new HarvestReport
        {
            material = m.id, materialName = m.Name, skill = (int)o.skill, xp = o.xp, anatomyXp = o.anatomyXp, performance = (float)o.performance,
            discarded = o.discarded, rushed = rushed,
        };
        report.lines.AddRange(o.lines);
        if (rushed)
            report.lines.Add("You worked faster than careful hands can: the work came out rough.");
        Give(player, o, fresh, report);
        Learn(player, knowledge, o, report);
        // The tools used wear a little.
        var worn = new HashSet<string>();
        foreach (HarvestStageResult st in result.stages)
            if (st != null && !string.IsNullOrEmpty(st.tool) && worn.Add(st.tool))
                HarvestToolkit.Wear(player, st.tool);
        if (debugLog)
            Debug.Log($"[Harvesting] {name}: {m.Name} - {report.Count} piece(s), performance {o.performance:0.00}.", this);
        Deliver(player, report);
        Touch(true);
        AnyHarvested?.Invoke(this, player, report);
    }

    /// <summary>The player hacks the hide off (the meat is freed, the hide is ruined).</summary>
    public void StripHide(CombatEntity player)
    {
        if (data == null)
            return;
        if (!RulesHere)
        {
            Net?.SendStrip();
            return;
        }
        if (carcass == null || carcass.hideOff)
        {
            Deliver(player, HarvestReport.Refused("", "the hide", carcass == null ? "It is still alive." : "The hide is already off."));
            return;
        }
        // Any material that waits for the hide (or the hide itself) carries the request.
        int key = PlayerKey(player);
        string material = "";
        for (int i = 0; i < data.materials.Count && material.Length == 0; i++)
            if (carcass.Availability(data, Settings, i, HarvestKnowledge.For(player).Data, key, out _) == HarvestAvailability.NeedsHide)
                material = data.materials[i].id;
        if (material.Length == 0)
            for (int i = 0; i < data.materials.Count && material.Length == 0; i++)
                if (HarvestCarcass.IsHide(data.materials[i])) material = data.materials[i].id;
        HarvestOutcome o = HarvestResolver.Resolve(data, Settings, carcass, new HarvestTaskResult { material = material, rough = true }, HarvestKnowledge.For(player).Data, key);
        var report = o.accepted
            ? new HarvestReport { material = material, materialName = "the hide", skill = (int)HarvestSkill.Skinning }
            : HarvestReport.Refused(material, "the hide", o.refusal);
        report.lines.AddRange(o.lines);
        Deliver(player, report);
        Touch(true);
        if (o.accepted)
            AnyHarvested?.Invoke(this, player, report);
    }

    /// <summary>Gives the pieces: into the player's inventory, onto the ground by the carcass when it is full.</summary>
    private void Give(CombatEntity player, HarvestOutcome o, double fresh, HarvestReport report)
    {
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        foreach (HarvestPiece piece in o.pieces)
        {
            HarvestMaterial pm = data.materials[piece.material];
            ItemSO item = null;
            List<int> codes = null;
            int count = piece.count;
            bool tannery = false;
            if (pm.kind == HarvestMaterialKind.Hide && HarvestCarcass.IsHide(pm) && o.hideSource != null)
            {
                CraftRules rules = CraftDatabase.Instance.Rules;
                CraftPiece hide = HarvestHides.Piece(pm, o.hideSource, fresh, rules);
                if (hide != null)
                {
                    item = CraftItems.PieceItem(rules, hide, null, player != null ? CraftKnowledge.For(player).Data : null);
                    count = 1;
                    tannery = item != null;
                }
            }
            if (item == null)
            {
                item = HarvestItems.ItemFor(data, pm);
                codes = HarvestItems.Codes(item, count, piece.quality, piece.marks);
            }
            if (item == null)
                continue;
            int left = inv != null ? inv.AddItem(item, count, codes) : count;
            bool onGround = false;
            if (left > 0)
            {
                List<int> rest = codes != null ? codes.GetRange(0, Mathf.Min(left, codes.Count)) : null;
                Vector3 at = CarcassCentre + Vector3.up * 0.4f;
                onGround = ItemHandler.SpawnWorldItem(item, left, rest, at, gameObject) != null;
                // An item asset with no model cannot lie on the ground: the same material's runtime item can.
                if (!onGround && item is IHarvestedItem && HarvestItems.Resolve(HarvestItems.IdOf(data.id, pm.id)) is ItemSO made && made != item)
                    onGround = ItemHandler.SpawnWorldItem(made, left, rest, at, gameObject) != null;
            }
            string unitName = piece.unit >= 0 ? UnitName(piece) : "";
            report.pieces.Add(new HarvestReportPiece
            {
                name = unitName.Length > 0 ? HarvestText.Capital(unitName) : item.Name, count = count, quality = piece.quality, marks = (int)piece.marks,
                item = ItemDatabase.Id(item) ?? "", tanneryHide = tannery,
            });
            if (left > 0)
                report.lines.Add(onGround ? $"Your pack is full: {left} {item.Name.ToLowerInvariant()} left on the ground."
                    : $"Your pack is full and the {item.Name.ToLowerInvariant()} cannot be set down: {left} lost.");
        }
    }

    private string UnitName(HarvestPiece piece)
    {
        foreach (HarvestUnit u in carcass.condition.units)
            if (u.material == piece.material && u.index == piece.unit)
                return HarvestCondition.UnitName(data, u);
        return "";
    }

    /// <summary>Experience (the method's skill, Anatomy, the character), the species note, and the player's copy of their skills.</summary>
    private void Learn(CombatEntity player, HarvestKnowledge k, HarvestOutcome o, HarvestReport report)
    {
        if (k == null)
            return;
        report.learnt.AddRange(k.Gain(o.skill, o.xp));
        report.learnt.AddRange(k.Gain(HarvestSkill.Anatomy, o.anatomyXp));
        var taken = new List<string>();
        float best = 0f;
        foreach (HarvestPiece p in o.pieces)
        {
            string id = data.materials[p.material].id;
            if (!taken.Contains(id)) taken.Add(id);
            best = Mathf.Max(best, p.quality);
        }
        string log = o.pieces.Count > 0
            ? $"{data.Name}: {report.materialName} ×{o.Count} - {HarvestText.Grade(best)} ({best * 100f:0}%)"
            : $"{data.Name}: {report.materialName} - nothing usable";
        k.Harvested(data.id, taken, o.Count, best, log);
        HarvestSettings s = Settings;
        if (s.characterXpShare > 0f && player != null)
        {
            ExperienceManager xp = player.GetComponentInParent<ExperienceManager>() ?? player.GetComponentInChildren<ExperienceManager>();
            int amount = Mathf.RoundToInt((o.xp + o.anatomyXp) * s.characterXpShare);
            if (xp != null && amount > 0)
                xp.AddExperience(amount);
        }
        if (!CardTable.IsLocalPlayer(player))
            Net?.SendSkills(player, k.Export());
    }

    /// <summary>Sends a report to the player's window (this machine's player, or a remote one through the network).</summary>
    private void Deliver(CombatEntity player, HarvestReport report)
    {
        if (player == null || report == null)
            return;
        if (CardTable.IsLocalPlayer(player) || Net == null)
            Harvested?.Invoke(player, report);
        else
            Net.SendReport(player, report);
    }

    /// <summary>A client receives its report from the server.</summary>
    public void ReceiveReport(CombatEntity player, HarvestReport report)
    {
        if (report != null)
            Harvested?.Invoke(player, report);
    }

    /// <summary>A client receives the plan of the work its player started.</summary>
    public void ReceivePlan(CombatEntity player, HarvestPlan plan)
    {
        if (plan != null && !RulesHere)
            Planned?.Invoke(player, plan);
    }

    /// <summary>A message for the player (theirs here, else through the network).</summary>
    public void Tell(CombatEntity player, string text)
    {
        if (player == null || string.IsNullOrEmpty(text))
            return;
        if (CardTable.IsLocalPlayer(player) || Net == null)
            InventoryManager.For(player)?.ShowMessage(text, InventoryFeedback.Kind.Info);
        else
            Net.Tell(player, text);
    }

    // ------------------------------------------------------------------ loot
    /// <summary>
    /// The loot tables drop nothing for it: its body is the loot (not in a network session where clients could not
    /// harvest it - without a NetCarcass the body is not kept there).
    /// </summary>
    public bool ReplacesTableDrops => replacesTableDrops && data != null && Settings.skipGenericMobDrops && (Net != null || !GameNet.NetAuthority.IsNetworked);

    // ------------------------------------------------------------------ interaction
    public string InteractionName => data != null ? $"{data.Name} carcass" : "Carcass";
    public string InteractionVerb => "Harvest";
    public Transform InteractionPoint => interactionPoint != null ? interactionPoint : carcassObject != null ? carcassObject.transform : transform;
    public float InteractionRange => Settings.workRange + (entity != null ? Mathf.Max(entity.Radius, (bodyFront - bodyBack) * 0.5f) : 0.5f);
    public int InteractionPriority => 4;
    public float HoldDuration => 0f;
    public bool AllowsClick => true;

    public bool CanInteract(PlayerInteractor interactor, out string reason)
    {
        reason = "";
        if (carcass == null || data == null || removing)
            return false;
        return PlayerOf(interactor) != null;
    }

    public void Interact(PlayerInteractor interactor)
    {
        CombatEntity player = PlayerOf(interactor);
        if (player == null || carcass == null)
            return;
        if (CardTable.IsLocalPlayer(player))
        {
            HarvestWindow.Show(this, player);
            return;
        }
        // A remote player (the server got the interaction from their client): the window opens on their screen.
        if (!RulesHere)
            return;
        if (Net != null && Net.Open(player))
            Net.SendSkills(player, HarvestKnowledge.For(player).Export());
        else
            Tell(player, "This creature is not set up for multiplayer: only the host can harvest it.");
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }

    private static CombatEntity PlayerOf(PlayerInteractor interactor) =>
        interactor != null && interactor.PlayerObject != null ? CombatEntity.Resolve(interactor.PlayerObject) : null;

    // ------------------------------------------------------------------ checking
    /// <summary>Configuration problems (the inspector lists them).</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        if (data == null && (anatomy == null || anatomy.data == null))
        {
            HarvestAnatomyData found = !string.IsNullOrWhiteSpace(anatomyId) ? HarvestDatabase.Instance.Anatomy(anatomyId) : FindForPrefab();
            if (found == null)
                errors.Add(string.IsNullOrWhiteSpace(anatomyId)
                    ? "No anatomy: assign a Creature Anatomy, name one (Anatomy Id), or give the mob a type the Harvest Database knows."
                    : $"The Harvest Database has no creature '{anatomyId}'.");
        }
        if (!practiceCarcass && GetComponent<MobStatusController>() == null && GetComponentInParent<CombatEntity>() == null)
            warnings.Add("No combat entity: it is never hit and never dies.");
        if (GetComponent<BodyPartController>() == null && !Settings.addBodyParts)
            warnings.Add("No Body Part Controller (and the database's Add Body Parts is off): hits are located on its regions, but there are no weak points.");
    }
}
