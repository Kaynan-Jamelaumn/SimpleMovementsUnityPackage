using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shows what a character has equipped: the item of each hand (in the hand while fighting, sheathed on the back or the
/// hip when not in use), an off-hand item stowed by a two-handed weapon, and worn armour (skinned onto the character's
/// skeleton, or attached to the slot's bone). Each item has exactly one model, created once and moved between points,
/// so swapping, sheathing and unequipping never duplicate or leave models behind.
/// <para>
/// Attachment points (<see cref="AttachPoint"/>) are your own transforms (Attachment Points), else the humanoid rig's
/// bones, else created on the character. Where an item goes and how it sits is item data (Item ▸ Equipment Visuals,
/// Armor ▸ Armor Visuals); nothing here is weapon-specific.
/// </para>
/// Added to the player automatically by the <see cref="InventoryManager"/> (Equipment Visuals option). Scripts and NPCs
/// can drive it with <see cref="SetHands"/>.
/// </summary>
[DisallowMultipleComponent]
public class EquipmentVisuals : MonoBehaviour, IWeaponVisualHost
{
    [Serializable]
    public class SocketOverride
    {
        public AttachPoint point = AttachPoint.Back;
        public Transform transform;
    }

    private enum Placement { None, Hand, Sheathed, Worn }

    private sealed class Visual
    {
        public ItemSO item;
        public InventoryItem invItem;
        public GameObject instance;
        public GameObject pairInstance;
        public Placement placement;
        public readonly List<Renderer> hidden = new List<Renderer>();
    }

    [Header("Attachment Points")]
    [Tooltip("Your own transforms for attachment points (a 'BackSocket' child, a quiver...). Points not listed are found on the " +
             "humanoid rig, or created next to the closest bone.")]
    [SerializeField] private List<SocketOverride> sockets = new List<SocketOverride>();
    [Tooltip("The character model's Animator (found when empty). Humanoid rigs give every bone automatically.")]
    [SerializeField] private Animator animator;

    [Header("Hands")]
    [Tooltip("Show the hands' items (weapons, shields, held items) on the character.")]
    [SerializeField] private bool showHandItems = true;
    [Tooltip("Seconds without fighting before weapons are sheathed (0 = never: weapons stay in hand, as before).")]
    [SerializeField, Min(0f)] private float autoSheatheDelay = 6f;
    [Tooltip("Weapons start sheathed (drawn as soon as the character attacks, blocks or is hurt).")]
    [SerializeField] private bool startSheathed = true;
    [Tooltip("Actions (by name in the player's input actions) that draw / sheathe by hand.")]
    [SerializeField] private string[] toggleActionNames = { "Sheathe", "ToggleWeapon", "DrawWeapon", "Holster" };
    [Tooltip("Key that draws / sheathes when the input actions have no such action (None = no key).")]
    [SerializeField] private Key toggleFallbackKey = Key.None;

    [Header("Armor")]
    [Tooltip("Show worn armour pieces (their Armor Model) on the character.")]
    [SerializeField] private bool showArmor = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    /// <summary>The weapons were drawn (true) or sheathed (false).</summary>
    public event Action<bool> DrawnChanged;

    private readonly Visual mainHand = new Visual();
    private readonly Visual offHand = new Visual();
    private bool offHandStowed;
    private readonly Dictionary<object, Visual> armor = new Dictionary<object, Visual>(ReferenceComparer<object>.Instance);
    private readonly Dictionary<AttachPoint, Transform> resolved = new Dictionary<AttachPoint, Transform>();
    private readonly Dictionary<Renderer, int> hideCounts = new Dictionary<Renderer, int>(ReferenceComparer<Renderer>.Instance);
    private readonly List<object> keyBuffer = new List<object>();
    private Dictionary<string, Transform> boneByName;
    private WeaponController weapons;
    private BlockController block;
    private EquipmentManager equipment;
    private CombatEntity entity;
    private Transform mainSocketOverride;
    private bool drawn;
    private float lastCombatTime = -999f;
    private CombatInputBinding toggleInput;
    private bool subWeapons, subBlock, subEquipment, subEntity;
    private bool drivenByInventory;
    private bool handsInitialized;

    /// <summary>Are the weapons in the hands (true) or sheathed (false)?</summary>
    public bool IsDrawn => drawn;

    /// <summary>The equipment visuals of a character, or null.</summary>
    public static EquipmentVisuals For(Component anyPart)
    {
        if (anyPart == null)
            return null;
        EquipmentVisuals v = anyPart.GetComponentInParent<EquipmentVisuals>();
        if (v != null)
            return v;
        PlayerStatusController ps = anyPart.GetComponentInParent<PlayerStatusController>();
        return ps != null ? ps.GetComponentInChildren<EquipmentVisuals>() : null;
    }

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        weapons = GetComponentInChildren<WeaponController>();
        block = GetComponentInChildren<BlockController>();
        entity = GetComponent<CombatEntity>();
        if (entity == null)
            entity = CombatEntity.Resolve(gameObject);
        drawn = !startSheathed || autoSheatheDelay <= 0f;
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        Subscribe();
        if (CombatInputBinding.IsPlayer(this))
            toggleInput = CombatInputBinding.Create(this, null, toggleActionNames, toggleFallbackKey, MouseFallback.None, "Draw / sheathe");
        RebuildArmor();
        ApplyPlacements();
    }

    /// <summary>Listens to the weapons, the block, the equipment and the body (each as soon as it exists: they may be added later).</summary>
    private void Subscribe()
    {
        if (!subWeapons)
        {
            if (weapons == null)
                weapons = GetComponentInChildren<WeaponController>();
            if (weapons != null)
            {
                weapons.Visuals = this;
                weapons.AttackStarted += OnAttackStarted;
                weapons.RangedFired += OnRangedFired;
                weapons.WeaponChanged += OnWeaponsChanged;
                weapons.OffHandChanged += OnWeaponsChanged;
                subWeapons = true;
            }
        }
        if (!subBlock)
        {
            if (block == null)
                block = GetComponentInChildren<BlockController>();
            if (block != null)
            {
                block.BlockChanged += OnBlockChanged;
                subBlock = true;
            }
        }
        if (!subEquipment)
        {
            if (equipment == null)
                equipment = EquipmentManager.For(this, addIfMissing: false);
            if (equipment != null)
            {
                equipment.Changed += RebuildArmor;
                subEquipment = true;
                RebuildArmor();
            }
        }
        if (!subEntity)
        {
            if (entity == null)
                entity = CombatEntity.Resolve(gameObject);
            if (entity != null)
            {
                entity.Damaged += OnDamaged;
                subEntity = true;
            }
        }
    }

    private void OnDisable()
    {
        if (subWeapons && weapons != null)
        {
            if (ReferenceEquals(weapons.Visuals, this))
                weapons.Visuals = null;
            weapons.AttackStarted -= OnAttackStarted;
            weapons.RangedFired -= OnRangedFired;
            weapons.WeaponChanged -= OnWeaponsChanged;
            weapons.OffHandChanged -= OnWeaponsChanged;
        }
        if (subBlock && block != null)
            block.BlockChanged -= OnBlockChanged;
        if (subEquipment && equipment != null)
            equipment.Changed -= RebuildArmor;
        if (subEntity && entity != null)
            entity.Damaged -= OnDamaged;
        subWeapons = subBlock = subEquipment = subEntity = false;
    }

    private void OnDestroy()
    {
        toggleInput?.Dispose();
        DestroyVisual(mainHand);
        DestroyVisual(offHand);
        foreach (Visual v in armor.Values)
            DestroyVisual(v);
        armor.Clear();
    }

    private void Update()
    {
        if (!subWeapons || !subBlock || !subEquipment || !subEntity)
        {
            // Parts added after this one (the inventory adds the block controller and the equipment later).
            if (Time.frameCount % 15 == 0)
                Subscribe();
        }
        if (toggleInput != null && toggleInput.Pressed && (weapons == null || !weapons.InputBlocked))
            SetDrawn(!drawn);
        if (drawn && autoSheatheDelay > 0f && Time.time - lastCombatTime > autoSheatheDelay && !InCombatAction())
            SetDrawn(false);
    }

    private bool InCombatAction() =>
        (weapons != null && (weapons.IsAttacking || weapons.IsReloading(WeaponHandSide.Main) || weapons.IsReloading(WeaponHandSide.Off))) ||
        (block != null && block.IsBlocking);

    // ------------------------------------------------------------------ events
    private void OnAttackStarted(AttackType input, AttackComponent attack) => MarkCombat();
    private void OnRangedFired(WeaponHandSide side, int count) => MarkCombat();
    private void OnBlockChanged(bool up)
    {
        if (up) MarkCombat();
    }

    private void OnDamaged(DamageInfo info)
    {
        if (info.amount > 0f && !info.isPeriodic)
            MarkCombat();
    }

    private void OnWeaponsChanged()
    {
        if (weapons == null)
            return;
        if (!drivenByInventory)
        {
            // No inventory drives the hands (scripts, NPCs): follow the weapon controller.
            WeaponSO main = weapons.EquippedWeapon;
            SetVisualItem(mainHand, main, weapons.HeldItem);
            ItemSO off = weapons.OffHandWeapon != null ? (ItemSO)weapons.OffHandWeapon : weapons.OffHandShield;
            InventoryItem offItem = weapons.OffHandWeapon != null ? weapons.OffHandItem : weapons.OffHandShieldItem;
            SetVisualItem(offHand, off, offItem);
        }
        ApplyPlacements();
    }

    private void MarkCombat()
    {
        lastCombatTime = Time.time;
        if (!drawn)
            SetDrawn(true);
    }

    // ------------------------------------------------------------------ public API
    /// <summary>Puts the hands' items in the hands now (attacks, blocks, aiming).</summary>
    public void DrawWeapons()
    {
        lastCombatTime = Time.time;
        if (!drawn)
            SetDrawn(true);
    }

    /// <summary>Draws (true) or sheathes (false) the hands' items.</summary>
    public void SetDrawn(bool value)
    {
        if (drawn == value)
            return;
        drawn = value;
        if (value)
            lastCombatTime = Time.time;
        ApplyPlacements();
        DrawnChanged?.Invoke(value);
        if (debugLog)
            Debug.Log($"[Equipment Visuals] {(value ? "Drawn" : "Sheathed")}.", this);
    }

    /// <summary>
    /// What the hands hold: <paramref name="main"/> (the selected hotbar item), <paramref name="off"/> (the Off Hand slot's
    /// item) and whether the off-hand item is stowed by a two-handed weapon.
    /// </summary>
    public void SetHands(InventoryItem main, InventoryItem off, bool offStowed)
    {
        drivenByInventory = true;
        SetMainHandItem(main, false);
        SetVisualItem(offHand, off != null ? off.itemScriptableObject : null, off);
        offHandStowed = offStowed && off != null;
        ApplyPlacements();
    }

    /// <summary>The item of the main hand (the hotbar's selected item).</summary>
    public void SetMainHandItem(InventoryItem main) => SetMainHandItem(main, true);

    private void SetMainHandItem(InventoryItem main, bool apply)
    {
        drivenByInventory = true;
        ItemSO before = mainHand.item;
        ItemSO item = main != null ? main.itemScriptableObject : null;
        SetVisualItem(mainHand, item, main);
        // Choosing a weapon (hotbar) draws it; the first setup at start does not.
        if (handsInitialized && item != before && item is WeaponSO)
            DrawWeapons();
        handsInitialized = true;
        if (apply)
            ApplyPlacements();
    }

    /// <summary>The socket used for the main hand (the inventory's Hand Parent; null = the weapon hand).</summary>
    public void SetMainHandSocket(Transform socket)
    {
        if (mainSocketOverride == socket)
            return;
        mainSocketOverride = socket;
        ApplyPlacements();
    }

    /// <summary>The model of a hand's item (in the hand or sheathed), or null.</summary>
    public Transform ModelFor(WeaponHandSide side)
    {
        Visual v = side == WeaponHandSide.Off ? offHand : mainHand;
        return v.instance != null ? v.instance.transform : null;
    }

    /// <summary>The transform of an attachment point (resolved or created), or null.</summary>
    public Transform GetSocket(AttachPoint point)
    {
        if (resolved.TryGetValue(point, out Transform t) && t != null)
            return t;
        t = ResolveSocket(point);
        resolved[point] = t;
        return t;
    }

    // ------------------------------------------------------------------ hands
    private void SetVisualItem(Visual v, ItemSO item, InventoryItem invItem)
    {
        if (!showHandItems)
            item = null;
        if (v.item == item && v.invItem == invItem && (item == null || v.instance != null))
            return;
        if (v.item != item || v.instance == null)
        {
            DestroyVisual(v);
            v.item = item;
            if (item != null)
            {
                GameObject model = HandModel(item);
                if (model != null)
                {
                    v.instance = Instantiate(model);
                    v.instance.name = model.name;
                    StripForDisplay(v.instance);
                }
            }
        }
        v.invItem = invItem;
    }

    private static GameObject HandModel(ItemSO item)
    {
        if (item.Visuals.model != null)
            return item.Visuals.model;
        if (item is ArmorSO shield && shield.ArmorModel != null)
            return shield.ArmorModel;
        return item.Prefab;
    }

    /// <summary>Moves every hand item where it belongs now (hand / sheath / stowed).</summary>
    private void ApplyPlacements()
    {
        PlaceHand(mainHand, WeaponHandSide.Main, false);
        PlaceHand(offHand, WeaponHandSide.Off, offHandStowed);
    }

    private void PlaceHand(Visual v, WeaponHandSide side, bool stowed)
    {
        if (v.instance == null || v.item == null)
            return;
        ItemSO item = v.item;
        ItemVisualSettings vis = item.Visuals;
        SheathPoint sheath = ResolveSheath(item, side);
        bool inHand = !stowed && (drawn || sheath == SheathPoint.StayInHand);
        if (stowed && sheath == SheathPoint.StayInHand)
            sheath = HandRules.IsShield(item) ? SheathPoint.Back : SheathPoint.HipLeft;

        if (inHand)
        {
            Transform socket;
            Vector3 pos, rot;
            if (side == WeaponHandSide.Main)
            {
                socket = MainHandSocket();
                pos = item.Position;
                rot = item.Rotation;
            }
            else
            {
                bool forearm = HandRules.IsShield(item) && vis.shieldOnForearm;
                socket = forearm ? GetSocket(AttachPoint.LeftForearm) : OffHandSocket();
                if (vis.customOffHandPose)
                {
                    pos = vis.offHandPosition;
                    rot = vis.offHandRotation;
                }
                else
                {
                    // The main-hand pose mirrored across the hand's side axis.
                    pos = new Vector3(-item.Position.x, item.Position.y, item.Position.z);
                    rot = new Vector3(item.Rotation.x, -item.Rotation.y, -item.Rotation.z);
                }
            }
            Attach(v.instance, socket, pos, rot, item.Scale);
            v.instance.SetActive(true);
            v.placement = Placement.Hand;
            return;
        }

        if (sheath == SheathPoint.Hidden)
        {
            v.instance.SetActive(false);
            v.placement = Placement.Sheathed;
            return;
        }
        Transform at = SheathSocket(sheath, vis.customSocket);
        if (at == null)
        {
            v.instance.SetActive(false);
            v.placement = Placement.Sheathed;
            return;
        }
        bool custom = vis.sheathPosition != Vector3.zero || vis.sheathRotation != Vector3.zero;
        Vector3 p = custom ? vis.sheathPosition : DefaultSheathPosition(sheath);
        Vector3 r = custom ? vis.sheathRotation : DefaultSheathRotation(sheath);
        Attach(v.instance, at, p, r, item.Scale);
        v.instance.SetActive(true);
        v.placement = Placement.Sheathed;
    }

    /// <summary>Where an item is sheathed (Auto resolved by category; the off hand uses the other hip).</summary>
    private static SheathPoint ResolveSheath(ItemSO item, WeaponHandSide side)
    {
        SheathPoint s = item.Visuals.sheath;
        if (s != SheathPoint.Auto)
            return s;
        if (HandRules.IsShield(item))
            return SheathPoint.Back;
        if (!(item is WeaponSO w))
            return SheathPoint.StayInHand;
        bool off = side == WeaponHandSide.Off;
        switch (w.Category)
        {
            case WeaponCategory.Sword:
            case WeaponCategory.Axe:
            case WeaponCategory.Mace:
            case WeaponCategory.Wand:
                return off ? SheathPoint.HipRight : SheathPoint.HipLeft;
            case WeaponCategory.Dagger:
            case WeaponCategory.Fist:
            case WeaponCategory.Tool:
            case WeaponCategory.Thrown:
                return off ? SheathPoint.HipLeft : SheathPoint.HipRight;
            case WeaponCategory.Greatsword:
            case WeaponCategory.Hammer:
            case WeaponCategory.Spear:
            case WeaponCategory.Staff:
                return SheathPoint.BackRight;
            case WeaponCategory.Bow:
            case WeaponCategory.Crossbow:
                return SheathPoint.BackLeft;
            case WeaponCategory.Shield:
                return SheathPoint.Back;
            default:
                return SheathPoint.StayInHand;
        }
    }

    private static Vector3 DefaultSheathPosition(SheathPoint s)
    {
        switch (s)
        {
            case SheathPoint.HipLeft:
            case SheathPoint.HipRight:
                return new Vector3(0f, -0.05f, 0f);
            default:
                return Vector3.zero;
        }
    }

    private static Vector3 DefaultSheathRotation(SheathPoint s)
    {
        switch (s)
        {
            case SheathPoint.BackRight: return new Vector3(0f, 0f, -35f);
            case SheathPoint.BackLeft: return new Vector3(0f, 0f, 35f);
            case SheathPoint.HipLeft: return new Vector3(160f, 0f, 10f);
            case SheathPoint.HipRight: return new Vector3(160f, 0f, -10f);
            default: return Vector3.zero;
        }
    }

    private Transform MainHandSocket()
    {
        if (mainSocketOverride != null)
            return mainSocketOverride;
        if (weapons != null && weapons.HandTransform != null)
            return weapons.HandTransform;
        return GetSocket(AttachPoint.RightHand);
    }

    private Transform OffHandSocket()
    {
        if (weapons != null && weapons.offHandGameObject != null)
            return weapons.offHandGameObject.transform;
        return GetSocket(AttachPoint.LeftHand);
    }

    private Transform SheathSocket(SheathPoint s, string custom)
    {
        switch (s)
        {
            case SheathPoint.Back: return GetSocket(AttachPoint.Back);
            case SheathPoint.BackLeft: return GetSocket(AttachPoint.BackLeft);
            case SheathPoint.BackRight: return GetSocket(AttachPoint.BackRight);
            case SheathPoint.HipLeft: return GetSocket(AttachPoint.HipLeft);
            case SheathPoint.HipRight: return GetSocket(AttachPoint.HipRight);
            case SheathPoint.Chest: return GetSocket(AttachPoint.Chest);
            case SheathPoint.Custom: return FindChild(custom);
            default: return null;
        }
    }

    // ------------------------------------------------------------------ armour
    /// <summary>Makes the worn models match the equipped armour (called when the equipment changes).</summary>
    public void RebuildArmor()
    {
        if (equipment == null)
            equipment = EquipmentManager.For(this, addIfMissing: false);
        keyBuffer.Clear();
        if (showArmor && equipment != null)
        {
            IReadOnlyList<EquipmentManager.Entry> entries = equipment.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!(entries[i].Item is ArmorSO a) || a.IsShield || a.ArmorModel == null || a.ArmorVisuals.attach == ArmorAttachMode.None)
                    continue;
                object key = entries[i].Key;
                keyBuffer.Add(key);
                if (!armor.TryGetValue(key, out Visual v) || v.item != a)
                {
                    if (v != null)
                        DestroyVisual(v);
                    armor[key] = WearArmor(a);
                }
            }
        }
        // Pieces no longer worn
        var gone = new List<object>();
        foreach (KeyValuePair<object, Visual> kv in armor)
            if (!keyBuffer.Contains(kv.Key))
                gone.Add(kv.Key);
        foreach (object k in gone)
        {
            DestroyVisual(armor[k]);
            armor.Remove(k);
        }
        keyBuffer.Clear();
    }

    private Visual WearArmor(ArmorSO a)
    {
        var v = new Visual { item = a, placement = Placement.Worn };
        ArmorVisualSettings vis = a.ArmorVisuals;
        GameObject model = a.ArmorModel;
        bool skinned = vis.attach == ArmorAttachMode.Skinned ||
                       (vis.attach == ArmorAttachMode.Auto && model.GetComponentInChildren<SkinnedMeshRenderer>(true) != null);
        if (skinned)
        {
            v.instance = Instantiate(model, transform);
            v.instance.name = model.name;
            v.instance.transform.localPosition = Vector3.zero;
            v.instance.transform.localRotation = Quaternion.identity;
            StripForDisplay(v.instance);
            foreach (Animator anim in v.instance.GetComponentsInChildren<Animator>(true))
                anim.enabled = false;
            int bound = 0, missing = 0;
            foreach (SkinnedMeshRenderer smr in v.instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Rebind(smr, ref bound, ref missing);
            if (missing > 0)
                Debug.LogWarning($"[Equipment Visuals] {a.Name}: {missing} of {bound + missing} bones of its model were not found on the character " +
                                 "(the armour must use the same skeleton, with the same bone names).", this);
        }
        else
        {
            AttachPoint point = DefaultArmorPoint(a.ArmorSlotType, false);
            Transform bone = !string.IsNullOrWhiteSpace(vis.bone) ? FindBone(vis.bone) : GetSocket(point);
            if (bone == null)
            {
                Debug.LogWarning($"[Equipment Visuals] {a.Name}: no bone to attach it to ({(string.IsNullOrWhiteSpace(vis.bone) ? point.ToString() : vis.bone)}).", this);
                return v;
            }
            v.instance = Instantiate(model);
            v.instance.name = model.name;
            StripForDisplay(v.instance);
            Attach(v.instance, bone, vis.position, vis.rotation, vis.scale);
            if (vis.IsPair(a.ArmorSlotType) && string.IsNullOrWhiteSpace(vis.bone))
            {
                Transform other = GetSocket(DefaultArmorPoint(a.ArmorSlotType, true));
                if (other != null && other != bone)
                {
                    v.pairInstance = Instantiate(model);
                    v.pairInstance.name = model.name + " (pair)";
                    StripForDisplay(v.pairInstance);
                    Vector3 s = vis.scale;
                    Attach(v.pairInstance, other, new Vector3(-vis.position.x, vis.position.y, vis.position.z),
                        new Vector3(vis.rotation.x, -vis.rotation.y, -vis.rotation.z), new Vector3(-s.x, s.y, s.z));
                }
            }
        }
        if (vis.hideCharacterParts != null)
            foreach (string part in vis.hideCharacterParts)
                Hide(v, part);
        if (debugLog)
            Debug.Log($"[Equipment Visuals] Wearing {a.Name} ({(skinned ? "skinned" : "attached")}).", this);
        return v;
    }

    /// <summary>The point a rigid piece of a slot attaches to (<paramref name="second"/>: the other side of a pair).</summary>
    private static AttachPoint DefaultArmorPoint(ArmorSlotType slot, bool second)
    {
        switch (slot)
        {
            case ArmorSlotType.Helmet: return AttachPoint.Head;
            case ArmorSlotType.Chestplate: return AttachPoint.Chest;
            case ArmorSlotType.Leggings: return AttachPoint.Hips;
            case ArmorSlotType.Boots: return second ? AttachPoint.RightFoot : AttachPoint.LeftFoot;
            case ArmorSlotType.Gloves: return second ? AttachPoint.RightHand : AttachPoint.LeftHand;
            case ArmorSlotType.Shoulders: return second ? AttachPoint.RightUpperArm : AttachPoint.LeftUpperArm;
            case ArmorSlotType.Bracers: return second ? AttachPoint.RightForearm : AttachPoint.LeftForearm;
            case ArmorSlotType.Belt: return AttachPoint.Hips;
            case ArmorSlotType.Cloak: return AttachPoint.Back;
            case ArmorSlotType.Amulet: return AttachPoint.Neck;
            default: return AttachPoint.Chest;
        }
    }

    private void Rebind(SkinnedMeshRenderer smr, ref int bound, ref int missing)
    {
        Transform[] bones = smr.bones;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null)
                continue;
            Transform mine = FindBone(bones[i].name);
            if (mine != null) { bones[i] = mine; bound++; }
            else missing++;
        }
        smr.bones = bones;
        if (smr.rootBone != null)
        {
            Transform root = FindBone(smr.rootBone.name);
            if (root != null)
                smr.rootBone = root;
        }
        smr.updateWhenOffscreen = true; // its bounds follow the character's animation
    }

    private void Hide(Visual v, string rendererName)
    {
        Transform t = FindBone(rendererName);
        if (t == null)
            return;
        foreach (Renderer r in t.GetComponents<Renderer>())
        {
            hideCounts.TryGetValue(r, out int n);
            if (n == 0)
                r.enabled = false;
            hideCounts[r] = n + 1;
            v.hidden.Add(r);
        }
    }

    // ------------------------------------------------------------------ sockets
    private Transform ResolveSocket(AttachPoint point)
    {
        foreach (SocketOverride o in sockets)
            if (o != null && o.point == point && o.transform != null)
                return o.transform;

        bool human = animator != null && animator.isHuman;
        switch (point)
        {
            case AttachPoint.RightHand: return Bone(HumanBodyBones.RightHand) ?? (weapons != null ? weapons.HandTransform : null);
            case AttachPoint.LeftHand: return Bone(HumanBodyBones.LeftHand) ?? (weapons != null ? weapons.OffHandTransform : null);
            case AttachPoint.Head: return Bone(HumanBodyBones.Head) ?? CreateSocket(point, null, new Vector3(0f, 0.95f, 0f), Vector3.zero);
            case AttachPoint.Neck: return Bone(HumanBodyBones.Neck) ?? Bone(HumanBodyBones.Head) ?? CreateSocket(point, null, new Vector3(0f, 0.85f, 0f), Vector3.zero);
            case AttachPoint.Chest: return ChestBone() ?? CreateSocket(point, null, new Vector3(0f, 0.72f, 0f), Vector3.zero);
            case AttachPoint.Hips: return Bone(HumanBodyBones.Hips) ?? CreateSocket(point, null, new Vector3(0f, 0.5f, 0f), Vector3.zero);
            case AttachPoint.LeftForearm: return Bone(HumanBodyBones.LeftLowerArm) ?? Bone(HumanBodyBones.LeftHand) ?? GetSocket(AttachPoint.LeftHand);
            case AttachPoint.RightForearm: return Bone(HumanBodyBones.RightLowerArm) ?? Bone(HumanBodyBones.RightHand) ?? GetSocket(AttachPoint.RightHand);
            case AttachPoint.LeftUpperArm: return Bone(HumanBodyBones.LeftUpperArm) ?? ChestBone();
            case AttachPoint.RightUpperArm: return Bone(HumanBodyBones.RightUpperArm) ?? ChestBone();
            case AttachPoint.LeftUpperLeg: return Bone(HumanBodyBones.LeftUpperLeg) ?? GetSocket(AttachPoint.Hips);
            case AttachPoint.RightUpperLeg: return Bone(HumanBodyBones.RightUpperLeg) ?? GetSocket(AttachPoint.Hips);
            case AttachPoint.LeftLowerLeg: return Bone(HumanBodyBones.LeftLowerLeg) ?? GetSocket(AttachPoint.LeftUpperLeg);
            case AttachPoint.RightLowerLeg: return Bone(HumanBodyBones.RightLowerLeg) ?? GetSocket(AttachPoint.RightUpperLeg);
            case AttachPoint.LeftFoot: return Bone(HumanBodyBones.LeftFoot) ?? GetSocket(AttachPoint.LeftLowerLeg);
            case AttachPoint.RightFoot: return Bone(HumanBodyBones.RightFoot) ?? GetSocket(AttachPoint.RightLowerLeg);
            // Sheath points: children of the closest bone, placed in the character's own frame (rig axes differ).
            case AttachPoint.Back: return CreateSocket(point, ChestBone(), human ? new Vector3(0f, 0f, -0.18f) : new Vector3(0f, 0.7f, -0.25f), Vector3.zero);
            case AttachPoint.BackLeft: return CreateSocket(point, ChestBone(), human ? new Vector3(-0.08f, 0.02f, -0.2f) : new Vector3(-0.1f, 0.7f, -0.27f), Vector3.zero);
            case AttachPoint.BackRight: return CreateSocket(point, ChestBone(), human ? new Vector3(0.08f, 0.02f, -0.2f) : new Vector3(0.1f, 0.7f, -0.27f), Vector3.zero);
            case AttachPoint.HipLeft: return CreateSocket(point, Bone(HumanBodyBones.Hips), human ? new Vector3(-0.17f, 0f, 0.02f) : new Vector3(-0.25f, 0.5f, 0f), Vector3.zero);
            case AttachPoint.HipRight: return CreateSocket(point, Bone(HumanBodyBones.Hips), human ? new Vector3(0.17f, 0f, 0.02f) : new Vector3(0.25f, 0.5f, 0f), Vector3.zero);
            default: return null;
        }
    }

    private Transform ChestBone() => Bone(HumanBodyBones.UpperChest) ?? Bone(HumanBodyBones.Chest) ?? Bone(HumanBodyBones.Spine);

    private Transform Bone(HumanBodyBones b)
    {
        if (animator == null || !animator.isHuman)
            return null;
        Transform t = animator.GetBoneTransform(b);
        return t != null ? t : null;
    }

    /// <summary>
    /// Creates a socket child of <paramref name="bone"/> (or of the character when there is none). The offset is in the
    /// CHARACTER's frame (x right, y up, z forward) - metres for bones; fractions of the body height for the character itself.
    /// </summary>
    private Transform CreateSocket(AttachPoint point, Transform bone, Vector3 offset, Vector3 euler)
    {
        Transform parent = bone != null ? bone : transform;
        string socketName = "Socket_" + point;
        Transform existing = parent.Find(socketName);
        if (existing != null)
            return existing;
        Transform s = new GameObject(socketName).transform;
        Vector3 local = offset;
        if (bone == null)
        {
            float h = entity != null ? entity.Height : 1.8f;
            local = new Vector3(offset.x, offset.y * h, offset.z);
        }
        s.position = parent.position + transform.rotation * local;
        s.rotation = transform.rotation * Quaternion.Euler(euler);
        s.SetParent(parent, true);
        return s;
    }

    private Transform FindChild(string childName) => string.IsNullOrWhiteSpace(childName) ? null : FindBone(childName);

    /// <summary>A transform of the character by name (bones, renderers, custom sockets).</summary>
    private Transform FindBone(string boneName)
    {
        if (string.IsNullOrWhiteSpace(boneName))
            return null;
        if (boneByName == null)
        {
            boneByName = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            Transform root = animator != null ? animator.transform : transform;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (!boneByName.ContainsKey(t.name))
                    boneByName[t.name] = t;
            if (root != transform)
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                    if (!boneByName.ContainsKey(t.name))
                        boneByName[t.name] = t;
        }
        boneByName.TryGetValue(boneName.Trim(), out Transform found);
        return found;
    }

    // ------------------------------------------------------------------ helpers
    private static void Attach(GameObject go, Transform parent, Vector3 position, Vector3 euler, Vector3 scale)
    {
        if (go == null || parent == null)
            return;
        Transform t = go.transform;
        if (t.parent != parent)
            t.SetParent(parent, false);
        t.localPosition = position;
        t.localRotation = Quaternion.Euler(euler);
        t.localScale = scale == Vector3.zero ? Vector3.one : scale;
    }

    /// <summary>A model for display only: no physics, no colliders, no pickup.</summary>
    private static void StripForDisplay(GameObject go)
    {
        foreach (ItemPickable p in go.GetComponentsInChildren<ItemPickable>(true))
            Destroy(p);
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rb);
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
            Destroy(c);
        if (!go.activeSelf)
            go.SetActive(true);
    }

    private void DestroyVisual(Visual v)
    {
        if (v == null)
            return;
        if (v.instance != null)
            Destroy(v.instance);
        if (v.pairInstance != null)
            Destroy(v.pairInstance);
        foreach (Renderer r in v.hidden)
        {
            if (r == null || !hideCounts.TryGetValue(r, out int n))
                continue;
            if (n <= 1)
            {
                hideCounts.Remove(r);
                r.enabled = true;
            }
            else
            {
                hideCounts[r] = n - 1;
            }
        }
        v.hidden.Clear();
        v.instance = null;
        v.pairInstance = null;
        v.item = null;
        v.invItem = null;
        v.placement = Placement.None;
    }
}
