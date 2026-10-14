using System;
using System.Collections.Generic;
using UnityEngine;

namespace HubWorld
{
    /// <summary>
    /// Duels: a player challenges another (the Interact key on them, or <see cref="Challenge"/>), the other accepts or
    /// declines, a short countdown, then combat is on between the two - and only them - even in the city, where nothing
    /// else can be harmed. A temporary arena can surround them; leaving it loses the duel. Duels never kill: the loser
    /// is saved at a sliver of health. Rules are in <see cref="DuelBook"/>; this is the scene side (and the authority).
    /// Added next to the <see cref="HubWorldDirector"/> automatically.
    /// </summary>
    /// <remarks>
    /// In a network session the server's duel system is the authority. On a client the book is a copy of the server's
    /// (<see cref="ApplyReplicated"/>, fed by <c>NetDuels</c>): the combat rules, the HUD and the challenge prompts read
    /// it, the arena ring is shown from it, and requests (<see cref="Challenge"/>, <see cref="Accept"/>,
    /// <see cref="Decline"/>, <see cref="Forfeit"/>) are checked locally for a quick answer, then sent to the server
    /// through <see cref="ClientRequest"/>. Nothing on a client ends, heals or saves anyone.
    /// </remarks>
    [DisallowMultipleComponent]
    public class DuelSystem : MonoBehaviour, ICombatRuleProvider
    {
        /// <summary>What a network client asks the server's duel system to do.</summary>
        public enum DuelRequest : byte
        {
            Challenge,
            Accept,
            Decline,
            Forfeit,
        }

        [Tooltip("Testing: challenges sent to players that are not this machine's player are accepted at once (lets one " +
                 "person duel a second player object in single player).")]
        public bool autoAcceptForOtherPlayers = false;

        public static DuelSystem Instance { get; private set; }

        /// <summary>A duel changed state (also its end; see <see cref="Duel.Reason"/>).</summary>
        public event Action<Duel> DuelChanged;

        /// <summary>
        /// Network clients: sends a request to the server (request, the asking player, the challenged player for
        /// <see cref="DuelRequest.Challenge"/>). Installed by the networking layer; null offline.
        /// </summary>
        public static Action<DuelRequest, CombatEntity, CombatEntity> ClientRequest;

        /// <summary>Optional: a player's name as every machine knows it (the networking layer's display name). Null or "" = the usual name.</summary>
        public static Func<CombatEntity, string> NameResolver;

        /// <summary>A network client mirrors the server's duels (read when acting).</summary>
        private static bool IsMirror => GameNet.NetAuthority.IsClientOnly;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ClientRequest = null;
            NameResolver = null;
        }

        private sealed class Running
        {
            public Duel duel;
            public CombatEntity a, b;
            public Vector3 center;
            public DuelArena arena;
            public Func<DamageInfo, bool> lethalA, lethalB;
            public Action<CombatEntity> diedA, diedB;
        }

        private readonly DuelBook book = new DuelBook();
        private readonly Dictionary<int, Running> running = new Dictionary<int, Running>();
        private readonly Dictionary<int, DuelSnapshot> mirrored = new Dictionary<int, DuelSnapshot>();
        private HubWorldDirector director;
        private DuelSettings settings;
        private float nextAttach;

        public int Priority => 100;
        public IReadOnlyList<Duel> OpenDuels => book.Open;

        private double Now => director != null ? director.Now : Time.timeAsDouble;

        private void Awake()
        {
            Instance = this;
            director = GetComponent<HubWorldDirector>();
            settings = director != null ? director.Settings.duels : HubWorldSettings.Instance.duels;
            book.ChallengeTimeout = settings.challengeTimeout;
            book.Countdown = settings.countdown;
            book.MaxDuration = settings.maxDuration;
            book.Changed += OnBookChanged;
            CombatRules.Register(this);
            GameNet.NetAuthority.ModeChanged += OnNetModeChanged;
        }

        private void OnDestroy()
        {
            GameNet.NetAuthority.ModeChanged -= OnNetModeChanged;
            CombatRules.Unregister(this);
            foreach (Running r in running.Values)
                Cleanup(r);
            running.Clear();
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!settings.enabled)
                return;
            if (Time.unscaledTime >= nextAttach)
            {
                nextAttach = Time.unscaledTime + 1f;
                AttachTargets();
            }
            if (IsMirror)
                return; // the server advances the book and watches the fighters
            book.Update(Now);
            foreach (Running r in new List<Running>(running.Values))
                Watch(r);
        }

        /// <summary>Joining or leaving a network session: duels of the other side (local ones, or a server's copy) are dropped.</summary>
        private void OnNetModeChanged(GameNet.NetMode mode)
        {
            foreach (Running r in running.Values)
                Cleanup(r);
            running.Clear();
            mirrored.Clear();
            book.Clear();
        }

        // ------------------------------------------------------------------ ids

        private static int Id(CombatEntity e) => PlayerIds.Of(e);

        private static CombatEntity Player(int id) => PlayerIds.Find(id);

        /// <summary>The open duel or challenge of <paramref name="player"/>, or null.</summary>
        public Duel DuelOf(CombatEntity player) => player != null ? book.Of(Id(player)) : null;

        /// <summary>The player behind an id of a <see cref="Duel"/>.</summary>
        public CombatEntity PlayerOf(int id) => Player(id);

        /// <summary>Are the two fighting each other in a duel right now?</summary>
        public bool AreFighting(CombatEntity a, CombatEntity b) => a != null && b != null && book.AreFighting(Id(a), Id(b));

        // ------------------------------------------------------------------ requests

        /// <summary>Could <paramref name="challenger"/> challenge <paramref name="target"/> now? (Reason when not.)</summary>
        public bool CanChallenge(CombatEntity challenger, CombatEntity target, out string reason)
        {
            reason = null;
            if (!settings.enabled)
            {
                reason = "Duels are disabled.";
                return false;
            }
            if (challenger == null || target == null || challenger == target ||
                challenger.Kind != CombatEntity.EntityKind.Player || target.Kind != CombatEntity.EntityKind.Player)
            {
                reason = "Only another player can be challenged.";
                return false;
            }
            if (challenger.IsDead || target.IsDead)
            {
                reason = "Not now.";
                return false;
            }
            PlayerRealmState sa = PlayerRealmState.Of(challenger), sb = PlayerRealmState.Of(target);
            RealmKind realm = sa != null ? sa.Realm : RealmKind.None;
            if (sb != null && sb.Realm != realm)
            {
                reason = "They are too far away.";
                return false;
            }
            if (realm == RealmKind.Transit || realm == RealmKind.Dungeon || (!settings.allowOutsideCity && realm != RealmKind.City && realm != RealmKind.None))
            {
                reason = settings.allowOutsideCity ? "No duels here." : "Duels are fought in the city.";
                return false;
            }
            if (Vector3.Distance(challenger.Position, target.Position) > settings.challengeRange)
            {
                reason = "Too far to challenge.";
                return false;
            }
            if (book.Of(Id(challenger)) != null)
            {
                reason = "You are already in a duel or waiting for an answer.";
                return false;
            }
            if (book.Of(Id(target)) != null)
            {
                reason = "They are already in a duel or deciding on one.";
                return false;
            }
            return true;
        }

        /// <summary><paramref name="challenger"/> challenges <paramref name="target"/>. False with a reason when they cannot.</summary>
        public bool Challenge(CombatEntity challenger, CombatEntity target, out string reason)
        {
            if (!CanChallenge(challenger, target, out reason))
                return false;
            if (IsMirror)
                return SendRequest(DuelRequest.Challenge, challenger, target, out reason);
            Duel d = book.Challenge(Id(challenger), Id(target), Now, out reason);
            if (d == null)
                return false;
            if (autoAcceptForOtherPlayers && !GameNet.NetAuthority.IsNetworked && PlayerLocator.Local != null && target != PlayerLocator.Local)
                book.Accept(d.Challenged, Now);
            return true;
        }

        /// <summary><paramref name="player"/> accepts the challenge addressed to them.</summary>
        public bool Accept(CombatEntity player)
        {
            Duel d = DuelOf(player);
            if (d == null || d.State != DuelState.Challenged || d.Challenged != Id(player))
                return false;
            if (IsMirror)
                return SendRequest(DuelRequest.Accept, player, null, out _);
            CombatEntity other = Player(d.Challenger);
            if (other == null || !CanStillFight(player, other))
            {
                book.End(d.Challenged, DuelEndReason.Cancelled, false);
                return false;
            }
            return book.Accept(d.Challenged, Now);
        }

        /// <summary><paramref name="player"/> declines the challenge addressed to them (or withdraws their own).</summary>
        public bool Decline(CombatEntity player)
        {
            if (player == null)
                return false;
            if (IsMirror)
            {
                Duel d = DuelOf(player);
                return d != null && d.State == DuelState.Challenged && SendRequest(DuelRequest.Decline, player, null, out _);
            }
            return book.Decline(Id(player));
        }

        /// <summary><paramref name="player"/> gives up their duel (their opponent wins).</summary>
        public bool Forfeit(CombatEntity player)
        {
            Duel d = DuelOf(player);
            if (d == null)
                return false;
            if (IsMirror)
                return SendRequest(DuelRequest.Forfeit, player, null, out _);
            if (d.State == DuelState.Challenged)
                return book.Decline(Id(player));
            return book.End(Id(player), DuelEndReason.Forfeit, true);
        }

        private static bool SendRequest(DuelRequest request, CombatEntity player, CombatEntity target, out string reason)
        {
            reason = null;
            if (ClientRequest == null)
            {
                reason = "Not connected to the server.";
                return false;
            }
            ClientRequest(request, player, target);
            return true; // sent: the server answers with the duel's new state and its messages
        }

        private bool CanStillFight(CombatEntity a, CombatEntity b)
        {
            if (a == null || b == null || !a.IsAlive || !b.IsAlive)
                return false;
            PlayerRealmState sa = PlayerRealmState.Of(a), sb = PlayerRealmState.Of(b);
            RealmKind ra = sa != null ? sa.Realm : RealmKind.None, rb = sb != null ? sb.Realm : RealmKind.None;
            return ra == rb && ra != RealmKind.Transit && ra != RealmKind.Dungeon;
        }

        // ------------------------------------------------------------------ combat rules

        public PvpVerdict PlayerVersusPlayer(CombatEntity a, CombatEntity b) =>
            book.AreFighting(Id(a), Id(b)) ? PvpVerdict.Opponents : PvpVerdict.Unspecified;

        /// <summary>Fighters use their abilities for real, even in the city (the rules still keep everyone else safe).</summary>
        public bool? AbilitiesVisualOnly(CombatEntity caster)
        {
            CombatEntity p = CombatRules.ControllingPlayer(caster);
            Duel d = p != null ? book.Of(Id(p)) : null;
            return d != null && d.State == DuelState.Fighting ? false : (bool?)null;
        }

        // ------------------------------------------------------------------ state changes

        private void OnBookChanged(Duel d)
        {
            if (IsMirror)
            {
                OnMirroredChange(d);
                DuelChanged?.Invoke(d);
                return;
            }
            CombatEntity a = Player(d.Challenger), b = Player(d.Challenged);
            string an = Name(a), bn = Name(b);
            switch (d.State)
            {
                case DuelState.Challenged:
                    HubWorldMessages.Send(a, $"You challenged {bn} to a duel.", HubMessageKind.Info);
                    HubWorldMessages.Send(b, $"{an} challenges you to a duel!", HubMessageKind.Warning);
                    break;
                case DuelState.Countdown:
                    BeginArena(d, a, b);
                    HubWorldMessages.Send(a, $"{bn} accepted the duel!", HubMessageKind.Info);
                    HubWorldMessages.Send(b, $"Duel accepted - get ready!", HubMessageKind.Info);
                    break;
                case DuelState.Fighting:
                    StartFight(d, a, b);
                    HubWorldMessages.Send(a, "Fight!", HubMessageKind.Banner);
                    HubWorldMessages.Send(b, "Fight!", HubMessageKind.Banner);
                    break;
                case DuelState.Ended:
                    EndDuel(d, a, b);
                    break;
            }
            DuelChanged?.Invoke(d);
        }

        private void BeginArena(Duel d, CombatEntity a, CombatEntity b)
        {
            var r = new Running { duel = d, a = a, b = b };
            r.center = a != null && b != null ? (a.Position + b.Position) * 0.5f : a != null ? a.Position : Vector3.zero;
            if (settings.arenaRadius > 0f && settings.showArena)
                r.arena = DuelArena.Create(r.center, settings.arenaRadius, a != null ? a.gameObject.scene : gameObject.scene);
            running[d.Id] = r;
        }

        private void StartFight(Duel d, CombatEntity a, CombatEntity b)
        {
            if (!running.TryGetValue(d.Id, out Running r))
            {
                BeginArena(d, a, b);
                r = running[d.Id];
            }
            if (a != null)
            {
                r.lethalA = info => SaveLoser(d, a, info);
                r.diedA = killer => OnFighterDied(d, a);
                a.LethalDamage += r.lethalA;
                a.Died += r.diedA;
            }
            if (b != null)
            {
                r.lethalB = info => SaveLoser(d, b, info);
                r.diedB = killer => OnFighterDied(d, b);
                b.LethalDamage += r.lethalB;
                b.Died += r.diedB;
            }
            if (settings.resetCooldownsAtStart)
            {
                if (a != null && a.Caster != null) a.Caster.ResetAllCooldowns();
                if (b != null && b.Caster != null) b.Caster.ResetAllCooldowns();
            }
        }

        /// <summary>A fighter would die from the opponent's hit: they are saved at a sliver of health and lose.</summary>
        private bool SaveLoser(Duel d, CombatEntity loser, DamageInfo info)
        {
            if (d.State != DuelState.Fighting || loser.Health == null)
                return false;
            CombatEntity from = CombatRules.ControllingPlayer(info.source);
            if (from == null || Id(from) != d.OpponentOf(Id(loser)))
                return false; // a mob or a trap: a real death (the duel is called off)
            float keep = Mathf.Max(1f, loser.MaxHealth * settings.loserHealth);
            loser.Health.AddCurrentValue(keep - loser.Health.CurrentValue);
            loser.SetInvulnerable(2f);
            book.End(Id(loser), DuelEndReason.Defeat, true);
            return true;
        }

        private void OnFighterDied(Duel d, CombatEntity who)
        {
            if (d.State != DuelState.Ended)
                book.End(Id(who), DuelEndReason.Cancelled, false);
        }

        private void EndDuel(Duel d, CombatEntity a, CombatEntity b)
        {
            if (running.TryGetValue(d.Id, out Running r))
            {
                Cleanup(r);
                running.Remove(d.Id);
            }
            bool fought = d.Reason == DuelEndReason.Defeat || d.Reason == DuelEndReason.Forfeit || d.Reason == DuelEndReason.LeftArena || d.Reason == DuelEndReason.TimeOut;
            if (fought && settings.healAfterDuel)
            {
                foreach (CombatEntity e in new[] { a, b })
                {
                    if (e == null || !e.IsAlive)
                        continue;
                    e.ApplyHeal(e.MaxHealth);
                    e.ClearControl();
                    e.SetInvulnerable(2f);
                }
            }
            CombatEntity winner = Player(d.Winner);
            string text;
            switch (d.Reason)
            {
                case DuelEndReason.Declined: text = $"{Name(b)} declined the duel."; break;
                case DuelEndReason.Expired: text = "The duel challenge expired."; break;
                case DuelEndReason.TimeOut: text = "The duel ended in a draw."; break;
                case DuelEndReason.Cancelled: text = "The duel was called off."; break;
                case DuelEndReason.LeftArena: text = winner != null ? $"{Name(winner)} wins - their opponent left the arena." : "The duel is over."; break;
                case DuelEndReason.Forfeit: text = winner != null ? $"{Name(winner)} wins - their opponent gave up." : "The duel is over."; break;
                default: text = winner != null ? $"{Name(winner)} wins the duel!" : "The duel is over."; break;
            }
            HubMessageKind kind = fought ? HubMessageKind.Banner : HubMessageKind.Info;
            HubWorldMessages.Send(a, text, kind);
            HubWorldMessages.Send(b, text, kind);
        }

        private void Cleanup(Running r)
        {
            if (r.a != null)
            {
                if (r.lethalA != null) r.a.LethalDamage -= r.lethalA;
                if (r.diedA != null) r.a.Died -= r.diedA;
            }
            if (r.b != null)
            {
                if (r.lethalB != null) r.b.LethalDamage -= r.lethalB;
                if (r.diedB != null) r.b.Died -= r.diedB;
            }
            if (r.arena != null)
                Destroy(r.arena.gameObject);
        }

        /// <summary>Calls off duels whose fighters travelled, vanished or left the arena.</summary>
        private void Watch(Running r)
        {
            Duel d = r.duel;
            if (d.State == DuelState.Ended)
                return;
            if (r.a == null || r.b == null || !CanStillFight(r.a, r.b))
            {
                CombatEntity gone = r.a == null || !r.a.IsAlive ? r.a : r.b;
                book.End(gone != null ? Id(gone) : d.Challenger, DuelEndReason.Cancelled, false);
                return;
            }
            if (d.State != DuelState.Fighting || !settings.leavingArenaLoses || settings.arenaRadius <= 0f)
                return;
            foreach (CombatEntity e in new[] { r.a, r.b })
            {
                Vector3 off = e.Position - r.center;
                off.y = 0f;
                if (off.magnitude > settings.arenaRadius + 0.5f)
                {
                    book.End(Id(e), DuelEndReason.LeftArena, true);
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ network copies

        /// <summary>
        /// Server side: the open duels as snapshots (with their arena), for the network layer to send to the clients.
        /// Deadlines are in this machine's <see cref="HubWorldDirector.Now"/>.
        /// </summary>
        public void Snapshot(List<DuelSnapshot> into)
        {
            if (into == null)
                return;
            into.Clear();
            IReadOnlyList<Duel> open = book.Open;
            for (int i = 0; i < open.Count; i++)
            {
                DuelSnapshot v = DuelSnapshot.Of(open[i]);
                if (running.TryGetValue(v.id, out Running r))
                {
                    v.arena = r.arena != null;
                    v.center = r.center;
                }
                into.Add(v);
            }
        }

        /// <summary>
        /// Network clients: replaces the local book with the server's open duels (deadlines already converted to this
        /// machine's <see cref="HubWorldDirector.Now"/>). Shows and removes the arena rings; raises <see cref="DuelChanged"/>.
        /// Ignored where the duel rules run (offline, on the server).
        /// </summary>
        public void ApplyReplicated(IReadOnlyList<DuelSnapshot> open)
        {
            if (!IsMirror)
                return;
            mirrored.Clear();
            if (open != null)
                for (int i = 0; i < open.Count; i++)
                    mirrored[open[i].id] = open[i];
            book.ApplySnapshot(open, Now);
            // An arena whose duel is still open but whose fighters only now became visible here.
            foreach (Duel d in book.Open)
                if (d.IsActive && !running.ContainsKey(d.Id))
                    OnMirroredChange(d);
        }

        /// <summary>A client's view of a duel change: only the arena ring (messages come from the server).</summary>
        private void OnMirroredChange(Duel d)
        {
            if (d.State == DuelState.Ended || d.State == DuelState.Challenged)
            {
                if (running.TryGetValue(d.Id, out Running old))
                {
                    Cleanup(old);
                    running.Remove(d.Id);
                }
                return;
            }
            if (running.ContainsKey(d.Id) || !mirrored.TryGetValue(d.Id, out DuelSnapshot v) || !v.arena)
                return;
            CombatEntity a = Player(d.Challenger), b = Player(d.Challenged);
            CombatEntity seen = a != null ? a : b;
            if (seen == null)
                return; // neither fighter exists on this machine (another realm): nothing to show
            var r = new Running { duel = d, a = a, b = b, center = v.center };
            if (settings.arenaRadius > 0f)
                r.arena = DuelArena.Create(v.center, settings.arenaRadius, seen.gameObject.scene);
            running[d.Id] = r;
        }

        /// <summary>Gives every player the "Challenge to a duel" interaction.</summary>
        private void AttachTargets()
        {
            IReadOnlyList<CombatEntity> all = CombatEntity.Players;
            for (int i = 0; i < all.Count; i++)
            {
                CombatEntity p = all[i];
                if (p == null)
                    continue;
                GameObject mover = PlayerLocator.MovableRoot(p);
                if (mover != null && mover.GetComponent<DuelChallengeTarget>() == null)
                    mover.AddComponent<DuelChallengeTarget>().Bind(p);
            }
        }

        private static string Name(CombatEntity e)
        {
            if (e == null)
                return "Someone";
            if (NameResolver != null)
            {
                string resolved = NameResolver(e);
                if (!string.IsNullOrEmpty(resolved))
                    return resolved;
            }
            PlayerNameComponent n = e.GetComponentInParent<PlayerNameComponent>();
            if (n == null)
                n = e.GetComponentInChildren<PlayerNameComponent>();
            return n != null && !string.IsNullOrEmpty(n.PlayerName) ? n.PlayerName : e.name;
        }

        /// <summary>A player's display name (their Player Name component, else the object's name).</summary>
        public static string DisplayName(CombatEntity e) => Name(e);
    }
}
