#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SimpleMovements.Guides
{
    /// <summary>Guides of the social systems: targeting, parties, guilds, nameplates, highlights, the portal panel.</summary>
    internal static class SocialGuideText
    {
        public const string Doc = "Player/09-Targeting-Party-Guild-Inspect.md";

        /// <summary>Input actions of the targeting keys missing from the project's Input Actions asset.</summary>
        public static void InputActions(Guide g, params string[] names)
        {
            var issues = new List<Validation.ValidationIssue>();
            Validation.SetupChecks.CheckInputActions(issues);
            List<Validation.ValidationIssue> missing = issues.Where(i => i.fixLabel == "Add" && names.Any(n => i.message.StartsWith($"'{n}'"))).ToList();
            g.Check(missing.Count == 0, "Keys in the Input Actions asset",
                missing.Count == 0 ? $"{string.Join(", ", names)} are there (rebindable in the game's Key Bindings)."
                    : $"Missing: {string.Join(", ", missing.Select(i => i.message.Split('\'')[1]))}. They still work on their default keys, but players cannot rebind them.",
                "Add Missing Actions", () => missing.ForEach(i => i.fix()));
        }
    }

    [CustomEditor(typeof(PlayerTargeting))]
    public class PlayerTargetingGuideEditor : GuidedEditor
    {
        protected override Guide BuildGuide()
        {
            var t = (PlayerTargeting)target;
            var g = new Guide("PlayerTargeting", "Player Targeting",
                    "Lets this machine's player select another player (or a mob) as their target. The target gets a coloured ring under its feet and the target frame in the top-left corner, whose menu invites to a party, trades, challenges to a duel and inspects.")
                .Where("Created by itself when a game starts with a player (one per machine, never on a dedicated server). Put one in a scene that stays loaded (City or Bootstrap) only to change these settings.")
                .How("Select Target (left click, or the middle of the screen while the cursor is locked), Cycle Target (Tab), Inspect (U), Target Menu (G) - all rebindable in Key Bindings.",
                     "Ring and name colour: red = can fight you here, green = your party, blue = an ally (guild, alliance, safe zone), grey = neutral.",
                     "Create Interface builds the target frame, party frame, inspect window, the nameplates and highlights of other players and the dungeon portal panel - each only when the scene has none of its own (put one on this object to change its settings).",
                     "Clicking a dungeon portal selects it: the portal panel shows how many player slots it has left.")
                .Doc(SocialGuideText.Doc);
            GuideUtil.One<PlayerTargeting>(g, "One Player Targeting", "Keep one: the other is destroyed at runtime.");
            SocialGuideText.InputActions(g, "SelectTarget", "CycleTarget", "InspectTarget", "TargetMenu");
            CombatSettings settings = SMAssets.Load<CombatSettings>("CombatSettings");
            if (settings != null && (t.selectLayers.value & settings.characterLayers.value) == 0)
                g.Check(false, "Select Layers", "Select Layers include none of the Combat Settings' Character Layers: clicks can never hit a player.");
            g.Note("Interface", t.createInterface ? "Create Interface is on: the frames, nameplates, highlights and portal panel are created at runtime." : "Create Interface is off: use your own UI (events TargetChanged, MenuRequested, PortalSelected).");
            if (Application.isPlaying)
            {
                g.Live("Target", t.Target != null ? $"{t.Target.name} ({PlayerTargeting.RelationOf(t.Target)})" : "none");
                g.Live("Selected portal", t.SelectedPortal != null ? t.SelectedPortal.name : "none");
            }
            return g;
        }
    }

    [CustomEditor(typeof(PartySystem))]
    public class PartySystemGuideEditor : GuidedEditor
    {
        protected override Guide BuildGuide()
        {
            var ps = (PartySystem)target;
            CombatSettings cs = SMAssets.Load<CombatSettings>("CombatSettings");
            var g = new Guide("PartySystem", "Party System",
                    "Parties of players: invite (target menu), accept or decline (Y / N, or the prompt), leave, remove a member, hand over the lead. Party members are allies: their heals and buffs reach each other, they cannot hurt each other, and a duel still lets two of them fight.")
                .Where("Created by itself when first needed and kept across scene changes. Put one in a scene that stays loaded only to change the Rules (party size, invitation time, who may invite).")
                .How($"Rules: up to {ps.Rules.maxMembers} members, invitations wait {ps.Rules.inviteSeconds:0} s, {(ps.Rules.onlyLeaderInvites ? "only the leader invites" : "any member invites")}, {(ps.Rules.disbandWhenAlone ? "a party of one is disbanded" : "a party of one stays")}.",
                     "Friendly fire between members: Combat Settings ▸ Party Friendly Fire" + (cs != null ? $" (now {(cs.partyFriendlyFire ? "ON" : "off")})." : " (off by default)."),
                     "Multiplayer: the server's party system decides; every client gets every party (Net Social relay), so all machines show the same colours, nameplates and highlights.",
                     "Players who disconnect leave their party after 10 seconds.")
                .Doc(SocialGuideText.Doc);
            GuideUtil.One<PartySystem>(g, "One Party System", "Keep one: the other removes itself at runtime.");
            GuideUtil.Relay(g, "GameNet.NetSocial", "Parties, invitations, guilds and inspection");
            if (Application.isPlaying)
            {
                g.Live("Parties", ps.Book.Parties.Count == 0 ? "none" : string.Join("\n", ps.Book.Parties.Select(p =>
                    $"#{p.Id}: " + string.Join(", ", p.Members.Select(m => PartySystem.NameOf(m) + (m == p.Leader ? " (leader)" : ""))))));
                g.Live("Invitations", ps.Book.Invites.Count == 0 ? "none" : string.Join("\n", ps.Book.Invites.Select(i => $"{PartySystem.NameOf(i.from)} → {PartySystem.NameOf(i.to)}")));
            }
            return g;
        }
    }

    [CustomEditor(typeof(PlayerSocial))]
    public class PlayerSocialGuideEditor : GuidedEditor
    {
        protected override Guide BuildGuide()
        {
            var s = (PlayerSocial)target;
            CombatSettings cs = SMAssets.Load<CombatSettings>("CombatSettings");
            var g = new Guide("PlayerSocial", "Player Social",
                    "Who this player is to others: guild (name and tag), alliance, fame and title. Shown on the target frame, the nameplates and the inspect window, and used by the combat rules.")
                .Where("On the player (next to the Player Status Controller). Every player gets one automatically at runtime; put it on the prefab only to set values for testing.")
                .How("Guild and alliance members are allies (Combat Settings ▸ Guild / Alliance Members Are Allies" + (cs != null ? $": now {(cs.guildMembersAreAllies ? "on" : "off")} / {(cs.allianceMembersAreAllies ? "on" : "off")}" : "") + "): they cannot hurt each other except in a duel.",
                     "Change it from your game code where the rules run (offline, or the server): SetGuild, SetAlliance, AddFame, SetTitle, SetAllowInspect. A client's copy only shows the server's values (Net Social relay).",
                     "Allow Inspect off: other players cannot see this player's equipment and stats.")
                .Doc(SocialGuideText.Doc);
            bool onPlayer = s.GetComponentInParent<PlayerStatusController>() != null || s.GetComponentInChildren<PlayerStatusController>(true) != null;
            g.Check(onPlayer, "On a player", onPlayer ? "Yes." : "Not on a player (no Player Status Controller here, above or below): it does nothing.");
            GuideUtil.Relay(g, "GameNet.NetSocial", "Guilds, fame and levels of other players");
            if (Application.isPlaying)
            {
                g.Live("Shown as", string.IsNullOrEmpty(s.GuildLine) ? "(no guild)" : s.GuildLine);
                g.Live("Level", s.Level > 0 ? s.Level.ToString() : "unknown");
            }
            return g;
        }
    }

    [CustomEditor(typeof(PlayerNameplates))]
    public class PlayerNameplatesGuideEditor : GuidedEditor
    {
        protected override Guide BuildGuide()
        {
            var n = (PlayerNameplates)target;
            var g = new Guide("PlayerNameplates", "Player Nameplates",
                    "Names and health bars above other players' heads, on this machine's screen.")
                .Where("Created next to the Player Targeting (its Create Interface). Put one on the Player Targeting object to change these settings.")
                .How($"Every other player within {n.maxDistance:0} m shows their name (coloured by relation) and guild tag; farther ones (or in another realm) show nothing, fading out over the last {n.fadeDistance:0} m.",
                     "A health bar shows for: " + string.Join(", ", new[] { n.barsForParty ? "party members" : null, n.barsForDuels ? "duellists" : null, n.barsInCombat ? $"players in combat (hit or hitting in the last {n.combatSeconds:0} s)" : null, n.barsForGuild ? "guild and alliance members" : null, n.alwaysShowBars ? "everyone" : null }.Where(x => x != null)) + ".",
                     "Bar colours: green = your party / guild / alliance, orange = duel, red = can attack you, amber = everyone else.",
                     "Hide Behind Walls tests the line of sight a few times a second per player (off by default).")
                .Doc(SocialGuideText.Doc);
            GuideUtil.One<PlayerNameplates>(g, "One Player Nameplates", "Keep one: the other removes itself.");
            GuideUtil.Relay(g, "GameNet.NetSocial", "Party colours and party health bars of other players");
            return g;
        }
    }

    [CustomEditor(typeof(PlayerHighlights))]
    public class PlayerHighlightsGuideEditor : GuidedEditor
    {
        protected override Guide BuildGuide()
        {
            var h = (PlayerHighlights)target;
            bool shader = Shader.Find("SimpleMovements/Player Silhouette") != null;
            var g = new Guide("PlayerHighlights", "Player Highlights",
                    "Subtle marks on the players bound to you: a coloured outline glow on party, guild and alliance members, a faint silhouette through walls for party members, and a distinct orange silhouette with a ground glow for anyone in a duel.")
                .Where("Created next to the Player Targeting (its Create Interface). Put one on the Player Targeting object to change the styles.")
                .How("Each bond (Party, Guild, Alliance, Duel) has a Style: colour (alpha = strength), Outline Glow, Through Walls, Ground Glow. A duel wins over the other bonds, then party, guild, alliance.",
                     $"Only players within {h.maxDistance:0} m and in your realm are marked.",
                     "The glow and the silhouette are extra materials added to the character's mesh renderers and removed when the bond ends. A renderer with several sub-meshes is marked on its last sub-mesh only.")
                .Doc(SocialGuideText.Doc);
            g.Check(shader, "Silhouette shader", shader ? "'SimpleMovements/Player Silhouette' found (always in builds: it is in a Resources folder)."
                : "Not found: only the ground glow is used. Keep Essentials/Social/Resources/SimpleMovementsSilhouette.shader in the project.");
            GuideUtil.Relay(g, "GameNet.NetSocial", "Party, guild and alliance highlights of other players");
            return g;
        }
    }

    [CustomEditor(typeof(PortalInfoPanel))]
    public class PortalInfoPanelGuideEditor : GuidedEditor
    {
        protected override Guide BuildGuide()
        {
            var p = (PortalInfoPanel)target;
            var g = new Guide("PortalInfoPanel", "Portal Info Panel",
                    "When the player clicks a dungeon portal (or walks up to one) a panel at the top of the screen shows how many player slots it has left, whether a shared run is going and how many are inside, and the dungeon's difficulty.")
                .Where("Created next to the Player Targeting (its Create Interface).")
                .How($"Shown for the selected portal while within {p.keepDistance:0} m, else for the nearest open dungeon portal within {p.nearDistance:0} m (Near Distance 0 = only selected portals).",
                     "The numbers come from the portal's Max Players and Sharing (or its spawner type's) and from its running shared or linked dungeon.",
                     "Multiplayer: the client asks the server about once a second while the panel is shown (Net Portals relay); until the answer arrives the portal's own settings are shown.")
                .Doc("HubWorld/06-Portal-Rules-Zones-Players-Links.md");
            GuideUtil.Relay(g, "GameNet.NetPortals", "The free slots of dungeon portals");
            if (Application.isPlaying && p.Shown != null)
            {
                PortalSlots s = p.Shown.Slots;
                g.Live("Portal", p.Shown.name);
                g.Live("Slots", s.Unlimited ? "no limit" : $"{s.Remaining} of {s.maxPlayers} free{(s.running ? $", {s.inside} inside" : "")}{(s.known ? "" : " (waiting for the server)")}");
            }
            return g;
        }
    }
}
#endif
