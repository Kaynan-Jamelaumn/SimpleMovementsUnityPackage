using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A dropped ability the player collects by walking over it (created by <see cref="AbilityAbsorption"/> or an
/// <see cref="AbilitySpawner"/>). It arms after a short delay, floats, is pulled toward nearby players, blinks before
/// vanishing and gives its <see cref="AbilityGrant"/> to the first player that accepts it. No physics setup needed.
/// </summary>
/// <remarks>
/// Network sessions: only the server spawns pickups and decides who collects them. When the pickup prefab has a
/// NetworkObject (and is registered as a spawnable prefab) every client sees a copy that only floats, spins and blinks
/// until the server removes it; without one the pickup exists on the server only (collectable, but invisible to clients
/// of a dedicated server). The generated default orb cannot be networked.
/// </remarks>
[DisallowMultipleComponent]
public class AbilityPickup : MonoBehaviour
{
    [Tooltip("The ability this pickup gives.")]
    public AbilityGrant grant;
    [Tooltip("Seconds before it can be collected.")]
    [Min(0f)] public float armDelay = 0.6f;
    [Tooltip("Seconds before it vanishes (0 = never).")]
    [Min(0f)] public float lifetime = 25f;
    [Tooltip("Collect distance (metres).")]
    [Min(0.2f)] public float radius = 1.4f;
    [Tooltip("Starts flying toward a player within this distance (0 = off).")]
    [Min(0f)] public float magnetRadius = 4f;
    [Tooltip("Effect played when collected.")]
    public GameObject collectVfx;

    private float spawnTime;
    private float nextRefusedMessage;
    private Vector3 basePosition;
    private Renderer[] renderers;
    private static GameObject generatedPrefab;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => generatedPrefab = null;

    /// <summary>
    /// Spawns a pickup for <paramref name="grant"/> at <paramref name="position"/>. <paramref name="sceneOf"/> (e.g. the
    /// killed character) gives the scene it belongs to; null = the active scene. Nothing on a network client (the
    /// server spawns pickups).
    /// </summary>
    public static AbilityPickup Spawn(AbilityGrant grant, Vector3 position, AbsorptionSettings settings = null, GameObject sceneOf = null)
    {
        if (GameNet.NetAuthority.IsClientOnly)
            return null;
        if (grant == null || grant.ability == null)
            return null;
        if (settings == null)
            settings = AbsorptionSettings.Instance;
        GameObject prefab = settings.pickupPrefab != null ? settings.pickupPrefab : CombatSettings.Instance.defaultAbsorbPickupPrefab;
        GameObject go;
        if (prefab != null)
            go = Instantiate(prefab, position, Quaternion.identity);
        else
            go = CreateDefaultVisual(position);
        go.SetActive(true);
        AbilityPickup p = go.GetComponent<AbilityPickup>();
        if (p == null)
            p = go.AddComponent<AbilityPickup>();
        p.grant = grant;
        p.armDelay = settings.pickupArmDelay;
        p.lifetime = settings.pickupLifetime;
        p.radius = settings.pickupRadius;
        p.magnetRadius = settings.magnetRadius;
        go.name = $"Ability Pickup ({grant.Name})";
        Publish(p, sceneOf);
        return p;
    }

    /// <summary>
    /// Spawns a pickup with a custom visual and timings (used by <see cref="AbilitySpawner"/>). See
    /// <see cref="Spawn(AbilityGrant, Vector3, AbsorptionSettings, GameObject)"/> for <paramref name="sceneOf"/>.
    /// </summary>
    public static AbilityPickup Spawn(AbilityGrant grant, Vector3 position, GameObject visualPrefab, float armDelay, float lifetime, GameObject sceneOf = null)
    {
        if (grant == null || grant.ability == null || GameNet.NetAuthority.IsClientOnly)
            return null;
        AbsorptionSettings settings = AbsorptionSettings.Instance;
        GameObject go = visualPrefab != null ? Instantiate(visualPrefab, position, Quaternion.identity) : null;
        if (go == null)
            return Spawn(grant, position, settings, sceneOf);
        go.SetActive(true);
        AbilityPickup p = go.GetComponent<AbilityPickup>();
        if (p == null)
            p = go.AddComponent<AbilityPickup>();
        p.grant = grant;
        p.armDelay = armDelay;
        p.lifetime = lifetime;
        p.radius = settings.pickupRadius;
        p.magnetRadius = settings.magnetRadius;
        go.name = $"Ability Pickup ({grant.Name})";
        Publish(p, sceneOf);
        return p;
    }

    /// <summary>
    /// Puts a configured pickup into the scene of <paramref name="sceneOf"/> (a server keeps every realm and dungeon
    /// run in its own scene) and shows it to the players (network spawn; nothing offline or without a NetworkObject).
    /// </summary>
    private static void Publish(AbilityPickup p, GameObject sceneOf)
    {
        GameObject go = p.gameObject;
        if (sceneOf != null)
        {
            UnityEngine.SceneManagement.Scene scene = sceneOf.scene;
            if (scene.IsValid() && scene.isLoaded && go.scene != scene)
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                p.Anchor(); // the ground below is found in the scene it now belongs to
            }
        }
        GameNet.NetSpawn.Spawn(go);
    }

    private static GameObject CreateDefaultVisual(Vector3 position)
    {
        if (generatedPrefab == null)
        {
            generatedPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            generatedPrefab.name = "Ability Orb";
            Destroy(generatedPrefab.GetComponent<Collider>());
            generatedPrefab.transform.localScale = Vector3.one * 0.45f;
            var mr = generatedPrefab.GetComponent<MeshRenderer>();
            Material m = CombatSettings.Instance.TelegraphMaterial;
            if (m != null)
            {
                mr.sharedMaterial = new Material(m) { color = new Color(0.55f, 0.35f, 1f, 1f), hideFlags = HideFlags.DontSave };
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(generatedPrefab.transform, false);
            light.type = LightType.Point;
            light.range = 3f;
            light.intensity = 2f;
            light.color = new Color(0.6f, 0.4f, 1f);
            generatedPrefab.SetActive(false);
            DontDestroyOnLoad(generatedPrefab);
        }
        return Instantiate(generatedPrefab, position, Quaternion.identity);
    }

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnEnable() => Anchor();

    /// <summary>Starts the pickup's life where it is: floats one metre above the ground below it.</summary>
    private void Anchor()
    {
        spawnTime = Time.time;
        basePosition = CombatQuery.SnapToGround(transform.position, 2f, 6f) + Vector3.up * 1f;
        transform.position = basePosition;
    }

    private void Update()
    {
        float age = Time.time - spawnTime;
        // Network sessions: the server decides when it vanishes and who takes it; a client's copy only animates.
        bool decides = GameNet.NetAuthority.RunsRules(gameObject);
        if (lifetime > 0f && age > lifetime)
        {
            if (decides)
                Remove();
            return;
        }

        // Float and spin.
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        Vector3 pos = basePosition + Vector3.up * (Mathf.Sin(age * 2.2f) * 0.15f);

        // Blink during the last 5 seconds.
        if (lifetime > 0f && renderers != null)
        {
            bool visible = age < lifetime - 5f || Mathf.Repeat(age * 6f, 1f) > 0.3f;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = visible;
        }

        if (age < armDelay || !decides)
        {
            transform.position = pos;
            return;
        }

        CombatEntity player = NearestPlayer(out float dist);
        if (player == null)
        {
            transform.position = pos;
            return;
        }

        if (magnetRadius > 0f && dist < magnetRadius && dist > radius * 0.5f)
        {
            Vector3 to = player.Center - basePosition;
            basePosition += to.normalized * Mathf.Min(to.magnitude, (6f + (magnetRadius - dist) * 3f) * Time.deltaTime);
            pos = basePosition;
        }
        transform.position = pos;

        if (CombatQuery.FlatDistance(player.Position, transform.position) <= radius + player.Radius)
            TryCollect(player);
    }

    private void TryCollect(CombatEntity player)
    {
        if (grant == null)
        {
            Remove();
            return;
        }
        if (AbilityAbsorption.TryGrant(grant, player))
        {
            if (collectVfx != null)
                AbilityPool.PlayVfx(collectVfx, transform.position, Quaternion.identity);
            Remove();
        }
        else if (Time.time >= nextRefusedMessage)
        {
            nextRefusedMessage = Time.time + 3f;
            Debug.Log($"[Absorption] {player.name} cannot take '{grant.Name}' (slots full or a better copy is equipped).", this);
        }
    }

    /// <summary>Removes the pickup (a network despawn on a server, so every client's copy goes too).</summary>
    private void Remove()
    {
        GameNet.NetSpawn.Destroy(gameObject);
    }

    /// <summary>
    /// The nearest living player who can take this pickup. In a network session only players in the pickup's own scene
    /// count: a server holds the City, the World and every dungeon run at overlapping coordinates.
    /// </summary>
    private CombatEntity NearestPlayer(out float distance)
    {
        if (!GameNet.NetAuthority.IsNetworked)
            return CombatEntity.NearestPlayer(transform.position, out distance);
        CombatEntity best = null;
        float bestSqr = float.MaxValue;
        Vector3 at = transform.position;
        IReadOnlyList<CombatEntity> players = CombatEntity.Players;
        for (int i = 0; i < players.Count; i++)
        {
            CombatEntity p = players[i];
            if (p == null || p.IsDead || p.gameObject.scene != gameObject.scene)
                continue;
            float d = (p.Position - at).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = p;
            }
        }
        distance = best != null ? Mathf.Sqrt(bestSqr) : float.MaxValue;
        return best;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
        if (magnetRadius > 0f)
        {
            Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, magnetRadius);
        }
    }
}
