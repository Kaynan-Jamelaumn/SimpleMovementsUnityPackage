# Housing 04 — Inns, Access and Belongings

[Back to the Housing guide](README.md)

## Inns

An inn has a limited number of rooms (its Inn Rooms), each with a bed and a chest. At its **innkeeper** a player sees
which rooms are free and what a night costs in each now, chooses 1 to 14 nights (*Max Nights*), pays, and gets the
room: its door opens for them, its bed is theirs, its chest keeps their things. One room per inn, at most 2 rooms in
all the inns (*Max Inn Rooms*).

* **Staying longer**: pay for more nights at tonight's price, up to 14 nights ahead.
* **Leaving**: the room is given up at once (no refund) and what is in its chest is held for the player.
* **When the stay ends** the room is free for someone else; the guest no longer opens its door, bed or chest, and
  what they left in the chest is **held** for them (below).
* **How long a night is**: with real-time lease days (a network session), a night is a whole lease day from when the
  stay began (one night: 24 hours later, whenever it was taken); the checkout hour is not used. With game days
  (offline), a stay ends at the **checkout hour** (10:00, *Checkout Hour*) of the game's sky (its Day Night Cycle or
  Weather System; without one, the housing day's hour) — a night lasts at least 6 hours (*Min Night Hours*), so a room
  taken at 9:00 runs until the next morning, not one hour. The hours are counted at the housing clock's steady pace:
  sleeping through the night (the sky skipping ahead) does not end a stay sooner.
* **A room counts by its floor**: a player belongs to the room whose floor is right under their feet (up to 2.5 m
  above it), so rooms stacked on the floors of an inn never mix up their guests.

### What a night costs

A night in a room costs the inn's **Nightly Price** × the room's **Quality** × the inn's **price level** × a
**scarcity surcharge**:

* **The price level** follows how full the inn has been lately: e^(2 × (occupancy − 0.7)) (*Price Response*,
  *Target Occupancy*), between 0.6 and 2 × (*Min Level*, *Max Level*). The occupancy is an average over the last two
  days (*Occupancy Days*) — a busy evening moves it little, a busy week a lot — and it has no memory beyond that
  average, so a burst of bookings fades with it.
* **Only rooms in use count**: a room counts as occupied while its guest (or someone they let in) was in it or slept in
  its bed within the last 24 hours (*Active Hours*), or was just let. Rooms booked only to push prices up — and never
  used — do not raise the price for anyone.
* **The last free rooms cost more**: from the target occupancy up, up to +50 % (*Scarcity*) for the very last room.
* A new inn starts at its own nightly price.

Prices are posted: a player always pays the price they were shown (a request whose price changed meanwhile is refused
and the window shows the new one).

## Letting people in

The tenant of a house or a guest of a room decides who else may use it. In the housing window, they **let in a
player standing next to them** (within 8 m), choosing what that player may do:

| Right | Lets them |
|---|---|
| **Enter** | Open its doors |
| **Garden** | Work its gardens (sow, water, harvest...) |
| **Chests** | Open its chests |
| **Beds** | Sleep in its beds |
| **Manage** | Let others in or out (only the tenant gives it) |

**Friend** is everything but Manage. A grant lasts — the player is known by their lasting key, not their name — until
the tenant takes it back or the stay or lease ends. Someone with Manage may give only what they may do themselves
(never Manage) and change or remove only those they let in; removing a manager removes everyone they let in. At most
16 people per place (*Max Grants*), two of them managers. Lists show other players by name and a short **fingerprint**
(four letters: "Kaya #M3QX") so two players called alike are told apart; keys never leave the server.

### What is locked

Everything that belongs to a place — its **doors** (Lockable Doors), **chests** (Storage), **beds** (Sleep Spots) and
**gardens** (Soil Fields) — refuses everyone but its tenant and those they let in: "Locked", "Not yours.", "Not your
bed.", "Not your garden.". The server checks every action; the client's prompts follow what the server sends, so they
match.

* The doors of a **house** nobody rents open for anyone (*Vacant Doors Open*), so players can look round it before they
  bid; its gardens may then be looked at (inspected), never worked. Inn rooms are never open to visitors: a free room
  stays locked for everyone, on every machine.
* Nobody is shut in: anyone standing inside may open the place's doors to leave. Whoever is let in, with any right (a
  friend let in only for the beds sleeps there undisturbed), may stay; so may someone whose access just ended, for 2
  minutes while they leave. Anyone else found inside a rented house or room is **shown out** after 3 seconds: put just
  outside the nearest door that leads out (else just outside its walls) — always on the ground, with room to stand,
  never in a wall or in another house or room. When no such spot exists they are only asked to leave.
* A chest someone has open closes for them when their access ends.
* **One thing, one place**: a chest, bed, door or garden listed by two places belongs to the one that was loaded first
  (the other is reported once in the log, and the validator reports it as an error). A house or inn with the id of
  another (a copied object) is **closed**: it opens for nobody, owns no chest and is not listed; so is a room with the
  id of another room of its inn.

## What is left behind

When a stay or a lease ends, everything in the place's chests is taken out — exactly what was there: each item, its
count and each copy's data (durability, loaded rounds) — and **held** for the tenant. It is never silently deleted:

* It can be **claimed at any innkeeper or realtor**, in their window ("Kept for you: Room 3, The Gilded Goose — 14
  items"). What fits goes into the player's inventory, each stack exactly as it was kept (a worn tool comes back
  worn, a weapon with the rounds it had loaded); the rest stays held. Like anything picked up, it fills the stacks the
  player already carries first — each unit keeps its own wear or quality, as in a harvest; only a stack whose unit in
  use is half used, or a weapon with rounds loaded, needs free room of its own, so its state never mixes with another's.
* The first 3 days are free (*Free Days*); after that a claim costs the *Fee* (0 by default: always free). A claim
  pays the fee the window showed: if it changed meanwhile (the free days ran out), the claim is refused ("The fee
  changed: look again."). A claim that could take only part of a bundle pays once: the rest is free.
* It is kept **until claimed** by default. You may set *Keep Days* (thrown away after so many days unclaimed; the
  player is told) and *Forget After Days* (thrown away when the player has been gone that many lease days, counted only
  while the world runs). Both are 0 — off — by default.
* Instead of throwing away a player's things, they must claim before they rent again once more than 120 stacks are
  held for them (*Max Stacks*): held items can never grow without limit, and nothing is lost to a full limit.
* Chests hold what belongs to their tenancy only: a new tenant always finds the place's chests empty (or as the scene
  makes them, before anyone rented it), and a player who rents the same house again does not find someone else's
  things. Each chest is kept by its place and its chest id, so two houses whose chests share an id (copies of one
  prefab) never mix their contents.
* Each world keeps its own belongings: when the session changes (single player to hosting, a host going back to
  single player) while a city is loaded, its chests go back to what the scene gave them before the other world fills
  them from its own books.

**Why not delete items after a few days?** Players lose things they cared about for being away a week — the most
frustrating outcome — and deleting saves nothing that a limit on held stacks does not. So held items are claimable
storage by default, at every innkeeper and realtor; a fee and deadlines are there if your game needs them.
