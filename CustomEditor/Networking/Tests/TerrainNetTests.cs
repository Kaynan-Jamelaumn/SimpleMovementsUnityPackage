#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GameNet.Tests
{
    /// <summary>
    /// Edit Mode tests for the open-world terrain's networking support: the multi-viewer distance arithmetic, the viewer
    /// set API, the removed-object ids and their sync, and the weather sync state (all offline - no session needed).
    /// </summary>
    public class TerrainNetTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
                if (o != null)
                    Object.DestroyImmediate(o);
            created.Clear();
        }

        private T Make<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            created.Add(go);
            return go.AddComponent<T>();
        }

        private Transform MakeTransform(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go.transform;
        }

        // ------------------------------------------------------------------ distances

        [Test]
        public void DistanceToRectIsZeroInsideAndEuclideanOutside()
        {
            Vector2 min = new Vector2(0f, 0f), max = new Vector2(10f, 10f);
            Assert.AreEqual(0f, WorldChunkMath.DistanceToRect(new Vector2(5f, 5f), min, max));
            Assert.AreEqual(0f, WorldChunkMath.DistanceToRect(new Vector2(10f, 0f), min, max));
            Assert.AreEqual(3f, WorldChunkMath.DistanceToRect(new Vector2(13f, 5f), min, max), 1e-5f);
            Assert.AreEqual(5f, WorldChunkMath.DistanceToRect(new Vector2(-3f, -4f), min, max), 1e-5f);
        }

        [Test]
        public void NearestDistanceUsesTheClosestViewerAndClampsTheCount()
        {
            Vector2 min = Vector2.zero, max = new Vector2(10f, 10f);
            var points = new[] { new Vector2(100f, 5f), new Vector2(12f, 5f), new Vector2(-50f, -50f) };
            Assert.AreEqual(2f, WorldChunkMath.NearestDistanceToRect(points, 3, min, max), 1e-5f);
            // Only the first viewer counts with a count of 1; a count beyond the array is clamped.
            Assert.AreEqual(90f, WorldChunkMath.NearestDistanceToRect(points, 1, min, max), 1e-5f);
            Assert.AreEqual(2f, WorldChunkMath.NearestDistanceToRect(points, 99, min, max), 1e-5f);
            Assert.IsTrue(float.IsPositiveInfinity(WorldChunkMath.NearestDistanceToRect(points, 0, min, max)));
            Assert.IsTrue(float.IsPositiveInfinity(WorldChunkMath.NearestDistanceToRect(null, 3, min, max)));
        }

        [Test]
        public void ChunkCoordinatesFloorAndRangeIsASquare()
        {
            Assert.AreEqual(new Vector2Int(0, 0), WorldChunkMath.ChunkCoordOf(new Vector2(0f, 239.9f), 240f));
            Assert.AreEqual(new Vector2Int(-1, 1), WorldChunkMath.ChunkCoordOf(new Vector2(-0.1f, 240f), 240f));
            Assert.AreEqual(new Vector2Int(3, -2), WorldChunkMath.ChunkCoordOf(new Vector2(3.5f, -1.5f), 0f)); // invalid size = 1
            Assert.IsTrue(WorldChunkMath.WithinChunkRange(new Vector2(2f, -2f), new Vector2Int(0, 0), 2));
            Assert.IsFalse(WorldChunkMath.WithinChunkRange(new Vector2(3f, 0f), new Vector2Int(0, 0), 2));
        }

        [Test]
        public void WeatherTimeRunsForwardAndRejectsBadInput()
        {
            Assert.AreEqual(110.0, WorldChunkMath.ExtrapolateWeatherTime(100.0, 50.0, 55.0, 2f), 1e-9);
            Assert.AreEqual(100.0, WorldChunkMath.ExtrapolateWeatherTime(100.0, 50.0, 49.0, 1f), 1e-9);    // clocks crossed
            Assert.AreEqual(100.0 + WorldChunkMath.MaxWeatherExtrapolation, WorldChunkMath.ExtrapolateWeatherTime(100.0, 0.0, 1e9, 1f), 1e-6);
            Assert.AreEqual(100.0, WorldChunkMath.ExtrapolateWeatherTime(100.0, 0.0, 10.0, float.NaN), 1e-9);
            Assert.AreEqual(100.0, WorldChunkMath.ExtrapolateWeatherTime(100.0, 0.0, 10.0, -3f), 1e-9);
            Assert.AreEqual(0.0, WorldChunkMath.ExtrapolateWeatherTime(double.NaN, 0.0, 10.0, 1f), 1e-9);
        }

        // ------------------------------------------------------------------ viewers

        [Test]
        public void SetViewersSkipsNullsAndDuplicatesAndSetsThePrimaryViewer()
        {
            EndlessTerrain terrain = Make<EndlessTerrain>("Terrain");
            Transform a = MakeTransform("A"), b = MakeTransform("B");
            terrain.SetViewers(new List<Transform> { null, a, a, b });
            Assert.AreSame(a, terrain.viewer);

            terrain.SetViewers(new List<Transform>());
            Assert.IsNull(terrain.viewer);

            terrain.SetViewers(new[] { b });
            Assert.AreSame(b, terrain.viewer);
            terrain.SetViewers(null);
            Assert.IsNull(terrain.viewer);
        }

        [Test]
        public void ViewerHookDeclinesOffline()
        {
            Assert.IsFalse(NetAuthority.IsNetworked, "Edit Mode tests run offline.");
            EndlessTerrain terrain = Make<EndlessTerrain>("Terrain");
            Transform a = MakeTransform("A"), b = MakeTransform("B");
            terrain.viewer = a;
            Assert.IsFalse(WorldTerrainNet.StreamAroundViewers(terrain, new[] { b }));
            Assert.AreSame(a, terrain.viewer, "Offline the hub world keeps setting the single viewer itself.");
            Assert.IsFalse(WorldTerrainNet.StreamAroundViewers(null, new[] { b }));
        }

        // ------------------------------------------------------------------ removed placed objects

        [Test]
        public void PlacedObjectIdsCompareByValue()
        {
            var a = new PlacedObjectId(-2, 5, 17);
            var b = new PlacedObjectId(-2, 5, 17);
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, new PlacedObjectId(-2, 5, 18));
            Assert.AreNotEqual(a, new PlacedObjectId(5, -2, 17));
        }

        [Test]
        public void RemovingByIdOfflineRemembersOnceAndRaisesTheEvent()
        {
            EndlessTerrain terrain = Make<EndlessTerrain>("Terrain");
            var raised = new List<PlacedObjectId>();
            System.Action<EndlessTerrain, PlacedObjectId> handler = (t, id) => { if (t == terrain) raised.Add(id); };
            EndlessTerrain.PlacedObjectRemoved += handler;
            try
            {
                var id = new PlacedObjectId(1, 2, 3);
                Assert.IsTrue(terrain.RemovePlacedObjectById(id));
                Assert.IsFalse(terrain.RemovePlacedObjectById(id), "Already removed.");
                Assert.IsFalse(terrain.RemovePlacedObjectById(new PlacedObjectId(1, 2, -1)), "Invalid index.");
                CollectionAssert.AreEqual(new[] { id }, raised);
                CollectionAssert.Contains(new List<PlacedObjectId>(terrain.RemovedObjects), id);
            }
            finally
            {
                EndlessTerrain.PlacedObjectRemoved -= handler;
            }
        }

        [Test]
        public void SyncRemovedObjectsReplacesTheSet()
        {
            EndlessTerrain terrain = Make<EndlessTerrain>("Terrain");
            var keep = new PlacedObjectId(0, 0, 1);
            var drop = new PlacedObjectId(0, 0, 2);
            var add = new PlacedObjectId(4, -1, 9);
            terrain.SetRemovedObjects(new[] { keep, drop });
            terrain.SyncRemovedObjects(new List<PlacedObjectId> { keep, add, add });
            var now = new List<PlacedObjectId>(terrain.RemovedObjects);
            Assert.AreEqual(2, now.Count);
            CollectionAssert.Contains(now, keep);
            CollectionAssert.Contains(now, add);
            CollectionAssert.DoesNotContain(now, drop);

            terrain.SyncRemovedObjects(null);
            Assert.AreEqual(0, terrain.RemovedObjects.Count);
        }

        // ------------------------------------------------------------------ weather

        [Test]
        public void WeatherStateDiffersBeyondClockOnlyForWhatMustApplyAtOnce()
        {
            var a = new WeatherSyncState { valid = true, weatherTime = 10.0, capturedAt = 1.0, worldSeed = 7, timeOfDay = -1f };
            WeatherSyncState b = a;
            b.weatherTime = 20.0;
            b.capturedAt = 11.0;
            Assert.IsFalse(a.DiffersBeyondClock(b), "Only the clock moved.");
            Assert.AreNotEqual(a, b);

            b = a;
            b.forced = true;
            b.forcedType = WeatherType.Rain;
            Assert.IsTrue(a.DiffersBeyondClock(b));
            b = a;
            b.worldSeed = 8;
            Assert.IsTrue(a.DiffersBeyondClock(b));
            b = a;
            b.timeOfDay = 12f;
            Assert.IsTrue(a.DiffersBeyondClock(b));
            b = a;
            b.valid = false;
            Assert.IsTrue(a.DiffersBeyondClock(b));
        }

        [Test]
        public void ApplyingTheServersWeatherSetsClockAndForcing()
        {
            WeatherSystem weather = Make<WeatherSystem>("Weather");
            var state = new WeatherSyncState
            {
                valid = true, weatherTime = 500.0, capturedAt = 100.0, timeOfDay = 9f,
                forced = true, forcedType = WeatherType.Snow, forcedStrength = 0.7f,
            };
            weather.ApplySyncState(state, 104.0);
            Assert.AreEqual(504.0, weather.WeatherTime, 1e-6);
            Assert.AreEqual(9f, weather.TimeOfDay);
            Assert.IsTrue(weather.IsForced);
            Assert.AreEqual(WeatherType.Snow, weather.Target.Type);

            state.forced = false;
            weather.ApplySyncState(state, 100.0);
            Assert.IsFalse(weather.IsForced);
            Assert.AreEqual(500.0, weather.WeatherTime, 1e-6);

            // Invalid states change nothing.
            weather.ApplySyncState(new WeatherSyncState { valid = false, weatherTime = 1.0 }, 0.0);
            Assert.AreEqual(500.0, weather.WeatherTime, 1e-6);
        }

        [Test]
        public void CapturedWeatherRoundTrips()
        {
            WeatherSystem server = Make<WeatherSystem>("Server Weather");
            server.WeatherTime = 1234.5;
            server.ForceWeather(WeatherType.Thunderstorm, 0.4f);
            WeatherSyncState s = server.CaptureSyncState(50.0);
            Assert.IsTrue(s.valid);
            Assert.AreEqual(1234.5, s.weatherTime, 1e-9);
            Assert.AreEqual(50.0, s.capturedAt, 1e-9);
            Assert.IsTrue(s.forced);
            Assert.AreEqual(WeatherType.Thunderstorm, s.forcedType);
            Assert.AreEqual(0.4f, s.forcedStrength, 1e-6f);
            Assert.AreEqual(0f, s.forcedSecondsLeft, "Forced until cleared.");

            WeatherSystem client = Make<WeatherSystem>("Client Weather");
            client.ApplySyncState(s, 50.0);
            Assert.AreEqual(1234.5, client.WeatherTime, 1e-9);
            Assert.IsTrue(client.IsForced);
            Assert.AreEqual(WeatherType.Thunderstorm, client.Target.Type);
        }
    }
}
#endif
