#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace InventorySystem.Tests
{
    /// <summary>
    /// Edit Mode tests (Window > General > Test Runner) for the merchant system: grid packing of shop items, item category
    /// resolution, prices, stock and what a merchant buys. Only compiled when the Unity Test Framework is present.
    /// </summary>
    public class MerchantSystemTests
    {
        private readonly List<Object> created = new List<Object>();

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (Object o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        private T Item<T>(string name, float price, System.Action<SerializedObject> setup = null) where T : ItemSO
        {
            T item = Track(ScriptableObject.CreateInstance<T>());
            item.name = name;
            var so = new SerializedObject(item);
            so.FindProperty("name").stringValue = name;
            so.FindProperty("price").floatValue = price;
            setup?.Invoke(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private ItemCategoryDatabase Database()
        {
            ItemCategoryDatabase db = Track(ItemCategoryDatabase.CreateDefault());
            foreach (ItemCategory c in db.Categories) Track(c);
            return db;
        }

        private Merchant MakeMerchant(ItemCategoryDatabase db)
        {
            var go = Track(new GameObject("Merchant"));
            Merchant m = go.AddComponent<Merchant>();
            var so = new SerializedObject(m);
            so.FindProperty("categoryDatabase").objectReferenceValue = db;
            so.ApplyModifiedPropertiesWithoutUndo();
            return m;
        }

        // ------------------------------------------------------------------ grid packing
        [Test]
        public void Packer_PlacesItemsInOrderWithoutOverlapInsideTheColumns()
        {
            var sizes = new List<Vector2Int> { new Vector2Int(1, 3), new Vector2Int(2, 2), new Vector2Int(1, 1), new Vector2Int(2, 4), new Vector2Int(1, 1) };
            var rotatable = new List<bool> { true, false, true, true, true };
            var rects = new List<GridRect>();
            var rotated = new List<bool>();
            int rows = MerchantGridPacker.Pack(sizes, rotatable, 4, true, rects, rotated);
            Assert.AreEqual(sizes.Count, rects.Count);
            for (int i = 0; i < rects.Count; i++)
            {
                Assert.GreaterOrEqual(rects[i].X, 0);
                Assert.LessOrEqual(rects[i].Right, 4, $"item {i} sticks out");
                Assert.LessOrEqual(rects[i].Bottom, rows);
                Vector2Int s = rotated[i] ? new Vector2Int(sizes[i].y, sizes[i].x) : sizes[i];
                Assert.AreEqual(s, new Vector2Int(rects[i].W, rects[i].H), $"item {i} has the wrong footprint");
                for (int j = i + 1; j < rects.Count; j++)
                    Assert.IsFalse(rects[i].Overlaps(rects[j]), $"items {i} and {j} overlap");
            }
            Assert.AreEqual(new GridRect(0, 0, 1, 3), rects[0], "the first item takes the first place");
        }

        [Test]
        public void Packer_TurnsItemsWiderThanTheGrid()
        {
            var rects = new List<GridRect>();
            var rotated = new List<bool>();
            MerchantGridPacker.Pack(new List<Vector2Int> { new Vector2Int(4, 1) }, new List<bool> { true }, 2, false, rects, rotated);
            Assert.IsTrue(rotated[0]);
            Assert.AreEqual(1, rects[0].W);
            Assert.AreEqual(4, rects[0].H);
        }

        // ------------------------------------------------------------------ categories
        [Test]
        public void Categories_SortItemsByKind()
        {
            ItemCategoryDatabase db = Database();
            WeaponSO sword = Item<WeaponSO>("Sword", 10, so => so.FindProperty("weaponCategory").enumValueIndex = (int)WeaponCategory.Sword);
            WeaponSO shield = Item<WeaponSO>("Buckler", 10, so => so.FindProperty("weaponCategory").enumValueIndex = (int)WeaponCategory.Shield);
            ArmorSO helmet = Item<ArmorSO>("Helm", 10, so => so.FindProperty("armorSlotType").enumValueIndex = (int)ArmorSlotType.Helmet);
            ArmorSO ring = Item<ArmorSO>("Ring", 10, so => so.FindProperty("armorSlotType").enumValueIndex = (int)ArmorSlotType.Ring);
            MaterialSO ore = Item<MaterialSO>("Iron Ore", 2, so => so.FindProperty("materialKind").enumValueIndex = (int)MaterialKind.Ore);
            MiscItemSO junk = Item<MiscItemSO>("Old Boot", 1);

            Assert.AreEqual("Weapons ▸ Swords", db.Resolve(sword).Path);
            Assert.AreEqual("Armor ▸ Shields", db.Resolve(shield).Path);
            Assert.AreEqual("Armor ▸ Helmets", db.Resolve(helmet).Path);
            Assert.AreEqual("Accessories ▸ Rings", db.Resolve(ring).Path);
            Assert.AreEqual("Materials ▸ Ores & Ingots", db.Resolve(ore).Path);
            Assert.AreEqual("Miscellaneous", db.Resolve(junk).Path);
        }

        [Test]
        public void Categories_TimedPotionsAreBuffsAndTheItemsOwnCategoryWins()
        {
            ItemCategoryDatabase db = Database();
            ConsumableSO heal = Item<ConsumableSO>("Healing Potion", 5, so => so.FindProperty("itemType").enumValueIndex = (int)ItemType.Potion);
            ConsumableSO elixir = Item<ConsumableSO>("Elixir", 5, so =>
            {
                so.FindProperty("itemType").enumValueIndex = (int)ItemType.Potion;
                SerializedProperty effects = so.FindProperty("effects");
                effects.arraySize = 1;
                effects.GetArrayElementAtIndex(0).FindPropertyRelative("timeBuffEffect").floatValue = 30f;
            });
            Assert.AreEqual("Consumables ▸ Potions", db.Resolve(heal).Path);
            Assert.AreEqual("Consumables ▸ Buffs", db.Resolve(elixir).Path);

            ItemCategory materials = null;
            foreach (ItemCategory c in db.Categories)
                if (c.DisplayName == "Materials") materials = c;
            var so2 = new SerializedObject(heal);
            so2.FindProperty("category").objectReferenceValue = materials;
            so2.ApplyModifiedPropertiesWithoutUndo();
            db.ClearCache();
            Assert.AreEqual(materials, db.Resolve(heal).Top);
        }

        // ------------------------------------------------------------------ prices
        [Test]
        public void Prices_FollowMultipliersAndNeverPayMoreThanTheyCharge()
        {
            Merchant m = MakeMerchant(Database());
            MiscItemSO gem = Item<MiscItemSO>("Gem", 100);
            m.Pricing.buyMultiplier = 1.5f;
            m.Pricing.sellMultiplier = 0.5f;
            Assert.AreEqual(150, m.PreviewBuyPrice(new MerchantStockEntry(gem, 1)));
            Assert.AreEqual(50, m.PreviewSellPrice(gem));

            m.Pricing.sellMultiplier = 3f; // would let the player profit
            m.Pricing.sellNeverAboveBuy = true;
            Assert.AreEqual(150, m.PreviewSellPrice(gem));

            var fixedEntry = new MerchantStockEntry(gem, 1) { fixedPrice = 7 };
            Assert.AreEqual(7, m.PreviewBuyPrice(fixedEntry));

            MiscItemSO free = Item<MiscItemSO>("Pebble", 0);
            Assert.AreEqual(m.Pricing.minimumBuyPrice, m.PreviewBuyPrice(new MerchantStockEntry(free, 1)));
        }

        [Test]
        public void Prices_WornItemsSellForLess()
        {
            Merchant m = MakeMerchant(Database());
            WeaponSO axe = Item<WeaponSO>("Axe", 100, so =>
            {
                so.FindProperty("maxDurability").intValue = 100;
                so.FindProperty("durability").intValue = 100;
            });
            m.Pricing.sellMultiplier = 0.5f;
            m.Pricing.brokenItemValue = 0f;
            var go = Track(new GameObject("Stack"));
            InventoryItem stack = go.AddComponent<InventoryItem>();
            stack.Initialize(axe, 1);
            stack.durability = 100;
            Assert.AreEqual(50, m.GetSellPrice(stack));
            stack.durability = 50;
            Assert.AreEqual(25, m.GetSellPrice(stack));
        }

        [Test]
        public void Prices_CategoryModifierChangesOnlyItsCategory()
        {
            ItemCategoryDatabase db = Database();
            Merchant m = MakeMerchant(db);
            ItemCategory materials = null;
            foreach (ItemCategory c in db.Categories)
                if (c.DisplayName == "Materials") materials = c;
            m.Pricing.modifiers.Add(new CategoryPriceModifier { category = materials, buyMultiplier = 2f, sellMultiplier = 1f });
            MaterialSO ore = Item<MaterialSO>("Ore", 10);
            MiscItemSO junk = Item<MiscItemSO>("Junk", 10);
            Assert.AreEqual(20, m.PreviewBuyPrice(new MerchantStockEntry(ore, 1)));
            Assert.AreEqual(10, m.PreviewBuyPrice(new MerchantStockEntry(junk, 1)));
        }

        // ------------------------------------------------------------------ stock and rules
        [Test]
        public void Stock_TakesRestocksAndKeepsItemsBoughtFromThePlayer()
        {
            MiscItemSO a = Item<MiscItemSO>("A", 1);
            MiscItemSO b = Item<MiscItemSO>("B", 1);
            var stock = new MerchantInventory();
            stock.Initialize(new[] { new MerchantStockEntry(a, 3) { maxQuantity = 5 }, new MerchantStockEntry(b, 1, unlimited: true) });
            MerchantStockSlot slotA = stock.Slots[0];
            Assert.AreEqual(2, stock.Take(slotA, 2));
            Assert.AreEqual(1, slotA.Quantity);
            Assert.AreEqual(1, stock.Take(slotA, 5), "only what is left is taken");
            Assert.IsFalse(slotA.InStock);
            Assert.AreEqual(10, stock.Take(stock.Slots[1], 10), "unlimited never runs out");

            stock.Restock(true);
            Assert.AreEqual(5, slotA.Quantity, "restocked to its maximum");

            MiscItemSO c = Item<MiscItemSO>("C", 1);
            stock.AddBought(c, 2, 4);
            Assert.AreEqual(3, stock.Slots.Count);
            Assert.IsTrue(stock.Slots[2].IsBuyback);
            stock.AddBought(a, 4, 4);
            Assert.AreEqual(9, slotA.Quantity, "a sold item of a stocked kind goes back to its line");
            stock.Restock(true);
            Assert.AreEqual(2, stock.Slots.Count, "sold-back items are cleared at restock");
        }

        [Test]
        public void Merchant_RefusesItemsThatCannotBeSoldOrAreNotWanted()
        {
            ItemCategoryDatabase db = Database();
            Merchant m = MakeMerchant(db);
            MiscItemSO quest = Item<MiscItemSO>("Key", 5, so =>
            {
                so.FindProperty("questItem").boolValue = true;
                so.FindProperty("canBeSold").boolValue = false;
            });
            Assert.IsFalse(m.WillBuy(quest, out string reason));
            Assert.IsNotEmpty(reason);

            ItemCategory weapons = null;
            foreach (ItemCategory c in db.Categories)
                if (c.DisplayName == "Weapons") weapons = c;
            var so2 = new SerializedObject(m);
            SerializedProperty buys = so2.FindProperty("buysCategories");
            buys.arraySize = 1;
            buys.GetArrayElementAtIndex(0).objectReferenceValue = weapons;
            so2.ApplyModifiedPropertiesWithoutUndo();
            WeaponSO sword = Item<WeaponSO>("Sword", 10, so => so.FindProperty("weaponCategory").enumValueIndex = (int)WeaponCategory.Sword);
            MaterialSO ore = Item<MaterialSO>("Ore", 10);
            Assert.IsTrue(m.WillBuy(sword, out _));
            Assert.IsFalse(m.WillBuy(ore, out _));
        }
    }
}
#endif
