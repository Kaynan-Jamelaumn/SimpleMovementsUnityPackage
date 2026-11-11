using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// A rune that has not been absorbed yet (decision D4): an inventory item that drops from monsters, sits in chests,
    /// merchants' stock or quest rewards, and can be traded. A rune user (Mage) absorbs it into their soul - by using it,
    /// or from the Rune Soul window - when the soul has room; the item is then gone. Other classes can carry, sell or trade it.
    /// <para>Drops are made at runtime from their id ("rune:fire:1:42:50" = Fire, rank 1, quality 42, energy 50), so every
    /// machine, save and merchant resolves the same rune. Create assets of your own (Create ▸ SimpleMovements ▸ Rune Magic
    /// ▸ Rune Item) for fixed runes in loot tables, chests, shops and rewards.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Rune", menuName = "SimpleMovements/Rune Magic/Rune Item", order = 21)]
    public class RuneItemSO : ItemSO, IRuntimeItem
    {
        [Header("Rune")]
        [Tooltip("The rune (its id in the Rune Magic Database: wind, fire...).")]
        [SerializeField] private string runeId = "fire";
        [SerializeField, Min(1)] private int rank = 1;
        [Tooltip("Stability and potential (0-100).")]
        [SerializeField, Range(0f, 100f)] private float quality = 40f;
        [Tooltip("Energy the rune already holds (kept within its maximum when absorbed).")]
        [SerializeField, Min(0f)] private float energy;

        [System.NonSerialized] private string runtimeId;

        /// <summary>The id of a rune made at runtime ("rune:fire:1:42:50"); null for an asset.</summary>
        public string RuntimeId => runtimeId;
        public string RuneId => runeId;
        public int Rank => rank;
        public float Quality => quality;
        public float Energy => energy;

        /// <summary>The rune this item holds.</summary>
        public RuneDrop Drop => new RuneDrop { runeId = runeId, rank = Mathf.Max(1, rank), quality = Mathf.Clamp(quality, 0f, 100f), energy = Mathf.Max(0f, energy) };

        public RuneItemSO()
        {
            itemType = ItemType.Miscellaneous;
            stackMax = 5;
            weight = 0.1f;
            // Using it asks to absorb it; the soul takes it out of the inventory itself when it is absorbed.
            maxDurability = 1;
            durability = 1;
            durabilityReductionPerUse = 0;
            shouldBeDestroyedOn0UsesLeft = false;
        }

        /// <summary>Makes this item the rune <paramref name="d"/> (runtime runes).</summary>
        public void Setup(RuneDrop d, string id)
        {
            runtimeId = id;
            runeId = d.runeId;
            rank = Mathf.Max(1, d.rank);
            quality = Mathf.Clamp(d.quality, 0f, 100f);
            energy = Mathf.Max(0f, d.energy);
            RuneCatalog c = RuneMagicDatabase.Instance.Catalog;
            RuneDef def = c.Rune(runeId);
            RuneAcquisitionBalance a = c.Balance.acquisition;
            name = RuneRules.DisplayName(c, runeId, rank) + " Rune";
            description = def != null && !string.IsNullOrEmpty(def.magicalProperties)
                ? def.magicalProperties + " A Mage can absorb it into their soul."
                : "A rune. A Mage can absorb it into their soul.";
            if (a != null)
            {
                weight = a.itemWeight;
                stackMax = Mathf.Max(1, a.itemStack);
                price = a.itemPricePerRank * rank * RarityPriceFactor(def != null ? def.rarity : RuneRarity.Common);
                canBeSold = a.itemsCanBeSold;
            }
            icon = RuneArt.Icon(def);
            prefab = RuneArt.Model(def);
        }

        private static float RarityPriceFactor(RuneRarity r) => 1 << Mathf.Clamp((int)r, 0, 4);

        public override bool CanUseFromQuickSlot => true;

        /// <summary>A rune has no uses to wear out.</summary>
        public override bool ShowsDurability => false;

        /// <summary>Using it asks the user's soul to absorb it (only rune users can; the soul checks the capacity).</summary>
        public override void UseItem(GameObject player, PlayerStatusController statusController)
        {
            base.UseItem(player, statusController);
            RuneSoul soul = player != null ? RuneSoul.For(player) : statusController != null ? RuneSoul.For(statusController) : null;
            if (soul != null)
                soul.RequestAbsorb(this);
        }

        public override void AppendTooltip(List<string> lines)
        {
            base.AppendTooltip(lines);
            if (lines == null) return;
            RuneCatalog c = RuneMagicDatabase.Instance.Catalog;
            RuneDef def = c.Rune(runeId);
            if (def == null)
            {
                lines.Add($"Unknown rune '{runeId}'");
                return;
            }
            QualityTier tier = c.Balance.QualityTierOf(quality);
            float max = c.Balance.MaxEnergy(rank, quality);
            lines.Add($"Rune · Rank {rank} · {def.rarity}");
            lines.Add($"Quality {quality:0}{(tier != null ? $" ({tier.name})" : "")} · Energy {Mathf.Min(energy, max):N0} / {max:N0}");
            lines.Add($"Occupies {Mathf.Max(1, def.slotCost)} soul slot{(def.slotCost > 1 ? "s" : "")} · Concepts: {def.concepts.Count} (hidden until discovered)");
            var names = new List<string>();
            foreach (string al in def.alignments)
            {
                AlignmentDef a = c.Alignment(al);
                names.Add(a != null ? a.Name : al);
            }
            if (names.Count > 0) lines.Add("Alignment: " + string.Join(", ", names));
            lines.Add("Mages absorb it by using it or from the Rune Soul window.");
        }

        public override void ValidateItem(List<string> errors, List<string> warnings)
        {
            base.ValidateItem(errors, warnings);
            RuneDef def = RuneMagicDatabase.Instance.Catalog.Rune(runeId);
            if (string.IsNullOrWhiteSpace(runeId)) errors?.Add("It names no rune (Rune Id).");
            else if (def == null) errors?.Add($"The Rune Magic Database has no rune '{runeId}'.");
            else if (rank < def.minRank || rank > def.maxRank) warnings?.Add($"Rank {rank} is outside {def.Name}'s ranks ({def.minRank}-{def.maxRank}).");
        }
    }
}
