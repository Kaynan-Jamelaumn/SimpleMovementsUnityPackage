# Player 08: Character size, Animator controller, movement audio, interaction

**Scripts:** `Essentials/Combat/CharacterBody.cs`, `Player/RPGSystem/Archetypes/CharacterIdentity.cs`,
`Player/Animation/CharacterStateAnimationHandler.cs`, `CustomEditor/Player/PlayerAnimatorSetup.cs`,
`Player/Audio/PlayerMovementAudio.cs`, `HubWorld/Runtime/RealmAudio.cs`, `Networking/Runtime/Economy/NetInteractor.cs`.

---

## 1. Character size: Character Identity vs Character Body

The two components work as a pair:

| | Character Identity | Character Body |
|---|---|---|
| What it is | **Who** the character is: race, class, archetypes, level, and the **chosen** height | **How big** the character is physically: the engine that scales the model and the colliders |
| Its height field | The height you pick (character creation). 0 = the race's default. **Clamped to the race's Height Range** | The base height before buffs. **Overwritten** by the Identity's height when the game starts |
| What it changes | Nothing visible by itself: it passes its height to the Character Body (`SetHeight`) | The model's scale, the **CharacterController** (height, radius, centre, step offset; feet stay on the ground), NavMeshAgent / capsule |

The final size, applied **when the game runs** (Edit mode never changes the prefab):

```
scale = height / Model Height × Height stat (races, traits, items, buffs)
scale = clamp(scale, Scale Limits.x, Scale Limits.y)
final height = Model Height × scale
```

* **Model Height**: how tall your model is at scale 1 (measure it once; 1.8 for a normal humanoid). If you raise
  Model Height and Height by the same amount, the scale stays 1 and nothing changes.
* **Scale Limits**: the smallest and largest scale allowed (0.5-2 = half to double the model's size). It protects
  against a giant or tiny character from a typo or stacked buffs. The inspector says when the limits cut your height.
* **Model**: the child that holds the meshes (for your player: **PlayerShellObject**). Empty = found automatically: the
  Movement Model's Player Shell Object, else the child holding an Animator or the skinned meshes. Never the root (the
  root is not scaled, so cameras and rigs keep their size).
* **CharacterController**: found on the same object or on the character's root. Its height, radius, centre and step
  offset are resized, keeping the bottom of the capsule where it was.

**Why nothing seemed to change:**

1. The size is applied in Play mode only. The Character Body inspector now shows, in Edit mode, the scale and the final
   height it will give, which model and which controller it will resize.
2. The Model field was empty, so only the controller was resized. It is now found automatically.
3. The Identity overwrites the Body's height, and clamps it to the race's Height Range.
4. While playing, changing Height on either component now updates the character at once.

Turn on **Log Changes** on the Character Body to see what was resized in the Console.

## 2. Animator controller

**Tools ▸ SimpleMovements ▸ Player ▸ Check Player Animator Controller** reports what your controller is missing.
**Complete Player Animator Controller** adds it. Select the player prefab (or the controller asset) first. Nothing that
exists is changed or removed.

It adds every parameter the game sets (missing ones only) and reports parameters with a wrong type:

| Group | Parameters |
|---|---|
| Locomotion | MovementX, MovementZ, MovementSpeed (Float); IsWalking, IsRunning, IsCrouching, IsJumping, IsDashing, IsRolling (Bool) |
| Ground and air | IsGrounded, IsFalling (Bool); AirTime (Float); JumpTrigger, LandTrigger (Trigger) |
| Swimming | IsSwimming, IsUnderwater (Bool); SwimSpeed, SwimPitch, SwimVertical (Float) |
| Actions | DashTrigger, RollTrigger (Trigger); IsInputLocked (Bool) |
| Attacks | IsAttacking (Bool); AttackCombo (Int); AttackTrigger, LightAttackTrigger, HeavyAttackTrigger, ChargedAttackTrigger, SpecialAttackTrigger, AlternateAttackTrigger, OffHandAttackTrigger (Trigger) |
| Blocking | IsBlocking (Bool); BlockHitTrigger, ParryTrigger, GuardBreakTrigger, ShieldBashTrigger (Trigger) |
| State | HitTrigger, DeathTrigger (Trigger); IsStunned, IsDead (Bool) |

It also adds three empty states when the controller has none; drag your clips onto them:

* **Swim**: Any State → Swim while IsSwimming, back to the default state when it turns off.
* **Fall**: Any State → Fall when IsFalling and AirTime > 0.3, back when IsGrounded.
* **Death**: Any State → Death on DeathTrigger, back when IsDead turns off (respawn).

The ground, air, hit, stun and death parameters were listed before but **nothing set them**. The new
`CharacterStateAnimationHandler` (created by `PlayerAnimationController`) drives them on every machine that shows the
character.

**Footsteps in time with the animation:** add Animation Events named **OnFootstep** to your walk and run clips (on
the frames where a foot touches the ground), and **OnLanding** to landing clips.

## 3. Movement audio and realm music

**Player Movement Audio** (Tools ▸ SimpleMovements ▸ Player ▸ Add Movement Audio): drag clips into its lists. Empty
lists are silent.

* **Footsteps:** walk, run (or the walk clips a little louder) and crouch (quiet). They follow the OnFootstep events
  when your clips have them, otherwise a timer.
* **Actions:** jump, landing (louder after a long fall), dash, roll.
* **Water:** splash, climbing out, swim strokes, a gasp when surfacing after a long dive, an underwater loop, and the
  world muffled underwater for your own player.
* **Output:** set it to your SFX mixer group.

In multiplayer, every machine plays the movement sounds of every player it shows, in 3D from that player. Replayed
steps play nothing. A dedicated server is silent.

**Realm Audio** (Tools ▸ SimpleMovements ▸ Scene ▸ Create Realm Music, in the City scene) plays music and ambience
for where your own player is:
* the City;
* the World, optionally one piece of music per zone ring (calm in the Safe Zone, tense further out);
* dungeons.

It cross-fades through the **SoundManager**, and creates one if the scene has none.

## 4. Interaction: Player Interactor and Net Interactor

The **Player Interactor** finds doors, chests, NPCs, merchants and items and uses them with the Interact key or a
click. The **Net Interactor** only relays it to the server in a network session. Its *Interactor* field was empty
because the player had no Player Interactor.

Now:
* **Make Selected Prefab Networked ▸ Player** adds a Player Interactor to the player root (when missing) and fills the
  field.
* The field fills itself in the inspector when the player has one.
* If a player still has none when the game starts, one is added (with a warning), so interaction always works.
