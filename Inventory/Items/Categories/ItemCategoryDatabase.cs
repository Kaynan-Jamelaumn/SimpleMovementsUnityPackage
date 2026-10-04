using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Where an item is listed: its top category (the tab) and, optionally, a subcategory under it.</summary>
public readonly struct ItemCategoryMatch
{
    public readonly ItemCategory Top;
    public readonly ItemCategory Sub;

    public ItemCategoryMatch(ItemCategory top, ItemCategory sub)
    {
        Top = top;
        Sub = sub;
    }

    /// <summary>The most precise category (the subcategory when there is one).</summary>
    public ItemCategory Leaf => Sub != null ? Sub : Top;
    public string Path => Leaf != null ? Leaf.Path : "";
    public bool IsIn(ItemCategory category) => category == null || (Leaf != null && Leaf.IsOrIsUnder(category));
}

/// <summary>
/// The shop categories of the game and how items are sorted into them. One database is shared by every merchant
/// (<see cref="Default"/>: the asset named "ItemCategoryDatabase" in a Resources folder - Tools ▸ SimpleMovements ▸
/// Project Setup ▸ Create Item Category Database - else the built-in tree). A merchant can use its own.
/// <para>An item's place is decided in this order: the Category set on the item; a category that lists the item; the
/// subcategory with the highest Match Priority whose rules match; a top category whose rules match; the Fallback.</para>
/// </summary>
[CreateAssetMenu(fileName = ResourcesName, menuName = "SimpleMovements/Items/Item Category Database", order = 31)]
public class ItemCategoryDatabase : ScriptableObject
{
    public const string ResourcesName = "ItemCategoryDatabase";

    [Tooltip("Every category and subcategory. Top categories become merchant tabs; subcategories become the filters under a tab.")]
    [SerializeField] private List<ItemCategory> categories = new List<ItemCategory>();
    [Tooltip("Where items that match no category go (usually Miscellaneous). Empty = the first top category named Miscellaneous.")]
    [SerializeField] private ItemCategory fallback;

    [NonSerialized] private readonly Dictionary<ItemSO, ItemCategoryMatch> cache = new Dictionary<ItemSO, ItemCategoryMatch>();
    [NonSerialized] private ItemCategory runtimeFallback;

    private static ItemCategoryDatabase defaultDatabase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => defaultDatabase = null;

    /// <summary>The project's database (Resources/ItemCategoryDatabase), else a built-in one made in memory.</summary>
    public static ItemCategoryDatabase Default
    {
        get
        {
            if (defaultDatabase == null)
            {
                defaultDatabase = Resources.Load<ItemCategoryDatabase>(ResourcesName);
                if (defaultDatabase == null)
                    defaultDatabase = CreateDefault();
            }
            return defaultDatabase;
        }
    }

    public IReadOnlyList<ItemCategory> Categories => categories ?? (categories = new List<ItemCategory>());

    /// <summary>Items that match nothing are listed here.</summary>
    public ItemCategory Fallback
    {
        get
        {
            if (fallback != null)
                return fallback;
            foreach (ItemCategory c in Categories)
                if (c != null && !c.IsSubcategory && string.Equals(c.DisplayName, "Miscellaneous", StringComparison.OrdinalIgnoreCase))
                    return c;
            if (runtimeFallback == null)
            {
                runtimeFallback = CreateInstance<ItemCategory>();
                runtimeFallback.hideFlags = HideFlags.DontSave;
                runtimeFallback.Configure("Miscellaneous", null, 1000, 0);
            }
            return runtimeFallback;
        }
    }

    /// <summary>The top categories (tabs), ordered by Sort Order then name.</summary>
    public List<ItemCategory> TopCategories(List<ItemCategory> into = null)
    {
        into = into ?? new List<ItemCategory>();
        into.Clear();
        foreach (ItemCategory c in Categories)
            if (c != null && !c.IsSubcategory && !into.Contains(c))
                into.Add(c);
        ItemCategory fb = Fallback; // items that match nothing need a tab too
        if (fb != null && !fb.IsSubcategory && !into.Contains(fb))
            into.Add(fb);
        into.Sort(Compare);
        return into;
    }

    /// <summary>The direct subcategories of <paramref name="parent"/>, ordered.</summary>
    public List<ItemCategory> ChildrenOf(ItemCategory parent, List<ItemCategory> into = null)
    {
        into = into ?? new List<ItemCategory>();
        into.Clear();
        if (parent == null)
            return into;
        foreach (ItemCategory c in Categories)
            if (c != null && c.Parent == parent && !into.Contains(c))
                into.Add(c);
        into.Sort(Compare);
        return into;
    }

    /// <summary>Order of tabs: Sort Order, then name.</summary>
    public static int Compare(ItemCategory a, ItemCategory b)
    {
        if (a == b) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        int c = a.SortOrder.CompareTo(b.SortOrder);
        return c != 0 ? c : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Where <paramref name="item"/> is listed (cached while playing).</summary>
    public ItemCategoryMatch Resolve(ItemSO item)
    {
        if (item == null)
            return new ItemCategoryMatch(Fallback, null);
        bool useCache = Application.isPlaying;
        if (useCache && cache.TryGetValue(item, out ItemCategoryMatch hit))
            return hit;
        ItemCategoryMatch result = Compute(item);
        if (useCache)
            cache[item] = result;
        return result;
    }

    /// <summary>Forget cached results (after changing categories or items while playing).</summary>
    public void ClearCache() => cache.Clear();

    private ItemCategoryMatch Compute(ItemSO item)
    {
        // 1. The item says where it goes.
        ItemCategory own = item.Category;
        if (own != null)
            return own.IsSubcategory ? new ItemCategoryMatch(own.Root, own) : new ItemCategoryMatch(own, BestChild(own, item));

        // 2. A category lists it (the deepest one wins).
        ItemCategory listed = null;
        foreach (ItemCategory c in Categories)
            if (c != null && c.Lists(item) && (listed == null || Depth(c) > Depth(listed)))
                listed = c;
        if (listed != null)
            return listed.IsSubcategory ? new ItemCategoryMatch(listed.Root, listed) : new ItemCategoryMatch(listed, BestChild(listed, item));

        // 3. The best subcategory by its rules, 4. else a top category by its rules.
        ItemCategory bestSub = null, bestTop = null;
        foreach (ItemCategory c in Categories)
        {
            if (c == null || !c.MatchesRules(item))
                continue;
            if (c.IsSubcategory)
            {
                if (bestSub == null || c.MatchPriority > bestSub.MatchPriority)
                    bestSub = c;
            }
            else if (bestTop == null || c.MatchPriority > bestTop.MatchPriority)
            {
                bestTop = c;
            }
        }
        if (bestSub != null)
            return new ItemCategoryMatch(bestSub.Root, bestSub);
        if (bestTop != null)
            return new ItemCategoryMatch(bestTop, null);

        // 5. Nowhere else.
        ItemCategory fb = Fallback;
        return new ItemCategoryMatch(fb != null ? fb.Root : null, fb != null && fb.IsSubcategory ? fb : null);
    }

    /// <summary>The subcategory of <paramref name="top"/> that fits <paramref name="item"/> best (null = none).</summary>
    private ItemCategory BestChild(ItemCategory top, ItemSO item)
    {
        ItemCategory best = null;
        foreach (ItemCategory c in Categories)
        {
            if (c == null || c == top || !c.IsSubcategory || !c.IsOrIsUnder(top))
                continue;
            if (!c.Lists(item) && !c.MatchesRules(item))
                continue;
            int score = c.Lists(item) ? int.MaxValue : c.MatchPriority;
            int bestScore = best == null ? int.MinValue : best.Lists(item) ? int.MaxValue : best.MatchPriority;
            if (best == null || score > bestScore)
                best = c;
        }
        return best;
    }

    private static int Depth(ItemCategory c)
    {
        int d = 0;
        for (ItemCategory p = c != null ? c.Parent : null; p != null && d < 16; p = p.Parent)
            d++;
        return d;
    }

    public void Validate(List<string> errors, List<string> warnings)
    {
        if (categories == null || categories.Count == 0)
        {
            warnings.Add("The database has no categories: every item goes to the fallback.");
            return;
        }
        var seen = new HashSet<ItemCategory>();
        for (int i = 0; i < categories.Count; i++)
        {
            ItemCategory c = categories[i];
            if (c == null)
            {
                warnings.Add($"Category #{i + 1} is empty.");
                continue;
            }
            if (!seen.Add(c))
                warnings.Add($"'{c.DisplayName}' is listed more than once.");
            if (c.Parent != null && !categories.Contains(c.Parent))
                warnings.Add($"'{c.Path}': its parent '{c.Parent.DisplayName}' is not in this database (it will not have a tab).");
            c.Validate(errors, warnings);
        }
        if (fallback == null && Fallback == runtimeFallback)
            warnings.Add("No fallback category (and none named Miscellaneous): unmatched items go to a built-in 'Miscellaneous' tab.");
    }

    private void OnValidate() => cache.Clear();

    // ------------------------------------------------------------------ the built-in tree
    /// <summary>A database with the standard tree, made in memory (used when the project has none).</summary>
    public static ItemCategoryDatabase CreateDefault()
    {
        var db = CreateInstance<ItemCategoryDatabase>();
        db.name = "Built-in Item Categories";
        db.hideFlags = HideFlags.DontSave;
        db.categories = BuildDefaultTree(() =>
        {
            var c = CreateInstance<ItemCategory>();
            c.hideFlags = HideFlags.DontSave;
            return c;
        });
        db.fallback = db.categories.Find(c => c != null && c.DisplayName == "Miscellaneous");
        return db;
    }

    /// <summary>Sets the categories and the fallback (editor setup tools).</summary>
    public void SetCategories(List<ItemCategory> list, ItemCategory fallbackCategory)
    {
        categories = list ?? new List<ItemCategory>();
        fallback = fallbackCategory;
        cache.Clear();
    }

    /// <summary>
    /// The standard categories: Weapons (Swords, Axes, Hammers &amp; Maces, Daggers, Spears, Bows, Staves &amp; Wands,
    /// Throwing, Fist, Tools, Other), Armor (Helmets, Chest Armor, Gloves, Boots, Leg Armor, Shields, Shoulders, Bracers),
    /// Accessories (Rings, Amulets, Belts, Trinkets, Cloaks), Consumables (Potions, Buffs, Food, Ammunition), Materials
    /// (Ores &amp; Ingots, Wood &amp; Stone, Herbs, Cloth &amp; Leather, Gems, Monster Parts) and Miscellaneous.
    /// <paramref name="make"/> creates each category object (in memory, or as sub-assets in the editor).
    /// </summary>
    public static List<ItemCategory> BuildDefaultTree(Func<ItemCategory> make)
    {
        var list = new List<ItemCategory>();
        ItemCategory Add(string label, ItemCategory parent, int order, int priority, params ItemCategoryRule[] rules)
        {
            ItemCategory c = make();
            c.Configure(label, parent, order, priority, rules);
            list.Add(c);
            return c;
        }
        ItemCategoryRule Weapons(params WeaponCategory[] w) => new ItemCategoryRule(ItemKind.Weapon) { weaponCategories = new List<WeaponCategory>(w) };
        ItemCategoryRule Slots(params SlotType[] s) => new ItemCategoryRule { equipmentSlots = new List<SlotType>(s) };
        ItemCategoryRule Types(ItemKind kind, params ItemType[] t) => new ItemCategoryRule(kind) { itemTypes = new List<ItemType>(t) };
        ItemCategoryRule Materials(params MaterialKind[] m) => new ItemCategoryRule(ItemKind.Material) { materialKinds = new List<MaterialKind>(m) };

        ItemCategory weapons = Add("Weapons", null, 0, 0, new ItemCategoryRule(ItemKind.Weapon));
        Add("Swords", weapons, 0, 0, Weapons(WeaponCategory.Sword, WeaponCategory.Greatsword));
        Add("Axes", weapons, 1, 0, Weapons(WeaponCategory.Axe));
        Add("Hammers & Maces", weapons, 2, 0, Weapons(WeaponCategory.Hammer, WeaponCategory.Mace));
        Add("Daggers", weapons, 3, 0, Weapons(WeaponCategory.Dagger));
        Add("Spears", weapons, 4, 0, Weapons(WeaponCategory.Spear));
        Add("Bows", weapons, 5, 0, Weapons(WeaponCategory.Bow, WeaponCategory.Crossbow));
        Add("Staves & Wands", weapons, 6, 0, Weapons(WeaponCategory.Staff, WeaponCategory.Wand));
        Add("Throwing", weapons, 7, 0, Weapons(WeaponCategory.Thrown));
        Add("Fist Weapons", weapons, 8, 0, Weapons(WeaponCategory.Fist));
        Add("Tools", weapons, 9, 0, Weapons(WeaponCategory.Tool));
        Add("Other Weapons", weapons, 10, -10, new ItemCategoryRule(ItemKind.Weapon));

        ItemCategory armor = Add("Armor", null, 1, 0, Slots(SlotType.Helmet, SlotType.Armor, SlotType.Gloves, SlotType.Boots, SlotType.Leggings,
            SlotType.Shield, SlotType.Shoulders, SlotType.Wrist));
        Add("Helmets", armor, 0, 0, Slots(SlotType.Helmet));
        Add("Chest Armor", armor, 1, 0, Slots(SlotType.Armor));
        Add("Gloves", armor, 2, 0, Slots(SlotType.Gloves));
        Add("Boots", armor, 3, 0, Slots(SlotType.Boots));
        Add("Leg Armor", armor, 4, 0, Slots(SlotType.Leggings));
        Add("Shields", armor, 5, 5, Slots(SlotType.Shield), Weapons(WeaponCategory.Shield));
        Add("Shoulders", armor, 6, 0, Slots(SlotType.Shoulders));
        Add("Bracers", armor, 7, 0, Slots(SlotType.Wrist));

        ItemCategory accessories = Add("Accessories", null, 2, 0, Slots(SlotType.Ring, SlotType.Amulet, SlotType.Belt, SlotType.Trinket, SlotType.Cloak),
            new ItemCategoryRule(ItemKind.Accessory));
        Add("Rings", accessories, 0, 0, Slots(SlotType.Ring));
        Add("Amulets", accessories, 1, 0, Slots(SlotType.Amulet));
        Add("Belts", accessories, 2, 0, Slots(SlotType.Belt));
        Add("Trinkets", accessories, 3, 0, Slots(SlotType.Trinket));
        Add("Cloaks", accessories, 4, 0, Slots(SlotType.Cloak));

        ItemCategory consumables = Add("Consumables", null, 3, 0, new ItemCategoryRule(ItemKind.Consumable), new ItemCategoryRule(ItemKind.Ammo));
        Add("Potions", consumables, 0, 0, Types(ItemKind.Consumable, ItemType.Potion));
        Add("Buffs", consumables, 1, 5, new ItemCategoryRule(ItemKind.Consumable) { itemTypes = new List<ItemType> { ItemType.Potion }, consumableEffects = ConsumableEffectFilter.HasTimedEffect });
        Add("Food", consumables, 2, 0, Types(ItemKind.Consumable, ItemType.Food));
        Add("Ammunition", consumables, 3, 0, new ItemCategoryRule(ItemKind.Ammo));
        Add("Other Consumables", consumables, 4, -10, new ItemCategoryRule(ItemKind.Consumable));

        ItemCategory materials = Add("Materials", null, 4, 0, new ItemCategoryRule(ItemKind.Material));
        Add("Ores & Ingots", materials, 0, 0, Materials(MaterialKind.Ore, MaterialKind.Ingot));
        Add("Wood & Stone", materials, 1, 0, Materials(MaterialKind.Wood, MaterialKind.Stone));
        Add("Herbs", materials, 2, 0, Materials(MaterialKind.Herb));
        Add("Cloth & Leather", materials, 3, 0, Materials(MaterialKind.Cloth, MaterialKind.Leather));
        Add("Gems", materials, 4, 0, Materials(MaterialKind.Gem));
        Add("Monster Parts", materials, 5, 0, Materials(MaterialKind.MonsterPart, MaterialKind.Essence));
        Add("Other Materials", materials, 6, -10, new ItemCategoryRule(ItemKind.Material));

        Add("Miscellaneous", null, 5, 0, new ItemCategoryRule(ItemKind.Miscellaneous));
        return list;
    }
}
