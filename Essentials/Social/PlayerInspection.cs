using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One equipped item of an inspected player.</summary>
[Serializable]
public struct InspectItem
{
    /// <summary>Where it is worn ("Helmet", "Main Hand"...).</summary>
    public string slot;
    /// <summary>Item id in the Item Database (the icon and tooltip come from it).</summary>
    public string itemId;
    /// <summary>The item's name (shown when the database does not know the id).</summary>
    public string itemName;
}

/// <summary>One line of an inspected player's stats ("Defense", "42").</summary>
[Serializable]
public struct InspectStat
{
    public string label;
    public string value;
}

/// <summary>What inspecting a player shows: who they are, their stats and their equipment.</summary>
[Serializable]
public struct InspectData
{
    /// <summary>The inspected player (HubWorld.PlayerIds).</summary>
    public int playerId;
    /// <summary>Empty when it worked, else why not ("Too far away.").</summary>
    public string error;
    public string name;
    public string title;
    public string guild;
    public string guildTag;
    public string alliance;
    public int fame;
    public int level;
    public string race;
    public string className;
    public float health;
    public float maxHealth;
    public InspectStat[] stats;
    public InspectItem[] equipment;

    public bool Ok => string.IsNullOrEmpty(error);

    public static InspectData Failed(int playerId, string why) => new InspectData { playerId = playerId, error = why, stats = Array.Empty<InspectStat>(), equipment = Array.Empty<InspectItem>() };
}

/// <summary>
/// Inspecting another player (the Inspect key on the current target, or the target frame's menu): their name, guild,
/// alliance, title, fame, level, race and class, health, combat stats and every equipped item.
/// <para>The data is gathered where the game rules run (<see cref="Build"/>), because only there are a player's
/// inventory and stats complete: offline at once, in a network session by the server, which checks distance and
/// the player's Allow Inspect, and sends the answer only to the player who asked (<see cref="Deliver"/>).</para>
/// </summary>
public static class PlayerInspection
{
    /// <summary>Farthest a player can be inspected from (meters).</summary>
    public static float MaxDistance = 40f;

    /// <summary>The answer to an inspection arrived (on the machine of the player who asked).</summary>
    public static event Action<InspectData> Received;

    /// <summary>
    /// Network clients: asks the server to inspect a player (by id). Installed by the networking layer; null offline
    /// and on the server.
    /// </summary>
    public static Action<int> ClientRequest;

    private static readonly CombatStatType[] ShownStats =
    {
        CombatStatType.Strength, CombatStatType.Agility, CombatStatType.Intelligence, CombatStatType.Endurance,
        CombatStatType.Defense, CombatStatType.MagicResistance, CombatStatType.CriticalChance, CombatStatType.CriticalDamage,
        CombatStatType.AttackSpeed, CombatStatType.CastingSpeed, CombatStatType.WeaponDamage,
    };

    private static readonly List<InspectItem> itemBuffer = new List<InspectItem>();
    private static readonly List<InspectStat> statBuffer = new List<InspectStat>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Received = null;
        ClientRequest = null;
        MaxDistance = 40f;
    }

    /// <summary><paramref name="viewer"/> inspects <paramref name="target"/> (the answer comes through <see cref="Received"/>).</summary>
    public static void Request(CombatEntity viewer, CombatEntity target)
    {
        int id = HubWorld.PlayerIds.Of(target);
        if (GameNet.NetAuthority.IsClientOnly)
        {
            if (ClientRequest != null && id > 0)
                ClientRequest(id);
            else
                Deliver(InspectData.Failed(id, "Not connected to the server."));
            return;
        }
        Deliver(Build(viewer, target));
    }

    /// <summary>Raises <see cref="Received"/> on this machine (the networking layer calls it with the server's answer).</summary>
    public static void Deliver(InspectData data)
    {
        try { Received?.Invoke(data); }
        catch (Exception e) { Debug.LogException(e); }
    }

    /// <summary>Can <paramref name="viewer"/> inspect <paramref name="target"/> now? (Reason when not.)</summary>
    public static bool CanInspect(CombatEntity viewer, CombatEntity target, out string reason)
    {
        reason = null;
        if (target == null || target.Kind != CombatEntity.EntityKind.Player)
        {
            reason = "Only players can be inspected.";
            return false;
        }
        if (viewer != null && viewer != target)
        {
            if (CombatRules.AreSeparated(viewer, target))
            {
                reason = "They are not here.";
                return false;
            }
            if (MaxDistance > 0f && Vector3.Distance(viewer.Position, target.Position) > MaxDistance)
            {
                reason = "Too far away to inspect.";
                return false;
            }
            PlayerSocial social = PlayerSocial.For(target, false);
            if (social != null && !social.AllowInspect)
            {
                reason = $"{PartySystem.NameOf(target)} does not allow inspection.";
                return false;
            }
        }
        return true;
    }

    /// <summary>Gathers what <paramref name="viewer"/> sees when inspecting <paramref name="target"/> (where the rules run).</summary>
    public static InspectData Build(CombatEntity viewer, CombatEntity target)
    {
        int id = HubWorld.PlayerIds.Of(target);
        if (!CanInspect(viewer, target, out string reason))
            return InspectData.Failed(id, reason);

        var data = new InspectData { playerId = id, error = "", name = PartySystem.NameOf(target) };
        PlayerSocial social = PlayerSocial.For(target, false);
        if (social != null)
        {
            data.title = social.Title;
            data.guild = social.GuildName;
            data.guildTag = social.GuildTag;
            data.alliance = social.AllianceName;
            data.fame = social.Fame;
        }
        CharacterIdentity identity = CharacterIdentity.For(target, false);
        if (identity != null)
        {
            data.level = identity.Level;
            data.race = identity.Race != null ? identity.Race.Name : "";
            data.className = identity.Class != null ? identity.Class.className : "";
        }
        data.health = target.MaxHealth * target.HealthRatio;
        data.maxHealth = target.MaxHealth;

        // Stats: the attributes always, the rest when they are not zero.
        statBuffer.Clear();
        CombatStats stats = CombatStats.For(target, false);
        EquipmentManager equipment = EquipmentManager.For(target, false);
        if (stats != null)
        {
            for (int i = 0; i < ShownStats.Length; i++)
            {
                CombatStatType s = ShownStats[i];
                float v = stats.Get(s);
                bool attribute = i < 4;
                if (!attribute && Mathf.Abs(v) < 0.01f)
                    continue;
                CombatStatInfo info = CombatStatInfo.Get(s);
                statBuffer.Add(new InspectStat { label = info.name, value = info.isPercent ? $"{v:0.#}%" : $"{v:0.#}" });
            }
        }
        if (equipment != null)
        {
            equipment.GetArmorDefense(out float defense, out float magicDefense);
            if (defense > 0f)
                statBuffer.Add(new InspectStat { label = "Armor Defense", value = $"{defense:0.#}" });
            if (magicDefense > 0f)
                statBuffer.Add(new InspectStat { label = "Armor Magic Defense", value = $"{magicDefense:0.#}" });
        }
        data.stats = statBuffer.ToArray();

        // Equipment: every equipped item (armor, jewelry, the weapon in hand).
        itemBuffer.Clear();
        if (equipment != null)
        {
            foreach (EquipmentManager.Entry e in equipment.Entries)
            {
                if (e == null || e.Item == null)
                    continue;
                itemBuffer.Add(new InspectItem
                {
                    slot = string.IsNullOrEmpty(e.Slot) ? "Equipped" : e.Slot,
                    itemId = ItemDatabase.Id(e.Item) ?? "",
                    itemName = e.Item.Name,
                });
            }
        }
        data.equipment = itemBuffer.ToArray();
        return data;
    }
}
