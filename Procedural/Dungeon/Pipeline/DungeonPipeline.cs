using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// One step of dungeon generation. Stages only talk through the <see cref="DungeonLayout"/> in the context: each
    /// reads what earlier stages produced and adds its own part. They run on a worker thread, so they must not touch
    /// UnityEngine.Object (GameObjects, assets, most Unity APIs) - only plain data and math.
    /// </summary>
    public interface IDungeonStage
    {
        string Name { get; }
        void Run(DungeonContext context);
    }

    /// <summary>Where a custom stage runs relative to the built-in ones.</summary>
    public enum StageSlot
    {
        AfterMacroPlan,
        AfterLayout,
        AfterConnectivity,
        AfterRoles,
        AfterCarve,
        AfterValidation,
        AfterAnalysis,
        AfterPopulation,
    }

    /// <summary>Thrown by a stage when the current attempt can't produce a valid dungeon: the pipeline retries with a derived seed.</summary>
    public sealed class DungeonGenerationException : Exception
    {
        public DungeonGenerationException(string message) : base(message)
        {
        }
    }

    /// <summary>Everything a stage gets: the request, the compiled profile, the layout being built, random streams, cancellation.</summary>
    public sealed class DungeonContext
    {
        public readonly DungeonRequest Request;
        public readonly CompiledProfile Profile;
        public readonly DungeonLayout Layout;
        /// <summary>The seed of this attempt: every random stream derives from it.</summary>
        public readonly int Seed;
        public readonly WorkToken Token;

        public DungeonContext(DungeonRequest request, CompiledProfile profile, DungeonLayout layout, int seed, WorkToken token)
        {
            Request = request;
            Profile = profile;
            Layout = layout;
            Seed = seed;
            Token = token;
        }

        public GenerationReport Report => Layout.Report;

        /// <summary>A random stream for a purpose (stage name), optionally per floor / area.</summary>
        public DungeonRandom Random(string purpose, int a = 0, int b = 0) => DungeonRandom.Create(Seed, DungeonRandom.Salt(purpose), a, b);

        public void Warn(string message) => Layout.Report.Warn(message);

        public void ThrowIfCancelled()
        {
            if (Token != null && Token.IsCancelled)
                throw new OperationCanceledException("Dungeon generation was cancelled.");
        }

        /// <summary>Stops this attempt; the pipeline retries with another seed.</summary>
        public void Fail(string reason) => throw new DungeonGenerationException(reason);

        /// <summary>
        /// Runs <paramref name="body"/> for every floor - in parallel when running on a TerrainWorkerPool worker,
        /// in order otherwise. Each floor must only touch its own data (and read shared data).
        /// </summary>
        public void ForEachFloor(Action<FloorLayout> body)
        {
            List<FloorLayout> floors = Layout.Floors;
            TerrainWorkerPool pool = TerrainWorkerPool.Current;
            if (pool != null && floors.Count > 1)
            {
                pool.For(floors.Count, i =>
                {
                    ThrowIfCancelled();
                    body(floors[i]);
                });
            }
            else
            {
                foreach (FloorLayout floor in floors)
                {
                    ThrowIfCancelled();
                    body(floor);
                }
            }
        }
    }

    /// <summary>
    /// Runs the stages in order and retries the whole dungeon with a derived seed when one fails validation
    /// (bounded by the profile's Max Attempts). Deterministic: the same request and profile give the same layout,
    /// on any thread, in any order.
    /// </summary>
    public sealed class DungeonPipeline
    {
        public readonly List<IDungeonStage> Stages = new List<IDungeonStage>();

        /// <summary>The standard stages, with a profile's custom stage assets slotted in.</summary>
        public static DungeonPipeline CreateDefault(IEnumerable<DungeonStageAsset> customStages = null)
        {
            var pipeline = new DungeonPipeline();
            var custom = new List<DungeonStageAsset>();
            if (customStages != null)
                foreach (DungeonStageAsset s in customStages)
                    if (s != null)
                        custom.Add(s);

            void Add(IDungeonStage stage, StageSlot after)
            {
                pipeline.Stages.Add(stage);
                foreach (DungeonStageAsset c in custom)
                    if (c.slot == after)
                        pipeline.Stages.Add(c);
            }

            Add(new MacroPlanStage(), StageSlot.AfterMacroPlan);
            Add(new LayoutStage(), StageSlot.AfterLayout);
            Add(new ConnectivityStage(), StageSlot.AfterConnectivity);
            Add(new RolesStage(), StageSlot.AfterRoles);
            Add(new CarveStage(), StageSlot.AfterCarve);
            Add(new ValidateStage(), StageSlot.AfterValidation);
            Add(new AnalysisStage(), StageSlot.AfterAnalysis);
            Add(new PopulationStage(), StageSlot.AfterPopulation);
            return pipeline;
        }

        /// <summary>
        /// Generates a dungeon. Throws <see cref="DungeonGenerationException"/> when every attempt failed and
        /// OperationCanceledException when <paramref name="token"/> was cancelled.
        /// </summary>
        public DungeonLayout Generate(DungeonRequest request, CompiledProfile profile, WorkToken token = null)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            request = request != null ? request.Clone() : new DungeonRequest();
            if (request.seed == 0)
                request.seed = Environment.TickCount ^ 0x5F3759DF;

            var total = Stopwatch.StartNew();
            var failures = new List<string>();
            int attempts = Mathf.Max(1, profile.Validation.maxAttempts);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                int seed = attempt == 0 ? request.seed : (int)PlacementRandom.Hash(request.seed, 0xA77E, attempt, 0, 0);
                var layout = new DungeonLayout
                {
                    Seed = request.seed,
                    AttemptSeed = seed,
                    Attempt = attempt,
                    Request = request,
                    CellSize = profile.CellSize,
                    FloorSpacing = profile.FloorSpacing,
                };
                foreach (string w in profile.Warnings)
                    layout.Report.Warn(w);
                var context = new DungeonContext(request, profile, layout, seed, token);

                try
                {
                    foreach (IDungeonStage stage in Stages)
                    {
                        context.ThrowIfCancelled();
                        long start = GenerationStats.Start();
                        var watch = Stopwatch.StartNew();
                        stage.Run(context);
                        watch.Stop();
                        layout.Report.AddTime(stage.Name, watch.Elapsed.TotalMilliseconds);
                        GenerationStats.Record("Dungeon: " + stage.Name, start);
                    }
                }
                catch (DungeonGenerationException e)
                {
                    failures.Add($"attempt {attempt} (seed {seed}): {e.Message}");
                    continue;
                }

                layout.Report.Attempts = attempt + 1;
                layout.Report.FailedAttempts.AddRange(failures);
                layout.Report.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
                GenerationStats.Count("Dungeons generated");
                if (attempt > 0)
                    GenerationStats.Count("Dungeon retries", attempt);
                return layout;
            }

            throw new DungeonGenerationException($"No valid dungeon after {attempts} attempts for seed {request.seed}:\n  " + string.Join("\n  ", failures));
        }
    }
}
