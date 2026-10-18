using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drains (or restores) the sanity of players near it every second: a horror's presence, a haunted crypt, darkness - or
/// a campfire, a shrine, a bard's song (positive). Stronger close by (falloff). Put it on a mob (it stops when the mob
/// dies), a trigger area or any object. Several auras add up; each one is a source of the player's
/// <see cref="SanityManager"/> (reduced by Sanity Resistance).
/// </summary>
[AddComponentMenu("SimpleMovements/Sanity Aura")]
public class SanityAura : MonoBehaviour
{
    [Tooltip("Name shown in the Sanity Manager's sources (\"Wraith\").")]
    [SerializeField] private string label = "";
    [Tooltip("Sanity per second at the centre: negative drains (a horror), positive restores (a campfire).")]
    [SerializeField] private float perSecond = -0.5f;
    [Tooltip("Radius of the aura (metres).")]
    [SerializeField, Min(0.1f)] private float radius = 8f;
    [Tooltip("At the edge of the radius the aura is this strong (1 = the same everywhere, 0 = fades to nothing).")]
    [SerializeField, Range(0f, 1f)] private float strengthAtEdge = 0.25f;
    [Tooltip("Only when the player can see it (nothing between them on the obstacle layers).")]
    [SerializeField] private bool needsLineOfSight = false;
    [Tooltip("Layers that block line of sight.")]
    [SerializeField] private LayerMask obstacleLayers = 1;
    [Tooltip("On a mob: the aura stops while it is dead or not in combat (off: always on).")]
    [SerializeField] private bool onlyWhileOwnerAlive = true;
    [Tooltip("Seconds between checks.")]
    [SerializeField, Min(0.1f)] private float checkInterval = 0.5f;

    private readonly HashSet<SanityManager> affected = new HashSet<SanityManager>();
    private readonly List<SanityManager> leaving = new List<SanityManager>();
    private CombatEntity owner;
    private float next;

    public float PerSecond { get => perSecond; set => perSecond = value; }
    public float Radius { get => radius; set => radius = Mathf.Max(0.1f, value); }

    private void Awake()
    {
        owner = GetComponentInParent<CombatEntity>();
    }

    private void OnDisable()
    {
        foreach (SanityManager s in affected) if (s != null) s.ClearSource(this);
        affected.Clear();
    }

    private void Update()
    {
        if (Time.time < next) return;
        next = Time.time + checkInterval;
        bool active = !(onlyWhileOwnerAlive && owner != null && (owner.IsDead || !owner.isActiveAndEnabled)) && perSecond != 0f;

        leaving.Clear();
        leaving.AddRange(affected);
        if (active)
        {
            Vector3 c = transform.position;
            IReadOnlyList<CombatEntity> players = CombatEntity.Players;
            for (int i = 0; i < players.Count; i++)
            {
                CombatEntity p = players[i];
                if (p == null || p == owner || p.IsDead || !GameNet.NetAuthority.RunsRules(p.gameObject)) continue;
                float d = Vector3.Distance(c, p.Center);
                if (d > radius) continue;
                if (needsLineOfSight && Physics.Linecast(c + Vector3.up * 0.5f, p.Center, obstacleLayers, QueryTriggerInteraction.Ignore)) continue;
                SanityManager s = p.GetComponentInChildren<SanityManager>();
                if (s == null) continue;
                float k = Mathf.Lerp(1f, strengthAtEdge, d / radius);
                s.SetSource(this, perSecond * k, string.IsNullOrEmpty(label) ? name : label);
                affected.Add(s);
                leaving.Remove(s);
            }
        }
        foreach (SanityManager s in leaving)
        {
            if (s != null) s.ClearSource(this);
            affected.Remove(s);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = perSecond < 0f ? new Color(0.6f, 0.2f, 0.8f, 0.5f) : new Color(0.3f, 0.9f, 0.6f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
