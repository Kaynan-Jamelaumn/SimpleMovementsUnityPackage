using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The injury system on one character (Project-Zomboid-like body damage). It tracks every body part of
/// <see cref="InjurySettings.bodyParts"/> - condition, injuries, whether it is lost - and plugs into the existing damage
/// pipeline: every hit the character's <see cref="CombatEntity"/> takes (weapons, projectiles, abilities, mobs, players,
/// hazards) lands on a part (from the <see cref="BodyPartController"/>'s location, else the hit's height and side), wears
/// its condition down, may cause injuries of the configured types and severities, and may cut the limb off.
/// <list type="bullet">
/// <item><b>Over time</b>: wounds bleed (health loss), clot, heal (faster when treated), get worse when untreated (an
/// open wound gets infected); the part's condition recovers when it has no injury.</item>
/// <item><b>Effects</b>: an impaired part applies its consequence scaled by the impairment (a limp, weaker attacks...),
/// each injury stage its own (an infection drains health), bleeding its status; parts and stages stop actions
/// (sprinting, two-handed weapons, a hand) through <see cref="IBodyActionLimits"/>, which movement, weapons, hands,
/// blocking, items and abilities ask.</item>
/// <item><b>Limb loss</b>: the part (and what is attached to it) is gone: its armour and the item it held fall, the limb
/// drops as an item to retrieve (<see cref="SeveredLimbItemSO"/>) with its condition and reattachment time limit, the
/// stump bleeds.</item>
/// <item><b>Treatment</b>: medical items and abilities apply <see cref="InjuryTreatment"/>s (bandage, splint, stitches,
/// reattachment...), see <see cref="BeginTreatment"/>.</item>
/// </list>
/// Added automatically to players (and mobs when enabled) while the project's Injury Settings are on. The rules run
/// where the game rules run (offline, server); a client's copy only shows the state it is sent
/// (<see cref="ApplySnapshot"/>).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-25)]
public partial class InjuryController : MonoBehaviour, IBodyActionLimits, IBodyLocomotion
{
    [Tooltip("Settings used by this character. Empty = the project's Injury Settings (Resources).")]
    [SerializeField] private InjurySettings settingsOverride;
    [Tooltip("Log injuries, treatments and limb losses.")]
    [SerializeField] private bool logChanges = false;

    private readonly List<InjuryPartState> parts = new List<InjuryPartState>(10);
    private readonly Dictionary<object, ConsequenceRunner> runners = new Dictionary<object, ConsequenceRunner>();
    private readonly List<object> staleRunners = new List<object>();
    private readonly List<InjuryPartState> candidates = new List<InjuryPartState>(6);
    private readonly Dictionary<BodyAction, string> reasons = new Dictionary<BodyAction, string>();
    private readonly object bleedingKey = new object();
    private CombatEntity entity;
    private InjurySettings builtFrom;
    private bool effectsDirty = true;
    private bool changedPending;
    private BodyAction blocked;
    private HandCapability hands = HandCapability.Full;
    private float bleedDebt;
    private float nextDrip;
    private float nextExpiryCheck;
    private float nextFactorRefresh;
    private InjuryResistances resistances;
    private InjuryHudAlert hudAlert;

    // ------------------------------------------------------------------ events
    /// <summary>Anything the health panel shows changed (raised at most once per frame).</summary>
    public event Action Changed;
    /// <summary>A new injury (part, injury).</summary>
    public event Action<InjuryPartState, Injury> InjuryAdded;
    /// <summary>An injury healed or was removed (part, injury).</summary>
    public event Action<InjuryPartState, Injury> InjuryRemoved;
    /// <summary>An injury moved to another stage (part, injury, previous stage index).</summary>
    public event Action<InjuryPartState, Injury, int> InjuryStageChanged;
    /// <summary>A limb was lost (the lost part, its record).</summary>
    public event Action<InjuryPartState, SeveredLimbRecord> LimbLost;
    /// <summary>A limb was reattached (the part, its record - null when regrown).</summary>
    public event Action<InjuryPartState, SeveredLimbRecord> LimbReattached;
    /// <summary>A treatment finished (treatment, part, injury - null for a limb -, source).</summary>
    public event Action<InjuryTreatment, InjuryPartState, Injury, TreatmentSource> Treated;
    /// <summary>A message for the player ("Bandaged your left arm.").</summary>
    public event Action<string> Message;
    /// <summary>What the hands can hold changed (the inventory refreshes the hands).</summary>
    public event Action HandsChanged;
    /// <summary>Any character lost a limb (controller, part, record).</summary>
    public static event Action<InjuryController, InjuryPartState, SeveredLimbRecord> AnyLimbLost;

    /// <summary>Counts every change: a cheap way for UI to know it must redraw.</summary>
    public int Version { get; private set; }

    private static readonly List<InjuryController> active = new List<InjuryController>();

    /// <summary>
    /// A lost limb's record known here: the registry where the rules run, else (a client) one received with a character's
    /// state. Null when unknown.
    /// </summary>
    public static SeveredLimbRecord FindRecord(int id)
    {
        SeveredLimbRecord r = SeveredLimbRegistry.Get(id);
        if (r != null || id <= 0)
            return r;
        for (int i = 0; i < active.Count; i++)
        {
            InjuryController c = active[i];
            if (c == null)
                continue;
            for (int p = 0; p < c.parts.Count; p++)
                if (c.parts[p].LostLimb != null && c.parts[p].LostLimb.Id == id)
                    return c.parts[p].LostLimb;
        }
        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active.Clear();
        AnyLimbLost = null;
    }

    // ------------------------------------------------------------------ access
    /// <summary>The settings in use.</summary>
    public InjurySettings Settings => settingsOverride != null ? settingsOverride : InjurySettings.Instance;

    public CombatEntity Entity => entity != null ? entity : (entity = CombatEntity.Resolve(gameObject) ?? GetComponentInParent<CombatEntity>());

    /// <summary>The tracked parts, in the order of the settings.</summary>
    public IReadOnlyList<InjuryPartState> Parts => parts;

    /// <summary>True where the injury rules run (offline, server). False on a client's copy (it shows what it is sent).</summary>
    public bool RulesHere => Entity == null || !Entity.IsReplica;

    /// <summary>
    /// The character's injury resistances (traits, race, class, items, buffs and its own list; the character stats Wound
    /// Chance, Bleeding... count there too). Added when missing.
    /// </summary>
    public InjuryResistances Resistances
    {
        get
        {
            if (resistances == null)
            {
                CombatEntity e = Entity;
                resistances = InjuryResistances.For(e != null ? (Component)e : this);
            }
            return resistances;
        }
    }

    /// <summary>The injury controller of a character (null when it has none).</summary>
    public static InjuryController For(Component anyPart)
    {
        if (anyPart == null)
            return null;
        InjuryController c = anyPart.GetComponentInParent<InjuryController>();
        if (c != null)
            return c;
        CombatEntity e = anyPart.GetComponentInParent<CombatEntity>();
        if (e == null)
        {
            PlayerStatusController ps = anyPart.GetComponentInParent<PlayerStatusController>();
            if (ps != null)
                e = CombatEntity.Resolve(ps.gameObject);
        }
        if (e != null)
            c = e.GetComponentInChildren<InjuryController>();
        if (c == null)
        {
            InventoryManager inv = anyPart.GetComponentInParent<InventoryManager>();
            if (inv != null && inv.Player != null)
                c = inv.Player.GetComponentInChildren<InjuryController>();
        }
        return c;
    }

    /// <summary>Adds controllers to characters while the injury system is on (called by <see cref="CombatEntity"/>).</summary>
    internal static void EnsureFor(CombatEntity e)
    {
        if (e == null)
            return;
        InjurySettings s = InjurySettings.Instance;
        if (!s.enabled)
            return;
        bool wanted = e.Kind == CombatEntity.EntityKind.Player ? s.players : e.Kind == CombatEntity.EntityKind.Mob && s.mobs;
        if (wanted && e.GetComponent<InjuryController>() == null)
            e.gameObject.AddComponent<InjuryController>();
    }

    public InjuryPartState GetPart(string partId)
    {
        if (string.IsNullOrWhiteSpace(partId))
            return null;
        for (int i = 0; i < parts.Count; i++)
            if (parts[i].Def.Matches(partId))
                return parts[i];
        return null;
    }

    public InjuryPartState GetPart(InjuryBodyPart def)
    {
        for (int i = 0; i < parts.Count; i++)
            if (parts[i].Def == def)
                return parts[i];
        return null;
    }

    /// <summary>Finds an injury by its uid (null = healed or unknown).</summary>
    public Injury FindInjury(int uid, out InjuryPartState part)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            Injury inj = parts[i].Find(uid);
            if (inj != null)
            {
                part = parts[i];
                return inj;
            }
        }
        part = null;
        return null;
    }

    /// <summary>Every injury of the character (a new list unless one is passed in).</summary>
    public List<Injury> AllInjuries(List<Injury> into = null)
    {
        into = into ?? new List<Injury>();
        into.Clear();
        for (int i = 0; i < parts.Count; i++)
            into.AddRange(parts[i].injuries);
        return into;
    }

    /// <summary>Health lost per second to bleeding right now (after the global bleeding rules).</summary>
    public float BleedingPerSecond
    {
        get
        {
            float b = 0f;
            for (int i = 0; i < parts.Count; i++)
                b += parts[i].BleedingPerSecond;
            BleedingRules r = Settings.bleeding;
            b *= r.multiplier;
            return r.maxPerSecond > 0f ? Mathf.Min(b, r.maxPerSecond) : b;
        }
    }

    public bool IsBleeding => BleedingPerSecond > 1e-4f;

    /// <summary>How many parts are missing.</summary>
    public int LostCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < parts.Count; i++)
                if (parts[i].Lost) n++;
            return n;
        }
    }

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        BuildParts();
    }

    private void OnEnable()
    {
        if (!active.Contains(this))
            active.Add(this);
        CombatEntity e = Entity;
        if (e != null)
        {
            e.Damaged += OnDamaged;
            e.Died += OnDied;
            e.Revived += OnRevived;
            e.BodyLimits = this;
        }
        effectsDirty = true;
    }

    private void OnDisable()
    {
        active.Remove(this);
        if (entity != null)
        {
            entity.Damaged -= OnDamaged;
            entity.Died -= OnDied;
            entity.Revived -= OnRevived;
            if (ReferenceEquals(entity.BodyLimits, this))
                entity.BodyLimits = null;
        }
        CancelTreatment(null);
        ClearRunners();
        strainRunning = null;
        RestoreBodyShape();
        effectsDirty = true;
        if (hudAlert != null)
            hudAlert.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        ClearRunners();
        RestoreAllVisuals();
        RestoreBodyShape();
        if (hudAlert != null)
            Destroy(hudAlert.gameObject);
    }

    /// <summary>(Re)creates the part states from the settings (keeps nothing).</summary>
    public void BuildParts()
    {
        InjurySettings s = Settings;
        builtFrom = s;
        ClearRunners();
        strainRunning = null;
        RestoreAllVisuals();
        RestoreBodyShape();
        hitboxCache = null;
        parts.Clear();
        if (s.bodyParts != null)
            foreach (InjuryBodyPart def in s.bodyParts)
                if (def != null && !string.IsNullOrWhiteSpace(def.id))
                    parts.Add(new InjuryPartState { Def = def, Condition = Mathf.Max(1f, def.maxCondition) });
        effectsDirty = true;
        MarkChanged();
    }

    private void Update()
    {
        InjurySettings s = Settings;
        if (s != builtFrom)
            BuildParts();
        if (!s.enabled)
        {
            if (runners.Count > 0 || blocked != BodyAction.None)
                Disable();
            return;
        }
        float dt = Time.deltaTime;
        if (RulesHere)
        {
            Tick(dt);
            TickTreatment(dt);
        }
        else
        {
            // A client's copy: the server's state arrives in snapshots; only the progress bar moves here.
            if (activeTreatment != null && activeTreatment.Elapsed < activeTreatment.Duration)
                activeTreatment.Elapsed += dt;
            if (effectsDirty)
            {
                effectsDirty = false;
                RecomputeLimits();
            }
        }
        UpdateHudAlert();
        FlushChanged();
    }

    private void LateUpdate()
    {
        KeepVisualsHidden();
        KeepPosture();
    }

    /// <summary>The system was turned off at runtime: everything this controller applied is removed.</summary>
    private void Disable()
    {
        ClearRunners();
        strainRunning = null;
        blocked = BodyAction.None;
        reasons.Clear();
        locomotion = BodyLocomotion.Normal;
        RestoreBodyShape();
        SetHands(HandCapability.Full);
        effectsDirty = true; // recomputed if the system is turned on again
        MarkChanged();
    }

    // ------------------------------------------------------------------ damage
    private void OnDamaged(DamageInfo info)
    {
        if (!RulesHere || !isActiveAndEnabled || !Settings.enabled)
            return;
        if (info.amount <= 0f || info.isPeriodic || info.delivery == DamageDelivery.Periodic)
            return; // bleeding, poison and other damage over time do not make new wounds
        CombatEntity e = Entity;
        if (e == null || e.IsDead)
            return;
        if (activeTreatment != null && Settings.damageInterruptsTreatment)
            CancelTreatment("Interrupted by a hit.");
        InjuryPartState part = ResolvePart(info);
        if (part != null)
            ApplyHit(part, info);
    }

    /// <summary>
    /// A hit on <paramref name="part"/> (the damage pipeline calls it for every hit; scripts may too): condition, limb
    /// loss, zero-condition rule and new injuries.
    /// </summary>
    public void ApplyHit(InjuryPartState part, DamageInfo info)
    {
        if (part == null || part.Lost || !RulesHere)
            return;
        InjurySettings s = Settings;
        float conditionDamage = info.amount * s.conditionPerDamage * part.Def.conditionDamageMultiplier;
        if (conditionDamage <= 0f)
            return;
        float before = part.Condition;
        part.Condition = Mathf.Max(0f, before - conditionDamage);
        float hitFraction = conditionDamage / part.MaxCondition;
        effectsDirty = true;
        MarkChanged();
        if (logChanges || s.logInjuries)
            Debug.Log($"[Injuries] {name}: hit on {part.Name} -{conditionDamage:0.#} condition ({part.Condition:0}/{part.MaxCondition:0}).", this);

        if (TryLoseFromHit(part, info, hitFraction, before))
            return;
        if (part.Condition <= 0f && before > 0f)
            ApplyZeroCondition(part, info);
        RollInjuries(part, info, hitFraction);
    }

    /// <summary>
    /// The part a hit lands on: the <see cref="BodyPartController"/>'s part (its matching injury parts), else the parts
    /// whose height band holds the hit; the side of the hit picks left or right; weights and height pick between an arm
    /// and its hand. A lost part passes the hit on to what it was attached to.
    /// </summary>
    public InjuryPartState ResolvePart(in DamageInfo info)
    {
        if (parts.Count == 0)
            return null;
        float h01 = float.NaN, lateral = float.NaN;
        CombatEntity e = Entity;
        // No real location: no point, or the body's centre (health lost outside the damage pipeline reports that).
        bool located = e != null && info.point != Vector3.zero && (info.point - e.Center).sqrMagnitude > 1e-4f;
        if (located)
        {
            Vector3 basePos = e.BasePosition;
            h01 = Mathf.Clamp01((info.point.y - basePos.y) / Mathf.Max(0.1f, e.Height));
            Vector3 right = e.transform.right;
            right.y = 0f;
            Vector3 offset = info.point - basePos;
            offset.y = 0f;
            if (right.sqrMagnitude > 1e-6f)
                lateral = Vector3.Dot(offset, right.normalized) / Mathf.Max(0.05f, e.Radius);
        }

        candidates.Clear();
        if (info.bodyPart != null)
        {
            // The part named like the profile part (a hit on the Left Forearm hitbox is the left forearm), else the parts
            // listing it by name in their Hit Parts (a simple profile's "Arms": the side and height pick the segment), else
            // the parts that take hits of it by one of its aliases.
            for (int i = 0; i < parts.Count; i++)
                if (parts[i].Def.IsNamed(info.bodyPart.name))
                    candidates.Add(parts[i]);
            if (candidates.Count == 0)
                for (int i = 0; i < parts.Count; i++)
                    if (parts[i].Def.ReceivesHitsOfExactly(info.bodyPart))
                        candidates.Add(parts[i]);
            if (candidates.Count == 0)
                for (int i = 0; i < parts.Count; i++)
                    if (parts[i].Def.ReceivesHitsOf(info.bodyPart))
                        candidates.Add(parts[i]);
        }
        if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(info.aimedBodyPart))
        {
            InjuryPartState aimed = GetPart(info.aimedBodyPart);
            if (aimed != null)
                candidates.Add(aimed);
        }
        if (candidates.Count == 0 && !float.IsNaN(h01) && info.delivery != DamageDelivery.Area)
            for (int i = 0; i < parts.Count; i++)
                if (parts[i].Def.InHeight(h01))
                    candidates.Add(parts[i]);
        InjuryPartState picked = candidates.Count > 0 ? PickCandidate(h01, lateral) : DefaultPart();
        return Redirect(picked);
    }

    private InjuryPartState PickCandidate(float h01, float lateral)
    {
        // Left or right: from where it hit, else at random (centre parts always stay).
        bool sided = false;
        for (int i = 0; i < candidates.Count; i++)
            if (candidates[i].Def.side != BodyPartSide.Center) { sided = true; break; }
        if (sided)
        {
            BodyPartSide want = !float.IsNaN(lateral) && Mathf.Abs(lateral) > 0.05f
                ? (lateral > 0f ? BodyPartSide.Right : BodyPartSide.Left)
                : (UnityEngine.Random.value < 0.5f ? BodyPartSide.Left : BodyPartSide.Right);
            for (int i = candidates.Count - 1; i >= 0 && candidates.Count > 1; i--)
            {
                BodyPartSide side = candidates[i].Def.side;
                if (side != BodyPartSide.Center && side != want)
                    candidates.RemoveAt(i);
            }
        }
        float total = 0f;
        for (int i = 0; i < candidates.Count; i++)
            total += Weight(candidates[i], h01);
        if (total <= 0f)
            return candidates[0];
        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= Weight(candidates[i], h01);
            if (roll <= 0f)
                return candidates[i];
        }
        return candidates[candidates.Count - 1];
    }

    private static float Weight(InjuryPartState p, float h01)
    {
        float w = Mathf.Max(0f, p.Def.hitWeight);
        if (!float.IsNaN(h01) && p.Def.InHeight(h01))
            w *= 3f; // hits at a part's height prefer it (a low hit on the legs is a foot more often)
        return w;
    }

    private InjuryPartState DefaultPart() => GetPart(Settings.defaultPart) ?? (parts.Count > 0 ? parts[0] : null);

    /// <summary>A lost part's hits go to what it was attached to (a missing hand: the arm; a missing arm: the torso).</summary>
    private InjuryPartState Redirect(InjuryPartState p)
    {
        for (int guard = 0; p != null && p.Lost && guard < 8; guard++)
        {
            InjuryPartState parent = GetPart(p.Def.attachedTo);
            p = parent ?? DefaultPart();
        }
        return p != null && p.Lost ? null : p;
    }

    private void ApplyZeroCondition(InjuryPartState part, in DamageInfo info)
    {
        CombatEntity e = Entity;
        switch (part.Def.atZeroCondition)
        {
            case ZeroConditionRule.Stun:
                if (e != null && part.Def.zeroConditionStunSeconds > 0f)
                    e.ApplyControl(ControlType.Stun, part.Def.zeroConditionStunSeconds, null);
                break;
            case ZeroConditionRule.Kill:
                if (e != null && e.Health != null && !e.IsDead)
                {
                    float lethal = e.Health.CurrentValue + e.MaxHealth * 5f + 1000f;
                    e.ApplyDamage(new DamageInfo
                    {
                        amount = lethal, source = info.source, target = e, point = e.Center, type = DamageType.True, unblockable = true,
                        isPeriodic = true, delivery = DamageDelivery.Periodic, threatMultiplier = 0f,
                    });
                }
                break;
            case ZeroConditionRule.LoseLimb:
                break; // handled with the limb rules (TryLoseFromHit)
        }
    }

    private void RollInjuries(InjuryPartState part, in DamageInfo info, float hitFraction)
    {
        InjurySettings s = Settings;
        if (s.injuryTypes == null)
            return;
        int made = 0;
        DamageForm form = DamageForms.Resolve(info);
        InjuryResistances res = Resistances;
        for (int i = 0; i < s.injuryTypes.Count && made < s.maxInjuriesPerHit; i++)
        {
            InjuryTypeDefinition type = s.injuryTypes[i];
            if (type == null || !type.causedByHits || type.trigger == null)
                continue;
            // How hard the hit counts for this injury: its form (a crushing blow breaks bones worse) and the character's
            // resistances (strong bones: milder fractures).
            float fraction = hitFraction * type.trigger.StrengthFor(form);
            if (res != null)
                fraction *= res.SeverityMultiplier(type, part.Def, form);
            if (!type.trigger.Matches(info, part.Def, fraction))
                continue;
            int stage = type.StageForHit(fraction);
            if (stage < 0)
                continue;
            float chance = ChanceOf(type, part, form, s);
            if (chance <= 0f || (chance < 1f && UnityEngine.Random.value > chance))
                continue;
            Injury made1 = AddInjury(part, type, stage, info.source, true);
            if (made1 != null)
            {
                if (made1.Form == DamageForm.None)
                    made1.Form = form;
                made++;
            }
        }
    }

    /// <summary>
    /// The chance (0-1+) that a hit of <paramref name="form"/> matching <paramref name="type"/>'s trigger on
    /// <paramref name="part"/> causes it: the trigger's chance, its form multiplier, the global multiplier and the
    /// character's resistances.
    /// </summary>
    public float ChanceOf(InjuryTypeDefinition type, InjuryPartState part, DamageForm form, InjurySettings s = null)
    {
        if (type == null || type.trigger == null)
            return 0f;
        s = s ?? Settings;
        float chance = type.trigger.ChanceFor(form) * s.injuryChanceMultiplier;
        InjuryResistances res = Resistances;
        if (res != null)
            chance *= res.InjuryChanceMultiplier(type, part != null ? part.Def : null, form);
        return chance;
    }

    // ------------------------------------------------------------------ injuries
    /// <summary>
    /// Adds an injury of <paramref name="typeId"/> at <paramref name="stage"/> to the part (or makes an existing one of the
    /// same type worse, see Merge Into Existing). Returns the injury, or null when nothing changed.
    /// </summary>
    public Injury AddInjury(string partId, string typeId, int stage = 0, CombatEntity cause = null)
    {
        InjuryTypeDefinition type = Settings.FindType(typeId);
        InjuryPartState part = GetPart(partId);
        if (type == null || part == null)
        {
            Debug.LogWarning($"[Injuries] {name}: no part '{partId}' or injury type '{typeId}'.", this);
            return null;
        }
        return AddInjury(part, type, stage, cause, false);
    }

    /// <summary>See <see cref="AddInjury(string,string,int,CombatEntity)"/>. <paramref name="fromHit"/>: a new hit on a treated injury reopens it.</summary>
    public Injury AddInjury(InjuryPartState part, InjuryTypeDefinition type, int stage, CombatEntity cause, bool fromHit)
    {
        if (part == null || type == null || type.stages == null || type.stages.Count == 0 || !RulesHere)
            return null;
        stage = Mathf.Clamp(stage, 0, type.stages.Count - 1);
        Injury existing = null;
        int sameType = 0;
        for (int i = 0; i < part.injuries.Count; i++)
        {
            if (part.injuries[i].Type != type)
                continue;
            sameType++;
            if (existing == null || part.injuries[i].StageIndex < existing.StageIndex)
                existing = part.injuries[i];
        }
        if (existing != null && (type.mergeIntoExisting || sameType >= Mathf.Max(1, type.maxPerPart)))
        {
            if (stage <= existing.StageIndex)
            {
                if (fromHit && (existing.Bandaged || existing.Treated) && existing.Stage != null && existing.Stage.bleedingPerSecond > 0f)
                {
                    // Hit again where it was bandaged: the bandage is soaked, the wound bleeds again.
                    existing.Bandaged = false;
                    existing.BandageEffectiveness = 0f;
                    existing.BleedTime = 0f;
                    effectsDirty = true;
                    MarkChanged();
                }
                return null;
            }
            ChangeStage(existing, stage, keepTreatment: !fromHit);
            existing.Cause = cause;
            return existing;
        }
        var injury = new Injury { Type = type, StageIndex = stage, Part = part, Cause = cause };
        UpdateFactors(injury);
        part.injuries.Add(injury);
        effectsDirty = true;
        if (logChanges || Settings.logInjuries)
            Debug.Log($"[Injuries] {name}: {injury.Name} ({Settings.SeverityName(injury.Severity)}) on {part.Name}.", this);
        InjuryAdded?.Invoke(part, injury);
        MarkChanged();
        return injury;
    }

    /// <summary>Moves an injury to another stage (worse or better). <paramref name="keepTreatment"/>: its bandage / splint stays.</summary>
    public void ChangeStage(Injury injury, int stage, bool keepTreatment = true)
    {
        if (injury == null || injury.Type == null || !RulesHere)
            return;
        stage = Mathf.Clamp(stage, 0, injury.Type.stages.Count - 1);
        int previous = injury.StageIndex;
        if (stage == previous)
            return;
        injury.SetStage(injury.Type, stage);
        if (!keepTreatment)
            injury.ClearTreatment();
        UpdateFactors(injury);
        effectsDirty = true;
        InjuryStageChanged?.Invoke(injury.Part, injury, previous);
        MarkChanged();
    }

    /// <summary>Removes an injury (healed, cured, or by script).</summary>
    public void RemoveInjury(Injury injury)
    {
        if (injury == null || injury.Part == null || !RulesHere)
            return;
        if (!injury.Part.injuries.Remove(injury))
            return;
        RemoveRunner(injury);
        effectsDirty = true;
        InjuryRemoved?.Invoke(injury.Part, injury);
        MarkChanged();
    }

    /// <summary>Gives a part condition back (treatments, scripts).</summary>
    public void RestoreCondition(InjuryPartState part, float amount)
    {
        if (part == null || part.Lost || amount <= 0f || !RulesHere)
            return;
        part.Condition = Mathf.Min(part.MaxCondition, part.Condition + amount);
        effectsDirty = true;
        MarkChanged();
    }

    /// <summary>Heals everything: injuries, condition and (optionally) lost limbs. Used on respawn and by scripts.</summary>
    public void HealAll(bool restoreLimbs = true)
    {
        if (!RulesHere)
            return;
        CancelTreatment(null);
        for (int i = 0; i < parts.Count; i++)
        {
            InjuryPartState p = parts[i];
            for (int j = p.injuries.Count - 1; j >= 0; j--)
                RemoveRunner(p.injuries[j]);
            p.injuries.Clear();
            if (p.Lost && !restoreLimbs)
                continue;
            if (p.Lost)
                RestorePart(p);
            p.Condition = p.MaxCondition;
        }
        bleedDebt = 0f;
        effectsDirty = true;
        MarkChanged();
    }

    // ------------------------------------------------------------------ over time
    private void Tick(float dt)
    {
        CombatEntity e = Entity;
        if (e == null || e.IsDead || dt <= 0f)
            return;
        InjurySettings s = Settings;
        if (Time.time >= nextFactorRefresh)
        {
            nextFactorRefresh = Time.time + 0.5f;
            RefreshFactors();
        }
        for (int p = 0; p < parts.Count; p++)
        {
            InjuryPartState part = parts[p];
            for (int i = part.injuries.Count - 1; i >= 0; i--)
            {
                if (i >= part.injuries.Count)
                    continue; // an injury above changed the list
                TickInjury(part, part.injuries[i], dt, s);
            }
            if (!part.Lost && part.injuries.Count == 0 && part.Condition < part.MaxCondition && part.Def.conditionRegenPerSecond > 0f)
            {
                float before = part.Condition;
                part.Condition = Mathf.Min(part.MaxCondition, part.Condition + part.Def.conditionRegenPerSecond * dt);
                if (Mathf.FloorToInt(before * 0.2f) != Mathf.FloorToInt(part.Condition * 0.2f))
                {
                    effectsDirty = true; // the impairment from condition changes in 5-point steps
                    MarkChanged();
                }
            }
        }
        TickBleeding(dt, s);
        TickLimbRecords();
        TickStrain(dt, s);
        if (effectsDirty)
            RecomputeEffects();
        foreach (KeyValuePair<object, ConsequenceRunner> kv in runners)
            kv.Value.Tick(dt);
    }

    private void TickInjury(InjuryPartState part, Injury injury, float dt, InjurySettings s)
    {
        InjuryStage stage = injury.Stage;
        if (stage == null)
            return;
        injury.Age += dt;
        if (stage.bleedingPerSecond > 0f && !injury.Clotted)
        {
            bool wasBleeding = injury.IsBleeding;
            injury.BleedTime += dt * Mathf.Max(0.1f, injury.HealingFactor); // fast healers clot sooner
            if (wasBleeding && !injury.IsBleeding)
            {
                effectsDirty = true;
                MarkChanged();
            }
        }
        if (stage.conditionLossPerSecond > 0f && !part.Lost && part.Condition > 0f)
        {
            float before = part.Condition;
            part.Condition = Mathf.Max(0f, part.Condition - stage.conditionLossPerSecond * dt);
            if (part.Condition <= 0f)
                ApplyZeroCondition(part, default);
            if (Mathf.FloorToInt(before * 0.2f) != Mathf.FloorToInt(part.Condition * 0.2f))
                effectsDirty = true;
        }
        if (injury.CanWorsen)
        {
            // Prone to it (sickly): the time comes sooner. Resistant: when it comes, a chance to fight it off (Worsen).
            injury.UntreatedTime += dt * Mathf.Max(1f, injury.WorseningFactor);
            if (injury.UntreatedTime >= stage.worsenAfterSeconds)
                Worsen(part, injury);
            return;
        }
        float rate = injury.HealRate * s.healingSpeedMultiplier;
        if (rate <= 0f)
            return;
        float before01 = injury.HealProgress;
        injury.HealProgress = Mathf.Min(1f, injury.HealProgress + dt * rate / Mathf.Max(1f, stage.healSeconds));
        if (injury.HealProgress >= 1f)
            Healed(part, injury);
        else if (Mathf.FloorToInt(before01 * 20f) != Mathf.FloorToInt(injury.HealProgress * 20f))
            MarkChanged(); // the panel's progress bars move in 5% steps
    }

    private void Worsen(InjuryPartState part, Injury injury)
    {
        InjuryStage stage = injury.Stage;
        InjuryTypeDefinition into = string.IsNullOrWhiteSpace(stage.worsensInto) ? null : Settings.FindType(stage.worsensInto);
        InjuryResistances res = Resistances;
        float odds = res != null ? res.WorseningMultiplier(injury, into) : 1f;
        if (odds < 1f && UnityEngine.Random.value >= odds)
        {
            // Fought off (for now): the clock starts again.
            injury.UntreatedTime = 0f;
            if (into != null && into.HasTag(InjuryDefaults.TagInfection))
                Notify($"Your body fought off an infection in your {part.Name.ToLowerInvariant()}.");
            MarkChanged();
            return;
        }
        if (into != null && into != injury.Type)
        {
            // It causes another injury (an infection) and stays as it is.
            injury.WorseningStopped = true;
            AddInjury(part, into, 0, injury.Cause, false);
            Notify($"Your {part.Name.ToLowerInvariant()} got worse: {into.StageName(0)}.");
        }
        else if (injury.StageIndex < injury.Type.stages.Count - 1)
        {
            ChangeStage(injury, injury.StageIndex + 1, keepTreatment: true);
            Notify($"{injury.Type.StageName(injury.StageIndex - 1)} on your {part.Name.ToLowerInvariant()} got worse: {injury.Name}.");
        }
        else
        {
            injury.WorseningStopped = true; // nothing worse to become
        }
        effectsDirty = true;
        MarkChanged();
    }

    /// <summary>Updates every injury's resistance factors (bleeding, healing, worsening) from the character's resistances.</summary>
    public void RefreshFactors()
    {
        if (!RulesHere)
            return;
        for (int p = 0; p < parts.Count; p++)
            for (int i = 0; i < parts[p].injuries.Count; i++)
                UpdateFactors(parts[p].injuries[i]);
    }

    private void UpdateFactors(Injury injury)
    {
        InjuryResistances res = RulesHere ? Resistances : null;
        if (injury == null || res == null)
            return;
        InjuryBodyPart def = injury.Part != null ? injury.Part.Def : null;
        float bleed = res.BleedingMultiplier(injury.Type, def);
        float heal = res.HealingMultiplier(injury.Type, def);
        InjuryStage st = injury.Stage;
        InjuryTypeDefinition into = st != null && !string.IsNullOrWhiteSpace(st.worsensInto) ? Settings.FindType(st.worsensInto) : null;
        float worsen = res.WorseningMultiplier(injury, into);
        if (Mathf.Abs(bleed - injury.BleedingFactor) > 1e-3f || Mathf.Abs(heal - injury.HealingFactor) > 1e-3f)
        {
            injury.BleedingFactor = bleed;
            injury.HealingFactor = heal;
            effectsDirty = true; // the bleeding total and its status change
            MarkChanged();
        }
        injury.WorseningFactor = worsen;
    }

    private void Healed(InjuryPartState part, Injury injury)
    {
        if (injury.Type.healStageByStage && injury.StageIndex > 0)
        {
            ChangeStage(injury, injury.StageIndex - 1, keepTreatment: true);
            injury.HealProgress = 0f;
            return;
        }
        if (logChanges || Settings.logInjuries)
            Debug.Log($"[Injuries] {name}: {injury.Name} on {part.Name} healed.", this);
        RemoveInjury(injury);
    }

    private void TickBleeding(float dt, InjurySettings s)
    {
        float perSecond = BleedingPerSecond;
        CombatEntity e = Entity;
        if (perSecond <= 0f || e == null)
        {
            bleedDebt = 0f;
            return;
        }
        bleedDebt += perSecond * dt;
        if (bleedDebt < 0.5f)
            return; // in small steps: fewer damage events
        float amount = bleedDebt;
        bleedDebt = 0f;
        if (!s.bleeding.canKill && e.Health != null && !e.IsUnconscious)
            amount = Mathf.Min(amount, e.Health.CurrentValue - 1f);
        if (amount > 0f)
            e.ApplyDamage(new DamageInfo
            {
                amount = amount, source = null, target = e, point = e.Center, direction = Vector3.down, isPeriodic = true,
                unblockable = true, type = DamageType.True, delivery = DamageDelivery.Periodic, threatMultiplier = 0f,
            });
        if (s.bleeding.dripVfx != null && Time.time >= nextDrip)
        {
            nextDrip = Time.time + s.bleeding.dripInterval;
            AbilityPool.PlayVfx(s.bleeding.dripVfx, e.BasePosition + Vector3.up * 0.05f, Quaternion.identity, 0f);
        }
    }

    // ------------------------------------------------------------------ effects and limits
    private ConsequenceRunner Runner(object key)
    {
        if (!runners.TryGetValue(key, out ConsequenceRunner r))
            runners[key] = r = new ConsequenceRunner(this, key);
        return r;
    }

    private void RemoveRunner(object key)
    {
        if (runners.TryGetValue(key, out ConsequenceRunner r))
        {
            r.Clear();
            runners.Remove(key);
        }
    }

    private void ClearRunners()
    {
        foreach (KeyValuePair<object, ConsequenceRunner> kv in runners)
            kv.Value.Clear();
        runners.Clear();
    }

    /// <summary>Works out every part's impairment, the consequences to run and the actions stopped.</summary>
    private void RecomputeEffects()
    {
        effectsDirty = false;
        InjurySettings s = Settings;
        staleRunners.Clear();
        foreach (object key in runners.Keys)
            if (key != strainKey)
                staleRunners.Add(key);

        for (int p = 0; p < parts.Count; p++)
        {
            InjuryPartState part = parts[p];
            part.Impairment = ImpairmentOf(part);
            StatusConsequence c = null;
            float strength = part.Impairment;
            if (part.Lost)
            {
                c = part.Def.lost != null && part.Def.lost.HasAnyEffect ? part.Def.lost : part.Def.impaired;
                strength = 1f;
            }
            else if (part.Impairment >= 0.05f)
                c = part.Def.impaired;
            if (c != null && c.HasAnyEffect)
            {
                Runner(part).Set(c, strength);
                staleRunners.Remove(part);
            }
            for (int i = 0; i < part.injuries.Count; i++)
            {
                Injury inj = part.injuries[i];
                InjuryStage st = inj.Stage;
                if (st == null || st.consequence == null || !st.consequence.HasAnyEffect)
                    continue;
                Runner(inj).Set(st.consequence, 1f);
                staleRunners.Remove(inj);
            }
        }
        float bleed = BleedingPerSecond;
        if (bleed > 1e-4f && s.bleeding.status != null && s.bleeding.status.HasAnyEffect)
        {
            Runner(bleedingKey).Set(s.bleeding.status, Mathf.Clamp(bleed / Mathf.Max(0.01f, s.bleeding.heavyBleedingPerSecond), 0.25f, 2f));
            staleRunners.Remove(bleedingKey);
        }
        for (int i = 0; i < staleRunners.Count; i++)
            RemoveRunner(staleRunners[i]);
        staleRunners.Clear();
        RecomputeLimits();
    }

    /// <summary>
    /// Works out the effects and the stopped actions now (they are otherwise updated in the next frame): scripts that
    /// change injuries and read <see cref="Allows"/> or <see cref="Hands"/> at once.
    /// </summary>
    public void RefreshEffects()
    {
        if (RulesHere)
            RecomputeEffects();
        else
            RecomputeLimits();
    }

    /// <summary>A part's impairment: 1 when lost, else its worst injury, or its lost condition (80% at 0 condition).</summary>
    public static float ImpairmentOf(InjuryPartState part)
    {
        if (part == null)
            return 0f;
        if (part.Lost)
            return 1f;
        float imp = (1f - part.Condition01) * 0.8f;
        for (int i = 0; i < part.injuries.Count; i++)
            imp = Mathf.Max(imp, part.injuries[i].Impairment);
        return Mathf.Clamp01(imp);
    }

    private void RecomputeLimits()
    {
        InjurySettings s = Settings;
        BodyAction now = BodyAction.None;
        reasons.Clear();
        for (int p = 0; p < parts.Count; p++)
        {
            InjuryPartState part = parts[p];
            float imp = RulesHere ? part.Impairment : (part.Impairment = ImpairmentOf(part));
            string why = part.Lost ? $"{part.Name} is missing" : $"{part.Name} is injured";
            if (part.Def.actionRules != null)
                foreach (BodyActionRule rule in part.Def.actionRules)
                    if (rule != null && rule.actions != BodyAction.None && imp + 1e-4f >= rule.fromImpairment)
                        Block(ref now, BodyActions.ResolveSide(rule.actions, part.Def.side, s.mainHandSide), why);
            for (int i = 0; i < part.injuries.Count; i++)
            {
                InjuryStage st = part.injuries[i].Stage;
                if (st != null && st.blocks != BodyAction.None)
                    Block(ref now, BodyActions.ResolveSide(st.blocks, part.Def.side, s.mainHandSide), $"{part.injuries[i].Name} ({part.Name})");
            }
        }
        // How the legs let it move (limp, hop, crawl), what that stops, and the body's shape (collider, hitboxes).
        locomotion = ComputeLocomotion(s);
        BodyAction moveBlocks = LocomotionBlocks(s, locomotion);
        if (moveBlocks != BodyAction.None)
            Block(ref now, moveBlocks, locomotion.reason);
        ApplyBodyShape(s);
        blocked = now;
        var h = new HandCapability
        {
            mainHand = (now & BodyAction.MainHand) == 0,
            offHand = (now & BodyAction.OffHand) == 0,
            mainReason = ReasonFor(BodyAction.MainHand),
            offReason = ReasonFor(BodyAction.OffHand),
        };
        h.twoHanded = h.mainHand && h.offHand && (now & BodyAction.TwoHandedWeapons) == 0;
        h.twoHandedReason = h.twoHanded ? "" : !h.mainHand ? h.mainReason : !h.offHand ? h.offReason : ReasonFor(BodyAction.TwoHandedWeapons);
        SetHands(h);
    }

    private void Block(ref BodyAction into, BodyAction actions, string why)
    {
        into |= actions;
        foreach (BodyAction a in BodyActions.All)
            if ((actions & a) != 0 && !reasons.ContainsKey(a))
                reasons[a] = why;
    }

    private void SetHands(HandCapability h)
    {
        bool changed = h.mainHand != hands.mainHand || h.offHand != hands.offHand || h.twoHanded != hands.twoHanded;
        hands = h;
        if (!changed)
            return;
        HandsChanged?.Invoke();
        InventoryManager inv = InventoryManager.For(this);
        if (inv != null)
            inv.RefreshHands();
    }

    // ------------------------------------------------------------------ IBodyActionLimits
    public bool Allows(BodyAction action) => !Settings.enabled || (blocked & action) == 0;

    public string ReasonFor(BodyAction action)
    {
        foreach (BodyAction a in BodyActions.All)
            if ((action & a) != 0 && (blocked & a) != 0)
                return reasons.TryGetValue(a, out string r) ? r : BodyActions.Describe(a);
        return "";
    }

    public HandCapability Hands => Settings.enabled ? hands : HandCapability.Full;

    /// <summary>Every action stopped right now.</summary>
    public BodyAction Blocked => blocked;

    // ------------------------------------------------------------------ death and respawn
    private void OnDied(CombatEntity killer)
    {
        CancelTreatment(null);
        bleedDebt = 0f;
    }

    private void OnRevived()
    {
        if (!RulesHere || !Settings.clearOnRespawn)
            return;
        bool restore = Settings.reattachment.restoreLimbsOnRespawn;
        for (int i = 0; i < parts.Count; i++)
            if (parts[i].Lost && restore && parts[i].LostLimb != null)
                parts[i].LostLimb.Consumed = true;
        HealAll(restore);
    }

    // ------------------------------------------------------------------ feedback
    private void MarkChanged()
    {
        changedPending = true;
        Version++;
    }

    private void FlushChanged()
    {
        if (!changedPending)
            return;
        changedPending = false;
        try { Changed?.Invoke(); }
        catch (Exception ex) { Debug.LogException(ex, this); }
    }

    /// <summary>Tells the player something (the inventory's message line for the local player, the Message event).</summary>
    private void Notify(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        Message?.Invoke(text);
        if (IsLocalPlayer(Entity))
            InventoryManager.For(this)?.ShowMessage(text, InventoryFeedback.Kind.Info);
    }

    /// <summary>Is <paramref name="e"/> this machine's player?</summary>
    public static bool IsLocalPlayer(CombatEntity e)
    {
        if (e == null || e.Kind != CombatEntity.EntityKind.Player)
            return false;
        if (!GameNet.NetAuthority.IsNetworkedObject(e.gameObject))
            return GameNet.NetAuthority.HasLocalPlayer;
        return GameNet.NetAuthority.IsLocallyControlled(e.gameObject);
    }

    private void UpdateHudAlert()
    {
        if (!Settings.showHudAlert || !IsLocalPlayer(Entity))
        {
            if (hudAlert != null && hudAlert.gameObject.activeSelf)
                hudAlert.gameObject.SetActive(false);
            return;
        }
        if (hudAlert == null)
            hudAlert = InjuryHudAlert.Create(this);
        if (hudAlert != null)
        {
            if (!hudAlert.gameObject.activeSelf)
                hudAlert.gameObject.SetActive(true);
            hudAlert.Tick();
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying && isActiveAndEnabled)
            effectsDirty = true;
    }
}
