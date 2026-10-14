#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameNet.Tests
{
    /// <summary>
    /// Edit Mode tests for the always-compiled parts of the networked inventory: slot references, stack and snapshot
    /// comparison, the plan a client follows to show a snapshot, what other players see, request validation, the send
    /// schedule, world item state and scene object paths.
    /// </summary>
    public class InventoryNetTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
                if (go != null)
                    Object.DestroyImmediate(go);
            created.Clear();
        }

        private static InventoryStackData Stack(InventoryArea area, int index, string id, int count = 1, params int[] durabilities) =>
            new InventoryStackData
            {
                area = (byte)area,
                index = (short)index,
                itemId = id,
                count = count,
                durability = 10f,
                durabilities = durabilities.Length > 0 ? durabilities : null,
                loadedRounds = -1,
            };

        // ------------------------------------------------------------------ slot references

        [Test]
        public void SlotRefValidityAndEquality()
        {
            Assert.IsFalse(InventorySlotRef.None.IsValid);
            Assert.IsTrue(new InventorySlotRef(InventoryArea.Bag, 3).IsValid);
            Assert.IsFalse(new InventorySlotRef { area = 9, index = 0 }.IsValid, "unknown area");
            Assert.IsFalse(new InventorySlotRef { area = 0, index = InventoryNetRules.MaxSlotsPerArea }.IsValid, "index out of range");
            Assert.AreEqual(new InventorySlotRef(InventoryArea.Equipment, 2), new InventorySlotRef(InventoryArea.Equipment, 2));
            Assert.AreNotEqual(new InventorySlotRef(InventoryArea.Equipment, 2), new InventorySlotRef(InventoryArea.Hotbar, 2));
        }

        // ------------------------------------------------------------------ stacks and snapshots

        [Test]
        public void StackEqualityIgnoresTheCooldownButNotTheContent()
        {
            InventoryStackData a = Stack(InventoryArea.Bag, 1, "potion", 3, 5, 6);
            InventoryStackData b = a;
            b.cooldown = 4f;
            Assert.IsTrue(a.Equals(b), "cooldown changes every frame and is not compared");

            b = a;
            b.durabilities = new[] { 5, 7 };
            Assert.IsFalse(a.Equals(b), "uses left differ");

            b = a;
            b.index = 2;
            Assert.IsFalse(a.Equals(b), "other slot");
            Assert.IsTrue(a.SameContent(b), "same content in another slot");

            b = a;
            b.durabilities = new int[0];
            a.durabilities = null;
            Assert.IsTrue(a.SameContent(b), "null and empty durability lists are the same");
        }

        [Test]
        public void SnapshotSameStateComparesEverythingButTheAck()
        {
            InventorySnapshot a = NewSnapshot();
            InventorySnapshot b = NewSnapshot();
            b.ack = 99;
            Assert.IsTrue(a.SameState(b));

            b.selectedHotbar = 2;
            Assert.IsFalse(a.SameState(b), "selection");

            b = NewSnapshot();
            b.quickSlots = new[] { "potion", "" };
            Assert.IsFalse(a.SameState(b), "quickslots");

            b = NewSnapshot();
            b.stacks[0].count = 2;
            Assert.IsFalse(a.SameState(b), "a stack count");

            b = NewSnapshot();
            b.storageSize = 8;
            Assert.IsFalse(a.SameState(b), "a storage opened");
            Assert.IsFalse(a.SameState(null));
        }

        [Test]
        public void SnapshotSortsStacksByAreaThenIndex()
        {
            var s = new InventorySnapshot
            {
                stacks = new[]
                {
                    Stack(InventoryArea.Storage, 0, "c"),
                    Stack(InventoryArea.Bag, 5, "b"),
                    Stack(InventoryArea.Hotbar, 1, "a"),
                    Stack(InventoryArea.Bag, 2, "d"),
                },
            };
            s.SortStacks();
            Assert.AreEqual("a", s.stacks[0].itemId);
            Assert.AreEqual("d", s.stacks[1].itemId);
            Assert.AreEqual("b", s.stacks[2].itemId);
            Assert.AreEqual("c", s.stacks[3].itemId);
        }

        private static InventorySnapshot NewSnapshot() => new InventorySnapshot
        {
            hotbarCount = 4,
            bagCount = 20,
            selectedHotbar = 1,
            storageSize = -1,
            quickSlots = new[] { "", "" },
            stacks = new[] { Stack(InventoryArea.Hotbar, 0, "sword"), Stack(InventoryArea.Bag, 3, "potion", 3) },
        };

        // ------------------------------------------------------------------ the client's apply plan

        [Test]
        public void PlanKeepsUpdatesMovesRemovesAndCreates()
        {
            var current = new List<InventoryStackData>
            {
                Stack(InventoryArea.Hotbar, 0, "sword"),          // kept
                Stack(InventoryArea.Bag, 1, "potion", 3),         // updated (count)
                Stack(InventoryArea.Bag, 2, "helmet"),            // moved to equipment 0
                Stack(InventoryArea.Bag, 3, "junk"),              // removed
            };
            var target = new List<InventoryStackData>
            {
                Stack(InventoryArea.Hotbar, 0, "sword"),
                Stack(InventoryArea.Bag, 1, "potion", 2),
                Stack(InventoryArea.Equipment, 0, "helmet"),
                Stack(InventoryArea.Bag, 4, "arrow", 20),         // created
            };
            List<InventorySnapshotDiff.Step> plan = InventorySnapshotDiff.Plan(current, target);
            Assert.AreEqual(5, plan.Count);
            AssertStep(plan, InventorySnapshotDiff.StepKind.Keep, 0, 0);
            AssertStep(plan, InventorySnapshotDiff.StepKind.Update, 1, 1);
            AssertStep(plan, InventorySnapshotDiff.StepKind.Move, 2, 2);
            AssertStep(plan, InventorySnapshotDiff.StepKind.Remove, 3, -1);
            AssertStep(plan, InventorySnapshotDiff.StepKind.Create, -1, 3);
            Assert.AreEqual(InventorySnapshotDiff.StepKind.Remove, plan[0].kind, "removals come first");
        }

        [Test]
        public void PlanUsesEveryStackExactlyOnce()
        {
            var current = new List<InventoryStackData>
            {
                Stack(InventoryArea.Bag, 0, "potion", 5),
                Stack(InventoryArea.Bag, 1, "potion", 5),
                Stack(InventoryArea.Bag, 2, "potion", 1),
            };
            var target = new List<InventoryStackData>
            {
                Stack(InventoryArea.Bag, 1, "potion", 5),
                Stack(InventoryArea.Bag, 7, "potion", 1),
                Stack(InventoryArea.Hotbar, 0, "potion", 5),
            };
            List<InventorySnapshotDiff.Step> plan = InventorySnapshotDiff.Plan(current, target);
            var usedCurrent = new HashSet<int>();
            var usedTarget = new HashSet<int>();
            foreach (InventorySnapshotDiff.Step s in plan)
            {
                if (s.current >= 0) Assert.IsTrue(usedCurrent.Add(s.current), "current used twice");
                if (s.target >= 0) Assert.IsTrue(usedTarget.Add(s.target), "target used twice");
            }
            Assert.AreEqual(3, usedCurrent.Count);
            Assert.AreEqual(3, usedTarget.Count);
            // The stack of 1 moves to bag 7 (an identical stack is preferred), the first stack of 5 to the hotbar.
            AssertStep(plan, InventorySnapshotDiff.StepKind.Keep, 1, 0);
            AssertStep(plan, InventorySnapshotDiff.StepKind.Move, 2, 1);
            AssertStep(plan, InventorySnapshotDiff.StepKind.Move, 0, 2);
        }

        [Test]
        public void PlanReplacesAnotherItemInTheSameSlot()
        {
            var current = new List<InventoryStackData> { Stack(InventoryArea.Bag, 0, "axe") };
            var target = new List<InventoryStackData> { Stack(InventoryArea.Bag, 0, "pick") };
            List<InventorySnapshotDiff.Step> plan = InventorySnapshotDiff.Plan(current, target);
            Assert.AreEqual(2, plan.Count);
            AssertStep(plan, InventorySnapshotDiff.StepKind.Remove, 0, -1);
            AssertStep(plan, InventorySnapshotDiff.StepKind.Create, -1, 0);
        }

        [Test]
        public void PlanOfEmptyListsIsEmpty()
        {
            Assert.AreEqual(0, InventorySnapshotDiff.Plan(null, null).Count);
            var current = new List<InventoryStackData> { Stack(InventoryArea.Bag, 0, null) };
            List<InventorySnapshotDiff.Step> plan = InventorySnapshotDiff.Plan(current, new List<InventoryStackData>());
            AssertStep(plan, InventorySnapshotDiff.StepKind.Remove, 0, -1);
        }

        private static void AssertStep(List<InventorySnapshotDiff.Step> plan, InventorySnapshotDiff.StepKind kind, int current, int target)
        {
            foreach (InventorySnapshotDiff.Step s in plan)
                if (s.kind == kind && s.current == current && s.target == target)
                    return;
            Assert.Fail($"No {kind} {current}->{target} in: {string.Join(", ", plan)}");
        }

        // ------------------------------------------------------------------ appearance

        [Test]
        public void AppearanceBecomesStacksAndComparesByContent()
        {
            var a = new InventoryAppearance { mainHand = "sword", mainHandSlot = 2, equipment = new[] { "helmet", "", null, "ring" }, drawn = true };
            List<InventoryStackData> stacks = a.ToStacks();
            Assert.AreEqual(3, stacks.Count);
            Assert.AreEqual((byte)InventoryArea.Hotbar, stacks[0].area);
            Assert.AreEqual(2, stacks[0].index);
            Assert.AreEqual("ring", stacks[2].itemId);
            Assert.AreEqual(3, stacks[2].index);

            var b = new InventoryAppearance { mainHand = "sword", mainHandSlot = 2, equipment = new[] { "helmet", null, "", "ring" }, drawn = true };
            Assert.IsTrue(a.Equals(b), "null and empty equipment entries are the same");
            b.drawn = false;
            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(new InventoryAppearance().Equals(new InventoryAppearance { mainHand = "", equipment = new string[0] }));
        }

        // ------------------------------------------------------------------ request validation

        [Test]
        public void WellFormedRequests()
        {
            var move = new InventoryRequest { kind = (byte)InventoryRequestKind.Move, from = new InventorySlotRef(InventoryArea.Bag, 0), to = new InventorySlotRef(InventoryArea.Hotbar, 1), quickIndex = -1 };
            Assert.IsTrue(InventoryNetRules.IsWellFormed(move));

            InventoryRequest bad = move;
            bad.to = InventorySlotRef.None;
            Assert.IsFalse(InventoryNetRules.IsWellFormed(bad), "a move needs a target");

            bad = move;
            bad.flag = true;
            bad.point = new Vector3(float.NaN, 0f, 0f);
            Assert.IsFalse(InventoryNetRules.IsWellFormed(bad), "a NaN drop point");

            bad = move;
            bad.itemId = new string('x', InventoryNetRules.MaxItemIdLength + 1);
            Assert.IsFalse(InventoryNetRules.IsWellFormed(bad), "an absurd item id");

            bad = move;
            bad.kind = 200;
            Assert.IsFalse(InventoryNetRules.IsWellFormed(bad), "unknown kind");

            var grid = move;
            grid.to = InventorySlotRef.None;
            grid.toGrid = true;
            grid.gridX = -1;
            grid.gridY = 3;
            Assert.IsTrue(InventoryNetRules.IsWellFormed(grid), "a grid target may stick out (the grid refuses it)");

            var split = new InventoryRequest { kind = (byte)InventoryRequestKind.Split, from = new InventorySlotRef(InventoryArea.Bag, 2), amount = 0 };
            Assert.IsFalse(InventoryNetRules.IsWellFormed(split), "split of nothing");
            split.amount = 3;
            Assert.IsTrue(InventoryNetRules.IsWellFormed(split));

            var use = new InventoryRequest { kind = (byte)InventoryRequestKind.UseItem };
            Assert.IsFalse(InventoryNetRules.IsWellFormed(use), "use needs an item");
            Assert.IsTrue(InventoryNetRules.IsWellFormed(new InventoryRequest { kind = (byte)InventoryRequestKind.Resync }));
        }

        [Test]
        public void DropPointStaysNearThePlayer()
        {
            Vector3 player = new Vector3(10f, 0f, 10f);
            Assert.AreEqual(new Vector3(11f, 0f, 10f), InventoryNetRules.ClampDropPoint(new Vector3(11f, 0f, 10f), player, Vector3.forward, 4f));
            Vector3 far = InventoryNetRules.ClampDropPoint(new Vector3(110f, 0f, 10f), player, Vector3.forward, 4f);
            Assert.AreEqual(4f, Vector3.Distance(far, player), 1e-3f);
            Vector3 nan = InventoryNetRules.ClampDropPoint(new Vector3(float.NaN, 0f, 0f), player, Vector3.forward, 4f);
            Assert.IsTrue(InventoryNetRules.IsFinite(nan));
            Assert.Less(Vector3.Distance(nan, player), 4f);
        }

        [Test]
        public void SnapshotsOlderThanTheLastRequestAreSkipped()
        {
            Assert.IsTrue(InventoryNetRules.ShouldApply(5, 5));
            Assert.IsTrue(InventoryNetRules.ShouldApply(6, 5));
            Assert.IsFalse(InventoryNetRules.ShouldApply(4, 5));
            Assert.IsTrue(InventoryNetRules.ShouldApply(2, uint.MaxValue - 1), "wrap-around");
            Assert.IsFalse(InventoryNetRules.ShouldApply(uint.MaxValue - 1, 2), "wrap-around");
        }

        // ------------------------------------------------------------------ send schedule

        [Test]
        public void ScheduleSendsAtOnceThenDebouncesAndPolls()
        {
            var s = new InventorySendSchedule(0.1f, 1f);
            Assert.AreEqual(InventorySendSchedule.Decision.Send, s.Evaluate(0f), "the first snapshot goes out at once");
            s.Sent(0f);
            Assert.AreEqual(InventorySendSchedule.Decision.Wait, s.Evaluate(0.05f));
            s.MarkDirty();
            Assert.AreEqual(InventorySendSchedule.Decision.Wait, s.Evaluate(0.05f), "too soon after the last send");
            Assert.AreEqual(InventorySendSchedule.Decision.Send, s.Evaluate(0.11f));
            s.Sent(0.11f);
            Assert.AreEqual(InventorySendSchedule.Decision.Wait, s.Evaluate(0.9f));
            Assert.AreEqual(InventorySendSchedule.Decision.Poll, s.Evaluate(1.2f));
            s.Polled(1.2f);
            Assert.AreEqual(InventorySendSchedule.Decision.Wait, s.Evaluate(1.5f));
        }

        // ------------------------------------------------------------------ world items

        [Test]
        public void WorldItemStateEquality()
        {
            var a = new WorldItemState { itemId = "arrow", quantity = 3, durabilities = new[] { 1, 2, 3 }, spin = true };
            var b = new WorldItemState { itemId = "arrow", quantity = 3, durabilities = new[] { 1, 2, 3 }, spin = true };
            Assert.IsTrue(a.Equals(b), "a new array with the same values is no change");
            b.quantity = 2;
            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(new WorldItemState().Equals(new WorldItemState { itemId = "", durabilities = new int[0] }));
        }

        // ------------------------------------------------------------------ scene paths

        [Test]
        public void ScenePathFindsTheSameObjectAmongSameNamedSiblings()
        {
            var root = new GameObject("InventoryNetTests Root");
            created.Add(root);
            var a = new GameObject("Chest");
            a.transform.SetParent(root.transform);
            var b = new GameObject("Chest");
            b.transform.SetParent(root.transform);
            var item = new GameObject("Item/With[Odd]Name");
            item.transform.SetParent(b.transform);

            string path = SceneObjectPath.Of(item.transform);
            Assert.IsNotNull(path);
            StringAssert.Contains("Chest[1]", path);
            Scene scene = root.scene;
            Assert.AreSame(item.transform, SceneObjectPath.Find(scene, path));
            Assert.AreSame(a.transform, SceneObjectPath.Find(scene, SceneObjectPath.Of(a.transform)));
            Assert.IsNull(SceneObjectPath.Find(scene, "InventoryNetTests Root/Nothing"));
            Assert.IsNull(SceneObjectPath.Find(scene, ""));
        }

        // ------------------------------------------------------------------ offline behaviour

        [Test]
        public void OfflineEveryEquipmentEffectRuns()
        {
            Assert.IsFalse(NetAuthority.IsNetworked, "tests run offline");
            Assert.IsTrue(EquipmentEffect.RunsHere(new PulseEffect(), null), "rules effects run offline");
            Assert.AreEqual(EquipmentEffectScope.Rules, new OnHitEffect().Scope);
            Assert.AreEqual(EquipmentEffectScope.Cosmetic, new AttachedVisualEffect().Scope);
            Assert.AreEqual(EquipmentEffectScope.Simulation, new CombatStatsEffect().Scope);
        }

        [Test]
        public void StorageLockIsFreedWhenItsUserClosedIt()
        {
            var go = new GameObject("InventoryNetTests Storage");
            created.Add(go);
            Storage storage = go.AddComponent<Storage>();
            var user = new GameObject("InventoryNetTests User");
            created.Add(user);
            InventoryManager first = user.AddComponent<InventoryManager>();
            Assert.IsFalse(storage.IsUsedByOther(first), "nobody uses it");
            storage.Claim(first);
            Assert.AreSame(first, storage.User);
            Assert.IsFalse(storage.IsUsedByOther(first), "its own user");
            // The user's window is not open (it never opened in edit mode): the lock is stale and is dropped.
            Assert.IsFalse(storage.IsUsedByOther(null));
            Assert.IsNull(storage.User);
            storage.Claim(first);
            storage.Release(first);
            Assert.IsNull(storage.User);
        }
    }
}
#endif
