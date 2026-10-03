using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Applies what equipped items do - stats, resistances, traits, passive behaviours, abilities, triggered effects -
/// exactly once while they are equipped, and removes exactly that when they come off.
/// <para>
/// There is one source of truth: the inventory reports which items sit in equipment slots
/// (<see cref="SyncFromSlots"/>, called after every inventory change) and the weapon controller reports the weapon in
/// hand (<see cref="Equip"/>/<see cref="Unequip"/>). The manager compares that with what is applied and only equips
/// what is new and unequips what is gone, so dragging, swapping, dropping, splitting or reloading can never apply an
/// item twice or forget to remove it. Armor sets are counted from the same data by the <see cref="ArmorSetManager"/>.
/// </para>
/// Lives on the player (added automatically by the inventory when missing).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(60)]
public class EquipmentManager : MonoBehaviour
{
    /// <summary>One equipped item and the handles of everything it applied.</summary>
    public sealed class Entry
    {
        /// <summary>What identifies it: the inventory item for slot items, any object for the weapon in hand or scripts.</summary>
        public object Key { get; internal set; }
        public ItemSO Item { get; internal set; }
        /// <summary>Where it is equipped ("Helmet", "Main Hand"...).</summary>
        public string Slot { get; internal set; }
        /// <summary>True when it was equipped because it sits in an equipment slot.</summary>
        public bool FromSlots { get; internal set; }
        public float EquippedAt { get; internal set; }
        internal readonly List<EquipmentEffectHandle> handles = new List<EquipmentEffectHandle>();
        public IReadOnlyList<EquipmentEffectHandle> Handles => handles;
        public InventoryItem InventoryItem => Key as InventoryItem;
    }

    [Header("References (found automatically when empty)")]
    [Tooltip("The inventory whose equipment slots are equipped. Empty = the InventoryManager of this character (or the only one in the scene).")]
    [SerializeField] private InventoryManager inventory;
    [Tooltip("Counts armor set pieces and applies set bonuses. Empty = the one on this character (added when missing).")]
    [SerializeField] private ArmorSetManager armorSets;

    [Header("Feedback")]
    [Tooltip("Play the armor pieces' equip/unequip sounds (not while loading).")]
    [SerializeField] private bool playEquipSounds = true;

    [Header("Debug")]
    [Tooltip("Log what is equipped and unequipped.")]
    [SerializeField] private bool debugLog = false;

    private readonly List<Entry> entries = new List<Entry>();
    private readonly Dictionary<object, Entry> byKey = new Dictionary<object, Entry>(ReferenceComparer<object>.Instance);
    private readonly Dictionary<InventoryItem, InventorySlot> desired = new Dictionary<InventoryItem, InventorySlot>(ReferenceComparer<InventoryItem>.Instance);
    private readonly HashSet<InventoryItem> suppressed = new HashSet<InventoryItem>(ReferenceComparer<InventoryItem>.Instance);
    private readonly List<EquipmentEffectHandle> tickList = new List<EquipmentEffectHandle>();
    private readonly List<ItemSO> itemsBuffer = new List<ItemSO>();
    private readonly Dictionary<ItemSO, Stack<object>> legacyKeys = new Dictionary<ItemSO, Stack<object>>(ReferenceComparer<ItemSO>.Instance);
    private EquipmentContext context;
    private PlayerStatusController status;
    private bool tickDirty = true;
    private bool syncRequested;
    private bool initialized;
    private bool loading = true;
    private int batchDepth;
    private bool batchChanged;

    /// <summary>Raised once after the equipped items changed (after a whole sync, not per item).</summary>
    public event Action Changed;
    /// <summary>Raised for each item equipped (true) or unequipped (false).</summary>
    public event Action<ItemSO, bool> ItemEquipped;

    /// <summary>The character and the shared bookkeeping effects use.</summary>
    public EquipmentContext Context => context ?? (context = new EquipmentContext(this));

    /// <summary>Everything equipped right now (do not modify).</summary>
    public IReadOnlyList<Entry> Entries => entries;

    public InventoryManager Inventory => inventory;
    public ArmorSetManager ArmorSets => armorSets;

    /// <summary>True once the first sync with the inventory has happened.</summary>
    public bool IsInitialized => initialized;

    /// <summary>The equipment manager of a character (added to the character when missing).</summary>
    public static EquipmentManager For(Component anyPart, bool addIfMissing = true)
    {
        if (anyPart == null)
            return null;
        EquipmentManager m = anyPart.GetComponentInParent<EquipmentManager>();
        if (m != null)
            return m;
        PlayerStatusController ps = anyPart as PlayerStatusController ?? anyPart.GetComponentInParent<PlayerStatusController>();
        GameObject host = ps != null ? ps.gameObject : anyPart.gameObject;
        m = host.GetComponentInChildren<EquipmentManager>(true);
        if (m != null || !addIfMissing)
            return m;
        return host.AddComponent<EquipmentManager>();
    }

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        status = GetComponent<PlayerStatusController>();
        if (status == null)
            status = GetComponentInParent<PlayerStatusController>();
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (status != null)
        {
            status.OnPlayerClassChanged += HandleClassChanged;
            status.OnStatsInitialized += HandleStatsInitialized;
        }
    }

    private void OnDisable()
    {
        if (status != null)
        {
            status.OnPlayerClassChanged -= HandleClassChanged;
            status.OnStatsInitialized -= HandleStatsInitialized;
        }
    }

    private void Start()
    {
        // One frame later: the class has set the base stats, the inventory has created its slots and loaded items.
        StartCoroutine(InitialSync());
    }

    private IEnumerator InitialSync()
    {
        yield return null;
        initialized = true;
        loading = true;
        SyncFromInventory();
        loading = false;
    }

    private void OnDestroy()
    {
        // Leave the character exactly as it was without equipment.
        BeginBatch();
        for (int i = entries.Count - 1; i >= 0; i--)
            RemoveEntry(entries[i], i, false);
        EndBatch();
        context?.LegacyStats.Clear();
    }

    private void Update()
    {
        if (syncRequested && initialized)
        {
            syncRequested = false;
            SyncFromInventory();
        }
        if (tickDirty)
            RebuildTickList();
        float dt = Time.deltaTime;
        for (int i = 0; i < tickList.Count; i++)
        {
            EquipmentEffectHandle h = tickList[i];
            if (h.Reverted || !h.NeedsTick)
                continue;
            try { h.Tick(dt); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
    }

    private void ResolveReferences()
    {
        if (inventory == null)
        {
            inventory = GetComponent<InventoryManager>();
            if (inventory == null)
                inventory = GetComponentInChildren<InventoryManager>(true);
            if (inventory == null)
            {
                // The inventory often lives on the UI canvas: pick the one that belongs to this player.
                foreach (InventoryManager m in FindObjectsByType<InventoryManager>(FindObjectsInactive.Include))
                {
                    if (m.Player == gameObject || (status != null && m.Player == status.gameObject) || m.GetComponentInParent<PlayerStatusController>() == status)
                    {
                        inventory = m;
                        break;
                    }
                }
            }
        }
        if (armorSets == null)
        {
            armorSets = GetComponent<ArmorSetManager>();
            if (armorSets == null && status != null)
                armorSets = status.ArmorSetManager != null ? status.ArmorSetManager : status.GetComponent<ArmorSetManager>();
            if (armorSets == null)
                armorSets = gameObject.AddComponent<ArmorSetManager>();
        }
    }

    private void HandleClassChanged(PlayerClass c) => context?.LegacyStats.OnClassReset();
    private void HandleStatsInitialized() => context?.LegacyStats.OnClassReset();

    // ------------------------------------------------------------------ syncing with the inventory
    /// <summary>Asks for a sync with the inventory's equipment slots at the next frame (cheap to call often).</summary>
    public void RequestSync() => syncRequested = true;

    /// <summary>Syncs with the inventory's equipment slots now.</summary>
    public void SyncFromInventory()
    {
        if (inventory == null)
            ResolveReferences();
        if (inventory != null)
            SyncFromSlots(inventory.EquipmentSlots);
    }

    /// <summary>
    /// Makes the equipped slot items match <paramref name="slots"/>: items that sit in their equipment slot are
    /// equipped, everything else that was equipped from slots is unequipped. Weapons and scripted items are left alone.
    /// </summary>
    public void SyncFromSlots(IEnumerable<InventorySlot> slots)
    {
        desired.Clear();
        if (suppressed.Count > 0)
            suppressed.RemoveWhere(i => i == null); // destroyed items
        if (slots != null)
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot == null || slot.heldItem == null)
                    continue;
                InventoryItem inv = slot.heldItem.GetComponent<InventoryItem>();
                if (inv == null || inv.itemScriptableObject == null || !SlotTypeHelper.Equips(inv.itemScriptableObject, slot.SlotType))
                    continue;
                if (suppressed.Contains(inv))
                    continue; // stowed (an off-hand shield while a two-handed weapon is held)
                desired[inv] = slot;
            }
        }

        BeginBatch();
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Entry e = entries[i];
            if (!e.FromSlots)
                continue;
            InventoryItem inv = e.InventoryItem;
            if (inv == null || !desired.TryGetValue(inv, out InventorySlot slot) || inv.itemScriptableObject != e.Item)
                RemoveEntry(e, i, true);
            else
                e.Slot = SlotTypeHelper.GetDisplayName(slot.SlotType);
        }
        foreach (KeyValuePair<InventoryItem, InventorySlot> kv in desired)
        {
            if (!byKey.ContainsKey(kv.Key))
                AddEntry(kv.Key, kv.Key.itemScriptableObject, SlotTypeHelper.GetDisplayName(kv.Value.SlotType), true);
            kv.Key.isEquipped = true;
        }
        desired.Clear();
        EndBatch();
    }

    /// <summary>
    /// Stows (or un-stows) an item that sits in an equipment slot: while stowed it stays in its slot but nothing it does
    /// applies (an off-hand shield while a two-handed weapon is held). Takes effect at the next sync. Returns true if it changed.
    /// </summary>
    public bool SetSuppressed(InventoryItem item, bool suppress)
    {
        if (item == null)
            return false;
        bool changed = suppress ? suppressed.Add(item) : suppressed.Remove(item);
        if (changed)
            RequestSync();
        return changed;
    }

    /// <summary>Is the item stowed (in its slot, but not applied)?</summary>
    public bool IsSuppressed(InventoryItem item) => item != null && suppressed.Contains(item);

    /// <summary>Un-stows every item.</summary>
    public void ClearSuppressed()
    {
        if (suppressed.Count == 0)
            return;
        suppressed.Clear();
        RequestSync();
    }

    // ------------------------------------------------------------------ manual equipping
    /// <summary>
    /// Equips <paramref name="item"/> under <paramref name="key"/> (the weapon in hand, an item a script puts on).
    /// Equipping the same item under the same key again does nothing; a different item replaces the old one.
    /// Returns true if something changed.
    /// </summary>
    public bool Equip(object key, ItemSO item, string slot = null)
    {
        if (key == null || item == null)
            return false;
        if (byKey.TryGetValue(key, out Entry existing))
        {
            if (existing.Item == item)
                return false;
            BeginBatch();
            RemoveEntry(existing, entries.IndexOf(existing), true);
            AddEntry(key, item, slot, false);
            EndBatch();
            return true;
        }
        BeginBatch();
        AddEntry(key, item, slot, false);
        EndBatch();
        return true;
    }

    /// <summary>Unequips what was equipped under <paramref name="key"/>. Returns true if something was equipped.</summary>
    public bool Unequip(object key)
    {
        if (key == null || !byKey.TryGetValue(key, out Entry e))
            return false;
        BeginBatch();
        RemoveEntry(e, entries.IndexOf(e), true);
        EndBatch();
        return true;
    }

    /// <summary>Unequips everything, including the weapon in hand.</summary>
    public void UnequipAll()
    {
        BeginBatch();
        for (int i = entries.Count - 1; i >= 0; i--)
            RemoveEntry(entries[i], i, true);
        EndBatch();
    }

    /// <summary>
    /// Old-style apply/remove (<see cref="ItemSO.ApplyEquippedStats"/>): every "apply" equips one more copy of the item,
    /// every "remove" unequips one, so old scripts keep working without values drifting.
    /// </summary>
    public void ApplyLegacy(ItemSO item, bool apply)
    {
        if (item == null)
            return;
        if (!legacyKeys.TryGetValue(item, out Stack<object> keys))
            legacyKeys[item] = keys = new Stack<object>();
        if (apply)
        {
            object key = new object();
            keys.Push(key);
            Equip(key, item, "Script");
        }
        else if (keys.Count > 0)
        {
            Unequip(keys.Pop());
        }
    }

    // ------------------------------------------------------------------ queries
    public bool IsEquipped(object key) => key != null && byKey.ContainsKey(key);

    /// <summary>Is at least one copy of this item asset equipped?</summary>
    public bool IsEquipped(ItemSO item)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Item == item) return true;
        return false;
    }

    /// <summary>How many copies of this item asset are equipped.</summary>
    public int CountEquipped(ItemSO item)
    {
        int n = 0;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Item == item) n++;
        return n;
    }

    /// <summary>The equipped item assets (a new list; pass one in to avoid allocating).</summary>
    public List<ItemSO> GetEquippedItems(List<ItemSO> into = null)
    {
        into = into ?? new List<ItemSO>();
        for (int i = 0; i < entries.Count; i++)
            into.Add(entries[i].Item);
        return into;
    }

    /// <summary>The equipped armor pieces.</summary>
    public List<ArmorSO> GetEquippedArmor(List<ArmorSO> into = null)
    {
        into = into ?? new List<ArmorSO>();
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Item is ArmorSO a) into.Add(a);
        return into;
    }

    /// <summary>The armor piece equipped in a slot, or null.</summary>
    public ArmorSO GetArmorInSlot(ArmorSlotType slot)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Item is ArmorSO a && a.ArmorSlotType == slot) return a;
        return null;
    }

    /// <summary>Total Defense and Magic Resistance points of the equipped armor pieces.</summary>
    public void GetArmorDefense(out float defense, out float magicDefense)
    {
        defense = magicDefense = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Item is ArmorSO a)
            {
                defense += a.GetEffectiveDefense();
                magicDefense += a.GetEffectiveMagicDefense();
            }
        }
    }

    /// <summary>A readable summary of what is equipped and what each item does right now (debug, inspector).</summary>
    public string Describe()
    {
        var sb = new StringBuilder();
        if (entries.Count == 0)
            sb.AppendLine("Nothing equipped.");
        foreach (Entry e in entries)
        {
            sb.AppendLine($"{e.Slot}: {(e.Item != null ? e.Item.Name : "?")}");
            foreach (EquipmentEffectHandle h in e.handles)
            {
                string d = h.Effect != null ? h.Effect.Describe() : "";
                string live = h.LiveStatus;
                if (!string.IsNullOrEmpty(d) || !string.IsNullOrEmpty(live))
                    sb.AppendLine($"   • {d}{(string.IsNullOrEmpty(live) ? "" : $"  [{live}]")}");
            }
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ internals
    private void AddEntry(object key, ItemSO item, string slot, bool fromSlots)
    {
        var e = new Entry
        {
            Key = key,
            Item = item,
            Slot = string.IsNullOrEmpty(slot) ? "Equipped" : slot,
            FromSlots = fromSlots,
            EquippedAt = Time.time,
        };
        entries.Add(e);
        byKey[key] = e;

        var effects = new List<EquipmentEffect>();
        try { item.CollectEquipEffects(effects); }
        catch (Exception ex) { Debug.LogException(ex, item); }
        EquipmentEffect.ApplyAll(effects, Context, 1f, item.Name, e.handles);

        if (key is InventoryItem inv && inv != null)
            inv.isEquipped = true;
        if (playEquipSounds && !loading && item is ArmorSO armor)
            Context.PlayOneShot(armor.EquipArmorSound);
        if (debugLog)
            Debug.Log($"[Equipment] Equipped {item.Name} ({e.Slot}): {e.handles.Count} effect(s).", this);

        tickDirty = true;
        batchChanged = true;
        ItemEquipped?.Invoke(item, true);
    }

    private void RemoveEntry(Entry e, int index, bool feedback)
    {
        if (e == null)
            return;
        if (index >= 0 && index < entries.Count && entries[index] == e)
            entries.RemoveAt(index);
        else
            entries.Remove(e);
        byKey.Remove(e.Key);

        EquipmentEffect.RevertAll(e.handles);

        InventoryItem inv = e.InventoryItem;
        if (inv != null)
            inv.isEquipped = false;
        if (feedback && playEquipSounds && !loading && e.Item is ArmorSO armor)
            Context.PlayOneShot(armor.UnequipArmorSound);
        if (debugLog)
            Debug.Log($"[Equipment] Unequipped {(e.Item != null ? e.Item.Name : "?")} ({e.Slot}).", this);

        tickDirty = true;
        batchChanged = true;
        ItemEquipped?.Invoke(e.Item, false);
    }

    private void BeginBatch() => batchDepth++;

    private void EndBatch()
    {
        if (--batchDepth > 0 || !batchChanged)
            return;
        batchChanged = false;
        try { Changed?.Invoke(); }
        catch (Exception e) { Debug.LogException(e, this); }
    }

    private void RebuildTickList()
    {
        tickDirty = false;
        tickList.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            List<EquipmentEffectHandle> hs = entries[i].handles;
            for (int j = 0; j < hs.Count; j++)
                tickList.Add(hs[j]);
        }
    }

    [ContextMenu("Log Equipment")]
    private void LogEquipment() => Debug.Log($"[Equipment] {name}\n{Describe()}", this);

    [ContextMenu("Sync With Inventory Now")]
    private void DebugSync() => SyncFromInventory();
}
