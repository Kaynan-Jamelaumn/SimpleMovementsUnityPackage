namespace HubWorld
{
    /// <summary>
    /// The rules of the dimensional jump, as pure functions (the director applies them, tests check them):
    /// <list type="bullet">
    /// <item>Players outside the city and not in a dungeon when the cycle ends are lost between dimensions: back to the
    /// city, with the loot-loss penalty and the Dimensional Disorientation debuff.</item>
    /// <item>Players in a dungeon finish their run. When they leave, they go back to the world they came from if its cycle
    /// is still running, otherwise straight to the city - with the same penalties.</item>
    /// </list>
    /// </summary>
    public static class CycleRules
    {
        /// <summary>
        /// Is a player in <paramref name="realm"/> at the moment of the jump lost between dimensions? Only those out in the
        /// world: a player still on the way out (the world was loading) never left, and is simply turned back at the gate.
        /// </summary>
        public static bool LostAtJump(RealmKind realm) => realm == RealmKind.World;

        /// <summary>Is a player in a dungeon untouched by the jump (they finish their run first)?</summary>
        public static bool ShelteredAtJump(RealmKind realm) => realm == RealmKind.Dungeon;

        /// <summary>Where a player leaving a dungeon goes: the world when its cycle is still the one they left, else the city.</summary>
        public static RealmKind DungeonExitDestination(int cycleEntered, int currentCycle) =>
            cycleEntered == currentCycle ? RealmKind.World : RealmKind.City;

        /// <summary>Does a player leaving a dungeon pay the jump's penalties (their world is gone)?</summary>
        public static bool PenalisedOnDungeonExit(int cycleEntered, int currentCycle) => cycleEntered != currentCycle;

        /// <summary>
        /// May a player go out through the city gate now? Not while the world is being replaced (<paramref name="jumping"/>),
        /// and not in the last <paramref name="closeGateSeconds"/> before the jump (0 = open until the jump).
        /// </summary>
        public static bool GateOpen(bool jumping, double secondsLeft, float closeGateSeconds) =>
            !jumping && (closeGateSeconds <= 0f || secondsLeft > closeGateSeconds);
    }
}
