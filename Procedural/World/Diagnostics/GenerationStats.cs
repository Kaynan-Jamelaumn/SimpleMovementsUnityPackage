using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

/// <summary>
/// How long each part of terrain generation takes: every stage records its time here (from worker threads and the
/// main thread), and the Generation Stats window (Window > SimpleMovements > Generation Stats) shows and copies
/// them. Recording costs a timestamp and a short lock per stage, not per cell. Set <see cref="Enabled"/> to false
/// to skip it entirely.
/// </summary>
public static class GenerationStats
{
    /// <summary>Whether stages are timed at all.</summary>
    public static bool Enabled = true;

    /// <summary>One timed stage.</summary>
    public sealed class Stage
    {
        public string Name;
        public string Group;
        public string Help;
        public long Count;
        public double TotalMs;
        public double MinMs = double.MaxValue;
        public double MaxMs;
        public double LastMs;

        public double AverageMs => Count > 0 ? TotalMs / Count : 0.0;

        public Stage Copy()
        {
            return (Stage)MemberwiseClone();
        }
    }

    // Stage names, in the order they happen, with their group and what they measure.
    public const string VoronoiPoints = "Voronoi points (per biome cell)";
    public const string WaterLakes = "Lakes and ponds";
    public const string WaterRivers = "Rivers (tracing)";
    public const string WaterLakeLevels = "Lake water levels";
    public const string WaterSetup = "Water setup total";
    public const string HeightMap = "Height map (biomes, landforms, coast)";
    public const string Erosion = "Erosion";
    public const string WaterApply = "Water carving and water map";
    public const string PlacementFields = "Object placement environment";
    public const string ChunkHeights = "Chunk heights total";
    public const string BiomeMap = "Biome map";
    public const string SplatPixels = "Texture splat maps";
    public const string TerrainMesh = "Terrain mesh";
    public const string WaterMesh = "Water mesh";
    public const string ChunkWorker = "Chunk total (worker thread)";
    public const string QueueWait = "Waiting for a worker thread";
    public const string LodMesh = "Distance LOD mesh";
    public const string ObjectPlacement = "Objects: deciding where (worker)";
    public const string ApplyMaterial = "Apply: material and textures";
    public const string ApplyMesh = "Apply: mesh upload";
    public const string ApplyWater = "Apply: water object";
    public const string ApplyTotal = "Apply: total (main thread)";
    public const string ColliderReady = "Collider cooked (job thread, wait)";
    public const string ObjectSpawnFrame = "Objects: creating (main thread, per frame)";
    public const string NavMesh = "NavMesh build (async, wait)";
    public const string ChunkVisible = "Request -> chunk visible";
    public const string ChunkObjects = "Request -> objects created";

    private static readonly string[][] Known =
    {
        new[] { VoronoiPoints, "Biomes", "Placing one Voronoi cell's biome points (done once per cell, when first needed)." },
        new[] { WaterLakes, "Water setup", "Finding the lakes and ponds a chunk touches (evaluating new lake sites)." },
        new[] { WaterRivers, "Water setup", "Tracing every river that can reach the chunk (cached: expensive for the first chunks of an area)." },
        new[] { WaterLakeLevels, "Water setup", "Working out lake water levels from the rivers around them." },
        new[] { WaterSetup, "Water setup", "Everything above plus the chunk's river raster." },
        new[] { HeightMap, "Heights", "The terrain height of every cell: biome blend, landforms, mountains, coast, volcanoes, lake and river carving." },
        new[] { Erosion, "Heights", "Thermal and hydraulic erosion (not recorded when off). With seamless erosion tiles this is building/assembling the tiles, which includes their heights." },
        new[] { WaterApply, "Heights", "Shore and river guarantees after erosion, and the chunk's water surface map." },
        new[] { PlacementFields, "Heights", "Slope, wetness and other maps the object placement reads." },
        new[] { ChunkHeights, "Heights", "The whole height stage of one chunk (water setup included)." },
        new[] { BiomeMap, "Chunk data", "Which biome each cell belongs to (and the texture blend)." },
        new[] { SplatPixels, "Chunk data", "The texture splat map pixels." },
        new[] { TerrainMesh, "Chunk data", "Vertices, normals and triangles of the terrain mesh." },
        new[] { WaterMesh, "Chunk data", "The water surface mesh." },
        new[] { ChunkWorker, "Chunk data", "One chunk's whole worker-thread job: heights, water, biome map, textures and meshes." },
        new[] { QueueWait, "Chunk data", "How long a chunk waited in the queue before a worker thread started it." },
        new[] { LodMesh, "Chunk data", "A coarser mesh for a distant chunk." },
        new[] { ObjectPlacement, "Objects", "Deciding where a chunk's trees, rocks and other objects go." },
        new[] { ApplyMaterial, "Main thread", "Creating the chunk's material and uploading its textures." },
        new[] { ApplyMesh, "Main thread", "Uploading the terrain mesh to the GPU." },
        new[] { ApplyWater, "Main thread", "Creating or updating the chunk's water object." },
        new[] { ApplyTotal, "Main thread", "Everything done on the main thread when a chunk's data arrives." },
        new[] { ColliderReady, "Main thread", "From scheduling the collider cooking job until the collider could be assigned." },
        new[] { ObjectSpawnFrame, "Main thread", "Main-thread time spent creating objects in one frame (kept under Object Spawn Budget)." },
        new[] { NavMesh, "Main thread", "From starting a chunk's NavMesh update until it finished." },
        new[] { ChunkVisible, "Overall", "From requesting a chunk until it has its mesh (what the player waits for)." },
        new[] { ChunkObjects, "Overall", "From requesting a chunk until all its objects exist." },
    };

    private static readonly object gate = new object();
    private static readonly Dictionary<string, Stage> stages = new Dictionary<string, Stage>();
    private static readonly List<Stage> order = new List<Stage>();
    private static readonly Dictionary<string, long> counters = new Dictionary<string, long>();
    private static readonly Stopwatch session = Stopwatch.StartNew();
    private static double firstChunkMs = -1.0;
    private static double firstObjectsMs = -1.0;

    /// <summary>Counters: "Chunks generated", "Objects created"...</summary>
    public const string ChunksGenerated = "Chunks generated";
    public const string ChunksApplied = "Chunks shown";
    public const string ObjectsCreated = "Objects created";
    public const string RiversTraced = "Rivers traced";

    /// <summary>A start time for <see cref="Record"/> (0 when timing is off).</summary>
    public static long Start()
    {
        return Enabled ? Stopwatch.GetTimestamp() : 0L;
    }

    /// <summary>Records the time since <paramref name="start"/> (from <see cref="Start"/>) for a stage.</summary>
    public static void Record(string name, long start)
    {
        if (start == 0L || !Enabled)
            return;
        Add(name, (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
    }

    /// <summary>Records a duration (milliseconds) for a stage.</summary>
    public static void Add(string name, double milliseconds)
    {
        if (!Enabled)
            return;
        lock (gate)
        {
            Stage stage = GetStage(name);
            stage.Count++;
            stage.TotalMs += milliseconds;
            stage.LastMs = milliseconds;
            if (milliseconds < stage.MinMs) stage.MinMs = milliseconds;
            if (milliseconds > stage.MaxMs) stage.MaxMs = milliseconds;
        }
    }

    /// <summary>Adds to a counter.</summary>
    public static void Count(string counter, long amount = 1)
    {
        if (!Enabled)
            return;
        lock (gate)
        {
            counters.TryGetValue(counter, out long value);
            counters[counter] = value + amount;
        }
    }

    /// <summary>Notes that a chunk became visible / got its objects (the first of each is kept, from the last reset).</summary>
    public static void MarkChunkVisible()
    {
        lock (gate)
        {
            if (firstChunkMs < 0.0)
                firstChunkMs = session.Elapsed.TotalMilliseconds;
        }
    }

    public static void MarkObjectsCreated()
    {
        lock (gate)
        {
            if (firstObjectsMs < 0.0)
                firstObjectsMs = session.Elapsed.TotalMilliseconds;
        }
    }

    /// <summary>Clears everything and restarts the session clock (the "first chunk" time counts from here).</summary>
    public static void Reset()
    {
        lock (gate)
        {
            stages.Clear();
            order.Clear();
            counters.Clear();
            firstChunkMs = firstObjectsMs = -1.0;
            session.Restart();
        }
    }

    /// <summary>Time since the last reset (or since the scripts were loaded).</summary>
    public static double SessionSeconds => session.Elapsed.TotalSeconds;
    /// <summary>Time from the last reset until the first chunk was visible; negative if none yet.</summary>
    public static double FirstChunkMs { get { lock (gate) return firstChunkMs; } }
    /// <summary>Time from the last reset until the first chunk had its objects; negative if none yet.</summary>
    public static double FirstObjectsMs { get { lock (gate) return firstObjectsMs; } }

    /// <summary>A copy of every stage recorded so far, in pipeline order.</summary>
    public static List<Stage> Snapshot()
    {
        lock (gate)
        {
            var list = new List<Stage>(order.Count);
            foreach (string[] known in Known)
                if (stages.TryGetValue(known[0], out Stage stage))
                    list.Add(stage.Copy());
            foreach (Stage stage in order)
                if (!IsKnown(stage.Name))
                    list.Add(stage.Copy());
            return list;
        }
    }

    public static Dictionary<string, long> Counters()
    {
        lock (gate)
            return new Dictionary<string, long>(counters);
    }

    /// <summary>What a stage measures (empty for unknown ones).</summary>
    public static string Describe(string name)
    {
        foreach (string[] known in Known)
            if (known[0] == name)
                return known[2];
        return "";
    }

    /// <summary>The stats as plain text (a table), for copying.</summary>
    public static string Report()
    {
        List<Stage> list = Snapshot();
        Dictionary<string, long> count = Counters();
        var text = new StringBuilder();
        text.AppendLine("=== Terrain generation stats ===");
        text.AppendLine($"Session: {SessionSeconds:0.0} s since reset");
        double first = FirstChunkMs, firstObjects = FirstObjectsMs;
        text.AppendLine($"First chunk visible after: {(first >= 0 ? (first / 1000.0).ToString("0.00") + " s" : "-")}");
        text.AppendLine($"First chunk with objects after: {(firstObjects >= 0 ? (firstObjects / 1000.0).ToString("0.00") + " s" : "-")}");
        foreach (KeyValuePair<string, long> pair in count)
            text.AppendLine($"{pair.Key}: {pair.Value}");
        text.AppendLine();
        text.AppendLine(string.Format("{0,-44} {1,7} {2,10} {3,10} {4,10} {5,10} {6,11}", "Stage", "Count", "Avg ms", "Min ms", "Max ms", "Last ms", "Total s"));
        string group = null;
        foreach (Stage stage in list)
        {
            if (stage.Group != group)
            {
                group = stage.Group;
                text.AppendLine($"-- {group}");
            }
            text.AppendLine(string.Format("{0,-44} {1,7} {2,10:0.00} {3,10:0.00} {4,10:0.00} {5,10:0.00} {6,11:0.00}",
                stage.Name, stage.Count, stage.AverageMs, stage.Count > 0 ? stage.MinMs : 0.0, stage.MaxMs, stage.LastMs, stage.TotalMs / 1000.0));
        }
        return text.ToString();
    }

    private static bool IsKnown(string name)
    {
        foreach (string[] known in Known)
            if (known[0] == name)
                return true;
        return false;
    }

    private static Stage GetStage(string name)
    {
        if (stages.TryGetValue(name, out Stage stage))
            return stage;
        stage = new Stage { Name = name, Group = "Other" };
        foreach (string[] known in Known)
        {
            if (known[0] == name)
            {
                stage.Group = known[1];
                stage.Help = known[2];
                break;
            }
        }
        stages[name] = stage;
        order.Add(stage);
        return stage;
    }
}
