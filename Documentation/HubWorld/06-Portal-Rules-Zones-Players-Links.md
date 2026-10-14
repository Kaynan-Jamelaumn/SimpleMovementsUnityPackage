# Hub World 06: Portal Rules, Zones, Player Limits and Linked Portals

**Scripts:** `Essentials/Portal.cs`; `Procedural/World/Spawnable/SpawnablePortal.cs`, `SpawnableMob.cs`,
`PortalSettings.cs`, `PortalSpawner.cs`, `PortalSitePlanner.cs`, `SpawnZones.cs`, `WorldSpawnHooks.cs`;
`Procedural/Dungeon/Runtime/DungeonSession.cs`, `DungeonEntrances.cs`.

---

## 1. Portals and mobs by zone

Each portal type (*EndlessTerrain ▸ Portal Settings ▸ Prefabs*) and each mob type (*Mob Settings ▸ Prefabs*) has a
**Zones** filter:

| Field | Meaning |
|---|---|
| Enabled | Off = any zone |
| Zone Names | Zones it may appear in, as named in *Hub World Settings ▸ Zones ▸ Rings* (case does not matter). Empty = any name |
| Min Ring / Max Ring | Ring range: 0 = the first ring around the city. Max −1 = no limit |
| Pvp | Any / Only Pvp / Only Non Pvp |

With the default rings (Safe Zone = ring 0, Frontier = 1, Dangerous Zone = 2, The Abyss = 3):

* **Beginner dungeon only near the city:** Zone Names = `Safe Zone`.
* **Raid portal only far out:** Min Ring = 2.
* **Arena portal only where PvP is on:** Pvp = Only Pvp.

The Hub World installs the zones (`WorldSpawnHooks.ZoneAt`). In a world without zones the filter lets everything
through. The portal type is picked at the planned point of each site, deterministically from the world seed, so every
machine sees the same portals. A portal type that is not allowed in a zone is simply never picked there.

## 2. What happens to a portal after use

There are three levels. The most specific level that is set wins:

1. The **Portal** component's own **After Use**: *Use Spawner Rule* (default), *Stays Open*, *Close After Entering*,
   *Close After Completing*, and *Reopen After* (seconds, 0 = never).
2. The portal **type**: **Override After Use** with its own rule and reopen delay.
3. **Portal Settings ▸ After Use** (the old global setting).

| Rule | Private dungeon (Separate Runs) | Shared or linked dungeon |
|---|---|---|
| Stays Open | Stays | Stays |
| Close After Entering | Closes as soon as the group goes in | Closes when the dungeon is full (Max Players), or when it ends if there is no limit |
| Close After Completing | Closes when the dungeon is completed through its exit portal | Same |

A spawned portal closes by closing its **site**. The site's linked portals close with it, and the spawner remembers the
closure until the site reopens. A hand-placed portal is hidden and lets nobody in, together with the hand-placed
portals that share its Link Id. In a network session, a hand-placed portal that never reopens is removed for everyone.
One that reopens later stays visible on the clients while closed, but nobody can enter it.

## 3. Player limit per portal

**Max Players** (on the Portal, or on the type, where a value above 0 wins): the most players in the portal's dungeon
at once. 0 means no limit; 1 means solo only.

**Sharing** (on the Portal, or the type's **Sharing** override):

* **Separate Runs** (default): every group that enters gets its own dungeon. Max Players limits the group that goes in
  together: the player who entered first, then nearby party members (*Bring Party Within*).
* **Shared Run**: everyone who enters while the portal's dungeon is running joins that same dungeon, up to Max Players.
  A player who arrives while it is full is turned away. The server logs it and raises `Portal.EntryRefused`, so a HUD
  can show a message. The players inside can work together, or fight each other where the zone rules allow PvP.

Players who join a running dungeon are placed in it straight away. In multiplayer their client loads the run's scene
and receives the floors reached so far. Their controls stay frozen until their client has built the floor they stand
on, as for the first group.

**What players see.** A player who selects a dungeon portal (clicks it) or walks up to one sees how many slots it has
left: "3 of 5 slots free", "Full (5 / 5 players)", "4 slots free" for private runs, "No player limit", or "Closed"
(see Player 09, *Dungeon portal panel*). From code: `portal.Slots` (`PortalSlots.Remaining`, `IsFull`, `inside`,
`running`...). On a network client the answer comes from the server through the `NetPortals` relay.

## 4. Linked portals: several entrances to one dungeon

Linked portals lead into **one** dungeon instance. Whoever enters any of them while it is running joins it.

**Spawned linked portals.** On the portal type:

| Field | Meaning |
|---|---|
| Linked Portals | 1 = a normal portal. 2-4 = that many portals per site, all leading into one dungeon |
| Linked Spacing | Distance between them (world units). They stay inside the site's chunk: with 240-unit chunks keep it under about 120 |
| Linked Arrival | *Same Entrance*: everyone arrives together. *Separate Entrances*: each portal has its own arrival point |

The linked portals share the site, so they appear, close and reopen together. They take the dungeon's seed from the
main portal's spot, so the dungeon is the same whichever portal is entered first. If no flat spot is found for a
linked portal, it is left out (with *Enable Detailed Logging*, the spawner logs why).

**Hand-placed linked portals.** Give two or more Portal components the same **Link Id** and choose each portal's
**Arrival Point**:

* 0 = the dungeon's entrance.
* 1, 2... = other arrival points on the first floor.

For example, Link Id `arena` with arrival points 0 and 1 brings two teams to opposite sides of the same dungeon.

**Where separate arrival points are:** entrance 0 is the dungeon's spawn. Each further point is the open, walkable
cell of the arrival floor farthest, by walking distance, from the spawn and from the points before it. Boss, guardian,
vault, trap, puzzle, ambush and secret rooms are never used, and neither is the floor's way down. A floor too small for
another point (less than 4 cells apart) uses the spawn.

The dungeon belongs to the portal entered first: its Max Players, profile and difficulty apply.

## 5. Multiplayer: how the endless terrain streams

The terrain is not sent over the network. Every machine generates it locally from the same seed, so the chunks are
identical everywhere. Only the changes are synchronised: removed objects (trees, rocks) and the weather.

**Which chunks are loaded:**

* **Server or host:** chunks are created around **every player** who is in the World, or travelling to it. Each chunk
  is kept while any of those players is within the view distance, and unloaded when nobody is. This is what lets the
  server place mobs and portals and run physics wherever any player is.
* **Pure client:** chunks are created around **its own player only**. Other players far away have no terrain on that
  client, which is fine because their movement is decided by the server, where their ground exists.
* **Level of detail** follows the machine's own player (on a dedicated server, the first player).

**Spawning** of mobs and portals happens only on the server, in the chunks loaded there. They reach the clients near
them as network objects.

The streamed-ground guard (*Player 07 §3*) uses the same rules. The server checks every player against its chunks;
each client checks only its own player.
