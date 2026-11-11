using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// A rune user's soul: Soul Integrity (with deep damage, conditions, Soul Collapse), Soul Rune Capacity, and soul
    /// recovery (rest, meditation, sanctuaries, items, rituals, modifiers from abilities and traits). Design spec 4, 14, 15, 20.
    /// <para>Put it on the player (Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune Magic to Selected Player); it brings a
    /// <see cref="SoulIntegrityManager"/>, so the HUD shows a Soul gauge. On players that are not rune users it does nothing
    /// and shows nothing: their abilities and essences are unchanged. A player that stops being a rune user keeps their soul,
    /// dormant.</para>
    /// <para>Where the rules run (offline, the server) this component decides everything and keeps the soul in the world's
    /// store (<see cref="RuneSoulSaves"/>). A network client only shows the <see cref="View"/> the server sends its owner
    /// (NetRuneMagic) and sends requests; calls that change the soul do nothing there.</para>
    /// <para>Independent resources (design spec 20): nothing here reads or changes health, mana, stamina or rune energy.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SoulIntegrityManager))]
    [AddComponentMenu("SimpleMovements/Rune Magic/Rune Soul")]
    public partial class RuneSoul : MonoBehaviour
    {
        [Tooltip("Log soul damage, collapses, rituals and saves to the Console.")]
        [SerializeField] private bool logEvents;

        private struct RecoveryModifier
        {
            public float multiplier;
            public float flatPerMinute;
        }

        private sealed class TimedRestore
        {
            public float perSecond;
            public float deepPerSecond;
            public float secondsLeft;
        }

        private static readonly List<RuneSoul> all = new List<RuneSoul>();

        private RuneSoulState state = new RuneSoulState();
        private readonly RuneSoulView view = new RuneSoulView();
        private readonly RuneSoulView scratch = new RuneSoulView();
        private int version;

        private CombatEntity entity;
        private bool entityHooked;
        private PlayerStatusController status;
        private SleepManager sleep;
        private ExperienceManager xp;
        private AvailabilityStateMachine availability;
        private SoulIntegrityManager manager;
        private bool looked;
        private float nextLook;

        private bool runeUser;
        private float nextUserCheck;
        private bool tracked;

        private readonly Dictionary<object, RecoveryModifier> recoveryModifiers = new Dictionary<object, RecoveryModifier>();
        private readonly Dictionary<object, int> capacityModifiers = new Dictionary<object, int>();
        private readonly Dictionary<object, float> maxModifiers = new Dictionary<object, float>();
        private readonly List<TimedRestore> timed = new List<TimedRestore>();

        private readonly object collapseKey = new object();
        private float incapacitatedUntil;
        private bool collapseHeld;
        private string lastCondition;
        private float lastRate;
        private RecoveryBreakdown lastBreakdown;
        private float timedPerMinute;
        private string message = "";
        private int messageSeq;
        private bool saveDirty;
        private float lastSaveMark = float.NegativeInfinity;
        private uint requestSeq;

        // ------------------------------------------------------------------ events
        /// <summary>The view changed (any machine).</summary>
        public event Action<RuneSoul> Changed;
        /// <summary>The soul was damaged (where the rules run).</summary>
        public event Action<RuneSoul, SoulDamageResult> Damaged;
        /// <summary>The soul collapsed (where the rules run): systems apply their consequences (pending energy is lost...).</summary>
        public event Action<RuneSoul> Collapsed;
        /// <summary>The soul's condition changed: previous id, new id (where the rules run).</summary>
        public event Action<RuneSoul, string, string> ConditionChanged;
        /// <summary>Meditation or a ritual began or ended (where the rules run).</summary>
        public event Action<RuneSoul, SoulChannel> ChannelChanged;

        // ------------------------------------------------------------------ lookup
        /// <summary>Every enabled soul.</summary>
        public static IReadOnlyList<RuneSoul> All => all;

        /// <summary>The soul of the player <paramref name="anyPart"/> belongs to (anything on the player); null when none.</summary>
        public static RuneSoul For(Component anyPart)
        {
            if (anyPart == null)
                return null;
            RuneSoul s = anyPart.GetComponentInParent<RuneSoul>();
            if (s != null)
                return s;
            CombatEntity e = anyPart as CombatEntity;
            if (e == null)
                e = CombatEntity.Resolve(anyPart.gameObject);
            if (e == null)
            {
                PlayerStatusController ps = anyPart.GetComponentInParent<PlayerStatusController>();
                if (ps != null) e = CombatEntity.Resolve(ps.gameObject);
            }
            if (e == null)
            {
                InventoryManager inv = anyPart.GetComponentInParent<InventoryManager>();
                if (inv != null && inv.Player != null) e = CombatEntity.Resolve(inv.Player);
            }
            if (e == null)
                return null;
            s = e.GetComponentInChildren<RuneSoul>(true);
            if (s == null) s = e.GetComponentInParent<RuneSoul>();
            if (s != null)
                return s;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].Entity == e) return all[i];
            return null;
        }

        /// <summary>The soul of the player owning <paramref name="go"/>.</summary>
        public static RuneSoul For(GameObject go) => go != null ? For(go.transform) : null;

        /// <summary>The soul of the player controlled on this machine (null when none).</summary>
        public static RuneSoul Local
        {
            get
            {
                for (int i = 0; i < all.Count; i++)
                {
                    RuneSoul s = all[i];
                    if (s == null) continue;
                    CombatEntity e = s.Entity;
                    GameObject go = e != null ? e.gameObject : s.gameObject;
                    bool local = GameNet.NetAuthority.IsNetworkedObject(go) ? GameNet.NetAuthority.IsLocallyControlled(go) : GameNet.NetAuthority.HasLocalPlayer;
                    if (local) return s;
                }
                return null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        // ------------------------------------------------------------------ state
        /// <summary>Where the rules run for this player (offline, the server): this copy decides.</summary>
        public bool IsAuthority => GameNet.NetAuthority.RunsRules(gameObject);

        /// <summary>The player's character.</summary>
        public CombatEntity Entity
        {
            get
            {
                Look();
                return entity;
            }
        }

        /// <summary>What the UI shows (computed here where the rules run, received from the server on the owner's client).</summary>
        public RuneSoulView View => view;

        /// <summary>Changes whenever <see cref="View"/> changes (the network layer sends it then).</summary>
        public int Version => version;

        /// <summary>Is this player a rune user (Mage)? Only they have a soul resource.</summary>
        public bool IsRuneUser => IsAuthority ? CheckRuneUser() : view.runeUser;

        public float Integrity => view.integrity;
        public float MaxIntegrity => view.maxIntegrity;
        public float DeepDamage => view.deepDamage;
        /// <summary>Highest Soul Integrity ordinary recovery can reach.</summary>
        public float Ceiling => view.Ceiling;
        public float Percent => view.Percent;
        public int Capacity => view.capacity;
        public int UsedSlots => view.usedSlots;
        public int FreeSlots => view.FreeSlots;
        public SoulChannel Channel => IsAuthority ? channel : view.channel;
        public bool IsMeditating => Channel == SoulChannel.Meditation;
        /// <summary>Recovery per minute right now (the view's).</summary>
        public float RecoveryPerMinute => view.recoveryPerMinute;
        /// <summary>The breakdown of <see cref="RecoveryPerMinute"/> (where the rules run).</summary>
        public RecoveryBreakdown RecoveryBreakdown => lastBreakdown;

        /// <summary>The soul's condition (design spec 14.3).</summary>
        public SoulCondition Condition => Balance.Condition(view.conditionId) ?? Balance.ConditionOf(view.integrity, Mathf.Max(1f, view.maxIntegrity));

        /// <summary>The installed network link of the owning client (NetRuneMagic); null offline and where the rules run.</summary>
        public IRuneMagicNet Net { get; set; }

        private static RuneBalance Balance => RuneMagicDatabase.Instance.Catalog.Balance;

        /// <summary>What the soul allows (synthesis, casting) right now. On a client: from the server's view.</summary>
        public SoulRestrictions Restrictions
        {
            get
            {
                if (IsAuthority)
                    return CheckRuneUser() ? SoulRules.Restrictions(Balance, state.soul, ComputeMax()) : SoulRestrictions.None;
                if (!view.runeUser)
                    return SoulRestrictions.None;
                // A client predicts from the server's condition (the server decides again).
                var s = new SoulState { initialized = true, integrity = view.integrity, deepDamage = view.deepDamage, collapseLocked = view.collapseLocked };
                return SoulRules.Restrictions(Balance, s, Mathf.Max(1f, view.maxIntegrity));
            }
        }

        /// <summary>Can runes costing <paramref name="slots"/> soul slots be added?</summary>
        public bool CanFit(int slots) => view.runeUser && CapacityRules.CanFit(view.capacity, view.usedSlots, slots);

        /// <summary>The soul as saved (JSON), where the rules run (tools, debugging).</summary>
        public string ExportState() => state.ToJson(true);

        /// <summary>Replaces the soul with <paramref name="json"/> (tools, debugging). Where the rules run only.</summary>
        public bool ImportState(string json)
        {
            if (!IsAuthority || string.IsNullOrEmpty(json))
                return false;
            ClearDiscovery();
            state = RuneSoulState.FromJson(json);
            SoulRules.Normalize(Balance, state.soul, ComputeMax());
            EnergyRules.ClampAll(Balance, state);
            runesVersion++;
            MarkSaveDirty(true);
            Refresh();
            return true;
        }

        // ------------------------------------------------------------------ lifecycle
        private void Awake()
        {
            manager = GetComponent<SoulIntegrityManager>();
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
            ClearSpelling();
            if (IsAuthority && channel != SoulChannel.None)
                EndChannel("");
            ReleaseChannelLock();
            ReleaseIncapacitation();
            // A client copy lets go of what it held for the server's view.
            if (availability != null && !IsAuthority)
            {
                availability.SetIncapacitated(collapseKey, false);
                availability.SetIncapacitated(channelKey, false);
            }
            replicatedChannelLock = false;
        }

        private void OnDestroy()
        {
            UnhookEntity();
            UnhookAbilities();
            if (tracked)
                RuneSoulSaves.Untrack(this);
            tracked = false;
        }

        private void Look()
        {
            // Everything found: done. Something missing (still being set up, or the player has no such part): looked for
            // again once a second.
            if (looked && entity != null && status != null && sleep != null && xp != null && availability != null)
                return;
            if (looked && Time.unscaledTime < nextLook)
                return;
            looked = true;
            nextLook = Time.unscaledTime + 1f;
            if (entity == null)
            {
                entity = CombatEntity.Resolve(gameObject);
                if (entity == null) entity = GetComponentInParent<CombatEntity>();
                if (entity == null) entity = GetComponentInChildren<CombatEntity>(true);
            }
            if (status == null)
            {
                status = GetComponentInParent<PlayerStatusController>();
                if (status == null) status = GetComponentInChildren<PlayerStatusController>(true);
                if (status == null && entity != null) status = entity.GetComponentInChildren<PlayerStatusController>(true);
            }
            Transform root = entity != null ? entity.transform : transform;
            if (sleep == null) sleep = status != null && status.SleepManager != null ? status.SleepManager : root.GetComponentInChildren<SleepManager>(true);
            if (xp == null) xp = status != null && status.XPManager != null ? status.XPManager : root.GetComponentInChildren<ExperienceManager>(true);
            if (availability == null)
            {
                availability = root.GetComponentInChildren<AvailabilityStateMachine>(true);
                if (availability == null) availability = GetComponentInParent<AvailabilityStateMachine>();
            }
            if (manager == null) manager = GetComponent<SoulIntegrityManager>();
            HookEntity();
        }

        private void HookEntity()
        {
            if (entityHooked || entity == null)
                return;
            entity.Damaged += OnEntityDamaged;
            entity.ControlApplied += OnEntityControlled;
            entity.Revived += RearmGrimoire;
            entityHooked = true;
        }

        private void UnhookEntity()
        {
            if (!entityHooked || entity == null)
                return;
            entity.Damaged -= OnEntityDamaged;
            entity.ControlApplied -= OnEntityControlled;
            entity.Revived -= RearmGrimoire;
            entityHooked = false;
        }

        /// <summary>Re-checks (at most twice a second) whether the player is a rune user; true when they are.</summary>
        private bool CheckRuneUser(bool now = false)
        {
            if (!now && Time.unscaledTime < nextUserCheck)
                return runeUser;
            nextUserCheck = Time.unscaledTime + 0.5f;
            bool was = runeUser;
            runeUser = RuneMagicGate.IsRuneUser(Entity);
            if (runeUser && !tracked && IsAuthority)
            {
                tracked = true;
                RuneSoulSaves.Track(this);
            }
            if (was && !runeUser)
            {
                // No longer a rune user (class changed): the soul stays as it is, dormant (decision D15).
                if (channel != SoulChannel.None) EndChannel("");
                if (discovery != null && discovery.Active) { ClearDiscovery(); RefreshDiscovery(); }
                ReleaseIncapacitation();
                timed.Clear();
            }
            return runeUser;
        }

        private void Update()
        {
            if (!IsAuthority)
            {
                if (manager != null) manager.SetReplica(true);
                TickSpells();
                TickSpelling();
                return;
            }
            if (manager != null) manager.SetReplica(false);
            Look();
            TickSpells();
            TickSpelling();
            if (!CheckRuneUser())
            {
                Refresh();
                return;
            }
            float dt = Time.deltaTime;
            RuneBalance b = Balance;
            float max = ComputeMax();
            SoulRules.Normalize(b, state.soul, max);
            ProcessAbsorbQueue();
            TickCollapse();
            TickChannel(b);
            TickDiscovery(b);
            TickGrimoire();
            TickRecovery(b, max, dt);
            TickTimed(b, max, dt);
            if (saveDirty && Time.unscaledTime - lastSaveMark >= 1f)
                MarkSaveDirty(true);
            Refresh();
        }

        // ------------------------------------------------------------------ damage and healing (where the rules run)
        /// <summary>
        /// Damages the soul (synthesis, casting, the Spelling System, absorption... design spec 14.1). <paramref name="kindId"/>
        /// names a Soul Damage Kind of the database (an unknown or empty kind is ordinary damage); the caller scales
        /// <paramref name="amount"/> with complexity and risk. Does nothing for non rune users, or on a network client.
        /// </summary>
        public SoulDamageResult Damage(float amount, string kindId = "", string source = null)
        {
            if (!IsAuthority || !CheckRuneUser(true))
                return default;
            RuneBalance b = Balance;
            float max = ComputeMax();
            float loss = manager != null ? manager.DecrementFactor : 0f;
            SoulDamageResult r = SoulRules.Damage(b, state.soul, max, amount, kindId, loss, 0f, DateTime.UtcNow.Ticks);
            if (r.applied > 0f || r.deepAdded > 0f)
                MarkSaveDirty(r.collapsedNow);
            if (logEvents && r.scaled > 0f)
                Debug.Log($"[Rune Magic] {name}: soul damage {r.applied:0.##} ({(string.IsNullOrEmpty(kindId) ? "ordinary" : kindId)}" +
                          $"{(string.IsNullOrEmpty(source) ? "" : ", " + source)}), deep +{r.deepAdded:0.##} → {state.soul.integrity:0.##}/{max:0.#}.", this);
            if (r.collapsedNow)
                OnCollapse(b);
            Damaged?.Invoke(this, r);
            Refresh();
            return r;
        }

        /// <summary>Ordinary recovery (never above the ceiling deep damage leaves). The gain factor applies. Returns what was restored.</summary>
        public float Restore(float amount, string source = null)
        {
            if (!IsAuthority || !CheckRuneUser(true) || !(amount > 0f))
                return 0f;
            float gain = manager != null ? Mathf.Max(0f, 1f + manager.IncrementFactor / 100f) : 1f;
            float restored = SoulRules.Restore(Balance, state.soul, ComputeMax(), amount * gain);
            if (restored > 0f) MarkSaveDirty(false);
            Refresh();
            return restored;
        }

        /// <summary>Specialized healing: removes deep damage (items flagged for it, rituals). Returns what was removed.</summary>
        public float HealDeep(float amount)
        {
            if (!IsAuthority || !CheckRuneUser(true) || !(amount > 0f))
                return 0f;
            float healed = SoulRules.HealDeep(Balance, state.soul, ComputeMax(), amount);
            if (healed > 0f) MarkSaveDirty(false);
            Refresh();
            return healed;
        }

        /// <summary>Recovers <paramref name="total"/> (and removes <paramref name="deepTotal"/> deep damage) over <paramref name="seconds"/>.</summary>
        public void AddTimedRestore(float total, float seconds, float deepTotal = 0f)
        {
            if (!IsAuthority || !CheckRuneUser(true) || seconds <= 0f || (!(total > 0f) && !(deepTotal > 0f)))
                return;
            timed.Add(new TimedRestore
            {
                perSecond = Mathf.Max(0f, total) / seconds,
                deepPerSecond = Mathf.Max(0f, deepTotal) / seconds,
                secondsLeft = seconds,
            });
        }

        /// <summary>Restores the soul completely, deep damage too (tools, debugging).</summary>
        public void RestoreFully()
        {
            if (!IsAuthority || !CheckRuneUser(true))
                return;
            SoulRules.RestoreFully(Balance, state.soul, ComputeMax());
            MarkSaveDirty(true);
            Refresh();
        }

        /// <summary>
        /// A soul item was used: removes its deep damage first (so the restore can reach higher), then restores, at once or
        /// over its duration. <paramref name="scale"/> is the strength of the copy used. False for non rune users.
        /// </summary>
        public bool UseRestorative(SoulRestorativeSO item, float scale = 1f)
        {
            if (item == null || !IsAuthority || !CheckRuneUser(true))
                return false;
            scale = Mathf.Max(0f, scale);
            float max = ComputeMax();
            float amount = (item.restoreAmount + item.restorePercent / 100f * max) * scale;
            float deep = item.deepHealAmount * scale;
            if (item.overSeconds > 0f)
            {
                AddTimedRestore(amount, item.overSeconds, deep);
                Say($"{item.Name}: your soul begins to mend.");
                return true;
            }
            float healed = HealDeep(deep);
            float restored = Restore(amount, item.Name);
            Say(healed > 0f
                ? $"{item.Name}: Soul Integrity +{restored:0.#}, deep damage −{healed:0.#}."
                : restored > 0f ? $"{item.Name}: Soul Integrity +{restored:0.#}." : $"{item.Name}: your soul has nothing to mend.");
            return true;
        }

        // ------------------------------------------------------------------ capacity and maximum
        /// <summary>A lasting (saved) bonus to the Soul Rune Capacity from <paramref name="source"/> (a breakthrough, a reward); 0 removes it.</summary>
        public void SetCapacityBonus(string source, int slots)
        {
            if (!IsAuthority || !CapacityRules.SetBonus(state.soul, source, slots))
                return;
            MarkSaveDirty(true);
            Refresh();
        }

        /// <summary>A lasting (saved) bonus to the maximum Soul Integrity from <paramref name="source"/>; 0 removes it.</summary>
        public void SetMaxIntegrityBonus(string source, float amount)
        {
            if (!IsAuthority || !SoulRules.SetMaxBonus(state.soul, source, amount))
                return;
            SoulRules.Normalize(Balance, state.soul, ComputeMax());
            MarkSaveDirty(true);
            Refresh();
        }

        /// <summary>The lasting capacity bonus of <paramref name="source"/> (0 when none).</summary>
        public int CapacityBonusOf(string source)
        {
            CapacityBonus x = state.soul.capacityBonuses?.Find(c => c != null && c.source == source);
            return x != null ? x.slots : 0;
        }

        /// <summary>
        /// A bonus to the capacity while something lasts (equipment, a passive ability): the same <paramref name="key"/>
        /// replaces its value. Not saved: its owner sets it again.
        /// </summary>
        public void SetCapacityModifier(object key, int slots)
        {
            if (key == null) return;
            if (slots == 0) capacityModifiers.Remove(key);
            else capacityModifiers[key] = slots;
            Refresh();
        }

        public void ClearCapacityModifier(object key)
        {
            if (key != null && capacityModifiers.Remove(key)) Refresh();
        }

        /// <summary>A bonus to the maximum Soul Integrity while something lasts (not saved).</summary>
        public void SetMaxIntegrityModifier(object key, float amount)
        {
            if (key == null) return;
            if (Mathf.Approximately(amount, 0f)) maxModifiers.Remove(key);
            else maxModifiers[key] = amount;
            if (IsAuthority) SoulRules.Normalize(Balance, state.soul, ComputeMax());
            Refresh();
        }

        public void ClearMaxIntegrityModifier(object key)
        {
            if (key == null || !maxModifiers.Remove(key)) return;
            if (IsAuthority) SoulRules.Normalize(Balance, state.soul, ComputeMax());
            Refresh();
        }

        /// <summary>
        /// Abilities and traits that improve Soul Regeneration (design spec 15.1): <paramref name="multiplier"/> scales every
        /// recovery (1 = none, 1.5 = 50% faster), <paramref name="flatPerMinute"/> adds recovery. One value per key.
        /// </summary>
        public void SetRecoveryModifier(object key, float multiplier, float flatPerMinute = 0f)
        {
            if (key == null) return;
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier)) multiplier = 1f;
            if (float.IsNaN(flatPerMinute) || float.IsInfinity(flatPerMinute)) flatPerMinute = 0f;
            if (Mathf.Approximately(multiplier, 1f) && Mathf.Approximately(flatPerMinute, 0f))
                recoveryModifiers.Remove(key);
            else
                recoveryModifiers[key] = new RecoveryModifier { multiplier = Mathf.Max(0f, multiplier), flatPerMinute = flatPerMinute };
        }

        public void ClearRecoveryModifier(object key)
        {
            if (key != null) recoveryModifiers.Remove(key);
        }

        private float ComputeMax()
        {
            float bonus = 0f;
            foreach (float v in maxModifiers.Values) bonus += v;
            return SoulRules.MaxIntegrity(Balance, state.soul, bonus);
        }

        private int Level => xp != null ? Mathf.Max(1, xp.CurrentLevel) : 1;

        private int ComputeCapacity(RuneBalance b)
        {
            int bonus = 0;
            foreach (int v in capacityModifiers.Values) bonus += v;
            return CapacityRules.Capacity(b, Level, state.soul, bonus);
        }

        /// <summary>The soul slots used by the runes held (the sum of their slot costs).</summary>
        private int ComputeUsedSlots() => RuneRules.UsedSlots(RuneMagicDatabase.Instance.Catalog, state);

        // ------------------------------------------------------------------ ticks
        private void TickRecovery(RuneBalance b, float max, float dt)
        {
            RecoveryContext c = BuildContext(b, out SoulSanctuary sanctuary);
            lastRate = RecoveryRules.PerMinute(b.soulRecovery, c, out lastBreakdown);
            if (lastRate > 0f && dt > 0f)
            {
                float restored = SoulRules.Restore(b, state.soul, max, lastRate * dt / 60f);
                if (restored > 0f) MarkSaveDirty(false);
            }
            if (sanctuary != null && c.alive && !c.inCombat && sanctuary.DeepHealPerMinute > 0f && dt > 0f &&
                SoulRules.HealDeep(b, state.soul, max, sanctuary.DeepHealPerMinute * dt / 60f) > 0f)
                MarkSaveDirty(false);
        }

        private RecoveryContext BuildContext(RuneBalance b, out SoulSanctuary sanctuary)
        {
            SoulRecoveryBalance r = b.soulRecovery;
            CombatEntity e = Entity;
            RecoveryContext c = RecoveryContext.Default;
            c.alive = e == null || (!e.IsDead && !e.IsUnconscious);
            c.inCombat = e != null && CombatActivity.InCombat(e, r != null ? r.combatWindowSeconds : 6f);
            c.sleeping = sleep != null && sleep.IsSleeping;
            SleepQuality q = sleep != null ? sleep.CurrentQuality : null;
            c.sleepQuality = q != null ? q.recoveryMultiplier : 1f;
            c.meditating = channel == SoulChannel.Meditation;
            c.meditationSeconds = c.meditating ? Time.time - channelStart : 0f;
            Vector3 p = e != null ? e.transform.position : transform.position;
            sanctuary = SoulSanctuary.Find(p);
            if (sanctuary != null)
            {
                c.inSanctuary = true;
                c.sanctuaryMultiplier = sanctuary.MultiplierFor(r);
                c.sanctuaryPassivePerMinute = sanctuary.PassivePerMinuteFor(r);
            }
            HubWorld.PlayerRealmState realm = HubWorld.PlayerRealmState.Of(e);
            c.inCity = realm != null && realm.InCity;
            float m = 1f, flat = 0f;
            foreach (RecoveryModifier mod in recoveryModifiers.Values)
            {
                m *= mod.multiplier;
                flat += mod.flatPerMinute;
            }
            c.modifierMultiplier = m;
            c.modifierFlatPerMinute = flat;
            c.gainFactorPercent = manager != null ? manager.IncrementFactor : 0f;
            return c;
        }

        private void TickTimed(RuneBalance b, float max, float dt)
        {
            timedPerMinute = 0f;
            if (timed.Count == 0 || dt <= 0f)
                return;
            float gain = manager != null ? Mathf.Max(0f, 1f + manager.IncrementFactor / 100f) : 1f;
            for (int i = timed.Count - 1; i >= 0; i--)
            {
                TimedRestore t = timed[i];
                float step = Mathf.Min(dt, t.secondsLeft);
                if (t.deepPerSecond > 0f) SoulRules.HealDeep(b, state.soul, max, t.deepPerSecond * step);
                if (t.perSecond > 0f) SoulRules.Restore(b, state.soul, max, t.perSecond * step * gain);
                timedPerMinute += t.perSecond * 60f * gain;
                t.secondsLeft -= step;
                if (t.secondsLeft <= 0f) timed.RemoveAt(i);
            }
            MarkSaveDirty(false);
        }

        private void TickCollapse()
        {
            if (collapseHeld && Time.time >= incapacitatedUntil)
                ReleaseIncapacitation();
        }

        private void OnCollapse(RuneBalance b)
        {
            EndChannel("");
            timed.Clear();
            SoulCollapseRules rules = b.soulCollapse;
            float seconds = rules != null ? rules.incapacitateSeconds : 0f;
            if (seconds > 0f)
            {
                incapacitatedUntil = Time.time + seconds;
                Look();
                if (availability != null)
                {
                    availability.SetIncapacitated(collapseKey, true);
                    collapseHeld = true;
                }
            }
            string lost = "";
            if (rules != null && rules.losePendingEnergy && state.pendingEnergy > 0f)
            {
                lost = $" {state.pendingEnergy:N0} magical energy waiting for a rune is lost.";
                state.pendingEnergy = 0f;
            }
            Say("Your soul collapses! You cannot cast or synthesize until it recovers." + lost);
            if (logEvents)
                Debug.Log($"[Rune Magic] {name}: Soul Collapse ({state.soul.collapses} so far).", this);
            Collapsed?.Invoke(this);
        }

        private void ReleaseIncapacitation()
        {
            if (collapseHeld && availability != null)
                availability.SetIncapacitated(collapseKey, false);
            collapseHeld = false;
            incapacitatedUntil = 0f;
        }

        // ------------------------------------------------------------------ view
        /// <summary>Recomputes the view (where the rules run) and raises <see cref="Changed"/> when it changed.</summary>
        private void Refresh()
        {
            if (!IsAuthority)
                return;
            BuildView(scratch);
            if (!scratch.SameAs(view))
            {
                view.CopyFrom(scratch);
                version++;
                Changed?.Invoke(this);
            }
            if (view.runeUser)
            {
                string c = view.conditionId;
                if (lastCondition != null && c != lastCondition)
                    ConditionChanged?.Invoke(this, lastCondition, c);
                lastCondition = c;
            }
            if (manager != null) manager.Mirror(view.integrity, Mathf.Max(1f, view.maxIntegrity));
        }

        private void BuildView(RuneSoulView v)
        {
            v.runeUser = runeUser;
            v.kept = tracked && RuneSoulSaves.IsKept(this);
            v.message = message;
            v.messageSeq = messageSeq;
            if (!runeUser)
            {
                v.integrity = v.maxIntegrity = v.deepDamage = 0f;
                v.conditionId = v.conditionName = v.castingDebuff = v.capacityStage = v.channelName = v.recoverySources = "";
                v.collapseLocked = false;
                v.synthesisAllowed = v.castingAllowed = false;
                v.capacity = v.usedSlots = v.nextStageLevel = v.nextStageCapacity = 0;
                v.channel = SoulChannel.None;
                v.channelProgress = v.recoveryPerMinute = v.incapacitatedFor = v.lockUntilPercent = v.pendingEnergy = 0f;
                v.runesVersion = runesVersion;
                return;
            }
            RuneBalance b = Balance;
            float max = ComputeMax();
            SoulState s = state.soul;
            v.integrity = s.integrity;
            v.maxIntegrity = max;
            v.deepDamage = s.deepDamage;
            SoulRestrictions r = SoulRules.Restrictions(b, s, max);
            v.conditionId = r.conditionId ?? "";
            v.conditionName = r.conditionName ?? "";
            v.castingDebuff = r.castingDebuff ?? "";
            v.collapseLocked = s.collapseLocked;
            v.lockUntilPercent = b.soulCollapse != null ? b.soulCollapse.lockUntilPercent : 0f;
            v.synthesisAllowed = r.synthesisAllowed;
            v.castingAllowed = r.castingAllowed;

            v.capacity = ComputeCapacity(b);
            v.usedSlots = ComputeUsedSlots();
            CapacityStage stage = b.StageAt(Level);
            v.capacityStage = stage != null ? stage.name ?? "" : "";
            CapacityStage next = CapacityRules.NextStage(b, Level);
            v.nextStageLevel = next != null ? next.fromLevel : 0;
            v.nextStageCapacity = next != null ? b.CapacityAt(next.fromLevel, v.capacity - (stage != null ? stage.capacity : 0)) : 0;

            v.channel = channel;
            v.channelName = channel != SoulChannel.None ? channelName ?? "" : "";
            v.channelProgress = channel == SoulChannel.Meditation ? RecoveryRules.MeditationSettled(b.soulRecovery, Time.time - channelStart)
                : channel == SoulChannel.Ritual ? (ritualSeconds > 0f ? Mathf.Clamp01((Time.time - channelStart) / ritualSeconds) : 1f) : 0f;
            v.recoveryPerMinute = lastRate + timedPerMinute;
            v.recoverySources = timedPerMinute > 0f ? (lastRate > 0f ? lastBreakdown.Describe() + " + items" : "items") : lastBreakdown.Describe();
            v.incapacitatedFor = collapseHeld ? Mathf.Max(0f, incapacitatedUntil - Time.time) : 0f;
            v.pendingEnergy = state.pendingEnergy;
            v.runesVersion = runesVersion;
        }

        /// <summary>A view from the server (owner's client, network layer).</summary>
        public void ApplyReplicatedView(RuneSoulView v)
        {
            if (v == null || IsAuthority)
                return;
            Look();
            bool lockedBefore = view.incapacitatedFor > 0f;
            view.CopyFrom(v);
            version++;
            if (manager != null)
            {
                manager.SetReplica(true);
                manager.ApplyReplicated(view.integrity, Mathf.Max(1f, view.maxIntegrity));
            }
            // The owner's own copy is held still like the server's (movement is predicted here).
            bool locked = view.incapacitatedFor > 0f;
            if (availability != null && locked != lockedBefore)
                availability.SetIncapacitated(collapseKey, locked);
            ApplyReplicatedChannelLock();
            Changed?.Invoke(this);
        }

        // ------------------------------------------------------------------ messages and saves
        /// <summary>Tells the player something (shown by the meditation screen; sent to the owner with the view).</summary>
        public void Say(string text)
        {
            if (string.IsNullOrEmpty(text) || !IsAuthority)
                return;
            message = text;
            messageSeq++;
            Refresh();
        }

        private void MarkSaveDirty(bool now)
        {
            saveDirty = true;
            if (!tracked || (!now && Time.unscaledTime - lastSaveMark < 1f))
                return;
            saveDirty = false;
            lastSaveMark = Time.unscaledTime;
            RuneSoulSaves.Changed(this);
        }

        /// <summary>The state to save (saves only).</summary>
        internal RuneSoulState StateForSave => state;

        /// <summary>The saved soul came back (saves only): it replaces the provisional one.</summary>
        internal void RestoreFromSave(RuneSoulState saved)
        {
            if (saved != null)
                state = saved.Clone();
            state.EnsureLists();
            SoulRules.Normalize(Balance, state.soul, ComputeMax());
            EnergyRules.ClampAll(Balance, state);
            runesVersion++;
            if (logEvents)
                Debug.Log($"[Rune Magic] {name}: soul {(saved != null ? "restored" : "new")} ({state.soul.integrity:0.#}, deep {state.soul.deepDamage:0.#}).", this);
            Refresh();
        }

        internal bool LogsEvents => logEvents;
    }
}
