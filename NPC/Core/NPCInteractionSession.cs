using System;
using UnityEngine;

/// <summary>
/// One player talking to one <see cref="NPC"/>: who the player is (and their interactor, inventory and wallet), which
/// behaviour is open (shop, dialogue), and when it ends - closed by the player, walking away, the player dying, the NPC
/// being disabled. Windows bind to a session and close when it ends.
/// </summary>
public sealed class NPCInteractionSession
{
    private CurrencyWallet wallet;
    private InventoryManager inventory;
    private Canvas canvas;

    public NPC Npc { get; }
    /// <summary>The player object (the root with the Player Status Controller).</summary>
    public GameObject Player { get; }
    /// <summary>The player's interactor (null when the session was opened by Player.OnInteract without one).</summary>
    public PlayerInteractor Interactor { get; }
    /// <summary>The behaviour whose window is open (null while the NPC's menu is shown).</summary>
    public NPCBehaviour ActiveBehaviour { get; internal set; }
    /// <summary>The behaviour was chosen from the NPC's menu (closing it can return to the menu).</summary>
    public bool OpenedFromMenu { get; internal set; }
    public bool IsOpen { get; internal set; }
    public float StartTime { get; }
    /// <summary>Why the session ended ("closed", "walked away"...); empty while open.</summary>
    public string EndReason { get; internal set; } = "";

    /// <summary>Raised once when the session ends.</summary>
    public event Action<NPCInteractionSession> Ended;

    public NPCInteractionSession(NPC npc, GameObject player, PlayerInteractor interactor)
    {
        Npc = npc;
        Player = player;
        Interactor = interactor;
        StartTime = Time.time;
        IsOpen = true;
    }

    /// <summary>The player's inventory (null if the player has none).</summary>
    public InventoryManager Inventory
    {
        get
        {
            if (inventory == null && Player != null)
                inventory = Interactor != null ? Interactor.Inventory : InventoryManager.For(Player.transform);
            return inventory;
        }
    }

    /// <summary>The player's wallet (added to the player when missing).</summary>
    public CurrencyWallet Wallet
    {
        get
        {
            if (wallet == null && Player != null)
                wallet = CurrencyWallet.For(Player.transform);
            return wallet;
        }
    }

    /// <summary>The canvas the session's windows go on (the player's inventory canvas, else a generated one).</summary>
    public Canvas Canvas
    {
        get
        {
            if (canvas == null)
                canvas = GameplayUIRoot.CanvasFor(Player != null ? Player.transform : Npc.transform);
            return canvas;
        }
    }

    /// <summary>Ends the session (closes its windows).</summary>
    public void End(string reason = "closed")
    {
        if (IsOpen && Npc != null)
            Npc.EndSession(this, reason);
    }

    /// <summary>The open behaviour is done (its window was closed): back to the NPC's menu, or the end of the session.</summary>
    public void BehaviourFinished(NPCBehaviour behaviour)
    {
        if (IsOpen && Npc != null && ReferenceEquals(ActiveBehaviour, behaviour))
            Npc.OnBehaviourFinished(this, behaviour);
    }

    internal void RaiseEnded()
    {
        try { Ended?.Invoke(this); }
        catch (Exception e) { Debug.LogException(e); }
        Ended = null;
    }
}
