using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>
    /// The trigger of a cave's passage: a cave mouth in the world (it sends players through its <see cref="Portal"/>), the
    /// way out of a cave or the way deeper (through the cave's <see cref="DungeonPortal"/>). Walking in, this machine's
    /// player sees the screen darken with the cave's name (<see cref="CaveTransition"/>); a moment later - still inside -
    /// the server (offline: this machine) sends them, the same way the portal's own trigger would (groups, party members,
    /// shared runs and every rule of the portal apply). Walking back out before that cancels it. A refused entry (a full
    /// cave, the re-entry cooldown) brings the light back at once.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class CavePassage : MonoBehaviour
    {
        private sealed class Visitor
        {
            public GameObject Root;
            public CombatEntity Entity;
            public int Colliders;
            public float Since;
            public bool Sent, Refused, Local;
        }

        [Tooltip("Seconds a player stays in the passage before being sent (the screen darkens meanwhile).")]
        [Range(0.1f, 3f)] public float delay = 0.6f;
        [Tooltip("Seconds after appearing before the passage reacts (so arriving next to it does not send anyone back).")]
        [Min(0f)] public float armDelay = 1.5f;
        [Tooltip("Shown while the screen is dark.")]
        public string title = "";
        [Tooltip("The second line.")]
        public string subtitle = "";
        [Tooltip("OPTIONAL. Played for this machine's player on the way in.")]
        public AudioClip sound;

        private Portal worldPortal;
        private DungeonPortal dungeonPortal;
        private Collider trigger;
        private float armedAt;
        private readonly Dictionary<GameObject, Visitor> inside = new Dictionary<GameObject, Visitor>();
        private readonly List<GameObject> scratch = new List<GameObject>();

        /// <summary>A player went through (the player's root): statistics, quests.</summary>
        public static event System.Action<CavePassage, GameObject> Used;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Used = null;

        /// <summary>The world portal it sends players through (a cave mouth), or null.</summary>
        public Portal WorldPortal => worldPortal;
        /// <summary>The dungeon portal it sends players through (inside a cave), or null.</summary>
        public DungeonPortal DungeonPortal => dungeonPortal;

        /// <summary>A cave mouth in the world: players go through <paramref name="portal"/>.</summary>
        public void LeadTo(Portal portal)
        {
            worldPortal = portal;
            dungeonPortal = null;
        }

        /// <summary>A passage inside a cave: players go through <paramref name="portal"/> (out, or deeper).</summary>
        public void LeadTo(DungeonPortal portal)
        {
            dungeonPortal = portal;
            worldPortal = null;
        }

        private void Awake()
        {
            trigger = GetComponent<Collider>();
            if (trigger == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(2.4f, 3f, 1.2f);
                box.center = new Vector3(0f, 1.5f, 0f);
                trigger = box;
            }
            trigger.isTrigger = true;
        }

        private void OnEnable()
        {
            armedAt = Time.time + armDelay;
            inside.Clear();
        }

        private void OnDisable()
        {
            // A passage that goes away while this machine's player waits in it must not leave the screen dark - unless the
            // player is on the way already (a party member went through first, taking the group deeper: the cave goes, the
            // next one is being made, and the transition lights up when they arrive).
            foreach (Visitor v in inside.Values)
            {
                if (!v.Local || v.Sent)
                    continue;
                DungeonRun run = v.Root != null && GameNet.NetAuthority.IsServer ? DungeonSession.RunOf(v.Root) : null;
                if (run == null || !run.IsEntering)
                    CaveTransition.FadeIn(0.6f);
            }
            inside.Clear();
        }

        private static bool IsLocal(CombatEntity e)
        {
            CombatEntity local = PlayerLocator.Local;
            if (local != null)
                return e != null && e == local;
            return !GameNet.NetAuthority.IsNetworked;   // offline without a located player: the one player there is
        }

        private bool Leads => worldPortal != null || dungeonPortal != null;

        /// <summary>
        /// The object that moves the player behind <paramref name="other"/>: its Combat Entity's, else - a player object
        /// without a status controller - the tag fallback of the portal the passage leads to (as that portal's own trigger
        /// would have recognised them). Null for anything else.
        /// </summary>
        private GameObject RootOf(Collider other, CombatEntity entity)
        {
            if (entity != null)
                return PlayerLocator.MovableRoot(entity);
            if (worldPortal != null)
                return worldPortal.PlayerFrom(other);
            if (dungeonPortal != null)
                return dungeonPortal.PlayerFrom(other);
            return null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time < armedAt || !Leads || other == null)
                return;
            CombatEntity entity = PlayerLocator.FromCollider(other);
            GameObject root = RootOf(other, entity);
            if (root == null)
                return;
            if (entity == null)
                entity = PlayerLocator.FromObject(root);
            if (inside.TryGetValue(root, out Visitor v))
            {
                v.Colliders++;
                return;
            }
            // Inside a cave only its players use its passages (as its portals do).
            if (dungeonPortal != null && GameNet.NetAuthority.IsServer && !DungeonNet.IsReplica(dungeonPortal))
            {
                DungeonRun run = DungeonSession.RunAt(dungeonPortal) ?? DungeonSession.RunOf(root);
                if (run == null || !run.IsInside || !run.IsParticipant(root))
                    return;
            }
            v = new Visitor { Root = root, Entity = entity, Colliders = 1, Since = Time.time, Local = IsLocal(entity) };
            inside[root] = v;
            if (v.Local)
            {
                CaveTransition.FadeOut(title, subtitle, delay);
                if (sound != null)
                    AudioSource.PlayClipAtPoint(sound, transform.position, 0.8f);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null)
                return;
            GameObject root = RootOf(other, PlayerLocator.FromCollider(other));
            if (root == null || !inside.TryGetValue(root, out Visitor v))
                return;
            if (--v.Colliders > 0)
                return;
            Leave(v);
        }

        private void Leave(Visitor v)
        {
            inside.Remove(v.Root);
            if (!v.Local)
                return;
            if (!v.Sent)
            {
                // Walked back out before being sent: the light comes back.
                CaveTransition.FadeIn(0.5f);
                return;
            }
            // A network client handed its player over, and the player walked back out (not moved by the server).
            if (!GameNet.NetAuthority.IsServer && v.Root != null && trigger != null &&
                (trigger.ClosestPoint(v.Root.transform.position) - v.Root.transform.position).sqrMagnitude < 12f * 12f)
                CaveTransition.HandBack();
        }

        /// <summary>Is the player still standing in the passage (colliders that left without telling: a teleport, a disabled controller)?</summary>
        private bool StillInside(Visitor v)
        {
            if (v.Root == null || !v.Root.activeInHierarchy || trigger == null)
                return false;
            Vector3 at = v.Root.transform.position;
            Vector3 closest = trigger.ClosestPoint(at);
            return (closest - at).sqrMagnitude < 1.5f * 1.5f;
        }

        private void Update()
        {
            if (inside.Count == 0)
                return;
            scratch.Clear();
            scratch.AddRange(inside.Keys);
            float now = Time.time;
            foreach (GameObject key in scratch)
            {
                if (!inside.TryGetValue(key, out Visitor v))
                    continue;
                if (!StillInside(v))
                {
                    // Gone: sent elsewhere (in the cave's hands now), or walked off.
                    Leave(v);
                    continue;
                }
                if (v.Sent || v.Refused || now - v.Since < delay)
                    continue;
                // The rules side sends (offline: this machine). A network client only darkened its own screen: it hands its
                // player over - the server decides, and the transition lights up when the player arrives (or was refused).
                if (!GameNet.NetAuthority.IsServer)
                {
                    v.Sent = true;
                    if (v.Local)
                        CaveTransition.HandOver();
                    continue;
                }
                // From here the cave has the player: sending may switch this passage off at once (a classic-mode world pause
                // hiding the terrain root), and OnDisable must not light the screen then.
                v.Sent = true;
                bool sent = false;
                try
                {
                    sent = Send(v.Root);
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e, this);
                }
                // Taken in already: with the group of a party member who went through first, or into the shared visit going
                // on. The run has them now (the transition lights up when they arrive).
                if (!sent && v.Root != null)
                {
                    DungeonRun taken = DungeonSession.RunOf(v.Root);
                    sent = taken != null && taken.IsEntering;
                }
                if (sent)
                {
                    try
                    {
                        Used?.Invoke(this, v.Root);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e, this);
                    }
                }
                else
                {
                    // Refused (full, closed, just came back...): no second try until the player steps out and in again.
                    v.Sent = false;
                    v.Refused = true;
                    if (v.Local)
                        CaveTransition.FadeIn(0.5f);
                }
            }
        }

        private bool Send(GameObject player)
        {
            if (player == null)
                return false;
            if (worldPortal != null)
                return worldPortal.isActiveAndEnabled && worldPortal.TryEnter(player);
            if (dungeonPortal == null)
                return false;
            DungeonRun run = DungeonSession.RunAt(dungeonPortal) ?? DungeonSession.RunOf(player);
            if (run == null || !run.IsInside || !run.IsParticipant(player))
                return false;
            DungeonSession.UsePortal(dungeonPortal, player);
            // Leaving ends the run, going deeper starts the next cave: either way the run is no longer standing here.
            return run.State != DungeonRunState.Inside;
        }
    }
}
