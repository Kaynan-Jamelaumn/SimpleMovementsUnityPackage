# Housing 03 — The Market of Houses

[Back to the Housing guide](README.md)

Houses are let by a **market that clears on a schedule**, like a sealed-bid auction that runs every six hours for
every house that is free or whose lease ends then. It is built so that prices follow **real demand only — offers that
can be taken at their word, and houses nobody takes** — and nothing else: interest that costs nothing (looking,
clicking, alts, orders placed and pulled) cannot move a price.

## Orders

At a realtor, a player makes **one order**: up to 8 houses (*Max Order Items*) **in order of preference**, each with a
term (week or month, as the house allows) and an **offer per day**.

* An offer must be at least what the house **takes** — for a free house, or one shown coming free, its **asking rent**
  (below); for a house that stays let, its **base rent** (or its asking rent, when that is lower): what others offer for
  a let house never makes anyone's offer dearer — and at most 50 × its base rent (*Max Offer Base*, a guard against
  typing errors); above 5 × the asking rent (*Confirm Above Asking*) the window asks to confirm.
* **It is paid in advance.** The order's deposit is the dearest of its houses' term totals (offer × days of the term),
  taken from the wallet when the order is placed. The days of each term are fixed then: a later change of *Week Days*
  or *Month Days* does not change what the order pays for. Money given back is paid into the wallet whenever the
  player is online.
* **It is sealed.** Nobody else sees it: not the offers, not who made them, not how many orders there are.
* **It is binding, with a notice.** Adding a house or offering more takes effect **at once**. Anything that takes
  something back — dropping a house, offering less, changing a term, withdrawing the order — takes effect only after
  the **order notice** (the renewal notice + the first-clearing delay + two clearings: 2½ days by default).
  Until then the order **stands as it was and may still win** (and is paid if it does) — but from the moment
  something is taken back, it **moves no price**: what is being taken back holds no rent up. The notice is longer than
  a house takes from being known to come free to being offered, so no offer can be pulled out of the way of a house
  that comes free. A change that takes back nothing beyond the change already waiting keeps its time.
* **It can be held for a house.** When a tenant's rent is fixed above the house's base rent — or a house is shown
  coming free (at its notice, or later, when its tenant stops renewing or wins another house), or comes free because its
  tenant won another house at its lease end — at a price above its base rent, that price rests
  on the best offer for the house from someone free to take it; that offer's order is **held** for the house until the
  house is offered (its lease end — and its first clearing, if the tenant then leaves). Until then the order wins no
  other house: at the house's lease end it is tried for that house alone, and at its first clearing for that house
  first, at its offer, whatever the house asks then. So the offer that set a price always takes the house if the tenant
  leaves: winning a cheap house added for the purpose cannot get it out, and neither can a scene that is not loaded for
  a while. While it is held, its changes that take something back and its withdrawal wait too, until the house has
  been offered. The player is told, and the realtor and their housing page show it ("Your order is held for …").
* **It stands** from clearing to clearing until it wins or is withdrawn. Changing it keeps its place in the queue (an
  older order wins ties).
* Who may order: a saved character (offline: always), who in a network session has been in the city for 3 online
  hours (*Min Online Hours*), rents fewer than *Max Houses* (1) — one more may be won while moving — and has no more
  than *Max Stacks* held for them (claim first). These are checked when an order is placed or changed; an order that
  stands is binding whatever happens afterwards (having too much held, say, does not set it aside).

## Clearings

Every **6 lease hours** (*Tick Hours*) the market clears. Leases end on clearings (changing *Tick Hours* on a world
moves every lease end onto the new grid, never earlier). Offered at a clearing:

* every free house that has waited the **first-clearing delay** (24 lease hours, *First Clearing Delay Hours*): a new
  house, or one whose lease just ended — everyone sees it first, there is no race to click;
* every house whose lease ends at this clearing **and is renewed** (its tenant renews and the renewal is paid for): its
  tenant's protection against the offers.

A lease that ends **without being renewed** — the tenant stopped renewing, is moving, could not pay, or has been away —
is not auctioned at its end: the house becomes free at that clearing and is offered after the first-clearing delay,
like any house that came free. That it **comes free** is public from the renewal notice a day before (the realtor shows
"Comes free in …"), so nobody can hand a house on to a friend.

The clearing matches players and houses with **deferred acceptance**: each order proposes to its favourite house; a
house keeps the best proposal so far; an order turned away (or displaced) proposes to its next house. An order held for
a house proposes nothing until that house is offered, and then that house first. Every lease that ends now and is
renewed (and paid for) holds its own house **on its own**, with the strength of its tenant's protection — whatever else
its tenant wins, and however many of their leases end at this clearing. The best proposal is the highest offer; a
tenant keeps ties; between equal offers the older order wins, then a draw nobody can predict or race. The result does
not depend on the order players or houses are looked at, **each order wins at most one house**, and it is **stable**:
no player and house would both rather have each other than what they got.

* **Winners pay what they offered** — not a cent more because someone else wanted the house, not less. The rest of their
  deposit comes back.
* A player who won a house while renting as many as they may (*Max Houses*) is **moving**. If one of their leases ends
  at this very clearing, that one is given up first: its renewal is not taken (the money stays in their rent account)
  and the house **comes free like any other** — free from this clearing, offered after the first-clearing delay, so
  nobody can be handed it; were two to end now, the one let most recently goes. Otherwise the lease that ends soonest
  (not the new house) ends at its term and is not renewed. With *Max Houses* 1: their old house. A player below the
  limit who wins a house keeps every house they renew.

## Asking rents

Each house has **its own asking rent**: its base rent (sizes, quality, district — [chapter 01](01-Overview-and-Setup.md))
times its own price level, between 0.25 and 4 × its base rent (*Min Level*, *Max Level*). A new house asks its base
rent. After every clearing, each house's asking rent follows **its own demand** — and only binding offers move it:

| What happens | What the asking rent does |
|---|---|
| A free house is offered and **nobody takes it** | It asks 3 % less (*Unlet Step*) — below its base too, down to the floor — and is offered again at every clearing |
| A let house that **someone besides its tenant** has a standing offer for, **above what it asks** | It asks up to 1 % more a clearing (*Rise Step*), **towards the best such offer, never past it** — that offer lost to the tenant's protection, but would take the house the moment it came free. The tenant's renewal follows, never faster than about 11 % a week |
| The best offer for a house that asks more than its base rent **goes** — it won another house, or is being taken back | The premium goes with it **at once**: the house asks the next offer standing behind it, or its base rent. A premium lasts only while an offer stands behind it. (A free house — or one shown coming free, which is priced like a free one from its notice — counts only offers from orders free to take it now, not those held for another house, and never rises) |
| A let house nobody else wants, at or below its base rent | It holds: its tenant keeps the rent they have |
| One player takes a house at its price — or offers far more than it asks, with nobody else wanting it | Nothing: what a winner offered teaches the market nothing |

There is **no level shared between houses**: one empty house never reprices its neighbours, and a house nobody names
does not move. The realtor shows each district's **usual level** (the median of its houses' asking rents against their
base rents) and each house's last prices actually paid.

### Why fake interest cannot move prices

* **Only binding offers count.** A price rises only towards a standing, prepaid offer, and a premium lasts only while
  one stands behind it. Every such offer can be taken at its word: the order notice keeps it in force for longer than
  any house takes to come free and be offered, and an offer being taken back holds no price up from that moment.
* **The offer a rent rests on is held to it.** A tenant's renewal is fixed from what the house really asks: more than
  its base rent only as far as an offer stands behind it from someone free to take the house. That offer's order is
  then held for the house: it wins nothing else until the house is offered, and is tried for it first. An alt that
  pushed a tenant's rent up takes that house — and pays — if the tenant leaves: it cannot pull the offer in time,
  win a cheap house first, withdraw just before the renewal is fixed, or make itself unable to order (too much held,
  say) to get out of it.
* **No offer is forced up.** Offers for a house that stays let start at its base rent, whatever others offered for it:
  an alt that lifts a let house's asking and withdraws before the house can ever be offered leaves nothing behind —
  no honest offer had to match it, and the renewal rests only on offers that still stand at the notice.
* **One offer, one rent.** An order held for one house holds up no other tenant's rent: it could not take their house
  now. An order offering for several houses wins only one, so it can never stand behind two renewals at once.
* **Prices never pass the offers.** A house never asks more than the best offer someone else made for it. Offers at
  exactly the asking rent lift nothing.
* **Losing offers change nothing anyone pays.** Winners pay their own offer: a friend's or an alt's bid just below the
  winner does not raise what the winner pays (it would, in an auction where the winner pays the second-highest bid).
* **Nothing is counted that costs nothing**: not viewers, not clicks, not the number of orders.
* **No race.** Orders are sealed and matched at the clearing; a house that comes free waits a day and is shown coming
  free a day before.
* **Alts are slow and expensive**: each needs hours online, its own deposit and its own home limit; a character cannot
  play twice at once.

In simulation (20 houses, 20 players who rent and renew while it is worth it to them, three runs each), six alts with
offers at 1.3–1.9 × the asking rent on houses with sitting tenants tried every way out of the houses they pushed:

| What the alts did | Honest tenants paid (per day; 88–94 without alts) | What the alts were made to take |
|---|---|---|
| Dropped every house the moment it was shown coming free | 101–105 | 116–144 houses in 150 days, paying 154 000–245 000 |
| Added a cheap free house to win at once when a house they pushed came free | 102–105 | 123–158 houses, paying 190 000–332 000 (held: the cheap house was no way out) |
| Withdrew just before the tenants' renewals were fixed | 89–91 | next to nothing — and they moved no rent |
| Got more held for them than allowed, to stop counting | 85–90 | the houses that came free (their orders still counted) — and they moved no rent |

Where alts' offers moved rents they paid for it many times over — 150 000–330 000 against about 270 000 of rent for
the whole city in that time, for houses they did not want; where they found a way not to pay, they moved nothing.

### Why pay-what-you-offer

The textbook "bid what it is worth to you" auction (the winner pays the second-highest bid) lets any losing bid — an
alt's, a friend's — set what the winner pays. That is the fake interest this market must not reward, so winners pay
their own offer. Players offer sensibly by looking at what houses were actually let for lately and at the asking rents,
which follow the offers.

### How it was checked

The rules were run against simulated players (each valuing each house differently; homeless players ordering their
three best houses; tenants renewing while it is worth it and moving when another house is clearly better), and the
asking rents were compared with the **exact competitive prices** of the same players — the lowest and highest prices
at which no player wants a house they do not have (the assignment game's buyer- and seller-optimal prices):

| Market | Competitive prices (mean) | Asking rents settled at |
|---|---|---|
| Twice as many players as houses | 103 – 117 | 125 |
| The same, other players | 131 – 142 | 134 – 136 |
| Demand doubling after two months | 103 – 117 | 111 – 114 |
| The same, other players | 106 – 125 | 110 – 121 |
| Half as many players as houses, or a crash from 2 × to 0.5 × | 25 – 26 | 26 – 34 (30 on average over six runs: the floor, and houses still let at their base rent) |

Prices settled within days of a change and stayed put (no swings). In a market exactly as full as the city, prices
hold where they are (nobody waits and nothing stands empty: nothing says a price is wrong). The Edit Mode tests
(chapter 05) check each rule.

## Sitting tenants

A tenant is never outbid by surprise, and their rent never jumps.

* **Renewal.** A day before a term ends (*Renewal Notice Hours*: 24 lease hours) the renewal is **fixed** and the
  tenant is told: its rent is what the house really asks — its asking rent, but more than its base rent only as far as
  an offer stands behind it from someone free to take the house (an offer being taken back, or one held for another
  house, holds nothing up) — and it **never rises faster than about 11 % a week** (*Max Rise Rate Per Day* 0.015); if
  the asking rent fell, the renewal falls with it. The order of the offer it rests on is held for the house (above),
  so if the tenant leaves at that price, that offer takes the house. The money is set aside from the tenant's **rent
  account**; if it is short, they are told how much to deposit before the term ends, or the lease ends.
* **Protection.** At the clearing that ends their term, their house is offered too: someone takes it only by offering
  **more than 1.1 × the tenant's protection** (*Protection Margin*). The protection is chosen by the tenant — by default
  2 × the renewal rent (*Default Protection*), up to 5 × (*Max Protection*) — and they pay for it: a premium of 5 % of
  (protection − rent) per day (*Protection Premium*). With the default that is 5 % on top of the rent. Once the renewal
  is fixed, **the price that takes the house at its lease end is public**: the realtor shows every player "an offer of
  at least … takes it from its tenant", so the lease end is an open auction above the tenant's protection — a tenant
  who sets a low protection cannot pass the house privately to a friend who knows the price; anyone may outbid them.
* **Rent account.** Deposited at a realtor or an innkeeper, holding at most 12 renewals ahead, premium included (*Max
  Prepaid Terms*: it is for rent, not a vault), withdrawable at any time (except what is set aside for a fixed renewal).
* **Stopping.** The tenant may stop renewing at any time (what was set aside goes back to the account).
* **Gone.** A tenant who has not been seen for 21 lease days (*Abandon After Days*; counted only while the world runs)
  does not renew: the lease ends at its term and the money stays in their rent account. A house the city has not had for
  a whole term (its scene gone, the house removed) is not renewed either.

When a lease ends, the house is free and offered after the first-clearing delay; what the tenant left in its chests is
held for them ([chapter 04](04-Inns-Access-and-Belongings.md)); its garden stays as it was and keeps living
([chapter 05](05-Gardens-Time-Saving-Multiplayer.md)).

## Settings (Rules ▸ The market of houses)

| Setting | Default | |
|---|---|---|
| Tick Hours | 6 | Lease-clock hours between clearings |
| First Clearing Delay Hours | 24 | A house that is new or came free waits this long before it is offered |
| Renewal Notice Hours | 24 | When a renewal is fixed (and a house coming free is shown) |
| Max Order Items | 8 | Houses one order may name |
| Confirm Above Asking | 5 | Offers above this × asking ask to confirm |
| Max Offer Base | 50 | No offer above this × base rent |
| Min Online Hours | 3 | Residency before a player may order (network session) |
| Default / Max Protection | 2 / 5 | × the renewal rent |
| Protection Margin | 0.1 | An offer must beat protection by this share |
| Protection Premium | 0.05 | Share of (protection − rent) paid per day |
| Max Rise Rate Per Day | 0.015 | A sitting tenant's rent rises at most this fast |
| Max Prepaid Terms | 12 | Rent account limit (in renewals, premium included) |
| Abandon After Days | 21 | Absent tenants stop renewing (0: never) |
| Min / Max Level | 0.25 / 4 | Asking rent bounds × base rent |
| Unlet Step | 0.03 | A free house left unlet asks this much less each clearing |
| Rise Step | 0.01 | A house wanted by someone else for more asks this much more each clearing (towards their offer) |
| Max Houses | 1 | Houses a player may rent at once (each renews on its own) |

The order notice is not a setting of its own: it is the renewal notice plus the first-clearing delay plus two
clearings, so that it always outlasts the time a house takes from being shown coming free to being offered.
