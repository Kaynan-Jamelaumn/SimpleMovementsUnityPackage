#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HubWorld;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
#if FISHNET
using FishNet.Managing;
using FishNet.Managing.Observing;
using FishNet.Object;
using FishNet.Observing;
using GameNet;
using GameNet.EditorTools;
#endif

namespace SimpleMovements.Setup
{
    /// <summary>
    /// What each scene of the game needs: the Bootstrap scene (multiplayer), the Hub City scene, the World scene and the
    /// Dungeon instance scene. Shown by the Scene &amp; Prefab Setup window.
    /// </summary>
    public static class SceneProfiles
    {
        // ================================================================== Bootstrap

        public static SetupProfile Bootstrap()
        {
            var p = new SetupProfile
            {
                Id = "bootstrap",
                Name = "Bootstrap Scene",
                Kind = SetupTargetKind.Scene,
                Summary = "Multiplayer entry point: the NetworkManager and the global relays.",
                Description =
                    "The first scene of a multiplayer build. It holds Fish-Net's NetworkManager (with the package's session " +
                    "components: start menu, realm scenes, player spawner) and the 'Network World' object whose relays keep the " +
                    "world cycle, duels, trading, parties, terrain changes and portal slots in sync for every client.\n\n" +
                    "It contains nothing else: the City, World and dungeon scenes are loaded through the network for each " +
                    "player. Single-player games that never go online do not need it.",
                DocPath = "Networking/01-Architecture-and-Local-Setup.md",
                FindDefault = () =>
                {
#if FISHNET
                    if (File.Exists(NetworkSetupMenu.BootstrapPath))
                        return NetworkSetupMenu.BootstrapPath;
#endif
                    return SetupUtil.ScenePathByName("Bootstrap");
                },
#if FISHNET
                CreateLabel = "Create Bootstrap Scene",
                Create = NetworkSetupMenu.CreateBootstrapScene,
#endif
            };
#if !FISHNET
            p.Add(SetupRequirement.Make("Multiplayer", "Fish-Net installed", SetupLevel.Optional)
                .Explain("The Bootstrap scene, the network relays and every Net… component need Fish-Net (4.x). Without it the game runs " +
                         "single player only and starts in the City scene.",
                         "Install Fish-Net from the Asset Store / its Git URL; it adds the FISHNET define. Then come back here.")
                .Checks(_ => SetupResult.Skip("Fish-Net is not installed: this scene is only needed for multiplayer.")));
#else
            // ---------------------------------------------------------------- the NetworkManager
            p.Add(SetupRequirement.Make("Network Manager", "NetworkManager object", SetupLevel.Required)
                .Explain("Fish-Net's NetworkManager runs the connection: transport, ticks, scenes, interest. The package's session components live on the same object.",
                         "Tools ▸ SimpleMovements ▸ Network ▸ Create Bootstrap Scene builds a complete one.")
                .Checks(ctx => SetupUtil.Single(ctx.All<NetworkManager>(), "NetworkManagers", "No NetworkManager in this scene."))
                .Fixes("Create NetworkManager", ctx =>
                {
                    ctx.Adopt(NetworkSetupMenu.BuildNetworkManager());
                    return true;
                }));

            p.Add(SetupRequirement.Make("Network Manager", "Fish-Net managers", SetupLevel.Required)
                .Explain("Tugboat (the UDP transport, 127.0.0.1:7770), Transport, Time (the tick: prediction and lag compensation run on it), " +
                         "Server, Client, Scene (City / World / dungeon scenes per connection), Prediction and Observer (who sees what) managers.",
                         "Add the missing components to the NetworkManager object.")
                .Checks(ctx =>
                {
                    NetworkManager nm = ctx.First<NetworkManager>();
                    if (nm == null)
                        return SetupResult.Skip("Needs the NetworkManager object first.");
                    List<string> missing = FishNetManagers.Where(t => SetupUtil.TypeByName(t) != null && nm.GetComponent(SetupUtil.TypeByName(t)) == null)
                        .Select(SetupUtil.ShortName).ToList();
                    return missing.Count == 0 ? SetupResult.Ok("All present.", nm) : SetupResult.Missing("Missing: " + string.Join(", ", missing) + ".", nm);
                })
                .Fixes("Add Missing Managers", ctx =>
                {
                    NetworkManager nm = ctx.First<NetworkManager>();
                    if (nm == null)
                        return false;
                    bool changed = false;
                    foreach (string t in FishNetManagers)
                    {
                        Type type = SetupUtil.TypeByName(t);
                        if (type != null && nm.GetComponent(type) == null)
                        {
                            ctx.Add(nm.gameObject, type);
                            changed = true;
                        }
                    }
                    return changed;
                }));

            p.Add(SetupRequirement.Make("Network Manager", "Server not started by Fish-Net on headless", SetupLevel.Recommended)
                .Explain("The Net Bootstrap starts the server itself (with the port and options from the command line). If Fish-Net's Server Manager " +
                         "also starts it on a headless build, the dedicated server starts twice and ignores the command line.",
                         "Server Manager ▸ Start On Headless: off.")
                .Checks(ctx =>
                {
                    var server = ctx.First<FishNet.Managing.Server.ServerManager>();
                    if (server == null)
                        return SetupResult.Skip("No Server Manager yet.");
                    SerializedProperty prop = new SerializedObject(server).FindProperty("_startOnHeadless");
                    if (prop == null)
                        return SetupResult.Skip("Cannot read this Fish-Net version's setting.");
                    return prop.boolValue ? SetupResult.Wrong("Start On Headless is on.", server) : SetupResult.Ok("Off.", server);
                })
                .Fixes("Turn It Off", ctx =>
                {
                    var server = ctx.First<FishNet.Managing.Server.ServerManager>();
                    if (server == null)
                        return false;
                    ctx.Record(server, "Start On Headless");
                    server.SetStartOnHeadless(false);
                    ctx.Dirty(server);
                    return true;
                }));

            p.Add(SetupRequirement.Make("Network Manager", "Realm interest rule (Observer Manager)", SetupLevel.Required)
                .Explain("The RealmObserverCondition decides who sees what: players only observe objects of their own scene (City, World, " +
                         "their dungeon run), limited by distance in the open world. Without it every client receives everything.",
                         "Observer Manager ▸ Default Conditions: the RealmObserverCondition asset (Assets/Networking).")
                .Checks(ctx =>
                {
                    var observers = ctx.First<ObserverManager>();
                    if (observers == null)
                        return SetupResult.Skip("No Observer Manager yet.");
                    return NetworkSetupMenu.HasRealmCondition(observers)
                        ? SetupResult.Ok("Uses the RealmObserverCondition.", observers)
                        : SetupResult.Wrong("Default Conditions do not include a RealmObserverCondition.", observers);
                })
                .Fixes("Assign Realm Condition", ctx =>
                {
                    var observers = ctx.First<ObserverManager>();
                    if (observers == null)
                        return false;
                    ctx.Record(observers, "Realm condition");
                    NetworkSetupMenu.AssignRealmCondition(observers);
                    ctx.Dirty(observers);
                    return true;
                }));

            foreach (var (typeName, level, why) in SessionComponents)
                p.Add(ComponentOnManager(typeName, level, why));

            p.Add(SetupRequirement.Make("Network Manager", "Player prefab on the Net Player Spawner", SetupLevel.Required)
                .Explain("The spawner gives every client its player character when it arrives in the City. It must be the networked player " +
                         "prefab (NetworkObject + NetPlayer on its root).",
                         "Make the player prefab networked (Player Prefab page, or Network ▸ Make Selected Prefab Networked ▸ Player), then drag it onto Net Player Spawner ▸ Player Prefab.")
                .Checks(ctx =>
                {
                    var spawner = ctx.First<NetPlayerSpawner>();
                    if (spawner == null)
                        return SetupResult.Skip("No Net Player Spawner yet.");
                    if (spawner.playerPrefab == null)
                        return SetupResult.Missing("Player Prefab is empty: nobody gets a character." +
                                                   (NetworkSetupMenu.FindPlayerPrefab() != null ? $" '{NetworkSetupMenu.FindPlayerPrefab().name}' is a networked player prefab." : " No networked player prefab in the project yet."), spawner);
                    if (spawner.playerPrefab.GetComponent<NetPlayer>() == null)
                        return SetupResult.Wrong($"'{spawner.playerPrefab.name}' has no NetPlayer on its root: make it networked (Player Prefab page).", spawner.playerPrefab);
                    return SetupResult.Ok($"'{spawner.playerPrefab.name}'.", spawner.playerPrefab);
                })
                .Fixes("Assign Networked Player Prefab", ctx =>
                {
                    var spawner = ctx.First<NetPlayerSpawner>();
                    GameObject prefab = NetworkSetupMenu.FindPlayerPrefab();
                    if (spawner == null || prefab == null)
                    {
                        EditorUtility.DisplayDialog("Setup", "No networked player prefab in the project yet: open the Player Prefab page and make your player networked first.", "OK");
                        return false;
                    }
                    ctx.Record(spawner, "Player prefab");
                    spawner.playerPrefab = prefab.GetComponent<NetworkObject>();
                    ctx.Dirty(spawner);
                    return true;
                }, inFixAll: false));

            // ---------------------------------------------------------------- the Network World
            p.Add(SetupRequirement.Make("Network World", "'Network World' object", SetupLevel.Required)
                .Explain("A scene NetworkObject every client observes. It carries the global relays (world cycle, duels, trading, terrain, parties, portals).",
                         "A root object named 'Network World' with a NetworkObject and a NetworkObserver.")
                .Checks(ctx =>
                {
                    GameObject world = ctx.RootNamed("Network World");
                    if (world == null)
                        return SetupResult.Missing("No root object named 'Network World'.");
                    return world.GetComponent<NetworkObject>() != null ? SetupResult.Ok("Found.", world) : SetupResult.Wrong("It has no NetworkObject.", world);
                })
                .Fixes("Create Network World", ctx =>
                {
                    GameObject world = ctx.RootNamed("Network World");
                    if (world == null)
                    {
                        ctx.Adopt(NetworkSetupMenu.BuildNetworkWorld());
                        return true;
                    }
                    ctx.Add<NetworkObject>(world);
                    return true;
                }));

            p.Add(SetupRequirement.Make("Network World", "Seen by every client", SetupLevel.Required)
                .Explain("The relays must reach every player wherever they are, so the Network World's observer ignores the realm rule.",
                         "Network World ▸ Network Observer ▸ Override Type: Ignore Manager.")
                .Checks(ctx =>
                {
                    GameObject world = ctx.RootNamed("Network World");
                    if (world == null)
                        return SetupResult.Skip("Needs the Network World first.");
                    var observer = world.GetComponent<NetworkObserver>();
                    if (observer == null)
                        return SetupResult.Missing("No Network Observer: the realm rule would hide the relays from players in other scenes.", world);
                    return NetworkSetupMenu.WorldObserverIgnoresManager(observer) ? SetupResult.Ok("Ignores the manager's conditions.", observer)
                        : SetupResult.Wrong("Its observer follows the manager's conditions.", observer);
                })
                .Fixes("Set Ignore Manager", ctx =>
                {
                    GameObject world = ctx.RootNamed("Network World");
                    if (world == null)
                        return false;
                    NetworkObserver observer = ctx.Add<NetworkObserver>(world); // the existing one when there is one
                    ctx.Record(observer, "Observer override");
                    NetworkSetupMenu.ConfigureWorldObserver(observer);
                    ctx.Dirty(observer);
                    return true;
                }));

            foreach (string relay in NetworkSetupMenu.GlobalRelayComponents)
                p.Add(Relay(relay));

            p.Add(SceneIds());

            // ---------------------------------------------------------------- build
            p.Add(SetupRequirement.Make("Build", "First scene in Build Settings", SetupLevel.Required)
                .Explain("The game starts in the first scene of the build: for multiplayer, the Bootstrap scene (its start menu offers Single Player, Host, Join and Server).",
                         "File ▸ Build Profiles / Build Settings: drag this scene to the top.")
                .Checks(ctx =>
                {
                    int i = SetupUtil.BuildIndex(ctx.Scene.path);
                    return i == 0 ? SetupResult.Ok("Scene 0.") : i < 0 ? SetupResult.Missing("Not in Build Settings.") : SetupResult.Wrong($"It is scene {i}: another scene starts first.");
                })
                .Fixes("Make It The First Scene", ctx => SetupUtil.AddToBuild(ctx.Scene.path, true)));

            p.Add(SetupRequirement.Make("Build", "City, World and Dungeon Instance scenes in the build", SetupLevel.Required)
                .Explain("The server loads them through the network by name (Hub World Settings ▸ City / World Scene Name, Dungeon Scenes ▸ Network Scene Name).",
                         "Add each scene to Build Settings (the Hub City / World / Dungeon Scene pages do it).")
                .Checks(_ =>
                {
                    List<string> missing = NetworkScenes().Where(n => !SetupUtil.InBuildByName(n)).ToList();
                    return missing.Count == 0 ? SetupResult.Ok("All in the build.") : SetupResult.Missing("Not in the build: " + string.Join(", ", missing) + ".");
                })
                .Fixes("Add The Ones Found", _ =>
                {
                    bool changed = false;
                    foreach (string n in NetworkScenes())
                    {
                        string path = SetupUtil.ScenePathByName(n);
                        if (path != null && !SetupUtil.InBuildByName(n))
                            changed |= SetupUtil.AddToBuild(path, false);
                    }
                    return changed;
                }));

            // ---------------------------------------------------------------- project assets used by the network
            p.Add(SetupRequirement.Make("Project", "Networked world item carrier (Resources/NetWorldItem)", SetupLevel.Recommended)
                .Explain("Dropped items, loot and thrown weapons whose own prefab is not networked are shown to every client through this carrier prefab.",
                         "Tools ▸ SimpleMovements ▸ Network ▸ Create World Item Prefab.")
                .Checks(_ =>
                {
                    var carrier = SMAssets.Load<GameObject>("NetWorldItem");
                    return SetupResult.Present(carrier != null, "Found.", "Missing: items dropped by players would only exist on the server.", carrier);
                })
                .Fixes("Create It", _ =>
                {
                    NetworkSetupMenu.CreateWorldItemPrefab();
                    return true;
                }));
            p.Add(SetupUtil.ResourcesAsset<ItemDatabase>("Project", "Item Database", ItemDatabase.ResourcesName, SetupLevel.Recommended,
                "Items travel over the network as stable ids from this database (inventories, drops, merchants, trading).",
                () => { NetworkSetupMenu.BuildItemDatabase(); return true; }));
            p.Add(SetupUtil.ResourcesAsset<AbilityDatabase>("Project", "Ability Database", "AbilityDatabase", SetupLevel.Recommended,
                "Abilities travel over the network as stable ids from this database (casts, ability slots, absorbed abilities).",
                () => { NetworkSetupMenu.BuildAbilityDatabase(); return true; }));

            // ---------------------------------------------------------------- what does not belong here
            p.Add(SetupRequirement.Make("Contents", "No player in this scene", SetupLevel.Required)
                .Explain("Every client gets its own player from the Net Player Spawner when it arrives in the City; a player placed here would be a second, unconnected character.",
                         "Delete the player from this scene (keep it as a prefab).")
                .Checks(ctx =>
                {
                    List<PlayerStatusController> players = ctx.All<PlayerStatusController>();
                    return players.Count == 0 ? SetupResult.Ok("None.") : SetupResult.Wrong($"'{players[0].name}' is a player in the Bootstrap scene.", players[0]);
                }));
            p.Add(DayNight("Shared systems", SetupLevel.Optional,
                "Put the Day Night Cycle here or in the City scene (one in total, in a scene that stays loaded). Players everywhere get the same hour."));
#endif
            p.Add(SetupUtil.HierarchyHealth());
            return p;
        }

#if FISHNET
        private static readonly string[] FishNetManagers =
        {
            "FishNet.Transporting.Tugboat.Tugboat", "FishNet.Managing.Transporting.TransportManager", "FishNet.Managing.Timing.TimeManager",
            "FishNet.Managing.Server.ServerManager", "FishNet.Managing.Client.ClientManager", "FishNet.Managing.Scened.SceneManager",
            "FishNet.Managing.Predicting.PredictionManager", "FishNet.Managing.Observing.ObserverManager",
        };

        private static readonly (string type, SetupLevel level, string why)[] SessionComponents =
        {
            ("GameNet.NetSession", SetupLevel.Required, "Keeps the game's network mode (offline / host / client / server) in sync and installs the bridges the gameplay code uses (ids, spawning, authority)."),
            ("GameNet.NetBootstrap", SetupLevel.Required, "The start menu and command line: Single Player, Host, Join, Dedicated Server; address, port, player name."),
            ("GameNet.NetRealmScenes", SetupLevel.Required, "Loads the City for each connection and moves players between the City, the World and their dungeon run."),
            ("GameNet.NetPlayerSpawner", SetupLevel.Required, "Gives each client its player character when it arrives in the City."),
            ("GameNet.NetLagCompensation", SetupLevel.Recommended, "Remembers where hittable objects were so a player's hit is judged against what their client saw. Without it fast targets are hard to hit online."),
            ("GameNet.HeadlessServer", SetupLevel.Recommended, "On a dedicated server: caps the frame rate, mutes audio and logs a status line."),
        };

        private static readonly Dictionary<string, (SetupLevel level, string why)> RelayInfo = new Dictionary<string, (SetupLevel, string)>
        {
            ["GameNet.NetHubWorld"] = (SetupLevel.Required, "The world cycle (timer, warnings, the jump) and hub messages to the players they are for."),
            ["GameNet.NetDuels"] = (SetupLevel.Recommended, "Duel challenges and the open duels (who fights whom, countdown, arena)."),
            ["GameNet.NetTrading"] = (SetupLevel.Recommended, "Player-to-player trading (requests, offers, lock, confirm)."),
            ["GameNet.NetWorldChunks"] = (SetupLevel.Required, "The open world's shared state: removed trees and rocks, the weather clock."),
            ["GameNet.NetSocial"] = (SetupLevel.Recommended, "Parties, guilds, fame, levels and inspecting other players. Without it party colours, nameplates and invites do not work online."),
            ["GameNet.NetPortals"] = (SetupLevel.Recommended, "The free player slots of dungeon portals shown when a player selects one."),
        };

        private static SetupRequirement ComponentOnManager(string typeName, SetupLevel level, string why)
        {
            string shortName = SetupUtil.ShortName(typeName);
            return SetupRequirement.Make("Network Manager", ObjectNames.NicifyVariableName(shortName), level)
                .Explain(why, $"Add {shortName} to the NetworkManager object.")
                .Checks(ctx =>
                {
                    Type t = SetupUtil.TypeByName(typeName);
                    if (t == null)
                        return SetupResult.Skip($"{shortName} is not in this project.");
                    NetworkManager nm = ctx.First<NetworkManager>();
                    if (nm == null)
                        return SetupResult.Skip("Needs the NetworkManager object first.");
                    Component c = SetupUtil.Or(nm.GetComponent(t), ctx.First(t));
                    if (c == null)
                        return SetupResult.Missing("Not in this scene.", nm);
                    return c.gameObject == nm.gameObject ? SetupResult.Ok("On the NetworkManager.", c) : SetupResult.Ok($"On '{c.name}'.", c);
                })
                .Fixes("Add To NetworkManager", ctx =>
                {
                    NetworkManager nm = ctx.First<NetworkManager>();
                    Type t = SetupUtil.TypeByName(typeName);
                    if (nm == null || t == null)
                        return false;
                    ctx.Add(nm.gameObject, t);
                    return true;
                });
        }

        private static SetupRequirement Relay(string typeName)
        {
            string shortName = SetupUtil.ShortName(typeName);
            (SetupLevel level, string why) info = RelayInfo.TryGetValue(typeName, out var i) ? i : (SetupLevel.Recommended, "A global network relay.");
            return SetupRequirement.Make("Network World", ObjectNames.NicifyVariableName(shortName) + " relay", info.level)
                .Explain(info.why, $"Add {shortName} to the 'Network World' object (Tools ▸ SimpleMovements ▸ Network ▸ Add Missing Relays To Network World).")
                .Checks(ctx =>
                {
                    Type t = SetupUtil.TypeByName(typeName);
                    if (t == null)
                        return SetupResult.Skip($"{shortName} is not in this project.");
                    GameObject world = ctx.RootNamed("Network World");
                    if (world == null)
                        return SetupResult.Skip("Needs the Network World first.");
                    Component c = world.GetComponent(t);
                    return SetupResult.Present(c != null, "On the Network World.", "Not on the Network World: this system does not work online.", c != null ? (Object)c : world);
                })
                .Fixes("Add Relay", ctx =>
                {
                    GameObject world = ctx.RootNamed("Network World");
                    Type t = SetupUtil.TypeByName(typeName);
                    if (world == null || t == null)
                        return false;
                    ctx.Add(world, t);
                    return true;
                });
        }

        /// <summary>Scene NetworkObjects need a scene id, which Fish-Net only gives while the scene is saved under a name.</summary>
        private static SetupRequirement SceneIds() =>
            SetupRequirement.Make("Network World", "Scene network ids", SetupLevel.Required)
                .Explain("Fish-Net identifies the NetworkObjects placed in a scene by a scene id. Objects added by a tool to a scene that was not saved yet have none " +
                         "('is expected to be initialized but was not' in Play mode).",
                         "Save the scene, then Tools ▸ SimpleMovements ▸ Network ▸ Fix Scene Network Ids (or Fish-Networking ▸ Utility ▸ Reserialize NetworkObjects).")
                .Checks(ctx =>
                {
                    List<NetworkObject> nobs = ctx.All<NetworkObject>();
                    if (nobs.Count == 0)
                        return SetupResult.Skip("No scene NetworkObject yet.");
                    List<NetworkObject> without = nobs.Where(n => !n.IsSceneObject).ToList();
                    return without.Count == 0 ? SetupResult.Ok($"{nobs.Count} scene NetworkObject(s), all with an id.")
                        : SetupResult.Wrong($"{without.Count} without a scene id ({string.Join(", ", without.Take(3).Select(n => n.name))}).", without[0]);
                })
                .Fixes("Assign Ids (saves the scene)", ctx =>
                {
                    if (string.IsNullOrEmpty(ctx.Scene.path))
                    {
                        EditorUtility.DisplayDialog("Setup", "Save the scene first: Fish-Net only gives ids to objects of a saved scene.", "OK");
                        return false;
                    }
                    return NetworkSetupMenu.AssignSceneIds(ctx.Scene) > 0;
                });

        private static IEnumerable<string> NetworkScenes()
        {
            HubWorldSettings s = SetupUtil.HubSettings();
            yield return s.citySceneName;
            yield return s.worldSceneName;
            if (s.dungeonScenes != null && !string.IsNullOrEmpty(s.dungeonScenes.networkSceneName))
                yield return s.dungeonScenes.networkSceneName;
        }
#endif

        // ================================================================== Hub City

        public static SetupProfile HubCity()
        {
            var p = new SetupProfile
            {
                Id = "city",
                Name = "Hub City Scene",
                Kind = SetupTargetKind.Scene,
                Summary = "The permanent city: gate, spawn, the Hub World Director, UI.",
                Description =
                    "The permanent hub city, never regenerated. Players start and respawn here, trade with NPCs, duel, and leave " +
                    "through the City Gate into the World. The Hub World Director (here, or in the Bootstrap scene) runs the " +
                    "dimensional cycle, the realms, the zones and the duels.\n\n" +
                    "Single player: the first scene of the build, with the player placed at the spawn. Multiplayer: loaded by the " +
                    "server for every client after the Bootstrap scene; the player is spawned by the network.",
                DocPath = "HubWorld/05-Setup-and-Configuration.md",
                FindDefault = () => SetupUtil.ScenePathByName(SetupUtil.HubSettings().citySceneName),
                CreateLabel = "Build City Scene...",
                Create = HubWorld.EditorTools.HubWorldSetupMenu.BuildCityScene,
            };

            p.Add(SetupRequirement.Make("Realm", "Marked as the City (Realm Scene)", SetupLevel.Required)
                .Explain("The Realm Scene tells the Hub World which realm a scene is, and where players arrive (through the gate) and spawn (at the start, after death).",
                         "A root object with a Realm Scene component, Realm = City.")
                .Checks(ctx => RealmCheck(ctx, RealmKind.City))
                .Fixes("Mark As City", ctx =>
                {
                    Vector3 gate = HubWorld.EditorTools.HubWorldSetupMenu.CityGatePosition(out Vector3 center);
                    GameObject go = ctx.CreateObject("Realm: City");
                    RealmScene realm = ctx.Add<RealmScene>(go);
                    realm.realm = RealmKind.City;
                    realm.arrivalPoint = HubWorld.EditorTools.HubWorldSetupMenu.Point("Arrival (inside the gate)", go.transform, gate + Vector3.forward * 12f, Quaternion.identity);
                    realm.spawnPoint = HubWorld.EditorTools.HubWorldSetupMenu.Point("Spawn (city square)", go.transform, center + Vector3.forward * 4f, Quaternion.identity);
                    ctx.Dirty(realm);
                    return true;
                }));
            p.Add(PointCheck(RealmKind.City, true));
            p.Add(PointCheck(RealmKind.City, false));
            p.Add(Gate(RealmGate.GateKind.CityGate, SetupLevel.Required,
                "Walking through the City Gate takes the player out into the World. Without one, players can never leave the city."));

            p.Add(SetupRequirement.Make("Hub World", "Hub World Director (here or in Bootstrap)", SetupLevel.Required)
                .Explain("Runs the hub world: the dimensional cycle and its jump, realms and travel, zones and PvP, duels. It also creates the HUD and the zone borders.",
                         "A root object with a Hub World Director, in this scene or in the Bootstrap scene (one in total).")
                .Checks(ctx =>
                {
                    List<HubWorldDirector> here = ctx.All<HubWorldDirector>();
                    if (here.Count > 1)
                        return SetupResult.Wrong($"{here.Count} directors: keep one.", here[1]);
                    if (here.Count == 1)
                        return SetupResult.Ok("In this scene.", here[0]);
                    HubWorldDirector other = Object.FindObjectsByType<HubWorldDirector>(FindObjectsInactive.Include).FirstOrDefault();
                    if (other != null)
                        return SetupResult.Ok($"In the open scene '{other.gameObject.scene.name}'.", other);
                    return SetupResult.Missing("None in this scene (open the Bootstrap scene too if you keep it there).");
                })
                .Fixes("Add Hub World Director", ctx =>
                {
                    ctx.Add<HubWorldDirector>(ctx.CreateObject("Hub World Director"));
                    return true;
                }));
            p.Add(SetupUtil.ResourcesAsset<HubWorldSettings>("Hub World", "Hub World Settings asset", "HubWorldSettings", SetupLevel.Recommended,
                "Cycle length, warnings, zones (safe / PvP rings), penalties, duels and the scene names. Without the asset the defaults are used and cannot be changed.",
                () => HubWorld.EditorTools.HubWorldSetupMenu.CreateSettings() != null));
            p.Add(SceneName(true));
            p.Add(SetupRequirement.Make("Hub World", "In Build Settings", SetupLevel.Required)
                .Explain("The City is loaded by name. Single player: it is the first scene. Multiplayer: it comes right after the Bootstrap scene.",
                         "File ▸ Build Profiles / Build Settings: add the scene.")
                .Checks(ctx =>
                {
                    int i = SetupUtil.BuildIndex(ctx.Scene.path);
                    if (i < 0)
                        return SetupResult.Missing("Not in Build Settings: the game cannot load it.");
                    bool bootstrapFirst = BootstrapIsFirst();
                    if (i == 0 || bootstrapFirst)
                        return SetupResult.Ok(i == 0 ? "First scene (single player starts here)." : $"Scene {i}, after the Bootstrap scene.");
                    return SetupResult.Wrong($"Scene {i}: single player starts in scene 0. Make the City (or the Bootstrap scene) first.");
                })
                .Fixes("Add To Build", ctx => SetupUtil.AddToBuild(ctx.Scene.path, !BootstrapIsFirst())));

            p.Add(SetupRequirement.Make("Players & UI", "Player (single player only)", SetupLevel.Optional)
                .Explain("Single player: put your player prefab at the Spawn point. Multiplayer: leave the City without a player - the server spawns one per client.")
                .Checks(ctx =>
                {
                    List<PlayerStatusController> players = ctx.All<PlayerStatusController>();
                    if (players.Count == 0)
                        return SetupResult.Note("No player here: right for multiplayer. For single player, drag your player prefab to the Spawn point.");
                    return SetupResult.Note($"'{players[0].name}' is placed here: right for single player. Remove it for multiplayer (the server spawns players).", players[0]);
                }));
            p.Add(SetupRequirement.Make("Players & UI", "Pause & settings menu", SetupLevel.Recommended)
                .Explain("Escape opens it in game: resume, settings (graphics, audio, key bindings), quit.",
                         "Tools ▸ SimpleMovements ▸ Scene ▸ Build Pause & Settings Menu.")
                .Checks(ctx =>
                {
                    var menu = ctx.First<PauseMenuManager>();
                    return SetupResult.Present(menu != null, "Found.", "None: Escape does nothing in game.", menu);
                })
                .Fixes("Build Menu", ctx =>
                {
                    PauseMenuManager m = GameMenuBuilder.Build();
                    if (m != null)
                        ctx.Adopt(m.transform.root.gameObject);
                    return m != null;
                }));
            p.Add(SetupRequirement.Make("Players & UI", "EventSystem", SetupLevel.Recommended)
                .Explain("Clicks on the UI (menus, merchants, inventory) go through the EventSystem. The inventory creates one at runtime when missing, but menus built in the scene need it in the editor.",
                         "GameObject ▸ UI ▸ Event System.")
                .Checks(ctx =>
                {
                    var es = ctx.First<UnityEngine.EventSystems.EventSystem>();
                    return SetupResult.Present(es != null, "Found.", "None in this scene.", es);
                })
                .Fixes("Create EventSystem", ctx =>
                {
                    GameObject go = ctx.CreateObject("EventSystem");
                    ctx.Add<UnityEngine.EventSystems.EventSystem>(go);
                    ctx.Add<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(go);
                    return true;
                }));
            p.Add(SetupRequirement.Make("Players & UI", "Player Targeting & Party System", SetupLevel.Optional)
                .Explain("Targeting (click a player, the target frame and menu), parties, nameplates, highlights and the portal panel. Created automatically at runtime with the defaults; add them only to change their settings.",
                         "Tools ▸ SimpleMovements ▸ Social ▸ Create Player Targeting And Party System.")
                .Checks(ctx =>
                {
                    var t = ctx.First<PlayerTargeting>();
                    return t != null ? SetupResult.Ok("In the scene (its settings are used).", t) : SetupResult.Note("Not in the scene: created at runtime with the default settings.");
                })
                .Fixes("Add Both", ctx =>
                {
                    ctx.Add<PlayerTargeting>(ctx.CreateObject("Player Targeting"));
                    if (ctx.First<PartySystem>() == null)
                        ctx.Add<PartySystem>(ctx.CreateObject("Party System"));
                    return true;
                }, inFixAll: false));

            p.Add(DayNight("Atmosphere", SetupLevel.Recommended,
                "The day/night clock: turns the sun, darkens the night, tells the weather the hour; 'Day only' / 'Night only' spawn rules read it. One in total, in the City or the Bootstrap scene."));
            p.Add(SetupRequirement.Make("Atmosphere", "Directional light (sun)", SetupLevel.Recommended)
                .Explain("The Day Night Cycle and the Weather System turn and dim the sun; without a directional light the city is lit only by the environment.",
                         "GameObject ▸ Light ▸ Directional Light.")
                .Checks(ctx =>
                {
                    Light sun = ctx.All<Light>().FirstOrDefault(l => l.type == LightType.Directional);
                    return SetupResult.Present(sun != null, $"'{(sun != null ? sun.name : "")}'.", "No directional light.", sun);
                })
                .Fixes("Add Sun", ctx =>
                {
                    GameObject go = ctx.CreateObject("Sun");
                    go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                    Light l = ctx.Add<Light>(go);
                    l.type = LightType.Directional;
                    l.shadows = LightShadows.Soft;
                    return true;
                }));
            p.Add(SetupRequirement.Make("Atmosphere", "Realm music", SetupLevel.Optional)
                .Explain("Plays the music and ambience of where your own player is: City, World (per zone ring), Dungeons.",
                         "Tools ▸ SimpleMovements ▸ Scene ▸ Create Realm Music, then drag your clips in.")
                .Checks(ctx =>
                {
                    var audio = ctx.First<RealmAudio>();
                    return audio != null ? SetupResult.Ok("Found.", audio) : SetupResult.Missing("No Realm Audio: no music.");
                })
                .Fixes("Create Realm Music", ctx =>
                {
                    GameObject go = ctx.CreateObject("Realm Music");
                    ctx.Add<RealmAudio>(go);
                    if (Object.FindAnyObjectByType<SoundManager>() == null)
                        ctx.Add<SoundManager>(go);
                    return true;
                }, inFixAll: false));

            p.Add(SetupRequirement.Make("Contents", "No mobs in the city", SetupLevel.Recommended)
                .Explain("The city is a safe place with NPCs only; hostile mobs belong in the World and the dungeons.")
                .Checks(ctx =>
                {
                    List<Mob> mobs = ctx.All<Mob>();
                    return mobs.Count == 0 ? SetupResult.Ok("None.") : SetupResult.Wrong($"{mobs.Count} mob(s), e.g. '{mobs[0].name}'.", mobs[0]);
                }));
            p.Add(SetupRequirement.Make("Contents", "No terrain or weather here", SetupLevel.Recommended)
                .Explain("The Terrain Generator, Endless Terrain and Weather System belong in the World scene: the city is regenerated never, the world every cycle.")
                .Checks(ctx =>
                {
                    Component c = SetupUtil.Or(SetupUtil.Or<Component>(ctx.First<TerrainGenerator>(), ctx.First<EndlessTerrain>()), ctx.First<WeatherSystem>());
                    return c == null ? SetupResult.Ok("None.") : SetupResult.Wrong($"'{c.name}' ({c.GetType().Name}) belongs in the World scene.", c);
                }));
            p.Add(SetupUtil.HierarchyHealth());
            return p;
        }

        // ================================================================== World

        public static SetupProfile World()
        {
            var p = new SetupProfile
            {
                Id = "world",
                Name = "World Scene",
                Kind = SetupTargetKind.Scene,
                Summary = "The open world: terrain, weather, the city's outside and return gate.",
                Description =
                    "The open world around the city, regenerated with a new seed every dimensional cycle: the procedural terrain " +
                    "(with its portals and mobs), the weather, and an outside copy of the city with the Return Gate.\n\n" +
                    "It is loaded when a player goes out through the City Gate (in multiplayer, by the server and by each client " +
                    "whose player is out) and may be unloaded while nobody is there. Players are never placed in it in the editor.",
                DocPath = "HubWorld/05-Setup-and-Configuration.md",
                FindDefault = () => SetupUtil.ScenePathByName(SetupUtil.HubSettings().worldSceneName),
            };

            p.Add(SetupRequirement.Make("Realm", "Marked as the World (Realm Scene)", SetupLevel.Required)
                .Explain("Tells the Hub World this scene is the open world, and where players arrive when they walk out of the city.",
                         "Tools ▸ SimpleMovements ▸ Hub World ▸ Make Open Scene The World Scene (also builds the city's outside and the Return Gate).")
                .Checks(ctx => RealmCheck(ctx, RealmKind.World))
                .Fixes("Mark As World", ctx =>
                {
                    Vector3 gate = HubWorld.EditorTools.HubWorldSetupMenu.CityGatePosition(out _);
                    GameObject go = ctx.CreateObject("Realm: World");
                    RealmScene realm = ctx.Add<RealmScene>(go);
                    realm.realm = RealmKind.World;
                    realm.hideWhileLocalPlayerAway = false;
                    realm.arrivalPoint = HubWorld.EditorTools.HubWorldSetupMenu.Point("Arrival (outside the return gate)", go.transform, gate + Vector3.back * 14f, Quaternion.Euler(0f, 180f, 0f));
                    ctx.Dirty(realm);
                    return true;
                }));
            p.Add(PointCheck(RealmKind.World, true));
            p.Add(SetupRequirement.Make("Realm", "The city seen from outside", SetupLevel.Recommended)
                .Explain("The world shows the city's walls (sealed inside) where the city stands, with the Return Gate in them. The terrain is levelled and kept clear around it.",
                         "Tools ▸ SimpleMovements ▸ Hub World ▸ Make Open Scene The World Scene builds it from the Hub World Settings.")
                .Checks(ctx =>
                {
                    GameObject exterior = ctx.RootNamed("City Exterior");
                    return SetupResult.Present(exterior != null, "'City Exterior' found.", "No 'City Exterior': the city is invisible from the world.", exterior);
                })
                .Fixes("Build City Exterior", ctx =>
                {
                    ctx.Adopt(HubWorld.EditorTools.HubWorldSetupMenu.CreateExterior());
                    return true;
                }));
            p.Add(Gate(RealmGate.GateKind.ReturnGate, SetupLevel.Recommended,
                "Walking through the Return Gate brings the player back into the city. Without one players can only come back when the city jumps (or by dying)."));

            p.Add(SetupRequirement.Make("Terrain", "Terrain Generator", SetupLevel.Required)
                .Explain("Generates the land: noise, biomes, rivers, objects, portals and mob spawns, from the world seed (a new one every cycle).",
                         "Add a Terrain Generator (and configure it in its inspector), or open your existing terrain scene and make it the World scene.")
                .Checks(ctx => SetupUtil.Single(ctx.All<TerrainGenerator>(), "Terrain Generators", "No Terrain Generator: the world is empty."))
                .Fixes("Add Terrain Generator (defaults)", ctx =>
                {
                    ctx.Add<TerrainGenerator>(ctx.CreateObject("Terrain Generator"));
                    return true;
                }, inFixAll: false));
            p.Add(SetupRequirement.Make("Terrain", "Endless Terrain (streaming)", SetupLevel.Required)
                .Explain("Streams the terrain in chunks around the player who is out in the world (the director sets its viewer).",
                         "Add an Endless Terrain next to the Terrain Generator; leave its Viewer empty.")
                .Checks(ctx => SetupUtil.Single(ctx.All<EndlessTerrain>(), "Endless Terrains", "No Endless Terrain: no chunk is ever built."))
                .Fixes("Add Endless Terrain", ctx =>
                {
                    TerrainGenerator tg = ctx.First<TerrainGenerator>();
                    ctx.Add<EndlessTerrain>(tg != null ? tg.gameObject : ctx.CreateObject("Endless Terrain"));
                    return true;
                }));
            p.Add(SetupRequirement.Make("Terrain", "Endless Terrain viewer left empty", SetupLevel.Required)
                .Explain("The Hub World Director sets the viewer to the player who is out in the world (and changes it as players come and go). A fixed viewer streams the wrong place.",
                         "Endless Terrain ▸ Viewer: empty.")
                .Checks(ctx =>
                {
                    EndlessTerrain t = ctx.First<EndlessTerrain>();
                    if (t == null)
                        return SetupResult.Skip("No Endless Terrain yet.");
                    return t.viewer == null ? SetupResult.Ok("Empty.", t) : SetupResult.Wrong($"Viewer is '{t.viewer.name}'.", t);
                })
                .Fixes("Clear Viewer", ctx =>
                {
                    EndlessTerrain t = ctx.First<EndlessTerrain>();
                    if (t == null)
                        return false;
                    ctx.Record(t, "Clear viewer");
                    t.viewer = null;
                    ctx.Dirty(t);
                    return true;
                }));
            p.Add(SetupRequirement.Make("Terrain", "Water level below the city ground", SetupLevel.Recommended)
                .Explain("The terrain is levelled to City Ground Height around the city. If the sea is higher, the city's ground ends up under water.",
                         "Hub World Settings ▸ Zones ▸ City Ground Height above the Terrain Generator's water level, then rebuild the city exterior.")
                .Checks(ctx =>
                {
                    TerrainGenerator tg = ctx.First<TerrainGenerator>();
                    if (tg == null)
                        return SetupResult.Skip("No Terrain Generator yet.");
                    ZoneSettings z = SetupUtil.HubSettings().zones;
                    if (!z.flattenCityGround)
                        return SetupResult.Skip("Flatten City Ground is off.");
                    return tg.SeaLevel < z.cityGroundHeight - 0.5f
                        ? SetupResult.Ok($"Water {tg.SeaLevel:0.#} < city ground {z.cityGroundHeight:0.#}.", tg)
                        : SetupResult.Wrong($"Water level {tg.SeaLevel:0.#} is not below City Ground Height {z.cityGroundHeight:0.#}.", tg);
                }));

            p.Add(SetupRequirement.Make("Weather", "Weather System", SetupLevel.Recommended)
                .Explain("Rain, snow, fog, wind, storms and tornadoes that drift over the land, decided by the biome and climate. Players in this scene get the weather.",
                         "Add a Weather System (any object of this scene) and assign its Terrain Generator.")
                .Checks(ctx => SetupUtil.Single(ctx.All<WeatherSystem>(), "Weather Systems", "No Weather System: the sky is always clear."))
                .Fixes("Add Weather System", ctx =>
                {
                    WeatherSystem w = ctx.Add<WeatherSystem>(ctx.CreateObject("Weather System"));
                    TerrainGenerator tg = ctx.First<TerrainGenerator>();
                    if (tg != null)
                        SetupUtil.SetReference(w, "terrainGenerator", tg);
                    return true;
                }));
            p.Add(SetupRequirement.Make("Weather", "Weather System ▸ Terrain Generator", SetupLevel.Recommended)
                .Explain("The weather reads the biomes and climate from the terrain generator.", "Weather System ▸ Terrain Generator: the scene's generator.")
                .Checks(ctx =>
                {
                    WeatherSystem w = ctx.First<WeatherSystem>();
                    TerrainGenerator tg = ctx.First<TerrainGenerator>();
                    if (w == null || tg == null)
                        return SetupResult.Skip("Needs both a Weather System and a Terrain Generator.");
                    Object assigned = SetupUtil.GetReference(w, "terrainGenerator");
                    return assigned != null ? SetupResult.Ok($"'{assigned.name}'.", w) : SetupResult.Missing("Empty.", w);
                })
                .Fixes("Assign", ctx => SetupUtil.SetReference(ctx.First<WeatherSystem>(), "terrainGenerator", ctx.First<TerrainGenerator>())));
            p.Add(SetupRequirement.Make("Weather", "Player Weather Effects", SetupLevel.Recommended)
                .Explain("Makes the weather matter: Soaked, Chilled, Frostbite, Heatstroke, Sandblasted... on players exposed to it (shelter protects). One for all players, on the Weather System object.",
                         "Tools ▸ SimpleMovements ▸ Weather ▸ Add Player Weather Effects.")
                .Checks(ctx =>
                {
                    WeatherSystem w = ctx.First<WeatherSystem>();
                    if (w == null)
                        return SetupResult.Skip("Needs a Weather System first.");
                    PlayerWeatherEffects fx = SetupUtil.Or(w.GetComponent<PlayerWeatherEffects>(), ctx.First<PlayerWeatherEffects>());
                    return SetupResult.Present(fx != null, "Found.", "None: the weather is only visual.", fx != null ? (Object)fx : w);
                })
                .Fixes("Add Player Weather Effects", ctx =>
                {
                    WeatherSystem w = ctx.First<WeatherSystem>();
                    if (w == null)
                        return false;
                    ctx.Add<PlayerWeatherEffects>(w.gameObject);
                    return true;
                }));

            p.Add(SceneName(false));
            p.Add(SetupRequirement.Make("Hub World", "In Build Settings", SetupLevel.Required)
                .Explain("The World is loaded by name when a player walks out of the city.", "File ▸ Build Profiles / Build Settings: add the scene.")
                .Checks(ctx => SetupUtil.BuildIndex(ctx.Scene.path) >= 0 ? SetupResult.Ok("In the build.") : SetupResult.Missing("Not in Build Settings: leaving the city fails."))
                .Fixes("Add To Build", ctx => SetupUtil.AddToBuild(ctx.Scene.path, false)));

            p.Add(SetupRequirement.Make("Contents", "No Hub World Director here", SetupLevel.Required)
                .Explain("The director lives in the City (or Bootstrap) scene, which stays loaded; the World is unloaded and regenerated.")
                .Checks(ctx =>
                {
                    var d = ctx.First<HubWorldDirector>();
                    return d == null ? SetupResult.Ok("None.") : SetupResult.Wrong($"'{d.name}' belongs in the City scene.", d);
                })
                .Fixes("Remove It From This Scene", ctx =>
                {
                    var d = ctx.First<HubWorldDirector>();
                    if (d == null)
                        return false;
                    Undo.DestroyObjectImmediate(d);
                    return true;
                }, inFixAll: false));
            p.Add(SetupRequirement.Make("Contents", "No player in this scene", SetupLevel.Required)
                .Explain("Players start in the City and walk out; a player placed here would be a second, stray character.", "Move the player to the City scene.")
                .Checks(ctx =>
                {
                    var players = ctx.All<PlayerStatusController>();
                    return players.Count == 0 ? SetupResult.Ok("None.") : SetupResult.Wrong($"'{players[0].name}' is a player in the World scene.", players[0]);
                }));
            p.Add(SetupRequirement.Make("Contents", "Cameras", SetupLevel.Optional)
                .Explain("The player brings its own camera from the City; a camera here adds a second camera and audio listener.")
                .Checks(ctx =>
                {
                    var cams = ctx.All<Camera>();
                    return cams.Count == 0 ? SetupResult.Ok("None.") : SetupResult.Note($"'{cams[0].name}' is a camera in the World scene: remove it unless you need it.", cams[0]);
                }));
            p.Add(SetupRequirement.Make("Contents", "No Day Night Cycle here", SetupLevel.Recommended)
                .Explain("The clock belongs in a scene that stays loaded (City or Bootstrap). One here is a second clock that removes itself at runtime, and the world has no night while it is unloaded.")
                .Checks(ctx =>
                {
                    var c = ctx.First<DayNightCycle>();
                    return c == null ? SetupResult.Ok("None.") : SetupResult.Wrong($"'{c.name}' should be in the City or Bootstrap scene.", c);
                }));
            p.Add(SetupUtil.HierarchyHealth());
            return p;
        }

        // ================================================================== Dungeon instance

        public static SetupProfile Dungeon()
        {
            var p = new SetupProfile
            {
                Id = "dungeon",
                Name = "Dungeon Scene",
                Kind = SetupTargetKind.Scene,
                Summary = "The empty template every dungeon run loads (multiplayer).",
                Description =
                    "In a network session each dungeon run loads its own copy of this (empty) scene, with its own physics. The " +
                    "server builds the dungeon inside its copy; the clients of the run rebuild each floor from the layout the " +
                    "server sends them, through the run's relay (NetDungeonRun) on the marker object.\n\n" +
                    "Single player creates its dungeon scenes at runtime and does not use this one. Keep it empty: everything in " +
                    "it is copied for every run.",
                DocPath = "Networking/10-Dungeons.md",
                FindDefault = () =>
                {
#if FISHNET
                    if (File.Exists(NetworkSetupMenu.DungeonInstancePath))
                        return NetworkSetupMenu.DungeonInstancePath;
#endif
                    HubWorldSettings s = SetupUtil.HubSettings();
                    return SetupUtil.ScenePathByName(s.dungeonScenes != null ? s.dungeonScenes.networkSceneName : "DungeonInstance");
                },
#if FISHNET
                CreateLabel = "Create Dungeon Instance Scene",
                Create = NetworkSetupMenu.CreateDungeonInstanceScene,
#endif
            };

            p.Add(SetupRequirement.Make("Dungeon instance", "Dungeon Instance marker", SetupLevel.Required)
                .Explain("Marks the template scene; its object carries the run's network relay.",
                         "A root object 'Dungeon Instance' with the Dungeon Instance Scene Marker component.")
                .Checks(ctx => SetupUtil.Single(ctx.All<GameNet.DungeonInstanceSceneMarker>(), "markers", "No Dungeon Instance Scene Marker."))
                .Fixes("Add Marker", ctx =>
                {
                    ctx.Add<GameNet.DungeonInstanceSceneMarker>(ctx.CreateObject("Dungeon Instance"));
                    return true;
                }));
#if FISHNET
            p.Add(SetupRequirement.Make("Dungeon instance", "Run relay (NetworkObject + NetDungeonRun)", SetupLevel.Required)
                .Explain("Sends the run's layout floor by floor, the states of doors, traps and chests, and carries the players' interactions to the server. Every stacked copy has its own.",
                         "On the marker object: a NetworkObject and a NetDungeonRun.")
                .Checks(ctx =>
                {
                    var marker = ctx.First<GameNet.DungeonInstanceSceneMarker>();
                    if (marker == null)
                        return SetupResult.Skip("Needs the marker first.");
                    var missing = new List<string>();
                    if (marker.GetComponent<NetworkObject>() == null) missing.Add("NetworkObject");
                    if (marker.GetComponent<NetDungeonRun>() == null) missing.Add("NetDungeonRun");
                    return missing.Count == 0 ? SetupResult.Ok("Both on the marker.", marker) : SetupResult.Missing("Missing: " + string.Join(", ", missing) + ".", marker);
                })
                .Fixes("Add Relay", ctx =>
                {
                    var marker = ctx.First<GameNet.DungeonInstanceSceneMarker>();
                    if (marker == null)
                        return false;
                    ctx.Add<NetworkObject>(marker.gameObject);
                    ctx.Add<NetDungeonRun>(marker.gameObject);
                    return true;
                }));
            p.Add(SceneIds());
#else
            p.Add(SetupRequirement.Make("Dungeon instance", "Multiplayer", SetupLevel.Optional)
                .Explain("This scene is only used by network sessions (Fish-Net). Single-player dungeons build their own scenes at runtime.")
                .Checks(_ => SetupResult.Skip("Fish-Net is not installed: single player does not use this scene.")));
#endif
            p.Add(SetupRequirement.Make("Dungeon instance", "Nothing else in the scene", SetupLevel.Recommended)
                .Explain("Everything in this scene is copied for every run: lights, cameras or players here would multiply. The dungeon brings its own lighting.",
                         "Delete every root object except 'Dungeon Instance'.")
                .Checks(ctx =>
                {
                    List<GameObject> extra = ctx.Roots().Where(g => g.GetComponent<GameNet.DungeonInstanceSceneMarker>() == null).ToList();
                    return extra.Count == 0 ? SetupResult.Ok("Empty apart from the marker.")
                        : SetupResult.Wrong($"{extra.Count} other root object(s): {string.Join(", ", extra.Take(4).Select(g => g.name))}.", extra[0]);
                }));

            p.Add(SetupRequirement.Make("Hub World", "Dungeons in their own scenes", SetupLevel.Recommended)
                .Explain("Hub World Settings ▸ Dungeons In Own Scenes gives every dungeon run its own scene and physics, so runs never collide with each other or with the world.",
                         "Hub World Settings ▸ Dungeons In Own Scenes: on.")
                .Checks(_ =>
                {
                    HubWorldSettings s = SetupUtil.HubSettingsAsset();
                    if (s == null)
                        return SetupResult.Note("No Hub World Settings asset: the default (on) is used.");
                    return s.dungeonsInOwnScenes ? SetupResult.Ok("On.", s) : SetupResult.Wrong("Off: dungeons are built far below the world instead.", s);
                })
                .Fixes("Turn On", _ =>
                {
                    HubWorldSettings s = SetupUtil.HubSettingsAsset();
                    if (s == null)
                        return false;
                    Undo.RecordObject(s, "Dungeons in own scenes");
                    s.dungeonsInOwnScenes = true;
                    EditorUtility.SetDirty(s);
                    return true;
                }));
            p.Add(SetupRequirement.Make("Hub World", "Network Scene Name matches", SetupLevel.Required)
                .Explain("Hub World Settings ▸ Dungeon Scenes ▸ Network Scene Name is the scene each run loads.",
                         "Set Network Scene Name to this scene's name.")
                .Checks(ctx =>
                {
                    HubWorldSettings s = SetupUtil.HubSettings();
                    string wanted = s.dungeonScenes != null ? s.dungeonScenes.networkSceneName : "";
                    return wanted == ctx.Scene.name ? SetupResult.Ok($"'{wanted}'.") : SetupResult.Wrong($"The settings name '{wanted}', this scene is '{ctx.Scene.name}'.");
                })
                .Fixes("Use This Scene", ctx =>
                {
                    HubWorldSettings s = SetupUtil.HubSettingsAsset();
                    if (s == null)
                        s = HubWorld.EditorTools.HubWorldSetupMenu.CreateSettings();
                    if (s == null)
                        return false;
                    Undo.RecordObject(s, "Network scene name");
                    if (s.dungeonScenes == null)
                        s.dungeonScenes = new ProceduralDungeon.DungeonSceneSettings();
                    s.dungeonScenes.networkSceneName = ctx.Scene.name;
                    EditorUtility.SetDirty(s);
                    return true;
                }));
            p.Add(SetupRequirement.Make("Hub World", "In Build Settings", SetupLevel.Required)
                .Explain("Runs load the scene by name through Fish-Net's scene manager.", "File ▸ Build Profiles / Build Settings: add the scene.")
                .Checks(ctx => SetupUtil.BuildIndex(ctx.Scene.path) >= 0 ? SetupResult.Ok("In the build.") : SetupResult.Missing("Not in Build Settings: online dungeon runs fail."))
                .Fixes("Add To Build", ctx => SetupUtil.AddToBuild(ctx.Scene.path, false)));
            p.Add(SetupUtil.HierarchyHealth());
            return p;
        }

        // ================================================================== shared pieces

        private static SetupResult RealmCheck(SetupContext ctx, RealmKind kind)
        {
            List<RealmScene> realms = ctx.All<RealmScene>();
            RealmScene mine = realms.FirstOrDefault(r => r.realm == kind);
            if (mine != null)
                return realms.Count > 1 ? SetupResult.Wrong($"{realms.Count} Realm Scenes: keep one per scene.", realms[1]) : SetupResult.Ok($"'{mine.name}'.", mine);
            if (realms.Count > 0)
                return SetupResult.Wrong($"'{realms[0].name}' marks this scene as {realms[0].realm}, not {kind}.", realms[0]);
            return SetupResult.Missing($"No Realm Scene: the Hub World does not know this is the {kind}.");
        }

        private static SetupRequirement PointCheck(RealmKind kind, bool arrival)
        {
            string what = arrival ? "Arrival point" : "Spawn point";
            return SetupRequirement.Make("Realm", what, SetupLevel.Recommended)
                .Explain(arrival
                        ? (kind == RealmKind.City ? "Where players appear when they come back from the world (just inside the gate)." : "Where players appear when they walk out of the city (outside the Return Gate).")
                        : "Where players start, and respawn after death (the city square).",
                    $"Realm Scene ▸ {what}: an empty object placed where players should appear (several players are spread around it).")
                .Checks(ctx =>
                {
                    RealmScene r = ctx.All<RealmScene>().FirstOrDefault(x => x.realm == kind);
                    if (r == null)
                        return SetupResult.Skip("Needs the Realm Scene first.");
                    Transform t = arrival ? r.arrivalPoint : r.spawnPoint;
                    return t != null ? SetupResult.Ok($"'{t.name}'.", t) : SetupResult.Missing("Empty: the Realm Scene object itself is used.", r);
                })
                .Fixes("Create " + what, ctx =>
                {
                    RealmScene r = ctx.All<RealmScene>().FirstOrDefault(x => x.realm == kind);
                    if (r == null)
                        return false;
                    Vector3 gate = HubWorld.EditorTools.HubWorldSetupMenu.CityGatePosition(out Vector3 center);
                    ctx.Record(r, what);
                    if (arrival)
                        r.arrivalPoint = HubWorld.EditorTools.HubWorldSetupMenu.Point(kind == RealmKind.City ? "Arrival (inside the gate)" : "Arrival (outside the return gate)",
                            r.transform, kind == RealmKind.City ? gate + Vector3.forward * 12f : gate + Vector3.back * 14f, Quaternion.identity);
                    else
                        r.spawnPoint = HubWorld.EditorTools.HubWorldSetupMenu.Point("Spawn (city square)", r.transform, center + Vector3.forward * 4f, Quaternion.identity);
                    ctx.Dirty(r);
                    return true;
                });
        }

        private static SetupRequirement Gate(RealmGate.GateKind kind, SetupLevel level, string why)
        {
            string label = kind == RealmGate.GateKind.CityGate ? "City Gate" : "Return Gate";
            return SetupRequirement.Make("Realm", label, level)
                .Explain(why, $"Tools ▸ SimpleMovements ▸ Hub World ▸ Add {label} At Scene View Pivot, or any trigger collider with a Realm Gate (Kind = {kind}).")
                .Checks(ctx =>
                {
                    RealmGate g = ctx.All<RealmGate>().FirstOrDefault(x => x.kind == kind);
                    return SetupResult.Present(g != null, $"'{(g != null ? g.name : "")}'.", $"No {label}.", g);
                })
                .Fixes("Add " + label, ctx =>
                {
                    Vector3 pos = HubWorld.EditorTools.HubWorldSetupMenu.CityGatePosition(out _);
                    ctx.Adopt(HubWorld.EditorTools.HubWorldSetupMenu.CreateGate(kind, pos));
                    return true;
                });
        }

        private static SetupRequirement SceneName(bool city)
        {
            string field = city ? "City Scene Name" : "World Scene Name";
            return SetupRequirement.Make("Hub World", $"Hub World Settings ▸ {field}", SetupLevel.Required)
                .Explain($"The Hub World loads the {(city ? "City" : "World")} by the name in its settings.", $"Hub World Settings ▸ {field}: this scene's name.")
                .Checks(ctx =>
                {
                    HubWorldSettings s = SetupUtil.HubSettings();
                    string wanted = city ? s.citySceneName : s.worldSceneName;
                    return wanted == ctx.Scene.name ? SetupResult.Ok($"'{wanted}'.") : SetupResult.Wrong($"The settings name '{wanted}', this scene is '{ctx.Scene.name}'.");
                })
                .Fixes("Use This Scene", ctx =>
                {
                    HubWorldSettings s = SetupUtil.HubSettingsAsset();
                    if (s == null)
                        s = HubWorld.EditorTools.HubWorldSetupMenu.CreateSettings();
                    if (s == null)
                        return false;
                    Undo.RecordObject(s, field);
                    if (city)
                        s.citySceneName = ctx.Scene.name;
                    else
                        s.worldSceneName = ctx.Scene.name;
                    EditorUtility.SetDirty(s);
                    return true;
                });
        }

        private static SetupRequirement DayNight(string group, SetupLevel level, string why) =>
            SetupRequirement.Make(group, "Day Night Cycle", level)
                .Explain(why, "Tools ▸ SimpleMovements ▸ Weather ▸ Add Day Night Cycle.")
                .Checks(ctx =>
                {
                    var here = ctx.First<DayNightCycle>();
                    if (here != null)
                        return SetupResult.Ok("In this scene.", here);
                    DayNightCycle other = Object.FindObjectsByType<DayNightCycle>(FindObjectsInactive.Include).FirstOrDefault();
                    if (other != null)
                        return SetupResult.Ok($"In the open scene '{other.gameObject.scene.name}'.", other);
                    return SetupResult.Missing("None in the open scenes: no night (unless another scene has one).");
                })
                .Fixes("Add Day Night Cycle", ctx =>
                {
                    ctx.Add<DayNightCycle>(ctx.CreateObject("Day Night Cycle"));
                    return true;
                });

        private static bool BootstrapIsFirst()
        {
            EditorBuildSettingsScene first = EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled);
            return first != null && Path.GetFileNameWithoutExtension(first.path).IndexOf("Bootstrap", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
