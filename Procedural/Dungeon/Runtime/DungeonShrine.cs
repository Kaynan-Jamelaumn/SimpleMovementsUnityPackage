using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A shrine altar: praying at it blesses every living player within Radius with one of its blessings (a timed buff
    /// through <see cref="TimedStatModifiers"/>), once.
    /// </summary>
    public class DungeonShrine : DungeonInteractable
    {
        [Serializable]
        public class Blessing
        {
            public string name = "Might";
            [Tooltip("Shown to the players.")]
            public string description = "+20% weapon damage";
            public List<CombatStatModifier> stats = new List<CombatStatModifier>();
        }

        [Tooltip("Players within this distance are blessed (meters).")]
        [Min(0f)] public float radius = 8f;
        [Tooltip("How long a blessing lasts (seconds).")]
        [Min(1f)] public float duration = 150f;
        [Tooltip("One is picked at random. Empty = Might, Fortitude, Swiftness, Insight.")]
        public List<Blessing> blessings = new List<Blessing>();

        public static event Action<DungeonShrine, Blessing> AnyBlessing;

        protected override bool Use(CombatEntity player)
        {
            if (blessings == null || blessings.Count == 0)
                blessings = Defaults();
            Blessing b = blessings[UnityEngine.Random.Range(0, blessings.Count)];
            int blessed = 0;
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null || !p.IsAlive || (p.transform.position - transform.position).sqrMagnitude > radius * radius)
                    continue;
                TimedStatModifiers.For(p).Apply(this, "Blessing of " + b.name, null, false, ElementType.Holy, duration, 1f, b.stats, null, null);
                blessed++;
            }
            if (blessed == 0)
                return false;
            DungeonInstance d = Dungeon;
            if (d != null)
                d.Stats.Bump(ref d.Stats.shrinesUsed);
            DungeonMessages.Show($"Blessing of {b.name}: {b.description} for {duration / 60f:0.#} minutes.", true);
            AnyBlessing?.Invoke(this, b);
            return true;
        }

        private static List<Blessing> Defaults()
        {
            Blessing Make(string name, string description, params (CombatStatType stat, float value)[] mods)
            {
                var b = new Blessing { name = name, description = description };
                foreach (var m in mods)
                    b.stats.Add(new CombatStatModifier { stat = m.stat, value = m.value });
                return b;
            }
            return new List<Blessing>
            {
                Make("Might", "+20% weapon damage, +4 Strength", (CombatStatType.WeaponDamage, 20f), (CombatStatType.Strength, 4f)),
                Make("Fortitude", "+15 Defense and Magic Resistance", (CombatStatType.Defense, 15f), (CombatStatType.MagicResistance, 15f)),
                Make("Swiftness", "+20% attack speed, +15% casting speed", (CombatStatType.AttackSpeed, 20f), (CombatStatType.CastingSpeed, 15f)),
                Make("Insight", "+5 Intelligence, +10% critical chance", (CombatStatType.Intelligence, 5f), (CombatStatType.CriticalChance, 10f)),
            };
        }
    }
}
