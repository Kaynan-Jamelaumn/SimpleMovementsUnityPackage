using System;
using System.Collections.Generic;

namespace HubWorld
{
    /// <summary>
    /// Small integer ids for players, used by the <see cref="DuelBook"/> and anything else that refers to a player by
    /// number. Unity's instance ids are not used (<c>Object.GetInstanceID</c> is obsolete in recent Unity versions).
    /// </summary>
    /// <remarks>
    /// When the game runs networked, <see cref="NetworkIdResolver"/> returns the id of the player's network object
    /// (plus one), which is the same on the server and every client, so an id sent over the network names the same
    /// player everywhere. Without it (or for objects that are not networked) ids come from a local counter that starts
    /// far above any network id, so the two never collide. Ids are never 0 or negative.
    /// </remarks>
    public static class PlayerIds
    {
        /// <summary>First id handed out by the local counter (network ids stay below it).</summary>
        public const int LocalIdBase = 1 << 20;

        /// <summary>
        /// Set by the networking layer: the network id of an entity (a positive number, the same on every machine), or 0
        /// when the entity is not networked.
        /// </summary>
        public static Func<CombatEntity, int> NetworkIdResolver;

        private static readonly Dictionary<CombatEntity, int> ids = new Dictionary<CombatEntity, int>(ReferenceComparer<CombatEntity>.Instance);
        private static readonly Dictionary<int, CombatEntity> entities = new Dictionary<int, CombatEntity>();
        private static int nextLocal = LocalIdBase;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ids.Clear();
            entities.Clear();
            nextLocal = LocalIdBase;
            NetworkIdResolver = null;
        }

        /// <summary>The id of <paramref name="entity"/> (0 for null).</summary>
        public static int Of(CombatEntity entity)
        {
            if (entity is null)
                return 0;
            int net = NetworkIdResolver != null ? NetworkIdResolver(entity) : 0;
            if (ids.TryGetValue(entity, out int id))
            {
                if (net <= 0 || net == id)
                    return id;
                entities.Remove(id); // the entity became networked after it was first seen
            }
            id = net > 0 ? net : nextLocal++;
            ids[entity] = id;
            entities[id] = entity;
            return id;
        }

        /// <summary>The live entity behind <paramref name="id"/>, or null.</summary>
        public static CombatEntity Find(int id)
        {
            if (id <= 0)
                return null;
            if (entities.TryGetValue(id, out CombatEntity e))
            {
                if (e != null)
                    return e;
                entities.Remove(id);
                ids.Remove(e);
            }
            // Not seen on this machine yet (a networked player known only by its id): look among the players.
            IReadOnlyList<CombatEntity> players = CombatEntity.Players;
            for (int i = 0; i < players.Count; i++)
                if (players[i] != null && Of(players[i]) == id)
                    return players[i];
            return null;
        }

        /// <summary>Is <paramref name="entity"/> the player named by <paramref name="id"/>?</summary>
        public static bool Is(CombatEntity entity, int id) => entity != null && id > 0 && Of(entity) == id;
    }
}
