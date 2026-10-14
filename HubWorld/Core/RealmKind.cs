namespace HubWorld
{
    /// <summary>Where a player is: the permanent city, the cyclical open world, a dungeon, or on the way between them.</summary>
    public enum RealmKind
    {
        /// <summary>Not known to the hub world yet (a player that has not been placed).</summary>
        None,
        /// <summary>The hub city: permanent, no mobs, no PvP (duels aside), abilities are only shown.</summary>
        City,
        /// <summary>The open world around the city, regenerated with a new seed every cycle.</summary>
        World,
        /// <summary>A dungeon instance (one scene per group).</summary>
        Dungeon,
        /// <summary>Between realms while the destination loads (controls off, hidden from combat).</summary>
        Transit,
    }
}
