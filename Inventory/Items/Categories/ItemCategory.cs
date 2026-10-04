using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Which item classes a category rule accepts.</summary>
public enum ItemKind
{
    /// <summary>Any item.</summary>
    Any,
    /// <summary><see cref="WeaponSO"/> (weapons and tools).</summary>
    Weapon,
    /// <summary><see cref="ArmorSO"/> (helmets, chest pieces, shields, armor rings...).</summary>
    Armor,
    /// <summary>An <see cref="EquippableSO"/> that is not armor (trinkets, rings and amulets made as Equippables).</summary>
    Accessory,
    /// <summary>Any <see cref="EquippableSO"/>, armor included.</summary>
    Equippable,
    /// <summary><see cref="ConsumableSO"/> (potions, food).</summary>
    Consumable,
    /// <summary><see cref="AmmoSO"/>.</summary>
    Ammo,
    /// <summary><see cref="MaterialSO"/> or an item of type Material.</summary>
    Material,
    /// <summary><see cref="MiscItemSO"/> or an item of type Miscellaneous.</summary>
    Miscellaneous,
}

/// <summary>Timed effects of a consumable, for rules that tell buffs from plain potions.</summary>
public enum ConsumableEffectFilter
{
    Any,
    /// <summary>Every effect is instant (a healing potion).</summary>
    InstantOnly,
    /// <summary>At least one effect lasts over time (a strength elixir, a regeneration potion).</summary>
    HasTimedEffect,
}

/// <summary>
/// One way an item can belong to an <see cref="ItemCategory"/>. Every condition that is set must hold; empty lists and
/// an empty name filter accept anything. A rule with no condition at all accepts every item (a catch-all).
/// </summary>
[Serializable]
public class ItemCategoryRule
{
    [Tooltip("Which item classes the rule accepts (Weapon, Armor, Consumable, Material...).")]
    public ItemKind kind = ItemKind.Any;
    [Tooltip("Item types accepted (the item's Item Type). Empty = any.")]
    public List<ItemType> itemTypes = new List<ItemType>();
    [Tooltip("Equipment slots accepted: the slot the item is worn in (armor by its armor slot, other items by their type). Empty = any.")]
    public List<SlotType> equipmentSlots = new List<SlotType>();
    [Tooltip("Weapon families accepted (weapons only). Empty = any.")]
    public List<WeaponCategory> weaponCategories = new List<WeaponCategory>();
    [Tooltip("Material kinds accepted (materials only). Empty = any.")]
    public List<MaterialKind> materialKinds = new List<MaterialKind>();
    [Tooltip("Consumables only: Instant Only = no effect over time; Has Timed Effect = buffs, regeneration.")]
    public ConsumableEffectFilter consumableEffects = ConsumableEffectFilter.Any;
    [Tooltip("Comma-separated words; the item's name must contain one of them (case does not matter). Empty = any name.")]
    public string nameContains = "";

    public ItemCategoryRule() { }

    public ItemCategoryRule(ItemKind kind) { this.kind = kind; }

    /// <summary>Has no condition: accepts every item.</summary>
    public bool IsCatchAll => kind == ItemKind.Any && Empty(itemTypes) && Empty(equipmentSlots) && Empty(weaponCategories) &&
                              Empty(materialKinds) && consumableEffects == ConsumableEffectFilter.Any && string.IsNullOrWhiteSpace(nameContains);

    public bool Matches(ItemSO item)
    {
        if (item == null || !KindMatches(item))
            return false;
        if (!Empty(itemTypes) && !itemTypes.Contains(item.ItemType))
            return false;
        if (!Empty(equipmentSlots) && !equipmentSlots.Contains(SlotTypeHelper.RequiredSlot(item)))
            return false;
        if (!Empty(weaponCategories) && !(item is WeaponSO w && weaponCategories.Contains(w.Category)))
            return false;
        if (!Empty(materialKinds) && !(item is MaterialSO m && materialKinds.Contains(m.Kind)))
            return false;
        if (consumableEffects != ConsumableEffectFilter.Any)
        {
            if (!(item is ConsumableSO c))
                return false;
            bool timed = HasTimedEffect(c);
            if (consumableEffects == ConsumableEffectFilter.HasTimedEffect ? !timed : timed)
                return false;
        }
        return NameMatches(item);
    }

    private bool KindMatches(ItemSO item)
    {
        switch (kind)
        {
            case ItemKind.Weapon: return item is WeaponSO;
            case ItemKind.Armor: return item is ArmorSO;
            case ItemKind.Accessory: return item is EquippableSO && !(item is ArmorSO);
            case ItemKind.Equippable: return item is EquippableSO;
            case ItemKind.Consumable: return item is ConsumableSO;
            case ItemKind.Ammo: return item is AmmoSO;
            case ItemKind.Material: return item is MaterialSO || item.ItemType == ItemType.Material;
            case ItemKind.Miscellaneous: return item is MiscItemSO || item.ItemType == ItemType.Miscellaneous;
            default: return true;
        }
    }

    private bool NameMatches(ItemSO item)
    {
        if (string.IsNullOrWhiteSpace(nameContains))
            return true;
        string itemName = string.IsNullOrEmpty(item.Name) ? item.name : item.Name;
        foreach (string word in nameContains.Split(','))
        {
            string w = word.Trim();
            if (w.Length > 0 && itemName.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static bool HasTimedEffect(ConsumableSO c)
    {
        if (c.Effects == null)
            return false;
        foreach (ConsumableEffect e in c.Effects)
            if (e != null && (e.timeBuffEffect > 0f || (e.randomTimeBuffEffect && e.maxTimeBuffEffect > 0f)))
                return true;
        return false;
    }

    private static bool Empty<T>(List<T> list) => list == null || list.Count == 0;

    /// <summary>"Weapon · Sword, Greatsword" (inspectors).</summary>
    public string Describe()
    {
        if (IsCatchAll)
            return "every item";
        var parts = new List<string>();
        if (kind != ItemKind.Any) parts.Add(kind.ToString());
        if (!Empty(itemTypes)) parts.Add("type " + string.Join("/", itemTypes));
        if (!Empty(equipmentSlots)) parts.Add("slot " + string.Join("/", equipmentSlots));
        if (!Empty(weaponCategories)) parts.Add(string.Join("/", weaponCategories));
        if (!Empty(materialKinds)) parts.Add(string.Join("/", materialKinds));
        if (consumableEffects != ConsumableEffectFilter.Any) parts.Add(consumableEffects == ConsumableEffectFilter.HasTimedEffect ? "timed effects" : "instant effects");
        if (!string.IsNullOrWhiteSpace(nameContains)) parts.Add($"name has \"{nameContains}\"");
        return string.Join(" · ", parts);
    }
}

/// <summary>
/// A shop category (Weapons, Armor, Consumables...) or, with a <see cref="Parent"/>, a subcategory (Weapons ▸ Swords).
/// Items belong to it when they set it as their Category, when it lists them in Items, or when one of its Rules
/// matches them (see <see cref="ItemCategoryDatabase.Resolve"/>). Merchants build their tabs from these assets, so new
/// categories need no code.
/// </summary>
[CreateAssetMenu(fileName = "Item Category", menuName = "SimpleMovements/Items/Item Category", order = 30)]
public class ItemCategory : ScriptableObject
{
    [Tooltip("Name shown on the tab. Empty = the asset's name.")]
    [SerializeField] private string displayName;
    [Tooltip("Optional text (tooltips, your own UI).")]
    [TextArea(1, 3)]
    [SerializeField] private string description;
    [Tooltip("Optional icon shown on the tab next to the name.")]
    [SerializeField] private Sprite icon;
    [Tooltip("Tint of the tab's accent (and of the category label in item details).")]
    [SerializeField] private Color color = new Color(0.35f, 0.6f, 1f, 1f);
    [Tooltip("Tabs are ordered by this (lowest first), then by name.")]
    [SerializeField] private int sortOrder;
    [Tooltip("The category this one is a subcategory of (Swords → Weapons). Empty = a top category (a tab).")]
    [SerializeField] private ItemCategory parent;

    [Header("Membership")]
    [Tooltip("Items that always belong here, whatever the rules say (unless the item sets another Category itself).")]
    [SerializeField] private List<ItemSO> items = new List<ItemSO>();
    [Tooltip("An item belongs here when ANY rule matches it.")]
    [SerializeField] private List<ItemCategoryRule> rules = new List<ItemCategoryRule>();
    [Tooltip("When several subcategories match an item, the highest priority wins (then the first in the database). " +
             "Catch-alls such as 'Other Weapons' use a negative priority.")]
    [SerializeField] private int matchPriority;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public Color Color => color;
    public int SortOrder => sortOrder;
    public int MatchPriority => matchPriority;
    public ItemCategory Parent => parent;
    public bool IsSubcategory => parent != null;
    public IReadOnlyList<ItemSO> Items => items ?? (items = new List<ItemSO>());
    public IReadOnlyList<ItemCategoryRule> Rules => rules ?? (rules = new List<ItemCategoryRule>());

    /// <summary>The top category above this one (itself when it is a top category).</summary>
    public ItemCategory Root
    {
        get
        {
            ItemCategory c = this;
            for (int guard = 0; c.parent != null && guard < 16; guard++)
                c = c.parent;
            return c;
        }
    }

    /// <summary>"Weapons ▸ Swords".</summary>
    public string Path
    {
        get
        {
            string p = DisplayName;
            ItemCategory c = parent;
            for (int guard = 0; c != null && guard < 16; guard++, c = c.parent)
                p = c.DisplayName + " ▸ " + p;
            return p;
        }
    }

    /// <summary>Is this category <paramref name="other"/> or one of its subcategories?</summary>
    public bool IsOrIsUnder(ItemCategory other)
    {
        if (other == null)
            return false;
        ItemCategory c = this;
        for (int guard = 0; c != null && guard < 16; guard++, c = c.parent)
            if (c == other)
                return true;
        return false;
    }

    /// <summary>Does it list <paramref name="item"/> in its Items?</summary>
    public bool Lists(ItemSO item) => item != null && items != null && items.Contains(item);

    /// <summary>Does one of its rules match <paramref name="item"/>?</summary>
    public bool MatchesRules(ItemSO item)
    {
        if (item == null || rules == null)
            return false;
        for (int i = 0; i < rules.Count; i++)
            if (rules[i] != null && rules[i].Matches(item))
                return true;
        return false;
    }

    /// <summary>Configures a category made from code (the default database, editor setup tools).</summary>
    public void Configure(string label, ItemCategory parentCategory, int order, int priority, params ItemCategoryRule[] matchRules)
    {
        displayName = label;
        name = label;
        parent = parentCategory;
        sortOrder = order;
        matchPriority = priority;
        rules = new List<ItemCategoryRule>(matchRules ?? Array.Empty<ItemCategoryRule>());
    }

    /// <summary>Sets the parent (editor tools); refuses a parent that would make a loop.</summary>
    public bool SetParent(ItemCategory newParent)
    {
        for (ItemCategory c = newParent; c != null; c = c.parent)
            if (c == this)
                return false;
        parent = newParent;
        return true;
    }

    public void Validate(List<string> errors, List<string> warnings)
    {
        for (ItemCategory c = parent; c != null; c = c.parent)
            if (c == this)
            {
                errors.Add($"'{DisplayName}' is its own parent (directly or through other categories).");
                break;
            }
        if ((rules == null || rules.Count == 0) && (items == null || items.Count == 0))
            warnings.Add($"'{DisplayName}' has no rules and lists no items: only items whose Category is set to it are listed.");
        if (rules != null)
            for (int i = 0; i < rules.Count; i++)
                if (rules[i] != null && rules[i].IsCatchAll && matchPriority >= 0 && parent != null)
                    warnings.Add($"'{Path}' rule #{i + 1} has no condition: it takes every item no other subcategory takes first. Give it a negative Match Priority.");
    }

    private void OnValidate()
    {
        if (parent == this)
            parent = null;
        for (ItemCategory c = parent; c != null; c = c.parent)
            if (c == this)
            {
                Debug.LogWarning($"[Categories] '{DisplayName}' cannot be under '{parent.DisplayName}' (that makes a loop): parent cleared.", this);
                parent = null;
                break;
            }
    }
}
