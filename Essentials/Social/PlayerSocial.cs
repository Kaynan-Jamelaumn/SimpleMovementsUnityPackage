using System;
using UnityEngine;

/// <summary>
/// Who a player is to other players: their guild (name and tag), their guild's alliance, their fame and title. Shown on
/// the target frame and the inspect window, and used by the combat rules: members of the same guild or alliance are
/// allies (Combat Settings ▸ Guild / Alliance Members Are Allies), so they cannot hurt each other except in a duel.
/// <para>Added to every player automatically (<see cref="For"/>); put it on the player prefab yourself to set a guild
/// for testing. Changes are made where the game rules run (offline, or on the server); in a network session the
/// values reach every client through the social relay, and a client's copy cannot be changed locally.</para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Player Social")]
public class PlayerSocial : MonoBehaviour
{
    [Tooltip("Guild name (empty = no guild).")]
    [SerializeField] private string guildName = "";
    [Tooltip("Short guild tag shown next to the name, e.g. KNT (empty = none).")]
    [SerializeField] private string guildTag = "";
    [Tooltip("Alliance of the player's guild (empty = none). Guilds of one alliance are allies.")]
    [SerializeField] private string allianceName = "";
    [Tooltip("Fame: renown earned by the player (shown when inspected). Change it with AddFame from your quests, kills, PvP...")]
    [SerializeField, Min(0)] private int fame;
    [Tooltip("Title shown under the name (\"the Brave\"), empty = none.")]
    [SerializeField] private string title = "";
    [Tooltip("Other players may inspect this player's equipment and stats.")]
    [SerializeField] private bool allowInspect = true;

    private CombatEntity entity;
    private int replicatedLevel;

    /// <summary>Any value changed (on every machine, also when the server's values arrive).</summary>
    public event Action Changed;

    public string GuildName => guildName ?? "";
    public string GuildTag => guildTag ?? "";
    public string AllianceName => allianceName ?? "";
    public int Fame => fame;
    public string Title => title ?? "";
    public bool AllowInspect => allowInspect;
    public bool HasGuild => !string.IsNullOrWhiteSpace(guildName);
    public bool HasAlliance => !string.IsNullOrWhiteSpace(allianceName);

    /// <summary>
    /// The player's level as every machine shows it: their Character Identity where the rules run, the server's value on
    /// network clients (0 = unknown).
    /// </summary>
    public int Level
    {
        get
        {
            if (!IsAuthority && replicatedLevel > 0)
                return replicatedLevel;
            CharacterIdentity id = Entity != null ? CharacterIdentity.For(Entity, false) : null;
            return id != null ? id.Level : replicatedLevel;
        }
    }

    /// <summary>A character's level for display (players through their social data; 0 = unknown).</summary>
    public static int LevelOf(CombatEntity e)
    {
        if (e == null)
            return 0;
        PlayerSocial s = For(e, false);
        if (s != null)
            return s.Level;
        CharacterIdentity id = CharacterIdentity.For(e, false);
        return id != null ? id.Level : 0;
    }

    /// <summary>The player's combat entity.</summary>
    public CombatEntity Entity
    {
        get
        {
            if (entity == null)
            {
                entity = GetComponent<CombatEntity>();
                if (entity == null) entity = GetComponentInChildren<CombatEntity>();
                if (entity == null) entity = GetComponentInParent<CombatEntity>();
            }
            return entity;
        }
    }

    /// <summary>"Name of Guild [TAG]", "Name of Guild" or "" (for the target frame).</summary>
    public string GuildLine
    {
        get
        {
            if (!HasGuild)
                return "";
            return string.IsNullOrWhiteSpace(guildTag) ? guildName : $"{guildName} [{guildTag}]";
        }
    }

    /// <summary>Can this machine change the values (offline, or the server)? A client's copy only shows the server's.</summary>
    public bool IsAuthority => !GameNet.NetAuthority.IsClientOnly && (Entity == null || !Entity.IsReplica);

    /// <summary>The social data of a player (added when missing and <paramref name="addIfMissing"/>), or null.</summary>
    public static PlayerSocial For(CombatEntity player, bool addIfMissing = true)
    {
        if (player == null)
            return null;
        PlayerSocial s = player.GetComponent<PlayerSocial>();
        if (s == null)
            s = player.GetComponentInParent<PlayerSocial>();
        if (s == null)
            s = player.GetComponentInChildren<PlayerSocial>();
        if (s == null && addIfMissing && player.Kind == CombatEntity.EntityKind.Player)
            s = player.gameObject.AddComponent<PlayerSocial>();
        return s;
    }

    /// <summary>Puts the player in a guild (null or "" = leaves it; the tag is cut to 5 characters).</summary>
    public void SetGuild(string name, string tag = "")
    {
        if (!IsAuthority)
            return;
        Set(Clean(name, 24), Clean(tag, 5).ToUpperInvariant(), allianceName, fame, title, allowInspect);
    }

    /// <summary>Sets the alliance (null or "" = none).</summary>
    public void SetAlliance(string name)
    {
        if (IsAuthority)
            Set(guildName, guildTag, Clean(name, 24), fame, title, allowInspect);
    }

    /// <summary>Adds (or with a negative amount removes) fame; never below 0.</summary>
    public void AddFame(int amount)
    {
        if (IsAuthority)
            Set(guildName, guildTag, allianceName, Mathf.Max(0, fame + amount), title, allowInspect);
    }

    public void SetFame(int value)
    {
        if (IsAuthority)
            Set(guildName, guildTag, allianceName, Mathf.Max(0, value), title, allowInspect);
    }

    public void SetTitle(string value)
    {
        if (IsAuthority)
            Set(guildName, guildTag, allianceName, fame, Clean(value, 32), allowInspect);
    }

    public void SetAllowInspect(bool value)
    {
        if (IsAuthority)
            Set(guildName, guildTag, allianceName, fame, title, value);
    }

    /// <summary>Network clients: the server's values (called by the social relay; ignored where the rules run).</summary>
    public void ApplyReplicated(string guild, string tag, string alliance, int fameValue, string titleValue, bool inspectable, int level)
    {
        if (IsAuthority)
            return;
        bool levelChanged = level != replicatedLevel;
        replicatedLevel = level;
        Set(guild ?? "", tag ?? "", alliance ?? "", fameValue, titleValue ?? "", inspectable);
        if (levelChanged)
            Changed?.Invoke();
    }

    private void Set(string guild, string tag, string alliance, int fameValue, string titleValue, bool inspectable)
    {
        if (guild == guildName && tag == guildTag && alliance == allianceName && fameValue == fame && titleValue == title && inspectable == allowInspect)
            return;
        guildName = guild;
        guildTag = tag;
        allianceName = alliance;
        fame = fameValue;
        title = titleValue;
        allowInspect = inspectable;
        Changed?.Invoke();
    }

    private static string Clean(string s, int max)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "";
        var sb = new System.Text.StringBuilder();
        foreach (char c in s.Trim())
        {
            if (c == '<' || c == '>' || char.IsControl(c))
                continue;
            sb.Append(c);
            if (sb.Length >= max)
                break;
        }
        return sb.ToString().Trim();
    }

    private void OnValidate()
    {
        guildTag = Clean(guildTag, 5);
        fame = Mathf.Max(0, fame);
        if (Application.isPlaying)
            Changed?.Invoke();
    }
}

/// <summary>Guild and alliance relations between players (and their summons), for the combat rules.</summary>
public static class CombatSocial
{
    /// <summary>Same guild?</summary>
    public static bool SameGuild(CombatEntity a, CombatEntity b)
    {
        PlayerSocial sa = SocialOf(a), sb = SocialOf(b);
        return sa != null && sb != null && sa != sb && sa.HasGuild && string.Equals(sa.GuildName, sb.GuildName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Guilds in the same alliance?</summary>
    public static bool SameAlliance(CombatEntity a, CombatEntity b)
    {
        PlayerSocial sa = SocialOf(a), sb = SocialOf(b);
        return sa != null && sb != null && sa != sb && sa.HasAlliance && string.Equals(sa.AllianceName, sb.AllianceName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Are the players behind <paramref name="a"/> and <paramref name="b"/> allies through their guild or alliance (with
    /// the Combat Settings switches on)? Two different players only.
    /// </summary>
    public static bool AreAffiliated(CombatEntity a, CombatEntity b)
    {
        if (a == null || b == null)
            return false;
        CombatSettings s = CombatSettings.Instance;
        if (s == null || (!s.guildMembersAreAllies && !s.allianceMembersAreAllies))
            return false;
        CombatEntity pa = CombatRules.ControllingPlayer(a), pb = CombatRules.ControllingPlayer(b);
        if (pa == null || pb == null || pa == pb)
            return false;
        return (s.guildMembersAreAllies && SameGuild(pa, pb)) || (s.allianceMembersAreAllies && SameAlliance(pa, pb));
    }

    private static readonly System.Collections.Generic.Dictionary<CombatEntity, PlayerSocial> Cache =
        new System.Collections.Generic.Dictionary<CombatEntity, PlayerSocial>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Cache.Clear();

    private static PlayerSocial SocialOf(CombatEntity e)
    {
        CombatEntity p = CombatRules.ControllingPlayer(e);
        if (p == null)
            return null;
        if (Cache.TryGetValue(p, out PlayerSocial s) && s != null)
            return s;
        if (Cache.Count > 512)
            Cache.Clear();
        s = PlayerSocial.For(p, false);
        if (s != null)
            Cache[p] = s;
        return s;
    }
}
