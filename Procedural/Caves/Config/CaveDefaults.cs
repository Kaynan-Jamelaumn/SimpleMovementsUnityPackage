using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>
    /// The built-in cave content: every material of the Crafting Database that can come out of the ground - its 14 ores,
    /// coal, limestone, clay, sand, borax, fuller's earth, tripoli, rock salt and its 16 gemstones - placed by depth and
    /// rarity (shallow caves: coal, stone, bog iron, red iron and green copper ore; medium: tin, brassy copper, lead, zinc,
    /// black iron, salt; deep: nickel, native silver, gold-bearing quartz, most gemstones; abyssal: orichalcum, mythril,
    /// rubies, sapphires and diamonds; fallen stars only in special chambers), and the settings that make a Dungeon Profile
    /// a cave (natural floor styles only, cave sizes, tunnels, drops and climbing roots, cave floor modifiers, guardian
    /// chambers and nests, kept generations).
    /// </summary>
    public static class CaveDefaults
    {
        // ------------------------------------------------------------------ resources

        /// <summary>The built-in resource entries (fresh copies).</summary>
        public static List<CaveResourceEntry> Resources()
        {
            var list = new List<CaveResourceEntry>
            {
                // ---- shallow: fuel, stone and the common ores
                Entry("Coal Seam", CraftStockKind.Fuel, "coal", 0f, 3.5f, 0.8f, 1.4f, CaveRarity.Common, CaveMount.Wall, 2, 4, 8, 14, 18f, 15f,
                    new Color(0.09f, 0.085f, 0.08f), new Color(0.04f, 0.04f, 0.045f), byHand: 3, richVein: 1f, grotto: 0.1f),
                Entry("Limestone", CraftStockKind.Supply, "limestone", 0f, 2.5f, 0.4f, 1f, CaveRarity.Common, CaveMount.Any, 1, 3, 6, 10, 20f, 12f,
                    new Color(0.74f, 0.72f, 0.65f), new Color(0.86f, 0.84f, 0.78f), byHand: 3, richVein: 0.5f, grotto: 0.1f),
                Entry("Clay Bank", CraftStockKind.Supply, "refractory_clay", 0f, 1.8f, 0.3f, 0.55f, CaveRarity.Common, CaveMount.Floor, 1, 2, 5, 8, 12f, 10f,
                    new Color(0.52f, 0.42f, 0.33f), new Color(0.66f, 0.55f, 0.44f), byHand: 4, richVein: 0.3f, grotto: 0f),
                Entry("Sand Bank", CraftStockKind.Supply, "sand", 0f, 1.5f, 0.2f, 0.4f, CaveRarity.Common, CaveMount.Floor, 1, 2, 6, 10, 10f, 10f,
                    new Color(0.7f, 0.62f, 0.46f), new Color(0.84f, 0.76f, 0.58f), byHand: 5, richVein: 0.2f, grotto: 0f),
                Entry("Bog Iron", CraftStockKind.Ore, "bog_iron", 0f, 1.6f, 0.3f, 0.7f, CaveRarity.Common, CaveMount.Any, 1, 3, 5, 9, 26f, 18f,
                    new Color(0.5f, 0.42f, 0.32f), new Color(0.55f, 0.4f, 0.2f), byHand: 2),
                Entry("Red Iron Ore", CraftStockKind.Ore, "hematite", 0f, 3.2f, 1f, 1.25f, CaveRarity.Common, CaveMount.Wall, 2, 4, 6, 10, 38f, 20f,
                    new Color(0.5f, 0.42f, 0.38f), new Color(0.55f, 0.22f, 0.18f), gem: "garnet", gemChance: 0.04f),
                Entry("Green Copper Ore", CraftStockKind.Ore, "malachite", 0f, 2.8f, 0.7f, 1f, CaveRarity.Common, CaveMount.Wall, 1, 3, 6, 10, 30f, 20f,
                    new Color(0.7f, 0.68f, 0.6f), new Color(0.18f, 0.55f, 0.35f), gem: "turquoise", gemChance: 0.08f),
                Entry("Fuller's Earth", CraftStockKind.Supply, "fullers_earth", 0.2f, 2f, 0.8f, 0.25f, CaveRarity.Uncommon, CaveMount.Floor, 1, 2, 4, 7, 12f, 15f,
                    new Color(0.55f, 0.52f, 0.42f), new Color(0.68f, 0.66f, 0.52f), byHand: 3, richVein: 0.3f, grotto: 0f),
                Entry("Tripoli", CraftStockKind.Supply, "tripoli", 0.3f, 2.5f, 1f, 0.2f, CaveRarity.Uncommon, CaveMount.Wall, 1, 2, 4, 7, 16f, 15f,
                    new Color(0.78f, 0.74f, 0.66f), new Color(0.9f, 0.86f, 0.76f), byHand: 2, richVein: 0.3f, grotto: 0.2f),
                Gem("Rock Crystal Cluster", "rock_crystal", 0.3f, 4.5f, 1.5f, 0.25f, CaveRarity.Uncommon, 2, 4, 40f, 40f, new Color(0.92f, 0.94f, 0.96f), 0.6f),
                Gem("Citrine Cluster", "citrine", 1f, 4.5f, 2.2f, 0.1f, CaveRarity.Uncommon, 2, 3, 40f, 45f, new Color(0.95f, 0.75f, 0.25f), 0.6f),

                // ---- medium
                Entry("Tin Stone", CraftStockKind.Ore, "cassiterite", 0.6f, 3.5f, 1.6f, 0.75f, CaveRarity.Uncommon, CaveMount.Wall, 1, 3, 5, 8, 41f, 25f,
                    new Color(0.62f, 0.6f, 0.56f), new Color(0.25f, 0.17f, 0.12f)),
                Entry("Brassy Copper Ore", CraftStockKind.Ore, "chalcopyrite", 1f, 3.8f, 2f, 0.7f, CaveRarity.Uncommon, CaveMount.Wall, 2, 3, 6, 9, 32f, 25f,
                    new Color(0.6f, 0.58f, 0.52f), new Color(0.78f, 0.68f, 0.3f), glow: 0.2f),
                Entry("Lead Ore", CraftStockKind.Ore, "galena", 1f, 4.2f, 2.2f, 0.55f, CaveRarity.Uncommon, CaveMount.Wall, 1, 3, 5, 9, 26f, 25f,
                    new Color(0.64f, 0.63f, 0.6f), new Color(0.5f, 0.52f, 0.56f), glow: 0.15f),
                Entry("Calamine", CraftStockKind.Ore, "calamine", 1f, 3.8f, 2f, 0.45f, CaveRarity.Uncommon, CaveMount.Any, 1, 2, 5, 8, 34f, 25f,
                    new Color(0.8f, 0.78f, 0.7f), new Color(0.75f, 0.72f, 0.6f)),
                Entry("Rock Salt", CraftStockKind.Quenchant, "brine", 1f, 3.5f, 1.8f, 0.4f, CaveRarity.Uncommon, CaveMount.Any, 1, 3, 4, 8, 18f, 30f,
                    new Color(0.7f, 0.68f, 0.66f), new Color(0.9f, 0.88f, 0.86f), byHand: 2, richVein: 0.4f, grotto: 0.3f),
                Entry("Borax Crust", CraftStockKind.Supply, "borax", 1.5f, 4f, 2.5f, 0.25f, CaveRarity.Uncommon, CaveMount.Any, 1, 2, 4, 7, 16f, 30f,
                    new Color(0.72f, 0.7f, 0.66f), new Color(0.95f, 0.95f, 0.92f), byHand: 2, richVein: 0.4f, grotto: 0.4f),
                Entry("Black Iron Ore", CraftStockKind.Ore, "magnetite", 1.5f, 5f, 2.8f, 0.65f, CaveRarity.Uncommon, CaveMount.Wall, 1, 3, 6, 9, 40f, 30f,
                    new Color(0.45f, 0.43f, 0.42f), new Color(0.15f, 0.15f, 0.17f), glow: 0.1f),
                Gem("Amethyst Geode", "amethyst", 1f, 5f, 2.5f, 0.15f, CaveRarity.Rare, 2, 3, 42f, 45f, new Color(0.55f, 0.3f, 0.7f), 0.9f),
                Gem("Turquoise Vein", "turquoise", 1.2f, 4.5f, 2.4f, 0.08f, CaveRarity.Rare, 2, 3, 30f, 45f, new Color(0.25f, 0.7f, 0.68f), 0.4f),
                Gem("Lapis Lazuli", "lapis_lazuli", 1.5f, 5f, 2.8f, 0.07f, CaveRarity.Rare, 2, 3, 32f, 45f, new Color(0.15f, 0.25f, 0.65f), 0.4f),
                Gem("Moonstone Seam", "moonstone", 1.8f, 5.5f, 3f, 0.06f, CaveRarity.Rare, 1, 3, 36f, 50f, new Color(0.82f, 0.86f, 0.92f), 1f),
                Gem("Garnet Cluster", "garnet", 1.5f, 5.5f, 3f, 0.12f, CaveRarity.Rare, 2, 3, 46f, 50f, new Color(0.55f, 0.08f, 0.12f), 0.7f),

                // ---- deep
                Entry("Green Nickel Ore", CraftStockKind.Ore, "garnierite", 2f, 5.5f, 3.2f, 0.35f, CaveRarity.Rare, CaveMount.Any, 1, 2, 5, 8, 24f, 35f,
                    new Color(0.55f, 0.6f, 0.5f), new Color(0.45f, 0.68f, 0.42f)),
                Entry("Native Silver", CraftStockKind.Ore, "native_silver", 2f, 6.5f, 3.5f, 0.26f, CaveRarity.Rare, CaveMount.Wall, 1, 2, 3, 6, 36f, 45f,
                    new Color(0.78f, 0.77f, 0.74f), new Color(0.75f, 0.76f, 0.8f), gem: "moonstone", gemChance: 0.1f, glow: 0.5f),
                Entry("Gold-Bearing Quartz", CraftStockKind.Ore, "gold_quartz", 2.5f, 7f, 4f, 0.2f, CaveRarity.Rare, CaveMount.Wall, 1, 2, 3, 6, 44f, 50f,
                    new Color(0.85f, 0.84f, 0.8f), new Color(1f, 0.82f, 0.35f), gem: "citrine", gemChance: 0.12f, glow: 0.8f),
                Gem("Blue Topaz Cluster", "topaz", 2.5f, 6.5f, 4f, 0.1f, CaveRarity.Rare, 2, 3, 50f, 55f, new Color(0.45f, 0.7f, 0.95f), 0.9f),
                Gem("Peridot Nodule", "peridot", 2.5f, 6f, 3.8f, 0.08f, CaveRarity.Rare, 1, 3, 44f, 55f, new Color(0.55f, 0.75f, 0.2f), 0.7f),
                Gem("Tourmaline Cluster", "tourmaline", 2.5f, 6.5f, 4f, 0.08f, CaveRarity.Rare, 1, 3, 46f, 55f, new Color(0.85f, 0.35f, 0.55f), 0.8f),
                Gem("Opal Seam", "opal", 2.5f, 6.5f, 4.2f, 0.06f, CaveRarity.Rare, 1, 2, 30f, 60f, new Color(0.88f, 0.9f, 0.95f), 1.2f),

                // ---- abyssal: the rarest metals and stones (a better pick is needed)
                Entry("Orichalcum Ore", CraftStockKind.Ore, "orichalcum_ore", 3.5f, 9f, 5.5f, 0.12f, CaveRarity.Epic, CaveMount.Wall, 1, 2, 3, 5, 50f, 75f,
                    new Color(0.4f, 0.32f, 0.28f), new Color(0.92f, 0.45f, 0.18f), gem: "ruby", gemChance: 0.1f, glow: 1.4f, minTool: 8f),
                Entry("Mythril Vein", CraftStockKind.Ore, "mythril_ore", 4f, 10f, 6.5f, 0.08f, CaveRarity.Legendary, CaveMount.Wall, 1, 1, 2, 4, 56f, 120f,
                    new Color(0.45f, 0.48f, 0.52f), new Color(0.55f, 0.72f, 0.9f), gem: "aquamarine", gemChance: 0.12f, glow: 1.8f, minTool: 10f),
                Entry("Fallen Star", CraftStockKind.Ore, "meteorite", 2.5f, 10f, 5f, 0.05f, CaveRarity.Epic, CaveMount.Floor, 1, 1, 2, 3, 60f, 180f,
                    new Color(0.16f, 0.15f, 0.15f), new Color(0.35f, 0.3f, 0.45f), gem: "peridot", gemChance: 0.25f, glow: 1f, minTool: 10f, specialOnly: true),
                Gem("Aquamarine Cluster", "aquamarine", 3.5f, 8f, 5f, 0.06f, CaveRarity.Epic, 1, 3, 50f, 70f, new Color(0.5f, 0.85f, 0.9f), 1.1f, minTool: 8f),
                Gem("Emerald Seam", "emerald", 3.5f, 8f, 5.2f, 0.05f, CaveRarity.Epic, 1, 2, 50f, 75f, new Color(0.1f, 0.62f, 0.32f), 1.1f, minTool: 8f),
                Gem("Ruby Cluster", "ruby", 4f, 9f, 6f, 0.04f, CaveRarity.Epic, 1, 2, 56f, 90f, new Color(0.8f, 0.08f, 0.15f), 1.3f, minTool: 8f),
                Gem("Sapphire Cluster", "sapphire", 4f, 9f, 6f, 0.04f, CaveRarity.Epic, 1, 2, 56f, 90f, new Color(0.12f, 0.25f, 0.8f), 1.3f, minTool: 8f),
                Gem("Diamond Pipe", "diamond", 5f, 12f, 7.5f, 0.025f, CaveRarity.Legendary, 1, 2, 64f, 150f, new Color(0.9f, 0.95f, 1f), 2f, minTool: 10f),
            };
            return list;
        }

        private static CaveResourceEntry Entry(string name, CraftStockKind kind, string id, float minDepth, float maxDepth, float peak, float weight,
            CaveRarity rarity, CaveMount mount, int veinMin, int veinMax, int lumpsMin, int lumpsMax, float work, float regrowMinutes, Color rock, Color mineral,
            string gem = "", float gemChance = 0f, float glow = 0f, float minTool = 0f, int byHand = 2, float richVein = 1.6f, float grotto = 0.3f,
            bool specialOnly = false)
        {
            return new CaveResourceEntry
            {
                name = name,
                kind = kind,
                craftId = id,
                depth = new FloatRange(minDepth, maxDepth),
                peakDepth = Mathf.Clamp(peak, minDepth, maxDepth),
                weight = weight,
                rarity = rarity,
                mount = mount,
                veinSize = new IntRange(veinMin, veinMax),
                lumps = new IntRange(lumpsMin, lumpsMax),
                workPerLump = work,
                regrowMinutes = regrowMinutes,
                rockColor = rock,
                mineralColor = mineral,
                gem = gem,
                gemChance = gemChance,
                glow = glow,
                minToolDamage = minTool,
                gatherByHand = byHand > 0,
                looseStones = byHand,
                richVeinAffinity = richVein,
                grottoAffinity = grotto,
                specialChambersOnly = specialOnly,
                veinSpread = 3.5f,
                size = kind == CraftStockKind.Supply || kind == CraftStockKind.Fuel ? new FloatRange(0.95f, 1.35f) : new FloatRange(0.8f, 1.15f),
            };
        }

        /// <summary>A crystal vein: its lumps are rough stones of the gem (mostly found in crystal grottos).</summary>
        private static CaveResourceEntry Gem(string name, string gemId, float minDepth, float maxDepth, float peak, float weight, CaveRarity rarity,
            int lumpsMin, int lumpsMax, float work, float regrowMinutes, Color color, float glow, float minTool = 0f)
        {
            CaveResourceEntry e = Entry(name, CraftStockKind.Gem, gemId, minDepth, maxDepth, peak, weight, rarity, CaveMount.Wall, 1, 2, lumpsMin, lumpsMax,
                work, regrowMinutes, new Color(0.4f, 0.38f, 0.36f), color, glow: glow, minTool: minTool, byHand: 0, richVein: 0.6f, grotto: 6f);
            e.size = new FloatRange(0.7f, 1f);
            return e;
        }

        // ------------------------------------------------------------------ profile and theme

        /// <summary>
        /// Makes <paramref name="p"/> a cave (keeps its tables, theme, build settings and custom stages): the Deep Caves type,
        /// then natural floor styles only (caverns, now and then a great den, a fungal hive or a chasm), cave sizes, narrow
        /// winding tunnels, drops and climbing roots between levels, cave floor modifiers from the second level on, guardian
        /// chambers, nests, ambushes and gardens, and kept generations (a cave entered again is not generated again).
        /// </summary>
        public static void ApplyProfile(DungeonProfile p)
        {
            if (p == null)
                return;
            DungeonTypes.Apply(p, DungeonType.DeepCaves);

            StyleWeights s = p.styles;
            s.rooms = s.bsp = s.hybrid = s.gridMaze = s.citadel = s.catacombs = s.tower = s.undercity = s.astral = 0f;
            s.caverns = 1.4f;
            s.den = 0.15f;
            s.hive = 0.08f;
            s.islands = 0.06f;
            p.naturalWeightPerFloor = 0f;
            p.repeatStylePenalty = 0.85f;
            p.openness = new FloatRange(0.25f, 0.85f);
            p.complexity = new FloatRange(0.35f, 0.8f);
            p.footprintVariation = 0.18f;
            p.difficultyPerFloor = 0.25f;
            p.sizeClasses = new[]
            {
                new SizeClassSettings { size = SizeClass.Small, floorCells = new Vector2Int(48, 48), floorCount = new Vector2Int(1, 2) },
                new SizeClassSettings { size = SizeClass.Medium, floorCells = new Vector2Int(64, 64), floorCount = new Vector2Int(2, 3) },
                new SizeClassSettings { size = SizeClass.Large, floorCells = new Vector2Int(80, 80), floorCount = new Vector2Int(3, 4) },
                new SizeClassSettings { size = SizeClass.Huge, floorCells = new Vector2Int(96, 96), floorCount = new Vector2Int(4, 6) },
            };

            CaveSettings c = p.caves;
            c.tunnelWidth = new IntRange(2, 3);
            c.tunnelWinding = 1.15f;
            c.chamberSpacing = 9f;
            c.fillNoiseStrength = 0.24f;
            c.minRegionCells = 36;
            c.ceilingLimits = new FloatRange(3.2f, 13f);

            ConnectionSettings con = p.connections;
            con.deadEndShare = new FloatRange(0.25f, 0.5f);
            con.loopChance = new FloatRange(0.1f, 0.35f);
            con.secretChance = 0.06f;

            LinkSettings l = p.links;
            l.dropChance = 0.4f;
            l.climbChance = 0.4f;
            l.extraStairChance = 0.3f;

            FloorModifierSettings m = p.floorModifiers;
            m.chance = 0.35f;
            m.firstFloor = 1;
            m.flooded = 1f;
            m.overgrown = 0.8f;
            m.darkness = 0.9f;
            m.molten = 0.6f;
            m.frozen = 0.5f;

            Boost(p, AreaRole.MiniBoss, 0.35f);
            Boost(p, AreaRole.Treasure, 0.7f);
            Boost(p, AreaRole.Nest, 0.4f);
            Boost(p, AreaRole.Ambush, 0.35f);
            Boost(p, AreaRole.Garden, 0.3f);
            Boost(p, AreaRole.Arena, 0.25f);

            p.build.keepGenerated = Mathf.Max(p.build.keepGenerated, 4);
        }

        /// <summary>Gives <paramref name="t"/> a cave's look: the Deep Caves colours, rough stone steps, cold crystal light, a dark, damp air.</summary>
        public static void ApplyTheme(DungeonTheme t)
        {
            if (t == null)
                return;
            DungeonTypes.ApplyTheme(t, DungeonType.DeepCaves);
            t.stairsColor = Color.Lerp(t.caveFloorColor, t.caveWallColor, 0.5f);
            t.trimColor = t.caveWallColor * 0.8f;
            t.crystalColor = new Color(0.45f, 0.8f, 1f);
            t.ambientLight = new Color(0.09f, 0.09f, 0.1f);
            t.fogColor = new Color(0.035f, 0.035f, 0.04f);
            t.fogDensity = 0.04f;
            t.applyAtmosphere = true;
        }

        private static void Boost(DungeonProfile p, AreaRole role, float chance)
        {
            if (p.roles == null)
                return;
            foreach (RoleRule r in p.roles)
                if (r != null && r.role == role)
                    r.chance = Mathf.Clamp01(chance);
        }

        // ------------------------------------------------------------------ names

        private static readonly string[] Adjectives =
        {
            "Whispering", "Hollow", "Dripping", "Gloom", "Ember", "Frost", "Echoing", "Sunken", "Glittering", "Ashen", "Mossy", "Shivering",
            "Deep", "Weeping", "Silent", "Crystal", "Iron", "Copper", "Black", "Old", "Forgotten", "Rumbling", "Winding", "Starless",
        };

        private static readonly string[] Nouns =
        {
            "Hollow", "Grotto", "Cavern", "Deep", "Hole", "Warren", "Delve", "Rift", "Throat", "Galleries", "Halls", "Pit", "Burrow", "Vault",
        };

        /// <summary>A name for the cave with <paramref name="seed"/> (the same cave always has the same name): "Whispering Grotto".</summary>
        public static string NameFor(int seed)
        {
            uint h = PlacementRandom.Hash(seed, PlacementRandom.StableHash("CaveName"), 0, 0, 0);
            string a = Adjectives[(int)(h % (uint)Adjectives.Length)];
            string n = Nouns[(int)((h / 97u) % (uint)Nouns.Length)];
            if (a == n)
                n = "Caves";
            return $"{a} {n}";
        }
    }
}
