#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SimpleMovements.Validation;
using UnityEditor;
using UnityEngine;

namespace HousingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the housing checks on scene objects (temporary objects, destroyed after each test): areas are
    /// truthful - two plots may not overlap (plots sharing a fence far from the origin only touch), a house stays on its
    /// land, another object of the city may not stand on a plot (measured by what it really covers, its parts together,
    /// only below the roof, what the property holds left out), a foreign garden may not lie in one nor a garden belong to
    /// two, an inn room needs a bed and a chest and belongs to one inn, a realtor whose Listings are all missing lets
    /// nothing, duplicated ids are reported with a fix that asks.
    /// <para>Each test sees only its own objects (<see cref="HousingSceneObjects.Of"/>), under fresh Housing Settings: the
    /// project's settings and the scenes the user has open change nothing.</para>
    /// </summary>
    public class HousingValidationTests
    {
        private readonly List<GameObject> made = new List<GameObject>();
        private HousingSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<HousingSettings>();
            settings.Defaults();
            HousingSettings.Override(settings);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in made)
                if (go != null) Object.DestroyImmediate(go);
            made.Clear();
            HousingSettings.Override(null);
            if (settings != null) Object.DestroyImmediate(settings);
        }

        /// <summary>The test's own objects (nothing else of the open scenes).</summary>
        private HousingSceneObjects Scene() => HousingSceneObjects.Of(made.ToArray());

        private GameObject Make(string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.position = at;
            made.Add(go);
            return go;
        }

        private Property Plot(string name, Vector3 at, Vector2 land, Vector2 house)
        {
            Property p = Make(name, at).AddComponent<Property>();
            p.land = HousingGeometry.Rectangle(Vector2.zero, land);
            p.house = HousingGeometry.Rectangle(Vector2.zero, house);
            p.floors = 1;
            p.floorHeight = 3f;
            p.gardens.Clear();
            p.chests.Clear();
            p.beds.Clear();
            p.doors.Clear();
            p.zones.Clear();
            return p;
        }

        /// <summary>A box of the city (a cube with its Box Collider) standing with its middle at <paramref name="centre"/>.</summary>
        private GameObject Block(string name, Vector3 centre, Vector3 size, float yaw = 0f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            made.Add(go);
            go.name = name;
            go.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = size;
            return go;
        }

        private static IEnumerable<string> All(HousingOverlaps r) => r.errors.Concat(r.warnings);

        private static string Text(HousingOverlaps r) => string.Join(" | ", All(r));

        [Test]
        public void TwoOverlappingPlotsAreAnError()
        {
            Property a = Plot("Rose Cottage", new Vector3(0, 0, 0), new Vector2(10, 10), new Vector2(4, 4));
            Property b = Plot("Ivy House", new Vector3(8, 0, 0), new Vector2(10, 10), new Vector2(4, 4));
            HousingOverlaps r = HousingValidation.OverlapsOf(a, Scene());
            Assert.IsTrue(r.errors.Any(e => e.Contains("Ivy House")), Text(r));
            b.transform.position = new Vector3(10, 0, 0); // side by side: they share a fence
            r = HousingValidation.OverlapsOf(a, Scene());
            Assert.IsFalse(r.errors.Any(e => e.Contains("Ivy House")), "plots that touch do not overlap: " + Text(r));
        }

        [Test]
        public void PlotsSharingALongFenceFarFromTheOriginOnlyTouch()
        {
            Quaternion turn = Quaternion.Euler(0f, 37f, 0f);
            Property a = Plot("Rose Cottage", new Vector3(4000f, 0f, -3000f), new Vector2(20f, 120f), new Vector2(4, 4));
            a.transform.rotation = turn;
            Property b = Plot("Ivy House", a.transform.position + turn * new Vector3(20f, 0f, 0f), new Vector2(20f, 120f), new Vector2(4, 4));
            b.transform.rotation = turn;
            HousingOverlaps r = HousingValidation.OverlapsOf(a, Scene());
            Assert.IsFalse(r.errors.Any(e => e.Contains("Ivy House")), "a shared 120 m fence 5 km out is touching: " + Text(r));
            b.transform.position = a.transform.position + turn * new Vector3(19f, 0f, 0f); // 1 m into it: 120 m²
            r = HousingValidation.OverlapsOf(a, Scene());
            Assert.IsTrue(r.errors.Any(e => e.Contains("Ivy House")), Text(r));
        }

        [Test]
        public void AHouseOffItsLandIsAnError()
        {
            Property p = Plot("Rose Cottage", Vector3.zero, new Vector2(10, 10), new Vector2(4, 4));
            var errors = new List<string>();
            var warnings = new List<string>();
            HousingValidation.CheckProperty(p, errors, warnings);
            Assert.IsEmpty(errors, string.Join(" | ", errors));
            p.house = HousingGeometry.Rectangle(new Vector2(6, 0), new Vector2(4, 4)); // half of it beyond the plot
            errors.Clear();
            HousingValidation.CheckProperty(p, errors, warnings);
            Assert.IsNotEmpty(errors, "the house is not on its land");
        }

        [Test]
        public void AnObjectOfTheCityStandingOnAPlotIsAnError()
        {
            Property p = Plot("Rose Cottage", Vector3.zero, new Vector2(10, 10), new Vector2(4, 4));
            GameObject stall = Block("Market Stall", new Vector3(3, 1, 3), new Vector3(3, 2, 3));
            HousingOverlaps r = HousingValidation.OverlapsOf(p, Scene());
            Assert.IsTrue(r.errors.Any(e => e.Contains("Market Stall")), "9 m² of the plot is covered: " + Text(r));
            stall.transform.SetParent(p.transform, true); // part of the property: fine
            r = HousingValidation.OverlapsOf(p, Scene());
            Assert.IsFalse(All(r).Any(e => e.Contains("Market Stall")), "its own house or shed is not an intruder: " + Text(r));
        }

        [Test]
        public void OnlyTheGroundAShapeReallyCoversCounts()
        {
            // A 16 × 12 m plot; its corner at (8, 6). Everything below passes clear of it, though their boxes reach over it.
            Property p = Plot("Rose Cottage", Vector3.zero, new Vector2(16, 12), new Vector2(4, 4));
            var mid = new Vector3(7.71f, 1.5f, 7.71f); // on the diagonal, 1 m clear of the corner
            Block("City Wall", mid, new Vector3(20f, 3f, 0.3f), 45f);
            GameObject meshWall = Block("Mesh Wall", mid + new Vector3(0.3f, 0f, 0.3f), new Vector3(20f, 3f, 0.3f), 45f);
            Mesh cube = meshWall.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(meshWall.GetComponent<BoxCollider>());
            meshWall.AddComponent<MeshCollider>().sharedMesh = cube; // concave: sampled from above
            GameObject tower = Make("Tower", new Vector3(8f + 2.47f, 2f, 6f + 2.47f)); // a round tower 0.5 m clear of the corner
            tower.AddComponent<SphereCollider>().radius = 3f;
            HousingOverlaps r = HousingValidation.OverlapsOf(p, Scene());
            Assert.IsFalse(All(r).Any(e => e.Contains("City Wall") || e.Contains("Mesh Wall") || e.Contains("Tower")), Text(r));
            tower.transform.position = new Vector3(7f, 2f, 5f); // now standing on the corner
            r = HousingValidation.OverlapsOf(p, Scene());
            Assert.IsTrue(r.errors.Any(e => e.Contains("Tower")), Text(r));
        }

        [Test]
        public void AnObjectsPartsCountTogether()
        {
            Property p = Plot("Rose Cottage", Vector3.zero, new Vector2(10, 10), new Vector2(2, 2));
            // 30 posts of 0.2 × 0.2 m (each below the warning area) under one kinematic body: 1.2 m² together.
            GameObject stall = Make("Market Stall", new Vector3(-3f, 0f, -3f));
            stall.AddComponent<Rigidbody>().isKinematic = true;
            for (int i = 0; i < 30; i++)
            {
                var post = new GameObject($"Post {i}");
                post.transform.SetParent(stall.transform, false);
                post.transform.localPosition = new Vector3(i % 6 * 0.5f, 0.5f, i / 6 * 0.5f);
                post.AddComponent<BoxCollider>().size = new Vector3(0.2f, 1f, 0.2f);
            }
            // Two colliders on the same 0.8 × 0.8 m spot: 0.64 m², counted once.
            GameObject crate = Make("Crate", new Vector3(3f, 0.4f, 3f));
            crate.AddComponent<BoxCollider>().size = new Vector3(0.8f, 0.8f, 0.8f);
            crate.AddComponent<BoxCollider>().size = new Vector3(0.8f, 0.8f, 0.8f);
            HousingOverlaps r = HousingValidation.OverlapsOf(p, Scene());
            Assert.IsTrue(r.errors.Any(e => e.Contains("Market Stall")), "the stall's posts together cover 1.2 m²: " + Text(r));
            Assert.IsTrue(r.warnings.Any(e => e.Contains("Crate")) && !r.errors.Any(e => e.Contains("Crate")), "the crate covers 0.64 m² once: " + Text(r));
        }

        [Test]
        public void WhatPassesOverTheRoofIsNotOnTheLand()
        {
            Property p = Plot("Rose Cottage", Vector3.zero, new Vector2(10, 10), new Vector2(4, 4)); // one 3 m floor
            Block("Footbridge", new Vector3(0f, 8.5f, 0f), new Vector3(20f, 1f, 2f));
            GameObject balcony = Block("Balcony", new Vector3(4f, 2.8f, 0f), new Vector3(2f, 0.4f, 2f));
            HousingOverlaps r = HousingValidation.OverlapsOf(p, Scene());
            Assert.IsFalse(All(r).Any(e => e.Contains("Footbridge")), "a bridge 5 m over the roof passes over: " + Text(r));
            Assert.IsTrue(All(r).Any(e => e.Contains("Balcony")), "a balcony below the roof reaches into the house's space: " + Text(r));
            balcony.transform.position = new Vector3(4f, 0.05f, 0f);
            balcony.transform.localScale = new Vector3(2f, 0.1f, 2f); // a pavement: flat ground
            r = HousingValidation.OverlapsOf(p, Scene());
            Assert.IsFalse(All(r).Any(e => e.Contains("Balcony")), Text(r));
        }

        [Test]
        public void WhatThePropertyHoldsAndWhatMovesAreNotIntruders()
        {
            Property p = Plot("Rose Cottage", Vector3.zero, new Vector2(10, 10), new Vector2(2, 2));
            GameObject bed = Block("Listed Bed", new Vector3(3f, 0.3f, 3f), new Vector3(1f, 0.6f, 2f));
            p.beds.Add(bed.AddComponent<SleepSpot>()); // listed, not a child
            Block("Trigger", new Vector3(-3f, 1f, 3f), new Vector3(2f, 2f, 2f)).GetComponent<BoxCollider>().isTrigger = true;
            Block("Cart", new Vector3(3f, 1f, -3f), new Vector3(2f, 2f, 2f)).AddComponent<Rigidbody>();
            GameObject rock = Block("Rock", new Vector3(-3f, 1f, -3f), new Vector3(2f, 2f, 2f));
            rock.layer = 4;
            settings.groundLayers = 1 << 4; // the rock's layer is ground
            HousingOverlaps r = HousingValidation.OverlapsOf(p, Scene());
            Assert.AreEqual(0, r.Count, Text(r));
        }

        [Test]
        public void AForeignGardenInAPlotIsAnError()
        {
            Property p = Plot("Rose Cottage", Vector3.zero, new Vector2(10, 10), new Vector2(4, 4));
            SoilField field = Make("Someone's Garden", new Vector3(3, 0, -3)).AddComponent<SoilField>();
            field.layout = new SoilLayout { outline = HousingGeometry.Rectangle(Vector2.zero, new Vector2(2, 2)) };
            HousingOverlaps r = HousingValidation.OverlapsOf(p, Scene());
            Assert.IsTrue(r.errors.Any(e => e.Contains("Someone's Garden")), Text(r));
            p.gardens.Add(field); // the plot's own garden
            r = HousingValidation.OverlapsOf(p, Scene());
            Assert.IsFalse(r.errors.Any(e => e.Contains("Someone's Garden")), Text(r));
        }

        [Test]
        public void AGardenTwoPropertiesHoldIsAnError()
        {
            Property a = Plot("Rose Cottage", Vector3.zero, new Vector2(10, 10), new Vector2(2, 2));
            Property b = Plot("Ivy House", new Vector3(30f, 0f, 0f), new Vector2(10, 10), new Vector2(2, 2));
            var go = new GameObject("Herb Bed");
            go.transform.SetParent(a.transform, false);
            go.transform.localPosition = new Vector3(3f, 0f, 3f);
            SoilField field = go.AddComponent<SoilField>();
            field.layout = new SoilLayout { outline = HousingGeometry.Rectangle(Vector2.zero, new Vector2(2, 2)) };
            b.gardens.Add(field); // a copy still pointing at it
            HousingOverlaps r = HousingValidation.OverlapsOf(a, Scene());
            Assert.IsTrue(r.errors.Any(e => e.Contains("Herb Bed") && e.Contains("both")), Text(r));
        }

        [Test]
        public void ARoomNeedsABedAndAChest()
        {
            Inn inn = Make("The Goose", Vector3.zero).AddComponent<Inn>();
            inn.area = HousingGeometry.Rectangle(Vector2.zero, new Vector2(20, 14));
            var roomGo = new GameObject("Room 1");
            roomGo.transform.SetParent(inn.transform, false);
            InnRoom room = roomGo.AddComponent<InnRoom>();
            room.area = HousingGeometry.Rectangle(Vector2.zero, new Vector2(4, 4));
            room.bed = null;
            room.chests.Clear();
            inn.rooms.Clear();
            inn.rooms.Add(room);
            var errors = new List<string>();
            var warnings = new List<string>();
            HousingValidation.CheckInn(inn, errors, warnings);
            Assert.IsTrue(errors.Any(e => e.ToLowerInvariant().Contains("bed")), string.Join(" | ", errors));
            Assert.IsTrue(errors.Any(e => e.ToLowerInvariant().Contains("chest")), string.Join(" | ", errors));
            var bedGo = new GameObject("Bed");
            bedGo.transform.SetParent(roomGo.transform, false);
            room.bed = bedGo.AddComponent<SleepSpot>();
            var chestGo = new GameObject("Chest");
            chestGo.transform.SetParent(roomGo.transform, false);
            Storage chest = chestGo.AddComponent<Storage>();
            room.chests.Add(new HousingChest { id = Property.NewId(), storage = chest });
            errors.Clear();
            HousingValidation.CheckInn(inn, errors, warnings);
            Assert.IsFalse(errors.Any(e => e.ToLowerInvariant().Contains("bed") || e.ToLowerInvariant().Contains("chest")), string.Join(" | ", errors));
        }

        [Test]
        public void ARoomUnderAnotherInnIsAnError()
        {
            Inn a = Make("The Goose", Vector3.zero).AddComponent<Inn>();
            a.area = HousingGeometry.Rectangle(Vector2.zero, new Vector2(20, 14));
            Inn b = Make("The Swan", new Vector3(40f, 0f, 0f)).AddComponent<Inn>();
            b.area = HousingGeometry.Rectangle(Vector2.zero, new Vector2(20, 14));
            var roomGo = new GameObject("Room 3");
            roomGo.transform.SetParent(b.transform, false);
            InnRoom room = roomGo.AddComponent<InnRoom>();
            room.area = HousingGeometry.Rectangle(Vector2.zero, new Vector2(4, 4));
            a.rooms.Clear();
            a.rooms.Add(room); // moved to the Swan, still listed by the Goose
            var errors = new List<string>();
            HousingValidation.CheckInn(a, errors, new List<string>());
            Assert.IsTrue(errors.Any(e => e.Contains("Room 3") && e.Contains("The Swan")), string.Join(" | ", errors));
        }

        [Test]
        public void RoomsOnDifferentFloorsNeverOverlap()
        {
            Inn inn = Make("The Goose", Vector3.zero).AddComponent<Inn>();
            inn.area = HousingGeometry.Rectangle(Vector2.zero, new Vector2(20, 14));
            inn.rooms.Clear();
            for (int floor = 0; floor < 2; floor++)
            {
                var go = new GameObject($"Room {floor + 1}");
                go.transform.SetParent(inn.transform, false);
                go.transform.localPosition = new Vector3(0f, floor * 3f, 0f);
                InnRoom room = go.AddComponent<InnRoom>();
                room.area = HousingGeometry.Rectangle(Vector2.zero, new Vector2(4, 4));
                inn.rooms.Add(room);
            }
            var errors = new List<string>();
            HousingValidation.CheckInn(inn, errors, new List<string>());
            Assert.IsFalse(errors.Any(e => e.Contains("overlap")), string.Join(" | ", errors));
            inn.rooms[1].transform.localPosition = new Vector3(1f, 0f, 0f); // the same floor, 1 m apart: 12 m² shared
            errors.Clear();
            HousingValidation.CheckInn(inn, errors, new List<string>());
            Assert.IsTrue(errors.Any(e => e.Contains("overlap")), string.Join(" | ", errors));
        }

        [Test]
        public void ARealtorWhoseListingsAreAllMissingLetsNothing()
        {
            Property p = Plot("Rose Cottage", Vector3.zero, new Vector2(10, 10), new Vector2(2, 2));
            Realtor realtor = Make("Realtor", new Vector3(20f, 0f, 0f)).AddComponent<Realtor>();
            realtor.listings.Clear();
            realtor.listings.Add(null); // the house it listed was deleted
            var issues = new List<ValidationIssue>();
            HousingValidation.CheckPlaces(Scene(), issues);
            Assert.IsTrue(issues.Any(i => i.message.Contains("lets none")), string.Join(" | ", issues.Select(i => i.message)));
            Assert.IsTrue(issues.Any(i => i.message.Contains(p.DisplayName) && i.message.Contains("No Realtor")), string.Join(" | ", issues.Select(i => i.message)));
        }

        [Test]
        public void ADuplicatedIdIsReportedWithAFixThatAsks()
        {
            Property a = Plot("Rose Cottage", Vector3.zero, new Vector2(10, 10), new Vector2(2, 2));
            Property b = Plot("Rose Cottage (copy)", new Vector3(30f, 0f, 0f), new Vector2(10, 10), new Vector2(2, 2));
            var so = new SerializedObject(b);
            so.FindProperty("propertyId").stringValue = a.PropertyId;
            so.ApplyModifiedPropertiesWithoutUndo();
            var issues = new List<ValidationIssue>();
            HousingValidation.CheckPlaces(Scene(), issues);
            ValidationIssue dup = issues.FirstOrDefault(i => i.message.Contains("same Property Id"));
            Assert.IsNotNull(dup, string.Join(" | ", issues.Select(i => i.message)));
            Assert.IsTrue(dup.CanFix && dup.fixLabel.EndsWith("…"), "its New Id fix asks which of the two keeps the id");
        }
    }
}
#endif
