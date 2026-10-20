using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Something a player does at a card table. Game actions have kinds of 0 and up (each game defines its own: play a card,
/// call Truco...); the table's own actions are negative (<see cref="CardTableActions"/>).
/// </summary>
[Serializable]
public struct CardAction
{
    public int kind;
    public int arg;

    public CardAction(int kind, int arg = 0)
    {
        this.kind = kind;
        this.arg = arg;
    }

    public override string ToString() => $"{kind}:{arg}";
}

/// <summary>The table's own actions (not part of any game).</summary>
public static class CardTableActions
{
    /// <summary>Ready to start (arg 1) or not (arg 0).</summary>
    public const int Ready = -1;
    /// <summary>Stand up and leave the table (forfeits a game being played).</summary>
    public const int Leave = -2;
    /// <summary>The table's host picks the stake (arg = index in the table's stakes).</summary>
    public const int ChooseStake = -3;
}

/// <summary>A button a player may press: an action with its label, and why it is greyed out.</summary>
[Serializable]
public sealed class CardActionOption
{
    public CardAction action;
    public string label = "";
    public bool enabled = true;
    public string reason = "";
    /// <summary>0 = normal, 1 = primary (highlighted), 2 = danger (fold, leave).</summary>
    public byte style;

    public CardActionOption() { }

    public CardActionOption(CardAction action, string label, bool enabled = true, string reason = "", byte style = 0)
    {
        this.action = action;
        this.label = label ?? "";
        this.enabled = enabled;
        this.reason = reason ?? "";
        this.style = style;
    }
}

/// <summary>One seat as a viewer sees it.</summary>
[Serializable]
public sealed class CardSeatView
{
    public string name = "";
    public int team;
    public bool occupied;
    public bool bot;
    public bool ready;
    public bool isTurn;
    public int cardCount;
    /// <summary>A word over the seat for a moment ("TRUCO!", "Accepted", "Fold").</summary>
    public string bubble = "";
    /// <summary>The seated player's network object id (−1 = none / offline): lets every machine find the body.</summary>
    public int objectId = -1;
    /// <summary>A number the game gives each seat for the Animator (<c>SeatValue</c>): the arms' position in arm wrestling, drunkenness...</summary>
    public float value;
}

/// <summary>A bar the window shows (arm wrestling's arms, a drinking timer, a player's stamina).</summary>
[Serializable]
public struct CardMeter
{
    public string label;
    /// <summary>0-1, or −1..1 for a centred meter.</summary>
    public float value;
    /// <summary>Centred: −1 = all the way left, +1 = all the way right (a tug of war).</summary>
    public bool centred;
    /// <summary>0 = accent, 1 = good, 2 = bad, 3 = gold.</summary>
    public byte color;

    public CardMeter(string label, float value, bool centred = false, byte color = 0)
    {
        this.label = label;
        this.value = value;
        this.centred = centred;
        this.color = color;
    }
}

/// <summary>A card on the table in front of a seat.</summary>
[Serializable]
public struct CardPlayView
{
    public int seat;
    public byte card;
    /// <summary>The card winning (or that won) the trick.</summary>
    public bool best;
}

/// <summary>
/// Everything one viewer of a card table sees: the seats, the viewer's own hand (only theirs - other hands are counts),
/// the cards on the table, the scores, what is going on, and the buttons they may press. Every game fills it the same
/// way, so one UI and one network message serve Truco, poker or any later game. Sent to each seated client
/// (their own hand) and to onlookers (no hand).
/// </summary>
[Serializable]
public sealed class CardTableView
{
    public string title = "";
    /// <summary>What happens now ("Your turn", "Bram called TRUCO!").</summary>
    public string status = "";
    /// <summary>The last events, newest last.</summary>
    public List<string> log = new List<string>();
    public string stakeText = "";
    /// <summary>The viewer's seat (−1 = watching, not seated).</summary>
    public int viewerSeat = -1;
    public int turnSeat = -1;
    /// <summary>The table's state: 0 lobby, 1 playing, 2 finished (result shown).</summary>
    public byte tablePhase;
    public int winningTeam = -1;
    public List<string> teamNames = new List<string>();
    public List<int> teamScores = new List<int>();
    /// <summary>What the current hand is worth, and any bet being decided ("Hand: 3 (Truco) · Six? called").</summary>
    public string valueText = "";
    public List<CardSeatView> seats = new List<CardSeatView>();
    /// <summary>The viewer's cards (codes, <see cref="PlayingCard.FromCode"/>).</summary>
    public List<byte> hand = new List<byte>();
    /// <summary>Which of <see cref="hand"/> may be played now.</summary>
    public List<bool> handPlayable = new List<bool>();
    public List<CardPlayView> played = new List<CardPlayView>();
    /// <summary>Cards every player sees in the middle (Truco: the vira; poker: the board).</summary>
    public List<byte> shared = new List<byte>();
    public string sharedLabel = "";
    /// <summary>Cards left in the deck (a face-down pile).</summary>
    public int deckCount;
    public List<CardActionOption> actions = new List<CardActionOption>();
    /// <summary>Bars for games without cards (arm wrestling, drinking) or extra information.</summary>
    public List<CardMeter> meters = new List<CardMeter>();

    public CardSeatView Seat(int index) => index >= 0 && index < seats.Count ? seats[index] : null;

    /// <summary>Is a game being played (not the lobby, not the result)?</summary>
    public bool Playing => tablePhase == 1;

    // ------------------------------------------------------------------ bytes
    private const byte Version = 2;

    public byte[] ToBytes()
    {
        using (var ms = new MemoryStream(512))
        using (var w = new BinaryWriter(ms))
        {
            w.Write(Version);
            w.Write(title ?? "");
            w.Write(status ?? "");
            w.Write((byte)Math.Min(log.Count, 12));
            for (int i = Math.Max(0, log.Count - 12); i < log.Count; i++) w.Write(log[i] ?? "");
            w.Write(stakeText ?? "");
            w.Write((sbyte)viewerSeat);
            w.Write((sbyte)turnSeat);
            w.Write(tablePhase);
            w.Write((sbyte)winningTeam);
            w.Write((byte)teamNames.Count);
            for (int i = 0; i < teamNames.Count; i++)
            {
                w.Write(teamNames[i] ?? "");
                w.Write(i < teamScores.Count ? teamScores[i] : 0);
            }
            w.Write(valueText ?? "");
            w.Write((byte)seats.Count);
            foreach (CardSeatView s in seats)
            {
                w.Write(s.name ?? "");
                w.Write((sbyte)s.team);
                byte flags = (byte)((s.occupied ? 1 : 0) | (s.bot ? 2 : 0) | (s.ready ? 4 : 0) | (s.isTurn ? 8 : 0));
                w.Write(flags);
                w.Write((byte)Math.Min(255, s.cardCount));
                w.Write(s.bubble ?? "");
                w.Write(s.objectId);
                w.Write(s.value);
            }
            w.Write((byte)hand.Count);
            for (int i = 0; i < hand.Count; i++)
            {
                w.Write(hand[i]);
                w.Write(i < handPlayable.Count && handPlayable[i]);
            }
            w.Write((byte)played.Count);
            foreach (CardPlayView p in played)
            {
                w.Write((sbyte)p.seat);
                w.Write(p.card);
                w.Write(p.best);
            }
            w.Write((byte)shared.Count);
            foreach (byte c in shared) w.Write(c);
            w.Write(sharedLabel ?? "");
            w.Write((byte)Math.Min(255, deckCount));
            w.Write((byte)actions.Count);
            foreach (CardActionOption a in actions)
            {
                w.Write(a.action.kind);
                w.Write(a.action.arg);
                w.Write(a.label ?? "");
                w.Write(a.enabled);
                w.Write(a.reason ?? "");
                w.Write(a.style);
            }
            w.Write((byte)Math.Min(meters.Count, 16));
            for (int i = 0; i < meters.Count && i < 16; i++)
            {
                w.Write(meters[i].label ?? "");
                w.Write(meters[i].value);
                w.Write(meters[i].centred);
                w.Write(meters[i].color);
            }
            w.Flush();
            return ms.ToArray();
        }
    }

    /// <summary>Reads a view written by <see cref="ToBytes"/> (null when the bytes are not one).</summary>
    public static CardTableView FromBytes(byte[] data)
    {
        if (data == null || data.Length == 0)
            return null;
        try
        {
            using (var ms = new MemoryStream(data))
            using (var r = new BinaryReader(ms))
            {
                if (r.ReadByte() != Version)
                    return null;
                var v = new CardTableView { title = r.ReadString(), status = r.ReadString() };
                int logs = r.ReadByte();
                for (int i = 0; i < logs; i++) v.log.Add(r.ReadString());
                v.stakeText = r.ReadString();
                v.viewerSeat = r.ReadSByte();
                v.turnSeat = r.ReadSByte();
                v.tablePhase = r.ReadByte();
                v.winningTeam = r.ReadSByte();
                int teams = r.ReadByte();
                for (int i = 0; i < teams; i++)
                {
                    v.teamNames.Add(r.ReadString());
                    v.teamScores.Add(r.ReadInt32());
                }
                v.valueText = r.ReadString();
                int seats = r.ReadByte();
                for (int i = 0; i < seats; i++)
                {
                    var s = new CardSeatView { name = r.ReadString(), team = r.ReadSByte() };
                    byte flags = r.ReadByte();
                    s.occupied = (flags & 1) != 0;
                    s.bot = (flags & 2) != 0;
                    s.ready = (flags & 4) != 0;
                    s.isTurn = (flags & 8) != 0;
                    s.cardCount = r.ReadByte();
                    s.bubble = r.ReadString();
                    s.objectId = r.ReadInt32();
                    s.value = r.ReadSingle();
                    v.seats.Add(s);
                }
                int hand = r.ReadByte();
                for (int i = 0; i < hand; i++)
                {
                    v.hand.Add(r.ReadByte());
                    v.handPlayable.Add(r.ReadBoolean());
                }
                int played = r.ReadByte();
                for (int i = 0; i < played; i++)
                    v.played.Add(new CardPlayView { seat = r.ReadSByte(), card = r.ReadByte(), best = r.ReadBoolean() });
                int shared = r.ReadByte();
                for (int i = 0; i < shared; i++) v.shared.Add(r.ReadByte());
                v.sharedLabel = r.ReadString();
                v.deckCount = r.ReadByte();
                int actions = r.ReadByte();
                for (int i = 0; i < actions; i++)
                {
                    var a = new CardActionOption { action = new CardAction(r.ReadInt32(), r.ReadInt32()), label = r.ReadString() };
                    a.enabled = r.ReadBoolean();
                    a.reason = r.ReadString();
                    a.style = r.ReadByte();
                    v.actions.Add(a);
                }
                int meters = r.ReadByte();
                for (int i = 0; i < meters; i++)
                    v.meters.Add(new CardMeter(r.ReadString(), r.ReadSingle(), r.ReadBoolean(), r.ReadByte()));
                return v;
            }
        }
        catch (EndOfStreamException) { return null; }
        catch (IOException) { return null; }
    }
}
