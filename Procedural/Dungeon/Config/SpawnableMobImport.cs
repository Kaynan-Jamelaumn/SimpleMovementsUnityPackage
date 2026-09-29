using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Reuses the world's mob definitions (<see cref="SpawnableMob"/>, from Procedural/World/Spawnable) for dungeon
    /// encounters: prefab, weight, rarity and pack behaviour carry over; biome and height preferences don't apply
    /// underground.
    /// </summary>
    public static class SpawnableMobImport
    {
        public static EncounterEntry ToEncounter(SpawnableMob mob, float cellSize = 1.5f)
        {
            if (mob == null)
                return null;
            int pack = Mathf.Max(1, mob.preferredPackSize);
            return new EncounterEntry
            {
                name = mob.mobPrefab != null ? mob.mobPrefab.name : "Mob",
                prefab = mob.mobPrefab,
                // Rarer mobs (higher rarity level) are less likely and cost more budget.
                weight = Mathf.Max(0.01f, mob.spawnWeight / Mathf.Max(1, mob.rarityLevel) * 5f),
                cost = Mathf.Max(1, mob.rarityLevel / 3),
                packSize = mob.isPackAnimal ? new IntRange(Mathf.Max(2, pack - 1), pack + 1) : new IntRange(1, 1),
                packRadius = Mathf.Max(1.5f, mob.packSpreadRadius / Mathf.Max(0.5f, cellSize) * 0.5f),
            };
        }

        /// <summary>Adds every mob of a world mob list to an encounter table.</summary>
        public static int AddAll(DungeonEncounterTable table, System.Collections.Generic.IEnumerable<SpawnableMob> mobs, float cellSize = 1.5f)
        {
            int added = 0;
            if (table == null || mobs == null)
                return 0;
            foreach (SpawnableMob mob in mobs)
            {
                EncounterEntry entry = ToEncounter(mob, cellSize);
                if (entry == null)
                    continue;
                table.entries.Add(entry);
                added++;
            }
            return added;
        }
    }
}
