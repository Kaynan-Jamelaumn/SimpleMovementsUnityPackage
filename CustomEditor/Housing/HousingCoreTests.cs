#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HousingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the housing rules (plain C#: no scene): the geometry of areas, base rents, inns (stays, prices,
    /// limits, the checkout hour), the market of houses (sealed binding orders paid as offered, with their notice; each
    /// player one house and their first choice; sitting tenants protected unless outbid beyond their protection; renewals
    /// that never rise faster than allowed; each house's asking rent from binding offers and vacancy only), permissions
    /// (managers give only what they have; removing one removes everyone under them), held items and saving.
    /// </summary>
    public class HousingCoreTests
    {
        // ---------------------------------------------------------------- helpers
        private static List<Vector2> Square(float x, float y, float size) =>
            new List<Vector2> { new Vector2(x, y), new Vector2(x + size, y), new Vector2(x + size, y + size), new Vector2(x, y + size) };

        private static HousingRules Rules()
        {
            HousingRules r = HousingRules.Defaults();
            r.market.firstClearingDelayHours = 0f;
            r.market.minOnlineHours = 0f;
            return r;
        }

        private static PropertyFacts House(string id, string district = "Old Town", int rentPerDay = 10) => new PropertyFacts
        {
            id = id, name = "House " + id, district = district, landArea = 100f, houseFootprint = 50f, floors = 1, baseRentPerDay = rentPerDay, weekly = true, monthly = true,
        };

        private static HousingBook Book(HousingRules rules, int houses, string district = "Old Town", int rentPerDay = 10)
        {
            var b = new HousingBook(rules);
            b.Load(new HousingSave());
            var list = new List<PropertyFacts>();
            for (int i = 0; i < houses; i++) list.Add(House("h" + i, district, rentPerDay));
            b.SetWorld(list, null);
            return b;
        }

        /// <summary>Places an order the way the world does: quote, pay, place.</summary>
        private static void Order(HousingBook b, string key, double now, params (string house, long perDay)[] items)
        {
            var list = items.Select(i => new OrderItem { property = i.house, term = (int)LeaseTerm.Week, rateMilli = i.perDay * 1000 }).ToList();
            OrderQuote q = b.QuoteOrder(key, list, now);
            Assert.IsTrue(q.ok, q.reason);
            Assert.IsTrue(b.PlaceOrder(key, key.ToUpperInvariant(), list, Math.Max(0, q.delta), now, out string why), why);
        }

        private static long Owed(HousingBook b, string key) => b.Account(key)?.owed ?? 0;

        /// <summary>Moves to the clearing after <paramref name="now"/> (and a little beyond).</summary>
        private static double ToNextClearing(HousingBook b, double now)
        {
            double t = b.NextClearing(now) + 0.001;
            b.Advance(t, DateTime.UtcNow.Ticks);
            return t;
        }

        // ---------------------------------------------------------------- geometry
        [Test]
        public void TouchingAreasDoNotOverlapCrossingOnesDo()
        {
            List<Vector2> a = Square(0, 0, 4), touching = Square(4, 0, 4), half = Square(2, 0, 4);
            Assert.AreEqual(16f, HousingGeometry.Area(a), 1e-4f);
            Assert.IsFalse(HousingGeometry.Overlap(a, touching), "sharing a fence is not overlapping");
            Assert.AreEqual(8f, HousingGeometry.SharedArea(a, half), 1e-3f);
            var bar = new List<Vector2> { new Vector2(-1, 1.5f), new Vector2(5, 1.5f), new Vector2(5, 2.5f), new Vector2(-1, 2.5f) };
            var post = new List<Vector2> { new Vector2(1.5f, -1), new Vector2(2.5f, -1), new Vector2(2.5f, 5), new Vector2(1.5f, 5) };
            Assert.AreEqual(1f, HousingGeometry.SharedArea(bar, post), 1e-3f, "a cross overlaps where its arms meet, with no corner inside the other");
            Assert.IsTrue(HousingGeometry.Overlap(a, new List<Vector2>(a)), "the same area overlaps itself");
        }

        [Test]
        public void ConcaveAreasAreMeasuredExactly()
        {
            // An L: a 4x4 square without its top-right 2x2 quarter.
            var l = new List<Vector2> { new Vector2(0, 0), new Vector2(4, 0), new Vector2(4, 2), new Vector2(2, 2), new Vector2(2, 4), new Vector2(0, 4) };
            Assert.AreEqual(12f, HousingGeometry.Area(l), 1e-4f);
            Assert.AreEqual(12f, HousingGeometry.Triangulate(l).Sum(t => HousingGeometry.Area(t)), 1e-3f, "the triangles cover it exactly");
            Assert.AreEqual(0f, HousingGeometry.SharedArea(l, Square(2.5f, 2.5f, 1)), 1e-4f, "the missing quarter is not part of it");
            Assert.IsTrue(HousingGeometry.Holds(l, Square(0.5f, 0.5f, 1)));
            Assert.IsFalse(HousingGeometry.Holds(l, Square(1.5f, 1.5f, 2)), "a square reaching into the missing quarter");
            var bowTie = new List<Vector2> { new Vector2(0, 0), new Vector2(2, 2), new Vector2(2, 0), new Vector2(0, 2) };
            Assert.IsFalse(HousingGeometry.IsSimple(bowTie));
            Assert.IsTrue(HousingGeometry.IsSimple(l));
        }

        [Test]
        public void AreasTurnWithTheirObject()
        {
            List<Vector2> local = HousingGeometry.Rectangle(Vector2.zero, new Vector2(4, 2));
            List<Vector2> world = HousingGeometry.ToWorld(local, new Vector3(10, 0, 5), Quaternion.Euler(0, 90, 0));
            Rect r = HousingGeometry.Bounds(world);
            Assert.AreEqual(2f, r.width, 1e-3f, "turned a quarter, 4 m along X becomes 4 m along Z");
            Assert.AreEqual(4f, r.height, 1e-3f);
            Assert.AreEqual(8f, HousingGeometry.Area(world), 1e-3f);
        }

        // ---------------------------------------------------------------- base rents
        [Test]
        public void BaseRentFollowsSizesDistrictAndQuality()
        {
            HousingRules r = Rules();
            r.districts = new List<HousingDistrict> { new HousingDistrict { name = "Old Town", desirability = 1f }, new HousingDistrict { name = "Hill", desirability = 2f } };
            var small = new PropertyFacts { district = "Old Town", landArea = 100, houseFootprint = 40, floors = 1, quality = 1 };
            var big = new PropertyFacts { district = "Old Town", landArea = 300, houseFootprint = 80, floors = 2, quality = 1, gardenArea = 40 };
            double s = HousingPricing.BaseDaily(small, r), b = HousingPricing.BaseDaily(big, r);
            Assert.Greater(b, s * 2, "a bigger house on more land with a garden");
            small.district = "Hill";
            Assert.AreEqual(s * 2, HousingPricing.BaseDaily(small, r), 1e-6, "a wanted district");
            small.quality = 1.5f;
            Assert.AreEqual(s * 3, HousingPricing.BaseDaily(small, r), 1e-6, "quality");
            small.baseRentPerDay = 7;
            Assert.AreEqual(7.0, HousingPricing.BaseDaily(small, r), 1e-9, "a rent set by hand is that rent");
            var tiny = new PropertyFacts { landArea = 1, houseFootprint = 1, quality = 0.25f };
            Assert.AreEqual(r.minDailyRent, HousingPricing.BaseDaily(tiny, r), 1e-9, "never below the least rent");
        }

        // ---------------------------------------------------------------- inns
        private static HousingBook InnBook(HousingRules r, int rooms, int nightly = 20)
        {
            var b = new HousingBook(r);
            b.Load(new HousingSave());
            var inn = new InnFacts { id = "inn", name = "The Goose", district = "Docks", nightlyPrice = nightly };
            for (int i = 0; i < rooms; i++) inn.rooms.Add(new InnRoomFacts { id = "r" + i, name = "Room " + (i + 1), quality = 1f });
            b.SetWorld(null, new[] { inn });
            return b;
        }

        [Test]
        public void AStayGivesAFreeRoomUntilItEnds()
        {
            HousingBook b = InnBook(Rules(), 2);
            InnQuote q = b.QuoteStay("inn", null, "ann", 3, 0.0);
            Assert.IsTrue(q.ok, q.reason);
            Assert.AreEqual(q.nightly * 3, q.total);
            Assert.IsTrue(b.Stay("inn", null, "ann", "Ann", 3, q.total, 0.0, out string why), why);
            Assert.AreEqual(HousingAccess.All, b.AccessOf("inn", q.roomId, "ann", 1.0), "her room");
            Assert.AreEqual(HousingAccess.None, b.AccessOf("inn", q.roomId, "bob", 1.0), "not his");
            Assert.IsFalse(b.QuoteStay("inn", null, "ann", 1, 0.5).ok, "one room per inn");
            Assert.IsFalse(b.Stay("inn", null, "bob", "Bob", 1, q.total + 999, 0.5, out _), "a stale price is refused (the money goes back)");
            b.Events.Clear();
            b.Advance(q.end + 0.01, DateTime.UtcNow.Ticks);
            Assert.AreEqual(HousingAccess.None, b.AccessOf("inn", q.roomId, "ann", q.end + 0.02), "the stay ended");
            Assert.IsTrue(b.Events.Any(e => e.kind == HousingEventKind.TenancyEnded && e.key == "ann"), "her chest is emptied for her");
        }

        [Test]
        public void TheLastRoomsCostMoreAndAFullInnRefuses()
        {
            HousingBook b = InnBook(Rules(), 4);
            long first = b.QuoteStay("inn", null, "k0", 1, 0.0).nightly;
            long last = 0;
            for (int i = 0; i < 4; i++)
            {
                InnQuote q = b.QuoteStay("inn", null, "k" + i, 1, 0.0);
                Assert.IsTrue(q.ok, q.reason);
                last = q.nightly;
                Assert.IsTrue(b.Stay("inn", null, "k" + i, "K", 1, q.total, 0.0, out _));
            }
            Assert.Greater(last, first, "the last free room costs more");
            Assert.IsFalse(b.QuoteStay("inn", null, "k9", 1, 0.0).ok, "every room is taken");
        }

        [Test]
        public void ABusyInnRaisesItsPricesAndAnEmptyOneLowersThem()
        {
            HousingRules r = Rules();
            HousingBook busy = InnBook(r, 4), empty = InnBook(r, 4);
            double t = 0.0;
            for (int day = 0; day < 6; day++, t += 1.0)
            {
                for (int i = 0; i < 4; i++)
                {
                    InnQuote q = busy.QuoteStay("inn", null, "k" + i, 1, t);
                    if (q.ok) busy.Stay("inn", null, "k" + i, "K", 1, q.total, t, out _);
                }
                // The guests are there (rooms booked and never used would not count).
                for (double h = t; h < t + 0.999; h += 0.1)
                {
                    busy.Advance(h, DateTime.UtcNow.Ticks);
                    for (int i = 0; i < 4; i++) busy.Present("inn", "r" + i, h);
                }
                busy.Advance(t + 0.999, DateTime.UtcNow.Ticks);
                empty.Advance(t + 0.999, DateTime.UtcNow.Ticks);
            }
            Assert.Greater(busy.InnLevel("inn", t).level, 1.05, "full for days: dearer");
            Assert.Less(empty.InnLevel("inn", t).level, 0.95, "empty for days: cheaper");
            Assert.GreaterOrEqual(empty.InnLevel("inn", t).level, r.inns.minLevel - 1e-6);
        }

        [Test]
        public void AStayEndsAtTheCheckoutHourOnGameTime()
        {
            HousingBook b = InnBook(Rules(), 1);
            b.HourOfDay = 22.0; // renting at 22:00
            double end = b.StayEnd(10.0 + 22.0 / 24.0, 1);
            Assert.AreEqual(10.0 + 1.0 + 10.0 / 24.0, end, 1e-6, "until 10:00 the next morning");
            b.HourOfDay = 9.0; // at 9:00 a night is not one hour long
            Assert.AreEqual(10.0 + 9.0 / 24.0 + 25.0 / 24.0, b.StayEnd(10.0 + 9.0 / 24.0, 1), 1e-6, "until 10:00 the next day");
            b.HourOfDay = -1.0;
            Assert.AreEqual(13.0, b.StayEnd(10.0, 3), 1e-9, "real time: whole days");
        }

        // ---------------------------------------------------------------- the market
        [Test]
        public void AnOrderWinsAFreeHouseAtItsOffer()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 1);
            long asking = b.AskingMilli("h0");
            Assert.AreEqual(10000, asking, "a new market asks the base rent");
            Order(b, "ann", 0.0, ("h0", 12));
            ToNextClearing(b, 0.0);
            PropertyRecord lease = b.LeaseOf("h0", "ann");
            Assert.IsNotNull(lease, "she rents it");
            Assert.AreEqual(12000, lease.lease.rateMilli, "at what she offered");
            Assert.AreEqual(0, Owed(b, "ann"), "the deposit was exactly the term");
            Assert.IsNull(b.OrderOf("ann"), "winning ends the order");
            Assert.AreEqual(asking, b.AskingMilli("h0"), "one player offering more teaches the market nothing: others are asked what it asked");
        }

        [Test]
        public void TheBestOfferWinsAndNobodyElsesOfferChangesWhatItPays()
        {
            HousingBook b = Book(Rules(), 1);
            Order(b, "ann", 0.0, ("h0", 12));
            Order(b, "bob", 0.0, ("h0", 20));
            Order(b, "eve", 0.0, ("h0", 19)); // a shill just below the winner
            ToNextClearing(b, 0.0);
            Assert.IsNotNull(b.LeaseOf("h0", "bob"));
            Assert.AreEqual(20000, b.LeaseOf("h0", "bob").lease.rateMilli, "he pays his own offer, whatever the others offered");
            Assert.AreEqual(10100, b.AskingMilli("h0"), 1, "others still offer more for it: it asks a step more (towards the best losing offer, never beyond)");
            Assert.AreEqual(0, Owed(b, "ann"), "a losing order stands (for the next time the house is offered)");
            Assert.IsNotNull(b.OrderOf("eve"));
            Assert.IsTrue(b.CancelOrder("eve", 1.0, out _));
            Assert.AreEqual(0, Owed(b, "eve"), "an order is binding: it stands for the order notice");
            b.Advance(1.0 + b.OrderNoticeDays + 0.01, DateTime.UtcNow.Ticks);
            Assert.IsNull(b.OrderOf("eve"));
            Assert.AreEqual(19 * 7, Owed(b, "eve"), "then it is withdrawn and its deposit owed back in full");
        }

        [Test]
        public void APlayerBestOnTwoHousesGetsTheirFirstChoiceAndTheNextBestOfferTheOther()
        {
            HousingBook b = Book(Rules(), 2);
            Order(b, "ann", 0.0, ("h1", 15), ("h0", 30)); // ann likes h1 best, offers more for h0
            Order(b, "bob", 0.0, ("h0", 11));
            ToNextClearing(b, 0.0);
            Assert.IsNotNull(b.LeaseOf("h1", "ann"), "her first choice");
            Assert.IsNotNull(b.LeaseOf("h0", "bob"), "so h0 goes to the next best offer");
            Assert.AreEqual(15 * 7, b.Save.citySink - 11 * 7, "each pays only for the house they got");
        }

        [Test]
        public void OffersBelowTheAskingRentAreRefused()
        {
            HousingBook b = Book(Rules(), 1);
            OrderQuote q = b.QuoteOrder("ann", new List<OrderItem> { new OrderItem { property = "h0", term = 0, rateMilli = 9000 } }, 0.0);
            Assert.IsFalse(q.ok);
        }

        private static HousingBook Rented(HousingRules r, out double t, int offer = 10)
        {
            HousingBook b = Book(r, 1);
            Order(b, "ann", 0.0, ("h0", offer));
            t = ToNextClearing(b, 0.0);
            Assert.IsNotNull(b.LeaseOf("h0", "ann"));
            return b;
        }

        /// <summary>Deposits enough for a few renewals and moves to just after the notice of the lease's end.</summary>
        private static double FundAndNotice(HousingBook b, string key, string house, double now)
        {
            Assert.IsTrue(b.Deposited(key, key, b.DepositRoom(key)));
            PropertyRecord p = b.LeaseOf(house, key);
            double at = p.lease.end - b.Rules.market.renewalNoticeHours / 24.0 + 0.001;
            b.Advance(at, DateTime.UtcNow.Ticks);
            Assert.IsTrue(p.noticed, "the renewal is fixed a day before");
            return at;
        }

        [Test]
        public void ASittingTenantKeepsTheHouseAgainstOffersWithinTheirProtection()
        {
            HousingRules r = Rules();
            HousingBook b = Rented(r, out double t);
            double at = FundAndNotice(b, "ann", "h0", t);
            PropertyRecord p = b.LeaseOf("h0", "ann");
            long renewTotal = p.renewTotal;
            Assert.Greater(renewTotal, 0);
            // Protection by default: twice the rent; an offer must beat 1.1 times that.
            Order(b, "bob", at, ("h0", 21));
            double end = p.lease.end;
            b.Advance(end + 0.001, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h0", "ann"), "21 a day does not beat a protection of 20 x 1.1 = 22");
            Assert.AreEqual(renewTotal, b.Save.citySink - 70, "she paid the renewal fixed at the notice: the losing offer changed nothing");
            Assert.IsNotNull(b.OrderOf("bob"), "his order still stands");
        }

        [Test]
        public void AnOfferBeyondTheProtectionTakesTheHouse()
        {
            HousingRules r = Rules();
            HousingBook b = Rented(r, out double t);
            long deposited = b.DepositRoom("ann");
            double at = FundAndNotice(b, "ann", "h0", t);
            Order(b, "bob", at, ("h0", 23));
            double end = b.LeaseOf("h0", "ann").lease.end;
            b.Events.Clear();
            b.Advance(end + 0.001, DateTime.UtcNow.Ticks);
            Assert.IsNull(b.LeaseOf("h0", "ann"));
            Assert.IsNotNull(b.LeaseOf("h0", "bob"), "23 beats 22");
            Assert.AreEqual(23000, b.LeaseOf("h0", "bob").lease.rateMilli, "at his offer");
            Assert.IsTrue(b.Events.Any(e => e.kind == HousingEventKind.TenancyEnded && e.key == "ann"), "her things are held for her");
            Assert.AreEqual(deposited, b.Account("ann").balance, "what she set aside for the renewal is back in her account");
        }

        [Test]
        public void ARenewalNeverRisesFasterThanAllowed()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 3);
            Order(b, "ann", 0.0, ("h0", 10));
            double t = ToNextClearing(b, 0.0);
            // A hot market: her house now asks three times what it did when she moved in.
            b.Save.properties.Find(x => x.id == "h0").level = Math.Log(3.0);
            Assert.Greater(b.AskingMilli("h0"), 25000, "it asks far more now");
            double at = FundAndNotice(b, "ann", "h0", t);
            PropertyRecord p = b.LeaseOf("h0", "ann");
            double cap = 10000 * Math.Exp(r.market.maxRiseRatePerDay * r.weekDays);
            Assert.LessOrEqual(p.renewRateMilli, (long)Math.Ceiling(cap), "a week's rise is capped");
            Assert.GreaterOrEqual(p.renewRateMilli, 10000, "and it does not fall below what she pays in a rising market");
        }

        [Test]
        public void AnUnfundedRenewalEndsTheLeaseAndTheHouseIsOfferedAgain()
        {
            HousingBook b = Rented(Rules(), out double t);
            PropertyRecord p = b.LeaseOf("h0", "ann");
            double end = p.lease.end;
            b.Advance(end + 0.001, DateTime.UtcNow.Ticks);
            Assert.IsNull(b.LeaseOf("h0", "ann"), "nothing in her rent account: the lease ended");
            Order(b, "bob", end + 0.01, ("h0", 11));
            ToNextClearing(b, end + 0.01);
            Assert.IsNotNull(b.LeaseOf("h0", "bob"), "offered again at the next clearing (no wait)");
        }

        [Test]
        public void AnUnletHouseAsksLessEachClearingWithinItsBounds()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 4);
            long before = b.AskingMilli("h0");
            double t = 0.0;
            for (int i = 0; i < 40; i++) t = ToNextClearing(b, t);
            long after = b.AskingMilli("h0");
            Assert.Less(after, before, "nobody wants it: it asks less");
            Assert.GreaterOrEqual(after, (long)(before * r.market.minLevel) - 1, "never below the lowest level");
        }

        [Test]
        public void AHouseSomeoneElseOffersMoreForAsksMoreTowardsThatOffer()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 3);
            double t = 0.0;
            for (int i = 0; i < 3; i++) Order(b, "t" + i, t, ("h" + i, 10));
            t = ToNextClearing(b, t);
            for (int i = 0; i < 3; i++) Assert.IsTrue(b.Deposited("t" + i, "T", b.DepositRoom("t" + i)));
            // A player without a home offers 11 a day for h0 (it cannot take it: its tenant is protected).
            Order(b, "w0", t, ("h0", 11));
            t = Run(b, t, 21.0);
            Assert.AreEqual(11000, b.AskingMilli("h0"), 1, "it asks what was offered for it - not more");
            Assert.AreEqual(10000, b.AskingMilli("h1"), "the houses nobody else wants do not move");
            Assert.LessOrEqual(b.LeaseOf("h0", "t0").lease.rateMilli, 11000, "its tenant's rent followed, no further");
            Assert.IsTrue(b.CancelOrder("w0", t, out _));
            t = Run(b, t, 21.0);
            Assert.AreEqual(10000, b.AskingMilli("h0"),
                "the offer taken back, nobody else stands behind a premium: it asks its base rent again");
        }

        [Test]
        public void NewcomersWaitBeforeTheirOrdersCount()
        {
            HousingRules r = Rules();
            r.market.minOnlineHours = 3f;
            HousingBook b = Book(r, 1);
            b.RequireResidency = true;
            var items = new List<OrderItem> { new OrderItem { property = "h0", term = 0, rateMilli = 12000 } };
            Assert.IsFalse(b.QuoteOrder("ann", items, 0.0).ok, "a character made a minute ago");
            b.Seen("ann", "Ann", DateTime.UtcNow.Ticks, 3.5 * 3600.0);
            Assert.IsTrue(b.QuoteOrder("ann", items, 0.0).ok, "after a few hours in the city");
        }

        [Test]
        public void MovingKeepsTheOldHouseUntilItsTermThenLetsItGo()
        {
            HousingBook b = Book(Rules(), 2);
            Order(b, "ann", 0.0, ("h0", 10));
            double t = ToNextClearing(b, 0.0);
            Order(b, "ann", t, ("h1", 12));
            t = ToNextClearing(b, t);
            Assert.IsNotNull(b.LeaseOf("h1", "ann"), "the new house");
            PropertyRecord old = b.LeaseOf("h0", "ann");
            Assert.IsNotNull(old, "the old one until its term");
            Assert.IsTrue(old.movingOut);
            Assert.IsFalse(b.MayOrder("ann", out _) && b.LeasesOf("ann").Count > 2, "never more than one house beyond the limit");
            Assert.IsFalse(b.SetRenewal("h0", "ann", true, 2f, t, out _), "a lease being left cannot be renewed");
        }

        // ---------------------------------------------------------------- permissions
        [Test]
        public void ManagersGiveOnlyWhatTheyHaveAndGoWithThoseTheyLetIn()
        {
            HousingBook b = Rented(Rules(), out double t);
            Assert.IsTrue(b.Grant("h0", null, "ann", "mia", "Mia", HousingAccess.Enter | HousingAccess.Garden | HousingAccess.Manage, t, out string why), why);
            Assert.IsFalse(b.Grant("h0", null, "mia", "max", "Max", HousingAccess.Storage, t, out _), "Mia may not give the chests: she cannot open them");
            Assert.IsFalse(b.Grant("h0", null, "mia", "max", "Max", HousingAccess.Manage, t, out _), "nor Manage");
            Assert.IsTrue(b.Grant("h0", null, "mia", "max", "Max", HousingAccess.Garden, t, out why), why);
            Assert.AreEqual(HousingAccess.Garden, b.AccessOf("h0", null, "max", t));
            Assert.IsFalse(b.Grant("h0", null, "mia", "ann", "Ann", HousingAccess.None, t, out _), "nobody touches the tenant");
            Assert.IsTrue(b.Grant("h0", null, "ann", "mia", "Mia", HousingAccess.None, t, out why), why);
            Assert.AreEqual(HousingAccess.None, b.AccessOf("h0", null, "max", t), "those Mia let in went with her");
            Assert.AreEqual(HousingAccess.None, b.AccessOf("h0", null, "mia", t));
        }

        [Test]
        public void AFingerprintNamesAPlayerWithoutTheirKey()
        {
            HousingBook b = Rented(Rules(), out double t);
            b.Grant("h0", null, "ann", "c0123456789abcdef01234567", "Bob", HousingAccess.Friend, t, out _);
            string fp = b.Fingerprint("c0123456789abcdef01234567");
            Assert.AreEqual(4, fp.Length);
            Assert.AreNotEqual(fp, b.Fingerprint("c0123456789abcdef01234568"));
            Assert.IsNotNull(b.GrantByFingerprint("h0", null, fp, t));
        }

        // ---------------------------------------------------------------- held items and the save
        [Test]
        public void HeldItemsAreFreeForAFewDaysAndTooManyStopNewOrders()
        {
            HousingRules r = Rules();
            r.held.fee = 5;
            r.held.maxStacks = 2;
            HousingBook b = Book(r, 1);
            var stacks = new List<HeldStack> { new HeldStack { itemId = "a", count = 1 }, new HeldStack { itemId = "b", count = 2 }, new HeldStack { itemId = "c", count = 3 } };
            long id = b.Hold("ann", "Ann", "Room 1", stacks, 0.0);
            HeldBundle bundle = b.Held(id);
            Assert.AreEqual(0, b.ClaimFee(bundle, 1.0), "free at first");
            Assert.AreEqual(5, b.ClaimFee(bundle, r.held.freeDays + 1.0));
            Assert.IsFalse(b.MayOrder("ann", out _), "more held than allowed: claim first");
            b.Claimed(id, new List<HeldStack> { stacks[0] });
            Assert.IsTrue(b.MayOrder("ann", out string why), why);
        }

        [Test]
        public void TheBooksSaveAndLoadWhole()
        {
            HousingBook b = Rented(Rules(), out double t);
            b.Grant("h0", null, "ann", "bob", "Bob", HousingAccess.Friend, t, out _);
            Order(b, "eve", t, ("h0", 50));
            string json = JsonUtility.ToJson(b.Save);
            var back = new HousingBook(Rules());
            back.Load(JsonUtility.FromJson<HousingSave>(json));
            back.SetWorld(new[] { House("h0") }, null);
            Assert.AreEqual(b.LeaseOf("h0", "ann").lease.end, back.LeaseOf("h0", "ann").lease.end, 1e-9);
            Assert.AreEqual(HousingAccess.Friend, back.AccessOf("h0", null, "bob", t));
            Assert.AreEqual(50000, back.OrderOf("eve").items[0].rateMilli);
            Assert.AreEqual(b.Fingerprint("bob"), back.Fingerprint("bob"), "the secret is kept");
        }

        [Test]
        public void ATenantMovingAtTheEndOfTheirLeaseNeitherPaysForNorKeepsTheOldHouse()
        {
            HousingBook b = Book(Rules(), 2);
            Order(b, "ann", 0.0, ("h0", 10));
            double t = ToNextClearing(b, 0.0);
            double at = FundAndNotice(b, "ann", "h0", t);
            long before = b.Account("ann").balance + b.LeaseOf("h0", "ann").reserved;
            Order(b, "ann", at, ("h1", 12)); // she would rather move
            double end = b.LeaseOf("h0", "ann").lease.end;
            b.Advance(end + 0.001, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h1", "ann"), "she moved");
            Assert.IsNull(b.LeaseOf("h0", "ann"), "and the old lease ended with its term (not renewed)");
            Assert.AreEqual(before, b.Account("ann").balance, "what she had set aside for the old house is back in her account");
        }

        [Test]
        public void TheClearingDoesNotDependOnTheOrderOfTheBooks()
        {
            string Run(bool reversed)
            {
                HousingRules r = Rules();
                var b = new HousingBook(r);
                b.Load(new HousingSave());
                var houses = new List<PropertyFacts> { House("a"), House("b"), House("c") };
                if (reversed) houses.Reverse();
                b.SetWorld(houses, null);
                var orders = new List<(string key, (string, long)[] items)>
                {
                    ("k1", new[] { ("a", 15L), ("b", 14L) }),
                    ("k2", new[] { ("a", 14L), ("c", 11L) }),
                    ("k3", new[] { ("b", 16L) }),
                    ("k4", new[] { ("c", 12L), ("a", 13L) }),
                };
                if (reversed) orders.Reverse();
                foreach (var o in orders) Order(b, o.key, 0.0, o.items);
                ToNextClearing(b, 0.0);
                var sb = new System.Text.StringBuilder();
                foreach (string h in new[] { "a", "b", "c" })
                    foreach (string k in new[] { "k1", "k2", "k3", "k4" })
                        if (b.LeaseOf(h, k) != null) sb.Append($"{h}={k}@{b.LeaseOf(h, k).lease.rateMilli};");
                return sb.ToString();
            }
            Assert.AreEqual(Run(false), Run(true));
            Assert.AreEqual("a=k1@15000;b=k3@16000;c=k4@12000;", Run(false), "each house to its best offer, each player one house");
        }
            // ---------------------------------------------------------------- the second review: market
        private static HousingBook World(HousingRules r, params (string id, string district, int rent)[] houses)
        {
            var b = new HousingBook(r);
            b.Load(new HousingSave());
            b.SetWorld(houses.Select(h => House(h.id, h.district, h.rent)).ToList(), null);
            return b;
        }

        /// <summary>Every house of the list let at <paramref name="perDay"/> to its own tenant ("t" + house id), each with a full rent account.</summary>
        private static double LetAll(HousingBook b, double t, IEnumerable<string> houses, long perDay)
        {
            var list = houses.ToList();
            foreach (string h in list) Order(b, "t" + h, t, (h, perDay > 0 ? perDay : (b.AskingMilli(h) + 999) / 1000));
            t = ToNextClearing(b, t);
            foreach (string h in list)
            {
                Assert.IsNotNull(b.LeaseOf(h, "t" + h), h + " is let");
                Assert.IsTrue(b.Deposited("t" + h, "T", b.DepositRoom("t" + h)));
            }
            return t;
        }

        private static double Run(HousingBook b, double t, double days)
        {
            for (double end = t + days; t < end;) t = ToNextClearing(b, t);
            return t;
        }

        [Test]
        public void AnOfferTakenBackLiftsNothingFromThenButStaysBinding()
        {
            HousingRules r = Rules();
            HousingBook kept = Book(r, 3), dropped = Book(r, 3);
            double t = LetAll(kept, 0.0, new[] { "h0", "h1", "h2" }, 10);
            LetAll(dropped, 0.0, new[] { "h0", "h1", "h2" }, 10);
            Order(kept, "w", t, ("h0", 20), ("h1", 20));
            Order(dropped, "w", t, ("h0", 20), ("h1", 20));
            double t2 = Run(kept, t, 1.0);
            dropped.Advance(t2, DateTime.UtcNow.Ticks);
            long lifted = dropped.AskingMilli("h0");
            Assert.Greater(lifted, 10200, "a day of a binding offer above it lifted h0");
            var onlyH1 = new List<OrderItem> { new OrderItem { property = "h1", term = 0, rateMilli = 20000 } };
            OrderQuote q = dropped.QuoteOrder("w", onlyH1, t2);
            Assert.IsTrue(q.ok && q.later, "dropping a house waits for the notice");
            Assert.IsTrue(dropped.PlaceOrder("w", "W", onlyH1, Math.Max(0, q.delta), t2, out string why), why);
            t2 = ToNextClearing(kept, t2);
            dropped.Advance(t2, DateTime.UtcNow.Ticks);
            Assert.AreEqual(10000, dropped.AskingMilli("h0"), "from the moment it is taken back, the offer holds no premium up");
            Assert.Greater(kept.AskingMilli("h0"), lifted, "where it stands, h0 goes on rising towards it");
            Assert.AreEqual(2, dropped.OrderOf("w").items.Count, "but the order stands as it was until the notice ends: it may still win h0");
        }

        [Test]
        public void AHouseNeverAsksMoreThanTheBestOfferForIt()
        {
            HousingRules r = Rules();
            HousingBook b = World(r, ("s0", "Hill", 5), ("s1", "Hill", 5), ("m0", "Hill", 10), ("m1", "Hill", 10), ("b0", "Hill", 30), ("b1", "Hill", 30));
            double t = LetAll(b, 0.0, new[] { "s0", "s1", "m0", "m1", "b0", "b1" }, 0); // each at its own asking rent
            // Players without a home offer exactly what the big houses ask: nobody offers a cent more.
            for (int i = 0; i < 4; i++) Order(b, "w" + i, t, (i % 2 == 0 ? "b0" : "b1", 30));
            t = Run(b, t, 30.0);
            Assert.AreEqual(30000, b.AskingMilli("b0"), "offers at the asking rent lift nothing");
            Assert.AreEqual(5000, b.AskingMilli("s0"), "and nothing else moves either");
            foreach (string h in new[] { "s0", "s1", "m0", "m1", "b0", "b1" }) b.Deposited("t" + h, "T", b.DepositRoom("t" + h));
            for (int i = 0; i < 4; i++) Order(b, "v" + i, t, (i % 2 == 0 ? "b0" : "b1", 36));
            for (int week = 0; week < 8; week++)
            {
                t = Run(b, t, 7.0);
                foreach (string h in new[] { "s0", "s1", "m0", "m1", "b0", "b1" }) b.Deposited("t" + h, "T", b.DepositRoom("t" + h));
            }
            Assert.Greater(b.AskingMilli("b0"), 30000, "offers above the asking rent lift the house they name");
            Assert.LessOrEqual(b.AskingMilli("b0"), 36000, "never beyond what was offered for it");
            Assert.AreEqual(5000, b.AskingMilli("s0"), "the small houses nobody named do not move");
        }

        [Test]
        public void ALetHouseRisesAtEveryClearingNotOnlyAtItsLeaseEnd()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 3);
            var list = new[] { "h0", "h1", "h2" };
            foreach (string h in list)
            {
                var items = new List<OrderItem> { new OrderItem { property = h, term = (int)LeaseTerm.Month, rateMilli = 10000 } };
                OrderQuote q = b.QuoteOrder("t" + h, items, 0.0);
                Assert.IsTrue(b.PlaceOrder("t" + h, "T", items, q.delta, 0.0, out string why), why);
            }
            double t = ToNextClearing(b, 0.0);
            Order(b, "w0", t, ("h0", 15));
            t = Run(b, t, 7.0); // months-long leases: no lease ends in this week
            Assert.Greater(b.AskingMilli("h0"), 13000, "about 1 % a clearing, towards the offer, while it stands");
        }

        [Test]
        public void ALoneUnwantedHouseDoesNotDragItsDistrictDown()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 10);
            var let = Enumerable.Range(0, 9).Select(i => "h" + i).ToList();
            double t = LetAll(b, 0.0, let, 10);
            bool taken = false;
            for (double end = t + 60.0; t < end;)
            {
                if (!taken && b.AskingMilli("h9") <= 5000)
                {
                    Order(b, "late", t, ("h9", b.AskingMilli("h9") / 1000 + 1));
                    taken = true;
                    t = ToNextClearing(b, t);
                    Assert.IsTrue(b.Deposited("late", "Late", b.DepositRoom("late")), "she pays her rent ahead");
                    continue;
                }
                t = ToNextClearing(b, t);
            }
            Assert.IsNotNull(b.LeaseOf("h9", "late"), "the unwanted house found its price");
            Assert.Greater(b.DistrictLevelOf("Old Town"), 0.98, "one empty house among ten barely moves its neighbours' rents");
            Assert.GreaterOrEqual(b.LeaseOf("h0", "th0").lease.rateMilli, 9800, "the others' tenants still pay what they did");
        }

        [Test]
        public void ADiscountNeverRaisesASittingTenantsRent()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 4);
            double t = LetAll(b, 0.0, new[] { "h1", "h2", "h3" }, 10);
            t = Run(b, t, 3.0); // h0 stands empty: it asks less
            long asking = b.AskingMilli("h0");
            Assert.Less(asking, 10000);
            Order(b, "ann", t, ("h0", (asking + 999) / 1000));
            t = ToNextClearing(b, t);
            PropertyRecord p = b.LeaseOf("h0", "ann");
            Assert.IsNotNull(p);
            Assert.IsTrue(b.Deposited("ann", "Ann", b.DepositRoom("ann")));
            long rate = p.lease.rateMilli;
            for (int week = 0; week < 8; week++)
            {
                t = Run(b, t, 7.0);
                Assert.IsNotNull(b.LeaseOf("h0", "ann"), "renewed");
                Assert.LessOrEqual(p.lease.rateMilli, rate, "the market stood still: her rent did not rise");
                rate = p.lease.rateMilli;
            }
        }

        [Test]
        public void AHouseThatIsNotRenewedComesFreeForEveryoneToSee()
        {
            HousingRules r = Rules();
            r.market.firstClearingDelayHours = 24f;
            HousingBook b = Book(r, 1);
            Order(b, "ann", 0.0, ("h0", 10));
            double t = Run(b, 0.0, 1.5);
            PropertyRecord p = b.LeaseOf("h0", "ann");
            Assert.IsNotNull(p, "she rents it");
            Assert.IsTrue(b.SetRenewal("h0", "ann", false, 0f, t, out string why), why);
            double end = p.lease.end;
            b.Advance(end - 0.5, DateTime.UtcNow.Ticks);
            Assert.IsTrue(b.StatusOf("h0", "carol", end - 0.5).comesFree, "from the notice everyone sees it comes free");
            Order(b, "bob", end - 0.4, ("h0", 10)); // her friend, told in private
            b.Advance(end + 0.001, DateTime.UtcNow.Ticks);
            Assert.IsNull(b.LeaseOf("h0", "bob"), "it is not handed on at its lease's end");
            Assert.IsTrue(b.StatusOf("h0", "carol", end + 0.001).vacant, "it is free");
            Order(b, "carol", end + 0.1, ("h0", 12));
            t = Run(b, end + 0.1, 1.5);
            Assert.IsNotNull(b.LeaseOf("h0", "carol"), "a day later the best offer takes it");
        }

        [Test]
        public void MaxHousesTwoKeepsTwoHomes()
        {
            HousingRules r = Rules();
            r.market.maxHouses = 2;
            HousingBook b = Book(r, 3);
            Order(b, "ann", 0.0, ("h0", 10));
            double t = ToNextClearing(b, 0.0);
            Order(b, "ann", t, ("h1", 10));
            t = ToNextClearing(b, t);
            Assert.IsFalse(b.LeaseOf("h0", "ann").movingOut, "two homes are allowed");
            Assert.IsFalse(b.LeaseOf("h1", "ann").movingOut);
            Order(b, "ann", t, ("h2", 10));
            t = ToNextClearing(b, t);
            Assert.IsTrue(b.LeaseOf("h0", "ann").movingOut, "a third: the one ending soonest is let go at its term");
            Assert.IsFalse(b.LeaseOf("h1", "ann").movingOut);
            Assert.IsFalse(b.LeaseOf("h2", "ann").movingOut);
        }

        [Test]
        public void AWithdrawalThePurseCannotTakeIsPutBackWhateverTheCap()
        {
            HousingBook b = Book(Rules(), 1);
            Assert.IsTrue(b.Deposited("ann", "Ann", 0) || true);
            b.UndoWithdraw("ann", "Ann", 500); // as if 500 stayed in her account after her lease ended
            Assert.AreEqual(0, b.AccountCap("ann"), "no lease: no cap");
            long taken = b.Withdraw("ann", 500);
            Assert.AreEqual(500, taken);
            b.UndoWithdraw("ann", "Ann", taken); // her purse refused it
            Assert.AreEqual(500, b.Account("ann").balance, "back where it was");
        }

        [Test]
        public void TheTermIsTheOneTheDepositWasTakenFor()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 1);
            Order(b, "ann", 0.0, ("h0", 10)); // a week at 10: 70 held
            r.weekDays = 14; // the rules change before the clearing
            double t = ToNextClearing(b, 0.0);
            PropertyRecord p = b.LeaseOf("h0", "ann");
            Assert.AreEqual(7, p.lease.termDays, "the week she paid for");
            Assert.AreEqual(70, b.Save.citySink, "and the city took what she paid, not more");
        }

        [Test]
        public void LeasesStayOnTheClearingsWhenTickHoursChange()
        {
            HousingRules r = Rules();
            HousingBook b = Rented(r, out double t);
            Assert.IsTrue(b.Deposited("ann", "Ann", b.DepositRoom("ann")));
            r.market.tickHours = 5f;
            for (double end = t + 30.0; t < end; t += 0.02)
            {
                b.Advance(t, DateTime.UtcNow.Ticks);
                Assert.IsNotNull(b.TenancyOf("h0", null, t), $"her paid lease holds at {t:0.00}");
            }
        }

        [Test]
        public void ATenantSeesTheirLeaseWhileItsHouseIsNotLoaded()
        {
            HousingRules r = Rules();
            HousingBook b = Rented(r, out double t);
            FundAndNotice(b, "ann", "h0", t);
            b.SetWorld(new List<PropertyFacts>(), null); // its scene unloaded
            PropertyStatus st = b.StatusOf("h0", "ann", t);
            Assert.IsFalse(st.known, "not listed");
            Assert.IsTrue(st.mine && st.noticed && st.renewTotal > 0, "but her lease is shown as the books hold it");
        }

        [Test]
        public void TheRentAccountAlwaysHoldsAWholeRenewal()
        {
            HousingRules r = Rules();
            r.market.maxPrepaidTerms = 1;
            HousingBook b = Rented(r, out double t);
            FundAndNotice(b, "ann", "h0", t);
            PropertyRecord p = b.LeaseOf("h0", "ann");
            Assert.AreEqual(p.renewTotal, p.reserved, "a deposit of all she may covers the renewal, premium included");
        }

        [Test]
        public void AnOrderLiftsOnlyTheHousesItNames()
        {
            HousingRules r = Rules();
            HousingBook b = World(r, ("a0", "A", 10), ("a1", "A", 10), ("a2", "A", 10), ("b0", "B", 10), ("b1", "B", 10), ("b2", "B", 10));
            double t = LetAll(b, 0.0, new[] { "a0", "a1", "a2", "b0", "b1", "b2" }, 10);
            Order(b, "w", t, ("a0", 15), ("b0", 15));
            t = Run(b, t, 14.0);
            Assert.Greater(b.AskingMilli("a0"), 10000);
            Assert.Greater(b.AskingMilli("b0"), 10000);
            foreach (string h in new[] { "a1", "a2", "b1", "b2" }) Assert.AreEqual(10000, b.AskingMilli(h), h + " was not named: it does not move");
        }

        [Test]
        public void GardenZonesArePricedAtTheirOwnRate()
        {
            HousingRules r = Rules();
            r.zonePerSquareMetre[0] = 0.2f;
            var plain = new PropertyFacts { landArea = 200, houseFootprint = 50 };
            var zoned = new PropertyFacts { landArea = 200, houseFootprint = 50 };
            zoned.zoneArea[0] = 50; // LandUse.Garden
            double extra = HousingPricing.BaseDaily(zoned, r) - HousingPricing.BaseDaily(plain, r);
            Assert.AreEqual(50 * (0.2 - r.landPerSquareMetre), extra, 1e-6, "a garden zone at the Garden zone rate instead of plain land");
        }

        [Test]
        public void TheLeastOfferNamedIsAccepted()
        {
            HousingBook b = Book(Rules(), 1, rentPerDay: 7);
            var low = new List<OrderItem> { new OrderItem { property = "h0", term = 0, rateMilli = 1 } };
            OrderQuote q = b.QuoteOrder("ann", low, 0.0);
            Assert.IsFalse(q.ok);
            string tail = q.reason.Substring(q.reason.IndexOf("at least ", StringComparison.Ordinal) + "at least ".Length);
            string number = new string(tail.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
            long named = (long)Math.Round(double.Parse(number, System.Globalization.CultureInfo.InvariantCulture) * 1000.0);
            Assert.IsTrue(b.QuoteOrder("ann", new List<OrderItem> { new OrderItem { property = "h0", term = 0, rateMilli = named } }, 0.0).ok, q.reason);
        }

        // ---------------------------------------------------------------- the second review: inns, grants, held items
        [Test]
        public void RoomsBookedAndNeverUsedDoNotRaiseThePrice()
        {
            HousingRules r = Rules();
            HousingBook b = InnBook(r, 4);
            double t = 0.0;
            for (int day = 0; day < 6; day++, t += 1.0)
            {
                for (int i = 0; i < 3; i++)
                {
                    InnQuote q = b.QuoteStay("inn", null, "alt" + i, 1, t);
                    if (q.ok) b.Stay("inn", null, "alt" + i, "Alt", 1, q.total, t, out _);
                }
                b.Advance(t + 0.999, DateTime.UtcNow.Ticks);
            }
            Assert.LessOrEqual(b.InnLevel("inn", t).level, 1.0, "never used: no dearer");
        }

        [Test]
        public void TooMuchHeldStopsNewStaysToo()
        {
            HousingRules r = Rules();
            r.held.maxStacks = 2;
            HousingBook b = InnBook(r, 2);
            b.Hold("ann", "Ann", "Room 1", new List<HeldStack> { new HeldStack { itemId = "a", count = 1 }, new HeldStack { itemId = "b", count = 1 }, new HeldStack { itemId = "c", count = 1 } }, 0.0);
            Assert.IsFalse(b.QuoteStay("inn", null, "ann", 1, 0.1).ok, "claim first");
        }

        [Test]
        public void RemovingAManagerRemovesEveryoneUnderThem()
        {
            HousingBook b = Rented(Rules(), out double t);
            Assert.IsTrue(b.Grant("h0", null, "ann", "mia", "Mia", HousingAccess.All, t, out string why), why);
            Assert.IsTrue(b.Grant("h0", null, "mia", "max", "Max", HousingAccess.Enter, t, out why), why);
            Assert.IsTrue(b.Grant("h0", null, "ann", "max", "Max", HousingAccess.All, t, out why), why); // the tenant makes him a manager: her choice now
            Assert.IsTrue(b.Grant("h0", null, "max", "zoe", "Zoe", HousingAccess.Storage, t, out why), why);
            Assert.IsTrue(b.Grant("h0", null, "ann", "mia", "Mia", HousingAccess.None, t, out why), why);
            Assert.AreEqual(HousingAccess.All, b.AccessOf("h0", null, "max", t), "the tenant's own choice stays");
            Assert.AreEqual(HousingAccess.Storage, b.AccessOf("h0", null, "zoe", t), "and those he let in");
            Assert.IsTrue(b.Grant("h0", null, "ann", "max", "Max", HousingAccess.None, t, out why), why);
            Assert.AreEqual(HousingAccess.None, b.AccessOf("h0", null, "zoe", t), "removing a manager removes those under them");
        }

        [Test]
        public void APartialClaimPaysTheFeeOnce()
        {
            HousingRules r = Rules();
            r.held.fee = 5;
            HousingBook b = Book(r, 1);
            var stacks = new List<HeldStack> { new HeldStack { itemId = "a", count = 1 }, new HeldStack { itemId = "b", count = 1 } };
            long id = b.Hold("ann", "Ann", "Room 1", stacks, 0.0);
            double late = r.held.freeDays + 1.0;
            b.Advance(late, DateTime.UtcNow.Ticks);
            Assert.AreEqual(5, b.ClaimFee(b.Held(id), late));
            b.Claimed(id, new List<HeldStack> { stacks[1] }); // paid, and took what fitted
            Assert.AreEqual(0, b.ClaimFee(b.Held(id), late), "the rest is paid for");
        }

        [Test]
        public void ThingsThrownAwayAreToldEvenToThoseAway()
        {
            HousingRules r = Rules();
            r.held.keepDays = 2f;
            HousingBook b = Book(r, 1);
            b.Hold("ann", "Ann", "Room 1", new List<HeldStack> { new HeldStack { itemId = "a", count = 1 } }, 0.0);
            b.Advance(3.0, DateTime.UtcNow.Ticks);
            Assert.IsEmpty(b.HeldFor("ann"));
            Assert.IsTrue(b.Account("ann").notices.Any(n => n.text.Contains("thrown away")), "kept in her messages");
        }

        // ---------------------------------------------------------------- the second review: geometry
        [Test]
        public void PlotsFarFromTheOriginThatShareAFenceDoNotOverlap()
        {
            List<Vector2> a = HousingGeometry.Rectangle(Vector2.zero, new Vector2(120f, 30f));
            List<Vector2> b = HousingGeometry.Rectangle(Vector2.zero, new Vector2(120f, 30f));
            var rnd = new System.Random(7);
            for (int i = 0; i < 200; i++)
            {
                float yaw = (float)(rnd.NextDouble() * 360.0);
                Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
                var posA = new Vector3(4000f + (float)rnd.NextDouble(), 0f, -3000f);
                Vector3 posB = posA + turn * new Vector3(0f, 0f, 30f); // the next plot along, sharing its long fence
                List<Vector2> bInA = HousingGeometry.Relative(b, posB, turn, posA, turn);
                // Compared in one plot's own space (no world rounding), within what the floats of their positions allow.
                Assert.Less(HousingGeometry.SharedArea(a, bInA), HousingGeometry.TouchingSlack(a, bInA, posA, posB), $"turned {yaw:0.0} degrees");
                Assert.Greater(HousingGeometry.SharedArea(a, HousingGeometry.Relative(b, posA + turn * new Vector3(0f, 0f, 29f), turn, posA, turn)), 100f,
                    "a metre over the fence is an overlap");
            }
        }

        [Test]
        public void ARepeatedPointDoesNotMakeAnOutlineCrossItself()
        {
            var l = new List<Vector2> { new Vector2(0, 0), new Vector2(4, 0), new Vector2(4, 0), new Vector2(4, 4), new Vector2(0, 4), new Vector2(0, 0) };
            Assert.IsTrue(HousingGeometry.IsSimple(l));
            Assert.AreEqual(16f, HousingGeometry.Area(l), 1e-4f);
        }

        [Test]
        public void ATurnedCombIsCutIntoTheRightTriangles()
        {
            var comb = new List<Vector2>
            {
                new Vector2(0, 0), new Vector2(18, 0), new Vector2(18, 10), new Vector2(15, 10), new Vector2(15, 4), new Vector2(12, 4), new Vector2(12, 10),
                new Vector2(9, 10), new Vector2(9, 4), new Vector2(6, 4), new Vector2(6, 10), new Vector2(3, 10), new Vector2(3, 4), new Vector2(0, 4),
            };
            for (float deg = 0f; deg < 360f; deg += 0.5f)
            {
                List<Vector2> turned = HousingGeometry.ToWorld(comb, Vector3.zero, Quaternion.Euler(0f, deg, 0f));
                float area = HousingGeometry.Area(turned);
                Assert.AreEqual(126f, area, 0.01f);
                Assert.AreEqual(area, HousingGeometry.Triangulate(turned).Sum(tr => HousingGeometry.Area(tr)), 0.01f, $"turned {deg} degrees");
                Assert.AreEqual(area, HousingGeometry.SharedArea(turned, turned), 0.02f, $"turned {deg} degrees");
            }
        }
            [Test]
        public void AnOrderTakenBackStandsUntilItsNoticeEnds()
        {
            HousingRules r = Rules();
            r.market.firstClearingDelayHours = 24f;
            HousingBook b = Book(r, 2);
            Order(b, "ann", 0.0, ("h0", 10));
            Order(b, "bob", 0.0, ("h1", 10));
            double t = Run(b, 0.0, 1.5);
            Assert.IsNotNull(b.LeaseOf("h0", "ann"));
            Assert.IsTrue(b.Deposited("ann", "Ann", b.DepositRoom("ann")));
            Assert.IsTrue(b.Deposited("bob", "Bob", b.DepositRoom("bob")));
            // Alt places an order on h0 well above its asking (it cannot beat her protection), then sees she will not renew.
            Order(b, "alt", t, ("h0", 15), ("h1", 15));
            PropertyRecord p = b.LeaseOf("h0", "ann");
            Assert.IsTrue(b.SetRenewal("h0", "ann", false, 0f, t, out string why), why);
            double end = p.lease.end;
            b.Advance(end - 0.9, DateTime.UtcNow.Ticks);
            Assert.IsTrue(b.StatusOf("h0", "alt", end - 0.9).comesFree);
            // It tries to pull h0 out of its order at once - too late: the change waits for the notice.
            var keep = new List<OrderItem> { new OrderItem { property = "h1", term = 0, rateMilli = 15000 } };
            OrderQuote q = b.QuoteOrder("alt", keep, end - 0.9);
            Assert.IsTrue(q.ok && q.later, "dropping a house takes effect later");
            Assert.IsTrue(b.PlaceOrder("alt", "Alt", keep, Math.Max(0, q.delta), end - 0.9, out why), why);
            b.Advance(end + 1.5, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h0", "alt"), "its offer still stood when the house came free: it took it at its word");
        }

        [Test]
        public void OfferingMoreTakesEffectAtOnce()
        {
            HousingBook b = Book(Rules(), 2);
            Order(b, "ann", 0.0, ("h0", 10));
            var more = new List<OrderItem> { new OrderItem { property = "h0", term = 0, rateMilli = 12000 }, new OrderItem { property = "h1", term = 0, rateMilli = 11000 } };
            OrderQuote q = b.QuoteOrder("ann", more, 0.01);
            Assert.IsTrue(q.ok && !q.later, "adding a house and offering more: at once");
            Assert.AreEqual(84 - 70, q.delta, "the deposit grows to the dearest item");
        }

        // ---------------------------------------------------------------- the third review: market
        [Test]
        public void AnOfferARentRestsOnCannotEscapeByWinningAnotherHouse()
        {
            foreach (float delay in new[] { 24f, 0f }) Escape(delay);
        }

        private static void Escape(float delayHours)
        {
            HousingRules r = Rules();
            r.market.firstClearingDelayHours = delayHours;
            HousingBook b = Book(r, 2);
            Order(b, "vic", 0.0, ("h0", 10));
            double t = Run(b, 0.0, 1.5);
            PropertyRecord p = b.LeaseOf("h0", "vic");
            Assert.IsNotNull(p, "vic rents h0");
            // An alt lifts h0 with an offer it means to escape; vic decides not to renew (nobody knows yet).
            Order(b, "alt", t, ("h0", 16));
            Assert.IsTrue(b.SetRenewal("h0", "vic", false, 0f, t, out string why), why);
            double end = p.lease.end;
            double notice = end - r.market.renewalNoticeHours / 24.0;
            b.Advance(notice + 0.001, DateTime.UtcNow.Ticks);
            Assert.Greater(b.AskingMilli("h0"), 10000, "the alt's offer lifted h0");
            Assert.IsTrue(b.StatusOf("h0", "alt", notice + 0.001).comesFree, "h0 comes free: everyone sees it now");
            Assert.AreEqual("h0", b.OrderOf("alt").pin, "its price rests on the alt's offer: the alt's order is held for h0");
            // The alt adds a cheap free house, first, to win at once - which would end its order, and its offer for h0 with it.
            long cheap = b.AskingMilli("h1");
            var escape = new List<OrderItem> { new OrderItem { property = "h1", term = 0, rateMilli = cheap }, new OrderItem { property = "h0", term = 0, rateMilli = 16000 } };
            OrderQuote q = b.QuoteOrder("alt", escape, notice + 0.002);
            Assert.IsTrue(q.ok && !q.later, "adding a house takes effect at once");
            Assert.IsTrue(b.PlaceOrder("alt", "Alt", escape, Math.Max(0, q.delta), notice + 0.002, out why), why);
            b.Advance(end + 1.5, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h0", "alt"), $"held for h0, it won nothing else first: at h0's first clearing its offer took it (delay {delayHours} h)");
            Assert.AreEqual(16000, b.LeaseOf("h0", "alt").lease.rateMilli, "at what it offered");
            Assert.IsNull(b.LeaseOf("h1", "alt"), "the cheap house was no way out");
        }

        [Test]
        public void ATenantWhoWinsAnotherHouseAtTheirLeaseEndHandsTheirsToNobody()
        {
            HousingRules r = Rules();
            r.market.firstClearingDelayHours = 24f;
            HousingBook b = Book(r, 2);
            Order(b, "vic", 0.0, ("h0", 10));
            double t = Run(b, 0.0, 1.5);
            PropertyRecord p = b.LeaseOf("h0", "vic");
            Assert.IsNotNull(p);
            Assert.IsTrue(b.Deposited("vic", "Vic", b.DepositRoom("vic")));
            double end = p.lease.end;
            Order(b, "fred", t, ("h0", 10)); // her friend, at the asking rent: far within her protection
            b.Advance(end - 0.1, DateTime.UtcNow.Ticks);
            Assert.IsTrue(p.noticed && p.reserved >= p.renewTotal, "her renewal is fixed and paid for");
            Assert.IsFalse(b.StatusOf("h0", "carol", end - 0.1).comesFree, "nobody is told it comes free");
            // Hours before her lease ends she orders the free house next door: she wins it at her lease end's clearing.
            Order(b, "vic", end - 0.1, ("h1", (b.AskingMilli("h1") + 999) / 1000));
            long asking = b.AskingMilli("h0");
            long balance = b.Account("vic").balance;
            b.Advance(end + 0.001, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h1", "vic"), "she moved");
            Assert.IsNull(b.LeaseOf("h0", "fred"), "her house is not handed to her friend at its lease end");
            Assert.IsTrue(b.StatusOf("h0", "carol", end + 0.001).vacant, "it is free, for everyone to see");
            Assert.AreEqual(asking, b.AskingMilli("h0"), "asking what it did: it was not offered, so it was not left unlet");
            Assert.Greater(b.Account("vic").balance, balance, "her renewal is back in her rent account");
            Order(b, "carol", end + 0.1, ("h0", 12));
            b.Advance(end + 1.2, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h0", "carol"), "a day later the best offer took it");
        }

        [Test]
        public void EveryLeaseRenewsOnItsOwnUnderMaxHouses()
        {
            HousingRules r = Rules();
            r.market.maxHouses = 2;
            HousingBook b = Book(r, 2);
            Order(b, "ann", 0.0, ("h0", 10));
            double t = ToNextClearing(b, 0.0);
            Order(b, "ann", t, ("h1", 10));
            t = ToNextClearing(b, t);
            PropertyRecord a = b.LeaseOf("h0", "ann"), c = b.LeaseOf("h1", "ann");
            Assert.IsNotNull(a);
            Assert.IsNotNull(c);
            r.market.tickHours = 24f; // the leases snap onto the new grid: both now end at the same clearing
            t = ToNextClearing(b, t);
            Assert.AreEqual(a.lease.end, c.lease.end, 1e-9, "both end at the same clearing");
            Assert.IsTrue(b.Deposited("ann", "Ann", b.DepositRoom("ann")));
            double end = a.lease.end;
            b.Advance(end + 0.01, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h0", "ann"), "the first renewed");
            Assert.IsNotNull(b.LeaseOf("h1", "ann"), "and the second too: each renewal holds its own house");
            Assert.Greater(b.LeaseOf("h1", "ann").lease.end, end + 1.0);
        }

        [Test]
        public void APlayerBelowTheLimitWhoWinsAtTheirLeaseEndKeepsTheirHouse()
        {
            HousingRules r = Rules();
            r.market.maxHouses = 2;
            HousingBook b = Book(r, 2);
            Order(b, "ann", 0.0, ("h0", 10));
            ToNextClearing(b, 0.0);
            PropertyRecord p = b.LeaseOf("h0", "ann");
            Assert.IsTrue(b.Deposited("ann", "Ann", b.DepositRoom("ann")));
            double end = p.lease.end;
            b.Advance(end - 0.1, DateTime.UtcNow.Ticks);
            Order(b, "ann", end - 0.1, ("h1", (b.AskingMilli("h1") + 999) / 1000));
            b.Advance(end + 0.01, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h1", "ann"), "she won a second house");
            Assert.IsNotNull(b.LeaseOf("h0", "ann"), "and kept her first: two are allowed");
            Assert.Greater(b.LeaseOf("h0", "ann").lease.end, end + 1.0, "renewed");
        }

        [Test]
        public void ARenewalNeverRestsOnAnOfferBeingTakenBack()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 1);
            Order(b, "vic", 0.0, ("h0", 10));
            double t = ToNextClearing(b, 0.0);
            PropertyRecord p = b.LeaseOf("h0", "vic");
            Assert.IsTrue(b.Deposited("vic", "Vic", b.DepositRoom("vic")));
            Order(b, "alt", t, ("h0", 16));
            double notice = p.lease.end - r.market.renewalNoticeHours / 24.0;
            t = Run(b, t, notice - 0.6 - t);
            Assert.Greater(b.AskingMilli("h0"), 10500, "lifted");
            // Half a day before the renewal is fixed the alt withdraws - the withdrawal takes days, so its order still stands then.
            Assert.IsTrue(b.CancelOrder("alt", t, out _));
            b.Advance(notice + 0.001, DateTime.UtcNow.Ticks);
            Assert.IsTrue(p.noticed);
            Assert.IsNotNull(b.OrderOf("alt"), "the order stands (binding) until its notice ends");
            Assert.AreEqual(10000, p.renewRateMilli, "but the renewal rests on no offer being taken back");
            Assert.IsTrue(string.IsNullOrEmpty(b.OrderOf("alt").pin), "and the order is not held for the house");
        }

        [Test]
        public void AnOrderHeldForOneHouseHoldsUpNoOtherRent()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 2);
            Order(b, "v0", 0.0, ("h0", 10));
            double t = ToNextClearing(b, 0.0);
            Order(b, "v1", t, ("h1", 10));
            t = ToNextClearing(b, t); // h1's lease ends six hours after h0's
            Assert.IsTrue(b.Deposited("v0", "V0", b.DepositRoom("v0")));
            Assert.IsTrue(b.Deposited("v1", "V1", b.DepositRoom("v1")));
            Order(b, "alt", t, ("h0", 16), ("h1", 16));
            PropertyRecord p0 = b.LeaseOf("h0", "v0"), p1 = b.LeaseOf("h1", "v1");
            double notice = r.market.renewalNoticeHours / 24.0;
            b.Advance(p0.lease.end - notice + 0.001, DateTime.UtcNow.Ticks);
            Assert.Greater(p0.renewRateMilli, 10000, "h0's renewal rests on the alt's offer");
            Assert.AreEqual("h0", b.OrderOf("alt").pin, "so the alt's order is held for h0");
            b.Advance(p1.lease.end - notice + 0.001, DateTime.UtcNow.Ticks);
            Assert.AreEqual(10000, p1.renewRateMilli, "held for h0, the alt could not take h1 now: h1's rent rests on nothing it offers");
            b.Advance(p1.lease.end + 0.01, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h0", "v0"), "v0 renewed (the offer is within her protection)");
            Assert.IsNotNull(b.LeaseOf("h1", "v1"), "v1 renewed");
            Assert.IsTrue(string.IsNullOrEmpty(b.OrderOf("alt").pin), "h0 was offered: the order is free again");
        }

        [Test]
        public void TooMuchHeldStopsNewOrdersButNotOneThatStands()
        {
            HousingRules r = Rules();
            r.held.maxStacks = 1;
            HousingBook b = Book(r, 1);
            Order(b, "vic", 0.0, ("h0", 10));
            double t = ToNextClearing(b, 0.0);
            PropertyRecord p = b.LeaseOf("h0", "vic");
            Order(b, "alt", t, ("h0", 12));
            Assert.IsTrue(b.SetRenewal("h0", "vic", false, 0f, t, out string why), why);
            // The alt gets more held for it than allowed (a room it gave up), hoping its order would no longer count.
            b.Hold("alt", "Alt", "Room 1", new List<HeldStack> { new HeldStack { itemId = "a", count = 1 }, new HeldStack { itemId = "b", count = 1 } }, t);
            Assert.IsFalse(b.MayOrder("alt", out _), "it may not place a new order");
            b.Advance(p.lease.end + 0.3, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h0", "alt"), "but the order that stood is binding: it took the house that came free");
        }

        [Test]
        public void APremiumGoesWithTheOfferBehindIt()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 2);
            var items = new List<OrderItem> { new OrderItem { property = "h0", term = (int)LeaseTerm.Month, rateMilli = 10000 } };
            OrderQuote q = b.QuoteOrder("th0", items, 0.0);
            Assert.IsTrue(b.PlaceOrder("th0", "T", items, q.delta, 0.0, out string why), why);
            double t = ToNextClearing(b, 0.0); // a month's lease: no renewal in the days below
            Order(b, "w", t, ("h0", 13));
            t = Run(b, t, 5.0);
            Assert.Greater(b.AskingMilli("h0"), 11500, "lifted towards the offer");
            Order(b, "w", t, ("h0", 13), ("h1", (b.AskingMilli("h1") + 999) / 1000)); // it wins the free house next door
            t = ToNextClearing(b, t);
            Assert.IsNotNull(b.LeaseOf("h1", "w"), "the offer behind h0's premium won another house");
            Assert.AreEqual(10000, b.AskingMilli("h0"), "the premium went with it: h0 asks its base rent at once");
        }

        [Test]
        public void AHouseGivenUpAtTheLimitComesFreeWithItsBackersHeld()
        {
            HousingRules r = Rules();
            r.market.firstClearingDelayHours = 24f;
            HousingBook b = Book(r, 4);
            Order(b, "vic", 0.0, ("h0", 10));
            double t = Run(b, 0.0, 1.5);
            PropertyRecord p = b.LeaseOf("h0", "vic");
            Assert.IsTrue(b.Deposited("vic", "Vic", b.DepositRoom("vic")));
            Order(b, "alt", t, ("h0", 12));
            double end = p.lease.end, notice = end - r.market.renewalNoticeHours / 24.0;
            b.Advance(notice + 0.001, DateTime.UtcNow.Ticks);
            Assert.Greater(p.renewRateMilli, 10000, "vic's renewal rests on the alt's offer");
            Assert.AreEqual("h0", b.OrderOf("alt").pin, "so the alt's order is held for h0");
            // A later, higher offer lifts h0 while it is let; then vic wins another house at her lease end, giving h0 up -
            // and the alt adds a cheap free house, to win it at that clearing once vic's protection turns it away from h0.
            Order(b, "cat", notice + 0.01, ("h0", 15));
            b.Advance(end - 0.1, DateTime.UtcNow.Ticks);
            Order(b, "alt", end - 0.1, ("h0", 12), ("h3", (b.MinOfferMilli("h3") + 999) / 1000));
            Order(b, "vic", end - 0.1, ("h1", (b.AskingMilli("h1") + 999) / 1000));
            b.Advance(end + 0.001, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h1", "vic"), "vic moved");
            Assert.IsTrue(b.StatusOf("h0", "x", end + 0.001).vacant, "h0 came free (given up at the house limit)");
            Assert.AreEqual("h0", b.OrderOf("alt").pin, "the alt is still held for h0 until its first clearing");
            Assert.AreEqual(0, b.LeasesOf("alt").Count, "and won nothing else at the lease end");
            Assert.AreEqual("h0", b.OrderOf("cat").pin, "h0 came free at the price the later offer stands behind: that order is held too");
            // She ranks the free house next door first, to win it at h0's first clearing instead.
            double first = end + r.market.firstClearingDelayHours / 24.0;
            b.Advance(first - 0.01, DateTime.UtcNow.Ticks);
            var both = new List<OrderItem> { new OrderItem { property = "h2", term = 0, rateMilli = b.MinOfferMilli("h2") }, new OrderItem { property = "h0", term = 0, rateMilli = 15000 } };
            OrderQuote q = b.QuoteOrder("cat", both, first - 0.01);
            Assert.IsTrue(q.ok && !q.later, q.reason);
            Assert.IsTrue(b.PlaceOrder("cat", "Cat", both, Math.Max(0, q.delta), first - 0.01, out string why), why);
            b.Advance(first + 0.01, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h0", "cat"), "held, she was tried for h0 first: her offer, the best behind its price, took it");
            Assert.AreEqual(15000, b.LeaseOf("h0", "cat").lease.rateMilli);
            Assert.IsNull(b.LeaseOf("h2", "cat"), "the house she ranked first was no way out");
            Assert.IsNotNull(b.LeaseOf("h3", "alt"), "h0 went to a better offer: only then was the alt free to take its cheap house");
        }

        [Test]
        public void AHoldOutlastsAnUnloadedScene()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 2);
            Order(b, "vic", 0.0, ("h0", 10));
            double t = ToNextClearing(b, 0.0);
            PropertyRecord p = b.LeaseOf("h0", "vic");
            Assert.IsTrue(b.Deposited("vic", "Vic", b.DepositRoom("vic")));
            Order(b, "alt", t, ("h0", 15));
            double notice = p.lease.end - r.market.renewalNoticeHours / 24.0;
            b.Advance(notice + 0.001, DateTime.UtcNow.Ticks);
            Assert.AreEqual("h0", b.OrderOf("alt").pin, "held for h0");
            b.SetWorld(new List<PropertyFacts> { House("h1") }, null); // h0's scene is not loaded for a while
            double back = ToNextClearing(b, notice + 0.001);
            b.SetWorld(new List<PropertyFacts> { House("h0"), House("h1") }, null);
            Assert.AreEqual("h0", b.OrderOf("alt").pin, "a clearing without h0's scene does not end the hold");
            Order(b, "alt", back, ("h0", 15), ("h1", (b.MinOfferMilli("h1") + 999) / 1000)); // a cheap way out, added at once
            b.Advance(p.lease.end - 0.01, DateTime.UtcNow.Ticks);
            Assert.IsNull(b.LeaseOf("h1", "alt"), "held, it won nothing else before h0 was offered");
        }

        [Test]
        public void AHouseComingFreeAsksOnlyWhatStandsBehindIt()
        {
            HousingRules r = Rules();
            r.market.firstClearingDelayHours = 24f;
            HousingBook b = Book(r, 2);
            Order(b, "bob", 0.0, ("h0", 10));
            double t = Run(b, 0.0, 1.5);
            Order(b, "ann", t, ("h1", 10));
            t = ToNextClearing(b, t); // ann's lease ends a clearing after bob's
            Assert.IsTrue(b.Deposited("bob", "Bob", b.DepositRoom("bob")));
            PropertyRecord p0 = b.LeaseOf("h0", "bob"), p1 = b.LeaseOf("h1", "ann");
            Assert.IsNotNull(p1);
            Order(b, "alt", t, ("h0", 15), ("h1", 15)); // lifts both
            Assert.IsTrue(b.SetRenewal("h1", "ann", false, 0f, t, out string why), why);
            double notice = r.market.renewalNoticeHours / 24.0;
            b.Advance(p0.lease.end - notice + 0.001, DateTime.UtcNow.Ticks);
            Assert.AreEqual("h0", b.OrderOf("alt").pin, "the alt is held for h0");
            b.Advance(p1.lease.end - notice + 0.001, DateTime.UtcNow.Ticks);
            Assert.IsTrue(b.StatusOf("h1", "hal", p1.lease.end - notice + 0.001).comesFree);
            Assert.AreEqual(10000, b.MinOfferMilli("h1"), "h1 comes free: the only offer behind its premium is held for h0, so it asks its base rent");
            Assert.AreEqual(10000, b.AskingMilli("h1"), "and shows it");
        }

        [Test]
        public void OffersForALetHouseStartAtItsBaseRent()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 1);
            var items = new List<OrderItem> { new OrderItem { property = "h0", term = (int)LeaseTerm.Month, rateMilli = 10000 } };
            OrderQuote q = b.QuoteOrder("ten", items, 0.0);
            Assert.IsTrue(b.PlaceOrder("ten", "Ten", items, q.delta, 0.0, out string why), why);
            double t = ToNextClearing(b, 0.0);
            PropertyRecord p = b.LeaseOf("h0", "ten");
            Assert.IsTrue(b.Deposited("ten", "Ten", b.DepositRoom("ten")));
            // An alt offers three times the rent early in a month's lease - and withdraws before the house can ever be offered.
            Order(b, "alt", t, ("h0", 30));
            t = Run(b, t, 12.0);
            Assert.Greater(b.AskingMilli("h0"), 13000, "the standing offer lifted h0");
            Assert.AreEqual(10000, b.MinOfferMilli("h0"), "yet anyone may offer from its base rent: the lift is not the floor");
            Order(b, "bea", t, ("h0", 10)); // honest, at the base rent
            Assert.IsTrue(b.CancelOrder("alt", t, out _));
            b.Advance(p.lease.end - r.market.renewalNoticeHours / 24.0 + 0.001, DateTime.UtcNow.Ticks);
            Assert.AreEqual(10000, p.renewRateMilli, "the withdrawn offer left nothing behind: the renewal is the base rent");
        }

        [Test]
        public void ThePriceThatTakesAHouseAtItsLeaseEndIsPublic()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 1);
            Order(b, "fay", 0.0, ("h0", 10));
            double t = ToNextClearing(b, 0.0);
            PropertyRecord p = b.LeaseOf("h0", "fay");
            Assert.IsTrue(b.Deposited("fay", "Fay", b.DepositRoom("fay")));
            Assert.IsTrue(b.SetRenewal("h0", "fay", true, 1f, t, out string why), why); // the least protection
            Assert.AreEqual(0, b.TakeAtMilli("h0"), "not known before the renewal is fixed");
            double notice = p.lease.end - r.market.renewalNoticeHours / 24.0;
            b.Advance(notice + 0.001, DateTime.UtcNow.Ticks);
            long take = b.TakeAtMilli("h0");
            Assert.AreEqual(HousingTenure.BeatMilli(p.protectionMilli, r.market.protectionMargin) + 1, take, "from the notice, anyone sees what takes it");
            Order(b, "amy", notice + 0.01, ("h0", (take + 999) / 1000)); // her friend, at the least that takes it
            Order(b, "zed", notice + 0.02, ("h0", (take + 999) / 1000 + 1)); // anyone else may offer more
            b.Advance(p.lease.end + 0.01, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h0", "zed"), "the best offer above the public price took it, not the friend's");
        }

        [Test]
        public void AHeldOrderCannotTakeItsHouseBackBeforeTheHouseIsOffered()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 2);
            Order(b, "vic", 0.0, ("h0", 10));
            double t = ToNextClearing(b, 0.0);
            PropertyRecord p = b.LeaseOf("h0", "vic");
            Order(b, "alt", t, ("h0", 15));
            Assert.IsTrue(b.SetRenewal("h0", "vic", false, 0f, t, out string why), why);
            double notice = p.lease.end - r.market.renewalNoticeHours / 24.0;
            b.Advance(notice + 0.001, DateTime.UtcNow.Ticks);
            Assert.AreEqual("h0", b.OrderOf("alt").pin, "h0 comes free on the alt's offer: held for it");
            b.SetWorld(new List<PropertyFacts> { House("h1") }, null); // h0's scene is not loaded for days
            Assert.IsTrue(b.CancelOrder("alt", notice + 0.002, out why), why);
            b.Advance(notice + b.OrderNoticeDays + 1.0, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.OrderOf("alt"), "the notice is over, but h0 has not been offered: the withdrawal waits");
            Assert.AreEqual("h0", b.OrderOf("alt").pin);
            b.SetWorld(new List<PropertyFacts> { House("h0"), House("h1") }, null);
            b.Advance(notice + b.OrderNoticeDays + 1.3, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h0", "alt"), "back in the city, h0 was offered: the held offer took it");
        }

        [Test]
        public void AHouseThatTurnsToComeFreeAfterItsNoticeHoldsItsBacker()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 3);
            Order(b, "kim", 0.0, ("h0", 10));
            double t = ToNextClearing(b, 0.0);
            Order(b, "vic", t, ("h1", 10));
            t = ToNextClearing(b, t); // vic's lease ends a clearing after kim's
            Assert.IsTrue(b.Deposited("kim", "Kim", b.DepositRoom("kim")));
            Assert.IsTrue(b.Deposited("vic", "Vic", b.DepositRoom("vic")));
            Order(b, "alt", t, ("h0", 16), ("h1", 16)); // lifts both
            PropertyRecord k = b.LeaseOf("h0", "kim"), v = b.LeaseOf("h1", "vic");
            double ek = k.lease.end, ev = v.lease.end, notice = r.market.renewalNoticeHours / 24.0;
            b.Advance(ev - notice + 0.001, DateTime.UtcNow.Ticks);
            Assert.AreEqual("h0", b.OrderOf("alt").pin, "held for h0 (its notice came first)");
            Assert.AreEqual(10000, v.renewRateMilli, "so h1's renewal rests on nothing of it");
            b.Advance(ek + 0.001, DateTime.UtcNow.Ticks);
            Assert.IsTrue(string.IsNullOrEmpty(b.OrderOf("alt").pin), "kim renewed: the alt is free again");
            // Vic now stops renewing: h1 comes free at its lease end, at a price the alt's offer stands behind.
            Assert.IsTrue(b.SetRenewal("h1", "vic", false, 0f, ek + 0.002, out string why), why);
            Assert.IsTrue(b.StatusOf("h1", "cat", ek + 0.002).comesFree);
            Assert.AreEqual("h1", b.OrderOf("alt").pin, "the offer h1's price rests on is held to it now");
            Order(b, "alt", ek + 0.003, ("h0", 16), ("h1", 16), ("h2", (b.MinOfferMilli("h2") + 999) / 1000)); // a cheap way out
            b.Advance(ev + 0.3, DateTime.UtcNow.Ticks);
            Assert.IsNull(b.LeaseOf("h2", "alt"), "held, it won nothing else first");
            Assert.IsNotNull(b.LeaseOf("h1", "alt"), "at h1's first clearing its offer took it");
        }

        [Test]
        public void AHouseLeftForAnotherAfterItsNoticeHoldsItsBacker()
        {
            HousingRules r = Rules();
            HousingBook b = Book(r, 3);
            Order(b, "kim", 0.0, ("h0", 10));
            double t = ToNextClearing(b, 0.0);
            t = ToNextClearing(b, t);
            Order(b, "vic", t, ("h1", 10));
            t = ToNextClearing(b, t); // vic's lease ends two clearings after kim's
            Assert.IsTrue(b.Deposited("kim", "Kim", b.DepositRoom("kim")));
            Assert.IsTrue(b.Deposited("vic", "Vic", b.DepositRoom("vic")));
            Order(b, "alt", t, ("h0", 16), ("h1", 16));
            PropertyRecord k = b.LeaseOf("h0", "kim"), v = b.LeaseOf("h1", "vic");
            double ek = k.lease.end, ev = v.lease.end;
            Assert.AreEqual(0.5, ev - ek, 1e-9);
            b.Advance(ev - r.market.renewalNoticeHours / 24.0 + 0.001, DateTime.UtcNow.Ticks);
            Assert.AreEqual("h0", b.OrderOf("alt").pin, "held for h0 (its notice came first): h1's renewal rests on nothing of it");
            b.Advance(ek + 0.001, DateTime.UtcNow.Ticks);
            Assert.IsTrue(string.IsNullOrEmpty(b.OrderOf("alt").pin), "kim renewed: the alt is free again");
            // Vic wins the free house next door before her lease ends: she leaves h1 at its term.
            Order(b, "vic", ek + 0.002, ("h2", (b.MinOfferMilli("h2") + 999) / 1000));
            b.Advance(ek + 0.26, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h2", "vic"), "she won h2");
            Assert.IsTrue(b.LeaseOf("h1", "vic").movingOut, "and leaves h1 at its term");
            Assert.AreEqual("h1", b.OrderOf("alt").pin, "h1 comes free at a price the alt's offer stands behind: held to it");
            b.Advance(ev + 0.3, DateTime.UtcNow.Ticks);
            Assert.IsNotNull(b.LeaseOf("h1", "alt"), "at h1's first clearing its offer took it");
        }

        // ---------------------------------------------------------------- speed
        [Test]
        public void ALongJumpClearedOverManyFramesEndsTheSameAsAtOnce()
        {
            HousingBook Make()
            {
                var b = new HousingBook(Rules());
                var save = new HousingSave();
                save.market.secret = 0x5eed1234L | 1L; // the same draws in both
                b.Load(save);
                b.SetWorld(Enumerable.Range(0, 6).Select(i => House("h" + i)).ToList(), null);
                for (int i = 0; i < 4; i++) Order(b, "t" + i, 0.0, ("h" + i, 10));
                double t = ToNextClearing(b, 0.0);
                for (int i = 0; i < 4; i++) Assert.IsTrue(b.Deposited("t" + i, "T", b.DepositRoom("t" + i)));
                Order(b, "alt", t, ("h0", 15), ("h1", 14), ("h4", 10));
                Order(b, "bea", t, ("h1", 16), ("h5", 10));
                Assert.IsTrue(b.SetRenewal("h2", "t2", false, 0f, t, out _));
                return b;
            }
            HousingBook atOnce = Make(), spread = Make();
            atOnce.Advance(40.0, 1);
            spread.ClearingBudgetMs = 1e-6; // a clearing a frame
            int frames = 0;
            do { spread.Advance(40.0, 1); frames++; } while (spread.CatchingUp && frames < 10000);
            Assert.Greater(frames, 100, "the jump was cleared over many frames");
            Assert.IsFalse(spread.CatchingUp);
            Assert.AreEqual(UnityEngine.JsonUtility.ToJson(atOnce.Save), UnityEngine.JsonUtility.ToJson(spread.Save), "and ended exactly the same");
        }
    }
}
#endif
