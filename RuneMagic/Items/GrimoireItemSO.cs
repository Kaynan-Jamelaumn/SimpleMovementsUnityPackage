using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// A grimoire in an inventory or on the ground: made at runtime from its id (<c>grimoire:&lt;serial&gt;:&lt;author&gt;</c>, see
    /// <see cref="GrimoireItems"/>); its pages are in the world's ledger under the serial, so the item can change hands (a trade, a
    /// drop, a loot) and whoever holds it reads it. Using it opens the book (only a Mage can read it).
    /// </summary>
    public class GrimoireItemSO : ItemSO, IRuntimeItem
    {
        [Header("Grimoire")]
        [SerializeField] private string serial = "";
        [SerializeField] private string author = "";

        [System.NonSerialized] private string runtimeId;

        /// <summary>The item's id ("grimoire:3fa9c1d2e4:Aria"); null for an asset.</summary>
        public string RuntimeId => runtimeId;
        public string Serial => serial;
        public string Author => author;

        public GrimoireItemSO()
        {
            itemType = ItemType.Miscellaneous;
            stackMax = 1;
            weight = 1f;
            // Using it opens the book; it is never used up.
            maxDurability = 1;
            durability = 1;
            durabilityReductionPerUse = 0;
            shouldBeDestroyedOn0UsesLeft = false;
        }

        /// <summary>Makes this item the grimoire <paramref name="id"/> (runtime items).</summary>
        public void Setup(string id, string bookSerial, string authorTag)
        {
            runtimeId = id;
            serial = bookSerial;
            author = authorTag;
            GrimoireBalance g = RuneMagicDatabase.Instance.Catalog.Grimoire;
            string title = g != null && !string.IsNullOrWhiteSpace(g.bookName) ? g.bookName : "Grimoire";
            name = title + " of " + authorTag;
            description = "A Mage's book of runes. Its author imprints copies of the runes of their soul on its pages; any Mage who holds it can read it " +
                          "and absorb a print from a page (a print can be absorbed once, and holds no energy).";
            if (g != null)
            {
                weight = g.itemWeight;
                price = g.itemPrice;
                canBeSold = g.canBeSold;
            }
            icon = GrimoireArt.Icon();
            prefab = GrimoireArt.Model();
        }

        public override bool CanUseFromQuickSlot => true;

        /// <summary>A book has no uses to wear out.</summary>
        public override bool ShowsDurability => false;

        /// <summary>Using it opens the book for the user's soul (only rune users can read it; the server checks the rest).</summary>
        public override void UseItem(GameObject player, PlayerStatusController statusController)
        {
            base.UseItem(player, statusController);
            RuneSoul soul = player != null ? RuneSoul.For(player) : statusController != null ? RuneSoul.For(statusController) : null;
            if (soul != null)
                soul.RequestOpenGrimoire(runtimeId);
        }

        public override void AppendTooltip(List<string> lines)
        {
            base.AppendTooltip(lines);
            if (lines == null) return;
            lines.Add($"Grimoire of {author}");
            RuneSoul local = RuneSoul.Local;
            GrimoireView v = local != null ? local.GrimoireView : null;
            if (v != null && v.serial == serial && v.pages.Count > 0)
                lines.Add($"{v.Filled} / {v.pages.Count} pages written");
            lines.Add("Mages read it by using it, and absorb the prints on its pages.");
        }

        public override void ValidateItem(List<string> errors, List<string> warnings)
        {
            base.ValidateItem(errors, warnings);
            if (string.IsNullOrEmpty(serial)) errors?.Add("It has no grimoire serial.");
        }
    }
}
