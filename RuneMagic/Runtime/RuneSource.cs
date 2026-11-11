using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// Rune data of a monster (design spec 3.1, directive 4): put it on a mob prefab to say what runes it carries and how
    /// strong they are. It only adds data: the mob, its AI, loot, experience and essences are untouched. Without it a
    /// monster's drop profile is found from its species (mob type), name and faction, its tier and boss status from its
    /// dungeon placement.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SimpleMovements/Rune Magic/Rune Source")]
    public class RuneSource : MonoBehaviour
    {
        [Tooltip("This monster carries no runes and gives no magical energy.")]
        [SerializeField] private bool noRunes;
        [Tooltip("Drop profile id (Rune Magic Database ▸ Drop Profiles). Empty = matched from species, name, faction and tags.")]
        [SerializeField] private string profile = "";
        [Tooltip("The monster's magical alignments (fire, wind...): runes of these alignments drop more often, even ones the profile does not list.")]
        [SerializeField] private List<string> alignments = new List<string>();
        [Tooltip("Special properties (ancient, crystalline, corrupted...): they match drop profiles' tags.")]
        [SerializeField] private List<string> tags = new List<string>();
        [Tooltip("The monster's rank: its runes are at least this rank (0 = from the profile).")]
        [SerializeField, Min(0)] private int monsterRank;
        [Tooltip("How rare the monster is (Auto = from its dungeon placement: normal, elite or boss).")]
        [SerializeField] private MonsterRarity rarity = MonsterRarity.Auto;
        [Tooltip("Dungeon difficulty tier for its drops (-1 = from its dungeon placement, 0 outside dungeons).")]
        [SerializeField] private int tier = -1;
        [Tooltip("Multiplies the drop chance.")]
        [SerializeField, Min(0f)] private float chanceMultiplier = 1f;
        [Tooltip("Multiplies the magical energy its killer gains.")]
        [SerializeField, Min(0f)] private float energyMultiplier = 1f;
        [Tooltip("Runes it always drops (besides the rolled ones): rune id with a fixed rank range.")]
        [SerializeField] private List<RuneDropEntry> guaranteed = new List<RuneDropEntry>();

        public bool NoRunes => noRunes;
        public string Profile => profile;
        public IList<string> Alignments => alignments;
        public IList<string> Tags => tags;
        public int MonsterRank => monsterRank;
        public MonsterRarity Rarity => rarity;
        public int Tier => tier;
        public float ChanceMultiplier => chanceMultiplier;
        public float EnergyMultiplier => energyMultiplier;
        public IReadOnlyList<RuneDropEntry> Guaranteed => guaranteed;

        /// <summary>The Rune Source of a monster (on it or its parents), or null.</summary>
        public static RuneSource Of(Component anyPart) => anyPart != null ? anyPart.GetComponentInParent<RuneSource>() : null;
    }
}
