using System;
using System.Collections.Generic;
using GameNet;
using UnityEngine;

/// <summary>How an inventory takes part in a network session (see <see cref="InventoryManager.NetRole"/>).</summary>
public enum InventoryNetRole
{
    /// <summary>Offline, or the server's copy (the host's own player included): the inventory works as always and is the truth.</summary>
    Local = 0,
    /// <summary>A client's copy of its own player: it shows the server's snapshot and turns every action into a request.</summary>
    OwnerClient = 1,
    /// <summary>A client's copy of another player: it only shows what that player holds and wears.</summary>
    RemoteCopy = 2,
}

/// <summary>
/// What a client's copy of an inventory sends to the server (installed by the network layer, NetInventory, on client
/// copies only; offline and on the server nothing is installed and the inventory works exactly as before).
/// </summary>
public interface IInventoryNetBridge
{
    /// <summary>Sends a request about the owner's own inventory (the bridge numbers it).</summary>
    void Send(InventoryManager inventory, InventoryRequest request);

    /// <summary>Sends a request about a world object (an item to pick up, a storage to open) to the server.</summary>
    void SendWorld(InventoryManager inventory, InventoryRequest request, GameObject target);
}

// The network side of the inventory: who decides (NetRole), the snapshot the server sends to the owner and how a client
// shows it, what other players see, and how the server repeats a client's request on its own copy.
public partial class InventoryManager
{
    /// <summary>Furthest from the player an item dropped through a client's request lands (metres).</summary>
    public const float MaxRequestDropDistance = 4f;

    [NonSerialized] private IInventoryNetBridge netBridge;
    [NonSerialized] private bool predicting;
    [NonSerialized] private InventorySnapshot pendingSnapshot;
    [NonSerialized] private bool warnedClientMutation;
    [NonSerialized] private HashSet<ItemSO> warnedMissingIds;
    [NonSerialized] private readonly List<InventoryStackData> applyData = new List<InventoryStackData>();
    [NonSerialized] private readonly List<InventoryItem> applyItems = new List<InventoryItem>();
    [NonSerialized] private readonly List<InventorySnapshotDiff.Step> applySteps = new List<InventorySnapshotDiff.Step>();
    [NonSerialized] private readonly List<InventoryStackData> appearanceStacks = new List<InventoryStackData>();

    /// <summary>
    /// The network layer's sender, set on client copies by NetInventory (null offline and on the server: then the
    /// inventory runs its own rules exactly as before).
    /// </summary>
    public IInventoryNetBridge NetBridge
    {
        get => netBridge;
        set
        {
            netBridge = value;
            pendingSnapshot = null;
        }
    }

    /// <summary>
    /// Does this copy decide (offline, the server), show its owner's server inventory (a client's own player), or only
    /// show what another player holds? Read when acting: a copy learns it is a client's when the session starts.
    /// </summary>
    public InventoryNetRole NetRole
    {
        get
        {
            if (netBridge == null || !NetAuthority.IsClientOnly)
                return InventoryNetRole.Local;
            return NetAuthority.IsLocallyControlled(PlayerObject) ? InventoryNetRole.OwnerClient : InventoryNetRole.RemoteCopy;
        }
    }

    /// <summary>This copy runs the inventory's rules (offline, or the server's copy).</summary>
    public bool IsAuthoritative => NetRole == InventoryNetRole.Local;

    private GameObject PlayerObject => player != null ? player : gameObject;

    /// <summary>Is this the inventory of the player controlled from this machine (its keys, mouse and screen)?</summary>
    private bool ReadsLocalInput => NetAuthority.IsLocallyControlled(PlayerObject);

    // ------------------------------------------------------------------ client: requests
    private bool SendRequest(InventoryRequest request)
    {
        if (netBridge == null)
            return false;
        try
        {
            netBridge.Send(this, request);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            return false;
        }
    }

    private void SendWorldRequest(InventoryRequest request, GameObject target)
    {
        if (netBridge == null || target == null)
            return;
        try { netBridge.SendWorld(this, request, target); }
        catch (Exception e) { Debug.LogException(e, this); }
    }

    private void RequestPickup(GameObject picked)
    {
        if (picked == null)
            return;
        ItemPickable p = picked.GetComponent<ItemPickable>();
        if (p == null)
            p = picked.GetComponentInParent<ItemPickable>();
        if (p == null || p.itemScriptableObject == null)
            return;
        SendWorldRequest(new InventoryRequest
        {
            kind = (byte)InventoryRequestKind.Pickup,
            itemId = ItemDatabase.Id(p.itemScriptableObject),
        }, p.gameObject);
    }

    private bool SendArmorCommand(InventoryRequestKind kind, ArmorSO armor)
    {
        if (NetRole != InventoryNetRole.OwnerClient)
            return false;
        string id = armor != null ? ItemDatabase.Id(armor) : "";
        if (armor != null && string.IsNullOrEmpty(id))
            return false;
        return SendRequest(new InventoryRequest { kind = (byte)kind, itemId = id });
    }

    /// <summary>The quickslot bar of a client's own player asks the server to (un)assign a quickslot.</summary>
    internal bool SendQuickSlotRequest(int index, ItemSO item)
    {
        if (NetRole != InventoryNetRole.OwnerClient)
            return false;
        string id = item != null ? ItemDatabase.Id(item) : "";
        if (item != null && string.IsNullOrEmpty(id))
        {
            WarnMissingId(item);
            return false;
        }
        return SendRequest(new InventoryRequest { kind = (byte)InventoryRequestKind.AssignQuickSlot, amount = index, itemId = id });
    }

    /// <summary>Tells the server the weapons were drawn or sheathed (a client's own player; the others see it).</summary>
    public void SendDrawnState(bool drawn)
    {
        if (NetRole == InventoryNetRole.OwnerClient)
            SendRequest(new InventoryRequest { kind = (byte)InventoryRequestKind.SetDrawn, flag = drawn });
    }

    /// <summary>Asks the server for the whole inventory again (a client's own player).</summary>
    public void RequestResync()
    {
        if (NetRole == InventoryNetRole.OwnerClient)
            SendRequest(new InventoryRequest { kind = (byte)InventoryRequestKind.Resync });
    }

    private int WarnClientMutation(string what, int result)
    {
        if (!warnedClientMutation)
        {
            warnedClientMutation = true;
            Debug.Log($"[Inventory] {what} was called on a network client's copy of the inventory and was ignored: the server " +
                      "decides what the inventory holds (call it on the server; the owner receives the result).", this);
        }
        return result;
    }

    private void WarnMissingId(ItemSO item)
    {
        if (item == null)
            return;
        warnedMissingIds = warnedMissingIds ?? new HashSet<ItemSO>(ReferenceComparer<ItemSO>.Instance);
        if (warnedMissingIds.Add(item))
            Debug.LogWarning($"[Inventory] '{item.Name}' is not in the Item Database, so it cannot be sent over the network. " +
                             "Run Tools ▸ SimpleMovements ▸ Project Setup ▸ Build Item Database.", item);
    }

    /// <summary>A dragged item was dropped on a slot (or into the world) on a client: the server repeats the drop.</summary>
    private void SendSlotDrop(InventoryItem item, InventorySlot target, bool toWorld, Vector3 point)
    {
        InventorySlot from = dragHandler != null && dragHandler.LastItemSlotObject != null ? dragHandler.LastItemSlotObject.GetComponent<InventorySlot>() : null;
        if (from == null || item == null || item.itemScriptableObject == null)
            return;
        var r = new InventoryRequest
        {
            kind = (byte)InventoryRequestKind.Move,
            from = SlotRefOf(from),
            to = target != null ? SlotRefOf(target) : InventorySlotRef.None,
            itemId = ItemDatabase.Id(item.itemScriptableObject),
            flag = toWorld,
            point = point,
            quickIndex = -1,
        };
        if (!r.from.IsValid || (!toWorld && !r.to.IsValid))
            return;
        SendRequest(r);
    }

    /// <summary>A dragged item was dropped on the grid on a client: the server repeats it with the same target and turn.</summary>
    private void SendGridDrop(int cx, int cy)
    {
        InventoryItem item = PrepareGridDrop();
        InventorySlot from = dragHandler != null && dragHandler.LastItemSlotObject != null ? dragHandler.LastItemSlotObject.GetComponent<InventorySlot>() : null;
        if (item == null || from == null)
            return;
        GridRect target = DragTarget(cx, cy);
        var r = new InventoryRequest
        {
            kind = (byte)InventoryRequestKind.Move,
            from = SlotRefOf(from),
            to = InventorySlotRef.None,
            itemId = ItemDatabase.Id(item.itemScriptableObject),
            toGrid = true,
            gridX = (short)Mathf.Clamp(target.X, short.MinValue, short.MaxValue),
            gridY = (short)Mathf.Clamp(target.Y, short.MinValue, short.MaxValue),
            rotated = dragRotated,
            quickIndex = -1,
        };
        if (r.from.IsValid)
            SendRequest(r);
    }

    // ------------------------------------------------------------------ storage helpers
    /// <summary>A client's storage window closes (the server closes the storage itself).</summary>
    private void CloseStorageView()
    {
        if (storageManager == null || !storageManager.IsStorageOpened)
            return;
        storageManager.CloseView();
        uiStateManager?.SetStorageOpened(false);
        if (!IsExternalPanelOpen && !IsInventoryOpened && ReadsLocalInput)
            SetCursorState(CursorLockMode.Locked, false);
    }

    /// <summary>Closes the storage window without the cursor or event handling (closing the inventory, re-packing).</summary>
    private void CloseCurrentStorageQuietly()
    {
        if (storageManager == null)
            return;
        Storage open = storageManager.Current;
        storageManager.CloseCurrentStorage();
        if (open != null)
            open.Release(this);
    }

    private void ReleaseStorageOnDestroy()
    {
        // The network layer closes it earlier (while the items still exist); this is the last chance to let others use it.
        Storage open = storageManager != null ? storageManager.Current : null;
        if (open != null)
            open.Release(this);
    }

    /// <summary>
    /// The server closes the open storage when the player is too far from it, dead, or gone (its contents are saved into
    /// it and other players can open it). Returns true when a storage was closed.
    /// </summary>
    public bool CloseStorageIf(Func<Storage, bool> shouldClose)
    {
        if (!IsAuthoritative || storageManager == null || !storageManager.IsStorageOpened)
            return false;
        Storage open = storageManager.Current;
        if (open != null && shouldClose != null && !shouldClose(open))
            return false;
        CloseStorage(open);
        return true;
    }

    // ------------------------------------------------------------------ slot references
    /// <summary>The network name of <paramref name="slot"/> (area and index), or <see cref="InventorySlotRef.None"/>.</summary>
    public InventorySlotRef SlotRefOf(InventorySlot slot)
    {
        if (slot == null)
            return InventorySlotRef.None;
        int i = IndexOf(HotbarSlots, slot);
        if (i >= 0)
            return new InventorySlotRef(InventoryArea.Hotbar, i);
        i = IndexOf(Slots, slot);
        if (i >= 0)
            return new InventorySlotRef(InventoryArea.Bag, i);
        IReadOnlyList<InventorySlot> eq = EquipmentSlots;
        for (int k = 0; k < eq.Count; k++)
            if (eq[k] == slot)
                return new InventorySlotRef(InventoryArea.Equipment, k);
        if (storageManager != null && storageManager.IsStorageOpened)
        {
            i = IndexOf(storageManager.SlotObjects, slot);
            if (i >= 0 && i < storageManager.ViewSize)
                return new InventorySlotRef(InventoryArea.Storage, i);
        }
        return InventorySlotRef.None;
    }

    /// <summary>The slot named by <paramref name="slotRef"/> (storage slots only while a storage is open), or null.</summary>
    public InventorySlot SlotAt(InventorySlotRef slotRef)
    {
        if (!slotRef.IsValid)
            return null;
        switch (slotRef.Area)
        {
            case InventoryArea.Hotbar:
                return At(HotbarSlots, slotRef.index);
            case InventoryArea.Bag:
                return At(Slots, slotRef.index);
            case InventoryArea.Equipment:
                IReadOnlyList<InventorySlot> eq = EquipmentSlots;
                return slotRef.index < eq.Count ? eq[slotRef.index] : null;
            case InventoryArea.Storage:
                if (storageManager == null || !storageManager.IsStorageOpened || slotRef.index >= storageManager.ViewSize)
                    return null;
                return At(storageManager.SlotObjects, slotRef.index);
            default:
                return null;
        }
    }

    private static int IndexOf(GameObject[] slots, InventorySlot slot)
    {
        if (slots == null)
            return -1;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && slots[i] == slot.gameObject)
                return i;
        return -1;
    }

    private static InventorySlot At(GameObject[] slots, int index)
    {
        if (slots == null || index < 0 || index >= slots.Length || slots[index] == null)
            return null;
        return slots[index].GetComponent<InventorySlot>();
    }

    private static InventoryItem ItemIn(InventorySlot slot) =>
        slot != null && slot.heldItem != null ? slot.heldItem.GetComponent<InventoryItem>() : null;

    private static bool HasId(InventoryItem item, string id) =>
        item != null && item.itemScriptableObject != null && !string.IsNullOrEmpty(id) && ItemDatabase.Id(item.itemScriptableObject) == id;

    private Vector3 PlayerPosition
    {
        get
        {
            CombatEntity e = PlayerEntity;
            return e != null ? e.Position : PlayerObject.transform.position;
        }
    }

    // ------------------------------------------------------------------ server: snapshot and appearance
    /// <summary>
    /// The inventory as the owner sees it (server side): layout, every occupied slot, the selection, the quickslots and
    /// the open storage. <paramref name="ack"/> is the last client request handled. Items missing from the Item
    /// Database cannot be named and are left out (with a warning).
    /// </summary>
    public InventorySnapshot CaptureSnapshot(uint ack)
    {
        var s = new InventorySnapshot
        {
            ack = ack,
            hotbarCount = (short)(HotbarSlots != null ? HotbarSlots.Length : 0),
            bagCount = (short)(Slots != null ? Slots.Length : 0),
            grid = gridActive && grid != null,
            gridColumns = (byte)Mathf.Clamp(gridSettings.columns, 0, 255),
            gridRows = (byte)Mathf.Clamp(gridSettings.rows, 0, 255),
            selectedHotbar = (byte)Mathf.Clamp(hotbar.Selected, 0, 255),
            storageSize = (short)(storageManager != null && storageManager.IsStorageOpened ? storageManager.ViewSize : -1),
            storageName = OpenedStorage != null ? OpenedStorage.name : "",
        };
        if (quickSlotBar != null)
        {
            int n = Mathf.Min(quickSlotBar.SlotCount, InventoryNetRules.MaxQuickSlots);
            s.quickSlots = new string[n];
            for (int i = 0; i < n; i++)
            {
                ItemSO q = quickSlotBar.Get(i);
                s.quickSlots[i] = q != null ? ItemDatabase.Id(q) ?? "" : "";
            }
        }
        applyData.Clear();
        CollectStacks(applyData, null, false, s.storageSize);
        s.stacks = applyData.ToArray();
        applyData.Clear();
        s.SortStacks();
        return s;
    }

    /// <summary>What other players see of this inventory (server side): the item in hand, the equipment, drawn or not.</summary>
    public InventoryAppearance CaptureAppearance()
    {
        IReadOnlyList<InventorySlot> eq = EquipmentSlots;
        var ids = new string[eq.Count];
        for (int k = 0; k < eq.Count; k++)
        {
            InventoryItem it = ItemIn(eq[k]);
            ids[k] = it != null && it.itemScriptableObject != null ? ItemDatabase.Id(it.itemScriptableObject) ?? "" : "";
        }
        InventoryItem held = SelectedHotbarItem;
        return new InventoryAppearance
        {
            mainHand = held != null && held.itemScriptableObject != null ? ItemDatabase.Id(held.itemScriptableObject) ?? "" : "",
            mainHandSlot = (byte)Mathf.Clamp(hotbar.Selected, 0, 255),
            equipment = ids,
            drawn = visuals != null && visuals.IsDrawn,
        };
    }

    private void CollectStacks(List<InventoryStackData> into, List<InventoryItem> items, bool appearanceOnly, int storageSlots)
    {
        bool forSnapshot = items == null;
        GameObject[] hotbarSlots = HotbarSlots;
        if (hotbarSlots != null)
            for (int i = 0; i < hotbarSlots.Length; i++)
                AddStack(into, items, InventoryArea.Hotbar, i, At(hotbarSlots, i), forSnapshot);
        GameObject[] bag = Slots;
        if (!appearanceOnly && bag != null)
            for (int i = 0; i < bag.Length; i++)
                AddStack(into, items, InventoryArea.Bag, i, At(bag, i), forSnapshot);
        IReadOnlyList<InventorySlot> eq = EquipmentSlots;
        for (int k = 0; k < eq.Count; k++)
            AddStack(into, items, InventoryArea.Equipment, k, eq[k], forSnapshot);
        if (!appearanceOnly && storageManager != null && storageManager.IsStorageOpened)
        {
            GameObject[] window = storageManager.SlotObjects;
            int n = storageSlots >= 0 ? Mathf.Min(storageSlots, window.Length) : window.Length;
            for (int i = 0; i < n; i++)
                AddStack(into, items, InventoryArea.Storage, i, At(window, i), forSnapshot);
        }
    }

    private void AddStack(List<InventoryStackData> into, List<InventoryItem> items, InventoryArea area, int index, InventorySlot slot, bool forSnapshot)
    {
        InventoryItem it = ItemIn(slot);
        if (it == null || index >= InventoryNetRules.MaxSlotsPerArea)
            return;
        string id = it.itemScriptableObject != null ? ItemDatabase.Id(it.itemScriptableObject) : null;
        if (id == null && forSnapshot)
        {
            WarnMissingId(it.itemScriptableObject);
            return;
        }
        List<int> list = it.DurabilityList;
        int[] durabilities = null;
        if (list != null && list.Count > 0)
        {
            int n = Mathf.Min(list.Count, InventoryNetRules.MaxDurabilities);
            durabilities = new int[n];
            for (int i = 0; i < n; i++)
                durabilities[i] = list[i];
        }
        into.Add(new InventoryStackData
        {
            area = (byte)area,
            index = (short)index,
            itemId = id,
            count = it.stackCurrent,
            durability = it.durability,
            durabilities = durabilities,
            loadedRounds = it.loadedRounds,
            rotated = it.gridRotated,
            cooldown = it.GetRemainingCooldown(),
        });
        items?.Add(it);
    }

    // ------------------------------------------------------------------ client: showing the server's inventory
    /// <summary>
    /// Shows the server's snapshot on a client's own player (no gameplay runs: no weight, no pickups, nothing dropped).
    /// Stacks still in place are kept, moved stacks are moved, the rest is removed or created. While an item is being
    /// dragged the snapshot waits until it is dropped.
    /// </summary>
    public void ApplySnapshot(InventorySnapshot snapshot)
    {
        if (snapshot == null || IsAuthoritative)
            return;
        if ((dragHandler != null && dragHandler.DraggedObject != null) || !SlotsReady)
        {
            pendingSnapshot = snapshot; // shown once the item is dropped / the slots are built
            return;
        }
        pendingSnapshot = null;
        ApplyLayout(snapshot);
        ApplyStorageView(snapshot);
        ApplyStacks(snapshot.stacks, false);
        ApplySelection(snapshot.selectedHotbar);
        ApplyQuickSlots(snapshot.quickSlots);
        FinishApply();
    }

    /// <summary>
    /// Shows what another player holds and wears on this machine's copy of that player. False when it could not be shown
    /// yet (the copy's slots are not built, or this is not another player's copy): call it again later.
    /// </summary>
    public bool ApplyAppearance(InventoryAppearance appearance)
    {
        if (NetRole != InventoryNetRole.RemoteCopy || !SlotsReady)
            return false;
        ApplyStacks(appearance.ToStacks(appearanceStacks), true);
        ApplySelection(appearance.mainHandSlot);
        FinishApply();
        if (visuals != null && visuals.IsDrawn != appearance.drawn)
            visuals.SetDrawn(appearance.drawn);
        return true;
    }

    /// <summary>The hotbar and bag slots exist (they are built in Start).</summary>
    private bool SlotsReady => slotManager != null && slotManager.InventorySlots != null && slotManager.HotbarSlots != null;

    private void ApplyPendingSnapshot()
    {
        if (pendingSnapshot != null && (dragHandler == null || dragHandler.DraggedObject == null) && SlotsReady)
            ApplySnapshot(pendingSnapshot);
    }

    private void FinishApply()
    {
        if (gridActive && gridView != null)
            gridView.LayoutItems();
        RefreshHand(false);
        NotifyInventoryChanged();
    }

    private void ApplySelection(int index)
    {
        GameObject[] slots = HotbarSlots;
        if (slots == null || index < 0 || index >= slots.Length || index == hotbar.Selected)
            return;
        HotbarHandler.SelectSlot(hotbar, index, slots, handParent);
    }

    private void ApplyQuickSlots(string[] ids)
    {
        if (quickSlotBar == null)
            return;
        for (int i = 0; i < quickSlotBar.SlotCount; i++)
        {
            string id = ids != null && i < ids.Length ? ids[i] : null;
            quickSlotBar.SetFromServer(i, string.IsNullOrEmpty(id) ? null : ItemDatabase.Item(id));
        }
    }

    private void ApplyLayout(InventorySnapshot s)
    {
        if (slotManager == null || slotManager.InventorySlots == null)
            return;
        GameObject[] hb = slotManager.HotbarSlots;
        if (s.hotbarCount > 0 && (hb == null || hb.Length != s.hotbarCount))
        {
            numberOfHotBarSlots = s.hotbarCount;
            previousHotbarSlots = s.hotbarCount;
            slotManager.UpdateHotbarSlots(s.hotbarCount);
            storageSlotsDirty = true;
        }
        bool layoutChanged = s.grid != gridActive || (s.grid && (s.gridColumns != gridSettings.columns || s.gridRows != gridSettings.rows));
        if (layoutChanged)
        {
            // The server switched between slots and the grid (or resized it): the bag is rebuilt empty and refilled below.
            foreach (GameObject go in slotManager.InventorySlots)
            {
                InventorySlot slot = go != null ? go.GetComponent<InventorySlot>() : null;
                if (slot != null && slot.heldItem != null)
                {
                    Destroy(slot.heldItem);
                    slot.heldItem = null;
                }
            }
            if (!s.grid && s.bagCount > 0)
                numberOfInventorySlots = s.bagCount;
            previousInventorySlots = numberOfInventorySlots;
            Repack(s.grid, s.grid ? s.gridColumns : gridSettings.columns, s.grid ? s.gridRows : gridSettings.rows);
        }
        else if (!s.grid && s.bagCount > 0 && slotManager.InventorySlots.Length != s.bagCount)
        {
            numberOfInventorySlots = s.bagCount;
            previousInventorySlots = s.bagCount;
            slotManager.UpdateInventorySlots(s.bagCount);
            storageSlotsDirty = true;
        }
    }

    private void ApplyStorageView(InventorySnapshot s)
    {
        if (storageManager == null)
            return;
        if (s.storageSize >= 0)
        {
            if (!storageManager.IsStorageOpened || storageManager.Current != null || storageManager.ViewSize != s.storageSize)
            {
                storageManager.OpenView(s.storageSize);
                uiStateManager?.SetStorageOpened(true);
                if (ReadsLocalInput)
                    SetCursorState(CursorLockMode.None, true);
            }
        }
        else if (storageManager.IsStorageOpened)
        {
            CloseStorageView();
        }
    }

    private void ApplyStacks(IList<InventoryStackData> target, bool appearanceOnly)
    {
        target = target ?? Array.Empty<InventoryStackData>();
        applyData.Clear();
        applyItems.Clear();
        CollectStacks(applyData, applyItems, appearanceOnly, -1);
        InventorySnapshotDiff.Plan(applyData, target, applySteps);

        // Everything that leaves its slot is taken out first, so moves never collide with each other.
        foreach (InventorySnapshotDiff.Step step in applySteps)
        {
            if (step.kind != InventorySnapshotDiff.StepKind.Remove && step.kind != InventorySnapshotDiff.StepKind.Move)
                continue;
            InventoryItem it = applyItems[step.current];
            Detach(it);
            if (step.kind == InventorySnapshotDiff.StepKind.Remove && it != null)
                Destroy(it.gameObject);
        }
        foreach (InventorySnapshotDiff.Step step in applySteps)
        {
            switch (step.kind)
            {
                case InventorySnapshotDiff.StepKind.Move:
                    if (Place(applyItems[step.current], target[step.target]))
                        Write(applyItems[step.current], target[step.target]);
                    break;
                case InventorySnapshotDiff.StepKind.Update:
                    Write(applyItems[step.current], target[step.target]);
                    break;
                case InventorySnapshotDiff.StepKind.Keep:
                    WriteCooldown(applyItems[step.current], target[step.target]);
                    break;
                case InventorySnapshotDiff.StepKind.Create:
                    CreateShown(target[step.target]);
                    break;
            }
        }
        applyData.Clear();
        applyItems.Clear();
        applySteps.Clear();
    }

    private void Detach(InventoryItem item)
    {
        if (item == null)
            return;
        InventorySlot holder = FindSlotHolding(item.gameObject);
        if (holder == null && storageManager != null && storageManager.IsStorageOpened)
            foreach (GameObject go in storageManager.SlotObjects)
            {
                InventorySlot s = go != null ? go.GetComponent<InventorySlot>() : null;
                if (s != null && s.heldItem == item.gameObject)
                {
                    holder = s;
                    break;
                }
            }
        if (holder != null)
            holder.heldItem = null;
    }

    private bool Place(InventoryItem item, InventoryStackData data)
    {
        if (item == null)
            return false;
        InventorySlot slot = SlotAt(data.Slot);
        if (slot == null)
        {
            Destroy(item.gameObject);
            return false;
        }
        ClearOccupant(slot, item.gameObject);
        item.gridRotated = data.rotated;
        slot.SetHeldItem(item.gameObject);
        return true;
    }

    private static void ClearOccupant(InventorySlot slot, GameObject keep)
    {
        if (slot.heldItem != null && slot.heldItem != keep)
        {
            Destroy(slot.heldItem); // a malformed snapshot named one slot twice: the slot shows the last one
            slot.heldItem = null;
        }
    }

    private void CreateShown(InventoryStackData data)
    {
        ItemSO so = ItemDatabase.Item(data.itemId);
        InventorySlot slot = SlotAt(data.Slot);
        if (so == null || slot == null || itemPrefab == null)
        {
            if (so == null && !string.IsNullOrEmpty(data.itemId))
                Debug.LogWarning($"[Inventory] The server sent item '{data.itemId}', which this build's Item Database does not have.", this);
            return;
        }
        ClearOccupant(slot, null);
        GameObject obj = Instantiate(itemPrefab);
        InventoryItem it = obj.GetComponent<InventoryItem>();
        if (it == null)
        {
            Destroy(obj);
            return;
        }
        it.Initialize(so, Mathf.Max(1, data.count), data.durabilities);
        Write(it, data);
        slot.SetHeldItem(obj);
    }

    private static void Write(InventoryItem it, InventoryStackData data)
    {
        if (it == null)
            return;
        ItemSO so = it.itemScriptableObject;
        it.stackMax = so != null ? Mathf.Max(1, so.StackMax) : 1;
        it.stackCurrent = Mathf.Max(1, data.count);
        if (!InventoryNetRules.SameInts(it.DurabilityList, data.durabilities))
            it.DurabilityList = data.durabilities != null ? new List<int>(data.durabilities) : new List<int>();
        it.durability = data.durability;
        it.loadedRounds = data.loadedRounds;
        it.gridRotated = data.rotated;
        it.UpdateTotalWeight();
        it.RefreshUI();
        WriteCooldown(it, data);
    }

    private static void WriteCooldown(InventoryItem it, InventoryStackData data)
    {
        if (it == null)
            return;
        float wanted = Mathf.Max(0f, InventoryNetRules.IsFinite(data.cooldown) ? data.cooldown : 0f);
        if (Mathf.Abs(it.GetRemainingCooldown() - wanted) > 0.25f)
            it.timeSinceLastUse = Time.time + wanted;
    }

    // ------------------------------------------------------------------ server: repeating a client's request
    /// <summary>
    /// Repeats a request of this inventory's owner on the server's copy, through the same code the pointer and keys use
    /// (so every rule applies: slot types, stacking, grid fit, cooldowns, room). The slot named must still hold the item
    /// the client saw, or the request is refused (the next snapshot shows the client the truth). World objects (pickups,
    /// opening a storage) are resolved and range-checked by the network layer, which then calls
    /// <see cref="ItemPicked"/> / <see cref="TryOpenStorage"/>. Returns true when something was done.
    /// </summary>
    public bool ApplyRequest(in InventoryRequest request, out string reason)
    {
        reason = "";
        if (!IsAuthoritative || !InventoryNetRules.IsWellFormed(request))
            return false;
        switch (request.Kind)
        {
            case InventoryRequestKind.Move:
                return ApplyMoveRequest(request);
            case InventoryRequestKind.Split:
            {
                InventoryItem stack = ItemIn(SlotAt(request.from));
                if (!HasId(stack, request.itemId) || request.amount >= stack.stackCurrent)
                    return false;
                return SplitStack(stack, request.amount);
            }
            case InventoryRequestKind.Rotate:
            {
                InventorySlot cell = SlotAt(request.from);
                InventoryItem item = ItemIn(cell);
                if (!HasId(item, request.itemId) || !gridActive || grid == null || !grid.IsCell(cell))
                    return false;
                RotateInPlace(item);
                return true;
            }
            case InventoryRequestKind.SelectHotbar:
                return SelectHotbarSlot(request.amount);
            case InventoryRequestKind.UseSelected:
            {
                if (request.amount != hotbar.Selected && !SelectHotbarSlot(request.amount))
                    return false;
                InventoryItem held = SelectedHotbarItem;
                // Weapons attack through the weapon layer (validated there); this request is for items only.
                if (!HasId(held, request.itemId) || held.itemScriptableObject is WeaponSO)
                    return false;
                return UseSelectedItem();
            }
            case InventoryRequestKind.UseItem:
            {
                ItemSO item = ItemDatabase.Item(request.itemId);
                if (item == null || item is WeaponSO)
                    return false; // throwables go through the weapon layer
                CombatEntity me = PlayerEntity;
                if (me != null && me.IsStunned)
                {
                    reason = "Cannot use items while stunned.";
                    return false;
                }
                return UseItemFromInventory(item, request.flag, out reason);
            }
            case InventoryRequestKind.AssignQuickSlot:
            {
                if (quickSlotBar == null)
                    return false;
                if (string.IsNullOrEmpty(request.itemId))
                {
                    quickSlotBar.Clear(request.amount);
                    return true;
                }
                ItemSO item = ItemDatabase.Item(request.itemId);
                return item != null && quickSlotBar.Assign(request.amount, item);
            }
            case InventoryRequestKind.CloseStorage:
                if (!IsStorageOpened)
                    return false;
                CloseStorage(OpenedStorage);
                return true;
            case InventoryRequestKind.QuickEquipArmor:
                return ItemDatabase.Item(request.itemId) is ArmorSO armor && QuickEquipArmor(armor);
            case InventoryRequestKind.UnequipAllArmor:
                UnequipAllArmor();
                return true;
            case InventoryRequestKind.OptimizeArmor:
                OptimizeArmorSets();
                return true;
            case InventoryRequestKind.SetDrawn:
                if (visuals == null)
                    return false;
                if (visuals.IsDrawn != request.flag)
                    visuals.SetDrawn(request.flag);
                return true;
            case InventoryRequestKind.Resync:
                return true;
            default:
                return false; // world requests are resolved by the network layer
        }
    }

    private bool ApplyMoveRequest(in InventoryRequest r)
    {
        InventorySlot from = SlotAt(r.from);
        InventoryItem item = ItemIn(from);
        if (!HasId(item, r.itemId) || dragHandler == null)
            return false;
        CancelDrag();
        // The same drag the pointer starts: grid origin first (it reads the slots), then the drag itself.
        BeginGridDrag(from, item, null);
        dragHandler.StartDragging(from, from.gameObject);
        if (dragHandler.DraggedObject != item.gameObject)
        {
            EndGridDrag();
            return false;
        }
        if (r.toGrid)
        {
            if (!gridActive || grid == null)
            {
                CancelDrag();
                return false;
            }
            ItemSO so = item.itemScriptableObject;
            bool canTurn = so.CanRotateInGrid && so.GridSize.x != so.GridSize.y;
            dragRotated = canTurn ? r.rotated : item.gridRotated;
            InventoryItem prepared = PrepareGridDrop();
            if (prepared == null)
                return false;
            Vector2Int size = GridInventory.SizeOf(so, dragRotated);
            HandleGridDropAt(prepared, new GridRect(r.gridX, r.gridY, size.x, size.y));
        }
        else if (r.quickIndex >= 0)
        {
            DropDragged(r.quickIndex, null, false, null);
        }
        else if (r.flag)
        {
            Transform body = PlayerObject.transform;
            Vector3 at = InventoryNetRules.ClampDropPoint(r.point, PlayerPosition, body.forward, MaxRequestDropDistance);
            DropDragged(-1, null, true, at);
        }
        else
        {
            InventorySlot to = SlotAt(r.to);
            if (to == null)
            {
                CancelDrag();
                return false;
            }
            DropDragged(-1, to, false, null);
        }
        dragHandler.CleanupDragging();
        EndGridDrag();
        NotifyInventoryChanged();
        return true;
    }
}
