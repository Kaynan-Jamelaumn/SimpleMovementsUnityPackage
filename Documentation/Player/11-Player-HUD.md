# 11 — Player HUD

The player's HUD is **built in code when the game starts**: nothing to assemble in the editor. Every part shares one
warm, soft look (rounded panels with a thin golden border and a soft shadow), and the inventory's hotbar and quickslots
are restyled and placed with it, so the bottom of the screen reads as one dock.

```
 ┌───────────────────────────────┐
 │ (Portrait)  Name        Class │                    ┌─────────┐ ┌──────────────────────────┐
 │  Lv ●       [Health 120/150]  │                    │ Active  │ │ [Q] [E] [R] [F] [1] [2]  │   ← abilities bar
 │             [Mana    40/80 ]  │                    │ Trait ◆ │ └──────────────────────────┘
 │             [Stamina       ]  │                    └─────────┘ ┌──────────────────────────┐ ┌─────────────┐
 │             [experience    ]  │                                │ hotbar 1 … 9             │ │ quickslots  │
 └───────────────────────────────┘                                └──────────────────────────┘ └─────────────┘
  (Food) (Water) (Rest) (Mind) …   ← needs gauges
```

## Building it (buttons)

Select the player (in the scene, in Prefab Mode, or its prefab in the Project window) and use either:

* the **Player HUD** component's inspector — add it with *Add Component ▸ Player HUD*, or the validator's
  *Add Player HUD* button — with **Build Status UI**, **Build Abilities UI**, **Build Both**, **Remove** and **Select**;
* or the menu **Tools ▸ SimpleMovements ▸ Player ▸ Player HUD ▸ Build Status UI / Build Abilities UI / Build Both /
  Add Player HUD To Selection**;
* or the **Player Setup Validator ▸ Player HUD** section (Add Player HUD, Build Status UI, Build Abilities UI).

What you build is **saved in the prefab / scene** (under the player's canvas, in a full-screen *Player HUD* object):
select it in the hierarchy and move it, resize it, recolour it, swap texts, images or whole slots — the game uses
what you built. Building again replaces it (Undo works). A part you never build is generated when the game starts
(*Build Missing At Runtime*), so a player without any setup still gets the full HUD.

The Inventory Manager adds a Player HUD when missing (Inventory Manager ▸ *Player HUD*). The HUD only shows for the
player of this machine (a HUD saved in the prefab stays hidden on other players' copies).

## Your own UI (customisable)

| Where | What it does |
|---|---|
| **Player HUD ▸ Status UI / Abilities UI** | Point them at your own objects (a **Hud Status Frame** / **Hud Ability Bar** with its parts assigned) instead of building. |
| **Hud Status Frame** (on the built status UI) | Every part is a field: name, class, level, initial, portrait picture, health / mana / stamina / experience bars, needs root and gauges, *Add Missing Gauges*. Any field can be empty. |
| **Hud Ability Bar** (on the built abilities UI) | Slots list (each **Hud Ability Slot** has a *Key Index*: which ability key it shows), trait panel / slot / caption, *Dock Above Hotbar* (off: stays where you put it), *Match Key Count*, *Fit To Slots*, *No Trait Text*. |
| **Hud Ability Slot** | Frame, icon, cooldown sweep, cooldown text, key text, charges, cast glow, cast progress, ready flash, initials: assign your own images / texts. |
| **Hud Need Gauge** | Status Key (Hunger, Thirst, Sleep, Sanity, BodyHeat, Oxygen, Weight…), ring, value, label, colours, warning level. |
| **Hud Resource Bar** | Low warning, trail delay, show value. |
| **Hud Shape** | Keeps the code-made rounded / circle / ring / glow sprites after a reload; set Kind to None to use your own sprite. |
| **Your Own UI prefabs** (Player HUD) | *Status Frame Prefab*, *Ability Bar Prefab*, *Ability Slot Prefab*, *Trait Slot Prefab*, *Need Gauge Prefab*: used by the Build buttons (and at runtime). |
| **Make a prefab to customise** (inspector buttons) | *Ability Slot Prefab*, *Trait Slot Prefab*, *Need Gauge Prefab* create a ready prefab in `Assets/Resources/SimpleMovements/UI/HUD` and assign it; *Save Status UI / Abilities UI As Prefab* save what you built (with your changes) and assign it. Open the prefab, restyle it, press Build again. |
| **Custom Root** | Build the HUD inside your own canvas or panel. |
| **Palette, sizes, margin, spacing** | The look of everything built. |

## Keys shown on the slots

* **Ability keys**: from each key's **Input Action Reference** (Abilities State Machine ▸ ability actions); when it is
  empty, the action named *Ability1…N* of the player's input actions. The text is Unity's binding display string (first
  binding only, shortened: Left Button → LMB, Left Shift → Shift), read every frame, so rebinding shows at once (when
  the rebinding is made on the same input actions asset).
* **Active trait**: the trait's Input, else its *ActiveTrait* action, else its fallback key (J).

## The parts

| Part | What it shows |
|---|---|
| **Player frame** (top left) | Portrait with the name's initial and the **level** badge, name and class, **health**, **mana** and **stamina** bars, and a thin **experience** bar to the next level. Bars glide to their value; a pale trail shows how much was just lost, then follows; a low bar pulses. Statuses the player does not have are left out. |
| **Needs** (under the frame) | Ring gauges for the other statuses: Food (hunger), Water (thirst), Rest (sleep), Mind (sanity), Warmth (body heat), Air (oxygen), Load (weight), with their percentage. A gauge shows only while its status shows its bar (rest, warmth and air appear when they matter) and pulses when low (Load: when nearly full). |
| **Abilities bar** (above the hotbar) | One slot per ability key (Abilities State Machine), in order: the ability's **icon**, its **key** in a small badge, a warm **glow while it is cast** and the cast filling the slot from the bottom, a **cooldown sweep with the seconds left**, a soft flash when it is ready again, the **charges** when it has several. Blue icon: not enough mana / stamina; grey: stunned, silenced, dead. **Empty keys show an empty slot (no icon).** **Hover**: name, key, description, cast time, cooldown, costs and effects, and when it is ready. Left click casts it. |
| **Active trait** (left of the abilities) | The character's active trait (the trait with a skill on its own key): its icon, key, cast glow and cooldown like an ability, its name above; hover shows the trait's description and its skill. "No Active Trait" when there is none. |
| **Hotbar** | The inventory's hotbar, restyled (rounded slots, warm colours); it stays where it is and the dock is placed around it. |
| **Quickslots** | Restyled and placed right of the hotbar, bottoms aligned. |

The old status bars made by the Inventory UI Builder are hidden (*Hide Old Status Bars*): the player frame replaces
them. Each status keeps working the same way; the HUD only reads it.

## Settings (Player HUD component)

| Setting | Use |
|---|---|
| Hud Root, Status UI, Abilities UI, Tooltip UI | What was built (or your own objects). |
| Build Missing At Runtime | Parts not built in the editor are generated when the game starts. |
| Status Frame, Needs, Ability Bar, Active Trait | Which parts are built. |
| Style Hotbar, Dock Quick Slots | Restyle / place the hotbar and quickslots when the game starts. |
| Hide Old Status Bars | Hide the Inventory UI Builder's status bars. |
| **Palette** | Every colour: panels, borders, slots, text, accent, casting glow, cooldown, bar colours. |
| Ability Slot Size, Trait Slot Size, Margin, Dock Spacing | Sizes and spacing (pixels at 1920 × 1080). |
| Custom Root, Status Frame / Ability Bar / Ability Slot / Trait Slot / Need Gauge Prefab | Your own UI (see above). |

Component menu ▸ *Rebuild HUD (Play Mode)* tries palette changes live (not saved).

## For your own UI

* `HudAbilitySlot` — one ability slot; `Bind(IHudAbilitySource, ...)` shows anything that implements the source
  (icon, key, slot, description, use).
* `HudResourceBar` — a smooth bar with the loss trail; call `SetValue(current, max)` every frame.
* `HudTooltip` — the hover panel.
* `AbilityStateMachine` gives `Icon`, `KeyLabel`, `DisplayName`, `CooldownRemaining`, `Charges`, `CastNow()`;
  `TraitManager.GetRuntimeBehaviours(trait)` gives the active trait's live ability slot and key.
* `StatusManager.UIImage` — the image a status fills.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| No HUD | Inventory Manager ▸ Player HUD off, or the player is not this machine's. The Console says if no player was found. |
| No abilities bar | The player has no Abilities State Machine / Player Ability Controller. |
| A slot has no key badge | That ability key has no input binding. |
| Hover shows nothing | The cursor must be free (open the inventory, or a cursor-free mode). |
| Two sets of status bars | Tick Hide Old Status Bars. |
| My changes to the built UI are gone | Build Status UI / Build Abilities UI replace what was built: save your version first (Save … As Prefab) and it is used by the next build. |
| Rounded corners / circles missing after a reload | The image lost its Hud Shape component: add one (Kind Rounded / Circle / Ring / Glow). |
