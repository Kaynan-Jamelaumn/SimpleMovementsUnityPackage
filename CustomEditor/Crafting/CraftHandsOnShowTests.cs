#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the rules behind what the 3D work places show (wave 2, the hands-on presentation): which way a
    /// bend goes - a forged bevel and the quench both curve a single edge toward its spine, the sign every drawing and model
    /// reads.
    /// </summary>
    public class CraftHandsOnShowTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything) => new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "handson");

        private static string Bar(CraftWorkshop w, string metal, double length = 200, double width = 30, double thickness = 6) =>
            w.AddPiece(CraftDefaults.Bar(w.Rules, length, width, thickness, CraftDefaults.StockMetals().First(m => m.name == metal).composition));

        private static double Bend(CraftWorkshop w, string id) => w.Piece(id).segments.Sum(s => s.bend);

        // ------------------------------------------------------------------ which way it bends
        [Test]
        public void ForgedBevel_CurvesASingleEdge_TowardTheSpine_AsTheQuenchDoes()
        {
            // Tilted blows forge a bevel on the face that is up: the stretched edge grows longer than the spine.
            CraftWorkshop w = Workshop();
            string blank = Bar(w, "Spring steel");
            w.Piece(blank).edges = 1;
            for (int k = 0; k < 12; k++)
            {
                foreach (MetalSegment s in w.Piece(blank).segments) s.edge.temp = s.body.temp = 1000; // kept at a forging heat
                Assert.IsTrue(w.Strike(blank, 0.2 + 0.05 * k, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 0.9, 0.8, out string why), why);
            }
            Assert.Greater(Bend(w, blank), 1, "a forged bevel curves a single-edged blade toward its spine: a positive bend, as a sabre's");

            // The other face up bevels the other face: still the edge that grows, still toward the spine.
            CraftWorkshop w2 = Workshop();
            string other = Bar(w2, "Spring steel");
            w2.Piece(other).edges = 1;
            Assert.IsTrue(w2.TurnPiece(other, out string turned), turned);
            for (int k = 0; k < 12; k++)
            {
                foreach (MetalSegment s in w2.Piece(other).segments) s.edge.temp = s.body.temp = 1000;
                Assert.IsTrue(w2.Strike(other, 0.2 + 0.05 * k, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 0.9, 0.8, out string why), why);
            }
            Assert.Greater(Bend(w2, other), 1, "turned over, the bevel still curves it toward the spine");

            // A single edge hardened upright and moved up and down: its curve goes the same way.
            CraftWorkshop w3 = Workshop();
            string blade = Bar(w3, "Spring steel", 200, 30, 5);
            w3.Piece(blade).edges = 1;
            foreach (MetalSegment s in w3.Piece(blade).segments) { s.edge.temp = s.body.temp = 860; s.edge.aust = s.body.aust = 1; s.edge.pearl = s.body.pearl = 0; }
            Assert.IsTrue(w3.MovePiece(blade, CraftLocation.Quench, out string into, 1, 0, 9999, 0, 0), into);
            w3.Agitate(blade, CraftQuenchMotion.UpDown, out _);
            w3.RunSteps(400);
            Assert.Greater(Bend(w3, blade), 0, "the quench curves a single edge toward the spine too");
        }
    }
}
#endif
