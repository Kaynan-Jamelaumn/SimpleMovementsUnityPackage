# Networking (Fish-Net) - Stages 2 and 3

Server-authoritative multiplayer for the hub world: a dedicated server (headless) or a host, clients with predicted
movement, authoritative combat with lag compensation, synchronized mobs, inventory, trading, world cycles and
dungeons on Fish-Net's scene stacking. Everything runs **on one machine** (`127.0.0.1`) - no remote server is needed.

Single player keeps working unchanged, with or without Fish-Net installed.

| Page | Contents |
|---|---|
| [00 - Start here: multiplayer basics](00-Start-Here-Multiplayer-Basics.md) | **New to Fish-Net? Read this first, then 13.** Server/client/host, every network word explained, the Bootstrap scene, step-by-step setup, local testing, how an action travels, common problems |
| [01 - Architecture and local setup](01-Architecture-and-Local-Setup.md) | Modes, the Bootstrap scene, bridges, interest management, lag compensation, setup and running locally |
| [02 - Movement prediction](02-Movement-Prediction.md) | Client-side prediction and server reconciliation of the character |
| [03 - Abilities](03-Abilities.md) | Casting through the server, cosmetic mirrors on clients |
| [04 - Status and death](04-Status-and-Death.md) | Health and resources, crowd control, buffs, XP, death and respawn |
| [05 - Weapons and lag compensation](05-Weapons-and-Lag-Compensation.md) | Melee and ranged attacks, blocking, rewinding hit queries |
| [06 - Mobs and AI](06-Mobs-and-AI.md) | Server-run AI, network-spawned mobs |
| [07 - Inventory and items](07-Inventory-and-Items.md) | Authoritative inventory, equipment, world items, storage |
| [08 - Interaction, merchants and trading](08-Interaction-Merchants-Trading.md) | The interaction relay, shops, currency, player trading |
| [09 - Hub world sync](09-Hub-World-Sync.md) | Dimensional cycles, realms and travel, zones, duels, messages |
| [10 - Dungeons](10-Dungeons.md) | Stacked dungeon scenes, layouts without seeds, progressive reveal |
| [11 - World terrain](11-World-Terrain.md) | The open world on server and clients |
| [12 - Script and component reference](12-Script-and-Component-Reference.md) | Every network script: where it goes, who runs it, what it does, and the menu that adds it |
| [13 - How the code works (guided tour)](13-How-The-Code-Works.md) | **For beginners reading the code:** the three layers and the bridge pattern, the main controllers, which script handles players / movement / inventory / combat / mobs, the startup order with method names, every RPC per system, how to read a Net script, and a small example of your own networked feature |
| [14 - Networked movement: the complete deep dive](14-Networked-Movement-Deep-Dive.md) | **Everything about player movement in multiplayer:** before/after, every script and manager, what a bridge is, how the server knows whose input is whose, what runs on server/owner/other clients, one tick step by step with code, prediction, reconcile and replay, spectators, smoothing, special cases, travel between City/World/dungeons, inventory and abilities, a full timeline, debugging |

Code: `Networking/Core` (always compiled), `Networking/Runtime` (compiled with Fish-Net), editor tools in
`CustomEditor/Networking` (Tools ▸ SimpleMovements ▸ Network), tests in `CustomEditor/Networking/Tests`.
