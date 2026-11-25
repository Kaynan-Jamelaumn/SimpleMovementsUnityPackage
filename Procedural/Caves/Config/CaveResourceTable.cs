using System;
using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>Where in a cave a deposit may sit.</summary>
    [Flags]
    public enum CaveMount
    {
        None = 0,
        /// <summary>Half sunk into a cave wall, at a height a player can swing a pick at.</summary>
        Wall = 1,
        /// <summary>A lump of rock on the cave floor (it blocks the way a little, so only where there is room around it).</summary>
        Floor = 2,
        Any = Wall | Floor,
    }

    /// <summary>How rare a resource is: a deposit's tier (0-4), its glint and the default chances.</summary>
    public enum CaveRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>
    /// One kind of deposit a cave can hold: what it gives (any material of the Crafting Database - an ore, coal, a stone,
    /// clay, salt, a gemstone), how deep and how often it appears, where it sits, how much it holds and how hard it is to
    /// mine. See <see cref="CaveResourceTable"/>.
    /// </summary>
    [Serializable]
    public class CaveResourceEntry
    {
        [Tooltip("Label: the prompt when a player aims at the deposit, reports, statistics.")]
        public string name = "Iron Ore Seam";
        [Tooltip("Off = never placed (keeps the entry for later).")]
        public bool enabled = true;

        [Header("What It Gives")]
        [Tooltip("What a lump is: Ore (a lump that keeps its seam), Fuel (coal), Supply (limestone, clay, sand, borax...), Quenchant (rock salt for brine) or Gem (a rough stone).")]
        public CraftStockKind kind = CraftStockKind.Ore;
        [Tooltip("The Crafting Database id of that material (ores: hematite, magnetite, bog_iron, malachite, chalcopyrite, cassiterite, galena, native_silver, gold_quartz, calamine, garnierite, meteorite, mythril_ore, orichalcum_ore; fuel: coal; supplies: limestone, refractory_clay, sand, borax, fullers_earth, tripoli; quenchant: brine; gems: rock_crystal, amethyst, ruby...).")]
        public string craftId = "hematite";
        [Tooltip("OPTIONAL. The item lumps become (empty = the stock item the crafting system makes for that id at runtime).")]
        public CraftStockItemSO itemOverride;
        [Tooltip("OPTIONAL. A gemstone the seam brings out now and then with a lump (a gem id; empty = none).")]
        public string gem = "";
        [Tooltip("Chance a lump brings that gemstone out with it.")]
        [Range(0f, 1f)] public float gemChance;

        [Header("How Deep and How Often")]
        [Tooltip("Cave depth range it appears in (see Cave Mining Stage > Depth: about 1 per floor down, more in caves further from the world's start and in the deeper caves reached through a cave's lowest passage). 0-1 Shallow, 1-2.5 Medium, 2.5-4 Deep, 4+ Abyssal.")]
        public FloatRange depth = new FloatRange(0f, 3f);
        [Tooltip("Depth where it is most common (inside Depth). Near the edges of the range it is a quarter as common.")]
        public float peakDepth = 1f;
        [Tooltip("How common it is at its peak, against the other entries (2 = twice as often as an entry with 1).")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Rarity: the deposit's tier (0-4), how much it glints, and what special chambers favour.")]
        public CaveRarity rarity = CaveRarity.Common;
        [Tooltip("Only in special chambers (rich veins, crystal grottos, guarded hoards) - never in ordinary caves.")]
        public bool specialChambersOnly;
        [Tooltip("How much more often it appears in a Rich Vein chamber (1 = as anywhere; ores usually more, stone less).")]
        [Min(0f)] public float richVeinAffinity = 1.5f;
        [Tooltip("How much more often it appears in a Crystal Grotto (gems: a lot; most others: rarely).")]
        [Min(0f)] public float grottoAffinity = 0.3f;

        [Header("Where It Sits")]
        [Tooltip("In cave walls, on the floor, or both.")]
        public CaveMount mount = CaveMount.Wall;
        [Tooltip("Deposits of one vein (placed close together: a seam rarely comes alone).")]
        public IntRange veinSize = new IntRange(1, 3);
        [Tooltip("How far apart the deposits of a vein spread (cells).")]
        [Min(1f)] public float veinSpread = 3.5f;
        [Tooltip("Size of the deposit's rock (1 = about a metre across). Bigger deposits hold more lumps.")]
        public FloatRange size = new FloatRange(0.85f, 1.2f);

        [Header("Mining")]
        [Tooltip("Lumps a deposit gives before it is worked out (scaled by its size).")]
        public IntRange lumps = new IntRange(6, 10);
        [Tooltip("Pick Tool Damage needed to break one lump off (a crafted pickaxe deals about 8-15 a swing).")]
        [Min(0.1f)] public float workPerLump = 28f;
        [Tooltip("Tool Damage a pick needs to break anything off (0 = any pick): the hardest seams wait for a better pick.")]
        [Min(0f)] public float minToolDamage;
        [Tooltip("Minutes of play until a worked-out deposit grows back (0 = never).")]
        [Min(0f)] public float regrowMinutes = 20f;
        [Tooltip("A few loose stones can be gathered by hand (Interact) before a pick is needed.")]
        public bool gatherByHand = true;
        [Tooltip("How many loose stones (by hand).")]
        [Min(0)] public int looseStones = 2;

        [Header("Look")]
        [Tooltip("OPTIONAL. The deposit's model (it gets an Ore Deposit when it has none; give it a collider). Empty = a generated rock with the mineral showing in it.")]
        public GameObject prefab;
        [Tooltip("Use the Crafting Database's colours for the rock and the mineral (off = the two colours below).")]
        public bool databaseColors = true;
        [Tooltip("Colour of the deposit's rock (Database Colors off, or no colour in the database).")]
        public Color rockColor = new Color(0.42f, 0.38f, 0.34f);
        [Tooltip("Colour of the mineral showing in the rock.")]
        public Color mineralColor = new Color(0.55f, 0.27f, 0.2f);
        [Tooltip("How much the mineral glows (0 = not at all; rare minerals catch the light).")]
        [Range(0f, 4f)] public float glow;

        /// <summary>The deposit's tier (0 common .. 4 legendary).</summary>
        public int Tier => (int)rarity;

        /// <summary>A copy (the defaults are copied into tables, never shared).</summary>
        public CaveResourceEntry Clone() => (CaveResourceEntry)MemberwiseClone();
    }

    /// <summary>
    /// The resources of a cave: every kind of deposit it can hold, with its depth range, commonness, rarity, vein size and
    /// mining values - all editable without code (spawn rates, rarity and depth requirements in one list). Used by a
    /// <see cref="CaveMiningStage"/>. Tools > SimpleMovements > Caves > Create Resource Table fills one from the
    /// Crafting Database (every ore, coal, stone, clay, salt and gemstone the crafting recipes use).
    /// </summary>
    [CreateAssetMenu(menuName = "SimpleMovements/Caves/Resource Table", fileName = "CaveResources")]
    public class CaveResourceTable : ScriptableObject
    {
        [Tooltip("The deposits a cave can hold. Each is placed where the cave's depth is inside its Depth range, more often near its Peak Depth, as often as its Weight against the others.")]
        public List<CaveResourceEntry> entries = new List<CaveResourceEntry>();

        private void OnValidate()
        {
            if (entries == null)
                return;
            foreach (CaveResourceEntry e in entries)
            {
                if (e == null)
                    continue;
                if (e.depth.max < e.depth.min)
                    e.depth = new FloatRange(e.depth.max, e.depth.min);
                e.peakDepth = Mathf.Clamp(e.peakDepth, e.depth.min, e.depth.max);
                e.veinSize = new IntRange(Mathf.Max(1, Mathf.Min(e.veinSize.min, e.veinSize.max)), Mathf.Max(1, Mathf.Max(e.veinSize.min, e.veinSize.max)));
                e.lumps = new IntRange(Mathf.Max(1, Mathf.Min(e.lumps.min, e.lumps.max)), Mathf.Max(1, Mathf.Max(e.lumps.min, e.lumps.max)));
            }
            DungeonProfile.MarkEdited();
        }

        /// <summary>A table with the built-in resources (every crafting material that can be mined; see <see cref="CaveDefaults"/>).</summary>
        public static CaveResourceTable CreateDefault()
        {
            var table = CreateInstance<CaveResourceTable>();
            table.name = "Cave Resources";
            table.entries = CaveDefaults.Resources();
            return table;
        }
    }
}
