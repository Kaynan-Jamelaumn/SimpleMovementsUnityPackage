#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;

namespace SimpleMovements.Tests
{
    /// <summary>Edit Mode tests of blackjack, poker, arm wrestling and the drinking contest (Test Runner ▸ EditMode).</summary>
    public class MinigameTests
    {
        private static PlayingCard C(CardRank r, CardSuit s) => new PlayingCard(r, s);

        private static CardGameSetup Setup(int seats, bool bots, int seed, float[] power = null) => new CardGameSetup
        {
            seed = seed,
            names = new[] { "A", "B", "C", "D", "E", "F" },
            bots = new[] { bots, bots, bots, bots, bots, bots },
            seated = new[] { true, true, true, true, true, true },
            firstDealer = 0,
            power = power,
        };

        /// <summary>Runs bots (and the clock) until the game ends; false if it never does.</summary>
        private static bool PlayOut(CardGameSession g, int seats, int maxSteps = 200000, float dt = 0.05f)
        {
            for (int step = 0; step < maxSteps && !g.IsOver; step++)
            {
                bool acted = false;
                for (int s = 0; s < seats && !acted; s++)
                    if (g.WantsBotAction(s))
                    {
                        Assert.IsTrue(g.TryAct(s, g.BotDecide(s), out string why), why);
                        acted = true;
                    }
                g.Tick(dt);
            }
            return g.IsOver;
        }

        // ------------------------------------------------------------------ blackjack
        [Test]
        public void Blackjack_Totals_SoftAcesAndNaturals()
        {
            Assert.AreEqual(21, BlackjackGame.Total(new[] { C(CardRank.Ace, CardSuit.Clubs), C(CardRank.King, CardSuit.Hearts) }, out bool soft));
            Assert.IsTrue(soft);
            Assert.IsTrue(BlackjackGame.IsBlackjack(new[] { C(CardRank.Ace, CardSuit.Clubs), C(CardRank.Ten, CardSuit.Hearts) }));
            Assert.AreEqual(21, BlackjackGame.Total(new[] { C(CardRank.Ace, CardSuit.Clubs), C(CardRank.Ace, CardSuit.Hearts), C(CardRank.Nine, CardSuit.Spades) }, out _));
            Assert.AreEqual(25, BlackjackGame.Total(new[] { C(CardRank.King, CardSuit.Clubs), C(CardRank.Queen, CardSuit.Hearts), C(CardRank.Five, CardSuit.Spades) }, out _));
            Assert.AreEqual(17, BlackjackGame.Total(new[] { C(CardRank.Ace, CardSuit.Clubs), C(CardRank.Six, CardSuit.Hearts), C(CardRank.King, CardSuit.Spades) }, out soft));
            Assert.IsFalse(soft, "the ace counts 1 there");
            Assert.IsFalse(BlackjackGame.IsBlackjack(new[] { C(CardRank.Seven, CardSuit.Clubs), C(CardRank.Seven, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Spades) }));
        }

        [Test]
        public void Blackjack_DealerDrawsTo17_AndPaysTheRound()
        {
            var settings = new BlackjackSettings { rounds = 1, roundPauseSeconds = 100f, dealerCardSeconds = 0f };
            var g = new BlackjackGame(settings, Setup(2, false, 3), 2);
            g.Start();
            Assert.IsFalse(g.TryAct(0, new CardAction(BlackjackGame.Bet, 1000), out _), "not that many chips");
            Assert.IsTrue(g.TryAct(0, new CardAction(BlackjackGame.Bet, 10), out string why), why);
            Assert.IsTrue(g.TryAct(1, new CardAction(BlackjackGame.Bet, 25), out why), why);
            Assert.AreEqual(BlackjackGame.Phase.Players, g.State, "everyone bet: the cards are dealt");
            for (int guard = 0; guard < 10 && g.State == BlackjackGame.Phase.Players; guard++)
                Assert.IsTrue(g.TryAct(g.Turn, new CardAction(BlackjackGame.Stand), out why), why);
            for (int guard = 0; guard < 20 && g.State == BlackjackGame.Phase.Dealer; guard++)
                g.Tick(0.1f);
            Assert.AreEqual(BlackjackGame.Phase.Pause, g.State);
            int dealer = BlackjackGame.Total(g.DealerHand, out _);
            Assert.GreaterOrEqual(dealer, 17, "the dealer draws to 17");
            for (int s = 0; s < 2; s++)
            {
                int p = BlackjackGame.Total(g.Hand(s), out _);
                int bet = s == 0 ? 10 : 25;
                int expected = BlackjackGame.IsBlackjack(g.Hand(s)) && !BlackjackGame.IsBlackjack(g.DealerHand) ? 100 + bet + bet / 2
                    : BlackjackGame.IsBlackjack(g.DealerHand) && !BlackjackGame.IsBlackjack(g.Hand(s)) ? 100 - bet
                    : dealer > 21 || p > dealer ? 100 + bet : p == dealer ? 100 : 100 - bet;
                Assert.AreEqual(expected, g.Chips(s), $"seat {s}: {p} against {dealer}");
            }
        }

        [Test]
        public void Blackjack_BotsPlayWholeGames()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var g = new BlackjackGame(new BlackjackSettings { roundPauseSeconds = 0.1f, dealerCardSeconds = 0.05f }, Setup(4, true, seed), 4);
                g.Start();
                Assert.IsTrue(PlayOut(g, 4), $"seed {seed}");
                float[] pay = g.Payouts;
                for (int s = 0; s < 4; s++)
                {
                    Assert.GreaterOrEqual(g.Chips(s), 0);
                    Assert.AreEqual(g.Chips(s) / 100f, pay[s], 1e-4);
                }
            }
        }

        [Test]
        public void Blackjack_APlayerLeavingLosesTheirChips_TheOthersGoOn()
        {
            var g = new BlackjackGame(new BlackjackSettings(), new CardGameSetup
            {
                seed = 6, names = new[] { "Ana", "Bia", "Bot" }, bots = new[] { false, false, true }, seated = new[] { true, true, true },
            }, 3);
            g.Start();
            Assert.IsTrue(g.TryAct(0, new CardAction(BlackjackGame.Bet, 50), out string why), why);
            g.PlayerLeft(0, "Ana");
            Assert.IsFalse(g.IsOver, "another player is still at the table");
            Assert.AreEqual(0f, g.Payouts[0], 1e-4, "leaving loses your chips, the bet included");
            Assert.IsTrue(g.TryAct(1, new CardAction(BlackjackGame.Bet, 10), out why), why);
            Assert.IsTrue(g.TryAct(2, new CardAction(BlackjackGame.Bet, 10), out why), why);
            Assert.AreEqual(BlackjackGame.Phase.Players, g.State, "the round goes on without her");
        }

        [Test]
        public void Blackjack_TheGameEndsWhenNoPlayerIsLeft()
        {
            var g = new BlackjackGame(new BlackjackSettings(), new CardGameSetup
            {
                seed = 5, names = new[] { "Human", "Bot" }, bots = new[] { false, true }, seated = new[] { true, true },
            }, 2);
            g.Start();
            g.PlayerLeft(0, "Human");
            Assert.IsTrue(g.IsOver, "no player is left at the table: the game ends");
            Assert.AreEqual(0f, g.Payouts[0], 1e-4);
        }

        // ------------------------------------------------------------------ poker
        private static int Score(params PlayingCard[] cards) => PokerHands.Best(cards);

        [Test]
        public void Poker_HandsRankInOrder()
        {
            int royal = Score(C(CardRank.Ace, CardSuit.Spades), C(CardRank.King, CardSuit.Spades), C(CardRank.Queen, CardSuit.Spades), C(CardRank.Jack, CardSuit.Spades), C(CardRank.Ten, CardSuit.Spades));
            int quads = Score(C(CardRank.Nine, CardSuit.Spades), C(CardRank.Nine, CardSuit.Hearts), C(CardRank.Nine, CardSuit.Clubs), C(CardRank.Nine, CardSuit.Diamonds), C(CardRank.Two, CardSuit.Spades));
            int full = Score(C(CardRank.Three, CardSuit.Spades), C(CardRank.Three, CardSuit.Hearts), C(CardRank.Three, CardSuit.Clubs), C(CardRank.Two, CardSuit.Diamonds), C(CardRank.Two, CardSuit.Spades));
            int flush = Score(C(CardRank.Two, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Hearts), C(CardRank.Nine, CardSuit.Hearts), C(CardRank.Jack, CardSuit.Hearts), C(CardRank.King, CardSuit.Hearts));
            int straight = Score(C(CardRank.Six, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Clubs), C(CardRank.Eight, CardSuit.Hearts), C(CardRank.Nine, CardSuit.Spades), C(CardRank.Ten, CardSuit.Hearts));
            int wheel = Score(C(CardRank.Ace, CardSuit.Hearts), C(CardRank.Two, CardSuit.Clubs), C(CardRank.Three, CardSuit.Hearts), C(CardRank.Four, CardSuit.Spades), C(CardRank.Five, CardSuit.Hearts));
            int trips = Score(C(CardRank.Four, CardSuit.Spades), C(CardRank.Four, CardSuit.Hearts), C(CardRank.Four, CardSuit.Clubs), C(CardRank.King, CardSuit.Diamonds), C(CardRank.Two, CardSuit.Spades));
            int twoPair = Score(C(CardRank.Four, CardSuit.Spades), C(CardRank.Four, CardSuit.Hearts), C(CardRank.King, CardSuit.Clubs), C(CardRank.King, CardSuit.Diamonds), C(CardRank.Two, CardSuit.Spades));
            int pairK = Score(C(CardRank.Ace, CardSuit.Spades), C(CardRank.Ace, CardSuit.Hearts), C(CardRank.King, CardSuit.Clubs), C(CardRank.Seven, CardSuit.Diamonds), C(CardRank.Two, CardSuit.Spades));
            int pairQ = Score(C(CardRank.Ace, CardSuit.Clubs), C(CardRank.Ace, CardSuit.Diamonds), C(CardRank.Queen, CardSuit.Clubs), C(CardRank.Seven, CardSuit.Hearts), C(CardRank.Two, CardSuit.Hearts));
            int high = Score(C(CardRank.Ace, CardSuit.Spades), C(CardRank.Jack, CardSuit.Hearts), C(CardRank.Nine, CardSuit.Clubs), C(CardRank.Seven, CardSuit.Diamonds), C(CardRank.Two, CardSuit.Spades));
            int[] order = { royal, quads, full, flush, straight, wheel, trips, twoPair, pairK, pairQ, high };
            for (int i = 1; i < order.Length; i++)
                Assert.Greater(order[i - 1], order[i], $"#{i - 1} beats #{i}");
            Assert.AreEqual("Royal Flush", PokerHands.Name(royal));
            Assert.AreEqual(PokerHands.Straight, PokerHands.Category(wheel), "A-2-3-4-5 is a straight");
            int seven = Score(C(CardRank.Two, CardSuit.Hearts), C(CardRank.Seven, CardSuit.Hearts), C(CardRank.Nine, CardSuit.Hearts),
                C(CardRank.King, CardSuit.Clubs), C(CardRank.King, CardSuit.Hearts), C(CardRank.Three, CardSuit.Hearts), C(CardRank.King, CardSuit.Spades));
            Assert.AreEqual(PokerHands.Flush, PokerHands.Category(seven), "the best five of seven: a flush beats three kings");
        }

        [Test]
        public void Poker_HeadsUp_ButtonPostsTheSmallBlindAndActsFirst()
        {
            var g = new PokerGame(new PokerSettings(), new CardGameSetup { seed = 2, names = new[] { "A", "B" }, bots = new[] { false, false }, firstDealer = 0 }, 2);
            g.Start();
            Assert.AreEqual(0, g.Button);
            Assert.AreEqual(1, g.StreetBet(0), "the button posts the small blind");
            Assert.AreEqual(2, g.StreetBet(1));
            Assert.AreEqual(0, g.Turn, "and acts first before the flop");
            Assert.IsFalse(g.TryAct(0, new CardAction(PokerGame.Check), out _), "1 to call");
            Assert.IsTrue(g.TryAct(0, new CardAction(PokerGame.Call), out string why), why);
            Assert.AreEqual(1, g.Turn, "the big blind has its option");
            Assert.IsTrue(g.TryAct(1, new CardAction(PokerGame.Check), out why), why);
            Assert.AreEqual(1, g.Street, "the flop");
            Assert.AreEqual(3, g.Board.Count);
            Assert.AreEqual(1, g.Turn, "after the flop the big blind acts first");
        }

        [Test]
        public void Poker_ChipsAreNeverCreatedOrLost()
        {
            for (int seed = 1; seed <= 60; seed++)
            {
                int seats = 2 + seed % 5;
                var g = new PokerGame(new PokerSettings { handPauseSeconds = 0f, runOutSeconds = 0f, botBoldness = 0.9f, maxHands = 25 }, Setup(seats, true, seed), seats);
                g.Start();
                for (int step = 0; step < 200000 && !g.IsOver; step++)
                {
                    int total = g.Pot;
                    for (int s = 0; s < seats; s++) total += g.Chips(s);
                    Assert.AreEqual(seats * 100, total, $"seed {seed}, hand {g.HandNumber}");
                    bool acted = false;
                    for (int s = 0; s < seats && !acted; s++)
                        if (g.WantsBotAction(s))
                        {
                            Assert.IsTrue(g.TryAct(s, g.BotDecide(s), out string why), $"seed {seed}: {why}");
                            acted = true;
                        }
                    g.Tick(0.05f);
                }
                Assert.IsTrue(g.IsOver, $"seed {seed} ends");
                float pay = 0f;
                foreach (float p in g.Payouts) pay += p;
                Assert.AreEqual(seats, pay, 1e-3, "what one wins another lost");
            }
        }

        [Test]
        public void Poker_AllInOverRaisesAreReturned()
        {
            // Two players, the big blind shoves for 100: the button folds; the shover gets everything back plus the blind.
            var g = new PokerGame(new PokerSettings { handPauseSeconds = 100f }, new CardGameSetup { seed = 9, names = new[] { "A", "B" }, bots = new[] { false, false }, firstDealer = 0 }, 2);
            g.Start();
            Assert.IsTrue(g.TryAct(0, new CardAction(PokerGame.Call), out string why), why);
            Assert.IsTrue(g.TryAct(1, new CardAction(PokerGame.AllIn), out why), why);
            Assert.IsTrue(g.TryAct(0, new CardAction(PokerGame.Fold), out why), why);
            Assert.AreEqual(102, g.Chips(1));
            Assert.AreEqual(98, g.Chips(0));
        }

        // ------------------------------------------------------------------ arm wrestling
        [Test]
        public void ArmWrestling_PushesWaitForGo_AndAreRateLimited()
        {
            var g = new ArmWrestlingGame(new ArmWrestlingSettings { countdownSeconds = 1f }, Setup(2, false, 1));
            g.Start();
            Assert.IsFalse(g.TryAct(0, new CardAction(ArmWrestlingGame.Push), out _), "not before GO!");
            g.Tick(1.1f);
            Assert.AreEqual(ArmWrestlingGame.Phase.Pulling, g.State);
            Assert.IsTrue(g.TryAct(0, new CardAction(ArmWrestlingGame.Push), out string why), why);
            Assert.Greater(g.Position, 0f, "seat 1's push moves the arms its way");
            Assert.IsFalse(g.TryAct(0, new CardAction(ArmWrestlingGame.Push), out _), "too fast");
            g.Tick(0.2f);
            Assert.IsTrue(g.TryAct(0, new CardAction(ArmWrestlingGame.Push), out why), why);
        }

        [Test]
        public void ArmWrestling_TheStrongerArmWinsMostMatches()
        {
            int strongWins = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var g = new ArmWrestlingGame(new ArmWrestlingSettings { countdownSeconds = 0.1f, roundPauseSeconds = 0.1f },
                    Setup(2, true, seed, new[] { 1.4f, 0.8f }));
                g.Start();
                Assert.IsTrue(PlayOut(g, 2, 100000, 0.03f), $"seed {seed}");
                if (g.WinningTeam == 0) strongWins++;
            }
            Assert.Greater(strongWins, 30, "strength matters");
        }

        // ------------------------------------------------------------------ drinking contest
        [Test]
        public void Drinking_TooSlowIsOut_AndDrunkPlayersSeeLookAlikes()
        {
            var g = new DrinkingContestGame(new DrinkingSettings { secondsToDrink = 2f, roundPauseSeconds = 0.1f, mugStrength = 20f, sickFrom = 100f },
                Setup(3, false, 4), 3);
            g.Start();
            Assert.IsTrue(g.TryAct(0, new CardAction(DrinkingContestGame.Drink), out string why), why);
            Assert.IsTrue(g.TryAct(1, new CardAction(DrinkingContestGame.Drink), out why), why);
            Assert.IsFalse(g.TryAct(0, new CardAction(DrinkingContestGame.Drink), out _), "one mug per round");
            g.Tick(2.5f);
            Assert.IsFalse(g.InGame(2), "seat 3 never drank");
            Assert.IsTrue(g.InGame(0) && g.InGame(1));
            g.Tick(0.2f);
            for (int round = 0; round < 3; round++)
            {
                g.TryAct(0, new CardAction(DrinkingContestGame.Drink), out _);
                g.TryAct(1, new CardAction(DrinkingContestGame.Drink), out _);
                g.Tick(0.2f);
            }
            var v = new CardTableView();
            for (int i = 0; i < 3; i++) v.seats.Add(new CardSeatView { occupied = true });
            g.FillView(v, 0);
            int drinkButtons = 0;
            foreach (CardActionOption a in v.actions)
                if (a.action.kind == DrinkingContestGame.Drink || a.action.kind == DrinkingContestGame.Spill) drinkButtons++;
            Assert.Greater(g.Drunkenness(0), 35f);
            Assert.Greater(drinkButtons, 1, "the Drink button has look-alikes");
        }

        [Test]
        public void Drinking_BotsDrinkUntilOneIsLeft()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var g = new DrinkingContestGame(new DrinkingSettings { roundPauseSeconds = 0.1f }, Setup(4, true, seed), 4);
                g.Start();
                Assert.IsTrue(PlayOut(g, 4, 100000, 0.1f), $"seed {seed}");
                if (g.WinningTeam >= 0)
                    Assert.IsTrue(g.InGame(g.WinningTeam) || g.Mugs(g.WinningTeam) > 0);
            }
        }
    }
}
#endif
