using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Implemented by characters that can be summoned (the Mob class) to receive their summoner and target.</summary>
public interface ISummonable
{
    /// <param name="summoner">Who summoned it (it joins this character's side).</param>
    /// <param name="target">Who it should attack first (may be null).</param>
    /// <param name="lifetime">Seconds before it disappears (0 = until killed).</param>
    /// <param name="preventAbsorption">Summons usually cannot drop absorbable abilities.</param>
    void OnSummoned(CombatEntity summoner, CombatEntity target, float lifetime, bool preventAbsorption);
}

/// <summary>Spawns creatures that fight for the caster.</summary>
[Serializable, AbilityMenu("Summon/Summon", "Spawns creatures (usually mob prefabs) that join the caster's side and attack its target.", 0)]
public class SummonAction : CastAction
{
    [Tooltip("Prefab to spawn (usually a mob prefab).")]
    public GameObject prefab;
    [Tooltip("How many to spawn (the Summon Count modifier adds to this).")]
    [Min(1)] public int count = 2;
    [Tooltip("Spawn radius around the anchor (metres).")]
    [Min(0f)] public float radius = 2.5f;
    [Tooltip("Seconds before the summons disappear (multiplied by the Duration modifier). 0 = until killed.")]
    [Min(0f)] public float lifetime = 20f;
    [Tooltip("Maximum summons from this ability alive at once per caster; the oldest disappear first. 0 = no limit.")]
    [Min(0)] public int maxAlive = 4;
    [Tooltip("Summons join the caster's team (otherwise they are independent).")]
    public bool joinCasterTeam = true;
    [Tooltip("Summons start by attacking the caster's target.")]
    public bool attackCasterTarget = true;
    [Tooltip("Summons disappear when the caster dies.")]
    public bool dieWithCaster = true;
    [Tooltip("Summoned mobs never drop absorbable abilities (prevents farming summons).")]
    public bool preventAbsorption = true;

    [Header("Feedback")]
    public GameObject spawnVfx;
    public AudioClip spawnSound;

    public SummonAction()
    {
        anchor = ActionAnchor.Caster;
    }

    public override void Execute(AbilityCastInstance cast)
    {
        if (prefab == null)
            return;
        ActionFrame frame = cast.GetFrame(anchor);
        AbilityStats s = cast.Stats;
        int n = s.SummonCount(count);
        float life = lifetime > 0f ? lifetime * s.duration : 0f;
        CombatEntity summoner = cast.CasterEntity;
        float start = UnityEngine.Random.Range(0f, 360f);

        for (int i = 0; i < n; i++)
        {
            float ang = start + 360f / n * i + UnityEngine.Random.Range(-15f, 15f);
            float r = radius * UnityEngine.Random.Range(0.6f, 1f);
            Vector3 p = frame.position + Quaternion.AngleAxis(ang, Vector3.up) * (frame.Forward * r);
            if (!CombatQuery.SampleNavMesh(p, 3f, out Vector3 onMesh))
                onMesh = CombatQuery.SnapToGround(p);
            Quaternion rot = Quaternion.LookRotation(frame.Forward, Vector3.up);

            GameObject go = UnityEngine.Object.Instantiate(prefab, onMesh, rot);
            if (spawnVfx != null)
                AbilityPool.PlayVfx(spawnVfx, onMesh, rot);

            CombatEntity e = CombatEntity.Resolve(go);
            if (e == null)
                e = CombatEntity.GetOrAdd(go);
            if (joinCasterTeam && summoner != null)
                e.Summoner = summoner;

            ISummonable summonable = go.GetComponentInChildren<ISummonable>();
            summonable?.OnSummoned(joinCasterTeam ? summoner : null, attackCasterTarget ? cast.Target : null, life, preventAbsorption);

            SummonRegistry.Register(summoner, this, go, maxAlive, dieWithCaster);
            if (life > 0f)
                AbilityRuntime.Schedule(life, () => SummonRegistry.Despawn(go, spawnVfx), go);
        }
        if (spawnSound != null)
            AbilityPool.PlaySound(spawnSound, frame.position, cast.Definition.presentation.volume);
        cast.EmitNoise(frame.position, 0.6f);
    }

    public override float Reach(AbilityDefinition def, in AbilityStats s) => anchor == ActionAnchor.Caster ? radius : def.Range(s) + radius;

    public override bool WouldHit(in CastPreview p) => prefab != null;

    public override string Describe(AbilityDefinition def, in AbilityStats s)
    {
        string what = prefab != null ? prefab.name : "(no prefab)";
        string life = lifetime > 0f ? $" for {lifetime * s.duration:0.#}s" : "";
        return $"Summons {s.SummonCount(count)} {what}{life}";
    }

    public override void Validate(AbilityDefinition def, string owner, List<string> errors, List<string> warnings)
    {
        if (prefab == null)
            errors.Add($"{owner}: no Prefab to summon.");
        else if (prefab.GetComponentInChildren<ISummonable>(true) == null)
            warnings.Add($"{owner}: '{prefab.name}' has no Mob (ISummonable); it will spawn but not follow the caster or attack its target.");
        if (count > 12)
            warnings.Add($"{owner}: summoning {count} at once is expensive.");
    }

    /// <summary>Summoned objects per caster and action (limits, despawn on death).</summary>
    public static class SummonRegistry
    {
        private struct Key : IEquatable<Key>
        {
            public CombatEntity caster;
            public object action;
            public bool Equals(Key o) => ReferenceEquals(caster, o.caster) && ReferenceEquals(action, o.action);
            public override bool Equals(object obj) => obj is Key k && Equals(k);
            public override int GetHashCode() =>
                (caster is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(caster)) * 31 +
                (action != null ? action.GetHashCode() : 0);
        }

        private static readonly Dictionary<Key, List<GameObject>> alive = new Dictionary<Key, List<GameObject>>();
        private static readonly HashSet<CombatEntity> watchedCasters = new HashSet<CombatEntity>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            alive.Clear();
            watchedCasters.Clear();
        }

        public static void Register(CombatEntity caster, SummonAction action, GameObject summon, int maxAlive, bool dieWithCaster)
        {
            var key = new Key { caster = caster != null ? caster : null, action = action };
            if (!alive.TryGetValue(key, out List<GameObject> list))
            {
                list = new List<GameObject>(4);
                alive[key] = list;
            }
            list.RemoveAll(g => g == null);
            list.Add(summon);
            while (maxAlive > 0 && list.Count > maxAlive)
            {
                GameObject oldest = list[0];
                list.RemoveAt(0);
                Despawn(oldest, action.spawnVfx);
            }

            if (dieWithCaster && caster != null && watchedCasters.Add(caster))
            {
                CombatEntity c = caster;
                c.Died += _ => DespawnAllOf(c);
            }
        }

        /// <summary>Living summons of a caster (any action).</summary>
        public static int CountFor(CombatEntity caster)
        {
            if (caster == null)
                return 0;
            CombatEntity id = caster;
            int n = 0;
            foreach (KeyValuePair<Key, List<GameObject>> kv in alive)
            {
                if (!ReferenceEquals(kv.Key.caster, id))
                    continue;
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    if (kv.Value[i] != null)
                        n++;
                }
            }
            return n;
        }

        public static void DespawnAllOf(CombatEntity caster)
        {
            if (caster == null)
                return;
            CombatEntity id = caster;
            foreach (KeyValuePair<Key, List<GameObject>> kv in alive)
            {
                if (!ReferenceEquals(kv.Key.caster, id))
                    continue;
                GameObject vfx = (kv.Key.action as SummonAction)?.spawnVfx;
                for (int i = 0; i < kv.Value.Count; i++)
                    Despawn(kv.Value[i], vfx);
                kv.Value.Clear();
            }
            watchedCasters.Remove(caster);
        }

        public static void Despawn(GameObject summon, GameObject vfx)
        {
            if (summon == null)
                return;
            if (vfx != null)
                AbilityPool.PlayVfx(vfx, summon.transform.position, summon.transform.rotation);
            UnityEngine.Object.Destroy(summon);
        }
    }
}
