using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A non-player character the player can interact with: name, title and portrait, how close the player must be, what
/// it says, and what it does - its <see cref="NPCBehaviour"/>s (trade with <see cref="Merchant"/>, talk with
/// <see cref="NPCDialogue"/>, anything you add). Interacting opens a session (<see cref="NPCInteractionSession"/>): an
/// NPC with one option opens it directly, otherwise a menu with its greeting and the options. The session ends when the
/// player closes it, walks away, dies, or the NPC is disabled.
/// <para>Found by the player's <see cref="PlayerInteractor"/> (needs a collider on the NPC or a child). Scripts and
/// UnityEvents can open it with <see cref="Interact()"/> (used by the nearest player); a second request in the same
/// frame is ignored, so two inputs never open and close it at once.</para>
/// </summary>
[DisallowMultipleComponent]
public class NPC : MonoBehaviour, IInteractable
{
    [Header("Identity")]
    [Tooltip("Name shown in prompts, dialogue and shops.")]
    [SerializeField] private string displayName = "Villager";
    [Tooltip("Optional role under the name (\"Blacksmith\", \"Innkeeper\").")]
    [SerializeField] private string title;
    [Tooltip("Optional picture shown in the dialogue and shop windows.")]
    [SerializeField] private Sprite portrait;
    [Tooltip("Stable id for saves and networking (filled automatically; keep it unique).")]
    [SerializeField] private string npcId;

    [Header("Interaction")]
    [Tooltip("How close the player must stand (metres, on the ground plane from the Interaction Point).")]
    [SerializeField, Min(0.5f)] private float interactionRange = 3f;
    [Tooltip("When several things can be interacted with, the highest priority is chosen first. NPCs default above items on the ground.")]
    [SerializeField] private int interactionPriority = 10;
    [Tooltip("The player can click the NPC to interact.")]
    [SerializeField] private bool allowClick = true;
    [Tooltip("Where the range is measured from (empty = this object). Put it at the counter for a shopkeeper behind one.")]
    [SerializeField] private Transform interactionPoint;
    [Tooltip("A session ends when the player walks farther than the Interaction Range plus this (metres).")]
    [SerializeField, Min(0f)] private float leaveDistanceMargin = 2f;
    [Tooltip("Seconds after a session ends before a new one can start (stops a double press reopening it).")]
    [SerializeField, Min(0f)] private float interactionCooldown = 0.2f;
    [Tooltip("How many players can use the NPC at once (0 = any number). Others see \"Busy\".")]
    [SerializeField, Min(0)] private int maxPlayersAtOnce = 0;
    [Tooltip("Seconds the Interact key must be held to talk (0 = a press).")]
    [SerializeField, Min(0f)] private float holdToInteract;
    [Tooltip("Refuse players the NPC considers enemies (needs a Combat Entity on the NPC). Note: a Combat Entity without AI counts " +
             "players as enemies, so turn this on only for NPCs whose faction is set up.")]
    [SerializeField] private bool refuseHostilePlayers;

    [Header("Conversation")]
    [Tooltip("One is picked at random when the NPC's menu opens (\"Welcome, traveller!\").")]
    [SerializeField, TextArea(1, 3)] private List<string> greetings = new List<string> { "Greetings, traveller. What can I do for you?" };
    [Tooltip("One is shown as a short message when the player leaves (empty = nothing).")]
    [SerializeField, TextArea(1, 2)] private List<string> farewells = new List<string> { "Safe travels." };
    [Tooltip("An NPC with a single option (e.g. only a shop) opens it directly instead of showing its menu first.")]
    [SerializeField] private bool openSingleOptionDirectly = true;
    [Tooltip("Closing an option chosen from the menu (the shop) goes back to the menu instead of ending the conversation.")]
    [SerializeField] private bool returnToMenuAfterOption;
    [Tooltip("Word of the prompt when the NPC has several options (\"Talk\").")]
    [SerializeField] private string talkVerb = "Talk";

    [Header("Presentation")]
    [Tooltip("Turn to face the player while talking.")]
    [SerializeField] private bool faceThePlayer = true;
    [Tooltip("Degrees per second when turning.")]
    [SerializeField, Min(0f)] private float turnSpeed = 360f;
    [Tooltip("Turn back to the original direction after the conversation.")]
    [SerializeField] private bool turnBackWhenDone = true;
    [Tooltip("Optional object shown while the NPC is the player's interaction target (a ring, an outline, a marker).")]
    [SerializeField] private GameObject focusIndicator;
    [Tooltip("Optional animator: the trigger below is set when a conversation starts (only if the animator has it).")]
    [SerializeField] private Animator animator;
    [SerializeField] private string interactTrigger = "Talk";
    [Tooltip("Played at the NPC when a conversation starts.")]
    [SerializeField] private AudioClip greetingSound;
    [Tooltip("Your own dialogue window prefab (needs an NPCDialogueWindow). Empty = the default window built at runtime.")]
    [SerializeField] private NPCDialogueWindow dialogueWindowPrefab;

    [Header("Events")]
    [Tooltip("A conversation started (the player object).")]
    public UnityEvent<GameObject> onConversationStarted = new UnityEvent<GameObject>();
    [Tooltip("A conversation ended (the player object).")]
    public UnityEvent<GameObject> onConversationEnded = new UnityEvent<GameObject>();

    /// <summary>Any NPC started a session.</summary>
    public static event Action<NPCInteractionSession> AnySessionStarted;
    /// <summary>Any NPC's session ended.</summary>
    public static event Action<NPCInteractionSession> AnySessionEnded;
    public event Action<NPCInteractionSession> SessionStarted;
    public event Action<NPCInteractionSession> SessionEnded;

    private static readonly List<NPC> all = new List<NPC>();
    private readonly List<NPCBehaviour> behaviours = new List<NPCBehaviour>();
    private readonly List<NPCBehaviour> optionBuffer = new List<NPCBehaviour>();
    private readonly List<NPCInteractionSession> sessions = new List<NPCInteractionSession>();
    private readonly HashSet<PlayerInteractor> focusedBy = new HashSet<PlayerInteractor>();
    private bool behavioursDirty = true;
    private int lastRequestFrame = -1;
    private float readyAt;
    private Quaternion restRotation;
    private bool hasRestRotation;
    private CombatEntity entity;
    private bool entityResolved;

    public static IReadOnlyList<NPC> All => all;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
    public string Title => title;
    public Sprite Portrait => portrait;
    public string NpcId => string.IsNullOrEmpty(npcId) ? gameObject.name : npcId;
    public IReadOnlyList<NPCInteractionSession> Sessions => sessions;
    public bool IsInConversation => sessions.Count > 0;
    public NPCDialogueWindow DialogueWindowPrefab => dialogueWindowPrefab;

    /// <summary>The behaviours (options) of the NPC, in option order.</summary>
    public IReadOnlyList<NPCBehaviour> Behaviours
    {
        get
        {
            if (behavioursDirty)
                RefreshBehaviours();
            return behaviours;
        }
    }

    // ------------------------------------------------------------------ IInteractable
    public string InteractionName => DisplayName;

    public string InteractionVerb
    {
        get
        {
            IReadOnlyList<NPCBehaviour> b = Behaviours;
            int enabledCount = 0;
            NPCBehaviour only = null;
            for (int i = 0; i < b.Count; i++)
                if (b[i] != null && b[i].isActiveAndEnabled) { enabledCount++; only = b[i]; }
            return enabledCount == 1 && openSingleOptionDirectly ? only.PromptVerb : string.IsNullOrWhiteSpace(talkVerb) ? "Talk" : talkVerb;
        }
    }

    public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;
    public float InteractionRange => interactionRange;
    public int InteractionPriority => interactionPriority;
    public float HoldDuration => Mathf.Max(0f, holdToInteract);
    public bool AllowsClick => allowClick;

    public bool CanInteract(PlayerInteractor interactor, out string reason)
    {
        reason = "";
        if (!isActiveAndEnabled)
            return false;
        CombatEntity self = Entity;
        if (self != null && !self.IsAlive)
        {
            reason = "Not responding";
            return false;
        }
        GameObject player = interactor != null ? interactor.PlayerObject : null;
        if (refuseHostilePlayers && self != null && player != null)
        {
            CombatEntity p = CombatEntity.Resolve(player);
            if (p != null && CombatRelations.IsEnemy(self, p))
            {
                reason = "Hostile";
                return false;
            }
        }
        if (maxPlayersAtOnce > 0 && sessions.Count >= maxPlayersAtOnce && SessionFor(player) == null)
        {
            reason = "Busy";
            return false;
        }
        if (!HasAnythingToOffer())
        {
            reason = "Nothing to say";
            return false;
        }
        return true;
    }

    /// <summary>The player's interactor asks to interact: opens a session, or closes the open one.</summary>
    public void Interact(PlayerInteractor interactor)
    {
        if (interactor == null || !ClaimRequestFrame())
            return;
        RequestFor(interactor.PlayerObject, interactor);
    }

    /// <summary>Opens (or closes) the NPC for the nearest player - for scripts, UnityEvents and older interaction code.</summary>
    public void Interact()
    {
        if (!ClaimRequestFrame())
            return;
        GameObject player = NearestPlayer();
        if (player == null)
            return;
        PlayerInteractor interactor = PlayerInteractor.For(player.transform);
        if (interactor != null && !interactor.IsInRange(this) && SessionFor(player) == null)
            return;
        RequestFor(player, interactor);
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused)
    {
        if (interactor == null)
            return;
        if (focused) focusedBy.Add(interactor);
        else focusedBy.Remove(interactor);
        focusedBy.RemoveWhere(i => i == null);
        if (focusIndicator != null)
            focusIndicator.SetActive(focusedBy.Count > 0);
    }

    // ------------------------------------------------------------------ sessions
    /// <summary>The open session of <paramref name="player"/>, or null.</summary>
    public NPCInteractionSession SessionFor(GameObject player)
    {
        if (player == null)
            return null;
        for (int i = 0; i < sessions.Count; i++)
            if (sessions[i].Player == player)
                return sessions[i];
        return null;
    }

    /// <summary>
    /// Opens a session with <paramref name="player"/> (scripts, cutscenes, quest triggers). Range and availability are not
    /// checked. Returns the session (the existing one when already open), or null when the NPC has nothing to offer.
    /// </summary>
    public NPCInteractionSession StartSession(GameObject player, PlayerInteractor interactor = null)
    {
        if (player == null)
            return null;
        Transform root = AbilitiesStateMachine.PlayerRoot(player.transform);
        player = root.gameObject;
        NPCInteractionSession existing = SessionFor(player);
        if (existing != null)
            return existing;
        if (interactor == null)
            interactor = PlayerInteractor.For(root);
        if (!HasAnythingToOffer())
            return null;

        var session = new NPCInteractionSession(this, player, interactor);
        sessions.Add(session);
        if (!hasRestRotation)
        {
            restRotation = transform.rotation;
            hasRestRotation = true;
        }
        interactor?.BeginInteraction(this, () => session.End("closed"));
        PlayIntro();
        onConversationStarted?.Invoke(player);
        SessionStarted?.Invoke(session);
        AnySessionStarted?.Invoke(session);

        GetOptions(session, optionBuffer);
        if (optionBuffer.Count == 1 && openSingleOptionDirectly)
            OpenBehaviour(session, optionBuffer[0], false);
        else
            ShowMenu(session);
        return session;
    }

    /// <summary>Shows the NPC's menu (greeting and options) in the session.</summary>
    public void ShowMenu(NPCInteractionSession session)
    {
        if (session == null || !session.IsOpen)
            return;
        CloseActiveBehaviour(session);
        // Usable options, and unusable ones that say why (shown disabled with the reason).
        var options = new List<NPCBehaviour>();
        IReadOnlyList<NPCBehaviour> own = Behaviours;
        for (int i = 0; i < own.Count; i++)
        {
            NPCBehaviour b = own[i];
            if (b == null || !b.isActiveAndEnabled)
                continue;
            if (b.IsAvailable(session, out string reason) || !string.IsNullOrEmpty(reason))
                options.Add(b);
        }
        NPCDialogueWindow window = NPCDialogueWindow.For(session, dialogueWindowPrefab);
        window.ShowMenu(session, PickGreeting(), options, b => OpenBehaviour(session, b, true));
    }

    /// <summary>Opens <paramref name="behaviour"/> in the session (closing the menu or another option).</summary>
    public void OpenBehaviour(NPCInteractionSession session, NPCBehaviour behaviour, bool fromMenu)
    {
        if (session == null || !session.IsOpen || behaviour == null)
            return;
        if (!behaviour.IsAvailable(session, out string reason))
        {
            session.Inventory?.ShowMessage(string.IsNullOrEmpty(reason) ? $"{behaviour.OptionLabel} is not available." : reason, InventoryFeedback.Kind.Info);
            return;
        }
        CloseActiveBehaviour(session);
        NPCDialogueWindow.HideFor(session); // the menu makes room for the option's window
        session.ActiveBehaviour = behaviour;
        session.OpenedFromMenu = fromMenu;
        try { behaviour.Begin(session); }
        catch (Exception e)
        {
            Debug.LogException(e, behaviour);
            EndSession(session, "error");
        }
    }

    internal void OnBehaviourFinished(NPCInteractionSession session, NPCBehaviour behaviour)
    {
        CloseActiveBehaviour(session);
        GetOptions(session, optionBuffer);
        if (returnToMenuAfterOption && session.OpenedFromMenu && optionBuffer.Count > 0)
            ShowMenu(session);
        else
            EndSession(session, "closed");
    }

    internal void EndSession(NPCInteractionSession session, string reason)
    {
        if (session == null || !session.IsOpen)
            return;
        session.IsOpen = false;
        session.EndReason = reason ?? "";
        CloseActiveBehaviour(session);
        NPCDialogueWindow.HideFor(session);
        sessions.Remove(session);
        session.Interactor?.EndInteraction(this);
        readyAt = Time.time + interactionCooldown;
        lastRequestFrame = Time.frameCount; // a press in the same frame does not reopen it
        if (reason == "closed" || reason == "walked away")
            SayFarewell(session);
        session.RaiseEnded();
        if (session.Player != null)
            onConversationEnded?.Invoke(session.Player);
        SessionEnded?.Invoke(session);
        AnySessionEnded?.Invoke(session);
    }

    /// <summary>Ends every open session (the NPC leaves, a cutscene starts).</summary>
    public void EndAllSessions(string reason = "ended")
    {
        for (int i = sessions.Count - 1; i >= 0; i--)
            if (i < sessions.Count)
                EndSession(sessions[i], reason);
    }

    private void CloseActiveBehaviour(NPCInteractionSession session)
    {
        NPCBehaviour b = session.ActiveBehaviour;
        session.ActiveBehaviour = null;
        if (b == null)
            return;
        try { b.End(session); }
        catch (Exception e) { Debug.LogException(e, b); }
    }

    // ------------------------------------------------------------------ behaviours
    /// <summary>Finds the behaviours again (after adding or removing one at runtime).</summary>
    public void RefreshBehaviours()
    {
        behavioursDirty = false;
        behaviours.Clear();
        GetComponents(behaviours);
        behaviours.RemoveAll(b => b == null);
        behaviours.Sort((a, b) => a.OptionOrder.CompareTo(b.OptionOrder));
    }

    internal void OnBehaviourDisabled(NPCBehaviour behaviour)
    {
        behavioursDirty = true;
        for (int i = sessions.Count - 1; i >= 0; i--)
            if (i < sessions.Count && ReferenceEquals(sessions[i].ActiveBehaviour, behaviour))
                EndSession(sessions[i], "option disabled");
    }

    /// <summary>The options usable in <paramref name="session"/>, in order.</summary>
    public List<NPCBehaviour> GetOptions(NPCInteractionSession session, List<NPCBehaviour> into)
    {
        into.Clear();
        IReadOnlyList<NPCBehaviour> b = Behaviours;
        for (int i = 0; i < b.Count; i++)
            if (b[i] != null && b[i].isActiveAndEnabled && b[i].IsAvailable(session, out _))
                into.Add(b[i]);
        return into;
    }

    private bool HasAnythingToOffer()
    {
        IReadOnlyList<NPCBehaviour> b = Behaviours;
        for (int i = 0; i < b.Count; i++)
            if (b[i] != null && b[i].isActiveAndEnabled)
                return true;
        return greetings != null && greetings.Exists(g => !string.IsNullOrWhiteSpace(g));
    }

    // ------------------------------------------------------------------ internals
    private bool ClaimRequestFrame()
    {
        if (lastRequestFrame == Time.frameCount)
            return false;
        lastRequestFrame = Time.frameCount;
        return true;
    }

    private void RequestFor(GameObject player, PlayerInteractor interactor)
    {
        if (player == null)
            return;
        player = AbilitiesStateMachine.PlayerRoot(player.transform).gameObject;
        NPCInteractionSession open = SessionFor(player);
        if (open != null)
        {
            open.End("closed"); // interacting again closes it
            return;
        }
        if (Time.time < readyAt)
            return;
        if (interactor != null && !CanInteract(interactor, out string reason))
        {
            if (!string.IsNullOrEmpty(reason))
                InventoryManager.For(player.transform)?.ShowMessage($"{DisplayName}: {reason}", InventoryFeedback.Kind.Info);
            return;
        }
        StartSession(player, interactor);
    }

    private CombatEntity Entity
    {
        get
        {
            if (!entityResolved)
            {
                entityResolved = true;
                entity = GetComponent<CombatEntity>();
            }
            return entity;
        }
    }

    private GameObject NearestPlayer()
    {
        Vector3 at = InteractionPoint.position;
        float best = (interactionRange + leaveDistanceMargin + 2f) * (interactionRange + leaveDistanceMargin + 2f);
        GameObject found = null;
        foreach (CombatEntity p in CombatEntity.Players)
        {
            if (p == null || !p.IsAlive)
                continue;
            float d = (p.transform.position - at).sqrMagnitude;
            if (d <= best)
            {
                best = d;
                found = p.gameObject;
            }
        }
        if (found == null)
        {
            PlayerStatusController ps = FindAnyObjectByType<PlayerStatusController>();
            if (ps != null && (ps.transform.position - at).sqrMagnitude <= best)
                found = ps.gameObject;
        }
        return found;
    }

    private string PickGreeting()
    {
        if (greetings == null || greetings.Count == 0)
            return "";
        var valid = greetings.FindAll(g => !string.IsNullOrWhiteSpace(g));
        return valid.Count > 0 ? valid[UnityEngine.Random.Range(0, valid.Count)] : "";
    }

    private void SayFarewell(NPCInteractionSession session)
    {
        if (farewells == null || farewells.Count == 0)
            return;
        var valid = farewells.FindAll(f => !string.IsNullOrWhiteSpace(f));
        if (valid.Count > 0)
            session.Inventory?.ShowMessage($"{DisplayName}: {valid[UnityEngine.Random.Range(0, valid.Count)]}", InventoryFeedback.Kind.Info);
    }

    private void PlayIntro()
    {
        if (greetingSound != null)
            AudioSource.PlayClipAtPoint(greetingSound, InteractionPoint.position);
        if (animator != null && !string.IsNullOrEmpty(interactTrigger) && animator.runtimeAnimatorController != null)
            foreach (AnimatorControllerParameter p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == interactTrigger)
                {
                    animator.SetTrigger(interactTrigger);
                    break;
                }
    }

    private void OnEnable()
    {
        if (!all.Contains(this))
            all.Add(this);
        behavioursDirty = true;
        if (focusIndicator != null)
            focusIndicator.SetActive(false);
    }

    private void OnDisable()
    {
        all.Remove(this);
        EndAllSessions("npc disabled");
        focusedBy.Clear();
        if (focusIndicator != null)
            focusIndicator.SetActive(false);
    }

    private void Update()
    {
        for (int i = sessions.Count - 1; i >= 0; i--)
        {
            if (i >= sessions.Count)
                continue;
            NPCInteractionSession s = sessions[i];
            if (s.Player == null || !s.Player.activeInHierarchy)
            {
                EndSession(s, "player gone");
                continue;
            }
            CombatEntity pe = CombatEntity.Resolve(s.Player);
            if (pe != null && !pe.IsAlive)
            {
                EndSession(s, "player died");
                continue;
            }
            if (!WithinLeaveDistance(s.Player.transform.position))
                EndSession(s, "walked away");
        }
        UpdateFacing();
    }

    private bool WithinLeaveDistance(Vector3 playerPosition)
    {
        Vector3 d = playerPosition - InteractionPoint.position;
        d.y = 0f;
        float r = interactionRange + leaveDistanceMargin;
        return d.sqrMagnitude <= r * r;
    }

    private void UpdateFacing()
    {
        if (!faceThePlayer || turnSpeed <= 0f)
            return;
        Quaternion target;
        if (sessions.Count > 0 && sessions[0].Player != null)
        {
            Vector3 to = sessions[0].Player.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f)
                return;
            target = Quaternion.LookRotation(to.normalized, Vector3.up);
        }
        else if (turnBackWhenDone && hasRestRotation)
        {
            target = restRotation;
            if (Quaternion.Angle(transform.rotation, target) < 0.5f)
            {
                transform.rotation = target;
                hasRestRotation = false;
                return;
            }
        }
        else
        {
            hasRestRotation = false;
            return;
        }
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    /// <summary>Configuration problems (shown by the NPC inspector).</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        if (GetComponentInChildren<Collider>(true) == null)
            errors.Add("No collider on the NPC or its children: the Player Interactor cannot find it (add a Capsule Collider).");
        IReadOnlyList<NPCBehaviour> b = Behaviours;
        if (b.Count == 0 && (greetings == null || !greetings.Exists(g => !string.IsNullOrWhiteSpace(g))))
            warnings.Add("The NPC has no behaviour (Merchant, NPC Dialogue...) and no greeting: there is nothing to interact with.");
        if (interactionRange < 1f)
            warnings.Add("Interaction Range is under 1 m: the player has to stand inside the NPC to use it.");
        if (string.IsNullOrWhiteSpace(displayName))
            warnings.Add("No display name: the object's name is shown.");
        foreach (NPCBehaviour behaviour in b)
            behaviour?.Validate(errors, warnings);
        if (!string.IsNullOrEmpty(npcId))
            foreach (NPC other in all)
                if (other != null && other != this && other.npcId == npcId)
                {
                    warnings.Add($"NPC Id '{npcId}' is also used by '{other.name}' (duplicated object?): saves and networking need unique ids.");
                    break;
                }
    }

    private void Reset()
    {
        displayName = gameObject.name;
        npcId = Guid.NewGuid().ToString("N").Substring(0, 12);
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(npcId))
            npcId = Guid.NewGuid().ToString("N").Substring(0, 12);
        behavioursDirty = true;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 at = InteractionPoint.position;
        Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.6f);
        DrawCircle(at, interactionRange);
        Gizmos.color = new Color(1f, 0.6f, 0.3f, 0.35f);
        DrawCircle(at, interactionRange + leaveDistanceMargin);
    }

    private static void DrawCircle(Vector3 c, float r)
    {
        const int steps = 40;
        Vector3 prev = c + new Vector3(r, 0f, 0f);
        for (int i = 1; i <= steps; i++)
        {
            float a = i * Mathf.PI * 2f / steps;
            Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all.Clear();
        AnySessionStarted = null;
        AnySessionEnded = null;
    }
}
