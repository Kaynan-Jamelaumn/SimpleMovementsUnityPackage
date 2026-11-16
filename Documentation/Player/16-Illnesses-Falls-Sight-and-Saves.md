# 16 — Illnesses, Falls, Sight & Saves

**Scripts:** `Essentials/Combat/Injuries/InjuryController.Illness.cs` (exposures, complications, contagion, immunity,
falls, sight), `InjurySettings.cs` (`IllnessRules`, `IllnessExposure`, `InjuryComplication`, `FallRules`),
`InjuryTypes.cs` (`VisionEffect`, `InjuryVision`, `ExposureSource`, `ComplicationWhen`), `InjuryDefaults.cs` (the
illnesses, their medicines, the upgrade of older assets), `InjurySaves.cs`, `Essentials/UI/HUD/InjuryScreenEffect.cs`,
`Inventory/Items/ConsumableSO.cs` (`IllnessRisk`), `Harvesting/Items/HarvestedFoodSO.cs`, tests in
`CustomEditor/Combat/InjuryIllnessTests.cs`.

The injury system ([Player 13](13-Injuries-and-Treatment.md)) also makes the character **ill**: a wound left dirty
gives tetanus, a severe infection reaches the blood, the cold gives a cold that may turn into a flu and then
pneumonia, bad meat gives food poisoning or worms, foul water dysentery, a sick animal's bite rabies. This page also
covers what the same update added around it: **hard landings** (a sprained ankle, a broken leg), what injuries do to
the player's **sight**, and injuries **kept between sessions**.

An illness is not a second system: it is an **injury type of the whole body**. It has stages, effects, a clock that
makes it worse, healing, treatments, resistances and a line on the HUD exactly like a laceration has; everything on
page 13 and [page 14](14-Damage-Forms-and-Injury-Resistances.md) applies. All of it is data in the Injury Settings.

---

## 1. Setup

* **A new Injury Settings asset** has everything (the Whole Body part, the illnesses, the exposures, the falls).
* **An asset made before this update** shows *Add Illnesses And New Injuries* at the top of its inspector. It adds the
  Whole Body part, the injury types that are missing (see *What was added* below), the exposures and the fall rules,
  and links the old types to the new ones (a severe infection now ends in sepsis, frostbite in gangrene, dirty wounds
  may give tetanus). **It keeps your own changes**: a type you already have is never replaced, a link is only set
  where the type still has its old default, and a dialog lists what it did (one Undo step).
* Then run *Tools ▸ SimpleMovements ▸ Player ▸ Injuries ▸ Create Medical Items (Presets)* again for the new medicines
  (and the new uses of the old items), and put some in loot tables or shops.
* Optional: give food and drink **Illness Risks** (§3d).

Until the button is pressed an older asset has the new **rules** - falls, the body's state, area hits, saves, the
new interface - but none of the new **injuries and illnesses**: a hard landing bruises or breaks a leg by the old
rules and cannot sprain an ankle, and nothing makes the character ill. (A built-in Whole Body part stands in, so an
illness given by a script still has a place to go once its type exists.)

**What was added** — injuries: Sprained Ankle, Dislocation, Eye Injury, Ear Injury, Severed Artery, Internal
Bleeding, Punctured Lung, Cut Tendon, Gangrene (their table is on page 13). Illnesses: Sepsis, Tetanus, Rabies, Cold
(→ Flu → Pneumonia), Dysentery, Food Poisoning, Parasites, Heatstroke, Venom.

---

## 2. The Whole Body part and what makes a type an illness

*Injury Settings ▸ Body Parts* has an 18th part, **Whole Body** (kind *Whole Body*, id `Body`). It is where illnesses
live: hits never land on it, it has no condition to wear down, it cannot be lost and it is not drawn on the figure -
the Health tab shows it as the **Whole Body** card beside the figure.

An injury type with **Systemic** on is an illness: whatever part it is given to, it goes to the Whole Body, and only
systemic types can be there. Everything else about it is an ordinary injury type (*Injury Settings ▸ Injuries*; the
*Illnesses* page lists them with a readable summary).

These stage and type fields were added for them (any injury may use them):

| Field | On | What it does |
|---|---|---|
| **Hidden** | stage | An incubation: the injury is there and its clock runs, but the player is told nothing - no row, no HUD line, no message - until it reaches a stage that shows ("You have come down with tetanus."). A healing illness never steps back down into a hidden stage |
| **Worsen Chance** | stage | When *Worsen After Seconds* runs out it gets worse with this chance (the character's resistance counts); otherwise the wait starts again. 1 = always (wounds). A cold becomes a flu 30% of the time |
| **Contagion Per Minute** | stage | Chance per minute to give it to each character within *Contagion Radius* (§3c) |
| **Vision** | stage | What it does to the player's sight (§6) |
| **Immunity Seconds** | type | After healing from it the character cannot catch it again for this long (a cold: 30 min) |
| **Complications** | type | Other injuries it may cause (§3a) |
| **Sided** | type | One per side instead of per part: an eye injury is the *Left* or the *Right* eye's (named so), and its Vision darkens that half of the screen |
| **Hud Alert**, **Icon Shape** | type | Its line on the HUD (*None / Notice / Warning / Critical*) and its icon in the Health tab |

### The default illnesses

| Illness | Caught from | Stages | What it does | Cure |
|---|---|---|---|---|
| **Sepsis** | a severe infection (10 min) or gangrene (7 min) left alone | Sepsis → Septic Shock after 8 min | −0.12 then −0.35 health/s, stamina and speed down, thirst; shock: no sprinting, stuns, tunnel vision | **antibiotics** only (it never heals by itself) |
| **Tetanus** | dirty wounds left untreated: punctures 2%/min, bites 1.5%, deep wounds 1%, lacerations 0.4% | *incubating 5 min (hidden)* → Tetanus → Severe (60% after 10 min) | muscle spasms (stuns), slower attacks and movement; severe: lockjaw, no sprint / roll / dash, −0.08 health/s | **antitoxin**: cures it while it incubates, lets it heal once it shows. Disinfecting, stitching or a sterile bandage on the wound prevents it. Immune for 1 h after |
| **Rabies** | a bite wound, 1.5% when it is made | *incubating 10 min (hidden)* → Rabies (Critical) | fever, fear, stuns, −0.06 health/s, sanity falls fast | **vaccine while it incubates**. Once it shows no medicine helps: only a healing ability that cures it |
| **Cold** | being cold (4%/min), freezing (8%), exhausted (1%); from someone near who has it | Cold → Flu (30% after 5 min) → Pneumonia (25% after 8 min) | coughing fits (short stuns), less stamina; flu: slower, thirst, haze; pneumonia: −0.06 health/s, no sprinting | passes by itself: a cold in 10 min, a flu eases back into a cold after about 19 (half that asleep); **cold remedy** halves it and stops it getting worse; pneumonia needs **antibiotics**. Immune for 30 min after |
| **Dysentery** | foul water or food (Illness Risks), tainted meat | *incubating 2 min (hidden)* → Dysentery → Severe (35% after 7 min) | thirst ×2.2 then ×3, hunger, stamina; severe: −0.08 health/s, no sprinting | passes slowly by itself; **stomach remedy**; severe needs the remedy or **antibiotics**. Immune for 15 min after |
| **Food Poisoning** | spoiled or badly butchered food | Nausea → Food Poisoning (50% after 2 min) | hunger and thirst drain, stamina, retching (stuns), −0.03 health/s | passes by itself (5 / 8 min); **stomach remedy** halves it |
| **Parasites** | raw or fouled meat (gut-shot, contaminated), foul water | *incubating 5 min (hidden)* → Intestinal Parasites → Heavy Infestation (50% after 15 min) | hunger ×1.8 then ×2.5, less stamina, −0.03 health/s | never by themselves: **antiparasitic** |
| **Heatstroke** | being overheated (10%/min) | Heat Exhaustion → Heatstroke (40% after 5 min) | thirst ×2 then ×2.5, less stamina, haze and dizziness, −0.08 health/s | passes by itself (5 / 13 min); staying in the heat brings it back |
| **Venom** | a creature's attack (*Injury ▸ Inflict Injury*, type `Venom`) | Envenomed · Deadly Venom | −0.12 / −0.3 health/s (Poison), stamina, haze | burns out by itself (4 / 9 min); **antivenom** ends it at once |

Everything above is the default data: change any number, add stages, or write your own (§8).

---

## 3. Where illnesses come from

### 3a. Complications (injury type ▸ Complications)

An injury can cause another one. Each entry: the *Injury Type* caused, **When**, a *Chance*, *From Stage* (only once
the injury is at least that bad) and the *Stage* the new one starts at.

| When | Rolled |
|---|---|
| **Inflicted** | once, when the injury appears (a bite: rabies) |
| **Worsened** | once, each time it moves to a worse stage |
| **While Untreated** | a chance **per minute** for as long as it is neither treated nor kept from worsening (a dirty puncture: tetanus) |

A complication that is an illness goes to the whole body; any other appears on the same part. The target's
resistances and immunity to the new injury count. Defaults: Deep Wound, Laceration, Puncture and Bite → Tetanus while
untreated; Bite → Rabies when inflicted; Severe Infection → Gangrene (3%/min while untreated).

This is separate from a stage's *Worsen After Seconds* / *Worsens Into*, the one thing an injury becomes when its
clock runs out: its own next stage, or - when another type is named - that injury, added once while the first one
stays (an open laceration → an infection on the same part; a severe infection → sepsis of the whole body).

### 3b. Exposures (Injury Settings ▸ Illnesses)

What the body's state may give the character, a chance per minute while it lasts:

| Source | True while |
|---|---|
| **Cold** / **Freezing** | the body heat is Cold or Extremely Cold / Extremely Cold |
| **Overheated** | the body heat is Extremely Hot |
| **Starving** / **Dehydrated** | hunger / thirst is empty |
| **Exhausted** | extremely sleepy |
| **Open Wound** / **Bleeding** | a wound that still bleeds is neither bandaged nor treated / anything bleeds |

The needs come from the player's status managers (Body Heat, Hunger, Thirst, Sleep); a need the player does not have
never exposes. An exposure may also give an ordinary injury on parts of some kinds (*Part Kinds*): the default
*Freezing → Frostbite on a hand or a foot*. Defaults: Cold → Cold 4%/min, Freezing → Cold 8%, Exhausted → Cold 1%,
Freezing → Frostbite 6%, Overheated → Heatstroke 10%.

Exposures, contagion and per-minute complications are checked every *Check Seconds* (5 by default); the chance is
scaled to the time that passed, so the setting changes the cost, not the odds. *Illnesses ▸ Enabled* off stops
exposures, contagion and the body's state; complications, food, abilities and scripts still give illnesses.

### 3c. Contagion

A stage with *Contagion Per Minute* passes its illness to every character with an Injury Controller within
*Contagion Radius* (4 m): players and mobs alike, each with that chance per minute, their own resistances and
immunity counting. It starts at the illness's first stage. Defaults: a cold 2%/min, a flu 4%, pneumonia 2%, dysentery
0.5%. Set the radius to 0 for no contagion.

### 3d. Food and drink

* **Any consumable** (`ConsumableSO`) has **Illness Risks**: an illness id, a chance each time it is used, a starting
  stage. Dirty water: `Dysentery` 0.3 and `Parasites` 0.1; raw meat: `Parasites` 0.25, `FoodPoisoning` 0.2. The item's
  tooltip says so ("May cause dysentery (30%)").
* **Harvested food** (`HarvestedFoodSO`, *Illness Chance*): a piece that is not fit to eat (spoiled, poisoned,
  tainted, contaminated, gut-shot) may give food poisoning - more likely the worse and the lower the quality of the
  piece - parasites from a gut-shot or contaminated one, dysentery from a tainted one. This is on top of the sickness
  it already caused.

### 3e. Abilities and scripts

* The ability effect ***Injury ▸ Inflict Injury*** takes an illness as its type (a spider's bite: `Venom`; a plague
  spell: `CommonCold` at stage 1): it goes to the whole body and the part settings are ignored. The effect's own
  chance decides; the target's immunity and resistances count.
* `InjuryController.Expose("Dysentery", 0.3f)` from a trap, a zone or a quest (§9).

---

## 4. The body's state: rest, hunger, cold

*Injury Settings ▸ Illnesses ▸ Body state* - for every injury and illness, not only illnesses:

* **Sleeping Heal Multiplier** (2): everything heals twice as fast while the character sleeps.
* **Starving Heal Multiplier** (0.5): half as fast while hunger or thirst is empty.
* **Weakened Worsening Multiplier** (1.5): injuries and illnesses get worse 50% sooner while the body is cold,
  starving or dehydrated.

The Health tab's summary says which applies ("Resting: healing ×2", "Cold, hungry or thirsty: injuries get worse
sooner"). So the cure for a flu is the old one: get warm, eat, drink and sleep.

---

## 5. Hard landings (Injury Settings ▸ Movement ▸ Falls)

Falls are judged in **metres dropped**, not in landing speed, so the rule does not depend on the gravity your
movement uses (the Player Movement Model's Gravity × Multiplier, which is stronger than real gravity).

A player who drops more than its **safe height** is hurt, by the metres above it:

* a **crushing hit on a foot or a leg** (a foot more often than the shin or the thigh above it), *Damage Per Metre*
  (3) - it wears the part down and causes injuries by the ordinary rules: a twisted or sprained ankle from a small
  drop, a cracked then a broken bone from a higher one;
* from **Both Legs Above** (5 m over) the other leg is hurt too, at 60%;
* **Health Percent Per Metre** (4): that share of the *maximum* health per metre over, which no armour softens - so a
  drop 25 m above the safe height kills anyone, whatever their health (0 = a fall only hurts the legs);
* a treatment being applied is interrupted (*Damage Interrupts Treatment*).

Measured with the default body, over 400 landings each (safe height 4 m):

| Drop | Sprain | Cracked bone | Broken | Compound | Both legs | Health |
|---|---|---|---|---|---|---|
| 5-6 m | 4 in 10 | - | - | - | - | −4% to −8% |
| 7-9 m | 1-2 in 10 | 4-5 in 10 | - | - | - | −13% to −18% |
| 11 m | 3 in 10 | 6 in 10 | 4 in 10 | - | always | −27% |
| 15 m | 3 in 10 | 1 in 10 | 6 in 10 | 4 in 10 | always | −43% |
| 22 m | 2 in 10 | - | 1 in 10 | 9 in 10 | always | −74% |

### What counts as a fall

* **A character's own jump never hurts** (*Own Jump Is Safe*). The height it gained by jumping right before the fall
  is taken off the drop, up to the highest jump it can make right now - its Jump Force with every trait, item, buff
  and size change, from a run. A character that jumps 12 m high lands that jump unhurt; jumping off a cliff counts
  the cliff, exactly as walking off it would. Being thrown higher than its legs could jump does count (the part above
  its own jump). Off = the drop is measured from the top of the jump.
* **A skill's leap is not a fall** (*Ignore Forced Movement*): nothing counts while an ability, a dodge or a
  knockback moves the character, nor the fall that follows it until it lands - a leap that carries the caster up and
  lets go does not break its legs.
* **A slowed descent starts over**: a glider opened in mid-air, a slow-fall effect or a second jump in the air ends
  the fall so far; only what is dropped after it counts. A descent that is slow all along (gliding, a lift, a slope,
  stairs) is never a fall.
* **Water** at least *Safe Water Depth* (1 m) deep breaks any fall; a puddle does not.
* Teleports are not falls. Mobs do not take fall injuries.

### Softening or removing fall damage (traits, items, skills, essences)

Everything that can carry a stat or a resistance can change what a fall does:

| What | Where | Effect |
|---|---|---|
| **Fall Damage** (character stat, percent) | a trait, a race or class, an item or armour set (*Equip Effects ▸ Stats*), a buff from an ability | what a landing does to the legs and the health: −50 = half. Several multiply |
| **Safe Fall Height** (character stat, metres) | the same places | added to the safe height: +3 = unhurt from 7 m. Negative = hurt by smaller drops |
| **Injury Resistance ▸ Fall Damage** | the trait behaviour *Passive ▸ Injury Resistances*, *Equip Effects ▸ Stats ▸ Injury Resistances*, the ability effect *Buff ▸ Injury Resistance* (timed), the Injury Resistances component, scripts | the same change, and **−100 = never hurt by a fall** (a Feather Fall spell for 10 s, boots of soft landing) |
| **Jump Force** | anything that raises it | the character's own jump is higher, and stays safe |
| Stronger legs | *Fracture Chance*, resistances to Crush hits or on feet and legs (page 14) | the same landing breaks less |

Trait presets: **Light Feet** (−50% fall damage, +2 m), **Feather Fall** (never hurt), **Weak Ankles** (+50%, −1.5 m).
The Health tab's summary lists what applies, and the Injury Controller's inspector shows the character's safe
height, its own jump height and its fall damage factor next to the **Land** test button.

`InjuryController.Land(metres)` causes a landing from a script (a trap door, a throw); the same safe height and
reductions apply.

---

## 6. Sight (stage ▸ Vision, Injury Settings ▸ Show Screen Effects)

A stage's **Vision** has four amounts, 0 to 1:

| Field | On the screen |
|---|---|
| **Darkness** | that eye's half of the picture goes dark: the left half for a left eye (a *Sided* injury, or one on a left part), both for an injury with no side. 1 = black |
| **Haze** | a pale blur over everything (a swollen eye, a fever) |
| **Tunnel** | the edges close in (shock, a concussion) |
| **Sway** | the picture breathes in and out (dizziness) |

The worst of each over all injuries is what shows. **Eye Injury** is the one built for it: a black eye dims its side
(30%), an injured eye darkens it (65%), a **blinded eye** blacks it out; with **both eyes blinded** only a faint
glimmer is left. It also costs critical chance (the aim), and the blinded eye attack speed. Also by themselves:
**blood loss** closes the edges in, redder the lower the health while bleeding, and a **new injury** flashes red at
the edges. Concussion, ear injuries, flu, pneumonia, sepsis, heatstroke, venom and internal bleeding have a Vision
too.

It is drawn with plain UI overlays for the local player only (no post-processing, any render pipeline), and it is
purely visual: what an injury *does* is its stage's effects and consequence. `InjuryController.Vision` gives the
amounts to a script (to mute sounds, to blur a custom camera effect).

---

## 7. Kept between sessions (Injury Settings ▸ Keep Between Sessions)

Players' injuries are saved: logging out with a broken leg and coming back finds it broken. Kept per player in the
world's save (the file the wallets are in), where the rules run (offline, or on the server):

* every part's condition and whether it is **missing**, every injury and illness with its stage, treatment, healing
  and worsening progress, and the immunities;
* written at once after anything that matters (a new injury, a treatment, a lost limb, a respawn) and every 15 s while
  things only heal; a crash loses at most that much healing;
* **time does not pass while the player is away** - a wound neither heals nor festers offline;
* a limb that was missing stays missing, but the limb item is not kept: it cannot be reattached after a restart;
* parts and types are saved by id, so the save survives changes to the settings (what no longer exists is dropped);
* a character that leaves dead comes back healthy when *Clear On Respawn* is on; mobs are never saved.

Off = every session starts healthy. `InjurySaves.Enabled = false` switches it off from code (a game with its own
save system can use `CaptureSave()` / `RestoreSave(bytes)` on the controller instead).

---

## 8. Making your own illness

1. *Injury Settings ▸ Injuries*: add a type. Give it an id, a name, **Systemic** on, *Caused By Hits* off, a
   **Hud Alert** and an *Icon Shape*, tags a medicine can target (`illness` plus your own).
2. Its **stages**, mildest first. Typical: a *Hidden* first stage with *Needs Treatment To Heal* on and *Worsen After
   Seconds* as the incubation; then stages with a **Consequence** (health per second, hunger / thirst drain, stat
   changes, stuns), *Blocks*, a *Vision*, a *Worsen Chance*; *Untreated Heal Rate* above 0 for one that passes by
   itself. *Heal Stage By Stage* makes it ease off stage by stage.
3. **How it is caught**: a complication on another injury, an exposure, an Illness Risk on an item, *Inflict Injury*
   on an ability, or `Expose` from a script.
4. **Its medicine**: a Medical Item whose treatment targets the type or its tag - *Treat Injury* (lets it heal, heal
   rate, stops it worsening) or *Cure Injury* (removes it, or steps it down). Tick **Preventive** for one meant to be
   taken before anything shows.

**Medicines and illnesses that do not show yet.** An ordinary medicine acts on what the player can see: a stomach
remedy used with nothing wrong says *Nothing to treat*. A **Preventive** medicine (antitoxin, rabies vaccine,
antiparasitic) also reaches a hidden stage, and using one - from the hotbar, or dropped on the Health tab's figure -
looks the same whether it found something or not: it is used up at once and says "You take the ...". So a player bitten by a wolf decides for themselves
whether a vaccine is worth it; taking it never reveals whether they were infected.

---

## 9. Scripting

```csharp
InjuryController c = InjuryController.For(player);

// Catch something: 30% (the character's resistances count), starting at its first stage.
Injury caught = c.Expose(InjuryDefaults.Dysentery, 0.3f);

// Give it outright, at a stage.
c.AddInjury(c.WholeBody, c.Settings.FindType(InjuryDefaults.CommonCold), 1);   // a flu

// Immunity.
bool safe = c.IsImmuneTo(c.Settings.FindType(InjuryDefaults.CommonCold));
c.SetImmunity(InjuryDefaults.Tetanus, 3600f);      // vaccinated for an hour

// The body's state and the sight.
bool cold = c.IsExposedTo(ExposureSource.Cold);
float healing = c.BodyHealFactor;                    // 2 asleep, 0.5 starving
InjuryVision sight = c.Vision;                       // left, right, haze, tunnel, sway; sight.Blind

// Falls: a landing after a 14 m drop; what this character can take.
c.Land(14f);
float safe = c.SafeFallHeight;                       // metres, with its Safe Fall Height stat
float own = c.OwnJumpHeight;                         // the highest jump it can make now
float factor = c.FallDamageFactor;                   // 0 = never hurt by a fall
InjuryResistances.For(player).Add(new InjuryResistance("Feather Fall") { fallDamage = -100f }, 1f, "Feather Fall", 10f);

// An explosion with no aimed part, a surgeon's cut.
c.ApplyAreaHit(new DamageInfo { amount = 60f, type = DamageType.Physical, delivery = DamageDelivery.Area });
c.Amputate(c.GetPart("LeftForearm"));

// Let time pass (tests, sleeping through the night, an offline catch-up of your own).
c.Simulate(600f);

// Your own saves.
byte[] state = c.CaptureSave();
c.RestoreSave(state);
```

A resistance to an illness is an ordinary Injury Resistance (page 14) that targets its type or its tag: *Iron
Stomach* = injury chance −60% on the tag `gut`; a race that never catches colds = −100% on `respiratory`.

---

## 10. Multiplayer

Everything here runs where the rules run (the server): exposures, contagion, complications, falls, saves. The
character's own client receives its illnesses with the rest of its injuries and draws the screen effects from them;
other clients receive only what shows on a body (missing limbs, how impaired each part is), never someone else's
illnesses - unless *Share Injuries With Everyone* is on (`NetInjuries`).

Hidden stages are not shown anywhere on the owner's screen, but they are part of the state its client receives: a
player who inspects their own network data could see one. If that matters for your game, keep incubations short or
do not rely on them for secrets.

---

## 11. Tests

*Test Runner ▸ EditMode ▸ InjuryIllnessTests* (18 tests): the defaults have the Whole Body part and every new type;
upgrading an older settings asset keeps its settings; the type lookup follows the list when a script changes it; a
wound heals while its infection clock runs; stitches torn open by a hit can be redone; an area hit is spread over
several parts; illnesses sit on the whole body and hits never land there; an eye injury takes a side and darkens
that half of the sight; immunity after an illness; a severe infection leads to sepsis and antibiotics let it heal;
an incubation is not shown but its medicine finds it; sprains come from crushing hits on a foot and stop sprinting;
gangrene - surgery in time, else amputation; a tourniquet stops an artery on a limb and a splint resets a joint;
falls softened by stats and resistances; which treatment suits an injury best; a save restores the same body; what other players receive is far smaller than
the owner's state.

The Injury Controller's inspector (Play Mode) has **Advance** (let minutes pass at once: watch a cold become a flu)
and **Land** (a landing after a drop of so many metres) to try the rest by hand.

---

## 12. Notes and limitations

* Illnesses use the player's needs for exposures and the body's state; a mob has none, so it catches illnesses only
  from contagion, complications, abilities and scripts.
* Contagion needs an Injury Controller on both characters; it does not go through walls or care about line of sight
  (a radius).
* Time away is not simulated (§7). `Simulate(seconds)` is there if your game wants to.
* Ear injuries are dizziness and a sanity drain: they do not change the game's audio.
* The screen effects are overlays, not a blur shader; a game with post-processing can read `Vision` and drive its
  own.
* Fall injuries are for players (mobs' positions are driven by their navigation).
* A fall is read from the character's height from frame to frame, where the rules run, with the movement's ground
  check when the character has one. The height dropped is exact; only the first moment of a fall and a slowed
  descent are judged by speed.
* A power that holds the character in the air and then ends (Sky Walk running out) lets it fall like anyone: give
  the power a *Buff ▸ Injury Resistance* with Fall Damage −100 for a few seconds if it should not.
