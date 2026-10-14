namespace GameNet
{
    /// <summary>
    /// A player component whose controls can be locked without switching components off - the network-safe way to
    /// freeze a player (travel, dungeon transitions): a predicted movement keeps ticking, it just ignores input and
    /// gravity while locked, and the lock is part of the state the server sends back. Implemented by the movement
    /// state machine; used by <see cref="ProceduralDungeon.DungeonSession.SetControls"/> for networked players.
    /// </summary>
    public interface IControlLock
    {
        /// <summary>Locks (true) or releases (false) the player's controls.</summary>
        void SetControlsLocked(bool locked);
    }
}
