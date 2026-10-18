# 00 - Start here: multiplayer and Fish-Net basics

This page is for someone who has never used Fish-Net or made a multiplayer game. It explains the ideas first, then how
this package uses them, then exactly what to click to get a working multiplayer test. The pages after this one (01 to
12) go deeper into each system.

You do **not** need a remote server, a hosting account or a second computer. Everything runs on your own machine.

---

## 1. The big idea in one picture

```
                 ┌───────────────────────────────┐
  Client A  ───► │            SERVER             │ ◄───  Client B
  (Ana's PC)     │  the only one who decides:    │       (Bram's PC)
  sends: "I      │  positions, damage, loot,     │       sends: "I press
  press W",      │  inventory, mobs, dungeons    │       attack"
  "I attack"     └───────────────┬───────────────┘
        ▲                        │ sends back what happened
        └────────────────────────┴──────────────────────────► everyone sees the same world
```

* **The server is the boss.** It runs the real game. Players only send **intentions** ("I pressed W", "I clicked
  attack", "I want to pick up this sword"). The server checks them and decides what happens.
* **Clients show the result.** Each player's game receives what the server decided and draws it.
* This is called a **server-authoritative** game. It makes cheating hard: a hacked client can ask for anything, but
  the server refuses what is not allowed.

## 2. Words you will see everywhere

| Word | What it means | In this package |
|---|---|---|
| **Fish-Net** | A free networking library for Unity. It sends messages between the server and the players. | Version 4.x. Installing it adds the `FISHNET` scripting define. Without it, the network code is not compiled and the game is single player only. |
| **Server** | The process that runs the real game. | Can be a *dedicated server* or part of a *host*. |
| **Client** | A player's game connected to a server. | Every player except the host's own. |
| **Host** | Server and a player in the same program (you play and serve at once). | Easiest way to test in the editor. |
| **Dedicated server** | A server with no player of its own, usually without graphics ("headless", batch mode). | What a real online game would run. You can run it locally. |
| **Single player / Offline** | No network at all. | The original game, unchanged. |
| **Connection** | One client connected to the server. | Fish-Net's `NetworkConnection`. |
| **NetworkManager** | The Fish-Net component that runs everything: it starts the server or client and holds the other managers. | Lives in the **Bootstrap** scene. |
| **Transport** | How bytes travel (UDP sockets). | **Tugboat** on port **7770**. |
| **NetworkObject** | A component that gives a GameObject a network identity, so the server and clients agree it is "the same object". | On the player prefab root, mob prefabs, world items, portals, interactables, and on two scene objects (Network World, Dungeon Instance). |
| **NetworkBehaviour** | A script that can send network messages. It needs a NetworkObject on the same object or a parent. | All the `Net...` components (`NetPlayer`, `NetMob`, `NetInventory`...). |
| **Spawning** | Creating a networked object on the server and telling the clients to create their copy. | Gameplay code calls `GameNet.NetSpawn.Spawn(go)` after `Instantiate`. Offline it does nothing. |
| **Prefab vs scene object** | A *spawned* object comes from a prefab registered with Fish-Net. A *scene object* is placed in a scene and identified by a **scene id**. | The **Fix Scene Network Ids** menu repairs scene ids if Fish-Net complains. |
| **Owner / ownership** | The client that controls an object. The server gives each player ownership of their own character. | `NetPlayer.IsLocalPlayer` is true only on the owner's machine. |
| **RPC** | A remote call: "run this function on the other side". *ServerRpc* = client asks the server. *ObserversRpc* = server tells every client that sees the object. *TargetRpc* = server tells one client. | Used for attacks, casts, inventory requests, trades. |
| **SyncVar** | A variable the server sets and Fish-Net copies to clients automatically. | Health, death flag, player name, realm... |
| **Tick** | The network clock. The server simulates in fixed steps, 30 per second by default. | `-tickrate 30` on the command line. |
| **Prediction** | The owner's game moves the character immediately, without waiting for the server, so controls feel instant. | Only the player's movement is predicted. |
| **Reconciliation** | The server sends back the true position. If the prediction was wrong, the client snaps back and replays its inputs. | Done automatically by `NetPlayerMovement`. |
| **Interpolation / smoothing** | Drawing an object between two network updates so it moves smoothly instead of 30 jumps per second. | `NetworkTickSmoother`, *Smooth Body* and the camera target smoother. |
| **Observers / interest management** | Which clients receive which objects. Far or unrelated objects are hidden to save bandwidth. | `RealmObserverCondition`: same scene only, and in the World within 140 m. |
| **Scene stacking** | Loading several copies of one scene at once, each with its own physics. | Each dungeon run is a stacked copy of the **DungeonInstance** scene. |
| **Lag compensation** | When the server checks if an attack hit, it rewinds everyone to where the attacker saw them. | `NetLagCompensation`, up to 350 ms. |

## 3. What runs where

| Thing | Server (or host) | Owner's client | Other clients |
|---|---|---|---|
| Your character's movement | Runs the real movement from your inputs | Predicts it at once, corrected by the server | Show it smoothly |
| Attacks, abilities, damage | Decides hits and damage | Sends "I attack / I cast" | See the animation and effects |
| Health, buffs, death | Decides | Shows (plus mana, XP, needs: owner only) | Show health and effects |
| Mobs and their AI | Spawn and run AI | Show | Show |
| Inventory, money | The truth | Shows its own; every action is a request | Never see another player's bag or money |
| Terrain | Generates from the seed | Generates the same terrain from the same seed | Same |
| Dungeons | Generates. **The seed never leaves the server** | Receives each floor's layout and builds it | Same, only for players in that dungeon |
| Camera, UI, keyboard | Off on a dedicated server | On | Off for other players' characters |

The rule that makes single player keep working: gameplay scripts never call Fish-Net directly. They ask small
always-compiled helpers in `Networking/Core` (namespace `GameNet`):

* `NetAuthority.IsServer`: "does this process decide?" (true offline, on a host and on a dedicated server).
* `NetAuthority.HasLocalPlayer`: "does this process have a player?" (false only on a dedicated server).
* `NetSpawn.Spawn(go)` / `NetSpawn.Destroy(go)`: spawn and remove objects (plain Unity calls offline).
* `NetScenes`: load scenes and move players between them.
* `LagCompensation.For(attacker)`: rewind for a hit check.

Offline these answer "single player" and nothing changes. With Fish-Net, the `Networking/Runtime` scripts plug in.

## 4. The Bootstrap scene, explained

The **Bootstrap** scene is the first scene of the build and the "power switch" of multiplayer. It contains no gameplay,
just the objects that start and run the session:

```
Bootstrap.unity
├─ NetworkManager            (Fish-Net) + its managers:
│   ├─ Tugboat               transport, port 7770
│   ├─ TimeManager           the tick clock
│   ├─ ServerManager / ClientManager
│   ├─ SceneManager          loads scenes over the network
│   ├─ PredictionManager     movement prediction
│   ├─ ObserverManager       who sees what (uses RealmObserverCondition)
│   ├─ NetSession            keeps GameNet.NetAuthority in step with Fish-Net
│   ├─ NetBootstrap          the start menu and command line (Single player / Host / Join / Dedicated server)
│   ├─ NetRealmScenes        loads the City for each player, moves players between City, World and dungeons
│   ├─ NetPlayerSpawner      creates each player's character when they arrive in the City
│   ├─ NetLagCompensation    remembers positions for fair hit checks
│   └─ HeadlessServer        frame cap, no audio, status log on a dedicated server
└─ Network World             a scene NetworkObject every client sees, holding the global relays:
    ├─ NetHubWorld           world cycle timer and hub messages
    ├─ NetDuels              duel requests and duels
    ├─ NetTrading            player-to-player trading
    ├─ NetWorldChunks        world terrain changes (removed trees, rocks) and weather
    └─ NetSocial             parties, guilds / fame / levels, inspecting other players
```

**What happens when you press Play in Bootstrap:**

1. `NetBootstrap` reads the command line. With nothing on it, it shows a menu: **Single player**, **Host**, **Join**,
   **Dedicated server**.
2. *Single player* loads the City scene directly. No network at all.
3. *Host* or *Dedicated server* starts the Fish-Net server. `NetSession` switches `NetAuthority.Mode` to Host or
   Server. `NetRealmScenes` loads the **City** on the server.
4. A client (or the host's own player) connects. `NetRealmScenes` makes it load the City.
5. When that client is in the City, `NetPlayerSpawner` spawns the **player prefab** at the City spawn point, owned by
   that client.
6. On the owner's machine, `NetPlayer` turns on the camera, input and UI for that character. On every other machine
   it turns them off for that copy.
7. When the player walks through the City gate, `NetRealmScenes` moves them to the **World** scene (their client loads
   it). A world portal moves them into their own stacked **DungeonInstance** scene.

## 5. Setting it up, step by step (do this once)

Menus are under **Tools ▸ SimpleMovements ▸ Network**.

1. **Install Fish-Net** (Asset Store or GitHub, version 4.x). Wait for the recompile. Check *Project Settings ▸ Player
   ▸ Scripting Define Symbols* contains `FISHNET`.
2. **Set up the hub world first** (City and World scenes): see *Hub World 05 - Setup and Configuration*.
3. **Create Bootstrap Scene.** Creates `Assets/Scenes/Bootstrap.unity`, puts it first in *Build Settings*, creates the
   observer condition asset and `Resources/SimpleMovements/Networking/NetWorldItem.prefab` (the networked carrier for dropped items).
4. **Create Dungeon Instance Scene.** The empty template each dungeon run stacks.
5. **Build Item Database** and **Build Ability Database.** Gives every item and ability a stable number that is the
   same on every machine (messages send the number, not the asset).
6. **Make your player networked.** Select the player prefab in the Project window, then **Make Selected Prefab
   Networked ▸ Player**. It adds a NetworkObject and these components to the root: `NetPlayer`, `NetPlayerMovement`,
   `NetAbilityCaster`, `NetWeaponCombat`, `NetCombatStatus`, `NetInventory`, `NetWallet`, `NetPlayerRealm`,
   `NetInteractor`. It also sets up model smoothing. Then open the Bootstrap scene and assign the prefab on
   **Net Player Spawner ▸ Player Prefab** (it is filled automatically when the hub world settings name it).
7. **Make the rest networked:**
   * **Make All Mob Prefabs Networked** (adds `NetMob`, `NetCombatStatus`, `NetAbilityCaster`, `NetworkTransform`).
   * **Make Selected Prefab Networked ▸** *Summon*, *Pickup or Loot Drop*, *Ability Pickup*, *World Portal*,
     *Interactable (merchant, chest, door, gate)* for those prefabs.
8. **Validate Network Setup.** Lists anything missing, with what to do. Also open *Tools ▸ SimpleMovements ▸
   Validation* for the full project check.
9. If the Console says a scene object is "expected to be initialized" or has a bad scene id, open the scenes and run
   **Fix Scene Network Ids (open scenes)**.

Do **not** put these on the player: a `NetworkTransform` (movement is predicted by `NetPlayerMovement`; a
NetworkTransform fights it and causes shaking), or Fish-Net's own `PlayerSpawner` (use `NetPlayerSpawner`).

## 6. Testing on your own machine

| I want to... | Do this |
|---|---|
| Test quickly in the editor | **Play In Editor As ▸ Host**. You are the server and a player. |
| Test the game without network | **Play In Editor As ▸ Single Player**, or the *Single player* button. |
| Have 2 players | **Build ▸ Server And Client**, then **Run ▸ Server And Two Clients**: a headless server plus two game windows, all on `127.0.0.1`. |
| Play in the editor next to a built server | **Run ▸ Local Dedicated Server**, then **Play In Editor As ▸ Client**. |
| Stop the launched programs | **Run ▸ Stop Launched Processes**. |
| See the connection state | Press **F9** in game (mode, players, ping, tick rate). |
| Read server logs | `Builds/*.log`. |

Command line for built games (all optional; everything defaults to this machine):

```
Server.exe -batchmode -nographics -server -port 7770
Client.exe -client -address 127.0.0.1 -port 7770 -name Ana
Game.exe -host            (or -offline)
          -maxplayers 16   -tickrate 30   -shard 1234
```

To play with a friend on your network, start a host or server on one PC and join with `-address <that PC's IP>`.
Port 7770 must be allowed by the firewall.

## 7. Following one action through the network

**Walking (predicted):**
1. Ana's client reads W and builds a `MovementInput` every tick.
2. It moves Ana's character at once (prediction) and sends the input to the server.
3. The server runs the same `MovementStateMachine.Simulate` with that input. That is the real position.
4. The server sends back a snapshot. If Ana's client was wrong, it snaps to the server's state and replays the inputs
   sent since then (reconciliation). Normally nothing visible happens.
5. Bram's client receives Ana's movement and shows it smoothly.

**Attacking (server decides):**
1. Ana clicks. Her client sends an attack intention with her aim and the tick she was looking at.
2. The server checks it is allowed (she has the weapon, is alive, is not attacking too fast).
3. It rewinds the targets to where Ana saw them (lag compensation), runs the hit check, applies damage.
4. Health changes reach every client that sees the target. The attack animation plays on every client.

**Picking up an item:**
1. Ana presses interact on a sword. Her client sends "pick up this world item".
2. The server checks distance and bag space, moves the item into her inventory and despawns the world item for
   everyone.
3. Only Ana's client receives her new inventory.

**Entering a dungeon:**
1. Ana walks into a portal. Only the server reacts to the trigger.
2. The server loads a new stacked copy of the DungeonInstance scene and generates the dungeon there.
3. Ana is moved into that scene. Her client loads it and receives the floor layout (never the seed), then builds it.
4. Her controls stay frozen until her client reports the floor is built, then she can move.

## 8. Common problems

| Symptom | Usual cause | Fix |
|---|---|---|
| Two characters, or a character standing still in the City | A player placed in the scene for single player | Removed automatically in network sessions; or delete it from the scene. |
| "Expected to be initialized" / scene id errors | Scene objects created without ids | **Fix Scene Network Ids (open scenes)** with the scenes open, then save. |
| My character shakes or jitters | A NetworkTransform on the player, root motion on, or the camera updating at the wrong time | Remove NetworkTransform from the player; turn off *Apply Root Motion*; CinemachineBrain *Update Method* = Late Update. See page 02. |
| Keys work for one player but move another | Missing `NetPlayer`, so input is not switched off on other copies | Re-run **Make Selected Prefab Networked ▸ Player**. |
| Mobs visible only on the host | Mob prefab not networked | **Make All Mob Prefabs Networked**. |
| Item names wrong or pickups refused | Item database out of date | **Build Item Database** after adding items. |
| Client cannot connect | Server not running, wrong address or port, firewall | Check F9 on the server, use `127.0.0.1:7770` locally. |

Next: **01 - Architecture and local setup**, then the system you care about. **12 - Script and component reference**
lists every network script and where it goes.
