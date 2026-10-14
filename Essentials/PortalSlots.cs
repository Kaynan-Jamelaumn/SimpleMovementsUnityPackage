using UnityEngine;

/// <summary>
/// How many players a dungeon portal can still take, as the server sees it (<see cref="Portal.Slots"/>): its player
/// limit, how many are already inside its shared dungeon, and whether it is open.
/// </summary>
public struct PortalSlots
{
    /// <summary>False on a network client until the server's answer arrived (the values are then this copy's settings).</summary>
    public bool known;
    /// <summary>The portal lets players in (a dungeon portal that is not closed after use).</summary>
    public bool open;
    /// <summary>Most players in its dungeon at once (0 = no limit).</summary>
    public int maxPlayers;
    /// <summary>Players inside its shared dungeon now (0 when none is running, or for private runs).</summary>
    public int inside;
    /// <summary>Everyone who enters joins one dungeon (Shared Run or linked portals); false = every group gets its own.</summary>
    public bool shared;
    /// <summary>Its shared dungeon is running.</summary>
    public bool running;
    /// <summary>Dungeon difficulty (1 = normal).</summary>
    public float difficulty;

    /// <summary>No player limit.</summary>
    public bool Unlimited => maxPlayers <= 0;

    /// <summary>
    /// Free slots: the limit minus the players inside a running shared dungeon; for private runs, the size of the group
    /// that may go in together. 0 when closed or full, -1 = no limit.
    /// </summary>
    public int Remaining
    {
        get
        {
            if (!open)
                return 0;
            if (maxPlayers <= 0)
                return -1;
            return Mathf.Max(0, maxPlayers - (shared && running ? inside : 0));
        }
    }

    /// <summary>Open, limited and no slot left.</summary>
    public bool IsFull => open && maxPlayers > 0 && Remaining == 0;
}
