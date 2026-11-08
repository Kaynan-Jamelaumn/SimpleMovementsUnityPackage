#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the guide (<see cref="CraftGuide"/>): every default design walked from its parts to its trial at
    /// each kind of default station that makes it, each step one the station can do (the Go lands on an area and a place it
    /// has) and the bench's own rules agreeing at the end; and representative pieces of every area - a stone, a hide, cloth,
    /// fibre and yarn, ore and what the furnace holds, metal, wood - whose steps follow the model's thresholds and order.
    /// The areas are window code the tests cannot reach: <see cref="Here"/> mirrors their own checks (each CraftMode's
    /// Available and HasPlace).
    /// </summary>
    public class CraftGuideTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools) => new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "guide");

        /// <summary>The default stations (the editor's presets).</summary>
        private static readonly (string name, CraftTools tools)[] Presets =
        {
            ("Smithy", CraftTools.Smithy), ("Smeltery", CraftTools.Smeltery), ("Tannery", CraftTools.Tannery), ("Leatherworks", CraftTools.Leatherworks),
            ("Woodshop", CraftTools.Woodshop), ("Tailor", CraftTools.Tailor), ("Armourer", CraftTools.Armourer), ("Spinnery", CraftTools.Spinnery),
            ("Lapidary", CraftTools.Lapidary), ("Jeweller", CraftTools.Jeweller),
        };

        private static bool Has(CraftTools tools, CraftTools t) => (tools & t) == t;

        /// <summary>Each area's places, by the names the guide uses (the areas' Place enums).</summary>
        private static readonly Dictionary<string, string[]> Places = new Dictionary<string, string[]>
        {
            [CraftGuide.Lapidary] = new[] { "Clean", "Test", "Identify", "Evaluate", "Orient", "Split", "Saw", "Grind", "Facet", "Polish" },
            [CraftGuide.Jeweller] = new[] { "Size", "Form", "Seat", "Solder", "Set", "Finish" },
            [CraftGuide.Fibres] = new[] { "Pick", "Scour", "Tease", "Card", "Comb" },
            [CraftGuide.Tailor] = new[] { "Spin", "Ply", "Skein", "Weave", "Fit", "Layout", "Needle", "Cut", "Sew" },
            [CraftGuide.Woodshop] = new[] { "Debark", "Split", "Rive", "Saw", "Bend", "Dry", "Finish" },
            [CraftGuide.Tannery] = new[] { "Beam", "Vats", "Frame" },
            [CraftGuide.Carve] = new[] { "Width", "Thickness", "Round", "Bore", "Saw" },
        };

        /// <summary>
        /// Would the window's Go land: is the area at a station with <paramref name="t"/>, and the place in it - as each area
        /// itself says (CraftXxxMode.Available and HasPlace, mirrored here).
        /// </summary>
        private static bool Here(CraftTools t, string area, string place)
        {
            bool p0 = string.IsNullOrEmpty(place);
            switch (area)
            {
                case CraftGuide.Forge: return Has(t, CraftTools.Forge) && p0;
                case CraftGuide.Anvil: return Has(t, CraftTools.Anvil) && p0;
                case CraftGuide.Grind: return (Has(t, CraftTools.Grinder) || Has(t, CraftTools.Polishing) || Has(t, CraftTools.Files) && (Has(t, CraftTools.Anvil) || Has(t, CraftTools.Forge))) && p0;
                case CraftGuide.Sharpen: return Has(t, CraftTools.Whetstone) && p0;
                case CraftGuide.Ore: return Has(t, CraftTools.OreBench) && p0;
                case CraftGuide.Furnace: return Has(t, CraftTools.Furnace) && p0;
                case CraftGuide.Crucible: return (Has(t, CraftTools.Crucible) || Has(t, CraftTools.Furnace)) && p0;
                case CraftGuide.Cutting: return Has(t, CraftTools.LeatherBench) && p0;
                case CraftGuide.Assemble: return (Has(t, CraftTools.AssemblyBench) || Has(t, CraftTools.LeatherBench)) && p0;
                case CraftGuide.Fibres: return Has(t, CraftTools.FibreBench) && (p0 || Places[area].Contains(place));
                case CraftGuide.Jeweller: return Has(t, CraftTools.JewelersBench) && (p0 || Places[area].Contains(place));
                case CraftGuide.Carve: return Has(t, CraftTools.Files) && (p0 || Places[area].Contains(place));
                case CraftGuide.Tannery:
                    if (!(Has(t, CraftTools.FleshingBeam) || Has(t, CraftTools.Vats) || Has(t, CraftTools.Frame) || Has(t, CraftTools.Rack) || Has(t, CraftTools.LeatherBench))) return false;
                    return p0 || place == "Beam" ? p0 || Has(t, CraftTools.FleshingBeam) || Has(t, CraftTools.LeatherBench)
                        : place == "Vats" ? Has(t, CraftTools.Vats) : place == "Frame" && (Has(t, CraftTools.Frame) || Has(t, CraftTools.Rack));
                case CraftGuide.Tailor:
                    if (!(Has(t, CraftTools.SpinningWheel) || Has(t, CraftTools.TailorTable) || Has(t, CraftTools.Loom))) return false;
                    if (p0) return true;
                    if (!Places[area].Contains(place)) return false;
                    return place == "Spin" || place == "Ply" ? Has(t, CraftTools.SpinningWheel) : place == "Weave" ? Has(t, CraftTools.Loom)
                        : place == "Skein" ? Has(t, CraftTools.SpinningWheel) || Has(t, CraftTools.TailorTable) : Has(t, CraftTools.TailorTable);
                case CraftGuide.Woodshop:
                    if (!(Has(t, CraftTools.ChoppingBlock) || Has(t, CraftTools.Sawhorse) || Has(t, CraftTools.SteamBox) || Has(t, CraftTools.DryingStack) || Has(t, CraftTools.FinishingBench) || Has(t, CraftTools.ShavingHorse))) return false;
                    if (p0) return true;
                    return place == "Debark" ? Has(t, CraftTools.ShavingHorse) : place == "Split" || place == "Rive" ? Has(t, CraftTools.ChoppingBlock)
                        : place == "Saw" ? Has(t, CraftTools.Sawhorse) || Has(t, CraftTools.Files) : place == "Bend" ? Has(t, CraftTools.SteamBox)
                        : place == "Dry" ? Has(t, CraftTools.DryingStack) : place == "Finish" && Has(t, CraftTools.FinishingBench);
                case CraftGuide.Lapidary:
                    if (!(Has(t, CraftTools.GemBench) || Has(t, CraftTools.FacetingMachine) || Has(t, CraftTools.JewelersBench))) return false;
                    if (p0) return true;
                    if (!Places[area].Contains(place)) return false;
                    return place == "Facet" ? Has(t, CraftTools.FacetingMachine) : place == "Polish" ? Has(t, CraftTools.FacetingMachine) || Has(t, CraftTools.Polishing)
                        : place == "Grind" ? Has(t, CraftTools.GemBench) || Has(t, CraftTools.FacetingMachine)
                        : place == "Clean" || place == "Test" || place == "Identify" || place == "Evaluate" ? Has(t, CraftTools.GemBench) || Has(t, CraftTools.JewelersBench)
                        : Has(t, CraftTools.GemBench);
                default:
                    return false;
            }
        }

        /// <summary>The step is there, names a known area (or none: done elsewhere) and a place the area has.</summary>
        private static CraftGuide.Step Known(CraftGuide.Step? s, string what)
        {
            Assert.IsTrue(s.HasValue, $"{what}: no step");
            CraftGuide.Step st = s.Value;
            if (st.area.Length > 0)
            {
                CollectionAssert.Contains(CraftGuide.AreaKeys, st.area, $"{what}: an area the window has");
                if (st.place.Length > 0)
                    Assert.IsTrue(Places.TryGetValue(st.area, out string[] names) && names.Contains(st.place), $"{what}: {st.area} has no place {st.place}");
            }
            return st;
        }

        /// <summary>The step is the one expected and the window's Go lands on it at this station.</summary>
        private static CraftGuide.Step Go(CraftGuide.Step? s, CraftTools t, string area, string place = "", string text = null)
        {
            CraftGuide.Step st = Known(s, text ?? area);
            Assert.AreEqual(area, st.area, st.text);
            Assert.AreEqual(place, st.place, st.text);
            if (text != null) StringAssert.Contains(text, st.text);
            Assert.IsTrue(Here(t, st.area, st.place), $"Go lands: {st.text} ({st.area} → {st.place})");
            return st;
        }

        /// <summary>A step done somewhere else: no area, so no Go.</summary>
        private static void Away(CraftGuide.Step? s, string text)
        {
            CraftGuide.Step st = Known(s, text);
            Assert.AreEqual("", st.area, st.text);
            StringAssert.Contains(text, st.text);
        }

        private static CraftGuide.Step? Next(CraftWorkshop w, CraftTools t, string id, Func<string, bool> tried = null) =>
            CraftGuide.Next(w, t, id != null ? w.Piece(id) : null, new CraftJournalData(), tried);

        // ------------------------------------------------------------------ every default design, from its parts to its trial
        [Test]
        public void EveryDefaultDesign_IsGuidedFromItsPartsToItsTrial_AtEveryStationThatMakesIt()
        {
            CraftRules rules = Rules();
            int walked = 0;
            foreach (CraftDesign d in rules.Designs)
            {
                var at = Presets.Where(p => CraftAssembly.MadeAt(rules, d, p.tools, out _)).ToList();
                Assert.IsNotEmpty(at, $"{d.id} is made at some default station");
                foreach ((string name, CraftTools tools) in at)
                {
                    Walk(rules, d, name, tools);
                    walked++;
                }
            }
            Assert.Greater(walked, 100);
        }

        /// <summary>Starts the item and does each step the guide names (filling its state as the work would) until it says it is done.</summary>
        private static void Walk(CraftRules rules, CraftDesign d, string station, CraftTools tools)
        {
            CraftWorkshop w = Workshop(tools);
            string asm = w.StartAssembly(d.id, out string why);
            Assert.IsNotNull(asm, $"{d.id} at the {station}: {why}");
            var tried = new HashSet<string>();
            var said = new List<string>();
            for (int n = 0; n < 60; n++)
            {
                CraftPiece a = w.Piece(asm);
                CraftGuide.Step st = Known(Next(w, tools, asm, tried.Contains), $"{d.id} at the {station} after [{string.Join(" | ", said)}]");
                said.Add(st.text);
                if (st.text == "Take it: it is done")
                {
                    Assert.AreEqual("", st.area);
                    return;
                }
                Assert.IsTrue(Here(tools, st.area, st.place), $"{d.id} at the {station}: \"{st.text}\" ({st.area} → {st.place}) is not here");
                Assert.IsFalse(said.Count > 2 && said[said.Count - 2] == st.text && said[said.Count - 3] == st.text, $"{d.id} at the {station}: stuck on \"{st.text}\"");
                Do(rules, w, a, d, st, tried);
            }
            Assert.Fail($"{d.id} at the {station}: never done: {string.Join(" | ", said)}");
        }

        /// <summary>What the step asks, done to the item's state (the guide reads only the state).</summary>
        private static void Do(CraftRules rules, CraftWorkshop w, CraftPiece a, CraftDesign d, CraftGuide.Step st, HashSet<string> tried)
        {
            string t = st.text;
            if (t.StartsWith("Fit its parts: "))
            {
                foreach (CraftComponentSpec c in d.components)
                {
                    if (c.optional || c.supply) continue;
                    if (!t.Contains(c.Name.ToLowerInvariant() + " first")) continue;
                    for (int i = 0; i < Math.Max(1, c.count); i++)
                        if (a.assembly.PieceIn(c.role, i) == null) Fit(rules, w, a, c, i);
                    return;
                }
                Assert.Fail($"no part for \"{t}\"");
            }
            if (t.StartsWith("Add the "))
            {
                CraftComponentSpec c = d.components.First(x => x.supply && t == $"Add the {x.Name.ToLowerInvariant()}");
                a.assembly.supplies.RemoveAll(v => v.name == c.role);
                a.assembly.supplies.Add(new CraftVariable { name = c.role, value = Math.Max(1, c.supplyCount) });
                return;
            }
            if (t.StartsWith("Fit another set of "))
            {
                CraftComponentSpec c = d.components.First(x => x.kind == CraftPieceKind.Fasteners && t.StartsWith($"Fit another set of {x.Name.ToLowerInvariant()}:"));
                w.Piece(a.assembly.PieceIn(c.role)).fasteners.count = Math.Max(1, c.fastenerCount) + 40;
                return;
            }
            if (t.StartsWith("Cut the setting's seat"))
            {
                CraftComponentSpec c = d.components.First(x => x.function == CraftComponentFunction.Setting);
                w.Piece(a.assembly.PieceIn(c.role)).SetTag(CraftJewelry.SeatTag, 1);
                return;
            }
            if (st.area == CraftGuide.Grind)
            {
                foreach (MetalSegment s in w.Piece(st.piece).segments) { s.bevelDepthL = s.bevelDepthR = s.thickness * 0.35; s.bevelWidthL = s.bevelWidthR = s.thickness; }
                return;
            }
            if (st.area == CraftGuide.Sharpen)
            {
                foreach (MetalSegment s in w.Piece(st.piece).segments) { s.apex = 1; s.burr = 0; s.chip = 0; }
                return;
            }
            if (t.StartsWith("Buff it"))
            {
                a.assembly.finish = 0.6;
                return;
            }
            if (t == "Finish it")
            {
                Assert.IsTrue(w.Finish(a.id, out string why), $"{d.id}: the guide says finish it, the bench says: {why}");
                return;
            }
            if (t.StartsWith("Try it"))
            {
                tried.Add(a.id);
                return;
            }
            if (t.StartsWith("The ") && t.Contains("too tight"))
            {
                foreach (JointState s in a.assembly.joints) if (s.gap < 0) s.gap = 0.2;
                return;
            }
            // a joint's work: the first joint still to make, as the guide picks it
            foreach (CraftJointSpec j in d.joints)
            {
                JointState s = a.assembly.Joint(j.id);
                if (s != null && CraftAssembly.JointDone(j, s) || !CraftAssembly.JointApplies(w, a, d, j)) continue;
                Assert.IsTrue(t.ToLowerInvariant().Contains(j.Name.ToLowerInvariant()) || j.kind == CraftJointKind.Wrap && t.StartsWith("String it") || j.kind == CraftJointKind.Buckle,
                    $"{d.id}: \"{t}\" names the joint {j.Name}");
                Done(j, s);
                return;
            }
            Assert.Fail($"{d.id}: nothing to do for \"{t}\"");
        }

        private static void Fit(CraftRules rules, CraftWorkshop w, CraftPiece a, CraftComponentSpec c, int index)
        {
            CraftPiece p;
            switch (c.kind)
            {
                case CraftPieceKind.Wood:
                    // sawn to the part's length, as the guide asks before a wooden part is fitted
                    double length = CraftWoodwork.LengthWanted(c, out double min, out double max) ? (min + max) * 0.5 : 300;
                    p = CraftDefaults.WoodBlank(rules, "ash", length, 30, 30);
                    break;
                case CraftPieceKind.Hide:
                    p = CraftLeather.NewHide(rules, c.leather != null && c.leather.fabric == CraftFabric.Cloth ? "wool_cloth" : "cow", 7 + index);
                    p.hide.pattern = c.leather?.pattern ?? "strap";
                    p.hide.dried = true;
                    break;
                case CraftPieceKind.Gem:
                    p = CraftLapidary.NewRough(rules, "garnet", 11 + index);
                    p.gem.stage = CraftGemStage.Polished;
                    break;
                case CraftPieceKind.Fasteners: p = CraftDefaults.FastenerSet(rules, c.fastener, Math.Max(1, c.fastenerCount), 5, ("iron", 1.0)); break;
                default: p = CraftDefaults.Bar(rules, 300, 30, 5, ("iron", 0.994), ("carbon", 0.006)); break;
            }
            string id = w.AddPiece(p);
            CraftPiece live = w.Piece(id);
            live.location = CraftLocation.Assembled;
            live.parent = a.id;
            a.assembly.parts.Add(new AssemblyPart { role = c.role, index = index, piece = id });
        }

        private static void Done(CraftJointSpec j, JointState s)
        {
            int count = Math.Max(1, j.count);
            switch (j.kind)
            {
                case CraftJointKind.Fit: s.gap = Math.Max(0, s.gap); break;
                case CraftJointKind.Peen: s.peen = 1.1; break;
                case CraftJointKind.Rivet: s.rivets = count; s.holes = count; break;
                case CraftJointKind.Stitch: s.stitches = count; s.holes = count; break;
                case CraftJointKind.Wrap: s.turns = count; break;
                case CraftJointKind.Wedge: case CraftJointKind.Socket: s.wedge = 0.8; break;
                case CraftJointKind.Buckle: s.rivets = count; s.stitches = CraftAssembly.StitchesPerBuckle * count; break;
                case CraftJointKind.Solder: s.solder = 1; break;
                case CraftJointKind.BezelSet: s.security = 1; break;
                case CraftJointKind.ProngSet: s.security = 1; s.pushes = count; break;
            }
            s.done = true;
        }

        // ------------------------------------------------------------------ stones
        [Test]
        public void AStone_NamedWrongly_GoesOnToBePlanned_AndIsSawnOrGroundByTheModelsThresholds()
        {
            CraftTools lap = CraftTools.Lapidary;
            CraftWorkshop w = Workshop(lap);
            string id = w.AddPiece(CraftLapidary.NewRough(w.Rules, "garnet", 3));
            CraftPiece p = w.Piece(id);
            Go(Next(w, lap, id), lap, CraftGuide.Lapidary, "Clean", "Clean the crust off");
            for (int k = 0; k < p.gem.crust.Count; k++) p.gem.crust[k] = 0;
            Go(Next(w, lap, id), lap, CraftGuide.Lapidary, "Test");
            p.gem.looked = true;
            p.gem.hardLo = 7;
            Go(Next(w, lap, id), lap, CraftGuide.Lapidary, "Identify", "Name it");
            Assert.IsTrue(w.IdentifyGem(id, "amethyst", out string why), why);
            Assert.IsFalse(p.gem.identified, "named wrongly");
            Assert.IsFalse(w.IdentifyGem(id, "garnet", out why), "the name sticks");
            Go(Next(w, lap, id), lap, CraftGuide.Lapidary, "Evaluate", "plan the cut");
            p.gem.evaluated = true;
            Go(Next(w, lap, id), lap, CraftGuide.Lapidary, "Orient", "Plan the cut");
            p.gem.oriented = true;
            p.gem.cut = "round_brilliant";
            p.gem.windowR = 0.15;
            Assert.Greater(CraftLapidary.Excess(p.gem), CraftLapidary.GrindableExcess);
            Go(Next(w, lap, id), lap, CraftGuide.Lapidary, "Saw", "Saw (or split)");
            Assert.IsFalse(w.GrindGem(id, new List<float> { 0, 0.5f }, 1, 1, out why), "the wheel refuses what the guide sends to the saw");
            p.gem.windowR = 0.9;
            Assert.LessOrEqual(CraftLapidary.Excess(p.gem), CraftLapidary.GrindableExcess);
            Go(Next(w, lap, id), lap, CraftGuide.Lapidary, "Grind", "Grind it to its outline");
            p.gem.stage = CraftGemStage.Faceted;
            Go(Next(w, lap, id), lap, CraftGuide.Lapidary, "Polish");
            p.gem.stage = CraftGemStage.Polished;
            Away(Next(w, lap, id), "take it to a jeweller's bench");
            Go(Next(w, CraftTools.Jeweller, id), CraftTools.Jeweller, CraftGuide.Jeweller, "Seat", "Ready to set");
        }

        // ------------------------------------------------------------------ hides and cloth
        private static void Cells(CraftPiece p, Action<HideCell> set)
        {
            foreach (HideCell c in p.hide.cells) if (c != null && c.present) set(c);
        }

        [Test]
        public void AHide_FollowsThePathOverTheTannery_AndIsCutAtALeatherBench()
        {
            CraftTools tan = CraftTools.Tannery;
            CraftWorkshop w = Workshop(tan | CraftTools.LeatherBench);
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "cow", 2));
            CraftPiece p = w.Piece(id);
            Cells(p, c => c.flesh = 0.4);
            Go(Next(w, tan, id), tan, CraftGuide.Tannery, "Beam", "Flesh it");
            Cells(p, c => c.flesh = 0);
            Go(Next(w, tan, id), tan, CraftGuide.Tannery, "Vats", "Lime it");
            // in a vat of lime: it is liming; in plain water: lift it out
            w.Vats[0].agent = "lime"; w.Vats[0].volume = 1;
            p.location = CraftLocation.Vat; p.vat = 0;
            Go(Next(w, tan, id), tan, CraftGuide.Tannery, "Vats", "LOOSE passes 70%");
            w.Vats[0].agent = "water";
            Go(Next(w, tan, id), tan, CraftGuide.Tannery, "Vats", "Lift it out of the vat, then lime it");
            Cells(p, c => c.hairLoose = 0.8);
            w.Vats[0].agent = "lime";
            Go(Next(w, tan, id), tan, CraftGuide.Tannery, "Vats", "It is limed: lift it out");
            p.location = CraftLocation.Air;
            Go(Next(w, tan, id), tan, CraftGuide.Tannery, "Beam", "Scrape the hair");
            Cells(p, c => c.hair = 0);
            Go(Next(w, tan, id), tan, CraftGuide.Tannery, "Vats", "CORE passes 75%");
            Cells(p, c => c.tanC = 0.9);
            Go(Next(w, tan, id), tan, CraftGuide.Tannery, "Frame", "Stretch it in the frame");
            p.location = CraftLocation.Frame;
            Cells(p, c => c.moisture = 0.8);
            Go(Next(w, tan, id), tan, CraftGuide.Tannery, "Frame", "the DRY bar's gold band");
            Cells(p, c => c.moisture = (CraftLeather.StakeWet + CraftLeather.DryAt) / 2);
            Go(Next(w, tan, id), tan, CraftGuide.Tannery, "Frame", "Stake it now, while it is damp");
            Cells(p, c => { c.moisture = 0.1; c.stiff = 0.6; });
            p.hide.dried = true;
            Go(Next(w, tan, id), tan, CraftGuide.Tannery, "Frame", "stake it supple");
            Cells(p, c => c.stiff = 0.1);
            Go(Next(w, tan, id), tan, CraftGuide.Tannery, "Frame", "take it down onto the bench");
            p.location = CraftLocation.Air;
            Go(Next(w, tan, id), tan, CraftGuide.Tannery, "Beam", "then take it to a leather bench");
            CraftTools lw = CraftTools.Leatherworks;
            Go(Next(w, lw, id), lw, CraftGuide.Cutting, "", "Cut its patterns");
            Cells(p, c => c.oil = 0.5);
            Go(Next(w, lw, id), lw, CraftGuide.Cutting, "", "Cut its patterns");
            Away(Next(w, tan, id), "Take it to a leather bench");
            p.hide.pattern = "cap";
            Go(Next(w, lw, id), lw, CraftGuide.Assemble, "", "Fit it into an item");
        }

        [Test]
        public void ARawHide_HungToDry_IsTakenDownForWhatItStillNeeds()
        {
            CraftTools tan = CraftTools.Tannery;
            CraftWorkshop w = Workshop(tan);
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 4));
            CraftPiece p = w.Piece(id);
            Cells(p, c => { c.flesh = 0; c.hair = 0; c.tanC = 0.1; });
            p.location = CraftLocation.Rack;
            Go(Next(w, tan, id), tan, CraftGuide.Tannery, "Frame", "Take it down onto the bench, then tan it");
        }

        [Test]
        public void Cloth_IsCutAtTheTailorsTable_OrAtALeatherBenchWithoutOne()
        {
            CraftWorkshop w = Workshop(CraftTools.Everything);
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, "wool_cloth", 5));
            Go(Next(w, CraftTools.Tailor, id), CraftTools.Tailor, CraftGuide.Tailor, "Layout", "Lay its patterns out");
            Go(Next(w, CraftTools.Leatherworks, id), CraftTools.Leatherworks, CraftGuide.Cutting, "", "Cut its patterns (along the grain)");
            Away(Next(w, CraftTools.Tannery, id), "tailor's table");
            Go(Next(w, CraftTools.Armourer, id), CraftTools.Armourer, CraftGuide.Tailor, "Layout");
        }

        // ------------------------------------------------------------------ fibre and yarn
        [Test]
        public void Fibre_IsMadeReady_ThenSpun_AndYarnWoven()
        {
            CraftTools sp = CraftTools.Spinnery;
            CraftWorkshop w = Workshop(CraftTools.Everything);
            string id = w.AddPiece(CraftFibre.NewFibre(w.Rules, "wool_fibre"));
            CraftPiece p = w.Piece(id);
            p.fibre.dirt = 0.5; p.fibre.cleaned = false;
            Go(Next(w, sp, id), sp, CraftGuide.Fibres, "Pick");
            p.fibre.cleaned = true; p.fibre.grease = 0.5; p.fibre.scoured = false;
            Go(Next(w, sp, id), sp, CraftGuide.Fibres, "Scour");
            p.fibre.scoured = true; p.fibre.wet = 0.6;
            Go(Next(w, sp, id), sp, CraftGuide.Fibres, "Scour", "drying");
            p.fibre.wet = 0; p.fibre.tangle = 0.6; p.fibre.opened = false;
            Go(Next(w, sp, id), sp, CraftGuide.Fibres, "Tease");
            p.fibre.opened = true; p.fibre.carded = false; p.fibre.combed = false;
            Go(Next(w, sp, id), sp, CraftGuide.Fibres, "Card");
            p.fibre.carded = true;
            Go(Next(w, sp, id), sp, CraftGuide.Tailor, "Spin");
            Go(Next(w, CraftTools.Tailor, id), CraftTools.Tailor, CraftGuide.Tailor, "Spin");
            string y = w.SpinFibre(id, 0.8, 0.5, 0.5, 30, out string why);
            Assert.IsNotNull(y, why);
            CraftPiece yarn = w.Piece(y);
            yarn.yarn.length = 2000;
            Go(Next(w, sp, y), sp, CraftGuide.Tailor, "Weave", "Weave it into cloth");
            yarn.yarn.length = CraftFibre.MetresPerCell * CraftFibre.MinWidth * 0.5;
            Go(Next(w, sp, y), sp, CraftGuide.Tailor, "Weave", "wind another bobbin on");
            // a tunic started here wants a body 6 cells wide and 6 rows long: 200 m weave only 2 such rows
            string tunic = w.StartAssembly("wool_tunic", out why);
            Assert.IsNotNull(tunic, why);
            yarn.yarn.length = 200;
            Go(Next(w, sp, y), sp, CraftGuide.Tailor, "Weave", "Too short for the wool tunic's tunic body (6 rows 6 wide; it weaves 2)");
            yarn.yarn.length = 2000;
            Go(Next(w, sp, y), sp, CraftGuide.Tailor, "Weave", "Weave it into cloth");
            yarn.yarn.measured = true;
            Go(Next(w, sp, y), sp, CraftGuide.Tailor, "Skein", "Reel it off");
        }

        // ------------------------------------------------------------------ ore and the furnace
        [Test]
        public void Ore_GoesToTheBench_TheFurnace_OrTheCrucible_AsItsMetalDemands()
        {
            CraftTools sm = CraftTools.Smeltery;
            CraftWorkshop w = Workshop(sm);
            string hem = w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 1, 1));
            CraftPiece h = w.Piece(hem);
            foreach (OreFragment f in h.ore.fragments) f.dirt = 0.3;
            Go(Next(w, sm, hem), sm, CraftGuide.Ore, "", "Crush it");
            foreach (OreFragment f in h.ore.fragments) f.dirt = 0;
            Go(Next(w, sm, hem), sm, CraftGuide.Furnace, "", "Charge it into the furnace");
            h.location = CraftLocation.Furnace;
            Go(Next(w, sm, hem), sm, CraftGuide.Furnace, "", "pull the bloom");
            Away(Next(w, CraftTools.Smithy, w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 1, 2))), "ore bench");
            // a sulfide is roasted first
            string gal = w.AddPiece(CraftOres.NewLump(w.Rules, "galena", 2, 1));
            foreach (OreFragment f in w.Piece(gal).ore.fragments) f.dirt = 0;
            Go(Next(w, sm, gal), sm, CraftGuide.Furnace, "", "roast it there first");
            w.Piece(gal).location = CraftLocation.Furnace;
            Go(Next(w, sm, gal), sm, CraftGuide.Furnace, "", "It is roasting");
            w.Piece(gal).ore.roasted = 1;
            Go(Next(w, sm, gal), sm, CraftGuide.Furnace, "", "run the metal off");
            // calamine's zinc boils off in the furnace: a copper melt in the crucible takes it
            string cal = w.AddPiece(CraftOres.NewLump(w.Rules, "calamine", 3, 1));
            foreach (OreFragment f in w.Piece(cal).ore.fragments) f.dirt = 0;
            Go(Next(w, sm, cal), sm, CraftGuide.Crucible, "", "molten copper");
            Assert.IsTrue(CraftSmelting.CrucibleTakes(w.Rules, w.Piece(cal), out string why), why);
            Away(Next(w, CraftTools.Smeltery & ~CraftTools.Crucible, cal), "crucible");
        }

        [Test]
        public void WithNothingPicked_TheFurnaceSaysWhatItHolds_OnceItsChargeIsSpent()
        {
            CraftTools sm = CraftTools.Smeltery;
            CraftWorkshop w = Workshop(sm);
            Assert.IsNull(Next(w, sm, null), "an empty furnace: nothing");
            w.Furnace.pool.Add(new CraftAmount("silver", 3));
            Go(Next(w, sm, null), sm, CraftGuide.Furnace, "", "Lift the bead out with the tongs");
            Assert.IsNull(w.TakeMelt("ingot", 0.5, 0.9, out string why), "too little to pour, as the guide says");
            w.Furnace.pool[0].grams = 400;
            w.Furnace.fire.temperature = 1200;
            Go(Next(w, sm, null), sm, CraftGuide.Crucible, "", "Run the metal");
            w.Furnace.fire.temperature = 300;
            Go(Next(w, sm, null), sm, CraftGuide.Furnace, "", "frozen");
            w.Furnace.pool.Clear();
            w.Furnace.bloom.Add(new CraftAmount("iron", 600));
            Go(Next(w, sm, null), sm, CraftGuide.Furnace, "", "Pull the bloom");
            string ore = w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 1, 1));
            w.Piece(ore).location = CraftLocation.Furnace;
            Assert.IsNull(Next(w, sm, null), "still smelting: the ore's own step says what comes next");
            Assert.IsNull(Next(w, CraftTools.Smithy, null), "no furnace, nothing");
        }

        // ------------------------------------------------------------------ metal
        [Test]
        public void Metal_AnOpenWeld_AMatte_AndSilverInLead_HaveTheirSteps()
        {
            CraftWorkshop w = Workshop(CraftTools.Everything);
            string bar = w.AddPiece(CraftDefaults.Bar(w.Rules, 200, 30, 8, ("iron", 0.994), ("carbon", 0.006)));
            CraftPiece b = w.Piece(bar);
            Assert.IsNull(Next(w, CraftTools.Smithy, bar), "a bar's path is the smith's own");
            foreach (MetalSegment s in b.segments) s.seamOpen = 0.8;
            Go(Next(w, CraftTools.Smithy, bar), CraftTools.Smithy, CraftGuide.Forge, "", "flux it again");
            foreach (MetalSegment s in b.segments) s.edge.temp = s.body.temp = 1300;
            Go(Next(w, CraftTools.Smithy, bar), CraftTools.Smithy, CraftGuide.Anvil, "", "strike the seam shut"); // at welding heat: struck on the anvil
            b.location = CraftLocation.Fire;
            Go(Next(w, CraftTools.Smithy, bar), CraftTools.Smithy, CraftGuide.Forge, "", "welding heat");
            string matte = w.AddPiece(CraftDefaults.Bar(w.Rules, 60, 30, 20, ("copper", 0.8), ("sulfur", 0.2)));
            Go(Next(w, CraftTools.Smeltery, matte), CraftTools.Smeltery, CraftGuide.Furnace, "", "Roast the matte");
            Away(Next(w, CraftTools.Smithy, matte), "furnace");
            w.Piece(matte).location = CraftLocation.Furnace;
            Go(Next(w, CraftTools.Smeltery, matte), CraftTools.Smeltery, CraftGuide.Furnace, "", "roasting");
            string lead = w.AddPiece(CraftDefaults.Bar(w.Rules, 60, 30, 20, ("lead", 0.996), ("silver", 0.004)));
            Go(Next(w, CraftTools.Smeltery, lead), CraftTools.Smeltery, CraftGuide.Furnace, "", "Cupel the lead: in the furnace with a bone-ash cupel");
            string wire = w.AddPiece(CraftDefaults.Bar(w.Rules, 120, 3, 2, ("silver", 0.925), ("copper", 0.075)));
            Go(Next(w, CraftTools.Jeweller, wire), CraftTools.Jeweller, CraftGuide.Jeweller, "Form");
        }

        // ------------------------------------------------------------------ wood
        [Test]
        public void Wood_IsDebarked_SplitOrRivenByItsSize_Bent_AndSeasoned()
        {
            CraftTools ws = CraftTools.Woodshop;
            CraftWorkshop w = Workshop(ws);
            string log = w.AddPiece(CraftDefaults.Log(w.Rules, "ash", 900, 200));
            Go(Next(w, ws, log), ws, CraftGuide.Woodshop, "Debark", "then split it");
            foreach (MetalSegment s in w.Piece(log).segments) s.bark = 0;
            w.Piece(log).wood.bark = 0;
            Go(Next(w, ws, log), ws, CraftGuide.Woodshop, "Split", "wedges");
            Assert.IsTrue(CraftWoodwork.SplitsWithWedges(w.Piece(log), out _));
            string small = w.AddPiece(CraftDefaults.Log(w.Rules, "ash", 900, 50));
            Go(Next(w, ws, small), ws, CraftGuide.Woodshop, "Debark", "then rive it");
            foreach (MetalSegment s in w.Piece(small).segments) s.bark = 0;
            Go(Next(w, ws, small), ws, CraftGuide.Woodshop, "Rive", "froe");
            Assert.IsTrue(CraftWoodwork.RivesWithFroe(w.Piece(small), out _));
            Away(Next(w, CraftTools.Woodshop & ~CraftTools.ChoppingBlock & ~CraftTools.ShavingHorse, small), "chopping block");
            // a snath wants its sweep before it seasons
            string snath = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 1500, 30, 30));
            Assert.IsTrue(w.SetIntent(snath, "scythe", "haft", out string why), why);
            CraftPiece sn = w.Piece(snath);
            sn.wood.moisture = 0.5;
            Go(Next(w, ws, snath), ws, CraftGuide.Woodshop, "Bend", "wants 25°");
            sn.location = CraftLocation.Steam;
            Go(Next(w, ws, snath), ws, CraftGuide.Woodshop, "Bend", "steam box");
            sn.location = CraftLocation.Air;
            sn.segments[1].bend = 24;
            Go(Next(w, ws, snath), ws, CraftGuide.Woodshop, "Dry", "It is green");
            Away(Next(w, CraftTools.Smithy, snath), "drying stack");
            sn.location = CraftLocation.Stack;
            Go(Next(w, ws, snath), ws, CraftGuide.Woodshop, "Dry", "seasoning");
            sn.wood.moisture = CraftWoodwork.DryEnough - 0.01;
            Go(Next(w, ws, snath), ws, CraftGuide.Woodshop, "Dry", "drag it off the stack");
            sn.location = CraftLocation.Air;
            // seasoned and bent (and no wider than its shape): sanded and sealed, if the woodworker likes, and then it is ready
            Go(Next(w, ws, snath), ws, CraftGuide.Woodshop, "Finish", "Sand the snath through the grits");
            sn.wood.lastGrit = 0.85;
            Go(Next(w, ws, snath), ws, CraftGuide.Woodshop, "Finish", "Seal the snath (optional)");
            sn.wood.oil = 0.3;
            Assert.IsNull(Next(w, ws, snath), "seasoned, bent, sanded and sealed: ready to fit");
        }

        // ------------------------------------------------------------------ an item, its thread and its fasteners
        [Test]
        public void AnItem_AsksForItsThread_AndAnotherSet_AsTheBenchWould()
        {
            CraftTools lw = CraftTools.Leatherworks;
            CraftWorkshop w = Workshop(lw);
            string asm = w.StartAssembly("leather_boots", out string why);
            CraftPiece a = w.Piece(asm);
            CraftDesign d = w.Rules.Design("leather_boots");
            foreach (CraftComponentSpec c in d.components.Where(x => !x.supply))
                for (int i = 0; i < c.count; i++) Fit(w.Rules, w, a, c, i);
            // no joint names the boots' thread, but the bench sews leather with the item's own: it is asked first
            Go(Next(w, lw, asm), lw, CraftGuide.Assemble, "", "Add the thread");
            CollectionAssert.Contains(CraftAssembly.Missing(w, a), "Thread: not added yet.");
            string belt = w.StartAssembly("sword_belt", out why);
            CraftPiece b = w.Piece(belt);
            CraftDesign bd = w.Rules.Design("sword_belt");
            foreach (CraftComponentSpec c in bd.components.Where(x => !x.supply))
                for (int i = 0; i < c.count; i++) Fit(w.Rules, w, b, c, i);
            w.Piece(b.assembly.PieceIn("buckles")).fasteners.count = 1;
            Go(Next(w, lw, belt), lw, CraftGuide.Assemble, "", "Fit another set of buckles: it holds 1 of 3");
            Assert.IsTrue(CraftAssembly.Missing(w, b).Any(m => m.StartsWith("Buckles: 1 of 3")), "the bench's rule");
        }
    }
}
#endif
