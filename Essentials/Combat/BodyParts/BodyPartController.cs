using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Body-part damage for one character (player or mob): finds which part each hit lands on, scales its damage by the
/// part, makes only the armour covering that part protect fully, and runs the part's effects (stagger, slow, sever...).
/// <para>
/// Location, in order: the part the attack aimed at (a leg sweep), the <see cref="BodyPartHitbox"/> collider that was
/// struck, the hitbox nearest the hit point, then the hit's height and side on the body. Damage over time has no
/// location; area damage uses the profile's default part.
/// </para>
/// It plugs into the combat system (<see cref="CombatEntity"/> interceptor, <see cref="CombatStats"/> defense), so
/// weapons, projectiles and abilities of players and mobs all use it. Add it to a character, or turn on
/// <c>Combat Settings ▸ Body Parts For Every Character</c>.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-30)]
public class BodyPartController : MonoBehaviour, IDamageInterceptor, ILocationalDefense
{
    /// <summary>Runtime state of one part of this character.</summary>
    public sealed class PartState
    {
        public BodyPartDefinition part;
        public float damageTaken;
        public int hits;
        public bool severed;
        public float nextStagger;
    }

    [Tooltip("The body parts. Empty = Combat Settings' default profile, else a built-in humanoid (head, torso, arms, legs).")]
    [SerializeField] private BodyPartProfile profile;
    [Tooltip("Hits closer than this to a hitbox collider count as hitting it (metres), for hits that report only a point.")]
    [SerializeField, Min(0f)] private float hitboxTolerance = 0.15f;
    [Tooltip("Log the part of every hit (debug).")]
    [SerializeField] private bool debugLog = false;
    [Tooltip("Scene view: the body-part bands, the hitboxes and the last hits (with the part and damage), so you can see where " +
             "attacks land. Bands and hitboxes show while the character is selected; hits show while playing.")]
    [SerializeField] private bool showInSceneView = true;
    [Tooltip("Draw the parts in the Scene view even when the character is not selected.")]
    [SerializeField] private bool alwaysShowParts = false;
    [Tooltip("Editor only: while playing, a label in the Game view shows each hit's part and damage where it landed (\"Head −14\").")]
    [SerializeField] private bool showHitsInGameView = true;
    [Tooltip("Seconds a hit stays marked in the Scene and Game views.")]
    [SerializeField, Min(0.5f)] private float hitMarkerTime = 4f;

    /// <summary>A recent hit, for the Scene view and the inspector.</summary>
    public struct RecentHit
    {
        public Vector3 point;
        public string part;
        public float damage;
        public float time;
        public int partIndex;
    }

    private readonly List<RecentHit> recentHits = new List<RecentHit>(8);

    /// <summary>The last hits (newest last), for debugging.</summary>
    public IReadOnlyList<RecentHit> RecentHits => recentHits;
    /// <summary>The hitboxes registered on this character.</summary>
    public IReadOnlyList<BodyPartHitbox> Hitboxes => hitboxes;
    public bool ShowInSceneView => showInSceneView;

    private static readonly Color[] PartColors =
    {
        new Color(0.95f, 0.35f, 0.35f), new Color(0.35f, 0.65f, 1f), new Color(0.45f, 0.85f, 0.45f), new Color(1f, 0.75f, 0.25f),
        new Color(0.75f, 0.5f, 1f), new Color(0.3f, 0.85f, 0.85f), new Color(1f, 0.5f, 0.8f), new Color(0.7f, 0.7f, 0.7f),
    };

    /// <summary>The colour of the part at <paramref name="index"/> in its profile (Scene view, inspectors).</summary>
    public static Color PartColor(int index) => index < 0 ? Color.white : PartColors[index % PartColors.Length];

    /// <summary>The colour of a part by name in <paramref name="p"/>.</summary>
    public static Color PartColor(BodyPartProfile p, string partName)
    {
        if (p == null || p.parts == null)
            return Color.white;
        for (int i = 0; i < p.parts.Count; i++)
            if (p.parts[i] != null && p.parts[i].Matches(partName))
                return PartColor(i);
        return Color.white;
    }

    /// <summary>A part was hit (after the damage was applied).</summary>
    public event Action<BodyPartHit> PartHit;
    /// <summary>A part was severed (part, the hit that did it).</summary>
    public event Action<BodyPartDefinition, DamageInfo> PartSevered;

    private readonly List<PartState> states = new List<PartState>(6);
    private readonly List<BodyPartHitbox> hitboxes = new List<BodyPartHitbox>(8);
    private readonly List<Transform> collapsedBones = new List<Transform>();
    private CombatEntity entity;
    private CombatStats stats;
    private EquipmentManager equipment;
    private BodyPartProfile runtimeProfile;
    private bool wasDead;
    private Transform[] boneCache;

    public int InterceptOrder => 0;

    /// <summary>The profile in use (assigned, Combat Settings' default, or the built-in humanoid).</summary>
    public BodyPartProfile Profile
    {
        get
        {
            if (profile != null) return profile;
            BodyPartProfile d = CombatSettings.Instance.defaultBodyPartProfile;
            if (d != null) return d;
            if (runtimeProfile == null) runtimeProfile = BodyPartProfile.CreateHumanoid();
            return runtimeProfile;
        }
        set
        {
            profile = value;
            states.Clear();
        }
    }

    public CombatEntity Entity => entity;

    /// <summary>True when a severed part stops this character from attacking with weapons.</summary>
    public bool WeaponAttacksDisabled { get; private set; }

    /// <summary>The body-part controller of a character, or null.</summary>
    public static BodyPartController For(Component anyPart) => anyPart != null ? anyPart.GetComponentInParent<BodyPartController>() : null;

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        entity = GetComponent<CombatEntity>();
        if (entity == null)
            entity = CombatEntity.Resolve(gameObject);
    }

    private void OnEnable()
    {
        if (entity == null)
            entity = CombatEntity.Resolve(gameObject);
        if (entity != null)
        {
            entity.AddDamageInterceptor(this);
            entity.Damaged += OnDamaged;
        }
        stats = CombatStats.For(this, addIfMissing: false);
        if (stats != null)
            stats.SetLocationalDefense(this);
        wasDead = entity != null && entity.IsDead;
    }

    private void OnDisable()
    {
        if (entity != null)
        {
            entity.RemoveDamageInterceptor(this);
            entity.Damaged -= OnDamaged;
        }
        if (stats != null)
            stats.SetLocationalDefense(null);
    }

    private void OnDestroy()
    {
        if (runtimeProfile != null)
            Destroy(runtimeProfile);
    }

    private void Update()
    {
        // CombatStats may be added after this component (players get it from their equipment).
        if (stats == null)
        {
            stats = CombatStats.For(this, addIfMissing: false);
            if (stats != null)
                stats.SetLocationalDefense(this);
        }
        // Revived (respawned) characters get their parts back.
        bool dead = entity != null && entity.IsDead;
        if (wasDead && !dead)
            ResetParts();
        wasDead = dead;
    }

    private void LateUpdate()
    {
        // Collapsed bones stay collapsed even if an animation writes their scale.
        for (int i = 0; i < collapsedBones.Count; i++)
            if (collapsedBones[i] != null)
                collapsedBones[i].localScale = Vector3.zero;
    }

    internal void RegisterHitbox(BodyPartHitbox h)
    {
        if (h != null && !hitboxes.Contains(h))
            hitboxes.Add(h);
    }

    internal void UnregisterHitbox(BodyPartHitbox h) => hitboxes.Remove(h);

    // ------------------------------------------------------------------ state
    public PartState GetState(BodyPartDefinition part)
    {
        if (part == null)
            return null;
        for (int i = 0; i < states.Count; i++)
            if (states[i].part == part)
                return states[i];
        var s = new PartState { part = part };
        states.Add(s);
        return s;
    }

    public PartState GetState(string partName) => GetState(Profile.Find(partName));

    public bool IsSevered(string partName)
    {
        BodyPartDefinition p = Profile.Find(partName);
        if (p == null) return false;
        for (int i = 0; i < states.Count; i++)
            if (states[i].part == p) return states[i].severed;
        return false;
    }

    /// <summary>Heals every part: damage records, severed limbs (bones and meshes come back), lasting penalties.</summary>
    public void ResetParts()
    {
        states.Clear();
        for (int i = 0; i < collapsedBones.Count; i++)
            if (collapsedBones[i] != null)
                collapsedBones[i].localScale = Vector3.one;
        collapsedBones.Clear();
        foreach (Renderer r in hiddenRenderers)
            if (r != null) r.enabled = true;
        hiddenRenderers.Clear();
        WeaponAttacksDisabled = false;
    }

    // ------------------------------------------------------------------ location
    /// <summary>The part a hit lands on (see the class summary for the order), or null when the profile has no parts.</summary>
    public BodyPartDefinition Locate(in DamageInfo info)
    {
        BodyPartProfile p = Profile;
        if (p == null)
            return null;

        BodyPartDefinition part = null;
        if (!string.IsNullOrWhiteSpace(info.aimedBodyPart))
            part = p.Find(info.aimedBodyPart);
        if (part == null && info.hitCollider != null)
        {
            BodyPartHitbox hb = info.hitCollider.GetComponent<BodyPartHitbox>();
            if (hb != null)
                part = p.whenSeveralParts == SeveralPartsRule.NearestToContact || info.point == Vector3.zero
                    ? p.Find(hb.part)
                    : NearestHitbox(p, info.point) ?? p.Find(hb.part);
        }
        if (part == null && info.isPeriodic)
            part = p.Default; // damage over time: no new wound location
        bool noPoint = info.point == Vector3.zero;
        if (part == null && (noPoint || (info.delivery == DamageDelivery.Area && p.areaDamageUsesDefaultPart)))
            part = p.Default;
        if (part == null && hitboxes.Count > 0)
            part = NearestHitbox(p, info.point);
        if (part == null)
            part = LocateByHeight(p, info.point);
        return Redirect(p, part);
    }

    /// <summary>
    /// The part of the hitbox(es) at <paramref name="point"/> (within Hitbox Tolerance). Several touched at once: the profile's
    /// When Several Parts rule picks one - a hit is one part, never several.
    /// </summary>
    private BodyPartDefinition NearestHitbox(BodyPartProfile p, Vector3 point)
    {
        BodyPartDefinition best = null;
        float bestSqr = float.MaxValue;
        float tol = hitboxTolerance * hitboxTolerance;
        for (int i = hitboxes.Count - 1; i >= 0; i--)
        {
            BodyPartHitbox h = hitboxes[i];
            if (h == null) { hitboxes.RemoveAt(i); continue; }
            Collider c = h.Collider;
            if (c == null || !c.enabled || !h.gameObject.activeInHierarchy)
                continue;
            Vector3 closest = c.ClosestPoint(point); // inside = the point itself
            float d = (closest - point).sqrMagnitude;
            if (d > tol)
                continue;
            BodyPartDefinition part = p.Find(h.part);
            if (part == null)
                continue;
            bool better;
            switch (p.whenSeveralParts)
            {
                case SeveralPartsRule.MostVulnerable:
                    better = best == null || part.damageMultiplier > best.damageMultiplier || (part.damageMultiplier == best.damageMultiplier && d < bestSqr);
                    break;
                case SeveralPartsRule.LeastVulnerable:
                    better = best == null || part.damageMultiplier < best.damageMultiplier || (part.damageMultiplier == best.damageMultiplier && d < bestSqr);
                    break;
                default:
                    better = d < bestSqr;
                    break;
            }
            if (better)
            {
                best = part;
                bestSqr = d;
            }
        }
        return best;
    }

    private BodyPartDefinition LocateByHeight(BodyPartProfile p, Vector3 point)
    {
        if (entity == null)
            return p.Default;
        Vector3 basePos = entity.BasePosition;
        float h01 = Mathf.Clamp01((point.y - basePos.y) / Mathf.Max(0.1f, entity.Height));
        Vector3 right = entity.transform.right;
        right.y = 0f;
        Vector3 offset = point - basePos;
        offset.y = 0f;
        float lateral = right.sqrMagnitude > 1e-6f ? Vector3.Dot(offset, right.normalized) / Mathf.Max(0.05f, entity.Radius) : 0f;
        return p.Locate(h01, lateral);
    }

    /// <summary>A severed part's hits go to its redirect part (one level).</summary>
    private BodyPartDefinition Redirect(BodyPartProfile p, BodyPartDefinition part)
    {
        if (part == null)
            return null;
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].part != part || !states[i].severed)
                continue;
            BodyPartDefinition to = p.Find(part.redirectWhenSevered);
            return to != null ? to : p.Default;
        }
        return part;
    }

    // ------------------------------------------------------------------ damage
    bool IDamageInterceptor.InterceptDamage(ref DamageInfo info)
    {
        if (!isActiveAndEnabled || info.isPeriodic || info.delivery == DamageDelivery.Periodic)
            return true;
        BodyPartDefinition part = Locate(info);
        if (part == null)
            return true;
        info.bodyPart = part;
        info.amount *= Mathf.Max(0f, part.damageMultiplier) * Mathf.Max(0f, Profile.globalDamageScale);
        if (debugLog)
            Debug.Log($"[Body Parts] {name}: hit on {part.name} ×{part.damageMultiplier:0.##} → {info.amount:0.#}", this);
        return true;
    }

    /// <summary>
    /// Defense against a hit on a part: the character's own value without armour, plus the armour covering the part in
    /// full, plus the other armour by the profile's Other Armor Share, plus the part's natural armour.
    /// </summary>
    public float DefenseAgainst(in DamageInfo info, CombatStatType stat, float total)
    {
        BodyPartDefinition part = info.bodyPart;
        if (part == null)
            return total;
        float all = 0f, covering = 0f;
        EquipmentManager eq = Equipment;
        if (eq != null)
        {
            IReadOnlyList<EquipmentManager.Entry> entries = eq.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!(entries[i].Item is ArmorSO a))
                    continue;
                float v = stat == CombatStatType.MagicResistance ? a.GetEffectiveMagicDefense() : a.GetEffectiveDefense();
                all += v;
                if (part.IsProtectedBy(a.ArmorSlotType))
                    covering += v;
            }
        }
        float share = Profile.otherArmorShare;
        float natural = stat == CombatStatType.MagicResistance ? part.bonusMagicResistance : part.bonusDefense;
        return total - all + covering + (all - covering) * share + natural;
    }

    private EquipmentManager Equipment
    {
        get
        {
            if (equipment == null)
                equipment = EquipmentManager.For(this, addIfMissing: false);
            return equipment;
        }
    }

    private void OnDamaged(DamageInfo info)
    {
        BodyPartDefinition part = info.bodyPart;
        if (part == null || info.amount <= 0f || entity == null)
            return;
        PartState state = GetState(part);
        state.damageTaken += info.amount;
        state.hits++;
        float maxHp = entity.MaxHealth > 0f ? entity.MaxHealth : 100f;
        bool killing = entity.Health != null ? entity.Health.CurrentValue <= 0f : entity.IsDead;
        var hit = new BodyPartHit
        {
            controller = this,
            target = entity,
            part = part,
            state = state,
            info = info,
            damageFraction = info.amount / maxHp,
            partDamageFraction = state.damageTaken / maxHp,
            killingBlow = killing,
        };
        if (part.effects != null)
        {
            for (int i = 0; i < part.effects.Count; i++)
            {
                BodyPartEffect e = part.effects[i];
                if (e == null)
                    continue;
                try
                {
                    if (e.ShouldApply(hit))
                        e.Apply(hit);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }
        }
        PartHit?.Invoke(hit);

        if (showInSceneView)
        {
            if (recentHits.Count >= 8)
                recentHits.RemoveAt(0);
            BodyPartProfile prof = Profile;
            recentHits.Add(new RecentHit
            {
                point = info.point.sqrMagnitude > 1e-6f ? info.point : entity.BasePosition + Vector3.up * entity.Height * 0.5f,
                part = part.name,
                damage = info.amount,
                time = Time.time,
                partIndex = prof != null && prof.parts != null ? prof.parts.IndexOf(part) : -1,
            });
        }
    }

    // ------------------------------------------------------------------ severing
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();

    /// <summary>Severs a part: hides its bones and meshes, spawns the severed piece, applies lasting penalties.</summary>
    public void Sever(BodyPartDefinition part, SeverBodyPartEffect how, in DamageInfo info)
    {
        if (part == null)
            return;
        PartState state = GetState(part);
        if (state.severed)
            return;
        state.severed = true;

        Transform firstBone = null;
        if (how != null)
        {
            if (how.bones != null)
                foreach (string b in how.bones)
                {
                    Transform t = FindBone(b);
                    if (t == null) continue;
                    if (firstBone == null) firstBone = t;
                    collapsedBones.Add(t);
                    t.localScale = Vector3.zero;
                }
            if (how.renderers != null)
                foreach (string rn in how.renderers)
                {
                    Transform t = FindBone(rn);
                    Renderer r = t != null ? t.GetComponent<Renderer>() : null;
                    if (r == null) continue;
                    if (firstBone == null) firstBone = t;
                    r.enabled = false;
                    hiddenRenderers.Add(r);
                }
            Vector3 at = firstBone != null ? firstBone.position : (info.point != Vector3.zero ? info.point : entity.Center);
            Quaternion rot = firstBone != null ? firstBone.rotation : Quaternion.identity;
            if (how.severedPrefab != null)
            {
                GameObject piece = Instantiate(how.severedPrefab, at, rot);
                Rigidbody rb = piece.GetComponent<Rigidbody>();
                if (rb != null && info.direction.sqrMagnitude > 1e-4f)
                    rb.AddForce((info.direction.normalized + Vector3.up * 0.5f) * 3f, ForceMode.VelocityChange);
                if (how.severedLifetime > 0f)
                    Destroy(piece, how.severedLifetime);
            }
            if (how.vfx != null)
                AbilityPool.PlayVfx(how.vfx, at, rot, 0f);
            if (how.sound != null)
                AbilityPool.PlaySound(how.sound, at, 1f);
            if (how.lastingSlow > 0f && entity != null && !entity.IsDead)
                entity.ApplyControl(ControlType.Slow, 3600f, info.source, how.lastingSlow);
            if (how.disablesWeaponAttacks)
                WeaponAttacksDisabled = true;
        }
        if (debugLog)
            Debug.Log($"[Body Parts] {name}: {part.name} severed.", this);
        PartSevered?.Invoke(part, info);
    }

    private Transform FindBone(string boneName)
    {
        if (string.IsNullOrWhiteSpace(boneName))
            return null;
        if (boneCache == null)
            boneCache = GetComponentsInChildren<Transform>(true);
        string n = boneName.Trim();
        for (int i = 0; i < boneCache.Length; i++)
            if (boneCache[i] != null && string.Equals(boneCache[i].name, n, StringComparison.OrdinalIgnoreCase))
                return boneCache[i];
        return null;
    }

    /// <summary>Adds controllers to characters when Combat Settings ▸ Body Parts For Every Character is on.</summary>
    internal static void EnsureFor(CombatEntity e)
    {
        if (e == null || !CombatSettings.Instance.bodyPartsForEveryCharacter)
            return;
        if (e.GetComponent<BodyPartController>() == null)
            e.gameObject.AddComponent<BodyPartController>();
    }

#if UNITY_EDITOR
    /// <summary>
    /// The character's body for drawing: its combat entity, else (in Edit Mode, before one exists) its Character
    /// Controller, capsule collider or renderers.
    /// </summary>
    private bool Body(out Vector3 basePos, out float height, out float radius)
    {
        CombatEntity e = entity != null ? entity : GetComponent<CombatEntity>();
        if (e == null)
            e = GetComponentInParent<CombatEntity>();
        if (e != null && e.Height > 0.05f)
        {
            basePos = e.BasePosition;
            height = e.Height;
            radius = Mathf.Max(0.05f, e.Radius);
            return true;
        }
        CharacterController cc = GetComponentInChildren<CharacterController>();
        if (cc != null)
        {
            float s = Mathf.Abs(cc.transform.lossyScale.y);
            height = cc.height * s;
            radius = cc.radius * Mathf.Abs(cc.transform.lossyScale.x);
            basePos = cc.transform.TransformPoint(cc.center) - Vector3.up * height * 0.5f;
            return true;
        }
        CapsuleCollider cap = GetComponentInChildren<CapsuleCollider>();
        if (cap != null && cap.GetComponent<BodyPartHitbox>() == null)
        {
            float s = Mathf.Abs(cap.transform.lossyScale.y);
            height = cap.height * s;
            radius = cap.radius * Mathf.Abs(cap.transform.lossyScale.x);
            basePos = cap.transform.TransformPoint(cap.center) - Vector3.up * height * 0.5f;
            return true;
        }
        var bounds = new Bounds(transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        basePos = new Vector3(bounds.center.x, any ? bounds.min.y : transform.position.y, bounds.center.z);
        height = any ? bounds.size.y : 1.8f;
        radius = any ? Mathf.Max(0.15f, Mathf.Min(bounds.extents.x, bounds.extents.z)) : 0.35f;
        return true;
    }

    /// <summary>Is this character (or one of its children, or a parent) selected?</summary>
    private bool IsSelected()
    {
        foreach (Transform t in UnityEditor.Selection.transforms)
            if (t != null && (t == transform || t.IsChildOf(transform) || transform.IsChildOf(t)))
                return true;
        return false;
    }

    private void OnDrawGizmos()
    {
        if (!showInSceneView)
            return;
        if (alwaysShowParts || IsSelected())
            DrawParts();
        if (!Application.isPlaying || recentHits.Count == 0)
            return;
        // The last hits: a sphere where each landed, with the part and the damage.
        for (int i = 0; i < recentHits.Count; i++)
        {
            RecentHit h = recentHits[i];
            float age = Time.time - h.time;
            if (age > hitMarkerTime)
                continue;
            Color c = PartColor(h.partIndex);
            c.a = Mathf.Lerp(1f, 0.15f, age / hitMarkerTime);
            Gizmos.color = c;
            Gizmos.DrawSphere(h.point, 0.06f);
            UnityEditor.Handles.Label(h.point + Vector3.up * 0.12f, $"{h.part} −{h.damage:0.#}", LabelStyle(c));
        }
    }

    private static GUIStyle labelStyle;

    private static GUIStyle LabelStyle(Color c)
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.box) { fontStyle = FontStyle.Bold, fontSize = 11, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(4, 4, 1, 1) };
        }
        labelStyle.normal.textColor = Color.Lerp(c, Color.white, 0.35f);
        return labelStyle;
    }

    /// <summary>Each part's height band in its colour (side parts on the outer sides) and the hitboxes.</summary>
    private void DrawParts()
    {
        BodyPartProfile p = profile != null ? profile : CombatSettings.Instance.defaultBodyPartProfile;
        if (p == null)
            p = runtimeProfile != null ? runtimeProfile : (runtimeProfile = BodyPartProfile.CreateHumanoid());
        if (p.parts == null || !Body(out Vector3 b, out float hgt, out float rad))
            return;
        hgt = Mathf.Max(0.1f, hgt);
        Vector3 right = transform.right, fwd = transform.forward;
        for (int i = 0; i < p.parts.Count; i++)
        {
            BodyPartDefinition part = p.parts[i];
            if (part == null)
                continue;
            Color c = PartColor(i);
            float lo = Mathf.Min(part.heightRange.x, part.heightRange.y) * hgt;
            float hi = Mathf.Max(part.heightRange.x, part.heightRange.y) * hgt;
            string label = $"{part.name} ×{part.damageMultiplier:0.##}";
            if (part.side == BodySide.Any)
            {
                UnityEditor.Handles.color = new Color(c.r, c.g, c.b, 0.9f);
                UnityEditor.Handles.DrawWireDisc(b + Vector3.up * lo, Vector3.up, rad);
                UnityEditor.Handles.DrawWireDisc(b + Vector3.up * hi, Vector3.up, rad);
                foreach (Vector3 d in new[] { right, -right, fwd, -fwd })
                    UnityEditor.Handles.DrawLine(b + Vector3.up * lo + d * rad, b + Vector3.up * hi + d * rad);
                UnityEditor.Handles.color = new Color(c.r, c.g, c.b, 0.12f);
                UnityEditor.Handles.DrawSolidDisc(b + Vector3.up * (lo + hi) * 0.5f, Vector3.up, rad);
                UnityEditor.Handles.Label(b + Vector3.up * (lo + hi) * 0.5f - right * (rad + 0.35f), label, LabelStyle(c));
            }
            else
            {
                // Side parts take the outer part of the body: from Side Offset × radius to the edge (and a little beyond).
                float inner = Mathf.Clamp01(part.sideOffset) * rad, outer = rad * 1.35f;
                Vector3 size = new Vector3(outer - inner, hi - lo, rad * 1.2f);
                Gizmos.color = new Color(c.r, c.g, c.b, 0.9f);
                bool left = part.side == BodySide.Left || part.side == BodySide.EitherSide;
                bool rightSide = part.side == BodySide.Right || part.side == BodySide.EitherSide;
                Matrix4x4 old = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(b, transform.rotation, Vector3.one);
                if (left) Gizmos.DrawWireCube(new Vector3(-(inner + outer) * 0.5f, (lo + hi) * 0.5f, 0f), size);
                if (rightSide) Gizmos.DrawWireCube(new Vector3((inner + outer) * 0.5f, (lo + hi) * 0.5f, 0f), size);
                Gizmos.color = new Color(c.r, c.g, c.b, 0.12f);
                if (left) Gizmos.DrawCube(new Vector3(-(inner + outer) * 0.5f, (lo + hi) * 0.5f, 0f), size);
                if (rightSide) Gizmos.DrawCube(new Vector3((inner + outer) * 0.5f, (lo + hi) * 0.5f, 0f), size);
                Gizmos.matrix = old;
                UnityEditor.Handles.Label(b + Vector3.up * (lo + hi) * 0.5f + (rightSide ? right : -right) * (outer + 0.35f), label, LabelStyle(c));
            }
        }
        IEnumerable<BodyPartHitbox> boxes = Application.isPlaying ? (IEnumerable<BodyPartHitbox>)hitboxes : GetComponentsInChildren<BodyPartHitbox>(true);
        foreach (BodyPartHitbox h in boxes)
        {
            if (h == null || h.Collider == null)
                continue;
            DrawHitbox(h, PartColor(p, h.part));
        }
    }

    /// <summary>Editor, while playing: each recent hit's part and damage, over the character in the Game view.</summary>
    private void OnGUI()
    {
        if (!showHitsInGameView || recentHits.Count == 0)
            return;
        Camera cam = Camera.main;
        if (cam == null)
            return;
        for (int i = 0; i < recentHits.Count; i++)
        {
            RecentHit h = recentHits[i];
            float age = Time.time - h.time;
            if (age > hitMarkerTime)
                continue;
            Vector3 sp = cam.WorldToScreenPoint(h.point + Vector3.up * (0.1f + age * 0.15f));
            if (sp.z <= 0f)
                continue;
            Color c = PartColor(h.partIndex);
            c.a = Mathf.Lerp(1f, 0f, age / hitMarkerTime);
            GUIStyle st = LabelStyle(c);
            string text = $"{h.part} −{h.damage:0.#}";
            Vector2 size = st.CalcSize(new GUIContent(text));
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, c.a);
            GUI.Label(new Rect(sp.x - size.x * 0.5f, Screen.height - sp.y - size.y, size.x, size.y), text, st);
            GUI.color = old;
        }
    }

    /// <summary>Draws a hitbox's collider in <paramref name="c"/> with its part name.</summary>
    public static void DrawHitbox(BodyPartHitbox h, Color c)
    {
        Collider col = h.Collider;
        Gizmos.color = c;
        Matrix4x4 old = Gizmos.matrix;
        Transform t = col.transform;
        switch (col)
        {
            case SphereCollider sc:
                Gizmos.matrix = t.localToWorldMatrix;
                Gizmos.DrawWireSphere(sc.center, sc.radius);
                break;
            case BoxCollider bc:
                Gizmos.matrix = t.localToWorldMatrix;
                Gizmos.DrawWireCube(bc.center, bc.size);
                break;
            case CapsuleCollider cc:
                {
                    Vector3 axis = cc.direction == 0 ? Vector3.right : cc.direction == 2 ? Vector3.forward : Vector3.up;
                    float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
                    Vector3 a = t.TransformPoint(cc.center + axis * half), z = t.TransformPoint(cc.center - axis * half);
                    float r = cc.radius * Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Abs(t.lossyScale.z));
                    Gizmos.DrawWireSphere(a, r);
                    Gizmos.DrawWireSphere(z, r);
                    Vector3 side = Vector3.Cross((a - z).normalized, Vector3.up);
                    if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
                    side = side.normalized * r;
                    Gizmos.DrawLine(a + side, z + side);
                    Gizmos.DrawLine(a - side, z - side);
                    break;
                }
            default:
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
                break;
        }
        Gizmos.matrix = old;
        UnityEditor.Handles.color = c;
        UnityEditor.Handles.Label(col.bounds.center, h.part);
    }
#endif
}
