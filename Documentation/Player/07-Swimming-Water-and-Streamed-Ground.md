# Player 07: Swimming, Water and Streamed Ground

**Scripts:** `Player/Model/SwimSettings.cs`, `Player/Controller/MovementStateMachine/States/SwimmingState.cs`,
`MovementStateMachine.cs`, `MovementContext.cs`; `Procedural/World/Queries/WaterQuery.cs`, `StreamedGround.cs`,
`LoadedTerrain.cs`; `Procedural/World/Water/Submersion.cs`, `SwimmableWater.cs`, `WaterVolume.cs`.

---

## 1. Swimming (Subnautica-style)

Swimming is a movement state (`Swimming`, appended after `Rolling`, so saved and network values do not change). It is
part of the deterministic movement step, so it is predicted and corrected in multiplayer like the other states. The
camera's pitch travels with the input (`MovementInput.CameraPitch`) and the swim momentum is part of the snapshot
(`SwimVelocity`), so the server and every client swim exactly alike.

**When the player swims:** the water at the player must be at least **Min Water Depth** deep (surface to bottom), and
the feet must be at least **Enter Depth** under the surface. Shallower water is waded through. The player stops
swimming when the water gets shallower than Min Water Depth − 0.1, or when the feet come above **Exit Depth**. In deep
water you never walk on the floor: you swim, even when you touch it (as in Subnautica).

**Controls in water (defaults):**

| Input | What it does |
|---|---|
| Forward / back | Swim **where the camera looks**: look down and press forward to dive, look up to rise |
| Left / right | Strafe (level) |
| Jump held | Swim straight up (no higher than floating at the surface, head out) |
| Crouch held | Swim straight down |
| Sprint held while moving | Swim faster (costs more stamina; set Fast Swim Multiplier to 1 to remove it) |
| Nothing held | You **stay at your depth** and glide to a stop (Buoyancy: Neutral) |
| Jump pressed at the surface while moving | Hop out of the water (onto a bank) |

The swimmer has momentum: it speeds up at **Acceleration** and slows down with **Water Drag** when nothing is held.
Looking up at the surface keeps you swimming along it instead of flying out. Gravity is off while swimming. Dashing
and rolling are not available in water (a dash or roll started on land finishes first).

### Settings (Player Movement Model ▸ Swimming)

| Setting | Default | Meaning |
|---|---|---|
| Enabled | on | Off = the old behaviour (deep water is walked through like air) |
| Min Water Depth | 1.4 | Water shallower than this is waded through |
| Enter Depth / Exit Depth | 1.1 / 0.75 | Feet depth to start / stop swimming |
| Float Depth | 1.35 | How far the feet hang under the surface when floating (head out) |
| Eye Height | 0 | Eyes above the feet, for "head underwater". 0 = from the CharacterController |
| Swim Where Camera Looks | on | Forward follows the camera's pitch. Off = forward stays level; only Jump/Crouch change depth |
| Look Pitch Dead Zone | 5° | A third-person camera looking slightly down does not make you sink while swimming forward. 0 for first person |
| Speed Multiplier | 0.85 | Swimming speed = walking speed × this |
| Fast Swim Multiplier | 1.4 | With Sprint |
| Vertical Speed | 3 | Up/down speed with Jump/Crouch |
| Acceleration / Water Drag | 7 / 3.5 | How fast you speed up / glide to a stop (lower drag = longer glide) |
| Buoyancy | Neutral | Neutral (stay at depth, Subnautica) / Floats (rise back to the surface) / Sinks |
| Rise Speed / Sink Speed | 1.2 / 1 | For Floats / Sinks |
| Stamina Cost | 1 | Stamina spent every 0.8 s while swimming |
| Fast Swim Stamina Cost | 3 | While swimming fast |
| Treading Stamina Cost | 0 | While not moving in the water |
| Exhausted Speed Multiplier | 0.5 | Out of stamina: slower, no fast swimming, cannot swim up |
| Sink When Exhausted | off | Out of stamina the player sinks |
| Jump Out At Surface | on | |

Stamina is only spent when the Movement Model's **Should Consume Stamina** is on, as for sprinting. Oxygen is used
while the head is underwater and refills at the surface (OxygenManager, see §2).

### Animation

Run **Tools ▸ SimpleMovements ▸ Player ▸ Complete Player Animator Controller** with the player selected: it adds every
parameter below (and a **Swim** state entered from Any State while IsSwimming). Drag a swim blend tree onto it.

| Parameter | Type | Meaning |
|---|---|---|
| `IsSwimming` | Bool | In the Swimming state |
| `IsUnderwater` | Bool | Swimming with the head under the surface |
| `SwimSpeed` | Float | Swim speed in m/s (0 = treading water) |
| `SwimPitch` | Float | Swim angle in degrees (+ rising, − diving): blend tread / forward / up / down clips |
| `SwimVertical` | Float | Up/down speed in m/s |

### Sound

**Tools ▸ SimpleMovements ▸ Player ▸ Add Movement Audio** adds a **Player Movement Audio**. In the water it plays a
splash on entering (louder when falling fast), swim strokes, a gasp when surfacing after a long dive, a loop while
underwater, and muffles the whole world for your own player while the head is under (a low-pass filter on the Audio
Listener, like Subnautica).

### Which water can be swum in

* **Terrain water** (oceans, lakes, ponds, rivers of the generated world). It is read per cell from the loaded chunks,
  so it needs no setup.
* **Hand-placed water.** Add a **Swimmable Water** component to an object with a Box Collider covering the water: the
  top of the box is the surface, its bottom the floor. Use it for a pool in the city or a flooded dungeon room.

## 2. Oxygen (the "drains on land" bug)

Each terrain chunk has one water trigger box that covers the whole chunk, from its deepest water bed to its highest
water surface. Before this fix, entering that box switched "underwater" on by itself, so standing on dry land in a
chunk with a lake drained oxygen.

Now the triggers only mean "near water". `Submersion` checks every frame whether the character's **head** is under the
actual water surface at its position (`WaterQuery`), and only then calls `OxygenManager.SetUnderwater(true)`. The head
is the top of the CharacterController (or the NavMeshAgent / colliders for mobs) minus 0.2 m. Nothing needs to be set
up again.

### Suffocation (out of breath)

When the oxygen bar is empty the player loses health. Configure it on the **Oxygen Manager ▸ Suffocation**:

| Setting | Default | Meaning |
|---|---|---|
| Oxygen Depleted Health Drain Rate | 10 | Health lost per hit (or a percentage, below). 0 = no damage |
| Suffocation Interval | 1 s | Seconds between two hits |
| Suffocation Grace Seconds | 0 | Seconds at zero oxygen before the first hit: a last chance to reach the surface |
| Suffocation Percent Of Max Health | off | The damage is a percentage of max health (10 = 10% per hit), so it hurts strong and weak characters alike |
| Suffocation Damage Increase | 0 | Added to every next hit while still out of breath (it gets worse the longer it lasts) |
| Suffocation Max Damage | 0 | Cap for a growing hit (0 = no cap) |

The oxygen drain itself (*Underwater Oxygen Drain Rate*, per tick) and the refill at the surface (the bar's
*Increment Value*) are in the same component. From code: `OxygenManager.SetSuffocation(damage, interval, percent)`. In
multiplayer the server deals the damage and the clients see the health bar.

### Holding Jump in the water

Holding **Jump** while swimming takes you **straight up** at *Vertical Speed* (3 m/s) until you float at the surface
with your head out. You never fly out of the water by holding it. **Pressing** Jump at the surface while moving hops
out onto a bank. Holding **Crouch** dives straight down. Out of stamina, you can't swim up (with *Sink When
Exhausted* on, you sink).

## 3. Long dashes and chunks that are not loaded yet

The world is generated around the players a little ahead of them. A chunk's collider is cooked on a worker thread and
arrives a frame or more after the chunk's data. The default dash covers about 350 m in one second, which is more than
the distance at which chunks are created, so it could end over ground with no collider and fall through the world.

**Fix:** after every movement step on the streamed terrain, the movement checks that the ground under the player is
ready: chunk loaded, collider assigned, chunk shown (`LoadedTerrain.IsGroundReady`).

* If the step ended over ground that is not ready, the player is put back where the step started, at the edge of the
  ready ground. The dash stops there and the player can move on once the chunk is ready (usually within a few frames).
* If the player is put somewhere not loaded yet (a respawn or a travel), they wait in place without falling until the
  ground under them is ready.
* **Streamed Ground Timeout** (8 s by default) is a safety net: after it, the player is let go and a warning is logged.

Settings on the **Movement State Machine**: *Wait For Streamed Ground* (on) and *Streamed Ground Timeout*.

In multiplayer, the check runs on the server and on the owner's machine (prediction). Other clients load only the
ground around their own player, so they never hold anyone. It does nothing inside dungeons and in scenes without an
`EndlessTerrain`.
