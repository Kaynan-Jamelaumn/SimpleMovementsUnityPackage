#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools ▸ SimpleMovements ▸ Inventory ▸ Generate Starter Item Library: a ready-to-use set of items made from the weapon
/// templates and the item presets, so a project has something to equip on day one - swords, greatswords, axes, maces
/// and hammers, spears, daggers, staves, knuckles, bows, a crossbow and throwing knives in tiers from wooden to
/// legendary; six armour sets with bonuses (cloth to mythril); shields, rings, amulets, cloaks, belts, bracers; potions,
/// food, ammo and medical items. Everything is data: the models, icons, animations and sounds are yours to assign.
/// Running it again only adds what is missing (existing assets are never overwritten).
/// </summary>
public static class StarterItemLibrary
{
    private const string Sub = "Starter";

    /// <summary>What was made.</summary>
    public sealed class Report
    {
        public int created;
        public int skipped;
        public readonly List<ItemSO> items = new List<ItemSO>();
        public readonly List<string> lines = new List<string>();
    }

    [MenuItem(SMMenu.Root + "Inventory/Generate Starter Item Library", false, SMMenu.Inventory + 22)]
    private static void GenerateMenu()
    {
        if (!EditorUtility.DisplayDialog("Starter Item Library",
                "Creates about 125 weapons with full movesets (hit volumes, chains, charged attacks, slams, effects), 19 armour sets " +
                "(95 pieces) with set bonuses, about 50 shields and accessories, about 45 potions, foods and drinks, 17 kinds of ammo " +
                "and the medical items in Resources/SimpleMovements/Items/.../Starter.\n\nItems that already exist are kept as they are.",
                "Generate", "Cancel"))
            return;
        Report r = Generate();
        Debug.Log($"[Items] Starter library: {r.created} asset(s) created, {r.skipped} already there.\n" + string.Join("\n", r.lines));
        EditorUtility.DisplayDialog("Starter Item Library", $"{r.created} asset(s) created, {r.skipped} already there.\n\n" +
                                                           "Assign each item's Prefab (model) and Icon, and the attacks' animations.", "OK");
    }

    /// <summary>Creates every missing item of the library. Returns what was done.</summary>
    public static Report Generate(bool weapons = true, bool armor = true, bool consumables = true)
    {
        var r = new Report();
        if (weapons) Weapons(r);
        if (armor)
        {
            ArmorSets(r);
            Accessories(r);
        }
        if (consumables) Consumables(r);
        AssetDatabase.SaveAssets();
        InjuryEditorTools.RegisterInItemDatabase(r.items);
        if (consumables && InjurySettings.IsEnabled)
            InjuryEditorTools.CreateMedicalItems(false);
        AssetDatabase.SaveAssets();
        string folder = SMAssetsEditor.Folder($"{SMAssets.Weapons}/{Sub}");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folder));
        return r;
    }

    // ================================================================== weapons
    private struct W
    {
        public string name;
        public StarterMovesets.Kind kind;
        public ItemPresets.WeaponTier tier;
        public string description;
        public ElementType element;
        public DamageForm form;
        public WeaponCategory category; // None = the moveset's
        public Action<SerializedObject> extra;

        public W(string name, StarterMovesets.Kind kind, ItemPresets.WeaponTier tier, string description)
        {
            this.name = name;
            this.kind = kind;
            this.tier = tier;
            this.description = description;
            element = ElementType.None;
            form = DamageForm.None;
            category = WeaponCategory.None;
            extra = null;
        }
    }

    private static List<(string folder, W[] items)> WeaponList()
    {
        const StarterMovesets.Kind Sword = StarterMovesets.Kind.Sword, Rapier = StarterMovesets.Kind.Rapier, Katana = StarterMovesets.Kind.Katana,
            Scimitar = StarterMovesets.Kind.Scimitar, Long = StarterMovesets.Kind.Longsword, Great = StarterMovesets.Kind.Greatsword,
            Axe = StarterMovesets.Kind.Axe, Greataxe = StarterMovesets.Kind.Greataxe, Mace = StarterMovesets.Kind.Mace, Flail = StarterMovesets.Kind.Flail,
            Hammer = StarterMovesets.Kind.Warhammer, Maul = StarterMovesets.Kind.Maul, Spear = StarterMovesets.Kind.Spear, Halberd = StarterMovesets.Kind.Halberd,
            Scythe = StarterMovesets.Kind.Scythe, Whip = StarterMovesets.Kind.Whip, Dagger = StarterMovesets.Kind.Dagger, Fist = StarterMovesets.Kind.Knuckles,
            Claws = StarterMovesets.Kind.Claws, BStaff = StarterMovesets.Kind.BattleStaff, MStaff = StarterMovesets.Kind.MageStaff, Wand = StarterMovesets.Kind.Wand;
        const ItemPresets.WeaponTier Wood = ItemPresets.WeaponTier.Wooden, Stone = ItemPresets.WeaponTier.Stone, Iron = ItemPresets.WeaponTier.Iron,
            Steel = ItemPresets.WeaponTier.Steel, Mythril = ItemPresets.WeaponTier.Mythril, Legend = ItemPresets.WeaponTier.Legendary;
        return new List<(string, W[])>
        {
            ("Swords", new[]
            {
                new W("Wooden Sword", Sword, Wood, "A practice blade. It bruises more than it cuts.") { form = DamageForm.Blunt },
                new W("Bronze Shortsword", Sword, Stone, "An old bronze blade, short and stout."),
                new W("Iron Sword", Sword, Iron, "A dependable soldier's sword."),
                new W("Steel Longsword", Sword, Steel, "Well balanced and keen."),
                new W("Steel Arming Sword", Sword, Steel, "The knight's sidearm, made to pair with a shield."),
                new W("Mythril Sabre", Scimitar, Mythril, "Light as a feather; finds the gaps in armour."),
                new W("Runeblade", Sword, Legend, "Runes flare along the fuller with every strike.") { element = ElementType.Lightning },
                new W("Dawnbreaker", Sword, Legend, "A blade that burns with the first light.") { element = ElementType.Fire },
            }),
            ("Rapiers", new[]
            {
                new W("Iron Rapier", Rapier, Iron, "A long, thin blade for thrusting."),
                new W("Steel Rapier", Rapier, Steel, "A duelist's blade: fast thrusts, fatal points."),
                new W("Duelist's Epee", Rapier, Steel, "Made for one thing: the first touch.") { extra = so => so.FindProperty("criticalChance").floatValue = 0.3f },
                new W("Mythril Estoc", Rapier, Mythril, "A stiff mythril point that slides through mail."),
                new W("Serpent's Tongue", Rapier, Legend, "Its point drips venom.") { element = ElementType.Poison },
            }),
            ("Katanas", new[]
            {
                new W("Iron Katana", Katana, Iron, "A curved, single-edged blade."),
                new W("Steel Katana", Katana, Steel, "Folded steel with a razor edge."),
                new W("Mythril Nodachi", Katana, Mythril, "A long field katana of mythril."),
                new W("Moonlight Katana", Katana, Legend, "It glows with a cold, holy light.") { element = ElementType.Holy },
            }),
            ("Scimitars", new[]
            {
                new W("Bronze Scimitar", Scimitar, Stone, "A curved bronze blade."),
                new W("Steel Scimitar", Scimitar, Steel, "Wide, sweeping, and sharp."),
                new W("Desert Wind", Scimitar, Mythril, "Its cuts arrive with a gust.") { element = ElementType.Wind },
                new W("Sunfire Scimitar", Scimitar, Legend, "Forged in the heart of the desert sun.") { element = ElementType.Fire },
            }),
            ("Longswords", new[]
            {
                new W("Iron Bastard Sword", Long, Iron, "A hand-and-a-half sword."),
                new W("Knight's Longsword", Long, Steel, "The classic two-handed knightly sword."),
                new W("Mythril Longsword", Long, Mythril, "Light enough for one hand, deadly in two."),
                new W("Oathkeeper", Long, Legend, "Holy runes for those who keep their word.") { element = ElementType.Holy },
            }),
            ("Greatswords", new[]
            {
                new W("Wooden Training Greatsword", Great, Wood, "Heavy oak for drills.") { form = DamageForm.Blunt },
                new W("Iron Greatsword", Great, Iron, "Heavy and two-handed: every swing clears the space around you."),
                new W("Steel Claymore", Great, Steel, "A highland blade as tall as its bearer."),
                new W("Mythril Zweihander", Great, Mythril, "Huge, but strangely light."),
                new W("Doomblade", Great, Legend, "Darkness clings to its edge.") { element = ElementType.Dark },
            }),
            ("Axes", new[]
            {
                new W("Stone Hatchet", Axe, Stone, "A sharpened stone on a stick. It still bites."),
                new W("Iron Axe", Axe, Iron, "Fells trees and foes alike."),
                new W("Bearded Axe", Axe, Iron, "Its long beard hooks shields and legs."),
                new W("Steel Battleaxe", Axe, Steel, "Made for war, not for wood."),
                new W("Mythril War Axe", Axe, Mythril, "Cuts through shields."),
                new W("Frostbite Cleaver", Axe, Legend, "Its wounds freeze over.") { element = ElementType.Ice },
            }),
            ("Greataxes", new[]
            {
                new W("Iron Greataxe", Greataxe, Iron, "Two hands, one swing, no more enemy."),
                new W("Executioner's Axe", Greataxe, Steel, "Made for one clean blow."),
                new W("Mythril Bardiche", Greataxe, Mythril, "A crescent blade on a long haft."),
                new W("Bloodreaver", Greataxe, Legend, "It thirsts.") { element = ElementType.Dark },
            }),
            ("Maces and Hammers", new[]
            {
                new W("Wooden Club", Mace, Wood, "A heavy branch. Crude, but bones still break."),
                new W("Iron Mace", Mace, Iron, "Flanged iron that dents armour and cracks bone."),
                new W("Morning Star", Mace, Steel, "A spiked ball on a haft: blunt and piercing at once.") { form = DamageForm.Pierce },
                new W("Steel Warhammer", Hammer, Steel, "A crushing blow in every swing."),
                new W("Mythril Maul", Maul, Mythril, "Impossibly heavy-hitting for its weight."),
                new W("Earthshaker", Hammer, Legend, "The ground trembles where it lands.") { element = ElementType.Earth, form = DamageForm.Crush },
            }),
            ("Flails", new[]
            {
                new W("Iron Flail", Flail, Iron, "A ball and chain that goes around shields."),
                new W("Steel Morning Flail", Flail, Steel, "A spiked ball on a steel chain."),
                new W("Thunder Flail", Flail, Mythril, "Each hit crackles.") { element = ElementType.Lightning },
            }),
            ("Mauls", new[]
            {
                new W("Stone Maul", Maul, Stone, "A boulder on a log."),
                new W("Iron Sledgehammer", Maul, Iron, "Breaks walls, gates and men."),
                new W("Titan's Maul", Maul, Legend, "Only giants should lift it.") { element = ElementType.Earth },
            }),
            ("Spears", new[]
            {
                new W("Wooden Spear", Spear, Wood, "A fire-hardened point. Keeps foes at a distance."),
                new W("Iron Spear", Spear, Iron, "Long reach, deep thrusts."),
                new W("Steel Pike", Spear, Steel, "Longer still: hold the line."),
                new W("Mythril Trident", Spear, Mythril, "Three points, blessed by the sea.") { element = ElementType.Water },
                new W("Stormpiercer", Spear, Legend, "Lightning follows its point.") { element = ElementType.Lightning },
            }),
            ("Polearms", new[]
            {
                new W("Steel Halberd", Halberd, Steel, "An axe blade, a spike and a hook on a pole."),
                new W("Iron Glaive", Halberd, Iron, "A long blade on a longer pole."),
                new W("Mythril Poleaxe", Halberd, Mythril, "Knights' favourite for breaking plate."),
                new W("Dragon's Halberd", Halberd, Legend, "Its axe glows like a forge.") { element = ElementType.Fire },
            }),
            ("Scythes", new[]
            {
                new W("Iron Scythe", Scythe, Iron, "A farm tool turned weapon."),
                new W("Steel War Scythe", Scythe, Steel, "The blade straightened for battle."),
                new W("Reaper's Scythe", Scythe, Legend, "Death's own.") { element = ElementType.Dark },
            }),
            ("Whips", new[]
            {
                new W("Leather Whip", Whip, Wood, "Stings, snares, disarms."),
                new W("Chain Whip", Whip, Iron, "A whip of linked iron."),
                new W("Thornlash", Whip, Mythril, "Barbed with poisoned thorns.") { element = ElementType.Poison },
                new W("Lightning Lash", Whip, Legend, "Cracks like thunder.") { element = ElementType.Lightning },
            }),
            ("Daggers", new[]
            {
                new W("Stone Knife", Dagger, Stone, "A flint blade, quick and nasty."),
                new W("Bone Shiv", Dagger, Stone, "A sharpened bone.") { form = DamageForm.Pierce },
                new W("Iron Dagger", Dagger, Iron, "Small, fast, and easy to hide."),
                new W("Steel Stiletto", Dagger, Steel, "A needle of a blade that slips between the ribs."),
                new W("Assassin's Blade", Dagger, Steel, "Made to strike once, unseen.") { extra = so => so.FindProperty("criticalChance").floatValue = 0.35f },
                new W("Mythril Kris", Dagger, Mythril, "Its wavy edge drips with venom.") { element = ElementType.Poison },
                new W("Shadowfang", Dagger, Legend, "Strikes from the dark.") { element = ElementType.Dark },
                new W("Frostfang", Dagger, Legend, "Its cuts frost over.") { element = ElementType.Ice },
            }),
            ("Knuckles", new[]
            {
                new W("Leather Hand Wraps", Fist, Wood, "Protect the knuckles. Your fists do the rest."),
                new W("Brass Knuckles", Fist, Stone, "A tavern brawler's best friend."),
                new W("Iron Knuckles", Fist, Iron, "Knuckles of iron."),
                new W("Steel Cestus", Fist, Steel, "Studded steel gloves of the arena."),
                new W("Spiked Gauntlets", Fist, Steel, "Every punch leaves holes.") { form = DamageForm.Pierce },
                new W("Bear Claws", Claws, Steel, "Hooked steel claws strapped to the hand."),
                new W("Mythril Claws", Claws, Mythril, "Three blades over the knuckles."),
                new W("Monk's Wraps", Fist, Iron, "Barely a weapon: the hits come from your own strength (Damage Roll: Stats).")
                {
                    extra = so =>
                    {
                        so.FindProperty("damageRoll").enumValueIndex = (int)WeaponDamageRoll.Stats;
                        so.FindProperty("minDamage").floatValue = 3f;
                        so.FindProperty("damagePerPoint").floatValue = 0.8f;
                        so.FindProperty("damageVariance").floatValue = 0.1f;
                    },
                },
                new W("Tiger Claws", Claws, Legend, "Swift as the beast.") { element = ElementType.Wind },
                new W("Titan Fists", Fist, Legend, "Gauntlets that hit like falling rocks.") { element = ElementType.Earth, form = DamageForm.Crush },
            }),
            ("Staves", new[]
            {
                new W("Oak Quarterstaff", BStaff, Wood, "A traveller's staff and a fighter's weapon."),
                new W("Iron-Shod Staff", BStaff, Iron, "Capped with iron: a fine quarterstaff."),
                new W("Monk's Staff", BStaff, Steel, "Spins faster than the eye can follow."),
                new W("Apprentice Staff", MStaff, Wood, "A student's focus: fires mana bolts."),
                new W("Frost Staff", MStaff, Steel, "Its bolts chill to the bone.") { element = ElementType.Ice },
                new W("Druid's Staff", MStaff, Steel, "Living wood that hurls stones of power.") { element = ElementType.Earth },
                new W("Staff of Storms", MStaff, Mythril, "Crackles with lightning.") { element = ElementType.Lightning },
                new W("Necromancer's Staff", MStaff, Mythril, "Bone and shadow.") { element = ElementType.Dark },
                new W("Staff of Light", MStaff, Legend, "Burns the wicked.") { element = ElementType.Holy },
                new W("Staff of Embers", MStaff, Legend, "An ember that never goes out.") { element = ElementType.Fire },
            }),
            ("Wands", new[]
            {
                new W("Willow Wand", Wand, Wood, "Fires little sparks of mana."),
                new W("Wand of Sparks", Wand, Iron, "Quick crackling bolts.") { element = ElementType.Lightning },
                new W("Wand of Frost", Wand, Steel, "Shards of ice.") { element = ElementType.Ice },
                new W("Wand of Embers", Wand, Steel, "Little fireballs.") { element = ElementType.Fire },
                new W("Venom Wand", Wand, Mythril, "Spits poison.") { element = ElementType.Poison },
                new W("Wand of the Void", Wand, Legend, "Darkness made into bolts.") { element = ElementType.Dark },
            }),
            ("Bows", new[]
            {
                new W("Wooden Shortbow", StarterMovesets.Kind.Shortbow, Wood, "A simple hunting bow. Fires Arrows."),
                new W("Hunting Bow", StarterMovesets.Kind.Shortbow, Iron, "Quick to draw for running game. Fires Arrows."),
                new W("Recurve Bow", StarterMovesets.Kind.Shortbow, Steel, "Short but powerful. Fires Arrows."),
                new W("Steel Longbow", StarterMovesets.Kind.Longbow, Steel, "Long draw, long range. Fires Arrows."),
                new W("Elven Longbow", StarterMovesets.Kind.Longbow, Mythril, "Silent and true. Fires Arrows."),
                new W("Dragonbone Bow", StarterMovesets.Kind.Longbow, Legend, "Its arrows catch fire.") { element = ElementType.Fire },
            }),
            ("Crossbows", new[]
            {
                new W("Iron Crossbow", StarterMovesets.Kind.Crossbow, Iron, "Slow to reload, punches through armour. Fires Bolts."),
                new W("Light Crossbow", StarterMovesets.Kind.Crossbow, Stone, "Small and quick to load. Fires Bolts."),
                new W("Steel Arbalest", StarterMovesets.Kind.HeavyCrossbow, Steel, "A siege crossbow: its bolts go through several foes."),
                new W("Repeating Crossbow", StarterMovesets.Kind.RepeatingCrossbow, Steel, "Six bolts, one after another. Fires Bolts."),
                new W("Mythril Siege Crossbow", StarterMovesets.Kind.HeavyCrossbow, Mythril, "Nothing stops its bolts."),
            }),
            ("Slings and Blowguns", new[]
            {
                new W("Leather Sling", StarterMovesets.Kind.Sling, Wood, "Hurls Stones."),
                new W("Shepherd's Sling", StarterMovesets.Kind.Sling, Iron, "Hurls Stones with deadly aim."),
                new W("Bamboo Blowgun", StarterMovesets.Kind.Blowgun, Wood, "Fires poisoned Darts."),
                new W("Assassin's Blowgun", StarterMovesets.Kind.Blowgun, Steel, "Silent death. Fires Darts."),
            }),
            ("Thrown", new[]
            {
                new W("Throwing Knives", StarterMovesets.Kind.ThrowingKnife, Iron, "A stack of balanced knives, thrown one by one."),
                new W("Iron Javelins", StarterMovesets.Kind.Javelin, Iron, "Heavy throwing spears."),
                new W("Steel Throwing Axes", StarterMovesets.Kind.ThrowingAxe, Steel, "They spin, they bite."),
                new W("Shuriken", StarterMovesets.Kind.Shuriken, Steel, "Throwing stars, flicked in quick succession."),
                new W("Mythril Chakram", StarterMovesets.Kind.Shuriken, Mythril, "A razor ring.") { form = DamageForm.Slash },
            }),
            ("Firearms", new[]
            {
                new W("Flintlock Pistol", StarterMovesets.Kind.Pistol, Iron, "Six shots before reloading. Fires Bullets."),
                new W("Duelling Pistol", StarterMovesets.Kind.Pistol, Steel, "Precise and deadly. Fires Bullets."),
                new W("Musket", StarterMovesets.Kind.Musket, Iron, "One shot, then a long reload. Fires Bullets."),
                new W("Blunderbuss", StarterMovesets.Kind.Blunderbuss, Iron, "A spray of shot at close range. Fires Shells."),
                new W("Hand Cannon", StarterMovesets.Kind.HandCannon, Steel, "Its shot explodes. Fires Iron Shot."),
            }),
        };
    }

    /// <summary>Re-applies the moveset of every starter weapon that already exists (updates older libraries).</summary>
    [MenuItem(SMMenu.Root + "Inventory/Rebuild Starter Weapon Movesets", false, SMMenu.Inventory + 23)]
    private static void RebuildMenu()
    {
        if (!EditorUtility.DisplayDialog("Starter weapons", "Re-apply the moveset, hit volumes and tier of every starter weapon that already exists? " +
                                                           "Changes you made to their attacks and stats are replaced (models, icons and animations are kept " +
                                                           "only where the moveset does not replace the attack).", "Rebuild", "Cancel"))
            return;
        var r = new Report();
        Weapons(r, rebuild: true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Items] Rebuilt {r.created} starter weapon moveset(s).");
    }

    private static void Weapons(Report r, bool rebuild = false)
    {
        foreach ((string folderName, W[] list) in WeaponList())
        {
            string folder = SMAssetsEditor.Folder($"{SMAssets.Weapons}/{Sub}/{folderName}");
            foreach (W w in list)
            {
                string path = $"{folder}/{SMAssetsEditor.CleanFileName(w.name)}.asset";
                WeaponSO weapon = AssetDatabase.LoadAssetAtPath<WeaponSO>(path);
                if (weapon != null && !rebuild)
                {
                    r.skipped++;
                    continue;
                }
                if (weapon == null && rebuild)
                    continue;
                if (weapon == null)
                {
                    weapon = ScriptableObject.CreateInstance<WeaponSO>();
                    AssetDatabase.CreateAsset(weapon, path);
                }
                var so = new SerializedObject(weapon);
                so.FindProperty("name").stringValue = w.name;
                so.FindProperty("description").stringValue = w.description;
                so.FindProperty("itemType").enumValueIndex = (int)ItemType.Weapon;
                so.FindProperty("stackMax").intValue = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                StarterMovesets.Apply(weapon, w.kind);
                so = new SerializedObject(weapon);
                if (w.category != WeaponCategory.None)
                    so.FindProperty("weaponCategory").enumValueIndex = (int)w.category;
                int stack = so.FindProperty("stackMax").intValue; // thrown weapons stack
                so.ApplyModifiedPropertiesWithoutUndo();
                ItemPresets.ApplyWeaponTier(weapon, w.tier);
                so = new SerializedObject(weapon);
                so.FindProperty("stackMax").intValue = stack;
                if (w.element != ElementType.None)
                    so.FindProperty("elementType").enumValueIndex = (int)w.element;
                if (w.form != DamageForm.None)
                    so.FindProperty("damageForm").enumValueIndex = (int)w.form;
                w.extra?.Invoke(so);
                so.ApplyModifiedPropertiesWithoutUndo();
                Made(weapon, r, $"Weapon: {w.name} ({w.tier} {w.kind}{(w.element != ElementType.None ? ", " + w.element : "")})");
            }
        }
    }

    // ================================================================== armour sets
    private struct SetSpec
    {
        public string name;
        public ItemPresets.ArmorMaterial material;
        public Color color;
        public string description;
        public (string name, string description, EquipmentEffect[] effects) two, four;
        /// <summary>Name of the pieces ("Assassin's") and the word per slot (helmet, chest, legs, boots, gloves); null = from the material.</summary>
        public string prefix;
        public string[] words;
    }

    private static readonly ArmorSlotType[] SetSlots =
    {
        ArmorSlotType.Helmet, ArmorSlotType.Chestplate, ArmorSlotType.Leggings, ArmorSlotType.Boots, ArmorSlotType.Gloves,
    };

    private static SetSpec[] Sets() => new[]
    {
        new SetSpec
        {
            name = "Apprentice's Garb", material = ItemPresets.ArmorMaterial.Cloth, color = new Color(0.55f, 0.6f, 1f),
            description = "Light robes for those who fight with magic.",
            two = ("Focused Mind", "+15% Max Mana", new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.MaxMana, 15f)) }),
            four = ("Arcane Flow", "+10% ability damage, -10% ability cooldowns",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.AbilityDamage, 10f), TraitModifier.Percent(TraitStat.AbilityCooldown, -10f)) }),
        },
        new SetSpec
        {
            name = "Ranger's Leathers", material = ItemPresets.ArmorMaterial.Leather, color = new Color(0.55f, 0.75f, 0.35f),
            description = "Supple leather for scouts and hunters.",
            two = ("Light Step", "+8% move speed", new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.MoveSpeed, 8f)) }),
            four = ("Hunter's Instinct", "+5 Agility, -30% wound chance",
                new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.Agility, 5f)), Stats(TraitModifier.Percent(TraitStat.WoundChance, -30f)) }),
        },
        new SetSpec
        {
            name = "Soldier's Chainmail", material = ItemPresets.ArmorMaterial.Chainmail, color = new Color(0.7f, 0.72f, 0.78f),
            description = "Interlocked rings that turn blades.",
            two = ("Drilled", "+5 Defense", new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.Defense, 5f)) }),
            four = ("Rings Of Steel", "-25% slash damage, -40% cuts from slashes and claws",
                new EquipmentEffect[]
                {
                    Stats(TraitModifier.Percent(TraitStat.SlashDamageTaken, -25f)),
                    Resist(new InjuryResistance("Chainmail", InjuryDefaults.TagWound) { forms = DamageFormMask.Slash | DamageFormMask.Claw, injuryChance = -40f }),
                }),
        },
        new SetSpec
        {
            name = "Ironclad Armor", material = ItemPresets.ArmorMaterial.Iron, color = new Color(0.6f, 0.6f, 0.62f),
            description = "Heavy iron plates. Slow, but hard to break.",
            two = ("Ironclad", "+8 Defense", new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.Defense, 8f)) }),
            four = ("Unbreakable", "+10% Max Health, -30% fracture chance",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.MaxHealth, 10f), TraitModifier.Percent(TraitStat.FractureChance, -30f)) }),
        },
        new SetSpec
        {
            name = "Knight's Plate", material = ItemPresets.ArmorMaterial.Steel, color = new Color(0.85f, 0.85f, 0.95f),
            description = "Full steel plate of a sworn knight.",
            two = ("Bulwark", "+12 Defense, +5 Strength",
                new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.Defense, 12f), new CombatStatModifier(CombatStatType.Strength, 5f)) }),
            four = ("Steel Resolve", "-15% blunt damage, -40% limb loss chance",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.BluntDamageTaken, -15f), TraitModifier.Percent(TraitStat.LimbLossChance, -40f)) }),
        },
        new SetSpec
        {
            name = "Mythril Vanguard", material = ItemPresets.ArmorMaterial.Mythril, color = new Color(0.45f, 0.85f, 1f),
            description = "Elven mythril: as strong as steel and light as cloth.",
            two = ("Warded", "+10 Magic Resistance, +15% fire resistance",
                new EquipmentEffect[] { new CombatStatsEffect(new[] { new CombatStatModifier(CombatStatType.MagicResistance, 10f) },
                                                                  new[] { new ElementalResistance(ElementType.Fire, 15f) }) }),
            four = ("Vanguard", "+15% weapon damage, -25% injury chance",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 15f), TraitModifier.Percent(TraitStat.InjuryChance, -25f)) }),
        },
        Set("Assassin's Shroud", ItemPresets.ArmorMaterial.Leather, new Color(0.3f, 0.3f, 0.35f), "Dark leathers that make no sound.", "Assassin's",
            new[] { "Hood", "Vest", "Leggings", "Soft Boots", "Wraps" },
            ("Unseen", "+5% critical chance", new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.CriticalChance, 5f)) }),
            ("Killing Blow", "+20% dagger damage, +25% critical damage, -20% stamina cost",
                new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.WeaponDamage, 20f, WeaponCategory.Dagger), new CombatStatModifier(CombatStatType.CriticalDamage, 25f)),
                                        Stats(TraitModifier.Percent(TraitStat.StaminaCost, -20f)) })),
        Set("Berserker's Hide", ItemPresets.ArmorMaterial.Leather, new Color(0.65f, 0.35f, 0.25f), "Furs and bone for those who fight without fear.", "Berserker's",
            new[] { "Skull Helm", "Hide Vest", "Fur Leggings", "Fur Boots", "Grips" },
            ("Rage", "+10% weapon damage", new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 10f)) }),
            ("Bloodlust", "+15% attack speed, -30% bleeding, +10% damage taken",
                new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.AttackSpeed, 15f)),
                                        Stats(TraitModifier.Percent(TraitStat.BleedingRate, -30f), TraitModifier.Percent(TraitStat.DamageTaken, 10f)) })),
        Set("Paladin's Aegis", ItemPresets.ArmorMaterial.Steel, new Color(1f, 0.92f, 0.6f), "Blessed white steel of a holy order.", "Paladin's",
            new[] { "Great Helm", "Breastplate", "Cuisses", "Sabatons", "Gauntlets" },
            ("Blessed", "+20% holy and +10% dark resistance",
                new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Holy, 20f), new ElementalResistance(ElementType.Dark, 10f) }) }),
            ("Divine Grace", "+20% healing received, +15% max health",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.HealingReceived, 20f), TraitModifier.Percent(TraitStat.MaxHealth, 15f)) })),
        Set("Shadow Weave", ItemPresets.ArmorMaterial.Cloth, new Color(0.35f, 0.2f, 0.5f), "Cloth woven from shadow.", "Shadowweave",
            new[] { "Cowl", "Robe", "Trousers", "Slippers", "Gloves" },
            ("Veil", "+20% dark resistance", new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Dark, 20f) }) }),
            ("Umbral Power", "+15% ability damage, +10% move speed",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.AbilityDamage, 15f), TraitModifier.Percent(TraitStat.MoveSpeed, 10f)) })),
        Set("Dragonscale Armor", ItemPresets.ArmorMaterial.Mythril, new Color(0.85f, 0.3f, 0.2f), "Scales shed by a dragon, hammered into plate.", "Dragonscale",
            new[] { "Helm", "Hauberk", "Greaves", "Boots", "Gauntlets" },
            ("Fireproof", "+30% fire resistance", new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Fire, 30f) }) }),
            ("Dragon's Hide", "-50% burn chance, -20% slash damage",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.BurnChance, -50f), TraitModifier.Percent(TraitStat.SlashDamageTaken, -20f)) })),
        Set("Frostguard Plate", ItemPresets.ArmorMaterial.Iron, new Color(0.6f, 0.85f, 1f), "Cold iron from the northern forges.", "Frostguard",
            new[] { "Helm", "Cuirass", "Greaves", "Boots", "Gauntlets" },
            ("Cold Blood", "+30% ice resistance", new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Ice, 30f) }) }),
            ("Winter's Wall", "-40% burn and frostbite chance, -20% blunt damage",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.BurnChance, -40f), TraitModifier.Percent(TraitStat.BluntDamageTaken, -20f)) })),
        Set("Battlemage Regalia", ItemPresets.ArmorMaterial.Chainmail, new Color(0.45f, 0.5f, 0.9f), "Mail enchanted for spellblades.", "Battlemage",
            new[] { "Circlet", "Mail Robe", "Mail Leggings", "Boots", "Spellguards" },
            ("Arcane Mail", "+15% max mana, +4 Defense",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.MaxMana, 15f)), Combat(new CombatStatModifier(CombatStatType.Defense, 4f)) }),
            ("Spellblade", "+10% ability and weapon damage",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.AbilityDamage, 10f), TraitModifier.Percent(TraitStat.WeaponDamage, 10f)) })),
        Set("Plague Doctor's Garb", ItemPresets.ArmorMaterial.Leather, new Color(0.4f, 0.45f, 0.3f), "Waxed leather and a beaked mask.", "Plague Doctor's",
            new[] { "Mask", "Coat", "Trousers", "Boots", "Gloves" },
            ("Miasma Filter", "+30% poison resistance", new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Poison, 30f) }) }),
            ("Physician", "-60% infection chance, +20% injury healing",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.InfectionChance, -60f), TraitModifier.Percent(TraitStat.InjuryHealing, 20f)) })),
        Set("Gladiator's Harness", ItemPresets.ArmorMaterial.Iron, new Color(0.8f, 0.6f, 0.3f), "Arena armour: little cover, lots of crowd.", "Gladiator's",
            new[] { "Helm", "Harness", "Greaves", "Sandals", "Manica" },
            ("Showman", "+5 Strength", new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.Strength, 5f)) }),
            ("Arena Veteran", "-30% injury chance, +10% max stamina",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.InjuryChance, -30f), TraitModifier.Percent(TraitStat.MaxStamina, 10f)) })),
        Set("Druid's Vestments", ItemPresets.ArmorMaterial.Leather, new Color(0.35f, 0.65f, 0.35f), "Bark, leaves and leather.", "Druid's",
            new[] { "Antler Crown", "Bark Vest", "Leaf Leggings", "Root Boots", "Vine Gloves" },
            ("Nature's Touch", "+1 health regeneration", new EquipmentEffect[] { Stats(TraitModifier.Flat(TraitStat.HealthRegen, 1f)) }),
            ("Wild Growth", "+30% injury healing, +20% earth resistance",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.InjuryHealing, 30f)), new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Earth, 20f) }) })),
        Set("Samurai Armor", ItemPresets.ArmorMaterial.Chainmail, new Color(0.75f, 0.2f, 0.2f), "Lacquered plates laced with silk.", "Samurai",
            new[] { "Kabuto", "Do", "Haidate", "Suneate", "Kote" },
            ("Bushido", "+15% sword damage", new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.WeaponDamage, 15f, WeaponCategory.Sword)) }),
            ("Way Of The Blade", "-25% slash damage, +20% critical damage",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.SlashDamageTaken, -25f)), Combat(new CombatStatModifier(CombatStatType.CriticalDamage, 20f)) })),
        Set("Sentinel's Bulwark", ItemPresets.ArmorMaterial.Steel, new Color(0.55f, 0.6f, 0.7f), "The heaviest plate there is.", "Sentinel's",
            new[] { "Visored Helm", "Plate", "Tassets", "Iron Boots", "Gauntlets" },
            ("Immovable", "+15 Defense", new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.Defense, 15f)) }),
            ("Last Wall", "-20% damage taken, -50% limb loss chance, -10% move speed",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.DamageTaken, -20f), TraitModifier.Percent(TraitStat.LimbLossChance, -50f),
                                              TraitModifier.Percent(TraitStat.MoveSpeed, -10f)) })),
        Set("Wanderer's Gear", ItemPresets.ArmorMaterial.Leather, new Color(0.6f, 0.5f, 0.35f), "Worn travelling clothes with many pockets.", "Wanderer's",
            new[] { "Hat", "Coat", "Trousers", "Boots", "Gloves" },
            ("Pack Straps", "+15 carry weight", new EquipmentEffect[] { Stats(TraitModifier.Flat(TraitStat.CarryWeight, 15f)) }),
            ("Long Road", "+10% move speed, -20% stamina cost",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.MoveSpeed, 10f), TraitModifier.Percent(TraitStat.StaminaCost, -20f)) })),
    };

    private static SetSpec Set(string name, ItemPresets.ArmorMaterial material, Color color, string description, string prefix, string[] words,
        (string, string, EquipmentEffect[]) two, (string, string, EquipmentEffect[]) four) => new SetSpec
    {
        name = name, material = material, color = color, description = description, prefix = prefix, words = words, two = two, four = four,
    };

    private static CharacterStatsEffect Stats(params TraitModifier[] mods) => new CharacterStatsEffect(mods);
    private static CombatStatsEffect Combat(params CombatStatModifier[] mods) => new CombatStatsEffect(mods);
    private static InjuryResistanceEquipEffect Resist(params InjuryResistance[] list) =>
        new InjuryResistanceEquipEffect { resistances = new List<InjuryResistance>(list) };

    private static void ArmorSets(Report r)
    {
        foreach (SetSpec spec in Sets())
        {
            string folder = SMAssetsEditor.Folder($"{SMAssets.Armor}/{Sub}/{SMAssetsEditor.CleanFileName(spec.name)}");
            string setPath = $"{folder}/{SMAssetsEditor.CleanFileName(spec.name)} Set.asset";
            ArmorSet set = AssetDatabase.LoadAssetAtPath<ArmorSet>(setPath);
            bool newSet = set == null;
            if (newSet)
            {
                set = ScriptableObject.CreateInstance<ArmorSet>();
                AssetDatabase.CreateAsset(set, setPath);
                var setSo = new SerializedObject(set);
                setSo.FindProperty("setName").stringValue = spec.name;
                setSo.FindProperty("setDescription").stringValue = spec.description;
                setSo.FindProperty("setColor").colorValue = spec.color;
                setSo.FindProperty("minimumPiecesForSet").intValue = 2;
                setSo.FindProperty("maximumSetPieces").intValue = SetSlots.Length;
                setSo.ApplyModifiedPropertiesWithoutUndo();
                set.SetEffects.Clear();
                set.SetEffects.Add(Tier(2, spec.name, spec.two));
                set.SetEffects.Add(Tier(4, spec.name, spec.four));
                r.created++;
                r.lines.Add($"Armor set: {spec.name} (2: {spec.two.description}; 4: {spec.four.description})");
            }
            else
                r.skipped++;

            foreach (ArmorSlotType slot in SetSlots)
            {
                string pieceName = PieceName(spec, slot);
                string path = $"{folder}/{SMAssetsEditor.CleanFileName(pieceName)}.asset";
                ArmorSO piece = AssetDatabase.LoadAssetAtPath<ArmorSO>(path);
                if (piece != null)
                {
                    r.skipped++;
                    if (!set.ContainsPiece(piece)) set.AddPiece(piece);
                    continue;
                }
                piece = ScriptableObject.CreateInstance<ArmorSO>();
                AssetDatabase.CreateAsset(piece, path);
                var so = new SerializedObject(piece);
                so.FindProperty("name").stringValue = pieceName;
                so.FindProperty("description").stringValue = $"Part of the {spec.name} set. {spec.description}";
                so.FindProperty("itemType").enumValueIndex = (int)ItemType.Armor;
                so.FindProperty("armorSlotType").enumValueIndex = (int)slot;
                so.FindProperty("belongsToArmorSet").objectReferenceValue = set;
                so.ApplyModifiedPropertiesWithoutUndo();
                ItemPresets.ApplyArmor(piece, spec.material);
                set.AddPiece(piece);
                if (!set.RequiredSlotTypes.Contains(slot))
                    set.RequiredSlotTypes.Add(slot);
                Made(piece, r, null);
            }
            EditorUtility.SetDirty(set);
        }
    }

    /// <summary>"Apprentice Hood", "Knight's Plate Greaves"...: a name per material and slot.</summary>
    private static string PieceName(SetSpec spec, ArmorSlotType slot)
    {
        if (!string.IsNullOrEmpty(spec.prefix) && spec.words != null && spec.words.Length == SetSlots.Length)
            return $"{spec.prefix} {spec.words[Array.IndexOf(SetSlots, slot)]}";
        string[] words;
        switch (spec.material)
        {
            case ItemPresets.ArmorMaterial.Cloth: words = new[] { "Hood", "Robe", "Trousers", "Sandals", "Gloves" }; break;
            case ItemPresets.ArmorMaterial.Leather: words = new[] { "Cap", "Jerkin", "Breeches", "Boots", "Bracers" }; break;
            case ItemPresets.ArmorMaterial.Chainmail: words = new[] { "Coif", "Hauberk", "Chausses", "Boots", "Gauntlets" }; break;
            default: words = new[] { "Helm", "Cuirass", "Greaves", "Sabatons", "Gauntlets" }; break;
        }
        int i = Array.IndexOf(SetSlots, slot);
        string prefix = spec.material == ItemPresets.ArmorMaterial.Cloth ? "Apprentice" : spec.material == ItemPresets.ArmorMaterial.Leather ? "Ranger"
            : spec.material == ItemPresets.ArmorMaterial.Chainmail ? "Soldier's" : spec.material == ItemPresets.ArmorMaterial.Iron ? "Ironclad"
            : spec.material == ItemPresets.ArmorMaterial.Steel ? "Knight's" : "Mythril";
        return $"{prefix} {words[Mathf.Clamp(i, 0, words.Length - 1)]}";
    }

    private static ArmorSetEffect Tier(int pieces, string setName, (string name, string description, EquipmentEffect[] effects) t) => new ArmorSetEffect
    {
        piecesRequired = pieces,
        effectName = $"{t.name} ({pieces})",
        effectDescription = t.description,
        effects = new List<EquipmentEffect>(t.effects),
    };

    // ================================================================== shields and accessories
    private struct A
    {
        public string name;
        public ArmorSlotType slot;
        public ItemPresets.ArmorMaterial material;
        public string description;
        public Func<ShieldDefense> shield;
        public EquipmentEffect[] effects;
    }

    private static A[] AccessoryList() => new[]
    {
        new A { name = "Wooden Buckler", slot = ArmorSlotType.Shield, material = ItemPresets.ArmorMaterial.Leather, shield = ShieldDefense.Buckler,
                description = "Small and quick: parry more than block." },
        new A { name = "Iron Round Shield", slot = ArmorSlotType.Shield, material = ItemPresets.ArmorMaterial.Iron, shield = ShieldDefense.Round,
                description = "A sturdy round shield." },
        new A { name = "Steel Kite Shield", slot = ArmorSlotType.Shield, material = ItemPresets.ArmorMaterial.Steel, shield = ShieldDefense.Kite,
                description = "Covers from shoulder to knee." },
        new A { name = "Mythril Tower Shield", slot = ArmorSlotType.Shield, material = ItemPresets.ArmorMaterial.Mythril, shield = ShieldDefense.Tower,
                description = "A wall you can carry." },
        new A { name = "Ring Of Vitality", slot = ArmorSlotType.Ring, material = ItemPresets.ArmorMaterial.Steel, description = "+10% Max Health.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.MaxHealth, 10f)) } },
        new A { name = "Ring Of The Mind", slot = ArmorSlotType.Ring, material = ItemPresets.ArmorMaterial.Cloth, description = "+15% Max Mana.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.MaxMana, 15f)) } },
        new A { name = "Ring Of Strength", slot = ArmorSlotType.Ring, material = ItemPresets.ArmorMaterial.Iron, description = "+4 Strength.",
                effects = new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.Strength, 4f)) } },
        new A { name = "Amulet Of Warding", slot = ArmorSlotType.Amulet, material = ItemPresets.ArmorMaterial.Mythril, description = "+8 Magic Resistance.",
                effects = new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.MagicResistance, 8f)) } },
        new A { name = "Amulet Of Thick Blood", slot = ArmorSlotType.Amulet, material = ItemPresets.ArmorMaterial.Steel, description = "Wounds bleed 40% less.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.BleedingRate, -40f)) } },
        new A { name = "Lucky Charm", slot = ArmorSlotType.Trinket, material = ItemPresets.ArmorMaterial.Cloth, description = "+3% critical chance.",
                effects = new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.CriticalChance, 3f)) } },
        new A { name = "Traveler's Cloak", slot = ArmorSlotType.Cloak, material = ItemPresets.ArmorMaterial.Leather, description = "+5% move speed.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.MoveSpeed, 5f)) } },
        new A { name = "Sturdy Belt", slot = ArmorSlotType.Belt, material = ItemPresets.ArmorMaterial.Leather, description = "+10 carry weight.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Flat(TraitStat.CarryWeight, 10f)) } },
        new A { name = "Duelist's Bracers", slot = ArmorSlotType.Bracers, material = ItemPresets.ArmorMaterial.Steel, description = "+5% attack speed.",
                effects = new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.AttackSpeed, 5f)) } },
        new A { name = "Iron Pauldrons", slot = ArmorSlotType.Shoulders, material = ItemPresets.ArmorMaterial.Iron, description = "Shoulder plates of iron." },
        // More shields
        new A { name = "Elven Buckler", slot = ArmorSlotType.Shield, material = ItemPresets.ArmorMaterial.Mythril, shield = ShieldDefense.Buckler, description = "Featherlight, made for parrying." },
        new A { name = "Iron Heater Shield", slot = ArmorSlotType.Shield, material = ItemPresets.ArmorMaterial.Iron, shield = ShieldDefense.Kite, description = "A knight's shield." },
        new A { name = "Spiked Round Shield", slot = ArmorSlotType.Shield, material = ItemPresets.ArmorMaterial.Steel, shield = ShieldDefense.Round, description = "The boss is a spike: bash with it." },
        new A { name = "Steel Pavise", slot = ArmorSlotType.Shield, material = ItemPresets.ArmorMaterial.Steel, shield = ShieldDefense.Tower, description = "A crossbowman's wall." },
        new A { name = "Dragon Shield", slot = ArmorSlotType.Shield, material = ItemPresets.ArmorMaterial.Mythril, shield = ShieldDefense.Kite, description = "+30% fire resistance.",
                effects = new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Fire, 30f) }) } },
        new A { name = "Holy Bulwark", slot = ArmorSlotType.Shield, material = ItemPresets.ArmorMaterial.Steel, shield = ShieldDefense.Tower, description = "+25% holy and dark resistance.",
                effects = new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Holy, 25f), new ElementalResistance(ElementType.Dark, 25f) }) } },
        // Rings
        new A { name = "Ring Of Fire Ward", slot = ArmorSlotType.Ring, material = ItemPresets.ArmorMaterial.Steel, description = "+20% fire resistance.",
                effects = new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Fire, 20f) }) } },
        new A { name = "Ring Of Frost Ward", slot = ArmorSlotType.Ring, material = ItemPresets.ArmorMaterial.Steel, description = "+20% ice resistance.",
                effects = new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Ice, 20f) }) } },
        new A { name = "Ring Of Storm Ward", slot = ArmorSlotType.Ring, material = ItemPresets.ArmorMaterial.Steel, description = "+20% lightning resistance.",
                effects = new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Lightning, 20f) }) } },
        new A { name = "Duelist's Ring", slot = ArmorSlotType.Ring, material = ItemPresets.ArmorMaterial.Mythril, description = "+5% critical chance.",
                effects = new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.CriticalChance, 5f)) } },
        new A { name = "Ring Of Haste", slot = ArmorSlotType.Ring, material = ItemPresets.ArmorMaterial.Mythril, description = "+8% move speed.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.MoveSpeed, 8f)) } },
        new A { name = "Ring Of Regeneration", slot = ArmorSlotType.Ring, material = ItemPresets.ArmorMaterial.Mythril, description = "+1 health regeneration.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Flat(TraitStat.HealthRegen, 1f)) } },
        new A { name = "Berserker Signet", slot = ArmorSlotType.Ring, material = ItemPresets.ArmorMaterial.Iron, description = "+12% weapon damage, +10% damage taken.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 12f), TraitModifier.Percent(TraitStat.DamageTaken, 10f)) } },
        new A { name = "Ring Of Thick Skin", slot = ArmorSlotType.Ring, material = ItemPresets.ArmorMaterial.Leather, description = "-30% wound chance.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.WoundChance, -30f)) } },
        new A { name = "Bone Ring", slot = ArmorSlotType.Ring, material = ItemPresets.ArmorMaterial.Leather, description = "-30% fracture chance.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.FractureChance, -30f)) } },
        new A { name = "Archmage's Band", slot = ArmorSlotType.Ring, material = ItemPresets.ArmorMaterial.Mythril, description = "-10% ability cooldowns, +10% ability damage.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.AbilityCooldown, -10f), TraitModifier.Percent(TraitStat.AbilityDamage, 10f)) } },
        // Amulets
        new A { name = "Amulet Of Life", slot = ArmorSlotType.Amulet, material = ItemPresets.ArmorMaterial.Mythril, description = "+15% max health.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.MaxHealth, 15f)) } },
        new A { name = "Holy Symbol", slot = ArmorSlotType.Amulet, material = ItemPresets.ArmorMaterial.Steel, description = "+20% holy resistance, +10% healing received.",
                effects = new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Holy, 20f) }), Stats(TraitModifier.Percent(TraitStat.HealingReceived, 10f)) } },
        new A { name = "Amulet Of Clarity", slot = ArmorSlotType.Amulet, material = ItemPresets.ArmorMaterial.Cloth, description = "+1 mana regeneration.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Flat(TraitStat.ManaRegen, 1f)) } },
        new A { name = "Talisman Of Health", slot = ArmorSlotType.Amulet, material = ItemPresets.ArmorMaterial.Cloth, description = "-50% infection chance, +20% injury healing.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.InfectionChance, -50f), TraitModifier.Percent(TraitStat.InjuryHealing, 20f)) } },
        new A { name = "Wolf Tooth Necklace", slot = ArmorSlotType.Amulet, material = ItemPresets.ArmorMaterial.Leather, description = "-25% bite and claw damage.",
                effects = new EquipmentEffect[] { Resist(new InjuryResistance("Wolf Tooth") { forms = DamageFormMask.Bite | DamageFormMask.Claw, damageTaken = -25f }) } },
        // Trinkets
        new A { name = "Lucky Coin", slot = ArmorSlotType.Trinket, material = ItemPresets.ArmorMaterial.Cloth, description = "+15% critical damage.",
                effects = new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.CriticalDamage, 15f)) } },
        new A { name = "Whetstone Charm", slot = ArmorSlotType.Trinket, material = ItemPresets.ArmorMaterial.Cloth, description = "+5% weapon damage.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 5f)) } },
        new A { name = "Sand Hourglass", slot = ArmorSlotType.Trinket, material = ItemPresets.ArmorMaterial.Cloth, description = "-10% ability cooldowns.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.AbilityCooldown, -10f)) } },
        new A { name = "Quiver Charm", slot = ArmorSlotType.Trinket, material = ItemPresets.ArmorMaterial.Cloth, description = "+15% projectile speed.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.ProjectileSpeed, 15f)) } },
        // Cloaks, belts, bracers, shoulders
        new A { name = "Cloak Of Shadows", slot = ArmorSlotType.Cloak, material = ItemPresets.ArmorMaterial.Cloth, description = "+15% dark resistance, +5% move speed.",
                effects = new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Dark, 15f) }), Stats(TraitModifier.Percent(TraitStat.MoveSpeed, 5f)) } },
        new A { name = "Fur Cloak", slot = ArmorSlotType.Cloak, material = ItemPresets.ArmorMaterial.Leather, description = "+25% ice resistance.",
                effects = new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Ice, 25f) }) } },
        new A { name = "Salamander Cloak", slot = ArmorSlotType.Cloak, material = ItemPresets.ArmorMaterial.Leather, description = "+25% fire resistance.",
                effects = new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Fire, 25f) }) } },
        new A { name = "Elven Cloak", slot = ArmorSlotType.Cloak, material = ItemPresets.ArmorMaterial.Mythril, description = "+10% move speed.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.MoveSpeed, 10f)) } },
        new A { name = "Belt Of The Giant", slot = ArmorSlotType.Belt, material = ItemPresets.ArmorMaterial.Iron, description = "+3 Strength, +20 carry weight.",
                effects = new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.Strength, 3f)), Stats(TraitModifier.Flat(TraitStat.CarryWeight, 20f)) } },
        new A { name = "Alchemist's Belt", slot = ArmorSlotType.Belt, material = ItemPresets.ArmorMaterial.Leather, description = "+20% healing received.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.HealingReceived, 20f)) } },
        new A { name = "Champion's Belt", slot = ArmorSlotType.Belt, material = ItemPresets.ArmorMaterial.Steel, description = "+10% max stamina.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.MaxStamina, 10f)) } },
        new A { name = "Archer's Bracers", slot = ArmorSlotType.Bracers, material = ItemPresets.ArmorMaterial.Leather, description = "+10% projectile speed, +3 Agility.",
                effects = new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.ProjectileSpeed, 10f)), Combat(new CombatStatModifier(CombatStatType.Agility, 3f)) } },
        new A { name = "Iron Bracers", slot = ArmorSlotType.Bracers, material = ItemPresets.ArmorMaterial.Iron, description = "Plain iron bracers." },
        new A { name = "Steel Pauldrons", slot = ArmorSlotType.Shoulders, material = ItemPresets.ArmorMaterial.Steel, description = "Steel shoulder plates." },
        new A { name = "Mythril Spaulders", slot = ArmorSlotType.Shoulders, material = ItemPresets.ArmorMaterial.Mythril, description = "Light mythril shoulder guards." },
    };

    private static void Accessories(Report r)
    {
        string folder = SMAssetsEditor.Folder($"{SMAssets.Armor}/{Sub}/Shields and Accessories");
        foreach (A a in AccessoryList())
        {
            string path = $"{folder}/{SMAssetsEditor.CleanFileName(a.name)}.asset";
            if (Exists<ArmorSO>(path, r))
                continue;
            var item = ScriptableObject.CreateInstance<ArmorSO>();
            AssetDatabase.CreateAsset(item, path);
            var so = new SerializedObject(item);
            so.FindProperty("name").stringValue = a.name;
            so.FindProperty("description").stringValue = a.description;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.Armor;
            so.FindProperty("armorSlotType").enumValueIndex = (int)a.slot;
            so.ApplyModifiedPropertiesWithoutUndo();
            ItemPresets.ApplyArmor(item, a.material);
            if (a.shield != null)
                item.SetShieldDefense(a.shield());
            if (a.effects != null)
                item.EquipEffects.AddRange(a.effects);
            Made(item, r, $"Armor: {a.name} ({SlotTypeHelper.GetDisplayName(a.slot)})");
        }
    }

    // ================================================================== consumables and ammo
    private static void Consumables(Report r)
    {
        string folder = SMAssetsEditor.Folder($"{SMAssets.Consumables}/{Sub}");
        foreach (ItemPresets.Consumable c in Enum.GetValues(typeof(ItemPresets.Consumable)))
        {
            string path = $"{folder}/{ObjectNames.NicifyVariableName(c.ToString())}.asset";
            if (Exists<ConsumableSO>(path, r))
                continue;
            var item = ScriptableObject.CreateInstance<ConsumableSO>();
            AssetDatabase.CreateAsset(item, path);
            ItemPresets.ApplyConsumable(item, c);
            Made(item, r, $"Consumable: {item.Name}");
        }
        string ammoFolder = SMAssetsEditor.Folder($"{SMAssets.Ammo}/{Sub}");
        foreach (ItemPresets.Ammo a in Enum.GetValues(typeof(ItemPresets.Ammo)))
        {
            string path = $"{ammoFolder}/{ObjectNames.NicifyVariableName(a.ToString())}.asset";
            if (Exists<AmmoSO>(path, r))
                continue;
            var item = ScriptableObject.CreateInstance<AmmoSO>();
            AssetDatabase.CreateAsset(item, path);
            ItemPresets.ApplyAmmo(item, a);
            Made(item, r, $"Ammo: {item.Name}");
        }
        foreach (C c in ConsumableList())
        {
            string path = $"{folder}/{SMAssetsEditor.CleanFileName(c.name)}.asset";
            if (Exists<ConsumableSO>(path, r))
                continue;
            var item = ScriptableObject.CreateInstance<ConsumableSO>();
            AssetDatabase.CreateAsset(item, path);
            ItemPresets.ApplyConsumable(item, c.name, c.description, c.food, c.stack, c.weight, c.price, c.cooldown, c.effects);
            Made(item, r, $"Consumable: {c.name}");
        }
        foreach (AmmoSpec a in AmmoList())
        {
            string path = $"{ammoFolder}/{SMAssetsEditor.CleanFileName(a.name)}.asset";
            if (Exists<AmmoSO>(path, r))
                continue;
            var item = ScriptableObject.CreateInstance<AmmoSO>();
            AssetDatabase.CreateAsset(item, path);
            var so = new SerializedObject(item);
            so.FindProperty("name").stringValue = a.name;
            so.FindProperty("description").stringValue = a.description;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.Ammo;
            so.FindProperty("ammoType").stringValue = a.type;
            so.FindProperty("damageMultiplier").floatValue = a.damage;
            so.FindProperty("bonusDamage").floatValue = a.bonus;
            so.FindProperty("element").enumValueIndex = (int)a.element;
            so.FindProperty("damageForm").enumValueIndex = (int)a.form;
            so.FindProperty("stackMax").intValue = a.stack;
            so.FindProperty("weight").floatValue = a.weight;
            so.FindProperty("price").floatValue = a.price;
            if (a.onHit != null)
            {
                SerializedProperty fx = so.FindProperty("onHitEffects");
                foreach (AbilityEffect e in a.onHit)
                {
                    fx.InsertArrayElementAtIndex(fx.arraySize);
                    fx.GetArrayElementAtIndex(fx.arraySize - 1).managedReferenceValue = e;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Made(item, r, $"Ammo: {a.name}");
        }
    }

    // ================================================================== more consumables and ammo
    private struct C
    {
        public string name, description;
        public bool food;
        public int stack;
        public float weight, price, cooldown;
        public (ConsumableEffectType type, float amount, float duration, float tick, string name)[] effects;
    }

    private static C Potion(string name, string description, float price, params (ConsumableEffectType, float, float, float, string)[] fx) =>
        new C { name = name, description = description, food = false, stack = 10, weight = 0.3f, price = price, cooldown = 1f, effects = fx };

    private static C Food(string name, string description, float price, int stack, float weight, params (ConsumableEffectType, float, float, float, string)[] fx) =>
        new C { name = name, description = description, food = true, stack = stack, weight = weight, price = price, cooldown = 0.5f, effects = fx };

    private static (ConsumableEffectType, float, float, float, string) E(ConsumableEffectType t, float amount, float seconds = 0f, string name = null) =>
        (t, amount, seconds, seconds > 0f ? 1f : 0f, name);

    private static C[] ConsumableList()
    {
        const ConsumableEffectType Hp = ConsumableEffectType.Hp, Mana = ConsumableEffectType.Mana, Sta = ConsumableEffectType.Stamina,
            Food_ = ConsumableEffectType.Food, Drink = ConsumableEffectType.Drink, Sanity = ConsumableEffectType.Sanity, Heat = ConsumableEffectType.BodyHeat,
            Sleep = ConsumableEffectType.Sleep, MaxHp = ConsumableEffectType.MaxHp, MaxMana = ConsumableEffectType.MaxMana, MaxSta = ConsumableEffectType.MaxStamina;
        return new[]
        {
            Potion("Minor Mana Potion", "Restores 25 mana.", 10f, E(Mana, 25f)),
            Potion("Greater Mana Potion", "Restores 100 mana.", 60f, E(Mana, 100f)),
            Potion("Minor Stamina Potion", "Restores 30 stamina.", 8f, E(Sta, 30f)),
            Potion("Greater Stamina Potion", "Restores 120 stamina.", 45f, E(Sta, 120f)),
            Potion("Superior Health Potion", "Restores 200 health.", 140f, E(Hp, 200f)),
            Potion("Elixir Of Life", "Restores 400 health and 100 mana.", 400f, E(Hp, 400f), E(Mana, 100f)),
            Potion("Rejuvenation Potion", "Restores 40 health, mana and stamina.", 50f, E(Hp, 40f), E(Mana, 40f), E(Sta, 40f)),
            Potion("Mana Regeneration Potion", "Restores 80 mana over 15 seconds.", 35f, E(Mana, 80f, 15f, "Mana Regeneration")),
            Potion("Stamina Regeneration Potion", "Restores 100 stamina over 15 seconds.", 30f, E(Sta, 100f, 15f, "Second Breath")),
            Potion("Elixir Of Vigor", "+30 max health for 5 minutes.", 80f, E(MaxHp, 30f, 300f, "Vigor")),
            Potion("Elixir Of Focus", "+40 max mana for 5 minutes.", 80f, E(MaxMana, 40f, 300f, "Focus")),
            Potion("Endurance Draught", "+40 max stamina for 5 minutes.", 60f, E(MaxSta, 40f, 300f, "Endurance")),
            Potion("Sanity Tonic", "Calms the mind (+30 sanity).", 25f, E(Sanity, 30f)),
            Potion("Warming Draught", "Warms you up in the cold.", 15f, E(Heat, 20f)),
            Potion("Cooling Draught", "Cools you down in the heat.", 15f, E(Heat, -20f)),
            Potion("Energy Tonic", "Keeps you awake.", 12f, E(Sleep, 25f), E(Sta, 20f)),
            Food("Cheese", "A wedge of hard cheese.", 4f, 20, 0.2f, E(Food_, 20f)),
            Food("Berries", "A handful of wild berries.", 1f, 30, 0.05f, E(Food_, 8f), E(Drink, 4f)),
            Food("Carrot", "Crunchy and fresh.", 1f, 30, 0.1f, E(Food_, 10f)),
            Food("Jerky", "Dried meat that keeps for months.", 5f, 30, 0.1f, E(Food_, 30f)),
            Food("Hardtack", "A soldier's biscuit. Never spoils.", 2f, 40, 0.1f, E(Food_, 25f)),
            Food("Grilled Fish", "Fresh from the river.", 6f, 20, 0.3f, E(Food_, 30f)),
            Food("Roast Chicken", "Juicy and filling; heals a little over time.", 12f, 10, 0.6f, E(Food_, 50f), E(Hp, 15f, 10f, "Well Fed")),
            Food("Fish Stew", "Hot stew: food, water and warmth.", 10f, 10, 0.5f, E(Food_, 45f), E(Drink, 15f), E(Heat, 5f)),
            Food("Mushroom Soup", "Earthy and warming.", 8f, 10, 0.4f, E(Food_, 35f), E(Drink, 10f), E(Heat, 8f)),
            Food("Hearty Stew", "A meal for a long day: heals over time and warms.", 18f, 10, 0.6f, E(Food_, 60f), E(Hp, 20f, 15f, "Hearty"), E(Heat, 10f)),
            Food("Honey Cake", "Sweet and comforting.", 8f, 20, 0.2f, E(Food_, 30f), E(Sanity, 5f)),
            Food("Meat Pie", "A golden crust full of meat.", 10f, 10, 0.4f, E(Food_, 40f), E(Sanity, 10f)),
            Food("Milk", "A bottle of fresh milk.", 3f, 10, 0.5f, E(Drink, 30f), E(Food_, 5f)),
            Food("Fruit Juice", "Refreshing and energising.", 4f, 10, 0.4f, E(Drink, 35f), E(Sta, 15f)),
            Food("Herbal Tea", "Soothing and warm.", 4f, 10, 0.3f, E(Drink, 30f), E(Sanity, 10f), E(Heat, 5f)),
            Food("Ale", "Good for the mood, bad for the legs.", 5f, 10, 0.5f, E(Drink, 20f), E(Sanity, 10f), E(Sta, -10f)),
            Food("Wine", "A fine vintage.", 15f, 10, 0.5f, E(Drink, 15f), E(Sanity, 15f)),
        };
    }

    private struct AmmoSpec
    {
        public string name, description, type;
        public float damage, bonus, weight, price;
        public int stack;
        public ElementType element;
        public DamageForm form;
        public AbilityEffect[] onHit;
    }

    private static AmmoSpec[] AmmoList() => new[]
    {
        new AmmoSpec { name = "Fire Arrows", description = "Burning arrows.", type = "Arrow", damage = 1f, stack = 40, weight = 0.06f, price = 3f, element = ElementType.Fire },
        new AmmoSpec { name = "Frost Arrows", description = "Arrows tipped with ice.", type = "Arrow", damage = 1f, stack = 40, weight = 0.06f, price = 3f, element = ElementType.Ice,
                       onHit = new AbilityEffect[] { new SlowEffect { slowAmount = 0.3f, duration = 2f } } },
        new AmmoSpec { name = "Blunt Arrows", description = "Padded heads: bruise and stun instead of cutting.", type = "Arrow", damage = 0.8f, stack = 50, weight = 0.05f, price = 1f,
                       form = DamageForm.Blunt, onHit = new AbilityEffect[] { new StunEffect { duration = 0.5f, chance = 0.25f } } },
        new AmmoSpec { name = "Barbed Arrows", description = "Hooked heads that make wounds bleed.", type = "Arrow", damage = 1.05f, stack = 40, weight = 0.06f, price = 2.5f,
                       form = DamageForm.Pierce, onHit = new AbilityEffect[] { new DamageOverTimeEffect { damagePerTick = 1.5f, tickInterval = 0.5f, duration = 4f } } },
        new AmmoSpec { name = "Armor-Piercing Bolts", description = "Hardened bolts.", type = "Bolt", damage = 1.3f, stack = 30, weight = 0.09f, price = 3f, form = DamageForm.Pierce },
        new AmmoSpec { name = "Poison Darts", description = "Darts soaked in venom.", type = "Dart", damage = 1f, bonus = 0f, stack = 40, weight = 0.02f, price = 2f, element = ElementType.Poison,
                       onHit = new AbilityEffect[] { new DamageOverTimeEffect { damagePerTick = 2f, tickInterval = 1f, duration = 6f, element = ElementType.Poison } } },
        new AmmoSpec { name = "Sleep Darts", description = "Darts that make the target slow and drowsy.", type = "Dart", damage = 0.5f, stack = 30, weight = 0.02f, price = 3f,
                       onHit = new AbilityEffect[] { new SlowEffect { slowAmount = 0.6f, duration = 4f } } },
        new AmmoSpec { name = "Iron Shot", description = "Heavy balls for the hand cannon.", type = "Shot", damage = 1f, stack = 20, weight = 0.4f, price = 4f, form = DamageForm.Crush },
        new AmmoSpec { name = "Silver Bullets", description = "Blessed silver, for the undead.", type = "Bullet", damage = 1.1f, stack = 30, weight = 0.03f, price = 5f, element = ElementType.Holy },
        new AmmoSpec { name = "River Pebbles", description = "Smooth sling stones.", type = "Stone", damage = 0.9f, stack = 40, weight = 0.08f, price = 0f, form = DamageForm.Blunt },
    };

    // ================================================================== helpers
    private static bool Exists<T>(string path, Report r) where T : UnityEngine.Object
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) == null)
            return false;
        r.skipped++;
        return true;
    }

    private static void Made(ItemSO item, Report r, string line)
    {
        item.SetGridSize(ItemPresets.SuggestGridSize(item), true);
        EditorUtility.SetDirty(item);
        r.items.Add(item);
        r.created++;
        if (!string.IsNullOrEmpty(line))
            r.lines.Add(line);
    }
}
#endif
