using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// The dungeon entry point (replaces the old DungeonGenerator component). Put it on an object whose position is
    /// the dungeon's origin, give it a <see cref="DungeonProfile"/>, and call <see cref="Generate(DungeonRequest)"/> -
    /// or let a portal do it through <see cref="DungeonSession"/>.
    ///
    /// Generation (all stages plus mesh data) runs on a background thread; the scene objects are then created a few
    /// milliseconds per frame. <see cref="Ready"/> fires once the first floors are playable; the rest keep building.
    /// </summary>
    public class DungeonManager : MonoBehaviour
    {
        [Header("What To Build")]
        [Tooltip("REQUIRED (unless the Portal sets one). The Dungeon Profile: the kind of dungeon to build - floors, styles, rooms, roles, population, theme. A Portal's own Dungeon Profile overrides this one for that portal.")]
        public DungeonProfile profile;
        [Tooltip("The request used by Generate() and Generate On Start: seed (0 = random), size, difficulty, floor count, style override. Portals build their own request (seed from the world position) and ignore this.")]
        public DungeonRequest request = new DungeonRequest();
        [Tooltip("Generate a dungeon as soon as the scene starts (for a dedicated dungeon scene or testing). Leave OFF for the prefab a Portal uses - the portal starts generation.")]
        public bool generateOnStart;

        [Header("How To Build")]
        [Tooltip("Generate on a background thread (recommended). Off = on the main thread, which freezes the game for the generation time (50-200 ms per dungeon).")]
        public bool useWorkerThread = true;
        [Tooltip("What the exit portal on the last floor does: Complete Dungeon (back to the world, dungeon counted as completed), Return To World, or Next Dungeon (a deeper, harder one).")]
        public DungeonPortal.PortalAction exitPortalAction = DungeonPortal.PortalAction.CompleteDungeon;
        [Tooltip("Floors that must be fully built (with NavMesh and mobs) before the player is let in. Deeper floors keep building behind them. 1 = fastest entry.")]
        [Range(1, 4)] public int playableFloors = 2;

        [Header("While the player is inside (used by DungeonSession)")]
        [Tooltip("OPTIONAL. Extra components to disable while the player is in the dungeon (EndlessTerrain, WeatherSystem, SpawnerManager and the world spawners are paused automatically). Re-enabled on leaving.")]
        public Behaviour[] pauseWhileInside = new Behaviour[0];
        [Tooltip("OPTIONAL. Objects to deactivate while inside (e.g. the world's terrain root, a skybox rig). Reactivated on leaving.")]
        public GameObject[] hideWhileInside = new GameObject[0];

        public DungeonInstance Current { get; private set; }
        public bool IsGenerating { get; private set; }
        /// <summary>0..1 over generation and building.</summary>
        public float Progress { get; private set; }
        public string Status { get; private set; } = "Idle";
        /// <summary>The report of the last generation (timings, retries, warnings).</summary>
        public GenerationReport LastReport { get; private set; }

        /// <summary>The first floors are built and playable.</summary>
        public event Action<DungeonInstance> Ready;
        /// <summary>Every floor is built.</summary>
        public event Action<DungeonInstance> Completed;
        public event Action<string> Failed;
        public event Action Cleared;

        private readonly DungeonObjectPool pool = new DungeonObjectPool();
        private DungeonBuilder builder;
        private Coroutine routine;
        private WorkToken token;

        private void Start()
        {
            if (generateOnStart && profile != null)
                Generate();
        }

        private void OnDestroy()
        {
            Cancel();
            Clear();
            pool.Clear();
        }

        public void Generate() => Generate(request);

        /// <summary>Starts building a dungeon (clearing the current one).</summary>
        public void Generate(DungeonRequest req)
        {
            Cancel();
            Clear();
            req = (req ?? new DungeonRequest()).Clone();
            if (req.seed == 0)
                req.seed = NewSeed();
            routine = StartCoroutine(GenerateRoutine(req));
        }

        /// <summary>Generates and builds synchronously (editor tools, tests). No NavMesh wait, no frame budget.</summary>
        public DungeonInstance GenerateImmediate(DungeonRequest req)
        {
            Cancel();
            Clear();
            req = (req ?? new DungeonRequest()).Clone();
            if (req.seed == 0)
                req.seed = NewSeed();
            CompiledProfile compiled = CompiledProfile.Compile(profile);
            DungeonPipeline pipeline = DungeonPipeline.CreateDefault(compiled.CustomStages);
            DungeonLayout layout = pipeline.Generate(req, compiled);
            LastReport = layout.Report;
            List<FloorMeshData> meshes = DungeonMeshing.BuildAll(layout, compiled);
            builder = new DungeonBuilder(compiled, layout, meshes, pool);
            IEnumerator build = builder.Build(transform, new DungeonBuilder.Settings { Immediate = true, ExitAction = exitPortalAction, PlayableFloors = playableFloors });
            while (build.MoveNext())
            {
            }
            Current = builder.Instance;
            Current.Manager = this;
            Status = "Ready";
            Progress = 1f;
            return Current;
        }

        private static int NewSeed()
        {
            int seed = Guid.NewGuid().GetHashCode();
            return seed == 0 ? 1 : seed;
        }

        private sealed class Job
        {
            public DungeonRequest Request;
            public CompiledProfile Profile;
            public DungeonPipeline Pipeline;
            public WorkToken Token;
            public DungeonLayout Layout;
            public List<FloorMeshData> Meshes;
            public Exception Error;
            private int done;

            public bool Done => Volatile.Read(ref done) == 1;

            public void Run()
            {
                try
                {
                    Layout = Pipeline.Generate(Request, Profile, Token);
                    if (Token == null || !Token.IsCancelled)
                        Meshes = DungeonMeshing.BuildAll(Layout, Profile);
                }
                catch (Exception e)
                {
                    Error = e;
                }
                finally
                {
                    Volatile.Write(ref done, 1);
                }
            }
        }

        private IEnumerator GenerateRoutine(DungeonRequest req)
        {
            IsGenerating = true;
            Progress = 0f;
            Status = "Generating layout";
            if (profile == null)
                Debug.LogWarning($"{name}: no Dungeon Profile assigned - using defaults.", this);

            CompiledProfile compiled = CompiledProfile.Compile(profile);
            token = new WorkToken();
            var job = new Job
            {
                Request = req,
                Profile = compiled,
                Pipeline = DungeonPipeline.CreateDefault(compiled.CustomStages),
                Token = token,
            };
            if (useWorkerThread && Application.isPlaying)
                DungeonWorkers.Pool.Enqueue(job.Run, null, token);
            else
                job.Run();

            float started = Time.realtimeSinceStartup;
            while (!job.Done)
            {
                Progress = Mathf.Min(0.3f, (Time.realtimeSinceStartup - started) * 0.3f);
                yield return null;
            }
            if (token.IsCancelled)
                yield break;
            if (job.Error != null)
            {
                IsGenerating = false;
                Status = "Failed";
                string message = job.Error is DungeonGenerationException ? job.Error.Message : job.Error.ToString();
                Debug.LogError($"{name}: dungeon generation failed: {message}", this);
                routine = null;
                Failed?.Invoke(message);
                yield break;
            }

            LastReport = job.Layout.Report;
            if (compiled.Validation.logReport)
                Debug.Log(job.Layout.Describe() + job.Layout.Report, this);

            Status = "Building";
            builder = new DungeonBuilder(compiled, job.Layout, job.Meshes, pool);
            bool readyRaised = false;
            var settings = new DungeonBuilder.Settings
            {
                ExitAction = exitPortalAction,
                PlayableFloors = playableFloors,
                Progress = (p, what) =>
                {
                    Progress = 0.3f + 0.7f * p;
                    Status = what;
                },
                Playable = instance =>
                {
                    Current = instance;
                    Current.Manager = this;
                    readyRaised = true;
                    Ready?.Invoke(instance);
                },
            };
            IEnumerator build = builder.Build(transform, settings);
            while (true)
            {
                bool more;
                try
                {
                    more = build.MoveNext();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                    IsGenerating = false;
                    Status = "Failed";
                    routine = null;
                    if (!readyRaised)
                        Failed?.Invoke(e.Message);
                    yield break;
                }
                if (!more)
                    break;
                yield return build.Current;
            }

            IsGenerating = false;
            Progress = 1f;
            Status = "Ready";
            routine = null;
            Completed?.Invoke(Current);
        }

        /// <summary>Stops a generation in progress.</summary>
        public void Cancel()
        {
            token?.Cancel();
            token = null;
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            IsGenerating = false;
        }

        /// <summary>Removes the current dungeon (prefab instances go back to the pool).</summary>
        public void Clear()
        {
            if (builder != null)
                builder.Teardown();
            builder = null;
            bool had = Current != null;
            Current = null;
            Status = "Idle";
            if (had)
                Cleared?.Invoke();
        }
    }

    /// <summary>Background threads for dungeon generation (shared by all managers, stopped when the application quits).</summary>
    public static class DungeonWorkers
    {
        private static TerrainWorkerPool pool;
        private static readonly object Gate = new object();

        public static TerrainWorkerPool Pool
        {
            get
            {
                lock (Gate)
                {
                    if (pool == null)
                    {
                        pool = new TerrainWorkerPool(Mathf.Clamp(Environment.ProcessorCount - 1, 1, 4), "Dungeon Worker");
                        Application.quitting += Shutdown;
                    }
                    return pool;
                }
            }
        }

        private static void Shutdown()
        {
            lock (Gate)
            {
                pool?.Dispose();
                pool = null;
                Application.quitting -= Shutdown;
            }
        }
    }
}
