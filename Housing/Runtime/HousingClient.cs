using System;
using UnityEngine;

/// <summary>Finds the housing desk a player stands at: the realtor or innkeeper whose window they have open.</summary>
public static class HousingDesks
{
    /// <summary>The desk of the player's open NPC session (where the rules run: the real check), or none.</summary>
    public static HousingDesk Of(CombatEntity player)
    {
        var desk = new HousingDesk();
        if (player == null) return desk;
        PlayerInteractor interactor = PlayerInteractor.For(player);
        GameObject po = interactor != null && interactor.PlayerObject != null ? interactor.PlayerObject : player.gameObject;
        foreach (NPC npc in NPC.All)
        {
            if (npc == null) continue;
            NPCInteractionSession s = npc.SessionFor(po);
            if (s == null || !s.IsOpen || !npc.IsWithinSessionRange(player.transform.position, 1f)) continue;
            if (s.ActiveBehaviour is Realtor realtor)
                desk.realtor = realtor;
            else if (s.ActiveBehaviour is Innkeeper keeper)
                desk.inn = keeper.InnServed;
            if (desk.AtDesk) break;
        }
        return desk;
    }
}

/// <summary>
/// How the housing windows talk to the housing office: a request (renting, an order, a deposit...) and the views to show.
/// Where the rules run (offline, the host's own player) it is done at once; on a network client it goes to the server
/// (installed by GameNet.NetHousing) and the answers come back through the same events.
/// </summary>
public static class HousingClient
{
    /// <summary>Installed by the network layer on a client: sends a request to the server.</summary>
    public static Action<HousingRequest> Remote;

    /// <summary>The innkeeper's view arrived (after a request, or when the books changed).</summary>
    public static event Action<InnView> InnViewArrived;
    /// <summary>The realtor's (or the player's own housing) view arrived.</summary>
    public static event Action<RealtorView> RealtorViewArrived;
    /// <summary>The answer to a request arrived (its seq is the request's).</summary>
    public static event Action<HousingReply> Answered;

    private static int seq;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Remote = null;
        InnViewArrived = null;
        RealtorViewArrived = null;
        Answered = null;
        seq = 0;
    }

    /// <summary>
    /// Sends a request for the local player; returns its number (its answer carries it). The number is set on
    /// <paramref name="r"/> (<c>r.seq</c>) before anything is sent: where the rules run (offline, the host's own player)
    /// the answer and the fresh views arrive DURING this call, before it returns - match answers by <c>r.seq</c>.
    /// With <see cref="HousingRequest.own"/> the views that come back are the player's own housing, wherever they stand.
    /// </summary>
    public static int Send(CombatEntity player, HousingRequest r)
    {
        if (r == null) return 0;
        r.seq = ++seq;
        if (string.IsNullOrEmpty(r.requestId)) r.requestId = Guid.NewGuid().ToString("N");
        if (GameNet.NetAuthority.IsServer)
        {
            HousingDesk desk = HousingDesks.Of(player);
            HousingReply reply = HousingService.Handle(player, r, desk);
            Answered?.Invoke(reply);
            PushViews(player, desk, r.own);
        }
        else if (Remote != null) Remote(r);
        else Answered?.Invoke(new HousingReply { seq = r.seq, ok = false, message = "Housing is not set up for multiplayer (the Network World needs a NetHousing)." });
        // (A network client's request that cannot be sent at all is refused with its number too: see NetHousing.)
        return r.seq;
    }

    /// <summary>Asks for the views again (a window opened, the books changed); <paramref name="own"/>: the player's own housing.</summary>
    public static int Refresh(CombatEntity player, bool own = false) => Send(player, new HousingRequest { kind = (byte)HousingRequestKind.View, own = own });

    /// <summary>Where the rules run: the views of the player's desk (or, <paramref name="own"/>, their own housing), made now.</summary>
    public static void PushViews(CombatEntity player, HousingDesk desk, bool own = false)
    {
        if (own) RealtorViewArrived?.Invoke(HousingService.RealtorViewFor(player, null));
        else if (desk.inn != null) InnViewArrived?.Invoke(HousingService.InnViewFor(player, desk.inn));
        else RealtorViewArrived?.Invoke(HousingService.RealtorViewFor(player, desk.realtor));
    }

    // ---------------------------------------------------------------- the network layer delivers here
    public static void ReceiveInnView(InnView v)
    {
        if (v != null) InnViewArrived?.Invoke(v);
    }

    public static void ReceiveRealtorView(RealtorView v)
    {
        if (v != null) RealtorViewArrived?.Invoke(v);
    }

    public static void ReceiveReply(HousingReply r)
    {
        if (r != null) Answered?.Invoke(r);
    }
}
