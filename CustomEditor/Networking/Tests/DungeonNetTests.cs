#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using ProceduralDungeon;
using UnityEngine;

namespace GameNet.Tests
{
    /// <summary>
    /// Edit Mode tests for the pure parts of the dungeon networking: the floor and header codec (a client rebuilds exactly
    /// the server's geometry from it, without the seed), the keyed build seeds, the element ids and the offline
    /// behaviour of the dungeon's network hooks.
    /// </summary>
    public class DungeonNetTests
    {
        private static DungeonLayout Generate(int seed, int floors, out CompiledProfile profile)
        {
            profile = CompiledProfile.Compile(null);
            return DungeonPipeline.CreateDefault().Generate(new DungeonRequest { seed = seed, floorCount = floors }, profile);
        }

        private static DungeonNetHeader HeaderOf(DungeonLayout layout) => new DungeonNetHeader
        {
            Generation = 3,
            Profile = "Crypt",
            Label = "Dungeon (portal)",
            Origin = new Vector3(10f, -4096f, 20f),
            Rotation = Quaternion.Euler(0f, 90f, 0f),
            CellSize = layout.CellSize,
            FloorSpacing = layout.FloorSpacing,
            Width = layout.Width,
            Height = layout.Height,
            FloorCount = layout.Floors.Count,
            ExitAction = (byte)DungeonPortal.PortalAction.NextDungeon,
            Slot = 7,
            SlotSize = 2048f,
            OwnPhysics = true,
        };

        /// <summary>A client's layout built from every floor of <paramref name="layout"/>, sent one at a time.</summary>
        private static DungeonLayout Replicate(DungeonLayout layout, CompiledProfile profile)
        {
            DungeonNetHeader header = DungeonLayoutCodec.DecodeHeader(DungeonLayoutCodec.EncodeHeader(HeaderOf(layout)));
            DungeonLayout replica = DungeonLayoutCodec.CreateReplicaLayout(header);
            for (int f = 0; f < layout.Floors.Count; f++)
            {
                byte[] packet = DungeonLayoutCodec.EncodeFloor(layout, f, layout.FloorBuildSeed(f), p => DungeonBuilder.BuiltOnClients(p, profile));
                DungeonNetFloor floor = DungeonLayoutCodec.DecodeFloor(packet, header.Width, header.Height);
                Assert.IsNotNull(floor, $"floor {f} decodes");
                Assert.IsTrue(DungeonLayoutCodec.Merge(replica, floor), $"floor {f} merges");
            }
            return replica;
        }

        // ------------------------------------------------------------------ header

        [Test]
        public void HeaderRoundTrips()
        {
            var h = new DungeonNetHeader
            {
                Generation = 5, Profile = "Deep Caves", Label = "Dungeon (Portal 3)", Origin = new Vector3(1f, -4096f, 2048f),
                Rotation = Quaternion.Euler(0f, 30f, 0f), CellSize = 1.5f, FloorSpacing = 12f, Width = 96, Height = 80,
                FloorCount = 4, ExitAction = 2, Slot = 3, SlotSize = 2048f, OwnPhysics = true,
            };
            DungeonNetHeader r = DungeonLayoutCodec.DecodeHeader(DungeonLayoutCodec.EncodeHeader(h));
            Assert.IsNotNull(r);
            Assert.AreEqual(h.Generation, r.Generation);
            Assert.AreEqual(h.Profile, r.Profile);
            Assert.AreEqual(h.Label, r.Label);
            Assert.AreEqual(h.Origin, r.Origin);
            Assert.Less(Quaternion.Angle(h.Rotation, r.Rotation), 0.01f);
            Assert.AreEqual(h.CellSize, r.CellSize);
            Assert.AreEqual(h.FloorSpacing, r.FloorSpacing);
            Assert.AreEqual(h.Width, r.Width);
            Assert.AreEqual(h.Height, r.Height);
            Assert.AreEqual(h.FloorCount, r.FloorCount);
            Assert.AreEqual(h.ExitAction, r.ExitAction);
            Assert.AreEqual(h.Slot, r.Slot);
            Assert.AreEqual(h.SlotSize, r.SlotSize);
            Assert.AreEqual(h.OwnPhysics, r.OwnPhysics);
        }

        [Test]
        public void MalformedPacketsAreRefused()
        {
            Assert.IsNull(DungeonLayoutCodec.DecodeHeader(null));
            Assert.IsNull(DungeonLayoutCodec.DecodeHeader(new byte[0]));
            Assert.IsNull(DungeonLayoutCodec.DecodeHeader(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.IsNull(DungeonLayoutCodec.DecodeFloor(new byte[] { 9, 9, 9 }, 10, 10));

            // A header with an impossible grid or floor count.
            var bad = new DungeonNetHeader { Width = 0, Height = 10, FloorCount = 1, CellSize = 1f };
            Assert.IsNull(DungeonLayoutCodec.DecodeHeader(DungeonLayoutCodec.EncodeHeader(bad)));
            bad = new DungeonNetHeader { Width = 10, Height = 10, FloorCount = DungeonLayoutCodec.MaxFloors + 1, CellSize = 1f };
            Assert.IsNull(DungeonLayoutCodec.DecodeHeader(DungeonLayoutCodec.EncodeHeader(bad)));
            bad = new DungeonNetHeader { Width = 10, Height = 10, FloorCount = 2, CellSize = float.NaN };
            Assert.IsNull(DungeonLayoutCodec.DecodeHeader(DungeonLayoutCodec.EncodeHeader(bad)));

            // A header is not a floor, and a floor of another grid size is refused.
            DungeonLayout layout = Generate(11, 2, out CompiledProfile profile);
            byte[] header = DungeonLayoutCodec.EncodeHeader(HeaderOf(layout));
            Assert.IsNull(DungeonLayoutCodec.DecodeFloor(header, layout.Width, layout.Height));
            byte[] floor = DungeonLayoutCodec.EncodeFloor(layout, 0, 1);
            Assert.IsNull(DungeonLayoutCodec.DecodeFloor(floor, layout.Width + 1, layout.Height));
            Assert.IsNull(DungeonLayoutCodec.DecodeHeader(floor));
        }

        // ------------------------------------------------------------------ floors

        [Test]
        public void FloorsRoundTripExactly()
        {
            DungeonLayout layout = Generate(4242, 3, out CompiledProfile profile);
            DungeonLayout replica = Replicate(layout, profile);
            Assert.AreEqual(layout.Floors.Count, replica.Floors.Count);
            Assert.AreEqual(0, replica.Seed, "no seed on clients");
            Assert.AreEqual(0, replica.AttemptSeed, "no seed on clients");
            Assert.AreEqual(0, replica.Request.seed, "no seed on clients");
            for (int f = 0; f < layout.Floors.Count; f++)
            {
                FloorLayout a = layout.Floors[f], b = replica.Floors[f];
                Assert.AreEqual(a.Index, b.Index);
                Assert.AreEqual(a.Spec.Style, b.Spec.Style);
                Assert.AreEqual(a.Spec.BaseY, b.Spec.BaseY);
                Assert.AreEqual(a.Spec.MaxCeiling, b.Spec.MaxCeiling);
                Assert.AreEqual(a.Spec.Modifier, b.Spec.Modifier);
                Assert.AreEqual(a.Spec.IsLast, b.Spec.IsLast);
                CollectionAssert.AreEqual(a.Grid.Type, b.Grid.Type, $"floor {f} types");
                CollectionAssert.AreEqual(a.Grid.Flags, b.Grid.Flags, $"floor {f} flags");
                CollectionAssert.AreEqual(a.Grid.Area, b.Grid.Area, $"floor {f} areas");
                CollectionAssert.AreEqual(a.Grid.Connection, b.Grid.Connection, $"floor {f} connections");
                CollectionAssert.AreEqual(a.Grid.FloorHeight, b.Grid.FloorHeight, $"floor {f} floor heights");
                CollectionAssert.AreEqual(a.Grid.CeilingHeight, b.Grid.CeilingHeight, $"floor {f} ceilings");
                Assert.AreEqual(a.Areas.Count, b.Areas.Count);
                for (int i = 0; i < a.Areas.Count; i++)
                {
                    Assert.AreEqual(a.Areas[i].Role, b.Areas[i].Role);
                    Assert.AreEqual(a.Areas[i].TemplateIndex, b.Areas[i].TemplateIndex);
                    Assert.AreEqual(a.Areas[i].TemplateOrigin, b.Areas[i].TemplateOrigin);
                    Assert.AreEqual(a.Areas[i].Bounds, b.Areas[i].Bounds);
                    CollectionAssert.AreEqual(a.Areas[i].Cells, b.Areas[i].Cells, $"floor {f} area {i} cells");
                }
                Assert.AreEqual(a.Connections.Count, b.Connections.Count);
                for (int i = 0; i < a.Connections.Count; i++)
                {
                    Assert.AreEqual(a.Connections[i].DoorA, b.Connections[i].DoorA);
                    Assert.AreEqual(a.Connections[i].DoorB, b.Connections[i].DoorB);
                    CollectionAssert.AreEqual(a.Connections[i].Track, b.Connections[i].Track);
                }
            }
            foreach (VerticalLink l in layout.Links)
            {
                VerticalLink r = replica.Links[l.Id];
                Assert.IsNotNull(r, $"link {l.Id}");
                Assert.AreEqual(l.Kind, r.Kind);
                Assert.AreEqual(l.Footprint, r.Footprint);
                Assert.AreEqual(l.UpperLanding, r.UpperLanding);
                Assert.AreEqual(l.LowerLanding, r.LowerLanding);
            }
        }

        [Test]
        public void ClientsGetNoMobsAndKeepTheServersPlacementIds()
        {
            DungeonLayout layout = Generate(77, 2, out CompiledProfile profile);
            DungeonLayout replica = Replicate(layout, profile);
            Assert.AreEqual(replica.Placements.Count, replica.PlacementIds.Count);
            for (int i = 0; i < replica.Placements.Count; i++)
            {
                Placement p = replica.Placements[i];
                Assert.AreNotEqual(PlacementKind.Mob, p.Kind, "mobs are never sent");
                Assert.AreNotEqual(PlacementKind.Boss, p.Kind, "bosses are never sent");
                Assert.AreNotEqual(PlacementKind.RoomController, p.Kind, "room events stay on the server");
                int id = replica.PlacementId(i);
                Placement original = layout.Placements[id];
                Assert.AreEqual(original.Kind, p.Kind);
                Assert.AreEqual(original.Floor, p.Floor);
                Assert.AreEqual(original.Cell, p.Cell);
                Assert.AreEqual(original.Link, p.Link);
            }
            int expected = 0;
            foreach (Placement p in layout.Placements)
                if (DungeonBuilder.BuiltOnClients(p, profile))
                    expected++;
            Assert.AreEqual(expected, replica.Placements.Count);
        }

        [Test]
        public void FloorsMustArriveInOrder()
        {
            DungeonLayout layout = Generate(9, 3, out CompiledProfile profile);
            DungeonLayout replica = DungeonLayoutCodec.CreateReplicaLayout(HeaderOf(layout));
            DungeonNetFloor second = DungeonLayoutCodec.DecodeFloor(DungeonLayoutCodec.EncodeFloor(layout, 1, 5), layout.Width, layout.Height);
            Assert.IsFalse(DungeonLayoutCodec.Merge(replica, second), "floor 1 before floor 0");
            DungeonNetFloor first = DungeonLayoutCodec.DecodeFloor(DungeonLayoutCodec.EncodeFloor(layout, 0, 4), layout.Width, layout.Height);
            Assert.IsTrue(DungeonLayoutCodec.Merge(replica, first));
            Assert.IsTrue(DungeonLayoutCodec.Merge(replica, second));
            Assert.IsFalse(DungeonLayoutCodec.Merge(replica, second), "the same floor twice");
            Assert.AreEqual(4, replica.FloorBuildSeed(0));
            Assert.AreEqual(5, replica.FloorBuildSeed(1));
            Assert.AreEqual(2, replica.Floors.Count, "future floors are not known");
        }

        [Test]
        public void ClientMeshesMatchTheServers()
        {
            DungeonLayout layout = Generate(31337, 3, out CompiledProfile profile);
            layout.FloorBuildSeeds = DungeonBuildSeed.ForFloors(layout.AttemptSeed, layout.Floors.Count);
            List<FloorMeshData> server = DungeonMeshing.BuildAll(layout, profile);
            DungeonLayout replica = Replicate(layout, profile);
            for (int f = 0; f < layout.Floors.Count; f++)
            {
                FloorMeshData a = server[f], b = DungeonMeshing.BuildFloor(replica, profile, f);
                Assert.AreEqual(a.Chunks.Count, b.Chunks.Count, $"floor {f} chunks");
                for (int c = 0; c < a.Chunks.Count; c++)
                    CollectionAssert.AreEqual(a.Chunks[c].Vertices, b.Chunks[c].Vertices, $"floor {f} chunk {c}");
                Assert.AreEqual(a.Links.Count, b.Links.Count, $"floor {f} links");
                for (int l = 0; l < a.Links.Count; l++)
                {
                    Assert.AreEqual(a.Links[l].LinkId, b.Links[l].LinkId);
                    CollectionAssert.AreEqual(a.Links[l].Visual.Vertices, b.Links[l].Visual.Vertices, $"floor {f} link {l}");
                }
            }
        }

        // ------------------------------------------------------------------ build seeds and ids

        [Test]
        public void BuildSeedsAreKeyedAndStable()
        {
            byte[] keyA = new byte[32], keyB = new byte[32];
            keyB[0] = 1;
            Assert.AreEqual(DungeonBuildSeed.ForFloor(keyA, 1234, 0), DungeonBuildSeed.ForFloor(keyA, 1234, 0), "deterministic");
            Assert.AreNotEqual(DungeonBuildSeed.ForFloor(keyA, 1234, 0), DungeonBuildSeed.ForFloor(keyB, 1234, 0), "depends on the secret key");
            Assert.AreNotEqual(DungeonBuildSeed.ForFloor(keyA, 1234, 0), DungeonBuildSeed.ForFloor(keyA, 1234, 1), "per floor");
            Assert.AreNotEqual(DungeonBuildSeed.ForFloor(keyA, 1234, 0), DungeonBuildSeed.ForFloor(keyA, 1235, 0), "per dungeon");
            for (int s = 0; s < 200; s++)
            {
                int seed = DungeonBuildSeed.ForFloor(keyA, s, s % 5);
                Assert.AreNotEqual(0, seed);
                Assert.AreNotEqual(s * 31 + s % 5, seed, "not the single-player formula");
            }
            int[] process = DungeonBuildSeed.ForFloors(99, 3);
            Assert.AreEqual(3, process.Length);
            Assert.AreEqual(process[1], DungeonBuildSeed.ForFloor(99, 1));
        }

        [Test]
        public void SessionDungeonSeedsAreKeyed()
        {
            byte[] keyA = new byte[32], keyB = new byte[32];
            keyB[31] = 7;
            int publicSeed = DungeonRequest.FromWorldPosition(12345, new Vector3(100f, 0f, -40f)).seed;
            int a = DungeonBuildSeed.SecretDungeonSeed(keyA, publicSeed);
            Assert.AreEqual(a, DungeonBuildSeed.SecretDungeonSeed(keyA, publicSeed), "the same portal leads to the same dungeon");
            Assert.AreNotEqual(publicSeed, a, "a client knowing the world seed cannot compute it");
            Assert.AreNotEqual(a, DungeonBuildSeed.SecretDungeonSeed(keyB, publicSeed), "depends on the server's key");
            Assert.AreNotEqual(DungeonBuildSeed.ForFloor(keyA, publicSeed, 0), a, "build seeds and dungeon seeds use different tags");
            Assert.AreEqual(0, DungeonBuildSeed.SecretDungeonSeed(0), "0 still means a random dungeon");
        }

        [Test]
        public void SinglePlayerKeepsItsMeshSeeds()
        {
            var layout = new DungeonLayout { AttemptSeed = 1000 };
            Assert.AreEqual(1000 * 31 + 2, layout.FloorBuildSeed(2));
            Assert.AreEqual(7, layout.PlacementId(7));
            layout.FloorBuildSeeds = new[] { 5, 6 };
            Assert.AreEqual(6, layout.FloorBuildSeed(1));
            Assert.AreEqual(1000 * 31 + 3, layout.FloorBuildSeed(3), "beyond the array: the old formula");
        }

        [Test]
        public void ElementIdRangesDoNotOverlap()
        {
            var ids = new HashSet<int>();
            for (int i = 0; i < 2000; i++)
                Assert.IsTrue(ids.Add(DungeonNetIds.ForPlacement(i)));
            for (int f = 0; f < 4; f++)
                for (int c = 0; c < 2000; c += 7)
                    Assert.IsTrue(ids.Add(DungeonNetIds.ForSecretDoor(f, c)));
            for (int f = 0; f < 4; f++)
                Assert.IsTrue(ids.Add(DungeonNetIds.ForFloor(f)));
            for (int l = 0; l < 50; l++)
                Assert.IsTrue(ids.Add(DungeonNetIds.ForLink(l)));
            Assert.AreEqual(DungeonNetIds.ForSecretDoor(DungeonNetIds.MaxFloor, DungeonNetIds.MaxCell) + 1, DungeonNetIds.FloorMechanicBase);
            Assert.AreEqual(-1, DungeonNetIds.ForPlacement(-1));
            Assert.AreEqual(-1, DungeonNetIds.ForSecretDoor(0, DungeonNetIds.MaxCell + 1));
            Assert.AreEqual(-1, DungeonNetIds.ForSecretDoor(DungeonNetIds.MaxFloor + 1, 0));
            Assert.AreEqual(-1, DungeonNetIds.ForLink(-3));
        }

        // ------------------------------------------------------------------ offline behaviour

        [Test]
        public void OfflineHooksDoNothing()
        {
            Assert.IsFalse(DungeonNet.ServerSession);
            var go = new GameObject("Chest");
            try
            {
                var chest = go.AddComponent<DungeonChest>();
                Assert.IsFalse(DungeonNet.IsReplica(chest));
                Assert.IsNull(DungeonNetIds.ElementOf(chest));
                Assert.AreEqual(-1, DungeonNetIds.Of(chest));
                DungeonNet.Publish(chest, 1);   // no element, no session: nothing happens
                Assert.IsFalse(DungeonNet.TryRelayInteract(chest, null));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ServerOnlyComponentsAreKnown()
        {
            Assert.IsTrue(DungeonNet.IsServerOnly(typeof(DungeonRoomEvent)));
            Assert.IsTrue(DungeonNet.IsServerOnly(typeof(DungeonTeleporter)));
            Assert.IsTrue(DungeonNet.IsServerOnly(typeof(DungeonNest)));
            Assert.IsFalse(DungeonNet.IsServerOnly(typeof(DungeonChest)), "chests show their state on clients");
            Assert.IsFalse(DungeonNet.IsServerOnly(typeof(DungeonGate)), "doors follow the server on clients");
            Assert.IsFalse(DungeonNet.IsServerOnly(typeof(DungeonPortal)), "portals are shown (they act on the server only)");
        }

        [Test]
        public void PositionPayloadRoundTrips()
        {
            var v = new Vector3(1.5f, -2.25f, 300.125f);
            Assert.IsTrue(DungeonNet.TryReadPosition(DungeonNet.PositionPayload(v), out Vector3 r));
            Assert.AreEqual(v, r);
            Assert.IsFalse(DungeonNet.TryReadPosition(null, out _));
            Assert.IsFalse(DungeonNet.TryReadPosition(new byte[4], out _));
            Assert.IsFalse(DungeonNet.TryReadPosition(DungeonNet.PositionPayload(new Vector3(float.NaN, 0f, 0f)), out _));
        }

        [Test]
        public void ScenesWithoutLeasesAreNotDungeons()
        {
            Assert.IsNull(DungeonScenes.Find(default));
            Assert.IsFalse(DungeonScenes.IsInstanceScene(default));
            Assert.IsFalse(DungeonScenes.LoadsThroughNetwork, "offline: run scenes are created locally");
        }
    }
}
#endif
