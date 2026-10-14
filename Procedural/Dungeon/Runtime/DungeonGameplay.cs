using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Short messages for the players ("The doors slam shut!", "You found the Vault Key"). Shown above the hotbar by the
    /// player's Inventory Manager when there is one; <see cref="Shown"/> lets any other UI show them instead or as well.
    /// </summary>
    public static class DungeonMessages
    {
        /// <summary>Raised for every message (text, important).</summary>
        public static event Action<string, bool> Shown;

        /// <summary>Also log messages to the Console (handy without a UI).</summary>
        public static bool LogToConsole;

        /// <summary>
        /// Shows a message about <paramref name="context"/> (a dungeon object) to the players of its dungeon. Offline it is
        /// the same as <see cref="Show(string, bool)"/>; on a network server it goes to the clients of that run, and is
        /// shown here only when this machine's player is in it.
        /// </summary>
        public static void Show(Component context, string text, bool important = false)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
            DungeonInstance dungeon = GameNet.NetAuthority.IsNetworked ? DungeonNet.DungeonOf(context) : null;
            if (dungeon == null || dungeon.IsReplica)
            {
                Show(text, important);
                return;
            }
            DungeonNet.Message(dungeon, text, important);
            CombatEntity local = PlayerLocator.Local;
            if (local != null && local.gameObject.scene == dungeon.gameObject.scene)
                Show(text, important);
            else if (LogToConsole)
                Debug.Log("[Dungeon] " + text);
        }

        /// <summary>Shows a message to this machine's player.</summary>
        public static void Show(string text, bool important = false)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
            Shown?.Invoke(text, important);
            if (LogToConsole)
                Debug.Log("[Dungeon] " + text);
            InventoryManager inventory = LocalInventory();
            if (inventory != null)
                inventory.ShowMessage(text, important ? InventoryFeedback.Kind.Warning : InventoryFeedback.Kind.Info);
        }

        /// <summary>This machine's player's inventory (its feedback line shows the message), or any inventory.</summary>
        private static InventoryManager LocalInventory()
        {
            InventoryManager[] all = UnityEngine.Object.FindObjectsByType<InventoryManager>(FindObjectsInactive.Exclude);
            if (all.Length == 0)
                return null;
            CombatEntity local = PlayerLocator.Local;
            if (local != null)
                foreach (InventoryManager inv in all)
                    if (inv != null && inv.Player != null && (inv.Player == local.gameObject || local.transform.IsChildOf(inv.Player.transform) || inv.Player.transform.IsChildOf(local.transform)))
                        return inv;
            return all[0];
        }
    }

    /// <summary>
    /// The keys a party has found in a dungeon (shared by every player in it), by key id. Cleared with the dungeon.
    /// </summary>
    public static class DungeonKeyRing
    {
        private static readonly Dictionary<DungeonInstance, HashSet<int>> Keys = new Dictionary<DungeonInstance, HashSet<int>>();

        /// <summary>A key was picked up (dungeon, key id).</summary>
        public static event Action<DungeonInstance, int> KeyAdded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Keys.Clear();

        public static bool Has(DungeonInstance dungeon, int key) =>
            dungeon != null && Keys.TryGetValue(dungeon, out HashSet<int> set) && set.Contains(key);

        public static void Add(DungeonInstance dungeon, int key)
        {
            if (dungeon == null)
                return;
            if (!Keys.TryGetValue(dungeon, out HashSet<int> set))
                Keys[dungeon] = set = new HashSet<int>();
            if (set.Add(key))
                KeyAdded?.Invoke(dungeon, key);
        }

        public static void Clear(DungeonInstance dungeon)
        {
            if (dungeon != null)
                Keys.Remove(dungeon);
        }
    }

    /// <summary>What happened in a dungeon run (read it from <see cref="DungeonInstance.Stats"/>, e.g. for an end screen).</summary>
    [Serializable]
    public class DungeonRunStats
    {
        public int mobsKilled;
        public int elitesKilled;
        public int bossesKilled;
        public int chestsOpened;
        public int keysFound;
        public int secretsFound;
        public int puzzlesSolved;
        public int roomsCleared;
        public int shrinesUsed;
        public int shortcutsOpened;
        public float startedAt;

        /// <summary>Seconds since the dungeon became playable.</summary>
        public float Elapsed => Time.time - startedAt;

        /// <summary>Raised whenever a counter changes.</summary>
        public event Action<DungeonRunStats> Changed;

        internal void Bump(ref int counter)
        {
            counter++;
            Changed?.Invoke(this);
        }

        public override string ToString() =>
            $"{mobsKilled} mobs ({elitesKilled} elites, {bossesKilled} bosses), {chestsOpened} chests, {keysFound} keys, {secretsFound} secrets, " +
            $"{puzzlesSolved} puzzles, {roomsCleared} rooms cleared, {shrinesUsed} shrines, {shortcutsOpened} shortcuts in {Elapsed:0}s";
    }

    /// <summary>Player queries shared by the dungeon's mechanics (multiplayer-safe: every player counts, not one tagged object).</summary>
    public static class DungeonPlayers
    {
        /// <summary>The nearest living player within <paramref name="radius"/> (meters, horizontal), or null.</summary>
        public static CombatEntity NearestAlive(Vector3 position, float radius)
        {
            CombatEntity best = null;
            float bestSq = radius * radius;
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null || !p.IsAlive)
                    continue;
                Vector3 d = p.transform.position - position;
                if (Mathf.Abs(d.y) > 3f)
                    continue;
                d.y = 0f;
                if (d.sqrMagnitude <= bestSq)
                {
                    bestSq = d.sqrMagnitude;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>Is <paramref name="player"/> alive and within <paramref name="radius"/> (meters, horizontal; 3 m vertical) of <paramref name="position"/>?</summary>
        public static bool IsWithin(CombatEntity player, Vector3 position, float radius)
        {
            if (player == null || !player.IsAlive)
                return false;
            Vector3 d = player.transform.position - position;
            if (Mathf.Abs(d.y) > 3f)
                return false;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        /// <summary>Living players standing in an area of a floor.</summary>
        public static int CountInArea(DungeonInstance dungeon, int floor, int area)
        {
            if (dungeon == null || dungeon.Layout == null)
                return 0;
            int n = 0;
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null || !p.IsAlive)
                    continue;
                if (!dungeon.WorldToCell(p.transform.position, out int f, out Vector2Int cell) || f != floor)
                    continue;
                TileGrid g = dungeon.Layout.Floors[f].Grid;
                if (g.Area[g.Index(cell)] == area)
                    n++;
            }
            return n;
        }

        /// <summary>The player (a Combat Entity) behind a collider, if it is alive.</summary>
        public static CombatEntity FromCollider(Collider other)
        {
            CombatEntity e = PlayerLocator.FromCollider(other);
            return e != null && e.IsAlive ? e : null;
        }
    }
}
