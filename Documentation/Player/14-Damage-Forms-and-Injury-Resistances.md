# 14 — Damage Forms & Injury Resistances

**Scripts:** `Essentials/Ability/Core/DamageForms.cs` (`DamageForm`, `DamageFormMask`, `DamageFormChances`, `DamageForms`),
`Essentials/Combat/Injuries/InjuryResistances.cs` (`InjuryResistance`, the `InjuryResistances` component),
`Essentials/Combat/Injuries/InjuryResistanceEffects.cs` (trait behaviour, equipment effect, ability buff),
`Player/RPGSystem/Trait/TraitStats.cs` (the injury character stats), `Player/RPGSystem/Trait/TraitPresets.cs` (Tough Skin,
Strong Bones...), `Essentials/Combat/Injuries/InjuryDefaults.cs` (odds by form, new injuries, creature presets),
tests in `CustomEditor/Combat/DamageFormTests.cs`.

Every hit now says **what it is meant to do to a body**: a sword slashes, a spear pierces, a mace bludgeons, a wolf
bites, a bear claws, a troll's stomp crushes. The injury system reads it: **a slash is far more likely to make a
bleeding cut than a blunt blow — not impossible, much less likely** — blunt blows bruise, break bones and concuss,
points make deep punctures that get infected, bites tear. And characters can **resist** each form and each kind of
injury: by race, class, trait, item or buff (Tough Skin, Strong Bones, Thick Blood, a skeleton that never bleeds...).

The form works with the injury system **off** too: the Slash / Pierce / Blunt Damage Taken stats and the resistances'
*Damage Taken* change the damage of hits of that form.

---

## 1. The damage forms

| Form | Meant for | Injuries it favours |
|---|---|---|
| **Slash** | swords, axes, greatswords, a blade trap | lacerations, deep wounds, scratches; **severs limbs** |
| **Pierce** | spears, daggers' stabs, arrows, bolts, horns, stingers, spikes | **puncture wounds** (infect quickly), deep wounds |
| **Blunt** | maces, hammers, staves, fists, shields, pushes, kicks, a ground slam's shockwave | **bruises**, **fractures**, **concussions** (head); rarely a cut |
| **Bite** | teeth | **bite wounds** (tear, bleed, infect fast), lacerations |
| **Claw** | claws, talons, a scratch attack | scratches and lacerations (many shallow cuts) |
| **Crush** | a giant's stomp, a falling rock, a crushing grip, a hard fall (chasms) | fractures (worse than blunt), bruises; can crush a hand or a foot off |
| *(None)* | spells, fire, ice, poison; weapons without a category, abilities and scripts that set none | element-driven injuries (burns, frostbite); unknown physical hits keep the old odds |

Forms are not elements: a flaming sword slashes **and** burns; a frost arrow pierces **and** can give frostbite.

### Where the form comes from

The first one set wins:

1. **The hit itself** (`DamageInfo.form`) — set by every source below.
2. **Ammo** (*Ammo ▸ Damage Form*): blunt arrows and sling stones = Blunt, broadheads = Slash. None = the attack's.
3. **The attack** (*Weapon ▸ Attacks ▸ Damage ▸ Damage Form*): a sword's thrust = Pierce, its pommel strike = Blunt,
   a shield bash = Blunt. None = the weapon's.
4. **The ground slam** (*Attack ▸ Impact ▸ Damage Form*, Blunt by default; Crush for a giant hammer).
5. **The weapon** (*Weapon ▸ Damage & Stats ▸ Damage Form*). None = **from its Weapon Category**: Sword, Greatsword,
   Axe → Slash; Spear, Dagger, Bow, Crossbow, Thrown → Pierce; Mace, Hammer, Staff, Fist, Shield → Blunt; Wand, Tool,
   Other → none (set it on the weapon). The inspector shows what the category gives ("→ Slash (from the category)"),
   and the item tooltip says it ("Sword: 8-12 slashing damage").
6. **Abilities**: the *Damage* effect has a **Form** field — a bite ability = Bite, a scratch = Claw, a shove or slam =
   Blunt, a spear throw = Pierce, a fireball = None. Its tooltip says it ("12 biting damage").
7. **Mobs' automatic basic attack**: *Mob ▸ Automatic Basic Attack ▸ Attack Form* (Bite by default: wolves, rats; Claw
   for bears and cats, Blunt for golems and charges, Pierce for horns and stingers, Crush for giants).
8. **Traps and hazards**: blade traps slash, dart traps and spike floors pierce, falling into a chasm crushes;
   *Dungeon Hazard ▸ Form* for your own.

`CombatEntity.ApplyDamage` resolves the form once at the start (`DamageForms.Resolve`: the hit's, else its weapon's), so
every resistance, interceptor and the injury system see the same form. Scripts: `new DamageInfo { ..., form = DamageForm.Bite }`.

---

## 2. Injuries by form

Every injury type's trigger has two rows of numbers, one per form (*Injury Settings ▸ Injuries ▸ a type ▸ Trigger ▸ By
Damage Form*):

* **Form Chances** — multiplies the type's *Chance* for hits of that form (0 = never from that form, 1 = unchanged).
* **Form Strength** — how hard a hit of that form counts for it (multiplies the hit's fraction for *Min Hit Fraction*
  and the stage): a crushing blow breaks a bone worse than a slash of the same damage.

The defaults (formless · slash · pierce · blunt · bite · claw · crush):

| Injury | Chances by form | Notes |
|---|---|---|
| **Scratch** | 1 · 1.2 · 0.6 · **0.15** · 0.8 · **1.8** · 0.2 | claws scratch |
| **Laceration** | 1 · **1.5** · 0.5 · **0.15** · 1.2 · 1.4 · 0.3 | the bleeding cut: slashes, rarely a mace |
| **Deep Wound** | 1 · 1.3 · 0.7 · **0.05** · 1 · 0.8 · 0.2 | |
| **Puncture Wound** *(new)* | 0.3 · 0.15 · **1.6** · 0 · 0.5 · 0.2 · 0 | bleeds less, infects in 7 min (deep: 5 min) |
| **Bite Wound** *(new)* | only bites (1) | tears and bleeds; infects in 4 min; *Mauled* from 30% hits |
| **Bruise** *(new)* | 0.5 · 0.1 · 0.1 · **1.6** · 0.4 · 0.1 · 1.5 | no bleeding, heals by itself (deep bruise from 20%) |
| **Fracture** | 1 · 0.3 · 0.25 · **1.6** · 0.4 · 0.15 · **2** | strength: blunt ×1.2, crush ×1.5, slash ×0.8 |
| **Concussion** *(new)* | head only, 0.5 · 0.1 · 0.1 · **1.5** · 0.1 · 0.1 · 1.5 | dizzy spells (short stuns), heals with rest |
| **Frostbite** *(new)* | Ice hits (element) | frostnip heals alone; frostbite needs treatment and rots |
| Burn | Fire hits (element) | unchanged |

What a hit does, on average (a forearm, the default settings — from a simulation of these rules):

| A hit of 15% of the part's condition | Ends with a bleeding injury | Mostly |
|---|---|---|
| Slash | **92%** | laceration 69%, scratch 15% |
| Pierce | 90% | puncture 80% |
| Blunt | **15%** | bruise 75%, a cut now and then |
| Bite | 95% | bite wound 70% |
| Claw | ~100% | laceration 63%, scratch 27% |
| Crush | 9% | fracture 60%, bruise 25% |

At 35% a blunt hit breaks the bone about half the time; a slash makes a deep wound about half the time.

**Formless** physical hits — a weapon without a category, an older ability or script that sets no form — keep each
injury's usual chance (formless odds 1 on the cuts and fractures), so existing content behaves as before; give them a
form to get the new behaviour.

The **order** of the list still matters (*Max Injuries Per Hit*, 1 by default): the defaults try Burn, Frostbite,
Concussion, Fracture, Deep Wound, Bite Wound, Puncture, Laceration, Scratch, then Bruise (a blow that broke nothing
still bruises).

### Limb loss by form

*Body Parts ▸ a limb ▸ Limb Loss ▸ Form Chances* multiplies the limb's severing *Chance*: slash 1, bite 0.6, crush 0.5,
claw 0.4, pierce 0.15, **blunt 0.1**, formless 1. Swords and axes take limbs; maces almost never (a crushing blow can
still take a hand or a foot).

### Existing settings assets

Assets made before this update keep working, with the same odds for every form. *Injury Settings ▸ Injuries* shows
**Apply Damage Form Defaults**: it gives the built-in injuries their odds by form, adds the new injuries in their place
in the list, and the limbs their severing odds — and keeps everything else you changed. (*Reset Injury Types To
Defaults* replaces the whole list instead.) Treatments: the Suture Kit now stitches punctures and bite wounds, Surgery
also treats deep punctures, mauling and frostbite; bandages, disinfectant and burn salve find the new injuries by their
tags (wound, bleeding, burn).

---

## 3. Resistances

### 3a. Character stats (the simple way)

Eleven new **character stats** (the same `TraitModifier` lists traits use), so they work everywhere stats do: a
**trait's** modifiers, a **race's or class's** *Character Stats* (and per level), an **item's** *Character Stats*
effect, an **armour set** bonus, a **Stat Buff** ability effect, a status consequence. Percent; negative = better,
except Injury Healing.

| Stat | Changes |
|---|---|
| **Slash / Pierce / Blunt Damage Taken** | health damage of hits of that form — claws count as slashes, bites as piercing, crushing as blunt (`CombatStats`, with the injury system on or off) |
| **Injury Chance** | the chance of every injury from hits |
| **Wound Chance** | scratches, cuts, deep wounds, punctures, bites (tag *wound*): **Tough Skin** |
| **Fracture Chance** | broken bones (tag *fracture*): **Strong Bones** |
| **Burn Chance** | burns and frostbite (tag *burn*) |
| **Infection Chance** | negative: an untreated wound may be **fought off** when it would get infected (its clock starts over: "Your body fought off an infection"); positive: it gets infected sooner |
| **Bleeding** | how fast wounds bleed: **Thick Blood** / Hemophiliac |
| **Injury Healing** | how fast injuries heal and clot: **Fast Healer** |
| **Limb Loss Chance** | the chance of losing a limb: sturdy limbs / Brittle Bones |

They are grouped under *Injuries* (and *Health & Defence*) in the trait inspector's stat menu. Like every percent
character stat they stack by multiplying and stop at -95%: for a true immunity use an Injury Resistance of -100 (below).

### 3b. Injury Resistances (the detailed way)

An **Injury Resistance** says exactly what it covers and what it changes:

| Field | Meaning |
|---|---|
| *Label* | its name in the Health tab ("Tough Skin") |
| *Injuries* | injury ids or tags it applies to (`wound`, `fracture`, `burn`, `infection`, `bruise`, `concussion`, `bite`, `Scratch`...); empty = every injury |
| *Forms* | only hits of these forms (Slash, Pierce, Blunt, Bite, Claw, Crush, Formless) |
| *Part Kinds* | only on these parts (Head for a thick skull); empty = every part |
| *Injury Chance* | -40 = 40% less likely, **-100 = immune**, +50 = a weakness |
| *Severity* | the hit counts softer (negative) for the stage: -40 = a cracked bone instead of a broken one |
| *Worsening* | the chance it gets worse untreated (or that a wound turns into it: infections); -100 = never |
| *Bleeding* | how fast the injuries above bleed; -100 = never bleeds |
| *Healing* | how fast they heal and clot |
| *Limb Loss* | the chance of losing a limb (to the forms above, on the parts above) |
| *Damage Taken* | health damage of hits of the forms above (+50 = a weakness: a skeleton against maces) |

Where to put them:

| Who | How |
|---|---|
| **A trait** | add the behaviour *Passive ▸ Injury Resistances* (Trait ▸ Behaviours) |
| **A race or a class** | an innate trait with it, or *Effects ▸ Stats ▸ Injury Resistances* on the race / class asset |
| **An item** (armour, a ring, a weapon while held), **an armour set** | *Equip Effects ▸ Stats ▸ Injury Resistances* |
| **An ability** (Stone Skin, Iron Bones) | the effect *Buff ▸ Injury Resistance* (*Duration*; casting it again refreshes it) |
| **A mob / NPC prefab** | the **Injury Resistances** component, its own list; *Add Creature Preset...* — Skeleton, Stone Golem, Troll, Slime, Armoured Beast, Undead |
| **A script** | `InjuryResistances.For(character).Add(list, strength, "source", seconds)` returns a token; `Remove(token)` |

Resistances of the same thing multiply (two -50% = -75%). Equipment strength (set bonuses, enhancements) scales them.

### 3c. Ready-made traits

*Assets ▸ Create ▸ SimpleMovements ▸ Character ▸ Trait From Preset ▸ Injuries*:

| Trait | Gives |
|---|---|
| **Tough Skin** | -40% wound chance, -10% slash damage |
| **Strong Bones** | -40% fracture chance, fractures 35% milder, -10% blunt damage |
| **Thick Blood** | -40% bleeding |
| **Fast Healer** | +50% injury healing |
| **Iron Constitution** | -60% infection chance |
| **Thick Skull** | -60% concussions, head injuries 30% milder |
| **Scaled Hide** | -25% slash damage, +15% blunt damage, -50% scratches and lacerations from slashes and claws |
| *Hemophiliac* (drawback) | +60% bleeding, -30% injury healing |
| *Brittle Bones* (drawback) | +50% fracture chance, +20% limb loss chance |
| *Thin Skin* (drawback) | +40% wound chance |
| *Sickly* (drawback) | +60% infection chance |

### Creature presets (Injury Resistances component)

| Preset | Does |
|---|---|
| **Skeleton** | never cut, bruised, infected or bleeding; -40% damage from slashes, piercing, bites, claws; maces and crushing +50% damage and +60% fractures |
| **Stone Golem** | no wounds, bruises, burns, concussions or bleeding; -60% damage and -80% limb loss from blades, points, teeth, claws; +25% blunt damage |
| **Troll** | injuries heal 4× faster, bleed half, never worsen; burns +50% and heal slower |
| **Slime** | no fractures, concussions or limb loss; -50% blunt damage; cuts barely bleed and close fast |
| **Armoured Beast** | -60% wounds from slashes, claws and bites; -30% slash and claw damage |
| **Undead** | bleeds 70% less, never infected or concussed, barely heals |

---

## 4. In the game

* **Health tab**: an injury's details say what caused it ("Caused by a slashing hit."); the summary lists the
  character's resistances ("Resistances: Tough Skin, -40% Bleeding"). Bleeding and healing times include them.
* **Injury Controller inspector** (Play Mode): the test hit has a form (try 20 damage Slash vs Blunt on an arm), and the
  resistances in effect are listed.
* **Multiplayer**: forms and resistances are applied where the rules run (the server); the injury snapshot carries each
  injury's form, bleeding and healing factors to the clients (snapshot version 3; version 2 is still read).

## 5. Scripting

```csharp
// A custom hit: a troll's club.
target.ApplyDamage(new DamageInfo { amount = 30f, source = troll, type = DamageType.Physical, form = DamageForm.Crush, point = hitPoint });

// What form a hit has (its own, else its weapon's).
DamageForm form = DamageForms.Resolve(info);

// A temporary resistance: 10 s of no bleeding.
InjuryResistances.For(player).Add(new InjuryResistance("Coagulant") { bleeding = -100f }, 1f, "Coagulant", 10f);

// How likely a laceration is for this character from a slash on the left arm right now.
InjuryController c = InjuryController.For(player);
float p = c.ChanceOf(c.Settings.FindType(InjuryDefaults.Laceration), c.GetPart("LeftArm"), DamageForm.Slash);
```

## 6. Tests

*Test Runner ▸ EditMode ▸ DamageFormTests*: forms from weapon categories and attacks, families and masks; the default
odds (slashes cut, blunt breaks, only teeth bite); 300 slash vs 300 blunt hits (slashes bleed far more often, blunt
blows still sometimes cut); blunt hits bruise and remember their form; limb loss by form and with a -100% resistance;
immunity, form-limited resistances (removed exactly), milder fractures, bleeding and healing factors, damage taken by
form; the skeleton preset; the injury stats; the new default injuries; upgrading an older settings asset; the snapshot.
