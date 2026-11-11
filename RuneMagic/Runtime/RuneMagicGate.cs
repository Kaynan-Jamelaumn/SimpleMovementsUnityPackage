using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// Who uses the Rune Magic System, and the rule that keeps them out of ability (essence) absorption.
    /// <para>A rune user is a player whose class is a rune-user class of the <see cref="RuneMagicDatabase"/>: a listed
    /// class asset, a class whose asset name, display name or Class archetype name is in Settings ▸ Rune User Class Names
    /// (default "Mage"), or a character with a listed Class archetype. Other classes are not affected in any way.</para>
    /// <para>Installed automatically before the first scene: it registers with <see cref="AbsorptionGate"/>, so
    /// <see cref="PlayerAbilityController.CanReceive"/> refuses every ability grant to rune users, ability pickups leave
    /// them alone for the other players, and (when Settings ▸ Essence Rolls On Rune User Kills is off) their kills roll no
    /// absorption. Where the rules run (offline, the server) that is the decision; other machines compute the same answer
    /// from the replicated class for their UI.</para>
    /// </summary>
    public static class RuneMagicGate
    {
        private sealed class Entry
        {
            public bool looked;
            public float nextLook;
            public PlayerStatusController status;
            public CharacterIdentity identity;
            public bool evaluated;
            public PlayerClass playerClass;
            public int archetypeCount;
            public RuneMagicDatabase database;
            public int databaseVersion;
            public bool result;
        }

        private static readonly Dictionary<CombatEntity, Entry> cache = new Dictionary<CombatEntity, Entry>(ReferenceComparer<CombatEntity>.Instance);
        private static readonly List<CombatEntity> stale = new List<CombatEntity>();
        private static readonly List<string> archetypeNames = new List<string>(4);
        private static bool installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            installed = false;
            cache.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInstall() => Install();

        /// <summary>Registers the absorption rules (done automatically when playing).</summary>
        public static void Install()
        {
            if (installed)
                return;
            installed = true;
            AbsorptionGate.AddBlockRule(BlocksAbsorption);
            AbsorptionGate.AddSuppressRule(SuppressesAbsorptionRolls);
        }

        /// <summary>Removes the absorption rules (tests, tools).</summary>
        public static void Uninstall()
        {
            installed = false;
            AbsorptionGate.RemoveBlockRule(BlocksAbsorption);
            AbsorptionGate.RemoveSuppressRule(SuppressesAbsorptionRolls);
            cache.Clear();
        }

        public static bool IsInstalled => installed;

        /// <summary>Rune users can never receive an absorbed ability (design directive 3; not a setting).</summary>
        private static bool BlocksAbsorption(CombatEntity receiver, AbilityGrant grant) => IsRuneUser(receiver);

        private static bool SuppressesAbsorptionRolls(CombatEntity killer)
        {
            RuneMagicDatabase db = RuneMagicDatabase.Instance;
            return db != null && !db.Catalog.Settings.essenceRollsOnRuneUserKills && IsRuneUser(killer);
        }

        // ------------------------------------------------------------------ queries
        /// <summary>Is the player of <paramref name="anyPart"/> a rune user? (Anything on the player; false for mobs.)</summary>
        public static bool IsRuneUser(Component anyPart)
        {
            if (anyPart == null)
                return false;
            CombatEntity e = anyPart as CombatEntity;
            if (e == null)
                e = CombatEntity.Resolve(anyPart.gameObject);
            return IsRuneUser(e);
        }

        /// <summary>Is <paramref name="entity"/> a player whose class uses Rune Magic? Cached; follows class changes.</summary>
        public static bool IsRuneUser(CombatEntity entity)
        {
            if (entity == null || entity.Kind != CombatEntity.EntityKind.Player)
                return false;
            RuneMagicDatabase db = RuneMagicDatabase.Instance;
            if (db == null || !db.Catalog.Settings.enabled)
                return false;

            Entry entry = Lookup(entity);
            PlayerClass playerClass = entry.status != null ? entry.status.CurrentPlayerClass : null;
            int archetypes = entry.identity != null ? entry.identity.Archetypes.Count : 0;
            if (entry.evaluated && entry.database == db && entry.databaseVersion == db.Version &&
                entry.playerClass == playerClass && entry.archetypeCount == archetypes)
                return entry.result;

            entry.result = IsRuneUserClass(playerClass, entry.identity != null ? entry.identity.Archetypes : null, db);
            entry.evaluated = true;
            entry.database = db;
            entry.databaseVersion = db.Version;
            entry.playerClass = playerClass;
            entry.archetypeCount = archetypes;
            return entry.result;
        }

        /// <summary>
        /// Is <paramref name="playerClass"/> (with the character's <paramref name="archetypes"/>, optional) a rune-user
        /// class of <paramref name="database"/> (null = the project's)? Only Class archetypes are considered.
        /// </summary>
        public static bool IsRuneUserClass(PlayerClass playerClass, IEnumerable<CharacterArchetype> archetypes = null, RuneMagicDatabase database = null)
        {
            if (database == null)
                database = RuneMagicDatabase.Instance;
            if (database == null)
                return false;
            RuneMagicSettings settings = database.Catalog.Settings;
            if (!settings.enabled)
                return false;
            if (database.ListsClass(playerClass))
                return true;

            archetypeNames.Clear();
            if (playerClass != null && playerClass.archetype != null)
            {
                if (database.ListsArchetype(playerClass.archetype))
                    return true;
                archetypeNames.Add(playerClass.archetype.Name);
            }
            if (archetypes != null)
                foreach (CharacterArchetype a in archetypes)
                {
                    if (a == null || a.kind != ArchetypeKind.Class)
                        continue;
                    if (database.ListsArchetype(a))
                        return true;
                    archetypeNames.Add(a.Name);
                }

            string assetName = playerClass != null ? playerClass.name : null;
            string displayName = playerClass != null ? playerClass.GetClassName() : null;
            return RuneUserRules.IsRuneUserClass(settings, assetName, displayName, archetypeNames);
        }

        /// <summary>Forget what is cached about <paramref name="entity"/> (null = everyone).</summary>
        public static void Invalidate(CombatEntity entity = null)
        {
            if (entity == null) cache.Clear();
            else cache.Remove(entity);
        }

        private static Entry Lookup(CombatEntity entity)
        {
            if (!cache.TryGetValue(entity, out Entry entry))
            {
                if (cache.Count >= 64)
                    PurgeDestroyed();
                entry = new Entry();
                cache[entity] = entry;
            }
            // Look the components up once; a player without them yet (still being set up) is looked at again once a second.
            if (!entry.looked || (entry.status == null && Time.unscaledTime >= entry.nextLook))
            {
                entry.looked = true;
                entry.nextLook = Time.unscaledTime + 1f;
                entry.status = entity.GetComponentInParent<PlayerStatusController>();
                if (entry.status == null)
                    entry.status = entity.GetComponentInChildren<PlayerStatusController>();
                entry.identity = CharacterIdentity.For(entity, false);
            }
            return entry;
        }

        private static void PurgeDestroyed()
        {
            stale.Clear();
            foreach (CombatEntity e in cache.Keys)
                if (e == null) stale.Add(e);
            foreach (CombatEntity e in stale)
                cache.Remove(e);
            stale.Clear();
        }
    }
}
