using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The armoury and the wardrobe: the rest of the built-in designs - more blades, hafted weapons, polearms, bows,
/// thrown weapons and tools; shields; plate, leather and cloth armour; everyday clothes - and the patterns, fibres,
/// cloths and woods they are made of. Each one gets a template item from Create Design Templates.
/// </summary>
public static partial class CraftDefaults
{
    // ------------------------------------------------------------------ materials
    private static List<CraftPattern> ArmouryPatterns() => new List<CraftPattern>
    {
        // Leather.
        Pattern("cuirass_panel", "Cuirass panel", 36, ".XXXXX.", "XXXXXXX", "XXXXXXX", "XXXXXXX", "XXXXXXX", ".XXXXX."),
        Pattern("chaps_leg", "Leg panel", 24, "XXX", "XXX", "XXX", "XXX", "XX.", "XX.", "XX.", "XX."),
        Pattern("pauldron", "Shoulder cop", 16, ".XXX.", "XXXXX", "XXXXX"),
        Pattern("tall_boot", "Tall boot upper", 30, "XXX", "XXX", "XXX", "XXX", "XXX", "XXX", "XX."),
        Pattern("sandal_strap", "Sandal strap", 6, "XXXXX"),
        Pattern("facing", "Shield facing", 20, ".XXX.", "XXXXX", "XXXXX", "XXXXX", ".XXX."),
        // Cloth.
        Pattern("tunic_body", "Tunic body", 34, ".XXXX.", "XXXXXX", "XXXXXX", "XXXXXX", "XXXXXX", "XXXXXX"),
        Pattern("sleeve", "Sleeve", 18, "XXX", "XXX", "XXX", "XX.", "XX."),
        Pattern("shirt_body", "Shirt body", 28, ".XXX.", "XXXXX", "XXXXX", "XXXXX", "XXXXX"),
        Pattern("coif", "Coif side", 14, ".XX.", "XXXX", "XXXX", "XX.."),
        Pattern("mitten", "Mitten", 16, "XX", "XX", "X."),
        Pattern("tabard_panel", "Tabard panel", 30, "XXXX", "XXXX", "XXXX", "XXXX", "XXXX", "XXXX", "XXXX", "XXXX"),
        Pattern("sash", "Sash", 8, "XXXXXXXXXXXX"),
        Pattern("foot_wrap", "Foot wrap", 8, "XXXXXX", "XXXXXX"),
        Pattern("robe_panel", "Robe panel", 44, ".XXXX.", "XXXXXX", "XXXXXX", "XXXXXX", "XXXXXX", "XXXXXX", "XXXXXX", "XXXXXX", "XXXXXX", "XXXXXX"),
        Pattern("hat_brim", "Hat brim", 16, "XXXX", "X..X", "X..X", "XXXX"),
        Pattern("cape", "Cape", 20, ".XXXXX.", "XXXXXXX", "XXXXXXX", "XXXXXXX", "XXXXXXX"),
    };

    private static List<CraftSupplyDefinition> ArmourySupplies() => new List<CraftSupplyDefinition>
    {
        Spinnable(Supply("hemp_fibre", "Hemp fibre", CraftSupplyKind.Fibre, "Hemp Fibre", 1, 0.4f, "Retted, scutched hemp: cord and canvas."), "coarse_hemp_cord", "hemp_cord", "hemp_twine"),
        Spinnable(Supply("cotton_fibre", "Cotton boll", CraftSupplyKind.Fibre, "Cotton Boll", 1, 0.6f, "Ginned cotton, soft and short-stapled."), "coarse_cotton_yarn", "cotton_thread", "fine_cotton_thread"),
        Spinnable(Supply("silk_floss", "Silk floss", CraftSupplyKind.Fibre, "Silk Floss", 1, 3, "Degummed silk from the cocoons: fine and very strong."), "raw_silk_yarn", "silk_yarn", "silk_thread"),
        Supply("coarse_hemp_cord", "Rough hemp cord", CraftSupplyKind.Thread, "Rough Hemp Cord", 0.9f, 0.3f, "Hairy and uneven, but hemp is strong."),
        Supply("hemp_cord", "Hemp cord", CraftSupplyKind.Thread, "Hemp Cord", 1.2f, 0.6f, "Strong cord: bowstrings, lacing, sail seams."),
        Supply("hemp_twine", "Hemp twine", CraftSupplyKind.Thread, "Hemp Twine", 1.35f, 1, "Fine, hard-twisted hemp: the best cheap bowstring."),
        Supply("coarse_cotton_yarn", "Slubby cotton yarn", CraftSupplyKind.Thread, "Slubby Cotton Yarn", 0.5f, 0.4f, "Soft and weak where it is thin."),
        Supply("cotton_thread", "Cotton thread", CraftSupplyKind.Thread, "Cotton Thread", 0.8f, 0.8f, "Soft, even sewing thread."),
        Supply("fine_cotton_thread", "Fine cotton thread", CraftSupplyKind.Thread, "Fine Cotton Thread", 0.95f, 1.4f, "Fine and smooth: shirts and linings."),
        Supply("raw_silk_yarn", "Raw silk yarn", CraftSupplyKind.Thread, "Raw Silk Yarn", 1.2f, 3, "Uneven silk, still stronger than most thread."),
        Supply("silk_yarn", "Silk yarn", CraftSupplyKind.Thread, "Silk Yarn", 1.4f, 4.5f, "Even silk yarn."),
        Supply("horsehair", "Horsehair", CraftSupplyKind.Thread, "Horsehair", 1.1f, 1, "Plaited horsehair: bowstrings and stiff stitching."),
        Supply("felt_padding", "Felt padding", CraftSupplyKind.Padding, "Felt Padding", 1.15f, 3, "Thick fulled wool: the best padding under a helm."),
    };

    private static List<CraftHideDefinition> ArmouryCloths()
    {
        var list = new List<CraftHideDefinition>
        {
            Hide("silk_cloth", "Silk cloth", 12, 18, 0.3f, 0.3f, 0.75f, 0.95f, 0f, 0f, 0.008f, 30, "Light, smooth and strong: robes, fine shirts, the lining of a lord's coat."),
            Hide("cotton_cloth", "Cotton cloth", 14, 20, 0.6f, 0.6f, 0.45f, 0.9f, 0f, 0f, 0.01f, 5, "Soft and cool: shirts, tunics, quilting."),
            Hide("felt", "Felt", 12, 14, 3f, 3f, 0.4f, 0.6f, 0f, 0f, 0.02f, 9, "Thick, fulled wool that does not fray: hats, padding, boot liners."),
            Hide("velvet", "Velvet", 12, 16, 1.4f, 1.4f, 0.5f, 0.85f, 0f, 0f, 0.012f, 40, "A rich, napped cloth: surcoats and capes for those who can pay."),
        };
        foreach (CraftHideDefinition c in list) c.textile = true;
        list[0].color = new Color(0.92f, 0.9f, 0.84f);
        list[1].color = new Color(0.95f, 0.94f, 0.9f);
        list[2].color = new Color(0.45f, 0.4f, 0.36f);
        list[3].color = new Color(0.42f, 0.1f, 0.16f);
        return list;
    }

    private static List<CraftWoodDefinition> ArmouryWoods() => new List<CraftWoodDefinition>
    {
        Wood("yew", "Yew", 0.67f, 0.55f, 0.8f, 0.9f, new Color(0.78f, 0.5f, 0.32f), 3, "Heartwood that resists, sapwood that pulls: the bow wood.", 1900, 40, 40),
        Wood("elm", "Elm", 0.6f, 0.45f, 0.75f, 0.45f, new Color(0.7f, 0.55f, 0.4f), 1, "Interlocked grain that will not split: shield boards, mallets, clubs."),
        Wood("walnut", "Walnut", 0.64f, 0.55f, 0.65f, 0.4f, new Color(0.42f, 0.3f, 0.2f), 2.5f, "Dark and fine: grips and stocks that look rich."),
        Wood("birch", "Birch", 0.65f, 0.5f, 0.55f, 0.45f, new Color(0.9f, 0.84f, 0.7f), 0.8f, "Cheap and even: handles and staves."),
        Wood("pine", "Pine plank", 0.48f, 0.25f, 0.35f, 0.5f, new Color(0.9f, 0.78f, 0.55f), 0.5f, "Light and soft: cheap boards; it dents.", 800, 300, 20),
    };

    // ------------------------------------------------------------------ designs
    private static void Stats(CraftDesign d, float min, float max, float crit, float critMul, float knock, float refMass, float balance, int durability, float price, int gx, int gy)
    {
        d.damageMin = min; d.damageMax = max; d.critChance = crit; d.critMultiplier = critMul; d.knockback = knock;
        d.referenceMass = refMass; d.idealBalance = balance; d.durability = durability; d.price = price; d.gridSize = new Vector2Int(gx, gy);
    }

    private static CraftDesign Garment(string id, string name, ArmorSlotType slot, string description, float defense, int durability, float price, int gx, int gy,
        CraftComponentSpec[] parts, params CraftJointSpec[] joints)
    {
        var d = new CraftDesign
        {
            id = id, displayName = name, kind = CraftDesignKind.Armor, armorSlot = slot, description = description,
            defense = defense, durability = durability, price = price, gridSize = new Vector2Int(gx, gy),
        };
        d.components.AddRange(parts);
        d.joints.AddRange(joints);
        return d;
    }

    private static CraftShapeTarget Plate(float lmin, float lmax, float mmin, float mmax, float dish, params (float t, float w, float th)[] points)
    {
        CraftShapeTarget s = Shape(lmin, lmax, mmin, mmax, points);
        s.dish = dish;
        return s;
    }

    private static CraftComponentSpec Thread(int count = 1) => SupplyPart("thread", "Thread", CraftSupplyKind.Thread, count, CraftComponentFunction.Thread);

    private static void AddArmoury(List<CraftDesign> list)
    {
        CraftDesign d;
        CraftShapeTarget blade, head;

        // ---- more blades
        blade = Shape(520, 640, 450, 850, (0, 13, 6), (0.16f, 13, 6), (0.17f, 44, 6), (0.6f, 38, 5), (1, 6, 3));
        blade.edgeFrom = 0.18f; blade.edgeAngle = 28; blade.tangTo = 0.16f;
        d = Hilted("shortsword", "Shortsword", WeaponCategory.Sword, DamageForm.Pierce, WeaponScaling.Agility, blade,
            Shape(140, 190, 110, 260, (0, 18, 11), (1, 18, 11)), 90, 110, 24, 32, Shape(35, 55, 120, 280, (0, 40, 34), (1, 40, 34)),
            "A short, broad double-edged sword: quick thrusts in close.");
        Stats(d, 8, 12, 0.09f, 1.6f, 0.35f, 800, 80, 260, 80, 1, 3);
        list.Add(d);

        blade = Shape(950, 1100, 1100, 1600, (0, 15, 7), (0.2f, 15, 7), (0.21f, 48, 6.5f), (0.55f, 42, 5.5f), (0.92f, 30, 4), (1, 8, 3));
        blade.edgeFrom = 0.22f; blade.edgeAngle = 30; blade.tangTo = 0.2f;
        d = Hilted("longsword", "Longsword", WeaponCategory.Sword, DamageForm.Slash, WeaponScaling.Strength, blade,
            Shape(220, 280, 200, 400, (0, 22, 13), (1, 22, 13)), 180, 240, 26, 34, Shape(45, 65, 250, 450, (0, 48, 40), (1, 48, 40)),
            "A hand-and-a-half sword: one hand or two, cut and thrust.");
        Stats(d, 13, 18, 0.07f, 1.7f, 0.8f, 1450, 130, 320, 170, 1, 5);
        d.joints[3].count = 14;
        list.Add(d);

        blade = Shape(780, 900, 750, 1150, (0, 13, 7), (0.12f, 13, 7), (0.13f, 36, 6.5f), (0.6f, 34, 5), (0.9f, 30, 3.5f), (1, 10, 2.5f));
        blade.edgeFrom = 0.14f; blade.edgeAngle = 26; blade.tangTo = 0.12f;
        d = Hilted("sabre", "Sabre", WeaponCategory.Sword, DamageForm.Slash, WeaponScaling.Agility, blade,
            Shape(100, 140, 100, 220, (0, 18, 10), (1, 18, 10)), 95, 115, 24, 30, Shape(25, 40, 60, 160, (0, 30, 26), (1, 30, 26)),
            "A single-edged cavalry sword: long, drawing cuts.");
        blade.edge = CraftEdgeKind.Single;
        Stats(d, 10, 14, 0.1f, 1.7f, 0.4f, 950, 140, 270, 130, 1, 4);
        list.Add(d);

        blade = Shape(650, 780, 900, 1300, (0, 14, 7), (0.15f, 14, 7), (0.16f, 40, 6), (0.7f, 58, 5), (0.95f, 50, 4), (1, 14, 3));
        blade.edgeFrom = 0.17f; blade.edgeAngle = 30; blade.tangTo = 0.15f;
        d = Hilted("falchion", "Falchion", WeaponCategory.Sword, DamageForm.Slash, WeaponScaling.Strength, blade,
            Shape(150, 200, 120, 260, (0, 18, 11), (1, 18, 11)), 95, 115, 26, 32, Shape(35, 50, 120, 260, (0, 38, 32), (1, 38, 32)),
            "A heavy single-edged blade that widens toward the tip: it chops like an axe.");
        blade.edge = CraftEdgeKind.Single;
        Stats(d, 11, 16, 0.06f, 1.7f, 0.7f, 1100, 190, 280, 110, 1, 4);
        list.Add(d);

        blade = Shape(950, 1100, 500, 850, (0, 12, 7), (0.12f, 12, 7), (0.13f, 24, 6), (0.5f, 16, 5), (1, 4, 3));
        blade.edgeFrom = 0.14f; blade.edgeAngle = 24; blade.tangTo = 0.12f;
        d = Hilted("rapier", "Rapier", WeaponCategory.Sword, DamageForm.Pierce, WeaponScaling.Agility, blade,
            Shape(160, 220, 150, 320, (0, 16, 12), (1, 16, 12)), 95, 120, 24, 30, Shape(40, 55, 180, 360, (0, 44, 38), (1, 44, 38)),
            "A long, narrow thrusting blade with a heavy hilt to balance it.");
        Stats(d, 9, 13, 0.14f, 1.9f, 0.2f, 1000, 90, 220, 160, 1, 5);
        list.Add(d);

        blade = Shape(280, 380, 220, 450, (0, 14, 7), (0.3f, 14, 7), (0.31f, 36, 7), (0.8f, 32, 5), (1, 6, 3));
        blade.edgeFrom = 0.33f; blade.edgeAngle = 26; blade.tangTo = 0.3f;
        d = Hilted("seax", "Seax", WeaponCategory.Dagger, DamageForm.Slash, WeaponScaling.Agility, blade,
            Shape(40, 70, 20, 70, (0, 14, 8), (1, 14, 8)), 100, 130, 26, 32, Shape(20, 35, 30, 100, (0, 28, 24), (1, 28, 24)),
            "A big single-edged knife with a thick spine: tool and weapon.");
        blade.edge = CraftEdgeKind.Single;
        Stats(d, 7, 11, 0.1f, 1.7f, 0.25f, 380, 40, 230, 45, 1, 2);
        d.joints[3].count = 8;
        list.Add(d);

        blade = Shape(200, 280, 80, 200, (0, 10, 6), (0.35f, 10, 6), (0.36f, 14, 9), (1, 3, 3));
        blade.edgeFrom = 0.4f; blade.edgeAngle = 40; blade.tangTo = 0.35f;
        d = Hilted("stiletto", "Stiletto", WeaponCategory.Dagger, DamageForm.Pierce, WeaponScaling.Agility, blade,
            Shape(60, 90, 25, 80, (0, 12, 8), (1, 12, 8)), 85, 105, 20, 26, Shape(20, 35, 30, 90, (0, 26, 22), (1, 26, 22)),
            "A slim, stiff spike of a blade: it finds the gaps in armour.");
        Stats(d, 6, 9, 0.2f, 2f, 0.1f, 220, 20, 200, 40, 1, 2);
        d.joints[3].count = 8;
        list.Add(d);

        // ---- hafted weapons
        head = Shape(120, 170, 600, 1000, (0, 32, 32), (0.4f, 32, 32), (0.5f, 26, 20), (0.75f, 14, 10), (1, 6, 6));
        head.eye = true; head.eyeAt = 0.45f; head.eyeDiameter = 28; head.hardened = true;
        d = Hafted("war_hammer", "War hammer", CraftDesignKind.Weapon, WeaponCategory.Hammer, DamageForm.Blunt, Metal("head", "Hammer head", CraftComponentFunction.Striking, head),
            WoodPart("haft", "Haft", 550, 750, 28, 36, 0.6f), CraftJointKind.Wedge, "A hammer face on one side and a beak on the other: it dents plate and punches through it.");
        Stats(d, 12, 17, 0.05f, 1.6f, 1.2f, 900, 420, 360, 95, 1, 3);
        list.Add(d);

        head = Shape(180, 240, 2200, 3800, (0, 70, 70), (1, 70, 70));
        head.eye = true; head.eyeAt = 0.5f; head.eyeDiameter = 34;
        d = Hafted("maul", "Maul", CraftDesignKind.Weapon, WeaponCategory.Hammer, DamageForm.Blunt, Metal("head", "Maul head", CraftComponentFunction.Striking, head),
            WoodPart("haft", "Haft", 850, 1000, 34, 42, 0.7f), CraftJointKind.Wedge, "A great two-handed hammer: slow, and nothing stands up to it.");
        Stats(d, 20, 28, 0.03f, 1.5f, 2f, 3500, 700, 420, 120, 2, 5);
        list.Add(d);

        head = Shape(100, 140, 900, 1500, (0, 30, 30), (0.2f, 70, 70), (0.8f, 70, 70), (1, 30, 30));
        head.eye = true; head.eyeAt = 0.5f; head.eyeDiameter = 28;
        d = Hafted("flanged_mace", "Flanged mace", CraftDesignKind.Weapon, WeaponCategory.Mace, DamageForm.Blunt, Metal("head", "Flanged head", CraftComponentFunction.Striking, head),
            WoodPart("haft", "Haft", 500, 650, 30, 36, 0.6f), CraftJointKind.Wedge, "A head of iron flanges that bite into armour instead of glancing off.");
        Stats(d, 13, 18, 0.05f, 1.5f, 1.5f, 1050, 380, 380, 110, 1, 3);
        list.Add(d);

        head = Shape(80, 110, 800, 1300, (0, 64, 64), (1, 64, 64));
        head.eye = true; head.eyeAt = 0.5f; head.eyeDiameter = 28;
        d = Hafted("morning_star", "Morning star", CraftDesignKind.Weapon, WeaponCategory.Mace, DamageForm.Blunt, Metal("head", "Ball head", CraftComponentFunction.Striking, head),
            WoodPart("haft", "Haft", 550, 700, 30, 38, 0.6f), CraftJointKind.Wedge, "An iron ball set with spikes on a stout haft.");
        d.components.Add(Fasteners("spikes", "Spikes", CraftFastenerKind.Stud, 8));
        d.joints.Add(Joint("spikes", "Spikes set", CraftJointKind.Rivet, "head", "spikes", 8, 0.8f));
        Stats(d, 14, 19, 0.06f, 1.6f, 1.3f, 1100, 420, 340, 115, 1, 3);
        list.Add(d);

        head = Shape(200, 260, 900, 1500, (0, 36, 28), (0.25f, 36, 28), (0.4f, 24, 10), (0.7f, 90, 6), (1, 220, 4));
        head.edge = CraftEdgeKind.Single; head.edgeFrom = 0.7f; head.edgeAngle = 32; head.eye = true; head.eyeAt = 0.12f; head.eyeDiameter = 32; head.hardened = true;
        d = Hafted("dane_axe", "Dane axe", CraftDesignKind.Weapon, WeaponCategory.Axe, DamageForm.Slash, Metal("head", "Broad axe head", CraftComponentFunction.Edge, head),
            WoodPart("haft", "Long haft", 1100, 1400, 32, 40, 0.65f), CraftJointKind.Wedge, "A thin, wide axe head on a long two-handed haft.");
        Stats(d, 18, 25, 0.05f, 1.8f, 1.4f, 1600, 900, 300, 140, 2, 5);
        list.Add(d);

        head = Shape(110, 150, 350, 650, (0, 28, 22), (0.3f, 28, 22), (0.5f, 20, 8), (1, 80, 4));
        head.edge = CraftEdgeKind.Single; head.edgeFrom = 0.7f; head.edgeAngle = 30; head.eye = true; head.eyeAt = 0.15f; head.eyeDiameter = 24; head.hardened = true;
        d = Hafted("throwing_axe", "Throwing axe", CraftDesignKind.Weapon, WeaponCategory.Thrown, DamageForm.Slash, Metal("head", "Axe head", CraftComponentFunction.Edge, head),
            WoodPart("haft", "Short haft", 320, 420, 24, 30, 0.55f), CraftJointKind.Wedge, "A light axe balanced to turn once in the air.");
        d.scaling = WeaponScaling.Agility;
        Stats(d, 9, 13, 0.08f, 1.6f, 0.4f, 600, 220, 200, 40, 1, 2);
        list.Add(d);

        // ---- polearms
        head = Shape(350, 450, 1600, 2600, (0, 30, 26), (0.2f, 30, 26), (0.25f, 180, 6), (0.55f, 150, 5), (0.6f, 40, 10), (1, 8, 6));
        head.edge = CraftEdgeKind.Single; head.edgeFrom = 0.25f; head.edgeAngle = 32; head.eye = true; head.eyeAt = 0.1f; head.eyeDiameter = 30; head.hardened = true;
        d = Hafted("halberd", "Halberd", CraftDesignKind.Weapon, WeaponCategory.Spear, DamageForm.Slash, Metal("head", "Halberd head", CraftComponentFunction.Edge, head),
            WoodPart("haft", "Pole", 1600, 2000, 30, 38, 0.65f), CraftJointKind.Socket, "An axe blade, a spike and a hook on a long pole.");
        d.components.Add(Fasteners("rivets", "Langets' rivets", CraftFastenerKind.Rivet, 2, true));
        Stats(d, 18, 26, 0.06f, 1.7f, 1.2f, 2200, 1300, 330, 180, 1, 7);
        list.Add(d);

        head = Shape(450, 550, 900, 1500, (0, 30, 26), (0.2f, 30, 26), (0.22f, 55, 6), (0.7f, 60, 5), (1, 10, 3));
        head.edge = CraftEdgeKind.Single; head.edgeFrom = 0.22f; head.edgeAngle = 30; head.eye = true; head.eyeAt = 0.1f; head.eyeDiameter = 30; head.hardened = true;
        d = Hafted("glaive", "Glaive", CraftDesignKind.Weapon, WeaponCategory.Spear, DamageForm.Slash, Metal("head", "Glaive blade", CraftComponentFunction.Edge, head),
            WoodPart("haft", "Pole", 1500, 1800, 30, 36, 0.6f), CraftJointKind.Socket, "A long single-edged blade on a pole: sweeping cuts at reach.");
        d.components.Add(Fasteners("rivets", "Socket rivets", CraftFastenerKind.Rivet, 2, true));
        Stats(d, 15, 21, 0.07f, 1.7f, 0.9f, 1500, 1100, 300, 150, 1, 7);
        list.Add(d);

        head = Shape(300, 400, 500, 900, (0, 28, 26), (0.3f, 28, 22), (0.32f, 70, 8), (0.45f, 52, 7), (1, 6, 4));
        head.edge = CraftEdgeKind.Double; head.edgeFrom = 0.32f; head.edgeAngle = 30; head.eye = true; head.eyeAt = 0.12f; head.eyeDiameter = 28; head.hardened = true;
        d = Hafted("boar_spear", "Boar spear", CraftDesignKind.Weapon, WeaponCategory.Spear, DamageForm.Pierce, Metal("head", "Broad spear head", CraftComponentFunction.Edge, head),
            WoodPart("haft", "Shaft", 1800, 2200, 28, 36, 0.65f), CraftJointKind.Socket, "A broad, leaf-shaped head on a thick shaft: it stops what charges it.");
        d.components.Add(Fasteners("rivets", "Socket pin", CraftFastenerKind.Rivet, 1, true));
        Stats(d, 13, 18, 0.1f, 1.8f, 0.6f, 600, 950, 300, 95, 1, 7);
        list.Add(d);

        // ---- thrown and missile weapons
        head = Shape(120, 180, 80, 180, (0, 18, 18), (0.4f, 18, 14), (0.42f, 18, 7), (1, 4, 3));
        head.edge = CraftEdgeKind.Double; head.edgeFrom = 0.42f; head.edgeAngle = 32; head.eye = true; head.eyeAt = 0.2f; head.eyeDiameter = 18; head.hardened = true;
        d = Hafted("javelin", "Javelin", CraftDesignKind.Weapon, WeaponCategory.Thrown, DamageForm.Pierce, Metal("head", "Javelin head", CraftComponentFunction.Edge, head),
            WoodPart("haft", "Light shaft", 1100, 1400, 18, 24, 0.5f), CraftJointKind.Socket, "A light spear made to be thrown.");
        d.scaling = WeaponScaling.Agility;
        Stats(d, 10, 14, 0.1f, 1.8f, 0.3f, 200, 600, 120, 25, 1, 5);
        list.Add(d);

        blade = Shape(200, 260, 90, 200, (0, 16, 5), (0.4f, 20, 5), (1, 4, 3));
        blade.edge = CraftEdgeKind.Double; blade.edgeFrom = 0.4f; blade.edgeAngle = 30; blade.hardened = true;
        d = new CraftDesign
        {
            id = "throwing_knives", displayName = "Throwing knives", kind = CraftDesignKind.Weapon, weaponCategory = WeaponCategory.Thrown, damageForm = DamageForm.Pierce,
            scaling = WeaponScaling.Agility, description = "A set of three balanced, handle-less knives.",
            components =
            {
                Metal("knives", "Throwing knife", CraftComponentFunction.Edge, blade, 3, 3),
                Leather("wrap", "Grip wrap", CraftComponentFunction.Wrap, "grip_wrap", 1f, 2.5f, 0.4f, 1, 3, 0.3f, true),
            },
            joints = { Joint("wrap", "Grip wraps", CraftJointKind.Wrap, "knives", "wrap", 6, 0.3f) },
        };
        Stats(d, 6, 9, 0.15f, 1.8f, 0.1f, 150, 0, 150, 30, 1, 2);
        list.Add(d);

        CraftComponentSpec stave = WoodPart("stave", "Bow stave", 1300, 1600, 26, 34, 0.7f, 3);
        stave.function = CraftComponentFunction.Striking;
        d = new CraftDesign
        {
            id = "hunting_bow", displayName = "Hunting bow", kind = CraftDesignKind.Weapon, weaponCategory = WeaponCategory.Bow, damageForm = DamageForm.Pierce,
            scaling = WeaponScaling.Agility, description = "A self bow of one stave, strung with cord or sinew.",
            components =
            {
                stave,
                SupplyPart("string", "Bowstring", CraftSupplyKind.Thread, 1, CraftComponentFunction.Thread),
                Leather("grip", "Grip wrap", CraftComponentFunction.Wrap, "grip_wrap", 1f, 2.5f, 0.4f, 1, 1, 0.3f, true),
            },
            joints = { Joint("strung", "Strung", CraftJointKind.Wrap, "stave", "string", 2, 1.2f), Joint("grip", "Grip wrap", CraftJointKind.Wrap, "stave", "grip", 8, 0.3f) },
        };
        Stats(d, 9, 13, 0.1f, 1.8f, 0.2f, 700, 0, 200, 50, 1, 5);
        list.Add(d);

        stave = WoodPart("stave", "Long stave", 1700, 2000, 30, 38, 0.8f, 3);
        stave.function = CraftComponentFunction.Striking;
        d = new CraftDesign
        {
            id = "longbow", displayName = "Longbow", kind = CraftDesignKind.Weapon, weaponCategory = WeaponCategory.Bow, damageForm = DamageForm.Pierce,
            scaling = WeaponScaling.Strength, description = "A tall yew bow with a heavy draw: it out-ranges everything.",
            components =
            {
                stave,
                SupplyPart("string", "Bowstring", CraftSupplyKind.Thread, 2, CraftComponentFunction.Thread),
                Leather("grip", "Grip wrap", CraftComponentFunction.Wrap, "grip_wrap", 1f, 2.5f, 0.4f, 1, 1, 0.3f, true),
            },
            joints = { Joint("strung", "Strung", CraftJointKind.Wrap, "stave", "string", 2, 1.2f), Joint("grip", "Grip wrap", CraftJointKind.Wrap, "stave", "grip", 8, 0.3f) },
        };
        Stats(d, 13, 18, 0.1f, 1.9f, 0.4f, 900, 0, 220, 90, 1, 6);
        list.Add(d);

        // ---- wooden and fist weapons
        CraftComponentSpec staff = WoodPart("staff", "Staff", 1700, 2000, 30, 38, 0.7f, 3);
        staff.function = CraftComponentFunction.Striking;
        d = new CraftDesign
        {
            id = "quarterstaff", displayName = "Quarterstaff", kind = CraftDesignKind.Weapon, weaponCategory = WeaponCategory.Staff, damageForm = DamageForm.Blunt,
            scaling = WeaponScaling.Strength, description = "A straight, tough staff: cheap, and quicker than it looks.",
            components = { staff, Leather("grip", "Grip wrap", CraftComponentFunction.Wrap, "grip_wrap", 1f, 2.5f, 0.4f, 1, 1, 0.3f, true) },
            joints = { Joint("grip", "Grip wrap", CraftJointKind.Wrap, "staff", "grip", 8, 0.3f) },
        };
        Stats(d, 8, 12, 0.04f, 1.5f, 0.9f, 1600, 0, 220, 15, 1, 6);
        list.Add(d);

        CraftComponentSpec club = WoodPart("club", "Club", 600, 800, 40, 70, 0.6f, 3);
        club.function = CraftComponentFunction.Striking;
        d = new CraftDesign
        {
            id = "club", displayName = "Studded club", kind = CraftDesignKind.Weapon, weaponCategory = WeaponCategory.Mace, damageForm = DamageForm.Blunt,
            scaling = WeaponScaling.Strength, description = "A heavy length of hardwood, thicker at the end, studded with iron if you have it.",
            components = { club, Fasteners("studs", "Iron studs", CraftFastenerKind.Stud, 10, true) },
            joints = { Joint("studs", "Studs driven in", CraftJointKind.Rivet, "club", "studs", 10, 0.6f) },
        };
        Stats(d, 9, 13, 0.03f, 1.5f, 1.2f, 1200, 0, 200, 12, 1, 3);
        list.Add(d);

        CraftShapeTarget knuckle = Shape(80, 100, 40, 110, (0, 30, 3), (1, 30, 3));
        d = new CraftDesign
        {
            id = "cestus", displayName = "Cestus", kind = CraftDesignKind.Weapon, weaponCategory = WeaponCategory.Fist, damageForm = DamageForm.Blunt,
            scaling = WeaponScaling.Strength, description = "Leather hand wraps with iron plates over the knuckles.",
            components =
            {
                Metal("plates", "Knuckle plate", CraftComponentFunction.Striking, knuckle, 2, 2),
                Leather("wraps", "Hand wrap", CraftComponentFunction.Backing, "glove", 1.5f, 3, 0.4f, 1, 2),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 6),
            },
            joints = { Joint("plates", "Plates riveted on", CraftJointKind.Rivet, "wraps", "plates", 6) },
        };
        Stats(d, 6, 9, 0.08f, 1.5f, 0.4f, 300, 0, 220, 35, 1, 1);
        list.Add(d);

        // ---- tools
        head = Shape(300, 400, 200, 380, (0, 18, 8), (0.15f, 18, 8), (0.2f, 30, 4), (1, 6, 2.5f));
        head.edge = CraftEdgeKind.Single; head.edgeFrom = 0.2f; head.edgeAngle = 22; head.eye = true; head.eyeAt = 0.05f; head.eyeDiameter = 18; head.hardened = true;
        d = Hafted("sickle", "Sickle", CraftDesignKind.Tool, WeaponCategory.Tool, DamageForm.Slash, Metal("head", "Sickle blade", CraftComponentFunction.Edge, head),
            WoodPart("haft", "Handle", 110, 140, 26, 32, 0.4f), CraftJointKind.Wedge, "A hooked blade for grain and grass.");
        d.toolType = ToolType.Scythe;
        Stats(d, 5, 8, 0.05f, 1.5f, 0.1f, 300, 120, 250, 25, 1, 2);
        d.toolDamage = 12;
        list.Add(d);

        head = Shape(600, 750, 500, 900, (0, 25, 8), (0.1f, 25, 8), (0.12f, 60, 3.5f), (0.8f, 45, 3), (1, 8, 2));
        head.edge = CraftEdgeKind.Single; head.edgeFrom = 0.12f; head.edgeAngle = 20; head.eye = true; head.eyeAt = 0.04f; head.eyeDiameter = 30; head.hardened = true;
        d = Hafted("scythe", "Scythe", CraftDesignKind.Tool, WeaponCategory.Tool, DamageForm.Slash, Metal("head", "Scythe blade", CraftComponentFunction.Edge, head),
            WoodPart("haft", "Snath", 1400, 1700, 30, 36, 0.5f), CraftJointKind.Wedge, "A long, thin, peened blade on a snath: it mows a field a day.");
        d.toolType = ToolType.Scythe;
        Stats(d, 9, 14, 0.05f, 1.6f, 0.5f, 900, 1100, 280, 55, 2, 6);
        d.toolDamage = 18;
        list.Add(d);

        head = Shape(170, 220, 1100, 1700, (0, 40, 32), (0.3f, 40, 32), (0.45f, 30, 14), (1, 95, 6));
        head.edge = CraftEdgeKind.Single; head.edgeFrom = 0.75f; head.edgeAngle = 30; head.eye = true; head.eyeAt = 0.15f; head.eyeDiameter = 32; head.hardened = true;
        d = Hafted("felling_axe", "Felling axe", CraftDesignKind.Tool, WeaponCategory.Tool, DamageForm.Slash, Metal("head", "Felling head", CraftComponentFunction.Edge, head),
            WoodPart("haft", "Long haft", 800, 950, 32, 40, 0.65f), CraftJointKind.Wedge, "A heavy, narrow axe on a long haft: for big trees.");
        d.toolType = ToolType.Axe;
        Stats(d, 9, 13, 0.03f, 1.5f, 0.8f, 1400, 600, 380, 70, 2, 5);
        d.toolDamage = 20;
        list.Add(d);

        // ---- shields
        CraftShapeTarget disc = Plate(280, 340, 650, 1300, 40, (0, 300, 1.8f), (1, 300, 1.8f));
        d = Garment("buckler", "Buckler", ArmorSlotType.Shield, "A small dished steel shield held in the fist: it parries more than it covers.", 7, 320, 55, 2, 2,
            new[]
            {
                Metal("plate", "Buckler plate", CraftComponentFunction.Protection, disc, 2),
                Leather("grip_strap", "Grip", CraftComponentFunction.Strap, "strap", 3, 5, 0, 1, 1, 0.5f),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 4),
            },
            Joint("grip_riveted", "Grip riveted on", CraftJointKind.Rivet, "plate", "grip_strap", 4));
        d.kind = CraftDesignKind.Shield;
        list.Add(d);

        CraftComponentSpec kite = WoodPart("board", "Boards", 900, 1100, 10, 16, 0.3f, 2);
        kite.function = CraftComponentFunction.Protection;
        kite.shape = Shape(900, 1100, 0, 99999, (0, 500, 12), (0.6f, 450, 12), (1, 60, 12));
        d = Garment("kite_shield", "Kite shield", ArmorSlotType.Shield, "A tall shield, round at the top and pointed below: it covers a rider's leg.", 15, 420, 110, 2, 4,
            new[]
            {
                kite,
                Leather("facing", "Leather facing", CraftComponentFunction.Protection, "facing", 2, 4, 0, 0.8f, 1, 1),
                Leather("grip_strap", "Enarmes", CraftComponentFunction.Strap, "strap", 3, 5, 0, 1, 2, 0.5f),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 12),
            },
            Joint("facing_riveted", "Facing riveted on", CraftJointKind.Rivet, "board", "facing", 8),
            Joint("straps_riveted", "Enarmes riveted on", CraftJointKind.Rivet, "board", "grip_strap", 4));
        d.kind = CraftDesignKind.Shield;
        list.Add(d);

        CraftComponentSpec heater = WoodPart("board", "Boards", 650, 800, 10, 16, 0.3f, 2);
        heater.function = CraftComponentFunction.Protection;
        heater.shape = Shape(650, 800, 0, 99999, (0, 600, 12), (0.6f, 560, 12), (1, 120, 12));
        d = Garment("heater_shield", "Heater shield", ArmorSlotType.Shield, "A flat-topped shield of boards under leather: the knight's shield.", 13, 380, 95, 3, 3,
            new[]
            {
                heater,
                Leather("facing", "Leather facing", CraftComponentFunction.Protection, "facing", 2, 4, 0, 0.8f, 1, 1),
                Leather("grip_strap", "Enarmes", CraftComponentFunction.Strap, "strap", 3, 5, 0, 1, 2, 0.5f),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 10),
            },
            Joint("facing_riveted", "Facing riveted on", CraftJointKind.Rivet, "board", "facing", 6),
            Joint("straps_riveted", "Enarmes riveted on", CraftJointKind.Rivet, "board", "grip_strap", 4));
        d.kind = CraftDesignKind.Shield;
        list.Add(d);

        // ---- plate and mail-less metal armour
        list.Add(Garment("kettle_hat", "Kettle hat", ArmorSlotType.Helmet, "A steel bowl with a wide brim: it sheds blows and rain from above.", 10, 380, 85, 2, 2,
            new[]
            {
                Metal("shell", "Brimmed shell", CraftComponentFunction.Protection, Plate(300, 360, 900, 1600, 70, (0, 320, 1.8f), (1, 320, 1.8f)), 2),
                Leather("liner", "Liner", CraftComponentFunction.Backing, "liner", 1.5f, 3.5f, 0.3f),
                SupplyPart("padding", "Padding", CraftSupplyKind.Padding, 1, CraftComponentFunction.Padding),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 6),
            },
            Joint("liner_riveted", "Liner riveted in", CraftJointKind.Rivet, "shell", "liner", 6)));

        list.Add(Garment("nasal_helm", "Nasal helm", ArmorSlotType.Helmet, "A conical steel helm with a bar down over the nose.", 11, 400, 95, 2, 2,
            new[]
            {
                Metal("shell", "Conical shell", CraftComponentFunction.Protection, Plate(230, 290, 650, 1300, 65, (0, 200, 2), (0.9f, 200, 2), (1, 30, 4)), 2),
                Leather("liner", "Liner", CraftComponentFunction.Backing, "liner", 1.5f, 3.5f, 0.3f),
                SupplyPart("padding", "Padding", CraftSupplyKind.Padding, 1, CraftComponentFunction.Padding),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 6),
            },
            Joint("liner_riveted", "Liner riveted in", CraftJointKind.Rivet, "shell", "liner", 6)));

        list.Add(Garment("great_helm", "Great helm", ArmorSlotType.Helmet, "A flat-topped steel barrel over the whole head, with slits to see through.", 18, 520, 180, 2, 2,
            new[]
            {
                Metal("shell", "Helm barrel", CraftComponentFunction.Protection, Plate(300, 360, 1800, 3000, 45, (0, 420, 2.2f), (1, 420, 2.2f)), 2.5f),
                Leather("liner", "Liner", CraftComponentFunction.Backing, "liner", 1.5f, 3.5f, 0.3f),
                SupplyPart("padding", "Padding", CraftSupplyKind.Padding, 2, CraftComponentFunction.Padding),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 10),
            },
            Joint("liner_riveted", "Liner riveted in", CraftJointKind.Rivet, "shell", "liner", 10)));

        list.Add(Garment("pauldrons", "Pauldrons", ArmorSlotType.Shoulders, "Dished steel plates over the shoulders, strapped on.", 8, 380, 90, 2, 2,
            new[]
            {
                Metal("cops", "Shoulder plate", CraftComponentFunction.Protection, Plate(180, 240, 330, 700, 40, (0, 170, 1.8f), (1, 150, 1.8f)), 1.5f, 2),
                Leather("straps", "Strap", CraftComponentFunction.Strap, "strap", 3, 5, 0, 1, 2, 0.5f),
                Fasteners("buckles", "Buckles", CraftFastenerKind.Buckle, 2),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 6),
            },
            Joint("straps_riveted", "Straps riveted on", CraftJointKind.Rivet, "cops", "straps", 6),
            Joint("buckles", "Buckles", CraftJointKind.Buckle, "straps", "buckles", 2)));

        list.Add(Garment("greaves", "Greaves", ArmorSlotType.Leggings, "Shaped steel plates over the shins, buckled behind the calf.", 9, 380, 95, 2, 3,
            new[]
            {
                Metal("plates", "Greave", CraftComponentFunction.Protection, Plate(320, 400, 450, 900, 35, (0, 140, 1.6f), (0.5f, 120, 1.6f), (1, 90, 1.6f)), 1.5f, 2),
                Leather("straps", "Strap", CraftComponentFunction.Strap, "strap", 3, 5, 0, 1, 4, 0.5f),
                Fasteners("buckles", "Buckles", CraftFastenerKind.Buckle, 4),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 8),
            },
            Joint("straps_riveted", "Straps riveted on", CraftJointKind.Rivet, "plates", "straps", 8),
            Joint("buckles", "Buckles", CraftJointKind.Buckle, "straps", "buckles", 4)));

        list.Add(Garment("sabatons", "Sabatons", ArmorSlotType.Boots, "Leather boots with overlapping steel lames over the feet.", 9, 380, 110, 2, 2,
            new[]
            {
                Metal("lames", "Foot lame", CraftComponentFunction.Protection, Plate(150, 200, 110, 260, 25, (0, 90, 1.5f), (1, 70, 1.5f)), 1.5f, 2),
                Leather("uppers", "Upper", CraftComponentFunction.Backing, "boot_upper", 2, 4, 0.3f, 0.9f, 2),
                Leather("soles", "Sole", CraftComponentFunction.Backing, "sole", 5, 9, 0, 0.4f, 2),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 8),
                Thread(),
            },
            Joint("soles_sewn", "Soles sewn on", CraftJointKind.Stitch, "uppers", "soles", 30),
            Joint("lames_riveted", "Lames riveted on", CraftJointKind.Rivet, "uppers", "lames", 8)));

        list.Add(Garment("vambraces", "Vambraces", ArmorSlotType.Bracers, "Steel tubes over the forearms, buckled on.", 7, 350, 70, 2, 2,
            new[]
            {
                Metal("plates", "Vambrace", CraftComponentFunction.Protection, Plate(220, 280, 220, 480, 30, (0, 110, 1.6f), (1, 90, 1.6f)), 1.5f, 2),
                Leather("straps", "Strap", CraftComponentFunction.Strap, "strap", 3, 5, 0, 1, 4, 0.5f),
                Fasteners("buckles", "Buckles", CraftFastenerKind.Buckle, 4),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 8),
            },
            Joint("straps_riveted", "Straps riveted on", CraftJointKind.Rivet, "plates", "straps", 8),
            Joint("buckles", "Buckles", CraftJointKind.Buckle, "straps", "buckles", 4)));

        list.Add(Garment("brigandine", "Brigandine", ArmorSlotType.Chestplate, "Small steel plates riveted inside a canvas coat: flexible, and hard to cut through.", 20, 450, 200, 2, 3,
            new[]
            {
                Cloth("cover", "Canvas cover", CraftComponentFunction.Backing, "jerkin_body", 0.8f, 2, 1, 1),
                Metal("plates", "Small plate", CraftComponentFunction.Protection, Plate(60, 90, 25, 85, 8, (0, 50, 1.4f), (1, 50, 1.4f)), 2.5f, 8),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 24),
                Thread(2),
            },
            Joint("plates_riveted", "Plates riveted in", CraftJointKind.Rivet, "cover", "plates", 24, 1.3f),
            Joint("seams", "Seams", CraftJointKind.Stitch, "cover", "thread", 20, 0.5f)));

        list.Add(Garment("scale_armour", "Scale armour", ArmorSlotType.Chestplate, "Rows of small steel scales laced to a leather coat like a fish's skin.", 18, 420, 170, 2, 3,
            new[]
            {
                Leather("backing", "Backing", CraftComponentFunction.Backing, "jerkin_body", 2, 4, 0.3f, 1, 1, 1),
                Metal("scales", "Scale", CraftComponentFunction.Protection, Plate(40, 60, 6, 30, 4, (0, 30, 1.2f), (1, 30, 1.2f)), 2.5f, 10),
                Thread(2),
            },
            Joint("scales_laced", "Scales laced on", CraftJointKind.Stitch, "backing", "scales", 40, 1.3f)));

        // ---- leather armour and wear
        list.Add(Garment("cuir_bouilli", "Cuir bouilli cuirass", ArmorSlotType.Chestplate, "Thick leather boiled and moulded hard: nearly as stiff as plate, far lighter.", 13, 300, 70, 2, 3,
            new[]
            {
                Leather("body", "Cuirass panel", CraftComponentFunction.Protection, "cuirass_panel", 4, 7, 0, 0.35f, 2, 2),
                Leather("straps", "Strap", CraftComponentFunction.Strap, "strap", 3, 5, 0, 1, 2, 0.5f),
                Fasteners("buckles", "Buckles", CraftFastenerKind.Buckle, 2),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 6),
            },
            Joint("straps_riveted", "Straps riveted on", CraftJointKind.Rivet, "body", "straps", 6),
            Joint("buckles", "Buckles", CraftJointKind.Buckle, "straps", "buckles", 2)));

        list.Add(Garment("leather_pauldrons", "Leather pauldrons", ArmorSlotType.Shoulders, "Thick, shaped leather over the shoulders.", 4, 220, 30, 2, 2,
            new[]
            {
                Leather("cops", "Shoulder cop", CraftComponentFunction.Protection, "pauldron", 3.5f, 6, 0, 0.5f, 2, 1.5f),
                Leather("straps", "Strap", CraftComponentFunction.Strap, "strap", 3, 5, 0, 1, 2, 0.5f),
                Fasteners("buckles", "Buckles", CraftFastenerKind.Buckle, 2),
                Thread(),
            },
            Joint("straps_sewn", "Straps sewn on", CraftJointKind.Stitch, "cops", "straps", 16),
            Joint("buckles", "Buckles", CraftJointKind.Buckle, "straps", "buckles", 2)));

        list.Add(Garment("leather_gloves", "Leather gloves", ArmorSlotType.Gloves, "Supple sewn gloves.", 2, 160, 15, 2, 2,
            new[] { Leather("gloves", "Glove", CraftComponentFunction.Protection, "glove", 1, 2.2f, 0.5f, 1, 2, 1.5f), Thread() },
            Joint("seams", "Seams", CraftJointKind.Stitch, "gloves", "thread", 30)));

        list.Add(Garment("leather_bracers", "Leather bracers", ArmorSlotType.Bracers, "Stiff leather cuffs buckled over the forearms.", 3, 220, 18, 2, 2,
            new[] { Leather("bracers", "Bracer", CraftComponentFunction.Protection, "bracer", 3, 5.5f, 0, 0.7f, 2, 1.5f), Fasteners("buckles", "Buckles", CraftFastenerKind.Buckle, 2) },
            Joint("buckles", "Buckles", CraftJointKind.Buckle, "bracers", "buckles", 2)));

        list.Add(Garment("leather_leggings", "Leather leggings", ArmorSlotType.Leggings, "Leather chaps over the legs, buckled to a belt.", 5, 230, 30, 2, 3,
            new[] { Leather("legs", "Leg panel", CraftComponentFunction.Protection, "chaps_leg", 2, 4, 0.3f, 1, 2, 1.5f), Fasteners("buckle", "Buckle", CraftFastenerKind.Buckle, 1), Thread() },
            Joint("seams", "Leg seams", CraftJointKind.Stitch, "legs", "thread", 30),
            Joint("buckle", "Buckle", CraftJointKind.Buckle, "legs", "buckle", 1)));

        list.Add(Garment("riding_boots", "Riding boots", ArmorSlotType.Boots, "Tall boots to the knee, with thick soles.", 4, 260, 40, 2, 3,
            new[]
            {
                Leather("uppers", "Tall upper", CraftComponentFunction.Protection, "tall_boot", 2, 3.5f, 0.4f, 1, 2, 1.5f),
                Leather("soles", "Sole", CraftComponentFunction.Backing, "sole", 5, 9, 0, 0.4f, 2),
                Thread(),
            },
            Joint("soles_sewn", "Soles sewn on", CraftJointKind.Stitch, "uppers", "soles", 36)));

        list.Add(Garment("sandals", "Sandals", ArmorSlotType.Boots, "Thick soles and a few straps: cool, quick, and little protection.", 1, 150, 8, 2, 1,
            new[]
            {
                Leather("soles", "Sole", CraftComponentFunction.Protection, "sole", 5, 9, 0, 0.4f, 2),
                Leather("straps", "Sandal strap", CraftComponentFunction.Strap, "sandal_strap", 2, 4, 0.2f, 1, 4, 0.5f),
                Thread(),
            },
            Joint("straps_sewn", "Straps sewn on", CraftJointKind.Stitch, "soles", "straps", 16)));

        list.Add(Garment("sword_belt", "Sword belt", ArmorSlotType.Belt, "A wide belt with hangers for a scabbard.", 1, 320, 25, 3, 1,
            new[]
            {
                Leather("strap", "Belt strap", CraftComponentFunction.Strap, "long_strap", 3, 5, 0, 1, 1, 2),
                Leather("hangers", "Hanger", CraftComponentFunction.Strap, "strap", 2.5f, 4.5f, 0, 1, 2, 0.6f),
                Fasteners("buckles", "Buckles", CraftFastenerKind.Buckle, 3),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 4),
            },
            Joint("hangers", "Hangers riveted on", CraftJointKind.Rivet, "strap", "hangers", 4),
            Joint("buckles", "Buckles", CraftJointKind.Buckle, "strap", "buckles", 3)));

        // ---- the tailor's: clothes
        list.Add(Garment("linen_shirt", "Linen shirt", ArmorSlotType.Chestplate, "A plain shirt of linen or cotton, worn under everything.", 1, 120, 8, 2, 2,
            new[]
            {
                Cloth("body", "Shirt body", CraftComponentFunction.Protection, "shirt_body", 0.25f, 1, 2, 1),
                Cloth("sleeves", "Sleeve", CraftComponentFunction.Protection, "sleeve", 0.25f, 1, 2, 0.5f),
                Thread(),
            },
            Joint("sleeves", "Sleeves set in", CraftJointKind.Stitch, "body", "sleeves", 24),
            Joint("side_seams", "Side seams", CraftJointKind.Stitch, "body", "thread", 20)));

        list.Add(Garment("wool_tunic", "Wool tunic", ArmorSlotType.Chestplate, "A warm tunic to the knee, sleeved.", 2, 160, 14, 2, 3,
            new[]
            {
                Cloth("body", "Tunic body", CraftComponentFunction.Protection, "tunic_body", 1, 3, 2, 1),
                Cloth("sleeves", "Sleeve", CraftComponentFunction.Protection, "sleeve", 1, 3, 2, 0.5f),
                Thread(2),
            },
            Joint("sleeves", "Sleeves set in", CraftJointKind.Stitch, "body", "sleeves", 24),
            Joint("side_seams", "Side seams", CraftJointKind.Stitch, "body", "thread", 24)));

        list.Add(Garment("padded_jack", "Padded jack", ArmorSlotType.Chestplate, "A short quilted coat with sleeves: lighter than a gambeson.", 8, 230, 40, 2, 3,
            new[]
            {
                Cloth("shell", "Outer shell", CraftComponentFunction.Protection, "tunic_body", 0.8f, 2.5f, 2, 1.5f),
                Cloth("sleeves", "Sleeve", CraftComponentFunction.Protection, "sleeve", 0.8f, 2.5f, 2, 0.8f),
                SupplyPart("padding", "Batting", CraftSupplyKind.Padding, 2, CraftComponentFunction.Padding),
                Thread(2),
            },
            Joint("quilting", "Quilting", CraftJointKind.Stitch, "shell", "padding", 36, 1.2f),
            Joint("sleeves", "Sleeves set in", CraftJointKind.Stitch, "shell", "sleeves", 24)));

        list.Add(Garment("robe", "Robe", ArmorSlotType.Chestplate, "A long, sleeved robe; in silk, the dress of scholars and mages.", 2, 160, 40, 2, 4,
            new[]
            {
                Cloth("panels", "Robe panel", CraftComponentFunction.Protection, "robe_panel", 0.25f, 1.8f, 2, 1),
                Cloth("sleeves", "Sleeve", CraftComponentFunction.Protection, "sleeve", 0.25f, 1.8f, 2, 0.5f),
                Thread(2),
            },
            Joint("side_seams", "Side seams", CraftJointKind.Stitch, "panels", "thread", 40),
            Joint("sleeves", "Sleeves set in", CraftJointKind.Stitch, "panels", "sleeves", 24)));
        list[list.Count - 1].magicDefense = 6;

        list.Add(Garment("wool_trousers", "Wool trousers", ArmorSlotType.Leggings, "Plain trousers of wool or canvas.", 2, 160, 12, 2, 3,
            new[] { Cloth("legs", "Trouser leg", CraftComponentFunction.Protection, "trouser_leg", 0.6f, 3, 2, 1), Thread() },
            Joint("inseam", "Inseams", CraftJointKind.Stitch, "legs", "thread", 30)));

        list.Add(Garment("linen_coif", "Linen coif", ArmorSlotType.Helmet, "A close cap of linen tied under the chin.", 1, 100, 5, 1, 1,
            new[] { Cloth("sides", "Coif side", CraftComponentFunction.Protection, "coif", 0.25f, 1.5f, 2, 1), Thread() },
            Joint("crown_seam", "Crown seam", CraftJointKind.Stitch, "sides", "thread", 14)));

        list.Add(Garment("arming_cap", "Arming cap", ArmorSlotType.Helmet, "A padded coif worn under a helm.", 3, 150, 15, 1, 1,
            new[]
            {
                Cloth("sides", "Coif side", CraftComponentFunction.Protection, "coif", 0.6f, 3, 2, 1),
                SupplyPart("padding", "Padding", CraftSupplyKind.Padding, 1, CraftComponentFunction.Padding),
                Thread(),
            },
            Joint("quilting", "Quilting", CraftJointKind.Stitch, "sides", "padding", 18)));

        list.Add(Garment("wool_hat", "Wool hat", ArmorSlotType.Helmet, "A crowned hat with a brim, of wool or felt.", 1, 120, 8, 2, 1,
            new[]
            {
                Cloth("crown", "Cap crown", CraftComponentFunction.Protection, "cap", 1, 3.5f, 1, 1),
                Cloth("brim", "Hat brim", CraftComponentFunction.Protection, "hat_brim", 1, 3.5f, 1, 0.6f),
                Thread(),
            },
            Joint("brim", "Brim sewn on", CraftJointKind.Stitch, "crown", "brim", 16)));

        list.Add(Garment("wool_mittens", "Wool mittens", ArmorSlotType.Gloves, "Warm mittens.", 1, 120, 6, 1, 1,
            new[] { Cloth("mittens", "Mitten", CraftComponentFunction.Protection, "mitten", 1, 3.5f, 2, 1), Thread() },
            Joint("seams", "Seams", CraftJointKind.Stitch, "mittens", "thread", 16)));

        list.Add(Garment("tabard", "Tabard", ArmorSlotType.Cloak, "Two panels over the shoulders: worn over armour, in a lord's colours.", 1, 150, 15, 2, 3,
            new[] { Cloth("panels", "Tabard panel", CraftComponentFunction.Protection, "tabard_panel", 0.25f, 2, 2, 1), Thread() },
            Joint("shoulders", "Shoulder seams", CraftJointKind.Stitch, "panels", "thread", 16)));

        list.Add(Garment("cape", "Cape", ArmorSlotType.Cloak, "A short cape from the shoulders, closed with a clasp.", 1, 150, 12, 2, 2,
            new[] { Cloth("cape", "Cape", CraftComponentFunction.Protection, "cape", 0.6f, 2.5f, 1, 1), Fasteners("clasp", "Clasp", CraftFastenerKind.Buckle, 1), Thread() },
            Joint("hem", "Hem", CraftJointKind.Stitch, "cape", "thread", 20, 0.5f),
            Joint("clasp", "Clasp sewn on", CraftJointKind.Buckle, "cape", "clasp", 1)));

        list.Add(Garment("sash", "Sash", ArmorSlotType.Belt, "A long cloth sash wound round the waist.", 0, 100, 4, 3, 1,
            new[] { Cloth("sash", "Sash", CraftComponentFunction.Strap, "sash", 0.25f, 2, 1, 1), Thread() },
            Joint("hem", "Hem", CraftJointKind.Stitch, "sash", "thread", 8, 0.5f)));

        list.Add(Garment("foot_wraps", "Foot wraps", ArmorSlotType.Boots, "Strips of cloth wound round the feet: better than bare.", 1, 100, 4, 1, 1,
            new[] { Cloth("wraps", "Foot wrap", CraftComponentFunction.Protection, "foot_wrap", 0.5f, 3, 2, 1), Thread() },
            Joint("hem", "Hem", CraftJointKind.Stitch, "wraps", "thread", 8, 0.5f)));
    }
}
