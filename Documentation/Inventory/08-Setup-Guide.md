# 08 — Setup Guide: Inventory, Items, Armor and Weapons

Step-by-step recipes. The reference for every field is in chapters 02–05; the tools in 06.

---

## 1. The inventory on the player

1. The player needs a `PlayerStatusController` (see [Player 01](../Player/01-Player-Setup.md)) and a
   `WeaponController` whose **Hand** is the hand bone.
2. Add an `InventoryManager` to the player.
3. **Select the player** (or open its prefab). The editor builds, inside the player:

   | Built | Where |
   |---|---|
   | Canvas (Screen Space Overlay, 1920×1080 scaler) | Inside the player |
   | Hotbar | Bottom centre |
   | Inventory panel with slots and a drop zone (drop items into the world) | Centre, closed |
   | Equipment panel: one labelled slot per armor / jewelry slot | Right, closed |
   | Storage panel, item info, hover panel, split popup | Created as needed |
   | Status bars (one per status manager) | Top left |
   | Interaction prompt | Screen centre |
   | Armor Sets window + a **Sets** button on the inventory | Centre, closed |
   | Slot and item prefabs, sprites | `Assets/Inventory/Generated` |

   Every reference is assigned. **Build / Repair UI Now** on the `InventoryManager` does it on demand;
   **UI Builder…** lets you choose slot counts, sizes, colours and which parts to build.
4. Set **Hand Parent** to the same hand bone as the Weapon Controller (the inspector offers *Use the weapon hand*).
5. Play: **Tab / I** opens the inventory, **1–9** select the hotbar, **left click** uses the selected item or attacks,
   **right click** a stack splits it, hovering shows the item's details.

| Input | Action name looked up | Fallback |
|---|---|---|
| Open / close the inventory | `Inventory`, `OpenInventory`, `ToggleInventory` | Tab, I |
| Use item / attack | `UseItem`, `Use`, `Attack`, `PrimaryAttack`, `Fire`, then any action on the left mouse button | Left mouse button |
| Armor Sets window | `ArmorSetUI`, `ArmorSets`, `Sets` | Sets button |
| Light / Heavy / Special / Alternate attack | `WeaponController ▸ Extra Attack Inputs` (Input Action references) | Or wire Player Input events to `OnLightAttack`… |

---

## 2. Any item

1. *Assets ▸ Create ▸ Scriptable Objects ▸ Item ▸ …* (Consumable, Equippable, Armor, Weapon).
2. At the top of the inspector, **Quick Setup**:
   * **Auto-Fill** — display name from the asset name; icon and prefab found by name (`IronSword`,
     `IronSword_Icon`…); slot, category or food / potion guessed from the name; stack size and durability.
   * **Icon From Prefab** — renders the prefab and saves it as the icon.
   * **Presets…** — ready-made values for that kind of item.
3. Fill in what is left: **Name**, **Description**, **Icon**, **Prefab** (the world / in-hand model), **Stack Max**,
   **Weight**, **Price**, **Durability**.
4. **Hand position** (Position, Rotation, Scale): how the model sits in the hand — tune it in Play mode, the values
   are kept.
5. The **Tooltip Preview** at the bottom shows exactly what the player will read.

### A world pickup

Put the item's prefab in the scene with a **Collider** and an `ItemPickable` (assign the item). Interact picks it up
(an *Interaction Time* > 0 means holding). `ItemSpawner` spawns random items; chests use `Storage`.

### A consumable (potion, food)

*Item ▸ Consumable*, then **Presets…** ▸ Potions / Food & Drink (minor / normal / greater health, regeneration,
mana, stamina, bread, cooked meat, apple, water…), or set its effects by hand. *Use Feedback* (animation, sound,
particles) plays when it is used.

---

## 3. Armor

1. *Assets ▸ Create ▸ Scriptable Objects ▸ Item ▸ Armor*.
2. **Armor Slot** — Helmet, Armor, Leggings, Boots, Gloves, Shield, Ring, Amulet… (it only fits its slot).
3. **Presets… ▸ Material** — Cloth, Leather, Chainmail, Iron, Steel, Mythril (defense scaled by slot).
4. Defense, Magic Resistance, elemental resistances, durability; **Equip Effects** for anything else (stats, procs,
   traits, abilities while worn — picked from a dropdown).
5. Drag it into the matching equipment slot in game: its stats apply; take it off and they are removed exactly.

---

## 4. An armor set

1. *Tools ▸ Inventory ▸ Armor Set Wizard*: name, colour, folder, material, which pieces, defense per piece, which
   tiers (2 / 3 / 4 pieces). It creates the set and every piece, linked both ways.
2. Open the **Armor Set** asset and fill in the **bonus tiers** (each tier = effects active from N worn pieces).
   The **Bonus Simulator** shows which tiers are active for 1…N pieces.
3. In game, the **Armor Sets window** (Sets button) lists worn sets, their pieces, progress and active / next
   bonuses, and opens by itself when a set is completed.

Existing armor: set *Belongs To Set* on each piece and press **Add pieces that reference this set** on the set.

---

## 5. A weapon

### Fastest: from a template

*Assets ▸ Create ▸ Scriptable Objects ▸ Item ▸ Weapon From Template ▸* Sword, Greatsword, Dagger, Spear, **Hammer**,
Axe, Bow or Staff. You get a full moveset (chains, charged attacks, alternate attack, on-hit effects). Then:

1. **Quick Setup ▸ Auto-Fill** and assign the **Prefab** (the model in the hand) and **Icon**.
2. **Presets… ▸ Tier** (Wooden → Legendary) for damage, crit, durability, weight and value.
3. In the **Attacks** section, give each attack its **Animation Clip** and sounds (templates leave them empty).
4. Press **▶** on an attack: the **Attack Preview** plays it on the player in the scene and draws the area it hits.

### The weapon inspector

| Section | What to set |
|---|---|
| Item | Name, icon, prefab, weight, durability, hand position |
| Damage & Stats | Category, scaling attribute, **min–max damage**, crit, knockback, attack speed, element; tool type / damage for axes and pickaxes |
| Attacks | One tab per input — **Normal** (required), Light, Heavy, Special, Alternate. Each attack: timing (startup → active → recovery), cost and movement, damage, **hit area**, on-hit effects, charge, behaviours, sound, chain hits |
| Hit Area & Range | What each attack reaches, **Set Max Range From Attacks**, the **Weapon Blade**, the old Attack Cast |
| Combos | Fixed input strings and the Combo Tree |
| Traits & While Wielded | Weapon traits (change the weapon) vs passive effects (on the player) |
| Animation & Sound | Animation set, attack / equip / unequip sounds |

### Choosing how an attack hits

| Hit Detection | Use for | Set |
|---|---|---|
| **Hit Shape** | Most attacks: a cone, circle, line, rectangle… in front of the character during the active phase | The attack's *Hit Shape* |
| **Weapon Blade** | Hits that should follow the animation: what the weapon itself touches | The weapon's **Hit Volumes** (once per weapon); each attack picks which |
| None | Attacks that only fire projectiles or abilities (behaviours) | Behaviours |
| Weapon Cast | Old weapons only | The weapon's Attack Cast |

**Weapon Blade** — the weapon's **Hit Volumes** on the model in the hand, following the animation and swept between
frames so fast swings hit everything they pass through. A weapon can have any number of volumes, each with a name and
a shape:

| Shape | For | Placed with |
|---|---|---|
| **Capsule** | Blades, shafts, handles, spear tips | **Axis** (handle → tip), **Start** / **End** (metres from the grip), **Radius** — or two marker children (*Start Marker*, *End Marker*) |
| **Sphere** | Mace and flail heads, pommels, fists | **Centre** (metres from the grip, model axes), **Radius** — or a *Centre Marker* child |
| **Box** | Axe and hammer heads, shields | **Centre**, **Size**, **Rotation** — or a *Centre Marker* child (its rotation is used too) |

**Hit Area & Range ▸ Add Volume ▾** adds ready-made ones (Blade, Handle, Head box, Head sphere, Pommel, Spear tip,
Shield). Markers are empty children of the weapon **prefab** placed exactly on the part — the most precise way, and
it works for any model. Each attack's **Volumes Used** buttons choose which volumes it uses (none selected = all):
a pommel strike uses *Pommel*, a shield bash *Shield*, a slash *Blade*.

### Example: a hammer with a ground slam

1. *Weapon From Template ▸ Hammer*; assign the prefab and clips.
2. **Hit Area & Range ▸ Add Volume ▾ ▸ Head (box)** and **Handle (capsule)**. Fit the Head box to the hammer's head
   (or add an empty child named `Head` at the head's centre in the prefab, rotated like the head).
3. **Attacks ▸ Heavy ▸ Hit Area & Targets ▸ Hit Detection = Weapon Blade**, **Volumes Used = Head** — the swing hits
   only what the head touches.
4. **Impact – ground slam ▸ Enabled**, **Contact Volume = Head** (the part that must reach the ground):

   | Field | Value | Meaning |
   |---|---|---|
   | Trigger | Ground Contact Or Active End | When the head reaches the ground (or at the end of the swing) |
   | Ground Layers | Nothing | = *Combat Settings ▸ Ground Layers* (physics: what counts as ground) |
   | Area | Circle, radius 3 | Everyone inside is hit, touching the hammer or not |
   | Filter | Enemies | Who the slam can hit (relations, not layers) |
   | Damage Multiplier | 1.5 | × the swing's damage |
   | Edge Multiplier | 0.5 | Half strength at the edge |
   | Effects | Knockback (up), Stun 0.5 s | What it does to each character hit |
   | VFX / Sound | Dust, crack | At the impact point |

5. **▶ Preview** the Heavy attack: the hammer appears in the animated hand, the Head box moves with the swing and
   leaves a trail, and when it reaches the ground the impact circle appears where it lands (orange). In game, an
   *Impact* behaviour moment (`AttackMoment.Impact`) lets you add more: cast an ability at the impact point, spawn a crack…

### Who an attack can hit

Each attack has a **Hit Filter** (Self, Allies, Enemies, Neutral, Party) and optional **Target Rules** (friendly fire
Game Rule / Never / Always, kinds of characters, only / ignore factions). A support weapon can heal its party
(filter *Party*, an on-hit Heal) while dealing no damage to them. See
[09 — Teams, Factions & Targeting](09-Teams-Factions-and-Targeting.md).

---

## 6. Test it

* `InventoryManager` inspector ▸ **Live (Play Mode)**: give yourself any item, see equipped items, combat stats,
  active set bonuses and the weapon's current attack.
* Weapon inspector ▸ **▶** / *Tools ▸ Inventory ▸ Attack Preview*: scrub an attack without Play mode.
* In Play mode, select the player: the Weapon Controller's gizmo draws the active attack's real hit area, its phase
  and the weapon's range. *Debug Mode* on the Weapon Controller logs attacks, hits and combos.
