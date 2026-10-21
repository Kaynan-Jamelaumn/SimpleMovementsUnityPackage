#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;

namespace SimpleMovements.Tests
{
    /// <summary>Edit Mode tests of the card framework and Truco (Window ▸ General ▸ Test Runner ▸ EditMode).</summary>
    public class TrucoTests
    {
        private static TrucoRules Rules() => new TrucoRules(new TrucoSettings());

        private static PlayingCard C(CardRank r, CardSuit s) => new PlayingCard(r, s);

        private static TrucoGame Game(bool bots, int seed = 7)
        {
            var g = new TrucoGame(new TrucoSettings { trickPauseSeconds = 0f, handPauseSeconds = 0f },
                new CardGameSetup { seed = seed, names = new[] { "A1", "B1", "A2", "B2" }, bots = new[] { bots, bots, bots, bots }, firstDealer = 0 });
            g.Start();
            return g;
        }

        // ------------------------------------------------------------------ cards
        [Test]
        public void Deck_Has52DifferentCards_AndCodesRoundTrip()
        {
            CardDeck deck = CardDeck.Standard();
            Assert.AreEqual(52, deck.Count);
            var seen = new HashSet<byte>();
            foreach (PlayingCard c in deck.Cards)
            {
                Assert.IsTrue(seen.Add(c.Code), c.ToString());
                Assert.AreEqual(c, PlayingCard.FromCode(c.Code));
            }
            deck.Shuffle(new System.Random(3));
            Assert.AreEqual(52, deck.Count);
        }

        [Test]
        public void Deal_ThreeEach_ThenTheVira()
        {
            TrucoGame g = Game(false);
            for (int s = 0; s < 4; s++)
                Assert.AreEqual(3, g.Player(s).Hand.Count);
            var v = new CardTableView();
            for (int i = 0; i < 4; i++) v.seats.Add(new CardSeatView());
            g.FillView(v, 0);
            Assert.AreEqual(27, v.deckCount, "40 - 12 dealt - the vira");
            Assert.AreEqual(1, v.shared.Count);
            Assert.AreEqual(3, v.hand.Count, "the viewer sees their own hand");
            g.FillView(v, -1);
            Assert.AreEqual(0, v.hand.Count, "an onlooker sees no hand");
        }

        // ------------------------------------------------------------------ rules
        [Test]
        public void Deck_IsTheFortyCardDeck_NoEightsNinesOrTens()
        {
            CardDeck deck = Rules().NewDeck();
            Assert.AreEqual(40, deck.Count);
            foreach (PlayingCard c in deck.Cards)
                Assert.IsFalse(c.rank == CardRank.Eight || c.rank == CardRank.Nine || c.rank == CardRank.Ten, c.ToString());
            Assert.AreEqual(52, new TrucoRules(new TrucoSettings { fortyCardDeck = false, rankSequence = new List<CardRank>
            {
                CardRank.Four, CardRank.Five, CardRank.Six, CardRank.Seven, CardRank.Eight, CardRank.Nine, CardRank.Ten,
                CardRank.Queen, CardRank.Jack, CardRank.King, CardRank.Ace, CardRank.Two, CardRank.Three,
            } }).NewDeck().Count, "the 52-card variant");
        }

        [Test]
        public void Manilha_IsTheNextRankAfterTheVira()
        {
            TrucoRules r = Rules();
            Assert.AreEqual(CardRank.Queen, r.ManilhaRankFor(C(CardRank.Seven, CardSuit.Hearts)), "vira 7 → Q (no 8, 9, 10)");
            Assert.AreEqual(CardRank.Jack, r.ManilhaRankFor(C(CardRank.Queen, CardSuit.Clubs)), "Q → J");
            Assert.AreEqual(CardRank.King, r.ManilhaRankFor(C(CardRank.Jack, CardSuit.Clubs)), "J → K");
            Assert.AreEqual(CardRank.Ace, r.ManilhaRankFor(C(CardRank.King, CardSuit.Clubs)), "K → A");
            Assert.AreEqual(CardRank.Four, r.ManilhaRankFor(C(CardRank.Three, CardSuit.Spades)), "3 wraps to 4");
        }

        [Test]
        public void Strength_ManilhasFirstBySuit_ThenThreeDownToFour()
        {
            TrucoRules r = Rules();
            CardRank m = CardRank.Jack;
            Assert.AreEqual(0, r.Strength(C(CardRank.Jack, CardSuit.Clubs), m));
            Assert.AreEqual(1, r.Strength(C(CardRank.Jack, CardSuit.Hearts), m));
            Assert.AreEqual(2, r.Strength(C(CardRank.Jack, CardSuit.Spades), m));
            Assert.AreEqual(3, r.Strength(C(CardRank.Jack, CardSuit.Diamonds), m));
            Assert.Less(r.Compare(C(CardRank.Jack, CardSuit.Diamonds), C(CardRank.Three, CardSuit.Clubs), m), 0, "the weakest manilha beats a 3");
            CardRank[] order = { CardRank.Three, CardRank.Two, CardRank.Ace, CardRank.King, CardRank.Queen,
                                 CardRank.Seven, CardRank.Six, CardRank.Five, CardRank.Four };
            for (int i = 1; i < order.Length; i++)
                Assert.Less(r.Compare(C(order[i - 1], CardSuit.Hearts), C(order[i], CardSuit.Hearts), m), 0, $"{order[i - 1]} beats {order[i]}");
            Assert.AreEqual(0, r.Compare(C(CardRank.Three, CardSuit.Hearts), C(CardRank.Three, CardSuit.Clubs), m), "suits do not matter outside manilhas");
        }

        [Test]
        public void Trick_TiesWhenBothTeamsHaveTheTopCard()
        {
            TrucoRules r = Rules();
            CardRank m = CardRank.Five;
            int Team(int s) => s % 2;
            var plays = new List<(int, PlayingCard)>
            {
                (0, C(CardRank.Three, CardSuit.Hearts)), (1, C(CardRank.Three, CardSuit.Clubs)), (2, C(CardRank.Four, CardSuit.Clubs)), (3, C(CardRank.Ace, CardSuit.Clubs)),
            };
            Assert.AreEqual(TrucoRules.Tie, r.TrickWinner(plays, m, Team, out _));
            plays[1] = (1, C(CardRank.King, CardSuit.Clubs));
            plays[2] = (2, C(CardRank.Three, CardSuit.Spades));
            Assert.AreEqual(0, r.TrickWinner(plays, m, Team, out int best), "two equal cards of the same team win");
            Assert.AreEqual(0, best, "the first of them");
            plays[3] = (3, C(CardRank.Five, CardSuit.Diamonds));
            Assert.AreEqual(1, r.TrickWinner(plays, m, Team, out best), "a manilha wins");
            Assert.AreEqual(3, best);
        }

        [Test]
        public void Hand_TieRules()
        {
            int T = TrucoRules.Tie, U = TrucoRules.Undecided;
            Assert.AreEqual(0, TrucoRules.HandWinner(new[] { 0, 0, U }, 2), "the first two tricks");
            Assert.AreEqual(1, TrucoRules.HandWinner(new[] { 1, T, U }, 2), "the first trick and a tie");
            Assert.AreEqual(0, TrucoRules.HandWinner(new[] { T, 0, U }, 2), "a tied first trick: the second decides");
            Assert.AreEqual(U, TrucoRules.HandWinner(new[] { 0, 1, U }, 2), "one each: play the third");
            Assert.AreEqual(U, TrucoRules.HandWinner(new[] { T, T, U }, 2));
            Assert.AreEqual(1, TrucoRules.HandWinner(new[] { T, T, 1 }, 3), "two ties: the third decides");
            Assert.AreEqual(TrucoRules.Draw, TrucoRules.HandWinner(new[] { T, T, T }, 3), "three ties: a draw");
            Assert.AreEqual(1, TrucoRules.HandWinner(new[] { 0, 1, 1 }, 3));
            Assert.AreEqual(0, TrucoRules.HandWinner(new[] { 0, 1, T }, 3), "one each and a tie: the first trick's winner");
        }

        // ------------------------------------------------------------------ calls
        [Test]
        public void Truco_AcceptRaiseAndWhoMayCall()
        {
            TrucoGame g = Game(false);
            int caller = g.Turn;
            int callerTeam = g.TeamOf(caller);
            int opponent = (caller + 1) % 4;
            Assert.IsFalse(g.TryAct(opponent, new CardAction(TrucoGame.Call), out _), "only on your turn");
            Assert.IsTrue(g.TryAct(caller, new CardAction(TrucoGame.Call), out string why), why);
            Assert.AreEqual(TrucoGame.Phase.Responding, g.State);
            Assert.IsFalse(g.TryAct(caller, new CardAction(CardGameSession.PlayCard, 0), out _), "no card until the call is answered");
            Assert.IsFalse(g.TryAct((caller + 2) % 4, new CardAction(TrucoGame.Accept), out _), "the caller's partner cannot answer");
            Assert.IsTrue(g.TryAct(opponent, new CardAction(TrucoGame.Accept), out why), why);
            Assert.AreEqual(3, g.HandValue);
            Assert.AreEqual(TrucoGame.Phase.Playing, g.State);
            Assert.IsFalse(g.Betting.CanCall(callerTeam, g.Settings, out _), "the team that called cannot raise its own call");
            Assert.IsTrue(g.Betting.CanCall(1 - callerTeam, g.Settings, out _), "the other team can ask for six");
        }

        [Test]
        public void Fold_GivesThePreviouslyAcceptedValue()
        {
            TrucoGame g = Game(false);
            int caller = g.Turn;
            int callerTeam = g.TeamOf(caller);
            int opponent = (caller + 1) % 4;
            Assert.IsTrue(g.TryAct(caller, new CardAction(TrucoGame.Call), out _));
            Assert.IsTrue(g.TryAct(opponent, new CardAction(TrucoGame.Raise), out string why), why);
            Assert.AreEqual(1 - callerTeam, g.Betting.CallerTeam, "now the raisers wait for an answer");
            Assert.IsTrue(g.TryAct((caller + 2) % 4, new CardAction(TrucoGame.Fold), out why), why);
            Assert.AreEqual(1, g.Score(1 - callerTeam), "the hand was still worth 1 when they folded");
            Assert.AreEqual(0, g.Score(callerTeam));
        }

        [Test]
        public void Play_OnlyYourOwnCardsOnYourTurn()
        {
            TrucoGame g = Game(false);
            int t = g.Turn;
            Assert.IsFalse(g.TryAct((t + 1) % 4, new CardAction(CardGameSession.PlayCard, 0), out _), "not their turn");
            Assert.IsFalse(g.TryAct(t, new CardAction(CardGameSession.PlayCard, 3), out _), "no fourth card");
            Assert.IsTrue(g.TryAct(t, new CardAction(CardGameSession.PlayCard, 0), out string why), why);
            Assert.AreEqual(2, g.Player(t).Hand.Count);
            Assert.AreEqual((t + 1) % 4, g.Turn, "clockwise");
        }

        // ------------------------------------------------------------------ hand of eleven
        /// <summary>Plays bot actions until <paramref name="until"/> holds or the game ends.</summary>
        private static void Drive(TrucoGame g, System.Func<bool> until)
        {
            for (int step = 0; step < 20000 && !g.IsOver && !until(); step++)
            {
                bool acted = false;
                for (int s = 0; s < 4 && !acted; s++)
                    if (g.WantsBotAction(s))
                    {
                        Assert.IsTrue(g.TryAct(s, g.BotDecide(s), out string why), why);
                        acted = true;
                    }
                if (!acted)
                    g.Tick(0.5f);
            }
        }

        [Test]
        public void HandOfEleven_TeamSeesPartnerCards_NoCalls_ThenDecides()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                // First to 2: a team at 1 point plays the next hand as a hand of eleven.
                var g = new TrucoGame(new TrucoSettings { pointsToWin = 2, trickPauseSeconds = 0f, handPauseSeconds = 0f },
                    new CardGameSetup { seed = seed, names = new[] { "A1", "B1", "A2", "B2" }, bots = new[] { true, true, true, true }, firstDealer = 0 });
                g.Start();
                Drive(g, () => g.State == TrucoGame.Phase.ElevenDecision || g.Kind == TrucoGame.HandKind.IronHand);
                if (g.IsOver)
                    continue; // a first hand worth 2 (an accepted Truco) ended it at once
                if (g.Kind == TrucoGame.HandKind.IronHand)
                {
                    Assert.IsFalse(g.CanCallNow(0, out _), "no calls when both teams are at eleven");
                    Assert.IsFalse(g.CanCallNow(1, out _));
                    continue;
                }
                int team = g.ElevenTeam;
                Assert.GreaterOrEqual(team, 0);
                Assert.IsFalse(g.CanCallNow(0, out _), "nobody calls in a hand of eleven");
                Assert.IsFalse(g.CanCallNow(1, out _));
                var v = new CardTableView();
                for (int i = 0; i < 4; i++) v.seats.Add(new CardSeatView());
                g.FillView(v, team); // seat `team` (0 or 1) is on the team at eleven
                Assert.AreEqual(3, v.seats[team + 2].revealed.Count, "the partner's hand is shown");
                Assert.AreEqual(0, v.seats[1 - team].revealed.Count, "the opponents' hands stay hidden");
                g.FillView(v, 1 - team);
                Assert.AreEqual(0, v.seats[team].revealed.Count, "the other team sees nothing");
                Assert.IsFalse(g.TryAct(1 - team, new CardAction(TrucoGame.PlayEleven), out _), "only the team at eleven decides");
                Assert.IsTrue(g.TryAct(team, new CardAction(TrucoGame.PlayEleven), out string why), why);
                Assert.AreEqual(3, g.HandValue, "a hand of eleven is worth 3");
                Assert.AreEqual(TrucoGame.Phase.Playing, g.State);
                return;
            }
            Assert.Fail("no seed reached a hand of eleven");
        }

        [Test]
        public void HandOfEleven_Running_GivesTheOtherTeamOne()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var g = new TrucoGame(new TrucoSettings { pointsToWin = 3, trickPauseSeconds = 0f, handPauseSeconds = 0f },
                    new CardGameSetup { seed = seed, names = new[] { "A1", "B1", "A2", "B2" }, bots = new[] { true, true, true, true }, firstDealer = 0 });
                g.Start();
                Drive(g, () => g.State == TrucoGame.Phase.ElevenDecision);
                if (g.IsOver || g.State != TrucoGame.Phase.ElevenDecision)
                    continue;
                int team = g.ElevenTeam, before = g.Score(1 - team);
                Assert.IsTrue(g.TryAct(team, new CardAction(TrucoGame.RunEleven), out string why), why);
                Assert.AreEqual(before + 1, g.Score(1 - team));
                return;
            }
            Assert.Fail("no seed reached a hand of eleven");
        }

        // ------------------------------------------------------------------ whole games
        [Test]
        public void BotsPlayAWholeGameToTwelve()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                TrucoGame g = Game(true, seed);
                for (int step = 0; step < 20000 && !g.IsOver; step++)
                {
                    bool acted = false;
                    for (int s = 0; s < 4 && !acted; s++)
                        if (g.WantsBotAction(s))
                        {
                            Assert.IsTrue(g.TryAct(s, g.BotDecide(s), out string why), $"seed {seed}: {why}");
                            acted = true;
                        }
                    if (!acted)
                        g.Tick(0.5f);
                }
                Assert.IsTrue(g.IsOver, $"seed {seed}: the game ends");
                Assert.GreaterOrEqual(g.Score(g.WinningTeam), 12);
            }
        }

        [Test]
        public void View_SurvivesTheNetwork()
        {
            TrucoGame g = Game(false);
            var v = new CardTableView { title = "Truco", stakeText = "10 gold each", viewerSeat = 2, tablePhase = 1 };
            for (int i = 0; i < 4; i++) v.seats.Add(new CardSeatView { name = "P" + i, occupied = true, objectId = 100 + i });
            g.FillView(v, 2);
            v.log.Add("hello");
            CardTableView back = CardTableView.FromBytes(v.ToBytes());
            Assert.IsNotNull(back);
            Assert.AreEqual(v.status, back.status);
            Assert.AreEqual(v.viewerSeat, back.viewerSeat);
            Assert.AreEqual(v.hand, back.hand);
            Assert.AreEqual(v.shared, back.shared);
            Assert.AreEqual(v.teamScores, back.teamScores);
            Assert.AreEqual(4, back.seats.Count);
            Assert.AreEqual(103, back.seats[3].objectId);
            Assert.AreEqual(v.actions.Count, back.actions.Count);
            Assert.AreEqual("hello", back.log[back.log.Count - 1]);
        }
    }
}
#endif
