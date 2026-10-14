#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using HubWorld;
using UnityEditor;
using UnityEngine;

namespace SimpleMovements.Guides
{
    /// <summary>Guides of the world systems: weather, day / night, the hub world's director, realms, gates and zone borders.</summary>
    public static class WorldGuides
    {
        /// <summary>The guide drawn at the top of the Weather System's inspector.</summary>
        public static Guide Weather(WeatherSystem w)
        {
            var g = new Guide("WeatherSystem", "Weather System",
                    "Weather that drifts over the land: rain, snow, hail, fog, mist, wind, sandstorms, thunderstorms and tornadoes, decided by the biome and climate under each weather system and by the hour (misty mornings, stormy afternoons).")
                .Where("In the World scene (with the Terrain Generator): players in its scene get the weather. One per scene.")
                .How("Visual effects follow the viewer (the player out in the world); the director sets it.",
                     "Player Weather Effects (on the same object) turn the weather into conditions: Soaked, Chilled, Frostbite, Heatstroke, Sandblasted... Shelter (a roof over the player) protects.",
                     "The Day Night Cycle (in the City or Bootstrap scene) tells it the hour.",
                     "Multiplayer: the server's weather clock is sent to every client (Net World Chunks), so everyone sees the same sky.")
                .Doc("Terrain/README.md");
            GuideUtil.One<WeatherSystem>(g, "One Weather System", "Keep one, in the World scene.");
            bool worldScene = w.gameObject.scene.GetRootGameObjects().Any(r => r.GetComponentInChildren<TerrainGenerator>(true) != null || r.GetComponentInChildren<EndlessTerrain>(true) != null);
            g.Check(worldScene, "In the World scene", worldScene ? "With the terrain." : "No terrain in this scene: put the Weather System in the World scene.");
            bool effects = w.GetComponent<PlayerWeatherEffects>() != null;
            g.Check(effects, "Player Weather Effects", effects ? "On this object: the weather affects players." : "None: the weather is only visual.",
                "Add Player Weather Effects", () => WeatherSetupMenu.AddPlayerEffects(w));
            DayNightCycle clock = GuideUtil.InOpenScenes<DayNightCycle>().FirstOrDefault();
            if (clock != null)
                g.Check(true, "Day Night Cycle", $"In {GuideUtil.Where(clock)}.", select: clock);
            else
                g.Note("Day Night Cycle", "None in the open scenes (it may be in the City or Bootstrap scene). Without one there is no night.");
            return g;
        }

        public static Guide Npc(NPC npc) =>
            new Guide("NPC", "NPC",
                    "A character players talk to with the Interact key (or a click): it greets them, and its behaviours (Merchant, dialogue...) open from the conversation.")
                .Where("Anywhere in a scene (usually the City). The NPC needs a collider for clicks; the player needs a Player Interactor.")
                .How("Interaction range and priority decide which NPC the player's prompt shows when several are near.",
                     "Behaviours on the same object add the options (a Merchant adds Trade).",
                     "Multiplayer: the server runs the conversation; the NPC needs a Net Interactable (Network ▸ Make Selected Prefab Networked ▸ Interactable) when it is a network object.")
                .Doc("NPC/README.md");

        public static Guide Merchant(Merchant m) =>
            new Guide("Merchant", "Merchant",
                    "Sells and buys items for a currency, from a stock (its goods list, restocked over time) and a price list (sell / buy multipliers, categories).")
                .Where("On an NPC (it adds the Trade option to the conversation).")
                .How("Prices come from each item's Price times the merchant's multipliers; categories (Item Category Database) decide what it buys.",
                     "The player's Currency Wallet pays; Resources/DefaultCurrency is used when the merchant names none.",
                     "Multiplayer: the server checks every purchase and sale; the window on the client only asks.")
                .Doc("NPC/README.md");

        public static Guide Portal(Portal p)
        {
            PortalSlots s = p.ComputeSlots();
            return new Guide("Portal", "World Portal",
                    "Sends the players who walk in into a procedural dungeon (or loads a scene in single player).")
                .Where("Placed in a scene, or spawned by the terrain's Portal Spawner (Portal Settings ▸ Prefabs). Multiplayer: a network object (Network ▸ Make Selected Prefab Networked ▸ World Portal).")
                .How(s.Unlimited ? "No player limit." : $"Max Players {s.maxPlayers}: " + (s.shared ? "players are turned away while its dungeon is full." : "the group going in together is cut to it."),
                     s.shared ? "Shared: everyone who enters while its dungeon is going joins the same run." : "Separate Runs: every group gets a dungeon of its own.",
                     "After Use decides whether it stays, closes once entered, or closes once its dungeon is completed.",
                     "Players who select it see its free slots in the portal panel.")
                .Doc("HubWorld/06-Portal-Rules-Zones-Players-Links.md");
        }
    }

    [CustomEditor(typeof(DayNightCycle))]
    public class DayNightCycleGuideEditor : GuidedEditor
    {
        protected override Guide BuildGuide()
        {
            var c = (DayNightCycle)target;
            var g = new Guide("DayNightCycle", "Day Night Cycle",
                    "The world's clock (0-24 h): it turns the sun, darkens the light and the ambience at night, tells the Weather System the hour, and is what 'Day only' / 'Night only' spawn rules read.")
                .Where("In a scene that stays loaded: the City, or the Bootstrap scene in multiplayer. It keeps itself across scene changes (Persistent); a second one removes itself.")
                .How($"A day lasts {c.dayLengthMinutes:0.#} real minutes (nights {(c.nightSpeed == 1f ? "at the same speed" : $"×{c.nightSpeed:0.##} faster")}); sunrise {c.sunriseHour:0.#} h, sunset {c.sunsetHour:0.#} h; the game starts at {c.startHour:0.#} h.",
                     c.sun != null ? $"It turns '{c.sun.name}'." : "Sun empty: it uses the active scene's sun (Lighting ▸ Sun Source) or the first directional light.",
                     "Multiplayer: only the server's clock is real; clients get it every few seconds (Net World Chunks ▸ Day Clock) and count smoothly in between.")
                .Doc("Terrain/README.md");
            GuideUtil.One<DayNightCycle>(g, "One Day Night Cycle", "The extra ones remove themselves at runtime: keep one, in the City or Bootstrap scene.");
            bool stays = InSceneThatStaysPublic(c);
            g.Check(stays, "In a scene that stays loaded", stays ? $"'{c.gameObject.scene.name}'." : $"'{c.gameObject.scene.name}' is not the City or Bootstrap scene: when it unloads the clock goes with it (or a second clock fights it).");
            bool directional = c.sun != null || RenderSettings.sun != null || GuideUtil.InOpenScenes<Light>().Any(l => l.type == LightType.Directional);
            g.Check(directional, "A sun", directional ? "A directional light to turn." : "No directional light in the open scenes: day and night only change the ambience.");
            if (Application.isPlaying)
                g.Live("Time", $"{Mathf.FloorToInt(c.Hour):00}:{Mathf.FloorToInt(c.Hour % 1f * 60f):00}  ({c.Phase})");
            return g;
        }

        private static bool InSceneThatStaysPublic(Component c)
        {
            var scene = c.gameObject.scene;
            if (scene.GetRootGameObjects().Any(r => r.GetComponentsInChildren<RealmScene>(true).Any(rs => rs.realm == RealmKind.City)))
                return true;
            return scene.name.Contains("Bootstrap") || scene.GetRootGameObjects().Any(r => r.name == "NetworkManager" || r.name == "Network World");
        }
    }

    [CustomEditor(typeof(PlayerWeatherEffects))]
    public class PlayerWeatherEffectsGuideEditor : GuidedEditor
    {
        protected override Guide BuildGuide()
        {
            var fx = (PlayerWeatherEffects)target;
            var g = new Guide("PlayerWeatherEffects", "Player Weather Effects",
                    "Makes the weather matter: players exposed to it get conditions - Soaked, Chilled, Frostbite, Heatstroke, Sandblasted, Windswept, Pelted by Hail - that change their stats, resistances, status bars or deal damage.")
                .Where("One for all players, on the Weather System object (Weather ▸ Add Player Weather Effects). Never on a player.")
                .How("Each condition has a measure (rain, snow, wind, heat, cold...) and a threshold; it grows with the weather's intensity and lingers a moment after.",
                     "Shelter: a roof above the player (Shelter Layers) blocks conditions that need the open sky; being underwater blocks others.",
                     "Conditions: assets in Conditions (Weather ▸ Create Default Weather Condition Assets writes editable ones), or inline Rules. A condition in both applies twice.",
                     "Multiplayer: the server applies conditions; clients see them through the status replication.")
                .Doc("Terrain/README.md");
            var issues = new List<Validation.ValidationIssue>();
            Validation.SetupChecks.CheckOpenScenes(issues);
            List<Validation.ValidationIssue> mine = issues.Where(i => i.category == "Weather" && i.context == fx).ToList();
            bool onWeather = fx.GetComponent<WeatherSystem>() != null;
            g.Check(onWeather, "On the Weather System object", onWeather ? "Yes." : "Not next to a Weather System: put it on the World scene's Weather System object.");
            foreach (Validation.ValidationIssue i in mine)
            {
                Validation.ValidationIssue captured = i;
                g.Check(false, i.fixLabel ?? "Weather", i.message, i.fixLabel, i.CanFix ? () => captured.fix() : (System.Action)null);
            }
            if (mine.Count == 0)
                g.Check(true, "Rules and shelter", "No rule applies twice; shelter does not hit the players themselves.");
            g.Note("Conditions", $"{fx.conditions.Count(c => c != null)} condition asset(s), {fx.rules.Count(r => r != null)} inline rule(s).");
            return g;
        }
    }

    [CustomEditor(typeof(HubWorldDirector))]
    public class HubWorldDirectorGuideEditor : GuidedEditor
    {
        protected override Guide BuildGuide()
        {
            var d = (HubWorldDirector)target;
            HubWorldSettings s = d.Settings != null ? d.Settings : Setup.SetupUtil.HubSettings();
            var g = new Guide("HubWorldDirector", "Hub World Director",
                    "Runs the hub world: the permanent City, the World regenerated with a new seed every dimensional cycle, the dungeons, travel between them, the zones (safe and PvP rings), duels and the cycle's penalties.")
                .Where("In the City scene, or in the Bootstrap scene in multiplayer (one in total). It adds the HUD, the duel system and the zone borders next to itself.")
                .How($"A cycle lasts {s.cycleMinutes:0.#} minutes; warnings before the jump; the City Gate closes {s.closeGateSecondsBeforeJump:0} s before it.",
                     $"Scenes: City '{s.citySceneName}', World '{s.worldSceneName}'{(s.dungeonsInOwnScenes ? ", each dungeon in its own scene" : "")}.",
                     "Zones: rings around the city (Hub World Settings ▸ Zones). PvP only when both players stand in a PvP ring; the city and safe rings never.",
                     "Multiplayer: the server's director decides; clients mirror it (Net Hub World relay).")
                .Doc("HubWorld/README.md")
                .Action("Check City Scene", "Scene and Prefab Setup ▸ Hub City Scene", () => Setup.SceneAndPrefabSetupWindow.Open("city"))
                .Action("Check World Scene", "Scene and Prefab Setup ▸ World Scene", () => Setup.SceneAndPrefabSetupWindow.Open("world"));
            GuideUtil.One<HubWorldDirector>(g, "One director", "Keep one, in the City or Bootstrap scene.");
            bool settings = Setup.SetupUtil.HubSettingsAsset() != null;
            g.Check(settings, "Hub World Settings asset", settings ? "Resources/HubWorldSettings." : "None: the defaults are used and cannot be changed.",
                "Create", () => HubWorld.EditorTools.HubWorldSetupMenu.CreateSettings());
            bool inBuild = Setup.SetupUtil.InBuildByName(s.citySceneName) && Setup.SetupUtil.InBuildByName(s.worldSceneName);
            g.Check(inBuild, "City and World scenes in the build", inBuild ? "Both." : "One or both are missing from Build Settings: open their pages in Scene and Prefab Setup.");
            GuideUtil.Relay(g, "GameNet.NetHubWorld", "The world cycle and hub messages");
            if (Application.isPlaying)
            {
                g.Live("Cycle", $"#{d.CycleIndex}, jump in {HubWorldMessages.FormatSpan(d.SecondsUntilJump)}{(d.CurrentModifier != null ? $", dimension {d.CurrentModifier.displayName}" : "")}");
                g.Live("Players", d.Players.Count == 0 ? "none" : string.Join("\n", d.Players.Where(p => p != null && p.Entity != null).Select(p => $"{p.Entity.name}: {p.Realm}{(string.IsNullOrEmpty(p.Zone.name) ? "" : " - " + p.Zone.name)}")));
            }
            return g;
        }
    }

    [CustomEditor(typeof(RealmScene))]
    public class RealmSceneGuideEditor : GuidedEditor
    {
        protected override Guide BuildGuide()
        {
            var r = (RealmScene)target;
            HubWorldSettings s = Setup.SetupUtil.HubSettings();
            var g = new Guide("RealmScene", "Realm Scene",
                    "Tells the hub world which realm this scene is (City or World) and where players appear in it.")
                .Where("One per realm scene, on a root object ('Realm: City' / 'Realm: World').")
                .How("Arrival Point: where players appear when they travel in (inside the City Gate / outside the Return Gate). Spawn Point (City): the start and respawn point. Several players are spread around the point.",
                     "Hide While Local Player Away: hides this scene's objects on this machine while its player is elsewhere (the City while you are out in the World).")
                .Doc("HubWorld/05-Setup-and-Configuration.md")
                .Action("Check This Scene", "Scene and Prefab Setup, page of this scene", () =>
                    Setup.SceneAndPrefabSetupWindow.Open(r.realm == RealmKind.World ? "world" : "city", r.gameObject.scene.path));
            string expected = r.realm == RealmKind.City ? s.citySceneName : r.realm == RealmKind.World ? s.worldSceneName : null;
            if (expected != null)
                g.Check(expected == r.gameObject.scene.name, "Scene name in the Hub World Settings",
                    expected == r.gameObject.scene.name ? $"'{expected}'." : $"The settings name the {r.realm} '{expected}', this scene is '{r.gameObject.scene.name}': it will not be loaded as the {r.realm}.");
            g.Check(r.arrivalPoint != null, "Arrival Point", r.arrivalPoint != null ? $"'{r.arrivalPoint.name}'." : "Empty: this object's position is used.");
            if (r.realm == RealmKind.City)
                g.Check(r.spawnPoint != null, "Spawn Point", r.spawnPoint != null ? $"'{r.spawnPoint.name}'." : "Empty: the Arrival Point is used.");
            int count = r.gameObject.scene.GetRootGameObjects().Sum(go => go.GetComponentsInChildren<RealmScene>(true).Length);
            g.Check(count == 1, "One per scene", count == 1 ? "Yes." : $"{count} Realm Scenes in '{r.gameObject.scene.name}': keep one.");
            return g;
        }
    }

    [CustomEditor(typeof(RealmGate))]
    public class RealmGateGuideEditor : GuidedEditor
    {
        protected override Guide BuildGuide()
        {
            var gate = (RealmGate)target;
            bool city = gate.kind == RealmGate.GateKind.CityGate;
            var g = new Guide("RealmGate", city ? "City Gate" : "Return Gate",
                    city ? "Walking through it takes the player out of the city into the World." : "Walking through it brings the player from the World back into the city.")
                .Where(city ? "In the City scene, in the city's wall." : "In the World scene, in the outside copy of the city's wall.")
                .How("Needs a trigger collider on the same object; the player must move with a Character Controller or a Rigidbody for triggers to fire.",
                     $"Arm Delay ({gate.armDelay:0.#} s): a player who just arrived through it is not sent straight back.",
                     city ? "The gate closes shortly before the city jumps (Hub World Settings ▸ Close Gate Seconds Before Jump)." : "Travel back is always allowed.",
                     "Multiplayer: the server decides; the client asks it when its player walks in.")
                .Doc("HubWorld/05-Setup-and-Configuration.md");
            Collider c = gate.GetComponent<Collider>();
            g.Check(c != null && c.isTrigger, "Trigger collider", c == null ? "No collider: nobody can walk through it." : c.isTrigger ? "Yes." : "The collider is not a trigger: it blocks instead.",
                c != null && !c.isTrigger ? "Make It A Trigger" : c == null ? "Add Box Trigger" : null, () =>
                {
                    Collider col = gate.GetComponent<Collider>();
                    if (col == null)
                        col = Undo.AddComponent<BoxCollider>(gate.gameObject);
                    Undo.RecordObject(col, "Trigger");
                    col.isTrigger = true;
                    EditorUtility.SetDirty(col);
                });
            RealmScene realm = gate.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RealmScene>(true)).FirstOrDefault();
            RealmKind wanted = city ? RealmKind.City : RealmKind.World;
            if (realm == null)
                g.Note("Scene", "This scene has no Realm Scene yet.");
            else
                g.Check(realm.realm == wanted, "In the right scene", realm.realm == wanted ? $"The {wanted} scene." : $"This is the {realm.realm} scene: a {(city ? "City" : "Return")} Gate belongs in the {wanted} scene.");
            return g;
        }
    }

    [CustomEditor(typeof(ZoneBorders))]
    public class ZoneBordersGuideEditor : GuidedEditor
    {
        protected override Guide BuildGuide()
        {
            var z = (ZoneBorders)target;
            return new Guide("ZoneBorders", "Zone Borders",
                    "Makes the borders of the world's rings visible to this machine's player: a glowing line on the ground and a translucent wall that players walk straight through.")
                .Where("Added next to the Hub World Director automatically (its Show Zone Borders), never on a dedicated server. Shown only while the player is out in the World.")
                .How(z.borders == ZoneBorders.Which.SafeZoneEdges ? "Draws only the edges of the safe zones: where PvP turns on or off." : "Draws every ring border (and the city walls).",
                     $"Appears within {z.showDistance:0} m of a border and gets stronger as the player comes near; {z.arcLength:0} m of it are built around the player, following the ground.",
                     "The rings come from Hub World Settings ▸ Zones ▸ Rings (each ring's PvP setting decides where the safe edges are).")
                .Doc("HubWorld/03-Zones-PvP-Difficulty.md");
        }
    }
}
#endif
