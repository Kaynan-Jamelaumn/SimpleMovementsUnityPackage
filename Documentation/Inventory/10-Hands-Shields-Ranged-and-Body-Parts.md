# 10 — Hands, Shields, Ranged Weapons, Body Parts, Visuals & Quickslots

This chapter covers the combat features added on top of the weapon system of [05](05-Weapons-and-Attacks.md):

| Section | What it covers |
|---|---|
| [1. Hands](#1-hands-one-handed-two-handed-dual-wield) | One-handed, two-handed and dual-wielded weapons, the Off Hand slot |
| [2. Body parts](#2-body-part-damage) | Hit location, damage multipliers, armour per part, stagger / slow / sever effects |
| [3. Shields and guards](#3-shields-and-weapon-guards) | Blocking by angle, stamina, guard break, parry, bash, AI blocking |
| [4. Ranged weapons](#4-ranged-weapons) | Bows (draw), crossbows and firearms (magazine), throwing, ammo, projectiles |
| [5. Equipment visuals](#5-equipment-visuals) | Weapons in hand / sheathed, shields on the forearm, armour on the body |
| [6. Quickslots](#6-consumable-quickslots) | Potions, food and throwables on keys or the D-pad |
| [7. The damage pipeline](#7-the-damage-pipeline) | In which order body parts, blocking and armour act |
| [8. Controls](#8-controls) | Every input, its action names and fallback |
| [9. Limitations](#9-limitations) | What is not covered |

Everything is **data on the item assets**; no weapon has code of its own. The player gets the runtime components
automatically (Inventory Manager ▸ *Hands, Visuals & Quickslots*); mobs share the same components.

---

## 1. Hands: one-handed, two-handed, dual wield

| Hand | What it holds |
|---|---|
| **Main hand** | The item of the selected hotbar slot (unchanged). |
| **Off hand** | The item of the **Off Hand** equipment slot (the old *Shield* slot, `SlotType.Shield`): a **shield**, or a **one-handed weapon that can be dual wielded**. |

**Weapon ▸ Hands & Guard ▸ Handling** (`WeaponHandling`):

| Field | Meaning |
|---|---|
| **Grip** | `Auto` (from the category), `OneHanded`, `TwoHanded`. Auto = two-handed for Greatsword, Hammer, Spear, Bow, Crossbow, Staff. |
| **Off Hand** | Can it go in the off hand? `Auto` = Sword, Dagger, Axe, Mace, Fist, Wand, Tool, Thrown, Shield. Two-handed weapons never can. |
| **Off Hand Attack** | Which attack of *this* weapon the off-hand input performs (its own chain, charge, on-hit effects, behaviours). |
| **Off Hand Damage / Stamina Multiplier** | Weaker or cheaper attacks while it is in the off hand. |

Rules (one place: `HandRules`):

* A **two-handed main weapon stows the off-hand item**: its defense, blocking and passive effects are switched off
  (`EquipmentManager.SetSuppressed`), a message says so, and the visuals put it on its sheath point. The item
  **stays in its slot** — nothing is moved, lost or duplicated — and comes back as soon as the two-handed weapon
  leaves the hand.
* Dropping a two-handed weapon *into* the off hand is refused with the reason (`SlotTypeHelper.WhyCannotPlace`).
* **Dual wielding**: main weapon + an off-hand weapon. Each hand has its own `ComboSystem` and chain
  (`WeaponHand`), its own damage, status effects and abilities. The off-hand input (or Alternate while dual
  wielding) attacks with the off-hand weapon; its hits come from the off hand's socket.
* **Left–right combos**: a combo sequence of the main weapon may contain `AttackType.OffHand`
  (e.g. Normal, OffHand, Normal → finisher).

**Setup:** give the Weapon Controller an **Off Hand Game Object** (the left hand bone). On a Humanoid it is found
automatically; the validator warns otherwise.

---

## 2. Body-part damage

A **Body Part Profile** (`Assets ▸ Create ▸ SimpleMovements ▸ Combat ▸ Body Part Profile`, or the built-in
humanoid) lists the parts of a character:

| Part (built-in humanoid) | Height | × damage | Protected by | Effect |
|---|---|---|---|---|
| Head | 83–100 % | 1.6 | Helmet | 35 % stagger |
| Torso | 45–83 % | 1.0 | Chestplate, Shoulders, Cloak, Belt | — |
| Arms (either side) | 45–86 %, outer side | 0.8 | Gloves, Bracers, Shoulders, Shield | — |
| Legs | 0–45 % | 0.85 | Leggings, Boots | 30 % slow |

Each part (`BodyPartDefinition`) has: name and aliases, a height band (0 = feet, 1 = top), a side, a damage
multiplier, the armour slots that protect it, extra natural defense / magic resistance, and **effects** picked from
a dropdown: *Stagger*, *Impair Movement* (slow / root), *Ability Effects* (any ability effect) and *Sever*
(dismemberment: collapses bones, hides renderers, spawns a severed piece, lasting slow, can disable weapon
attacks). Add your own by deriving from `BodyPartEffect`.

**Where an attack lands** (`BodyPartController`):

1. the attack's **Aimed Body Part** (Attack ▸ Body Part & Blocking), e.g. *Legs* for a sweep;
2. the **collider hit**, when it carries a `BodyPartHitbox` (exact hit boxes on bones);
3. area and periodic damage → the profile's default part;
4. otherwise the hit point's height and side on the character.

**One hit, one part.** An attack damages a character at most once per swing (or once per *Rehit Interval*), and
that hit counts for exactly one part, so damage is never doubled when a blade crosses the head and the torso:

* Weapon blades and projectiles use the **first contact**: the point where the weapon first touches the body.
* With hitboxes, when that point is inside several of them, the profile's **When Several Parts** decides:
  *Nearest To Contact* (default), *Most Vulnerable* (rewards aiming at weak spots) or *Least Vulnerable*.
* Damage over time and area damage use the *Default Part*.

Splitting one hit's damage between parts was left out on purpose: it makes every hit weaker and the numbers hard
to read. If you want big weapons to "hit everything", give them a higher base damage instead.

**Hits on the player and mob attacks.** Mob abilities (claws, slams, swings) land at the attacker's strike height
(*Combat Settings ▸ Ability Strike Height*, 65 % of the attacker's height, with *Ability Strike Spread* variation)
and on the side facing the attacker. A wolf bites legs, a man hits the torso and sometimes the head or arms, an
ogre hits heads. Projectiles land where they touch.

**Armour per part.** Only the armour that covers the part counts fully; other worn armour counts at
*Other Armor Share* (25 % by default). A helmet protects headshots and barely the legs.

**Who has body parts:** characters with a `BodyPartController` (add it, or turn on
*Combat Settings ▸ Body Parts For Every Character*). Without one, damage works exactly as before.

### Configuring body parts

1. Add **Body Part Controller** to the character (the object with its `CombatEntity`).
2. Its inspector draws the parts it uses as a body (centre parts in the middle, side parts on both sides, each with its
   colour and damage multiplier) and lists what protects each part and what it does when hit. With no profile
   assigned it uses *Combat Settings ▸ Default Body Part Profile*, else the built-in humanoid.
3. **Create Editable Profile** saves a copy as an asset and assigns it. In the profile you set, per part:
   * **Height Range** (0 = feet, 1 = top of the character's height) and **Side** (Any, Left, Right, Either Side with
     **Side Offset** = how far out from the centre, as a fraction of the body radius);
   * **Damage Multiplier**, **Protected By** (armour slots), **Bonus Defense / Magic Resistance**;
   * **Effects** (Stagger, Impair Movement, Ability Effects, Sever), each with a chance and a minimum damage;
   * the profile's **Default Part** (area damage), **Other Armor Share** and **Global Damage Scale**.
   *Reset To Humanoid* in the profile restores the defaults. Give non-humanoids (a wolf, a spider) their own profile.
4. Optional, for exact hits that follow the animation: **Add Hitboxes (Humanoid)** puts a sphere on the head, a
   capsule on the torso and capsules on the upper and lower arms and legs (triggers named `Hitbox_…`, on the
   character's layer). Adjust their radius on each object; **Remove Hitboxes** deletes them. On other rigs, add a
   collider plus a **Body Part Hitbox** (Part = a part's name) to each bone.

### Seeing them

The character needs a **Body Part Controller component in the scene or prefab**. *Body Parts For Every Character*
only adds one when the game starts, so there is nothing to draw while editing; add the component to see the parts.
The Scene view's **Gizmos** button must be on.

* **Scene view, the character (or any of its children) selected:** each part's height band as a coloured ring
  (side parts as boxes on the outer sides) with a label such as "Arms ×0.8", and every hitbox in its part's
  colour. *Always Show Parts* draws them without selecting.
* **Scene view, while playing:** every hit is marked where it landed with the part and the damage (fades after
  *Hit Marker Time*). Turn it off with *Show In Scene View*.
* **Game view, while playing in the Editor:** a label rises from each hit ("Head −14"), so you see which part was
  struck while you fight. *Show Hits In Game View*; never shown in builds.
* **Inspector, while playing:** hits and damage per part, severed parts, the last hit, and a **Hit** button per part
  (test damage aimed at that part, unblockable) to check multipliers, armour and effects. *Reset Parts* heals them.

---

## 3. Shields and weapon guards

**Shield = armour in the Shield / Off Hand slot** with a **Shield Defense** (Armor ▸ Shield; Presets ▸ Shield
Defense: Buckler, Round, Kite, Tower). **Weapons can guard** too (Weapon ▸ Hands & Guard ▸ Guard, off by default;
the Greatsword and Staff templates turn it on).

| Setting | Meaning |
|---|---|
| Coverage Angle | Arc in front of the defender that is protected (Buckler 90°, Round 120°, Kite 140°, Tower 170°). |
| Covered Body Parts | Empty = all; a buckler leaves the legs open. |
| Physical / Magical / Elemental Reduction | Fraction removed. **True damage is never blocked.** |
| Blocks Projectiles / Area Damage | Arrows, explosions. |
| Stamina Per Damage / Per Block / Per Second | Cost of holding and of each blocked hit (× the attack's **Guard Damage**). |
| Guard Break | When stamina runs out: the rest of the hit passes, the defender is staggered, the guard is down for a while. |
| Parry Window | Raising the guard just before a hit negates it and staggers the attacker (projectiles optional). |
| Raise Time, Move Speed, Knockback While Blocking | How heavy it feels. |
| Durability Per Block | Wear on the shield (or the guarding weapon). |
| Bash | Attacking while blocking: damage, stagger, knockback, stamina, cooldown. |

`BlockController` (added to the player automatically; add it to mobs that should block):

* **Source:** the off-hand shield → the main weapon's guard → the off-hand weapon's guard. Without a Weapon
  Controller (simple mobs) it uses its own *Shield Item* or *Innate Defense*.
* **Blocks** when the hit comes from inside the coverage arc and the body part is covered. Attacks marked
  **Unblockable** pass.
* **AI:** reacts to a melee swing starting (*AI Block Chance*, *Reaction Time*, *Hold Time*), sometimes guards when
  an enemy is close, lowers its guard to attack.
* **Animator:** bool `IsBlocking`, triggers `BlockHitTrigger`, `ParryTrigger`, `GuardBreakTrigger`, `ShieldBashTrigger`
  (all renameable; missing parameters are ignored).
* A two-handed weapon stows the shield, so a greatsword user blocks with the greatsword's guard (if it has one).

---

## 4. Ranged weapons

**Weapon ▸ Ranged Weapon ▸ Ranged** picks a mechanic. The attacks keep their timing, animation, stamina, on-hit
effects and behaviours; those listed in **Firing Attacks** fire a projectile when they strike (start of Active).
The others stay melee (a bow bash on Alternate).

| Mechanic | For | How it works |
|---|---|---|
| **Bow (draw and release)** | Bows, slings, blowguns, magic wands that "charge" | **Style** decides taps and holds (below). Strength follows *Strength Curve* over *Draw Time*: damage, speed (a faster arrow also flies farther) and accuracy grow from the weakest to the full draw. Holding a full draw past *Steady Hold Time* makes the aim shake. Stamina per shot and while drawing; slower movement while drawing. |
| **Magazine (crossbow, firearm)** | Crossbows, pistols, rifles, shotguns | *Magazine Size* loaded rounds (kept on the item stack), *Reload Time* (R, or automatically when empty), *Fire Interval*, *Automatic*, *Projectiles Per Shot* (pellets), *Spread*, *Bloom* when firing in a row. |
| **Throw** | Knives, javelins, axes, grenades | Hold to throw harder (optional), lobbed by *Loft*. *Consumes Item* throws one unit of the weapon's stack (raise Stack Max). Thrown items can be picked up again. |

**Holding the Use input with a bow.** Yes: pressing Use starts the attack and releasing it looses the arrow, so how
long you hold is the draw. What a tap does is the bow's **Style**:

| Style | Tap | Hold |
|---|---|---|
| **Hold To Draw** (default) | Nothing (or a weak arrow with *Release Below Minimum*) | Must reach *Min Effective Draw*; stronger up to *Draw Time* |
| **Quick Or Drawn** | A **quick shot** (*Quick Damage / Velocity / Spread*) | Released after *Quick Shot Time*: grows from the quick shot to the full draw |
| **Quick Only** | Fires on press, no draw | Nothing more; with *Repeat While Held*, keeps firing every *Quick Fire Interval* |

A weapon that charges only a little: Hold To Draw with a short *Draw Time* (0.3 s). A weapon where holding should
only add range: set *Min Damage = Max Damage* and different velocities (or the reverse for damage only).

**What a shot costs.** Every mechanic can spend:

* **Ammo items** (*Ammo ▸ Ammo Type*, below). Leave Ammo Type empty for a weapon that needs no ammo.
* **Stamina** (*Stamina Per Shot*, and *Stamina While Drawing* for bows).
* **Extra Costs** (any number): **Mana**, **Health**, more **Stamina**, or units of any **Item** (a mana crystal,
  gunpowder). Without enough, the weapon does not fire and a message says what is missing ("Not enough mana",
  "No Mana Crystal left"). A magic bow: Ammo Type empty + Extra Cost Mana 5.

**Projectiles** (`ProjectileSettings`, every mechanic): model, radius, gravity, max distance, pierce (with falloff),
damage falloff over distance, stick into surfaces (recoverable with a chance), explosion radius and fuse, spin,
impact effect and sound. Projectile hits go through the same pipeline as melee hits: body parts (by the collider
hit), shields (arrows from the front are blocked), armour, on-hit effects.

**Ammo** (`Assets ▸ Create ▸ SimpleMovements ▸ Items ▸ Ammo`, or *Ammo Preset*): an item with an **Ammo Type**
(Arrow, Bolt, Bullet, Shell, Dart, Stone…). A weapon fires the ammo whose type matches its *Ammo ▸ Ammo Type*;
the ammo can multiply damage, add damage, change the element and speed, swap the projectile model and add on-hit
effects. Empty Ammo Type or *Infinite* = no ammo needed. The inventory counts and spends ammo (bag and hotbar).

**Aiming:** players shoot at the crosshair (a ray from the camera through the screen centre, *Aim Layers*); mobs shoot
straight ahead at their target. The projectile starts at a child of the weapon model named *Muzzle Marker*
(e.g. `Muzzle`, `ArrowRest`), else at the hand.

Templates: **Bow** (Bow mechanic), **Crossbow** (magazine 1, bolts, pierce 1), **Pistol** (magazine 8, bloom),
**Throwing Knife** (Throw, stack 10, dual-wieldable).

---

## 5. Equipment visuals

`EquipmentVisuals` (added to the player automatically) shows what is equipped:

* **Weapons** in the right hand, **off-hand items** in the left hand (shields on the forearm when *Shield On
  Forearm*). Item ▸ *Equipment Visuals*: the **Model** (empty = the item's Prefab), the **Sheath** point, a custom
  socket, sheath position/rotation, an off-hand pose.
* **Sheathing:** weapons go to their sheath point after *Auto Sheathe Delay* (6 s) out of combat and come back to
  the hand when you attack, block, are hit, fire or select them. Sheath *Auto*: one-handed blades and maces on the
  left hip, daggers on the right hip, big weapons, bows and shields on the back; tools and other items stay in hand.
  *StayInHand* keeps the old behaviour; *Hidden* hides it.
* **Attachment points:** Right/Left Hand, Back, Back Left/Right, Hip Left/Right, Chest, Head, Hips, forearms, upper
  arms. On a Humanoid they come from its bones; *Socket Overrides* replace any of them; missing ones are created as
  `Socket_<point>` children.
* **Armour** (Armor ▸ *Armor Visuals*): a **skinned** model is bound to the character's bones by name; a **rigid**
  model is attached to a point (pairs such as gloves and boots get a mirrored copy). *Hide Character Parts* hides
  body renderers under the armour (shared between pieces).
* Swapping and unequipping never duplicate models: each held model carries a `HeldItemModel` marker and only those
  are removed.

---

## 6. Consumable quickslots

`QuickSlotBar` (added to the inventory automatically, built beside the hotbar):

* **Assign** by dragging an item onto a quickslot (the item itself goes back where it was — the slot only remembers
  the **item definition**), or with the quickslot key while hovering an item in the inventory. Right click clears a
  slot. Assigning an item already on another slot moves it.
* **Use** with its key / button: the first stack that is not on cooldown is used, one unit is spent, the item's
  effects apply, the UI and weight update. Consumables (potions, food, buffs) and **throwables** (weapons with a Throw
  mechanic: thrown without changing the weapon in hand).
* **Feedback:** "No Health Potion left", "on cooldown (1.2 s)", "cannot be used now" in the message line; the cell
  flashes and greys out at 0.
* Counts follow the inventory (bag + hotbar with *Include Hotbar*); an emptied stack leaves an empty but assigned slot
  (*Clear When Depleted* empties it instead). Item cooldowns and a short shared cooldown prevent double use.
* Because a slot references the definition, not a stack, there are no stale references after a stack is split,
  merged, moved, dropped or used up.

---

## 7. The damage pipeline

`CombatEntity.ApplyDamage`:

1. damage-taken multiplier;
2. **interceptors**, in order: **body parts** (0: locate the part, multiply, remember it) → **blocking** (100:
   coverage, part, parry, reduction, stamina, guard break). An interceptor can stop the hit
   (`DamageStopped`, `StoppedHitCount`);
3. **damage-taken modifiers**: `CombatStats` armour — *per body part* when one was located (`ILocationalDefense`);
4. health.

`DamageInfo` carries `delivery` (Melee, Projectile, Area, Periodic), `hitCollider`, `aimedBodyPart`, `bodyPart`,
`unblockable`, `guardDamage`, and the results `blocked`, `parried`, `blockedAmount`. A fully blocked hit applies no
knockback and no on-hit effects. `CombatEvents.Blocked` reports every block.

Players and mobs share all of it: a mob with a `BlockController` blocks, a mob with a `BodyPartController` takes
headshots, a mob's arrows are blocked by the player's shield.

---

## 8. Controls

Every input is looked up **by name** in the player's input actions; when none exists, the fallback is used (and
skipped with a warning if that key is already bound to something else). The easiest setup is to add these actions
to the input actions (Player map), named as below; the updated `PlayerInput.inputactions` delivered with this
update adds them without changing the existing ones:

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| RotateItem | R (only read while the inventory is open; abilities do not fire then) | Right shoulder |
| Reload | B | West button |
| Block | Middle mouse (the right button rotates the camera) | Left trigger |
| OffHandAttack | H | Left shoulder |
| Sheathe | N | Right stick press |
| QuickSlot1 – 4 | F1 – F4 (number keys select the hotbar) | D-pad up / down for 1 – 2 |

Rebind them in the Input Actions editor as you like; the scripts only need the names. Key labels (quickslots, the
optional grid hint) show only the device you are using: keyboard keys until you touch a gamepad, then its buttons.

| Input | Action names tried | Fallback |
|---|---|---|
| Off-hand attack | `OffHandAttack`, `LeftAttack`, `SecondaryAttack` | Right mouse (*Right Mouse Off Hand Fallback*); Alternate also attacks with the off hand while dual wielding |
| Block / guard | `Block`, `Guard`, `Defend`, `Shield` | Right mouse (not while dual wielding); hold, or toggle with *Hold To Block* off |
| Shield bash | attack while blocking | — |
| Reload | `Reload` | R |
| Draw / sheathe | `Sheathe`, `ToggleWeapon`, `DrawWeapon`, `Holster` | none by default |
| Quickslot *n* | `QuickSlot{n}`, `Quickslot{n}`, `QuickItem{n}`, `UseQuickSlot{n}` | Z, X, C, V (B, N, F1, F2 for slots 5–8); D-pad on a gamepad |
| Rotate grid item | `RotateItem`, `Rotate` | R (see [11](11-Grid-Inventory.md)) |

Attack inputs are ignored while the inventory is open.

---

## 9. Limitations

* Animations are not included: the system sets triggers and bools (`OffHandAttackTrigger`, `IsBlocking`,
  `ParryTrigger`…) and plays the clips assigned on the attacks; add the clips and Animator states for your rig.
  Draw / reload animations play through the attack's charge animation and the mechanic's clip.
* Visual attachment uses Humanoid bones when available; generic rigs need *Socket Overrides*.
* Severing hides and collapses bones; it does not cut meshes. Assign *Severed Prefab* for the piece that falls.
* Body parts on mobs without a `BodyPartController` are not used (by design: opt-in per character or globally).
* Networking is not handled here (the systems are multiplayer-safe in finding their own player, but do not
  synchronise state).
