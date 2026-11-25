using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>
    /// The fade between the open world and a cave, for this machine's player: walking into a cave mouth (or a passage
    /// inside) the screen darkens and the cave's name shows while the cave is made and built; once the player stands in
    /// it the darkness lifts. The same on the way out. Created when first needed, kept across scenes. It never stays dark:
    /// a run that fails, is refused or takes too long (<see cref="MaxDarkSeconds"/>) always brings the light back.
    /// <para>Drawn with IMGUI (like the dungeon map), so it needs no canvas or UI package.</para>
    /// </summary>
    [AddComponentMenu("")]
    public sealed class CaveTransition : MonoBehaviour
    {
        /// <summary>Longest the screen stays dark (seconds), whatever happens.</summary>
        public static float MaxDarkSeconds = 25f;

        /// <summary>
        /// A network client whose player was handed to the server and was neither taken (the server locks a player's controls
        /// when it takes them into a run) nor moved after this many seconds lights up again: the server said no (a full cave,
        /// the re-entry cooldown) - it does not tell clients.
        /// </summary>
        public static float RefusedAfterSeconds = 6f;

        private static CaveTransition instance;
        private static Texture2D black;

        private float alpha, target, speed = 2f, darkSince = -1f, holdUntil;
        private string title = "", subtitle = "";
        // Where this machine's player stood when the screen went dark: once it has been moved far (into the cave, or
        // back to the world - the server moves a client's player) the light comes back.
        private Vector3 darkFrom;
        private bool watching;
        // A network client's player handed to the server (see HandOver), and when it was first seen moved far. Taken: the
        // server locked its controls (it took the player into a run: they stay locked until the player stands in the cave);
        // released: when it unlocked them again.
        private bool handedOver, fromCave, taken;
        private float handedAt, movedAt = -1f, openedAt = -1f, releasedAt = -1f;
        private MovementStateMachine heldMovement;
        private CombatEntity heldOf;
        private GUIStyle titleStyle, subtitleStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            MaxDarkSeconds = 25f;
            RefusedAfterSeconds = 6f;
        }

        private static CaveTransition Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                {
                    var go = new GameObject("[Cave Transition]") { hideFlags = HideFlags.HideInHierarchy };
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<CaveTransition>();
                }
                return instance;
            }
        }

        /// <summary>Is the screen (partly) dark now?</summary>
        public static bool IsDark => instance != null && instance.alpha > 0.01f;

        /// <summary>Darkens the screen over <paramref name="seconds"/>, showing <paramref name="caption"/> (and a second line).</summary>
        public static void FadeOut(string caption, string detail = "", float seconds = 0.6f)
        {
            CaveTransition t = Instance;
            if (t == null)
                return;
            t.title = caption ?? "";
            t.subtitle = detail ?? "";
            t.target = 1f;
            t.speed = 1f / Mathf.Max(0.05f, seconds);
            t.holdUntil = 0f;
            if (t.darkSince < 0f)
                t.darkSince = Time.unscaledTime;
            CombatEntity local = PlayerLocator.Local;
            t.watching = local != null;
            if (local != null)
                t.darkFrom = local.transform.position;
        }

        /// <summary>Brings the light back over <paramref name="seconds"/> (after <paramref name="delay"/> seconds).</summary>
        public static void FadeIn(float seconds = 0.9f, float delay = 0f)
        {
            if (instance == null)
                return;
            instance.target = 0f;
            instance.speed = 1f / Mathf.Max(0.05f, seconds);
            instance.holdUntil = Time.unscaledTime + Mathf.Max(0f, delay);
            instance.handedOver = false;
            instance.taken = false;
            instance.movedAt = -1f;
            instance.openedAt = -1f;
            instance.releasedAt = -1f;
        }

        /// <summary>
        /// A network client: its player stood in a passage long enough to be sent, and the server decides now. The screen
        /// stays dark until its copy of the cave is built, it is moved back to the world, or - not moved at all after
        /// <see cref="RefusedAfterSeconds"/> - the server said no.
        /// </summary>
        public static void HandOver()
        {
            CaveTransition t = instance;
            if (t == null || t.target <= 0f)
                return;
            t.handedOver = true;
            t.taken = false;
            t.handedAt = Time.unscaledTime;
            t.movedAt = -1f;
            t.openedAt = -1f;
            t.releasedAt = -1f;
            // Already in a cave (a client only has copies of the caves its player is in): the next one is built where this
            // one was, so its player may not move far - the new copy being built is enough.
            t.fromCave = false;
            foreach (CaveInstance c in CaveInstance.All)
                t.fromCave |= c != null && c.IsReplica;
            CombatEntity local = PlayerLocator.Local;
            t.watching = local != null;
            if (local != null)
                t.darkFrom = local.transform.position;
        }

        /// <summary>A network client's player walked back out of the passage after being handed over: the light comes back.</summary>
        public static void HandBack()
        {
            if (instance != null && instance.handedOver)
                FadeIn(0.6f, 0.6f);
        }

        /// <summary>
        /// Are this machine's player's controls locked by the server (taken into a run and waiting: the dungeon is being made,
        /// or the player was moved into it and waits for its copy to be built)? Server state, sent back to its owner.
        /// </summary>
        private bool LocalHeld(CombatEntity local)
        {
            if (local == null)
                return false;
            if (heldOf != local || heldMovement == null)
            {
                heldOf = local;
                GameObject root = PlayerLocator.MovableRoot(local);
                heldMovement = root != null ? root.GetComponentInChildren<MovementStateMachine>(true) : null;
            }
            return heldMovement != null && heldMovement.Frozen;
        }

        /// <summary>Is a network client's copy of a cave on its way (made, its first level not built yet)?</summary>
        private static bool CaveBuilding()
        {
            foreach (CaveInstance c in CaveInstance.All)
                if (c != null && c.IsReplica && !c.IsBuilt)
                    return true;
            return false;
        }

        private void OnEnable()
        {
            DungeonSession.RunEntered += OnRunEntered;
            DungeonSession.RunEnded += OnRunEnded;
            DungeonSession.PlayerJoined += OnPlayerJoined;
            CaveInstance.Opened += OnCaveOpened;
        }

        private void OnDisable()
        {
            DungeonSession.RunEntered -= OnRunEntered;
            DungeonSession.RunEnded -= OnRunEnded;
            DungeonSession.PlayerJoined -= OnPlayerJoined;
            CaveInstance.Opened -= OnCaveOpened;
        }

        // A network client's copy of a cave has its first level: the light comes back once its player stands in it (see Update).
        private void OnCaveOpened(CaveInstance cave)
        {
            if (cave != null && cave.IsReplica && target > 0f)
                openedAt = Time.unscaledTime;
        }

        private static bool Mine(DungeonRun run) => run != null && (run.HasLocalPlayer || (PlayerLocator.Local == null && !GameNet.NetAuthority.IsNetworked));

        // In: the group stands in the cave (its first floors are built). A short hold lets the camera settle first.
        private void OnRunEntered(DungeonRun run)
        {
            if (Mine(run))
                FadeIn(1.1f, 0.25f);
        }

        // Out (or failed, or refused): back in the world.
        private void OnRunEnded(DungeonRun run, DungeonRunEnd how)
        {
            if (Mine(run) || how == DungeonRunEnd.Failed)
                FadeIn(1f, 0.15f);
        }

        private void OnPlayerJoined(DungeonRun run, GameObject player)
        {
            CombatEntity local = PlayerLocator.Local;
            if (local != null && player != null && (local.gameObject == player || local.transform.IsChildOf(player.transform)))
                FadeIn(1.1f, 0.25f);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            // Never dark for good: a run that went wrong (or a client whose arrival the server holds too long).
            if (target > 0f && darkSince >= 0f && now - darkSince > MaxDarkSeconds)
                FadeIn(1f);
            // A network client is not told about runs. Its copy of a cave lights it up once its first level is built
            // (OnCaveOpened); otherwise it lights up once the server has moved its player far (back out to the world) and let
            // it go with no cave on its way, once the server let go of a player it had taken without moving it (the visit
            // failed), or - handed over, never taken - when the server said no. While the server holds the player (its
            // controls locked: the cave being made, however long that takes, or the player waiting in it for its copy) the
            // screen stays dark, up to Max Dark Seconds.
            if (target > 0f && GameNet.NetAuthority.IsClientOnly && darkSince >= 0f && now - darkSince > 1f)
            {
                CombatEntity local = PlayerLocator.Local;
                bool building = CaveBuilding();
                bool held = LocalHeld(local);
                if (handedOver && held)
                    taken = true;
                if (held)
                    releasedAt = -1f;
                else if (taken && releasedAt < 0f)
                    releasedAt = now;
                if (local != null && watching && movedAt < 0f && (local.transform.position - darkFrom).sqrMagnitude > 40f * 40f)
                    movedAt = now;
                if (openedAt >= 0f && (fromCave || movedAt >= 0f))
                    FadeIn(1.1f, 0.4f);   // in the cave (or the next one)
                else if (movedAt >= 0f && openedAt < 0f && !building && !held && now - movedAt > 1.2f)
                    FadeIn(1.1f, 0.2f);   // back in the world
                else if (taken && !held && movedAt < 0f && openedAt < 0f && !building && now - releasedAt > 1.2f)
                    FadeIn(0.8f, 0.1f);   // taken, then let go where it stood: the visit did not happen
                else if (handedOver && !taken && movedAt < 0f && openedAt < 0f && !building && now - handedAt > Mathf.Max(1f, RefusedAfterSeconds))
                    FadeIn(0.6f);         // never taken: the server said no
            }
            if (now < holdUntil)
                return;
            alpha = Mathf.MoveTowards(alpha, target, speed * Time.unscaledDeltaTime);
            if (alpha <= 0.001f && target <= 0f)
                darkSince = -1f;
        }

        private void OnGUI()
        {
            if (alpha <= 0.001f)
                return;
            if (black == null)
            {
                black = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
                black.SetPixel(0, 0, Color.black);
                black.Apply();
            }
            GUI.depth = -10000;
            Color before = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), black, ScaleMode.StretchToFill);
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
                subtitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic, wordWrap = true };
            }
            titleStyle.fontSize = Mathf.Max(18, Screen.height / 24);
            subtitleStyle.fontSize = Mathf.Max(12, Screen.height / 48);
            float textAlpha = Mathf.Clamp01((alpha - 0.5f) * 2f);
            if (textAlpha > 0f && !string.IsNullOrEmpty(title))
            {
                GUI.color = new Color(0.92f, 0.88f, 0.8f, textAlpha);
                float h = titleStyle.fontSize * 2f;
                GUI.Label(new Rect(0f, Screen.height * 0.45f - h * 0.5f, Screen.width, h), title, titleStyle);
                if (!string.IsNullOrEmpty(subtitle))
                {
                    GUI.color = new Color(0.75f, 0.72f, 0.66f, textAlpha);
                    float sh = subtitleStyle.fontSize * 2f;
                    // Dots that move while the cave is still being made.
                    int dots = 1 + (int)(Time.unscaledTime * 2f) % 3;
                    GUI.Label(new Rect(0f, Screen.height * 0.45f + h * 0.4f, Screen.width, sh), subtitle + new string('.', dots), subtitleStyle);
                }
            }
            GUI.color = before;
        }
    }
}
