# Housing 05 — Gardens, Time, Saving, Multiplayer, Scripting and Tests

[Back to the Housing guide](README.md)

## Sleeping gardens

A big city has hundreds of gardens; only the ones near a player need to be alive on screen. Every Soil Field
(**Agriculture Database ▸ Rules ▸ Sleeping gardens**):

| Setting | Default | What it does |
|---|---|---|
| Sleep When Alone | on | A garden nobody is near falls asleep |
| Wake Radius | 120 m | A player this close to the garden's outline wakes it |
| Sleep Radius | 150 m | Nobody this close for Sleep Delay Seconds: it sleeps (the gap stops it flickering at the edge) |
| Sleep Delay Seconds | 20 | Real seconds nobody is near before it sleeps |
| Unload When Asleep | on | A sleeping garden is freed from memory (its soil, plants and drawing) once its save is written |
| Maintenance Lag Hours | 72 | A sleeping garden further behind than this is caught up in the background |
| Catch Up Millis Per Frame | 2 | Milliseconds per frame spent catching gardens up |

* **Asleep, nothing is lost.** Its soil, what covers it (greenhouses, shade, cold frames), its plants and its weather
  are in its save; it is simply not simulated or drawn. With Unload When Asleep it is freed from memory **only once
  that save is written** (a save that fails keeps it in memory, and is tried again), and rebuilt from the save when it
  wakes. Unloading never deletes: a save is written whole (aside, then swapped in, the previous copy kept as a
  backup), and a save that cannot be read is never written over — the backup is used, or the garden refuses work
  until it is mended. Where the rules run (offline, the server) "near" means near any player whose scene is not apart
  from the garden's (a player kept in DontDestroyOnLoad counts everywhere); on a client, near its own player.
* **Each world has its own gardens.** Gardens and the farm clock are kept with the world that is open (offline, online
  or `-world <id>`): house #3's garden in the friends' city is not the one in a solo game. Saves from before worlds
  were kept apart are copied into each world the first time it keeps farming.
* **Waking catches up truthfully.** The time it slept is lived again in lockstep, in steps of at most 3 hours: the
  weather of that time first — the city's climate for those dates, with the same seasons, rain, frost, heat and
  sunlight the awake gardens had (a garden in a scene with a Weather System lives its slept hours, all but the last
  two, with the city's climate: the live weather only knows now) — then each plant through it, under its covers. A
  month asleep is a month of growth, ripening, drought, rot and frost, not a single big step (which would get water,
  frost and harvest windows wrong); flowers open by how far the flowering has come, not one a step, so produce
  ripens, overripens and rots on the same days as in a garden watched hour by hour (an Edit Mode test compares the
  two, within its tolerances). It runs within a per-frame budget, the garden nearest a player first; until it has
  caught up, actions wait ("The garden is waking up."). Gardens wake and sleep a few a frame, the nearest first: a
  realm shown again (offline: back in the city) leaves its gardens asleep until someone comes near.
* **Abandoned gardens keep living.** A garden nobody visits — a house that is unlet, a tenant who left — keeps its last
  state and is caught up in the background, one at a time, whenever it falls more than 3 days behind: its fruit
  ripens and rots, its plants grow, wilt or die, on the same days as in a watched garden. A garden that fell asleep
  in the middle of a catch-up is finished first.
* **In a network session** a garden's state is sent only to the clients near it; a client far away holds no picture of
  it, and receives one when its player comes near. A request is answered only from beside the garden: a player far
  away cannot wake it.

The Soil Field inspector shows, in Play mode, whether a garden is asleep, catching up or awake, with **Sleep now** and
**Wake now** buttons.

## Housing time

Leases, stays, clearings and held items count **lease days** on the housing clock (*Rules ▸ Time*):

* **Auto** (the default): real days in a network session — people log off for nights and weekends — and game days
  offline (a week of 24-minute days is under three real hours of play).
* The clock **runs only while the game or the server runs**: a server down for a day costs nobody a day of their
  lease, and nothing ends while it is down. After a restart, housing time waits up to 15 minutes (at most as long as
  it was down: *Restart Hold Minutes*) so players have time to come back before anything ends.
* It moves at a fixed rate (night speed, pauses and time skips of the sky do not change it) and never goes back.
* The books catch up every frame and before every request, so every quote, view and check reads the present.

## Saving

* **One file per world**: `Application.persistentDataPath/Worlds/<world>/world.json` — the world is `offline`,
  `online`, or the server's `-world <name>` command-line argument. It holds the housing books and the wallets of
  currencies kept between sessions **together**, so money and leases are always saved as one: a crash can never keep
  a refund and lose the payment, or the other way round. The gardens and the farm clock are saved per world too, in
  the world's `Agriculture` folder (a save from before is copied into each world the first time).
* Written **atomically** (a temporary file replaces the old one, which is kept as `world.json.bak`, and is flushed to
  disk), on a worker thread so the game does not stall, at most *Save Delay Seconds* (2) after a change — the whole
  world's delay, wallets included — and synchronously when the world closes, on quit and when the app is paused.
* **Opening** takes the newest complete copy: `world.json`, or `world.json.tmp` when it is newer (a crash after the
  write and before the swap), else `world.json.bak`. An unreadable `world.json` is set aside as
  `world.json.unreadable-<time>` (never thrown away, never written over), and the log says which file was used. If no
  copy can be read, the housing office stays **closed** and nothing is overwritten (players see "The housing office
  is closed").
* An **operating-system lock** on the world's lock file keeps two copies of the game — two processes, on Windows,
  macOS or Linux — from writing the same world; it is released when the process ends, even in a crash.
* Switching worlds (playing alone, then hosting) closes one world — written once more, whole — before the next opens:
  nothing of one world is written into the other.
* A wallet of a kept currency refuses to spend or receive until its saved amount has been restored; what was spent or
  earned meanwhile is added to it, not lost. A player a world does not know starts there with the starting amounts.
  A kept currency that is not loaded yet at that moment (only a later scene uses it) gets its saved amount — once — as
  soon as it loads where the rules run (offline, the server: when a scene brings it, or the first time the wallet uses
  it), and the owner's client shows it as soon as it has it loaded too; until then its saved amount is kept as it was,
  through every save.
* Chests of rented places are saved by place and chest id, with the tenancy that owns what is in them: when a chest's
  window closes, every minute, and when the world is written for the last time. A script that changes a chest's items
  directly calls `HousingDirector.ChestChanged(storage)`.
* A world takes the Housing Settings' *Clock* each time it loads, keeping the lease days left of every stay and lease.

## Who a player is

The books know players by a **lasting key**: a hash of their saved character's id (offline, "local"). The id itself
never reaches other clients. In a network session a character needs a proper saved id (old short ids are moved to new
ones when characters load), and a character can be in the world only once at a time: a second login with the same
character waits, without a key, until the first session ends; a session that has sent nothing for a few seconds (a
crash the server has not noticed yet) is closed for the new login; a host's own player follows the same rule; player
objects are never pooled. So nobody can play one character twice to bid against themselves or empty a chest twice.
A game with its own accounts can set `PlayerKeys.Resolver` (the network layer installs its own only when none is
set): it returns a key, `""` for "no key" (final), or `null` to fall back to the character's id.

## Multiplayer

Put **NetHousing** on the "Network World" object (*Network ▸ Add Missing Relays To Network World*). The server decides
everything:

* **Requests** from a client's housing window (renting a room, an order, a renewal, a deposit, a claim, letting
  someone in) are checked again on the server — the player from the connection, a lasting key, not too many too fast,
  an open session with the innkeeper or realtor within range where the request needs one — and done with the player's
  own wallet. A request sent twice (a retry) is done once. The answer carries the request's number back.
* **Views**: each player receives only their own stay, leases, order, account, messages and held items. Nobody else's
  offers, keys or money ever leave the server; the realtor shows the market (asking rents, sales, free houses) only.
* **Access**: each player receives where they may go, so their prompts match the server, which checks again.
* **Doors**: the open or closed state of every Lockable Door, for every client.

Gardens on properties are networked like any Soil Field (NetworkObject + NetSoilField; *Housing ▸ Make Selected
Gardens Networked*).

## Scripting

Where the rules run (offline, the server):

```csharp
HousingBook book = HousingDirector.Book;            // null on a network client
string key = HousingDirector.KeyOf(player);         // a player's lasting key
PropertyRecord lease = book.LeaseOf(property.PropertyId, key);
long asking = book.AskingMilli(property.PropertyId); // per day, thousandths of the currency
HousingAccess a = HousingGate.AccessOf(new HousingPlace(property), player);
HousingDirector.Tell(key, "The baker left you a pie on the porch.");
HousingDirector.SaveNow();
```

* **Requests** go through `HousingClient.Send(player, request)` — offline (and on the host) it is done at once, and
  the answer and the fresh views arrive during the call; online it goes to the server — and answers arrive as
  `HousingClient.Answered` (matched by `request.seq`), `InnViewArrived`, `RealtorViewArrived`. The views are those of
  the player's desk (the innkeeper or realtor they talk to); a request with `own = true` gets the player's own housing
  (`RealtorView.own`) wherever they stand. The server side is `HousingService.Handle(player, request, desk)`.
* **Windows**: `HousingWindows.OpenMine()`, `CloseMine()`, `ToggleMine()`, `ShowTheWay(property, player)`;
  `HousingHotkey.Enabled = false` turns the My Housing key off for a game that opens the window from its own menu.
* **Events**: `HousingDirector.BooksChanged` (open windows refresh), `HousingDirector.AccessChanged` (a player's
  places changed), `LockableDoor.AnyStateChanged`.
* **Access hooks**: `SoilField.AccessCheck`, `Storage.AccessCheck`, `SleepSpot.AccessCheck` and
  `LockableDoor.AccessCheck` are installed by Housing (`HousingGate.Install`); each returns a refusal or null. Your own
  rules can wrap them (keep the previous delegate and call it first).
* **The rules are plain C#** (`Housing/Core`): `HousingBook` takes the world's facts (`SetWorld`), a save (`Load`) and
  the time (`Advance(now, utcNow)`), and answers quotes and changes (`QuoteStay`/`Stay`, `QuoteOrder`/`PlaceOrder`,
  `SetRenewal`, `Deposited`/`Withdraw`, `Grant`, `Hold`/`Claimed`...). It never touches a wallet or a chest: the caller
  pays and takes, then records it; what the world must follow comes out as `Events`.
* `HousingSettings.Override(settings)` uses specific settings (tests, bootstrap scripts).

## Tests

**Window ▸ General ▸ Test Runner ▸ Edit Mode**:

* `CustomEditor/Housing/HousingCoreTests.cs` — areas (touching areas do not overlap, crossing ones do; concave and
  turned outlines measured exactly; a repeated point is not a crossing; plots far from the origin that share a fence do
  not overlap), base rents from sizes, district, quality and garden zones; inns (a stay gives a free room until it
  ends; the last rooms cost more and a full inn refuses; a busy inn raises its prices, an empty one lowers them, and
  rooms booked but never used do not; checkout hour on game time; too much held stops new stays); the market (an order
  wins a free house at its offer; the best offer wins and nobody else's offer changes what it pays; a player best on two
  houses gets their first choice; offers below the asking rent are refused, and the least offer named is accepted; a
  sitting tenant keeps the house within their protection, an offer beyond it takes the house; renewals never rise
  faster than allowed, and a discount never raises a tenant's rent; an unfunded renewal ends the lease; a house that is
  not renewed comes free for everyone to see; an unlet house asks less each clearing; a house someone else offers more
  for asks more, at every clearing, towards that offer and never past it, and only the houses an order names; an offer
  taken back stands until its notice ends but lifts nothing from then, offering more takes effect at once; a premium goes
  with the offer behind it; the offer a rent rests on cannot escape by winning another house, is taken at its word
  whatever the house asks then, and an order held for one house holds up no other rent; a renewal never rests on an
  offer being taken back; too much held stops new orders but not one that stands; a tenant who wins another house at
  their lease end hands theirs to nobody; every lease renews on its own under Max Houses, and a player below the limit
  keeps their house; a house given up at the house limit comes free with its backers held, a hold outlasts an unloaded
  scene, a house coming free asks only what stands behind it, offers for a let house start at its base rent, and the
  price that takes a house at its lease end is public; a lone unwanted house does not drag its neighbours down; newcomers wait; moving and Max Houses;
  terms fixed with the deposit; leases stay on the clearings when
  Tick Hours changes; a tenant sees their lease while its house is not loaded; the rent account holds a whole renewal;
  withdrawals the purse refuses are put back; the clearing does not depend on the order of the books); managers give
  only what they have and removing one removes everyone under them; fingerprints; held items (free days, a partial
  claim pays once, thrown-away items are told); the books save and load whole.
* `CustomEditor/Housing/HousingValidationTests.cs` — two overlapping plots are an error (touching ones are not), a
  house off its land, an object of the city standing on a plot (not when it is part of the property), a room without a
  bed or a chest, a foreign garden on a plot.
* `CustomEditor/Agriculture/AgricultureTests.cs` — sleeping gardens: a garden asleep for weeks wakes up as if it had
  been watched hour by hour (lavender, carrots, wheat: their stages, health, soil water and nutrients), a catch-up
  spread over many frames ends the same as one done at once, and a catch-up follows the seasons.
