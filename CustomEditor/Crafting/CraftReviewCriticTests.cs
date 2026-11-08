#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of what the final review of the crafting system found missing between its areas: the guide goes on
    /// from the furnace with the bloom it pulled (closed up at a forge's welding heat until it is a sound bar - taken to a
    /// forge from a smeltery, which has none), and a weld or a bloom at welding heat is struck on the anvil, where the
    /// hammer is.
    /// </summary>
    public class CraftReviewCriticTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything) => new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "critic");

        private static void SetHeat(CraftPiece p, double celsius)
        {
            foreach (MetalSegment s in p.segments)
                foreach (CraftZone z in new[] { s.edge, s.body })
                    z.temp = z.prevTemp = celsius;
        }

        private static double WeldingHeat(CraftWorkshop w, CraftPiece p) => CraftForging.WeldingHeat(w.Rules, p.Profile(w.Rules));

        private static CraftGuide.Step Step(CraftWorkshop w, CraftTools tools, string id)
        {
            CraftGuide.Step? s = CraftGuide.Next(w, tools, w.Piece(id), new CraftJournalData());
            Assert.IsTrue(s.HasValue, "the guide has a step for it");
            return s.Value;
        }

        // ------------------------------------------------------------------ the bloom, from the furnace to a sound bar
        [Test]
        public void Guide_ABloomPulledFromTheFurnace_IsClosedUpAtAForge_UntilItIsASoundBar()
        {
            CraftWorkshop w = Workshop();
            w.Furnace.bloom.Add(new CraftAmount("iron", 600));
            w.Furnace.fire.temperature = 1250;
            CraftGuide.Step? pull = CraftGuide.Next(w, CraftTools.Smeltery, null, new CraftJournalData());
            Assert.IsTrue(pull.HasValue && pull.Value.text.Contains("Pull the bloom"), "the furnace's own step");
            string id = w.ExtractBloom(out string why);
            Assert.IsNotNull(id, why);
            CraftPiece b = w.Piece(id);
            Assert.IsTrue(CraftLook.Bloom(w.Rules, b), "drawn from the furnace, it is a bloom");

            // a smeltery has no forge: the bloom is taken to one (no Go)
            CraftGuide.Step away = Step(w, CraftTools.Smeltery, id);
            Assert.AreEqual("", away.area, away.text);
            StringAssert.Contains("Take the bloom to a forge", away.text);

            // at a smithy: still at welding heat, it is struck on the anvil; cooled, it goes back into the fire
            SetHeat(b, WeldingHeat(w, b) - 20);
            CraftGuide.Step strike = Step(w, CraftTools.Smithy, id);
            Assert.AreEqual(CraftGuide.Anvil, strike.area, strike.text);
            StringAssert.Contains("flat blows", strike.text);
            CollectionAssert.Contains(CraftGuide.AreaKeys, strike.area, "an area the window has");
            SetHeat(b, 600);
            CraftGuide.Step reheat = Step(w, CraftTools.Smithy, id);
            Assert.AreEqual(CraftGuide.Forge, reheat.area, reheat.text);
            StringAssert.Contains("into the fire", reheat.text);
            b.location = CraftLocation.Fire;
            CraftGuide.Step inFire = Step(w, CraftTools.Smithy, id);
            Assert.AreEqual(CraftGuide.Forge, inFire.area, inFire.text);
            StringAssert.Contains("take it out and strike it", inFire.text);
            b.location = CraftLocation.Air;

            // hammered sound at welding heat, as the anvil does it: a bar of iron, and the bloom's step is gone
            for (int k = 0; k < 600 && CraftLook.Bloom(w.Rules, w.Piece(id)); k++)
            {
                CraftPiece p = w.Piece(id);
                SetHeat(p, WeldingHeat(w, p) - 20);
                foreach (MetalSegment s in p.segments) s.scale = 0;
                Assert.IsTrue(w.Strike(id, p.PositionOf((k * 7) % p.segments.Count), CraftStrikeTool.Flat, CraftStrikeFace.Flat, 1, 0, out why), why);
            }
            Assert.IsFalse(CraftLook.Bloom(w.Rules, w.Piece(id)), "closed up");
            CraftGuide.Step? after = CraftGuide.Next(w, CraftTools.Smithy, w.Piece(id), new CraftJournalData());
            Assert.IsFalse(after.HasValue && after.Value.text.Contains("bloom"), after.HasValue ? after.Value.text : "no step: a bar's path is the smith's own");
        }

        // ------------------------------------------------------------------ a casting is the same on every machine
        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        /// <summary>
        /// A casting made on the server (<paramref name="cast"/>) and the one a client's copy makes - restored from the
        /// server's snapshot, then applying the server's recorded operation, as <c>NetCraftStation</c>'s copies do - are the
        /// same piece, flaw for flaw.
        /// </summary>
        private static void SameOnEveryMachine(CraftWorkshop server, Func<CraftWorkshop, string> cast, string what)
        {
            CraftWorkshop client = CraftWorkshop.Restore(server.Rules, server.Capture(false));
            int before = server.History.Count;
            string id = cast(server);
            Assert.IsFalse(string.IsNullOrEmpty(id), what + ": it casts");
            Assert.AreEqual(before + 1, server.History.Count, what + ": one operation");
            Assert.IsTrue(client.ApplyRemote(server.History.Last()), what + ": the copy applies it");
            CraftPiece a = server.Piece(id), b = client.Piece(id);
            Assert.IsNotNull(a, what);
            Assert.IsNotNull(b, what + ": the copy has the casting");
            Assert.IsTrue(a.defects.Count > 0 || a.segments.Any(g => g.slag > 0), what + ": a casting with flaws placed here and there (the case that differed)");
            Assert.AreEqual(UnityEngine.JsonUtility.ToJson(a), UnityEngine.JsonUtility.ToJson(b), what + ": the same casting on the server and on the copy");
            Assert.AreEqual(server.Checksum(), client.Checksum(), what);
        }

        [Test]
        public void Casting_IsTheSameOnTheServerAndOnEveryCopy_ItsFlawsWhereTheyFell()
        {
            // poured from the crucible, unskimmed and broken twice: dross in it and cold shuts, each where it fell
            CraftWorkshop w = Workshop(CraftTools.Forge | CraftTools.Crucible);
            foreach ((string element, double fraction) in Metal("Bronze"))
                w.Crucible.melt.Add(new CraftAmount(element, 900 * fraction));
            w.Crucible.temperature = CraftSmelting.MeltProfile(w.Rules, w.Crucible).Liquidus + 80;
            w.Crucible.dross = 25;
            SameOnEveryMachine(w, s => s.Pour("ingot", 0.55, 0.6, 2, out _), "a dirty pour broken twice");
            // run out of the furnace with its slag over it
            CraftWorkshop f = Workshop(CraftTools.Smeltery);
            f.Furnace.pool.Add(new CraftAmount("copper", 700));
            f.Furnace.slag = 60;
            f.Furnace.fire.temperature = 1250;
            SameOnEveryMachine(f, s => s.TakeMelt("ingot", 0.55, 0.8, out _, 0, 0, 1, 1), "run out of the furnace");
        }

        [Test]
        public void Guide_AWeldAtWeldingHeat_IsStruckOnTheAnvil_ACooledOneGoesBackToTheFire()
        {
            CraftWorkshop w = Workshop();
            string bar = w.AddPiece(CraftDefaults.Bar(w.Rules, 200, 30, 8, ("iron", 0.994), ("carbon", 0.006)));
            CraftPiece b = w.Piece(bar);
            foreach (MetalSegment s in b.segments) s.seamOpen = 0.8;
            SetHeat(b, WeldingHeat(w, b) - 20);
            CraftGuide.Step strike = Step(w, CraftTools.Smithy, bar);
            Assert.AreEqual(CraftGuide.Anvil, strike.area, strike.text);
            StringAssert.Contains("strike the seam shut", strike.text);
            SetHeat(b, 500);
            CraftGuide.Step again = Step(w, CraftTools.Smithy, bar);
            Assert.AreEqual(CraftGuide.Forge, again.area, again.text);
            StringAssert.Contains("welding heat in the fire", again.text);
        }
    }
}
#endif
