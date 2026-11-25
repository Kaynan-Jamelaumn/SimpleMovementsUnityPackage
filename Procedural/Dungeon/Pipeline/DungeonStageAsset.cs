using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Base for project-specific stages kept as assets and listed on a profile (hazard passes, lock-and-key puzzles,
    /// custom room roles...). Run() is called on a worker thread: read settings from serialized fields, and never
    /// call Unity APIs from it.
    /// </summary>
    public abstract class DungeonStageAsset : ScriptableObject, IDungeonStage
    {
        [Tooltip("When this stage runs: after which built-in stage (e.g. After Population to add your own placements, After Roles to add roles before carving).")]
        public StageSlot slot = StageSlot.AfterPopulation;

        // Object.name may only be read on the main thread; cache it for the worker.
        private string cachedName;

        public virtual string Name => string.IsNullOrEmpty(cachedName) ? GetType().Name : cachedName;

        protected virtual void OnEnable()
        {
            cachedName = name;
        }

        /// <summary>
        /// Called on the main thread when a profile that lists this stage is compiled, before any generation starts: read
        /// whatever needs Unity (other assets, names, prefabs) here and keep it as plain data for <see cref="Run"/>, which
        /// runs on a worker thread. Generations already running keep what they started with.
        /// </summary>
        public virtual void Prepare()
        {
        }

        public abstract void Run(DungeonContext context);
    }
}
