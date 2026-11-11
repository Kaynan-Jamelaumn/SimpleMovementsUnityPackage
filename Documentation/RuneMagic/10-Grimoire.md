# Rune Magic 10 — The Grimoire

An addition made after the nine phases (the design spec does not have it). Every Mage begins with a **grimoire**: a book whose pages hold **prints**, copies of the runes of a Mage's soul. A Mage **imprints** a rune of their soul on a page (the rune stays, but **loses part of its energy**, and it must have that much), and a Mage who holds the book **absorbs** a print from a page (it becomes a rune of their soul **with no energy**; a print can be absorbed **once**). The book is an item, so **another Mage who holds yours** can read it and absorb from it.

It is class-gated and mutually exclusive like the rest of Rune Magic: only rune users (Mages) read a grimoire; nobody else gets a soul, a window or a request.

## What a player does

| | |
|---|---|
| **Open it** | Use the grimoire item in the inventory, or the **Grimoire** button of the Rune Soul window, or the `Grimoire` Input Action (no key in code: *Tools ▸ SimpleMovements ▸ Rune Magic ▸ Add Rune Magic Input Actions* adds it without a key; bind it in the Input Actions editor and players rebind it in Key Bindings). A player holding several books (their own and another Mage's) picks one at the top of the window. |
| **Imprint** | Pick a blank page; the right column lists the runes of the soul with the energy each imprint costs. A rune that does not hold that much shows *needs N more* and cannot be chosen: give it energy first. The rune keeps its rank, quality and (for a rune made by synthesis) its profile in the print. |
| **Absorb** | Pick a page with a print and press *Absorb into my soul*. It becomes a **new** rune with the print's rank and quality and **no energy** (the Mage gives it energy from the pending pool like any rune); it takes soul slots as any rune does, so a full soul says *no room* and leaves the print on the page. The page is blank again (or spent for good if the balance says so). A rune whose alignment clashes with the soul's strains it, as any absorption. |
| **Erase** | The author may erase a print (twice-click to confirm). Nothing is given back. |
| **Read another Mage's book** | Anyone who holds a grimoire item (a trade, a drop, a loot, a gift) opens it and sees its pages. They absorb prints; only the author writes or erases (*Only Author Imprints*). |

## The rules (all numbers are data: *Rune Magic Database ▸ Balance ▸ Grimoire*)

| Field | Built-in | |
|---|---|---|
| Give On Spawn | on | A Mage's soul is given a grimoire once (the soul remembers it). |
| Reissue If Missing | on | A Mage who enters the game holding none of their own grimoires gets it again, with its pages, **once per visit and once after each respawn** (which may cost items; never twice in a row, so a book handed to someone else stays handed). It exists because your game may not keep the inventory between sessions; with the inventory kept, switch it off. While another player holds the same book too, both copies show the same pages. |
| Pages | 10 | Prints a book holds. A smaller number later never destroys a print (pages are only ever added). |
| Imprint Energy Fraction | 0.25 | The imprint costs the rune this share of **its maximum energy** (250 for a rank 1 rune, 6,250 for rank 3), at least *Min Imprint Cost* (10). A rune with less energy cannot be imprinted: nothing changes. |
| Allow Composite | on | Runes made by synthesis can be imprinted (and absorbed) with their profile. |
| Only Author Imprints | on | |
| Allow Erase | on | |
| Absorbed Energy Fraction | 0 | Energy an absorbed print holds, as a share of its maximum. **0 = none** (the feature's rule). Above 0 the validator warns that copying becomes cheaper than earning. |
| Page Free After Absorb | on | Off: an absorbed page is spent for good (a one-time book). |
| Book Name / Item Weight / Price / Can Be Sold | Grimoire / 1 / 0 / off | The item. |

**Balance of the loop:** copying a rune costs a quarter of its maximum energy *and* a soul slot *and* the copy starts with no energy, so a grimoire is a way to spend a slot and energy to **duplicate a rune you need twice** (for rank advancement, which takes seven of one rank) or to **pass a rune to another Mage**, not a way to get power for free. The Balance Report is unchanged; tune the fraction in the table.

## How it works

- **The book is an item.** Its id is `grimoire:<serial>:<author>` (the serial of the book, the author's name as letters and digits). Every machine makes the item from the id through the Item Database, as rune items are (`GrimoireItems`, `GrimoireItemSO`), so saves, trades, drops, merchants and the network resolve it. It does not stack. Using it opens the book.
- **The pages are not in the item.** They are in the world's **ledger** (`GrimoireStore`, section `runeMagicGrimoires` of the world's store, written with the rest of the world; each world has its own), under the serial. The item travels between players; the pages stay with the serial. An id the ledger does not know opens nothing (*The pages of that grimoire are lost*), so a made-up id gives nothing.
- **Reading needs holding.** The server checks that the player is a Mage, that the ledger has the book, and that **the player's inventory holds the item** before it opens, imprints, absorbs or erases. A book that leaves the inventory while open empties the window at once.
- **Authority.** Everything happens where the rules run (offline, the server). The owner's client sends requests (`GrimoireOpen` 14, `GrimoireImprint` 15, `GrimoireAbsorb` 16, `GrimoireErase` 17: values are append-only) and receives the open book as a view (`TargetGrimoireView`, to the owner only: the pages, nothing of the soul). The request guard, text limits and per-kind checks are those of every other request.
- **Who wrote it.** The author is identified by the player's lasting key (`PlayerKeys`); without a key (no persistence) the soul uses a session key, adopted as the real key when it arrives.
- **Saves.** The soul keeps only `grimoireSerial` and `grimoireGiven` (soul save version 6; older saves load and are given a grimoire at their next visit). Data version 8 adds the balance block (`Upgrade` fills it).

## Against duplication

Cooldowns and hourly limits per Mage, and an audit log in every book: see [11](11-Charge-Simulator-and-Guards.md#3-the-grimoires-guards).

## Giving a grimoire by other means

`GrimoireStore.Create(authorKey, authorName)` makes a new book in the world's ledger; `GrimoireItems.For(book)` is its item, which you can give a player through any reward or quest path (`InventoryManager.AddItem`). Switch *Give On Spawn* off if grimoires should only come that way.

## For developers

| File | Does |
|---|---|
| `Core/Data/GrimoireDefs.cs` | `GrimoireBalance` |
| `Core/State/GrimoireState.cs` | `GrimoirePage`, `GrimoireData` (a book), `GrimoireLedger` (the world's books; JSON) |
| `Core/Grimoire/GrimoireRules.cs` | The rules (imprint, absorb, erase), the item id codec (`GrimoireIds`), the request texts (`GrimoireRequestText`), the owner's views (`GrimoireView`) |
| `Runtime/GrimoireStore.cs`, `GrimoireItems.cs`, `Items/GrimoireItemSO.cs` | The world's ledger, the runtime item source and the item |
| `Runtime/RuneSoul.Grimoire.cs` | The soul's side: the Mage's own book, the requests, holding, views |
| `UI/GrimoireWindow.cs` | The window |
| `Networking/*/RuneMagic*` | Request kinds, limits, the view RPC |

**Existing files this touches** (all Rune Magic's own): the settings (`grimoireAction`), the input names, the runtime bootstrap, `RuneSoul` (`TickGrimoire`), the Rune Soul window (a button), the network request kinds and `NetRuneMagic`. **No existing project file changes for the grimoire.**

## Tests

Without Unity (`GrimoireCoreTests`, 27 tests): ids and malformed ids; the imprint cost; copying a rune (the rune stays, loses energy, the print has no energy and no instance id); refusal for lacking energy (nothing changes, also the exact boundary); every refusal leaves soul and book as they were; author-only writing; composite runes; absorbing (a new rune, no energy, a new instance, once, only with room, composite slot cost, spent pages, energy fraction); another Mage reading and absorbing but not writing or erasing; erasing; the ledger (unique serials, JSON round trip with prints, broken text, pages only grow); the soul's serial and older saves; the views and the largest book against the network limit; the request texts; the balance validator, the data upgrade, the input action name; and the energy-only update.
`PlayerSetupRegistrationTests` (7 tests) cover the player validators (the journals listed, registration, applicability, what Rune Magic registers, no Rune Magic type named by the validator). In Unity's Test Runner (category *RequiresEngine*, `GrimoireRuntimeTests`, and two of the registration tests): a soul that is not a rune user cannot open, imprint, absorb or erase; the item is made from its id and found through the Item Database; the ledger; the window opens only for a Mage holding a book.

## What to check in Unity

1. A new Mage begins with a grimoire in the inventory (a few seconds after the soul is ready); using it opens the window.
2. Imprint a rune that has enough energy: it loses the cost, the page shows the print. Try one that has not enough: it is refused with the numbers.
3. Absorb the print: a new rune with 0 energy appears in the Rune Soul window; the page is blank again; the soul with no room refuses.
4. Give the grimoire to another Mage (trade or drop): they open it, absorb a print, and cannot imprint or erase. A non-Mage cannot open it.
5. Network: the owner receives the open book; another player never does; a book dropped while open closes its window.
6. Restart the game: the pages are still there (the world's store), and the Mage still has their book (or gets it again once per visit).

## Decisions and limitations

1. **The grimoire is not in the design spec**: the numbers are mine (a quarter of the rune's maximum energy). Tune them.
2. **Inventory persistence** is the game's (this project's inventory may not be kept between sessions): *Reissue If Missing* covers it; the cost is that while another player holds your book, you also hold it (both copies share the pages).
3. **A book's pages are shared by every copy of its item** (there is one ledger entry per serial).
4. **A rune that anchors a spell can be imprinted** (it only loses energy, which the spell needs to be cast).
5. **No per-print icon**: the window lists names; the item has one generic icon and model.
6. **Not checked in Unity**: the window's layout, the item's icon and model, and a trade of the book between two players.
