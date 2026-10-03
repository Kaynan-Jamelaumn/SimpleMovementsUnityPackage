using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A physical projectile (arrow, bolt, bullet, thrown knife, grenade) flying with gravity, sweeping its path each frame
/// so fast shots never pass through thin targets. Characters are found through their colliders (body-part hitboxes
/// included, for exact hit locations); who it may hit and what a hit does are decided by its launcher (a weapon's attack,
/// a script, an AI). It can pierce, lose damage with distance, stick into what it hits, be picked up again (ammo, the
/// thrown weapon), and explode on impact or when its fuse runs out.
/// </summary>
public class WeaponProjectile : MonoBehaviour
{
    /// <summary>Everything a launch needs. The callbacks decide relations and effects.</summary>
    public struct LaunchData
    {
        public GameObject prefab;
        public Vector3 position;
        public Vector3 velocity;
        public ProjectileSettings settings;
        public CombatEntity shooter;
        public GameObject shooterObject;
        /// <summary>Item a stuck projectile turns into when recovered (ammo, the thrown weapon), or null.</summary>
        public ItemSO recoverItem;
        public IList<int> recoverDurability;
        /// <summary>May it hit this character (relations, filters)? Null = everyone but the shooter.</summary>
        public Func<CombatEntity, bool> canHit;
        /// <summary>A character was hit: (target, point, flight direction, collider struck, damage fraction).</summary>
        public Action<CombatEntity, Vector3, Vector3, Collider, float> onHitCharacter;
        /// <summary>Something that is not a character was hit (a target dummy, a switch): (collider, point, direction).</summary>
        public Action<Collider, Vector3, Vector3> onHitObject;
    }

    private static readonly RaycastHit[] hits = new RaycastHit[16];
    private static readonly Collider[] overlaps = new Collider[32];
    private static readonly List<CombatEntity> blastBuffer = new List<CombatEntity>(16);
    private static GameObject fallbackPrefab;

    private LaunchData data;
    private ProjectileSettings ps;
    private Vector3 velocity;
    private float travelled;
    private float age;
    private int pierceLeft;
    private float strength = 1f;
    private bool flying;
    private bool exploded;
    private GameObject visual;
    private readonly HashSet<CombatEntity> alreadyHit = new HashSet<CombatEntity>(ReferenceComparer<CombatEntity>.Instance);

    public bool IsFlying => flying;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => fallbackPrefab = null;

    /// <summary>Launches a projectile. Returns it (null when there is nothing to launch).</summary>
    public static WeaponProjectile Launch(LaunchData d)
    {
        var go = new GameObject("Projectile");
        go.transform.position = d.position;
        go.transform.rotation = Quaternion.LookRotation(d.velocity.sqrMagnitude > 1e-6f ? d.velocity : Vector3.forward);
        var p = go.AddComponent<WeaponProjectile>();
        p.Begin(d);
        return p;
    }

    private void Begin(LaunchData d)
    {
        data = d;
        ps = d.settings ?? new ProjectileSettings();
        velocity = d.velocity;
        pierceLeft = Mathf.Max(0, ps.pierce);
        flying = true;
        if (d.shooter != null)
            alreadyHit.Add(d.shooter); // never hits its own shooter

        GameObject prefab = d.prefab != null ? d.prefab : Fallback();
        if (prefab != null)
        {
            visual = Instantiate(prefab, transform.position, transform.rotation, transform);
            visual.name = prefab.name;
            if (!visual.activeSelf)
                visual.SetActive(true);
            // A model used as a projectile must not collide with anything itself, nor be picked up mid-flight.
            foreach (Collider c in visual.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
            foreach (Rigidbody rb in visual.GetComponentsInChildren<Rigidbody>(true))
                rb.isKinematic = true;
            foreach (ItemPickable ip in visual.GetComponentsInChildren<ItemPickable>(true))
                Destroy(ip);
        }
        // Point-blank: something already overlapping the muzzle.
        CheckOverlapAtStart();
    }

    private static GameObject Fallback()
    {
        GameObject p = ProjectileInstance.ResolvePrefab(null);
        if (p != null)
            return p;
        if (fallbackPrefab == null)
        {
            fallbackPrefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            fallbackPrefab.name = "Default Weapon Projectile";
            Destroy(fallbackPrefab.GetComponent<Collider>());
            fallbackPrefab.transform.localScale = new Vector3(0.04f, 0.3f, 0.04f);
            fallbackPrefab.SetActive(false);
        }
        return fallbackPrefab;
    }

    private void CheckOverlapAtStart()
    {
        CombatSettings settings = CombatSettings.Instance;
        int n = Physics.OverlapSphereNonAlloc(transform.position, ps.radius, overlaps, settings.characterLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n && flying; i++)
        {
            Collider c = overlaps[i];
            if (c == null || IsShooterCollider(c))
                continue;
            CombatEntity e = CombatEntity.Resolve(c);
            if (e != null)
                HitCharacter(e, transform.position, c);
        }
    }

    private void Update()
    {
        if (!flying)
            return;
        float dt = Time.deltaTime;
        age += dt;
        if (ps.fuseTime > 0f && age >= ps.fuseTime)
        {
            Explode(transform.position);
            Stop(false, null, Vector3.zero, Vector3.zero);
            return;
        }

        velocity += Vector3.down * (ps.gravity * dt);
        Vector3 step = velocity * dt;
        float len = step.magnitude;
        if (len < 1e-6f)
            return;
        Vector3 dir = step / len;
        Vector3 from = transform.position;

        CombatSettings settings = CombatSettings.Instance;
        int mask = settings.characterLayers | settings.obstacleLayers | settings.groundLayers;
        Vector3 castFrom = from - dir * ps.radius;
        int n = Physics.SphereCastNonAlloc(castFrom, ps.radius, dir, hits, len + ps.radius, mask, QueryTriggerInteraction.Collide);
        SortHits(n);
        for (int i = 0; i < n && flying; i++)
        {
            RaycastHit h = hits[i];
            Collider col = h.collider;
            if (col == null || IsShooterCollider(col))
                continue;
            Vector3 point = h.distance <= 0f && h.point == Vector3.zero ? col.ClosestPoint(from) : h.point;
            CombatEntity e = CombatEntity.Resolve(col);
            if (e != null)
            {
                HitCharacter(e, point, col);
                continue;
            }
            if (col.isTrigger)
                continue; // triggers that are not characters (zones, pickups) do not stop it
            data.onHitObject?.Invoke(col, point, dir);
            if (ps.fuseTime > 0f)
            {
                // Grenades bounce until the fuse runs out.
                Vector3 normal = h.normal.sqrMagnitude > 1e-6f ? h.normal : -dir;
                velocity = Vector3.Reflect(velocity, normal) * 0.45f;
                transform.position = point + normal * (ps.radius + 0.01f);
                return;
            }
            Explode(point);
            Stop(true, col, point, h.normal);
            return;
        }
        if (!flying)
            return;

        transform.position = from + step;
        travelled += len;
        if (ps.alignToVelocity && velocity.sqrMagnitude > 1e-4f)
            transform.rotation = Quaternion.LookRotation(velocity);
        else if (Mathf.Abs(ps.spin) > 0.01f && visual != null)
            visual.transform.Rotate(Vector3.right, ps.spin * dt, Space.Self);

        if (travelled >= ps.maxDistance)
        {
            if (ps.explosionRadius > 0f)
                Explode(transform.position);
            Stop(false, null, Vector3.zero, Vector3.zero);
        }
    }

    private bool IsShooterCollider(Collider c)
    {
        if (data.shooterObject == null)
            return false;
        Transform t = c.transform;
        return t == data.shooterObject.transform || t.IsChildOf(data.shooterObject.transform);
    }

    private void HitCharacter(CombatEntity e, Vector3 point, Collider col)
    {
        if (e == null || !e.IsAlive || alreadyHit.Contains(e))
            return;
        if (data.canHit != null ? !data.canHit(e) : e == data.shooter)
            return; // passes through (allies, the shooter's party...)
        alreadyHit.Add(e);
        float distanceFactor = ps.maxDistance > 0f ? Mathf.Lerp(1f, ps.damageAtMaxDistance, Mathf.Clamp01(travelled / ps.maxDistance)) : 1f;
        float fraction = strength * distanceFactor;
        Vector3 dir = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : transform.forward;
        try
        {
            data.onHitCharacter?.Invoke(e, point, dir, col, fraction);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
        if (pierceLeft > 0)
        {
            pierceLeft--;
            strength *= ps.pierceFalloff;
            return;
        }
        Explode(point);
        Stop(true, col, point, -dir, e);
    }

    /// <summary>The blast of an explosive projectile: everyone the launcher may hit inside the radius (half strength at the edge).</summary>
    private void Explode(Vector3 point)
    {
        if (exploded || ps.explosionRadius <= 0f)
            return;
        exploded = true;
        var shape = new HitShape { type = HitShapeType.Sphere, radius = ps.explosionRadius };
        ResolvedShape r = ResolvedShape.Resolve(shape, point, Quaternion.identity, 1f);
        CombatQuery.Overlap(r, blastBuffer);
        for (int i = 0; i < blastBuffer.Count; i++)
        {
            CombatEntity e = blastBuffer[i];
            if (e == null || !e.IsAlive)
                continue;
            if (data.canHit != null ? !data.canHit(e) : e == data.shooter)
                continue;
            float d = Vector3.Distance(e.Center, point);
            float falloff = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(d / ps.explosionRadius));
            Vector3 dir = e.Center - point;
            try
            {
                data.onHitCharacter?.Invoke(e, e.Center, dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up, null, ps.explosionDamage * falloff * strength);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }
        blastBuffer.Clear();
        if (data.shooter != null)
            CombatEvents.EmitNoise(point, ps.explosionRadius * 4f, data.shooter, 0.9f);
    }

    /// <summary>Ends the flight: sticks into what it hit (and may become a pickup), or vanishes.</summary>
    private void Stop(bool impact, Collider into, Vector3 point, Vector3 normal, CombatEntity character = null)
    {
        flying = false;
        if (impact)
        {
            if (ps.impactVfx != null)
                AbilityPool.PlayVfx(ps.impactVfx, point, Quaternion.LookRotation(normal.sqrMagnitude > 1e-6f ? normal : Vector3.up), 0f);
            if (ps.impactSound != null)
                AbilityPool.PlaySound(ps.impactSound, point, 1f);
        }
        bool canStick = impact && ps.stickInSurfaces && visual != null && !exploded;
        if (!canStick)
        {
            Destroy(gameObject);
            return;
        }

        // Stuck: a little way into the surface, following what it hit (a character's bone, a moving door).
        transform.position = point + (velocity.sqrMagnitude > 1e-6f ? velocity.normalized * 0.05f : Vector3.zero);
        if (into != null)
            transform.SetParent(into.transform, true);
        bool recoverable = character == null && data.recoverItem != null && UnityEngine.Random.value < ps.recoverChance;
        if (recoverable)
            MakePickable();
        if (ps.stuckLifetime > 0f && !recoverable)
            Destroy(gameObject, ps.stuckLifetime);
        else if (recoverable)
            Destroy(gameObject, Mathf.Max(ps.stuckLifetime, 60f));
    }

    /// <summary>The stuck projectile can be picked up: one unit of its ammo item (or the thrown weapon).</summary>
    private void MakePickable()
    {
        ItemPickable pick = gameObject.AddComponent<ItemPickable>();
        pick.itemScriptableObject = data.recoverItem;
        pick.quantity = 1;
        pick.DurabilityList = data.recoverDurability != null ? new List<int>(data.recoverDurability) : new List<int>();
        pick.InteractionTime = data.recoverItem.PickUpTime;
        pick.spinInWorld = false;
        var box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true; // the interaction ray finds it; nothing bumps into it
        Bounds b = default;
        bool found = false;
        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            if (rend is ParticleSystemRenderer || rend is TrailRenderer) continue;
            if (!found) { b = rend.bounds; found = true; }
            else b.Encapsulate(rend.bounds);
        }
        if (found)
        {
            box.center = transform.InverseTransformPoint(b.center);
            Vector3 s = transform.lossyScale;
            box.size = new Vector3(
                Mathf.Max(0.15f, b.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x))),
                Mathf.Max(0.15f, b.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y))),
                Mathf.Max(0.15f, b.size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z))));
        }
        else
        {
            box.size = Vector3.one * 0.3f;
        }
    }

    private static void SortHits(int n)
    {
        for (int i = 1; i < n; i++)
        {
            RaycastHit key = hits[i];
            int j = i - 1;
            while (j >= 0 && hits[j].distance > key.distance)
            {
                hits[j + 1] = hits[j];
                j--;
            }
            hits[j + 1] = key;
        }
    }
}
