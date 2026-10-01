#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Presets and automatic setup for item assets (used by the item inspectors and the Create menus):
/// <list type="bullet">
/// <item><b>Auto-Fill</b>: display name, icon and prefab found by name, armor slot / weapon category / food-or-potion
/// guessed from the name, sensible stack size and durability.</item>
/// <item><b>Icon From Prefab</b>: renders the prefab and saves it as the item's icon sprite.</item>
/// <item><b>Armor materials</b> (Cloth … Mythril), scaled by the armor slot.</item>
/// <item><b>Weapon tiers</b> (Wooden … Legendary), scaled by the weapon category.</item>
/// <item><b>Consumables</b>: health, mana, stamina and regeneration potions, food and water.</item>
/// </list>
/// Every change goes through SerializedObject, so it can be undone.
/// </summary>
public static class ItemPresets
{
    // ================================================================== auto-fill
    /// <summary>Fills what is missing or clearly wrong. Returns a description of each change (empty = nothing to do).</summary>
    public static List<string> AutoFill(ItemSO item)
    {
        var changes = new List<string>();
        if (item == null)
            return changes;
        var so = new SerializedObject(item);
        string assetName = item.name;
        string words = Words(assetName);

        SerializedProperty nameProp = so.FindProperty("name");
        if (string.IsNullOrWhiteSpace(nameProp.stringValue))
        {
            nameProp.stringValue = ObjectNames.NicifyVariableName(assetName.Replace('_', ' ').Replace('-', ' ')).Trim();
            changes.Add($"name '{nameProp.stringValue}'");
        }
        string displayName = nameProp.stringValue;

        SerializedProperty icon = so.FindProperty("icon");
        if (icon.objectReferenceValue == null)
        {
            Sprite s = FindByName<Sprite>("t:Sprite", assetName, displayName, assetName + "_Icon", assetName + "Icon", "Icon_" + assetName);
            if (s != null) { icon.objectReferenceValue = s; changes.Add($"icon '{s.name}'"); }
        }
        SerializedProperty prefab = so.FindProperty("prefab");
        if (prefab.objectReferenceValue == null)
        {
            GameObject p = FindByName<GameObject>("t:Prefab", assetName, displayName);
            if (p != null) { prefab.objectReferenceValue = p; changes.Add($"prefab '{p.name}'"); }
        }

        SerializedProperty itemType = so.FindProperty("itemType");
        SerializedProperty stackMax = so.FindProperty("stackMax");
        SerializedProperty maxDur = so.FindProperty("maxDurability");
        SerializedProperty dur = so.FindProperty("durability");

        switch (item)
        {
            case WeaponSO _:
                {
                    SetEnum(itemType, (int)ItemType.Weapon, "type Weapon", changes);
                    SerializedProperty cat = so.FindProperty("weaponCategory");
                    if (cat.enumValueIndex == (int)WeaponCategory.None && GuessCategory(words, out WeaponCategory c))
                        SetEnum(cat, (int)c, $"category {c}", changes);
                    if (stackMax.intValue != 1) { stackMax.intValue = 1; changes.Add("stack 1"); }
                    if (maxDur.intValue <= 0) { maxDur.intValue = 200; changes.Add("durability 200"); }
                    break;
                }
            case ArmorSO _:
                {
                    SerializedProperty slot = so.FindProperty("armorSlotType");
                    if (GuessArmorSlot(words, out ArmorSlotType guessed) && slot.enumValueIndex != (int)guessed)
                        SetEnum(slot, (int)guessed, $"slot {guessed}", changes);
                    if (stackMax.intValue != 1) { stackMax.intValue = 1; changes.Add("stack 1"); }
                    bool jewelry = IsJewelry((ArmorSlotType)slot.enumValueIndex);
                    if (maxDur.intValue <= 0 && !jewelry) { maxDur.intValue = 100; changes.Add("durability 100"); }
                    break;
                }
            case ConsumableSO _:
                {
                    bool food = IsFoodName(words);
                    SetEnum(itemType, (int)(food ? ItemType.Food : ItemType.Potion), food ? "type Food" : "type Potion", changes);
                    if (stackMax.intValue < 1) { stackMax.intValue = food ? 20 : 10; changes.Add($"stack {stackMax.intValue}"); }
                    break;
                }
            default:
                if (stackMax.intValue < 1) { stackMax.intValue = item is EquippableSO ? 1 : 10; changes.Add($"stack {stackMax.intValue}"); }
                break;
        }
        if (maxDur.intValue > 0 && (dur.intValue <= 0 || dur.intValue > maxDur.intValue))
        {
            dur.intValue = maxDur.intValue;
            changes.Add($"starting durability {dur.intValue}");
        }
        so.ApplyModifiedProperties();
        return changes;
    }

    private static void SetEnum(SerializedProperty p, int value, string label, List<string> changes)
    {
        if (p == null || p.enumValueIndex == value) return;
        p.enumValueIndex = value;
        changes.Add(label);
    }

    private static string Normalize(string s) => string.IsNullOrEmpty(s) ? "" : s.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");

    /// <summary>Lower-case words of a name ("IronHelmet_02" → " iron helmet 02 ").</summary>
    private static string Words(string s) => " " + ObjectNames.NicifyVariableName(s.Replace('_', ' ').Replace('-', ' ')).ToLowerInvariant() + " ";

    /// <summary>An asset whose name matches one of <paramref name="names"/> (ignoring case, spaces and underscores); exact matches first.</summary>
    private static T FindByName<T>(string filter, params string[] names) where T : UnityEngine.Object
    {
        var wanted = new List<string>();
        foreach (string n in names)
            if (!string.IsNullOrWhiteSpace(n)) wanted.Add(Normalize(n));
        T contains = null;
        foreach (string guid in AssetDatabase.FindAssets($"{filter} {names[0]}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(o is T t)) continue;
                string n = Normalize(t.name);
                if (wanted.Contains(n)) return t;
                if (contains == null && n.Contains(wanted[0])) contains = t;
            }
        }
        return contains;
    }

    private static bool Has(string words, params string[] keys)
    {
        foreach (string k in keys)
            if (words.Contains(" " + k + " ") || words.Contains(" " + k + "s ")) return true;
        return false;
    }

    public static bool GuessArmorSlot(string words, out ArmorSlotType slot)
    {
        slot = ArmorSlotType.Helmet;
        if (Has(words, "helmet", "helm", "hat", "hood", "cap", "crown", "mask")) { slot = ArmorSlotType.Helmet; return true; }
        if (Has(words, "chest", "chestplate", "armor", "armour", "cuirass", "tunic", "robe", "breastplate", "vest", "shirt")) { slot = ArmorSlotType.Chestplate; return true; }
        if (Has(words, "legging", "leg", "pant", "greave", "trouser", "skirt")) { slot = ArmorSlotType.Leggings; return true; }
        if (Has(words, "boot", "shoe", "sabaton", "sandal")) { slot = ArmorSlotType.Boots; return true; }
        if (Has(words, "glove", "gauntlet", "mitt", "hand")) { slot = ArmorSlotType.Gloves; return true; }
        if (Has(words, "shield", "buckler")) { slot = ArmorSlotType.Shield; return true; }
        if (Has(words, "ring", "band")) { slot = ArmorSlotType.Ring; return true; }
        if (Has(words, "amulet", "necklace", "pendant", "talisman")) { slot = ArmorSlotType.Amulet; return true; }
        if (Has(words, "trinket", "charm", "idol", "relic")) { slot = ArmorSlotType.Trinket; return true; }
        if (Has(words, "cloak", "cape", "mantle")) { slot = ArmorSlotType.Cloak; return true; }
        if (Has(words, "belt", "sash", "girdle")) { slot = ArmorSlotType.Belt; return true; }
        if (Has(words, "shoulder", "pauldron", "spaulder")) { slot = ArmorSlotType.Shoulders; return true; }
        if (Has(words, "bracer", "wrist", "vambrace", "bracelet")) { slot = ArmorSlotType.Bracers; return true; }
        return false;
    }

    public static bool GuessCategory(string words, out WeaponCategory c)
    {
        c = WeaponCategory.None;
        if (Has(words, "greatsword", "claymore", "zweihander")) { c = WeaponCategory.Greatsword; return true; }
        if (Has(words, "sword", "blade", "katana", "saber", "sabre", "rapier", "scimitar")) { c = WeaponCategory.Sword; return true; }
        if (Has(words, "dagger", "knife", "dirk", "stiletto")) { c = WeaponCategory.Dagger; return true; }
        if (Has(words, "axe", "hatchet", "tomahawk")) { c = WeaponCategory.Axe; return true; }
        if (Has(words, "mace", "club", "flail", "morningstar")) { c = WeaponCategory.Mace; return true; }
        if (Has(words, "hammer", "maul", "warhammer")) { c = WeaponCategory.Hammer; return true; }
        if (Has(words, "spear", "lance", "pike", "halberd", "trident", "glaive")) { c = WeaponCategory.Spear; return true; }
        if (Has(words, "crossbow")) { c = WeaponCategory.Crossbow; return true; }
        if (Has(words, "bow", "longbow", "shortbow")) { c = WeaponCategory.Bow; return true; }
        if (Has(words, "staff", "stave", "rod")) { c = WeaponCategory.Staff; return true; }
        if (Has(words, "wand", "scepter", "sceptre")) { c = WeaponCategory.Wand; return true; }
        if (Has(words, "fist", "knuckle", "claw", "cestus")) { c = WeaponCategory.Fist; return true; }
        if (Has(words, "pickaxe", "pick", "shovel", "sickle", "tool")) { c = WeaponCategory.Tool; return true; }
        return false;
    }

    private static bool IsFoodName(string words) =>
        Has(words, "food", "bread", "meat", "apple", "berry", "fish", "stew", "soup", "cheese", "cake", "pie", "mushroom", "carrot", "egg", "steak", "water", "juice", "milk", "drink", "ration");

    private static bool IsJewelry(ArmorSlotType s) => s == ArmorSlotType.Ring || s == ArmorSlotType.Amulet || s == ArmorSlotType.Trinket;

    // ================================================================== icon from prefab
    /// <summary>Renders the item's prefab (Unity's asset preview) and saves it next to the item as its icon sprite.</summary>
    public static void GenerateIcon(ItemSO item)
    {
        if (item == null || item.Prefab == null)
        {
            EditorUtility.DisplayDialog("Icon From Prefab", "Assign the item's Prefab first.", "OK");
            return;
        }
        GameObject prefab = item.Prefab;
        int frames = 0;
        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            Texture2D preview = AssetPreview.GetAssetPreview(prefab);
            if (preview == null && frames++ < 300)
                return; // the preview renders asynchronously
            EditorApplication.update -= tick;
            if (preview == null)
            {
                Debug.LogWarning($"[Items] Unity could not render a preview of '{prefab.name}' (a prefab with no visible mesh?).", item);
                return;
            }
            SaveIcon(item, preview);
        };
        EditorApplication.update += tick;
    }

    private static void SaveIcon(ItemSO item, Texture2D preview)
    {
        // Copy through a RenderTexture: preview textures are not readable.
        var rt = RenderTexture.GetTemporary(preview.width, preview.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(preview, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, preview.width, preview.height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        string itemPath = AssetDatabase.GetAssetPath(item);
        string dir = string.IsNullOrEmpty(itemPath) ? "Assets" : Path.GetDirectoryName(itemPath).Replace('\\', '/');
        string path = $"{dir}/{item.name}_Icon.png";
        File.WriteAllBytes(path, copy.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(copy);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        var so = new SerializedObject(item);
        so.FindProperty("icon").objectReferenceValue = sprite;
        so.ApplyModifiedProperties();
        EditorGUIUtility.PingObject(sprite);
        Debug.Log($"[Items] Saved the icon of '{item.name}' to {path}.", item);
    }

    // ================================================================== armor materials
    public enum ArmorMaterial { Cloth, Leather, Chainmail, Iron, Steel, Mythril }

    // Values of a full chest piece; other slots are scaled.
    private static readonly (float def, float magic, float weight, int durability, float price)[] ArmorBase =
    {
        (3f, 6f, 2f, 60, 10f),       // Cloth: light, good against magic
        (6f, 3f, 4f, 100, 25f),      // Leather
        (10f, 2f, 8f, 160, 60f),     // Chainmail
        (14f, 2f, 12f, 220, 100f),   // Iron
        (20f, 3f, 14f, 300, 180f),   // Steel
        (28f, 12f, 9f, 450, 450f),   // Mythril: strong and light
    };

    private static float SlotFactor(ArmorSlotType s)
    {
        switch (s)
        {
            case ArmorSlotType.Chestplate: return 1f;
            case ArmorSlotType.Shield: return 0.9f;
            case ArmorSlotType.Leggings: return 0.75f;
            case ArmorSlotType.Helmet: return 0.5f;
            case ArmorSlotType.Shoulders: return 0.4f;
            case ArmorSlotType.Boots: return 0.35f;
            case ArmorSlotType.Gloves: return 0.3f;
            case ArmorSlotType.Cloak: return 0.3f;
            case ArmorSlotType.Bracers: return 0.25f;
            case ArmorSlotType.Belt: return 0.2f;
            default: return 0.1f; // jewelry
        }
    }

    /// <summary>Sets Defense, Magic Resistance, weight, durability and price for the material, scaled by the armor's slot.</summary>
    public static void ApplyArmor(ArmorSO armor, ArmorMaterial material)
    {
        var so = new SerializedObject(armor);
        var slot = (ArmorSlotType)so.FindProperty("armorSlotType").enumValueIndex;
        var b = ArmorBase[(int)material];
        float f = SlotFactor(slot);
        bool jewelry = IsJewelry(slot);
        so.FindProperty("defenseValue").floatValue = jewelry ? 0f : Mathf.Round(b.def * f * 10f) / 10f;
        // Jewelry: no defense, half of the material's magic resistance (it is where magic protection comes from).
        so.FindProperty("magicDefenseValue").floatValue = Mathf.Round(b.magic * (jewelry ? 0.5f : f) * 10f) / 10f;
        so.FindProperty("weight").floatValue = jewelry ? 0.1f : Mathf.Round(b.weight * f * 10f) / 10f;
        int durability = jewelry ? 0 : Mathf.Max(10, Mathf.RoundToInt(b.durability * Mathf.Lerp(0.6f, 1f, f)));
        so.FindProperty("maxDurability").intValue = durability;
        so.FindProperty("durability").intValue = durability;
        so.FindProperty("price").floatValue = Mathf.Round(b.price * (jewelry ? 1.5f : f));
        so.FindProperty("stackMax").intValue = 1;
        so.FindProperty("itemType").enumValueIndex = (int)ItemType.Armor;
        SerializedProperty n = so.FindProperty("name");
        if (string.IsNullOrWhiteSpace(n.stringValue))
            n.stringValue = $"{material} {SlotTypeHelper.GetDisplayName(slot)}";
        so.ApplyModifiedProperties();
    }

    // ================================================================== weapon tiers
    public enum WeaponTier { Wooden, Stone, Iron, Steel, Mythril, Legendary }

    private static readonly float[] TierDamage = { 0.6f, 0.8f, 1f, 1.3f, 1.7f, 2.3f };
    private static readonly int[] TierDurability = { 80, 120, 200, 300, 450, 800 };
    private static readonly float[] TierPrice = { 10f, 20f, 50f, 120f, 300f, 1000f };
    private static readonly float[] TierCrit = { 0f, 0f, 0f, 0.02f, 0.04f, 0.08f };
    private static readonly float[] TierWeight = { 0.7f, 1.2f, 1f, 1f, 0.7f, 0.8f };

    private static (float min, float max, float crit, float weight) CategoryBase(WeaponCategory c)
    {
        switch (c)
        {
            case WeaponCategory.Sword: return (10f, 14f, 0.08f, 3f);
            case WeaponCategory.Greatsword: return (18f, 26f, 0.05f, 7f);
            case WeaponCategory.Dagger: return (6f, 9f, 0.18f, 1f);
            case WeaponCategory.Spear: return (11f, 15f, 0.07f, 4f);
            case WeaponCategory.Axe: return (12f, 17f, 0.1f, 4f);
            case WeaponCategory.Mace:
            case WeaponCategory.Hammer: return (16f, 22f, 0.05f, 6f);
            case WeaponCategory.Bow:
            case WeaponCategory.Crossbow: return (12f, 16f, 0.1f, 2f);
            case WeaponCategory.Staff:
            case WeaponCategory.Wand: return (8f, 11f, 0.05f, 3f);
            case WeaponCategory.Fist: return (5f, 8f, 0.12f, 1f);
            case WeaponCategory.Tool: return (4f, 6f, 0.02f, 3f);
            default: return (8f, 12f, 0.07f, 3f);
        }
    }

    /// <summary>Sets damage, critical chance, weight, durability and price for the tier, based on the weapon's category.</summary>
    public static void ApplyWeaponTier(WeaponSO weapon, WeaponTier tier)
    {
        var so = new SerializedObject(weapon);
        var category = (WeaponCategory)so.FindProperty("weaponCategory").enumValueIndex;
        var b = CategoryBase(category);
        int t = (int)tier;
        so.FindProperty("minDamage").floatValue = Mathf.Round(b.min * TierDamage[t]);
        so.FindProperty("maxDamage").floatValue = Mathf.Round(b.max * TierDamage[t]);
        so.FindProperty("criticalChance").floatValue = Mathf.Clamp01(b.crit + TierCrit[t]);
        so.FindProperty("weight").floatValue = Mathf.Round(b.weight * TierWeight[t] * 10f) / 10f;
        so.FindProperty("maxDurability").intValue = TierDurability[t];
        so.FindProperty("durability").intValue = TierDurability[t];
        so.FindProperty("price").floatValue = TierPrice[t];
        so.FindProperty("stackMax").intValue = 1;
        SerializedProperty n = so.FindProperty("name");
        if (string.IsNullOrWhiteSpace(n.stringValue) && category != WeaponCategory.None)
            n.stringValue = $"{tier} {ObjectNames.NicifyVariableName(category.ToString())}";
        so.ApplyModifiedProperties();
    }

    // ================================================================== consumables
    public enum Consumable
    {
        MinorHealthPotion, HealthPotion, GreaterHealthPotion, RegenerationPotion, ManaPotion, StaminaPotion,
        Bread, CookedMeat, Apple, WaterBottle,
    }

    private struct Fx
    {
        public ConsumableEffectType type; public float amount; public float duration; public float tick; public string name;
        public Fx(ConsumableEffectType type, float amount, float duration = 0f, float tick = 0f, string name = null)
        { this.type = type; this.amount = amount; this.duration = duration; this.tick = tick; this.name = name; }
    }

    private static (string name, string description, bool food, int stack, float weight, float price, float cooldown, Fx[] effects) ConsumableData(Consumable c)
    {
        switch (c)
        {
            case Consumable.MinorHealthPotion: return ("Minor Health Potion", "Restores 25 health.", false, 10, 0.2f, 10f, 1f, new[] { new Fx(ConsumableEffectType.Hp, 25f) });
            case Consumable.HealthPotion: return ("Health Potion", "Restores 50 health.", false, 10, 0.3f, 25f, 1f, new[] { new Fx(ConsumableEffectType.Hp, 50f) });
            case Consumable.GreaterHealthPotion: return ("Greater Health Potion", "Restores 100 health.", false, 10, 0.4f, 60f, 1f, new[] { new Fx(ConsumableEffectType.Hp, 100f) });
            case Consumable.RegenerationPotion: return ("Regeneration Potion", "Restores 60 health over 10 seconds.", false, 10, 0.3f, 40f, 1f, new[] { new Fx(ConsumableEffectType.Hp, 60f, 10f, 1f, "Regeneration") });
            case Consumable.ManaPotion: return ("Mana Potion", "Restores 50 mana.", false, 10, 0.3f, 25f, 1f, new[] { new Fx(ConsumableEffectType.Mana, 50f) });
            case Consumable.StaminaPotion: return ("Stamina Potion", "Restores 60 stamina.", false, 10, 0.3f, 20f, 1f, new[] { new Fx(ConsumableEffectType.Stamina, 60f) });
            case Consumable.Bread: return ("Bread", "Simple, filling food.", true, 20, 0.3f, 3f, 0.5f, new[] { new Fx(ConsumableEffectType.Food, 25f) });
            case Consumable.CookedMeat:
                return ("Cooked Meat", "Very filling and restores a little health over time.", true, 20, 0.5f, 8f, 0.5f,
                new[] { new Fx(ConsumableEffectType.Food, 40f), new Fx(ConsumableEffectType.Hp, 10f, 5f, 1f, "Well Fed") });
            case Consumable.Apple:
                return ("Apple", "A little food and water.", true, 20, 0.1f, 1f, 0.5f,
                new[] { new Fx(ConsumableEffectType.Food, 10f), new Fx(ConsumableEffectType.Drink, 5f) });
            default: return ("Water Bottle", "Quenches thirst.", true, 10, 0.5f, 2f, 0.5f, new[] { new Fx(ConsumableEffectType.Drink, 40f) });
        }
    }

    /// <summary>Sets the consumable's effects, type, stack, weight, price and cooldown (and name/description when empty).</summary>
    public static void ApplyConsumable(ConsumableSO item, Consumable preset)
    {
        var d = ConsumableData(preset);
        var so = new SerializedObject(item);
        SerializedProperty n = so.FindProperty("name");
        if (string.IsNullOrWhiteSpace(n.stringValue)) n.stringValue = d.name;
        SerializedProperty desc = so.FindProperty("description");
        if (string.IsNullOrWhiteSpace(desc.stringValue)) desc.stringValue = d.description;
        so.FindProperty("itemType").enumValueIndex = (int)(d.food ? ItemType.Food : ItemType.Potion);
        so.FindProperty("stackMax").intValue = d.stack;
        so.FindProperty("weight").floatValue = d.weight;
        so.FindProperty("price").floatValue = d.price;
        so.FindProperty("cooldown").floatValue = d.cooldown;
        so.FindProperty("maxDurability").intValue = 0;
        so.FindProperty("durability").intValue = 1;

        SerializedProperty effects = so.FindProperty("effects");
        effects.ClearArray();
        foreach (Fx fx in d.effects)
        {
            effects.InsertArrayElementAtIndex(effects.arraySize);
            SerializedProperty e = effects.GetArrayElementAtIndex(effects.arraySize - 1);
            e.FindPropertyRelative("effectType").enumValueIndex = (int)fx.type;
            e.FindPropertyRelative("itemType").enumValueIndex = (int)(d.food ? ConsumableType.Food : ConsumableType.Potion);
            e.FindPropertyRelative("effectName").stringValue = fx.name ?? $"{d.name} {fx.type}";
            e.FindPropertyRelative("amount").floatValue = fx.amount;
            e.FindPropertyRelative("timeBuffEffect").floatValue = fx.duration;   // 0 = instant
            e.FindPropertyRelative("tickCooldown").floatValue = fx.tick;
            e.FindPropertyRelative("isProcedural").boolValue = fx.duration > 0f; // spread the amount over the duration
            e.FindPropertyRelative("isStackable").boolValue = false;
            e.FindPropertyRelative("randomAmount").boolValue = false;
            e.FindPropertyRelative("randomTimeBuffEffect").boolValue = false;
            e.FindPropertyRelative("randomTickCooldown").boolValue = false;
            e.FindPropertyRelative("applicationChance").floatValue = 1f;
            e.FindPropertyRelative("minimumLevel").intValue = 1;
        }
        so.ApplyModifiedProperties();
    }

    // ================================================================== create menus
    private const string ConsumableMenu = "Assets/Create/Scriptable Objects/Item/Consumable Preset/";
    [MenuItem(ConsumableMenu + "Minor Health Potion", priority = 120)] private static void CMinor() => CreateConsumable(Consumable.MinorHealthPotion);
    [MenuItem(ConsumableMenu + "Health Potion", priority = 121)] private static void CHealth() => CreateConsumable(Consumable.HealthPotion);
    [MenuItem(ConsumableMenu + "Greater Health Potion", priority = 122)] private static void CGreater() => CreateConsumable(Consumable.GreaterHealthPotion);
    [MenuItem(ConsumableMenu + "Regeneration Potion", priority = 123)] private static void CRegen() => CreateConsumable(Consumable.RegenerationPotion);
    [MenuItem(ConsumableMenu + "Mana Potion", priority = 124)] private static void CMana() => CreateConsumable(Consumable.ManaPotion);
    [MenuItem(ConsumableMenu + "Stamina Potion", priority = 125)] private static void CStamina() => CreateConsumable(Consumable.StaminaPotion);
    [MenuItem(ConsumableMenu + "Bread", priority = 140)] private static void CBread() => CreateConsumable(Consumable.Bread);
    [MenuItem(ConsumableMenu + "Cooked Meat", priority = 141)] private static void CMeat() => CreateConsumable(Consumable.CookedMeat);
    [MenuItem(ConsumableMenu + "Apple", priority = 142)] private static void CApple() => CreateConsumable(Consumable.Apple);
    [MenuItem(ConsumableMenu + "Water Bottle", priority = 143)] private static void CWater() => CreateConsumable(Consumable.WaterBottle);

    private static void CreateConsumable(Consumable preset)
    {
        var item = ScriptableObject.CreateInstance<ConsumableSO>();
        string fileName = ObjectNames.NicifyVariableName(preset.ToString());
        CreateAsset(item, fileName);
        ApplyConsumable(item, preset);
        AutoFill(item);
    }

    /// <summary>Saves <paramref name="asset"/> in the folder selected in the Project window and selects it.</summary>
    public static void CreateAsset(ScriptableObject asset, string fileName)
    {
        string folder = "Assets";
        if (Selection.activeObject != null)
        {
            string p = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrEmpty(p))
                folder = AssetDatabase.IsValidFolder(p) ? p : Path.GetDirectoryName(p).Replace('\\', '/');
        }
        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset"));
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
#endif
