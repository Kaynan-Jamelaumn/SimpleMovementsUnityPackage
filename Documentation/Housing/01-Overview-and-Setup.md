# Housing 01 — Overview and Setup

[Back to the Housing guide](README.md)

## Five-minute setup

All menus are under **Tools ▸ SimpleMovements ▸ Housing** (and **GameObject ▸ SimpleMovements ▸ Housing** in the
Hierarchy's right-click menu). Every step is undoable.

1. **Settings (optional).** *Create Housing Settings* makes `Resources/SimpleMovements/Settings/HousingSettings`
   (also in *Project Setup*). Without it the built-in rules are used. Pick the **Currency** rents are paid in (empty:
   the project's default currency). In a network session it must be money that is **kept between sessions** (a
   number currency with *Keep Between Sessions*): the housing office refuses a currency that comes back every session,
   since it would buy houses for free.
2. **A house.** *Create Property* puts a 16 × 12 m plot at the Scene view's pivot, on the ground, with an 8 × 6 m
   placeholder house, its front door, a chest, a bed and a 6 × 4 m garden. Or select your own house model and
   *Create Property Over Selected Object*: the house becomes the model's footprint (turned with it, its floors from its
   height), the land the house with 2 m around it, and the model becomes the property's child (so it is its house, not
   something standing on its land). A model inside a prefab is offered an unpack; kept in the prefab, it goes in the
   property's *Parts* and its doors, chests, beds and gardens are listed. Each Door Controller of the model gets a
   Lockable Door. Shape it in the Property inspector — the **Land**, **House** and **Zones** tools
   ([chapter 02](02-Placing-Houses-and-Inns.md)).
3. **A realtor.** *Create Realtor NPC* makes an NPC with the **Property** option: it lets every property of the city
   (or only its *Listings*, or one *District*). Keep its *Max Players At Once* at 0 so it serves everyone.
4. **An inn.** *Create Inn* makes a building area with two rooms; *Add Room To Selected Inn* (or **+ Room** in its
   inspector) adds more — each a 4 × 4 m room with a bed, a chest and a door, on a free spot whose door opens onto a
   corridor reached from the entrance. *Create Innkeeper NPC* (with the inn selected) puts an innkeeper inside it, with
   the **Rent a room** option.
5. **Check.** *Validate Housing* reports overlapping areas, objects standing on a plot, houses off their land, rooms
   without a bed or a chest, chests that would lose what tenants keep in them, things two places claim, duplicated ids
   — in the Console; the **Project Validator** shows the same with a fix button where there is one.
6. **Multiplayer.** The bootstrap scene's "Network World" object carries the global relays (*Tools ▸ SimpleMovements ▸
   Network ▸ Create Bootstrap Scene*; for an existing scene *Add Missing Relays To Network World*), `NetHousing` among
   them. Gardens on properties need a `NetworkObject` and a `NetSoilField`: select the properties (or their gardens) and
   *Housing ▸ Make Selected Gardens Networked*.
7. **Play.** Talk to the realtor or the innkeeper.

The **Housing Director** makes itself (a `[Housing Director]` object) as soon as a property or an inn wakes up: it keeps
the clock, the books, the chests and the doors, and saves.

## What a player does

* **A room for the night.** At an innkeeper: see the free rooms and what a night costs now, choose how many nights,
  pay, and the room is theirs — its door opens for them, its bed is theirs to sleep in, its chest keeps their things.
  They may stay longer (paying for more nights) or give the room up. When the stay ends, the room is someone else's
  to rent and what was left in its chest is **held** for them.
* **A house.** At a realtor: the list of houses — the district, what each asks per day, weekly and/or monthly, the
  house (footprint, floors) and the land, the garden (yes or no, and how big), the other parts of the land (a yard, an
  orchard, a workshop), whether it is free or let and until when, and the prices it was let for lately. The player
  makes an **order**: a few houses in order of preference, an offer per day and a term for each, and pays its deposit
  (enough for the dearest of them). At the next clearing of the market (every 6 lease hours) they win at most one
  house, and pay what they offered for it; the rest of the deposit comes back. If they win nothing, the order stands
  for the next clearings until they take it back (offers are binding: taking one back, or offering less, takes effect
  two and a half days later; offering more is immediate). If a tenant's rent was fixed on their offer, their order is
  held for that house until it is offered, and takes it if the tenant leaves. A house that has just come free waits a
  day before its first clearing so everyone can see it.
* **Their home.** A tenant pays their next terms from a **rent account** at a realtor or an innkeeper (deposited in advance). A day
  before a term ends, the renewal is fixed — its rent never rises faster than about 11 % a week, only with the market,
  and only as far as an offer that could take the house stands behind it — and the money is set aside. They choose their **protection**: how much others would have to offer to take
  the house from them (and pay a small premium for it). They let friends in, choosing what each may do — and someone
  they let manage it can let others in too (from their own My home page or the innkeeper).
* **Their messages.** Every innkeeper and realtor shows the player's messages (an order won, a lease that will not
  renew, money owed and paid back) and what is held for them — claimed there.
* **Their own housing, anywhere.** The **My Housing** key (**P** by default, rebindable in Key Bindings) opens the
  player's own page wherever they are: their leases and renewals, protection, who they let in, their order, their
  rent account, messages and what is held for them (deposits, withdrawals and claims are made at a realtor or an
  innkeeper).

### The windows

| Window | Opens | Shows |
|---|---|---|
| **Innkeeper** | talking to an innkeeper (**Rent a room**) | the free rooms and tonight's price of each, nights to pay for, the player's stay (stay longer, leave), who they let in, their account, messages and held items |
| **Realtor** | talking to a realtor (**Property**) | **Houses** (each with its district, distance and direction, sizes — house, floors, land, garden, zones — asking rent per day and per term, weekly/monthly, free or let and until when, the first clearing of a free house, recent prices; **Show the way** marks it in the world), **My order** (up to 8 houses ranked, a term and an offer each, the deposit, the next clearing), **My home** (leases, renewal, protection, grants), **Account** |
| **My Housing** | the My Housing key, or `HousingWindows.OpenMine()` | My home and Account, from anywhere |

## The components

| Component | On | What it is |
|---|---|---|
| **Property** | an empty object at the plot's origin (its house model, chests, beds, doors and gardens as children) | A house to rent with its land: the land and house outlines, floors, zones, lease terms, quality, district, what belongs to it |
| **Inn** | the inn building's root | The building's area, its nightly price, its rooms |
| **Inn Room** | a child of the inn | Its floor area, quality, its bed (a Sleep Spot), chest(s) (Storage) and door(s) |
| **Lockable Door** | a door leaf (with a collider) | A door only the tenant and those let in may open; it swings itself, or drives a Door Controller |
| **Realtor** | an NPC | The **Property** option: the houses, orders, leases, the rent account |
| **Innkeeper** | an NPC (in its inn, or with *Inn* set) | The **Rent a room** option: the rooms of its inn |
| **Housing Settings** | an asset in Resources | The rules: time, sizes, districts, the market, the inns, limits, held items, the editor's checks |
| **NetHousing** | the Network World object | Requests, the player's own views, access, door states in a network session |

## Housing Settings

**Places**

| Setting | Default | What it does |
|---|---|---|
| Currency | (project default) | The money of rents, deposits and refunds |
| Vacant Doors Open | on | Doors of a house nobody rents open for anyone, so players can look round it before they bid |
| Save Delay Seconds | 2 | Real seconds between a change and the world's save, wallets included (also saved when the world closes, on quit and on pause) |
| Clearing Budget Ms | 4 | Real milliseconds a frame may spend on the market's clearings: a long jump of the clock is cleared over a few frames instead of freezing one; requests wait meanwhile (0: all at once) |
| Ground Layers | nothing | Layers that may lie on a plot without counting as standing on it (ground, roads, decals) |
| Intrusion Warn Area | 0.05 m² | An object covering this much of a plot is a warning; 1 m² or more an error |

**Rules ▸ Time** — *Clock* (**Auto**: real days in a network session, game days offline; or always Game Time or Real
Time), *Real Hours Per Day* (24), *Game Day Minutes* (24), *Restart Hold Minutes* (15: after a restart, housing time
waits — at most as long as the game was down — so players have time to come back before anything ends), *Week Days*
(7), *Month Days* (28). Housing time stops whenever the game or the server is not running: nothing ends while it is down.

**Rules ▸ What sizes are worth** (base rent per day) — *House Per Square Metre* (0.12, the ground floor), *Extra Floor
Share* (0.6 of the ground floor for each floor above), *Land Per Square Metre* (0.02, land that is neither house,
garden nor zone), *Garden Per Square Metre* (0.06), *Zone Per Square Metre* (one rate per Land Use: Garden 0.06, Yard
0.025, Orchard 0.05, Pasture 0.02, Workshop 0.05, Storage 0.04, Courtyard 0.03, Other 0.02), *Min Daily Rent* (2),
*Districts* (each with a *Desirability* that multiplies its properties' base rent: a fine quarter 1.5, the docks 0.7).
The base rent is (house + garden + zones + rest of the land) × quality × the district's desirability; a property may
also set its own *Base Rent Per Day*.

**Rules ▸ The market of houses** — see [chapter 03](03-The-Market.md) for what each does.

**Rules ▸ Inns** — see [chapter 04](04-Inns-Access-and-Belongings.md).

**Rules ▸ Limits per player** — *Max Inn Rooms* (2, at most one per inn), *Max Grants* (16 people per place).

**Rules ▸ What tenants leave behind** — *Free Days* (3), *Fee* (0: claims are always free), *Keep Days* (0: kept until
claimed), *Forget After Days* (0: never), *Max Stacks* (120: above it a player must claim before they rent again —
nothing is thrown away).

The settings' own check warns about a currency that is not kept between sessions, a Game Time clock in a network
session (a week would last a few real hours: players who log off for a night would lose their homes), duplicated
district names and a month not longer than a week.
