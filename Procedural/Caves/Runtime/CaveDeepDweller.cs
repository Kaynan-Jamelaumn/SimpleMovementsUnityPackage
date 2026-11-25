using System.Collections;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>
    /// "Deep-Dweller": what living far down does to a cave's creature (added by the cave's build to every creature below
    /// the Shallow band, where the rules run - offline or the server). Per band below Shallow it gets Endurance and
    /// Defense through the creature's <see cref="CombatStats"/> (more health, less physical damage taken - the same stats
    /// equipment and buffs use) and deals more damage of every kind. It starts at full health with its larger pool (once:
    /// later its wounds stay, a level streamed out and back in included). Removed with the creature; <see cref="Remove"/>
    /// takes it away earlier.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class CaveDeepDweller : MonoBehaviour, IDamageDealtModifier
    {
        public const string Label = "Deep-Dweller";

        private CombatEntity entity;
        private CombatStats stats;
        private object token;
        private float damageFactor = 1f;
        private bool registered;
        // Its larger health pool has not been filled yet (once: a creature's wounds stay when its level is streamed out and in).
        private bool needsFill;

        /// <summary>Bands below Shallow it was made tougher for (1 = Medium, 2 = Deep, 3 = Abyssal).</summary>
        public int Bands { get; private set; }

        /// <summary>The depth band of the place it was made for.</summary>
        public CaveDepthBand Band => (CaveDepthBand)Mathf.Clamp(Bands, 0, 3);

        /// <summary>Makes <paramref name="creature"/> tougher for <paramref name="bands"/> bands (0 = not at all). Rules side only.</summary>
        public static CaveDeepDweller Apply(GameObject creature, int bands, CaveEnemySettings s)
        {
            if (creature == null || bands <= 0 || s == null || !s.depthToughness)
                return null;
            CombatEntity e = creature.GetComponentInChildren<CombatEntity>(true);
            if (e == null)
                return null;   // a stand-in without combat: nothing to make tougher
            CaveDeepDweller d = e.GetComponent<CaveDeepDweller>();
            if (d == null)
                d = e.gameObject.AddComponent<CaveDeepDweller>();
            d.Set(e, bands, s);
            return d;
        }

        private void Set(CombatEntity e, int bands, CaveEnemySettings s)
        {
            Remove();
            entity = e;
            Bands = bands;
            damageFactor = 1f + Mathf.Max(0f, s.damagePercentPerBand) * bands / 100f;
            float endurance = Mathf.Max(0f, s.endurancePerBand) * bands;
            float defense = Mathf.Max(0f, s.defensePerBand) * bands;
            if (endurance > 0f || defense > 0f)
            {
                stats = CombatStats.For(e, true);
                if (stats != null)
                {
                    var mods = new System.Collections.Generic.List<CombatStatModifier>(2);
                    if (endurance > 0f)
                        mods.Add(new CombatStatModifier(CombatStatType.Endurance, endurance));
                    if (defense > 0f)
                        mods.Add(new CombatStatModifier(CombatStatType.Defense, defense));
                    token = stats.AddModifiers(Label, mods);
                }
            }
            if (damageFactor > 1.0001f)
            {
                e.AddDamageDealtModifier(this);
                registered = true;
            }
            needsFill = endurance > 0f;
            if (needsFill && isActiveAndEnabled)
                StartCoroutine(FillHealth());
        }

        // The stats apply their Endurance to max health on their next update: then the creature starts full - once.
        private IEnumerator FillHealth()
        {
            yield return null;
            yield return null;
            if (!needsFill || entity == null || entity.IsDead)
                yield break;
            HealthManager health = entity.GetComponentInChildren<HealthManager>(true);
            if (health == null)
                health = entity.GetComponentInParent<HealthManager>();
            if (health != null && !health.IsReplica)
                health.SetCurrentValue(health.MaxValue);
            needsFill = false;
        }

        private void OnEnable()
        {
            // Not filled yet - a dormant creature (an ambush wave) switched on now, or one switched off before its fill ran:
            // it starts full now. Later it keeps its wounds (a level streamed out and back in).
            if (needsFill && entity != null && token != null)
                StartCoroutine(FillHealth());
        }

        /// <summary>Takes the toughness away again.</summary>
        public void Remove()
        {
            if (stats != null && token != null)
                stats.RemoveModifiers(token);
            token = null;
            stats = null;
            if (registered && entity != null)
                entity.RemoveDamageDealtModifier(this);
            registered = false;
            needsFill = false;
            Bands = 0;
        }

        private void OnDestroy() => Remove();

        public float ModifyDamageDealt(in DamageInfo info, float amount) => amount * damageFactor;
    }
}
