# Hub World 05 — Setup and Configuration

---

## 1. Step by step

| # | Do | Result |
|---|---|---|
| 1 | *Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Hub World Settings (Resources)* | `Assets/Resources/SimpleMovements/Settings/HubWorldSettings.asset` |
| 2 | Select your player prefab in the Project window, then *Hub World ▸ Build City Scene…* | A City scene: ground and walls (placeholders), the **City Gate** on the south side, *Realm: City* with the arrival point (inside the gate) and the spawn point (the square), the **Hub World Director**, your player at the spawn. Saved, first in Build Settings, its name written to the settings. |
| 3 | Replace the placeholder walls with your city (keep a City Gate and the Realm: City points). Add NPCs and merchants — no mobs. | |
| 4 | Open the scene with your `TerrainGenerator`, `EndlessTerrain` and `WeatherSystem`; *Hub World ▸ Make Open Scene The World Scene* | The exterior copy of the city at *City Center* (walls, sealed inside), the **Return Gate**, *Realm: World* with the arrival point outside the gate; the EndlessTerrain's Viewer cleared (the director sets it). Saved, added to Build Settings. Remove any player or camera from this scene. |
| 5 | Optional: dimensional modifiers (*Assets ▸ Create ▸ SimpleMovements ▸ Hub World ▸ Dimensional Modifier*) in *Settings ▸ Modifiers* | |
| 6 | *Hub World ▸ Validate Setup* | A checklist in a dialog and the Console |
| 7 | Open the City scene and press Play | |

**Testing a jump quickly:** *Cycle Minutes* 3 (warnings at 1 and 0.5 min), or right-click the Hub World Director ▸
*Skip To 70 Seconds Before The Jump* / *Jump Now* in Play mode (`HubWorldDirector.JumpNow()`,
`SetSecondsUntilJump(s)` from code).

**Testing duels alone:** put a second player object in the City scene, enable *Auto Accept For Other Players* on the
`DuelSystem` (added to the director at runtime) and challenge it.

## 2. Settings (`HubWorldSettings`)

### Scenes

| Setting | Default | Meaning |
|---|---|---|
| City Scene Name | City | the permanent city (Build Settings) |
| World Scene Name | World | the open world (Build Settings) |
| Load City On Start | on | load the City when the game starts in another scene (a bootstrap) |
| Unload World When Empty | on | unload the World while nobody is out (local play). A dedicated world server keeps it loaded. |

### Dimensional cycle and atmosphere

| Setting | Default | Meaning |
|---|---|---|
| Cycle Minutes | 90 | length of a cycle |
| Warning Minutes | 10, 5, 1 | warnings before the jump |
| Shard Seed | 0 | seed of the sequence of worlds (0 = random each session) |
| Use Unscaled Time | off | real time (pausing does not pause the cycle) |
| Close Gate Seconds Before Jump | 30 | the City Gate seals this long before the jump |
| Warning / Jump / Lost Text | … | `{0}` = time left / what was lost |
| Warning / Final Warning / Jump Sound, Sound Volume | — | 2D one-shots |
| Atmosphere Ramp Seconds | 600 | the sky drifts toward the rift during this final window |
| Rift Fog Color / Fog Density / Ambient Color / Sun Multiplier | violet / 0.02 / violet / 0.4 | the sky at the jump |

### Lost between dimensions

| Setting | Default | Meaning |
|---|---|---|
| Loot Loss ▸ Item Fraction | 0.25 | share of carried items lost (see 02 §4) |
| Loot Loss ▸ Include Hotbar | on | the hotbar counts as carried |
| Loot Loss ▸ Currency Fraction | 0 | share of money lost |
| Loot Loss ▸ Protected Items | — | never lost |
| Disorientation ▸ Enabled / Label / Duration | on / Dimensional Disorientation / 300 s | the debuff |
| Disorientation ▸ Character Stats / Combat Stats / Attached Effect | −20 % move, +15 % damage taken, −30 % stamina & mana regen, −15 % attack & cast speed | what it does |

### Modifiers

| Setting | Default | Meaning |
|---|---|---|
| Modifiers | — | `DimensionalModifier` assets (02 §7) |
| No Modifier Weight | 2 | weight of a cycle without one |

### Zones

| Setting | Default | Meaning |
|---|---|---|
| City Center | (0, 0) | where the city stands in the world (x, z) |
| City Wall Radius | 80 | inside: the city (never PvP) |
| Keep Clear Radius | 140 | no objects, mobs or portals around the city |
| Flatten City Ground / City Ground Height / Flatten Blend | on / 20 / 80 | level the ground under the city's exterior |
| Rings | 4 rings (03 §1) | name, outer radius, PvP, dungeon difficulty start/end, mob strength start/end, reward multiplier, colour |
| Difficulty Per Km Beyond / Max Difficulty | 0.5 / 8 | past the last ring |
| Scale World Mobs | on | apply the ring's mob strength |
| PvP In Dungeons | off | members of the same run may fight |
| Check Interval | 0.2 s | how often each player's zone is checked |

### City, duels, dungeons, travel

| Setting | Default | Meaning |
|---|---|---|
| City Abilities Visual Only | on | 04 §1 |
| Duels ▸ Enabled / Allow Outside City | on / on | |
| Duels ▸ Challenge Range / Challenge Timeout / Countdown / Max Duration | 8 m / 30 s / 3 s / 180 s | |
| Duels ▸ Arena Radius / Leaving Arena Loses / Show Arena | 12 m / on / on | 0 = no arena |
| Duels ▸ Loser Health / Heal After Duel / Reset Cooldowns At Start | 5 % / on / on | |
| Dungeons In Own Scenes | on | one scene (own physics, own slot) per dungeon run; off = classic mode |
| Dungeon Scenes ▸ Own Physics / Slot Origin / Slot Size / Slots Per Row | on / (0, −4096, 0) / 2048 / 16 | 01 §5 |
| Arrival Ground Timeout | 20 s | longest wait for ground under an arrival point |
| Log Events | off | cycle, travel and zone events in the Console |

## 3. Components

| Component | Where | Notes |
|---|---|---|
| `HubWorldDirector` | City scene (moves itself to DontDestroyOnLoad) | *Settings* (empty = Resources), *Create Hud*, *Adopt Unplaced Players*; adds `DuelSystem`, `CycleAtmosphere`, `CityAbilityRelay`, `HubWorldHud` |
| `RealmScene` | one root object in the City scene and one in the World scene | *Realm*, *Arrival Point*, *Spawn Point* (City), *Arrival Spread*, *Hide While Local Player Away* (City) |
| `RealmKeepActive` | City scene roots that must stay on while the local player is away | |
| `RealmGate` | the City Gate (City scene), the Return Gate (World scene) | *Kind*, *Arm Delay*, *Message Cooldown*; needs a trigger collider (added if missing) |
| `PlayerRealmState` | every player (added by the director) | realm, destination, world cycle, zone; events |
| `DuelChallengeTarget` | every player (added by the duel system) | the "Challenge to a duel" interaction |
| `ZoneMobStrength` | world mobs spawned away from the city | their strength |

## 4. Editor menus and tests

| Menu (*Tools ▸ SimpleMovements ▸ Hub World*) | Does |
|---|---|
| Create Settings Asset (Resources) | the settings asset |
| Build City Scene… | §1 step 2 |
| Make Open Scene The World Scene | §1 step 4 |
| Add City Gate / Return Gate At Scene View Pivot | a gate (pillars, lintel, glowing veil, trigger) |
| Validate Setup | Build Settings, realm scenes, gates, director, viewers, portals, modifiers |
| Help | the steps |

**Tests** (*Window ▸ General ▸ Test Runner ▸ Edit Mode*, `CustomEditor/HubWorld/HubWorldTests.cs`): the cycle clock
(order, cadence, gaps, skipping), seeds and weighted rolls, the zone map (rings, monotonic difficulty, cap), loot loss
(bounds, protection, averages, determinism), duels (full flow, decline, expiry, time-out), the jump's rules, combat rule
priority, levelled areas, physics routing.

## 5. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Walking into the City Gate does nothing | no director; the player has no CharacterController / Rigidbody; the gate has no trigger; the gate is sealed (message shown) | Validate Setup; the director's *Log Events* |
| "cannot load the World scene" | not in Build Settings, or the name differs | Make Open Scene The World Scene; check *World Scene Name* |
| The player falls through the world on arrival | the ground did not generate within *Arrival Ground Timeout* (huge view distance, slow machine) or the arrival point is over water | raise the timeout; move the arrival point onto the levelled area |
| Terrain appears inside the city | the City scene is not marked (no `RealmScene`) or *Unload World When Empty* is off | mark the City; keep the setting on for local play |
| The city's walls float / sink in the world | *City Ground Height* differs from where the World's walls stand | set it to the walls' base height (and keep *Flatten City Ground* on) |
| Trees / mobs inside the city exterior | *Keep Clear Radius* smaller than the walls | raise it |
| No PvP in a dangerous zone | the other player is in a safe ring (both must be in a PvP ring), same party, or in a duel with someone else | 03 §2 |
| Abilities do nothing in the city (they go on cooldown) | by design (visual only); the Console says so once per player | *City Abilities Visual Only* off, or duel |
| Camera passes through dungeon walls | the camera's collision uses Unity's default physics scene, the dungeon has its own | *Dungeon Scenes ▸ Own Physics* off (the slots still keep dungeons apart) |
| A dungeon is dark | its theme has *Apply Atmosphere* off (a fresh dungeon scene has default lighting) | turn it on |
| The world is the same every cycle | the World scene's TerrainGenerator is created after the load (not in the scene) | keep the TerrainGenerator in the World scene (it reads `SeedOverride` on Awake) |
| Players in the city get attacked by world mobs | the World stays loaded while someone is in the city at the same place (*Unload World When Empty* off in one process) | keep it on, or author the City away from the world's city spot |
| The city's copy in the World has a square block poking through its walls | built before the fix | run *Make Open Scene The World Scene* again and choose **Rebuild** |
| Patches of water around the city in the World | lakes / rivers placed before the ground was levelled | fixed: the city area and its blend ring get no lakes, rivers or sea. If the whole ring is flooded, *City Ground Height* is below the terrain's water level (Validate Setup says so) - raise it, then rebuild the exterior |
| Network session: the player cannot walk forward, or shakes, at the spawn | the single-player player placed by *Build City Scene* was made networked with the prefab and stayed in the City, where the real player spawns | fixed: placed players are removed in network sessions (or delete the player from the City scene yourself) |
