using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// The entrance and exit portals inside a dungeon (added by the builder to the theme's portal prefabs or the
    /// primitive stand-ins). Stepping in hands the player to <see cref="DungeonSession"/>: back to the world, back to
    /// the world with the dungeon completed, or on to the next (deeper) dungeon. A trigger collider is added when the
    /// object has none.
    /// </summary>
    [DisallowMultipleComponent]
    public class DungeonPortal : MonoBehaviour
    {
        public enum PortalAction
        {
            /// <summary>Leave: the player goes back to the world portal (the dungeon isn't completed).</summary>
            ReturnToWorld,
            /// <summary>Finish: the player goes back to the world and the dungeon counts as completed.</summary>
            CompleteDungeon,
            /// <summary>Go deeper: a new, harder dungeon is generated and the player continues there.</summary>
            NextDungeon,
        }

        [Tooltip("What stepping in does. Set by the builder: the entrance portal returns to the world; the exit portal uses the DungeonManager's Exit Portal Action.")]
        public PortalAction action = PortalAction.ReturnToWorld;
        [Tooltip("Tag of the player (the collider may be on a child of the tagged object).")]
        public string playerTag = "Player";
        [Tooltip("Seconds after appearing before the portal reacts (so arriving on it doesn't send you straight back).")]
        [Min(0f)] public float armDelay = 1.5f;

        private float armedAt;

        private void Awake()
        {
            EnsureTrigger();
        }

        private void OnEnable()
        {
            armedAt = Time.time + armDelay;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time < armedAt)
                return;
            GameObject player = FindPlayer(other);
            if (player != null)
                DungeonSession.UsePortal(this, player);
        }

        /// <summary>The tagged object behind the collider (preferring the one with the CharacterController), or null.</summary>
        private GameObject FindPlayer(Collider other)
        {
            Transform tagged = null;
            for (Transform t = other.transform; t != null; t = t.parent)
            {
                bool match;
                try
                {
                    match = t.CompareTag(playerTag);
                }
                catch (UnityException)
                {
                    return null;   // tag not defined
                }
                if (match)
                    tagged = t;
            }
            if (tagged == null)
                return null;
            CharacterController cc = other.GetComponentInParent<CharacterController>();
            if (cc != null && (cc.transform == tagged || cc.transform.IsChildOf(tagged) || tagged.IsChildOf(cc.transform)))
                return cc.gameObject;
            return tagged.gameObject;
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
                box.center = new Vector3(0f, 1.5f, 0f);
                box.size = new Vector3(2f, 3f, 1.5f);
            }
        }
    }
}
