using UnityEngine;

/// <summary>
/// A dropped ability the player collects by walking over it (created by <see cref="AbilityAbsorption"/> or an
/// <see cref="AbilitySpawner"/>). It arms after a short delay, floats, is pulled toward nearby players, blinks before
/// vanishing and gives its <see cref="AbilityGrant"/> to the first player that accepts it. No physics setup needed.
/// </summary>
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

    /// <summary>Spawns a pickup for <paramref name="grant"/> at <paramref name="position"/>.</summary>
    public static AbilityPickup Spawn(AbilityGrant grant, Vector3 position, AbsorptionSettings settings = null)
    {
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
        return p;
    }

    /// <summary>Spawns a pickup with a custom visual and timings (used by <see cref="AbilitySpawner"/>).</summary>
    public static AbilityPickup Spawn(AbilityGrant grant, Vector3 position, GameObject visualPrefab, float armDelay, float lifetime)
    {
        if (grant == null || grant.ability == null)
            return null;
        AbsorptionSettings settings = AbsorptionSettings.Instance;
        GameObject go = visualPrefab != null ? Instantiate(visualPrefab, position, Quaternion.identity) : null;
        if (go == null)
            return Spawn(grant, position, settings);
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
        return p;
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

    private void OnEnable()
    {
        spawnTime = Time.time;
        basePosition = CombatQuery.SnapToGround(transform.position, 2f, 6f) + Vector3.up * 1f;
        transform.position = basePosition;
    }

    private void Update()
    {
        float age = Time.time - spawnTime;
        if (lifetime > 0f && age > lifetime)
        {
            Destroy(gameObject);
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

        if (age < armDelay)
        {
            transform.position = pos;
            return;
        }

        CombatEntity player = CombatEntity.NearestPlayer(transform.position, out float dist);
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
            Destroy(gameObject);
            return;
        }
        if (AbilityAbsorption.TryGrant(grant, player))
        {
            if (collectVfx != null)
                AbilityPool.PlayVfx(collectVfx, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
        else if (Time.time >= nextRefusedMessage)
        {
            nextRefusedMessage = Time.time + 3f;
            Debug.Log($"[Absorption] {player.name} cannot take '{grant.Name}' (slots full or a better copy is equipped).", this);
        }
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
