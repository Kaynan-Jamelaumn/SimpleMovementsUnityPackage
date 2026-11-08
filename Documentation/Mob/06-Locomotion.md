# Mobs 06 — Locomotion: Flying, Swimming and Gaits

Not every creature walks. A mob's **Locomotion** (a section of its [Mob Profile](02-AI-Profiles-and-Behaviour.md#3-mobprofile))
says **where it lives** — on the ground, in the air, in water, or on land and in water — **how fast it goes in each**,
and **how it moves in each**: its *gait*. A slime bounces, a frog leaps, a snake slithers, a spider zigzags, a hawk
glides, a bat flutters, a small fish darts and a sea serpent undulates.

Everything else stays the same for every creature: the AI decides where to go (wander, chase, flee, flank, keep its
distance) and the NavMesh finds the way; locomotion only changes how the body travels that path, and where the mob may
go at all.

## Quick start

1. Open the mob's **Mob Profile**.
2. Press **Locomotion Preset ▾** at the top and pick the closest creature (table below).
3. Adjust the **Locomotion** section: speeds, heights, depths.
4. Optional: put **Mob Body Motion** on the mob's *model* (a child, never the mob itself) for squash and stretch and
   banking (§6).

| Preset | Habitat | On land | In water | In the air |
|---|---|---|---|---|
| Walker | Ground | Walk | Paddles out (if pushed in) | — |
| Hopper (rabbit) | Ground | Hop | Paddle | — |
| Slime | Ground | Bounce (squash and stretch) | Paddle | — |
| Frog | Amphibious | Leap | Swim (shallow) | — |
| Snake | Ground | Slither | Undulate | — |
| Crawler (worm, beetle) | Ground | Crawl (never runs) | Paddle | — |
| Skittering (spider, insect) | Ground | Zigzag | Paddle | — |
| Bird | Flying (lands to rest) | Hop | — | Flap, 5 m up |
| Bird Of Prey | Flying (lands to rest) | Walk | — | Glide, 12 m up, fast dives |
| Bat | Flying (never lands) | — | — | Flutter, 3 m up |
| Insect / wisp | Flying (never lands) | — | — | Hover, 2 m up |
| Fish | Aquatic | (suffocates) | Dart | — |
| Shark | Aquatic | (suffocates) | Swim, 1.5 m down, deep water only | — |
| Sea Serpent | Aquatic | (suffocates) | Undulate, 2 m down | — |
| Crocodile | Amphibious | Crawl (sprints a little) | Undulate, just under the surface | — |
| Turtle | Amphibious | Crawl, very slow | Paddle | — |

## 1. Habitats

| Habitat | Moves | Keeps out of | Out of its element |
|---|---|---|---|
| **Ground** | Walks (its land gait) | Water deeper than **Max Wade Depth** (0 = 80 % of its height): it wades through shallows, never walks along a lake bed | Pushed into deep water, it paddles at the surface (head out) and heads back to land |
| **Flying** | Flies at **Cruise Altitude** over land and water | Nothing | Lands to rest after **Rest After** seconds with nothing to do (if **Lands To Rest**), takes off again when it moves |
| **Aquatic** | Swims **Swim Depth** under the surface | Dry land and water shallower than **Min Swim Depth** | Washed up or knocked ashore it flops (a quarter of its speed), heads back to water, and after **Stranded Grace** seconds loses **Stranded Damage Per Second** health a second |
| **Amphibious** | Walks on land, swims in water | Nothing | — |

**What counts as water** is the same water the player swims in: the generated terrain's oceans, lakes, ponds and rivers,
and `SwimmableWater` volumes (pools, a flooded room). A mob swims where the water is at least *Min Swim Depth* deep
(a ground animal where it is deeper than it can wade).

**Where it may go.** A mob is never sent to a place its habitat keeps it out of: such points are never picked to wander,
patrol, flee, flank or dodge to. When its target stands there — a fish chasing a player on the shore, a wolf chasing a
player swimming in a lake — it goes as close as it may (the water's edge), the path counts as **incomplete**, and the AI
does what it does with any target it cannot reach: uses a ranged ability if it has one, otherwise gives up after a few
seconds.

## 2. Speeds

**Land Speed**, **Water Speed** and **Air Speed** multiply its walking and running speed (the profile's walk / run
multipliers of the Speed Manager's speed) in each medium: an otter swims faster than it runs (Water 1.4), a deer swims
slowly (Water 0.5), a hawk flies far faster than it walks (Air 1.6, Land 0.4). Crowd control (slows, roots) applies on
top, as before.

A **Crawl** gait never runs faster than **Crawl Run Limit** × its walking speed (a crocodile sprints a little: 1.8; a
turtle barely: 1.1).

## 3. Gaits

| Gait | Medium | What it does | Shape |
|---|---|---|---|
| **Walk** | land | An ordinary walk or run | — |
| **Hop** | land | Small quick hops: it moves only while in the air and pauses between hops | *Height*, *Length* of a hop, *Air Share* (time in the air) |
| **Bounce** | land | A slime: high springy bounces, stretched in the air, squashed between them | as Hop |
| **Leap** | land | Long high leaps with a crouch between them (frogs, kangaroos, jumping spiders) | as Hop |
| **Zigzag** | land | Sharp changes of side as it goes, at its own random moments (two mobs never zigzag in step) | *Width*, *Interval* between changes |
| **Crawl** | land | Low and steady; never breaks into a run (see Crawl Run Limit) | — |
| **Slither** | land | An S-shaped path that comes back to its line | *Width* (amplitude), *Length* (wavelength) |
| **Swim** | water | A steady swim with a gentle rise and fall | *Height* of the bob, *Interval* |
| **Undulate** | water | A sinuous wave (eels, sea serpents, crocodiles) | as Slither |
| **Dart** | water | A burst, then a glide (small fish); it averages its pace | *Interval* (one burst and glide) |
| **Paddle** | water | At the surface, head out (ducks, turtles, land animals) | *Height* of the bob |
| **Flap** | air | Steady flapping flight with a slight rise and fall | *Height*, *Interval* (a wingbeat) |
| **Hover** | air | Holds its place, bobbing (insects, wisps) | *Height*, *Interval* |
| **Glide** | air | Sinks slowly through a long glide, then climbs back with a few wingbeats | *Height* (the drop), *Interval* (one glide) |
| **Flutter** | air | Erratic, jinking flight (bats, moths) | *Height*, *Width*, *Interval* |

Each gait has a usual **shape** worked out from the mob's height. Tick **Custom Land / Water / Air Shape** to set it
yourself (the fields a gait does not use are ignored). Hops always land: a mob that stops mid-hop finishes its hop.

## 4. Flying

A flyer follows the NavMesh of the ground under it and holds its body **Cruise Altitude** above it — above the water's
surface over a lake. It climbs at **Climb Rate** and comes down at **Dive Rate**. Within **Descend Within** metres of the
target it is fighting it comes down to **Combat Altitude**, so its bites and claws reach (set it near the height of what
it hunts); it climbs back when the fight moves away.

Its NavMesh is the ground's: it flies wherever a walker could walk, over lakes (whose beds the terrain's NavMesh covers)
and over walls the NavMesh passes; it does not fly over a chasm the NavMesh does not cross.

## 5. Swimming

A swimmer follows the NavMesh of the lake or sea bed and holds the middle of its body **Swim Depth** under the surface,
its back never out of the water (Paddle: three quarters under, head out). Water shallower than that: it swims as deep as
it can, then lies on the bottom. The generated terrain's NavMesh covers its lake and sea beds; a hand-made lake needs its
bed in a NavMesh Surface.

## 6. How it looks

- **Animator parameters** (names in the profile's *Animation* section; missing ones are ignored): `IsFlying`,
  `IsSwimming` and `IsAirborne` (in the air mid-hop, bounce or leap), besides `Speed`, `SpeedPercent` and the others
  ([04 §1](04-Animation-Death-Spawning.md#1-animation)). `SpeedPercent` is the speed against its run **in the medium it is
  in**.
- **Height** comes from the NavMeshAgent's *Base Offset*: the altitude, the swim depth and a hop's arc move the whole mob
  (colliders and hitboxes too, so a flying bat is hit where it is seen).
- **Mob Body Motion** (*Add Component ▸ SimpleMovements ▸ Mobs ▸ Mob Body Motion*), on the mob's **model**: squash and
  stretch with its vertical motion (a slime's bounce, a frog's landing), and while flying or swimming it banks into its
  turns and pitches its nose up as it climbs and down as it dives. Presentation only.
- **Rotate To Ground Normal** keeps the body level while it flies or swims (the ground far below is not what it stands on).

## 7. Death

A flyer killed in the air **falls** to the ground, a swimmer killed in water **floats up** to the surface, and a hopper
killed mid-hop drops — so its carcass can be reached and harvested.

## 8. Spawning

The open-world spawner (`EndlessTerrain ▸ Mob Settings`) places each mob by its habitat: **aquatic** mobs only in water
at least *Min Swim Depth* deep (not held to their biome's land height band — a lake bed lies below it), **amphibious** and
**flying** mobs on land or over water, **ground** mobs on land as before. A chunk's population counts its water for the
mobs that live in water. Dungeon and hand-placed spawns are where you put them: put aquatic mobs in water.

## 9. Multiplayer

Nothing to set up. The server runs the AI and the locomotion; the height (altitude, depth, hops) travels with the
position through the NetworkTransform, and the medium (flying, swimming) with the mob's replicated state, so clients pick
the right animations. A hop is seen, not sent: clients play `IsAirborne` from the movement they see.

## 10. Scripting

| Member | |
|---|---|
| `MobProfile.locomotion` (`MobLocomotion`) | The settings; `MobLocomotion.Create(Preset)` / `Apply(Preset)` |
| `MobMotor.Medium`, `IsFlying`, `IsSwimming`, `IsAirborne`, `IsLanded`, `IsStranded`, `Lift`, `Squash` | What it is doing now (server / single player) |
| `MobMotor.AllowsPoint(point)` | May its habitat take it there? |
| `MobMotor.PaceIn(mode)`, `RunPace` | Its pace in the medium it is in |
| `MobMovementStateMachine.IsOffTheGround`, `IsFlying` | Flying or swimming, on any machine (client copies too) |
| `MobGait` | The gait rules as plain arithmetic (`Land`, `Water`, `Air`, `SwimOffset`, `FlyOffset`) — tested in `MobLocomotionTests` |

A **new gait** is a new value of `MobLandGait`, `MobWaterGait` or `MobAirGait` and its case in `MobGait`: the motor
applies any gait's *speed scale* (its rhythm), *lift* (height) and *lateral* drift.
