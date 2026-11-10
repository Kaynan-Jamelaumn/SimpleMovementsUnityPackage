# 06 — Breeding, Genetics, Behaviour and the Bond

[← Animals](README.md)

Animals now have families and long lives on the farm. A well kept pair of the owner's own can be bred. Their young are
born (or hatch) in the pen, carry their parents' traits a little either way and sometimes a mutation, and know their
parents and grandparents, so a line can be bred toward better animals generation after generation. Animals sharing a pen
grow close, miss each other when parted, and rival males fret. The bond with the owner grows with care, meals from the
hand and time spent by the pen, and unlocks each species' perks: a cow that lets down her milk without a fuss, a horse
that comes when called. A bond is one part of welfare and never a stand-in for it: a devoted animal can still be sick or
hungry.

* [Breeding](#breeding)
* [Pregnancy, birth and nursing](#pregnancy-birth-and-nursing)
* [Inheritance](#inheritance)
* [Kin](#kin)
* [Relationships between animals](#relationships-between-animals)
* [The bond with the owner](#the-bond-with-the-owner)
* [Behaviour](#behaviour)
* [The window](#the-window)
* [Authoring](#authoring)
* [The rules](#the-rules)
* [Multiplayer: what the server checks](#multiplayer-what-the-server-checks)
* [Scripting](#scripting)
* [Saving](#saving)
* [Tests](#tests)

## Breeding

On the animal's **Family** page, **Breed it with** lists the partners it could be bred with in this pen: the player's own
animals of its kind and of the other sex. Each partner has a **Breed with** button, or the reason it cannot be bred now.
Breeding is a request like any other (`AnimalActionKind.Breed`), checked by the rules:

| Check | Refusal |
|---|---|
| Both in this pen, alive, **both the player's own** | *Only their owner breeds them: both must be yours.* |
| The player may keep animals in this pen | *You cannot keep animals in this pen.* |
| One kind (one species); a female and a male | *They are not of one kind.*, *They are both female.* |
| Not close kin (parent and young, full siblings), unless the rules allow it | *They are too closely related.* |
| Each in a stage of its life that breeds (*Breeding Enabled*: the built-ins' adults) | *Ferdinand is too young.*, *… is past breeding.* |
| Each healthy, fed and kept well enough (the species' *Min Health* 60, *Min Nutrition* 55, *Min Welfare* 50), not starving | *Daisy is too poorly fed to breed.* |
| She not expecting; neither in its cooldown | *Daisy is in calf.*, *Ferdinand is not ready again yet (about 20 hours).* |
| Room in the pen for the largest litter (each young needs its adult space), the young already on the way to its mothers counted | *There is no room here for their young (up to 6, each needing 6 m² grown).* |
| Room in the player's keeping for the largest litter (*Max Animals Per Player*), the young already on the way counted | *You could not keep their young: … young on the way counted.* |
| What breeding takes, carried (a species' *Cost Item* × *Cost Count*; none for the built-ins) | *Breeding them takes 3 grain mix: you have not enough.* |

**The chance.** Whether she conceives is decided once, by a hash of the pair and her number of tries (the same world gives
the same answer, and there is no randomness to fish for):

> chance = Conception (0.75) × her fertility (×0.5 to ×1.5) × his (×0.7 to ×1.3) × her welfare (Excellent ×1.1, Good ×1.05) × age (a senior parent ×0.6) × (1 − 2 × their kinship), at most 0.98

* **It took**: *Daisy and Ferdinand are a pair: Daisy is in calf. If all goes well, a calf in about 14 days.* She is
  expecting, and cannot be bred again until after the birth; he rests his *Sire Cooldown* (a day; half a day for a
  rooster, a drake or a rabbit buck; two days for a stallion).
* **It did not take**: *… it did not take. They may try again in 24 hours.* What breeding took is spent all the same.
* Either way they are **mates** (a bond between them), and the try is in their histories.

## Pregnancy, birth and nursing

| Species | Expecting | Gestation | Young | Cooldown after | Nurse | Birth |
|---|---|---|---|---|---|---|
| Chicken | broody | 72 h | 2-5 chicks | 96 h | no (chicks feed themselves) | hatched a clutch |
| Duck | broody | 84 h | 2-5 ducklings | 96 h | no | hatched a clutch |
| Rabbit | expecting | 48 h | 3-6 kits | 72 h | yes | kindled |
| Sheep | in lamb | 240 h | 1-2 lambs | 336 h | yes | lambed |
| Goat | in kid | 240 h | 1-3 kids | 336 h | yes | kidded |
| Pig | in pig | 192 h | 3-6 piglets | 240 h | yes | farrowed |
| Cow | in calf | 336 h | 1 calf | 480 h | yes | calved |
| Horse | in foal | 480 h | 1 foal | 720 h | yes | foaled |

* **While she carries** she eats and needs 15 % more (*Pregnancy Appetite*). Late on (the last 40 %) everyone can see it:
  *She is heavy with young.*, and her body is broader. Her window says *in calf: due in about 9 days*.
* **She can lose them**: if her health falls below *Miscarry Health Below* (20), the pregnancy ends (*Daisy has lost her
  young.*, her owner is told) and she may be bred again after twice the retry wait.
* **The birth** happens in the step of the simulation her young are due, in her pen (or at the boarding stable, if she is
  boarded then). Each young is a new animal whose id was taken from the world's serial at the mating (so a long catch-up
  gives every young the id a watched stretch would, whatever order the pens are brought up to date in):
  * its owner is its mother's owner;
  * it starts at the first stage of its species' life (a calf a Newborn, a chick a Baby), born that hour;
  * it knows its mother, sire and grandparents, and its generation is one past its elder parent's;
  * mother, young and litter-mates are kin from the first hour.

  Her owner is told: *Daisy has calved: a calf.* / *Henrietta has hatched a clutch: 4 young.* She may be bred again
  after her cooldown. Young can take a pen past its comfortable size: a full pen is crowded, as any.
* **Young on the way count.** Letting out another crate, moving a mother into a pen or breeding another pair counts the
  young already on the way, in the owner's limit and in the pen's room.
* **Nothing is born twice.** A birth happens once and empties the pregnancy (before any young is made). A Breed request retried (the same token) is
  answered from what was done. A save restores the pregnancy as it was, and its young come with the same genes as if
  there had been no save, because they were fixed at the mating.
* **Nursing.** The young of a kind that nurses (all but the birds) suckle from their mother while she is in their pen,
  up to and including the Baby stage. They need no milk replacer, and are as well nourished as she is (her nutrition).
  She eats 15 % more for each young suckling (*Nursing Appetite*). A calf taken from its mother, or whose mother died,
  needs milk replacer in the feeder (or from the hand) until it is weaned.

## Inheritance

Every animal has nine traits (0-1, 0.5 the breed's typical) and its looks:

| Trait | What it does |
|---|---|
| Size | how big it grows (and so how much it eats) |
| Growth | how fast it grows |
| Thrift (feed efficiency) | more from the same food |
| Hardiness (resistance) | hardship, toxins and illness |
| Fertility | the chance to conceive, and the size of a litter |
| Temper (temperament) | how much handling and crowding stress it, how readily it shies from strangers |
| Produce quality and yield | what it gives (chapter 05) |
| Longevity | how long it lives |
| Coat, pattern, a rare trait | how it looks; a rare trait makes its Premium products Special (chapter 05) |

A young's traits come from its parents:

* **each trait** is its parents' mean, a little either way (*Inherit Spread*: a normal spread of 0.06);
* **a mutation**: with *Mutation Chance* (4 %) a trait strays further (a normal spread of *Mutation Size*, 0.2). Both are
  settings, so a world can be made steadier or wilder;
* **its coat** is one parent's (one its breed has) and its **pattern** one parent's (sometimes a new one, at the mutation
  chance);
* **a rare trait** passes to half the young of a parent that carries one (*Rare Inherit Chance*); rarely (*Rare Mutation
  Chance*, 2 %) a young carries one neither parent had;
* **its breed** is its mother's or, for a cross, its sire's, half the time each;
* **inbreeding** weakens it: its hardiness, fertility and longevity are lessened by *Inbreeding Penalty* (0.1) × its
  parents' kinship × 4 (0.1 for half siblings, 0.2 for full ones).

So a keeper who breeds their most fertile, hardy, docile animals with the finest produce, generation after generation,
breeds a better herd. Its owner sees an animal's traits in words on its Family page (*Fertility: good*, *Temper: docile*,
*It carries a rare trait.*, *Inbred: its parents were close kin.*), never as numbers.

## Kin

Each animal knows its mother, sire and four grandparents (an animal bought knows none). From them:

| Kinship | Who |
|---|---|
| 0.25 | parent and young; full siblings |
| 0.125 | half siblings; grandparent and grandchild; aunt or uncle and nephew or niece |
| 0.0625 | first cousins (two shared grandparents) |
| 0.03125 | one shared grandparent |

Close kin (0.25) are not bred unless *Allow Close Kin* is on. Kin conceive less readily (× (1 − 2 × kinship)), and their
young are weakened (above). The window notes a partner who is *related: their young may be weaker*.

## Relationships between animals

Each animal keeps its bonds with others, each a level 0-100. Of companions it keeps at most *Keep Bonds* (8): in a crowded
pen it grows close to the first it gets to know, and makes room for a new one only by letting go of one it no longer
lives with. Kin and rivals are always kept.

| Bond | How it comes |
|---|---|

| **Companion** | two animals in one pen grow used to each other: +4 a game day with one of their kind, +1 with another kind |
| **Kin** | a mother and her young (80), litter-mates (60), at birth |
| **Mate** | a bred pair (20) |
| **Rival** | two grown males of a kind whose males are rivals (bulls, rams, bucks, boars, roosters, drakes, stallions) in one pen: their rivalry grows +10 a day, from nothing (two raised as companions are not fierce rivals the day they are grown) |

Apart, a bond fades 2 a day (kin 1). What company does, each step:

* **close companions near** (bonds from *Close From*, 50) lift its spirits (up to +4 happiness) and calm it (up to −4 stress);
* **a close companion just parted from** (moved, boarded, sold, dead) is missed: up to +8 stress, fading over 72 hours,
  and its owner is told once: *Bella seems to miss Daisy.* At the boarding stable an owner's animals are together (a
  mother and the young born there are not parted);
* **a fierce rival in the pen** stresses it (up to +8).

The Family page tells an animal's companions in words: *Close to Bella*, *Misses Daisy (kin)*, *Paired with Ferdinand*,
*A fierce rival of Brutus*. Bodies keep with their closest companion now and then (see [Behaviour](#behaviour)).

## The bond with the owner

An animal's **affinity** with its owner (0-100) grows by:

* **care** (petting most, brushing, washing, hoof care) and **medicine**, and **meals from the hand**: together at most
  *Affinity Per Day* (8) a game day (chapter 04);
* **time together**: while its owner stands within *Near Metres* (8 m) of its pen's fence, it gains *Together Per Hour*
  (0.5) a game hour, at most *Together Per Day* (2) a game day. The server measures where the owner really stands.

It fades (0.5 a day) once its owner has neither cared for it nor spent time near it for two days (*Affinity Neglect
Hours*). It is a tenth of welfare (its weight in the welfare formula, chapter 04): a bond lifts a well kept animal, and
cannot save a neglected one.

**What a bond brings:**

* **Easier handling**: care, milking and shearing stress it less as its bond with its owner grows (× (1 − affinity/100):
  a trusting animal is easier for anyone), and its owner's not at all once it is calm with them (*Calm Handling*).
* **Friendlier animals**: it greets its owner and follows them along the fence (below).
* **Each species' perks**, unlocked at a bond:

| Species | Perks (bond needed) |
|---|---|
| Chicken | Comes running (40), Follows you about (60), Devoted (90) |
| Duck | Quacks a welcome (40), Waddles after you (60), Devoted (90) |
| Rabbit | Hops to the fence (40), Lets you hold it: calm handling (70), Devoted (90) |
| Sheep | Stands quiet for shearing: calm handling (60), Follows you like a lamb (70), Devoted (90) |
| Goat | Comes to see what you bring (30), Trots after you (50), Stands for milking: calm handling (60), Devoted (90) |
| Pig | Grunts a welcome (40), Follows you about (60), Devoted (90) |
| Cow | Lows a welcome (40), Lets down her milk for you: calm handling (60), Follows you to the gate (75), Devoted (90) |
| Horse | Whickers when it sees you (30), Comes when you call (50), Trusts your hands: calm handling (60), Follows you along the fence (70), Devoted (90) |

*Devoted* is a cosmetic mark: the animal wears a red ribbon, plain for everyone to see. *Comes when you call* adds **Call
it** to the Family page: the horse comes to the fence where its owner stands.

## Behaviour

Bodies only show the animals, near players; the simulation goes on for all of them as data (nothing is moved or steered
for animals nobody is near). Each species has a **behaviour profile**:

| Species | Sociability | Grooming | Flightiness | Curiosity | Herds |
|---|---|---|---|---|---|
| Chicken | 0.6 | 0.4 | 0.6 | 0.4 | yes |
| Duck | 0.7 | 0.5 | 0.5 | 0.3 | yes |
| Rabbit | 0.5 | 0.6 | 0.8 | 0.2 | no |
| Sheep | 0.8 | 0.1 | 0.6 | 0.2 | yes |
| Goat | 0.6 | 0.2 | 0.3 | 0.8 | yes |
| Pig | 0.6 | 0.1 | 0.3 | 0.6 | no |
| Cow | 0.7 | 0.3 | 0.4 | 0.4 | yes |
| Horse | 0.6 | 0.3 | 0.5 | 0.5 | yes |

What a body does, by the needs and looks its picture shows (Phases 1-2) and now its ways:

| Behaviour | When |
|---|---|
| Idle, Eating, Drinking, Sleeping, Walking | its needs and the hour (Phase 1) |
| Grazing | in a pasture |
| Sick | lies down by day, mopes head low, walks slowly, coughs (Phase 2) |
| Stressed | paces with short pauses (Phase 2) |
| **Grooming** | idle, now and then (its kind's *Grooming*): preening, licking, dust-bathing |
| **Socialising** | it walks to its closest companion in the pen and they nuzzle or peck (its kind's *Sociability*) |
| **Herding** | a herding kind drifts toward the others |
| **Following its mother** | a young (below Adolescent) keeps close to its mother when she is in the pen |
| **Shying** | a stranger within 4 m makes an animal of a flighty kind move off (*Flightiness*; a shy one, of a skittish temper, readily, a steady one seldom) |
| **Curiosity** | it comes to the fence to look at someone there (its kind's *Curiosity*) |
| **Greeting** | its owner coming within 6 m: it comes over with a hop and a nod and a contented call (with a greeting perk), at most every 40 s |
| **Following** | its owner near the pen: it keeps to their side of the fence (with a following perk) |
| **Coming when called** | its owner's *Call it* (with the perk) |

Every behaviour stays inside the pen's fence. Who an animal's owner is only its owner's machine knows: on other
machines it treats everyone alike (it may shy from its own owner there).

## The window

The pen window has a fifth tab, **Family**:

* its **bond** with its owner (a gauge and a word: *a stranger to you*, *wary*, *it knows you*, *friendly*, *devoted*) and the perks it
  has brought, for its owner;
* *in calf: due in about 9 days*;
* its parents (*Out of Daisy by Ferdinand.*), its generation on the farm and the young it has had;
* its companions in words;
* what its owner knows of its breeding (its traits in words);
* **Breed it with**: its partners here, each with *Breed with …* or why not, and the reason it cannot be bred now;
* **Call it**, for one that comes when called.

The Overview shows the bond gauge and whether it is expecting. **Look closely** tells its owner when it is expecting and
where it was bred (*Bred here: out of Daisy by Ferdinand (generation 1).*). In Play Mode the Animal Pen inspector
shows each animal's affinity, generation, young, pregnancy (litter, hours to go, kinship) or readiness, and its bonds,
with three debug buttons: **Due now** (every mother expecting is due at the next step), **Bond +25** and **Ready to
breed**.

## Authoring

A species asset's **Breeding**:

| Field | What |
|---|---|
| Enabled | its animals breed at all |
| Gestation Hours | mating to birth (or hatching) |
| Litter Min / Max | young in a birth (a fertile mother leans to more) |
| Cooldown Hours / Sire Cooldown Hours | before the mother, and the sire, may breed again |
| Conception | the chance a typical, well kept pair conceive |
| Min Health / Min Nutrition / Min Welfare | what a parent needs |
| Cost Item / Cost Count | an item breeding takes from the player (an item key: `animal:feed/grain_mix`; empty: none) |
| Nurses / Nurses Through | its young suckle from their mother up to and including this stage |
| Males Rival | grown males of its kind are rivals |
| Birth Verb / Expecting Word | the words (*calved*, *in calf*) |

Its **Behaviour**: *Sociability*, *Grooming*, *Flightiness*, *Curiosity* (0-1), *Herds*, *Young Follow Mother*, *Notice
Range* (metres from the fence it notices people within). Its **Perks**: each an *Id*, *Name*, *Description*, *Kind*
(Follow, Greet, Calm Handling, Come When Called, Ribbon) and the *Min Affinity* it unlocks at. Each stage of its life has
*Breeding Enabled*.

**Old species assets.** A species asset made before Phase 4 (*Data Version* below 4) takes its breeding, behaviour and
perks from the built-in species of its id until it is updated (**Animals ▸ Update Species Assets To Phase 5**, which also
fills in what a Phase 1, 2 or 3 asset lacks).

**Validation** checks: a gestation under an hour, Litter Max below Litter Min, a Conception of 0, a cost with no item or
an item that is none, a species that breeds in no stage of its life, perks without an id or two of one id, and the
breeding and social rules.

## The rules

**Animal Rules ▸ Breeding:**

| Setting | Default | What |
|---|---|---|
| Enabled | on | breeding at all |
| Inherit Spread | 0.06 | how far a young's trait strays from its parents' mean |
| Mutation Chance / Mutation Size | 0.04 / 0.2 | per trait |
| Rare Inherit Chance / Rare Mutation Chance | 0.5 / 0.02 | a rare trait passed on / new |
| Allow Close Kin | off | parent and young, full siblings may be bred |
| Inbreeding Penalty | 0.1 | × kinship × 4 off hardiness, fertility, longevity |
| Retry Hours | 24 | before a pair whose mating did not take may try again |
| Fertility Spread | 0.5 | how much fertility moves the chance (the sire's at 60 %) |
| Pregnancy Appetite / Nursing Appetite | ×1.15 / +0.15 a young | what a mother eats and needs |
| Miscarry Health Below | 20 | a mother below loses her young |

**Animal Rules ▸ Social:**

| Setting | Default | What |
|---|---|---|
| Enabled | on | relationships and time together at all |
| Bond Per Day / Other Kind Bond Per Day / Bond Fade Per Day | 4 / 1 / 2 | companions |
| Close From / Keep Bonds | 50 / 8 | |
| Kin At Birth / Littermates At Birth / Mate Bond | 80 / 60 / 20 | |
| Companion Happiness / Companion Calm | 4 / 4 | close companions near |
| Miss Stress / Miss Hours | 8 / 72 | a close companion parted from |
| Rival Stress / Rivalry Per Day | 8 / 10 | rival males |
| Together Per Hour / Together Per Day / Near Metres | 0.5 / 2 / 8 m | the owner's time by the pen |
| Handling Relief | 1 | how much a bond eases its owner's handling |

## Multiplayer: what the server checks

* **Breeding** is a request through the relay, checked like any other: the player from the connection, the rate, a
  well-formed request (the partner's id in its text), the pen within reach, and then every check above, on the server,
  with the server's own records (rights, ownership, readiness, kinship, room, the player's own items for the cost).
  The roll is the server's. Nothing of the genes is decided on a client.
* **Births** happen only in the server's simulation; the young's ids come from the server's world serial.
* **Time together** counts only where the server sees the owner standing.
* **What a client is shown**: its own animals' bond (a coarse level and a word), perks, traits in words, partners and
  readiness, family and companions; to carers, family, pregnancy and companions; to everyone, how its body behaves
  (shy of people or not, its mother and closest companion here, a ribbon, heavy with young). Never a gene, an affinity
  or a bond as a number.
* **Owners are told** of a birth, a loss and a companion missed, as of their animals' other news (and, since this phase,
  of a full nest and someone else's collecting: chapter 05).

## Scripting

```csharp
AnimalSpeciesData cow = AnimalDatabase.Instance.Species("cow");
bool ready = AnimalBreeding.Ready(daisy, cow, rules, now, out string why);
bool pair = AnimalBreeding.Compatible(daisy, ferdinand, cow, rules, out AnimalRecord mother, out AnimalRecord sire, out why);
float kin = AnimalBreeding.Kinship(daisy, ferdinand);
float chance = AnimalBreeding.ConceptionChance(mother, sire, cow, rules);
AnimalGenes young = AnimalBreeding.Inherit(mother.genes, sire.genes, seed, rules.breeding, cow.Breed("holstein"), kin);

// Breeding (through the director, like every request)
pen.Perform(new AnimalRequest { kind = AnimalActionKind.Breed, animal = daisy.id, text = ferdinand.id }, player);

// The bond and relationships
AnimalSocial.TimeTogether(animal, hours, now, rules);       // what the director does for owners near
bool calm = AnimalSocial.HasPerk(animal, cow, AnimalPerkKind.CalmHandling);
AnimalBond b = AnimalSocial.BondWith(bella, daisy.id);       // kind, level, lastTogether
```

The notices add `Mated`, `GaveBirth`, `Miscarried` and `MissesCompanion` (and the young's history `Born`).

## Saving

The save is **version 4**. Each animal record adds its parents and their names, its grandparents, its generation, its
pregnancy (the sire, his genes at the mating, the kinship, when conceived and due, the litter, its seed and the young's
ids), when it may breed again, its tries, its young, its bonds, when its owner was last near it, and the affinity time
together gave it today. A save of version 1, 2 or 3 loads with no family, no pregnancy and no bonds yet; it is written
back as version 4.

## Tests

`AnimalBreedingTests` (23, Edit Mode):

* every built-in species breeds, behaves and bonds in its own way, and its content and the rules are sound;
* a well kept pair have a calf that knows its parents, its generation, its owner and its first stage; mother and calf are
  kin; the cooldowns; her owner is told; a birth is saved soon; nothing is born twice;
* only a compatible, ready pair of the owner's own may be bred (both female, two kinds, another's animal, too young,
  rights, poorly fed, kept badly, unwell, in a cooldown, no room for the young, the owner's limit, the cost, and nothing
  done when the cost did not come);
* close kin are not bred unless the rules allow it; kinship for parent and young, full and half siblings, grandparents,
  uncles, cousins and strangers;
* the chance to conceive follows fertility, welfare, age and kin;
* the young inherit their parents' mean, near it with the spread, deterministically, with configurable mutations; rare
  traits pass on or appear; coats within the breed; inbreeding weakens;
* a clutch of chicks hatches whole, each with its own id and genes, litter-mates kin;
* a calf suckles from its mother (and its mother eats more) and goes hungry without her or milk replacer;
* a mother in poor health loses her young, and her owner is told;
* births in a catch-up are the births of a watched stretch (the same ids, genes and lives);
* a pregnancy is saved, and the young born after a load are the same;
* pen-mates grow close, miss each other when parted (told once), and the bond fades apart;
* two bulls in a pen are rivals and fret;
* time together deepens the bond (at most its daily share) and keeps it from fading;
* a deep bond unlocks the perks and calms its owner's milking, and never stands in for welfare;
* its owner sees its family, bond, traits and partners, and others only its ways; no number of the bond or the genes is
  sent; a mother late in carrying shows it to everyone;
* a record of an older save loads with no family yet;
* and the cases of the review: young on the way count in the owner's limit and the pen's room; the young's ids are fixed
  at the mating, whatever order the pens are brought up to date in; time together never makes a day of history ahead of
  the simulation and keeps its daily share on the record; a new rival is noticed in a crowded pen, the bonds an animal has
  are kept rather than made and dropped every step, and old company does not make a rivalry fierce at once; young born at
  the boarding stable are with their mother; a look tells a stranger nothing of a pregnancy; breeding takes its cost whole.
  Each fails with its bug put back (ten bugs, each caught).

**Manual Play Mode checks:**

1. A pasture on a property; **Give Me A Keeper's Starter Kit**; let out a cow and buy a second crate until you have a bull
   (or let out two crates of chickens for a hen and a rooster).
2. Keep them well a day (or **+1 day** in the inspector). On the cow's **Family** page, *Breed with* the bull. If it did not
   take, try again after a day (**Ready to breed** skips the wait).
3. **Due now** in the inspector, then **+6 h**: the calf is born in the pen, its owner is told, it follows its mother and
   suckles (no milk replacer needed). Its Family page names its parents.
4. **Bond +25** a few times: the cow greets you with a hop, follows you along the fence, her milking no longer stresses
   her; at 90 she wears a ribbon. With a horse, *Call it*.
5. Move one of two close sheep to another pen: the other one is told to miss it.
6. Quit and play again with a cow expecting: she is still expecting, and her calf comes when due.
7. Online, host plus a client: the client breeds its own pair; the birth happens on the server; the client sees its calf,
  its bond and its traits in words; another client sees neither.
