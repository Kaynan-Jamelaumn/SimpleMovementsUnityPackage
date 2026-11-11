using ProceduralDungeon;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// A source of magical knowledge in a dungeon (design spec 17.1): an ancient inscription, a magical artifact, a sealed
    /// chamber... A rune user interacts with it (the player's Interact input, like every dungeon object), plays a minigame
    /// and answers a riddle about a hidden concept. A correct answer permanently adds the concept to their knowledge; a wrong
    /// one burns this kind of source for that concept, so they must find another dungeon that teaches it.
    /// <para>Put it on the prop's prefab (it needs a collider for the player's interaction ray) and list the prefab in the
    /// dungeon's Prop Table, in the areas where such places belong (libraries, shrines, crypts, secret rooms): the dungeon
    /// then spawns it and its difficulty comes from the dungeon's tier. Players who are not rune users find nothing here.</para>
    /// <para>Where the rules run (offline, the server) it starts the discovery on the player's Rune Soul; a network client's
    /// Interact is relayed by the dungeon's own request path and checked there (distance, line of play). It is never used up:
    /// what a player can still learn here is their own, saved with their soul.</para>
    /// </summary>
    [AddComponentMenu("SimpleMovements/Rune Magic/Concept Discovery Source")]
    public class ConceptDiscoverySource : DungeonInteractable
    {
        [Tooltip("Which kind of source this is (Rune Magic Database ▸ Discovery Sources): it decides which runes' concepts it teaches. " +
                 "A wrong answer burns this kind for that concept, so give a dungeon several different kinds.")]
        [SerializeField] private string sourceId = "ancient_inscription";
        [Tooltip("Difficulty band index (0 = Beginner ... 4 = Legendary). -1 = from the dungeon's tier (outside dungeons: Beginner).")]
        [SerializeField] private int tierOverride = -1;
        [Tooltip("Teach this concept (id: wind.pressure) instead of a random one the player can learn. Empty = random.")]
        [SerializeField] private string fixedConcept = "";
        [Tooltip("Name shown in the prompt (empty = the source kind's name).")]
        [SerializeField] private string displayName = "";

        public string SourceId { get => sourceId; set => sourceId = value; }
        public int TierOverride { get => tierOverride; set => tierOverride = value; }
        public string FixedConcept { get => fixedConcept; set => fixedConcept = value; }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(displayName)) return displayName;
                DiscoverySourceDef def = RuneMagicDatabase.Instance.Catalog.DiscoverySource(sourceId);
                return def != null ? def.Name : name;
            }
        }

        /// <summary>The dungeon tier this source sits in (0 outside dungeons).</summary>
        public int DungeonTier
        {
            get
            {
                if (!TryGetComponent(out DungeonSpawned tag)) tag = GetComponentInParent<DungeonSpawned>();
                return tag != null ? tag.tier : 0;
            }
        }

        private void Reset()
        {
            holdToUse = 0f; // used with the Interact input: standing near it must not start a riddle
            uses = 0;
            cooldown = 1f;
        }

        /// <summary>Starts the discovery for <paramref name="player"/>. Nothing is used up; the soul decides what it can teach.</summary>
        protected override bool Use(CombatEntity player)
        {
            RuneSoul soul = RuneSoul.For(player);
            if (soul == null || !soul.IsRuneUser)
                return false; // the glyphs mean nothing to anyone else
            return soul.BeginDiscovery(sourceId, DungeonTier, tierOverride, fixedConcept, transform, out _);
        }

        // Never started by standing next to it (the base class's Hold To Use): a riddle is only opened on purpose.
        protected override void Update() { }

        // A source is never "used up": it teaches as long as the player can still learn from it.
        protected override void OnUsedUp() { }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.45f, 0.86f, 0.82f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, useDistance);
        }
    }
}
