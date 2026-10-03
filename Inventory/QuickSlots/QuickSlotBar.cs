using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Quickslots for consumables: potions, food, buffs, throwables used with one key without opening the inventory or
/// changing the item in hand.
/// <para>
/// A quickslot remembers an item DEFINITION (the asset), never a stack: it shows how many the inventory holds and uses the
/// first stack it finds. So stacks can be split, moved, merged, emptied or dropped without stale references, and nothing
/// is ever duplicated - a quickslot only points at items that are in the inventory. An emptied quickslot keeps its item
/// (greyed) until more are picked up, unless Clear When Depleted is on.
/// </para>
/// Assign: drag an inventory item onto a quickslot, or press a quickslot key while the pointer is over an item in the open
/// inventory. Right click clears a quickslot. Use: its key (input actions QuickSlot1..N, else the fallback keys), the
/// gamepad D-pad, or a click. Built and wired by the <see cref="InventoryManager"/> (Quick Slots option).
/// </summary>
[DisallowMultipleComponent]
public class QuickSlotBar : MonoBehaviour
{
    [Tooltip("The inventory the items come from (found automatically).")]
    [SerializeField] private InventoryManager inventory;
    [Tooltip("Number of quickslots.")]
    [SerializeField, Range(1, 8)] private int slotCount = 4;
    [Tooltip("The item each quickslot uses (a definition, not a stack). Set them here, by dragging items onto the bar, or from code.")]
    [SerializeField] private List<ItemSO> assigned = new List<ItemSO>();

    [Header("Input")]
    [Tooltip("Action names per slot: {0} is the slot number (1, 2...). The first name found in the player's input actions is used.")]
    [SerializeField] private string[] actionNamePatterns = { "QuickSlot{0}", "Quickslot{0}", "QuickItem{0}", "UseQuickSlot{0}" };
    [Tooltip("Keys used when the input actions have no quickslot action (slot 1, 2...). A key already used by another action is skipped.")]
    [SerializeField] private Key[] fallbackKeys = { Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.F1, Key.F2 };
    [Tooltip("Gamepad D-pad up / right / down / left use quickslots 1-4.")]
    [SerializeField] private bool gamepadDpad = true;

    [Header("Rules")]
    [Tooltip("Stacks in the hotbar count and are used too (off = only the bag).")]
    [SerializeField] private bool includeHotbar = true;
    [Tooltip("Seconds between any two quickslot uses (stops accidental double uses). The item's own Cooldown also applies, per item.")]
    [SerializeField, Min(0f)] private float sharedCooldown = 0.35f;
    [Tooltip("A quickslot whose item ran out is emptied (off = it keeps the item, greyed, until more are picked up).")]
    [SerializeField] private bool clearWhenDepleted = false;
    [Tooltip("Quickslots can be used while the inventory is open.")]
    [SerializeField] private bool usableWhileInventoryOpen = false;

    [Header("UI")]
    [Tooltip("Build the bar's UI next to the hotbar when there is none.")]
    [SerializeField] private bool buildUI = true;
    [Tooltip("Size of a quickslot cell (pixels).")]
    [SerializeField, Min(24f)] private float cellSize = 60f;
    [Tooltip("The cells (built automatically when empty).")]
    [SerializeField] private List<QuickSlotCell> cells = new List<QuickSlotCell>();
    [SerializeField] private RectTransform barRoot;

    /// <summary>A quickslot was used (slot, item).</summary>
    public event Action<int, ItemSO> Used;
    /// <summary>A quickslot could not be used (slot, reason shown to the player).</summary>
    public event Action<int, string> Failed;
    /// <summary>An assignment changed.</summary>
    public event Action Changed;

    private readonly Dictionary<ItemSO, float> itemCooldownUntil = new Dictionary<ItemSO, float>(ReferenceComparer<ItemSO>.Instance);
    private readonly Dictionary<ItemSO, float> itemCooldownLength = new Dictionary<ItemSO, float>(ReferenceComparer<ItemSO>.Instance);
    private readonly List<CombatInputBinding> bindings = new List<CombatInputBinding>();
    private readonly List<RaycastResult> raycastBuffer = new List<RaycastResult>();
    private float sharedUntil;
    private bool dpadUsable;

    public int SlotCount => slotCount;
    public InventoryManager Inventory => inventory;

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<InventoryManager>();
        if (inventory == null)
            inventory = GetComponentInParent<InventoryManager>();
        Resize();
    }

    private void Start()
    {
        if (inventory != null && CombatInputBinding.IsPlayer(inventory))
        {
            for (int i = 0; i < slotCount; i++)
            {
                var names = new string[actionNamePatterns != null ? actionNamePatterns.Length : 0];
                for (int n = 0; n < names.Length; n++)
                    names[n] = string.Format(actionNamePatterns[n], i + 1);
                Key key = fallbackKeys != null && i < fallbackKeys.Length ? fallbackKeys[i] : Key.None;
                bindings.Add(CombatInputBinding.Create(inventory, null, names, key, MouseFallback.None, $"Quickslot {i + 1}"));
            }
            dpadUsable = gamepadDpad && !DpadBound();
        }
        if (buildUI && cells.Count == 0)
            BuildUI();
        else
            for (int i = 0; i < cells.Count; i++)
                if (cells[i] != null) cells[i].Bind(this, i);
        if (inventory != null)
            inventory.InventoryChanged += OnInventoryChanged;
    }

    private void OnDestroy()
    {
        foreach (CombatInputBinding b in bindings)
            b?.Dispose();
        bindings.Clear();
        if (inventory != null)
            inventory.InventoryChanged -= OnInventoryChanged;
    }

    private void OnValidate()
    {
        Resize();
    }

    private void Resize()
    {
        if (assigned == null)
            assigned = new List<ItemSO>();
        while (assigned.Count < slotCount) assigned.Add(null);
        while (assigned.Count > slotCount) assigned.RemoveAt(assigned.Count - 1);
    }

    private void Update()
    {
        if (inventory == null)
            return;
        for (int i = 0; i < bindings.Count && i < slotCount; i++)
        {
            if (bindings[i] == null || !bindings[i].Pressed)
                continue;
            // Over an item in the open inventory: the key assigns that item instead.
            if (inventory.IsInventoryOpened && TryAssignHovered(i))
                continue;
            TryUse(i);
        }
        if (dpadUsable && Gamepad.current != null && Gamepad.current.dpad != null)
        {
            var d = Gamepad.current.dpad;
            if (slotCount > 0 && d.up != null && d.up.wasPressedThisFrame) TryUse(0);
            if (slotCount > 1 && d.right != null && d.right.wasPressedThisFrame) TryUse(1);
            if (slotCount > 2 && d.down != null && d.down.wasPressedThisFrame) TryUse(2);
            if (slotCount > 3 && d.left != null && d.left.wasPressedThisFrame) TryUse(3);
        }
        RefreshUI();
    }

    private bool DpadBound()
    {
        // The D-pad fallback is skipped when the input actions use it for something else.
        var shared = inventory != null ? SharedPlayerInput.Acquire(inventory) : null;
        if (shared == null)
            return false;
        try
        {
            InputActionAsset asset = shared.asset;
            return CombatInputBinding.IsBound(asset, "<Gamepad>/dpad/up", out _) || CombatInputBinding.IsBound(asset, "<Gamepad>/dpad", out _);
        }
        finally
        {
            SharedPlayerInput.Release(shared);
        }
    }

    // ------------------------------------------------------------------ assignment
    /// <summary>The item of a quickslot (null = empty).</summary>
    public ItemSO Get(int index) => index >= 0 && index < assigned.Count ? assigned[index] : null;

    /// <summary>Can this item go in a quickslot (consumables, throwables)?</summary>
    public static bool IsCompatible(ItemSO item) => item != null && item.CanUseFromQuickSlot;

    /// <summary>
    /// Puts <paramref name="item"/> in quickslot <paramref name="index"/> (a reference: the item stays in the inventory).
    /// An item already in another quickslot moves (the two swap). Returns false (with feedback) for items that cannot be quick-used.
    /// </summary>
    public bool Assign(int index, ItemSO item)
    {
        if (index < 0 || index >= slotCount)
            return false;
        if (item == null)
        {
            Clear(index);
            return true;
        }
        if (!IsCompatible(item))
        {
            Feedback(index, $"{item.Name} cannot go in a quickslot (only consumables and throwables).");
            return false;
        }
        int existing = assigned.IndexOf(item);
        if (existing == index)
            return true;
        if (existing >= 0)
            assigned[existing] = assigned[index]; // swap the two quickslots
        assigned[index] = item;
        Changed?.Invoke();
        cells.ForEach(c => { if (c != null && c.Index == index) c.Flash(true); });
        return true;
    }

    /// <summary>Empties a quickslot.</summary>
    public void Clear(int index)
    {
        if (index < 0 || index >= assigned.Count || assigned[index] == null)
            return;
        assigned[index] = null;
        Changed?.Invoke();
    }

    /// <summary>Assigns the item under the pointer in the open inventory (true when there was one).</summary>
    private bool TryAssignHovered(int index)
    {
        if (EventSystem.current == null || Mouse.current == null)
            return false;
        var e = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
        InventorySlot slot = inventory.FindSlotUnderPointer(e);
        InventoryItem item = slot != null && slot.heldItem != null ? slot.heldItem.GetComponent<InventoryItem>() : null;
        if (item == null || item.itemScriptableObject == null)
            return false;
        Assign(index, item.itemScriptableObject);
        return true;
    }

    /// <summary>The quickslot cell under the pointer, or null (dropping a dragged item there assigns it).</summary>
    public QuickSlotCell CellUnder(PointerEventData eventData)
    {
        if (eventData == null || EventSystem.current == null)
            return null;
        raycastBuffer.Clear();
        EventSystem.current.RaycastAll(eventData, raycastBuffer);
        foreach (RaycastResult r in raycastBuffer)
        {
            QuickSlotCell c = r.gameObject != null ? r.gameObject.GetComponentInParent<QuickSlotCell>() : null;
            if (c != null && c.Bar == this)
                return c;
        }
        return null;
    }

    // ------------------------------------------------------------------ use
    /// <summary>How many of a quickslot's item the inventory holds.</summary>
    public int Count(int index)
    {
        ItemSO item = Get(index);
        return item != null && inventory != null ? inventory.CountItem(item, includeHotbar) : 0;
    }

    /// <summary>Seconds left before a quickslot can be used again.</summary>
    public float CooldownRemaining(int index)
    {
        ItemSO item = Get(index);
        float now = Time.time;
        float own = item != null && itemCooldownUntil.TryGetValue(item, out float until) ? until - now : 0f;
        return Mathf.Max(0f, Mathf.Max(own, sharedUntil - now));
    }

    /// <summary>
    /// Uses a quickslot: checks there is one, that it is ready and usable now, then consumes one from the first stack
    /// and applies the item (or throws it). Returns true if it was used; otherwise the reason is shown.
    /// </summary>
    public bool TryUse(int index)
    {
        if (inventory == null || index < 0 || index >= slotCount)
            return false;
        ItemSO item = Get(index);
        if (item == null)
            return Fail(index, $"Quickslot {index + 1} is empty.");
        if (inventory.IsInventoryOpened && !usableWhileInventoryOpen)
            return false;
        CombatEntity me = inventory.PlayerEntity;
        if (me != null && me.IsDead)
            return false;
        if (me != null && me.IsStunned)
            return Fail(index, "Cannot use items while stunned.");
        float wait = CooldownRemaining(index);
        if (wait > 0.05f && Time.time < (itemCooldownUntil.TryGetValue(item, out float u) ? u : 0f))
            return Fail(index, $"{item.Name} is on cooldown ({wait:0.0}s).");
        if (Time.time < sharedUntil)
            return false; // the short shared lockout: silent
        if (inventory.CountItem(item, includeHotbar) <= 0)
        {
            if (clearWhenDepleted)
                Clear(index);
            return Fail(index, $"No {item.Name} left.");
        }

        bool used = inventory.UseItemFromInventory(item, includeHotbar, out string reason);
        if (!used)
            return Fail(index, string.IsNullOrEmpty(reason) ? $"{item.Name} cannot be used now." : reason);

        sharedUntil = Time.time + sharedCooldown;
        float cooldown = CombatStats.ItemCooldown(inventory, item.Cooldown);
        if (cooldown > 0f)
        {
            itemCooldownUntil[item] = Time.time + cooldown;
            itemCooldownLength[item] = cooldown;
        }
        foreach (QuickSlotCell c in cells)
            if (c != null && c.Index == index) c.Flash(true);
        Used?.Invoke(index, item);
        if (clearWhenDepleted && inventory.CountItem(item, includeHotbar) <= 0)
            Clear(index);
        return true;
    }

    private bool Fail(int index, string reason)
    {
        Feedback(index, reason);
        Failed?.Invoke(index, reason);
        return false;
    }

    private void Feedback(int index, string reason)
    {
        foreach (QuickSlotCell c in cells)
            if (c != null && c.Index == index) c.Flash(false);
        inventory?.ShowMessage(reason, InventoryFeedback.Kind.Warning);
    }

    private void OnInventoryChanged()
    {
        if (!clearWhenDepleted || inventory == null)
            return;
        for (int i = 0; i < assigned.Count; i++)
            if (assigned[i] != null && inventory.CountItem(assigned[i], includeHotbar) <= 0)
                assigned[i] = null;
    }

    // ------------------------------------------------------------------ UI
    /// <summary>The key shown on a quickslot.</summary>
    private static readonly string[] DpadNames = { "D-Pad Up", "D-Pad Right", "D-Pad Down", "D-Pad Left" };

    /// <summary>The key of a quickslot for the device in use (keyboard or gamepad, never both); empty when it has none.</summary>
    public string KeyLabel(int index)
    {
        bool pad = InputDeviceTracker.Current == InputDeviceTracker.Kind.Gamepad;
        if (index < bindings.Count && bindings[index] != null)
        {
            CombatInputBinding b = bindings[index];
            if (b.HasAction)
            {
                string shown = InputDeviceTracker.DisplayString(b.Action);
                if (!string.IsNullOrEmpty(shown))
                    return shown;
            }
            else if (!pad && fallbackKeys != null && index < fallbackKeys.Length && fallbackKeys[index] != Key.None && b.HasAny)
            {
                return fallbackKeys[index].ToString();
            }
        }
        if (pad && dpadUsable && index < DpadNames.Length)
            return DpadNames[index];
        return "";
    }

    private void RefreshUI()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            QuickSlotCell c = cells[i];
            if (c == null)
                continue;
            ItemSO item = Get(c.Index);
            float left = CooldownRemaining(c.Index);
            float length = item != null && itemCooldownLength.TryGetValue(item, out float l) ? Mathf.Max(l, sharedCooldown) : sharedCooldown;
            c.Refresh(item, Count(c.Index), KeyLabel(c.Index), length > 0f ? left / length : 0f);
        }
    }

    /// <summary>Builds the bar's cells next to the hotbar (or at the bottom of the inventory's canvas).</summary>
    public void BuildUI()
    {
        Transform parent = barRoot != null ? barRoot.parent : null;
        if (barRoot == null)
        {
            Canvas canvas = inventory != null ? inventory.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            if (canvas == null && inventory != null && inventory.HotbarSlots != null && inventory.HotbarSlots.Length > 0 && inventory.HotbarSlots[0] != null)
                canvas = inventory.HotbarSlots[0].GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[Quickslots] No canvas found for the quickslot bar: assign Bar Root, or build the inventory UI first.", this);
                return;
            }
            parent = inventory != null ? inventory.transform : canvas.transform;
            if (!(parent is RectTransform))
                parent = canvas.transform;
            barRoot = InventoryUIFactory.Rect("QuickSlotBar", parent);
            barRoot.anchorMin = barRoot.anchorMax = new Vector2(0.5f, 0f);
            barRoot.pivot = new Vector2(0f, 0f);
            barRoot.anchoredPosition = new Vector2(HotbarHalfWidth() + 24f, 16f);
            var bg = barRoot.gameObject.AddComponent<UnityEngine.UI.Image>();
            bg.color = InventoryUIFactory.PanelColor;
            bg.raycastTarget = false;
            var h = InventoryUIFactory.Horizontal(barRoot.gameObject, 6f);
            h.padding = new RectOffset(6, 6, 6, 6);
            h.childForceExpandHeight = false;
            var fit = barRoot.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fit.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        }
        cells.Clear();
        for (int i = barRoot.childCount - 1; i >= 0; i--)
            if (barRoot.GetChild(i).GetComponent<QuickSlotCell>() != null)
                Destroy(barRoot.GetChild(i).gameObject);
        for (int i = 0; i < slotCount; i++)
            cells.Add(QuickSlotCell.Create(barRoot, this, i, cellSize));
        barRoot.SetAsLastSibling();
        RefreshUI();
    }

    private float HotbarHalfWidth()
    {
        if (inventory == null || inventory.HotbarSlots == null || inventory.HotbarSlots.Length == 0 || inventory.HotbarSlots[0] == null)
            return 220f;
        Transform panel = inventory.HotbarSlots[0].transform.parent;
        if (panel != null && panel.parent is RectTransform rt)
            return rt.rect.width * 0.5f;
        return 220f;
    }
}
