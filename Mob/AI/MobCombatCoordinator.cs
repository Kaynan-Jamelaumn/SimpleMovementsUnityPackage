using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordinates mobs fighting the same target: only a few may attack in melee (or cast ranged abilities) at once
/// while the others circle and wait, and each mob gets its own angle around the target so a group surrounds the
/// target instead of stacking behind each other. Limits come from <see cref="CombatSettings"/>.
/// </summary>
public static class MobCombatCoordinator
{
    private sealed class Engagement
    {
        public readonly List<MobMovementContext> engaged = new List<MobMovementContext>(8);
        public readonly List<MobMovementContext> melee = new List<MobMovementContext>(4);
        public readonly List<MobMovementContext> ranged = new List<MobMovementContext>(4);
        public readonly Dictionary<MobMovementContext, float> tokenTime = new Dictionary<MobMovementContext, float>(8);
        public readonly Dictionary<MobMovementContext, float> slotAngle = new Dictionary<MobMovementContext, float>(8);
        public float nextSlotUpdate;
    }

    private static readonly Dictionary<CombatEntity, Engagement> byTarget = new Dictionary<CombatEntity, Engagement>();
    private static readonly Dictionary<MobMovementContext, CombatEntity> engagedWith = new Dictionary<MobMovementContext, CombatEntity>();
    private static readonly List<MobMovementContext> sortBuffer = new List<MobMovementContext>(16);
    private static readonly List<float> angleBuffer = new List<float>(16);
    private const float TokenTimeout = 6f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        byTarget.Clear();
        engagedWith.Clear();
    }

    private static Engagement Get(CombatEntity target, bool create)
    {
        if (target == null)
            return null;
        if (!byTarget.TryGetValue(target, out Engagement e) && create)
        {
            e = new Engagement();
            byTarget[target] = e;
        }
        return e;
    }

    /// <summary>Registers <paramref name="mob"/> as fighting <paramref name="target"/> (leaves its previous fight).</summary>
    public static void Engage(MobMovementContext mob, CombatEntity target)
    {
        if (mob == null || target == null)
            return;
        if (engagedWith.TryGetValue(mob, out CombatEntity current) && current == target)
            return;
        Disengage(mob);
        Engagement e = Get(target, true);
        e.engaged.Add(mob);
        e.nextSlotUpdate = 0f;
        engagedWith[mob] = target;
    }

    /// <summary>Removes <paramref name="mob"/> from its fight and gives back its tokens.</summary>
    public static void Disengage(MobMovementContext mob)
    {
        if (mob == null || !engagedWith.TryGetValue(mob, out CombatEntity target))
            return;
        engagedWith.Remove(mob);
        if (!byTarget.TryGetValue(target, out Engagement e))
            return;
        e.engaged.Remove(mob);
        e.melee.Remove(mob);
        e.ranged.Remove(mob);
        e.tokenTime.Remove(mob);
        e.slotAngle.Remove(mob);
        e.nextSlotUpdate = 0f;
        if (e.engaged.Count == 0)
            byTarget.Remove(target);
    }

    /// <summary>How many mobs are fighting <paramref name="target"/>.</summary>
    public static int EngagedCount(CombatEntity target)
    {
        Engagement e = Get(target, false);
        return e != null ? e.engaged.Count : 0;
    }

    /// <summary>
    /// Asks for permission to attack <paramref name="target"/> (melee or ranged). Returns true when granted (or
    /// already held). Tokens expire if not used.
    /// </summary>
    public static bool RequestToken(MobMovementContext mob, CombatEntity target, bool melee)
    {
        if (mob == null || target == null)
            return false;
        Engage(mob, target);
        Engagement e = Get(target, true);
        Expire(e);
        List<MobMovementContext> list = melee ? e.melee : e.ranged;
        if (list.Contains(mob))
        {
            e.tokenTime[mob] = Time.time;
            return true;
        }
        CombatSettings s = CombatSettings.Instance;
        int max = melee ? s.maxSimultaneousMeleeAttackers : s.maxSimultaneousRangedAttackers;
        if (list.Count >= max)
            return false;
        list.Add(mob);
        e.tokenTime[mob] = Time.time;
        return true;
    }

    /// <summary>Gives back the attack tokens of <paramref name="mob"/>.</summary>
    public static void ReleaseToken(MobMovementContext mob)
    {
        if (mob == null || !engagedWith.TryGetValue(mob, out CombatEntity target))
            return;
        if (!byTarget.TryGetValue(target, out Engagement e))
            return;
        e.melee.Remove(mob);
        e.ranged.Remove(mob);
        e.tokenTime.Remove(mob);
    }

    public static bool HoldsToken(MobMovementContext mob)
    {
        if (mob == null || !engagedWith.TryGetValue(mob, out CombatEntity target))
            return false;
        Engagement e = Get(target, false);
        return e != null && (e.melee.Contains(mob) || e.ranged.Contains(mob));
    }

    /// <summary>True if the melee slots on <paramref name="target"/> are all taken by other mobs.</summary>
    public static bool MeleeSlotsFull(MobMovementContext mob, CombatEntity target)
    {
        Engagement e = Get(target, false);
        if (e == null)
            return false;
        Expire(e);
        int others = e.melee.Count - (e.melee.Contains(mob) ? 1 : 0);
        return others >= CombatSettings.Instance.maxSimultaneousMeleeAttackers;
    }

    private static void Expire(Engagement e)
    {
        float now = Time.time;
        for (int i = e.melee.Count - 1; i >= 0; i--)
            if (IsStale(e, e.melee[i], now)) e.melee.RemoveAt(i);
        for (int i = e.ranged.Count - 1; i >= 0; i--)
            if (IsStale(e, e.ranged[i], now)) e.ranged.RemoveAt(i);
    }

    private static bool IsStale(Engagement e, MobMovementContext m, float now)
    {
        if (m == null || m.Entity == null || !m.Entity.IsAlive)
            return true;
        return e.tokenTime.TryGetValue(m, out float t) && now - t > TokenTimeout;
    }

    /// <summary>
    /// The angle (degrees, world yaw) around <paramref name="target"/> this mob should stand at. Mobs keep their
    /// order around the target so they do not cross each other; angles are spread evenly.
    /// </summary>
    public static float GetSlotAngle(MobMovementContext mob, CombatEntity target)
    {
        Engagement e = Get(target, false);
        Vector3 tp = target.Position;
        if (e == null || e.engaged.Count <= 1 || !e.engaged.Contains(mob))
            return AngleOf(mob, tp);

        if (Time.time >= e.nextSlotUpdate)
        {
            e.nextSlotUpdate = Time.time + 1f;
            sortBuffer.Clear();
            angleBuffer.Clear();
            for (int i = e.engaged.Count - 1; i >= 0; i--)
            {
                MobMovementContext m = e.engaged[i];
                if (m == null || m.Entity == null || !m.Entity.IsAlive)
                {
                    e.engaged.RemoveAt(i);
                    continue;
                }
                sortBuffer.Add(m);
                angleBuffer.Add(AngleOf(m, tp));
            }
            // Sort by current angle (insertion sort, small lists).
            for (int i = 1; i < sortBuffer.Count; i++)
            {
                MobMovementContext m = sortBuffer[i];
                float a = angleBuffer[i];
                int j = i - 1;
                while (j >= 0 && angleBuffer[j] > a)
                {
                    sortBuffer[j + 1] = sortBuffer[j];
                    angleBuffer[j + 1] = angleBuffer[j];
                    j--;
                }
                sortBuffer[j + 1] = m;
                angleBuffer[j + 1] = a;
            }
            int n = sortBuffer.Count;
            float step = 360f / Mathf.Max(1, n);
            // Rotate the even spread to best match current positions (average offset).
            float offset = 0f;
            for (int i = 0; i < n; i++)
                offset += Mathf.DeltaAngle(i * step, angleBuffer[i]);
            offset /= Mathf.Max(1, n);
            for (int i = 0; i < n; i++)
                e.slotAngle[sortBuffer[i]] = i * step + offset;
            sortBuffer.Clear();
            angleBuffer.Clear();
        }
        return e.slotAngle.TryGetValue(mob, out float angle) ? angle : AngleOf(mob, tp);
    }

    private static float AngleOf(MobMovementContext m, Vector3 around)
    {
        Vector3 d = m.Transform.position - around;
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    /// <summary>Direction (flat) for a yaw angle in degrees.</summary>
    public static Vector3 Direction(float angleDeg)
    {
        float r = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
    }

    /// <summary>How many other casters are casting <paramref name="ability"/> right now (avoids stacking walls/cages).</summary>
    public static int CastingSameAbility(AbilityDefinition ability, CombatEntity except)
    {
        if (ability == null)
            return 0;
        int n = 0;
        IReadOnlyList<CombatEntity> all = CombatEntity.All;
        for (int i = 0; i < all.Count; i++)
        {
            CombatEntity e = all[i];
            if (e == null || e == except || e.Caster == null)
                continue;
            AbilityCastInstance c = e.Caster.CurrentCast;
            if (c != null && c.Definition == ability)
                n++;
        }
        return n;
    }
}
