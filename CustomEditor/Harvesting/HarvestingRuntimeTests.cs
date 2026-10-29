#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HarvestingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of harvesting's place in the game, on the built-in creatures: every material is an item that every
    /// machine makes the same from its id; skinned hides become hides of the tannery, sized like the crafting system's; the
    /// creatures' body parts (with their weak points) come from their anatomy and lead back to it; the materials feed
    /// substances the Alchemy Database knows; and the tool presets each do best the work they are made for.
    /// </summary>
    public class HarvestingRuntimeTests
    {
        private HarvestDatabase builtIn;

        [SetUp]
        public void UseBuiltInCreatures()
        {
            // Items and body parts made earlier (a Play session, the project's own creatures) are keyed by creature id.
            ClearCaches();
            builtIn = ScriptableObject.CreateInstance<HarvestDatabase>();
            HarvestDatabase.Override(builtIn);
        }

        [TearDown]
        public void Restore()
        {
            HarvestDatabase.Override(null);
            Object.DestroyImmediate(builtIn);
            ClearCaches();
        }

        private static void ClearCaches()
        {
            HarvestItems.ClearCaches();
            HarvestBodyParts.ClearCaches();
            HarvestArt.ClearCaches();
        }

        [Test]
        public void EveryMaterialIsAnItemMadeFromItsId()
        {
            Assert.AreEqual(HarvestDefaults.Anatomies().Count, HarvestDatabase.Instance.Anatomies.Count);
            foreach (HarvestAnatomyData a in HarvestDatabase.Instance.Anatomies)
            foreach (HarvestMaterial m in a.materials)
            {
                ItemSO item = HarvestItems.ItemFor(a, m);
                Assert.IsNotNull(item, $"{a.id}/{m.id}");
                string id = HarvestItems.IdOf(a.id, m.id);
                Assert.AreSame(item, HarvestItems.Resolve(id), $"{id}: one item per id");
                Assert.AreEqual(id, ((IRuntimeItem)item).RuntimeId);
                Assert.AreEqual(m.IsFood, item is HarvestedFoodSO, $"{id}: food is eaten");
                Assert.IsTrue(HarvestItems.KeepsQuality(item), $"{id}: each piece keeps its quality");
                Assert.AreEqual(m.Name, item.Name);
            }
            Assert.IsNull(HarvestItems.Resolve("harvest:bear/no_such_thing"));
            Assert.IsNull(HarvestItems.Resolve("harvest:unicorn/horn"));
            Assert.IsNull(HarvestItems.Resolve("bear/claws"));
        }

        [Test]
        public void PiecesKeepTheirQualityAndMarks()
        {
            HarvestAnatomyData a = HarvestDefaults.Anatomy("wolf");
            ItemSO fangs = HarvestItems.ItemFor(a, a.Material("fangs"));
            List<int> codes = HarvestItems.Codes(fangs, 3, 0.83, HarvestMarks.Cracked);
            Assert.AreEqual(3, codes.Count);
            Assert.IsTrue(HarvestItemCode.TryDecode(codes[0], out double quality, out HarvestMarks marks));
            Assert.AreEqual(0.83, quality, 0.002);
            Assert.AreEqual(HarvestMarks.Cracked, marks & HarvestMarks.Cracked);
            Assert.IsNull(HarvestItems.Codes(ScriptableObject.CreateInstance<MaterialSO>(), 2, 0.5, HarvestMarks.None), "a plain item keeps nothing");
        }

        [Test]
        public void EachBiteOfFoodEatsOnePiece()
        {
            HarvestAnatomyData a = HarvestDefaults.Anatomy("bear");
            var food = (HarvestedFoodSO)HarvestItems.ItemFor(a, a.Material("haunch"));
            // A bite takes a copy once its value is used up (InventoryItem.durability - the per-use loss <= 0): it must
            // outweigh any value a piece can hold, or a spoiling, rough or soiled piece lasts many bites.
            int every = HarvestItemCode.Encode(1, (HarvestMarks)HarvestMarksInfo.Mask);
            foreach (int code in new[] { HarvestItemCode.Encode(0.5, HarvestMarks.Spoiling), HarvestItemCode.Encode(0.3, HarvestMarks.Spoiled | HarvestMarks.Rough), every })
            {
                float durability = code;
                durability -= food.DurabilityReductionPerUse;
                Assert.LessOrEqual(durability, 0f, $"a piece valued {code} goes in one bite");
            }
        }

        [Test]
        public void MerchantsPayEachPieceByItsOwnQuality()
        {
            HarvestAnatomyData a = HarvestDefaults.Anatomy("drake");
            ItemSO heartstone = HarvestItems.ItemFor(a, a.Material("heartstone"));
            ItemCategoryDatabase db = ItemCategoryDatabase.CreateDefault();
            var merchantGo = new GameObject("Merchant");
            var stackGo = new GameObject("Stack");
            try
            {
                Merchant m = merchantGo.AddComponent<Merchant>();
                var so = new UnityEditor.SerializedObject(m);
                so.FindProperty("categoryDatabase").objectReferenceValue = db;
                so.ApplyModifiedPropertiesWithoutUndo();
                InventoryItem stack = stackGo.AddComponent<InventoryItem>();
                // The next copy is the last value: pristine on top of a fair one and a ruined one.
                stack.Initialize(heartstone, 3, new[] { HarvestItemCode.Encode(0.05, HarvestMarks.Cracked), HarvestItemCode.Encode(0.5, HarvestMarks.None),
                    HarvestItemCode.Encode(1, HarvestMarks.None) });
                int top = m.GetSellPrice(stack), second = m.GetSellPrice(stack, null, 1), third = m.GetSellPrice(stack, null, 2);
                Assert.Greater(top, second);
                Assert.Greater(second, third, "several sold at once are each paid by their own quality");
                Assert.AreEqual(top, m.GetSellPrice(stack, null, 0));
            }
            finally
            {
                Object.DestroyImmediate(stackGo);
                Object.DestroyImmediate(merchantGo);
                foreach (ItemCategory c in db.Categories) Object.DestroyImmediate(c);
                Object.DestroyImmediate(db);
            }
        }

        [Test]
        public void SkinnedHidesBecomeTanneryHides()
        {
            // The crafting defaults with the hides harvesting adds to the Crafting Database (troll hide, snakeskin).
            CraftContent content = CraftDefaults.Content();
            content.hides.AddRange(HarvestCraftHides.All());
            CraftRules rules = CraftRules.Compile(new CraftSimulationSettings(), content);
            int hides = 0;
            foreach (HarvestAnatomyData a in HarvestDefaults.Anatomies())
            foreach (HarvestMaterial m in a.materials)
            {
                if (string.IsNullOrWhiteSpace(m.craftHide)) continue;
                hides++;
                CraftHideDefinition d = rules.Hide(m.craftHide);
                Assert.IsNotNull(d, $"{a.id}/{m.id}: the Crafting Database has no hide '{m.craftHide}'");
                // The tannery lays hides out with the spine across: its width is the pelt's length.
                Assert.AreEqual(d.height, a.peltSize.x, $"{a.id}: pelt width = the {d.id} hide's height");
                Assert.AreEqual(d.width, a.peltSize.y, $"{a.id}: pelt length = the {d.id} hide's width");
                HarvestCondition whole = HarvestCondition.Create(a, 9);
                CraftPiece piece = HarvestHides.Piece(m, whole, 1.0, rules);
                Assert.IsNotNull(piece, $"{a.id}: a tannery hide");
                Assert.IsNotNull(piece.hide);
                float present = piece.hide.cells.Count(c => c.present) / (float)piece.hide.cells.Count;
                Assert.Greater(present, 0.5f, $"{a.id}: most of an unhurt hide comes off");
            }
            Assert.GreaterOrEqual(hides, 8, "the skinned creatures give tannery hides");
        }

        [Test]
        public void BodyPartsComeFromTheAnatomyAndLeadBack()
        {
            foreach (HarvestAnatomyData a in HarvestDefaults.Anatomies())
            {
                BodyPartProfile p = HarvestBodyParts.ProfileFor(a);
                Assert.IsNotNull(p, a.id);
                Assert.AreEqual(a.regions.Count, p.parts.Count, $"{a.id}: a part for every region");
                foreach (BodyPartDefinition part in p.parts)
                    Assert.GreaterOrEqual(a.RegionOfPart(part.name), 0, $"{a.id}: the part {part.name} names its region");
                foreach (HarvestRegion r in a.regions.Where(r => r.weakPoint))
                {
                    BodyPartDefinition part = p.parts.First(x => a.RegionOfPart(x.name) == a.RegionIndex(r.id));
                    Assert.Greater(part.damageMultiplier, 1f, $"{a.id}: {r.id} is a weak point");
                }
            }
            Assert.IsTrue(HarvestDefaults.Anatomies().All(a => a.regions.Any(r => r.weakPoint)), "every creature has somewhere to aim");
        }

        [Test]
        public void TheInspectionShowsWhatGrowsOnPartsWithoutHide()
        {
            // A griffon's wings have no hide cells: the inspection's Hide is the small hide their feathers grow on.
            var g = new HarvestAnatomyData { id = "griffon", displayName = "Griffon", plan = HarvestBodyPlan.Quadruped, peltSize = new Vector2Int(12, 16) };
            g.regions = HarvestDefaults.QuadrupedRegions(wings: true);
            g.materials.Add(HarvestBuild.Material("pelt", "Griffon pelt", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "pelt"));
            g.materials.Add(HarvestBuild.Material("feathers", "Flight feathers", HarvestMaterialKind.Feathers, HarvestMethod.Skinning, "feathers")
                .From("wing_l").From("wing_r").Yield(20f, 40f, "feather"));
            HarvestCondition k = HarvestCondition.Create(g, 3);
            int wing = g.RegionIndex("wing_l");
            Assert.AreEqual(1f, HarvestInspectView.Value(g, k, wing, HarvestInspectLayer.Hide), 1e-6f, "whole until the fight reaches it");
            k.ApplyHit(g, HarvestSettings.Defaults(), new HarvestHit { region = wing, form = DamageForm.Pierce, severity = 0.1f, u = -1f, v = -1f, source = "test" });
            float hide = HarvestInspectView.Value(g, k, wing, HarvestInspectLayer.Hide);
            Assert.Less(hide, 1f, "the arrow holed it");
            Assert.AreEqual((float)k.regions[wing].CoverCondition(), hide, 1e-6f);
            Assert.LessOrEqual(HarvestInspectView.Value(g, k, wing, HarvestInspectLayer.Overall), hide);
        }

        [Test]
        public void MaterialsFeedSubstancesAlchemyKnows()
        {
            var db = ScriptableObject.CreateInstance<AlchemyDatabase>();
            try
            {
                AlchemyDefaults.Fill(db);
                AlchemyRules rules = db.Rules;
                int releases = 0;
                foreach (HarvestAnatomyData a in HarvestDefaults.Anatomies())
                foreach (HarvestMaterial m in a.materials)
                foreach (HarvestAlchemyRelease r in m.alchemy)
                {
                    releases++;
                    Assert.IsNotNull(rules.Substance(r.substance), $"{a.id}/{m.id}: no substance '{r.substance}'");
                    AlchemyIngredientProfile profile = HarvestedItemInfo.AlchemyProfile(m);
                    Assert.IsNotNull(profile, $"{a.id}/{m.id}: an alchemy ingredient");
                }
                Assert.Greater(releases, 5);
            }
            finally
            {
                Object.DestroyImmediate(db);
            }
        }

        [Test]
        public void EachToolDoesBestWhatItIsMadeFor()
        {
            var expected = new Dictionary<HarvestToolPreset, HarvestToolKind>
            {
                [HarvestToolPreset.SkinningKnife] = HarvestToolKind.Skinning,
                [HarvestToolPreset.ButcherKnife] = HarvestToolKind.Butchering,
                [HarvestToolPreset.Cleaver] = HarvestToolKind.Chopping,
                [HarvestToolPreset.Lancet] = HarvestToolKind.Fine,
                [HarvestToolPreset.BoneSaw] = HarvestToolKind.Saw,
                [HarvestToolPreset.Scraper] = HarvestToolKind.Scraper,
                [HarvestToolPreset.Pliers] = HarvestToolKind.Pliers,
            };
            foreach (KeyValuePair<HarvestToolPreset, HarvestToolKind> e in expected)
            {
                HarvestToolSO tool = ScriptableObject.CreateInstance<HarvestToolSO>();
                try
                {
                    tool.ApplyPreset(e.Key);
                    HarvestToolProfile p = tool.Profile(null, "test");
                    Assert.AreEqual(1f, p.fit.For(e.Value), 1e-4, $"{e.Key} is made for {e.Value}");
                    Assert.AreEqual(1f, p.sharpness, 1e-4, "a new tool is keen");
                    Assert.IsFalse(string.IsNullOrEmpty(tool.Name));
                }
                finally
                {
                    Object.DestroyImmediate(tool);
                }
            }
        }
    }
}
#endif
