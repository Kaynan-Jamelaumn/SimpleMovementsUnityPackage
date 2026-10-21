#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
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
                "Creates about 125 weapons with full movesets (hit volumes, chains, charged attacks, slams, effects), 60 armour sets " +
                "(19 standard, 15 built from bracers, rings, cloaks, belts..., 22 with 2 to 9 pieces and bonuses at any count: full set only, " +
                "one bonus at 2, 3 or 4, a bonus per piece, up to 5 steps), 144 armour pieces outside any set (some with powers, some with abilities on their key: Acid Blood Burst, Heist, Twin Gates...), " +
                "about 50 shields and accessories, about 45 potions, foods and drinks, 17 kinds of ammo " +
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
        /// <summary>The slots of the pieces (null = helmet, chest, legs, boots, gloves); <see cref="words"/> follows it.</summary>
        public ArmorSlotType[] slots;
        /// <summary>Pieces for the second bonus (0 = 4, or every piece when the set has fewer).</summary>
        public int fullAt;

        /// <summary>Any number of bonus steps (overrides <see cref="two"/> and <see cref="four"/>).</summary>
        public TierSpec[] tiers;

        public ArmorSlotType[] Slots => slots != null && slots.Length > 0 ? slots : SetSlots;
        public int FullAt => fullAt > 0 ? fullAt : Mathf.Min(4, Slots.Length);
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

    private static readonly ArmorSlotType
        Head = ArmorSlotType.Helmet, Chest = ArmorSlotType.Chestplate, Legs = ArmorSlotType.Leggings, Feet = ArmorSlotType.Boots,
        Hands = ArmorSlotType.Gloves, Shield = ArmorSlotType.Shield, Ring = ArmorSlotType.Ring, Trinket = ArmorSlotType.Trinket,
        Cloak = ArmorSlotType.Cloak, Belt = ArmorSlotType.Belt, Shoulders = ArmorSlotType.Shoulders, Bracers = ArmorSlotType.Bracers,
        Amulet = ArmorSlotType.Amulet;

    // ================================================================== sets with any number of bonus steps
    private struct TierSpec
    {
        public int pieces;
        public string name, description, group;
        public EquipmentEffect[] effects;
    }

    /// <summary>A bonus at <paramref name="pieces"/> pieces that adds up with the others.</summary>
    private static TierSpec T(int pieces, string name, string description, params EquipmentEffect[] fx) =>
        new TierSpec { pieces = pieces, name = name, description = description, effects = fx };

    /// <summary>A bonus in an upgrade group: only the highest reached step of the group is active.</summary>
    private static TierSpec Step(string group, int pieces, string name, string description, params EquipmentEffect[] fx) =>
        new TierSpec { pieces = pieces, name = name, description = description, group = group, effects = fx };

    private static SetSpec TSet(string name, ItemPresets.ArmorMaterial material, Color color, string description, string prefix,
        ArmorSlotType[] slots, string[] words, params TierSpec[] tiers) => new SetSpec
    {
        name = name, material = material, color = color, description = description, prefix = prefix, slots = slots, words = words, tiers = tiers,
    };

    private static OnHitEffect OnHit(float chance, float cooldown, params AbilityEffect[] fx) =>
        new OnHitEffect { chance = chance, cooldown = cooldown, effects = new List<AbilityEffect>(fx) };

    private static WhenHitEffect WhenHit(float chance, float cooldown, params AbilityEffect[] fx) =>
        new WhenHitEffect { chance = chance, cooldown = cooldown, effects = new List<AbilityEffect>(fx) };

    private static PulseEffect Pulse(float interval, float radius, TargetFilter who, bool combatOnly, params AbilityEffect[] fx) =>
        new PulseEffect { interval = interval, radius = radius, affects = who, onlyInCombat = combatOnly, effects = new List<AbilityEffect>(fx) };

    private static WhileConditionEffect While(WhileConditionEffect.Condition condition, float threshold, params EquipmentEffect[] fx) =>
        new WhileConditionEffect { condition = condition, threshold = threshold, effects = new List<EquipmentEffect>(fx) };

    private static DamageOverTimeEffect Dot(ElementType element, float perTick, float seconds) =>
        new DamageOverTimeEffect { element = element, damagePerTick = perTick, tickInterval = 0.5f, duration = seconds };

    private static StatEffect Exhaust(float stamina) => new StatEffect { effectType = AttackEffectType.Stamina, amount = -stamina, effectName = "Exhausted" };

    /// <summary>The power ability <paramref name="ability"/> on the item's key (the ability asset is generated if missing).</summary>
    private static AbilityOnKeyEffect Key(string ability) => new AbilityOnKeyEffect { ability = PowerAbilityGenerator.GetOrCreate(ability) };

    private static CombatStatsEffect Crit(float percent) => Combat(new CombatStatModifier(CombatStatType.CriticalChance, percent));

    /// <summary>
    /// Sets with their own bonus steps: some only at the full set, some with a single bonus at 2, 3 or 4 pieces, some
    /// with a bonus for every piece, some whose steps replace each other (an upgrade group) and some with 4 or 5 steps.
    /// </summary>
    private static SetSpec[] TieredSets() => new[]
    {
        // ---------------- full set only
        TSet("Regalia Of The Fallen King", ItemPresets.ArmorMaterial.Mythril, new Color(0.8f, 0.7f, 0.3f), "A dead king's armor. It does nothing until it is whole.", "Fallen King's",
            new[] { Head, Chest, Legs, Feet, Hands, Cloak, Amulet }, new[] { "Crown", "Cuirass", "Greaves", "Sabatons", "Gauntlets", "Royal Mantle", "Seal" },
            T(7, "Return Of The King", "Full set only: +20 Defense, +20% weapon and ability damage, survive a killing blow (every 5 minutes)",
                Combat(new CombatStatModifier(CombatStatType.Defense, 20f)),
                Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 20f), TraitModifier.Percent(TraitStat.AbilityDamage, 20f)),
                Behaviour(new CheatDeathTrait()))),
        TSet("Dreadnought Plate", ItemPresets.ArmorMaterial.Steel, new Color(0.3f, 0.3f, 0.35f), "Walking siege armor. Only works as one machine.", "Dreadnought",
            new[] { Head, Chest, Legs, Feet, Shoulders, Belt }, new[] { "Helm", "Hull", "Legplates", "Treads", "Bastions", "Girdle" },
            T(6, "Unstoppable", "Full set only: -30% damage taken, -60% control taken, -50% limb loss, -20% move speed",
                Stats(TraitModifier.Percent(TraitStat.DamageTaken, -30f), TraitModifier.Percent(TraitStat.ControlTaken, -60f),
                      TraitModifier.Percent(TraitStat.LimbLossChance, -50f), TraitModifier.Percent(TraitStat.MoveSpeed, -20f)))),
        TSet("Nomad's Wraps", ItemPresets.ArmorMaterial.Cloth, new Color(0.85f, 0.75f, 0.55f), "Desert cloth: three pieces, one purpose.", "Nomad's",
            new[] { Head, Cloak, Feet }, new[] { "Headwrap", "Sand Cloak", "Sandals" },
            T(3, "Desert Born", "Full set only: +25% fire resistance, -30% stamina cost, +10% move speed",
                Ward(ElementType.Fire, 25f), Stats(TraitModifier.Percent(TraitStat.StaminaCost, -30f), TraitModifier.Percent(TraitStat.MoveSpeed, 10f)))),

        // ---------------- a single bonus at 2, 3 or 4 pieces
        TSet("Twin Fangs", ItemPresets.ArmorMaterial.Leather, new Color(0.45f, 0.7f, 0.3f), "A pair of venom-soaked bracers. Wear both.", "Twin Fang",
            new[] { Hands, Bracers }, new[] { "Grips", "Bracers" },
            T(2, "Venom", "2 pieces: 30% of weapon hits poison (3 damage every 0.5 s for 4 s)", OnHit(0.3f, 0f, Dot(ElementType.Poison, 3f, 4f)))),
        TSet("Titan's Grip", ItemPresets.ArmorMaterial.Iron, new Color(0.6f, 0.5f, 0.4f), "Gauntlets and bracers forged for giants.", "Titan's",
            new[] { Hands, Bracers }, new[] { "Gauntlets", "Bracers" },
            T(2, "Titan Strength", "2 pieces: +8 Strength, +25% knockback, +20% hammer and mace damage",
                Combat(new CombatStatModifier(CombatStatType.Strength, 8f), new CombatStatModifier(CombatStatType.Knockback, 25f),
                       new CombatStatModifier(CombatStatType.WeaponDamage, 20f, WeaponCategory.Hammer), new CombatStatModifier(CombatStatType.WeaponDamage, 20f, WeaponCategory.Mace)))),
        TSet("Stoneskin Totems", ItemPresets.ArmorMaterial.Leather, new Color(0.55f, 0.5f, 0.45f), "Three stone charms; any two of them harden the skin.", "Stoneskin",
            new[] { Trinket, Amulet, Belt }, new[] { "Totem", "Pendant", "Belt" },
            T(2, "Stoneskin", "2 pieces: -15% slash, pierce and blunt damage, -25% fracture chance",
                Stats(TraitModifier.Percent(TraitStat.SlashDamageTaken, -15f), TraitModifier.Percent(TraitStat.PierceDamageTaken, -15f),
                      TraitModifier.Percent(TraitStat.BluntDamageTaken, -15f), TraitModifier.Percent(TraitStat.FractureChance, -25f)))),
        TSet("Hunter's Camouflage", ItemPresets.ArmorMaterial.Leather, new Color(0.35f, 0.45f, 0.25f), "Leaves and bark stitched to leather.", "Hunter's",
            new[] { Head, Chest, Legs, Cloak, Feet }, new[] { "Ghillie Hood", "Ghillie Vest", "Leggings", "Leaf Cloak", "Moccasins" },
            T(3, "Ambush", "3 pieces: +20% bow and crossbow damage, +10% critical chance, +20% crouch speed",
                Combat(new CombatStatModifier(CombatStatType.WeaponDamage, 20f, WeaponCategory.Bow), new CombatStatModifier(CombatStatType.WeaponDamage, 20f, WeaponCategory.Crossbow),
                       new CombatStatModifier(CombatStatType.CriticalChance, 10f)),
                Stats(TraitModifier.Percent(TraitStat.CrouchSpeed, 20f)))),
        TSet("Bloodbound Pact", ItemPresets.ArmorMaterial.Chainmail, new Color(0.6f, 0.05f, 0.05f), "Four cursed pieces sealed with blood.", "Bloodbound",
            new[] { Chest, Hands, Ring, Amulet }, new[] { "Hauberk", "Gauntlets", "Ring", "Vial" },
            T(4, "Blood Pact", "4 pieces: heal for 8% of damage dealt; below 30% health +30% weapon damage; +15% damage taken",
                Behaviour(new LifeStealTrait()),
                While(WhileConditionEffect.Condition.HealthBelow, 0.3f, Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 30f))),
                Stats(TraitModifier.Percent(TraitStat.DamageTaken, 15f)))),

        // ---------------- 3 and 5 (no 2-piece bonus)
        TSet("Stormforged Plate", ItemPresets.ArmorMaterial.Steel, new Color(0.4f, 0.6f, 1f), "Steel struck by lightning while it cooled.", "Stormforged",
            new[] { Head, Chest, Legs, Feet, Hands }, new[] { "Helm", "Breastplate", "Greaves", "Boots", "Gauntlets" },
            T(3, "Conductor", "3 pieces: +30% lightning resistance, +10% attack speed",
                Ward(ElementType.Lightning, 30f), Combat(new CombatStatModifier(CombatStatType.AttackSpeed, 10f))),
            T(5, "Thunderclap", "5 pieces: 20% of hits stun for 0.6 s (every 3 s at most); attackers in melee are slowed",
                OnHit(0.2f, 3f, new StunEffect { duration = 0.6f }),
                WhenHit(0.5f, 2f, new SlowEffect { slowAmount = 0.4f, duration = 2f }))),
        TSet("Inferno Regalia", ItemPresets.ArmorMaterial.Chainmail, new Color(1f, 0.4f, 0.1f), "Mail that is always warm to the touch.", "Inferno",
            new[] { Head, Chest, Legs, Feet, Hands }, new[] { "Crown", "Coat", "Leggings", "Boots", "Gloves" },
            T(3, "Kindling", "3 pieces: +25% fire resistance; 25% of hits set the target on fire",
                Ward(ElementType.Fire, 25f), OnHit(0.25f, 0f, Dot(ElementType.Fire, 4f, 3f))),
            T(5, "Living Inferno", "5 pieces: in combat, burn enemies within 4 m every 2 s",
                Pulse(2f, 4f, TargetFilter.Enemies, true, new DamageEffect { amount = 8f, element = ElementType.Fire }))),

        // ---------------- 2, 3 and full (three steps)
        TSet("Frost Warden's Aegis", ItemPresets.ArmorMaterial.Iron, new Color(0.6f, 0.85f, 1f), "Shield and plates rimed with eternal frost.", "Frost Warden's",
            new[] { Head, Chest, Shoulders, Shield }, new[] { "Helm", "Plate", "Pauldrons", "Glacier Shield" },
            T(2, "Chill", "2 pieces: +25% ice resistance", Ward(ElementType.Ice, 25f)),
            T(3, "Frostbite", "3 pieces: attackers are slowed by 40% for 2 s", WhenHit(1f, 1f, new SlowEffect { slowAmount = 0.4f, duration = 2f })),
            T(4, "Permafrost", "Full set: 25% of attackers are frozen in place for 1.5 s, +10 Defense",
                WhenHit(0.25f, 4f, new RootEffect { duration = 1.5f }), Combat(new CombatStatModifier(CombatStatType.Defense, 10f)))),
        TSet("Whisperwind Gear", ItemPresets.ArmorMaterial.Cloth, new Color(0.75f, 0.95f, 0.95f), "Each piece makes you lighter.", "Whisperwind",
            new[] { Feet, Cloak, Belt, Ring }, new[] { "Slippers", "Cloak", "Sash", "Band" },
            T(2, "Light Step", "2 pieces: +10% move speed", Stats(TraitModifier.Percent(TraitStat.MoveSpeed, 10f))),
            T(3, "Updraft", "3 pieces: double jump", Behaviour(new DoubleJumpTrait())),
            T(4, "Featherfall", "Full set: glide while holding Jump, +15% jump force",
                Behaviour(new GlideTrait()), Stats(TraitModifier.Percent(TraitStat.JumpForce, 15f)))),
        TSet("Necromancer's Shroud", ItemPresets.ArmorMaterial.Cloth, new Color(0.3f, 0.5f, 0.3f), "Robes that drink the last breath of the fallen.", "Necromancer's",
            new[] { Head, Chest, Hands, Amulet, Ring }, new[] { "Cowl", "Robe", "Bone Gloves", "Phylactery", "Ring" },
            T(2, "Grave Chill", "2 pieces: +20% dark resistance, +10% ability damage",
                Ward(ElementType.Dark, 20f), Stats(TraitModifier.Percent(TraitStat.AbilityDamage, 10f))),
            T(4, "Soul Harvest", "4 pieces: kills heal 5% and restore 10 stamina", Behaviour(new OnKillRewardsTrait())),
            T(5, "Lich Form", "Full set: survive a killing blow; +20% max mana; -20% healing received",
                Behaviour(new CheatDeathTrait()), Stats(TraitModifier.Percent(TraitStat.MaxMana, 20f), TraitModifier.Percent(TraitStat.HealingReceived, -20f)))),

        // ---------------- four or five steps
        TSet("Shadow Stalker", ItemPresets.ArmorMaterial.Leather, new Color(0.15f, 0.1f, 0.2f), "Six pieces, four secrets.", "Shadow Stalker",
            new[] { Head, Chest, Legs, Feet, Hands, Cloak }, new[] { "Mask", "Vest", "Leggings", "Boots", "Gloves", "Shroud" },
            T(2, "Soft Steps", "2 pieces: +25% crouch speed", Stats(TraitModifier.Percent(TraitStat.CrouchSpeed, 25f))),
            T(3, "Precision", "3 pieces: +8% critical chance", Crit(8f)),
            T(4, "Hamstring", "4 pieces: critical hits slow the target by 50% for 2 s",
                new OnHitEffect { chance = 1f, onlyCriticalHits = true, effects = new List<AbilityEffect> { new SlowEffect { slowAmount = 0.5f, duration = 2f } } }),
            T(6, "Assassinate", "Full set: +40% critical damage, +25% dagger damage",
                Combat(new CombatStatModifier(CombatStatType.CriticalDamage, 40f), new CombatStatModifier(CombatStatType.WeaponDamage, 25f, WeaponCategory.Dagger)))),
        TSet("Sanctified Templar", ItemPresets.ArmorMaterial.Steel, new Color(1f, 0.95f, 0.75f), "Seven pieces blessed in seven temples.", "Templar",
            new[] { Head, Chest, Legs, Feet, Hands, Shield, Amulet }, new[] { "Helm", "Hauberk", "Chausses", "Sabatons", "Gauntlets", "Aegis", "Reliquary" },
            T(2, "Devotion", "2 pieces: +15% holy resistance", Ward(ElementType.Holy, 15f)),
            T(3, "Smite", "3 pieces: 20% of hits deal 10 holy damage", OnHit(0.2f, 0f, new DamageEffect { amount = 10f, element = ElementType.Holy })),
            T(5, "Bulwark Of Faith", "5 pieces: -15% damage taken, +20% healing received",
                Stats(TraitModifier.Percent(TraitStat.DamageTaken, -15f), TraitModifier.Percent(TraitStat.HealingReceived, 20f))),
            T(7, "Consecration", "Full set: heal allies within 6 m for 8 every 3 s",
                Pulse(3f, 6f, TargetFilter.Allies | TargetFilter.Self, false, new HealEffect { amount = 8f }))),
        TSet("Warborn Fury", ItemPresets.ArmorMaterial.Iron, new Color(0.7f, 0.2f, 0.1f), "The more it bleeds, the harder it hits.", "Warborn",
            new[] { Head, Chest, Legs, Feet, Hands, Belt }, new[] { "Helm", "Cuirass", "Tassets", "Boots", "Fists", "War Belt" },
            T(2, "Battle Cry", "2 pieces: in combat, +10% attack speed",
                While(WhileConditionEffect.Condition.InCombat, 0f, Combat(new CombatStatModifier(CombatStatType.AttackSpeed, 10f)))),
            T(3, "Blood Rage", "3 pieces: below 50% health, +20% weapon damage",
                While(WhileConditionEffect.Condition.HealthBelow, 0.5f, Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 20f)))),
            T(4, "Stagger", "4 pieces: half of your hits drain 15 stamina from the target", OnHit(0.5f, 0f, Exhaust(15f))),
            T(5, "Second Wind", "5 pieces: below 25% health heal 30% (every 90 s)", Behaviour(new SecondWindTrait())),
            T(6, "Undying Rage", "Full set: below 25% health, -40% damage taken",
                While(WhileConditionEffect.Condition.HealthBelow, 0.25f, Stats(TraitModifier.Percent(TraitStat.DamageTaken, -40f))))),

        // ---------------- every piece counts (upgrade groups: only the highest step is active)
        TSet("Gambler's Luck", ItemPresets.ArmorMaterial.Cloth, new Color(0.2f, 0.75f, 0.35f), "Every lucky charm adds to the odds.", "Gambler's",
            new[] { Ring, Amulet, Trinket, Belt, Hands }, new[] { "Dice Ring", "Clover", "Lucky Coin", "Card Belt", "Gloves" },
            Step("luck", 1, "Lucky", "1 piece: +2% critical chance", Crit(2f)),
            Step("luck", 2, "Luckier", "2 pieces: +4% critical chance", Crit(4f)),
            Step("luck", 3, "Charmed", "3 pieces: +7% critical chance", Crit(7f)),
            Step("luck", 4, "Blessed By Fortune", "4 pieces: +10% critical chance", Crit(10f)),
            T(5, "Jackpot", "Full set: critical hits heal you for 5 and deal +30% critical damage",
                Combat(new CombatStatModifier(CombatStatType.CriticalDamage, 30f)),
                new OnHitEffect { chance = 1f, onlyCriticalHits = true, effects = new List<AbilityEffect> { new HealEffect { amount = 5f, recipient = EffectRecipient.Caster } } })),
        TSet("Ascendant Seraph", ItemPresets.ArmorMaterial.Mythril, new Color(1f, 1f, 0.85f), "Eight radiant pieces; the light grows with each one.", "Seraph",
            new[] { Head, Chest, Legs, Feet, Hands, Shoulders, Cloak, Amulet }, new[] { "Halo", "Raiment", "Greaves", "Sandals", "Gauntlets", "Wings", "Mantle", "Sigil" },
            Step("radiance", 2, "Glimmer", "2 pieces: +5% max health and holy resistance",
                Stats(TraitModifier.Percent(TraitStat.MaxHealth, 5f)), Ward(ElementType.Holy, 5f)),
            Step("radiance", 4, "Radiance", "4 pieces: +10% max health and holy resistance (replaces Glimmer)",
                Stats(TraitModifier.Percent(TraitStat.MaxHealth, 10f)), Ward(ElementType.Holy, 10f)),
            Step("radiance", 6, "Blinding Light", "6 pieces: +15% max health and holy resistance (replaces Radiance)",
                Stats(TraitModifier.Percent(TraitStat.MaxHealth, 15f)), Ward(ElementType.Holy, 15f)),
            T(6, "Wings", "6 pieces: glide while holding Jump", Behaviour(new GlideTrait())),
            T(8, "Ascension", "Full set: double jump, survive a killing blow, 6 holy damage to enemies within 5 m every 2 s in combat",
                Behaviour(new DoubleJumpTrait()), Behaviour(new CheatDeathTrait()),
                Pulse(2f, 5f, TargetFilter.Enemies, true, new DamageEffect { amount = 6f, element = ElementType.Holy }))),
        TSet("Iron Fortress", ItemPresets.ArmorMaterial.Iron, new Color(0.5f, 0.5f, 0.55f), "The more of it you wear, the less can hurt you.", "Fortress",
            new[] { Head, Chest, Legs, Feet, Hands, Shield }, new[] { "Helm", "Plate", "Greaves", "Boots", "Gauntlets", "Wall" },
            Step("fortress", 3, "Battlements", "3 pieces: +8 Defense", Combat(new CombatStatModifier(CombatStatType.Defense, 8f))),
            Step("fortress", 5, "Keep", "5 pieces: +16 Defense (replaces Battlements)", Combat(new CombatStatModifier(CombatStatType.Defense, 16f))),
            Step("fortress", 6, "Citadel", "Full set: +25 Defense, -30% control taken (replaces Keep)",
                Combat(new CombatStatModifier(CombatStatType.Defense, 25f)), Stats(TraitModifier.Percent(TraitStat.ControlTaken, -30f)))),
        TSet("Sunforged Panoply", ItemPresets.ArmorMaterial.Mythril, new Color(1f, 0.75f, 0.2f), "Nine pieces forged at noon on the longest day.", "Sunforged",
            new[] { Head, Chest, Legs, Feet, Hands, Shoulders, Bracers, Belt, Cloak },
            new[] { "Sun Helm", "Sun Plate", "Sun Greaves", "Sun Boots", "Sun Gauntlets", "Sun Pauldrons", "Sun Vambraces", "Sun Belt", "Sun Cloak" },
            Step("sun", 3, "Dawn", "3 pieces: +5% weapon damage, +10% fire resistance",
                Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 5f)), Ward(ElementType.Fire, 10f)),
            Step("sun", 5, "Morning", "5 pieces: +10% weapon damage, +20% fire resistance (replaces Dawn)",
                Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 10f)), Ward(ElementType.Fire, 20f)),
            Step("sun", 7, "Noon", "7 pieces: +15% weapon damage, +30% fire resistance (replaces Morning)",
                Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 15f)), Ward(ElementType.Fire, 30f)),
            T(7, "Sunfire", "7 pieces: 20% of hits burn the target", OnHit(0.2f, 0f, Dot(ElementType.Fire, 5f, 3f))),
            T(9, "Solar Flare", "Full set: burn enemies within 5 m every 2 s in combat, +2 health regeneration",
                Pulse(2f, 5f, TargetFilter.Enemies, true, new DamageEffect { amount = 10f, element = ElementType.Fire }),
                Stats(TraitModifier.Flat(TraitStat.HealthRegen, 2f)))),

        // ---------------- mixed steps with groups and extras
        TSet("Trinity Of Elements", ItemPresets.ArmorMaterial.Mythril, new Color(0.7f, 0.4f, 0.9f), "Ring, pendant and charm: fire, ice and lightning.", "Trinity",
            new[] { Ring, Amulet, Trinket }, new[] { "Ring Of Embers", "Pendant Of Frost", "Charm Of Storms" },
            T(1, "Attunement", "1 piece: +10% fire, ice and lightning resistance",
                new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Fire, 10f), new ElementalResistance(ElementType.Ice, 10f), new ElementalResistance(ElementType.Lightning, 10f) })),
            T(2, "Confluence", "2 pieces: +15% ability damage", Stats(TraitModifier.Percent(TraitStat.AbilityDamage, 15f))),
            T(3, "Trinity", "Full set: hits may burn, chill (slow) or shock (stun)",
                OnHit(0.15f, 0f, Dot(ElementType.Fire, 3f, 3f)), OnHit(0.15f, 0f, new SlowEffect { slowAmount = 0.35f, duration = 2f }),
                OnHit(0.08f, 3f, new StunEffect { duration = 0.5f }))),
        TSet("Tidecaller's Raiment", ItemPresets.ArmorMaterial.Cloth, new Color(0.2f, 0.55f, 0.85f), "Robes that never quite dry.", "Tidecaller's",
            new[] { Head, Chest, Legs, Amulet }, new[] { "Coral Crown", "Robe", "Wraps", "Pearl" },
            T(2, "Tides", "2 pieces: +1 mana regeneration, +20% water resistance",
                Stats(TraitModifier.Flat(TraitStat.ManaRegen, 1f)), Ward(ElementType.Water, 20f)),
            T(3, "Undertow", "3 pieces: 20% of ability hits slow the target by 50%",
                new OnHitEffect { trigger = ProcTrigger.AbilityHits, chance = 0.2f, effects = new List<AbilityEffect> { new SlowEffect { slowAmount = 0.5f, duration = 2f } } }),
            T(4, "Tsunami", "Full set: +25% ability area, +1 ability charge",
                Stats(TraitModifier.Percent(TraitStat.AbilityArea, 25f), TraitModifier.Flat(TraitStat.AbilityCharges, 1f)))),
        TSet("Juggernaut's Harness", ItemPresets.ArmorMaterial.Steel, new Color(0.45f, 0.35f, 0.3f), "Built to charge through walls.", "Juggernaut",
            new[] { Chest, Shoulders, Feet, Belt, Bracers }, new[] { "Harness", "Spiked Pauldrons", "Stompers", "Belt", "Vambraces" },
            T(3, "Momentum", "3 pieces: +20% knockback, +5 Endurance",
                Combat(new CombatStatModifier(CombatStatType.Knockback, 20f), new CombatStatModifier(CombatStatType.Endurance, 5f))),
            T(5, "Trample", "Full set: hits drain 20 stamina and 25% of them stun for 0.8 s (every 4 s at most)",
                OnHit(1f, 0f, Exhaust(20f)), OnHit(0.25f, 4f, new StunEffect { duration = 0.8f }))),

        // ---------------- sets with powers
        TSet("Colossus Plate", ItemPresets.ArmorMaterial.Steel, new Color(0.55f, 0.45f, 0.35f), "Armor forged for a giant, and it makes you one.", "Colossus",
            new[] { Head, Chest, Legs, Feet, Hands }, new[] { "Helm", "Breastplate", "Greaves", "Stompers", "Fists" },
            T(2, "Bulk", "2 pieces: +10 Defense", Combat(new CombatStatModifier(CombatStatType.Defense, 10f))),
            T(4, "Colossal", "4 pieces: you are 25% bigger; health, strength and knockback grow with you",
                new PowerWhileWornEffect("Colossal", new SizeChangePower { sizePercent = 25f })),
            T(5, "Unmovable", "Full set: immune to knockback, pulls and slows",
                new PowerWhileWornEffect("Unmovable", new ControlImmunityPower { immuneTo = ControlImmunity.AllDisplacement | ControlImmunity.Slow, cleanseOnStart = false }))),
        TSet("Featherlight Regalia", ItemPresets.ArmorMaterial.Cloth, new Color(0.85f, 0.9f, 1f), "Silk so light it floats.", "Featherlight",
            new[] { Head, Chest, Feet, Cloak }, new[] { "Circlet", "Robe", "Slippers", "Wings" },
            T(2, "Light", "2 pieces: +10% move speed, +15% jump force",
                Stats(TraitModifier.Percent(TraitStat.MoveSpeed, 10f), TraitModifier.Percent(TraitStat.JumpForce, 15f))),
            T(4, "Skywalker", "Full set: walk on air for 6 mana per second",
                new PowerWhileWornEffect("Skywalker", new MovementPower { behaviour = new SkyWalkTrait { cost = SkyWalkCost.Mana, costPerSecond = 6f, endWhenEmpty = false } }))),
        TSet("Sprite's Trinkets", ItemPresets.ArmorMaterial.Mythril, new Color(0.6f, 1f, 0.6f), "Tiny trinkets of the fae.", "Sprite's",
            new[] { Ring, Amulet, Trinket }, new[] { "Ring", "Locket", "Acorn" },
            T(3, "Fae Size", "Full set: you are 35% smaller, 25% faster, +10% critical chance; you take 20% more damage",
                new PowerWhileWornEffect("Fae Size", new SizeChangePower { sizePercent = -35f, healthPerSize = 0f, weaponDamagePerSize = 0f, knockbackPerSize = 0f,
                    strengthPerSize = 0f, jumpPerSize = 0f, moveSpeedPercent = 25f, damageTakenPercent = 20f }), Crit(10f))),
        TSet("Juggernaut Titan", ItemPresets.ArmorMaterial.Iron, new Color(0.4f, 0.4f, 0.45f), "Each piece adds more mass.", "Titan",
            new[] { Head, Chest, Legs, Feet, Shoulders, Belt }, new[] { "Helm", "Plate", "Legplates", "Boots", "Pauldrons", "Girdle" },
            Step("titan", 2, "Heavy", "2 pieces: 5% bigger", new PowerWhileWornEffect("Heavy", new SizeChangePower { sizePercent = 5f })),
            Step("titan", 4, "Massive", "4 pieces: 15% bigger (replaces Heavy)", new PowerWhileWornEffect("Massive", new SizeChangePower { sizePercent = 15f })),
            Step("titan", 6, "Titanic", "Full set: 30% bigger (replaces Massive)", new PowerWhileWornEffect("Titanic", new SizeChangePower { sizePercent = 30f })),
            T(6, "Immovable", "Full set: immune to knockback and stuns",
                new PowerWhileWornEffect("Immovable", new ControlImmunityPower { immuneTo = ControlImmunity.AllDisplacement | ControlImmunity.Stun, cleanseOnStart = false }))),
    };

    private static SetSpec Set(string name, ItemPresets.ArmorMaterial material, Color color, string description, string prefix,
        ArmorSlotType[] slots, string[] words, int fullAt, (string, string, EquipmentEffect[]) two, (string, string, EquipmentEffect[]) four)
    {
        SetSpec s = Set(name, material, color, description, prefix, words, two, four);
        s.slots = slots;
        s.fullAt = fullAt;
        return s;
    }

    private static PassiveBehaviourEffect Behaviour(TraitBehaviour b) => new PassiveBehaviourEffect { behaviour = b };

    private static CombatStatsEffect Ward(ElementType element, float percent) =>
        new CombatStatsEffect(null, new[] { new ElementalResistance(element, percent) });

    /// <summary>Sets made of the other slots (jewellery, cloaks, belts, bracers, shoulders, shields) or mixing them in.</summary>
    private static SetSpec[] AccessorySets() => new[]
    {
        Set("Windwalker's Regalia", ItemPresets.ArmorMaterial.Cloth, new Color(0.6f, 0.95f, 0.85f), "Light gear that rides the wind.", "Windwalker's",
            new[] { Cloak, Feet, Belt, Bracers }, new[] { "Cloak", "Boots", "Sash", "Wristwraps" }, 4,
            ("Tailwind", "+10% move speed, +10% jump force",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.MoveSpeed, 10f), TraitModifier.Percent(TraitStat.JumpForce, 10f)) }),
            ("Ride The Wind", "Double jump, and glide while holding Jump",
                new EquipmentEffect[] { Behaviour(new DoubleJumpTrait()), Behaviour(new GlideTrait()) })),
        Set("Duelist's Finery", ItemPresets.ArmorMaterial.Leather, new Color(0.85f, 0.75f, 0.45f), "For those who fight with one blade and one hand free.", "Duelist's",
            new[] { Hands, Bracers, Cloak, Ring }, new[] { "Gloves", "Bracers", "Half-Cape", "Signet" }, 4,
            ("Poise", "+5% critical chance", new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.CriticalChance, 5f)) }),
            ("Riposte", "+15% sword and dagger damage, +10% attack speed",
                new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.WeaponDamage, 15f, WeaponCategory.Sword),
                                               new CombatStatModifier(CombatStatType.WeaponDamage, 15f, WeaponCategory.Dagger),
                                               new CombatStatModifier(CombatStatType.AttackSpeed, 10f)) })),
        Set("Archmage's Jewels", ItemPresets.ArmorMaterial.Mythril, new Color(0.55f, 0.45f, 1f), "Rings and charms that hum with power.", "Archmage's",
            new[] { Ring, Amulet, Trinket }, new[] { "Ring", "Pendant", "Focus Crystal" }, 3,
            ("Attuned", "+20% max mana, +1 mana regeneration",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.MaxMana, 20f), TraitModifier.Flat(TraitStat.ManaRegen, 1f)) }),
            ("Arcane Mastery", "+20% ability damage, -15% ability cooldowns, +1 ability charge",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.AbilityDamage, 20f), TraitModifier.Percent(TraitStat.AbilityCooldown, -15f),
                                              TraitModifier.Flat(TraitStat.AbilityCharges, 1f)) })),
        Set("Bulwark Of The Legion", ItemPresets.ArmorMaterial.Steel, new Color(0.75f, 0.2f, 0.2f), "Standard issue of the shield wall.", "Legion",
            new[] { Shield, Shoulders, Bracers, Belt }, new[] { "Scutum", "Pauldrons", "Vambraces", "Cingulum" }, 4,
            ("Shield Wall", "+10 Defense", new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.Defense, 10f)) }),
            ("Hold The Line", "-15% damage taken, -40% control taken",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.DamageTaken, -15f), TraitModifier.Percent(TraitStat.ControlTaken, -40f)) })),
        Set("Ranger's Kit", ItemPresets.ArmorMaterial.Leather, new Color(0.4f, 0.6f, 0.3f), "A hunter's hood, quiver strap and bracers.", "Ranger's Kit",
            new[] { Head, Cloak, Bracers, Belt, Feet }, new[] { "Hood", "Cloak", "Bracers", "Quiver Belt", "Boots" }, 4,
            ("Keen Eye", "+15% projectile speed", new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.ProjectileSpeed, 15f)) }),
            ("Volley", "+1 extra projectile, +15% bow damage",
                new EquipmentEffect[] { Stats(TraitModifier.Flat(TraitStat.ExtraProjectiles, 1f)),
                                        Combat(new CombatStatModifier(CombatStatType.WeaponDamage, 15f, WeaponCategory.Bow)) })),
        Set("Vampire Lord's Vestments", ItemPresets.ArmorMaterial.Cloth, new Color(0.55f, 0.05f, 0.1f), "Velvet and blood rubies.", "Vampire Lord's",
            new[] { Cloak, Amulet, Ring, Hands }, new[] { "Cape", "Blood Ruby", "Signet", "Gloves" }, 4,
            ("Thirst", "+15% dark resistance, -20% holy resistance",
                new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Dark, 15f), new ElementalResistance(ElementType.Holy, -20f) }) }),
            ("Blood Drinker", "Heal for 8% of the damage you deal",
                new EquipmentEffect[] { Behaviour(new LifeStealTrait()) })),
        Set("Thornguard", ItemPresets.ArmorMaterial.Iron, new Color(0.35f, 0.5f, 0.25f), "Iron grown over with thorny vines.", "Thornguard",
            new[] { Shoulders, Chest, Bracers, Shield }, new[] { "Spaulders", "Hauberk", "Bracers", "Spiked Shield" }, 4,
            ("Bramble Skin", "-20% slash and pierce damage",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.SlashDamageTaken, -20f), TraitModifier.Percent(TraitStat.PierceDamageTaken, -20f)) }),
            ("Thorns", "Attackers within 4 m take 20% of their damage back", new EquipmentEffect[] { Behaviour(new ThornsTrait()) })),
        Set("Elementalist's Charms", ItemPresets.ArmorMaterial.Mythril, new Color(0.95f, 0.6f, 0.2f), "Four charms, one per element.", "Elementalist's",
            new[] { Ring, Amulet, Trinket, Bracers }, new[] { "Ember Ring", "Frost Pendant", "Storm Charm", "Stone Bracers" }, 4,
            ("Balance", "+10% fire, ice, lightning and earth resistance",
                new EquipmentEffect[] { new CombatStatsEffect(null, new[] { new ElementalResistance(ElementType.Fire, 10f), new ElementalResistance(ElementType.Ice, 10f),
                                                                            new ElementalResistance(ElementType.Lightning, 10f), new ElementalResistance(ElementType.Earth, 10f) }) }),
            ("Elemental Lord", "+20% ability damage, +15% ability area",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.AbilityDamage, 20f), TraitModifier.Percent(TraitStat.AbilityArea, 15f)) })),
        Set("Pilgrim's Blessing", ItemPresets.ArmorMaterial.Cloth, new Color(0.95f, 0.9f, 0.75f), "Simple gifts carried on a long road.", "Pilgrim's",
            new[] { Amulet, Cloak, Belt, Feet }, new[] { "Holy Symbol", "Mantle", "Rope Belt", "Sandals" }, 4,
            ("Faith", "+15% healing received, +15% holy resistance",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.HealingReceived, 15f)), Ward(ElementType.Holy, 15f) }),
            ("Second Wind", "Below 25% health: heal 30% and 1 s invulnerability (every 90 s)", new EquipmentEffect[] { Behaviour(new SecondWindTrait()) })),
        Set("Executioner's Garb", ItemPresets.ArmorMaterial.Leather, new Color(0.2f, 0.15f, 0.15f), "Black hood and heavy gloves.", "Executioner's",
            new[] { Head, Hands, Bracers, Belt }, new[] { "Hood", "Gloves", "Bracers", "Belt" }, 4,
            ("Grim", "+10% critical damage", new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.CriticalDamage, 10f)) }),
            ("Clean Cut", "+20% axe and greatsword damage, +10% weapon damage",
                new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.WeaponDamage, 20f, WeaponCategory.Axe),
                                               new CombatStatModifier(CombatStatType.WeaponDamage, 20f, WeaponCategory.Greatsword)),
                                        Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 10f)) })),
        Set("Monk's Discipline", ItemPresets.ArmorMaterial.Cloth, new Color(0.95f, 0.6f, 0.25f), "Wraps, beads and a sash.", "Monk's",
            new[] { Hands, Bracers, Belt, Amulet, Feet }, new[] { "Hand Wraps", "Arm Wraps", "Sash", "Prayer Beads", "Sandals" }, 4,
            ("Inner Calm", "+1 stamina regeneration, -20% stamina cost",
                new EquipmentEffect[] { Stats(TraitModifier.Flat(TraitStat.StaminaRegen, 1f), TraitModifier.Percent(TraitStat.StaminaCost, -20f)) }),
            ("Flowing Fists", "+20% knuckle damage, +15% attack speed, +10% move speed",
                new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.WeaponDamage, 20f, WeaponCategory.Fist),
                                               new CombatStatModifier(CombatStatType.AttackSpeed, 15f)),
                                        Stats(TraitModifier.Percent(TraitStat.MoveSpeed, 10f)) })),
        Set("Miner's Gear", ItemPresets.ArmorMaterial.Leather, new Color(0.6f, 0.45f, 0.25f), "Tough gear from the deep mines.", "Miner's",
            new[] { Head, Hands, Belt, Feet }, new[] { "Lamp Helmet", "Work Gloves", "Tool Belt", "Steel-Toe Boots" }, 4,
            ("Hard Hat", "-30% fracture chance, -20% blunt damage",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.FractureChance, -30f), TraitModifier.Percent(TraitStat.BluntDamageTaken, -20f)) }),
            ("Deep Delver", "+25 carry weight, +15% earth resistance",
                new EquipmentEffect[] { Stats(TraitModifier.Flat(TraitStat.CarryWeight, 25f)), Ward(ElementType.Earth, 15f) })),
        Set("Storm Caller's Regalia", ItemPresets.ArmorMaterial.Chainmail, new Color(0.45f, 0.65f, 1f), "Copper-laced mail that crackles.", "Storm Caller's",
            new[] { Shoulders, Chest, Bracers, Ring, Amulet }, new[] { "Pauldrons", "Coat", "Coils", "Spark Ring", "Lightning Rod" }, 4,
            ("Grounded", "+25% lightning resistance", new EquipmentEffect[] { Ward(ElementType.Lightning, 25f) }),
            ("Eye Of The Storm", "+15% ability damage, +15% casting speed, +10% move speed",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.AbilityDamage, 15f), TraitModifier.Percent(TraitStat.MoveSpeed, 10f)),
                                        Combat(new CombatStatModifier(CombatStatType.CastingSpeed, 15f)) })),
        Set("Warlord's Trappings", ItemPresets.ArmorMaterial.Steel, new Color(0.7f, 0.55f, 0.2f), "Trophies of a hundred battles.", "Warlord's",
            new[] { Shoulders, Cloak, Belt, Trinket, Amulet, Bracers }, new[] { "Trophy Pauldrons", "War Banner", "Champion Belt", "War Horn", "Medal", "Vambraces" }, 4,
            ("Commanding", "+5 Strength, +5 Endurance",
                new EquipmentEffect[] { Combat(new CombatStatModifier(CombatStatType.Strength, 5f), new CombatStatModifier(CombatStatType.Endurance, 5f)) }),
            ("Warlord", "+15% weapon damage, +15% max health, 2% health per second after 6 s out of combat",
                new EquipmentEffect[] { Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 15f), TraitModifier.Percent(TraitStat.MaxHealth, 15f)),
                                        Behaviour(new OutOfCombatRegenTrait()) })),
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
        foreach (SetSpec spec in Sets().Concat(AccessorySets()).Concat(TieredSets()))
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
                int minimum = spec.tiers != null ? spec.tiers.Min(t => t.pieces) : 2;
                setSo.FindProperty("minimumPiecesForSet").intValue = minimum;
                setSo.FindProperty("maximumSetPieces").intValue = spec.Slots.Length;
                setSo.FindProperty("requiresAllPiecesToComplete").boolValue = true;
                setSo.ApplyModifiedPropertiesWithoutUndo();
                set.SetEffects.Clear();
                if (spec.tiers != null)
                {
                    foreach (TierSpec t in spec.tiers)
                        set.SetEffects.Add(Tier(t));
                    r.lines.Add($"Armor set: {spec.name}, {spec.Slots.Length} pieces (" +
                                string.Join("; ", spec.tiers.Select(t => $"{(t.pieces == spec.Slots.Length ? "full set" : t.pieces.ToString())}: {t.description}")) + ")");
                }
                else
                {
                    set.SetEffects.Add(Tier(2, spec.name, spec.two));
                    set.SetEffects.Add(Tier(spec.FullAt, spec.name, spec.four));
                    r.lines.Add($"Armor set: {spec.name} (2: {spec.two.description}; {spec.FullAt}: {spec.four.description})");
                }
                r.created++;
            }
            else
                r.skipped++;

            foreach (ArmorSlotType slot in spec.Slots)
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
        if (!string.IsNullOrEmpty(spec.prefix) && spec.words != null && spec.words.Length == spec.Slots.Length)
            return $"{spec.prefix} {spec.words[Array.IndexOf(spec.Slots, slot)]}";
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

    private static ArmorSetEffect Tier(TierSpec t) => new ArmorSetEffect
    {
        piecesRequired = t.pieces,
        effectName = $"{t.name} ({t.pieces})",
        effectDescription = t.description,
        upgradeGroup = t.group ?? "",
        effects = new List<EquipmentEffect>(t.effects),
    };

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

    /// <summary>Pieces that belong to no set: plain ones per material for the extra slots, and unique pieces with their own effects.</summary>
    private static A[] PieceList()
    {
        const ItemPresets.ArmorMaterial Cloth = ItemPresets.ArmorMaterial.Cloth, Leather = ItemPresets.ArmorMaterial.Leather,
            Chain = ItemPresets.ArmorMaterial.Chainmail, Iron = ItemPresets.ArmorMaterial.Iron, Steel = ItemPresets.ArmorMaterial.Steel,
            Mythril = ItemPresets.ArmorMaterial.Mythril;
        A P(string name, ArmorSlotType slot, ItemPresets.ArmorMaterial m, string description, params EquipmentEffect[] fx) =>
            new A { name = name, slot = slot, material = m, description = description, effects = fx.Length > 0 ? fx : null };
        var list = new List<A>
        {
            // Plain pieces (no set, no effect) for every material
            P("Padded Cap", Head, Cloth, "A quilted cap."),
            P("Padded Gambeson", Chest, Cloth, "Layers of quilted cloth."),
            P("Linen Trousers", Legs, Cloth, "Plain travelling trousers."),
            P("Cloth Shoes", Feet, Cloth, "Soft and quiet."),
            P("Studded Leather Cap", Head, Leather, "Leather with iron studs."),
            P("Studded Leather Armor", Chest, Leather, "Leather with iron studs."),
            P("Studded Leather Leggings", Legs, Leather, "Leather with iron studs."),
            P("Riding Boots", Feet, Leather, "Tall leather boots."),
            P("Leather Gloves", Hands, Leather, "Plain leather gloves."),
            P("Leather Shoulder Guards", Shoulders, Leather, "Hardened leather on the shoulders."),
            P("Leather Belt", Belt, Leather, "A plain belt with a buckle."),
            P("Travelling Cloak", Cloak, Cloth, "Keeps the rain off."),
            P("Mail Shirt", Chest, Chain, "A short shirt of mail."),
            P("Mail Mittens", Hands, Chain, "Mail that covers the hands."),
            P("Mail Mantle", Shoulders, Chain, "Mail over the shoulders."),
            P("Iron Kettle Hat", Head, Iron, "A wide-brimmed iron helmet."),
            P("Iron Brigandine", Chest, Iron, "Iron plates riveted inside cloth."),
            P("Iron Greaves", Legs, Iron, "Iron shin and thigh plates."),
            P("Iron Boots", Feet, Iron, "Heavy iron boots."),
            P("Iron Gauntlets", Hands, Iron, "Iron gauntlets."),
            P("Iron Girdle", Belt, Iron, "A belt of iron plates."),
            P("Steel Barbute", Head, Steel, "An open-faced steel helm."),
            P("Steel Breastplate", Chest, Steel, "A polished steel breastplate."),
            P("Steel Cuisses", Legs, Steel, "Steel thigh plates."),
            P("Steel Sabatons", Feet, Steel, "Steel foot plates."),
            P("Steel Vambraces", Bracers, Steel, "Steel forearm guards."),
            P("Mythril Circlet", Head, Mythril, "Weighs nothing, stops a sword."),
            P("Mythril Shirt", Chest, Mythril, "Light as silk, hard as dragon scale."),
            P("Mythril Greaves", Legs, Mythril, "Mythril leg guards."),
            P("Mythril Boots", Feet, Mythril, "Mythril boots."),
            P("Mythril Gauntlets", Hands, Mythril, "Mythril gauntlets."),
            P("Mythril Bracers", Bracers, Mythril, "Mythril bracers."),
            P("Mythril Belt", Belt, Mythril, "A belt of mythril links."),
            // Unique pieces
            P("Thief's Hood", Head, Leather, "+8% move speed, +3% critical chance.",
                Stats(TraitModifier.Percent(TraitStat.MoveSpeed, 8f)), Combat(new CombatStatModifier(CombatStatType.CriticalChance, 3f))),
            P("Miner's Helmet", Head, Iron, "-25% fracture chance.",
                Stats(TraitModifier.Percent(TraitStat.FractureChance, -25f))),
            P("Wizard's Hat", Head, Cloth, "+15% max mana, +10% ability damage.",
                Stats(TraitModifier.Percent(TraitStat.MaxMana, 15f), TraitModifier.Percent(TraitStat.AbilityDamage, 10f))),
            P("Horned Helm", Head, Iron, "+3 Strength, +10% knockback.",
                Combat(new CombatStatModifier(CombatStatType.Strength, 3f), new CombatStatModifier(CombatStatType.Knockback, 10f))),
            P("Crown Of Thorns", Head, Iron, "Attackers take damage back; +5% damage taken.",
                Behaviour(new ThornsTrait()), Stats(TraitModifier.Percent(TraitStat.DamageTaken, 5f))),
            P("Plague Mask", Head, Leather, "-50% infection chance, +20% poison resistance.",
                Stats(TraitModifier.Percent(TraitStat.InfectionChance, -50f)), Ward(ElementType.Poison, 20f)),
            P("Firewalker's Coat", Chest, Leather, "+30% fire resistance, -40% burn chance.",
                Ward(ElementType.Fire, 30f), Stats(TraitModifier.Percent(TraitStat.BurnChance, -40f))),
            P("Fur-Lined Parka", Chest, Leather, "+30% ice resistance.", Ward(ElementType.Ice, 30f)),
            P("Brigand's Vest", Chest, Leather, "+15 carry weight, +5% critical chance.",
                Stats(TraitModifier.Flat(TraitStat.CarryWeight, 15f)), Combat(new CombatStatModifier(CombatStatType.CriticalChance, 5f))),
            P("Cleric's Vestment", Chest, Cloth, "+20% ability healing, +15% holy resistance.",
                Stats(TraitModifier.Percent(TraitStat.AbilityHealing, 20f)), Ward(ElementType.Holy, 15f)),
            P("Juggernaut Plate", Chest, Steel, "-25% damage taken, -15% move speed.",
                Stats(TraitModifier.Percent(TraitStat.DamageTaken, -25f), TraitModifier.Percent(TraitStat.MoveSpeed, -15f))),
            P("Vampiric Cuirass", Chest, Chain, "Heal for 8% of the damage you deal.", Behaviour(new LifeStealTrait())),
            P("Acrobat's Leggings", Legs, Cloth, "+15% jump force, +5% move speed.",
                Stats(TraitModifier.Percent(TraitStat.JumpForce, 15f), TraitModifier.Percent(TraitStat.MoveSpeed, 5f))),
            P("Armored Tassets", Legs, Steel, "-50% limb loss chance.",
                Stats(TraitModifier.Percent(TraitStat.LimbLossChance, -50f))),
            P("Boots Of Leaping", Feet, Leather, "Double jump.", Behaviour(new DoubleJumpTrait())),
            P("Boots Of Speed", Feet, Leather, "+15% move speed, +10% sprint speed.",
                Stats(TraitModifier.Percent(TraitStat.MoveSpeed, 15f), TraitModifier.Percent(TraitStat.SprintSpeed, 10f))),
            P("Silent Slippers", Feet, Cloth, "+20% crouch speed.", Stats(TraitModifier.Percent(TraitStat.CrouchSpeed, 20f))),
            P("Climber's Boots", Feet, Leather, "Climb walls.", Behaviour(new WallClimbTrait())),
            P("Gauntlets Of Power", Hands, Steel, "+5 Strength, +10% weapon damage.",
                Combat(new CombatStatModifier(CombatStatType.Strength, 5f)), Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 10f))),
            P("Archer's Gloves", Hands, Leather, "+10% bow and crossbow damage, +10% charge speed.",
                Combat(new CombatStatModifier(CombatStatType.WeaponDamage, 10f, WeaponCategory.Bow), new CombatStatModifier(CombatStatType.WeaponDamage, 10f, WeaponCategory.Crossbow),
                       new CombatStatModifier(CombatStatType.ChargeSpeed, 10f))),
            P("Spellweaver Gloves", Hands, Cloth, "+15% casting speed, -10% ability cost.",
                Combat(new CombatStatModifier(CombatStatType.CastingSpeed, 15f)), Stats(TraitModifier.Percent(TraitStat.AbilityCost, -10f))),
            P("Surgeon's Gloves", Hands, Cloth, "+30% injury healing.", Stats(TraitModifier.Percent(TraitStat.InjuryHealing, 30f))),
            P("Spiked Pauldrons", Shoulders, Iron, "Attackers take damage back.", Behaviour(new ThornsTrait())),
            P("Mantle Of The Sage", Shoulders, Cloth, "+1 mana regeneration, -10% ability cooldowns.",
                Stats(TraitModifier.Flat(TraitStat.ManaRegen, 1f), TraitModifier.Percent(TraitStat.AbilityCooldown, -10f))),
            P("Wristblades", Bracers, Steel, "+10% dagger and fist damage, +5% attack speed.",
                Combat(new CombatStatModifier(CombatStatType.WeaponDamage, 10f, WeaponCategory.Dagger), new CombatStatModifier(CombatStatType.WeaponDamage, 10f, WeaponCategory.Fist),
                       new CombatStatModifier(CombatStatType.AttackSpeed, 5f))),
            P("Bracers Of Deflection", Bracers, Iron, "-20% pierce damage.", Stats(TraitModifier.Percent(TraitStat.PierceDamageTaken, -20f))),
            P("Feather Cloak", Cloak, Cloth, "Glide while holding Jump.", Behaviour(new GlideTrait())),
            P("Cloak Of The Phoenix", Cloak, Mythril, "Survive a deadly blow once in a while.", Behaviour(new CheatDeathTrait())),
            P("Bandolier", Belt, Leather, "+20 carry weight.", Stats(TraitModifier.Flat(TraitStat.CarryWeight, 20f))),
            P("Belt Of Second Wind", Belt, Leather, "Below 25% health: heal 30% (every 90 s).", Behaviour(new SecondWindTrait())),
            P("Bloodstone Amulet", Amulet, Mythril, "-30% bleeding.", Stats(TraitModifier.Percent(TraitStat.BleedingRate, -30f))),
            P("Ring Of Recovery", Ring, Steel, "Regenerate 2% health per second after 6 s out of combat.", Behaviour(new OutOfCombatRegenTrait())),

            // ---- more plain pieces
            P("Quilted Hood", Head, Cloth, "A warm quilted hood."),
            P("Monk's Robe", Chest, Cloth, "Simple and comfortable."),
            P("Silk Gloves", Hands, Cloth, "Fine silk gloves."),
            P("Linen Sash", Belt, Cloth, "A wide linen sash."),
            P("Hunter's Cap", Head, Leather, "A cap with a feather."),
            P("Hide Armor", Chest, Leather, "Thick tanned hide."),
            P("Hide Leggings", Legs, Leather, "Thick tanned hide."),
            P("Hide Boots", Feet, Leather, "Thick tanned hide."),
            P("Leather Bracers", Bracers, Leather, "Laced leather bracers."),
            P("Leather Cape", Cloak, Leather, "A short leather cape."),
            P("Mail Coif", Head, Chain, "A hood of mail."),
            P("Mail Leggings", Legs, Chain, "Mail chausses."),
            P("Mail Boots", Feet, Chain, "Mail-covered boots."),
            P("Mail Bracers", Bracers, Chain, "Mail sleeves."),
            P("Iron Sallet", Head, Iron, "A sweeping iron helmet."),
            P("Iron Pauldron Set", Shoulders, Iron, "Matched iron pauldrons."),
            P("Iron Vambraces", Bracers, Iron, "Iron forearm guards."),
            P("Steel Armet", Head, Steel, "A close steel helm."),
            P("Steel Gauntlets", Hands, Steel, "Articulated steel gauntlets."),
            P("Steel Spaulders", Shoulders, Steel, "Layered steel spaulders."),
            P("Steel War Belt", Belt, Steel, "A belt of steel plates."),
            P("Mythril Cowl", Head, Mythril, "A hood of mythril links."),
            P("Mythril Pauldrons", Shoulders, Mythril, "Light mythril pauldrons."),
            P("Mythril Cloak", Cloak, Mythril, "Woven mythril thread."),

            // ---- more unique pieces
            P("Berserker's Mask", Head, Leather, "+15% weapon damage, -10% max health.",
                Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 15f), TraitModifier.Percent(TraitStat.MaxHealth, -10f))),
            P("Sage's Circlet", Head, Mythril, "+20% max mana, +1 mana regeneration.",
                Stats(TraitModifier.Percent(TraitStat.MaxMana, 20f), TraitModifier.Flat(TraitStat.ManaRegen, 1f))),
            P("Watcher's Helm", Head, Steel, "-30% control taken.", Stats(TraitModifier.Percent(TraitStat.ControlTaken, -30f))),
            P("Gladiator's Breastplate", Chest, Steel, "+10% weapon damage, +10% attack speed in combat.",
                Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 10f)),
                While(WhileConditionEffect.Condition.InCombat, 0f, Combat(new CombatStatModifier(CombatStatType.AttackSpeed, 10f)))),
            P("Stormweave Robe", Chest, Cloth, "+25% lightning resistance; 15% of ability hits stun for 0.4 s.",
                Ward(ElementType.Lightning, 25f),
                new OnHitEffect { trigger = ProcTrigger.AbilityHits, chance = 0.15f, cooldown = 2f, effects = new List<AbilityEffect> { new StunEffect { duration = 0.4f } } }),
            P("Bramble Mail", Chest, Chain, "Attackers in melee are slowed.", WhenHit(1f, 1f, new SlowEffect { slowAmount = 0.3f, duration = 1.5f })),
            P("Last Stand Plate", Chest, Iron, "Below 30% health: -35% damage taken.",
                While(WhileConditionEffect.Condition.HealthBelow, 0.3f, Stats(TraitModifier.Percent(TraitStat.DamageTaken, -35f)))),
            P("Runner's Leggings", Legs, Leather, "+10% sprint speed, -15% stamina cost.",
                Stats(TraitModifier.Percent(TraitStat.SprintSpeed, 10f), TraitModifier.Percent(TraitStat.StaminaCost, -15f))),
            P("Frostbound Greaves", Legs, Iron, "+20% ice resistance; attackers are chilled (slowed).",
                Ward(ElementType.Ice, 20f), WhenHit(0.5f, 1f, new SlowEffect { slowAmount = 0.25f, duration = 2f })),
            P("Venomous Gloves", Hands, Leather, "25% of weapon hits poison.", OnHit(0.25f, 0f, Dot(ElementType.Poison, 2f, 4f))),
            P("Exhausting Gauntlets", Hands, Iron, "Weapon hits drain 8 stamina.", OnHit(1f, 0f, Exhaust(8f))),
            P("Fencer's Bracers", Bracers, Leather, "+6% critical chance, +10% critical damage.",
                Combat(new CombatStatModifier(CombatStatType.CriticalChance, 6f), new CombatStatModifier(CombatStatType.CriticalDamage, 10f))),
            P("Battlemage Bracers", Bracers, Chain, "+10% casting speed, +10% ability damage.",
                Combat(new CombatStatModifier(CombatStatType.CastingSpeed, 10f)), Stats(TraitModifier.Percent(TraitStat.AbilityDamage, 10f))),
            P("Warlord's Pauldrons", Shoulders, Steel, "+5 Strength, +10% knockback.",
                Combat(new CombatStatModifier(CombatStatType.Strength, 5f), new CombatStatModifier(CombatStatType.Knockback, 10f))),
            P("Healer's Mantle", Shoulders, Cloth, "+25% ability healing.", Stats(TraitModifier.Percent(TraitStat.AbilityHealing, 25f))),
            P("Burning Aura Cloak", Cloak, Leather, "In combat, enemies within 3 m burn for 4 every 2 s.",
                Pulse(2f, 3f, TargetFilter.Enemies, true, new DamageEffect { amount = 4f, element = ElementType.Fire })),
            P("Cloak Of Mending", Cloak, Cloth, "Heals you and allies within 5 m for 3 every 4 s.",
                Pulse(4f, 5f, TargetFilter.Allies | TargetFilter.Self, false, new HealEffect { amount = 3f })),
            P("Assassin's Belt", Belt, Leather, "+20% dagger damage.", Combat(new CombatStatModifier(CombatStatType.WeaponDamage, 20f, WeaponCategory.Dagger))),
            P("Ring Of Fury", Ring, Iron, "Below 50% health: +15% attack speed.",
                While(WhileConditionEffect.Condition.HealthBelow, 0.5f, Combat(new CombatStatModifier(CombatStatType.AttackSpeed, 15f)))),
            P("Ring Of Sparks", Ring, Steel, "10% of weapon hits deal 8 lightning damage.", OnHit(0.1f, 0f, new DamageEffect { amount = 8f, element = ElementType.Lightning })),
            P("Amulet Of Thorns", Amulet, Iron, "Attackers take damage back.", Behaviour(new ThornsTrait())),
            P("Hourglass Pendant", Amulet, Mythril, "-15% ability cooldowns.", Stats(TraitModifier.Percent(TraitStat.AbilityCooldown, -15f))),
            P("Rabbit's Foot", Trinket, Leather, "+4% critical chance, +5% move speed.",
                Crit(4f), Stats(TraitModifier.Percent(TraitStat.MoveSpeed, 5f))),
            P("War Drum", Trinket, Leather, "In combat: +10% weapon damage.",
                While(WhileConditionEffect.Condition.InCombat, 0f, Stats(TraitModifier.Percent(TraitStat.WeaponDamage, 10f)))),

            // ---- items with powers while worn
            P("Belt Of The Titan", Belt, Steel, "You are 15% bigger: more health, strength and knockback.",
                new PowerWhileWornEffect("Titan's Belt", new SizeChangePower { sizePercent = 15f, growSeconds = 0.5f })),
            P("Giant's Gauntlets", Hands, Iron, "You are 10% bigger and hit harder.",
                new PowerWhileWornEffect("Giant's Gauntlets", new SizeChangePower { sizePercent = 10f, weaponDamagePerSize = 1f })),
            P("Pixie Ring", Ring, Mythril, "You are 20% smaller and 15% faster, but take 10% more damage.",
                new PowerWhileWornEffect("Pixie Ring", new SizeChangePower { sizePercent = -20f, healthPerSize = 0f, weaponDamagePerSize = 0f, knockbackPerSize = 0f,
                    strengthPerSize = 0f, jumpPerSize = 0f, moveSpeedPercent = 15f, damageTakenPercent = 10f })),
            P("Boots Of The Sky", Feet, Mythril, "Walk on air: 10 stamina per second in the air.",
                new PowerWhileWornEffect("Boots Of The Sky", new MovementPower { behaviour = new SkyWalkTrait { cost = SkyWalkCost.Stamina, costPerSecond = 10f, endWhenEmpty = false } })),
            P("Anchor Pendant", Amulet, Iron, "Immune to knockback and pulls.",
                new PowerWhileWornEffect("Anchor", new ControlImmunityPower { immuneTo = ControlImmunity.AllDisplacement, cleanseOnStart = false })),
            P("Ring Of The Unbound", Ring, Mythril, "Immune to roots and slows.",
                new PowerWhileWornEffect("Unbound", new ControlImmunityPower { immuneTo = ControlImmunity.Root | ControlImmunity.Slow, cleanseOnStart = false })),
            P("Helm Of The Unyielding", Head, Steel, "Immune to stuns; -10% move speed.",
                new PowerWhileWornEffect("Unyielding", new ControlImmunityPower { immuneTo = ControlImmunity.Stun, cleanseOnStart = false }),
                Stats(TraitModifier.Percent(TraitStat.MoveSpeed, -10f))),

            // ---- items with abilities (cast with the item's key) and fate/trick passives
            P("Corpse Golem Essence", Trinket, Leather, "Acid Blood: melee attackers are splashed (poison, corroded gear; yours too). Acid Blood Burst on its key.",
                Key("Acid Blood Burst"), Behaviour(new AcidBloodTrait()), Ward(ElementType.Poison, 20f)),
            P("Gambler's Coin", Trinket, Steel, "Lucky Coin on its key: a random stat goes up or down for 20 s.", Key("Lucky Coin")),
            P("Philosopher's Stone", Amulet, Mythril, "Equivalent Exchange on its key: one stat up, another down.", Key("Equivalent Exchange")),
            P("Pickpocket's Gloves", Hands, Leather, "Heist on its key: steal a random item from a player; +3% critical chance.", Key("Heist"), Crit(3f)),
            P("Gate Stone", Trinket, Mythril, "Twin Gates on its key: two linked portals anyone can use.", Key("Twin Gates")),
            P("Fang Of The Leech", Ring, Iron, "Leech Life on its key: drain a target's health into yours.", Key("Leech Life")),
            P("Debt Collector's Ledger", Trinket, Leather, "Incoming Debt on its key: the target's next ability costs double or recharges twice as slow.", Key("Incoming Debt")),
            P("Soulbinder Chain", Amulet, Iron, "Soul Link on its key: share the damage with a target, both ways.", Key("Soul Link")),
            P("Hourglass Of Return", Trinket, Mythril, "Chronal Recall on its key: return to where you were 4 s ago.", Key("Chronal Recall")),
            P("Fate's Promise", Amulet, Mythril, "Once per fight or duel, a killing blow is ignored (fate collects: -20% damage for 15 s).", Behaviour(new FatesDebtTrait())),
            P("Phantom Cloak", Cloak, Cloth, "Phantom Exchange on its key: leave a phantom and swap places with it.", Key("Phantom Exchange")),
            P("Gravity Gauntlets", Hands, Iron, "Gravital Pull on its key: drag every enemy within 10 m to you.", Key("Gravital Pull")),
            P("Giant's Heart", Amulet, Steel, "Gigantification on its key: 80% bigger for 15 s.", Key("Gigantification")),
            P("Shrinking Charm", Trinket, Cloth, "Minimization on its key: half size, faster, more fragile.", Key("Minimization")),
            P("Mirage Pendant", Amulet, Mythril, "Greater Mirage on its key: walking copies of you.", Key("Greater Mirage")),
            P("Vanishing Cloak", Cloak, Cloth, "Vanish on its key: invisible until you attack or are hit.", Key("Vanish")),
            P("Anchor Boots", Feet, Iron, "Immovable Force on its key: immune to every crowd control for 8 s.", Key("Immovable Force")),
            P("Mana Burner's Ring", Ring, Steel, "Mana Burn on its key.", Key("Mana Burn")),
            P("Blood Pact Dagger Charm", Trinket, Iron, "Blood Pact on its key: trade your health for your allies'.", Key("Blood Pact")),
            new A { name = "Mirror Shield", slot = ArmorSlotType.Shield, material = ItemPresets.ArmorMaterial.Steel, shield = ShieldDefense.Kite,
                    description = "Mirror Ward on its key: for 5 s, half of every hit goes back to the attacker.", effects = new EquipmentEffect[] { Key("Mirror Ward") } },
            new A { name = "Gravity Well Orb", slot = ArmorSlotType.Trinket, material = ItemPresets.ArmorMaterial.Mythril,
                    description = "Gravity Well on its key.", effects = new EquipmentEffect[] { Key("Gravity Well") } },
            new A { name = "Twin Mirror", slot = ArmorSlotType.Trinket, material = ItemPresets.ArmorMaterial.Mythril,
                    description = "Mutual Exchange on its key: swap a stat with the target.", effects = new EquipmentEffect[] { Key("Mutual Exchange") } },
            new A { name = "Displacer Ring", slot = ArmorSlotType.Ring, material = ItemPresets.ArmorMaterial.Mythril,
                    description = "Transposition on its key: swap places with the target.", effects = new EquipmentEffect[] { Key("Transposition") } },
        };
        return list.ToArray();
    }

    private static void Accessories(Report r)
    {
        MakeArmor(r, $"{SMAssets.Armor}/{Sub}/Shields and Accessories", AccessoryList());
        MakeArmor(r, $"{SMAssets.Armor}/{Sub}/Armor Pieces", PieceList());
    }

    private static void MakeArmor(Report r, string path0, A[] items)
    {
        string folder = SMAssetsEditor.Folder(path0);
        foreach (A a in items)
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
