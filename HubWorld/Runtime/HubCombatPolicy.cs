using ProceduralDungeon;

namespace HubWorld
{
    /// <summary>
    /// The hub world's combat rules (registered with <see cref="CombatRules"/> by the <see cref="HubWorldDirector"/>):
    /// <list type="bullet">
    /// <item>City and travel: no PvP, abilities are only shown.</item>
    /// <item>World: PvP only when BOTH players stand in a PvP ring, so nobody can be attacked from (or hide in) a safe zone.</item>
    /// <item>Dungeons: PvP between members of the same run only when Zone Settings ▸ PvP In Dungeons is on.</item>
    /// <item>Players in different realms never fight.</item>
    /// </list>
    /// Duels (<see cref="DuelSystem"/>, higher priority) can still allow two players to fight in the city.
    /// </summary>
    public sealed class HubCombatPolicy : ICombatRuleProvider
    {
        private readonly HubWorldSettings settings;

        public HubCombatPolicy(HubWorldSettings settings)
        {
            this.settings = settings;
        }

        public int Priority => 0;

        public PvpVerdict PlayerVersusPlayer(CombatEntity a, CombatEntity b)
        {
            PlayerRealmState sa = PlayerRealmState.Of(a), sb = PlayerRealmState.Of(b);
            if (sa == null || sb == null || sa.Realm == RealmKind.None || sb.Realm == RealmKind.None)
                return PvpVerdict.Unspecified; // not placed by the hub world: the usual rules decide
            if (sa.Realm != sb.Realm)
                return PvpVerdict.Forbidden;
            switch (sa.Realm)
            {
                case RealmKind.World:
                    return sa.Zone.pvp && sb.Zone.pvp ? PvpVerdict.Allowed : PvpVerdict.Forbidden;
                case RealmKind.Dungeon:
                    if (!settings.zones.pvpInDungeons)
                        return PvpVerdict.Forbidden;
                    DungeonRun run = DungeonSession.RunOf(a.gameObject);
                    return run != null && run == DungeonSession.RunOf(b.gameObject) ? PvpVerdict.Allowed : PvpVerdict.Forbidden;
                default:
                    return PvpVerdict.Forbidden; // the city, players in transit
            }
        }

        public bool? AbilitiesVisualOnly(CombatEntity caster)
        {
            if (!settings.cityAbilitiesVisualOnly)
                return null;
            PlayerRealmState s = PlayerRealmState.Of(CombatRules.ControllingPlayer(caster));
            if (s == null)
                return null;
            return s.InCity || s.InTransit ? true : (bool?)null;
        }
    }
}
