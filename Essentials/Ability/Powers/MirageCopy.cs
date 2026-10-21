using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One illusory copy made by <see cref="MiragePower"/>: a copy of the character's model (meshes, equipment, Animator;
/// no scripts, colliders or physics), standing idle or wandering around, that vanishes after its lifetime or when
/// it takes a hit. As a decoy it is a <see cref="CombatEntity"/> of the caster's team, so enemies attack it.
/// </summary>
[DisallowMultipleComponent]
public class MirageCopy : MonoBehaviour
{
    private static readonly int MovementX = Animator.StringToHash("MovementX");
    private static readonly int MovementZ = Animator.StringToHash("MovementZ");
    private static readonly int MovementSpeed = Animator.StringToHash("MovementSpeed");
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly RaycastHit[] hits = new RaycastHit[8];

    private Animator animator;
    private CombatEntity entity;
    private Collider ownCollider;
    private float vanishAt;
    private MirageMotion motion;
    private bool changesMind;
    private Vector2 speeds;
    private Vector3 direction;
    private GameObject vfx;
    private bool vanished;
    private System.Random rng;
    private float spawnTime;
    private float turnAt;

    /// <summary>The character it copies.</summary>
    public CombatEntity Original { get; private set; }

    /// <summary>Makes a copy of <paramref name="model"/> at <paramref name="position"/>. Returns null when it cannot.</summary>
    public static MirageCopy Create(CombatEntity owner, Transform ownerRoot, Transform model, Vector3 position, Quaternion rotation, float lifetime,
        MirageMotion motion, bool changesMind, Vector2 walkRunSpeed, bool vanishWhenHit, bool decoy, GameObject vfx, int seed = 0, bool visualOnly = false)
    {
        if (model == null || ownerRoot == null)
            return null;
        var root = new GameObject($"{ownerRoot.name} (Mirage)");
        root.layer = ownerRoot.gameObject.layer;
        root.transform.SetPositionAndRotation(position, rotation);

        // Copy the model under an inactive holder, so none of its scripts wake up, then strip everything but visuals.
        var holder = new GameObject("Mirage Holder");
        holder.SetActive(false);
        GameObject copy = Instantiate(model.gameObject, holder.transform);
        Strip(copy);
        copy.transform.SetParent(root.transform, false);
        // Same pose relative to the root as the original model.
        copy.transform.localPosition = ownerRoot.InverseTransformPoint(model.position);
        copy.transform.localRotation = Quaternion.Inverse(ownerRoot.rotation) * model.rotation;
        copy.transform.localScale = model.lossyScale;
        Destroy(holder);

        var c = root.AddComponent<MirageCopy>();
        c.Original = owner;
        c.animator = copy.GetComponentInChildren<Animator>(true);
        c.vanishAt = Time.time + Mathf.Max(0.2f, lifetime);
        c.motion = motion;
        c.changesMind = changesMind;
        c.speeds = walkRunSpeed;
        c.vfx = vfx;
        c.direction = rotation * Vector3.forward;
        // Seeded: every machine moves the same copy the same way.
        c.rng = new System.Random(seed != 0 ? seed : UnityEngine.Random.Range(1, int.MaxValue));
        c.spawnTime = Time.time;
        c.turnAt = c.Range(0.8f, 2.5f);
        if (visualOnly)
        {
            // A client's copy: the server's copy takes the hits.
            copy.SetActive(true);
            if (c.animator != null)
            {
                c.animator.enabled = true;
                AnimatorParameterCache.SetBool(c.animator, "IsGrounded", true);
            }
            if (vfx != null)
                Instantiate(vfx, position, rotation);
            return c;
        }

        // A body for hits: the original's size.
        float height = owner != null && owner.Height > 0.1f ? owner.Height : 1.8f;
        float radius = owner != null && owner.Radius > 0.05f ? owner.Radius : 0.35f;
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.height = height;
        capsule.radius = radius;
        capsule.center = new Vector3(0f, height * 0.5f, 0f);
        c.ownCollider = capsule;
        var body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        if (decoy || vanishWhenHit)
        {
            c.entity = root.AddComponent<CombatEntity>();
            if (owner != null)
            {
                c.entity.SetTeam(owner.Team);
                c.entity.SetFaction(owner.Faction);
                if (decoy)
                    c.entity.Summoner = owner; // the caster's side: enemies of the caster attack it
            }
            if (vanishWhenHit)
                c.entity.Damaged += c.OnHit;
        }

        copy.SetActive(true);
        if (c.animator != null)
        {
            c.animator.enabled = true;
            AnimatorParameterCache.SetBool(c.animator, "IsGrounded", true);
        }
        if (vfx != null)
            Instantiate(vfx, position, rotation);
        return c;
    }

    /// <summary>Removes every script, collider and physics part of a copied model, keeping meshes and the Animator.</summary>
    private static void Strip(GameObject go)
    {
        // Several passes: a component another one requires can only go after it.
        for (int pass = 0; pass < 4; pass++)
        {
            bool left = false;
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is Animator || c is Renderer || c is MeshFilter || c is LODGroup || c is ParticleSystem)
                    continue;
                if (c is MonoBehaviour || c is Collider || c is Rigidbody || c is Joint || c is CharacterController || c is AudioSource ||
                    c is UnityEngine.AI.NavMeshAgent || c is Cloth)
                {
                    try { DestroyImmediate(c); }
                    catch (Exception) { left = true; }
                }
            }
            if (!left)
                break;
        }
    }

    /// <summary>The ground below <paramref name="p"/> (ignoring <paramref name="ignore"/>'s own colliders).</summary>
    public static Vector3 Ground(Vector3 p, Transform ignore)
    {
        int n = Physics.RaycastNonAlloc(p + Vector3.up * 3f, Vector3.down, hits, 8f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 found = p;
        for (int i = 0; i < n; i++)
        {
            if (ignore != null && hits[i].transform.IsChildOf(ignore)) continue;
            if (hits[i].collider.GetComponentInParent<MirageCopy>() != null) continue;
            if (hits[i].distance < best)
            {
                best = hits[i].distance;
                found = hits[i].point;
            }
        }
        return found;
    }

    private float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

    private void OnHit(DamageInfo info) => Vanish();

    /// <summary>Makes the copy disappear now.</summary>
    public void Vanish()
    {
        if (vanished)
            return;
        vanished = true;
        if (vfx != null)
            Instantiate(vfx, transform.position, transform.rotation);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (entity != null)
            entity.Damaged -= OnHit;
    }

    private void Update()
    {
        if (vanished)
            return;
        if (Time.time >= vanishAt || (Original != null && !Original.IsAlive))
        {
            Vanish();
            return;
        }
        float speed = motion == MirageMotion.Walk ? speeds.x : motion == MirageMotion.Run ? speeds.y : 0f;
        // Decisions on the copy's own clock from a shared seed, so every machine's copy turns at the same moments.
        if (Time.time - spawnTime >= turnAt)
        {
            turnAt += Range(1.2f, 3f);
            if (changesMind)
                motion = (MirageMotion)rng.Next(0, 3);
            direction = Quaternion.Euler(0f, Range(-120f, 120f), 0f) * direction;
        }
        if (speed > 0f)
        {
            // Turn away from walls ahead.
            if (Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.3f, direction, out RaycastHit wall, 1f, ~0, QueryTriggerInteraction.Ignore) &&
                wall.collider != ownCollider && !wall.transform.IsChildOf(transform))
                direction = Quaternion.Euler(0f, 180f, 0f) * direction;
            direction.y = 0f;
            direction.Normalize();
            Vector3 next = transform.position + direction * speed * Time.deltaTime;
            transform.position = Ground(next, transform);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 6f);
        }
        if (animator != null)
        {
            float amount = motion == MirageMotion.Idle ? 0f : 1f;
            AnimatorParameterCache.SetFloat(animator, MovementZ, amount, 0.1f);
            AnimatorParameterCache.SetFloat(animator, MovementX, 0f, 0.1f);
            AnimatorParameterCache.SetFloat(animator, MovementSpeed, amount, 0.1f);
            AnimatorParameterCache.SetFloat(animator, Speed, speed, 0.1f);
            AnimatorParameterCache.SetBool(animator, "IsWalking", motion == MirageMotion.Walk);
            AnimatorParameterCache.SetBool(animator, "IsRunning", motion == MirageMotion.Run);
        }
    }
}
