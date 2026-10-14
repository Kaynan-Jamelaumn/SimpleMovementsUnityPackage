using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace HubWorld
{
    /// <summary>
    /// Marks a scene as a realm of the hub world - the City or the World - and holds its arrival points. Put exactly one
    /// on a root object of the City scene and one on a root object of the World scene (Tools ▸ SimpleMovements ▸ Hub
    /// World builds both scenes with it). Dungeon scenes are created at runtime and need none.
    /// </summary>
    /// <remarks>
    /// The City and the World's exterior copy of the city stand at the same place, so while this machine's player is out
    /// of the city, the City's objects are switched off locally (<see cref="hideWhileLocalPlayerAway"/>). Roots holding a
    /// camera, an event system, a screen-space canvas, a player or a <see cref="RealmKeepActive"/> stay on.
    /// <para>On a host (server and player in one process) the server still simulates the City for everyone else, so there
    /// only what this machine shows is switched off - renderers, lights, sounds, terrain drawing - and colliders, gates,
    /// NPCs and scripts keep running. Networked objects are left alone on every machine (Fish-Net shows each machine
    /// only what it observes, and a root holding one only loses its own visuals). A dedicated server never hides
    /// anything.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public class RealmScene : MonoBehaviour
    {
        [Tooltip("Which realm this scene is.")]
        public RealmKind realm = RealmKind.City;

        [Tooltip("Where players arrive when they come through a gate: City = just inside the main gate; World = in front " +
                 "of the return gate, outside the walls. Empty = this object.")]
        public Transform arrivalPoint;

        [Tooltip("City only: where new players appear (and players lost between dimensions are put). Empty = the arrival point.")]
        public Transform spawnPoint;

        [Tooltip("Players arriving together are spread on a ring of this radius around the point (metres).")]
        [Min(0f)] public float arrivalSpread = 1.5f;

        [Tooltip("City only: switch this scene's objects off on this machine while the local player is elsewhere (the " +
                 "world's exterior copy of the city stands at the same place). The city keeps existing for everyone else.")]
        public bool hideWhileLocalPlayerAway = true;

        private static readonly List<RealmScene> all = new List<RealmScene>();
        private readonly List<GameObject> hidden = new List<GameObject>();
        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        private readonly List<Behaviour> hiddenBehaviours = new List<Behaviour>();

        public static IReadOnlyList<RealmScene> All => all;

        /// <summary>Is this scene's content currently switched off locally?</summary>
        public bool IsHidden => hidden.Count > 0 || hiddenRenderers.Count > 0 || hiddenBehaviours.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        private void OnEnable()
        {
            if (!all.Contains(this))
                all.Add(this);
        }

        private void OnDisable() => all.Remove(this);

        private void OnDestroy() => all.Remove(this);

        /// <summary>The first loaded realm scene of a kind, or null.</summary>
        public static RealmScene Find(RealmKind kind)
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].realm == kind)
                    return all[i];
            return null;
        }

        /// <summary>The realm scene of a Unity scene, or null.</summary>
        public static RealmScene ForScene(Scene scene)
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].gameObject.scene == scene)
                    return all[i];
            return null;
        }

        public Scene Scene => gameObject.scene;

        /// <summary>Where the <paramref name="index"/>-th arriving player goes (the first on the point, the others around it).</summary>
        public Pose ArrivalPose(int index = 0) => PoseAround(arrivalPoint != null ? arrivalPoint : transform, index);

        /// <summary>Where a new (or lost) player appears.</summary>
        public Pose SpawnPose(int index = 0) => PoseAround(spawnPoint != null ? spawnPoint : arrivalPoint != null ? arrivalPoint : transform, index);

        private Pose PoseAround(Transform point, int index)
        {
            Vector3 p = point.position;
            Quaternion r = Quaternion.Euler(0f, point.eulerAngles.y, 0f);
            if (index > 0 && arrivalSpread > 0f)
            {
                float angle = index * 137.5f; // golden angle: a few players never stack
                float radius = arrivalSpread * Mathf.Sqrt(index);
                p += Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
            }
            return new Pose(p, r);
        }

        /// <summary>Switches this scene's objects off (or back on) on this machine only. See the class remarks.</summary>
        public void SetHidden(bool hide)
        {
            if (!hide)
            {
                foreach (GameObject go in hidden)
                    if (go != null)
                        go.SetActive(true);
                hidden.Clear();
                foreach (Renderer r in hiddenRenderers)
                    if (r != null)
                        r.enabled = true;
                hiddenRenderers.Clear();
                foreach (Behaviour b in hiddenBehaviours)
                    if (b != null)
                        b.enabled = true;
                hiddenBehaviours.Clear();
                return;
            }
            if (!hideWhileLocalPlayerAway || IsHidden || !gameObject.scene.isLoaded)
                return;
            if (!GameNet.NetAuthority.HasLocalPlayer)
                return; // a dedicated server shows nothing and must keep every realm whole
            bool networked = GameNet.NetAuthority.IsNetworked;
            // A host keeps simulating this scene for the other players: only switch off what this machine shows.
            bool visualOnly = networked && GameNet.NetAuthority.IsServer;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (root == gameObject || !root.activeSelf || MustStayActive(root))
                    continue;
                if (networked && GameNet.NetAuthority.IsNetworkedObject(root))
                    continue; // Fish-Net shows each machine only the networked objects it observes
                // Networked objects deeper in a root are Fish-Net's too: such a root only loses its own visuals.
                if (visualOnly || (networked && HoldsNetworkedObject(root)))
                {
                    HideVisuals(root, networked);
                    continue;
                }
                root.SetActive(false);
                hidden.Add(root);
            }
        }

        /// <summary>
        /// Switches off what a root shows (renderers, lights, sounds, world-space canvases, terrain), nothing else; with
        /// <paramref name="skipNetworked"/> the parts of networked objects are left to Fish-Net.
        /// </summary>
        private void HideVisuals(GameObject root, bool skipNetworked)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || (skipNetworked && GameNet.NetAuthority.IsNetworkedObject(r.gameObject)))
                    continue;
                r.enabled = false;
                hiddenRenderers.Add(r);
            }
            foreach (Behaviour b in root.GetComponentsInChildren<Behaviour>())
            {
                if (!b.enabled || !(b is Light || b is AudioSource || b is Canvas || b is Terrain))
                    continue;
                if (skipNetworked && GameNet.NetAuthority.IsNetworkedObject(b.gameObject))
                    continue;
                b.enabled = false;
                hiddenBehaviours.Add(b);
            }
        }

        /// <summary>Does anything under <paramref name="root"/> belong to a spawned network object?</summary>
        private static bool HoldsNetworkedObject(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (GameNet.NetAuthority.IsNetworkedObject(t.gameObject))
                    return true;
            return false;
        }

        private static bool MustStayActive(GameObject root)
        {
            if (root.GetComponentInChildren<RealmKeepActive>(true) != null || root.GetComponentInChildren<Camera>(true) != null ||
                root.GetComponentInChildren<EventSystem>(true) != null || root.GetComponentInChildren<HubWorldDirector>(true) != null)
                return true;
            foreach (Canvas c in root.GetComponentsInChildren<Canvas>(true))
                if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace)
                    return true;
            foreach (CombatEntity e in root.GetComponentsInChildren<CombatEntity>(true))
                if (e.Kind == CombatEntity.EntityKind.Player)
                    return true;
            return root.GetComponentInChildren<PlayerStatusController>(true) != null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = realm == RealmKind.City ? new Color(0.4f, 0.8f, 1f) : new Color(0.5f, 1f, 0.5f);
            DrawPoint(arrivalPoint != null ? arrivalPoint : transform);
            if (spawnPoint != null)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.4f);
                DrawPoint(spawnPoint);
            }
        }

        private void DrawPoint(Transform t)
        {
            Gizmos.DrawWireSphere(t.position + Vector3.up * 0.1f, Mathf.Max(0.5f, arrivalSpread));
            Gizmos.DrawLine(t.position + Vector3.up, t.position + Vector3.up + t.forward * 2f);
        }
    }
}
