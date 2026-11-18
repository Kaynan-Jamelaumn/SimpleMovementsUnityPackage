# Mobs 07 — Senses & Noise

Characters make noise; mobs and players hear it. A mob notices its enemies with three senses - **sight** (a shape in
front of or around it), **touch** (anyone very close) and **hearing** - and each can be tuned or switched off per mob
type, so one mob is blind and hunts by ear while another is deaf and has sharp eyes.

## 1. What makes noise

Every noise has a **radius**: how far it is heard in the open by a listener with normal hearing. The built-in ones are
in *Combat Settings ▸ Noise* (Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Combat Settings):

| Action | Radius | Notes |
|---|---|---|
| Standing still | — | silent |
| Sneaking (crouched, moving) | 2.5 m | |
| Walking | 7 m | |
| Running | 14 m | |
| Swimming | 8 m | |
| Jump (push-off) | 8 m | |
| Landing | 5 – 18 m | by how fast the character was falling (4 – 16 m/s); a short drop is silent |
| Dash / roll | 11 m / 8 m | |
| Weapon swing | 10 m | |
| Shot (bow, crossbow, firearm) | 18 m | |
| A blow landing | 10 m | heard around the victim, blamed on the attacker |
| Death cry | 12 m | allies of the dead come and look |
| Ability | its own | *Ability Definition ▸ Presentation ▸ Noise Radius* |
| Explosion | 4 × blast radius | |
| Using a dungeon object | 5 m | chest 7 m, lever 9 m, herbs 2 m, a smashed barrel 14 m |
| A mob's own steps | ~4 – 11 m | × the profile's *Movement Noise* |

*Loudness* at the top of the section scales everything. The **Noise Reduction** stat (traits, gear, buffs) makes a
character's own body quieter - steps, jumps, landings, dodges, swings - and a negative value makes it louder (heavy
armour). Abilities, explosions and blows are not dampened by it.

### How sound carries

- A listener hears a noise within `radius × its hearing`.
- **Walls muffle**: when something on the Obstacle Layers stands between the noise and the listener's ears, the noise
  carries only *Wall Muffling* (0.6) of that distance. Closer than *Muffle Free Distance* (2.5 m) a wall changes
  nothing.
- One ray is cast per listener, and only for noises that would be heard in the open.

### Noise from your own code

```csharp
// A character makes a noise (mobs that are its enemies become aware of it):
entity.MakeNoise(NoiseKind.Voice, 20f, 0.8f);                    // a shout

// A noise nobody is blamed for (mobs come and look):
CombatEvents.EmitNoise(position, 15f, null, 0.7f, NoiseKind.Object);
```

For things in a scene use the **Noise Emitter** component (a bell, an alarm, a lure): call `Emit()` from a UnityEvent,
let it repeat, or set *Impact Speed* so a thrown object sounds where it lands - a bottle thrown to pull a guard away.

## 2. What a mob sees: Vision Shape

*Mob Profile ▸ Perception ▸ Vision Shape*. Walls block every shape; a sneaking player shortens the range
(*Sneak Detection Multiplier*).

| Shape | Sees | Use |
|---|---|---|
| **Cone** | *Sight Range* far, *Field Of View* wide, in front | the usual |
| **Sphere** | everything within *Sight Range*, whichever way it faces | bosses, creatures with eyes all round |
| **Box** | a corridor straight ahead: *Sight Range* long, *Vision Width* wide | a sentry watching a passage, tunnel vision |
| **None** | nothing: **blind** | hunters by ear |

Whatever the shape, **Close Sense Radius** notices anyone that close all around - touch, breath, the ground shaking.
It is all a blind mob has besides its ears, and a mob only attacks what it sees or feels.

Select a mob in the Scene view to see its senses: **yellow** = what it sees, **orange** = what it feels, **blue** = how
far it hears a walking (inner circle) and a running (outer circle) player.

## 3. What a mob hears

| Field | Meaning |
|---|---|
| **Hearing Multiplier** | multiplies every noise's radius for this mob. 0 = **deaf**; 1 = normal; 2 – 3 = sharp ears |
| **Hearing Alertness** | how much a heard noise raises its suspicion of whoever made it. 0 = it only goes to look; 1 = a second or two of footsteps, or one swing nearby, and it knows; 3+ = one loud noise is enough |
| **Search Radius** | while searching it walks to a few spots around the place instead of only turning. Blind hunters prowl like this |
| **Movement Noise** | how loud the mob itself is (0 = silent) |

The **Hearing** stat adds to the multiplier (percent).

A noise made by an enemy does two things: the mob learns **where** the enemy is (the place of the noise), and its
**awareness** of that enemy rises. Below full awareness it goes to look (walking for a quiet noise, running for an
alarming one). At full awareness it has noticed the enemy exactly as if it had seen it - **whether or not the enemy is
inside its vision shape** - and hunts it at the place it last heard it. Each new noise moves that place; when the
noises stop, the mob arrives where the last one was, waits (*Memory Duration*), searches, and gives up.

A noise nobody is blamed for, an ally's fight or an ally's death cry only makes it come and look.

A **deaf** mob should also have *Respond To Help Radius* 0 (it cannot hear its allies call).

Sleeping dungeon mobs (barracks) wake to noise too: a sleeper hears half as far as it would awake (*Sleep Hearing*).

## 4. The examples

*Tools ▸ SimpleMovements ▸ Mobs ▸ Create Example Mob* puts a ready mob in the scene (placeholder body, every
component wired, its profile created from the preset). The same behaviours are in every preset menu
(*Apply Preset ▾*, *Mob Profile From Preset…*).

| Example | Senses | How it plays | How to beat it |
|---|---|---|---|
| **Blind Hunter** | Vision None, touch 2.5 m, hearing ×2.5, alertness 3, search 6 m | hears a run 35 m away, a walk 17 m, a sneak 6 m; runs to the noise, prowls where it stopped, attacks what it touches. Never dodges (it sees neither swings nor telegraphs) | stand still when it is close; sneak when it is far; throw something to send it elsewhere |
| **Deaf Watcher** | Cone 28 m × 100°, touch 1.5 m, hearing 0 | run, fight and smash things behind its back; in its cone, sneaking barely helps. Turns slowly, does not answer calls for help | stay out of the cone; go round behind it |
| **Sentry** | Box 30 m × 5 m, hearing ×1 but alertness 0.5 | holds its post and its facing (*Hold Facing*), watches one corridor; a noise makes it turn and look | walk round the corridor it watches, or lure it off with a noise |

## 5. What players hear

Players hear the same noises by the same rules. The local player's screen shows an **arc** around the middle of the
screen in the direction of each noise, wider and brighter the louder it arrives, for a moment after it:
other players (pale), mobs (orange), things (blue). So a player can tell that someone is running behind a wall or that
a blind hunter is prowling nearby - and stays unheard by standing still or crouching.

- Not shown: the player's own noises, its party and summons (*Hear Allies*), and anything in plain sight on screen
  (*Hide Seen Sources*).
- *Combat Settings ▸ Noise ▸ What players hear*: on/off, the players' hearing multiplier, the colours.
- The **Player Hearing** component is added to players automatically; add it yourself to change its ring size, how
  long arcs stay, or the player's own hearing. Its `Heard` event (and the static `AnyHeard`) gives every noise that
  reached the player - direction, distance, loudness - for sounds, subtitles or your own UI.
- Network games: mob AI hears on the server; each client makes the other characters' movement noise from what it sees
  of them, for its own player's arcs.
