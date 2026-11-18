using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Base of the dungeon's usable objects (rest fountains, shrines, chests, herbs, altars, levers). Used with the
    /// player's Interact input action - whatever key or button the player's input actions bind it to: the player's
    /// <see cref="PlayerInteractor"/> finds it, shows its prompt ("Open  Chest", with the bound key and the hold
    /// progress) and uses it for the player who pressed (<see cref="IInteractionPrompt"/>); a <c>Player.OnInteract</c>
    /// without an interactor works too (it is an <see cref="Interactable"/>).
    /// <para>Standing next to it for Hold To Use seconds only uses it for a player who has no Interact input at all (no
    /// Player Interactor, no Player component) - so a bare test character still works - or when Stand To Use is on.
    /// A player with the input never spends a shrine or pays an altar by walking past it.</para>
    /// <para>Network sessions: only the server uses it (hold-to-use measured from the server's positions, or a client's
    /// request validated by GameNet.NetDungeonRun - see <see cref="IDungeonNetInteract"/>); the number of uses is
    /// published to the clients, whose copies show it (<see cref="ApplyNetState"/>).</para>
    /// </summary>
    public abstract class DungeonInteractable : Interactable, IDungeonNetState, IDungeonNetInteract, IInteractionPrompt
    {
        [Tooltip("Seconds the Interact input must be held to use it (0 = a press). The prompt shows the progress.")]
        [Min(0f)] public float interactHold;
        [Tooltip("Seconds a player without an Interact input must stand within Use Distance to use it (0 = never). Players who " +
                 "have the input use it with that instead, unless Stand To Use is on.")]
        [Min(0f)] public float holdToUse = 1.2f;
        [Tooltip("Standing next to it for Hold To Use seconds uses it for every player, also those with an Interact input.")]
        public bool standToUse;
        [Tooltip("How close a player must stand (meters).")]
        [Min(0.3f)] public float useDistance = 1.8f;
        [Tooltip("How many times it can be used (0 = unlimited).")]
        [Min(0)] public int uses = 1;
        [Tooltip("Seconds between uses.")]
        [Min(0f)] public float cooldown = 2f;

        public int TimesUsed { get; private set; }
        public bool UsedUp => uses > 0 && TimesUsed >= uses;

        private float held;
        private float readyAt;
        private bool usedUpShown;
        private new DungeonSpawned tag;

        protected DungeonInstance Dungeon => (tag != null || (tag = GetComponent<DungeonSpawned>()) != null) ? tag.Dungeon : GetComponentInParent<DungeonInstance>();

        /// <summary>The Interact key: used by the nearest living player.</summary>
        public override void Interact()
        {
            if (DungeonNet.IsReplica(this))
            {
                // A client's copy: the server uses it (for this machine's player).
                CombatEntity mine = PlayerLocator.Local;
                DungeonNet.TryRelayInteract(this, mine != null ? PlayerLocator.MovableRoot(mine) : null);
                return;
            }
            CombatEntity player = DungeonPlayers.NearestAlive(transform.position, useDistance + 2f);
            // A host's Interact key is its own player's, even when a client's player stands nearer.
            CombatEntity local = GameNet.NetAuthority.IsNetworked ? PlayerLocator.Local : null;
            if (local != null && local != player && DungeonPlayers.IsWithin(local, transform.position, useDistance + 2f))
                player = local;
            if (player != null)
                TryUse(player);
        }

        protected virtual void Update()
        {
            if (holdToUse <= 0f || UsedUp || Time.time < readyAt)
                return;
            if (DungeonNet.IsReplica(this))
                return;   // the server measures who stands next to it
            CombatEntity player = DungeonPlayers.NearestAlive(transform.position, useDistance);
            // A player with an Interact input uses it with that (the prompt shows the key): never by standing near.
            if (player != null && !standToUse && HasInteractInput(player))
                player = null;
            held = player != null ? held + Time.deltaTime : 0f;
            if (player != null && held >= holdToUse)
            {
                held = 0f;
                TryUse(player);
            }
        }

        // ------------------------------------------------------------------ the player's Interact input (IInteractionPrompt)

        /// <summary>Name in the prompt: the dungeon table entry it came from ("Ornate Chest", "Gas Valve"), else the object's name.</summary>
        public virtual string PromptName
        {
            get
            {
                if (tag == null)
                    tag = GetComponent<DungeonSpawned>();
                string entry = tag != null ? tag.entryName : null;
                return !string.IsNullOrWhiteSpace(entry) ? entry : gameObject.name.Replace("(Clone)", "").Trim();
            }
        }

        /// <summary>What using it does, in the prompt ("Open", "Pray", "Pull").</summary>
        public virtual string PromptVerb => "Use";

        /// <summary>Seconds the Interact input must be held.</summary>
        public virtual float PromptHold => interactHold;

        /// <summary>The same hold for a player whose Interact goes through the older <c>Player.OnInteract</c> (it reads this).</summary>
        public override float InteractionTime
        {
            get => Mathf.Max(base.InteractionTime, PromptHold);
            set => base.InteractionTime = value;
        }

        /// <summary>How far using it is heard (metres; 0 = silent). Default: Combat Settings ▸ Noise ▸ Interact Radius.</summary>
        protected virtual float UseNoiseRadius => CombatSettings.Instance.noise.interactRadius;

        /// <summary>Why it can't be used any more, in the prompt of a player who aims at it.</summary>
        protected virtual string UsedUpReason => "Already used";

        /// <summary>Can <paramref name="player"/> use it now (besides not being used up or cooling down)? <paramref name="reason"/> says why not.</summary>
        protected virtual bool CanUse(CombatEntity player, out string reason)
        {
            reason = "";
            return true;
        }

        public bool CanInteract(PlayerInteractor interactor, out string reason)
        {
            reason = "";
            if (UsedUp)
            {
                reason = UsedUpReason;
                return false;
            }
            if (Time.time < readyAt)
            {
                reason = "Not ready yet";
                return false;
            }
            CombatEntity player = EntityOf(interactor);
            if (player == null || !player.IsAlive)
                return false;
            return CanUse(player, out reason);
        }

        /// <summary>The Interact input of <paramref name="interactor"/>'s player: used by that player.</summary>
        public void Interact(PlayerInteractor interactor)
        {
            CombatEntity player = DungeonNet.IsReplica(this) ? null : EntityOf(interactor);
            if (player != null && player.IsAlive)
                TryUse(player);
            else
                Interact();   // a client's copy asks the server; without a known player, the nearest one uses it
        }

        private PlayerInteractor lastInteractor;
        private CombatEntity lastInteractorEntity;

        private CombatEntity EntityOf(PlayerInteractor interactor)
        {
            if (interactor == null)
                return null;
            if (lastInteractor != interactor || lastInteractorEntity == null)
            {
                lastInteractor = interactor;
                lastInteractorEntity = CombatEntity.Resolve(interactor.PlayerObject);
            }
            return lastInteractorEntity;
        }

        // Which players have an Interact input (looked up again every few seconds: components come and go).
        private static readonly System.Collections.Generic.Dictionary<CombatEntity, float> inputChecked = new System.Collections.Generic.Dictionary<CombatEntity, float>();
        private static readonly System.Collections.Generic.HashSet<CombatEntity> withInput = new System.Collections.Generic.HashSet<CombatEntity>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInputCache()
        {
            inputChecked.Clear();
            withInput.Clear();
        }

        /// <summary>
        /// Does <paramref name="player"/> have an Interact input: a <see cref="PlayerInteractor"/> (it reads the player's
        /// Interact input action), or the older <c>Player</c> component (its OnInteract is wired to that action)?
        /// </summary>
        public static bool HasInteractInput(CombatEntity player)
        {
            if (player == null)
                return false;
            if (inputChecked.TryGetValue(player, out float at) && Time.time - at < 3f)
                return withInput.Contains(player);
            if (inputChecked.Count > 64)
            {
                inputChecked.Clear();
                withInput.Clear();
            }
            inputChecked[player] = Time.time;
            PlayerInteractor interactor = PlayerInteractor.For(player);
            bool has = (interactor != null && interactor.isActiveAndEnabled) || player.GetComponentInParent<global::Player>() != null ||
                       player.GetComponentInChildren<global::Player>() != null;
            if (has)
                withInput.Add(player);
            else
                withInput.Remove(player);
            return has;
        }

        public bool TryUse(CombatEntity player)
        {
            if (UsedUp || Time.time < readyAt || player == null || DungeonNet.IsReplica(this))
                return false;
            if (!Use(player))
                return false;
            // Using it is heard (mobs in earshot come for whoever did it).
            float noise = UseNoiseRadius;
            if (noise > 0f)
                CombatEvents.EmitNoise(transform.position, noise, player, 0.5f, NoiseKind.Interaction);
            TimesUsed++;
            readyAt = Time.time + cooldown;
            if (UsedUp && !usedUpShown)
            {
                usedUpShown = true;
                OnUsedUp();
            }
            DungeonNet.Publish(this, NetState);
            return true;
        }

        /// <summary>The state published to clients after each use (default: the number of uses).</summary>
        protected virtual int NetState => TimesUsed;

        /// <summary>
        /// A client's copy shows the server's state: the number of uses (and, once used up, what <see cref="OnUsedUp"/>
        /// shows). Override <see cref="OnNetState"/> for what else a use shows (a lid, a handle...).
        /// </summary>
        public virtual void ApplyNetState(int state, byte[] payload)
        {
            TimesUsed = Mathf.Max(0, state);
            OnNetState(state, payload);
            if (UsedUp && !usedUpShown)
            {
                usedUpShown = true;
                OnUsedUp();
            }
        }

        /// <summary>A client's copy received a new state from the server (see <see cref="ApplyNetState"/>).</summary>
        protected virtual void OnNetState(int state, byte[] payload)
        {
        }

        /// <summary>How close a player must be (server positions) for a client's request to use it.</summary>
        public virtual float NetInteractRange => useDistance + 1.5f;

        /// <summary>The server uses it for a client's validated request.</summary>
        public virtual bool NetInteract(CombatEntity player) => TryUse(player);

        /// <summary>What using it does. Return false when nothing happened (it isn't counted as used).</summary>
        protected abstract bool Use(CombatEntity player);

        /// <summary>Called once it can't be used any more (dim its light, close it...).</summary>
        protected virtual void OnUsedUp()
        {
            foreach (Light l in GetComponentsInChildren<Light>())
                l.intensity *= 0.3f;
        }
    }
}
