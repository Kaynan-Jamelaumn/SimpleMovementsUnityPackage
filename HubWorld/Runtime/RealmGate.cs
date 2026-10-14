using System.Collections.Generic;
using UnityEngine;

namespace HubWorld
{
    /// <summary>
    /// A gate between the city and the world. The City Gate (in the City scene) sends players out into the world; the
    /// Return Gate (on the exterior copy of the city, in the World scene) brings them back. Walking into its trigger asks
    /// the <see cref="HubWorldDirector"/>, which decides (the gate closes shortly before a jump) and moves the player.
    /// Needs a trigger collider (one is added around its renderers when missing) and a player with a CharacterController
    /// or Rigidbody, like the world portals.
    /// </summary>
    /// <remarks>
    /// In a network session only the server's copy of a player uses a gate: the server simulates every player's movement,
    /// so the trigger fires there; a client's predicted copy walking into the trigger does nothing (the server's decision
    /// reaches the client as a realm change and a scene move).
    /// </remarks>
    [DisallowMultipleComponent]
    public class RealmGate : MonoBehaviour
    {
        public enum GateKind
        {
            /// <summary>In the City scene: out into the world.</summary>
            CityGate,
            /// <summary>In the World scene: back into the city.</summary>
            ReturnGate,
        }

        [Tooltip("City Gate: out into the world (put it in the City scene). Return Gate: back into the city (World scene).")]
        public GateKind kind = GateKind.CityGate;

        [Tooltip("Seconds after the gate appears (or a player arrives) before it reacts, so arriving next to it does not send anyone straight back.")]
        [Min(0f)] public float armDelay = 1.5f;

        [Tooltip("Seconds between two 'the gate is sealed' messages to the same player.")]
        [Min(0f)] public float messageCooldown = 3f;

        private float armedAt;
        private readonly Dictionary<CombatEntity, float> lastMessage = new Dictionary<CombatEntity, float>();
        private static readonly List<RealmGate> active = new List<RealmGate>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();

        private void Awake() => EnsureTrigger();

        private void OnEnable()
        {
            armedAt = Time.time + armDelay;
            if (!active.Contains(this))
                active.Add(this);
        }

        private void OnDisable() => active.Remove(this);

        /// <summary>
        /// Is <paramref name="player"/> at a gate of <paramref name="kind"/> in their own scene - inside its trigger or within
        /// <paramref name="tolerance"/> metres of it? The server checks this before acting on a client's gate request.
        /// </summary>
        public static bool IsNear(CombatEntity player, GateKind kind, float tolerance)
        {
            GameObject mover = player != null ? RealmTravel.Mover(player) : null;
            if (mover == null)
                return false;
            Vector3 p = mover.transform.position;
            float sqr = Mathf.Max(0f, tolerance) * Mathf.Max(0f, tolerance);
            for (int i = 0; i < active.Count; i++)
            {
                RealmGate gate = active[i];
                if (gate == null || gate.kind != kind || gate.gameObject.scene != mover.scene)
                    continue;
                foreach (Collider c in gate.GetComponentsInChildren<Collider>())
                    if (c.isTrigger && c.enabled && (c.ClosestPoint(p) - p).sqrMagnitude <= sqr)
                        return true;
            }
            return false;
        }

        private void OnTriggerEnter(Collider other) => TryUse(other);

        private void OnTriggerStay(Collider other)
        {
            // A player standing in the gate while it was sealed goes through once it opens.
            if (Time.frameCount % 15 == 0)
                TryUse(other);
        }

        private void TryUse(Collider other)
        {
            if (Time.time < armedAt)
                return;
            CombatEntity player = PlayerLocator.FromCollider(other);
            HubWorldDirector director = HubWorldDirector.Instance;
            if (player == null || director == null)
                return;
            // Only the machine that decides acts: never a client's (predicted) copy of a networked player.
            if (!GameNet.NetAuthority.RunsRules(player.gameObject) || HubWorldDirector.IsMirror)
                return;
            PlayerRealmState state = PlayerRealmState.Of(player);
            if (state == null || state.InTransit || state.AwaitingArrival || Time.time - state.RealmSince < armDelay)
                return;
            bool ok;
            string reason;
            if (kind == GateKind.CityGate)
            {
                if (!state.InCity)
                    return;
                ok = director.RequestLeaveCity(player, out reason);
            }
            else
            {
                if (!state.InWorld)
                    return;
                ok = director.RequestReturnToCity(player, out reason);
            }
            if (!ok && !string.IsNullOrEmpty(reason) && (!lastMessage.TryGetValue(player, out float t) || Time.time - t >= messageCooldown))
            {
                lastMessage[player] = Time.time;
                HubWorldMessages.Send(player, reason, HubMessageKind.Warning);
            }
        }

        private void EnsureTrigger()
        {
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
                if (c.isTrigger)
                    return;
            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    b.Encapsulate(renderers[i].bounds);
                Vector3 s = transform.lossyScale;
                box.center = transform.InverseTransformPoint(b.center);
                box.size = new Vector3(b.size.x / Mathf.Max(0.001f, Mathf.Abs(s.x)), b.size.y / Mathf.Max(0.001f, Mathf.Abs(s.y)), b.size.z / Mathf.Max(0.001f, Mathf.Abs(s.z)));
            }
            else
            {
                box.center = new Vector3(0f, 2f, 0f);
                box.size = new Vector3(5f, 4f, 1.5f);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = kind == GateKind.CityGate ? new Color(0.4f, 1f, 0.6f, 0.35f) : new Color(0.4f, 0.7f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
                Gizmos.DrawCube(box.center, box.size);
            else
                Gizmos.DrawCube(new Vector3(0f, 2f, 0f), new Vector3(5f, 4f, 1.5f));
        }
    }
}
