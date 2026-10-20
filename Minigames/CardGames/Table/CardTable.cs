using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The network side of a card table (set by <c>GameNet.NetCardTable</c>): null offline, where the table runs everything
/// itself.
/// </summary>
public interface ICardTableNet
{
    /// <summary>True on the server (the rules run here); false on a client's copy.</summary>
    bool RunsTableRules { get; }

    /// <summary>A client asks the server to do <paramref name="action"/> for its player.</summary>
    void SendAction(CardAction action);

    /// <summary>The server tells a player something ("You need 10 gold").</summary>
    void Tell(CombatEntity player, string text);
}

/// <summary>
/// A table where players sit and play a card game (<see cref="CardGameDefinition"/>: Truco, later poker...).
/// <list type="bullet">
/// <item><b>Seats</b> (<see cref="CardSeat"/> on the benches): players interact to sit; they are placed facing the
/// table, their controls are locked, the Animator's <c>IsSitting</c> turns on, and the table's window opens.</item>
/// <item><b>Lobby</b>: the host (the first player seated) picks the stake; everyone presses Ready; empty seats are
/// filled with bots (<i>Bots Fill Empty Seats</i>) and the game starts.</item>
/// <item><b>Stakes</b> (<see cref="CardStake"/>): money or items are taken from every player at the start and the winning
/// team shares them; a limb stake cuts a limb off each loser (injury system). Leaving during a game forfeits it.</item>
/// <item><b>Cards</b> are dealt into the players' hands (on their hand bones, <see cref="CardTableVisuals"/>) and
/// played onto the table; the window (<see cref="CardTableUI"/>) shows the hand, the table and the buttons.</item>
/// </list>
/// The rules run where the game rules run (offline, or the server with a <c>GameNet.NetCardTable</c>): clients send
/// their actions and receive what they may see (their own hand only).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Minigames/Card Table")]
public class CardTable : MonoBehaviour
{
    public enum TablePhase : byte { Lobby, Playing, Finished }

    private sealed class Occupant
    {
        public CombatEntity player;
        public string name;
        public bool bot;
        public bool ready;
    }

    [Header("Game")]
    [Tooltip("The card game played here. Empty = Truco with the default rules.")]
    [SerializeField] private CardGameDefinition game;
    [Tooltip("Name of the table in the prompt (\"The Rusty Mug\"). Empty = the game's name only.")]
    [SerializeField] private string tableName = "";
    [Tooltip("The middle of the table top (cards are played around it). Empty = this object.")]
    [SerializeField] private Transform center;
    [Tooltip("The seats, clockwise. Empty = every Card Seat under the table, in their Seat Index order.")]
    [SerializeField] private List<CardSeat> seats = new List<CardSeat>();

    [Header("Stakes")]
    [Tooltip("What can be played for. The first one is the default; the host can pick another (Host Chooses Stake).")]
    [SerializeField] private List<CardStake> stakes = new List<CardStake> { new CardStake() };
    [Tooltip("The first player seated picks the stake in the lobby.")]
    [SerializeField] private bool hostChoosesStake = true;
    [Tooltip("Bots put in the stake too (paid by the house): beating bots wins money or items. Off = only players' " +
             "stakes are in the pot.")]
    [SerializeField] private bool houseCoversBots = true;

    [Header("Bots")]
    [Tooltip("Seats nobody sits in are played by the computer when the players start (off = the game needs every seat filled).")]
    [SerializeField] private bool botsFillEmptySeats = true;
    [SerializeField] private List<string> botNames = new List<string> { "Zé", "Tonho", "Dita", "Chico", "Lia", "Bento", "Quitéria", "Juca" };
    [Tooltip("Optional body shown in a bot's seat (any model: it is only a visual, made on every machine).")]
    [SerializeField] private GameObject botPrefab;

    [Header("Flow")]
    [Tooltip("Seconds the result stays on the table before the lobby opens again.")]
    [SerializeField, Min(0f)] private float resultSeconds = 6f;
    [Tooltip("Show the cards in the world: in the players' hands and on the table top.")]
    [SerializeField] private bool showCardsInWorld = true;
    [Tooltip("Size of the cards in the world (1 = a real card, 6.3 × 8.8 cm).")]
    [SerializeField, Min(0.2f)] private float cardScale = 1.4f;
    [Tooltip("Log the table's events in the Console.")]
    [SerializeField] private bool logToConsole;

    private static readonly List<CardTable> all = new List<CardTable>();

    private Occupant[] occupants = new Occupant[0];
    private int stakeIndex;
    private TablePhase phase;
    private CardGameSession session;
    private float resultTimer;
    private readonly List<string> log = new List<string>();
    private int tableVersion;
    private float[] botTimers = new float[0];
    private bool[] botWaiting = new bool[0];
    private System.Random rng;
    private CombatEntity[] startPlayers = new CombatEntity[0];
    private bool[] startBots = new bool[0];
    private CardStake takenStake;
    private readonly List<int> paidSeats = new List<int>();
    private CardGameDefinition runtimeGame;
    private readonly HashSet<CombatEntity> sitting = new HashSet<CombatEntity>();
    private readonly HashSet<CombatEntity> stillSitting = new HashSet<CombatEntity>();
    private CardTableView cachedView;
    private int cachedVersion = int.MinValue;
    private int cachedSeat = int.MinValue;

    // A client's copy: what the server sent.
    private CardTableView publicView, privateView;
    private int remoteVersion;

    // ------------------------------------------------------------------ access
    /// <summary>Every table in the loaded scenes.</summary>
    public static IReadOnlyList<CardTable> All => all;

    /// <summary>Set by the network layer (null offline).</summary>
    public ICardTableNet Net { get; set; }

    /// <summary>True where the game rules run (offline, the server).</summary>
    public bool RulesHere => Net != null ? Net.RunsTableRules : GameNet.NetAuthority.RunsRules(gameObject);

    public CardGameDefinition Game
    {
        get
        {
            if (game != null) return game;
            if (runtimeGame == null) runtimeGame = TrucoGameDefinition.CreateDefault();
            return runtimeGame;
        }
    }

    public Transform Center => center != null ? center : transform;
    public IReadOnlyList<CardSeat> Seats => seats;
    public int SeatCount => Mathf.Min(seats.Count, Game.SeatCount);
    public TablePhase Phase => phase;
    public CardGameSession Session => session;
    public IReadOnlyList<CardStake> Stakes => stakes;
    public CardStake CurrentStake => stakes != null && stakes.Count > 0 ? stakes[Mathf.Clamp(stakeIndex, 0, stakes.Count - 1)] : NoStake;
    public bool ShowCardsInWorld => showCardsInWorld;
    public float CardScale => cardScale;
    public GameObject BotPrefab => botPrefab;

    private static readonly CardStake NoStake = new CardStake();

    /// <summary>"Truco · The Rusty Mug · 10 gold each" (the seats' prompt).</summary>
    public string PromptName
    {
        get
        {
            string n = Game.DisplayName + (string.IsNullOrWhiteSpace(tableName) ? "" : " · " + tableName);
            CardStake s = RulesHere ? CurrentStake : null;
            string stake = s != null ? s.Describe() : publicView != null ? publicView.stakeText : "";
            return string.IsNullOrEmpty(stake) ? n : $"{n} · {stake}";
        }
    }

    /// <summary>Changes whenever what the table shows changed (the network sends views when it moves).</summary>
    public int ViewVersion
    {
        get
        {
            if (!RulesHere)
                return remoteVersion;
            unchecked { return tableVersion * 1000003 + (session != null ? session.Version : 0); }
        }
    }

    /// <summary>The table the local player sits at (null when they sit nowhere).</summary>
    public static CardTable LocalTable
    {
        get
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].LocalSeat >= 0)
                    return all[i];
            return null;
        }
    }

    /// <summary>The table <paramref name="player"/> sits at (rules side), or null.</summary>
    public static CardTable TableOf(CombatEntity player)
    {
        if (player == null)
            return null;
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && all[i].SeatOf(player) >= 0)
                return all[i];
        return null;
    }

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        rng = new System.Random();
        CollectSeats();
    }

    private void OnEnable()
    {
        if (!all.Contains(this))
            all.Add(this);
        if (showCardsInWorld && GetComponent<CardTableVisuals>() == null)
            gameObject.AddComponent<CardTableVisuals>();
    }

    private void OnDisable()
    {
        all.Remove(this);
        if (RulesHere && Application.isPlaying)
            CloseTable("The table closed.");
        ClearSitting();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => all.Clear();

    /// <summary>Finds the seats (the list, else every Card Seat below) and numbers them.</summary>
    public void CollectSeats()
    {
        seats.RemoveAll(s => s == null);
        if (seats.Count == 0)
        {
            seats.AddRange(GetComponentsInChildren<CardSeat>(true));
            seats.Sort((a, b) =>
            {
                int ia = a.SeatIndex < 0 ? int.MaxValue : a.SeatIndex, ib = b.SeatIndex < 0 ? int.MaxValue : b.SeatIndex;
                return ia.CompareTo(ib);
            });
        }
        for (int i = 0; i < seats.Count; i++)
            seats[i].SeatIndex = i;
        int n = SeatCount;
        if (occupants.Length != n)
        {
            occupants = new Occupant[n];
            botTimers = new float[n];
            botWaiting = new bool[n];
        }
    }

    // ------------------------------------------------------------------ seating
    /// <summary>The seat of <paramref name="player"/> (rules side), or −1.</summary>
    public int SeatOf(CombatEntity player)
    {
        if (player == null)
            return -1;
        for (int i = 0; i < occupants.Length; i++)
            if (occupants[i] != null && !occupants[i].bot && occupants[i].player == player)
                return i;
        return -1;
    }

    /// <summary>The player sitting at <paramref name="seat"/> on this machine (a bot or nobody: null).</summary>
    public CombatEntity SeatPlayer(int seat)
    {
        if (seat < 0)
            return null;
        if (RulesHere)
            return seat < occupants.Length && occupants[seat] != null && !occupants[seat].bot ? occupants[seat].player : null;
        CardSeatView s = (privateView ?? publicView)?.Seat(seat);
        return s != null && s.occupied && !s.bot && s.objectId > 0 ? HubWorld.PlayerIds.Find(s.objectId) : null;
    }

    /// <summary>Can <paramref name="player"/> sit at <paramref name="seat"/> now (and why not)?</summary>
    public bool CanSit(int seat, CombatEntity player, out string reason)
    {
        reason = "";
        if (seat < 0 || seat >= SeatCount)
            return false;
        if (player == null || player.IsDead || player.IsUnconscious || player.IsSleeping)
            return false;
        if (!RulesHere)
        {
            // A client guesses from the last view; the server decides.
            CardTableView v = publicView;
            CardSeatView s = v?.Seat(seat);
            if (s != null && s.occupied && !s.bot) { reason = "Taken"; return false; }
            if (v != null && v.Playing) { reason = "A game is being played"; return false; }
            if (LocalSeat >= 0) { reason = "You are seated"; return false; }
            return true;
        }
        if (SeatOf(player) >= 0) { reason = "You are seated"; return false; }
        CardTable other = TableOf(player);
        if (other != null && other != this) { reason = "You are at another table"; return false; }
        if (phase == TablePhase.Playing) { reason = "A game is being played"; return false; }
        Occupant o = occupants[seat];
        if (o != null && !o.bot) { reason = "Taken"; return false; }
        return Game.CanPlay(player, out reason);
    }

    /// <summary>Sits <paramref name="player"/> at <paramref name="seat"/> (rules side; a client's interaction is relayed).</summary>
    public bool TrySit(int seat, CombatEntity player, out string reason)
    {
        if (!RulesHere)
        {
            reason = "";
            return false;
        }
        if (!CanSit(seat, player, out reason))
            return false;
        occupants[seat] = new Occupant { player = player, name = NameOf(player) };
        CardSeat s = seats[seat];
        HubWorld.RealmTravel.Place(player, s.SitPose);
        HubWorld.RealmTravel.SetControls(player, false);
        AddLog($"{occupants[seat].name} sits down.");
        if (hostChoosesStake && HostSeat == seat && stakes.Count > 1)
            Tell(player, "You are the host: pick the stake, then press Ready.");
        else if (!CurrentStake.CanPay(player, out string why))
            Tell(player, $"The stake here is {CurrentStake.Describe()}: {why}.");
        Changed();
        return true;
    }

    /// <summary>Stands <paramref name="player"/> up (rules side). A game being played is forfeited by their team.</summary>
    public void Leave(CombatEntity player, string why = "leaves the table")
    {
        int seat = SeatOf(player);
        if (seat >= 0)
            StandUp(seat, why);
    }

    private void StandUp(int seat, string why)
    {
        Occupant o = occupants[seat];
        if (o == null)
            return;
        occupants[seat] = null;
        AddLog($"{o.name} {why}.");
        if (phase == TablePhase.Playing && session != null && !session.IsOver)
            session.PlayerLeft(seat, o.name);
        if (o.player != null)
        {
            HubWorld.RealmTravel.SetControls(o.player, true);
            if (!o.player.IsDead)
                HubWorld.RealmTravel.Place(o.player, seats[seat].StandPose);
        }
        if (phase != TablePhase.Playing && HumanCount == 0)
            for (int i = 0; i < occupants.Length; i++)
                if (occupants[i] != null && occupants[i].bot)
                    occupants[i] = null;
        Changed();
    }

    /// <summary>Everyone stands up; stakes of a game being played go back (the table was removed).</summary>
    private void CloseTable(string why)
    {
        if (phase == TablePhase.Playing)
        {
            Refund();
            AddLog(why + " Stakes were returned.");
            phase = TablePhase.Lobby;
            session = null;
        }
        for (int i = 0; i < occupants.Length; i++)
        {
            Occupant o = occupants[i];
            occupants[i] = null;
            if (o != null && !o.bot && o.player != null)
                HubWorld.RealmTravel.SetControls(o.player, true);
        }
    }

    private int HumanCount
    {
        get
        {
            int n = 0;
            foreach (Occupant o in occupants)
                if (o != null && !o.bot) n++;
            return n;
        }
    }

    /// <summary>The seat of the table's host: the first seat with a player (they pick the stake).</summary>
    public int HostSeat
    {
        get
        {
            for (int i = 0; i < occupants.Length; i++)
                if (occupants[i] != null && !occupants[i].bot)
                    return i;
            return -1;
        }
    }

    private static string NameOf(CombatEntity player)
    {
        PlayerNameComponent n = player.GetComponentInChildren<PlayerNameComponent>(true);
        if (n == null)
            n = player.GetComponentInParent<PlayerNameComponent>();
        return n != null && !string.IsNullOrWhiteSpace(n.PlayerName) ? n.PlayerName : player.name;
    }

    // ------------------------------------------------------------------ actions
    /// <summary>
    /// <paramref name="player"/> does <paramref name="action"/> (rules side): the table's own actions (ready, stake,
    /// leave) or a game action. False (and why) when it is not allowed.
    /// </summary>
    public bool Act(CombatEntity player, CardAction action, out string reason)
    {
        reason = "";
        if (!RulesHere)
        {
            reason = "Ask the server";
            return false;
        }
        int seat = SeatOf(player);
        if (seat < 0) { reason = "You are not seated at this table"; return false; }
        Occupant o = occupants[seat];
        switch (action.kind)
        {
            case CardTableActions.Leave:
                StandUp(seat, phase == TablePhase.Playing ? "leaves the game" : "leaves the table");
                return true;

            case CardTableActions.Ready:
                if (phase != TablePhase.Lobby) { reason = "A game is being played"; return false; }
                if (action.arg != 0 && (!Game.CanPlay(player, out reason) || !CurrentStake.CanPay(player, out reason))) return false;
                o.ready = action.arg != 0;
                AddLog(o.ready ? $"{o.name} is ready." : $"{o.name} is not ready.");
                Changed();
                TryStart();
                return true;

            case CardTableActions.ChooseStake:
                if (phase != TablePhase.Lobby) { reason = "A game is being played"; return false; }
                if (!hostChoosesStake) { reason = "This table has a fixed stake"; return false; }
                if (seat != HostSeat) { reason = $"{occupants[HostSeat].name} (the host) picks the stake"; return false; }
                if (action.arg < 0 || action.arg >= stakes.Count) { reason = "No such stake"; return false; }
                if (stakeIndex != action.arg)
                {
                    stakeIndex = action.arg;
                    foreach (Occupant other in occupants)
                        if (other != null && !other.bot) other.ready = false;
                    AddLog($"{o.name} sets the stake: {CurrentStake.Title}.");
                    Changed();
                }
                return true;
        }
        if (phase != TablePhase.Playing || session == null) { reason = "No game is being played"; return false; }
        return session.TryAct(seat, action, out reason);
    }

    /// <summary>
    /// The local player does <paramref name="action"/> from the window: at once where the rules run, else sent to the
    /// server.
    /// </summary>
    public void RequestAction(CardAction action)
    {
        if (!RulesHere)
        {
            Net?.SendAction(action);
            return;
        }
        CombatEntity me = LocalPlayer;
        if (me == null)
            return;
        if (!Act(me, action, out string reason) && !string.IsNullOrEmpty(reason))
            Tell(me, reason);
    }

    /// <summary>Starts the game once every player is ready (bots fill the empty seats; the stakes are taken).</summary>
    private void TryStart()
    {
        if (phase != TablePhase.Lobby)
            return;
        int n = SeatCount;
        int humans = 0;
        for (int i = 0; i < n; i++)
        {
            Occupant o = occupants[i];
            if (o == null || o.bot) continue;
            humans++;
            if (!o.ready) return;
        }
        if (humans == 0)
            return;
        CardGameDefinition g = Game;
        int playing = botsFillEmptySeats ? n : humans;
        if (!g.PlaysWithEmptySeats && playing < n)
        {
            AddLog($"Waiting for {n - humans} more player(s).");
            Changed();
            return;
        }
        if (playing < Mathf.Min(n, g.MinPlayers))
        {
            AddLog($"{g.DisplayName} needs at least {g.MinPlayers} players.");
            Changed();
            return;
        }
        for (int i = 0; i < n; i++)
        {
            Occupant o = occupants[i];
            if (o == null || o.bot || g.CanPlay(o.player, out string cannot)) continue;
            o.ready = false;
            Tell(o.player, cannot + ".");
            Changed();
            return;
        }

        // Take the stakes (all or nothing). The players of this game first: a refund goes back to them.
        startPlayers = new CombatEntity[n];
        startBots = new bool[n];
        for (int i = 0; i < n; i++)
            startPlayers[i] = occupants[i] != null && !occupants[i].bot ? occupants[i].player : null;
        CardStake stake = CurrentStake;
        paidSeats.Clear();
        for (int i = 0; i < n; i++)
        {
            Occupant o = occupants[i];
            if (o == null || o.bot) continue;
            if (!stake.CanPay(o.player, out string why) || !stake.Take(o.player))
            {
                Refund(stake);
                o.ready = false;
                Tell(o.player, string.IsNullOrEmpty(why) ? "You cannot pay the stake." : why + ".");
                AddLog($"{o.name} cannot pay the stake.");
                Changed();
                return;
            }
            paidSeats.Add(i);
        }

        var used = new HashSet<string>();
        for (int i = 0; i < n; i++)
        {
            if (occupants[i] != null) { used.Add(occupants[i].name); continue; }
            if (!botsFillEmptySeats) continue;
            occupants[i] = new Occupant { bot = true, name = BotName(used), ready = true };
            used.Add(occupants[i].name);
        }
        var names = new string[n];
        var bots = new bool[n];
        var seatedNow = new bool[n];
        var power = new float[n];
        int dealer = -1;
        for (int i = 0; i < n; i++)
        {
            Occupant o = occupants[i];
            names[i] = o != null ? o.name : "";
            bots[i] = startBots[i] = o != null && o.bot;
            seatedNow[i] = o != null;
            power[i] = o == null ? 0f : o.bot ? g.BotPower(rng) : g.PowerOf(o.player);
            botWaiting[i] = false;
        }
        for (int k = 0, start = rng.Next(n); k < n && dealer < 0; k++)
            if (seatedNow[(start + k) % n]) dealer = (start + k) % n;
        takenStake = stake;
        session = g.CreateSession(new CardGameSetup
        {
            seed = rng.Next(), names = names, bots = bots, firstDealer = Mathf.Max(0, dealer), seated = seatedNow, power = power,
        });
        session.Message += AddLog;
        log.Clear();
        AddLog($"{Game.DisplayName} begins. Stake: {stake.Title}.");
        phase = TablePhase.Playing;
        session.Start();
        Changed();
    }

    private string BotName(HashSet<string> used)
    {
        if (botNames != null)
            foreach (string b in botNames)
                if (!string.IsNullOrWhiteSpace(b) && !used.Contains(b + " (bot)"))
                    return b + " (bot)";
        return $"Bot {used.Count + 1}";
    }

    // ------------------------------------------------------------------ the game running
    private void Update()
    {
        if (!RulesHere)
            return;
        float dt = Time.deltaTime;
        // Players who died, were knocked out or vanished leave (forfeiting a game).
        for (int i = 0; i < occupants.Length; i++)
        {
            Occupant o = occupants[i];
            if (o == null || o.bot) continue;
            if (o.player == null) StandUp(i, "is gone");
            else if (o.player.IsDead || o.player.IsUnconscious) StandUp(i, "is out");
        }

        if (phase == TablePhase.Playing && session != null)
        {
            session.Tick(dt);
            Vector2 think = Game.BotThinkSeconds;
            for (int s = 0; s < occupants.Length && !session.IsOver; s++)
            {
                if (occupants[s] == null || !occupants[s].bot || !session.WantsBotAction(s))
                {
                    botWaiting[s] = false;
                    continue;
                }
                if (!botWaiting[s])
                {
                    botWaiting[s] = true;
                    botTimers[s] = UnityEngine.Random.Range(Mathf.Min(think.x, think.y), Mathf.Max(think.x, think.y));
                    continue;
                }
                botTimers[s] -= dt;
                if (botTimers[s] > 0f)
                    continue;
                botWaiting[s] = false;
                CardAction a = session.BotDecide(s);
                if (!session.TryAct(s, a, out _))
                    for (int c = 0; c < 13 && !session.TryAct(s, new CardAction(CardGameSession.PlayCard, c), out _); c++) { }
            }
            if (session.IsOver)
                EndGame();
        }
        else if (phase == TablePhase.Finished)
        {
            resultTimer -= dt;
            if (resultTimer <= 0f)
                BackToLobby();
        }
    }

    private void EndGame()
    {
        int winner = session.WinningTeam;
        Settle(winner);
        try { Game.AfterGame(session, startPlayers, rng, AddLog); }
        catch (Exception e) { Debug.LogException(e, this); }
        phase = TablePhase.Finished;
        resultTimer = resultSeconds;
        Changed();
    }

    private void BackToLobby()
    {
        if (session != null)
            session.Message -= AddLog;
        session = null;
        phase = TablePhase.Lobby;
        takenStake = null;
        paidSeats.Clear();
        for (int i = 0; i < occupants.Length; i++)
        {
            if (occupants[i] == null) continue;
            if (occupants[i].bot) occupants[i] = null;
            else occupants[i].ready = false;
        }
        AddLog("Press Ready to play again.");
        Changed();
    }

    /// <summary>Pays the stakes out: the winning team shares the pot; a limb stake takes a limb from each loser.</summary>
    private void Settle(int winner)
    {
        CardStake stake = takenStake;
        if (stake == null || stake.kind == CardStakeKind.None)
            return;
        float[] pay = session != null ? session.Payouts : null;
        if (pay != null)
        {
            SettlePayouts(stake, pay, session.PayoutsFromHouse);
            return;
        }
        if (winner < 0)
        {
            Refund(stake);
            AddLog("Nobody won: the stakes were returned.");
            return;
        }
        Vector3 drop = Center.position + Vector3.up * 0.3f;
        int n = startPlayers.Length;
        if (stake.kind == CardStakeKind.Limb)
        {
            for (int i = 0; i < n; i++)
            {
                CombatEntity p = startPlayers[i];
                if (p == null || Game.TeamOfSeat(i) == winner) continue;
                string lost = stake.TakeLimb(p, rng);
                AddLog(lost != null ? $"{NameOf(p)} loses their {lost.ToLowerInvariant()}!" : $"{NameOf(p)} has nothing left to lose.");
            }
            return;
        }
        int stakers = paidSeats.Count;
        if (houseCoversBots)
            for (int i = 0; i < n; i++)
                if (startBots[i]) stakers++;
        int winners = 0;
        for (int i = 0; i < n; i++)
            if (Game.TeamOfSeat(i) == winner) winners++;
        if (winners == 0)
            return;
        int unit = stake.kind == CardStakeKind.Currency ? stake.amount : stake.quantity;
        int total = unit * stakers;
        int share = total / winners, rest = total % winners;
        bool first = true;
        for (int i = 0; i < n; i++)
        {
            CombatEntity p = startPlayers[i];
            if (p == null || Game.TeamOfSeat(i) != winner) continue;
            int amount = share + (first ? rest : 0);
            first = false;
            stake.Give(p, 0, amount, drop);
            AddLog(stake.kind == CardStakeKind.Currency ? $"{NameOf(p)} wins {stake.Currency.Format(amount)}." : $"{NameOf(p)} wins {amount} × {stake.item.Name}.");
        }
    }

    /// <summary>
    /// Games where everyone plays for themselves (blackjack, poker): each player gets back their payout (2 = twice the
    /// stake). Without the house paying, players never get more than the stakes put in (bots' winnings stay with the
    /// house). A limb stake takes a limb from everyone who ended with less than they started with.
    /// </summary>
    private void SettlePayouts(CardStake stake, float[] pay, bool fromHouse)
    {
        Vector3 drop = Center.position + Vector3.up * 0.3f;
        int n = Mathf.Min(startPlayers.Length, pay.Length);
        if (stake.kind == CardStakeKind.Limb)
        {
            for (int i = 0; i < n; i++)
            {
                CombatEntity p = startPlayers[i];
                if (p == null || pay[i] >= 0.999f) continue;
                string lost = stake.TakeLimb(p, rng);
                AddLog(lost != null ? $"{NameOf(p)} loses their {lost.ToLowerInvariant()}!" : $"{NameOf(p)} has nothing left to lose.");
            }
            return;
        }
        float factor = 1f;
        if (!fromHouse && !houseCoversBots)
        {
            float humans = 0f;
            for (int i = 0; i < n; i++)
                if (startPlayers[i] != null) humans += Mathf.Max(0f, pay[i]);
            if (humans > paidSeats.Count && humans > 0f)
                factor = paidSeats.Count / humans;
        }
        int unit = stake.kind == CardStakeKind.Currency ? stake.amount : stake.quantity;
        for (int i = 0; i < n; i++)
        {
            CombatEntity p = startPlayers[i];
            if (p == null || !paidSeats.Contains(i)) continue;
            int amount = Mathf.FloorToInt(unit * Mathf.Max(0f, pay[i]) * factor + 1e-4f);
            if (amount > 0)
                stake.Give(p, 0, amount, drop);
            string what = stake.kind == CardStakeKind.Currency ? stake.Currency.Format(amount) : $"{amount} × {stake.item.Name}";
            AddLog(amount > 0 ? $"{NameOf(p)} gets {what}." : $"{NameOf(p)} lost the stake.");
        }
    }

    private void Refund(CardStake stake = null)
    {
        stake = stake ?? takenStake;
        if (stake == null || stake.kind == CardStakeKind.None || stake.kind == CardStakeKind.Limb)
        {
            paidSeats.Clear();
            return;
        }
        Vector3 drop = Center.position + Vector3.up * 0.3f;
        foreach (int i in paidSeats)
        {
            CombatEntity p = i < startPlayers.Length && startPlayers[i] != null ? startPlayers[i] : occupants[i]?.player;
            if (p != null)
                stake.Give(p, 1, 0, drop);
        }
        paidSeats.Clear();
    }

    // ------------------------------------------------------------------ what players see
    /// <summary>Everything <paramref name="viewerSeat"/> sees (−1 = an onlooker). Rules side.</summary>
    public CardTableView BuildView(int viewerSeat)
    {
        var v = new CardTableView { title = Game.DisplayName + (string.IsNullOrWhiteSpace(tableName) ? "" : " · " + tableName) };
        v.viewerSeat = viewerSeat;
        v.tablePhase = (byte)phase;
        v.stakeText = CurrentStake.Describe();
        int n = SeatCount;
        int ready = 0, humans = 0;
        for (int i = 0; i < n; i++)
        {
            Occupant o = occupants[i];
            var s = new CardSeatView
            {
                name = o != null ? o.name : "Empty seat",
                team = Game.TeamOfSeat(i),
                occupied = o != null,
                bot = o != null && o.bot,
                ready = o != null && o.ready,
                objectId = o != null && !o.bot && o.player != null ? HubWorld.PlayerIds.Of(o.player) : -1,
            };
            if (o != null && !o.bot) { humans++; if (o.ready) ready++; }
            v.seats.Add(s);
        }
        for (int t = 0; t < Game.TeamCount; t++)
        {
            v.teamNames.Add(Game.TeamName(t));
            v.teamScores.Add(0);
        }
        if (session != null)
            session.FillView(v, phase == TablePhase.Playing || phase == TablePhase.Finished ? viewerSeat : -1);
        v.log.AddRange(log);

        Occupant me = viewerSeat >= 0 && viewerSeat < n ? occupants[viewerSeat] : null;
        if (phase == TablePhase.Lobby)
        {
            int empty = n - humans;
            string seatsNote = empty <= 0 ? "" : botsFillEmptySeats ? $" · {empty} seat(s) will be played by bots"
                : Game.PlaysWithEmptySeats ? (humans < Game.MinPlayers ? $" · needs at least {Game.MinPlayers} players" : "")
                : $" · waiting for {empty} more player(s)";
            v.status = humans == 0 ? "Sit down to play." : $"{ready}/{humans} ready{seatsNote}.";
            v.valueText = Game.Description;
            if (me != null)
            {
                bool canPay = CurrentStake.CanPay(me.player, out string why);
                v.actions.Add(me.ready
                    ? new CardActionOption(new CardAction(CardTableActions.Ready, 0), "Not ready")
                    : new CardActionOption(new CardAction(CardTableActions.Ready, 1), "Ready", canPay, why, 1));
                if (hostChoosesStake && stakes.Count > 1)
                {
                    bool host = viewerSeat == HostSeat;
                    for (int i = 0; i < stakes.Count; i++)
                        v.actions.Add(new CardActionOption(new CardAction(CardTableActions.ChooseStake, i),
                            (i == stakeIndex ? "● " : "○ ") + stakes[i].Title, host && i != stakeIndex,
                            host ? (i == stakeIndex ? "The current stake" : "") : "Only the host picks the stake"));
                }
            }
        }
        else if (phase == TablePhase.Finished && session != null)
        {
            v.status = (session.WinningTeam >= 0 ? $"{Game.TeamName(session.WinningTeam)} wins! " : "") + "A new game can start in a moment.";
        }
        if (me != null)
            v.actions.Add(new CardActionOption(new CardAction(CardTableActions.Leave),
                phase == TablePhase.Playing ? "Leave (forfeit)" : "Leave table", true, "", 2));
        return v;
    }

    /// <summary>What the local player sees now (their seat's view, or an onlooker's).</summary>
    public CardTableView LocalView
    {
        get
        {
            if (!RulesHere)
                return privateView != null && privateView.viewerSeat >= 0 ? privateView : publicView;
            int seat = LocalSeat;
            int version = ViewVersion;
            if (cachedView == null || cachedVersion != version || cachedSeat != seat)
            {
                cachedView = BuildView(seat);
                cachedVersion = version;
                cachedSeat = seat;
            }
            return cachedView;
        }
    }

    /// <summary>The local player's seat (−1 = not seated here).</summary>
    public int LocalSeat
    {
        get
        {
            if (!RulesHere)
                return privateView != null ? privateView.viewerSeat : -1;
            for (int i = 0; i < occupants.Length; i++)
                if (occupants[i] != null && !occupants[i].bot && IsLocalPlayer(occupants[i].player))
                    return i;
            return -1;
        }
    }

    /// <summary>The local player when they sit here (rules side), else null.</summary>
    public CombatEntity LocalPlayer
    {
        get
        {
            int s = LocalSeat;
            return s >= 0 && RulesHere ? occupants[s].player : null;
        }
    }

    /// <summary>The seated humans with their seats (the network layer sends each one their own view).</summary>
    public void SeatedPlayers(List<(int seat, CombatEntity player)> into)
    {
        into.Clear();
        for (int i = 0; i < occupants.Length; i++)
            if (occupants[i] != null && !occupants[i].bot && occupants[i].player != null)
                into.Add((i, occupants[i].player));
    }

    /// <summary>Is <paramref name="e"/> this machine's player?</summary>
    public static bool IsLocalPlayer(CombatEntity e)
    {
        if (e == null || e.Kind != CombatEntity.EntityKind.Player)
            return false;
        if (!GameNet.NetAuthority.IsNetworkedObject(e.gameObject))
            return GameNet.NetAuthority.HasLocalPlayer;
        return GameNet.NetAuthority.IsLocallyControlled(e.gameObject);
    }

    // ------------------------------------------------------------------ network (client copies)
    /// <summary>A client receives the view every onlooker gets.</summary>
    public void ReceivePublicView(byte[] data)
    {
        CardTableView v = CardTableView.FromBytes(data);
        if (v == null) return;
        publicView = v;
        remoteVersion++;
    }

    /// <summary>A client receives its own seat's view (viewer seat −1 = it left the table).</summary>
    public void ReceivePrivateView(byte[] data)
    {
        CardTableView v = CardTableView.FromBytes(data);
        if (v == null) return;
        privateView = v.viewerSeat >= 0 ? v : null;
        if (v.viewerSeat < 0)
            publicView = v;
        remoteVersion++;
    }

    // ------------------------------------------------------------------ messages and changes
    /// <summary>Tells <paramref name="player"/> something (the inventory's message line; over the network for remote players).</summary>
    public void Tell(CombatEntity player, string text)
    {
        if (player == null || string.IsNullOrEmpty(text))
            return;
        if (IsLocalPlayer(player))
            InventoryManager.For(player)?.ShowMessage(text, InventoryFeedback.Kind.Info);
        else
            Net?.Tell(player, text);
    }

    /// <summary>A message from the server for the local player.</summary>
    public void ShowLocalMessage(string text)
    {
        CombatEntity me = null;
        IReadOnlyList<CombatEntity> players = CombatEntity.Players;
        for (int i = 0; i < players.Count && me == null; i++)
            if (IsLocalPlayer(players[i])) me = players[i];
        if (me != null && !string.IsNullOrEmpty(text))
            InventoryManager.For(me)?.ShowMessage(text, InventoryFeedback.Kind.Info);
    }

    private void AddLog(string line)
    {
        if (string.IsNullOrEmpty(line))
            return;
        log.Add(line);
        if (log.Count > 30)
            log.RemoveAt(0);
        if (logToConsole)
            Debug.Log($"[Card Table] {name}: {line}", this);
        Changed();
    }

    private void Changed() => tableVersion++;

    // ------------------------------------------------------------------ seated bodies (every machine)
    private void LateUpdate()
    {
        // Sitting pose for everyone seated here, the window for the local player.
        stillSitting.Clear();
        CardTableView view = LocalView;
        for (int i = 0; i < SeatCount; i++)
        {
            CombatEntity e = SeatPlayer(i);
            if (e == null) continue;
            stillSitting.Add(e);
            SetSitting(e, true, view?.Seat(i) != null ? view.Seat(i).value : 0f);
        }
        foreach (CombatEntity e in sitting)
            if (!stillSitting.Contains(e))
                SetSitting(e, false);
        sitting.Clear();
        foreach (CombatEntity e in stillSitting)
            sitting.Add(e);
        if (LocalSeat >= 0)
            CardTableUI.Show(this);
    }

    private void ClearSitting()
    {
        foreach (CombatEntity e in sitting)
            SetSitting(e, false);
        sitting.Clear();
    }

    /// <summary>The Animator's IsSitting, and SeatValue (what the game gives the seat: arm wrestling's arms, drunkenness).</summary>
    private static void SetSitting(CombatEntity e, bool on, float value = 0f)
    {
        if (e == null)
            return;
        Transform root = AbilitiesStateMachine.PlayerRoot(e);
        PlayerAnimationModel model = root != null ? root.GetComponentInChildren<PlayerAnimationModel>() : null;
        if (model == null)
            return;
        model.SetParameterSafe("IsSitting", on);
        model.SetParameterSafe("SeatValue", on ? value : 0f);
    }

    // ------------------------------------------------------------------ editor
    /// <summary>
    /// Testing (the inspector's Watch Bots Play): every free seat gets a bot and a game starts for fun, without players.
    /// Rules side, in the lobby only.
    /// </summary>
    public void StartBotsOnlyGame()
    {
        if (!RulesHere || phase != TablePhase.Lobby)
            return;
        CollectSeats();
        int n = SeatCount;
        var used = new HashSet<string>();
        var names = new string[n];
        var bots = new bool[n];
        var power = new float[n];
        startPlayers = new CombatEntity[n];
        startBots = new bool[n];
        for (int i = 0; i < n; i++)
        {
            if (occupants[i] == null)
                occupants[i] = new Occupant { bot = true, name = BotName(used), ready = true };
            used.Add(occupants[i].name);
            names[i] = occupants[i].name;
            bots[i] = startBots[i] = occupants[i].bot;
            startPlayers[i] = occupants[i].bot ? null : occupants[i].player;
            power[i] = occupants[i].bot ? Game.BotPower(rng) : Game.PowerOf(occupants[i].player);
            botWaiting[i] = false;
        }
        paidSeats.Clear();
        takenStake = null;
        session = Game.CreateSession(new CardGameSetup { seed = rng.Next(), names = names, bots = bots, firstDealer = rng.Next(n), power = power });
        session.Message += AddLog;
        log.Clear();
        AddLog($"{Game.DisplayName} (test game, no stakes).");
        phase = TablePhase.Playing;
        session.Start();
        Changed();
    }

    public void Validate(List<string> errors, List<string> warnings)
    {
        CardGameDefinition g = Game;
        g.Validate(errors, warnings);
        var found = seats.Count > 0 ? seats : new List<CardSeat>(GetComponentsInChildren<CardSeat>(true));
        if (found.Count < g.SeatCount)
            errors.Add($"{g.DisplayName} needs {g.SeatCount} seats; the table has {found.Count} (add Card Seat components to the benches).");
        else if (found.Count > g.SeatCount)
            warnings.Add($"The table has {found.Count} seats; {g.DisplayName} uses the first {g.SeatCount}.");
        if (stakes == null || stakes.Count == 0)
            warnings.Add("No stakes: games are played just for fun.");
        else
            for (int i = 0; i < stakes.Count; i++)
                stakes[i]?.Validate(name, errors, warnings);
    }

    private void OnValidate()
    {
        if (stakes == null || stakes.Count == 0)
            stakes = new List<CardStake> { new CardStake() };
    }
}
