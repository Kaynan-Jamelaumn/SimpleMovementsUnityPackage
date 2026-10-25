# Minigames 02 — Blackjack, Poker, Arm Wrestling and Drinking Contests

**Scripts:** `Minigames/CardGames/Core/ChipGameSession.cs` (chip games), `Minigames/CardGames/Blackjack/BlackjackGame.cs`
and `BlackjackDefinition.cs` (the asset), `Minigames/CardGames/Poker/PokerHands.cs`, `PokerGame.cs` and `PokerDefinition.cs`, `Minigames/CardGames/Contests/ArmWrestlingGame.cs` /
`ArmWrestlingDefinition.cs`, `DrinkingContestGame.cs` / `DrinkingContestDefinition.cs`, tests in
`CustomEditor/Minigames/MinigameTests.cs`.

All four run on the same **Card Table** as Truco ([Minigames 01](01-Card-Games-and-Truco.md)): benches or stools with
Card Seats, the lobby (host picks the stake, everyone Ready), stakes of money, items or a limb, bots, the table window,
the 3D cards and the network (each player sees only their own cards). Only the game asset differs.

| Menu (*Tools ▸ SimpleMovements ▸ Minigames ▸*) | Builds | Seats | Bots fill empty seats |
|---|---|---|---|
| Create Blackjack Table | a round table, 4 stools in an arc facing the dealer | 1-4 players | off (play alone against the dealer) |
| Create Poker Table | a round table, 6 stools | 2-6 | on |
| Create Arm Wrestling Table | a small table, 2 stools face to face | 2 | on (wrestle a bot) |
| Create Drinking Contest Table | a round table, 4 stools | 2-4 | on |

The game assets are made in `Resources/SimpleMovements/Minigames/` (Blackjack, Poker, ArmWrestling, DrinkingContest);
every rule below is a field on them. **Number keys** press the window's buttons (1 = the first): mash *1* to push in arm
wrestling.

---

## 1. How stakes work in games where everyone plays alone

Truco is a team game: the winning team shares the pot. Blackjack and poker are **chip games**: each player's stake buys
the same number of chips (*Starting Chips*, 100). At the end everyone gets back **what their chips are worth** — end with
150 chips of 100 and you get 1.5 times your stake; end with 0 and you lost it.

* **Blackjack** is played against the house: the house pays what you win.
* **Poker** is played against the other seats: what one wins another lost. With *House Covers Bots* the bots' chips are
  real (beating bots wins money); without it players never get more than the stakes players put in.
* **Limb stakes**: everyone who ends with *less* than they started with loses a limb.
* **Leaving** during the game loses your chips (and what you had bet), and the game goes on for the others; it ends
  when no player is left. A game that ends in the middle of a hand gives the bets on the table back.

Arm wrestling and the drinking contest have a winner like Truco: the winner takes the pot (or the losers lose a limb).

---

## 2. Blackjack

Against the house dealer, up to the table's seats (4).

1. **Bet** — every player picks a bet (*Bets*: 10 / 25 / 50, or all in). When everyone has bet the cards are dealt:
   two face up to each player, two to the dealer — the second face down.
2. **Play** — in seat order, each player **Hits** (one more card), **Stands**, or **Doubles** (twice the bet, exactly
   one more card; only on the first two cards). Over 21 is a bust.
3. **The dealer** turns the hidden card over and draws until 17 (*Dealer Hits Soft 17* to also draw on a soft 17).
4. **Payout** — closer to 21 than the dealer (or the dealer busts): the bet is doubled; a **blackjack** (an ace and a
   ten-card) pays 3:2 (*Blackjack Pays*); the same total: the bet comes back (push); less, or a bust: lost. A dealer
   blackjack beats everything but a player's blackjack.

A game is *Rounds* rounds (6), or ends sooner when everyone is broke. *Decks* in the shoe (2), reshuffled when low.
No splitting and no insurance. Bots bet small and play a simple basic strategy.

Aces count 11 unless that busts (`BlackjackGame.Total`, with *soft* when an ace counts 11).

---

## 3. Texas Hold'em

No-limit hold'em for 2-8 seats (6 by default).

* **Blinds**: the small blind (*Small Blind*, 1) and the big blind (twice it) are posted left of the button; they
  **double every** *Blinds Double Every* hands (8). Heads-up, the button posts the small blind and acts first before the
  flop.
* **Betting rounds**: pre-flop (two hole cards each), the **flop** (three cards), the **turn**, the **river**. On your turn:
  **Fold**, **Check** / **Call**, **Bet / Raise** (to the minimum raise, half the pot, the pot) or **All-in**. A raise
  re-opens the betting for everyone else.
* **All-in**: when everyone (or all but one) is all-in, the rest of the board is dealt one card at a time.
* **Showdown**: the best five of the seven cards (`PokerHands.Best`: royal / straight flush, four of a kind, full house,
  flush, straight — the ace also plays low in A-2-3-4-5 —, three of a kind, two pair, pair, high card, with kickers).
  **Side pots**: a player all-in for less only wins what they could cover from each opponent; the rest goes to the best
  hand among those who put more in. Ties split the pot.
* Players out of chips are out. The game ends when one player has all the chips, or after *Max Hands* (30).

The window shows your hole cards at the bottom, the board in the middle with the pot and the bet, everyone's chips on
top, and at the showdown the hands that were shown (the winners lit). Your own best hand is named on top. Bots judge
their hole cards before the flop and their made hand after it; they call by pot odds, raise strong hands and bluff now
and then (*Bot Boldness*).

---

## 4. Arm wrestling

Two players face to face (each sits on a stool; the Animator's `SeatValue` goes from −1, losing, to +1, winning — a
blend tree on it moves the arms).

1. **Grip**: a countdown (*Countdown Seconds*), then **GO!**
2. **Push**: mash the Push button (or the *1* key). Pushes are limited to one every *Push Cooldown* (0.15 s), so nobody can
   mash faster than that — strength decides, not the mouse. Each push moves the arms by *Push Strength* × your strength,
   and costs stamina (*Stamina Per Push*); stamina comes back (*Stamina Regen*), and a tired arm pushes weaker.
3. **Grit**: a big push for a lot of stamina (*Grit Cost*, *Grit Strength*) — for a comeback.
4. The stronger arm also **wins ground by itself** (*Strength Drift*). Pin the other arm to the table to win the round;
   **best of three** (*Rounds To Win*).

**Strength** (`ArmWrestlingDefinition.PowerOf`): body size (Character Body: races and the Height stat) × health × max
stamina × **the main arm's condition** (injury system: a broken or cut arm is weak). A player **missing the main arm (or
its hand) cannot wrestle**. Bots get a strength of 0.85-1.15.

Afterwards, with the injury system on, the loser's arm may be **strained** (*Loser Strain Chance* 20%: the first stage
of *Strain Injury*, a cracked bone). Bet a limb, and the loser loses it.

---

## 5. Drinking contest

Two to six drinkers (4 seats by default).

* Every round everyone must **Drink** their mug before the time runs out (*Seconds To Drink*, 7). Too slow: out.
* A mug makes a drinker drunker by *Mug Strength* (13) ÷ their **tolerance**, a little random; each round's mug is
  stronger (*Stronger Each Round*). At 100% they **pass out**.
* **Tolerance** (`DrinkingContestDefinition.PowerOf`): body size × health × **a full stomach** (the hunger bar).
* Past *Blurry From* (35%) the Drink button has **look-alikes** — *Dirnk*, *Drnik*, *Drink?* — in a shuffled order:
  clicking one **spills the mug** (and costs everyone 1.5 s). The drunker, the more of them.
* Past *Sick From* (70%) a mug can come back up: out.
* **Give up** any time. The last one standing wins; if everyone goes down in the same round, the one who drank most.
* Afterwards everyone is **Drunk** (*Drunk Effect*: less critical chance and attack speed, slower walking), for up to
  *Drunk Seconds* (4 min) and as strongly as they were drunk. The Animator's `SeatValue` is the drunkenness (0-1) while
  seated — sway with it.

---

## 6. Animator parameters

*Tools ▸ SimpleMovements ▸ Player ▸ Complete Player Animator Controller* adds them:

| Parameter | Type | Meaning |
|---|---|---|
| `IsSitting` | Bool | seated at any table (the Sit state) |
| `SeatValue` | Float | what the game gives the seat: arm wrestling −1..1 (losing → winning), drinking 0..1 (drunk), 0 in card games |

---

## 7. Writing another game

The table takes any `CardGameDefinition`. Besides what [Minigames 01 §6](01-Card-Games-and-Truco.md#6-adding-another-card-game)
shows, a definition can say:

| Member | Use |
|---|---|
| `PlaysWithEmptySeats`, `MinPlayers` | seats may stay empty (blackjack, poker), and how many players the game needs |
| `CanPlay(player, out reason)` | who may sit and press Ready (arm wrestling: an arm) |
| `PowerOf(player)`, `BotPower(rng)` | a strength the table measures for each seat at the start (`CardGameSetup.power`) |
| `AfterGame(session, players, rng, log)` | what the game does to the real players afterwards (a strained arm, drunkenness) |

and a session:

| Member | Use |
|---|---|
| `Payouts`, `PayoutsFromHouse` | chip games: what each seat gets back (`ChipGameSession` does it from the chips) |
| `PlayerLeft(seat, name)` | a player left: their team forfeits (default), or the game goes on without them |
| `view.meters`, `seat.value` | bars in the window (centred for a tug of war) and the Animator's `SeatValue` |

Real-time games (arm wrestling) are ticked every frame on the server and limit their own action rate; the table sends
views at most every *Send Interval* (0.1 s) over the network.

**Put the definition in a file named after it** (`MyGameDefinition.cs` for `class MyGameDefinition`): Unity binds a
ScriptableObject's script to the class named like its file. A definition sharing a file with its session class
(`MyGame.cs`) can still be created from the menu, but the asset loses its script ("'MyGame' is missing the class attribute
'ExtensionOfNativeClass'"), the table's *Game* field reads empty and the table falls back to Truco.

## 8. Tests

*Test Runner ▸ EditMode ▸ MinigameTests*: blackjack totals and soft aces, the dealer drawing to 17 and the round's
payouts, whole bot games, leaving mid-round; poker hand ranks (with the A-5 straight and kickers, best five of seven), the
heads-up blinds, chips never created or lost over 60 bot games of 2-6 players (side pots, all-ins), returned over-raises;
arm wrestling's countdown and push limit, the stronger arm winning most matches; the drinking contest's timer, the
look-alike buttons, and bot contests ending.
