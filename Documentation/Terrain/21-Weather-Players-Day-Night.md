# Terrain 21: Weather and players, day and night, time and weather spawn rules

**Scripts:** `Procedural/World/Weather/PlayerWeatherEffects.cs`, `DayNightCycle.cs`, `WorldTime.cs`,
`Procedural/World/Spawnable/SpawnConditions.cs`; changed: `WeatherSystem.cs` (`WeatherAt`, `Daylight`),
`WeatherSystem.Effects.cs`, `WeatherSystem.Storms.cs` (`StrikeLightningAt`), `SpawnableMob.cs`, `SpawnablePortal.cs`,
`WorldSpawnHooks.cs`, `MobSpawner.cs`, `PortalSpawner.cs`, `Inventory/ItemSpawner.cs`; editor
`CustomEditor/TerrainGenerator/WeatherSetupMenu.cs`, tests `WeatherRuleTests.cs`.

---

## 0. Where each component goes

| Component | Where | How many | Why |
|---|---|---|---|
| **Weather System** | In the **World scene**. The object with `EndlessTerrain` and the `TerrainGenerator` is a good place, but any object of that scene works: it finds the Terrain Generator and the viewer by itself | One | The weather belongs to the open world. The "only players in the World get weather" rule reads the Weather System's scene, so it must not be in the City, the Bootstrap scene or a persistent object |
| **Player Weather Effects** | On the **same object as the Weather System** (its inspector has an Add button). **Not on the player** | One for all players | One component checks every player at their own position, on the machine that runs the rules (offline, or the server). On the player prefab it would run once per player copy, including every client's copies |
| **Day Night Cycle** | **One** in a scene that loads **first and stays**: the **Bootstrap** scene in a network game, else the first gameplay scene (the City). Not on the player, not in every scene | One per game | It keeps itself across scene changes (*Persistent*). A second one, for example in a scene loaded later, removes itself, so an extra copy does no harm |
| **Weather Condition** assets | Anywhere in the project (default: `Assets/Resources/SimpleMovements/World/Weather`), listed on the Player Weather Effects | As many as you like | Each one is one condition (Soaked, Chilled…) you edit in its own asset |

**Multiplayer:**
* Each player is affected by the weather **where they stand**: one player can be Soaked in a storm while another is
  dry on the coast. The server checks every player separately (position, roof above, head under water).
* **Time of day is shared:** the server's Day Night Cycle is the only real clock. Its hour is sent to every client
  every 2 seconds (the World relay `NetWorldChunks`), whether the client is in the City, the World or a dungeon. So
  everyone has the same sunrise, sunset and night spawns. Clients keep counting smoothly between updates.

## 1. Questions and answers

| Question | Answer |
|---|---|
| Is the weather different where each player is? | **Yes.** The weather is a function of *place* and *time* (§13). Two players far apart have different weather: one can be in a blizzard on a mountain while the other is in the sun on the coast. |
| Does the weather only run where there are players? | **Yes.** Nothing is simulated for the whole world. The weather is only *computed* for the spots that are asked about: the local player (effects), each player (statuses) and the spots where something may spawn. The rain, snow and fog *effects* exist only around your own player on your own machine. A dedicated server shows no effects at all. |
| Can I see it? | **Yes.** Rain, snow, hail, sand and dust, mist, fog, darker light under clouds, lightning bolts and flashes, a tornado funnel, trees swaying with the wind, and wet or snowy ground (with the package terrain shader). No assets needed. |
| Can I hear it? | **Yes, once you give it sounds.** On the Weather System ▸ **Audio**, assign *Rain Loop*, *Wind Loop*, *Hail Loop*, *Sandstorm Loop*, *Tornado Loop* and *Thunder Clips* (several). Each loop's volume follows the weather; thunder comes after the flash, later when the strike is far. Empty fields are silent. |
| Does the weather affect the players? | **Now yes**, with **Player Weather Effects** (§2): Soaked, Chilled, Frostbite, Heatstroke, Sandblasted, Windswept, Pelted by Hail, the body temperature, and optional lightning strikes. |
| Can monsters, portals or loot depend on the weather or the time? | **Now yes**, with **Time and Weather** rules on every mob type, portal type and item drop (§4). |
| Is there a real day and night? | **Now yes**, with the **Day Night Cycle** (§3). The old Day Night Manager (skybox only) still works and is used when there is no Day Night Cycle. |

## 2. Player Weather Effects (statuses from the weather)

**Add it:** select the Weather System; its inspector has an **Add Player Weather Effects** button. You can also use
**Tools ▸ SimpleMovements ▸ Weather ▸ Add Player Weather Effects**.

Every *Check Interval* (2 s) it looks at the weather **at each player's own position** and gives that player the
matching statuses. They are normal timed buffs and debuffs (`TimedStatModifiers`), so they:
* show on the status icons;
* change the stats exactly and are removed exactly;
* reach every client in multiplayer through the existing status replication.

A status is renewed while the weather goes on. It ends when the weather stops, or *Linger Seconds* later (wet
clothes dry slowly).

| Default status | When | What it does |
|---|---|---|
| **Soaked** | Rain ≥ 0.35, under the open sky | −20% Lightning resistance, +15% Fire resistance, −10% Stamina Regen; feels 8 points colder; lingers 20 s |
| **Chilled** | Air ≤ 2 °C (roof or not) | −8% Move Speed, −10% Stamina Regen |
| **Frostbite** | A Blizzard, under the open sky | −15% Move Speed, 0.5 Ice damage per second; feels 10 points colder |
| **Heatstroke** | Air ≥ 35 °C, under the open sky | −20% Stamina Regen, +15% Stamina Cost, −0.4 Thirst per second; lingers 10 s |
| **Sandblasted** | Dust ≥ 0.5, under the open sky | −10% Move Speed, 0.3 Earth damage per second |
| **Windswept** | Wind ≥ 14 m/s, under the open sky | −10% Move Speed, −15% Projectile Speed |
| **Pelted by Hail** | Hail ≥ 0.4, under the open sky | 1 damage per second |

Stronger weather gives a stronger status: half strength at the threshold, full strength in the worst weather. Turn
this off per rule with *Scale With Intensity*.

### Every condition is configured in the Inspector (no code)

The seven defaults above are only a starting point written in code. Everything about them, and any condition you add,
is edited in the Unity Editor. Two ways:

1. **Weather Condition assets (recommended).** Run **Tools ▸ SimpleMovements ▸ Weather ▸ Create Default Weather
   Condition Assets**. It writes Soaked, Chilled, Frostbite, Heatstroke, Sandblasted, Windswept and Pelted by Hail as
   assets in `Assets/Resources/SimpleMovements/World/Weather`, and puts them in the Player Weather Effects' **Conditions**
   list. The component's own copies are removed so nothing applies twice. Select an asset and change it.
   * **New condition:** *Create ▸ SimpleMovements ▸ Weather ▸ Weather Condition*, then drag it into *Conditions*.
   * **Same set in several scenes:** use the same assets.
2. **Directly on the component:** the **Rules** list holds conditions written on the Player Weather Effects itself. Add,
   remove or edit entries. Right-click the component ▸ *Reset Conditions To Defaults* brings the seven back.

Both lists are used together. Each condition has:

| Group | Fields |
|---|---|
| Identity | *Enabled*, *Label* (the status icon's name), *Debuff*, *Element* |
| **When** | *Measure*: Rain, Snow, Hail, Thunder, Dust, Fog, Heat, Tornado (0–1 amounts), Wind Speed (m/s), Colder Than / Hotter Than (°C), or Weather Is with a list of weather types. *Threshold*, *Weathers*, *Needs Open Sky*, *Not Underwater* |
| **Strength** | *Scale With Intensity*, *Strength At Threshold* (0.5 = half strength when it just starts), *Custom Full Strength* + *Full Strength At* (where it reaches full strength, e.g. −10 °C), *Linger Seconds* |
| **What it does** | *Combat Stats* (Defense, Critical Chance…), *Resistances* (per element), *Character Stats* (Move Speed, Stamina Regen, Stamina Cost, Health Regen, Damage Taken…), *Damage Per Second* with *Damage Type* and *Damage Element*, *Status Rates* (drain or fill Health, Stamina, Mana, Hunger, Thirst, Oxygen, Sanity, Sleep or Body Heat per second; Heatstroke drains Thirst by default), *Body Heat Shift*, *Attached Vfx* |
| **Events** | *On Start* / *On End* (UnityEvents with the player's root object). Hook any of your own scripts: play a sound, start a quest step, give an achievement, add a custom status |

The stats, resistances and character stats are applied as a normal timed buff or debuff. They show on the status
icons, replicate to the clients, and are removed exactly when the condition ends. Damage, status rates and events run
where the game rules run (offline or on the server).

**Shelter:** a roof, a cave ceiling, a rock overhang or a tree canopy above the player blocks rules marked *Needs
Open Sky*. The check is a ray upwards from the head, *Shelter Check Height* long, on *Shelter Layers*; exclude your
player layer from them. Cold and wind (*Needs Open Sky* off) reach you anyway. A shelter only brings the felt
temperature part of the way back to comfortable (*Shelter Comfort*).

**Body heat:** with *Drive Body Heat* on, the player's **Body Heat** moves toward the temperature the player feels.
That is the air temperature, with wind chill when it is cold, the shelter and the rules' shifts, turned into the
body-heat scale (50 = normal). Defaults, at 1.6 points per °C from 18 °C:

| Air | Body heat it moves toward | For a human (ideal 18 °C ± 8 °C) |
|---|---|---|
| 18 °C | 50 | Comfortable |
| 5 °C | 29 | Chilly (more hunger, a little slower) |
| −7 °C | 10 | Freezing / Hypothermia (loses health, shivers) |
| 31 °C | 71 | Warm (more thirst and stamina use) |
| 43 °C | 90 | Overheated / Heat Exhaustion (loses health, faints) |

The comfortable range depends on the player's **race / class** (Character Archetype ▸ Survival ▸ Temperature: a desert
race with an ideal of 30 °C is comfortable at 31 °C and chilly at 18 °C), and **Heat / Cold Resistance** (combat stats
from races, classes, traits, equipment, abilities; 100 = immune) keep the body nearer its ideal. See
[Player 12 — Survival](../Player/12-Survival-Death-and-Respawn.md#4-body-temperature).

**No double penalties (*Temperature Handling*):** players with a Body Heat Manager get their cold and heat effects
from the body temperature, so by default (*Skip Temperature Rules*) the conditions that only measure the air
temperature (**Chilled**, **Heatstroke**) are not applied to them, and Ice / Fire conditions (**Frostbite**) are
scaled by the player's Cold / Heat Resistance (immune = no status, no damage). *Skip All Cold And Heat Rules* also
turns Ice / Fire conditions into a pure body-heat shift; *Keep All* applies everything as before.

**Lightning (off by default):** with *Lightning Strikes* on, a player under the open sky in a thunderstorm can be
struck: *Strike Chance Per Minute* × the storm's strength, for *Strike Damage* Lightning damage. The bolt, the flash
and the thunder show on the machine that runs the rules (offline, or the host's own screen). On other clients the
player only sees the damage, so keep this off for multiplayer unless that is fine for your game.

**Where it runs:** where the game rules run (offline, or on the server or host for every player). Clients run
nothing; they receive the statuses, the health and the body heat. Only players **in the Weather System's world**
count. A player in the City, in a dungeon or travelling gets nothing: their weather statuses end and their body heat
goes back to their own ideal temperature.

Events for your UI or sounds: `StatusChanged(player, rule, on)` and `PlayerStruck(player, damage)`.
`TryGetPlayerWeather(player, out weather, out sheltered)` gives the last weather a player was checked in.

## 3. Day Night Cycle

**Add it:** **Tools ▸ SimpleMovements ▸ Weather ▸ Add Day Night Cycle**, in a scene that stays loaded: the City, or
the Bootstrap scene in a network session. It keeps itself across scene changes (*Persistent*).

| Setting | Default | Meaning |
|---|---|---|
| Start Hour | 8 | Hour when the game starts |
| Day Length Minutes | 24 | Real minutes for a whole day (24 = one game hour per real minute) |
| Night Speed | 1 | 2 = nights pass twice as fast |
| Sunrise / Sunset Hour | 6 / 19 | Day is between them |
| Paused | off | Stops the clock |
| Sun | auto | The directional light it turns. Empty = the scene's sun, else the brightest directional light |
| Rotate Sun / Sun Azimuth / Noon Elevation | on / 180 / 65 | The sun rises in the east at sunrise, is in the south at noon (Azimuth 180), sets in the west |
| Control Light / Night Ambient | on / 0.18 | Darkens the sun and the ambient light at night, down to 18% ambient |
| Twilight Color | orange | Sun colour at sunrise and sunset |

**What it does:**
* It turns the sun and darkens the night.
* It gives the Weather System the hour. The weather has misty mornings, stormier afternoons and colder nights, so
  Chilled and the body heat follow the night.
* It is what the "Day only / Night only / Hours" spawn rules and the mobs' *Is Diurnal / Is Nocturnal* read
  (`WorldTime`).

When the Weather System controls the light (Lighting Effects on), the day/night darkening is applied inside it, on
top of the clouds and storms, so the two never fight. Each scene keeps its own lighting. Dungeons are not darkened
at night.

**Multiplayer:** the server's clock is the only real one. Its hour is sent to every client every 2 seconds by the
World relay (`NetWorldChunks ▸ Day Clock`, *Clock Send Seconds*), wherever the client's player is: the City, the
World or a dungeon. All players therefore share the same time of day. Clients keep counting smoothly in between and
correct themselves when they drift. A jump in time (setting `Hour` on the server) is sent at once.

**Events:** `PhaseChanged(previous, now)` with the phases Night, Dawn, Day and Dusk.

**From code:**
* Reading: `WorldTime.Hour`, `WorldTime.IsDay`, `WorldTime.IsNight`, `WorldTime.Phase`.
* Setting: `DayNightCycle.Instance.Hour = 22` (on the server).

## 4. Time and weather spawn rules (mobs, world portals, loot)

Every **mob type** (Mob Settings ▸ Prefabs), **portal type** (Portal Settings ▸ Prefabs) and **item drop** (Item
Spawner) now has **Time and Weather** (`SpawnConditions`). The defaults are *Any time, Any weather*, so nothing
changes until you choose a rule.

| Field | Choices |
|---|---|
| Time | **Any** (doesn't matter) · **Day Only** · **Night Only** · **Hours** (From Hour to To Hour; 22 to 4 crosses midnight) |
| Weather | **Any** (doesn't matter) · **Only In** the listed weathers · **Never In** the listed weathers |
| Weathers | The list: Clear, Cloudy, Windy, Mist, Fog, Rain, Thunderstorm, Hail, Snow, Blizzard, Sandstorm, HeatWave, Tornado |
| Despawn When Unmet | Remove it again when the rule stops being true |

**Examples:**

| You want | Set |
|---|---|
| Zombies only at night, gone in the morning | Mob ▸ Time: Night Only, Despawn When Unmet ✔ |
| A frost wolf in snow and blizzards | Mob ▸ Weather: Only In [Snow, Blizzard] |
| Slimes never in a heat wave or a sandstorm | Mob ▸ Weather: Never In [HeatWave, Sandstorm] |
| A storm dungeon that opens in thunderstorms and closes after | Portal ▸ Weather: Only In [Thunderstorm], Despawn When Unmet ✔ |
| A "midnight" portal | Portal ▸ Time: Hours, 23 to 1 |
| A moonflower that only drops at night | Item Drop ▸ Time: Night Only |
| A mob that doesn't care | Leave Any / Any |

**How each one uses it:**
* **Mobs:** a type whose rule is false at a spot gets weight 0 there, so another type is picked instead. With
  *Despawn When Unmet*, living mobs whose rule ended leave, without counting as killed. They only leave once no
  player of their realm is within *Hidden Spawn Distance*, so nobody sees them vanish and a mob fighting you stays.
* **World portals:** a site whose type's rule is false stays empty. It is checked again every 5 s and the portal
  appears as soon as the rule is true. With *Despawn When Unmet*, an open portal closes when its rule ends. This does
  not count as used and has no reappear delay. It comes back when the rule is true again. The site planning (where
  the portals are) never changes with the weather.
* **Loot:** an item drop whose rule is false at the drop's spot is skipped.

The **weather is the weather at the spot**, not the weather of some player. The **time** is `WorldTime`: the Day
Night Cycle, else the Weather System's Time Of Day, else the old Day Night Manager. Without a Weather System, weather
rules let everything through; without any clock, time rules do. In multiplayer, spawning runs on the server, so the
server's time and weather decide.

*Is Diurnal / Is Nocturnal* on a mob type still only make it **rarer** at the other time (Mob Settings ▸ Enable Time
Based Spawning, Inactive Time Weight). They now read the real clock too. Use the Time rule for a hard "only".

## 5. Checklist

1. World scene: a **Weather System**, and on it **Player Weather Effects**. Set *Shelter Layers* to everything
   except the player layer.
2. City (or Bootstrap) scene: a **Day Night Cycle**.
3. Weather System ▸ Audio: the rain, wind, hail, sandstorm and tornado loops and a few thunder clips.
4. Players have a **Body Heat Manager** (Player Status Controller) for the temperature part.
5. Set **Time and Weather** on the mob types, portal types and item drops that need it.
6. Test: on the Weather System inspector in Play mode, **force** a weather (Rain, Blizzard...) and watch the status
   icons. Turn on *Log Changes* on Player Weather Effects. Set *Day Length Minutes* to 1 to see a whole day pass in a
   minute.

## 6. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| No statuses in the rain | No Player Weather Effects, rule off, or a roof above (the player's own collider on Shelter Layers) | Add it; remove the player layer from Shelter Layers; Log Changes |
| Statuses in the City | The City is not a separate realm (single-scene setup) | Expected without the Hub World's realms. Put the City under a roof, or turn off rules |
| Never night | No Day Night Cycle (only the old manager), or Control Light off and no Weather System lighting | Add a Day Night Cycle |
| Night mobs never spawn | Time rule Night Only, no clock in the server's scenes | The Day Night Cycle must be in a scene the server has loaded (Bootstrap or City) |
| Storm portal never appears | Weather rule Only In [Thunderstorm], but that biome rarely storms | Check the Weather System forecast and map; force a Thunderstorm to test |
| Silent weather | No audio clips on the Weather System | Assign the loops and thunder clips |
